#nullable enable
// Создано: 2026-09-23 — R37-c (баг-репорт пользователя 23.09):
// «не могу добавить камни Ци в зарядник, лекарство добавляется без проблем».
//
// КОРЕНЬ: домен Charger (ChargerService: слоты камней/буфер/тепло) жил своей
// жизнью — НИКАК не связан с предметами инвентаря. Камень Ци (QiStoneData,
// GENERATORS_SYSTEM §10) нельзя вставить в зарядник: окна H нет, моста нет;
// вместо этого зарядник (Slot=Belt) маскировался под пояс — BeltSlotRow
// принимал только Consumable (лекарство — да, камень — молчаливый отказ).
//
// МОСТ (этот файл): единственная точка конверсии предмет ↔ домен.
//   QiStoneData (инвентарь)  →  QiStone (домен ChargerSlot)
//     quality = Common (предметного качества нет — канон §10 «качество не
//     предусмотрено»), size = маппинг 5 размеров, element = Neutral (чистое
//     Ци), maxQi/currentQi = QiAmount/QiRemaining предмета (1024×см³).
//
// КОНТРАКТ ИЗВЛЕЧЕНИЯ (анти-дюп): инвентарь хранит стаки ItemId+Count —
// состояние per-экземпляра НЕ хранится. Вернуть в инвентарь можно только
// ПОЛНЫЙ камень (QiRemaining == QiAmount — предметов в стаке не меняли) или
// ПУСТОЙ (кристалл отдал Ци и рассыпался — в инвентарь не возвращается).
// Частично-израсходованный камень извлечь нельзя: тост с причиной. Иначе
// «вставил 1024 → буфер забрал 500 → вынул полный 1024» = дюп Ци.
//
// Режим домена: активен ТОЛЬКО при надетом заряднике (EquipmentSlot.Belt +
// ItemType=="Charger"). ChargerModule больше не автоактивирует — мост
// синхронизирует по EquipmentChangedEvent (RestoreState экипировки публикует
// события → корректно и после загрузки сейва).
using System;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Charger
{
    /// <summary>
    /// Мост между предметной системой (QiStoneData инвентаря) и доменом
    /// зарядника (QiStone в слотах ChargerService). Владелец режима домена.
    /// </summary>
    public sealed class ChargerItemBridge : IDisposable
    {
        public const string ChargerItemType = "Charger";

        [Inject] private readonly IInventoryService _inventory = null!;
        [Inject] private readonly IItemDatabaseService _itemDb = null!;
        [Inject] private readonly IEquipmentService _equipment = null!;
        [Inject] private readonly ChargerService _charger = null!;
        [Inject] private readonly IPublisher<ToastShownEvent> _toastPub = null!;
        [Inject] private readonly ISubscriber<EquipmentChangedEvent> _equipChangedSub = null!;

        private IDisposable? _equipChangedToken;

        /// <summary>Зарядник надет в слот пояса (ItemType=="Charger").</summary>
        public bool IsChargerEquipped =>
            _equipment.GetEquipped(EquipmentSlot.Belt) is EquipmentData eq
            && eq.ItemType == ChargerItemType;

        /// <summary>
        /// Подписка + первичная синхронизация режима. Вызывается из
        /// ChargerModule.Start() (после Configure).
        /// </summary>
        public void Initialize()
        {
            _equipChangedToken?.Dispose();
            _equipChangedToken = _equipChangedSub.Subscribe(OnEquipmentChanged);
            SyncModeToEquipped();
        }

        private void OnEquipmentChanged(in EquipmentChangedEvent e)
        {
            if (e.Slot != EquipmentSlot.Belt) return;
            SyncModeToEquipped();
        }

        /// <summary>
        /// Режим домена = факт экипировки. Идемпотентен (клэмп дребезга
        /// событий): повторная активация при уже On не дублирует.
        /// </summary>
        private void SyncModeToEquipped()
        {
            if (IsChargerEquipped)
            {
                if (_charger.Mode == ChargerMode.Off) _charger.Activate();
            }
            else if (_charger.Mode == ChargerMode.On)
            {
                _charger.Deactivate();
            }
        }

        /// <summary>Snapshot слотов домена для UI (без мутации).</summary>
        public ChargerSlotSnapshot GetSlot(int slotIndex)
            => _charger.GetSlotSnapshot(slotIndex);

        /// <summary>
        /// Вставить камень Ци из инвентаря в первый подходящий слот.
        /// R10 P1-SlotId: слот-адресность — изымается ровно 1 шт из УКАЗАННОЙ
        /// кучки (Guid), как ItemUseService.TryUseFromInventory. Возвращает
        /// false с человекочитаемой причиной в <paramref name="message"/>
        /// (тост публикуется здесь же — молчаливых отказов больше нет).
        /// </summary>
        public bool TryInsertStone(Guid slotId, string expectedItemId, out string message)
        {
            message = string.Empty;
            if (slotId == Guid.Empty && string.IsNullOrEmpty(expectedItemId))
            {
                message = "Предмет не определён";
                return false;
            }
            if (!IsChargerEquipped)
            {
                message = "⛔ Зарядник не надет — камни вставляются только в надетый зарядник (слот пояса, окно H)";
                PublishToast(message);
                return false;
            }
            if (_itemDb == null || !_itemDb.TryGetItem(expectedItemId, out var item) || item is not QiStoneData stone)
            {
                message = "⛔ Это не камень Ци";
                PublishToast(message);
                return false;
            }

            // Слот-адресная проверка кучки (R10 P1-SlotId): Guid.Empty —
            // легаси/не-инвентарный источник → отказ без фолбэка на весь предмет.
            if (slotId != Guid.Empty)
            {
                int slotIndex = _inventory.FindSlotIndexBySlotId(slotId);
                if (slotIndex < 0)
                {
                    message = "⛔ Кучка изменилась — вставка отменена";
                    PublishToast(message);
                    return false;
                }
                var invSlot = _inventory.GetAllSlots()[slotIndex];
                if (invSlot.IsEmpty || invSlot.ItemId != expectedItemId)
                {
                    message = "⛔ Кучка изменилась — вставка отменена";
                    PublishToast(message);
                    return false;
                }
            }
            else if (_inventory.GetItemCount(expectedItemId) <= 0)
            {
                message = "⛔ Камня нет в инвентаре";
                PublishToast(message);
                return false;
            }

            // Конверсия предмета → доменный камень (канон §10: фактический объём).
            var domainStone = ToDomainStone(stone);

            // Вставка ДО списания: слот может не принять (занят/гейт качества).
            if (!_charger.InsertStone(domainStone))
            {
                message = "⛔ Нет свободного слота под этот камень";
                PublishToast(message);
                return false;
            }

            // Списание после успешной вставки — предмет не теряется на отказе.
            bool removed = slotId != Guid.Empty
                ? _inventory.TryRemoveFromSlot(slotId, expectedItemId, 1)
                : _inventory.TryRemoveItem(expectedItemId, 1);
            if (!removed)
            {
                // Вставили, но изъять не вышли (гонка с другим окном) — откат.
                _charger.RemoveStone(FindSlotOf(domainStone));
                message = "⛔ Не удалось изъять камень из инвентаря";
                PublishToast(message);
                return false;
            }

            message = $"💎 {stone.NameRu} → зарядник (осталось {_charger.ActiveSlotsCount}/{_charger.SlotCount} слотов)";
            PublishToast(message);
            Console.WriteLine($"[ChargerBridge] Inserted '{expectedItemId}' (qi {stone.QiRemaining}/{stone.QiAmount}) from slot {slotId}");
            return true;
        }

        /// <summary>
        /// Извлечь камень из слота обратно в инвентарь. Контракт анти-дюпа:
        /// только полный камень; пустой — «рассыпался» (не возвращается);
        /// частичный — отказ с причиной.
        /// </summary>
        public bool TryExtractStone(int slotIndex, out string message)
        {
            message = string.Empty;
            var snap = _charger.GetSlotSnapshot(slotIndex);
            if (!snap.HasStone)
            {
                message = "Слот пуст";
                return false;
            }

            if (snap.CurrentQi <= 0)
            {
                // Пустой камень: кристалл отдал Ци — рассыпался (выброс).
                _charger.RemoveStone(slotIndex);
                message = "🏳 Пустой камень рассыпался в пыль — Ци извлечена ранее";
                PublishToast(message);
                return true;
            }

            if (snap.CurrentQi < snap.MaxQi)
            {
                message = $"⛔ Камень частично истощён ({snap.CurrentQi}/{snap.MaxQi} Ци) — дождитесь опустошения или оставьте в заряднике";
                PublishToast(message);
                return false;
            }

            // Полный камень → предмет в инвентарь (определение из базы).
            var stone = _charger.RemoveStone(slotIndex);
            if (stone == null) { message = "⛔ Слот изменился"; return false; }

            var data = FromDomainStone(stone);
            if (data == null || !_inventory.TryAddItem(data, 1))
            {
                message = "⛔ Инвентарь полон — камень оставлен в слоте";
                // Вернуть в слот (вставка обратно в тот же индекс).
                _charger.InsertStone(stone, slotIndex);
                PublishToast(message);
                return false;
            }

            message = $"💎 {data.NameRu} извлечён в инвентарь";
            PublishToast(message);
            Console.WriteLine($"[ChargerBridge] Extracted stone from slot {slotIndex} (qi {stone.CurrentQi}/{stone.MaxQi})");
            return true;
        }

        // === Конверсия (единственная точка предмет↔домен) =============

        private static QiStone ToDomainStone(QiStoneData data)
        {
            // Размер: 5→5 (Dust→Tiny … Boulder→Huge). Качество: предметного
            // нет (§10) → Common. Стихия: чистое Ци → Neutral.
            var domainSize = data.Size switch
            {
                Core.Data.QiStoneSize.Dust => QiStoneSize.Tiny,
                Core.Data.QiStoneSize.Pebble => QiStoneSize.Small,
                Core.Data.QiStoneSize.Shard => QiStoneSize.Medium,
                Core.Data.QiStoneSize.Stone => QiStoneSize.Large,
                Core.Data.QiStoneSize.Boulder => QiStoneSize.Huge,
                _ => QiStoneSize.Tiny,
            };
            long qi = data.QiRemaining > 0 ? data.QiRemaining : 0;
            return new QiStone(QiStoneQuality.Common, domainSize, Element.Neutral,
                data.QiAmount, qi);
        }

        private static QiStoneData? FromDomainStone(QiStone stone)
        {
            // Обратный маппинг; определение берётся из сидера (ItemId-шаблоны
            // qistone_{size}_{type} — предмет вернётся в существующий стак).
            var size = stone.Size switch
            {
                QiStoneSize.Tiny => Core.Data.QiStoneSize.Dust,
                QiStoneSize.Small => Core.Data.QiStoneSize.Pebble,
                QiStoneSize.Medium => Core.Data.QiStoneSize.Shard,
                QiStoneSize.Large => Core.Data.QiStoneSize.Stone,
                QiStoneSize.Huge => Core.Data.QiStoneSize.Boulder,
                _ => Core.Data.QiStoneSize.Dust,
            };
            var data = CultivationGame.Modules.Generator.QiStoneSeeder.Create(size, isChaotic: false);
            // Полный камень: QiRemaining = MaxQi домена (объём сохранён).
            data.QiAmount = stone.MaxQi;
            data.QiRemaining = stone.CurrentQi;
            return data;
        }

        private int FindSlotOf(QiStone stone)
        {
            for (int i = 0; i < _charger.SlotCount; i++)
            {
                if (ReferenceEquals(_charger.GetSlotSnapshot(i).Stone, stone)) return i;
            }
            return -1;
        }

        private void PublishToast(string message)
        {
            _toastPub?.Publish(new ToastShownEvent(message, 2.5f));
        }

        public void Dispose()
        {
            _equipChangedToken?.Dispose();
            _equipChangedToken = null;
        }
    }

    /// <summary>Immutable-снимок слота зарядника для UI/симов.</summary>
    public readonly struct ChargerSlotSnapshot
    {
        public readonly int SlotIndex;
        public readonly bool HasStone;
        public readonly long CurrentQi;
        public readonly long MaxQi;
        public readonly QiStone? Stone;

        public ChargerSlotSnapshot(int slotIndex, bool hasStone, long currentQi, long maxQi, QiStone? stone)
        {
            SlotIndex = slotIndex;
            HasStone = hasStone;
            CurrentQi = currentQi;
            MaxQi = maxQi;
            Stone = stone;
        }
    }
}
