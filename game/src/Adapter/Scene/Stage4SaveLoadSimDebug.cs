#nullable enable
// Создано: 2026-10-20 — Этап-4 аудита (сохранение/загрузка): headless-верификация
// ПОЛНОГО GameSession.LoadGame-пути — не только агрегатор (QA-требование
// аудита: «включая неуспешную загрузку через полный GameSession»).
//
// Этап-4 проверяет 4 фикса:
//   P1  «неудачная загрузка уничтожает сессию»:
//     a) pre-flight Validate ДО сброса доменов — повреждённый/неполный/
//        неподдерживаемый слот отвергается, БЕЗ единого касания мира;
//     b) транзакционная загрузка — сбой RestoreState ПОСЛЕ сброса откатывается
//        (повторный сброс + восстановление из in-memory снимка): игрок
//        ВОЗВРАЩАЕТСЯ В СВОЙ МИР (state=Playing), а не в главное меню;
//   P1  «полнота формата»: v2-каталог обязательных блоков — сейв без
//     quests/npc_groups (новые обязательные) отвергается ДО сброса;
//   BUG-013 «спавнер не видит восстановленных NPC»: ActiveNPCCount == реестр,
//     DespawnNPC восстановленного NPC — полный путь очистки, лимит честный;
//   GAP-014 «группы не переживают сейв»: состав/лидер/маршрут/тип задачи
//     группы восстанавливаются из блока npc_groups.
//
// Сценарий (GODOT_NEWGAME=1 GODOT_STAGE4_DEBUG=1):
//   0. Сессия Playing (мир собран); чистим QA-слоты.
//   1. Маркеры: камни в инвентарь + QA-группа (лидер+участник, маршрут 3 точки)
//      → Save слота A (v2). Инвариант спавнера: трекер == реестр (NewGame).
//   2. Мутации ПОСЛЕ сейва: камни → 0, QA-группа расформирована.
//   3. Pre-flight: битые слоты (без quests / без npc_groups / версия из
//      будущего) → RejectedByPrecheck, State=Playing, мир НЕ тронут
//      (камни по-прежнему 0, группы по-прежнему нет).
//   4. Откат: слот проходит pre-flight, но npc-блок ломается в RestoreState
//      (bodyPartCount > длины массивов) → RolledBack, State=Playing,
//      МИР ДО-ЗАГРУЗКИ ВОССТАНОВЛЕН (камни 0, группы нет — НЕ из сейва!).
//   5. Успех: LoadGame(A) → Success, State=Playing; камни == сейв;
//      QA-группа восстановлена (id/лидер/состав/маршрут); спавнер
//      синхронизирован (ActiveNPCCount == реестр, восстановленные в трекере).
//   6. DespawnNPC восстановленного NPC → реестр/трекер/провайдеры чисты.
//   7. Новый спавн после загрузки работает (лимит считает восстановленных).
//
// Запуск: GODOT_NEWGAME=1 GODOT_STAGE4_DEBUG=1 \
//   godot --headless --path . scenes/MainMenu.tscn
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Headless-верификация Этапа-4: транзакционная загрузка через полный
/// GameSession + синхронизация спавнера + персистентность групп NPC.
/// Итог: [Stage4Sim] VERDICT: PASS/FAIL.
/// </summary>
public partial class Stage4SaveLoadSimDebug : Node
{
    private const string SlotA = "qa_stage4_world";
    private const string StoneId = "material_stone";

    [Inject] private IGameSession? _session;
    [Inject] private ISaveService? _save;
    [Inject] private IInventoryService? _inventory;
    [Inject] private IItemDatabaseService? _itemDb;
    [Inject] private INPCService? _npcs;
    [Inject] private INPCSpawnerService? _spawner;
    [Inject] private INPCGroupService? _groups;
    private IResolver? _resolver;

    public override async void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
        {
            ContainerAdapter.InjectProperties(this, container);
            _resolver = container.Resolve<Core.DI.IResolver>();
        }

