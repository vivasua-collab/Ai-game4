# 🏗️ Архитектура Cultivation World Simulator

> Подробное описание архитектуры с интеграцией Phaser 3.
> Версия: 21 | Год: 2026
> Обновлено: 2026-03-24 16:55 UTC (Time Scaling, исправление инверсии скорости)

---

## 🎮 Режим игры

### Однопользовательская игра

**Текущий статус:** Игра является **однопользовательской**.

- Один игрок управляет одним персонажем
- Все данные хранятся на сервере в SQLite
- Сессия загружается автоматически при запуске

### Возможность мультиплеера (не точно)

Если в будущем будет добавлен мультиплеер:

1. **Статичная скорость тиков**: Все игроки живут в одном времени
2. **Серверное время**: Time speed устанавливается сервером
3. **Синхронизация**: Все клиенты получают одинаковые события

**Архитектурные решения для будущего:**
- Клиент-серверное разделение уже реализовано
- API routes готовы для расширения
- Возможность заменить React-фронтенд без изменения API

---

## 📐 Общая архитектура

### ⚠️ Sandbox-совместимая архитектура (v14)

> **Важно:** Приложение работает в sandboxed iframe (Preview Panel).
> `localStorage` и `sessionStorage` **ЗАБЛОКИРОВАНЫ**.
> Все данные хранятся на сервере через HTTP API.

