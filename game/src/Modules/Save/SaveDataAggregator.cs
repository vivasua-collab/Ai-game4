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

    // ──────────────────────────────────────────────────────────────────
    // R17 (аудит-0911 SAV-5 + G-2): ЯВНЫЙ порядок RestoreState.
    //
    // Прежде поведение: обход в порядке регистраций DI-контейнера
    // (insertion-порядок Dictionary — деталь реализации .NET). Рефакторинг
    // регистраций молча менял порядок; с появлением блока item_db это
    // стало КРИТИЧНО: NPCService.RestoreState резолвит EquipmentIds через
    // IItemDatabaseService (NPC-3-фикс) — каталог ДОЛЖЕН быть восстановлен
    // ДО npc-блока, иначе экипировка NPC молча теряется.
    //
    // Порядок — теперь контракт (SAVE_SYSTEM §4.5):
    //   мир → время → каталог предметов → игрок → статы → тело → Ци →
    //   инвентарь → кукла → пояс → техники → слоты → формация → NPC →
    //   звери → квесты → валюта → зарядник → мета.
    // Блоки вне списка (будущие) восстанавливаются после, в порядке
    // регистраций. Отсутствующие в сейве ключи пропускаются (форвард-
    // совместимость старых файлов — без изменений).
    // ──────────────────────────────────────────────────────────────────
    private static readonly string[] RestoreOrder =
    {
        "world", "world_time", "item_db",
        "player", "stats", "body", "qi",
        "inventory", "equipment", "storage_rings", "belt",
        "techniques", "technique_slots", "formation",
        "npc", "npc_relationships", "corpses", "animals", "quests",
        "currency", "charger", "save_meta",
    };

    // ──────────────────────────────────────────────────────────────────
    // Этап-3 аудит (adit_10_20, дефект 1 / P1): версионированная схема
    // целостности сейва. Прежде Load() возвращал true для ЛЮБОГО валидного
    // JSON — включая пустой «{}»: RestoreOne молча пропускал отсутствующие
    // блоки, errors оставался пустым, а GameSession.LoadGame запускала мир
    // с несуществующим состоянием (Data от fallback TestPolygon, сетка от
    // перегенерации, домены пусты). Версия формата писалась в save_meta,
    // но никогда не проверялась.
    //
    // Новый контракт:
    //  • save_meta.Version — ОБЯЗАТЕЛЕН: без него формат неизвестен → отказ
    //    (в проекте не существовало формата без меты — SaveService пишет
    //    save_meta с самой первой записи);
    //  • версия новее текущей → отказ («обновите игру»);
    //  • версия известна → её критические блоки ОБЯЗАНЫ присутствовать;
    //  • неизвестная версия → отказ (не «считать все блоки необязательными»);
    //  • некритические блоки по-прежнему опциональны (форвард-
    //    совместимость: сейв v1 без formation/charger корректен);
    //  • при провале формата RestoreState НЕ вызывается — домены остаются
    //    нетронутыми (вызывающий честно вернёт сессию в меню).
    //
    // Миграции: при появлении v2 — добавить case в CriticalBlocksForVersion
    // и (при необходимости) migrate-хук, преобразующий v1-данные до v2-формы.
    // ──────────────────────────────────────────────────────────────────

    /// <summary>Текущая версия формата сейва (пишется в save_meta.Version).</summary>
    public const int CurrentFormatVersion = 1;

    /// <summary>Критические блоки формата v1: без них мир нецелостен.</summary>
    private static readonly string[] CriticalBlocksV1 = { "world", "world_time", "player", "body" };

    /// <summary>
    /// Критические блоки для конкретной версии сейва. null — версия
    /// неизвестна (нет правил совместимости) → отказ загрузки.
    /// </summary>
    private static string[]? CriticalBlocksForVersion(int version) => version switch
    {
        1 => CriticalBlocksV1,
        _ => null,
    };

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

        // R17: сначала — блоки из явного контракта RestoreOrder (в этом
        // порядке), затем остальные (будущие модули) в порядке регистраций.
        var errors = new List<string>();

        // Этап-3 (P1): целостность файла ДО восстановления — битый/неполный
        // сейв НЕ доходит до RestoreState (раньше «{}» проходил как успех).
        if (!ValidateFormat(dict, errors))
        {
            LastErrors = errors;
            foreach (var e in errors)
                Console.WriteLine($"[SaveDataAggregator] Load REJECTED (format) — {e}");
            return false;
        }
        var restored = new HashSet<ISaveable>(
            System.Collections.Generic.ReferenceEqualityComparer.Instance);
        foreach (var key in RestoreOrder)
        {
            for (int i = 0; i < _saveables.Count; i++)
            {
                var s = _saveables[i];
                if (restored.Contains(s) || s.SaveKey != key) continue;
                restored.Add(s);
                RestoreOne(s, dict, errors);
            }
        }
        for (int i = 0; i < _saveables.Count; i++)
        {
            var s = _saveables[i];
            if (restored.Contains(s)) continue;
            restored.Add(s);
            RestoreOne(s, dict, errors);
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

    /// <summary>
    /// Этап-3 (P1): проверка целостности формата сейва. Возвращает false и
    /// наполняет errors, если сейв не соответствует ни одному известному
    /// формату (нет меты / версия неизвестна / версия новее игры / отсутствует
    /// критический блок).
    /// </summary>
    private static bool ValidateFormat(Dictionary<string, object> dict, List<string> errors)
    {
        if (!dict.TryGetValue("save_meta", out var metaRaw) || metaRaw is not JsonElement metaEl)
        {
            errors.Add("format: блок 'save_meta' отсутствует — версия формата неизвестна; загрузка не поддерживается");
            return false;
        }

        // Naming: CamelCase (SaveJson) с fallback на PascalCase — сейвы,
        // записанные Modules-хендлером до единой naming-политики.
        var versionProp = GetPropertyIgnoreCase(metaEl, "version");
        if (versionProp is not { ValueKind: JsonValueKind.Number })
        {
            errors.Add("format: 'save_meta.version' не читается — сейв без версии формата не поддерживается");
            return false;
        }

        int version = versionProp.Value.GetInt32();
        if (version > CurrentFormatVersion)
        {
            errors.Add($"format: сейв версии {version} новее поддерживаемой ({CurrentFormatVersion}) — обновите игру");
            return false;
        }

        var critical = CriticalBlocksForVersion(version);
        if (critical == null)
        {
            errors.Add($"format: версия сейва {version} не поддерживается (правила совместимости не определены)");
            return false;
        }

        bool ok = true;
        foreach (var key in critical)
        {
            if (!dict.ContainsKey(key))
            {
                errors.Add($"format: отсутствует критический блок '{key}' (обязателен для версии {version})");
                ok = false;
            }
        }
        return ok;
    }

    /// <summary>Свойство JsonElement без учёта регистра (CamelCase/PascalCase сейвы).</summary>
    private static JsonElement? GetPropertyIgnoreCase(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                return prop.Value;
        }
        return null;
    }

    /// <summary>Восстановить один блок (отсутствующий блок — не ошибка).</summary>
    private void RestoreOne(ISaveable s, Dictionary<string, object> dict, List<string> errors)
    {
        // Отсутствующий блок (модуль появился после записи сейва) — не
        // ошибка: форвард-совместимость старых файлов.
        if (!dict.TryGetValue(s.SaveKey, out var raw)) return;
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

    public bool HasSave(string slotName) => _fileHandler.HasSave(slotName);
    public bool DeleteSave(string slotName) => _fileHandler.DeleteSave(slotName);
    public IReadOnlyList<string> GetAllSaves() => _fileHandler.GetAllSaves();

    /// <summary>
    /// Этап-3 (меню сейвов): мета слота для UI — без полного RestoreState.
    /// Читает только save_meta (дата записи UTC-ticks + версия формата).
    /// </summary>
    public bool TryGetSlotMeta(string slotName, out long savedAtUtcTicks, out int version)
    {
        savedAtUtcTicks = 0;
        version = 0;
        var dict = _fileHandler.Load(slotName);
        if (dict == null || !dict.TryGetValue("save_meta", out var raw) || raw is not JsonElement el)
            return false;
        try
        {
            var savedAt = GetPropertyIgnoreCase(el, "savedAt");
            if (savedAt is { ValueKind: JsonValueKind.Number })
                savedAtUtcTicks = savedAt.Value.GetInt64();
            var ver = GetPropertyIgnoreCase(el, "version");
            if (ver is { ValueKind: JsonValueKind.Number })
                version = ver.Value.GetInt32();
            return savedAt is { ValueKind: JsonValueKind.Number };
        }
        catch
        {
            return false;
        }
    }

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
