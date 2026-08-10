#nullable enable
using System.Collections.Generic;
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Interfaces;

/// <summary>
/// NPC lifecycle + AI dispatch. Cap at <c>MAX_ACTIVE_NPCS = 100</c>.
/// Three-tier AI cadence: Spinal (every tick), Neural (~3 ticks), Brain (~10 ticks).
/// </summary>
public interface INPCService
{
    NPCState GetNPC(int entityId);
    IReadOnlyList<int> GetActiveNPCs();
    void SpawnNPC(NPCState state);
    void DespawnNPC(int entityId);
    void ProcessTick();
}
