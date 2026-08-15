#nullable enable
// Создано: 2026-05-20 19:11:00 UTC
// Редактировано: 2026-05-21 08:15:33 UTC — Волна 4: PerkType → Core.Data (устранение Core→Module зависимости)
// Фаза 4, задача 4.2 — интерфейс сервиса перков NPC.
// Перки = постоянные баффы (duration=float.MaxValue) через IBuffService.
using System.Collections.Generic;
using CultivationGame.Core.Data;

using CultivationGame.Core;
namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Интерфейс сервиса перков NPC.
    /// Управляет наложением/снятием перков и расчётом бонусов проводимости.
    /// Перки делегируют в BuffService как постоянные баффы.
    /// </summary>
    public interface IPerkService
    {
        /// <summary>Применить перк к сущности (постоянный бафф + обновление проводимости).</summary>
        bool ApplyPerk(string entityId, PerkType perkType);

        /// <summary>Снять перк с сущности (удаление баффа + пересчёт проводимости).</summary>
        bool RemovePerk(string entityId, PerkType perkType);

        /// <summary>Проверить наличие перка у сущности.</summary>
        bool HasPerk(string entityId, PerkType perkType);

        /// <summary>Получить суммарный бонус проводимости от всех перков сущности (0..N).</summary>
        float GetTotalConductivityBonus(string entityId);

        /// <summary>Получить все перки сущности.</summary>
        List<PerkType> GetAllPerksForEntity(string entityId);
    }
}
