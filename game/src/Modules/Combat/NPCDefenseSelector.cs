#nullable enable
// Создано: 2026-09-10 — R16 «доработка боевой системы» (D4).
// NPCDefenseSelector — выбор активной защиты NPC (COMBAT_SYSTEM.md §7).
// Pure C# (без движковых типов): детерминированная эвристика по экипировке
// и статам защитника. До R16 CombatService передавал DefenseSubtype.None
// для NPC-защитника — слои Dodge/Parry/Block были мертвы для NPC.
//
// Эвристика (слои §7 работают только при выбранной защите):
//   1. Щит во второй руке (WeaponOff) → Block (блок щитом: shieldBlock+STR).
//   2. Оружие в основной руке и STR заметно выше AGI → Parry (парирование:
//      weaponParryBonus+AGI — «силовая» школа фехтования).
//   3. Прочее → Dodge (уклонение: базовый 5% + AGI — универсально).
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.Combat
{
    /// <summary>
    /// R16: выбор стойки активной защиты для NPC-защитника.
    /// Вызывается CombatService.BuildAndExecuteDamageRequest для каждой
    /// атаки по NPC (детерминизм: без RNG — только статы/экипировка).
    /// </summary>
    public static class NPCDefenseSelector
    {
        /// <summary>
        /// Порог превосходства STR над AGI для выбора парирования (§7.2:
        /// парирование любит AGI, но с оружием в руках «силовое» фехтование
        /// STR-бойца тоже осмысленно). AGI ≥ STR → уклонение.
        /// </summary>
        private const int ParryStrengthMargin = 4;

        /// <summary>
        /// Выбрать защиту NPC. Pure-функция от экипировки/статов.
        /// </summary>
        /// <param name="hasShield">Щит во второй руке (WeaponOff)</param>
        /// <param name="hasWeapon">Оружие в основной руке (WeaponMain)</param>
        /// <param name="agi">Ловкость защитника</param>
        /// <param name="str">Сила защитника</param>
        public static DefenseSubtype PickDefense(bool hasShield, bool hasWeapon, int agi, int str)
        {
            // Щит — приоритет: Block использует shieldBlock-бонус экипировки
            // (без стойки бонус не применяется вообще — слой §7.3).
            if (hasShield)
                return DefenseSubtype.Block;

            // Вооружённый «силовик» парирует (оружие даёт parryBonus).
            if (hasWeapon && str > agi + ParryStrengthMargin)
                return DefenseSubtype.Parry;

            // Остальные уклоняются (кулаки/лёгкие/ловкие — Dodge, §7.1).
            return DefenseSubtype.Dodge;
        }
    }
}
