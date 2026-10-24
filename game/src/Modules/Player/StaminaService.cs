#nullable enable
// Создано: 2026-10-24 — Этап-17 аудита (ST17-001/002/003): реализация
// IStaminaService — полная подсистема выносливости игрока.
//
// ST17-001: интерфейс существовал с 2026-05-28 (Core/Interfaces), но НЕ имел
// ни реализации, ни DI-регистрации, ни потребителей — мёртвый контракт
// (префикс Stage17-сима: 8 FAIL). Канонический слой — Modules/Player
// (домен игрока), регистрация — PlayerModuleServices, потребление —
// PlayerModule.Tick (реген) и будущие действия (запланированный расход бега
// — HOTKEYS.md «Нет стамины бега: 📋 Запланировано»).
//
// ST17-002: ЗАПРЕТ 3.9 — целочисленная арифметика, промилле ×1000.
// Внутренняя модель: _currentMilli = текущая ×1000 (milli-единицы) —
// дробный реген без потерь; все внешние значения целые.
//   CurrentPromille = _currentMilli / _max  (1000‰ = полная, 0‰ = истощение)
//   Spend(p): стоимость = p × _max milli (p‰ от максимума); диапазон [1..1000],
//     недостаток = отказ БЕЗ частичного списания; события только при изменении.
//   Regenerate(deltaTimeMs): аккумулятор «scale-остаток»:
//     a += num'·ms; gain = a/den'; a %= den'  — floor(суммы частей) == floor(целого),
//     короткие частые вызовы == один длинный (ST17-002/D). Режимы скорости —
//     из PlayerConfig: сон (SleepStaminaRecoveryPercentPerHour, %/ч → ‰/ч) и
//     бодрствование (StaminaAwakeRegenPromillePerMinute, ‰/мин). Переключение
//     режима сбрасывает аккумулятор (документированная потеря ≤ 0.001 ед.).
//     Время — ИГРОВОЕ: 1 игровая минута = 60 000, 1 час = 3 600 000
//     (канон TIME_SYSTEM §5: 1 тик = 1 игровая минута; PlayerModule передаёт
//     DeltaTime × 60 000).
//
// ST17-003: жизненный цикл — ISaveable (блок "stamina", RestoreState идёт в
// хвосте после player — порядок регистраций) + IWorldResettable (New Game /
// LoadGame reset → полный ресурс). Death/respawn НЕ восстанавливают ресурс
// автоматически: канон такого требования не содержит (HOTKEYS/BODY/TIME —
// нет контракта «респавн = полная стамина»), поведение задокументировано:
// ресурс переживает смерть/возрождение неизменным и продолжает реген.
using System;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Player;

/// <summary>
/// Реализация <see cref="IStaminaService"/> — выносливость ИГРОКА
/// (per-игрок, не NPC: интерфейс описывает ресурс игрока). Целочисленная
/// модель с milli-единицами внутри; все публичные значения — int.
/// </summary>
public sealed class StaminaService : IStaminaService, ISaveable, IWorldResettable
{
    private const int PromilleFull = 1000;
    private const long MsPerGameMinute = 60_000;
    private const long MsPerGameHour = 3_600_000;

    /// <summary>Канонический дефолт максимума (если конфиг невалиден).</summary>
    private const int DefaultMaxStamina = 100;

    /// <summary>Верхний кламп конфига (защита аккумулятора от переполнений).</summary>
    private const int MaxStaminaConfigCap = 2_000_000;

    /// <summary>Верхний кламп скоростей регена из конфига (‰/мин и %/ч).</summary>
    private const float RegenRateConfigCap = 10_000f;

    [Inject] private readonly IPublisher<StaminaChangedEvent> _changedPub = null!;
    // Сон-режим регена. IPlayerService — сервис ТОГО ЖЕ модуля (Player):
    // инъекция через Core-интерфейс корректна (архитектурный запрет —
    // IQi/IBody/ICombat; прецедент — StatService в PlayerService).
    [Inject] private readonly IPlayerService? _player = null;
    [Inject] private readonly PlayerConfig? _config = null;

