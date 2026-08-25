#nullable enable
// Создано: 2026-08-25 — NPC_COMBAT_PREP Phase 5: окно торговли (лавка торговца).
// TradeWindow — модальное окно 900×600 по центру: слева ассортимент торговца
// (ЛКМ — купить), справа инвентарь игрока (ЛКМ — продать), сверху баланс
// духовных камней, снизу подсказка. Esc закрывает (GameWorldController).
//
// Backend: Modules/Trade/TradeService (события TradeContracts.cs).
// Пауза/резюм тиков — GameWorldController по TradeOpened/ClosedEvent
// (единая авторитетная точка резюма, как DialogueEndedEvent для диалогов).
//
// Паттерны: InventoryWindow (layout/тосты), DialogueWindow (IsOpen + Esc в
// контроллере), BeltSlotRow (подписки + _ExitTree dispose).
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;
using CoreContracts = CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Adapter.UI;

/// <summary>
/// Торговое окно (лавка). Открывается по TradeOpenedEvent (после выбора
/// «Покажи товары» в диалоге с торговцем), закрывается по TradeClosedEvent
/// или Esc (через GameWorldController → Close()). Пока окно открыто,
/// тики на паузе (ставит/снимает GameWorldController по тем же событиям).
/// </summary>
public partial class TradeWindow : Control
{
    [Inject] private ITradeService Trade = null!;
    [Inject] private ICurrencyService Currency = null!;
    [Inject] private IInventoryService Inventory = null!;
    [Inject] private IItemDatabaseService ItemDb = null!;
    [Inject] private INPCService NpcService = null!;

    [Inject] private Core.Events.ISubscriber<CoreContracts.TradeOpenedEvent> OpenedSub = null!;
    [Inject] private Core.Events.ISubscriber<CoreContracts.TradeClosedEvent> ClosedSub = null!;
    [Inject] private Core.Events.ISubscriber<CoreContracts.TradeCompletedEvent> CompletedSub = null!;
    [Inject] private Core.Events.ISubscriber<CoreContracts.TradeFailedEvent> FailedSub = null!;
    [Inject] private Core.Events.ISubscriber<CoreContracts.CurrencyChangedEvent> CurrencySub = null!;
    [Inject] private Core.Events.IPublisher<CoreContracts.ToastShownEvent> ToastPub = null!;

    private Panel _panel = null!;
    private Label _balanceLabel = null!;
    private VBoxContainer _stockList = null!;
    private VBoxContainer _inventoryList = null!;
    private Label _stockSummary = null!;
    private Label _inventorySummary = null!;

    private string _merchantId = string.Empty;
    private string _merchantName = string.Empty;

    private System.IDisposable? _openedToken;
    private System.IDisposable? _closedToken;
    private System.IDisposable? _completedToken;
    private System.IDisposable? _failedToken;
    private System.IDisposable? _currencyToken;

    /// <summary>Окно открыто (для modalOpen-гардов GameWorldController).</summary>
    public bool IsOpen => Visible;

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        BuildUI();
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;

        // Подписки на события торговли (токены диспозятся в _ExitTree).
        _openedToken = OpenedSub?.Subscribe(OnTradeOpened);
        _closedToken = ClosedSub?.Subscribe(OnTradeClosed);
        _completedToken = CompletedSub?.Subscribe(OnTradeCompleted);
        _failedToken = FailedSub?.Subscribe(OnTradeFailed);
        _currencyToken = CurrencySub?.Subscribe(OnCurrencyChanged);

