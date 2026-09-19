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
// R19 (2026-09-19) «Потребляемые ресурсы» — новые шаги:
//   12. Пилюля лечения: ПКМ → «Использовать», урон → использование →
//       счётчик −1, HP восстановлен (маршрутизация heal через IItemUseService).
//   13. Сгенерированное лекарство с EffectType="Heal" (ЗАГЛАВНАЯ — баг
//       пользователя: не лечил/показывал сырой ключ): кнопка есть,
//       кейс-нормализация работает, HP восстанавливается.
//   14. Материал (ItemCategory.Material): кнопки «Использовать» НЕТ.
//   15. Читаемость подокна: панель имеет высоту (регресс: фон не
//       рисовался при высоте 0), фон непрозрачен, контраст ≥ 4.5:1.
// Запуск: GODOT_NEWGAME=1 GODOT_CONTEXT_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn
using Godot;
using System;
using System.Collections.Generic;
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
    [Inject] private IBodyService? _body;      // R19: лечение/урон в тесте
    [Inject] private IQiService? _qi;          // R19: поглощение Ци камня

    private const string StoneId = "material_stone";
    private const string QiDustId = "qistone_dust_calm";
    private const string HealPillId = "con_pill_healing";
    private const string HealCapitalTestId = "con_test_heal_capital"; // R19: репродукция бага "Heal"

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

        // === 2. Камень Ци: кнопка «Использовать» + РЕАЛЬНОЕ поглощение ===
        // R19: расширен — кнопка теперь приходит из маршрутизации
        // IItemUseService; проверяем и энд-ту-энд поглощение через неё
        // (миграция TryUseQiStone → ItemUseService.TryUseFromInventory).
        int qiSlot = FindSlotIndex(_inventory, QiDustId);
        bool qiUseButton = false;
        bool qiAbsorbed = false;
        if (qiSlot >= 0)
        {
            int qiCountBefore = _inventory.GetItemCount(QiDustId);
            // Сливаем половину Ци: стартовый буфер 1000/1000 полон — AddQi
            // капится по MaxQi, поглощение пыли (+1024) дало бы +0 (так было
            // и в старом TryUseQiStone: тост «+0 Ци»). Дрейн открывает окно.
            _qi?.TryConsumeQi(System.Math.Min(_qi.CurrentQi, _qi.MaxQi / 2));
            long qiBefore = _qi?.CurrentQi ?? 0;

            win.CloseContextMenu();
            win.OpenContextMenu(qiSlot);
            var qiMenu = win.ContextMenuForQA;
            qiUseButton = qiMenu?.UseButtonForQA != null;
            if (qiMenu?.UseButtonForQA != null)
            {
                // Нажатие как у игрока: хендлер закрывает меню и вызывает
                // TryUseItem(slotId, itemId) → сервис → Qi +1024, счётчик −1.
                // (≥1024: мир живёт — фоновая регенерация Ци может добавить
                // чуть больше за время ожидания; расход Ци у стоячего игрока нет.)
                qiMenu.UseButtonForQA.EmitSignal(BaseButton.SignalName.Pressed);
                await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
                long qiAfter = _qi?.CurrentQi ?? 0;
                long maxQi = _qi?.MaxQi ?? 0;
                int qiCountAfter = _inventory.GetItemCount(QiDustId);
                // ≥ дрейна: пыль +1024 но капится по MaxQi; мир живёт —
                // фоновая регенерация может добавить ещё; главное — буфер
                // заполнился и счётчик уменьшился ровно на 1.
                qiAbsorbed = qiAfter > qiBefore && qiAfter <= maxQi && qiCountAfter == qiCountBefore - 1;
                GD.Print($"[ContextSim] 2b. поглощение через кнопку: Ци {qiBefore}→{qiAfter} (макс {maxQi}), счётчик {qiCountBefore}→{qiCountAfter}");
            }
            win.CloseContextMenu();
        }
        GD.Print($"[ContextSim] 2. камень Ци «Использовать» в меню: {qiUseButton} (ожидаем True), поглощение сквозь кнопку: {qiAbsorbed}");
        pass &= qiUseButton && qiAbsorbed;

        // === 3. Диалог разделения: слайдер и числа ==================
        // R10 P1-SlotId: открытие по стабильному идентичности кучки.
        Guid stoneSlotId = _inventory.GetAllSlots()[stoneSlot].SlotId;
        win.OpenSplitDialog(stoneSlotId);
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
            // R10 P1-SlotId: слот-адресность по Guid (индекс — только QA).
            Variant dragData = pileRow._GetDragData(Vector2.Zero);
            bool hasSlotId = CharacterDollPanel.TryParseDragData(dragData, out var draggedId, out var src, out Guid dragSlotId)
                && draggedId == StoneId && src == "inventory" && dragSlotId == pileRow.SlotIdForQA;

            int groundBefore = _groundItems.Count;
            trashZone._DropData(Vector2.Zero, dragData);
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);

            int totalAfterDrop = _inventory.GetItemCount(StoneId);
            int groundAfter = _groundItems.Count;
            pileDropOk = hasSlotId && totalAfterDrop == total - 3 && groundAfter == groundBefore + 1;
            GD.Print($"[ContextSim] 7. drag-data с slot_id={hasSlotId}, корзина выбросила кучку ×3: инвентарь {total}→{totalAfterDrop} (ожидали {total - 3}, НЕ 0), ground {groundBefore}→{groundAfter}");
        }
        else
        {
            GD.Print("[ContextSim] 7. FAIL — строка-кучка ×3 не найдена");
        }
        pass &= pileDropOk;

        // === 8. Esc-приоритет попапов ==============================
        win.OpenContextMenu(stoneSlot);
        bool escMenu = win.CloseTopmostPopup() && !win.IsContextMenuOpenForQA;
        win.OpenSplitDialog(stoneSlotId);
        bool escDlg = win.CloseTopmostPopup() && !win.IsSplitDialogOpenForQA && !win.IsContextMenuOpenForQA;
        bool escNothing = !win.CloseTopmostPopup(); // попапов нет → false (окно закрывает контроллер)
        GD.Print($"[ContextSim] 8. Esc-приоритет: меню закрылось={escMenu}, диалог закрылся={escDlg}, без попапов → false={escNothing}");
        pass &= escMenu && escDlg && escNothing;

        // === 9. R10 P1-SlotId: stale DROP при мутации между drag и drop ===
        // Сценарий ревью: start drag кучки B → между start/end удалена другая
        // кучка A (индексы сдвинулись!) → drop. По SlotId выбросится ИМЕННО B,
        // а не «другая кучка по старому индексу» и НЕ все кучки (старый
        // деструктивный фолбэк DropItemOnGround).
        {
            // Подготовка: одна кучка ×7 → детерминированный СЕРВИСНЫЙ сплит
            // ×7 → ×3 + ×4. (TryAddItem не годится: maxStack камня = 20 —
            // добавка 4 сливается в ОДНУ кучку ×11, двух кучек не будет;
            // первая версия шага именно на этом и падала.)
            {
                int idxPrep = FindSlotIndex(_inventory, StoneId);
                if (idxPrep >= 0 && _inventory.GetAllSlots()[idxPrep].Count >= 3)
                    _inventory.TrySplitSlot(idxPrep, 4); // ×7 → ×3 + ×4
            }
            win.RefreshExternally();
            await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);

            var slotsNow = _inventory.GetAllSlots();
            // B — ВТОРАЯ кучка (новая, ×4 — большая); A — первая (×3).
            int idxA = -1, idxB = -1;
            for (int i = 0; i < slotsNow.Count; i++)
            {
                if (slotsNow[i].ItemId != StoneId) continue;
                if (idxA < 0) idxA = i; else idxB = i;
            }
            bool staleDropOk = false;
            if (idxA >= 0 && idxB >= 0)
            {
                Guid idA = slotsNow[idxA].SlotId, idB = slotsNow[idxB].SlotId;
                int cntA = slotsNow[idxA].Count, cntB = slotsNow[idxB].Count;
                int totalBefore = _inventory.GetItemCount(StoneId);

                // Захват drag-data для B (кучка больше) — как это делает UI.
                var rowsNow = win.GetRowsForQA();
                InventoryItemRow? rowB = null;
                foreach (var r in rowsNow)
                    if (r.ItemIdForQA == StoneId && r.SlotIdForQA == idB) rowB = r;
                if (rowB != null)
                {
                    Variant staleDrag = rowB._GetDragData(Vector2.Zero);

                    // МУТАЦИЯ между drag и drop: удаляем A → индексы сдвинулись.
                    _inventory.TryRemoveFromSlot(idA, StoneId, cntA);

                    int groundBefore2 = _groundItems.Count;
                    trashZone._DropData(Vector2.Zero, staleDrag);
                    await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);

                    int totalAfter2 = _inventory.GetItemCount(StoneId);
                    int groundAfter2 = _groundItems.Count;
                    // B (cntB) выброшена; A уже удалена мутацией → осталось 0 камня;
                    // ключевой инвариант: удалено ровно cntB (не «все кучки», не «не та»).
                    staleDropOk = totalAfter2 == totalBefore - cntA - cntB
                        && groundAfter2 == groundBefore2 + 1;
                    GD.Print($"[ContextSim] 9. stale drop: кучка B×{cntB} захвачена, A×{cntA} удалена в полёте → выброшено ровно B (инвентарь {totalBefore}→{totalAfter2}, ожидали {totalBefore - cntA - cntB}), ground +1 = {groundAfter2 == groundBefore2 + 1}");
                }
                else
                {
                    GD.Print("[ContextSim] 9. FAIL — строка кучки B не найдена");
                }
            }
            else
            {
                GD.Print("[ContextSim] 9. FAIL — подготовка двух кучек не удалась");
            }
            pass &= staleDropOk;
        }

        // === 10. R10 P1-SlotId: stale SPLIT — исходная кучка исчезла → ОТКАЗ ===
        // Сценарий ревью: открыть Split для кучки → мутация удалила ЕЁ →
        // подтвердить → операция отклонена (не применена к чужому слоту).
        {
            if (_itemDb.TryGetItem(StoneId, out var stoneItem3) && stoneItem3 != null)
                _inventory.TryAddItem(stoneItem3, 6); // одна кучка ×6
            win.RefreshExternally();
            await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);

            int idxS = FindSlotIndex(_inventory, StoneId);
            bool staleSplitRefused = false;
            if (idxS >= 0)
            {
                var slotS = _inventory.GetAllSlots()[idxS];
                Guid idS = slotS.SlotId;
                int cntS = slotS.Count;
                int totalBeforeS = _inventory.GetItemCount(StoneId);

                win.OpenSplitDialog(idS);
                var dlgS = win.SplitDialogForQA;
                bool dlgSOpened = dlgS != null;

                if (dlgSOpened)
                {
                    // МУТАЦИЯ: исходная кучка удалена ДО подтверждения.
                    _inventory.TryRemoveFromSlot(idS, StoneId, cntS);

                    int pilesBefore = 0;
                    foreach (var s in _inventory.GetAllSlots())
                        if (s.ItemId == StoneId) pilesBefore++;

                    // Подтверждение по stale SlotId → ОТКАЗ.
                    bool applied = win.TrySplitSlotForDialog(idS, StoneId, 2);

                    int pilesAfter = 0;
                    foreach (var s in _inventory.GetAllSlots())
                        if (s.ItemId == StoneId) pilesAfter++;
                    int totalAfterS = _inventory.GetItemCount(StoneId);

                    staleSplitRefused = !applied
                        && dlgSOpened
                        && pilesAfter == pilesBefore
                        && totalAfterS == totalBeforeS - cntS; // только мутация, не сплит
                    win.CloseSplitDialog();
                    GD.Print($"[ContextSim] 10. stale split (кучка исчезла): подтверждение отклонено={(!applied)}, кучек {pilesBefore}→{pilesAfter} (не изменилось), тотал {totalBeforeS}→{totalAfterS} (сплит не применён к чужому слоту)");
                }
                else
                {
                    GD.Print("[ContextSim] 10. FAIL — диалог не открылся");
                }
            }
            else
            {
                GD.Print("[ContextSim] 10. FAIL — подготовка кучки не удалась");
            }
            pass &= staleSplitRefused;
        }

        // === 11. R10 P1-SlotId: split применяется к ИСХОДНОЙ кучке при сдвиге индексов ===
        // Сценарий: диалог для кучки B → удалена ДРУГАЯ кучка A (индексы
        // сдвинулись) → подтвердить → разделена именно B (по SlotId).
        {
            if (_itemDb.TryGetItem(StoneId, out var stoneItem4) && stoneItem4 != null)
                _inventory.TryAddItem(stoneItem4, 9); // ×9 одной кучкой (других кучек камня нет)
            // Вторая кучка ДРУГОГО предмета — Qi-пыль (для сдвига индексов).
            win.RefreshExternally();
            await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);

            int idxT = FindSlotIndex(_inventory, StoneId);
            bool splitTargetOk = false;
            if (idxT >= 0)
            {
                var slotT = _inventory.GetAllSlots()[idxT];
                Guid idT = slotT.SlotId;
                int cntT = slotT.Count;

                win.OpenSplitDialog(idT);
                var dlgT = win.SplitDialogForQA;
                if (dlgT != null)
                {
                    // Мутация: удаляем другую кучку (Qi-пыль) — индексы дрейфуют.
                    int qiIdx = FindSlotIndex(_inventory, QiDustId);
                    int qiCnt = qiIdx >= 0 ? _inventory.GetAllSlots()[qiIdx].Count : 0;
                    if (qiIdx >= 0)
                        _inventory.TryRemoveFromSlot(_inventory.GetAllSlots()[qiIdx].SlotId, QiDustId, qiCnt);

                    // Подтверждение: разделить 9 → 4+5 — должно примениться к idT.
                    bool appliedT = win.TrySplitSlotForDialog(idT, StoneId, 4);
                    int idxAfter = _inventory.FindSlotIndexBySlotId(idT);
                    int cntAfter = idxAfter >= 0 ? _inventory.GetAllSlots()[idxAfter].Count : 0;
                    int pilesT = 0;
                    foreach (var s in _inventory.GetAllSlots())
                        if (s.ItemId == StoneId) pilesT++;

                    splitTargetOk = appliedT && cntAfter == cntT - 4 && pilesT == 2
                        && _inventory.GetItemCount(StoneId) == cntT;
                    win.CloseSplitDialog();
                    GD.Print($"[ContextSim] 11. split при дрейфе индексов: применён к исходной кучке={appliedT} (×{cntT}→×{cntAfter} + ×4), кучек={pilesT} (2), тотал={_inventory.GetItemCount(StoneId)} (не изменился)");
                }
                else
                {
                    GD.Print("[ContextSim] 11. FAIL — диалог не открылся");
                }
            }
            else
            {
                GD.Print("[ContextSim] 11. FAIL — подготовка кучки не удалась");
            }
            pass &= splitTargetOk;
        }

        // === 12. R19: пилюля лечения — «Использовать» из ПКМ-меню =====
        // Сценарий пользователя: «Я поднял лекарство, у меня нет пояса,
        // должна быть возможность потребить его по правой кнопке».
        // Уронить торс → ПКМ → кнопка → счётчик −1, HP восстановлен.
        {
            int healSlot = FindSlotIndex(_inventory, HealPillId);
            bool healOk = false;
            if (healSlot >= 0 && _body != null && _itemDb!.TryGetItem(HealPillId, out _))
            {
                int pillCountBefore = _inventory.GetItemCount(HealPillId);
                int redFull = TotalRedHp();
                _body.ApplyDamage(BodyPartType.Torso, 15);
                int redDamaged = TotalRedHp();

                win.CloseContextMenu();
                win.OpenContextMenu(healSlot);
                var healMenu = win.ContextMenuForQA;
                bool healButton = healMenu?.UseButtonForQA != null;
                if (healMenu?.UseButtonForQA != null)
                {
                    healMenu.UseButtonForQA.EmitSignal(BaseButton.SignalName.Pressed);
                    await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
                }
                else
                {
                    win.CloseContextMenu();
                }

                int redHealed = TotalRedHp();
                int pillCountAfter = _inventory.GetItemCount(HealPillId);
                // Урон проходит через броню торса (стартовая броня L1) —
                // абсолютные числа не детерминированы; тело живёт —
                // регенерация тикает. Инварианты: урон применился (HP упал),
                // heal применился (HP вырос), счётчик −1 ровно.
                healOk = healButton
                    && redDamaged < redFull
                    && redHealed > redDamaged
                    && pillCountAfter == pillCountBefore - 1;
                GD.Print($"[ContextSim] 12. пилюля из ПКМ-меню: кнопка={healButton}, HP {redFull}→(урон)→{redDamaged}→(heal 30)→{redHealed}, счётчик {pillCountBefore}→{pillCountAfter}");
            }
            else
            {
                GD.Print("[ContextSim] 12. FAIL — пилюля лечения/тело не найдены");
            }
            pass &= healOk;
        }

        // === 13. R19: сгенерированное лекарство "Heal" (ЗАГЛАВНАЯ) =====
        // Баг-репродукция пользователя: генератор пишет EffectType="Heal",
        // старые потребители switch-ят по "heal" → лекарство НЕ ЛЕЧИЛО
        // (даже через пояс), а меню показывало сырой ключ «Heal +15».
        // Кейс-нормализация в IItemUseService должна это починить.
        {
            var capitalPill = new ItemData
            {
                ItemId = HealCapitalTestId,
                NameRu = "Тестовое лекарство (Heal)",
                NameEn = "Test Medicine (Heal)",
                Description = "Репродукция бага: EffectType с заглавной буквы",
                Category = ItemCategory.Consumable,
                ItemType = "Consumable",
                Rarity = ItemRarity.Common,
                Stackable = true,
                MaxStack = 10,
                Weight = 0.1f,
                Volume = 0.1f,
                Value = 4,
                HasDurability = false,
                Effects = new List<ItemEffect> { new ItemEffect { EffectType = "Heal", Value = 25 } },
            };
            _itemDb!.Register(capitalPill);
            _inventory!.TryAddItem(capitalPill, 2);
            win.RefreshExternally();
            await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);

            int capSlot = FindSlotIndex(_inventory, HealCapitalTestId);
            bool capitalOk = false;
            if (capSlot >= 0 && _body != null)
            {
                int capCountBefore = _inventory.GetItemCount(HealCapitalTestId);
                int redFull2 = TotalRedHp();
                _body.ApplyDamage(BodyPartType.Torso, 10);
                int redDamaged2 = TotalRedHp();

                win.CloseContextMenu();
                win.OpenContextMenu(capSlot);
                var capMenu = win.ContextMenuForQA;
                bool capButton = capMenu?.UseButtonForQA != null;
                if (capMenu?.UseButtonForQA != null)
                {
                    capMenu.UseButtonForQA.EmitSignal(BaseButton.SignalName.Pressed);
                    await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
                }
                else
                {
                    win.CloseContextMenu();
                }

                int redHealed2 = TotalRedHp();
                int capCountAfter = _inventory.GetItemCount(HealCapitalTestId);
                // Инварианты (броня/регенерация — относительные): урон
                // применился, heal 25 применился, счётчик −1.
                capitalOk = capButton
                    && redDamaged2 < redFull2
                    && redHealed2 > redDamaged2
                    && capCountAfter == capCountBefore - 1;
                GD.Print($"[ContextSim] 13. лекарство \"Heal\" (заглавная): кнопка={capButton} (кейс нормализован), HP {redFull2}→(урон)→{redDamaged2}→(heal 25)→{redHealed2}, счётчик {capCountBefore}→{capCountAfter}");
            }
            else
            {
                GD.Print("[ContextSim] 13. FAIL — тестовое лекарство не добавлено");
            }
            pass &= capitalOk;
        }

        // === 14. R19: материал — кнопки «Использовать» НЕТ ============
        // Маршрутизация: Material → не употребляется (крафт/алхимия).
        {
            int matSlot = FindSlotIndex(_inventory, StoneId);
            bool matNoButton = false;
            if (matSlot >= 0)
            {
                win.CloseContextMenu();
                win.OpenContextMenu(matSlot);
                matNoButton = win.ContextMenuForQA?.UseButtonForQA == null;
                win.CloseContextMenu();
            }
            GD.Print($"[ContextSim] 14. материал «Использовать» скрыт: {matNoButton} (ожидаем True — тип не используется напрямую)");
            pass &= matNoButton;
        }

        // === 15. R19: читаемость подокна ==============================
        // Регресс-гард бага «фон не рисовался»: панель была Panel с
        // CustomMinimumSize(400, 0) без контейнера — высота 0, StyleBox
        // не рисовался, текст плавал на тёмном бекдропе (контраст ~2.9:1).
        // Теперь: PanelContainer → реальная высота, фон непрозрачен,
        // контраст WCAG ≥ 4.5:1.
        {
            int rbSlot = FindSlotIndex(_inventory, HealPillId);
            bool readabilityOk = false;
            if (rbSlot >= 0)
            {
                win.CloseContextMenu();
                win.OpenContextMenu(rbSlot);
                var rbMenu = win.ContextMenuForQA;
                if (rbMenu != null)
                {
                    await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
                    float panelH = rbMenu.MenuPanelHeightForQA;
                    float bgAlpha = rbMenu.MenuPanelBgAlphaForQA;
                    float contrast = rbMenu.DescriptionContrastForQA;
                    readabilityOk = panelH > 100f && bgAlpha >= 0.95f && contrast >= 4.5f;
                    GD.Print($"[ContextSim] 15. читаемость: высота панели={panelH:F0}px (>100), альфа фона={bgAlpha:F2} (≥0.95), контраст текст/фон={contrast:F1}:1 (≥4.5)");
                }
                win.CloseContextMenu();
            }
            else
            {
                GD.Print("[ContextSim] 15. FAIL — пилюля для проверки читаемости не найдена");
            }
            pass &= readabilityOk;
        }

        // === HOLD для VLM-скриншота (GODOT_CONTEXT_HOLD=1) ==========
        if (System.Environment.GetEnvironmentVariable("GODOT_CONTEXT_HOLD") == "1")
        {
            // Открыть контекстное меню свойства камня (скриншот №1),
            // затем диалог разделения (скриншот №2, если задан _SPLIT).
            // GODOT_CONTEXT_MENU_ITEM (R19): какой предмет показывать в
            // меню скриншота — по itemId (default: камень). Пример:
            // con_pill_healing — снимок с кнопкой «Использовать».
            // NOTE: переменные _MENU/_SPLIT читает сам сим — GameBoot-автоматика
            // GODOT_SCREENSHOT здесь НЕ используется (она завершает процесс).
            string? shot = System.Environment.GetEnvironmentVariable("GODOT_SCREENSHOT_MENU");
            string? shotSplit = System.Environment.GetEnvironmentVariable("GODOT_SCREENSHOT_SPLIT");
            string? shotItemId = System.Environment.GetEnvironmentVariable("GODOT_CONTEXT_MENU_ITEM");
            string shotTarget = string.IsNullOrEmpty(shotItemId) ? StoneId : shotItemId!;
            if (!string.IsNullOrEmpty(shot))
            {
                win.OpenContextMenu(FindSlotIndex(_inventory, shotTarget));
                await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
                var img = GetViewport().GetTexture().GetImage();
                img.SavePng(shot);
                GD.Print($"[ContextSim] screenshot: {shot} (item {shotTarget})");
            }
            if (!string.IsNullOrEmpty(shotSplit))
            {
                win.CloseContextMenu();
                int shotSlot = FindSlotIndex(_inventory, StoneId);
                if (shotSlot >= 0)
                    win.OpenSplitDialog(_inventory.GetAllSlots()[shotSlot].SlotId);
                await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
                var img2 = GetViewport().GetTexture().GetImage();
                img2.SavePng(shotSplit);
                GD.Print($"[ContextSim] screenshot split: {shotSplit}");
            }
            GD.Print("[ContextSim] HOLD: меню/диалог открыты (VLM-скриншоты)");
        }

        GD.Print($"[ContextSim] VERDICT: {(pass ? "PASS — ПКМ-свойства, слайдер деления, кучки, слот-адресный выброс работают" : "FAIL")}");

        // QA-режим без HOLD: сим терминален — завершаем процесс сразу,
        // не греем таймаут обертки (раньше выход был только по SIGTERM).
        if (System.Environment.GetEnvironmentVariable("GODOT_CONTEXT_HOLD") != "1")
            GetTree().Quit();
    }

    private static int FindSlotIndex(IInventoryService inv, string itemId)
    {
        var slots = inv.GetAllSlots();
        for (int i = 0; i < slots.Count; i++)
            if (slots[i].ItemId == itemId)
                return i;
        return -1;
    }

    /// <summary>Суммарный «красный» HP игрока по всем частям (R19: heal-тесты).</summary>
    private int TotalRedHp()
    {
        int total = 0;
        var parts = _body?.GetAllParts();
        if (parts == null) return 0;
        foreach (var p in parts) total += p.CurrentRedHP;
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
