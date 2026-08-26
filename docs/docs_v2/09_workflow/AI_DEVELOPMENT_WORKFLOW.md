# AI Development Workflow

> **Раздел:** 09_workflow
> **Статус:** Принятый workflow для AI-агентной разработки.
> **Связанные документы:** `00_overview/TECHNOLOGY_DECISIONS.md`, `01_architecture/ARCHITECTURE.md`, `01_architecture/MIGRATION_MAP.md`.

---

## 0. Принцип

Проект разрабатывается через **AI-агентный цикл**. AI-агент не имеет доступа к визуальному редактору движка — все артефакты (код, сцены, конфиги, ассеты-метаданные) авторятся в виде **текстовых файлов**. Человек нужен только для финального визуального QA.

**Главное правило:** Документация — это спецификация. Любое расхождение кода с документацией = баг.

---

## 1. Цикл итерации

### 1.1. Полный цикл

```
1. AI читает документацию (docs_v2/) → понимает, что нужно сделать.
2. AI пишет C#-код (модули Core/Modules/Entry).
3. AI пишет текстовые сцены (формат зависит от движка — для Godot: .tscn).
4. AI пишет JSON-конфиги (data/techniques/, data/items/, ...).
5. AI пишет .csproj/.sln обновления (если нужно).
6. AI коммитит в Git (текстовые файлы — без binary).
7. CI/headless-проверка:
   a. `dotnet build` — компиляция C#.
   b. Движок --headless --check-only — проверка сцен/ресурсов.
   c. `dotnet test` — unit- и integration-тесты.
   d. Движок --headless — рантайм-тесты / скриншот-тесты.
8. AI анализирует ошибки → переходит к шагу 2.
9. Когда все тесты зелёные → Git push.
10. Человек делает финальный визуальный QA (запуск игры, игра 5–15 минут).
11. Человек репортит визуальные баги → цикл повторяется.
```

### 1.2. Скорость итераций

- **Чистый код (без визуала):** итерация 5–30 минут (build + tests).
- **Код + сцены + UI:** итерация 30 минут – 2 часа (headless tests + скриншоты).
- **Сложные правки (миграция форматов, refactor):** итерация 2–8 часов.

---

## 2. Headless-тестирование

### 2.1. Принцип

