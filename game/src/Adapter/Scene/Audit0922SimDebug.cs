#nullable enable
// Создано: 2026-09-22 — внешний аудит upload/audit_09_22_07_30.txt: runtime-
// верификация P1-5 (EventBus: exception внутри subscriber + re-entrancy) и
// P1-8 (path traversal в Modules-layer SaveFileHandler) — прямой запрос
// аудитора («проверить реальным runtime-тестом, результат выгрузить в
// checkpoints/»), плюс смежные инфраструктурные контракты P1-1/P1-4/P1-6/P1-7.
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

        GD.Print($"[Audit0922Sim] VERDICT: {(_allPass
            ? "PASS — EventBus isolation/re-entrancy, path sanitisation, DI override prune, DeleteSave honesty, Trade/Corpse reset-контракты"
            : "FAIL — см. DEFECT-строки выше (runtime-подтверждение аудита 09.22)")}");

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
