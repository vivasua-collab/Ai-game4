#nullable enable
// Создано: 2026-09-03 — Phase 8 ч.3: линия огня для дальнего боя.
//
// ПРОБЛЕМА: ranged-атака (лук) проверяла только дистанцию — стрела летела
// СКВОЗЬ деревья и камни (механика «walls don't stop arrows»). Физическая
// боевка (основной приоритет MVP) требует честной траектории.
//
// РЕШЕНИЕ: Bresenham-линия от атакующего к цели; промежуточные тайлы
// (кроме крайних) должны быть «прозрачными» для снаряда.
//
// ЧТО БЛОКИРУЕТ СТРЕЛУ (TILE_SYSTEM.md §2, IsPassable=false):
//   • деревья (Tree_Oak/Pine/Birch), камни (Rock_Small/Medium/Large)
// ЧТО НЕ БЛОКИРУЕТ:
//   • вода (Water_Deep непроходима для ХОДЬБЫ, но стрела летит над ней)
//   • лава, Void-террейн — аналогично
//   • кусты (Bush — проходим), руда/трава/сундуки — низкие объекты
//
// Паттерн: чистая статика без состояния (integer math, ЗАПРЕТ 3.9-style).
// Потребители: CombatRangeGateService (module-гейт), PlayerCombatAdapter
// (LOS-фильтр выбора цели), CombatSimDebug (headless-верификация 3d).
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Combat;

/// <summary>
/// Phase 8 ч.3: линия огня (line of sight) для дальнего боя.
/// Bresenham по тайловой сетке; блокиратор — непроходимый ОБЪЕКТ
/// (дерево/камень), террейн не блокирует (стрела летит над водой).
/// </summary>
public static class CombatLos
{
    /// <summary>
    /// Есть ли линия огня от (x0,y0) до (x1,y1).
    /// Крайние тайлы НЕ проверяются (там стоят сам стрелок и цель).
    /// </summary>
    public static bool HasLineOfSight(
        ITileService tiles, int x0, int y0, int x1, int y1)
    {
        if (tiles == null) return true; // нет сетки — не блокируем (fallback)

        // Bresenham (integer): все точки отрезка по тайлам.
        int dx = System.Math.Abs(x1 - x0);
        int dy = System.Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        int x = x0, y = y0;
        while (true)
        {
            // Промежуточные тайлы траектории: стартовый (стрелок) и
            // конечный (цель) не проверяем — стоять «в дереве» можно
            // легально (спавн/телепорт), блокирует только путь между.
            if (!(x == x0 && y == y0) && !(x == x1 && y == y1))
            {
                if (BlocksLineOfFire(tiles.GetTile(x, y)))
                    return false;
            }

            if (x == x1 && y == y1) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 < dx) { err += dx; y += sy; }
        }
        return true;
    }

    /// <summary>
    /// Блокирует ли тайл полёт снаряда. Только НЕПРОХОДИМЫЕ объекты
    /// (деревья/камни). Вода/лава/кусты/руда — стрела пролетает.
    /// </summary>
    public static bool BlocksLineOfFire(in GameTile tile)
        => tile.Object != ObjectType.None && !ObjectDefaults.IsPassable(tile.Object);
}
