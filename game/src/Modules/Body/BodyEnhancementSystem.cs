#nullable enable
// Создано: 2026-05-20 18:18 UTC
// ============================================================================
// BodyEnhancementSystem.cs — Система врождённых усилений тела по видам
// Cultivation World Simulator
// ============================================================================
//
// Источник: docs/NPC.md §5 Pipeline — BodyEnhancementSystem
//
// Генерирует список усилений (BodyEnhancement) на основе вида (SpeciesData)
// и уровня. Усиления НЕ модифицируют BodyPart напрямую — вместо этого
// NPCAssemblyService использует этот список для расчёта BaseDamage/BaseDefense.
//
// Логика усилений по видам (таблица §5):
//   NaturalArmor  → снижение урона на % (для NPCAssemblyService → BaseDefense)
//   NaturalWeapon → бонус урона           (для NPCAssemblyService → BaseDamage)
//   BodyHardening → +HP к части           (увеличивает MaxRedHP через SetMaxHP)
//   QiInfusion    → множитель урона/защиты (умножает BaseDamage/BaseDefense)
//   SizeGrowth    → увеличение размера     (+1 tier к SizeClass)
// Migrated from Ai-game3 (Unity) to Ai-game4 (Godot). SeededRandom lives in
// CultivationGame.Core.Data; extension methods NextFloat(min,max) added there.
// ============================================================================

