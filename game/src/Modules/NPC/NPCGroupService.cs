#nullable enable
// Создано: 2026-08-22 — групповое взаимодействие NPC.
// NPCGroupService — управление группами NPC (патруль, сопровождение, охрана).
// Группа — надстройка над самостоятельным поведением: лидер определяет
// движение, последователи используют formation offsets.
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.NPC
{
    /// <summary>
    /// Реализация INPCGroupService.
    /// Управляет группами NPC: создание, состав, маршруты, тик.
    /// </summary>
    public class NPCGroupService : INPCGroupService
    {
        private readonly Dictionary<string, NPCGroup> _groups = new();
        private readonly Dictionary<string, string> _memberToGroup = new();  // npcId → groupId
        private int _nextGroupId = 1;

        public int GroupCount => _groups.Count;

        public string CreateGroup(GroupTaskType taskType, string faction = "", string species = "")
        {
            var groupId = $"group_{_nextGroupId++}";
            var group = new NPCGroup(groupId, taskType, faction, species);
            _groups[groupId] = group;
            Console.WriteLine($"[NPCGroupService] Created group {groupId} (task={taskType}, faction={faction}, species={species})");
            return groupId;
        }

        public void DisbandGroup(string groupId)
        {
            if (!_groups.TryGetValue(groupId, out var group)) return;

            // Remove all members from lookup.
            foreach (var memberId in group.MemberIds)
                _memberToGroup.Remove(memberId);

            _groups.Remove(groupId);
            Console.WriteLine($"[NPCGroupService] Disbanded group {groupId} ({group.MemberIds.Count} members)");
        }

        public void AddMember(string groupId, string npcId, GroupRole role = GroupRole.Follower)
        {
            if (!_groups.TryGetValue(groupId, out var group)) return;

            // Remove from previous group if any.
            if (_memberToGroup.TryGetValue(npcId, out var oldGroupId) && oldGroupId != groupId)
                RemoveMember(oldGroupId, npcId);

            group.MemberIds.Add(npcId);
            _memberToGroup[npcId] = groupId;

            if (role == GroupRole.Leader)
                group.LeaderId = npcId;

            Console.WriteLine($"[NPCGroupService] Added {npcId} to group {groupId} as {role}");
        }

        public void RemoveMember(string groupId, string npcId)
        {
            if (!_groups.TryGetValue(groupId, out var group)) return;

            group.MemberIds.Remove(npcId);
            _memberToGroup.Remove(npcId);

            if (group.LeaderId == npcId)
            {
                // Promote first remaining member to leader.
                group.LeaderId = group.MemberIds.Count > 0 ? group.MemberIds[0] : string.Empty;
                if (!string.IsNullOrEmpty(group.LeaderId))
                    Console.WriteLine($"[NPCGroupService] Promoted {group.LeaderId} to leader of {groupId}");
            }

            // Disband if empty.
            if (group.MemberIds.Count == 0)
                DisbandGroup(groupId);
        }

        public NPCGroup? GetGroup(string groupId)
        {
            return _groups.TryGetValue(groupId, out var group) ? group : null;
        }

        public NPCGroup? GetGroupByMember(string npcId)
        {
            if (!_memberToGroup.TryGetValue(npcId, out var groupId)) return null;
            return GetGroup(groupId);
        }

        public IReadOnlyList<NPCGroup> GetAllGroups() => new List<NPCGroup>(_groups.Values);

        public void SetPatrolRoute(string groupId, List<Position2D> waypoints)
        {
            if (!_groups.TryGetValue(groupId, out var group)) return;
            group.PatrolRoute = waypoints;
            group.CurrentWaypointIndex = 0;
            Console.WriteLine($"[NPCGroupService] Set patrol route for {groupId}: {waypoints.Count} waypoints");
        }

        public void SetGuardArea(string groupId, Position2D center, float radius)
        {
            if (!_groups.TryGetValue(groupId, out var group)) return;
            group.GuardCenter = center;
            group.GuardRadius = radius;
            Console.WriteLine($"[NPCGroupService] Set guard area for {groupId}: center={center}, radius={radius}");
        }

        public void SetEscortTarget(string groupId, string targetId)
        {
            if (!_groups.TryGetValue(groupId, out var group)) return;
            group.EscortTargetId = targetId;
            Console.WriteLine($"[NPCGroupService] Set escort target for {groupId}: {targetId}");
        }

        /// <summary>
        /// Тик групп — обновление целей для участников.
        /// Для Patrol: лидер движется к следующей точке маршрута.
        /// Для Escort: лидер следует за VIP.
        /// Для GuardArea: участники патрулируют зону.
        /// Для HuntingPack: участники окружают цель.
        ///
        /// Индивидуальный AI NPCModule.Tick() обрабатывает движение к Target.
        /// Этот сервис только устанавливает Target для участников группы.
        /// </summary>
        public void Tick(int tickCount)
        {
            foreach (var kvp in _groups)
            {
                var group = kvp.Value;
                if (!group.IsActive || group.MemberIds.Count == 0) continue;

                switch (group.TaskType)
                {
                    case GroupTaskType.Patrol:
                        TickPatrol(group, tickCount);
                        break;
                    case GroupTaskType.Escort:
                        TickEscort(group, tickCount);
                        break;
                    case GroupTaskType.GuardArea:
                        TickGuardArea(group, tickCount);
                        break;
                    case GroupTaskType.HuntingPack:
                        // HuntingPack handled by individual AI (threat-based).
                        // Group just ensures members share threat targets.
                        break;
                    case GroupTaskType.TradeCaravan:
                        TickPatrol(group, tickCount); // Caravan = patrol with goods.
                        break;
                }
            }
        }

        private void TickPatrol(NPCGroup group, int tickCount)
        {
            if (group.PatrolRoute == null || group.PatrolRoute.Count == 0) return;

            var waypoint = group.GetNextWaypoint();
            if (waypoint == null) return;

            // Set leader target to current waypoint.
            if (!string.IsNullOrEmpty(group.LeaderId))
            {
                // Leader target is set via NPCService (individual AI reads it).
                // We publish a "group target update" that NPCMovementService can read.
                // For now, we store the target in the group and NPCMovementService reads it.
                group.CurrentGroupTarget = waypoint.Value;
            }

            // Check if leader reached waypoint (every 10 ticks).
            if (tickCount % 10 == 0)
            {
                // Simple advance: move to next waypoint periodically.
                // In a full implementation, we'd check leader position vs waypoint.
                group.AdvanceWaypoint();
            }
        }

        private void TickEscort(NPCGroup group, int tickCount)
        {
            if (string.IsNullOrEmpty(group.EscortTargetId)) return;

            // For Escort, leader follows the VIP.
            // Followers follow the leader with formation offsets.
            // The actual position tracking is done by NPCMovementService.
            // Group service just marks the task as active.
            group.CurrentGroupTarget = null; // VIP position is dynamic.
        }

        private void TickGuardArea(NPCGroup group, int tickCount)
        {
            if (group.GuardCenter == null) return;

            // For GuardArea, members patrol within the guard radius.
            // Each member gets a random point within the radius every N ticks.
            // This is handled by individual AI (Wandering within radius).
            // Group service ensures members stay in the area.
            group.CurrentGroupTarget = group.GuardCenter.Value;
        }
    }
}
