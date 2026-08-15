#nullable enable
// Создано: 2026-05-20 18:00:11 UTC
// Редактировано: 2026-05-20 18:18 UTC — фикс: species.BodyMaterial → species.Material (SpeciesData)
// Редактировано: 2026-05-20 18:18 UTC — Фаза 2: шаги 5-7 (экипировка, техники, инвентарь, имя)
// Редактировано: 2026-05-20 18:43:21 UTC — Фаза 3: копирование статов из SoulData в NPCState (задача 3.3)
// Редактировано: 2026-05-20 19:11:00 UTC — Фаза 4, задача 4.6: AwakeningAge копирование
// Редактировано: 2026-05-22 04:14:49 UTC — Спринт 3 B6: InnateElement из SoulData
// Оркестратор полного пайплайна сборки NPC (Шаги 1-7 + CalculateTotals)
// Источник: docs/NPC_ASSEMBLY_PIPELINE.md §1-8
//
// ПРОТИВОРЕЧИЕ #1: Унифицированные модули — те же формулы Qi.
// ПРОТИВОРЕЧИЕ #3: NPCCombatAdapter НЕ списывает HP напрямую — только через BodyParts.
// ПРОТИВОРЕЧИЕ #5: CurrentQi = CoreCapacity.
// ПРОТИВОРЕЧИЕ #6: NPCState.BodyParts = List<BodyPart> (не Dictionary).
using System;
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Data;
using CultivationGame.Modules.Body;
using CultivationGame.Modules.Generator;
using CultivationGame.Modules.NPC.Data;

namespace CultivationGame.Modules.NPC
{
    /// <summary>
    /// Оркестратор полного пайплайна сборки NPC.
    /// Выполняет шаги 1-7 пайплайна:
    /// 1. SoulGenerator → SoulData
    /// 2. SpeciesRegistry → SpeciesData (фенотип)
    /// 3. BodyFactory → List<BodyPart> (тело)
    /// 4. Qi — уже в SoulData (расширенная формула)
    /// 5. Экипировка / Усиление тела
    /// 6. Техники (L1+)
    /// 7. Инвентарь
    /// 8. CalculateTotals → NPCState
    ///
    /// ПРОТИВОРЕЧИЕ #6: BodyParts = List<BodyPart>, единая система урона.
    /// ПРОТИВОРЕЧИЕ #1: Унифицированные модули — формулы из документации.
    /// </summary>
    public sealed class NPCAssemblyService
    {
        private readonly SoulGenerator _soulGenerator;
        private readonly SpeciesRegistry _speciesRegistry;
        private readonly IBodyFactory _bodyFactory;
        private readonly NPCConfig _config;
        private readonly ITechniqueGeneratorService _techniqueGenerator;
        private readonly IItemGeneratorService _itemGenerator;
        private readonly BodyEnhancementSystem _enhancementSystem;
        private readonly NPCNameGenerator _nameGenerator;

        public NPCAssemblyService(
            SoulGenerator soulGenerator,
            SpeciesRegistry speciesRegistry,
            IBodyFactory bodyFactory,
            NPCConfig config,
            ITechniqueGeneratorService techniqueGenerator,
            IItemGeneratorService itemGenerator,
            BodyEnhancementSystem enhancementSystem,
            NPCNameGenerator nameGenerator)
        {
            _soulGenerator = soulGenerator ?? throw new ArgumentNullException(nameof(soulGenerator));
            _speciesRegistry = speciesRegistry ?? throw new ArgumentNullException(nameof(speciesRegistry));
            _bodyFactory = bodyFactory ?? throw new ArgumentNullException(nameof(bodyFactory));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _techniqueGenerator = techniqueGenerator ?? throw new ArgumentNullException(nameof(techniqueGenerator));
            _itemGenerator = itemGenerator ?? throw new ArgumentNullException(nameof(itemGenerator));
            _enhancementSystem = enhancementSystem ?? throw new ArgumentNullException(nameof(enhancementSystem));
            _nameGenerator = nameGenerator ?? throw new ArgumentNullException(nameof(nameGenerator));
        }

