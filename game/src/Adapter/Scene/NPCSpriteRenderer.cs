#nullable enable
// Создано: 2026-08-22 — NPC_COMBAT_PREP Phase 1: рендер NPC.
// NPCSpriteRenderer — Godot Node2D that draws colored circles for each
// spawned human NPC. Re-queries NPCService every frame so NPCs are drawn
// at their current position (NPCMovementService updates positions per tick).
// Источник: docs/docs_v2/09_workflow/NPC_COMBAT_PREP.md §Phase 1
// Редактировано: 2026-08-25 — Phase 7: HP-бар над NPC при повреждении
// (IBodyDataProvider.GetCurrentHealth/GetMaxHealth; бар скрыт, пока HP полный).
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
/// Phase 7: рисует HP-бар над раненым NPC (текущий/полный RedHP).
/// </summary>
public partial class NPCSpriteRenderer : Node2D
{
    [Inject] private INPCService? _npcService;
    [Inject] private IBodyDataProvider? _bodyProvider;

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

    // Cached sprites per role.
    private readonly Dictionary<NPCRole, Texture2D> _spriteCache = new();

    public override void _Draw()
    {
        if (_npcService == null) return;

        _idSnapshot.Clear();
        foreach (var id in _npcService.GetAllNPCIds())
            _idSnapshot.Add(id);

        float halfTile = _tilePixels * 0.5f;

        foreach (var id in _idSnapshot)
        {
            var npc = _npcService.GetNPC(id);
            if (npc == null || !_npcService.IsAlive(id)) continue;

            float cx = npc.Position.X * _tilePixels + halfTile;
            float cy = npc.Position.Y * _tilePixels + halfTile;

            // Get or create sprite for this role.
            if (!_spriteCache.TryGetValue(npc.Role, out var tex))
            {
                tex = ProceduralSpriteGenerator.CreateNPCSprite(npc.Role);
                _spriteCache[npc.Role] = tex;
            }

            // Draw sprite centered on tile.
            float spriteSize = tex.GetWidth();
            var pos = new Vector2(cx - spriteSize / 2f, cy - spriteSize / 2f);
            DrawTexture(tex, pos);

            // Phase 7: HP-бар над NPC — только если повреждён (полный HP не рисуем,
            // чтобы не засорять HUD в мирное время). Высота полосы — над спрайтом.
            // 2026-09-04 S1 (VLM-аудит): + имя и уровень NPC над баром —
            // информативность боя (видно КТО ранен и его силу).
            if (_bodyProvider != null)
            {
                int hp = _bodyProvider.GetCurrentHealth(id);
                int maxHp = _bodyProvider.GetMaxHealth(id);
                if (maxHp > 0 && hp < maxHp)
                {
                    float barTop = cy - spriteSize / 2f - 8f;
                    DrawNpcHealthBar(cx, barTop, hp, maxHp);

                    var st = _npcService.GetNPCState(id);
                    if (st != null)
                    {
                        int lvl = (int)st.CultivationLevel;
                        string nameText = lvl > 0
                            ? $"{st.DisplayName} · L{lvl} · {hp}HP"
                            : $"{st.DisplayName} · {hp}HP";
                        DrawNpcNamePlate(cx, barTop, nameText);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Phase 7: компактный HP-бар (48×5 px): тёмная подложка + заливка по ratio.
    /// Цвет: зелёный > 50%, жёлтый > 25%, красный ниже (семантика BodyStatusPanel).
    /// </summary>
    private void DrawNpcHealthBar(float cx, float top, int hp, int maxHp)
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
    /// 2026-09-04 S1: нейм-плейт над HP-баром NPC: «Имя · L{уровень} · {hp}HP».
    /// Рисуется только вместе с HP-баром (NPC повреждён → бой идёт).
    /// Центрируется над баром (паттерн FormationVisualRenderer), тень для
    /// читаемости на любом фоне.
    /// </summary>
    private void DrawNpcNamePlate(float cx, float barTop, string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var font = ThemeDB.FallbackFont;
        const int fontSize = 10;
        const float plateWidth = 96f;
        var pos = new Vector2(cx - plateWidth / 2f, barTop - 4f);

        // Тень (контраст на любом фоне) + основной текст.
        DrawString(font, pos + new Vector2(1, 1), text,
            HorizontalAlignment.Center, plateWidth, fontSize,
            new Color(0, 0, 0, 0.75f));
        DrawString(font, pos, text,
            HorizontalAlignment.Center, plateWidth, fontSize,
            new Color(0.95f, 0.9f, 0.8f));
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
