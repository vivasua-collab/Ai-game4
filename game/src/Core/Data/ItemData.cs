#nullable enable
using System.Collections.Generic;
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Data;

// Created: 2026-05-08 (Ai-game3) — migrated 2026-08-15.
// Item data (base class) — modular architecture.
// Version 2.0: list-based inventory (no grid, mass + volume).
// Migrated from Unity ScriptableObject → plain C# class for engine-agnostic Core.

/// <summary>
/// Базовые данные предмета.
/// Версия 2.0: строчная модель инвентаря (нет сетки, масса + объём).
/// Engine-agnostic: no ScriptableObject base (consumers register instances via DI).
/// </summary>
public class ItemData
{
    // === Basic Info ===

    /// <summary>Уникальный ID предмета</summary>
    public string ItemId = string.Empty;

    /// <summary>Название на русском</summary>
    public string NameRu = string.Empty;

    /// <summary>Название на английском</summary>
    public string NameEn = string.Empty;

    /// <summary>Описание предмета</summary>
    public string Description = string.Empty;

    // === Classification ===

    /// <summary>Категория предмета</summary>
    public ItemCategory Category = ItemCategory.Misc;

    /// <summary>Тип предмета (детальная классификация)</summary>
    public string ItemType = string.Empty;

    /// <summary>Редкость</summary>
    public ItemRarity Rarity = ItemRarity.Common;

    // === Stacking ===

    /// <summary>Можно стакать</summary>
    public bool Stackable = true;

    /// <summary>Максимум в стаке</summary>
    public int MaxStack = 99;

    // === Physical ===

    /// <summary>Вес (кг)</summary>
    public float Weight = 0.1f;

    /// <summary>Объём (литры) — определяет вместимость в рюкзак/хранилище</summary>
    public float Volume = 1.0f;

    /// <summary>Стоимость (духовные камни)</summary>
    public int Value = 1;

    // === Durability ===

    /// <summary>Имеет прочность</summary>
    public bool HasDurability = false;

    /// <summary>Максимальная прочность</summary>
    public int MaxDurability = 100;

    // === Effects ===

    /// <summary>Эффекты при использовании</summary>
    public List<ItemEffect> Effects = new();

    // === Requirements ===

    /// <summary>Минимальный уровень культивации</summary>
    public int RequiredCultivationLevel = 0;

    /// <summary>Требования к характеристикам</summary>
    public List<StatRequirement> StatRequirements = new();

    // === Storage ===

    /// <summary>Куда можно поместить предмет (флаг вложения)</summary>
    public NestingFlag AllowNesting = NestingFlag.Any;
}

/// <summary>Эффект предмета при использовании.</summary>
public class ItemEffect
{
    public string EffectType = string.Empty;
    public float Value;
    public int Duration;
}

/// <summary>Требование к характеристике для использования предмета.</summary>
public class StatRequirement
{
    public string StatName = string.Empty;
    public int MinValue;
}

/// <summary>Специальный эффект предмета.</summary>
public class SpecialEffect
{
    public string EffectName = string.Empty;
    public string Description = string.Empty;
    public float TriggerChance;
}
