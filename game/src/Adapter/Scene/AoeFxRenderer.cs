#nullable enable
// Создано: 2026-09-21 — R29 (план R23 §6.7, дизайн §1.1): вспышки площадных
// залпов по AoeImpactEvent (издан CombatService.ExecuteAoeVolley в R25 —
// «для VFX-фазы»: форма/эпицентр/радиус/стихия/цели).
//
// Рисует по ФОРМЕ события (тайлы → пиксели):
//   Circle     — расширяющееся кольцо от эпицентра + заливка-вспышка;
//   Cone       — клин от кастера (Origin) к направлению прицеливания
//                (Origin→Epicenter), полуугол HalfAngleDeg;
//   Semicircle — полукруг перед кастером по направлению к прицелу;
//   Line       — полоса-луч Origin→Epicenter (длина = RadiusTiles).
// Плюс маркеры целей: маленькое кольцо на каждой цели из TargetIds
// (NPC ∪ звери ∪ игрок — паттерн DamageNumberRenderer D5), «у каждого
// своя вспышка»; цифры урона рисует DamageNumberRenderer.
//
// Паттерн StrikeFxRenderer (R16): Node2D + _Draw, пул структур без
// аллокаций на кадр, MaxConcurrent, QA-статики инкрементятся В
// ОБРАБОТЧИКЕ СОБЫТИЯ — headless-прогоны без рендера всё равно считают.
// ZIndex = Objects+2 (уровень свипов: над телами/барами, под цифрами +3
// и стрелами +4 — вспышка не перекрывает боевой текст).
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// R29: вспышка площадного залпа — контур формы + заливка + маркеры целей.
/// Цвет — стихия залпа (ElementPalette). Жизнь ~0.5с, квадратичное
/// затухание (паттерн Strike).
/// </summary>
public partial class AoeFxRenderer : Node2D
{
    [Inject] private ISubscriber<Core.Messaging.Contracts.AoeImpactEvent>? _aoeImpactSub;
    [Inject] private INPCService? _npcService;
    [Inject] private IPlayerService? _playerService;
    [Inject] private IAnimalService? _animalService;

    private System.IDisposable? _aoeImpactToken;

    /// <summary>Один залп (пул структур, без аллокаций на событие).</summary>
    private struct Volley
    {
        public Vector2 Origin;      // кастер (пиксели, центр тайла)
        public Vector2 Epicenter;   // точка прицеливания (пиксели)
        public AoeShape Shape;
        public float RadiusPx;      // радиус/полудлина в пикселях
        public float HalfAngleRad;  // полуугол конуса/полукруга
        public float Age;
        public Color Colour;        // стихия
    }

    /// <summary>Маркер-вспышка на одной цели залпа.</summary>
    private struct TargetFlash
    {
        public Vector2 Center;
        public float Age;
        public Color Colour;
    }

    private readonly List<Volley> _volleys = new(8);
    private readonly List<TargetFlash> _flashes = new(16);
    private const int MaxConcurrentVolleys = 12;   // анти-спам толпы
    private const int MaxConcurrentFlashes = 32;
    private const float VolleyLifetimeSec = 0.5f;
    private const float FlashLifetimeSec = 0.35f;

    // === QA-счётчики (GODOT_COMBAT_SIM 3k; инкремент на событии) ===
    public static int TotalVolleys;        // залпов получено
    public static int TotalTargetFlashes;  // маркеров целей (длина TargetIds)
    // R29-фикс-диагностика: жив ли _Draw-пайплайн (печатается в 3k; 0 при
    // headless — там рендера нет, это НОРМА; >0 при opengl3-скриншотах).
    public static int TotalDrawCalls;

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        ZIndex = (int)RenderLayer.Objects + 2; // уровень свипов, под цифрами

