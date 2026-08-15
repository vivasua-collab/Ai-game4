#nullable enable
// Создано: 2026-05-21 19:25:59 UTC
// Редактировано: 2026-05-22 04:14:49 UTC — Спринт 3 B6: GetElement/GetMaterial реализация
// Редактировано: 2026-05-22 07:55:00 UTC — Аудит MED-1: INPCService вместо NPCService (DI compliance)
// Редактировано: 2026-05-22 11:30:00 UTC — Спринт 8 C10: GetMorphology реализация
// Спринт 2 B3: Адаптер единого доступа к статам для CombatService.
// Скрывает источник данных (IStatService для игрока, INPCService для NPC)
// за единым интерфейсом IStatProvider.
//
// ЗАПРЕТ 3.9: Все статы — int. IStatService.GetStat() возвращает float,
// кастуем к int. NPCState хранит int напрямую.
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Combat
{
    /// <summary>
    /// Адаптер единого доступа к статам для боевой системы.
    /// Спринт 2 B3: CombatService инжектирует IStatProvider
    /// вместо IStatService + NPCService по отдельности.
    ///
    /// Логика:
    /// - Если entityId = "player" или зарегистрирован в IStatService → IStatService
    /// - Иначе если entityId найден в INPCService → NPCState.Strength/Agility/...
    /// - Иначе → 0 (сущность не найдена)
    ///
    /// MED-1 FIX: Теперь инжектит INPCService вместо конкретного NPCService.
    /// ЗАПРЕТ 3.9: Все статы — int.
    /// </summary>
    public class StatProviderAdapter : IStatProvider
    {
        private readonly IStatService _playerStatService;
        private readonly INPCService _npcService; // MED-1: интерфейс вместо конкретного класса

        public StatProviderAdapter(IStatService playerStatService, INPCService npcService)
        {
            _playerStatService = playerStatService;
            _npcService = npcService;
        }

        /// <summary>
        /// Получить значение стата сущности.
        /// Для игрока — IStatService.GetStat() → (int).
        /// Для NPC — NPCState.Strength/Agility/Vitality/Intelligence.
        /// MED-1: доступ через INPCService.GetNPCState() вместо конкретного NPCService.
        /// </summary>
        public int GetStat(string entityId, StatType type)
        {
            // Сначала проверяем NPC (по entityId в INPCService)
            var npcState = _npcService.GetNPCState(entityId);
            if (npcState != null)
            {
                return type switch
                {
                    StatType.Strength => npcState.Strength,
                    StatType.Agility => npcState.Agility,
                    StatType.Vitality => npcState.Vitality,
                    StatType.Intelligence => npcState.Intelligence,
                    _ => 0
                };
            }

            // Игрок — IStatService.GetStat() возвращает float, кастуем к int (ЗАПРЕТ 3.9)
            float rawValue = _playerStatService.GetStat(type);
            return (int)rawValue;
        }

        /// <summary>
        /// Получить врождённую стихию сущности.
        /// Для игрока — Element.Neutral.
        /// Для NPC — NPCState.InnateElement (из SoulData).
        /// Спринт 3 B6: для стихийных множителей в DamageService.
        /// </summary>
        public Element GetElement(string entityId)
        {
            var npcState = _npcService.GetNPCState(entityId);
            if (npcState != null)
                return npcState.InnateElement;
            return Element.Neutral; // Игрок — Neutral
        }

        /// <summary>
        /// Получить материал тела сущности.
        /// Для игрока — BodyMaterial.Organic.
        /// Для NPC — NPCState.BodyMaterial (из SpeciesData.Material).
        /// Спринт 3 B6: для материального снижения в DefenseProcessor.
        /// </summary>
        public BodyMaterial GetMaterial(string entityId)
        {
            var npcState = _npcService.GetNPCState(entityId);
            if (npcState != null)
                return npcState.BodyMaterial;
            return BodyMaterial.Organic; // Игрок — Organic
        }

        /// <summary>
        /// Получить морфологию тела сущности.
        /// Для игрока — Morphology.Humanoid.
        /// Для NPC — NPCState.Morphology (из SpeciesData.Morphology).
        /// Спринт 8 C10: для выбора таблицы попадания по частям тела.
        /// </summary>
        public Morphology GetMorphology(string entityId)
        {
            var npcState = _npcService.GetNPCState(entityId);
            if (npcState != null)
                return npcState.Morphology;
            return Morphology.Humanoid; // Игрок — Humanoid
        }
    }
}
