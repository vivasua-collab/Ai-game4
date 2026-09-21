#nullable enable
// Этап 3 внедрения ЦИ (2026-08-23): TechniqueEffectRenderer — схематические
// визуальные эффекты техник (TECHNIQUE_EFFECTS.md, категория «sprite-swap»
// заменена на custom _Draw — принцип проекта: без PNG на этом этапе).
//
// Виды (VisualKind из TechniqueCastResultEvent):
//   0 Directional — снаряд: круг летит origin→target со шлейфом
//   1 Expanding   — AoE: растущая окружность с затуханием
//   2 Self        — аура вокруг игрока (Support/Sensory/Movement)
//   3 Heal        — зелёное расширяющееся кольцо
//   4 Shield      — щит вокруг игрока (Defense, живёт пока активен Ци-буфер)
// Плюс: медитация — мягкое пульсирующее кольцо (MeditationStateChangedEvent).
//
// Цвета по стихиям — ELEMENTS_SYSTEM.md §2. Пул визуалов — переиспользование.
// Рендер — представление, не источник истины (урон применяется независимо).
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Рендерер схематических эффектов техник. Node2D в мировом пространстве
/// (child of world root), рисует в _Draw, обновляет активные визуалы в _Process.
/// R29 (план R23 §6.7): + аура УДЕРЖАНИЯ техники (HeldTechniqueChangedEvent,
/// контракт обещал «визуал ауры» цветом стихии) — пульсирующий обод +
/// вращающиеся искры-руны вокруг игрока, пока техника запаркована в ауре.
/// </summary>
public partial class TechniqueEffectRenderer : Node2D
{
    [Inject] private IPlayerService Player = null!;
    [Inject] private ISubscriber<TechniqueCastResultEvent> CastResultSub = null!;
    [Inject] private ISubscriber<MeditationStateChangedEvent> MeditationSub = null!;
    [Inject] private ISubscriber<QiBufferStateChangedEvent> QiBufferSub = null!;
    // R29: аура удержания (техника запаркована в ауре — HeldTechniqueChangedEvent).
    [Inject] private ISubscriber<HeldTechniqueChangedEvent> HeldChangedSub = null!;

    /// <summary>Вид визуала (соответствует TechniqueCastResultEvent.VisualKind).</summary>
    private enum VisualKind { Directional = 0, Expanding = 1, Self = 2, Heal = 3, Shield = 4, Meditation = 5 }

    private class ActiveVisual
    {
        public VisualKind Kind;
        public Vector2 Origin;      // мировые пиксели
        public Vector2 Target;      // мировые пиксели
        public Color Color;
        public float Elapsed;
        public float Duration;
        public bool Used;
    }

    private readonly List<ActiveVisual> _active = new(16);
    private readonly Stack<ActiveVisual> _pool = new(16);
    private System.IDisposable? _castResultToken;
    private System.IDisposable? _meditationToken;
    private System.IDisposable? _qiBufferToken;
    private System.IDisposable? _heldChangedToken;
    private bool _meditationVisible;
    private float _meditationPulse;

    // === R29: аура удержания ===
    // Цвет стихии удерживаемой техники; null — аура выключена.
    private Color? _heldAuraColour;
    private float _heldPulse;

    // === QA-счётчик (GODOT_COMBAT_SIM 3k; инкремент на событии) ===
    public static int TotalHoldAuraChanges;

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        ZIndex = (int)RenderLayer.Player + 5; // выше персонажей, ниже HUD

