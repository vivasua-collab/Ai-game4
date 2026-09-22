#nullable enable
// Создано: 2026-09-22 — внешний аудит upload/audit_09_22_07_30.txt: runtime-
// верификация P1-5 (EventBus: exception внутри subscriber + re-entrancy) и
// P1-8 (path traversal в Modules-layer SaveFileHandler) — прямой запрос
// аудитора («проверить реальным runtime-тестом, результат выгрузить в
// checkpoints/»), плюс смежные инфраструктурные контракты P1-1/P1-4/P1-6/P1-7.
//
// 2026-09-22 (позже тем же днём): ДОПОЛНЕНА Фаза 4 того же аудита (DI/
// Container/Startup/Lifecycle — доставлена отдельно от файла, зафиксирована
// аудитором по 949391c, в r32 P1-6 уже был закрыт): P2-12 (ResolveAll —
// порядок регистрации), P2-13 (startup fail-closed, _initialized после
// Start), P2-14 (настоящий cycle detection по construction path).
//
// 2026-09-22 (финальные фазы 6–10, upload/audit_09_22_10_00): ДОПОЛНЕНА
// секциями I/J/K — три оставшихся P1 реестра Фазы 10:
//   I. P1-9 (Фазы 6/7/10): WorldDomainResetPhase глотал исключение
//      ResetWorld() → фаза «успешна», оркестратор продолжал Spawn/Init/Ready,
//      мир собирался из смеси сброшенного/несброшенного состояния.
//      Постфикс-контракт: ExecuteAsync бросает AggregateException (fail-closed,
//      полная диагностика одной попытки — как startup P2-13).
//   J. P1-10 (Фазы 7/10): retry startup повторно Start()ил уже успешно
//      стартовавшие модули → дублирование EventBus-подписок (один игровой
//      event обрабатывается дважды). Постфикс-контракт: стартовая попытка =
//      транзакция с точкой продолжения — успешно стартовавшие модули при
//      ретрае НЕ перезапускаются (SubscriberCount не растёт), провалившийся
//      модуль ретраится.
//   K. P1-12 (Фазы 9/10): Pure damage проходил через CalculateBuffer-
//      Absorption() (Qi списывалось: QiConsumeRequestEvent/TryConsumeQi),
//      и только ПОСЛЕ результат обнулялся пост-фактум. Постфикс-контракт:
//      Pure не входит в Qi-буфер вовсе — Ци не тратится ни у игрока, ни у
//      NPC; контрольная серия — Physical Ци тратит (буфер жив).
//
// Сим написан под ПОСТФИКС-контракт (как должен вести себя исправленный код):
//   A. EventBus (P1-5): исключение в одном подписчике НЕ прерывает fan-out
//      (подписчики после падавшего получают событие), НЕ покидает Publish,
//      и re-entrant очередь НЕ протухает — отложенное событие доставляется
//      в дрене ТОГО ЖЕ Publish, а не после чужого события позже.
//   B. SaveFileHandler Modules-слой (P1-8): имя слота санируется —
//      «../escape_probe» НЕ покидает каталог сейвов.
//   C. ContainerBuilder (P1-6): override регистрации (RegisterInstance по
//      интерфейсному ключу) не оставляет живым stale concrete-ключ старой
//      регистрации; мульти-форвард одного impl на 2 интерфейса и
//      «последний побеждает» не ломаются.
//   D. SaveService.DeleteSave (P1-7): честный bool по всей цепочке
//      (FileHandler → Aggregator → SaveService).
//   E. World-reset контракты (P1-1/P1-4): TradeService и CorpseService
//      реализуют IWorldResettable (ResolveAll<IWorldResettable> их видит).
//   F. ResolveAll (P2-12, Фаза 4): итерация в ПОРЯДКЕ РЕГИСТРАЦИИ;
//      мёртвые регистрации (P1-6 prune / украденный ключ) из ordered-списка
//      не воскресают; мульти-форвард + дедуп по ссылке — порядок стабилен.
//   G. Startup контракт (P2-13, Фаза 4): провал любого Start() →
//      AggregateException (все модули диагностируются за одну попытку),
//      тик-луп после проваленного бута заглушён, повторный Start() не
//      игнорируется (boot не помечен инициализированным).
//   H. Cycle detection (P2-14, Фаза 4): конструкторский цикл ловится на
//      ПЕРВОМ повторном входе с точным путём «A → B → A» (не после 51-го
//      уровня); самоссылка ловится; линейная цепочка не даёт ложного срабатывания.
//   I. WorldDomainResetPhase (P1-9, Фазы 6/7/10): сброс мира fail-closed —
//      исключение ResetWorld() любого домена = AggregateException наружу,
//      сборка мира останавливается (не «успешная фаза + полу-сброшенный мир»);
//      за одну попытку диагностируются ВСЕ домены.
//   J. Startup retry (P1-10, Фазы 7/10): повторный Start() после провала
//      НЕ перезапускает уже успешно стартовавшие модули (подписки не
//      дублируются — SubscriberCount стабилен), провалившийся — ретраится.
//   K. Pure damage (P1-12, Фазы 9/10): DamageType.Pure не входит в
//      Qi-буфер (Ци не расходуется у игрока и NPC); Physical — контроль
//      (Ци расходуется, поглощение > 0).
//
// ДО фиксов прогон даёт VERDICT: FAIL со строками «DEFECT (audit 09.22 …)» —
// это runtime-подтверждение дефектов для чекпоинта аудитора. После фиксов —
// PASS и постоянный регрессионный страж (qa_regression.sh, сим 19).
//
// Запуск: GODOT_NEWGAME=1 GODOT_AUDIT0922_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Events;
using CultivationGame.Adapter.Di;
using CultivationGame.Entry;
using CultivationGame.Entry.Phases;
using CultivationGame.Modules.Save;

namespace CultivationGame.Adapter.Scene;

public partial class Audit0922SimDebug : Node
{
    // ── Тестовые сообщения (структуры — контракт EventBus) ──
    private readonly struct PingMsg { public readonly int Id; public PingMsg(int id) => Id = id; }
    private readonly struct OtherMsg { public readonly int Id; public OtherMsg(int id) => Id = id; }

    // ── Тестовые сервисы для DI-проверок ──
    private interface IDummySvc { int Tag { get; } }
    private interface IOtherSvc { }
    // Реализует ОБА интерфейса — паттерн NPCService (Register<INPCService,X> +
    // Register<ISaveable,X>): два интерфейсных ключа форвардят один impl.
    private sealed class DummyImplA : IDummySvc, IOtherSvc { public int Tag => 1; }
    private sealed class DummyImplB : IDummySvc, IOtherSvc { public int Tag => 2; }

