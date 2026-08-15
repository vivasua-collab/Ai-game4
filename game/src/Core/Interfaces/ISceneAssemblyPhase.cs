#nullable enable
using CultivationGame.Core;
using CultivationGame.Core.Data;
using System.Threading.Tasks;
// Создано: 2026-05-09 20:27:00 UTC
// Редактировано: 2026-05-10 11:40:00 UTC — Phase 18C: добавлен SkipOnLoad в ISceneAssemblyPhase

namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Состояние фазы сборки сцены.
    /// Аналог IsNeeded() из легаси IScenePhase, но с поддержкой асинхронности.
    /// </summary>
    public enum SceneAssemblyPhaseState
    {
        /// <summary>Не запускалась</summary>
        Pending,
        /// <summary>Выполняется</summary>
        Running,
        /// <summary>Успешно завершена</summary>
        Completed,
        /// <summary>Ошибка при выполнении</summary>
        Failed,
        /// <summary>Пропущена (не нужна или зависимости не удовлетворены)</summary>
        Skipped
    }

    /// <summary>
    /// Интерфейс фазы сборки сцены (runtime-аналог легаси IScenePhase).
    /// Каждая фаза:
    ///   1. Проверяет готовность (CanExecute)
    ///   2. Выполняется асинхронно (ExecuteAsync)
    ///   3. Отслеживает состояние (State)
    ///
    /// Фаза идемпотентна — повторный вызов ExecuteAsync при State=Completed безопасен.
    /// Оркестратор вызывает фазы в порядке Order, проверяя CanExecute перед запуском.
    /// </summary>
    public interface ISceneAssemblyPhase
    {
        /// <summary>Имя фазы для логирования</summary>
        string PhaseName { get; }

        /// <summary>Порядок выполнения (0 = первый)</summary>
        int Order { get; }

        /// <summary>Текущее состояние фазы</summary>
        SceneAssemblyPhaseState State { get; }

        /// <summary>
        /// Проверяет, можно ли выполнить фазу.
        /// Возвращает false, если зависимости не удовлетворены.
        /// </summary>
        bool CanExecute();

        /// <summary>
        /// Человекочитаемая причина, почему CanExecute() вернул false.
        /// Пустая строка если CanExecute() вернул true.
        /// </summary>
        string BlockReason { get; }

        /// <summary>
        /// Выполняет фазу асинхронно.
        /// При успешном завершении State = Completed.
        /// При ошибке State = Failed.
        /// </summary>
        Task ExecuteAsync();

        /// <summary>
        /// Пометить фазу как пропущенную.
        /// P16-A02 FIX: обновляет State = Skipped и устанавливает BlockReason.
        /// Вызывается оркестратором при CanExecute() == false.
        /// </summary>
        void MarkAsSkipped(string reason);

        /// <summary>
        /// Сброс состояния (для перезапуска сборки сцены).
        /// Переводит State в Pending.
        /// </summary>
        void Reset();

        /// <summary>
        /// Должна ли фаза пропускаться при загрузке сохранения.
        /// true = фаза генерации (тайлы, NPC, игрок), пропускается при Load
        /// false = фаза подключения (UI, подписки), выполняется всегда
        /// Phase 18C: Load-сценарий требует пропуска генерационных фаз.
        /// </summary>
        bool SkipOnLoad { get; }
    }

    /// <summary>
    /// Логгер сборки сцены.
    /// Реализация по умолчанию: UnityEngine.Debug.Log.
    /// </summary>
    public interface ISceneAssemblyLogger
    {
        /// <summary>Логирует начало выполнения фазы</summary>
        void LogPhaseStart(string phaseName, int order);

        /// <summary>Логирует успешное завершение фазы</summary>
        void LogPhaseComplete(string phaseName, int order, float elapsedMs);

        /// <summary>Логирует ошибку при выполнении фазы</summary>
        void LogPhaseFailed(string phaseName, int order, string error);

        /// <summary>Логирует пропуск фазы</summary>
        void LogPhaseSkipped(string phaseName, int order, string reason);

        /// <summary>Логирует сводный отчёт по сборке</summary>
        void LogSummary(int total, int completed, int skipped, int failed, float totalTimeMs);
    }
}
