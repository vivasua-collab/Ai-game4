#nullable enable
// Создано: 2026-05-18 17:58:25 UTC
// Точка входа модуля Generator.
// IStartable — инициализация базы данных предметов.
// Migrated from Ai-game3 (Unity+VContainer+MessagePipe) to Ai-game4 (Godot+DI+EventBus) 2026-08-15.
using System;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Generator;

/// <summary>
/// Точка входа модуля Generator.
/// Инициализирует ItemDatabaseService при старте (загрузка предустановленных предметов).
/// </summary>
public class GeneratorModule : IModule
{
    [Inject] private readonly IItemDatabaseService _itemDatabase = null!;

    public string ModuleName => "Generator";

    public void Start()
    {
        // Вызываем Initialize() через concrete-тип, так как метод не входит в интерфейс
        if (_itemDatabase is ItemDatabaseService dbServiceImpl)
        {
            dbServiceImpl.Initialize();
            Console.WriteLine("[GeneratorModule] База данных предметов инициализирована");
        }
        else
        {
            Console.WriteLine("[GeneratorModule] IItemDatabaseService не является ItemDatabaseService — Initialize() пропущен");
        }

        Console.WriteLine($"[GeneratorModule] Модуль запущен. Зарегистрировано предметов: {_itemDatabase.Count}");
    }

    public void Tick(int tickCount)
    {
        // Generator has no per-tick work
    }

    public void Dispose()
    {
    }
}
