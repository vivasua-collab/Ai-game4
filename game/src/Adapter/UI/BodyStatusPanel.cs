#nullable enable
using System;
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.UI;

/// <summary>
/// Body status panel — схематическое отображение частей тела персонажа.
///
/// Показывает:
/// - Силуэт тела (гуманоид/животное) с частями, окрашенными по состоянию
/// - HP бары (RedHP функциональный + BlackHP структурный) для каждой части
/// - Состояние: Healthy/Bruised/Wounded/Disabled/Severed
///
/// Design per docs_v2/07_ui/UI_DESIGN.md §6.2 (Character Sheet) + §2.3 (body status colors).
/// BodyPartState: 5 значений (Healthy, Bruised, Wounded, Disabled, Severed).
/// </summary>
public partial class BodyStatusPanel : Control
{
    [Inject] private IBodyService BodyService { get; set; } = null!;
    [Inject] private ISubscriber<BodyPartDamagedEvent> DamagedSub { get; set; } = null!;
    [Inject] private ISubscriber<BodyPartHealedEvent> HealedSub { get; set; } = null!;
    [Inject] private ISubscriber<BodyPartSeveredEvent> SeveredSub { get; set; } = null!;

    private IDisposable? _damagedToken;
    private IDisposable? _healedToken;
    private IDisposable? _severedToken;

    // Layout: silhouette on left (200×300), part list on right (300×300).
    private BodySilhouetteRenderer _silhouetteNode = null!;
    private VBoxContainer _partList = null!;
    private Label _morphologyLabel = null!;

    // Cached part data for rendering.
    private Dictionary<BodyPartType, BodyPartData> _partsCache = new();

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
        {
            ContainerAdapter.InjectProperties(this, container);
        }

        BuildUI();
        RefreshFromBody();

