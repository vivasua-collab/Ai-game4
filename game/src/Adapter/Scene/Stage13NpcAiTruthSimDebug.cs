#nullable enable
// Создано: 2026-10-23 — Этап-13 аудита владельца (ИИ NPC, цели, навигация,
// группы, животные). Канон: checkpoints/10_23_stage13_npc_ai_audit_request.md
// ([STAGE 13 AUDIT] verbatim + таблица пре-верификации).
// Запуск:
//   GODOT_NEWGAME=1 GODOT_STAGE13_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn
//
// Сценарии (детерминированные: фиксированные позиции/угрозы/цели/тайлы):
//   A. ST13-001 — движение сквозь препятствия vs A* (NPC_AI_SYSTEM §6):
//      A1 прямая без препятствий; A2 обход стены (ни одного кадра на
//      непроходимом тайле); A3 цели нет пути (нет телепорта/зависания,
//      позиция стабилизируется у стены); A4 препятствие убрано → re-path;
//      A5 несколько NPC одновременно (вкл. участника группы).
//   B. ST13-002 — приоритет группы vs бой (GROUP_SYSTEM §6: 1.бой 2.группа
//      3.индивидуальное): B1 Attacking-участник идёт к врагу, не к waypoint;
//      B2 после боя возвращается к групповой задаче; B3 Fleeing не
//      подавляется групповой целью.
//   C. ST13-003 — переключение на более опасную цель (§5.3/§9.2):
//      C1 цель-ИГРОК + более опасный NPC-атакующий → переключение (раньше
//      блокировалось !IsPlayer); C2 ретаргет по событию урона (регресс R20);
//      C2b атрибуция DoT → OriginalAttackerId (регресс PT-001); C3
//      детерминированный tie-break (min-ordinal, стабильность текущей цели);
//      C4 мёртвая цель → выход из боя (регресс AUDIT-0921 B4).
//   D. ST13-004 — месть животного каноническому атакующему (ANIMALS §5.1):
//      D1 игрок (регресс); D2 NPC-атакующий → чейз/интент в NPC, не в
//      игрока; D3 смерть атакующего → de-aggro; D4 неизвестный атакующий
//      (DoT без атрибуции) → месть не включается; D5 кролик мирный
//      (регресс); D6 смерть игрока снимает ТОЛЬКО месть к игроку.
//   E. ST13-005 — детект по радиусу (§2.1/§9.1: LOS не требуется —
//      кандидат DISPROVED как дефект): E1 агро через стену в радиусе —
//      норм. поведение по спеце; E1b вне радиуса — нет агро. Открытый
//      вопрос «терять ли уже обнаруженную цель за препятствием» — GAP.
//   F. ST13-006 — черты личности в решениях (NPC.md §4.2): F1 Loyal
//      (−50% idle), F2 Treacherous (Attitude < Neutral → +40% patrol).
//      Перки/лестница Attitude §10.2/фракции — GAP владельцу.
//
// Итог: [Stage13Sim] VERDICT: PASS / FAIL (+ пофазовые PASS/FAIL/GAP/NOT TESTED).
using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Adapter.Di;
using CultivationGame.Modules.NPC;

namespace CultivationGame.Adapter.Scene
{
    /// <summary>
    /// Headless-верификация Этапа-13: решения/движение/группы/животные.
    /// Каждый сценарий печатает входное состояние и причину PASS/FAIL.
    /// </summary>
    public partial class Stage13NpcAiTruthSimDebug : Node
    {
        // === DI: сервисы ===
        [Inject] private INPCService? _npcs;
        [Inject] private INPCSpawnerService? _spawner;
        [Inject] private INPCGroupService? _groups;
        [Inject] private ITileService? _tiles;
        [Inject] private IPlayerService? _player;
        [Inject] private AnimalService? _animals;
        [Inject] private NPCAIService? _aiQa;

        // === DI: события ===
        [Inject] private IPublisher<DamageAppliedEvent>? _dmgPub;
        [Inject] private IPublisher<PlayerDeathEvent>? _playerDeathPub;
        [Inject] private IPublisher<PlayerReviveEvent>? _playerRevivePub;
        [Inject] private ISubscriber<AttackIntentEvent>? _intentSub;
        [Inject] private ISubscriber<CombatDisengageEvent>? _disengageSub;

        private IDisposable? _intentToken;
        private IDisposable? _disengageToken;

        // === Буферы наблюдений ===
        private readonly List<AttackIntentEvent> _intents = new();
        private readonly List<CombatDisengageEvent> _disengages = new();

        // === Результаты ===
        private readonly List<(string Id, string Status, string Detail)> _results = new();

        // === Арена (угол карты, чистые тайлы; стены — по сценариям) ===
        private const int MinX = 4, MaxX = 21, MinY = 4, MaxY = 21;
        private readonly List<Position2D> _activeWalls = new();

        public override void _Ready()
        {
            var container = Scene.GameBoot.Container;
            if (container != null)
                ContainerAdapter.InjectProperties(this, container);

            _intentToken = _intentSub?.Subscribe((in AttackIntentEvent e) => _intents.Add(e));
            _disengageToken = _disengageSub?.Subscribe((in CombatDisengageEvent e) => _disengages.Add(e));

            GD.Print("[Stage13Sim] Ready — Этап-13 (NPC AI/навигация/группы/животные) starts in 3s");
            _ = RunAllScenariosAsync();
        }

        public override void _ExitTree()
        {
            _intentToken?.Dispose();
            _disengageToken?.Dispose();
        }

