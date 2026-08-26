#nullable enable
using System.Collections.Generic;
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Interfaces;

/// <summary>
/// Сервис управления группами NPC.
/// Группа — это надстройка над самостоятельным поведением NPC.
/// У группы есть одна цель (объект или место привязки, маршрут) и задача.
///
/// Сервис управляет:
/// - Созданием/расформированием групп
/// - Назначением лидера и участников
/// - Вычислением целей для участников (formation offsets, patrol routes)
/// - Тиком групп (обновление целей для участников)
/// </summary>
public interface INPCGroupService
{
    /// <summary>Количество активных групп.</summary>
    int GroupCount { get; }

    /// <summary>Создать новую группу.</summary>
    /// <param name="taskType">Тип задачи (Patrol/Escort/GuardArea/HuntingPack/TradeCaravan)</param>
    /// <param name="faction">Фракция (для однотипных NPC)</param>
    /// <param name="species">Вид (для животных)</param>
    /// <returns>ID созданной группы</returns>
    string CreateGroup(GroupTaskType taskType, string faction = "", string species = "");

    /// <summary>Расформировать группу.</summary>
    void DisbandGroup(string groupId);

    /// <summary>Добавить NPC в группу.</summary>
    /// <param name="groupId">ID группы</param>
    /// <param name="npcId">ID NPC</param>
    /// <param name="role">Роль (Leader/Follower)</param>
    void AddMember(string groupId, string npcId, GroupRole role = GroupRole.Follower);

    /// <summary>Удалить NPC из группы.</summary>
    void RemoveMember(string groupId, string npcId);

    /// <summary>Получить группу по ID.</summary>
    NPCGroup? GetGroup(string groupId);

    /// <summary>Получить группу, в которой состоит NPC.</summary>
    NPCGroup? GetGroupByMember(string npcId);

    /// <summary>Получить все активные группы.</summary>
    IReadOnlyList<NPCGroup> GetAllGroups();

    /// <summary>Установить маршрут патруля для группы.</summary>
    void SetPatrolRoute(string groupId, List<Position2D> waypoints);

    /// <summary>Установить зону охраны для группы.</summary>
    void SetGuardArea(string groupId, Position2D center, float radius);

    /// <summary>Установить цель сопровождения.</summary>
    void SetEscortTarget(string groupId, string targetId);

    /// <summary>
    /// Тик групп — обновление целей для участников.
    /// Вызывается из NPCModule.Tick().
    /// </summary>
    void Tick(int tickCount);
}