using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.Body
{
    /// <summary>
    /// Тип врождённого усиления тела.
    /// Определяет, как именно усиление влияет на расчёт характеристик.
    /// Источник: Pipeline §5
    /// </summary>
    public enum EnhancementType
    {
        /// <summary>Снижение урона на % — вклад в BaseDefense</summary>
        NaturalArmor,

        /// <summary>Врождённое оружие — бонус урона, вклад в BaseDamage</summary>
        NaturalWeapon,

        /// <summary>Упрочнение тела — +HP к целевой части</summary>
        BodyHardening,

        /// <summary>Ци-насыщение — множитель урона/защиты для части</summary>
        QiInfusion,

        /// <summary>Увеличение размера (+1 tier к SizeClass)</summary>
        SizeGrowth
    }

    /// <summary>
    /// Описание одного врождённого усиления.
    /// Value — числовое значение усиления (абсолютное или процентное в зависимости от типа).
    /// TargetPart — BodyPartType.All означает «все части тела».
    /// </summary>
    public sealed class BodyEnhancement
    {
        /// <summary>Тип усиления</summary>
        public EnhancementType Type;

        /// <summary>Числовое значение (абсолютное для Weapon/Hardening, % для Armor/Infusion, tier для SizeGrowth)</summary>
        public float Value;

        /// <summary>Целевая часть тела (BodyPartType.All = все части)</summary>
        public BodyPartType TargetPart;

        /// <summary>Человекочитаемое описание усиления</summary>
        public string Description = string.Empty;

        public override string ToString()
            => $"BodyEnhancement({Type}, {TargetPart}, Value={Value}, \"{Description}\")";
    }

    /// <summary>
    /// Система генерации врождённых усилений тела по видам.
    /// Не зависит от других сервисов — все данные из SpeciesData + хардкод-таблица.
    /// DI: регистрируется как Singleton без зависимостей.
    /// </summary>
    public sealed class BodyEnhancementSystem
    {
        // === Внутренние структуры для таблицы усилений ===

        /// <summary>
        /// Диапазон уровней, при которых применяется набор усилений.
        /// MinLevel включительно, MaxLevel включительно.
        /// </summary>
        private readonly struct LevelRange
        {
            public readonly int MinLevel;
            public readonly int MaxLevel;

            public LevelRange(int min, int max)
            {
                MinLevel = min;
                MaxLevel = max;
            }

            /// <summary>Проверить, попадает ли уровень в диапазон</summary>
            public bool Contains(int level) => level >= MinLevel && level <= MaxLevel;
        }

        /// <summary>
        /// Шаблон усиления — до привязки к конкретному виду.
        /// Используется для построения таблицы усилений.
        /// </summary>
        private readonly struct EnhancementTemplate
        {
            public readonly EnhancementType Type;
            public readonly float Value;
            public readonly BodyPartType TargetPart;
            public readonly string Description;

            public EnhancementTemplate(EnhancementType type, float value,
                BodyPartType targetPart, string description)
            {
                Type = type;
                Value = value;
                TargetPart = targetPart;
                Description = description;
            }
        }

        /// <summary>
        /// Запись таблицы усилений для конкретного вида.
        /// Связывает SpeciesId + LevelRange со списком шаблонов.
        /// </summary>
        private readonly struct SpeciesEnhancementEntry
        {
            public readonly string SpeciesId;
            public readonly LevelRange Range;
            public readonly EnhancementTemplate[] Templates;

            public SpeciesEnhancementEntry(string speciesId, LevelRange range,
                EnhancementTemplate[] templates)
            {
                SpeciesId = speciesId;
                Range = range;
                Templates = templates;
            }
        }

        // === Таблица усилений по видам (Pipeline §5) ===
        // Строится один раз в конструкторе, доступна только для чтения.

        private readonly SpeciesEnhancementEntry[] _enhancementTable;

        /// <summary>
        /// Конструктор без зависимостей. Строит хардкод-таблицу усилений.
        /// </summary>
        public BodyEnhancementSystem()
        {
            _enhancementTable = BuildEnhancementTable();
        }

        // ====================================================================
        // ПУБЛИЧНЫЕ МЕТОДЫ
        // ====================================================================

        /// <summary>
        /// Сгенерировать список усилений для указанного вида и уровня.
        /// Усиления определяются по хардкод-таблице §5.
        /// SeededRandom используется для вариации значений (±10% от базового).
        /// Если вид не имеет усилений (human/elf), возвращает пустой список.
        /// </summary>
        /// <param name="species">Данные вида (SpeciesId обязателен)</param>
        /// <param name="level">Уровень сущности (0+)</param>
        /// <param name="rng">Детерминированный генератор для вариации</param>
        /// <returns>Список усилений (может быть пустым)</returns>
        public List<BodyEnhancement> GenerateEnhancements(
            SpeciesData species, int level, SeededRandom rng)
        {
            var result = new List<BodyEnhancement>();

            if (species == null)
                return result;

            string speciesId = species.SpeciesId;

            // Поиск всех подходящих записей в таблице
            for (int i = 0; i < _enhancementTable.Length; i++)
            {
                ref readonly var entry = ref _enhancementTable[i];

                // Проверка вида
                if (entry.SpeciesId != speciesId)
                    continue;

                // Проверка диапазона уровней
                if (!entry.Range.Contains(level))
                    continue;

                // Создание усилений из шаблонов с вариацией rng
                for (int j = 0; j < entry.Templates.Length; j++)
                {
                    ref readonly var template = ref entry.Templates[j];

                    // Вариация значения: ±10% от базового (кроме SizeGrowth — целое число)
                    float finalValue = template.Value;
                    if (template.Type != EnhancementType.SizeGrowth)
                    {
                        float variation = rng.NextFloat(-0.1f, 0.1f);
                        finalValue = template.Value * (1f + variation);
                    }

                    var enhancement = new BodyEnhancement
                    {
                        Type = template.Type,
                        Value = finalValue,
                        TargetPart = template.TargetPart,
                        Description = template.Description
                    };

                    result.Add(enhancement);
                }
            }

            return result;
        }

        /// <summary>
        /// Применить усиления к списку частей тела.
        /// Модифицирует BodyPart через SetMaxHP для BodyHardening.
        /// Остальные усиления сохраняются как метаданные —
        /// NPCAssemblyService использует их для расчёта BaseDamage/BaseDefense.
        ///
        /// Логика применения:
        /// - NaturalArmor  → метаданные (ArmorBonus += Value), расчёт в NPCAssemblyService
        /// - NaturalWeapon → метаданные (DamageBonus += Value), расчёт в NPCAssemblyService
        /// - BodyHardening → непосредственное увеличение MaxRedHP через SetMaxHP
        /// - QiInfusion    → метаданные (QiMultiplier *= Value), расчёт в NPCAssemblyService
        /// - SizeGrowth    → не применяется к частям, обрабатывается отдельно
        /// </summary>
        /// <param name="bodyParts">Список частей тела для модификации</param>
        /// <param name="enhancements">Список усилений для применения</param>
        public void ApplyEnhancements(
            List<BodyPart> bodyParts, List<BodyEnhancement> enhancements)
        {
            if (bodyParts == null || enhancements == null || enhancements.Count == 0)
                return;

            foreach (var enh in enhancements)
            {
                // BodyHardening — единственное усиление, модифицирующее BodyPart напрямую
                if (enh.Type == EnhancementType.BodyHardening)
                {
                    ApplyBodyHardening(bodyParts, enh);
                }
                // Остальные усиления — метаданные для NPCAssemblyService
                // NaturalArmor, NaturalWeapon, QiInfusion, SizeGrowth
                // рассчитываются при сборке NPC через GetEnhancementBonuses()
            }
        }

        /// <summary>
        /// Рассчитать суммарные бонусы усилений для combat-расчётов.
        /// Используется NPCAssemblyService для вычисления BaseDamage/BaseDefense.
        ///
        /// Возвращает структуру с агрегированными бонусами по типу усиления.
        /// </summary>
        /// <param name="enhancements">Список усилений</param>
        /// <param name="partType">Часть тела для фильтрации (BodyPartType.All = все)</param>
        /// <returns>Агрегированные бонусы</returns>
        public EnhancementBonuses GetEnhancementBonuses(
            List<BodyEnhancement> enhancements, BodyPartType partType)
        {
            var bonuses = new EnhancementBonuses();

            if (enhancements == null || enhancements.Count == 0)
                return bonuses;

            foreach (var enh in enhancements)
            {
                // Фильтр по целевой части: усилие применяется,
                // если TargetPart совпадает или TargetPart == All
                bool applies = enh.TargetPart == BodyPartType.All
                    || enh.TargetPart == partType;

                if (!applies)
                    continue;

                switch (enh.Type)
                {
                    case EnhancementType.NaturalArmor:
                        // Снижение урона на % (Value = процент, напр. 10 = 10%)
                        bonuses.ArmorBonusPercent += enh.Value;
                        break;

                    case EnhancementType.NaturalWeapon:
                        // Бонус урона (абсолютное значение)
                        bonuses.DamageBonus += enh.Value;
                        break;

                    case EnhancementType.QiInfusion:
                        // Множитель урона/защиты (Value = процент, напр. 50 = ×1.5)
                        bonuses.QiDamageMultiplier += enh.Value / 100f;
                        bonuses.QiDefenseMultiplier += enh.Value / 100f;
                        break;

                    case EnhancementType.BodyHardening:
                        // Бонус HP уже применён через SetMaxHP,
                        // но сохраняем для информационных целей
                        bonuses.HPBonus += (int)enh.Value;
                        break;

                    case EnhancementType.SizeGrowth:
                        // Количество тиров увеличения размера
                        bonuses.SizeTierBonus += (int)enh.Value;
                        break;
                }
            }

            return bonuses;
        }

        /// <summary>
        /// Рассчитать новый класс размера с учётом SizeGrowth усилений.
        /// </summary>
        /// <param name="baseSize">Базовый класс размера из SpeciesData</param>
        /// <param name="enhancements">Список усилений</param>
        /// <returns>Итоговый класс размера</returns>
        public SizeClass GetAdjustedSizeClass(
            SizeClass baseSize, List<BodyEnhancement> enhancements)
        {
            if (enhancements == null || enhancements.Count == 0)
                return baseSize;

            int tierBonus = 0;
            foreach (var enh in enhancements)
            {
                if (enh.Type == EnhancementType.SizeGrowth)
                    tierBonus += (int)enh.Value;
            }

            if (tierBonus == 0)
                return baseSize;

            // SizeClass — enum: Tiny=0, Small=1, Medium=2, Large=3, Huge=4, Gargantuan=5, Colossal=6
            int currentTier = (int)baseSize;
            int newTier = Math.Min(currentTier + tierBonus,
                (int)SizeClass.Colossal);

            return (SizeClass)newTier;
        }

        // ====================================================================
        // ПРИВАТНЫЕ МЕТОДЫ
        // ====================================================================

        /// <summary>
        /// Применить BodyHardening к целевым частям тела.
        /// Увеличивает MaxRedHP через SetMaxHP, сохраняя пропорцию урона.
        /// </summary>
        private void ApplyBodyHardening(List<BodyPart> bodyParts, BodyEnhancement enh)
        {
            int hpIncrease = (int)enh.Value;
            if (hpIncrease <= 0)
                return;

            foreach (var part in bodyParts)
            {
                // Фильтр по целевой части
                bool applies = enh.TargetPart == BodyPartType.All
                    || enh.TargetPart == part.Type;

                if (!applies)
                    continue;

                // Увеличиваем MaxRedHP, пересчитываем MaxBlackHP по множителю
                int newMaxRed = part.MaxRedHP + hpIncrease;
                int newMaxBlack = part.MaxBlackHP > 0
                    ? (int)(newMaxRed * GameConstants.STRUCTURAL_HP_MULTIPLIER)
                    : 0;

                part.SetMaxHP(newMaxRed, newMaxBlack);
            }
        }

        /// <summary>
        /// Построить хардкод-таблицу усилений по видам.
        /// Источник: Pipeline §5 — таблица усилений.
        ///
        /// Порядок записей не важен — поиск по SpeciesId + LevelRange.
        /// </summary>
        private SpeciesEnhancementEntry[] BuildEnhancementTable()
        {
            return new[]
            {
                // === Волк (L1-3) ===
                // NaturalWeapon(LeftArm+RightArm, +5), NaturalArmor(Torso, +10%)
                new SpeciesEnhancementEntry("wolf", new LevelRange(1, 3),
                    new[]
                    {
                        new EnhancementTemplate(EnhancementType.NaturalWeapon, 5f,
                            BodyPartType.LeftArm, "Волчьи когти (левая лапа)"),
                        new EnhancementTemplate(EnhancementType.NaturalWeapon, 5f,
                            BodyPartType.RightArm, "Волчьи когти (правая лапа)"),
                        new EnhancementTemplate(EnhancementType.NaturalArmor, 10f,
                            BodyPartType.Torso, "Волчья шкура (+10% защиты)"),
                    }),

                // === Тигр (L2-5) ===
                new SpeciesEnhancementEntry("tiger", new LevelRange(2, 5),
                    new[]
                    {
                        new EnhancementTemplate(EnhancementType.NaturalWeapon, 10f,
                            BodyPartType.LeftArm, "Тигриные когти (левая лапа)"),
                        new EnhancementTemplate(EnhancementType.NaturalWeapon, 10f,
                            BodyPartType.RightArm, "Тигриные когти (правая лапа)"),
                        new EnhancementTemplate(EnhancementType.NaturalArmor, 20f,
                            BodyPartType.Torso, "Тигриная шкура (+20% защиты)"),
                    }),

                // === Дракон (L5+) ===
                new SpeciesEnhancementEntry("dragon", new LevelRange(5, 99),
                    new[]
                    {
                        new EnhancementTemplate(EnhancementType.NaturalWeapon, 20f,
                            BodyPartType.LeftArm, "Драконьи когти (левая лапа)"),
                        new EnhancementTemplate(EnhancementType.NaturalWeapon, 20f,
                            BodyPartType.RightArm, "Драконьи когти (правая лапа)"),
                        new EnhancementTemplate(EnhancementType.NaturalArmor, 30f,
                            BodyPartType.All, "Драконья чешуя (+30% защиты)"),
                        new EnhancementTemplate(EnhancementType.QiInfusion, 50f,
                            BodyPartType.Torso, "Драконье Ци (+50% урон/защита)"),
                    }),

                // === Паук (L0-2) ===
                new SpeciesEnhancementEntry("spider", new LevelRange(0, 2),
                    new[]
                    {
                        new EnhancementTemplate(EnhancementType.NaturalWeapon, 3f,
                            BodyPartType.Head, "Паучьи жвала (+3 урона)"),
                        new EnhancementTemplate(EnhancementType.BodyHardening, 15f,
                            BodyPartType.Torso, "Хитиновый панцирь (+15 HP)"),
                    }),

                // === Призрак (L1+) ===
                new SpeciesEnhancementEntry("ghost", new LevelRange(1, 99),
                    new[]
                    {
                        new EnhancementTemplate(EnhancementType.QiInfusion, 100f,
                            BodyPartType.Torso, "Призрачное Ци (+100% урон/защита)"),
                        new EnhancementTemplate(EnhancementType.QiInfusion, 50f,
                            BodyPartType.Head, "Призрачный разум (+50% урон/защита)"),
                    }),

                // === Голем (L1-3) ===
                new SpeciesEnhancementEntry("golem", new LevelRange(1, 3),
                    new[]
                    {
                        new EnhancementTemplate(EnhancementType.BodyHardening, 50f,
                            BodyPartType.All, "Каменное тело (+50 HP все части)"),
                        new EnhancementTemplate(EnhancementType.NaturalArmor, 25f,
                            BodyPartType.All, "Каменная броня (+25% защиты)"),
                    }),

                // === Демон (L3+) ===
                new SpeciesEnhancementEntry("demon", new LevelRange(3, 99),
                    new[]
                    {
                        new EnhancementTemplate(EnhancementType.NaturalWeapon, 15f,
                            BodyPartType.LeftArm, "Демонические когти (левая рука)"),
                        new EnhancementTemplate(EnhancementType.NaturalWeapon, 15f,
                            BodyPartType.RightArm, "Демонические когти (правая рука)"),
                        new EnhancementTemplate(EnhancementType.QiInfusion, 20f,
                            BodyPartType.All, "Демоническое Ци (+20% урон/защита)"),
                    }),

                // === Великан (L1+) ===
                new SpeciesEnhancementEntry("giant", new LevelRange(1, 99),
                    new[]
                    {
                        new EnhancementTemplate(EnhancementType.BodyHardening, 30f,
                            BodyPartType.All, "Исполинское тело (+30 HP все части)"),
                        new EnhancementTemplate(EnhancementType.NaturalWeapon, 15f,
                            BodyPartType.LeftArm, "Великанская мощь (левая рука)"),
                        new EnhancementTemplate(EnhancementType.NaturalWeapon, 15f,
                            BodyPartType.RightArm, "Великанская мощь (правая рука)"),
                        new EnhancementTemplate(EnhancementType.SizeGrowth, 1f,
                            BodyPartType.All, "Исполинский рост (+1 tier размера)"),
                    }),

                // Примечание: human, elf — нет усилений (используют экипировку).
                // phoenix — нет записи в таблице §5, усилений не имеет.
            };
        }
    }

    /// <summary>
    /// Агрегированные бонусы усилений для combat-расчётов.
    /// Используется NPCAssemblyService для вычисления итоговых характеристик.
    ///
    /// NaturalArmor  → ArmorBonusPercent (суммарный % снижения урона)
    /// NaturalWeapon → DamageBonus (суммарный абсолютный бонус урона)
    /// BodyHardening → HPBonus (суммарный бонус HP, уже применён через SetMaxHP)
    /// QiInfusion    → QiDamageMultiplier / QiDefenseMultiplier (аддитивные множители)
    /// SizeGrowth    → SizeTierBonus (количество тиров увеличения)
    /// </summary>
    public struct EnhancementBonuses
    {
        /// <summary>Суммарный % снижения урона (NaturalArmor)</summary>
        public float ArmorBonusPercent;

        /// <summary>Суммарный абсолютный бонус урона (NaturalWeapon)</summary>
        public float DamageBonus;

        /// <summary>Суммарный бонус HP (BodyHardening, информационно)</summary>
        public int HPBonus;

        /// <summary>Аддитивный множитель Ци-урона (QiInfusion, 0.5 = +50%)</summary>
        public float QiDamageMultiplier;

        /// <summary>Аддитивный множитель Ци-защиты (QiInfusion, 0.5 = +50%)</summary>
        public float QiDefenseMultiplier;

        /// <summary>Количество тиров увеличения размера (SizeGrowth)</summary>
        public int SizeTierBonus;

        /// <summary>Проверка: есть ли хоть одно усиление</summary>
        public bool HasAny =>
            ArmorBonusPercent > 0f ||
            DamageBonus > 0f ||
            HPBonus > 0 ||
            QiDamageMultiplier > 0f ||
            QiDefenseMultiplier > 0f ||
            SizeTierBonus > 0;

        public override string ToString()
            => $"EnhancementBonuses(Armor={ArmorBonusPercent:F1}%, Dmg=+{DamageBonus:F1}, " +
               $"HP=+{HPBonus}, QiDmg=×{1f + QiDamageMultiplier:F2}, QiDef=×{1f + QiDefenseMultiplier:F2}, " +
               $"SizeTier=+{SizeTierBonus})";
    }
}
