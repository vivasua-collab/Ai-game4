#nullable enable
// Создано: 2026-08-22 — Phase C (BODY-IMPL-PLAN): рендер животных.
// AnimalSpriteRenderer — Godot Node2D that draws simple colored circles
// for each animal on the world map. Re-queries AnimalService every frame
// so wandering animals are drawn at their current tile position.
// Источник: checkpoints/08_22_body_impl_plan.md Phase C
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;
using CultivationGame.Modules.NPC;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Renders simple wandering animals (wolf/deer/rabbit) as colored circles
/// on the world map. Each species has a distinct colour; size is scaled by
/// <see cref="SizeClass"/>. Designed for ≤10 animals — performance is a
/// non-issue at this count.
///
/// ZIndex = RenderLayer.Objects (3) — same as environment objects / ground
/// items, just below the player (4) so the player visually stands over
/// animals when overlapping tiles.
/// </summary>
public partial class AnimalSpriteRenderer : Node2D
{
    [Inject] private AnimalService? _animalService = null!;

    // Cache species → colour to avoid switch per frame.
    private static readonly Dictionary<string, Color> SpeciesColours = new()
    {
        { "wolf",   new Color(0.32f, 0.32f, 0.36f) },  // dark grey
        { "deer",   new Color(0.55f, 0.38f, 0.22f) },  // brown
        { "rabbit", new Color(0.92f, 0.92f, 0.92f) },  // white
    };

    private static readonly Color OutlineColour = new(0.05f, 0.04f, 0.02f, 0.85f);
    private static readonly Color ShadowColour = new(0f, 0f, 0f, 0.30f);

    private int _tilePixels;
    // Re-allocated every frame — avoid GC by reusing a single list.
    private readonly List<AnimalEntity> _snapshot = new();

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
        {
            ContainerAdapter.InjectProperties(this, container);
        }
        _tilePixels = GameConstants.TILE_PIXELS;
        ZIndex = (int)RenderLayer.Objects;
        GD.Print($"[AnimalSpriteRenderer] Ready — tilePixels={_tilePixels}");
    }

    public override void _PhysicsProcess(double delta)
    {
        // Animals move once per game tick (1-15 Hz), but the renderer runs
        // every physics frame (60 Hz). QueueRedraw is cheap; Godot batches.
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_animalService == null) return;

        // Take a snapshot of current animal positions (avoid concurrent
        // mutation during iteration — Tick runs on the same thread but
        // defensive copying is cheap at this count).
        _snapshot.Clear();
        foreach (var a in _animalService.GetAllAnimals())
            _snapshot.Add(a);

        float halfTile = _tilePixels * 0.5f;

        foreach (var animal in _snapshot)
        {
            if (!animal.IsAlive) continue;

            float cx = animal.Position.X * _tilePixels + halfTile;
            float cy = animal.Position.Y * _tilePixels + halfTile;

            float radius = GetRadiusForSize(animal.Size);
            Color bodyColour = GetColourForSpecies(animal.Species);

            // Soft shadow (offset down-right).
            DrawCircle(new Vector2(cx + 2f, cy + 3f), radius * 0.95f, ShadowColour);

            // Body.
            DrawCircle(new Vector2(cx, cy), radius, bodyColour);

            // Outline (DrawArc — pointCount ≈ 2π × radius for smooth circle).
            int arcPoints = Mathf.Max(12, (int)(radius * 2f));
            DrawArc(
                new Vector2(cx, cy),
                radius,
                startAngle: 0f,
                endAngle: Mathf.Tau,
                pointCount: arcPoints,
                color: OutlineColour,
                width: 1.5f);

            // Small eye-dot to indicate facing direction (towards Target).
            if (animal.Target is { } target)
            {
                int dx = System.Math.Sign(target.X - animal.Position.X);
                int dy = System.Math.Sign(target.Y - animal.Position.Y);
                if (dx != 0 || dy != 0)
                {
                    float eyeX = cx + dx * radius * 0.45f;
                    float eyeY = cy + dy * radius * 0.45f;
                    DrawCircle(new Vector2(eyeX, eyeY), Mathf.Max(1.2f, radius * 0.18f), OutlineColour);
                }
            }
        }
    }

    private static float GetRadiusForSize(SizeClass size)
    {
        // Tile = 64 px; player sprite is ~24 px wide. Animals scale around that.
        return size switch
        {
            SizeClass.Tiny     => 4f,
            SizeClass.Small    => 7f,   // rabbit
            SizeClass.Medium   => 11f,  // wolf, deer
            SizeClass.Large    => 16f,
            SizeClass.Huge     => 22f,
            SizeClass.Gargantuan => 30f,
            SizeClass.Colossal => 40f,
            _ => 11f,
        };
    }

    private static Color GetColourForSpecies(string species)
    {
        return SpeciesColours.TryGetValue(species, out var c)
            ? c
            : new Color(0.5f, 0.5f, 0.5f);
    }
}
