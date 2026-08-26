# Stat Development System

**Версия:** 1.0  
**Дата:** 2026-03-14  
**Статус:** Реализовано

---

## Обзор

Stat Development System — система развития характеристик персонажа через действия. Основана на **виртуальной дельте**, которая закрепляется при сне.

---

## Ключевые принципы

1. **Тело независимо от культивации** — развитие тела не зависит от уровня Ци
2. **Действия создают дельту** — тренировка, бой, работа развивают характеристики
3. **Сон закрепляет прогресс** — виртуальная дельта конвертируется в постоянные статы
4. **Пороги развития** — чем выше стат, тем больше дельты нужно для повышения

---

## Формула порогов развития

```
threshold = max(1.0, floor(currentStat / 10))
```

| Текущий стат | Порог | Накопленная дельта для +1 |
|-------------|-------|---------------------------|
| 10 | 1.0 | 1.0 |
| 20 | 2.0 | 2.0 |
| 50 | 5.0 | 5.0 |
| 100 | 10.0 | 10.0 |

---

## Структура файлов

```
src/lib/game/
├── stat-development.ts     # Виртуальная дельта
├── stat-threshold.ts       # Пороги развития
├── stat-truth.ts           # Truth System интеграция
├── stat-simulation.ts      # Симуляция развития
└── training-system.ts      # Система тренировки

src/types/
└── stat-development.ts     # Типы
```

---

## Типы данных

### StatDevelopment

```typescript
interface StatDevelopment {
  current: number;        // Текущее значение
  virtualDelta: number;   // Виртуальная дельта (не закреплённая)
  threshold: number;      // Текущий порог
  sources: DeltaSource[]; // Источники дельты
}
```

### DeltaSource

```typescript
interface DeltaSource {
  type: DeltaSourceType;
  timestamp: number;
  value: number;
  description?: string;
}

type DeltaSourceType = 
  | 'combat_attack'
  | 'combat_defense'
  | 'training'
  | 'work'
  | 'technique_practice'
  | 'meditation';
```

---

## Источники дельты

### Боевая дельта

| Действие | Сила | Ловкость | Интеллект |
|----------|------|----------|-----------|
| Удар (попал) | +0.001 | - | - |
| Удар (блок) | +0.0005 | - | - |
| Уворот | - | +0.001 | - |
| Парирование | - | +0.001 | - |
| Использование техники | - | - | +0.0005 |

### Тренировка

| Тип | Распределение | Эффективность |
|-----|---------------|---------------|
| classical | 50% сила / 50% ловкость | 100% |
| focused | 70% основная / 30% вторичная | 80% |
| extreme | 95% основная / 5% вторичная | 60% (только L5+) |

---

## Закрепление при сне

### Кап закрепления

```
MAX_CONSOLIDATION_PER_SLEEP = 0.20  // за 8 часов
```

### Формула

```typescript
function calculateMaxConsolidation(sleepHours: number): number {
  // 4 часа = +0.067, 6 часов = +0.133, 8 часов = +0.20
  return Math.min(0.20, sleepHours * 0.025);
}
```

### Процесс

```typescript
function processSleep(
  stats: CharacterStatsDevelopment,
  sleepHours: number
): SleepConsolidationResult {
  const maxConsolidation = calculateMaxConsolidation(sleepHours);
  
  // Распределение по статам с приоритетом наибольшей дельты
  const result = consolidateAllStats(stats, maxConsolidation);
  
  return result;
}
```

---

## Пример развития

### 1000 дней игры

| Характеристика | Начало | После 1000 дней |
|---------------|--------|------------------|
| Сила | 10 | ~55 |
| Ловкость | 10 | ~55 |
| Интеллект | 10 | ~55 |
| Живучесть | 10 | ~55 |

**Множитель от тела:** ×4.75 (независимо от культивации)

---

## Система тренировки

### TrainingSession

```typescript
interface TrainingSession {
  id: string;
  type: TrainingType;
  startedAt: number;
  duration: number;      // в минутах
  mainStat: StatName;
  secondaryStat?: StatName;
  fatigueCost: number;
}
```

### API

```typescript
// Начать тренировку
POST /api/training/start
{
  "type": "focused",
  "mainStat": "strength",
  "secondaryStat": "agility"
}

// Завершить тренировку
POST /api/training/complete
{
  "sessionId": "session_001"
}
```

---

## Усталость и эффективность

```typescript
function calculateTrainingEfficiency(fatigue: number): number {
  // 0% усталости = 100% эффективность
  // 50% усталости = 75% эффективность
  // 100% усталости = 25% эффективность
  return Math.max(0.25, 1 - fatigue * 0.5);
}
```

---

## Живучесть (Vitality)

### Влияние на HP

```typescript
function calculateVitalityMultiplier(vitality: number): number {
  // vitality 10 = ×1.0
  // vitality 50 = ×3.0
  // vitality 100 = ×5.5
  return 1 + (vitality - 10) * 0.05;
}
```

### Бонусы живучести

| Vitality | HP множитель | Регенерация | Сопротивление шоку |
|----------|--------------|-------------|---------------------|
| 10 | ×1.0 | 0% | 0% |
| 30 | ×2.0 | +20% | +20% |
| 50 | ×3.0 | +40% | +40% |
| 100 | ×5.5 | +90% | +50% |

---

## API Endpoints

### Добавить дельту

```typescript
POST /api/character/delta
{
  "sessionId": "session_001",
  "delta": {
    "strength": 0.01,
    "agility": 0.005
  }
}
```

### Получить статы

```typescript
GET /api/character/stats?sessionId=session_001
```

---

## Связанные системы

- [Body System](./body.md) — части тела и HP
- [Combat System](./combat-system.md) — источники боевой дельты
- [Fatigue System](./fatigue-system.md) — усталость
- [Training System](./training-system.md) — тренировка

---

## Константы

```typescript
// src/lib/game/constants.ts
export const STAT_DEVELOPMENT_CONSTANTS = {
  DELTA_PER_ATTACK: 0.001,
  DELTA_PER_DODGE: 0.001,
  MAX_CONSOLIDATION_PER_SLEEP: 0.20,
  TRAINING_DELTA_PER_MINUTE: 0.002,
  FATIGUE_PER_TRAINING_MINUTE: 0.5,
};
```

---

*Документ создан: 2026-03-14*
