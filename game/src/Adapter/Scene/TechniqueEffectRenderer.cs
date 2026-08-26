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
/// </summary>
public partial class TechniqueEffectRenderer : Node2D
{
    [Inject] private IPlayerService Player = null!;
    [Inject] private ISubscriber<TechniqueCastResultEvent> CastResultSub = null!;
    [Inject] private ISubscriber<MeditationStateChangedEvent> MeditationSub = null!;
    [Inject] private ISubscriber<QiBufferStateChangedEvent> QiBufferSub = null!;

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
    private bool _meditationVisible;
    private float _meditationPulse;

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        ZIndex = (int)RenderLayer.Player + 5; // выше персонажей, ниже HUD

        _castResultToken = CastResultSub?.Subscribe(OnCastResult);
        _meditationToken = MeditationSub?.Subscribe(OnMeditationChanged);
        _qiBufferToken = QiBufferSub?.Subscribe(OnQiBufferChanged);
        GD.Print("[TechniqueEffectRenderer] Ready");
    }

    public override void _ExitTree()
    {
        _castResultToken?.Dispose();
        _meditationToken?.Dispose();
        _qiBufferToken?.Dispose();
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

        if (anyAlive || _active.Count > 0) QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var v in _active)
            DrawVisual(v);

        if (_meditationVisible)
            DrawMeditationAura();
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
        v.Color = ElementColor(e.Element);
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

    // === Рисование ===

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

    /// <summary>Цвет стихии (ELEMENTS_SYSTEM.md §2).</summary>
    private static Color ElementColor(Element e) => e switch
    {
        Element.Fire => new Color(1.0f, 0.35f, 0.12f),
        Element.Water => new Color(0.2f, 0.5f, 1.0f),
        Element.Earth => new Color(0.6f, 0.4f, 0.2f),
        Element.Air => new Color(0.75f, 0.78f, 0.75f),
        Element.Lightning => new Color(0.95f, 0.88f, 0.25f),
        Element.Void => new Color(0.42f, 0.1f, 0.55f),
        Element.Light => new Color(1.0f, 0.9f, 0.45f),
        Element.Poison => new Color(0.5f, 0.15f, 0.7f),
        _ => new Color(0.95f, 0.95f, 0.95f),
    };
}
