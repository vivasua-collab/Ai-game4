# 🔧 АРХИТЕКТУРА РЕФАКТОРИНГА: Серверная Миграция

**Версия:** 1.0
**Дата:** 2026-03-25
**Статус:** 📋 ПЛАНИРОВАНИЕ

---

## 🎯 ЦЕЛЬ РЕФАКТОРИНГА

### Принципы целевой архитектуры

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                    ПРИНЦИПЫ СЕРВЕРНОЙ АРХИТЕКТУРЫ                             │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   1. СЕРВЕР - ИСТОЧНИК ИСТИНЫ                                               │
│      ├── Все расчёты урона на сервере                                        │
│      ├── Все списания Qi на сервере                                          │
│      ├── Все изменения HP на сервере                                         │
│      └── Все решения AI на сервере                                           │
│                                                                              │
│   2. КЛИЕНТ - ТОЛЬКО ОТОБРАЖЕНИЕ                                            │
│      ├── Получение команд от сервера                                         │
│      ├── Визуализация (анимации, эффекты)                                    │
│      ├── Отправка input на сервер                                            │
│      └── НИКАКОЙ бизнес-логики                                               │
│                                                                              │
│   3. ПРОТОКОЛ ВЗАИМОДЕЙСТВИЯ                                                 │
│      ├── HTTP API - REST операции (инвентарь, медитация)                    │
│      └── WebSocket - Real-time (бой, NPC, движение)                          │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

---

## 📊 АНАЛИЗ: ПЕРЕПИСАТЬ ИЛИ РЕФАКТОРИТЬ?

### Текущее состояние проекта

| Аспект | Оценка | Комментарий |
|--------|--------|-------------|
| **Объём кода** | ~50,000+ строк | Большой объём |
| **Архитектура БД** | ✅ Правильная | Prisma + SQLite работает |
| **TruthSystem** | ✅ Правильная | Память первична |
| **Event Bus** | ✅ Правильная | Уже серверная |
| **Phaser сцены** | ⚠️ Смешанная | Логика + отображение |
| **Боевая система** | ❌ На клиенте | Нужна миграция |
| **AI система** | ❌ На клиенте | Нужна миграция |
| **Техники** | ❌ На клиенте | Нужна миграция |

### Сравнение подходов

| Критерий | Переписать | Рефакторинг |
|----------|------------|-------------|
| **Время** | 4-8 недель | 2-4 недели |
| **Риск** | Высокий (потеря функционала) | Низкий (постепенно) |
| **Сохранение данных** | Миграция БД | Без изменений |
| **Тестирование** | С нуля | Постепенное |
| **Откат** | Сложно | Возможно на каждом этапе |

### 🎯 РЕШЕНИЕ: ПОЭТАПНЫЙ РЕФАКТОРИНГ

**Причины:**
1. TruthSystem и Event Bus уже работают правильно
2. База данных и Prisma не требуют изменений
3. React UI компоненты не требуют изменений
4. Phaser визуал можно оставить

**Требуется миграция:**
1. Боевой системы
2. AI системы
3. Техник и Qi

---

## 🔍 ДЕТАЛЬНЫЙ АНАЛИЗ ПРОБЛЕМ

### 1. БОЕВАЯ СИСТЕМА

