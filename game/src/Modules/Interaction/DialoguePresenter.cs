#nullable enable
// Создано: 2026-05-09 — Phase 14: Dialogue Presenter
// Подписывается на диалоговые события и предоставляет данные для UI диалога.
// EVT-01: НЕ инжектит IDialogueService — только MessagePipe.
using System;
using System.Collections.Generic;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Core.Events;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.UI
{
    /// <summary>
    /// Dialogue Presenter.
    /// Подписывается на диалоговые события и предоставляет данные для UI.
    ///
    /// Подписки (Sub):
    /// - DialogueStartedEvent → показ диалогового окна
    /// - DialogueEndedEvent → скрытие диалогового окна
    /// - DialogueChoiceSelectedEvent → обновление выбора
    ///
    /// Публикации (Pub):
    /// - UIAdvanceDialogueRequestEvent → продвижение диалога
    /// - UISelectChoiceRequestEvent → выбор в диалоге
    ///
    /// АРХИТЕКТУРА (EVT-01): DialoguePresenter НЕ вызывает IDialogueService.
    /// Только Pub/Sub через MessagePipe.
    /// </summary>
    public class DialoguePresenter : IDisposable
    {
        // === MessagePipe: паблишеры ===
        private readonly IPublisher<UIAdvanceDialogueRequestEvent> _advanceDialoguePub;
        private readonly IPublisher<UISelectChoiceRequestEvent> _selectChoicePub;

        // === MessagePipe: подписки ===
        private readonly ISubscriber<DialogueStartedEvent> _dialogueStartedSub;
        private readonly ISubscriber<DialogueEndedEvent> _dialogueEndedSub;
        private readonly ISubscriber<DialogueChoiceSelectedEvent> _choiceSelectedSub;
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();

        // === Состояние ===
        private bool _isDialogueOpen;
        private string _currentNpcId;
        private string _currentDialogueId;
        private int _lastChoiceIndex = -1;

        public DialoguePresenter(
            IPublisher<UIAdvanceDialogueRequestEvent> advanceDialoguePub,
            IPublisher<UISelectChoiceRequestEvent> selectChoicePub,
            ISubscriber<DialogueStartedEvent> dialogueStartedSub,
            ISubscriber<DialogueEndedEvent> dialogueEndedSub,
            ISubscriber<DialogueChoiceSelectedEvent> choiceSelectedSub)
        {
            _advanceDialoguePub = advanceDialoguePub;
            _selectChoicePub = selectChoicePub;
            _dialogueStartedSub = dialogueStartedSub;
            _dialogueEndedSub = dialogueEndedSub;
            _choiceSelectedSub = choiceSelectedSub;
        }

        /// <summary>
        /// Инициализация подписок.
        /// </summary>
        public void Initialize()
        {
            foreach (var sub in _subscriptions) sub.Dispose();
            _subscriptions.Clear();

            _subscriptions.Add(_dialogueStartedSub.Subscribe(OnDialogueStarted));
            _subscriptions.Add(_dialogueEndedSub.Subscribe(OnDialogueEnded));
            _subscriptions.Add(_choiceSelectedSub.Subscribe(OnChoiceSelected));
        }

        // === Свойства для UI ===

        /// <summary>Открыт ли диалог</summary>
        internal bool IsDialogueOpen => _isDialogueOpen;

        /// <summary>Текущий NPC</summary>
        internal string CurrentNpcId => _currentNpcId;

        /// <summary>Текущий диалог</summary>
        internal string CurrentDialogueId => _currentDialogueId;

        // === Команды от UI (через MessagePipe) ===

        /// <summary>
        /// Продвинуть диалог (нажатие / клик).
        /// Публикует UIAdvanceDialogueRequestEvent.
        /// </summary>
        internal void RequestAdvance()
        {
            _advanceDialoguePub.Publish(new UIAdvanceDialogueRequestEvent());
        }

        /// <summary>
        /// Выбрать вариант в диалоге.
        /// Публикует UISelectChoiceRequestEvent.
        /// </summary>
        internal void RequestChoice(int choiceIndex)
        {
            _selectChoicePub.Publish(new UISelectChoiceRequestEvent(choiceIndex));
        }

        // === Обработчики событий ===

        private void OnDialogueStarted(in DialogueStartedEvent e)
        {
            _isDialogueOpen = true;
            _currentNpcId = e.NpcId;
            _currentDialogueId = e.DialogueId;
            _lastChoiceIndex = -1;
        }

        private void OnDialogueEnded(in DialogueEndedEvent e)
        {
            _isDialogueOpen = false;
            _currentNpcId = null;
            _currentDialogueId = null;
        }

        private void OnChoiceSelected(in DialogueChoiceSelectedEvent e)
        {
            _lastChoiceIndex = e.ChoiceIndex;
        }

        public void Dispose()
        {
            foreach (var sub in _subscriptions) sub.Dispose();
            _subscriptions.Clear();
        }
    }
}
