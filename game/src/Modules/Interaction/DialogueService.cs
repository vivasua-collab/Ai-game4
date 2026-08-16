#nullable enable
// Создано: 2026-05-09 — Phase 13: реализация IDialogueService
// Управление диалогами: ветки, выборы, типографический эффект.
// EVT-01: НЕ инжектит INPCService — только подписки MessagePipe.
// Редактировано: 2026-05-10 — Phase 17A: Q13-E01, Q13-E02 fixes
using System;
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Modules.Interaction.Data;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Interaction
{
    /// <summary>
    /// Реализация IDialogueService.
    /// Управляет деревом диалога: начало, продвижение, выбор, конец.
    ///
    /// АРХИТЕКТУРА (EVT-01): Модуль Interaction НЕ инжектит INPCService.
    /// Кросс-модульные данные — через MessagePipe:
    /// - DialogueStartedEvent, DialogueEndedEvent, DialogueChoiceSelectedEvent — публикация
    /// - NPCInteractedEvent → подписка (автостарт диалога при AI-инициированном talk)
    /// - InteractionCompletedEvent → подписка (автостарт диалога при клике игрока)
    /// </summary>
    public class DialogueService : IDialogueService, IDisposable
    {
        // === MessagePipe: паблишеры ===
        private readonly IPublisher<DialogueStartedEvent> _dialogueStartedPub;
        private readonly IPublisher<DialogueEndedEvent> _dialogueEndedPub;
        private readonly IPublisher<DialogueChoiceSelectedEvent> _choiceSelectedPub;

        // === MessagePipe: подписки ===
        private readonly ISubscriber<NPCInteractedEvent> _npcInteractedSub;
        private readonly ISubscriber<InteractionCompletedEvent> _interactionCompletedSub;
        private readonly ISubscriber<UIAdvanceDialogueRequestEvent> _uiAdvanceSub; // Q13-E02 FIX
        private readonly ISubscriber<UISelectChoiceRequestEvent> _uiSelectChoiceSub; // Q13-E02 FIX
        private IDisposable _npcInteractedSubscription;
        private IDisposable _interactionCompletedSubscription;
        private IDisposable _uiAdvanceSubscription; // Q13-E02 FIX
        private IDisposable _uiSelectChoiceSubscription; // Q13-E02 FIX

        // === Typewriter-эффект ===
        private readonly DialogueTypewriter _typewriter;

        // === Состояние диалога ===
        private string _currentNpcId;
        private string _currentDialogueId;
        private DialogueNode _currentNode;
        private bool _isInDialogue;

        // === Реестр диалогов: dialogueId → Dictionary<NodeId, DialogueNode> ===
        private readonly Dictionary<string, Dictionary<string, DialogueNode>> _dialogues
            = new Dictionary<string, Dictionary<string, DialogueNode>>();

        // === Связь NPC → диалог ===
        private readonly Dictionary<string, string> _npcDialogueMap = new Dictionary<string, string>();

        public DialogueService(
            IPublisher<DialogueStartedEvent> dialogueStartedPub,
            IPublisher<DialogueEndedEvent> dialogueEndedPub,
            IPublisher<DialogueChoiceSelectedEvent> choiceSelectedPub,
            ISubscriber<NPCInteractedEvent> npcInteractedSub,
            ISubscriber<InteractionCompletedEvent> interactionCompletedSub,
            ISubscriber<UIAdvanceDialogueRequestEvent> uiAdvanceSub, // Q13-E02 FIX
            ISubscriber<UISelectChoiceRequestEvent> uiSelectChoiceSub, // Q13-E02 FIX
            DialogueTypewriter typewriter)
        {
            _dialogueStartedPub = dialogueStartedPub;
            _dialogueEndedPub = dialogueEndedPub;
            _choiceSelectedPub = choiceSelectedPub;
            _npcInteractedSub = npcInteractedSub;
            _interactionCompletedSub = interactionCompletedSub;
            _uiAdvanceSub = uiAdvanceSub; // Q13-E02 FIX
            _uiSelectChoiceSub = uiSelectChoiceSub; // Q13-E02 FIX
            _typewriter = typewriter;
        }

        /// <summary>
        /// Инициализация: подписки + дефолтные диалоги.
        /// C01-fix: Dispose подписок перед повторной подпиской (double-Initialize safe).
        /// </summary>
        public void Initialize(InteractionConfig config)
        {
            _typewriter.Speed = Math.Max(config.TypewriterSpeed, 0.01f); // B02-fix: Speed >= 0.01

            // B01-fix: Dispose предыдущих подписок при повторном Initialize
            _npcInteractedSubscription?.Dispose();
            _interactionCompletedSubscription?.Dispose();
            _uiAdvanceSubscription?.Dispose(); // Q13-E02 FIX
            _uiSelectChoiceSubscription?.Dispose(); // Q13-E02 FIX

            // Подписка: при AI-инициированном взаимодействии с NPC (тип "talk")
            _npcInteractedSubscription = _npcInteractedSub.Subscribe(OnNPCInteracted);

            // C01-fix: Подписка на InteractionCompletedEvent — мост от InteractionService
            _interactionCompletedSubscription = _interactionCompletedSub.Subscribe(OnInteractionCompleted);

            // Q13-E02 FIX: Подписка на UI-запросы для управления диалогом
            _uiAdvanceSubscription = _uiAdvanceSub.Subscribe(OnUIAdvanceDialogue);
            _uiSelectChoiceSubscription = _uiSelectChoiceSub.Subscribe(OnUISelectChoice);

            RegisterDefaultDialogues();
        }

        // === IDialogueService ===

        public bool StartDialogue(string npcId, string dialogueId)
        {
            if (_isInDialogue) return false;
            if (!_dialogues.TryGetValue(dialogueId, out var nodes)) return false;
            if (!nodes.TryGetValue("start", out var startNode)) return false;

            _currentNpcId = npcId;
            _currentDialogueId = dialogueId;
            _currentNode = startNode;
            _isInDialogue = true;

            // Запуск typewriter-эффекта
            _typewriter.StartText(_currentNode.Text);

            _dialogueStartedPub.Publish(new DialogueStartedEvent(npcId, dialogueId));
            return true;
        }

        public void AdvanceDialogue()
        {
            if (!_isInDialogue || _currentNode == null) return;

            // Если typewriter ещё не завершён — досрочно показать весь текст
            if (!_typewriter.IsComplete)
            {
                _typewriter.CompleteImmediately();
                return;
            }

            // Если текущий узел — конечный, завершаем диалог
            if (_currentNode.IsEndNode)
            {
                EndDialogue();
                return;
            }

            // Если есть линейный переход (без выборов) — перейти к следующему узлу
            if (_currentNode.Choices.Count == 0 && !string.IsNullOrEmpty(_currentNode.NextNodeId))
            {
                GoToNode(_currentNode.NextNodeId);
                return;
            }

            // Если есть выборы — ждём SelectChoice() от UI
            // Ничего не делаем (UI должен показать варианты)
        }

        public void SelectChoice(int choiceIndex)
        {
            if (!_isInDialogue || _currentNode == null) return;
            if (choiceIndex < 0 || choiceIndex >= _currentNode.Choices.Count) return;

            var choice = _currentNode.Choices[choiceIndex];

            // Публикуем событие выбора
            _choiceSelectedPub.Publish(new DialogueChoiceSelectedEvent(
                _currentNpcId, _currentDialogueId, choiceIndex));

            // Переходим к узлу, указанному в выборе
            if (!string.IsNullOrEmpty(choice.TargetNodeId))
            {
                GoToNode(choice.TargetNodeId);
            }
            else
            {
                EndDialogue();
            }
        }

        public void EndDialogue()
        {
            if (!_isInDialogue) return;

            string npcId = _currentNpcId;
            string dialogueId = _currentDialogueId;

            _currentNpcId = null;
            _currentDialogueId = null;
            _currentNode = null;
            _isInDialogue = false;
            _typewriter.Stop();

            _dialogueEndedPub.Publish(new DialogueEndedEvent(npcId, dialogueId));
        }

        public bool IsInDialogue => _isInDialogue;
        public string CurrentDialogueId => _currentDialogueId ?? "";

        // === Дополнительные свойства (для UI) ===

        /// <summary>Текущий отображаемый текст (typewriter)</summary>
        internal string CurrentDisplayText => _typewriter.DisplayText;

        /// <summary>Текущий полный текст узла</summary>
        internal string CurrentFullText => _currentNode?.Text ?? "";

        /// <summary>Завершён ли typewriter-эффект</summary>
        internal bool IsTypewriterComplete => _typewriter.IsComplete;

        /// <summary>Текущие варианты выбора</summary>
        internal IReadOnlyList<DialogueChoice> CurrentChoices
        {
            get
            {
                if (_currentNode == null || _currentNode.Choices.Count == 0)
                    return Array.Empty<DialogueChoice>();
                return _currentNode.Choices;
            }
        }

        // === Обработчики событий ===

        /// <summary>
        /// При AI-инициированном взаимодействии с NPC (тип "talk") — автостарт диалога.
        /// </summary>
        private void OnNPCInteracted(in NPCInteractedEvent e)
        {
            if (_isInDialogue) return;
            if (e.InteractionType != GameConstants.InteractionType.Talk) return;

            if (_npcDialogueMap.TryGetValue(e.NpcId, out var dialogueId))
            {
                StartDialogue(e.NpcId, dialogueId);
            }
        }

        /// <summary>
        /// C01-fix: При завершении взаимодействия (клик игрока) — автостарт диалога,
        /// если цель — NPC с привязанным диалогом.
        /// </summary>
        private void OnInteractionCompleted(in InteractionCompletedEvent e)
        {
            if (_isInDialogue) return;

            // Проверяем, есть ли у цели диалог
            if (_npcDialogueMap.TryGetValue(e.TargetId, out var dialogueId))
            {
                StartDialogue(e.TargetId, dialogueId);
            }
        }

        /// <summary>
        /// Q13-E02 FIX: Обработка запроса UI на продвижение диалога.
        /// </summary>
        private void OnUIAdvanceDialogue(in UIAdvanceDialogueRequestEvent e)
        {
            AdvanceDialogue();
        }

        /// <summary>
        /// Q13-E02 FIX: Обработка запроса UI на выбор в диалоге.
        /// </summary>
        private void OnUISelectChoice(in UISelectChoiceRequestEvent e)
        {
            SelectChoice(e.ChoiceIndex);
        }

        // === Внутренние методы ===

        private void GoToNode(string nodeId)
        {
            if (!_dialogues.TryGetValue(_currentDialogueId, out var nodes))
            {
                Console.WriteLine($"[DialogueService] GoToNode: диалог '{_currentDialogueId}' не найден");
                return;
            }
            if (!nodes.TryGetValue(nodeId, out var node))
            {
                Console.WriteLine($"[DialogueService] GoToNode: узел '{nodeId}' не найден в диалоге '{_currentDialogueId}'");
                return;
            }

            _currentNode = node;
            _typewriter.StartText(_currentNode.Text);

            // Если новый узел — конечный, после typewriter можно завершить
            if (_currentNode.IsEndNode && _typewriter.IsComplete)
            {
                // Ждём AdvanceDialogue() для завершения
            }
        }

        /// <summary>
        /// Регистрация диалога.
        /// </summary>
        internal void RegisterDialogue(string dialogueId, Dictionary<string, DialogueNode> nodes)
        {
            if (string.IsNullOrEmpty(dialogueId) || nodes == null) return;
            _dialogues[dialogueId] = nodes;
        }

        /// <summary>
        /// Связать NPC с диалогом.
        /// </summary>
        internal void MapNpcDialogue(string npcId, string dialogueId)
        {
            _npcDialogueMap[npcId] = dialogueId;
        }

        // === Тестовые диалоги ===

        private void RegisterDefaultDialogues()
        {
            // Диалог старейшины
            var elderDialogue = new Dictionary<string, DialogueNode>
            {
                ["start"] = new DialogueNode
                {
                    NodeId = "start",
                    Text = "Приветствую тебя, путник. Я старейшина этой деревни. Чем могу помочь?",
                    Choices =
                    {
                        new DialogueChoice { Index = 0, Text = "Расскажи о деревне", TargetNodeId = "about_village" },
                        new DialogueChoice { Index = 1, Text = "Мне нужны задания", TargetNodeId = "quests" },
                        new DialogueChoice { Index = 2, Text = "Прощай", TargetNodeId = null }
                    }
                },
                ["about_village"] = new DialogueNode
                {
                    NodeId = "about_village",
                    Text = "Наша деревня стоит у подножия Духовных гор. Когда-то здесь кипела жизнь, но теперь волки и демоны терроризируют окрестности...",
                    NextNodeId = "about_village_2"
                },
                ["about_village_2"] = new DialogueNode
                {
                    NodeId = "about_village_2",
                    Text = "Будь осторожен, путник. За лесом обитают существа, с которыми не справиться простому смертному.",
                    Choices =
                    {
                        new DialogueChoice { Index = 0, Text = "Я пойду", TargetNodeId = null }
                    }
                },
                ["quests"] = new DialogueNode
                {
                    NodeId = "quests",
                    Text = "Волки терроризируют пастухов, а кузнецу нужно железо. Поможешь?",
                    Choices =
                    {
                        new DialogueChoice { Index = 0, Text = "Конечно, помогу", TargetNodeId = "quests_accept" },
                        new DialogueChoice { Index = 1, Text = "Потом", TargetNodeId = null }
                    }
                },
                ["quests_accept"] = new DialogueNode
                {
                    NodeId = "quests_accept",
                    Text = "Благодарю! Возвращайся, когда выполнишь поручения."
                }
            };
            RegisterDialogue("dialogue_elder", elderDialogue);
            MapNpcDialogue("elder_01", "dialogue_elder");

            // Диалог торговца
            var merchantDialogue = new Dictionary<string, DialogueNode>
            {
                ["start"] = new DialogueNode
                {
                    NodeId = "start",
                    Text = "Добро пожаловать в мою лавку! У меня есть всё, что нужно путнику.",
                    Choices =
                    {
                        new DialogueChoice { Index = 0, Text = "Покажи товары", TargetNodeId = "show_goods" },
                        new DialogueChoice { Index = 1, Text = "До свидания", TargetNodeId = null }
                    }
                },
                ["show_goods"] = new DialogueNode
                {
                    NodeId = "show_goods",
                    Text = "Вот, смотри: зелья, свитки, снаряжение. Выбирай, что по карману!"
                }
            };
            RegisterDialogue("dialogue_merchant", merchantDialogue);
            MapNpcDialogue("merchant_01", "dialogue_merchant");
        }

        public void Dispose()
        {
            _npcInteractedSubscription?.Dispose();
            _npcInteractedSubscription = null;
            _interactionCompletedSubscription?.Dispose();
            _interactionCompletedSubscription = null;
            _uiAdvanceSubscription?.Dispose(); // Q13-E02 FIX
            _uiAdvanceSubscription = null; // Q13-E02 FIX
            _uiSelectChoiceSubscription?.Dispose(); // Q13-E02 FIX
            _uiSelectChoiceSubscription = null; // Q13-E02 FIX
            _typewriter?.Stop();
            _dialogues.Clear();
            _npcDialogueMap.Clear();
        }
    }
}
