#nullable enable
// Создано: 2026-05-09 15:30:00 UTC
// Редактировано: 2026-05-22 04:14:49 UTC — Спринт 3 B6: InnateElement в NPCSaveEntry + CaptureState/RestoreState
// Редактировано: 2026-05-20 19:11:00 UTC — Фаза 4, задача 4.6: AwakeningAge в NPCSaveEntry + RestoreState
// Редактировано: 2026-05-20 19:11:00 UTC — Фаза 4, задача 4.4: создание NPC из save при отсутствии в реестре
// Редактировано: 2026-05-21 08:15:33 UTC — Волна 3: flat-массивы сериализация + per-entity регистрация после load
// Редактировано: 2026-05-10 — Phase 17C: Vector2 → Position2D в сигнатурах методов
// Редактировано: 2026-05-10 12:00:00 UTC — Phase 18A: реализация ISaveable
// Редактировано: 2026-05-10 12:30:00 UTC — Phase 18A FIX: аудит D3/D5/D6
// Реализация INPCService.
// Управление NPC: хранение состояний, запросы, кэш Ци для AI-решений.
// EVT-01: Все кросс-модульные взаимодействия — через MessagePipe.
// Hub-and-Spoke: NPCService НЕ инжектит сервисы других модулей.
using System;
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Modules.Body;
using CultivationGame.Modules.NPC.Data;
using CultivationGame.Core.Events;

namespace CultivationGame.Modules.NPC
{
    /// <summary>
    /// Реализация INPCService.
    /// Хранит Dictionary<string, NPCState> всех активных NPC.
    /// Делегирует логику отношений в NPCRelationshipService.
    ///
    /// АРХИТЕКТУРА (EVT-01): NPC модуль НЕ инжектит IQiService, ICombatService.
    /// Все кросс-модульные взаимодействия — ТОЛЬКО через MessagePipe:
    /// - QiChangedEvent → кэш Ци игрока для AI-решений
    /// - AttitudeChangedEvent → публикация при изменении отношения
    /// - NPCAIStateChangedEvent → публикация при смене AI-состояния
    /// </summary>
    public class NPCService : INPCService, ISaveable, IDisposable
    {
        // === Зависимости (внутримодульные) ===
        private readonly NPCRelationshipService _relationshipService;

        // Волна 3: per-entity провайдеры для регистрации после загрузки
        private readonly IBodyDataProvider _bodyDataProvider;
        private readonly IQiDataProvider _qiDataProvider;
        private readonly IEquipmentDataProvider _equipmentDataProvider;

        // === MessagePipe: паблишеры ===
        private readonly IPublisher<AttitudeChangedEvent> _attitudeChangedPub;
        private readonly IPublisher<NPCAIStateChangedEvent> _aiStateChangedPub;
        private readonly IPublisher<NPCDeathEvent> _npcDeathPub;
        private readonly IPublisher<NPCDamagedEvent> _npcDamagedPub;
        private readonly IPublisher<NPCInteractedEvent> _npcInteractedPub; // Фаза 3, задача 3.8

        // === MessagePipe: подписки ===
        private readonly ISubscriber<QiChangedEvent> _qiChangedSub;
        private IDisposable _qiChangedSubscription;

        // === Хранилище состояний NPC ===
        private readonly Dictionary<string, NPCState> _npcStates = new Dictionary<string, NPCState>();

