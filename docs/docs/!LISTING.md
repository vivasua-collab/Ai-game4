# 📂 docs/ — Основная документация проекта — листинг

**Версия:** 5.5
**Дата:** 2026-07-14
**Проект:** Cultivation World Simulator (Unity 6.3 URP 2D)

---

## ⚠️ Важно

> **Все документы в `docs/` — ТЕОРИЯ и СПЕЦИФИКАЦИИ.**
> **Код реализации:** `UnityProject/Assets/Scripts/` (429 файлов, ~77K строк).
> **Инструкции для Unity Editor:** `Legacy/docs_asset_setup/` → [!LISTING.md](../Legacy/docs_asset_setup/!LISTING.md).
> **Временная документация, аудиты, черновики:** `docs_temp/` → [!listing.md](../docs_temp/!listing.md).
> **Архив (Phaser-эра):** `docs_old/` → [Listing.md](../docs_old/Listing.md).
> **Чекпоинты работы:** `checkpoints/` (183 файла, самостоятельный реестр).
> **Все папки docs/Legacy/checkpoints — внутри UnityProject/.**

### 📐 Оценка токенов

> **Метод:** `chars ÷ 3` — приближённая оценка для русскоязычного текста с кодом.
> Реальное потребление зависит от токенизатора (GPT-4: ~2.5 chars/token, Claude: ~3.5).
> **Легенда стоимости:** 🔥 >15K | ⚠️ 5K–15K | ✅ <5K

---

## 📋 docs/ — Основная документация (60 файлов, 1.4 MB, ~580K токенов)

### Справочники

| Файл | Описание | Размер | Токены ≈ | |
|------|----------|--------|----------|-|
| [!Ai_Skills.md](./!Ai_Skills.md) | Доступные Skills ИИ-ассистента (ASR, TTS, LLM, Web-Search и др.) | 11 KB | 3.8K | ✅ |
| [!hotkeys.md](./!hotkeys.md) | Горячие клавиши Unity Editor и Runtime | 8 KB | 2.7K | ✅ |
| [!LISTING.md](./!LISTING.md) | Этот файл — список основной документации | 30 KB | 10.0K | ⚠️ |
| [UNITY_DOCS_LINKS.md](./UNITY_DOCS_LINKS.md) | 150+ ссылок на документацию Unity 6.3 | 14 KB | 4.6K | ✅ |
| [SETUP_GUIDE.md](./SETUP_GUIDE.md) | Руководство по установке и настройке проекта | 10 KB | 3.4K | ✅ |
| [GLOSSARY.md](./GLOSSARY.md) | Глоссарий терминов проекта | 24 KB | 8.0K | ⚠️ |

### Архитектура и основы

| Файл | Описание | Размер | Токены ≈ | |
|------|----------|--------|----------|-|
| [ARCHITECTURE.md](./ARCHITECTURE.md) | Общая архитектура, паттерны, модули | 28 KB | 9.5K | ⚠️ |
| [ARCHITECTURE_CODE.md](./ARCHITECTURE_CODE.md) | Архитектура кодовой базы, модули, зависимости | 22 KB | 7.4K | ⚠️ |
| [ARCHITECTURE_FILE_TREE.md](./ARCHITECTURE_FILE_TREE.md) | Дерево файлов кодовой базы | 10 KB | 3.3K | ✅ |
| [ARCHITECTURE_IMPL.md](./ARCHITECTURE_IMPL.md) | Архитектура реализации: статусы модулей, ход реализации | 35 KB | 11.7K | ⚠️ |
| [SCENE_BUILDER_SYSTEM.md](./SCENE_BUILDER_SYSTEM.md) | Система сборки сцены (Runtime): RuntimeSceneBuilder, 10 фаз, WireUIViews | 7 KB | 2.3K | ✅ |
| [SCENE_BUILDER_SYSTEM_Old.md](./SCENE_BUILDER_SYSTEM_Old.md) | Система сборки сцены (Editor-time, ЗАМОРОЖЕН): FullSceneBuilder, 20 фаз | 15 KB | 5.0K | ✅ |
| [DATA_MODELS.md](./DATA_MODELS.md) | Модели данных, 17 сущностей + UI-модели, ScriptableObjects | 25 KB | 8.3K | ⚠️ |
| [DEVELOPMENT_PLAN.md](./DEVELOPMENT_PLAN.md) | ⚠️ ARCHIVED — legacy план (Фазы 1–8) | 13 KB | 4.3K | ✅ |

