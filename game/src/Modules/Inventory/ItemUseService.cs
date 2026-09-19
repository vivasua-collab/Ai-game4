#nullable enable
// Создано: 2026-09-19 — R19 «Потребляемые ресурсы»: единая маршрутизация
// использования предметов ПО ТИПУ (см. IItemUseService — аудит в шапке).
//
// Прецеденты архитектуры:
//   • EVT-01 (BeltService): кросс-модульные эффекты — сервисы игрока
//     (IBodyService/IQiService) инжектятся напрямую в сервис модуля Inventory.
//   • Тосты от модулей — GameWorldController подписан (этап 7:
//     «тосты от модулей (InventoryWindow.TryUseQiStone и др.)»).
//   • R10 P1-SlotId: действия над кучками — по стабильному Guid.
//
// Что здесь НЕ реализовано (заглушки маршрутизации, будущее):
//   • "material"  — трава-материал: не употребляется (крафт/алхимия).
//   • "teleport", "vitality_boost" — свитки/эликсиры будущих фаз.
//   • Растения и еда — добавятся как новые ключи эффектов (satiation и др.),
//     маршрутизация уже расширяема: неизвестный ключ = «не используется».
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Inventory;

/// <summary>
/// Маршрутизация «использования» предметов: камни Ци (поглощение + риск
/// хаоса), расходники heal/qi_restore. Ключи эффектов нормализуются в
/// нижний регистр (баг-фикс: генератор писал "Heal", потребители ждали "heal"
/// — сгенерированные лекарства не лечили даже через пояс).
/// </summary>
public sealed class ItemUseService : IItemUseService
{
    [Inject] private readonly IInventoryService _inventory = null!;
    [Inject] private readonly IItemDatabaseService _itemDb = null!;
    [Inject] private readonly IBodyService _body = null!;
    [Inject] private readonly IQiService _qi = null!;

    [Inject] private readonly IPublisher<ConsumableUsedEvent> _usedPub = null!;
    [Inject] private readonly IPublisher<ToastShownEvent> _toastPub = null!;

    /// <summary>Риск хаотичной Ци при поглощении: 10% −10% MaxHP (§10.2 канон).</summary>
    private const float ChaoticRiskChance = 0.10f;

    private const float ChaoticDamageMaxHpFraction = 0.10f;

    // === IItemUseService: маршрутизация ============================

    public ItemUseInfo GetUseInfo(ItemData item)
    {
        if (item == null) return ItemUseInfo.NotUsable("Предмет не определён");

        // 1) Камни Ци — поглощение Ци (этап 7 внедрения ЦИ, канон §10).
        if (item is QiStoneData stone)
        {
            string chaoticNote = stone.IsChaotic ? " · хаос: 10% риск −10% HP" : "";
            return new ItemUseInfo(
                usable: true,
                actionLabel: $"Поглотить Ци камня (1 шт.): +{stone.QiAmount} ед.{chaoticNote}",
                effectSummary: $"+{stone.QiAmount} Ци",
                unusableReason: string.Empty);
        }

        // 2) Расходники — по эффектам (нормализованные ключи).
        if (item.Category == ItemCategory.Consumable && item.Effects is { Count: > 0 })
        {
            bool hasUsable = false;
            var parts = new List<string>();
            foreach (var fx in item.Effects)
            {
                string key = Normalize(fx.EffectType);
                string? label = key switch
                {
                    "heal" => $"Лечение +{(int)fx.Value} HP",
                    "qi_restore" => $"+{(long)fx.Value} Ци",
                    _ => null, // material/teleport/... — не употребляется
                };
                if (label != null)
                {
                    hasUsable = true;
                    parts.Add(label);
                }
            }
            if (hasUsable)
                return new ItemUseInfo(true, string.Join(", ", parts), string.Join(", ", parts), string.Empty);
            return ItemUseInfo.NotUsable("Эффект не реализован (материал/будущие фазы)");
        }

        // 3) Всё остальное: материалы, экипировка, свитки техник, квестовые.
        return ItemUseInfo.NotUsable(item.Category switch
        {
            ItemCategory.Material => "Материал — не употребляется (крафт/алхимия)",
            ItemCategory.Technique => "Свиток изучается через книгу техник (T)",
            ItemCategory.Quest => "Квестовый предмет",
            _ => "Этот тип предметов не используется напрямую",
        });
    }