        // === Конструктор (VContainer) ===
        public NPCService(
            NPCRelationshipService relationshipService,
            IBodyDataProvider bodyDataProvider,
            IQiDataProvider qiDataProvider,
            IEquipmentDataProvider equipmentDataProvider,
            IPublisher<AttitudeChangedEvent> attitudeChangedPub,
            IPublisher<NPCAIStateChangedEvent> aiStateChangedPub,
            IPublisher<NPCDeathEvent> npcDeathPub,
            IPublisher<NPCDamagedEvent> npcDamagedPub,
            IPublisher<NPCInteractedEvent> npcInteractedPub, // Фаза 3, задача 3.8
            ISubscriber<QiChangedEvent> qiChangedSub)
        {
            _relationshipService = relationshipService;
            _bodyDataProvider = bodyDataProvider;
            _qiDataProvider = qiDataProvider;
            _equipmentDataProvider = equipmentDataProvider;
            _attitudeChangedPub = attitudeChangedPub;
            _aiStateChangedPub = aiStateChangedPub;
            _npcDeathPub = npcDeathPub;
            _npcDamagedPub = npcDamagedPub;
            _npcInteractedPub = npcInteractedPub; // Фаза 3, задача 3.8
            _qiChangedSub = qiChangedSub;
        }

        /// <summary>
        /// Инициализация: подписка на QiChangedEvent для кэша Ци.
        /// Вызывается из NPCModule.Start().
        /// </summary>
        public void Initialize()
        {
            // Подписка на QiChangedEvent — кэшируем Ци игрока для AI-решений
            _qiChangedSubscription = _qiChangedSub.Subscribe(OnQiChanged);
        }

        // === INPCService: Запросы ===

        /// <summary>
        /// Получить данные NPC по идентификатору.
        /// Возвращает NPCData из NPCState или null.
        /// </summary>
        public NPCData GetNPC(string npcId)
        {
            if (!_npcStates.TryGetValue(npcId, out var state)) return null;
            return StateToData(state);
        }

        /// <summary>
        /// Получить идентификаторы NPC в радиусе от позиции.
        /// </summary>
        public IReadOnlyList<string> GetNearbyNPCIds(Position2D position, float range)
        {
            var result = new List<string>();
            float rangeSq = range * range;

            foreach (var kvp in _npcStates)
            {
                if (!kvp.Value.IsAlive) continue;
                float distSq = (kvp.Value.Position - position).SqrMagnitude;
                if (distSq <= rangeSq)
                {
                    result.Add(kvp.Key);
                }
            }
            return result;
        }

        /// <summary>
        /// Получить отношение NPC к цели.
        /// Делегирует в NPCRelationshipService.
        /// </summary>
        public Attitude GetAttitude(string npcId, string targetId)
        {
            return _relationshipService.GetAttitude(npcId, targetId);
        }

        /// <summary>
        /// Изменить отношение NPC к цели.
        /// Делегирует в NPCRelationshipService, затем публикует событие.
        /// </summary>
        public void ModifyAttitude(string npcId, string targetId, int delta)
        {
            Attitude oldAttitude = _relationshipService.GetAttitude(npcId, targetId);
            _relationshipService.ModifyAttitude(npcId, targetId, delta);
            Attitude newAttitude = _relationshipService.GetAttitude(npcId, targetId);

            // Публикуем событие изменения отношения
            _attitudeChangedPub.Publish(new AttitudeChangedEvent(
                npcId, targetId, oldAttitude, newAttitude));
        }

        // === Дополнительные методы (расширяют INPCService) ===

        /// <summary>
        /// Жив ли NPC.
        /// </summary>
        public bool IsAlive(string npcId)
        {
            return _npcStates.TryGetValue(npcId, out var state) && state.IsAlive;
        }

        /// <summary>
        /// Получить текущее AI-состояние NPC.
        /// </summary>
        public NPCAIState GetAIState(string npcId)
        {
            if (!_npcStates.TryGetValue(npcId, out var state))
                return NPCAIState.Idle;
            return state.AIState;
        }

        /// <summary>
        /// Получить идентификаторы всех активных NPC.
        /// </summary>
        public IReadOnlyList<string> GetAllNPCIds()
        {
            var result = new List<string>(_npcStates.Count);
            foreach (var kvp in _npcStates)
            {
                if (kvp.Value.IsAlive)
                    result.Add(kvp.Key);
            }
            return result;
        }

