#nullable enable
#if DEBUG
// Создано: 2026-09-23 — R37-c (баг-репорт пользователя 23.09):
// «не могу добавить камни Ци в зарядник, лекарство добавляется без проблем».
//
// Полный путь бага: генерация зарядника (L3+) → экипировка в слот пояса →
// гейт пояса (зарядник ≠ пояс) → окно H → вставка камня (мост) → буфер
// растёт → Ци практику → контракт анти-дюпа (полный/частичный/пустой) →
// сейв round-trip (MaxQi камня из моста) → пояс-контроль (armor_belt).
//
// Запуск (headless, вердикт):
//   GODOT_NEWGAME=1 GODOT_CHARGERQA_DEBUG=1 \
//     tools/run_godot.sh --headless --path game scenes/MainMenu.tscn
using Godot;
using System;
using System.Linq;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

public partial class ChargerSimDebug : Node
{
    [Inject] private IInventoryService? _inventory;
    [Inject] private IItemDatabaseService? _itemDb;
    [Inject] private IEquipmentService? _equipment;
    [Inject] private IItemGeneratorService? _itemGen;
    [Inject] private IEquipmentGenerator? _equipGen;
    [Inject] private IQiService? _qi;
    [Inject] private Modules.Charger.ChargerItemBridge? _bridge;
    [Inject] private Modules.Charger.ChargerService? _charger;
    [Inject] private Modules.Inventory.BeltService? _belt;

