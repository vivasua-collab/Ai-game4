# 🔋 Концепция Зарядника (Qi Charger)

**Версия:** 1.0  
**Создано:** 2026-02-28  
**Статус:** Черновик

---

## 📋 Обзор

**Зарядник** — специальный тип экипировки, предназначенный для:
1. **Хранения камней Ци** в упорядоченном виде
2. **Контролируемого поглощения** Ци из камней
3. **Буферизации Ци** для быстрого использования в бою

### Ключевая концепция

```
┌─────────────────────────────────────────────────────────────────────────┐
│                     ПРИНЦИП РАБОТЫ ЗАРЯДНИКА                            │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│    ┌─────────────┐      ┌─────────────┐      ┌─────────────┐            │
│    │  КАМЕНЬ Ци  │ ──→  │  ЗАРЯДНИК  │ ──→  │  ПРАКТИК   │            │
│    │  (источник) │      │  (буфер)    │      │  (приёмник) │            │
│    └─────────────┘      └─────────────┘      └─────────────┘            │
│          │                    │                    │                    │
│          │    скорость        │   проводимость    │                    │
│          │    высвобождения   │   зарядника       │                    │
│          │         ↓          │        ↓          │                    │
│          └──── 50-200 ед/сек ─┴── 5-50 ед/сек ────┘                    │
│                               ↓                                          │
│                        ограничивающий                                    │
│                          фактор                                          │
│                                                                          │
│   "Нельзя вылить бассейн за секунды через трубочку для коктейлей"       │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 1️⃣ ТИПЫ ЗАРЯДНИКОВ

### 1.1 По форм-фактору

```typescript
type ChargerFormFactor = 
  | 'belt'       // Поясной (носят на поясе)
  | 'bracelet'   // Браслет (на запястье)
  | 'necklace'   // Ожерелье (на шее)
  | 'ring'       // Кольцо (на пальце)
  | 'backpack';  // Ранец (на спине)

interface ChargerFormFactorData {
  name: string;
  slot: EquipmentSlot;          // Слот экипировки
  baseSlotCount: number;        // Базовое количество слотов
  maxSlotCount: number;         // Макс. слотов (с улучшениями)
  baseCapacity: number;         // Базовая ёмкость буфера (ед Ци)
  description: string;
  wearPosition: string;
}

const CHARGER_FORM_FACTORS: Record<ChargerFormFactor, ChargerFormFactorData> = {
  belt: {
    name: 'Пояс-накопитель',
    slot: 'belt',
    baseSlotCount: 3,
    maxSlotCount: 8,
    baseCapacity: 500,
    description: 'Широкий пояс с гнёздами для камней. Удобен и незаметен.',
    wearPosition: 'Талия',
  },
  bracelet: {
    name: 'Браслет-накопитель',
    slot: 'bracelet',
    baseSlotCount: 2,
    maxSlotCount: 4,
    baseCapacity: 200,
    description: 'Наручный браслет с камнями. Быстрый доступ.',
    wearPosition: 'Запястье',
  },
  necklace: {
    name: 'Ожерелье-накопитель',
    slot: 'necklace',
    baseSlotCount: 1,
    maxSlotCount: 3,
    baseCapacity: 1000,
    description: 'Кулон с камнем у сердца. Максимальная ёмкость.',
    wearPosition: 'Шея',
  },
  ring: {
    name: 'Кольцо-накопитель',
    slot: 'ring_right',
    baseSlotCount: 1,
    maxSlotCount: 1,
    baseCapacity: 50,
    description: 'Кольцо с миниатюрным камнем. Минималистично.',
    wearPosition: 'Палец',
  },
  backpack: {
    name: 'Ранец-накопитель',
    slot: 'backpack',
    baseSlotCount: 6,
    maxSlotCount: 15,
    baseCapacity: 2000,
    description: 'Рюкзак с множеством гнёзд. Максимальная функциональность.',
    wearPosition: 'Спина',
  },
};
```

### 1.2 По назначению

```typescript
type ChargerPurpose = 
  | 'accumulation'  // Для медитации (медленное поглощение)
  | 'combat'        // Для боя (быстрое поглощение)
  | 'hybrid';       // Универсальный

interface ChargerPurposeData {
  name: string;
  description: string;
  
  // Модификаторы
  absorptionSpeed: number;     // Множитель скорости поглощения
  bufferSize: number;          // Множитель размера буфера
  combatEfficiency: number;    // Эффективность в бою (%)
  
  // Особенности
  features: string[];
}

