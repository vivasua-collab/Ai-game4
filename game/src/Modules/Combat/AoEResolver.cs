#nullable enable
// Создано: 2026-09-21 — R25 «AoE-A: мгновенный залп» (план
//   checkpoints/plans/2026-09-20_r23_multicombat_aoe_design.md §2.2-A,
//   выбор пользователя 2026-09-21: вариант A, friendly-fire = нейтралы
//   задеваются, эпицентр = точка прицеливания).
//
// ЗАЧЕМ: сбор целей площадной техники (Subtype=RangedAoe) в форме
// (круг/конус/полукруг/линия) по int-геометрии тайлов (ЗАПРЕТ 3.9 —
// Чебышёв для круга/радиуса, квадрат скалярного произведения для конуса
// без float и тригонометрии). Каждый цель залпа идёт ПОЛНЫМ 11-слойным
// пайплайном (CombatService.BuildAndExecuteDamageRequest) — броня/щит Ци/
// парирование/месть per-target честны для каждого задетого (включая
// нейтралов — «мир жесток», месть включается сама через DamageAppliedEvent).
//
// КТО СПРАШИВАЕТ:
//   • CombatService.ExecuteAoeVolley — резолв целей залпа;
//   • CombatSimDebug 3h (QA) — верификация геометрии/спада/лимита целей.
//
// АРХИТЕКТУРА: кросс-модульные инъекции — паттерн «sanctioned exceptions»
// (как CombatRangeGateService): позиционные данные нужны ДЛЯ проверки,
// событие AoeImpactEvent публикует вызывающий (CombatService).
//
// R23-эп.3 §6.4 ХУК SpareAllies: AllyCheck-делегат (null в v1 — сопартийцев
// нет, «бережное» Пламя включится с party-системой без переделки AoE).
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Helpers;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Combat;

/// <summary>Цель площадного залпа + индивидуальный множитель спада.</summary>
public sealed class AoeTarget
{
    /// <summary>ID сущности (NPC/зверь/игрок) — defender для пайплайна урона</summary>
    public string EntityId = string.Empty;

    /// <summary>Позиция цели в тайлах (диагностика/сортировка)</summary>
    public Position2D Position;

    /// <summary>Множитель силы на этой цели в промилле (1000 = полный, спад к краю)</summary>
    public int FalloffPermil = 1000;
}

/// <summary>Параметры запроса площадного резолва (int-геометрия).</summary>
public struct AoeQuery
{
    /// <summary>ID кастера (исключается из целей)</summary>
    public string CasterId;

    /// <summary>Позиция кастера в тайлах (вершина конуса/источник луча)</summary>
    public Position2D CasterPos;

    /// <summary>Точка прицеливания в тайлах (эпицентр круга / направление конуса)</summary>
    public Position2D AimPos;

    /// <summary>Форма области</summary>
    public AoeShape Shape;

    /// <summary>Радиус/полудлина в тайлах</summary>
    public int RadiusTiles;

    /// <summary>Полуугол конуса (градусы; Semicircle игнорирует — 90)</summary>
    public int HalfAngleDeg;

    /// <summary>Спад урона на краю в промилле (0 = без спада)</summary>
    public int FalloffPermil;

    /// <summary>Максимум целей (0 = без лимита)</summary>
    public int MaxTargets;

    /// <summary>§6.4 хук «бережной» версии: &gt;0 + союзник цели → щадить</summary>
    public int SpareAlliesPermil;
}

/// <summary>
/// R25: резолвер целей площадной техники. Engine-agnostic, int-геометрия.
/// </summary>
public sealed class AoEResolver
{
    // R23-эп.3 §6.4: хук «союзник кастера» (party-аура). null в v1 —
    // сопартийцев нет, SpareAllies всегда «не союзник» (false). Появится
    // party-система → CombatModule/PartyService присвоит предикат, и
    // «Пламя, пощадящее своих» заработает БЕЗ переделки резолвера.
    public Func<string, string, bool>? AllyCheck;

    // Sanctioned exceptions (паттерн CombatRangeGateService): позиции нужны
    // ДЛЯ резолва; опциональность — устойчивость к порядку сборки модулей.
    [Inject] private readonly IPlayerService? _player = null;
    [Inject] private readonly INPCService? _npcs = null;
    [Inject] private readonly IAnimalService? _animals = null;

    /// <summary>Тайловая позиция сущности: игрок (оба алиаса) / NPC / зверь.</summary>
    public bool TryResolveTile(string entityId, out int x, out int y)
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

