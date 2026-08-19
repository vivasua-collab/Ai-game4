# START_PROMPT — Cultivation World Simulator (Ai-game4)

> **Назначение:** Этот файл читается ПЕРВЫМ при старте любой сессии AI-агента.
> Содержит правила работы, структуру проекта, запреты, точку входа в память.
> **Дата обновления:** 2026-08-15

---

## 1. Быстрый старт (прочитать первым делом)

```
1. Прочитать START_PROMPT.md (этот файл)           ← ~1500 токенов
2. Прочитать SESSION_SUMMARY.md                     ← ~400 токенов
3. Прочитать docs/docs_v2/00_overview/PROJECT_CONCEPT.md  ← ~600 токенов
─────────────────────────────────────────────────────────
Итого при старте: ~2500 токенов
```

**НЕ читать:** worklog.md целиком (большой), все чекпоинты, docs_old/, docs_temp/ (только по необходимости).

---

## 2. Проект

| Параметр | Значение |
|----------|----------|
| **Название** | Cultivation World Simulator (Ai-game4) |
| **Жанр** | Xianxia cultivation life-sim (Kenshi + RimWorld + cultivation) |
| **Движок** | Godot 4.7.1 .NET (C#, .NET 8) |
| **Рендер** | Чистый 2D top-down orthographic (gl_compatibility) |
| **Сетевой режим** | Полностью однопользовательская, все данные локально |
| **Репозиторий** | https://github.com/vivasua-collab/Ai-game4 |
| **Лицензия** | Приватный проект |

---

## 3. Структура проекта

```
/home/z/my-project/Ai-game4/          ← canonical (git репозиторий)
├── START_PROMPT.md                    ← этот файл (правила работы)
├── SESSION_SUMMARY.md                 ← автогенерация, контекст последних сессий
├── README.md                          ← описание для GitHub
├── .gitignore / .gitattributes
├── game/                              ← Godot проект
│   ├── src/
│   │   ├── Core/                      ← engine-agnostic (чистый C#)
│   │   │   ├── Data/                  ← Constants, Enums, Structs, DataModels
│   │   │   ├── Interfaces/            ← 30+ сервисных интерфейсов
│   │   │   ├── Messaging/Contracts/   ← readonly struct события (~130)
│   │   │   ├── DI/                    ← IContainerBuilder, Container, InjectAttribute
│   │   │   └── Events/                ← IPublisher<T>, ISubscriber<T>, EventBus
│   │   ├── Modules/                   ← 16 модулей (Body, Qi, Combat, NPC, ...)
│   │   ├── Entry/                     ← GameSession, SceneOrchestrator, 10 Phases
│   │   └── Adapter/                   ← Godot-specific (Input, Rendering, UI, Scene)
│   ├── scenes/                        ← .tscn файлы (MainMenu, GameWorld)
│   ├── data/                          ← JSON конфиги
│   ├── resources/                     ← темы (.tres)
│   └── project.godot
├── docs/                              ← документация
│   ├── docs_v2/                       ← ★ АКТУАЛЬНАЯ engine-agnostic спецификация
│   ├── docs/                          ← архив Unity 6.3 итерации
│   ├── docs_old/                      ← архив Phaser итерации
│   └── docs_temp/                     ← исследования, планы
├── checkpoints/                       ← чекпоинты работы (см. §6)
└── worklog.md                         ← хроника работы (append-only)
```

**Симлинки в `/home/z/my-project/` (Вариант D — гибридный, см. [ENVIRONMENT_LINKING.md](docs/docs_v2/09_workflow/ENVIRONMENT_LINKING.md)):**

| Симлинк | Цель | Назначение |
|---------|------|------------|
| `aigame4` | `Ai-game4/` | **Единая точка входа** (как Ai-game3-ref) — весь репозиторий |
| `checkpoints` | `Ai-game4/checkpoints` | Прямой доступ к чекпоинтам |
| `game` | `Ai-game4/game` | Код игры (backward compat) |
| `game-docs` | `Ai-game4/docs` | Документация (backward compat) |
| `godot` | `/home/z/godot` | Godot 4.7.1 binary (toolchain) |
| `Ai-game3-ref` | — | Reference clone Ai-game3 (Unity, только для чтения) |

**Доступ к ключевым файлам:**
- `aigame4/START_PROMPT.md` — этот файл (правила работы)
- `aigame4/SESSION_SUMMARY.md` — контекст сессий
- `aigame4/worklog.md` — хроника работы (append-only)
- `aigame4/recover_sandbox.sh` — скрипт восстановления песочницы
- `aigame4/checkpoints/` или `checkpoints/` — все чекпоинты

**Восстановление после сбоя:** `bash /home/z/my-project/aigame4/recover_sandbox.sh`

---

## 4. Архитектура (кратко)

**3-слойная архитектура с изоляцией движка:**

| Слой | Зависимости | Назначение | Engine-agnostic? |
|------|-------------|------------|------------------|
| **Core** | чистый C# | DI, EventBus, интерфейсы, контракты, data models | ✅ Да |
| **Modules** | Core | 16 модулей: Body, Qi, Combat, NPC, Player, World, Tile, ... | ✅ Да |
| **Entry** | Core + Modules | GameSession, SceneOrchestrator, 10 фаз сборки | ✅ Да |
| **Adapter** | Core + Modules + Godot | Input, Rendering, UI, Scene, Persistence | ❌ Godot |

**Принцип:** 100% игровой логики — engine-agnostic. Смена движка затрагивает только Adapter.

**Паттерны:**
- Hub-and-Spoke: модули общаются только через EventBus (`readonly struct` контракты)
- DI: `IContainerBuilder` + `[Inject]` attribute (property injection)
- Zero-GC: `IPublisher<T>.Publish(in T)` — `in` параметр, no boxing
- Tick-based sim: `ITickable.Tick(int tickCount)`, 1 tick = 1 game minute

---

## 5. Окружение разработки

| Компонент | Путь | Версия |
|-----------|------|--------|
| .NET SDK | `/home/z/.dotnet/` | 8.0.424 + 9.0.317 |
| Godot | `/home/z/godot/Godot_v4.7.1-stable_mono_linux_x86_64/` | 4.7.1 stable mono |
| Проект | `/home/z/my-project/Ai-game4/` | git main branch |
| Reference (Ai-game3) | `/home/z/my-project/Ai-game3-ref/` | Unity итерация (только чтение) |

**Команды:**
```bash
# Сборка
cd /home/z/my-project/Ai-game4/game
export DOTNET_ROOT=/home/z/.dotnet && export PATH=$DOTNET_ROOT:$PATH
dotnet build

# Headless проверка
export GODOT=/home/z/godot/Godot_v4.7.1-stable_mono_linux_x86_64/Godot_v4.7.1-stable_mono_linux.x86_64
"$GODOT" --headless --path . --quit

# Скриншот (визуальная верификация)
export GODOT_SCREENSHOT=/tmp/shot.png
"$GODOT" --path . --rendering-driver opengl3
# (нужен Xvfb + LIBGL_ALWAYS_SOFTWARE=1 для headless рендеринга)
```

### Восстановление песочницы

Песочница эфемерна — пересоздаётся периодически. При сбое:

```bash
# Одна команда для полного восстановления:
bash /home/z/my-project/Ai-game4/recover_sandbox.sh
```

Скрипт `recover_sandbox.sh` выполняет:
1. Устанавливает .NET SDK 8.0 + 9.0 (если нет)
2. Скачивает Godot 4.7.1 .NET (если нет)
3. Клонирует/обновляет Ai-game4 с GitHub
4. Чинит симлинки (game, game-docs, godot)
5. Создаёт NuGet.config (локальный, не в git)
6. Верифицирует сборку + headless запуск

**Где РЕАЛЬНО хранятся данные:**
- ✅ **GitHub** `vivasua-collab/Ai-game4` — код, документация, checkpoints (вечно)
- ✅ **GitHub** `vivasua-collab/Ai-game3` — reference (вечно)
- ⚠️ **Песочница** `/home/z/my-project/Ai-game4/` — клон (исчезает при reset)
- ⚠️ **Песочница** `/home/z/.dotnet/`, `/home/z/godot/` — инструменты (исчезают при reset)
- ⚠️ **Песочница** `/home/z/my-project/worklog.md` — хроника (исчезает при reset)

**Правило:** ключевые решения дублировать в `checkpoints/` (в git, не теряются).

---

## 6. Правила работы с чекпоинтами

### 6.1. Назначение чекпоинтов

Чекпоинты — **фиксация этапов работы**. Позволяют:
- Определить **место и время ошибки** (когда что-то сломалось)
- Понять **причину решения** (почему выбран этот подход)
- Восстановить контекст при новой сессии

### 6.2. Формат имени файла

```
checkpoints/ММ_ДД_краткое_описание.md
```

Примеры:
- `08_15_transfer_core_from_aigame3.md`
- `08_15_audit_module_dependencies.md`
- `08_15_fix_di_resolution_bug.md`

### 6.3. Структура чекпоинта

```markdown
# Чекпоинт: <краткое описание>

**Дата:** YYYY-MM-DD HH:MM UTC
**Сессия:** <ID сессии или краткий контекст>
**Тип:** audit | implementation | fix | decision | migration

---

## Контекст
<почему создан этот чекпоинт, что предшествовало>

## Что сделано
- <конкретные шаги>

## Решения
- <решение 1> — <причина>
- <решение 2> — <причина>

## Найденные проблемы
- <проблема> — <влияние> — <статус>

## Следующие шаги
- <что делать дальше>

## Файлы
- <созданные/изменённые файлы>
```

### 6.4. Когда создавать чекпоинт

| Событие | Создавать чекпоинт? |
|---------|---------------------|
| Завершение этапа (спринта, фазы) | ✅ Да |
| Архитектурное решение | ✅ Да |
| Найдена и исправлена баг | ✅ Да |
| Миграция кода из другого проекта | ✅ Да |
| Аудит модуля/слоя | ✅ Да |
| Мелкое изменение (1-2 файла) | ❌ Нет (достаточно worklog) |
| Эксперимент/прототип | ⚠️ Только если решение принято |

### 6.5. Правила

1. **Один чекпоинт = одно событие.** Не смешивать аудит + реализацию.
2. **Имя файла — краткое описание,** не дата-время.
3. **Глубина:** 50-200 строк. Не дублировать worklog.
4. **Факты, не мнения.** «CombatService использует float для Potency» — да. «CombatService плохо написан» — нет.
5. **Ссылки на файлы.** Указывать пути к релевантным файлам.
6. **Git commit.** Чекпоинт коммитится вместе с кодом.

---

## 7. SESSION_SUMMARY.md

**Файл:** `/home/z/my-project/Ai-game4/SESSION_SUMMARY.md`

Автогенерируемый компактный контекст последних 5 сессий. Заменяет чтение worklog + чекпоинтов при старте.

**Формат:**
```markdown
# Сводка сессий (обновляется при завершении каждой сессии)
Обновлено: YYYY-MM-DD HH:MM UTC

## Проект
Cultivation World Simulator, Godot 4.7.1 .NET, C#

## Последние сессии (5 дней)

### YYYY-MM-DD
- <ключевое действие 1>
- <ключевое действие 2>

## Активные задачи
- [ ] <задача>

## Замороженные решения (НЕ нарушать)
- <решение> — <причина>

## Предупреждения
- <предупреждение>
```

**Лимит:** <500 токенов (~1.5 KB). Обновляется в конце каждой сессии.

---

## 8. worklog.md

**Файл:** `/home/z/my-project/worklog.md` (вне git, локальный)

Хроника работы всех агентов. Append-only.

**Проблема:** worklog растёт, может теряться/урезаться при больших размерах.

**Решение:**
1. worklog — **только для текущей сессии.** Не полагаться на него как на память.
2. При завершении сессии — **обновить SESSION_SUMMARY.md** (в git, не теряется).
3. При превышении 1000 строк — **архивировать** в `checkpoints/worklog_backup_YYYYMMDD.md`.
4. Ключевые решения — **дублировать в чекпоинты**, не только в worklog.

---

## 9. Запреты (ЗАМОРОЖЕННЫЕ решения)

| # | Запрет | Причина |
|---|--------|---------|
| 1 | **НЕ использовать Unity.** | Проект на Godot 4.7.1. |
| 2 | **НЕ использовать `float` для Qi-значений.** Только `long`. | L9 ~524M effectiveQi, точность. |
| 3 | **НЕ возвращаться к MonoGame/Phaser.** | Решение принято: Godot 4.7.1 primary. |
| 4 | **НЕ запускать Next.js DEV сервер.** | Песочница не используется для игры. |
| 5 | **НЕ коммитить `.godot/`, `*.uid`, `*.import`, `bin/`, `obj/`.** | Локальный cache. |
| 6 | **НЕ коммитить токены/ключи в git.** | Безопасность. Токен хранить в памяти сессии. |
| 7 | **НЕ использовать `SetStyleBox`** (старый API). | Использовать `SetStylebox` (Godot 4.7 canonical). |
| 8 | **НЕ использовать `TileMap`** (deprecated). | Использовать `TileMapLayer` (4.5+ canonical). |
| 9 | **НЕ использовать `SetAnchorsPreset` без offsets.** | Использовать `SetAnchorsAndOffsetsPreset` или явные Anchor*+Offset*. |
| 10 | **НЕ использовать `config/name` с пробелами в project.godot.** | Ломает C# assembly loading. |

---

## 10. Текущий статус (на момент обновления)

**Дата:** 2026-08-15
**Версия:** v0.1.0
**Стадия:** Перенос кода из Ai-game3 (Unity итерация)

**Что работает:**
- Core слой (DI, EventBus, 24 интерфейса, контракты) — stubs
- 16 Modules — stubs
- Entry (GameSession, SceneOrchestrator, 10 Phases)
- Adapter (Godot: GameBoot, InputAdapter, SceneBuilder, MainMenuController, GameWorldController, UIFactory, ParchmentTheme)
- MainMenu + GameWorld сцены рендерятся
- Тестовый полигон (50×50 tiles), пиксель-арт игрок

**Что в работе:**
- Перенос Core/Data + Core/Interfaces + Core/Messaging из Ai-game3
- Перенос Modules (Calculators + Configs + Services с адаптацией MessagePipe→EventBus)
- Перенос Tests

**Что НЕ начато:**
- Реальные игровые системы (combat, Qi math, body system)
- UI Views (22 планированных)
- Save system (JSON serialization)
- Контент (lore, items, techniques)

---

## 11. Ссылки

| Что | Где |
|-----|-----|
| Концепция игры | `docs/docs_v2/00_overview/PROJECT_CONCEPT.md` |
| Тех. решения | `docs/docs_v2/00_overview/TECHNOLOGY_DECISIONS.md` |
| Архитектура | `docs/docs_v2/01_architecture/ARCHITECTURE.md` |
| 16 модулей | `docs/docs_v2/01_architecture/MODULE_STRUCTURE.md` |
| Производительность | `docs/docs_v2/01_architecture/PERFORMANCE_STRATEGY.md` |
| Алгоритмы | `docs/docs_v2/09_workflow/ALGORITHMS.md` |
| AI workflow | `docs/docs_v2/09_workflow/AI_DEVELOPMENT_WORKFLOW.md` |
| Схема памяти | `docs/docs_temp/LONG_TERM_MEMORY_SCHEME.md` (из Ai-game3) |

---

## 12. Приоритеты разработки

1. **Перенос кода из Ai-game3** (текущий приоритет)
   - Core (Data + Interfaces + Messaging) — чистый C#, переносится напрямую
   - Modules (Calculators + Configs) — чистая логика, переносится напрямую
   - Modules (Services) — адаптация MessagePipe→EventBus, VContainer→наш DI
   - Tests — перенос с минимальной адаптацией

2. **После переноса:** реализация реальных игровых систем
   - Combat (11-layer damage pipeline)
   - Qi (long arithmetic, breakthrough)
   - Body (Kenshi-style, dual HP)
   - NPC AI (3-tier nervous system)

3. **Далее:** UI Views, Save system, контент

---

*Этот файл — точка входа для любой новой сессии. Держи его актуальным.*
