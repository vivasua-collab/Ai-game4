#nullable enable
// Создано: 2026-10-22 — Этап-6 аудита (инвентарь/торговля/экономика): headless-
// верификация четырёх фиксов по дословным регрессионным сценариям аудитора:
//
//   BUG-018 (P2) «зацикливание добавления предмета при некорректном MaxStack»:
//     — TryAddItem стакающегося предмета с MaxStack=0 и MaxStack=-1:
//       контролируемый отказ (false, ничего не добавлено, один быстрый возврат),
//       а не зависание/stack overflow и не слоты с Count ≤ 0;
//     — ItemDatabase.Register отклоняет битое определение (Stackable=true,
//       MaxStack ≤ 0) — каталог не может быть источником поломки;
//     — RestoreState каталога пропускает битую запись (skipped), валидные
//       предметы восстанавливаются.
//
//   BUG-019 (P2) «баланс и события расходятся при достижении максимума»:
//     — CurrencyService: баланс int.MaxValue − 3, Add(10) → баланс достигает
//       максимума, CurrencyChangedEvent.Delta == 3 (фактическое, не +10);
//     — TradeService.TrySell при потолке баланса: TradeCompletedEvent.Price ==
//       реально зачисленная сумма (частичная выплата), баланс == int.MaxValue.
//
//   BUG-020 (P2) «котировки обмена могут вернуть отрицательную сумму»:
//     — QuoteStonesToGold(10) == 500 (канонический пример);
//     — граница int.MaxValue: 21474836 → 1073741800 (представимо);
//     — 21474837 (аудиторский пример int-переполнения stones×100) → прежде
//       отрицательное золото, теперь +1073741850 — положительное;
//     — непредставимый объём (продажа ≥ 42949673 камней, покупка ≥ 17895698):
//       ExchangeQuote.IsRepresentable == false, Gold == 0 — явный отказ.
//
//   BUG-021 (P2) «загрузка колец хранения не проверяет содержимое стаков»:
//     — RestoreState с битым сохранением: неизвестный ItemId, Count > MaxStack
//       (500 против 99), мёртвые enum-значения, отрицательный Tier,
//       неположительный MaxVolume — всё отклонено с журналом; валидный слот
//       восстановлен и извлекаем; переполненный стак НЕ попадает в рабочее
//       состояние (TryRetrieve не может выдать лишнего).
//
// Запуск: GODOT_NEWGAME=1 GODOT_STAGE6_DEBUG=1 \
//   godot --headless --path . scenes/MainMenu.tscn
using Godot;
using System.Collections.Generic;
using System.Linq;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Helpers;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Modules.Save;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Headless-верификация Этапа-6: инварианты предметов, честные события
/// кошелька/сделок, непредставимые котировки, целостность колец.
/// Итог: [Stage6Sim] VERDICT: PASS/FAIL.
/// </summary>
public partial class Stage6TradeEconomySimDebug : Node
{
    [Inject] private IInventoryService? _inventory;
    [Inject] private IItemDatabaseService? _itemDb;
    [Inject] private ICurrencyService? _currency;
    [Inject] private ICurrencyExchangeService? _exchange;
    [Inject] private IStorageRingService? _storageRing;
    [Inject] private ITradeService? _trade;
    [Inject] private INPCSpawnerService? _npcSpawner;
    [Inject] private IPlayerService? _playerService;

    [Inject] private ISubscriber<CurrencyChangedEvent>? _currencyChangedSub;
    [Inject] private ISubscriber<TradeCompletedEvent>? _tradeCompletedSub;
    [Inject] private ISubscriber<ItemAddedEvent>? _itemAddedSub;

    private System.IDisposable? _currencyChangedToken;
    private System.IDisposable? _tradeCompletedToken;
    private System.IDisposable? _itemAddedToken;

