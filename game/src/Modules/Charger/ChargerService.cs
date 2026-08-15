#nullable enable
// Создано: 2026-05-08 12:52:40 UTC
// Редактировано: 2026-05-08 14:22:17 UTC — аудит: CH-02, CH-03, CH-05 (QiChangedEvent), CH-11 (убраны [Inject] с полей)
// Редактировано: 2026-05-08 15:20 UTC — аудит CH-16: проверка боя при TransferToPractitioner, CH-17: фикс DepleteStones remainder
// Редактировано: 2026-05-09 04:35:17 UTC — Phase 4 Qi: IQiService.CurrentQi long (убран каст), TryConsumeQi(long), AddQi
// Редактировано: 2026-05-09 — EVT: убрана инъекция IQiService, кросс-модульные вызовы через MessagePipe
// Редактировано: 2026-05-10 11:10:00 UTC — добавлена публикация FormationContributeQiRequestEvent
//   CurrentQi/Conductivity → кэш из QiChangedEvent
//   TryConsumeQi/AddQi → QiConsumeRequestEvent/QiAddRequestEvent
// Редактировано: 2026-05-10 12:00:00 UTC — Phase 18A: реализация ISaveable
// Редактировано: 2026-05-10 12:30:00 UTC — Phase 18A FIX D2: long→string для совместимости с JsonUtility
// Реализация IChargerService — сервис зарядников Ци.
// Замена: MonoBehaviour → VContainer [Inject], C# events → MessagePipe
using System;
using System.Collections.Generic;
using System.Linq;
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Core.Events;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Charger
{
    /// <summary>
    /// Сервис зарядников Ци.
    /// Управляет слотами камней, буфером, тепловым балансом.
    /// Зависимости: ITimeService — через конструктор (stub в Core).
    /// Кросс-модульные вызовы (Qi) — через MessagePipe события.
    /// </summary>
    public class ChargerService : IChargerService, ISaveable, IDisposable
    {
        // === Зависимости (DI через конструктор) ===
        // CH-11: [Inject] убраны — VContainer использует конструктор
        private readonly ITimeService _timeService;

        // === Messaging (внутримодульные) ===
        private readonly IPublisher<ChargerStateChangedEvent> _stateChangedPublisher;
        // QiChangedPublisher убран (Phase 4): QiService.AddQi() публикует событие внутренне

        // === Messaging (кросс-модульные команды) ===
        private readonly IPublisher<QiConsumeRequestEvent> _qiConsumeRequestPub;
        private readonly IPublisher<QiAddRequestEvent> _qiAddRequestPub;
        private readonly IPublisher<FormationContributeQiRequestEvent> _contributeQiRequestPub;

        // === Кэш кросс-модульного состояния (из QiChangedEvent) ===
        private long _cachedCurrentQi;
        private float _cachedConductivity;
        private IDisposable _qiChangedSubscription;

        // === Внутренние компоненты ===
        private readonly List<ChargerSlot> _slots = new List<ChargerSlot>();
        private readonly ChargerBuffer _buffer;
        private readonly ChargerHeat _heat;

        // === Состояние ===
        private ChargerMode _mode = ChargerMode.Off;

        // === Свойства IChargerService ===

        public bool IsOperational => !_heat.IsOverheated && _mode == ChargerMode.On;
        public bool IsOverheated => _heat.IsOverheated;
        public float HeatLevel => _heat.HeatLevel;
        public HeatState HeatState => _heat.State;
        public int SlotCount => _slots.Count;
        public int ActiveSlotsCount => _slots.Count(s => s.HasStone);
        public long BufferQi => _buffer.CurrentQi;
        public long BufferCapacity => _buffer.Capacity;
        public ChargerMode Mode => _mode;

        // === Конструктор (VContainer) ===

        public ChargerService(
            ITimeService timeService,
            IPublisher<ChargerStateChangedEvent> stateChangedPublisher,
            IPublisher<ChargerBufferChangedEvent> bufferChangedPublisher,
            IPublisher<ChargerHeatChangedEvent> heatChangedPublisher,
            IPublisher<ChargerOverheatedEvent> overheatedPublisher,
            IPublisher<ChargerCooledDownEvent> cooledDownPublisher,
            IPublisher<QiConsumeRequestEvent> qiConsumeRequestPub,
            IPublisher<QiAddRequestEvent> qiAddRequestPub,
            IPublisher<FormationContributeQiRequestEvent> contributeQiRequestPub,
            ISubscriber<QiChangedEvent> qiChangedSub)
            // qiChangedPublisher убран (Phase 4): QiService.AddQi() публикует событие внутренне
        {
            _timeService = timeService;
            _stateChangedPublisher = stateChangedPublisher;
            _qiConsumeRequestPub = qiConsumeRequestPub;
            _qiAddRequestPub = qiAddRequestPub;
            _contributeQiRequestPub = contributeQiRequestPub;

            _buffer = new ChargerBuffer(bufferChangedPublisher);
            _heat = new ChargerHeat(heatChangedPublisher, overheatedPublisher, cooledDownPublisher);

            // EVT: подписка на QiChangedEvent для кэширования состояния Ци
            // вместо инъекции IQiService
            _qiChangedSubscription = qiChangedSub.Subscribe((in QiChangedEvent e) => {
                _cachedCurrentQi = e.Current;
                _cachedConductivity = e.Conductivity;
            });
        }

        // === Инициализация ===

        /// <summary>
        /// Настроить зарядник по конфигурации.
        /// Вызывается из ChargerModule.Start() после DI.
        /// </summary>
        public void Configure(ChargerBufferConfig bufferConfig, List<ChargerSlotConfig> slotConfigs)
        {
            // Настраиваем слоты
            _slots.Clear();
            foreach (var slotConfig in slotConfigs)
            {
                _slots.Add(new ChargerSlot(slotConfig));
            }

            // Настраиваем буфер
            _buffer.Configure(bufferConfig.Capacity, bufferConfig.Conductivity, bufferConfig.EfficiencyLoss);
        }

        // === Управление режимом ===

        public void Activate()
        {
            if (_mode == ChargerMode.On) return;
            _mode = ChargerMode.On;
        }

        public void Deactivate()
        {
            if (_mode == ChargerMode.Off) return;
            _mode = ChargerMode.Off;
        }

        // === Операции со слотами ===

        public ChargerSlotState GetSlotState(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Count) return ChargerSlotState.Inactive;
            return _slots[slotIndex].State;
        }

        public bool TryCharge(int slotIndex, float qiAmount)
        {
            // CH-06: Камень Ци — не перезаряжаемый источник.
            // Зарядка слота = вставка нового камня через InsertStone().
            // Метод оставлен в интерфейсе для будушей расширяемости
            // (например, перезаряжаемые камни из ScriptableObject).
            return false;
        }

        public bool TryDischarge(int slotIndex, float qiAmount)
        {
            // CH-06: Извлечь Ци из камня и добавить в буфер зарядника
            if (slotIndex < 0 || slotIndex >= _slots.Count) return false;
            var slot = _slots[slotIndex];
            if (!slot.HasStone) return false;

            long extracted = slot.InsertedStone.ExtractQi((long)qiAmount);
            if (extracted <= 0) return false;

            // Ци из камня попадает в буфер зарядника
            long added = _buffer.AddQi(extracted);
            return added > 0;
        }

        // === Операции с Ци ===

        public bool UseQiForTechnique(long qiCost)
        {
            if (_heat.IsOverheated) return false;
            // CH-24: qiCost ≤ 0 — некорректный вызов, может добавить Ци через TryConsumeQi
            if (qiCost <= 0) return false;

            long practitionerQi = _cachedCurrentQi; // EVT: кэш из QiChangedEvent

            if (!_buffer.CanUseTechnique(qiCost, practitionerQi)) return false;

            ChargerBufferResult result = _buffer.UseQiForTechnique(qiCost, practitionerQi);

            // Тратим из ядра практика
            if (result.QiFromCore > 0)
            {
                // EVT: QiConsumeRequestEvent вместо прямого вызова IQiService.TryConsumeQi
                _qiConsumeRequestPub.Publish(new QiConsumeRequestEvent((long)result.QiFromCore, "Charger"));
            }

            // Добавляем тепло от использования буфера
            if (result.QiFromBuffer > 0)
            {
                _heat.AddHeatFromQi(result.QiFromBuffer);
            }

            return true;
        }

        public bool CanUseTechnique(long qiCost)
        {
            if (_heat.IsOverheated) return false;
            // CH-24: qiCost ≤ 0 — некорректный вызов
            if (qiCost <= 0) return false;
            long practitionerQi = _cachedCurrentQi; // EVT: кэш из QiChangedEvent
            return _buffer.CanUseTechnique(qiCost, practitionerQi);
        }

        public long GetAvailableQi()
        {
            long practitionerQi = _cachedCurrentQi; // EVT: кэш из QiChangedEvent
            return _buffer.GetEffectiveQiAvailable(practitionerQi);
        }

        // === Боевой режим ===

        public void EnterCombat()
        {
            _heat.EnterCombat();
            // Автоактивация в бою
            if (_mode == ChargerMode.Off)
            {
                Activate();
            }
        }

        public void ExitCombat()
        {
            _heat.ExitCombat();
        }

        // === Вставка/извлечение камней (доп. API) ===

        /// <summary>Вставить камень в слот</summary>
        public bool InsertStone(QiStone stone, int slotIndex = -1)
        {
            ChargerSlot targetSlot = null;

            if (slotIndex >= 0 && slotIndex < _slots.Count)
            {
                targetSlot = _slots[slotIndex];
            }
            else
            {
                // Первый подходящий свободный слот
                targetSlot = _slots.Find(s => s.CanInsert && s.CanAcceptStone(stone));
            }

            if (targetSlot == null || !targetSlot.InsertStone(stone)) return false;

            ChargerSlotState newState = targetSlot.State;
            _stateChangedPublisher.Publish(new ChargerStateChangedEvent(
                targetSlot.SlotIndex, ChargerSlotState.Empty, newState,
                (float)stone.CurrentQi, (float)stone.MaxQi));

            return true;
        }

        /// <summary>Извлечь камень из слота</summary>
        public QiStone RemoveStone(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Count) return null;

            QiStone stone = _slots[slotIndex].RemoveStone();
            if (stone != null)
            {
                _stateChangedPublisher.Publish(new ChargerStateChangedEvent(
                    slotIndex, ChargerSlotState.Active, ChargerSlotState.Empty, 0, 0));
            }
            return stone;
        }

        // === Тик (вызывается из ChargerModule) ===

        /// <summary>
        /// Обновление зарядника. Вызывается каждый кадр из ChargerModule.Tick().
        /// </summary>
        public void Tick()
        {
            float deltaTime = _timeService.DeltaTime;

            // Обновляем тепло
            _heat.DissipateHeat(deltaTime);

            // Если зарядник активен и работоспособен
            if (_mode == ChargerMode.On && IsOperational)
            {
                ProcessChargerOperation(deltaTime);
            }
        }

        // === Внутренняя обработка ===

        private void ProcessChargerOperation(float deltaTime)
        {
            // 1. Извлекаем Ци из камней
            float totalStoneRate = CalculateTotalStoneRate();

            if (totalStoneRate > 0 && !_buffer.IsFull)
            {
                long added = _buffer.AccumulateFromStones(totalStoneRate, deltaTime);

                if (added > 0)
                {
                    // Уменьшаем Ци в камнях пропорционально их вкладу
                    DepleteStones(added, totalStoneRate);

                    // CH-03: Тепло от накопления УБРАНО — legacy добавляет тепло
                    // только от ИСПОЛЬЗОВАНИЯ Ци (техники), не от накопления буфера.
                    // Накопление — естественный процесс, не вызывающий перегрев.
                }
            }

            // 2. Передаём Ци практику (если не в бою и есть проводимость)
            // CH-16: В бою НЕ работает — практик фокусируется на противнике (док. §4.5)
            if (!_heat.IsOverheated && !_heat.IsInCombat && !_buffer.IsEmpty)
            {
                float conductivity = _cachedConductivity; // EVT: кэш из QiChangedEvent
                long transferred = _buffer.TransferToPractitioner(conductivity, deltaTime);

                if (transferred > 0)
                {
                    // EVT: QiAddRequestEvent вместо прямого вызова IQiService.AddQi
                    _qiAddRequestPub.Publish(new QiAddRequestEvent(transferred, "Charger"));
                    // QiChangedEvent публикуется внутри QiService при обработке QiAddRequestEvent

                    // Запрос на внесение Ци в формацию (если формация в стадии приёма).
                    // FormationService подписан на FormationContributeQiRequestEvent и
                    // игнорирует запрос, если формация неактивна (None/Drawing).
                    // Зарядник — внешняя система, публикующая запрос (EVT-01).
                    _contributeQiRequestPub.Publish(
                        new FormationContributeQiRequestEvent("Charger", transferred));
                }
            }

            // 3. Проверяем пустые камни
            CheckDepletedStones();
        }

        /// <summary>Рассчитать суммарную скорость камней</summary>
        private float CalculateTotalStoneRate()
        {
            float total = 0f;
            foreach (var slot in _slots)
            {
                if (slot.HasStone)
                {
                    float stoneRate = slot.InsertedStone.GetEffectiveReleaseRate(_buffer.Conductivity);
                    stoneRate *= (1f + slot.AbsorptionBonus);
                    total += stoneRate;
                }
            }
            return total;
        }

        /// <summary>
        /// Уменьшить Ци в камнях пропорционально их вкладу.
        /// Без этого камни дают бесконечное Ци.
        /// </summary>
        private void DepleteStones(long totalAmount, float totalRate)
        {
            if (totalAmount <= 0 || totalRate <= 0) return;

            // Рассчитываем доли для каждого камня
            var shares = new List<long>();
            long distributed = 0;

            foreach (var slot in _slots)
            {
                if (slot.HasStone)
                {
                    float stoneRate = slot.InsertedStone.GetEffectiveReleaseRate(_buffer.Conductivity);
                    stoneRate *= (1f + slot.AbsorptionBonus);
                    float proportion = stoneRate / totalRate;
                    long share = (long)(totalAmount * proportion);
                    shares.Add(share);
                    distributed += share;
                }
                else
                {
                    shares.Add(0);
                }
            }

            // Распределяем остаток от целочисленного усечения
            // CH-17: Убрано условие shares[i] > 0 — при малом totalAmount все shares = 0,
            // и остаток никогда не распределялся, создавая бесконечные камни.
            // Legacy-код не имеет этого условия.
            long remainder = totalAmount - distributed;
            for (int i = 0; i < remainder && i < shares.Count; i++)
            {
                shares[i] += 1;
            }

            // Применяем истощение
            int shareIndex = 0;
            foreach (var slot in _slots)
            {
                if (slot.HasStone && shareIndex < shares.Count)
                {
                    slot.InsertedStone.ExtractQi(shares[shareIndex]);
                }
                shareIndex++;
            }
        }

        /// <summary>Проверить и обработать пустые камни</summary>
        private void CheckDepletedStones()
        {
            foreach (var slot in _slots)
            {
                // CH-02: HasStone возвращает false для пустых камней!
                // Правильная проверка: камень есть, но Ци в нём = 0
                if (slot.InsertedStone != null && slot.InsertedStone.IsEmpty)
                {
                    ChargerSlotState oldState = ChargerSlotState.Active; // Был активен до истощения
                    int slotIdx = slot.SlotIndex;
                    slot.RemoveStone();
                    _stateChangedPublisher.Publish(new ChargerStateChangedEvent(
                        slotIdx, oldState, ChargerSlotState.Depleted, 0, 0));
                }
            }
        }

        // === ISaveable ===

        /// <summary>
        /// Ключ сохранения для модуля зарядников.
        /// </summary>
        public string SaveKey => "charger";

        /// <summary>
        /// Сериализовать состояние зарядника.
        /// Сохраняем: режим (On/Off), состояние буфера (Qi),
        /// состояние тепла (уровень, перегрев, кулдаун, боевой режим),
        /// камни в слотах (качество, размер, стихия, текущее/максимальное Ци).
        /// </summary>
        public object CaptureState()
        {
            // Собираем данные по слотам с камнями
            var slotData = new ChargerSlotSaveData[_slots.Count];
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                var save = new ChargerSlotSaveData
                {
                    slotIndex = slot.SlotIndex,
                    hasStone = slot.InsertedStone != null
                };

                if (slot.InsertedStone != null)
                {
                    var stone = slot.InsertedStone;
                    save.stoneQuality = (int)stone.Quality;
                    save.stoneSize = (int)stone.Size;
                    save.stoneElement = (int)stone.Element;
                    save.stoneCurrentQi = stone.CurrentQi.ToString();
                    save.stoneMaxQi = stone.MaxQi.ToString();
                }

                slotData[i] = save;
            }

            var data = new ChargerSaveData
            {
                mode = (int)_mode,
                bufferQi = _buffer.CurrentQi.ToString(),
                heatLevel = _heat.HeatLevel,
                isOverheated = _heat.IsOverheated ? 1 : 0,
                cooldownTimer = _heat.CooldownRemaining,
                isInCombat = _heat.IsInCombat ? 1 : 0,
                slots = slotData
            };
            return data;
        }

        /// <summary>
        /// Десериализовать состояние зарядника.
        /// Восстанавливаем: режим, буфер, тепло, камни в слотах.
        /// Предполагается, что Configure() уже вызван (слоты созданы).
        /// </summary>
        public void RestoreState(object state)
        {
            if (state is not ChargerSaveData data || data == null) return;

            // Восстанавливаем режим
            _mode = (ChargerMode)data.mode;

            // Восстанавливаем буфер — добавляем сохранённое Ци
            // (Configure() уже сбросил буфер в 0)
            long bufferQiVal = long.Parse(data.bufferQi);
            if (bufferQiVal > 0)
            {
                _buffer.AddQi(bufferQiVal);
            }

            // Восстанавливаем тепло
            // ChargerHeat не имеет публичного API для прямого задания уровня тепла,
            // поэтому используем AddHeat для установки приближённого значения.
            // Перегрев и боевой режим восстанавливаем через внутренние методы.
            if (data.heatLevel > 0f)
            {
                _heat.AddHeat(data.heatLevel);
            }
            if (data.isInCombat == 1)
            {
                _heat.EnterCombat();
            }
            // Примечание: восстановление перегрева (cooldownTimer) невозможно через
            // публичный API ChargerHeat. При перегреве тепло будет рассеиваться
            // автоматически с следующего кадра.

            // Восстанавливаем камни в слотах
            if (data.slots != null)
            {
                foreach (var slotSave in data.slots)
                {
                    if (!slotSave.hasStone) continue;
                    if (slotSave.slotIndex < 0 || slotSave.slotIndex >= _slots.Count) continue;

                    // Создаём камень с нужными параметрами
                    var stone = new QiStone(
                        (QiStoneQuality)slotSave.stoneQuality,
                        (QiStoneSize)slotSave.stoneSize,
                        (Element)slotSave.stoneElement);

                    // Извлекаем разницу, чтобы получить сохранённое текущее Ци
                    // (конструктор создаёт камень с полным Ци)
                    long stoneCurrentQiVal = long.Parse(slotSave.stoneCurrentQi);
                    long diff = stone.MaxQi - stoneCurrentQiVal;
                    if (diff > 0)
                    {
                        stone.ExtractQi(diff);
                    }

                    // Вставляем камень в слот
                    _slots[slotSave.slotIndex].InsertStone(stone);
                }
            }
        }

        // === IDisposable ===

        /// <summary>
        /// Очистка подписок.
        /// VContainer автоматически вызовет Dispose при уничтожении LifetimeScope.
        /// </summary>
        public void Dispose()
        {
            _qiChangedSubscription?.Dispose();
            _qiChangedSubscription = null;
        }
    }

    // === Сериализуемые структуры для ISaveable ===

    /// <summary>
    /// Корневая структура сохранения модуля зарядников.
    /// JsonUtility требует [Serializable] для всех вложенных типов.
    /// </summary>
    [Serializable]
    public class ChargerSaveData
    {
        /// <summary>Режим зарядника (int-каст ChargerMode)</summary>
        public int mode;

        /// <summary>Текущее Ци в буфере</summary>
        public string bufferQi; // long как string — JsonUtility не поддерживает long

        /// <summary>Уровень тепла (0-1.0)</summary>
        public float heatLevel;

        /// <summary>Флаг перегрева (1 = да, 0 = нет)</summary>
        public int isOverheated;

        /// <summary>Оставшееся время кулдауна при перегреве</summary>
        public float cooldownTimer;

        /// <summary>Флаг боевого режима (1 = да, 0 = нет)</summary>
        public int isInCombat;

        /// <summary>Массив данных слотов</summary>
        public ChargerSlotSaveData[] slots;
    }

    /// <summary>
    /// Сохраняемое состояние одного слота зарядника.
    /// </summary>
    [Serializable]
    public class ChargerSlotSaveData
    {
        /// <summary>Индекс слота</summary>
        public int slotIndex;

        /// <summary>Флаг наличия камня (1 = да, 0 = нет)</summary>
        public bool hasStone;

        /// <summary>Качество камня (int-каст QiStoneQuality)</summary>
        public int stoneQuality;

        /// <summary>Размер камня (int-каст QiStoneSize)</summary>
        public int stoneSize;

        /// <summary>Стихия камня (int-каст Element)</summary>
        public int stoneElement;

        /// <summary>Текущее Ци камня</summary>
        public string stoneCurrentQi; // long как string — JsonUtility не поддерживает long

        /// <summary>Максимальное Ци камня</summary>
        public string stoneMaxQi; // long как string — JsonUtility не поддерживает long
    }
}
