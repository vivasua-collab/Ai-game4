#nullable enable
// Создано: 2026-10-22 — Этап-5 аудита (бой/состояния сущностей): headless-верификация
// трёх фиксов по регрессионным сценариям аудитора:
//
//   BUG-015 (P1) «бой объявляет цель убитой до фактической смерти тела»:
//     — удар 50 по голове свежей цели НЕ убивает (70/30-сплит оставляет 15/50
//       красной HP; прежний IsFatalHit (Head/Heart ≥ 50) ставил ложную смерть:
//       Victory/Defeat/EnemyKilledEvent без трупа и NPCDeathEvent);
//     — повторный удар 50 добивает: смерть подтверждается телом, события
//       смерти/зачёта/труп приходят РОВНО один раз;
//     — то же для сердца (два удара по 50 при MaxRedHP=80 — с BUG-016
//       сердце принимает полный урон; раньше требовалось три);
//     — консистентность DamageService: result.IsFatal == !IsEntityAlive
//       после каждого удара (единый источник истины — тело);
//     — сценарий игрока: удар 50 в голову — игрок жив, PlayerDeathEvent нет;
//     — реальный бой через CombatService.ExecuteAttack: смерть NPC завершает
//       бой (IsInCombat=false), зачёт убийства ровно один.
//
//   BUG-016 (P2) «30% урона по сердцу теряется»:
//     — прямое попадание 10 в сердце: красная HP −10 (80→70), чёрная = 0
//       (прежде: −7, 30% отбрасывалось в несуществующий структурный слой);
//     — обычная часть (торс): сплит 70/30 сохранён (−7/−3);
//     — интеграция: два удара по 50 в сердце (80 HP) убивают цель.
//
//   BUG-017 (P2) «убийство DoT не засчитывается в квестах»:
//     — реальная цепочка: ApplyBuff(elemental_poison, sourceId=player) →
//       BuffTickProcessor → BodyModule → DamageAppliedEvent(dot:фантом,
//       OriginalAttacker=player) → AnimalService: NPCDeathEvent(killer=
//       «player_0» — атрибуция, не «dot:…») + EnemyKilledEvent → квест
//       «Охота на волков» +1 шаг;
//     — два добивающих удара → квест Completed (3/3), зачёт ровно по одному
//       на волка (никаких двойных EnemyKilledEvent).
//
// Запуск: GODOT_NEWGAME=1 GODOT_STAGE5_DEBUG=1 \
//   godot --headless --path . scenes/MainMenu.tscn
using Godot;
using System.Collections.Generic;
using System.Linq;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Helpers;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Headless-верификация Этапа-5: тело = единый источник истины смерти,
/// part-aware сплит урона, унифицированный зачёт убийств (удар + DoT).
/// Итог: [Stage5Sim] VERDICT: PASS/FAIL.
/// </summary>
public partial class Stage5CombatTruthSimDebug : Node
{
    [Inject] private IBodyService? _bodyService;
    [Inject] private IBodyDataProvider? _bodyProvider;
    [Inject] private IDamageService? _damageService;
    [Inject] private INPCSpawnerService? _npcSpawner;
    [Inject] private INPCService? _npcService;
    [Inject] private Modules.NPC.AnimalService? _animalService;
    [Inject] private IBuffService? _buffService;
    [Inject] private IQuestService? _quests;
    [Inject] private ICombatService? _combatService;
    [Inject] private ICorpseService? _corpseService;
    [Inject] private IPlayerService? _playerService;

    [Inject] private ISubscriber<EnemyKilledEvent>? _enemyKilledSub;
    [Inject] private ISubscriber<NPCDeathEvent>? _npcDeathSub;
    [Inject] private ISubscriber<PlayerDeathEvent>? _playerDeathSub;
    [Inject] private IPublisher<DamageAppliedEvent>? _damagePub;

    private System.IDisposable? _enemyKilledToken;
    private System.IDisposable? _npcDeathToken;
    private System.IDisposable? _playerDeathToken;

    // === Счётчики событий (глобальные за время сима; шаги считают дельты) ===
    private readonly List<string> _kills = new();            // EnemyKilledEvent.EnemyId
    private readonly List<(string npc, string killer)> _deaths = new(); // NPCDeathEvent
    private int _playerDeaths;

