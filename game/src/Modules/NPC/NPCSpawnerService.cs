#nullable enable
// Создано: 2026-05-09 15:30:00 UTC
// Редактировано: 2026-05-21 08:15:33 UTC — Волна 2.1: per-entity регистрация NPC при спавне + очистка при деспавне
// Редактировано: 2026-05-22 13:50:00 UTC — Этап 3.3: P2-X1 FIX: BuffService.RemoveAllBuffs при деспавне NPC
// Реализация INPCSpawnerService.
// Спавн и деспавн NPC: делегирует NPCAssemblyService для полной генерации.
// EVT-01: Все кросс-модульные взаимодействия — через MessagePipe.
// Задача 1.9: LocationData.DangerLevel — IWorldService для определения уровня локации.
// Задача 3.B: NPC.CurrentLocation — назначается при спавне, обновляется через LocationChangedEvent.
using System;
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Core.Events;

namespace CultivationGame.Modules.NPC
{
    /// <summary>
    /// Реализация INPCSpawnerService.
    /// Создаёт NPC через NPCAssemblyService (полный пайплайн генерации),
    /// регистрирует в NPCService, публикует NPCSpawnedEvent.
    ///
    /// Фаза 1: новый SpawnNPC делегирует _assemblyService.Assemble().
    /// Старый SpawnNPC(presetId, position) — [Obsolete], мапит preset→species+role.
    /// </summary>
    public class NPCSpawnerService : INPCSpawnerService, IDisposable
    {
        // === Зависимости ===
        private readonly NPCService _npcService;
        private readonly NPCMovementService _movementService;
        private readonly NPCRelationshipService _relationshipService;
        private readonly NPCAssemblyService _assemblyService;
        private readonly NPCConfig _config;
        private readonly IWorldService _worldService; // Задача 1.9: для DangerLevel локации

        // Волна 2.1: per-entity провайдеры для регистрации NPC
        private readonly IBodyDataProvider _bodyDataProvider;
        private readonly IQiDataProvider _qiDataProvider;
        private readonly IEquipmentDataProvider _equipmentDataProvider;
        private readonly IBuffService _buffService; // Этап 3.3: очистка баффов при деспавне

        // === MessagePipe: паблишеры ===
        private readonly IPublisher<NPCSpawnedEvent> _spawnedPub;
        private readonly IPublisher<NPCDespawnedEvent> _despawnedPub;

        // === MessagePipe: подписки (Задача 3.B) ===
        private readonly ISubscriber<LocationChangedEvent> _locationChangedSub;

        // 2026-08-22, этап 2: генератор экипировки «Матрёшка»
        private readonly IEquipmentGenerator _equipmentGenerator;
        private IDisposable _locationChangedSubscription;

        // === Состояние ===
        private readonly HashSet<string> _spawnedIds = new HashSet<string>();
        private string _currentLocationId; // Кэш текущей локации (Задача 3.B)

        // === Конструктор (VContainer) ===
        public NPCSpawnerService(
            NPCService npcService,
            NPCMovementService movementService,
            NPCRelationshipService relationshipService,
            NPCAssemblyService assemblyService,
            NPCConfig config,
            IWorldService worldService,
            IBodyDataProvider bodyDataProvider,
            IQiDataProvider qiDataProvider,
            IEquipmentDataProvider equipmentDataProvider,
            IBuffService buffService, // Этап 3.3: очистка баффов при деспавне
            IPublisher<NPCSpawnedEvent> spawnedPub,
            IPublisher<NPCDespawnedEvent> despawnedPub,
            ISubscriber<LocationChangedEvent> locationChangedSub,
            IEquipmentGenerator equipmentGenerator) // 2026-08-22, этап 2
        {
            _npcService = npcService;
            _movementService = movementService;
            _relationshipService = relationshipService;
            _assemblyService = assemblyService;
            _config = config;
            _worldService = worldService;
            _bodyDataProvider = bodyDataProvider;
            _qiDataProvider = qiDataProvider;
            _equipmentDataProvider = equipmentDataProvider;
            _buffService = buffService; // Этап 3.3
            _spawnedPub = spawnedPub;
            _despawnedPub = despawnedPub;
            _locationChangedSub = locationChangedSub;
            _equipmentGenerator = equipmentGenerator;

            // Инициализация текущей локации (Задача 1.9)
            _currentLocationId = _worldService?.CurrentLocationId;
        }

