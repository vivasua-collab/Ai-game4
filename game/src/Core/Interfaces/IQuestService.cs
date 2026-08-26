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
    }
}
