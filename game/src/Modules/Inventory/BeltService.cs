#nullable enable
// Создано: 2026-08-22 — слоты быстрого доступа пояса.
// BeltService — 7 слотов расходников (хотбар 3-9 по HOTKEYS.md §8),
// активны ТОЛЬКО когда надет пояс (EquipmentSlot.Belt). Расходники
// кладутся из инвентаря (drag&drop в InventoryWindow), используются
// клавишами 3-9 или кликом по HotbarPanel.
//
// Архитектура (EVT-01): кросс-модульные эффекты — через EventBus:
//   - EquipmentChangedEvent (подписка) → гейт пояса
//   - BeltSlotsChangedEvent / ConsumableUsedEvent (публикация) → UI/модули
// R19 (2026-09-19): применение эффектов расходников — ДЕЛЕГИРОВАНО
// IItemUseService (единая маршрутизация по типу предмета + кейс-
// нормализация ключей). Пояс отвечает только за хранение слотов и списание.
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Inventory;

/// <summary>Один слот пояса: стек одного расходника.</summary>
public sealed class BeltSlot
{
    public string ItemId = string.Empty;
    public int Count;
}
/// <summary>
/// Слоты быстрого доступа пояса. Слоты 0-6 соответствуют хотбару 3-9.
/// Без пояса все операции Assign/Use возвращают false, слоты пусты.
/// При снятии пояса содержимое возвращается в инвентарь (overflow — на землю).
///
/// R17 (аудит-0911 INV-4): содержимое пояса НЕ сохранялось и НЕ сбрасывалось
/// при пересборке мира → те же дюп/потери, что у куклы (предмет вне сейва и
/// вне инвентаря). Теперь: блок "belt" (слоты itemId+count; предметы в поясе
/// ИЗЪЯТЫ из инвентаря — TryAssign списывает, restore кладёт обратно в слот
/// без инвентаря) + IWorldResettable.
/// </summary>
public sealed class BeltService : ISaveable, IWorldResettable, IDisposable
{
    public const int SlotCount = 7;          // хотбар 3-9
    public const int HotbarFirstIndex = 3;   // хотбар-индекс первого слота пояса

    [Inject] private readonly IInventoryService _inventory = null!;
    [Inject] private readonly IEquipmentService _equipment = null!;
    [Inject] private readonly IItemDatabaseService _itemDb = null!;

    [Inject] private readonly IPublisher<BeltSlotsChangedEvent> _slotsChangedPub = null!;
    [Inject] private readonly IPublisher<ConsumableUsedEvent> _usedPub = null!;
    // AUDIT-0921 A4 (belt-путь): тосты отказа — пояс не может съесть
    // предмет с нереализованным эффектом молча.
    [Inject] private readonly IPublisher<Core.Messaging.Contracts.ToastShownEvent> _toastPub = null!;
    [Inject] private readonly ISubscriber<EquipmentChangedEvent> _equipChangedSub = null!;

    // R19: единая маршрутизация эффектов (кейс-нормализация "Heal"→"heal",
    // будущие типы) — ItemUseService модуля Inventory (тот же контейнер).
    [Inject] private readonly IItemUseService _itemUse = null!;

    private IDisposable? _equipChangedToken;

    private readonly BeltSlot[] _slots = CreateSlots();

    private static BeltSlot[] CreateSlots()
    {
        var arr = new BeltSlot[SlotCount];
        for (int i = 0; i < SlotCount; i++) arr[i] = new BeltSlot();
        return arr;
    }

    /// <summary>Пояс надет — слоты активны (UI показывает ряд пояса).</summary>
    public bool IsBeltEquipped => _equipment?.GetEquipped(EquipmentSlot.Belt) != null;

    public void Initialize()
    {
        _equipChangedToken?.Dispose();
        _equipChangedToken = _equipChangedSub.Subscribe(OnEquipmentChanged);
    }

    private void OnEquipmentChanged(in EquipmentChangedEvent e)
    {
        if (e.Slot != EquipmentSlot.Belt) return;

        // Пояс сняли — вернуть содержимое в инвентарь, слоты очистить.
        if (e.ItemId is null || e.ItemId.Length == 0)
        {
            for (int i = 0; i < SlotCount; i++)
                ReturnSlotToInventory(i);
        }
    }

