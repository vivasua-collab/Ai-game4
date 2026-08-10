# 🚀 План внедрения системы развития тела

**Версия:** 1.0
**Создано:** 2026-03-14
**Статус:** 📋 Готов к реализации
**Источник:** docs/body.md + docs/development-1000-days-calculation.md + docs/stat-threshold-system.md

---

## 📊 Обзор внедрения

### Цель
Реализовать систему развития физических характеристик с механизмами:
- **Виртуальная дельта** — временное накопление прогресса
- **Пороги развития** — естественное замедление роста
- **Закрепление при сне** — конвертация виртуальной дельты в постоянные статы
- **Тренировка** — целенаправленное развитие характеристик

### Ключевые параметры

| Параметр | Значение |
|----------|----------|
| Прирост за действие | 0.001 |
| Кап закрепления за сон (8ч) | +0.20 |
| Минимальный сон для закрепления | 4 часа |
| Формула порога | `floor(currentStat / 10)`, min 1.0 |
| Множитель культивации | НЕТ (тело независимо) |

---

## 🗂️ Структура файлов для создания/изменения

### Новые файлы

| Файл | Назначение |
|------|------------|
| `src/types/stat-development.ts` | Типы и интерфейсы |
| `src/lib/game/stat-development.ts` | Основная логика развития |
| `src/lib/game/stat-threshold.ts` | Система порогов |
| `src/lib/game/training-system.ts` | Система тренировки |
| `docs/implementation/phase-1-types.md` | Фаза 1: Типы |
| `docs/implementation/phase-2-thresholds.md` | Фаза 2: Пороги |
| `docs/implementation/phase-3-virtual-delta.md` | Фаза 3: Виртуальная дельта |
| `docs/implementation/phase-4-sleep-consolidation.md` | Фаза 4: Закрепление |
| `docs/implementation/phase-5-training.md` | Фаза 5: Тренировка |
| `docs/implementation/phase-6-combat-integration.md` | Фаза 6: Интеграция с боем |
| `docs/implementation/phase-7-ui.md` | Фаза 7: UI |
| `docs/implementation/phase-8-testing.md` | Фаза 8: Тестирование |

### Изменяемые файлы

| Файл | Изменения |
|------|-----------|
| `prisma/schema.prisma` | Добавить поля StatDevelopment |
| `src/lib/game/constants.ts` | Добавить константы развития |
| `src/lib/game/fatigue-system.ts` | Интеграция с развитием |
| `src/lib/game/time-system.ts` | Обработка сна и закрепления |
| `src/lib/game/combat-system.ts` | Генерация виртуальной дельты |

---

## 📋 Фазы внедрения

### ФАЗА 1: Типы и интерфейсы
**Файл:** `docs/implementation/phase-1-types.md`
**Задачи:**
1. Создать `src/types/stat-development.ts`
2. Определить интерфейс `StatDevelopment`
3. Определить интерфейс `CharacterStatsDevelopment`
4. Определить интерфейс `TrainingSession`
5. Добавить типы вариантов тренировки

**Зависимости:** нет
**Приоритет:** P0 (блокирующая)

---

### ФАЗА 2: Система порогов развития
**Файл:** `docs/implementation/phase-2-thresholds.md`
**Задачи:**
1. Создать `src/lib/game/stat-threshold.ts`
2. Реализовать `calculateStatThreshold(currentStat: number): number`
3. Реализовать `getStatProgress(stat: StatDevelopment): number`
4. Реализовать `canAdvanceStat(stat: StatDevelopment): boolean`
5. Реализовать `advanceStat(stat: StatDevelopment): AdvanceResult`
6. Написать юнит-тесты

**Зависимости:** Фаза 1
**Приоритет:** P0 (блокирующая)

---

### ФАЗА 3: Виртуальная дельта
**Файл:** `docs/implementation/phase-3-virtual-delta.md`
**Задачи:**
1. Создать `src/lib/game/stat-development.ts`
2. Реализовать `addVirtualDelta(stat, amount, source): AddDeltaResult`
3. Реализовать `getMaxVirtualDelta(stat): number` (если нужен кап)
4. Реализовать `calculateDeltaFromAction(action, intensity): number`
5. Определить таблицу действий и их вклад в дельту
6. Написать юнит-тесты