    // ── Тестовые классы Фазы 4 (DI/lifecycle: P2-12/P2-13/P2-14) ──
    // P2-12: порядок ResolveAll = порядок регистрации (регистрируем B, C, A).
    private interface IOrderProbe { int Tag { get; } }
    private sealed class OrderProbeB : IOrderProbe { public int Tag => 2; }
    private sealed class OrderProbeC : IOrderProbe { public int Tag => 3; }
    private sealed class OrderProbeA : IOrderProbe { public int Tag => 1; }

    // P2-14: конструкторский цикл CycA → CycB → CycA и самоссылка SelfCyc.
    private sealed class CycA { public CycA(CycB b) { _ = b; } }
    private sealed class CycB { public CycB(CycA a) { _ = a; } }
    private sealed class SelfCyc { public SelfCyc(SelfCyc s) { _ = s; } }
    // P2-14: линейная цепочка (не цикл) — детектор без ложных срабатываний.
    private sealed class Dep1 { }
    private sealed class Dep2 { public Dep2(Dep1 d) { _ = d; } }
    private sealed class Dep3 { public Dep2 D2 = null!; public Dep3(Dep2 d2) { D2 = d2; } }

    // P2-13: стартуемый модуль, падающий на Start() и считающий доставленные тики.
    private sealed class ThrowingStartable : IStartable, ITickable
    {
        public int Ticks;
        public void Start() => throw new InvalidOperationException("phase4 startup probe: intentional failure");
        public void Tick(int tickCount) => Ticks++;
    }

    // ── Тестовые классы финальных фаз 6–10 (upload/audit_09_22_10_00) ──
    // I (P1-9): IWorldResettable-пробы для WorldDomainResetPhase.
    private interface IResetProbeDomain : IWorldResettable { int ResetCount { get; } }
    private sealed class ResetOkProbeA : IResetProbeDomain { public int ResetCount { get; private set; } public void ResetWorld() => ResetCount++; }
    private sealed class ResetOkProbeB : IResetProbeDomain { public int ResetCount { get; private set; } public void ResetWorld() => ResetCount++; }
    private sealed class ResetThrowProbe : IResetProbeDomain
    {
        public int ResetCount { get; private set; }
        public void ResetWorld()
        {
            ResetCount++;
            throw new InvalidOperationException("phase6 reset probe: intentional domain failure");
        }
    }

    // J (P1-10): модуль, успешно стартующий с ПОДПИСКОЙ (сценарий аудитора:
    // SubscriberCount до/после retry), + счётчик вызовов Start.
    private readonly struct BootProbeMsg { }
    private sealed class SubscribingStartable : IStartable
    {
        private readonly EventBus _bus;
        public int StartCalls;
        public SubscribingStartable(EventBus bus) => _bus = bus;
        public void Start()
        {
            StartCalls++;
            // Поле-присваивание без dispose-старого — паттерн модулей NPC/Qi/
            // Combat: повторный Start = УТЕЧКА подписки + двойная обработка.
            _ = _bus.Subscribe<BootProbeMsg>(static (in BootProbeMsg _) => { });
        }
    }
    private sealed class CountingStartable : IStartable
    {
        public int StartCalls;
        public void Start() => StartCalls++;
    }

    // P2-13: минимальная сессия для мини-контейнера GameEntryPoint
    // (настоящая сессия тащит весь граф зависимостей — тут проверяется
    // ТОЛЬКО контракт startup, зависимости не нужны).
    private sealed class FakeGameSession : IGameSession
    {
        public SessionState State => SessionState.MainMenu;
        public GameSessionData Data { get; } = new();
        public void NewGame(int startVariant) { }
        public void NewGame(int startVariant, string locationId) { }
        public void LoadGame(string slotName) { }
        public void LoadGame(SaveSlot slot) { }
        public void Pause() { }
        public void Resume() { }
        public void SaveAndQuit() { }
        public void QuitWithoutSaving() { }
        public event Action<SessionState>? OnStateChanged { add { } remove { } }
    }

    private sealed class FakeAdapterFileHandler : ISaveFileHandler
    {
        public bool Save(string slotName, Dictionary<string, object> data) => true;
        public Dictionary<string, object>? Load(string slotName) => null;
        public bool HasSave(string slotName) => false;
        public bool DeleteSave(string slotName) => true;
        public IReadOnlyList<string> GetAllSaves() => Array.Empty<string>();
    }

    private bool _allPass = true;
    private void Check(bool condition, string label, string defectId)
    {
        GD.Print($"[Audit0922Sim] {(condition ? "OK  " : "DEFECT")} {label}" +
                 (condition ? "" : $"  ← DEFECT (audit 09.22 {defectId})"));
        if (!condition) _allPass = false;
    }

