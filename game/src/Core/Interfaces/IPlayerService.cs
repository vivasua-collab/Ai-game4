#nullable enable
// Создано: 2026-05-09 16:16:00 UTC
// Редактировано: 2026-05-09 16:26:00 UTC — PlayerSleepState, PlayerStance перенесены в Enums.cs
// Редактировано: 2026-05-10 — Phase 17C: Vector2 → Position2D в сигнатурах методов
// Редактировано: 2026-05-20 18:43:21 UTC — Фаза 3: CultivationLevel + GetCurrentQi (задача 3.E)
// Интерфейс сервиса игрока — тонкий фасад.
// АРХИТЕКТУРА: Hub-and-Spoke. PlayerService НЕ инжектит интерфейсы других модулей.
// Все кросс-модульные взаимодействия — ТОЛЬКО через MessagePipe (EVT-01).
using System.Collections.Generic;
using CultivationGame.Core.Data;

using CultivationGame.Core;
namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Тонкий фасад для управления состоянием игрока.
    /// НЕ God Object — делегирует логику внутримодульным сервисам.
    ///
    /// Кросс-модульное взаимодействие — через MessagePipe:
    /// - PlayerDeathEvent / PlayerReviveEvent — публикация
    /// - PlayerSleepEvent — публикация
    /// - PlayerPositionChangedEvent — публикация
    /// - DamageAppliedEvent — подписка (реакция на урон)
    /// - QiDepletedEvent — подписка (прерывание действий)
    /// - BodyPartSeveredEvent — подписка (оглушение)
    /// - CombatStartedEvent / CombatEndedEvent — подписка (боевая стойка)
    /// </summary>
    public interface IPlayerService
    {
        /// <summary>Идентификатор игрока</summary>
        string PlayerId { get; }

        /// <summary>Текущая позиция игрока</summary>
        Position2D Position { get; }

        /// <summary>Жив ли игрок</summary>
        bool IsAlive { get; }

        /// <summary>Спит ли игрок</summary>
        bool IsSleeping { get; }

        /// <summary>Состояние сна</summary>
        PlayerSleepState SleepState { get; }

        /// <summary>Боевая стойка</summary>
        PlayerStance Stance { get; }

        /// <summary>Начать сон на указанное количество часов</summary>
        void StartSleep(float hours);

        /// <summary>Разбудить игрока</summary>
        void WakeUp();

        /// <summary>Установить позицию игрока</summary>
        void SetPosition(Position2D position);

        /// <summary>Установить позицию по координатам. Ai-game3 compatibility.</summary>
        void MoveTo(int x, int y);

        /// <summary>Спавн игрока. Ai-game3 compatibility — вызывается из PlayerSpawnPhase.</summary>
        void Spawn(Position2D position);

        /// <summary>Получить назначенные техники</summary>
        IReadOnlyList<string> GetAssignedTechniques();

        /// <summary>Обновление кадра (делегирует внутримодульным сервисам)</summary>
        void Tick(float deltaTime);

        /// <summary>Уровень культивации игрока (Фаза 3, задача 3.E)</summary>
        CultivationLevel CultivationLevel { get; }

        /// <summary>Текущее количество Ци игрока (Фаза 3, задача 3.E)</summary>
        long GetCurrentQi();
    }
}
