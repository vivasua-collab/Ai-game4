#nullable enable
// Создано: 2026-05-08 10:07:00 UTC
// Редактировано: 2026-05-20 18:00:11 UTC — Фаза 1: INPCSpawnerService + новая сигнатура SpawnNPC (C8)
// Редактировано: 2026-05-22 07:55:00 UTC — Аудит MED-1: +GetNPCState() для DI compliance
// Редактировано: 2026-05-23 — IMPL-1: NPCState moved from Modules.NPC.Data to Core.Data
using System.Collections.Generic;
using CultivationGame.Core.Data;

using CultivationGame.Core;
namespace CultivationGame.Core.Interfaces
{
    public interface INPCService
    {
        NPCData GetNPC(string npcId);
        IReadOnlyList<string> GetNearbyNPCIds(Position2D position, float range);
        Attitude GetAttitude(string npcId, string targetId);
        void ModifyAttitude(string npcId, string targetId, int delta);
        bool IsAlive(string npcId);
        NPCAIState GetAIState(string npcId);
        IReadOnlyList<string> GetAllNPCIds();
        void SetAIState(string npcId, NPCAIState state);
        void UpdatePosition(string npcId, Position2D position);

        /// <summary>
        /// Получить NPCState напрямую (для статов, стихии, материала).
        /// Аудит MED-1: добавлен в INPCService для DI compliance —
        /// StatProviderAdapter теперь инжектит INPCService вместо конкретного NPCService.
        /// </summary>
        NPCState GetNPCState(string npcId);
    }

    public interface INPCSpawnerService
    {
        /// <summary>
        /// Спавн NPC по пресету (legacy-сигнатура).
        /// Используйте SpawnNPC с speciesId для полной генерации.
        /// </summary>
        [System.Obsolete("Используйте SpawnNPC с speciesId, roleId, locationLevel")]
        string SpawnNPC(string presetId, Position2D position);

        /// <summary>
        /// Спавн NPC через полный пайплайн генерации.
        /// speciesId — идентификатор вида ("human", "wolf", ...)
        /// roleId — роль NPC
        /// locationLevel — уровень локации (0-10)
        /// position — позиция в мире
        /// seed — seed для детерминированной генерации
        /// </summary>
        string SpawnNPC(string speciesId, NPCRole roleId, int locationLevel, Position2D position, long seed);

        void DespawnNPC(string npcId);
        IReadOnlyList<string> GetSpawnedNPCIds();
        int ActiveNPCCount { get; }
    }
}
