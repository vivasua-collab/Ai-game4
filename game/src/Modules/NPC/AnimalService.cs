#nullable enable
// Создано: 2026-08-22 — Phase C (BODY-IMPL-PLAN): простые животные на тестовом полигоне.
// AnimalService — manages simple wandering animals with assembled bodies.
// Без AI-комбата, без pathfinding — только случайное блуждание в радиусе 5 тайлов.
// Body parts регистрируются в IBodyDataProvider per-entity (как у NPC).
// Источник: checkpoints/08_22_body_impl_plan.md Phase C
//
// 2026-09-11 (аудит боя с животными D1–D7): IAnimalService — боевой профиль
// для чужих модулей (таргетинг Space, трупы, статы вида, имена UI);
// OnDamageApplied — детект смерти (NPCDeathEvent → CorpseService §5
// DEATH_AND_LOOT: труп-контейнер с духовными камнями); месть волка —
// ЧЕЙЗ игрока + атака только вплотную + de-aggro за 5 тайлов (ANIMALS §5);
// кролик мирный — не мстит (ANIMALS §5.5).
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Modules.Body;

namespace CultivationGame.Modules.NPC
{
    /// <summary>
    /// Manages simple wandering animals (wolf, deer, rabbit) on the test polygon.
    ///
    /// Responsibilities:
    ///  * Spawn animals with assembled Quadruped body (via IBodyFactory + IBodyDataProvider).
    ///  * Per-tick wander: pick a random walkable target within 5 tiles, step
    ///    towards it. Cooldown and speed are species-dependent.
    ///
    /// NOT a full NPC system — no soul, no AI combat, no inventory. Animals
    /// exist purely as «mobile furniture» that can be damaged/healed via
    /// IBodyDataProvider.
    ///
    /// Lifecycle:
    ///   * IStartable.Start() — fallback spawn on TestPolygon for direct
    ///     scene load (e.g. `godot scenes/GameWorld.tscn` without going
    ///     through MainMenu). Mirrors the TileModule/PlayerModule pattern.
    ///   * AnimalSpawnPhase — proper location-aware spawn during scene
    ///     assembly (called from GameSession.NewGame via RunAssembly).
    ///     Clears any existing animals first.
    ///   * ITickable.Tick() — drives wandering at 1/5/15 Hz depending on TimeSpeed.
    /// </summary>
    public sealed class AnimalService : IStartable, ITickable, IAnimalService
    {
        // === DI dependencies ===
        private readonly IBodyDataProvider _bodyDataProvider;
        private readonly IBodyFactory _bodyFactory;
        private readonly ITileService _tileService;
        private readonly SpeciesRegistry _speciesRegistry;
        private readonly ISubscriber<DamageAppliedEvent> _damageAppliedSub;
        private readonly IPublisher<AttackIntentEvent> _attackIntentPub;
        // 2026-09-11 (D2/D4): смерть животного → NPCDeathEvent (труп/killfeed);
        // позиция и канонический ID игрока для честной мести (чейз/смежность);
        // de-aggro → CombatDisengageEvent (симметрия с leash NPC R16 — бой
        // не должен «висеть» вечно, когда зверь отстал).
        private readonly IPublisher<NPCDeathEvent> _npcDeathPub;
        private readonly IPublisher<CombatDisengageEvent> _combatDisengagePub;
        private readonly IPlayerService _playerService;

        // === State ===
        private readonly List<AnimalEntity> _animals = new();
        private IDisposable? _damageAppliedToken;

        // Track which animals are hostile (attacked by player → retaliate).
        private readonly HashSet<string> _hostileAnimals = new();

        // Deterministic RNG for wandering (separate from spawn RNG which is created per SpawnForLocation call).
        private readonly SeededRandom _wanderRng = new SeededRandom(seed: 0xC0FFEE);

        // Monotonic counter for entity IDs.
        private int _nextId = 1;

        // === Constants (species tuning) ===
        private const int WanderRadius = 5;
        private const int MaxTargetPickAttempts = 6;

        // Prime offset so animal spawns use an independent RNG stream from
        // tile/object generation (both derive from loc.Seed).
        private const int AnimalSeedOffset = 7919;

        // Minimum Chebyshev distance from player spawn (map centre) so animals
        // don't pop in on top of the player.
        private const int MinDistanceFromPlayer = 5;

        // Cap attempts to avoid infinite loop on tiny maps with no walkable tiles.
        private const int MaxSpawnAttempts = 200;

