#nullable enable
// Создано: 2026-05-08 10:07:00 UTC
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Save service — owns the save/load pipeline and orchestrates the
    /// <see cref="ISaveable"/> aggregator. All operations take a
    /// <see cref="SaveSlot"/> value (name + type).
    /// </summary>
    public interface ISaveService
    {
        bool Save(SaveSlot slot);
        bool Load(SaveSlot slot);
        bool HasSave(SaveSlot slot);
        bool DeleteSave(SaveSlot slot);
        IReadOnlyList<SaveInfo> GetAllSaves();
    }
}
