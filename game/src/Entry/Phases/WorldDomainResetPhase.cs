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
                // Один сломанный домен не должен ронять всю пересборку —
                // но и молчать нельзя: состояние мира может быть грязным.
                Console.WriteLine($"[WorldDomainReset] {domain.GetType().Name}.ResetWorld() FAILED: " +
                                  $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        // R15-аудит (P2-2): кэш спрайтов оружия — world-scoped (ключи по
        // itemId прошлого мира). Унаследовано из NpcDomainResetPhase.
        Adapter.Scene.WeaponVisualCatalog.ResetCache();

        Console.WriteLine($"[Phase {PhaseOrder}] {PhaseName} complete — {reset} world-scoped доменов сброшено " +
                          "(npc/corpses/groups/animals/formations/charger/player/stats/qi/quests/currency/" +
                          "inventory/equipment/belt/ground/world/time) + weapon-cache");
        return Task.CompletedTask;
    }
}
