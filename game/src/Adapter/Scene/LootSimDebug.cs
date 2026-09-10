#nullable enable
// Создано: 2026-09-10 — R13 FULL-LOOT: headless-верификация (GODOT_LOOT_DEBUG=1).
//
// Проверяет три фичи задачи:
//   1. «NPC спаун через генерацию»: состав населения сгенерирован
//      NPCSpawnCompositionService (не хардкод-массив) — роли РАЗНООБРАЗНЫ,
//      целевая численность > 0, состав детерминирован от сида локации.
//   2. «Доступ к инвентарю мёртвого NPC»: смерть → CorpseService создаёт
//      труп-контейнер (экипировка + карманы + духовные камни), LootWindow
//      показывает строки, ЛКМ-взятие SlotId-адресно, повторный клик по
//      устаревшему слоту ОТКАЗЫВАЕТСЯ (TOCTOU-защита).
//   3. «Full loot»: LootAll забирает ВСЁ (инвентарь игрока реально растёт),
//      труп удаляется; ReinforcementTick восполняет популяцию (мир живёт).
//      + TTL: RemoveOldCorpses убирает старые трупы.
//
// Запуск: GODOT_NEWGAME=1 GODOT_LOOT_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn
// Паттерн — KillFeedSimDebug (env-хук, public QA-поля, VERDICT в конце).
using Godot;
using System.Linq;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Events;
using CultivationGame.Adapter.Di;
using CultivationGame.Adapter.UI;
using CultivationGame.Adapter.Scene;
using CultivationGame.Modules.NPC;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Headless-верификация R13 FULL-LOOT: спаун через генерацию, трупы-контейнеры,
/// обыск по SlotId, full loot, TTL, респаун популяции.
/// Итог: [LootSim] VERDICT: PASS/FAIL.
/// </summary>
public partial class LootSimDebug : Node
{
    [Inject] private INPCService? _npcService;
    [Inject] private ICorpseService? _corpses;
    [Inject] private IInventoryService? _inventory;
    [Inject] private IPublisher<Core.Messaging.Contracts.NPCDeathEvent>? _deathPub;
    [Inject] private NPCSpawnCompositionService? _composition;

    private GameWorldController? _world;

    public override async void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        GD.Print($"[LootSim] diag: npc={_npcService != null} corpses={_corpses != null} " +
                 $"inv={_inventory != null} pub={_deathPub != null} comp={_composition != null}");

        if (_npcService == null || _corpses == null || _inventory == null || _deathPub == null)
        {
            GD.Print("[LootSim] VERDICT: FAIL — DI injection failed");
            return;
        }