    // === IItemUseService: использование из инвентаря ================

    public bool TryUseFromInventory(Guid slotId, string expectedItemId)
    {
        if (slotId == Guid.Empty || string.IsNullOrEmpty(expectedItemId)) return false;
        if (!_itemDb.TryGetItem(expectedItemId, out var item) || item == null) return false;

        // Требуется хотя бы 1 шт. в адресованной кучке.
        int slotIndex = _inventory.FindSlotIndexBySlotId(slotId);
        if (slotIndex < 0)
        {
            PublishToast("Стак изменился — использование отменено");
            Console.WriteLine($"[ItemUse] Refused: slotId {slotId} not found (stale)");
            return false;
        }
        var slot = _inventory.GetAllSlots()[slotIndex];
        if (slot.IsEmpty || slot.ItemId != expectedItemId)
        {
            PublishToast("Стак изменился — использование отменено");
            Console.WriteLine($"[ItemUse] Refused: slotId {slotId} drifted ({slot.ItemId} != {expectedItemId})");
            return false;
        }

        // Слот-адресное списание ровно 1 шт. (R10 P1-SlotId).
        if (!_inventory.TryRemoveFromSlot(slotId, expectedItemId, 1))
        {
            PublishToast("Не удалось использовать предмет");
            Console.WriteLine($"[ItemUse] Failed to remove 1 from slotId {slotId} ({expectedItemId})");
            return false;
        }

        var applied = ApplyEffects(item);

        // События по эффектам (нормализованные ключи — консистентно для всех потребителей).
        foreach (var fx in applied.Items)
            _usedPub.Publish(new ConsumableUsedEvent(item.ItemId, fx.EffectType, fx.Value));

        // Обратная связь.
        PublishToast(BuildToast(item, applied));
        Console.WriteLine($"[ItemUse] Used '{item.NameRu}' ({item.ItemId}) from slot {slotId}: {DescribeEffects(applied)}");

        return true;
    }

    // === IItemUseService: применение эффектов (без списания) ========

    public AppliedEffects ApplyEffects(ItemData item)
    {
        var result = new AppliedEffects();
        if (item == null) return result;

        // Камень Ци: мгновенное поглощение (v1 этапа 7) + риск хаоса.
        if (item is QiStoneData stone)
        {
            long before = _qi?.CurrentQi ?? 0;
            _qi?.AddQi(stone.QiAmount);
            long gained = (_qi?.CurrentQi ?? 0) - before;
            result.Items.Add(new AppliedEffect("qi_restore", stone.QiAmount, gained));

            if (stone.IsChaotic)
            {
                var rng = new Random((int)DateTime.UtcNow.Ticks);
                if (rng.NextDouble() < ChaoticRiskChance)
                {
                    int damage = ApplyChaoticDamage();
                    result.ChaoticDamage = true;
                    result.ChaoticDamageAmount = damage;
                }
            }
            return result;
        }

        // Расходники: маршрутизация по нормализованному ключу эффекта.
        if (item.Effects == null) return result;
        foreach (var fx in item.Effects)
        {
            string key = Normalize(fx.EffectType);
            switch (key)
            {
                case "heal":
                {
                    int appliedHeal = HealWoundedParts((int)fx.Value);
                    result.Items.Add(new AppliedEffect(key, fx.Value, appliedHeal));
                    break;
                }
                case "qi_restore":
                {
                    long before = _qi?.CurrentQi ?? 0;
                    _qi?.AddQi((long)fx.Value);
                    long gained = (_qi?.CurrentQi ?? 0) - before;
                    result.Items.Add(new AppliedEffect(key, fx.Value, gained));
                    break;
                }
                default:
                    // material/teleport/vitality_boost/... — будущие фазы:
                    // ключ не маршрутизируется — эффект не применяется.
                    break;
            }
        }
        return result;
    }

