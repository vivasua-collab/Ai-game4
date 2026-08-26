#nullable enable
// Создано: 2026-08-25 — NPC_COMBAT_PREP Phase 4: торговля с NPC (лавка торговца).
// TradeService публикует эти события; UI (TradeWindow) и GameWorldController
// (пауза/резюм тиков) подписываются на них. DialogueService публикует
// TradeRequestedEvent (выбор «Покажи товары»), TradeModule слушает его
// и открывает сессию торговли.
//
// Стиль контрактов: readonly struct + in-публикация (Zero-GC, EVT-01).

namespace CultivationGame.Core.Messaging.Contracts;

/// <summary>
/// Запрос на открытие торговли с торговцем. Публикуется DialogueService
/// при выборе «Покажи товары» (TargetNodeId == "open_trade"); TradeModule
/// подписан и вызывает ITradeService.OpenTrade.
/// </summary>
public readonly struct TradeRequestedEvent
{
    /// <summary>Идентификатор NPC-торговца (npcId).</summary>
    public readonly string NpcId;

    public TradeRequestedEvent(string npcId)
    {
        NpcId = npcId;
    }
}

/// <summary>
/// Сессия торговли начата (лавка открыта). Публикуется TradeService.OpenTrade
/// после генерации ассортимента. TradeWindow показывает окно,
/// GameWorldController ставит тики на паузу.
/// </summary>
public readonly struct TradeOpenedEvent
{
    /// <summary>Идентификатор NPC-торговца, с которым ведётся торговля.</summary>
    public readonly string NpcId;

    public TradeOpenedEvent(string npcId)
    {
        NpcId = npcId;
    }
}

/// <summary>
/// Сессия торговли завершена (лавка закрыта). Публикуется TradeService.CloseTrade.
/// TradeWindow скрывается, GameWorldController резюмирует тики —
/// единая авторитетная точка резюма (как DialogueEndedEvent для диалогов).
/// </summary>
public readonly struct TradeClosedEvent
{
    public TradeClosedEvent()
    {
    }
}

/// <summary>
/// Одна завершённая сделка покупки/продажи. Price — полная сумма сделки
/// (духовные камни). UI перечитывает ассортимент и баланс по этому событию.
/// </summary>
public readonly struct TradeCompletedEvent
{
    /// <summary>Идентификатор NPC-торговца.</summary>
    public readonly string NpcId;

    /// <summary>Идентификатор предмета сделки.</summary>
    public readonly string ItemId;

    /// <summary>Количество предметов в сделке.</summary>
    public readonly int Count;

    /// <summary>true — покупка (игрок платит), false — продажа (игрок получает).</summary>
    public readonly bool IsPurchase;

    /// <summary>Полная сумма сделки в духовных камнях (int, ЗАПРЕТ 3.9).</summary>
    public readonly int Price;

    public TradeCompletedEvent(string npcId, string itemId, int count, bool isPurchase, int price)
    {
        NpcId = npcId;
        ItemId = itemId;
        Count = count;
        IsPurchase = isPurchase;
        Price = price;
    }
}

/// <summary>
/// Сделка не состоялась (не хватает камней, инвентарь полон, нет товара и т.п.).
/// Reason — готовый русский текст для тоста/UI.
/// </summary>
public readonly struct TradeFailedEvent
{
    /// <summary>Причина отказа (русский текст для UI).</summary>
    public readonly string Reason;

    public TradeFailedEvent(string reason)
    {
        Reason = reason;
    }
}
