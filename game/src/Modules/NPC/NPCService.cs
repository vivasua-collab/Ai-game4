#nullable enable
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.NPC;

/// <summary>
/// NPCService — registry of all NPCs. V1 stub: Dictionary&lt;int, NPCState&gt;.
/// ProcessTick runs the 3-tier nervous system (spinal / neural / brain) —
/// V1 just increments counters stored in a side-dictionary (NPCState itself
/// doesn't have tick-counter fields in the Core data model).
/// </summary>
public sealed class NPCService : INPCService
{
    private int _nextId = 1;
    private readonly Dictionary<int, NPCState> _npcs = new();
    private readonly Dictionary<int, (int spinal, int neural, int brain)> _tickCounters = new();
    private readonly NPCConfig _config;

    public NPCService(NPCConfig? config = null) => _config = config ?? new NPCConfig();

    public NPCState GetNPC(int entityId)
    {
        // Per interface contract, returns NPCState (not nullable). If missing,
        // return a default empty state — caller should check GetActiveNPCs first.
        return _npcs.TryGetValue(entityId, out var n) ? n : new NPCState { Id = entityId.ToString(), IsAlive = false };
    }

    public IReadOnlyList<int> GetActiveNPCs()
    {
        var list = new List<int>(_npcs.Count);
        foreach (var kv in _npcs) if (kv.Value.IsAlive) list.Add(kv.Key);
        return list;
    }

    public void SpawnNPC(NPCState state)
    {
        int id = _nextId++;
        // NPCState.Id is a string in the Core data model; mirror it as int key.
        if (string.IsNullOrEmpty(state.Id)) state.Id = "npc_" + id;
        _npcs[id] = state;
        _tickCounters[id] = (0, 0, 0);
        Console.WriteLine($"[NPCService] Spawned NPC id={id} ('{state.Name}') @ {state.Position}");
    }

    public void DespawnNPC(int entityId)
    {
        _npcs.Remove(entityId);
        _tickCounters.Remove(entityId);
        Console.WriteLine($"[NPCService] Despawned NPC {entityId}");
    }

    public void ProcessTick()
    {
        foreach (var kv in _npcs)
        {
            int id = kv.Key;
            var n = kv.Value;
            if (!n.IsAlive) continue;

            var c = _tickCounters[id];
            c.spinal++;
            if (c.spinal % _config.NeuralTickEvery == 0) c.neural++;
            if (c.spinal % _config.BrainTickEvery == 0)
            {
                c.brain++;
                // V1: brain tier logs occasionally
                if (c.brain % 3 == 0)
                {
                    Console.WriteLine($"[NPCService] NPC {id} ('{n.Name}') brain tick #{c.brain}");
                }
            }
            _tickCounters[id] = c;
        }
    }
}
