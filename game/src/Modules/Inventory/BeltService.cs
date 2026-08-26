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
// Эффекты расходников применяют IBodyService (heal) и IQiService
// (qi_restore) — сервисы игрока инжектятся напрямую (прецедент:
// InventoryModule инжектит сервисы того же контейнера).
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
/// </summary>
public sealed class BeltService : IDisposable
{
    public const int SlotCount = 7;          // хотбар 3-9
    public const int HotbarFirstIndex = 3;   // хотбар-индекс первого слота пояса

    [Inject] private readonly IInventoryService _inventory = null!;
    [Inject] private readonly IEquipmentService _equipment = null!;
    [Inject] private readonly IItemDatabaseService _itemDb = null!;
    [Inject] private readonly IBodyService _body = null!;
    [Inject] private readonly IQiService _qi = null!;

    [Inject] private readonly IPublisher<BeltSlotsChangedEvent> _slotsChangedPub = null!;
    [Inject] private readonly IPublisher<ConsumableUsedEvent> _usedPub = null!;
    [Inject] private readonly ISubscriber<EquipmentChangedEvent> _equipChangedSub = null!;

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

        if (!_inventory.TryAddItem(item, slot.Count))
        {
            // Инвентарь не принял даже с дропом — оставляем в слоте.
            return false;
        }

        slot.ItemId = string.Empty;
        slot.Count = 0;
        _slotsChangedPub.Publish(new BeltSlotsChangedEvent(slotIndex, string.Empty, 0));
        return true;
    }

    /// <summary>
    /// Использовать расходник из слота (клавиша хотбара или клик).
    /// Применяет все Effects предмета, списывает 1 шт.
    /// </summary>
    public bool Use(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return false;

        var slot = _slots[slotIndex];
        if (slot.Count <= 0 || string.IsNullOrEmpty(slot.ItemId)) return false;
        if (!_itemDb.TryGetItem(slot.ItemId, out var item)) return false;

        foreach (var effect in item.Effects)
            ApplyEffect(effect);

        slot.Count--;
        if (slot.Count <= 0)
        {
            slot.ItemId = string.Empty;
            slot.Count = 0;
        }

        foreach (var effect in item.Effects)
            _usedPub.Publish(new ConsumableUsedEvent(item.ItemId, effect.EffectType, effect.Value));

        _slotsChangedPub.Publish(new BeltSlotsChangedEvent(slotIndex, slot.ItemId, slot.Count));
        return true;
    }

    /// <summary>
    /// Применить один эффект расходника. V1: heal (распределение по раненым
    /// частям), qi_restore. Неизвестные типы эффектов — заглушка (будущие фазы).
    /// </summary>
    private void ApplyEffect(ItemEffect effect)
    {
        switch (effect.EffectType)
        {
            case "heal":
            {
                int remaining = (int)effect.Value;
                // Лечим самые повреждённые части, пока хватает эффекта.
                var parts = _body.GetAllParts();
                if (parts != null)
                {
                    var ordered = new List<BodyPartData>(parts);
                    ordered.Sort(static (a, b) => a.CurrentRedHP.CompareTo(b.CurrentRedHP));
                    foreach (var p in ordered)
                    {
                        if (remaining <= 0) break;
                        int missing = p.MaxRedHP - p.CurrentRedHP;
                        if (missing <= 0) continue;
                        int amount = Math.Min(missing, remaining);
                        _body.HealPart(p.Type, amount);
                        remaining -= amount;
                    }
                }
                break;
            }
            case "qi_restore":
                // ЗАПРЕТ 2: Qi = long. Value расходника — float из данных, кастуем.
                _qi.AddQi((long)effect.Value);
                break;
            default:
                // teleport / vitality_boost и др. — будущие фазы (генераторы/мир).
                break;
        }
    }

    public void Dispose()
    {
        _equipChangedToken?.Dispose();
        _equipChangedToken = null;
    }
}
