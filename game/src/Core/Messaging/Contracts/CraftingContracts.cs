#nullable enable
namespace CultivationGame.Core.Messaging.Contracts;

// Created: 2026-05-08 (Ai-game3) — migrated 2026-08-15.
// Crafting contracts: completion, failure.
// P1-03 FIX: EntityId in CraftCompletedEvent (InventoryModule subscribes and adds result).

// === КРАФТ ===

/// <summary>
/// Крафт успешно завершён.
/// P1-03 FIX: InventoryModule подписывается и добавляет результат в инвентарь.
/// </summary>
public readonly struct CraftCompletedEvent
{
    public readonly string RecipeId;
    public readonly string ResultItemId;
    public readonly int Count;
    public readonly string EntityId;

    public CraftCompletedEvent(string recipeId, string resultItemId, int count, string entityId = "")
        { RecipeId = recipeId; ResultItemId = resultItemId; Count = count; EntityId = entityId; }
}

/// <summary>
/// Крафт не удался (недостаточно ресурсов или неверный рецепт)
/// </summary>
public readonly struct CraftFailedEvent
{
    public readonly string RecipeId;
    public readonly string Reason;
    public CraftFailedEvent(string recipeId, string reason)
        { RecipeId = recipeId; Reason = reason; }
}