        /// <summary>
        /// Радиус агро мести: игрок дальше — животное отстаёт и возвращается
        /// к блужданию (ANIMALS.md §5.4 «прекращение retaliation»).
        /// </summary>
        private const int RetaliationAgroRadius = 5;

        /// <summary>
        /// Дистанция укуса (Чебышёв, тайлы) — мстящее животное атакует ТОЛЬКО
        /// вплотную (ANIMALS.md §5.2: «пока цель в зоне досягаемости»).
        /// До R16-аудита интент публиковался с ЛЮБОЙ дистанции.
        /// </summary>
        private const int AnimalAttackRangeTiles = 2;

        /// <summary>
        /// Construct AnimalService. Resolved by DI; constructor injection picks
        /// up IBodyDataProvider, IBodyFactory, ITileService, SpeciesRegistry.
        /// </summary>
        public AnimalService(
            IBodyDataProvider bodyDataProvider,
            IBodyFactory bodyFactory,
            ITileService tileService,
            SpeciesRegistry speciesRegistry,
            ISubscriber<DamageAppliedEvent> damageAppliedSub,
            IPublisher<AttackIntentEvent> attackIntentPub,
            IPublisher<NPCDeathEvent> npcDeathPub,
            IPublisher<CombatDisengageEvent> combatDisengagePub,
            IPlayerService playerService)
        {
            _bodyDataProvider = bodyDataProvider ?? throw new ArgumentNullException(nameof(bodyDataProvider));
            _bodyFactory = bodyFactory ?? throw new ArgumentNullException(nameof(bodyFactory));
            _tileService = tileService ?? throw new ArgumentNullException(nameof(tileService));
            _speciesRegistry = speciesRegistry ?? throw new ArgumentNullException(nameof(speciesRegistry));
            _damageAppliedSub = damageAppliedSub ?? throw new ArgumentNullException(nameof(damageAppliedSub));
            _attackIntentPub = attackIntentPub ?? throw new ArgumentNullException(nameof(attackIntentPub));
            _npcDeathPub = npcDeathPub ?? throw new ArgumentNullException(nameof(npcDeathPub));
            _combatDisengagePub = combatDisengagePub ?? throw new ArgumentNullException(nameof(combatDisengagePub));
            _playerService = playerService ?? throw new ArgumentNullException(nameof(playerService));
        }

        // === IStartable ===

        /// <summary>
        /// Fallback spawn on TestPolygon — invoked by GameEntryPoint when the
        /// scene loads directly without going through MainMenu (e.g. CLI headless).
        /// Same pattern as TileModule.Start()/PlayerModule.Start(). If
        /// AnimalSpawnPhase later runs (via NewGame), it clears and re-spawns
        /// with the proper location.
        ///
        /// Reads map dimensions from <see cref="ITileService"/> (already
        /// generated by TileModule.Start()) and honours GODOT_MAP_SIZE env
        /// var for the large-world perf-test path.
        /// </summary>
        public void Start()
        {
            if (_animals.Count > 0) return;

            int width = _tileService.MapWidth > 0 ? _tileService.MapWidth : 50;
            int height = _tileService.MapHeight > 0 ? _tileService.MapHeight : 50;
            int seed = 12345;  // TestPolygon default
            string locId = "test_polygon";

            // Mirror TileModule.Start()'s GODOT_MAP_SIZE override for the
            // large-world perf-test path so animal spawn coords stay in-bounds.
            var envSize = System.Environment.GetEnvironmentVariable("GODOT_MAP_SIZE");
            if (!string.IsNullOrEmpty(envSize) && int.TryParse(envSize, out _))
            {
                seed = 67890;
                locId = "large_world";
            }

            SpawnForLocation(width, height, seed, locId);

            // Subscribe to damage events for animal retaliation.
            _damageAppliedToken = _damageAppliedSub.Subscribe(OnDamageApplied);
        }

        // === Spawn API ===

