#nullable enable
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Сервис трупов NPC (R13 FULL-LOOT).
    /// Хранит контейнеры лута мёртвых NPC: снапшот экипировки + инвентаря +
    /// духовных камней на момент смерти. Обыск — по SlotId (адресно) или
    /// «забрать всё». Выдача предметов игроку — через ItemAddRequestEvent
    /// (EVT-02: command-событие, InventoryModule обрабатывает внутренне,
    /// включая overflow → земля).
    ///
    /// События: CorpseCreatedEvent / CorpseRemovedEvent / CorpseLootedEvent.
    /// </summary>
    public interface ICorpseService
    {
        /// <summary>Число активных трупов (QA/диагностика).</summary>
        int CorpseCount { get; }

        /// <summary>Все трупы (для рендерера/QA).</summary>
        IReadOnlyList<CorpseData> GetAllCorpses();

        /// <summary>Труп по ID (null — не найден).</summary>
        CorpseData? GetCorpse(string corpseId);

        /// <summary>
        /// Ближайший труп к позиции в радиусе (тайлы, Чебышёв).
        /// null — трупов в радиусе нет.
        /// </summary>
        CorpseData? FindNearestCorpse(Position2D position, float rangeTiles);

        /// <summary>
        /// Обыск по адресу: забрать ОДНУ запись (предмет×стак) из трупа.
        /// Публикует ItemAddRequestEvent(itemId, count, "loot") и
        /// CorpseLootedEvent. Опустевший труп убирается ("looted").
        /// </summary>
        /// <returns>true — предмет снят с трупа и отправлен в инвентарь.</returns>
        bool TryTakeItem(string corpseId, Guid slotId);

        /// <summary>
        /// FULL LOOT: забрать ВСЁ из трупа (каждая запись → отдельный
        /// ItemAddRequestEvent). Опустевший труп убирается ("looted").
        /// </summary>
        /// <returns>Число забранных записей (позиций).</returns>
        int LootAll(string corpseId);

        /// <summary>
        /// Убрать труп вручную (причина "manual"). Публикует CorpseRemovedEvent.
        /// </summary>
        void RemoveCorpse(string corpseId);

        /// <summary>
        /// Убрать трупы старше maxAgeGameSeconds игровой смерти
        /// (TTL-очистка, причина "expired"). Публикует CorpseRemovedEvent.
        /// </summary>
        /// <returns>Число убранных трупов.</returns>
        int RemoveOldCorpses(float maxAgeGameSeconds);
    }
}
