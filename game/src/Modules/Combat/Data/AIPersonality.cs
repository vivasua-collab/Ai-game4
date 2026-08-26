#nullable enable
// Создано: 2026-05-09
// Данные AI противника.
// Перенесено из legacy Combat/AIPersonality.cs с адаптацией под модульную архитектуру.
using CultivationGame.Core;
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.Combat.Data
{
    /// <summary>
    /// Данные AI противника.
    /// Определяют поведение NPC в бою: агрессию, тактику, предпочтения.
    /// </summary>
    public class AIPersonality
    {
        /// <summary>Идентификатор личности AI</summary>
        public string PersonalityId;

        /// <summary>Агрессивность (0-1): 0 = полностью оборонительный, 1 = полностью агрессивный</summary>
        public float Aggressiveness = 0.5f;

        /// <summary>Склонность к техникам (0-1): 0 = только базовые атаки, 1 = максимум техник</summary>
        public float TechniquePreference = 0.3f;

        /// <summary>Склонность к защите (0-1)</summary>
        public float DefenseTendency = 0.3f;

        /// <summary>Порог HP для бегства (0-1): доля HP, при которой NPC пытается сбежать</summary>
        public float FleeThreshold = 0.1f;

        /// <summary>Задержка между действиями (секунды)</summary>
        public float ActionDelay = 1.0f;

        /// <summary>Черты характера (комбинируемые флаги)</summary>
        public PersonalityTrait Traits = PersonalityTrait.None;

        /// <summary>Предпочитаемый элемент (Neutral = любой)</summary>
        public Element PreferredElement = Element.Neutral;

        /// <summary>
        /// Создать стандартную AI-личность по типу.
        /// </summary>
        public static AIPersonality CreateAggressive()
        {
            return new AIPersonality
            {
                PersonalityId = "aggressive",
                Aggressiveness = 0.9f,
                TechniquePreference = 0.5f,
                DefenseTendency = 0.1f,
                FleeThreshold = 0.05f,
                ActionDelay = 0.5f,
                Traits = PersonalityTrait.Aggressive
            };
        }

        public static AIPersonality CreateCautious()
        {
            return new AIPersonality
            {
                PersonalityId = "cautious",
                Aggressiveness = 0.3f,
                TechniquePreference = 0.4f,
                DefenseTendency = 0.7f,
                FleeThreshold = 0.3f,
                ActionDelay = 1.5f,
                Traits = PersonalityTrait.Cautious
            };
        }

        public static AIPersonality CreateBalanced()
        {
            return new AIPersonality
            {
                PersonalityId = "balanced",
                Aggressiveness = 0.5f,
                TechniquePreference = 0.3f,
                DefenseTendency = 0.3f,
                FleeThreshold = 0.15f,
                ActionDelay = 1.0f,
                Traits = PersonalityTrait.None
            };
        }
    }
}
