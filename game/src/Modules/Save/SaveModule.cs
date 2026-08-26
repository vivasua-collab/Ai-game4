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

    private IDisposable? _saveSubToken;
    private IDisposable? _loadSubToken;
    private SaveConfig _config = new();

    public void Start()
    {
        _saveSubToken = _saveSub.Subscribe(OnSaveRequested);
        _loadSubToken = _loadSub.Subscribe(OnLoadRequested);
        Console.WriteLine("[SaveModule] Started");
    }

    public void Tick(int tickCount)
    {
        // Autosave check every 60 ticks (per task brief)
        if (tickCount % 60 != 0) return;
        if (_config.AutoSaveIntervalMinutes <= 0) return;

        var slot = new SaveSlot($"autosave_{(tickCount / 60):D4}", SaveSlotType.AutoSave);
        _saveService.Save(slot);
    }

    private void OnSaveRequested(in SaveRequestedEvent e)
    {
        _saveService.Save(new SaveSlot(e.SlotName, e.SlotType));
        _saveCompletedPublisher.Publish(new SaveCompletedEvent(true, e.SlotName, null));
    }

    private void OnLoadRequested(in LoadRequestedEvent e)
    {
        _saveService.Load(new SaveSlot(e.SlotName, e.SlotType));
        _loadCompletedPublisher.Publish(new LoadCompletedEvent(true, e.SlotName, null));
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
        builder.Register<SaveModule>(Lifetime.Singleton);
    }
}
