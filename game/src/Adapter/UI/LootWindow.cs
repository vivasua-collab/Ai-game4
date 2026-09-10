#nullable enable
// Создано: 2026-09-10 — R13 FULL-LOOT: окно обыска трупа (LootWindow).
// Модальное окно 760×520 по центру: слева содержимое трупа (ЛКМ — взять
// предмет; группы: экипировка/карманы/духовные камни), справа — инвентарь
// игрока (контекст, что ляжет сверху). «Забрать всё» — full loot одной
// кнопкой. Esc закрывает (GameWorldController).
//
// Backend: Modules/NPC/CorpseService (ICorpseService). Взятие предмета —
// SlotId-адресно (паттерн R10 TOCTOU-защиты): строка строки помнит Guid
// слота, повторный клик по устаревшей строке НЕ возьмёт чужой предмет.
// Пауза/резюм тиков — GameWorldController (единая авторитетная точка,
// как DialogueWindow/TradeWindow).
//
// Паттерны: TradeWindow (layout/тосты/строки), DialogueWindow (IsOpen+Esc).
using Godot;
using System.Collections.Generic;
using System.Linq;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;
using CoreContracts = CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Adapter.UI;

/// <summary>
/// Окно обыска трупа (R13 FULL-LOOT). Открывается GameWorldController по E
/// рядом с трупом (ICorpseService.FindNearestCorpse), закрывается по Esc или
/// кнопке «Уйти». Пока открыто — тики на паузе (обыск = планирование).
/// Обновляется по CorpseLootedEvent (содержимое изменилось) и
/// ItemAddedEvent (инвентарь игрока изменился).
/// </summary>
public partial class LootWindow : Control
{
    [Inject] private ICorpseService Corpses = null!;
    [Inject] private IInventoryService Inventory = null!;
    [Inject] private IItemDatabaseService ItemDb = null!;

    [Inject] private Core.Events.ISubscriber<CoreContracts.CorpseLootedEvent> LootedSub = null!;
    [Inject] private Core.Events.ISubscriber<CoreContracts.ItemAddedEvent> ItemAddedSub = null!;
    [Inject] private Core.Events.IPublisher<CoreContracts.ToastShownEvent> ToastPub = null!;

    private Panel _panel = null!;
    private Label _headerLabel = null!;
    private Label _corpseSummary = null!;
    private Label _inventorySummary = null!;
    private VBoxContainer _corpseList = null!;
    private VBoxContainer _inventoryList = null!;
    private Label _footerLabel = null!;
    private Button _lootAllButton = null!;

    private string _corpseId = string.Empty;

    private System.IDisposable? _lootedToken;
    private System.IDisposable? _itemAddedToken;

    /// <summary>Окно открыто (modalOpen-гарды GameWorldController).</summary>
    public bool IsOpen => Visible;

    // === Диагностика (internal: headless-QA GODOT_LOOT_DEBUG=1) ===

    /// <summary>Число строк содержимого трупа (QA).</summary>
    internal int CorpseRowCount => _corpseList?.GetChildren()
        .OfType<LootItemRow>().Count(r => !r.IsQueuedForDeletion()) ?? 0;

    /// <summary>Снимок строк трупа «itemId|count|source» (QA).</summary>
    internal IReadOnlyList<string> DebugCorpseRows { get; } = new List<string>();

    /// <summary>Текст кнопки «Забрать всё» (QA: счётчик записей).</summary>
    internal string DebugLootAllText => _lootAllButton?.Text ?? "";

    /// <summary>Текст шапки окна (QA: имя трупа).</summary>
    internal string DebugHeaderText => _headerLabel?.Text ?? "";

    /// <summary>Текст футера (QA: упоминает Esc и ЛКМ).</summary>
    internal string DebugFooterText => _footerLabel?.Text ?? "";

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        BuildUI();
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;

        // Обновления по событиям (токены диспозятся в _ExitTree).
        _lootedToken = LootedSub?.Subscribe(OnCorpseLooted);
        _itemAddedToken = ItemAddedSub?.Subscribe(OnItemAdded);

