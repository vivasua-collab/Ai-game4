#nullable enable
// Создано: 2026-05-18 12:00:00 UTC — Body доработка: шаблон части тела
// Data-driven определение части тела для BodyTemplate.
using System;

namespace CultivationGame.Core.Data
{
    /// <summary>
    /// Шаблон части тела — data-driven определение.
    /// Содержит базовые HP, функции, слоты экипировки, иерархию.
    /// Используется BodyFactory для создания BodyPart.
    /// Источник: BODY_SYSTEM.md §"Части тела гуманоида"
    /// </summary>
    public sealed class BodyPartTemplate
    {
        /// <summary>Строковый идентификатор ("head", "torso", "left_arm" ...)</summary>
        public string PartId { get; }

        /// <summary>Тип части тела (enum для быстрого маппинга)</summary>
        public BodyPartType PartType { get; }

        /// <summary>Базовая функциональная HP (красная)</summary>
        public float BaseFunctionalHP { get; }

        /// <summary>
        /// Базовая структурная HP (чёрная).
        /// Обычно = FunctionalHP × 2.0. Heart = 0 (только красная HP).
        /// </summary>
        public float BaseStructuralHP { get; }

        /// <summary>Функции части тела (комбинируемые флаги)</summary>
        public BodyPartFunction Functions { get; }

        /// <summary>Слоты экипировки, привязанные к этой части</summary>
        public EquipmentSlot[] EquipmentSlots { get; }

        /// <summary>
        /// Идентификатор родительской части (null для корневой — Torso).
        /// Используется для иерархии зависимостей.
        /// </summary>
        public string? ParentPartId { get; }

        /// <summary>Масштабируется ли HP от Vitality (true для органических, false для Amorphous)</summary>
        public bool VitalityScalesHP { get; }

        /// <summary>Жизненно важная часть (голова, сердце, торс) — потеря = смерть</summary>
        public bool IsVital { get; }

        /// <summary>
        /// Создать шаблон части тела.
        /// </summary>
        public BodyPartTemplate(
            string partId,
            BodyPartType partType,
            float baseFunctionalHP,
            float baseStructuralHP,
            BodyPartFunction functions,
            EquipmentSlot[]? equipmentSlots,
            string? parentPartId,
            bool isVital,
            bool vitalityScalesHP = true)
        {
            PartId = partId ?? throw new ArgumentNullException(nameof(partId));
            PartType = partType;
            BaseFunctionalHP = Math.Max(1f, baseFunctionalHP);
            BaseStructuralHP = Math.Max(0f, baseStructuralHP); // 0 для Heart
            Functions = functions;
            EquipmentSlots = equipmentSlots ?? Array.Empty<EquipmentSlot>();
            ParentPartId = parentPartId; // null для корневой
            IsVital = isVital;
            VitalityScalesHP = vitalityScalesHP;
        }

        public override string ToString() =>
            $"BodyPartTemplate({PartId}, {PartType}, FuncHP={BaseFunctionalHP}, StructHP={BaseStructuralHP}, Funcs={Functions})";
    }
}
