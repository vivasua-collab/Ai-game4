#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Modules.Combat;

namespace CultivationGame.Entry.Phases;

/// <summary>
/// Этап 1 внедрения ЦИ (2026-08-23): выдача игроку случайного тест-набора техник.
///
/// Тест-режим (TECHNIQUE_SYSTEM.md §12 слоты):
///   1. Cultivation-слот: одна пассивная техника культивации (Common, neutral).
///   2. Combat-пул (3+(L-1) слотов): случайные активные техники разных типов —
///      Combat/Defense/Support/Healing/Movement (равномерно по циклу).
///   3. Curse-слот: одна техника проклятия (если уровень ≥ 2 — иначе пропустить).
///   4. Formation-слот: одна техника формаций (создание формаций, этап 5).
///
/// Все техники генерируются ITechniqueGeneratorService (детерминированно,
/// seed из сессии) и изучаются через TechniqueService.LearnTechnique,
/// который проверяет слоты и резонанс уровней (§8.1).
/// </summary>
public sealed class TechniqueGrantPhase : AbstractSceneAssemblyPhase
{
    public override string PhaseName => "TechniqueGrant";
    public override int PhaseOrder => 45; // После PlayerSpawn (4), до NPC-фаз (50+)

    [Inject] private readonly ITechniqueGeneratorService _techniqueGenerator = null!;
    [Inject] private readonly TechniqueService _techniques = null!;
    [Inject] private readonly IQiService _qi = null!;

    /// <summary>Типы активных техник для случайного наполнения Combat-пула.</summary>
    private static readonly TechniqueType[] ActivePoolTypes =
    {
        TechniqueType.Combat,
        TechniqueType.Defense,
        TechniqueType.Combat,
        TechniqueType.Support,
        TechniqueType.Healing,
        TechniqueType.Combat,
        TechniqueType.Movement,
        TechniqueType.Sensory,
    };

    public override Task ExecuteAsync()
    {
        int level = (int)_qi.CultivationLevel;
        // Тест-режим: набор случайный при каждой новой игре; seed логируется
        // для воспроизведения (принцип детерминированности SeededRandom).
        long seed = Environment.TickCount;
        Console.WriteLine($"[TechniqueGrant] seed={seed}");

        int granted = 0;

        // 1. Cultivation-слот: пассивная техника культивации.
        var cultivation = _techniqueGenerator.GenerateSpecified(
            TechniqueType.Cultivation, 1, level, seed + 1);
        if (_techniques.LearnTechnique(cultivation)) granted++;

        // 2. Combat-пул: наполняем до отказа слота (попыток больше, чем слотов —
        // часть может не пройти из-за повторных ID/уровня).
        int combatSlots = TechniqueService.SlotCapacity(TechniqueType.Combat, level);
        const int maxAttempts = 40;
        int attempts = 0;
        int grantedActive = 0;
        while (grantedActive < combatSlots && attempts < maxAttempts)
        {
            var type = ActivePoolTypes[attempts % ActivePoolTypes.Length];
            var tech = _techniqueGenerator.GenerateSpecified(
                type, level, level, seed + 10 + attempts);
            attempts++;
            if (_techniques.LearnTechnique(tech)) { grantedActive++; granted++; }
        }

        // 3. Curse-слот (требует минимум L2 по резонансу не обязателен, но
        // проклятия на L1 избыточны — даём с L1 для теста).
        var curse = _techniqueGenerator.GenerateSpecified(
            TechniqueType.Curse, level, level, seed + 500);
        if (_techniques.LearnTechnique(curse)) granted++;

        // 4. Formation-слот: техника создания формаций (этап 5 внедрения).
        var formation = _techniqueGenerator.GenerateSpecified(
            TechniqueType.Formation, level, level, seed + 600);
        if (_techniques.LearnTechnique(formation)) granted++;

        Console.WriteLine($"[Phase {PhaseOrder}] {PhaseName} complete — granted {granted} techniques " +
                          $"(cultivation 1, active {grantedActive}/{combatSlots}, curse, formation) at L{level}");
        return Task.CompletedTask;
    }
}
