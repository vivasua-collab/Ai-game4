#nullable enable
// Создано: 2026-05-09 15:30:00 UTC
// Логика отношений NPC: хранение, изменение, затухание.
// EVT-01: Подписка на DayChangedEvent для затухания (не инжектит ITimeService).
// Hub-and-Spoke: кросс-модульные события — только через MessagePipe.
using System;
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.NPC
{
    /// <summary>
    /// Запись отношения между NPC и целью.
    /// Хранит числовое значение и метаданные.
    /// </summary>
    public class RelationshipRecord
    {
        /// <summary>Числовой счёт отношения (-100..+100)</summary>
        public int AttitudeScore;

        /// <summary>День последнего взаимодействия</summary>
        public int LastInteractionDay;

        /// <summary>Флаг семейной связи (запрещает затухание)</summary>
        public bool HasFamilyFlag;

        /// <summary>Флаг клятвы (запрещает затухание)</summary>
        public bool HasSwornFlag;

        /// <summary>Количество взаимодействий</summary>
        public int InteractionCount;
    }

    /// <summary>
    /// Сервис управления отношениями NPC.
    /// Хранит словарь отношений, обрабатывает изменение и затухание.
    ///
    /// АРХИТЕКТУРА (EVT-01): Подписывается на DayChangedEvent
    /// через MessagePipe для обработки затухания отношений.
    /// НЕ инжектит ITimeService напрямую.
    ///
    /// Затухание: после AttitudeDecayStartDays дней без взаимодействия,
    /// счёт уменьшается на AttitudeDecayPerDay в день.
    /// Семейные и клятвенные отношения НЕ затухают.
    ///
    /// АУДИТ-0921_2030 Ф4 (P1): до этого сервиса не было НИ в reset, НИ в
    /// save — при этом GetAttitude() (через NPCService) читает ИМЕННО
    /// отсюда. Дефект был двойной: (a) _relationships переживали NewGame
    /// (месть/дружба прошлого мира наследовалась, а при переиспользовании
    /// NPC ID — «призрачные» отношения); (b) сейв хранил NPCSaveEntry.
    /// AttitudeScore, но RestoreState его никуда не возвращал — после Load
    /// ВСЕ отношения сбрасывались в Neutral (боевые обиды -20 «терялись»,
    /// «NPCService хранит мёртвое поле»). Теперь: IWorldResettable + свой
    /// блок сейва "npc_relationships" (полные записи, не только счёт).
    /// </summary>
    public class NPCRelationshipService : IDisposable, IWorldResettable, ISaveable
    {
        // === Зависимости ===
        private readonly NPCConfig _config;

        // === MessagePipe: подписки ===
        private readonly ISubscriber<DayChangedEvent> _dayChangedSub;
        private IDisposable _dayChangedSubscription;

        // === MessagePipe: паблишеры ===
        private readonly IPublisher<AttitudeChangedEvent> _attitudeChangedPub;

        // === Хранилище отношений ===
        private readonly Dictionary<(string npcId, string targetId), RelationshipRecord> _relationships
            = new Dictionary<(string npcId, string targetId), RelationshipRecord>();

        // === Текущий день (из DayChangedEvent) ===
        private int _currentDay;

        // === Конструктор (VContainer) ===
        public NPCRelationshipService(
            NPCConfig config,
            ISubscriber<DayChangedEvent> dayChangedSub,
            IPublisher<AttitudeChangedEvent> attitudeChangedPub)
        {
            _config = config;
            _dayChangedSub = dayChangedSub;
            _attitudeChangedPub = attitudeChangedPub;
        }

        /// <summary>
        /// Инициализация: подписка на DayChangedEvent для затухания.
        /// Вызывается из NPCModule.Start().
        /// </summary>
        public void Initialize()
        {
            _dayChangedSubscription = _dayChangedSub.Subscribe(OnDayChanged);
        }

        // === Публичный API ===

        /// <summary>
        /// Получить отношение NPC к цели (enum Attitude).
        /// </summary>
        public Attitude GetAttitude(string npcId, string targetId)
        {
            var record = GetOrCreateRecord(npcId, targetId);
            return CalculateAttitude(record.AttitudeScore);
        }

        /// <summary>
        /// Получить числовой счёт отношения (-100..+100).
        /// </summary>
        public int GetAttitudeScore(string npcId, string targetId)
        {
            var record = GetOrCreateRecord(npcId, targetId);
            return record.AttitudeScore;
        }

        /// <summary>
        /// Изменить отношение NPC к цели.
        /// Клэмпит в диапазон -100..+100, обновляет LastInteractionDay.
        /// </summary>
        public void ModifyAttitude(string npcId, string targetId, int delta)
        {
            var record = GetOrCreateRecord(npcId, targetId);
            int oldScore = record.AttitudeScore;
            int newScore = Math.Clamp(oldScore + delta, -100, 100);

            record.AttitudeScore = newScore;
            record.LastInteractionDay = _currentDay;
            record.InteractionCount++;
        }

        /// <summary>
        /// Установить флаг семейной связи (запрещает затухание).
        /// </summary>
        public void SetFamilyFlag(string npcId, string targetId, bool value)
        {
            var record = GetOrCreateRecord(npcId, targetId);
            record.HasFamilyFlag = value;
        }

        /// <summary>
        /// Установить флаг клятвы (запрещает затухание).
        /// </summary>
        public void SetSwornFlag(string npcId, string targetId, bool value)
        {
            var record = GetOrCreateRecord(npcId, targetId);
            record.HasSwornFlag = value;
        }

        /// <summary>
        /// Удалить все отношения NPC (при деспавне).
        /// </summary>
        public void RemoveAllForNPC(string npcId)
        {
            var keysToRemove = new List<(string, string)>();
            foreach (var kvp in _relationships)
            {
                if (kvp.Key.npcId == npcId || kvp.Key.targetId == npcId)
                    keysToRemove.Add(kvp.Key);
            }
            foreach (var key in keysToRemove)
                _relationships.Remove(key);
        }

        // === Затухание отношений ===

        /// <summary>
        /// Обработчик DayChangedEvent — затухание отношений.
        /// Уменьшает счёт на AttitudeDecayPerDay за каждый день
        /// без взаимодействия после AttitudeDecayStartDays.
        /// Семейные и клятвенные отношения НЕ затухают.
        /// </summary>
        private void OnDayChanged(in DayChangedEvent e)
        {
            _currentDay = e.Day;
            ProcessAttitudeDecay();
        }

        /// <summary>
        /// Обработать затухание всех отношений.
        /// </summary>
        private void ProcessAttitudeDecay()
        {
            foreach (var kvp in _relationships)
            {
                var record = kvp.Value;

                // Семейные и клятвенные отношения не затухают
                if (record.HasFamilyFlag || record.HasSwornFlag) continue;

                // Нулевое отношение не затухает (уже нейтральное)
                if (record.AttitudeScore == 0) continue;

                // Проверка: прошло ли достаточно дней с последнего взаимодействия
                int daysSinceInteraction = _currentDay - record.LastInteractionDay;
                if (daysSinceInteraction < _config.AttitudeDecayStartDays) continue;

                // Затухание: сдвигаем к нулю
                int decay = _config.AttitudeDecayPerDay;
                if (record.AttitudeScore > 0)
                {
                    record.AttitudeScore = Math.Max(0, record.AttitudeScore - decay);
                }
                else
                {
                    record.AttitudeScore = Math.Min(0, record.AttitudeScore + decay);
                }
            }
        }

        // === Утилиты ===

        /// <summary>
        /// Получить или создать запись отношения.
        /// </summary>
        private RelationshipRecord GetOrCreateRecord(string npcId, string targetId)
        {
            var key = (npcId, targetId);
            if (!_relationships.TryGetValue(key, out var record))
            {
                record = new RelationshipRecord
                {
                    AttitudeScore = 0,
                    LastInteractionDay = _currentDay,
                    HasFamilyFlag = false,
                    HasSwornFlag = false,
                    InteractionCount = 0
                };
                _relationships[key] = record;
            }
            return record;
        }

        /// <summary>
        /// Конвертировать числовой счёт в enum Attitude.
        /// Диапазоны соответствуют определению Attitude в Enums.cs.
        /// </summary>
        public static Attitude CalculateAttitude(int score)
        {
            if (score <= -51) return Attitude.Hatred;
            if (score <= -21) return Attitude.Hostile;
            if (score <= -10) return Attitude.Unfriendly;
            if (score <= 9) return Attitude.Neutral;
            if (score <= 49) return Attitude.Friendly;
            if (score <= 79) return Attitude.Allied;
            return Attitude.SwornAlly;
        }

        // === АУДИТ-0921_2030 Ф4: IWorldResettable ========================

        /// <summary>
        /// Пересборка мира (меню → NewGame/LoadGame до RestoreState):
        /// отношения привязаны к сущностям конкретного мира — очищаются
        /// целиком. День возвращается к «до событий» (DayChangedEvent
        /// нового мира обновит его при первом тике времени).
        /// </summary>
        public void ResetWorld()
        {
            int total = _relationships.Count;
            _relationships.Clear();
            _currentDay = 0;
            if (total > 0)
                Console.WriteLine($"[NPCRelationshipService] ResetWorld: {total} отношений очищено (New Game)");
        }

        // === АУДИТ-0921_2030 Ф4: ISaveable — блок "npc_relationships" ====

        public string SaveKey => "npc_relationships";

        /// <summary>R11 P0-Save: тип state-блока для persistence round-trip.</summary>
        public Type StateType => typeof(NpcRelationshipSaveState);

        public object CaptureState()
        {
            // Полные записи (счёт + флаги + день последнего взаимодействия +
            // счётчик): затухание/семейные связи продолжаются после Load.
            var state = new NpcRelationshipSaveState
            {
                Entries = new NpcRelationshipSaveEntry[_relationships.Count]
            };
            int i = 0;
            foreach (var kvp in _relationships)
            {
                state.Entries[i++] = new NpcRelationshipSaveEntry
                {
                    NpcId = kvp.Key.npcId ?? "",
                    TargetId = kvp.Key.targetId ?? "",
                    AttitudeScore = kvp.Value.AttitudeScore,
                    LastInteractionDay = kvp.Value.LastInteractionDay,
                    HasFamilyFlag = kvp.Value.HasFamilyFlag,
                    HasSwornFlag = kvp.Value.HasSwornFlag,
                    InteractionCount = kvp.Value.InteractionCount
                };
            }
            return state;
        }

        public void RestoreState(object state)
        {
            if (state is not NpcRelationshipSaveState data) return;

            // GameSession.LoadGame уже сделал ResetWorld — наполняем словарь.
            _relationships.Clear();
            if (data.Entries != null)
            {
                foreach (var e in data.Entries)
                {
                    if (string.IsNullOrEmpty(e.NpcId) || string.IsNullOrEmpty(e.TargetId)) continue;
                    _relationships[(e.NpcId, e.TargetId)] = new RelationshipRecord
                    {
                        AttitudeScore = Math.Clamp(e.AttitudeScore, -100, 100),
                        LastInteractionDay = e.LastInteractionDay,
                        HasFamilyFlag = e.HasFamilyFlag,
                        HasSwornFlag = e.HasSwornFlag,
                        InteractionCount = e.InteractionCount
                    };
                }
            }
            Console.WriteLine($"[NPCRelationshipService] RestoreState: {_relationships.Count} отношений восстановлено " +
                "(боевые обиды/дружба переживают Load, АУДИТ-0921_2030 Ф4)");
        }

        // === Сериализационные DTO (public-поля; IncludeFields — SaveJson) ===

        public class NpcRelationshipSaveState
        {
            public NpcRelationshipSaveEntry[]? Entries;
        }

        public class NpcRelationshipSaveEntry
        {
            public string NpcId = "";
            public string TargetId = "";
            public int AttitudeScore;
            public int LastInteractionDay;
            public bool HasFamilyFlag;
            public bool HasSwornFlag;
            public int InteractionCount;
        }

        public void Dispose()
        {
            _dayChangedSubscription?.Dispose();
            _dayChangedSubscription = null;
            _relationships.Clear();
        }
    }
}