        /// <summary>
        /// Установить AI-состояние NPC с публикацией события.
        /// </summary>
        public void SetAIState(string npcId, NPCAIState state)
        {
            if (!_npcStates.TryGetValue(npcId, out var npcState)) return;

            // NPC-B04: не меняем AIState для мёртвых NPC
            if (!npcState.IsAlive) return;

            NPCAIState oldState = npcState.AIState;
            if (oldState == state) return;

            npcState.AIState = state;
            npcState.StateTimer = 0f;

            _aiStateChangedPub.Publish(new NPCAIStateChangedEvent(
                npcId, oldState, state));
        }

        /// <summary>
        /// Обновить позицию NPC.
        /// </summary>
        public void UpdatePosition(string npcId, Position2D position)
        {
            if (!_npcStates.TryGetValue(npcId, out var state)) return;
            state.Position = position;
        }

        // === Управление внутренним хранилищем (для NPCSpawnerService) ===

        /// <summary>
        /// Обработать взаимодействие с NPC (задача 3.8).
        /// Публикует NPCInteractedEvent через MessagePipe.
        /// </summary>
        public void OnNPCInteracted(string npcId, string initiatorId, string interactionType)
        {
            if (!_npcStates.ContainsKey(npcId)) return;
            _npcInteractedPub.Publish(new NPCInteractedEvent(npcId, initiatorId, interactionType));
        }

        /// <summary>
        /// Зарегистрировать NPC в хранилище. Вызывается NPCSpawnerService.
        /// </summary>
        internal void RegisterNPC(NPCState state)
        {
            if (state == null || string.IsNullOrEmpty(state.NpcId)) return;
            _npcStates[state.NpcId] = state;
        }

        /// <summary>
        /// Удалить NPC из хранилища. Вызывается NPCSpawnerService.
        /// </summary>
        internal bool UnregisterNPC(string npcId)
        {
            return _npcStates.Remove(npcId);
        }

        /// <summary>
        /// Получить NPCState напрямую.
        /// MED-1: теперь публичный метод INPCService (был internal).
        /// </summary>
        public NPCState GetNPCState(string npcId)
        {
            _npcStates.TryGetValue(npcId, out var state);
            return state;
        }

        /// <summary>
        /// Получить все зарегистрированные NPCState (для AI-тика).
        /// </summary>
        internal Dictionary<string, NPCState>.ValueCollection GetAllStates()
        {
            return _npcStates.Values;
        }

        // === Обработчики событий ===

        /// <summary>
        /// Обработчик QiChangedEvent — кэшируем Ци игрока для AI.
        /// EVT-01: заменяет инъекцию IQiService.
        /// </summary>
        private void OnQiChanged(in QiChangedEvent e)
        {
            // Кэшируем Ци для каждого NPC (для AI-решений)
            // В будущих фазах: per-entity кэш по EntityId
            foreach (var state in _npcStates.Values)
            {
                state.CachedPlayerQi = e.Current;
                state.CachedPlayerLevel = e.CultivationLevel;
            }
        }

        // === Утилиты ===

        /// <summary>
        /// Конвертировать NPCState в NPCData (для внешнего API).
        /// </summary>
        private NPCData StateToData(NPCState state)
        {
            return new NPCData
            {
                NpcId = state.NpcId,
                PresetId = state.PresetId,
                DisplayName = state.DisplayName,
                Category = state.Category,
                Personality = state.Personality,
                Position = state.Position,
                // Расширенные поля (Phase 9)
                SoulType = state.SoulType,
                Morphology = state.Morphology,
                BodyMaterial = state.BodyMaterial,
                CultivationLevel = state.CultivationLevel,
                SubLevel = state.SubLevel,
                CoreQuality = state.CoreQuality,
                MaxQi = state.MaxQi,
                CurrentQi = state.CurrentQi,
                Conductivity = state.Conductivity,
                MaxHealth = state.MaxHealth,
                CurrentHealth = state.CurrentHealth,
                Role = state.Role,
                AIState = state.AIState,
                TargetId = state.TargetId,
                StateTimer = state.StateTimer,
                AttitudeScore = state.AttitudeScore,
                IsAlive = state.IsAlive,
                IsInCombat = state.IsInCombat,
                SectId = state.SectId,
                CurrentLocation = state.CurrentLocation
            };
        }

