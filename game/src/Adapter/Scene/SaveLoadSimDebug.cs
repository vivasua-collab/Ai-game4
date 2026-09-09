#nullable enable
// Создано: 2026-09-09 (R11, внешнее ревью Save/Load) — round-trip верификация
// persistence: изменить состояние → Save → полностью изменить → Load →
// сравнить состояние до Save и после Load.
//
// v2 (2026-09-09, дефект теста найден пользователем): в v1 «камни 10→10» и
// «слот5 ''→''» — мутации TryRemoveItem(StoneId, int.MaxValue) (транзакционный
// отказ: доступно меньше запрошенного) и AssignSlot(5, "qa_probe_technique")
// (валидация IsLearned отклоняет несуществующую технику) НЕ изменяли
// состояние, из-за чего проверки восстановления были ТРИВИАЛЬНЫ
// (неизменённое сравнивалось с неизменённым). Усиление:
//   • Мутации используют РЕАЛЬНЫЕ ресурсы: удаление ТОЧНОГО числа камней,
//     НАСТОЯЩАЯ изученная техника (TechniqueService.GetAllTechniques()).
//   • Integrity-контроль (анти-тривиальность): каждая мутация ОБЯЗАНА
//     изменить наблюдаемое состояние, иначе VERDICT: FAIL — равенство
//     «до/после Load» больше не может пройти вхолостую.
//   • Снимки CaptureState обязаны СОДЕРЖАТЬ маркеры мутаций (id камня,
//     id техники, id формации) — сейв зависит от мутаций, сравнение
//     нетривиально. Пустые блоки («{}») → FAIL.
//
// Проверяет (GODOT_SAVELOAD_DEBUG=1):
//   0. ResolveAll<ISaveable> ≥ 8 РАЗЛИЧНЫХ сервисов (DI-фикс R11: раньше
//      мульти-интерфейсные регистрации коллапсировали в SaveService ×7).
//   1. Мутации домена: инвентарь (камни), тело (урон), формация (активация),
//      слот техник (назначение изученной), зарядник (активация) — каждая
//      с integrity-контролем реальности изменения.
//   2. Снимки CaptureState каждого ISaveable (канонический JSON) + маркеры
//      содержимого + запрет пустых блоков.
//   3. Save → true + SaveCompletedEvent несёт РЕАЛЬНЫЙ результат.
//   4. ФАЙЛ: каждый зарегистрированный блок присутствует и НЕ пуст;
//      поля IncludeFields реально записаны (slots/parts/activeFormationId…).
//   5. Полная мутация состояния (отличная от сохранённого) + integrity.
//   6. Load → true.
//   7. Состояние после Load == до Save (json-сравнение снимков + домен:
//      камни/формация/слот/тело/зарядник).
//   8. Чистка слота.
// Запуск: GODOT_NEWGAME=1 GODOT_SAVELOAD_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Events;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Adapter.Di;
using CultivationGame.Modules.Save;

namespace CultivationGame.Adapter.Scene;

public partial class SaveLoadSimDebug : Node
{
    private const string SlotName = "qa_roundtrip";
    private const string StoneId = "material_stone";

    [Inject] private ISaveService? _saveService;
    [Inject] private IInventoryService? _inventory;
    [Inject] private IItemDatabaseService? _itemDb;
    [Inject] private IBodyService? _body;
    [Inject] private IFormationService? _formation;
    [Inject] private IChargerService? _charger;
    [Inject] private IResolver? _resolver;
    [Inject] private ISubscriber<SaveCompletedEvent>? _saveCompletedSub;

