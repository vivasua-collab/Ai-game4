#nullable enable
// Создано: 2026-09-08 — Review этап 4: headless-верификация инвентарных
// транзакций (GODOT_STORAGE_DEBUG=1). Проверяет:
//   1. SpiritStorage: TryRetrieve возвращает РЕАЛЬНЫЙ ItemData (P0-1),
//      stackable-стекинг не жжёт слоты (P1-2), кэш = сумме по стекам.
//   2. StorageRing: операции реально списывают Ци (P1-3); unknown-item
//      не извлекается (P0-1-паттерн).
//   3. Крафт: overflow результата падает на землю, а не исчезает (P1-4).
//   4. Pickup: unknown-item остаётся на земле (P1-5); граничная дистанция
//      ровно maxDistance подбирается (P2-6).
// Запуск: GODOT_NEWGAME=1 GODOT_STORAGE_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn
using Godot;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Adapter.Di;
using CultivationGame.Modules.Inventory;

namespace CultivationGame.Adapter.Scene;

public partial class StorageSimDebug : Node
{
    [Inject] private ISpiritStorageService? _spiritStorage;
    [Inject] private IStorageRingService? _ringStorage;
    [Inject] private IQiService? _qiService;
    [Inject] private IGroundItemService? _groundItems;
    [Inject] private IItemDatabaseService? _itemDb;
    [Inject] private IInventoryService? _inventory;
    [Inject] private IPublisher<CraftCompletedEvent>? _craftCompletedPub;
    [Inject] private ISubscriber<ItemDroppedEvent>? _droppedSub;

    private System.IDisposable? _droppedToken;
    private long _lastDropId;
    private string _lastDroppedItemId = "";
    private int _lastDroppedCount;

    private const string PlayerQiId = "player";

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

        GD.Print("[StorageSim] Ready — inventory transaction verification starts in 2s");
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

        if (_spiritStorage == null || _ringStorage == null || _qiService == null
            || _groundItems == null || _itemDb == null || _inventory == null
            || _craftCompletedPub == null)
        {
            GD.Print("[StorageSim] FAIL — DI not wired");
            PrintVerdict(false);
            return;
        }

        bool pass = true;

        // === 1. SpiritStorage (P0-1 + P1-2) ==============================
        // Стекинг: 3 stackable-предмета → ОДИН слот (раньше 3 слота).
        if (!_itemDb.TryGetItem(Modules.Combat.CombatRangeGateService.ArrowItemId, out var arrowItem)
            || arrowItem == null)
        {
            GD.Print("[StorageSim] FAIL — ammo_arrow not in ItemDatabase (StartingGearPhase?)");
            PrintVerdict(false);
            return;
        }

        // Придать игроку Ци (доступ стоит 10; NewGame может стартовать с малым Ци).
        long qiBoost = 500;
        _qiService.AddQi(qiBoost);
        long qiBefore = _qiService.CurrentQi;

        bool s1 = _spiritStorage.TryStore(arrowItem);
        bool s2 = _spiritStorage.TryStore(arrowItem);
        bool s3 = _spiritStorage.TryStore(arrowItem);
        int slots = _spiritStorage.UsedSlots;
        var contents = _spiritStorage.GetStoredItems();
        int stacked = contents.Count > 0 ? contents[0].Count : 0;
        GD.Print($"[StorageSim] spirit store x3: {s1}/{s2}/{s3}, slots={slots} (ожидаем 1), stackCount={stacked} (ожидаем 3)");
        pass &= s1 && s2 && s3 && slots == 1 && stacked == 3;

        // Извлечение возвращает РЕАЛЬНЫЙ предмет (P0-1: раньше null!).
        bool retrieved = _spiritStorage.TryRetrieve(arrowItem.ItemId, out var gotItem);
        bool gotRealItem = retrieved && gotItem != null && gotItem.ItemId == arrowItem.ItemId;
        int slotsAfter = _spiritStorage.UsedSlots;
        GD.Print($"[StorageSim] spirit retrieve: ok={retrieved}, item={(gotItem != null ? gotItem.ItemId : "NULL")} (P0-1), slots={slotsAfter} (ожидаем 1)");
        pass &= gotRealItem && slotsAfter == 1;

        // Ци реально списалась за операции (4 × accessCost=10).
        long qiDelta = qiBefore - _qiService.CurrentQi;
        long expectedCost = 4 * _spiritStorage.GetAccessCost();
        GD.Print($"[StorageSim] spirit Qi: -{qiDelta} (ожидаем {expectedCost})");
        pass &= qiDelta == expectedCost;

