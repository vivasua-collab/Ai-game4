#nullable enable
// Создано: 2026-05-20 18:43:21 UTC
// Фаза 3: взвешенный маппинг NPCRole → SpeciesId (задача 3.6)
// Источник: docs/NPC_ASSEMBLY_PIPELINE.md §2

using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.NPC
{
    /// <summary>
    /// Селектор вида NPC на основе роли.
    /// Взвешенный маппинг: NPCRole → (speciesId, weight).
    /// Источник: NPC_ASSEMBLY_PIPELINE.md §2.
    /// </summary>
    public sealed class NPCSpeciesSelector
    {
        /// <summary>Таблица весов: роль → список (speciesId, вес)</summary>
        private static readonly Dictionary<NPCRole, (string species, float weight)[]> RoleSpeciesTable = new()
        {
            { NPCRole.Monster, new[] { ("wolf", 30f), ("tiger", 25f), ("spider", 25f), ("dragon", 10f), ("phoenix", 10f) } },
            { NPCRole.Guard, new[] { ("human", 80f), ("elf", 20f) } },
            { NPCRole.Merchant, new[] { ("human", 60f), ("elf", 40f) } },
            { NPCRole.Cultivator, new[] { ("human", 50f), ("elf", 30f), ("demon", 20f) } },
            { NPCRole.Elder, new[] { ("human", 60f), ("elf", 40f) } },
            { NPCRole.Disciple, new[] { ("human", 70f), ("demon", 30f) } },
            { NPCRole.Enemy, new[] { ("human", 50f), ("demon", 50f) } },
            { NPCRole.Passerby, new[] { ("human", 90f), ("elf", 10f) } },
        };

        /// <summary>
        /// Выбрать вид NPC на основе роли и seed.
        /// Детерминирован: одинаковый seed → одинаковый результат.
        /// </summary>
        public string SelectSpecies(NPCRole role, SeededRandom rng)
        {
            if (!RoleSpeciesTable.TryGetValue(role, out var entries))
            {
                // По умолчанию: human
                return "human";
            }

            float[] weights = new float[entries.Length];
            for (int i = 0; i < entries.Length; i++)
                weights[i] = entries[i].weight;

            int index = rng.NextWeighted(weights);
            return entries[index].species;
        }
    }
}