**Зависимости:** Фаза 1
**Приоритет:** P0 (блокирующая)

---

### ФАЗА 4: Закрепление при сне
**Файл:** `docs/implementation/phase-4-sleep-consolidation.md`
**Задачи:**
1. Реализовать `calculateMaxConsolidation(sleepHours: number): number`
2. Реализовать `consolidateDelta(stat, sleepHours): ConsolidationResult`
3. Интегрировать с `time-system.ts` (обработка сна)
4. Добавить логирование закрепления
5. Написать юнит-тесты

**Зависимости:** Фаза 2, Фаза 3
**Приоритет:** P0 (блокирующая)

---

### ФАЗА 5: Система тренировки
**Файл:** `docs/implementation/phase-5-training.md`
**Задачи:**
1. Создать `src/lib/game/training-system.ts`
2. Определить 3 варианта тренировки:
   - **Классическая** (50/50 физическая/ментальная)
   - **Фокусная** (70/30)
   - **Экстремальная** (95/5)
3. Реализовать `startTraining(character, config): TrainingSession`
4. Реализовать `processTrainingTick(session): TrainingTickResult`
5. Реализовать `endTraining(session): TrainingResult`
6. Интегрировать с усталостью

**Зависимости:** Фаза 3
**Приоритет:** P1

---

### ФАЗА 6: Интеграция с боем
**Файл:** `docs/implementation/phase-6-combat-integration.md`
**Задачи:**
1. Изменить `combat-system.ts` — генерация дельты за удары
2. Добавить расчёт дельты для разных типов атак
3. Интегрировать с Fatigue System
4. Добавить множители от оружия/техник
5. Протестировать баланс

**Зависимости:** Фаза 3
**Приоритет:** P1

---

### ФАЗА 7: UI компоненты
**Файл:** `docs/implementation/phase-7-ui.md`
**Задачи:**
1. Компонент отображения прогресса характеристики
2. Компонент панели развития
3. Компонент выбора тренировки
4. Отображение порогов и прогресса
5. Визуализация виртуальной дельты

**Зависимости:** Фаза 1-4
**Приоритет:** P2

---

### ФАЗА 8: Тестирование и балансировка
**Файл:** `docs/implementation/phase-8-testing.md`
**Задачи:**
1. Симуляция развития за 1000 дней
2. Симуляция развития за 10000 дней
3. Проверка достижимости целевых статов
4. Корректировка параметров
5. Документирование результатов

**Зависимости:** Все фазы
**Приоритет:** P2

---

## 🔄 Граф зависимостей

```
Фаза 1 (Типы)
    │
    ├──► Фаза 2 (Пороги) ──┐
    │                      │
    └──► Фаза 3 (Вирт.дельта) ──┬──► Фаза 4 (Сон)
                               │
                               ├──► Фаза 5 (Тренировка)
                               │
                               └──► Фаза 6 (Бой)
                                       
Фаза 1-4 ──► Фаза 7 (UI)
Все фазы ──► Фаза 8 (Тесты)
```

---

## 📐 Схема базы данных

### Расширение модели Character

```prisma
model Character {
  // ... существующие поля ...
  
  // === Система развития характеристик ===
  // JSON: { strength: {...}, agility: {...}, intelligence: {...}, vitality: {...} }
  statsDevelopment String @default("{}")
  
  // === Активная тренировка ===
  // JSON: { type, targetStat, startedAt, progress, fatigue }
  activeTraining String @default("{}")
}
```

### Структура JSON statsDevelopment

```typescript
{
  "strength": {
    "current": 15.0,
    "virtualDelta": 0.85,
    "threshold": 1.0,
    "lastTrainingAt": "2026-03-14T10:00:00Z"
  },
  "agility": {
    "current": 12.0,
    "virtualDelta": 0.42,
    "threshold": 1.0,
    "lastTrainingAt": "2026-03-14T08:30:00Z"
  },
  // ...
}
```

---

## ⚙️ Константы системы

### Файл: `src/lib/game/constants.ts`

