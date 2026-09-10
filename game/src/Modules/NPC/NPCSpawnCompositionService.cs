#nullable enable
// Создано: 2026-09-10 — R13 «NPC спаун через генерацию».
// РЕДАКТИРОВАНО (R14, 2026-09-10, запрос пользователя): внутрисессионное
// восполнение населения ЗАПРЕЩЕНО. Пока персонаж в локации, новые NPC не
// генерируются — убийства необратимы в рамках сессии. Единственная точка
// входа новых NPC при живом игроке — ивенты (TrySpawnEventNpc): приход
// каравана, нападение на локацию, другие событийные появления.
//
// Естественное восстановление населения — только при ОТСУТСТВИИ персонажа
// (он в другом поселении или на карте мира): при следующей (пере)сборке
// локации состав регенерируется GenerateStartup по правилу «таймера
// памяти» TRANSITION_SYSTEM.md §5.3/§11.1 (NPC возвращаются через 1
// игровой день после ухода игрока).
//
// Генератор состава населения локации: заменяет ХАРДКОД-массив SpawnRoles
// в HumanNPCSpawnPhase (7 фиксированных ролей) на процедурную композицию,
// выводимую из типа локации (Farm/WildLands/Dungeon...), уровня опасности
// и сида локации — детерминированно для одного и того же сида.
// Зависимости инжектятся конструктором (паттерн AnimalService/NPCService).
//
// ДЕТЕРМИНИЗМ: стартовый состав — SeededRandom(loc.Seed + offset); уровни
// ролей — независимые броски того же потока. Ивент-спауны сидируются
// порядковым номером и игровым временем (эмерджентность ивентов).
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
    /// R14: причина ивент-спауна NPC при живом игроке в локации.
    /// </summary>
    public enum NPCEventSpawnReason
    {
        /// <summary>Приход торгового каравана (GROUP_SYSTEM §3.5): торговец/охрана.</summary>
        Caravan,
        /// <summary>Нападение на локацию: враждебная волна.</summary>
        Raid,
        /// <summary>Другое событийное появление (квест, паломник, сюрприз).</summary>
        Event,
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
    ///
    /// R14: поддержание популяции в сессии УДАЛЕНО (ReinforcementTick) —
    /// мир НЕ восполняет потери, пока игрок в локации; только ивенты.
    /// </summary>
    public class NPCSpawnCompositionService
    {
        // Независимые RNG-потоки (prime-offset, паттерн фаз спавна).
        private const int CompositionSeedOffset = 31337;
        private const int EventSpawnSeedOffset = 60013;

        /// <summary>Кап на общий размер композиции (защита малых карт).</summary>
        private const int MaxCompositionSize = 12;

        // === Ивент-спаун (единственный внутрисессионный источник NPC) ===
        /// <summary>Минимальная дистанция ивент-спауна от игрока (тайлы, Чебышёв).</summary>
        private const int EventSpawnMinPlayerDistance = 12;
        private const int MaxSpawnAttempts = 60;

        private readonly NPCService _npcService;
        private readonly INPCSpawnerService _spawner;
        private readonly IWorldService _worldService;
        private readonly ITileService _tiles;
        private readonly ITimeService _timeService;
        private readonly IPlayerService _playerService;
        private int _nextEventSpawnSeq = 1;

        /// <summary>Целевой размер населения (стартовый состав текущей локации).</summary>
        private int _targetPopulation;

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

        /// <summary>Число ивент-спаунов с момента старта (QA: только ивенты).</summary>
        public int EventSpawnCount { get; private set; }

        // === Стартовый состав ===

        /// <summary>
        /// Сгенерировать стартовый состав населения локации.
        /// Детерминированно: один seed → один состав.
        /// Вызывается при (пере)сборке локации — момент, когда игрока в ней
        /// НЕ было (естественное восстановление населения, R14).
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

        // === Ивент-спаун (R14) ===

        /// <summary>
        /// Ивент-спаун NPC при живом игроке в локации — ЕДИНСТВЕННЫЙ
        /// внутрисессионный источник новых NPC (R14): приход каравана,
        /// нападение на локацию, другое событие. Вызывается будущим
        /// event-pipeline / GROUP_SYSTEM (караваны, рейды), НЕ тиками.
        /// Возвращает ID нового NPC или null.
        /// </summary>
        public string? TrySpawnEventNpc(NPCEventSpawnReason reason)
        {
            var location = _worldService?.CurrentLocation;
            if (location == null)
            {
                Console.WriteLine("[SpawnComposition] EventSpawn: нет активной локации — отказ");
                return null;
            }

            float now = _timeService?.TotalTime ?? 0f;
            var rng = new SeededRandom((location.Seed + EventSpawnSeedOffset)
                + (long)_nextEventSpawnSeq * 7919 + (long)(now * 100f));

            var request = EventRequestFor(reason, rng, location);

            // Позиция: walkable, на отшибе (ивент «приходит извне»).
            var pos = FindEventSpawnPosition(rng, location);
            if (pos is null)
            {
                Console.WriteLine($"[SpawnComposition] EventSpawn({reason}): позиция не найдена — отложено");
                return null;
            }

            long seed = location.Seed + EventSpawnSeedOffset + (long)_nextEventSpawnSeq * 104729;
            _nextEventSpawnSeq++;

            string npcId = _spawner.SpawnNPC(request.SpeciesId, request.Role, request.Level, pos.Value, seed);
            if (!string.IsNullOrEmpty(npcId))
            {
                EventSpawnCount++;
                Console.WriteLine($"[SpawnComposition] EventSpawn({reason}) #{EventSpawnCount}: " +
                          $"{request} at ({pos.Value.X},{pos.Value.Y})");
            }
            return npcId;
        }

        /// <summary>Состав ивент-гостя по причине (караван/набег/событие).</summary>
        private static SpawnRequest EventRequestFor(NPCEventSpawnReason reason, SeededRandom rng, LocationData location)
        {
            int danger = Math.Max(0, location.DangerLevel);
            return reason switch
            {
                // Караван (GROUP_SYSTEM §3.5): торговец + охрана сопровождения.
                NPCEventSpawnReason.Caravan => new SpawnRequest(
                    rng.Next(0, 4) == 0 ? NPCRole.Guard : NPCRole.Merchant,
                    RollLevel(rng, 1, danger)),
                // Набег на локацию: враждебная волна (бандит/зверь).
                NPCEventSpawnReason.Raid => new SpawnRequest(
                    NPCRole.Enemy,
                    RollLevel(rng, 1, danger),
                    rng.Next(0, 4) == 0 ? "wolf" : "human"),
                // Прочее событие: смешанный гость.
                _ => new SpawnRequest(rng.Next(0, 4) switch
                    {
                        0 => NPCRole.Passerby,
                        1 => NPCRole.Cultivator,
                        2 => NPCRole.Guard,
                        _ => NPCRole.Disciple,
                    },
                    RollLevel(rng, 1, danger)),
            };
        }

        /// <summary>Сброс для повторной сборки сцены (ReAssembly).</summary>
        public void Reset()
        {
            _targetPopulation = 0;
            EventSpawnCount = 0;
            _nextEventSpawnSeq = 1;
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

        private Position2D? FindEventSpawnPosition(SeededRandom rng, LocationData loc)
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
                if (dist < EventSpawnMinPlayerDistance) continue;

                return new Position2D(x, y);
            }
            return null;
        }
    }
}
