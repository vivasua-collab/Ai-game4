#nullable enable
// Создано: 2026-05-09
// Редактировано: 2026-05-09 — CMB-A01: порядок пайплайна урона (QiBuffer ДО брони)
// Редактировано: 2026-05-09 — CMB-A02: интеграция LevelSuppression через DamageRequest
// Редактировано: 2026-05-09 — CMB-A05: вызов DetermineAttackResult()
// Редактировано: 2026-05-09 — CMB-A07: DefenderElement из DamageRequest
// Редактировано: 2026-05-09 — CMB-A08: DefenderMaterial из DamageRequest
// Редактировано: 2026-05-09 — EVT-01: убрана инъекция IQiBufferService/IEquipmentService,
//   буфер Ци рассчитывается inline (формулы из GameConstants/Core),
//   броня берётся из кэша EquipmentChangedEvent,
//   расход Ци через QiConsumeRequestEvent
// Редактировано: 2026-05-20 18:43:21 UTC — Фаза 3: per-entity armor через IEquipmentDataProvider (3.M)
// Редактировано: 2026-05-21 19:25:59 UTC — Спринт 2 B2: IBuffService + IFormationService интеграция
// Редактировано: 2026-05-22 07:55:00 UTC — Аудит CRIT-1: integer math для буффов/формации (ЗАПРЕТ 3.9)
//   + MED-2: formation defense bonus
// Редактировано: 2026-05-22 09:51:00 UTC — Спринт 5: DetermineAttackResult из статов (C1/C2/C3)
// Редактировано: 2026-05-22 09:51:00 UTC — Спринт 6: coverage roll (C5) + penetration (C6)
// Редактировано: 2026-05-22 11:30:00 UTC — Спринт 7 C8: Element в DamageAppliedEvent
// Редактировано: 2026-05-22 13:08:27 UTC — P0-X1 FIX: QiConsumeRequestEvent + EntityId; P2-4.2 FIX: buffReduction в noArmorContext
// Редактировано: 2026-05-22 13:48:17 UTC — Этап 2.1: QiBuffer расчёт → integer math (ЗАПРЕТ 3.9)
// Редактировано: 2026-05-25 07:01:36 UTC — ЗАПРЕТ 3.9: _cachedTotalArmor float → int, конвертация на границе события
// Реализация IDamageService — ЕДИНЫЙ пайплайн урона.
// Заменяет legacy два несовместимых пайплайна (ICombatant.DealDamage / ICombatTarget.TakeDamage).
// КРИТИЧЕСКАЯ: Все типы урона проходят через этот сервис.
using System;
using CultivationGame.Core;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Combat
{
    /// <summary>
    /// Реализация IDamageService.
    /// ЕДИНЫЙ пайплайн урона — объединяет ICombatant и ICombatTarget.
    ///
    /// Пайплайн (по COMBAT_SYSTEM.md / ALGORITHMS.md §5):
    /// 1. DamageCalculator.CalculateRawDamage() — базовый урон × грейд × potency
    /// 2. LevelSuppression.CalculateSuppression() — подавление уровнем (CMB-A02)
    /// 3. DamageCalculator.GetElementMultiplier() — стихийные преимущества (CMB-A07)
    /// 4. DamageCalculator.DetermineHitPart() — определение части тела
    /// 5. DamageCalculator.DetermineAttackResult() — активная защита (CMB-A05)
    /// 6. QiBuffer поглощение (inline расчёт по формулам GameConstants) — СЛОЙ 5, ДО брони (CMB-A01)
    /// 7. DefenseProcessor.ApplyDefense() — броня + материал тела (СЛОЙ 6-8)
    /// 8. Публикация DamageAppliedEvent — BodyService.ApplyDamage() автоматически
    ///
    /// АРХИТЕКТУРА: DamageService НЕ инжектит IBodyService, IQiBufferService, IEquipmentService напрямую.
    /// BodyService подписан на DamageAppliedEvent и применяет урон автоматически.
    /// QiBuffer поглощение рассчитывается inline (формулы из GameConstants — Core, не Qi модуль).
    /// Броня кэшируется из EquipmentChangedEvent.
    /// Это сохраняет Hub-and-Spoke: Combat ← Core.Messaging → Body/Qi/Equipment.
    ///
    /// EVT-01: полная независимость модулей через событийную модель.
    /// </summary>
    public class DamageService : IDamageService, IDisposable
    {
        // === Зависимости (DI через конструктор) ===
        private readonly IPublisher<DamageAppliedEvent> _damageAppliedPub;

        // Спринт 2 B2: IBuffService для модификаторов урона/защиты
        private readonly IBuffService _buffService;
        // Спринт 2 B2/C9: IFormationService для баффов формаций
        private readonly IFormationService _formationService;

        // EVT-01: подписки на кросс-модульные события (вместо инъекции IQiBufferService/IEquipmentService)
        private readonly ISubscriber<QiBufferStateChangedEvent> _qiBufferStateChangedSub;
        private readonly ISubscriber<EquipmentChangedEvent> _equipmentChangedSub;
        private readonly ISubscriber<QiChangedEvent> _qiChangedSub;
        private readonly IPublisher<QiConsumeRequestEvent> _qiConsumeRequestPub;

        // Фаза 3 (3.M): per-entity armor через IEquipmentDataProvider
        private readonly IEquipmentDataProvider _equipmentDataProvider;

        // Спринт 4 B8: per-entity QiBuffer через IQiDataProvider
        private readonly IQiDataProvider _qiDataProvider;

        // EVT-01: кэш состояния из событий
        private bool _cachedBufferIsActive;
        private QiBufferMode _cachedBufferMode;
        private long _cachedBufferQiInvested;
        private long _cachedCurrentQi; // для расчёта буфера
        private int _cachedTotalArmor; // ЗАПРЕТ 3.9: int вместо float, конвертация на границе EquipmentChangedEvent

        // IDisposable для подписок
        private IDisposable _qiBufferStateChangedSubscription;
        private IDisposable _equipmentChangedSubscription;
        private IDisposable _qiChangedSubscription;

        // === Конструктор (VContainer) ===
        public DamageService(
            IPublisher<DamageAppliedEvent> damageAppliedPub,
            ISubscriber<QiBufferStateChangedEvent> qiBufferStateChangedSub,
            ISubscriber<EquipmentChangedEvent> equipmentChangedSub,
            ISubscriber<QiChangedEvent> qiChangedSub,
            IPublisher<QiConsumeRequestEvent> qiConsumeRequestPub,
            IEquipmentDataProvider equipmentDataProvider, // Фаза 3 (3.M)
            IBuffService buffService, // Спринт 2 B2
            IFormationService formationService, // Спринт 2 B2/C9
            IQiDataProvider qiDataProvider) // Спринт 4 B8: per-entity QiBuffer
        {
            _damageAppliedPub = damageAppliedPub;
            _qiBufferStateChangedSub = qiBufferStateChangedSub;
            _equipmentChangedSub = equipmentChangedSub;
            _qiChangedSub = qiChangedSub;
            _qiConsumeRequestPub = qiConsumeRequestPub;
            _equipmentDataProvider = equipmentDataProvider; // Фаза 3 (3.M)
            _buffService = buffService; // Спринт 2 B2
            _formationService = formationService; // Спринт 2 B2/C9
            _qiDataProvider = qiDataProvider; // Спринт 4 B8

            // EVT-01: подписка на кэш состояния буфера
            _qiBufferStateChangedSubscription = _qiBufferStateChangedSub.Subscribe((in QiBufferStateChangedEvent e) => {
                _cachedBufferIsActive = e.IsActive;
                _cachedBufferMode = e.Mode;
                _cachedBufferQiInvested = e.QiInvested;
            });

            // EVT-01: подписка на кэш брони
            _equipmentChangedSubscription = _equipmentChangedSub.Subscribe((in EquipmentChangedEvent e) => {
                _cachedTotalArmor = (int)e.TotalArmor; // ЗАПРЕТ 3.9: конвертация на границе события
            });

            // EVT-01: подписка на кэш текущего Ци (для расчёта буфера)
            _qiChangedSubscription = _qiChangedSub.Subscribe((in QiChangedEvent e) => {
                _cachedCurrentQi = e.Current;
            });
        }

        // === IDamageService ===

        /// <summary>
        /// Рассчитать полный урон по единому пайплайну.
        /// Порядок слоёв соответствует COMBAT_SYSTEM.md / ALGORITHMS.md §5.
        /// </summary>
        public DamageResult CalculateDamage(DamageRequest request)
        {
            // === СЛОЙ 1: Базовый урон (грейд × potency) ===
            int rawDamage = DamageCalculator.CalculateRawDamage(request);

            // === СЛОЙ 2: Подавление уровнем (CMB-A02) ===
            // P1-4.1 FIX: integer math — промилле вместо float (ЗАПРЕТ 3.9)
            int suppressionPermil = LevelSuppression.CalculateSuppressionPermil(
                request.AttackerLevel, request.DefenderLevel, request.AttackType);

            // === СЛОЙ 3: Стихийный множитель (CMB-A07: DefenderElement из запроса) ===
            // P1-5.1 FIX: integer math — промилле вместо float (ЗАПРЕТ 3.9)
            int elementPermil = DamageCalculator.GetElementMultiplierPermil(
                request.Element, request.DefenderElement);

            // Итоговый урон до защиты (integer math: long для промежуточных)
            int preDefenseDamage = (int)((long)rawDamage * suppressionPermil / 1000 * elementPermil / 1000);

            // СЛОЙ 3a: Бафф на урон атакующего (Спринт 2 B2)
            // CRIT-1: integer math — GetStatModifierPermil() возвращает модификатор в промилле.
            // Формула: preDefenseDamage = preDefenseDamage * (1000 + modPermil) / 1000
            int atkBuffPermil = _buffService.GetStatModifierPermil(request.AttackerId, StatType.Damage);
            if (atkBuffPermil != 0)
            {
                preDefenseDamage = (int)((long)preDefenseDamage * (1000 + atkBuffPermil) / 1000);
            }

            // СЛОЙ 3b: Бафф формаций — DAMAGE (Спринт 2 B2/C9)
            // CRIT-1: integer math — GetFormationBonusPermil() возвращает бонус в промилле.
            int formationDmgPermil = _formationService.GetFormationBonusPermil(StatType.Damage);
            if (formationDmgPermil != 0)
            {
                preDefenseDamage = (int)((long)preDefenseDamage * (1000 + formationDmgPermil) / 1000);
            }

            // === СЛОЙ 4: Определение части тела ===
            // Спринт 8 C10: передаём TargetMorphology для выбора таблицы попадания
            BodyPartType hitPart = DamageCalculator.DetermineHitPart(request.TargetMorphology);

            // === СЛОЙ 5: Активная защита (CMB-A05) ===
            // Спринт 5 C1/C2/C3: передаём статы для расчёта шансов
            // P2-5.2 FIX: передаём DefenderSTR для блока вместо AttackerSTR
            CombatAttackResult attackResult = DamageCalculator.DetermineAttackResult(
                request.ActiveDefense,
                request.DefenderAGI,       // C1: для уклонения
                request.ArmorDodgePenalty, // C1: штраф брони
                request.DefenderSTR,       // P2-5.2 FIX: STR защищающегося для блока
                request.WeaponParryBonus,  // C3: бонус парирования
                request.ShieldBlock,       // C3: блок щита
                request.AttackerLuck,      // C2: шанс крита
                request.TechniqueCritBonus // C2: бонус крита техники
            );

            // Уклонение — полный промах
            if (attackResult == CombatAttackResult.Dodge)
            {
                return new DamageResult(0, 0, 0, hitPart, CombatAttackResult.Dodge, false);
            }

            // Парирование — снижение урона на 50%
            // P1-4.1 FIX: integer math — defense multiplier в промилле (ЗАПРЕТ 3.9)
            int defensePermil = attackResult switch
            {
                CombatAttackResult.Parry => 500,      // 50% урона
                CombatAttackResult.Block => 500,      // 50% урона
                CombatAttackResult.CriticalHit => 1500, // 150% урона (крит ×1.5)
                _ => 1000                              // 100% — без изменений
            };
            int postDefenseActionDamage = (int)((long)preDefenseDamage * defensePermil / 1000);

            // === СЛОЙ 5b: Ци-буфер (ДО брони — CMB-A01) ===
            // Ци защищает от ЛЮБОГО урона — даже во сне (COMBAT_SYSTEM.md §5)
            // Спринт 4 B8: per-entity QiBuffer — для NPC через IQiDataProvider,
            // для игрока — из кэша QiBufferStateChangedEvent
            // Этап 2.1: ЗАПРЕТ 3.9 — piercingDamage в integer
            int absorbedByQi = 0;
            int piercingDamage = postDefenseActionDamage;

            bool bufferActive;
            QiBufferMode bufferMode;
            long bufferQiInvested;
            long targetCurrentQi;

            // P2-4.1 FIX: используем флаг IsPlayerTarget вместо магической строки "player"
            if (request.IsPlayerTarget)
            {
                // Игрок — из кэша событий (быстрый путь)
                bufferActive = _cachedBufferIsActive;
                bufferMode = _cachedBufferMode;
                bufferQiInvested = _cachedBufferQiInvested;
                targetCurrentQi = _cachedCurrentQi;
            }
            else
            {
                // NPC — из IQiDataProvider (B8: per-entity)
                bufferActive = _qiDataProvider.IsQiBufferActive(request.TargetId);
                bufferMode = _qiDataProvider.GetQiBufferMode(request.TargetId);
                bufferQiInvested = _qiDataProvider.GetQiBufferInvested(request.TargetId);
                targetCurrentQi = _qiDataProvider.GetCurrentQi(request.TargetId);
            }

            if (bufferActive)
            {
                var bufferResult = CalculateBufferAbsorption(
                    piercingDamage, request.Type, bufferMode, bufferQiInvested, targetCurrentQi, request.TargetId);
                absorbedByQi = bufferResult.AbsorbedDamage;
                piercingDamage = bufferResult.PiercingDamage;
            }

            // Чистый урон игнорирует броню и Ци-буфер
            if (request.Type == DamageType.Pure)
            {
                piercingDamage = postDefenseActionDamage;
                absorbedByQi = 0;
            }

            // === СЛОЙ 6-8: Защита (броня + материал тела) ===
            // CMB-A08: используем DefenderMaterial из запроса вместо хардкода
            // Фаза 3 (3.M): per-entity armor через IEquipmentDataProvider
            // Этап 2.1: ЗАПРЕТ 3.9 — armor в integer
            int armorValue;
            if (_equipmentDataProvider.HasEntity(request.TargetId))
            {
                armorValue = (int)_equipmentDataProvider.GetTotalArmor(request.TargetId);
            }
            else
            {
                armorValue = _cachedTotalArmor; // ЗАПРЕТ 3.9: уже int
            }

            // Спринт 2 B2: Бафф на защиту защищающегося
            // CRIT-1: integer math — GetStatModifierPermil() возвращает модификатор в промилле.
            // MED-2: Formation defense bonus тоже добавляется.
            int defBuffPermil = _buffService.GetStatModifierPermil(request.TargetId, StatType.Defense);
            int buffReductionPermil = defBuffPermil > 0 ? defBuffPermil : 0;

            // MED-2: Formation defense bonus
            int formationDefPermil = _formationService.GetFormationBonusPermil(StatType.Defense);
            if (formationDefPermil > 0)
                buffReductionPermil += formationDefPermil;

            // Спринт 6 C5: Coverage roll — проверка покрытия брони
            // if (random() < armor.Coverage) → броня покрывает, применить DefenseProcessor
            // else → урон проходит напрямую мимо брони
            bool armorCoversHit = true;
            int armorCoverage = _equipmentDataProvider.GetArmorCoverage(request.TargetId);
            if (armorCoverage > 0 && armorCoverage < 100)
            {
                int coverageRoll = Random.Shared.Next(0, 100);
                armorCoversHit = coverageRoll < armorCoverage;
            }
            else if (armorCoverage <= 0)
            {
                armorCoversHit = false; // Нет брони — не покрывает
            }
            // armorCoverage >= 100 → armorCoversHit = true (полное покрытие)

            int postArmorDamage;
            int absorbedByArmor;

            if (armorCoversHit)
            {
                // Броня покрывает — применить DefenseProcessor с пробитием (Спринт 6 C6)
                var defenseContext = new DefenseContext(
                    request.TargetId, armorValue, request.DefenderMaterial,
                    buffReductionPermil, request.Penetration); // C6: penetration
                postArmorDamage = DefenseProcessor.ApplyDefense(piercingDamage, defenseContext);
                absorbedByArmor = piercingDamage - postArmorDamage;
            }
            else
            {
                // Броня не покрыла — урон проходит напрямую (только material reduction)
                // P2-4.2 FIX: defensive buffs (Shock, Formation) работают даже без покрытия брони
                var noArmorContext = new DefenseContext(
                    request.TargetId, 0, request.DefenderMaterial, buffReductionPermil, 0);
                postArmorDamage = DefenseProcessor.ApplyDefense(piercingDamage, noArmorContext);
                absorbedByArmor = 0;
            }

            // === Определение фатальности ===
            bool isFatal = DamageCalculator.IsFatalHit(hitPart, postArmorDamage);

            // === Публикация DamageAppliedEvent ===
            // BodyService автоматически применит урон через подписку на это событие
            int finalDamage = postArmorDamage;

            var damageResult = new DamageResult(
                finalDamage, absorbedByQi, absorbedByArmor,
                hitPart, attackResult, isFatal);

            // Публикуем событие — BodyService.ApplyDamage() сработает автоматически
            // Спринт 7 C8: передаём Element для стихийных эффектов
            // P2-7.3 FIX: передаём AttackSubtype для различения slashing/piercing от blunt
            var attackSubtype = GetCombatSubtypeFromRequest(request);
            _damageAppliedPub.Publish(new DamageAppliedEvent(
                request.AttackerId, request.TargetId,
                finalDamage, request.Type, request.Element,
                hitPart, attackResult, attackSubtype));

            return damageResult;
        }

        /// <summary>
        /// Применить защиту к урону (броня + материал).
        /// </summary>
        public int ApplyDefense(int damage, DefenseContext context)
        {
            return DefenseProcessor.ApplyDefense(damage, context);
        }

        // === EVT-01: Inline расчёт поглощения Ци-буфера ===
        // Формулы идентичны QiBufferService.ProcessRawQiDamage/ProcessShieldDamage.
        // Используются ТОЛЬКО константы из GameConstants (Core), без зависимостей от Qi модуля.

        /// <summary>
        /// Рассчитать поглощение урона через Ци-буфер.
        /// EVT-01: inline расчёт вместо _qiBufferService.AbsorbDamage().
        /// Формулы из GameConstants (Core) — идентичны QiBufferService.
        /// Спринт 4 B8: принимает параметры буфера вместо использования глобального кэша.
        /// P0-X1 FIX: передаёт EntityId в QiConsumeRequestEvent для корректного списания Ци с NPC.
        /// Этап 2.1: ЗАПРЕТ 3.9 — integer math (промилле), без float.
        /// </summary>
        private QiBufferResult CalculateBufferAbsorption(int incomingDamage, DamageType damageType,
            QiBufferMode bufferMode, long qiInvested, long currentQi, string targetEntityId)
        {
            if (incomingDamage <= 0)
                return new QiBufferResult(0, incomingDamage, 0, currentQi, false, false);

            if (currentQi < GameConstants.MIN_QI_FOR_BUFFER)
                return new QiBufferResult(0, incomingDamage, 0, currentQi, bufferMode == QiBufferMode.Shield, false);

            bool isQiDamage = damageType == DamageType.Qi || damageType == DamageType.Elemental;

            QiBufferResult result = bufferMode == QiBufferMode.Shield
                ? ProcessShieldDamage(incomingDamage, currentQi, isQiDamage)
                : ProcessRawQiDamage(incomingDamage, currentQi, isQiDamage);

            // P0-X1 FIX: Запрашиваем расход Ци через событие С EntityId (QiService фильтрует по EntityId)
            // Для NPC дополнительно списываем Qi напрямую через IQiDataProvider
            if (result.QiConsumed > 0)
            {
                _qiConsumeRequestPub.Publish(new QiConsumeRequestEvent(result.QiConsumed, "DamageService", targetEntityId));
                // P0-X1: NPC Qi — прямое списание из IQiDataProvider (QiService обрабатывает только игрока)
                if (!string.IsNullOrEmpty(targetEntityId) && targetEntityId != "player")
                {
                    _qiDataProvider.TryConsumeQi(targetEntityId, result.QiConsumed);
                }
            }

            return result;
        }

        /// <summary>
        /// Обработка урона в режиме сырой Ци.
        /// Источник: ALGORITHMS.md §2.3
        /// Идентично QiBufferService.ProcessRawQiDamage — формулы из GameConstants (Core).
        /// Этап 2.1: ЗАПРЕТ 3.9 — integer math (промилле), без float.
        /// Формулы: absorbableDamage = damage × absorptionPermil / 1000,
        /// guaranteedPiercing = damage × piercingPermil / 1000,
        /// requiredQi = absorbableDamage × ratio.
        /// </summary>
        private QiBufferResult ProcessRawQiDamage(int damage, long currentQi, bool isQiDamage)
        {
            // Этап 2.1: integer константы (ЗАПРЕТ 3.9)
            int absorptionPermil = isQiDamage
                ? GameConstants.RAW_QI_ABSORPTION_PERMIL      // 900 = 90% для техник Ци
                : GameConstants.PHYSICAL_RAW_QI_ABSORPTION_PERMIL; // 800 = 80% для физики

            int piercingPermil = isQiDamage
                ? GameConstants.RAW_QI_PIERCING_PERMIL        // 100 = 10% для техник Ци
                : GameConstants.PHYSICAL_RAW_QI_PIERCING_PERMIL;   // 200 = 20% для физики

            int ratio = isQiDamage
                ? GameConstants.RAW_QI_RATIO_INT           // 3:1 для техник Ци
                : GameConstants.PHYSICAL_RAW_QI_RATIO_INT; // 5:1 для физики

            // long для промежуточных, чтобы избежать overflow
            int absorbableDamage = (int)((long)damage * absorptionPermil / 1000);
            int guaranteedPiercing = (int)((long)damage * piercingPermil / 1000);
            long requiredQi = (long)absorbableDamage * ratio;

            if (currentQi >= requiredQi)
            {
                return new QiBufferResult(
                    absorbableDamage, guaranteedPiercing,
                    requiredQi, currentQi - requiredQi,
                    false, false);
            }
            else
            {
                // Недостаточно Ци — частичное поглощение
                // absorbed = absorbableDamage × currentQi / requiredQi (integer division)
                int absorbed = requiredQi > 0 ? (int)(absorbableDamage * currentQi / requiredQi) : 0;
                int piercingDamage = damage - absorbed;
                return new QiBufferResult(
                    absorbed, piercingDamage,
                    currentQi, 0,
                    false, true);
            }
        }

        /// <summary>
        /// Обработка урона в режиме щита.
        /// Источник: ALGORITHMS.md §2.3
        /// Идентично QiBufferService.ProcessShieldDamage — формулы из GameConstants (Core).
        /// Этап 2.1: ЗАПРЕТ 3.9 — integer math, без float.
        /// Формулы: requiredQi = damage × ratio, piercing = damage - absorbed.
        /// </summary>
        private QiBufferResult ProcessShieldDamage(int damage, long currentQi, bool isQiDamage)
        {
            // Этап 2.1: integer константы (ЗАПРЕТ 3.9)
            int ratio = isQiDamage
                ? GameConstants.SHIELD_QI_RATIO_INT        // 1:1 для техник Ци
                : GameConstants.PHYSICAL_SHIELD_QI_RATIO_INT;   // 2:1 для физики

            long requiredQi = (long)damage * ratio;

            if (currentQi >= requiredQi)
            {
                return new QiBufferResult(
                    damage, 0,
                    requiredQi, currentQi - requiredQi,
                    true, false);
            }
            else
            {
                // Недостаточно Ци — частичное поглощение
                // absorbed = damage × currentQi / requiredQi (integer division)
                int absorbed = requiredQi > 0 ? (int)((long)damage * currentQi / requiredQi) : 0;
                int piercingDamage = damage - absorbed;
                return new QiBufferResult(
                    absorbed, piercingDamage,
                    currentQi, 0,
                    true, true);
            }
        }

        public void Dispose()
        {
            _qiBufferStateChangedSubscription?.Dispose();
            _qiBufferStateChangedSubscription = null;
            _equipmentChangedSubscription?.Dispose();
            _equipmentChangedSubscription = null;
            _qiChangedSubscription?.Dispose();
            _qiChangedSubscription = null;
        }

        /// <summary>
        /// P2-7.3 FIX: Получить CombatSubtype из DamageRequest.
        /// Используется для передачи подтипа атаки в DamageAppliedEvent,
        /// чтобы CombatConsequencesService мог различать slashing/piercing от blunt.
        /// </summary>
        private static CombatSubtype GetCombatSubtypeFromRequest(DamageRequest request)
        {
            // Если подтип явно задан — используем его
            if (request.AttackSubtype != CombatSubtype.None)
                return request.AttackSubtype;

            // Fallback: маппинг AttackType → CombatSubtype (для обратной совместимости)
            return request.AttackType switch
            {
                AttackType.MeleeStrike => CombatSubtype.MeleeStrike,
                AttackType.MeleeWeapon => CombatSubtype.MeleeWeapon,
                AttackType.Ranged => CombatSubtype.RangedProjectile,
                AttackType.Ultimate => CombatSubtype.None,
                AttackType.Technique => CombatSubtype.None,
                _ => CombatSubtype.None
            };
        }
    }
}
