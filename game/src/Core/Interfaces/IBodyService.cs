#nullable enable
// Создано: 2026-05-08 10:07:00 UTC
// Редактировано: 2026-05-20 18:43:21 UTC — Фаза 3: TODO 3.9 о переходе HP на float
// Редактировано: 2026-05-09 14:00:00 UTC — аудит: R-06 ApplyDamage принимает totalDamage, добавлено IsVital
// Редактировано: 2026-05-18 — П.24: RecalculateHPFromVitality, +BodyPartFunction в BodyPartData
// Редактировано: 2026-05-18 12:00:00 UTC — P1-06 FIX: Initialize, ProcessRegeneration добавлены в интерфейс
// Редактировано: 2026-05-18 13:10:29 UTC — P0-01/P0-05 FIX: +ReattachPart, P1-04 FIX: +BaseHitChance в BodyPartData, P1-05 FIX: +GetMorphology/GetSizeClass
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;
namespace CultivationGame.Core.Interfaces
{
    public interface IBodyService
    {
        string EntityId { get; }
        BodyPartState GetPartState(BodyPartType type);
        bool IsPartSevered(BodyPartType type);
        bool IsPartDisabled(BodyPartType type);

        /// <summary>
        /// AUDIT-0911 BOD-2/PLR-1: единое правило тел — vital Head/Heart
        /// RedHP ≤ 0 = мёртв (реализация BodyService уже существует —
        /// IBodyDataProvider.IsEntityAlive; интерфейс теперь честно его
        /// экспортирует и для consumers IBodyService, например
        /// PlayerService.IsAlive).
        /// </summary>
        bool IsEntityAlive(string entityId);
        float GetPartHealthRatio(BodyPartType type);

        /// <summary>
        /// АУДИТ-1020 (BUG-010, волна 2): источник последнего нанесённого удара
        /// по игроку (ID атакующего; null — урон без явного источника, напр.
        /// прямой вызов из QA/старых путей). BodyService кэширует его в
        /// ApplyDamage ДО применения урона — к моменту BodyCriticalEvent
        /// (смерть публикуется синхронно внутри того же вызова) killing blow
        /// детерминированно известен (гонка кэша оверлея смерти исключена).
        /// </summary>
        string? LastDamageSourceId { get; }

        /// <summary>
        /// АУДИТ-1020 (BUG-010): sourceId — источник удара (кэшируется в
        /// LastDamageSourceId ДО применения урона). Опционален: все прежние
        /// вызовы (2 аргумента) не меняются.
        /// </summary>
        void ApplyDamage(BodyPartType target, int totalDamage, string? sourceId = null);
        void HealPart(BodyPartType target, int amount);

        /// <summary>
        /// ST11-001 (Этап-11 аудита, 23.10): восстановить КРАСНУЮ HP части до
        /// максимума — путь ПОЛНОГО восстановления (респавн), без 70/30-сплита
        /// лечения. Ампутированная часть игнорируется (приживление — отдельный
        /// контракт ReattachPart). Публикует BodyPartHealedEvent с фактическим
        /// количеством. Источник нормы: DEATH_AND_LOOT §4.2 «полное лечение
        /// до RedHP=max» — HealPart(missing) закрывал дефицит лишь на 70%
        /// (RED_HP_RATIO-сплит в чёрную HP), а частично леченная vital-часть
        /// меняла исход проверки PlayerService.Revive (голова 35/50).
        /// </summary>
        void HealPartToFullRed(BodyPartType target);

        /// <summary>
        /// ST11-007 (Этап-11 аудита): слоты экипировки, блокируемые ампутацией
        /// данной части (Core-контракт для производных состояний после
        /// RestoreState — InventoryModule не ссылается на Modules.Body).
        /// </summary>
        EquipmentSlot[] GetBlockedSlotsForPart(BodyPartType part);

        bool IsSlotBlocked(EquipmentSlot slot);
        IReadOnlyList<BodyPartData> GetAllParts();

        /// <summary>
        /// Инициализировать тело сущности.
        /// </summary>
        void Initialize(string entityId, Morphology morphology, BodyMaterial material, SizeClass size, float vitality);

        /// <summary>
        /// Обработать регенерацию за один кадр.
        /// </summary>
        void ProcessRegeneration(float deltaTime);

        /// <summary>
        /// П.24: Пересчитать HP всех частей при изменении Vitality.
        /// Сохраняет пропорцию текущего урона (damage_ratio).
        /// </summary>
        void RecalculateHPFromVitality(float oldVitality, float newVitality);

        /// <summary>
        /// Приживить ампутированную часть тела (P0-01/P0-05 FIX).
        /// Публикует BodyPartReattachedEvent → SeveredDebuffSystem снимает дебаффы.
        /// </summary>
        bool ReattachPart(BodyPartType type, int redHP, int blackHP);

        /// <summary>
        /// Получить морфологию тела (P1-05 FIX).
        /// </summary>
        Morphology GetMorphology();

        /// <summary>
        /// Получить класс размера тела (P1-05 FIX).
        /// </summary>
        SizeClass GetSizeClass();
    }

    // ЗАПРЕТ 3.9: HP остаётся int. Дробные типы для HP ЗАПРЕЩЕНЫ.
    // BodyPartData использует int для HP — это корректно и не подлежит изменению.
    public readonly struct BodyPartData
    {
        public readonly BodyPartType Type;
        public readonly BodyPartState State;
        public readonly bool IsVital;
        public readonly int CurrentRedHP;
        public readonly int MaxRedHP;
        public readonly int CurrentBlackHP;
        public readonly int MaxBlackHP;
        public readonly BodyPartFunction Functions;
        public readonly float BaseHitChance;  // P1-04 FIX: шанс попадания для Combat модуля

        public BodyPartData(BodyPartType type, BodyPartState state, bool isVital, int curRed, int maxRed, int curBlack, int maxBlack,
            BodyPartFunction functions = BodyPartFunction.None, float baseHitChance = 0.1f)
        {
            Type = type; State = state; IsVital = isVital;
            CurrentRedHP = curRed; MaxRedHP = maxRed;
            CurrentBlackHP = curBlack; MaxBlackHP = maxBlack;
            Functions = functions;
            BaseHitChance = baseHitChance;
        }
    }
}