    private const string StoneId = "qistone_dust_calm";

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        GD.Print("[ChargerSim] Ready — тест зарядника старт через 2с");
        _ = RunSequenceAsync();
    }

    private async System.Threading.Tasks.Task RunSequenceAsync()
    {
        await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);

        var world = GetParent() as GameWorldController ?? FindWorld(GetTree().Root);
        if (world == null)
        {
            GD.Print("[ChargerSim] VERDICT: FAIL — GameWorldController not found");
            return;
        }
        if (_inventory == null || _itemDb == null || _equipment == null || _itemGen == null || _equipGen == null
            || _qi == null || _bridge == null || _charger == null || _belt == null)
        {
            GD.Print("[ChargerSim] VERDICT: FAIL — DI not wired");
            return;
        }

        bool pass = true;

        // === A. Инфраструктура (prefix-дефекты: отсутствовали ДО фикса) ===
        bool a1 = Godot.InputMap.HasAction("charger_window");
        GD.Print($"[ChargerSim] A1. InputMap action charger_window (H): {a1} — {(a1 ? "OK" : "DEFECT")}");
        pass &= a1;

        var window = world.ChargerWindowForQA;
        bool a2 = window != null;
        GD.Print($"[ChargerSim] A2. Окно ChargerWindow создано: {a2} — {(a2 ? "OK" : "DEFECT")}");
        pass &= a2;

        // === B. Камни в инвентарь + гейт «не надет» (раньше — молчание) ===
        Modules.Generator.QiStoneSeeder.Seed(_itemDb);
        var stoneDef = _itemDb.TryGetItem(StoneId, out var stoneItem) ? stoneItem : null;
        bool a3 = stoneDef is QiStoneData;
        GD.Print($"[ChargerSim] A3. Камень Ци сидирован ({StoneId}): {a3} — {(a3 ? "OK" : "DEFECT")}");
        pass &= a3;
        if (stoneDef == null) { GD.Print("[ChargerSim] VERDICT: FAIL"); return; }

        _inventory.TryAddItem(stoneDef, 3);
        int stonesBefore = _inventory.GetItemCount(StoneId);
        GD.Print($"[ChargerSim] B0. Камней в инвентаре: {stonesBefore} (стартовый набор + 3 добавленных; отсчёт от факта)");

        // B1: вставка без зарядника → честный отказ с причиной (не молчание).
        string refuseMsg = "";
        var slots = _inventory.GetAllSlots();
        var stoneSlot = slots.FirstOrDefault(s => !s.IsEmpty && s.ItemId == StoneId);
        bool b1 = stoneSlot != null && !_bridge.TryInsertStone(stoneSlot.SlotId, StoneId, out refuseMsg);
        bool b1Kept = _inventory.GetItemCount(StoneId) == stonesBefore;
        GD.Print($"[ChargerSim] B1. вставка БЕЗ зарядника: отказ={b1} (причина: «{refuseMsg}»), камень не потерян={b1Kept} — " +
                 $"{(b1 && b1Kept ? "OK" : "DEFECT")}");
        pass &= b1 && b1Kept;

        // === C. Зарядник: L3+ → инвентарь → надеть в слот пояса ===
        _qi.SetCultivationLevel(3);
        var chargerItem = _itemGen.GenerateChargerForLevel(3, 92001);
        bool c1 = chargerItem != null && _inventory.TryAddItem(chargerItem, 1);
        GD.Print($"[ChargerSim] C1. зарядник сгенерирован (L3): {c1} — {(c1 ? "OK" : "DEFECT")}");
        pass &= c1;
        if (chargerItem == null) { GD.Print("[ChargerSim] VERDICT: FAIL"); return; }

        _inventory.TryRemoveItem(chargerItem.ItemId, 1);
        bool c2 = _equipment.TryEquip(EquipmentSlot.Belt, chargerItem);
        bool c2Equipped = _bridge.IsChargerEquipped;
        GD.Print($"[ChargerSim] C2. зарядник надет в Belt: equip={c2}, IsChargerEquipped={c2Equipped} — " +
                 $"{(c2 && c2Equipped ? "OK" : "DEFECT")}");
        pass &= c2 && c2Equipped;

        // C3: ГЕЙТ ПОЯСА (ядро бага): зарядник ≠ пояс — слоты быстрого
        // доступа НЕ включаются (раньше IsBeltEquipped=true → ряд пояса
        // принимал лекарства и молча отвергал камни).
        bool c3 = !_belt.IsBeltEquipped;
        GD.Print($"[ChargerSim] C3. IsBeltEquipped при НАДЕТОМ ЗАРЯДНИКЕ: {_belt.IsBeltEquipped} (ожидаем false) — " +
                 $"{(c3 ? "OK" : "DEFECT: зарядник маскируется под пояс")}");
        pass &= c3;

        // C4: режим домена синхронизировал мост (Activate при надевании).
        bool c4 = _charger.Mode == Core.Interfaces.ChargerMode.On;
        GD.Print($"[ChargerSim] C4. режим зарядника после экипировки: {_charger.Mode} (ожидаем On) — {(c4 ? "OK" : "DEFECT")}");
        pass &= c4;

        // === D. Окно H ===
        window?.Toggle();
        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        bool d1 = window is { Visible: true };
        // Гейт-лейбл скрыт (зарядник надет), слоты видимы.
        bool d2 = window != null && window.SlotsVisibleForQA;
        GD.Print($"[ChargerSim] D1. окно H открыто: {d1}; контент (слоты) виден: {d2} — {(d1 && d2 ? "OK" : "DEFECT")}");
        pass &= d1 && d2;
        window?.Toggle(); // закрыть (модальная пауза снята)
        await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);

        // === E. Вставка камня (мост, слот-адресно) ===
        slots = _inventory.GetAllSlots();
        stoneSlot = slots.FirstOrDefault(s => !s.IsEmpty && s.ItemId == StoneId);
        bool e1 = false, e2 = false, e3 = false;
        if (stoneSlot != null)
        {
            e1 = _bridge.TryInsertStone(stoneSlot.SlotId, StoneId, out var insertMsg);
            GD.Print($"[ChargerSim] E1. вставка камня: {e1} («{insertMsg}»)");
            e2 = _inventory.GetItemCount(StoneId) == stonesBefore - 1;
            GD.Print($"[ChargerSim] E2. камень покинул инвентарь: {e2} (осталось {_inventory.GetItemCount(StoneId)})");
            e3 = _charger.GetSlotSnapshot(0).HasStone
                 && _charger.GetSlotSnapshot(0).MaxQi == ((QiStoneData)stoneDef).QiAmount;
            var snap = _charger.GetSlotSnapshot(0);
            GD.Print($"[ChargerSim] E3. камень в домене: слот 0 = {snap.HasStone}, MaxQi={snap.MaxQi} " +
                     $"(ожидаем {((QiStoneData)stoneDef).QiAmount} — канон 1024×см³, не доменный дефолт)");
        }
        else
        {
            GD.Print("[ChargerSim] E1. DEFECT — кучка камня не найдена");
        }
        pass &= e1 && e2 && e3;

        // Скриншот окна H с камнем (Xvfb + opengl3: GODOT_CHARGER_SHOT).
        string? shotPrefix = System.Environment.GetEnvironmentVariable("GODOT_CHARGER_SHOT");
        if (!string.IsNullOrEmpty(shotPrefix) && window != null)
        {
            window.Toggle();
            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
            SaveScreenshot($"{shotPrefix}_charger.png");
            window.Toggle();
            await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
        }

        // === F. Домен качает (окно закрыто — модальная пауза снята) ===
        // Метрика: камень истощается, буфер/ядро практика растут. Буфер
        // часто ~0 (TransferToPractitioner вне боя сразу сливает Ци ядру —
        // CH-16), поэтому одна цифра BufferQi не показательна.
        long stoneBeforeF = _charger.GetSlotSnapshot(0).CurrentQi;
        long bufferBefore = _charger.BufferQi;
        long playerQiBefore = _qi.CurrentQi;
        await ToSignal(GetTree().CreateTimer(3.0), SceneTreeTimer.SignalName.Timeout);
        long stoneAfterF = _charger.GetSlotSnapshot(0).CurrentQi;
        long bufferAfter = _charger.BufferQi;
        long playerQiAfter = _qi.CurrentQi;
        bool f1 = stoneAfterF < stoneBeforeF || bufferAfter > bufferBefore || playerQiAfter > playerQiBefore;
        GD.Print($"[ChargerSim] F1. домен качает: камень {stoneBeforeF}→{stoneAfterF}, буфер {bufferBefore}→{bufferAfter}, " +
                 $"ядро практика {playerQiBefore}→{playerQiAfter} — {(f1 ? "OK" : "DEFECT: домен не работает")}");
        pass &= f1;
        // F2: Ци доходит до практика (камень — буфер — ядро).
        bool f2 = playerQiAfter > playerQiBefore || bufferAfter > 0;
        GD.Print($"[ChargerSim] F2. Ци дошло до практика (ядро/буфер): {f2} — {(f2 ? "OK" : "DEFECT: потери в никуда")}");
        pass &= f2;

        // === G. Контракт анти-дюпа (извлечение) ===
        // G1: частично-истощённый камень извлечь НЕЛЬЗЯ (инвентарь хранит
        // стаки ItemId+Count — состояния per-экземпляра нет; возврат частичного
        // = дюп «вставил 1024 → вынул полный 1024»).
        _charger.Deactivate(); // детерминизм: камень не истощается дальше
        var partialSnap = _charger.GetSlotSnapshot(0);
        string partialMsg = "";
        bool g1 = partialSnap.HasStone && partialSnap.CurrentQi < partialSnap.MaxQi
                  && !_bridge.TryExtractStone(0, out partialMsg);
        GD.Print($"[ChargerSim] G1. частичный камень ({partialSnap.CurrentQi}/{partialSnap.MaxQi}): извлечение " +
                 $"отклонено={g1} («{partialMsg}») — {(g1 ? "OK (анти-дюп)" : "DEFECT: дюп Ци")}");
        pass &= g1;

        // G2: ПОЛНЫЙ камень извлекается честно (режим Off → не истощается).
        var slotForSecond = _inventory.GetAllSlots().FirstOrDefault(s => !s.IsEmpty && s.ItemId == StoneId);
        bool g2 = false, g3 = false;
        if (slotForSecond != null)
        {
            bool ins = _bridge.TryInsertStone(slotForSecond.SlotId, StoneId, out _);
            int slotIdx = -1;
            for (int i = 0; i < _charger.SlotCount; i++)
                if (_charger.GetSlotSnapshot(i).HasStone) { slotIdx = (slotIdx < 0 ? i : slotIdx); }
            // второй камень — в первом свободном (индекс 1 или 2)
            int secondIdx = -1;
            for (int i = 0; i < _charger.SlotCount; i++)
                if (i != 0 && _charger.GetSlotSnapshot(i).HasStone) { secondIdx = i; break; }
            if (ins && secondIdx >= 0)
            {
                var fullSnap = _charger.GetSlotSnapshot(secondIdx);
                if (fullSnap.CurrentQi == fullSnap.MaxQi)
                {
                    bool ext = _bridge.TryExtractStone(secondIdx, out var extMsg);
                    g2 = ext && !_charger.GetSlotSnapshot(secondIdx).HasStone;
                    GD.Print($"[ChargerSim] G2. полный камень извлечён: {ext} («{extMsg}») — {(g2 ? "OK" : "DEFECT")}");
                }
            }
            g3 = _inventory.GetItemCount(StoneId) == stonesBefore - 1; // в слоте 0 остался 1, второй вернулся
            GD.Print($"[ChargerSim] G3. камень вернулся в инвентарь: {g3} (осталось {_inventory.GetItemCount(StoneId)}, " +
                     $"ожидаем {stonesBefore - 1}: в заряднике 1, стартовые на месте)");
        }
        pass &= g2 && g3;

        // G4: ПУСТОЙ камень — «рассыпался» (в инвентарь НЕ возвращается).
        // Опустошаем камень слота 0 напрямую (доменный ExtractQi — как
        // CheckDepletedStones), затем — ветка моста «пустой».
        bool g4 = false;
        var snap0 = _charger.GetSlotSnapshot(0);
        if (snap0.HasStone)
        {
            _ = snap0.Stone?.ExtractQi(snap0.CurrentQi); // → пустой
            int beforeDrop = _inventory.GetItemCount(StoneId);
            bool extEmpty = _bridge.TryExtractStone(0, out var emptyMsg);
            int afterDrop = _inventory.GetItemCount(StoneId);
            g4 = extEmpty && afterDrop == beforeDrop;
            GD.Print($"[ChargerSim] G4. пустой камень рассыпался (в инвентарь НЕ вернулся): {extEmpty} «{emptyMsg}», " +
                     $"предметов {beforeDrop}→{afterDrop} — {(g4 ? "OK (анти-дюп пустышек)" : "DEFECT: дюп")}");
        }
        else
        {
            GD.Print("[ChargerSim] G4. слот 0 пуст — проверка пропущена");
            g4 = true;
        }
        pass &= g4;

        // === H. Сейв round-trip: MaxQi камня из МОСТА не урезается ===
        // (баг RestoreState: пересоздавал камень по доменному дефолту
        // BaseQi(размер)×mult → мост-камень 1024 превращался в 100).
        // Вставляем свежий полный камень → Capture → Restore → MaxQi тот же.
        bool h1 = false;
        var slotForThird = _inventory.GetAllSlots().FirstOrDefault(s => !s.IsEmpty && s.ItemId == StoneId);
        if (slotForThird != null && _bridge.TryInsertStone(slotForThird.SlotId, StoneId, out _))
        {
            _charger.Deactivate(); // детерминизм: камень не истощается
            long maxQiBefore = 0;
            for (int i = 0; i < _charger.SlotCount; i++)
                if (_charger.GetSlotSnapshot(i).HasStone) maxQiBefore = _charger.GetSlotSnapshot(i).MaxQi;
            var state = _charger.CaptureState();
            _charger.RestoreState(state);
            long maxQiAfter = 0; bool hasStone = false;
            for (int i = 0; i < _charger.SlotCount; i++)
                if (_charger.GetSlotSnapshot(i).HasStone) { hasStone = true; maxQiAfter = _charger.GetSlotSnapshot(i).MaxQi; }
            h1 = hasStone && maxQiAfter == maxQiBefore;
            GD.Print($"[ChargerSim] H1. сейв round-trip MaxQi: {maxQiBefore} → {maxQiAfter} — " +
                     $"{(h1 ? "OK" : "DEFECT: reload урезает камень")}");
        }
        pass &= h1;

        // === I. Пояс-контроль: зарядник снят → домен Off; armor_belt → пояс ===
        _equipment.TryUnequip(EquipmentSlot.Belt, out _);
        await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
        bool i1 = !_bridge.IsChargerEquipped && _charger.Mode == Core.Interfaces.ChargerMode.Off;
        GD.Print($"[ChargerSim] I1. снят зарядник: домен выключен={i1} (Mode={_charger.Mode}) — {(i1 ? "OK" : "DEFECT: качает без зарядника")}");
        pass &= i1;

        var beltItem = _equipGen.GenerateArmor(3, "armor_belt", 92002);
        bool i2 = false;
        if (beltItem != null)
        {
            _inventory.TryAddItem(beltItem, 1);
            _inventory.TryRemoveItem(beltItem.ItemId, 1);
            bool eq = _equipment.TryEquip(EquipmentSlot.Belt, beltItem);
            i2 = eq && _belt.IsBeltEquipped;
            GD.Print($"[ChargerSim] I2. настоящий пояс (armor_belt): надет={eq}, IsBeltEquipped={_belt.IsBeltEquipped} — " +
                     $"{(i2 ? "OK (слоты 3-9 для лекарств — с поясом)" : "DEFECT")}");
            _equipment.TryUnequip(EquipmentSlot.Belt, out _);
        }
        pass &= i2;

        GD.Print($"[ChargerSim] VERDICT: {(pass ? "PASS — камни Ци вставляются в зарядник (окно H/меню/дроп), анти-дюп жив, пояс ≠ зарядник, домен гейтируется экипировкой" : "FAIL — см. DEFECT-строки выше")}");
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

    private void SaveScreenshot(string path)
    {
        var img = GetViewport().GetTexture().GetImage();
        if (img != null)
        {
            var err = img.SavePng(path);
            GD.Print($"[ChargerSim] screenshot: {path} ({err == Error.Ok})");
        }
    }
}
#endif