### Системы данных

| Файл | Описание | Размер | Токены ≈ | |
|------|----------|--------|----------|-|
| [CONFIGURATIONS.md](./CONFIGURATIONS.md) | Конфигурации: уровни культивации, техники, материалы | 11 KB | 3.7K | ✅ |
| [ALGORITHMS.md](./ALGORITHMS.md) | Алгоритмы и формулы: урон, подавление, Ци буфер | 28 KB | 9.3K | ⚠️ |

### Игровые системы

| Файл | Описание | Размер | Токены ≈ | |
|------|----------|--------|----------|-|
| [COMBAT_SYSTEM.md](./COMBAT_SYSTEM.md) | Боевая система, пайплайн урона 11 слоёв | 30 KB | 10.1K | ⚠️ |
| [TECHNIQUE_SYSTEM.md](./TECHNIQUE_SYSTEM.md) | Система техник культивации | 12 KB | 3.9K | ✅ |
| [QI_SYSTEM.md](./QI_SYSTEM.md) | Система Ци: накопление, проводимость, плотность, прорывы | 20 KB | 6.6K | ⚠️ |
| [BODY_SYSTEM.md](./BODY_SYSTEM.md) | Kenshi-style система тела, двойная HP | 24 KB | 8.1K | ⚠️ |
| [INVENTORY_SYSTEM.md](./INVENTORY_SYSTEM.md) | Инвентарь v2.0, экипировка, слоты, рюкзак, кольца | 23 KB | 7.6K | ⚠️ |
| [EQUIPMENT_SYSTEM.md](./EQUIPMENT_SYSTEM.md) | Экипировка: материалы, грейды, прочность | 30 KB | 10.0K | ⚠️ |
| [BUFF_SYSTEM.md](./BUFF_SYSTEM.md) | Система баффов/дебаффов (устарело, см. BUFF_MODIFIERS) | 31 KB | 10.4K | ⚠️ |
| [BUFF_MODIFIERS_SYSTEM.md](./BUFF_MODIFIERS_SYSTEM.md) | Баффы + модификаторы (актуальная версия) | 53 KB | 17.9K | 🔥 |
| [FORMATION_SYSTEM.md](./FORMATION_SYSTEM.md) | Система формаций, физические носители, утечка Ци | 19 KB | 6.2K | ⚠️ |
| [CHARGER_SYSTEM.md](./CHARGER_SYSTEM.md) | Зарядные ядра для формаций | 23 KB | 7.6K | ⚠️ |
| [ELEMENTS_SYSTEM.md](./ELEMENTS_SYSTEM.md) | Стихии, противоположности, сродство | 14 KB | 4.7K | ✅ |
| [MODIFIERS_SYSTEM.md](./MODIFIERS_SYSTEM.md) | Модификаторы, баффы, дебаффы | 22 KB | 7.2K | ⚠️ |
| [STAT_THRESHOLD_SYSTEM.md](./STAT_THRESHOLD_SYSTEM.md) | Пороги развития характеристик | 12 KB | 4.1K | ✅ |
| [GENERATORS_SYSTEM.md](./GENERATORS_SYSTEM.md) | Генераторы: NPC, техники, предметы | 9 KB | 2.9K | ✅ |
| [GENERATORS_NAME_FIX.md](./GENERATORS_NAME_FIX.md) | Исправление грамматики в генераторах | 16 KB | 5.3K | ⚠️ |
| [PERK_SYSTEM.md](./PERK_SYSTEM.md) | Система перков | 45 KB | 15.1K | 🔥 |
| [MORTAL_DEVELOPMENT.md](./MORTAL_DEVELOPMENT.md) | Этапы развития смертного до культивации | 20 KB | 6.6K | ⚠️ |
| [JOURNAL_SYSTEM.md](./JOURNAL_SYSTEM.md) | Система журнала/летописи | 22 KB | 7.5K | ⚠️ |
| [BREAKTHROUGH_MODELS_COMPARISON.md](./BREAKTHROUGH_MODELS_COMPARISON.md) | Сравнение моделей прорыва (А/Б/В) — реализовано в QiBreakthroughCalculator | 28 KB | 9.3K | ✅ |
| [TECHNIQUE_USAGE_REPORT.md](./TECHNIQUE_USAGE_REPORT.md) | Отчёт: использование техник практиками | 12 KB | 4.0K | ✅ |
| [TechniqueEffectsSystem.md](./TechniqueEffectsSystem.md) | Система эффектов техник — ElementalEffectService | 20 KB | 6.7K | ✅ |
| [BuffSystem_Examples.md](./BuffSystem_Examples.md) | Примеры кода: система баффов | 12 KB | 4.0K | ✅ |
| [StatThresholdSystem_Examples.md](./StatThresholdSystem_Examples.md) | Примеры кода: пороги развития | 12 KB | 4.0K | ✅ |
| [FormationSystem_Examples.md](./FormationSystem_Examples.md) | Примеры кода: система формаций | 12 KB | 4.0K | ✅ |