        /// <summary>
        /// Spawn a new animal of the given species at the given tile position.
        /// Assembles a Quadruped body via BodyFactory and registers it in
        /// IBodyDataProvider under a unique entity ID.
        /// </summary>
        /// <returns>The created AnimalEntity.</returns>
        public AnimalEntity SpawnAnimal(string species, Position2D position)
        {
            if (string.IsNullOrEmpty(species))
                throw new ArgumentException("Species must be non-empty.", nameof(species));

            var data = _speciesRegistry.GetSpecies(species)
                ?? throw new ArgumentException($"Unknown species '{species}'.", nameof(species));

            var entityId = $"animal_{species}_{_nextId++}";

            var animal = new AnimalEntity(
                entityId,
                species,
                position,
                data.Morphology,
                data.Material,
                data.Size);

            // Species-specific wander tuning.
            // Rabbit: fast (2 tiles/tick), short cooldown.
            // Wolf:   normal speed, medium cooldown.
            // Deer:   normal speed, medium-long cooldown.
            switch (species)
            {
                case "rabbit":
                    animal.MoveSpeedTilesPerTick = 2;
                    break;
                case "wolf":
                case "deer":
                default:
                    animal.MoveSpeedTilesPerTick = 1;
                    break;
            }

            // Initial cooldown: 1-3 ticks (so they don't all move on tick 1).
            animal.MoveCooldownTicks = _wanderRng.Next(1, 4);

            // Assemble body parts via BodyFactory (data-driven from BodyTemplateProvider).
            // Vitality comes from SpeciesData.BaseVitality (wolf=10, deer=8, rabbit=4).
            var parts = _bodyFactory.CreateBody(data.Morphology, data.Size, data.BaseVitality);
            _bodyDataProvider.SetBodyParts(entityId, parts);

            _animals.Add(animal);
            return animal;
        }

        /// <summary>
        /// Clear all animals (used by AnimalSpawnPhase before re-spawning for
        /// a new location). Also removes their BodyParts from IBodyDataProvider.
        /// 2026-09-11 (аудит): заодно чистим hostile-реестр — месть животных
        /// прошлого мира не должна переживать пересборку.
        /// </summary>
        public void ClearAnimals()
        {
            foreach (var a in _animals)
                _bodyDataProvider.RemoveEntity(a.EntityId);
            _animals.Clear();
            _hostileAnimals.Clear();
        }

        /// <summary>
        /// Spawn 3-5 animals at random walkable positions for the given location.
        /// Uses a SeededRandom derived from <paramref name="seed"/> + AnimalSeedOffset
        /// so the same location always produces the same set of animals. Species
        /// are picked uniformly from {wolf, deer, rabbit}. Avoids the map centre
        /// (player spawn) by MinDistanceFromPlayer.
        /// </summary>
        public int SpawnForLocation(int width, int height, int seed, string locationId)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentException($"SpawnForLocation: bad dims {width}×{height}");

            var rng = new SeededRandom(seed + AnimalSeedOffset);

            int targetCount = rng.Next(3, 6);  // 3-5 animals
            string[] speciesPool = { "wolf", "deer", "rabbit" };

            int playerX = width / 2;
            int playerY = height / 2;

            int spawned = 0;
            int attempts = 0;
            while (spawned < targetCount && attempts < MaxSpawnAttempts)
            {
                attempts++;

                // Random position within the map, leaving a 1-tile margin.
                int x = rng.Next(1, width - 1);
                int y = rng.Next(1, height - 1);

                if (!_tileService.IsWalkable(x, y))
                    continue;

                // Stay clear of the player spawn (map centre).
                int distToPlayer = Math.Max(Math.Abs(x - playerX), Math.Abs(y - playerY));
                if (distToPlayer < MinDistanceFromPlayer)
                    continue;

                var species = speciesPool[rng.Next(0, speciesPool.Length)];
                var pos = new Position2D(x, y);

                try
                {
                    var animal = SpawnAnimal(species, pos);
                    spawned++;
                    Console.WriteLine(
                        $"[AnimalSpawn] Spawned {species} #{animal.EntityId} at {pos} (body: {animal.Morphology}/{animal.Size})");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AnimalSpawn] Failed to spawn {species} at {pos}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            Console.WriteLine(
                $"[AnimalSpawn] {locationId}: spawned {spawned}/{targetCount} animals (attempts={attempts})");
            return spawned;
        }

        // === Query ===

        /// <summary>Get all animals (snapshot for renderer). Read-only view.</summary>
        public IReadOnlyList<AnimalEntity> GetAllAnimals() => _animals;

        // === IAnimalService (2026-09-11, аудит D1–D7) ===

        /// <summary>
        /// Живые животные в радиусе (Чебышёв, тайлы) — кандидаты таргетинга
        /// Space-атак игрока. Мёртвые исключаются (труп — отдельная сущность).
        /// </summary>
        public IReadOnlyList<AnimalInfo> GetAliveAnimalsInRange(Position2D center, float rangeTiles)
        {
            int range = (int)Math.Ceiling(rangeTiles);
            var result = new List<AnimalInfo>(_animals.Count);
            foreach (var a in _animals)
            {
                if (!a.IsAlive) continue;
                int dist = Math.Max(Math.Abs(a.Position.X - center.X), Math.Abs(a.Position.Y - center.Y));
                if (dist > range) continue;
                result.Add(ToAnimalInfo(a));
            }
            return result;
        }

