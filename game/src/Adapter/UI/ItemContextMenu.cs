#nullable enable
// Создано: 2026-09-09 — ПКМ-контекстное меню инвентаря (запрос пользователя):
//   • ЛКМ по-прежнему drag&drop/двойной клик, но ПКМ на предмете открывает
//     окно свойств (характеристики предмета всех категорий).
//   • Для стакающихся предметов (Stackable && Count > 1) — кнопка
//     «Разделить стак…», открывающая классический диалог деления:
//     слайдер с «−»/«+» по краям, два числа (слева — сколько останется
//     в исходном стке, справа — сколько уйдёт в новую кучку).
//   • «Выбросить стак» — выбрасывает ТОЛЬКО этот слот (кучку), а не все
//     предметы типа (важно при множественных кучках одного ItemId).
//   • Будущее (планы): алхимия — точные количества вещества из кучки.
using Godot;
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Adapter.UI;

/// <summary>
/// Контекстное окно свойств предмета (ПКМ на строке инвентаря).
/// Полупрозрачный фон поглощает клики: клик мимо панели = закрыть.
/// Панель позиционируется возле курсора (с клампом в границы окна).
/// </summary>
public partial class ItemContextMenu : Control
{
    private readonly InventoryWindow _parent;
    private readonly InventorySlot _slot;
    private readonly ItemData _item;

    private Panel _panel = null!;

    // === QA-акцессоры (GODOT_CONTEXT_DEBUG, №26) ===
    public Button? UseButtonForQA { get; private set; }
    public Button? SplitButtonForQA { get; private set; }
    public Button? DropButtonForQA { get; private set; }
    /// <summary>R10 P1-SlotId: стабильная идентичность кучки (действия меню).</summary>
    public Guid SlotIdForQA => _slot.SlotId;
    public int PropertyCountForQA { get; private set; }

    public ItemContextMenu(InventoryWindow parent, InventorySlot slot, ItemData item)
    {
        _parent = parent;
        _slot = slot;
        _item = item;
        Name = "ItemContextMenu";
    }

