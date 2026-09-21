#nullable enable
// Создано: 2026-09-21 — R28 «Хоуминг A: снаряды-сущности» (план
//   checkpoints/plans/2026-09-20_r23_multicombat_aoe_design.md §3.2-A,
//   подтверждён пользователем §6.6: Reynolds seek + ограничение поворота).
//
// ЗАЧЕМ: самонаведение БЕЗ «turn on a dime» — снаряд не разворачивается
// мгновенно (анти-паттерн из gamedev-практик): доворот ≤ 45°/тик →
// кайт честен (AGI-цель отрывается на поворотах), погоня осмысленна.
//
// РЕАЛИЗАЦИЯ: 8 октантов направлений (Чебышёв-метрика мира: скорость
// ЕДИНА по всем 8 направлениям — диагональ = тайл). Вся математика int
// (ЗАПРЕТ 3.9): позиция в МИЛЛИ-ТАЙЛАХ (×1000), октант из дельты —
// сравнение |2dy| vs |dx| (без float/тригонометрии).
//
// Публичные статические методы — юнит-тестируемы (COMBAT_SIM 3j) без
// сервисов, как AoEResolver.IsPointInside.
using System;

namespace CultivationGame.Modules.Combat;

/// <summary>
/// R28: наведение снаряда-сущности (Reynolds seek + turn-budget 45°/тик).
/// </summary>
public static class ProjectileSteering
{
    /// <summary>Дефолтная скорость снаряда (тайлов/сек, Чебышёв).</summary>
    public const int DefaultSpeedTilesPerSec = 8;

    /// <summary>Время жизни (сек): 5с — «Ци рассеивается вдали от мастера».</summary>
    public const float LifeSec = 5f;

    /// <summary>Порог контакта (милли-тайлы, Чебышёв): «рядом с целью».</summary>
    public const int ContactRangeMilliTiles = 1000;

    /// <summary>
    /// Октант направления из дельты (милли-тайлы): 0=(+X) 1=(+X+Y) 2=(+Y)
    /// 3=(-X+Y) 4=(-X) 5=(-X-Y) 6=(-Y) 7=(+X-Y). Границы октанта — без
    /// тригонометрии: 2|dy| ≤ |dx| → ось X; 2|dx| ≤ |dy| → ось Y; иначе диагональ.
    /// </summary>
    public static int OctantFromDelta(int dx, int dy)
    {
        int adx = Math.Abs(dx), ady = Math.Abs(dy);
        bool xDom = ady * 2 <= adx;
        bool yDom = adx * 2 <= ady;
        bool px = dx >= 0, py = dy >= 0;

        if (xDom) return px ? 0 : 4;
        if (yDom) return py ? 2 : 6;
        if (px && py) return 1;
        if (!px && py) return 3;
        if (!px && !py) return 5;
        return 7;
    }

    /// <summary>Единичный вектор октанта (компоненты {-1,0,1}; Чебышёв-мир).</summary>
    public static void VectorFromOctant(int octant, out int dx, out int dy)
    {
        switch (((octant % 8) + 8) % 8)
        {
            case 0: dx = 1; dy = 0; return;
            case 1: dx = 1; dy = 1; return;
            case 2: dx = 0; dy = 1; return;
            case 3: dx = -1; dy = 1; return;
            case 4: dx = -1; dy = 0; return;
            case 5: dx = -1; dy = -1; return;
            case 6: dx = 0; dy = -1; return;
            default: dx = 1; dy = -1; return;
        }
    }

    /// <summary>
    /// Turn-budget: доворот к желаемому октанту НЕ БОЛЕЕ 45° (±1 октант) за
    /// тик — анти «turn on a dime». Кратчайшая сторона (по/против часовой).
    /// 180°-разворот = 4 тика честной погони.
    /// </summary>
    public static int TurnToward(int currentOctant, int desiredOctant)
    {
        int cur = ((currentOctant % 8) + 8) % 8;
        int des = ((desiredOctant % 8) + 8) % 8;
        int diff = (des - cur + 8) % 8; // 0..7
        if (diff == 0) return cur;
        // 1..3 → +1 (кратчайшая против часовой); 5..7 → -1; 4 — любой (берём +1)
        if (diff <= 4) return (cur + 1) % 8;
        return (cur + 7) % 8;
    }

    /// <summary>
    /// Скорость в милли-тайлах/сек: тайлы/сек × 1000 (int, ЗАПРЕТ 3.9).
    /// Позиция тика: pos += vector × speedMilliPerSec × deltaTime (физика
    /// времени — float Time API, как pendingCasts/_combatTimer).
    /// </summary>
    public static int SpeedMilliPerSec(int tilesPerSec)
        => Math.Max(1, tilesPerSec) * 1000;
}