    private bool _initialized;
    private int _max;               // абсолютные единицы (из PlayerConfig)
    private long _currentMilli;     // текущая ×1000
    private long _lastPublishedMilli = long.MinValue; // для «события только при изменении»

    // Реген-аккумулятор (ST17-002): num/den — НОД-редуцированная скорость
    // (num milli за den ms); _regenAccumulator накапливает num'·ms и
    // отдаёт целые порции gain = a/den', сохраняя остаток — сумма floor
    // частей тождественно равна floor целого (нет дрейфа округления).
    private long _regenAccumulator;
    private long _regenNum;
    private long _regenDen = 1;
    private bool _regenSleepMode;
    // Признак «скорость настроена»: после EnsureInitialized аккумулятор ещё
    // НЕ сконфигурирован (num=0) — первый Regenerate обязан настроить скорость,
    // а не «узнать» режим по умолчанию (баг ревизии-1: _regenSleepMode=false
    // совпадал с бодрствованием → первая настройка пропускалась → реген
    // бодрствования был мёртв до первого сна; пруф — [D1] 60×1000мс → 0).
    private bool _regenConfigured;

    // === IStaminaService ===

    /// <summary>Текущая выносливость (целые абсолютные единицы).</summary>
    public int CurrentStamina
    {
        get { EnsureInitialized(); return (int)Math.Min(_currentMilli / 1000, _max); }
    }

    /// <summary>Максимальная выносливость (абсолютные единицы, из PlayerConfig).</summary>
    public int MaxStamina
    {
        get { EnsureInitialized(); return _max; }
    }

    /// <summary>Промилле от максимума [0..1000]; нулевой максимум → 0 (без деления на ноль).</summary>
    public int CurrentPromille
    {
        get
        {
            EnsureInitialized();
            if (_max <= 0) return 0;
            long promille = _currentMilli / _max;
            if (promille < 0) promille = 0;
            if (promille > PromilleFull) promille = PromilleFull;
            return (int)promille;
        }
    }

    /// <summary>Истощение: CurrentPromille == 0 (согласован с текущим ресурсом).</summary>
    public bool IsExhausted => CurrentPromille <= 0;

    /// <summary>
    /// Расход выносливости: <paramref name="amountPromille"/>‰ от максимума.
    /// Контракт [1..1000]: некорректные аргументы — отказ БЕЗ изменения
    /// состояния (обход ограничений невозможен). Недостаток ресурса — отказ
    /// БЕЗ частичного списания. Событие — только при фактическом изменении.
    /// </summary>
    public bool Spend(int amountPromille)
    {
        EnsureInitialized();
        if (amountPromille < 1 || amountPromille > PromilleFull) return false;
        if (_max <= 0) return false;

        long costMilli = (long)amountPromille * _max; // p‰ × max = p·max milli
        if (_currentMilli < costMilli) return false;  // недостаток: не списываем

        _currentMilli -= costMilli;
        PublishIfChanged();
        return true;
    }

    /// <summary>
    /// Регенерация за шаг игрового времени (миллисекунды игрового времени:
    /// 1 игровая минута = 60 000). Скорость — по режиму: сон
    /// (SleepStaminaRecoveryPercentPerHour) или бодрствование
    /// (StaminaAwakeRegenPromillePerMinute). Нулевая/отрицательная дельта —
    /// no-op. Полный ресурс — аккумулятор сбрасывается (документировано).
    /// </summary>
    public void Regenerate(int deltaTimeMs)
    {
        EnsureInitialized();
        if (deltaTimeMs <= 0) return;
        ConfigureAccumulatorForMode();
        ApplyRegen(deltaTimeMs);
    }

    /// <summary>
    /// Установить текущую выносливость напрямую (загрузка/читы).
    /// Отрицательные значения клампятся к 0, сверх максимума — к максимуму.
    /// </summary>
    public void SetCurrent(int current)
    {
        EnsureInitialized();
        long units = Math.Clamp((long)current, 0, _max);
        _currentMilli = units * 1000;
        PublishIfChanged();
    }

