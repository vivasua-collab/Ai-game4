#nullable enable
// Создано: 2026-09-03 — Phase 8 ч.3: визуальный трассер снаряда дальнего боя.
//
// ПРОБЛЕМА: ranged-атака наносила урон по DamageAppliedEvent сразу после
// каста — визуально «стрела из ниоткуда»: игрок не ВИДИТ, кто и откуда
// стрелял (особенно NPC-лучники: урон прилетает без анимации).
//
// РЕШЕНИЕ: Node2D + _Draw (паттерн DamageNumberRenderer — без спрайтов,
// без аллокаций Node на событие). Подписан на DamageAppliedEvent:
//   • фильтр AttackSubtype == RangedProjectile (стрелы; Qi-снаряды
//     рисует TechniqueEffectRenderer — слои не пересекаются);
//   • полёт: линия-стрела из позиции атакующего в цель (~0.22с),
//     тёплый коричневый цвет + короткий шлейф;
//   • попадание: маленькая вспышка на цели (~0.12с).
// Данные ПОЗИЦИЙ резолвятся на момент события (NPC мог сдвинуться —
// снимок в момент попадания; MVP-достаточно).
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Phase 8 ч.3: трассер стрелы (полёт + вспышка попадания).
/// ZIndex выше цифр урона: снаряд читается поверх боевого текста.
/// </summary>
public partial class ProjectileRenderer : Node2D
{
    [Inject] private ISubscriber<Core.Messaging.Contracts.DamageAppliedEvent>? _damageSub;
    [Inject] private INPCService? _npcService;
    [Inject] private IPlayerService? _playerService;

    private System.IDisposable? _damageToken;

    /// <summary>Один летящий снаряд (пул структур, анти-аллокация).</summary>
    private struct Flight
    {
        public Vector2 From;        // мировые пиксели (атакующий)
        public Vector2 To;          // мировые пиксели (цель)
        public float Age;           // сек с момента создания
        public bool ImpactPhase;    // true = долетела, рисуем вспышку
    }

    private readonly List<Flight> _flights = new(8);
    private const int MaxConcurrent = 16;       // анти-спам при массовом бое
    private const float FlySec = 0.22f;         // полёт стрелы
    private const float ImpactSec = 0.12f;      // вспышка попадания

    // Цвета: древко — тёплый орех, наконечник/вспышка — светлая бронза.
    private static readonly Color ShaftColour = new(0.62f, 0.45f, 0.28f, 0.95f);
    private static readonly Color TrailColour = new(0.62f, 0.45f, 0.28f, 0.35f);
    private static readonly Color ImpactColour = new(1.0f, 0.85f, 0.55f, 0.8f);

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        // Выше ground items (+1), NPC-баров (+2), цифр урона (+3).
        ZIndex = (int)RenderLayer.Objects + 4;

        _damageToken = _damageSub?.Subscribe(OnDamageApplied);
        GD.Print("[ProjectileRenderer] Ready");
    }

    public override void _ExitTree()
    {
        _damageToken?.Dispose();
        _damageToken = null;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_flights.Count == 0) return;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float dt = (float)GetPhysicsProcessDeltaTime();

        for (int i = _flights.Count - 1; i >= 0; i--)
        {
            var f = _flights[i];
            f.Age += dt;

            if (f.ImpactPhase)
            {
                if (f.Age >= ImpactSec) { _flights.RemoveAt(i); continue; }
                _flights[i] = f;
                DrawImpact(f);
                continue;
            }

            if (f.Age >= FlySec)
            {
                // Стрела долетела → фаза вспышки (возраст перезапускается).
                f.ImpactPhase = true;
                f.Age = 0f;
                _flights[i] = f;
                continue;
            }
            _flights[i] = f;
            DrawFlight(f);
        }
    }

    /// <summary>Стрела в полёте: линия-древко + шлейф за ней.</summary>
    private void DrawFlight(in Flight f)
    {
        float k = f.Age / FlySec;                       // 0..1 по траектории
        var head = f.From.Lerp(f.To, k);
        var dir = (f.To - f.From).Normalized();
        if (dir == Vector2.Zero) dir = Vector2.Right;

        // Древко 26px, наконечник опережает древко (визуальный «разбег»).
        var tail = head - dir * 26f;
        DrawLine(tail, head, ShaftColour, 2.5f, antialiased: true);

        // Шлейф — короткая затухающая линия позади древка.
        var trailEnd = tail - dir * 10f;
        DrawLine(tail, trailEnd, TrailColour, 1.5f, antialiased: true);

        // Наконечник: маленький треугольник по направлению полёта.
        var perp = new Vector2(-dir.Y, dir.X);
        var tip = head + dir * 6f;
        DrawTriangle(tip, head + perp * 2.5f, head - perp * 2.5f, ShaftColour);
    }

    /// <summary>Вспышка попадания: расширяющееся кольцо (0..0.12с).</summary>
    private void DrawImpact(in Flight f)
    {
        float k = f.Age / ImpactSec;                    // 0..1
        var colour = ImpactColour;
        colour.A = 0.8f * (1f - k);
        DrawArc(f.To, 3f + 9f * k, 0f, Mathf.Tau, 12, colour, 2f, antialiased: true);
    }

    private void DrawTriangle(Vector2 a, Vector2 b, Vector2 c, Color colour)
    {
        DrawPolygon(new Vector2[] { a, b, c }, new Color[] { colour, colour, colour });
    }

    /// <summary>Попадание стрелой → запуск полёта от атакующего к цели.</summary>
    private void OnDamageApplied(in Core.Messaging.Contracts.DamageAppliedEvent e)
    {
        // Только стрелы: Qi-снаряды техник рисует TechniqueEffectRenderer.
        if (e.AttackSubtype != CombatSubtype.RangedProjectile) return;
        if (_flights.Count >= MaxConcurrent) return;    // анти-спам

        var from = ResolvePixelPos(e.SourceId);
        var to = ResolvePixelPos(e.TargetId);
        if (from == null || to == null) return;

        _flights.Add(new Flight { From = from.Value, To = to.Value, Age = 0f });
    }

    /// <summary>Пиксельная позиция сущности (центр тайла) или null.</summary>
    private Vector2? ResolvePixelPos(string entityId)
    {
        float tile = GameConstants.TILE_PIXELS;

        var npc = _npcService?.GetNPC(entityId);
        if (npc != null)
            return new Vector2(npc.Position.X * tile + tile / 2f, npc.Position.Y * tile + tile / 2f);

        if (IsPlayer(entityId) && _playerService != null)
            return new Vector2(
                _playerService.Position.X * tile + tile / 2f,
                _playerService.Position.Y * tile + tile / 2f);

        return null; // животные/неизвестные — без визуала
    }

    private static bool IsPlayer(string id) => id == "player" || id == "player_0";
}
