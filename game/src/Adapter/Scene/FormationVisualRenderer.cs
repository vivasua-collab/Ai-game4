#nullable enable
// Этап 6 внедрения ЦИ (2026-08-23): FormationVisualRenderer — отображение
// формаций на поверхности (FORMATION_SYSTEM.md + formation_visualization.md,
// категория «Static / FormationArrayEffect»; sprite-swap заменён на custom _Draw).
//
// Слои отрисовки (по formation_visualization.md):
//   1. Контур по Shape (Circle/Triangle/Square/Pentagon/Star/Hexagram)
//   2. Руны на вершинах + центральный глиф
//   3. Прогресс наполнения (дуга-индикатор + текст %)
//   4. Радиус действия (тонкий пунктир, кап 30 тайлов для читаемости)
//
// Стадии → стиль:
//   Drawing  — золотой пунктирный контур
//   Filling  — контур стихии + заливка сектора по fillRatio
//   Active   — пульсирующее свечение (3 контура) + яркие руны
//   Depleted — серый тусклый (одноразовые исчезают сразу)
using Godot;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Adapter.Di;
using CultivationGame.Modules.Formation;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Рендерер формаций на земле. Подписан на события стадий/пула формации;
// данные формы и стихии — из FormationService.CurrentFormation.
/// ZIndex ниже персонажей (массив лежит на земле).
/// </summary>
public partial class FormationVisualRenderer : Node2D
{
    [Inject] private FormationService Formations = null!;
    [Inject] private ISubscriber<FormationStageChangedEvent> StageSub = null!;
    [Inject] private ISubscriber<FormationQiPoolChangedEvent> PoolSub = null!;
    [Inject] private ISubscriber<FormationActivatedEvent> ActivatedSub = null!;

    private System.IDisposable? _stageToken;
    private System.IDisposable? _poolToken;
    private System.IDisposable? _activatedToken;

    // Живой визуал формации (одна активная — как в сервисе).
    private bool _visible;
    private float _fillRatio;          // 0..1 из FormationQiPoolChangedEvent
    private float _pulseTime;
    private bool _dirty = true;

    // Кэш геометрии (пересчёт при смене формации).
    private Vector2 _center;           // мировые пиксели
    private float _contourRadius;      // пиксели
    private Color _elementColor;
    private FormationShape _shape;
    private string _label = "";
    private FormationStage _lastStage;

    /// <summary>Визуальный радиус контура по размеру (тайлы → пиксели).
    /// Small 3×3 м = 1.5 т = 96 px и т.д. (FORMATION_SYSTEM §4).</summary>
    private static float ContourRadiusPixels(FormationSize size) => size switch
    {
        FormationSize.Small => 1.5f * GameConstants.TILE_PIXELS,
        FormationSize.Medium => 5f * GameConstants.TILE_PIXELS,
        FormationSize.Large => 15f * GameConstants.TILE_PIXELS,
        FormationSize.Great => 50f * GameConstants.TILE_PIXELS,
        _ => 60f * GameConstants.TILE_PIXELS
    };

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        ZIndex = (int)RenderLayer.Objects + 2; // на земле, выше объектов

