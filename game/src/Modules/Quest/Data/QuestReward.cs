// Создано: 2026-05-09 — Phase 12: модель награды за квест
using CultivationGame.Core;
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.Quest.Data
{
    /// <summary>
    /// Награда за квест.
    /// Типы:
    /// - Item: TargetId = ItemId, Amount = количество
    /// - Qi: Amount = количество Ци
    /// - Experience: Amount = количество опыта (будущее)
    /// - Technique: TargetId = TechniqueId
    /// - FactionRep: TargetId = FactionId, Amount = изменение репутации
    /// </summary>
    public class QuestReward
    {
        /// <summary>Уникальный идентификатор награды</summary>
        public string RewardId;

        /// <summary>Тип награды</summary>
        public QuestRewardType Type;

        /// <summary>
        /// Идентификатор цели (ItemId, TechniqueId, FactionId).
        /// Для Qi и Experience — не используется.
        /// </summary>
        public string TargetId;

        /// <summary>Количество (предметов, Ци, опыта, репутации)</summary>
        public int Amount;
    }
}
