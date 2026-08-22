#nullable enable
// Создано: 2026-08-22 — NPC_COMBAT_PREP Phase 1: рендер NPC.
// NPCSpriteRenderer — Godot Node2D that draws colored circles for each
// spawned human NPC. Re-queries NPCService every frame so NPCs are drawn
// at their current position (NPCMovementService updates positions per tick).
// Источник: docs/docs_v2/09_workflow/NPC_COMBAT_PREP.md §Phase 1
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Renders spawned NPCs as colored circles on the world map. Color encodes
/// the NPC role (merchant=cold, cultivator=violet, guard=blue, passerby=grey).
/// ZIndex = RenderLayer.Objects (3) — same as animals, below the player (4).
/// </summary>
public partial class NPCSpriteRenderer : Node2D
{
    [Inject] private INPCService? _npcService;

    private int _tilePixels;
    private readonly List<string> _idSnapshot = new();

    private static readonly Color OutlineColour = new(0.05f, 0.04f, 0.02f, 0.85f);
    private static readonly Color ShadowColour = new(0f, 0f, 0f, 0.30f);

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
        {
            ContainerAdapter.InjectProperties(this, container);
        }
        _tilePixels = GameConstants.TILE_PIXELS;
        ZIndex = (int)RenderLayer.Objects;
        GD.Print("[NPCSpriteRenderer] Ready");
    }

    public override void _PhysicsProcess(double delta)
    {
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_npcService == null) return;

        _idSnapshot.Clear();
        foreach (var id in _npcService.GetAllNPCIds())
            _idSnapshot.Add(id);

        float halfTile = _tilePixels * 0.5f;
        const float radius = 12f; // human — Medium humanoid, slightly larger than wolves

        foreach (var id in _idSnapshot)
        {
            var npc = _npcService.GetNPC(id);
            if (npc == null || !_npcService.IsAlive(id)) continue;

            float cx = npc.Position.X * _tilePixels + halfTile;
            float cy = npc.Position.Y * _tilePixels + halfTile;

            Color bodyColour = GetColourForRole(npc.Role);

            DrawCircle(new Vector2(cx + 2f, cy + 3f), radius * 0.95f, ShadowColour);
            DrawCircle(new Vector2(cx, cy), radius, bodyColour);
            DrawArc(new Vector2(cx, cy), radius, 0f, Mathf.Tau,
                Mathf.Max(12, (int)(radius * 2f)), OutlineColour, 1.5f);
        }
    }

    private static Color GetColourForRole(NPCRole role) => role switch
    {
        NPCRole.Merchant   => new Color(0.20f, 0.55f, 0.60f), // teal
        NPCRole.Cultivator => new Color(0.55f, 0.30f, 0.70f), // violet
        NPCRole.Guard      => new Color(0.25f, 0.40f, 0.75f), // blue
        NPCRole.Elder      => new Color(0.75f, 0.60f, 0.25f), // gold
        NPCRole.Monster    => new Color(0.60f, 0.20f, 0.20f), // red
        NPCRole.Enemy      => new Color(0.70f, 0.35f, 0.15f), // orange
        _                  => new Color(0.62f, 0.62f, 0.58f), // passerby grey
    };
}