        // === ISaveable ===

        /// <summary>
        /// Ключ сохранения для модуля NPC.
        /// </summary>
        public string SaveKey => "npc";

        /// <summary>
        /// Сериализовать состояние NPC в JSON.
        /// Сохраняются: количество активных NPC и упрощённый список
        /// (NpcId, IsAlive, AIState (int), PosX, PosY, AttitudeScore, CurrentHealth).
        /// </summary>
        public object CaptureState()
        {
            // Формируем упрощённый список записей NPC для сериализации
            var entries = new NPCSaveEntry[_npcStates.Count];
            int i = 0;
            foreach (var kvp in _npcStates)
            {
                var s = kvp.Value;
                entries[i++] = new NPCSaveEntry
                {
                    NpcId = s.NpcId ?? "",
                    IsAlive = s.IsAlive,
                    AIState = (int)s.AIState,
                    PosX = s.Position.X,
                    PosY = s.Position.Y,
                    AttitudeScore = s.AttitudeScore,
                    CurrentHealth = s.CurrentHealth,
                    // D3 FIX: критические данные для полного восстановления
                    MaxHealth = s.MaxHealth,
                    CurrentQi = s.CurrentQi.ToString(),
                    MaxQi = s.MaxQi.ToString(),
                    CultivationLevel = (int)s.CultivationLevel,
                    SubLevel = s.SubLevel,
                    CoreQuality = (int)s.CoreQuality,
                    Role = (int)s.Role,
                    IsInCombat = s.IsInCombat,
                    TargetId = s.TargetId ?? "",
                    SoulType = (int)s.SoulType,
                    Morphology = (int)s.Morphology,
                    BodyMaterial = (int)s.BodyMaterial,
                    SectId = s.SectId ?? "",
                    CurrentLocation = s.CurrentLocation ?? "",
                    // Фаза 1: новые поля пайплайна (C5)
                    DisplayName = s.DisplayName ?? "",
                    Category = (int)s.Category,
                    Personality = (int)s.Personality,
                    Conductivity = s.Conductivity,
                    SpeciesId = s.SpeciesId ?? "",
                    StateTimer = s.StateTimer,
                    Age = s.Age,
                    AwakeningAge = s.AwakeningAge, // Фаза 4, задача 4.6
                    AwakeningType = (int)s.AwakeningType,
                    MortalStage = (int)s.MortalStage,
                    QiDensity = s.QiDensity,
                    MaxLifespan = s.MaxLifespan,
                    BaseDamage = s.BaseDamage,
                    BaseDefense = s.BaseDefense,
                    AggressionLevel = s.AggressionLevel,

                    // Волна 3: flat-массивы для BodyParts (Decision A)
                    BodyPartCount = s.BodyParts.Count,
                    BodyPartTypes = SerializeBodyPartTypes(s.BodyParts),
                    BodyPartRedHP = SerializeBodyPartRedHP(s.BodyParts),
                    BodyPartBlackHP = SerializeBodyPartBlackHP(s.BodyParts),
                    BodyPartMaxRedHP = SerializeBodyPartMaxRedHP(s.BodyParts),
                    BodyPartMaxBlackHP = SerializeBodyPartMaxBlackHP(s.BodyParts),
                    BodyPartIsVital = SerializeBodyPartIsVital(s.BodyParts),

                    // TechniqueIds — объединённая строка с разделителем
                    TechniqueIdsJoined = string.Join("|", s.TechniqueIds),

                    // Equipment — flat-массивы (joined strings для совместимости с JsonUtility)
                    EquipmentSlots = SerializeEquipmentSlots(s.EquipmentIds),
                    EquipmentItemIdsJoined = SerializeEquipmentItemIds(s.EquipmentIds),

                    // Inventory — flat-массивы
                    InventoryItemIdsJoined = SerializeInventoryItemIds(s.InventorySlots),
                    InventoryCounts = SerializeInventoryCounts(s.InventorySlots),
                    InventoryCategories = SerializeInventoryCategories(s.InventorySlots),
                    InventoryRarities = SerializeInventoryRarities(s.InventorySlots),

                    // Stats (int — решение дизайнера #7)
                    Strength = s.Strength,
                    Agility = s.Agility,
                    Vitality = s.Vitality,
                    Intelligence = s.Intelligence,

                    // Спринт 3 B6: InnateElement
                    InnateElement = (int)s.InnateElement
                };
            }