        /// <summary>
        /// Инициализация: подписка на LocationChangedEvent (Задача 3.B).
        /// Вызывается из NPCModule.Start().
        /// </summary>
        public void Initialize()
        {
            _locationChangedSubscription = _locationChangedSub?.Subscribe(OnLocationChanged);
        }

        // === INPCSpawnerService ===

        /// <summary>
        /// Legacy-спавн NPC по пресету.
        /// Мапит presetId → speciesId + roleId, делегирует новому SpawnNPC.
        /// Задача 1.9: пытается получить DangerLevel из IWorldService.
        /// </summary>
        [Obsolete("Используйте SpawnNPC с speciesId, roleId, locationLevel")]
        public string SpawnNPC(string presetId, Position2D position)
        {
            // Маппинг preset → species + role
            NPCRole role = ParsePresetRole(presetId);
            string speciesId = GetDefaultSpeciesId(role);

            // Задача 1.9: получаем DangerLevel из IWorldService, если доступен
            int locationLevel = GetLocationDangerLevel() ?? GetDefaultLocationLevel(role);

            // Генерация seed из presetId
            long seed = presetId != null ? presetId.GetHashCode() : DateTime.UtcNow.Ticks;

            #pragma warning disable CS0618 // Подавить предупреждение о устаревшем методе
            return SpawnNPC(speciesId, role, locationLevel, position, seed);
            #pragma warning restore CS0618
        }

        /// <summary>
        /// Спавн NPC через полный пайплайн генерации.
        /// Делегирует NPCAssemblyService.Assemble(), регистрирует в NPCService.
        /// Задача 3.B: устанавливает CurrentLocation из кэша.
        /// </summary>
        public string SpawnNPC(string speciesId, NPCRole roleId, int locationLevel, Position2D position, long seed)
        {
            // Проверка лимита активных NPC
            if (_spawnedIds.Count >= _config.MaxActiveNPCs) return null;

            // Полная генерация через AssemblyService
            NPCState state;
            try
            {
                state = _assemblyService.Assemble(speciesId, roleId, locationLevel, position, seed);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NPCSpawnerService] Ошибка генерации NPC: {ex.Message}");
                return null;
            }

            if (state == null) return null;

            string npcId = state.NpcId;

            // Задача 3.B: назначаем текущую локацию NPC
            if (string.IsNullOrEmpty(state.CurrentLocation))
                state.CurrentLocation = _currentLocationId;

            // 2026-08-22, физический прототип: диспозиция к игроку по роли.
            state.Disposition = RoleToDisposition(roleId);

            // 2026-08-22 (этап 2): экипировка из генератора «Матрёшка» —
            // реальные Damage/Penetration/Defense вместо упрощённой формулы
            // «+5 за предмет». Enemy получает оружие уровнем выше.
            EquipFromGenerator(state, roleId, locationLevel, seed);

            // Регистрация в NPCService
            _npcService.RegisterNPC(state);
            _spawnedIds.Add(npcId);

            // Волна 2.1: Регистрация в per-entity провайдерах
            // Без этого NPC невидим для боевой системы, формаций и регенерации Ци
            _bodyDataProvider.SetBodyParts(npcId, state.BodyParts);
            _qiDataProvider.SetQiState(npcId, state.CurrentQi, state.MaxQi, state.Conductivity);
            _equipmentDataProvider.SetEquipment(npcId, state.EquipmentIds);
            // Спринт 8 C12: TotalArmor = equipment armor + BaseDefense (NaturalArmor)
            int equipmentArmor = CalculateEquipmentArmor(state);
            int totalArmor = equipmentArmor + state.BaseDefense;
            _equipmentDataProvider.SetTotalArmor(npcId, totalArmor);
            _equipmentDataProvider.SetTotalDamage(npcId, state.BaseDamage + _generatedDamage.GetValueOrDefault(npcId));
            _generatedDamage.Remove(npcId);

