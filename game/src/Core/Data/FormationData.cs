#nullable enable
// Создано: 2026-05-09
// Данные определений формаций.
// Описывает конкретные формации: тип, размер, уровень, эффекты.
//
// 2026-08-26 (аудит-1 A-2): перенесён из Modules/Formation/Data в Core/Data —
// чистый DTO (зависимости только Core), на который ссылается Core-интерфейс
// IVerificationService. Нарушение «Core не зависит от Modules» устранено
// (прецедент: NPCState перенесён аудитом 08-21). FormationRegistry остался
// в Modules/Formation/Data (это сервисная инфраструктура модуля).
using System.Collections.Generic;

namespace CultivationGame.Core.Data
{
    /// <summary>
    /// Определение формации — описывает одну конкретную формацию.
    /// В будущих фазах может загружаться из ScriptableObject.
    /// Этап 4 внедрения ЦИ (2026-08-23): +Shape (визуальный контур),
    /// +EffectRadiusMeters (радиус действия по размеру, FORMATION_SYSTEM §4).
    /// </summary>
    public class FormationData
    {
        /// <summary>Уникальный идентификатор формации</summary>
        public string Id;

        /// <summary>Отображаемое название</summary>
        public string DisplayName;

        /// <summary>Тип формации</summary>
        public FormationType FormationType;

        /// <summary>Размер формации</summary>
        public FormationSize Size;

        /// <summary>Геометрическая форма контура (этап 4)</summary>
        public FormationShape Shape = FormationShape.Circle;

        /// <summary>Требуемый уровень формации (1-10)</summary>
        public int RequiredLevel;

        /// <summary>Стихия формации (Neutral = универсальная)</summary>
        public Element Element = Element.Neutral;

        /// <summary>Радиус действия в метрах (по размеру: 50/200/600/1000/5000)</summary>
        public int EffectRadiusMeters = 50;

        /// <summary>Тип ядра (для формаций с физическим ядром)</summary>
        public FormationCoreType CoreType = FormationCoreType.Disk;

        /// <summary>Многоразовая ли формация (с физическим ядром)</summary>
        public bool IsReusable;

        /// <summary>Эффекты формации</summary>
        public List<FormationEffectEntry> Effects = new List<FormationEffectEntry>();

        /// <summary>
        /// Создать данные формации "Базовый барьер" (L1, Small)
        /// </summary>
        public static FormationData CreateBasicBarrier()
        {
            return new FormationData
            {
                Id = "basic_barrier",
                DisplayName = "Базовый щит",
                FormationType = FormationType.Barrier,
                Size = FormationSize.Small,
                RequiredLevel = 1,
                Element = Element.Neutral,
                IsReusable = false,
                Effects = new List<FormationEffectEntry>
                {
                    new FormationEffectEntry
                    {
                        EffectType = FormationEffectType.Shield,
                        TargetStat = StatType.Defense,
                        Value = 0.3f,
                        TargetTag = "ally"
                    }
                }
            };
        }

        /// <summary>
        /// Создать данные формации "Меч Дао" (L3, Medium)
        /// </summary>
        public static FormationData CreateDaoBlade()
        {
            return new FormationData
            {
                Id = "dao_blade",
                DisplayName = "Меч Дао",
                FormationType = FormationType.Amplification,
                Size = FormationSize.Medium,
                RequiredLevel = 3,
                Element = Element.Fire,
                IsReusable = false,
                Effects = new List<FormationEffectEntry>
                {
                    new FormationEffectEntry
                    {
                        EffectType = FormationEffectType.Buff,
                        TargetStat = StatType.Damage,
                        Value = 0.3f,
                        TargetTag = "ally"
                    }
                }
            };
        }

        /// <summary>
        /// Создать данные формации "Теневые оковы" (L4, Medium)
        /// </summary>
        public static FormationData CreateShadowBindings()
        {
            return new FormationData
            {
                Id = "shadow_bindings",
                DisplayName = "Теневые оковы",
                FormationType = FormationType.Suppression,
                Size = FormationSize.Medium,
                RequiredLevel = 4,
                Element = Element.Void,
                IsReusable = false,
                Effects = new List<FormationEffectEntry>
                {
                    new FormationEffectEntry
                    {
                        EffectType = FormationEffectType.Debuff,
                        TargetStat = StatType.Speed,
                        Value = 0.5f,
                        TargetTag = "enemy"
                    }
                }
            };
        }
    }

    /// <summary>
    /// Запись эффекта формации.
    /// Определяет, как формация воздействует на сущности.
    /// </summary>
    public class FormationEffectEntry
    {
        /// <summary>Тип эффекта</summary>
        public FormationEffectType EffectType;

        /// <summary>Целевая характеристика</summary>
        public StatType TargetStat;

        /// <summary>Значение эффекта (модификатор: 0.3 = +30%)</summary>
        public float Value;

        /// <summary>Тип контроля (только для EffectType.Control)</summary>
        public ControlType ControlType = ControlType.None;

        /// <summary>Цель: "ally" или "enemy"</summary>
        public string TargetTag = "ally";
    }
}