        private async Task RunAllScenariosAsync()
        {
            await ToSignal(GetTree().CreateTimer(3.0), SceneTreeTimer.SignalName.Timeout);

            if (_npcs == null || _spawner == null || _groups == null || _tiles == null
                || _player == null || _animals == null || _aiQa == null || _dmgPub == null)
            {
                GD.Print("[Stage13Sim] FAIL — DI not wired");
                PrintSummary();
                return;
            }

            GD.Print($"[Stage13Sim] player={_player.PlayerId} @ {_player.Position}; карта {_tiles.MapWidth}x{_tiles.MapHeight}");

            PrepareArena();

            // Порядок: D6 (смерть игрока) — последней: побочные эффекты
            // (экран смерти) не должны влиять на прочие сценарии.
            await ScenarioA();
            await ScenarioB();
            await ScenarioC();
            await ScenarioD();
            await ScenarioD6();
            await ScenarioE();
            await ScenarioF();

            PrintSummary();
        }

        // ================================================================
        //  Хелперы
        // ================================================================

        private void Record(string id, bool pass, string detail)
            => _results.Add((id, pass ? "PASS" : "FAIL", detail));

        private void RecordGap(string id, string detail)
            => _results.Add((id, "GAP", detail));

        private void RecordNotTested(string id, string detail)
            => _results.Add((id, "NOT TESTED", detail));

        private void PrepareArena()
        {
            // Чистая трава на арене — детерминизм (объекты генерации убраны).
            for (int x = MinX; x <= MaxX; x++)
                for (int y = MinY; y <= MaxY; y++)
                    _tiles!.SetTile(x, y, GameTile.CreateTerrain(x, y, TerrainType.Grass));
            GD.Print($"[Stage13Sim] Арена ({MinX}..{MaxX})² очищена (Grass)");
        }

        private void Wall(int x, int y)
        {
            _tiles!.SetTile(x, y, GameTile.CreateWithObject(x, y, TerrainType.Grass, ObjectType.Rock_Large));
            _activeWalls.Add(new Position2D(x, y));
        }

        private void ClearWalls()
        {
            foreach (var w in _activeWalls)
                _tiles!.SetTile(w.X, w.Y, GameTile.CreateTerrain(w.X, w.Y, TerrainType.Grass));
            _activeWalls.Clear();
        }

        private static Position2D P(int x, int y) => new(x, y);

        private string SpawnNpc(string tag, int x, int y, NPCRole role, long seed)
        {
            string? id = _spawner!.SpawnNPC("human", role, 1, P(x, y), seed);
            if (id == null)
            {
                GD.PrintErr($"[Stage13Sim] спавн {tag} не удался");
                return "";
            }
            return id;
        }

        /// <summary>Пининг цели: мир тикает, побочные NPC не должны дрейфовать.
        /// Вызывается каждый кадр цикла ожидания.</summary>
        private static void Pin(INPCService svc, string id, Position2D pos)
        {
            var st = svc.GetNPCState(id);
            if (st != null && st.Position != pos) svc.UpdatePosition(id, pos);
        }