const CHARGER_PURPOSES: Record<ChargerPurpose, ChargerPurposeData> = {
  accumulation: {
    name: 'Медитационный',
    description: 'Оптимизирован для длительного поглощения Ци во время медитации.',
    absorptionSpeed: 0.8,       // Медленнее, но эффективнее
    bufferSize: 1.5,            // Больший буфер
    combatEfficiency: 50,       // Неудобен в бою
    features: ['Высокая эффективность', 'Больший буфер', 'Сниженный нагрев'],
  },
  combat: {
    name: 'Боевой',
    description: 'Оптимизирован для быстрого доступа к Ци в бою.',
    absorptionSpeed: 1.5,       // Быстрее
    bufferSize: 0.7,            // Меньший буфер (компактность)
    combatEfficiency: 100,
    features: ['Быстрый сброс Ци', 'Защита от повреждений', 'Автоинъекция'],
  },
  hybrid: {
    name: 'Универсальный',
    description: 'Сбалансированный вариант для всех ситуаций.',
    absorptionSpeed: 1.0,
    bufferSize: 1.0,
    combatEfficiency: 75,
    features: ['Баланс', 'Адаптивность'],
  },
};
```

---

## 2️⃣ ТЕХНИЧЕСКИЕ ХАРАКТЕРИСТИКИ

### 2.1 Слоты для камней

```typescript
interface QiStoneSlot {
  id: string;                   // ID слота
  index: number;                // Номер слота
  
  // Совместимость
  compatibility: {
    minQuality: QiStoneQuality; // Минимальное качество камня
    maxSize: QiStoneSize;       // Максимальный размер камня
    types: QiStoneType[];       // Допустимые типы камней
  };
  
  // Текущий камень
  currentStone: QiStone | null;
  
  // Состояние
  state: {
    isActive: boolean;          // Активен для поглощения
    isSealed: boolean;          // Запечатан
    damageLevel: number;        // Уровень повреждения (0-100%)
  };
  
  // Модификаторы
  modifiers?: {
    absorptionBonus: number;    // % бонус к поглощению
    qiRetention: number;        // % сохранения Ци
  };
}
```

### 2.2 Буфер Ци

```typescript
interface QiBuffer {
  // === ЁМКОСТЬ ===
  capacity: {
    base: number;               // Базовая ёмкость (от типа зарядника)
    current: number;            // Текущая ёмкость (с учётом улучшений)
    max: number;                // Максимальная ёмкость
  };
  
  // === СОДЕРЖИМОЕ ===
  content: {
    currentQi: number;          // Текущее количество Ци
    maxQi: number;              // Максимум (может превышать capacity при зарядке)
    overchargePossible: boolean;// Можно ли перезарядить сверх лимита
    overchargePenalty: number;  // Штраф за перезарядку (%/сек утечки)
  };
  
  // === ПАРАМЕТРЫ ПОТОКА ===
  flow: {
    // Скорость входящего потока (от камней)
    inputRate: number;          // ед/сек
    
    // Скорость исходящего потока (к практику)
    outputRate: number;         // ед/сек
    
    // Проводимость зарядника
    conductivity: number;       // ед/сек (ограничивающий фактор!)
  };
  
  // === СОСТОЯНИЕ ===
  state: {
    temperature: number;        // Температура (0-100%)
    stability: number;          // Стабильность (0-100%)
    lastActivity: Date;         // Последняя активность
  };
}
```

### 2.3 Проводимость зарядника

```typescript
// Ключевой параметр - ограничивает скорость передачи Ци

interface ChargerConductivity {
  // === БАЗОВАЯ ПРОВОДИМОСТЬ ===
  base: number;                 // ед/сек (зависит от материала и качества)
  
  // === МОДИФИКАТОРЫ ===
  modifiers: {
    material: number;           // Множитель от материала
    quality: number;            // Множитель от качества
    enhancement: number;        // Множитель от улучшений
  };
  
  // === ИТОГОВАЯ ===
  effective: number;            // ед/сек
  
  // === СРАВНЕНИЕ С МЕРИДИАНАМИ ===
  // Проводимость зарядника обычно НИЖЕ проводимости меридиан
  // Это делает зарядник "узким горлышком"
  
  // Формула итоговой скорости:
  // effectiveRate = min(
  //   stone.releaseRate,         // Камень может выдать
  //   charger.conductivity,       // Зарядник может передать
  //   practitioner.conductivity   // Практик может принять
  // )
}