            // Регистрация точки спавна в сервисе движения
            _movementService.RegisterSpawnPosition(npcId, position);

            // Публикация события спавна (V-03: SpeciesId + RoleId)
            _spawnedPub.Publish(new NPCSpawnedEvent(npcId, speciesId, roleId));

            return npcId;
        }

        /// <summary>
        /// Деспавнить NPC. Удаляет из NPCService, публикует событие.
        /// </summary>
        public void DespawnNPC(string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return;
            if (!_spawnedIds.Contains(npcId)) return;

            // Волна 2.1: Очистка per-entity провайдеров при деспавне
            _bodyDataProvider.RemoveEntity(npcId);
            _qiDataProvider.RemoveEntity(npcId);
            _equipmentDataProvider.RemoveEntity(npcId);
            // Этап 3.3: P2-X1 FIX — очистка баффов/дебаффов NPC при деспавне
            _buffService.RemoveAllBuffs(npcId);

            // Удаление точки спавна из сервиса движения
            _movementService.UnregisterSpawnPosition(npcId);

            // NPC-A12: чистим отношения при деспавне
            _relationshipService.RemoveAllForNPC(npcId);

            // Удаление из NPCService
            _npcService.UnregisterNPC(npcId);
            _spawnedIds.Remove(npcId);

            // Публикация события деспавна
            _despawnedPub.Publish(new NPCDespawnedEvent(npcId));
        }

        /// <summary>
        /// Получить идентификаторы всех заспавненных NPC.
        /// </summary>
        public IReadOnlyList<string> GetSpawnedNPCIds()
        {
            var result = new List<string>(_spawnedIds.Count);
            foreach (var id in _spawnedIds)
                result.Add(id);
            return result;
        }

        /// <summary>
        /// Количество активных NPC.
        /// </summary>
        public int ActiveNPCCount => _spawnedIds.Count;

        // === Внутренние методы (legacy-маппинг) ===

        // Сгенерированный урон оружия по npcId (до регистрации в провайдере).
        private readonly Dictionary<string, int> _generatedDamage = new();