        var animal = _animals?.TryGetAnimal(entityId);
        if (animal.HasValue && animal.Value.IsValid)
        {
            x = animal.Value.Position.X;
            y = animal.Value.Position.Y;
            return true;
        }
        return false;
    }

    /// <summary>
    /// АУДИТ-0921_2030 Ф3-3 (P2): позиция ТОЛЬКО ЖИВОЙ сущности — семантика
    /// для хоуминг-снарядов (seek). Прежний TryResolveTile позиционен:
    /// мёртвый NPC остаётся в реестре (IsAlive=false), зверь — тоже
    /// (AnimalService держит труп до обыска), поэтому UpdateProjectiles
    /// считал цель «живой», снаряд корректировал траекторию к трупу и
    /// контакт запускал полный damage pipeline по мёртвому. Здесь живость
    /// проверяется явно: NPC — IsAlive, зверь — AnimalInfo.IsAlive,
    /// игрок — позиция (смерть игрока обрабатывается своим доменом).
    /// </summary>
    public bool TryResolveAliveTile(string entityId, out int x, out int y)
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
            if (_npcs != null && !_npcs.IsAlive(entityId)) return false;
            x = npc.Position.X;
            y = npc.Position.Y;
            return true;
        }

        var animal = _animals?.TryGetAnimal(entityId);
        if (animal.HasValue && animal.Value.IsValid)
        {
            if (!animal.Value.IsAlive) return false;
            x = animal.Value.Position.X;
            y = animal.Value.Position.Y;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Собрать цели в форме. Порядок: сортировка по Чебышёв-дистанции от
    /// источника спада (круг — эпицентр, прочие — кастер), срез MaxTargets.
    /// Friendly-fire: НЕЙТРАЛЫ ЗАДЕВАЮТСЯ (решение пользователя — месть
    /// включается сама через DamageAppliedEvent → RetaliateOrFlee).
    /// </summary>
    public List<AoeTarget> Resolve(in AoeQuery q)
    {
        var result = new List<AoeTarget>();
        int radius = Math.Max(1, q.RadiusTiles);

        // Источник спада: круг — эпицентр (взрыв в точке), конус/полукруг/
        // линия — кастер (волна слабеет по мере удаления от мастера).
        int fallCx = q.Shape == AoeShape.Circle ? q.AimPos.X : q.CasterPos.X;
        int fallCy = q.Shape == AoeShape.Circle ? q.AimPos.Y : q.CasterPos.Y;

        // Центр сбора кандидатов: круг — эпицентр, прочие — кастер.
        int searchCx = q.Shape == AoeShape.Circle ? q.AimPos.X : q.CasterPos.X;
        int searchCy = q.Shape == AoeShape.Circle ? q.AimPos.Y : q.CasterPos.Y;
        var searchCenter = new Position2D(searchCx, searchCy);

        var candidates = new List<(string Id, Position2D Pos)>();

        // NPC (радиус + 1 запас на границы формы)
        if (_npcs != null)
        {
            var nearby = _npcs.GetNearbyNPCIds(searchCenter, radius + 1);
            if (nearby != null)
            {
                foreach (var id in nearby)
                {
                    if (!_npcs.IsAlive(id)) continue;
                    var npc = _npcs.GetNPC(id);
                    if (npc == null) continue;
                    if (PlayerIdResolver.AreSameEntity(id, q.CasterId)) continue;
                    candidates.Add((id, npc.Position));
                }
            }
        }

        // Звери (D1-паттерн: волки живут в AnimalService)
        if (_animals != null)
        {
            foreach (var animal in _animals.GetAliveAnimalsInRange(searchCenter, radius + 1))
            {
                if (PlayerIdResolver.AreSameEntity(animal.EntityId, q.CasterId)) continue;
                candidates.Add((animal.EntityId, animal.Position));
            }
        }

        // Игрок (если кастует не игрок — толпа может задеть и его)
        if (_player != null && !PlayerIdResolver.IsPlayer(q.CasterId))
        {
            if (!PlayerIdResolver.AreSameEntity(_player.PlayerId, q.CasterId))
                candidates.Add((_player.PlayerId, _player.Position));
        }

        foreach (var (id, pos) in candidates)
        {
            if (!IsInsideShape(in q, radius, pos)) continue;

            // §6.4 SpareAllies: «бережная» версия щадит союзников (хук).
            if (q.SpareAlliesPermil > 0 && AllyCheck != null && AllyCheck(q.CasterId, id))
                continue;

            // Спад: Чебышёв от источника к цели, clamp в [0..radius]
            int dist = Math.Max(Math.Abs(pos.X - fallCx), Math.Abs(pos.Y - fallCy));
            dist = Math.Min(dist, radius);
            int mult = 1000 - q.FalloffPermil * dist / radius;
            if (mult < 0) mult = 0;

            result.Add(new AoeTarget
            {
                EntityId = id,
                Position = pos,
                FalloffPermil = mult
            });
        }

        // Приоритет ближних + лимит целей (баланс толпы)
        result.Sort((a, b) =>
        {
            int da = Math.Max(Math.Abs(a.Position.X - fallCx), Math.Abs(a.Position.Y - fallCy));
            int db = Math.Max(Math.Abs(b.Position.X - fallCx), Math.Abs(b.Position.Y - fallCy));
            return da.CompareTo(db);
        });
        if (q.MaxTargets > 0 && result.Count > q.MaxTargets)
            result.RemoveRange(q.MaxTargets, result.Count - q.MaxTargets);

        return result;
    }

    /// <summary>
    /// Точка внутри формы. int-математика:
    /// - Circle: Чебышёв от эпицентра ≤ radius;
    /// - Cone/Semicircle: Чебышёв от кастера ≤ radius + угол(v, d) ≤ half:
    ///   квадрат косинуса — рациональные дроби (cos²30=3/4, cos²45=1/2,
    ///   cos²60=1/4, cos²90=0) → сравнение квадратов скалярного
    ///   произведения БЕЗ float/тригонометрии;
    /// - Line: проекция на направление d в [0..radius·|d|] и поперечное
    ///   отклонение |v×d| ≤ |d|² (ширина луча ~1 тайл).
    /// </summary>
        /// <summary>
        /// R25: точка внутри формы (публично для QA-стенда CombatSimDebug 3h
        /// и VFX-фазы — подсветка формы при предпросмотре прицеливания).
        /// Делегирует в IsInsideShape.
        /// </summary>
        public static bool IsPointInside(in AoeQuery q, int radius, Position2D pos)
            => IsInsideShape(in q, radius, pos);

    private static bool IsInsideShape(in AoeQuery q, int radius, Position2D pos)
    {
        int vx = pos.X - q.CasterPos.X;
        int vy = pos.Y - q.CasterPos.Y;

        switch (q.Shape)
        {
            case AoeShape.Circle:
            {
                int dx = pos.X - q.AimPos.X;
                int dy = pos.Y - q.AimPos.Y;
                return Math.Max(Math.Abs(dx), Math.Abs(dy)) <= radius;
            }

            case AoeShape.Semicircle:
                // Полукруг = конус 90° (v·d ≥ 0): всё перед кастером
                return Math.Max(Math.Abs(vx), Math.Abs(vy)) <= radius
                    && Dot(vx, vy, q.AimPos.X - q.CasterPos.X, q.AimPos.Y - q.CasterPos.Y) >= 0;

            case AoeShape.Cone:
            {
                if (Math.Max(Math.Abs(vx), Math.Abs(vy)) > radius) return false;
                int dx = q.AimPos.X - q.CasterPos.X;
                int dy = q.AimPos.Y - q.CasterPos.Y;
                if (dx == 0 && dy == 0) return true; // прицел в кастер — форма «вырождена», берём всех в радиусе
                int dot = Dot(vx, vy, dx, dy);
                // v·d ≥ 0 — цель хотя бы в полупространстве прицела
                if (dot < 0) return false;
                long v2 = (long)vx * vx + (long)vy * vy;
                long d2 = (long)dx * dx + (long)dy * dy;
                long dot2 = (long)dot * dot;
                return HalfAngleCheck(q.HalfAngleDeg, dot2, v2, d2);
            }

            case AoeShape.Line:
            {
                int dx = q.AimPos.X - q.CasterPos.X;
                int dy = q.AimPos.Y - q.CasterPos.Y;
                if (dx == 0 && dy == 0) return Math.Max(Math.Abs(vx), Math.Abs(vy)) <= radius;
                long dot = Dot(vx, vy, dx, dy);
                if (dot < 0) return false;                       // позади кастера
                long d2 = (long)dx * dx + (long)dy * dy;
                // Вдоль луча: (v·d)/|d| ≤ radius ⇔ dot² ≤ radius²·|d|²
                // (dot ≥ 0 уже проверено; квадраты — int-математика, ЗАПРЕТ 3.9)
                if (dot * dot > (long)radius * radius * d2) return false;
                // Поперечное отклонение: |v×d|/|d| ≤ 1 тайл ⇔ |cross|² ≤ |d|²
                long cross = (long)vx * dy - (long)vy * dx;
                return cross * cross <= d2;
            }

            default:
                return false;
        }
    }

    /// <summary>
    /// Угловая проверка конуса через квадраты (int): cos²half для
    /// {30°,45°,60°,90°} = {3/4, 1/2, 1/4, 0} — рациональные дроби,
    /// сравнение без float (ЗАПРЕТ 3.9). Прочие углы — clamp к ближайшему.
    /// Условие «угол(v,d) ≤ half» ⇔ cos²θ ≥ cos²half ⇔ dot² ≥ cos²half·|v|²·|d|².
    /// </summary>
    private static bool HalfAngleCheck(int halfAngleDeg, long dot2, long v2, long d2)
    {
        switch (halfAngleDeg)
        {
            case <= 30:  // cos²30 = 3/4: 4·dot² ≥ 3·|v|²·|d|²
                return 4 * dot2 >= 3 * v2 * d2;
            case <= 45:  // cos²45 = 1/2: 2·dot² ≥ |v|²·|d|²
                return 2 * dot2 >= v2 * d2;
            case <= 60:  // cos²60 = 1/4: 4·dot² ≥ |v|²·|d|²
                return 4 * dot2 >= v2 * d2;
            default:     // ≥ 90°: cos²90 = 0 — полупространство (v·d ≥ 0 уже проверено)
                return true;
        }
    }

    private static int Dot(int ax, int ay, int bx, int by)
        => ax * bx + ay * by;
}
