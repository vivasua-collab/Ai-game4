#nullable enable
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Player;

/// <summary>
/// StatService — per-stat value store with additive bonuses, virtual delta
/// (for sleep consolidation) and threshold gates (CanAdvance).
///
/// Implements <see cref="IStatService"/>. All operations are keyed by
/// <see cref="StatType"/> (single player scope in V1; multi-entity support
/// to be added when NPC stats are migrated).
///
/// AUDIT-0911 PLR-2/C-1/PLR-9/BOD-8 FIX: раньше никто не вызывал
/// SetBaseStat → GetStat всегда 0 → CombatService получал STR/AGI/INT/Luck=0
/// (AGI-ускорение атаки §8.2 мертво), а ModifyStat/SetStat НЕ публиковали
/// StatChangedEvent (контракт интерфейса обещал!) → подписка BodyModule.
/// OnStatChanged → RecalculateHPFromVitality (П.24: VIT→HP) была мёртвой.
/// Теперь: InitializeDefaults задаёт врождённые статы игрока (канон
/// STAT_THRESHOLD_SYSTEM: базовые ~10 — порог floor(stat/10) от 10),
/// мутации публикуют событие → пересчёт HP живёт.
/// </summary>
public sealed class StatService : IStatService
{
    private readonly Dictionary<StatType, float> _base = new();
    private readonly Dictionary<StatType, float> _bonus = new();
    private readonly Dictionary<StatType, float> _virtualDelta = new();
    private readonly Dictionary<StatType, float> _threshold = new();

    // AUDIT-0911: издатель StatChangedEvent (контракт IStatService «с
    // публикацией StatChangedEvent» — раньше не выполнялся).
    private readonly IPublisher<StatChangedEvent>? _statChangedPub;

    // AUDIT-0911: владелец статов (Body-домен игрока — "player";
    // BodyService.EntityId сравнивается с этим ID через AreSameEntity).
    private string _entityId = "player";

    public StatService() { }

    public StatService(IPublisher<StatChangedEvent> statChangedPub)
    {
        _statChangedPub = statChangedPub;
    }

    /// <summary>
    /// AUDIT-0911 PLR-2: врождённые статы игрока. Канон — SoulGenerator
    /// DefaultBaseStats[SoulType.Character] = (10,10,10,10): игрок —
    /// обычный персонаж, стартовый уровень 1. Luck — 5 (NPC-волк AGI 14
    /// даёт волку честное превосходство в уклонении на старте).
    /// Идемпотентно: существующие значения не затираются (тёплый рестарт
    /// сессии не сбрасывает рост статов).
    /// </summary>
    public void InitializeDefaults(string entityId)
    {
        if (!string.IsNullOrEmpty(entityId)) _entityId = entityId;

        SetDefault(StatType.Strength, 10f);
        SetDefault(StatType.Agility, 10f);
        SetDefault(StatType.Vitality, 10f);
        SetDefault(StatType.Intelligence, 10f);
        SetDefault(StatType.Perception, 10f);
        SetDefault(StatType.Luck, 5f);
    }

    private void SetDefault(StatType type, float value)
    {
        if (!_base.ContainsKey(type)) _base[type] = value;
    }

    /// <summary>Internal — set base value (e.g. from CharacterData on spawn).</summary>
    public void SetBaseStat(StatType type, float value)
    {
        _base.TryGetValue(type, out var old);
        _base[type] = value;
        PublishStatChanged(type, old, value);
    }

    /// <summary>Internal — add a bonus (e.g. from equipment).</summary>
    public void AddBonus(StatType type, float value)
    {
        _bonus.TryGetValue(type, out var cur);
        _bonus[type] = cur + value;
    }

    /// <summary>Internal — remove a bonus.</summary>
    public void RemoveBonus(StatType type, float value)
    {
        if (!_bonus.TryGetValue(type, out var cur)) return;
        float newVal = cur - value;
        if (System.Math.Abs(newVal) < 1e-6f) _bonus.Remove(type);
        else _bonus[type] = newVal;
    }

    /// <summary>Internal — set threshold (e.g. from config).</summary>
    public void SetThreshold(StatType type, float value) => _threshold[type] = value;

    // === IStatService ===

    public float GetStat(StatType type)
        => _base.TryGetValue(type, out var v) ? v : 0f;

    public float GetStatBonus(StatType type)
        => _bonus.TryGetValue(type, out var v) ? v : 0f;

    public void ModifyStat(StatType type, float delta)
    {
        _base.TryGetValue(type, out var cur);
        _base[type] = cur + delta;
        // AUDIT-0911 C-1/PLR-9: контракт интерфейса — публикация события
        // (BodyModule П.24: VIT→HP пересчёт; раньше подписка была мёртвой).
        PublishStatChanged(type, cur, cur + delta);
    }

    public void SetStat(StatType type, float value)
    {
        _base.TryGetValue(type, out var old);
        _base[type] = value;
        PublishStatChanged(type, old, value);
    }

    public StatDomain GetStatDomain(StatType type) => type switch
    {
        StatType.Intelligence or StatType.Perception or StatType.Luck
            or StatType.CritChance or StatType.CritDamage
            or StatType.Conductivity or StatType.QiEfficiency or StatType.QiCost
            or StatType.QiRestoration or StatType.Cooldown => StatDomain.Soul,
        _ => StatDomain.Body,
    };

    public float GetVirtualDelta(StatType type)
        => _virtualDelta.TryGetValue(type, out var v) ? v : 0f;

    public void AddVirtualDelta(StatType type, float amount)
    {
        _virtualDelta.TryGetValue(type, out var cur);
        _virtualDelta[type] = cur + amount;
    }

    public void ConsolidateSleep(float hours)
    {
        // V1: convert virtual delta to base stat at the configured rate.
        // Real formula lives in StatCalculator; this is a placeholder.
        if (hours < 4f) return;
        foreach (var kvp in _virtualDelta)
        {
            float consolidate = kvp.Value * 0.20f;
            _base.TryGetValue(kvp.Key, out var cur);
            _base[kvp.Key] = cur + consolidate;
            // AUDIT-0911: закрепление сна — тоже мутация статов → событие
            // (BodyModule пересчитает HP при росте VIT).
            PublishStatChanged(kvp.Key, cur, cur + consolidate);
        }
        _virtualDelta.Clear();
    }

    public float GetThreshold(StatType type)
        => _threshold.TryGetValue(type, out var v) ? v : 100f;

    public bool CanAdvance(StatType type)
    {
        if (!_virtualDelta.TryGetValue(type, out var delta)) return false;
        return delta >= GetThreshold(type);
    }

    private void PublishStatChanged(StatType type, float oldValue, float newValue)
    {
        if (_statChangedPub == null) return;
        if (oldValue.Equals(newValue)) return; // холостое событие не шумим
        _statChangedPub.Publish(new StatChangedEvent(_entityId, type, oldValue, newValue));
    }
}
