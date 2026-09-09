#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Save;

/// <summary>
/// SaveDataAggregator — collects state from all registered ISaveable services
/// when SaveRequestedEvent fires, and restores it on LoadRequestedEvent.
///
/// R11 P0-Save/P1 (внешнее ревью 2026-09-09) — контракт persistence изменён:
/// <list type="bullet">
/// <item><b>Типизированный round-trip.</b> Файл несёт {SaveKey → JSON-блок}.
///   При Load значения приходят из file-handler'а как <see cref="JsonElement"/>;
///   агрегатор десериализует каждый блок в <see cref="ISaveable.StateType"/>
///   и только потом передаёт в RestoreState. Раньше RestoreState получал
///   «сырой» JsonElement, паттерн `state is XxxSaveData` молча проваливался
///   у ВСЕХ реализаторов — восстановление не работало вообще.</item>
/// <item><b>Транзакционность.</b> Ошибка обязательного state-блока проваливает
///   операцию: Save — файл не пишется (частичный сейв = скрытая потеря
///   прогресса при следующем Load); Load — возвращает false, если хоть один
///   блок не восстановился. Раньше исключения подавлялись, а Save/Load
///   всегда сообщали успех.</item>
/// <item><b>LastError-контракт.</b> <see cref="LastErrors"/> — список ошибок
///   последней операции (для честного SaveCompletedEvent/LoadCompletedEvent).</item>
/// </list>
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

    /// <summary>Ошибки последней Save/Load (пусто = успех). Порядок — как у блоков.</summary>
    public IReadOnlyList<string> LastErrors { get; private set; } = Array.Empty<string>();

    public void Register(ISaveable saveable)
    {
        if (saveable == null) return;
        if (!_saveables.Contains(saveable)) _saveables.Add(saveable);
    }

    /// <summary>Сколько блоков зарегистрировано (QA/диагностика).</summary>
    public int RegisteredCount => _saveables.Count;

    /// <summary>SaveKey всех зарегистрированных блоков (QA/диагностика).</summary>
    public IReadOnlyList<string> RegisteredKeys
    {
        get
        {
            var keys = new string[_saveables.Count];
            for (int i = 0; i < _saveables.Count; i++) keys[i] = _saveables[i].SaveKey;
            return keys;
        }
    }

    public bool Save(string slotName)
    {
        var dict = new Dictionary<string, object>(_saveables.Count);
        var errors = new List<string>();
        foreach (var s in _saveables)
        {
            try
            {
                dict[s.SaveKey] = s.CaptureState() ?? new object();
            }
            catch (Exception ex)
            {
                errors.Add($"capture[{s.SaveKey}]: {ex.GetType().Name}: {ex.Message}");
            }
        }
        LastErrors = errors;

        // Транзакционность: обязательный блок упал при CaptureState — файл НЕ
        // пишем. Частичный файл = тихая потеря прогресса при следующем Load.
        if (errors.Count > 0)
        {
            foreach (var e in errors)
                Console.WriteLine($"[SaveDataAggregator] Save ABORTED (no file written) — {e}");
            return false;
        }

        return _fileHandler.Save(slotName, dict);
    }

    public bool Load(string slotName)
    {
        var dict = _fileHandler.Load(slotName);
        if (dict == null)
        {
            LastErrors = new[] { "file missing, unreadable or invalid JSON" };
            return false;
        }

        var errors = new List<string>();
        foreach (var s in _saveables)
        {
            // Отсутствующий блок (модуль появился после записи сейва) — не
            // ошибка: форвард-совместимость старых файлов.
            if (!dict.TryGetValue(s.SaveKey, out var raw)) continue;
            try
            {
                var state = ConvertToStateType(raw, s.StateType, s.SaveKey);
                s.RestoreState(state);
            }
            catch (Exception ex)
            {
                errors.Add($"restore[{s.SaveKey}]: {ex.GetType().Name}: {ex.Message}");
            }
        }
        LastErrors = errors;

        // Честный результат: false, если ХОТЬ ОДИН обязательный блок не
        // восстановился (раньше исключения глотались и Load всегда был true).
        if (errors.Count > 0)
        {
            foreach (var e in errors)
                Console.WriteLine($"[SaveDataAggregator] Load PARTIAL — {e}");
            return false;
        }
        return true;
    }

    public bool HasSave(string slotName) => _fileHandler.HasSave(slotName);
    public bool DeleteSave(string slotName) => _fileHandler.DeleteSave(slotName);
    public IReadOnlyList<string> GetAllSaves() => _fileHandler.GetAllSaves();

    /// <summary>
    /// R11 P0: конверсия сырого значения из файла в конкретный тип блока.
    /// File-handler десериализует Dictionary&lt;string, object&gt; — значения
    /// приходят как <see cref="JsonElement"/>; перегоняем в StateType.
    /// Прямые (уже типизированные) значения — для тестов и прямых вызовов.
    /// </summary>
    private static object ConvertToStateType(object raw, Type stateType, string saveKey)
    {
        if (raw is JsonElement element)
        {
            var deserialized = JsonSerializer.Deserialize(element.GetRawText(), stateType, SaveJson.Options);
            if (deserialized == null)
                throw new InvalidOperationException(
                    $"block '{saveKey}' deserialized to null (type {stateType.Name})");
            return deserialized;
        }

        if (stateType.IsInstanceOfType(raw))
            return raw;

        // Чужой тип (прямой вызов с анонимным/DTO-объектом) — перегон через JSON.
        var json = JsonSerializer.Serialize(raw, raw.GetType(), SaveJson.Options);
        var converted = JsonSerializer.Deserialize(json, stateType, SaveJson.Options);
        if (converted == null)
            throw new InvalidOperationException(
                $"block '{saveKey}' conversion to {stateType.Name} yielded null");
        return converted;
    }
}
