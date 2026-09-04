#nullable enable
// Создано: 2026-09-04 — S1 (директива пользователя: информативность UI).
// QuestWindow — окно квестов (клавиша Q, рекламировалась в легенде HUD и
// F1-справке, но не существовала — вторая «мёртвая проводка» после J).
//
// Список ВСЕХ квестов с состоянием и прогрессом целей; для NotStarted —
// кнопка «Принять» (IQuestService.StartQuest). Квесты «оживают» из UI:
// раньше система квестов была недостижима (никто не вызывал StartQuest).
//
// Паттерн: HotkeysWindow (модальное окно, пауза из GameWorldController).
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.UI;

/// <summary>
/// Окно квестов (Q): все квесты, статусы, цели, награды, принятие.
/// </summary>
public partial class QuestWindow : Control
{
    [Inject] private readonly IQuestService? _questService = null;

    private VBoxContainer? _list;
    private Label? _summaryLabel;

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        BuildUI();
        Visible = false;
        GD.Print("[QuestWindow] Ready");
    }

    public void Toggle()
    {
        Visible = !Visible;
        if (Visible) Rebuild();
    }

    private void Rebuild()
    {
        if (_list == null) return;

        // Очистить список (кроме шапки/подвала — список отдельным узлом).
        foreach (var child in _list.GetChildren())
            child.QueueFree();

        var quests = _questService?.GetQuestSummaries();
        if (quests == null || quests.Count == 0)
        {
            _summaryLabel!.Text = "Квесты не найдены";
            return;
        }

        int active = 0, done = 0, available = 0;
        foreach (var q in quests)
        {
            if (q.Status == QuestStatus.Active) active++;
            else if (q.Status == QuestStatus.Completed) done++;
            else if (q.Status == QuestStatus.NotStarted) available++;
            _list.AddChild(MakeQuestCard(q));
        }
        _summaryLabel!.Text = $"Активных: {active} · Доступно: {available} · Завершено: {done}";
    }

    // === Construction ===

    private void BuildUI()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var bg = new ColorRect { Name = "Background", Color = new Godot.Color(0.05f, 0.03f, 0.02f, 0.78f) };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Stop;
        AddChild(bg);

        var panel = new Panel { Name = "QuestPanel" };
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        panel.OffsetLeft = -380; panel.OffsetRight = 380;
        panel.OffsetTop = -300; panel.OffsetBottom = 300;
        panel.MouseFilter = MouseFilterEnum.Stop;
        AddChild(panel);

        var style = new StyleBoxFlat { BgColor = new Godot.Color(0.10f, 0.08f, 0.05f, 0.98f) };
        style.SetBorderWidthAll(2);
        style.SetBorderColor(new Godot.Color(0.55f, 0.42f, 0.20f, 0.9f));
        style.SetCornerRadiusAll(8);
        panel.AddThemeStyleboxOverride("panel", style);

        var root = new VBoxContainer();
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.OffsetLeft = 12; root.OffsetRight = -12;
        root.OffsetTop = 10; root.OffsetBottom = -10;
        root.AddThemeConstantOverride("separation", 6);
        panel.AddChild(root);

        var header = new HBoxContainer();
        var title = new Label { Text = "◆  Журнал заданий  ◆" };
        title.AddThemeFontSizeOverride("font_size", 20);
        title.AddThemeColorOverride("font_color", new Godot.Color(0.95f, 0.80f, 0.45f));
        header.AddChild(title);
        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddChild(spacer);
        var closeBtn = new Button { Text = "×" };
        closeBtn.Pressed += () => Visible = false;
        header.AddChild(closeBtn);
        root.AddChild(header);

        _summaryLabel = new Label { Text = "" };
        _summaryLabel.AddThemeFontSizeOverride("font_size", 12);
        _summaryLabel.AddThemeColorOverride("font_color", new Godot.Color(0.7f, 0.6f, 0.45f));
        root.AddChild(_summaryLabel);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        root.AddChild(scroll);

        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_list);

        var hint = new Label { Text = "Q / Esc — закрыть · кнопка «Принять» запускает квест" };
        hint.AddThemeFontSizeOverride("font_size", 11);
        hint.AddThemeColorOverride("font_color", new Godot.Color(0.6f, 0.55f, 0.45f));
        root.AddChild(hint);
    }

    private Control MakeQuestCard(QuestSummary q)
    {
        var card = new Panel();
        var cardStyle = new StyleBoxFlat { BgColor = new Godot.Color(0.15f, 0.12f, 0.08f, 0.9f) };
        cardStyle.SetBorderWidthAll(1);
        cardStyle.SetBorderColor(StatusColour(q.Status, false));
        cardStyle.SetCornerRadiusAll(6);
        cardStyle.ContentMarginLeft = 10;
        cardStyle.ContentMarginRight = 10;
        cardStyle.ContentMarginTop = 8;
        cardStyle.ContentMarginBottom = 8;
        card.AddThemeStyleboxOverride("panel", cardStyle);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 3);
        card.AddChild(vbox);

        // Заголовок: имя + статус.
        var head = new HBoxContainer();
        var name = new Label { Text = q.DisplayName };
        name.AddThemeFontSizeOverride("font_size", 15);
        name.AddThemeColorOverride("font_color", new Godot.Color(0.95f, 0.9f, 0.8f));
        head.AddChild(name);
        head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var status = new Label { Text = StatusRu(q.Status) };
        status.AddThemeFontSizeOverride("font_size", 13);
        status.AddThemeColorOverride("font_color", StatusColour(q.Status, true));
        status.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        head.AddChild(status);
        vbox.AddChild(head);

        // Описание.
        var desc = new Label
        {
            Text = q.Description,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        desc.AddThemeFontSizeOverride("font_size", 12);
        desc.AddThemeColorOverride("font_color", new Godot.Color(0.8f, 0.75f, 0.65f));
        vbox.AddChild(desc);

        // Цели с прогрессом.
        foreach (var (objective, progress, target, complete) in q.Objectives)
        {
            var obj = new Label
            {
                Text = complete ? $"✔ {objective}" : $"◻ {objective}",
            };
            obj.AddThemeFontSizeOverride("font_size", 12);
            obj.AddThemeColorOverride("font_color", complete
                ? new Godot.Color(0.55f, 0.8f, 0.4f)
                : new Godot.Color(0.75f, 0.7f, 0.6f));
            vbox.AddChild(obj);
        }

        // Награды.
        if (q.RewardTexts is { Length: > 0 })
        {
            var rew = new Label { Text = "Награда: " + string.Join(", ", q.RewardTexts) };
            rew.AddThemeFontSizeOverride("font_size", 11);
            rew.AddThemeColorOverride("font_color", new Godot.Color(0.85f, 0.75f, 0.35f));
            vbox.AddChild(rew);
        }

        // Кнопка «Принять» для доступных квестов.
        if (q.Status == QuestStatus.NotStarted)
        {
            var startBtn = new Button { Text = "Принять" };
            startBtn.AddThemeFontSizeOverride("font_size", 12);
            string questId = q.QuestId;
            startBtn.Pressed += () =>
            {
                bool ok = _questService?.StartQuest(questId) ?? false;
                if (ok)
                {
                    GD.Print($"[QuestWindow] Квест принят: {questId}");
                    Rebuild();
                }
            };
            vbox.AddChild(startBtn);
        }

        return card;
    }

    private static string StatusRu(QuestStatus s) => s switch
    {
        QuestStatus.NotStarted => "доступен",
        QuestStatus.Active     => "активен",
        QuestStatus.Completed  => "завершён",
        QuestStatus.Failed     => "провален",
        QuestStatus.Abandoned  => "брошен",
        _ => s.ToString(),
    };

    private static Godot.Color StatusColour(QuestStatus s, bool bright)
    {
        return s switch
        {
            QuestStatus.Active    => bright ? new Godot.Color(0.55f, 0.8f, 0.4f) : new Godot.Color(0.35f, 0.5f, 0.3f),
            QuestStatus.Completed => bright ? new Godot.Color(0.6f, 0.6f, 0.55f) : new Godot.Color(0.4f, 0.4f, 0.38f),
            QuestStatus.Failed    => bright ? new Godot.Color(0.85f, 0.35f, 0.3f) : new Godot.Color(0.55f, 0.25f, 0.22f),
            QuestStatus.Abandoned => bright ? new Godot.Color(0.6f, 0.45f, 0.4f) : new Godot.Color(0.42f, 0.32f, 0.3f),
            _                     => bright ? new Godot.Color(0.9f, 0.8f, 0.45f) : new Godot.Color(0.6f, 0.52f, 0.32f),
        };
    }
}
