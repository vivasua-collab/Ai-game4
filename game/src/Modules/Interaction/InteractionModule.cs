#nullable enable
// Создано: 2026-05-09 — Phase 13: точка входа модуля Interaction
// Migrated from Ai-game3 (Unity+VContainer+MessagePipe) to Ai-game4 (Godot+DI+EventBus) 2026-08-15.
using System;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Interaction;

/// <summary>
/// Точка входа модуля Interaction.
/// Инициализирует сервисы конфигурацией и обрабатывает тики (typewriter).
/// </summary>
public class InteractionModule : IModule
{
    [Inject] private readonly InteractionService _interactionServiceImpl = null!;
    [Inject] private readonly DialogueService _dialogueServiceImpl = null!;
    [Inject] private readonly DialogueTypewriter _typewriter = null!;
    [Inject] private readonly ITimeService _timeService = null!;

    // IMPL-3: Config injected via DI (replaces obsolete SetConfig()).
    [Inject] private readonly InteractionConfig _config = null!;
    private bool _isConfigured;

    public string ModuleName => "Interaction";

    public void Start()
    {
        // IMPL-3: Config injected via DI. Flag still used by Tick.
        _isConfigured = true;

        _interactionServiceImpl.Initialize(_config);
        _dialogueServiceImpl.Initialize(_config);
    }

    public void Tick(int tickCount)
    {
        if (!_isConfigured) return;

        // Обработка typewriter-эффекта (BD-42: ITimeService.DeltaTime)
        _typewriter.Tick(_timeService.DeltaTime);
    }

    public void Dispose()
    {
        _interactionServiceImpl?.Dispose();
        _dialogueServiceImpl?.Dispose();
    }
}
