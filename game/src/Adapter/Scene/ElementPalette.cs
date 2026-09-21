#nullable enable
// Создано: 2026-09-21 — R29 (план R23 §6.7): общий источник цветов стихий
// для МИРОВЫХ VFX-рендереров. До R29 единственная копия жила приватным
// статиком в TechniqueEffectRenderer.ElementColor — новые рендеры
// (AoeFxRenderer, HomingProjectileRenderer) переносят её сюда, источник
// становится один (чистый рефактор, поведение идентично).
//
// Канон цветов: ELEMENTS_SYSTEM.md §2.
using CultivationGame.Core.Data;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Цвета стихий для мировых боевых VFX (вспышки/снаряды/ауры).
/// UI-панели пользуются своими темами (ElementStyle) — там другой
/// контраст-контекст (пергамент), мировые цвета ярче.
/// </summary>
public static class ElementPalette
{
    /// <summary>Яркий цвет стихии для эффекта (ELEMENTS_SYSTEM.md §2).</summary>
    public static Godot.Color ToColor(Element e) => e switch
    {
        Element.Fire => new Godot.Color(1.0f, 0.35f, 0.12f),
        Element.Water => new Godot.Color(0.2f, 0.5f, 1.0f),
        Element.Earth => new Godot.Color(0.6f, 0.4f, 0.2f),
        Element.Air => new Godot.Color(0.75f, 0.78f, 0.75f),
        Element.Lightning => new Godot.Color(0.95f, 0.88f, 0.25f),
        Element.Void => new Godot.Color(0.42f, 0.1f, 0.55f),
        Element.Light => new Godot.Color(1.0f, 0.9f, 0.45f),
        Element.Poison => new Godot.Color(0.5f, 0.15f, 0.7f),
        _ => new Godot.Color(0.95f, 0.95f, 0.95f),
    };
}
