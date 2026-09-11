#nullable enable
// Создано: 2026-09-11 — аудит боя с животными: headless-верификация
// ПОЛНОГО контура игрок↔животное (GODOT_ANIMALQA_DEBUG=1).
//
// Жалоба пользователя: «Бегу с посохом за волком, пытаюсь его бить, а
// результата 0. Как будто не проходит регистрация урона.» Аудит нашёл 7
// дефектов (D1–D7); этот QA закрывает регресс-покрытием весь контур:
//   1. D1 — таргетинг: DebugFindNearestTarget видит волка (NPC ∪ животные);
//   2. D1 — урон: интент игрока по волку → HP волка падает (11-слойный
//      пайплайн через BodyService._entityBodyParts — тело волка зареги-
//      стрировано IBodyDataProvider);
//   3. D4 — месть: волк → hostile, ЧЕЙЗ + укус вплотную → HP игрока падает;
//   4. D2/D3 — смерть волка → NPCDeathEvent → CorpseService: труп-контейнер
//      «Волк» с духовными камнями (DEATH_AND_LOOT §5);
//   5. D6 — killfeed-имя: «Волк», не «???» (EventLogWindow.Name);
//   6. D4 — de-aggro: игрок телепортом уходит >5 тайлов → месть гаснет;
//   7. мирный вид: кролик после урона НЕ становится hostile (ANIMALS §5.5).
//
// Запуск: GODOT_NEWGAME=1 GODOT_ANIMALQA_DEBUG=1 \
//   godot --headless --path . scenes/MainMenu.tscn
// Паттерн — CombatAISimDebug (env-хук, DI-инъекция из GameBoot, VERDICT).
using Godot;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Adapter.Di;
using CultivationGame.Modules.NPC;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Headless-верификация боя с животными (волк: месть/смерть/труп;
/// кролик: мирный вид). Итог: [AnimalQA] VERDICT: PASS/FAIL.
/// </summary>
public partial class AnimalCombatSimDebug : Node2D
{
    [Inject] private IPublisher<AttackIntentEvent>? _attackIntentPub;
    [Inject] private ISubscriber<DamageAppliedEvent>? _damageSub;
    [Inject] private ISubscriber<PlayerPositionChangedEvent>? _playerPosSub;
    [Inject] private ISubscriber<CombatStartedEvent>? _combatStartedSub;
    [Inject] private ISubscriber<CombatEndedEvent>? _combatEndedSub;
    [Inject] private IPlayerService? _playerService;
    [Inject] private IBodyDataProvider? _bodyProvider;
    [Inject] private ICorpseService? _corpseService;
    [Inject] private Modules.Player.PlayerCombatAdapter? _combatAdapter;
    // Конкретный AnimalService — QA-API (IsHostile, GetAllAnimals, SpawnAnimal).
    [Inject] private Modules.NPC.AnimalService? _animalService;
    // Проверка завершения боя при de-aggro (AbandonCombat через disengage).
    [Inject] private CultivationGame.Modules.Combat.CombatService? _combatServiceImpl;

    private System.IDisposable? _damageToken;
    private System.IDisposable? _playerPosToken;
    private System.IDisposable? _combatStartedToken;
    private System.IDisposable? _combatEndedToken;
    private int _combatEndedCount;

    // GWC-родитель: телепорт игрока через DEBUG_TeleportPlayer (логика+визуал).
    private GameWorldController? _gwc;

    // HP игрока — под "player" (BodyService._entityId; CombatSimDebug-паттерн).
    private const string PlayerBodyId = "player";

    private int _hitsOnTrackedAnimal;
    private string? _trackedAnimalId;

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);
        _gwc = GetParent() as GameWorldController;

        GD.Print($"[AnimalQA] diag: intentPub={_attackIntentPub != null} dmgSub={_damageSub != null} " +
                 $"player={_playerService != null} body={_bodyProvider != null} corpse={_corpseService != null} " +
                 $"adapter={_combatAdapter != null} animals={_animalService != null}");

        _damageToken = _damageSub?.Subscribe((in DamageAppliedEvent e) =>
        {
            if (_trackedAnimalId != null && e.TargetId == _trackedAnimalId)
            {
                _hitsOnTrackedAnimal++;
                GD.Print($"[AnimalQA] dmg-событие: {e.Damage} урона ({e.Result}, часть {e.HitPart})");
            }
        });

        // Отладка отката позиции — снята (причина найдена: _PhysicsProcess-синк
        // GWC к _visualPosition; телепорт теперь через DEBUG_TeleportPlayer).

        // Отладка рестарта боя после de-aggro — diagnostic + счётчик концов.
        _combatStartedToken = _combatStartedSub?.Subscribe((in CombatStartedEvent e) =>
        {
            GD.Print($"[AnimalQA] combat-started: {e.InstigatorId} → {e.TargetId}");
        });
        _combatEndedToken = _combatEndedSub?.Subscribe((in CombatEndedEvent _) => _combatEndedCount++);

        GD.Print("[AnimalQA] Ready — сценарий через 2с");
        _ = RunScenarioAsync();
    }

    public override void _ExitTree()
    {
        _damageToken?.Dispose();
        _damageToken = null;
        _playerPosToken?.Dispose();
        _playerPosToken = null;
        _combatStartedToken?.Dispose();
        _combatStartedToken = null;
        _combatEndedToken?.Dispose();
        _combatEndedToken = null;
    }

    private async System.Threading.Tasks.Task RunScenarioAsync()
    {
        await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);

        if (_attackIntentPub == null || _playerService == null || _bodyProvider == null
            || _corpseService == null || _combatAdapter == null || _animalService == null
            || _combatServiceImpl == null)
        {
            GD.Print("[AnimalQA] FAIL — DI not wired");
            PrintVerdict(false);
            return;
        }

        var animals = _animalService.GetAllAnimals();
        AnimalEntity? wolf = null, rabbit = null, deer = null;
        foreach (var a in animals)
        {
            if (!a.IsAlive) continue;
            if (wolf == null && a.Species == "wolf") wolf = a;
            else if (rabbit == null && a.Species == "rabbit") rabbit = a;
            else if (deer == null && a.Species == "deer") deer = a;
        }
        if (wolf == null)
        {
            // Виды спавнятся равномерно, но пул может не дать волка —
            // досыпавливаем честным спауном (тот же сервис/тело).
            var pp = _playerService.Position;
            wolf = _animalService.SpawnAnimal("wolf", new Position2D(pp.X + 2, pp.Y + 2));
            GD.Print($"[AnimalQA] волка не было в композиции — доспавн {wolf.EntityId}");
        }
        _trackedAnimalId = wolf.EntityId;
        GD.Print($"[AnimalQA] волк: {wolf.EntityId} @ {wolf.Position}, HP={_bodyProvider.GetCurrentHealth(wolf.EntityId)}");

        // Игрока — вплотную к волку (как «бегу с посохом за волком»).
        // Телепорт — через GWC (логика + визуал): прямой SetPosition откатывается
        // _PhysicsProcess-синком к _visualPosition в тот же кадр.
        _gwc!.DEBUG_TeleportPlayer(wolf.Position.X + 1, wolf.Position.Y);
        GD.Print($"[AnimalQA] игрок телепортирован к волку: сейчас @ {_playerService.Position}");

        bool allOk = true;

        // === 1. D1: ТАГРЕТИНГ — Space-селектор видит волка ===
        string? found = _combatAdapter.DebugFindNearestTarget();
        bool targetingOk = found == wolf.EntityId;
        GD.Print($"[AnimalQA] 1-таргетинг: ближайшая цель Space = '{found}' " +
                 $"(ожидается волк {wolf.EntityId}) → {(targetingOk ? "OK" : "FAIL")}");
        allOk &= targetingOk;

        // === 2. D1: УРОН — интент игрока → HP волка падает ===
        int wolfHpBefore = _bodyProvider.GetCurrentHealth(wolf.EntityId);
        _hitsOnTrackedAnimal = 0;
        _attackIntentPub.Publish(new AttackIntentEvent(
            PlayerBodyId, wolf.EntityId, "basic_attack", false));
        // Каст ~0.5с + резолв — ждём до 3с, до первого попадания.
        float waited = 0f;
        while (waited < 3f && _hitsOnTrackedAnimal == 0)
        {
            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
            waited += 0.5f;
        }
        int wolfHpAfter = _bodyProvider.GetCurrentHealth(wolf.EntityId);
        bool damageOk = _hitsOnTrackedAnimal > 0 && wolfHpAfter < wolfHpBefore;
        GD.Print($"[AnimalQA] 2-урон: HP волка {wolfHpBefore} → {wolfHpAfter}, попаданий={_hitsOnTrackedAnimal} " +
                 $"→ {(damageOk ? "OK" : "FAIL")}");
        allOk &= damageOk;

        // === 3. D4: МЕСТЬ — волк hostile и кусает игрока ===
        bool hostileAfterHit = _animalService.IsHostile(wolf.EntityId);
        int playerHpBefore = _bodyProvider.GetCurrentHealth(PlayerBodyId);
        float waitedBite = 0f;
        while (waitedBite < 12f && _bodyProvider.GetCurrentHealth(PlayerBodyId) >= playerHpBefore)
        {
            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
            waitedBite += 0.5f;
        }
        int playerHpAfter = _bodyProvider.GetCurrentHealth(PlayerBodyId);
        bool retaliationOk = hostileAfterHit && playerHpAfter < playerHpBefore;
        GD.Print($"[AnimalQA] 3-месть: hostile={hostileAfterHit}, HP игрока {playerHpBefore} → {playerHpAfter} " +
                 $"(укус за {waitedBite:F1}с) → {(retaliationOk ? "OK" : "FAIL")}");
        allOk &= retaliationOk;

        // === 4. D2/D3: СМЕРТЬ — добиваем волка → труп «Волк» с камнями ===
        // ДЕТЕРМИНИРОВАННО: волк-«подранок» (Голова разбита — честное состояние
        // «долго били»: SetHP(0) по vital-части) + ОДИН заряженный удар через
        // РЕАЛЬНЫЙ пайплайн. Аудит вскрыл: смерть по «суммарный HP ≤ 0» при
        // per-part floor практически недостижима → волк был «бессмертен»;
        // теперь смерть = vital-разрушение (IsEntityAlive — единое правило тел).
        // Проверяем и честный Victory/EndCombat (CombatService detects death).
        int corpsesBefore = _corpseService.CorpseCount;
        bool combatActiveBeforeKill = _combatServiceImpl.IsInCombat;
        foreach (var p in _bodyProvider.GetBodyParts(wolf.EntityId))
        {
            if (p.Type == BodyPartType.Head) p.SetHP(0, p.CurrentBlackHP);
        }
        GD.Print($"[AnimalQA] 4-подранок: Голова волка разбита (HP тела={_bodyProvider.GetCurrentHealth(wolf.EntityId)}, " +
                 $"alive-по-правилам-тел={_bodyProvider.IsEntityAlive(wolf.EntityId)})");
        _attackIntentPub.Publish(new AttackIntentEvent(
            PlayerBodyId, wolf.EntityId, "basic_attack", false, potencyPermil: 100000, isCharged: true));
        await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);
        int corpsesAfter = _corpseService.CorpseCount;
        // Труп волка — последний в реестре (в этом прогоне умер только он).
        var allCorpses = _corpseService.GetAllCorpses();
        var wolfCorpse = allCorpses.Count > 0 ? allCorpses[allCorpses.Count - 1] : null;
        bool deathOk = !wolf.IsAlive && corpsesAfter == corpsesBefore + 1;
        bool corpseOk = deathOk && wolfCorpse != null
            && wolfCorpse.NpcId == wolf.EntityId
            && wolfCorpse.DisplayName == "Волк"
            && wolfCorpse.ItemCount >= 1
            && !_animalService.IsHostile(wolf.EntityId); // месть погашена смертью
        // CombatService обязан завершить бой (Victory) при смерти защитника.
        bool combatEndedOnKill = !_combatServiceImpl.IsInCombat;
        corpseOk &= combatEndedOnKill;
        GD.Print($"[AnimalQA] 4-смерть: бой был={combatActiveBeforeKill}, волк IsAlive={wolf.IsAlive}, " +
                 $"трупов {corpsesBefore}→{corpsesAfter}, DisplayName='{wolfCorpse?.DisplayName}', " +
                 $"записей лута={wolfCorpse?.ItemCount}, бой завершён={combatEndedOnKill} → {(corpseOk ? "OK" : "FAIL")}");
        allOk &= corpseOk;

        // === 5. D6: KILLFEED-ИМЯ — EventLogWindow резолвит «Волк» ===
        string feedName = _animalService.GetDisplayName(wolf.EntityId) ?? "???";
        bool nameOk = feedName == "Волк";
        GD.Print($"[AnimalQA] 5-killfeed-имя: '{feedName}' → {(nameOk ? "OK" : "FAIL")}");
        allOk &= nameOk;

        // === 6. D4: DE-AGGRO — игрок уходит >5 тайлов → месть гаснет,
        // бой завершается (CombatDisengageEvent → AbandonCombat) ===
        // (второй зверь: олень, при его отсутствии — доспавн волка; кролик
        // НЕ годится — мирный вид никогда не становится hostile)
        AnimalEntity? second = deer;
        if (second == null)
        {
            var pp = _playerService.Position;
            second = _animalService.SpawnAnimal("wolf", new Position2D(pp.X + 2, pp.Y));
        }
        _gwc!.DEBUG_TeleportPlayer(second.Position.X + 1, second.Position.Y);
        _attackIntentPub.Publish(new AttackIntentEvent(
            PlayerBodyId, second.EntityId, "basic_attack", false));
        // Резолв удара + возможный укус зверя — 3.5с (ходовая модель).
        await ToSignal(GetTree().CreateTimer(3.5), SceneTreeTimer.SignalName.Timeout);
        bool secondHostile = _animalService.IsHostile(second.EntityId);
        bool combatWasActive = _combatServiceImpl.IsInCombat;

        // Телепорт игрока в даль (карта 50×50 — угол противоположен).
        int endedBeforeTeleport = _combatEndedCount;
        _gwc!.DEBUG_TeleportPlayer(2, 2);
        float waitedCalm = 0f;
        while (waitedCalm < 8f && _animalService.IsHostile(second.EntityId))
        {
            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
            waitedCalm += 0.5f;
        }
        // Месть погашена + бой зверя завершён по disengage (CombatEndedEvent
        // после телепорта; глобальный IsInCombat НЕ проверяем — враждебный
        // человек-NPC у угла карты может легитимно начать СВОЙ бой с игроком).
        bool deAggroOk = secondHostile && !_animalService.IsHostile(second.EntityId)
                         && _combatEndedCount > endedBeforeTeleport;
        GD.Print($"[AnimalQA] 6-de-aggro: hostile после удара={secondHostile}, бой был={combatWasActive}, " +
                 $"после телепорта игрока в даль (за {waitedCalm:F1}с) hostile={_animalService.IsHostile(second.EntityId)}, " +
                 $"CombatEnded(+{_combatEndedCount - endedBeforeTeleport}) → {(deAggroOk ? "OK" : "FAIL")}");
        allOk &= deAggroOk;

        // === 7. МИРНЫЙ ВИД — кролик не мстит (ANIMALS §5.5) ===
        if (rabbit == null || !rabbit.IsAlive)
        {
            var pp = _playerService.Position;
            rabbit = _animalService.SpawnAnimal("rabbit", new Position2D(pp.X + 2, pp.Y + 2));
        }
        _gwc!.DEBUG_TeleportPlayer(rabbit.Position.X + 1, rabbit.Position.Y);
        _attackIntentPub.Publish(new AttackIntentEvent(
            PlayerBodyId, rabbit.EntityId, "basic_attack", false));
        await ToSignal(GetTree().CreateTimer(3.5), SceneTreeTimer.SignalName.Timeout);
        bool rabbitPeaceful = !_animalService.IsHostile(rabbit.EntityId);
        GD.Print($"[AnimalQA] 7-мирный-вид: кролик после урона hostile={_animalService.IsHostile(rabbit.EntityId)} " +
                 $"→ {(rabbitPeaceful ? "OK" : "FAIL")}");
        allOk &= rabbitPeaceful;

        PrintVerdict(allOk);
    }

    private static void PrintVerdict(bool pass) => GD.Print(pass
        ? "[AnimalQA] VERDICT: PASS — таргетинг/урон/месть/смерть→труп/killfeed-имя/de-aggro/мирный вид: бой с животными зарегистрирован полностью"
        : "[AnimalQA] VERDICT: FAIL — см. шаги выше (какой контур не прошёл)");
}
