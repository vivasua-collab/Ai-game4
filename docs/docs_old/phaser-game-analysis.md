# 📊 АНАЛИЗ: PhaserGame.tsx — Возможности оптимизации

**Дата анализа:** 2026-03-15
**Текущий размер:** 2,798 строк
**Предыдущий размер:** 3,656 строк (-858 строк после Phase 3)

---

## 📁 ТЕКУЩАЯ СТРУКТУРА ФАЙЛА

| Секция | Строки | Размер | Описание |
|--------|--------|--------|----------|
| Импорты и константы | 1-100 | ~100 | Импорты, игровые константы |
| Интерфейсы | 100-130 | ~30 | TrainingTarget, DamageNumber |
| Глобальные переменные | 130-210 | ~80 | React ↔ Phaser bridge |
| Helper: Текстуры | 210-400 | ~190 | createTarget, createTargetTexture, damageTarget |
| Training NPC System | 509-710 | ~200 | loadTrainingNPCs, createTrainingNPCSprite |
| Charging Wrappers | 710-1000 | ~290 | Локальные обёртки |
| executeTechniqueInDirection | 1000-1300 | ~300 | Визуальные эффекты техник |
| GameSceneConfig.create | 1300-2000 | ~700 | Создание сцены, UI |
| GameSceneConfig.update | 2000-2480 | ~480 | Update loop |
| React Component | 2480-2798 | ~320 | React hooks, инициализация |

---

## 🎯 ПЛАН ОПТИМИЗАЦИИ

### Приоритет 1: Высокий (сильное влияние)

#### 1. TrainingTarget System → `src/game/services/training-targets.ts`
**Строки для выноса:** ~200
**Функции:**
- `createTargetTexture()` — создание текстуры чучела
- `createTarget()` — создание объекта чучела
- `updateTargetHpBar()` — обновление HP бара
- `showDamageNumber()` — показ урона
- `damageTarget()` — нанесение урона

**Зависимости:**
- `DAMAGE_COLORS` (константа)
- `TARGET_HITBOX_RADIUS` (константа)
- `globalDamageNumbers` (global state)
- `globalTargets` (global state)

**Сложность:** Низкая (функции независимы)

---

#### 2. TrainingNPC System → `src/game/services/training-npcs.ts`
**Строки для выноса:** ~200
**Функции:**
- `loadTrainingNPCs()` — загрузка NPC с сервера
- `createTrainingNPCSprite()` — создание спрайта NPC
- `updateTrainingNPCHPBar()` — обновление HP бара NPC
- `getLevelColor()` — цвет по уровню
- `getSpeciesIcon()` — иконка по виду

**Зависимости:**
- `WORLD_WIDTH`, `WORLD_HEIGHT` (константы)
- `globalTrainingNPCs` (global state)
- `globalGameScene` (global state)

**Сложность:** Низкая

---

#### 3. Technique Execution → `src/game/services/technique-executor.ts`
**Строки для выноса:** ~300
**Функции:**
- `executeTechniqueInDirection()` — основная функция исполнения техники
- `extractRangeData()` — извлечение данных о дальности

**Зависимости:**
- `METERS_TO_PIXELS` (константа)
- `globalSessionId`, `globalCharacter`, `globalTargets`
- `eventBusClient`
- `checkAttackHit`, `getElementColor` (уже вынесены)
- `damageTarget` (будет в training-targets.ts)

**Сложность:** Средняя (много зависимостей)

---

### Приоритет 2: Средний

#### 4. UI Components → `src/game/services/game-ui.ts`
**Строки для выноса:** ~400
**Функции:**
- `createTopBar()` — верхняя панель
- `createStatusBars()` — HP/Qi бары
- `createChatPanel()` — чат панель
- `createCombatSlots()` — слоты техник
- `createMinimap()` — миникарта
- `updateUIPositions()` — обновление позиций UI

**Сложность:** Средняя (требует рефакторинга GameSceneConfig.create)

---

#### 5. Keyboard Handlers → `src/game/services/keyboard-handlers.ts`
**Строки для выноса:** ~200
**Функции:**
- `setupKeyboardHandlers()` — все обработчики клавиш
- Number keys (1-9, 0, -) для слотов
- ENTER, ESC, I для чата/инвентаря

**Сложность:** Низкая

---

### Приоритет 3: Низкий (требует осторожности)

#### 6. GameSceneConfig.create → Модульная структура
**Проблема:** 700 строк в одном методе
**Решение:** Разбить на подфункции
- `createWorld()` — создание мира
- `createPlayer()` — создание игрока
- `createEnvironment()` — окружение
- `createUI()` — UI элементы
- `setupInput()` — обработка ввода

**Сложность:** Высокая (требует тщательного тестирования)

---

## 📊 ПРОГНОЗ РЕЗУЛЬТАТОВ

| После выноса | Строк в PhaserGame.tsx | Сокращение |
|--------------|------------------------|------------|
| Текущее | 2,798 | — |
| TrainingTarget System | ~2,600 | -200 |
| TrainingNPC System | ~2,400 | -200 |
| Technique Execution | ~2,100 | -300 |
| UI Components | ~1,700 | -400 |
| Keyboard Handlers | ~1,500 | -200 |
| **Итого** | **~1,500** | **-1,300 (-46%)** |

---

## ⚠️ РИСКИ И ОГРАНИЧЕНИЯ

### Архитектурные ограничения:

1. **Глобальные переменные (НЕ ВЫНОСИТЬ)**
   - Это НЕ мусор, а архитектурное решение для React ↔ Phaser bridge
   - `globalSessionId`, `globalCharacter`, `globalTechniques` и т.д.
   - Альтернативы (Context, Plugin) отложены

2. **Зависимости от scene.data**
   - Phaser хранит состояние в `scene.data`
   - Вынесенные функции должны получать scene как параметр

3. **Порядок инициализации**
   - GameSceneConfig должен быть в одном файле
   - Можно выносить только helper functions

### Рекомендуемый порядок выноса:

```
1. TrainingTarget System (простой, ~200 строк)
2. TrainingNPC System (простой, ~200 строк)
3. Technique Execution (средний, ~300 строк)
4. Keyboard Handlers (простой, ~200 строк)
5. UI Components (средний, ~400 строк)
```

---

## 🎯 РЕКОМЕНДАЦИИ

### Мгновенные улучшения (без риска):

1. **Вынести константы** в отдельные файлы:
   - `DAMAGE_COLORS` → `src/game/constants/colors.ts`
   - `TARGET_POSITIONS` → `src/game/constants/positions.ts`

2. **Консолидировать импорты:**
   - Использовать barrel exports из `@/game/services/`

3. **Добавить type-only импорты:**
   - `import type { ... }` для типов

### Среднесрочные улучшения:

1. **Создать GameContext класс:**
   - Инкапсулировать глобальные переменные
   - Упростить передачу состояния

2. **Использовать Phaser Registry:**
   - Вместо scene.data для хранения состояния

---

## 📝 ЗАКЛЮЧЕНИЕ

**Текущий статус:** Файл оптимизирован на 23.5% (с 3,656 до 2,798 строк)

**Потенциал:** Дополнительное сокращение на 46% (до ~1,500 строк)

**Рекомендация:** Выполнить вынос в указанном порядке по приоритетам. Начать с простых модулей (TrainingTarget, TrainingNPC).

---

*Анализ выполнен: 2026-03-15*
