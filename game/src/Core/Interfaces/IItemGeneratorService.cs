#nullable enable
// Создано: 2026-05-18 17:58:25 UTC
// Редактировано: 2026-03-05 12:00:00 UTC — Task 2.2: GenerateChargerForLevel
// Фасад генерации предметов. Обёртка над внутренними генераторами.
// Пайплайн: Generator.Generate() → DTO → Factory.Create() → SO → ItemDatabase.Register() → return SO.
using CultivationGame.Core.Data;
using System.Collections.Generic;

using CultivationGame.Core;
namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Фасад генерации предметов.
    /// Создаёт DTO через внутренние генераторы, конвертирует в SO через Factory,
    /// регистрирует в ItemDatabase и возвращает готовый SO.
    /// </summary>
    public interface IItemGeneratorService
    {
        /// <summary>Сгенерировать оружие по уровню культивации.</summary>
        EquipmentData GenerateWeaponForLevel(int cultivationLevel, long seed = 0);

        /// <summary>Сгенерировать броню по уровню культивации.</summary>
        EquipmentData GenerateArmorForLevel(int cultivationLevel, long seed = 0);

        /// <summary>Сгенерировать расходник по уровню культивации.</summary>
        ItemData GenerateConsumableForLevel(int cultivationLevel, long seed = 0);

        /// <summary>Сгенерировать зарядник Ци по уровню культивации (L3+, Belt-слот).</summary>
        EquipmentData GenerateChargerForLevel(int cultivationLevel, long seed = 0);

        /// <summary>Случайная экипировка (50/50 оружие/броня).</summary>
        EquipmentData GenerateRandomEquipment(int playerLevel, long seed = 0);

        /// <summary>Список лута (экипировка).</summary>
        List<EquipmentData> GenerateLoot(int playerLevel, int count, long seed = 0);

        /// <summary>Список лута (расходники).</summary>
        List<ItemData> GenerateConsumableLoot(int playerLevel, int count, long seed = 0);
    }
}