        GD.Print("[LootWindow] Ready");
    }

    public override void _ExitTree()
    {
        _lootedToken?.Dispose();
        _itemAddedToken?.Dispose();
        _lootedToken = _itemAddedToken = null;
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
        bg.MouseFilter = MouseFilterEnum.Stop; // клик по подложке НЕ закрывает окно (только Esc/«Уйти»)
        AddChild(bg);

        // Главная панель: 760×520 по центру.
        _panel = new Panel { Name = "LootPanel" };
        _panel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        _panel.OffsetLeft = -380;
        _panel.OffsetRight = 380;
        _panel.OffsetTop = -260;
        _panel.OffsetBottom = 260;
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

        // Шапка: «☠ Обыск: Имя» + подсказка.
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 12);
        outer.AddChild(header);

        _headerLabel = new Label
        {
            Text = "☠ Обыск",
            HorizontalAlignment = HorizontalAlignment.Left,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _headerLabel.AddThemeFontSizeOverride("font_size", 22);
        _headerLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkBlack);
        header.AddChild(_headerLabel);

        outer.AddChild(new HSeparator());

        // Контент: слева содержимое трупа, справа инвентарь игрока.
        var content = new HBoxContainer
        {
            Name = "ContentRow",
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        content.AddThemeConstantOverride("separation", 12);
        outer.AddChild(content);

        // ── Левая колонка: содержимое трупа ──
        var left = new VBoxContainer { Name = "CorpseColumn", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        left.AddThemeConstantOverride("separation", 4);
        content.AddChild(left);

        _corpseSummary = new Label
        {
            Text = "Содержимое (ЛКМ — взять)",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        _corpseSummary.AddThemeFontSizeOverride("font_size", 13);
        _corpseSummary.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        left.AddChild(_corpseSummary);

        var corpseScroll = new ScrollContainer
        {
            Name = "CorpseScroll",
            CustomMinimumSize = new Vector2(340, 360),
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        left.AddChild(corpseScroll);

        _corpseList = new VBoxContainer
        {
            Name = "CorpseList",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _corpseList.AddThemeConstantOverride("separation", 3);
        corpseScroll.AddChild(_corpseList);

        // ── Правая колонка: инвентарь игрока (контекст) ──
        var right = new VBoxContainer { Name = "InventoryColumn", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 4);
        content.AddChild(right);

        _inventorySummary = new Label
        {
            Text = "Твой инвентарь",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        _inventorySummary.AddThemeFontSizeOverride("font_size", 13);
        _inventorySummary.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        right.AddChild(_inventorySummary);

        var invScroll = new ScrollContainer
        {
            Name = "InventoryScroll",
            CustomMinimumSize = new Vector2(340, 360),
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

        // Кнопки: «Забрать всё» (full loot) + «Уйти».
        var buttons = new HBoxContainer
        {
            Name = "ButtonsRow",
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        buttons.AddThemeConstantOverride("separation", 16);
        outer.AddChild(buttons);

        _lootAllButton = new Button
        {
            Name = "LootAllButton",
            Text = "✦ Забрать всё (0)",
        };
        _lootAllButton.AddThemeFontSizeOverride("font_size", 16);
        _lootAllButton.AddThemeColorOverride("font_color", ParchmentTheme.AccentGold);
        _lootAllButton.Pressed += OnLootAllPressed;
        buttons.AddChild(_lootAllButton);

        var leaveButton = new Button
        {
            Name = "LeaveButton",
            Text = "Уйти",
        };
        leaveButton.AddThemeFontSizeOverride("font_size", 16);
        leaveButton.Pressed += Close;
        buttons.AddChild(leaveButton);

        // Подвал: подсказка управления.
        _footerLabel = new Label
        {
            Text = "ЛКМ — взять предмет · «Забрать всё» — полный лут · Esc — уйти",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _footerLabel.AddThemeFontSizeOverride("font_size", 12);
        _footerLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        outer.AddChild(_footerLabel);
    }

    // === Открытие/закрытие ===

    /// <summary>
    /// Открыть окно обыска трупа. Вызывает GameWorldController (E рядом с
    /// трупом); сам контроллер ставит паузу тиков (авторитетная точка).
    /// </summary>
    public void Open(string corpseId)
    {
        var corpse = Corpses?.GetCorpse(corpseId);
        if (corpse == null)
        {
            GD.Print($"[LootWindow] Open: труп '{corpseId}' не найден");
            return;
        }

        _corpseId = corpseId;
        _panel.Name = $"LootPanel_{corpseId}";
        // Шапка: имя погибшего (QA: проверка DebugHeaderText) + уровень в скобках.
        _headerLabel.Text = corpse.NpcLevel > 0
            ? $"☠ Обыск: {corpse.DisplayName} (L{corpse.NpcLevel})"
            : $"☠ Обыск: {corpse.DisplayName}";
        Visible = true;
        MouseFilter = MouseFilterEnum.Stop;
        RefreshAll();
        GD.Print($"[LootWindow] Opened — обыск '{corpseId}'");
    }

    /// <summary>Закрыть окно (Esc/кнопка «Уйти» — вызывает GameWorldController).</summary>
    public void Close()
    {
        if (!Visible) return;
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        _corpseId = string.Empty;
        GD.Print("[LootWindow] Closed");
    }

    // === Действия ===

    private void OnLootAllPressed()
    {
        if (string.IsNullOrEmpty(_corpseId)) return;

        int taken = Corpses?.LootAll(_corpseId) ?? 0;
        if (taken > 0)
        {
            PublishToast($"☠ Полный лут: {taken} позиций → инвентарь");
            // RefreshAll придёт по CorpseLootedEvent; если труп опустел и
            // удалён — окно закрывает GameWorldController по CorpseRemovedEvent.
        }
        else
        {
            PublishToast("Труп уже пуст");
        }
        RefreshAll();
    }

    /// <summary>Взять один предмет (вызов из LootItemRow, ЛКМ по строке).</summary>
    internal void HandleTake(System.Guid slotId)
    {
        if (string.IsNullOrEmpty(_corpseId)) return;

        if (Corpses?.TryTakeItem(_corpseId, slotId) != true)
            PublishToast("Предмет уже взят");
        // RefreshAll придёт по CorpseLootedEvent.
    }

    // === Обновление по событиям ===

    private void OnCorpseLooted(in CoreContracts.CorpseLootedEvent e)
    {
        if (!Visible) return;
        if (e.CorpseId != _corpseId) return; // чужой труп — не наш случай
        RefreshAll();
    }

    private void OnItemAdded(in CoreContracts.ItemAddedEvent e)
    {
        if (!Visible) return;
        RefreshInventory();
    }

    // === Обновление ===

    private void RefreshAll()
    {
        RefreshCorpse();
        RefreshInventory();
    }

    private void RefreshCorpse()
    {
        foreach (var child in _corpseList.GetChildren())
            child.QueueFree();
        ((List<string>)DebugCorpseRows).Clear();

        var corpse = Corpses?.GetCorpse(_corpseId);
        if (corpse == null || corpse.IsEmpty)
        {
            _corpseList.AddChild(BuildEmptyLabel("Труп пуст"));
            _corpseSummary.Text = "Содержимое — пусто";
            _lootAllButton.Text = "✦ Забрать всё (0)";
            _lootAllButton.Disabled = true;
            return;
        }

        // Группировка: экипировка → карманы → духовные камни (важное сверху).
        var ordered = corpse.Items
            .OrderBy(i => SourceOrder(i.Source))
            .ThenBy(i => i.ItemId);

        int positions = 0, units = 0;
        foreach (var item in ordered)
        {
            positions++;
            units += item.Count;
            _corpseList.AddChild(new LootItemRow(item, this));
            ((List<string>)DebugCorpseRows).Add($"{item.ItemId}|{item.Count}|{item.Source}");
        }

        _corpseSummary.Text = $"Содержимое — {positions} поз. / {units} шт. (ЛКМ — взять)";
        _lootAllButton.Text = $"✦ Забрать всё ({positions})";
        _lootAllButton.Disabled = false;
    }

    private static int SourceOrder(string source) => source switch
    {
        "equipment" => 0,
        "spirit_stones" => 1,
        "inventory" => 2,
        _ => 3,
    };

    private void RefreshInventory()
    {
        foreach (var child in _inventoryList.GetChildren())
            child.QueueFree();

        var slots = Inventory?.GetAllSlots();
        if (slots == null || slots.Count == 0)
        {
            _inventoryList.AddChild(BuildEmptyLabel("◇ Инвентарь пуст"));
            _inventorySummary.Text = "Твой инвентарь";
            return;
        }

        int positions = 0, units = 0;
        foreach (var slot in slots)
        {
            if (slot.Count <= 0) continue;
            positions++;
            units += slot.Count;
            _inventoryList.AddChild(new LootItemRow(
                new CorpseItem(slot.SlotId, slot.ItemId, slot.Count, slot.Rarity, "player"),
                this, readOnly: true));
        }

        if (positions == 0)
            _inventoryList.AddChild(BuildEmptyLabel("◇ Инвентарь пуст"));

        float weight = Inventory?.GetCurrentWeight() ?? 0f;
        float maxWeight = Inventory?.GetEffectiveMaxWeight() ?? 50f;
        _inventorySummary.Text = $"Твой инвентарь — {positions} поз. / {units} шт. · {weight:F1}/{maxWeight:F0} кг";
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

    internal string GetItemName(string itemId)
    {
        if (ItemDb != null && ItemDb.TryGetItem(itemId, out var item))
            return item.NameRu;
        return itemId;
    }

    internal string GetItemDescription(string itemId)
    {
        if (ItemDb != null && ItemDb.TryGetItem(itemId, out var item)
            && !string.IsNullOrEmpty(item.Description))
            return item.Description;
        return "Описание отсутствует";
    }

    internal Godot.Color GetItemRowColor(string itemId)
    {
        if (ItemDb != null && ItemDb.TryGetItem(itemId, out var item))
            return CharacterDollPanel.GetRarityColor(item.Rarity);
        return ParchmentTheme.InkBlack;
    }

    /// <summary>Тост (показывается GameWorldController по ToastShownEvent).</summary>
    private void PublishToast(string message)
        => ToastPub?.Publish(new CoreContracts.ToastShownEvent(message, 2.5f));
}

/// <summary>
/// Одна строка содержимого: предмет трупа (readOnly=false, ЛКМ — взять по
/// SlotId-адресу) или предмет инвентаря игрока (readOnly=true, без клика).
/// </summary>
public partial class LootItemRow : HBoxContainer
{
    private static readonly Godot.Color HoverTint = new(1.08f, 1.05f, 0.9f);

    private readonly CorpseItem _item;
    private readonly LootWindow _parent;
    private readonly bool _readOnly;

    /// <summary>SlotId строки (QA: адресная идентификация).</summary>
    public System.Guid SlotId => _item.SlotId;

    /// <summary>ItemId строки (QA).</summary>
    public string ItemId => _item.ItemId;

    public LootItemRow(CorpseItem item, LootWindow parent, bool readOnly = false)
    {
        _item = item;
        _parent = parent;
        _readOnly = readOnly;

        Name = readOnly
            ? $"LootRow_Player_{item.ItemId}"
            : $"LootRow_{item.Source}_{item.ItemId}";
        MouseFilter = readOnly ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;

        AddThemeConstantOverride("separation", 10);

        string displayName = _parent.GetItemName(item.ItemId);
        Godot.Color nameColor = _parent.GetItemRowColor(item.ItemId);

        // Индикатор редкости (как TradeItemRow/InventoryItemRow).
        var indicator = new ColorRect
        {
            Color = nameColor,
            CustomMinimumSize = new Vector2(6, 22),
        };

        var nameLabel = new Label
        {
            Text = displayName,
            CustomMinimumSize = new Vector2(200, 22),
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 14);
        nameLabel.AddThemeColorOverride("font_color", nameColor);

        var qtyLabel = new Label
        {
            Text = $"×{item.Count}",
            CustomMinimumSize = new Vector2(52, 22),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        qtyLabel.AddThemeFontSizeOverride("font_size", 14);
        qtyLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);

        // Слотовая метка происхождения: «ношеное» / «карманы» / «камни» / «—».
        var sourceLabel = new Label
        {
            Text = SourceLabelRu(item.Source),
            CustomMinimumSize = new Vector2(70, 22),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        sourceLabel.AddThemeFontSizeOverride("font_size", 12);
        sourceLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);

        TooltipText = $"{displayName}\n{_parent.GetItemDescription(item.ItemId)}";

        AddChild(indicator);
        AddChild(nameLabel);
        AddChild(qtyLabel);
        AddChild(sourceLabel);

        if (!readOnly)
        {
            MouseEntered += () => Modulate = HoverTint;
            MouseExited += () => Modulate = Colors.White;
        }
    }

    private static string SourceLabelRu(string source) => source switch
    {
        "equipment" => "ношеное",
        "inventory" => "карманы",
        "spirit_stones" => "камни",
        "player" => "твоё",
        _ => "—",
    };

    /// <summary>ЛКМ — взять предмет (только для строк трупа).</summary>
    public override void _GuiInput(InputEvent @event)
    {
        if (_readOnly) return;
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            _parent.HandleTake(_item.SlotId);
            GetViewport().SetInputAsHandled();
        }
    }
}