// Примеры проводимости:
const CHARGER_CONDUCTIVITY_EXAMPLES = {
  // Базовый пояс (common)
  basic_belt: {
    base: 5.0,                  // 5 ед/сек = 18 000 ед/час
    effective: 5.0,
    note: 'Для практика с проводимостью 2.0: скорость = 2.0 (ограничение практика)',
  },
  
  // Ремесленный пояс (uncommon)
  crafted_belt: {
    base: 10.0,                 // 10 ед/сек = 36 000 ед/час
    effective: 10.0,
    note: 'Достаточно для практика с проводимостью до 10.0',
  },
  
  // Мастерский пояс (rare)
  master_belt: {
    base: 25.0,                 // 25 ед/сек = 90 000 ед/час
    effective: 25.0,
    note: 'Для продвинутых практиков',
  },
  
  // Легендарный пояс (legendary)
  legendary_belt: {
    base: 50.0,                 // 50 ед/сек = 180 000 ед/час
    effective: 50.0,
    note: 'Не ограничивает даже мастеров 9 уровня',
  },
};
```

---

## 3️⃣ МЕХАНИКА РАБОТЫ

### 3.1 Поглощение из камней

```typescript
interface AbsorptionProcess {
  // === ИСТОЧНИКИ (камни в слотах) ===
  sources: {
    stone: QiStone;
    slot: QiStoneSlot;
    availableQi: number;
    releaseRate: number;        // Скорость высвобождения из камня
  }[];
  
  // === ПРОМЕЖУТОЧНЫЙ БУФЕР ===
  buffer: QiBuffer;
  
  // === ПРИЁМНИК (практик) ===
  receiver: {
    conductivity: number;       // Проводимость меридиан
    currentQi: number;
    coreCapacity: number;
    freeSpace: number;
  };
  
  // === РАСЧЁТ СКОРОСТИ ===
  calculation: {
    totalSourceRate: number;    // Суммарная скорость всех камней
    bufferConductivity: number; // Проводимость зарядника
    receiverConductivity: number; // Проводимость практика
    effectiveRate: number;      // Итоговая скорость = min(all three)
    bottleneck: string;         // 'stones' | 'charger' | 'practitioner'
  };
}

function calculateAbsorptionProcess(
  charger: QiCharger,
  practitioner: Character
): AbsorptionProcess {
  // 1. Суммарная скорость камней
  const totalSourceRate = charger.slots
    .filter(s => s.currentStone && s.state.isActive)
    .reduce((sum, slot) => sum + slot.currentStone!.properties.releaseRate, 0);
  
  // 2. Проводимость зарядника
  const bufferConductivity = charger.buffer.flow.conductivity;
  
  // 3. Проводимость практика
  const receiverConductivity = practitioner.conductivity;
  
  // 4. Итоговая скорость = минимум из трёх
  const effectiveRate = Math.min(
    totalSourceRate,
    bufferConductivity,
    receiverConductivity
  );
  
  // 5. Определение узкого места
  let bottleneck: string;
  if (receiverConductivity <= totalSourceRate && receiverConductivity <= bufferConductivity) {
    bottleneck = 'practitioner'; // "Трубочка для коктейлей" - меридианы
  } else if (bufferConductivity <= totalSourceRate) {
    bottleneck = 'charger';      // Зарядник ограничивает
  } else {
    bottleneck = 'stones';       // Камни не успевают
  }
  
  return {
    sources: charger.slots
      .filter(s => s.currentStone && s.state.isActive)
      .map(slot => ({
        stone: slot.currentStone!,
        slot,
        availableQi: slot.currentStone!.qiContent.current,
        releaseRate: slot.currentStone!.properties.releaseRate,
      })),
    buffer: charger.buffer,
    receiver: {
      conductivity: practitioner.conductivity,
      currentQi: practitioner.currentQi,
      coreCapacity: practitioner.coreCapacity,
      freeSpace: practitioner.coreCapacity - practitioner.currentQi,
    },
    calculation: {
      totalSourceRate,
      bufferConductivity,
      receiverConductivity,
      effectiveRate,
      bottleneck,
    },
  };
}
```

### 3.2 Режимы работы

```typescript
type ChargerMode = 
  | 'off'            // Выключен (камни не расходуются)
  | 'trickle'        // Капельный (медленное поглощение)
  | 'normal'         // Нормальный
  | 'burst'          // Всплеск (максимальная скорость)
  | 'combat';        // Боевой (оптимизирован для боя)

interface ChargerModeData {
  name: string;
  description: string;
  
  // Параметры
  speedMultiplier: number;      // Множитель скорости
  efficiencyLoss: number;       // Потеря эффективности (%)
  heatGeneration: number;       // Генерация тепла (%/сек)
  
  // Условия
  requirements: {
    minBufferCharge: number;    // Мин. заряд буфера (%)
    cooldownAfter?: number;     // Кулдаун после режима (сек)
  };
  
