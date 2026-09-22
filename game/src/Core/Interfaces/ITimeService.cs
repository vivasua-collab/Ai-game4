#nullable enable
using CultivationGame.Core;
using CultivationGame.Core.Data;
// Создано: 2026-05-08 10:07:00 UTC
// Редактировано: 2026-05-08 11:35:38 UTC — удалён избыточный SetSpeed() (дублирует Speed setter)
// Редактировано: 2026-08-15 — added CurrentTime + IsPaused for Ai-game3 compatibility.
namespace CultivationGame.Core.Interfaces
{
    public interface ITimeService
    {
        /// <summary>
        /// R36-a (аудит 09.22 12:00, Фаза 11 / P2-26): МИРОВОЙ тик — игровые
        /// минуты с начала текущего мира (0 = канонический старт 06:00 день 1).
        /// Сбрасывается ResetWorld, при Load канонизируется из даты (P2-29).
        /// Каденции мировых ритмов (автосейв «каждые 30 игровых минут»)
        /// привязываются к ЭТОМУ счётчику, а не к процессному тику движка.
        /// </summary>
        int TickCount { get; }

        /// <summary>
        /// Последний шаг времени. 1f = 1 «секунда при Normal-скорости».
        /// P2-28 (Фаза 11): IsPaused ⇒ DeltaTime == 0 — контракт «время
        /// стоит» для независимых от тик-лупа потребителей.
        /// </summary>
        float DeltaTime { get; }
        float TotalTime { get; }
        int CurrentDay { get; }
        int CurrentMonth { get; }
        int CurrentYear { get; }
        int CurrentHour { get; }
        TimeOfDay TimeOfDay { get; }
        /// <summary>Скорость времени. Использовать setter для изменения.</summary>
        TimeSpeed Speed { get; set; }
        /// <summary>True when Speed == TimeSpeed.Paused. Ai-game3 compatibility.</summary>
        bool IsPaused { get; }
        /// <summary>Current game time (1 tick = 1 minute). Ai-game3 compatibility.</summary>
        WorldTime CurrentTime { get; }
        /// <summary>
        /// Остановить время. P2-27 (Фаза 11): Resume() восстанавливает
        /// скорость ДО паузы (Fast/Quick не уничтожаются в Normal).
        /// </summary>
        void Pause();
        void Resume();

        /// <summary>
        /// R35 (аудит 09.22 12:00, Фаза 11 / P1-13): продвинуть мировые часы
        /// СКАЧКОМ на <paramref name="ticks"/> минут БЕЗ посимвольной симуляции
        /// (hitch: реальный лаг превысил потолок честного догона —
        /// TickCatchUpClock.MaxCatchupSeconds). Календарь/счётчики остаются
        /// честными; пертиковые эффекты (реген/баффы/NPC) пропущенных минут
        /// не получают. Публикует TimeHitchedEvent + видимое предупреждение.
        /// </summary>
        void BulkAdvanceTicks(int ticks);
    }
}
