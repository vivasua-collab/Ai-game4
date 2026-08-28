#nullable enable
// Создано: 2026-08-28 — Книга Техник (этап «Библиотека + Лодаут»).
//
// ElementStyle — единая палитра стихий и акценты блоков типов техник.
// Одна стихия = один цвет ВО ВСЁМ UI (Книга Техник, слоты быстрого доступа,
// будущие списки выбора). Принцип Old School RPG: цвет несёт смысл.
using Godot;
using CultivationGame.Core.Data;

namespace CultivationGame.Adapter.UI;

/// <summary>
/// Палитра стихий и типов техник для UI (Книга Техник, слоты, чипы).
/// </summary>
public static class ElementStyle
{
    /// <summary>Цвет стихии.</summary>
    public static Color ElementColor(Element e) => e switch
    {
        Element.Fire      => new Color(0.80f, 0.30f, 0.18f),
        Element.Water     => new Color(0.25f, 0.50f, 0.75f),
        Element.Earth     => new Color(0.55f, 0.42f, 0.22f),
        Element.Air       => new Color(0.65f, 0.72f, 0.75f),
        Element.Lightning => new Color(0.90f, 0.78f, 0.25f),
        Element.Void      => new Color(0.42f, 0.30f, 0.52f),
        Element.Light     => new Color(0.93f, 0.88f, 0.62f),
        Element.Poison    => new Color(0.40f, 0.65f, 0.30f),
        _                 => new Color(0.60f, 0.58f, 0.54f), // Neutral
    };

    /// <summary>Акцент блока типа (бордюр/заголовок в Книге Техник).</summary>
    public static Color TypeAccent(TechniqueType t) => t switch
    {
        TechniqueType.Combat      => new Color(0.75f, 0.28f, 0.20f),
        TechniqueType.Defense     => new Color(0.45f, 0.50f, 0.62f),
        TechniqueType.Support     => new Color(0.35f, 0.60f, 0.40f),
        TechniqueType.Healing     => new Color(0.55f, 0.80f, 0.55f),
        TechniqueType.Movement    => new Color(0.55f, 0.65f, 0.80f),
        TechniqueType.Sensory     => new Color(0.80f, 0.70f, 0.40f),
        TechniqueType.Poison      => new Color(0.45f, 0.65f, 0.30f),
        TechniqueType.Curse       => new Color(0.55f, 0.30f, 0.55f),
        TechniqueType.Formation   => new Color(0.70f, 0.55f, 0.25f),
        TechniqueType.Cultivation => new Color(0.85f, 0.72f, 0.45f),
        _                         => new Color(0.60f, 0.58f, 0.54f),
    };

    /// <summary>Эмодзи стихии (компактные подписи чипов).</summary>
    public static string ElementEmoji(Element e) => e switch
    {
        Element.Fire      => "🔥",
        Element.Water     => "💧",
        Element.Earth     => "🪨",
        Element.Air       => "💨",
        Element.Lightning => "⚡",
        Element.Void      => "🌑",
        Element.Light     => "✨",
        Element.Poison    => "☠",
        _                 => "⚪",
    };

    /// <summary>Русское имя стихии.</summary>
    public static string ElementName(Element e) => e switch
    {
        Element.Fire      => "Огонь",
        Element.Water     => "Вода",
        Element.Earth     => "Земля",
        Element.Air       => "Воздух",
        Element.Lightning => "Молния",
        Element.Void      => "Пустота",
        Element.Light     => "Свет",
        Element.Poison    => "Яд",
        _                 => "Нейтрально",
    };

    /// <summary>Русское имя типа техники (заголовки блоков).</summary>
    public static string TypeName(TechniqueType t) => t switch
    {
        TechniqueType.Combat      => "Атака",
        TechniqueType.Defense     => "Защита",
        TechniqueType.Support     => "Поддержка",
        TechniqueType.Healing     => "Исцеление",
        TechniqueType.Movement    => "Перемещение",
        TechniqueType.Sensory     => "Восприятие",
        TechniqueType.Poison      => "Яд",
        TechniqueType.Curse       => "Проклятие",
        TechniqueType.Formation   => "Формация",
        TechniqueType.Cultivation => "Культивация",
        _                         => t.ToString()
    };

    /// <summary>Русское имя грейда.</summary>
    public static string GradeName(TechniqueGrade g) => g switch
    {
        TechniqueGrade.Common       => "Обычная",
        TechniqueGrade.Refined      => "Очищенная",
        TechniqueGrade.Perfect      => "Совершенная",
        TechniqueGrade.Transcendent => "ТРАНСЦЕНДЕНТНАЯ",
        _                           => g.ToString()
    };
}
