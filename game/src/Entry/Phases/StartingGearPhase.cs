#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Modules.Combat; // Phase 8 ч.3: CombatRangeGateService.ArrowItemId
using CultivationGame.Modules.Generator;

namespace CultivationGame.Entry.Phases;

/// <summary>
/// Phase 5 (2026-09-03): СТАРТОВАЯ ГЕНЕРАЦИЯ предметов (замена сейвов по
/// решению пользователя — сейвы отложены, каждая новая игра начинается
/// с детерминированного стартового набора).
///
/// ЗАМЕНЯЕТ dev-хак InventoryWindow.SeedGeneratedItems (#if DEBUG, слой UI,
/// в release не выполнялся). Теперь:
///   • правильный слой — Entry-фаза сборки сцены (работает и в release);
///   • регистрация канонических предметов в БД (материалы/расходники/камни Ци)
///     ДО спавна NPC (лавки через EnsureMaterialItem находят их сразу);
///   • стартовый набор: оружие НАДЕТО (вооружённый melee с первого шага),
///     лук в инвентаре (тест дальнего боя Phase 8 ч.2 без читов).
///
/// Состав (EQUIPMENT_SYSTEM.md §10, стартовое имущество практика L1):
///   1. Кинжал L1 — авто-экипировка в WeaponMain (подтип MeleeWeapon, M2)
///   2. Лук L1 — в инвентарь (режим дальнего боя: клавиша 2)
///   3. 2 случайных оружия L1-3, 2 брони L1-2, 1 случайный предмет L2
///   4. Материалы ×5 (древесина/камень/руда/волокно) — крафт + продажа
///   5. Расходники ×5 (ягоды/трава/пилюля лечения/пилюля Ци)
///   6. Камни Ци (dust×3, pebble×2, shard×1, chaotic dust×1) — валюта/поглощение
///
/// Техники выдаёт TechniqueGrantPhase (PhaseOrder 13) — без изменений.
/// </summary>
public sealed class StartingGearPhase : AbstractSceneAssemblyPhase
{
    public override string PhaseName => "StartingGear";
    // После PlayerSpawn (4); рядом с NPC/Animal (5) — фазе нужен только игрок.
    // Регистрация предметов в БД ДО HumanNPCSpawn (6): EnsureMaterialItem
    // лавок найдёт канонические материалы сразу.
    public override int PhaseOrder => 5;

    [Inject] private readonly IPlayerService _player = null!;
    [Inject] private readonly IInventoryService _inventory = null!;
    [Inject] private readonly IItemDatabaseService _itemDb = null!;
    [Inject] private readonly IEquipmentService _equipment = null!;
    [Inject] private readonly IEquipmentGenerator _equipmentGenerator = null!;

    /// <summary>Детерминированный сид стартового набора (одинаков в каждой игре).</summary>
    private const long Seed = 1000;

    /// <summary>
    /// Phase 8 ч.3: стартовый колчан. 30 стрел — базовый запас на первые
    /// бои (дальний бой — приоритет MVP); расход 1 стрела/выстрел.
    /// </summary>
    private const int ArrowQuiverSize = 30;

