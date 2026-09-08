#nullable enable
using CultivationGame.Core;
using CultivationGame.Core.Data;
using System.Threading.Tasks;
// Создано: 2026-05-09 20:27:00 UTC
// Редактировано: 2026-05-10 11:40:00 UTC — Phase 18C: добавлен SkipOnLoad в ISceneAssemblyPhase
// Редактировано: 2026-09-08 (ревью-1): lifecycle-контракт фазы ДОВЕДЁН ДО РАБОЧЕГО:
//   + MarkAsRunning/MarkAsCompleted/MarkAsFailed — оркестратор владеет переходами
//     Pending → Running → Completed/Failed (раньше State менялся только MarkAs'ом
//     Skipped, контракт «фазы сами выставляют Completed» не выполнялся никем);
//   + enum SceneAssemblyMode (NewGame/LoadGame) — режим прогона для SkipOnLoad;
//   − удалён ISceneAssemblyLogger — легаси Unity-итерации (UnityEngine.Debug.Log,
//     0 реализаций, 0 потребителей; оркестратор логирует в Console/GD.Print).

namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Режим прогона сборки сцены. Задаёт, как оркестратор применяет
    /// <see cref="ISceneAssemblyPhase.SkipOnLoad"/>.
    /// </summary>
    public enum SceneAssemblyMode
    {
        /// <summary>Новая игра: все фазы выполняются (проверка CanExecute).</summary>
        NewGame,
        /// <summary>
        /// Загрузка сейва: генеративные фазы (SkipOnLoad=true) пропускаются —
        /// их состояние восстанавливается из сейва; wiring-фазы выполняются.
        /// </summary>
        LoadGame,
    }

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
    /// 2026-09-08 (ревью-1): LIFECYCLE — ответственность ОРКЕСТРАТОРА.
    /// Оркестратор перед прогоном вызывает Reset() на всех фазах, затем
    /// ведёт переходы: MarkAsRunning → ExecuteAsync → MarkAsCompleted
    /// (или MarkAsFailed при исключении), MarkAsSkipped — для CanExecute()==false
    /// и SkipOnLoad-фаз в режиме LoadGame. Реализации ExecuteAsync НЕ меняют
    /// State самостоятельно (за исключением особых случаев).
    /// Фаза идемпотентна — повторный вызов ExecuteAsync при State=Completed
    /// безопасен (оркестратор Reset-ит фазы перед новой сборкой).
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
        /// Выполняет фазу асинхронно. Оркестратор переводит State в Running
        /// до вызова и в Completed/Failed после — реализации НЕ меняют State.
        /// </summary>
        Task ExecuteAsync();

        /// <summary>
        /// Пометить фазу как пропущенную.
        /// P16-A02 FIX: обновляет State = Skipped и устанавливает BlockReason.
        /// Вызывается оркестратором при CanExecute() == false или SkipOnLoad
        /// в режиме LoadGame.
        /// </summary>
        void MarkAsSkipped(string reason);

        /// <summary>
        /// 2026-09-08 (ревью-1): переводит State = Running. Вызывает оркестратор
        /// непосредственно перед ExecuteAsync.
        /// </summary>
        void MarkAsRunning();

        /// <summary>
        /// 2026-09-08 (ревью-1): переводит State = Completed. Вызывает оркестратор
        /// после успешного ExecuteAsync.
        /// </summary>
        void MarkAsCompleted();

        /// <summary>
        /// 2026-09-08 (ревью-1): переводит State = Failed и заполняет BlockReason
        /// текстом ошибки. Вызывает оркестратор при исключении в ExecuteAsync.
        /// </summary>
        void MarkAsFailed(string error);

        /// <summary>
        /// Сброс состояния (для перезапуска сборки сцены).
        /// Переводит State в Pending и очищает BlockReason. Вызывается
        /// оркестратором в начале каждого прогона (фазы — синглтоны DI).
        /// </summary>
        void Reset();

        /// <summary>
        /// Должна ли фаза пропускаться при загрузке сохранения.
        /// true = фаза генерации (тайлы, NPC, игрок), пропускается при Load
        /// false = фаза подключения (UI, подписки, валидация), выполняется всегда
        /// Phase 18C: Load-сценарий требует пропуска генерационных фаз.
        /// </summary>
        bool SkipOnLoad { get; }
    }
}