        _stageToken = StageSub?.Subscribe(OnStageChanged);
        _poolToken = PoolSub?.Subscribe(OnPoolChanged);
        _activatedToken = ActivatedSub?.Subscribe(OnActivated);
        GD.Print("[FormationVisualRenderer] Ready");
    }

    public override void _ExitTree()
    {
        _stageToken?.Dispose();
        _poolToken?.Dispose();
        _activatedToken?.Dispose();
    }

    public override void _Process(double delta)
    {
        if (!_visible) return;
        _pulseTime += (float)delta;
        // Свечение активной формации пульсирует; наполнение меняет прогресс.
        QueueRedraw();
    }

    // === Подписки ===

    private void OnStageChanged(in FormationStageChangedEvent e)
    {
        _lastStage = e.NewStage;
        if (e.NewStage == FormationStage.None)
        {
            _visible = false;
        }
        else
        {
            _visible = true;
            RebuildGeometry();
        }
        QueueRedraw();
    }

    private void OnPoolChanged(in FormationQiPoolChangedEvent e)
    {
        _fillRatio = e.FillRatio;
        _dirty = true;
        QueueRedraw();
    }

    private void OnActivated(in FormationActivatedEvent e)
    {
        _visible = true;
        _fillRatio = 1f;
        RebuildGeometry();
        QueueRedraw();
    }

    private void RebuildGeometry()
    {
        var f = Formations?.CurrentFormation;
        if (f == null) return;

        _center = new Vector2(
            Formations.PositionX * GameConstants.TILE_PIXELS + GameConstants.TILE_PIXELS / 2f,
            Formations.PositionY * GameConstants.TILE_PIXELS + GameConstants.TILE_PIXELS / 2f);
        _contourRadius = ContourRadiusPixels(f.Size);
        _elementColor = ElementColor(f.Element);
        _shape = f.Shape;
        _label = f.DisplayName;
        _dirty = false;
    }

    // === Рисование ===

    public override void _Draw()
    {
        if (!_visible) return;
        var f = Formations?.CurrentFormation;
        if (f == null || _dirty) RebuildGeometry();
        if (f == null) return;

        switch (_lastStage)
        {
            case FormationStage.Drawing:
                DrawContourDashed(new Color(0.98f, 0.75f, 0.14f, 0.9f));
                DrawRunes(new Color(0.98f, 0.75f, 0.14f, 0.5f));
                break;

            case FormationStage.Filling:
                DrawContourSolid(WithAlpha(_elementColor, 0.85f));
                DrawFill(WithAlpha(_elementColor, 0.30f));
                DrawRunes(WithAlpha(_elementColor, 0.7f));
                DrawProgressArc(WithAlpha(_elementColor, 0.95f));
                break;

            case FormationStage.Active:
            {
                // Пульсирующее свечение: 3 контура с разной фазой.
                float pulse = 0.5f + 0.5f * Mathf.Sin(_pulseTime * 2.5f);
                DrawContourSolid(WithAlpha(_elementColor, 0.9f + 0.1f * pulse));
                DrawContourRadius(_contourRadius * (1.06f + 0.03f * pulse), WithAlpha(_elementColor, 0.35f));
                DrawContourRadius(_contourRadius * (1.12f + 0.05f * pulse), WithAlpha(_elementColor, 0.18f));
                DrawFill(WithAlpha(_elementColor, 0.16f + 0.06f * pulse));
                DrawRunes(WithAlpha(_elementColor, 0.8f + 0.2f * pulse));
                DrawCenterGlyph(WithAlpha(_elementColor, 0.95f), glow: true);
                break;
            }

            case FormationStage.Depleted:
                DrawContourSolid(new Color(0.4f, 0.4f, 0.42f, 0.6f));
                DrawRunes(new Color(0.4f, 0.4f, 0.42f, 0.4f));
                break;
        }

        // Радиус действия — тонкая пунктирная окружность (кап 30 тайлов).
        float effectRadius = Mathf.Min(f.EffectRadiusMeters / 2f, 30f) * GameConstants.TILE_PIXELS;
        DrawDashedCircle(_center, effectRadius, new Color(1f, 1f, 1f, 0.10f));

        // Подпись формации над контуром.
        var font = ThemeDB.FallbackFont;
        DrawString(font, _center + new Vector2(-_contourRadius, -_contourRadius - 26f),
            _label, HorizontalAlignment.Center, _contourRadius * 2, 13,
            WithAlpha(_elementColor, 0.95f));
    }

    /// <summary>Вершины контура по форме.</summary>
    private Vector2[] ShapePoints(Vector2 center, float radius, FormationShape shape)
    {
        switch (shape)
        {
            case FormationShape.Circle:
                return new[] { center }; // особый случай — рисуется DrawArc
            case FormationShape.Triangle:
                return RegularPolygon(center, radius, 3, -Mathf.Pi / 2);
            case FormationShape.Square:
                return RegularPolygon(center, radius, 4, Mathf.Pi / 4);
            case FormationShape.Pentagon:
                return RegularPolygon(center, radius, 5, -Mathf.Pi / 2);
            case FormationShape.Star:
            {
                // 10 вершин: чередование внешнего/внутреннего радиуса.
                var pts = new Vector2[10];
                for (int i = 0; i < 10; i++)
                {
                    float ang = -Mathf.Pi / 2 + i * Mathf.Pi / 5;
                    float r = i % 2 == 0 ? radius : radius * 0.45f;
                    pts[i] = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                }
                return pts;
            }
            case FormationShape.Hexagram:
            {
                // Два наложенных треугольника — 6 внешних вершин.
                return RegularPolygon(center, radius, 6, 0f);
            }
            default:
                return RegularPolygon(center, radius, 5, -Mathf.Pi / 2);
        }
    }

    private static Vector2[] RegularPolygon(Vector2 center, float radius, int n, float rotation)
    {
        var pts = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float ang = rotation + i * Mathf.Tau / n;
            pts[i] = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;
        }
        return pts;
    }

    private void DrawContourSolid(Color color, float width = 3f)
    {
        if (_shape == FormationShape.Circle)
        {
            DrawArc(_center, _contourRadius, 0, Mathf.Tau, 64, color, width);
        }
        else
        {
            var pts = ShapePoints(_center, _contourRadius, _shape);
            if (_shape == FormationShape.Hexagram)
            {
                // Гексаграмма = 2 треугольника через вершину.
                for (int start = 0; start < 6; start += 2)
                {
                    var tri = new Vector2[] { pts[start], pts[(start + 2) % 6], pts[(start + 4) % 6] };
                    DrawPolyline(AddClosingPoint(tri), color, width);
                }
            }
            else
            {
                DrawPolyline(AddClosingPoint(pts), color, width);
            }
        }
    }

    private void DrawContourDashed(Color color)
    {
        if (_shape == FormationShape.Circle)
        {
            DrawDashedCircle(_center, _contourRadius, color);
        }
        else
        {
            // Пунктир по вершинам: сегменты полилинии через одну.
            var pts = ShapePoints(_center, _contourRadius, _shape);
            for (int i = 0; i < pts.Length; i += 2)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Length];
                DrawLine(a, (a + b) / 2f, color, 2.5f);
            }
        }
    }

    private void DrawContourRadius(float radius, Color color)
    {
        DrawArc(_center, radius, 0, Mathf.Tau, 64, color, 2f);
    }

    /// <summary>Заливка контура по прогрессу наполнения (полигон/круг с alpha).</summary>
    private void DrawFill(Color color)
    {
        if (_fillRatio <= 0f) return;
        var filled = WithAlpha(color, color.A * Mathf.Min(1f, _fillRatio));
        if (_shape == FormationShape.Circle)
        {
            DrawCircle(_center, _contourRadius * _fillRatio, filled);
        }
        else
        {
            var pts = ShapePoints(_center, _contourRadius * _fillRatio, _shape);
            if (pts.Length > 2) DrawColoredPolygon(pts, filled);
        }
    }

    /// <summary>Руны на вершинах контура (маленькие ромбы).</summary>
    private void DrawRunes(Color color)
    {
        if (_shape == FormationShape.Circle)
        {
            // 8 рун по окружности.
            for (int i = 0; i < 8; i++)
            {
                float ang = i * Mathf.Tau / 8;
                DrawDiamond(_center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * _contourRadius,
                    6f, color);
            }
        }
        else
        {
            var pts = ShapePoints(_center, _contourRadius, _shape);
            foreach (var p in pts) DrawDiamond(p, 7f, color);
        }
    }

    /// <summary>Центральный глиф формации.</summary>
    private void DrawCenterGlyph(Color color, bool glow)
    {
        // Ромб + точка в центре.
        DrawDiamond(_center, glow ? 14f : 10f, color);
        DrawCircle(_center, 3f, WithAlpha(color, 0.6f));
    }

    /// <summary>Дуга-индикатор прогресса наполнения вокруг центра.</summary>
    private void DrawProgressArc(Color color)
    {
        float r = _contourRadius * 0.55f;
        // Фон дуги.
        DrawArc(_center, r, 0, Mathf.Tau, 48, WithAlpha(color, 0.15f), 4f);
        // Прогресс (от -90°).
        if (_fillRatio > 0f)
            DrawArc(_center, r, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * _fillRatio, 48, color, 4f);

        // Текст процентов.
        var font = ThemeDB.FallbackFont;
        DrawString(font, _center + new Vector2(-30f, 6f),
            $"{(int)(_fillRatio * 100)}%", HorizontalAlignment.Center, 60f, 14,
            new Color(1f, 1f, 1f, 0.9f));
    }

    private void DrawDiamond(Vector2 pos, float size, Color color)
    {
        var pts = new Vector2[]
        {
            pos + new Vector2(0, -size),
            pos + new Vector2(size, 0),
            pos + new Vector2(0, size),
            pos + new Vector2(-size, 0),
        };
        DrawColoredPolygon(pts, color);
    }

    private void DrawDashedCircle(Vector2 center, float radius, Color color)
    {
        const int segments = 36;
        for (int i = 0; i < segments; i += 2)
        {
            float a0 = i * Mathf.Tau / segments;
            float a1 = (i + 1) * Mathf.Tau / segments;
            DrawArc(center, radius, a0, a1, 4, color, 2f);
        }
    }

    private static Vector2[] AddClosingPoint(Vector2[] pts)
    {
        var closed = new Vector2[pts.Length + 1];
        System.Array.Copy(pts, closed, pts.Length);
        closed[pts.Length] = pts[0];
        return closed;
    }

    private static Color WithAlpha(Color c, float a)
    {
        var copy = c;
        copy.A = Mathf.Clamp(a, 0f, 1f);
        return copy;
    }

    private static Color ElementColor(Element e) => e switch
    {
        Element.Fire => new Color(1.0f, 0.35f, 0.12f),
        Element.Water => new Color(0.2f, 0.5f, 1.0f),
        Element.Earth => new Color(0.6f, 0.4f, 0.2f),
        Element.Air => new Color(0.75f, 0.78f, 0.75f),
        Element.Lightning => new Color(0.95f, 0.88f, 0.25f),
        Element.Void => new Color(0.42f, 0.1f, 0.55f),
        Element.Light => new Color(1.0f, 0.9f, 0.45f),
        Element.Poison => new Color(0.5f, 0.15f, 0.7f),
        _ => new Color(0.9f, 0.85f, 0.6f), // neutral/чистое Ци — золотистый
    };
}
