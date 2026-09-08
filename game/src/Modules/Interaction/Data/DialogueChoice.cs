#nullable enable
// Создано: 2026-05-09 — Phase 13: модель выбора в диалоге
using System.Collections.Generic;
namespace CultivationGame.Modules.Interaction.Data
{
    /// <summary>
    /// Вариант ответа игрока в диалоге.
    /// Каждый выбор ведёт к определённому узлу диалога.
    /// </summary>
    public class DialogueChoice
    {
        /// <summary>Индекс выбора (для SelectChoice)</summary>
        public int Index;

        /// <summary>Текст варианта ответа (для UI)</summary>
        public string Text;

        /// <summary>Идентификатор узла, к которому ведёт этот выбор</summary>
        public string TargetNodeId;

        /// <summary>Условие доступности (будущее расширение: уровень культивации, фракция и т.д.)</summary>
        public string ConditionId;

        /// <summary>
        /// Review этап 7 (P1-3): квесты, принимаемые этим выбором (команда
        /// QuestStartRequestedEvent при SelectChoice). Нарратив «Конечно,
        /// помогу» теперь связан с контрактом StartQuest (раньше выбор только
        /// менял узел — квесты приходилось принимать вручную в окне квестов).
        /// </summary>
        public readonly List<string> QuestIdsToStart = new List<string>();
    }
}
