#nullable enable
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;
// Создано: 2026-05-09 — Phase 14: интерфейс UI-сервиса
// Редактировано: 2026-08-15 — расширены ShowView/HideView для Ai-game4 Phase wiring.
namespace CultivationGame.Core.Interfaces
{
    public interface IUIService
    {
        GameState CurrentUIState { get; }
        void SetUIState(GameState state);
        void ShowToast(string message);
        void ShowModal(string title, string message);

        /// <summary>Показать вид по идентификатору (HUD, Inventory, Dialogue, ...).</summary>
        void ShowView(string viewId);

        /// <summary>Скрыть вид по идентификатору.</summary>
        void HideView(string viewId);

        /// <summary>Скрыть все виды.</summary>
        void HideAllViews();

        /// <summary>Открыт ли вид с указанным идентификатором.</summary>
        bool IsViewVisible(string viewId);
    }
}
