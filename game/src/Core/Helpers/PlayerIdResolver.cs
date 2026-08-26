#nullable enable
// Создано: 2026-08-26 — B1 (централизация P0-DUAL-PLAYER-ID).
//
// Контекст: исторически в кодовой базе ID игрока представлен двумя алиасами:
//   "player"   — публикует QiChangedEvent (QiConfig.EntityId), тело игрока (BodyConfig default)
//   "player_0" — PlayerService.Data.Id, NPCAIService.PlayerId, Combat, NPC AI
//
// Это приводило к багам, когда фильтрация событий по EntityId не срабатывала
// для одного из алиасов. Локальные нормализаторы были в трёх местах:
//   • BodyService.IsPlayerEntityId (стр ~474)
//   • PlayerService проверка e.EntityId == "player" || == "player_0" (стр ~178)
//   • TechniqueChargeService.TryGetQiCache + IsPlayerId (стр ~365-383)
//
// Решение: единый статический helper. Канонический ID — "player_0".
// Все алиасы нормализуются к каноническому через Normalize().
//
// Архитектура: Core слой (engine-agnostic, чистый C#), без зависимостей.

using System;

namespace CultivationGame.Core.Helpers
{
    /// <summary>
    /// Централизованный определитель ID игрока (B1, 2026-08-26).
    /// Заменяет три локальные копии нормализации "player"/"player_0".
    ///
    /// Канонический ID игрока — <see cref="PlayerCanonical"/> ("player_0").
    /// Все события/кэши, сравнивающие сущность с игроком, должны использовать
    /// <see cref="Normalize"/> для ключа/lookup и <see cref="IsPlayer"/> /
    /// <see cref="AreSameEntity"/> для сравнений.
    /// </summary>
    public static class PlayerIdResolver
    {
        /// <summary>Алиас "player" — публикует QiChangedEvent, тело игрока (BodyConfig).</summary>
        public const string PlayerQiAlias = "player";

        /// <summary>Канонический ID игрока — PlayerService.Data.Id, NPC AI, Combat.</summary>
        public const string PlayerCanonical = "player_0";

        /// <summary>
        /// Нормализовать ID сущности к каноническому виду.
        /// "player" → "player_0"; все остальные возвращаются как есть.
        /// null/empty → возвращается как есть (caller ответственен за валидацию).
        /// </summary>
        public static string Normalize(string? raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw ?? string.Empty;
            return raw == PlayerQiAlias ? PlayerCanonical : raw;
        }

        /// <summary>
        /// Является ли ID одним из алиасов игрока ("player" или "player_0").
        /// null/empty → false.
        /// </summary>
        public static bool IsPlayer(string? id)
        {
            return id == PlayerQiAlias || id == PlayerCanonical;
        }

        /// <summary>
        /// Указывают ли два ID на одну и ту же сущность.
        /// Учитывает алиасы игрока: "player" и "player_0" считаются одинаковыми.
        /// null/empty → false (нельзя сравнивать пустые).
        /// </summary>
        public static bool AreSameEntity(string? a, string? b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            return Normalize(a) == Normalize(b);
        }

        /// <summary>
        /// Является ли сущность с указанным ID игроком.
        /// Эквивалент <see cref="IsPlayer"/>, но с более читаемым именем для caller'ов,
        /// проверяющих "это игрок?".
        /// </summary>
        public static bool IsPlayerEntity(string? id) => IsPlayer(id);
    }
}
