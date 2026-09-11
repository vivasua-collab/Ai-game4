#nullable enable
// Создано: 2026-09-11 — аудит боя с животными (D1–D7): Core-интерфейс
// доступа к животным для чужих модулей. До этого AnimalService жил как
// конкретный класс Modules/NPC — Adapter/Scene (AnimalSpriteRenderer)
// тянул его напрямую, но боевые потребители (PlayerCombatAdapter в
// Modules/Player, StatProviderAdapter/CorpseService в Combat/NPC)
// должны работать через Core-интерфейс (паттерн INPCService).
//
// Проблема, которую закрывает интерфейс: PlayerCombatAdapter.FindNearest
// Target видел только NPCService.GetNearbyNPCIds — волки НЕ регистрируются
// в NPC-реестре (живут в AnimalService._animals) → Space-атака по животному
// молча не находила цель («результата 0»).
using System.Collections.Generic;
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Interfaces;

/// <summary>
/// Доступ к животным мира (волк/олень/кролик) для боевых потребителей:
/// таргетинг атак игрока, трупы (DEATH_AND_LOOT §5), боевые статы вида,
/// отображаемые имена (killfeed/цифры урона). Реализация — AnimalService.
/// </summary>
public interface IAnimalService
{
    /// <summary>
    /// Живые животные в радиусе от точки (Чебышёв, тайлы) — кандидаты
    /// таргетинга атак игрока (Space/ranged). Мёртвые не включаются.
    /// </summary>
    /// <param name="center">Центр поиска (позиция игрока).</param>
    /// <param name="rangeTiles">Радиус в тайлах (Чебышёв).</param>
    IReadOnlyList<AnimalInfo> GetAliveAnimalsInRange(Position2D center, float rangeTiles);

    /// <summary>
    /// Профиль животного по ID сущности (null — не животное или не найдено).
    /// Статы снимаются из SpeciesRegistry вида.
    /// </summary>
    AnimalInfo? TryGetAnimal(string entityId);

    /// <summary>
    /// Отображаемое имя животного для UI: killfeed («☠ Волк повержен»),
    /// труп-контейнер, цифры урона. Не-животное → null.
    /// </summary>
    string? GetDisplayName(string entityId);
}
