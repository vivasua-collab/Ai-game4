#nullable enable
// Создано: 2026-08-25 — NPC_COMBAT_PREP Phase 7: Combat Visuals.
// DamageNumberRenderer — всплывающие цифры урона над целью (Node2D + _Draw,
// без аллокаций Label — паттерн проекта). Подписан на DamageAppliedEvent:
//   Hit/CriticalHit → «−N» (красный / золотой для крита)
//   Dodge → «уклонение», Parry → «парирование», Block → «блок» (серые)
// Позиция: NPC (INPCService.Position) или игрок (IPlayerService.Position).
// Источник: docs/docs_v2/09_workflow/NPC_COMBAT_PREP.md §Phase 7.
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Floating combat text (Phase 7). Active numbers are pooled structs drawn
/// via DrawString with the fallback font; each floats up ~26px and fades
/// over ~0.9s. ZIndex above ground items, below UI.
/// </summary>
public partial class DamageNumberRenderer : Node2D
{
    [Inject] private ISubscriber<Core.Messaging.Contracts.DamageAppliedEvent>? _damageSub;
    [Inject] private INPCService? _npcService;
    [Inject] private IPlayerService? _playerService;

    private System.IDisposable? _damageToken;

    /// <summary>Одно всплывающее число (пул, без аллокаций на событие).</summary>
    private struct FloatText
    {
        public Vector2 Position;   // стартовая позиция (пиксели, мир)
        public string Text;        // «−12» / «уклонение» / ...
        public Color Colour;       // цвет по типу результата
        public float Age;          // сек с момента создания
    }

    private readonly List<FloatText> _active = new();
    private const int MaxConcurrent = 48;      // анти-спам при массовом бое
    private const float LifetimeSec = 0.9f;
    private const float RisePixels = 26f;

    // Стартовые смещения, чтобы числа от серии ударов не сливались.
    private float _jitter;

    private static readonly Color DamageColour = new(0.92f, 0.22f, 0.18f);
    private static readonly Color CritColour = new(1.0f, 0.78f, 0.25f);
    private static readonly Color PlayerHurtColour = new(1.0f, 0.35f, 0.30f);
    private static readonly Color MissWordColour = new(0.75f, 0.75f, 0.70f, 0.9f);

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        ZIndex = (int)RenderLayer.Objects + 3; // выше ground items (+1) и NPC-баров (+2)

        // Подписка ПОСЛЕ DI-инъекции (_Ready, не _EnterTree — там поля ещё null).
        _damageToken = _damageSub?.Subscribe(OnDamageApplied);

        GD.Print("[DamageNumberRenderer] Ready");
    }

    public override void _ExitTree()
    {
        _damageToken?.Dispose();
        _damageToken = null;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_active.Count == 0) return;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float dt = (float)GetPhysicsProcessDeltaTime();
        var font = ThemeDB.FallbackFont;

        // Обновление возраста + отрисовка задом наперёд (свежие сверху).
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var t = _active[i];
            t.Age += dt;
            if (t.Age >= LifetimeSec)
            {
                _active.RemoveAt(i);
                continue;
            }
            _active[i] = t;

            float k = t.Age / LifetimeSec;              // 0..1
            float y = t.Position.Y - RisePixels * k;    // всплытие
            byte alpha = (byte)(255 * (1f - k * k));    // затухание (квадратичное)

            var colour = t.Colour;
            colour.A = alpha / 255f;
            DrawString(font, new Vector2(t.Position.X, y), t.Text,
                HorizontalAlignment.Center, -1, 16, colour);
        }
    }

    /// <summary>Событие урона → всплывающее число над целью.</summary>
    private void OnDamageApplied(in Core.Messaging.Contracts.DamageAppliedEvent e)
    {
        if (_active.Count >= MaxConcurrent) return; // анти-спам

        Vector2? worldPos = ResolveTargetPixelPos(e.TargetId);
        if (worldPos == null) return;

        // Текст и цвет по результату атаки (CombatAttackResult).
        string text;
        Color colour;
        switch (e.Result)
        {
            case CombatAttackResult.CriticalHit:
                text = $"КРИТ −{e.Damage}";
                colour = CritColour;
                break;
            case CombatAttackResult.Hit:
                text = $"−{e.Damage}";
                colour = IsPlayer(e.TargetId) ? PlayerHurtColour : DamageColour;
                break;
            case CombatAttackResult.Dodge:
                text = "уклонение";
                colour = MissWordColour;
                break;
            case CombatAttackResult.Parry:
                text = "парирование";
                colour = MissWordColour;
                break;
            case CombatAttackResult.Block:
                text = "блок";
                colour = MissWordColour;
                break;
            default:
                return; // Miss и прочее не показываем (шум)
        }

        // Джиттер по X, чтобы серия ударов читалась.
        _jitter = (_jitter + 13f) % 7f;
        float offset = _jitter - 3f;

        _active.Add(new FloatText
        {
            Position = new Vector2(worldPos.Value.X + offset, worldPos.Value.Y - 18f),
            Text = text,
            Colour = colour,
            Age = 0f,
        });
    }

    /// <summary>Пиксельная позиция цели (центр тайла) или null.</summary>
    private Vector2? ResolveTargetPixelPos(string entityId)
    {
        float tile = GameConstants.TILE_PIXELS;

        // NPC (включая животных — их Id тоже в NPCService? нет: AnimalService отдельный).
        var npc = _npcService?.GetNPC(entityId);
        if (npc != null)
            return new Vector2(npc.Position.X * tile + tile / 2f, npc.Position.Y * tile + tile / 2f);

        // Игрок (оба исторических ID).
        if (IsPlayer(entityId) && _playerService != null)
            return new Vector2(
                _playerService.Position.X * tile + tile / 2f,
                _playerService.Position.Y * tile + tile / 2f);

        return null; // животные/неизвестные — без позиционирования пока
    }

    private static bool IsPlayer(string id) => id == "player" || id == "player_0";
}
