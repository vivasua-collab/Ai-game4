#nullable enable
// Создано: 2026-09-10 — R16 «доработка боевой системы»: анимация удара.
// StrikeFxRenderer — простая анимация нанесения удара (Node2D + _Draw,
// паттерн DamageNumberRenderer: без PNG, без аллокаций на кадр).
//
//   AttackIntentEvent (melee)  → «свип» — бледная дуга-замах у ЦЕЛИ
//                                 по направлению атака→цель (даже при промахе
//                                 — замах состоялся; «уклонение» пишет текстом
//                                 DamageNumberRenderer).
//   DamageAppliedEvent (melee) → «слэш» — яркая дуга + искры попадания
//                                 (Hit/Parry/Block; крит — золотой и крупнее).
//
// Ranged не рисуем: полёт стрелы — трассер ProjectileRenderer (фаза 8 ч.3).
// Пул структур + счётчики-статические для QA (CombatAISimDebug): инкремент
// В ОБРАБОТЧИКЕ СОБЫТИЯ, не в _Draw — headless-прогоны без рендера всё равно
// считают анимации.
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// R16: простая анимация удара — дуги-слэши и искры попадания (процедурно,
/// _Draw; ZIndex = Objects+2 — поверх тел NPC, под цифрами урона +3).
/// </summary>
public partial class StrikeFxRenderer : Node2D
{
    [Inject] private ISubscriber<Core.Messaging.Contracts.AttackIntentEvent>? _intentSub;
    [Inject] private ISubscriber<Core.Messaging.Contracts.DamageAppliedEvent>? _damageSub;
    [Inject] private INPCService? _npcService;
    [Inject] private IPlayerService? _playerService;

    private System.IDisposable? _intentToken;
    private System.IDisposable? _damageToken;

    /// <summary>Одна анимация (пул, без аллокаций на событие).</summary>
    private struct Strike
    {
        public Vector2 Center;     // центр ЦЕЛИ (пиксели, мир)
        public float DirX;         // направление удара (атакующий → цель)
        public float DirY;
        public float Age;          // сек с момента создания
        public bool Impact;        // false = свип-замах (бледный), true = попадание
        public bool Crit;          // крит — золотой, крупнее
    }

    private readonly List<Strike> _active = new();
    private const int MaxConcurrent = 32;       // анти-спам при массовом бое
    private const float SwipeLifetimeSec = 0.25f; // свип-замах
    private const float StrikeLifetimeSec = 0.22f; // слэш попадания

    // === QA-счётчики (GODOT_COMBATAI_DEBUG; инкремент на событии) ===
    public static int TotalSwipes;   // свипы-замахи (AttackIntentEvent, melee)
    public static int TotalStrikes;  // слэши попаданий (DamageAppliedEvent, melee)

    private static readonly Color SwipeColour = new(0.85f, 0.85f, 0.80f, 0.45f);
    private static readonly Color StrikeColour = new(0.98f, 0.95f, 0.88f, 0.95f);
    private static readonly Color CritColour = new(1.0f, 0.80f, 0.28f, 1.0f);
    private static readonly Color SparkColour = new(1.0f, 0.90f, 0.60f, 0.9f);

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        ZIndex = (int)RenderLayer.Objects + 2; // над телами/барами, под цифрами (+3)

        _intentToken = _intentSub?.Subscribe(OnAttackIntent);
        _damageToken = _damageSub?.Subscribe(OnDamageApplied);