        /// <summary>
        /// Собрать NPC через полный пайплайн (Шаги 1-7 + CalculateTotals).
        /// </summary>
        /// <param name="speciesId">Идентификатор вида ("human", "wolf", ...)</param>
        /// <param name="roleId">Роль NPC</param>
        /// <param name="locationLevel">Уровень локации (0-10)</param>
        /// <param name="position">Позиция в мире</param>
        /// <param name="seed">Seed для детерминированной генерации</param>
        /// <returns>Полностью собранный NPCState (Шаги 1-7)</returns>
        public NPCState Assemble(string speciesId, NPCRole roleId, int locationLevel, Position2D position, long seed)
        {
            var rng = new SeededRandom(seed);

            // === Шаг 1: Генерация души ===
            SoulData soul = _soulGenerator.Generate(speciesId, roleId, locationLevel, seed);

            // === Шаг 2: Выбор фенотипа ===
            SpeciesData species = _speciesRegistry.GetSpecies(speciesId);
            if (species == null)
                throw new ArgumentException($"Вид не найден: {speciesId}", nameof(speciesId));

            // === Шаг 3: Сборка тела ===
            float vitality = species.BaseVitality;
            List<BodyPart> bodyParts = _bodyFactory.CreateBody(species.Morphology, species.Size, vitality);

            // === Шаг 4: Qi — уже рассчитано в SoulData ===
            // (Расширенная формула с ConductivityGrowthMultiplier — ПРОТИВОРЕЧИЕ #4)

            // === Шаг 5: Экипировка / Усиление тела ===
            var equipmentIds = new Dictionary<EquipmentSlot, string>();
            List<BodyEnhancement> enhancements = new List<BodyEnhancement>();

            bool isHumanoidOrHybrid = species.Morphology == Morphology.Humanoid
                || species.Morphology == Morphology.HybridHarpy
                || species.Morphology == Morphology.HybridLamia;

            int cultLevel = (int)soul.CultivationLevel;

            if (isHumanoidOrHybrid)
            {
                // Гуманоиды: экипировка (оружие + броня + зарядник)
                EquipHumanoid(equipmentIds, cultLevel, rng);
            }
            else
            {
                // Негуманоиды: врождённые усиления тела
                enhancements = _enhancementSystem.GenerateEnhancements(species, cultLevel, rng);
                _enhancementSystem.ApplyEnhancements(bodyParts, enhancements);
            }

            // === Шаг 6: Техники (L1+) ===
            var techniqueIds = new List<string>();
            if (cultLevel >= 1)
            {
                GenerateTechniques(techniqueIds, cultLevel, roleId, rng);
            }

            // === Шаг 7: Инвентарь ===
            var inventorySlots = new List<InventorySlot>();
            FillInventory(inventorySlots, cultLevel, rng);

            // === Шаг 8: CalculateTotals → Сборка NPCState ===
            // Бонусы усилений для боевых параметров
            var enhBonuses = _enhancementSystem.GetEnhancementBonuses(enhancements, BodyPartType.All);

            var state = new NPCState
            {
                // Идентификация
                NpcId = GenerateNpcId(),
                PresetId = speciesId,
                DisplayName = _nameGenerator.Generate(species, roleId, soul.CultivationLevel, rng),
                SpeciesId = speciesId,

                // Классификация (из фенотипа)
                Role = roleId,
                Category = DetermineCategory(roleId),
                Personality = DeterminePersonality(roleId),
                SoulType = species.SoulType,
                Morphology = species.Morphology,
                BodyMaterial = species.Material,

                // Культивация (из души)
                CultivationLevel = soul.CultivationLevel,
                SubLevel = soul.SubLevel,
                CoreQuality = soul.CoreQuality,
                MaxQi = soul.CoreCapacity,
                CurrentQi = soul.CurrentQi, // ПРОТИВОРЕЧИЕ #5: = CoreCapacity
                Conductivity = soul.Conductivity,

                // Здоровье (из тела — ПРОТИВОРЕЧИЕ #6)
                BodyParts = bodyParts,
                MaxHealth = CalculateTotalHealth(bodyParts),
                CurrentHealth = CalculateTotalHealth(bodyParts),

                // Параметры души
                Age = soul.Age,
                AwakeningAge = soul.AwakeningAge, // Задача 4.6: для расчёта latePenalty
                AwakeningType = soul.AwakeningType,
                MortalStage = soul.MortalStage,
                QiDensity = soul.QiDensity,
                MaxLifespan = soul.MaxLifespan,

                // Экипировка (Шаг 5)
                EquipmentIds = equipmentIds,

                // Техники (Шаг 6)
                TechniqueIds = techniqueIds,

                // Инвентарь (Шаг 7)
                InventorySlots = inventorySlots,

                // AI-состояние
                AIState = DetermineDefaultAIState(roleId),
                TargetId = null,
                StateTimer = 0f,

                // Отношения
                AttitudeScore = 0,

                // Флаги
                IsAlive = true,
                IsInCombat = false,

                // Принадлежность
                SectId = null,
                CurrentLocation = null, // Устанавливается в NPCSpawnerService (задача 3.B)

                // Позиция
                Position = position,

                // Боевые параметры (CalculateTotals — с учётом экипировки, техник, усилений)
                BaseDamage = CalculateBaseDamage(species, soul, equipmentIds, techniqueIds, enhBonuses),
                BaseDefense = CalculateBaseDefense(species, bodyParts, equipmentIds, enhBonuses),
                AggressionLevel = CalculateAggressionLevel(roleId),

                // Угрозы
                Threats = new Dictionary<string, float>(),

                // Кэш
                CachedPlayerQi = 0,
                CachedPlayerLevel = 1,

                // Базовые статы (Фаза 3, задача 3.3: из SoulData.CalculateStats)
                Strength = soul.Strength,
                Agility = soul.Agility,
                Vitality = soul.Vitality,
                Intelligence = soul.Intelligence,

                // Врождённая стихия (Спринт 3 B6: из SoulData.InnateElement)
                InnateElement = soul.InnateElement
            };

            return state;
        }

