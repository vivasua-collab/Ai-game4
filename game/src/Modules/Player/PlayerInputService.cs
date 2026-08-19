#nullable enable
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Player;

/// <summary>
/// PlayerInputService — holds the current InputFrameData snapshot and exposes
/// it via the <see cref="IPlayerInputService"/> interface. Sticky action
/// flags (one-shot keys) are tracked here and cleared by <see cref="ResetFrameFlags"/>.
///
/// The Adapter layer calls <see cref="UpdateInputState"/> each physics frame;
/// <see cref="PlayerModule"/> calls <see cref="ResetFrameFlags"/> AFTER all
/// consumers have read the frame (PLR-E06).
/// </summary>
public sealed class PlayerInputService : IPlayerInputService
{
    private InputFrameData _frame;
    private bool _interact;
    private bool _inventory;
    private bool _inventoryRaw;
    private bool _rest;
    private bool _harvest;
    private bool _specialAction;
    private bool _pause;
    private bool _quickSave;
    private bool _quickLoad;
    private bool _journal;
    private bool _techniques;
    private bool _characterSheet;
    private bool _questLog;
    private bool _map;
    private bool _minimap;
    private bool _meditate;
    private bool _attack;
    private bool _defend;
    private bool _timeSpeedUp;
    private bool _timeSpeedDown;
    private int _selectedSlot;

    /// <summary>
    /// Internal — the raw underlying frame. Not part of the interface;
    /// consumers should use the typed properties on IPlayerInputService.
    /// </summary>
    public InputFrameData CurrentFrame => _frame;

    // === IPlayerInputService ===

    public Position2D MoveDirection => new Position2D(
        (int)(_frame.MoveDirection.X * 1000),
        (int)(_frame.MoveDirection.Y * 1000));

    public bool RunHeld => _frame.IsRun;
    public bool IsAttackPressed => _attack;
    public bool IsDefendPressed => _defend;
    public bool IsInteractPressed => _interact;
    public bool IsHarvestPressed => _harvest && !InputDisabled;
    public bool IsInventoryPressed => _inventory && !InputDisabled;
    public bool IsInventoryPressedRaw => _inventoryRaw;
    public bool IsMeditatePressed => _meditate;

    // === Ai-game3 compatibility: sticky flags ===
    public bool IsPausePressed => _pause && !InputDisabled;
    public bool IsQuickSavePressed => _quickSave && !InputDisabled;
    public bool IsQuickLoadPressed => _quickLoad && !InputDisabled;
    public bool IsTimeSpeedUpPressed => _timeSpeedUp && !InputDisabled;
    public bool IsTimeSpeedDownPressed => _timeSpeedDown && !InputDisabled;

    public int SelectedTechniqueSlot => _selectedSlot;

    // Mouse state
    public bool IsLMBPressed => _frame.IsLmbPressed && !InputDisabled;
    public bool IsRMBPressed => _frame.IsRmbPressed;
    public bool IsRMBHeld => _frame.RmbHoldDuration > 0f;
    public bool IsRMBLongPress => _frame.RmbHoldDuration >= 0.3f;
    public int MouseWorldX => (int)(_frame.MouseWorldPos.X * 1000);
    public int MouseWorldY => (int)(_frame.MouseWorldPos.Y * 1000);
    public bool IsMouseOverUI => _frame.IsOverUI;

    public bool InputDisabled { get; set; }

    public void UpdateInputState(InputFrameData data)
    {
        _frame = data;
        _inventoryRaw = data.IsSticky("i") || data.IsSticky("inventory");
        if (!InputDisabled)
        {
            if (_inventoryRaw) _inventory = true;
            if (data.IsSticky("interact")) _interact = true;
            if (data.IsSticky("rest")) _rest = true;
            if (data.IsSticky("harvest")) _harvest = true;
            if (data.IsSticky("special_action")) _specialAction = true;
            if (data.IsSticky("escape") || data.IsSticky("pause")) _pause = true;
            if (data.IsSticky("save")) _quickSave = true;
            if (data.IsSticky("load")) _quickLoad = true;
            if (data.IsSticky("journal")) _journal = true;
            if (data.IsSticky("techniques")) _techniques = true;
            if (data.IsSticky("character_sheet")) _characterSheet = true;
            if (data.IsSticky("quest_log")) _questLog = true;
            if (data.IsSticky("world_map")) _map = true;
            if (data.IsSticky("minimap")) _minimap = true;
            if (data.IsSticky("minimap")) _meditate = true;
            if (data.IsSticky("attack")) _attack = true;
            if (data.IsSticky("defend")) _defend = true;
            if (data.IsSticky("time_speed_up")) _timeSpeedUp = true;
            if (data.IsSticky("time_speed_down")) _timeSpeedDown = true;
        }
        if (data.HotbarSlot is int slot && slot > 0) _selectedSlot = slot;
    }

    public void ResetFrameFlags()
    {
        _interact = _inventory = _rest = _harvest = _specialAction = false;
        _pause = _quickSave = _quickLoad = _journal = _techniques = false;
        _characterSheet = _questLog = _map = _minimap = false;
        _meditate = _attack = _defend = false;
        _timeSpeedUp = _timeSpeedDown = false;
        _inventoryRaw = false;
        _selectedSlot = 0;
    }
}