#### Текущее состояние (НЕПРАВИЛЬНО)

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                    БОЙ - НА КЛИЕНТЕ (ТЕКУЩЕЕ)                                 │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   Клиент (Phaser)                                                           │
│   ┌─────────────────────────────────────────────────────────────────────┐   │
│   │                                                                      │   │
│   │   TechniqueSlotsManager.use()                                       │   │
│   │   ├── this.characterQi -= qiCost  ← ЧИТ: подмена Qi                 │   │
│   │   ├── calculateDamage(technique)  ← Расчёт на клиенте               │   │
│   │   └── fireProjectile(damage)      ← Снаряд с уроном                │   │
│   │                                                                      │   │
│   │   ProjectileManager.onProjectileHit()                               │   │
│   │   ├── hitResult = proj.hit(npc)   ← Расчёт попадания               │   │
│   │   ├── npc.takeDamage(damage)      ← Урон на клиенте!               │   │
│   │   └── npc.hp -= damage            ← HP изменяется локально          │   │
│   │                                                                      │   │
│   │   NPCSprite.updateSpinalAI()                                        │   │
│   │   ├── spinalController.update()   ← AI на клиенте                   │   │
│   │   └── executeSpinalAction()       ← Выполнение действий             │   │
│   │                                                                      │   │
│   └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
│   Сервер                                                                     │
│   ┌─────────────────────────────────────────────────────────────────────┐   │
│   │                                                                      │   │
│   │   WebSocket: player:attack                                          │   │
│   │   └── Принимает damage от клиента ← ЧИТ: подмена урона              │   │
│   │                                                                      │   │
│   │   Event Bus: technique:use                                          │   │
│   │   └── Только списание Qi (если используется)                        │   │
│   │                                                                      │   │
│   └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

#### Проблемные файлы

| Файл | Строки | Проблема |
|------|--------|----------|
| `src/game/services/TechniqueSlotsManager.ts` | 281-575 | Расчёт урона, списание Qi |
| `src/game/services/ProjectileManager.ts` | 111-156 | Применение урона локально |
| `src/game/objects/TechniqueProjectile.ts` | 423-477 | hit() с уроном |
| `src/lib/game/damage-pipeline.ts` | 196-360 | Весь pipeline на клиенте |
| `src/lib/game/combat-system.ts` | 718-806 | Расчёт урона техники |
| `src/lib/game/npc-damage-calculator.ts` | 167-324 | Урон от NPC |
| `src/game/objects/NPCSprite.ts` | 993-1045 | takeDamage() локально |

---

### 2. AI СИСТЕМА

#### Текущее состояние (НЕПРАВИЛЬНО)

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                    AI - НА КЛИЕНТЕ (ТЕКУЩЕЕ)                                  │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   Клиент (Phaser)                                                           │
│   ┌─────────────────────────────────────────────────────────────────────┐   │
│   │                                                                      │   │
│   │   LocationScene.updateAI()                                          │   │
│   │   ├── Каждый кадр (60 FPS)                                          │   │
│   │   └── updateNPCBehavior(npc)                                        │   │
│   │                                                                      │   │
│   │   NPCSprite.updateSpinalAI()                                        │   │
│   │   ├── SpinalController.update()                                     │   │
│   │   │   ├── Анализ окружения                                          │   │
│   │   │   ├── Принятие решений                                          │   │
│   │   │   └── Генерация действий                                        │   │
│   │   └── executeSpinalAction()                                         │   │
│   │       ├── dodge, flinch, flee                                       │   │
│   │       └── qi_shield, alert                                          │   │
│   │                                                                      │   │
│   │   NPCSprite.takeDamage()                                            │   │
│   │   ├── generateSpinalSignal('damage')                                │   │
│   │   └── Локальная реакция на урон                                     │   │
│   │                                                                      │   │
│   └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
│   Сервер (WebSocket)                                                        │
│   ┌─────────────────────────────────────────────────────────────────────┐   │
│   │                                                                      │   │
│   │   processLocalAI()                                                  │   │
│   │   ├── Простая проверка дистанции                                    │   │
│   │   ├── Hardcoded damage = 10                                         │   │
│   │   └── НЕ интегрирован с TruthSystem                                 │   │
│   │                                                                      │   │
│   └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

#### Проблемные файлы

| Файл | Строки | Проблема |
|------|--------|----------|
| `src/game/scenes/LocationScene.ts` | 1340-1359 | updateAI() на клиенте |
| `src/game/objects/NPCSprite.ts` | 239-297 | updateSpinalAI() |
| `src/lib/game/ai/spinal/spinal-controller.ts` | 111-154 | Весь AI на клиенте |
| `src/lib/game/ai/spinal/reflexes.ts` | Весь файл | Рефлексы на клиенте |
| `mini-services/game-ws/index.ts` | 321-527 | Упрощённый AI, нет интеграции |