    public override void _Ready()
    {
        Theme = ParchmentTheme.Create();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        // Затемнение — клик мимо панели закрывает меню.
        var backdrop = new ColorRect
        {
            Name = "Backdrop",
            Color = new Color(0f, 0f, 0f, 0.25f),
        };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        backdrop.MouseFilter = MouseFilterEnum.Stop;
        backdrop.GuiInput += OnBackdropInput;
        AddChild(backdrop);

        var rarityColor = CharacterDollPanel.GetRarityColor(_item.Rarity);

        // ── Панель с содержимым ──
        _panel = new Panel
        {
            Name = "MenuPanel",
            CustomMinimumSize = new Vector2(400, 0),
        };
        _panel.MouseFilter = MouseFilterEnum.Stop;
        AddChild(_panel);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        vbox.OffsetLeft = 12;
        vbox.OffsetRight = -12;
        vbox.OffsetTop = 10;
        vbox.OffsetBottom = -10;
        vbox.AddThemeConstantOverride("separation", 4);
        _panel.AddChild(vbox);

        // Заголовок: имя предмета.
        var title = new Label
        {
            Text = _item.NameRu,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 18);
        title.AddThemeColorOverride("font_color", rarityColor);
        vbox.AddChild(title);

        // Подзаголовок: редкость · категория · стак.
        var sub = new Label
        {
            Text = $"{RarityNameRu(_item.Rarity)} · {CategoryNameRu(_item.Category)}" +
                   (_item.Stackable ? $" · стак ×{_slot.Count}/{_item.MaxStack}" : ""),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        sub.AddThemeFontSizeOverride("font_size", 12);
        sub.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        vbox.AddChild(sub);

        vbox.AddChild(new HSeparator());

        // ── Свойства (характеристики) ──
        var props = BuildProperties();
        PropertyCountForQA = props.Count;
        foreach (var (labelText, valueText) in props)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);

            var key = new Label
            {
                Text = labelText,
                CustomMinimumSize = new Vector2(150, 0),
            };
            key.AddThemeFontSizeOverride("font_size", 13);
            key.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
            row.AddChild(key);

            var val = new Label
            {
                Text = valueText,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            val.AddThemeFontSizeOverride("font_size", 13);
            val.AddThemeColorOverride("font_color", ParchmentTheme.InkBlack);
            row.AddChild(val);

            vbox.AddChild(row);
        }

        // Описание (если есть).
        if (!string.IsNullOrEmpty(_item.Description))
        {
            vbox.AddChild(new HSeparator());
            var desc = new Label
            {
                Text = _item.Description,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            desc.AddThemeFontSizeOverride("font_size", 12);
            desc.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
            vbox.AddChild(desc);
        }

        // ── Кнопки действий ──
        vbox.AddChild(new HSeparator());
        var buttons = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        buttons.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(buttons);

        // ⚡ Использовать — только камни Ци (RMB-поведение этапа 7).
        if (_item.Category == ItemCategory.QiStone)
        {
            var useBtn = new Button
            {
                Text = "⚡ Использовать",
                TooltipText = "Поглотить Ци камня (1 шт.)",
            };
            useBtn.Pressed += () =>
            {
                _parent.CloseContextMenu();
                _parent.TryUseQiStone(_item.ItemId);
            };
            buttons.AddChild(useBtn);
            UseButtonForQA = useBtn;
        }

        // ✂ Разделить стак — только для стакающихся ×2+.
        if (_item.Stackable && _slot.Count > 1)
        {
            var splitBtn = new Button
            {
                Text = "✂ Разделить стак…",
                TooltipText = "Отделить часть стака в новую кучку",
            };
            splitBtn.Pressed += () =>
            {
                _parent.CloseContextMenu();
                // R10 P1-SlotId: открытие по стабильной идентичности кучки.
                _parent.OpenSplitDialog(_slot.SlotId);
            };
            buttons.AddChild(splitBtn);
            SplitButtonForQA = splitBtn;
        }

        // 🗑 Выбросить стак — выбрасывает только ЭТОТ слот (кучку).
        var dropBtn = new Button
        {
            Text = "🗑 Выбросить стак",
            TooltipText = _item.Stackable && _slot.Count > 1
                ? $"Выбросить {_slot.Count} шт. этой кучки"
                : "Выбросить на землю рядом с персонажем",
        };
        dropBtn.Pressed += () =>
        {
            _parent.CloseContextMenu();
            // R10 P1-SlotId: выброс ТОЛЬКО этой кучки по стабильному Guid;
            // при stale — отказ внутри DropSlotOnGround (не «весь предмет»).
            _parent.DropSlotOnGround(_slot.SlotId, _item.ItemId);
        };
        buttons.AddChild(dropBtn);
        DropButtonForQA = dropBtn;

        // Позиционирование: возле переданной точки (локальные координаты окна),
        // с клампом в границы. Отложим на один кадр — к моменту _Ready
        // у панели уже есть рассчитанный размер.
        CallDeferred(nameof(PositionPanel));
    }

    /// <summary>Позиционировать панель возле курсора с клампом в границы окна.</summary>
    private void PositionPanel()
    {
        Vector2 anchor = _parent.LastContextMenuPosition;
        Vector2 size = _panel.Size;
        Vector2 windowSize = Size;
        float x = Mathf.Clamp(anchor.X + 12, 4, Mathf.Max(4, windowSize.X - size.X - 4));
        float y = Mathf.Clamp(anchor.Y - size.Y / 2, 4, Mathf.Max(4, windowSize.Y - size.Y - 4));
        _panel.Position = new Vector2(x, y);
    }

    private void OnBackdropInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            _parent.CloseContextMenu();
        }
    }

    // === Формирование списка свойств по категории ===

    private List<(string, string)> BuildProperties()
    {
        var list = new List<(string, string)>();

        // Общие физические свойства (на 1 шт. + итог по стаку).
        int count = _slot.Count;
        list.Add(("Вес", $"{_item.Weight:F2} кг/шт" + (count > 1 ? $" (итог {_item.Weight * count:F1} кг)" : "")));
        list.Add(("Объём", $"{_item.Volume:F2} л/шт" + (count > 1 ? $" (итог {_item.Volume * count:F1} л)" : "")));
        list.Add(("Стоимость", $"{_item.Value} кам.Ци/шт" + (count > 1 ? $" (итог {_item.Value * count})" : "")));

        if (_item.HasDurability)
            list.Add(("Прочность", $"{_item.MaxDurability} (макс.)"));

        if (_item.RequiredCultivationLevel > 0)
            list.Add(("Требует уровень", $"{_item.RequiredCultivationLevel}"));

        // Категорийные свойства.
        switch (_item)
        {
            case EquipmentData eq:
                AddEquipmentProps(list, eq);
                break;
            case QiStoneData stone:
                list.Add(("Размер", StoneSizeNameRu(stone.Size)));
                list.Add(("Запас Ци", $"{stone.QiAmount} ед."));
                list.Add(("Тип Ци", stone.IsChaotic ? "хаотичная (10% риск −10% HP)" : "спокойная"));
                list.Add(("Плотность", "1024 ед./см³ (канон)"));
                break;
        }

        // Эффекты расходников.
        if (_item.Effects is { Count: > 0 })
        {
            foreach (var fx in _item.Effects)
            {
                string fxName = fx.EffectType switch
                {
                    "heal" => "Лечение",
                    "qi_restore" => "Восстановление Ци",
                    "material" => "Материал (не употребляется)",
                    _ => fx.EffectType,
                };
                list.Add(("Эффект", $"{fxName} +{fx.Value}" + (fx.Duration > 0 ? $" ({fx.Duration}с)" : "")));
            }
        }

        // Требования к статам.
        if (_item.StatRequirements is { Count: > 0 })
        {
            foreach (var req in _item.StatRequirements)
                list.Add(("Требует", $"{req.StatName} ≥ {req.MinValue}"));
        }

        return list;
    }

    private static void AddEquipmentProps(List<(string, string)> list, EquipmentData eq)
    {
        list.Add(("Слот", CharacterDollPanel.GetSlotLabel(eq.Slot)));
        list.Add(("Грейд", eq.Grade.ToString()));
        list.Add(("Уровень предмета", $"{eq.ItemLevel}"));

        if (eq.Damage > 0)
        {
            list.Add(("Урон", $"{eq.Damage}"));
            list.Add(("Пробитие брони", $"{eq.Penetration}"));
            list.Add(("Дальность", eq.AttackRange <= 2 ? $"{eq.AttackRange} (ближний)" : $"{eq.AttackRange} (дальний)"));
        }
        if (eq.Defense > 0)
        {
            list.Add(("Защита", $"{eq.Defense}"));
            list.Add(("Покрытие", $"{eq.Coverage:F0}%"));
            if (eq.DamageReduction > 0) list.Add(("Снижение урона", $"{eq.DamageReduction:F0}%"));
        }
        if (eq.DodgeBonus != 0) list.Add(("Уклонение", $"{eq.DodgeBonus:+0;-0}%"));
        if (eq.MoveSpeedPenalty != 0) list.Add(("Скорость хода", $"{eq.MoveSpeedPenalty:+0;-0}%"));
        if (eq.QiFlowPenalty != 0) list.Add(("Проводимость Ци", $"{eq.QiFlowPenalty:+0;-0}%"));
        if (eq.WeightBonus > 0) list.Add(("Бонус веса рюкзака", $"+{eq.WeightBonus:F0} кг"));
        if (eq.VolumeBonus > 0) list.Add(("Бонус объёма рюкзака", $"+{eq.VolumeBonus:F0} л"));
        if (eq.WeightReduction > 0) list.Add(("Снижение веса", $"{eq.WeightReduction:F0}%"));
        if (eq.StorageRingTier > 0) list.Add(("Тир кольца хранения", $"{eq.StorageRingTier}"));
        if (eq.StorageMaxVolume > 0) list.Add(("Объём кольца", $"{eq.StorageMaxVolume:F0} л"));
        if (eq.TechniqueDamageBonus != 0) list.Add(("Урон техник", $"{eq.TechniqueDamageBonus:+0;-0}%"));
        if (eq.QiCostReduction != 0) list.Add(("Цена Ци техник", $"{eq.QiCostReduction:+0;-0}%"));
        if (eq.ChargeSpeedBonus != 0) list.Add(("Скорость заряда", $"{eq.ChargeSpeedBonus:+0;-0}%"));

        if (eq.StatBonuses is { Count: > 0 })
        {
            foreach (var bonus in eq.StatBonuses)
                list.Add(("Бонус", $"{bonus.StatName} {bonus.Value:+0;-0}" + (bonus.IsPercentage ? "%" : "")));
        }
        if (eq.SpecialEffects is { Count: > 0 })
        {
            foreach (var fx in eq.SpecialEffects)
                list.Add(("Особый эффект", $"{fx.EffectName} ({fx.TriggerChance:F0}%)"));
        }
    }

    // === Русские названия enum-ов ===

    private static string CategoryNameRu(ItemCategory c) => c switch
    {
        ItemCategory.Weapon => "Оружие",
        ItemCategory.Armor => "Броня",
        ItemCategory.Accessory => "Аксессуар",
        ItemCategory.Consumable => "Расходник",
        ItemCategory.Material => "Материал",
        ItemCategory.Technique => "Свиток техники",
        ItemCategory.Quest => "Квестовый предмет",
        ItemCategory.QiStone => "Камень Ци",
        _ => "Разное",
    };

    private static string RarityNameRu(ItemRarity r) => r switch
    {
        ItemRarity.Common => "Обычный",
        ItemRarity.Uncommon => "Необычный",
        ItemRarity.Rare => "Редкий",
        ItemRarity.Epic => "Эпический",
        ItemRarity.Legendary => "Легендарный",
        ItemRarity.Mythic => "Мифический",
        _ => "—",
    };

    private static string StoneSizeNameRu(QiStoneSize s) => s switch
    {
        QiStoneSize.Dust => "Пыль (1 см³)",
        QiStoneSize.Pebble => "Галька (8 см³)",
        QiStoneSize.Shard => "Осколок (27 см³)",
        QiStoneSize.Stone => "Камень (64 см³)",
        QiStoneSize.Boulder => "Глыба (125 см³)",
        _ => s.ToString(),
    };
}

