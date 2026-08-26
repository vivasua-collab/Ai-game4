#nullable enable
// NPCVisualService — STUB for Ai-game4 (Godot rendering handled in Adapter layer).
// Ai-game3 used Unity SpriteRenderer/GameObject for NPC visuals.
// Per task rules: skip Unity-specific files. This stub provides the API surface
// that NPCModule expects, but does nothing — actual rendering is handled by
// the Godot Adapter (to be implemented in a future task).
using System;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.NPC;

/// <summary>
/// Stub implementation of NPC visual service. Real Godot rendering
/// will be implemented in the Adapter layer.
/// </summary>
public sealed class NPCVisualService : IDisposable
{
    public void Initialize()
    {
        // No-op — Godot Adapter will handle NPC rendering.
    }

    public void UpdateVisualPositions()
    {
        // No-op.
    }

    public void Dispose()
    {
        // No-op.
    }
}