  // Эффекты
  sideEffects: string[];
}

const CHARGER_MODES: Record<ChargerMode, ChargerModeData> = {
  off: {
    name: 'Выключен',
    description: 'Зарядник неактивен, камни сохраняются.',
    speedMultiplier: 0,
    efficiencyLoss: 0,
    heatGeneration: 0,
    requirements: { minBufferCharge: 0 },
    sideEffects: ['Полное сохранение камней'],
  },
  trickle: {
    name: 'Капельный',
    description: 'Очень медленное поглощение, минимальные потери.',
    speedMultiplier: 0.25,
    efficiencyLoss: 2,
    heatGeneration: 0.1,
    requirements: { minBufferCharge: 0 },
    sideEffects: ['Минимальный нагрев', '98% эффективность'],
  },
  normal: {
    name: 'Нормальный',
    description: 'Стандартный режим поглощения.',
    speedMultiplier: 1.0,
    efficiencyLoss: 5,
    heatGeneration: 0.5,
    requirements: { minBufferCharge: 0 },
    sideEffects: ['Сбалансированный режим'],
  },
  burst: {
    name: 'Всплеск',
    description: 'Максимальная скорость, повышенные потери.',
    speedMultiplier: 2.0,
    efficiencyLoss: 15,
    heatGeneration: 2.0,
    requirements: { minBufferCharge: 0, cooldownAfter: 60 },
    sideEffects: ['Быстрое поглощение', 'Повышенный износ', 'Требует охлаждения'],
  },
  combat: {
    name: 'Боевой',
    description: 'Оптимизация для боя: быстрый сброс, защита камней.',
    speedMultiplier: 1.5,
    efficiencyLoss: 10,
    heatGeneration: 1.0,
    requirements: { minBufferCharge: 20 },
    sideEffects: ['Приоритет боевых функций', 'Защита камней'],
  },
};
```

---

## 4️⃣ ИСПОЛЬЗОВАНИЕ В БОЮ

### 4.1 Концепция "батарейки"

```typescript
interface CombatQiInjection {
  // === ИСТОЧНИК ===
  source: {
    charger: QiCharger;
    availableQi: number;        // Ци в буфере зарядника
  };
  
  // === ЦЕЛЬ ===
  target: {
    practitioner: Character;
    currentQi: number;
    coreCapacity: number;
  };
  
  // === ПАРАМЕТРЫ ИНЪЕКЦИИ ===
  injection: {
    amount: number;             // Количество Ци
    rate: number;               // Скорость (ед/сек)
    duration: number;           // Длительность (сек)
  };
  
  // === РЕЗУЛЬТАТ ===
  result: {
    success: boolean;
    qiTransferred: number;
    qiLost: number;             // Потери при передаче
    heatGenerated: number;
    cooldownRequired: number;   // Кулдаун (сек)
  };
}

// Сценарии использования в бою:

// 1. Экстренная подзарядка
const emergencyInjection: CombatQiInjection = {
  source: { charger, availableQi: 500 },
  target: { practitioner, currentQi: 100, coreCapacity: 1000 },
  injection: { amount: 500, rate: 50, duration: 10 },
  // Результат: +450 Ци за 10 секунд (10% потери)
};

// 2. Поддержка техники
const techniqueSupport: CombatQiInjection = {
  source: { charger, availableQi: 100 },
  target: { practitioner, currentQi: 50, coreCapacity: 1000 },
  injection: { amount: 50, rate: 25, duration: 2 },
  // Результат: +45 Ци за 2 секунды (доп. Ци для техники)
};

// 3. Восстановление после боя
const postCombatRecovery: CombatQiInjection = {
  source: { charger, availableQi: 2000 },
  target: { practitioner, currentQi: 0, coreCapacity: 1000 },
  injection: { amount: 1000, rate: 10, duration: 100 },
  // Результат: +950 Ци за ~1.7 минуты
};
```

### 4.2 Баланс в бою

```typescript
interface CombatBalance {
  // === ОГРАНИЧЕНИЯ ===
  limitations: {
    // Нельзя мгновенно восстановить всё Ци
    maxInjectionRate: number;   // Ограничение скорости = проводимость зарядника
    
    // Перегрев при интенсивном использовании
    maxTemperature: 100;        // % - при достижении блокируется
    
    // Кулдаун между инъекциями
    minInjectionInterval: 5;    // сек
    
    // Потери Ци при быстрой передаче
    combatEfficiency: 0.85;     // 15% потерь в бою
  };
  