        GD.Print("[StrikeFx] Ready");
    }

    public override void _ExitTree()
    {
        _intentToken?.Dispose();
        _intentToken = null;
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

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var s = _active[i];
            float lifetime = s.Impact ? StrikeLifetimeSec : SwipeLifetimeSec;
            s.Age += dt;
            if (s.Age >= lifetime)
            {
                _active.RemoveAt(i);
                continue;
            }
            _active[i] = s;

            float k = s.Age / lifetime; // 0..1
            float alpha = 1f - k * k;   // квадратичное затухание

            // Угол направления удара (атакующий → цель): дуга «рассекает»
            // цель поперёк направления + лёгкий поворот по мере жизни
            // (имитация движения клинка).
            float dirAngle = Mathf.Atan2(s.DirY, s.DirX);
            float sweep = s.Impact ? 1.15f : 0.85f;      // размах дуги (рад)
            float radius = s.Crit ? 20f : (s.Impact ? 15f : 13f);
            float grow = radius * (0.75f + 0.5f * k);    // дуга растёт
            float rot = dirAngle + sweep * (0.15f + 0.55f * k); // «проведение»

            var colour = s.Impact
                ? (s.Crit ? CritColour : StrikeColour)
                : SwipeColour;
            colour.A *= alpha;

            // Дуга клинка: от rot-sweep до rot+sweep вокруг центра цели.
            DrawArc(s.Center, grow, rot - sweep, rot + sweep, 10,
                colour, s.Crit ? 4f : 3f, true);

            // Попадание: искры — короткие лучи из центра (крит — длиннее).
            if (s.Impact && k < 0.6f)
            {
                int sparks = s.Crit ? 7 : 5;
                float sparkAlpha = (1f - k / 0.6f) * 0.8f;
                var sparkColour = SparkColour with { A = sparkAlpha };
                for (int j = 0; j < sparks; j++)
                {
                    float ang = dirAngle + (j - sparks / 2f) * 0.5f;
                    float len = (s.Crit ? 10f : 7f) * (1f - k);
                    var from = s.Center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (grow * 0.7f);
                    var to = from + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * len;
                    DrawLine(from, to, sparkColour, 1.5f, true);
                }
            }
        }
    }

    /// <summary>Замах (AttackIntentEvent, melee): бледный свип у цели.</summary>
    private void OnAttackIntent(in Core.Messaging.Contracts.AttackIntentEvent e)
    {
        if (e.IsRanged) return; // дальний бой — трассер ProjectileRenderer

        var dir = ResolveDirection(e.AttackerId, e.TargetId);
        var center = ResolveTargetPixelPos(e.TargetId);
        if (center == null || dir == null) return;

        if (_active.Count >= MaxConcurrent) return;
        TotalSwipes++;

        _active.Add(new Strike
        {
            Center = center.Value,
            DirX = dir.Value.X,
            DirY = dir.Value.Y,
            Age = 0f,
            Impact = false,
            Crit = false,
        });
    }

    /// <summary>Попадание (DamageAppliedEvent, melee): яркий слэш + искры.</summary>
    private void OnDamageApplied(in Core.Messaging.Contracts.DamageAppliedEvent e)
    {
        // Только ближний бой (подтипы §4.1) и «осязаемые» результаты
        // (Hit/крит/парирование/блок; Dodge — текст «уклонение»).
        if (e.AttackSubtype is not (CombatSubtype.MeleeStrike or CombatSubtype.MeleeWeapon))
            return;
        if (e.Result is not (CombatAttackResult.Hit or CombatAttackResult.CriticalHit
            or CombatAttackResult.Parry or CombatAttackResult.Block))
            return;

        var dir = ResolveDirection(e.SourceId, e.TargetId);
        var center = ResolveTargetPixelPos(e.TargetId);
        if (center == null) return;
        // Направление не резолвится (источник неизвестен) — бьём «сверху-вниз».
        var d = dir ?? new Vector2(0.3f, 0.95f);

        if (_active.Count >= MaxConcurrent) return;
        TotalStrikes++;

        _active.Add(new Strike
        {
            Center = center.Value,
            DirX = d.X,
            DirY = d.Y,
            Age = 0f,
            Impact = true,
            Crit = e.Result == CombatAttackResult.CriticalHit,
        });
    }

    /// <summary>Пиксельная позиция цели (центр тайла) или null.</summary>
    private Vector2? ResolveTargetPixelPos(string entityId)
    {
        float tile = GameConstants.TILE_PIXELS;

        var npc = _npcService?.GetNPC(entityId);
        if (npc != null)
            return new Vector2(npc.Position.X * tile + tile / 2f, npc.Position.Y * tile + tile / 2f);

        if (IsPlayer(entityId) && _playerService != null)
            return new Vector2(
                _playerService.Position.X * tile + tile / 2f,
                _playerService.Position.Y * tile + tile / 2f);

        return null; // животные/неизвестные — без FX пока
    }

    /// <summary>Направление удара (атакующий → цель), пиксели, или null.</summary>
    private Vector2? ResolveDirection(string attackerId, string targetId)
    {
        var from = ResolveTargetPixelPos(attackerId);
        var to = ResolveTargetPixelPos(targetId);
        if (from == null || to == null) return null;

        var dir = to.Value - from.Value;
        if (dir.LengthSquared() < 1f)
            dir = new Vector2(0.3f, 0.95f); // вплотную — «сверху»
        return dir.Normalized();
    }

    private static bool IsPlayer(string id) =>
        id == "player" || id == "player_0";
}