    private bool _lastSaveEventSuccess = true;
    private string? _lastSaveEventError;
    private IDisposable? _saveEventToken;

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        GD.Print("[SaveLoadSim] Ready — round-trip Save/Load тест (v2: реальные мутации + integrity) старт через 2с");
        _ = RunSequenceAsync();
    }

    public override void _ExitTree()
    {
        _saveEventToken?.Dispose();
    }

    /// <summary>Integrity-контроль: condition обязана выполняться, иначе проблема фиксируется и VERDICT падает.</summary>
    private static bool Require(bool condition, string label, string detail, List<string> problems)
    {
        if (!condition)
            problems.Add($"{label}: {detail}");
        return condition;
    }

    private async System.Threading.Tasks.Task RunSequenceAsync()
    {
        await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);

        if (_saveService == null || _inventory == null || _itemDb == null
            || _body == null || _formation == null || _charger == null || _resolver == null)
        {
            GD.Print("[SaveLoadSim] VERDICT: FAIL — DI not wired");
            return;
        }

        // Зонд реального результата SaveCompletedEvent (P1: раньше всегда true).
        _saveEventToken = _saveCompletedSub?.Subscribe((in SaveCompletedEvent e) =>
        {
            _lastSaveEventSuccess = e.Success;
            _lastSaveEventError = e.Error;
        });

        bool pass = true;
        var problems = new List<string>(); // integrity-нарушения (анти-тривиальность)

        // Чистый слот (мог остаться от прошлого прогона).
        _saveService.DeleteSave(new SaveSlot(SlotName, SaveSlotType.Manual));

        // === 0. Регистрация ISaveable (DI-фикс R11 P0) ================
        var saveables = _resolver.ResolveAll<ISaveable>().ToList();
        var distinct = saveables.Distinct().ToList();
        var keys = distinct.Select(s => s.SaveKey).ToList();
        bool registrationOk = distinct.Count >= 8
            && keys.Distinct().Count() == keys.Count
            && keys.Contains("formation"); // раньше терялся (перетирание ключа)
        GD.Print($"[SaveLoadSim] 0. ResolveAll<ISaveable>: {distinct.Count} различных (≥8), ключи [{string.Join(", ", keys)}], formation присутствует={keys.Contains("formation")}");
        pass &= registrationOk;

        // === 1. Мутации домена (РЕАЛЬНЫЕ, integrity-контроль) =========
        if (!_itemDb.TryGetItem(StoneId, out var stone) || stone == null)
        {
            GD.Print("[SaveLoadSim] VERDICT: FAIL — material_stone нет в ItemDatabase");
            return;
        }
        var slotSvc = _resolver.Resolve<Modules.Player.TechniqueSlotService>();
        var techniqueSvc = _resolver.Resolve<Modules.Combat.TechniqueService>();

        // 1a. Инвентарь: добавить камни (счётчик обязан вырасти).
        int stoneCount0 = _inventory.GetItemCount(StoneId);
        _inventory.TryAddItem(stone, 5);
        int stoneCount1 = _inventory.GetItemCount(StoneId);
        Require(stoneCount1 > stoneCount0, "add-камни",
            $"TryAddItem(5): {stoneCount0}→{stoneCount1} — счётчик не вырос, проверка была бы тривиальна", problems);

        // 1b. Тело: полное лечение → урон (ratio обязан упасть).
        var partTypes = _body.GetAllParts();
        var target = partTypes.Count > 0 ? partTypes[0] : default(BodyPartData);
        _body.HealPart(target.Type, int.MaxValue);
        float ratioBefore = _body.GetPartHealthRatio(target.Type);
        _body.ApplyDamage(target.Type, 25);
        float ratio1 = _body.GetPartHealthRatio(target.Type);
        Require(ratio1 < ratioBefore, "body-урон",
            $"{ratioBefore:0.00}→{ratio1:0.00} — урон не применился, проверка была бы тривиальна", problems);

        // 1c. Формация: StartDrawing (ActiveFormationId обязан стать непустым).
        _formation.StartDrawing("basic_barrier", "player");
        string formationId1 = _formation.ActiveFormationId ?? "";
        Require(formationId1.Length > 0, "формация",
            "ActiveFormationId пуст после StartDrawing — проверка была бы тривиальна", problems);

        // 1d. Слот техник: РЕАЛЬНАЯ изученная техника (валидация IsLearned).
        //     v1-дефект: «qa_probe_technique» не существует → AssignSlot
        //     молча отказывал → слот5 оставался «''» → сравнение тривиально.
        string techId = techniqueSvc?.GetAllTechniques().Keys.FirstOrDefault() ?? "";
        if (techId.Length == 0 && techniqueSvc != null)
        {
            // Fallback: изучить дешёвую боевую технику (Level = текущий →
            // окно резонанса проходит; subtype None валиден).
            bool learned = techniqueSvc.LearnTechnique("qa_roundtrip_tech",
                TechniqueType.Combat, TechniqueGrade.Common, CombatSubtype.None,
                qiCost: 100, cooldown: 1f);
            if (learned) techId = "qa_roundtrip_tech";
        }
        slotSvc.ClearSlot(5);
        bool assigned = slotSvc.AssignSlot(5, techId);
        string slot5Tech1 = slotSvc.GetTechniqueAtSlot(5) ?? "";
        Require(techId.Length > 0, "слот-техника",
            "нет ни одной изученной техники (fallback-изучение тоже не сработало)", problems);
        Require(assigned && slot5Tech1 == techId, "слот-назначение",
            $"AssignSlot(5, '{techId}') → '{slot5Tech1}' (назначение не прошло — проверка была бы тривиальна)", problems);

        // 1e. Зарядник: Activate (Mode обязан стать On).
        ChargerMode mode0 = _charger.Mode;
        _charger.Activate();
        ChargerMode mode1 = _charger.Mode;
        Require(mode1 == ChargerMode.On, "зарядник-вкл",
            $"Activate() → Mode={mode1} — постусловие нарушено", problems);

        bool step1Ok = problems.Count == 0;
        GD.Print($"[SaveLoadSim] 1. мутации ДО Save (integrity {(step1Ok ? "OK" : "FAIL")}): " +
                 $"камни {stoneCount0}+5→{stoneCount1}, HP {ratioBefore:0.00}→{ratio1:0.00}, " +
                 $"формация '{formationId1}', слот5 '{slot5Tech1}', зарядник {mode0}→{mode1}");
        pass &= step1Ok;

        // === 2. Снимки CaptureState + маркеры содержимого ==============
        var snapshot = new Dictionary<string, string>();
        foreach (var s in distinct)
        {
            if (s.SaveKey == "save_meta") continue; // timestamp изменяется всегда
            snapshot[s.SaveKey] = JsonSerializer.Serialize(s.CaptureState(), SaveJson.Options);
        }

        // Integrity: НИ один блок не пуст («{}» = состояние не пишется).
        foreach (var kv in snapshot)
            Require(kv.Value.Length > 5, $"блок '{kv.Key}'",
                "снимок пуст — состояние модуля не попадает в сейв", problems);

        // Integrity: снимки СОДЕРЖАТ маркеры мутаций → сейв зависит от
        // мутаций → round-trip-сравнение нетривиально.
        if (snapshot.TryGetValue("inventory", out var invJson))
            Require(invJson.Contains(StoneId), "маркер inventory",
                $"в снимке нет '{StoneId}' — сейв не отражает мутацию инвентаря", problems);
        if (snapshot.TryGetValue("technique_slots", out var slotJson))
            Require(slotJson.Contains(techId), "маркер technique_slots",
                $"в снимке нет '{techId}' — сейв не отражает назначение слота", problems);
        if (snapshot.TryGetValue("formation", out var formJson))
            Require(formJson.Contains(formationId1), "маркер formation",
                $"в снимке нет '{formationId1}' — сейв не отражает активацию формации", problems);
        if (snapshot.TryGetValue("body", out var bodyJson))
            Require(bodyJson.Contains("parts"), "маркер body",
                "в снимке нет поля parts — сейв не отражает состояние тела", problems);
        if (snapshot.TryGetValue("charger", out var chgJson))
            Require(chgJson.Contains("mode"), "маркер charger",
                "в снимке нет поля mode — сейв не отражает режим зарядника", problems);

        bool step2Ok = problems.Count == 0;
        GD.Print($"[SaveLoadSim] 2. снимки {snapshot.Count} блоков (integrity {(step2Ok ? "OK" : "FAIL")}): маркеры камень/техника/формация/части/зарядник присутствуют");
        pass &= step2Ok;

        // === 3. Save ===================================================
        bool saveOk = _saveService.Save(new SaveSlot(SlotName, SaveSlotType.Manual));
        GD.Print($"[SaveLoadSim] 3. Save('{SlotName}') → {saveOk}, SaveCompletedEvent: success={_lastSaveEventSuccess}, error={_lastSaveEventError ?? "-"}");
        pass &= saveOk;
        pass &= _lastSaveEventSuccess; // событие подтверждает успех (P1-фикс)

        // === 4. Файл: блоки присутствуют и НЕ пусты ====================
        var fileOk = InspectSaveFile(keys, out string fileReport);
        GD.Print($"[SaveLoadSim] 4. файл main.json: {fileReport}");
        pass &= fileOk;

        // === 5. Полная мутация (состояние ≠ сохранённому) =============
        // v1-дефект: TryRemoveItem(int.MaxValue) — транзакционный отказ
        // (доступно меньше запрошенного) → «камни 10→10», мутации НЕ БЫЛО.
        // v2: удаляем РОВНО доступное число и контролируем результат.
        int p5 = problems.Count;
        bool removed = _inventory.TryRemoveItem(StoneId, stoneCount1);
        int stoneCountMutated = _inventory.GetItemCount(StoneId);

        _body.HealPart(target.Type, int.MaxValue); // полное лечение
        float ratioHealed = _body.GetPartHealthRatio(target.Type);

        _formation.DeactivateFormation();
        string formationIdMutated = _formation.ActiveFormationId ?? "";

        slotSvc.ClearSlot(5);
        string slot5Mutated = slotSvc.GetTechniqueAtSlot(5) ?? "";

        _charger.Deactivate();
        ChargerMode modeMutated = _charger.Mode;

        Require(removed && stoneCountMutated == 0, "remove-камни",
            $"TryRemoveItem({stoneCount1}) → {removed}, счётчик {stoneCount1}→{stoneCountMutated} — мутация не сработала, проверка была бы тривиальна", problems);
        Require(ratioHealed > ratio1, "body-лечение",
            $"{ratio1:0.00}→{ratioHealed:0.00} — лечение не применилось, проверка была бы тривиальна", problems);
        Require(formationId1.Length > 0 && formationIdMutated.Length == 0, "формация-сброс",
            $"'{formationId1}'→'{formationIdMutated}' — сброс не сработал, проверка была бы тривиальна", problems);
        Require(slot5Tech1.Length > 0 && slot5Mutated.Length == 0, "слот-очистка",
            $"'{slot5Tech1}'→'{slot5Mutated}' — очистка не сработала, проверка была бы тривиальна", problems);
        Require(mode1 == ChargerMode.On && modeMutated == ChargerMode.Off, "зарядник-выкл",
            $"{mode1}→{modeMutated} — деактивация не сработала, проверка была бы тривиальна", problems);

        bool step5Ok = problems.Count == p5;
        GD.Print($"[SaveLoadSim] 5. мутации ПОСЛЕ Save (integrity {(step5Ok ? "OK" : "FAIL")}): " +
                 $"камни {stoneCount1}→{stoneCountMutated}, HP {ratio1:0.00}→{ratioHealed:0.00}, " +
                 $"формация '{formationId1}'→'{formationIdMutated}', слот5 '{slot5Tech1}'→'{slot5Mutated}', " +
                 $"зарядник {mode1}→{modeMutated}");
        pass &= step5Ok;

        // === 6. Load ===================================================
        bool loadOk = _saveService.Load(new SaveSlot(SlotName, SaveSlotType.Manual));
        GD.Print($"[SaveLoadSim] 6. Load('{SlotName}') → {loadOk}");
        pass &= loadOk;

        // === 7. Состояние восстановлено == до Save =====================
        // Теперь НЕ тривиально: перед Load состояние гарантированно отличалось.
        var restoreOk = new List<string>();
        foreach (var s in distinct)
        {
            if (s.SaveKey == "save_meta") continue;
            var jsonNow = JsonSerializer.Serialize(s.CaptureState(), SaveJson.Options);
            bool equal = jsonNow == snapshot[s.SaveKey];
            if (!equal)
                GD.Print($"[SaveLoadSim] 7. БЛОК '{s.SaveKey}' НЕ восстановлен:\n  до: {snapshot[s.SaveKey]}\n  после: {jsonNow}");
            restoreOk.Add($"{s.SaveKey}={(equal ? "OK" : "FAIL")}");
        }
        GD.Print($"[SaveLoadSim] 7. round-trip блоков: {string.Join(", ", restoreOk)}");
        pass &= restoreOk.All(x => x.EndsWith("OK"));

        // Доменные проверки (все значения нетривиальны благодаря шагам 1/5).
        int stoneCount2 = _inventory.GetItemCount(StoneId);
        string formationId2 = _formation.ActiveFormationId ?? "";
        string slot5Tech2 = slotSvc.GetTechniqueAtSlot(5) ?? "";
        float ratio2 = _body.GetPartHealthRatio(target.Type);
        ChargerMode mode2 = _charger.Mode;
        bool domainOk = stoneCount2 == stoneCount1
            && formationId2 == formationId1
            && slot5Tech2 == slot5Tech1
            && Math.Abs(ratio2 - ratio1) < 0.02f
            && mode2 == mode1;
        GD.Print($"[SaveLoadSim] 7b. домен: камни {stoneCount1}→{stoneCount2} (==), формация '{formationId1}'→'{formationId2}' (==), " +
                 $"слот5 '{slot5Tech1}'→'{slot5Tech2}' (==), HP-части {ratio1:0.00}→{ratio2:0.00} (≈), зарядник {mode1}→{mode2} (==)");
        pass &= domainOk;

        // === 8. Чистка =================================================
        _saveService.DeleteSave(new SaveSlot(SlotName, SaveSlotType.Manual));

        // Финальный integrity-гейт: если хоть одна мутация была нереальной,
        // равенства шага 7 ничего не доказывают.
        if (problems.Count > 0)
        {
            GD.Print($"[SaveLoadSim] INTEGRITY-НАРУШЕНИЯ ({problems.Count}) — мутации не были реальными, round-trip доказательной силы не имеет:");
            foreach (var p in problems)
                GD.Print($"  • {p}");
        }
        pass &= problems.Count == 0;

        GD.Print($"[SaveLoadSim] VERDICT: {(pass ? "PASS — Save/Load round-trip: 8 блоков, типизация, IncludeFields, честный success, РЕАЛЬНЫЕ мутации (анти-тривиальность)" : "FAIL")}");

        // QA-режим: сим терминален — завершаем процесс сразу.
        GetTree().Quit();
    }

    /// <summary>
    /// Проверка файла сейва: каждый зарегистрированный блок присутствует,
    /// является непустым JSON-объектом, и публичные ПОЛЯ реально записаны
    /// (IncludeFields): в inventory-блоке должно быть свойство slots,
    /// в body — parts, в formation — activeFormationId (раньше блоки
    /// писались как «{}» — поля System.Text.Json по умолчанию не пишет).
    /// </summary>
    private bool InspectSaveFile(List<string> expectedKeys, out string report)
    {
        report = "FAIL — файл не найден";
        try
        {
            string root = ProjectSettings.GlobalizePath("user://saves");
            string path = Path.Combine(root, SlotName, "main.json");
            if (!File.Exists(path))
            {
                report = $"FAIL — {path} не существует";
                return false;
            }

            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
            });

            var missing = new List<string>();
            var empty = new List<string>();
            foreach (var key in expectedKeys)
            {
                if (!doc.RootElement.TryGetProperty(key, out var block))
                {
                    missing.Add(key);
                    continue;
                }
                if (block.ValueKind == JsonValueKind.Object && !block.EnumerateObject().Any())
                    empty.Add(key);
            }

            // Поля IncludeFields: контрольные точки по блокам с полями.
            var fieldProbe = new List<string>();
            bool HasProp(JsonElement root, string block, string prop)
            {
                return root.TryGetProperty(block, out var b)
                    && b.ValueKind == JsonValueKind.Object
                    && b.EnumerateObject().Any(p => string.Equals(p.Name, prop, StringComparison.OrdinalIgnoreCase));
            }
            if (HasProp(doc.RootElement, "inventory", "slots")) fieldProbe.Add("inventory.slots");
            if (HasProp(doc.RootElement, "body", "parts")) fieldProbe.Add("body.parts");
            if (HasProp(doc.RootElement, "formation", "activeFormationId")) fieldProbe.Add("formation.activeFormationId");
            if (HasProp(doc.RootElement, "technique_slots", "slots")) fieldProbe.Add("technique_slots.slots");

            long size = new FileInfo(path).Length;
            bool ok = missing.Count == 0 && empty.Count == 0 && fieldProbe.Count >= 3;
            report = $"{(ok ? "OK" : "FAIL")} — {size} байт, блоки {doc.RootElement.EnumerateObject().Count()}/{expectedKeys.Count}"
                + (missing.Count > 0 ? $", ОТСУТСТВУЮТ: [{string.Join(", ", missing)}]" : "")
                + (empty.Count > 0 ? $", ПУСТЫЕ: [{string.Join(", ", empty)}]" : "")
                + $", поля-маркеры: [{string.Join(", ", fieldProbe)}]";
            return ok;
        }
        catch (Exception ex)
        {
            report = $"FAIL — {ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }
}
