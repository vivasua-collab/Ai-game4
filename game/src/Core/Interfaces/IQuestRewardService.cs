#nullable enable
// Создано: 2026-05-09 — Phase 12: интерфейс службы наград за квесты
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;
namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Служба выдачи наград за квесты.
    /// АРХИТЕКТУРА (EVT-01): НЕ инжектит IInventoryService, IQiService напрямую.
    /// Публикует command-события: ItemAddRequestEvent, QiAddRequestEvent.
    /// </summary>
    public interface IQuestRewardService
    {
        /// <summary>Выдать награды за квест</summary>
        bool GrantRewards(string questId);

        /// <summary>Были ли награды уже выданы за квест</summary>
        bool AreRewardsGranted(string questId);
    }
}