    /// <summary>Snapshot слотов для UI (не мутируется извне — копия).</summary>
    public IReadOnlyList<BeltSlot> GetSlots()
    {
        var copy = new List<BeltSlot>(SlotCount);
        foreach (var s in _slots)
            copy.Add(new BeltSlot { ItemId = s.ItemId, Count = s.Count });
        return copy;
    }

    /// <summary>
    /// Положить расходник из инвентаря в слот пояса. Переносит весь доступный
    /// стек (до MaxStack слота). Возвращает перенесённое количество.
    /// </summary>
    public int TryAssign(int slotIndex, string itemId)
    {
        if (!IsBeltEquipped) return 0;
        if (slotIndex < 0 || slotIndex >= SlotCount || string.IsNullOrEmpty(itemId)) return 0;
        if (!_itemDb.TryGetItem(itemId, out var item)) return 0;
        if (item.Category != ItemCategory.Consumable) return 0;

        var slot = _slots[slotIndex];
        if (slot.Count > 0 && slot.ItemId != itemId) return 0; // слот занят другим

        int available = _inventory.GetItemCount(itemId);
        if (available <= 0) return 0;

        int capacity = item.MaxStack > 0 ? item.MaxStack : 1;
        int toMove = Math.Min(available, capacity - slot.Count);
        if (toMove <= 0) return 0;

        if (!_inventory.TryRemoveItem(itemId, toMove)) return 0;

        slot.ItemId = itemId;
        slot.Count += toMove;
        _slotsChangedPub.Publish(new BeltSlotsChangedEvent(slotIndex, slot.ItemId, slot.Count));
        return toMove;
    }

    /// <summary>Вернуть содержимое слота в инвентарь (клик правой/удаление).</summary>
    public bool TryTakeBack(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return false;
        return ReturnSlotToInventory(slotIndex);
    }

    private bool ReturnSlotToInventory(int slotIndex)
    {
        var slot = _slots[slotIndex];
        if (slot.Count <= 0) return false;
        if (!_itemDb.TryGetItem(slot.ItemId, out var item)) return false;

        // AUDIT-0911 INV-3 FIX: раньше 2-arg TryAddItem возвращал true даже при
        // ЧАСТИЧНОМ добавлении (volume limit), а слот пояса очищался ЦЕЛИКОМ —
        // (count − addedCount) расходников терялись бесследно. Теперь остаток
        // остаётся в слоте пояса (валидное хранилище), инвентарь получает
        // столько, сколько реально влезло.
        if (!_inventory.TryAddItem(item, slot.Count, out int addedCount) || addedCount <= 0)
        {
            // Инвентарь ничего не принял — оставляем в слоте.
            return false;
        }

        if (addedCount < slot.Count)
        {
            // Частичный возврат: остаток живёт в слоте пояса.
            slot.Count -= addedCount;
            _slotsChangedPub.Publish(new BeltSlotsChangedEvent(slotIndex, slot.ItemId, slot.Count));
            return true;
        }

        slot.ItemId = string.Empty;
        slot.Count = 0;
        _slotsChangedPub.Publish(new BeltSlotsChangedEvent(slotIndex, string.Empty, 0));
        return true;
    }

    /// <summary>
    /// Использовать расходник из слота (клавиша хотбара или клик).
    /// Эффекты применяет IItemUseService (R19: единая маршрутизация по типу
    /// предмета — та же точка правды, что и ПКМ-«Использовать» в инвентаре),
    /// списывает 1 шт. из слота пояса.
    /// </summary>
    public bool Use(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return false;

        var slot = _slots[slotIndex];
        if (slot.Count <= 0 || string.IsNullOrEmpty(slot.ItemId)) return false;
        if (!_itemDb.TryGetItem(slot.ItemId, out var item)) return false;

        // AUDIT-0921 A4 (P1, belt-путь): гейт реализуемости ДО списания —
        // тот же контракт, что и ItemUseService.TryUseFromInventory
        // (пояс/хотбар прежде мог съесть teleport/vitality_boost-material
        // без эффекта; камни Ци — A6: «только зарядник H»).
        var info = _itemUse.GetUseInfo(item);
        if (!info.Usable)
        {
            _toastPub?.Publish(new Core.Messaging.Contracts.ToastShownEvent(
                info.UnusableReason, 2.5f));
            Console.WriteLine($"[Belt] Use refused (no supported effect): {slot.ItemId} — {info.UnusableReason}");
            return false;
        }

        // R19: применение эффектов — делегирование ItemUseService
        // (раньше — приватный ApplyEffect: heal/qi_restore с РЕГИСТРО-
        // чувствительными ключами — сгенерированные "Heal"-лекарства
        // не лечили; теперь кейс нормализуется в одном месте).
        var applied = _itemUse.ApplyEffects(item);

        slot.Count--;
        if (slot.Count <= 0)
        {
            slot.ItemId = string.Empty;
            slot.Count = 0;
        }

        foreach (var fx in applied.Items)
            _usedPub.Publish(new ConsumableUsedEvent(item.ItemId, fx.EffectType, fx.Value));

        _slotsChangedPub.Publish(new BeltSlotsChangedEvent(slotIndex, slot.ItemId, slot.Count));
        return true;
    }