        _world = GetParent() as GameWorldController ?? FindWorld(GetTree().Root);
        await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);

        if (_world == null)
        {
            GD.Print("[LootSim] VERDICT: FAIL — GameWorldController not found");
            return;
        }

        bool pass = true;

        // === 1. Спаун через генерацию =======================================
        var npcIds = _npcService.GetAllNPCIds().ToArray();
        var roles = npcIds.Select(id => _npcService.GetNPCState(id)?.Role).Distinct().Count();
        int targetPop = _composition?.TargetPopulation ?? 0;
        GD.Print($"[LootSim] step1 generated population: {npcIds.Length} NPC, {roles} distinct roles, " +
                 $"targetPop={targetPop}");
        if (npcIds.Length < 5) { GD.Print("[LootSim] step1 FAIL: слишком мало NPC"); pass = false; }
        if (roles < 3) { GD.Print("[LootSim] step1 FAIL: роли не разнообразны (хардкод?)"); pass = false; }
        if (targetPop <= 0) { GD.Print("[LootSim] step1 FAIL: TargetPopulation не установлен"); pass = false; }

        // Детерминизм: тот же сид → тот же размер состава.
        var loc = new CultivationGame.Core.Data.LocationData
        {
            Id = "qa_loc", Seed = 12345, DangerLevel = 0,
            LocationType = CultivationGame.Core.Data.LocationType.Farm,
            Width = 50, Height = 50,
        };
        var comp1 = _composition!.GenerateStartup(loc);
        var comp2 = _composition.GenerateStartup(loc);
        bool deterministic = comp1.Count == comp2.Count
            && comp1.Zip(comp2).All(p => p.First.Role == p.Second.Role && p.First.Level == p.Second.Level);
        GD.Print($"[LootSim] step1b determinism: {(deterministic ? "OK" : "FAIL")} ({comp1.Count} requests)");
        if (!deterministic) { pass = false; }

        // === 2. Смерть → труп-контейнер ====================================
        string npcId = npcIds[0];
        var state = _npcService.GetNPCState(npcId);
        string npcName = state?.DisplayName ?? "Существо";
        // Как NPCCombatAdapter: IsAlive=false перед событием.
        if (state != null) { state.IsAlive = false; state.CurrentHealth = 0; }
        _deathPub.Publish(new Core.Messaging.Contracts.NPCDeathEvent(npcId, "player_0"));
        await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);

        if (_corpses.CorpseCount == 0)
        { GD.Print("[LootSim] step2 FAIL: труп не создан"); pass = false; }

        var corpse = _corpses.GetAllCorpses().FirstOrDefault(c => c.NpcId == npcId);
        if (corpse == null)
        { GD.Print("[LootSim] step2 FAIL: труп NPC не найден"); pass = false; }
        else
        {
            GD.Print($"[LootSim] step2 corpse '{corpse.DisplayName}': {corpse.ItemCount} записей " +
                     $"(equip={corpse.Items.Count(i => i.Source == "equipment")}, " +
                     $"inv={corpse.Items.Count(i => i.Source == "inventory")}, " +
                     $"stones={corpse.Items.Count(i => i.Source == "spirit_stones")})");
            if (corpse.ItemCount < 2) { GD.Print("[LootSim] step2 FAIL: лут подозрительно мал"); pass = false; }

            // === 3. LootWindow: содержимое =================================
            var lootWindow = _world.LootWindowForQA;
            if (lootWindow == null) { GD.Print("[LootSim] step3 FAIL: окно не найдено"); pass = false; }
            else
            {
                lootWindow.Open(corpse.CorpseId);
                await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
                GD.Print($"[LootSim] step3 window: rows={lootWindow.CorpseRowCount}, " +
                         $"header='{lootWindow.DebugHeaderText}', lootAll='{lootWindow.DebugLootAllText}', " +
                         $"footer='{lootWindow.DebugFooterText}'");
                if (lootWindow.CorpseRowCount != corpse.ItemCount)
                { GD.Print("[LootSim] step3 FAIL: строк не совпадает с предметами"); pass = false; }
                if (!lootWindow.DebugHeaderText.Contains(corpse.DisplayName))
                { GD.Print("[LootSim] step3 FAIL: имя трупа не в шапке"); pass = false; }
                if (!lootWindow.DebugLootAllText.Contains(corpse.ItemCount.ToString()))
                { GD.Print("[LootSim] step3 FAIL: счётчик в кнопке не сходится"); pass = false; }

                // === 4. SlotId-взятие + TOCTOU =============================
                long invBefore = TotalUnits();
                var firstItem = corpse.Items[0];
                int corpseBefore = corpse.ItemCount;
                lootWindow.HandleTake(firstItem.SlotId);
                await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
                GD.Print($"[LootSim] step4 take-by-slot: items {corpseBefore}→{corpse.ItemCount}, " +
                         $"inv units {invBefore}→{TotalUnits()}");
                if (corpse.ItemCount != corpseBefore - 1)
                { GD.Print("[LootSim] step4 FAIL: предмет не снят"); pass = false; }
                if (TotalUnits() <= invBefore)
                { GD.Print("[LootSim] step4 FAIL: инвентарь игрока не вырос"); pass = false; }

                // Повторное взятие ТОГО ЖЕ SlotId → отказ (TOCTOU-защита).
                lootWindow.HandleTake(firstItem.SlotId);
                await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
                bool doubleTake = corpse.ItemCount == corpseBefore - 1; // не изменился повторно
                GD.Print($"[LootSim] step4b double-take refused: {(doubleTake ? "OK" : "FAIL")}");
                if (!doubleTake) { pass = false; }

                // === 5. FULL LOOT ============================================
                lootWindow.Close();
                long invBeforeAll = TotalUnits();
                int corpseItemsBefore = corpse.ItemCount;
                int taken = _corpses.LootAll(corpse.CorpseId);
                await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
                GD.Print($"[LootSim] step5 full-loot: taken={taken}/{corpseItemsBefore}, " +
                         $"inv units {invBeforeAll}→{TotalUnits()}, corpseCount={_corpses.CorpseCount}");
                if (taken != corpseItemsBefore || taken == 0)
                { GD.Print("[LootSim] step5 FAIL: не всё взято"); pass = false; }
                if (TotalUnits() <= invBeforeAll)
                { GD.Print("[LootSim] step5 FAIL: инвентарь не вырос после full loot"); pass = false; }
                if (_corpses.GetCorpse(corpse.CorpseId) != null)
                { GD.Print("[LootSim] step5 FAIL: труп не удалён после обыска"); pass = false; }
            }
        }

        // === 6. TTL =========================================================
        // Новая смерть → RemoveOldCorpses: проверка пути удаления.
        // maxAge=-1: возраст (now - diedAt ≥ 0) всегда > -1 → удаляет всё
        // (детерминированно, без ожидания игрового времени).
        if (npcIds.Length > 1)
        {
            string npcId2 = npcIds[1];
            var st2 = _npcService.GetNPCState(npcId2);
            if (st2 != null) st2.IsAlive = false;
            _deathPub.Publish(new Core.Messaging.Contracts.NPCDeathEvent(npcId2, "old_age"));
            await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
            int beforeTtl = _corpses.CorpseCount;
            int removed = _corpses.RemoveOldCorpses(-1f); // возраст ≥ 0 > -1 → все удаляются
            GD.Print($"[LootSim] step6 ttl: {beforeTtl}→{_corpses.CorpseCount} (removed {removed})");
            if (_corpses.CorpseCount != 0 || removed != beforeTtl)
            { GD.Print("[LootSim] step6 FAIL: TTL не убрал трупы"); pass = false; }
        }

        // === 7. Респаун популяции («спаун через генерацию», живой мир) ======
        if (_composition != null)
        {
            // Цель: живых СТРОГО НИЖЕ floor (порог включения респауна).
            var all = _npcService.GetAllNPCIds().ToArray();
            int floor = System.Math.Max(1, (int)System.Math.Ceiling(targetPop * 0.6));
            int aliveNow = 0;
            foreach (var id in all) if (_npcService.IsAlive(id)) aliveNow++;
            // Доводим живых до floor-1 (убиваем сверх порога).
            int needKill = aliveNow - (floor - 1);
            foreach (var id in all)
            {
                if (needKill <= 0) break;
                var st = _npcService.GetNPCState(id);
                if (st == null || !st.IsAlive) continue;
                st.IsAlive = false;
                needKill--;
            }
            int aliveBefore = 0;
            foreach (var id in all) if (_npcService.IsAlive(id)) aliveBefore++;
            int reinforcBefore = _composition.ReinforcementCount;
            // forceCheck=true — детерминированный обход 45с-интервала (QA).
            string? newNpc = _composition.ReinforcementTick(forceCheck: true);
            await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
            GD.Print($"[LootSim] step7 reinforcement: alive={aliveBefore}/{targetPop} (floor={floor}), " +
                     $"spawned={newNpc != null}, total reinforc={_composition.ReinforcementCount}");
            if (aliveBefore < floor && _composition.ReinforcementCount == reinforcBefore)
            { GD.Print("[LootSim] step7 FAIL: популяция ниже порога, но респаун не случился"); pass = false; }
        }

        GD.Print($"[LootSim] VERDICT: {(pass ? "PASS — full-loot: спаун-генерация/трупы/обыск/full-loot/TTL/респаун" : "FAIL")}");
    }

    private long TotalUnits()
    {
        long total = 0;
        var slots = _inventory?.GetAllSlots();
        if (slots == null) return 0;
        foreach (var s in slots) total += s.Count;
        return total;
    }

    private static GameWorldController? FindWorld(Node node)
    {
        if (node is GameWorldController world) return world;
        foreach (var child in node.GetChildren())
        {
            var found = FindWorld(child);
            if (found != null) return found;
        }
        return null;
    }
}