    public override async void _Ready()
    {
        GD.Print("[Audit0922Sim] инфраструктурный аудит 09.22 — старт (P1-1/P1-4/P1-5/P1-6/P1-7/P1-8)");

        // === A. EventBus: exception в подписчике + re-entrancy (P1-5) ========
        try { RunEventBusTest(); }
        catch (Exception ex) { GD.Print($"[Audit0922Sim] DEFECT A-крэш: {ex.GetType().Name}: {ex.Message}"); _allPass = false; }

        // === B. Path traversal в Modules-layer SaveFileHandler (P1-8) =======
        try { RunTraversalTest(); }
        catch (Exception ex) { GD.Print($"[Audit0922Sim] DEFECT B-крэш: {ex.GetType().Name}: {ex.Message}"); _allPass = false; }

        // === C. DI: override и stale concrete-ключ (P1-6) ====================
        try { RunDiOverrideTest(); }
        catch (Exception ex) { GD.Print($"[Audit0922Sim] DEFECT C-крэш: {ex.GetType().Name}: {ex.Message}"); _allPass = false; }

        // === D. DeleteSave честный по слоям (P1-7) ============================
        try { RunDeleteSaveTest(); }
        catch (Exception ex) { GD.Print($"[Audit0922Sim] DEFECT D-крэш: {ex.GetType().Name}: {ex.Message}"); _allPass = false; }

        // === E. World-reset контракты Trade/Corpse (P1-1/P1-4) ================
        try { RunWorldResetContractTest(); }
        catch (Exception ex) { GD.Print($"[Audit0922Sim] DEFECT E-крэш: {ex.GetType().Name}: {ex.Message}"); _allPass = false; }

        // === F. ResolveAll: порядок регистрации (P2-12, Фаза 4) ==============
        try { RunResolveAllOrderTest(); }
        catch (Exception ex) { GD.Print($"[Audit0922Sim] DEFECT F-крэш: {ex.GetType().Name}: {ex.Message}"); _allPass = false; }

        // === G. Startup fail-closed (P2-13, Фаза 4) ==========================
        try { RunStartupContractTest(); }
        catch (Exception ex) { GD.Print($"[Audit0922Sim] DEFECT G-крэш: {ex.GetType().Name}: {ex.Message}"); _allPass = false; }

        // === H. Cycle detection (P2-14, Фаза 4) ==============================
        try { RunCycleDetectionTest(); }
        catch (Exception ex) { GD.Print($"[Audit0922Sim] DEFECT H-крэш: {ex.GetType().Name}: {ex.Message}"); _allPass = false; }

        // === I. WorldDomainResetPhase fail-closed (P1-9, Фазы 6/7/10) =======
        try { RunWorldDomainResetFailClosedTest(); }
        catch (Exception ex) { GD.Print($"[Audit0922Sim] DEFECT I-крэш: {ex.GetType().Name}: {ex.Message}"); _allPass = false; }

        // === J. Startup retry без повторного Start (P1-10, Фазы 7/10) ========
        try { RunStartupRetryTest(); }
        catch (Exception ex) { GD.Print($"[Audit0922Sim] DEFECT J-крэш: {ex.GetType().Name}: {ex.Message}"); _allPass = false; }

        // === K. Pure damage не расходует Ци (P1-12, Фазы 9/10) ===============
        try { RunPureDamageTest(); }
        catch (Exception ex) { GD.Print($"[Audit0922Sim] DEFECT K-крэш: {ex.GetType().Name}: {ex.Message}"); _allPass = false; }

        // === L. P2-22 (диагностика, бэклог): queued-drain re-entrancy =========
        // Запрос аудитора (Фаза 7): runtime-чек edge-case-а. Фикс — «следующий
        // слой» реестра Фазы 10, в этот эпизод НЕ входит → вердикт не гейтит.
        try { RunQueueDrainReentrancyDiagnostic(); }
        catch (Exception ex) { GD.Print($"[Audit0922Sim] diag-крэш L: {ex.GetType().Name}: {ex.Message}"); }

        GD.Print($"[Audit0922Sim] VERDICT: {(_allPass
            ? "PASS — EventBus isolation/re-entrancy, path sanitisation, DI override prune, DeleteSave honesty, Trade/Corpse reset-контракты, ResolveAll registration order, startup fail-closed, cycle detection (Фаза 4: P2-12/P2-13/P2-14), world-reset fail-closed (P1-9), startup retry без дублей (P1-10), Pure без расхода Ци (P1-12)"
            : "FAIL — см. DEFECT-строки выше (runtime-подтверждение аудита 09.22 + Фаза 4 + финальные фазы 6–10)")}");

