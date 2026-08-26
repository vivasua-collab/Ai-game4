#nullable enable
// Создано: 2026-08-26 — C2 (окно Культивации Ци, этап C).
//
// TechniqueSlotService — единый источник правды для слотов быстрого доступа техник (3-9).
//
// Контекст: пользователь хочет, чтобы клавиши 3-9 кастовали техники из назначенных слотов
// (аналогично поясу, но для техник). Слоты настраиваются в окне CultivationWindow.
//
// Архитектура:
//   • TechniqueSlotService — Module слой (Player), ISaveable, подписан на TechniqueForgottenEvent
//     (авто-очистка слота при удалении техники).
//   • Public API: AssignSlot / ClearSlot / GetTechniqueAtSlot / GetAllSlots.
//   • При изменении публикует TechniqueSlotAssignedEvent / TechniqueSlotClearedEvent (для UI).
//   • CultivationWindow (Adapter) и HotbarPanel (Adapter) подписаны на события — обновляют UI.
//   • Adapter Layer (InputAdapter) → TechniqueSlotService.GetTechniqueAtSlot(N) →
//     TechniqueCastRequestedEvent. Hub-and-Spoke соблюдён.
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Modules.Combat;

namespace CultivationGame.Modules.Player
{
    /// <summary>
    /// Сервис слотов быстрого доступа техник (C2, 2026-08-26).
    /// 7 слотов (3-9). Каждый слот хранит ID техники (или пусто).
    /// ISaveable — сериализуется в save-файл.
    /// </summary>
    public sealed class TechniqueSlotService : ISaveable, IDisposable
    {
        // === Диапазон слотов ===
        /// <summary>Минимальный индекс слота (включительно).</summary>
        public const int MinSlot = 3;
        /// <summary>Максимальный индекс слота (включительно).</summary>
        public const int MaxSlot = 9;
        /// <summary>Количество слотов (7 = слоты 3..9).</summary>
        public const int SlotCount = MaxSlot - MinSlot + 1;

        // === Зависимости ===
        [Inject] private readonly TechniqueService _techniques = null!;
        [Inject] private readonly IPublisher<TechniqueSlotAssignedEvent> _assignedPub = null!;
        [Inject] private readonly IPublisher<TechniqueSlotClearedEvent> _clearedPub = null!;
        [Inject] private readonly ISubscriber<TechniqueForgottenEvent> _forgottenSub = null!;

        private IDisposable? _forgottenToken;

        // === Состояние ===
        // slotIndex(3..9) → techniqueId. Пустая ячейка = ключ отсутствует в словаре.
        private readonly Dictionary<int, string> _slots = new();

        // === ISaveable ===
        public string SaveKey => "technique_slots";

        public TechniqueSlotService()
        {
        }

        /// <summary>Запустить подписки. Вызывается из PlayerModule.Start или DI.</summary>
        public void Start()
        {
            _forgottenToken = _forgottenSub.Subscribe(OnTechniqueForgotten);
        }

        /// <summary>
        /// Назначить технику в слот (3..9). Если слот занят — перезаписывает.
        /// Если техника с таким ID не изучена — отказ (возвращает false).
        /// Если techniqueId пустой — эквивалентно ClearSlot.
        /// </summary>
        public bool AssignSlot(int slotIndex, string techniqueId)
        {
            if (slotIndex < MinSlot || slotIndex > MaxSlot) return false;
            if (string.IsNullOrEmpty(techniqueId))
            {
                return ClearSlot(slotIndex);
            }
            // Валидация: техника должна быть изучена
            if (_techniques != null && !_techniques.IsLearned(techniqueId)) return false;

            _slots[slotIndex] = techniqueId;
            _assignedPub.Publish(new TechniqueSlotAssignedEvent(slotIndex, techniqueId));
            return true;
        }

        /// <summary>
        /// Очистить слот. Возвращает true, если слот был занят.
        /// </summary>
        public bool ClearSlot(int slotIndex)
        {
            if (slotIndex < MinSlot || slotIndex > MaxSlot) return false;
            if (!_slots.Remove(slotIndex)) return false;
            _clearedPub.Publish(new TechniqueSlotClearedEvent(slotIndex));
            return true;
        }

        /// <summary>
        /// Получить ID техники в слоте (null/empty — слот пуст).
        /// </summary>
        public string? GetTechniqueAtSlot(int slotIndex)
        {
            if (slotIndex < MinSlot || slotIndex > MaxSlot) return null;
            return _slots.TryGetValue(slotIndex, out var id) ? id : null;
        }

        /// <summary>
        /// Получить словарь всех занятых слотов (для UI и сейва).
        /// Ключ — slotIndex (3..9), значение — techniqueId.
        /// </summary>
        public IReadOnlyDictionary<int, string> GetAllSlots() => _slots;

        /// <summary>
        /// Найти слот, в который назначена указанная техника (или -1, если нигде).
        /// </summary>
        public int FindSlotForTechnique(string techniqueId)
        {
            foreach (var kvp in _slots)
                if (kvp.Value == techniqueId) return kvp.Key;
            return -1;
        }

        /// <summary>
        /// Обработчик TechniqueForgottenEvent — авто-очистка слота при удалении техники.
        /// </summary>
        private void OnTechniqueForgotten(in TechniqueForgottenEvent e)
        {
            int slot = FindSlotForTechnique(e.TechniqueId);
            if (slot >= MinSlot)
            {
                ClearSlot(slot);
            }
        }

        // === ISaveable ===

        public object CaptureState()
        {
            // Сериализуем как массив {slot, techId} — компактнее, чем Dictionary<int,string>
            // (поддержка JSON-сериализации в SaveFileHandler).
            var list = new List<SlotEntry>();
            foreach (var kvp in _slots)
                list.Add(new SlotEntry { Slot = kvp.Key, TechId = kvp.Value });
            return new SlotState { Slots = list };
        }

        public void RestoreState(object state)
        {
            _slots.Clear();
            if (state is SlotState slotState && slotState.Slots != null)
            {
                foreach (var entry in slotState.Slots)
                {
                    if (entry.Slot >= MinSlot && entry.Slot <= MaxSlot && !string.IsNullOrEmpty(entry.TechId))
                        _slots[entry.Slot] = entry.TechId;
                }
            }
        }

        public void Dispose()
        {
            _forgottenToken?.Dispose();
            _forgottenToken = null;
        }

        // === Сериализационные DTO (public для JSON-сериализации) ===

        public class SlotState
        {
            public List<SlotEntry>? Slots;
        }

        public class SlotEntry
        {
            public int Slot;
            public string TechId = "";
        }
    }
}
