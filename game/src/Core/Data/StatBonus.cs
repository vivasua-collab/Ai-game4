#nullable enable
// Создано: 2026-05-08 10:07:00 UTC
// Редактировано: 2026-05-08 11:35:38 UTC — W5: camelCase→PascalCase для согласованности
using System;

namespace CultivationGame.Core.Data
{
    /// <summary>
    /// Единый класс бонуса к характеристике.
    /// Используется в ItemData, EquipmentData и генераторах.
    /// </summary>
    [Serializable]
    public class StatBonus
    {
        /// <summary>Название характеристики</summary>
        public string StatName = string.Empty;

        /// <summary>Значение бонуса (абсолютное или процентное)</summary>
        public float Value;

        /// <summary>Является ли бонус процентным (true = +X%, false = +X)</summary>
        public bool IsPercentage;
    }
}
