#nullable enable
// Этап 2 внедрения ЦИ (2026-08-23): TechniquesPanel — панель техник игрока (T).
// Non-modal (не ставит паузу): боевые техники требуют быстрого доступа.
// Строки-кнопки: ЛКМ — выбрать активную, двойной клик — каст.
// Подписки: TechniqueLearned/Forget/SelectionChanged — синхронизация списка;
// кулдауны обновляются в _Process по TechniqueService.GetCooldown.
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Adapter.Di;
using CultivationGame.Modules.Combat;

namespace CultivationGame.Adapter.UI;

/// <summary>
/// Панель техник игрока (клавиша T). Показывает изученные техники по слотам,
/// выбор активной (Z — каст, X — следующая). Кулдауны — затемнение + секунды.
/// </summary>
public partial class TechniquesPanel : Panel
{
    [Inject] private TechniqueService Techniques = null!;
    [Inject] private IPublisher<TechniqueCastRequestedEvent> CastPub = null!;
    [Inject] private ISubscriber<TechniqueLearnedEvent> LearnedSub = null!;
    [Inject] private ISubscriber<TechniqueForgottenEvent> ForgottenSub = null!;
    [Inject] private ISubscriber<TechniqueSelectionChangedEvent> SelectionSub = null!;
    [Inject] private IQiService Qi = null!;

    private VBoxContainer _list = null!;
    private Label _headerLabel = null!;
    private readonly Dictionary<string, Button> _rowById = new();
    private System.IDisposable? _learnedToken;
    private System.IDisposable? _forgottenToken;
    private System.IDisposable? _selectionToken;
    private bool _initialized;

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        BuildUI();

        _learnedToken = LearnedSub?.Subscribe(OnLearned);
        _forgottenToken = ForgottenSub?.Subscribe(OnForgotten);
        _selectionToken = SelectionSub?.Subscribe(OnSelectionChanged);