            var data = new NPCSaveData
            {
                ActiveNPCCount = _npcStates.Count,
                Entries = entries
            };
            return data;
        }

        /// <summary>
        /// Восстановить состояние NPC.
        /// Задача 4.4: если NPC из сохранения не найден в реестре —
        /// создаём новый NPCState и регистрируем в _npcStates.
        /// Runtime-коллекции (BodyParts, TechniqueIds, EquipmentIds, InventorySlots)
        /// инициализируются пустыми — они не сериализуются в NPCSaveEntry.
        /// </summary>
        public void RestoreState(object state)
        {
            if (state is not NPCSaveData data) return;
            if (data.Entries == null) return;

            foreach (var entry in data.Entries)
            {
                if (string.IsNullOrEmpty(entry.NpcId)) continue;

                if (!_npcStates.TryGetValue(entry.NpcId, out var npcState))
                {
                    // Задача 4.4: создаём нового NPC из сохранения, если не найден в реестре
                    npcState = new NPCState
                    {
                        NpcId = entry.NpcId,
                        // Идентификация — PresetId неизвестен при восстановлении из save
                        PresetId = "",
                        DisplayName = entry.DisplayName ?? "",
                        SpeciesId = entry.SpeciesId ?? "",

                        // Классификация
                        Role = (NPCRole)entry.Role,
                        Category = (NPCCategory)entry.Category,
                        Personality = (PersonalityTrait)entry.Personality,
                        SoulType = (SoulType)entry.SoulType,
                        Morphology = (Morphology)entry.Morphology,
                        BodyMaterial = (BodyMaterial)entry.BodyMaterial,

                        // Runtime-коллекции — пустые (не сериализуются в NPCSaveEntry)
                        BodyParts = new List<BodyPart>(),
                        TechniqueIds = new List<string>(),
                        EquipmentIds = new Dictionary<EquipmentSlot, string>(),
                        InventorySlots = new List<InventorySlot>(),
                        Threats = new Dictionary<string, float>(),

                        // Базовые статы — значения по умолчанию (не сериализуются)
                        Strength = 0,
                        Agility = 0,
                        Vitality = 0,
                        Intelligence = 0,

                        // Кэш Ци — начальные значения (обновятся при QiChangedEvent)
                        CachedPlayerQi = 0,
                        CachedPlayerLevel = 1
                    };

                    _npcStates[entry.NpcId] = npcState;
                }

                // Обновляем ВСЕ сериализуемые поля (для новых и существующих NPC)
                npcState.IsAlive = entry.IsAlive;
                npcState.AIState = (NPCAIState)entry.AIState;
                npcState.Position = new Position2D((int)entry.PosX, (int)entry.PosY);
                npcState.AttitudeScore = entry.AttitudeScore;
                npcState.CurrentHealth = entry.CurrentHealth;

                // D3 FIX: восстановление дополнительных полей
                npcState.MaxHealth = entry.MaxHealth;
                if (long.TryParse(entry.CurrentQi, out var currentQi))
                    npcState.CurrentQi = currentQi;
                if (long.TryParse(entry.MaxQi, out var maxQi))
                    npcState.MaxQi = maxQi;
                npcState.CultivationLevel = (CultivationLevel)entry.CultivationLevel;
                npcState.SubLevel = entry.SubLevel;
                npcState.CoreQuality = (CoreQuality)entry.CoreQuality;
                npcState.Role = (NPCRole)entry.Role;
                npcState.IsInCombat = entry.IsInCombat;
                npcState.TargetId = entry.TargetId;
                npcState.SoulType = (SoulType)entry.SoulType;
                npcState.Morphology = (Morphology)entry.Morphology;
                npcState.BodyMaterial = (BodyMaterial)entry.BodyMaterial;
                npcState.SectId = entry.SectId;
                npcState.CurrentLocation = entry.CurrentLocation;

                // Фаза 1: восстановление новых полей пайплайна
                if (!string.IsNullOrEmpty(entry.DisplayName))
                    npcState.DisplayName = entry.DisplayName;
                npcState.Category = (NPCCategory)entry.Category;
                npcState.Personality = (PersonalityTrait)entry.Personality;
                npcState.Conductivity = entry.Conductivity;
                if (!string.IsNullOrEmpty(entry.SpeciesId))
                    npcState.SpeciesId = entry.SpeciesId;
                npcState.StateTimer = entry.StateTimer;
                npcState.Age = entry.Age;
                npcState.AwakeningAge = entry.AwakeningAge; // Фаза 4, задача 4.6
                npcState.AwakeningType = (AwakeningType)entry.AwakeningType;
                npcState.MortalStage = (MortalStage)entry.MortalStage;
                npcState.QiDensity = entry.QiDensity;
                npcState.MaxLifespan = entry.MaxLifespan;
                npcState.BaseDamage = entry.BaseDamage;
                npcState.BaseDefense = entry.BaseDefense;
                npcState.AggressionLevel = entry.AggressionLevel;

                // Волна 3: Восстановление BodyParts из flat-массивов (Decision A)
                if (entry.BodyPartCount > 0 && entry.BodyPartTypes != null)
                {
                    var bpList = new List<BodyPart>(entry.BodyPartCount);
                    for (int j = 0; j < entry.BodyPartCount; j++)
                    {
                        bool isVital = entry.BodyPartIsVital != null && j < entry.BodyPartIsVital.Length
                            ? entry.BodyPartIsVital[j] != 0 : false;
                        var bp = new BodyPart(
                            (BodyPartType)entry.BodyPartTypes[j],
                            entry.BodyPartMaxRedHP[j],
                            isVital);
                        bp.SetHP(entry.BodyPartRedHP[j], entry.BodyPartBlackHP[j]);
                        bpList.Add(bp);
                    }
                    npcState.BodyParts = bpList;
                }

                // TechniqueIds — из joined-строки
                if (!string.IsNullOrEmpty(entry.TechniqueIdsJoined))
                    npcState.TechniqueIds = new List<string>(entry.TechniqueIdsJoined.Split('|'));

                // Equipment — из flat-массивов
                if (entry.EquipmentSlots != null && !string.IsNullOrEmpty(entry.EquipmentItemIdsJoined))
                {
                    var itemIds = entry.EquipmentItemIdsJoined.Split('|');
                    npcState.EquipmentIds = new Dictionary<EquipmentSlot, string>();
                    for (int j = 0; j < entry.EquipmentSlots.Length && j < itemIds.Length; j++)
                        npcState.EquipmentIds[(EquipmentSlot)entry.EquipmentSlots[j]] = itemIds[j];
                }

                // Inventory — из flat-массивов
                if (!string.IsNullOrEmpty(entry.InventoryItemIdsJoined) && entry.InventoryCounts != null)
                {
                    var invIds = entry.InventoryItemIdsJoined.Split('|');
                    npcState.InventorySlots = new List<InventorySlot>();
                    for (int j = 0; j < invIds.Length && j < entry.InventoryCounts.Length; j++)
                    {
                        var cat = j < entry.InventoryCategories.Length ? (ItemCategory)entry.InventoryCategories[j] : ItemCategory.Material;
                        var rar = j < entry.InventoryRarities.Length ? (ItemRarity)entry.InventoryRarities[j] : ItemRarity.Common;
                        npcState.InventorySlots.Add(new InventorySlot(invIds[j], entry.InventoryCounts[j], cat, rar));
                    }
                }

                // Stats (int — решение дизайнера #7)
                npcState.Strength = entry.Strength;
                npcState.Agility = entry.Agility;
                npcState.Vitality = entry.Vitality;
                npcState.Intelligence = entry.Intelligence;

                // Спринт 3 B6: InnateElement
                npcState.InnateElement = (Element)entry.InnateElement;

                // Волна 3: Регистрация в per-entity провайдерах после загрузки
                if (npcState.IsAlive)
                {
                    _bodyDataProvider.SetBodyParts(entry.NpcId, npcState.BodyParts);
                    _qiDataProvider.SetQiState(entry.NpcId, npcState.CurrentQi, npcState.MaxQi, npcState.Conductivity);
                    _equipmentDataProvider.SetEquipment(entry.NpcId, npcState.EquipmentIds);
                    _equipmentDataProvider.SetTotalArmor(entry.NpcId, npcState.BaseDefense);
                    _equipmentDataProvider.SetTotalDamage(entry.NpcId, npcState.BaseDamage);
                }
            }
        }

