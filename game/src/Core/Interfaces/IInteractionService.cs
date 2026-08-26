#nullable enable
// Создано: 2026-05-08 10:07:00 UTC
// Редактировано: 2026-05-08 10:55:19 UTC — добавлен using UnityEngine для Vector2
// Редактировано: 2026-05-10 — Phase 17C: Vector2 → Position2D в сигнатурах методов
using CultivationGame.Core.Data;
using CultivationGame.Core;
namespace CultivationGame.Core.Interfaces
{
    public interface IInteractionService
    {
        string GetNearestInteractableId(Position2D position, float range);
        bool TryInteract(string targetId);
    }

    public interface IDialogueService
    {
        bool StartDialogue(string npcId, string dialogueId);
        void AdvanceDialogue();
        void SelectChoice(int choiceIndex);
        void EndDialogue();
        bool IsInDialogue { get; }
        string CurrentDialogueId { get; }
    }
}
