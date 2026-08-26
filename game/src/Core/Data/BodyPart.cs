#nullable enable
// Создано: 2026-05-08 15:42:00 UTC
// Редактировано: 2026-05-09 12:00:00 UTC — аудит: DISC-01 одновременный split, BD-22 Severed guard, BD-27 комментарий, BD-29 IsVital
// Редактировано: 2026-05-18 — +BodyPartFunction Functions, +SetMaxHP для Vitality пересчёта
// Редактировано: 2026-05-18 13:10:29 UTC — P0-02 FIX: +Reattach(), P2-04 FIX: Functions→readonly, P1-04 FIX: BaseHitChance в ToData()
// Редактировано: 2026-05-23 — IMPL-1: Moved from Modules/Body to Core/Data
//   (fix Core→Modules architecture violation: NPCState in Core.Data references BodyPart, so BodyPart must be in Core.Data too).
// Migrated from Ai-game3 (Unity) to Ai-game4 (Godot).
// Данные части тела — мутабельный класс с двойной HP (Kenshi-style).
// Источник: BODY_SYSTEM.md "Система двойной HP"
using System;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Core.Data
{
    /// <summary>
    /// Часть тела с Kenshi-style двойной HP.
    ///
    /// Концепция:
    /// - Красная HP (функциональная) — работоспособность части
    /// - Чёрная HP (структурная) — физическая целостность
    ///
    /// Соотношение: Чёрная HP = Красная HP × STRUCTURAL_HP_MULTIPLIER (2.0)
    /// Исключение: Сердце имеет ТОЛЬКО красную HP (CORE-C01)
    ///
    /// Состояния (пороги RedHP):
    /// - Healthy:  RedHP ≥ 70% MaxRedHP
    /// - Bruised:  30% ≤ RedHP < 70% MaxRedHP
    /// - Wounded:  0 < RedHP < 30% MaxRedHP
    /// - Disabled: RedHP ≤ 0 (и BlackHP > 0)
    /// - Severed:  BlackHP ≤ 0 и MaxBlackHP > 0 (необратимо)
    ///
    /// Модель урона: ОДНОВРЕМЕННЫЙ split (DISC-01).
    /// RedHP и BlackHP уменьшаются НЕЗАВИСИМО — без overflow.
    /// Источник: BODY_SYSTEM.md «Урон распределяется одновременно (не последовательно!)»
    /// </summary>
    public class BodyPart
    {
        // === Идентификация ===
        public BodyPartType Type { get; }
        public bool IsVital { get; }

        // === Функции части тела (П.23) ===
        public BodyPartFunction Functions { get; }

        // === HP ===
        public int CurrentRedHP { get; private set; }
        public int MaxRedHP { get; private set; }
        public int CurrentBlackHP { get; private set; }
        public int MaxBlackHP { get; private set; }

        // === Состояние (вычисляется из HP) ===
        public BodyPartState State { get; private set; }

        // === Шанс попадания ===
        // P1-02 (V3) FIX: readonly auto-property вместо private set (аналогично P2-04 V2 FIX для Functions)
        public float BaseHitChance { get; }

        // === Конструктор ===

        /// <summary>
        /// Создать часть тела.
        /// </summary>
        /// <param name="partType">Тип части тела</param>
        /// <param name="maxRedHP">Максимальная красная HP</param>
        /// <param name="isVital">Жизненно важная (голова, сердце)</param>
        /// <param name="functions">Функции части тела (П.23)</param>
        public BodyPart(BodyPartType partType, int maxRedHP, bool isVital = false,
            BodyPartFunction functions = BodyPartFunction.None)
        {
            Type = partType;
            IsVital = isVital;
            Functions = functions;

            MaxRedHP = Math.Max(1, maxRedHP);
            CurrentRedHP = MaxRedHP;

            // CORE-C01: Сердце имеет ТОЛЬКО красную HP
            if (partType == BodyPartType.Heart)
            {
                MaxBlackHP = 0;
                CurrentBlackHP = 0;
            }
            else
            {
                MaxBlackHP = (int)(MaxRedHP * GameConstants.STRUCTURAL_HP_MULTIPLIER);
                CurrentBlackHP = MaxBlackHP;
            }

            State = BodyPartState.Healthy;

            // P1-02 (V3) FIX: BaseHitChance инициализируется через конструктор (readonly)
            BaseHitChance = GameConstants.BodyPartHitChances.TryGetValue(partType, out float chance)
                ? chance
                : 0.1f;
        }

        // === Методы ===

        /// <summary>
        /// Нанести урон части тела (раздельно по типам HP).
        /// DISC-01: RedHP и BlackHP уменьшаются НЕЗАВИСИМО — без overflow.
        /// Если часть уже Severed — урон не применяется.
        /// Источник: BODY_SYSTEM.md «Урон распределяется одновременно (не последовательно!)»
        /// </summary>
        /// <returns>True если урон применён, false если часть отрублена</returns>
        public bool TakeDamage(int redDmg, int blackDmg)
        {
            // Отрубленная часть не получает урон
            if (State == BodyPartState.Severed)
                return false;

            // DISC-01: Одновременный split — RedHP и BlackHP уменьшаются независимо
            // Без overflow: избыток RedHP НЕ переходит в BlackHP
            if (redDmg > 0)
            {
                CurrentRedHP = Math.Max(0, CurrentRedHP - redDmg);
            }

            if (blackDmg > 0 && MaxBlackHP > 0)
            {
                CurrentBlackHP = Math.Max(0, CurrentBlackHP - blackDmg);
            }

            UpdateState();
            return true;
        }

        /// <summary>
        /// Восстановить HP.
        /// Порядок: сначала чёрная (структурная), затем красная (функциональная).
        /// ФОРМ-BOD-02: нельзя вылечить отрубленную часть.
        /// </summary>
        /// <returns>Количество реально восстановленной HP (RedHP + BlackHP)</returns>
        public int Heal(int redHeal, int blackHeal = 0)
        {
            // Нельзя вылечить отрубленную часть
            if (State == BodyPartState.Severed)
                return 0;

            int previousRedHP = CurrentRedHP;
            int previousBlackHP = CurrentBlackHP;

            // ФОРМ-BOD-02: сначала структурная (black) HP
            if (blackHeal > 0 && MaxBlackHP > 0)
            {
                CurrentBlackHP = Math.Min(MaxBlackHP, CurrentBlackHP + blackHeal);
            }

            // Затем функциональная (red) HP
            if (redHeal > 0)
            {
                CurrentRedHP = Math.Min(MaxRedHP, CurrentRedHP + redHeal);
            }

            UpdateState();
            // R-03: возвращаем реально исцелённую HP (Red + Black)
            return (CurrentRedHP - previousRedHP) + (CurrentBlackHP - previousBlackHP);
        }

        /// <summary>
        /// Принудительно установить HP (для save/load).
        /// BD-22: Severed — необратимое состояние, SetHP игнорируется.
        /// </summary>
        public void SetHP(int redHP, int blackHP)
        {
            // BD-22: Severed необратим — SetHP не может обратить ампутацию
            if (State == BodyPartState.Severed) return;

            CurrentRedHP = Math.Max(0, Math.Min(MaxRedHP, redHP));
            CurrentBlackHP = Math.Max(0, Math.Min(MaxBlackHP, blackHP));
            UpdateState();
        }

        /// <summary>
        /// Установить новые максимальные HP (для пересчёта при изменении Vitality, П.24).
        /// Сохраняет пропорцию текущего урона.
        /// </summary>
        public void SetMaxHP(int newMaxRed, int newMaxBlack)
        {
            // Сохраняем пропорцию урона
            float redDamageRatio = MaxRedHP > 0
                ? (float)(MaxRedHP - CurrentRedHP) / MaxRedHP : 0f;
            float blackDamageRatio = MaxBlackHP > 0
                ? (float)(MaxBlackHP - CurrentBlackHP) / MaxBlackHP : 0f;

            MaxRedHP = Math.Max(1, newMaxRed);
            MaxBlackHP = Math.Max(0, newMaxBlack);

            // Восстанавливаем текущие HP с той же пропорцией урона
            CurrentRedHP = Math.Max(0, Math.Min(MaxRedHP,
                (int)(MaxRedHP * (1f - redDamageRatio))));
            CurrentBlackHP = MaxBlackHP > 0
                ? Math.Max(0, Math.Min(MaxBlackHP,
                    (int)(MaxBlackHP * (1f - blackDamageRatio))))
                : 0;

            // Не обновляем состояние — Severed остаётся Severed
            if (State != BodyPartState.Severed)
                UpdateState();
        }

        /// <summary>
        /// Приживление ампутированной части тела (P0-02 FIX).
        /// Восстанавливает HP части и переводит из Severed → Healthy/Bruised/etc.
        /// Используется для магического лечения, регенерации практика L10.
        /// </summary>
        /// <param name="redHP">Красная HP после приживления</param>
        /// <param name="blackHP">Чёрная HP после приживления</param>
        /// <returns>True если приживление успешно, false если часть не была Severed</returns>
        public bool Reattach(int redHP, int blackHP)
        {
            // Приживление возможно только для ампутированной части
            if (State != BodyPartState.Severed) return false;

            MaxRedHP = Math.Max(1, redHP);
            MaxBlackHP = Math.Max(0, blackHP);
            CurrentRedHP = Math.Min(MaxRedHP, Math.Max(1, redHP));
            CurrentBlackHP = Math.Min(MaxBlackHP, Math.Max(0, blackHP));

            // Обновляем состояние — Severed → Healthy/Bruised/etc.
            UpdateState();
            return true;
        }

        /// <summary>
        /// Экспорт данных для IBodyService.GetAllParts().
        /// BD-29: включает IsVital для внешних систем (UI, IsAlive).
        /// P1-04 FIX: включает BaseHitChance для Combat модуля.
        /// </summary>
        public BodyPartData ToData()
        {
            return new BodyPartData(Type, State, IsVital, CurrentRedHP, MaxRedHP, CurrentBlackHP, MaxBlackHP, Functions, BaseHitChance);
        }

        /// <summary>
        /// Обновить состояние части тела на основе текущих HP.
        /// НОВ-ТЕЛ-01: maxBlackHP > 0 — отсекать только если часть имела структурные HP.
        /// Сердце (maxBlackHP=0) не может быть Severed.
        /// </summary>
        private void UpdateState()
        {
            // Severed: структурная HP ≤ 0 и часть имела структурную HP
            if (CurrentBlackHP <= 0 && MaxBlackHP > 0)
            {
                State = BodyPartState.Severed;
                CurrentRedHP = 0;
                CurrentBlackHP = 0;
            }
            // Disabled: красная HP ≤ 0
            else if (CurrentRedHP <= 0)
            {
                State = BodyPartState.Disabled;
                CurrentRedHP = 0;
            }
            // Wounded: красная HP < 30%
            else if (MaxRedHP > 0 && CurrentRedHP < MaxRedHP * 0.3f)
            {
                State = BodyPartState.Wounded;
            }
            // Bruised: красная HP < 70%
            else if (MaxRedHP > 0 && CurrentRedHP < MaxRedHP * 0.7f)
            {
                State = BodyPartState.Bruised;
            }
            else
            {
                State = BodyPartState.Healthy;
            }
        }
    }
}