        private async Task WaitFrames(int frames, params (string id, Position2D pos)[] pins)
        {
            for (int i = 0; i < frames; i++)
            {
                if (_npcs != null)
                    foreach (var (id, pos) in pins) Pin(_npcs, id, pos);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }

        private async Task<bool> WaitFramesSampling(
            string moverId,
            Position2D goal,
            int frames,
            bool stopOnReach,
            Action<int>? onFrame = null,
            params (string id, Position2D pos)[] pins)
        {
            // Возвращает false, если хоть один сегмент траектории мовера
            // прошёл через непроходимый тайл. Движение ТИКОВОЕ (шаг до
            // нескольких тайлов за тик) — проверяем СЕГМЕНТ между
            // последовательными позициями (supercover-Bresenham), а не
            // только позиции на кадрах: иначе прыжок «через стену» между
            // кадрами не ловится. Ранний выход при dist ≤ 1.6 к goal.
            bool clean = true;
            bool reached = false;
            var last = _npcs!.GetNPCState(moverId)?.Position ?? new Position2D(0, 0);
            for (int i = 0; i < frames && !reached; i++)
            {
                if (_npcs != null)
                    foreach (var (id, pos) in pins) Pin(_npcs, id, pos);
                var st = _npcs!.GetNPCState(moverId);
                if (st != null)
                {
                    if (st.Position != last)
                    {
                        if (!SegmentWalkable(last, st.Position))
                        {
                            GD.PrintErr($"[Stage13Sim] кадр {i}: сегмент {moverId} ({last.X},{last.Y})→({st.Position.X},{st.Position.Y}) пересекает непроходимые тайлы");
                            clean = false;
                        }
                        last = st.Position;
                    }
                    if (stopOnReach && Position2D.Distance(st.Position, goal) <= 1.6f)
                        reached = true;
                }
                onFrame?.Invoke(i);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            _reachedGoal = reached;
            return clean;
        }

        private bool _reachedGoal;

        /// <summary>Supercover-Bresenham: все тайлы отрезка (включая концы)
        /// проходимы? Диагональные шаги проверяют ОБА ортогональных соседа
        /// (угол не срезается).</summary>
        private bool SegmentWalkable(Position2D a, Position2D b)
        {
            int x0 = a.X, y0 = a.Y;
            int x1 = b.X, y1 = b.Y;
            if (!_tiles!.IsWalkable(x0, y0) || !_tiles.IsWalkable(x1, y1)) return false;
            int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;
            int x = x0, y = y0;
            while (x != x1 || y != y1)
            {
                int e2 = err * 2;
                if (e2 > -dy && e2 < dx)
                {
                    // диагональный шаг: угол не режем — оба соседа проходимы
                    if (!_tiles.IsWalkable(x + sx, y) || !_tiles.IsWalkable(x, y + sy)) return false;
                    x += sx; y += sy; err -= dy; err += dx;
                }
                else if (e2 > -dy) { x += sx; err -= dy; }
                else if (e2 < dx) { y += sy; err += dx; }
                if ((x != x1 || y != y1) && !_tiles.IsWalkable(x, y)) return false;
            }
            return true;
        }

        private float Dist(string idA, Position2D b)
        {
            var st = _npcs!.GetNPCState(idA);
            return st == null ? float.MaxValue : Position2D.Distance(st.Position, b);
        }

        private void SetAttacking(string id, string targetId)
        {
            var st = _npcs!.GetNPCState(id);
            if (st == null) return;
            st.TargetId = targetId;
            _npcs.SetAIState(id, NPCAIState.Attacking); // → IsInCombat=true (адаптер)
        }

        /// <summary>Мирное движение к цели (без атак/интентов) — детерминированная
        /// проба навигации: kiting/оружие/готовность ударов не вмешиваются.</summary>
        private void SetFollowing(string id, string targetId)
        {
            var st = _npcs!.GetNPCState(id);
            if (st == null) return;
            st.TargetId = targetId;
            if (st.AIState != NPCAIState.Following)
                _npcs.SetAIState(id, NPCAIState.Following);
        }

        /// <summary>Ре-установка Following если DecideNextState перевёл
        /// состояние (таймаут 6с) — удерживает мовера на маршруте.</summary>
        private void KeepFollowing(string id, string targetId)
        {
            var st = _npcs!.GetNPCState(id);
            if (st == null) return;
            if (st.AIState != NPCAIState.Following || st.TargetId != targetId)
                SetFollowing(id, targetId);
        }

        // ================================================================
        //  A. ST13-001 — навигация сквозь препятствия
        // ================================================================

        private async Task ScenarioA()
        {
            GD.Print("[Stage13Sim] ─── A. ST13-001: NPC не проходят сквозь препятствия (A*, §6) ───");
            // Движение тестируем МИРНОЙ веткой Following: Attacking-мProvOST
            // зависит от оружия (лучник kiting стоит с дистанции) — для
            // навигации важна траектория, а не бой.

            // --- A1: прямая без препятствий ---
            string b1 = SpawnNpc("A1-target", 14, 6, NPCRole.Passerby, 2026102301);
            string a1 = SpawnNpc("A1-mover", 6, 6, NPCRole.Passerby, 2026102302);
            SetFollowing(a1, b1);
            bool a1Clean = await WaitFramesSampling(a1, P(14, 6), 480, false,
                _ => KeepFollowing(a1, b1), (b1, P(14, 6))); // ~8с
            float a1Dist = Dist(a1, P(14, 6));
            bool a1ok = a1Clean && a1Dist <= 2.05f; // followDistance=2
            GD.Print($"[Stage13Sim] A1: прямая: дистанция до цели {a1Dist:F1} (≤2.05), сегменты чисты={a1Clean} → {(a1ok ? "PASS" : "FAIL")}");
            Record("A1", a1ok, $"dist={a1Dist:F1}, clean={a1Clean}");

            // --- A2: стена с обходом (зазор сверху) ---
            // Mover (6,12) → target (18,12). Стена x=12, y=8..16, зазор y=6..7.
            for (int y = 8; y <= 16; y++) Wall(12, y);
            string b2 = SpawnNpc("A2-target", 18, 12, NPCRole.Passerby, 2026102303);
            string a2 = SpawnNpc("A2-mover", 6, 12, NPCRole.Passerby, 2026102304);
            SetFollowing(a2, b2);
            bool a2Clean = await WaitFramesSampling(a2, P(18, 12), 1800, false,
                _ => KeepFollowing(a2, b2), (b2, P(18, 12))); // ~30с
            float a2Dist = Dist(a2, P(18, 12));
            bool a2ok = a2Clean && a2Dist <= 2.05f;
            GD.Print($"[Stage13Sim] A2: обход стены: дистанция {a2Dist:F1} (≤2.05), ни одного сегмента сквозь стену={a2Clean} → {(a2ok ? "PASS" : "FAIL")}");
            Record("A2", a2ok, $"dist={a2Dist:F1}, clean={a2Clean}");

            // --- A3: пути нет (кольцо вокруг цели) → нет телепорта/зависания ---
            for (int x = 16; x <= 20; x++) { Wall(x, 16); Wall(x, 20); }
            for (int y = 17; y <= 19; y++) { Wall(16, y); Wall(20, y); }
            string b3 = SpawnNpc("A3-target", 18, 18, NPCRole.Passerby, 2026102305);
            string a3 = SpawnNpc("A3-mover", 14, 18, NPCRole.Passerby, 2026102306);
            SetFollowing(a3, b3);
            var lastA3 = _npcs!.GetNPCState(a3)!.Position;
            int stableFrames = 0; int maxStable = 0;
            bool a3Clean = await WaitFramesSampling(a3, P(18, 18), 1200, false, _ =>
            {
                KeepFollowing(a3, b3);
                var st = _npcs.GetNPCState(a3);
                if (st == null) return;
                float d = Position2D.Distance(st.Position, lastA3);
                if (d < 0.01f) { stableFrames++; maxStable = Math.Max(maxStable, stableFrames); }
                else stableFrames = 0;
                lastA3 = st.Position;
            }, (b3, P(18, 18))); // ~20с
            float a3Dist = Dist(a3, P(18, 18));
            bool a3Stable = maxStable >= 120; // ≥2с без движения → не циклит
            bool a3ok = a3Clean && a3Dist > 2.05f && a3Stable;
            GD.Print($"[Stage13Sim] A3: пути нет: дистанция {a3Dist:F1} (>2.05 — не достиг), чисто={a3Clean}, стабилен={a3Stable} (max {maxStable} кадров) → {(a3ok ? "PASS" : "FAIL")}");
            Record("A3", a3ok, $"dist={a3Dist:F1}, clean={a3Clean}, stable={a3Stable}");

            // --- A4: кольцо убрано → re-path и достижимость ---
            ClearWalls();
            bool a4Clean = await WaitFramesSampling(a3, P(18, 18), 1200, false,
                _ => KeepFollowing(a3, b3), (b3, P(18, 18))); // ~20с
            float a4Dist = Dist(a3, P(18, 18));
            bool a4ok = a4Clean && a4Dist <= 2.05f;
            GD.Print($"[Stage13Sim] A4: препятствие убрано: дистанция {a4Dist:F1} (≤2.05), чисто={a4Clean} → {(a4ok ? "PASS" : "FAIL")}");
            Record("A4", a4ok, $"dist={a4Dist:F1}, clean={a4Clean}");

            // --- A5: несколько NPC одновременно (мульти-мовер стресс) ---
            string b5 = SpawnNpc("A5-target", 8, 18, NPCRole.Passerby, 2026102307);
            string m1 = SpawnNpc("A5-m1", 6, 6, NPCRole.Passerby, 2026102308);
            string m2 = SpawnNpc("A5-m2", 16, 6, NPCRole.Passerby, 2026102309);
            string m3 = SpawnNpc("A5-m3", 16, 16, NPCRole.Passerby, 2026102311);
            SetFollowing(m1, b5);
            SetFollowing(m2, b5);
            SetFollowing(m3, b5);
            bool a5Clean = await WaitFramesSampling(m1, P(8, 18), 1800, false, _ =>
            {
                KeepFollowing(m1, b5); KeepFollowing(m2, b5); KeepFollowing(m3, b5);
            }, (b5, P(8, 18))); // ~30с
            var stM2 = _npcs!.GetNPCState(m2);
            var stM3 = _npcs.GetNPCState(m3);
            bool a5m2Clean = stM2 == null || SegmentWalkable(P(16, 6), stM2.Position);
            bool a5m3Clean = stM3 == null || SegmentWalkable(P(16, 16), stM3.Position);
            float dM1 = Dist(m1, P(8, 18)), dM2 = Dist(m2, P(8, 18)), dM3 = Dist(m3, P(8, 18));
            bool a5ok = a5Clean && a5m2Clean && a5m3Clean
                && dM1 <= 2.05f && dM2 <= 2.05f && dM3 <= 2.05f;
            GD.Print($"[Stage13Sim] A5: мульти: m1={dM1:F1}, m2={dM2:F1}, m3={dM3:F1} (все ≤2.05), сегменты чисты={a5Clean && a5m2Clean && a5m3Clean} → {(a5ok ? "PASS" : "FAIL")}");
            Record("A5", a5ok, $"dM1={dM1:F1}, dM2={dM2:F1}, dM3={dM3:F1}");
        }

        private async Task ScenarioB()
        {
            GD.Print("[Stage13Sim] ─── B. ST13-002: бой > групповая задача > индивидуальное (§6) ───");

            // --- B1: Attacking-участник группы идёт к врагу, а не к waypoint ---
            // Враг ДАЛЕКО на ВОСТОКЕ (40,5; Евклид > любой AttackRange лука) —
            // лучник тоже обязан СБЛИЖАТЬСЯ (kiting не срабатывает за
            // пределами дальности оружия). Waypoint — чистый ЮГ: направления
            // различимы по осям. Мовер (12,5).
            string enemy = SpawnNpc("B1-enemy", 40, 5, NPCRole.Passerby, 2026102312);
            string member = SpawnNpc("B1-member", 12, 5, NPCRole.Guard, 2026102313);
            string grp = _groups!.CreateGroup(GroupTaskType.Patrol, "qa_stage13_b", "human");
            _groups.AddMember(grp, member, GroupRole.Leader);
            _groups.SetPatrolRoute(grp, new List<Position2D> { P(12, 20), P(19, 20) });
            await WaitFrames(90, (enemy, P(40, 5))); // ~1.5с: группа тикнула
            var groupTarget = _groups.GetGroupByMember(member)?.CurrentGroupTarget;
            GD.Print($"[Stage13Sim] B1: CurrentGroupTarget группы = {groupTarget}");
            float distEnemyBefore = Dist(member, P(40, 5));
            float distWaypointBefore = groupTarget.HasValue ? Dist(member, groupTarget.Value) : -1f;
            SetAttacking(member, enemy);
            await WaitFrames(120, (enemy, P(40, 5))); // ~2с в бою
            float distEnemyAfter = Dist(member, P(40, 5));
            float distWaypointAfter = groupTarget.HasValue ? Dist(member, groupTarget.Value) : -1f;
            bool b1 = distEnemyAfter < distEnemyBefore - 1f; // к врагу (восток)
            GD.Print($"[Stage13Sim] B1: в бою: до врага {distEnemyBefore:F1}→{distEnemyAfter:F1} (↓ — восток), до waypoint {distWaypointBefore:F1}→{distWaypointAfter:F1} → {(b1 ? "PASS" : "FAIL")}");
            Record("B1", b1, $"enemy {distEnemyBefore:F1}→{distEnemyAfter:F1}");

            // --- B2: бой завершён → возврат к групповой задаче ---
            _npcs!.SetAIState(member, NPCAIState.Wandering);
            var mst = _npcs.GetNPCState(member);
            if (mst != null) { mst.IsInCombat = false; mst.TargetId = ""; }
            var groupTarget2 = _groups.GetGroupByMember(member)?.CurrentGroupTarget;
            float before2 = groupTarget2.HasValue ? Dist(member, groupTarget2.Value) : -1f;
            await WaitFrames(150); // ~2.5с
            var groupTarget3 = _groups.GetGroupByMember(member)?.CurrentGroupTarget;
            float after3 = groupTarget3.HasValue ? Dist(member, groupTarget3.Value) : -1f;
            bool b2 = after3 >= 0 && after3 < before2 - 0.5f; // группа ведёт к waypoint
            GD.Print($"[Stage13Sim] B2: после боя: до групповой цели {before2:F1}→{after3:F1} (↓ — вернулся к задаче) → {(b2 ? "PASS" : "FAIL")}");
            Record("B2", b2, $"{before2:F1}→{after3:F1}");

            await ScenarioB3Flee();
        }

        private async Task ScenarioB3Flee()
        {
            // --- B3: Fleeing не подавляется групповой целью ---
            // Угроза ВОСТОК (14,12), участник (10,12), waypoint ЮГ (10,18).
            // Бегство = ЗАПАД (x↓); группа = ЮГ (y↑). Различимы по осям.
            GD.Print("[Stage13Sim] ─── B3: Fleeing vs групповая цель ───");
            string threat = SpawnNpc("B3-threat", 14, 12, NPCRole.Passerby, 2026102313);
            string runner = SpawnNpc("B3-runner", 10, 12, NPCRole.Guard, 2026102314);
            string grp = _groups!.CreateGroup(GroupTaskType.Patrol, "qa_stage13_b3", "human");
            _groups.AddMember(grp, runner, GroupRole.Leader);
            _groups.SetPatrolRoute(grp, new List<Position2D> { P(10, 18), P(18, 18) });
            await WaitFrames(70, (threat, P(14, 12)));
            var st = _npcs!.GetNPCState(runner);
            if (st != null) { st.Threats[threat] = 10f; } // ниже порога — только позиция для бегства
            _npcs.SetAIState(runner, NPCAIState.Fleeing);
            var before = _npcs.GetNPCState(runner)!.Position;
            await WaitFrames(90, (threat, P(14, 12))); // ~1.5с (< таймаута Fleeing 4с)
            var after = _npcs.GetNPCState(runner)!.Position;
            bool fledWest = after.X < before.X - 0.5f;
            GD.Print($"[Stage13Sim] B3: бегство: x {before.X}→{after.X} (запад от угрозы), y {before.Y}→{after.Y} → {(fledWest ? "PASS" : "FAIL")}");
            Record("B3", fledWest, $"({before.X},{before.Y})→({after.X},{after.Y})");
        }

        // ================================================================
        //  C. ST13-003 — переключение на более опасную цель
        // ================================================================

        private async Task ScenarioC()
        {
            GD.Print("[Stage13Sim] ─── C. ST13-003: более опасная цель (§5.3/§9.2) ───");

            // --- C1: цель-ИГРОК + более опасный NPC-атакующий → переключение ---
            var pp = _player!.Position;
            string p1 = SpawnNpc("C1-npc", pp.X + 2, pp.Y + 1, NPCRole.Enemy, 2026102315);
            string q1 = SpawnNpc("C1-danger", 8, 8, NPCRole.Passerby, 2026102316);
            var st1 = _npcs!.GetNPCState(p1);
            if (st1 != null) st1.Threats[_player.PlayerId] = 60f;
            SetAttacking(p1, _player.PlayerId);
            await WaitFrames(30, (q1, P(8, 8)));
            var st1b = _npcs.GetNPCState(p1);
            if (st1b != null) st1b.Threats[q1] = 200f;
            await WaitFrames(150, (q1, P(8, 8))); // ~2.5с: AI-тик должен пересмотреть цель
            var targetAfter = _npcs.GetNPCState(p1)?.TargetId ?? "";
            bool c1 = targetAfter == q1;
            GD.Print($"[Stage13Sim] C1: цель-игрок (угроза 60) + NPC-угроза 200 → TargetId='{targetAfter}' (ожид '{q1}') → {(c1 ? "PASS" : "FAIL")}");
            Record("C1", c1, $"TargetId='{targetAfter}'");

            // --- C2: ретаргет по событию урона (регресс R20) ---
            string r2 = SpawnNpc("C2-cur", 12, 6, NPCRole.Passerby, 2026102317);
            string q2 = SpawnNpc("C2-new", 14, 10, NPCRole.Passerby, 2026102318);
            string p2 = SpawnNpc("C2-npc", 12, 10, NPCRole.Enemy, 2026102319);
            var st2 = _npcs.GetNPCState(p2);
            if (st2 != null) st2.Threats[r2] = 60f;
            SetAttacking(p2, r2);
            await WaitFrames(30, (r2, P(12, 6)), (q2, P(14, 10)));
            _dmgPub!.Publish(new DamageAppliedEvent(q2, p2, 40, DamageType.Physical,
                Element.Neutral, BodyPartType.Torso, CombatAttackResult.Hit, CombatSubtype.None, q2));
            await WaitFrames(150, (r2, P(12, 6)), (q2, P(14, 10)));
            var t2 = _npcs.GetNPCState(p2)?.TargetId ?? "";
            bool c2 = t2 == q2;
            GD.Print($"[Stage13Sim] C2: урон от нового атакующего (80 > 60) → TargetId='{t2}' (ожид '{q2}') → {(c2 ? "PASS" : "FAIL")}");
            Record("C2", c2, $"TargetId='{t2}'");

            // --- C2b: DoT-атрибуция — угроза каноническому автору (регресс) ---
            string q3 = SpawnNpc("C2b-author", 16, 10, NPCRole.Passerby, 2026102320);
            string p3 = SpawnNpc("C2b-npc", 16, 6, NPCRole.Enemy, 2026102321);
            var st3 = _npcs.GetNPCState(p3);
            if (st3 != null) st3.Threats.Clear();
            _dmgPub!.Publish(new DamageAppliedEvent("dot:poison_qa", p3, 45, DamageType.Elemental,
                Element.Neutral, BodyPartType.Torso, CombatAttackResult.Hit, CombatSubtype.None, q3));
            await WaitFrames(30, (q3, P(16, 10)));
            var st3b = _npcs.GetNPCState(p3);
            bool c2b = st3b != null && st3b.Threats.ContainsKey(q3) && !st3b.Threats.ContainsKey("dot:poison_qa");
            GD.Print($"[Stage13Sim] C2b: DoT-тик: угроза автору '{q3}'={st3b?.Threats.ContainsKey(q3)}, фантом 'dot:…' отсутствует={!st3b?.Threats.ContainsKey("dot:poison_qa")} → {(c2b ? "PASS" : "FAIL")}");
            Record("C2b", c2b, "атрибуция OriginalAttackerId");

            // --- C3: детерминированный tie-break (min-ordinal при равной угрозе) ---
            string tA = SpawnNpc("C3-A", 18, 6, NPCRole.Passerby, 2026102322);
            string tB = SpawnNpc("C3-B", 18, 10, NPCRole.Passerby, 2026102323);
            string p4 = SpawnNpc("C3-npc", 18, 14, NPCRole.Enemy, 2026102324);
            string expected = string.CompareOrdinal(tA, tB) < 0 ? tA : tB;
            int consistent = 0; const int rounds = 5;
            for (int r = 0; r < rounds; r++)
            {
                var st4 = _npcs.GetNPCState(p4);
                if (st4 == null) break;
                st4.Threats.Clear();
                st4.Threats[tA] = 100f;
                st4.Threats[tB] = 100f;
                _npcs.SetAIState(p4, NPCAIState.Wandering);
                if (st4.IsInCombat) { st4.IsInCombat = false; }
                st4.TargetId = "";
                await WaitFrames(90, (tA, P(18, 6)), (tB, P(18, 10))); // ~1.5с: повторный вход в атаку
                var picked = _npcs.GetNPCState(p4)?.TargetId ?? "";
                if (picked == expected) consistent++;
            }
            bool c3 = consistent == rounds;
            GD.Print($"[Stage13Sim] C3: равная угроза 100/100: выбран min-ordinal '{expected}' в {consistent}/{rounds} повторах → {(c3 ? "PASS" : "FAIL")}");
            Record("C3", c3, $"{consistent}/{rounds} → '{expected}'");

            // --- C4: мёртвая цель → выход из боя (регресс B4) ---
            string d4 = SpawnNpc("C4-doomed", 6, 10, NPCRole.Passerby, 2026102325);
            string p5 = SpawnNpc("C4-npc", 6, 14, NPCRole.Enemy, 2026102326);
            SetAttacking(p5, d4);
            await WaitFrames(30, (d4, P(6, 10)));
            var doomed = _npcs.GetNPCState(d4);
            if (doomed != null) doomed.IsAlive = false;
            await WaitFrames(120); // ~2с
            var st5 = _npcs.GetNPCState(p5);
            bool c4 = st5 != null && st5.AIState != NPCAIState.Attacking
                && string.IsNullOrEmpty(st5.TargetId) && !st5.IsInCombat;
            GD.Print($"[Stage13Sim] C4: цель умерла → AIState={st5?.AIState}, TargetId='{st5?.TargetId}', IsInCombat={st5?.IsInCombat} → {(c4 ? "PASS" : "FAIL")}");
            Record("C4", c4, $"state={st5?.AIState}");
        }

        // ================================================================
        //  D. ST13-004 — месть животного каноническому атакующему
        // ================================================================

        private async Task ScenarioD()
        {
            GD.Print("[Stage13Sim] ─── D. ST13-004: месть животного — только атаковавшему (ANIMALS §5.1) ───");

            // --- D1: игрок атакует волка → чейз/интент в игрока (регресс) ---
            var pp = _player!.Position;
            var w1 = _animals!.SpawnAnimal("wolf", P(pp.X + 2, pp.Y + 2));
            _intents.Clear();
            _dmgPub!.Publish(new DamageAppliedEvent(_player.PlayerId, w1.EntityId, 5, DamageType.Physical,
                Element.Neutral, BodyPartType.Torso, CombatAttackResult.Hit, CombatSubtype.None, _player.PlayerId));
            await WaitFrames(240); // ~4с (4 тика)
            bool d1hostile = _animals.IsHostile(w1.EntityId);
            bool d1intent = false;
            lock (_intents) d1intent = _intents.Exists(i => i.AttackerId == w1.EntityId
                && i.TargetId == _player.PlayerId);
            bool d1 = d1hostile && d1intent;
            GD.Print($"[Stage13Sim] D1: игрок ударил волка: hostile={d1hostile}, интент в игрока={d1intent} → {(d1 ? "PASS" : "FAIL")}");
            Record("D1", d1, $"hostile={d1hostile}, intent→player={d1intent}");

            // --- D2: NPC атакует волка → чейз/интент в NPC, не в игрока ---
            string n2 = SpawnNpc("D2-attacker", 14, 12, NPCRole.Passerby, 2026102327);
            var w2 = _animals.SpawnAnimal("wolf", P(11, 12));
            _intents.Clear();
            float distBefore = Position2D.Distance(w2.Position, P(14, 12));
            _dmgPub.Publish(new DamageAppliedEvent(n2, w2.EntityId, 5, DamageType.Physical,
                Element.Neutral, BodyPartType.Torso, CombatAttackResult.Hit, CombatSubtype.None, n2));
            await WaitFrames(240, (n2, P(14, 12))); // ~4с
            var w2info = _animals.TryGetAnimal(w2.EntityId);
            float distAfter = w2info == null ? -1 : Position2D.Distance(w2info!.Value.Position, P(14, 12));
            bool d2intent = false;
            lock (_intents) d2intent = _intents.Exists(i => i.AttackerId == w2.EntityId && i.TargetId == n2);
            bool d2playerIntent = false;
            lock (_intents) d2playerIntent = _intents.Exists(i => i.AttackerId == w2.EntityId
                && i.TargetId == _player.PlayerId);
            bool d2 = distAfter >= 0 && distAfter < distBefore - 1f && d2intent && !d2playerIntent;
            GD.Print($"[Stage13Sim] D2: NPC ударил волка: дистанция к NPC {distBefore:F1}→{distAfter:F1} (↓), интент в NPC={d2intent}, в игрока={d2playerIntent} → {(d2 ? "PASS" : "FAIL")}");
            Record("D2", d2, $"dist {distBefore:F1}→{distAfter:F1}, intent→npc={d2intent}");

            // --- D3: атакующий умер → месть прекращается (§5.4) ---
            var n2state = _npcs!.GetNPCState(n2);
            if (n2state != null) n2state.IsAlive = false;
            await WaitFrames(240); // ~4с
            bool d3 = !_animals.IsHostile(w2.EntityId);
            GD.Print($"[Stage13Sim] D3: атакующий умер: hostile={_animals.IsHostile(w2.EntityId)} (ожид false) → {(d3 ? "PASS" : "FAIL")}");
            Record("D3", d3, $"hostile={_animals.IsHostile(w2.EntityId)}");

            // --- D4: неизвестный атакующий (DoT без атрибуции) → месть НЕ включается ---
            var w3 = _animals.SpawnAnimal("wolf", P(8, 16));
            _dmgPub.Publish(new DamageAppliedEvent("dot:mystery_qa", w3.EntityId, 3, DamageType.Elemental,
                Element.Neutral, BodyPartType.Torso, CombatAttackResult.Hit, CombatSubtype.None, null));
            await WaitFrames(240); // ~4с
            bool d4 = !_animals.IsHostile(w3.EntityId);
            GD.Print($"[Stage13Sim] D4: DoT без атрибуции: hostile={_animals.IsHostile(w3.EntityId)} (ожид false) → {(d4 ? "PASS" : "FAIL")}");
            Record("D4", d4, $"hostile={_animals.IsHostile(w3.EntityId)}");

            // --- D5: кролик мирный (регресс §5.5) ---
            var r5 = _animals.SpawnAnimal("rabbit", P(10, 16));
            _dmgPub.Publish(new DamageAppliedEvent(_player.PlayerId, r5.EntityId, 1, DamageType.Physical,
                Element.Neutral, BodyPartType.Torso, CombatAttackResult.Hit, CombatSubtype.None, _player.PlayerId));
            await WaitFrames(240); // ~4с
            bool d5 = !_animals.IsHostile(r5.EntityId);
            GD.Print($"[Stage13Sim] D5: кролик: hostile={_animals.IsHostile(r5.EntityId)} (ожид false) → {(d5 ? "PASS" : "FAIL")}");
            Record("D5", d5, $"hostile={_animals.IsHostile(r5.EntityId)}");
        }

        /// <summary>D6 отдельно и последним: смерть игрока — тяжёлый побочный
        /// путь (экран смерти); после asserts немедленно revive.</summary>
        private async Task ScenarioD6()
        {
            GD.Print("[Stage13Sim] ─── D6: смерть игрока снимает только месть к игроку (PT-001 фильтр) ───");
            // Волк мстит NPC → смерть игрока НЕ должна гасить эту месть.
            string n6 = SpawnNpc("D6-attacker", 12, 16, NPCRole.Passerby, 2026102328);
            var w6 = _animals!.SpawnAnimal("wolf", P(10, 16));
            _dmgPub!.Publish(new DamageAppliedEvent(n6, w6.EntityId, 5, DamageType.Physical,
                Element.Neutral, BodyPartType.Torso, CombatAttackResult.Hit, CombatSubtype.None, n6));
            await WaitFrames(120, (n6, P(12, 16))); // ~2с: месть включена
            if (!_animals.IsHostile(w6.EntityId))
            {
                GD.PrintErr("[Stage13Sim] D6: месть к NPC не включилась — сценарий не тестерен");
                RecordNotTested("D6", "месть к NPC не включилась (см. D2)");
                return;
            }
            _playerDeathPub!.Publish(new PlayerDeathEvent("qa_stage13_d6"));
            await WaitFrames(120, (n6, P(12, 16)));
            bool d6 = _animals.IsHostile(w6.EntityId); // месть к NPC живёт
            GD.Print($"[Stage13Sim] D6: после смерти игрока hostile(к NPC)={d6} (ожид true) → {(d6 ? "PASS" : "FAIL")}");
            Record("D6", d6, $"hostile={d6}");
            _playerRevivePub!.Publish(new PlayerReviveEvent());
            await WaitFrames(60);
        }

        // ================================================================
        //  E. ST13-005 — детект по радиусу (норма; кандидат DISPROVED)
        // ================================================================

        private async Task ScenarioE()
        {
            GD.Print("[Stage13Sim] ─── E. ST13-005: детект по радиусу (§2.1/§9.1; LOS в детекте спецем не требуется) ───");

            var pp = _player!.Position;
            // E1: враждебный NPC в радиусе, между ним и игроком — стена.
            // Норма §2.1/§9.1: обнаружение ПО РАДИУСУ → угроза должна появиться.
            int ex = pp.X - 4, ey = pp.Y;
            for (int y = ey - 1; y <= ey + 1; y++) Wall(pp.X - 2, y);
            string h1 = SpawnNpc("E1-hostile", ex, ey, NPCRole.Enemy, 2026102329);
            await WaitFrames(150); // ~2.5с
            var st = _npcs!.GetNPCState(h1);
            bool e1 = st != null && st.Threats.ContainsKey(_player.PlayerId)
                && st.Threats[_player.PlayerId] >= 50f;
            GD.Print($"[Stage13Sim] E1: hostile в радиусе 4, стена между: угроза игроку={st?.Threats.ContainsKey(_player.PlayerId)} (норма: радиус, не видимость) → {(e1 ? "PASS" : "FAIL")}");
            Record("E1", e1, "детект по радиусу — норма §2.1");
            ClearWalls();

            // E1b: вне радиуса (7 > AggroRadius 5) — агро нет.
            string h2 = SpawnNpc("E1b-far", pp.X - 7, pp.Y + 3, NPCRole.Enemy, 2026102330);
            await WaitFrames(150);
            var st2 = _npcs.GetNPCState(h2);
            bool e1b = st2 == null || !st2.Threats.ContainsKey(_player.PlayerId);
            GD.Print($"[Stage13Sim] E1b: hostile вне радиуса (7): угроза игроку={!st2?.Threats.ContainsKey(_player.PlayerId)} (ожид true) → {(e1b ? "PASS" : "FAIL")}");
            Record("E1b", e1b, "радиус уважается");

            // E-GAP: должен ли NPC ТЕРЯТЬ уже обнаруженную цель за препятствием —
            // спец не устанавливает. Не добавлять механику без решения владельца.
            GD.Print("[Stage13Sim] E-GAP (ST13-005): потеря уже обнаруженной цели за препятствием в спеце не определена → GAP владельцу");
            RecordGap("E-GAP", "ST13-005: семантика потери обнаруженной цели за препятствием — вопрос владельцу");
        }

        // ================================================================
        //  F. ST13-006 — черты личности в решениях (NPC.md §4.2)
        // ================================================================

        private async Task ScenarioF()
        {
            GD.Print("[Stage13Sim] ─── F. ST13-006: Loyal/Treacherous в весах решений (NPC.md §4.2) ───");
            await Task.CompletedTask;

            string fBase = SpawnNpc("F-base", 6, 20, NPCRole.Passerby, 2026102331);
            var baseSt = _npcs!.GetNPCState(fBase);
            if (baseSt == null) { RecordNotTested("F1", "спавн не удался"); RecordNotTested("F2", "спавн не удался"); return; }

            var (bi, bw, bp, bf, ba, bfo) = _aiQa!.ComputeDecideWeightsForQa(baseSt);

            // F1: Loyal — −50% idle (NPC.md §4.2). Work/talk в упрощённый
            // chooser не входят — ограничение задокументировано в отчёте.
            var loyalSt = _npcs.GetNPCState(fBase);
            if (loyalSt != null) loyalSt.Personality = PersonalityTrait.Loyal;
            var (li, lw, lp, lf, la, lfo) = _aiQa.ComputeDecideWeightsForQa(loyalSt!);
            bool f1 = Math.Abs(li - bi * 0.5f) < 0.01f;
            GD.Print($"[Stage13Sim] F1: Loyal: idle {bi:F1}→{li:F1} (ожид ×0.5) → {(f1 ? "PASS" : "FAIL")}");
            Record("F1", f1, $"idle {bi:F1}→{li:F1}");

            // F2: Treacherous — Attitude < Neutral → +40% patrol.
            var treachSt = _npcs.GetNPCState(fBase);
            if (treachSt != null) treachSt.Personality = PersonalityTrait.Treacherous;
            var (ti0, tw0, tp0, tf0, ta0, tfo0) = _aiQa.ComputeDecideWeightsForQa(treachSt!);
            bool f2neutral = Math.Abs(tp0 - bp) < 0.01f; // Neutral → без изменений
            _npcs.ModifyAttitude(fBase, _player!.PlayerId, -60); // Hostile (< Neutral)
            var (ti1, tw1, tp1, tf1, ta1, tfo1) = _aiQa.ComputeDecideWeightsForQa(treachSt!);
            bool f2hostile = Math.Abs(tp1 - bp * 1.4f) < 0.01f; // +40% patrol
            bool f2 = f2neutral && f2hostile;
            GD.Print($"[Stage13Sim] F2: Treacherous: patrol @Neutral {tp0:F1} (== base {bp:F1}), @Hostile {tp1:F1} (ожид ×1.4={bp * 1.4f:F1}) → {(f2 ? "PASS" : "FAIL")}");
            Record("F2", f2, $"patrol {tp0:F1}/{tp1:F1} vs base {bp:F1}");
            if (loyalSt != null) loyalSt.Personality = PersonalityTrait.None;

            // F-GAP: перки в решениях AI, лестница Attitude §10.2, фракции —
            // механики не определены нормативно для текущей фазы → GAP.
            GD.Print("[Stage13Sim] F-GAP (ST13-006): перки в решениях AI/лестница Attitude §10.2/фракции — GAP владельцу (механики не определены, не придумываем)");
            RecordGap("F-GAP", "ST13-006: перки/Attitude-лестница/фракции — вопросы владельцу");
        }

        // ================================================================
        //  Итоги
        // ================================================================

        private void PrintSummary()
        {
            int pass = 0, fail = 0, gap = 0, nt = 0;
            foreach (var (_, status, _) in _results)
            {
                if (status == "PASS") pass++;
                else if (status == "FAIL") fail++;
                else if (status == "GAP") gap++;
                else nt++;
            }
            GD.Print($"[Stage13Sim] Итого: PASS={pass}, FAIL={fail}, GAP={gap}, NOT TESTED={nt}");
            GD.Print($"[Stage13Sim] VERDICT: {(fail == 0 ? "PASS" : "FAIL")}");
        }
    }
}