/// <summary>
/// Диалог «Разделить стак» — классическое деление ползунком:
///
///   ┌──────────────────────────────────────────────┐
///   │              ✂ Разделить стак                 │
///   │        Камень ×50 → две кучки                 │
///   ├──────────────────────────────────────────────┤
///   │   35           [−]  ═══╪═══════  [+]      15  │
///   │ останется      (ползунок)            отде­лится│
///   ├──────────────────────────────────────────────┤
///   │         [ ✓ Разделить ]   [ Отмена ]          │
///   └──────────────────────────────────────────────┘
///
/// Слайдер показывает, сколько предметов УЙДЁТ в новую кучку (правое число);
/// левое число — сколько останется в исходном стке. Кнопки «−»/«+» по краям
/// слайдера двигают значение по одному. Подтверждение — TrySplitSlot.
/// </summary>
public partial class SplitStackDialog : Control
{
    private readonly InventoryWindow _parent;
    private readonly InventorySlot _slot;
    private readonly ItemData _item;
    private readonly int _total;

    private HSlider _slider = null!;
    private Label _leftLabel = null!;
    private Label _rightLabel = null!;
    private Button _confirmButton = null!;

    // === QA-акцессоры (GODOT_CONTEXT_DEBUG, №26) ===
    public HSlider SliderForQA => _slider;
    public Button MinusButtonForQA { get; private set; } = null!;
    public Button PlusButtonForQA { get; private set; } = null!;
    public Label LeftLabelForQA => _leftLabel;
    public Label RightLabelForQA => _rightLabel;
    public Button ConfirmButtonForQA => _confirmButton;
    /// <summary>Сколько предметов уйдёт в новую кучку (текущее значение слайдера).</summary>
    public int CurrentMoveForQA => (int)_slider.Value;
    public int TotalForQA => _total;
    /// <summary>R10 P1-SlotId: стабильная идентичность исходной кучки.</summary>
    public Guid SlotIdForQA => _slot.SlotId;