        _aoeImpactToken = _aoeImpactSub?.Subscribe(OnAoeImpact);
        GD.Print("[AoeFx] Ready");
    }

    public override void _ExitTree()
    {
        _aoeImpactToken?.Dispose();
        _aoeImpactToken = null;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_volleys.Count == 0 && _flashes.Count == 0) return;
        QueueRedraw();
    }

    public override void _Draw()
    {
        TotalDrawCalls++;
        float dt = (float)GetPhysicsProcessDeltaTime();

        for (int i = _volleys.Count - 1; i >= 0; i--)
        {
            var v = _volleys[i];
            v.Age += dt;
            if (v.Age >= VolleyLifetimeSec)
            {
                _volleys.RemoveAt(i);
                continue;
            }
            _volleys[i] = v;
            DrawVolley(v);
        }

        for (int i = _flashes.Count - 1; i >= 0; i--)
        {
            var f = _flashes[i];
            f.Age += dt;
            if (f.Age >= FlashLifetimeSec)
            {
                _flashes.RemoveAt(i);
                continue;
            }
            _flashes[i] = f;
            DrawFlash(f);
        }
    }

    /// <summary>Залп площадной техники → вспышка формы + маркеры целей.</summary>
    private void OnAoeImpact(in Core.Messaging.Contracts.AoeImpactEvent e)
    {
        float tile = GameConstants.TILE_PIXELS;
        TotalVolleys++;

        if (_volleys.Count < MaxConcurrentVolleys)
        {
            // Направление Origin→Epicenter (конус/полукруг/линия); для круга
            // не используется.
            _volleys.Add(new Volley
            {
                Origin = new Vector2(e.OriginX * tile + tile / 2f, e.OriginY * tile + tile / 2f),
                Epicenter = new Vector2(e.EpicenterX * tile + tile / 2f, e.EpicenterY * tile + tile / 2f),
                Shape = e.Shape,
                RadiusPx = Mathf.Max(1f, e.RadiusTiles) * tile,
                HalfAngleRad = Mathf.DegToRad(Mathf.Max(1, e.HalfAngleDeg)),
                Age = 0f,
                Colour = ElementPalette.ToColor(e.Element),
            });
        }

        // Маркеры целей: вспышка на каждой (позиция на момент события —
        // снимок, паттерн ProjectileRenderer).
        foreach (var id in e.TargetIds)
        {
            var pos = ResolveEntityPixelPos(id);
            if (pos == null) continue;
            if (_flashes.Count >= MaxConcurrentFlashes) break;
            TotalTargetFlashes++;
            _flashes.Add(new TargetFlash
            {
                Center = pos.Value,
                Age = 0f,
                Colour = ElementPalette.ToColor(e.Element),
            });
        }
    }

    // === Рисование ===

    private void DrawVolley(in Volley v)
    {
        float k = v.Age / VolleyLifetimeSec;      // 0..1
        float alpha = 1f - k * k;                 // квадратичное затухание
        var colour = v.Colour;
        float dir = (v.Epicenter - v.Origin).Angle();

        switch (v.Shape)
        {
            case AoeShape.Circle:
            {
                // Расширяющееся кольцо от эпицентра + заливка-вспышка.
                float radius = v.RadiusPx * (0.35f + 0.65f * k);
                DrawCircle(v.Epicenter, radius, WithAlpha(colour, 0.16f * alpha));
                DrawArc(v.Epicenter, radius, 0f, Mathf.Tau, 40,
                    WithAlpha(colour, 0.85f * alpha), 4f, true);
                // Ядро эпицентра — белая точка, гаснет первой.
                DrawCircle(v.Epicenter, 10f * (1f - k), new Color(1f, 1f, 1f, 0.8f * alpha));
                break;
            }
            case AoeShape.Cone:
            {
                // Клин: две грани от кастера + дуга на радиусе + веер-заливка.
                float sweep = v.HalfAngleRad;
                float grow = v.RadiusPx * (0.55f + 0.45f * k);
                DrawFan(v.Origin, dir, sweep, grow, WithAlpha(colour, 0.14f * alpha));
                DrawArc(v.Origin, grow, dir - sweep, dir + sweep, 24,
                    WithAlpha(colour, 0.85f * alpha), 3.5f, true);
                DrawLine(v.Origin, v.Origin + AngleToVector(dir - sweep) * grow,
                    WithAlpha(colour, 0.8f * alpha), 2.5f, true);
                DrawLine(v.Origin, v.Origin + AngleToVector(dir + sweep) * grow,
                    WithAlpha(colour, 0.8f * alpha), 2.5f, true);
                break;
            }
            case AoeShape.Semicircle:
            {
                // Полукруг перед кастером: дуга ±90° + хорда-замыкатель.
                float grow = v.RadiusPx * (0.55f + 0.45f * k);
                DrawFan(v.Origin, dir, Mathf.Pi / 2f, grow, WithAlpha(colour, 0.14f * alpha));
                DrawArc(v.Origin, grow, dir - Mathf.Pi / 2f, dir + Mathf.Pi / 2f, 32,
                    WithAlpha(colour, 0.85f * alpha), 3.5f, true);
                // Хорда: от края к краю полукруга.
                var a = v.Origin + AngleToVector(dir - Mathf.Pi / 2f) * grow;
                var b = v.Origin + AngleToVector(dir + Mathf.Pi / 2f) * grow;
                DrawLine(a, b, WithAlpha(colour, 0.5f * alpha), 2f, true);
                break;
            }
            case AoeShape.Line:
            {
                // Луч: полоса Origin→Epicenter (длина = радиус) шириной ~1
                // тайл + яркое ядро-линия; «прожигает» путь.
                var end = v.Origin + AngleToVector(dir) * v.RadiusPx;
                float width = GameConstants.TILE_PIXELS * (0.9f + 0.4f * k);
                DrawThickLine(v.Origin, end, width, WithAlpha(colour, 0.15f * alpha));
                DrawLine(v.Origin, end, WithAlpha(colour, 0.9f * alpha), 3.5f, true);
                // Ядро луча светлеет к завершению.
                DrawLine(v.Origin + AngleToVector(dir) * 8f, end,
                    new Color(1f, 1f, 1f, 0.5f * alpha), 1.5f, true);
                break;
            }
        }
    }

    /// <summary>Маркер цели: расширяющееся кольцо (~0.35с, каскад цифр следом).</summary>
    private void DrawFlash(in TargetFlash f)
    {
        float k = f.Age / FlashLifetimeSec;
        float alpha = 1f - k * k;
        float radius = 8f + 16f * k;
        DrawArc(f.Center, radius, 0f, Mathf.Tau, 16,
            WithAlpha(f.Colour, 0.9f * alpha), 2.5f, true);
        DrawCircle(f.Center, 4f * (1f - k), new Color(1f, 1f, 1f, 0.7f * alpha));
    }

    // === Геометрические помощники (только _Draw-поток) ===

    private static Vector2 AngleToVector(float angle) =>
        new(Mathf.Cos(angle), Mathf.Sin(angle));

    /// <summary>Веер-заливка сектора (полигон по дуге).</summary>
    private void DrawFan(Vector2 origin, float dir, float sweep, float radius, Color colour)
    {
        if (sweep <= 0f || radius <= 0f) return;
        int steps = Mathf.Clamp((int)(sweep / 0.12f), 3, 24) + 1;
        // Полигон = origin + (steps+1) точек дуги → размер steps+2
        // (R29-фикс: было steps+1 — IndexOutOfRangeException рвал _Draw,
        // конус и маркеры целей не рисовались; поймано VLM-скриншотом 3k).
        var points = new Vector2[steps + 2];
        points[0] = origin;
        var colours = new Color[points.Length];
        for (int i = 0; i <= steps; i++)
        {
            float a = dir - sweep + 2f * sweep * i / steps;
            points[i + 1] = origin + AngleToVector(a) * radius;
            colours[i + 1] = colour;
        }
        colours[0] = colour;
        DrawPolygon(points, colours);
    }

    /// <summary>Толстая линия-полоса (2 прямоугольника по нормали).</summary>
    private void DrawThickLine(Vector2 from, Vector2 to, float width, Color colour)
    {
        var dir = (to - from).Normalized();
        if (dir == Vector2.Zero) dir = Vector2.Right;
        var perp = new Vector2(-dir.Y, dir.X) * (width / 2f);
        DrawPolygon(
            new Vector2[] { from - perp, to - perp, to + perp, from + perp },
            new Color[] { colour, colour, colour, colour });
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

    /// <summary>Копия цвета с изменённой альфой.</summary>
    private static Color WithAlpha(Color c, float a)
    {
        var copy = c;
        copy.A = Mathf.Clamp(a, 0f, 1f);
        return copy;
    }
}
