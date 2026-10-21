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
        // Этап-4 (GAP-014): группы — ПОСЛЕ npc И animals: состав группы
        // валидируется по ОБЕИМ реестрам (HumanNPCSpawnPhase наполняет группы
        // NPC, GroupSpawnPhase — зверями: волчья стая/стадо оленей).
        "npc", "npc_relationships", "corpses", "animals", "npc_groups", "quests",
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
    // Этап-4 аудит (20.10.2026, P1 «полнота формата»): перечень критических
    // блоков v1 {world, world_time, player, body} был НЕПОЛОН — сейв с
    // корректной метой, но без инвентаря/Ци/квестов/NPC/отношений проходил
    // валидацию, а после сброса доменов эти состояния терялись без ошибки.
    // Новый контракт:
    //  • полный ЯВНЫЙ каталог обязательных блоков на каждую версию;
    //  • необязательные блоки — только как ДОКУМЕНТИРОВАННОЕ правило
    //    совместимости конкретной версии (список OptionalBlocksV1),
    //    а не молчаливый пропуск любого отсутствующего ключа;
    //  • v2 — текущий формат писателя (все 23 блока, включая новый
    //    npc_groups); v1 — легаси-сейвы (meta.Version=1 писался всегда,
    //    с R11): поздние блоки в v1 опциональны с семантикой «отсутствует =
    //    домен в дефолте после сброса».
    //
    // Полный контракт версий:
    //  • save_meta.Version — ОБЯЗАТЕЛЕН: без него формат неизвестен → отказ;
    //  • версия новее текущей → отказ («обновите игру»);
    //  • версия известна → её обязательные блоки ОБЯЗАНЫ присутствовать
    //    (проверка ДО вызова любого RestoreState);
    //  • неизвестная версия → отказ (не «считать все блоки необязательными»);
    //  • при провале формата RestoreState НЕ вызывается — домены остаются
    //    нетронутыми (вызывающий честно вернёт сессию в меню/в игру).
    //
    // Миграции: при появлении v3 — добавить case в MandatoryBlocksForVersion,
    // зафиксировать опциональные блоки v2 (если появятся) и (при
    // необходимости) migrate-хук, преобразующий данные до текущей формы.
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Текущая версия формата сейва (пишется в save_meta.Version).
    /// Этап-4: 1 → 2 — новый блок npc_groups (GAP-014) + полный mandatory-
    /// каталог. Легаси-сейвы v1 читаются по правилам v1 (OptionalBlocksV1).
    /// </summary>
    public const int CurrentFormatVersion = 2;

    // ── v2 (текущий писатель) ──
    // ВСЕ блоки, которые пишет агрегатор в полной production-сборке
    // (23 шт.: 22 сервиса + npc_groups). Отсутствие ЛЮБОГО из них в v2-файле
    // = файл писался не этим конвейером (обрезан/подделан) → отказ.
    private static readonly string[] MandatoryBlocksV2 =
    {
        "world", "world_time", "item_db",
        "player", "stats", "body", "qi",
        "inventory", "equipment", "storage_rings", "belt",
        "techniques", "technique_slots", "formation",
        "npc", "npc_groups", "npc_relationships", "corpses", "animals", "quests",
        "currency", "charger", "save_meta",
    };

    // ── v1 (легаси, meta.Version=1 писался с R11) ──
    // Ядро, присутствовавшее в каждом v1-сейве со стабилизации контракта
    // RestoreOrder (R17, 09.11). Без любого из них мир нецелостен → отказ.
    private static readonly string[] MandatoryBlocksV1 =
    {
        "world", "world_time",
        "player", "stats", "body", "qi",
        "inventory", "equipment", "belt",
        "techniques", "technique_slots",
        "npc", "npc_relationships", "animals", "quests",
        "currency", "save_meta",
    };

    // ЯВНОЕ правило совместимости v1 (аудит Этап-4: «необязательные блоки
    // должны быть явно определены правилами совместимости»): блоки,
    // добавленные в схему ПОЗЖЕ стабилизации v1-ядра, в старых сейвах
    // отсутствуют — это НЕ ошибка файла: семантика «отсутствует = домен
    // остаётся в дефолте после сброса». Присутствуют — восстанавливаются.
    //   formation      — FormationService (R15)
    //   item_db        — ItemDatabaseService (R17, 09.11)
    //   corpses        — CorpseService (R13, 09.10)
    //   storage_rings  — StorageRingService
    //   charger        — ChargerService (R37, 09.23)
    //   npc_groups     — NPCGroupService (Этап-4: в v1 групп не было — после
    //                    загрузки легаси-сейва группы пусты, состав мира
    //                    задают восстановленные NPC)
    private static readonly string[] OptionalBlocksV1 =
    {
        "formation", "item_db", "corpses", "storage_rings", "charger", "npc_groups",
    };

    /// <summary>
    /// Обязательные блоки для конкретной версии сейва. null — версия
    /// неизвестна (нет правил совместимости) → отказ загрузки.
    /// </summary>
    private static string[]? MandatoryBlocksForVersion(int version) => version switch
    {
        1 => MandatoryBlocksV1,
        2 => MandatoryBlocksV2,
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

        RestoreAllFrom(dict, errors);
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
    /// Этап-4 (P1 «неудачная загрузка убивает сессию»): pre-flight проверка
    /// слота БЕЗ побочных эффектов — файл читается и валидируется по формату
    /// (мета/версия/обязательные блоки), но RestoreState НЕ вызывается, домены
    /// не трогаются. Вызывается GameSession.LoadGame ДО сброса активного мира:
    /// повреждённый/неполный/неподдерживаемый слот должен отвергаться, пока
    /// текущий мир ещё цел.
    /// </summary>
    public bool Validate(string slotName)
    {
        var dict = _fileHandler.Load(slotName);
        var errors = new List<string>();
        if (dict == null)
        {
            errors.Add("file missing, unreadable or invalid JSON");
            LastErrors = errors;
            return false;
        }
        if (!ValidateFormat(dict, errors))
        {
            LastErrors = errors;
            foreach (var e in errors)
                Console.WriteLine($"[SaveDataAggregator] Validate('{slotName}') REJECTED — {e}");
            return false;
        }
        LastErrors = Array.Empty<string>();
        return true;
    }

    /// <summary>
    /// Этап-4 (P1, транзакционная загрузка): in-memory снимок ВСЕХ доменов
    /// (тот же конвейер CaptureState, что у Save, но БЕЗ записи файла и БЕЗ
    /// SaveStartedEvent — снимок неразрушающий для transient-систем).
    /// null — любой CaptureState упал (снимок недостоверен, откат невозможен).
    /// </summary>
    public Dictionary<string, object>? CaptureSnapshot()
    {
        var dict = new Dictionary<string, object>(_saveables.Count);
        foreach (var s in _saveables)
        {
            try
            {
                dict[s.SaveKey] = s.CaptureState() ?? new object();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SaveDataAggregator] CaptureSnapshot ABORTED — capture[{s.SaveKey}]: {ex.GetType().Name}: {ex.Message}");
                LastErrors = new[] { $"capture[{s.SaveKey}]: {ex.GetType().Name}: {ex.Message}" };
                return null;
            }
        }
        LastErrors = Array.Empty<string>();
        return dict;
    }

    /// <summary>
    /// Этап-4 (P1): восстановление ВСЕХ доменов из in-memory снимка
    /// (ветка отката транзакционной загрузки). Контракт вызывающего: домены
    /// УЖЕ сброшены (WorldDomainResetCoordinator.ResetAll) — RestoreState
    /// делает merge, не чистит. Снимок снят тем же составом сервисов, потому
    /// формат-валидация здесь избыточна; используется тот же walk RestoreOrder.
    /// </summary>
    public bool RestoreSnapshot(Dictionary<string, object> snapshot)
    {
        var errors = new List<string>();
        RestoreAllFrom(snapshot, errors);
        LastErrors = errors;
        if (errors.Count > 0)
        {
            foreach (var e in errors)
                Console.WriteLine($"[SaveDataAggregator] RestoreSnapshot PARTIAL — {e}");
            return false;
        }
        return true;
    }

    /// <summary>Общий walk восстановления: RestoreOrder → хвост регистраций.</summary>
    private void RestoreAllFrom(Dictionary<string, object> dict, List<string> errors)
    {
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

        var mandatory = MandatoryBlocksForVersion(version);
        if (mandatory == null)
        {
            errors.Add($"format: версия сейва {version} не поддерживается (правила совместимости не определены)");
            return false;
        }

        bool ok = true;
        foreach (var key in mandatory)
        {
            if (!dict.ContainsKey(key))
            {
                errors.Add($"format: отсутствует обязательный блок '{key}' (обязателен для версии {version})");
                ok = false;
            }
        }
        // Диагностика для v1: каких опциональных блоков нет (правило
        // совместимости — домен останется в дефолте; информативно, не ошибка).
        if (ok && version < CurrentFormatVersion)
        {
            var absent = new List<string>();
            foreach (var key in OptionalBlocksV1)
                if (!dict.ContainsKey(key)) absent.Add(key);
            if (absent.Count > 0)
                Console.WriteLine($"[SaveDataAggregator] v{version}-legаси: опциональные блоки отсутствуют " +
                    $"[{string.Join(", ", absent)}] — домены останутся в дефолте (правило совместимости v1)");
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