        /// <summary>
        /// Профиль животного по ID (null — не животное/не найдено). Статы вида
        /// из SpeciesRegistry (волк STR 8/AGI 14) — для боевого пайплайна.
        /// </summary>
        public AnimalInfo? TryGetAnimal(string entityId)
        {
            if (string.IsNullOrEmpty(entityId)) return null;
            var a = _animals.Find(x => x.EntityId == entityId);
            return a == null ? null : ToAnimalInfo(a);
        }

        /// <summary>
        /// Отображаемое имя животного для killfeed/трупа/цифр урона.
        /// Не-животное → null (вызывающий остаётся на своём резолве).
        /// </summary>
        public string? GetDisplayName(string entityId)
        {
            var a = _animals.Find(x => x.EntityId == entityId);
            return a == null ? null : SpeciesDisplayName(a.Species);
        }

        /// <summary>QA-геттер (AnimalCombatSimDebug): животное сейчас мстит?</summary>
        public bool IsHostile(string entityId) => _hostileAnimals.Contains(entityId);

        /// <summary>Снимок боевого профиля из сущности + статов вида.</summary>
        private AnimalInfo ToAnimalInfo(AnimalEntity a)
        {
            var data = _speciesRegistry.GetSpecies(a.Species);
            return new AnimalInfo(
                a.EntityId,
                a.Species,
                a.Position,
                a.Morphology,
                a.Material,
                a.IsAlive,
                data != null ? (int)data.BaseStrength : 8,
                data != null ? (int)data.BaseAgility : 10,
                data != null ? (int)data.BaseVitality : 10,
                data != null ? (int)data.BaseIntelligence : 2);
        }

        /// <summary>Вид → имя для UI («wolf» → «Волк»).</summary>
        private static string SpeciesDisplayName(string species) => species switch
        {
            "wolf"   => "Волк",
            "deer"   => "Олень",
            "rabbit" => "Кролик",
            _        => species,
        };

        // === ITickable ===

        /// <summary>
        /// Per-tick wander update. Called by GameEntryPoint at 1/5/15 Hz
        /// depending on TimeSpeed.
        ///
        /// Per animal:
        ///   1. If dead → skip.
        ///   2. Decrement MoveCooldownTicks. If still >0, skip.
        ///   3. If no Target (or target reached) → pick a new walkable tile
        ///      within WanderRadius. Reset cooldown (species-dependent).
        ///   4. Step MoveSpeedTilesPerTick tiles towards Target via
        ///      greedy single-tile moves (diagonal first, then orthogonal).
        /// </summary>
        /// <summary>Cleanup: unsubscribe from events.</summary>
        public void Dispose()
        {
            _damageAppliedToken?.Dispose();
            _damageAppliedToken = null;
        }

