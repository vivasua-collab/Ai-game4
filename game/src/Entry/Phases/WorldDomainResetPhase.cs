#nullable enable
// Создано: 2026-09-15 — R17 «Полнота сейва» (аудит-0911 E-1): единый сброс
// world-scoped доменов перед пересборкой мира. Заменяет NpcDomainResetPhase
// (сбрасывал только NPC-домен): теперь контракт IWorldResettable охватывает
// ВСЕ домены, чьё состояние привязано к миру, — NPC/звери/трупы/формации/
// зарядник/игрок/статы/Ци/квесты/валюта/инвентарь/кукла/пояс/предметы-на-
// земле/мир/время. Новые модули подключаются ДЕКЛАРАТИВНО (реализация
// интерфейса + DI-регистрация) — без правок фазы/сессии.
using System;
using System.Threading.Tasks;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Entry.Phases;

/// <summary>
/// Phase 0 — сброс ВСЕХ world-scoped доменов (ResolveAll&lt;IWorldResettable&gt;)
/// перед пересборкой мира. Фазы и доменные сервисы — DI-синглтоны на весь
/// процесс: без сброса повторная сборка (меню → NewGame) наследует состояние
/// прошлого мира (double-spawn NPC, ghost-animals, формация мира A действует
/// в мире B, дюп экипировки куклы, рюкзак прошлого мира + второй стартовый
/// набор, квесты/валюта/время прошлого мира).
///
/// SkipOnLoad=true (наследует): на LoadGame этот же сброс выполняет
/// GameSession.LoadGame ДО RestoreState — фазы идут ПОСЛЕ восстановления
/// состояния из сейва, сброс в фазе wipesил бы только что загруженное.
/// </summary>
public sealed class WorldDomainResetPhase : AbstractSceneAssemblyPhase
{
    public override string PhaseName => "WorldDomainReset";
    public override int PhaseOrder => 0;

    [Inject] private readonly IResolver _resolver = null!;

    public override Task ExecuteAsync()
    {
        // P1-9 (аудит 09.22): FAIL-CLOSED контракт reset-фазы.
        //
        // Этап-3 аудита (adit_10_20, дефект 4): реализация вынесена в
        // WorldDomainResetCoordinator — единая семантика для NewGame-фазы
        // и GameSession.LoadGame (прежде сессия прерывала сброс на первом
        // исключении — два разных поведения одного процесса).
        //
        // P2-20 (аудит, Фаза 6): rollback НЕ выполняется — reset не
        // транзакционен (домены уже изменены до провала); контракт =
        // «сброс упал → world-transition неуспешна», а не «мир частично
        // жив». Fake-rollback лишь маскировал бы грязь.
        int reset = WorldDomainResetCoordinator.ResetAll(_resolver);

        Console.WriteLine($"[Phase {PhaseOrder}] {PhaseName} complete — {reset} world-scoped доменов сброшено " +
                          "(npc/corpses/groups/animals/formations/charger/player/stats/qi/quests/currency/" +
                          "inventory/equipment/belt/ground/world/time) + weapon-cache");
        return Task.CompletedTask;
    }
}