        // === Вспомогательные методы сериализации (Волна 3) ===

        private static int[] SerializeBodyPartTypes(List<BodyPart> parts)
        {
            var result = new int[parts.Count];
            for (int i = 0; i < parts.Count; i++) result[i] = (int)parts[i].Type;
            return result;
        }
        private static int[] SerializeBodyPartRedHP(List<BodyPart> parts)
        {
            var result = new int[parts.Count];
            for (int i = 0; i < parts.Count; i++) result[i] = parts[i].CurrentRedHP;
            return result;
        }
        private static int[] SerializeBodyPartBlackHP(List<BodyPart> parts)
        {
            var result = new int[parts.Count];
            for (int i = 0; i < parts.Count; i++) result[i] = parts[i].CurrentBlackHP;
            return result;
        }
        private static int[] SerializeBodyPartMaxRedHP(List<BodyPart> parts)
        {
            var result = new int[parts.Count];
            for (int i = 0; i < parts.Count; i++) result[i] = parts[i].MaxRedHP;
            return result;
        }
        private static int[] SerializeBodyPartMaxBlackHP(List<BodyPart> parts)
        {
            var result = new int[parts.Count];
            for (int i = 0; i < parts.Count; i++) result[i] = parts[i].MaxBlackHP;
            return result;
        }
        private static int[] SerializeBodyPartIsVital(List<BodyPart> parts)
        {
            var result = new int[parts.Count];
            for (int i = 0; i < parts.Count; i++) result[i] = parts[i].IsVital ? 1 : 0;
            return result;
        }
        private static int[] SerializeEquipmentSlots(Dictionary<EquipmentSlot, string> eq)
        {
            var result = new int[eq.Count];
            int i = 0;
            foreach (var kvp in eq) result[i++] = (int)kvp.Key;
            return result;
        }
        private static string SerializeEquipmentItemIds(Dictionary<EquipmentSlot, string> eq)
        {
            var ids = new string[eq.Count];
            int i = 0;
            foreach (var kvp in eq) ids[i++] = kvp.Value ?? "";
            return string.Join("|", ids);
        }
        private static string SerializeInventoryItemIds(List<InventorySlot> inv)
        {
            var ids = new string[inv.Count];
            for (int i = 0; i < inv.Count; i++) ids[i] = inv[i].ItemId ?? "";
            return string.Join("|", ids);
        }
        private static int[] SerializeInventoryCounts(List<InventorySlot> inv)
        {
            var result = new int[inv.Count];
            for (int i = 0; i < inv.Count; i++) result[i] = inv[i].Count;
            return result;
        }
        private static int[] SerializeInventoryCategories(List<InventorySlot> inv)
        {
            var result = new int[inv.Count];
            for (int i = 0; i < inv.Count; i++) result[i] = (int)inv[i].Category;
            return result;
        }
        private static int[] SerializeInventoryRarities(List<InventorySlot> inv)
        {
            var result = new int[inv.Count];
            for (int i = 0; i < inv.Count; i++) result[i] = (int)inv[i].Rarity;
            return result;
        }

