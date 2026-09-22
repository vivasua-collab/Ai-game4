#nullable enable
using System;
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
public sealed class StatService : IStatService, ISaveable, IWorldResettable
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

    // ── R35 (Фаза 14 / P1-20 + P2-49): конвейер развития по канону ──
    // STAT_THRESHOLD_SYSTEM.md §2/§4.2/§9: дельты — ТОЛЬКО для первичных
    // статов, НЕотрицательные, с капами (STR/AGI/VIT 10.0, INT 15.0).
    // Прежде AddVirtualDelta принимал ЛЮБЫЕ значения (отрицательные,
    // вторичные статы, без потолка) — при появлении продюсеров это
    // превращалось бы в дыру инвариантов.
    private static readonly Dictionary<StatType, float> VirtualDeltaCaps = new()
    {
        [StatType.Strength] = 10f,
        [StatType.Agility] = 10f,
        [StatType.Vitality] = 10f,
        [StatType.Intelligence] = 15f,
    };

    /// <summary>Только первичные статы развиваются физическими действиями (§2).</summary>
    private static bool IsPrimaryStat(StatType type)
        => type is StatType.Strength or StatType.Agility
                    or StatType.Vitality or StatType.Intelligence;

    public void AddVirtualDelta(StatType type, float amount)
    {
        // P2-49: инварианты — не-отрицательная, только первичная, с капом.
        if (amount <= 0f) return; // отрицательные/нулевые — не прогресс
        if (!IsPrimaryStat(type)) return; // вторичные не развиваются (§2)
        _virtualDelta.TryGetValue(type, out var cur);
        // §4.2: «Если дельта достигла капа — дальнейший прирост невозможен,
        // пока часть не будет закреплена сном».
        float cap = VirtualDeltaCaps.TryGetValue(type, out var c) ? c : 10f;
        float next = Math.Min(cur + amount, cap);
        if (next <= cur) return;
        _virtualDelta[type] = next;
    }

    /// <summary>
    /// R35 (Фаза 14 / P1-20 + P2-43) ФИКС: закрепление при сне — ПО КАНОНУ
    /// STAT_THRESHOLD_SYSTEM §6:
    ///   1. Минимум 4 часа — иначе закрепления нет (дельта сохраняется).
    ///   2. consolidated = min(virtualDelta, sleepHours × 0.025) — переносится
    ///      в реальную характеристику (дробно; максимум +0.20 при 8ч).
    ///   3. ОСТАТОК дельты сохраняется (прежде — Clear() терял прогресс).
    ///   4. §6.4: после закрепления, пока delta ≥ threshold — стат +1,
    ///      delta -= threshold (дискретный шаг развития).
    /// Прежде: virtualDelta × 0.20 + Clear() — формула игнорировала часы,
    /// остаток терялся, порог не проверялся.
    /// </summary>
    public void ConsolidateSleep(float hours)
    {
        if (hours < MinSleepHours) return; // §6.1: минимум 4 часа

        // Снапшот: внутри цикла мутируем словарь (Remove/перезапись дельты).
        // Путь холодный (сон 1-2 раза в игровые сутки) — аллокация ок.
        var snapshot = new List<KeyValuePair<StatType, float>>(_virtualDelta);
        foreach (var kvp in snapshot)
        {
            StatType type = kvp.Key;
            if (!IsPrimaryStat(type)) continue; // чужие записи не трогаем
            float delta = kvp.Value;
            if (delta <= 0f) continue;

            // §6.2: перенос в стат (дробный).
            float consolidate = Math.Min(delta, hours * ConsolidationPerHour);
            if (consolidate > 0f)
            {
                _base.TryGetValue(type, out var cur);
                float next = Math.Min(cur + consolidate, Core.Data.GameConstants.MAX_STAT_VALUE);
                _base[type] = next;
                // AUDIT-0911: закрепление сна — тоже мутация статов → событие
                // (BodyModule пересчитает HP при росте VIT).
                PublishStatChanged(type, cur, next);
                delta -= consolidate;
            }

            // §6.4: дискретный шаг повышения, пока дельты хватает на порог.
            while (delta >= GetThreshold(type) && delta > 0f)
            {
                float threshold = GetThreshold(type);
                _base.TryGetValue(type, out var cur);
                if (cur >= Core.Data.GameConstants.MAX_STAT_VALUE) break; // M-35 жёсткий кап
                _base[type] = Math.Min(cur + 1f, Core.Data.GameConstants.MAX_STAT_VALUE);
                PublishStatChanged(type, cur, _base[type]);
                delta -= threshold;
            }

            if (delta <= 0f) _virtualDelta.Remove(type);
            else _virtualDelta[type] = delta;
        }
    }

    /// <summary>§6.1: минимум часов сна для закрепления.</summary>
    public const float MinSleepHours = 4f;

    /// <summary>§6.2: скорость закрепления (максимум +0.20 за 8ч).</summary>
    public const float ConsolidationPerHour = 0.025f;

    public float GetThreshold(StatType type)
    {
        // R35 (Фаза 14 / P1-20) ФИКС: порог — по канону §3.1
        // threshold = max(1.0, floor(currentStat / 10)). Прежде — константа
        // 100 (никакой producer её не мог бы преодолеть). Явный override
        // (SetThreshold — модификаторы §3.3 будущего) имеет приоритет.
        if (_threshold.TryGetValue(type, out var stored)) return stored;
        if (!IsPrimaryStat(type)) return 100f; // вторичные не развиваются — порог неважен
        float stat = GetStat(type);
        return MathF.Max(1f, MathF.Floor(stat / 10f));
    }

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

    // ══════════════════════════════════════════════════════════════
    // R17: ISaveable — блок "stats". Рост статов (сон ConsolidateSleep,
    // перки, модификаторы) ДО R17 терялся при загрузке — статы
    // сбрасывались к дефолтам InitializeDefaults. Бонусы экипировки НЕ
    // храним: они пересчитываются из предмета при каждой экипировке
    // (хранение = риск рассинхрона с фактически надетым).
    // ══════════════════════════════════════════════════════════════

    public sealed class StatsSaveState
    {
        public string EntityId = "";
        public List<StatSaveEntry> Base = new();
        public List<StatSaveEntry> VirtualDelta = new();
        public List<StatSaveEntry> Threshold = new();
    }

    public sealed class StatSaveEntry
    {
        public int Stat;
        public float Value;
    }

    public string SaveKey => "stats";
    public Type StateType => typeof(StatsSaveState);

    public object CaptureState()
    {
        var data = new StatsSaveState { EntityId = _entityId };
        foreach (var kvp in _base)
            data.Base.Add(new StatSaveEntry { Stat = (int)kvp.Key, Value = kvp.Value });
        foreach (var kvp in _virtualDelta)
            data.VirtualDelta.Add(new StatSaveEntry { Stat = (int)kvp.Key, Value = kvp.Value });
        foreach (var kvp in _threshold)
            data.Threshold.Add(new StatSaveEntry { Stat = (int)kvp.Key, Value = kvp.Value });
        return data;
    }

    public void RestoreState(object state)
    {
        if (state is not StatsSaveState data || data == null) return;

        if (!string.IsNullOrEmpty(data.EntityId)) _entityId = data.EntityId;

        // Старые значения — для честных old→new событий (PublishStatChanged
        // гасит холостые old==new; при сбросе в дефолт и восстановлении
        // тех же значений событие и не нужно).
        var oldBase = new Dictionary<StatType, float>(_base);

        _base.Clear();
        _virtualDelta.Clear();
        _threshold.Clear();

        foreach (var e in data.Base)
            if (Enum.IsDefined(typeof(StatType), e.Stat))
                _base[(StatType)e.Stat] = e.Value;
        foreach (var e in data.VirtualDelta)
            if (Enum.IsDefined(typeof(StatType), e.Stat))
                _virtualDelta[(StatType)e.Stat] = e.Value;
        foreach (var e in data.Threshold)
            if (Enum.IsDefined(typeof(StatType), e.Stat))
                _threshold[(StatType)e.Stat] = e.Value;

        // События с РЕАЛЬНЫМ diff → BodyModule пересчитает VIT→HP (П.24).
        foreach (var kvp in _base)
        {
            oldBase.TryGetValue(kvp.Key, out var oldVal);
            PublishStatChanged(kvp.Key, oldVal, kvp.Value);
        }

        Console.WriteLine($"[StatService] RestoreState: base={_base.Count}, virtual={_virtualDelta.Count}, threshold={_threshold.Count}");
    }

    // R17 (E-1): пересборка мира = врождённые дефолты (10/10/10/10, Luck 5).
    public void ResetWorld()
    {
        _base.Clear();
        _bonus.Clear();
        _virtualDelta.Clear();
        _threshold.Clear();
        InitializeDefaults(_entityId);
    }
}