  // === ТАКТИЧЕСКОЕ ПРИМЕНЕНИЕ ===
  tactics: {
    // Зарядник - не бесконечная батарейка
    // Камни Ци - ограниченный ресурс
    
    // Рациональное использование:
    // 1. Экстренная подзарядка при <20% Ци
    // 2. Поддержка мощной техники
    // 3. Восстановление между боями
    
    // Неэффективно:
    // 1. Постоянная подпитка (быстрый расход камней)
    // 2. Использование при полном ядре
    // 3. Частые мелкие инъекции (потери на "включение")
  };
}
```

### 4.3 Интеграция с боевой системой

```typescript
// Использование Ци из зарядника для техник

function useTechniqueWithCharger(
  technique: Technique,
  practitioner: Character,
  charger: QiCharger | null,
  target?: Entity
): TechniqueResult {
  // 1. Расчёт стоимости техники
  const qiCost = technique.qiCost;
  
  // 2. Определение источника Ци
  let qiSource: 'core' | 'charger' | 'mixed';
  let qiFromCore = 0;
  let qiFromCharger = 0;
  
  if (!charger || charger.buffer.content.currentQi < qiCost * 0.2) {
    // Только из ядра
    qiSource = 'core';
    qiFromCore = qiCost;
  } else if (practitioner.currentQi >= qiCost) {
    // Из ядра (предпочтительнее - эффективнее)
    qiSource = 'core';
    qiFromCore = qiCost;
  } else {
    // Смешанный источник
    qiSource = 'mixed';
    qiFromCore = practitioner.currentQi;
    qiFromCharger = qiCost - practitioner.currentQi;
  }
  
  // 3. Проверка возможности
  if (qiFromCore > practitioner.currentQi) {
    return { success: false, error: 'Недостаточно Ци' };
  }
  if (qiFromCharger > (charger?.buffer.content.currentQi || 0)) {
    return { success: false, error: 'Недостаточно Ци в заряднике' };
  }
  
  // 4. Потери при использовании зарядника
  const chargerLoss = qiFromCharger * 0.15; // 15% потерь
  
  // 5. Выполнение техники
  const result = executeTechnique(technique, practitioner, target);
  
  // 6. Списание Ци
  practitioner.currentQi -= qiFromCore;
  if (charger && qiFromCharger > 0) {
    charger.buffer.content.currentQi -= (qiFromCharger + chargerLoss);
    charger.buffer.state.temperature += qiFromCharger * 0.01; // Нагрев
  }
  
  return {
    ...result,
    qiUsed: { fromCore: qiFromCore, fromCharger: qiFromCharger },
    qiLost: chargerLoss,
  };
}
```

---

## 5️⃣ ИСПОЛЬЗОВАНИЕ ДЛЯ МЕДИТАЦИИ

### 5.1 Ускорение медитации

```typescript
interface MeditationWithCharger {
  // === БАЗОВАЯ МЕДИТАЦИЯ ===
  baseMeditation: {
    qiFromEnvironment: number;  // Ци из среды (поглощение меридианами)
    qiFromMicroCore: number;    // Ци от микро-ядра
    totalBaseRate: number;      // Скорость базового накопления
  };
  
  // === ДОПОЛНИТЕЛЬНО ОТ ЗАРЯДНИКА ===
  chargerBoost: {
    qiFromCharger: number;      // Ци из зарядника
    chargerRate: number;        // Скорость из зарядника
    efficiency: number;         // Эффективность (учёт потерь)
  };
  
  // === ИТОГО ===
  total: {
    combinedRate: number;       // Суммарная скорость
    speedupFactor: number;      // Во сколько раз быстрее
    timeToFullCore: number;     // Время до полного ядра
  };
}

function calculateMeditationWithCharger(
  practitioner: Character,
  charger: QiCharger | null,
  location: LocationData
): MeditationWithCharger {
  // Базовая скорость
  const baseRate = calculateQiRates(practitioner, location).total;
  
  // Скорость от зарядника
  let chargerRate = 0;
  let chargerEfficiency = 0;
  
  if (charger && charger.buffer.content.currentQi > 0) {
    // В режиме медитации используется "капельный" режим для эффективности
    const process = calculateAbsorptionProcess(charger, practitioner);
    chargerRate = process.calculation.effectiveRate * CHARGER_MODES.trickle.speedMultiplier;
    chargerEfficiency = (100 - CHARGER_MODES.trickle.efficiencyLoss) / 100;
  }
  
  // Итоговая скорость
  const combinedRate = baseRate + (chargerRate * chargerEfficiency);
  const speedupFactor = combinedRate / baseRate;
  const timeToFullCore = (practitioner.coreCapacity - practitioner.currentQi) / combinedRate;
  
  return {
    baseMeditation: {
      qiFromEnvironment: baseRate * 0.7, // Условно 70% от базы = среда
      qiFromMicroCore: baseRate * 0.3,   // 30% = микроядро
      totalBaseRate: baseRate,
    },
    chargerBoost: {
      qiFromCharger: chargerRate * chargerEfficiency,
      chargerRate,
      efficiency: chargerEfficiency,
    },
    total: {
      combinedRate,
      speedupFactor: charger ? Math.max(1, speedupFactor) : 1,
      timeToFullCore,
    },
  };
}

