#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Interfaces;

/// <summary>
/// Sticky-flag input service. The Adapter pushes an <see cref="InputFrameData"/>
/// each frame; modules read sticky flags during their <c>Tick()</c> and the
/// PlayerModule calls <see cref="ResetFrameFlags"/> at the end of the tick.
/// </summary>
public interface IPlayerInputService
{
    InputFrameData CurrentFrame { get; }
    void UpdateFrame(InputFrameData frame);
    void ResetFrameFlags();

    bool IsInteractPressed { get; }
    bool IsInventoryPressed { get; }
    bool IsRestPressed { get; }
    bool IsHarvestPressed { get; }
    bool IsSpecialActionPressed { get; }
    bool IsPausePressed { get; }
    bool IsQuickSavePressed { get; }
    bool IsQuickLoadPressed { get; }
    bool IsJournalPressed { get; }
    bool IsTechniquesPressed { get; }
    bool IsCharacterSheetPressed { get; }
    bool IsQuestLogPressed { get; }
    bool IsMapPressed { get; }
    bool IsMinimapPressed { get; }
}
