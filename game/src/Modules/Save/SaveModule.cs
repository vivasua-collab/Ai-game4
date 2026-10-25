#nullable enable
using System;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Save;

// Note: ISaveFileHandler (Core.Interfaces) is registered here as the Modules-
// layer default (AppContext.BaseDirectory). The Adapter layer (GameBoot) may
// override this registration with Adapter.Persistence.SaveFileHandler
// (Godot ProjectSettings.GlobalizePath) before the container is built.
// See audit issue #6.

/// <summary>
/// Save module — ticks every 60 ticks to check for autosave.
/// Subscribes to SaveRequestedEvent / LoadRequestedEvent via the bus.
/// </summary>
public sealed class SaveModule : IModule, IWorldResettable
{
    public string ModuleName => "Save";

    [Inject] private readonly ISaveService _saveService = null!;
    [Inject] private readonly ISubscriber<SaveRequestedEvent> _saveSub = null!;
    [Inject] private readonly ISubscriber<LoadRequestedEvent> _loadSub = null!;
    [Inject] private readonly IPublisher<SaveCompletedEvent> _saveCompletedPublisher = null!;
    [Inject] private readonly IPublisher<LoadCompletedEvent> _loadCompletedPublisher = null!;
    // SAVE-A1 FIX (аудит-4): сбор ISaveable из DI-контейнера.
    [Inject] private readonly SaveDataAggregator _aggregator = null!;
    [Inject] private readonly IResolver _resolver = null!;
    // R17 (аудит-0911 SAV-2): конфиг ИНЖЕКТИТСЯ (раньше `new SaveConfig()` —
    // поле AutoSaveIntervalMinutes читалось только как вкл/выкл), а гейт
    // сессии останавливает автосейв в главном меню (раньше SaveModule.Tick
    // писал autosave-слоты от fallback-мира меню — мусорные сейвы).
    [Inject] private readonly SaveConfig _config = null!;
    [Inject] private readonly IGameSession _session = null!;
    // R36-a (Фаза 11 / P2-26): мировой тик — единственный источник каденции
    // автосейва. Прежде Tick(processTick) считал по счётчику ПРОЦЕССА
    // (GameBoot._currentTick): после NewGame/Load (TickCount → 0 / из сейва)
    // первый автосейв мог прийти через 1/7/18 минут — остаток процессного
    // счётчика ломал контракт «каждые 30 ИГРОВЫХ минут».
    [Inject] private readonly ITimeService _timeService = null!;
    // PT3-001: дешёвый перечень имён слотов (без парсинга main.json) — для
    // ротации и чистки легаси. SaveDataAggregator держит тот же инстанс
    // хендлера (Adapter-перерегистрация — синглтон), метa-таймстемпы берём
    // только для ≤10 ротационных слотов через _aggregator.TryGetSlotMeta.
    [Inject] private readonly ISaveFileHandler _fileHandler = null!;

    private IDisposable? _saveSubToken;
    private IDisposable? _loadSubToken;

    /// <summary>Мировой тик последнего автосейва (null — отсчёт ещё не начат).</summary>
    private int? _lastAutosaveWorldTick;

    /// <summary>R17: слот эпохи «перезапись одного файла» — больше НЕ пишется
    /// (PT3-001: ротация autosave_01..10); старый каталог остаётся на диске
    /// как валидный сейв предыдущей сессии (виден в меню, грузится).</summary>
    private const string AutoSaveSlotName = "autosave";

    // ── PT3-001 (плейтест-2-рестарт, 25.10): ротация + чистка легаси ──

    /// <summary>Имена ротационных слотов: autosave_01..autosave_10.</summary>
    private static string RotationSlotName(int i) => $"autosave_{i:D2}";

    /// <summary>Индекс ротационного слота (1..N) по имени; 0 — не ротационный.</summary>
    private int ParseRotationIndex(string name)
    {
        if (name.Length != "autosave_XX".Length
            || !name.StartsWith("autosave_", StringComparison.Ordinal)) return 0;
        char d1 = name[^2], d2 = name[^1];
        if (!char.IsDigit(d1) || !char.IsDigit(d2)) return 0;
        int idx = (d1 - '0') * 10 + (d2 - '0');
        return idx >= 1 && idx <= Math.Max(1, _config.AutoSaveRotationSlots) ? idx : 0;
    }

