#nullable enable
// Создано: 2026-09-08 — баг-репорт пользователя: «не могу перетащить камень
// на иконку корзина, чтобы выбросить». Headless-верификация (GODOT_TRASHDROP_DEBUG=1):
//   1. Инвентарная строка МАТЕРИАЛА даёт drag-data (баг: _GetDragData
//      возвращал пустой Variant для не-экипировки → drag не стартовал).
//   2. Зона «Выбросить» принимает drag (CanDropData) и выбрасывает предмет:
//      инвентарь минус, ItemDroppedEvent плюс, ground items плюс.
//   3. Кукла отклоняет материал (HandleDropOnSlot → false) — надеть нельзя.
//   4. Экипировка по-прежнему даёт drag-data (регрессия не сломана).
//   5. Корзина отвергает чужой source (doll:...) — drop не пройдёт.
// NOTE: прямой вызов _GetDragData вне реального GUI-drag даёт безвредный
// engine-ERROR «!gui_is_dragging» при SetDragPreview — шум теста, не игры
// (в реальном использовании _GetDragData вызывает сам движок во время drag).
// Запуск: GODOT_NEWGAME=1 GODOT_TRASHDROP_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn
using Godot;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Adapter.Di;
using CultivationGame.Adapter.UI;

namespace CultivationGame.Adapter.Scene;

public partial class TrashDropSimDebug : Node
{
    [Inject] private IInventoryService? _inventory;
    [Inject] private IItemDatabaseService? _itemDb;
    [Inject] private IGroundItemService? _groundItems;
    [Inject] private ISubscriber<ItemDroppedEvent>? _droppedSub;

    private System.IDisposable? _droppedToken;
    private long _lastDropId;
    private string _lastDroppedItemId = "";
    private int _lastDroppedCount;

    private const string StoneId = "material_stone";

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        _droppedToken = _droppedSub?.Subscribe((in ItemDroppedEvent e) =>
        {
            _lastDropId = e.DropId;
            _lastDroppedItemId = e.ItemId;
            _lastDroppedCount = e.Count;
        });