        public void Tick(int tickCount)
        {
            // Periodic debug log so headless tests can confirm Tick is wired.
            // First 3 ticks + every 30 after that.
            if (_animals.Count > 0 && (tickCount <= 3 || (tickCount > 0 && tickCount % 30 == 0)))
            {
                var first = _animals[0];
                Console.WriteLine(
                    $"[AnimalService] Tick #{tickCount} — {_animals.Count} animals; sample: {first.Species}#{first.EntityId} @ {first.Position}");
            }

            for (int i = 0; i < _animals.Count; i++)
            {
                var a = _animals[i];
                if (!a.IsAlive) continue;

                // === Combat: hostile animals chase & bite the player ===
                // 2026-09-11 (аудит D4): честная месть по ANIMALS.md §5.2/5.4 —
                // ЧЕЙЗ игрока (раньше стоял на месте и «кусал» через всю карту:
                // интент уходил с любой дистанции, никто его не гейтил);
                // атака — только вплотную (Чебышёв ≤ 2); игрок дальше 5 тайлов —
                // de-aggro, возврат к блужданию.
                if (_hostileAnimals.Contains(a.EntityId))
                {
                    var playerPos = _playerService.Position;
                    int dist = Math.Max(
                        Math.Abs(a.Position.X - playerPos.X),
                        Math.Abs(a.Position.Y - playerPos.Y));

                    if (dist > RetaliationAgroRadius)
                    {
                        // §5.4: цель ушла — месть прекращается. Бой (если был)
                        // завершаем честно — симметрия с leash NPC (R16 D3):
                        // CombatDisengageEvent → CombatModule.OnCombatDisengage →
                        // AbandonCombat — иначе 1v1-бой «висит» вечно (игрок
                        // остаётся в боевой стойке/гейтах).
                        _hostileAnimals.Remove(a.EntityId);
                        a.Target = null;
                        a.MoveCooldownTicks = 5; // ~5 тиков «успокаивается»
                        _combatDisengagePub.Publish(new CombatDisengageEvent(
                            a.EntityId, $"animal-deaggro: игрок дальше {RetaliationAgroRadius} тайлов"));
                        Console.WriteLine($"[AnimalService] {a.Species}#{a.EntityId}: игрок ушёл ({dist} тайлов, " +
                            $"wolf=({a.Position.X},{a.Position.Y}) player=({playerPos.X},{playerPos.Y})) → de-aggro, wander");
                        continue;
                    }

                    if (a.CombatCooldownTicks > 0)
                    {
                        a.CombatCooldownTicks--;
                        // Кулдаун укуса ≠ кулдаун бега: во время ожидания
                        // продолжаем ЧЕЙЗ (шаг к игроку), не стоим.
                        a.Target = new Position2D(playerPos.X, playerPos.Y);
                        StepTowardsTarget(a);
                        continue;
                    }

                    if (dist <= AnimalAttackRangeTiles)
                    {
                        // Канонический ID игрока (раньше — литерал "player",
                        // алиас работал через PlayerIdResolver, но источник
                        // должен быть честным).
                        _attackIntentPub.Publish(new AttackIntentEvent(
                            a.EntityId, _playerService.PlayerId, string.Empty, false));
                        // Аудит: 2 тика (НЕ 3 как в ANIMALS §5.3): окно чужого хода
                        // CombatService — 2.5с (EnemyTurnTimeout), 3-й тик ВСЕГДА
                        // опаздывает в чужой ход → укус отклонялся бы вечно.
                        // 2 тика ≈ 2с < 2.5с — месть работает; темп медленнее
                        // NPC-людей (1.6с) — дух спеки сохранён.
                        a.CombatCooldownTicks = 2;
                    }
                    else
                    {
                        // Не дотянулся — сближаемся.
                        a.Target = new Position2D(playerPos.X, playerPos.Y);
                        StepTowardsTarget(a);
                    }
                    continue; // Не блуждаем, пока мстим
                }

                // === Normal wandering ===
                // Cooldown gate — wait before next action.
                if (a.MoveCooldownTicks > 0)
                {
                    a.MoveCooldownTicks--;
                    continue;
                }

                // Need a new target?
                if (a.Target is null || a.Position == a.Target.Value)
                {
                    if (!TryPickNewTarget(a))
                    {
                        // No walkable tile found — wait one tick and retry.
                        a.MoveCooldownTicks = 1;
                        continue;
                    }
                }

                // Step towards target.
                StepTowardsTarget(a);

                // If reached target, schedule next wander.
                if (a.Target is null || a.Position == a.Target.Value)
                {
                    a.Target = null;
                    a.MoveCooldownTicks = GetWanderCooldownTicks(a.Species);
                }
            }
        }