    /// <summary>
    /// PT3-001: выбор слота автосейва — «принцип слотов + ротация»:
    /// 1) слот без save_meta (пустой/битый ротационный) → перезаписываем его;
    /// 2) есть свободный индекс → первый свободный (заполняем по порядку);
    /// 3) все заняты → САМЫЙ СТАРЫЙ по save_meta.SavedAt (ticks UTC).
    /// Состояние НЕ в памяти — ротация честно переживает рестарт процесса.
    /// </summary>
    private string PickRotationSlot()
    {
        int slots = Math.Max(1, _config.AutoSaveRotationSlots);
        var taken = new bool[slots + 1];
        int noMetaIdx = 0, oldestIdx = 0;
        long oldestTicks = long.MaxValue;
        foreach (var name in _fileHandler.GetAllSaves())
        {
            int idx = ParseRotationIndex(name);
            if (idx <= 0) continue;
            taken[idx] = true;
            if (_aggregator.TryGetSlotMeta(name, out long ticks, out _) && ticks > 0)
            {
                if (ticks < oldestTicks) { oldestTicks = ticks; oldestIdx = idx; }
            }
            else if (noMetaIdx == 0)
            {
                noMetaIdx = idx; // битый ротационный — лучший кандидат на перезапись
            }
        }
        if (noMetaIdx > 0) return RotationSlotName(noMetaIdx);
        for (int i = 1; i <= slots; i++)
            if (!taken[i]) return RotationSlotName(i);
        return oldestIdx > 0 ? RotationSlotName(oldestIdx) : RotationSlotName(1);
    }

    /// <summary>
    /// PT3-001: чистка легаси-автосейвов старых билдов (схема до R17 плодила
    /// слоты autosave_NNNN — у владельца их накопилось 601: меню загрузки
    /// строило 601 кнопку и парсило 601 × main.json ≈ 600MB — фриз съедал
    /// клики и Esc, «слот не выбирается» (репорт плейтест-2-рестарт).
    /// Критерий: autosave_ + 3+ цифр — ровно старая схема; ручные slot_N,
    /// quicksave, одиночный R17-autosave и новые ротационные autosave_XX
    /// НЕ трогаются. Один проход на Start (идемпотентно, _startCompleted).
    /// </summary>
    private void CleanupLegacyAutosaves()
    {
        try
        {
            int removed = 0, failed = 0;
            foreach (var name in _fileHandler.GetAllSaves())
            {
                if (name.Length < "autosave_XXX".Length
                    || !name.StartsWith("autosave_", StringComparison.Ordinal)) continue;
                bool allDigits = true;
                for (int i = "autosave_".Length; i < name.Length; i++)
                    if (!char.IsDigit(name[i])) { allDigits = false; break; }
                // 3+ цифр после префикса = легаси (новая ротация — ровно 2)
                if (!allDigits || name.Length - "autosave_".Length < 3) continue;
                if (_saveService.DeleteSave(new SaveSlot(name, SaveSlotType.AutoSave))) removed++;
                else failed++;
            }
            if (removed > 0 || failed > 0)
                Console.WriteLine($"[SaveModule] PT3-001: легаси-автосейвы удалены: {removed}" +
                    (failed > 0 ? $", НЕ удалось удалить: {failed} (файл занят/нет прав)" : ""));
        }
        catch (Exception ex)
        {
            // Чистка — гигиена диска: сбой не должен ронять старт модуля.
            Console.WriteLine($"[SaveModule] PT3-001: чистка легаси-автосейвов НЕ удалась: {ex.Message}");
        }
    }

        // P1-10 (аудит 09.22, Фазы 7/10): идемпотентный Start — повторный вызов
    // (прямой вызов вне GameEntryPoint / повторная инстанциация сцены) не
    // дублирует подписки/инициализацию. Флаг — только при УСПЕШНОМ завершении
    // (провал остаётся ретраимым). Основной слой защиты — GameEntryPoint
    // (_startedModules, resume from failure); это страховка от прямых вызовов.
    private bool _startCompleted;

    public void Start()
    {
        if (_startCompleted) return;

        _saveSubToken = _saveSub.Subscribe(OnSaveRequested);
        _loadSubToken = _loadSub.Subscribe(OnLoadRequested);

        // SAVE-A1 FIX (аудит-4): агрегатор оставался ПУСТЫМ — RegisterSaveable()
        // никто не вызывал, при сохранении уходили только метаданные SaveService.
        // Собираем все ISaveable из DI-контейнера: ResolveAll дедуплицирует
        // форвард-регистрации, а кэш синглтонов по типу реализации
        // (Container.Resolve: service-type + impl-type) гарантирует те же
        // экземпляры, что живут в игре — Body/Charger/Inventory/NPC/
        // TechniqueSlot (+ save_meta самого SaveService).
        int registered = 0;
        foreach (var saveable in _resolver.ResolveAll<ISaveable>())
        {
            _aggregator.Register(saveable);
            registered++;
        }
        Console.WriteLine($"[SaveModule] Started — {registered} ISaveable service(s) registered");

        // PT3-001: чистка легаси-мусора autosave_NNNN (601 каталог у владельца
        // ломал меню загрузки) — ДО первого автосейва и любого открытия меню.
        CleanupLegacyAutosaves();
    
        _startCompleted = true;
    }

