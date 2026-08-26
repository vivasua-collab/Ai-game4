#nullable enable
// Создано: 2026-05-09 05:15:31 UTC
// Конфигурация модуля баффов.
// BD-48 урок: class вместо struct (mutable struct risk).
using CultivationGame.Core;
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.Buff
{
    /// <summary>
    /// Конфигурация модуля баффов.
    /// </summary>
    public class BuffConfig
    {
        /// <summary>Интервал тиков по умолчанию (секунды)</summary>
        public float DefaultTickInterval = 1f;

        /// <summary>Максимальное количество баффов на одной сущности</summary>
        public int MaxBuffsPerEntity = 20;
    }
}
