#nullable enable
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Save;

/// <summary>
/// SaveService — implements ISaveService. Owns a SaveDataAggregator that
/// collects state from all registered ISaveable services. SaveService itself
/// implements ISaveable for its own metadata block.
/// </summary>
public sealed class SaveService : ISaveService, ISaveable
{
    private readonly SaveDataAggregator _aggregator;
    private readonly SaveConfig _config;

    public SaveService(SaveDataAggregator aggregator, SaveConfig? config = null)
    {
        _aggregator = aggregator ?? throw new ArgumentNullException(nameof(aggregator));
        _config = config ?? new SaveConfig();
        // Make sure the aggregator has us (meta) registered.
        _aggregator.Register(this);
    }

    // ISaveable
    public string SaveKey => "save_meta";

    public object CaptureState()
    {
        return new { savedAt = DateTime.UtcNow.Ticks, version = 1 };
    }

    public void RestoreState(object state)
    {
        // Nothing to restore for the meta-block in V1.
    }

    // ISaveService
    public event Action<bool, string>? OnSaveCompleted;
    public event Action<bool, string>? OnLoadCompleted;

    public void Save(string slotName, SaveSlotType slotType)
    {
        bool ok = _aggregator.Save(slotName);
        OnSaveCompleted?.Invoke(ok, slotName);
        Console.WriteLine($"[SaveService] Save('{slotName}', {slotType}) → {(ok ? "OK" : "FAILED")}");
    }

    public void Load(string slotName)
    {
        bool ok = _aggregator.Load(slotName);
        OnLoadCompleted?.Invoke(ok, slotName);
        Console.WriteLine($"[SaveService] Load('{slotName}') → {(ok ? "OK" : "FAILED")}");
    }

    public bool HasSave(string slotName) => _aggregator.HasSave(slotName);

    public void DeleteSave(string slotName)
    {
        _aggregator.DeleteSave(slotName);
        Console.WriteLine($"[SaveService] DeleteSave('{slotName}')");
    }

    public IReadOnlyList<string> GetAllSaves() => _aggregator.GetAllSaves();

    /// <summary>
    /// Register an ISaveable service. NOT on the ISaveService interface —
    /// callers must cast to SaveService (concrete) to call this.
    /// </summary>
    public void RegisterSaveable(ISaveable saveable) => _aggregator.Register(saveable);
}
