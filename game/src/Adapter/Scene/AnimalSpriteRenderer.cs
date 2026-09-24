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
        // G0 (I-7): общий таймер анимационных фаз зверей.
        _animClock += (float)delta;

        // Animals move once per game tick (1-15 Hz), but the renderer runs
        // every physics frame (60 Hz). QueueRedraw is cheap; Godot batches.
        QueueRedraw();
    }

    // Cached sprites per species.
    private readonly Dictionary<string, Texture2D> _spriteCache = new();

    // === G0 (I-7, 2026-09-25): анимация зверей (sprite-swap в _Draw) =====
    // Листы animal_{species}_{anim} (idle/walk; run/attack/death — реестр
    // готов, триггеры — фазы G3+). Нет PNG → процедурный кэш по виду (как
    // сейчас). Кадры — DrawTextureRegion; движение — дельта позиции между
    // кадрами (шаг зверя дискретный — 1-2 тайла за тик).
    private float _animClock;
    private readonly Dictionary<string, Vector2> _animalLastPos = new();
    private readonly Dictionary<string, AnimalAnimInfo> _lastAnimInfo = new();

    /// <summary>G0: резолв анимации зверя (для _Draw и headless-QA).</summary>
    public sealed class AnimalAnimInfo
    {
        public string AnimId = "animal_idle";
        public SpriteSheetCache.SpriteSheet? Sheet;
        public Texture2D? Procedural;
        public int Frame;
        public bool IsPng => Sheet != null;
    }

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

            // G0 (I-7): анимационный резолв — PNG-лист animal_{species}_{kind}
            // → регион кадра; нет PNG → процедурный кэш по виду КАК СЕЙЧАС.
            bool moving = IsAnimalMoving(animal.EntityId, animal.Position);
            var anim = ResolveAnimalAnim(animal.EntityId, animal.Species, animal.Size, moving);
            _lastAnimInfo[animal.EntityId] = anim;

            // Draw sprite centered on tile.
            float spriteSize = anim.Sheet?.FrameSize ?? anim.Procedural!.GetWidth();
            var pos = new Vector2(cx - spriteSize / 2f, cy - spriteSize / 2f);
            if (anim.Sheet != null)
            {
                int col = anim.Frame % anim.Sheet.FrameCount;
                int row = anim.Frame / anim.Sheet.FrameCount;
                var src = new Rect2(col * anim.Sheet.FrameSize, row * anim.Sheet.FrameSize,
                                    anim.Sheet.FrameSize, anim.Sheet.FrameSize);
                DrawTextureRectRegion(anim.Sheet.Texture,
                    new Rect2(pos, new Vector2(spriteSize, spriteSize)), src);
            }
            else
            {
                DrawTexture(anim.Procedural!, pos);
            }

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

    // === G0 (I-7): анимационный резолв =====================================

    /// <summary>
    /// Движение зверя: дельта позиции с прошлого кадра отрисовки
    /// (шаг зверя — телепорт 1-2 тайла за игровой тик → любая дельта > 0
    /// означает «идёт»; между тиками кадр замирает — это канон D1).
    /// </summary>
    private bool IsAnimalMoving(string entityId, Position2D position)
    {
        var p = new Vector2(position.X, position.Y);
        bool moving = true;
        if (_animalLastPos.TryGetValue(entityId, out var last))
            moving = (p - last).LengthSquared() > 0.0004f; // >0.02 тайла
        _animalLastPos[entityId] = p;
        return moving;
    }

    /// <summary>
    /// Резолв анимации зверя: walk (движение) / idle. Лист
    /// animal_{species}_{kind}; нет PNG → процедурный кэш по виду.
    /// public: headless-QA (GODOT_ANIMQA_DEBUG) резолвит без отрисовки.
    /// </summary>
    public AnimalAnimInfo ResolveAnimalAnim(string entityId, string species, SizeClass size, bool moving)
    {
        string kind = moving ? "walk" : "idle";
        string animId = $"animal_{species.ToLowerInvariant()}_{kind}";
        var sheet = SpriteSheetCache.TryGetSheet(animId);

        var info = new AnimalAnimInfo();
        if (sheet != null)
        {
            info.AnimId = animId;
            info.Sheet = sheet;
            float fps = kind == "walk" ? sheet.Fps : sheet.Fps * 0.4f;
            float phase = _animClock * fps + PhaseOffset(entityId);
            info.Frame = (int)phase % sheet.FrameCount;
            return info;
        }

        info.AnimId = $"animal_{species}_static";
        info.Procedural = GetOrCreateSpeciesTexture(species, size);
        return info;
    }

    /// <summary>Стабильный сдвиг фазы по id (стадо не марширует в ногу).</summary>
    private static float PhaseOffset(string entityId)
    {
        int h = 19;
        foreach (char c in entityId) h = h * 33 + c;
        return (h & 0x7FFFFFFF) % 1000 / 97f;
    }

    private Texture2D GetOrCreateSpeciesTexture(string species, SizeClass size)
    {
        if (_spriteCache.TryGetValue(species, out var tex)) return tex;
        tex = ProceduralSpriteGenerator.CreateAnimalSprite(species, size);
        _spriteCache[species] = tex;
        return tex;
    }

    // === G0: QA-доступ (GODOT_ANIMQA_DEBUG) ================================

    /// <summary>QA: последний резолв анимации зверя (по id; null — не резолвился).</summary>
    public AnimalAnimInfo? GetAnimalAnimInfo(string entityId) =>
        _lastAnimInfo.TryGetValue(entityId, out var info) ? info : null;

    /// <summary>QA: число зверей с PNG-анимацией в последнем проходе _Draw.</summary>
    public int AnimalPngAnimatedCount
    {
        get
        {
            int n = 0;
            foreach (var info in _lastAnimInfo.Values) if (info.IsPng) n++;
            return n;
        }
    }

    private static Color GetColourForSpecies(string species)
    {
        return SpeciesColours.TryGetValue(species, out var c)
            ? c
            : new Color(0.5f, 0.5f, 0.5f);
    }
}
