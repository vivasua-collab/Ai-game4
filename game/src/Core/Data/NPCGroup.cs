#nullable enable
using System.Collections.Generic;
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Data;

/// <summary>
/// Тип задачи для группы NPC.
/// Определяет поведение группы как надстройку над индивидуальным AI.
/// </summary>
public enum GroupTaskType
{
    /// <summary>Патруль по маршруту (waypoints).</summary>
    Patrol,
    /// <summary>Сопровождение VIP-персонажа (защита + следование).</summary>
    Escort,
    /// <summary>Охрана местности (защита зоны, атака нарушителей).</summary>
    GuardArea,
    /// <summary>Охота стаей (фланговая атака на цель).</summary>
    HuntingPack,
    /// <summary>Торговый караван (движение по маршруту + охрана).</summary>
    TradeCaravan,
}

/// <summary>
/// Роль NPC в группе.
/// </summary>
public enum GroupRole
{
    /// <summary>Лидер — определяет движение группы.</summary>
    Leader,
    /// <summary>Последователь — движется за лидером с formation offset.</summary>
    Follower,
}

/// <summary>
/// Состояние группы NPC.
/// Группа — это надстройка над самостоятельным поведением NPC.
/// У группы есть одна цель (объект или место привязки, маршрут) и задача.
/// </summary>
public sealed class NPCGroup
{
    /// <summary>Уникальный ID группы.</summary>
    public string GroupId { get; }

    /// <summary>Тип задачи группы.</summary>
    public GroupTaskType TaskType { get; set; }

    /// <summary>ID лидера группы (NPC ID или "player").</summary>
    public string LeaderId { get; set; } = string.Empty;

    /// <summary>Список ID участников группы (включая лидера).</summary>
    public List<string> MemberIds { get; } = new();

    /// <summary>Фракция группы (для однотипных NPC).</summary>
    public string Faction { get; set; } = string.Empty;

    /// <summary>Вид группы (для животных: "wolf_pack", "deer_herd").</summary>
    public string Species { get; set; } = string.Empty;

    /// <summary>Маршрут патруля (список точек). null если нет маршрута.</summary>
    public List<Position2D>? PatrolRoute { get; set; }

    /// <summary>Индекс текущей точки маршрута.</summary>
    public int CurrentWaypointIndex { get; set; }

    /// <summary>Центр охраняемой зоны (для GuardArea).</summary>
    public Position2D? GuardCenter { get; set; }

    /// <summary>Радиус охраняемой зоны (тайлы).</summary>
    public float GuardRadius { get; set; } = 5f;

    /// <summary>ID сопровождаемого VIP (для Escort).</summary>
    public string EscortTargetId { get; set; } = string.Empty;

    /// <summary>Активна ли группа.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Текущая цель группы (waypoint, guard center, etc.). Обновляется в Tick.</summary>
    public Position2D? CurrentGroupTarget { get; set; }

    public NPCGroup(string groupId, GroupTaskType taskType, string faction = "", string species = "")
    {
        GroupId = groupId ?? string.Empty;
        TaskType = taskType;
        Faction = faction ?? string.Empty;
        Species = species ?? string.Empty;
    }

    /// <summary>Получить следующую точку маршрута (зацикливание).</summary>
    public Position2D? GetNextWaypoint()
    {
        if (PatrolRoute == null || PatrolRoute.Count == 0) return null;
        return PatrolRoute[CurrentWaypointIndex % PatrolRoute.Count];
    }

    /// <summary>Перейти к следующей точке маршрута.</summary>
    public void AdvanceWaypoint()
    {
        if (PatrolRoute != null && PatrolRoute.Count > 0)
            CurrentWaypointIndex = (CurrentWaypointIndex + 1) % PatrolRoute.Count;
    }
}

/// <summary>
/// Смещение участника группы относительно лидера (formation offset).
/// Используется для построения группы (клин, линия, круг).
/// </summary>
public readonly struct FormationOffset
{
    public readonly int Dx;  // смещение по X (тайлы)
    public readonly int Dy;  // смещение по Y (тайлы)

    public FormationOffset(int dx, int dy) { Dx = dx; Dy = dy; }

    /// <summary>Стандартные смещения для строя из N участников.</summary>
    public static List<FormationOffset> GetLineFormation(int memberCount)
    {
        var offsets = new List<FormationOffset>();
        int half = memberCount / 2;
        for (int i = 0; i < memberCount; i++)
        {
            offsets.Add(new FormationOffset(i - half, 0));
        }
        return offsets;
    }

    /// <summary>Клиновидное построение (V-formation).</summary>
    public static List<FormationOffset> GetWedgeFormation(int memberCount)
    {
        var offsets = new List<FormationOffset>();
        for (int i = 0; i < memberCount; i++)
        {
            int row = i / 2;
            int side = (i % 2 == 0) ? -1 : 1;
            offsets.Add(new FormationOffset(side * (row + 1), -row));
        }
        return offsets;
    }
}
