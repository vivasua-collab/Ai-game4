#nullable enable
// Создано: 2026-05-18 12:00:00 UTC — Body доработка: реестр видов
// Редактировано: 2026-05-18 12:00:00 UTC — P1-03 FIX: кэширование GetAllSpecies
// Редактировано: 2026-05-18 13:10:29 UTC — P1-07 FIX: кэширование GetSpeciesBySoulType
// Редактировано: 2026-05-18 — V3 FIX: P1-06 IStartable вместо RegisterBuildCallback
// Migrated from Ai-game3 (Unity) to Ai-game4 (Godot). Removed VContainer.Unity.IStartable —
// caller must invoke Initialize() explicitly (or rely on lazy init in GetSpecies).
// Level 3 иерархии: SoulType → Morphology → Species.
// Источник: ALGORITHMS.md П.25, ENTITY_TYPES.md §4
using System;
using System.Collections.Generic;
using System.Linq;
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.Body
{
    /// <summary>
    /// Реестр всех видов в игре.
    /// Загружает SpeciesData из кода (позже — из JSON-конфигов).
    /// Источник: ALGORITHMS.md П.25
    /// MIGRATION: IStartable removed (was VContainer lifecycle hook). Caller must
    /// invoke Initialize() once before first GetSpecies() — or rely on the
    /// lazy self-initialization in GetSpecies().
    /// </summary>
    public sealed class SpeciesRegistry
    {
        private readonly Dictionary<string, SpeciesData> _species = new();
        private bool _initialized;

        // P1-03 FIX: кэшированный список для GetAllSpecies
        private List<SpeciesData>? _allSpeciesCache;
        private bool _cacheDirty = true;

        // P1-07 FIX: кэшированный словарь для GetSpeciesBySoulType
        private readonly Dictionary<SoulType, List<SpeciesData>> _soulTypeCache = new();

        /// <summary>Инициализировать реестр. Idempotent.</summary>
        public void Initialize()
        {
            if (_initialized) return;

            // === Characters ===
            Register(new SpeciesData("human", SoulType.Character, Morphology.Humanoid, BodyMaterial.Organic, SizeClass.Medium,
                baseStrength: 10, baseAgility: 10, baseVitality: 10, baseIntelligence: 10,
                baseAgeRange: (16, 30), lifespanRange: (70, 100)));

            Register(new SpeciesData("elf", SoulType.Character, Morphology.Humanoid, BodyMaterial.Organic, SizeClass.Medium,
                baseStrength: 8, baseAgility: 12, baseVitality: 8, baseIntelligence: 12,
                baseAgeRange: (16, 30), lifespanRange: (200, 500)));

            Register(new SpeciesData("demon", SoulType.Character, Morphology.Humanoid, BodyMaterial.Organic, SizeClass.Medium,
                baseStrength: 14, baseAgility: 10, baseVitality: 12, baseIntelligence: 8,
                baseAgeRange: (16, 30), lifespanRange: (150, 300)));

            Register(new SpeciesData("giant", SoulType.Character, Morphology.Humanoid, BodyMaterial.Organic, SizeClass.Huge,
                baseStrength: 18, baseAgility: 6, baseVitality: 16, baseIntelligence: 4,
                baseAgeRange: (20, 50), lifespanRange: (100, 200)));

            // === Creatures ===
            Register(new SpeciesData("wolf", SoulType.Creature, Morphology.Quadruped, BodyMaterial.Organic, SizeClass.Medium,
                baseStrength: 8, baseAgility: 14, baseVitality: 10, baseIntelligence: 4,
                baseAgeRange: (1, 3), lifespanRange: (10, 15)));

            // Phase C (BODY-IMPL-PLAN): simple wandering animals on the test polygon.
            // Deer and rabbit added so AnimalSpawnPhase has the full {wolf, deer, rabbit}
            // pool to pick from. Stats per checkpoints/08_22_body_impl_plan.md.
            Register(new SpeciesData("deer", SoulType.Creature, Morphology.Quadruped, BodyMaterial.Organic, SizeClass.Medium,
                baseStrength: 6, baseAgility: 12, baseVitality: 8, baseIntelligence: 2,
                baseAgeRange: (1, 4), lifespanRange: (10, 20)));

            Register(new SpeciesData("rabbit", SoulType.Creature, Morphology.Quadruped, BodyMaterial.Organic, SizeClass.Small,
                baseStrength: 3, baseAgility: 14, baseVitality: 4, baseIntelligence: 1,
                baseAgeRange: (0, 2), lifespanRange: (3, 7)));

            Register(new SpeciesData("tiger", SoulType.Creature, Morphology.Quadruped, BodyMaterial.Organic, SizeClass.Large,
                baseStrength: 14, baseAgility: 12, baseVitality: 12, baseIntelligence: 4,
                baseAgeRange: (2, 5), lifespanRange: (15, 25)));

            Register(new SpeciesData("dragon", SoulType.Creature, Morphology.Quadruped, BodyMaterial.Scaled, SizeClass.Huge,
                baseStrength: 20, baseAgility: 10, baseVitality: 18, baseIntelligence: 10,
                baseAgeRange: (50, 100), lifespanRange: (1000, 5000)));

            Register(new SpeciesData("phoenix", SoulType.Creature, Morphology.Bird, BodyMaterial.Ethereal, SizeClass.Large,
                baseStrength: 8, baseAgility: 16, baseVitality: 8, baseIntelligence: 12,
                baseAgeRange: (10, 30), lifespanRange: (500, 999)));

            Register(new SpeciesData("spider", SoulType.Creature, Morphology.Arthropod, BodyMaterial.Chitin, SizeClass.Tiny,
                baseStrength: 4, baseAgility: 12, baseVitality: 4, baseIntelligence: 2,
                baseAgeRange: (0, 1), lifespanRange: (1, 3)));

            // === Spirits ===
            Register(new SpeciesData("ghost", SoulType.Spirit, Morphology.Amorphous, BodyMaterial.Ethereal, SizeClass.Medium,
                baseStrength: 0, baseAgility: 0, baseVitality: 0, baseIntelligence: 12,
                baseAgeRange: (0, 0), lifespanRange: (0, 0)));

            // === Constructs ===
            Register(new SpeciesData("golem", SoulType.Construct, Morphology.Humanoid, BodyMaterial.Mineral, SizeClass.Large,
                baseStrength: 16, baseAgility: 4, baseVitality: 20, baseIntelligence: 2,
                baseAgeRange: (0, 0), lifespanRange: (0, 0)));

            _initialized = true;
        }

        /// <summary>Зарегистрировать вид.</summary>
        public void Register(SpeciesData species)
        {
            if (species == null) throw new ArgumentNullException(nameof(species));
            _species[species.SpeciesId] = species;
            _cacheDirty = true;
            _soulTypeCache.Clear();  // P1-07 FIX: инвалидация кэша при регистрации
        }

        /// <summary>Получить данные вида по ID. Triggers lazy Initialize if needed.</summary>
        public SpeciesData? GetSpecies(string speciesId)
        {
            if (!_initialized)
                Initialize();
            if (_species.TryGetValue(speciesId, out var data))
                return data;
            return null;
        }

        /// <summary>Получить все зарегистрированные виды. P1-03 FIX: кэшируется.</summary>
        public IReadOnlyList<SpeciesData> GetAllSpecies()
        {
            if (!_initialized)
                Initialize();
            if (_cacheDirty || _allSpeciesCache == null)
            {
                _allSpeciesCache = new List<SpeciesData>(_species.Values);
                _cacheDirty = false;
            }
            return _allSpeciesCache.AsReadOnly();
        }

        /// <summary>Получить виды по типу души. P1-07 FIX: кэшируется.</summary>
        public IReadOnlyList<SpeciesData> GetSpeciesBySoulType(SoulType soulType)
        {
            if (!_initialized)
                Initialize();
            if (!_soulTypeCache.TryGetValue(soulType, out var cached))
            {
                cached = _species.Values.Where(s => s.SoulType == soulType).ToList();
                _soulTypeCache[soulType] = cached;
            }
            return cached.AsReadOnly();
        }

        /// <summary>Проверить, существует ли вид.</summary>
        public bool SpeciesExists(string speciesId) => _species.ContainsKey(speciesId);
    }
}