    private int KillsOf(string id) => _kills.Count(k => k == id);
    private int DeathsOf(string id) => _deaths.Count(d => d.npc == id);
    private string? LastKillerOf(string id) =>
        _deaths.LastOrDefault(d => d.npc == id).killer;
    private int CorpsesOf(string id) =>
        _corpseService?.GetAllCorpses().Count(c => c.NpcId == id) ?? 0;

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        _enemyKilledToken = _enemyKilledSub?.Subscribe((in EnemyKilledEvent e) =>
        {
            _kills.Add(e.EnemyId);
            GD.Print($"[Stage5Sim] ← EnemyKilledEvent: {e.EnemyId}");
        });
        _npcDeathToken = _npcDeathSub?.Subscribe((in NPCDeathEvent e) =>
        {
            _deaths.Add((e.NpcId, e.KillerId ?? "?"));
            GD.Print($"[Stage5Sim] ← NPCDeathEvent: {e.NpcId} (killer={e.KillerId})");
        });
        _playerDeathToken = _playerDeathSub?.Subscribe((in PlayerDeathEvent _) =>
        {
            _playerDeaths++;
        });

        GD.Print("[Stage5Sim] Ready — Этап-5: тело=истина смерти / сплит сердца / DoT-зачёт, старт через 2с");
        _ = RunSequenceAsync();
    }

    public override void _ExitTree()
    {
        _enemyKilledToken?.Dispose(); _enemyKilledToken = null;
        _npcDeathToken?.Dispose(); _npcDeathToken = null;
        _playerDeathToken?.Dispose(); _playerDeathToken = null;
    }

    private async System.Threading.Tasks.Task RunSequenceAsync()
    {
        await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);

        if (_bodyService == null || _bodyProvider == null || _damageService == null
            || _npcSpawner == null || _npcService == null || _animalService == null
            || _buffService == null || _quests == null || _combatService == null
            || _corpseService == null || _playerService == null || _damagePub == null
            || _enemyKilledToken == null || _npcDeathToken == null)
        {
            GD.Print("[Stage5Sim] VERDICT: FAIL — DI not wired");
            return;
        }

        bool pass = true;
        var pp = _playerService.Position;

        // ── 0. Квест-сценарий BUG-017: реестр + старт «Охоты на волков» ──
        if (_quests.GetQuestStatus("quest_kill_wolves") == QuestStatus.NotStarted
            && !_quests.StartQuest("quest_kill_wolves"))
        {
            GD.Print("[Stage5Sim] VERDICT: FAIL — quest_kill_wolves не зарегистрирован/не стартует");
            return;
        }
        GD.Print("[Stage5Sim] 0-квест: quest_kill_wolves Active");

        // ── 1. BUG-016: сплит урона по способностям части (NPC, реальный
        //       fan-out DamageAppliedEvent → BodyService) ──────────────────
        string npcA = _npcSpawner.SpawnNPC("human", NPCRole.Passerby, 1,
            new Position2D(pp.X + 3, pp.Y + 3), 77101);
        var heartA = Part(_bodyProvider, npcA, BodyPartType.Heart);
        var torsoA = Part(_bodyProvider, npcA, BodyPartType.Torso);
        pass &= heartA != null && torsoA != null;

        // Сердце 10: красная −10 (полный урон), чёрная остаётся 0.
        // NOTE: BodyPart — живая ссылка из провайдера; сравниваем СНИМКИ
        // значений до/после удара (не ссылки).
        int heartRedA = heartA!.CurrentRedHP;
        _damagePub.Publish(Hit("player_0", npcA, 10, BodyPartType.Heart));
        await Wait(0.2);
        var heartA2 = Part(_bodyProvider, npcA, BodyPartType.Heart)!;
        bool heartSplitOk = heartA2.CurrentRedHP == heartRedA - 10
            && heartA2.CurrentBlackHP == 0 && heartA2.MaxBlackHP == 0;
        GD.Print($"[Stage5Sim] 1-сердце-10: red {heartRedA}→{heartA2.CurrentRedHP} " +
                 $"(−10, полный), black={heartA2.CurrentBlackHP} → {(heartSplitOk ? "OK" : "FAIL")}");
        pass &= heartSplitOk;

        // Торс 10: сплит 70/30 сохранён (−7 красной, −3 чёрной).
        int torsoRedA = torsoA!.CurrentRedHP;
        int torsoBlackA = torsoA.CurrentBlackHP;
        _damagePub.Publish(Hit("player_0", npcA, 10, BodyPartType.Torso));
        await Wait(0.2);
        var torsoA2 = Part(_bodyProvider, npcA, BodyPartType.Torso)!;
        bool torsoSplitOk = torsoA2.CurrentRedHP == torsoRedA - 7
            && torsoA2.CurrentBlackHP == torsoBlackA - 3;
        GD.Print($"[Stage5Sim] 1-торс-10: red {torsoRedA}→{torsoA2.CurrentRedHP} (−7), " +
                 $"black {torsoBlackA}→{torsoA2.CurrentBlackHP} (−3) → {(torsoSplitOk ? "OK (70/30)" : "FAIL")}");
        pass &= torsoSplitOk;
        pass &= _bodyProvider.IsEntityAlive(npcA);

        // ── 2. BUG-015: удар 50 в голову НЕ убивает (аудиторский пример) ──
        int headRedA = Part(_bodyProvider, npcA, BodyPartType.Head)!.CurrentRedHP;
        int headBlackA = Part(_bodyProvider, npcA, BodyPartType.Head)!.CurrentBlackHP;
        int kills0 = _kills.Count; int deaths0 = _deaths.Count;
        _damagePub.Publish(Hit("player_0", npcA, 50, BodyPartType.Head));
        await Wait(0.2);
        var headA2 = Part(_bodyProvider, npcA, BodyPartType.Head)!;
        bool noPrematureKill = headA2.CurrentRedHP == headRedA - 35 // 70/30: 50 → 35 red
            && headA2.CurrentBlackHP == headBlackA - 15
            && _bodyProvider.IsEntityAlive(npcA)
            && _kills.Count == kills0 && _deaths.Count == deaths0;
        GD.Print($"[Stage5Sim] 2-голова-50: red {headRedA}→{headA2.CurrentRedHP} " +
                 $"(−35, жив), kill/death-событий нет → {(noPrematureKill ? "OK (ложной смерти нет)" : "FAIL")}");
        pass &= noPrematureKill;

        // Повторный 50: голова red → 0 → СМЕРТЬ, подтверждена телом.
        _damagePub.Publish(Hit("player_0", npcA, 50, BodyPartType.Head));
        await Wait(0.3);
        bool killOnce = !_bodyProvider.IsEntityAlive(npcA)
            && KillsOf(npcA) == 1 && DeathsOf(npcA) == 1
            && LastKillerOf(npcA) == "player_0" && CorpsesOf(npcA) == 1;
        GD.Print($"[Stage5Sim] 2-голова-50×2: dead={!_bodyProvider.IsEntityAlive(npcA)}, " +
                 $"kills={KillsOf(npcA)}, deaths={DeathsOf(npcA)} (killer={LastKillerOf(npcA)}), " +
                 $"трупов={CorpsesOf(npcA)} → {(killOnce ? "OK (ровно один зачёт)" : "FAIL")}");
        pass &= killOnce;

        // ── 3. BUG-015+016: два удара по 50 в СЕРДЦЕ (80 HP) ──────────────
        string npcB = _npcSpawner.SpawnNPC("human", NPCRole.Passerby, 1,
            new Position2D(pp.X + 5, pp.Y + 1), 77102);
        var heartB = Part(_bodyProvider, npcB, BodyPartType.Heart)!;
        int heartRedB = heartB.CurrentRedHP;
        int killsB0 = _kills.Count;
        _damagePub.Publish(Hit("player_0", npcB, 50, BodyPartType.Heart));
        await Wait(0.2);
        var heartB2 = Part(_bodyProvider, npcB, BodyPartType.Heart)!;
        bool heartFirstHit = heartB2.CurrentRedHP == heartRedB - 50
            && _bodyProvider.IsEntityAlive(npcB) && _kills.Count == killsB0;
        GD.Print($"[Stage5Sim] 3-сердце-50: red {heartRedB}→{heartB2.CurrentRedHP} " +
                 $"(−50 полный, жив), зачёта нет → {(heartFirstHit ? "OK (порог ≥50 не убивает)" : "FAIL")}");
        pass &= heartFirstHit;

        _damagePub.Publish(Hit("player_0", npcB, 50, BodyPartType.Heart));
        await Wait(0.3);
        bool heartKill = !_bodyProvider.IsEntityAlive(npcB)
            && KillsOf(npcB) == 1 && DeathsOf(npcB) == 1 && CorpsesOf(npcB) == 1;
        GD.Print($"[Stage5Sim] 3-сердце-50×2: dead={!_bodyProvider.IsEntityAlive(npcB)}, " +
                 $"kills={KillsOf(npcB)}, трупов={CorpsesOf(npcB)} → {(heartKill ? "OK" : "FAIL")}");
        pass &= heartKill;

        // ── 4. BUG-015: консистентность реального DamageService ───────────
        //    Инвариант аудита: result.IsFatal == !IsEntityAlive ПОСЛЕ каждого
        //    удара (при любом ролле части/крита/брони); события смерти
        //    приходят только при фактической гибели. Гигантский добивающий
        //    удар (овекилл-каскад R41-1) гарантирует смерть — броня
        //    (минимум 1 урона DefenseProcessor) и конечности его не
        //    останавливают: избыток доходит до витальных.
        string npcC = _npcSpawner.SpawnNPC("human", NPCRole.Passerby, 1,
            new Position2D(pp.X + 6, pp.Y + 4), 77103);
        bool consistencyOk = true;
        for (int i = 0; i < 5 && _bodyProvider.IsEntityAlive(npcC); i++)
        {
            int killsC0 = KillsOf(npcC);
            var res = _damageService.CalculateDamage(Pure("player_0", npcC, 20));
            bool deadNow = !_bodyProvider.IsEntityAlive(npcC);
            bool stepOk = res.IsFatal == deadNow
                && KillsOf(npcC) == killsC0 + (deadNow ? 1 : 0)
                && DeathsOf(npcC) == (deadNow ? 1 : 0);
            GD.Print($"[Stage5Sim] 4-CalculateDamage #{i + 1}: final={res.FinalDamage} " +
                     $"part={res.HitPart} IsFatal={res.IsFatal} dead={deadNow} → {(stepOk ? "OK" : "FAIL")}");
            consistencyOk &= stepOk;
        }
        for (int i = 0; i < 4 && _bodyProvider.IsEntityAlive(npcC); i++)
        {
            int killsC0 = KillsOf(npcC);
            var res = _damageService.CalculateDamage(Pure("player_0", npcC, 3000));
            bool deadNow = !_bodyProvider.IsEntityAlive(npcC);
            bool stepOk = res.IsFatal == deadNow
                && KillsOf(npcC) == killsC0 + (deadNow ? 1 : 0)
                && DeathsOf(npcC) == (deadNow ? 1 : 0);
            GD.Print($"[Stage5Sim] 4-овекилл #{i + 1} (3000): final={res.FinalDamage}, " +
                     $"part={res.HitPart}, IsFatal={res.IsFatal}, dead={deadNow} → {(stepOk ? "OK" : "FAIL")}");
            consistencyOk &= stepOk;
        }
        bool consistencyDeath = !_bodyProvider.IsEntityAlive(npcC)
            && KillsOf(npcC) == 1 && DeathsOf(npcC) == 1 && CorpsesOf(npcC) == 1;
        GD.Print($"[Stage5Sim] 4-итог: dead={!_bodyProvider.IsEntityAlive(npcC)}, " +
                 $"kills={KillsOf(npcC)}, трупов={CorpsesOf(npcC)} → " +
                 $"{(consistencyOk && consistencyDeath ? "OK (IsFatal ⟺ тело)" : "FAIL")}");
        pass &= consistencyOk && consistencyDeath;

        // ── 5. BUG-017: DoT-килл волка = шаг квеста (реальная цепочка) ────
        int wolvesKilled = 0;
        var wolf1 = _animalService.SpawnAnimal("wolf", new Position2D(pp.X + 2, pp.Y + 6));
        bool poisonApplied = _buffService.ApplyBuff(wolf1.EntityId, "elemental_poison",
            9f, 99999f, sourceId: PlayerIdResolver.PlayerCanonical);
        GD.Print($"[Stage5Sim] 5-яд: ApplyBuff({wolf1.EntityId}, source=player)={poisonApplied}; жду тик ~1.2с");
        await Wait(1.4); // TickInterval яда = 1с (первый тик убивает: 99999 урона)

        int questProgress1 = WolfQuestProgress();
        bool dotKillOk = !wolf1.IsAlive
            && !_bodyProvider.IsEntityAlive(wolf1.EntityId)
            && KillsOf(wolf1.EntityId) == 1
            && DeathsOf(wolf1.EntityId) == 1
            && LastKillerOf(wolf1.EntityId) == PlayerIdResolver.PlayerCanonical // атрибуция, не «dot:…»
            && CorpsesOf(wolf1.EntityId) == 1
            && questProgress1 == 1;
        wolvesKilled += dotKillOk ? 1 : 0;
        GD.Print($"[Stage5Sim] 5-DoT-килл: dead={!wolf1.IsAlive}, kills={KillsOf(wolf1.EntityId)}, " +
                 $"killer={LastKillerOf(wolf1.EntityId)} (атрибуция!), трупов={CorpsesOf(wolf1.EntityId)}, " +
                 $"квест-прогресс={questProgress1}/3 → {(dotKillOk ? "OK (DoT зачитан квесту)" : "FAIL")}");
        pass &= poisonApplied && dotKillOk;

        // Добивание: два волка обычным ударом (единый домен смерти зверей) —
        // зачёт ровно по одному на волка, квест завершается на 3/3.
        var wolf2 = _animalService.SpawnAnimal("wolf", new Position2D(pp.X + 4, pp.Y + 6));
        var wolf3 = _animalService.SpawnAnimal("wolf", new Position2D(pp.X + 6, pp.Y + 6));
        _damagePub.Publish(Hit(PlayerIdResolver.PlayerCanonical, wolf2.EntityId, 80000, BodyPartType.Torso));
        await Wait(0.2);
        _damagePub.Publish(Hit(PlayerIdResolver.PlayerCanonical, wolf3.EntityId, 80000, BodyPartType.Torso));
        await Wait(0.3);

        bool wolves2Ok = KillsOf(wolf2.EntityId) == 1 && KillsOf(wolf3.EntityId) == 1
            && DeathsOf(wolf2.EntityId) == 1 && DeathsOf(wolf3.EntityId) == 1;
        wolvesKilled += wolves2Ok ? 2 : 0;
        bool questDone = _quests.GetQuestStatus("quest_kill_wolves") == QuestStatus.Completed;
        GD.Print($"[Stage5Sim] 5-добивание: kills w2={KillsOf(wolf2.EntityId)} w3={KillsOf(wolf3.EntityId)}, " +
                 $"квест={(questDone ? "Completed (3/3)" : $"прогресс {WolfQuestProgress()}/3")} → " +
                 $"{(wolves2Ok && questDone ? "OK (двойного зачёта нет)" : "FAIL")}");
        pass &= wolves2Ok && questDone && wolvesKilled == 3;

        // ── 6. BUG-015/017: реальный бой через CombatService ─────────────
        //    Смерть NPC завершает бой (Victory → EndCombat), зачёт убийства
        //    публикуется только доменом смерти (раньше — CombatService).
        string npcD = _npcSpawner.SpawnNPC("human", NPCRole.Passerby, 1,
            new Position2D(pp.X + 7, pp.Y + 2), 77104);
        var acceptance = _combatService.ExecuteAttack(PlayerIdResolver.PlayerCanonical,
            "basic_attack", npcD, isRanged: false, potencyPermil: 300000, isCharged: true);
        await Wait(0.4);
        bool combatOk = acceptance == AttackAcceptance.Accepted
            && !_bodyProvider.IsEntityAlive(npcD)
            && !_combatService.IsInCombat
            && KillsOf(npcD) == 1 && DeathsOf(npcD) == 1 && CorpsesOf(npcD) == 1;
        GD.Print($"[Stage5Sim] 6-реальный бой: accepted={acceptance}, dead={!_bodyProvider.IsEntityAlive(npcD)}, " +
                 $"бой завершён={!_combatService.IsInCombat}, kills={KillsOf(npcD)}, трупов={CorpsesOf(npcD)} " +
                 $"→ {(combatOk ? "OK (единственный зачёт, бой закрыт)" : "FAIL")}");
        pass &= combatOk;

        // ── 7. Игрок: удар 50 в голову не убивает; сплит сердца 100% ──────
        HealPlayerFully();
        int playerDeaths0 = _playerDeaths;
        var head = PlayerPart(BodyPartType.Head)!;
        var heart = PlayerPart(BodyPartType.Heart)!;
        var torso = PlayerPart(BodyPartType.Torso)!;

        _bodyService.ApplyDamage(BodyPartType.Head, 50, "qa_stage5_att");
        var head2 = PlayerPart(BodyPartType.Head)!;
        bool playerHeadOk = head2.CurrentRedHP == head.CurrentRedHP - 35
            && head2.CurrentBlackHP == head.CurrentBlackHP - 15
            && _bodyProvider.IsEntityAlive("player")
            && _playerDeaths == playerDeaths0;
        GD.Print($"[Stage5Sim] 7-игрок-голова-50: red {head.CurrentRedHP}→{head2.CurrentRedHP} (−35), " +
                 $"black −15, жив, PlayerDeathEvent={_playerDeaths - playerDeaths0} → " +
                 $"{(playerHeadOk ? "OK (поражения нет)" : "FAIL")}");
        pass &= playerHeadOk;

        _bodyService.ApplyDamage(BodyPartType.Heart, 10, "qa_stage5_att");
        var heart2 = PlayerPart(BodyPartType.Heart)!;
        bool playerHeartOk = heart2.CurrentRedHP == heart.CurrentRedHP - 10
            && heart2.CurrentBlackHP == 0
            && _bodyProvider.IsEntityAlive("player");
        GD.Print($"[Stage5Sim] 7-игрок-сердце-10: red {heart.CurrentRedHP}→{heart2.CurrentRedHP} (−10 " +
                 $"полный), black=0, жив → {(playerHeartOk ? "OK (BUG-016)" : "FAIL")}");
        pass &= playerHeartOk;

        _bodyService.ApplyDamage(BodyPartType.Torso, 10, "qa_stage5_att");
        var torso2 = PlayerPart(BodyPartType.Torso)!;
        bool playerTorsoOk = torso2.CurrentRedHP == torso.CurrentRedHP - 7
            && torso2.CurrentBlackHP == torso.CurrentBlackHP - 3
            && _bodyProvider.IsEntityAlive("player")
            && _playerDeaths == playerDeaths0;
        GD.Print($"[Stage5Sim] 7-игрок-торс-10: red −7, black −3 (сплит 70/30), PlayerDeathEvent=0 → " +
                 $"{(playerTorsoOk ? "OK" : "FAIL")}");
        pass &= playerTorsoOk;

        GD.Print($"[Stage5Sim] Итог: kills-всего={_kills.Count}, deaths-всего={_deaths.Count}, " +
                 $"playerDeaths={_playerDeaths}");
        GD.Print($"[Stage5Sim] VERDICT: {(pass
            ? "PASS — BUG-015/016/017: тело=истина смерти, сплит сердца 100%, DoT-зачёт квесту + атрибуция"
            : "FAIL")}");
    }

    // === Helpers ===

    private Godot.SignalAwaiter Wait(double seconds) =>
        ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private static DamageAppliedEvent Hit(string attacker, string target, int damage,
        BodyPartType part) =>
        new(attacker, target, damage, DamageType.Qi, Element.Neutral, part,
            CombatAttackResult.Hit);

    private static DamageRequest Pure(string attacker, string target, int damage) =>
        new(attacker, target, damage,
            DamageType.Pure, Element.Neutral, Element.Neutral,
            AttackType.Normal, TechniqueGrade.Common, 1000, 1, 1,
            DefenseSubtype.None, BodyMaterial.Organic,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            Morphology.Humanoid, 10, false, CombatSubtype.None);

    private static BodyPart? Part(IBodyDataProvider provider, string entityId, BodyPartType type) =>
        provider.GetBodyParts(entityId).FirstOrDefault(p => p.Type == type);

    private BodyPartData PlayerPart(BodyPartType type) =>
        _bodyService!.GetAllParts().FirstOrDefault(p => p.Type == type);

    private int WolfQuestProgress()
    {
        var s = _quests?.GetQuestSummaries().FirstOrDefault(q => q.QuestId == "quest_kill_wolves");
        return s?.Objectives.Length > 0 ? s.Objectives[0].Progress : -1;
    }

    private void HealPlayerFully()
    {
        if (_bodyService == null) return;
        foreach (var p in _bodyService.GetAllParts())
            _bodyService.HealPart(p.Type, 10000);
        GD.Print($"[Stage5Sim] игрок вылечен: HP={_bodyProvider?.GetCurrentHealth("player")}");
    }
}
