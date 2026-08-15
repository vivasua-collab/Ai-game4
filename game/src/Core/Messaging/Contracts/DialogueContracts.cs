#nullable enable
namespace CultivationGame.Core.Messaging.Contracts;

// Created: 2026-05-08 (Ai-game3) — migrated 2026-08-15.
// Dialogue + interaction contracts: dialogue start/end/choice, interaction completed.

// === ДИАЛОГИ ===

/// <summary>
/// Начат диалог с NPC
/// </summary>
public readonly struct DialogueStartedEvent
{
    public readonly string NpcId;
    public readonly string DialogueId;
    public DialogueStartedEvent(string npcId, string dialogueId)
        { NpcId = npcId; DialogueId = dialogueId; }
}

/// <summary>
/// Диалог завершён
/// </summary>
public readonly struct DialogueEndedEvent
{
    public readonly string NpcId;
    public readonly string DialogueId;
    public DialogueEndedEvent(string npcId, string dialogueId)
        { NpcId = npcId; DialogueId = dialogueId; }
}

/// <summary>
/// Игрок выбрал вариант ответа в диалоге
/// </summary>
public readonly struct DialogueChoiceSelectedEvent
{
    public readonly string NpcId;
    public readonly string DialogueId;
    public readonly int ChoiceIndex;
    public DialogueChoiceSelectedEvent(string npcId, string dialogueId, int choiceIndex)
        { NpcId = npcId; DialogueId = dialogueId; ChoiceIndex = choiceIndex; }
}

// === ВЗАИМОДЕЙСТВИЕ ===

/// <summary>
/// Взаимодействие завершено (успешно).
/// Публикуется InteractionService при успешном TryInteract().
/// </summary>
public readonly struct InteractionCompletedEvent
{
    public readonly string TargetId;
    public readonly string InteractionType;
    public InteractionCompletedEvent(string targetId, string interactionType)
        { TargetId = targetId; InteractionType = interactionType; }
}
