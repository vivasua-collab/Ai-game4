#nullable enable
// Создано: 2026-09-21 — R29 (план R23 §6.7, дизайн §1.2): визуальный полёт
// самонаводящихся снарядов по ProjectileSpawnedEvent (издан
// CombatService.TrySpawnHomingProjectile в R28 — «рендерер интерполирует
// полёт от FromX/Y без дополнительного события»).
//
// ВАЖНО: полёт — ПРЕДСТАВЛЕНИЕ, не источник истины (урон решает симуляция
// в CombatService-тике; принцип из шапки TechniqueEffectRenderer). Визуал:
//   • стартует в точке выпуска (милли-тайлы → пиксели), скорость — из
//     события (тайлов/сек → пиксели/сек, real-time);
//   • каждый кадр доворачивает вектор к ТЕКУЩЕЙ позиции цели (ре-резолв
//     через сервисы) с визуальным лимитом поворота — «закручивание»
//     читается, кайт виден (анти «turn on a dime», как в симуляции);
//   • контакт (~0.8 тайла) → вспышка-кольцо; цель пропала → летит в
//     последнюю точку и тает; потолок жизни 5с (симметрия с симуляцией).
//
// Паттерн StrikeFxRenderer (R16): Node2D + _Draw, пул структур,
// MaxConcurrent, QA-статик инкрементится В ОБРАБОТЧИКЕ СОБЫТИЯ.
// ZIndex = Objects+4 (паттерн ProjectileRenderer: снаряд читается поверх
// боевого текста).
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// R29: летящий самонаводящийся снаряд — светящийся болт цвета стихии с
/// хвостом; наведение на живую цель с лимитом поворота; вспышка контакта.
/// </summary>
public partial class HomingProjectileRenderer : Node2D
{
    [Inject] private ISubscriber<Core.Messaging.Contracts.ProjectileSpawnedEvent>? _projectileSub;
    [Inject] private INPCService? _npcService;
    [Inject] private IPlayerService? _playerService;
    [Inject] private IAnimalService? _animalService;

    private System.IDisposable? _projectileToken;

    /// <summary>Один визуальный снаряд (пул структур, без аллокаций на кадр).</summary>
    private struct Bolt
    {
        public Vector2 Pos;             // мировые пиксели
        public Vector2 Dir;             // нормализованный вектор полёта
        public float SpeedPxPerSec;     // скорость (real-time)
        public float Age;
        public float LifeSec;           // потолок (симметрия с симуляцией 5с)
        public Color Colour;            // стихия
        public string TargetId;         // для ре-резолва позиции
        public Vector2 LastTargetPos;   // милли-пиксели → пиксели (снимок)
        public bool Impact;             // фаза вспышки контакта
        public float ImpactAge;
    }

    private readonly List<Bolt> _bolts = new(8);
    private const int MaxConcurrent = 16;          // анти-спам массового боя
    private const float ImpactLifetimeSec = 0.12f; // вспышка контакта
    private const float ContactPx = 0.8f * GameConstants.TILE_PIXELS;
    private const float MaxVisualTurnRadPerSec = Mathf.Pi / 4f; // ~45°/с — «закрутка»

    // === QA-счётчик (GODOT_COMBAT_SIM 3k; инкремент на событии) ===
    public static int TotalLaunches;   // пусков снарядов получено
    // R29-фикс-диагностика: жив ли _Draw-пайплайн (0 при headless — норма).
    public static int TotalDrawCalls;

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        // Выше ground items (+1), NPC-баров (+2), цифр урона (+3) — паттерн
        // ProjectileRenderer (стрелы): снаряд поверх боевого текста.
        ZIndex = (int)RenderLayer.Objects + 4;

