#nullable enable
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
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
    // B5 (2026-08-26): публикуем SaveStartedEvent перед сборкой состояния,
    // чтобы transient-системы (TechniqueChargeService, AuraHoldService) могли
    // отменить активные зарядки/удержания с возвратом 50% Ци.
    [Inject] private readonly IPublisher<SaveStartedEvent> _saveStartedPub = null!;
    private SaveConfig _config = new();

    // ISaveable
    public string SaveKey => "save_meta";

    /// <summary>R11 P0: типизированный state-блок меты (был анонимный тип).</summary>
    private sealed class SaveMetaState
    {
        public long SavedAt;
        public int Version;
    }

    /// <summary>R11 P0-Save: конкретный тип для round-trip десериализации.</summary>
    public Type StateType => typeof(SaveMetaState);

    public object CaptureState()
    {
        // Этап-4 (P1 «полнота формата»): версия формата — из ЕДИНОГО
        // источника правды (каталог версий агрегатора), не хардкод.
        return new SaveMetaState { SavedAt = DateTime.UtcNow.Ticks, Version = SaveDataAggregator.CurrentFormatVersion };
    }

    public void RestoreState(object state)
    {
        // Nothing to restore for the meta-block in V1.
    }

    // ISaveService
    // 2026-09-06 (аудит, санация мёртвого API): удалены C#-event'ы
    // OnSaveCompleted/OnLoadCompleted — 0 подписчиков, дублировали живой
    // контракт шины SaveCompletedEvent (публикует SaveModule по
    // SaveRequestedEvent). Межмодульные уведомления — только через шину.

    public bool Save(SaveSlot slot)
    {
        // B5: уведомить transient-системы о начале сейва — они отменят активные
        // зарядки/удержания с возвратом 50% Ци. Иначе состояние теряется при сериализации.
        _saveStartedPub?.Publish(new SaveStartedEvent(slot.Name, slot.Type));

        bool ok = _aggregator.Save(slot.Name);
        LastError = ok ? null : DescribeErrors("Save", slot);
        Console.WriteLine($"[SaveService] Save('{slot}') → {(ok ? "OK" : "FAILED")}{(ok ? "" : $" — {LastError}")}");
        return ok;
    }

    public bool Load(SaveSlot slot)
    {
        bool ok = _aggregator.Load(slot.Name);
        LastError = ok ? null : DescribeErrors("Load", slot);
        Console.WriteLine($"[SaveService] Load('{slot}') → {(ok ? "OK" : "FAILED")}{(ok ? "" : $" — {LastError}")}");
        return ok;
    }

    // ── Этап-4 (P1, 20.10.2026): pre-flight + транзакционная загрузка ──

    /// <summary>In-memory снимок доменов для отката неудачной загрузки.</summary>
    private Dictionary<string, object>? _loadBackup;

    public bool Validate(SaveSlot slot)
    {
        bool ok = _aggregator.Validate(slot.Name);
        LastError = ok ? null : DescribeErrors("Validate", slot);
        Console.WriteLine($"[SaveService] Validate('{slot}') → {(ok ? "OK (формат поддерживается)" : $"REJECTED — {LastError}")}");
        return ok;
    }

    public bool CaptureBackup()
    {
        _loadBackup = null;
        var snapshot = _aggregator.CaptureSnapshot();
        if (snapshot == null)
        {
            LastError = "backup capture failed (см. SaveDataAggregator.LastErrors)";
            Console.WriteLine($"[SaveService] CaptureBackup → FAILED — {_lastCaptureBackupError()}");
            return false;
        }
        _loadBackup = snapshot;
        Console.WriteLine($"[SaveService] CaptureBackup → OK ({snapshot.Count} блоков в памяти)");
        return true;
    }

    public bool RestoreBackup()
    {
        if (_loadBackup == null)
        {
            Console.WriteLine("[SaveService] RestoreBackup → FAILED — снимка нет (CaptureBackup не звался или уже израсходован)");
            return false;
        }
        var backup = _loadBackup;
        _loadBackup = null; // одноразовый: частичный откат хуже честного отказа
        bool ok = _aggregator.RestoreSnapshot(backup);
        LastError = ok ? null : DescribeErrors("RestoreBackup", new SaveSlot("backup", SaveSlotType.Manual));
        Console.WriteLine($"[SaveService] RestoreBackup → {(ok ? "OK" : $"FAILED — {LastError}")}");
        return ok;
    }

    public void DiscardBackup()
    {
        if (_loadBackup != null)
            Console.WriteLine($"[SaveService] DiscardBackup — снимок ({_loadBackup.Count} блоков) освобождён без восстановления");
        _loadBackup = null;
    }

    private string? _lastCaptureBackupError()
        => _aggregator.LastErrors.Count > 0 ? string.Join("; ", _aggregator.LastErrors) : null;

    /// <summary>R11 P1-Save: детализованное описание ошибки для событий/UI.</summary>
    public string? LastError { get; private set; }

    private string? DescribeErrors(string op, SaveSlot slot)
    {
        var errs = _aggregator.LastErrors;
        if (errs.Count == 0) return $"{op}('{slot}') failed (file I/O)";
        return $"{op}('{slot}') failed: {string.Join("; ", errs)}";
    }

    public bool HasSave(SaveSlot slot) => _aggregator.HasSave(slot.Name);

    /// <summary>
    /// Удалить слот. P1-7 (аудит 09.22): честный bool по всей цепочке
    /// (FileHandler → Aggregator → сюда). Семантика «операция выполнена»
    /// (P2-15): true — файл существовал и удалён; false — файла не было
    /// или I/O-отказ. Прежний вариант безусловно возвращал true — UI
    /// получал ложный успех при отсутствующем файле.
    /// </summary>
    public bool DeleteSave(SaveSlot slot)
    {
        bool ok = _aggregator.DeleteSave(slot.Name);
        Console.WriteLine($"[SaveService] DeleteSave('{slot}') → {(ok ? "OK" : "FAILED (missing or I/O)")}");
        return ok;
    }

    public IReadOnlyList<SaveInfo> GetAllSaves()
    {
        var names = _aggregator.GetAllSaves();
        var list = new List<SaveInfo>(names.Count);
        foreach (var n in names)
        {
            // Этап-3 (меню сейвов): фактическая дата записи из save_meta
            // (раньше CreatedUnixSeconds=0 — UI не мог показать «когда сейвился»).
            long createdUnix = 0;
            if (_aggregator.TryGetSlotMeta(n, out long savedAtTicks, out _))
            {
                try
                {
                    createdUnix = new DateTimeOffset(new DateTime(savedAtTicks, DateTimeKind.Utc)).ToUnixTimeSeconds();
                }
                catch (ArgumentOutOfRangeException)
                {
                    createdUnix = 0;
                }
            }
            list.Add(new SaveInfo(new SaveSlot(n), n, createdUnix, 0L, 0, string.Empty));
        }
        return list;
    }

    /// <summary>
    /// Register an ISaveable service. NOT on the ISaveService interface —
    /// callers must cast to SaveService (concrete) to call this.
    /// </summary>
    public void RegisterSaveable(ISaveable saveable) => _aggregator.Register(saveable);
}
