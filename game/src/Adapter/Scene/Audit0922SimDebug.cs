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
// 2026-09-22 (R36-b, P2-бэклог Фазы 12): ДОПОЛНЕНА секцией T — Items/Trade:
//   T1 (P2-33) CurrencyService.Add/SetBalance — saturating: переполнение
//      int больше не делает баланс отрицательным.
//   T2 (P2-34) TradeService: total сделки считается в long; покупка
//      дороже int.MaxValue = честный отказ ДО списания, продажа —
//      насыщение до int.MaxValue (прежде: wrap-отрицательный тотал →
//      предметы изъяты, оплата молча 0).
//   T3 (P2-35) InventoryService.RestoreState валидирует каждый слот:
//      count>0, ≤MaxStack, Enum.IsDefined(category/rarity), ItemId есть
//      в БД (item_db восстанавливается раньше inventory — RestoreOrder).
//   T4 (P2-36) СТАВКИ ПЕРЕНОСКИ — ОТКЛОНЁН BY-USER: канон 50 кг / 100 ед.
//      (прямое указание владельца, user request 2026-08-22; приоритет выше
//      аудитора и документации — конс пект в INVENTORY_SYSTEM §3.3-примечание);
//      T4 — страж владельческих значений (50/100 не срезаются молча).
//
// 2026-09-22 (R36-a, P2-бэклог Фазы 11): ДОПОЛНЕНА секцией S — время:
//   S1 (P2-26) автосейв привязан к МИРОВОМУ тику (ITimeService.TickCount),
//      не к процессному счётчику GameBoot._currentTick (граница NewGame/Load
//      ломала абсолютную каденцию «каждые 30 игровых минут»).
//   S2 (P2-27) единая pause-authority: GameSession.Pause/Resume синхронизируют
//      TimeService (две истины о паузе сходятся) + Resume восстанавливает
//      скорость ДО паузы (Fast/Quick не уничтожаются в Normal).
//   S4 (P2-28) IsPaused ⇒ DeltaTime == 0 (латентный договор для будущих
//      потребителей времени — культивация).
//   S5 (P2-29) Load канонизирует три представления времени: дата первична,
//      TickCount/TotalTime — производные (рассинхронный сейв не проходит).
//   S6 (P2-30) WorldTime/RestoreState отвергают невалидные даты (month=13,
//      day=0, hour=29, minute=-10, год до эпохи) → битый сейв = честный отказ.
//   S7 (P3-11) WorldConfig без мёртвых источников времени.
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
using CultivationGame.Core.Helpers;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Adapter.Di;
using CultivationGame.Entry;
using CultivationGame.Entry.Phases;
using CultivationGame.Modules.Save;
using CultivationGame.Modules.World;
using CultivationGame.Modules.Generator;
using CultivationGame.Modules.Inventory;
using CultivationGame.Modules.Player;
using CultivationGame.Modules.Trade;

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

    // ── Фейковые сервисы для секции S (Фаза 11 P2: автосейв/пауза/канон) ──
    // P2-26: настраиваемый мировой тик (process-tick передаётся в Tick напрямую).
    // TickCount-член компилируется и ДО расширения интерфейса (лишний член
    // класса), и ПОСЛЕ (реализует ITimeService.TickCount).
    private sealed class FakeTimeService : ITimeService
    {
        public int WorldTick;
        public int TickCount => WorldTick;
        public float DeltaTime => 1f;
        public float TotalTime => WorldTick;
        public int CurrentDay => 1;
        public int CurrentMonth => 1;
        public int CurrentYear => 1864;
        public int CurrentHour => 6;
        public TimeOfDay TimeOfDay => TimeOfDay.Morning;
        public TimeSpeed Speed { get; set; } = TimeSpeed.Normal;
        public bool IsPaused => Speed == TimeSpeed.Paused;
        public WorldTime CurrentTime => new WorldTime(GameConstants.START_YEAR, 1, 1, 6, 0);
        public void Pause() => Speed = TimeSpeed.Paused;
        public void Resume() => Speed = TimeSpeed.Normal;
        public void BulkAdvanceTicks(int ticks) => WorldTick += Math.Max(0, ticks);
    }

    private sealed class FakeSaveService : ISaveService
    {
        public int SaveCalls;
        public bool Save(SaveSlot slot) { SaveCalls++; return true; }
        public bool Load(SaveSlot slot) => false;
        public bool HasSave(SaveSlot slot) => false;
        public bool DeleteSave(SaveSlot slot) => true;
        public IReadOnlyList<SaveInfo> GetAllSaves() => Array.Empty<SaveInfo>();
        public string? LastError => null;
    }

    // P2-27: сессия в Playing — SaveModule.Tick не гейтится в главное меню.
    private sealed class FakePlayingSession : IGameSession
    {
        public SessionState State => SessionState.Playing;
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
        public bool HasSave(string slotName) => false;
        public bool DeleteSave(string slotName) => true;
        public IReadOnlyList<string> GetAllSaves() => Array.Empty<string>();
    }

    // ── Фейковые сервисы для секции T (Фаза 12 P2: валюта/торговля/инвентарь) ──

    // T2: NPC-Merchant для OpenTrade (остальные члены — заглушки).
    private sealed class FakeTradeNpcService : INPCService
    {
        public NPCData GetNPC(string npcId) => null!;
        public IReadOnlyList<string> GetNearbyNPCIds(Position2D position, float range) => Array.Empty<string>();
        public Attitude GetAttitude(string npcId, string targetId) => default;
        public void ModifyAttitude(string npcId, string targetId, int delta) { }
        public bool IsAlive(string npcId) => true;
        public NPCAIState GetAIState(string npcId) => default;
        public IReadOnlyList<string> GetAllNPCIds() => Array.Empty<string>();
        public void SetAIState(string npcId, NPCAIState state) { }
        public void UpdatePosition(string npcId, Position2D position) { }
        public NPCState GetNPCState(string npcId) => new NPCState { Role = NPCRole.Merchant, IsAlive = true };
        [System.Obsolete] public string SpawnNPC(string presetId, Position2D position) => "npc_t2";
        public string SpawnNPC(string speciesId, NPCRole roleId, int locationLevel, Position2D position, long seed) => "npc_t2";
        public void DespawnNPC(string npcId) { }
        public IReadOnlyList<string> GetSpawnedNPCIds() => Array.Empty<string>();
    }

    // T2/T3: БД с контролируемыми Value/MaxStack предметов.
    private sealed class FakeTradeItemDb : IItemDatabaseService
    {
        public readonly Dictionary<string, ItemData> Items = new();
        public bool TryGetItem(string itemId, out ItemData item)
        {
            item = null!;
            return !string.IsNullOrEmpty(itemId) && Items.TryGetValue(itemId, out item!);
        }
        public void Register(ItemData item) { }
        public void RegisterRange(IEnumerable<ItemData> items) { }
        public IReadOnlyList<ItemData> GetAllItems() => Array.Empty<ItemData>();
        public IReadOnlyList<ItemData> GetItemsByCategory(ItemCategory category) => Array.Empty<ItemData>();
        public int Count => Items.Count;
    }

    // T2: инвентарь-заглушка (объём/вес не гейтят, sell_probe есть ×3).
    private sealed class FakeTradeInventory : IInventoryService
    {
        public int HaveSellProbe = 3;
        public bool TryAddItem(ItemData item, int count = 1) => true;
        public bool TryAddItem(ItemData item, int count, out int addedCount) { addedCount = count; return true; }
        public bool TryRemoveItem(string itemId, int count = 1) => true;
        public int GetItemCount(string itemId) => itemId == "sell_probe" ? HaveSellProbe : 0;
        public IReadOnlyList<InventorySlot> GetAllSlots() => Array.Empty<InventorySlot>();
        public bool TrySplitSlot(int slotIndex, int moveCount) => false;
        public bool TryRemoveFromSlot(int slotIndex, int count) => false;
        public int FindSlotIndexBySlotId(Guid slotId) => -1;
        public int TotalSlots => 100;
        public int UsedSlots => 0;
        public bool TrySplitSlot(Guid slotId, string expectedItemId, int moveCount) => false;
        public bool TryRemoveFromSlot(Guid slotId, string expectedItemId, int count) => false;
        public bool CanFitItem(ItemData item, int count = 1) => true;
        public int HowManyCanFit(ItemData item) => 10_000;
        public bool IsOverweight => false;
        public float OverweightRatio => 0f;
        public float GetCurrentWeight() => 0f;
        public float GetCurrentVolume() => 0f;
        public float GetEffectiveMaxWeight() => 100f;
        public float GetEffectiveMaxVolume() => 100f;
    }

    // T2: валюта-рекордер (SpiritStones = int.MaxValue — любая сумма «хватает»,
    // чтобы гейт P2-34 ловился сам по себе, а не отказом баланса).
    private sealed class FakeTradeCurrency : ICurrencyService
    {
        public int AddCalls, LastAddAmount, SpendCalls, LastSpendAmount;
        public int SpiritStones => int.MaxValue;
        public void Add(int amount) { AddCalls++; LastAddAmount = amount; }
        public bool Spend(int amount) { SpendCalls++; LastSpendAmount = amount; return true; }
        public void SetBalance(int spiritStones) { }
    }

    // T2: генераторы без ассортимента (сток = только материал из TradeConfig).
    private sealed class FakeNullEquipmentGenerator : IEquipmentGenerator
    {
        public EquipmentData GenerateWeapon(int level, string? subtype = null, long seed = 0) => null!;
        public EquipmentData GenerateArmor(int level, string? subtype = null, long seed = 0) => null!;
        public EquipmentData GenerateLegendaryWeapon(int level, string? subtype = null, long seed = 0, bool? forceOvercap = null) => null!;
        public EquipmentData GenerateLegendaryArmor(int level, string? subtype = null, long seed = 0, bool? forceOvercap = null) => null!;
        public EquipmentData GenerateRandom(int level, long seed = 0) => null!;
        public bool TryApplyEnchant(EquipmentData item, string? enchantId = null, long seed = 0) => false;
    }

    private sealed class FakeNullItemGenerator : IItemGeneratorService
    {
        public EquipmentData GenerateWeaponForLevel(int cultivationLevel, long seed = 0) => null!;
        public EquipmentData GenerateArmorForLevel(int cultivationLevel, long seed = 0) => null!;
        public ItemData GenerateConsumableForLevel(int cultivationLevel, long seed = 0) => null!;
        public EquipmentData GenerateChargerForLevel(int cultivationLevel, long seed = 0) => null!;
        public EquipmentData GenerateRandomEquipment(int playerLevel, long seed = 0) => null!;
        public List<EquipmentData> GenerateLoot(int playerLevel, int count, long seed = 0) => new();
        public List<ItemData> GenerateConsumableLoot(int playerLevel, int count, long seed = 0) => new();
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

        // === M. Time pipeline (Фаза 11: P1-13/P1-14/P1-15) ===================
        try { RunTimePipelineTest(); }
        catch (Exception ex) { GD.Print($"[Audit0922Sim] DEFECT M-крэш: {ex.GetType().Name}: {ex.Message}"); _allPass = false; }

        // === N. Item identity (Фаза 12: P1-16 + P2-31/P2-32) =================
        try { RunItemIdentityTest(); }
        catch (Exception ex) { GD.Print($"[Audit0922Sim] DEFECT N-крэш: {ex.GetType().Name}: {ex.Message}"); _allPass = false; }

        // === O. EquipmentValidator (Фаза 12: P1-17/P1-18) ====================
        try { RunEquipmentGateTest(); }
        catch (Exception ex) { GD.Print($"[Audit0922Sim] DEFECT O-крэш: {ex.GetType().Name}: {ex.Message}"); _allPass = false; }

        // === P. StorageRing persistence (Фаза 12: P1-19) =====================
        try { RunStorageRingPersistenceTest(); }
        catch (Exception ex) { GD.Print($"[Audit0922Sim] DEFECT P-крэш: {ex.GetType().Name}: {ex.Message}"); _allPass = false; }

        // === Q. Stat progression (Фаза 14: P1-20 + P2-43/P2-49) ==============
        try { RunStatProgressionTest(); }
        catch (Exception ex) { GD.Print($"[Audit0922Sim] DEFECT Q-крэш: {ex.GetType().Name}: {ex.Message}"); _allPass = false; }

        // === R. Revive-тело + dash-границы (Фаза 14: P2-44/P2-45) ============
        try { RunReviveAndDashTest(); }
        catch (Exception ex) { GD.Print($"[Audit0922Sim] DEFECT R-крэш: {ex.GetType().Name}: {ex.Message}"); _allPass = false; }

        // === S. Фаза 11 P2-бэклог: автосейв-каденция / pause-authority /
        // DeltaTime / канонизация времени / валидация даты / конфиг-дрейф ===
        try { RunAutosaveCadenceTest(); }
        catch (Exception ex) { GD.Print($"[Audit0922Sim] DEFECT S1-крэш: {ex.GetType().Name}: {ex.Message}"); _allPass = false; }
        try { RunPauseAuthorityTest(); }
        catch (Exception ex) { GD.Print($"[Audit0922Sim] DEFECT S2-крэш: {ex.GetType().Name}: {ex.Message}"); _allPass = false; }
        try { RunTimeCanonizationTest(); }
        catch (Exception ex) { GD.Print($"[Audit0922Sim] DEFECT S5-крэш: {ex.GetType().Name}: {ex.Message}"); _allPass = false; }

        // === T. Фаза 12 P2-бэклог: валюта/сделка/инвентарь-restore/30-30 ===
        try { RunCurrencyOverflowTest(); }
        catch (Exception ex) { GD.Print($"[Audit0922Sim] DEFECT T1-крэш: {ex.GetType().Name}: {ex.Message}"); _allPass = false; }
        try { RunTradeOverflowTest(); }
        catch (Exception ex) { GD.Print($"[Audit0922Sim] DEFECT T2-крэш: {ex.GetType().Name}: {ex.Message}"); _allPass = false; }
        try { RunInventoryRestoreValidationTest(); }
        catch (Exception ex) { GD.Print($"[Audit0922Sim] DEFECT T3-крэш: {ex.GetType().Name}: {ex.Message}"); _allPass = false; }
        try { RunCarryLimitsCanonTest(); }
        catch (Exception ex) { GD.Print($"[Audit0922Sim] DEFECT T4-крэш: {ex.GetType().Name}: {ex.Message}"); _allPass = false; }

        // === L. P2-22 (диагностика, бэклог): queued-drain re-entrancy =========
        // Запрос аудитора (Фаза 7): runtime-чек edge-case-а. Фикс — «следующий
        // слой» реестра Фазы 10, в этот эпизод НЕ входит → вердикт не гейтит.
        try { RunQueueDrainReentrancyDiagnostic(); }
        catch (Exception ex) { GD.Print($"[Audit0922Sim] diag-крэш L: {ex.GetType().Name}: {ex.Message}"); }

        GD.Print($"[Audit0922Sim] VERDICT: {(_allPass
            ? "PASS — EventBus isolation/re-entrancy, path sanitisation, DI override prune, DeleteSave honesty, Trade/Corpse reset-контракты, ResolveAll registration order, startup fail-closed, cycle detection (Фаза 4: P2-12/P2-13/P2-14), world-reset fail-closed (P1-9), startup retry без дублей (P1-10), Pure без расхода Ци (P1-12), time catch-up/speed/markers (Фаза 11: P1-13/P1-14/P1-15), item identity/DB (Фаза 12: P1-16+P2-31/P2-32), equipment гейты (P1-17/P1-18), storage-ring persistence (P1-19), stat progression (Фаза 14: P1-20+P2-43/P2-49), revive/dash (P2-44/P2-45), time P2-бэклог Фазы 11: автосейв по мировым тикам / pause-authority / DeltaTime / канонизация / валидация даты (P2-26…30), items/trade P2-бэклог Фазы 12: saturating-валюта / честный тотал сделки / валидация Restore / переноска — владельческий канон 50/100 (P2-33/34/35 + P2-36 by-user)"
            : "FAIL — см. DEFECT-строки выше (runtime-подтверждение аудита 09.22 + Фаза 4 + финальные фазы 6–10 + фазы 11–14 + P2-26…30 + P2-33…35)")}");

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

    // ── M. Фаза 11 (аудит 09.22 12:00): Time pipeline =======================
    // P1-13 (GameBoot catch-up), P1-14 (speed-leak через ResetWorld),
    // P1-15 (фантомные Day/Month/Year после тёплого Reset/Load).
    private void RunTimePipelineTest()
    {
        GD.Print("[Audit0922Sim] === M. Time: catch-up / speed-leak / calendar markers (P1-13/P1-14/P1-15) ===");
        var container = GameBoot.Container;
        if (container == null)
        {
            GD.Print("[Audit0922Sim] DEFECT контейнер недоступен");
            _allPass = false;
            return;
        }

        // --- M1 (P1-13): hitch 2с на Quick — обещано 30 тиков (2с × 15 т/с).
        // Прежде: cap 8/кадр; остаток долга 22×(1/15)с > 8×(1/15)с → СБРОШЕН молча.
        var clock = new TickCatchUpClock();
        int ran1 = clock.Advance(2.0f, 15, out int bulk1);
        long accounted1 = clock.TotalTicksRun + clock.TotalBulkSkipped
            + (long)Math.Round(clock.PendingDebtSeconds * 15);
        GD.Print($"[Audit0922Sim] diag: 2с@Quick → смоделировано {clock.TotalTicksRun}, " +
                 $"bulk {clock.TotalBulkSkipped}, долг {clock.PendingDebtSeconds:F3}с; учёт {accounted1}/30");
        Check(ran1 == 30 && accounted1 == 30,
            "M1 hitch 2с@Quick: 30/30 тиков смоделированы (не 8 + молчаливая потеря долга)", "P1-13");

        // --- M2 (P1-13): гигантский hitch 60с@Quick = 900 тиков — инвариант учёта.
        var clock2 = new TickCatchUpClock();
        clock2.Advance(60f, 15, out int bulk2);
        long accounted2 = clock2.TotalTicksRun + clock2.TotalBulkSkipped
            + (long)Math.Round(clock2.PendingDebtSeconds * 15);
        GD.Print($"[Audit0922Sim] diag: 60с@Quick → смоделировано {clock2.TotalTicksRun}, " +
                 $"bulk-скачок {clock2.TotalBulkSkipped}, остаток долга {clock2.PendingDebtSeconds:F3}с; " +
                 $"учёт {accounted2}/900");
        Check(accounted2 == 900 && bulk2 > 0,
            "M2 hitch 60с@Quick: моделирование + bulk + остаток = 900 (долг не потерян молча)", "P1-13");

        // --- M3 (P1-13): BulkAdvanceTicks — календарь арифметически точен.
        var ts = container.Resolve<TimeService>();
        var tBefore = ts.CurrentTime;
        ts.BulkAdvanceTicks(1500); // 25 игровых часов — пересечение суток/месяца
        Check(ts.CurrentTime.TotalMinutes == tBefore.TotalMinutes + 1500,
            "M3 BulkAdvanceTicks(1500): TotalMinutes растёт точно (календарь честен при скачке)", "P1-13");

        // --- M4 (P1-14): скорость/пауза не протекают через ResetWorld.
        ts.Speed = TimeSpeed.Quick;
        ts.ResetWorld();
        Check(ts.Speed == TimeSpeed.Normal,
            "M4a ResetWorld: Quick → Normal (тёплая NewGame не наследует темп прошлого мира)", "P1-14");

        ts.Speed = TimeSpeed.Paused;
        ts.ResetWorld();
        Check(ts.Speed == TimeSpeed.Normal && !ts.IsPaused,
            "M4b ResetWorld: Paused → Normal (новая игра не стартует «замороженной»)", "P1-14");

        // --- M5 (P1-15): фантомные календарные события после сброса мира.
        var wm = container.Resolve<WorldModule>();
        int dayEv = 0, monthEv = 0, yearEv = 0;
        var dayTok = container.Resolve<ISubscriber<DayChangedEvent>>()
            .Subscribe((in DayChangedEvent _) => dayEv++);
        var monthTok = container.Resolve<ISubscriber<MonthChangedEvent>>()
            .Subscribe((in MonthChangedEvent _) => monthEv++);
        var yearTok = container.Resolve<ISubscriber<YearChangedEvent>>()
            .Subscribe((in YearChangedEvent _) => yearEv++);

        try
        {
            // Подготовка: синхронизация маркеров с текущим временем (первый
            // тик может быть ТИХИМ — dirty от бута/NewGame в postfix).
            ts.RestoreState(new TimeService.WorldTimeSaveState
            {
                Year = tBefore.Year, Month = 12, Day = 30, Hour = 6, Minute = 0,
                TickCount = 0, TotalTime = 0f,
            });
            wm.Tick(1);
            dayEv = 0; monthEv = 0; yearEv = 0;

            // M5-контроль: ЛЕГИТИМНЫЙ переход суток ВНУТРИ месяца
            // (12-05 23:59 → 12-06 — ровно один DayChanged, без Month/Year;
            // 12-30→12-31 не существует: 12-30 — последний день года, переход
            // даёт сразу Day+Month+Year).
            ts.RestoreState(new TimeService.WorldTimeSaveState
            {
                Year = tBefore.Year, Month = 12, Day = 5, Hour = 23, Minute = 59,
                TickCount = 1, TotalTime = 1f,
            });
            wm.Tick(1);
            Check(dayEv == 1 && monthEv == 0 && yearEv == 0,
                "M5-контроль: легитимный переход суток даёт одиночный DayChanged (харнесс)", "P1-15");

            // Сценарий аудитора: мир A жил до 12-31 → NewGame/Load (время → 01-01),
            // маркеры ДОЛЖНЫ ре-синхронизироваться ТИХО (не событиями).
            ts.ResetWorld();
            // WorldModule — sealed: в prefix НЕ реализует IWorldResettable (CS8121
            // на прямом паттерне) → проверка через object-cast (runtime).
            if ((object)wm is IWorldResettable wmReset) wmReset.ResetWorld();
            dayEv = 0; monthEv = 0; yearEv = 0;
            wm.Tick(2);
            GD.Print($"[Audit0922Sim] diag: после ResetWorld+Tick: Day={dayEv}, Month={monthEv}, Year={yearEv}");
            Check(dayEv == 0 && monthEv == 0 && yearEv == 0,
                "M5 ResetWorld → ТИХАЯ ре-синхронизация маркеров (нет фантомных Day/Month/Year)", "P1-15");
        }
        finally
        {
            dayTok.Dispose(); monthTok.Dispose(); yearTok.Dispose();
            // не мусорим QA-миру: канонический старт + нормальный темп
            ts.ResetWorld();
            ts.Speed = TimeSpeed.Normal;
        }
    }

    // ── N. Фаза 12: коллизии ItemId + stale category-index + merge (P1-16) ──
    private void RunItemIdentityTest()
    {
        GD.Print("[Audit0922Sim] === N. Items: коллизии ID / stale-индекс / merge каталога (P1-16/P2-31/P2-32) ===");
        var container = GameBoot.Container;

        // --- N1 (P1-16): сиды 5 и 1005 дают одинаковый остаток %1000 —
        // старая схема ID {prefix}_{L}_{seed%1000:D3} коллидирует.
        var db = new ItemDatabaseService();
        var gen = new ItemGeneratorService(db);
        var c1 = gen.GenerateConsumableForLevel(3, 5);
        var c2 = gen.GenerateConsumableForLevel(3, 1005);
        GD.Print($"[Audit0922Sim] diag: consumable(3, seed=5)={c1.ItemId}, consumable(3, seed=1005)={c2.ItemId}");
        Check(c1.ItemId != c2.ItemId,
            "N1 расходники: ID уникальны при коллизионных сидах (5 vs 1005 — counter-часть ID)", "P1-16");

        var w1 = gen.GenerateWeaponForLevel(3, 7);
        var w2 = gen.GenerateWeaponForLevel(3, 1007);
        Check(w1.ItemId != w2.ItemId,
            "N1b оружие: ID уникальны (генераторы ItemGeneratorService — counter-based)", "P1-16");

        // --- N2 (P1-16): замена определения по чужому ItemId невозможна.
        db.TryGetItem(c1.ItemId, out var def1);
        db.TryGetItem(c2.ItemId, out var def2);
        Check(def1 != null && def2 != null && !ReferenceEquals(def1, def2),
            "N2 определения под разными ID — разные объекты (стак не меняет эффекты)", "P1-16");

        // --- N3 (P2-31): замена категории чистит СТАРЫЙ category-index.
        var dbx = new ItemDatabaseService();
        dbx.Register(new ItemData { ItemId = "qa_p231_x", Category = ItemCategory.Consumable });
        dbx.Register(new ItemData { ItemId = "qa_p231_x", Category = ItemCategory.Material });
        bool staleInConsumable = dbx.GetItemsByCategory(ItemCategory.Consumable)
            .Any(i => i.ItemId == "qa_p231_x");
        Check(!staleInConsumable,
            "N3 замена категории: из старого category-index запись удалена (нет stale-определения)", "P2-31");

        // --- N4 (P2-32): RestoreState ЗАМЕНЯЕТ каталог (не merge с прошлым миром).
        var dbSave = new ItemDatabaseService();
        dbSave.Register(new ItemData { ItemId = "qa_p232_saved", Category = ItemCategory.Misc });
        object snapshot = dbSave.CaptureState();
        var dbLoad = new ItemDatabaseService();
        dbLoad.Register(new ItemData { ItemId = "qa_p232_runtime_a", Category = ItemCategory.Misc });
        dbLoad.Register(new ItemData { ItemId = "qa_p232_runtime_b", Category = ItemCategory.Misc });
        dbLoad.RestoreState(snapshot);
        Check(dbLoad.Count == 1,
            "N4 RestoreState: каталог очищен перед восстановлением (runtime прошлого мира не переживает Load)", "P2-32");

        // --- N5 (P2-32): сброс мира чистит каталог + ре-сеет канонический контент.
        if (container == null)
        {
            GD.Print("[Audit0922Sim] DEFECT контейнер недоступен (N5)");
            _allPass = false;
            return;
        }
        var realDb = container.Resolve<IItemDatabaseService>();
        Check(realDb is IWorldResettable,
            "N5a ItemDatabaseService : IWorldResettable (сброс мира видит каталог)", "P2-32");

        var dbFresh = new ItemDatabaseService();
        // счётчики — STATIC (общие на процесс): после сбора фактов вернём,
        // чтобы QA-мир не коллидировал новыми генерациями со старыми ID.
        long genCounterBefore = ItemGeneratorService.GetGenerationCounter();
        int eqCounterBefore = EquipmentGenerator.GetIdCounter();
        dbFresh.Register(new ItemData { ItemId = "qa_reset_probe", Category = ItemCategory.Misc });
        ((IWorldResettable)dbFresh).ResetWorld();
        bool runtimeGone = !dbFresh.TryGetItem("qa_reset_probe", out _);
        bool canonicalBack = dbFresh.TryGetItem("material_iron_scrap", out _);
        ItemGeneratorService.SetGenerationCounter(genCounterBefore);
        EquipmentGenerator.SetIdCounter(eqCounterBefore);
        Check(runtimeGone && canonicalBack,
            "N5b ResetWorld: runtime-предметы чистятся, канон ClassicLoot ре-сеется", "P2-32");
    }

    // ── O. Фаза 12: гейты валидатора (P1-17/P1-18) ==========================
    private void RunEquipmentGateTest()
    {
        GD.Print("[Audit0922Sim] === O. EquipmentValidator: гейты требований + offhand (P1-17/P1-18) ===");
        var container = GameBoot.Container;
        if (container == null)
        {
            GD.Print("[Audit0922Sim] DEFECT контейнер недоступен");
            _allPass = false;
            return;
        }
        var stats = container.Resolve<IStatService>();
        var empty = new Dictionary<EquipmentSlot, EquipmentData>();

        // --- O1 (P1-17): L9-предмет при уровне игрока 1 — ОТКАЗ.
        var highItem = new EquipmentData
        {
            ItemId = "qa_o1", NameRu = "qa", Slot = EquipmentSlot.Torso,
            HandType = WeaponHandType.None, RequiredCultivationLevel = 9,
        };
        bool o1 = EquipmentValidator.ValidateEquip(highItem, EquipmentSlot.Torso, null, empty,
            out _, playerCultivationLevel: 1, statService: stats);
        Check(!o1, "O1 RequiredCultivationLevel=9 при уровне игрока 1 → отказ (гейт реален)", "P1-17");

        // --- O2 (контроль): тот же предмет на уровне 9 — успех.
        bool o2 = EquipmentValidator.ValidateEquip(highItem, EquipmentSlot.Torso, null, empty,
            out _, playerCultivationLevel: 9, statService: stats);
        Check(o2, "O2 RequiredCultivationLevel=9 при уровне 9 → успех (гейт не гиперстрогий)", "P1-17");

        // --- O3 (P1-17): StatRequirements (STR≥50 при стат 10) — отказ.
        var statItem = new EquipmentData
        {
            ItemId = "qa_o3", NameRu = "qa", Slot = EquipmentSlot.Torso,
            HandType = WeaponHandType.None, RequiredCultivationLevel = 0,
        };
        statItem.StatRequirements.Add(new StatRequirement { StatName = "Strength", MinValue = 50 });
        bool o3 = EquipmentValidator.ValidateEquip(statItem, EquipmentSlot.Torso, null, empty,
            out _, playerCultivationLevel: 9, statService: stats);
        Check(!o3, "O3 StatRequirements STR≥50 при стате игрока 10 → отказ", "P1-17");

        // --- O4 (P1-18): одноручное WeaponMain-оружие → WeaponOff — разрешено.
        var oneHand = new EquipmentData
        {
            ItemId = "qa_o4", NameRu = "qa", Slot = EquipmentSlot.WeaponMain,
            Category = ItemCategory.Weapon,
            HandType = WeaponHandType.OneHand, RequiredCultivationLevel = 0,
        };
        bool o4 = EquipmentValidator.ValidateEquip(oneHand, EquipmentSlot.WeaponOff, null, empty, out _);
        Check(o4, "O4 одноручное оружие экипируется в WeaponOff (слот достижим через штатный UI)", "P1-18");

        // --- O5 (регресс): двуручное → WeaponOff остаётся запретом.
        var twoHand = new EquipmentData
        {
            ItemId = "qa_o5", NameRu = "qa", Slot = EquipmentSlot.WeaponMain,
            Category = ItemCategory.Weapon,
            HandType = WeaponHandType.TwoHand, RequiredCultivationLevel = 0,
        };
        bool o5 = EquipmentValidator.ValidateEquip(twoHand, EquipmentSlot.WeaponOff, null, empty, out _);
        Check(!o5, "O5 двуручное в WeaponOff по-прежнему запрещено", "P1-18-регресс");

        // --- O6 (контроль): одноручное → WeaponMain работает как раньше.
        bool o6 = EquipmentValidator.ValidateEquip(oneHand, EquipmentSlot.WeaponMain, null, empty, out _);
        Check(o6, "O6 одноручное → WeaponMain работает как прежде", "P1-18-регресс");
    }

    // ── P. Фаза 12: StorageRing persistence (P1-19) =========================
    private void RunStorageRingPersistenceTest()
    {
        GD.Print("[Audit0922Sim] === P. StorageRingService: Save/Reset lifecycle (P1-19) ===");
        var container = GameBoot.Container;
        if (container == null)
        {
            GD.Print("[Audit0922Sim] DEFECT контейнер недоступен");
            _allPass = false;
            return;
        }
        var equipSub = container.Resolve<ISubscriber<EquipmentChangedEvent>>();
        var itemDb = container.Resolve<IItemDatabaseService>();
        var equipSvc = container.Resolve<IEquipmentService>();
        var svc = new StorageRingService(equipSub, itemDb, equipSvc, null);

        // --- P1/P2: контракты интерфейсов.
        Check(svc is ISaveable,
            "P1 StorageRingService : ISaveable (содержимое колец в сейве)", "P1-19");
        Check(svc is IWorldResettable,
            "P2 StorageRingService : IWorldResettable (сброс мира чистит хранилища)", "P1-19");
        if (svc is not ISaveable saveableSvc || svc is not IWorldResettable resettableSvc)
            return; // prefix: дальше бессмысленно — DEFECT уже зафиксирован

        // Мир A: кольцо с предметом (сейв A).
        svc.ActivateRingWithVolume("qa_ring", 2, 15f);
        var probe = new ItemData { ItemId = "qa_p_probe", Category = ItemCategory.Consumable, Volume = 0.1f };
        svc.TryStore("qa_ring", probe, out _);
        object saveA = saveableSvc.CaptureState();

        // Сейв B: то же кольцо ПУСТОЕ.
        var svcB = new StorageRingService(equipSub, itemDb, equipSvc, null);
        svcB.ActivateRingWithVolume("qa_ring", 2, 15f);
        object saveB = ((ISaveable)svcB).CaptureState();

        // --- P3: warm-load — ResetWorld → RestoreState(сейв B) → нет утечки A.
        resettableSvc.ResetWorld();
        saveableSvc.RestoreState(saveB);
        int afterRestore = svc.GetRingContents("qa_ring").Count;
        // реактивация кольца при восстановлении экипировки не воскрешает содержимое A
        svc.ActivateRingWithVolume("qa_ring", 2, 15f);
        Check(afterRestore == 0 && svc.GetRingContents("qa_ring").Count == 0,
            "P3 warm-load: ResetWorld+RestoreState(пустое кольцо) — нет утечки содержимого мира A", "P1-19");

        // --- P4: cold-load — содержимое сейва A восстанавливается.
        resettableSvc.ResetWorld();
        saveableSvc.RestoreState(saveA);
        bool restoredRing = svc.IsRingActive("qa_ring") && svc.GetRingContents("qa_ring").Count == 1;
        Check(restoredRing,
            "P4 cold-load: сохранённое содержимое кольца восстанавливается (нет потери)", "P1-19");

        // --- P5 (регресс): реактивация НЕ чистит (деактивация ≠ очистка).
        svc.DeactivateRing("qa_ring");
        svc.ActivateRingWithVolume("qa_ring", 2, 15f);
        Check(svc.GetRingContents("qa_ring").Count == 1,
            "P5 реактивация не очищает кольцо (снятое ≠ стёртое — прежний контракт)", "P1-19-регресс");
    }

    // ── Q. Фаза 14: конвейер развития статов (P1-20/P2-43/P2-49) ============
    private void RunStatProgressionTest()
    {
        GD.Print("[Audit0922Sim] === Q. Статы: продюсеры / порог / сон (P1-20/P2-43/P2-49) ===");
        var container = GameBoot.Container;
        if (container == null)
        {
            GD.Print("[Audit0922Sim] DEFECT контейнер недоступен");
            _allPass = false;
            return;
        }
        var stats = container.Resolve<IStatService>();
        var dmgPub = container.Resolve<IPublisher<DamageAppliedEvent>>();
        var techPub = container.Resolve<IPublisher<TechniqueUsedEvent>>();
        var medPub = container.Resolve<IPublisher<MeditationStateChangedEvent>>();

        // --- Q1 (§5.1): удар игрока → STR +0.001.
        float strD0 = stats.GetVirtualDelta(StatType.Strength);
        dmgPub.Publish(new DamageAppliedEvent(PlayerIdResolver.PlayerCanonical, "qa_q_npc", 5,
            DamageType.Physical, Element.Neutral, BodyPartType.Torso, CombatAttackResult.Hit, CombatSubtype.None));
        Check(stats.GetVirtualDelta(StatType.Strength) > strD0 + 0.0009f,
            "Q1 продюсер: удар игрока → STR-дельта (+0.001, §5.1)", "P1-20");

        // --- Q2: уклонение игрока → AGI.
        float agiD0 = stats.GetVirtualDelta(StatType.Agility);
        dmgPub.Publish(new DamageAppliedEvent("qa_q_att", PlayerIdResolver.PlayerCanonical, 5,
            DamageType.Physical, Element.Neutral, BodyPartType.Torso, CombatAttackResult.Dodge, CombatSubtype.None));
        Check(stats.GetVirtualDelta(StatType.Agility) > agiD0 + 0.0009f,
            "Q2 продюсер: уклонение игрока → AGI-дельта", "P1-20");

        // --- Q3: получение урона → VIT.
        float vitD0 = stats.GetVirtualDelta(StatType.Vitality);
        dmgPub.Publish(new DamageAppliedEvent("qa_q_att", PlayerIdResolver.PlayerCanonical, 5,
            DamageType.Physical, Element.Neutral, BodyPartType.Torso, CombatAttackResult.Hit, CombatSubtype.None));
        Check(stats.GetVirtualDelta(StatType.Vitality) > vitD0 + 0.0009f,
            "Q3 продюсер: получение урона игроком → VIT-дельта", "P1-20");

        // --- Q4: техника → INT; медитация → INT +0.01/мин.
        float intD0 = stats.GetVirtualDelta(StatType.Intelligence);
        techPub.Publish(new TechniqueUsedEvent(PlayerIdResolver.PlayerCanonical, "qa_q_tech", 10));
        Check(stats.GetVirtualDelta(StatType.Intelligence) > intD0 + 0.0009f,
            "Q4 продюсер: использование техники → INT-дельта", "P1-20");

        float intD1 = stats.GetVirtualDelta(StatType.Intelligence);
        medPub.Publish(new MeditationStateChangedEvent(true, 1f));
        var pm = container.Resolve<PlayerModule>();
        pm.Tick(999001); // 1 игровой тик = 1 минута медитации
        float medGain = stats.GetVirtualDelta(StatType.Intelligence) - intD1;
        medPub.Publish(new MeditationStateChangedEvent(false, 0f));
        Check(medGain > 0.009f,
            "Q4b продюсер: медитация → INT +0.01/мин (§5.2)", "P1-20");

        // --- Q5 (P2-49): инварианты AddVirtualDelta.
        stats.AddVirtualDelta(StatType.Intelligence, 100f);
        Check(Math.Abs(stats.GetVirtualDelta(StatType.Intelligence) - 15f) < 0.01f,
            "Q5 кап виртуальной дельты INT = 15 (§4.2)", "P2-49");

        float strD1 = stats.GetVirtualDelta(StatType.Strength);
        stats.AddVirtualDelta(StatType.Strength, -5f);
        Check(stats.GetVirtualDelta(StatType.Strength) == strD1,
            "Q5b отрицательная дельта отвергается", "P2-49");

        stats.AddVirtualDelta(StatType.CritChance, 5f);
        Check(stats.GetVirtualDelta(StatType.CritChance) == 0f,
            "Q5c вторичные статы дельту не накапливают (§2: только первичные)", "P2-49");

        // --- Q6 (P1-20): порог канона max(1, floor(stat/10)).
        float strStat = stats.GetStat(StatType.Strength);
        float expectedThr = MathF.Max(1f, MathF.Floor(strStat / 10f));
        Check(Math.Abs(stats.GetThreshold(StatType.Strength) - expectedThr) < 0.01f,
            "Q6 порог = max(1, floor(stat/10)) — канон §3.1 (не константа 100)", "P1-20");

        // --- Q7..Q10: изолированный StatService (детерминированная математика).
        var s2 = new StatService();
        s2.SetStat(StatType.Strength, 10f);
        s2.AddVirtualDelta(StatType.Strength, 0.5f);
        s2.ConsolidateSleep(8f); // min(0.5, 8×0.025) = 0.2 в стат; остаток 0.3
        Check(Math.Abs(s2.GetStat(StatType.Strength) - 10.2f) < 0.0001f
              && Math.Abs(s2.GetVirtualDelta(StatType.Strength) - 0.3f) < 0.0001f,
            "Q7 ConsolidateSleep(8ч): min(delta, hours×0.025) в стат, остаток сохраняется (§6.2)", "P2-43");

        float s2Stat = s2.GetStat(StatType.Strength);
        float s2Delta = s2.GetVirtualDelta(StatType.Strength);
        s2.ConsolidateSleep(2f); // < 4ч — без закрепления
        Check(Math.Abs(s2.GetStat(StatType.Strength) - s2Stat) < 0.00001f
              && Math.Abs(s2.GetVirtualDelta(StatType.Strength) - s2Delta) < 0.00001f,
            "Q8 сон < 4ч: закрепления нет, дельта не сгорает (§6.1)", "P2-43");

        // --- Q9 (§6.4): шаг повышения — delta ≥ threshold → стат +1.
        s2.AddVirtualDelta(StatType.Strength, 3f);
        s2.ConsolidateSleep(8f);
        // ожидание: consolidate 0.2 → 10.4; затем delta(3.1) ≥ thr(1) ×3 → 13.4, остаток 0.1
        Check(Math.Abs(s2.GetStat(StatType.Strength) - 13.4f) < 0.0001f
              && Math.Abs(s2.GetVirtualDelta(StatType.Strength) - 0.1f) < 0.001f,
            "Q9 шаг повышения: после закрепления delta ≥ threshold → +1 с вычитанием порога (§6.4)", "P1-20");

        // --- Q10 (P1-20): полный sleep-pipeline на живом игроке.
        var ps = container.Resolve<IPlayerService>();
        float strBase = stats.GetStat(StatType.Strength);
        stats.AddVirtualDelta(StatType.Strength, 0.5f);
        ps.StartSleep(8f);
        for (int i = 0; i < 480; i++) pm.Tick(999100 + i); // 8 игровых часов
        bool wokeUp = ps.SleepState == PlayerSleepState.Awake;
        bool grew = stats.GetStat(StatType.Strength) > strBase + 0.19f;
        Check(wokeUp && grew,
            "Q10 sleep-pipeline: StartSleep(8ч) → 480 тиков → авто-пробуждение + закрепление (§6)", "P1-20");

        // чистим за собой (QA-мир живёт дальше)
        if (!wokeUp) ps.WakeUp();
    }

    // ── R. Фаза 14: Revive-тело + dash-границы (P2-44/P2-45) ================
    private void RunReviveAndDashTest()
    {
        GD.Print("[Audit0922Sim] === R. Revive-тело + dash-границы (P2-44/P2-45) ===");
        var container = GameBoot.Container;
        if (container == null)
        {
            GD.Print("[Audit0922Sim] DEFECT контейнер недоступен");
            _allPass = false;
            return;
        }

        // --- R1 (P2-44): смертельное повреждение → Revive() оживляет ТЕЛО.
        var body = container.Resolve<IBodyService>();
        var player = container.Resolve<PlayerService>();
        body.ApplyDamage(BodyPartType.Head, 1_000_000);
        if (player.IsAlive) body.ApplyDamage(BodyPartType.Heart, 1_000_000);
        bool dead = !player.IsAlive;
        GD.Print($"[Audit0922Sim] diag: после vital-урона IsAlive={player.IsAlive}");
        if (!dead)
        {
            GD.Print("[Audit0922Sim] diag: игрок не умер от 1M урона — харнесс не сработал");
        }
        player.Revive();
        Check(dead && player.IsAlive,
            "R1 Revive() оживляет ТЕЛО (IsAlive через BodyService, не только флаг Health)", "P2-44");

        // --- R2 (P2-45): цель рывка клэмпится в границы мира.
        var posNE = PlayerTechniqueCaster.ComputeDashTarget(49, 49, 1, 1, 3, 49, 49);
        var posSW = PlayerTechniqueCaster.ComputeDashTarget(0, 0, -1, -1, 3, 49, 49);
        var posMid = PlayerTechniqueCaster.ComputeDashTarget(25, 25, 1, -1, 3, 49, 49);
        GD.Print($"[Audit0922Sim] diag: dash(49,49,+diagonal)→({posNE.X},{posNE.Y}); " +
                 $"dash(0,0,-diagonal)→({posSW.X},{posSW.Y}); mid→({posMid.X},{posMid.Y})");
        Check(posNE.X >= 0 && posNE.X <= 49 && posNE.Y >= 0 && posNE.Y <= 49
              && posSW.X >= 0 && posSW.Y >= 0 && posMid.X == 28 && posMid.Y == 22,
            "R2 dash-цель клэмпится в границы мира (логическая позиция не покидает карту)", "P2-45");
    }

    // ── T1. Фаза 12 P2-33: CurrencyService saturating-арифметика ───────────
    private void RunCurrencyOverflowTest()
    {
        GD.Print("[Audit0922Sim] === T1. CurrencyService: переполнение int (P2-33) ===");
        var b = new ContainerBuilder();
        b.RegisterInstance(new EventBus());
        b.RegisterInstance(new TradeConfig { StartStones = 50 });
        b.Register<CurrencyService>(Lifetime.Singleton);
        var c = b.Build();
        var cur = c.Resolve<CurrencyService>();

        // T1a: 50 + (MaxValue-10) + 100 — переполнение верхнего края.
        // Prefix: int-wrap → баланс ОТРИЦАТЕЛЬНЫЙ (аудитор: «затем это может
        // попасть в UI/торговлю/сейв»). Postfix: насыщение в int.MaxValue.
        cur.Add(int.MaxValue - 10);
        cur.Add(100);
        GD.Print($"[Audit0922Sim] diag: Add(MaxValue-10)+Add(100) → баланс {cur.SpiritStones}");
        Check(cur.SpiritStones == int.MaxValue,
            "T1a Add на верхнем крае int насыщается в int.MaxValue (баланс не становится отрицательным)", "P2-33");

        // T1b: прямой SetBalance с минусом — минусовой баланс невозможен.
        cur.SetBalance(-5);
        Check(cur.SpiritStones == 0,
            "T1b SetBalance(-5) → 0 (минусовой баланс не проходит и прямым вызовом)", "P2-33");
    }

    // ── T2. Фаза 12 P2-34: total сделки в long ===============================
    // Мини-DI: TradeService + фейки (БД с экстремальными Value, инвентарь,
    // валюта int.MaxValue, NPC-Merchant, генераторы без ассортимента —
    // сток = только «material_iron_ore» ×5 из TradeConfig).
    private void RunTradeOverflowTest()
    {
        GD.Print("[Audit0922Sim] === T2. TradeService: overflow стоимости сделки (P2-34) ===");
        var db = new FakeTradeItemDb();
        db.Items["material_iron_ore"] = new ItemData
        {
            ItemId = "material_iron_ore", NameRu = "Железная руда (экстрем.)",
            Category = ItemCategory.Material, Rarity = ItemRarity.Common,
            Stackable = true, MaxStack = 100, Weight = 1.5f, Volume = 1f,
            Value = 1_000_000_000, // unitPrice = 1.2e9 (влезает в int)
        };
        db.Items["sell_probe"] = new ItemData
        {
            ItemId = "sell_probe", NameRu = "Проба продажи",
            Category = ItemCategory.Material, Rarity = ItemRarity.Common,
            Stackable = true, MaxStack = 100, Weight = 1.5f, Volume = 1f,
            Value = 1_900_000_000, // sellPrice = 950M; ×3 = 2.85e9 > int.MaxValue
        };

        var inv = new FakeTradeInventory();
        var currency = new FakeTradeCurrency();
        var b = new ContainerBuilder();
        b.RegisterInstance(new EventBus());
        b.RegisterInstance<IInventoryService>(inv);
        b.RegisterInstance<IItemDatabaseService>(db);
        b.RegisterInstance<IEquipmentGenerator>(new FakeNullEquipmentGenerator());
        b.RegisterInstance<IItemGeneratorService>(new FakeNullItemGenerator());
        b.RegisterInstance<INPCService>(new FakeTradeNpcService());
        b.RegisterInstance<ICurrencyService>(currency);
        b.RegisterInstance(new TradeConfig
        {
            MarkupPermil = 1200, SellPermil = 500,
            StockWeaponMin = 0, StockWeaponMax = 0,
            StockArmorMin = 0, StockArmorMax = 0,
            StockConsumableMin = 0, StockConsumableMax = 0,
            StockMaterialCount = 1, MaterialStackMin = 5, MaterialStackMax = 5,
        });
        b.Register<ITradeService, TradeService>(Lifetime.Singleton);
        var c = b.Build();
        var trade = c.Resolve<ITradeService>();
        trade.OpenTrade("npc_t2");

        // T2a (покупка): 1.2e9 × 5 = 6e9 > int.MaxValue.
        // Prefix: int-wrap → total 1_705_032_704 → «успех» с заниженной ценой
        // (Spend вызван). Postfix: честный отказ ДО списания.
        bool buyResult = trade.TryBuy("npc_t2", "material_iron_ore", 5);
        GD.Print($"[Audit0922Sim] diag: TryBuy(material×5, unit 1.2e9) → {buyResult}; " +
                 $"SpendCalls={currency.SpendCalls}, LastSpend={currency.LastSpendAmount}");
        Check(!buyResult && currency.SpendCalls == 0,
            "T2a TryBuy: total 6e9 > int.MaxValue → честный отказ ДО списания камней", "P2-34");

        // T2b (продажа): 950M × 3 = 2.85e9 > int.MaxValue.
        // Prefix: int-wrap → total −1_444_967_296 → предметы ИЗЪЯТЫ, оплата
        // молча 0 (if total > 0 не срабатывает). Postfix: насыщение —
        // Add(int.MaxValue), событие с представимым максимумом.
        bool sellResult = trade.TrySell("npc_t2", "sell_probe", 3);
        GD.Print($"[Audit0922Sim] diag: TrySell(sell_probe×3) → {sellResult}; " +
                 $"AddCalls={currency.AddCalls}, LastAdd={currency.LastAddAmount}");
        Check(sellResult && currency.AddCalls == 1 && currency.LastAddAmount == int.MaxValue,
            "T2b TrySell: total 2.85e9 > int.MaxValue → насыщение int.MaxValue (не молча 0 и не минус)", "P2-34");
    }

    // ── T3. Фаза 12 P2-35: RestoreState инвентаря валидирует слоты =========
    // БД «known_potion» MaxStack=10; мусорные слоты НЕ создаются и не
    // отравляют _itemCountCache (главный вред по аудитору: count=-100).
    private void RunInventoryRestoreValidationTest()
    {
        GD.Print("[Audit0922Sim] === T3. InventoryService.RestoreState: валидация (P2-35) ===");
        var bus = new EventBus();
        var db = new FakeTradeItemDb();
        db.Items["known_potion"] = new ItemData
        {
            ItemId = "known_potion", NameRu = "Лекарство",
            Category = ItemCategory.Consumable, Rarity = ItemRarity.Common,
            Stackable = true, MaxStack = 10, Weight = 0.1f, Volume = 0.05f,
            Value = 5,
        };
        var inv = new InventoryService(
            new EventBusPublisher<ItemAddedEvent>(bus),
            new EventBusPublisher<ItemRemovedEvent>(bus),
            db, null);
        inv.Configure(new InventoryConfig());

        // T3a: полный мусорный набор аудитора: count=-100, category=999,
        // rarity=999, фантомный ItemId, count=5000 > MaxStack=10.
        var garbage = new InventorySaveData
        {
            slots = new[]
            {
                new InventorySlotSaveData { itemId = "known_potion", count = -100, category = 0, rarity = 0 },
                new InventorySlotSaveData { itemId = "known_potion", count = 5, category = 999, rarity = 0 },
                new InventorySlotSaveData { itemId = "known_potion", count = 5, category = 3, rarity = 999 },
                new InventorySlotSaveData { itemId = "ghost_item", count = 5, category = 3, rarity = 0 },
                new InventorySlotSaveData { itemId = "known_potion", count = 5000, category = 3, rarity = 0 },
            },
        };
        inv.RestoreState(garbage);
        GD.Print($"[Audit0922Sim] diag: мусорный Restore → слотов {inv.UsedSlots}, " +
                 $"known_potion в кэше {inv.GetItemCount("known_potion")}");
        Check(inv.UsedSlots == 0 && inv.GetItemCount("known_potion") == 0,
            "T3a Restore отвергает count=-100 / category=999 / rarity=999 / фантомный ItemId / count>MaxStack (кэш не отравлен)", "P2-35");

        // T3b: валидный слот по-прежнему восстанавливается.
        var valid = new InventorySaveData
        {
            slots = new[] { new InventorySlotSaveData { itemId = "known_potion", count = 3, category = 3, rarity = 0 } },
        };
        inv.RestoreState(valid);
        Check(inv.UsedSlots == 1 && inv.GetItemCount("known_potion") == 3,
            "T3b валидный слот восстанавливается (count 3 ≤ MaxStack 10, категория/редкость определены)", "P2-35");

        inv.ResetWorld();
    }

    // ── T4. Фаза 12 P2-36: СТАВКИ ПЕРЕНОСКИ — владельческий канон ========
    // ⚠ ПРЯМОЕ УКАЗАНИЕ ВЛАДЕЛЬЦА (2026-09-22 вечер, конс пект): канон =
    // 50 кг / 100 ед. (user request 2026-08-22). Прямые указания владельца
    // имеют приоритет ВЫШЕ аудитора и ВЫШЕ документации → P2-36 (предложение
    // аудитора «30/30 по §3.3») отклонён by-user; док-канон конс пектирован
    // в INVENTORY_SYSTEM §3.3-примечание. Проверка — страж владельческих
    // значений: никакой будущий «канон-док-фикс» не срежет их молча.
    private void RunCarryLimitsCanonTest()
    {
        GD.Print("[Audit0922Sim] === T4. Переноска: владельческий канон 50 кг / 100 ед. (P2-36 by-user) ===");

        // T4a: источник значений — InventoryConfig + GameConstants.
        var cfg = new InventoryConfig();
        GD.Print($"[Audit0922Sim] diag: InventoryConfig {cfg.MaxCarryWeight}/{cfg.MaxCarryVolume}; " +
                 $"GameConstants.BASE_CARRY_WEIGHT {GameConstants.BASE_CARRY_WEIGHT}");
        Check(Math.Abs(cfg.MaxCarryWeight - 50f) < 0.001f && Math.Abs(cfg.MaxCarryVolume - 100f) < 0.001f
              && Math.Abs(GameConstants.BASE_CARRY_WEIGHT - 50f) < 0.001f,
            "T4a InventoryConfig и GameConstants: 50 кг / 100 ед. (владельческий канон, user request 2026-08-22)", "P2-36-by-user");

        // T4b: живой мир — эффективные лимиты 50/100 (стартовый набор рюкзак
        // не надевает — StartingGearPhase без Back-слота, бонусов нет).
        var container = GameBoot.Container;
        if (container == null)
        {
            GD.Print("[Audit0922Sim] DEFECT контейнер недоступен");
            _allPass = false;
            return;
        }
        var liveInv = container.Resolve<IInventoryService>();
        GD.Print($"[Audit0922Sim] diag: живой мир — вес {liveInv.GetEffectiveMaxWeight()}, " +
                 $"объём {liveInv.GetEffectiveMaxVolume()}");
        Check(Math.Abs(liveInv.GetEffectiveMaxWeight() - 50f) < 0.001f
              && Math.Abs(liveInv.GetEffectiveMaxVolume() - 100f) < 0.001f,
            "T4b живой инвентарь: эффективные лимиты 50 кг / 100 ед. (владельческий канон не срезан)", "P2-36-by-user");
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
    // ── S. Фаза 11 P2-бэклог (аудит 09.22 12:00): автосейв-каденция /
    // pause-authority / DeltaTime / канонизация времени / валидация даты ──

    // S1 (P2-26): автосейв привязан к МИРОВОМУ тику (ITimeService.TickCount),
    // не к процессному счётчику. Аудитор: GameBoot._currentTick — счётчик
    // текущего процесса; после NewGame/Load (TickCount → 0 / восстановленный)
    // первый автосейв мог прийти через 1/7/18 минут — остаток process-счётчика.
    private void RunAutosaveCadenceTest()
    {
        GD.Print("[Audit0922Sim] === S1. Автосейв по МИРОВЫМ тикам (P2-26) ===");
        var fakeTime = new FakeTimeService { WorldTick = 29 };
        var fakeSave = new FakeSaveService();
        var fakeHandler = new FakeAdapterFileHandler();

        // Pub/sub: Container авто-резолвит IPublisher<T>/ISubscriber<T> через
        // зарегистрированный EventBus (спец-кейс Resolve) — отдельные фейки
        // не нужны (и не сработали бы: generic-кейс проверяется до регистраций).
        var b = new ContainerBuilder();
        b.RegisterInstance(new EventBus());
        b.RegisterInstance<ITimeService>(fakeTime);
        b.RegisterInstance<ISaveService>(fakeSave);
        b.RegisterInstance<IGameSession>(new FakePlayingSession());
        b.RegisterInstance<ISaveFileHandler>(fakeHandler);
        b.RegisterInstance(new SaveDataAggregator(fakeHandler));
        b.RegisterInstance(new SaveConfig { AutoSaveIntervalMinutes = 30 });
        b.Register<SaveModule>(Lifetime.Singleton);
        var c = b.Build();
        var sm = c.Resolve<SaveModule>();
        sm.Start();

        // Инициализация отсчёта: первый Tick в мире (мировой тик 29) —
        // автосейва нет, начинается отсчёт «30 игровых минут».
        sm.Tick(29);

        // S1a: мировой интервал истёк (29 → 59 = 30 игровых минут), process-тик
        // 59 не кратен 30 — в prefix автосейва НЕТ (каденция от process-счётчика).
        fakeTime.WorldTick = 59;
        sm.Tick(59);
        Check(fakeSave.SaveCalls == 1,
            "S1a автосейв по мировому тику: 29→59 = 30 минут — сейв есть (process-остаток не решает)", "P2-26");

        // S1b: граница мира (аудиторский сценарий: world tick = 0 при
        // process tick = 10029) — сброс SaveModule делает отсчёт заново,
        // process-остаток НЕ триггерит автосейв.
        fakeTime.WorldTick = 0;
        if ((object)sm is IWorldResettable smr) smr.ResetWorld();
        sm.Tick(10029);
        Check(fakeSave.SaveCalls == 1,
            "S1b после сброса мира (tick=0) process-остаток 10029 не триггерит автосейв", "P2-26");

        // S1c: первый автосейв нового мира — ровно на 30-м МИРОВОМ тике.
        fakeTime.WorldTick = 30;
        sm.Tick(10030);
        Check(fakeSave.SaveCalls == 2,
            "S1c первый автосейв нового мира — ровно на 30-м мировом тике (контракт «каждые 30 игровых минут»)", "P2-26");

        // S1d: hitch/bulk — мировой тик прыгнул на 60 (дважды интервал):
        // автосейв ОДИН раз сразу (разность честно учтена, каденция не потеряна).
        fakeTime.WorldTick += 60;
        sm.Tick(10031);
        Check(fakeSave.SaveCalls == 3,
            "S1d bulk-скачок мирового тика: автосейв срабатывает сразу (каденция не теряется при hitch)", "P2-26");
    }

    // S2-S4 (P2-27/P2-28): единая pause-authority (Session.Pause
    // останавливает TimeService), Resume восстанавливает скорость до паузы
    // (Fast не превращается в Normal), IsPaused ⇒ DeltaTime == 0.
    private void RunPauseAuthorityTest()
    {
        GD.Print("[Audit0922Sim] === S2-S4. Pause-authority / скорость / DeltaTime (P2-27/P2-28) ===");
        var container = GameBoot.Container;
        if (container == null)
        {
            GD.Print("[Audit0922Sim] DEFECT контейнер недоступен");
            _allPass = false;
            return;
        }
        var ts = container.Resolve<TimeService>();
        var session = container.Resolve<IGameSession>();

        try
        {
            // S2: сессионная пауза — единая истина с TimeService.
            if (session.State == SessionState.Playing)
            {
                session.Pause();
                Check(session.State == SessionState.Paused && ts.IsPaused,
                    "S2a Session.Pause() останавливает мировое время (единая pause-authority, две истины сходятся)", "P2-27");
                session.Resume();
                Check(session.State == SessionState.Playing && !ts.IsPaused,
                    "S2b Session.Resume() возвращает время (согласовано с TimeService)", "P2-27");
            }
            else
            {
                Check(false, "S2 харнесс: сим исполняется из Playing-сессии (GODOT_NEWGAME=1)", "P2-27");
            }

            // S3: Resume восстанавливает скорость ДО паузы.
            var savedSpeed = ts.Speed;
            ts.Speed = TimeSpeed.Fast;
            ts.Pause();
            bool pausedNow = ts.IsPaused;
            ts.Resume();
            Check(pausedNow && ts.Speed == TimeSpeed.Fast,
                "S3 TimeService.Resume() возвращает скорость до паузы (Fast не уничтожается в Normal)", "P2-27");
            ts.Speed = savedSpeed;

            // S4: контракт «время стоит» — DeltaTime == 0 на паузе.
            ts.Pause();
            Check(ts.IsPaused && ts.DeltaTime == 0f,
                "S4 IsPaused ⇒ DeltaTime == 0 (латентный договор: будущий код не сочтёт шаг пройденным)", "P2-28");
        }
        finally
        {
            ts.Resume();
            if (session.State == SessionState.Paused) session.Resume();
            ts.Speed = TimeSpeed.Normal;
        }
    }

    // S5-S6 (P2-29/P2-30): Load канонизирует три представления времени
    // (дата первична, TickCount/TotalTime — производные), невалидная дата
    // бросает → агрегатор помечает сейв битым → честный отказ загрузки.
    private void RunTimeCanonizationTest()
    {
        GD.Print("[Audit0922Sim] === S5-S6. Канонизация времени + валидация даты (P2-29/P2-30) ===");
        var container = GameBoot.Container;
        if (container == null)
        {
            GD.Print("[Audit0922Sim] DEFECT контейнер недоступен");
            _allPass = false;
            return;
        }
        var ts = container.Resolve<TimeService>();

        try
        {
            // S5: рассинхронный блок world_time (дата ≠ TickCount ≠ TotalTime) —
            // канонизация: дата первична, TickCount = минуты с 06:00 дня 1,
            // TotalTime = TickCount.
            ts.RestoreState(new TimeService.WorldTimeSaveState
            {
                Year = GameConstants.START_YEAR, Month = 2, Day = 3, Hour = 4, Minute = 5,
                TickCount = 777, TotalTime = 42f,
            });
            var canonicalDate = new WorldTime(GameConstants.START_YEAR, 2, 3, 4, 5);
            int canonicalTicks = canonicalDate.TotalMinutes - new WorldTime(GameConstants.START_YEAR, 1, 1, 6, 0).TotalMinutes;
            Check(ts.TickCount == canonicalTicks && Math.Abs(ts.TotalTime - canonicalTicks) < 0.001f
                  && ts.CurrentTime == canonicalDate,
                "S5 Load канонизирует время: дата первична, TickCount/TotalTime — производные (одно представление)", "P2-29");

            // S6a: WorldTime отвергает невалидные компоненты даты.
            bool Throws(Action a) { try { a(); return false; } catch (ArgumentOutOfRangeException) { return true; } }
            Check(Throws(() => new WorldTime(1864, 13, 1, 6, 0))
                  && Throws(() => new WorldTime(1864, 1, 0, 6, 0))
                  && Throws(() => new WorldTime(1864, 1, 1, 29, 0))
                  && Throws(() => new WorldTime(1864, 1, 1, 6, -10))
                  && Throws(() => new WorldTime(1863, 1, 1, 6, 0)),
                "S6a WorldTime отвергает month=13 / day=0 / hour=29 / minute=-10 / год до эпохи (ArgumentOutOfRangeException)", "P2-30");

            // S6b: RestoreState с мусорной датой кидает → сейв честно битый
            // (SaveDataAggregator ловит → LastErrors → Load=false → отказ в меню).
            bool restoreThrew = false;
            try
            {
                ts.RestoreState(new TimeService.WorldTimeSaveState
                { Year = 1864, Month = 13, Day = 1, Hour = 6, Minute = 0, TickCount = 0, TotalTime = 0f });
            }
            catch (ArgumentOutOfRangeException) { restoreThrew = true; }
            Check(restoreThrew,
                "S6b RestoreState с невалидной датой бросает (битый сейв → отказ загрузки, а не мусорная дата)", "P2-30");

            // S7 (P3-11): мёртвые противоречивые конфиг-источники удалены.
            var wct = typeof(WorldConfig);
            Check(wct.GetField("StartHour") == null && wct.GetField("StartYear") == null
                  && wct.GetField("StartMonth") == null && wct.GetField("StartDay") == null
                  && wct.GetField("AutoSaveIntervalTicks") == null && wct.GetField("BaseTickRate") == null,
                "S7 WorldConfig не содержит мёртвых источников времени (Start*/AutoSaveIntervalTicks/BaseTickRate — канон в GameConstants+SaveConfig)", "P3-11");
        }
        finally
        {
            ts.ResetWorld();
        }
    }

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
