#nullable enable
// Создано: 2026-05-20 18:43:21 UTC
// Провайдер данных тела per-entity.
// Позволяет получать/устанавливать BodyParts по entityId.
// Единая система через BodyParts для всех сущностей (решение ПРОТИВОРЕЧИЯ #3/#6).
// BodyDataProvider использует List<BodyPart> — не Dictionary, не BodyPartData.
// Редактировано: 2026-05-23 — IMPL-1: BodyPart moved from Modules.Body to Core.Data
using System.Collections.Generic;

using CultivationGame.Core;
using CultivationGame.Core.Data;
namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Провайдер данных тела per-entity.
    /// Позволяет получать/устанавливать BodyParts по entityId.
    /// Единая система через BodyParts для всех сущностей (решение ПРОТИВОРЕЧИЯ #3/#6).
    /// BodyDataProvider использует List{BodyPart} — не Dictionary, не BodyPartData.
    /// </summary>
    public interface IBodyDataProvider
    {
        /// <summary>Получить BodyParts сущности по entityId</summary>
        List<BodyPart> GetBodyParts(string entityId);

        /// <summary>Установить BodyParts для сущности (при создании NPC)</summary>
        void SetBodyParts(string entityId, List<BodyPart> parts);

        /// <summary>Проверить существование сущности в провайдере</summary>
        bool HasEntity(string entityId);

        /// <summary>Удалить сущность из провайдера (при деспавне NPC)</summary>
        void RemoveEntity(string entityId);

        /// <summary>Получить сумму CurrentRedHP всех частей (текущее здоровье)</summary>
        int GetCurrentHealth(string entityId);

        /// <summary>Получить сумму MaxRedHP всех частей (максимальное здоровье)</summary>
        int GetMaxHealth(string entityId);

        /// <summary>
        /// Проверить, жива ли сущность (Спринт 1 A2).
        /// Возвращает false, если любая жизненно важная часть (Head, Heart)
        /// имеет CurrentRedHP <= 0. Это корректная проверка смерти —
        /// не «суммарный HP <= 0», а именно «жизненно важная часть уничтожена».
        /// </summary>
        bool IsEntityAlive(string entityId);
    }
}