    public SplitStackDialog(InventoryWindow parent, InventorySlot slot, ItemData item)
    {
        _parent = parent;
        _slot = slot;
        _item = item;
        _total = slot.Count;
        Name = "SplitStackDialog";
    }

    public override void _Ready()
    {
        Theme = ParchmentTheme.Create();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        // Затемнение фона — клик мимо = отмена.
        var backdrop = new ColorRect
        {
            Name = "Backdrop",
            Color = new Color(0f, 0f, 0f, 0.35f),
        };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        backdrop.MouseFilter = MouseFilterEnum.Stop;
        backdrop.GuiInput += OnBackdropInput;
        AddChild(backdrop);

        // Центрированная панель.
        var panel = new Panel
        {
            Name = "SplitPanel",
            CustomMinimumSize = new Vector2(560, 0),
        };
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        panel.OffsetLeft = -280;
        panel.OffsetRight = 280;
        panel.OffsetTop = -120;
        panel.OffsetBottom = 120;
        panel.MouseFilter = MouseFilterEnum.Stop;
        AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        vbox.OffsetLeft = 16;
        vbox.OffsetRight = -16;
        vbox.OffsetTop = 12;
        vbox.OffsetBottom = -12;
        vbox.AddThemeConstantOverride("separation", 8);
        panel.AddChild(vbox);

        var rarityColor = CharacterDollPanel.GetRarityColor(_item.Rarity);

        // Заголовок.
        var title = new Label
        {
            Text = "✂ Разделить стак",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 20);
        title.AddThemeColorOverride("font_color", ParchmentTheme.InkBlack);
        vbox.AddChild(title);

        var info = new Label
        {
            Text = $"{_item.NameRu} ×{_total} → две кучки",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        info.AddThemeFontSizeOverride("font_size", 14);
        info.AddThemeColorOverride("font_color", rarityColor);
        vbox.AddChild(info);

        vbox.AddChild(new HSeparator());

        // ── Строка деления: [число-слева] [−] [слайдер] [+] [число-справа] ──
        var sliderRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        sliderRow.AddThemeConstantOverride("separation", 12);
        sliderRow.SizeFlagsVertical = SizeFlags.ExpandFill;
        vbox.AddChild(sliderRow);

        // Левое число: сколько останется в исходном стке.
        var leftBox = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        leftBox.AddThemeConstantOverride("separation", 0);
        sliderRow.AddChild(leftBox);

        _leftLabel = new Label
        {
            Text = "0",
            CustomMinimumSize = new Vector2(64, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _leftLabel.AddThemeFontSizeOverride("font_size", 22);
        _leftLabel.AddThemeColorOverride("font_color", ParchmentTheme.AccentGold);
        leftBox.AddChild(_leftLabel);

        var leftCap = new Label
        {
            Text = "останется",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        leftCap.AddThemeFontSizeOverride("font_size", 11);
        leftCap.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        leftBox.AddChild(leftCap);

        // Кнопка «−».
        var minusBtn = new Button
        {
            Text = "−",
            CustomMinimumSize = new Vector2(44, 44),
            TooltipText = "Меньше в новую кучку",
        };
        minusBtn.Pressed += () => Nudge(-1);
        sliderRow.AddChild(minusBtn);
        MinusButtonForQA = minusBtn;

        // Ползунок: значение = сколько УЙДЁТ в новую кучку.
        _slider = new HSlider
        {
            MinValue = 1,
            MaxValue = Mathf.Max(1, _total - 1),
            Step = 1,
            Value = Mathf.Max(1, _total / 2),
            CustomMinimumSize = new Vector2(240, 32),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        _slider.ValueChanged += OnSliderChanged;
        sliderRow.AddChild(_slider);

        // Кнопка «+».
        var plusBtn = new Button
        {
            Text = "+",
            CustomMinimumSize = new Vector2(44, 44),
            TooltipText = "Больше в новую кучку",
        };
        plusBtn.Pressed += () => Nudge(+1);
        sliderRow.AddChild(plusBtn);
        PlusButtonForQA = plusBtn;

        // Правое число: сколько уйдёт в новую кучку.
        var rightBox = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        rightBox.AddThemeConstantOverride("separation", 0);
        sliderRow.AddChild(rightBox);

        _rightLabel = new Label
        {
            Text = "0",
            CustomMinimumSize = new Vector2(64, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _rightLabel.AddThemeFontSizeOverride("font_size", 22);
        _rightLabel.AddThemeColorOverride("font_color", ParchmentTheme.AccentGreen);
        rightBox.AddChild(_rightLabel);

        var rightCap = new Label
        {
            Text = "отделится",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        rightCap.AddThemeFontSizeOverride("font_size", 11);
        rightCap.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        rightBox.AddChild(rightCap);

        vbox.AddChild(new HSeparator());

        // ── Кнопки подтверждения ──
        var buttonRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        buttonRow.AddThemeConstantOverride("separation", 12);
        vbox.AddChild(buttonRow);

        _confirmButton = new Button
        {
            Text = "✓ Разделить",
            TooltipText = "Подтвердить деление стака",
        };
        _confirmButton.Pressed += OnConfirm;
        buttonRow.AddChild(_confirmButton);

        var cancelBtn = new Button
        {
            Text = "Отмена",
            TooltipText = "Закрыть без изменений",
        };
        cancelBtn.Pressed += () => _parent.CloseSplitDialog();
        buttonRow.AddChild(cancelBtn);

        UpdateNumbers();
    }

    /// <summary>Сдвинуть значение слайдера на delta (кнопки −/+).</summary>
    private void Nudge(int delta)
    {
        _slider.Value = Mathf.Clamp(_slider.Value + delta, _slider.MinValue, _slider.MaxValue);
    }

    private void OnSliderChanged(double value)
    {
        UpdateNumbers();
    }

    private void UpdateNumbers()
    {
        int move = (int)_slider.Value;
        _leftLabel.Text = (_total - move).ToString();
        _rightLabel.Text = move.ToString();
    }

    private void OnConfirm()
    {
        int move = (int)_slider.Value;
        // R10 P1-SlotId: действие ТОЛЬКО по стабильной идентичности кучки
        // (SlotId + expectedItemId). Если инвентарь мутировал с момента
        // открытия диалога и кучка исчезла/подменена — операция отклонена
        // внутри TrySplitSlotForDialog (тост «Стак изменился»), диалог
        // остаётся открытым для осознанного решения игрока.
        if (_parent.TrySplitSlotForDialog(_slot.SlotId, _item.ItemId, move))
        {
            _parent.CloseSplitDialog();
        }
    }

    private void OnBackdropInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            _parent.CloseSplitDialog();
        }
    }
}
