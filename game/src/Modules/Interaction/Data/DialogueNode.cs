#nullable enable
// Создано: 2026-05-09 — Phase 13: модель узла диалога
using System.Collections.Generic;

namespace CultivationGame.Modules.Interaction.Data
{
    /// <summary>
    /// Узел диалога (одна реплика NPC + варианты ответа игрока).
    /// Формирует дерево диалога: каждый узел может вести к другим узлам.
    /// </summary>
    public class DialogueNode
    {
        /// <summary>Идентификатор узла (уникальный в рамках диалога)</summary>
        public string NodeId;

        /// <summary>Текст реплики NPC</summary>
        public string Text;

        /// <summary>Варианты ответа игрока (пустой = конец диалога)</summary>
        public readonly List<DialogueChoice> Choices = new List<DialogueChoice>();

        /// <summary>
        /// Идентификатор следующего узла, если нет выборов (линейный диалог).
        /// null или пустой = конец диалога.
        /// </summary>
        public string NextNodeId;

        /// <summary>Является ли этот узел конечным (нет выборов и нет NextNodeId)</summary>
        public bool IsEndNode => Choices.Count == 0 && string.IsNullOrEmpty(NextNodeId);
    }
}