        GD.Print("[TradeWindow] Ready");
    }

    public override void _ExitTree()
    {
        _openedToken?.Dispose();
        _closedToken?.Dispose();
        _completedToken?.Dispose();
        _failedToken?.Dispose();
        _currencyToken?.Dispose();
        _openedToken = _closedToken = _completedToken = _failedToken = _currencyToken = null;
    }

    // === Layout ===

    private void BuildUI()
    {
        Theme = ParchmentTheme.Create();

        // Полноэкранный оверлей (тёмная подложка съедает клики — мир не реагирует).
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var bg = new ColorRect
        {
            Name = "Background",
            Color = new Color(0.05f, 0.03f, 0.02f, 0.7f),
        };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Stop; // клик по подложке НЕ закрывает лавку (только Esc)
        AddChild(bg);

        // Главная панель: 900×600 по центру (как InventoryWindow 880×560).
        _panel = new Panel { Name = "TradePanel" };
        _panel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        _panel.OffsetLeft = -450;
        _panel.OffsetRight = 450;
        _panel.OffsetTop = -300;
        _panel.OffsetBottom = 300;
        _panel.MouseFilter = MouseFilterEnum.Stop;
        AddChild(_panel);

        // Внешний VBox: шапка / контент / подвал.
        var outer = new VBoxContainer();
        outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        outer.OffsetLeft = 16;
        outer.OffsetRight = -16;
        outer.OffsetTop = 12;
        outer.OffsetBottom = -12;
        outer.AddThemeConstantOverride("separation", 8);
        _panel.AddChild(outer);

        // Шапка: название лавки + баланс духовных камней.
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 12);
        outer.AddChild(header);

        var titleLabel = new Label
        {
            Text = "◆ Лавка торговца ◆",
            HorizontalAlignment = HorizontalAlignment.Left,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 24);
        titleLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkBlack);
        header.AddChild(titleLabel);

        _balanceLabel = new Label
        {
            Text = "Духовные камни: 0",
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        _balanceLabel.AddThemeFontSizeOverride("font_size", 18);
        _balanceLabel.AddThemeColorOverride("font_color", ParchmentTheme.AccentGold);
        header.AddChild(_balanceLabel);

        outer.AddChild(new HSeparator());

        // Контент: слева товары торговца, справа инвентарь игрока.
        var content = new HBoxContainer
        {
            Name = "ContentRow",
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        content.AddThemeConstantOverride("separation", 12);
        outer.AddChild(content);

        // ── Левая колонка: ассортимент торговца ──
        var left = new VBoxContainer { Name = "StockColumn", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        left.AddThemeConstantOverride("separation", 4);
        content.AddChild(left);

        _stockSummary = new Label
        {
            Text = "Товары (ЛКМ — купить)",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        _stockSummary.AddThemeFontSizeOverride("font_size", 13);
        _stockSummary.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        left.AddChild(_stockSummary);

        var stockScroll = new ScrollContainer
        {
            Name = "StockScroll",
            CustomMinimumSize = new Vector2(410, 420),
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        left.AddChild(stockScroll);

        _stockList = new VBoxContainer
        {
            Name = "StockList",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _stockList.AddThemeConstantOverride("separation", 3);
        stockScroll.AddChild(_stockList);

        // ── Правая колонка: инвентарь игрока (продажа) ──
        var right = new VBoxContainer { Name = "InventoryColumn", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 4);
        content.AddChild(right);

        _inventorySummary = new Label
        {
            Text = "Инвентарь (ЛКМ — продать)",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        _inventorySummary.AddThemeFontSizeOverride("font_size", 13);
        _inventorySummary.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        right.AddChild(_inventorySummary);

        var invScroll = new ScrollContainer
        {
            Name = "InventoryScroll",
            CustomMinimumSize = new Vector2(410, 420),
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        right.AddChild(invScroll);

        _inventoryList = new VBoxContainer
        {
            Name = "InventoryList",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _inventoryList.AddThemeConstantOverride("separation", 3);
        invScroll.AddChild(_inventoryList);

        // Подвал: подсказка управления.
        var footer = new Label
        {
            Text = "ЛКМ — купить/продать 1 · Shift+ЛКМ — 5 · Esc — закрыть",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        footer.AddThemeFontSizeOverride("font_size", 12);
        footer.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        outer.AddChild(footer);
    }

    // === Открытие/закрытие (по событиям шины) ===

    private void OnTradeOpened(in CoreContracts.TradeOpenedEvent e)
    {
        _merchantId = e.NpcId;
        _merchantName = ResolveNpcName(e.NpcId);
        _panel.Name = $"TradePanel_{e.NpcId}";
        Visible = true;
        MouseFilter = MouseFilterEnum.Stop;
        RefreshAll();
        GD.Print($"[TradeWindow] Opened — лавка '{_merchantName}' ({e.NpcId})");
    }

    private void OnTradeClosed(in CoreContracts.TradeClosedEvent e)
    {
        if (!Visible) return;
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        _merchantId = string.Empty;
        _merchantName = string.Empty;
        GD.Print("[TradeWindow] Closed");
    }

    /// <summary>
    /// Закрыть лавку (Esc — вызывает GameWorldController). Сервис закрывает
    /// сессию → TradeClosedEvent скроет окно и резюмит тики. Прямое скрытие —
    /// страховка на случай, если сессия уже закрыта.
    /// </summary>
    public void Close()
    {
        if (Trade is { IsTrading: true })
            Trade.CloseTrade();
        else
            OnTradeClosed(default);
    }

    // === Сделки ===

    private void OnTradeCompleted(in CoreContracts.TradeCompletedEvent e)
    {
        string name = ResolveItemName(e.ItemId);
        PublishToast(e.IsPurchase
            ? $"Куплено: {name} ×{e.Count} (−{e.Price} 🔶)"
            : $"Продано: {name} ×{e.Count} (+{e.Price} 🔶)");
        RefreshAll();
    }

    private void OnTradeFailed(in CoreContracts.TradeFailedEvent e)
    {
        if (!string.IsNullOrEmpty(e.Reason))
            PublishToast($"⚠ {e.Reason}");
        RefreshAll();
    }

    private void OnCurrencyChanged(in CoreContracts.CurrencyChangedEvent e)
    {
        RefreshBalance();
    }

    internal void HandleBuy(string itemId, int count)
    {
        if (Trade == null || string.IsNullOrEmpty(_merchantId)) return;
        Trade.TryBuy(_merchantId, itemId, count);
        // Обновление списков/баланса — по TradeCompleted/TradeFailedEvent.
    }

    internal void HandleSell(string itemId, int count)
    {
        if (Trade == null || string.IsNullOrEmpty(_merchantId)) return;
        Trade.TrySell(_merchantId, itemId, count);
    }

    // === Обновление ===

    private void RefreshAll()
    {
        RefreshBalance();
        RefreshStock();
        RefreshInventory();
    }

    private void RefreshBalance()
    {
        int stones = Currency?.SpiritStones ?? 0;
        _balanceLabel.Text = $"Духовные камни: {stones}";
    }

    private void RefreshStock()
    {
        foreach (var child in _stockList.GetChildren())
            child.QueueFree();

        var stock = Trade?.GetMerchantStock(_merchantId);
        if (stock is not { Count: > 0 })
        {
            _stockList.AddChild(BuildEmptyLabel("Товары закончились"));
            _stockSummary.Text = "Товары (ЛКМ — купить)";
            return;
        }

        int positions = 0;
        int totalUnits = 0;
        foreach (var entry in stock)
        {
            if (entry.Count <= 0) continue;
            positions++;
            totalUnits += entry.Count;

            int price = Trade!.GetBuyPrice(entry.ItemId);
            _stockList.AddChild(new TradeItemRow(
                entry.ItemId, entry.Count, price, isBuy: true, this));
        }

        if (positions == 0)
            _stockList.AddChild(BuildEmptyLabel("Товары закончились"));

        _stockSummary.Text = $"Товары — {positions} поз. / {totalUnits} шт. (ЛКМ — купить)";
    }

    private void RefreshInventory()
    {
        foreach (var child in _inventoryList.GetChildren())
            child.QueueFree();

        var slots = Inventory?.GetAllSlots();
        if (slots == null || slots.Count == 0)
        {
            _inventoryList.AddChild(BuildEmptyLabel("◇ Инвентарь пуст"));
            _inventorySummary.Text = "Инвентарь (ЛКМ — продать)";
            return;
        }

        int positions = 0;
        foreach (var slot in slots)
        {
            if (slot.Count <= 0) continue;
            positions++;
            int price = Trade?.GetSellPrice(slot.ItemId) ?? 0;
            _inventoryList.AddChild(new TradeItemRow(
                slot.ItemId, slot.Count, price, isBuy: false, this));
        }

        if (positions == 0)
            _inventoryList.AddChild(BuildEmptyLabel("◇ Инвентарь пуст"));

        _inventorySummary.Text = $"Инвентарь — {positions} поз. (ЛКМ — продать)";
    }

    // === Вспомогательные ===

    private static Label BuildEmptyLabel(string text)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        label.AddThemeFontSizeOverride("font_size", 16);
        label.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        return label;
    }

    private string ResolveItemName(string itemId)
    {
        if (ItemDb != null && ItemDb.TryGetItem(itemId, out var item))
            return item.NameRu;
        return itemId;
    }

    private string ResolveNpcName(string npcId)
    {
        // Как DialogueWindow.Open: DisplayName + роль (fallback — npcId).
        var npc = NpcService?.GetNPC(npcId);
        return npc != null ? $"{npc.DisplayName}" : npcId;
    }

    /// <summary>Тост (показывается GameWorldController по ToastShownEvent).</summary>
    private void PublishToast(string message)
    {
        ToastPub?.Publish(new CoreContracts.ToastShownEvent(message, 2.5f));
    }

    /// <summary>Название предмета для строки (резолв через БД окна).</summary>
    internal string GetItemName(string itemId) => ResolveItemName(itemId);

    /// <summary>Цвет редкости предмета для строки (fallback — чернила).</summary>
    internal Godot.Color GetItemRowColor(string itemId)
    {
        if (ItemDb != null && ItemDb.TryGetItem(itemId, out var item))
            return CharacterDollPanel.GetRarityColor(item.Rarity);
        return ParchmentTheme.InkBlack;
    }
}

/// <summary>
/// Одна строка лавки: товар торговца (isBuy=true, ЛКМ — покупка) или предмет
/// инвентаря игрока (isBuy=false, ЛКМ — продажа). Shift+ЛКМ — партия из 5.
/// </summary>
public partial class TradeItemRow : HBoxContainer
{
    private const int BatchCount = 5;

    private readonly string _itemId;
    private readonly bool _isBuy;
    private readonly TradeWindow _parent;

    public TradeItemRow(string itemId, int count, int price, bool isBuy, TradeWindow parent)
    {
        _itemId = itemId;
        _isBuy = isBuy;
        _parent = parent;

        Name = $"Trade_{(isBuy ? "Buy" : "Sell")}_{itemId}";
        MouseFilter = MouseFilterEnum.Stop;
        TooltipText = $"{itemId} · цена за 1 шт.: {price}";

        AddThemeConstantOverride("separation", 10);

        string displayName = _parent.GetItemName(itemId);
        Godot.Color nameColor = _parent.GetItemRowColor(itemId);

        // Индикатор редкости (как InventoryItemRow).
        var indicator = new ColorRect
        {
            Color = nameColor,
            CustomMinimumSize = new Vector2(6, 22),
        };

        var nameLabel = new Label
        {
            Text = displayName,
            CustomMinimumSize = new Vector2(230, 22),
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 14);
        nameLabel.AddThemeColorOverride("font_color", nameColor);

        var qtyLabel = new Label
        {
            Text = $"×{count}",
            CustomMinimumSize = new Vector2(56, 22),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        qtyLabel.AddThemeFontSizeOverride("font_size", 14);
        qtyLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);

        var priceLabel = new Label
        {
            Text = $"🔶 {price}",
            CustomMinimumSize = new Vector2(90, 22),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        priceLabel.AddThemeFontSizeOverride("font_size", 14);
        priceLabel.AddThemeColorOverride("font_color", ParchmentTheme.AccentGold);

        AddChild(indicator);
        AddChild(nameLabel);
        AddChild(qtyLabel);
        AddChild(priceLabel);
    }

    /// <summary>ЛКМ — сделка на 1 шт., Shift+ЛКМ — на партию (5).</summary>
    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
        {
            int count = mb.ShiftPressed ? BatchCount : 1;
            if (_isBuy)
                _parent.HandleBuy(_itemId, count);
            else
                _parent.HandleSell(_itemId, count);
            GetViewport().SetInputAsHandled();
        }
    }
}
