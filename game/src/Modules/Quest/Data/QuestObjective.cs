// Создано: 2026-05-09 — Phase 12: модель цели квеста
using CultivationGame.Core;
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.Quest.Data
{
    /// <summary>
    /// Цель квеста (одна задача в рамках квеста).
    /// Квест может иметь несколько целей — все должны быть выполнены для завершения.
    ///
    /// Примеры:
    /// - KillEnemy: TargetId="wolf", Target=3 → убить 3 волков
    /// - GatherItem: TargetId="iron", Target=5 → собрать 5 железа
    /// - ReachLocation: TargetId="forest", Target=1 → достигнуть леса
    /// - TalkToNPC: TargetId="elder_01", Target=1 → поговорить со старейшиной
    /// </summary>
    public class QuestObjective
    {
        /// <summary>Уникальный идентификатор цели (в рамках квеста)</summary>
        public string ObjectiveId;

        /// <summary>Тип цели</summary>
        public QuestObjectiveType Type;

        /// <summary>
        /// Идентификатор цели (EnemyId, ItemId, LocationId, NPCId, RecipeId).
        /// Для ReachCultivationLevel — уровень культивации как строка (напр. "3").
        /// </summary>
        public string TargetId;

        /// <summary>Текущий прогресс</summary>
        public int Progress;

        /// <summary>Целевое значение</summary>
        public int Target;

        /// <summary>Описание цели (для UI)</summary>
        public string Description;

        /// <summary>Выполнена ли цель</summary>
        public bool IsComplete => Progress >= Target;

        /// <summary>
        /// Увеличить прогресс на значение.
        /// Не превышает Target. B04 (FIX): отрицательный amount отклоняется.
        /// Возвращает true, если цель только что завершилась.
        /// </summary>
        public bool AddProgress(int amount)
        {
            if (IsComplete) return false;
            if (amount <= 0) return false;
            int previous = Progress;
            Progress = System.Math.Min(Progress + amount, Target);
            return previous < Target && IsComplete;
        }

        /// <summary>Сбросить прогресс</summary>
        public void Reset()
        {
            Progress = 0;
        }
    }
}
