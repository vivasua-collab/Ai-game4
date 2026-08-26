#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Messaging.Contracts;

// Created: 2026-05-09 (Ai-game3) — migrated 2026-08-15.
// UI request/event contracts: state changes, interactions, toasts, modals.
// UI requests are published by UI, subscribed by other modules.
// UI answers are published by modules, subscribed by UI for display.

// === UI ЗАПРОСЫ (от UI к другим модулям) ===

/// <summary>
/// Запрос смены экрана UI
/// </summary>
public readonly struct UIStateChangeRequestEvent
{
    public readonly GameState TargetState;
    public UIStateChangeRequestEvent(GameState targetState)
        { TargetState = targetState; }
}

/// <summary>
/// Запрос взаимодействия (кнопка "E" / клик)
/// </summary>
public readonly struct UIInteractRequestEvent { }

/// <summary>
/// Запрос продвижения диалога (клик / пробел)
/// </summary>
public readonly struct UIAdvanceDialogueRequestEvent { }

/// <summary>
/// Запрос выбора в диалоге
/// </summary>
public readonly struct UISelectChoiceRequestEvent
{
    public readonly int ChoiceIndex;
    public UISelectChoiceRequestEvent(int choiceIndex)
        { ChoiceIndex = choiceIndex; }
}

/// <summary>
/// Запрос сохранения
/// </summary>
public readonly struct UISaveRequestEvent
{
    public readonly int SlotIndex;
    public UISaveRequestEvent(int slotIndex) { SlotIndex = slotIndex; }
}

/// <summary>
/// Запрос загрузки
/// </summary>
public readonly struct UILoadRequestEvent
{
    public readonly int SlotIndex;
    public UILoadRequestEvent(int slotIndex) { SlotIndex = slotIndex; }
}

/// <summary>
/// Запрос паузы
/// </summary>
public readonly struct UIPauseRequestEvent { }

/// <summary>
/// Запрос продолжения
/// </summary>
public readonly struct UIResumeRequestEvent { }

// === UI ОТВЕТЫ (для отображения) ===

/// <summary>
/// Показать уведомление (toast)
/// </summary>
public readonly struct ToastShownEvent
{
    public readonly string Message;
    public readonly float Duration;
    public ToastShownEvent(string message, float duration)
        { Message = message; Duration = duration; }
}

/// <summary>
/// Показать модальное окно
/// </summary>
public readonly struct ModalShownEvent
{
    public readonly string Title;
    public readonly string Message;
    public ModalShownEvent(string title, string message)
        { Title = title; Message = message; }
}

// === CULTIVATION WINDOW (этап C, 2026-08-26) ===
// Окно Культивации Ци — отдельное окно (как инвентарь), открывается клавишей K.
// Содержит вкладки: Техники / Меридианы / Ядро + панель слотов техник (3-9).

/// <summary>
/// Запрос: переключить окно Культивации (открыть/закрыть).
/// Публикуется Adapter (клавиша K). Подписан CultivationWindow.
/// Open=true → показать окно; Open=false → скрыть.
/// </summary>
public readonly struct CultivationWindowToggleRequestedEvent
{
    public readonly bool Open;
    public CultivationWindowToggleRequestedEvent(bool open) { Open = open; }
}

/// <summary>
/// Событие: техника установлена в слот быстрого доступа (3-9).
/// Публикуется CultivationWindow при установке/переназначении.
/// Подписаны: TechniqueSlotService (обновление состояния), HotbarPanel (UI).
/// </summary>
public readonly struct TechniqueSlotAssignedEvent
{
    public readonly int SlotIndex;       // 3..9
    public readonly string TechniqueId;  // пусто = слот очищен
    public TechniqueSlotAssignedEvent(int slotIndex, string techniqueId)
        { SlotIndex = slotIndex; TechniqueId = techniqueId ?? string.Empty; }
}

/// <summary>
/// Событие: слот быстрого доступа техник очищен.
/// Публикуется CultivationWindow при снятии техники со слота.
/// </summary>
public readonly struct TechniqueSlotClearedEvent
{
    public readonly int SlotIndex;
    public TechniqueSlotClearedEvent(int slotIndex) { SlotIndex = slotIndex; }
}
