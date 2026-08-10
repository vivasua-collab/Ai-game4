#nullable enable
namespace CultivationGame.Core.Interfaces;

/// <summary>UI orchestration (engine-agnostic). Adapter renders the views.</summary>
public interface IUIService
{
    void ShowView(string viewId);
    void HideView(string viewId);
    void HideAllViews();
    bool IsViewVisible(string viewId);

    void ShowNotification(string message, float duration = 3f);
    void ShowTooltip(string text, float x, float y);
    void HideTooltip();
}