### NPC и AI

| Файл | Описание | Размер | Токены ≈ | |
|------|----------|--------|----------|-|
| [NPC_AI_SYSTEM.md](./NPC_AI_SYSTEM.md) | AI NPC, Spinal Controller, рефлексы, behaviour tree | 21 KB | 6.9K | ⚠️ |
| [NPC.md](./NPC.md) | Сборка NPC: компоненты, пайплайн, GeneratedNPC, NPCState, ссылки | 16 KB | 5.3K | ⚠️ |
| [NPC_ASSEMBLY_PIPELINE.md](./NPC_ASSEMBLY_PIPELINE.md) | Пайплайн сборки NPC: 8 шагов, душа→фенотип→тело→Ци→экипировка→техники→инвентарь→регистрация | 44 KB | 14.6K | ⚠️ |
| [NPC_ASSEMBLY_EXAMPLES.md](./NPC_ASSEMBLY_EXAMPLES.md) | Примеры сборки NPC — VContainer+MessagePipe | 36 KB | 12.0K | ✅ |
| [NPC_L6_ASSEMBLY_EXAMPLE.md](./NPC_L6_ASSEMBLY_EXAMPLE.md) | Пример сборки NPC — Человек L6 Культиватор | 8 KB | 2.7K | ✅ |
| [NameGenerator_Russian.md](./NameGenerator_Russian.md) | Генератор названий на русском языке | 24 KB | 8.0K | ⚠️ |

### Мир и время

| Файл | Описание | Размер | Токены ≈ | |
|------|----------|--------|----------|-|
| [WORLD_SYSTEM.md](./WORLD_SYSTEM.md) | Мир, локации, 3D координаты | 11 KB | 3.8K | ✅ |
| [WORLD_MAP_SYSTEM.md](./WORLD_MAP_SYSTEM.md) | Мировая карта: секторы, местность, навигация | 80 KB | 26.8K | 🔥 |
| [LOCATION_MAP_SYSTEM.md](./LOCATION_MAP_SYSTEM.md) | Локации: генерация зданий, препятствий, ресурсов | 55 KB | 18.2K | 🔥 |
| [TILE_SYSTEM.md](./TILE_SYSTEM.md) | Тайловая система: структура, параметры, генерация | 116 KB | 38.6K | 🔥 |
| [TILE_SYSTEM_IMPLEMENTATION.md](./TILE_SYSTEM_IMPLEMENTATION.md) | Тайловая система — реализация (краткий справочник) | 8 KB | 2.7K | ✅ |
| [TRANSITION_SYSTEM.md](./TRANSITION_SYSTEM.md) | Система переходов между картами | 191 KB | 63.5K | 🔥 |
| [TIME_SYSTEM.md](./TIME_SYSTEM.md) | Система времени, тики, календарь | 20 KB | 6.5K | ⚠️ |
| [FACTION_SYSTEM.md](./FACTION_SYSTEM.md) | Фракции, отношения, нации | 9 KB | 2.9K | ✅ |
| [LORE_SYSTEM.md](./LORE_SYSTEM.md) | Лор, истории, миростроение | 6 KB | 2.1K | ✅ |
| [ENTITY_TYPES.md](./ENTITY_TYPES.md) | Иерархия типов сущностей | 22 KB | 7.3K | ⚠️ |

