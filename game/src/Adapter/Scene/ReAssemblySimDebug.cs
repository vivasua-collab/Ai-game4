#nullable enable
// Создано: 2026-09-08 — ревью-1 P1-3: headless-верификация ПОВТОРНОЙ сборки
// сцены в одном процессе (GODOT_REASSEMBLY_DEBUG=1).
//
// Проверяет контракт lifecycle оркестратора, который раньше не работал:
//   1. RESET: фазы — DI-синглтоны; после 1-й сборки их State=Completed.
//      Без Reset() в RunAssembly CanExecute()==false у всех фаз → вторая
//      сборка «пропустила» бы 15/15 фаз. Ожидание: все фазы Completed.
//   2. WORLD-SCOPED СБРОС РЕЕСТРА (WorldInitPhase): без Clear() реестр
//      техник накапливал бы техники прошлого мира (~x2). Ожидание: размер
//      после 2-й сборки ≈ размеру 1-й (не удвоился).
//   3. Повторный прогон не бросает исключений (GameSession сам ловит и
//      откатывает state — здесь зовём оркестратор напрямую).
//
// 2026-09-10 (аудит R14, P2-1): + NPC-домен — ассерты double-spawn.
//   4. NPC-РЕЕСТР: сброс домена (фаза 0; до R17 — NpcDomainResetPhase, с
//      R17 — WorldDomainResetPhase/IWorldResettable) + GameSession.LoadGame
//      — без сброса повторная сборка накапливает NPC прошлого мира поверх
//      новой композиции (рост реестра к cap, «призраки»). Ожидание:
//      alive-NPC после 2-й сборки ≈ композиции 1-й (не удвоился, не в ноль).
//   5. GHOST-NPC: убитый в мире 1 NPC не переживает пересборку.
//   6. TРУПЫ: труп мира 1 (создан QA-смертью) не переживает пересборку
//      (CorpseService.ResetWorld; RemoveInternal → CorpseRemovedEvent).
//   7. ГРУППЫ: реестр групп мира 1 (+QA-группа) не накапливается поверх
//      новой композиции (NPCGroupService.ResetWorld).
//
// 2026-09-15 (R17 «Полнота сейва», E-1/NPC-1/WT-5): + world-scoped домены.
//   8. ЗВЕРИ: лишний зверь мира 1 (QA-спаун поверх композиции) не переживает
//      пересборку; поголовье после 2-й сборки ≈ композиции (не удвоилось
//      и не ноль) — AnimalService.ResetWorld (IWorldResettable).
//   9. ВРЕМЯ: часы мира 2 = 06:00 дня 1 (TimeService.ResetWorld) — до R17
//      тёплая пересборка наследовала день/время прошлого мира (WT-5).
//
// Запуск: GODOT_NEWGAME=1 GODOT_REASSEMBLY_DEBUG=1 \
//   godot --headless --path . scenes/MainMenu.tscn
// Паттерн следует GODOT_KILLFEED_DEBUG (env-хук, DI-инъекция из GameBoot).
using Godot;
using System.Linq;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Core.DI;
using CultivationGame.Entry;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Headless-верификация повторной сборки мира (Reset фаз + сброс реестров
/// техник и NPC-домена). Итог: [ReAssemblySim] VERDICT: PASS/FAIL.
/// </summary>
public partial class ReAssemblySimDebug : Node
{
    private SceneOrchestrator? _orchestrator;
    private Modules.Generator.TechniqueRegistry? _registry;
    private INPCService? _npcService;
    private ICorpseService? _corpseService;
    private INPCGroupService? _groupService;
    private IPublisher<NPCDeathEvent>? _npcDeathPub;
    // R17: звери + время — world-scoped домены в контрактe IWorldResettable.
    private Modules.NPC.AnimalService? _animalService;
    private ITimeService? _timeService;

    public override async void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
        {
            ContainerAdapter.InjectProperties(this, container);
            _orchestrator = container.Resolve<SceneOrchestrator>();
            _registry = container.Resolve<Modules.Generator.TechniqueRegistry>();
            _npcService = container.Resolve<INPCService>();
            _corpseService = container.Resolve<ICorpseService>();
            _groupService = container.Resolve<INPCGroupService>();
            _npcDeathPub = container.Resolve<IPublisher<NPCDeathEvent>>();
            _animalService = container.Resolve<Modules.NPC.AnimalService>();
            _timeService = container.Resolve<ITimeService>();
        }

        GD.Print($"[ReAssemblySim] diag: orchestrator={_orchestrator != null} registry={_registry != null} " +
                 $"npc={_npcService != null} corpse={_corpseService != null} group={_groupService != null} " +
                 $"deathPub={_npcDeathPub != null} animals={_animalService != null} time={_timeService != null}");
        if (_orchestrator == null || _registry == null || _npcService == null
            || _corpseService == null || _groupService == null || _npcDeathPub == null
            || _animalService == null || _timeService == null)
        {
            GD.Print("[ReAssemblySim] VERDICT: FAIL — DI resolution failed");
            return;
        }

        // Даём первой сборке и хукам времени (TradeUX/Dialogue) утихнуть.
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);

        // === Снимок мира 1 ДО его «загрязнения» QA-артефактами ===
        int registryBefore = _registry.Count;
        int aliveBefore = _npcService.GetAllNPCIds().Count;
        int groupsBefore = _groupService.GroupCount;
        int corpsesBefore = _corpseService.CorpseCount;

        // === «Грязный» мир 1: труп (QA-смерть первого NPC) + QA-группа ===
        // Смерть честная: IsAlive=false ДО публикации (тот же контракт, что
        // NPCCombatAdapter/OnYearChanged) — иначе CorpseService её отбросит.
        string killedId = _npcService.GetAllNPCIds().FirstOrDefault() ?? "";
        bool corpseMade = false;
        if (!string.IsNullOrEmpty(killedId))
        {
            var st = _npcService.GetNPCState(killedId);
            if (st != null)
            {
                st.CurrentHealth = 0;
                st.IsAlive = false;
                st.IsInCombat = false;
                _npcDeathPub.Publish(new NPCDeathEvent(killedId, "reassembly_qa"));
                corpseMade = _corpseService.CorpseCount > corpsesBefore;
            }
        }
        // QA-группа поверх композиции мира 1 (паттерн GroupSpawnPhase).
        _groupService.CreateGroup(GroupTaskType.Patrol, "reassembly_qa");
        int groupsDirty = _groupService.GroupCount;

        // R17 (NPC-1): QA-зверь поверх композиции мира 1 — «грязь» для
        // проверки сброса животного домена (SpawnAnimal не проверяет
        // проходимость — координаты безопасны).
        int animalsBefore = _animalService.GetAllAnimals().Count;
        var qaAnimal = _animalService.SpawnAnimal("wolf", new Position2D(2, 2));
        string qaAnimalId = qaAnimal.EntityId;
        int animalsDirty = _animalService.GetAllAnimals().Count;

        // R17 (WT-5): «грязное» время — мир 1 тикал ≥ 0.5с (await выше),
        // пересборка обязана вернуть 06:00 дня 1.
        var dirtyTime = _timeService.CurrentTime;

        var resolver = container!.Resolve<Core.DI.IResolver>();
        var phases = resolver.ResolveAll<ISceneAssemblyPhase>().OrderBy(p => p.Order).ToList();
        int totalPhases = phases.Count;

        // === ВТОРАЯ сборка в том же процессе (как «меню → NewGame» повторно) ===
        try
        {
            _orchestrator.RunAssembly(System.Threading.CancellationToken.None,
                SceneAssemblyMode.NewGame).GetAwaiter().GetResult();
        }
        catch (System.Exception ex)
        {
            GD.Print($"[ReAssemblySim] VERDICT: FAIL — повторная сборка бросила {ex.GetType().Name}: {ex.Message}");
            return;
        }

        // Проверка 1: все фазы выполнились (Reset отработал, никто не Skipped).
        int completed = phases.Count(p => p.State == SceneAssemblyPhaseState.Completed);
        int skipped = phases.Count(p => p.State == SceneAssemblyPhaseState.Skipped);
        int failed = phases.Count(p => p.State == SceneAssemblyPhaseState.Failed);

        // Проверка 2: реестр техник НЕ удвоился (world-scoped Clear в WorldInitPhase).
        int registryAfter = _registry.Count;
        bool registrySane = registryAfter <= (registryBefore + registryBefore / 2 + 10);

        // Проверка 4 (R14-аудит P2-1): NPC-реестр НЕ удвоился (NpcDomainResetPhase)
        // и НЕ выродился в ноль (сброс есть — спавн-фазы 6/7/8 обязаны наполнить
        // новый мир). Толерантность — как у реестра техник: композиция RNG.
        int aliveAfter = _npcService.GetAllNPCIds().Count;
        bool npcSane = aliveAfter <= (aliveBefore + aliveBefore / 2 + 10)
                       && aliveAfter >= aliveBefore / 2;

        // Проверка 5 (R14-аудит P2-1): ghost-NPC — убитый в мире 1 не пережил сборку.
        bool ghostGone = string.IsNullOrEmpty(killedId) || _npcService.GetNPCState(killedId) == null;

        // Проверка 6 (R13/R14-аудит): труп мира 1 не пережил сборку; спавн-фазы
        // трупов не создают — после сброса реестр обязан быть пуст.
        int corpsesAfter = _corpseService.CorpseCount;
        bool corpsesSane = corpsesAfter == 0;

        // Проверка 7 (R14-аудит P2-1): группы мира 1 (композиция + QA-группа)
        // не накопились поверх новой композиции. Fresh-композиция ≈ groupsBefore;
        // без Reset было бы ≈ 2×groupsBefore+1.
        int groupsAfter = _groupService.GroupCount;
        bool groupsSane = groupsAfter <= (groupsBefore + groupsBefore / 2 + 2)
                          && groupsAfter >= 1;

        // Проверка 8 (R17 NPC-1): звери — QA-зверь не пережил пересборку,
        // поголовье ≈ композиции (не удвоилось и не ноль). Композиция = фаза 6
        // (3-5) + GroupSpawnPhase-звери (волчья стая) — потому не хардкодим
        // диапазон, а сверяем с before (паттерн NPC-проверки 4).
        int animalsAfter = _animalService.GetAllAnimals().Count;
        bool qaAnimalGone = _animalService.GetAllAnimals().All(a => a.EntityId != qaAnimalId);
        bool animalsSane = animalsAfter <= (animalsBefore + animalsBefore / 2 + 2)
                           && animalsAfter >= animalsBefore / 2
                           && animalsAfter < animalsDirty;

        // Проверка 9 (R17 WT-5): часы мира 2 = 06:00 дня 1 — TimeService
        // в IWorldResettable-сбросе (до R17 наследовали dirty-время мира 1).
        var timeAfter = _timeService.CurrentTime;
        bool timeSane = timeAfter.Year == GameConstants.START_YEAR
                        && timeAfter.Month == 1 && timeAfter.Day == 1
                        && timeAfter.Hour == 6;

        GD.Print($"[ReAssemblySim] phases: total={totalPhases} completed={completed} " +
                 $"skipped={skipped} failed={failed}; registry: before={registryBefore} after={registryAfter}");
        GD.Print($"[ReAssemblySim] npc-domain: alive before={aliveBefore} after={aliveAfter} " +
                 $"→ {(npcSane ? "OK" : "FAIL (double-spawn/over-wipe)")}; ghost '{killedId}' " +
                 $"→ {(ghostGone ? "OK" : "FAIL (пережил сборку)")}; corpse made={corpseMade} " +
                 $"after={corpsesAfter} → {(corpsesSane ? "OK" : "FAIL (труп пережил сборку)")}; " +
                 $"groups before={groupsBefore} dirty={groupsDirty} after={groupsAfter} " +
                 $"→ {(groupsSane ? "OK" : "FAIL (накопление групп)")}");
        GD.Print($"[ReAssemblySim] R17-domains: animals before={animalsBefore} dirty={animalsDirty} " +
                 $"after={animalsAfter} → {(animalsSane ? "OK" : "FAIL (double-spawn/over-wipe)")}; " +
                 $"qa-animal '{qaAnimalId}' → {(qaAnimalGone ? "OK" : "FAIL (пережил сборку)")}; " +
                 $"time dirty={dirtyTime} after={timeAfter} → {(timeSane ? "OK (06:00 дня 1)" : "FAIL (время пережило сборку)")}");

        // QA-харнесс валиден только если «грязь» мира 1 реально создана:
        // без трупа/QA-группы/QA-зверя проверки 5–8 вырождаются в always-true.
        bool harnessOk = corpseMade && groupsDirty == groupsBefore + 1
                         && animalsDirty == animalsBefore + 1;

        bool pass = completed == totalPhases && totalPhases > 0
                    && skipped == 0 && failed == 0 && registrySane
                    && npcSane && ghostGone && corpsesSane && groupsSane
                    && animalsSane && qaAnimalGone && timeSane && harnessOk;
        GD.Print(pass
            ? "[ReAssemblySim] VERDICT: PASS — Reset фаз работает (все фазы переисполнены), реестры world-scoped: техники/NPC/трупы/группы/звери/время не переживают пересборку, double-spawn отсутствует"
            : "[ReAssemblySim] VERDICT: FAIL — см. значения выше (skipped>0 = Reset не отработал; registry/npc/animals x2 = Clear/ResetWorld не отработал; ghost/corpse/qa-animal = домен пережил сборку; time = WT-5 не сброшен; harness=false = QA-грязь не создана)");
    }
}
