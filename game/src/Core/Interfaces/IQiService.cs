#nullable enable
using CultivationGame.Core;
using CultivationGame.Core.Data;
// Создано: 2026-05-08 10:07:00 UTC
// Редактировано: 2026-05-09 05:15:31 UTC — QI-A01: убран мёртвый Tick() из интерфейса
// Редактировано: 2026-05-22 13:48:17 UTC — Этап 2.1: QiBufferResult float→int (ЗАПРЕТ 3.9); AbsorbDamage float→int
// Редактировано: 2026-05-24 05:45:00 UTC — FIX CS0117: +QiBufferMode.None (значение по умолчанию)

namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Интерфейс сервиса Ци.
    /// Управляет накоплением, расходом, регенерацией Ци, уровнями культивации и прорывами.
    /// Fix-01: Все Qi-значения используют long (>2.1B на L5+).
    /// Источник: QI_SYSTEM.md, legacy QiController.cs
    /// </summary>
    public interface IQiService
    {
        // === Идентификация ===

        /// <summary>Идентификатор сущности</summary>
        string EntityId { get; }

        // === Qi состояние ===

        /// <summary>Текущее количество Ци (Fix-01: long)</summary>
        long CurrentQi { get; }

        /// <summary>Максимальное количество Ци = coreCapacity × qiDensity (Fix-01: long)</summary>
        long MaxQi { get; }

        /// <summary>Отношение currentQi / maxQi (0.0 - 1.0)</summary>
        float QiRatio { get; }

        /// <summary>Ци исчерпан</summary>
        bool IsEmpty { get; }

        /// <summary>Ци полный</summary>
        bool IsFull { get; }

        // === Операции с Qi ===

        /// <summary>Потратить Ци. Возвращает false если недостаточно или amount ≤ 0.</summary>
        bool TryConsumeQi(long amount);

        /// <summary>Добавить Ци. Не превышает MaxQi. Игнорирует amount &lt; 0.</summary>
        void AddQi(long amount);

        /// <summary>
        /// Пассивная регенерация Ци (микроядро: 10% ёмкости/сутки).
        /// Вызывается из QiModule.ITickable.Tick() с ITimeService.DeltaTime.
        /// </summary>
        void Regenerate(float delta);

        // === Культивация ===

        /// <summary>Уровень культивации (1-10)</summary>
        CultivationLevel CultivationLevel { get; }

        /// <summary>Под-уровень культивации (0-9)</summary>
        int SubLevel { get; }

        /// <summary>Качество ядра</summary>
        CoreQuality CoreQuality { get; }

        // === Характеристики ядра ===

        /// <summary>Базовая ёмкость ядра (Fix-01: long)</summary>
        long CoreCapacity { get; }

        /// <summary>Плотность Ци = 2^(level-1)</summary>
        float QiDensity { get; }

        /// <summary>Эффективное Ци = currentQi × qiDensity (Fix-01: long)</summary>
        long EffectiveQi { get; }

        /// <summary>Итоговая проводимость меридиан (с учётом бонусов)</summary>
        float Conductivity { get; }

        /// <summary>Бонус проводимости от перков (0.0 - 2.0)</summary>
        float ConductivityBonus { get; }

        /// <summary>Установить бонус проводимости от перков</summary>
        void SetConductivityBonus(float bonus);

        // === Прорыв ===

        /// <summary>Проверить возможность прорыва</summary>
        bool CanBreakthrough(bool isMajorLevel);

        /// <summary>Рассчитать требование прорыва (Модель В)</summary>
        long CalculateBreakthroughRequirement(bool isMajorLevel);

        /// <summary>Выполнить прорыв. После прорыва Ци = 0.</summary>
        bool TryBreakthrough();

        /// <summary>Установить уровень культивации напрямую (для загрузки/тестов)</summary>
        void SetCultivationLevel(int level, int subLevel = 0);
    }

    /// <summary>
    /// Интерфейс Ци-буфера (защита от урона).
    /// Перенесён из Combat/QiBuffer.cs в модуль Qi.
    /// Активируется при наличии щитовой техники или автоматически (сырая Ци).
    /// </summary>
    public interface IQiBufferService
    {
        /// <summary>Буфер активен</summary>
        bool IsActive { get; }

        /// <summary>Текущий режим буфера</summary>
        QiBufferMode Mode { get; }

        /// <summary>Инвестированное Ци в буфер</summary>
        long QiInvested { get; }

        /// <summary>Активировать буфер (инвестирует Ци из IQiService)</summary>
        void Activate(long qiInvested, QiBufferMode mode);

        /// <summary>Деактивировать буфер (возвращает остаток Ци)</summary>
        void Deactivate();

        /// <summary>
        /// Поглотить урон через буфер Ци.
        /// Возвращает QiBufferResult с деталями поглощения.
        /// </summary>
        QiBufferResult AbsorbDamage(int incomingDamage, DamageType damageType);
    }

    /// <summary>
    /// Результат обработки урона через буфер Ци.
    /// readonly struct — нулевая GC-аллокация.
    /// Fix-01: QiConsumed/QiRemaining — long.
    /// Этап 2.1: AbsorbedDamage/PiercingDamage — int (ЗАПРЕТ 3.9).
    /// </summary>
    public readonly struct QiBufferResult
    {
        public readonly int AbsorbedDamage;
        public readonly int PiercingDamage;
        public readonly long QiConsumed;
        public readonly long QiRemaining;
        public readonly bool WasShieldActive;
        public readonly bool WasQiDepleted;

        public QiBufferResult(int absorbedDamage, int piercingDamage,
            long qiConsumed, long qiRemaining, bool wasShieldActive, bool wasQiDepleted)
        {
            AbsorbedDamage = absorbedDamage;
            PiercingDamage = piercingDamage;
            QiConsumed = qiConsumed;
            QiRemaining = qiRemaining;
            WasShieldActive = wasShieldActive;
            WasQiDepleted = wasQiDepleted;
        }
    }

    /// <summary>
    /// Режим Ци-буфера.
    /// </summary>
    public enum QiBufferMode
    {
        None,       // Буфер неактивен
        RawQi,      // Сырая Ци — поглощение 90%, пробитие 10%
        Shield      // Щит — поглощение по формулам щита
    }
}
