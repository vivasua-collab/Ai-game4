#nullable enable
using CultivationGame.Core;
using CultivationGame.Core.Data;
// Создано: 2026-05-20 18:43:21 UTC
// Редактировано: 2026-05-22 07:55:00 UTC — Спринт 4 B8: +QiBuffer методы для per-entity QiBuffer в DamageService
// Редактировано: 2026-05-22 13:08:27 UTC — P0-X1 FIX: +ConsumeQi для NPC Qi-расхода через буфер
// Провайдер данных Ци per-entity.
// Позволяет получать/устанавливать состояние Ци по entityId.
// QiDataProvider хранит проводимость, рассчитанную по расширенной формуле (решение ПРОТИВОРЕЧИЯ #4).
namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Провайдер данных Ци per-entity.
    /// Позволяет получать/устанавливать состояние Ци по entityId.
    /// QiDataProvider хранит проводимость, рассчитанную по расширенной формуле (решение ПРОТИВОРЕЧИЯ #4).
    /// </summary>
    public interface IQiDataProvider
    {
        /// <summary>Текущее Ци сущности</summary>
        long GetCurrentQi(string entityId);

        /// <summary>Максимальное Ци сущности</summary>
        long GetMaxQi(string entityId);

        /// <summary>Проводимость сущности</summary>
        float GetConductivity(string entityId);

        /// <summary>Установить состояние Ци для сущности (при создании NPC)</summary>
        void SetQiState(string entityId, long currentQi, long maxQi, float conductivity);

        /// <summary>Проверить существование сущности</summary>
        bool HasEntity(string entityId);

        /// <summary>Удалить сущность (при деспавне)</summary>
        void RemoveEntity(string entityId);

        /// <summary>Уровень культивации сущности</summary>
        int GetCultivationLevel(string entityId);

        /// <summary>Установить уровень культивации</summary>
        void SetCultivationLevel(string entityId, int level);

        // === Спринт 4 B8: Per-entity QiBuffer ===

        /// <summary>Активен ли QiBuffer для сущности</summary>
        bool IsQiBufferActive(string entityId);

        /// <summary>Режим QiBuffer для сущности</summary>
        QiBufferMode GetQiBufferMode(string entityId);

        /// <summary>Количество Ци, вложенной в QiBuffer сущности</summary>
        long GetQiBufferInvested(string entityId);

        /// <summary>Установить состояние QiBuffer для сущности</summary>
        void SetQiBufferState(string entityId, bool isActive, QiBufferMode mode, long qiInvested);

        // === P0-X1 FIX: NPC Qi расход ===

        /// <summary>
        /// Попытаться списать Ци с NPC-сущности.
        /// P0-X1 FIX: для NPC Qi расход через QiBuffer — прямое списание из IQiDataProvider.
        /// QiService обрабатывает только свои (игрок) QiConsumeRequestEvent.
        /// Возвращает true, если Ци успешно списано.
        /// </summary>
        bool TryConsumeQi(string entityId, long amount);
    }
}
