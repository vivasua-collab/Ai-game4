#nullable enable
// Создано: 2026-05-20 18:43:21 UTC
// Фаза 3: структура меридиан (задача 3.A)
// Источник: docs/NPC_ASSEMBLY_PIPELINE.md §3
//
// Меридианы привязаны к ДУШЕ, не к телу.
// Spirit имеют меридианы без тела. Construct — фиксированные меридианы.

using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.NPC.Data
{
    /// <summary>
    /// Структура меридиан сущности.
    /// Ядро души (core) → корень меридиан → основной ствол → ветви → узлы вывода.
    /// Привязка к ДУШЕ, не к телу (Pipeline §3).
    /// Пока: формальная структура данных, без визуализации.
    /// </summary>
    public sealed class MeridianStructure
    {
        /// <summary>Базовая проводимость = coreCapacity / 360</summary>
        public float BaseConductivity;

        /// <summary>Количество ветвей (по уровню культивации)</summary>
        public int BranchCount;

        /// <summary>Узлы вывода Ци (для техник)</summary>
        public List<MeridianNode> OutputNodes = new();

        /// <summary>Множитель роста проводимости с возрастом (только ↑)</summary>
        public float GrowthMultiplier = 1.0f;

        /// <summary>
        /// Создать структуру меридиан на основе уровня культивации и coreCapacity.
        /// </summary>
        public static MeridianStructure Create(int cultivationLevel, long coreCapacity, int age, float levelGrowthFactor)
        {
            var structure = new MeridianStructure();

            // Базовая проводимость = coreCapacity / 360
            structure.BaseConductivity = coreCapacity / 360f;

            // Количество ветвей по уровню: L0=0, L1=1, L2=2, L3=4, L4=8, L5=16, L6=32, L7+=64
            structure.BranchCount = cultivationLevel switch
            {
                0 => 0,
                1 => 1,
                2 => 2,
                3 => 4,
                4 => 8,
                5 => 16,
                6 => 32,
                _ => 64 // L7+
            };

            // Множитель роста с возрастом (расширенная формула — ПРОТИВОРЕЧИЕ #4)
            float effectiveAge = age * levelGrowthFactor;
            structure.GrowthMultiplier = 1.0f + 0.001f * effectiveAge;

            // Узлы вывода: по количеству ветвей
            for (int i = 0; i < structure.BranchCount; i++)
            {
                structure.OutputNodes.Add(new MeridianNode
                {
                    Id = $"node_{i}",
                    OutputEfficiency = 1.0f
                });
            }

            return structure;
        }
    }

    /// <summary>
    /// Узел вывода Ци в меридианах.
    /// Привязка к части тела может быть null для Spirit.
    /// </summary>
    public sealed class MeridianNode
    {
        /// <summary>Идентификатор узла</summary>
        public string Id;

        /// <summary>Привязка к части тела (может быть BodyPartType.All для Spirit)</summary>
        public BodyPartType LinkedBodyPart = BodyPartType.All;

        /// <summary>Эффективность вывода Ци (для техник, 0.0-2.0)</summary>
        public float OutputEfficiency = 1.0f;
    }
}
