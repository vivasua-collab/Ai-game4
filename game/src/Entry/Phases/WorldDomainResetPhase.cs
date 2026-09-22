#nullable enable
// Создано: 2026-09-15 — R17 «Полнота сейва» (аудит-0911 E-1): единый сброс
// world-scoped доменов перед пересборкой мира. Заменяет NpcDomainResetPhase
// (сбрасывал только NPC-домен): теперь контракт IWorldResettable охватывает
// ВСЕ домены, чьё состояние привязано к миру, — NPC/звери/трупы/формации/
// зарядник/игрок/статы/Ци/квесты/валюта/инвентарь/кукла/пояс/предметы-на-
// земле/мир/время. Новые модули подключаются ДЕКЛАРАТИВНО (реализация
// интерфейса + DI-регистрация) — без правок фазы/сессии.
using System;
using System.Collections.Generic;
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
        // P1-9 (аудит 09.22, Фазы 6/7/10): FAIL-CLOSED контракт reset-фазы.
        //
        // Прежде: исключение ResetWorld() одного домена глоталось с логом —
        // ExecuteAsync «завершался успешно», оркестратор MarkAsCompleted и
        // продолжал Spawn/Init/Ready: новый мир собирался из смеси нового,
        // старого и частично-сброшенного состояния, а последующие фазы
        // считали мир чистым (WorldDomainReset — фаза 0). LoadGame-путь при
        // этом aborted честно (GameSession ловит исключение наружу) — одна
        // семантика контракта, два разных поведения.
        //
        // Теперь — зеркально startup-контракту P2-13 (GameEntryPoint):
        //   • одна попытка = полная диагностика: ВСЕ домены получают
        //     ResetWorld() (сбросы доменов независимы — продолжение цикла
        //     после провала не порождает новых зависимостей);
        //   • провалы агрегируются → наружу AggregateException с именем
        //     каждого домена;
        //   • SceneOrchestrator ловит → MarkAsFailed + SceneAssemblyFailed
        //     → rethrow → GameSession.NewGame/LoadGame возвращает сессию в
        //     MainMenu: частично-сброшенный мир НЕ собирается дальше.
        //
        // P2-20 (аудит, Фаза 6): rollback НЕ выполняется — reset не
        // транзакционен (домены уже изменены до провала); контракт =
        // «сброс упал → world-transition неуспешна», а не «мир частично
        // жив». Fake-rollback лишь маскировал бы грязь.
        var failures = new List<Exception>();
        int reset = 0;
        foreach (var domain in _resolver.ResolveAll<IWorldResettable>())
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
                    $"World domain {domain.GetType().FullName} failed to reset (P1-9 fail-closed)", ex));
            }
        }

        // R15-аудит (P2-2): кэш спрайтов оружия — world-scoped (ключи по
        // itemId прошлого мира). Унаследовано из NpcDomainResetPhase.
        // Выполняется и при провале доменов: кэш — presentation-слой,
        // перезагрузится лениво; сборка ниже всё равно не продолжится.
        Adapter.Scene.WeaponVisualCatalog.ResetCache();

        if (failures.Count > 0)
        {
            throw new AggregateException(
                $"WorldDomainReset FAILED: {failures.Count} world-scoped domain(s) failed to reset — " +
                "fail-closed contract (P1-9): a partially-reset world must NOT continue assembly",
                failures);
        }

        Console.WriteLine($"[Phase {PhaseOrder}] {PhaseName} complete — {reset} world-scoped доменов сброшено " +
                          "(npc/corpses/groups/animals/formations/charger/player/stats/qi/quests/currency/" +
                          "inventory/equipment/belt/ground/world/time) + weapon-cache");
        return Task.CompletedTask;
    }
}
