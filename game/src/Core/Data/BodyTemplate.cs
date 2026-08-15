#nullable enable
// Создано: 2026-05-18 12:00:00 UTC — Body доработка: шаблон тела
// Редактировано: 2026-05-18 13:10:29 UTC — P2-01 FIX: GetChildPartIds возвращает IEnumerable через yield
// Композиция BodyPartTemplate по морфологии.
using System;
using System.Collections.Generic;
using System.Linq;

namespace CultivationGame.Core.Data
{
    /// <summary>
    /// Шаблон тела — композиция BodyPartTemplate по морфологии.
    /// Определяет набор частей тела, их иерархию, материал и размер по умолчанию.
    /// Используется BodyFactory для создания List&lt;BodyPart&gt;.
    /// Источник: BODY_SYSTEM.md §"Части тела"
    /// </summary>
    public sealed class BodyTemplate
    {
        /// <summary>Идентификатор шаблона ("humanoid", "quadruped", "bird" ...)</summary>
        public string TemplateId { get; }

        /// <summary>Морфология тела</summary>
        public Morphology Morphology { get; }

        /// <summary>Материал тела по умолчанию</summary>
        public BodyMaterial DefaultMaterial { get; }

        /// <summary>Размер по умолчанию</summary>
        public SizeClass DefaultSize { get; }

        /// <summary>Режим масштабирования HP от Vitality</summary>
        public VitalityScalingMode ScalingMode { get; }

        /// <summary>Шаблоны частей тела</summary>
        public IReadOnlyList<BodyPartTemplate> Parts { get; }

        /// <summary>Иерархия: partId → parentPartId (null для корневой)</summary>
        public IReadOnlyDictionary<string, string?> Hierarchy { get; }

        private readonly Dictionary<BodyPartType, BodyPartTemplate> _partsByType;

        /// <summary>
        /// Создать шаблон тела.
        /// </summary>
        public BodyTemplate(
            string templateId,
            Morphology morphology,
            BodyMaterial defaultMaterial,
            SizeClass defaultSize,
            VitalityScalingMode scalingMode,
            IReadOnlyList<BodyPartTemplate> parts,
            IReadOnlyDictionary<string, string?> hierarchy)
        {
            TemplateId = templateId ?? throw new ArgumentNullException(nameof(templateId));
            Morphology = morphology;
            DefaultMaterial = defaultMaterial;
            DefaultSize = defaultSize;
            ScalingMode = scalingMode;
            Parts = parts ?? throw new ArgumentNullException(nameof(parts));
            Hierarchy = hierarchy ?? throw new ArgumentNullException(nameof(hierarchy));

            // Индекс для быстрого поиска по типу
            _partsByType = parts.ToDictionary(p => p.PartType, p => p);
        }

        /// <summary>
        /// Получить шаблон части тела по типу.
        /// </summary>
        public BodyPartTemplate? GetPartTemplate(BodyPartType partType)
        {
            _partsByType.TryGetValue(partType, out var template);
            return template;
        }

        /// <summary>
        /// Получить ID дочерних частей для указанной родительской.
        /// P2-01 FIX: IEnumerable через yield — без GC-аллокации.
        /// </summary>
        public IEnumerable<string> GetChildPartIds(string parentPartId)
        {
            foreach (var kvp in Hierarchy)
            {
                if (kvp.Value == parentPartId)
                    yield return kvp.Key;
            }
        }

        public override string ToString() =>
            $"BodyTemplate({TemplateId}, {Morphology}, Parts={Parts.Count})";
    }
}
