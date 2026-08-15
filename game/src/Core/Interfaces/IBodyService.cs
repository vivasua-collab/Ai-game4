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
        float GetPartHealthRatio(BodyPartType type);
        void ApplyDamage(BodyPartType target, int totalDamage);
        void HealPart(BodyPartType target, int amount);
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
