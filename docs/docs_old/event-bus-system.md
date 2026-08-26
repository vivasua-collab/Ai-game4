# Event Bus System

**Версия:** 1.0  
**Дата:** 2026-03-14  
**Статус:** Реализовано

---

## Обзор

Event Bus — централизованная система обработки событий для синхронизации состояния между сервером и клиентом. Все взаимодействия с Truth System происходят через Event Bus.

---

## Архитектура

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   Client    │────▶│  Event Bus  │────▶│ TruthSystem │
│  (React)    │◀────│  (Server)   │◀────│   (Memory)  │
└─────────────┘     └─────────────┘     └─────────────┘
                           │
                           ▼
                    ┌─────────────┐
                    │   Handlers  │
                    │ (combat,    │
                    │  stat, etc) │
                    └─────────────┘
```

---

## Структура файлов

```
src/lib/game/event-bus/
├── index.ts           # Экспорт API
├── types.ts           # Типы событий
├── client.ts          # Клиентская часть
├── processor.ts       # Обработка событий
├── validator.ts       # Валидация
├── logger.ts          # Логирование
└── handlers/
    ├── combat.ts      # Боевые события
    ├── stat.ts        # Изменение характеристик
    ├── body.ts        # Состояние тела
    ├── inventory.ts   # Инвентарь
    ├── movement.ts    # Перемещение
    └── environment.ts # Окружение
```

---

## Типы событий

### BaseEvent
```typescript
interface BaseEvent {
  type: string;
  timestamp: number;
  sessionId: string;
}
```

### Основные категории событий

| Категория | Префикс | Примеры |
|-----------|---------|---------|
| Combat | `combat:` | `combat:attack`, `combat:damage`, `combat:dodge` |
| Stat | `stat:` | `stat:change`, `stat:train` |
| Body | `body:` | `body:damage`, `body:heal` |
| Inventory | `inventory:` | `inventory:add`, `inventory:remove`, `inventory:equip` |
| Movement | `movement:` | `movement:move`, `movement:teleport` |
| Environment | `env:` | `env:interact`, `env:destroy` |

---

## Использование

### Клиентская сторона

```typescript
import { eventBusClient } from '@/lib/game/event-bus';

// Отправка события атаки
eventBusClient.reportDamageDealt({
  targetId: 'TEMP_enemy_001',
  damage: 50,
  type: 'physical',
});

// Отправка события перемещения
eventBusClient.reportMove({
  from: { x: 100, y: 200 },
  to: { x: 150, y: 250 },
});
```

### Серверная сторона

```typescript
import { processEvent } from '@/lib/game/event-bus';

// Обработка входящего события
const result = await processEvent(event);
```

---

## Обработчики (Handlers)

### Combat Handler
Обрабатывает боевые события:
- `combat:attack` — атака
- `combat:damage` — получение урона
- `combat:dodge` — уклонение
- `combat:block` — блок

### Stat Handler
Обрабатывает изменение характеристик:
- `stat:change` — изменение стата
- `stat:train` — тренировка

### Body Handler
Обрабатывает состояние тела:
- `body:damage` — повреждение части тела
- `body:heal` — лечение
- `body:apply_condition` — применение состояния

### Inventory Handler
Обрабатывает инвентарь:
- `inventory:add` — добавление предмета
- `inventory:remove` — удаление предмета
- `inventory:equip` — экипировка
- `inventory:use` — использование

---

## Валидация

Все события проходят через валидатор перед обработкой:

```typescript
// validator.ts
export function validateEvent(event: unknown): event is GameEvent {
  // Проверка структуры события
  // Проверка обязательных полей
  // Проверка типов данных
}
```

---

## Логирование

Все события логируются через QiLogger:

```typescript
// logger.ts
export function logEvent(event: GameEvent, result: EventResult) {
  qiLogger.log('EVENT_BUS', `${event.type}: ${result.success ? 'OK' : 'FAILED'}`);
}
```

---

## Правила

1. **Память первична** — все изменения проходят через Truth System
2. **БД вторична** — запись в БД происходит после обработки в памяти
3. **Единая точка входа** — все события через Event Bus
4. **Валидация обязательна** — все события должны быть валидными

---

## Связанные системы

- [Condition System](./condition-system.md)
- [Stat Development](./stat-development-system.md)
- [Truth System](./truth-system.md)

---

*Документ создан: 2026-03-14*
