#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CultivationGame.Modules.Save;

/// <summary>
/// SaveFileHandler — JSON file I/O for save slots.
/// V1 stub: uses System.Text.Json. Path is config-driven; falls back to a
/// platform-agnostic local "saves" directory.
/// </summary>
public sealed class SaveFileHandler
{
    private readonly SaveConfig _config;

    public SaveFileHandler(SaveConfig? config = null) => _config = config ?? new SaveConfig();

    private string ResolveDir()
    {
        string raw = _config.SaveDirectory.Replace("user://", "").Replace("res://", "");
        if (string.IsNullOrWhiteSpace(raw)) raw = "saves";
        if (!Path.IsPathRooted(raw))
        {
            raw = Path.Combine(AppContext.BaseDirectory, raw);
        }
        Directory.CreateDirectory(raw);
        return raw;
    }

    private string SlotPath(string slotName) =>
        Path.Combine(ResolveDir(), slotName + ".json");

    public bool Save(string slotName, Dictionary<string, object> data)
    {
        try
        {
            var path = SlotPath(slotName);
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(path, json);
            Console.WriteLine($"[SaveFileHandler] Wrote {path} ({data.Count} sections)");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SaveFileHandler] Save FAILED for '{slotName}': {ex.Message}");
            return false;
        }
    }

    public Dictionary<string, object>? Load(string slotName)
    {
        try
        {
            var path = SlotPath(slotName);
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            Console.WriteLine($"[SaveFileHandler] Read {path} ({data?.Count ?? 0} sections)");
            return data;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SaveFileHandler] Load FAILED for '{slotName}': {ex.Message}");
            return null;
        }
    }

    public bool HasSave(string slotName) => File.Exists(SlotPath(slotName));

    public bool DeleteSave(string slotName)
    {
        var path = SlotPath(slotName);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    public IReadOnlyList<string> GetAllSaves()
    {
        var list = new List<string>();
        var dir = ResolveDir();
        if (!Directory.Exists(dir)) return list;
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            list.Add(Path.GetFileNameWithoutExtension(file));
        }
        return list;
    }
}