        _projectileToken = _projectileSub?.Subscribe(OnProjectileSpawned);
        GD.Print("[HomingFx] Ready");
    }

    public override void _ExitTree()
    {
        _projectileToken?.Dispose();
        _projectileToken = null;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        bool anyAlive = false;

        for (int i = _bolts.Count - 1; i >= 0; i--)
        {
            var b = _bolts[i];

            if (b.Impact)
            {
                b.ImpactAge += dt;
                if (b.ImpactAge >= ImpactLifetimeSec) { _bolts.RemoveAt(i); continue; }
                _bolts[i] = b;
                anyAlive = true;
                continue;
            }

            b.Age += dt;
            if (b.Age >= b.LifeSec)
            {
                // «Ци рассеивается вдали от мастера» — тает без вспышки.
                _bolts.RemoveAt(i);
                continue;
            }

            // Seek: цель жива → обновляем точку наведения (ре-резолв каждый
            // кадр — визуал следует за убегающей целью).
            var live = ResolveEntityPixelPos(b.TargetId);
            if (live != null) b.LastTargetPos = live.Value;

            // Доворот с визуальным лимитом (анти «turn on a dime»).
            var desired = b.LastTargetPos - b.Pos;
            if (desired.LengthSquared() > 1f)
            {
                var des = desired.Normalized();
                float maxTurn = MaxVisualTurnRadPerSec * dt;
                b.Dir = RotateToward(b.Dir, des, maxTurn);
            }

            // Полёт.
            b.Pos += b.Dir * b.SpeedPxPerSec * dt;
            _bolts[i] = b;
            anyAlive = true;

            // Контакт: вспышка на месте (урон решает симуляция).
            if (b.Pos.DistanceTo(b.LastTargetPos) <= ContactPx)
            {
                b.Impact = true;
                b.ImpactAge = 0f;
                _bolts[i] = b;
            }
        }

        if (anyAlive || _bolts.Count > 0) QueueRedraw();
    }

    public override void _Draw()
    {
        TotalDrawCalls++;
        foreach (var b in _bolts)
        {
            if (b.Impact) DrawImpact(b);
            else DrawFlight(b);
        }
    }

    // === Подписка ===

    private void OnProjectileSpawned(in Core.Messaging.Contracts.ProjectileSpawnedEvent e)
    {
        TotalLaunches++;
        if (_bolts.Count >= MaxConcurrent) return; // анти-спам

        float tile = GameConstants.TILE_PIXELS;
        var pos = new Vector2(e.FromX / 1000f * tile, e.FromY / 1000f * tile);

        var target = ResolveEntityPixelPos(e.TargetId);
        var lastTarget = target ?? pos + Vector2.Right * tile * 3f;

        var dir = (lastTarget - pos).Normalized();
        if (dir == Vector2.Zero) dir = Vector2.Right;

        _bolts.Add(new Bolt
        {
            Pos = pos,
            Dir = dir,
            SpeedPxPerSec = Mathf.Max(1, e.SpeedTilesPerSec) * tile,
            Age = 0f,
            LifeSec = 5f,
            Colour = ElementPalette.ToColor(e.Element),
            TargetId = e.TargetId,
            LastTargetPos = lastTarget,
            Impact = false,
            ImpactAge = 0f,
        });
        // R29-диагностика (одна строка на пуск): где родился и куда летит.
        GD.Print($"[HomingFx] bolt spawned at {pos} → target {lastTarget} " +
                 $"(dir {dir}, {SpeedPx(e)}px/s)");
        QueueRedraw();
    }

    private static float SpeedPx(in Core.Messaging.Contracts.ProjectileSpawnedEvent e) =>
        Mathf.Max(1, e.SpeedTilesPerSec) * GameConstants.TILE_PIXELS;

    // === Рисование (паттерн Directional из TechniqueEffectRenderer) ===

    private void DrawFlight(in Bolt b)
    {
        // Хвост: 3 затухающих круга позади (след Ци) — R29-буст читаемости:
        // llvmpipe-кадры слабо читали бледный Air-болт (vfx_shot8 VLM:
        // «что-то есть, но не снаряд») → крупнее ядро/хвост/сердцевина.
        for (int i = 1; i <= 3; i++)
        {
            var trailPos = b.Pos - b.Dir * (12f * i);
            DrawCircle(trailPos, 11f - i * 2.5f, WithAlpha(b.Colour, 0.38f / i));
        }

        // Ореол-свечение (читаемость на любом фоне).
        DrawCircle(b.Pos, 16f, WithAlpha(b.Colour, 0.22f));

        // Ядро: круг стихии + белая сердцевина.
        DrawCircle(b.Pos, 13f, WithAlpha(b.Colour, 0.95f));
        DrawCircle(b.Pos, 7f, new Color(1f, 1f, 1f, 0.95f));
    }

    /// <summary>Вспышка контакта: расширяющееся кольцо (0..0.12с).</summary>
    private void DrawImpact(in Bolt b)
    {
        float k = b.ImpactAge / ImpactLifetimeSec;
        var colour = b.Colour;
        colour.A = 0.9f * (1f - k);
        DrawArc(b.LastTargetPos, 4f + 18f * k, 0f, Mathf.Tau, 16, colour, 3f, true);
        DrawCircle(b.LastTargetPos, 5f * (1f - k), new Color(1f, 1f, 1f, 0.85f * (1f - k)));
    }

    // === Помощники ===

    /// <summary>Поворот вектора к цели не более, чем на maxRad (shortest arc).</summary>
    private static Vector2 RotateToward(Vector2 current, Vector2 target, float maxRad)
    {
        float cur = current.Angle();
        float des = target.Angle();
        float diff = Mathf.Wrap(des - cur, -Mathf.Pi, Mathf.Pi);
        if (Mathf.Abs(diff) <= maxRad) return target;
        float next = cur + Mathf.Sign(diff) * maxRad;
        return new Vector2(Mathf.Cos(next), Mathf.Sin(next));
    }

    /// <summary>Пиксельная позиция сущности (NPC ∪ зверь ∪ игрок) или null.</summary>
    private Vector2? ResolveEntityPixelPos(string entityId)
    {
        float tile = GameConstants.TILE_PIXELS;

        var npc = _npcService?.GetNPC(entityId);
        if (npc != null)
            return new Vector2(npc.Position.X * tile + tile / 2f, npc.Position.Y * tile + tile / 2f);

        var animal = _animalService?.TryGetAnimal(entityId);
        if (animal != null)
            return new Vector2(
                animal.Value.Position.X * tile + tile / 2f,
                animal.Value.Position.Y * tile + tile / 2f);

        if (IsPlayer(entityId) && _playerService != null)
            return new Vector2(
                _playerService.Position.X * tile + tile / 2f,
                _playerService.Position.Y * tile + tile / 2f);

        return null;
    }

    private static bool IsPlayer(string id) => id == "player" || id == "player_0";

    private static Color WithAlpha(Color c, float a)
    {
        var copy = c;
        copy.A = Mathf.Clamp(a, 0f, 1f);
        return copy;
    }
}
