#nullable enable
using Godot;

namespace CultivationGame.Adapter.UI;

/// <summary>
/// Static factory for Godot Control nodes. All sizes are expressed in
/// per-mille (‰) of the 1920×1080 reference resolution per
/// docs_v2/07_ui/UI_DESIGN.md §4.2 — guarantees pixel-perfect scaling
/// across 1080p/1440p/4K with integer math.
///
/// Godot 4.7 notes:
///  • Control nodes gained <c>OffsetTransform*</c> properties (visual-only
///    translate/rotate/scale that do NOT affect layout or input hit-testing).
///    Use <see cref="ApplyVisualOffset"/> for hover/press animations.
///  • <c>SetAnchorsAndOffsetsPreset</c> is the canonical way to set layout;
///    avoid manually poking <c>AnchorLeft/Top/Right/Bottom</c> + <c>Offset*</c>
///    unless you need non-preset layouts.
/// </summary>
public static class UIFactory
{
    /// <summary>Reference width (‰ base).</summary>
    public const float RefWidth = 1920f;

    /// <summary>Reference height (‰ base).</summary>
    public const float RefHeight = 1080f;

    // ──────────────────────────────────────────────────────────────────
    //  Basic Control factories
    // ──────────────────────────────────────────────────────────────────

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
            // 4.7: default GrowHorizontal/GrowVertical = both directions so
            // containers can size the button; keep it for layout consistency.
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
        };
        return btn;
    }

    public static Label CreateLabel(string name, string text, int fontSizePpm = 18)
    {
        var label = new Label
        {
            Name = name,
            Text = text,
            // 4.7: enable AutoWrap by default for long localized strings.
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
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
            // 4.7: ColorRect defaults to Stop mouse filter, which blocks input
            // for underlying UI. Most background rects should be Ignore.
            MouseFilter = Control.MouseFilterEnum.Ignore,
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

    // ──────────────────────────────────────────────────────────────────
    //  Godot 4.7 — Control offset transforms (visual-only)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Apply a visual-only offset to a Control (Godot 4.7 feature).
    /// The offset does NOT affect layout, anchoring, or input hit-testing —
    /// perfect for hover/press/click animations that should not reflow siblings.
    ///
    /// See: https://docs.godotengine.org/en/4.7/classes/class_control.html
    /// </summary>
    public static void ApplyVisualOffset(Control control, Vector2 offset, float rotation = 0f, Vector2? scale = null)
    {
        // 4.7 added visual offset transform properties to Control.
        // They are applied AFTER layout, purely for rendering.
        control.OffsetTransformPosition = offset;
        if (rotation != 0f)
            control.OffsetTransformRotation = rotation;
        if (scale.HasValue)
            control.OffsetTransformScale = scale.Value;
    }

    /// <summary>Reset the visual offset transform (e.g. on mouse exit).</summary>
    public static void ClearVisualOffset(Control control)
    {
        control.OffsetTransformPosition = Vector2.Zero;
        control.OffsetTransformRotation = 0f;
        control.OffsetTransformScale = Vector2.One;
    }

    /// <summary>
    /// Wire hover animation: lift the button 2px up on hover, restore on exit.
    /// Uses 4.7 offset transforms so layout is never recomputed.
    /// </summary>
    public static void AddHoverLift(Button button, float liftPx = 2f)
    {
        button.MouseEntered += () => ApplyVisualOffset(button, new Vector2(0, -liftPx));
        button.MouseExited += () => ClearVisualOffset(button);
    }

    /// <summary>
    /// Wire press animation: shrink to 97% on press, restore on release/exit.
    /// </summary>
    public static void AddPressShrink(Button button, float factor = 0.97f)
    {
        button.ButtonDown += () => ApplyVisualOffset(button, Vector2.Zero, 0f, new Vector2(factor, factor));
        button.ButtonUp += () => ClearVisualOffset(button);
        button.MouseExited += () => ClearVisualOffset(button);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Layout helpers (4.7 canonical API)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Anchor a Control to fill its parent (preset 15). Use for backgrounds,
    /// full-screen overlays, and root containers.
    /// </summary>
    public static T FillParent<T>(T control) where T : Control
    {
        control.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return control;
    }

    /// <summary>
    /// Anchor a Control to the center of its parent (preset 8).
    /// Offsets are relative to the center point.
    /// </summary>
    public static T CenterInParent<T>(T control, float offsetLeft, float offsetTop, float offsetRight, float offsetBottom)
        where T : Control
    {
        control.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        control.OffsetLeft = offsetLeft;
        control.OffsetTop = offsetTop;
        control.OffsetRight = offsetRight;
        control.OffsetBottom = offsetBottom;
        return control;
    }

    /// <summary>
    /// Anchor a Control to a horizontal band at the top of the parent.
    /// Offsets define the top/bottom of the band.
    /// </summary>
    public static T TopBand<T>(T control, float topOffset, float bottomOffset) where T : Control
    {
        control.AnchorLeft = 0f;
        control.AnchorTop = 0f;
        control.AnchorRight = 1f;
        control.AnchorBottom = 0f;
        control.OffsetLeft = 0;
        control.OffsetTop = topOffset;
        control.OffsetRight = 0;
        control.OffsetBottom = bottomOffset;
        return control;
    }

    /// <summary>
    /// Anchor a Control to the bottom-right corner of its parent.
    /// Offsets are negative (distance from the right/bottom edge).
    /// </summary>
    public static T BottomRightCorner<T>(T control, float offsetLeftNeg, float offsetRightNeg, float offsetTopNeg, float offsetBottomNeg)
        where T : Control
    {
        control.AnchorLeft = 1f;
        control.AnchorTop = 1f;
        control.AnchorRight = 1f;
        control.AnchorBottom = 1f;
        control.OffsetLeft = offsetLeftNeg;
        control.OffsetTop = offsetTopNeg;
        control.OffsetRight = offsetRightNeg;
        control.OffsetBottom = offsetBottomNeg;
        return control;
    }

    // ──────────────────────────────────────────────────────────────────
    //  Per-mille → pixel helpers
    // ──────────────────────────────────────────────────────────────────

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
