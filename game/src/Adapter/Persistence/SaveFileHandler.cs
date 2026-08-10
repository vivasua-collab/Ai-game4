#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

namespace CultivationGame.Adapter.Persistence;

/// <summary>
/// JSON file I/O for game saves. Lives in the Adapter layer (engine-specific
/// only because it calls <see cref="ProjectSettings.GlobalizePath"/> to resolve
/// the res:// path to a real filesystem path).
///
/// Save layout (per docs_v2/05_data/SAVE_SYSTEM.md):
///   {saveRoot}/
///     {slotName}/
///       main.sav            ← aggregated game state (JSON)
///       metadata.sav        ← slot metadata (timestamp, version, character name)
///       chunks/             ← per-chunk tile data
///       locations/          ← per-location data
///
/// The handler is intentionally minimal: it just (de)serialises objects to disk.
/// All gameplay-state aggregation is the responsibility of the engine-agnostic
/// SaveDataAggregator in CultivationGame.Modules.Save.
/// </summary>
public sealed class SaveFileHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _saveRoot;

    public SaveFileHandler()
    {
        _saveRoot = ProjectSettings.GlobalizePath("res://saves");
        Directory.CreateDirectory(_saveRoot);
    }

    /// <summary>Override constructor — used in tests to point at a temp dir.</summary>
    public SaveFileHandler(string saveRoot)
    {
        _saveRoot = saveRoot;
        Directory.CreateDirectory(_saveRoot);
    }

    public string SaveRoot => _saveRoot;

    /// <summary>
    /// Serialise <paramref name="data"/> to JSON and write it to
    /// <c>{saveRoot}/{slotName}/{fileName}</c>. Creates directories as needed.
    /// </summary>
    public void Write(string slotName, string fileName, object data)
    {
        if (string.IsNullOrEmpty(slotName)) throw new ArgumentException("slotName is required", nameof(slotName));
        if (string.IsNullOrEmpty(fileName)) throw new ArgumentException("fileName is required", nameof(fileName));

        var dir = Path.Combine(_saveRoot, SanitizeSlotName(slotName));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, SanitizeFileName(fileName));

        var json = JsonSerializer.Serialize(data, data.GetType(), JsonOptions);
        File.WriteAllText(path, json);

        GD.Print($"[SaveFileHandler] Written {path} ({json.Length} bytes)");
    }

    /// <summary>
    /// Read and deserialise JSON from <c>{saveRoot}/{slotName}/{fileName}</c>.
    /// Returns <c>default(T)</c> if the file does not exist.
    /// </summary>
    public T? Read<T>(string slotName, string fileName)
    {
        var path = Path.Combine(_saveRoot, SanitizeSlotName(slotName), SanitizeFileName(fileName));
        if (!File.Exists(path)) return default;

        var json = File.ReadAllText(path);
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            GD.PrintErr($"[SaveFileHandler] Failed to parse {path}: {ex.Message}");
            return default;
        }
    }

    /// <summary>Read raw JSON text (no deserialisation). Returns null if file missing.</summary>
    public string? ReadText(string slotName, string fileName)
    {
        var path = Path.Combine(_saveRoot, SanitizeSlotName(slotName), SanitizeFileName(fileName));
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <summary>Check whether a save slot directory exists.</summary>
    public bool Exists(string slotName)
    {
        return Directory.Exists(Path.Combine(_saveRoot, SanitizeSlotName(slotName)));
    }

    /// <summary>Delete an entire save slot directory (recursive).</summary>
    public void Delete(string slotName)
    {
        var dir = Path.Combine(_saveRoot, SanitizeSlotName(slotName));
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
            GD.Print($"[SaveFileHandler] Deleted slot {slotName}");
        }
    }

    /// <summary>List all save slot names (top-level directories under saveRoot).</summary>
    public IReadOnlyList<string> GetAllSlots()
    {
        if (!Directory.Exists(_saveRoot)) return Array.Empty<string>();
        return Directory
            .GetDirectories(_saveRoot)
            .Select(Path.GetFileName)
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .ToList();
    }

    /// <summary>List all files inside a given save slot.</summary>
    public IReadOnlyList<string> ListSlotFiles(string slotName)
    {
        var dir = Path.Combine(_saveRoot, SanitizeSlotName(slotName));
        if (!Directory.Exists(dir)) return Array.Empty<string>();
        return Directory
            .GetFiles(dir, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetFileName(p))
            .ToList();
    }

    // ---- Path sanitisation (defence against path traversal) ----

    private static string SanitizeSlotName(string slotName)
    {
        // Allow only alphanumerics, dash, underscore — strip everything else.
        var clean = new string(slotName.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
        if (string.IsNullOrEmpty(clean)) clean = "unnamed";
        return clean;
    }

    private static string SanitizeFileName(string fileName)
    {
        // Strip directory separators / parent refs.
        var clean = fileName.Replace('/', '_').Replace('\\', '_').Replace("..", "_");
        if (!clean.EndsWith(".sav", StringComparison.OrdinalIgnoreCase) &&
            !clean.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            clean += ".sav";
        }
        return clean;
    }
}