        /// <summary>
        /// Экипировка NPC из генератора «Матрёшка» (2026-08-22, этап 2):
        /// оружие всем гуманоидам; броня — 60% шанс. Предметы регистрируются
        /// в ItemDatabase, экипируется в state.EquipmentIds, урон оружия
        /// добавляется к BaseDamage. Enemy/Monster — оружие уровнем выше.
        /// </summary>
        private void EquipFromGenerator(NPCState state, NPCRole role, int locationLevel, long seed)
        {
            if (_equipmentGenerator == null || state.EquipmentIds == null) return;

            int weaponLevel = System.Math.Clamp(
                locationLevel + (state.Disposition == NPCDisposition.Hostile ? 1 : 0), 1, 9);

            try
            {
                var weapon = _equipmentGenerator.GenerateWeapon(weaponLevel, null, seed);
                state.EquipmentIds[EquipmentSlot.WeaponMain] = weapon.ItemId;
                _generatedDamage[state.NpcId] = weapon.Damage;

                // Armor: 60% шанс, торс или голова.
                var equipRng = new SeededRandom(seed ^ 0x5EED);
                if (equipRng.Next(0, 100) < 60)
                {
                    string armorSub = (seed & 1) == 0 ? "armor_torso" : "armor_head";
                    var armor = _equipmentGenerator.GenerateArmor(locationLevel, armorSub, seed + 2);
                    state.EquipmentIds[armor.Slot] = armor.ItemId;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NPCSpawnerService] EquipFromGenerator failed for {state.NpcId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Диспозиция к игроку по роли (2026-08-22, физический прототип):
        /// Enemy/Monster — Hostile; Guard — Friendly; Merchant — Merchant;
        /// остальные (Passerby/Cultivator/Elder/Disciple) — Neutral.
        /// </summary>
        private static NPCDisposition RoleToDisposition(NPCRole role) => role switch
        {
            NPCRole.Enemy   => NPCDisposition.Hostile,
            NPCRole.Monster => NPCDisposition.Hostile,
            NPCRole.Guard   => NPCDisposition.Friendly,
            NPCRole.Merchant => NPCDisposition.Merchant,
            _ => NPCDisposition.Neutral,
        };

        /// <summary>
        /// Определить роль NPC из идентификатора пресета.
        /// </summary>
        private NPCRole ParsePresetRole(string presetId)
        {
            if (string.IsNullOrEmpty(presetId)) return NPCRole.Passerby;
            string rolePart = presetId.Split('_')[0].ToLower();
            return rolePart switch
            {
                "monster" => NPCRole.Monster,
                "guard" => NPCRole.Guard,
                "merchant" => NPCRole.Merchant,
                "cultivator" => NPCRole.Cultivator,
                "elder" => NPCRole.Elder,
                "disciple" => NPCRole.Disciple,
                "enemy" => NPCRole.Enemy,
                "passerby" => NPCRole.Passerby,
                _ => NPCRole.Passerby
            };
        }

        /// <summary>
        /// Вид по умолчанию для роли.
        /// </summary>
        private string GetDefaultSpeciesId(NPCRole role)
        {
            return role switch
            {
                NPCRole.Monster => "wolf",
                _ => "human"
            };
        }

        /// <summary>
        /// Уровень локации по умолчанию для роли (legacy).
        /// </summary>
        private int GetDefaultLocationLevel(NPCRole role)
        {
            return role switch
            {
                NPCRole.Passerby => 0,
                NPCRole.Merchant => 1,
                NPCRole.Guard => 2,
                NPCRole.Cultivator => 3,
                NPCRole.Elder => 5,
                NPCRole.Enemy => 1,
                NPCRole.Monster => 1,
                NPCRole.Disciple => 2,
                _ => 1
            };
        }

        // === Обработчики событий ===

        /// <summary>
        /// Обработчик LocationChangedEvent — обновляет кэш текущей локации (Задача 3.B).
        /// Когда игрок перемещается, обновляем CurrentLocation для всех NPC.
        /// NPC, принадлежащие старой локации, могут быть деспавнены (TBD — Фаза 2+).
        /// </summary>
        private void OnLocationChanged(in LocationChangedEvent e)
        {
            _currentLocationId = e.NewLocationId;
        }

        // === Вспомогательные методы (Задача 1.9) ===

        /// <summary>
        /// Спринт 8 C12: Рассчитать броню экипировки NPC.
        /// Пока IItemDatabaseService не доступен — используем упрощённую формулу:
        /// каждая надетая броня +5, каждое оружие +0.
        /// В будущем: разрешить item ID через IItemDatabaseService для получения точных значений.
        /// </summary>
        private int CalculateEquipmentArmor(NPCState state)
        {
            int armor = 0;
            if (state.EquipmentIds == null) return 0;

            // Броневые слоты: Head, Torso, Legs, Feet
            var armorSlots = new[] {
                EquipmentSlot.Head,
                EquipmentSlot.Torso,
                EquipmentSlot.Legs,
                EquipmentSlot.Feet
            };

            foreach (var slot in armorSlots)
            {
                if (state.EquipmentIds.ContainsKey(slot) &&
                    !string.IsNullOrEmpty(state.EquipmentIds[slot]))
                {
                    armor += 5; // Упрощённо: +5 за каждый надетый предмет брони
                }
            }

            return armor;
        }

        /// <summary>
        /// Получить DangerLevel текущей локации из IWorldService.
        /// Возвращает null, если IWorldService недоступен.
        /// Задача 1.9: интеграция LocationData.DangerLevel.
        /// </summary>
        private int? GetLocationDangerLevel()
        {
            if (_worldService == null) return null;
            if (string.IsNullOrEmpty(_currentLocationId)) return null;

            var location = _worldService.GetLocation(_currentLocationId);
            // LocationInfo — struct, проверяем Id
            if (string.IsNullOrEmpty(location.Id)) return null;

            return location.DangerLevel;
        }

        public void Dispose()
        {
            _locationChangedSubscription?.Dispose();
            _locationChangedSubscription = null;
            _spawnedIds.Clear();
        }
    }
}