        GD.Print("[TrashDropSim] Ready — инвентарный drag→корзина тест старт через 2с");
        _ = RunSequenceAsync();
    }

    public override void _ExitTree()
    {
        _droppedToken?.Dispose();
        _droppedToken = null;
    }

    private async System.Threading.Tasks.Task RunSequenceAsync()
    {
        await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);

        var world = GetParent() as GameWorldController ?? FindWorld(GetTree().Root);
        if (world == null)
        {
            GD.Print("[TrashDropSim] VERDICT: FAIL — GameWorldController not found");
            return;
        }

        if (_inventory == null || _itemDb == null || _groundItems == null)
        {
            GD.Print("[TrashDropSim] VERDICT: FAIL — DI not wired");
            return;
        }

        var win = world.InventoryWindowForQA;
        if (win == null)
        {
            GD.Print("[TrashDropSim] VERDICT: FAIL — InventoryWindow not found");
            return;
        }

        bool pass = true;

        // === 1. Материал даёт drag-data (багфикс) ======================
        if (!_itemDb.TryGetItem(StoneId, out var stone) || stone == null)
        {
            GD.Print($"[TrashDropSim] VERDICT: FAIL — {StoneId} нет в ItemDatabase");
            return;
        }

        // Положить 5 камней в инвентарь (как будто намайнил).
        if (!_inventory.TryAddItem(stone, 5))
        {
            GD.Print("[TrashDropSim] VERDICT: FAIL — не удалось добавить камни в инвентарь");
            return;
        }

        // Открываем окно (RefreshItems вызывается только при открытии —
        // RefreshExternally имеет гард _isVisible).
        win.Toggle();
        await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);

        var stoneRow = win.FindRowForQA(StoneId);
        if (stoneRow == null)
        {
            GD.Print("[TrashDropSim] VERDICT: FAIL — строка камня не найдена в окне");
            return;
        }

        // Главный баг: раньше пустой Variant → drag не начинался ВООБЩЕ.
        Variant dragData = stoneRow._GetDragData(Vector2.Zero);
        bool dragStarts = dragData.VariantType == Variant.Type.Dictionary
            && CharacterDollPanel.TryParseDragData(dragData, out var draggedId, out var source)
            && draggedId == StoneId && source == "inventory";
        GD.Print($"[TrashDropSim] 1. материал {StoneId} drag-data: {(dragStarts ? "есть (source=inventory)" : "НЕТ — баг воспроизводится")} — было пусто до фикса");
        pass &= dragStarts;

        // === 2. Корзина принимает и выбрасывает ========================
        var trashZone = win.FindTrashZoneForQA();
        if (trashZone == null)
        {
            GD.Print("[TrashDropSim] VERDICT: FAIL — TrashDropZone не найдена");
            return;
        }

        bool canDrop = trashZone._CanDropData(Vector2.Zero, dragData);
        GD.Print($"[TrashDropSim] 2a. CanDropData(материал): {canDrop} (ожидаем True)");
        pass &= canDrop;

        int groundBefore = _groundItems.Count;
        _lastDropId = 0;
        int stonesBefore = _inventory.GetItemCount(StoneId);

        trashZone._DropData(Vector2.Zero, dragData);
        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);

        int stonesAfter = _inventory.GetItemCount(StoneId);
        int groundAfter = _groundItems.Count;
        // DropItemOnGround выбрасывает ВЕСЬ стек (дизайн): count = stonesBefore
        // (стартовые 5 + тестовые 5 = 10).
        bool droppedOnGround = _lastDropId > 0 && _lastDroppedItemId == StoneId
            && _lastDroppedCount == stonesBefore;
        bool removedFromInventory = stonesAfter == 0;
        bool groundIncremented = groundAfter == groundBefore + 1;
        GD.Print($"[TrashDropSim] 2b. DropData: инвентарь {stonesBefore}→{stonesAfter} (ожидаем →0), " +
                  $"ItemDroppedEvent={(droppedOnGround ? $"dropId={_lastDropId} ×{_lastDroppedCount}" : "НЕТ")}, " +
                  $"ground items {groundBefore}→{groundAfter}");
        pass &= removedFromInventory && droppedOnGround && groundIncremented;

        // === 3. Кукла отклоняет материал ===============================
        var dollPanel = win.GetDollPanel();
        bool dollRejected = true;
        if (dollPanel != null)
        {
            dollRejected = !dollPanel.HandleDropOnSlot(EquipmentSlot.Torso, stone);
        }
        GD.Print($"[TrashDropSim] 3. кукла отклоняет камень: {dollRejected} (ожидаем True — не экипировка)");
        pass &= dollRejected;

        // === 4. Экипировка всё ещё draggable (регрессия) ================
        // Экипировка генерруется с динамическими ID (сид StartingGearPhase):
        // ищем первый ItemData-экипировки среди слотов инвентаря.
        string? equipId = null;
        foreach (var invSlot in _inventory.GetAllSlots())
        {
            if (_itemDb.TryGetItem(invSlot.ItemId, out var invItem) && invItem is EquipmentData)
            {
                equipId = invSlot.ItemId;
                break;
            }
        }

        if (equipId != null)
        {
            var equipRow = win.FindRowForQA(equipId);
            Variant equipDrag = equipRow != null
                ? equipRow._GetDragData(Vector2.Zero)
                : new Variant();
            bool equipDragStarts = CharacterDollPanel.TryParseDragData(equipDrag, out var eqId, out var eqSource)
                && eqId == equipId && eqSource == "inventory";
            GD.Print($"[TrashDropSim] 4. экипировка {equipId} drag-data: {equipDragStarts} (регрессия не сломана)");
            pass &= equipDragStarts;
        }
        else
        {
            GD.Print("[TrashDropSim] 4. экипировки в инвентаре нет — пропуск (не FAIL)");
        }

        // === 5. Корзина отвергает чужой source ==========================
        var fakeDict = CharacterDollPanel.CreateDragData(stone, "doll:Torso");
        bool foreignRejected = !trashZone._CanDropData(Vector2.Zero, fakeDict);
        GD.Print($"[TrashDropSim] 5. корзина отвергает source='doll:Torso': {foreignRejected} (ожидаем True)");
        pass &= foreignRejected;

        GD.Print($"[TrashDropSim] VERDICT: {(pass ? "PASS — материалы draggable, корзина выбрасывает, кукла отклоняет не-экипировку" : "FAIL")}");
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