        GD.Print("[Stage4Sim] Ready — Этап-4: полный GameSession.LoadGame-путь, старт через 2с");
        await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);

        if (_session == null || _save == null || _inventory == null || _itemDb == null
            || _npcs == null || _spawner == null || _groups == null || _resolver == null)
        {
            GD.Print("[Stage4Sim] VERDICT: FAIL — DI not wired");
            return;
        }
        if (_session.State != SessionState.Playing)
        {
            GD.Print($"[Stage4Sim] VERDICT: FAIL — сессия не Playing (state={_session.State}); нужен GODOT_NEWGAME=1");
            return;
        }

        bool pass = true;
        var problems = new List<string>();
        string savesRoot = ProjectSettings.GlobalizePath("user://saves");

        // Чистые QA-слоты (могли остаться от прошлого прогона).
        CleanupSlots(savesRoot);

        // ── 1. Маркеры мира + Save слота A ─────────────────────────────
        if (!_itemDb.TryGetItem(StoneId, out var stone) || stone == null)
        {
            GD.Print("[Stage4Sim] VERDICT: FAIL — material_stone нет в ItemDatabase");
            return;
        }
        int stones0 = _inventory.GetItemCount(StoneId);
        _inventory.TryAddItem(stone, 5);
        int stonesSaved = _inventory.GetItemCount(StoneId);
        Require(stonesSaved == stones0 + 5, "1-камни-в-сейв",
            $"TryAddItem(5): {stones0}→{stonesSaved} — сейв без маркера камней нетривиален", problems);

        // QA-группа: лидер + участник из состава мира, маршрут 3 точки.
        var worldNpcIds = _npcs.GetAllNPCIds();
        if (worldNpcIds.Count < 2)
        {
            GD.Print($"[Stage4Sim] VERDICT: FAIL — в мире {worldNpcIds.Count} NPC (нужно ≥2 для QA-группы)");
            return;
        }
        string qaLeader = worldNpcIds[0];
        string qaMember = worldNpcIds[1];
        string qaGroupId = _groups.CreateGroup(GroupTaskType.Patrol, faction: "stage4_qa");
        _groups.AddMember(qaGroupId, qaLeader, GroupRole.Leader);
        _groups.AddMember(qaGroupId, qaMember, GroupRole.Follower);
        _groups.SetPatrolRoute(qaGroupId, new List<Position2D>
        {
            new(10, 10), new(20, 10), new(20, 20),
        });
        int groupsSaved = _groups.GroupCount;
        var qaGroupSaved = _groups.GetGroup(qaGroupId);
        Require(qaGroupSaved != null && qaGroupSaved.LeaderId == qaLeader
                && qaGroupSaved.MemberIds.Contains(qaMember) && qaGroupSaved.PatrolRoute?.Count == 3,
            "1-QA-группа", $"группа '{qaGroupId}' кривая (лидер/состав/маршрут)", problems);

        // Инвариант спавнера ДО сейва (NewGame: трекер == реестр).
        var npcSvc = _resolver.Resolve<Modules.NPC.NPCService>();
        int registry0 = npcSvc.GetAllStates().Count;
        int tracker0 = _spawner.ActiveNPCCount;
        Require(tracker0 == registry0, "1-инвариант-спавнера",
            $"NewGame: трекер {tracker0} ≠ реестр {registry0} — предусловие BUG-013-теста нарушено", problems);

        bool savedA = _save.Save(new SaveSlot(SlotA, SaveSlotType.Manual));
        Require(savedA, "1-Save", $"Save('{SlotA}') → {savedA}", problems);
        GD.Print($"[Stage4Sim] 1. маркеры: камни {stones0}+5→{stonesSaved}, QA-группа '{qaGroupId}' " +
                 $"(лидер '{qaLeader}', участник '{qaMember}', маршрут 3тц), NPC в мире={worldNpcIds.Count}; Save(A)={savedA}");

        // ── 2. Мутации ПОСЛЕ сейва (мир ≠ сохранённому) ────────────────
        bool removed = _inventory.TryRemoveItem(StoneId, stonesSaved);
        int stonesMutated = _inventory.GetItemCount(StoneId);
        _groups.DisbandGroup(qaGroupId);
        int groupsMutated = _groups.GroupCount;
        bool groupGone = _groups.GetGroup(qaGroupId) == null;
        Require(removed && stonesMutated == 0, "2-камни-мутация",
            $"после Save: камни {stonesSaved}→{stonesMutated} (remove={removed}) — мутация не сработала, reject-проверки тривиальны", problems);
        Require(groupGone && groupsMutated == groupsSaved - 1, "2-группа-мутация",
            $"DisbandGroup: {groupsSaved}→{groupsMutated}, get={groupGone} — мутация не сработала", problems);
        GD.Print($"[Stage4Sim] 2. мутации после Save: камни {stonesSaved}→{stonesMutated}, группы {groupsSaved}→{groupsMutated}");

        // ── 3. P1 pre-flight: битые слоты отвергаются ДО сброса мира ───
        string validJson = ReadSlotJson(savesRoot, SlotA);
        Require(validJson.Length > 0, "3-подготовка", "main.json слота A не читается", problems);

        // 3a: v2 без обязательного блока quests (ядро было и в v1 — но в v2
        //     каталог полный; аудит Этап-4 требовал полноту).
        var noQuests = JsonNode.Parse(validJson)!;
        noQuests.AsObject().Remove("quests");
        WriteRawSlot(savesRoot, SlotA + "_no_quests", noQuests.ToJsonString());

        // 3b: v2 без npc_groups — блок GAP-014 обязан быть обязательным.
        var noGroups = JsonNode.Parse(validJson)!;
        noGroups.AsObject().Remove("npc_groups");
        WriteRawSlot(savesRoot, SlotA + "_no_groups", noGroups.ToJsonString());

        // 3c: версия из будущего.
        var futureVer = JsonNode.Parse(validJson)!;
        futureVer["save_meta"]!["version"] = Modules.Save.SaveDataAggregator.CurrentFormatVersion + 99;
        WriteRawSlot(savesRoot, SlotA + "_future", futureVer.ToJsonString());

        bool s3 = true;
        foreach (var (suffix, expectError) in new[]
                 {
                     ("_no_quests", "quests"), ("_no_groups", "npc_groups"), ("_future", "новее"),
                 })
        {
            string slot = SlotA + suffix;
            _session.LoadGame(new SaveSlot(slot, SaveSlotType.Manual));
            var outcome = _session.LastLoadOutcome;
            bool rejected = outcome == LoadGameOutcome.RejectedByPrecheck
                            && _session.State == SessionState.Playing
                            && (_save.LastError ?? "").Contains(expectError);
            // Мир НЕ тронут: мутации шага 2 живы.
            bool untouched = _inventory.GetItemCount(StoneId) == 0
                             && _groups.GetGroup(qaGroupId) == null;
            s3 &= rejected && untouched;
            GD.Print($"[Stage4Sim] 3. slot '{suffix}': outcome={outcome}, state={_session.State}, " +
                     $"ошибка='{_save.LastError?.Truncate(110)}', мир нетронут={untouched}");
            if (!rejected)
                problems.Add($"3-pre-flight '{suffix}': outcome={outcome} (нужно RejectedByPrecheck), state={_session.State}");
            if (!untouched)
                problems.Add($"3-pre-flight '{suffix}': мир ТРОНУТ при reject — потеря прогресса!");
        }
        Require(s3, "3-pre-flight-полный",
            "битые слоты: reject до сброса не работает (см. строки выше)", problems);

        // ── 4. P1 откат: RestoreState падает ПОСЛЕ сброса → мир возвращается ──
        // Слот проходит pre-flight (все блоки на месте, версия валидна), но
        // npc-блок ломается ПРИ ВОССТАНОВЛЕНИИ: bodyPartCount > длины массивов
        // → IndexOutOfRange в NPCService.RestoreState → Load=false → откат.
        var corrupt = JsonNode.Parse(validJson)!;
        var entries = corrupt["npc"]!["entries"]!.AsArray();
        bool corrupted = false;
        foreach (var entry in entries)
        {
            var types = entry!["bodyPartTypes"]?.AsArray();
            if (types != null && types.Count > 0)
            {
                entry["bodyPartCount"] = types.Count + 50; // выход за границы массивов
                corrupted = true;
                break;
            }
        }
        Require(corrupted, "4-подготовка", "не найдено npc-записи с bodyPartTypes для порчи", problems);
        string corruptSlot = SlotA + "_corrupt_restore";
        WriteRawSlot(savesRoot, corruptSlot, corrupt.ToJsonString());

        _session.LoadGame(new SaveSlot(corruptSlot, SaveSlotType.Manual));
        var outcome4 = _session.LastLoadOutcome;
        // КЛЮЧЕВОЙ инвариант: мир — НЕ из сейва, а ДО-ЗАГРУЗОЧНЫЙ (мутации живы):
        // камни 0, QA-группы нет. Преже сброс доменов успевал уничтожить мир.
        bool rollbackWorldIntact = _session.State == SessionState.Playing
                                   && outcome4 == LoadGameOutcome.RolledBack
                                   && _inventory.GetItemCount(StoneId) == 0
                                   && _groups.GetGroup(qaGroupId) == null;
        // Спавнер синхронизирован и после отката (IPostLoadSync в ветке rollback).
        int registry4 = npcSvc.GetAllStates().Count;
        int tracker4 = _spawner.ActiveNPCCount;
        bool rollbackSyncOk = tracker4 == registry4;
        GD.Print($"[Stage4Sim] 4. corrupt-restore: outcome={outcome4}, state={_session.State}, " +
                 $"мир до-загрузочный={_inventory.GetItemCount(StoneId) == 0 && _groups.GetGroup(qaGroupId) == null}, " +
                 $"спавнер {tracker4}==реестр {registry4}={rollbackSyncOk}");
        Require(rollbackWorldIntact, "4-откат-мира",
            $"outcome={outcome4}, state={_session.State}, камни={_inventory.GetItemCount(StoneId)} " +
            $"(нужно 0 — мир ДО загрузки, не из сейва)", problems);
        Require(rollbackSyncOk, "4-откат-синхронизация",
            $"после отката трекер {tracker4} ≠ реестр {registry4}", problems);

        // ── 5. Успешная загрузка слота A через полный GameSession ──────
        _session.LoadGame(new SaveSlot(SlotA, SaveSlotType.Manual));
        var outcome5 = _session.LastLoadOutcome;
        int stones5 = _inventory.GetItemCount(StoneId);
        var group5 = _groups.GetGroup(qaGroupId);
        bool groupRestored = group5 != null
                             && group5.LeaderId == qaLeader
                             && group5.MemberIds.Contains(qaMember)
                             && group5.MemberIds.Count == 2
                             && group5.PatrolRoute?.Count == 3
                             && group5.TaskType == GroupTaskType.Patrol
                             && _groups.GetGroupByMember(qaMember)?.GroupId == qaGroupId;
        bool loadOk = outcome5 == LoadGameOutcome.Success
                      && _session.State == SessionState.Playing
                      && stones5 == stonesSaved
                      && groupRestored;
        GD.Print($"[Stage4Sim] 5. Load(A): outcome={outcome5}, state={_session.State}, " +
                 $"камни {stonesMutated}→{stones5} (== сейвовым {stonesSaved}: {stones5 == stonesSaved}), " +
                 $"QA-группа восстановлена={groupRestored} (лидер '{group5?.LeaderId}', участников {group5?.MemberIds.Count}, маршрут {group5?.PatrolRoute?.Count})");
        Require(loadOk, "5-успешная-загрузка",
            $"outcome={outcome5}, state={_session.State}, камни={stones5}≠{stonesSaved}, группа={groupRestored}", problems);

        // BUG-013: спавнер синхронизирован с восстановленным реестром.
        int registry5 = npcSvc.GetAllStates().Count;
        int tracker5 = _spawner.ActiveNPCCount;
        var spawnedIds5 = _spawner.GetSpawnedNPCIds();
        var alive5 = _npcs.GetAllNPCIds();
        bool restoredTracked = alive5.All(id => spawnedIds5.Contains(id));
        bool syncOk = outcome5 == LoadGameOutcome.Success
                      && tracker5 == registry5
                      && restoredTracked;
        GD.Print($"[Stage4Sim] 5b. BUG-013: спавнер {tracker5} == реестр {registry5} " +
                 $"(IPostLoadSync), восстановленные живые NPC в трекере={restoredTracked} " +
                 $"(живых {alive5.Count}, трекер {spawnedIds5.Count})");
        Require(syncOk, "5b-BUG-013-синхронизация",
            $"после загрузки трекер {tracker5} ≠ реестр {registry5} или живые NPC не в трекере " +
            $"(в реестре, но не в трекере: [{string.Join(", ", alive5.Where(id => !spawnedIds5.Contains(id)).Take(3))}])", problems);

        // ── 6. BUG-013: штатный DespawnNPC восстановленного NPC ────────
        string despawnId = alive5.FirstOrDefault()
                           ?? spawnedIds5.FirstOrDefault() ?? "";
        if (despawnId.Length == 0)
        {
            problems.Add("6-despawn: в мире нет ни одного NPC для деспавна");
        }
        else
        {
            var equipProvider = _resolver.Resolve<IEquipmentDataProvider>();
            int trackerBefore = _spawner.ActiveNPCCount;
            _spawner.DespawnNPC(despawnId);
            bool despawned = _npcs.GetNPCState(despawnId) == null
                             && !_spawner.GetSpawnedNPCIds().Contains(despawnId)
                             && _spawner.ActiveNPCCount == trackerBefore - 1
                             // пер-entity данные сняты (провайдер больше не знает NPC)
                             && equipProvider.GetTotalArmor(despawnId) == 0;
            GD.Print($"[Stage4Sim] 6. BUG-013: DespawnNPC('{despawnId}') → реестр/трекер/провайдеры " +
                     $"чисты={despawned} (трекер {trackerBefore}→{_spawner.ActiveNPCCount})");
            Require(despawned, "6-despawn-восстановленного",
                "DespawnNPC восстановленного NPC не очистил реестр/трекер/провайдеры", problems);
        }

        // ── 7. Спавн после загрузки: лимит честно считает восстановленных ──
        // ActiveNPCCount теперь == реестру (шаг 5b) — гейт MaxActiveNPCs в
        // SpawnNPC читает ЭТО число; проверяем работоспособность спавна
        // и инкремент обоих счётчиков согласованным образом.
        var spawnPos = new Position2D(5, 5);
        string newNpc = _spawner.SpawnNPC("human", NPCRole.Passerby, 1, spawnPos, 20261020);
        bool spawnOk = !string.IsNullOrEmpty(newNpc)
                       && _npcs.GetNPCState(newNpc ?? "") != null
                       && _spawner.GetSpawnedNPCIds().Contains(newNpc ?? "")
                       && _spawner.ActiveNPCCount == npcSvc.GetAllStates().Count;
        GD.Print($"[Stage4Sim] 7. спавн после загрузки: '{newNpc}' → реестр+трекер согласованы={spawnOk} " +
                 $"(ActiveNPCCount={_spawner.ActiveNPCCount}, реестр={npcSvc.GetAllStates().Count})");
        Require(spawnOk, "7-спавн-после-загрузки",
            $"SpawnNPC после загрузки: id='{newNpc}', трекер {_spawner.ActiveNPCCount} vs реестр {npcSvc.GetAllStates().Count}", problems);
        if (!string.IsNullOrEmpty(newNpc))
            _spawner.DespawnNPC(newNpc); // чистка QA-NPC

        // ── Финал: чистка + вердикт ────────────────────────────────────
        CleanupSlots(savesRoot);
        _save.DeleteSave(new SaveSlot(SlotA, SaveSlotType.Manual));

        if (problems.Count > 0)
        {
            GD.Print($"[Stage4Sim] ПРОБЛЕМЫ ({problems.Count}):");
            foreach (var p in problems)
                GD.Print($"  • {p}");
        }
        pass &= problems.Count == 0;

        GD.Print(pass
            ? "[Stage4Sim] VERDICT: PASS — Этап-4: pre-flight до сброса (битые слоты не трогают мир), транзакционный откат (сбой RestoreState возвращает до-загрузочный мир), полнота v2 (quests/npc_groups обязательны), BUG-013 (спавнер==реестр после Load, деспавн/спавн восстановленных), GAP-014 (группы переживают сейв: лидер/состав/маршрут)"
            : "[Stage4Sim] VERDICT: FAIL — см. проблемы выше");

        // QA-режим: сим терминален.
        GetTree().Quit();
    }

    /// <summary>Integrity-контроль: condition обязана выполняться, иначе VERDICT FAIL.</summary>
    private static void Require(bool condition, string label, string detail, List<string> problems)
    {
        if (!condition)
            problems.Add($"{label}: {detail}");
    }

    private static string ReadSlotJson(string root, string slot)
        => File.ReadAllText(Path.Combine(root, slot, "main.json"));

    private static void WriteRawSlot(string root, string slot, string json)
    {
        var dir = Path.Combine(root, slot);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "main.json"), json);
    }

    private void CleanupSlots(string root)
    {
        foreach (var suffix in new[] { "", "_no_quests", "_no_groups", "_future", "_corrupt_restore" })
        {
            var dir = Path.Combine(root, SlotA + suffix);
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}

/// <summary>Локальное расширение для читаемых логов (усечение длинных строк ошибок).</summary>
internal static class Stage4StringExtensions
{
    public static string Truncate(this string? s, int max)
        => string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max] + "…");
}