// Пример:
// Базовая скорость: 0.5 ед/сек = 1800 ед/час
// Зарядник (проводимость 10 ед/сек, капельный режим 0.25): 2.5 ед/сек
// Итого: 0.5 + 2.5 * 0.98 = 2.95 ед/сек
// Ускорение: ~6x
```

### 5.2 Оптимизация процесса

```typescript
interface MeditationOptimization {
  // === РЕКОМЕНДАЦИИ ПО ИСПОЛЬЗОВАНИЮ ===
  recommendations: {
    // Когда использовать зарядник
    useChargerWhen: [
      'Ядро < 90% (выше - только базовое накопление)',
      'Срочная необходимость в Ци',
      'Низкая плотность Ци в месте медитации',
    ];
    
    // Когда НЕ использовать
    dontUseChargerWhen: [
      'Ядро > 95% (рациональнее базовое)',
      'Нет запаса камней',
      'Высокая температура зарядника',
    ];
    
    // Оптимальная стратегия
    optimalStrategy: [
      'Начать с капельного режима',
      'Переключить на нормальный при необходимости',
      'Оставить запас камней для боя',
    ];
  };
  
  // === РАСЧЁТ ОПТИМАЛЬНОГО РЕЖИМА ===
  function calculateOptimalMode(
    currentQi: number,
    coreCapacity: number,
    chargerCharge: number,
    availableStones: number
  ): ChargerMode {
    const fillPercent = currentQi / coreCapacity * 100;
    
    if (fillPercent < 50) {
      return 'burst';     // Быстрое восстановление
    } else if (fillPercent < 80) {
      return 'normal';    // Стандартное накопление
    } else if (fillPercent < 95) {
      return 'trickle';   // Осторожное дозаполнение
    } else {
      return 'off';       // Только базовое накопление
    }
  }
}
```

---

## 6️⃣ КОНСТРУКЦИЯ И МАТЕРИАЛЫ

### 6.1 Материалы

```typescript
type ChargerMaterial = 
  | 'iron'           // Железо (базовый)
  | 'spirit_iron'    // Духовное железо
  | 'jade'           // Нефрит
  | 'spirit_jade'    // Духовный нефрит
  | 'crystal'        // Кристалл
  | 'spirit_crystal' // Духовный кристалл
  | 'bone'           // Кость
  | 'dragon_bone';   // Кость дракона

interface ChargerMaterialData {
  name: string;
  
  // Характеристики
  properties: {
    baseConductivity: number;   // Базовая проводимость (ед/сек)
    maxConductivity: number;    // Макс. проводимость
    durability: number;         // Прочность
    qiRetention: number;        // Сохранение Ци (%/час)
    heatResistance: number;     // Термостойкость
  };
  
  // Совместимость с камнями
  compatibility: {
    minStoneQuality: QiStoneQuality;
    stoneTypes: QiStoneType[];
  };
  
  // Стоимость
  baseCost: number;            // Духовные камни
}

const CHARGER_MATERIALS: Record<ChargerMaterial, ChargerMaterialData> = {
  iron: {
    name: 'Железо',
    properties: {
      baseConductivity: 5,
      maxConductivity: 10,
      durability: 100,
      qiRetention: 95,          // 5% потерь в час
      heatResistance: 50,
    },
    compatibility: {
      minStoneQuality: 'raw',
      stoneTypes: ['calm'],
    },
    baseCost: 50,
  },
  spirit_iron: {
    name: 'Духовное железо',
    properties: {
      baseConductivity: 15,
      maxConductivity: 30,
      durability: 200,
      qiRetention: 98,
      heatResistance: 70,
    },
    compatibility: {
      minStoneQuality: 'rough',
      stoneTypes: ['calm'],
    },
    baseCost: 200,
  },
  jade: {
    name: 'Нефрит',
    properties: {
      baseConductivity: 10,
      maxConductivity: 25,
      durability: 150,
      qiRetention: 97,
      heatResistance: 60,
    },
    compatibility: {
      minStoneQuality: 'rough',
      stoneTypes: ['calm'],
    },
    baseCost: 150,
  },
  spirit_jade: {
    name: 'Духовный нефрит',
    properties: {
      baseConductivity: 25,
      maxConductivity: 50,
      durability: 300,
      qiRetention: 99,
      heatResistance: 80,
    },
    compatibility: {
      minStoneQuality: 'refined',
      stoneTypes: ['calm', 'chaotic'],
    },
    baseCost: 500,
  },
  dragon_bone: {
    name: 'Кость дракона',
    properties: {
      baseConductivity: 50,
      maxConductivity: 100,
      durability: 1000,
      qiRetention: 99.5,
      heatResistance: 95,
    },
    compatibility: {
      minStoneQuality: 'pure',
      stoneTypes: ['calm', 'chaotic'],
    },
    baseCost: 5000,
  },
  // ... другие материалы
};
```

### 6.2 Редкость и качество

```typescript
// Редкость зарядника = редкость материалов + качество изготовления

