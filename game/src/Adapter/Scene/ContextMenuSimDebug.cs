#nullable enable
// Создано: 2026-09-09 — ПКМ-контекстное меню инвентаря (запрос пользователя).
// Headless-верификация (GODOT_CONTEXT_DEBUG=1):
//   1. ПКМ по строке открывает окно свойств (характеристики предмета).
//   2. Кнопка «Разделить стак…» есть только у стакающихся ×2+;
//      камень Ци получает кнопку «Использовать» (этап 7 ЦИ сохранён).
//   3. Диалог деления: слайдер [1..count-1], два числа по краям
//      (слева — остаток исходного стака, справа — новая кучка), в сумме = count.
//   4. Кнопки «−»/«+» двигают значение слайдера на 1.
//   5. Подтверждение → множественные стаки («кучки») одного ItemId;
//      тотал/вес/объём не меняются.
//   6. Граничные случаи TrySplitSlot: отрицательный индекс, 0, весь стак,
//      не-стакающийся предмет → false.
//   7. Корзина выбрасывает ТОЛЬКО перетащенную кучку (drag-data несёт
//      slot_index), остальные кучки того же предмета остаются.
//   8. Esc-приоритет: CloseTopmostPopup закрывает верхний попап, не окно.
// Запуск: GODOT_NEWGAME=1 GODOT_CONTEXT_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn
using Godot;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;
using CultivationGame.Adapter.UI;

namespace CultivationGame.Adapter.Scene;

public partial class ContextMenuSimDebug : Node
{
    [Inject] private IInventoryService? _inventory;
    [Inject] private IItemDatabaseService? _itemDb;
    [Inject] private IGroundItemService? _groundItems;