```
┌─────────────────────────────────────────────────────────────────────┐
│                    CLIENT (Browser - Sandbox)                       │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │                    page.tsx (Main Entry)                    │   │
│  │              Auto-init via /api/game/last-session           │   │
│  └───────────────────────────┬─────────────────────────────────┘   │
│                              │                                      │
│         ┌────────────────────┼────────────────────┐                │
│         ▼                    ▼                    ▼                │
│  ┌─────────────┐      ┌─────────────┐      ┌─────────────┐        │
│  │ PhaserGame  │      │  ChatPanel  │      │ActionButtons│        │
│  │   (2D)      │      │   (Chat)    │      │ (Rest/Stats)│        │
│  └──────┬──────┘      └──────┬──────┘      └──────┬──────┘        │
│         │                    │                    │                │
│         └────────────────────┼────────────────────┘                │
│                              ▼                                      │
│  ┌───────────────────────────────────────────────────────────────┐│
│  │  Zustand Store (game.store.ts) ─ IN-MEMORY ONLY              ││
│  │  ❌ NO localStorage  ❌ NO sessionStorage                    ││
│  │  ✅ Session restored from server on page load                ││
│  └───────────────────────────────────────────────────────────────┘│
│  ┌───────────────────────────────────────────────────────────────┐│
│  │  GameBridge (memory-only)                                    ││
│  │  sessionId, currentLocationId ─ volatile, per-session        ││
│  └───────────────────────────────────────────────────────────────┘│
└────────────────────────────┼────────────────────────────────────────┘
                             │ HTTP (fetch)
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│                        SERVER (Next.js API)                         │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐                 │
│  │/api/game/   │  │/api/rest    │  │/api/        │                 │
│  │start,move   │  │             │  │technique/*  │                 │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘                 │
│         │                │                │                         │
│         └────────────────┼────────────────┘                         │
│                          ▼                                          │
│           ┌──────────────────────────────┐                          │
│           │      TruthSystem             │  ← ПАМЯТЬ ПЕРВИЧНА!     │
│           │  (Active Sessions in Memory) │                          │
│           └──────────────┬───────────────┘                          │
│                          │                                          │
│         ┌────────────────┼────────────────┐                         │
│         ▼                ▼                ▼                         │
│   ┌───────────┐   ┌───────────┐   ┌───────────┐                    │
│   │ Character │   │ Techniques│   │ Inventory │                    │
│   │  (mem)    │   │   (mem)   │   │   (mem)   │                    │
│   └───────────┘   └───────────┘   └───────────┘                    │
│                          │                                          │
│                          ▼ Автосохранение / Критические события     │
│                   ┌─────────────┐                                   │
│                   │   Prisma    │                                   │
│                   │   (SQLite)  │                                   │
│                   └─────────────┘                                   │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 📁 Структура проекта

### `/src/app/api/` - API эндпоинты (42 файла)

#### Игровые API
| Файл | Назначение |
|------|------------|
| `game/start/route.ts` | Создание новой игры |
| `game/state/route.ts` | Получение состояния |
| `game/move/route.ts` | Движение + время + пассивное Ци |
| `game/save/route.ts` | Сохранение игры |
| `game/last-session/route.ts` | **Получение последней сессии (sandbox fix)** |
| `game/end/route.ts` | Завершение сессии |
| `game/event/route.ts` | Event Bus для Phaser |
| `rest/route.ts` | Медитация, отдых, сон |
| `chat/route.ts` | Чат с LLM |
| `map/route.ts` | Карта мира |
| `meditation/route.ts` | Медитация |
| `character/data/route.ts` | Данные персонажа |

#### Техники API
| Файл | Назначение |
|------|------------|
| `technique/use/route.ts` | Использование техники |
| `technique/slot/route.ts` | Назначение в слот (слот 1 = только melee_strike) |
| `techniques/pool/route.ts` | Пул техник при прорыве |

#### Инвентарь API
| Файл | Назначение |
|------|------------|
| `inventory/route.ts` | Получение инвентаря |
| `inventory/use/route.ts` | Использование предмета |
| `inventory/equip/route.ts` | Экипировка предмета |
| `inventory/move/route.ts` | Перемещение предмета |
| `inventory/sync/route.ts` | Синхронизация |
| `inventory/state/route.ts` | Состояние инвентаря |
| `inventory/storage/route.ts` | Хранилище |
| `inventory/add-qi-stone/route.ts` | Добавление Ци-камня |

#### Генераторы API
| Файл | Назначение |
|------|------------|
| `generator/techniques/route.ts` | Генерация техник |
| `generator/equipment/route.ts` | Генерация экипировки |
| `generator/formations/route.ts` | Генерация формаций |
| `generator/npc/route.ts` | Генерация NPC |
| `generator/items/route.ts` | Генерация предметов |

#### Читы API
| Файл | Назначение |
|------|------------|
| `cheats/route.ts` | Основные читы |
| `cheat/level/route.ts` | Изменение уровня |
| `cheat/qi/route.ts` | Изменение Ци |
| `cheat/fatigue/route.ts` | Изменение усталости |
| `cheat/resources/route.ts` | Ресурсы |
| `cheat/generate-technique/route.ts` | Генерация техники |

#### Системные API
| Файл | Назначение |
|------|------------|
| `database/migrate/route.ts` | Миграция БД |
| `database/reset/route.ts` | Сброс БД |
| `llm/status/route.ts` | Статус LLM |
| `llm/route.ts` | LLM запросы |
| `settings/llm/route.ts` | Настройки LLM |
| `system/gpu/route.ts` | Информация о GPU |
| `logs/route.ts` | Логи системы |

### `/src/lib/generator/` - Генераторы (22 файла)

| Файл | Назначение |
|------|------------|
| `technique-generator.ts` | **Генератор техник** (combat, defense, curse, poison) |
| `equipment-generator-v2.ts` | **Генератор экипировки v2 (Grade система)** |
| `grade-system.ts` | **Система грейдов (damaged→transcendent)** |
| `grade-selector.ts` | Выбор грейда по распределению |
| `grade-validator.ts` | Валидация грейдов |
| `materials-registry.ts` | **Реестр материалов (T1-T5)** |
| `durability-system.ts` | **Система прочности** |
| `preset-storage.ts` | **Хранилище пресетов** (JSON файлы) |
| `id-config.ts` | **Конфигурация ID префиксов (26 префиксов)** |
| `technique-config.ts` | Конфигурация типов техник |
| `weapon-generator.ts` | Генератор оружия (legacy) |
| `weapon-config.ts` | Конфигурация типов оружия |
| `weapon-categories.ts` | Категории оружия (one_handed, two_handed, etc.) |
| `armor-generator.ts` | Генератор брони (legacy) |
| `accessory-generator.ts` | Генератор аксессуаров |
| `consumable-generator.ts` | Генератор расходников |
| `charger-generator.ts` | Генератор заряжаемых предметов |
| `qi-stone-generator.ts` | Генератор Ци-камней |
| `formation-generator.ts` | Генератор формаций |
| `npc-generator.ts` | Генератор NPC |
| `name-generator.ts` | Генератор имён |
| `soul-mapping.ts` | **Маппинг Species → SoulEntity** |
| `base-item-generator.ts` | Базовый класс генератора |
| `generated-objects-loader.ts` | Загрузчик сгенерированных объектов |
| `item-config.ts` | Конфигурация предметов |
| `lore-formulas.ts` | Лорные формулы |

### `/src/services/` - Сервисы (14 файлов)

| Файл | Назначение |
|------|------------|
| `game.service.ts` | Основной игровой сервис |
| `game-bridge.service.ts` | **Мост Phaser ↔ API (memory-only, v2.0)** |
| `game-client.service.ts` | Клиентские операции |
| `character.service.ts` | Операции с персонажем |
| `character-data.service.ts` | Данные персонажа |
| `inventory.service.ts` | Операции с инвентарём |
| `inventory-sync.service.ts` | Синхронизация инвентаря |
| `technique-pool.service.ts` | **Пул техник при прорыве** |
| `world.service.ts` | Управление миром |
| `map.service.ts` | Карта мира |
| `session.service.ts` | Управление сессиями |
| `time-tick.service.ts` | **Единый обработчик тиков времени** |
| `cheats.service.ts` | Сервис читов |

### `/src/lib/game/` - Игровая логика (40+ файлов)

| Файл | Назначение |
|------|------------|
| `truth-system.ts` | **Система Истинности (singleton, память первична)** |
| `qi-system.ts` | Медитация, прорыв (сервер) |
| `qi-shared.ts` | **Единое место расчётов Ци** |
| `qi-tick-processor.ts` | **Batch обработка эффектов Ци при тиках (NEW)** |
| `conductivity-system.ts` | **Единая формула проводимости** |
| `techniques.ts` | Активные техники (CombatSubtype, эффективность) |
| `combat-system.ts` | **Боевая система (1258 строк)** |
| `npc-damage-calculator.ts` | **Расчёт урона NPC→Player** |
| `fatigue-system.ts` | Система усталости |
| `time-system.ts` | 🔄 Система тиков (к миграции в time.store) |
| `time-db.ts` | 🔄 Продвижение времени в БД (к деактивации) |
| `time-scaling.ts` | **Масштабирование скорости и кулдаунов (NEW)** |
| `action-speeds.ts` | **Профили активностей (NEW)** |
| `activity-manager.ts` | **Менеджер авто-переключения времени (NEW)** |
| `body-system.ts` | Система тела (Kenshi-style) |
| `limb-attachment.ts` | Приживление конечностей |
| `world-coordinates.ts` | 3D координаты мира |
| `constants.ts` | Все константы игры |
| `stat-threshold.ts` | **Система порогов развития** |
| `stat-development.ts` | **Виртуальная дельта развития** |
| `training-system.ts` | **Система тренировки характеристик** |
| `stat-simulation.ts` | **Симуляция развития** |
| `npc-ai.ts` | **ИИ поведение NPC** |
| `npc-collision.ts` | **Система коллизий NPC** |
| `wave-manager.ts` | **Менеджер волн (Training Ground)** |
| `session-npc-manager.ts` | **Менеджер сессионных NPC** |

### `/src/game/` - Phaser 3 движок (NEW)

| Файл | Назначение |
|------|------------|
| `scenes/LocationScene.ts` | **Основная сцена локации с боевой системой** |
| `scenes/TrainingScene.ts` | Сцена тренировки |
| `scenes/WorldScene.ts` | Сцена карты мира |
| `objects/NPCSprite.ts` | **NPC спрайт с Arcade Physics** |
| `objects/TechniqueProjectile.ts` | **Снаряды техник (projectile, beam, aoe)** |
| `groups/NPCGroup.ts` | **Группа NPC с коллизиями** |
| `services/ProjectileManager.ts` | **Менеджер снарядов (NEW)** |
| `services/combat-utils.ts` | **Утилиты боя (checkAttackHit, getElementColor)** |
| `services/sprite-loader.ts` | Загрузчик спрайтов |
| `constants.ts` | Константы Phaser (DEPTHS, SPEEDS) |

### `/src/components/game/` - Игровые компоненты

| Файл | Назначение |
|------|------------|
| `PhaserGame.tsx` | 2D игра на Phaser 3 |
| `ChatPanel.tsx` | Чат + статус бар |
| `RestDialog.tsx` | Диалог (Медитация, Отдых, Сон) |
| `StatusDialog.tsx` | Полный статус персонажа |
| `TechniquesDialog.tsx` | **Просмотр техник + назначение в слоты** |
| `InventoryDialog.tsx` | Инвентарь |
| `InventoryPanel.tsx` | Панель инвентаря |
| `BodyDoll.tsx` | Кукла тела |
| `BodyDollEditor.tsx` | Редактор тела |
| `ActionButtons.tsx` | Кнопки быстрых действий |
| `CheatMenuDialog.tsx` | Диалог читов |

### `/src/data/presets/` - Унифицированные пресеты

| Файл | Назначение |
|------|------------|
| `base-preset.ts` | Базовый интерфейс BasePreset |
| `technique-presets.ts` | Пресеты техник |
| `skill-presets.ts` | Пресеты навыков культивации |
| `formation-presets.ts` | Пресеты формаций |
| `item-presets.ts` | Пресеты предметов |
| `character-presets.ts` | Стартовые персонажи |
| `role-presets.ts` | Пресеты ролей |
| `species-presets.ts` | Пресеты видов |
| `personality-presets.ts` | Пресеты личностей |
| `index.ts` | Единый экспорт + утилиты |

---

## ⚡ Система Истинности (Truth System)

> **Версия:** 1.0 | **Дата:** 2026-02-28

### Принцип

```
┌─────────────────────────────────────────────────────────────────┐
│                   ИЕРАРХИЯ ИСТОЧНИКОВ ИСТИНЫ                    │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│   1. ПАМЯТЬ (TruthSystem) ────────────────── ПЕРВИЧНЫЙ         │
│      │                                                          │
│      ├─ Активная сессия                                         │
│      ├─ Все расчёты происходят здесь                            │
│      └─ Мгновенный доступ к данным                              │
│                                                                 │
│   2. БД (Prisma/SQLite) ─────────────────── ВТОРИЧНЫЙ          │
│      │                                                          │
│      ├─ Загрузка сессии при старте                              │
│      ├─ Сохранение при смене локации                            │
│      ├─ Немедленное сохранение критических данных               │
│      └─ Периодическое автосохранение                            │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Жизненный цикл сессии

