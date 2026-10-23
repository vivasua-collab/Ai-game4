#nullable enable
// Создано: 2026-10-23 — Этап-10 аудита владельца (жизненный цикл мира).
// Запуск: GODOT_NEWGAME=1 GODOT_STAGE10_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn
//
// Проверяет кандидаты ST10-01..04 (задание checkpoints/
// 10_23_stage10_lifecycle_audit_request.md):
//   A. ST10-01 — очередь истощённых ресурсов: границы задержки, мульти-
//      ресурсы/координаты, повторное истощение, ИЗОЛЯЦИЯ МИРОВ (сброс
//      очереди при пересборке мира = IWorldResettable-контракт; префикс:
//      ResourceService НЕ IWorldResettable — очередь переживает NewGame и
//      armed-событие респауна стреляет по координатам НОВОЙ карты),
//      идемпотентность Initialize.
//   B. ST10-02 — TTL трупов: RemoveOldCorpses по игровому возрасту
//      (свежий живёт, состаренный снят, CorpseRemovedEvent для рендера и
//      окна обыска); wiring — NPCModule.Tick:177 (R13, каждый тик).
//   C. ST10-03 — гигиена групп при смерти: смерть лидера → продвижение
//      следующего, смерть всех → расформирование, реестр member→group
//      чист (префикс: подписки на NPCDeathEvent нет — мёртвые остаются
//      участниками и лидерами).
//   D. ST10-04 — скачки времени: BulkAdvanceTicks публикует TimeHitched,
//      календарные события НЕ сыпятся в скачке; первый естественный тик
//      после скачка даёт ровно ОДИН переход (день/месяц/год — финальная
//      дата), маркеры ре-синхронизированы, повторов нет (контракт P1-13).
//   E. REF-строки: PT-C02 (агро-сброс на смерть) — RESPAWN-сим в зоне
//      контроля этапа; физический пункт плейтеста симулятором не закрывается.
//
// Итог: [Stage10Sim] VERDICT: PASS / FAIL (+ пофазовые PASS/FAIL/GAP/REF).
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Modules.Tile;
using CultivationGame.Modules.NPC;
using CultivationGame.Modules.World;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene
{
    /// <summary>
    /// Headless-верификация Этапа-10: жизненный цикл мира — ресурсы, трупы,
    /// группы, время. Каждый сценарий печатает ожидание/факт; финал — VERDICT.
    /// </summary>
    public partial class Stage10WorldLifecycleTruthSimDebug : Node
    {
        // === DI: сервисы ===
        [Inject] private ResourceService? _resourceService;
        [Inject] private CorpseService? _corpseService;
        [Inject] private NPCGroupService? _groupService;
        [Inject] private INPCService? _npcService;
        [Inject] private TimeService? _timeService;

        // === DI: события ===
        [Inject] private IPublisher<NPCDeathEvent>? _npcDeathPub;
        [Inject] private ISubscriber<ResourceRespawnedEvent>? _respawnedSub;
        [Inject] private ISubscriber<CorpseCreatedEvent>? _corpseCreatedSub;
        [Inject] private ISubscriber<CorpseRemovedEvent>? _corpseRemovedSub;
        [Inject] private ISubscriber<DayChangedEvent>? _daySub;
        [Inject] private ISubscriber<MonthChangedEvent>? _monthSub;
        [Inject] private ISubscriber<YearChangedEvent>? _yearSub;
        [Inject] private ISubscriber<TimeHitchedEvent>? _hitchedSub;

        private IDisposable? _respawnedToken;
        private IDisposable? _corpseCreatedToken;
        private IDisposable? _corpseRemovedToken;
        private IDisposable? _dayToken;
        private IDisposable? _monthToken;
        private IDisposable? _yearToken;
        private IDisposable? _hitchedToken;

        // === Зафиксированные факты ===
        private readonly List<ResourceRespawnedEvent> _respawns = new();
        private readonly List<CorpseCreatedEvent> _corpsesCreated = new();
        private readonly List<string> _corpsesRemoved = new();
        private int _dayEvents, _monthEvents, _yearEvents, _hitchedEvents;
        private int _lastDaySeen, _lastMonthSeen, _lastYearSeen;

        private readonly List<(string Id, string Status, string Detail)> _results = new();

        public override void _Ready()
        {
            var container = Scene.GameBoot.Container;
            if (container != null)
                ContainerAdapter.InjectProperties(this, container);

            _respawnedToken = _respawnedSub?.Subscribe((in ResourceRespawnedEvent e) =>
            {
                _respawns.Add(e);
                GD.Print($"[Stage10Sim] RESPAWNED: ({e.X},{e.Y}) {e.ResourceId} obj={e.OriginalObject}");
            });
            _corpseCreatedToken = _corpseCreatedSub?.Subscribe((in CorpseCreatedEvent e) =>
                GD.Print($"[Stage10Sim] CORPSE CREATED: {e.CorpseId} npc={e.NpcId}"));
            _corpseRemovedToken = _corpseRemovedSub?.Subscribe((in CorpseRemovedEvent e) =>
            {
                _corpsesRemoved.Add(e.CorpseId);
                GD.Print($"[Stage10Sim] CORPSE REMOVED: {e.CorpseId} reason='{e.Reason}'");
            });
            _dayToken = _daySub?.Subscribe((in DayChangedEvent e) =>
            {
                _dayEvents++; _lastDaySeen = e.Day;
                GD.Print($"[Stage10Sim] DAY CHANGED: {e.Day}");
            });
            _monthToken = _monthSub?.Subscribe((in MonthChangedEvent e) =>
            {
                _monthEvents++; _lastMonthSeen = e.Month;
                GD.Print($"[Stage10Sim] MONTH CHANGED: {e.Month}");
            });
            _yearToken = _yearSub?.Subscribe((in YearChangedEvent e) =>
            {
                _yearEvents++; _lastYearSeen = e.Year;
                GD.Print($"[Stage10Sim] YEAR CHANGED: {e.Year}");
            });
            _hitchedToken = _hitchedSub?.Subscribe((in TimeHitchedEvent e) =>
            {
                _hitchedEvents++;
                GD.Print($"[Stage10Sim] TIME HITCHED: +{e.SkippedTicks} ticks");
            });

            GD.Print("[Stage10Sim] Ready — Этап-10 (lifecycle мира) starts in 3s");
            _ = RunAllScenariosAsync();
        }

        public override void _ExitTree()
        {
            _respawnedToken?.Dispose();
            _corpseCreatedToken?.Dispose();
            _corpseRemovedToken?.Dispose();
            _dayToken?.Dispose();
            _monthToken?.Dispose();
            _yearToken?.Dispose();
            _hitchedToken?.Dispose();
        }

        private async Task RunAllScenariosAsync()
        {
            await ToSignal(GetTree().CreateTimer(3.0), SceneTreeTimer.SignalName.Timeout);

            if (_resourceService == null || _corpseService == null || _groupService == null
                || _npcService == null || _timeService == null || _npcDeathPub == null
                || _respawnedSub == null || _corpseCreatedSub == null || _corpseRemovedSub == null
                || _daySub == null || _hitchedSub == null)
            {
                GD.Print("[Stage10Sim] FAIL — DI not wired");
                PrintVerdict(false);
                return;
            }

            await ScenarioA();
            await ScenarioB();
            await ScenarioC();
            await ScenarioD();
            RecordRef();
            PrintSummary();
        }

        // ================================================================
        //  A. ST10-01 — очередь истощённых ресурсов
        // ================================================================

        private static GameTile MakeDepletedTile(ObjectType obj, string resourceId, float max)
            => new GameTile
            {
                X = 0, Y = 0,
                Object = obj,
                IsHarvestable = false,
                ResourceAmount = 0f,
                ResourceMax = max,
                ResourceId = resourceId,
            };

        private int AbsDay() => _timeService!.CurrentTime.TotalMinutes / 60 / 24;

        private async Task ScenarioA()
        {
            GD.Print("[Stage10Sim] ─── A. ST10-01: очередь истощённых ресурсов ───");
            await Task.CompletedTask;
            int day0 = AbsDay();
            var birch = MakeDepletedTile(ObjectType.Tree_Birch, "qa_wood", 40f);
            var ore = MakeDepletedTile(ObjectType.OreVein, "qa_ore", 100f);
            int birchDelay = ObjectDefaults.GetRespawnDays(ObjectType.Tree_Birch);
            int oreDelay = ObjectDefaults.GetRespawnDays(ObjectType.OreVein);
            GD.Print($"[Stage10Sim] delays: birch={birchDelay}, ore={oreDelay}");

            // A1: граница задержки — до срока нет, в срок ровно одно, повторно нет.
            _respawns.Clear();
            _resourceService!.RegisterDepletedResource(11, 11, birch);
            _resourceService.RespawnCheck(day0 + birchDelay - 1);
            bool a1early = _respawns.Count == 0;
            _resourceService.RespawnCheck(day0 + birchDelay);
            bool a1fired = _respawns.Count == 1
                           && _respawns[0].X == 11 && _respawns[0].Y == 11
                           && _respawns[0].ResourceId == "qa_wood";
            _resourceService.RespawnCheck(day0 + birchDelay + 1);
            bool a1 = a1early && a1fired && _respawns.Count == 1; // из очереди изъят
            GD.Print($"[Stage10Sim] A1: граница; early={a1early} fired={a1fired} повтор={_respawns.Count == 1} → {(a1 ? "PASS" : "FAIL")}");
            Record("A1", a1, $"берёза delay={birchDelay}: до срока 0 событий, в срок 1 (11,11 qa_wood), повторный RespawnCheck — 0 (изъят из очереди)");

            // A2: несколько ресурсов на разных координатах — каждый в свой срок.
            _respawns.Clear();
            _resourceService.RegisterDepletedResource(12, 12, birch);
            _resourceService.RegisterDepletedResource(13, 13, ore);
            _resourceService.RespawnCheck(day0 + birchDelay);
            bool a2birch = _respawns.Count(r => r.X == 12) == 1 && _respawns.Count(r => r.X == 13) == 0;
            _resourceService.RespawnCheck(day0 + oreDelay);
            bool a2ore = _respawns.Count(r => r.X == 13) == 1 && _respawns.Count == 2;
            bool a2 = a2birch && a2ore;
            GD.Print($"[Stage10Sim] A2: мульти-ресурсы; birch-срок={a2birch} ore-срок={a2ore} всего={_respawns.Count} → {(a2 ? "PASS" : "FAIL")}");
            Record("A2", a2, $"разные координаты/задержки: берёза в свой срок, руда ({oreDelay}) в свой; событий 2");

            // A3: повторное истощение той же клетки — новая запись, снова стреляет.
            _respawns.Clear();
            _resourceService.RegisterDepletedResource(11, 11, birch);
            _resourceService.RespawnCheck(day0 + birchDelay + 1);
            bool a3 = _respawns.Count == 1 && _respawns[0].X == 11;
            GD.Print($"[Stage10Sim] A3: повторное истощение; событий={_respawns.Count} → {(a3 ? "PASS" : "FAIL")}");
            Record("A3", a3, "повторное истощение (11,11) — новая запись очереди, респаун повторяется");

            // A4: контракт пересборки мира — ResourceService обязан быть IWorldResettable.
            // (приведение к object: sealed-класс без интерфейса иначе не допускается
            // к pattern-матчингу — сам факт отказа компиляции и есть симптом дефекта)
            bool isResettable = (object)_resourceService is IWorldResettable;
            GD.Print($"[Stage10Sim] A4: ResourceService is IWorldResettable = {isResettable} → {(isResettable ? "PASS" : "FAIL")}");
            Record("A4", isResettable,
                isResettable
                    ? "IWorldResettable-контракт: ResolveAll координатора подхватит сброс очереди"
                    : "DEFECT: ResourceService НЕ IWorldResettable — _depleted переживает пересборку мира: armed-событие респауна старого мира стреляет по координатам НОВОЙ карты");

            // A5: изоляция миров — после сброса мира armed-очередь не стреляет.
            _respawns.Clear();
            _resourceService.RegisterDepletedResource(21, 21, birch);
            _resourceService.RegisterDepletedResource(22, 22, ore);
            bool a5;
            if ((object)_resourceService is IWorldResettable resettable)
            {
                resettable.ResetWorld(); // пересборка мира (NewGame/LoadGame-фаза)
                _resourceService.RespawnCheck(day0 + 365);
                a5 = _respawns.Count == 0;
                GD.Print($"[Stage10Sim] A5: изоляция миров; после ResetWorld событий={_respawns.Count} (ожид 0) → {(a5 ? "PASS" : "FAIL")}");
                Record("A5", a5, "ResetWorld → очередь очищена; RespawnCheck(+365 дней) — 0 событий: старый мир не воздействует на новый");
            }
            else
            {
                a5 = false;
                GD.Print("[Stage10Sim] A5: изоляция миров НЕВОЗМОЖНА — ResourceService не IWorldResettable (см. A4) → FAIL");
                Record("A5", false, "без ResetWorld очередь старого мира остаётся armed: событие респауна применится к НОВОЙ карте (TileService.OnResourceRespawned восстанавливает объект по координатам)");
            }

            // A6: Initialize идемпотентен; DayChanged-цепочка респауна жива.
            // Из-за day-boundary: регистрируем herb (delay 2), продвигаем время
            // на 2 дня (запись становится due), публикуем DayChanged — подписка
            // (одна, ??=-идемпотентная после ×3 Initialize) должна дать 1 событие.
            _respawns.Clear();
            _resourceService.Initialize();
            _resourceService.Initialize();
            _resourceService.Initialize();
            var herb = MakeDepletedTile(ObjectType.Herb, "qa_herb", 20f);
            _resourceService.RegisterDepletedResource(31, 31, herb);
            _timeService!.BulkAdvanceTicks(2 * 1440); // +2 дня — herb due
            var dayPub = Scene.GameBoot.Container?.Resolve<IPublisher<DayChangedEvent>>();
            dayPub?.Publish(new DayChangedEvent(_timeService.CurrentTime.Day));
            bool a6 = _respawns.Count == 1 && _respawns[0].X == 31;
            GD.Print($"[Stage10Sim] A6: Initialize ×3 + DayChanged; событий={_respawns.Count} (ожид 1) → {(a6 ? "PASS" : "FAIL")}");
            Record("A6", a6, "Initialize ??=-идемпотентен: ×3 вызова, due-запись при DayChanged — ровно 1 респаун (дублей нет)");

            // Чистим хвосты очереди для следующих секций (если сброс недоступен).
            _resourceService.RespawnCheck(day0 + 400);
        }

        // ================================================================
        //  B. ST10-02 — TTL трупов
        // ================================================================

        private async Task ScenarioB()
        {
            GD.Print("[Stage10Sim] ─── B. ST10-02: TTL трупов ───");
            await Task.CompletedTask;

            // Две живые цели (не из секции C — там свои).
            var alive = _npcService!.GetAllNPCIds().Where(id => _npcService.IsAlive(id)).ToList();
            var groupIds = new HashSet<string>(
                _groupService!.GetAllGroups().SelectMany(g => g.MemberIds));
            var free = alive.Where(id => !groupIds.Contains(id)).ToList();
            if (free.Count < 2)
            {
                GD.Print("[Stage10Sim] B: НЕДОСТАТОЧНО свободных живых NPC → NOT TESTED");
                Record("B1", "NOT TESTED", $"живых вне групп: {free.Count}/2");
                Record("B2", "NOT TESTED", "—");
                Record("B3", "NOT TESTED", "—");
                return;
            }
            string victim1 = free[0], victim2 = free[1];

            // QA-убийство по канону LootSimDebug: реальный флоу ставит
            // IsAlive=false ДО публикации события (иначе CorpseService
            // честно отказывает «труп для живого» — фейк-деф гард).
            foreach (var v in new[] { victim1, victim2 })
            {
                var st = _npcService.GetNPCState(v);
                if (st != null) { st.IsAlive = false; st.CurrentHealth = 0; }
            }
            _corpsesCreated.Clear(); // подписка считает создания
            int corpsesBefore = _corpseService!.CorpseCount;
            _npcDeathPub!.Publish(new NPCDeathEvent(victim1, "qa_stage10"));
            _npcDeathPub.Publish(new NPCDeathEvent(victim2, "qa_stage10"));
            int fresh = _corpseService.CorpseCount - corpsesBefore;
            if (fresh < 2)
            {
                GD.Print($"[Stage10Sim] B: трупов создано {fresh}/2 (NPCDeathEvent-путь) → NOT TESTED");
                Record("B1", "NOT TESTED", $"создано трупов: {fresh}");
                Record("B2", "NOT TESTED", "—");
                Record("B3", "NOT TESTED", "—");
                return;
            }
            var c1 = _corpseService.GetAllCorpses().First(c => c.NpcId == victim1);
            var c2 = _corpseService.GetAllCorpses().First(c => c.NpcId == victim2);

            // B1: свежий труп не тронут TTL-очисткой (граница: возраст < maxAge).
            _corpsesRemoved.Clear();
            int removedFresh = _corpseService.RemoveOldCorpses(1440f);
            bool b1 = removedFresh == 0 && _corpseService.GetCorpse(c1.CorpseId) != null
                      && _corpseService.GetCorpse(c2.CorpseId) != null;
            GD.Print($"[Stage10Sim] B1: свежие трупы живы после TTL-очистки; removed={removedFresh} → {(b1 ? "PASS" : "FAIL")}");
            Record("B1", b1, $"RemoveOldCorpses(1440): возраст свежих < TTL → 0 снято, оба доступны для обыска");

            // B2: состаренный труп снят; CorpseRemovedEvent (рендер/окно обыска).
            c1.DiedAtGameSeconds -= 2000f; // QA-подмена возраста (публичное поле CorpseData)
            int removedAged = _corpseService.RemoveOldCorpses(1440f);
            bool b2 = removedAged == 1 && _corpseService.GetCorpse(c1.CorpseId) == null
                      && _corpsesRemoved.Count == 1 && _corpsesRemoved[0] == c1.CorpseId;
            GD.Print($"[Stage10Sim] B2: состаренный снят; removed={removedAged}, event={_corpsesRemoved.Count} → {(b2 ? "PASS" : "FAIL")}");
            Record("B2", b2, $"возраст 2000+ игросек > TTL 1440 → снят, CorpseRemovedEvent опубликован (рендер-маркер/окно обыска чистятся)");

            // B3: второй (свежий) жив; wiring — NPCModule.Tick:177 (R13).
            bool b3 = _corpseService.GetCorpse(c2.CorpseId) != null && _corpseService.CorpseCount >= 1;
            GD.Print($"[Stage10Sim] B3: второй труп жив; count={_corpseService.CorpseCount} → {(b3 ? "PASS" : "FAIL")}");
            Record("B3", b3, "выборочность: снят только состаренный; вызов очистки в игровом тике — NPCModule.Tick (R13, 1440 игросек)");
        }

        // ================================================================
        //  C. ST10-03 — гигиена групп при смерти участников
        // ================================================================

        private async Task ScenarioC()
        {
            GD.Print("[Stage10Sim] ─── C. ST10-03: группы и смерть участников ───");
            await Task.CompletedTask;

            var alive = _npcService!.GetAllNPCIds().Where(id => _npcService.IsAlive(id)).ToList();
            // Секция B уже убила двух; берём следующих живых (B идёт раньше C).
            var deadPool = _npcService.GetAllNPCIds().Where(id => !_npcService.IsAlive(id)).ToList();
            var candidates = alive.Except(deadPool).ToList();
            if (candidates.Count < 3)
            {
                GD.Print($"[Stage10Sim] C: живых кандидатов {candidates.Count}/3 → NOT TESTED");
                Record("C1", "NOT TESTED", $"кандидатов: {candidates.Count}");
                Record("C2", "NOT TESTED", "—");
                Record("C3", "NOT TESTED", "—");
                return;
            }
            string gId = _groupService!.CreateGroup(GroupTaskType.Patrol, faction: "qa_stage10");
            string n1 = candidates[0], n2 = candidates[1], n3 = candidates[2];
            _groupService.AddMember(gId, n1, GroupRole.Leader);
            _groupService.AddMember(gId, n2);
            _groupService.AddMember(gId, n3);
            int groupsBefore = _groupService.GroupCount;

            // C1: смерть лидера → лидерство следующему, мёртвый исключён.
            _npcDeathPub!.Publish(new NPCDeathEvent(n1, "qa_stage10"));
            var gAfterLeaderDeath = _groupService.GetGroup(gId);
            bool c1 = gAfterLeaderDeath != null
                      && !gAfterLeaderDeath.MemberIds.Contains(n1)
                      && gAfterLeaderDeath.LeaderId == n2
                      && _groupService.GetGroupByMember(n1) == null;
            GD.Print($"[Stage10Sim] C1: смерть лидера {n1}; участники=[{string.Join(",", gAfterLeaderDeath?.MemberIds ?? new List<string>())}] лидер={gAfterLeaderDeath?.LeaderId} (ожид {n2}) → {(c1 ? "PASS" : "FAIL")}");
            Record("C1", c1, c1
                ? "мёртвый лидер исключён, лидерство передано следующему участнику"
                : "DEFECT: смерть NPC не обрабатывается группой — мёртвый остаётся участником/лидером (нет подписки на NPCDeathEvent)");

            // C2: смерть второго → лидерство третьему.
            _npcDeathPub.Publish(new NPCDeathEvent(n2, "qa_stage10"));
            var gAfter2 = _groupService.GetGroup(gId);
            bool c2 = gAfter2 != null && gAfter2.LeaderId == n3
                      && !gAfter2.MemberIds.Contains(n2)
                      && gAfter2.MemberIds.Count == 1;
            GD.Print($"[Stage10Sim] C2: смерть {n2}; лидер={gAfter2?.LeaderId} (ожид {n3}), участников={gAfter2?.MemberIds.Count} → {(c2 ? "PASS" : "FAIL")}");
            Record("C2", c2, "последовательные смерти: лидерство migrating, состав сжимается");

            // C3: смерть последнего → группа расформирована, реестр чист.
            _npcDeathPub.Publish(new NPCDeathEvent(n3, "qa_stage10"));
            bool c3 = _groupService.GetGroup(gId) == null
                      && _groupService.GetGroupByMember(n3) == null
                      && _groupService.GroupCount == groupsBefore - 1; // минус наша (расформирована)
            GD.Print($"[Stage10Sim] C3: смерть последнего; группа расформирована={_groupService.GetGroup(gId) == null}, GroupCount={_groupService.GroupCount} (до {groupsBefore}, ожид {groupsBefore - 1}) → {(c3 ? "PASS" : "FAIL")}");
            Record("C3", c3, c3
                ? "пустая группа расформирована (счётчик -1), member→group реестр чист"
                : "DEFECT: пустая группа/мёртвый участник остаются в реестре (висячие ссылки, переживают Save/Load)");
        }

        // ================================================================
        //  D. ST10-04 — скачки времени (BulkAdvanceTicks)
        // ================================================================

        private async Task ScenarioD()
        {
            GD.Print("[Stage10Sim] ─── D. ST10-04: скачки времени ───");

            // D1: скачок 1500 тиков (25 игровых часов) — только TimeHitched,
            //     календарные события НЕ публикуются в самом скачке.
            _dayEvents = 0; _monthEvents = 0; _yearEvents = 0; _hitchedEvents = 0;
            var before = _timeService!.CurrentTime;
            _timeService.BulkAdvanceTicks(1500);
            var after = _timeService.CurrentTime;
            bool d1 = _hitchedEvents == 1 && _dayEvents == 0
                      && after.TotalMinutes - before.TotalMinutes == 1500;
            GD.Print($"[Stage10Sim] D1: BulkAdvanceTicks(1500); hitched={_hitchedEvents}, day-событий в скачке={_dayEvents}, минуты {before.TotalMinutes}→{after.TotalMinutes} → {(d1 ? "PASS" : "FAIL")}");
            Record("D1", d1, "в скачке: ровно 1 TimeHitchedEvent, календарные события НЕ публикуются (контракт P1-13)");

            // D2: первый естественный тик после скачка — ровно ОДИН DayChanged
            //     (финальная дата), без месяца/года; маркеры ре-синхронизированы
            //     (последующие тики молчат).
            bool gotDay = await WaitUntil(() => _dayEvents >= 1, 20f);
            bool d2 = gotDay && _dayEvents == 1 && _monthEvents == 0 && _yearEvents == 0
                      && _lastDaySeen == _timeService.CurrentTime.Day;
            // Подождём ещё пару тиков: повторов быть не должно.
            await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);
            bool d2stable = _dayEvents == 1 && _monthEvents == 0 && _yearEvents == 0;
            GD.Print($"[Stage10Sim] D2: тик после скачка; DayChanged={_dayEvents} (ожид 1, day={_lastDaySeen}), Month={_monthEvents}, Year={_yearEvents}, стабилен={d2stable} → {(d2 && d2stable ? "PASS" : "FAIL")}");
            Record("D2", d2 && d2stable, "первый тик после скачка: ровно 1 DayChanged (финальная дата), Month/Year тишина, дальнейшие тики молчат (маркеры синхронизированы)");

            // D3: переход месяца (+30 дней) и года (+360).
            // Скачок ровно на N месяцев оставляет day-of-month тем же —
            // пост-фикс контракт: DayChanged молчит, Month/Year стреляют
            // ровно один раз на первом естественном тике (префикс: события
            // НЕ публиковались ВООБЩЕ — месяц/год вложены в ветку дня).
            _dayEvents = 0; _monthEvents = 0; _yearEvents = 0; _hitchedEvents = 0;
            _timeService.BulkAdvanceTicks(30 * 1440);
            bool d3h1 = _hitchedEvents == 1 && _dayEvents == 0 && _monthEvents == 0;
            await WaitUntil(() => _monthEvents >= 1, 20f);
            bool d3m = _hitchedEvents == 1 && _monthEvents == 1 && _dayEvents == 0 && _yearEvents == 0;
            GD.Print($"[Stage10Sim] D3a: месяц; hitched={_hitchedEvents} day={_dayEvents} (ожид 0 — day-of-month тот же) month={_monthEvents} year={_yearEvents} → {(d3h1 && d3m ? "PASS" : "FAIL")}");
            Record("D3a", d3h1 && d3m, "+30 дней (day-of-month неизменен): 1 hitched в скачке; на тике — MonthChanged 1×, день/год молчат (календарные компоненты независимы)");

            _dayEvents = 0; _monthEvents = 0; _yearEvents = 0; _hitchedEvents = 0;
            _timeService.BulkAdvanceTicks(360 * 1440);
            bool d3h2 = _hitchedEvents == 1 && _dayEvents == 0 && _yearEvents == 0;
            await WaitUntil(() => _yearEvents >= 1, 20f);
            bool d3y = _hitchedEvents == 1 && _yearEvents == 1 && _monthEvents == 0 && _dayEvents == 0;
            GD.Print($"[Stage10Sim] D3b: год; hitched={_hitchedEvents} day={_dayEvents} month={_monthEvents} (ожид 0 — month-of-year тот же) year={_yearEvents} → {(d3h2 && d3y ? "PASS" : "FAIL")}");
            Record("D3b", d3h2 && d3y, "+360 дней (только год меняется): 1 hitched; на тике — YearChanged 1×, месяц/день молчат");

            // D4 (GAP-заметка): подписчики, считающие DayChanged-события
            // (квесты «прожить N дней», старение), получают за скачок ≤ 1
            // событие — документированная семантика P1-13 (per-tick эффекты
            // пропускаются, hitch сигнализируется отдельно). Скачок ровно на
            // N месяцев/лет НЕ меняет day-of-month/month-of-year — переходные
            // события этих компонентов молчат по значению (финальное состояние
            // равно исходному). Решение о счёте «пропущенных» дней/месяцев —
            // за владельцем; поведение зафиксировано, не менялось.
            Record("D4", "GAP",
                "календарные подписчики считают события, не разность дат: hitch через N дней даёт ≤1 DayChanged; скачок ровно в N месяцев/лет не меняет младший компонент — его событие молчит. Квесты «прожить N дней»/старение недосчитывают скачок — дизайн-решение за владельцем (считать hitched-время или оставить)");
        }

        // ================================================================
        //  E. Ссылки (не считаются в вердикт)
        // ================================================================

        private void RecordRef()
        {
            // E1: изоляция миров — покрыта A4/A5 (сброс очереди ресурсов).
            // E2: PT-C02 (агро-сброс на смерть игрока) — RESPAWN-сим шаг 5
            //     выполняется в зоне контроля этапа; ФИЗИЧЕСКИЙ пункт
            //     плейтеста симулятором не закрывается (канон).
            _results.Add(("E2", "REF",
                "PT-C02: агро-сброс на смерть — RESPAWN-сим (зона контроля этапа); физический пункт плейтеста остаётся владельцу"));
        }

        // ================================================================
        //  ХЕЛПЕРЫ
        // ================================================================

        private async Task<bool> WaitUntil(System.Func<bool> predicate, float timeoutSec)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSec)
            {
                if (predicate()) return true;
                await ToSignal(GetTree().CreateTimer(0.25), SceneTreeTimer.SignalName.Timeout);
                elapsed += 0.25f;
            }
            return predicate();
        }

        private void Record(string id, bool pass, string detail)
            => _results.Add((id, pass ? "PASS" : "FAIL", detail));

        private void Record(string id, string status, string detail)
            => _results.Add((id, status, detail));

        private void PrintSummary()
        {
            GD.Print("[Stage10Sim] ═══════ СВОДКА ЭТАПА-10 ═══════");
            int pass = 0, fail = 0, notTested = 0, gap = 0, refN = 0;
            foreach (var (id, status, detail) in _results)
            {
                GD.Print($"[Stage10Sim] {id,-5} {status,-10} {detail}");
                if (status == "PASS") pass++;
                else if (status == "FAIL") fail++;
                else if (status == "GAP") gap++;
                else if (status == "REF") refN++;
                else notTested++;
            }
            GD.Print($"[Stage10Sim] Итого: PASS={pass} FAIL={fail} NOT TESTED={notTested} GAP={gap} REF={refN}");
            PrintVerdict(fail == 0 && notTested == 0 && pass > 0);
        }

        private static void PrintVerdict(bool pass)
        {
            GD.Print($"[Stage10Sim] VERDICT: {(pass ? "PASS — ST10-01..04 проверены (GAP: hitched-счёт дней, сохранение состояния тайлов)" : "FAIL")}");
        }
    }
}
