#nullable enable
// Создано: 2026-10-20 — Этап-3 аудита (adit_10_20, дефект 4 / P2): единый
// координатор сброса world-scoped доменов для ОБЕИХ точек world-transition.
//
// Прежде сброс выполнялся в ДВУХ местах с РАЗНОЙ семантикой отказов:
//   • WorldDomainResetPhase (NewGame): try/catch на каждый домен, сбор всех
//     ошибок → AggregateException (контракт P1-9 fail-closed);
//   • GameSession.LoadGame: голый foreach — ПЕРВОЕ исключение прерывало цикл,
//     последующие домены оставались НЕ сброшенными, диагностика терялась.
// Один процесс — два поведения. Теперь оба пути делегируют сюда: полная
// диагностика + fail-closed на world-transition любого типа.
using System;
using System.Collections.Generic;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Entry.Phases;

/// <summary>
/// Единственная реализация контракта сброса world-scoped доменов
/// (ResolveAll&lt;IWorldResettable&gt;). Используется WorldDomainResetPhase
/// (NewGame-режим, фаза 0) и GameSession.LoadGame (до RestoreState).
/// </summary>
public static class WorldDomainResetCoordinator
{
    /// <summary>
    /// Сбросить ВСЕ домены: одно исключение НЕ прерывает цикл — каждая
    /// попытка независима (P1-9), ошибки агрегируются, полная диагностика,
    /// затем AggregateException (fail-closed: частично-сброшенный мир не
    /// имеет права продолжать сборку/загрузку).
    /// </summary>
    /// <returns>Число успешно сброшенных доменов (для логов).</returns>
    public static int ResetAll(IResolver resolver)
    {
        if (resolver == null) throw new ArgumentNullException(nameof(resolver));

        var failures = new List<Exception>();
        int reset = 0;
        foreach (var domain in resolver.ResolveAll<IWorldResettable>())
        {
            try
            {
                domain.ResetWorld();
                reset++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WorldDomainReset] {domain.GetType().Name}.ResetWorld() FAILED: " +
                                  $"{ex.GetType().Name}: {ex.Message}");
                failures.Add(new InvalidOperationException(
                    $"World domain {domain.GetType().FullName} failed to reset (fail-closed)", ex));
            }
        }

        // R15-аудит (P2-2): кэш спрайтов оружия — world-scoped (ключи по
        // itemId прошлого мира). Выполняется ВСЕГДА (и при провале): кэш —
        // presentation-слой, перезагружается лениво; при провале сборка
        // дальше не пойдёт, при успехе кэш уже чист.
        Adapter.Scene.WeaponVisualCatalog.ResetCache();

        if (failures.Count > 0)
        {
            throw new AggregateException(
                $"WorldDomainReset FAILED: {failures.Count} world-scoped domain(s) failed to reset — " +
                "fail-closed contract: a partially-reset world must NOT continue assembly/load",
                failures);
        }

        return reset;
    }
}