```
START SESSION                    ACTIVE SESSION                   END SESSION
    │                                  │                               │
    ▼                                  ▼                               ▼
┌─────────────┐                 ┌─────────────┐               ┌─────────────┐
│ Загрузка    │                 │ Расчёты в   │               │ Финальное   │
│ из БД в     │ ───────────────▶│ памяти      │──────────────▶│ сохранение  │
│ память      │                 │             │               │ в БД        │
└─────────────┘                 └──────┬──────┘               └─────────────┘
                                       │
                    ┌──────────────────┼──────────────────┐
                    ▼                  ▼                  ▼
              ┌───────────┐      ┌───────────┐      ┌───────────┐
              │Автосохран.│      │ Критич.   │      │ Смена     │
              │ (1 мин)   │      │ события   │      │ локации   │
              │           │      │ (техники) │      │           │
              └───────────┘      └───────────┘      └───────────┘
                    │                  │                  │
                    └──────────────────┴──────────────────┘
                                       │
                                       ▼
                              ┌─────────────┐
                              │ Сохранение  │
                              │ в БД        │
                              └─────────────┘
```

### Критические события (немедленное сохранение)

| Событие | Действие |
|---------|----------|
| Получение новой техники | `db.characterTechnique.create()` + memory |
| Получение нового предмета | `db.inventoryItem.create()` + memory |
| Смена локации | Сохранение текущего состояния → загрузка новой |
| Прорыв уровня | `applyBreakthrough()` - полное сохранение персонажа |
| Медитация на проводимость | `updateConductivity()` - сохранение в БД |
| Завершение боя | Сохранение состояния персонажа |

