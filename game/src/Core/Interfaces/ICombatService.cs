#nullable enable
// Создано: 2026-05-08 10:07:00 UTC
// Редактировано: 2026-05-25 06:00:00 UTC — A3-5 FIX: +ExecuteAttack overload с targetId/isRanged
// Редактировано: 2026-05-24 06:42:00 UTC — FIX CS1729/CS0171: this() 28→27 аргументов (лишний 0 в C1/C2/C3/C6 группе; DefenderSTR уже отдельным параметром)
// Редактировано: 2026-05-24 05:45:00 UTC — FIX CS0103/CS1729/CS0171: +using System; (Math.Round в DamageRequest)
// Редактировано: 2026-05-09 — CMB-A02/A05/A07/A08: добавлены поля для пайплайна урона
// Редактировано: 2026-05-22 04:14:49 UTC — Спринт 3 B1: статы атакующего/защищающегося для scaling формул
// Редактировано: 2026-05-22 07:55:00 UTC — Аудит CRIT-1: Potency→int промилле, DefenseContext.DamageReduction→int промилле
// Редактировано: 2026-05-22 09:51:00 UTC — Спринт 5 C1/C2/C3: ArmorDodgePenalty, AttackerLuck, TechniqueCritBonus, ShieldBlock, WeaponParryBonus
// Редактировано: 2026-05-22 09:51:00 UTC — Спринт 6 C6: Penetration в DamageRequest + DefenseContext
// Редактировано: 2026-05-22 11:30:00 UTC — Спринт 8 C10: TargetMorphology в DamageRequest
// Редактировано: 2026-05-22 13:08:27 UTC — P2-5.2 FIX: DefenderSTR для блока вместо attackerSTR
// Редактировано: 2026-05-22 13:55:00 UTC — Этап 3.5: P2-4.1 FIX: IsPlayerTarget; P2-7.3 FIX: AttackSubtype в DamageRequest
using System;

using CultivationGame.Core;
using CultivationGame.Core.Data;
namespace CultivationGame.Core.Interfaces
{
    public interface ICombatService
    {
        bool IsInCombat { get; }
        CombatStage CurrentStage { get; }
        string CurrentTargetId { get; }
        void StartCombat(string instigatorId, string targetId);
        void EndCombat();
        void ExecuteAttack(string attackerId, string techniqueId);
        /// <summary>
        /// A3-5 FIX: Полная сигнатура ExecuteAttack с TargetId и IsRanged.
        /// Позволяет передать цель и тип дальности из AttackIntentEvent.
        /// Обратная совместимость: старый 2-параметровый вызов делегирует сюда с defaults.
        /// </summary>
        void ExecuteAttack(string attackerId, string techniqueId, string targetId = null, bool isRanged = false);
        void ExecuteDefense(string defenderId, DefenseSubtype defenseType);
    }

    public interface IDamageService
    {
        DamageResult CalculateDamage(DamageRequest request);
        int ApplyDefense(int damage, DefenseContext context);
    }

