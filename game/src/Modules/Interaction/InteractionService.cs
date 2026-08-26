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
        private readonly IPublisher<NPCInteractedEvent> _npcInteractedPub; // Q13-E01 FIX

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
        /// Реестр интерактивных объектов.
        /// В будущем — динамический, пополняемый через события.
        /// Пока — простой словарь Id → позиция.
        /// </summary>
        private readonly Dictionary<string, Position2D> _interactablePositions = new Dictionary<string, Position2D>();

        public InteractionService(
            IPublisher<InteractionCompletedEvent> interactionCompletedPub,
            IPublisher<NPCInteractedEvent> npcInteractedPub, // Q13-E01 FIX
            ISubscriber<PlayerPositionChangedEvent> positionChangedSub,
            ISubscriber<UIInteractRequestEvent> interactRequestSub) // Q14-E01 FIX
        {
            _interactionCompletedPub = interactionCompletedPub;
            _npcInteractedPub = npcInteractedPub; // Q13-E01 FIX
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

            // Регистрация тестовых интерактивных объектов
            RegisterDefaultInteractables();
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
            if (!_interactablePositions.ContainsKey(targetId)) return false;

            // Проверка дальности
            float distSq = (_interactablePositions[targetId] - _playerPosition).SqrMagnitude;
            float range = _config != null ? _config.DefaultInteractionRange : 2f;
            if (distSq > range * range) return false;

            // A03-fix: Используем константу вместо магической строки
            _interactionCompletedPub.Publish(new InteractionCompletedEvent(
                targetId, GameConstants.InteractionType.Interact));

            // Q13-E01 FIX: Публикация NPCInteractedEvent для NPC-целей
            // Позволяет DialogueService реагировать на взаимодействия с NPC
            if (IsNPCInteractable(targetId))
            {
                _npcInteractedPub.Publish(new NPCInteractedEvent(
                    targetId, "player", GameConstants.InteractionType.Talk));
            }

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

        /// <summary>
        /// Q13-E01 FIX: Проверяет, является ли интерактивный объект NPC.
        /// NPC-идентификаторы содержат ключевые слова: elder_, merchant_, guard_ и т.д.
        /// </summary>
        private bool IsNPCInteractable(string targetId)
        {
            if (string.IsNullOrEmpty(targetId)) return false;
            // NPC-объекты определяются по паттерну идентификатора
            return targetId.StartsWith("elder_")
                || targetId.StartsWith("merchant_")
                || targetId.StartsWith("guard_")
                || targetId.StartsWith("villager_")
                || targetId.StartsWith("smith_");
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

        // === Тестовые данные ===

        private void RegisterDefaultInteractables()
        {
            // Тестовые интерактивные объекты (в будущем — через события)
            RegisterInteractable("elder_01", new Position2D((int)5f, (int)5f));
            RegisterInteractable("merchant_01", new Position2D((int)10f, (int)3f));
            RegisterInteractable("chest_01", new Position2D((int)3f, (int)8f));
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