        // ===================================================================
        // Шаг 5: Экипировка гуманоидов
        // ===================================================================

        /// <summary>
        /// Гуманоиды получают полный комплект экипировки с L1+.
        /// Качество предметов масштабируется по уровню.
        /// Слоты: WeaponMain, WeaponOff, Head, Torso, Belt, Legs, Feet.
        /// </summary>
        private void EquipHumanoid(Dictionary<EquipmentSlot, string> equipmentIds, int cultLevel, SeededRandom rng)
        {
            if (cultLevel < 1) return;

            // L1+: Основное оружие (WeaponMain)
            long weaponSeed = rng.Next(0, int.MaxValue);
            var weapon = _itemGenerator.GenerateWeaponForLevel(cultLevel, weaponSeed);
            if (weapon != null)
                equipmentIds[EquipmentSlot.WeaponMain] = weapon.ItemId;

            // L1+: Вторичное оружие / щит (WeaponOff)
            long offWeaponSeed = rng.Next(0, int.MaxValue);
            var offWeapon = _itemGenerator.GenerateWeaponForLevel(cultLevel, offWeaponSeed);
            if (offWeapon != null)
                equipmentIds[EquipmentSlot.WeaponOff] = offWeapon.ItemId;

            // L1+: Шлем (Head)
            long headSeed = rng.Next(0, int.MaxValue);
            var headArmor = _itemGenerator.GenerateArmorForLevel(cultLevel, headSeed);
            if (headArmor != null)
                equipmentIds[EquipmentSlot.Head] = headArmor.ItemId;

            // L1+: Нагрудник (Torso)
            long torsoSeed = rng.Next(0, int.MaxValue);
            var torsoArmor = _itemGenerator.GenerateArmorForLevel(cultLevel, torsoSeed);
            if (torsoArmor != null)
                equipmentIds[EquipmentSlot.Torso] = torsoArmor.ItemId;

            // L1+: Зарядник Ци (Belt) — генератор может вернуть null для низких уровней
            long chargerSeed = rng.Next(0, int.MaxValue);
            var charger = _itemGenerator.GenerateChargerForLevel(cultLevel, chargerSeed);
            if (charger != null)
                equipmentIds[EquipmentSlot.Belt] = charger.ItemId;

            // L1+: Поножи (Legs)
            long legsSeed = rng.Next(0, int.MaxValue);
            var legsArmor = _itemGenerator.GenerateArmorForLevel(cultLevel, legsSeed);
            if (legsArmor != null)
                equipmentIds[EquipmentSlot.Legs] = legsArmor.ItemId;

            // L1+: Сапоги (Feet)
            long feetSeed = rng.Next(0, int.MaxValue);
            var feetArmor = _itemGenerator.GenerateArmorForLevel(cultLevel, feetSeed);
            if (feetArmor != null)
                equipmentIds[EquipmentSlot.Feet] = feetArmor.ItemId;
        }

        // ===================================================================
        // Шаг 6: Генерация техник
        // ===================================================================