---

### 3. ТЕХНИКИ И QI

#### Текущее состояние (НЕПРАВИЛЬНО)

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                    ТЕХНИКИ - НА КЛИЕНТЕ (ТЕКУЩЕЕ)                             │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   Клиент (Phaser)                                                           │
│   ┌─────────────────────────────────────────────────────────────────────┐   │
│   │                                                                      │   │
│   │   TechniqueSlotsManager.use()                                       │   │
│   │   ├── characterQi -= qiCost  ← СПИСАНИЕ QI НА КЛИЕНТЕ               │   │
│   │   │   └── ЧИТ: подмена characterQi                                  │   │
│   │   │                                                                  │   │
│   │   ├── calculateDamage(technique)                                    │   │
│   │   │   ├── qiDensity = calculateQiDensity(level)                     │   │
│   │   │   ├── capacity = calculateTechniqueCapacity(...)                │   │
│   │   │   ├── stability = checkDestabilization(...)                     │   │
│   │   │   └── damage = qi * density * mult * mastery * grade            │   │
│   │   │   └── ВЕСЬ РАСЧЁТ НА КЛИЕНТЕ!                                   │   │
│   │   │                                                                  │   │
│   │   └── onFireProjectile({ damage, element, ... })                    │   │
│   │       └── Создание снаряда с УЖЕ рассчитанным уроном                │   │
│   │                                                                      │   │
│   └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
│   Сервер                                                                     │
│   ┌─────────────────────────────────────────────────────────────────────┐   │
│   │                                                                      │   │
│   │   Event Bus: technique:use                                          │   │
│   │   └── Только проверка canUse, НЕ применяется урон                   │   │
│   │                                                                      │   │
│   │   TruthSystem.spendQi()                                             │   │
│   │   └── Вызывается ТОЛЬКО если клиент использует Event Bus            │   │
│   │       └── TechniqueSlotsManager НЕ использует Event Bus!            │   │
│   │                                                                      │   │
│   └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

#### Проблемные файлы

| Файл | Строки | Проблема |
|------|--------|----------|
| `src/game/services/TechniqueSlotsManager.ts` | 281-393 | use() списывает Qi локально |
| `src/game/services/TechniqueSlotsManager.ts` | 512-575 | calculateDamage() |
| `src/lib/game/techniques.ts` | 519-569 | useTechnique() |
| `src/lib/game/qi-system.ts` | Частично | Дублирование логики |

---

## 📋 ПЛАН РЕФАКТОРИНГА

### Фаза 1: Серверный Combat API (Критическая)

**Цель:** Перенести все расчёты урона на сервер

**Новые файлы:**
```
src/lib/game/server/
├── combat-service.ts       # Главный сервис боя
├── damage-calculator.ts    # Калькулятор урона (миграция)
├── technique-service.ts    # Сервис техник
└── types.ts                # Типы
```

**Новые API endpoints:**
```
POST /api/combat/attack     # Атака игрока
POST /api/combat/technique  # Использование техники
POST /api/combat/npc-attack # Атака NPC (от сервера)
```

**WebSocket события:**
```
player:attack    → { targetId, techniqueId, qiInput }
                 ← { damage, newHp, effects }

npc:attack       ← { npcId, targetId, damage }
                 → { playerHp, effects }
```

**Миграция файлов:**
| Откуда | Куда | Действие |
|--------|------|----------|
| `damage-pipeline.ts` | `server/damage-calculator.ts` | Перенести |
| `combat-system.ts` | `server/combat-service.ts` | Перенести |
| `npc-damage-calculator.ts` | `server/damage-calculator.ts` | Интегрировать |

---

### Фаза 2: Серверный AI

**Цель:** Перенести все AI решения на сервер