AI-агент не видит рендер. Чтобы проверить корректность:
1. Unit-тесты для всей логики (pure C#).
2. Integration-тесты для межмодульного взаимодействия.
3. Скриншот-тесты для визуального QA.

### 2.2. Уровни тестирования

| Уровень | Что тестирует | Инструмент | Когда запускается |
|---------|---------------|------------|-------------------|
| **Unit** | Один сервис/калькулятор (pure C#) | `dotnet test` (xUnit/NUnit) | При каждом коммите |
| **Integration** | Взаимодействие модулей (через шину событий) | `dotnet test` (integration tests) | При каждом коммите |
| **Scene** | Корректность текстовых сцен | Движок `--headless --check-only` | При каждом коммите |
| **Runtime** | Запуск сцены в headless-режиме | Движок `--headless --script` | Ночные / pre-merge |
| **Screenshot** | Визуальное сравнение с эталоном | Движок `--headless` + diff tool | Pre-merge для UI-правок |

### 2.3. CI pipeline

```
on: [push, pull_request]
jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      - name: Setup Engine
        # Установка движка (Godot Mono, MonoGame, и т.д.)
      - name: Restore
        run: dotnet restore
      - name: Build
        run: dotnet build --configuration Release --no-restore
      - name: Test
        run: dotnet test --configuration Release --no-build --logger trx
      - name: Engine Headless Check
        run: engine --headless --check-only --path .
      - name: Screenshot Tests
        run: engine --headless --path . --script res://tests/run_screenshot_tests.gd
      - name: Upload Screenshots
        uses: actions/upload-artifact@v3
        with:
          name: screenshots
          path: tests/screenshots/
```

### 2.4. Что тестируется на каждом уровне

#### 2.4.1. Unit-тесты (pure C#)

| Модуль | Что тестируется |
|--------|-----------------|
| Body | BodyDamageCalculator, BodyFactory, ампутации, регенерация |
| Qi | QiService (long arithmetic), QiBufferService, QiRegenCalculator, QiBreakthroughCalculator |
| Combat | DamageCalculator (11 слоёв), LevelSuppression, DefenseProcessor |
| Buff | BuffCalculator (мягкие капы), BuffTickProcessor, иммунитеты |
| Inventory | EquipmentService, EquipmentValidator, CraftingService |
| NPC | NPCAIService, NPCRelationshipService |
| Formation | FormationCalculator (contourQi, capacity, drain) |
| Generator | MatryoshkaGenerator, GradeSelector, MaterialRegistry |

> **Принцип:** Все игровые системы — pure C#, тестируются через `dotnet test`. Никаких engine-зависимостей в тестах.

#### 2.4.2. Integration-тесты

- Hub-and-Spoke integration: проверка, что модули НЕ имеют прямых зависимостей.
- Save/Load integration: сохранение → загрузка → сравнение состояний.
- Combat pipeline integration: атака → пайплайн → результат.
- Scene assembly integration: 10 фаз последовательно → корректное состояние.

#### 2.4.3. Скриншот-тесты

- Tile rendering (правильные тайлы на правильных позициях).
- UI layout (панели, кнопки, текст — корректное позиционирование).
- Sprite composition (player, NPC, формации).
- Lighting (2D-свет не делает спрайты чёрными).
- Sorting layers (правильный z-order).

**Процесс:**
1. AI рендерит сцену в headless-режиме.
2. Скриншот сохраняется в `tests/screenshots/actual/`.
3. Diff с эталоном `tests/screenshots/expected/`.
4. Если расхождение > порога (например, 5% пикселей) → тест провален.
5. AI анализирует diff → исправляет → повторяет.

---

## 3. Документация как спецификация

### 3.1. Принцип

`docs_v2/` — единственный source of truth. AI-агенты работают строго по документации; любые расхождения кода с документацией = баг.

### 3.2. Иерархия документов

| Приоритет | Документ | Область |
|-----------|----------|---------|
| 1 | `09_workflow/ALGORITHMS.md` | Формулы, расчёты, мягкие капы |
| 2 | `04_entities/ENTITY_TYPES.md` (TODO) | Типы сущностей |
| 3 | `06_player/EQUIPMENT_SYSTEM.md` (TODO) | Экипировка |
| 4 | `02_systems/ELEMENTS_SYSTEM.md` (TODO) | Стихии |
| 5 | `02_systems/BUFF_MODIFIERS_SYSTEM.md` (TODO) | Баффы |
| 6 | `01_architecture/ARCHITECTURE.md` | Архитектура |
| 7 | Остальные | Конкретные системы |

### 3.3. Правила работы с документацией

1. **Перед изменением кода** — проверить, что говорит документация.
2. **Если код расходится с документацией** — либо исправить код, либо обновить документацию (с обоснованием).
3. **Если документация неполна** — дополнить её перед реализацией.
4. **Никаких «тайных» знаний** — всё, что знает AI, должно быть в docs_v2/.

### 3.4. Формат документации

- Markdown (`.md`).
- Русский язык (основной язык проекта).
- Чёткая иерархия заголовков.
- Таблицы для структурированных данных.
- Code blocks для примеров (с указанием языка).
- Связанные документы внизу каждого файла.

---

## 4. Контрольные аудиты

### 4.1. Принцип

Регулярные аудиты кода против документации — для выявления расхождений.

### 4.2. Типы аудитов

| Тип | Что проверяет | Периодичность |
|-----|---------------|---------------|
| **Architecture audit** | Соответствие структуры модулей `MODULE_STRUCTURE.md` | После крупных refactor-ов |
| **Formula audit** | Соответствие формул в коде `ALGORITHMS.md` | После правок в combat/qi/buff |
| **Hub-and-Spoke audit** | Отсутствие межмодульных прямых зависимостей | Раз в 2 недели |
| **Zero-GC audit** | Отсутствие аллокаций в hot path (через profiler) | Раз в месяц |
| **Documentation audit** | Полнота и актуальность docs_v2 | После крупных фич |

### 4.3. Шаблон аудита

```markdown
# Audit Report — <DATE>

## Audit Type
- [ ] Architecture
- [ ] Formula
- [ ] Hub-and-Spoke
- [ ] Zero-GC
- [ ] Documentation

## Findings

### Critical (blocking)
1. <file:line> — <description>

### Warnings (should fix)
1. <file:line> — <description>

### Info (suggestions)
1. <file:line> — <description>

## Summary
- Total findings: X
- Critical: Y
- Warnings: Z
- Info: W

## Next Actions
1. ...
```

### 4.4. История аудитов (справочно)

В предыдущей итерации (Unity 6.3) было проведено несколько аудитов, выявивших:
- Двойная публикация событий (PLR-A01).
- God Objects (PlayerController 1425 LOC, BuffManager 1614 LOC, EquipmentController 1418 LOC).
- Мёртвые stub-сервисы (19 файлов, не регистрировались).
- Циркулярные зависимости (TileMapService ↔ ResourceService).
- Утечки корутин (DialogueTypewriter без CancellationToken).

Эти проблемы решены в архитектуре docs_v2 через:
- Парность событий (start + end).
- Расщепление God Objects на сервисы + калькуляторы + helper-ы.
- ModuleServices pattern (единая регистрация в корневом scope).
- readonly struct контракты через шину событий.
- async/await с CancellationToken.

---

## 5. Git workflow

### 5.1. Ветвление

| Ветвь | Назначение |
|-------|------------|
| `main` | Стабильная версия, проходит все тесты |
| `feature/<name>` | Новая фича (модуль, система, refactor) |
| `fix/<name>` | Баг-фикс |
| `docs/<name>` | Только правки документации |
| `experiment/<name>` | Эксперимент (можно переписывать историю) |

### 5.2. Коммиты

**Формат:**
```
<type>(<scope>): <subject>

<body>

<footer>
```

**Типы:**
- `feat` — новая фича.
- `fix` — баг-фикс.
- `docs` — документация.
- `refactor` — рефакторинг без изменения поведения.
- `test` — тесты.
- `chore` — технические задачи (build, CI).
- `perf` — производительность.

**Примеры:**
```
feat(combat): implement 11-layer damage pipeline

Слои 1-10 реализованы в DamageCalculator. Слой 11 (последствия)
делегируется в BodyService через BodyPartDamagedEvent.

Refs: ALGORITHMS.md §5
```

```
fix(qi): track consumed Qi in buffer for correct return

QI-A05: при Deactivate возвращалось некорректное количество Ци,
т.к. не отслеживалось потреблённое. Теперь QiBufferService хранит
_consumedQi и возвращает его при деактивации.
```

### 5.3. Two-PC workflow

При разработке на двух ПК (sandbox + локальный):

1. **Sandbox (AI):** коммиты в `feature/*`, push в GitHub.
2. **Локальный ПК:** pull, visual QA, push фиксов (если нужно).
3. **Sandbox:** pull, продолжение работы.

> **Критично:** Все файлы должны быть текстовыми. Binary файлы (сцены в проприетарных форматах, .meta, .asset) — причина merge hell.

### 5.4. .gitignore

```
# Build artifacts
bin/
obj/
*.dll
*.pdb

# IDE
.vs/
.vscode/
.idea/
*.user

# OS
.DS_Store
Thumbs.db

# Engine-specific (пример для Godot)
.godot/
*.import

# Saves (runtime)
saves/

# Logs
*.log
logs/
```

---

## 6. AI-навыки (skills)

### 6.1. Доступные skills для разработки

| Skill | Use case | Приоритет |
|-------|----------|-----------|
| **Web-Search** | Поиск документации движка, туториалов, решений | Высокий |
| **Web-Reader** | Чтение онлайн-документации | Высокий |
| **Image-Generation** | Иконки предметов, концепт-арт, UI элементы, спрайты | Высокий |
| **Image-Search** | Поиск референсов, текстур окружения | Высокий |
| **VLM** | Анализ скриншотов рендеринга, проверка UI | Высокий |
| **XLSX** | Таблицы баланса техник, предметов, уровней | Высокий |
| **Image-Edit** | Модификация существующих ассетов, вариации | Средний |
| **Charts** | Визуализация данных баланса, архитектурные диаграммы | Средний |
| **DOCX** | Дизайн-документы, GDD | Средний |
| **PDF** | Формальные отчёты, white papers | Средний |
| **LLM** | Генерация диалогов NPC, описание лора | Средний |
| **Agent-Browser** | Интерактивное взаимодействие с веб-страницами | Средний |
| **TTS** | Озвучка NPC, голосовые подсказки | Низкий |
| **Video-Understanding** | Анализ видео (например, записей QA) | Низкий |

### 6.2. Примеры использования

#### 6.2.1. Поиск решения проблемы рендеринга

```
1. Web-Search: "движок 2D рендеринг чёрные спрайты без освещения"
2. Web-Reader: <URL документации>
3. Синтез решения → применение к SceneBuilder
4. Скриншот-тест для проверки
```

#### 6.2.2. Генерация иконки предмета

```
1. Image-Generation: "Fantasy cultivation herb icon, glowing green, Chinese style, transparent background, 64x64"
2. Сохранение в assets/sprites/icons/consumables/
3. Регистрация в SpriteCatalog
4. Скриншот-тест: иконка в инвентаре
```

#### 6.2.3. Анализ скриншота проблемы

```
1. VLM: [пользователь загружает скриншот] → "Что не так с рендерингом?"
2. Анализ: спрайты чёрные → нет 2D-освещения → Sprite-Lit-Default без света.
3. Применение фикса в SceneBuilder (добавить глобальный 2D-свет).
4. Скриншот-тест: свет работает.
```

#### 6.2.4. Создание таблицы баланса

```
1. XLSX: Создать таблицу техник культивации
2. Колонки: Name, QiCost, Damage, Cooldown, Element, Level
3. Формулы: DPS = Damage / Cooldown
4. Экспорт данных в JSON (data/techniques/)
5. Тест: загрузка техник из JSON в TechniqueService
```

#### 6.2.5. Архитектурная диаграмма

```
1. Charts: Создать архитектурную диаграмму Hub-and-Spoke
2. 16 модулей → Core → event bus связи
3. Вывод: Playwright+CSS → PNG/SVG
4. Сохранить в docs_v2/assets/diagrams/
```

---

## 7. Жизненный цикл фичи

### 7.1. Этапы

```
1. Замечена потребность (из audit, user feedback, plan).
2. Обновлена документация (docs_v2/02_systems/... или соответствующий файл).
3. Создана ветка feature/<name>.
4. Реализован код (Core/Modules/Entry).
5. Написаны тесты (unit + integration).
6. Обновлён скриншот-эталон (если UI/визуал).
7. CI зелёный.
8. Code review (через PR).
9. Merge в main.
10. Человек делает визуальный QA.
11. Если баги → новый fix branch.
```

### 7.2. Правила реализации

1. **Сначала документация** — обновить docs_v2 перед изменением кода.
2. **Тесты вместе с кодом** — не «потом».
3. **Zero-GC проверка** — для hot path обязателен профайлинг.
4. **Engine-agnostic** — код в Core/Modules не должен зависеть от движка.
5. **ModuleServices pattern** — единая регистрация для всех модулей.
6. **readonly struct** — все контракты сообщений.
7. **async/await** — не Coroutines/timers.
8. **CancellationToken** — все долгие операции.

---

## 8. Контрольные точки

### 8.1. Перед коммитом

- [ ] Код компилируется (`dotnet build` зелёный).
- [ ] Тесты проходят (`dotnet test` зелёный).
- [ ] Движок --headless --check-only зелёный.
- [ ] Скриншот-тесты зелёные (если UI/визуал).
- [ ] Документация обновлена.
- [ ] Нет debug-only кода в production.
- [ ] Нет TODO без issue-ссылки.

### 8.2. Перед merge в main

- [ ] Code review пройден.
- [ ] CI зелёный.
- [ ] Человек сделал визуальный QA (если UI/визуал).
- [ ] CHANGELOG обновлён (если significant change).
- [ ] Version bump (если нужно).

### 8.3. Перед release

- [ ] Все тесты зелёные.
- [ ] Performance budgets не превышены (см. `01_architecture/PERFORMANCE_STRATEGY.md`).
- [ ] Все TODO имеют issue-ссылки или удалены.
- [ ] Documentation audit пройден.
- [ ] Сохранения совместимы с предыдущей версией (или миграция).

---

## 9. Обработка ошибок

### 9.1. Типы ошибок

| Тип | Что делать |
|-----|------------|
| **Compile error** | AI исправляет код, повторяет `dotnet build`. |
| **Test failure** | AI анализирует, исправляет код или тест, повторяет. |
| **Scene check failure** | AI правит текстовую сцену, повторяет. |
| **Runtime error (headless)** | AI смотрит лог, добавляет логирование, повторяет. |
| **Screenshot diff** | AI анализирует diff (через VLM если нужно), исправляет, повторяет. |
| **Visual QA failure (human)** | Человек репортит, AI исправляет. |

### 9.2. Эскалация

- Если AI не может исправить за 3 итерации → помечает как `blocked`, человек разбирается.
- Если визуальный баг не воспроизводится в headless → человек делает запись экрана, AI анализирует через VLM.

---

## 10. Миграция с предыдущих итераций

### 10.1. Принцип

Проект прошёл 2 итерации: Phaser (отменена) и Unity 6.3 (застопорилась на графике). docs_v2 — engine-agnostic переработка всей документации.

### 10.2. Что взято из предыдущих итераций

| Что | Откуда | Статус |
|-----|--------|--------|
| Hub-and-Spoke архитектура | Unity iteration | ✅ Сохранён |
| 16 модулей + ModuleServices | Unity iteration | ✅ Сохранён |
| readonly struct контракты | Unity iteration | ✅ Сохранён |
| Все игровые формулы | Unity iteration | ✅ Сохранён |
| Tick-based симуляция | Unity iteration | ✅ Сохранён |
| ISaveable pattern | Unity iteration | ✅ Сохранён |
| Kenshi-style body | Unity iteration + Phaser iteration | ✅ Сохранён |
| Matryoshka generation | Phaser iteration | ✅ Сохранён |
| 3-tier AI nervous system | Unity iteration | ✅ Сохранён |
| Дизайн-корпус (формулы, лор, баланс) | Обе итерации | ✅ Сохранён |

### 10.3. Что отброшено

| Что | Причина |
|-----|---------|
| MonoBehaviour UI Views | Engine-specific |
| RuntimeSceneBuilder (Unity) | Engine-specific |
| ScriptableObjects | Engine-specific |
| Unity Tilemap + GameTile : TileBase | Engine-specific |
| CameraFollow, Light2D, uGUI Canvas | Engine-specific |
| VContainer, MessagePipe, UniTask | Engine-specific (паттерны сохранены, реализации — нет) |
| Editor auto-config phases | Engine-specific |
| asmdef | Engine-specific |
| Phaser.Physics.Arcade, Phaser.Scene | Engine-specific |
| Caddy gateway, Socket.io, Bun, Prisma | Server stack — отменён (игра однопользовательская) |

> Подробная карта миграции — в `01_architecture/MIGRATION_MAP.md`.

---

## 11. Связанные документы

| Документ | Описание |
|----------|----------|
| `00_overview/TECHNOLOGY_DECISIONS.md` | Выбор движка, стратегия производительности |
| `01_architecture/ARCHITECTURE.md` | Архитектура |
| `01_architecture/MIGRATION_MAP.md` | Карта миграции старых документов |
| `09_workflow/ALGORITHMS.md` | Формулы (источник истины) |
| `09_workflow/TESTING_RULES.md` (TODO) | Правила тестирования |

---

*Документ создан в рамках Task 2-a: engine-agnostic rewrite. Источники: `docs/!Ai_Skills.md` v3.1, `docs_temp/ENGINE_CHOICE_ANALYSIS.md` §6, `docs_temp/WORKFLOW_GITHUB_UNITY.md`, `docs_temp/GIT_WORKFLOW_TWO_PC.md`.*