### Рендеринг и графика

| Файл | Описание | Размер | Токены ≈ | |
|------|----------|--------|----------|-|
| [SORTING_LAYERS.md](./SORTING_LAYERS.md) | Sorting Layers: порядок, диагностика, код = источник истины | 19 KB | 6.3K | ⚠️ |
| [SPRITE_INDEX.md](./SPRITE_INDEX.md) | Индекс спрайтов: размеры, PPU, формат | 17 KB | 5.8K | ⚠️ |

### Специальные системы

| Файл | Описание | Размер | Токены ≈ | |
|------|----------|--------|----------|-|
| [SAVE_SYSTEM.md](./SAVE_SYSTEM.md) | Сохранения, JSON, сериализация, Unix timestamps | 4 KB | 1.3K | ✅ |
| [WORLD_SAVE_SYSTEM.md](./WORLD_SAVE_SYSTEM.md) | Сохранение мира, chunk-based persistence | 62 KB | 20.7K | 🔥 |

### Тестирование

| Файл | Описание | Размер | Токены ≈ | |
|------|----------|--------|----------|-|
| [UNIT_TEST_RULES.md](./UNIT_TEST_RULES.md) | Правила написания unit тестов | 9 KB | 2.9K | ✅ |
| [RUNNING_TESTS.md](./RUNNING_TESTS.md) | Запуск тестов в Unity Test Framework | 12 KB | 4.0K | ✅ |

---

### 💰 Топ-5 самых дорогих файлов docs/

| Файл | Токены ≈ | |
|------|----------|-|
| TRANSITION_SYSTEM.md | 63.5K | 🔥 |
| TILE_SYSTEM.md | 38.6K | 🔥 |
| WORLD_MAP_SYSTEM.md | 26.8K | 🔥 |
| WORLD_SAVE_SYSTEM.md | 20.7K | 🔥 |
| LOCATION_MAP_SYSTEM.md | 18.2K | 🔥 |

> ⚡ Эти 5 файлов = **~168K токенов** (42% от всей папки docs/). Читать только по необходимости.

---

## 🔗 Соседние папки документации

> Этот листинг ограничен папкой `docs/`. Для других папок — см. собственные листинги:

| Папка | Листинг | Файлов | Объём | Описание |
|-------|---------|--------|-------|----------|
| `Legacy/docs_asset_setup/` | [!LISTING.md](../Legacy/docs_asset_setup/!LISTING.md) | 34 | 375 KB | Инструкции для Unity Editor |
| `docs_temp/` | [!listing.md](../docs_temp/!listing.md) | 24 | 0.9 MB | Черновики, аудиты, исследования |
| `docs_old/` | [Listing.md](../docs_old/Listing.md) | 69 | 2.0 MB | Архив Phaser-эры |
| `checkpoints/` | — | 139 | 956 KB | Чекпоинты работы |

---

## 🔗 Связи основных документов

