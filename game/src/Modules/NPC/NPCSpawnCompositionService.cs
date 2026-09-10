#nullable enable
// Создано: 2026-09-10 — R13 «NPC спаун через генерацию».
// Генератор состава населения локации: заменяет ХАРДКОД-массив SpawnRoles
// в HumanNPCSpawnPhase (7 фиксированных ролей) на процедурную композицию,
// выводимую из типа локации (Farm/WildLands/Dungeon...), уровня опасности
// и сида локации — детерминированно для одного и того же сида.
//
// Плюс поддержание популяции (ITickable): мир «живёт» — если игрок выбивает
// население (full-loot цикл: убил → обыскал), ReinforcementTick со временем
// генерирует замену (новый сгенерированный NPC вне поля зрения игрока).
// Зависимости инжектятся конструктором (паттерн AnimalService/NPCService).
//
// ДЕТЕРМИНИЗМ: стартовый состав — SeededRandom(loc.Seed + offset); уровни
// ролей — независимые броски того же потока. Респаун-цикл эмерджентный
// (зависит от действий игрока), его RNG сидируется игровым временем.
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.NPC
{
    /// <summary>
    /// Запрос на спавн одного NPC (роль, уровень, вид).
    /// </summary>
    public readonly struct SpawnRequest
    {
        public readonly NPCRole Role;
        public readonly int Level;
        public readonly string SpeciesId;

        public SpawnRequest(NPCRole role, int level, string speciesId = "human")
            { Role = role; Level = level; SpeciesId = speciesId ?? "human"; }

        public override string ToString() => $"{SpeciesId}/{Role} L{Level}";
    }

    /// <summary>
    /// Генератор состава населения локации («NPC спаун через генерацию»).
    ///
    /// Правила композиции по типу локации (детерминированно от сида):
    ///  • Farm/Village/Sect — мирное ядро: торговец, старейшина, стража,
    ///    прохожие, культиваторы + бандиты по уровню опасности.
    ///  • WildLands/Dungeon/Secret — дикое: бандиты/монстры доминируют,
    ///    мирные редки.
    ///  • Прочие (Region/Area/...) — смешанный состав.
    /// DangerLevel поднимает уровни и число врагов.
    /// </summary>
    public class NPCSpawnCompositionService
    {
        // Независимый RNG-поток (prime-offset, паттерн фаз спавна).
        private const int CompositionSeedOffset = 31337;
        private const int ReinforcementSeedOffset = 60013;

        /// <summary>Кап на общий размер композиции (защита малых карт).</summary>
        private const int MaxCompositionSize = 12;

        // === Респаун (поддержание популяции) ===
        /// <summary>Интервал проверки популяции (игровые секунды).</summary>
        private const float ReinforcementCheckIntervalSec = 45f;
        /// <summary>Минимальная дистанция респауна от игрока (тайлы, Чебышёв).</summary>
        private const int ReinforcementMinPlayerDistance = 12;
        /// <summary>Доля от стартового состава, ниже которой включается респаун.</summary>
        private const float PopulationFloorRatio = 0.6f;
        private const int MaxSpawnAttempts = 60;

        private readonly NPCService _npcService;
        private readonly INPCSpawnerService _spawner;
        private readonly IWorldService _worldService;
        private readonly ITileService _tiles;
        private readonly ITimeService _timeService;
        private readonly IPlayerService _playerService;
        private int _nextReinforcementSeq = 1;

        /// <summary>Целевой размер населения (стартовый состав текущей локации).</summary>
        private int _targetPopulation;
        private float _lastCheckGameSeconds = -1f;

        public NPCSpawnCompositionService(
            NPCService npcService,
            INPCSpawnerService spawner,
            IWorldService worldService,
            ITileService tiles,
            ITimeService timeService,
            IPlayerService playerService)
        {
            _npcService = npcService ?? throw new ArgumentNullException(nameof(npcService));
            _spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
            _worldService = worldService;
            _tiles = tiles;
            _timeService = timeService;
            _playerService = playerService;
        }

        // === QA-диагностика ===

        /// <summary>Целевая численность населения (QA).</summary>
        public int TargetPopulation => _targetPopulation;

        /// <summary>Число респаунов с момента старта (QA: мир восполняется).</summary>
        public int ReinforcementCount { get; private set; }

        // === Стартовый состав ===

        /// <summary>
        /// Сгенерировать стартовый состав населения локации.
        /// Детерминированно: один seed → один состав.
        /// </summary>
        public List<SpawnRequest> GenerateStartup(LocationData location)
        {
            var rng = new SeededRandom((location?.Seed ?? 0) + CompositionSeedOffset);
            int danger = Math.Max(0, location?.DangerLevel ?? 0);
            var requests = new List<SpawnRequest>();

            bool peaceful = IsPeacefulLocation(location);
            bool wild = IsWildLocation(location);

            if (peaceful)
            {
                // Мирное ядро: торговец всегда, старейшина при danger==0.
                requests.Add(new SpawnRequest(NPCRole.Merchant, RollLevel(rng, 1, danger)));
                if (danger == 0)
                    requests.Add(new SpawnRequest(NPCRole.Elder, RollLevel(rng, 2, danger)));

                int guards = 1 + (danger >= 2 ? 1 : 0) + rng.Next(0, 2);
                for (int i = 0; i < guards; i++)
                    requests.Add(new SpawnRequest(NPCRole.Guard, RollLevel(rng, 2, danger)));

                int passersby = 2 + rng.Next(0, 2 + danger / 2);
                for (int i = 0; i < passersby; i++)
                    requests.Add(new SpawnRequest(NPCRole.Passerby, RollLevel(rng, 0, danger)));

                int cultivators = 1 + rng.Next(0, 2);
                for (int i = 0; i < cultivators; i++)
                    requests.Add(new SpawnRequest(NPCRole.Cultivator, RollLevel(rng, 3, danger)));

                // Даже на ферме шалят бандиты: минимум 1 при danger 0.
                int enemies = Math.Max(1, danger + rng.Next(1, 3));
                for (int i = 0; i < enemies; i++)
                    requests.Add(new SpawnRequest(NPCRole.Enemy, RollLevel(rng, 1, danger)));
            }
            else if (wild)
            {
                // Дикие земли: враги доминируют, мирные редки.
                int enemies = 2 + danger + rng.Next(0, 3);
                for (int i = 0; i < enemies; i++)
                    requests.Add(new SpawnRequest(NPCRole.Enemy, RollLevel(rng, 1, danger)));

                int monsters = 1 + danger / 2 + rng.Next(0, 2);
                for (int i = 0; i < monsters; i++)
                    requests.Add(new SpawnRequest(NPCRole.Monster, RollLevel(rng, 1, danger), "wolf"));

                int cultivators = rng.Next(0, 2);
                for (int i = 0; i < cultivators; i++)
                    requests.Add(new SpawnRequest(NPCRole.Cultivator, RollLevel(rng, 2, danger)));

                int passersby = rng.Next(0, 2);
                for (int i = 0; i < passersby; i++)
                    requests.Add(new SpawnRequest(NPCRole.Passerby, RollLevel(rng, 0, danger)));

                int guards = rng.Next(0, 2);
                for (int i = 0; i < guards; i++)
                    requests.Add(new SpawnRequest(NPCRole.Guard, RollLevel(rng, 2, danger)));

                if (rng.Next(0, 2) == 1)
                    requests.Add(new SpawnRequest(NPCRole.Merchant, RollLevel(rng, 1, danger)));
            }
            else
            {
                // Смешанный состав (Region/Area/Building/Room/...).
                int enemies = 1 + danger / 2 + rng.Next(0, 2);
                for (int i = 0; i < enemies; i++)
                    requests.Add(new SpawnRequest(NPCRole.Enemy, RollLevel(rng, 1, danger)));

                int passersby = 1 + rng.Next(0, 3);
                for (int i = 0; i < passersby; i++)
                    requests.Add(new SpawnRequest(NPCRole.Passerby, RollLevel(rng, 0, danger)));

                int cultivators = rng.Next(0, 2);
                for (int i = 0; i < cultivators; i++)
                    requests.Add(new SpawnRequest(NPCRole.Cultivator, RollLevel(rng, 2, danger)));

                if (rng.Next(0, 2) == 1)
                    requests.Add(new SpawnRequest(NPCRole.Guard, RollLevel(rng, 2, danger)));

                if (rng.Next(0, 3) == 0)
                    requests.Add(new SpawnRequest(NPCRole.Merchant, RollLevel(rng, 1, danger)));
            }

            // Кап: лишние срезаются с КОНЦА (мирные роли идут раньше врагов —
            // при резе остаются мирные, враги уходят первыми: безопаснее).
            if (requests.Count > MaxCompositionSize)
                requests.RemoveRange(MaxCompositionSize, requests.Count - MaxCompositionSize);

            _targetPopulation = requests.Count;
            return requests;
        }

        // === Поддержание популяции (респаун) ===

        /// <summary>
        /// Периодическая проверка (вызывается из NPCModule.Tick): население
        /// просело ниже PopulationFloorRatio от целевого → одна попытка
        /// респауна за вызов. Возвращает ID нового NPC или null.
        /// Локация — IWorldService.CurrentLocation (единый источник геометрии/сида).
        /// forceCheck=true — обойти интервал (QA: детерминированный вызов).
        /// </summary>
        public string? ReinforcementTick(bool forceCheck = false)
        {
            var location = _worldService?.CurrentLocation;
            if (location == null) return null;
            if (_targetPopulation <= 0) return null;

            float now = _timeService?.TotalTime ?? 0f;
            if (!forceCheck && _lastCheckGameSeconds >= 0f
                && now - _lastCheckGameSeconds < ReinforcementCheckIntervalSec)
                return null;
            _lastCheckGameSeconds = now;

            // Считаем ТОЛЬКО живых NPC (трупы населением не считаются).
            int alive = 0;
            foreach (var id in _npcService.GetAllNPCIds())
                if (_npcService.IsAlive(id)) alive++;

            int floor = Math.Max(1, (int)Math.Ceiling(_targetPopulation * PopulationFloorRatio));
            if (alive >= floor) return null;

            // Роль замены: по типу локации (враги в диком мире, микс в мирном).
            var rng = new SeededRandom((location.Seed + ReinforcementSeedOffset)
                + (long)_nextReinforcementSeq * 7919 + (long)(now * 100f));
            bool wild = IsWildLocation(location);
            var request = wild
                ? new SpawnRequest(rng.Next(0, 3) switch
                    {
                        0 => NPCRole.Enemy,
                        1 => NPCRole.Enemy,
                        _ => NPCRole.Cultivator,
                    },
                    RollLevel(rng, 1, location.DangerLevel),
                    rng.Next(0, 4) == 0 ? "wolf" : "human")
                : new SpawnRequest(rng.Next(0, 6) switch
                    {
                        0 => NPCRole.Passerby,
                        1 => NPCRole.Cultivator,
                        2 => NPCRole.Guard,
                        3 => NPCRole.Enemy,
                        4 => NPCRole.Disciple,
                        _ => NPCRole.Enemy,
                    },
                    RollLevel(rng, 1, location.DangerLevel));

            // Позиция: walkable, вне поля зрения игрока.
            var pos = FindReinforcementPosition(rng, location);
            if (pos is null)
            {
                Console.WriteLine("[SpawnComposition] Reinforcement: позиция не найдена — отложено");
                return null;
            }

            long seed = location.Seed + ReinforcementSeedOffset + (long)_nextReinforcementSeq * 104729;
            _nextReinforcementSeq++;

            string npcId = _spawner.SpawnNPC(request.SpeciesId, request.Role, request.Level, pos.Value, seed);
            if (!string.IsNullOrEmpty(npcId))
            {
                ReinforcementCount++;
                Console.WriteLine($"[SpawnComposition] Reinforcement #{ReinforcementCount}: " +
                          $"{request} at ({pos.Value.X},{pos.Value.Y}) — население {alive}/{_targetPopulation}");
            }
            return npcId;
        }

        /// <summary>Сброс для повторной сборки сцены (ReAssembly).</summary>
        public void Reset()
        {
            _targetPopulation = 0;
            _lastCheckGameSeconds = -1f;
            ReinforcementCount = 0;
            _nextReinforcementSeq = 1;
        }

        // === Вспомогательные ===

        private static bool IsPeacefulLocation(LocationData? loc) => loc?.LocationType switch
        {
            LocationType.Farm => true,
            LocationType.Village => true,
            LocationType.Sect => true,
            _ => false,
        };

        private static bool IsWildLocation(LocationData? loc) => loc?.LocationType switch
        {
            LocationType.WildLands => true,
            LocationType.Dungeon => true,
            LocationType.Secret => true,
            _ => false,
        };

        /// <summary>Базовый уровень роли ± 1 с учётом опасности (кап 0..9).</summary>
        private static int RollLevel(SeededRandom rng, int baseLevel, int danger)
        {
            int delta = rng.Next(-1, 2); // -1..+1
            return Math.Clamp(baseLevel + delta + Math.Max(0, danger - 1), 0, 9);
        }

        private Position2D? FindReinforcementPosition(SeededRandom rng, LocationData loc)
        {
            int mapW = _tiles?.MapWidth > 0 ? _tiles.MapWidth : loc.Width;
            int mapH = _tiles?.MapHeight > 0 ? _tiles.MapHeight : loc.Height;
            var playerPos = _playerService?.Position ?? new Position2D(mapW / 2, mapH / 2);

            for (int attempt = 0; attempt < MaxSpawnAttempts; attempt++)
            {
                int x = rng.Next(1, Math.Max(2, mapW - 1));
                int y = rng.Next(1, Math.Max(2, mapH - 1));
                if (_tiles != null && !_tiles.IsWalkable(x, y)) continue;

                int dist = Math.Max(Math.Abs(x - playerPos.X), Math.Abs(y - playerPos.Y));
                if (dist < ReinforcementMinPlayerDistance) continue;

                return new Position2D(x, y);
            }
            return null;
        }
    }
}