        // Subscribe to body events for live updates.
        _damagedToken = DamagedSub.Subscribe(OnPartDamaged);
        _healedToken = HealedSub.Subscribe(OnPartHealed);
        _severedToken = SeveredSub.Subscribe(OnPartSevered);
    }

    public override void _ExitTree()
    {
        _damagedToken?.Dispose();
        _healedToken?.Dispose();
        _severedToken?.Dispose();
    }

    private void BuildUI()
    {
        Theme = ParchmentTheme.Create();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var hbox = new HBoxContainer();
        hbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        hbox.OffsetLeft = 8;
        hbox.OffsetRight = -8;
        hbox.OffsetTop = 8;
        hbox.OffsetBottom = -8;
        hbox.AddThemeConstantOverride("separation", 12);
        AddChild(hbox);

        // ── Left: Silhouette ──
        var leftWrap = new VBoxContainer
        {
            Name = "SilhouetteWrap",
            CustomMinimumSize = new Vector2(200, 400),
        };
        hbox.AddChild(leftWrap);

        _morphologyLabel = new Label
        {
            Text = "Тело: Гуманоид",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _morphologyLabel.AddThemeFontSizeOverride("font_size", 16);
        _morphologyLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkBlack);
        leftWrap.AddChild(_morphologyLabel);

        _silhouetteNode = new BodySilhouetteRenderer();
        _silhouetteNode.CustomMinimumSize = new Vector2(200, 350);
        leftWrap.AddChild(_silhouetteNode);

        // ── Right: Part list with HP bars ──
        var rightWrap = new VBoxContainer
        {
            Name = "PartListWrap",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        hbox.AddChild(rightWrap);

        var listTitle = new Label
        {
            Text = "Части тела:",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        listTitle.AddThemeFontSizeOverride("font_size", 16);
        listTitle.AddThemeColorOverride("font_color", ParchmentTheme.InkBlack);
        rightWrap.AddChild(listTitle);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(300, 350),
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        rightWrap.AddChild(scroll);

        _partList = new VBoxContainer
        {
            Name = "Parts",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _partList.AddThemeConstantOverride("separation", 3);
        scroll.AddChild(_partList);
    }

    /// <summary>Refresh panel from BodyService (called on open + after events).</summary>
    public void RefreshFromBody()
    {
        if (BodyService == null) return;

        // Update morphology label.
        _morphologyLabel.Text = $"Тело: {BodyService.EntityId}";

        // Cache parts.
        _partsCache.Clear();
        foreach (var part in BodyService.GetAllParts())
        {
            _partsCache[part.Type] = part;
        }

        // Update silhouette.
        if (_silhouetteNode is BodySilhouetteRenderer sr)
        {
            sr.SetParts(_partsCache);
        }

        // Update part list.
        RefreshPartList();
    }

    private void RefreshPartList()
    {
        // Clear existing.
        foreach (var child in _partList.GetChildren())
        {
            child.QueueFree();
        }

        // Build rows.
        foreach (var kvp in _partsCache)
        {
            var part = kvp.Value;
            var row = CreatePartRow(part);
            _partList.AddChild(row);
        }
    }

    private HBoxContainer CreatePartRow(BodyPartData part)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var stateColor = GetStateColor(part.State);
        var stateGlyph = GetStateGlyph(part.State);

        // Status indicator (colored square + glyph).
        var indicator = new ColorRect
        {
            Color = stateColor,
            CustomMinimumSize = new Vector2(6, 22),
        };
        row.AddChild(indicator);

        // Part name + glyph.
        var nameLabel = new Label
        {
            Text = $"{stateGlyph} {GetPartLabel(part.Type)}",
            CustomMinimumSize = new Vector2(120, 22),
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 13);
        nameLabel.AddThemeColorOverride("font_color", stateColor);
        row.AddChild(nameLabel);

        // HP bar (RedHP).
        if (part.MaxRedHP > 0)
        {
            var redRatio = part.MaxRedHP > 0 ? (float)part.CurrentRedHP / part.MaxRedHP : 0f;
            var hpLabel = new Label
            {
                Text = $"{part.CurrentRedHP}/{part.MaxRedHP}",
                CustomMinimumSize = new Vector2(80, 22),
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            hpLabel.AddThemeFontSizeOverride("font_size", 12);
            hpLabel.AddThemeColorOverride("font_color", stateColor);
            row.AddChild(hpLabel);
        }

        return row;
    }

    // === Event handlers (live updates) ===

    private void OnPartDamaged(in BodyPartDamagedEvent e)
    {
        CallDeferred(nameof(RefreshFromBody));
    }

    private void OnPartHealed(in BodyPartHealedEvent e)
    {
        CallDeferred(nameof(RefreshFromBody));
    }

    private void OnPartSevered(in BodyPartSeveredEvent e)
    {
        CallDeferred(nameof(RefreshFromBody));
    }

    // === Helpers ===

    private static Color GetStateColor(BodyPartState state)
    {
        return state switch
        {
            BodyPartState.Healthy  => new Color(0.3f, 0.6f, 0.3f),  // green
            BodyPartState.Bruised  => new Color(0.8f, 0.7f, 0.2f),  // yellow
            BodyPartState.Wounded  => new Color(0.8f, 0.5f, 0.2f),  // orange
            BodyPartState.Disabled => new Color(0.8f, 0.2f, 0.2f),  // red
            BodyPartState.Severed  => new Color(0.4f, 0.4f, 0.4f),  // grey
            _                      => new Color(0.5f, 0.5f, 0.5f),
        };
    }

    private static string GetStateGlyph(BodyPartState state)
    {
        return state switch
        {
            BodyPartState.Healthy  => "◉",  // filled circle
            BodyPartState.Bruised  => "◐",  // half circle
            BodyPartState.Wounded  => "◑",  // other half
            BodyPartState.Disabled => "◒",  // quarter
            BodyPartState.Severed  => "○",  // empty circle
            _                      => "○",
        };
    }

    private static string GetPartLabel(BodyPartType type)
    {
        return type switch
        {
            BodyPartType.Head          => "Голова",
            BodyPartType.Torso          => "Торс",
            BodyPartType.Heart          => "Сердце",
            BodyPartType.LeftArm        => "Левая рука",
            BodyPartType.RightArm       => "Правая рука",
            BodyPartType.LeftHand       => "Левая кисть",
            BodyPartType.RightHand      => "Правая кисть",
            BodyPartType.LeftLeg        => "Левая нога",
            BodyPartType.RightLeg       => "Правая нога",
            BodyPartType.LeftFoot       => "Левая стопа",
            BodyPartType.RightFoot      => "Правая стопа",
            BodyPartType.FrontLeftLeg   => "Передняя левая",
            BodyPartType.FrontRightLeg  => "Передняя правая",
            BodyPartType.BackLeftLeg    => "Задняя левая",
            BodyPartType.BackRightLeg   => "Задняя правая",
            BodyPartType.Tail           => "Хвост",
            BodyPartType.LeftWing       => "Левое крыло",
            BodyPartType.RightWing      => "Правое крыло",
            BodyPartType.BirdTail       => "Хвост",
            BodyPartType.Core           => "Ядро",
            BodyPartType.Essence        => "Сущность",
            _                           => type.ToString(),
        };
    }
}

/// <summary>
/// Renders schematic body silhouette via _Draw().
/// Humanoid: head circle + torso rect + 4 limbs + heart indicator.
/// Parts colored by BodyPartState.
/// </summary>
public partial class BodySilhouetteRenderer : Control
{
    private Dictionary<BodyPartType, BodyPartData> _parts = new();

    public void SetParts(Dictionary<BodyPartType, BodyPartData> parts)
    {
        _parts = parts;
        QueueRedraw();
    }

    public override void _Draw()
    {
        // Background.
        DrawRect(new Rect2(0, 0, 200, 350), new Color(0.1f, 0.08f, 0.05f, 0.5f), true);

        if (_parts.Count == 0)
        {
            DrawString(GetDefaultFont(), new Vector2(60, 175), "Нет данных");
            return;
        }

        // Determine morphology from parts.
        bool isHumanoid = _parts.ContainsKey(BodyPartType.LeftArm) || _parts.ContainsKey(BodyPartType.RightArm);
        bool isQuadruped = _parts.ContainsKey(BodyPartType.FrontLeftLeg) || _parts.ContainsKey(BodyPartType.BackLeftLeg);
        bool isBird = _parts.ContainsKey(BodyPartType.LeftWing) || _parts.ContainsKey(BodyPartType.RightWing);

        if (isHumanoid) DrawHumanoid();
        else if (isQuadruped) DrawQuadruped();
        else if (isBird) DrawBird();
        else DrawAmorphous();
    }

    private void DrawHumanoid()
    {
        // Layout (200×350 canvas):
        // Head: circle at (100, 40), r=20
        // Torso: rect (80, 65, 40, 80)
        // Heart: small circle at (100, 95), r=6
        // LeftArm: rect (55, 70, 20, 60)
        // RightArm: rect (125, 70, 20, 60)
        // LeftHand: circle (65, 140), r=8
        // RightHand: circle (135, 140), r=8
        // LeftLeg: rect (82, 150, 15, 70)
        // RightLeg: rect (103, 150, 15, 70)
        // LeftFoot: rect (78, 225, 20, 10)
        // RightFoot: rect (102, 225, 20, 10)

        var headColor = GetPartColor(BodyPartType.Head);
        var torsoColor = GetPartColor(BodyPartType.Torso);
        var heartColor = GetPartColor(BodyPartType.Heart);
        var leftArmColor = GetPartColor(BodyPartType.LeftArm);
        var rightArmColor = GetPartColor(BodyPartType.RightArm);
        var leftHandColor = GetPartColor(BodyPartType.LeftHand);
        var rightHandColor = GetPartColor(BodyPartType.RightHand);
        var leftLegColor = GetPartColor(BodyPartType.LeftLeg);
        var rightLegColor = GetPartColor(BodyPartType.RightLeg);
        var leftFootColor = GetPartColor(BodyPartType.LeftFoot);
        var rightFootColor = GetPartColor(BodyPartType.RightFoot);

        // Head.
        DrawCircle(new Vector2(100, 40), 20, headColor);
        DrawCircleOutline(new Vector2(100, 40), 20, ParchmentTheme.InkBlack);

        // Torso.
        DrawRect(new Rect2(80, 65, 40, 80), torsoColor, true);
        DrawRect(new Rect2(80, 65, 40, 80), ParchmentTheme.InkBlack, false, 1f);

        // Heart (inside torso).
        DrawCircle(new Vector2(100, 95), 6, heartColor);
        DrawCircleOutline(new Vector2(100, 95), 6, ParchmentTheme.InkBlack);

        // Arms.
        DrawRect(new Rect2(55, 70, 20, 60), leftArmColor, true);
        DrawRect(new Rect2(55, 70, 20, 60), ParchmentTheme.InkBlack, false, 1f);
        DrawRect(new Rect2(125, 70, 20, 60), rightArmColor, true);
        DrawRect(new Rect2(125, 70, 20, 60), ParchmentTheme.InkBlack, false, 1f);

        // Hands.
        DrawCircle(new Vector2(65, 140), 8, leftHandColor);
        DrawCircleOutline(new Vector2(65, 140), 8, ParchmentTheme.InkBlack);
        DrawCircle(new Vector2(135, 140), 8, rightHandColor);
        DrawCircleOutline(new Vector2(135, 140), 8, ParchmentTheme.InkBlack);

        // Legs.
        DrawRect(new Rect2(82, 150, 15, 70), leftLegColor, true);
        DrawRect(new Rect2(82, 150, 15, 70), ParchmentTheme.InkBlack, false, 1f);
        DrawRect(new Rect2(103, 150, 15, 70), rightLegColor, true);
        DrawRect(new Rect2(103, 150, 15, 70), ParchmentTheme.InkBlack, false, 1f);

        // Feet.
        DrawRect(new Rect2(78, 225, 20, 10), leftFootColor, true);
        DrawRect(new Rect2(78, 225, 20, 10), ParchmentTheme.InkBlack, false, 1f);
        DrawRect(new Rect2(102, 225, 20, 10), rightFootColor, true);
        DrawRect(new Rect2(102, 225, 20, 10), ParchmentTheme.InkBlack, false, 1f);
    }

    private void DrawQuadruped()
    {
        // Layout: horizontal body, 4 legs, head front, tail back.
        // Head: circle (40, 120), r=18
        // Torso: rect (55, 100, 100, 50)
        // Heart: circle (80, 125), r=5
        // Front legs: rect (60, 155, 12, 40) + rect (80, 155, 12, 40)
        // Back legs: rect (120, 155, 12, 40) + rect (140, 155, 12, 40)
        // Tail: rect (155, 110, 30, 8)

        var headColor = GetPartColor(BodyPartType.Head);
        var torsoColor = GetPartColor(BodyPartType.Torso);
        var heartColor = GetPartColor(BodyPartType.Heart);
        var flLegColor = GetPartColor(BodyPartType.FrontLeftLeg);
        var frLegColor = GetPartColor(BodyPartType.FrontRightLeg);
        var blLegColor = GetPartColor(BodyPartType.BackLeftLeg);
        var brLegColor = GetPartColor(BodyPartType.BackRightLeg);
        var tailColor = GetPartColor(BodyPartType.Tail);

        // Head.
        DrawCircle(new Vector2(40, 120), 18, headColor);
        DrawCircleOutline(new Vector2(40, 120), 18, ParchmentTheme.InkBlack);

        // Torso.
        DrawRect(new Rect2(55, 100, 100, 50), torsoColor, true);
        DrawRect(new Rect2(55, 100, 100, 50), ParchmentTheme.InkBlack, false, 1f);

        // Heart.
        DrawCircle(new Vector2(80, 125), 5, heartColor);

        // Legs.
        DrawRect(new Rect2(60, 155, 12, 40), flLegColor, true);
        DrawRect(new Rect2(60, 155, 12, 40), ParchmentTheme.InkBlack, false, 1f);
        DrawRect(new Rect2(80, 155, 12, 40), frLegColor, true);
        DrawRect(new Rect2(80, 155, 12, 40), ParchmentTheme.InkBlack, false, 1f);
        DrawRect(new Rect2(120, 155, 12, 40), blLegColor, true);
        DrawRect(new Rect2(120, 155, 12, 40), ParchmentTheme.InkBlack, false, 1f);
        DrawRect(new Rect2(140, 155, 12, 40), brLegColor, true);
        DrawRect(new Rect2(140, 155, 12, 40), ParchmentTheme.InkBlack, false, 1f);

        // Tail.
        DrawRect(new Rect2(155, 110, 30, 8), tailColor, true);
        DrawRect(new Rect2(155, 110, 30, 8), ParchmentTheme.InkBlack, false, 1f);
    }

    private void DrawBird()
    {
        // Layout: body ellipse, 2 wings, head, tail, 2 legs.
        // Head: circle (100, 50), r=15
        // Torso: ellipse (100, 120), rx=30, ry=40
        // Heart: circle (100, 110), r=5
        // LeftWing: rect (60, 90, 30, 40)
        // RightWing: rect (110, 90, 30, 40)
        // BirdTail: triangle (85, 160, 115, 160, 100, 190)
        // Legs: rect (90, 160, 6, 30) + rect (104, 160, 6, 30)

        var headColor = GetPartColor(BodyPartType.Head);
        var torsoColor = GetPartColor(BodyPartType.Torso);
        var heartColor = GetPartColor(BodyPartType.Heart);
        var lwingColor = GetPartColor(BodyPartType.LeftWing);
        var rwingColor = GetPartColor(BodyPartType.RightWing);
        var tailColor = GetPartColor(BodyPartType.BirdTail);

        // Head.
        DrawCircle(new Vector2(100, 50), 15, headColor);
        DrawCircleOutline(new Vector2(100, 50), 15, ParchmentTheme.InkBlack);

        // Body (ellipse approximated).
        DrawRect(new Rect2(70, 90, 60, 60), torsoColor, true);
        DrawRect(new Rect2(70, 90, 60, 60), ParchmentTheme.InkBlack, false, 1f);

        // Heart.
        DrawCircle(new Vector2(100, 110), 5, heartColor);

        // Wings.
        DrawRect(new Rect2(60, 90, 30, 40), lwingColor, true);
        DrawRect(new Rect2(60, 90, 30, 40), ParchmentTheme.InkBlack, false, 1f);
        DrawRect(new Rect2(110, 90, 30, 40), rwingColor, true);
        DrawRect(new Rect2(110, 90, 30, 40), ParchmentTheme.InkBlack, false, 1f);

        // Tail (triangle).
        DrawTriangle(new Vector2(85, 160), new Vector2(115, 160), new Vector2(100, 190), tailColor);

        // Legs (simplified — birds have 2 legs, use LeftLeg/RightLeg).
        var leftLegColor = GetPartColor(BodyPartType.LeftLeg);
        var rightLegColor = GetPartColor(BodyPartType.RightLeg);
        DrawRect(new Rect2(90, 155, 6, 30), leftLegColor, true);
        DrawRect(new Rect2(104, 155, 6, 30), rightLegColor, true);
    }

    private void DrawAmorphous()
    {
        // Core + Essence (2 circles).
        var coreColor = GetPartColor(BodyPartType.Core);
        var essenceColor = GetPartColor(BodyPartType.Essence);

        DrawCircle(new Vector2(100, 120), 30, coreColor);
        DrawCircleOutline(new Vector2(100, 120), 30, ParchmentTheme.InkBlack);
        DrawCircle(new Vector2(100, 200), 15, essenceColor);
        DrawCircleOutline(new Vector2(100, 200), 15, ParchmentTheme.InkBlack);
    }

    private Color GetPartColor(BodyPartType type)
    {
        if (!_parts.TryGetValue(type, out var part))
        {
            // Part not in cache = severed/missing.
            return new Color(0.3f, 0.3f, 0.3f, 0.5f); // dark grey, semi-transparent
        }

        return part.State switch
        {
            BodyPartState.Healthy  => new Color(0.3f, 0.6f, 0.3f),
            BodyPartState.Bruised  => new Color(0.8f, 0.7f, 0.2f),
            BodyPartState.Wounded  => new Color(0.8f, 0.5f, 0.2f),
            BodyPartState.Disabled => new Color(0.8f, 0.2f, 0.2f),
            BodyPartState.Severed  => new Color(0.3f, 0.3f, 0.3f, 0.3f),
            _                      => new Color(0.5f, 0.5f, 0.5f),
        };
    }

    private void DrawCircleOutline(Vector2 center, float radius, Color color)
    {
        for (int angle = 0; angle < 360; angle += 10)
        {
            float rad1 = Mathf.DegToRad(angle);
            float rad2 = Mathf.DegToRad(angle + 10);
            var p1 = center + new Vector2(Mathf.Cos(rad1) * radius, Mathf.Sin(rad1) * radius);
            var p2 = center + new Vector2(Mathf.Cos(rad2) * radius, Mathf.Sin(rad2) * radius);
            DrawLine(p1, p2, color, 1f);
        }
    }

    private void DrawTriangle(Vector2 p1, Vector2 p2, Vector2 p3, Color color)
    {
        // Filled triangle via 3 lines + fill.
        DrawLine(p1, p2, ParchmentTheme.InkBlack, 1f);
        DrawLine(p2, p3, ParchmentTheme.InkBlack, 1f);
        DrawLine(p3, p1, ParchmentTheme.InkBlack, 1f);
        // Fill: draw to centroid.
        var centroid = (p1 + p2 + p3) / 3f;
        DrawLine(p1, centroid, color, 1f);
        DrawLine(p2, centroid, color, 1f);
        DrawLine(p3, centroid, color, 1f);
    }

    private static Font GetDefaultFont()
    {
        return ThemeDB.FallbackFont;
    }
}
