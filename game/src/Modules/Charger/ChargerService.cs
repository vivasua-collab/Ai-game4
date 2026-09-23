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

namespace CultivationGame.Modules.Charger
{
    /// <summary>
    /// Сервис зарядников Ци.
    /// Управляет слотами камней, буфером, тепловым балансом.
    /// Зависимости: ITimeService — через конструктор (stub в Core).
    /// Кросс-модульные вызовы (Qi) — через MessagePipe события.
    /// </summary>
    // R17 (аудит-0911 CH-2): + IWorldResettable — Configure() вызывается один
    // раз за процесс (guard GameEntryPoint), а RestoreState мержил поверх
    // живого состояния: warm-load занимал слоты камнями прошлой сессии
    // (InsertStone тихо false — камни из сейва молча терялись), буфер
    // накапливался поверх, перегрев оставался. ResetLiveState() — единая
    // точка сброса (начала RestoreState + пересборка мира).
    public class ChargerService : IChargerService, ISaveable, IWorldResettable, IDisposable
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

        /// <summary>
        /// R37-c: immutable-снимок слота для UI/моста (ChargerItemBridge,
        /// ChargerWindow): камень + остатки Ци без мутации домена.
        /// </summary>
        public ChargerSlotSnapshot GetSlotSnapshot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Count)
                return new ChargerSlotSnapshot(slotIndex, false, 0, 0, null);
            var slot = _slots[slotIndex];
            var stone = slot.InsertedStone;
            return new ChargerSlotSnapshot(slotIndex, stone != null,
                stone?.CurrentQi ?? 0, stone?.MaxQi ?? 0, stone);
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

            // AUDIT-0921 B2 (P1): кламп запроса по СВОБОДНОМУ МЕСТУ буфера ДО
            // извлечения. Прежде: ExtractQi(50) при свободных 20 → камень −50,
            // буфер +20, 30 Qi исчезало (реальная потеря ресурса). Теперь
            // извлекается ровно столько, сколько поместится (худший случай
            // — остаток камня): камень и буфер в балансе.
            long freeSpace = _buffer.Capacity - _buffer.CurrentQi;
            if (freeSpace <= 0) return false;
            long want = Math.Min((long)qiAmount, freeSpace);
            if (want <= 0) return false;

            long extracted = slot.InsertedStone.ExtractQi(want);
            if (extracted <= 0) return false;

            // Ци из камня попадает в буфер зарядника (want ≤ freeSpace → без потерь)
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
            // AUDIT-0921 B3 (P1/P0): ИЗВЛЕЧЕНИЕ-ПЕРВЫМ. Прежде: буфер получал
            // Ци по СКОРОСТИ (AccumulateFromStones без знания об остатках
            // камней), затем DepleteStones снимал пропорциональные доли с
            // молчаливым клампом по остатку → при полупустых камнях буфер
            // получал БОЛЬШЕ, чем отдавали камни (Ци из воздуха — нарушение
            // экономики ресурса). Теперь: Peek (желаемое, кламп по свободному
            // месту) → ExtractFromStonesCapped (клампы по остаткам + перенос
            // дефицита на камни с запасом) → CommitAccumulation (буфер получает
            // только подтверждённое). Инвариант: stones −N ⇔ buffer +N.
            float totalStoneRate = CalculateTotalStoneRate();

            if (totalStoneRate > 0 && !_buffer.IsFull)
            {
                long desired = _buffer.PeekAccumulation(totalStoneRate, deltaTime);
                if (desired > 0)
                {
                    long extracted = ExtractFromStonesCapped(desired, totalStoneRate);
                    _buffer.CommitAccumulation(desired, extracted);
                    // extracted == 0 (камни пусты в этом кадре) — intent всё равно
                    // списан: rate-обещание не банкируется (см. CommitAccumulation).
                }

                // CH-03: Тепло от накопления УБРАНО — legacy добавляет тепло
                // только от ИСПОЛЬЗОВАНИЯ Ци (техники), не от накопления буфера.
                // Накопление — естественный процесс, не вызывающий перегрев.
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
        /// AUDIT-0921 B3: извлечь до <paramref name="requested"/> Ци из камней
        /// с УЧЁТОМ фактических остатков. Доли — по вкладу скорости; кламп
        /// по CurrentQi каждого камня; дефицит (камень не смог отдать долю)
        /// ПЕРЕНОСИТСЯ камням с остатком (проходы до исчерпания запроса или
        /// камней). Возвращает подтверждённую сумму ≤ requested.
        /// Замена DepleteStones: прежний метод снимал доли с молчаливым
        /// клампом — разница «запрошено/снято» материализовалась в буфере
        /// из воздуха. Наследует CH-17 (распределение остатка усечения).
        /// </summary>
        private long ExtractFromStonesCapped(long requested, float totalRate)
        {
            if (requested <= 0 || totalRate <= 0) return 0;

            // Активные камни с их скоростями (параллельно долям скорости).
            var stones = new List<(ChargerSlot slot, float rate)>();
            foreach (var slot in _slots)
            {
                if (slot.HasStone)
                {
                    float stoneRate = slot.InsertedStone.GetEffectiveReleaseRate(_buffer.Conductivity);
                    stoneRate *= (1f + slot.AbsorptionBonus);
                    if (stoneRate > 0f)
                        stones.Add((slot, stoneRate));
                }
            }
            if (stones.Count == 0) return 0;

            long extractedTotal = 0;
            long remaining = requested;

            // Проходы: в каждом снимаем пропорциональные доли, клампим по
            // остаткам; дефицит (бюджет не выбран) переносится на следующий
            // проход к камням, у которых ещё есть Ци.
            while (remaining > 0)
            {
                float passRate = 0f;
                foreach (var (slot, rate) in stones)
                    if (slot.InsertedStone.CurrentQi > 0)
                        passRate += rate;
                if (passRate <= 0f) break; // все камни пусты

                long budget = remaining;
                long passExtracted = 0;

                foreach (var (slot, rate) in stones)
                {
                    if (budget <= 0) break;
                    long avail = slot.InsertedStone.CurrentQi;
                    if (avail <= 0) continue;

                    long share = (long)(budget * (rate / passRate));
                    if (share <= 0) share = 1; // CH-17-наследник: минимальный прогресс
                    if (share > budget) share = budget;
                    if (share > avail) share = avail; // кламп по остатку камня

                    long got = slot.InsertedStone.ExtractQi(share);
                    passExtracted += got;
                    budget -= got;
                }

                if (passExtracted <= 0) break; // прогресса нет — выходим
                extractedTotal += passExtracted;
                remaining -= passExtracted;
            }

            return extractedTotal;
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

        /// <summary>R11 P0-Save (review): тип state-блока для persistence round-trip.</summary>
        public Type StateType => typeof(ChargerSaveData);

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
        /// R17 (аудит-0911 CH-2): ПЕРВЫМ делом — сброс живого состояния
        /// (слоты/буфер/тепло/режим): Configure() зовётся один раз за процесс,
        /// warm-load мержил сейв поверх прошлой сессии (двойное зачисление
        /// буфера, занятые слоты → камни из сейва молча потеряны).
        /// </summary>
        public void RestoreState(object state)
        {
            if (state is not ChargerSaveData data || data == null) return;

            // R17 (CH-2): сброс ДО восстановления — сейв становится
            // единственным источником истины (а не «накладка» поверх живого).
            ResetLiveState();

            // Восстанавливаем режим
            _mode = (ChargerMode)data.mode;

            // Восстанавливаем буфер — добавляем сохранённое Ци
            // (Configure() уже сбросил буфер в 0)
            long bufferQiVal = long.TryParse(data.bufferQi, out var parsedBuffer) ? parsedBuffer : 0;
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

                    // Создаём камень с нужными параметрами.
                    // R37-c: maxQi — из сейва (stoneMaxQi), НЕ доменный дефолт:
                    // камни, вставленные мостом из предметов (QiStoneData,
                    // канон 1024×см³), имеют собственный объём — прежде
                    // reload урезал их до BaseQi(размер)×mult с потерей Ци.
                    long stoneMaxQiVal = long.TryParse(slotSave.stoneMaxQi, out var parsedMax) ? parsedMax : 0;
                    long stoneCurrentQiVal = long.TryParse(slotSave.stoneCurrentQi, out var parsedStone) ? parsedStone : 0;

                    var stone = new QiStone(
                        (QiStoneQuality)slotSave.stoneQuality,
                        (QiStoneSize)slotSave.stoneSize,
                        (Element)slotSave.stoneElement,
                        stoneMaxQiVal,
                        stoneCurrentQiVal);
                    _ = stone.ExtractQi(Math.Max(0, stone.MaxQi - stone.CurrentQi)); // страховка клампа

                    // Вставляем камень в слот
                    _slots[slotSave.slotIndex].InsertStone(stone);
                }
            }
        }

        /// <summary>
        /// R17 (аудит-0911 CH-2): полный сброс живого состояния зарядника —
        /// слоты пусты, буфер разряжен, тепло сброшено, режим Off.
        /// Вызывается в начале RestoreState (сейв — единственный источник
        /// истины) и из ResetWorld (пересборка мира). Идемпотентен.
        /// </summary>
        private void ResetLiveState()
        {
            // Слоты: извлечь камни через публичный API (честные события
            // ChargerStateChangedEvent → UI-панель обновится).
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].InsertedStone != null)
                    RemoveStone(i);
            }

            // Буфер: разрядить в ноль (событие ChargerBufferChangedEvent).
            if (_buffer.CurrentQi > 0)
                _buffer.ExtractQi(_buffer.CurrentQi);

            // Тепло/перегрев/кулдаун/бой — в «холодное» состояние.
            _heat.Reset();

            // Режим — Off (RestoreState выставит из сейва).
            _mode = ChargerMode.Off;
        }

        // R17 (E-1): IWorldResettable — пересборка мира = пустой холодный
        // зарядник (Configure() одноразовый за процесс, без сброса камни/
        // буфер/тепло прошлого мира переживали бы NewGame #2).
        public void ResetWorld() => ResetLiveState();

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
