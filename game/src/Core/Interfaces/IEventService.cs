#nullable enable
// Создано: 2026-05-09 17:30:00 UTC
// Интерфейс службы мировых событий.
// Управление глобальными событиями в мире (катастрофы, нашествия, праздники).
using System.Collections.Generic;

using CultivationGame.Core;
using CultivationGame.Core.Data;
namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Служба мировых событий.
    /// Управляет глобальными событиями, влияющими на весь мир:
    /// катастрофы, нашествия демонов, праздники, стихийные бедствия.
    ///
    /// АРХИТЕКТУРА (EVT-01): Модули НЕ инжектят IEventService напрямую.
    /// Кросс-модульные взаимодействия — через MessagePipe (WorldEventTriggeredEvent, WorldEventEndedEvent).
    /// </summary>
    public interface IEventService
    {
        /// <summary>Активировать мировое событие</summary>
        void TriggerWorldEvent(string eventId);

        /// <summary>Проверить, активно ли мировое событие</summary>
        bool IsEventActive(string eventId);

        /// <summary>Список идентификаторов активных мировых событий</summary>
        IReadOnlyList<string> GetActiveEvents();

        /// <summary>Завершить мировое событие</summary>
        void EndWorldEvent(string eventId);
    }
}
