# 💧 Система утечки Ци в формациях

**Дата:** 2026-03-22
**Версия:** 1.0
**Статус:** 📋 Проектирование

---

## 🎯 КЛЮЧЕВЫЕ ПРИНЦИПЫ

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                     ПРИНЦИПЫ УТЕЧКИ Ци                                       │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  1. Ци — ТОЛЬКО ЦЕЛЫЕ ЧИСЛА                                                 │
│     ───────────────────────────────────────────────────────────────────────  │
│     - Нет дробных значений Ци                                               │
│     - Утечка всегда = 1, 2, 3... Ци за раз                                  │
│     - Отображается только целое число                                       │
│                                                                              │
│  2. ПРИВЯЗКА К ТИКАМ                                                         │
│     ───────────────────────────────────────────────────────────────────────  │
│     - 1 тик = 1 минута игрового времени                                     │
│     - Утечка проверяется каждый тик                                         │
│     - Но происходит только через ИНТЕРВАЛ тиков                             │
│                                                                              │
│  3. ИНТЕРВАЛЬНАЯ СИСТЕМА                                                     │
│     ───────────────────────────────────────────────────────────────────────  │
│     - Каждые N тиков формация теряет M Ци                                   │
│     - N = интервал (зависит от уровня)                                      │
│     - M = количество за раз (зависит от размера)                            │
│                                                                              │
│  4. РАЗУМНЫЕ ВРЕМЕНА ЖИЗНИ                                                   │
│     ───────────────────────────────────────────────────────────────────────  │
│     - L1 Small: дни                                                         │
│     - L5 Great: месяцы/годы                                                 │
│     - L9 Heavy: годы/десятилетия                                            │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 📐 ФОРМУЛЫ

### Параметры утечки:

```typescript
// === ИНТЕРВАЛ УТЕЧКИ (в тиках) ===
// Каждые N тиков происходит потеря Ци
// Чем выше уровень — тем чаще утечка (формация сложнее)

const DRAIN_INTERVAL_BY_LEVEL: Record<number, number> = {
  1: 60,    // Каждые 60 тиков = каждый час
  2: 50,    // Каждые 50 минут
  3: 40,    // Каждые 40 минут
  4: 30,    // Каждые 30 минут
  5: 20,    // Каждые 20 минут
  6: 15,    // Каждые 15 минут
  7: 10,    // Каждые 10 минут
  8: 8,     // Каждые 8 минут
  9: 5,     // Каждые 5 минут
};

// === КОЛИЧЕСТВО Ци ЗА РАЗ ===
// Сколько Ци теряется за одну утечку
// Зависит от размера формации

const DRAIN_AMOUNT_BY_SIZE: Record<FormationSize, number> = {
  small: 1,     // 1 Ци за раз
  medium: 3,    // 3 Ци за раз
  large: 10,    // 10 Ци за раз
  great: 30,    // 30 Ци за раз
};

// Тяжёлые формации
const DRAIN_AMOUNT_HEAVY = 100;  // 100 Ци за раз
```

### Расчёт:

```typescript
interface FormationDrainParams {
  // Интервал утечки в тиках
  drainInterval: number;      // Каждые N тиков
  
  // Количество Ци за утечку
  drainAmount: number;        // M Ци за раз
  
  // Время между утечками в реальном времени
  drainIntervalMinutes: number;  // N минут
  
  // Утечка в час (игровое время)
  drainPerHour: number;       // (60 / N) × M
}

function calculateDrainParams(
  level: number,
  size: FormationSize,
  isHeavy: boolean = false
): FormationDrainParams {
  const drainInterval = DRAIN_INTERVAL_BY_LEVEL[level];
  const drainAmount = isHeavy 
    ? DRAIN_AMOUNT_HEAVY 
    : DRAIN_AMOUNT_BY_SIZE[size];
  
  // Утечка в час
  const drainsPerHour = 60 / drainInterval;
  const drainPerHour = Math.floor(drainsPerHour) * drainAmount;
  
  return {
    drainInterval,
    drainAmount,
    drainIntervalMinutes: drainInterval,
    drainPerHour,
  };
}

// Примеры:
// L1 Small: интервал 60, кол-во 1 → 1 Ци/час
// L5 Great: интервал 20, кол-во 30 → 90 Ци/час
// L9 Heavy: интервал 5, кол-во 100 → 1200 Ци/час
```

---

## 📊 ТАБЛИЦА УТЕЧЕК

### Утечка в час (игровое время):