    // === Наблюдения событий ===
    private readonly List<(int balance, int delta)> _currencyEvents = new();
    private readonly List<(string itemId, int count, bool purchase, int price)> _tradeEvents = new();
    private readonly List<(string itemId, int count, string source)> _itemEvents = new();

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        _currencyChangedToken = _currencyChangedSub?.Subscribe((in CurrencyChangedEvent e) =>
        {
            _currencyEvents.Add((e.SpiritStones, e.Delta));
        });
        _tradeCompletedToken = _tradeCompletedSub?.Subscribe((in TradeCompletedEvent e) =>
        {
            _tradeEvents.Add((e.ItemId, e.Count, e.IsPurchase, e.Price));
            GD.Print($"[Stage6Sim] ← TradeCompletedEvent: {e.ItemId}×{e.Count} " +
                     $"purchase={e.IsPurchase} price={e.Price}");
        });
        _itemAddedToken = _itemAddedSub?.Subscribe((in ItemAddedEvent e) =>
        {
            _itemEvents.Add((e.ItemId, e.Count, e.Source));
            GD.Print($"[Stage6Sim] ← ItemAddedEvent: {e.ItemId}×{e.Count} (source={e.Source})");
        });

        GD.Print("[Stage6Sim] Ready — Этап-6: инварианты предметов / честный кошелёк / котировки / кольца, старт через 2с");
        _ = RunSequenceAsync();
    }

    public override void _ExitTree()
    {
        _currencyChangedToken?.Dispose(); _currencyChangedToken = null;
        _tradeCompletedToken?.Dispose(); _tradeCompletedToken = null;
        _itemAddedToken?.Dispose(); _itemAddedToken = null;
    }

    private async System.Threading.Tasks.Task RunSequenceAsync()
    {
        await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);

        if (_inventory == null || _itemDb == null || _currency == null || _exchange == null
            || _storageRing == null || _trade == null || _npcSpawner == null || _playerService == null)
        {
            GD.Print("[Stage6Sim] VERDICT: FAIL — DI not wired");
            return;
        }

        bool pass = true;
        var pp = _playerService.Position;

        // ════════════════════════════════════════════════════════════════
        // 1. BUG-018: MaxStack ≤ 0 → контролируемый отказ TryAddItem
        // ════════════════════════════════════════════════════════════════
        var brokenZero = new ItemData
        {
            ItemId = "qa_stack_broken_zero", NameRu = "Битый стак 0",
            Stackable = true, MaxStack = 0, Category = ItemCategory.Material,
            Weight = 0.5f, Volume = 0.5f,
        };
        var brokenNeg = new ItemData
        {
            ItemId = "qa_stack_broken_neg", NameRu = "Битый стак -1",
            Stackable = true, MaxStack = -1, Category = ItemCategory.Material,
            Weight = 0.5f, Volume = 0.5f,
        };
        int itemEvents0 = _itemEvents.Count;
        bool addZeroRejected = !_inventory.TryAddItem(brokenZero, 5);
        bool addNegRejected = !_inventory.TryAddItem(brokenNeg, 5);
        bool noJunkSlots = _inventory.GetItemCount("qa_stack_broken_zero") == 0
                        && _inventory.GetItemCount("qa_stack_broken_neg") == 0;
        bool noBadEvents = _itemEvents.Count == itemEvents0;
        bool b018AddOk = addZeroRejected && addNegRejected && noJunkSlots && noBadEvents;
        GD.Print($"[Stage6Sim] 1-TryAddItem-отказ: MaxStack=0 → {addZeroRejected}, " +
                 $"MaxStack=-1 → {addNegRejected}, мусорных слотов нет → {noJunkSlots}, " +
                 $"событий с count ≤ 0 нет → {noBadEvents} → {(b018AddOk ? "OK" : "FAIL")}");
        pass &= b018AddOk;

        // BUG-018: Register отклоняет битое определение.
        _itemDb.Register(brokenZero);
        bool registerRejected = !_itemDb.TryGetItem("qa_stack_broken_zero", out _);
        // Валидный предмет регистрируется нормально (контроль ложных срабатываний).
        var okItem = new ItemData
        {
            ItemId = "qa_stack_ok", NameRu = "Валидный стак",
            Stackable = true, MaxStack = 99, Category = ItemCategory.Material,
            Weight = 0.5f, Volume = 0.5f, Value = 5,
        };
        _itemDb.Register(okItem);
        bool registerValidOk = _itemDb.TryGetItem("qa_stack_ok", out _);
        bool b018RegOk = registerRejected && registerValidOk;
        GD.Print($"[Stage6Sim] 1-Register: битое отклонено → {registerRejected}, " +
                 $"валидное принято → {registerValidOk} → {(b018RegOk ? "OK" : "FAIL")}");
        pass &= b018RegOk;

        // BUG-018: RestoreState каталога пропускает битую запись.
        var dbState = new Modules.Generator.ItemDatabaseService.ItemDbSaveState();
        dbState.Items.Add(new Modules.Generator.ItemDatabaseService.ItemDbEntry
        {
            TypeKey = "item",
            Json = System.Text.Json.JsonSerializer.Serialize(brokenNeg, SaveJson.Options),
        });
        dbState.Items.Add(new Modules.Generator.ItemDatabaseService.ItemDbEntry
        {
            TypeKey = "item",
            Json = System.Text.Json.JsonSerializer.Serialize(okItem, SaveJson.Options),
        });
        if (_itemDb is Modules.Generator.ItemDatabaseService dbService)
        {
            dbService.RestoreState(dbState);
        }
        else
        {
            GD.Print("[Stage6Sim] VERDICT: FAIL — ItemDatabaseService не резолвится конкретным типом");
            return;
        }
        bool restoreSkippedBroken = !_itemDb.TryGetItem("qa_stack_broken_neg", out _);
        bool restoreKeptValid = _itemDb.TryGetItem("qa_stack_ok", out _);
        bool b018RestoreOk = restoreSkippedBroken && restoreKeptValid;
        GD.Print($"[Stage6Sim] 1-RestoreState: битая запись пропущена → {restoreSkippedBroken}, " +
                 $"валидная восстановлена → {restoreKeptValid} → {(b018RestoreOk ? "OK" : "FAIL")}");
        pass &= b018RestoreOk;

        // ════════════════════════════════════════════════════════════════
        // 2. BUG-019: фактический Delta у потолка баланса
        // ════════════════════════════════════════════════════════════════
        _currency.SetBalance(int.MaxValue - 3);
        _currencyEvents.Clear();
        _currency.Add(10);
        bool balanceAtMax = _currency.SpiritStones == int.MaxValue;
        bool deltaIsActual = _currencyEvents.Count == 1 && _currencyEvents[0].delta == 3
                          && _currencyEvents[0].balance == int.MaxValue;
        bool b019AddOk = balanceAtMax && deltaIsActual;
        GD.Print($"[Stage6Sim] 2-Add(10)@Max-3: баланс {int.MaxValue} → " +
                 $"{_currency.SpiritStones}, Delta события = " +
                 $"{(_currencyEvents.Count == 1 ? _currencyEvents[0].delta : -999)} " +
                 $"(ожидание 3) → {(b019AddOk ? "OK" : "FAIL")}");
        pass &= b019AddOk;

        // BUG-019: TrySell при потолке — TradeCompletedEvent.Price = зачисленное.
        // Реальный торговец + реальный инвентарь: продаём 30 руды (цена 2/шт =
        // 60 номинала) при балансе MaxValue-3 → зачислено 3, событие честно.
        var ore = EnsureOre();
        if (ore == null)
        {
            GD.Print("[Stage6Sim] VERDICT: FAIL — material_iron_ore недоступен для секции продажи");
            return;
        }
        _inventory.TryAddItem(ore, 30, ItemSources.Cheat);
        int oreHave = _inventory.GetItemCount(ore.ItemId);
        if (oreHave < 1)
        {
            GD.Print("[Stage6Sim] VERDICT: FAIL — руда не попала в инвентарь (объём?)");
            return;
        }
        string merchantId = _npcSpawner.SpawnNPC("human", NPCRole.Merchant, 1,
            new Position2D(pp.X + 3, pp.Y + 3), 77360);
        _trade.OpenTrade(merchantId);
        _currency.SetBalance(int.MaxValue - 3);
        _tradeEvents.Clear();
        bool sold = _trade.TrySell(merchantId, ore.ItemId, oreHave);
        bool sellEventHonest = _tradeEvents.Count == 1 && _tradeEvents[0].price == 3
                            && !_tradeEvents[0].purchase;
        bool sellBalanceMax = _currency.SpiritStones == int.MaxValue;
        bool oreSoldOut = _inventory.GetItemCount(ore.ItemId) == 0;
        bool b019SellOk = sold && sellEventHonest && sellBalanceMax && oreSoldOut;
        GD.Print($"[Stage6Sim] 2-TrySell@Max-3: продано {oreHave} шт, сделка {sold}, " +
                 $"Price события = {(_tradeEvents.Count == 1 ? _tradeEvents[0].price : -999)} " +
                 $"(ожидание 3), баланс == MaxValue → {sellBalanceMax}, " +
                 $"руда изъята → {oreSoldOut} → {(b019SellOk ? "OK" : "FAIL")}");
        pass &= b019SellOk;

        // ════════════════════════════════════════════════════════════════
        // 3. BUG-020: котировки — long-математика + явный отказ
        // ════════════════════════════════════════════════════════════════
        var q10 = _exchange.QuoteStonesToGold(10);
        bool q10Ok = q10.Gold == 500 && q10.IsRepresentable;
        var qBoundary = _exchange.QuoteStonesToGold(21474836);   // 21474836×100×0.5
        bool qBoundaryOk = qBoundary.Gold == 1073741800 && qBoundary.IsRepresentable;
        var qAuditCase = _exchange.QuoteStonesToGold(21474837); // аудит: int-переполнение ×100
        bool qAuditOk = qAuditCase.Gold == 1073741850 && qAuditCase.IsRepresentable
                     && qAuditCase.Gold > 0;
        var qUnrep = _exchange.QuoteStonesToGold(42949673);     // > int.MaxValue после 500‰
        bool qUnrepOk = !qUnrep.IsRepresentable && qUnrep.Gold == 0;
        var qBuyUnrep = _exchange.QuoteGoldToStones(17895698);   // ×1200‰ > int.MaxValue
        bool qBuyOk = !qBuyUnrep.IsRepresentable && qBuyUnrep.Gold == 0;
        var qBuyBoundary = _exchange.QuoteGoldToStones(17895697); // 2147483640 ≤ MaxValue
        bool qBuyBoundaryOk = qBuyBoundary.Gold == 2147483640 && qBuyBoundary.IsRepresentable;
        bool b020Ok = q10Ok && qBoundaryOk && qAuditOk && qUnrepOk && qBuyOk && qBuyBoundaryOk;
        GD.Print($"[Stage6Sim] 3-котировки: 10→{q10.Gold} | граница 21474836→{qBoundary.Gold} " +
                 $"| аудит-кейс 21474837→{qAuditCase.Gold} (прежде отрицательное) | " +
                 $"непредставимая продажа → representable={qUnrep.IsRepresentable} | " +
                 $"непредставимая покупка → representable={qBuyUnrep.IsRepresentable} " +
                 $"| граница покупки → {qBuyBoundary.Gold} → {(b020Ok ? "OK" : "FAIL")}");
        pass &= b020Ok;

        // ════════════════════════════════════════════════════════════════
        // 4. BUG-021: RestoreState колец отсекает битые слоты и кольца
        // ════════════════════════════════════════════════════════════════
        var ringState = new Modules.Inventory.StorageRingService.StorageRingsSaveState();
        var goodRing = new Modules.Inventory.StorageRingService.RingEntrySave
        {
            RingItemId = "qa_ring_good", Tier = 2, MaxVolume = 15f, Capacity = 0, IsActive = true,
        };
        goodRing.StoredItems.Add(new InventorySlot(ore.ItemId, 10, ore.Category, ore.Rarity)); // валидный
        goodRing.StoredItems.Add(new InventorySlot("qa_ghost_item_9", 5, ItemCategory.Material, ItemRarity.Common)); // неизвестный ItemId
        goodRing.StoredItems.Add(new InventorySlot(ore.ItemId, 500, ore.Category, ore.Rarity)); // Count > MaxStack(99)
        goodRing.StoredItems.Add(new InventorySlot(ore.ItemId, 7, (ItemCategory)999, ore.Rarity)); // мёртвый enum категории
        goodRing.StoredItems.Add(new InventorySlot(ore.ItemId, 7, ore.Category, (ItemRarity)999)); // мёртвый enum редкости
        ringState.Rings.Add(goodRing);
        ringState.Rings.Add(new Modules.Inventory.StorageRingService.RingEntrySave
        {
            RingItemId = "qa_ring_neg_tier", Tier = -1, MaxVolume = 15f, IsActive = true,
        });
        ringState.Rings.Add(new Modules.Inventory.StorageRingService.RingEntrySave
        {
            RingItemId = "qa_ring_bad_volume", Tier = 2, MaxVolume = 0f, IsActive = true,
        });
        ringState.Rings.Add(new Modules.Inventory.StorageRingService.RingEntrySave
        {
            RingItemId = "qa_ring_neg_volume", Tier = 2, MaxVolume = -5f, IsActive = true,
        });

        if (_storageRing is Modules.Inventory.StorageRingService ringService)
        {
            ringService.RestoreState(ringState);
        }
        else
        {
            GD.Print("[Stage6Sim] VERDICT: FAIL — StorageRingService не резолвится конкретным типом");
            return;
        }

        var goodContents = _storageRing.GetRingContents("qa_ring_good");
        bool onlyValidSlot = goodContents.Count == 1
                          && goodContents[0].ItemId == ore.ItemId && goodContents[0].Count == 10;
        bool badRingsDropped = _storageRing.GetRingContents("qa_ring_neg_tier").Count == 0
                            && _storageRing.GetRingContents("qa_ring_bad_volume").Count == 0
                            && _storageRing.GetRingContents("qa_ring_neg_volume").Count == 0;
        bool b021RestoreOk = onlyValidSlot && badRingsDropped;
        GD.Print($"[Stage6Sim] 4-RestoreState: валидный слот ×10 → {goodContents.Count == 1}, " +
                 $"битые кольца (Tier<0, MaxVolume≤0) → {badRingsDropped} " +
                 $"→ {(b021RestoreOk ? "OK" : "FAIL")}");
        pass &= b021RestoreOk;

        // Переполненный стак (500) не в рабочем состоянии → извлекаем не
        // больше допустимого: 10 извлечений по 1, 11-й отказ, счёт не > 10.
        int retrieved = 0;
        for (int i = 0; i < 15; i++)
        {
            if (_storageRing.TryRetrieve("qa_ring_good", ore.ItemId, out _, out _))
                retrieved++;
        }
        bool noOverretrieve = retrieved == 10;
        GD.Print($"[Stage6Sim] 4-TryRetrieve: извлечено {retrieved} из сохранённых 10 " +
                 $"(переполненный 500-стак в кольцо не попал) → {(noOverretrieve ? "OK" : "FAIL")}");
        pass &= noOverretrieve;

        // ════════════════════════════════════════════════════════════════
        // Итог
        // ════════════════════════════════════════════════════════════════
        GD.Print($"[Stage6Sim] VERDICT: {(pass
            ? "PASS — BUG-018/019/020/021: инварианты стаков, честные Delta/Price, непредставимые котировки, валидация колец"
            : "FAIL")}");
    }

    // === Helpers ===

    private Godot.SignalAwaiter Wait(double seconds) =>
        ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    /// <summary>Руда для секций продажи/колец: каталог уже содержит её после генерации стока.</summary>
    private ItemData? EnsureOre()
    {
        if (_itemDb!.TryGetItem("material_iron_ore", out var existing))
            return existing;
        var ore = new ItemData
        {
            ItemId = "material_iron_ore", NameRu = "Железная руда",
            Category = ItemCategory.Material, Stackable = true, MaxStack = 99,
            Weight = 1.5f, Volume = 1.0f, Value = 5,
        };
        _itemDb.Register(ore);
        return ore;
    }
}
