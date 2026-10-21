#nullable enable
// Создано: 2026-08-22 — групповое взаимодействие NPC.
// Редактировано: 2026-10-20 — Этап-4 аудита (GAP-014): + ISaveable — группы
//   (состав/лидер/маршрут/тип задачи) переживают сейв-лоад-цикл. Прежде
//   сервис реализовывал только INPCGroupService: восстановленные из сейва
//   NPC теряли групповые связи — патрули/караваны/стаи исчезали до
//   пересборки мира (групповые фазы спавна имеют SkipOnLoad=true).
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
    /// Этап-4 (GAP-014): + ISaveable (блок "npc_groups") — групповое
    /// поведение (патруль/караван/охотничья стая) — мировое состояние,
    /// а не transient-эффект: состав, лидер, маршрут и тип задачи
    /// восстанавливаются из сейва (после блока "npc" — контракт RestoreOrder).
    /// </summary>
    public class NPCGroupService : INPCGroupService, ISaveable
    {
        private readonly Dictionary<string, NPCGroup> _groups = new();
        private readonly Dictionary<string, string> _memberToGroup = new();  // npcId → groupId
        private int _nextGroupId = 1;

        // Этап-4 (GAP-014): для валидации участников при RestoreState —
        // группа ссылается на NPC И зверей (волчья стая/стадо оленей —
        // участники из AnimalService): «npc» и «animals» восстанавливаются
        // РАНЬШЕ «npc_groups» (контракт RestoreOrder агрегатора).
        private readonly NPCService _npcService;
        private readonly IAnimalService _animalService;

        public NPCGroupService(NPCService npcService, IAnimalService animalService)
        {
            _npcService = npcService ?? throw new ArgumentNullException(nameof(npcService));
            _animalService = animalService ?? throw new ArgumentNullException(nameof(animalService));
        }

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

        /// <summary>
        /// R14-аудит (P2-1): сброс реестра групп при пересборке мира в том же
        /// процессе (меню → NewGame/LoadGame). Сервис — DI-синглтон на весь
        /// процесс: без сброса группы прошлого мира (и их членство) оставались
        /// бы в _groups/_memberToGroup поверх новых.
        /// </summary>
        public void ResetWorld()
        {
            int groups = _groups.Count;
            _groups.Clear();
            _memberToGroup.Clear();
            _nextGroupId = 1;
            Console.WriteLine($"[NPCGroupService] ResetWorld: {groups} групп очищено");
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

        // === ISaveable (Этап-4, GAP-014) ================================

        /// <summary>Ключ блока сохранения групп NPC.</summary>
        public string SaveKey => "npc_groups";

        /// <summary>Типизированный state-блок для round-trip десериализации.</summary>
        public Type StateType => typeof(NPCGroupSaveData);

        /// <summary>
        /// Снимок всех групп: ID, тип задачи, лидер, участники (joined "|" —
        /// конвенция NPCSaveEntry), фракция/вид, маршрут (flat X/Y), позиция
        /// в маршруте, зона охраны, цель сопровождения, активность.
        /// CurrentGroupTarget НЕ сохраняется — runtime-значение, пересчитывается Tick-ом.
        /// NextGroupId — чтобы новые группы после загрузки не коллизировались
        /// с восстановленными ID (CreateGroup перезаписал бы group_N).
        /// </summary>
        public object CaptureState()
        {
            var entries = new NPCGroupSaveEntry[_groups.Count];
            int i = 0;
            foreach (var kvp in _groups)
            {
                var g = kvp.Value;
                var route = g.PatrolRoute;
                int routeCount = route?.Count ?? 0;
                var px = new int[routeCount];
                var py = new int[routeCount];
                if (route != null)
                {
                    for (int j = 0; j < routeCount; j++) { px[j] = route[j].X; py[j] = route[j].Y; }
                }

                var memberIds = new string[g.MemberIds.Count];
                for (int j = 0; j < memberIds.Length; j++) memberIds[j] = g.MemberIds[j] ?? "";

                entries[i++] = new NPCGroupSaveEntry
                {
                    GroupId = g.GroupId ?? "",
                    TaskType = (int)g.TaskType,
                    LeaderId = g.LeaderId ?? "",
                    MemberIdsJoined = string.Join("|", memberIds),
                    Faction = g.Faction ?? "",
                    Species = g.Species ?? "",
                    CurrentWaypointIndex = g.CurrentWaypointIndex,
                    PatrolRouteCount = routeCount,
                    PatrolX = px,
                    PatrolY = py,
                    HasGuardCenter = g.GuardCenter.HasValue,
                    GuardCenterX = g.GuardCenter?.X ?? 0,
                    GuardCenterY = g.GuardCenter?.Y ?? 0,
                    GuardRadius = g.GuardRadius,
                    EscortTargetId = g.EscortTargetId ?? "",
                    IsActive = g.IsActive,
                };
            }
            return new NPCGroupSaveData
            {
                GroupCount = _groups.Count,
                NextGroupId = _nextGroupId,
                Entries = entries,
            };
        }

        /// <summary>
        /// Восстановление групп из сейва. Контракты:
        ///  • полная пересборка (не merge): перед RestoreState домены сброшены
        ///   (WorldDomainResetCoordinator), а повторный restore не должен
        ///   дублировать группы;
        ///  • участники валидируются по реестрам NPCService И AnimalService —
        ///   блоки "npc" и "animals" восстанавливаются РАНЬШЕ (RestoreOrder);
        ///   участник, которого нет нигде (битый сейв/вырезан), отбрасывается
        ///   с логом;
        ///  • лидер отсутствует → промоут первый валидный участник (та же
        ///   семантика, что RemoveMember);
        ///  • группа без валидных участников не восстанавливается (инвариант
        ///   RemoveMember: пустая группа расформируется).
        /// </summary>
        public void RestoreState(object state)
        {
            if (state is not NPCGroupSaveData data) return;

            _groups.Clear();
            _memberToGroup.Clear();
            _nextGroupId = 1;
            if (data.Entries == null) return;

            int restored = 0, droppedMembers = 0, droppedGroups = 0, promotedLeaders = 0;
            foreach (var entry in data.Entries)
            {
                if (string.IsNullOrEmpty(entry.GroupId)) continue;

                var group = new NPCGroup(entry.GroupId, (GroupTaskType)entry.TaskType,
                    entry.Faction ?? "", entry.Species ?? "");

                // Участники — только присутствующие в реестрах: NPC
                // (HumanNPCSpawnPhase) ИЛИ звери (GroupSpawnPhase: волчья
                // стая/стадо оленей — участники из AnimalService).
                var joined = entry.MemberIdsJoined ?? "";
                var members = joined.Length == 0
                    ? Array.Empty<string>()
                    : joined.Split('|');
                foreach (var memberId in members)
                {
                    if (string.IsNullOrEmpty(memberId)) continue;
                    if (_npcService.GetNPCState(memberId) == null
                        && _animalService.TryGetAnimal(memberId) == null)
                    {
                        droppedMembers++;
                        Console.WriteLine($"[NPCGroupService] Restore: участник '{memberId}' группы '{entry.GroupId}' отсутствует в реестрах NPC/зверей — отброшен");
                        continue;
                    }
                    group.MemberIds.Add(memberId);
                    _memberToGroup[memberId] = group.GroupId;
                }

                if (group.MemberIds.Count == 0)
                {
                    droppedGroups++;
                    Console.WriteLine($"[NPCGroupService] Restore: группа '{entry.GroupId}' без валидных участников — не восстановлена");
                    continue;
                }

                // Лидер: из сейва, если он среди участников; иначе — промоут
                // первого (семантика RemoveMember: пустая лидер-позиция не живёт).
                if (group.MemberIds.Contains(entry.LeaderId))
                {
                    group.LeaderId = entry.LeaderId;
                }
                else
                {
                    group.LeaderId = group.MemberIds[0];
                    promotedLeaders++;
                    if (!string.IsNullOrEmpty(entry.LeaderId))
                        Console.WriteLine($"[NPCGroupService] Restore: лидер '{entry.LeaderId}' группы '{entry.GroupId}' не в составе — промоут '{group.LeaderId}'");
                }

                // Маршрут (flat X/Y — конвенция Волны 3).
                if (entry.PatrolRouteCount > 0 && entry.PatrolX != null && entry.PatrolY != null)
                {
                    int n = System.Math.Min(entry.PatrolRouteCount, System.Math.Min(entry.PatrolX.Length, entry.PatrolY.Length));
                    var route = new List<Position2D>(n);
                    for (int j = 0; j < n; j++)
                        route.Add(new Position2D(entry.PatrolX[j], entry.PatrolY[j]));
                    group.PatrolRoute = route;
                }
                group.CurrentWaypointIndex = entry.CurrentWaypointIndex;

                // Зона охраны (nullable-структура → флаг наличия + координаты).
                if (entry.HasGuardCenter)
                    group.GuardCenter = new Position2D(entry.GuardCenterX, entry.GuardCenterY);
                group.GuardRadius = entry.GuardRadius;
                group.EscortTargetId = entry.EscortTargetId ?? "";
                group.IsActive = entry.IsActive;

                _groups[group.GroupId] = group;
                restored++;
            }

            // Счётчик ID новых групп — не назад/вперёд поверх восстановленных.
            if (data.NextGroupId > _nextGroupId)
                _nextGroupId = data.NextGroupId;

            Console.WriteLine($"[NPCGroupService] RestoreState: {restored} групп восстановлено " +
                $"(участников отброшено: {droppedMembers}, групп без состава: {droppedGroups}, промоутов лидера (повышений первого участника): {promotedLeaders}); nextGroupId={_nextGroupId}");
        }
    }

    /// <summary>Типизированный state-блок групп NPC (round-trip persistence).</summary>
    [Serializable]
    public struct NPCGroupSaveData
    {
        public int GroupCount;
        public int NextGroupId;
        public NPCGroupSaveEntry[] Entries;
    }

    /// <summary>
    /// Сериализуемая запись группы (flat-массивы + joined-строки — конвенция
    /// NPCSaveEntry Волны 3: совместимость с IncludeFields-конвейером SaveJson).
    /// </summary>
    [Serializable]
    public struct NPCGroupSaveEntry
    {
        public string GroupId;
        public int TaskType;
        public string LeaderId;
        public string MemberIdsJoined;
        public string Faction;
        public string Species;
        public int CurrentWaypointIndex;
        public int PatrolRouteCount;
        public int[] PatrolX;
        public int[] PatrolY;
        public bool HasGuardCenter;
        public int GuardCenterX;
        public int GuardCenterY;
        public float GuardRadius;
        public string EscortTargetId;
        public bool IsActive;
    }
}