```typescript
// === Система развития тела ===
export const STAT_DEVELOPMENT_CONSTANTS = {
  // Прирост за действие
  DELTA_PER_ACTION: 0.001,
  
  // Закрепление при сне
  MIN_SLEEP_HOURS_FOR_CONSOLIDATION: 4,
  MAX_SLEEP_HOURS_FOR_CONSOLIDATION: 8,
  MAX_CONSOLIDATION_PER_SLEEP: 0.20, // За 8 часов
  BASE_CONSOLIDATION_PER_HOUR: 0.033,
  MIN_CONSOLIDATION_PER_SLEEP: 0.067, // За 4 часа
  
  // Пороги развития
  THRESHOLD_DIVISOR: 10, // floor(stat / 10)
  MIN_THRESHOLD: 1.0,
  
  // Множители источников дельты
  DELTA_SOURCES: {
    combat_hit: 0.001,
    combat_block: 0.0005,
    combat_dodge: 0.001,
    physical_labor: 0.001,
    training_classical: 0.01, // за минуту
    training_focused: 0.015,
    training_extreme: 0.02,
    technique_learning: 0.001, // за минуту изучения
    meditation_intelligence: 0.01, // за минуту
  },
  
  // Варианты тренировки (физическая/ментальная нагрузка)
  TRAINING_TYPES: {
    classical: { physical: 0.5, mental: 0.5, deltaMultiplier: 1.0 },
    focused: { physical: 0.7, mental: 0.3, deltaMultiplier: 1.2 },
    extreme: { physical: 0.95, mental: 0.05, deltaMultiplier: 1.5 },
  },
} as const;
```

---

## 🧪 Критерии приёмки

### Функциональные требования

1. **Накопление дельты**
   - [ ] Каждое боевое действие добавляет виртуальную дельту
   - [ ] Дельта корректно суммируется
   - [ ] Источник дельты логируется

2. **Пороги развития**
   - [ ] Порог вычисляется по формуле `floor(stat/10)`
   - [ ] При достижении порога происходит повышение
   - [ ] Остаток дельты сохраняется

3. **Закрепление при сне**
   - [ ] Сон < 4 часов не даёт закрепления
   - [ ] 8 часов сна дают кап +0.20
   - [ ] Прогресс корректно отображается

4. **Тренировка**
   - [ ] 3 варианта тренировки работают
   - [ ] Усталость корректно влияет на тренировку
   - [ ] Критическая усталость останавливает тренировку

### Нефункциональные требования

1. **Производительность**
   - [ ] Расчёт дельты не замедляет бой
   - [ ] Закрепление при сне происходит моментально

2. **Баланс**
   - [ ] За 1000 дней достижим стат ~55
   - [ ] За 10000 дней достижим стат ~125
   - [ ] Развитие замедляется естественным образом

---

## 📝 Примечания для ИИ агентов

### Важно!

1. **НЕ ИЗОБРЕТАТЬ ВЕЛОСИПЕД**
   - Использовать существующие типы из `src/types/body.ts`
   - Переиспользовать `fatigue-system.ts` для интеграции усталости
   - Использовать существующий паттерн сериализации JSON

2. **СОБЛЮДАТЬ КОНВЕНЦИИ**
   - Все функции должны быть чистыми где возможно
   - Документировать JSDoc для всех публичных функций
   - Логировать важные события

3. **БАЛАНС**
   - При сомнениях сверяться с docs/development-1000-days-calculation.md
   - Исторические варианты (зачёркнутые) НЕ реализовывать
   - Финальная формула порогов: `floor(stat/10)`, минимум 1.0

4. **ТЕСТИРОВАНИЕ**
   - Симуляция развития — обязательный этап (Фаза 8)
   - Проверять граничные случаи (стат 10, 19, 20, 99, 100)

---

## 🔗 Связанные документы

- [body.md](./body.md) — Система тела
- [stat-threshold-system.md](./stat-threshold-system.md) — Система порогов
- [development-1000-days-calculation.md](./development-1000-days-calculation.md) — Расчёты
- [body-development-analysis.md](./body-development-analysis.md) — Анализ
- [checkpoint_03_14_plan.md](./checkpoints/checkpoint_03_14_plan.md) — Чекпоинт

---

*Документ создан: 2026-03-14*
*Для реализации ИИ агентами*
