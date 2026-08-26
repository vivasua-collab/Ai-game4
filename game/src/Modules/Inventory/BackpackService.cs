#nullable enable
// Создано: 2026-05-18 18:12:00 UTC
// Сервис управления рюкзаком.
// Отслеживает экипировку/снятие рюкзака (слот Back),
// пересчитывает эффективные лимиты веса и объёма.
// Формулы из legacy:
//   effectiveMaxWeight = baseMaxWeight + backpack.WeightBonus
//   effectiveMaxVolume = baseMaxVolume + backpack.VolumeBonus
//   effectiveWeight = rawWeight * (1 - backpack.WeightReduction / 100)

using System;
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Core.Events;

namespace CultivationGame.Modules.Inventory
{
    /// <summary>
    /// Сервис управления рюкзаком.
    /// Подписывается на EquipmentChangedEvent через MessagePipe (EVT-01).
    /// При экипировке/снятии рюкзака (слот Back) пересчитывает бонусы.
    ///
    /// Зависимости:
    /// - ISubscriber&lt;EquipmentChangedEvent&gt; — обнаружение смены рюкзака
    /// - IItemDatabaseService — lookup itemId → EquipmentData
    /// - IEquipmentService — текущий предмет в слоте Back (тот же модуль)
    ///
    /// АРХИТЕКТУРА: IEquipmentService — сервис того же модуля Inventory,
    /// поэтому прямая инъекция допустима (Hub-and-Spoke запрещает только
    /// кросс-модульную прямую инъекцию).
    /// </summary>
    public class BackpackService : IDisposable
    {
        // === Зависимости (DI через конструктор) ===
        private readonly ISubscriber<EquipmentChangedEvent> _equipChangedSub;
        private readonly IItemDatabaseService _itemDatabase;
        private readonly IEquipmentService _equipmentService;

        // === Состояние ===
        /// <summary>Кэш текущего рюкзака (null если не надет)</summary>
        private EquipmentData _currentBackpack;

        /// <summary>Подписка на событие смены экипировки</summary>
        private IDisposable _equipChangedSubscription;

        // === Конструктор (VContainer) ===
        public BackpackService(
            ISubscriber<EquipmentChangedEvent> equipChangedSub,
            IItemDatabaseService itemDatabase,
            IEquipmentService equipmentService)
        {
            _equipChangedSub = equipChangedSub;
            _itemDatabase = itemDatabase;
            _equipmentService = equipmentService;
        }

        /// <summary>
        /// Инициализация: загрузить текущий рюкзак и подписаться на события.
        /// Вызывается из InventoryModule.IStartable.Start().
        /// </summary>
        public void Initialize()
        {
            // Загрузить текущий рюкзак (если уже надет до инициализации)
            RefreshCurrentBackpack();

            // Подписка на смену экипировки
            _equipChangedSubscription = _equipChangedSub.Subscribe(OnEquipmentChanged);
        }

        // === Публичные методы ===

        /// <summary>
        /// Эффективный максимальный вес с учётом бонуса рюкзака.
        /// Формула: baseMaxWeight + backpack.WeightBonus
        /// Если рюкзака нет — возвращается базовый вес без бонуса.
        /// </summary>
        public float GetEffectiveMaxWeight(float baseMaxWeight)
        {
            if (_currentBackpack == null) return baseMaxWeight;
            return baseMaxWeight + _currentBackpack.WeightBonus;
        }

        /// <summary>
        /// Эффективный максимальный объём с учётом бонуса рюкзака.
        /// Формула: baseMaxVolume + backpack.VolumeBonus
        /// Если рюкзака нет — возвращается базовый объём без бонуса.
        /// </summary>
        public float GetEffectiveMaxVolume(float baseMaxVolume)
        {
            if (_currentBackpack == null) return baseMaxVolume;
            return baseMaxVolume + _currentBackpack.VolumeBonus;
        }

        /// <summary>
        /// Эффективный вес с учётом снижения от рюкзака.
        /// Формула: rawWeight * (1 - backpack.WeightReduction / 100)
        /// Если рюкзака нет — вес без изменений.
        /// </summary>
        public float GetEffectiveWeight(float rawWeight)
        {
            if (_currentBackpack == null) return rawWeight;
            float reduction = _currentBackpack.WeightReduction / 100f;
            return rawWeight * (1f - reduction);
        }

        /// <summary>
        /// Текущий бонус к весу от рюкзака (0 если не надет).
        /// </summary>
        public float CurrentWeightBonus => _currentBackpack?.WeightBonus ?? 0f;

        /// <summary>
        /// Текущий бонус к объёму от рюкзака (0 если не надет).
        /// </summary>
        public float CurrentVolumeBonus => _currentBackpack?.VolumeBonus ?? 0f;

        /// <summary>
        /// Текущее снижение веса в процентах (0 если не надет).
        /// </summary>
        public float CurrentWeightReduction => _currentBackpack?.WeightReduction ?? 0f;

        /// <summary>
        /// Надет ли рюкзак в данный момент.
        /// </summary>
        public bool HasBackpack => _currentBackpack != null;

        // === Обработчики событий ===

        /// <summary>
        /// Обработчик EquipmentChangedEvent.
        /// При смене слота Back — обновить кэш рюкзака.
        /// EVT-01: подписка через MessagePipe, не прямая инъекция EquipmentService.
        /// </summary>
        private void OnEquipmentChanged(in EquipmentChangedEvent e)
        {
            // Нас интересует только слот Back
            if (e.Slot != EquipmentSlot.Back) return;

            RefreshCurrentBackpack();

            Console.WriteLine($"[BackpackService] Смена рюкзака: Slot=Back, ItemId={e.ItemId}, " +
                      $"WeightBonus={CurrentWeightBonus}, VolumeBonus={CurrentVolumeBonus}, " +
                      $"WeightReduction={CurrentWeightReduction}%");
        }

        /// <summary>
        /// Обновить кэш текущего рюкзака из EquipmentService.
        /// </summary>
        private void RefreshCurrentBackpack()
        {
            var equipped = _equipmentService.GetEquipped(EquipmentSlot.Back);
            _currentBackpack = equipped;
        }

        // === IDisposable ===

        public void Dispose()
        {
            _equipChangedSubscription?.Dispose();
            _equipChangedSubscription = null;
        }
    }
}
