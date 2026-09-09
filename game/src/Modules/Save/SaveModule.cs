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
public sealed class SaveModule : IModule
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

    private IDisposable? _saveSubToken;
    private IDisposable? _loadSubToken;
    private SaveConfig _config = new();

    public void Start()
    {
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
    }

    public void Tick(int tickCount)
    {
        // Autosave check every 60 ticks (per task brief)
        if (tickCount % 60 != 0) return;
        if (_config.AutoSaveIntervalMinutes <= 0) return;

        var slot = new SaveSlot($"autosave_{(tickCount / 60):D4}", SaveSlotType.AutoSave);
        // R11 P1-Save: результат автосейва не глотаем — лог при провале.
        if (!_saveService.Save(slot))
            Console.WriteLine($"[SaveModule] Autosave FAILED: {_saveService.LastError}");
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
    }
}