| Уровень | Интервал (мин) | Small | Medium | Large | Great | Heavy |
|---------|----------------|-------|--------|-------|-------|-------|
| L1 | 60 | 1 | 3 | 10 | 30 | — |
| L2 | 50 | 1 | 3 | 12 | 36 | — |
| L3 | 40 | 2 | 5 | 15 | 45 | — |
| L4 | 30 | 2 | 6 | 20 | 60 | — |
| L5 | 20 | 3 | 9 | 30 | 90 | — |
| L6 | 15 | 4 | 12 | 40 | 120 | 400 |
| L7 | 10 | 6 | 18 | 60 | 180 | 600 |
| L8 | 8 | 8 | 23 | 75 | 225 | 750 |
| L9 | 5 | 12 | 36 | 120 | 360 | 1200 |

### Время жизни без подпитки:

| Формация | Ёмкость | Утечка/час | Время до 0 |
|----------|---------|------------|------------|
| L1 Small | 800 | 1 | 800 часов = 33 дня |
| L3 Small | 3,200 | 2 | 1,600 часов = 67 дней |
| L5 Small | 12,800 | 3 | 4,267 часов = 178 дней |
| L5 Great | 1,280,000 | 90 | 14,222 часов = 593 дня |
| L7 Great | 5,120,000 | 180 | 28,444 часов = 3.3 года |
| L9 Great | 20,480,000 | 360 | 56,889 часов = 6.5 года |
| L7 Heavy | 51,200,000 | 600 | 85,333 часов = 9.7 года |
| L9 Heavy | 204,800,000 | 1,200 | 170,667 часов = 19.5 года |

---

## 🔄 АЛГОРИТМ ОБРАБОТКИ

### Проверка утечки каждый тик:

```typescript
interface FormationState {
  id: string;
  currentQi: number;
  capacity: number;
  level: number;
  size: FormationSize;
  isHeavy: boolean;
  
  // === ПАРАМЕТРЫ УТЕЧКИ ===
  drainInterval: number;      // Интервал в тиках
  drainAmount: number;        // Ци за раз
  
  // === СОСТОЯНИЕ УТЕЧКИ ===
  ticksSinceLastDrain: number;  // Тиков с последней утечки
  lastDrainTick: number;        // Глобальный номер тика последней утечки
}

// Проверка при каждом тике:
function checkFormationDrain(
  formation: FormationState,
  currentGlobalTick: number
): { drained: number; newQi: number } {
  // Считаем сколько тиков прошло
  const ticksPassed = currentGlobalTick - formation.lastDrainTick;
  
  // Проверяем, наступил ли интервал утечки
  if (ticksPassed < formation.drainInterval) {
    return { drained: 0, newQi: formation.currentQi };
  }
  
  // Сколько раз должна была произойти утечка
  const drainCount = Math.floor(ticksPassed / formation.drainInterval);
  
  // Сколько Ци потеряно
  const totalDrained = drainCount * formation.drainAmount;
  
  // Новое значение (минимум 0)
  const newQi = Math.max(0, formation.currentQi - totalDrained);
  
  // Обновляем последний тик утечки
  formation.lastDrainTick = currentGlobalTick;
  formation.currentQi = newQi;
  
  return { 
    drained: Math.min(totalDrained, formation.currentQi), 
    newQi 
  };
}
```

### Интеграция с time-tick.service.ts:

```typescript
// Добавить в processTimeTickEffects:

export async function processTimeTickEffects(
  options: ProcessTimeTickOptions
): Promise<TimeTickResult> {
  // ... существующий код ...
  
  // === ОБРАБОТКА ФОРМАЦИЙ ===
  // Если есть активные формации в локации
  const activeFormations = await getActiveFormationsInLocation(locationId);
  
  for (const formation of activeFormations) {
    const drainResult = checkFormationDrain(formation, currentGlobalTick);
    
    if (drainResult.drained > 0) {
      // Логируем утечку
      logFormationDrain(formation.id, drainResult.drained);
      
      // Если формация истощена
      if (drainResult.newQi === 0) {
        await handleFormationDepleted(formation);
      }
    }
  }
  
  // ... остальной код ...
}
```

---

## 💾 СХЕМА ДАННЫХ

### Prisma schema:

```prisma
model Formation {
  id          String   @id @default(cuid())
  
  // === ПАРАМЕТРЫ ===
  level       Int
  type        String
  size        String
  isHeavy     Boolean  @default(false)
  
  // === ЁМКОСТЬ ===
  capacity    Int
  currentQi   Int
  
  // === УТЕЧКА ===
  drainInterval    Int      // Интервал в тиках
  drainAmount      Int      // Ци за раз
  lastDrainTick    Int      @default(0)
  
  // === СОСТОЯНИЕ ===
  isActive    Boolean  @default(false)
  locationId  String?
  
  createdAt   DateTime @default(now())
  updatedAt   DateTime @updatedAt
  
  @@map("formations")
}
```

