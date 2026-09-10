#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Messaging.Contracts;

// Создано: 2026-09-10 — R13 FULL-LOOT: события жизненного цикла трупа.
// CorpseService (NPC-модуль) публикует, CorpseSpriteRenderer / EventLogWindow /
// LootWindow (Adapter) подписываются. Паттерн — GroundItemContracts.

/// <summary>
/// Событие: появился труп NPC (контейнер лута).
/// CorpseSpriteRenderer рисует маркер, EventLog пишет «☠ Имя — можно обыскать».
/// </summary>
public readonly struct CorpseCreatedEvent
{
    public readonly string CorpseId;
    public readonly string NpcId;
    public readonly string DisplayName;
    public readonly int X;             // тайл
    public readonly int Y;
    public readonly int ItemCount;

    public CorpseCreatedEvent(string corpseId, string npcId, string displayName, int x, int y, int itemCount)
        { CorpseId = corpseId; NpcId = npcId; DisplayName = displayName; X = x; Y = y; ItemCount = itemCount; }
}

/// <summary>
/// Событие: труп исчез (опустошён и убран, либо истёк TTL).
/// CorpseSpriteRenderer удаляет маркер.
/// </summary>
public readonly struct CorpseRemovedEvent
{
    public readonly string CorpseId;
    /// <summary>Причина: "looted" (опустошён) | "expired" (TTL) | "manual".</summary>
    public readonly string Reason;

    public CorpseRemovedEvent(string corpseId, string reason)
        { CorpseId = corpseId; Reason = reason; }
}

/// <summary>
/// Событие: из трупа забрали предметы (полностью или частично).
/// LootWindow обновляет список, EventLog пишет «Обыскано: Имя».
/// </summary>
public readonly struct CorpseLootedEvent
{
    public readonly string CorpseId;
    /// <summary>Число забранных записей (позиций, не штук).</summary>
    public readonly int TakenEntries;

    public CorpseLootedEvent(string corpseId, int takenEntries)
        { CorpseId = corpseId; TakenEntries = takenEntries; }
}
