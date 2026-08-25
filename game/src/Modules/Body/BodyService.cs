#nullable enable
// Создано: 2026-05-08 15:50:00 UTC
// Редактировано: 2026-05-09 14:00:00 UTC — аудит: R-01 regen rate, R-03 actual heal, R-04/R-06 single SplitDamage
//   BD-05 регенерация только RedHP, BD-09 материальное снижение, BD-12 EntityId null guard,
//   BD-21 L10 регенерация, BD-23 morphology missing parts, BD-24 Heart HealPart,
//   BD-26 GC аллокация, BD-30 детерминированный fallback, BD-32 HealPart actual amount,
//   BD-47 _regenRate из константы, DISC-01 одновременный split
// Редактировано: 2026-05-09 — EVT: убрана инъекция IQiService, CultivationLevel → кэш из QiChangedEvent
// Редактировано: 2026-05-18 — P1-14 FIX: QiChangedEvent → CultivationLevelChangedEvent
// Редактировано: 2026-05-10 12:00:00 UTC — Phase 18A: реализация ISaveable
// Редактировано: 2026-05-18 — миграция на BodyFactory, +RecalculateHPFromVitality, +SizeClass
// Редактировано: 2026-05-18 12:00:00 UTC — CreateBodyWithCustomHP→CreateBody (P2-01 FIX)
// Редактировано: 2026-05-18 13:10:29 UTC — P0-01/P0-05 FIX: +ReattachPart, +IPublisher<BodyPartReattachedEvent>, P1-02 FIX: size в BodySaveData, P1-05 FIX: GetMorphology/GetSizeClass, P2-02 FIX: единый _dataDirty
// Редактировано: 2026-05-18 — V3 FIXES: P0-01 split _dataDirty→_listDirty+_cacheDirty, P1-01 HealPart guard, P1-07 BodyPartDamagedEvent always+StateChanged, P2-01 static VitalPriority, P2-05 IsVital в BodyPartSeveredEvent, P2-04 MaxRedHP/CurrentRedHP в события
// Редактировано: 2026-05-20 18:43:21 UTC — Фаза 3: реализация IBodyDataProvider (per-entity BodyParts)
// Редактировано: 2026-05-21 08:15:33 UTC — Волна 2.2: NPC урон через OnDamageApplied + _entityBodyParts
// Редактировано: 2026-05-21 18:35:52 UTC — Спринт 1 A1: убрано двойное материальное снижение (DefenseProcessor уже снижает)
// Реализация IBodyService — система тела с двойной HP и Body→Equipment маппингом.
// Источник: BODY_SYSTEM.md, ALGORITHMS.md, plan_03_body.md
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Body
{
    /// <summary>
    /// Сервис тела — реализация IBodyService.
    /// Управляет частями тела, двойной HP, ампутацией и блокировкой слотов экипировки.
    /// 
    /// Ключевая связь: Body→Equipment через BodySlotMapping.
    /// При ампутации части тела публикуется BodyPartSeveredEvent с BlockedSlots.
    /// 
    /// Кросс-модульные вызовы (Qi) — через MessagePipe события.
    /// CultivationLevel кэшируется из QiChangedEvent.
    /// </summary>
    public class BodyService : IBodyService, IBodyDataProvider, ISaveable, IDisposable
    {
        // P2-01 (V3) FIX: статический массив для ResolveTarget — без GC-аллокации при каждом вызове
        private static readonly BodyPartType[] VitalPriority = { BodyPartType.Head, BodyPartType.Heart };

        // === DI-зависимости ===
        private readonly IPublisher<BodyPartDamagedEvent> _damagedPublisher;
        private readonly IPublisher<BodyPartSeveredEvent> _severedPublisher;
        private readonly IPublisher<BodyPartHealedEvent> _healedPublisher;
        private readonly IPublisher<BodyPartReattachedEvent> _reattachedPublisher;
        private readonly IPublisher<BodyCriticalEvent> _criticalPublisher;  // P2-07 FIX
        private readonly ISubscriber<DamageAppliedEvent> _damageSubscriber;

        // === Кэш кросс-модульного состояния (из CultivationLevelChangedEvent — P1-14 FIX) ===
        private int _cachedCultivationLevel = 1; // P1-14 FIX: уровень культивации из CultivationLevelChangedEvent
        private IDisposable _cultivationLevelSubscription;

        // === Внутреннее состояние ===
        private readonly Dictionary<BodyPartType, BodyPart> _parts = new();
        private readonly HashSet<BodyPartType> _severedParts = new();
        private readonly List<BodyPartData> _partsCache = new();

        // === IBodyDataProvider: хранилище per-entity BodyParts (для NPC) ===
        // Ключевой словарь: entityId → List<BodyPart> (решение ПРОТИВОРЕЧИЯ #3/#6)
        private readonly Dictionary<string, List<BodyPart>> _entityBodyParts = new();

        // BD-26: Кэшированный список BodyPart для ProcessRegeneration (без GC-аллокации)
        private readonly List<BodyPart> _bodyPartsList = new();
        // P0-01 (V3) FIX: разделённый dirty-флаг — _listDirty для _bodyPartsList, _cacheDirty для _partsCache
        // Проблема: единый _dataDirty снимался в ProcessRegeneration до регенерации,
        // но если healAmount<=0, кэш не обновлялся, а флаг уже снят.
        private bool _listDirty = true;   // для _bodyPartsList (ProcessRegeneration)
        private bool _cacheDirty = true;  // для _partsCache (GetAllParts)

        private string _entityId;
        private BodyMaterial _material;
        private Morphology _morphology;
        private SizeClass _size = SizeClass.Medium;
        private bool _isInitialized;

        // === Регенерация ===
        // BD-47: Скорость регенерации из константы
        private readonly float _regenRate = GameConstants.BASE_BODY_REGEN_RATE;
        private float _regenAccumulator; // аккумулятор дробной регенерации

        // === IDisposable для подписок ===
        private IDisposable _damageSubscription;

        // === Конструктор ===

        // === Фабрика (инжектируется через интерфейс — P1-10 FIX) ===
        private readonly IBodyFactory _bodyFactory;

        public BodyService(
            IPublisher<BodyPartDamagedEvent> damagedPublisher,
            IPublisher<BodyPartSeveredEvent> severedPublisher,
            IPublisher<BodyPartHealedEvent> healedPublisher,
            IPublisher<BodyPartReattachedEvent> reattachedPublisher,
            IPublisher<BodyCriticalEvent> criticalPublisher,  // P2-07 FIX
            ISubscriber<DamageAppliedEvent> damageSubscriber,
            ISubscriber<CultivationLevelChangedEvent> cultivationLevelSub,  // P1-14 FIX
            IBodyFactory bodyFactory)
        {
            _damagedPublisher = damagedPublisher ?? throw new ArgumentNullException(nameof(damagedPublisher));
            _severedPublisher = severedPublisher ?? throw new ArgumentNullException(nameof(severedPublisher));
            _healedPublisher = healedPublisher ?? throw new ArgumentNullException(nameof(healedPublisher));
            _reattachedPublisher = reattachedPublisher ?? throw new ArgumentNullException(nameof(reattachedPublisher));
            _criticalPublisher = criticalPublisher ?? throw new ArgumentNullException(nameof(criticalPublisher));  // P2-07 FIX
            _damageSubscriber = damageSubscriber ?? throw new ArgumentNullException(nameof(damageSubscriber));
            _bodyFactory = bodyFactory ?? throw new ArgumentNullException(nameof(bodyFactory));

            // P1-14 FIX: подписка на CultivationLevelChangedEvent вместо QiChangedEvent
            // Получаем событие ТОЛЬКО при изменении уровня (не при каждом изменении Ци)
            _cultivationLevelSubscription = cultivationLevelSub.Subscribe(OnCultivationLevelChanged);
        }

        // === Инициализация (вызывается из BodyModule) ===

        /// <summary>
        /// Инициализировать тело сущности.
        /// Создаёт части тела через BodyFactory (data-driven).
        /// </summary>
        public void Initialize(string entityId, Morphology morphology, BodyMaterial material, float vitality)
        {
            if (_isInitialized) return;

            _entityId = entityId ?? string.Empty;
            _morphology = morphology;
            _material = material;

            // Создаём части тела через фабрику (data-driven)
            var createdParts = _bodyFactory.CreateBody(morphology, _size, vitality);
            foreach (var part in createdParts)
            {
                _parts[part.Type] = part;
            }

            // BD-23: Части, отсутствующие в морфологии, считаются «ампутированными»
            // для корректной работы IsSlotBlocked (змея не может экипировать оружие)
            foreach (BodyPartType type in Enum.GetValues(typeof(BodyPartType)))
            {
                if (!_parts.ContainsKey(type))
                    _severedParts.Add(type);
            }

            _listDirty = true;
            _cacheDirty = true;

            // Подписка на входящий урон
            _damageSubscription = _damageSubscriber.Subscribe(OnDamageApplied);

            _isInitialized = true;
        }

        /// <summary>
        /// Инициализировать тело сущности с указанием SizeClass.
        /// </summary>
        public void Initialize(string entityId, Morphology morphology, BodyMaterial material,
            SizeClass size, float vitality)
        {
            _size = size;
            Initialize(entityId, morphology, material, vitality);
        }

        // === IBodyService ===

        // BD-12: EntityId возвращает string.Empty вместо null до Initialize
        public string EntityId => _entityId ?? string.Empty;

        public BodyPartState GetPartState(BodyPartType type)
        {
            if (_parts.TryGetValue(type, out var part))
                return part.State;
            return BodyPartState.Severed; // Несуществующая часть = отрубленная
        }

        public bool IsPartSevered(BodyPartType type)
        {
            if (_parts.TryGetValue(type, out var part))
                return part.State == BodyPartState.Severed;
            return true; // Несуществующая часть считается отрубленной
        }

        public bool IsPartDisabled(BodyPartType type)
        {
            if (_parts.TryGetValue(type, out var part))
                return part.State == BodyPartState.Disabled;
            return false;
        }

        public float GetPartHealthRatio(BodyPartType type)
        {
            if (!_parts.TryGetValue(type, out var part))
                return 0f;
            return part.MaxRedHP > 0 ? (float)part.CurrentRedHP / part.MaxRedHP : 0f;
        }

        /// <summary>
        /// Применить урон к части тела.
        /// Порядок:
        /// 1. Проверить существование части (fallback → Torso → vital)
        /// 2. Если часть Severed — fallback на Torso
        /// 3. Применить материальное снижение
        /// 4. Split 70/30 и нанести урон через BodyPart.TakeDamage
        /// 5. Публикация событий при изменении состояния
        /// 
        /// R-06: Принимает totalDamage — split происходит внутри один раз.
        /// Для прямого урона в конкретный HP тип используйте ApplyDirectDamage.
        /// </summary>
        public void ApplyDamage(BodyPartType target, int totalDamage)
        {
            if (!_isInitialized) return;
            if (totalDamage <= 0) return;

            // Определяем реальную цель
            BodyPart part = ResolveTarget(target);
            if (part == null) return; // Нет живых частей — урон некуда применять

            // A1 FIX: Убрано двойное материальное снижение.
            // DefenseProcessor.ApplyDefense() уже применяет materialReduction
            // в слое 6-8 пайплайна урона. Повторное снижение здесь давало
            // Scaled: ×0.7×0.7=×0.49 вместо ×0.7; Chitin: двойное снижение.
            // Теперь передаём totalDamage напрямую в SplitDamage.

            // Split 70/30 — единственная точка разделения (R-04/R-06)
            var (finalRedDmg, finalBlackDmg) = BodyDamageCalculator.SplitDamage(totalDamage);

            // Запоминаем предыдущее состояние
            BodyPartState previousState = part.State;

            // Наносим урон (DISC-01: одновременный split, без overflow)
            bool applied = part.TakeDamage(finalRedDmg, finalBlackDmg);
            if (!applied) return; // Часть отрублена (не должно быть после ResolveTarget)

            _listDirty = true;
            _cacheDirty = true;

            // P1-07 (V3) FIX: Публикуем событие повреждения при ЛЮБОМ применённом уроне,
            // не только при смене состояния. UI нуждается в обновлении HP-бара.
            BodyPartState newState = part.State;
            bool stateChanged = newState != previousState;
            _damagedPublisher.Publish(new BodyPartDamagedEvent(
                _entityId, part.Type, totalDamage, newState, stateChanged,
                part.CurrentRedHP, part.MaxRedHP));  // P2-04 (V3) FIX; A1 FIX: reducedDamage→totalDamage

            // При ампутации — публикация события блокировки слотов
            if (newState == BodyPartState.Severed && previousState != BodyPartState.Severed)
            {
                _severedParts.Add(part.Type);
                var blockedSlots = BodySlotMapping.GetBlockedSlots(part.Type);

                _severedPublisher.Publish(new BodyPartSeveredEvent(
                    _entityId, part.Type, blockedSlots, part.IsVital));  // P2-05 (V3) FIX
            }

            // P2-07 FIX: Публикация BodyCriticalEvent при Disabled жизненно важной части
            // P2-05 (V3) FIX: +при Severed vital-части (ампутация через урон)
            if (part.IsVital)
            {
                if ((newState == BodyPartState.Disabled && previousState != BodyPartState.Disabled) ||
                    (newState == BodyPartState.Severed && previousState != BodyPartState.Severed))
                {
                    _criticalPublisher.Publish(new BodyCriticalEvent(
                        _entityId, part.Type, newState,
                        part.MaxRedHP > 0 ? (float)part.CurrentRedHP / part.MaxRedHP : 0f));
                }
            }
        }

        /// <summary>
        /// Исцелить часть тела.
        /// BD-24: Для Heart (MaxBlackHP=0) всё лечение идёт в RedHP.
        /// BD-32: Публикует реально исцелённое количество HP.
        /// </summary>
        public void HealPart(BodyPartType target, int amount)
        {
            if (!_isInitialized || amount <= 0) return;

            if (!_parts.TryGetValue(target, out var part))
                return;

            BodyPartState previousState = part.State;

            // BD-24: Для Heart (MaxBlackHP=0) — 100% лечения в RedHP
            int redHeal, blackHeal;
            if (part.MaxBlackHP == 0)
            {
                redHeal = amount;
                blackHeal = 0;
            }
            else
            {
                redHeal = (int)(amount * GameConstants.RED_HP_RATIO);
                blackHeal = amount - redHeal;
            }

            // R-03: Heal теперь возвращает реально исцелённую HP (Red + Black)
            int actualHealed = part.Heal(redHeal, blackHeal);
            // P1-01 (V3) FIX: guard для actualHealed <= 0 (не только Severed)
            // Ранее: событие публиковалось даже при Amount=0 (часть на MaxHP)
            if (actualHealed <= 0) return;

            _listDirty = true;
            _cacheDirty = true;

            // R-03: Публикуем реально исцелённое количество HP
            // P2-04 (V3) FIX: +MaxRedHP, +CurrentRedHP для UI
            _healedPublisher.Publish(new BodyPartHealedEvent(
                _entityId, part.Type, actualHealed, part.CurrentRedHP, part.MaxRedHP));
        }

        /// <summary>
        /// Проверить: блокирован ли слот экипировки из-за ампутации.
        /// Использует кэш _severedParts для O(1) доступа.
        /// BD-23: Учитывает отсутствующие в морфологии части.
        /// </summary>
        public bool IsSlotBlocked(EquipmentSlot slot)
        {
            return BodySlotMapping.IsSlotBlocked(slot, _severedParts);
        }

        /// <summary>
        /// Получить данные всех частей тела.
        /// Использует кэширование — пересоздаётся только при изменении.
        /// </summary>
        public IReadOnlyList<BodyPartData> GetAllParts()
        {
            if (_cacheDirty)  // P0-01 (V3) FIX: _cacheDirty вместо _dataDirty
            {
                _partsCache.Clear();
                foreach (var kvp in _parts)
                {
                    _partsCache.Add(kvp.Value.ToData());
                }
                _cacheDirty = false;
            }
            return _partsCache;
        }

        // === Регенерация (вызывается из BodyModule.Tick) ===

        /// <summary>
        /// Обработать регенерацию за один кадр.
        /// ТОЛЬКО RedHP — не BlackHP (план 05_08_impl_03_body.md, шаг 5).
        /// BlackHP восстанавливается только специальными средствами.
        /// Скорость = BASE_BODY_REGEN_RATE × RegenerationMultipliers[CultivationLevel].
        /// </summary>
        public void ProcessRegeneration(float deltaTime)
        {
            if (!_isInitialized || deltaTime <= 0f) return;

            // BD-26: Используем кэшированный список без GC-аллокации
            // P0-01 (V3) FIX: _listDirty вместо _dataDirty
            if (_listDirty)
            {
                _bodyPartsList.Clear();
                foreach (var kvp in _parts)
                    _bodyPartsList.Add(kvp.Value);
                _listDirty = false;
            }

            if (!BodyDamageCalculator.IsAlive(_bodyPartsList)) return;

            // Множитель регенерации от уровня культивации
            float regenMultiplier = GetRegenMultiplier();
            float regenAmount = _regenRate * regenMultiplier * deltaTime;

            // BD-21: L10 (Ascension) — мгновенное восстановление
            if (float.IsInfinity(regenAmount) || regenAmount > int.MaxValue)
            {
                // Мгновенная полная регенерация
                foreach (var part in _bodyPartsList)
                {
                    if (part.State == BodyPartState.Severed) continue;
                    part.Heal(part.MaxRedHP - part.CurrentRedHP, 0);
                }
                _listDirty = true;
                _cacheDirty = true;
                _regenAccumulator = 0f;
                return;
            }

            // Аккумулятор для дробной регенерации
            _regenAccumulator += regenAmount;

            // BD-21: Защита от overflow при касте float→int
            _regenAccumulator = Math.Min(_regenAccumulator, int.MaxValue);
            int healAmount = (int)_regenAccumulator;
            if (healAmount <= 0) return;

            // Вычитаем только целую часть
            _regenAccumulator -= healAmount;

            // Регенерация только RedHP по всем не-ампутированным частям
            foreach (var part in _bodyPartsList)
            {
                if (part.State == BodyPartState.Severed) continue;
                if (part.CurrentRedHP < part.MaxRedHP)
                {
                    part.Heal(healAmount, 0);
                    _listDirty = true;
                    _cacheDirty = true;
                }
            }
        }

        // === Внутренние методы ===

        /// <summary>
        /// Определить реальную цель урона.
        /// Fallback: несуществующая/ампутированная → Torso → vital parts.
        /// BD-30: Детерминированный порядок выбора vital-части (Head → Heart).
        /// </summary>
        private BodyPart ResolveTarget(BodyPartType target)
        {
            // Проверяем: часть существует и не отрублена?
            if (_parts.TryGetValue(target, out var part) && part.State != BodyPartState.Severed)
                return part;

            // Fallback на торс
            if (_parts.TryGetValue(BodyPartType.Torso, out var torso) && torso.State != BodyPartState.Severed)
                return torso;

            // BD-30: Детерминированный fallback на жизненно важные части (Head → Heart)
            // P2-01 (V3) FIX: static readonly вместо аллокации нового массива при каждом вызове
            foreach (var vitalType in VitalPriority)
            {
                if (_parts.TryGetValue(vitalType, out var vPart) && vPart.State != BodyPartState.Severed)
                    return vPart;
            }

            // Нет живых частей
            return null;
        }

        /// <summary>
        /// Обработчик входящего урона (подписка на DamageAppliedEvent).
        /// R-04: Передаёт полный урон в ApplyDamage, split происходит внутри.
        /// Волна 2.2: также обрабатывает NPC урон через _entityBodyParts.
        /// </summary>
        private void OnCultivationLevelChanged(in CultivationLevelChangedEvent e)
        {
            if (e.EntityId != _entityId) return;  // Фильтруем по EntityId
            _cachedCultivationLevel = e.NewLevel;
        }

        private void OnDamageApplied(in DamageAppliedEvent e)
        {
            // 1. Игрок: урон по нашим собственным BodyParts.
            // P0 FIX (2026-08-25, NPC_COMBAT_PREP Phase 8 wiring): NPC AI атакует
            // игрока как "player_0" (NPCAIService.PlayerId), а тело игрока
            // инициализировано под "player" (BodyConfig default) — из-за строгого
            // сравнения урон NPC никогда не применялся к игроку (тост показывался,
            // HP не падал, смерть была недостижима). Принимаем оба исторических ID
            // (прецедент двойной проверки: PlayerService, GameWorldController,
            // NPCAIService.Friendly-ветка).
            if (e.TargetId == _entityId || (IsPlayerEntityId(e.TargetId) && IsPlayerEntityId(_entityId)))
            {
                ApplyDamage(e.HitPart, e.Damage);
                return;
            }

            // 2. NPC: урон через per-entity BodyParts (Волна 2.2 фикс)
            // Без этого блока NPC бессмертен — BodyService фильтрует по _entityId
            if (_entityBodyParts.TryGetValue(e.TargetId, out var npcParts))
            {
                ApplyDamageToEntityParts(e.TargetId, npcParts, e.HitPart, e.Damage);
            }
        }

        /// <summary>
        /// Исторические ID игрока в кодовой базе: "player" (InventoryModule,
        /// BodyConfig) и "player_0" (PlayerService, Combat, NPC AI).
        /// P0 FIX: единая точка нормализации для сравнения.
        /// </summary>
        private static bool IsPlayerEntityId(string? id) => id == "player" || id == "player_0";

        /// <summary>
        /// Применить урон к частям тела NPC (per-entity).
        /// Аналогично ApplyDamage, но работает с List<BodyPart> из _entityBodyParts.
        /// Волна 2.2: устраняет бессмертие NPC.
        /// </summary>
        private void ApplyDamageToEntityParts(string entityId, List<BodyPart> parts, BodyPartType target, int totalDamage)
        {
            if (totalDamage <= 0) return;

            // Определяем реальную цель урона из NPC BodyParts
            BodyPart part = ResolveEntityTarget(parts, target);
            if (part == null) return;

            // Split 70/30
            var (finalRedDmg, finalBlackDmg) = BodyDamageCalculator.SplitDamage(totalDamage);

            BodyPartState previousState = part.State;
            bool applied = part.TakeDamage(finalRedDmg, finalBlackDmg);
            if (!applied) return;

            BodyPartState newState = part.State;
            bool stateChanged = newState != previousState;

            _damagedPublisher.Publish(new BodyPartDamagedEvent(
                entityId, part.Type, totalDamage, newState, stateChanged,
                part.CurrentRedHP, part.MaxRedHP));

            if (newState == BodyPartState.Severed && previousState != BodyPartState.Severed)
            {
                var blockedSlots = BodySlotMapping.GetBlockedSlots(part.Type);
                _severedPublisher.Publish(new BodyPartSeveredEvent(
                    entityId, part.Type, blockedSlots, part.IsVital));
            }

            if (part.IsVital)
            {
                if ((newState == BodyPartState.Disabled && previousState != BodyPartState.Disabled) ||
                    (newState == BodyPartState.Severed && previousState != BodyPartState.Severed))
                {
                    _criticalPublisher.Publish(new BodyCriticalEvent(
                        entityId, part.Type, newState,
                        part.MaxRedHP > 0 ? (float)part.CurrentRedHP / part.MaxRedHP : 0f));
                }
            }
        }

        /// <summary>
        /// Определить реальную цель урона для NPC BodyParts.
        /// Порядок: прямое попадание → Torso → жизненно важные (Head, Heart).
        /// </summary>
        private BodyPart ResolveEntityTarget(List<BodyPart> parts, BodyPartType target)
        {
            // Прямое попадание
            foreach (var part in parts)
                if (part.Type == target && part.State != BodyPartState.Severed)
                    return part;

            // Fallback на Torso
            foreach (var part in parts)
                if (part.Type == BodyPartType.Torso && part.State != BodyPartState.Severed)
                    return part;

            // Fallback на жизненно важные
            foreach (var vitalType in VitalPriority)
                foreach (var part in parts)
                    if (part.Type == vitalType && part.State != BodyPartState.Severed)
                        return part;

            return null;
        }

        /// <summary>
        /// Множитель регенерации по уровню культивации.
        /// L1: 1.1x, L2: 2.0x, ..., L10: ∞
        /// EVT: уровень культивации кэшируется из QiChangedEvent.
        /// </summary>
        private float GetRegenMultiplier()
        {
            // EVT: используем кэш вместо инъекции IQiService
            int level = _cachedCultivationLevel;
            if (level >= 1 && level <= GameConstants.RegenerationMultipliers.Length)
                return GameConstants.RegenerationMultipliers[level - 1];
            return 1f;
        }

        // === ISaveable ===

        /// <summary>
        /// Ключ сохранения для модуля тела.
        /// </summary>
        public string SaveKey => "body";

        /// <summary>
        /// Снять состояние тела для сериализации.
        /// Сохраняет: морфологию, материал, упрощённое состояние частей тела
        /// (тип, состояние, текущая красная/чёрная HP).
        /// </summary>
        public object CaptureState()
        {
            // Собираем упрощённые данные по всем частям тела
            var partsArray = new BodyPartSaveEntry[_parts.Count];
            int i = 0;
            foreach (var kvp in _parts)
            {
                var part = kvp.Value;
                partsArray[i] = new BodyPartSaveEntry
                {
                    partType = (int)part.Type,
                    state = (int)part.State,
                    currentRedHP = part.CurrentRedHP,
                    currentBlackHP = part.CurrentBlackHP
                };
                i++;
            }

            var data = new BodySaveData
            {
                morphology = (int)_morphology,
                material = (int)_material,
                size = (int)_size,  // P1-02 FIX: сохранение SizeClass
                parts = partsArray
            };
            return data;
        }

        /// <summary>
        /// Восстановить состояние тела из JSON.
        /// Восстанавливает HP каждой части тела по сохранённым данным.
        /// Морфология и материал не меняются (тело уже инициализировано),
        /// но проверяем соответствие для безопасности.
        /// </summary>
        public void RestoreState(object state)
        {
            if (state is not BodySaveData data || data == null) return;

            // Проверяем соответствие морфологии и материала
            // (тело уже инициализировано через Initialize, проверяем консистентность)
            if ((Morphology)data.morphology != _morphology ||
                (BodyMaterial)data.material != _material)
            {
                // Несовпадение морфологии/материала — восстановление небезопасно
                // Логируем предупреждение и пропускаем
                Console.WriteLine(
                    $"[BodyService.RestoreState] Несовпадение морфологии/материала: " +
                    $"saved=({(Morphology)data.morphology}, {(BodyMaterial)data.material}), " +
                    $"current=({_morphology}, {_material})");
                return;
            }

            // P1-02 FIX: Восстанавливаем SizeClass
            _size = (SizeClass)data.size;

            // Восстанавливаем HP каждой части тела
            if (data.parts != null)
            {
                foreach (var entry in data.parts)
                {
                    var partType = (BodyPartType)entry.partType;
                    if (_parts.TryGetValue(partType, out var part))
                    {
                        // Используем SetHP для корректного восстановления с обновлением состояния
                        part.SetHP(entry.currentRedHP, entry.currentBlackHP);
                    }
                }
            }

            // Помечаем кэш грязным после восстановления
            _listDirty = true;
            _cacheDirty = true;

            // Обновляем набор ампутированных частей
            _severedParts.Clear();
            foreach (var kvp in _parts)
            {
                if (kvp.Value.State == BodyPartState.Severed)
                    _severedParts.Add(kvp.Key);
            }
            // Части, отсутствующие в морфологии, тоже считаются «ампутированными» (BD-23)
            foreach (BodyPartType type in Enum.GetValues(typeof(BodyPartType)))
            {
                if (!_parts.ContainsKey(type))
                    _severedParts.Add(type);
            }
        }

        /// <summary>
        /// Очистка подписок.
        /// BD-41: IDisposable реализован — VContainer автоматически вызовет Dispose.
        /// </summary>
        public void Dispose()
        {
            _damageSubscription?.Dispose();
            _damageSubscription = null;
            _cultivationLevelSubscription?.Dispose();
            _cultivationLevelSubscription = null;
        }

        // === П.24: Vitality → HP пересчёт ===

        /// <summary>
        /// П.24: Пересчитать HP всех частей при изменении Vitality.
        /// Сохраняет пропорцию текущего урона (damage_ratio).
        /// Источник: ALGORITHMS.md П.24, BODY_SYSTEM.md §"Живучесть"
        /// Формула: hpMultiplier = 1 + (Vit - 10) × 0.05
        /// </summary>
        public void RecalculateHPFromVitality(float oldVitality, float newVitality)
        {
            if (!_isInitialized) return;

            float oldMult = 1f + (oldVitality - 10f) * GameConstants.VITALITY_HP_COEFFICIENT;
            float newMult = 1f + (newVitality - 10f) * GameConstants.VITALITY_HP_COEFFICIENT;

            if (Math.Abs(oldMult - newMult) < 0.0001f) return;

            float ratio = newMult / oldMult;

            foreach (var kvp in _parts)
            {
                var part = kvp.Value;
                if (part.State == BodyPartState.Severed) continue;

                // Используем SetMaxHP для пересчёта с сохранением damage_ratio
                int newMaxRed = Math.Max(1, (int)Math.Round(part.MaxRedHP * ratio));
                int newMaxBlack = part.MaxBlackHP > 0
                    ? (int)Math.Round(part.MaxBlackHP * ratio) : 0;

                part.SetMaxHP(newMaxRed, newMaxBlack);
            }

            _listDirty = true;
            _cacheDirty = true;
        }

        // === P0-01/P0-05 FIX: Приживление ===

        /// <summary>
        /// Приживить ампутированную часть тела (P0-01/P0-05 FIX).
        /// Восстанавливает HP части и публикует BodyPartReattachedEvent.
        /// SeveredDebuffSystem автоматически снимет дебаффы.
        /// </summary>
        /// <param name="type">Тип части для приживления</param>
        /// <param name="redHP">Красная HP после приживления</param>
        /// <param name="blackHP">Чёрная HP после приживления</param>
        /// <returns>True если приживление успешно</returns>
        public bool ReattachPart(BodyPartType type, int redHP, int blackHP)
        {
            if (!_isInitialized) return false;

            if (!_parts.TryGetValue(type, out var part)) return false;

            // Делегируем BodyPart.Reattach()
            if (!part.Reattach(redHP, blackHP)) return false;

            // Удаляем из набора ампутированных частей
            _severedParts.Remove(type);

            // Инвалидируем кэш
            _listDirty = true;
            _cacheDirty = true;

            // P0-05 FIX: Публикуем BodyPartReattachedEvent
            // SeveredDebuffSystem подпишется и снимет дебаффы
            _reattachedPublisher.Publish(new BodyPartReattachedEvent(_entityId, type));

            return true;
        }

        // === P1-05 FIX: Экспозиция морфологии и размера ===

        /// <summary>
        /// Получить морфологию тела (P1-05 FIX).
        /// Нужно для: Combat (расчёт урона), NPC (AI-поведение), UI.
        /// </summary>
        public Morphology GetMorphology() => _morphology;

        /// <summary>
        /// Получить класс размера тела (P1-05 FIX).
        /// Нужно для: Combat (множитель урона), UI.
        /// </summary>
        public SizeClass GetSizeClass() => _size;

        // === IBodyDataProvider (Фаза 3) ===
        // Per-entity провайдер данных тела для NPC.
        // Единая система через BodyParts — ПРОТИВОРЕЧИЕ #3/#6.
        // Существующий IBodyService (_parts) обслуживает игрока,
        // IBodyDataProvider (_entityBodyParts) обслуживает NPC.

        /// <summary>
        /// Получить BodyParts сущности по entityId.
        /// Возвращает пустой список, если сущность не найдена.
        /// </summary>
        public List<BodyPart> GetBodyParts(string entityId)
        {
            if (entityId == null || !_entityBodyParts.TryGetValue(entityId, out var parts))
                return new List<BodyPart>();
            return parts;
        }

        /// <summary>
        /// Установить BodyParts для сущности (при создании NPC).
        /// Перезаписывает существующие данные, если сущность уже есть.
        /// </summary>
        public void SetBodyParts(string entityId, List<BodyPart> parts)
        {
            if (entityId == null) return;
            if (parts == null)
            {
                _entityBodyParts.Remove(entityId);
                return;
            }
            _entityBodyParts[entityId] = parts;
        }

        /// <summary>
        /// Проверить существование сущности в провайдере.
        /// A2 FIX: также возвращает true для игрока (entityId == _entityId).
        /// </summary>
        public bool HasEntity(string entityId)
        {
            if (entityId == null) return false;
            // A2 FIX: Игрок тоже существует в провайдере
            if (entityId == _entityId) return _isInitialized;
            return _entityBodyParts.ContainsKey(entityId);
        }

        /// <summary>
        /// Удалить сущность из провайдера (при деспавне NPC).
        /// </summary>
        public void RemoveEntity(string entityId)
        {
            if (entityId != null)
                _entityBodyParts.Remove(entityId);
        }

        /// <summary>
        /// Получить сумму CurrentRedHP всех неампутированных частей (текущее здоровье).
        /// Суммирует CurrentRedHP только для частей, чьё состояние ≠ Severed.
        /// A2 FIX: также работает для игрока (проверяет _parts, если entityId == _entityId).
        /// </summary>
        public int GetCurrentHealth(string entityId)
        {
            // A2 FIX: Игрок — части тела в _parts, а не в _entityBodyParts
            if (entityId == _entityId)
            {
                int playerTotal = 0;
                foreach (var kvp in _parts)
                {
                    if (kvp.Value.State != BodyPartState.Severed)
                        playerTotal += kvp.Value.CurrentRedHP;
                }
                return playerTotal;
            }

            // NPC — части тела в _entityBodyParts
            if (entityId == null || !_entityBodyParts.TryGetValue(entityId, out var parts))
                return 0;

            int total = 0;
            foreach (var part in parts)
            {
                if (part.State != BodyPartState.Severed)
                    total += part.CurrentRedHP;
            }
            return total;
        }

        /// <summary>
        /// Получить сумму MaxRedHP всех неампутированных частей (максимальное здоровье).
        /// Суммирует MaxRedHP только для частей, чьё состояние ≠ Severed.
        /// A2 FIX: также работает для игрока (проверяет _parts, если entityId == _entityId).
        /// </summary>
        public int GetMaxHealth(string entityId)
        {
            // A2 FIX: Игрок — части тела в _parts, а не в _entityBodyParts
            if (entityId == _entityId)
            {
                int playerTotal = 0;
                foreach (var kvp in _parts)
                {
                    if (kvp.Value.State != BodyPartState.Severed)
                        playerTotal += kvp.Value.MaxRedHP;
                }
                return playerTotal;
            }

            // NPC — части тела в _entityBodyParts
            if (entityId == null || !_entityBodyParts.TryGetValue(entityId, out var parts))
                return 0;

            int total = 0;
            foreach (var part in parts)
            {
                if (part.State != BodyPartState.Severed)
                    total += part.MaxRedHP;
            }
            return total;
        }

        /// <summary>
        /// Проверить, жива ли сущность (Спринт 1 A2).
        /// Возвращает false, если любая жизненно важная часть (Head, Heart)
        /// имеет CurrentRedHP <= 0.
        /// Для игрока проверяет _parts, для NPC — _entityBodyParts.
        /// </summary>
        public bool IsEntityAlive(string entityId)
        {
            // Игрок
            if (entityId == _entityId)
            {
                foreach (var kvp in _parts)
                {
                    if (kvp.Value.IsVital && kvp.Value.CurrentRedHP <= 0)
                        return false;
                }
                return true;
            }

            // NPC
            if (entityId == null || !_entityBodyParts.TryGetValue(entityId, out var parts))
                return false; // Сущность не найдена — считаем мёртвой

            foreach (var part in parts)
            {
                if (part.IsVital && part.CurrentRedHP <= 0)
                    return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Структура данных сохранения для одной части тела.
    /// JsonUtility требует [Serializable] и публичных полей.
    /// </summary>
    [Serializable]
    public struct BodyPartSaveEntry
    {
        // Тип части тела (int — сериализация enum как числа)
        public int partType;

        // Состояние части тела (int — сериализация enum как числа)
        public int state;

        // Текущая красная (функциональная) HP
        public int currentRedHP;

        // Текущая чёрная (структурная) HP
        public int currentBlackHP;
    }

    /// <summary>
    /// Структура данных сохранения для BodyService.
    /// JsonUtility требует [Serializable] и публичных полей.
    /// </summary>
    [Serializable]
    public class BodySaveData
    {
        // Морфология тела (int — сериализация enum как числа)
        public int morphology;

        // Материал тела (int — сериализация enum как числа)
        public int material;

        // P1-02 FIX: Класс размера (int — сериализация enum как числа)
        public int size;

        // Упрощённые данные по частям тела
        public BodyPartSaveEntry[] parts;
    }
}
