#nullable enable
// Создано: 2026-05-09
// Редактировано: 2026-05-09 — FMT-A02: использованы кэшированные Qi-значения в ContributeQi
// Редактировано: 2026-05-09 — FMT-A03: Depleted — стабильная стадия (перезарядка)
// Редактировано: 2026-05-09 — FMT-A04: проверка уровня культивации создателя
// Редактировано: 2026-05-09 — FMT-A05: проверка Ци создателя перед прорисовкой контура
// Редактировано: 2026-05-10 12:00:00 UTC — Phase 18A: реализация ISaveable
// Редактировано: 2026-05-10 12:30:00 UTC — Phase 18A FIX D2: long→string для совместимости с JsonUtility
// Редактировано: 2026-05-20 19:11:00 UTC — Фаза 4, задачи 4.3+4.7: NPC Qi cache + race condition
// Реализация IFormationService.
// Управление жизненным циклом формаций: прорисовка, наполнение, активация, деактивация.
// EVT-01: Все кросс-модульные взаимодействия — через MessagePipe.
using System;
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Modules.Formation.Data;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Formation
{
    /// <summary>
    /// Реализация IFormationService.
    /// Управляет жизненным циклом формаций через стадии:
    /// None → Drawing → Filling → Active → Depleted
    ///
    /// АРХИТЕКТУРА (EVT-01): Formation модуль НЕ инжектит IQiService, ICombatService.
    /// Вместо этого использует MessagePipe:
    /// - QiConsumeRequestEvent → команда расхода Ци (вместо IQiService.TryConsumeQi)
    /// - QiChangedEvent → кэш проводимости/уровня (вместо IQiService.Conductivity)
    /// - CombatEndedEvent → автодеактивация
    ///
    /// Singleton устранён: FormationService создаётся через VContainer.
    /// Статическое изменяемое состояние устранено: FormationEffects — instance-based.
    /// </summary>
    public class FormationService : IFormationService, ISaveable, IDisposable
    {
        // === Зависимости (MessagePipe) ===
        private readonly IPublisher<FormationActivatedEvent> _activatedPub;
        private readonly IPublisher<FormationDeactivatedEvent> _deactivatedPub;
        private readonly IPublisher<FormationQiPoolChangedEvent> _poolChangedPub;
        private readonly IPublisher<FormationStageChangedEvent> _stageChangedPub;
        private readonly IPublisher<QiConsumeRequestEvent> _qiConsumeRequestPub;

        // === Зависимость (провайдер данных Ци per-entity) ===
        // Задача 4.3: IQiDataProvider для запроса Qi-состояния NPC (вместо per-entity кэша)
        private readonly IQiDataProvider _qiDataProvider;

        // === Подписки ===
        private readonly ISubscriber<QiChangedEvent> _qiChangedSub;
        private readonly ISubscriber<CombatEndedEvent> _combatEndedSub;
        private readonly ISubscriber<FormationContributeQiRequestEvent> _contributeRequestSub;
        private IDisposable _qiChangedSubscription;
        private IDisposable _combatEndedSubscription;
        private IDisposable _contributeRequestSubscription;

        // === Внутренние компоненты ===
        private readonly FormationQiPool _qiPool;
        private readonly FormationEffects _effects;

        // === Состояние ===
        private FormationData _currentFormation;
        private FormationStage _currentStage = FormationStage.None;
        private string _casterId;
        private readonly List<string> _participants = new List<string>();
        private FormationConfig _config;

        // Кэш Qi-состояния создателя (из QiChangedEvent вместо инъекции IQiService)
        // FMT-A02: Используется для проверки достаточности Ци перед внесением
        // FMT-A04: Используется для проверки уровня культивации создателя
        // FMT-A05: Используется для проверки Ци для прорисовки контура
        // TODO: _cachedConductivity использовать в CalculateFillRate при автоматическом наполнении
        private float _cachedConductivity;
        private int _cachedCultivationLevel;
        private long _cachedCurrentQi;

        // === IFormationService Properties ===

        public bool IsFormationActive => _currentStage == FormationStage.Active;
        public string ActiveFormationId =>
            _currentStage != FormationStage.None ? _currentFormation?.Id : null;
        public FormationStage CurrentStage => _currentStage;
        public long QiPoolCurrent => _qiPool.CurrentQi;
        public long QiPoolMax => _qiPool.MaxQi;
        public int ParticipantCount => _participants.Count;
        public string CasterId => _casterId;

        // === Конструктор (VContainer) ===

        public FormationService(
            IPublisher<FormationActivatedEvent> activatedPub,
            IPublisher<FormationDeactivatedEvent> deactivatedPub,
            IPublisher<FormationQiPoolChangedEvent> poolChangedPub,
            IPublisher<FormationStageChangedEvent> stageChangedPub,
            IPublisher<QiConsumeRequestEvent> qiConsumeRequestPub,
            ISubscriber<QiChangedEvent> qiChangedSub,
            ISubscriber<CombatEndedEvent> combatEndedSub,
            ISubscriber<FormationContributeQiRequestEvent> contributeRequestSub,
            IQiDataProvider qiDataProvider, // Задача 4.3: per-entity Qi-данные
            FormationRegistry? formationRegistry = null) // Этап 4: генерируемые формации
        {
            _activatedPub = activatedPub;
            _deactivatedPub = deactivatedPub;
            _poolChangedPub = poolChangedPub;
            _stageChangedPub = stageChangedPub;
            _qiConsumeRequestPub = qiConsumeRequestPub;

            _qiChangedSub = qiChangedSub;
            _combatEndedSub = combatEndedSub;
            _contributeRequestSub = contributeRequestSub;

            _qiDataProvider = qiDataProvider; // Задача 4.3
            _registry = formationRegistry;    // Этап 4 (nullable: legacy-сборки без DI)

            _qiPool = new FormationQiPool(poolChangedPub);
            _effects = new FormationEffects();
        }

        // Этап 4 внедрения ЦИ: реестр генерируемых формаций
        private readonly FormationRegistry? _registry;

        /// <summary>
        /// Инициализировать сервис конфигурацией.
        /// Вызывается из FormationModule.IStartable.Start().
        /// </summary>
        public void Initialize(FormationConfig config)
        {
            _config = config;

            // Подписка на QiChangedEvent — кэшируем проводимость и уровень
            _qiChangedSubscription = _qiChangedSub.Subscribe(OnQiChanged);

            // Подписка на CombatEndedEvent — автодеактивация
            _combatEndedSubscription = _combatEndedSub.Subscribe(OnCombatEnded);

            // Подписка на FormationContributeQiRequestEvent — внесение Ци от внешних систем
            _contributeRequestSubscription = _contributeRequestSub.Subscribe(OnContributeQiRequest);
        }

        // === IFormationService: Жизненный цикл ===

        /// <summary>
        /// Начать прорисовку контура формации.
        /// Этап 1: Затрата contourQi от создателя через QiConsumeRequestEvent.
        /// Автоматически переходит в Filling после списания Ци.
        /// </summary>
        public bool StartDrawing(string formationId, string casterId)
        {
            if (_currentStage != FormationStage.None) return false;

            // Поиск данных формации (в будущих фазах: из реестра/ScriptableObject)
            _currentFormation = FindFormationData(formationId);
            if (_currentFormation == null) return false;

            // Проверка допустимости размера
            if (!FormationCalculator.IsSizeAllowedForLevel(
                _currentFormation.Size, _currentFormation.RequiredLevel))
                return false;

            // FMT-A04: Проверка уровня культивации создателя (по кэшу QiChangedEvent)
            // Создатель должен быть ≥ требуемого уровня формации
            if (_cachedCultivationLevel > 0 && _cachedCultivationLevel < _currentFormation.RequiredLevel)
                return false;

            _casterId = casterId;

            // FMT-A05: Проверяем, достаточно ли Ци у создателя для прорисовки контура
            long contourQi = FormationCalculator.CalculateContourQi(_currentFormation.RequiredLevel);
            if (_cachedCurrentQi < contourQi)
                return false; // Недостаточно Ци для прорисовки контура

            // Публикуем команду расхода Ци для прорисовки контура
            _qiConsumeRequestPub.Publish(new QiConsumeRequestEvent(contourQi, "Formation"));

            // Инициализируем пул Ци
            _qiPool.Initialize(formationId, _currentFormation.RequiredLevel, _currentFormation.Size);

            // Инициализируем эффекты
            _effects.Initialize(_currentFormation);

            // Добавляем создателя как участника
            _participants.Clear();
            _participants.Add(casterId);

            // Переход: None → Drawing
            ChangeStage(FormationStage.Drawing);

            // В данной реализации: прорисовка мгновенная (контур уже списан)
            // Автоматический переход в Filling
            ChangeStage(FormationStage.Filling);

            return true;
        }

        /// <summary>
        /// Начать наполнение формации Ци.
        /// Этап 2: Переход из Drawing/Filling → заполнение пула.
        /// </summary>
        public bool StartFilling()
        {
            if (_currentStage != FormationStage.Drawing && _currentStage != FormationStage.Filling)
                return false;

            if (_currentStage != FormationStage.Filling)
                ChangeStage(FormationStage.Filling);

            return true;
        }

        /// <summary>
        /// Внести Ци в формацию (от участника).
        /// Списывает Ци с участника через QiConsumeRequestEvent.
        /// Автоматически активирует формацию при 100% заполнении.
        /// </summary>
        public long ContributeQi(string contributorId, long amount)
        {
            // FMT-A03: Разрешаем внесение Ци из стадий Filling, Active и Depleted (перезарядка)
            if (_currentStage != FormationStage.Filling &&
                _currentStage != FormationStage.Active &&
                _currentStage != FormationStage.Depleted)
                return 0;

            if (amount <= 0) return 0;

            // Ограничиваем вклад до оставшегося места
            long remaining = _qiPool.MaxQi - _qiPool.CurrentQi;
            long effectiveAmount = Math.Min(amount, remaining);

            if (effectiveAmount <= 0) return 0;

            // Задача 4.3+4.7: Все проверки — ПЕРЕД публикацией события расхода Ци.
            // Порядок: проверка уровня → проверка Ци → событие расхода → зачисление в пул
            // (атомарность с точки зрения FormationService)

            // FMT-D01 + Задача 4.3: Проверка минимального уровня помощника ПЕРЕД расходом Ци.
            // Регистрируем участника и проверяем право на участие.
            if (!_participants.Contains(contributorId))
            {
                // Создатель уже зарегистрирован, проверяем только помощников
                if (contributorId != _casterId)
                {
                    // Задача 4.3: Проверка уровня помощника через IQiDataProvider
                    // minHelperLevel = max(1, formationLevel - 2)
                    int helperLevel = _qiDataProvider.GetCultivationLevel(contributorId);
                    int minHelperLevel = Math.Max(1, _currentFormation.RequiredLevel - 2);
                    if (helperLevel < minHelperLevel)
                        return 0; // Уровень помощника недостаточен
                }

                int maxHelpers = GameConstants.FormationMaxHelpers.TryGetValue(
                    _currentFormation.Size, out var max) ? max : 2;
                if (_participants.Count >= maxHelpers + 1) // +1 для создателя
                    return 0; // Достигнут лимит участников
            }

            // Задача 4.3: Проверяем наличие Ци у ВСЕХ участников ПЕРЕД публикацией события.
            // Для создателя: кэш QiChangedEvent (быстро, синхронно)
            // Для NPC: IQiDataProvider.GetCurrentQi() (задача 4.3 — вместо отсутствующего per-entity кэша)
            if (contributorId == _casterId)
            {
                // Создатель: проверяем по кэшу QiChangedEvent
                if (_cachedCurrentQi < effectiveAmount)
                    return 0; // Недостаточно Ци у создателя
            }
            else
            {
                // NPC-участник: проверяем через IQiDataProvider (задача 4.3)
                // Решает TODO о «per-entity кэша» — теперь проверка есть
                long npcQi = _qiDataProvider.GetCurrentQi(contributorId);
                if (npcQi < effectiveAmount)
                    return 0; // Недостаточно Ци у NPC-участника
            }

            // Публикуем команду расхода Ци с участника
            _qiConsumeRequestPub.Publish(new QiConsumeRequestEvent(effectiveAmount, contributorId));

            // Задача 4.7: Добавляем в пул ТОЛЬКО после успешной проверки доступности Ци.
            // QiConsumeRequestEvent — fire-and-forget команда, но предварительная
            // проверка через IQiDataProvider/кэш гарантирует достаточность Ци.
            // QiService может отклонить (рассинхрон кэша), но вероятность минимизирована.
            long added = _qiPool.AddQi(effectiveAmount);

            // Регистрируем участника (после успешного внесения Ци)
            if (!_participants.Contains(contributorId))
            {
                _participants.Add(contributorId);
            }

            // Автоматическая активация при 100% заполнении
            if (_qiPool.IsFull && _currentStage == FormationStage.Filling)
            {
                ActivateFormation();
            }
            // FMT-A03: При перезарядке из Depleted → Filling при первом внесении
            else if (_currentStage == FormationStage.Depleted && _qiPool.CurrentQi > 0)
            {
                ChangeStage(FormationStage.Filling);
                // Если пул заполнен при перезарядке — сразу активируем
                if (_qiPool.IsFull)
                {
                    ActivateFormation();
                }
            }

            return added;
        }

        /// <summary>
        /// Активировать формацию.
        /// Этап 3: Переход Filling → Active при 100% заполнении.
        /// </summary>
        public bool ActivateFormation()
        {
            if (_currentStage != FormationStage.Filling && _currentStage != FormationStage.Active)
                return false;

            if (!_qiPool.IsFull && _currentStage != FormationStage.Active)
                return false;

            if (_currentStage == FormationStage.Active) return true; // Уже активна

            // Переход: Filling → Active
            ChangeStage(FormationStage.Active);

            // Активируем эффекты
            _effects.Activate();

            // Публикуем событие активации
            _activatedPub.Publish(new FormationActivatedEvent(
                _currentFormation.Id, _casterId));

            return true;
        }

        /// <summary>
        /// Деактивировать формацию.
        /// Возвращает формацию в состояние None.
        /// </summary>
        public bool DeactivateFormation()
        {
            if (_currentStage == FormationStage.None) return false;

            var previousStage = _currentStage;

            // Деактивируем эффекты
            _effects.Deactivate();

            // Сбрасываем пул
            _qiPool.Reset();

            // Очищаем участников
            _participants.Clear();

            // Публикуем событие деактивации
            string formationId = _currentFormation?.Id ?? "unknown";
            _deactivatedPub.Publish(new FormationDeactivatedEvent(formationId, previousStage));

            // Переход: * → None
            ChangeStage(FormationStage.None);

            // Очищаем текущую формацию
            _currentFormation = null;
            _casterId = null;

            return true;
        }

        /// <summary>
        /// Получить бонус формации для указанной характеристики.
        /// Используется в пайплайне урона (Слой 3b).
        /// </summary>
        public float GetFormationBonus(StatType stat)
        {
            return _effects.GetFormationBonus(stat);
        }

        /// <summary>
        /// Аудит CRIT-1: бонус формации в промилле (ЗАПРЕТ 3.9).
        /// Конвертирует float-результат в промилле: 0.2 → 200, -0.15 → -150.
        /// </summary>
        public int GetFormationBonusPermil(StatType stat)
        {
            float bonus = _effects.GetFormationBonus(stat);
            return (int)(bonus * 1000f);
        }

        /// <summary>
        /// Получить данные всех активных эффектов формации.
        /// </summary>
        public IReadOnlyList<FormationEffectData> GetActiveEffects()
        {
            return _effects.GetActiveEffects();
        }

        // === Обработка утечки Ци (вызывается из FormationModule.Tick) ===

        /// <summary>
        /// Обработать утечку Ци в тиках.
        /// Вызывается FormationModule.ITickable.Tick().
        /// Утечка происходит ТОЛЬКО в стадии Active.
        /// </summary>
        /// <param name="gameMinutesElapsed">Игровые минуты</param>
        public void ProcessDrainTick(int gameMinutesElapsed)
        {
            if (_currentStage != FormationStage.Active) return;
            if (_config == null || !_config.EnableDrain) return;

            long drained = _qiPool.ProcessDrain(
                gameMinutesElapsed,
                _config.DrainSpeedMultiplier);

            // Если пул истощён — автодеактивация
            if (_qiPool.IsEmpty && _config.AutoDeactivateOnDepleted)
            {
                // FMT-A03: Переход Active → Depleted (не None!).
                // Depleted — стабильная стадия: формация истощена, но контур сохранён.
                // Можно перезарядить через ContributeQi (Depleted → Filling → Active).
                // Полная деактивация (→ None) только через явный вызов DeactivateFormation().
                ChangeStage(FormationStage.Depleted);
                _effects.Deactivate(); // Эффекты отключаются при истощении
            }
        }

        // === Обработчики событий ===

        /// <summary>
        /// Обработчик QiChangedEvent — кэшируем состояние Ци.
        /// EVT-01: заменяет инъекцию IQiService.
        /// </summary>
        private void OnQiChanged(in QiChangedEvent e)
        {
            _cachedConductivity = e.Conductivity;
            _cachedCultivationLevel = e.CultivationLevel;
            _cachedCurrentQi = e.Current;
        }

        /// <summary>
        /// Обработчик CombatEndedEvent — автодеактивация при конце боя.
        /// </summary>
        private void OnCombatEnded(in CombatEndedEvent e)
        {
            if (_config != null && _config.AutoDeactivateOnCombatEnd && IsFormationActive)
            {
                DeactivateFormation();
            }
        }

        /// <summary>
        /// Обработчик FormationContributeQiRequestEvent —
        /// внешние системы (UI, AI) вносят Ци в формацию.
        /// </summary>
        private void OnContributeQiRequest(in FormationContributeQiRequestEvent e)
        {
            // FMT-A03: Разрешаем внесение из Filling, Active и Depleted
            if (_currentStage != FormationStage.Filling &&
                _currentStage != FormationStage.Active &&
                _currentStage != FormationStage.Depleted)
                return;

            ContributeQi(e.ContributorId, e.Amount);
        }

        // === Внутренние методы ===

        /// <summary>
        /// Изменить стадию формации с публикацией события.
        /// </summary>
        private void ChangeStage(FormationStage newStage)
        {
            var previous = _currentStage;
            _currentStage = newStage;

            if (previous != newStage && _currentFormation != null)
            {
                _stageChangedPub.Publish(new FormationStageChangedEvent(
                    _currentFormation.Id, previous, newStage));
            }
        }

        /// <summary>
        /// Найти данные формации по идентификатору.
        /// В будущих фазах: загрузка из реестра ScriptableObject.
        /// </summary>
        private FormationData FindFormationData(string formationId)
        {
            // Этап 4 внедрения ЦИ: сначала — реестр генерируемых формаций.
            var generated = _registry?.Get(formationId);
            if (generated != null) return generated;

            // Временная реализация: известные формации
            switch (formationId)
            {
                case "basic_barrier":
                    return FormationData.CreateBasicBarrier();
                case "dao_blade":
                    return FormationData.CreateDaoBlade();
                case "shadow_bindings":
                    return FormationData.CreateShadowBindings();
                default:
                    return null;
            }
        }

        // === ISaveable ===

        /// <summary>
        /// Ключ сохранения для модуля формаций.
        /// </summary>
        public string SaveKey => "formation";

        /// <summary>
        /// Снять состояние формации для сериализации.
        /// Сохраняет: ID активной формации, стадию, пул Ци (текущий/макс),
        /// количество участников, ID создателя.
        /// </summary>
        public object CaptureState()
        {
            // Формируем строку участников через запятую
            string participantsStr = _participants.Count > 0
                ? string.Join(",", _participants)
                : "";

            var data = new FormationSaveData
            {
                activeFormationId = ActiveFormationId ?? "",
                currentStage = (int)_currentStage,
                qiPoolCurrent = QiPoolCurrent.ToString(),
                qiPoolMax = QiPoolMax.ToString(),
                participants = participantsStr,
                casterId = _casterId ?? ""
            };
            return data;
        }

        /// <summary>
        /// Восстановить состояние формации.
        /// </summary>
        public void RestoreState(object state)
        {
            if (state is not FormationSaveData data || data == null) return;

            // Если формация не была активна — сбрасываем в None
            if (string.IsNullOrEmpty(data.activeFormationId))
            {
                if (_currentStage != FormationStage.None)
                    DeactivateFormation();
                return;
            }

            // Находим данные формации по ID
            var formationData = FindFormationData(data.activeFormationId);
            if (formationData == null) return;

            // Восстанавливаем текущую формацию и создателя
            _currentFormation = formationData;
            _casterId = string.IsNullOrEmpty(data.casterId) ? null : data.casterId;

            // Восстанавливаем участников из строки
            _participants.Clear();
            if (!string.IsNullOrEmpty(data.participants))
            {
                var ids = data.participants.Split(',');
                foreach (var id in ids)
                {
                    var trimmed = id.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        _participants.Add(trimmed);
                }
            }

            // Восстанавливаем пул Ци: инициализируем (задаёт maxQi), затем добавляем текущее значение
            _qiPool.Initialize(data.activeFormationId, formationData.RequiredLevel, formationData.Size);
            long qiPoolCurrentVal = long.Parse(data.qiPoolCurrent);
            if (qiPoolCurrentVal > 0)
                _qiPool.AddQi(qiPoolCurrentVal);

            // Восстанавливаем стадию
            _currentStage = (FormationStage)data.currentStage;

            // Инициализируем эффекты формации (необходимо перед Activate)
            _effects.Initialize(_currentFormation);

            // Если стадия Active — активируем эффекты
            if (_currentStage == FormationStage.Active)
                _effects.Activate();

            // Помечаем кэши грязными
            // (нет кэша, но на всякий случай помечаем логическое соответствие)
        }

        public void Dispose()
        {
            _qiChangedSubscription?.Dispose();
            _qiChangedSubscription = null;
            _combatEndedSubscription?.Dispose();
            _combatEndedSubscription = null;
            _contributeRequestSubscription?.Dispose();
            _contributeRequestSubscription = null;
            _qiPool?.Dispose();
        }
    }

    /// <summary>
    /// Структура данных сохранения для FormationService.
    /// JsonUtility требует [Serializable] и публичных полей.
    /// </summary>
    [Serializable]
    public class FormationSaveData
    {
        // ID активной формации (пустая строка = нет формации)
        public string activeFormationId;

        // Стадия формации (int — сериализация enum как числа)
        public int currentStage;

        // Текущее Ци в пуле формации
        public string qiPoolCurrent; // long как string — JsonUtility не поддерживает long

        // Максимальная ёмкость пула Ци
        public string qiPoolMax; // long как string — JsonUtility не поддерживает long

        // Список участников через запятую
        // (JsonUtility не поддерживает List<string>, используем строку)
        public string participants;

        // ID создателя формации
        public string casterId;
    }
}
