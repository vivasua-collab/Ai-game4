#nullable enable
// Создано: 2026-08-22 — Phase C (BODY-IMPL-PLAN): рендер животных.
// AnimalSpriteRenderer — Godot Node2D that draws simple colored circles
// for each animal on the world map. Re-queries AnimalService every frame
// so wandering animals are drawn at their current tile position.
// Источник: checkpoints/08_22_body_impl_plan.md Phase C
// Редактировано: 2026-09-19 R18-1 — HP-бар над животным (как у NPC с
// 2026-08-25 Phase 7): если повреждён ИЛИ hostile (месть), под глобальным
// тумблером GameSettings.ShowEnemyVitals (высокая сложность — без
// индикации противника; HP зверей — IBodyDataProvider per-entity).
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Adapter.Di;
using CultivationGame.Adapter.Persistence;
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
    // R18-1: HP зверей — тот же per-entity провайдер, что у NPC (Quadruped
    // тела регистрирует AnimalService в IBodyDataProvider).
    [Inject] private IBodyDataProvider? _bodyProvider;
    // R27 (2026-09-21): выбранная игроком цель (Tab) — рамка-подсветка.
    [Inject] private ISubscriber<PlayerTargetChangedEvent>? _targetChangedSub;
    private System.IDisposable? _targetChangedToken;
    private string _selectedTargetId = string.Empty;

    // Cache species → colour to avoid switch per frame.
    private static readonly Dictionary<string, Color> SpeciesColours = new()
    {
        { "wolf",   new Color(0.32f, 0.32f, 0.36f) },  // dark grey
        { "deer",   new Color(0.55f, 0.38f, 0.22f) },  // brown
        { "rabbit", new Color(0.92f, 0.92f, 0.92f) },  // white
    };

    private static readonly Color OutlineColour = new(0.05f, 0.04f, 0.02f, 0.85f);
    private static readonly Color ShadowColour = new(0f, 0f, 0f, 0.30f);
    // R27: рамка выбранной цели — янтарная (паттерн NPCSpriteRenderer).
    private static readonly Color SelectedTargetColour = new(0.98f, 0.76f, 0.19f, 0.95f);

    private int _tilePixels;
    // Re-allocated every frame — avoid GC by reusing a single list.
    private readonly List<AnimalEntity> _snapshot = new();

    public override void _Ready()
    {
        // R18-1: глобальный тумблер индикации врагов (persists в user://).
        GameSettings.EnsureLoaded();
        var container = Scene.GameBoot.Container;
        if (container != null)
        {
            ContainerAdapter.InjectProperties(this, container);
        }
        _tilePixels = GameConstants.TILE_PIXELS;
        ZIndex = (int)RenderLayer.Objects;

        // R27: выбор цели игрока (Tab) → рамка-подсветка выбранного зверя.
        _targetChangedToken = _targetChangedSub?.Subscribe((in PlayerTargetChangedEvent e) =>
        {
            _selectedTargetId = e.TargetId;
            QueueRedraw();
        });

        GD.Print($"[AnimalSpriteRenderer] Ready — tilePixels={_tilePixels}");
    }

    public override void _ExitTree()
    {
        _targetChangedToken?.Dispose();
        _targetChangedToken = null;
    }

    public override void _PhysicsProcess(double delta)
    {
        // Animals move once per game tick (1-15 Hz), but the renderer runs
        // every physics frame (60 Hz). QueueRedraw is cheap; Godot batches.
        QueueRedraw();
    }

    // Cached sprites per species.
    private readonly Dictionary<string, Texture2D> _spriteCache = new();

    public override void _Draw()
    {
        if (_animalService == null) return;

        _snapshot.Clear();
        foreach (var a in _animalService.GetAllAnimals())
            _snapshot.Add(a);

        float halfTile = _tilePixels * 0.5f;

        foreach (var animal in _snapshot)
        {
            if (!animal.IsAlive) continue;

            float cx = animal.Position.X * _tilePixels + halfTile;
            float cy = animal.Position.Y * _tilePixels + halfTile;

            // Get or create sprite for this species.
            if (!_spriteCache.TryGetValue(animal.Species, out var tex))
            {
                tex = ProceduralSpriteGenerator.CreateAnimalSprite(animal.Species, animal.Size);
                _spriteCache[animal.Species] = tex;
            }

            // Draw sprite centered on tile.
            float spriteSize = tex.GetWidth();
            var pos = new Vector2(cx - spriteSize / 2f, cy - spriteSize / 2f);
            DrawTexture(tex, pos);

            // R27: рамка-подсветка ВЫБРАННОЙ цели (паттерн NPCSpriteRenderer).
            if (!string.IsNullOrEmpty(_selectedTargetId) && animal.EntityId == _selectedTargetId)
            {
                DrawRect(new Rect2(pos.X - 3f, pos.Y - 3f, spriteSize + 6f, spriteSize + 6f),
                    SelectedTargetColour, false, 2.5f);
            }

            // R18-1: HP-бар над зверем — повреждён ИЛИ hostile (месть),
            // под глобальным тумблером ShowEnemyVitals. Паттерн NPC-бара
            // (Phase 7): 48×5, зелёный/жёлтый/красный по ratio.
            if (GameSettings.ShowEnemyVitals && _bodyProvider != null)
            {
                int hp = _bodyProvider.GetCurrentHealth(animal.EntityId);
                int maxHp = _bodyProvider.GetMaxHealth(animal.EntityId);
                bool hostile = _animalService.IsHostile(animal.EntityId);
                if (maxHp > 0 && (hp < maxHp || hostile))
                {
                    float barTop = cy - spriteSize / 2f - 8f;
                    DrawAnimalHealthBar(cx, barTop, hp, maxHp);
                }
            }
        }
    }

    /// <summary>
    /// R18-1: HP-бар над животным — тот же стиль, что у NPC (Phase 7):
    /// тёмная подложка + заливка по ratio + тонкая рамка. 48×5 px.
    /// </summary>
    private void DrawAnimalHealthBar(float cx, float top, int hp, int maxHp)
    {
        const float barWidth = 48f;
        const float barHeight = 5f;
        float ratio = maxHp > 0 ? (float)hp / maxHp : 0f;

        // Подложка.
        DrawRect(new Rect2(cx - barWidth / 2f, top, barWidth, barHeight),
            new Color(0.05f, 0.04f, 0.02f, 0.8f));

        // Заливка.
        var fillColour = ratio > 0.5f
            ? new Color(0.30f, 0.75f, 0.30f)
            : ratio > 0.25f
                ? new Color(0.85f, 0.75f, 0.25f)
                : new Color(0.85f, 0.25f, 0.20f);
        float fillWidth = barWidth * ratio;
        if (fillWidth > 0.5f)
            DrawRect(new Rect2(cx - barWidth / 2f, top, fillWidth, barHeight), fillColour);

        // Тонкая рамка.
        DrawRect(new Rect2(cx - barWidth / 2f, top, barWidth, barHeight),
            new Color(0f, 0f, 0f, 0.5f), false, 1f);
    }

    /// <summary>
    /// R18-1 QA (headless — БЕЗ отрисовки): будет ли нарисован HP-бар для
    /// животного в текущем состоянии (тумблер + damaged-OR-hostile).
    /// Верифицируется в ANIMALQA (шаг 8).
    /// </summary>
    public bool WouldDrawAnimalHealthBar(string animalEntityId)
    {
        if (_animalService == null || _bodyProvider == null) return false;
        var animal = _animalService.TryGetAnimal(animalEntityId);
        if (animal == null || !animal.Value.IsAlive) return false;
        int maxHp = _bodyProvider.GetMaxHealth(animalEntityId);
        if (maxHp <= 0) return false;
        return GameSettings.ShowEnemyVitals
            && (_bodyProvider.GetCurrentHealth(animalEntityId) < maxHp
                || _animalService.IsHostile(animalEntityId));
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
