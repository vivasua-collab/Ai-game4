#nullable enable
// Создано: 2026-05-20 19:11:00 UTC
// Редактировано: 2026-05-21 08:15:33 UTC — Волна 4: PerkType перенесён в Core/Data/Enums.cs
// Фаза 4, задача 4.2 — данные перков NPC (бонусы проводимости).
// Перки реализованы как постоянные баффы через BuffService.
using CultivationGame.Core;
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.NPC.Data
{
    /// <summary>
    /// Данные перка — статическая конфигурация бонуса.
    /// Содержит идентификацию, описание и числовое значение бонуса.
    /// PerkType определён в Core/Data/Enums.cs (Волна 4: устранение Core→Module зависимости).
    /// </summary>
    public sealed class PerkData
    {
        public PerkType Type;
        public string Id;               // Уникальный ID для BuffService (например "perk_golden_body")
        public string NameRu;           // Название на русском
        public string Description;      // Описание перка
        public float ConductivityBonus; // Бонус проводимости (0..1, доля от базовой)
    }
}