        _castResultToken = CastResultSub?.Subscribe(OnCastResult);
        _meditationToken = MeditationSub?.Subscribe(OnMeditationChanged);
        _qiBufferToken = QiBufferSub?.Subscribe(OnQiBufferChanged);
        _heldChangedToken = HeldChangedSub?.Subscribe(OnHeldTechniqueChanged);
        GD.Print("[TechniqueEffectRenderer] Ready");
    }

    public override void _ExitTree()
    {
        _castResultToken?.Dispose();
        _meditationToken?.Dispose();
        _qiBufferToken?.Dispose();
        _heldChangedToken?.Dispose();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        bool anyAlive = false;

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var v = _active[i];
            v.Elapsed += dt;
            if (v.Elapsed >= v.Duration)
            {
                _active.RemoveAt(i);
                _pool.Push(v);
            }
            else
            {
                anyAlive = true;
            }
        }

        if (_meditationVisible) { _meditationPulse += dt; anyAlive = true; }
        if (_heldAuraColour != null) { _heldPulse += dt; anyAlive = true; }

        if (anyAlive || _active.Count > 0) QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var v in _active)
            DrawVisual(v);

        if (_meditationVisible)
            DrawMeditationAura();

        // R29: аура удержания — поверх остальных (заряженная техника видна).
        if (_heldAuraColour != null)
            DrawHeldAura(_heldAuraColour.Value);
    }

    // === Подписки ===

    private void OnCastResult(in TechniqueCastResultEvent e)
    {
        if (!e.Success) return;

        var kind = e.VisualKind switch
        {
            1 => VisualKind.Expanding,
            2 => VisualKind.Self,
            3 => VisualKind.Heal,
            4 => VisualKind.Shield,
            _ => VisualKind.Directional
        };

        var v = _pool.Count > 0 ? _pool.Pop() : new ActiveVisual();
        v.Kind = kind;
        v.Origin = new Vector2(e.OriginX / 1000f, e.OriginY / 1000f);
        v.Target = new Vector2(e.TargetX / 1000f, e.TargetY / 1000f);
        v.Color = ElementPalette.ToColor(e.Element);
        v.Elapsed = 0f;
        v.Duration = kind switch
        {
            VisualKind.Directional => 0.45f,
            VisualKind.Expanding => 0.8f,
            VisualKind.Self => 1.2f,
            VisualKind.Heal => 0.9f,
            VisualKind.Shield => 2.5f,
            _ => 0.5f
        };
        v.Used = true;
        _active.Add(v);
        QueueRedraw();
    }

    private void OnMeditationChanged(in MeditationStateChangedEvent e)
    {
        _meditationVisible = e.IsActive;
        QueueRedraw();
    }

    private void OnQiBufferChanged(in QiBufferStateChangedEvent e)
    {
        if (e.EntityId != "player") return;
        if (e.IsActive)
        {
            var v = _pool.Count > 0 ? _pool.Pop() : new ActiveVisual();
            v.Kind = VisualKind.Shield;
            v.Origin = Vector2.Zero;
            v.Target = Vector2.Zero;
            v.Color = new Color(0.4f, 0.7f, 1.0f, 0.8f);
            v.Elapsed = 0f;
            v.Duration = float.MaxValue; // пока буфер активен (заменяется деактивацией)
            v.Used = true;
            _active.Add(v);
        }
        else
        {
            // Снять все щиты.
            for (int i = _active.Count - 1; i >= 0; i--)
                if (_active[i].Kind == VisualKind.Shield)
                {
                    var v = _active[i];
                    _active.RemoveAt(i);
                    _pool.Push(v);
                }
        }
        QueueRedraw();
    }

    // === R29: аура удержания ===

    /// <summary>
    /// R29: удержание техники в ауре изменилось (AuraHoldService).
    /// TechniqueId ≠ "" → аура включена цветом стихии; "" → выключена.
    /// Считает QA-статик (проводка события → рендерер, headless-безопасно).
    /// </summary>
    private void OnHeldTechniqueChanged(in HeldTechniqueChangedEvent e)
    {
        if (e.EntityId != "player" && e.EntityId != "player_0") return;
        TotalHoldAuraChanges++;

        _heldAuraColour = string.IsNullOrEmpty(e.TechniqueId)
            ? null
            : ElementPalette.ToColor(e.Element);
        QueueRedraw();
    }

    private void DrawVisual(ActiveVisual v)
    {
        float t = v.Duration > 0 ? v.Elapsed / v.Duration : 1f; // 0..1
        var playerPos = PlayerPixelPos();

        switch (v.Kind)
        {
            case VisualKind.Directional:
            {
                // Снаряд: летит по прямой origin→target.
                var pos = v.Origin.Lerp(v.Target, t);
                float alpha = 1.0f - 0.3f * t;
                // Шлейф: 3 затухающих круга позади.
                for (int i = 1; i <= 3; i++)
                {
                    var trailPos = v.Origin.Lerp(v.Target, Mathf.Max(0f, t - 0.08f * i));
                    DrawCircle(trailPos, 9f - i * 2f, WithAlpha(v.Color, 0.25f * alpha / i));
                }
                DrawCircle(pos, 11f, WithAlpha(v.Color, 0.95f * alpha));
                DrawCircle(pos, 5f, new Color(1f, 1f, 1f, 0.9f * alpha));
                break;
            }
            case VisualKind.Expanding:
            {
                // AoE: растущая окружность от точки применения.
                float radius = 12f + 90f * t;
                float alpha = 1.0f - t;
                DrawCircle(v.Target, radius, WithAlpha(v.Color, 0.22f * alpha));
                DrawArc(v.Target, radius, 0f, Mathf.Tau, 32, WithAlpha(v.Color, 0.9f * alpha), 3f);
                break;
            }
            case VisualKind.Self:
            {
                // Аура вокруг игрока: пульсирующее кольцо.
                float pulse = 0.9f + 0.1f * Mathf.Sin(v.Elapsed * 10f);
                float radius = 46f * pulse;
                float alpha = 1.0f - 0.5f * t;
                DrawArc(playerPos, radius, 0f, Mathf.Tau, 40, WithAlpha(v.Color, 0.8f * alpha), 3.5f);
                DrawArc(playerPos, radius * 0.6f, 0f, Mathf.Tau, 32, WithAlpha(v.Color, 0.4f * alpha), 2f);
                break;
            }
            case VisualKind.Heal:
            {
                // Лечение: зелёное расширяющееся кольцо + восходящие искры.
                float radius = 18f + 52f * t;
                float alpha = 1.0f - t;
                var healColor = new Color(0.35f, 0.9f, 0.45f);
                DrawArc(playerPos, radius, 0f, Mathf.Tau, 40, WithAlpha(healColor, 0.85f * alpha), 3f);
                for (int i = 0; i < 4; i++)
                {
                    float sparkT = (v.Elapsed * 1.4f + i * 0.25f) % 1f;
                    var sparkPos = playerPos + new Vector2(Mathf.Sin(i * 2.1f + v.Elapsed) * 22f, 20f - 46f * sparkT);
                    DrawCircle(sparkPos, 3f, WithAlpha(healColor, 0.8f * (1f - sparkT)));
                }
                break;
            }
            case VisualKind.Shield:
            {
                // Щит: двойная дуга вокруг игрока.
                float pulse = 0.95f + 0.05f * Mathf.Sin(v.Elapsed * 6f);
                float radius = 52f * pulse;
                DrawArc(playerPos, radius, 0f, Mathf.Tau, 48, WithAlpha(v.Color, 0.75f), 4f);
                DrawArc(playerPos, radius * 0.8f, 0f, Mathf.Tau, 40, WithAlpha(v.Color, 0.35f), 2f);
                break;
            }
        }
    }

    private void DrawMeditationAura()
    {
        var pos = PlayerPixelPos();
        float pulse = Mathf.Sin(_meditationPulse * 2.2f);
        float radius = 40f + 6f * pulse;
        var gold = new Color(0.85f, 0.75f, 0.35f);
        // Мягкое затухающее кольцо + восходящие частицы Ци.
        DrawArc(pos, radius, 0f, Mathf.Tau, 40, WithAlpha(gold, 0.55f + 0.15f * pulse), 2.5f);
        for (int i = 0; i < 5; i++)
        {
            float sparkT = (_meditationPulse * 0.7f + i * 0.2f) % 1f;
            var sparkPos = pos + new Vector2(Mathf.Sin(i * 1.9f + _meditationPulse) * 18f, 14f - 40f * sparkT);
            DrawCircle(sparkPos, 2.5f, WithAlpha(gold, 0.7f * (1f - sparkT)));
        }
    }

    /// <summary>
    /// R29: аура удержания — плотный пульсирующий обод цвета стихии +
    /// 4 вращающиеся искры-руны («заряженная Ци кружит вокруг мастера»).
    /// Радиус 34px — плотнее медитации (40) и щита (52): слои не сливаются.
    /// </summary>
    private void DrawHeldAura(Color colour)
    {
        var pos = PlayerPixelPos();
        float pulse = Mathf.Sin(_heldPulse * 3.4f);
        float radius = 34f + 4f * pulse;

        DrawArc(pos, radius, 0f, Mathf.Tau, 40, WithAlpha(colour, 0.7f + 0.2f * pulse), 3f);
        DrawArc(pos, radius * 0.62f, 0f, Mathf.Tau, 32, WithAlpha(colour, 0.35f), 1.5f);

        // Вращающиеся искры-руны (орбита).
        for (int i = 0; i < 4; i++)
        {
            float ang = _heldPulse * 1.6f + i * (Mathf.Pi / 2f);
            var spark = pos + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;
            DrawCircle(spark, 3.5f, WithAlpha(colour, 0.9f));
            DrawCircle(spark, 1.5f, new Color(1f, 1f, 1f, 0.85f));
        }
    }

    private Vector2 PlayerPixelPos()
    {
        if (Player == null) return Vector2.Zero;
        var p = Player.Position;
        return new Vector2(
            p.X * GameConstants.TILE_PIXELS + GameConstants.TILE_PIXELS / 2f,
            p.Y * GameConstants.TILE_PIXELS + GameConstants.TILE_PIXELS / 2f);
    }

    /// <summary>Копия цвета с изменённой альфой (Godot Color не имеет ctor(Color, float)).</summary>
    private static Color WithAlpha(Color c, float a)
    {
        var copy = c;
        copy.A = Mathf.Clamp(a, 0f, 1f);
        return copy;
    }

    // R29: цвета стихий вынесены в общий ElementPalette (чистый перенос
    // значений — источник один для всех мировых VFX-рендереров).
}
