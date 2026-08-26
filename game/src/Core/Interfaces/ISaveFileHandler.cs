#nullable enable
using System.Collections.Generic;

namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Engine-agnostic interface for save file I/O.
    ///
    /// Lives in Core so that <c>Modules.Save.SaveDataAggregator</c> can depend
    /// on it without taking a Godot reference. The concrete implementation is
    /// registered in the Adapter layer (Godot-aware) for production use and
    /// in the Modules layer (AppContext.BaseDirectory) for headless tests.
    ///
    /// API: flat-file layout — one JSON file per save slot
    /// (<c>{saveRoot}/{slotName}.json</c>).
    ///
    /// See audit issue #6 (08_15_code_audit.md).
    /// </summary>
    public interface ISaveFileHandler
    {
        /// <summary>
        /// Serialise <paramref name="data"/> to JSON and write it to
        /// <c>{saveRoot}/{slotName}.json</c>. Returns false on I/O error.
        /// </summary>
        bool Save(string slotName, Dictionary<string, object> data);

        /// <summary>
        /// Read and deserialise JSON from <c>{saveRoot}/{slotName}.json</c>.
        /// Returns null if the file does not exist or fails to parse.
        /// </summary>
        Dictionary<string, object>? Load(string slotName);

        /// <summary>True if a save file exists for <paramref name="slotName"/>.</summary>
        bool HasSave(string slotName);

        /// <summary>Delete the save file for <paramref name="slotName"/>. Returns false if missing.</summary>
        bool DeleteSave(string slotName);

        /// <summary>List all save slot names (file stems, no extension).</summary>
        IReadOnlyList<string> GetAllSaves();
    }
}
