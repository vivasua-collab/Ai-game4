#nullable enable
// Создано: 2026-05-18 12:00:00 UTC — Body доработка: фабрика создания тел
// Редактировано: 2026-05-18 12:00:00 UTC — P0-01 FIX: передача pt.Functions, P0-02/P0-03 FIX: SetMaxHP, P2-01: удалён мёртвый CreateBody, P2-02: удалён дублирующий RecalculateHP
// Редактировано: 2026-05-18 — P1-10 FIX: реализует IBodyFactory
// Migrated from Ai-game3 (Unity) to Ai-game4 (Godot). UnityEngine.MathF.RoundToInt → (int)Math.Round.
// Заменяет BodyMorphology.cs — data-driven подход через BodyTemplate.
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.Body
{
    /// <summary>
    /// Фабрика создания тел.
    /// Заменяет хардкод из BodyMorphology.cs на data-driven подход через BodyTemplate.
    /// Источник: BODY_SYSTEM.md §"Живучесть", §"Классы размера"
    /// </summary>
    public sealed class BodyFactory : IBodyFactory
    {
        private readonly BodyTemplateProvider _templateProvider;

        public BodyFactory(BodyTemplateProvider templateProvider)
        {
            _templateProvider = templateProvider ?? throw new ArgumentNullException(nameof(templateProvider));
        }

        /// <summary>
        /// Создать тело для новой сущности.
        /// Вычисляет HP на основе: baseHP × vitalityMultiplier × sizeMultiplier.
        /// P0-01 FIX: Передаёт BodyPartFunction из шаблона.
        /// P0-02/P0-03 FIX: Использует SetMaxHP для корректной установки MaxBlackHP.
        /// </summary>
        public List<BodyPart> CreateBody(Morphology morphology, SizeClass size, float vitality)
        {
            var template = _templateProvider.GetTemplate(morphology);
            var sizeMult = GameConstants.SizeClassHPMultipliers.TryGetValue(size, out var sm) ? sm : 1f;
            var vitMult = CalculateVitalityMultiplier(vitality, template.ScalingMode);

            var parts = new List<BodyPart>();
            foreach (var pt in template.Parts)
            {
                float effectiveMult = pt.VitalityScalesHP ? vitMult : 1f;
                int effectiveRed = (int)Math.Round(pt.BaseFunctionalHP * effectiveMult * sizeMult);
                int effectiveBlack = (int)Math.Round(pt.BaseStructuralHP * effectiveMult * sizeMult);

                // P0-01 FIX: Передаём pt.Functions в конструктор BodyPart
                var part = new BodyPart(pt.PartType, effectiveRed, pt.IsVital, pt.Functions);

                // P0-02/P0-03 FIX: Используем SetMaxHP для корректной установки MaxBlackHP
                // Конструктор BodyPart устанавливает MaxBlackHP = effectiveRed × STRUCTURAL_HP_MULTIPLIER
                // Для Heart/Core/Essence (BaseStructuralHP=0) нужно обнулить MaxBlackHP
                // Для частей с кастомной структурной HP — установить корректное значение
                if (effectiveBlack != part.MaxBlackHP)
                {
                    part.SetMaxHP(effectiveRed, effectiveBlack);
                }

                parts.Add(part);
            }

            return parts;
        }

        /// <summary>
        /// Вычислить множитель HP от Vitality.
        /// Формула: 1 + (Vit - 10) × 0.05
        /// Источник: BODY_SYSTEM.md §"Живучесть"
        /// </summary>
        public static float CalculateVitalityMultiplier(float vitality, VitalityScalingMode mode)
        {
            return mode switch
            {
                VitalityScalingMode.Standard => 1f + (vitality - 10f) * GameConstants.VITALITY_HP_COEFFICIENT,
                VitalityScalingMode.Amorphous => 1f, // HP = Qi, не масштабируется
                VitalityScalingMode.Construct => 1f,  // HP от размера, не от Vit
                _ => 1f
            };
        }
    }
}