```
ARCHITECTURE.md (9.5K токенов, корневой документ)
    ├── DATA_MODELS.md (7.2K)
    ├── ALGORITHMS.md (9.3K)
    ├── CONFIGURATIONS.md (3.7K)
    │
    ├── COMBAT_SYSTEM.md (10.1K)
    │   ├── TECHNIQUE_SYSTEM.md (3.9K)
    │   ├── QI_SYSTEM.md (6.6K)
    │   ├── BODY_SYSTEM.md (8.1K)
    │   ├── BUFF_SYSTEM.md (10.4K) ← устарело
    │   └── BUFF_MODIFIERS_SYSTEM.md (17.9K) 🔥 ← актуально
    │
    ├── INVENTORY_SYSTEM.md (7.6K)
    │   └── EQUIPMENT_SYSTEM.md (10.0K)
    │
    ├── FORMATION_SYSTEM.md (6.2K)
    │   └── CHARGER_SYSTEM.md (7.6K)
    │
    ├── NPC_AI_SYSTEM.md (6.9K)
    │   ├── NPC.md (5.3K) — сборка, компоненты, пайплайн
    │   └── NPC_ASSEMBLY_PIPELINE.md (14.6K) — 8-шаговый пайплайн генерации NPC
    │
    ├── WORLD_SYSTEM.md (3.8K)
    │   ├── WORLD_MAP_SYSTEM.md (26.8K) 🔥
    │   ├── LOCATION_MAP_SYSTEM.md (18.2K) 🔥
    │   ├── TILE_SYSTEM.md (38.6K) 🔥
    │   ├── SORTING_LAYERS.md (6.3K)
    │   ├── TRANSITION_SYSTEM.md (63.5K) 🔥
    │   ├── TIME_SYSTEM.md (6.5K)
    │   ├── FACTION_SYSTEM.md (2.9K)
    │   └── ENTITY_TYPES.md (7.3K)
    │
    ├── SAVE_SYSTEM.md (1.3K)
    │   └── WORLD_SAVE_SYSTEM.md (20.7K) 🔥
    │
    ├── GENERATORS_SYSTEM.md (2.9K)
    │   └── GENERATORS_NAME_FIX.md (5.3K)
    │
    └── PERK_SYSTEM.md (15.1K) 🔥
        └── MORTAL_DEVELOPMENT.md (6.6K)
```

> 🔥 Все 7 «дорогих» файлов docs/ — в ветке WORLD (5) + PERK_SYSTEM (1) + BUFF_MODIFIERS (1).

---

## 📝 Рекомендации по чтению (с учётом стоимости)

### Минимальный старт (~16K токенов):
1. `START_PROMPT.md` — 2.4K ✅ — правила работы
2. `!hotkeys.md` — 2.7K ✅ — горячие клавиши
3. `ARCHITECTURE.md` — 9.5K ⚠️ — общая картина
4. `DATA_MODELS.md` — 7.2K ⚠️ — структуры данных

### Для боевой системы (~55K токенов — дорого!):
1. `COMBAT_SYSTEM.md` (10.1K) → `QI_SYSTEM.md` (6.6K) → `BODY_SYSTEM.md` (8.1K) → `BUFF_MODIFIERS_SYSTEM.md` (17.9K 🔥) → `TECHNIQUE_SYSTEM.md` (3.9K)

### Для инвентаря (~18K токенов):
1. `INVENTORY_SYSTEM.md` (7.6K) → `EQUIPMENT_SYSTEM.md` (10.0K)
2. `docs_asset_setup/17_InventoryData.md` (3.4K) + `18_InventoryUI.md` (4.6K) + `19_NPCPlacement.md` (5.4K)

### Для NPC и AI (~15K токенов):
1. `NPC.md` (5.3K) → `NPC_ASSEMBLY_PIPELINE.md` (14.6K) → `NPC_AI_SYSTEM.md` (6.9K) → `GENERATORS_SYSTEM.md` (2.9K)

### ⛔ Читать только по прямой необходимости:
- `TRANSITION_SYSTEM.md` — 63.5K 🔥 (самый дорогой файл)
- `TILE_SYSTEM.md` — 38.6K 🔥
- `WORLD_MAP_SYSTEM.md` — 26.8K 🔥
- `WORLD_SAVE_SYSTEM.md` — 20.7K 🔥
- `worklog.md` — 34.0K 🔥 (читать только хвост)