        /// <summary>
        /// Handle damage applied to an animal — mark as hostile, track attacker.
        /// 2026-09-11 (аудит D2): + детект смерти — BodyService уже применил урон
        /// к частям тела (подписка раньше нашей: Body-модуль стартует до NPC),
        /// HP ≤ 0 → IsAlive=false + NPCDeathEvent → CorpseService (труп-контейнер,
        /// DEATH_AND_LOOT §5) + killfeed. Раньше смерть животного не детектилась
        /// НИКЕМ: волк «жил» на 0 HP, трупа и записи в журнале не было.
        /// Кролик мирный (ANIMALS §5.5): месть не включает — просто убегает.
        /// </summary>
        private void OnDamageApplied(in DamageAppliedEvent e)
        {
            // Copy fields for use in lambda (in parameter cannot be captured).
            string targetId = e.TargetId;
            string sourceId = e.SourceId;

            // Check if target is one of our animals.
            var animal = _animals.Find(a => a.EntityId == targetId);
            if (animal == null || !animal.IsAlive) return;

            // Смерть: 2026-09-11 (аудит боя, «сущности неубиваемы») — ЕДИНОЕ
            // правило тел (BODY_SYSTEM, как у NPCCombatAdapter): жизненно
            // важная часть уничтожена (IsEntityAlive) ИЛИ полный дренаж HP.
            // HP пересчитывается из BodyParts (единая система тел).
            if (_bodyDataProvider.HasEntity(animal.EntityId)
                && (!_bodyDataProvider.IsEntityAlive(animal.EntityId)
                    || _bodyDataProvider.GetCurrentHealth(animal.EntityId) <= 0))
            {
                animal.IsAlive = false;
                _hostileAnimals.Remove(animal.EntityId);
                animal.Target = null;
                _npcDeathPub.Publish(new NPCDeathEvent(animal.EntityId, sourceId));
                Console.WriteLine($"[AnimalService] {animal.Species}#{animal.EntityId} погиб от {sourceId} → NPCDeathEvent (труп через CorpseService)");
                return;
            }

            // Кролик не мстит (мирный вид) — урон не делает его hostile.
            if (animal.Species == "rabbit")
            {
                Console.WriteLine($"[AnimalService] {animal.Species}#{animal.EntityId} hit by {sourceId} → мирный вид, бежит");
                return;
            }

            // Mark as hostile (retaliate).
            _hostileAnimals.Add(animal.EntityId);

            // Track attacker ID (for AttackIntentEvent target).
            animal.LastAttackerId = sourceId;
            animal.CombatCooldownTicks = 0; // attack next tick

            Console.WriteLine($"[AnimalService] {animal.Species}#{animal.EntityId} hit by {e.SourceId} → hostile!");
        }

        // === Helpers ===

        /// <summary>
        /// Pick a random walkable tile within ±WanderRadius of the animal's
        /// current position. Tries MaxTargetPickAttempts random offsets;
        /// returns false if none of them are walkable.
        /// </summary>
        private bool TryPickNewTarget(AnimalEntity a)
        {
            for (int attempt = 0; attempt < MaxTargetPickAttempts; attempt++)
            {
                int dx = _wanderRng.Next(-WanderRadius, WanderRadius + 1);
                int dy = _wanderRng.Next(-WanderRadius, WanderRadius + 1);

                // Skip zero-offset (would set Target == Position immediately).
                if (dx == 0 && dy == 0) continue;

                int tx = a.Position.X + dx;
                int ty = a.Position.Y + dy;

                if (_tileService.IsWalkable(tx, ty))
                {
                    a.Target = new Position2D(tx, ty);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Move the animal up to MoveSpeedTilesPerTick tiles towards Target.
        /// Greedy: each sub-step tries diagonal first, then horizontal,
        /// then vertical. If all three are blocked, aborts the target.
        /// </summary>
        private void StepTowardsTarget(AnimalEntity a)
        {
            if (a.Target is null) return;
            var target = a.Target.Value;

            int stepsRemaining = a.MoveSpeedTilesPerTick;
            while (stepsRemaining > 0 && a.Position != target)
            {
                int dx = Math.Sign(target.X - a.Position.X);
                int dy = Math.Sign(target.Y - a.Position.Y);

                int newX = a.Position.X + dx;
                int newY = a.Position.Y + dy;

                // Try diagonal.
                if (dx != 0 && dy != 0 && _tileService.IsWalkable(newX, newY))
                {
                    a.Position = new Position2D(newX, newY);
                }
                // Try horizontal.
                else if (dx != 0 && _tileService.IsWalkable(a.Position.X + dx, a.Position.Y))
                {
                    a.Position = new Position2D(a.Position.X + dx, a.Position.Y);
                }
                // Try vertical.
                else if (dy != 0 && _tileService.IsWalkable(a.Position.X, a.Position.Y + dy))
                {
                    a.Position = new Position2D(a.Position.X, a.Position.Y + dy);
                }
                else
                {
                    // Blocked — give up on this target.
                    a.Target = null;
                    a.MoveCooldownTicks = GetWanderCooldownTicks(a.Species);
                    return;
                }

                stepsRemaining--;
            }
        }

        /// <summary>
        /// Species-dependent cooldown in ticks between wander decisions.
        /// Rabbit (small/fast): 2-3 ticks. Wolf (predator): 3-5. Deer: 3-6.
        /// </summary>
        private int GetWanderCooldownTicks(string species)
        {
            return species switch
            {
                "rabbit" => _wanderRng.Next(2, 4),
                "wolf"   => _wanderRng.Next(3, 6),
                "deer"   => _wanderRng.Next(3, 7),
                _        => _wanderRng.Next(3, 6),
            };
        }
    }
}