    /// <summary>
    /// Запрос на расчёт урона.
    /// CMB-A02: добавлены AttackerLevel/DefenderLevel для подавления уровнем.
    /// CMB-A05: добавлен ActiveDefense для активной защиты.
    /// CMB-A07: добавлен DefenderElement для стихийного множителя.
    /// CMB-A08: добавлен DefenderMaterial для материала тела.
    /// Спринт 3 B1: добавлены AttackerSTR/AGI/INT, DefenderAGI для stat scaling.
    /// Аудит CRIT-1: Potency → int (промилле, 1000=×1.0), ЗАПРЕТ 3.9.
    /// Спринт 5 C1: ArmorDodgePenalty — штраф уклонения от брони (промилле).
    /// Спринт 5 C2: AttackerLuck, TechniqueCritBonus — шанс крита из удачи и техники.
    /// Спринт 5 C3: ShieldBlock, WeaponParryBonus — шанс блока/парирования (промилле).
    /// Спринт 6 C6: Penetration — пробитие брони атакующего.
    /// </summary>
    public readonly struct DamageRequest
    {
        public readonly string AttackerId;
        public readonly string TargetId;
        public readonly int BaseDamage;
        public readonly DamageType Type;
        public readonly Element Element;
        public readonly Element DefenderElement;        // Стихия защищающегося (CMB-A07)
        public readonly AttackType AttackType;
        public readonly TechniqueGrade Grade;
        public readonly int PotencyPermil;              // Мощность в промилле (CRIT-1: 1000=×1.0, 2000=×2.0)
        public readonly int AttackerLevel;             // Уровень культивации атакующего (CMB-A02)
        public readonly int DefenderLevel;             // Уровень культивации защищающегося (CMB-A02)
        public readonly DefenseSubtype ActiveDefense;   // Активная защита цели (CMB-A05)
        public readonly BodyMaterial DefenderMaterial;  // Материал тела цели (CMB-A08)

        // Спринт 3 B1: Статы атакующего для scaling формул (промилле: 50 = +5% за единицу)
        public readonly int AttackerSTR;               // Сила атакующего (MeleeStrike scaling)
        public readonly int AttackerAGI;               // Ловкость атакующего (MeleeWeapon scaling)
        public readonly int AttackerINT;               // Интеллект атакующего (Ranged scaling)
        public readonly int DefenderAGI;               // Ловкость защищающегося (для уклонения)

        // Спринт 5 C1: Штраф уклонения от брони
        public readonly int ArmorDodgePenalty;          // Штраф уклонения брони (промилле)

        // Спринт 5 C2: Шанс критического удара
        public readonly int AttackerLuck;               // Удача атакующего (влияет на шанс крита)
        public readonly int TechniqueCritBonus;         // Бонус крита от техники (промилле)

        // Спринт 5 C3: Блок и парирование
        public readonly int ShieldBlock;                // Блок щита (промилле, базовый шанс)
        public readonly int WeaponParryBonus;           // Бонус парирования оружия (промилле)
        public readonly int DefenderSTR;               // P2-5.2 FIX: Сила защищающегося (для блока вместо attackerSTR)

        // Спринт 6 C6: Пробитие брони
        public readonly int Penetration;                // Пробитие брони атакующего (уменьшает эффективную броню)

        // Спринт 8 C10: Морфология цели для таблицы попадания
        public readonly Morphology TargetMorphology;     // Морфология цели (выбирает таблицу BodyPartHitChances)

        // P2-4.1 FIX: Флаг «цель — игрок» вместо магической строки "player"
        public readonly bool IsPlayerTarget;              // true, если цель — игрок (для ветвления QiBuffer/armor cache)

        // P2-7.3 FIX: Подтип атаки для различения slashing/piercing от blunt (для кровотечения)
        public readonly CombatSubtype AttackSubtype;       // Подтип боевой техники (MeleeStrike, MeleeWeapon, и т.д.)

        /// <summary>
        /// Обратная совместимость — старый конструктор.
        /// Новые поля получают значения по умолчанию.
        /// CRIT-1: potency float→промилле (potency * 1000).
        /// P3-4.1 FIX: Math.Round вместо truncation (0.9999f → 1000, не 999).
        /// </summary>
        public DamageRequest(string attackerId, string targetId, int baseDamage, DamageType type,
            Element element, AttackType attackType, TechniqueGrade grade, float potency)
            : this(attackerId, targetId, baseDamage, type, element, Element.Neutral,
                   attackType, grade, (int)Math.Round(potency * 1000f), 1, 1, DefenseSubtype.None, BodyMaterial.Organic,
                   0, 0, 0, 0, // B1: статы = 0
                   0, 0, 0, 0, 0, 0, // C1/C2/C3/C6: armorDodgePenalty, attackerLuck, techniqueCritBonus, shieldBlock, weaponParryBonus, penetration
                   Morphology.Humanoid, 10, // C10: морфология, P2-5.2: DefenderSTR
                   targetId == "player", // P2-4.1: IsPlayerTarget из строки (обратная совместимость)
                   CombatSubtype.None) // P2-7.3: подтип атаки (неизвестен в старом конструкторе)
        {
        }

        /// <summary>
        /// Полный конструктор с данными пайплайна урона + статами (Спринт 3 B1).
        /// CRIT-1: potencyParam теперь int (промилле, 1000=×1.0).
        /// Спринт 5 C1/C2/C3: ArmorDodgePenalty, AttackerLuck, TechniqueCritBonus, ShieldBlock, WeaponParryBonus.
        /// Спринт 6 C6: Penetration.
        /// Спринт 8 C10: TargetMorphology.
        /// </summary>
        public DamageRequest(string attackerId, string targetId, int baseDamage, DamageType type,
            Element element, Element defenderElement, AttackType attackType, TechniqueGrade grade,
            int potencyPermil, int attackerLevel, int defenderLevel,
            DefenseSubtype activeDefense, BodyMaterial defenderMaterial,
            int attackerSTR, int attackerAGI, int attackerINT, int defenderAGI,
            int armorDodgePenalty, int attackerLuck, int techniqueCritBonus,
            int shieldBlock, int weaponParryBonus, int penetration,
            Morphology targetMorphology = Morphology.Humanoid,
            int defenderSTR = 10, // P2-5.2: DefenderSTR для блока
            bool isPlayerTarget = false, // P2-4.1: флаг «цель — игрок»
            CombatSubtype attackSubtype = CombatSubtype.None) // P2-7.3: подтип атаки
        {
            AttackerId = attackerId;
            TargetId = targetId;
            BaseDamage = baseDamage;
            Type = type;
            Element = element;
            DefenderElement = defenderElement;
            AttackType = attackType;
            Grade = grade;
            PotencyPermil = potencyPermil;
            AttackerLevel = attackerLevel;
            DefenderLevel = defenderLevel;
            ActiveDefense = activeDefense;
            DefenderMaterial = defenderMaterial;
            AttackerSTR = attackerSTR;
            AttackerAGI = attackerAGI;
            AttackerINT = attackerINT;
            DefenderAGI = defenderAGI;
            ArmorDodgePenalty = armorDodgePenalty;
            AttackerLuck = attackerLuck;
            TechniqueCritBonus = techniqueCritBonus;
            ShieldBlock = shieldBlock;
            WeaponParryBonus = weaponParryBonus;
            DefenderSTR = defenderSTR; // P2-5.2: Сила защищающегося
            Penetration = penetration;
            TargetMorphology = targetMorphology; // C10: морфология цели
            IsPlayerTarget = isPlayerTarget; // P2-4.1: флаг «цель — игрок»
            AttackSubtype = attackSubtype; // P2-7.3: подтип атаки (для кровотечения)
        }
    }