---

## 🛠️ Окружение проекта

**Unity 6.3 (6000.3)**, URP 2D, C#
**GitHub:** `vivasua-collab/Ai-game3.git`, branch `main`

**Структура проекта:**
```
/home/z/my-project/
├── UnityProject/              # Unity проект
│   ├── Assets/Scripts/        # 429 C# файлов — модульная архитектура
│   ├── Legacy/                # АРХИВ (заморожено, справочник)
│   │   └── docs_asset_setup/  # Инструкции для Unity Editor (24 файла)
│   ├── checkpoints/           # Чекпоинты работы (183 файла, ~1.1 MB)
│   ├── checkpoints_archive/   # Архив чекпоинтов
│   ├── docs/                  # Основная документация (60 файлов, ~580K токенов) ← этот листинг
│   ├── docs_old/              # Архив Phaser-эры (69 файлов, ~668K токенов)
│   └── docs_temp/             # Черновики, аудиты (24 файла, ~385K токенов)
├── Caveman.md                 # Режим коммуникации (1.4K токенов)
├── START_PROMPT.md            # Правила работы ИИ-агента (2.4K токенов)
├── README.md                  # Описание проекта
└── .gitignore
```

---

## 📊 Сводная статистика по всем папкам

| Папка | Файлов | Объём | Токены ≈ | 🔥 | ⚠️ | ✅ | Листинг |
|-------|--------|-------|----------|-----|-----|-----|---------|
| `docs/` | 60 | 1.8 MB | 580K | 7 | 23 | 14 | Этот файл |
| `Legacy/docs_asset_setup/` | 34 | 375 KB | 125K | 0 | 6 | 28 | [!LISTING.md](../Legacy/docs_asset_setup/!LISTING.md) ⛔ FROZEN |
| `docs_temp/` | 24 | 0.9 MB | 385K | 8 | 12 | 36 | [!listing.md](../docs_temp/!listing.md) |
| `docs_old/` | 69 | 2.0 MB | 668K | — | — | — | [Listing.md](../docs_old/Listing.md) |
| Корневые | 4 | 120 KB | 40K | 1 | 0 | 3 | — |
| **Итого** | **206** | **4.9 MB** | **~1.6M** | **16** | **40** | **81** | |

### Распределение по стоимости (docs/)

```
✅ Дешёвые (<5K токенов)    14 файлов  ██████████████                       33%
⚠️ Средние  (5K–15K токенов) 22 файла  ██████████████████████████           51%
🔥 Дорогие  (>15K токенов)   7 файлов  ███████                               16%
```

> **Вывод:** 7 «дорогих» файлов содержат **~55% токенов** docs/. Чтение только по необходимости — главный рычаг экономии.

---

*Редактировано: 2026-07-14*
*Изменения v5.6: Перенесено 10 файлов из docs_temp/ в docs/ (реализованные механики). OrbitalWeaponSystem помечен ОТБРОШЕНО, PROJECT_SETUP_PLAN ARCHIVED. docs_temp/ 34→24, docs/ 49→60.*
*Изменения v5.5: Актуализация чисел (429 файлов кода, 60 файлов docs, 183 чекпоинта). Обновлены ARCHITECTURE_CODE v3.18, ARCHITECTURE_IMPL v1.1, ARCHITECTURE_FILE_TREE v3.17, SETUP_GUIDE v2.0, README v0.2.0, START_PROMPT 2026-07-14.*
*Изменения v5.4: Папки checkpoints/docs/Legacy перенесены в UnityProject/; обновлены все пути и ссылки*
*Изменения v5.3: Добавлен NPC_ASSEMBLY_PIPELINE.md (перенесён из docs_temp/); обновлена статистика (44 файла, 1.3 MB)*
*Изменения v5.2: Обновлены размеры ARCHITECTURE_CODE.md и SCENE_BUILDER_SYSTEM.md; docs_asset_setup/ → FROZEN*
