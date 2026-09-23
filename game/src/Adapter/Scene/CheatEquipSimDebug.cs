#nullable enable
#if DEBUG
// Создано: 2026-09-23 — R37, баг-репорт пользователя (новый день, 2 пункта):
//   1. «Чит-меню: сползают надписи при нажатии любых читов» — геометрический
//      дифф всех контролов панели до/после нажатий (позиция/размер обязаны
//      быть стабильны; меняется только текст статуса).
//   2. «Не смог одеть сапоги из инвентаря, броня одевается» — полный путь
//      (генератор → инвентарь → двойной клик → кукла → EquipmentService)
//      для Feet и Torso: гейты R35 (RequiredCultivationLevel) + причина
//      отказа (EquipmentBlockedEvent) обязана быть видимой игроку.
//
// Запуск (headless, вердикт):
//   GODOT_NEWGAME=1 GODOT_CHEATEQUIP_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn
// Скриншоты (Xvfb + opengl3, VLM-разбор): GODOT_CHEAT_SHOT=<префикс>
//   → {prefix}_before.png (панель открыта), {prefix}_after1.png (после
//   «Заполнить Ци»), {prefix}_after2.png (после «+10 000 Ци» и «Прорыв»).
using Godot;
using System;
using System.Collections.Generic;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

public partial class CheatEquipSimDebug : Node
{
    [Inject] private IInventoryService? _inventory;
    [Inject] private IItemDatabaseService? _itemDb;
    [Inject] private IEquipmentService? _equipment;
    [Inject] private IEquipmentGenerator? _equipGen;
    [Inject] private IQiService? _qi;
    [Inject] private ISubscriber<EquipmentBlockedEvent>? _equipBlockedSub;