---

## 🎮 ИНТЕРФЕЙС ОТОБРАЖЕНИЯ

### Как показывать игроку:

```typescript
interface FormationDisplay {
  // Текущее состояние
  currentQi: number;          // Целое число
  capacity: number;           // Целое число
  fillPercent: number;        // Math.round(currentQi / capacity * 100)
  
  // Утечка
  drainPerHour: number;       // "Утечка: 90 Ци/час"
  
  // Время до истощения
  timeRemaining: {
    hours: number;
    days: number;
    formatted: string;        // "178 дней" или "3.3 года"
  };
}

function formatTimeRemaining(drainPerHour: number, currentQi: number): string {
  if (drainPerHour <= 0) return '∞';
  
  const hoursRemaining = currentQi / drainPerHour;
  
  if (hoursRemaining < 24) {
    return `${Math.floor(hoursRemaining)} часов`;
  }
  
  const days = hoursRemaining / 24;
  
  if (days < 365) {
    return `${Math.floor(days)} дней`;
  }
  
  const years = days / 365;
  return `${years.toFixed(1)} года`;
}
```

---

## ✅ ПРИМЕРЫ СЦЕНАРИЕВ

### Сценарий 1: L5 Barrier Great без подпитки

```
Формация:
- Уровень: L5
- Размер: Great
- Ёмкость: 1,280,000 Ци
- Интервал утечки: 20 тиков (20 минут)
- Утечка за раз: 30 Ци
- Утечка в час: 90 Ци

Игра идёт 1 час (60 тиков):
- Проверок: 60
- Утечек: 3 (на 20, 40, 60 тиках)
- Потеряно: 90 Ци
- Осталось: 1,279,910 Ци

Игра идёт 24 часа (1440 тиков):
- Утечек: 72
- Потеряно: 2,160 Ци
- Осталось: 1,277,840 Ци

Время до истощения:
- 1,280,000 / 90 = 14,222 часа ≈ 593 дня
```

### Сценарий 2: L1 Small (кампания новичка)

```
Формация:
- Уровень: L1
- Размер: Small
- Ёмкость: 800 Ци
- Интервал утечки: 60 тиков (1 час)
- Утечка за раз: 1 Ци
- Утечка в час: 1 Ци

Игра идёт 1 час:
- Потеряно: 1 Ци
- Осталось: 799 Ци

Время до истощения:
- 800 / 1 = 800 часов ≈ 33 дня

Это даёт новичку месяц активной игры без забот о подпитке.
```

### Сценарий 3: L9 Heavy (защита города)

```
Формация:
- Уровень: L9
- Размер: Heavy
- Ёмкость: 204,800,000 Ци
- Интервал утечки: 5 тиков (5 минут)
- Утечка за раз: 100 Ци
- Утечка в час: 1,200 Ци

Время до истощения:
- 204,800,000 / 1,200 = 170,667 часа ≈ 19.5 года

Это стратегический объект — работает десятилетиями без внимания.
```

---

## 🔧 КОНСТАНТЫ

```typescript
// src/lib/game/constants.ts

export const FORMATION_DRAIN_CONSTANTS = {
  // Интервал утечки по уровню (в тиках)
  DRAIN_INTERVAL_BY_LEVEL: {
    1: 60,
    2: 50,
    3: 40,
    4: 30,
    5: 20,
    6: 15,
    7: 10,
    8: 8,
    9: 5,
  } as Record<number, number>,
  
  // Количество Ци за раз по размеру
  DRAIN_AMOUNT_BY_SIZE: {
    small: 1,
    medium: 3,
    large: 10,
    great: 30,
  } as Record<FormationSize, number>,
  
  // Множитель для тяжёлых формаций
  DRAIN_AMOUNT_HEAVY: 100,
};
```

---

## 📚 СВЯЗАННЫЕ ДОКУМЕНТЫ

- **[formation_unified.md](./formation_unified.md)** — Единая концепция формаций
- **[ARCHITECTURE.md](./ARCHITECTURE.md)** — Архитектура и система тиков

---

*Документ создан: 2026-03-22*
*Версия: 1.0*
*Система утечки с привязкой к тикам*
*Ци — только целые числа*
*Статус: Готов к реализации*
