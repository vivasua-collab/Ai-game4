#nullable enable
using Godot;

namespace CultivationGame.Adapter.UI;

/// <summary>
/// «Древний Пергамент» theme — Godot <see cref="Theme"/> resource factory.
/// Colours per docs_v2/07_ui/UI_DESIGN.md §2.1.
/// The factory builds a fresh Theme every call (Themes are Godot Resources,
/// cheap to construct). For performance, cache the result once per scene.
/// </summary>
public static class ParchmentTheme
{
    // ---- Core palette (§2.1) ----
    public static readonly Color ParchmentBase  = new("e8d5a8");
    public static readonly Color ParchmentDark  = new("c9a878");
    public static readonly Color ParchmentLight = new("f5e8c8");
    public static readonly Color InkBlack       = new("2a1d10");
    public static readonly Color InkFaded       = new("5a4a35");
    public static readonly Color AccentGold     = new("b8860b");
    public static readonly Color AccentRed      = new("8b0000");
    public static readonly Color AccentGreen    = new("4a6b3a");
    public static readonly Color AccentBlue     = new("3a5a7b");
    public static readonly Color AccentPurple   = new("5a3a7b");

    // ---- Item rarity palette (§2.2) ----
    public static readonly Color RarityCommon    = new("6b7280");
    public static readonly Color RarityUncommon  = new("22c55e");
    public static readonly Color RarityRare      = new("3b82f6");
    public static readonly Color RarityEpic      = new("a855f7");
    public static readonly Color RarityLegendary = new("fbbf24");
    public static readonly Color RarityMythic    = new("ef4444");

    /// <summary>
    /// Build a fully-styled <see cref="Theme"/> for the parchment look.
    /// Apply to a Control via <c>Control.Theme = ParchmentTheme.Create();</c>
    /// or globally via <c>GetTree().Root.Theme = ...</c>.
    /// </summary>
    public static Theme Create()
    {
        var theme = new Theme();

        // ---- Default font sizes (per-mille → px) ----
        // 18‰ of 1080 ≈ 19px for body text.
        theme.SetFontSize("font_size", "Label", 19);
        theme.SetFontSize("font_size", "Button", 19);
        theme.SetFontSize("font_size", "LineEdit", 19);

        // ---- Default colours ----
        theme.SetColor("font_color",         "Label",     InkBlack);
        theme.SetColor("font_color",         "Button",    InkBlack);
        theme.SetColor("font_hover_color",   "Button",    ParchmentLight);
        theme.SetColor("font_pressed_color", "Button",    AccentRed);
        theme.SetColor("font_disabled_color","Button",    InkFaded);
        theme.SetColor("font_color",         "LineEdit",  InkBlack);

        // ---- Button StyleBoxes ----
        theme.SetStylebox("normal",   "Button", ButtonNormal());
        theme.SetStylebox("hover",    "Button", ButtonHover());
        theme.SetStylebox("pressed",  "Button", ButtonPressed());
        theme.SetStylebox("disabled", "Button", ButtonDisabled());
        theme.SetStylebox("focus",    "Button", new StyleBoxEmpty());

        // ---- Panel StyleBox ----
        theme.SetStylebox("panel", "Panel", PanelStyle());

        // ---- LineEdit StyleBox ----
        theme.SetStylebox("normal", "LineEdit", LineEditStyle());

        return theme;
    }

    private static StyleBoxFlat ButtonNormal()
    {
        var sb = new StyleBoxFlat
        {
            BgColor = ParchmentDark,
            BorderColor = AccentGold,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
        };
        return sb;
    }

    private static StyleBoxFlat ButtonHover()
    {
        var sb = ButtonNormal();
        sb.BgColor = ParchmentLight;
        sb.BorderWidthBottom = 3;
        sb.BorderWidthTop = 3;
        sb.BorderWidthLeft = 3;
        sb.BorderWidthRight = 3;
        return sb;
    }

    private static StyleBoxFlat ButtonPressed()
    {
        var sb = ButtonNormal();
        sb.BgColor = new Color(
            ParchmentDark.R * 0.85f,
            ParchmentDark.G * 0.85f,
            ParchmentDark.B * 0.85f
        );
        return sb;
    }

    private static StyleBoxFlat ButtonDisabled()
    {
        var sb = ButtonNormal();
        sb.BgColor = new Color(ParchmentDark.R, ParchmentDark.G, ParchmentDark.B, 0.5f);
        sb.BorderColor = new Color(AccentGold.R, AccentGold.G, AccentGold.B, 0.4f);
        return sb;
    }

    private static StyleBoxFlat PanelStyle()
    {
        var sb = new StyleBoxFlat
        {
            BgColor = ParchmentBase,
            BorderColor = ParchmentDark,
            BorderWidthBottom = 3,
            BorderWidthTop = 3,
            BorderWidthLeft = 3,
            BorderWidthRight = 3,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 12,
            ContentMarginBottom = 12,
        };
        return sb;
    }

    private static StyleBoxFlat LineEditStyle()
    {
        var sb = new StyleBoxFlat
        {
            BgColor = ParchmentLight,
            BorderColor = ParchmentDark,
            BorderWidthBottom = 1,
            BorderWidthTop = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
        };
        return sb;
    }

    /// <summary>Lookup colour by rarity name (used by item slots / inventory).</summary>
    public static Color RarityColor(string rarity)
    {
        return rarity switch
        {
            "Common"    => RarityCommon,
            "Uncommon"  => RarityUncommon,
            "Rare"      => RarityRare,
            "Epic"      => RarityEpic,
            "Legendary" => RarityLegendary,
            "Mythic"    => RarityMythic,
            _           => RarityCommon,
        };
    }
}