**Новые файлы:**
```
src/lib/game/server/
├── ai-service.ts           # Главный AI сервис
├── npc-ai-manager.ts       # Менеджер NPC AI
├── spinal-server.ts        # Серверный Spinal адаптер
└── action-processor.ts     # Обработчик действий
```

**Интеграция с WebSocket:**
```
mini-services/game-ws/
├── index.ts               # Entry point
├── ai/
│   ├── npc-processor.ts   # Обработка NPC AI
│   ├── spinal-adapter.ts  # Адаптер Spinal для сервера
│   └── action-broadcaster.ts
└── package.json
```

**WebSocket события:**
```
[Каждый тик на сервере]
AI Loop:
  for npc in active_npcs:
    action = spinal_controller.update(npc, players)
    if action:
      broadcast('npc:action', { npcId, action })
```

**Клиентский NPCSprite:**
```
executeServerAction(action)  # Остается - только визуал
applyServerUpdate(changes)   # Остается - синхронизация HP
takeDamage()                 # УДАЛИТЬ или оставить только визуал
```

---

### Фаза 3: Серверные Техники

**Цель:** Перенести расчёт и применение техник на сервер

**Новая архитектура:**
```
┌──────────────────────────────────────────────────────────────────────────────┐
│                    ПОТОК ИСПОЛЬЗОВАНИЯ ТЕХНИКИ                                │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   1. КЛИЕНТ: Нажатие кнопки техники                                         │
│      └── gameSocket.emit('technique:use', {                                 │
│            techniqueId: 'fireball_1',                                        │
│            targetX: 500,                                                     │
│            targetY: 300,                                                     │
│            qiInput: 50  // Опционально                                       │
│          })                                                                  │
│                                                                              │
│   2. СЕРВЕР: Проверка и расчёт                                              │
│      ├── Проверка владения техникой                                         │
│      ├── Проверка Qi (qiCost)                                               │
│      ├── Расчёт урона через damage-calculator                               │
│      ├── Списание Qi через TruthSystem                                      │
│      └── Возврат результата                                                 │
│                                                                              │
│   3. СЕРВЕР: Отправка результата                                            │
│      └── socket.emit('technique:result', {                                  │
│            success: true,                                                    │
│            damage: 45,                                                       │
│            projectile: { element, speed, ... },                              │
│            currentQi: 150                                                    │
│          })                                                                  │
│                                                                              │
│   4. КЛИЕНТ: Визуализация                                                   │
│      ├── Создание снаряда (ТОЛЬКО визуал)                                   │
│      ├── Анимация полёта                                                    │
│      └── При попадании: НИЧЕГО не рассчитывает                               │
│          └── Ждёт серверный combat:hit                                      │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

---

### Фаза 4: Удаление клиентской логики

**Цель:** Очистить клиент от бизнес-логики

**Удалить из клиента:**
- `TechniqueSlotsManager.calculateDamage()`
- `ProjectileManager.onProjectileHit()` - применение урона
- `NPCSprite.takeDamage()` - изменение HP
- `NPCSprite.updateSpinalAI()` - AI решения
- `LocationScene.updateAI()` - AI обновление

**Оставить на клиенте:**
- Phaser rendering
- Input handling
- Visual effects
- UI updates
- WebSocket event handling

---

## 📁 НОВАЯ СТРУКТУРА ФАЙЛОВ

### Серверная логика

```
src/lib/game/server/
├── index.ts                    # Экспорты
├── types.ts                    # Общие типы
│
├── combat/
│   ├── combat-service.ts       # Главный сервис боя
│   ├── damage-calculator.ts    # Расчёт урона (из damage-pipeline.ts)
│   ├── technique-service.ts    # Сервис техник
│   ├── qi-manager.ts           # Управление Qi
│   └── types.ts                # Типы боя
│
├── ai/
│   ├── ai-service.ts           # Главный AI сервис
│   ├── npc-ai-manager.ts       # Менеджер NPC
│   ├── spinal-server.ts        # Серверный Spinal
│   ├── action-processor.ts     # Обработчик действий
│   └── types.ts                # Типы AI
│
└── sync/
    ├── state-sync.ts           # Синхронизация состояния
    └── broadcast.ts            # Broadcast утилиты