        /// <summary>
        /// Сгенерировать техники для NPC.
        /// Количество: L1-2=1, L3-4=2, L5-6=3, L7+=4.
        /// </summary>
        private void GenerateTechniques(List<string> techniqueIds, int cultLevel, NPCRole roleId, SeededRandom rng)
        {
            int count = cultLevel switch
            {
                >= 7 => 4,
                >= 5 => 3,
                >= 3 => 2,
                _ => 1
            };

            long baseSeed = rng.Next(0, int.MaxValue);
            var techniques = _techniqueGenerator.GenerateMultiple(cultLevel, roleId, count, baseSeed);

            foreach (var tech in techniques)
            {
                techniqueIds.Add(tech.TechniqueId);
            }
        }

        // ===================================================================
        // Шаг 7: Заполнение инвентаря
        // ===================================================================

        /// <summary>
        /// Заполнить инвентарь NPC:
        /// L1+: 1-2 лечебных пилюли
        /// L3+, 10%: 1 Ци-настойка
        /// Все: 0-2 куска материала
        /// L3+, 10%: осколок духовного камня
        /// L5+, 5%: фрагмент духовного камня
        /// </summary>
        private void FillInventory(List<InventorySlot> inventorySlots, int cultLevel, SeededRandom rng)
        {
            // L1+: 1-2 лечебных пилюли
            if (cultLevel >= 1)
            {
                long healSeed = rng.Next(0, int.MaxValue);
                var healPill = _itemGenerator.GenerateConsumableForLevel(cultLevel, healSeed);
                if (healPill != null)
                {
                    int count = rng.Next(1, 3); // 1-2 штуки
                    inventorySlots.Add(new InventorySlot(healPill.ItemId, count, healPill.Category, healPill.Rarity));
                }
            }

            // L3+, 10%: Ци-настойка
            if (cultLevel >= 3 && rng.NextBool(0.10f))
            {
                long qiSeed = rng.Next(0, int.MaxValue);
                var qiPotion = _itemGenerator.GenerateConsumableForLevel(cultLevel, qiSeed);
                if (qiPotion != null)
                {
                    inventorySlots.Add(new InventorySlot(qiPotion.ItemId, 1, qiPotion.Category, qiPotion.Rarity));
                }
            }

            // Все: 0-2 куска материала
            int materialCount = rng.Next(0, 3);
            if (materialCount > 0)
            {
                // Условный ID материала — в реальной системе был бы MaterialGenerator
                string materialId = cultLevel >= 5 ? "material_spirit_stone_shard" : "material_iron_scrap";
                var rarity = cultLevel >= 5 ? ItemRarity.Uncommon : ItemRarity.Common;
                inventorySlots.Add(new InventorySlot(materialId, materialCount, ItemCategory.Material, rarity));
            }

            // L3+, 10%: осколок духовного камня
            if (cultLevel >= 3 && rng.NextBool(0.10f))
            {
                inventorySlots.Add(new InventorySlot("spirit_stone_shard", 1, ItemCategory.Material, ItemRarity.Rare));
            }

            // L5+, 5%: фрагмент духовного камня
            if (cultLevel >= 5 && rng.NextBool(0.05f))
            {
                inventorySlots.Add(new InventorySlot("spirit_stone_fragment", 1, ItemCategory.Material, ItemRarity.Epic));
            }
        }

        // ===================================================================
        // Вспомогательные методы
        // ===================================================================

        /// <summary>
        /// Рассчитать общее здоровье как сумму MaxRedHP всех частей тела.
        /// ПРОТИВОРЕЧИЕ #6: Единая система через BodyParts.
        /// </summary>
        public static int CalculateTotalHealth(List<BodyPart> bodyParts)
        {
            int total = 0;
            foreach (var part in bodyParts)
                total += part.MaxRedHP;
            return total;
        }

        /// <summary>
        /// Генерация уникального NpcId.
        /// </summary>
        private string GenerateNpcId()
        {
            return $"npc_{Guid.NewGuid():N}".Substring(0, 20);
        }

        /// <summary>
        /// Определить категорию NPC по роли.
        /// </summary>
        private NPCCategory DetermineCategory(NPCRole role)
        {
            return role switch
            {
                NPCRole.Elder => NPCCategory.Unique,
                NPCRole.Guard => NPCCategory.Plot,
                _ => NPCCategory.Temp
            };
        }