    // === Внутренняя модель ===

    private void EnsureInitialized()
    {
        if (_initialized) return;

        int max = _config?.StaminaMax ?? DefaultMaxStamina;
        if (max < 1)
        {
            Console.WriteLine($"[StaminaService] PlayerConfig.StaminaMax={max} < 1 — некорректен, fallback {DefaultMaxStamina} (ST17-008)");
            max = DefaultMaxStamina;
        }
        if (max > MaxStaminaConfigCap)
        {
            Console.WriteLine($"[StaminaService] PlayerConfig.StaminaMax={max} > {MaxStaminaConfigCap} — кламп (защита аккумулятора от переполнений)");
            max = MaxStaminaConfigCap;
        }

        _max = max;
        _currentMilli = (long)max * 1000; // новая сессия = полный ресурс (ST17-002/B)
        _regenAccumulator = 0;
        _regenNum = 0;
        _regenDen = 1;
        _regenSleepMode = false;
        _regenConfigured = false;
        _initialized = true;
        Console.WriteLine($"[StaminaService] Инициализация: Max={_max}, полный ресурс 1000‰ (сон/бодрствование из PlayerConfig)");
    }

    /// <summary>
    /// Настроить аккумулятор под текущий режим (сон/бодрствование).
    /// Переключение режима сбрасывает накопленный суб-единичный остаток
    /// (потеря ≤ 0.001 единицы; переключения редки — сон/пробуждение).
    /// Скорости: num = ‰-скорость × max (milli за период), den = период (ms),
    /// затем НОД-редукция (минимизация риска переполнения num'·ms).
    /// </summary>
    private void ConfigureAccumulatorForMode()
    {
        bool sleeping = _player != null && _player.IsSleeping;
        if (_regenConfigured && sleeping == _regenSleepMode && _regenDen > 0) return;

        _regenSleepMode = sleeping;
        _regenAccumulator = 0;
        _regenConfigured = true;

        long num;
        long den;
        if (sleeping)
        {
            // Сон: PlayerConfig.SleepStaminaRecoveryPercentPerHour (%/ч от max)
            // → ‰/ч (×10) → milli за час (‰ × max).
            float percentPerHour = _config?.SleepStaminaRecoveryPercentPerHour ?? 100f;
            if (percentPerHour < 0f) percentPerHour = 0f;
            if (percentPerHour > RegenRateConfigCap)
            {
                Console.WriteLine($"[StaminaService] SleepStaminaRecoveryPercentPerHour={percentPerHour} > {RegenRateConfigCap} — кламп");
                percentPerHour = RegenRateConfigCap;
            }
            num = (long)(percentPerHour * 10f) * _max;
            den = MsPerGameHour;
        }
        else
        {
            // Бодрствование: PlayerConfig.StaminaAwakeRegenPromillePerMinute.
            float promillePerMinute = _config?.StaminaAwakeRegenPromillePerMinute ?? 25f;
            if (promillePerMinute < 0f) promillePerMinute = 0f;
            if (promillePerMinute > RegenRateConfigCap)
            {
                Console.WriteLine($"[StaminaService] StaminaAwakeRegenPromillePerMinute={promillePerMinute} > {RegenRateConfigCap} — кламп");
                promillePerMinute = RegenRateConfigCap;
            }
            num = (long)promillePerMinute * _max;
            den = MsPerGameMinute;
        }

        if (num <= 0)
        {
            // Нулевая скорость: реген отключён конфигом — аккумулятор нейтрален.
            _regenNum = 0;
            _regenDen = 1;
            return;
        }

        long g = Gcd(num, den);
        if (g > 1) { num /= g; den /= g; }
        _regenNum = num;
        _regenDen = den;
    }
    /// <summary>
    /// Применить реген: точная целочисленная схема «scale-остаток» с
    /// чанкованием против переполнения (num'·ms ≤ long.MaxValue/8).
    /// Полный ресурс — стоп + сброс аккумулятора (документированный инвариант).
    /// </summary>
    private void ApplyRegen(long ms)
    {
        if (_regenNum <= 0 || _regenDen <= 0) { PublishIfChanged(); return; }

        long remaining = ms;
        while (remaining > 0)
        {
            long chunk = remaining;
            // Защита от переполнения num'·chunk: дробим шаг.
            if (_regenNum > 1 && chunk > long.MaxValue / 8 / _regenNum)
                chunk = Math.Max(1, long.MaxValue / 8 / _regenNum);

            _regenAccumulator += _regenNum * chunk;
            long gain = _regenAccumulator / _regenDen;
            _regenAccumulator -= gain * _regenDen;
            remaining -= chunk;

            if (gain > 0)
            {
                long room = (long)_max * 1000 - _currentMilli;
                if (gain >= room)
                {
                    _currentMilli = (long)_max * 1000;
                    _regenAccumulator = 0; // полный ресурс — суб-единичный долг не копим
                    PublishIfChanged();
                    return;
                }
                _currentMilli += gain;
            }
        }
        PublishIfChanged();
    }