### Интеграция с API routes (выполнено 2026-02-28)

| API Route | Статус | Методы TruthSystem |
|-----------|--------|-------------------|
| `/api/game/start` | ✅ | `loadSession()` после создания |
| `/api/game/state` | ✅ | `getSessionState()` → fallback БД |
| `/api/game/save` | ✅ | POST: `saveToDatabase()`, `quickSave()` |
| `/api/rest` | ✅ | `updateCharacter()`, `advanceTime()` |
| `/api/meditation` | ✅ | `addQi()`, `applyBreakthrough()`, `updateConductivity()` |
| `/api/technique/use` | ✅ | `spendQi()`, `updateFatigue()` |

### API TruthSystem

> 📖 **Полный список функций:** [docs/FUNCTIONS.md](./FUNCTIONS.md) — раздел «Система Истинности»

### Файлы

| Файл | Назначение |
|------|------------|
| `src/lib/game/truth-system.ts` | Ядро системы, singleton управления сессиями |

---

## 🔄 Поток запуска (Server-only Storage)

> **Версия:** 2.0 | **Дата:** 2026-03-11
> Причина изменения: sandbox iframe блокирует localStorage/sessionStorage

```
┌──────────────────────────────────────────────────────────────┐
│                        Page Load                              │
└──────────────────────────┬───────────────────────────────────┘
                           │
                           ▼
┌──────────────────────────────────────────────────────────────┐
│         fetch('/api/game/last-session')                      │
│         → Server returns last updated session                │
└──────────────────────────┬───────────────────────────────────┘
                           │
           ┌───────────────┴───────────────┐
           ▼                               ▼
┌─────────────────────┐         ┌─────────────────────┐
│   Session Found     │         │   No Session        │
│   (server)          │         │   (server null)     │
│                     │         │                     │
│   loadGame(id)      │         │   startGame(1)      │
│         │           │         │   (variant 1: sect) │
│         ▼           │         │         │           │
│   Load to Zustand   │         │         ▼           │
│   (memory only)     │         │   Create Character  │
│         │           │         │   Create Session    │
│         ▼           │         │   Create Location   │
│   2D Mode Active    │         │   Create Techniques │
│                     │         │         │           │
│                     │         │         ▼           │
│                     │         │   Load to Zustand   │
│                     │         │   (memory only)     │
│                     │         │         │           │
│                     │         │         ▼           │
│                     │         │   2D Mode Active    │
└─────────────────────┴─────────┴─────────────────────┘
                           │
                           ▼
               ┌─────────────────────┐
               │   2D Mode Active    │
               │   - PhaserGame      │
               │   - ChatPanel       │
               │   - ActionButtons   │
               │   - Zustand Store   │
               │     (memory only)   │
               └─────────────────────┘
```

### Ключевые изменения (v14)

| Было | Стало |
|------|-------|
| `localStorage.getItem('session_id')` | `fetch('/api/game/last-session')` |
| `sessionStorage.sessionId` | `GameBridge.sessionId` (memory) |
| `localStorage.saves` | `fetch('/api/game/save')` |
| Zustand + localStorage sync | Zustand (memory only) |

---

## 📁 Структура модулей

### `/src/app/` - Next.js App Router

| Файл | Назначение |
|------|------------|
| `page.tsx` | Главная страница, автозапуск игры |
| `layout.tsx` | Корневой layout |
| `api/game/start/route.ts` | Создание новой игры |
| `api/game/state/route.ts` | Получение состояния |
| `api/game/move/route.ts` | Движение + время + пассивное восстановление Ци |
| `api/rest/route.ts` | Медитация, отдых, сон |
| `api/technique/use/route.ts` | Использование техники |
| `api/chat/route.ts` | Чат с LLM |

### `/src/components/game/` - Игровые компоненты

| Файл | Назначение |
|------|------------|
| `PhaserGame.tsx` | 2D игра на Phaser 3 |
| `ChatPanel.tsx` | Чат + статус бар |
| `RestDialog.tsx` | Единый диалог (Медитация, Отдых, Сон) |
| `StatusDialog.tsx` | Полный статус персонажа |
| `TechniquesDialog.tsx` | Просмотр и использование техник |
| `ActionButtons.tsx` | Кнопки быстрых действий |

### `/src/data/presets/` - Унифицированные пресеты

| Файл | Назначение |
|------|------------|
| `base-preset.ts` | Базовый интерфейс BasePreset |
| `technique-presets.ts` | Пресеты техник (13 шт) |
| `skill-presets.ts` | Пресеты навыков культивации (9 шт) |
| `formation-presets.ts` | Пресеты формаций (8 шт) |
| `item-presets.ts` | Пресеты предметов (15 шт) |
| `character-presets.ts` | Стартовые персонажи (6 шт) |
| `index.ts` | Единый экспорт + утилиты |

### `/src/lib/game/` - Игровая логика

| Файл | Назначение |
|------|------------|
| `constants.ts` | Все константы игры |
| `truth-system.ts` | **Система Истинности (singleton, память первична)** |
| `qi-system.ts` | Медитация, прорыв (сервер) |
| `qi-shared.ts` | Расчёты Ци (клиент/сервер) |
| `fatigue-system.ts` | Система усталости |
| `techniques.ts` | Активные техники |
| `time-system.ts` | Система тиков времени |
| `time-db.ts` | Продвижение времени в БД |
| `world-coordinates.ts` | 3D координаты мира |