        public void Dispose()
        {
            _qiChangedSubscription?.Dispose();
            _qiChangedSubscription = null;
            _npcStates.Clear();
        }
    }

    /// <summary>
    /// Структура данных для сериализации состояния NPC.
    /// JsonUtility требует [Serializable] и публичные поля.
    /// Содержит массив упрощённых записей для каждого NPC.
    /// </summary>
    [Serializable]
    public struct NPCSaveData
    {
        public int ActiveNPCCount;
        public NPCSaveEntry[] Entries;
    }

    /// <summary>
    /// Упрощённая запись состояния одного NPC для сериализации.
    /// Содержит ключевые поля, достаточные для восстановления при загрузке.
    /// Фаза 1: расширена полями пайплайна генерации (C5).
    /// </summary>
    [Serializable]
    public struct NPCSaveEntry
    {
        // Идентификация
        public string NpcId;
        public string DisplayName;       // Фаза 1
        public string SpeciesId;         // Фаза 1 (замена PresetId)

        // Состояние жизни
        public bool IsAlive;
        public int CurrentHealth;
        public int MaxHealth;

        // Культивация и Ци
        public int CultivationLevel;
        public int SubLevel;
        public int CoreQuality;
        public string CurrentQi;         // long как string — совместимость с JsonUtility
        public string MaxQi;             // long как string
        public float Conductivity;        // Фаза 1

