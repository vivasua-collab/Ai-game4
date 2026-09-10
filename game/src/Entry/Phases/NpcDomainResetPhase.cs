#nullable enable
// Создано: 2026-09-10 — R13-аудит (P2-4) + R14-аудит (P2-1): сброс NPC-домена
// перед пересборкой мира. Фазы — DI-синглтоны на весь процесс, и доменные
// сервисы тоже: без сброса повторная сборка (меню → NewGame) даёт
// double-spawn, трупы/группы прошлого мира переживают сборку.
using System;
using System.Threading.Tasks;
using CultivationGame.Core.DI;
using CultivationGame.Modules.NPC;

namespace CultivationGame.Entry.Phases;

/// <summary>
/// Phase 0 — сброс NPC-домена (реестр NPC, per-entity провайдеры тела/Ци/
/// экипировки, баффы, якоря блуждания, отношения, трупы, группы) перед
/// пересборкой мира. Паттерн WorldInitPhase.Clear (реестр техник) — но
/// раньше: домен должен быть пуст ДО любых спавн-фаз (AnimalSpawn 6 /
/// HumanNPCSpawn 7 / GroupSpawn 8).
/// SkipOnLoad=true (наследует): на LoadGame сброс выполняет
/// GameSession.LoadGame ДО RestoreState — фазы идут ПОСЛЕ восстановления
/// состояния из сейва, сброс в фазе wipesил бы только что загруженных NPC.
/// </summary>
public sealed class NpcDomainResetPhase : AbstractSceneAssemblyPhase
{
    public override string PhaseName => "NpcDomainReset";
    public override int PhaseOrder => 0;

    [Inject] private readonly NPCModule _npcModule = null!;

    public override Task ExecuteAsync()
    {
        _npcModule.ResetWorld();
        // R15-аудит (P2-2): кэш спрайтов оружия — world-scoped (ключи по
        // itemId прошлого мира; ResetCache существовал, но не вызывался).
        Adapter.Scene.WeaponVisualCatalog.ResetCache();
        Console.WriteLine($"[Phase {PhaseOrder}] {PhaseName} complete — NPC domain wiped (registry/corpses/groups/weapon-cache)");
        return Task.CompletedTask;
    }
}