    private const string StoneId = "material_stone";
    private const string QiDustId = "qistone_dust_calm";

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        GD.Print("[ContextSim] Ready — ПКМ-меню/разделение стака тест старт через 2с");
        _ = RunSequenceAsync();
    }

    private async System.Threading.Tasks.Task RunSequenceAsync()
    {
        await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);

        var world = GetParent() as GameWorldController ?? FindWorld(GetTree().Root);
        if (world == null)
        {
            GD.Print("[ContextSim] VERDICT: FAIL — GameWorldController not found");
            return;
        }
        if (_inventory == null || _itemDb == null || _groundItems == null)
        {
            GD.Print("[ContextSim] VERDICT: FAIL — DI not wired");
            return;
        }
        var win = world.InventoryWindowForQA;
        if (win == null)
        {
            GD.Print("[ContextSim] VERDICT: FAIL — InventoryWindow not found");
            return;
        }

        bool pass = true;

        // === Подготовка: 5 камней → один стак ×10 (старт 5 + тест 5) ===
        if (!_itemDb.TryGetItem(StoneId, out var stone) || stone == null)
        {
            GD.Print($"[ContextSim] VERDICT: FAIL — {StoneId} нет в ItemDatabase");
            return;
        }
        int stonesBefore = _inventory.GetItemCount(StoneId);
        _inventory.TryAddItem(stone, 5);
        int total = _inventory.GetItemCount(StoneId); // ожидаем 10

        float weightBefore = _inventory.GetCurrentWeight();
        float volumeBefore = _inventory.GetCurrentVolume();

        win.Toggle(); // открыть окно → RefreshItems
        await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);

        int stoneSlot = FindSlotIndex(_inventory, StoneId);
        if (stoneSlot < 0)
        {
            GD.Print("[ContextSim] VERDICT: FAIL — слот камня не найден");
            return;
        }

        // === 1. ПКМ → окно свойств ================================
        win.OpenContextMenu(stoneSlot);
        var menu = win.ContextMenuForQA;
        bool menuOpened = menu != null && win.IsContextMenuOpenForQA;
        bool hasProps = menu != null && menu.PropertyCountForQA >= 3; // вес/объём/стоимость
        bool hasSplit = menu != null && menu.SplitButtonForQA != null;
        bool hasDrop = menu != null && menu.DropButtonForQA != null;
        GD.Print($"[ContextSim] 1. ПКМ открывает свойства: меню={menuOpened}, строк характеристик={menu?.PropertyCountForQA ?? 0} (≥3), кнопка «Разделить»={hasSplit}, кнопка «Выбросить»={hasDrop}");
        pass &= menuOpened && hasProps && hasSplit && hasDrop;

        // === 2. Камень Ци: кнопка «Использовать» в меню ============
        int qiSlot = FindSlotIndex(_inventory, QiDustId);
        bool qiUseButton = false;
        if (qiSlot >= 0)
        {
            win.CloseContextMenu();
            win.OpenContextMenu(qiSlot);
            qiUseButton = win.ContextMenuForQA?.UseButtonForQA != null;
            win.CloseContextMenu();
        }
        GD.Print($"[ContextSim] 2. камень Ци «Использовать» в меню: {qiUseButton} (ожидаем True — поведение этапа 7 сохранено)");
        pass &= qiUseButton;

        // === 3. Диалог разделения: слайдер и числа ==================
        win.OpenSplitDialog(stoneSlot);
        var dlg = win.SplitDialogForQA;
        bool dlgOpened = dlg != null && win.IsSplitDialogOpenForQA;
        bool sliderBounds = dlg != null
            && (int)dlg.SliderForQA.MinValue == 1
            && (int)dlg.SliderForQA.MaxValue == total - 1;
        bool numbersSum = dlg != null
            && int.Parse(dlg.LeftLabelForQA.Text) + int.Parse(dlg.RightLabelForQA.Text) == total;
        bool defaultValue = dlg != null && dlg.CurrentMoveForQA >= 1 && dlg.CurrentMoveForQA <= total - 1;
        GD.Print($"[ContextSim] 3. диалог деления: открыт={dlgOpened}, слайдер [1..{total - 1}]={sliderBounds}, числа левые+правые={numbersSum} (сумма {total}), стартовое={dlg?.CurrentMoveForQA}");
        pass &= dlgOpened && sliderBounds && numbersSum && defaultValue;

        // === 4. Кнопки −/+ двигают значение ========================
        if (dlg != null)
        {
            int v0 = dlg.CurrentMoveForQA;
            dlg.MinusButtonForQA.EmitSignal(BaseButton.SignalName.Pressed);
            int vMinus = dlg.CurrentMoveForQA;
            dlg.PlusButtonForQA.EmitSignal(BaseButton.SignalName.Pressed);
            int vPlus = dlg.CurrentMoveForQA;
            bool nudgeWorks = (vMinus == v0 - 1) && (vPlus == vMinus + 1);
            GD.Print($"[ContextSim] 4. кнопки −/+: {v0} →(−) {vMinus} →(+) {vPlus} (ожидаем −1/+1)");
            pass &= nudgeWorks;

            // === 5. Подтверждение: сплит 10 → 7+3 ==================
            dlg.SliderForQA.Value = 3; // левое число станет 7, правое 3
            int leftNum = int.Parse(dlg.LeftLabelForQA.Text);
            int rightNum = int.Parse(dlg.RightLabelForQA.Text);
            dlg.ConfirmButtonForQA.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);

            bool dlgClosed = !win.IsSplitDialogOpenForQA;
            var slotsAfter = _inventory.GetAllSlots();
            int stoneSlots = 0;
            foreach (var s in slotsAfter)
                if (s.ItemId == StoneId) stoneSlots++;
            int totalAfter = _inventory.GetItemCount(StoneId);
            bool pilesCreated = stoneSlots == 2 && leftNum == total - 3 && rightNum == 3 && totalAfter == total;
            float weightAfter = _inventory.GetCurrentWeight();
            float volumeAfter = _inventory.GetCurrentVolume();
            bool physicsSame = System.Math.Abs(weightAfter - weightBefore) < 0.01f
                && System.Math.Abs(volumeAfter - volumeBefore) < 0.01f;
            GD.Print($"[ContextSim] 5. сплит {total} → {leftNum}+{rightNum}: диалог закрыт={dlgClosed}, кучек={stoneSlots} (2), тотал={totalAfter} (не изменился), вес/объём неизменны={physicsSame}");
            pass &= dlgClosed && pilesCreated && physicsSame;
        }

        // === 6. Граничные случаи TrySplitSlot ======================
        bool edgeNeg = !_inventory.TrySplitSlot(-1, 5);
        bool edgeZero = !_inventory.TrySplitSlot(stoneSlot, 0);
        var allSlots = _inventory.GetAllSlots();
        int currentCount = 0;
        for (int i = 0; i < allSlots.Count; i++)
            if (allSlots[i].ItemId == StoneId) { currentCount = allSlots[i].Count; break; }
        bool edgeAll = !_inventory.TrySplitSlot(stoneSlot, currentCount); // весь стак — нельзя
        bool edgeOver = !_inventory.TrySplitSlot(stoneSlot, currentCount + 5); // больше стака — нельзя
        int equipSlot = -1;
        for (int i = 0; i < allSlots.Count; i++)
        {
            if (_itemDb.TryGetItem(allSlots[i].ItemId, out var it) && it is EquipmentData)
            { equipSlot = i; break; }
        }
        bool edgeNonStack = equipSlot >= 0 && !_inventory.TrySplitSlot(equipSlot, 1);
        GD.Print($"[ContextSim] 6. границы: отрицательный={edgeNeg}, ноль={edgeZero}, весь стак={edgeAll}, больше стака={edgeOver}, не-стакающийся={edgeNonStack} (все True)");
        pass &= edgeNeg && edgeZero && edgeAll && edgeOver && (equipSlot < 0 || edgeNonStack);

        // === 7. Корзина выбрасывает ТОЛЬКО кучку ===================
        var trashZone = win.FindTrashZoneForQA();
        if (trashZone == null)
        {
            GD.Print("[ContextSim] VERDICT: FAIL — TrashDropZone не найдена");
            return;
        }

        // Найти строку-кучку ×3 (вторая кучка камня).
        var rows = win.GetRowsForQA();
        InventoryItemRow? pileRow = null;
        foreach (var r in rows)
        {
            if (r.ItemIdForQA == StoneId)
            {
                var sl = _inventory.GetAllSlots();
                if (r.SlotIndexForQA >= 0 && r.SlotIndexForQA < sl.Count
                    && sl[r.SlotIndexForQA].Count == 3)
                    pileRow = r;
            }
        }

        bool pileDropOk = false;
        if (pileRow != null)
        {
            Variant dragData = pileRow._GetDragData(Vector2.Zero);
            bool hasSlotIndex = CharacterDollPanel.TryParseDragData(dragData, out var draggedId, out var src, out int dragSlot)
                && draggedId == StoneId && src == "inventory" && dragSlot == pileRow.SlotIndexForQA;

            int groundBefore = _groundItems.Count;
            trashZone._DropData(Vector2.Zero, dragData);
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);

            int totalAfterDrop = _inventory.GetItemCount(StoneId);
            int groundAfter = _groundItems.Count;
            pileDropOk = hasSlotIndex && totalAfterDrop == total - 3 && groundAfter == groundBefore + 1;
            GD.Print($"[ContextSim] 7. drag-data с slot_index={hasSlotIndex}, корзина выбросила кучку ×3: инвентарь {total}→{totalAfterDrop} (ожидали {total - 3}, НЕ 0), ground {groundBefore}→{groundAfter}");
        }
        else
        {
            GD.Print("[ContextSim] 7. FAIL — строка-кучка ×3 не найдена");
        }
        pass &= pileDropOk;

        // === 8. Esc-приоритет попапов ==============================
        win.OpenContextMenu(stoneSlot);
        bool escMenu = win.CloseTopmostPopup() && !win.IsContextMenuOpenForQA;
        win.OpenSplitDialog(stoneSlot < 0 ? 0 : stoneSlot);
        bool escDlg = win.CloseTopmostPopup() && !win.IsSplitDialogOpenForQA && !win.IsContextMenuOpenForQA;
        bool escNothing = !win.CloseTopmostPopup(); // попапов нет → false (окно закрывает контроллер)
        GD.Print($"[ContextSim] 8. Esc-приоритет: меню закрылось={escMenu}, диалог закрылся={escDlg}, без попапов → false={escNothing}");
        pass &= escMenu && escDlg && escNothing;

        // === HOLD для VLM-скриншота (GODOT_CONTEXT_HOLD=1) ==========
        if (System.Environment.GetEnvironmentVariable("GODOT_CONTEXT_HOLD") == "1")
        {
            // Открыть контекстное меню свойства камня (скриншот №1),
            // затем диалог разделения (скриншот №2, если задан _SPLIT).
            // NOTE: переменные _MENU/_SPLIT читает сам сим — GameBoot-автоматика
            // GODOT_SCREENSHOT здесь НЕ используется (она завершает процесс).
            string? shot = System.Environment.GetEnvironmentVariable("GODOT_SCREENSHOT_MENU");
            string? shotSplit = System.Environment.GetEnvironmentVariable("GODOT_SCREENSHOT_SPLIT");
            if (!string.IsNullOrEmpty(shot))
            {
                win.OpenContextMenu(FindSlotIndex(_inventory, StoneId));
                await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
                var img = GetViewport().GetTexture().GetImage();
                img.SavePng(shot);
                GD.Print($"[ContextSim] screenshot: {shot}");
            }
            if (!string.IsNullOrEmpty(shotSplit))
            {
                win.CloseContextMenu();
                win.OpenSplitDialog(FindSlotIndex(_inventory, StoneId));
                await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
                var img2 = GetViewport().GetTexture().GetImage();
                img2.SavePng(shotSplit);
                GD.Print($"[ContextSim] screenshot split: {shotSplit}");
            }
            GD.Print("[ContextSim] HOLD: меню/диалог открыты (VLM-скриншоты)");
        }

        GD.Print($"[ContextSim] VERDICT: {(pass ? "PASS — ПКМ-свойства, слайдер деления, кучки, слот-адресный выброс работают" : "FAIL")}");
    }

    private static int FindSlotIndex(IInventoryService inv, string itemId)
    {
        var slots = inv.GetAllSlots();
        for (int i = 0; i < slots.Count; i++)
            if (slots[i].ItemId == itemId)
                return i;
        return -1;
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
