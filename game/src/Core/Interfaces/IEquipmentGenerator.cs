#nullable enable
// Создано: 2026-08-22 — интерфейс генератора экипировки «Матрёшка»
// (EQUIPMENT_SYSTEM.md §2: База × Материал × Грейд × Зачарование).
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Interfaces;

/// <summary>
/// Генератор экипировки по архитектуре «Матрёшка».
/// Все методы детерминированы при заданном seed (ЗАПРЕТ 3.9-friendly:
/// целочисленные статы, промилле-аккуратность не требуется на этом слое).
/// </summary>
public interface IEquipmentGenerator
{
    /// <summary>
    /// Сгенерировать оружие. subtype — id базового класса из
    /// EquipmentGenerationTables.Weapons ("sword", "dagger", ...) или null → случайный.
    /// </summary>
    EquipmentData GenerateWeapon(int level, string? subtype = null, long seed = 0);

    /// <summary>
    /// Сгенерировать броню. subtype — id из EquipmentGenerationTables.Armors
    /// ("armor_head", "armor_torso", ...) или null → случайный.
    /// </summary>
    EquipmentData GenerateArmor(int level, string? subtype = null, long seed = 0);

    /// <summary>
    /// 2026-08-26 — ЛЕГЕНДАРНОЕ оружие (принудительный путь Epic→Legendary:
    /// grade=Transcendent, Rarity=Legendary, гарант. зачарование + макс.
    /// бонусы грейда + value ×3).
    /// forceOvercap: null → ролл LEGENDARY_OVERCAP_CHANCE (18%); true/false —
    /// детерминированный оверкап (урон и прочность по формулам L+1).
    /// </summary>
    EquipmentData GenerateLegendaryWeapon(int level, string? subtype = null, long seed = 0, bool? forceOvercap = null);

    /// <summary>
    /// 2026-08-26 — ЛЕГЕНДАРНАЯ броня (принудительный путь Epic→Legendary).
    /// forceOvercap: null → ролл LEGENDARY_OVERCAP_CHANCE (18%); true/false —
    /// детерминированный оверкап (защита и прочность по формулам L+1).
    /// </summary>
    EquipmentData GenerateLegendaryArmor(int level, string? subtype = null, long seed = 0, bool? forceOvercap = null);

    /// <summary>Случайная экипировка (оружие или броня, 50/50).</summary>
    EquipmentData GenerateRandom(int level, long seed = 0);

    /// <summary>
    /// Наложить зачарование на предмет (§8): предмет должен соответствовать
    /// MinGrade определения. enchantId — id из EquipmentGenerationTables.Enchants
    /// или null → случайное подходящее.
    /// </summary>
    bool TryApplyEnchant(EquipmentData item, string? enchantId = null, long seed = 0);
}
