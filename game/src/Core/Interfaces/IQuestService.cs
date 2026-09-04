#nullable enable
// Создано: 2026-05-08 10:07:00 UTC
// Редактировано: 2026-05-09 — Phase 12: расширен интерфейс для системы квестов
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;
namespace CultivationGame.Core.Interfaces
{
    public interface IQuestService
    {
        /// <summary>Начать квест</summary>
        bool StartQuest(string questId);

        /// <summary>Бросить квест</summary>
        bool AbandonQuest(string questId);

        /// <summary>Завершить квест (если все цели выполнены)</summary>
        bool CompleteQuest(string questId);

        /// <summary>Провалить квест</summary>
        bool FailQuest(string questId, string reason = "");

        /// <summary>Идентификаторы активных квестов</summary>
        IReadOnlyList<string> GetActiveQuestIds();

        /// <summary>Завершён ли квест</summary>
        bool IsQuestComplete(string questId);

        /// <summary>Статус квеста</summary>
        QuestStatus GetQuestStatus(string questId);

        /// <summary>Существует ли квест с данным ID</summary>
        bool QuestExists(string questId);

        /// <summary>Тип квеста</summary>
        QuestType GetQuestType(string questId);

        /// <summary>
        /// 2026-09-04 S1: сводки ВСЕХ квестов (id, название, статус, прогресс,
        /// цели, награды) — для окна квестов (Q). Read-only DTO: Core не
        /// ссылается на типы модуля Quest.
        /// </summary>
        IReadOnlyList<QuestSummary> GetQuestSummaries();
    }

    /// <summary>
    /// 2026-09-04 S1: read-only сводка квеста для UI (окно квестов Q).
    /// </summary>
    public sealed class QuestSummary
    {
        public string QuestId = "";
        public string DisplayName = "";
        public string Description = "";
        public QuestStatus Status;
        /// <summary>0..1 по всем целям</summary>
        public float OverallProgress;
        /// <summary>Цели: текст + прогресс</summary>
        public (string Description, int Progress, int Target, bool Complete)[] Objectives =
            System.Array.Empty<(string, int, int, bool)>();
        /// <summary>Награды: человекочитаемые строки</summary>
        public string[] RewardTexts = System.Array.Empty<string>();
    }
}