interface ChargerRarity {
  // От редкости зависят:
  // - Базовая проводимость
  // - Количество слотов
  // - Размер буфера
  // - Максимальный уровень улучшений
}

const CHARGER_RARITY_SCALING = {
  common: {
    conductivityMultiplier: 1.0,
    slotsBase: 1,
    bufferMultiplier: 1.0,
    maxEnhancement: 3,
  },
  uncommon: {
    conductivityMultiplier: 1.5,
    slotsBase: 2,
    bufferMultiplier: 1.5,
    maxEnhancement: 5,
  },
  rare: {
    conductivityMultiplier: 2.0,
    slotsBase: 3,
    bufferMultiplier: 2.0,
    maxEnhancement: 7,
  },
  legendary: {
    conductivityMultiplier: 3.0,
    slotsBase: 4,
    bufferMultiplier: 3.0,
    maxEnhancement: 9,
  },
  divine: {
    conductivityMultiplier: 5.0,
    slotsBase: 5,
    bufferMultiplier: 5.0,
    maxEnhancement: 12,
  },
};
```

---

## 7️⃣ ПОЛНАЯ СТРУКТУРА

### 7.1 Интерфейс

```typescript
interface QiCharger {
  // === ИДЕНТИФИКАЦИЯ ===
  id: string;
  name: string;
  description: string;
  
  // === ТИП ===
  formFactor: ChargerFormFactor;
  purpose: ChargerPurpose;
  
  // === РЕДКОСТЬ ===
  rarity: EquipmentRarity;
  quality: number;              // 1-10
  
  // === МАТЕРИАЛ ===
  material: ChargerMaterial;
  
  // === СЛОТЫ ===
  slots: QiStoneSlot[];
  activeSlots: number;          // Количество активных слотов
  
  // === БУФЕР ===
  buffer: QiBuffer;
  
  // === РЕЖИМ ===
  mode: ChargerMode;
  modeCooldown: number;         // Кулдаун смены режима
  
  // === ФИЗИЧЕСКИЕ ПАРАМЕТРЫ ===
  physical: {
    weight: number;             // кг
    durability: number;         // 0-100%
  };
  
  // === БОНУСЫ (как экипировка) ===
  bonuses?: EquipmentBonuses;
  
  // === ТРЕБОВАНИЯ ===
  requirements?: EquipmentRequirements;
  
  // === СТОИМОСТЬ ===
  baseValue: number;
}
```

### 7.2 Примеры зарядников

#### Базовый пояс-накопитель

```yaml
qi_charger:
  id: "basic_belt_charger"
  name: "Простой пояс-накопитель"
  description: "Железный пояс с тремя гнёздами для камней Ци."
  
formFactor: belt
purpose: hybrid

rarity: common
quality: 3

material: iron

slots:
  - id: "slot_1"
    index: 1
    compatibility:
      minQuality: raw
      maxSize: medium
      types: [calm]
    currentStone: null
    state: { isActive: true, isSealed: false, damageLevel: 0 }
  - id: "slot_2"
    index: 2
    compatibility:
      minQuality: raw
      maxSize: medium
      types: [calm]
    currentStone: null
    state: { isActive: true, isSealed: false, damageLevel: 0 }
  - id: "slot_3"
    index: 3
    compatibility:
      minQuality: raw
      maxSize: small
      types: [calm]
    currentStone: null
    state: { isActive: true, isSealed: false, damageLevel: 0 }

buffer:
  capacity:
    base: 500
    current: 500
    max: 500
  content:
    currentQi: 0
    maxQi: 500
    overchargePossible: false
  flow:
    inputRate: 15
    outputRate: 5
    conductivity: 5
  state:
    temperature: 0
    stability: 100

mode: normal

physical:
  weight: 2.0
  durability: 100