```

### WebSocket сервис

```
mini-services/game-ws/
├── index.ts                    # Entry point + Socket.IO setup
├── package.json
│
├── handlers/
│   ├── player.ts               # player:* события
│   ├── combat.ts               # combat:* события
│   ├── technique.ts            # technique:* события
│   └── world.ts                # world:* события
│
├── managers/
│   ├── connection-manager.ts   # Управление подключениями
│   ├── npc-manager.ts          # Управление NPC
│   └── tick-loop.ts            # Основной цикл
│
└── lib/
    ├── spinal-adapter.ts       # Адаптер Spinal для WS
    └── api-client.ts           # Клиент к основному API
```

### Клиент (только отображение)

```
src/game/
├── scenes/
│   ├── LocationScene.ts        # Упрощён - только визуал
│   └── ...
│
├── objects/
│   ├── NPCSprite.ts            # Упрощён - только визуал
│   └── TechniqueProjectile.ts  # Только визуал снаряда
│
├── services/
│   ├── ProjectileManager.ts    # Упрощён - только визуал
│   └── TechniqueSlotsManager.ts # Упрощён - только UI
│
└── socket/
    ├── game-socket.ts          # WebSocket клиент
    └── event-handlers.ts       # Обработка событий
```

---

## 🔄 МИГРАЦИЯ КОДА

### Что переносим на сервер

| Файл-источник | Файл-назначение | Функции |
|---------------|-----------------|---------|
| `damage-pipeline.ts` | `server/combat/damage-calculator.ts` | `processDamagePipeline`, `calculateLevelSuppression` |
| `combat-system.ts` | `server/combat/combat-service.ts` | `calculateTechniqueDamageFull` |
| `npc-damage-calculator.ts` | `server/combat/damage-calculator.ts` | `calculateDamageFromNPC` |
| `techniques.ts` | `server/combat/technique-service.ts` | `useTechnique`, `calculateTechniqueEffectiveness` |
| `spinal-controller.ts` | `server/ai/spinal-server.ts` | Весь класс |
| `spinal-reflexes.ts` | `server/ai/spinal-server.ts` | Все рефлексы |

### Что остаётся на клиенте

| Файл | Что остаётся |
|------|--------------|
| `TechniqueSlotsManager.ts` | UI слоты, отправка `technique:use` |
| `ProjectileManager.ts` | Создание визуала снаряда, полёт |
| `NPCSprite.ts` | `executeServerAction()`, `applyServerUpdate()` |
| `LocationScene.ts` | Rendering, input handling |

---

## 📊 ЧЕК-ЛИСТ РЕФАКТОРИНГА

### Фаза 1: Combat API
- [ ] Создать `server/combat/damage-calculator.ts`
- [ ] Мигрировать `processDamagePipeline()`
- [ ] Создать API `/api/combat/attack`
- [ ] Обновить `ProjectileManager` - убрать расчёт урона
- [ ] Тест: урон рассчитывается на сервере

### Фаза 2: Techniques
- [ ] Создать `server/combat/technique-service.ts`
- [ ] Мигрировать расчёт урона техник
- [ ] Добавить WebSocket `technique:use`
- [ ] Убрать `calculateDamage()` из клиента
- [ ] Тест: Qi списывается на сервере

### Фаза 3: AI
- [ ] Создать `server/ai/spinal-server.ts`
- [ ] Интегрировать с WebSocket tick loop
- [ ] Убрать `updateSpinalAI()` из NPCSprite
- [ ] Добавить `npc:action` broadcast
- [ ] Тест: NPC управляются сервером

### Фаза 4: Cleanup
- [ ] Удалить дублирующий код
- [ ] Упростить NPCSprite
- [ ] Упростить LocationScene
- [ ] Финальное тестирование

---

## 🎯 ОЖИДАЕМЫЙ РЕЗУЛЬТАТ

### После рефакторинга

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                    ЦЕЛЕВАЯ АРХИТЕКТУРА                                        │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   СЕРВЕР (Единая точка истины)                                              │
│   ┌─────────────────────────────────────────────────────────────────────┐   │
│   │                                                                      │   │
│   │   TruthSystem                                                       │   │
│   │   ├── Хранит ВСЁ состояние игры                                     │   │
│   │   ├── character, npcs, world, time                                  │   │
│   │   └── Единственный источник изменений                               │   │
│   │                                                                      │   │
│   │   CombatService                                                     │   │
│   │   ├── Все расчёты урона                                             │   │
│   │   ├── Все применения эффектов                                       │   │
│   │   └── Валидация действий                                            │   │
│   │                                                                      │   │
│   │   TechniqueService                                                  │   │
│   │   ├── Расчёт урона техник                                           │   │
│   │   ├── Списание Qi                                                   │   │
│   │   └── Проверка владения                                             │   │
│   │                                                                      │   │
│   │   AIService                                                         │   │
│   │   ├── SpinalController на сервере                                   │   │
│   │   ├── Принятие решений для NPC                                      │   │
│   │   └── Tick loop: обновление каждые 1 сек                           │   │
│   │                                                                      │   │
│   └─────────────────────────────────────────────────────────────────────┘   │
│                                  │                                           │
│                                  │ WebSocket                                 │
│                                  ▼                                           │
│   ┌─────────────────────────────────────────────────────────────────────┐   │
│   │                                                                      │   │
│   │   Socket.IO (Порт 3003)                                             │   │
│   │   ├── player:connect, player:move, player:attack                    │   │
│   │   ├── technique:use                                                 │   │
│   │   ├── world:tick, world:sync                                        │   │
│   │   ├── npc:action, npc:update, npc:despawn                           │   │
│   │   └── combat:hit, combat:death                                      │   │
│   │                                                                      │   │
│   └─────────────────────────────────────────────────────────────────────┘   │
│                                  │                                           │
│                                  │ Broadcast                                 │
│                                  ▼                                           │
│   КЛИЕНТ (Только отображение)                                               │
│   ┌─────────────────────────────────────────────────────────────────────┐   │
│   │                                                                      │   │
│   │   Phaser Scenes                                                     │   │
│   │   ├── Rendering (спрайты, анимации)                                 │   │
│   │   ├── Input handling (клавиатура, мышь)                             │   │
│   │   └── Отправка input на сервер                                      │   │
│   │                                                                      │   │
│   │   NPCSprite                                                         │   │
│   │   ├── executeServerAction(action) → визуал                          │   │
│   │   ├── applyServerUpdate(changes) → HP бары                          │   │
│   │   └── БЕЗ takeDamage(), БЕЗ updateSpinalAI()                        │   │
│   │                                                                      │   │
│   │   TechniqueSlotsManager                                             │   │
│   │   ├── UI слоты                                                      │   │
│   │   ├── Отправка technique:use на сервер                              │   │
│   │   └── БЕЗ calculateDamage()                                         │   │
│   │                                                                      │   │
│   └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

---

## 📚 СВЯЗАННЫЕ ДОКУМЕНТЫ

- [ARCHITECTURE.md](./ARCHITECTURE.md) - Текущая архитектура
- [architecture-analysis.md](./architecture-analysis.md) - Анализ архитектуры
- [ARCHITECTURE_future.md](./ARCHITECTURE_future.md) - Будущая архитектура
- [checkpoint_03_25_AI_server_fix.md](./checkpoints/checkpoint_03_25_AI_server_fix.md) - Текущие исправления

---

*Документ создан: 2026-03-25*
*Статус: Основа для планирования рефакторинга*
