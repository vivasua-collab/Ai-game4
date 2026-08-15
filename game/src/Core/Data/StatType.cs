#nullable enable
// Создано: 2026-05-08 10:07:00 UTC
// Редактировано: 2026-05-09 05:15:31 UTC — Phase 5: расширение статов из BUFF_MODIFIERS_SYSTEM.md
// Типы характеристик сущности
namespace CultivationGame.Core.Data
{
    public enum StatType
    {
        // === ПЕРВИЧНЫЕ ХАРАКТЕРИСТИКИ ===
        Strength,       // Сила
        Agility,        // Ловкость (переименовано из Dexterity)
        Intelligence,   // Интеллект
        Vitality,       // Живучесть (переименовано из Constitution)
        Conductivity,   // Проводимость (Core-специфичная для культивации)

        // === ВТОРИЧНЫЕ ХАРАКТЕРИСТИКИ ===
        Speed,          // Скорость
        AttackSpeed,    // Скорость атаки
        Damage,         // Урон
        Defense,        // Защита
        Armor,          // Броня
        CritChance,     // Шанс крита
        CritDamage,     // Критический урон
        QiCost,         // Стоимость Ци
        QiEfficiency,   // Эффективность Ци
        Cooldown,       // Кулдаун
        Lifesteal,      // Вампиризм

        // === ДОПОЛНИТЕЛЬНЫЕ ХАРАКТЕРИСТИКИ (Phase 5) ===
        Stealth,        // Скрытность
        Perception,     // Восприятие
        HealingReceived,// Получаемое исцеление
        WeightCapacity, // Переносимый вес
        HpRegen,        // Регенерация HP
        Thorns,         // Шипы (возврат урона)
        Luck,           // Удача
        ExpBonus,       // Бонус опыта
        StaminaCost,    // Стоимость выносливости
        StaminaRegen,   // Регенерация выносливости
        QiRestoration,  // Восстановление Ци
        Evasion         // Уклонение
    }
}