        /// <summary>
        /// Черты характера по умолчанию для роли.
        /// </summary>
        private PersonalityTrait DeterminePersonality(NPCRole role)
        {
            return role switch
            {
                NPCRole.Guard => PersonalityTrait.Aggressive | PersonalityTrait.Loyal,
                NPCRole.Enemy => PersonalityTrait.Aggressive | PersonalityTrait.Vengeful,
                NPCRole.Monster => PersonalityTrait.Aggressive,
                NPCRole.Elder => PersonalityTrait.Cautious | PersonalityTrait.Curious,
                NPCRole.Merchant => PersonalityTrait.Cautious | PersonalityTrait.Ambitious,
                NPCRole.Cultivator => PersonalityTrait.Ambitious | PersonalityTrait.Loyal,
                NPCRole.Disciple => PersonalityTrait.Ambitious | PersonalityTrait.Loyal,
                _ => PersonalityTrait.None
            };
        }

        /// <summary>
        /// Начальное AI-состояние по роли.
        /// </summary>
        private NPCAIState DetermineDefaultAIState(NPCRole role)
        {
            return role switch
            {
                NPCRole.Monster => NPCAIState.Wandering,
                NPCRole.Guard => NPCAIState.Patrolling,
                NPCRole.Merchant => NPCAIState.Trading,
                NPCRole.Cultivator => NPCAIState.Cultivating,
                NPCRole.Elder => NPCAIState.Idle,
                NPCRole.Disciple => NPCAIState.Cultivating,
                NPCRole.Enemy => NPCAIState.Wandering,
                NPCRole.Passerby => NPCAIState.Idle,
                _ => NPCAIState.Idle
            };
        }

        /// <summary>
        /// Рассчитать базовый урон с учётом экипировки, техник и усилений.
        /// Формула: baseDamage = STR × size × levelMult + weaponDamage + Σ(techniqueDamage) + enhancementDamageBonus
        /// Всё умножается на QiInfusion множитель.
        /// </summary>
        private int CalculateBaseDamage(SpeciesData species, SoulData soul,
            Dictionary<EquipmentSlot, string> equipmentIds,
            List<string> techniqueIds,
            EnhancementBonuses enhBonuses)
        {
            // Базовый урон = STR × размер × множитель уровня
            float baseDmg = species.BaseStrength;
            if (GameConstants.SizeClassStrengthMultipliers.TryGetValue(species.Size, out float sizeMult))
                baseDmg *= sizeMult;
            if (soul.CultivationLevel != CultivationLevel.None)
                baseDmg *= 1.0f + ((int)soul.CultivationLevel - 1) * 0.1f;

            // Бонус усилений: NaturalWeapon
            baseDmg += enhBonuses.DamageBonus;

            // QiInfusion множитель
            if (enhBonuses.QiDamageMultiplier > 0f)
                baseDmg *= (1f + enhBonuses.QiDamageMultiplier);

            // Техники: суммарный урон техник (TBD — нужен TechniqueRegistry для lookup)
            // Пока: +10% за каждую технику
            baseDmg *= 1f + techniqueIds.Count * 0.1f;

            return Math.Max(1, (int)baseDmg);
        }

        /// <summary>
        /// Рассчитать базовую защиту с учётом брони и усилений.
        /// Формула: materialReduction + Σ(armorDefense) + enhancementArmorBonus
        /// Всё умножается на QiInfusion множитель.
        /// </summary>
        private int CalculateBaseDefense(SpeciesData species, List<BodyPart> bodyParts,
            Dictionary<EquipmentSlot, string> equipmentIds,
            EnhancementBonuses enhBonuses)
        {
            float defense = 0f;

            // Снижение урона материала тела
            if (GameConstants.BodyMaterialReduction.TryGetValue(species.Material, out float reduction))
                defense += reduction * 100f;

            // Бонус усилений: NaturalArmor (% → абсолютный)
            defense += enhBonuses.ArmorBonusPercent;

            // QiInfusion множитель
            if (enhBonuses.QiDefenseMultiplier > 0f)
                defense *= (1f + enhBonuses.QiDefenseMultiplier);

            // Экипировка: каждая броня +5 защиты (TBD — нужен EquipmentDataProvider для lookup)
            defense += equipmentIds.Count * 5f;

            return Math.Max(0, (int)defense);
        }

        /// <summary>
        /// Рассчитать уровень агрессии по роли (0..1).
        /// </summary>
        private float CalculateAggressionLevel(NPCRole role)
        {
            return role switch
            {
                NPCRole.Monster => 0.8f,
                NPCRole.Enemy => 0.9f,
                NPCRole.Guard => 0.4f,
                NPCRole.Cultivator => 0.2f,
                NPCRole.Disciple => 0.3f,
                NPCRole.Merchant => 0.1f,
                NPCRole.Elder => 0.15f,
                NPCRole.Passerby => 0.05f,
                _ => 0.3f
            };
        }
    }
}