    // === Внутренние механики ========================================

    /// <summary>
    /// Лечение: распределяем по самым повреждённым частям (прецедент —
    /// BeltService.ApplyEffect "heal", перенесено без изменения механики).
    /// </summary>
    private int HealWoundedParts(int amount)
    {
        int remaining = amount;
        int appliedTotal = 0;
        var parts = _body?.GetAllParts();
        if (parts == null) return 0;

        var ordered = new List<BodyPartData>(parts);
        ordered.Sort(static (a, b) => a.CurrentRedHP.CompareTo(b.CurrentRedHP));
        foreach (var p in ordered)
        {
            if (remaining <= 0) break;
            int missing = p.MaxRedHP - p.CurrentRedHP;
            if (missing <= 0) continue;
            int toHeal = Math.Min(missing, remaining);
            _body.HealPart(p.Type, toHeal);
            remaining -= toHeal;
            appliedTotal += toHeal;
        }
        return appliedTotal;
    }

    /// <summary>
    /// Урон хаотичной Ци: −10% MaxHP в торс (витальная часть — нагрузка на
    /// культивационное ядро). Перенесено из InventoryWindow.ApplyChaoticDamage.
    /// </summary>
    private int ApplyChaoticDamage()
    {
        if (_body == null) return 0;
        int maxHp = 0;
        var parts = _body.GetAllParts();
        if (parts == null) return 0;
        foreach (var p in parts) maxHp += p.MaxRedHP;
        if (maxHp <= 0) return 0;

        int damage = (int)Math.Max(1, maxHp * ChaoticDamageMaxHpFraction);
        _body.ApplyDamage(BodyPartType.Torso, damage);
        Console.WriteLine($"[ItemUse] Chaotic Qi damage: -{damage} HP (10% of {maxHp})");
        return damage;
    }

    /// <summary>Нормализация ключа эффекта: нижний регистр (баг "Heal" vs "heal").</summary>
    private static string Normalize(string effectType)
        => effectType?.Trim().ToLowerInvariant() ?? string.Empty;

    private static string DescribeEffects(AppliedEffects applied)
    {
        if (applied.Items.Count == 0) return "эффекты не применены";
        var parts = new List<string>();
        foreach (var fx in applied.Items)
            parts.Add(fx.EffectType == "heal"
                ? $"heal {fx.Applied:F0}/{fx.Value:F0} HP"
                : $"{fx.EffectType} +{fx.Applied:F0}");
        if (applied.ChaoticDamage)
            parts.Add($"ХАОС: −{applied.ChaoticDamageAmount} HP");
        return string.Join(", ", parts);
    }

    private static string BuildToast(ItemData item, AppliedEffects applied)
    {
        var parts = new List<string>();
        foreach (var fx in applied.Items)
        {
            if (fx.EffectType == "heal")
                parts.Add(fx.Applied > 0
                    ? $"❤ Лечение +{fx.Applied:F0} HP"
                    : "❤ Тело не требует лечения");
            else if (fx.EffectType == "qi_restore")
                parts.Add($"+{fx.Applied:F0} Ци");
        }
        if (parts.Count == 0)
            parts.Add("без эффекта");

        string msg = $"{item.NameRu}: {string.Join(", ", parts)}";
        if (applied.ChaoticDamage)
            msg += $" — 💥 хаотичная Ци ранила вас! (−{applied.ChaoticDamageAmount} HP)";
        return msg;
    }

    private void PublishToast(string message)
    {
        Console.WriteLine($"[ItemUse] {message}");
        _toastPub?.Publish(new ToastShownEvent(message, 2.5f));
    }
}
