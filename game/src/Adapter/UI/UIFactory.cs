#nullable enable
using Godot;

namespace CultivationGame.Adapter.UI;

/// <summary>
/// Static factory for Godot Control nodes. All sizes are expressed in
/// per-mille (‰) of the 1920x1080 reference resolution per
/// docs_v2/07_ui/UI_DESIGN.md §4.2 — this guarantees pixel-perfect scaling
/// across 1080p/1440p/4K with integer math.
/// </summary>
public static class UIFactory
{
    /// <summary>Reference width (‰ base).</summary>
    public const float RefWidth = 1920f;

    /// <summary>Reference height (‰ base).</summary>
    public const float RefHeight = 1080f;

    public static Panel CreatePanel(string name, float widthPpm, float heightPpm)
    {
        var panel = new Panel
        {
            Name = name,
            CustomMinimumSize = PpmToSize(widthPpm, heightPpm),
        };
        return panel;
    }

    public static Button CreateButton(string name, string text, float widthPpm, float heightPpm)
    {
        var btn = new Button
        {
            Name = name,
            Text = text,
            CustomMinimumSize = PpmToSize(widthPpm, heightPpm),
        };
        return btn;
    }

    public static Label CreateLabel(string name, string text, int fontSizePpm = 18)
    {
        var label = new Label
        {
            Name = name,
            Text = text,
        };
        label.AddThemeFontSizeOverride("font_size", PpmToPx(fontSizePpm));
        return label;
    }

    public static VBoxContainer CreateVBox(string name, float separationPm = 8f)
    {
        var box = new VBoxContainer { Name = name };
        box.AddThemeConstantOverride("separation", PpmToPx(separationPm));
        return box;
    }

    public static HBoxContainer CreateHBox(string name, float separationPm = 8f)
    {
        var box = new HBoxContainer { Name = name };
        box.AddThemeConstantOverride("separation", PpmToPx(separationPm));
        return box;
    }

    public static ColorRect CreateColorRect(string name, Color color, float widthPm, float heightPm)
    {
        var rect = new ColorRect
        {
            Name = name,
            Color = color,
            CustomMinimumSize = PpmToSize(widthPm, heightPm),
        };
        return rect;
    }

    public static ProgressBar CreateProgressBar(string name, float widthPm, float heightPm, Color fill)
    {
        var bar = new ProgressBar
        {
            Name = name,
            CustomMinimumSize = PpmToSize(widthPm, heightPm),
            MinValue = 0f,
            MaxValue = 1f,
            Value = 1f,
            ShowPercentage = false,
        };
        var sb = new StyleBoxFlat { BgColor = fill };
        bar.AddThemeStyleboxOverride("fill", sb);
        return bar;
    }

    // ---- Per-mille → pixel helpers ----

    /// <summary>
    /// Per-mille of the shorter reference axis (1080 height) → pixels,
    /// rounded to nearest integer.
    /// </summary>
    public static int PpmToPx(float ppm)
    {
        return Mathf.RoundToInt(ppm * RefHeight / 1000f);
    }

    /// <summary>Per-mille (width, height) of reference resolution → Vector2 px.</summary>
    public static Vector2 PpmToSize(float widthPpm, float heightPpm)
    {
        return new Vector2(
            Mathf.RoundToInt(widthPpm * RefWidth / 1000f),
            Mathf.RoundToInt(heightPpm * RefHeight / 1000f)
        );
    }

    /// <summary>Per-mille of width → px (use for X-anchored sizes).</summary>
    public static int PpmWidthToPx(float ppm) => Mathf.RoundToInt(ppm * RefWidth / 1000f);

    /// <summary>Per-mille of height → px (use for Y-anchored sizes).</summary>
    public static int PpmHeightToPx(float ppm) => Mathf.RoundToInt(ppm * RefHeight / 1000f);
}
