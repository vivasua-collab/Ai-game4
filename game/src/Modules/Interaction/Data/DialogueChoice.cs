#nullable enable
// Создано: 2026-05-09 — Phase 13: модель выбора в диалоге
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
    }
}
