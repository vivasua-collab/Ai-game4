#nullable enable
// Создано: 2026-05-18 17:58:25 UTC
// Точка входа модуля Generator.
// IStartable — инициализация базы данных предметов.
// Migrated from Ai-game3 (Unity+VContainer+MessagePipe) to Ai-game4 (Godot+DI+EventBus) 2026-08-15.
using System;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Generator;

/// <summary>
/// Точка входа модуля Generator.
/// Инициализирует ItemDatabaseService при старте (загрузка предустановленных предметов).
/// </summary>
public class GeneratorModule : IModule
{
    [Inject] private readonly IItemDatabaseService _itemDatabase = null!;
    [Inject] private readonly IItemGeneratorService _itemGenerator = null!;
    [Inject] private readonly ITechniqueGeneratorService _techniqueGenerator = null!;

    public string ModuleName => "Generator";

    public void Start()
    {
        // Вызываем Initialize() через concrete-тип, так как метод не входит в интерфейс
        if (_itemDatabase is ItemDatabaseService dbServiceImpl)
        {
            dbServiceImpl.Initialize();
            Console.WriteLine("[GeneratorModule] База данных предметов инициализирована");
        }
        else
        {
            Console.WriteLine("[GeneratorModule] IItemDatabaseService не является ItemDatabaseService — Initialize() пропущен");
        }

        Console.WriteLine($"[GeneratorModule] Модуль запущен. Зарегистрировано предметов: {_itemDatabase.Count}");

        // Debug mode: GODOT_GEN_DEBUG=1 generates sample items + techniques, prints to log.
        // Usage: GODOT_GEN_DEBUG=1 godot --headless scenes/GameWorld.tscn
        var debugFlag = Environment.GetEnvironmentVariable("GODOT_GEN_DEBUG");
        if (!string.IsNullOrEmpty(debugFlag) && debugFlag != "0")
        {
            RunGeneratorDebugDump();
        }
    }

    public void Tick(int tickCount)
    {
        // Generator has no per-tick work
    }

    public void Dispose()
    {
    }

    /// <summary>
    /// Debug dump: generate 5 items + 3 techniques, print all fields.
    /// Verifies generators produce valid output.
    /// </summary>
    private void RunGeneratorDebugDump()
    {
        Console.WriteLine("");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("  GENERATOR DEBUG DUMP (GODOT_GEN_DEBUG=1)");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");

        // === Items: weapon, armor, consumable, charger, random ===
        Console.WriteLine("");
        Console.WriteLine("── Items (5 samples) ──");
        try
        {
            var weapon = _itemGenerator.GenerateWeaponForLevel(cultivationLevel: 3, seed: 1001);
            Console.WriteLine($"  [Weapon]  {weapon.ItemId} | {weapon.NameRu} | rarity={weapon.Rarity} | slot={weapon.Slot} | dmg={weapon.Damage} | pen={weapon.Penetration} | hand={weapon.HandType} | wt={weapon.Weight}F");
            Console.WriteLine($"            category={weapon.Category} | grade={weapon.Grade} | material={weapon.MaterialCategory}:{weapon.MaterialTier} | dur={weapon.MaxDurability}");

            var armor = _itemGenerator.GenerateArmorForLevel(cultivationLevel: 3, seed: 2002);
            Console.WriteLine($"  [Armor]   {armor.ItemId} | {armor.NameRu} | rarity={armor.Rarity} | slot={armor.Slot} | def={armor.Defense} | cov={armor.Coverage}F | dr={armor.DamageReduction}F | dodge={armor.DodgeBonus}F");
            Console.WriteLine($"            category={armor.Category} | grade={armor.Grade} | wt={armor.Weight}F");

            var consumable = _itemGenerator.GenerateConsumableForLevel(cultivationLevel: 3, seed: 3003);
            Console.WriteLine($"  [Consum]  {consumable.ItemId} | {consumable.NameRu} | rarity={consumable.Rarity} | stack={consumable.MaxStack} | wt={consumable.Weight}F | vol={consumable.Volume}F");

            var charger = _itemGenerator.GenerateChargerForLevel(cultivationLevel: 5, seed: 4004);
            Console.WriteLine($"  [Charger] {charger.ItemId} | {charger.NameRu} | rarity={charger.Rarity} | slot={charger.Slot} | hand={charger.HandType}");

            var random = _itemGenerator.GenerateRandomEquipment(playerLevel: 4, seed: 5005);
            Console.WriteLine($"  [Random]  {random.ItemId} | {random.NameRu} | rarity={random.Rarity} | slot={random.Slot} | dmg={random.Damage} | def={random.Defense}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Item generation FAILED: {ex.GetType().Name}: {ex.Message}");
        }

        // === Techniques: 3 samples for different roles ===
        Console.WriteLine("");
        Console.WriteLine("── Techniques (3 samples) ──");
        try
        {
            var roles = new[] { NPCRole.Cultivator, NPCRole.Guard, NPCRole.Enemy };
            for (int i = 0; i < roles.Length; i++)
            {
                var tech = _techniqueGenerator.Generate(cultivationLevel: 3 + i, roles[i], seed: 6000 + i);
                Console.WriteLine($"  [Tech{i+1}] {tech.TechniqueId} | {tech.NameRu} | role={roles[i]}");
                Console.WriteLine($"           type={tech.Type} | sub={tech.Subtype} | element={tech.Element} | cap={tech.CapacityCost} | qiCost={tech.QiCost}");
                Console.WriteLine($"           cooldown={tech.Cooldown}F | range={tech.Range} | castTime={tech.CastTime}F | dmg={tech.BaseDamage} | mastery={tech.Mastery}F | ult={tech.IsUltimate}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Technique generation FAILED: {ex.GetType().Name}: {ex.Message}");
        }

        // === Loot batch ===
        Console.WriteLine("");
        Console.WriteLine("── Loot batch (3 items) ──");
        try
        {
            var loot = _itemGenerator.GenerateLoot(playerLevel: 3, count: 3, seed: 7000);
            foreach (var item in loot)
            {
                Console.WriteLine($"  [Loot]    {item.ItemId} | {item.NameRu} | rarity={item.Rarity} | slot={item.Slot}");
            }

            var consumableLoot = _itemGenerator.GenerateConsumableLoot(playerLevel: 3, count: 3, seed: 8000);
            foreach (var item in consumableLoot)
            {
                Console.WriteLine($"  [CLoot]   {item.ItemId} | {item.NameRu} | rarity={item.Rarity} | stack={item.MaxStack}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Loot generation FAILED: {ex.GetType().Name}: {ex.Message}");
        }

        // === Database stats ===
        Console.WriteLine("");
        Console.WriteLine("── Database stats after generation ──");
        Console.WriteLine($"  Total items registered: {_itemDatabase.Count}");
        var byCategory = new[] { ItemCategory.Weapon, ItemCategory.Armor, ItemCategory.Accessory, ItemCategory.Consumable, ItemCategory.Material };
        foreach (var cat in byCategory)
        {
            var items = _itemDatabase.GetItemsByCategory(cat);
            Console.WriteLine($"  {cat}: {items.Count} items");
        }

        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("");
    }
}