    public override Task ExecuteAsync()
    {
        int registered = 0;
        int granted = 0;

        // === 1. Канонические материалы (БД + игроку ×5) ===
        var materials = new (string id, string name, float weight, float volume, ItemRarity rarity, int maxStack)[]
        {
            ("material_wood",    "Древесина",             0.5f,  1.0f, ItemRarity.Common,   100),
            ("material_stone",   "Камень",                1.0f,  1.0f, ItemRarity.Common,   100),
            ("material_iron_ore","Железная руда",         1.5f,  1.0f, ItemRarity.Uncommon, 100),
            ("material_fiber",   "Растительное волокно",  0.05f, 0.2f, ItemRarity.Common,   100),
        };
        foreach (var (id, name, weight, volume, rarity, maxStack) in materials)
        {
            var item = new ItemData
            {
                ItemId = id,
                NameRu = name,
                NameEn = name,
                Description = "Материал",
                Category = ItemCategory.Material,
                ItemType = "Material",
                Rarity = rarity,
                Stackable = true,
                MaxStack = maxStack,
                Weight = weight,
                Volume = volume,
                Value = 5,
                HasDurability = false,
            };
            _itemDb.Register(item);
            registered++;
            if (_inventory.TryAddItem(item, 5)) granted++;
        }

        // === 2. Канонические расходники (БД + игроку ×5) ===
        var consumables = new (string id, string name, float weight, float volume, ItemRarity rarity, int maxStack, string effect, int value)[]
        {
            ("consumable_berry",  "Ягоды",               0.05f, 0.1f, ItemRarity.Common,   50, "heal",       5),
            ("consumable_herb",   "Лекарственная трава", 0.03f, 0.1f, ItemRarity.Uncommon, 50, "material",   0),
            ("con_pill_healing",  "Пилюля лечения",      0.05f, 0.1f, ItemRarity.Common,   20, "heal",      30),
            ("con_pill_qi",       "Пилюля Ци",           0.05f, 0.1f, ItemRarity.Uncommon, 20, "qi_restore", 50),
        };
        foreach (var (id, name, weight, volume, rarity, maxStack, effect, value) in consumables)
        {
            var item = new ItemData
            {
                ItemId = id,
                NameRu = name,
                NameEn = name,
                Description = "Расходник",
                Category = ItemCategory.Consumable,
                ItemType = "Consumable",
                Rarity = rarity,
                Stackable = true,
                MaxStack = maxStack,
                Weight = weight,
                Volume = volume,
                Value = value,
                HasDurability = false,
            };
            _itemDb.Register(item);
            registered++;
            if (_inventory.TryAddItem(item, 5)) granted++;
        }

        // === 2b. Phase 8 ч.3: стрелы (боеприпас дальнего боя) ===
        // Расходник лука: 1 стрела = 1 выстрел (CombatRangeGateService
        // списывает при каждом ranged-интенте). Стартовый колчан 30 —
        // хватает на тесты боёвки без читов; докупается/добывается позже.
        var arrow = new ItemData
        {
            ItemId = CombatRangeGateService.ArrowItemId,
            NameRu = "Стрела",
            NameEn = "Arrow",
            Description = "Боеприпас для лука",
            Category = ItemCategory.Material,
            ItemType = "Ammo",
            Rarity = ItemRarity.Common,
            Stackable = true,
            MaxStack = 100,
            Weight = 0.02f,
            Volume = 0.05f,
            Value = 1,
            HasDurability = false,
        };
        _itemDb.Register(arrow);
        registered++;
        if (_inventory.TryAddItem(arrow, ArrowQuiverSize)) granted++;

        // === 3. Камни Ци: регистрация 10 канонических + стартовый набор ===
        QiStoneSeeder.Seed(_itemDb);
        registered += 10; // 5 размеров × calm/chaotic
        if (_itemDb.TryGetItem("qistone_dust_calm", out var qDust))
            { _inventory.TryAddItem(qDust, 3); granted++; }
        if (_itemDb.TryGetItem("qistone_pebble_calm", out var qPebble))
            { _inventory.TryAddItem(qPebble, 2); granted++; }
        if (_itemDb.TryGetItem("qistone_shard_calm", out var qShard))
            { _inventory.TryAddItem(qShard, 1); granted++; }
        if (_itemDb.TryGetItem("qistone_dust_chaotic", out var qChaotic))
            { _inventory.TryAddItem(qChaotic, 1); granted++; }

        // === 4. Экипировка («Матрёшка», детерминированный сид) ===
        int weapons = 0, armors = 0;

        // 4a. Стартовый кинжал L1 — НАДЕТЬ (вооружённый melee с первого шага).
        var dagger = _equipmentGenerator.GenerateWeapon(1, "dagger", Seed + 1);
        if (dagger != null)
        {
            _itemDb.Register(dagger);
            if (_equipment.TryEquip(EquipmentSlot.WeaponMain, dagger)) weapons++;
        }

        // 4b. Стартовый лук L1 — в инвентарь (дальний бой Phase 8 ч.2:
        // надеть через инвентарь (I) → клавиша 2 — режим дальнего боя).
        var bow = _equipmentGenerator.GenerateWeapon(1, "bow", Seed + 2);
        if (bow != null)
        {
            _itemDb.Register(bow);
            if (_inventory.TryAddItem(bow, 1)) weapons++;
        }

        // 4c. 2 случайных оружия L1-3 (разнообразие для экспериментов/торговли).
        for (int i = 0; i < 2; i++)
        {
            var weapon = _equipmentGenerator.GenerateWeapon(1 + i, null, Seed + 10 + i);
            if (weapon != null)
            {
                _itemDb.Register(weapon);
                if (_inventory.TryAddItem(weapon, 1)) weapons++;
            }
        }

        // 4d. 2 брони L1-2 (торс + случайная).
        var torso = _equipmentGenerator.GenerateArmor(1, "armor_torso", Seed + 100);
        if (torso != null)
        {
            _itemDb.Register(torso);
            if (_inventory.TryAddItem(torso, 1)) armors++;
        }
        var armor = _equipmentGenerator.GenerateArmor(2, null, Seed + 101);
        if (armor != null)
        {
            _itemDb.Register(armor);
            if (_inventory.TryAddItem(armor, 1)) armors++;
        }

        // 4e. 1 случайный предмет L2.
        if (_equipmentGenerator.GenerateRandom(2, Seed + 200) is { } eq)
        {
            _itemDb.Register(eq);
            _inventory.TryAddItem(eq, 1);
        }

        bool daggerEquipped = _equipment.GetEquipped(EquipmentSlot.WeaponMain) != null;
        Console.WriteLine(
            $"[Phase {PhaseOrder}] {PhaseName} complete — стартовый набор: " +
            $"БД +{registered}, выдано {granted} позиций, оружие {weapons} шт " +
            $"(кинжал надет: {daggerEquipped}, лук в инвентаре: {bow != null}), " +
            $"броня {armors} шт, стрел {ArrowQuiverSize}. " +
            $"Замена сейвов: каждая новая игра = одинаковый старт (сид {Seed}).");

        return Task.CompletedTask;
    }
}
