# Condition System

**Версия:** 1.0  
**Дата:** 2026-03-14  
**Статус:** Реализовано

---

## Обзор

Condition System — система состояний (баффов/дебаффов) для персонажей. Управляет временными эффектами, их применением, тиком и удалением.

---

## Типы состояний

### Категории

| Категория | Описание | Примеры |
|-----------|----------|---------|
| **Buff** | Положительные эффекты | Регенерация, Усиление, Защита |
| **Debuff** | Отрицательные эффекты | Яд, Кровотечение, Замедление |
| **Status** | Нейтральные состояния | Оглушение, Сон, Медитация |

### Структура Condition

```typescript
interface ActiveCondition {
  id: string;
  type: ConditionType;
  source: string;          // Источник (техника, предмет, окружение)
  target: string;          // Цель (characterId)
  value: number;           // Значение эффекта
  duration: number;        // Оставшееся время (в минутах)
  maxDuration: number;     // Максимальная длительность
  stacks: number;          // Количество стаков
  maxStacks: number;       // Макс. стаков
  appliedAt: number;       // Время применения
  metadata?: Record<string, unknown>; // Дополнительные данные
}
```

---

## Структура файлов

```
src/lib/game/
├── condition-manager.ts    # Управление состояниями
├── condition-registry.ts   # Регистр типов состояний
├── condition-effects.ts   # Применение эффектов
└── bleeding-system.ts      # Система кровотечения
```

---

## ConditionManager

Основной класс для управления состояниями:

```typescript
class ConditionManager {
  // Добавить состояние
  addCondition(condition: ActiveCondition): void;
  
  // Удалить состояние
  removeCondition(conditionId: string): void;
  
  // Получить активные состояния
  getActiveConditions(targetId: string): ActiveCondition[];
  
  // Тик состояний (вызывается каждую минуту)
  tick(targetId: string): ConditionTickResult;
  
  // Очистить все состояния
  clearAll(targetId: string): void;
}
```

---

## ConditionRegistry

Регистр типов состояний с конфигурацией:

```typescript
const CONDITION_REGISTRY = {
  // Положительные
  regeneration: {
    category: 'buff',
    stackable: true,
    maxStacks: 5,
    tickEffect: 'heal',
    icon: '💚',
    name: 'Регенерация',
  },
  clarity: {
    category: 'buff',
    stackable: false,
    tickEffect: 'qi_regen',
    icon: '✨',
    name: 'Ясность',
  },
  
  // Отрицательные
  poison: {
    category: 'debuff',
    stackable: true,
    maxStacks: 10,
    tickEffect: 'damage',
    icon: '☠️',
    name: 'Отравление',
  },
  bleeding: {
    category: 'debuff',
    stackable: true,
    maxStacks: 5,
    tickEffect: 'damage_percent',
    icon: '🩸',
    name: 'Кровотечение',
  },
  stun: {
    category: 'status',
    stackable: false,
    tickEffect: 'none',
    icon: '💫',
    name: 'Оглушение',
  },
};
```

---

## Обработка тиков

### ConditionTickResult

```typescript
interface ConditionTickResult {
  targetId: string;
  conditions: ActiveCondition[];
  effects: ConditionEffect[];
  expired: string[];        // ID истёкших состояний
  damage: number;
  healing: number;
  qiChange: number;
  fatigueChange: number;
}
```

### Пример тика

```typescript
// Каждую игровую минуту
const result = conditionManager.tick('character_001');

// Результат:
{
  targetId: 'character_001',
  conditions: [...],
  effects: [
    { type: 'poison_damage', value: -5 },
    { type: 'regeneration_heal', value: 10 },
  ],
  expired: ['stun_123'],
  damage: 5,
  healing: 10,
  qiChange: 0,
  fatigueChange: 0,
}
```

---

## Эффекты состояний

### Урон
- **poison** — фиксированный урон за тик
- **bleeding** — процент от максимального HP
- **burning** — фиксированный урон + шанс распространения

### Лечение
- **regeneration** — фиксированное лечение
- **vitality_boost** — процент от максимального HP

### Модификаторы
- **haste** — +скорость атаки
- **slow** — -скорость передвижения
- **weakness** — -урон
- **fortify** — +защита

---

## API

### Добавление состояния

```typescript
POST /api/conditions
{
  "action": "add",
  "sessionId": "session_001",
  "condition": {
    "type": "poison",
    "source": "technique_poison_strike",
    "value": 5,
    "duration": 60,
    "stacks": 1
  }
}
```

### Тик состояний

```typescript
POST /api/conditions/tick
{
  "sessionId": "session_001",
  "ticks": 1
}
```

### Список состояний

```typescript
GET /api/conditions?sessionId=session_001
```

---

## Визуализация

### ConditionBadge компонент

```tsx
<ConditionBadge condition={condition} />
// Отображает иконку, стаки и таймер
```

### ActiveConditionsPanel

```tsx
<ActiveConditionsPanel conditions={activeConditions} />
// Панель со всеми активными состояниями
```

---

## Интеграция с другими системами

| Система | Интеграция |
|---------|------------|
| Combat | Наложение состояний при атаке |
| Techniques | Техники могут накладывать состояния |
| Inventory | Расходники могут снимать состояния |
| Meditation | Очищение негативных состояний |

---

## Связанные системы

- [Event Bus System](./event-bus-system.md)
- [Combat System](./combat-system.md)
- [Technique System](./technique-system.md)

---

*Документ создан: 2026-03-14*
