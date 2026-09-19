#nullable enable
// Создано: 2026-09-19 — R19 «Потребляемые ресурсы»: маршрутизация использования
// предметов ПО ТИПУ (запрос пользователя): «кнопка "Использовать", эффект
// которой зависит от характеристик объекта и возможности его использования.
// Кроме лекарств будут растения, свитки, еда — маршрутизация и эффекты должны
// зависеть от типа объекта».
//
// Аудит перед реализацией (2026-09-19):
//   • BeltService.ApplyEffect — heal/qi_restore УЖЕ реализованы, но работают
//     ТОЛЬКО через пояс (хотбар 3-9). Без пояса лекарство потребить нельзя.
//   • InventoryWindow.TryUseQiStone — поглощение Ци только для камней Ци.
//   • ItemGeneratorService пишет EffectType="Heal" (заглавная), потребители
//     switch-ят по "heal" — сгенерированные лекарства не лечили даже с поясом.
// IItemUseService — единая точка правды: нормализация ключей + маршрутизация.
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Interfaces;

/// <summary>
/// Результат проверки «можно ли использовать» + описание действия для UI.
/// </summary>
public readonly struct ItemUseInfo
{
    /// <summary>Предмет можно использовать (показывать кнопку «Использовать»).</summary>
    public readonly bool Usable;

    /// <summary>Текст действия для tooltip (например, «Поглотить Ци камня (1 шт.)»).</summary>
    public readonly string ActionLabel;

    /// <summary>Краткое описание эффекта для tooltip («Лечение +15»).</summary>
    public readonly string EffectSummary;

    /// <summary>Причина отказа — для tooltip выключенной кнопки / лога.</summary>
    public readonly string UnusableReason;

    public ItemUseInfo(bool usable, string actionLabel, string effectSummary, string unusableReason)
    {
        Usable = usable;
        ActionLabel = actionLabel;
        EffectSummary = effectSummary;
        UnusableReason = unusableReason;
    }

    public static ItemUseInfo NotUsable(string reason)
        => new(false, string.Empty, string.Empty, reason);
}

/// <summary>Применённый эффект (нормализованный ключ + величина + фактический вклад).</summary>
public readonly struct AppliedEffect
{
    /// <summary>Нормализованный (нижний регистр) ключ эффекта: "heal", "qi_restore".</summary>
    public readonly string EffectType;

    /// <summary>Заявленная величина эффекта (из данных предмета).</summary>
    public readonly float Value;

    /// <summary>Фактически применённая величина (может быть меньше: heal по раненым частям).</summary>
    public readonly float Applied;

    public AppliedEffect(string effectType, float value, float applied)
    {
        EffectType = effectType;
        Value = value;
        Applied = applied;
    }
}

/// <summary>Сводка применённых эффектов одного использования.</summary>
public sealed class AppliedEffects
{
    public List<AppliedEffect> Items { get; } = new();

    /// <summary>Хаотичная Ци камня ранила практика (для тоста/лога).</summary>
    public bool ChaoticDamage { get; set; }

    /// <summary>Величина урона хаотичной Ци (0 если не было).</summary>
    public int ChaoticDamageAmount { get; set; }
}

/// <summary>
/// Единая маршрутизация «использования» предметов по типу объекта.
/// Потребители: ПКМ-контекстное меню инвентаря (кнопка «Использовать»),
/// BeltService (слоты пояса 3-9), будущие растения/еда/свитки.
/// </summary>
public interface IItemUseService
{
    /// <summary>
    /// Проверить используемость предмета и получить описание действия.
    /// Маршрутизация: камни Ци → поглощение; расходники с эффектами
    /// heal/qi_restore → применение; материалы/экипировка/свитки → нельзя.
    /// </summary>
    ItemUseInfo GetUseInfo(ItemData item);

    /// <summary>
    /// Использовать 1 шт. из КОНКРЕТНОЙ кучки инвентаря (R10 P1-SlotId:
    /// действие по стабильному SlotId + expectedItemId; при мутации/подмене
    /// кучки — отказ, инвентарь не тронут). Списывает 1 шт., применяет
    /// эффекты, публикует ConsumableUsedEvent и тост.
    /// </summary>
    bool TryUseFromInventory(Guid slotId, string expectedItemId);

    /// <summary>
    /// Применить эффекты предмета к игроку БЕЗ списания (внешние хранилища:
    /// слот пояса уже изъял предмет). Возвращает нормализованную сводку
    /// для событий/тостов. Неизвестные типы эффектов игнорируются
    /// (маршрутизация расширяема — растения/еда будущих фаз).
    /// </summary>
    AppliedEffects ApplyEffects(ItemData item);
}