        // === 2. StorageRing (P1-3) =======================================
        const string testRing = "qa_test_ring";
        _ringStorage.ActivateRing(testRing, 1, 4);
        long qiBeforeStore = _qiService.CurrentQi;
        bool stored = _ringStorage.TryStore(testRing, arrowItem, out long storeCost);
        long qiAfterStore = _qiService.CurrentQi;
        bool storeCharged = stored && qiBeforeStore - qiAfterStore == storeCost && storeCost > 0;
        GD.Print($"[StorageSim] ring store: ok={stored}, cost={storeCost}, charged={qiBeforeStore - qiAfterStore} (P1-3: списание реальное)");
        pass &= storeCharged;

        long qiBeforeRetrieve = _qiService.CurrentQi;
        bool ringRetrieved = _ringStorage.TryRetrieve(testRing, arrowItem.ItemId, out var ringItem, out long retrieveCost);
        long qiAfterRetrieve = _qiService.CurrentQi;
        bool retrieveCharged = ringRetrieved && qiBeforeRetrieve - qiAfterRetrieve == retrieveCost && retrieveCost > 0;
        bool ringItemReal = ringRetrieved && ringItem != null && ringItem.ItemId == arrowItem.ItemId;
        GD.Print($"[StorageSim] ring retrieve: ok={ringRetrieved}, item={(ringItem != null ? ringItem.ItemId : "NULL")}, cost={retrieveCost}, charged={qiBeforeRetrieve - qiAfterRetrieve}");
        pass &= retrieveCharged && ringItemReal;

        // Unknown-item из кольца не извлекается (P0-1-паттерн).
        _ringStorage.TryStore(testRing, arrowItem, out _);
        bool unknownRetrieved = _ringStorage.TryRetrieve(testRing, "totally_unknown_item_qa", out var unknownItem, out _);
        GD.Print($"[StorageSim] ring retrieve unknown: ok={unknownRetrieved} (ожидаем False, item остаётся)");
        pass &= !unknownRetrieved;

        // === 3. Крафт overflow (P1-4) ====================================
        // CraftCompletedEvent с гигантским количеством: TryAddItem упрётся в
        // объём → остаток должен упасть на землю (ItemDroppedEvent), а не исчезнуть.
        _lastDropId = 0;
        _craftCompletedPub.Publish(new CraftCompletedEvent("qa_recipe", arrowItem.ItemId, 100000));
        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        bool overflowDropped = _lastDropId > 0 && _lastDroppedItemId == arrowItem.ItemId;
        int invArrows = _inventory.GetItemCount(arrowItem.ItemId);
        GD.Print($"[StorageSim] craft overflow: dropped={overflowDropped} (dropId={_lastDropId}, itemId='{_lastDroppedItemId}', count={_lastDroppedCount}), inventory arrows={invArrows}");
        pass &= overflowDropped;

        // === 4. Pickup unknown (P1-5) + граница (P2-6) ===================
        // Центр подбора вдали от игрока и от предметов других фаз.
        // NOTE: на земле уже лежит overflow-дроп фазы 3 (крафт) — учитываем
        // его в ожидании (2 = unknown + overflow-дроп).
        float px = 1000f, py = 1000f;
        int groundBefore = _groundItems.Count;
        _groundItems.DropItem("totally_unknown_item_qa", 1, px, py);
        bool unknownPicked = _groundItems.TryPickupNearest(px, py, 50f);
        int groundCount = _groundItems.Count;
        bool unknownRemained = groundCount == groundBefore + 1;
        GD.Print($"[StorageSim] pickup unknown: picked={unknownPicked} (ожидаем False), ground items={groundCount} (было {groundBefore}; unknown остался: {unknownRemained})");
        pass &= !unknownPicked && unknownRemained;

        // Граничная дистанция: предмет РОВНО на maxDistance подбирается (P2-6).
        // Отдельный центр (вдали от unknown-предмета, который остался на земле).
        float bx = 5000f, by = 5000f;
        _groundItems.DropItem(arrowItem.ItemId, 1, bx + 30f, by); // ровно 30 при maxDistance=30
        bool boundaryPicked = _groundItems.TryPickupNearest(bx, by, 30f);
        GD.Print($"[StorageSim] pickup boundary: picked={boundaryPicked} (ожидаем True — строго граничная дистанция)");
        pass &= boundaryPicked;

        PrintVerdict(pass);
    }

    private static void PrintVerdict(bool pass)
    {
        GD.Print($"[StorageSim] VERDICT: {(pass ? "PASS — spirit retrieve/stacking, ring Qi, craft overflow, pickup integrity" : "FAIL")}");
    }
}
