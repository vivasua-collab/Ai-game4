#nullable enable
// Создано: 2026-05-20 18:18 UTC
// Фаза 2.4: Генератор имён NPC на основе Legacy NamingDatabase + NameBuilder
// Источник: docs/NPC.md §Имена, Legacy NamingDatabase, NameBuilder
using System;
using CultivationGame.Core;
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.NPC
{
    /// <summary>
    /// Генератор имён NPC. Детерминированный алгоритм на основе SeededRandom.
    /// Формирует имя по SoulType, SpeciesId, роли и уровню культивации.
    /// Источник: Legacy NamingDatabase + NameBuilder.
    /// </summary>
    public sealed class NPCNameGenerator
    {
        // === Таблицы имён: Люди (мужские/женские отдельно для 50/50) ===
        private static readonly string[] HumanMaleNames =
        {
            "Иван", "Пётр", "Дмитрий", "Алексей", "Сергей",
            "Михаил", "Андрей", "Николай"
        };

        private static readonly string[] HumanFemaleNames =
        {
            "Мария", "Анна", "Елена", "Ольга", "Наталья",
            "Татьяна", "Светлана", "Екатерина"
        };

        // === Таблицы имён: Эльфы ===
        private static readonly string[] ElfNames =
        {
            "Элариэль", "Сильвен", "Лориэн", "Аэлита", "Тарил",
            "Индиэль", "Фаэлон", "Иллирия", "Кэлиан", "Амариэль"
        };

        // === Таблицы имён: Демоны ===
        private static readonly string[] DemonNames =
        {
            "Азраил", "Лилит", "Вельзевул", "Мара", "Асмодей",
            "Найя", "Белиал", "Карнак", "Зера", "Моргош"
        };

        // === Титулы Старейшин ===
        private static readonly string[] ElderTitles =
            { "Старейшина", "Мудрец", "Наставник" };

        // === Титулы Культиваторов (по уровню) ===
        private static readonly string CultivatorTitleL1_3 = "Практик";
        private static readonly string CultivatorTitleL4_6 = "Мастер";
        private static readonly string CultivatorTitleL7_Plus = "Учитель";

        // === Названия видов (Creature) ===
        private static readonly string[] CreatureSpeciesIds =
            { "wolf", "tiger", "dragon", "spider", "giant" };

        private static readonly string[] CreatureSpeciesNamesRu =
            { "Волк", "Тигр", "Дракон", "Паук", "Великан" };

        // === Прилагательные для существ (по уровню) ===
        private static readonly string[] AdjL0_1 = { "Серый", "Малый", "Молодой" };
        private static readonly string[] AdjL2_3 = { "Крупный", "Свирепый", "Опытный" };
        private static readonly string[] AdjL4_5 = { "Древний", "Могучий", "Грозный" };
        private static readonly string[] AdjL6_Plus = { "Легендарный", "Первозданный", "Невероятный" };

        // === Специальные прилагательные для драконов ===
        private static readonly string[] DragonAdjectives =
            { "Огненный", "Ледяной", "Громовой", "Теневой" };

        // === Места для духов ===
        private static readonly string[] SpiritPlaces =
        {
            "Подземелья", "Забытого Храма", "Древних Руин",
            "Тёмного Леса", "Пустоши", "Болот", "Горных Пещер"
        };

        // === Материалы для конструктов (родительный падеж) ===
        private static readonly string[] ConstructMaterials =
            { "камня", "железа", "меди", "кристалла", "обсидиана", "духовного камня" };

        // Уровни материала: L0-1=камня, L2-3=железа, L4-5=меди, L6+=кристалла
        private static readonly string MaterialL0_1 = "камня";
        private static readonly string MaterialL2_3 = "железа";
        private static readonly string MaterialL4_5 = "меди";
        private static readonly string MaterialL6_Plus = "кристалла";

        /// <summary>
        /// Сгенерировать имя NPC на основе вида, роли, уровня культивации и RNG.
        /// </summary>
        public string Generate(SpeciesData species, NPCRole role, CultivationLevel level, SeededRandom rng)
        {
            if (species == null) throw new ArgumentNullException(nameof(species));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            int lvl = (int)level;

            return species.SoulType switch
            {
                SoulType.Character => GenerateCharacterName(species.SpeciesId, role, lvl, rng),
                SoulType.Creature  => GenerateCreatureName(species.SpeciesId, lvl, rng),
                SoulType.Spirit    => GenerateSpiritName(rng),
                SoulType.Construct => GenerateConstructName(lvl, rng),
                _ => $"Неизвестный {species.SpeciesId}"
            };
        }

        // ===================================================================
        // Character: выбор имени по SpeciesId + роль + уровень
        // ===================================================================

        private string GenerateCharacterName(string speciesId, NPCRole role, int lvl, SeededRandom rng)
        {
            // Выбор пола (50/50) → таблица имён
            string firstName = speciesId switch
            {
                "human"  => PickHumanName(rng),
                "elf"    => rng.NextElement(ElfNames),
                "demon"  => rng.NextElement(DemonNames),
                _        => rng.NextElement(HumanMaleNames) // Фоллбэк
            };

            // Старейшина: "{Title} {FirstName}"
            if (role == NPCRole.Elder)
                return $"{rng.NextElement(ElderTitles)} {firstName}";

            // Культиватор: "{CultivatorTitle} {FirstName}"
            if (role == NPCRole.Cultivator)
                return $"{GetCultivatorTitle(lvl)} {firstName}";

            // Остальные роли: просто "{FirstName}"
            return firstName;
        }

        /// <summary>
        /// Выбрать имя человека с учётом пола (50/50 male/female).
        /// </summary>
        private string PickHumanName(SeededRandom rng)
        {
            bool isMale = rng.NextBool(0.5f);
            return isMale
                ? rng.NextElement(HumanMaleNames)
                : rng.NextElement(HumanFemaleNames);
        }

        /// <summary>
        /// Получить титул культиватора по уровню.
        /// L1-3=Практик, L4-6=Мастер, L7+=Учитель.
        /// </summary>
        private static string GetCultivatorTitle(int lvl)
        {
            if (lvl >= 7) return CultivatorTitleL7_Plus;
            if (lvl >= 4) return CultivatorTitleL4_6;
            return CultivatorTitleL1_3;
        }

        // ===================================================================
        // Creature: "{Adjective} {SpeciesNameRu}"
        // ===================================================================

        private string GenerateCreatureName(string speciesId, int lvl, SeededRandom rng)
        {
            string speciesNameRu = GetSpeciesNameRu(speciesId);

            // Дракон — всегда специальные прилагательные
            if (speciesId == "dragon")
                return $"{rng.NextElement(DragonAdjectives)} {speciesNameRu}";

            string adjective = GetCreatureAdjective(lvl, rng);
            return $"{adjective} {speciesNameRu}";
        }

        /// <summary>
        /// Получить название вида на русском (для Creature).
        /// </summary>
        private static string GetSpeciesNameRu(string speciesId)
        {
            for (int i = 0; i < CreatureSpeciesIds.Length; i++)
            {
                if (CreatureSpeciesIds[i] == speciesId)
                    return CreatureSpeciesNamesRu[i];
            }
            // Фоллбэк: заглавная буква speciesId
            return char.ToUpper(speciesId[0]) + speciesId.Substring(1);
        }

        /// <summary>
        /// Получить прилагательное для существа по уровню культивации.
        /// </summary>
        private static string GetCreatureAdjective(int lvl, SeededRandom rng)
        {
            string[] pool = lvl switch
            {
                >= 6 => AdjL6_Plus,
                >= 4 => AdjL4_5,
                >= 2 => AdjL2_3,
                _    => AdjL0_1
            };
            return rng.NextElement(pool);
        }

        // ===================================================================
        // Spirit: "Призрак {Place}"
        // ===================================================================

        private string GenerateSpiritName(SeededRandom rng)
        {
            return $"Призрак {rng.NextElement(SpiritPlaces)}";
        }

        // ===================================================================
        // Construct: "Голем из {MaterialGenitive}"
        // Материал по уровню: L0-1=камня, L2-3=железа, L4-5=меди, L6+=кристалла
        // ===================================================================

        private string GenerateConstructName(int lvl, SeededRandom rng)
        {
            // Основной материал по уровню
            string material = lvl switch
            {
                >= 6 => MaterialL6_Plus,
                >= 4 => MaterialL4_5,
                >= 2 => MaterialL2_3,
                _    => MaterialL0_1
            };

            // Шанс (15%) получить редкий материал из полной таблицы вместо уровня
            if (rng.NextBool(0.15f))
                material = rng.NextElement(ConstructMaterials);

            return $"Голем из {material}";
        }
    }
}
