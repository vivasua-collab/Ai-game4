#nullable enable
using System;
using System.Collections.Generic;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Save;

/// <summary>
/// SaveDataAggregator — collects state from all registered ISaveable services
/// when SaveRequestedEvent fires, and restores it on LoadRequestedEvent.
/// V1 stub: just iterates registered saveables and calls CaptureState/RestoreState.
///
/// Depends on <see cref="ISaveFileHandler"/> (interface) so the Adapter layer
/// can swap in a Godot-aware file handler without breaking the Modules layer
/// (audit issue #6).
/// </summary>
public sealed class SaveDataAggregator
{
    private readonly List<ISaveable> _saveables = new();
    private readonly ISaveFileHandler _fileHandler;

    public SaveDataAggregator(ISaveFileHandler fileHandler)
    {
        _fileHandler = fileHandler ?? throw new ArgumentNullException(nameof(fileHandler));
    }

    public void Register(ISaveable saveable)
    {
        if (saveable == null) return;
        if (!_saveables.Contains(saveable)) _saveables.Add(saveable);
    }

    public bool Save(string slotName)
    {
        var dict = new Dictionary<string, object>(_saveables.Count);
        foreach (var s in _saveables)
        {
            try
            {
                dict[s.SaveKey] = s.CaptureState() ?? new object();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SaveDataAggregator] CaptureState failed for '{s.SaveKey}': {ex.Message}");
                dict[s.SaveKey] = new { error = ex.Message };
            }
        }
        return _fileHandler.Save(slotName, dict);
    }

    public bool Load(string slotName)
    {
        var dict = _fileHandler.Load(slotName);
        if (dict == null) return false;
        foreach (var s in _saveables)
        {
            if (!dict.TryGetValue(s.SaveKey, out var state)) continue;
            try
            {
                s.RestoreState(state);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SaveDataAggregator] RestoreState failed for '{s.SaveKey}': {ex.Message}");
            }
        }
        return true;
    }

    public bool HasSave(string slotName) => _fileHandler.HasSave(slotName);
    public bool DeleteSave(string slotName) => _fileHandler.DeleteSave(slotName);
    public IReadOnlyList<string> GetAllSaves() => _fileHandler.GetAllSaves();
}
