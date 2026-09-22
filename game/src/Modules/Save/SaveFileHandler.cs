#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Save;

/// <summary>
/// SaveFileHandler — JSON file I/O for save slots.
/// V1 stub: uses System.Text.Json. Path is config-driven; falls back to a
/// platform-agnostic local "saves" directory.
///
/// Implements <see cref="ISaveFileHandler"/> so the engine-agnostic
/// <see cref="SaveDataAggregator"/> can depend on the interface rather than
/// the concrete type. In production the Adapter layer registers
/// <c>Adapter.Persistence.SaveFileHandler</c> (Godot-aware) as the
/// <see cref="ISaveFileHandler"/>; this Modules-layer implementation is the
/// fallback for headless tests where Godot is unavailable.
/// </summary>
public sealed class SaveFileHandler : ISaveFileHandler
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

    /// <summary>
    /// Санация имени слота (P1-8, аудит 09.22): имя слота не должно
    /// определять произвольный filesystem-путь — '../x' выталкивал запись
    /// ЗА пределы каталога сейвов (runtime-подтверждено: файл создан в
    /// родителе). Правила — ПАРИТЕТ с Adapter.Persistence.SaveFileHandler
    /// .SanitizeSlotName: только буквы/цифры/дефис/подчёркивание, остальное
    /// вырезается; пустой результат → "unnamed". Разделителей путей в
    /// whitelist нет — traversal невозможен по построению.
    /// </summary>
    private static string SanitizeSlotName(string slotName)
    {
        var sb = new System.Text.StringBuilder(slotName.Length);
        foreach (char c in slotName)
        {
            if (char.IsLetterOrDigit(c) || c == '-' || c == '_') sb.Append(c);
        }
        var clean = sb.ToString();
        return string.IsNullOrEmpty(clean) ? "unnamed" : clean;
    }

    private string SlotPath(string slotName) =>
        Path.Combine(ResolveDir(), SanitizeSlotName(slotName) + ".json");

    public bool Save(string slotName, Dictionary<string, object> data)
    {
        try
        {
            var path = SlotPath(slotName);
            // R11 P0-Save (review): единые опции конвейера — IncludeFields
            // (поля XxxSaveData!), CamelCase, case-insensitive чтение.
            var json = JsonSerializer.Serialize(data, SaveJson.Options);
            // R17 (аудит-0911 SAV-1): АТОМАРНАЯ запись — tmp → rename.
            // Раньше File.WriteAllText обрывался на краше/питании →
            // усечённый JSON → слот потерян без восстановления (дока
            // SAVE_SYSTEM §9.1 обещала tmp+rename с самого R11).
            var tmpPath = path + ".tmp";
            File.WriteAllText(tmpPath, json);
            File.Move(tmpPath, path, overwrite: true);
            Console.WriteLine($"[SaveFileHandler] Wrote {path} ({data.Count} sections, atomic tmp→rename)");
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
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json, SaveJson.Options);
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
        // R17 (аудит-0911 SAV-7): IOException наружу — краш вызывающего.
        try
        {
            var path = SlotPath(slotName);
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SaveFileHandler] DeleteSave FAILED for '{slotName}': {ex.Message}");
            return false;
        }
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