        Visible = false;
        _initialized = true;
        RebuildList();
        GD.Print("[TechniquesPanel] Ready");
    }

    public override void _ExitTree()
    {
        _learnedToken?.Dispose();
        _forgottenToken?.Dispose();
        _selectionToken?.Dispose();
    }

    public override void _Process(double delta)
    {
        if (!_initialized || !Visible) return;

        // Обновление кулдаунов/доступности Ци (дёшево: ≤ ~10 строк).
        foreach (var kvp in _rowById)
        {
            var tech = Techniques.GetTechnique(kvp.Key);
            if (tech == null) continue;
            float cd = Techniques.GetCooldown(kvp.Key);
            var btn = kvp.Value;

            if (cd > 0f)
            {
                btn.Disabled = true;
                btn.Text = RowTitle(tech) + $"  ⏳{cd:F0}с";
            }
            else
            {
                bool noQi = Qi.CurrentQi < tech.QiCost;
                btn.Disabled = tech.Type == TechniqueType.Cultivation;
                btn.Text = RowTitle(tech) + (noQi && tech.QiCost > 0 ? "  ⚠ мало Ци" : "");
            }
        }
    }

    private void BuildUI()
    {
        // Левая сторона экрана, вертикально по центру.
        SetAnchorsPreset(Control.LayoutPreset.CenterLeft);
        CustomMinimumSize = new Vector2(470, 0);
        OffsetLeft = 12;
        OffsetTop = -220;
        MouseFilter = MouseFilterEnum.Stop;

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.09f, 0.06f, 0.9f),
        };
        style.SetBorderWidthAll(1);
        style.SetBorderColor(new Color(0.45f, 0.35f, 0.2f, 0.8f));
        style.SetCornerRadiusAll(6);
        AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        vbox.OffsetLeft = 8; vbox.OffsetRight = -8;
        vbox.OffsetTop = 6; vbox.OffsetBottom = -6;
        vbox.AddThemeConstantOverride("separation", 4);
        AddChild(vbox);

        _headerLabel = new Label { Text = "Техники" };
        _headerLabel.AddThemeFontSizeOverride("font_size", 17);
        _headerLabel.AddThemeColorOverride("font_color", new Color(0.94f, 0.83f, 0.66f));
        vbox.AddChild(_headerLabel);

        _list = new VBoxContainer();
        _list.AddThemeConstantOverride("separation", 3);
        vbox.AddChild(_list);

        var hint = new Label
        {
            Text = "ЛКМ — выбрать | 2×ЛКМ — применить | X — след. | Z — каст выбранной",
        };
        hint.AddThemeFontSizeOverride("font_size", 11);
        hint.AddThemeColorOverride("font_color", new Color(0.6f, 0.55f, 0.45f));
        vbox.AddChild(hint);
    }

    private void RebuildList()
    {
        foreach (var child in _list.GetChildren())
            child.QueueFree();
        _rowById.Clear();

        var ids = Techniques.GetOrderedIds();
        var all = Techniques.GetAllTechniques();

        int combatUsed = 0, combatCap = TechniqueService.SlotCapacity(TechniqueType.Combat, (int)Qi.CultivationLevel);
        foreach (var t in all.Values)
            if (TechniqueService.SlotCategory(t.Type) == TechniqueType.Combat) combatUsed++;

        _headerLabel.Text = $"Техники — слоты: боевые {combatUsed}/{combatCap} + проклятие + формация + культивация";

        foreach (var id in ids)
        {
            var tech = Techniques.GetTechnique(id);
            if (tech == null) continue;

            var btn = new Button
            {
                Text = RowTitle(tech),
                Alignment = HorizontalAlignment.Left,
                ClipText = false,
                AutowrapMode = TextServer.AutowrapMode.Off,
            };
            btn.AddThemeFontSizeOverride("font_size", 13);
            btn.Pressed += () => OnRowClicked(id);
            // Двойной клик эмулируем таймером (Godot Button не имеет DoubleClick).
            btn.GuiInput += (@event) =>
            {
                if (@event is InputEventMouseButton mb && mb.Pressed && mb.DoubleClick)
                {
                    OnRowClicked(id);
                    RequestCast(id);
                }
            };

            _list.AddChild(btn);
            _rowById[id] = btn;
        }

        UpdateSelectionHighlight();
    }

    private void OnRowClicked(string id)
    {
        Techniques.SelectTechnique(id);
    }

    private void RequestCast(string id)
    {
        var mouse = GetGlobalMousePosition();
        CastPub.Publish(new TechniqueCastRequestedEvent(
            id, (int)(mouse.X * 1000), (int)(mouse.Y * 1000)));
    }

    private string RowTitle(LearnedTechnique tech)
    {
        string emoji = ElementEmoji(tech.Element);
        string grade = tech.Grade switch
        {
            TechniqueGrade.Refined => "·Очищ",
            TechniqueGrade.Perfect => "·Соверш",
            TechniqueGrade.Transcendent => "·ТРАНСЦЕНД",
            _ => ""
        };
        string ulti = tech.IsUltimate ? "⚡" : "";
        string passive = tech.Type == TechniqueType.Cultivation ? " [пассив]" : "";
        return $"{emoji} {ulti}{tech.Name} L{tech.Level}{grade} | ⚔{tech.BaseDamage} " +
               $"Ци:{tech.QiCost} КД:{tech.Cooldown:F0}с М:{tech.Mastery:F0}%{passive}";
    }

    private static string ElementEmoji(Element e) => e switch
    {
        Element.Fire => "🔥",
        Element.Water => "💧",
        Element.Earth => "🪨",
        Element.Air => "💨",
        Element.Lightning => "⚡",
        Element.Void => "🌑",
        Element.Light => "✨",
        Element.Poison => "☠",
        _ => "⚪",
    };

    private void UpdateSelectionHighlight()
    {
        string? sel = Techniques.SelectedTechniqueId;
        foreach (var kvp in _rowById)
        {
            bool selected = kvp.Key == sel;
            kvp.Value.Modulate = selected
                ? new Color(1.0f, 0.9f, 0.5f)
                : new Color(1f, 1f, 1f);
        }
    }

    private void OnLearned(in TechniqueLearnedEvent e) => RebuildList();
    private void OnForgotten(in TechniqueForgottenEvent e) => RebuildList();

    private void OnSelectionChanged(in TechniqueSelectionChangedEvent e)
    {
        if (Visible) UpdateSelectionHighlight();
    }
}
