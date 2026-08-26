#nullable enable
using System.Collections.Generic;
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Data;

// Created: 2026-05-08 (Ai-game3) — migrated 2026-08-15.
// Equipment data (weapon, armor, accessories) — modular architecture.
// Migrated from Unity ScriptableObject → plain C# class for engine-agnostic Core.

/// <summary>
/// Данные экипировки (оружие, броня, аксессуары).
/// Экипировка НЕ стакается — каждый экземпляр уникален (grade, durability).
/// Engine-agnostic: no ScriptableObject base.
/// </summary>
public class EquipmentData : ItemData
{
    public EquipmentData()
    {
        Stackable = false; // Экипировка не стакается
    }

    // === Equipment ===

    /// <summary>Слот экипировки</summary>
    public EquipmentSlot Slot = EquipmentSlot.None;

    /// <summary>Тип хвата (одноручное/двуручное)</summary>
    public WeaponHandType HandType = WeaponHandType.OneHand;

    // === Stats ===

    /// <summary>Урон (для оружия)</summary>
    public int Damage = 0;

    /// <summary>Пробитие брони оружия. C6: Уменьшает эффективную броню цели.</summary>
    public int Penetration = 0;

    /// <summary>Дальность атаки в ед. мира. ≤2 = ближний бой, >2 = дальний бой. Фаза 9A.</summary>
    public int AttackRange = 2;

    /// <summary>Защита (для брони)</summary>
    public int Defense = 0;

    /// <summary>Покрытие брони (%)</summary>
    public float Coverage = 100f;

    /// <summary>Снижение урона (%)</summary>
    public float DamageReduction = 0f;

    /// <summary>Бонус к уклонению (%)</summary>
    public float DodgeBonus = 0f;

    // === Penalties ===

    /// <summary>Штраф скорости перемещения (%) — отрицательный</summary>
    public float MoveSpeedPenalty = 0f;

    /// <summary>Штраф проводимости Ци (%) — может быть отрицательным или положительным</summary>
    public float QiFlowPenalty = 0f;

    // === Material ===

    /// <summary>ID материала</summary>
    public string MaterialId = string.Empty;

    /// <summary>Категория материала (Metal, Leather, Cloth и т.д.)</summary>
    public MaterialCategory MaterialCategory = MaterialCategory.Metal;

    /// <summary>Тир материала</summary>
    public int MaterialTier = 1;

    /// <summary>Грейд экипировки</summary>
    public EquipmentGrade Grade = EquipmentGrade.Common;

    /// <summary>Уровень предмета (1-9)</summary>
    public int ItemLevel = 1;

    // === Bonuses ===

    /// <summary>Бонусы к характеристикам</summary>
    public List<StatBonus> StatBonuses = new();

    /// <summary>Особые эффекты</summary>
    public List<SpecialEffect> SpecialEffects = new();

    // === Technique Bonuses ===

    /// <summary>Бонус к урону техник (%) — для магического оружия</summary>
    public float TechniqueDamageBonus = 0f;

    /// <summary>Снижение стоимости Ци техник (%) — от качества оружия</summary>
    public float QiCostReduction = 0f;

    /// <summary>Ускорение накачки техник (%) — для духовного оружия</summary>
    public float ChargeSpeedBonus = 0f;

    // === Backpack Bonuses ===

    /// <summary>Бонус к максимальному весу (кг) — для рюкзаков</summary>
    public float WeightBonus;

    /// <summary>Бонус к максимальному объёму (литры) — для рюкзаков</summary>
    public float VolumeBonus;

    /// <summary>Снижение веса предметов (%) — 0-50% для рюкзаков</summary>
    public float WeightReduction;

    // === Storage Ring ===

    /// <summary>Тир кольца хранения (1-5) — определяет Qi cost и вместимость</summary>
    public int StorageRingTier;

    /// <summary>Вместимость кольца хранения (слоты) — DEPRECATED, используйте storageMaxVolume</summary>
    public int StorageCapacity;

    /// <summary>Максимальный объём кольца хранения (литры) — STR-MODEL</summary>
    public float StorageMaxVolume;
}
