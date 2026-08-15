#nullable enable
// Создано: 2026-05-09 00:00:00 UTC
// Редактировано: 2026-05-09 — INV-04: заменена прямая инъекция IBodyService на событийную модель.
// Редактировано: 2026-05-09 — EVT-02: EquipmentChangedEvent публикуется с TotalArmor.
// АРХИТЕКТУРА: VContainer sibling scopes НЕ видят регистрации друг друга.
// Межмодульное общение — ТОЛЬКО через MessagePipe (Hub-and-Spoke).
// EquipmentService подписывается на BodyPartSeveredEvent и кэширует заблокированные слоты.
// Реализация IEquipmentService.
// КРИТИЧЕСКАЯ СВЯЗЬ: Body→Equipment — ампутация блокирует слот экипировки.
// Заменяет legacy EquipmentController.cs (1418 LOC) — God Object разделён.
using System;
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Inventory
{
    /// <summary>
    /// Реализация IEquipmentService.
    /// Управляет экипировкой персонажа: надевание, снятие, проверка слотов.
    ///
    /// КРИТИЧЕСКАЯ СВЯЗЬ: Body→Equipment
    /// - При ампутации части тела слот экипировки блокируется
    /// - Подписывается на BodyPartSeveredEvent для автоснятия экипировки
    /// - QI-C01 урок: подписка на кросс-модульные события
    ///
    /// INV-04: НЕ инжектит IBodyService напрямую.
    /// АРХИТЕКТУРА: VContainer sibling scopes не видят регистрации друг друга.
    /// Межмодульное общение — ТОЛЬКО через MessagePipe (Hub-and-Spoke).
    /// Вместо этого EquipmentService кэширует заблокированные слоты на основе событий.
    /// </summary>
    public class EquipmentService : IEquipmentService, IDisposable
    {
        // === Зависимости (DI через конструктор) ===
        private readonly IPublisher<EquipmentChangedEvent> _equipChangedPub;
        private readonly IPublisher<EquipmentBlockedEvent> _equipBlockedPub;
        private readonly ISubscriber<BodyPartSeveredEvent> _severedSub;

        // === Состояние ===
        private readonly Dictionary<EquipmentSlot, EquipmentData> _equipment = new();

        // INV-04: Локальный кэш заблокированных слотов.
        // Заполняется из BodyPartSeveredEvent (MessagePipe).
        // BodyPartSeveredEvent содержит BlockedSlots — массив EquipmentSlot.
        private readonly HashSet<EquipmentSlot> _blockedSlots = new();

        private string _entityId;
        private IDisposable _severedSubscription;

        // === Свойства ===
        public string EntityId => _entityId;
        public bool IsTwoHandEquipped => EquipmentStatAggregator.IsTwoHandEquipped(_equipment);

        // === Конструктор (VContainer) ===
        public EquipmentService(
            IPublisher<EquipmentChangedEvent> equipChangedPub,
            IPublisher<EquipmentBlockedEvent> equipBlockedPub,
            ISubscriber<BodyPartSeveredEvent> severedSub)
        {
            _equipChangedPub = equipChangedPub;
            _equipBlockedPub = equipBlockedPub;
            _severedSub = severedSub;
        }

        /// <summary>
        /// Инициализация: установить entityId и подписаться на события.
        /// Вызывается из InventoryModule.IStartable.Start().
        /// QI-C01: Подписка на кросс-модульные события.
        /// </summary>
        public void Initialize(string entityId)
        {
            _entityId = entityId;

            // QI-C01: Подписка на BodyPartSeveredEvent — автоснятие экипировки
            _severedSubscription = _severedSub.Subscribe(OnBodyPartSevered);
        }

        // === IEquipmentService ===

        public EquipmentData GetEquipped(EquipmentSlot slot)
        {
            return _equipment.TryGetValue(slot, out var item) ? item : null;
        }

        public bool TryEquip(EquipmentSlot slot, EquipmentData item)
        {
            if (item == null) return false;

            // Проверка: слот заблокирован (ампутация)
            if (_blockedSlots.Contains(slot))
            {
                _equipBlockedPub.Publish(new EquipmentBlockedEvent(
                    _entityId ?? "unknown", slot, "Слот заблокирован: ампутированная часть тела"));
                return false;
            }

            // Валидация через EquipmentValidator (без IBodyService — используем кэш)
            if (!EquipmentValidator.ValidateEquip(item, slot, _blockedSlots, _equipment, out var reason))
            {
                _equipBlockedPub.Publish(new EquipmentBlockedEvent(_entityId ?? "unknown", slot, reason));
                return false;
            }

            // INV-B05 FIX / P1-02 FIX: Если в слоте уже есть предмет — публикуем событие
            // со OldItemId. Подписчик (InventoryModule) вернёт старый предмет в инвентарь.
            string oldItemId = null;
            if (_equipment.TryGetValue(slot, out var existing) && existing != null)
            {
                oldItemId = existing.ItemId;
                _equipment.Remove(slot);
            }

            // Двуручное оружие: снимаем WeaponOff если нужно
            if (EquipmentValidator.ShouldUnequipOffHand(item, slot))
            {
                if (_equipment.TryGetValue(EquipmentSlot.WeaponOff, out var offHand))
                {
                    _equipment.Remove(EquipmentSlot.WeaponOff);
                    _equipChangedPub.Publish(new EquipmentChangedEvent(_entityId ?? "unknown", EquipmentSlot.WeaponOff, null, GetTotalArmor()));
                }
            }

            // Надеваем предмет
            _equipment[slot] = item;
            _equipChangedPub.Publish(new EquipmentChangedEvent(_entityId ?? "unknown", slot, item.ItemId, oldItemId, GetTotalArmor()));
            return true;
        }

        public bool TryUnequip(EquipmentSlot slot, out EquipmentData item)
        {
            if (!_equipment.TryGetValue(slot, out item) || item == null)
            {
                item = null;
                return false;
            }

            _equipment.Remove(slot);
            _equipChangedPub.Publish(new EquipmentChangedEvent(_entityId ?? "unknown", slot, null, item.ItemId, GetTotalArmor()));
            return true;
        }

        public bool IsSlotBlocked(EquipmentSlot slot)
        {
            return _blockedSlots.Contains(slot);
        }

        public float GetTotalArmor()
        {
            return EquipmentStatAggregator.GetTotalArmor(_equipment);
        }

        public float GetTotalDamage()
        {
            return EquipmentStatAggregator.GetTotalDamage(_equipment);
        }

        public WeaponHandType GetWeaponHandType()
        {
            return EquipmentStatAggregator.GetWeaponHandType(_equipment);
        }

        // === Обработчики событий ===

        /// <summary>
        /// Обработчик BodyPartSeveredEvent.
        /// QI-C01 урок: кросс-модульная подписка.
        /// При ампутации — кэшируем заблокированные слоты и автоснимаем экипировку.
        ///
        /// АРХИТЕКТУРА: НЕ ссылается на BodySlotMapping из Modules.Body!
        /// BodyPartSeveredEvent уже содержит BlockedSlots (заполняется BodyService).
        /// Это сохраняет Hub-and-Spoke: Inventory ← Core.Messaging ← Body.
        /// </summary>
        private void OnBodyPartSevered(in BodyPartSeveredEvent e)
        {
            // INV-B08 FIX: Проверяем, что событие относится к нашей сущности
            if (_entityId == null || e.EntityId != _entityId) return;

            // Кэшируем заблокированные слоты из события
            foreach (var slot in e.BlockedSlots)
            {
                _blockedSlots.Add(slot);

                // Автоснятие экипировки с заблокированного слота
                if (_equipment.TryGetValue(slot, out var item) && item != null)
                {
                    var unequippedItemId = item.ItemId;
                    _equipment.Remove(slot);
                    _equipBlockedPub.Publish(new EquipmentBlockedEvent(
                        _entityId, slot, $"Ампутация части тела {e.Part}"));
                    _equipChangedPub.Publish(new EquipmentChangedEvent(_entityId, slot, null, unequippedItemId, GetTotalArmor()));
                }
            }
        }

        // === IDisposable ===

        public void Dispose()
        {
            _severedSubscription?.Dispose();
            _severedSubscription = null;
        }
    }
}
