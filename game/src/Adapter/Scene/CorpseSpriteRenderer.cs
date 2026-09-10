#nullable enable
// Создано: 2026-09-10 — R13 FULL-LOOT: рендер трупов NPC.
// CorpseSpriteRenderer — Godot Node2D, рисует маркеры трупов (снапшот
// ICorpseService.GetAllCorpses каждый кадр, паттерн NPCSpriteRenderer).
// Маркер: тёмный «саван» (эллипс) + контрастный крест/приманка «☠»-стиля
// + число записей лута и имя погибшего. ZIndex = Objects (3) — уровень NPC;
// труп рисуется ПОД живых (NPCSpriteRenderer тоже 3 — порядок в дереве:
// CorpseSpriteRenderer добавляется раньше → рисуется первым).
//
// Пустые трупы рисуются приглушённо (лор: «обобран»), неопустошённые —
// с золотистой меткой «есть лут» (информативность: игрок видит выгоду).
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Renders corpse markers (R13 FULL-LOOT): dark shroud + loot badge +
/// name plate. Re-queries ICorpseService every frame (паттерн NPCSpriteRenderer).
/// </summary>
public partial class CorpseSpriteRenderer : Node2D
{
    [Inject] private ICorpseService? _corpses;

    private int _tilePixels;

    private static readonly Color ShroudColour = new(0.16f, 0.13f, 0.10f, 0.92f);
    private static readonly Color ShroudEmpty = new(0.30f, 0.28f, 0.25f, 0.55f);
    private static readonly Color OutlineColour = new(0.05f, 0.04f, 0.02f, 0.85f);
    private static readonly Color LootBadgeColour = new(0.85f, 0.65f, 0.20f); // золото — «есть лут»
    private static readonly Color CrossColour = new(0.75f, 0.72f, 0.65f);

    // QA-доступ (GODOT_LOOT_DEBUG): число отрисованных маркеров последнего кадра.
    public int LastDrawnMarkerCount { get; private set; }

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);
        _tilePixels = GameConstants.TILE_PIXELS;
        ZIndex = (int)RenderLayer.Objects;
        GD.Print("[CorpseSpriteRenderer] Ready");
    }

    public override void _PhysicsProcess(double delta)
    {
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_corpses == null) return;

        var all = _corpses.GetAllCorpses();
        LastDrawnMarkerCount = all.Count;

        float halfTile = _tilePixels * 0.5f;

        foreach (var corpse in all)
        {
            float cx = corpse.Position.X * _tilePixels + halfTile;
            float cy = corpse.Position.Y * _tilePixels + halfTile;
            bool hasLoot = !corpse.IsEmpty;

            // «Саван»: тёмный эллипс (полтайла) — тело лежит.
            var shroudRect = new Rect2(cx - halfTile * 0.6f, cy - halfTile * 0.25f,
                halfTile * 1.2f, halfTile * 0.5f);
            DrawRect(shroudRect, hasLoot ? ShroudColour : ShroudEmpty);
            DrawRect(shroudRect, OutlineColour, false, 1f);

            // Крест поверх савана (читаемость «труп» на любом фоне).
            DrawLine(new Vector2(cx - 6, cy - 6), new Vector2(cx + 6, cy + 6), CrossColour, 2f);
            DrawLine(new Vector2(cx - 6, cy + 6), new Vector2(cx + 6, cy - 6), CrossColour, 2f);

            // Лут-бейдж: золотая точка сверху-справа — «обыскать выгодно».
            if (hasLoot)
                DrawCircle(new Vector2(cx + halfTile * 0.55f, cy - halfTile * 0.3f), 4f, LootBadgeColour);

            // Нейм-плейт: «Имя · N§» (N записей лута; пусто — без счётчика).
            var font = ThemeDB.FallbackFont;
            const int fontSize = 10;
            const float plateWidth = 110f;
            string text = hasLoot
                ? $"{corpse.DisplayName} · {corpse.ItemCount}§"
                : $"{corpse.DisplayName}";
            var pos = new Vector2(cx - plateWidth / 2f, cy - halfTile * 0.45f - 4f);
            DrawString(font, pos + new Vector2(1, 1), text,
                HorizontalAlignment.Center, plateWidth, fontSize,
                new Color(0, 0, 0, 0.75f));
            DrawString(font, pos, text,
                HorizontalAlignment.Center, plateWidth, fontSize,
                hasLoot ? new Color(0.95f, 0.9f, 0.8f) : new Color(0.7f, 0.68f, 0.62f));
        }
    }
}
