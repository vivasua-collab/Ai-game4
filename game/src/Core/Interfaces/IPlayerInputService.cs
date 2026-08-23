#nullable enable
// Создано: 2026-05-09 16:16:00 UTC
// Редактировано: 2026-05-24 15:58:33 UTC — Фаза 9B: InputFrameData вместо 8 параметров + LMB/RMB
// Интерфейс сервиса ввода игрока.
// Чистый C# — НЕ MonoBehaviour, получает ввод извне (от Unity InputSystem адаптера).
using CultivationGame.Core.Data;

using CultivationGame.Core;
namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Сервис ввода игрока.
    /// Получает состояние ввода извне и предоставляет его другим сервисам.
    /// НЕ зависит от Unity Input System напрямую — адаптер передаёт данные.
    ///
    /// Фаза 9B: UpdateInputState() принимает InputFrameData вместо 8+ параметров.
    /// Это решает проблему раздутой сигнатуры (8→16 параметров при добавлении мыши).
    /// InputFrameData — readonly struct, zero-alloc.
    /// </summary>
    public interface IPlayerInputService
    {
        /// <summary>Направление движения (нормализованный 2D вектор)</summary>
        Position2D MoveDirection { get; }

        /// <summary>Зажата ли клавиша бега</summary>
        bool RunHeld { get; }

        /// <summary>Нажата ли атака (J или ЛКМ, однократное)</summary>
        bool IsAttackPressed { get; }

        /// <summary>Нажата ли защита (K, однократное)</summary>
        bool IsDefendPressed { get; }

        /// <summary>Нажато ли взаимодействие (E, однократное)</summary>
        bool IsInteractPressed { get; }

        /// <summary>Нажата ли добыча (F, однократное) — сбор ресурсов/использование инструмента</summary>
        bool IsHarvestPressed { get; }

        /// <summary>Нажат ли инвентарь (I, однократное)</summary>
        bool IsInventoryPressed { get; }

        /// <summary>Нажат ли лист персонажа (C, однократное)</summary>
        bool IsCharacterSheetPressed { get; }

        /// <summary>Raw-флаг инвентаря, БЕЗ проверки InputDisabled (для toggle)</summary>
        bool IsInventoryPressedRaw { get; }

        /// <summary>Нажата ли медитация (M, однократное)</summary>
        bool IsMeditatePressed { get; }

        // === Этап 2 внедрения ЦИ (2026-08-23): техники ===

        /// <summary>Нажат ли каст техники (Z, однократное)</summary>
        bool IsCastTechniquePressed { get; }

        /// <summary>Нажат ли цикл выбора техники (X, однократное)</summary>
        bool IsCycleTechniquePressed { get; }

        /// <summary>Нажата ли панель техник (T, однократное)</summary>
        bool IsTechniquesPressed { get; }

        // === Ai-game3 compatibility: sticky flags ===

        /// <summary>Нажат ли pause (Esc, однократное). Ai-game3 compatibility.</summary>
        bool IsPausePressed { get; }

        /// <summary>Нажат ли quicksave (F5, однократное). Ai-game3 compatibility.</summary>
        bool IsQuickSavePressed { get; }

        /// <summary>Нажат ли quickload (F9, однократное). Ai-game3 compatibility.</summary>
        bool IsQuickLoadPressed { get; }

        /// <summary>Нажато ли увеличение скорости времени (+/PageUp, однократное).</summary>
        bool IsTimeSpeedUpPressed { get; }

        /// <summary>Нажато ли уменьшение скорости времени (-/PageDown, однократное).</summary>
        bool IsTimeSpeedDownPressed { get; }

        /// <summary>Текущий кадр ввода (raw). Ai-game3 compatibility — для Adapter'ов.</summary>
        InputFrameData CurrentFrame { get; }

        /// <summary>Выбранная техника (слот 1-9, 0 = не выбрана)</summary>
        int SelectedTechniqueSlot { get; }

        // === Фаза 9B: Мышь ===

        /// <summary>ЛКМ нажата (однократное, после проверки IsOverUI)</summary>
        bool IsLMBPressed { get; }

        /// <summary>ПКМ нажата — короткий клик (однократное)</summary>
        bool IsRMBPressed { get; }

        /// <summary>ПКМ удерживается (состояние, НЕ one-shot)</summary>
        bool IsRMBHeld { get; }

        /// <summary>ПКМ удержание ≥ 300мс (контекстное меню)</summary>
        bool IsRMBLongPress { get; }

        /// <summary>Мировая позиция мыши X (промилле: 1000 = 1.0 ед.)</summary>
        int MouseWorldX { get; }

        /// <summary>Мировая позиция мыши Y (промилле: 1000 = 1.0 ед.)</summary>
        int MouseWorldY { get; }

        /// <summary>Курсор над UI?</summary>
        bool IsMouseOverUI { get; }

        // === Общие ===

        /// <summary>Отключён ли ввод (UI, катсцена и т.д.)</summary>
        bool InputDisabled { get; set; }

        /// <summary>
        /// Обновить состояние ввода из InputFrameData.
        /// Вызывается каждый кадр из GameInputAdapter.
        /// Фаза 9B: заменяет старую 8-параметровую версию.
        /// </summary>
        void UpdateInputState(InputFrameData data);

        /// <summary>
        /// Сбросить одноразовые флаги (после обработки).
        /// Вызывается в конце кадра.
        /// </summary>
        void ResetFrameFlags();
    }
}
