#nullable enable
// Создано: 2026-08-22 — Phase C (BODY-IMPL-PLAN): простые животные на тестовом полигоне.
// AnimalEntity — простой POCO для животного (волк / олень / кролик).
// Без AI-комбата, без анимаций — только случайное блуждание + собранное тело.
// Источник: checkpoints/08_22_body_impl_plan.md Phase C
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.NPC
{
    /// <summary>
    /// Простая сущность животного на карте.
    /// Не является полноценным NPC (нет Soul, нет AI) — это «фоновый» объект,
    /// который блуждает по карте и имеет собранное тело (Quadruped).
    /// Body parts хранятся в IBodyDataProvider per-entity (как у NPC).
    /// </summary>
    public sealed class AnimalEntity
    {
        /// <summary>Уникальный идентификатор сущности (для IBodyDataProvider).</summary>
        public string EntityId { get; }

        /// <summary>Вид ("wolf", "deer", "rabbit").</summary>
        public string Species { get; }

        /// <summary>Текущая позиция в тайлах.</summary>
        public Position2D Position { get; set; }

        /// <summary>Цель блуждания (null = стоять, ждать нового выбора цели).</summary>
        public Position2D? Target { get; set; }

        /// <summary>Морфология тела (Quadruped для всех животных).</summary>
        public Morphology Morphology { get; }

        /// <summary>Материал тела (Organic).</summary>
        public BodyMaterial Material { get; }

        /// <summary>Класс размера (Medium / Small).</summary>
        public SizeClass Size { get; }

        /// <summary>Живо ли животное.</summary>
        public bool IsAlive { get; set; } = true;

        /// <summary>
        /// Сколько тиков до следующего выбора цели/движения.
        /// 1 тик ≈ 1 секунда при Normal скорости (1 Hz).
        /// </summary>
        public int MoveCooldownTicks { get; set; }

        /// <summary>
        /// Скорость перемещения в тайлах за тик (1 = обычная, 2 = быстрая).
        /// Кролик = 2 (быстрый), волк/олень = 1.
        /// </summary>
        public int MoveSpeedTilesPerTick { get; set; } = 1;

        /// <summary>Cooldown for combat attacks (ticks until next attack).</summary>
        public int CombatCooldownTicks { get; set; }

        /// <summary>ID of last attacker (for retaliation targeting).</summary>
        public string LastAttackerId { get; set; } = string.Empty;

        public AnimalEntity(
            string entityId,
            string species,
            Position2D position,
            Morphology morphology,
            BodyMaterial material,
            SizeClass size)
        {
            EntityId = entityId ?? string.Empty;
            Species = species ?? string.Empty;
            Position = position;
            Morphology = morphology;
            Material = material;
            Size = size;
        }

        public override string ToString()
            => $"Animal[{Species}#{EntityId} @ {Position} ({(IsAlive ? "alive" : "dead")})]";
    }
}