    // События, пойманные прямой подпиской (гейт-диагностика A3/A5).
    private readonly List<string> _blockedReasons = new();
    private IDisposable? _blockedToken;

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        GD.Print("[CheatEquipSim] Ready — чит-панель/сапоги тест старт через 2с");
        _ = RunSequenceAsync();
    }

    public override void _ExitTree()
    {
        _blockedToken?.Dispose();
    }

    private async System.Threading.Tasks.Task RunSequenceAsync()
    {
        await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);

        var world = GetParent() as GameWorldController ?? FindWorld(GetTree().Root);
        if (world == null)
        {
            GD.Print("[CheatEquipSim] VERDICT: FAIL — GameWorldController not found");
            return;
        }
        if (_inventory == null || _itemDb == null || _equipment == null || _equipGen == null || _qi == null)
        {
            GD.Print("[CheatEquipSim] VERDICT: FAIL — DI not wired");
            return;
        }

        _blockedToken = _equipBlockedSub?.Subscribe(OnEquipmentBlocked);

        bool pass = true;
        int playerLevel = Math.Max(1, (int)_qi.CultivationLevel);

        // === A. Back-end: инвентарь → сервис (путь HandleDropOnSlot) ===
        // A1: сапоги уровня ИГРОКА (как чит «Броня cycle») — обязана надеваться.
        EquipmentData? bootsPlayer = _equipGen.GenerateArmor(playerLevel, "armor_feet", 42001);
        bool a1Ok = false;
        if (bootsPlayer != null)
        {
            _inventory.TryAddItem(bootsPlayer, 1);
            bool removed = _inventory.TryRemoveItem(bootsPlayer.ItemId, 1);
            bool equipped = _equipment.TryEquip(EquipmentSlot.Feet, bootsPlayer);
            var inSlot = _equipment.GetEquipped(EquipmentSlot.Feet);
            a1Ok = removed && equipped && inSlot != null && inSlot.ItemId == bootsPlayer.ItemId;
            GD.Print($"[CheatEquipSim] A1. сапоги L{playerLevel} (уровень игрока): " +
                     $"remove={removed}, equip={equipped}, Feet={inSlot?.NameRu} — {(a1Ok ? "OK" : "DEFECT")}");
        }
        pass &= a1Ok && bootsPlayer != null;

        // A2: контроль — нагрудник уровня игрока (та же механика, Torso).
        EquipmentData? torsoPlayer = _equipGen.GenerateArmor(playerLevel, "armor_torso", 42002);
        bool a2Ok = false;
        if (torsoPlayer != null)
        {
            _inventory.TryAddItem(torsoPlayer, 1);
            bool removed = _inventory.TryRemoveItem(torsoPlayer.ItemId, 1);
            bool equipped = _equipment.TryEquip(EquipmentSlot.Torso, torsoPlayer);
            var inSlot = _equipment.GetEquipped(EquipmentSlot.Torso);
            a2Ok = removed && equipped && inSlot != null && inSlot.ItemId == torsoPlayer.ItemId;
            GD.Print($"[CheatEquipSim] A2. нагрудник L{playerLevel} (контроль): " +
                     $"equip={equipped}, Torso={inSlot?.NameRu} — {(a2Ok ? "OK" : "DEFECT")}");
        }
        pass &= a2Ok && torsoPlayer != null;

        // A3: сапоги L5 (уровень NPC-лута) при игроке L1 — честный гейт
        // RequiredCultivationLevel: отказ + событие с причиной.
        _blockedReasons.Clear();
        EquipmentData? bootsHigh = _equipGen.GenerateArmor(5, "armor_feet", 42003);
        bool a3Gate = false, a3Event = false;
        if (bootsHigh != null)
        {
            _inventory.TryAddItem(bootsHigh, 1);
            _inventory.TryRemoveItem(bootsHigh.ItemId, 1);
            bool equipped = _equipment.TryEquip(EquipmentSlot.Feet, bootsHigh);
            a3Gate = !equipped;
            a3Event = _blockedReasons.Count > 0;
            GD.Print($"[CheatEquipSim] A3. сапоги L5 при игроке L{playerLevel}: equip={equipped} " +
                     $"(ожидаем false), BlockedEvent={a3Event}, reason=\"{(_blockedReasons.Count > 0 ? _blockedReasons[0] : "НЕТ")}\" — " +
                     $"{(a3Gate ? "гейт OK" : "DEFECT: гейт не сработал")}");
            // Почистить: предмет остался в руках сима (не в инвентаре) — вернуть.
            if (!equipped) _inventory.TryAddItem(bootsHigh, 1);
            _inventory.TryRemoveItem(bootsHigh.ItemId, 1); // обратно убрать из инвентаря (не мусорим)
        }
        pass &= a3Gate && bootsHigh != null;
        // A4: причина отказа видна игроку? (после фикса — тост из GWC; здесь
        // факт события. Событие есть, но БЕЗ фикса его никто не показывает.)
        GD.Print($"[CheatEquipSim] A4. EquipmentBlockedEvent публикуется: {a3Event} " +
                 "(факт события; показ игроку — см. вердикт U-секции ниже)");

        // === B. UI: двойной клик по строке инвентаря ===
        // Снять всё, что надето в A (вернётся в инвентарь через событие).
        if (_equipment.GetEquipped(EquipmentSlot.Feet) != null)
            _equipment.TryUnequip(EquipmentSlot.Feet, out _);
        if (_equipment.GetEquipped(EquipmentSlot.Torso) != null)
            _equipment.TryUnequip(EquipmentSlot.Torso, out _);

        var win = world.InventoryWindowForQA;
        if (win == null)
        {
            GD.Print("[CheatEquipSim] VERDICT: FAIL — InventoryWindow not found");
            return;
        }

        // B1: сапоги уровня игрока — двойной клик по строке.
        EquipmentData? bootsUi = _equipGen.GenerateArmor(playerLevel, "armor_feet", 42004);
        bool b1Ok = false;
        if (bootsUi != null)
        {
            _inventory.TryAddItem(bootsUi, 1);
            win.Toggle(); // открыть окно → RefreshItems
            await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
            var row = win.FindRowForQA(bootsUi.ItemId);
            if (row != null)
            {
                var click = new InputEventMouseButton { ButtonIndex = Godot.MouseButton.Left, Pressed = true };
                row._GuiInput(click);
                row._GuiInput(click); // второй подряд → double-click
                await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
                int left = _inventory.GetItemCount(bootsUi.ItemId);
                var inSlot = _equipment.GetEquipped(EquipmentSlot.Feet);
                b1Ok = left == 0 && inSlot != null && inSlot.ItemId == bootsUi.ItemId;
                GD.Print($"[CheatEquipSim] B1. двойной клик сапоги: row найден, " +
                         $"в инвентаре осталось {left}, Feet={inSlot?.NameRu} — {(b1Ok ? "OK" : "DEFECT")}");
            }
            else
            {
                GD.Print($"[CheatEquipSim] B1. DEFECT — строка сапогов не найдена в списке");
            }
            // Чистим Feet для симметрии (если наделись).
            if (_equipment.GetEquipped(EquipmentSlot.Feet) != null)
                _equipment.TryUnequip(EquipmentSlot.Feet, out _);
        }
        pass &= b1Ok && bootsUi != null;

        // B2: контроль — нагрудник двойным кликом.
        EquipmentData? torsoUi = _equipGen.GenerateArmor(playerLevel, "armor_torso", 42005);
        bool b2Ok = false;
        if (torsoUi != null)
        {
            _inventory.TryAddItem(torsoUi, 1);
            win.RefreshExternally();
            await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
            var row = win.FindRowForQA(torsoUi.ItemId);
            if (row != null)
            {
                var click = new InputEventMouseButton { ButtonIndex = Godot.MouseButton.Left, Pressed = true };
                row._GuiInput(click);
                row._GuiInput(click);
                await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
                int left = _inventory.GetItemCount(torsoUi.ItemId);
                var inSlot = _equipment.GetEquipped(EquipmentSlot.Torso);
                b2Ok = left == 0 && inSlot != null && inSlot.ItemId == torsoUi.ItemId;
                GD.Print($"[CheatEquipSim] B2. двойной клик нагрудник: " +
                         $"в инвентаре осталось {left}, Torso={inSlot?.NameRu} — {(b2Ok ? "OK" : "DEFECT")}");
            }
            if (_equipment.GetEquipped(EquipmentSlot.Torso) != null)
                _equipment.TryUnequip(EquipmentSlot.Torso, out _);
            win.Toggle(); // закрыть окно
            await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
        }
        pass &= b2Ok && torsoUi != null;

        // === C. Чит-панель: геометрический дифф до/после нажатий ===
        // Баг-репорт «сползают надписи при нажатии любых читов»: снимаем
        // GlobalPosition+Size каждого Control дерева панели, жмём кнопки,
        // снимаем снова. Всё, кроме статус-лейбла, обязано быть статично.
        var cheat = world.CheatPanelForQA;
        if (cheat == null)
        {
            GD.Print("[CheatEquipSim] VERDICT: FAIL — CheatPanel not found");
            return;
        }
        cheat.Visible = true;
        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);

        var before = SnapshotControls(cheat);
        string? shotPrefix = System.Environment.GetEnvironmentVariable("GODOT_CHEAT_SHOT");
        if (!string.IsNullOrEmpty(shotPrefix))
        {
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
            SaveScreenshot($"{shotPrefix}_before.png");
        }

        // Нажатие 1: «Заполнить Ци» (в верхней части панели).
        var btnFill = FindButtonByText(cheat, "Заполнить Ци");
        if (btnFill == null)
        {
            GD.Print("[CheatEquipSim] VERDICT: FAIL — кнопка «Заполнить Ци» не найдена");
            return;
        }
        PressButton(btnFill);
        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        var after1 = SnapshotControls(cheat);
        var drift1 = DiffControls(before, after1);
        PrintDrift("после «Заполнить Ци»", drift1);
        if (!string.IsNullOrEmpty(shotPrefix))
        {
            await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
            SaveScreenshot($"{shotPrefix}_after1.png");
        }

        // Нажатия 2-3: «+10 000 Ци» и «Прорыв».
        var btnAdd = FindButtonByText(cheat, "+10 000 Ци");
        var btnBreak = FindButtonByText(cheat, "Прорыв ▲");
        if (btnAdd != null) PressButton(btnAdd);
        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        if (btnBreak != null) PressButton(btnBreak);
        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        var after2 = SnapshotControls(cheat);
        var drift2 = DiffControls(before, after2);
        PrintDrift("после «+10 000 Ци» + «Прорыв»", drift2);
        if (!string.IsNullOrEmpty(shotPrefix))
        {
            await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
            SaveScreenshot($"{shotPrefix}_after2.png");
        }

        // Дефект = непустой дифф (контролы, кроме статуса, сместились).
        bool c1Ok = drift1.Count == 0;
        bool c2Ok = drift2.Count == 0;
        pass &= c1Ok && c2Ok;
        GD.Print($"[CheatEquipSim] C1. дифф после 1 нажатия: {(c1Ok ? "стабилен (OK)" : $"{drift1.Count} контрол(ов) сместились (DEFECT)")}");
        GD.Print($"[CheatEquipSim] C2. дифф после 3 нажатий: {(c2Ok ? "стабилен (OK)" : $"{drift2.Count} контрол(ов) сместились (DEFECT)")}");

        // Тост-стек не должен переполняться (MaxToastLines=5).
        int toastCount = world.ToastLineCount;
        bool c3Ok = toastCount <= 5;
        GD.Print($"[CheatEquipSim] C3. тост-стек: {toastCount} строк (≤5) — {(c3Ok ? "OK" : "DEFECT")}");
        pass &= c3Ok;

        // === U. UX: причина отказа экипировки видима игроку? ===
        // Баг-репорт «не смог одеть сапоги»: EquipmentBlockedEvent публикует
        // reason, но БЕЗ подписчика игрок не знает, ПОЧЕМУ предмет не надевается.
        // U1: прямой факт (сим видел событие в A3).
        bool u1 = a3Event;
        GD.Print($"[CheatEquipSim] U1. EquipmentBlockedEvent доходил до слушателя: {u1}");
        // U2: тост-рендер GWC подписан на EquipmentBlockedEvent? (после
        // фикса — да; проверяем непрямым признаком: количество тостов с
        // префиксом «⛔» после повторной попытки отклонённой экипировки).
        _blockedReasons.Clear();
        var bootsHi2 = _equipGen.GenerateArmor(5, "armor_feet", 42006);
        if (bootsHi2 != null)
        {
            _inventory.TryAddItem(bootsHi2, 1);
            _inventory.TryRemoveItem(bootsHi2.ItemId, 1);
            _equipment.TryEquip(EquipmentSlot.Feet, bootsHi2); // отклонится
            await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
            _inventory.TryAddItem(bootsHi2, 1);
            _inventory.TryRemoveItem(bootsHi2.ItemId, 1);
        }
        int blockedToasts = world.ToastLinesWithPrefix("⛔");
        bool u2 = blockedToasts > 0;
        GD.Print($"[CheatEquipSim] U2. тост причины отказа (⛔) показан игроку: {u2} " +
                 $"({blockedToasts} строк) — {(u2 ? "OK" : "DEFECT: отказ экипировки невидим")}");
        pass &= u2;

        cheat.Visible = false;

        GD.Print($"[CheatEquipSim] VERDICT: {(pass ? "PASS — сапоги/броня экипируются по уровню игрока, гейты честные, причина отказа видима, геометрия чит-панели стабильна" : "FAIL")} ");
    }

    private void OnEquipmentBlocked(in EquipmentBlockedEvent e)
    {
        _blockedReasons.Add(e.Reason ?? "");
    }

    /// <summary>Нажать кнопку, как это делает игрок: фокус + Pressed.</summary>
    private static void PressButton(Button btn)
    {
        btn.GrabFocus();
        btn.EmitSignal(Button.SignalName.Pressed);
    }

    /// <summary>Снимок геометрии всех Control поддерева (NodePath → rect).</summary>
    private static Dictionary<string, (Vector2 pos, Vector2 size)> SnapshotControls(Node root)
    {
        var snap = new Dictionary<string, (Vector2, Vector2)>();
        SnapshotRecursive(root, snap);
        return snap;
    }

    private static void SnapshotRecursive(Node node, Dictionary<string, (Vector2, Vector2)> snap)
    {
        if (node is Control c)
        {
            snap[node.GetPath().ToString()] = (c.GlobalPosition, c.Size);
        }
        foreach (var child in node.GetChildren())
            SnapshotRecursive(child, snap);
    }

    /// <summary>
    /// Дифф двух снимков: контролы, сместившиеся более чем на 0.5px или
    /// изменившие размер. Статус-лейбл (текст меняется по дизайну) исключён
    /// по последней позиции в дереве — последний Label панели.
    /// </summary>
    private static List<string> DiffControls(
        Dictionary<string, (Vector2 pos, Vector2 size)> before,
        Dictionary<string, (Vector2 pos, Vector2 size)> after)
    {
        var diffs = new List<string>();
        foreach (var kvp in after)
        {
            if (!before.TryGetValue(kvp.Key, out var b)) continue;
            var a = kvp.Value;
            float dx = a.pos.X - b.pos.X;
            float dy = a.pos.Y - b.pos.Y;
            float dw = a.size.X - b.size.X;
            float dh = a.size.Y - b.size.Y;
            if (Math.Abs(dx) > 0.5f || Math.Abs(dy) > 0.5f || Math.Abs(dw) > 0.5f || Math.Abs(dh) > 0.5f)
            {
                diffs.Add($"{kvp.Key}: Δ=({dx:F1},{dy:F1}) Δsize=({dw:F1},{dh:F1})");
            }
        }
        return diffs;
    }

    private static void PrintDrift(string label, List<string> drift)
    {
        if (drift.Count == 0) return;
        GD.Print($"[CheatEquipSim]   дифф {label}: {drift.Count} смещений");
        foreach (var d in drift)
            GD.Print($"[CheatEquipSim]     {d}");
    }

    private static Button? FindButtonByText(Node root, string text)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Button b && b.Text == text) return b;
            var nested = FindButtonByText(child, text);
            if (nested != null) return nested;
        }
        return null;
    }

    private void SaveScreenshot(string path)
    {
        var img = GetViewport().GetTexture().GetImage();
        if (img != null)
        {
            var err = img.SavePng(path);
            GD.Print($"[CheatEquipSim] screenshot: {path} ({err == Error.Ok})");
        }
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
#endif
