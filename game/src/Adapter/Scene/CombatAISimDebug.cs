#nullable enable
// Создано: 2026-09-10 — R16 «доработка боевой системы»: headless-верификация
// ИИ NPC в бою (GODOT_COMBATAI_DEBUG=1).
//
// Проверяет (все — БЕЗ рендера, счётчики/состояния):
//   1. МЕСТЬ: игрок атакует NPC → NPC входит в Attacking (или Fleeing для
//      миролюбивого) — главный дефект D1 «манекен» закрыт.
//   2. ДВУСТОРОННИЙ БОЙ: урон летит в ОБЕ стороны (игрок→NPC + NPC→игрок
//      собственными атаками ИИ).
//   3. СЕЛЕКТОР ЗАЩИТ NPC: детерминированные кейсы NPCDefenseSelector
//      (щит→Block, силовик→Parry, прочие→Dodge) + лог наблюдаемых
//      результатов Dodge/Parry/Block в живом обмене ударами.
//   4. БЕГСТВО В БОЮ: HP NPC ниже порога → Fleeing + CombatDisengageEvent +
//      IsInCombat сброшен (спека NPC_AI_SYSTEM §4.2).
//   5. LEASH: цель-игрок вне AggroRadius×3 → NPC выходит из боя (aggro-drop).
//   6. СТОЙКА ИГРОКА (G): DefenseIntentEvent → CombatService.CurrentPlayerDefense.
//   7. АНИМАЦИЯ УДАРА: StrikeFxRenderer.TotalSwipes/TotalStrikes ≥ 1
//      (счётчики инкрементятся в обработчиках событий — headless-совместимо).
//
// Паттерн: CombatSimDebug (async-сценарий, VERDICT в конце).
using Godot;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// R16: headless-верификация боевого ИИ NPC. Добавляется в дерево
/// GameWorldController-ом при GODOT_COMBATAI_DEBUG=1.
/// </summary>
public partial class CombatAISimDebug : Node2D
{
    [Inject] private IPublisher<AttackIntentEvent>? _attackIntentPub;
    [Inject] private IPublisher<DefenseIntentEvent>? _defenseIntentPub;
    [Inject] private ISubscriber<DamageAppliedEvent>? _damageSub;
    [Inject] private ISubscriber<CombatDisengageEvent>? _disengageSub;
    [Inject] private ISubscriber<CombatStartedEvent>? _combatStartedSub;
    [Inject] private INPCService? _npcService;
    [Inject] private IPlayerService? _playerService;
    [Inject] private CultivationGame.Modules.Combat.CombatService? _combatServiceImpl;

    private System.IDisposable? _damageToken;
    private System.IDisposable? _disengageToken;
    private System.IDisposable? _combatStartedToken;

    private const string PlayerCombatId = "player_0";

    // Счётчики наблюдений.
    private int _damageToNpc;
    private int _damageToPlayer;
    private int _npcDefendedDodge;
    private int _npcDefendedParry;
    private int _npcDefendedBlock;
    private int _disengageCount;
    private int _combatStartedCount;
    private string _lastDisengageReason = "";
    // Отслеживаемый NPC (счётчик урона по нему) — задаётся при выборе.
    private string? _trackedNpcId;

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        GD.Print($"[CombatAI] diag: intentPub={_attackIntentPub != null} defensePub={_defenseIntentPub != null} " +
                 $"dmgSub={_damageSub != null} disSub={_disengageSub != null} " +
                 $"npc={_npcService != null} player={_playerService != null} combat={_combatServiceImpl != null}");

        _damageToken = _damageSub?.Subscribe((in DamageAppliedEvent e) =>
        {
            if (e.TargetId == PlayerCombatId || e.TargetId == "player") _damageToPlayer++;
            if (_trackedNpcId != null && e.TargetId == _trackedNpcId) _damageToNpc++;
            if (e.Result == CombatAttackResult.Dodge) _npcDefendedDodge++;
            if (e.Result == CombatAttackResult.Parry) _npcDefendedParry++;
            if (e.Result == CombatAttackResult.Block) _npcDefendedBlock++;
        });

        _disengageToken = _disengageSub?.Subscribe((in CombatDisengageEvent e) =>
        {
            _disengageCount++;
            _lastDisengageReason = e.Reason;
            GD.Print($"[CombatAI] disengage: {e.NpcId} — {e.Reason}");
        });