    public void Dispose()
    {
        _equipChangedToken?.Dispose();
        _equipChangedToken = null;
    }

    // ══════════════════════════════════════════════════════════════
    // R17 (INV-4): ISaveable — блок "belt"
    // ══════════════════════════════════════════════════════════════

    /// <summary>Типизированный state-блок для round-trip десериализации.</summary>
    public sealed class BeltSaveState
    {
        public List<BeltSaveEntry> Slots = new();
    }

    public sealed class BeltSaveEntry
    {
        public int SlotIndex;
        public string ItemId = "";
        public int Count;
    }

    public string SaveKey => "belt";
    public Type StateType => typeof(BeltSaveState);

    public object CaptureState()
    {
        var data = new BeltSaveState();
        for (int i = 0; i < SlotCount; i++)
        {
            if (_slots[i].Count <= 0 || string.IsNullOrEmpty(_slots[i].ItemId)) continue;
            data.Slots.Add(new BeltSaveEntry
            {
                SlotIndex = i,
                ItemId = _slots[i].ItemId,
                Count = _slots[i].Count,
            });
        }
        return data;
    }

    public void RestoreState(object state)
    {
        if (state is not BeltSaveState data || data == null) return;

        // Очистить текущее содержимое БЕЗ возврата в инвентарь: сейв-инвентарь
        // сам по себе полный источник истины (в поясе предметы уже изъяты).
        for (int i = 0; i < SlotCount; i++)
        {
            _slots[i].ItemId = string.Empty;
            _slots[i].Count = 0;
        }

        int restored = 0;
        foreach (var entry in data.Slots)
        {
            if (entry.SlotIndex < 0 || entry.SlotIndex >= SlotCount) continue;
            if (string.IsNullOrEmpty(entry.ItemId) || entry.Count <= 0) continue;
            // Фантомный ID (старый сейв без item_db) — слот остаётся пустым;
            // Use() лениво резолвит предмет, здесь проверяем каталог.
            if (!_itemDb.TryGetItem(entry.ItemId, out _))
            {
                Console.WriteLine($"[BeltService] RestoreState: предмет '{entry.ItemId}' не найден в каталоге — слот {entry.SlotIndex} пропущен");
                continue;
            }
            _slots[entry.SlotIndex].ItemId = entry.ItemId;
            _slots[entry.SlotIndex].Count = entry.Count;
            _slotsChangedPub.Publish(new BeltSlotsChangedEvent(entry.SlotIndex, entry.ItemId, entry.Count));
            restored++;
        }

        Console.WriteLine($"[BeltService] RestoreState: {restored}/{data.Slots.Count} слотов пояса");
    }

    // R17 (E-1): IWorldResettable — пересборка мира = пустой пояс.
    // Возврата в инвентарь НЕТ: ResetWorld вызывается до RestoreState
    // (LoadGame) или до StartingGearPhase (NewGame) — инвентарь будет
    // наполнен заново из сейва/стартового набора.
    public void ResetWorld()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (_slots[i].Count <= 0) continue;
            _slots[i].ItemId = string.Empty;
            _slots[i].Count = 0;
            _slotsChangedPub.Publish(new BeltSlotsChangedEvent(i, string.Empty, 0));
        }
    }
}
