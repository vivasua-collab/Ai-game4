#nullable enable
namespace CultivationGame.Core.Messaging.Contracts;

// Created: 2026-05-19 (Ai-game3, InputLogContracts) + 2026-05-24 (MouseContracts) — merged & migrated 2026-08-15.
// Input logging + mouse click-to-move / context-menu / tracking contracts.
// Merged from Ai-game3 InputLogContracts.cs + MouseContracts.cs per task spec.

// ============================================================================
// INPUT LOG CONTRACTS (keyboard logging for InputLogPanel)
// ============================================================================

/// <summary>
/// Тип события клавиши.
/// </summary>
public enum InputKeyEventType
{
    /// <summary>Однократное нажатие (wasPressedThisFrame)</summary>
    Pressed,
    /// <summary>Удержание (isPressed)</summary>
    Held
}

/// <summary>
/// Событие: нажата клавиша ввода.
/// Публикуется GameInputAdapter при обнаружении нажатия/удержания клавиши.
/// InputLogPanel подписывается и отображает в реальном времени.
/// </summary>
public readonly struct InputKeyEvent
{
    /// <summary>Название клавиши (W, A, S, D, I, J, K, E, M, F5, F9, Escape, Shift)</summary>
    public readonly string KeyName;

    /// <summary>Тип события: Pressed (однократное) или Held (удержание)</summary>
    public readonly InputKeyEventType EventType;

    /// <summary>Направление движения (для WASD) — null для других клавиш</summary>
    public readonly string Direction;

    /// <summary>Метка времени (frameCount)</summary>
    public readonly int Frame;

    public InputKeyEvent(string keyName, InputKeyEventType eventType, string direction, int frame)
    {
        KeyName = keyName;
        EventType = eventType;
        Direction = direction;
        Frame = frame;
    }
}

/// <summary>
/// Событие: выполнено игровое действие в ответ на ввод.
/// Публикуется PlayerModule, UIService и др. при обработке ввода.
/// </summary>
public readonly struct InputActionEvent
{
    /// <summary>Название действия (Move, ToggleInventory, Attack, Defend, Interact, Meditate, Save, Load, Pause)</summary>
    public readonly string ActionName;

    /// <summary>Описание результата (направление, состояние UI и т.д.)</summary>
    public readonly string Description;

    /// <summary>Метка времени (frameCount)</summary>
    public readonly int Frame;

    public InputActionEvent(string actionName, string description, int frame)
    {
        ActionName = actionName;
        Description = description;
        Frame = frame;
    }
}

// ============================================================================
// MOUSE CONTRACTS (LMB/RMB click-to-move, context menu, tracking)
// ============================================================================

/// <summary>
/// Кнопка мыши — определяет какая кнопка нажата.
/// </summary>
public enum MouseButton
{
    None = 0,
    Left = 1,
    Right = 2
}

/// <summary>
/// Событие ввода мыши.
/// Публикуется GameInputAdapter при нажатии ЛКМ/ПКМ.
/// Позиция в мировых координатах — int промилле (ЗАПРЕТ 3.9).
/// </summary>
public readonly struct MouseInputEvent
{
    /// <summary>Какая кнопка нажата</summary>
    public readonly MouseButton Button;

    /// <summary>Мировая позиция клика (промилле: 1000 = 1.0 ед. мира)</summary>
    public readonly int WorldX;

    /// <summary>Мировая позиция клика (промилле: 1000 = 1.0 ед. мира)</summary>
    public readonly int WorldY;

    /// <summary>Курсор над UI-элементом?</summary>
    public readonly bool IsOverUI;

    public MouseInputEvent(MouseButton button, int worldX, int worldY, bool isOverUI)
    {
        Button = button;
        WorldX = worldX;
        WorldY = worldY;
        IsOverUI = isOverUI;
    }
}

/// <summary>
/// Событие клика-перемещения.
/// Публикуется ClickIntentResolver когда ЛКМ клик на пустое место.
/// Игрок перемещается к точке. WASD отменяет.
/// </summary>
public readonly struct ClickToMoveEvent
{
    /// <summary>Целевая позиция X (промилле: 1000 = 1.0 ед. мира)</summary>
    public readonly int TargetX;

    /// <summary>Целевая позиция Y (промилле: 1000 = 1.0 ед. мира)</summary>
    public readonly int TargetY;

    public ClickToMoveEvent(int targetX, int targetY)
    {
        TargetX = targetX;
        TargetY = targetY;
    }
}

/// <summary>
/// Событие запроса контекстного меню.
/// Публикуется GameInputAdapter при RMB удержание ≥ 300мс.
/// </summary>
public readonly struct ContextMenuRequestedEvent
{
    /// <summary>Мировая позиция X (промилле)</summary>
    public readonly int WorldX;

    /// <summary>Мировая позиция Y (промилле)</summary>
    public readonly int WorldY;

    public ContextMenuRequestedEvent(int worldX, int worldY)
    {
        WorldX = worldX;
        WorldY = worldY;
    }
}

/// <summary>
/// Событие трекинга цели.
/// Публикуется TrackingService при RMB короткий клик.
/// </summary>
public readonly struct TrackingTargetEvent
{
    /// <summary>ID цели (null если сброс)</summary>
    public readonly string TargetId;

    public TrackingTargetEvent(string targetId)
    {
        TargetId = targetId;
    }
}
