#nullable enable
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Interfaces;

/// <summary>Save / load orchestration. Adapter layer performs the actual I/O.</summary>
public interface ISaveService
{
    void Save(string slotName, SaveSlotType slotType);
    void Load(string slotName);
    bool HasSave(string slotName);
    void DeleteSave(string slotName);
    IReadOnlyList<string> GetAllSaves();

    event Action<bool, string>? OnSaveCompleted;
    event Action<bool, string>? OnLoadCompleted;
}
