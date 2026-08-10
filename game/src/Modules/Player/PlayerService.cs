#nullable enable
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Player;

/// <summary>
/// PlayerService — owns the player's CharacterData, position, facing.
/// V1 stub: single player instance.
/// </summary>
public sealed class PlayerService : IPlayerService
{
    private readonly CharacterData _player = new();
    private readonly PlayerConfig _config;
    private bool _spawned;

    public PlayerService(PlayerConfig? config = null) => _config = config ?? new PlayerConfig();

    public CharacterData Player => _player;
    public Position2D Position => _player.Position;
    public Direction Facing => _player.Facing;

    public void MoveTo(int x, int y)
    {
        var old = _player.Position;
        _player.Position = new Position2D(x, y);
        Console.WriteLine($"[PlayerService] Move {old} → ({x}, {y})");
    }

    public void SetFacing(Direction dir)
    {
        _player.Facing = dir;
    }

    public void Spawn(Position2D position)
    {
        _player.Id = "player";
        _player.Name = "Player";
        _player.Position = position;
        _player.Facing = _config.StartFacing;
        _player.CultivationLevel = 1;
        _player.CoreCapacity = _config.BaseMaxQi;
        _player.CurrentQi = 0;
        _player.Health = _config.BaseMaxHealth;
        _player.Age = 16;
        _spawned = true;
        Console.WriteLine($"[PlayerService] Player spawned @ {position}, hp {_player.Health}");
    }

    /// <summary>Internal — whether Spawn() has been called. Not on interface.</summary>
    public bool IsSpawned => _spawned;
}

/// <summary>
/// PlayerInputService — holds the current InputFrameData and sticky flags.
/// Adapter layer calls UpdateFrame() each physics frame; PlayerModule.Tick()
/// calls ResetFrameFlags() AFTER all consumers have read the frame.
/// </summary>
public sealed class PlayerInputService : IPlayerInputService
{
    private InputFrameData _frame = new(
        Vector2f.Zero, false, false, false, 0f, Vector2f.Zero, false, null,
        new HashSet<string>(), 0);

    // Sticky action flags — set by UpdateFrame based on StickyKeys, cleared by ResetFrameFlags
    private bool _interact;
    private bool _inventory;
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

    public InputFrameData CurrentFrame => _frame;

    public void UpdateFrame(InputFrameData frame)
    {
        _frame = frame;
        // Map sticky keys → action flags (V1 simple mapping)
        var keys = frame.StickyKeys;
        if (keys.Contains("interact")) _interact = true;
        if (keys.Contains("inventory") || keys.Contains("i")) _inventory = true;
        if (keys.Contains("rest") || keys.Contains("r")) _rest = true;
        if (keys.Contains("harvest") || keys.Contains("f")) _harvest = true;
        if (keys.Contains("special")) _specialAction = true;
        if (keys.Contains("pause") || keys.Contains("escape")) _pause = true;
        if (keys.Contains("f5")) _quickSave = true;
        if (keys.Contains("f9")) _quickLoad = true;
        if (keys.Contains("journal") || keys.Contains("j")) _journal = true;
        if (keys.Contains("techniques") || keys.Contains("k")) _techniques = true;
        if (keys.Contains("character") || keys.Contains("c")) _characterSheet = true;
        if (keys.Contains("quest") || keys.Contains("q")) _questLog = true;
        if (keys.Contains("map") || keys.Contains("m")) _map = true;
        if (keys.Contains("minimap") || keys.Contains("tab")) _minimap = true;
    }

    public void ResetFrameFlags()
    {
        _interact = _inventory = _rest = _harvest = _specialAction = false;
        _pause = _quickSave = _quickLoad = _journal = _techniques = false;
        _characterSheet = _questLog = _map = _minimap = false;
    }

    public bool IsInteractPressed => _interact;
    public bool IsInventoryPressed => _inventory;
    public bool IsRestPressed => _rest;
    public bool IsHarvestPressed => _harvest;
    public bool IsSpecialActionPressed => _specialAction;
    public bool IsPausePressed => _pause;
    public bool IsQuickSavePressed => _quickSave;
    public bool IsQuickLoadPressed => _quickLoad;
    public bool IsJournalPressed => _journal;
    public bool IsTechniquesPressed => _techniques;
    public bool IsCharacterSheetPressed => _characterSheet;
    public bool IsQuestLogPressed => _questLog;
    public bool IsMapPressed => _map;
    public bool IsMinimapPressed => _minimap;
}

/// <summary>
/// StatService — per-entity stat system. Base stats + additive bonuses.
/// V1 stub: stores base stats and bonuses in nested dictionaries.
/// </summary>
public sealed class StatService : IStatService
{
    private readonly Dictionary<int, Dictionary<StatType, float>> _baseStats = new();
    private readonly Dictionary<int, Dictionary<StatType, float>> _bonuses = new();

    public float GetStat(int entityId, StatType stat)
    {
        if (_baseStats.TryGetValue(entityId, out var dict) && dict.TryGetValue(stat, out var v))
            return v;
        return 0f;
    }

    public void AddBonus(int entityId, StatType stat, float value)
    {
        if (!_bonuses.TryGetValue(entityId, out var dict))
        {
            dict = new Dictionary<StatType, float>();
            _bonuses[entityId] = dict;
        }
        dict[stat] = (dict.TryGetValue(stat, out var cur) ? cur : 0f) + value;
    }

    public void RemoveBonus(int entityId, StatType stat, float value)
    {
        if (!_bonuses.TryGetValue(entityId, out var dict)) return;
        if (!dict.TryGetValue(stat, out var cur)) return;
        float newVal = cur - value;
        if (Math.Abs(newVal) < 1e-6f) dict.Remove(stat);
        else dict[stat] = newVal;
    }

    public float GetStatWithBonuses(int entityId, StatType stat)
    {
        float baseV = GetStat(entityId, stat);
        float bonus = 0f;
        if (_bonuses.TryGetValue(entityId, out var dict) && dict.TryGetValue(stat, out var b))
            bonus = b;
        return baseV + bonus;
    }

    /// <summary>Internal — set base stat. Not on interface.</summary>
    public void SetBaseStat(int entityId, StatType stat, float value)
    {
        if (!_baseStats.TryGetValue(entityId, out var dict))
        {
            dict = new Dictionary<StatType, float>();
            _baseStats[entityId] = dict;
        }
        dict[stat] = value;
    }
}
