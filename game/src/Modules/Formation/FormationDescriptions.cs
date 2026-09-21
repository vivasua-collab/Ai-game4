#nullable enable
// Создано: 2026-09-21 — R30-эпизод 2, П3 (репорт плейтеста 21.09): «светлая
// формация создалась, но не понятно, что делает — нет описания типа действия».
//
// ЕДИНЫЙ источник текстов о формациях для UI:
//   • GameWorldController — тосты стадий (Drawing/Filling с радиусом зарядки П2)
//     и активации (полное описание эффектов);
//   • FormationVisualRenderer — подзаголовок под именем формации в мире
//     (краткое действие — читается прямо у контура).
//
// Паттерн: статический чистый класс без состояния (прецедент ElementPalette,
// «единственный источник истины» для UI-текстов).

using System;
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.Formation;

/// <summary>
/// Человекочитаемые описания формаций: тип действия, эффекты, радиус,
/// правило зарядки (П2). Формат — краткий для мира, полный для тостов.
/// </summary>
public static class FormationDescriptions
{
    /// <summary>Название типа (совпадает с GameWorldController-тостами).</summary>
    public static string TypeLabel(FormationType t) => t switch
    {
        FormationType.Barrier => "Барьер",
        FormationType.Trap => "Ловушка",
        FormationType.Amplification => "Усиление",
        FormationType.Suppression => "Подавление",
        FormationType.Gathering => "Сбор Ци",
        FormationType.Detection => "Обнаружение",
        FormationType.Teleportation => "Портал",
        FormationType.Summoning => "Призыв",
        _ => t.ToString()
    };

    /// <summary>Краткое «что делает» — одна фраза (подзаголовок в мире).</summary>
    public static string TypeAction(FormationType t) => t switch
    {
        FormationType.Barrier => "защитный щит союзникам в зоне",
        FormationType.Trap => "замораживает/замедляет врагов в зоне",
        FormationType.Amplification => "усиливает союзников в зоне",
        FormationType.Suppression => "ослабляет врагов в зоне",
        FormationType.Gathering => "×2 к поглощению Ци при медитации в зоне",
        FormationType.Detection => "обостряет чутьё союзников в зоне",
        FormationType.Teleportation => "коренит врагов у контура",
        FormationType.Summoning => "тень сражается за вас",
        _ => "эффект формации"
    };

    /// <summary>
    /// Краткая сводка эффектов для подзаголовка в мире:
    /// «урон союзникам +35%», «скорость врагов −45%» и т.п.
    /// </summary>
    public static string ShortSummary(FormationData f)
    {
        if (f?.Effects is not { Count: > 0 }) return TypeAction(f?.FormationType ?? FormationType.Barrier);
        var first = f.Effects[0];
        string line = EffectLine(first);
        return f.Effects.Count > 1 ? $"{line} (+{f.Effects.Count - 1})" : line;
    }

    /// <summary>Полное описание для тоста активации:
    /// тип + действие + все эффекты + радиус + правило зарядки.</summary>
    public static string FullDescription(FormationData f, int chargingRadiusTiles)
    {
        string label = TypeLabel(f.FormationType);
        string action = TypeAction(f.FormationType);
        var parts = new System.Collections.Generic.List<string>();
        if (f.Effects is { Count: > 0 })
        {
            foreach (var e in f.Effects)
            {
                string line = EffectLine(e);
                if (line.Length > 0) parts.Add(line);
            }
        }
        string effects = parts.Count > 0 ? string.Join(", ", parts) : "нет активных эффектов";
        int radiusM = (int)(f.EffectRadiusMeters / GameConstants.TILE_SIZE_M);
        return $"{label} — {action}: {effects} · радиус {radiusM} м · " +
               $"зарядка в {chargingRadiusTiles} тайлах от контура";
    }

    /// <summary>Одна строка эффекта: «урон союзникам +35%».</summary>
    public static string EffectLine(FormationEffectEntry e)
    {
        string stat = StatLabel(e.TargetStat);
        bool pct = e.Value <= 1f; // генератор пишет доли (0.35 = +35%)
        string value = pct ? $"{(int)Math.Round(e.Value * 100f)}%" : e.Value.ToString("0.#");
        return e.EffectType switch
        {
            FormationEffectType.Buff => $"{stat} союзников +{value}",
            FormationEffectType.Debuff => $"{stat} врагов −{value}",
            FormationEffectType.Shield => $"щит: {stat} +{value}",
            FormationEffectType.Damage => $"урон врагам {value}",
            FormationEffectType.Heal => $"лечение союзников {value}",
            FormationEffectType.Control => $"контроль: {ControlLabel(e.ControlType)} {stat} врагов −{value}",
            FormationEffectType.Summon => $"призыв: {stat} +{value}",
            _ => $"{stat} {value}"
        };
    }

    private static string ControlLabel(ControlType c) => c switch
    {
        ControlType.Freeze => "заморозка",
        ControlType.Slow => "замедление",
        ControlType.Root => "корень",
        ControlType.Stun => "оглушение",
        _ => c.ToString()
    };

    private static string StatLabel(StatType s) => s switch
    {
        StatType.Damage => "урон",
        StatType.Speed => "скорость",
        StatType.Defense => "защита",
        StatType.Conductivity => "проводимость",
        StatType.Intelligence => "чутьё",
        StatType.AttackSpeed => "скорость атаки",
        _ => s.ToString()
    };
}
