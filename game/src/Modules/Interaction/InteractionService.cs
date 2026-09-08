#nullable enable
// Создано: 2026-05-09 — Phase 13: реализация IInteractionService
// Поиск ближайшего интерактивного объекта, выполнение взаимодействия.
// EVT-01: НЕ инжектит INPCService, IPlayerService — только подписки MessagePipe.
// Редактировано: 2026-05-10 — Phase 17A: Q13-E01, Q13-E02 fixes
// Редактировано: 2026-05-10 — Phase 17C: Vector2 → Position2D, Q14-E01 FIX: подписка на UIInteractRequestEvent
using System;
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Interaction
{
    /// <summary>
    /// Реализация IInteractionService.
    /// Управляет поиском интерактивных объектов и выполнением взаимодействий.
    ///
    /// АРХИТЕКТУРА (EVT-01): Модуль Interaction НЕ инжектит INPCService, IPlayerService.
    /// Все кросс-модульные данные поступают через подписки MessagePipe:
    /// - PlayerPositionChangedEvent → обновление позиции для поиска ближайшего
    /// - NPCInteractedEvent → триггер диалога
    /// - InteractionCompletedEvent → публикация при успешном взаимодействии
    /// </summary>
    public class InteractionService : IInteractionService, IDisposable
    {
        // === MessagePipe: паблишеры ===
        private readonly IPublisher<InteractionCompletedEvent> _interactionCompletedPub;

        // === MessagePipe: подписки ===
        private readonly ISubscriber<PlayerPositionChangedEvent> _positionChangedSub;
        // Q14-E01 FIX: подписка на UIInteractRequestEvent
        private readonly ISubscriber<UIInteractRequestEvent> _interactRequestSub;
        private IDisposable _positionChangedSubscription;
        private IDisposable _interactRequestSubscription;

        // === Конфигурация ===
        private InteractionConfig _config;

        // === Состояние ===
        private Position2D _playerPosition;
        private string _nearestInteractableId;

        /// <summary>
        /// Реестр ДИНАМИЧЕСКИ зарегистрированных интерактивных объектов.
        /// Review этап 6 (P1-3): фиктивный статический registry (elder_01/
        /// merchant_01/chest_01 на выдуманных координатах) УДАЛЁН — реальные
        /// NPC спавнятся с динамическими ID, а рабочий E-путь взаимодействия
        /// живёт в GameWorldController (INPCService.GetNearbyNPCIds →
        /// DialogueService.TryStartNpcDialogue). Реестр пополняют системы-
        /// владельцы через RegisterInteractable (будущие сундуки/объекты);
        /// prefix-эвристика «NPC по строке ID» удалена (не соответствует
        /// процедурным сущностям).
        /// </summary>
        private readonly Dictionary<string, Position2D> _interactablePositions = new Dictionary<string, Position2D>();

        public InteractionService(
            IPublisher<InteractionCompletedEvent> interactionCompletedPub,
            ISubscriber<PlayerPositionChangedEvent> positionChangedSub,
            ISubscriber<UIInteractRequestEvent> interactRequestSub) // Q14-E01 FIX
        {
            _interactionCompletedPub = interactionCompletedPub;
            _positionChangedSub = positionChangedSub;
            _interactRequestSub = interactRequestSub; // Q14-E01 FIX
        }

        /// <summary>
        /// Инициализация с конфигурацией.
        /// Вызывается из InteractionModule.Start().
        /// B01-fix: Dispose предыдущей подписки при повторном Initialize.
        /// </summary>
        public void Initialize(InteractionConfig config)
        {
            _config = config;

            // B01-fix: Dispose предыдущей подписки
            _positionChangedSubscription?.Dispose();

            // Подписка на позицию игрока для обновления ближайшего объекта
            _positionChangedSubscription = _positionChangedSub.Subscribe(OnPlayerPositionChanged);

            // Q14-E01 FIX: подписка на запрос взаимодействия от UI
            _interactRequestSubscription = _interactRequestSub.Subscribe(OnUIInteractRequest);

            // Review этап 6 (P1-3): регистрация фиктивных интерактивных объектов
            // (elder_01/merchant_01/chest_01) УДАЛЕНА — реестр динамический.
        }

        // === IInteractionService ===

        public string GetNearestInteractableId(Position2D position, float range)
        {
            string nearest = null;
            float nearestDistSq = range * range;

            foreach (var kvp in _interactablePositions)
            {
                float distSq = (kvp.Value - position).SqrMagnitude;
                if (distSq <= nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearest = kvp.Key;
                }
            }

            return nearest;
        }

        public bool TryInteract(string targetId)
        {
            if (string.IsNullOrEmpty(targetId)) return false;
            if (!_interactablePositions.TryGetValue(targetId, out var targetPos)) return false;

            // Проверка дальности
            float distSq = (targetPos - _playerPosition).SqrMagnitude;
            float range = _config != null ? _config.DefaultInteractionRange : 2f;
            if (distSq > range * range) return false;

            // A03-fix: Используем константу вместо магической строки
            _interactionCompletedPub.Publish(new InteractionCompletedEvent(
                targetId, GameConstants.InteractionType.Interact));

            // Review этап 6 (P1-3): NPCInteractedEvent больше НЕ публикуется по
            // prefix-эвристике — NPC-взаимодействия идут реальным E-путём
            // (GameWorldController → DialogueService); квест-трекер подписан на
            // NPCInteractedEvent от реальных событий (см. этап 7).

            return true;
        }

        // === Дополнительные методы ===

        /// <summary>
        /// Получить идентификатор ближайшего интерактивного объекта (кешированный).
        /// </summary>
        internal string GetCachedNearestId()
        {
            return _nearestInteractableId;
        }

        /// <summary>
        /// Зарегистрировать интерактивный объект.
        /// </summary>
        internal void RegisterInteractable(string id, Position2D position)
        {
            if (string.IsNullOrEmpty(id)) return;
            _interactablePositions[id] = position;
        }

        /// <summary>
        /// Удалить интерактивный объект.
        /// </summary>
        internal void UnregisterInteractable(string id)
        {
            _interactablePositions.Remove(id);
        }

        // === Обработчики событий ===

        private void OnPlayerPositionChanged(in PlayerPositionChangedEvent e)
        {
            _playerPosition = new Position2D((int)e.X, (int)e.Y);
            UpdateNearestInteractable();
        }

        /// <summary>
        /// Q14-E01 FIX: обработчик запроса взаимодействия от UI.
        /// UI публикует UIInteractRequestEvent, InteractionService реагирует
        /// попыткой взаимодействия с ближайшим объектом.
        /// </summary>
        private void OnUIInteractRequest(in UIInteractRequestEvent e)
        {
            if (string.IsNullOrEmpty(_nearestInteractableId)) return;
            TryInteract(_nearestInteractableId);
        }

        private void UpdateNearestInteractable()
        {
            float range = _config != null ? _config.DefaultInteractionRange : 2f;
            _nearestInteractableId = GetNearestInteractableId(_playerPosition, range);
        }

        public void Dispose()
        {
            _positionChangedSubscription?.Dispose();
            _positionChangedSubscription = null;
            _interactRequestSubscription?.Dispose(); // Q14-E01 FIX
            _interactRequestSubscription = null;
            _interactablePositions.Clear();
        }
    }
}