baseValue: 100
```

#### Мастерский боевой браслет

```yaml
qi_charger:
  id: "master_combat_bracelet"
  name: "Боевой браслет мастера"
  description: "Духовно-железный браслет для быстрых инъекций Ци в бою."
  
formFactor: bracelet
purpose: combat

rarity: rare
quality: 8

material: spirit_iron

slots:
  - id: "slot_1"
    index: 1
    compatibility:
      minQuality: refined
      maxSize: small
      types: [calm]
    currentStone: null
    modifiers:
      absorptionBonus: 10
      qiRetention: 98
  - id: "slot_2"
    index: 2
    compatibility:
      minQuality: refined
      maxSize: small
      types: [calm]
    currentStone: null
    modifiers:
      absorptionBonus: 10
      qiRetention: 98

buffer:
  capacity:
    base: 200
    current: 300
    max: 300
  content:
    currentQi: 0
    maxQi: 300
    overchargePossible: true
    overchargePenalty: 2
  flow:
    inputRate: 25
    outputRate: 20
    conductivity: 20
  state:
    temperature: 0
    stability: 100

mode: combat

bonuses:
  stats:
    - type: agility
      value: 2
      isPercent: false

physical:
  weight: 0.5
  durability: 100

requirements:
  cultivationLevel: { min: 3 }
  stats: { conductivity: 1.0 }

baseValue: 800
```

---

## 8️⃣ ИНТЕГРАЦИЯ С СИСТЕМОЙ

### 8.1 Prisma Schema

```prisma
model QiCharger {
  id          String   @id @default(cuid())
  createdAt   DateTime @default(now())
  updatedAt   DateTime @updatedAt
  
  // === ИДЕНТИФИКАЦИЯ ===
  name        String
  description String
  
  // === ТИП ===
  formFactor  String   // belt, bracelet, necklace, ring, backpack
  purpose     String   // accumulation, combat, hybrid
  
  // === РЕДКОСТЬ ===
  rarity      String
  quality     Int
  
  // === МАТЕРИАЛ ===
  material    String
  
  // === СЛОТЫ (JSON) ===
  slots       Json     // QiStoneSlot[]
  activeSlots Int
  
  // === БУФЕР (JSON) ===
  buffer      Json     // QiBuffer
  
  // === РЕЖИМ ===
  mode        String
  modeCooldown Int     @default(0)
  
  // === ФИЗИКА ===
  weight      Float
  durability  Float
  
  // === БОНУСЫ ===
  bonuses     Json?
  
  // === СВЯЗИ ===
  characterId String?
  character   Character? @relation(fields: [characterId], references: [id])
  
  // Камни в слотах
  stones      QiStone[]
  
  @@map("qi_chargers")
}
```

### 8.2 Связь с экипировкой

```typescript
// Зарядник - подтип экипировки

const chargerEquipment: Equipment = {
  equipmentType: 'wearable',
  subType: 'charger_belt', // или charger_bracelet, charger_necklace
  
  // ... стандартные поля экипировки
  
  // Специфичные для зарядника:
  chargerData: {
    slots: [...],
    buffer: {...},
    mode: 'normal',
  },
};
```

---

## 9️⃣ ПЛАН РЕАЛИЗАЦИИ

### Фаза 1: Базовая структура (Приоритет: Высокий)

1. **Типы и интерфейсы** — `src/types/charger.ts`
2. **Пресеты зарядников** — `src/data/presets/charger-presets.ts`
3. **Интеграция с камнями Ци**

### Фаза 2: Механики поглощения (Приоритет: Высокий)

1. **Расчёт скорости** — проводимость как ограничитель
2. **Режимы работы** — off/trickle/normal/burst/combat
3. **Буфер Ци** — накопление и выдача

### Фаза 3: Боевое применение (Приоритет: Средний)

1. **Инъекция Ци** — использование в бою
2. **Перегрев и кулдауны** — баланс
3. **Интеграция с техниками**

### Фаза 4: UI и крафт (Приоритет: Низкий)

1. **Интерфейс зарядника** — отображение слотов, буфера
2. **Управление камнями** — вставка/извлечение
3. **Крафт и улучшения**

---

## 🔗 Связанные документы

- [qi_stone.md](./qi_stone.md) — Концепция камней Ци
- [equip.md](./equip.md) — Система экипировки
- [body.md](./body.md) — Концепция тела (меридианы)
- [COMBAT_TECHNIQUES_SYSTEM.md](./COMBAT_TECHNIQUES_SYSTEM.md) — Боевая система

---

*Документ создан: 2026-02-28*  
*Версия: 1.0*