    private void PublishIfChanged()
    {
        if (_currentMilli == _lastPublishedMilli) return;
        _lastPublishedMilli = _currentMilli;
        _changedPub?.Publish(new StaminaChangedEvent(CurrentPromille, CurrentStamina, MaxStamina));
    }

    private static long Gcd(long a, long b)
    {
        while (b != 0) { long t = a % b; a = b; b = t; }
        return a < 0 ? -a : a;
    }

    // ══════════════════════════════════════════════════════════════
    // ST17-003: ISaveable — блок "stamina". RestoreOrder-хвост (после
    // player) — ресурс не зависит от порядка восстановления доменов.
    // Absent-блок в легаси-сейвах = пост-ресет дефолт (OptionalBlocksV1).
    // ══════════════════════════════════════════════════════════════

    /// <summary>Типизированный state-блок для round-trip десериализации.</summary>
    public sealed class StaminaSaveState
    {
        public int Max;          // контрольное поле (источник истины — PlayerConfig)
        public long CurrentMilli;// текущая ×1000 — точное восстановление
    }

    public string SaveKey => "stamina";
    public Type StateType => typeof(StaminaSaveState);

    public object CaptureState()
    {
        EnsureInitialized();
        return new StaminaSaveState { Max = _max, CurrentMilli = _currentMilli };
    }

    public void RestoreState(object state)
    {
        if (state is not StaminaSaveState data || data == null) return;
        EnsureInitialized();

        // Максимум — из КОНФИГА (источник истины); сейвовое поле — контроль.
        // Точное восстановление значения при неизменном конфиге; при
        // изменившемся максимуме — кламп к новому пределу + диагностика.
        _currentMilli = Math.Clamp(data.CurrentMilli, 0, (long)_max * 1000);
        if (data.Max > 0 && data.Max != _max)
        {
            Console.WriteLine($"[StaminaService] RestoreState: сейв Max={data.Max} ≠ конфиг Max={_max} — значение клампится к конфигу");
        }
        // Суб-единичный прогресс регена (аккумулятор < den' ≈ ≤0.001 ед.) не
        // переносится:observable-состояние (CurrentMilli) восстанавливается точно.
        _regenAccumulator = 0;
        PublishIfChanged();
        Console.WriteLine($"[StaminaService] RestoreState: Current={CurrentStamina}/{_max} ({CurrentPromille}‰)");
    }

    // ST17-003: IWorldResettable — New Game / LoadGame (reset до RestoreState)
    // пересоздают ресурс новой сессии: конфиг перечитывается, ресурс полный.
    public void ResetWorld()
    {
        _initialized = false;
        EnsureInitialized();
        PublishIfChanged();
        Console.WriteLine($"[StaminaService] ResetWorld: ресурс новой сессии (Current={CurrentStamina}/{_max}, 1000‰)");
    }
}