### `/src/stores/` - Zustand хранилища

| Файл | Назначение |
|------|------------|
| `game.store.ts` | **Глобальное состояние (IN-MEMORY ONLY)** |
| `time.store.ts` | **Система времени (WorldTime, скорости, тики) (NEW)** |

**⚠️ Важно:** Zustand store работает только в памяти!
- ❌ НЕ синхронизируется с localStorage
- ✅ Загружается с сервера при инициализации
- ✅ Переживает перезагрузку страницы через `/api/game/last-session`

**Экспортируемые селекторы game.store:**
- `useGameCharacter()` - данные персонажа
- `useGameLocation()` - текущая локация
- `useGameMessages()` - сообщения чата
- `useGameLoading()` - состояние загрузки
- `useGameActions()` - все действия (loadState, startGame, etc.)
- `useGameSessionId()` - ID текущей сессии

**Экспортируемые селекторы time.store:**
- `useTimePaused()` - пауза
- `useTimeRunning()` - запущен ли таймер
- `useTickCount()` - счётчик тиков
- `useTimeSpeed()` - текущая скорость
- `useGameTime()` - игровое время (WorldTime)
- `useFormattedGameTime()` - отформатированное время

---

## 🎮 Phaser 3 интеграция

### Конфигурация

> 📖 **Примеры кода:** [ARCHITECTURE_code_base.md](./ARCHITECTURE_code_base.md#phaser-config)

### SSR совместимость

> 📖 **Примеры кода:** [ARCHITECTURE_code_base.md](./ARCHITECTURE_code_base.md#phaser-config)

### Генерация текстур (без ассетов)

> 📖 **Примеры кода:** [ARCHITECTURE_code_base.md](./ARCHITECTURE_code_base.md#phaser-textures)

📖 **Подробности:** [docs/PHASER_STACK.md](./PHASER_STACK.md)

---

## 🔌 API Эндпоинты

### Игровые

| Эндпоинт | Метод | Описание |
|----------|-------|----------|
| `/api/game/start` | POST | Создать новую игру |
| `/api/game/state` | GET | Получить состояние |
| `/api/game/move` | POST | Движение + время + пассивное Ци |
| `/api/rest` | POST | Медитация, отдых, сон |
| `/api/technique/use` | POST | Использовать технику |
| `/api/chat` | POST | Действие + LLM ответ |

### Системные

| Эндпоинт | Метод | Описание |
|----------|-------|----------|
| `/api/database/migrate` | GET/POST | Статус/миграция БД |
| `/api/llm/status` | GET | Статус LLM провайдеров |
| `/api/settings/llm` | GET/POST | Настройки LLM |

---

## 🧩 Система пресетов

### Категории

| Категория | Описание | Цвет UI |
|-----------|----------|---------|
| basic | Базовые пресеты | text-gray-400 |
| advanced | Продвинутые | text-blue-400 |
| master | Мастерские | text-purple-400 |
| legendary | Легендарные | text-amber-400 |

> 📖 **Полный список функций:** [docs/FUNCTIONS.md](./FUNCTIONS.md) — раздел «Утилиты пресетов»

---

## 📊 Диаграмма компонентов

```
page.tsx
    │
    ├── PhaserGame.tsx
    │       │
    │       └── Phaser 3 (dynamic import)
    │               │
    │               ├── GameScene (movement)
    │               ├── Minimap
    │               └── UI Overlay
    │
    ├── ChatPanel.tsx
    │       │
    │       ├── CompactStatusBar
    │       ├── MessageList
    │       └── InputField
    │
    └── ActionButtons.tsx
            │
            ├── StatusDialog.tsx
            │       └── 4 вкладки: Характеристики, Культивация, Состояние, Время
            │
            ├── RestDialog.tsx
            │       └── 3 вкладки: Медитация, Отдых, Сон
            │
            └── TechniquesDialog.tsx
                    └── Группировка по типу, кнопка использования
```

---

## 🚀 Расширение системы

### Добавление новой кнопки действия

1. Добавить кнопку в `ActionButtons.tsx`
2. Создать диалог в `components/game/NewDialog.tsx`
3. Создать API эндпоинт если нужен расчёт

### Добавление нового пресета

1. Выбрать файл: technique/skill/formation/item/character-presets.ts
2. Добавить пресет с уникальным ID
3. Указать category, rarity, requirements

### Добавление нового API

1. Создать `src/app/api/new-action/route.ts`
2. Добавить Zod валидацию
3. Вернуть `character` для синхронизации

---

## 🚌 Event Bus (Шина событий)

> **Версия:** 3.0 | **Дата:** 2026-03-01

### Назначение

Event Bus используется **ТОЛЬКО** для связи между:
- **Phaser Engine (клиент)** ↔ **Server (Truth System)**

React компоненты взаимодействуют между собой напрямую через API вызовы!

### Архитектура

```
┌─────────────────────────────────────────────────────────────────┐
│                        PHASER ENGINE                            │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │ 1. Проверка Qi локально (быстрая проверка UI)             │  │
│  │ 2. Отправка technique:use через Event Bus                 │  │
│  │ 3. Получение ответа (canUse, damageMultiplier, currentQi) │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────┬───────────────────────────────────┘
                              │ HTTP POST /api/game/event
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                        SERVER (Event Bus)                       │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │ handlers/combat.ts → handleTechniqueUse()                 │  │
│  │   ├─ Поиск техники в БД (technique.qiCost)                │  │
│  │   ├─ Проверка владения техникой                           │  │
│  │   ├─ Проверка Qi через TruthSystem                        │  │
│  │   ├─ Списание Qi: TruthSystem.spendQi()                   │  │
│  │   ├─ Расчёт бонусов урона (mastery, conductivity)         │  │
│  │   └─ Возврат: canUse, damageMultiplier, currentQi         │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

### Поток использования техники

```
┌──────────────────────────────────────────────────────────────────┐
│                     ИСПОЛЬЗОВАНИЕ ТЕХНИКИ                        │
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│   1. ДВИЖОК (Phaser)                                             │
│      │                                                           │
│      ├─ Проверка: currentQi >= qiCost (локально)                 │
│      │                                                           │
│      └─ Event: technique:use { techniqueId, position }           │
│                                                                  │
│   2. EVENT BUS HANDLER (combat.ts)                               │
│      │                                                           │
│      ├─ db.technique.findUnique(techniqueId)                     │
│      │   └─ Получаем qiCost из БД (НЕ хардкод!)                  │
│      │                                                           │
│      ├─ TruthSystem.spendQi(sessionId, qiCost)                   │
│      │   └─ ЕДИНСТВЕННОЕ место списания Ци                       │
│      │                                                           │
│      ├─ Расчёт damageMultiplier                                  │
│      │   ├─ masteryBonus (до +50% при 100% мастерства)           │
│      │   └─ conductivityBonus (проводимость Ци)                  │
│      │                                                           │
│      └─ Response: { canUse, damageMultiplier, currentQi }        │
│                                                                  │
│   3. ДВИЖОК (получение ответа)                                   │
│      │                                                           │
│      ├─ canUse = true → наносим урон                             │
│      │   └─ finalDamage = baseDamage * damageMultiplier          │
│      │                                                           │
│      └─ canUse = false → показываем ошибку                       │
│          └─ "Недостаточно Ци" / "Техника не найдена"             │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

### Важные правила

| Правило | Причина |
|---------|---------|
| **Qi списывается ОДИН раз** | Только в `TruthSystem.spendQi()` |
| **qiCost из БД** | Не хардкодить значения! |
| **changes.currentQi** | Только для информации клиенту |
| **Event Bus ≠ React** | React компоненты → прямые API |

### Файлы Event Bus

| Файл | Назначение |
|------|------------|
| `src/lib/game/event-bus/index.ts` | Главный модуль шины |
| `src/lib/game/event-bus/handlers/combat.ts` | Обработка техник |
| `src/lib/game/event-bus/processor.ts` | Процессор событий |
| `src/lib/game/skeleton/event-processor.ts` | Интеграция с TruthSystem |
| `src/lib/game/skeleton/combat-processor.ts` | Боевая логика |

---

## 🐛 Исправленные баги (2026-03-01)

### Двойное списание Ци

**Проблема:** При использовании техники Ци списывалась дважды:
1. `combat-processor.ts` → `TruthSystem.spendQi()` ✅
2. `event-processor.ts` → применял `changes.currentQi` повторно ❌

**Решение:** В `event-processor.ts` убрано повторное применение `changes.currentQi` к Ци.

```typescript
// event-processor.ts (ИСПРАВЛЕНО)
// ВАЖНО: combat processor уже списывает Ци напрямую через TruthSystem.spendQi()
// Поэтому здесь НЕ нужно повторно применять changes.currentQi к Ци!
// Только обновляем усталость и здоровье
```

### Hardcoded Qi Cost

**Проблема:** В `handlers/combat.ts` стоимость Ци была захардкожена (10, 20).

**Решение:** qiCost теперь читается из базы данных:

```typescript
// handlers/combat.ts (ИСПРАВЛЕНО)
const technique = await db.technique.findUnique({ where: { id: techniqueId } });
const qiCost = technique.qiCost ?? 0;
```

### Qi Stones не добавлялись

**Проблема:** CheatMenuContent вызывал несуществующий API маршрут.

**Решение:** Добавлен `add_qi_stone` в `cheats.service.ts`, CheatMenuContent использует `/api/cheats`.

---

## 🏥 Система тела (Kenshi-style) — Event Bus

> **Версия:** 1.0 | **Дата:** 2026-03-01

### События тела через Event Bus

| Событие | Описание | Параметры |
|---------|----------|-----------|
| `body:damage` | Нанесение урона части тела | `partId`, `damage`, `damageType`, `source`, `penetration` |
| `body:heal` | Лечение части тела | `partId?`, `amount`, `source` |
| `body:attach_limb` | Приживление конечности | `partType`, `partId`, `donorCultivationLevel`, `donorCultivationSubLevel` |
| `body:regenerate` | Регенерация конечности | `partId`, `useFormation` |
| `body:update` | Обновление состояния тела | `bodyState`, `changedParts` |

### Поток повреждения части тела

```
┌──────────────────────────────────────────────────────────────────┐
│                     ПОВРЕЖДЕНИЕ ЧАСТИ ТЕЛА                       │
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│   1. ДВИЖОК (Phaser)                                             │
│      │                                                           │
│      └─ Event: body:damage { partId, damage, damageType }        │
│                                                                  │
│   2. EVENT BUS HANDLER (handlers/body.ts)                        │
│      │                                                           │
│      ├─ Загрузка bodyState из БД                                 │
│      │   └─ Десериализация или создание нового тела              │
│      │                                                           │
│      ├─ applyDamageToLimb(part, damage, { penetration })         │
│      │   ├─ Функциональная HP уменьшается первой                 │
│      │   ├─ Структурная HP поглощает остаток                     │
│      │   └─ Проверка отрубания (structuralHP <= 0)               │
│      │                                                           │
│      ├─ Обновление статуса части                                 │
│      │   └─ getLimbStatus() → healthy/damaged/crippled/severed   │
│      │                                                           │
│      ├─ Сохранение bodyState в БД                                │
│      │                                                           │
│      └─ Response: { severed, fatal, newStatus }                  │
│                                                                  │
│   3. ДВИЖОК (получение ответа)                                   │
│      │                                                           │
│      ├─ visual:update_body_part → обновление HP баров            │
│      ├─ visual:show_notification → "Отрублено: Левая рука!"      │
│      └─ game:death → если fatal = true                          │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

### Экипировка через Event Bus

| Событие | Описание | Параметры |
|---------|----------|-----------|
| `inventory:equip_item` | Экипировать предмет | `itemId`, `slotId` |
| `inventory:unequip_item` | Снять предмет | `slotId` |
| `inventory:drop_item` | Выбросить предмет | `itemId`, `quantity`, `position` |

### Поток экипировки

```
┌──────────────────────────────────────────────────────────────────┐
│                     ЭКИПИРОВКА ПРЕДМЕТА                          │
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│   1. ДВИЖОК (Phaser)                                             │
│      │                                                           │
│      └─ Event: inventory:equip_item { itemId, slotId }           │
│                                                                  │
│   2. EVENT BUS HANDLER (handlers/inventory.ts)                   │
│      │                                                           │
│      ├─ inventoryService.equipItem(characterId, itemId, slotId)  │
│      │   ├─ Проверка совместимости слота                         │
│      │   ├─ Снятие текущего предмета в слоте                     │
│      │   └─ Создание записи в Equipment таблице                  │
│      │                                                           │
│      ├─ truthSystem.updateInventory() → синхронизация памяти     │
│      │                                                           │
│      └─ Response: { equipped: { slotId, itemId } }               │
│                                                                  │
│   3. ДВИЖОК (получение ответа)                                   │
│      │                                                           │
│      └─ visual:equipment_changed → обновление UI экипировки     │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

### Файлы

| Файл | Назначение |
|------|------------|
| `src/lib/game/events/game-events.ts` | Типы событий (BodyEvent, InventoryEvent) |
| `src/lib/game/event-bus/handlers/body.ts` | Обработчик событий тела |
| `src/lib/game/event-bus/handlers/inventory.ts` | Обработчик событий инвентаря |
| `src/lib/game/body-system.ts` | Логика повреждений и регенерации |
| `src/lib/game/limb-attachment.ts` | Приживление и регенерация конечностей |
| `src/components/game/BodyDoll.tsx` | UI компонент куклы тела |

---

## 🔒 Sandbox ограничения (2026-03-11)

> **Версия:** 14 | **Критическое изменение архитектуры**

### Проблема

Приложение работает в **sandboxed iframe** (Preview Panel) с ограничениями:

```
┌─────────────────────────────────────────────────────────────────┐
│                    SANDBOX RESTRICTIONS                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│   ❌ localStorage     → SecurityError: Forbidden               │
│   ❌ sessionStorage   → SecurityError: Forbidden               │
│   ❌ IndexedDB        → SecurityError (вероятно)              │
│   ❌ document.cookie  → SecurityError                          │
│                                                                 │
│   ✅ fetch()          → Работает                               │
│   ✅ WebSocket        → Работает                               │
│   ✅ Web Workers      → Работает                               │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Решение

Полный переход на **Server-only Storage**:

| Компонент | Решение |
|-----------|---------|
| Session ID | `/api/game/last-session` |
| Saves List | `/api/game/save` (GET) |
| Settings | Server-side (Prisma) |
| Game State | TruthSystem (memory) + Prisma (persist) |

### Изменённые файлы (v14)

| Файл | Изменение |
|------|-----------|
| `src/app/page.tsx` | Удалён localStorage, используется `/api/game/last-session` |
| `src/components/game/GameMenuDialog.tsx` | Серверный список сохранений |
| `src/services/game-bridge.service.ts` | Memory-only (v2.0) |
| `src/game/scenes/WorldScene.ts` | Удалён localStorage fallback |
| `src/game/scenes/LocationScene.ts` | Удалён localStorage |
| `src/app/api/game/last-session/route.ts` | **НОВЫЙ** API для получения последней сессии |

### API: /api/game/last-session

```typescript
// GET /api/game/last-session
// Возвращает последнюю обновлённую сессию

// Response:
{
  success: true,
  session: {
    id: "clxxxx...",
    worldName: "Мир секты...",
    worldDay: 1,
    updatedAt: "2026-03-11T11:31:45.197Z",
    character: {
      id: "...",
      name: "Путник",
      cultivationLevel: 1,
      currentQi: 0,
      health: 100
    }
  }
}
```

### Паттерн инициализации (v14)

> 📖 **Пример кода:** [docs/FUNCTIONS.md](./FUNCTIONS.md) — раздел «API Эндпоинты»

См. `/api/game/last-session` для получения последней сессии с сервера.

---

## 📊 История версий

| Версия | Дата | Изменения |
|--------|------|-----------|
| 14 | 2026-03-11 | Sandbox fix: удалён localStorage/sessionStorage |
| 13 | 2026-03-01 | Event Bus v3, система тела Kenshi-style |
| 12 | 2026-03-01 | Инвентарь через Event Bus |
| 11 | 2026-02-28 | TruthSystem интеграция |
| 10 | 2026-02-15 | Phaser 3 интеграция |

---

*Архитектура актуальна для версии 14 (2026-03-11)*

---

## 📈 Система развития характеристик (Stat Development)

> **Версия:** 1.0 | **Дата:** 2026-03-14
> **Источник:** docs/body-development-analysis.md, docs/stat-threshold-system.md

### Концепция

```
┌─────────────────────────────────────────────────────────────────┐
│                    СИСТЕМА РАЗВИТИЯ ТЕЛА                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│   1. ВИРТУАЛЬНАЯ ДЕЛЬТА                                         │
│      │                                                          │
│      ├─ Временное накопление прогресса                          │
│      ├─ Источники: бой, тренировка, труд, медитация             │
│      └─ Прирост за действие: 0.001                              │
│                                                                 │
│   2. ПОРОГИ РАЗВИТИЯ                                            │
│      │                                                          │
│      ├─ Чем выше стат → больше дельты для повышения             │
│      ├─ Формула: threshold = floor(currentStat / 10)            │
│      └─ Минимум: 1.0                                            │
│                                                                 │
│   3. ЗАКРЕПЛЕНИЕ ПРИ СНЕ                                        │
│      │                                                          │
│      ├─ Конвертация виртуальной дельты в постоянные статы       │
│      ├─ Кап: +0.20 за 8 часов сна                               │
│      └─ Минимум: 4 часа для закрепления                         │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Ключевые параметры

| Параметр | Значение |
|----------|----------|
| Прирост за действие | 0.001 |
| Кап закрепления за сон (8ч) | +0.20 |
| Минимальный сон для закрепления | 4 часа |
| Формула порога | `floor(stat/10)`, min 1.0 |
| Множитель культивации | НЕТ (тело независимо) |

### Достижимость

| Период | Достижимый стат |
|--------|-----------------|
| 1000 дней | ~55 |
| 3000 дней | ~80 |
| 10000 дней | ~125 |

### Файлы системы

| Файл | Назначение |
|------|------------|
| `src/types/stat-development.ts` | Типы и интерфейсы |
| `src/lib/game/stat-threshold.ts` | Система порогов развития |
| `src/lib/game/stat-development.ts` | Виртуальная дельта + закрепление |
| `src/lib/game/training-system.ts` | Система тренировки |
| `src/lib/game/stat-simulation.ts` | Симуляция развития |
| `src/lib/game/constants.ts` | STAT_DEVELOPMENT_CONSTANTS |

### Типы тренировок

| Тип | Физическая/Ментальная | Множитель дельты | Риск |
|-----|----------------------|------------------|------|
| Классическая | 50% / 50% | ×1.0 | Низкий |
| Фокусная | 70% / 30% | ×1.2 | Средний |
| Экстремальная | 95% / 5% | ×1.5 | Высокий |

### API системы

> 📖 **Полный список функций:** [docs/FUNCTIONS.md](./FUNCTIONS.md) — раздел «Система развития характеристик»

### Интеграция с боем

> 📖 **Подробнее:** [docs/FUNCTIONS.md](./FUNCTIONS.md) — раздел «Боевая система»

---

## ⏰ Система Time Scaling (планируется)

> **Версия:** 1.0 | **Дата:** 2026-03-24

### Принцип работы

Time Scaling — система масштабирования скорости игровых процессов в зависимости от скорости времени.

**Фундаментальное правило:**
```
1 TICK = 1 СЕКУНДА РЕАЛЬНОГО ВРЕМЕНИ (FIXED)
Переменная: minutesPerTick = игровое время за тик
```

### Масштабирование скорости

| Speed | minutesPerTick | Scaling Factor | Восприятие |
|-------|----------------|----------------|------------|
| `superSuperSlow` | 0.25 | 20.0 | Движение быстрее, бой детальный |
| `superSlow` | 0.5 | 10.0 | Замедленно |
| `slow` | 1 | 5.0 | Медленно |
| `normal` | 5 | 1.0 | Базовая скорость |
| `fast` | 15 | 0.33 | Быстро (путешествие) |
| `ultra` | 60 | 0.083 | Очень быстро (медитация) |

### Автоматическое переключение

| Активность | Auto-switch | Скорость |
|------------|-------------|----------|
| Бой | ✅ | `superSuperSlow` |
| Путешествие | ✅ | `fast` |
| Медитация | ✅ | `ultra` |
| Отдых | ✅ | `ultra` |
| Исследование | ❌ | Ручная |

### Формулы

```typescript
// Скорость движения
scaledSpeed = baseSpeed * (5 / minutesPerTick)

// Кулдаун
scaledCooldown = baseGameMinutes / (5 / minutesPerTick)

// Игровые минуты → реальные ms
realMs = (gameMinutes / minutesPerTick) * 1000
```

### Новые модули (планируется)

| Файл | Назначение |
|------|------------|
| `src/lib/game/time-scaling.ts` | Функции масштабирования |
| `src/lib/game/action-speeds.ts` | Профили активностей |
| `src/lib/game/activity-manager.ts` | Автоматическое переключение |

> 📖 **Подробности:** [docs/TIME_SYSTEM.md](./TIME_SYSTEM.md)

---

*Документ обновлён: 2026-03-24 15:37 UTC*