    public void Tick(int tickCount)
    {
        // R17 (аудит-0911 SAV-2 + E-4): автосейв — ТОЛЬКО в активной игровой
        // сессии. Тик-луп больше не гоняет симуляцию за главным меню (гейт
        // в GameBoot), но и здесь страховочный гейт: MainMenu/Loading/Saving/
        // Quitting — не пишем (раньше каждые 60 тиков плодились слоты
        // autosave_NNNN от fallback-мира меню — без чистки, сотни каталогов).
        if (_session.State != SessionState.Playing) return;

        // Интервал — ИГРОВЫЕ минуты из конфига (1 тик = 1 игровая минута).
        // 0/отрицательное значение — автосейв выключен.
        int interval = _config.AutoSaveIntervalMinutes;
        if (interval <= 0) return;

        // R36-a (Фаза 11 / P2-26): каденция — МИРОВОЙ тик (ITimeService.
        // TickCount), параметр tickCount (процессный) ИГНОРИРУЕТСЯ. Разностная
        // схема: первый Tick после старта/сброса мира НАЧИНАЕТ отсчёт
        // (автосейв — ровно через interval мировых минут, не мгновенно);
        // дальше — автосейв при накоплении ≥ interval (hitch/bulk-скачок
        // мирового тика НЕ теряет каденцию: разность честно учитывает
        // пропущенные минуты).
        int worldTick = _timeService.TickCount;
        if (_lastAutosaveWorldTick is not int last)
        {
            _lastAutosaveWorldTick = worldTick;
            return;
        }
        if (worldTick - last < interval) return;

        // PT3-001: ротационный слот (autosave_01..10, перезапись старейшего)
        // вместо единого R17-слота — указание владельца «максимум 10
        // автосейвов, ротация или слоты».
        var slot = new SaveSlot(PickRotationSlot(), SaveSlotType.AutoSave);
        // R11 P1-Save: результат автосейва не глотаем — лог при провале.
        if (!_saveService.Save(slot))
            Console.WriteLine($"[SaveModule] Autosave FAILED: {_saveService.LastError}");
        _lastAutosaveWorldTick = worldTick;
    }

    // ── R36-a (Фаза 11 / P2-26): IWorldResettable ─────────────────────
    /// <summary>
    /// Пересборка мира (NewGame ИЛИ LoadGame — сброс выполняет
    /// WorldDomainResetPhase/GameSession ДО RestoreState): отсчёт каденции
    /// автосейва сбрасывается — новый мир начинает с чистым интервалом PT3
    /// (TickCount нового мира = 0/восстановленный из сейва, процессный
    /// счётчик продолжает расти и НЕ влияет). Ротационный индекс НЕ
    /// сбрасывается — он вычисляется по save_meta на каждом автосейве.
    /// </summary>
    public void ResetWorld()
    {
        _lastAutosaveWorldTick = null;
        Console.WriteLine("[SaveModule] ResetWorld: отсчёт автосейва сброшен (каденция — мировой тик, P2-26)");
    }

    private void OnSaveRequested(in SaveRequestedEvent e)
    {
        // R11 P1-Save (review): публикуем РЕАЛЬНЫЙ результат операции —
        // раньше SaveCompletedEvent(true, …) писался безусловно, и UI/
        // автоматика получали ложный успех при упавшем файле/блоке.
        bool ok = _saveService.Save(new SaveSlot(e.SlotName, e.SlotType));
        _saveCompletedPublisher.Publish(new SaveCompletedEvent(
            ok, e.SlotName, ok ? null : _saveService.LastError ?? "save failed"));
    }

    private void OnLoadRequested(in LoadRequestedEvent e)
    {
        // R11 P1-Save (review): честный LoadCompletedEvent.
        bool ok = _saveService.Load(new SaveSlot(e.SlotName, e.SlotType));
        _loadCompletedPublisher.Publish(new LoadCompletedEvent(ok, e.SlotName));
    }

    public void Dispose()
    {
        _saveSubToken?.Dispose();
        _loadSubToken?.Dispose();
        Console.WriteLine("[SaveModule] Disposed");
    }
}

public static class SaveModuleServices
{
    public static void Register(IContainerBuilder builder)
    {
        builder.Register<SaveConfig>(Lifetime.Singleton);
        // Modules-layer default — uses AppContext.BaseDirectory. The Adapter
        // layer may override ISaveFileHandler with a Godot-aware impl.
        builder.Register<SaveFileHandler>(Lifetime.Singleton);
        builder.Register<ISaveFileHandler, SaveFileHandler>(Lifetime.Singleton);
        builder.Register<SaveDataAggregator>(Lifetime.Singleton);
        builder.Register<SaveService>(Lifetime.Singleton);
        builder.Register<ISaveService, SaveService>(Lifetime.Singleton);
        // SAVE-A1 (аудит-4): save_meta самого SaveService попадает в агрегатор
        // через ResolveAll<ISaveable> (версия формата + время сейва). Без этой
        // регистрации self-bound ключ перетирается форвардом ISaveService, и
        // ISaveable-реализация SaveService оставалась мёртвым кодом.
        builder.Register<ISaveable, SaveService>(Lifetime.Singleton);
        builder.Register<SaveModule>(Lifetime.Singleton);
        // R36-a (P2-26): WorldDomainResetPhase видит SaveModule в реестре
        // сбрасываемых доменов (мульти-форвард того же синглтона).
        builder.Register<IWorldResettable, SaveModule>(Lifetime.Singleton);
    }
}
