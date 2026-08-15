#nullable enable
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Save;

/// <summary>
/// SaveService — implements <see cref="ISaveService"/>. Owns a
/// SaveDataAggregator that collects state from all registered ISaveable
/// services. SaveService itself implements ISaveable for its own metadata.
/// </summary>
public sealed class SaveService : ISaveService, ISaveable
{
    [Inject] private readonly SaveDataAggregator _aggregator = null!;
    private SaveConfig _config = new();

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

    public bool Save(SaveSlot slot)
    {
        bool ok = _aggregator.Save(slot.Name);
        OnSaveCompleted?.Invoke(ok, slot.Name);
        Console.WriteLine($"[SaveService] Save('{slot}') → {(ok ? "OK" : "FAILED")}");
        return ok;
    }

    public bool Load(SaveSlot slot)
    {
        bool ok = _aggregator.Load(slot.Name);
        OnLoadCompleted?.Invoke(ok, slot.Name);
        Console.WriteLine($"[SaveService] Load('{slot}') → {(ok ? "OK" : "FAILED")}");
        return ok;
    }

    public bool HasSave(SaveSlot slot) => _aggregator.HasSave(slot.Name);

    public bool DeleteSave(SaveSlot slot)
    {
        _aggregator.DeleteSave(slot.Name);
        Console.WriteLine($"[SaveService] DeleteSave('{slot}')");
        return true;
    }

    public IReadOnlyList<SaveInfo> GetAllSaves()
    {
        var names = _aggregator.GetAllSaves();
        var list = new List<SaveInfo>(names.Count);
        foreach (var n in names)
        {
            list.Add(new SaveInfo(new SaveSlot(n), n, 0L, 0L, 0, string.Empty));
        }
        return list;
    }

    /// <summary>
    /// Register an ISaveable service. NOT on the ISaveService interface —
    /// callers must cast to SaveService (concrete) to call this.
    /// </summary>
    public void RegisterSaveable(ISaveable saveable) => _aggregator.Register(saveable);
}