        _combatStartedToken = _combatStartedSub?.Subscribe((in CombatStartedEvent e) =>
        {
            _combatStartedCount++;
            GD.Print($"[CombatAI] combat started: {e.InstigatorId} → {e.TargetId}");
        });

        // R16: режим — сценарий QA (COMBATAI_DEBUG) или визуальный HOLD
        // (STRIKEFX_HOLD: живая драка для скриншотов).
        _holdMode = System.Environment.GetEnvironmentVariable("GODOT_STRIKEFX_HOLD") == "1";
        GD.Print($"[CombatAI] Ready — {(_holdMode ? "HOLD (визуальный)" : "сценарий")} через 2с");
        if (_holdMode) _ = RunHoldLoopAsync();
        else _ = RunScenarioAsync();
    }

    /// <summary>
    /// R16: визуальный HOLD (GODOT_STRIKEFX_HOLD=1) — БЕЗ сценария: NPC у
    /// игрока и цикл NPC→player интентов (реальная драка: свипы, замахи
    /// оружия NPC, цифры урона, HP-бары). Для Xvfb-скриншотов
    /// (GODOT_SCREENSHOT + DELAY) — статичный кадр живого боя.
    /// Интенты от NPC: отклонения молчат (не-игрок), боя стартует честно.
    /// </summary>
    private bool _holdMode;

    private async System.Threading.Tasks.Task RunHoldLoopAsync()
    {
        await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);

        string? npcId = FindHostileNpc() ?? FindAnyNpc();
        if (npcId == null || _playerService == null || _attackIntentPub == null)
        {
            GD.Print("[CombatAI] HOLD: no NPC — abort");
            return;
        }
        var npcState = _npcService!.GetNPCState(npcId)!;
        var playerPos = _playerService.Position;
        npcState.Position = new Position2D(playerPos.X + 1, playerPos.Y);
        GD.Print($"[CombatAI] HOLD: {npcId} рядом с игроком, цикл атак включён");

        // Цикл: интент NPC каждые 0.3с (реальный бой через turn-гейты +
        // постоянные свипы/замахи для скриншота: бо́льшая часть кадров
        // содержит активную анимацию удара — окно 0.25с из 0.3с).
        while (true)
        {
            _attackIntentPub.Publish(new AttackIntentEvent(
                npcId, PlayerCombatId, "npc_strike", false));
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        }
    }

    public override void _ExitTree()
    {
        _damageToken?.Dispose();
        _damageToken = null;
        _disengageToken?.Dispose();
        _disengageToken = null;
        _combatStartedToken?.Dispose();
        _combatStartedToken = null;
    }

    private async System.Threading.Tasks.Task RunScenarioAsync()
    {
        await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);

        if (_attackIntentPub == null || _npcService == null || _playerService == null
            || _combatServiceImpl == null)
        {
            GD.Print("[CombatAI] FAIL — DI not wired");
            PrintVerdict(false);
            return;
        }

        // === Выбор и подготовка NPC ===
        string? npcId = FindHostileNpc() ?? FindAnyNpc();
        if (npcId == null)
        {
            GD.Print("[CombatAI] FAIL — no NPC spawned");
            PrintVerdict(false);
            return;
        }
        var npcState = _npcService.GetNPCState(npcId)!;
        _trackedNpcId = npcId;
        var playerPos = _playerService.Position;
        npcState.Position = new Position2D(playerPos.X + 1, playerPos.Y); // рядом
        GD.Print($"[CombatAI] NPC {npcId} ({npcState.Role}/{npcState.Disposition}, " +
                 $"personality={npcState.Personality}) телепортирован к игроку");

        // === 1. МЕСТЬ: игрок атакует NPC → NPC отвечает ===
        npcState.Threats.Clear();
        _damageToNpc = 0;
        _attackIntentPub.Publish(new AttackIntentEvent(
            PlayerCombatId, npcId, "basic_attack", false));
        await ToSignal(GetTree().CreateTimer(2.5), SceneTreeTimer.SignalName.Timeout);

        bool npcResponded = npcState.AIState is NPCAIState.Attacking or NPCAIState.Fleeing;
        bool retaliationOk = npcResponded && _damageToNpc > 0;
        GD.Print($"[CombatAI] 1-месть: AIState={npcState.AIState}, " +
                 $"урон по NPC={_damageToNpc}, combatStarted={_combatStartedCount} → " +
                 $"{(retaliationOk ? "OK" : "FAIL")}");

        // === 2. ДВУСТОРОННИЙ БОЙ: NPC атакует игрока сам ===
        // (месть уже в Attacking — ждём атак ИИ; ходовая модель отдаст ход NPC
        // после резолва атаки игрока, кулдаун 1.6с + каст ~0.5с.)
        float waited = 0f;
        while (waited < 10f && _damageToPlayer == 0)
        {
            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
            waited += 0.5f;
        }
        // R16-аудит (P2-2): урон по NPC обязателен в ЛЮБОЙ ветке — иначе
        // fallback FindAnyNpc (миролюбивый NPC → Fleeing без единого удара
        // по игроку) проходил тест «двусторонний бой» впустую.
        bool twoWayOk = (_damageToPlayer > 0 || npcState.AIState == NPCAIState.Fleeing)
                        && _damageToNpc > 0;
        if (npcState.AIState == NPCAIState.Fleeing && _damageToPlayer == 0)
            GD.Print("[CombatAI] WARN: тест 2 вырожден — NPC бежал, не ударив игрока (не-Hostile fallback)");
        GD.Print($"[CombatAI] 2-двусторонний-бой: игрок получил {_damageToPlayer} атак, " +
                 $"урон по NPC={_damageToNpc}, AIState={npcState.AIState} → {(twoWayOk ? "OK" : "FAIL")}");

        // === 3. СЕЛЕКТОР ЗАЩИТ NPC (детерминированные кейсы) ===
        bool selectorOk = true;
        selectorOk &= CultivationGame.Modules.Combat.NPCDefenseSelector.PickDefense(true, true, 10, 10) == DefenseSubtype.Block;
        selectorOk &= CultivationGame.Modules.Combat.NPCDefenseSelector.PickDefense(false, true, 10, 15) == DefenseSubtype.Parry;
        selectorOk &= CultivationGame.Modules.Combat.NPCDefenseSelector.PickDefense(false, true, 12, 10) == DefenseSubtype.Dodge;
        selectorOk &= CultivationGame.Modules.Combat.NPCDefenseSelector.PickDefense(false, false, 8, 8) == DefenseSubtype.Dodge;
        // R16-аудит (P2-1): ПРОВОДКА селектора в реальном бою — детерминированно
        // через CombatService.LastNpcDefenseSelected (селектор вызывается на
        // КАЖДОЙ атаке по NPC-защитнику; None после тестов 1–2 = регрессия
        // проводки в BuildAndExecuteDamageRequest — дефект D4). Счётчики
        // Dodge/Parry/Block (успешные защиты) — только информация: зависят от
        // RNG-ролла, при одном обмене могут быть нулевыми.
        bool wiringOk = _combatServiceImpl!.LastNpcDefenseSelected != DefenseSubtype.None;
        selectorOk &= wiringOk;
        int defendedTotal = _npcDefendedDodge + _npcDefendedParry + _npcDefendedBlock;
        GD.Print($"[CombatAI] 3-селектор: кейсы {(selectorOk ? "OK" : "FAIL")}; проводка в бою: " +
                 $"selected={_combatServiceImpl.LastNpcDefenseSelected} → {(wiringOk ? "OK" : "FAIL")}; " +
                 $"успешные защиты: dodge={_npcDefendedDodge}, parry={_npcDefendedParry}, " +
                 $"block={_npcDefendedBlock} (инфо, RNG)");

        // === 4. БЕГСТВО В БОЮ (HP<20%) ===
        _disengageCount = 0;
        npcState.CurrentHealth = 1; // healthRatio ≈ 0 < FleeHealthRatio (0.2)
        await ToSignal(GetTree().CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);
        bool fleeOk = npcState.AIState == NPCAIState.Fleeing
                      && !npcState.IsInCombat
                      && _disengageCount > 0;
        GD.Print($"[CombatAI] 4-бегство: AIState={npcState.AIState}, IsInCombat={npcState.IsInCombat}, " +
                 $"disengage={_disengageCount} ('{_lastDisengageReason}') → {(fleeOk ? "OK" : "FAIL")}");

        // === 5. LEASH (aggro-drop) ===
        _disengageCount = 0;
        npcState.IsInCombat = true;
        npcState.TargetId = PlayerCombatId;
        // Игрок «убегает»: телепорт за радиус привязи (AggroRadius×3 = 15 тайлов).
        var npcNow = npcState.Position;
        int farX = System.Math.Min(npcNow.X + 25, 49);
        int farY = System.Math.Min(npcNow.Y + 25, 49);
        _playerService.SetPosition(new Position2D(farX, farY));
        await ToSignal(GetTree().CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);
        bool leashOk = !npcState.IsInCombat && _disengageCount > 0;
        // R16-аудит (P1-1): после выхода из боя НИКАКОГО зависшего каста —
        // AbandonCombat/EndCombat обязан погасить _isCasting (иначе все
        // следующие атаки отклоняются гейтом «Каст уже идёт» навсегда).
        bool noStaleCast = !_combatServiceImpl!.IsCasting;
        leashOk &= noStaleCast;
        GD.Print($"[CombatAI] 5-leash: игрок → ({farX},{farY}), IsInCombat={npcState.IsInCombat}, " +
                 $"AIState={npcState.AIState}, disengage={_disengageCount}, " +
                 $"staleCast={_combatServiceImpl.IsCasting} → {(leashOk ? "OK" : "FAIL")}");
        // Вернуть игрока (последующие хуки/визуал не должны «улететь»).
        _playerService.SetPosition(playerPos);

        // === 6. СТОЙКА ЗАЩИТЫ ИГРОКА (G) ===
        _defenseIntentPub!.Publish(new DefenseIntentEvent(PlayerCombatId, DefenseSubtype.Dodge));
        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        bool stanceDodge = _combatServiceImpl.CurrentPlayerDefense == DefenseSubtype.Dodge;
        _defenseIntentPub.Publish(new DefenseIntentEvent(PlayerCombatId, DefenseSubtype.Parry));
        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        bool stanceParry = _combatServiceImpl.CurrentPlayerDefense == DefenseSubtype.Parry;
        bool stanceOk = stanceDodge && stanceParry;
        GD.Print($"[CombatAI] 6-стойка: Dodge={stanceDodge}, Parry={stanceParry} → " +
                 $"{(stanceOk ? "OK" : "FAIL")}");

        // === 7. АНИМАЦИЯ УДАРА (StrikeFxRenderer) ===
        bool fxOk = StrikeFxRenderer.TotalSwipes >= 1 && StrikeFxRenderer.TotalStrikes >= 1;
        GD.Print($"[CombatAI] 7-анимация: swipes={StrikeFxRenderer.TotalSwipes}, " +
                 $"strikes={StrikeFxRenderer.TotalStrikes} → {(fxOk ? "OK" : "FAIL")}");

        // === ИТОГ ===
        bool pass = retaliationOk && twoWayOk && selectorOk && fleeOk && leashOk && stanceOk && fxOk;
        GD.Print($"[CombatAI] SUMMARY: месть={retaliationOk} двусторонний={twoWayOk} " +
                 $"селектор={selectorOk} бегство={fleeOk} leash={leashOk} " +
                 $"стойка={stanceOk} анимация={fxOk}");
        PrintVerdict(pass);
    }

    private string? FindHostileNpc()
    {
        if (_npcService == null) return null;
        foreach (var id in _npcService.GetAllNPCIds())
        {
            var state = _npcService.GetNPCState(id);
            if (state != null && state.IsAlive && state.Disposition == NPCDisposition.Hostile)
                return id;
        }
        return null;
    }

    private string? FindAnyNpc()
    {
        if (_npcService == null) return null;
        foreach (var id in _npcService.GetAllNPCIds())
        {
            if (_npcService.IsAlive(id)) return id;
        }
        return null;
    }

    private static void PrintVerdict(bool pass)
    {
        GD.Print($"[CombatAI] VERDICT: {(pass
            ? "PASS — месть/двусторонний бой/селектор защит/бегство/leash/стойка/анимация"
            : "FAIL")}");
    }
}