        // Позиция и AI
        public float PosX;
        public float PosY;
        public int AIState;
        public string TargetId;
        public float StateTimer;          // Фаза 1

        // Классификация
        public int Role;
        public int Category;              // Фаза 1: NPCCategory
        public int Personality;            // Фаза 1: PersonalityTrait [Flags]
        public int SoulType;
        public int Morphology;
        public int BodyMaterial;

        // Параметры пайплайна (Фаза 1)
        public int Age;
        public int AwakeningAge;       // Фаза 4, задача 4.6: возраст пробуждения
        public int AwakeningType;
        public int MortalStage;
        public int QiDensity;
        public int MaxLifespan;

        // Боевые параметры
        public int BaseDamage;
        public int BaseDefense;
        public float AggressionLevel;
        public bool IsInCombat;

        // Принадлежность
        public int AttitudeScore;
        public string SectId;
        public string CurrentLocation;

        // Волна 3: flat-массивы для BodyParts (Decision A: Flat-массивы)
        public int BodyPartCount;
        public int[] BodyPartTypes;
        public int[] BodyPartRedHP;
        public int[] BodyPartBlackHP;
        public int[] BodyPartMaxRedHP;
        public int[] BodyPartMaxBlackHP;
        public int[] BodyPartIsVital;

        // TechniqueIds — объединённая строка с разделителем |
        public string TechniqueIdsJoined;

        // Equipment — flat-массивы (joined string для совместимости с JsonUtility)
        public int[] EquipmentSlots;
        public string EquipmentItemIdsJoined;

        // Inventory — flat-массивы (joined string для itemIds)
        public string InventoryItemIdsJoined;
        public int[] InventoryCounts;
        public int[] InventoryCategories;
        public int[] InventoryRarities;

        // Stats (int — решение дизайнера #7 + ЗАПРЕТ 3.9)
        public int Strength;
        public int Agility;
        public int Vitality;
        public int Intelligence;

        // Спринт 3 B6: InnateElement (int — Element enum)
        public int InnateElement;
    }
}