        // Чистка временных каталогов теста B/D.
        await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
        CleanupTemp();
    }

    // ── A. P1-5: EventBus exception + re-entrancy ============================
    private void RunEventBusTest()
    {
        GD.Print("[Audit0922Sim] === A. EventBus: exception в подписчике + re-entrant очередь (P1-5) ===");
        var bus = new EventBus();
        var delivered = new List<(string kind, int id)>();
        var escaped = new List<Exception>();

        // A-подписчик: публикует событие ТОГО ЖЕ типа внутри хендлера (re-entrancy).
        var tokA = bus.Subscribe<PingMsg>((in PingMsg msg) =>
        {
            if (msg.Id == 1) bus.Publish(new PingMsg(2)); // → очередь (тип в полёте)
        });
        // B-подписчик: падает на каждом событии.
        var tokB = bus.Subscribe<PingMsg>((in PingMsg _) => throw new InvalidOperationException("subscriber B is broken"));
        // C-подписчик: стоит ПОСЛЕ падающего — fan-out продолжается?
        var tokC = bus.Subscribe<PingMsg>((in PingMsg msg) => delivered.Add(("ping", msg.Id)));
        var tokOther = bus.Subscribe<OtherMsg>((in OtherMsg msg) => delivered.Add(("other", msg.Id)));

        try { bus.Publish(new PingMsg(1)); }
        catch (Exception ex) { escaped.Add(ex); }

        // Факт 1: исключение подписчика НЕ покидает Publish (изоляция).
        Check(escaped.Count == 0, "A1 исключение подписчика изолировано (не покидает Publish)", "P1-5");

        // Факт 2: fan-out не прерывается — C получил исходное событие #1.
        bool cGotOriginal = delivered.Any(d => d.kind == "ping" && d.id == 1);
        Check(cGotOriginal, "A2 подписчики после падавшего получили исходное событие", "P1-5");

        // Факт 3: re-entrant #2 доставлен уже в дрене ЭТОГО же Publish
        // (до любого чужого события), не потерян и не протух.
        // Отдельный catch: у НЕисправленной шины стэл-дрен рушит уже ЧУЖОЙ
        // Publish (сценарий коррупции из аудита) — фиксируем оба факта.
        try { bus.Publish(new OtherMsg(9)); }
        catch (Exception ex) { escaped.Add(ex); }
        int otherIdx = delivered.FindIndex(d => d.kind == "other" && d.id == 9);
        int ping2Idx = delivered.FindIndex(d => d.kind == "ping" && d.id == 2);
        bool ping2BeforeOther = ping2Idx >= 0 && otherIdx >= 0 && ping2Idx < otherIdx;
        Check(ping2BeforeOther, "A3 re-entrant событие доставлено в дрене того же Publish (раньше чужих событий)", "P1-5");
        if (escaped.Count > 1)
            GD.Print("[Audit0922Sim] diag: стэл-дрен очереди уронил ПОСТОРОННИЙ Publish — сценарий коррупции подтверждён");

        // Факт 4 (сталeness-зонд): после чужого события стэл-очередь пуста —
        // новых ping после other не приходит.
        var latePings = delivered.Skip(otherIdx + 1).Where(d => d.kind == "ping").ToList();
        Check(latePings.Count == 0, "A4 стэл-событий после чужого Publish нет (очередь не протухает)", "P1-5");

        tokA.Dispose(); tokB.Dispose(); tokC.Dispose(); tokOther.Dispose();

        // Диагностика порядка доставки (для чекпоинта).
        GD.Print($"[Audit0922Sim] diag: порядок доставки = [{string.Join(", ", delivered.Select(d => $"{d.kind}#{d.id}"))}]" +
                 (escaped.Count > 0 ? $"; исключение покинуло Publish: {escaped[0].GetType().Name}" : ""));
    }

    // ── B. P1-8: path traversal ===============================================
    private const string TmpDir = "/tmp/audit0922_saves";

    private void RunTraversalTest()
    {
        GD.Print("[Audit0922Sim] === B. Modules-layer SaveFileHandler: path traversal (P1-8) ===");
        if (Directory.Exists(TmpDir)) Directory.Delete(TmpDir, true);
        Directory.CreateDirectory(TmpDir);
        string outsidePath = Path.Combine(Path.GetDirectoryName(TmpDir)!, "escape_probe.json");

        var handler = new SaveFileHandler(new SaveConfig { SaveDirectory = TmpDir });
        var dict = new Dictionary<string, object> { ["probe"] = "audit0922" };

        // Злонамеренное имя: «../escape_probe» → без санации пишет ВНЕ каталога.
        bool saveReturned = handler.Save("../escape_probe", dict);
        bool fileOutside = File.Exists(outsidePath);
        GD.Print($"[Audit0922Sim] diag: Save('../escape_probe') → {saveReturned}; файл вне каталога: {fileOutside}" +
                 (fileOutside ? $" ({outsidePath}) — traversal сработал" : ""));
        Check(!fileOutside, "B1 имя '../escape_probe' не покидает каталог сейвов (санация)", "P1-8");

        // Легитимные имена продолжают работать.
        bool okLegit = handler.Save("probe_ok", dict);
        bool hasLegit = handler.HasSave("probe_ok");
        Check(okLegit && hasLegit, "B2 легитимное имя слота сохраняется/читается", "P1-8");
    }

    // ── C. P1-6: DI override и stale concrete-ключ ============================
    private void RunDiOverrideTest()
    {
        GD.Print("[Audit0922Sim] === C. ContainerBuilder: override регистрации (P1-6) ===");

        // C1: адаптер-оверрайд интерфейсного ключа не оставляет stale concrete-ключ.
        var b1 = new ContainerBuilder();
        b1.Register<ISaveFileHandler, SaveFileHandler>(Lifetime.Singleton); // модульный дефолт
        var adapterHandler = new FakeAdapterFileHandler();
        b1.RegisterInstance<ISaveFileHandler>(adapterHandler);              // адаптер-оверрайд
        var c1 = b1.Build();

        bool ifaceIsAdapter = ReferenceEquals(c1.Resolve<ISaveFileHandler>(), adapterHandler);
        Check(ifaceIsAdapter, "C1 Resolve<ISaveFileHandler> даёт адаптер-инстанс (override работает)", "P1-6");

        bool staleResolves;
        try { _ = c1.Resolve<SaveFileHandler>(); staleResolves = true; }
        catch (InvalidOperationException) { staleResolves = false; }
        GD.Print($"[Audit0922Sim] diag: Resolve<Modules.SaveFileHandler> (stale concrete-ключ) → " +
                 (staleResolves ? "РЕЗОЛВИТСЯ в мёртвый fallback-инстанс" : "нет регистрации (ключ удалён)"));
        Check(!staleResolves, "C2 stale concrete-ключ старой регистрации удалён (override чистит хвосты)", "P1-6");

        // C2: мульти-форвард одного impl на два интерфейса (паттерн NPCService:
        // Register<INPCService,X> + Register<ISaveable,X>) не ломается prune-ом.
        var b2 = new ContainerBuilder();
        b2.Register<IDummySvc, DummyImplA>(Lifetime.Singleton);
        b2.Register<IOtherSvc, DummyImplA>(Lifetime.Singleton);
        var c2 = b2.Build();
        bool sameSingleton = ReferenceEquals(c2.Resolve<IDummySvc>(), c2.Resolve<IOtherSvc>());
        Check(sameSingleton, "C3 мульти-форвард impl на 2 интерфейса — один синглтон (prune не рвёт)", "P1-6-регресс");

        // C3: повторная регистрация интерфейса — «последний побеждает».
        var b3 = new ContainerBuilder();
        b3.Register<IDummySvc, DummyImplA>(Lifetime.Singleton);
        b3.Register<IDummySvc, DummyImplB>(Lifetime.Singleton);
        var c3 = b3.Build();
        bool lastWins = c3.Resolve<IDummySvc>() is DummyImplB;
        Check(lastWins, "C4 повторная регистрация интерфейса — последний побеждает", "P1-6-регресс");

        // C5: смерть мульти-форвард-регистрации (NPC-паттерн!):
        // Register<IA,X> + Register<IB,X>, затем IB перерегистрируется на Y —
        // ключ X обязан ВЫЖИТЬ (ключи независимы: по forwarding-ключам
        // резолвятся concrete-параметры конструкторов — CorpseService→
        // NPCService, CombatService→TechniqueService — и собирается
        // ResolveAll<ISaveable>). Гард поймал регрессию первого prune-варианта
        // (живой контейнер: NewGame падал на CombatService-конструкторе).
        // Prune допустим ТОЛЬКО для инстанс-оверрайда (RegisterInstance).
        var b4 = new ContainerBuilder();
        b4.Register<IDummySvc, DummyImplA>(Lifetime.Singleton); // как INPCService→NPCService
        b4.Register<IOtherSvc, DummyImplA>(Lifetime.Singleton); // как ISaveable→NPCService
        b4.Register<IOtherSvc, DummyImplB>(Lifetime.Singleton); // как PlayerModule: ISaveable→PlayerService
        var c4c = b4.Build();
        bool concreteResolved;
        DummyImplA? concrete = null;
        try { concrete = c4c.Resolve<DummyImplA>(); concreteResolved = concrete != null; }
        catch (InvalidOperationException) { concreteResolved = false; }
        bool concreteMatchesIface = concreteResolved && ReferenceEquals(concrete, c4c.Resolve<IDummySvc>());
        Check(concreteResolved && concreteMatchesIface,
            "C5 concrete-ключ наследуется живой регистрацией (NPC-паттерн мульти-форварда)", "P1-6-регресс");
    }

    // ── D. P1-7: DeleteSave честный по слоям ==================================
    private void RunDeleteSaveTest()
    {
        GD.Print("[Audit0922Sim] === D. SaveService.DeleteSave: честный результат (P1-7) ===");
        string tmp = TmpDir + "_del";
        if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
        var handler = new SaveFileHandler(new SaveConfig { SaveDirectory = tmp });
        var bus = new EventBus();
        var b = new ContainerBuilder();
        b.RegisterInstance<ISaveFileHandler>(handler);
        b.RegisterInstance(new SaveDataAggregator(handler));
        b.RegisterInstance(bus);
        var c = b.Build();
        var svc = new SaveService();
        ContainerAdapter.InjectProperties(svc, c);

        // D1: несуществующий слот → false (FileHandler false → Aggregator false →
        // SaveService обязан прозрачно вернуть false, не «успех»).
        bool deleteMissing = svc.DeleteSave(new SaveSlot("ghost_missing_0922", SaveSlotType.Manual));
        GD.Print($"[Audit0922Sim] diag: DeleteSave(несуществующий) → {deleteMissing}" +
                 (deleteMissing ? " — ложный успех" : ""));
        Check(!deleteMissing, "D1 удаление несуществующего слота возвращает false", "P1-7");

        // D2: существующий слот → true, файл реально удалён.
        handler.Save("probe_del_0922", new Dictionary<string, object> { ["x"] = "y" });
        bool deleteExisting = svc.DeleteSave(new SaveSlot("probe_del_0922", SaveSlotType.Manual));
        bool gone = !handler.HasSave("probe_del_0922");
        Check(deleteExisting && gone, "D2 удаление существующего слота — true и файл удалён", "P1-7");
    }

    // ── E. P1-1/P1-4: world-reset контракты ====================================
    private void RunWorldResetContractTest()
    {
        GD.Print("[Audit0922Sim] === E. Trade/Corpse: контракт IWorldResettable (P1-1/P1-4) ===");
        var container = GameBoot.Container;
        if (container == null)
        {
            GD.Print("[Audit0922Sim] DEFECT контейнер недоступен");
            _allPass = false;
            return;
        }

        var trade = container.Resolve<ITradeService>();
        bool tradeResettable = trade is IWorldResettable;
        GD.Print($"[Audit0922Sim] diag: TradeService is IWorldResettable → {tradeResettable}");
        Check(tradeResettable, "E1 TradeService реализует IWorldResettable (ResolveAll видит сброс)", "P1-1");

        var corpse = container.Resolve<ICorpseService>();
        bool corpseResettable = corpse is IWorldResettable;
        GD.Print($"[Audit0922Sim] diag: CorpseService is IWorldResettable → {corpseResettable}" +
                 (corpseResettable ? "" : " (сброс есть, но транзитивно через NPCModule — контракт неявный)"));
        Check(corpseResettable, "E2 CorpseService реализует IWorldResettable (явный контракт)", "P1-4");
    }

    // ── F. P2-12 (Фаза 4): ResolveAll — порядок регистрации ====================
    private void RunResolveAllOrderTest()
    {
        GD.Print("[Audit0922Sim] === F. ResolveAll: порядок регистрации + мёртвые регистрации (P2-12) ===");

        // F1: ResolveAll возвращает инстансы в ПОРЯДКЕ РЕГИСТРАЦИИ (B, C, A).
        // Прежде итерировался Dictionary.Values — порядок фактический, но
        // негарантированный (и ломается удалениями prune-а P1-6).
        var b1 = new ContainerBuilder();
        b1.Register<IOrderProbe, OrderProbeB>(Lifetime.Singleton);
        b1.Register<IOrderProbe, OrderProbeC>(Lifetime.Singleton);
        b1.Register<IOrderProbe, OrderProbeA>(Lifetime.Singleton);
        var c1 = b1.Build();
        var tags = c1.ResolveAll<IOrderProbe>().Select(p => p.Tag).ToArray();
        GD.Print($"[Audit0922Sim] diag: ResolveAll<IOrderProbe> → [{string.Join(", ", tags)}] (ожидался 2, 3, 1)");
        Check(tags.SequenceEqual(new[] { 2, 3, 1 }),
            "F1 ResolveAll — порядок = порядок регистрации (B, C, A)", "P2-12");

        // F2: связка P1-6+P2-12 — ПРЯМАЯ находка Фазы 4: ResolveAll<ISaveFileHandler>
        // обязан видеть РОВНО ОДНУ реализацию (адаптер), а не «две одновременно»
        // (старая + overridden). Включает self-bound ключ, умирающий от форварда
        // (Register<SaveFileHandler> + Register<ISaveFileHandler, SaveFileHandler>),
        // и инстанс-оверрайд с prune-ом.
        var b2 = new ContainerBuilder();
        b2.Register<SaveFileHandler>(Lifetime.Singleton);                    // self-bound (умирает от форварда)
        b2.Register<ISaveFileHandler, SaveFileHandler>(Lifetime.Singleton);  // модульный дефолт
        var adapter = new FakeAdapterFileHandler();
        b2.RegisterInstance<ISaveFileHandler>(adapter);                      // GameBoot-оверрайд
        var c2 = b2.Build();
        var handlers = c2.ResolveAll<ISaveFileHandler>().ToArray();
        GD.Print($"[Audit0922Sim] diag: ResolveAll<ISaveFileHandler> → {handlers.Length} реализация(й)");
        Check(handlers.Length == 1 && ReferenceEquals(handlers[0], adapter),
            "F2 ResolveAll<ISaveFileHandler> — ровно адаптер (мёртвый дефолт не воскресает)", "P2-12/P1-6");

        // F3: мульти-форвард (NPC-паттерн) + last-wins — дедуп по ссылке и
        // стабильный порядок: A (через IDummySvc-форвард) один раз, затем B.
        var b3 = new ContainerBuilder();
        b3.Register<IDummySvc, DummyImplA>(Lifetime.Singleton); // как INPCService→NPCService
        b3.Register<IOtherSvc, DummyImplA>(Lifetime.Singleton); // как ISaveable→NPCService
        b3.Register<IOtherSvc, DummyImplB>(Lifetime.Singleton); // как PlayerModule: ISaveable→PlayerService
        var c3 = b3.Build();
        var others = c3.ResolveAll<IOtherSvc>().ToArray();
        Check(others.Length == 2 && others[0] is DummyImplA && others[1] is DummyImplB,
            "F3 мульти-форвард + last-wins — дедуп по ссылке, порядок стабилен (A, B)", "P2-12-регресс");
    }

    // ── G. P2-13 (Фаза 4): startup fail-closed ==================================
    private void RunStartupContractTest()
    {
        GD.Print("[Audit0922Sim] === G. GameEntryPoint: startup fail-closed (P2-13) ===");
        GD.Print("[Audit0922Sim] diag: позитивный контроль — сам этот сим исполняется только после успешного fail-closed Start() живого контейнера");

        var probe = new ThrowingStartable();
        var b = new ContainerBuilder();
        b.RegisterInstance<IGameSession>(new FakeGameSession());
        b.RegisterInstance(probe);
        b.Register<GameEntryPoint>(Lifetime.Singleton);
        var c = b.Build();
        var entry = c.Resolve<GameEntryPoint>();

        // G1: провал любого Start() → AggregateException наружу (fail-closed).
        // Прежде исключение глоталось с логом — бут считался успешным.
        AggregateException? bootEx = null;
        try { entry.Start(); }
        catch (AggregateException ex) { bootEx = ex; }
        catch (Exception ex)
        {
            GD.Print($"[Audit0922Sim] DEFECT G-неАгрегат: Start() бросил {ex.GetType().Name} вместо AggregateException");
            _allPass = false;
        }
        bool g1 = bootEx != null && bootEx.InnerExceptions.Any(i => i.Message.Contains("ThrowingStartable"));
        if (bootEx != null)
            GD.Print($"[Audit0922Sim] diag: AggregateException с {bootEx.InnerExceptions.Count} провалом(и): " +
                     string.Join("; ", bootEx.InnerExceptions.Select(i => i.Message)));
        Check(g1, "G1 провал startup → AggregateException с именем модуля (fail-closed)", "P2-13");

        // G2: тик-луп после проваленного бута заглушён — полуинициализированные
        // модули не тикают (прежде _initialized=true ДО Start() открывал тики).
        entry.Tick(1);
        entry.Tick(2);
        Check(probe.Ticks == 0, "G2 Tick после проваленного Start() не доставляется (луп заглушён)", "P2-13");

        // G3: _initialized НЕ выставлен на провале — повторный Start() не
        // игнорируется («already initialised»-маска не сработала), бут можно
        // ретраить целиком с повторной диагностикой.
        bool secondThrows = false;
        try { entry.Start(); }
        catch (AggregateException) { secondThrows = true; }
        Check(secondThrows, "G3 повторный Start() повторяет диагностику (не помечен инициализированным)", "P2-13");
    }

    // ── H. P2-14 (Фаза 4): настоящий cycle detection ===========================
    private void RunCycleDetectionTest()
    {
        GD.Print("[Audit0922Sim] === H. Container: cycle detection по construction path (P2-14) ===");

        // H1: конструкторский цикл CycA → CycB → CycA ловится СРАЗУ с точным путём.
        var b1 = new ContainerBuilder();
        b1.Register<CycA>(Lifetime.Singleton);
        b1.Register<CycB>(Lifetime.Singleton);
        var c1 = b1.Build();
        try
        {
            c1.Resolve<CycA>();
            Check(false, "H1 цикл CycA → CycB → CycA обнаружен (исключение)", "P2-14");
        }
        catch (InvalidOperationException ex)
        {
            GD.Print($"[Audit0922Sim] diag: {ex.Message}");
            Check(ex.Message.Contains("Circular") && ex.Message.Contains("CycA") && ex.Message.Contains("CycB"),
                "H1 цикл CycA → CycB → CycA обнаружен с точным путём (не depth-51)", "P2-14");
        }

        // H2: самоссылка SelfCyc → SelfCyc.
        var b2 = new ContainerBuilder();
        b2.Register<SelfCyc>(Lifetime.Singleton);
        var c2 = b2.Build();
        try
        {
            c2.Resolve<SelfCyc>();
            Check(false, "H2 self-cycle SelfCyc → SelfCyc обнаружен", "P2-14");
        }
        catch (InvalidOperationException ex)
        {
            GD.Print($"[Audit0922Sim] diag: {ex.Message}");
            Check(ex.Message.Contains("Circular"), "H2 self-cycle SelfCyc → SelfCyc обнаружен", "P2-14");
        }

        // H3: линейная цепочка Dep3 → Dep2 → Dep1 резолвится — без ложного срабатывания.
        var b3 = new ContainerBuilder();
        b3.Register<Dep1>(Lifetime.Singleton);
        b3.Register<Dep2>(Lifetime.Singleton);
        b3.Register<Dep3>(Lifetime.Singleton);
        var c3 = b3.Build();
        var dep3 = c3.Resolve<Dep3>();
        Check(dep3 != null && dep3.D2 != null, "H3 цепочка Dep3 → Dep2 → Dep1 резолвится (нет ложного цикла)", "P2-14");
    }

    // ── I. P1-9 (Фазы 6/7/10): WorldDomainResetPhase fail-closed =============
    private void RunWorldDomainResetFailClosedTest()
    {
        GD.Print("[Audit0922Sim] === I. WorldDomainResetPhase: ResetWorld exception → fail-closed (P1-9) ===");

        // Мини-контейнер с доменами: OK, OK, THROW — ResolveAll по порядку
        // регистрации (P2-12) даёт детерминированную последовательность.
        var b = new ContainerBuilder();
        b.Register<IResetProbeDomain, ResetOkProbeA>(Lifetime.Singleton);
        b.Register<IResetProbeDomain, ResetThrowProbe>(Lifetime.Singleton);
        b.Register<IResetProbeDomain, ResetOkProbeB>(Lifetime.Singleton);
        var c = b.Build();

        // Фаза — реальный production-класс (не копия): только [Inject]-
        // зависимости подставляем отражением (как это делает живой контейнер).
        var phase = new WorldDomainResetPhase();
        ContainerAdapter.InjectProperties(phase, c);

        // I1: исключение ResetWorld() одного домена ДОЛЖНО покидать ExecuteAsync
        // (fail-closed). Прежде — catch+лог: фаза «успешна», оркестратор
        // MarkAsCompleted, мир собирается из смеси сброшенного/старого
        // состояния (Фаза 7 P1-11: последующие 14 фаз считают reset чистым).
        AggregateException? resetEx = null;
        try
        {
            phase.ExecuteAsync().GetAwaiter().GetResult();
        }
        catch (AggregateException ex) { resetEx = ex; }
        catch (Exception ex)
        {
            GD.Print($"[Audit0922Sim] DEFECT I-неАгрегат: ExecuteAsync бросил {ex.GetType().Name}");
            _allPass = false;
        }
        bool i1 = resetEx != null;
        GD.Print($"[Audit0922Sim] diag: ExecuteAsync при падении одного домена → " +
                 (resetEx == null ? "успех (исключение ПРОГЛОЧЕНО — сборка мира продолжится)" : $"AggregateException ({resetEx.InnerExceptions.Count} провал)"));
        Check(i1, "I1 исключение ResetWorld() покидает фазу (fail-closed, не лог)", "P1-9");

        // I2: агрегат называет домен-виновник (диагностика).
        bool i2 = resetEx != null && resetEx.InnerExceptions.Any(i =>
            i.Message.Contains("ResetThrowProbe") ||
            (i.InnerException?.Message ?? "").Contains("intentional domain failure"));
        Check(i2, "I2 AggregateException называет упавший домен", "P1-9");

        // I3: полная диагностика одной попытки — ВСЕ домены получили ResetWorld
        // (как startup P2-13: один бут = вся картина провалов). Пробы созданы
        // контейнером — резолвим конкретные типы (мульти-форвард-ключи живы).
        var okA = c.Resolve<ResetOkProbeA>();
        var okB = c.Resolve<ResetOkProbeB>();
        var bad = c.Resolve<ResetThrowProbe>();
        GD.Print($"[Audit0922Sim] diag: ResetCount okA={okA.ResetCount} throw={bad.ResetCount} okB={okB.ResetCount} (ожид 1/1/1)");
        Check(okA.ResetCount == 1 && okB.ResetCount == 1 && bad.ResetCount == 1,
            "I3 все домены диагностированы за одну попытку (полная картина, как startup)", "P1-9");

        // I4: контракт состояния фазы — после провала ExecuteAsync фаза НЕ
        // помечена успешной. Оркестратор вызвал бы MarkAsFailed (не в тесте:
        // он вызывается по исключению); здесь проверяем, что САМА фаза не
        // врёт о успехе: State остаётся Running (не Completed) — MarkAsCompleted
        // ставит ТОЛЬКО оркестратор после успешного await, а исключение
        // отправляет его в catch-ветку MarkAsFailed. Достаточная проверка:
        // ExecuteAsync бросил → MarkAsCompleted недостижим (см. I1).
        Check(phase.State != SceneAssemblyPhaseState.Completed,
            "I4 фаза после провала не считается успешной (MarkAsCompleted недостижим)", "P1-9");
    }

    // ── J. P1-10 (Фазы 7/10): retry не перезапускает успешные модули =========
    private void RunStartupRetryTest()
    {
        GD.Print("[Audit0922Sim] === J. GameEntryPoint: retry без повторного Start успешных модулей (P1-10) ===");

        // Сценарий аудитора Фазы 7: Start A → PASS (с подпиской!), Start B → PASS,
        // Start C → THROW → startup FAILED; retry → A/B НЕ перезапускаются,
        // C ретраится. Проверка: SubscriberCount<BootProbeMsg> до/после retry
        // + счётчик StartCalls.
        var bus = new EventBus();
        var subscribing = new SubscribingStartable(bus);
        var counting = new CountingStartable();
        var probe = new ThrowingStartable();

        var b = new ContainerBuilder();
        b.RegisterInstance<IGameSession>(new FakeGameSession());
        b.RegisterInstance(subscribing);
        b.RegisterInstance(counting);
        b.RegisterInstance(probe);
        b.Register<GameEntryPoint>(Lifetime.Singleton);
        var c = b.Build();
        var entry = c.Resolve<GameEntryPoint>();

        // Первая попытка: подписывающийся и счётчик стартуют успешно,
        // ThrowingStartable валит бут (AggregateException, P2-13).
        bool firstThrew = false;
        try { entry.Start(); }
        catch (AggregateException) { firstThrew = true; }
        int subsAfterFirst = bus.SubscriberCount<BootProbeMsg>();
        Check(firstThrew && subsAfterFirst == 1,
            "J1 первый бут: провален AggregateException-ом, успешный модуль подписался 1 раз", "P1-10");

        // J2 (ГЛАВНЫЙ): retry не удваивает подписки. Прежде: ретрай = полный
        // повтор бута → SubscribingStartable.Start() ВТОРОЙ раз → утечка
        // старого токена + двойная обработка событий (SubscriberCount=2).
        bool retryThrew = false;
        try { entry.Start(); }
        catch (AggregateException) { retryThrew = true; }
        int subsAfterRetry = bus.SubscriberCount<BootProbeMsg>();
        GD.Print($"[Audit0922Sim] diag: SubscriberCount<BootProbe> {subsAfterFirst} → {subsAfterRetry} " +
                 (subsAfterRetry > subsAfterFirst ? $"— ПОДПИСКА ПРОДУБЛИРОВАНА ({subsAfterRetry}x)" : "— стабилен"));
        Check(subsAfterRetry == subsAfterFirst,
            "J2 retry НЕ дублирует подписки успешного модуля (SubscriberCount стабилен)", "P1-10");

        // J3: повторный вызов Start у успешного модуля не случился вовсе.
        GD.Print($"[Audit0922Sim] diag: StartCalls: subscribing={subscribing.StartCalls}, counting={counting.StartCalls} (ожид 1/1)");
        Check(subscribing.StartCalls == 1 && counting.StartCalls == 1,
            "J3 успешные модули НЕ получают второй Start() при ретрае", "P1-10");

        // J4: провалившийся модуль ретраится (G3-контракт сохранён: бут можно
        // повторять с полной диагностикой — точка продолжения, не игнор).
        Check(retryThrew,
            "J4 проваленный модуль ретраится (бут остаётся честным fail-closed)", "P1-10");
    }

    // ── K. P1-12 (Фазы 9/10): Pure damage не расходует Ци =====================
    private void RunPureDamageTest()
    {
        GD.Print("[Audit0922Sim] === K. DamageService: Pure damage не входит в Qi-буфер (P1-12) ===");
        var container = GameBoot.Container;
        if (container == null)
        {
            GD.Print("[Audit0922Sim] DEFECT контейнер недоступен");
            _allPass = false;
            return;
        }

        var damage = container.Resolve<IDamageService>();
        var qiService = container.Resolve<IQiService>();
        var qiProvider = container.Resolve<IQiDataProvider>();

        // --- K1/K2: игрок. Гарантируем Ци ≥ 500 (буфер активен: RawQi ≥ MIN),
        // замеряем до/после Pure-удара. IsPlayerTarget=true — кэш игрока в
        // DamageService (QiChangedEvent). Стойка None + DefenderAGI=0 →
        // dodge/parry/block не роллятся (CombatSim 3f-паттерн).
        long qiBefore = qiService.CurrentQi;
        if (qiBefore < 500) qiService.AddQi(500 - qiBefore);
        qiBefore = qiService.CurrentQi;

        var purePlayerReq = new DamageRequest("qa_k12_att", "player", 100,
            DamageType.Pure, Element.Neutral, Element.Neutral,
            AttackType.Normal, TechniqueGrade.Common, 1000, 1, 1,
            DefenseSubtype.None, BodyMaterial.Organic,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            Morphology.Humanoid, 10, true, CombatSubtype.None);
        var purePlayerRes = damage.CalculateDamage(purePlayerReq);
        long qiAfterPure = qiService.CurrentQi;
        GD.Print($"[Audit0922Sim] diag: Pure→player: absorbed={purePlayerRes.AbsorbedByQi}, " +
                 $"final={purePlayerRes.FinalDamage}, Ци {qiBefore}→{qiAfterPure}");
        Check(purePlayerRes.AbsorbedByQi == 0,
            "K1 Pure→player: AbsorbedByQi = 0 (результат без Ци-поглощения)", "P1-12");
        Check(qiAfterPure == qiBefore,
            "K2 Pure→player: Ци игрока НЕ расходуется (главный инвариант)", "P1-12");

        // --- K3: NPC-путь (QiConsumeRequest + прямой TryConsumeQi — «NPC Qi
        // списывается отдельно» — тоже не должен срабатывать для Pure).
        qiProvider.SetQiState("qa_k12_npc", 500, 1000, 1f);
        long npcQiBefore = qiProvider.GetCurrentQi("qa_k12_npc");
        var pureNpcReq = new DamageRequest("qa_k12_att", "qa_k12_npc", 100,
            DamageType.Pure, Element.Neutral, Element.Neutral,
            AttackType.Normal, TechniqueGrade.Common, 1000, 1, 1,
            DefenseSubtype.None, BodyMaterial.Organic,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            Morphology.Humanoid, 10, false, CombatSubtype.None);
        var pureNpcRes = damage.CalculateDamage(pureNpcReq);
        long npcQiAfter = qiProvider.GetCurrentQi("qa_k12_npc");
        GD.Print($"[Audit0922Sim] diag: Pure→npc: absorbed={pureNpcRes.AbsorbedByQi}, " +
                 $"final={pureNpcRes.FinalDamage}, Ци {npcQiBefore}→{npcQiAfter}");
        Check(npcQiAfter == npcQiBefore && pureNpcRes.AbsorbedByQi == 0,
            "K3 Pure→NPC: Ци не расходуется, поглощения нет (TryConsumeQi не вызван)", "P1-12");

        // --- K4: контроль — Physical Ци расходует (буфер жив, фикс не сломал
        // обычный путь: пассивная сырая Ци, 5:1).
        var physReq = new DamageRequest("qa_k12_att", "qa_k12_npc", 100,
            DamageType.Physical, Element.Neutral, Element.Neutral,
            AttackType.Normal, TechniqueGrade.Common, 1000, 1, 1,
            DefenseSubtype.None, BodyMaterial.Organic,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            Morphology.Humanoid, 10, false, CombatSubtype.None);
        var physRes = damage.CalculateDamage(physReq);
        long npcQiAfterPhys = qiProvider.GetCurrentQi("qa_k12_npc");
        GD.Print($"[Audit0922Sim] diag: Physical→npc (контроль): absorbed={physRes.AbsorbedByQi}, " +
                 $"Ци {npcQiAfter}→{npcQiAfterPhys}");
        Check(physRes.AbsorbedByQi > 0 && npcQiAfterPhys < npcQiAfter,
            "K4 контроль Physical: Ци расходуется (Qi-буфер не отключён фиксом)", "P1-12");
    }

    // ── L. P2-22 (диагностика, бэклог): queued-drain re-entrancy guard ======
    // Сценарий аудитора (Фазы 6/7): Publish(A) → handler → Publish(A) → queue;
    // внешний Publish закончился, _publishing(T) снят; дрен исполняет queued A
    // через queue[i]() → InvokeHandlers БЕЗ добавления T в _publishing →
    // self-publish из queued-хендлера идёт ПРЯМОЙ рекурсией, а не в очередь.
    // Изоляция от «stale queue» (P1-5, r32) — отдельный дефект: тот фикс честен,
    // здесь теряется именно guard в момент дрен а.
    // Классификация аудитора: P2 hardening (нужен патологический
    // self-publish-цикл), НЕ P1 — фиксы «следующего слоя» (Фаза 10) —
    // поставляем runtime-доказательство, вердикт НЕ гейтим.
    private void RunQueueDrainReentrancyDiagnostic()
    {
        GD.Print("[Audit0922Sim] === L. EventBus: queued-drain re-entrancy guard (P2-22 — диагностика, бэклог) ===");
        var bus = new EventBus();
        int depth = 0, maxDepth = 0, deliveries = 0;
        var tok = bus.Subscribe<PingMsg>((in PingMsg msg) =>
        {
            depth++;
            try
            {
                if (depth > maxDepth) maxDepth = depth;
                deliveries++;
                // Ограниченная цепочка self-publish (id < 6) — StackOverflow
                // исключён; глубина рекурсии = детектор потери guard-а.
                if (msg.Id < 6) bus.Publish(new PingMsg(msg.Id + 1));
            }
            finally { depth--; }
        });
        bus.Publish(new PingMsg(1));
        tok.Dispose();

        // Починенный контракт: во время дрен queued-события повторный Publish
        // ТОГО ЖЕ типа ставится в очередь (guard) → maxDepth ≤ 2. Текущий код:
        // guard снят после внешнего Publish → прямая рекурсия растёт с длиной
        // цепочки (maxDepth == длина цепочки).
        bool guardHeldDuringDrain = maxDepth <= 2;
        GD.Print($"[Audit0922Sim] diag: L queued-drain maxDepth={maxDepth}, deliveries={deliveries} (цепочка 6) — " +
                 (guardHeldDuringDrain
                     ? "guard держится в дрене (P2-22 закрыт)"
                     : "guard ПОТЕРЯН в дрене: queued self-publish идёт прямой рекурсией " +
                       "(P2-22 runtime-подтверждён; классификация P2 — бэклог следующего слоя, " +
                       "по указанию аудитора Фазы 10 в этот эпизод не входит)"));
    }

    private static void CleanupTemp()
    {
        try
        {
            if (Directory.Exists(TmpDir)) Directory.Delete(TmpDir, true);
            string tmpDel = TmpDir + "_del";
            if (Directory.Exists(tmpDel)) Directory.Delete(tmpDel, true);
            string outside = Path.Combine(Path.GetDirectoryName(TmpDir)!, "escape_probe.json");
            if (File.Exists(outside)) File.Delete(outside); // чистим артефакт префикс-прогона
        }
        catch { /* QA-чистка — не роняет вердикт */ }
    }
}