    public readonly struct DamageResult
    {
        public readonly int FinalDamage;
        public readonly int AbsorbedByQi;
        public readonly int AbsorbedByArmor;
        public readonly BodyPartType HitPart;
        public readonly CombatAttackResult Result;
        public readonly bool IsFatal;

        public DamageResult(int finalDamage, int absorbedByQi, int absorbedByArmor,
            BodyPartType hitPart, CombatAttackResult result, bool isFatal)
        {
            FinalDamage = finalDamage; AbsorbedByQi = absorbedByQi;
            AbsorbedByArmor = absorbedByArmor; HitPart = hitPart;
            Result = result; IsFatal = isFatal;
        }
    }

    public readonly struct DefenseContext
    {
        public readonly string DefenderId;
        public readonly int ArmorValue;
        public readonly BodyMaterial Material;
        public readonly int DamageReductionPermil; // Снижение урона в промилле (CRIT-1: 200=20%, 300=30%)
        public readonly int Penetration;            // Спринт 6 C6: пробитие брони атакующего

        /// <summary>
        /// Конструктор без пробития (обратная совместимость).
        /// </summary>
        public DefenseContext(string defenderId, int armorValue, BodyMaterial material, int damageReductionPermil)
            : this(defenderId, armorValue, material, damageReductionPermil, 0) { }

        /// <summary>
        /// Полный конструктор с пробитием (Спринт 6 C6).
        /// </summary>
        public DefenseContext(string defenderId, int armorValue, BodyMaterial material,
            int damageReductionPermil, int penetration)
        {
            DefenderId = defenderId; ArmorValue = armorValue;
            Material = material; DamageReductionPermil = damageReductionPermil;
            Penetration = penetration;
        }
    }
}
