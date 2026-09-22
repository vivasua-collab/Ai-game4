#nullable enable
// Создано: 2026-09-22 — R35 (внешний аудит 09.22 12:00, Фаза 11): P1-13.
// Чистая математика fixed-timestep catch-up, извлечённая из GameBoot._
// PhysicsProcess в отдельный тестируемый класс (сим №19, секция M).
//
// КОНТРАКТ (postfix — «честный catch-up»):
//   • За один physics-кадр моделируется ДО budget = max(8, speed×2) тиков
//     (spiral-of-death guard; при Quick бюджет 30 — типичный hitch одного
//     кадра погашается полностью).
//   • Накопленный долг НЕ сбрасывается молча: он расплачивается на
//     следующих кадрах (8+ тиков/кадр при потреблении speed/60).
//   • Если долг всё же превышает потолок MaxCatchupSeconds секунд —
//     излишек возвращается как bulkSkippedTicks: Caller (GameBoot)
//     продвигает мировые часы скачком (TimeService.BulkAdvanceTicks),
//     синхронно двигает process-tick и пишет ВИДИМОЕ предупреждение.
//     Календарь остаётся честным (реальное время == смоделированное +
//     пропущенное + остаток долга); пертиковые эффекты (реген/баффы/NPC)
//     пропущенных минут не получают — это осознанный компромисс против
//     spiral-of-death, задокументирован в TIME_SYSTEM.md.
//
// ИНВАРИАНТ УЧЁТА (проверяется симом M2):
//   TotalTicksRun + TotalBulkSkipped + PendingDebtTicks == Σ(delta × speed)
//
// Запуск под prefix-алгоритмом (дословно прежнее поведение GameBoot:
// cap 8 + молчаливый сброс backlog) — см. комментарий в Advance().
using System;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Fixed-timestep аккумулятор тиков с честным catch-up (P1-13, Фаза 11
/// внешнего аудита 09.22 12:00). Единственный владелец математика
/// «сколько тиков должно пройти» — GameBoot только вызывает Advance и
/// исполняет результат.
/// </summary>
public sealed class TickCatchUpClock
{
    /// <summary>
    /// Потолок долга в СЕКУНДАХ реального времени: долг свыше этого
    /// компенсируется bulk-скачком мировых часов (календарь честен,
    /// симуляция пертиков пропускается — видимое предупреждение).
    /// </summary>
    public const double MaxCatchupSeconds = 5.0;

    /// <summary>Дефолтный spiral-guard (тиков за один кадр) для Normal и ниже.</summary>
    public const int BaseTickBudget = 8;

    private double _accumulatorSeconds;

    /// <summary>Суммарно смоделировано тиков (для инварианта учёта/QA).</summary>
    public long TotalTicksRun { get; private set; }

    /// <summary>Суммарно пропущено bulk-скачком (для инварианта учёта/QA).</summary>
    public long TotalBulkSkipped { get; private set; }

    /// <summary>Остаток долга в секундах (меньше одного тика после Advance).</summary>
    public double PendingDebtSeconds => _accumulatorSeconds;

    /// <summary>Сброс аккумулятора (новая сессия/мир).</summary>
    public void Reset() => _accumulatorSeconds = 0;

    /// <summary>
    /// Поглотить delta реального времени и вернуть, сколько тиков нужно
    /// смоделировать СЕЙЧАС. bulkSkippedTicks &gt; 0 — излишек долга,
    /// который Caller должен компенсировать скачком мировых часов.
    /// </summary>
    /// <param name="deltaSeconds">Реальное время кадра (сек).</param>
    /// <param name="ticksPerSecond">Скорость времени (TimeSpeed: 1/5/15).</param>
    /// <param name="bulkSkippedTicks">Излишек долга для bulk-скачка (0 — долг в норме).</param>
    /// <returns>Число тиков к посимвольной симуляции в этом кадре.</returns>
    public int Advance(float deltaSeconds, int ticksPerSecond, out int bulkSkippedTicks)
    {
        bulkSkippedTicks = 0;
        if (ticksPerSecond <= 0) return 0;

        double tickInterval = 1.0 / ticksPerSecond;
        _accumulatorSeconds += deltaSeconds;

        // ── ФИКС P1-13 (Фаза 11, аудит 09.22 12:00): честный catch-up.
        // Прежде (prefix): cap 8/кадр + молчаливый сброс backlog → при
        // Quick hitch ≥ ~1.07с игровое время терялось (аудитор: «реальное
        // прошедшее время ≠ реально смоделированное игровое время»).
        //
        // Теперь:
        //   • бюджет/кадр = max(8, speed×2) — типичный hitch одного-двух
        //     кадров погашается ПОЛНОСТЬЮ в первый же кадр (Quick: 30);
        //   • долг НЕ сбрасывается молча — расплачивается на следующих
        //     кадрах (бюджет > скорости потребления → долг гарантированно тает);
        //   • чрезмерный долг (> MaxCatchupSeconds реального времени) —
        //     излишек возвращается bulkSkippedTicks: Caller продвигает
        //     мировые часы скачком (календарь честен) с видимым
        //     предупреждением (GameBoot/TimeService.HITCH).
        // ─────────────────────────────────────────────────────────────────
        int budget = Math.Max(BaseTickBudget, ticksPerSecond * 2);
        int ticksRun = 0;
        while (_accumulatorSeconds >= tickInterval && ticksRun < budget)
        {
            _accumulatorSeconds -= tickInterval;
            ticksRun++;
            TotalTicksRun++;
        }

        double ceilingSeconds = tickInterval * MaxCatchupSeconds;
        if (_accumulatorSeconds > ceilingSeconds)
        {
            double excessSeconds = _accumulatorSeconds - ceilingSeconds;
            int skipped = (int)Math.Floor(excessSeconds / tickInterval);
            if (skipped > 0)
            {
                _accumulatorSeconds -= skipped * tickInterval;
                bulkSkippedTicks = skipped;
                TotalBulkSkipped += skipped;
            }
        }

        return ticksRun;
    }
}
