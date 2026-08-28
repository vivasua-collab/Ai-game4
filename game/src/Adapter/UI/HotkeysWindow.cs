#nullable enable
// Создано: 2026-08-28 — окно-справка «Горячие клавиши» (F1).
//
// Решение пользователя: все подсказки по горячим клавишам выносятся из
// игровых панелей в отдельное меню (Old School: F1 = help). Обязателен
// фон — текст не накладывается на окружение и всегда читается одинаково.
//
// Источник перечня: InputMapInitializer.cs (канонический реестр действий).
// Окно модальное, паузит игру (справочник — чтение, не бой).
using Godot;
using System.Collections.Generic;

namespace CultivationGame.Adapter.UI;

/// <summary>
/// Окно-справка: полный перечень горячих клавиш проекта (F1).
/// </summary>
public partial class HotkeysWindow : Control
{
    private bool _initialized;

    public override void _Ready()
    {
        BuildUI();
        Visible = false;
        _initialized = true;
        GD.Print("[HotkeysWindow] Ready");
    }

    public void Open()
    {
        if (Visible) return;
        Visible = true;
    }

    public void Close() => Visible = false;

    public void Toggle()
    {
        if (Visible) Close();
        else Open();
    }

    // === Construction ===

    private void BuildUI()
    {
        // Полноэкранный оверлей: однородный тёмный фон под ВСЕМ окном.
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var bg = new ColorRect
        {
            Name = "Background",
            Color = new Color(0.05f, 0.03f, 0.02f, 0.78f),
        };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Stop;
        AddChild(bg);

        var panel = new Panel { Name = "HotkeysPanel" };
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        panel.OffsetLeft = -340; panel.OffsetRight = 340;
        panel.OffsetTop = -330; panel.OffsetBottom = 330;
        panel.MouseFilter = MouseFilterEnum.Stop;
        AddChild(panel);

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.10f, 0.08f, 0.05f, 0.98f),
        };
        style.SetBorderWidthAll(2);
        style.SetBorderColor(new Color(0.55f, 0.42f, 0.20f, 0.9f));
        style.SetCornerRadiusAll(8);
        panel.AddThemeStyleboxOverride("panel", style);

        var root = new VBoxContainer();
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.OffsetLeft = 12; root.OffsetRight = -12;
        root.OffsetTop = 10; root.OffsetBottom = -10;
        root.AddThemeConstantOverride("separation", 6);
        panel.AddChild(root);

        // Заголовок + закрыть.
        var header = new HBoxContainer();
        var title = new Label { Text = "◆  Горячие клавиши  ◆" };
        title.AddThemeFontSizeOverride("font_size", 20);
        title.AddThemeColorOverride("font_color", new Color(0.95f, 0.80f, 0.45f));
        header.AddChild(title);

        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddChild(spacer);

        var closeBtn = new Button { Text = "×" };
        closeBtn.Pressed += Close;
        header.AddChild(closeBtn);
        root.AddChild(header);

        root.AddChild(MakeSeparator());

        // Прокручиваемый перечень по группам.
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        root.AddChild(scroll);

        var list = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        list.AddThemeConstantOverride("separation", 2);
        scroll.AddChild(list);

        foreach (var (groupName, entries) in BuildContent())
        {
            list.AddChild(MakeGroupHeader(groupName));
            foreach (var (key, desc) in entries)
                list.AddChild(MakeRow(key, desc));
            list.AddChild(MakeSeparator());
        }
    }

    /// <summary>Полный перечень горячих клавиш (канон: InputMapInitializer).</summary>
    private static (string Group, (string Key, string Desc)[] Entries)[] BuildContent() => new[]
    {
        ("Движение", new[]
        {
            ("W A S D / стрелки", "перемещение"),
            ("Shift (удерж.)", "бег"),
        }),
        ("Бой и техники", new[]
        {
            ("Пробел", "атака"),
            ("Z", "каст выбранной техники: заряд → удержание в ауре → повторный Z — выпуск"),
            ("X", "следующая техника (цикл выбора)"),
            ("3 … 9", "каст техники из слота быстрого доступа"),
            ("Shift + 1 … 9", "использовать предмет из пояса"),
            ("1 / 2", "оружие ближнего / дальнего боя (зарезервировано)"),
        }),
        ("Взаимодействие", new[]
        {
            ("E", "взаимодействие / диалог с NPC"),
            ("F", "собрать ресурс с тайла"),
            ("ЛКМ", "движение к точке / выбор"),
            ("ПКМ", "контекстное действие"),
        }),
        ("Окна", new[]
        {
            ("B", "инвентарь"),
            ("C", "лист персонажа"),
            ("T", "Книга Техник (библиотека: уровни / типы / стихии)"),
            ("K", "окно Культивации (меридианы, ядро, техники культивации)"),
            ("J / Q", "журнал / журнал заданий"),
            ("M / N", "карта мира / мини-карта"),
        }),
        ("Состояние практика", new[]
        {
            ("V", "медитация (поглощение Ци из среды)"),
            ("R", "отдых"),
        }),
        ("Система", new[]
        {
            ("Esc", "пауза / закрыть активное окно"),
            ("F5 / F9", "быстрая запись / загрузка"),
            ("Page Up / Page Down", "скорость времени"),
            ("F1", "это окно справки"),
        }),
        ("Разработка (DEBUG)", new[]
        {
            ("F2", "чит-меню"),
            ("` (backquote)", "лог ввода"),
        }),
    };

    // === Helpers ===

    private static Label MakeGroupHeader(string text)
    {
        var label = new Label { Text = $"▓ {text.ToUpperInvariant()}" };
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", new Color(0.80f, 0.66f, 0.35f));
        return label;
    }

    private static HBoxContainer MakeRow(string key, string desc)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);

        // «Клавиша» — плашка с рамкой (Old School look).
        var keyPanel = new Panel
        {
            CustomMinimumSize = new Vector2(170, 24),
        };
        var keyStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.20f, 0.17f, 0.12f, 0.95f),
        };
        keyStyle.SetBorderWidthAll(1);
        keyStyle.SetBorderColor(new Color(0.62f, 0.50f, 0.28f));
        keyStyle.SetCornerRadiusAll(4);
        keyStyle.ContentMarginLeft = 6;
        keyStyle.ContentMarginRight = 6;
        keyPanel.AddThemeStyleboxOverride("panel", keyStyle);

        var keyLabel = new Label
        {
            Text = key,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        keyLabel.AddThemeFontSizeOverride("font_size", 12);
        keyLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.86f, 0.62f));
        keyLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        keyPanel.AddChild(keyLabel);
        row.AddChild(keyPanel);

        var descLabel = new Label
        {
            Text = desc,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        descLabel.AddThemeFontSizeOverride("font_size", 13);
        descLabel.AddThemeColorOverride("font_color", new Color(0.90f, 0.86f, 0.78f));
        row.AddChild(descLabel);

        return row;
    }

    private static HSeparator MakeSeparator()
    {
        var sep = new HSeparator();
        sep.AddThemeConstantOverride("separation", 4);
        return sep;
    }
}
