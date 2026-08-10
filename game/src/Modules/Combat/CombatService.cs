#nullable enable
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Combat;

/// <summary>
/// CombatService — tracks active combats. V1 stub: just maintains a set of
/// combatant IDs. Damage calc is a flat formula based on the technique's
/// EffectiveDamage field.
/// </summary>
public sealed class CombatService : ICombatService
{
    private readonly HashSet<int> _inCombat = new();
    private readonly CombatConfig _config;

    public CombatService(CombatConfig? config = null) => _config = config ?? new CombatConfig();

    public bool IsInCombat(int entityId) => _inCombat.Contains(entityId);

    public void ProcessAttack(int attackerId, int targetId, TechniqueData technique)
    {
        _inCombat.Add(attackerId);
        _inCombat.Add(targetId);
        float dmg = CalculateDamage(attackerId, targetId, technique);
        Console.WriteLine($"[CombatService] {attackerId} hits {targetId} for {dmg:F1} (tech={technique?.Name ?? "none"})");
    }

    public float CalculateDamage(int attackerId, int targetId, TechniqueData technique)
    {
        // V1 stub formula: base + technique's qi cost as a damage proxy
        float dmg = _config.BaseDamage;
        if (technique != null)
        {
            dmg += technique.QiCost * 0.5f;
        }
        return Math.Max(1f, dmg);
    }

    /// <summary>Internal — exits combat for an entity. Not on interface.</summary>
    public void ExitCombat(int entityId)
    {
        _inCombat.Remove(entityId);
    }

    /// <summary>Internal — active combatant IDs. Not on interface.</summary>
    public IReadOnlyCollection<int> GetActiveCombatantIds() => _inCombat;
}
