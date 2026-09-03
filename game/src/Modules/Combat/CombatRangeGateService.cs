#nullable enable
// Создано: 2026-09-03 — Phase 8 ч.3: гейты дальнего боя (LOS + боеприпасы).
//
// ЗАЧЕМ: две проверки физкорректности ranged-атак, которые до этого
// отсутствовали (стрельба сквозь стены + бесконечные стрелы):
//   1. LOS — Bresenham-линия от атакующего к цели (CombatLos);
//   2. Ammo — расход 1 стрелы на выстрел (инвентарь игрока).
//
// КТО СПРАШИВАЕТ:
//   • CombatModule.OnAttackIntent — авторитетный гейт ВСЕХ ranged-интентов
//     (игрок + NPC + sim) ДО старта боя и расходников;
//   • CombatSimDebug 3d — headless-верификация обеих проверок;
//   • CheatPanel — статус колчана.
//
// АРХИТЕКТУРА: кросс-модульные инъекции — по паттерну «sanctioned
// exceptions» (как PlayerCombatAdapter→INPCService): позиционные и
// инвентарные данные нужны ДЛЯ проверки, событие отклонения публикует
// вызывающий (CombatModule) — событийный поток не нарушается.
using System;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Helpers;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Combat;

/// <summary>
/// Phase 8 ч.3: гейт дальнего боя — линия огня + расход боеприпасов.
/// </summary>
public sealed class CombatRangeGateService
{
    /// <summary>
    /// ID предмета «стрела» (регистрирует StartingGearPhase, §10 стартовый
    /// набор; sim/читы добирают через IItemDatabaseService по этому ID).
    /// </summary>
    public const string ArrowItemId = "ammo_arrow";

    [Inject] private readonly ITileService? _tiles = null;
    [Inject] private readonly IPlayerService? _player = null;
    [Inject] private readonly INPCService? _npcs = null;
    [Inject] private readonly IInventoryService? _inventory = null;

    /// <summary>
    /// Есть ли линия огня между двумя сущностями (игрок/NPC в любых
    /// комбинациях). Неизвестные сущности → true (не блокируем:
    /// животные/будущие сущности без позиционного резолва).
    /// </summary>
    public bool HasLineOfSight(string attackerId, string targetId)
    {
        if (!TryResolveTile(attackerId, out int ax, out int ay)) return true;
        if (!TryResolveTile(targetId, out int tx, out int ty)) return true;
        return CombatLos.HasLineOfSight(_tiles, ax, ay, tx, ty);
    }

    /// <summary>
    /// Расход 1 стрелы на выстрел. Игрок — из инвентаря (нет стрел →
    /// false); NPC — безлимитно (MVP: у NPC нет инвентаря; «колчан
    /// NPC» — отложенная задача). true = стрела списана, можно стрелять.
    /// </summary>
    public bool TryConsumeRangedAmmo(string attackerId)
    {
        // NPC стреляют «из экипировки» — расход не списываем (MVP).
        if (!PlayerIdResolver.IsPlayer(attackerId)) return true;
        if (_inventory == null) return true; // инвентаря нет — не блокируем

        if (_inventory.GetItemCount(ArrowItemId) <= 0) return false;
        return _inventory.TryRemoveItem(ArrowItemId, 1);
    }

    /// <summary>Сколько стрел у игрока (для чит-статуса и sim-диагностики).</summary>
    public int GetArrowCount()
        => _inventory?.GetItemCount(ArrowItemId) ?? 0;

    /// <summary>Тайловая позиция сущности: игрок (оба исторических ID) или NPC.</summary>
    private bool TryResolveTile(string entityId, out int x, out int y)
    {
        x = y = -1;

        if (PlayerIdResolver.IsPlayer(entityId))
        {
            if (_player == null) return false;
            x = _player.Position.X;
            y = _player.Position.Y;
            return true;
        }

        var npc = _npcs?.GetNPC(entityId);
        if (npc != null)
        {
            x = npc.Position.X;
            y = npc.Position.Y;
            return true;
        }
        return false; // животные/неизвестные — LOS не проверяем
    }
}
