#nullable enable
// R40-5a/R41-2 (репорт владельца 18.10 №5 «спам отрисовки в лог» + №6 «просадки
// FPS»): общая диагностика отрисовки карты.
//
// (а) ЛОГ-ШТАМП: три GD.Print в _Draw (BiomeTiles / SurfaceTransitions /
//     ObjectLayer) валили 3 строки × 10 Гц = 30 строк/с в лог (спам по
//     репорту + синхронный console-IO в кадре — кандидат №1 просадок на
//     Windows). Теперь: не чаще 1 строки на рендерер в 5 с (константа
//     PrintIntervalMs); полный verbose — env GODOT_RENDER_LOG=1 (старое
//     поведение для отладки).
// (б) PERF-СЧЁТЧИКИ (R41-2): статические счётчики фактических вызовов _Draw
//     и нарисованных тайлов — их читает PerfOverlay (F6/GODOT_PERF_OVERLAY)
//     для анализа «кто перерисовывается и как часто».
using System;
using System.Collections.Generic;
using Godot;

namespace CultivationGame.Adapter.Scene;

public static class RenderDiagnostics
{
    /// <summary>Троттл печати статистики отрисовки: 1 строка на рендерер в 5 с.</summary>
    public const int PrintIntervalMs = 5000;

    /// <summary>
    /// R40-5a: verbose-режим — каждый _Draw печатает свою строку (как до R41).
    /// Включается запуском с GODOT_RENDER_LOG=1.
    /// </summary>
    public static bool Verbose =>
        System.Environment.GetEnvironmentVariable("GODOT_RENDER_LOG") == "1";

    // Последний тик печати по ключу рендерера.
    private static readonly Dictionary<string, ulong> _lastPrint = new();

    /// <summary>
    /// Пора ли печатать статистику (раз в PrintIntervalMs либо verbose)?
    /// Вызывается ПОСЛЕ отрисовки; запоминает момент печати только при true.
    /// </summary>
    public static bool ShouldPrint(string key)
    {
        if (Verbose) return true;
        ulong now = Time.GetTicksMsec();
        if (_lastPrint.TryGetValue(key, out ulong last) && now - last < PrintIntervalMs)
            return false;
        _lastPrint[key] = now;
        return true;
    }

    // === R41-2: счётчики редравов (инкремент в каждом _Draw; PerfOverlay
    // читает дельты раз в секунду → «redraws/s» по каждому рендереру) ===
    public static long BiomeDraws;        // BiomeTileRenderer._Draw
    public static long TransitionDraws;   // SurfaceTransitionRenderer._Draw
    public static long ObjectDraws;       // ObjectLayerRenderer._Draw
    public static long NpcDraws;          // NPCSpriteRenderer._Draw
    public static long AnimalDraws;       // AnimalSpriteRenderer._Draw
    public static long CorpseDraws;       // CorpseSpriteRenderer._Draw
    public static long QueueRedrawAllCalls; // SceneBuilder.QueueRedrawAll (глобальные редравы)
    /// <summary>Тайлов биома в последнем кадре BiomeTileRenderer (для оверлея).</summary>
    public static int LastBiomeTilesDrawn;
}
