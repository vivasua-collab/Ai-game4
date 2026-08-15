// Создано: 2026-05-09 — Phase 12: модель данных квеста
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.Quest.Data
{
    /// <summary>
    /// Определение квеста (шаблон + текущее состояние).
    /// Хранится в QuestService как реестр всех доступных и активных квестов.
    /// </summary>
    public class QuestData
    {
        // === Идентификация ===

        /// <summary>Уникальный идентификатор квеста</summary>
        public string QuestId;

        /// <summary>Отображаемое название</summary>
        public string DisplayName;

        /// <summary>Описание квеста</summary>
        public string Description;

        /// <summary>Тип квеста</summary>
        public QuestType Type;

        // === Состояние ===

        /// <summary>Текущий статус</summary>
        public QuestStatus Status;

        // === Цели ===

        /// <summary>Список целей квеста</summary>
        public readonly List<QuestObjective> Objectives = new List<QuestObjective>();

        // === Награды ===

        /// <summary>Список наград за квест</summary>
        public readonly List<QuestReward> Rewards = new List<QuestReward>();

        // === Условия (будущее расширение) ===

        /// <summary>Идентификатор квеста-предпосылки (null = нет)</summary>
        public string PrerequisiteQuestId;

        /// <summary>Минимальный уровень культивации для взятия квеста</summary>
        public int RequiredCultivationLevel;

        /// <summary>Срок выполнения в игровых днях (0 = без срока)</summary>
        public int TimeLimitDays;

        /// <summary>День взятия квеста (для расчёта срока)</summary>
        public int StartDay;

        // === Вычисляемые ===

        /// <summary>Все ли цели выполнены</summary>
        public bool AllObjectivesComplete
        {
            get
            {
                if (Objectives.Count == 0) return false;
                for (int i = 0; i < Objectives.Count; i++)
                {
                    if (!Objectives[i].IsComplete) return false;
                }
                return true;
            }
        }

        /// <summary>Общий прогресс квеста (0.0 — 1.0)</summary>
        public float OverallProgress
        {
            get
            {
                if (Objectives.Count == 0) return 0f;
                float total = 0f;
                for (int i = 0; i < Objectives.Count; i++)
                {
                    var obj = Objectives[i];
                    total += obj.Target > 0 ? (float)obj.Progress / obj.Target : 0f;
                }
                return total / Objectives.Count;
            }
        }

        /// <summary>Истёк ли срок квеста (если задан)</summary>
        public bool IsExpired(int currentDay)
        {
            if (TimeLimitDays <= 0) return false;
            return currentDay > StartDay + TimeLimitDays;
        }

        /// <summary>Найти цель по идентификатору</summary>
        public QuestObjective FindObjective(string objectiveId)
        {
            for (int i = 0; i < Objectives.Count; i++)
            {
                if (Objectives[i].ObjectiveId == objectiveId)
                    return Objectives[i];
            }
            return null;
        }
    }
}
