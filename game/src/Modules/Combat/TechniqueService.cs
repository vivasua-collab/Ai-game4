#nullable enable
// Создано: 2026-05-09
// Редактировано: 2026-05-24 05:45:00 UTC — FIX CS1061: LearnedTechnique +CastTime/ArmorPenetration/BaseDamage/Element/IsUltimate; LearnTechnique расширён
// Редактировано: 2026-05-09 — CMB-A06: QiCost изменён с float на long (Fix-01)
// Редактировано: 2026-05-09 — EVT-01: убрана инъекция IQiService,
//   кросс-модульное общение через QiChangedEvent + QiConsumeRequestEvent
// Редактировано: 2026-08-23 — Этап 1-2 внедрения ЦИ:
//   слотовая модель TECHNIQUE_SYSTEM.md §12 (Cultivation 1, Combat 3+(L-1),
//   Curse 1, Formation 1), LearnTechnique(TechniqueData), выбор активной
//   техники, рост мастерства при использовании (§10), публикация
//   TechniqueLearnedEvent/TechniqueSelectionChangedEvent.
// Редактировано: 2026-08-28 — двухслойная модель «Библиотека + Лодаут»:
//   • БИБЛИОТЕКА (сколько техник ЗНАЕМ): единый cap, растёт с уровнем
//     культивации (LibraryCapacityBase = 8 + 2×(L−1)), расширяемый
//     (ExtraLibraryCapacity — задел под перки/предметы). Категории
//     Cultivation/Curse/Formation сохраняют персональные лимиты ×1;
//     Combat-пул §12 (3+(L−1)) больше НЕ ограничивает изучение — его роль
//     играет библиотечный cap (решение пользователя 2026-08-28).
//   • ЭХО МАСТЕРСТВА («осмысление», решение пользователя): при забвении
//     15% мастерства уходит в эхо профиля (тип+стихия); при изучении новой
//     техники того же профиля — стартовое мастерство из эха (поглощается,
//     потолок 50).
//   • СВИТКИ ТЕХНИК: запись базовой (без мастерства) техники на свиток
//     (стоимость 2×QiCost), изучение со свитка обходит окно резонанса §8.1
//     (нишевые цели: вернуть старую технику), свиток расходуется.
//   • ISaveable: изученные техники + выбранная + эхо + свитки (раньше в сейв
//     уходили только слоты — рассинхрон при загрузке).
// Сервис управления техниками — изучение, применение, отслеживание кулдаунов.
using System;
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Combat
{
    /// <summary>
    /// Сервис управления техниками.
    /// Отслеживает изученные техники, кулдауны и стоимость Ци.
    ///
    /// АРХИТЕКТУРА: TechniqueService НЕ инжектит IBodyService, IQiService.
    /// Межмодульное общение — через EventBus (TechniqueUsedEvent, QiConsumeRequestEvent).
    ///
    /// СЛОТЫ (TECHNIQUE_SYSTEM.md §12):
    ///   Cultivation 1 | Combat 3+(L-1) | Curse 1 | Formation 1.
    ///   Все активные типы (Defense/Support/Healing/Movement/Sensory/Poison)
    ///   занимают слоты пула Combat.
    ///
    /// CMB-A06: LearnedTechnique.QiCost — long (Fix-01: все Qi-значения long).
    /// EVT-01: кэш Qi из QiChangedEvent вместо инъекции IQiService.
    /// </summary>
    public class TechniqueService : IDisposable, ISaveable
    {
        // === Библиотека (2026-08-28) ===

        /// <summary>Базовый размер библиотеки на L1. L1=8 … L9=24.</summary>
        public const int LibraryBaseSize = 8;

        /// <summary>Прирост библиотеки за уровень культивации.</summary>
        public const int LibraryPerLevel = 2;

        /// <summary>Доля мастерства, уходящая в эхо при забвении («осмысление»).</summary>
        public const float EchoTransferRatio = 0.15f;

        /// <summary>Потолок стартового мастерства из эха (и самого эха).</summary>
        public const float EchoMasteryCap = 50f;

        /// <summary>Множитель стоимости Ци записи техники на свиток.</summary>
        public const long ScrollQiCostMultiplier = 2;

        /// <summary>
        /// Внешний прирост ёмкости библиотеки (перки/предметы в будущем).
        /// </summary>
        public int ExtraLibraryCapacity;

        // === Зависимости ===
        private readonly IPublisher<TechniqueUsedEvent> _techniqueUsedPub;
        private readonly IPublisher<TechniqueLearnedEvent> _techniqueLearnedPub;
        private readonly IPublisher<TechniqueForgottenEvent> _techniqueForgottenPub;
        private readonly IPublisher<TechniqueSelectionChangedEvent> _techniqueSelectionPub;

        // EVT-01: подписки на кросс-модульные события (вместо инъекции IQiService)
        private readonly ISubscriber<QiChangedEvent> _qiChangedSub;
        private readonly IPublisher<QiConsumeRequestEvent> _qiConsumeRequestPub;

        // EVT-01: кэш состояния из событий
        private int _cachedCultivationLevel = 1;
        private string _cachedEntityId = "";
        private long _cachedCurrentQi;

        // IDisposable для подписок
        private IDisposable _qiChangedSubscription;

        // === Состояние ===
        private readonly Dictionary<string, LearnedTechnique> _learnedTechniques = new();
        private readonly Dictionary<string, float> _cooldowns = new();
        private readonly List<string> _orderedIds = new(); // порядок изучения (для UI/цикла выбора)
        private string? _selectedTechniqueId;

        // Эхо мастерства: ключ "{(int)Type}:{(int)Element}" → накопленное осмысление.
        private readonly Dictionary<string, float> _masteryEcho = new();

        // Реестр свитков: ScrollId → базовый снимок TechniqueData (Mastery=0).
        private readonly Dictionary<string, TechniqueData> _scrolls = new();

        // === Конструктор ===
        public TechniqueService(
            IPublisher<TechniqueUsedEvent> techniqueUsedPub,
            IPublisher<TechniqueLearnedEvent> techniqueLearnedPub,
            IPublisher<TechniqueForgottenEvent> techniqueForgottenPub,
            IPublisher<TechniqueSelectionChangedEvent> techniqueSelectionPub,
            ISubscriber<QiChangedEvent> qiChangedSub,
            IPublisher<QiConsumeRequestEvent> qiConsumeRequestPub)
        {
            _techniqueUsedPub = techniqueUsedPub;
            _techniqueLearnedPub = techniqueLearnedPub;
            _techniqueForgottenPub = techniqueForgottenPub;
            _techniqueSelectionPub = techniqueSelectionPub;
            _qiChangedSub = qiChangedSub;
            _qiConsumeRequestPub = qiConsumeRequestPub;

            // EVT-01: подписка на кэш состояния Ци
            _qiChangedSubscription = _qiChangedSub.Subscribe((in QiChangedEvent e) => {
                _cachedCurrentQi = e.Current;
                _cachedCultivationLevel = e.CultivationLevel;
                _cachedEntityId = e.EntityId;
            });
        }

        // === Выбор активной техники ===

        /// <summary>
        /// Этап 5: внешний бонус урона техник (пермил, ЗАПРЕТ 3.9).
        /// Источник — активная формация Amplification в зоне игрока.
        /// 0 = нет бонуса; 1300 = +30%. Применяется в CombatService.GetTechniqueDamage.
        /// </summary>
        public int ExternalDamageBonusPermil;

        /// <summary>ID выбранной техники (null — не выбрана).</summary>
        public string? SelectedTechniqueId => _selectedTechniqueId;

        /// <summary>Выбранная техника (null — не выбрана).</summary>
        public LearnedTechnique? SelectedTechnique
            => _selectedTechniqueId != null && _learnedTechniques.TryGetValue(_selectedTechniqueId, out var t) ? t : null;

        /// <summary>Выбрать активную технику (для каста по R/клику). null — сброс.</summary>
        public void SelectTechnique(string? techniqueId)
        {
            if (techniqueId != null && !_learnedTechniques.ContainsKey(techniqueId)) return;
            _selectedTechniqueId = techniqueId;
            _techniqueSelectionPub.Publish(new TechniqueSelectionChangedEvent(techniqueId));
        }

        /// <summary>Циклический выбор следующей кастуемой техники (Q).</summary>
        public void CycleSelection()
        {
            if (_orderedIds.Count == 0) return;
            int idx = _selectedTechniqueId != null ? _orderedIds.IndexOf(_selectedTechniqueId) : -1;
            string next = _orderedIds[(idx + 1) % _orderedIds.Count];
            SelectTechnique(next);
        }

        // === Слоты (TECHNIQUE_SYSTEM.md §12) ===

        /// <summary>Категория слота для типа техники.</summary>
        public static TechniqueType SlotCategory(TechniqueType type)
        {
            return type switch
            {
                TechniqueType.Cultivation => TechniqueType.Cultivation,
                TechniqueType.Curse => TechniqueType.Curse,
                TechniqueType.Formation => TechniqueType.Formation,
                // Defense/Support/Healing/Movement/Sensory/Poison/Combat → пул Combat
                _ => TechniqueType.Combat
            };
        }

        /// <summary>Ёмкость слотов категории для уровня культивации (§12).</summary>
        public static int SlotCapacity(TechniqueType category, int cultivationLevel)
        {
            int level = Math.Max(1, cultivationLevel);
            return category switch
            {
                TechniqueType.Cultivation => 1,
                TechniqueType.Curse => 1,
                TechniqueType.Formation => 1,
                TechniqueType.Combat => 3 + (level - 1),
                _ => 3 + (level - 1)
            };
        }

        /// <summary>Сколько техник категории занято.</summary>
        public int UsedSlots(TechniqueType type)
        {
            var category = SlotCategory(type);
            int used = 0;
            foreach (var t in _learnedTechniques.Values)
                if (SlotCategory(t.Type) == category) used++;
            return used;
        }

        /// <summary>Сколько свободных слотов для категории типа.</summary>
        public int FreeSlots(TechniqueType type)
        {
            return SlotCapacity(SlotCategory(type), _cachedCultivationLevel) - UsedSlots(type);
        }

        // === Библиотека (2026-08-28: единый cap вместо Combat-пула §12) ===

        /// <summary>Базовая ёмкость библиотеки техник для уровня культивации.</summary>
        public static int LibraryCapacityBase(int cultivationLevel)
        {
            int level = Math.Max(1, cultivationLevel);
            return LibraryBaseSize + (level - 1) * LibraryPerLevel;
        }

        /// <summary>Текущая полная ёмкость библиотеки (база + расширения).</summary>
        public int LibraryCapacity => LibraryCapacityBase(_cachedCultivationLevel) + ExtraLibraryCapacity;

        /// <summary>Сколько техник изучено (вся библиотека, включая культ/проклятия/формации).</summary>
        public int LibraryUsed => _learnedTechniques.Count;

        /// <summary>Сколько свободных мест в библиотеке.</summary>
        public int LibraryFree => LibraryCapacity - LibraryUsed;

        /// <summary>Окно резонанса §8.1: минимальный изучаемый уровень для текущего L.</summary>
        public int ResonanceMinLevel => Math.Max(1, _cachedCultivationLevel - 4);

        // === Изучение ===

        /// <summary>
        /// Изучить сгенерированную технику (TechniqueData из TechniqueGeneratorService).
        /// Проверяет: ёмкость библиотеки (единый cap), уровень резонанса
        /// (§8.1: L_техники ≤ L_практика и ≥ max(1, L_практика − 4)),
        /// персональные лимиты категорий Cultivation/Curse/Formation (×1).
        /// При наличии эха мастерства того же профиля (тип+стихия) — стартовое
        /// мастерство из эха (поглощается).
        /// </summary>
        public bool LearnTechnique(TechniqueData data)
        {
            return LearnCore(data, fromScroll: false);
        }

        /// <summary>Общий путь изучения (обычный и со свитка).</summary>
        private bool LearnCore(TechniqueData data, bool fromScroll)
        {
            if (data == null || string.IsNullOrEmpty(data.TechniqueId)) return false;
            if (_learnedTechniques.ContainsKey(data.TechniqueId)) return false;

            // Ограничение уровня (TECHNIQUE_SYSTEM.md §8.1 — Резонанс Ци).
            // Свиток — доказательство постижения: обход окна резонанса
            // (нишевая цель свитков — вернуть старую технику выше окна).
            if (!fromScroll)
            {
                int minL = ResonanceMinLevel;
                if (data.Level > _cachedCultivationLevel || data.Level < minL) return false;
            }

            // Персональные лимиты категорий (§12): одна техника культивации,
            // одно проклятие, одна формация. Combat-пул больше не ограничивает
            // изучение — его роль играет ёмкость библиотеки.
            if (data.Type == TechniqueType.Cultivation
                || data.Type == TechniqueType.Curse
                || data.Type == TechniqueType.Formation)
            {
                if (UsedSlots(data.Type) >= 1) return false;
            }

            // Ёмкость библиотеки — «разум культиватора не безграничен».
            if (LibraryUsed + 1 > LibraryCapacity) return false;

            var learned = new LearnedTechnique
            {
                TechniqueId = data.TechniqueId,
                Name = data.NameRu,
                Type = data.Type,
                Grade = data.Grade,
                Subtype = data.Subtype,
                Level = data.Level,
                Element = data.Element,
                QiCost = data.QiCost,
                // B2 (2026-08-26): фикс бага — CapacityCost не копировался из TechniqueData,
                // из-за чего TechniqueChargeService брал fallback (qiCost как capacity),
                // и окно перезарядки [qiCost..capacity] схлопывалось → potency 1001-2000
                // (overcharge, Stage 2) был недостижим.
                CapacityCost = data.CapacityCost,
                Cooldown = data.Cooldown,
                CastTime = data.CastTime,
                Range = data.Range,
                ArmorPenetration = data.ArmorPenetration,
                BaseDamage = data.BaseDamage,
                IsUltimate = data.IsUltimate,
                Mastery = data.Mastery
            };

            // Эхо мастерства: «осмысление» профиля (тип+стихия) даёт новой
            // технике того же профиля стартовое мастерство (поглощается).
            string echoKey = EchoKey(data.Type, data.Element);
            if (_masteryEcho.TryGetValue(echoKey, out float echoBonus) && echoBonus > 0f)
            {
                learned.Mastery = MathF.Min(EchoMasteryCap, MathF.Max(learned.Mastery, echoBonus));
                _masteryEcho.Remove(echoKey);
            }

            _learnedTechniques[data.TechniqueId] = learned;
            _orderedIds.Add(data.TechniqueId);

            // Авто-выбор первой изученной техники
            if (_selectedTechniqueId == null) SelectTechnique(data.TechniqueId);

            _techniqueLearnedPub.Publish(new TechniqueLearnedEvent(
                data.TechniqueId, data.NameRu, data.Type, data.Grade));
            return true;
        }

        /// <summary>
        /// Забыть технику (освободить место в библиотеке).
        /// Мастерство не пропадает полностью: 15% уходит в «эхо осмысления»
        /// профиля (тип+стихия) и станет стартовым мастерством следующей
        /// техники того же профиля (решение пользователя 2026-08-28).
        /// </summary>
        public bool ForgetTechnique(string techniqueId)
        {
            if (!_learnedTechniques.TryGetValue(techniqueId, out var tech)) return false;

            // Эхо мастерства (до удаления — нужны Type/Element/Mastery).
            if (tech.Mastery > 0f)
            {
                string key = EchoKey(tech.Type, tech.Element);
                float acc = _masteryEcho.TryGetValue(key, out var cur) ? cur : 0f;
                _masteryEcho[key] = MathF.Min(EchoMasteryCap, acc + tech.Mastery * EchoTransferRatio);
            }

            _learnedTechniques.Remove(techniqueId);
            _orderedIds.Remove(techniqueId);
            _cooldowns.Remove(techniqueId);
            if (_selectedTechniqueId == techniqueId)
            {
                _selectedTechniqueId = _orderedIds.Count > 0 ? _orderedIds[0] : null;
                _techniqueSelectionPub.Publish(new TechniqueSelectionChangedEvent(_selectedTechniqueId));
            }
            _techniqueForgottenPub.Publish(new TechniqueForgottenEvent(techniqueId));
            return true;
        }

        /// <summary>Забыть все техники (тест-режим/читы). Свитки и эхо сохраняются.</summary>
        public void ForgetAll()
        {
            foreach (var id in _orderedIds.ToArray())
                ForgetTechnique(id);
        }

        /// <summary>Полный сброс библиотеки: техники + свитки + эхо (читы/тесты).</summary>
        public void ForgetAllWithLibrary()
        {
            ForgetAll();
            _scrolls.Clear();
            _masteryEcho.Clear();
        }

        // === Эхо мастерства (осмысление) ===

        /// <summary>Ключ эха для профиля (тип+стихия).</summary>
        public static string EchoKey(TechniqueType type, Element element)
            => $"{(int)type}:{(int)element}";

        /// <summary>Накопленное эхо осмысления для профиля (0 — нет).</summary>
        public float GetMasteryEcho(TechniqueType type, Element element)
            => _masteryEcho.TryGetValue(EchoKey(type, element), out var v) ? v : 0f;

        // === Свитки техник (2026-08-28) ===

        /// <summary>Все записанные свитки.</summary>
        public IReadOnlyCollection<TechniqueData> GetAllScrolls() => _scrolls.Values;

        /// <summary>Стоимость Ци записи техники на свиток (2×QiCost).</summary>
        public static long ScrollQiCost(LearnedTechnique tech) => tech.QiCost * ScrollQiCostMultiplier;

        /// <summary>Есть ли уже свиток этой техники.</summary>
        public bool HasScrollFor(string techniqueId) => _scrolls.ContainsKey(ScrollIdFor(techniqueId));

        /// <summary>ID свитка для техники.</summary>
        public static string ScrollIdFor(string techniqueId) => "scroll_" + techniqueId;

        /// <summary>
        /// Записать базовую (не улучшенную) версию изученной техники на свиток.
        /// Мастерство на свиток НЕ пишется (наработанное — только в памяти практика).
        /// Стоимость Ци = 2×QiCost (списание через QiConsumeRequestEvent, EVT-01).
        /// Вызывающий UI обязан заранее проверить достаточность Ци.
        /// </summary>
        public bool InscribeScroll(string techniqueId)
        {
            if (!_learnedTechniques.TryGetValue(techniqueId, out var tech)) return false;
            string scrollId = ScrollIdFor(techniqueId);
            if (_scrolls.ContainsKey(scrollId)) return false; // уже записана

            // Списание Ци (EVT-01: событие, не инъекция IQiService).
            long cost = ScrollQiCost(tech);
            if (cost > 0)
                _qiConsumeRequestPub.Publish(new QiConsumeRequestEvent(cost, "InscribeScroll"));

            // Базовый снимок: всё, кроме мастерства (и грейд остаётся — он
            // внутреннее свойство сгенерированной техники, не «улучшение»).
            _scrolls[scrollId] = new TechniqueData
            {
                TechniqueId = tech.TechniqueId,
                NameRu = tech.Name,
                NameEn = tech.Name,
                Description = "Свиток техники: базовая форма без наработанного мастерства.",
                Type = tech.Type,
                Subtype = tech.Subtype,
                Grade = tech.Grade,
                Element = tech.Element,
                Level = tech.Level,
                CapacityCost = tech.CapacityCost,
                QiCost = tech.QiCost,
                BaseDamage = tech.BaseDamage,
                Cooldown = tech.Cooldown,
                Range = tech.Range,
                CastTime = tech.CastTime,
                IsUltimate = tech.IsUltimate,
                Mastery = 0f,
                ArmorPenetration = tech.ArmorPenetration,
            };
            return true;
        }

        /// <summary>
        /// Изучить технику со свитка. Обходит окно резонанса (свиток —
        /// доказательство постижения), но уважает ёмкость библиотеки,
        /// лимиты категорий и дубликаты. Свиток РАСХОДУЕТСЯ при изучении.
        /// </summary>
        public bool LearnFromScroll(string scrollId)
        {
            if (!_scrolls.TryGetValue(scrollId, out var data)) return false;
            if (!LearnCore(data, fromScroll: true)) return false;
            _scrolls.Remove(scrollId);
            return true;
        }

        /// <summary>Свиток по ID (null — нет).</summary>
        public TechniqueData? GetScroll(string scrollId)
            => _scrolls.TryGetValue(scrollId, out var d) ? d : null;

        /// <summary>
        /// Изучить технику (legacy-сигнатура по компонентам).
        /// CMB-A06: qiCost — long (Fix-01).
        /// </summary>
        public bool LearnTechnique(string techniqueId, TechniqueType type, TechniqueGrade grade,
            CombatSubtype subtype, long qiCost, float cooldown,
            float castTime = 0.5f, int armorPenetration = 0, int baseDamage = 0,
            Element element = Element.Neutral, bool isUltimate = false)
        {
            var data = new TechniqueData
            {
                TechniqueId = techniqueId,
                NameRu = techniqueId,
                Type = type,
                Grade = grade,
                Subtype = subtype,
                Level = _cachedCultivationLevel,
                Element = element,
                QiCost = qiCost,
                Cooldown = cooldown,
                CastTime = castTime,
                ArmorPenetration = armorPenetration,
                BaseDamage = baseDamage,
                IsUltimate = isUltimate
            };
            return LearnTechnique(data);
        }

        // === Использование ===

        /// <summary>
        /// Использовать технику (legacy — мгновенный расход Ци).
        /// CMB-A06: QiCost — long, каст не нужен.
        /// EVT-01: проверка Ци из кэша + QiConsumeRequestEvent вместо IQiService.TryConsumeQi.
        /// Этап 1: рост мастерства при использовании (§10: mastery = min(100, +0.01)).
        /// </summary>
        public bool UseTechnique(string techniqueId)
        {
            if (!_learnedTechniques.TryGetValue(techniqueId, out var tech))
                return false;

            // Проверка кулдауна
            if (_cooldowns.TryGetValue(techniqueId, out var remaining) && remaining > 0)
                return false;

            // EVT-01: проверка Ци из кэша (best-effort: кэш актуален в рамках кадра)
            if (_cachedCurrentQi < tech.QiCost)
                return false;

            // EVT-01: запрашиваем расход Ци через событие
            _qiConsumeRequestPub.Publish(new QiConsumeRequestEvent(tech.QiCost, "TechniqueService"));

            // Установка кулдауна
            if (tech.Cooldown > 0)
                _cooldowns[techniqueId] = tech.Cooldown;

            // Рост мастерства (TECHNIQUE_SYSTEM.md §5.1 шаг 5)
            tech.Mastery = MathF.Min(100f, tech.Mastery + 0.01f);

            // Публикация события
            // Фаза 9D: QiCost float→int (ЗАПРЕТ 3.9)
            _techniqueUsedPub.Publish(new TechniqueUsedEvent(
                _cachedEntityId, techniqueId, (int)tech.QiCost));

            return true;
        }

        /// <summary>
        /// Stage 0 (2026-08-25, GLM-5.3): завершение использования техники ПОСЛЕ зарядки.
        /// Вызывается PlayerTechniqueCaster.OnChargeCompleted после того, как
        /// TechniqueChargeService дренировал Ци тиками (расход уже учтён).
        /// Делает ТОЛЬКО: установку кулдауна, рост мастерства, публикацию TechniqueUsedEvent.
        /// НЕ списывает Ци (уже сделано сервисом зарядки).
        /// </summary>
        public bool CompleteUse(string techniqueId)
        {
            if (!_learnedTechniques.TryGetValue(techniqueId, out var tech))
                return false;

            // Кулдаун (на случай повторного вызова — но PlayerTechniqueCaster гарантирует путь)
            if (_cooldowns.TryGetValue(techniqueId, out var remaining) && remaining > 0)
                return false;

            // Установка кулдауна
            if (tech.Cooldown > 0)
                _cooldowns[techniqueId] = tech.Cooldown;

            // Рост мастерства (TECHNIQUE_SYSTEM.md §5.1 шаг 5)
            tech.Mastery = MathF.Min(100f, tech.Mastery + 0.01f);

            // Публикация события
            _techniqueUsedPub.Publish(new TechniqueUsedEvent(
                _cachedEntityId, techniqueId, (int)tech.QiCost));

            return true;
        }

        /// <summary>
        /// Обновить кулдауны (вызывается из CombatModule.Tick).
        /// </summary>
        public void UpdateCooldowns(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            List<string> expired = null;
            foreach (var kvp in _cooldowns)
            {
                float remaining = kvp.Value - deltaTime;
                if (remaining <= 0f)
                {
                    expired ??= new List<string>();
                    expired.Add(kvp.Key);
                }
                else
                {
                    _cooldowns[kvp.Key] = remaining;
                }
            }

            if (expired != null)
            {
                foreach (var key in expired)
                    _cooldowns.Remove(key);
            }
        }

        /// <summary>
        /// Получить оставшееся время кулдауна.
        /// </summary>
        public float GetCooldown(string techniqueId)
        {
            return _cooldowns.TryGetValue(techniqueId, out var remaining) ? remaining : 0f;
        }

        /// <summary>
        /// Проверить, изучена ли техника.
        /// </summary>
        public bool IsLearned(string techniqueId)
        {
            return _learnedTechniques.ContainsKey(techniqueId);
        }

        /// <summary>
        /// Получить изученную технику.
        /// </summary>
        public LearnedTechnique GetTechnique(string techniqueId)
        {
            return _learnedTechniques.TryGetValue(techniqueId, out var tech) ? tech : null;
        }

        /// <summary>
        /// Получить все изученные техники.
        /// </summary>
        public IReadOnlyDictionary<string, LearnedTechnique> GetAllTechniques()
        {
            return _learnedTechniques;
        }

        /// <summary>Идентификаторы изученных техник в порядке изучения (для UI).</summary>
        public IReadOnlyList<string> GetOrderedIds()
        {
            return _orderedIds;
        }

        // === ISaveable (2026-08-28) ===
        // Раньше в сейв уходили только слоты (TechniqueSlotService), а сами
        // техники — нет: загрузка оставляла бы слоты, указывающие в пустоту.
        // Теперь библиотека персистентна: изученные техники + выбранная +
        // эхо мастерства + свитки + расширение ёмкости.

        public string SaveKey => "techniques";

        public object CaptureState()
        {
            var state = new TechniqueServiceState
            {
                SelectedId = _selectedTechniqueId,
                Echo = new Dictionary<string, float>(_masteryEcho),
                ExtraLibraryCapacity = ExtraLibraryCapacity,
            };
            foreach (var id in _orderedIds)
            {
                if (!_learnedTechniques.TryGetValue(id, out var t)) continue;
                state.Learned.Add(TechniqueSnapshotDto.FromLearned(t));
            }
            foreach (var kvp in _scrolls)
                state.Scrolls.Add(TechniqueSnapshotDto.FromData(kvp.Key, kvp.Value));
            return state;
        }

        public void RestoreState(object state)
        {
            if (state is not TechniqueServiceState s) return;

            _learnedTechniques.Clear();
            _orderedIds.Clear();
            _cooldowns.Clear();
            _masteryEcho.Clear();
            _scrolls.Clear();

            if (s.Learned != null)
            {
                foreach (var dto in s.Learned)
                {
                    if (string.IsNullOrEmpty(dto?.TechniqueId)) continue;
                    if (_learnedTechniques.ContainsKey(dto.TechniqueId)) continue;
                    _learnedTechniques[dto.TechniqueId] = dto.ToLearned();
                    _orderedIds.Add(dto.TechniqueId);
                }
            }
            if (s.Scrolls != null)
            {
                foreach (var dto in s.Scrolls)
                {
                    if (string.IsNullOrEmpty(dto?.ScrollId) || string.IsNullOrEmpty(dto.TechniqueId)) continue;
                    _scrolls[dto.ScrollId] = dto.ToData();
                }
            }
            if (s.Echo != null)
            {
                foreach (var kvp in s.Echo)
                    _masteryEcho[kvp.Key] = kvp.Value;
            }
            ExtraLibraryCapacity = s.ExtraLibraryCapacity;

            _selectedTechniqueId =
                s.SelectedId != null && _learnedTechniques.ContainsKey(s.SelectedId)
                    ? s.SelectedId
                    : null;
        }

        public void Dispose()
        {
            _qiChangedSubscription?.Dispose();
            _qiChangedSubscription = null;
        }
    }

    // === Сериализационные DTO (2026-08-28) ===
    // Свойства (не поля): System.Text.Json без IncludeFields не сериализует
    // public-поля — см. находку в checkpoints/08_28_technique_book_and_ui_cleanup.md.

    /// <summary>Снимок сейва TechniqueService.</summary>
    public sealed class TechniqueServiceState
    {
        public List<TechniqueSnapshotDto> Learned { get; set; } = new();
        public List<TechniqueSnapshotDto> Scrolls { get; set; } = new();
        public Dictionary<string, float> Echo { get; set; } = new();
        public string? SelectedId { get; set; }
        public int ExtraLibraryCapacity { get; set; }
    }

    /// <summary>
    /// Снимок техники: для изученных (Mastery — наработанное) и для свитков
    /// (Mastery всегда 0 — базовая форма).
    /// </summary>
    public sealed class TechniqueSnapshotDto
    {
        public string? ScrollId { get; set; }
        public string TechniqueId { get; set; } = "";
        public string NameRu { get; set; } = "";
        public string Description { get; set; } = "";
        public int Type { get; set; }
        public int Subtype { get; set; }
        public int Grade { get; set; }
        public int Element { get; set; }
        public int Level { get; set; }
        public long QiCost { get; set; }
        public int CapacityCost { get; set; }
        public int BaseDamage { get; set; }
        public float Cooldown { get; set; }
        public float Range { get; set; }
        public float CastTime { get; set; }
        public int ArmorPenetration { get; set; }
        public bool IsUltimate { get; set; }
        public float Mastery { get; set; }

        public static TechniqueSnapshotDto FromLearned(LearnedTechnique t) => new()
        {
            TechniqueId = t.TechniqueId,
            NameRu = t.Name,
            Type = (int)t.Type,
            Subtype = (int)t.Subtype,
            Grade = (int)t.Grade,
            Element = (int)t.Element,
            Level = t.Level,
            QiCost = t.QiCost,
            CapacityCost = t.CapacityCost,
            BaseDamage = t.BaseDamage,
            Cooldown = t.Cooldown,
            Range = t.Range,
            CastTime = t.CastTime,
            ArmorPenetration = t.ArmorPenetration,
            IsUltimate = t.IsUltimate,
            Mastery = t.Mastery,
        };

        public static TechniqueSnapshotDto FromData(string scrollId, TechniqueData d) => new()
        {
            ScrollId = scrollId,
            TechniqueId = d.TechniqueId,
            NameRu = d.NameRu,
            Description = d.Description,
            Type = (int)d.Type,
            Subtype = (int)d.Subtype,
            Grade = (int)d.Grade,
            Element = (int)d.Element,
            Level = d.Level,
            QiCost = d.QiCost,
            CapacityCost = d.CapacityCost,
            BaseDamage = d.BaseDamage,
            Cooldown = d.Cooldown,
            Range = d.Range,
            CastTime = d.CastTime,
            ArmorPenetration = d.ArmorPenetration,
            IsUltimate = d.IsUltimate,
            Mastery = d.Mastery,
        };

        public LearnedTechnique ToLearned() => new()
        {
            TechniqueId = TechniqueId,
            Name = NameRu,
            Type = (TechniqueType)Type,
            Subtype = (CombatSubtype)Subtype,
            Grade = (TechniqueGrade)Grade,
            Element = (Element)Element,
            Level = Level,
            QiCost = QiCost,
            CapacityCost = CapacityCost,
            BaseDamage = BaseDamage,
            Cooldown = Cooldown,
            Range = Range,
            CastTime = CastTime,
            ArmorPenetration = ArmorPenetration,
            IsUltimate = IsUltimate,
            Mastery = Mastery,
        };

        public TechniqueData ToData() => new()
        {
            TechniqueId = TechniqueId,
            NameRu = NameRu,
            NameEn = NameRu,
            Description = Description,
            Type = (TechniqueType)Type,
            Subtype = (CombatSubtype)Subtype,
            Grade = (TechniqueGrade)Grade,
            Element = (Element)Element,
            Level = Level,
            QiCost = QiCost,
            CapacityCost = CapacityCost,
            BaseDamage = BaseDamage,
            Cooldown = Cooldown,
            Range = Range,
            CastTime = CastTime,
            ArmorPenetration = ArmorPenetration,
            IsUltimate = IsUltimate,
            Mastery = Mastery,
        };
    }

    /// <summary>
    /// Данные изученной техники.
    /// CMB-A06: QiCost — long (Fix-01: все Qi-значения long).
    /// FIX CS1061: добавлены CastTime, ArmorPenetration, BaseDamage, Element, IsUltimate —
    /// поля из TechniqueData, необходимые CombatService.
    /// Этап 1: +Name, +Level, +Range, +Mastery (живой рост при использовании).
    /// </summary>
    public class LearnedTechnique
    {
        public string TechniqueId;
        public string Name = "";
        public TechniqueType Type;
        public TechniqueGrade Grade;
        public CombatSubtype Subtype;
        public int Level;                 // Уровень техники (1..9)
        public long QiCost;               // CMB-A06: long вместо float (Fix-01)
        public float Cooldown;
        public int CapacityCost;
        public float CastTime;            // FIX CS1061: время каста (из TechniqueData)
        public float Range;               // Дальность (метры, из TechniqueData)
        public int ArmorPenetration;      // FIX CS1061: пробитие брони (из TechniqueData, C6)
        public int BaseDamage;            // FIX CS1061: базовый урон (из TechniqueData, P1-6.1: int)
        public Element Element;           // FIX CS1061: стихия (из TechniqueData, B5)
        public bool IsUltimate;           // FIX CS1061: Ultimate-техника (из TechniqueData)
        public float Mastery;             // Этап 1: мастерство 0..100 (растёт при использовании)
    }
}
