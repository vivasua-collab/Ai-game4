# Карта миграции документов

> **Раздел:** 01_architecture
> **Статус:** Справочник соответствия старых и новых документов.
> **Связанные документы:** `README.md` (docs_v2).

---

## 0. Принцип

В этом документе перечислены все исходные файлы из `docs/`, `docs_old/`, `docs_temp/`, и указано, куда они мигрировали в `docs_v2/`. Файлы, помеченные **«удалён»**, либо engine-специфичны и не переносятся, либо устарели.

**Цветовые метки:**
- ✅ Перенесён напрямую (с engine-agnostic адаптацией)
- 🔄 Перенесён частично (концепции сохранены, реализация отброшена)
- 📦 Объединён с другими (контент распределён по нескольким новым файлам)
- ❌ Удалён (engine-специфичный или устаревший)
- 🔜 TODO (планируется к переносу в следующей итерации)

---

## 1. Корневой файл

| Старый файл | Новый файл | Статус | Примечание |
|-------------|------------|--------|------------|
| `Caveman.md` | — | ❌ Удалён | Это коммуникационный протокол AI-агента, не игровой концепт. Не переносится. |

---

## 2. `docs/` (60 файлов, итерация Unity 6.3)

### 2.1. Справочники

| Старый файл | Новый файл | Статус | Примечание |
|-------------|------------|--------|------------|
| `docs/!Ai_Skills.md` | `09_workflow/AI_DEVELOPMENT_WORKFLOW.md` (частично) | 🔄 | Использован для контекста AI-инструментов. Workflow описан в новом виде. |
| `docs/!hotkeys.md` | `07_ui/HOTKEYS.md` | ✅ | Hotkeys — engine-agnostic, перенесены. |
| `docs/!LISTING.md` | — | ❌ Удалён | Листинг документации —.meta-файл, не нужен в docs_v2. |
| `docs/UNITY_DOCS_LINKS.md` | — | ❌ Удалён | Ссылки на Unity-документацию — engine-specific. |
| `docs/SETUP_GUIDE.md` | `09_workflow/PROJECT_SETUP.md` | 🔜 | Setup переписывается под engine-agnostic подход. |
| `docs/GLOSSARY.md` | `00_overview/GLOSSARY.md` | ✅ | Полностью перенесён, расширен, очищен от engine-terms. |

### 2.2. Архитектура и основы

| Старый файл | Новый файл | Статус | Примечание |
|-------------|------------|--------|------------|
| `docs/ARCHITECTURE.md` | `01_architecture/ARCHITECTURE.md` + `00_overview/PROJECT_CONCEPT.md` | 📦 | Концепция → PROJECT_CONCEPT, архитектура → ARCHITECTURE. Все Unity-terms удалены. |
| `docs/ARCHITECTURE_CODE.md` | `01_architecture/MODULE_STRUCTURE.md` + `01_architecture/DI_AND_EVENTBUS.md` + `01_architecture/PERFORMANCE_STRATEGY.md` | 📦 | Структура модулей, DI/event bus, performance — разделены. Unity-код (asmdef, MonoBehaviour) удалён. |
| `docs/ARCHITECTURE_FILE_TREE.md` | `01_architecture/FILE_TREE.md` | ✅ | Структура файлов адаптирована под engine-agnostic (core/modules/entry/adapter вместо Assets/Scripts). |
| `docs/ARCHITECTURE_IMPL.md` | `01_architecture/MODULE_STRUCTURE.md` (частично) | 🔄 | Статусы реализации сохранены как «История фаз». Unity-примеры кода удалены. |
| `docs/SCENE_BUILDER_SYSTEM.md` | `01_architecture/ARCHITECTURE.md` §6 (Сборка сцены) | 🔄 | Концепция сборки сцены сохранена. RuntimeSceneBuilder переименован в «сборщик сцены», abstract. |
| `docs/SCENE_BUILDER_SYSTEM_Old.md` | — | ❌ Удалён | Editor-time FullSceneBuilder — Unity-специфичный, заморожен. |
| `docs/DATA_MODELS.md` | `05_data/DATA_MODELS.md` | 🔜 | Модели данных переносятся с заменой ScriptableObject → data resource. |
| `docs/DEVELOPMENT_PLAN.md` | — | ❌ Удалён | Legacy plan (Фазы 1–8 GameManager/SceneLoader). Заменён актуальным планом в README.md docs_v2. |

### 2.3. Системы данных

| Старый файл | Новый файл | Статус | Примечание |
|-------------|------------|--------|------------|
| `docs/CONFIGURATIONS.md` | `05_data/CONFIGURATIONS.md` | 🔜 | Уровни культивации, техники, материалы — engine-agnostic. |
| `docs/ALGORITHMS.md` | `09_workflow/ALGORITHMS.md` | ✅ | Полностью перенесён. Все формулы сохранены. Unity-файловые пути удалены. |

### 2.4. Игровые системы

| Старый файл | Новый файл | Статус | Примечание |
|-------------|------------|--------|------------|
| `docs/COMBAT_SYSTEM.md` | `02_systems/COMBAT_SYSTEM.md` | 🔜 | 11-слойный пайплайн. Engine-agnostic. |
| `docs/TECHNIQUE_SYSTEM.md` | `02_systems/TECHNIQUE_SYSTEM.md` | 🔜 | Техники. Engine-agnostic. |
| `docs/QI_SYSTEM.md` | `02_systems/QI_SYSTEM.md` | 🔜 | Ци, Модель В. Engine-agnostic. |
| `docs/BODY_SYSTEM.md` | `02_systems/BODY_SYSTEM.md` | 🔜 | Kenshi-style, двойная HP. Engine-agnostic. |
| `docs/INVENTORY_SYSTEM.md` | `06_player/INVENTORY_SYSTEM.md` | 🔜 | Engine-agnostic. |
| `docs/EQUIPMENT_SYSTEM.md` | `06_player/EQUIPMENT_SYSTEM.md` | 🔜 | Материалы, грейды, прочность. Engine-agnostic. |
| `docs/BUFF_SYSTEM.md` | — | ❌ Удалён | Устарело. Заменено на BUFF_MODIFIERS_SYSTEM.md. |
| `docs/BUFF_MODIFIERS_SYSTEM.md` | `02_systems/BUFF_MODIFIERS_SYSTEM.md` | 🔜 | Актуальная версия баффов. |
| `docs/FORMATION_SYSTEM.md` | `02_systems/FORMATION_SYSTEM.md` | 🔜 | Формации. Engine-agnostic. |
| `docs/CHARGER_SYSTEM.md` | `02_systems/CHARGER_SYSTEM.md` | 🔜 | Зарядники. Engine-agnostic. |
| `docs/ELEMENTS_SYSTEM.md` | `02_systems/ELEMENTS_SYSTEM.md` | 🔜 | Стихии. Engine-agnostic. |
| `docs/MODIFIERS_SYSTEM.md` | — | ❌ Удалён | Дубликат BUFF_MODIFIERS_SYSTEM.md. |
| `docs/STAT_THRESHOLD_SYSTEM.md` | `02_systems/STAT_THRESHOLD_SYSTEM.md` | 🔜 | Пороги развития. Engine-agnostic. |
| `docs/GENERATORS_SYSTEM.md` | `05_data/GENERATORS_SYSTEM.md` | 🔜 | Генераторы (Матрёшка). Engine-agnostic. |
| `docs/GENERATORS_NAME_FIX.md` | `08_content/NAME_GENERATOR.md` | 🔜 | Исправления грамматики — интегрируется в NameGenerator. |
| `docs/PERK_SYSTEM.md` | `02_systems/PERK_SYSTEM.md` | 🔜 | Перк-система. Engine-agnostic. |
| `docs/MORTAL_DEVELOPMENT.md` | `06_player/MORTAL_DEVELOPMENT.md` | 🔜 | Этапы смертного. Engine-agnostic. |
| `docs/JOURNAL_SYSTEM.md` | `06_player/JOURNAL_SYSTEM.md` | 🔜 | Журнал. Engine-agnostic. |
| `docs/BREAKTHROUGH_MODELS_COMPARISON.md` | `02_systems/BREAKTHROUGH_MODELS.md` | 🔜 | Сравнение моделей прорыва. Engine-agnostic. |
| `docs/TECHNIQUE_USAGE_REPORT.md` | — | ❌ Удалён | Отчёт об использовании техник — отчёт, не спецификация. |
| `docs/TechniqueEffectsSystem.md` | `02_systems/TECHNIQUE_EFFECTS.md` | 🔜 | Эффекты техник. Engine-agnostic. |
| `docs/BuffSystem_Examples.md` | — | ❌ Удалён | Примеры кода Unity — engine-specific. |
| `docs/StatThresholdSystem_Examples.md` | — | ❌ Удалён | Примеры кода Unity — engine-specific. |
| `docs/FormationSystem_Examples.md` | — | ❌ Удалён | Примеры кода Unity — engine-specific. |

### 2.5. NPC и AI

| Старый файл | Новый файл | Статус | Примечание |
|-------------|------------|--------|------------|
| `docs/NPC_AI_SYSTEM.md` | `04_entities/NPC_AI_SYSTEM.md` | 🔜 | Spinal/Neural/Brain AI. Engine-agnostic. |
| `docs/NPC.md` | `04_entities/NPC.md` | 🔜 | Сборка NPC. Engine-agnostic. |
| `docs/NPC_ASSEMBLY_PIPELINE.md` | `04_entities/NPC_ASSEMBLY_PIPELINE.md` | 🔜 | 8-шаговый пайплайн. Engine-agnostic. |
| `docs/NPC_ASSEMBLY_EXAMPLES.md` | — | ❌ Удалён | Примеры кода Unity. |
| `docs/NPC_L6_ASSEMBLY_EXAMPLE.md` | — | ❌ Удалён | Пример кода Unity. |
| `docs/NameGenerator_Russian.md` | `08_content/NAME_GENERATOR.md` | 🔜 | Генератор имён. Engine-agnostic. |

### 2.6. Мир и время

| Старый файл | Новый файл | Статус | Примечание |
|-------------|------------|--------|------------|
| `docs/WORLD_SYSTEM.md` | `03_world/WORLD_SYSTEM.md` | 🔜 | Локации. Engine-agnostic. |
| `docs/WORLD_MAP_SYSTEM.md` | `03_world/WORLD_MAP_SYSTEM.md` | 🔜 | Карта мира, секторы. Engine-agnostic. |
| `docs/LOCATION_MAP_SYSTEM.md` | `03_world/LOCATION_MAP_SYSTEM.md` | 🔜 | Генерация зданий. Engine-agnostic. |
| `docs/TILE_SYSTEM.md` | `03_world/TILE_SYSTEM.md` | 🔜 | Тайлы. Engine-agnostic. |
| `docs/TILE_SYSTEM_IMPLEMENTATION.md` | — | ❌ Удалён | Unity-реализация тайлов (GameTile : TileBase). |
| `docs/TRANSITION_SYSTEM.md` | `03_world/TRANSITION_SYSTEM.md` | 🔜 | Переходы. Engine-agnostic. |
| `docs/TIME_SYSTEM.md` | `03_world/TIME_SYSTEM.md` | 🔜 | Время, тики, календарь. Engine-agnostic. Unity Coroutines → Timer-сервис. |
| `docs/FACTION_SYSTEM.md` | `04_entities/FACTION_SYSTEM.md` | 🔜 | Фракции. Engine-agnostic. |
| `docs/LORE_SYSTEM.md` | `08_content/LORE_SYSTEM.md` | 🔜 | Лор. Engine-agnostic. |
| `docs/ENTITY_TYPES.md` | `04_entities/ENTITY_TYPES.md` | 🔜 | Типы сущностей. Engine-agnostic. |

### 2.7. Рендеринг и графика

| Старый файл | Новый файл | Статус | Примечание |
|-------------|------------|--------|------------|
| `docs/SORTING_LAYERS.md` | `07_ui/RENDER_LAYERS.md` | 🔄 | Unity SortingLayer → «слои рендеринга» (engine-agnostic). 6 слоёв: Default/Background/Terrain/Objects/Player/UI. |
| `docs/SPRITE_INDEX.md` | `07_ui/SPRITE_CATALOG.md` | 🔄 | Unity-спрайты → каталог ассетов (engine-agnostic). |

### 2.8. Специальные системы

| Старый файл | Новый файл | Статус | Примечание |
|-------------|------------|--------|------------|
| `docs/SAVE_SYSTEM.md` | `05_data/SAVE_SYSTEM.md` | 🔜 | JSON, ISaveable. Engine-agnostic. |
| `docs/WORLD_SAVE_SYSTEM.md` | `05_data/WORLD_SAVE_SYSTEM.md` | 🔜 | Chunk-based persistence. Engine-agnostic. |

### 2.9. Тестирование

| Старый файл | Новый файл | Статус | Примечание |
|-------------|------------|--------|------------|
| `docs/UNIT_TEST_RULES.md` | `09_workflow/TESTING_RULES.md` | 🔜 | Правила тестов. Engine-agnostic. |
| `docs/RUNNING_TESTS.md` | `09_workflow/TESTING_RULES.md` | 🔄 | Запуск тестов: `dotnet test` (Unity Test Framework удалена). |

---

## 3. `docs_old/` (69 файлов, итерация Phaser)

> **Примечание:** docs_old — архив Phaser-эры (Next.js + Phaser 3 + Prisma). Большинство файлов — engine-specific (Phaser/React/TypeScript/HTTP API). Сохранены только концепции, не реализация.

### 3.1. Архитектура

| Старый файл | Новый файл | Статус | Примечание |
|-------------|------------|--------|------------|
| `docs_old/ARCHITECTURE.md` | `01_architecture/ARCHITECTURE.md` + `00_overview/PROJECT_CONCEPT.md` (концепции) | 🔄 | TruthSystem → память первична (концепция сохранена). Sandbox-архитектура удалена. Caddy/Socket.io/Bun — engine-specific. |
| `docs_old/ARCHITECTURE_future.md` | — | ❌ Удалён | Будущая облачная архитектура — отменена (игра однопользовательская). |
| `docs_old/ARCHITECTURE_refact.md` | — | ❌ Удалён | Server migration plan — отменён. |
| `docs_old/ARCHITECTURE_cloud.md` | — | ❌ Удалён | «Божество → Облако → Земля» thin-client — отменён. |
| `docs_old/ARCHITECTURE_code_base.md` | — | ❌ Удалён | Код-base Phaser-эры. |
| `docs_old/matryoshka-architecture.md` | `02_systems/GENERATORS_SYSTEM.md` (концепция) + `00_overview/GLOSSARY.md` (Matryoshka) | 🔄 | Принцип Матрёшки (Base × Grade × Specialization) сохранён. TypeScript-код удалён. |
| `docs_old/sector-architecture.md` | `03_world/WORLD_MAP_SYSTEM.md` (концепция) | 🔄 | Секторная архитектура — концепция сохранена. |
| `docs_old/architecture-analysis.md` | — | ❌ Удалён | Анализ Phaser-архитектуры. |
| `docs_old/phaser-game-analysis.md` | — | ❌ Удалён | Анализ PhaserGame.tsx. |
| `docs_old/PHASER_STACK.md` | — | ❌ Удалён | Phaser 3.90.0 стек — engine-specific. |
| `docs_old/PHASE3-PHASER-PROGRESS.md` | — | ❌ Удалён | Tracker миграции Phaser — устарел. |

### 3.2. Документация и roadmap

| Старый файл | Новый файл | Статус | Примечание |
|-------------|------------|--------|------------|
| `docs_old/README.md` | — | ❌ Удалён | Категоризация docs_old. |
| `docs_old/Listing.md` | — | ❌ Удалён | Листинг docs_old — meta-файл. |
| `docs_old/INSTALL.md` | — | ❌ Удалён | Bun/Next.js/Prisma install guide — engine-specific. |
| `docs_old/PROJECT_ROADMAP.md` | — | ❌ Удалён | Roadmap с Go rewrite, Tauri, Electron — отменён. |
| `docs_old/development-1000-days-calculation.md` | `02_systems/STAT_THRESHOLD_SYSTEM.md` (концепция) | 🔄 | Stat Threshold system принята как основа. |
| `docs_old/body-development-analysis.md` | `02_systems/STAT_THRESHOLD_SYSTEM.md` (концепция) | 🔄 | Виртуальная дельта, sleep consolidation — концепции сохранены. |
| `docs_old/formation_analysis.md` | `02_systems/FORMATION_SYSTEM.md` (концепция) | 🔄 | Анализ формаций — концепции сохранены. |
| `docs_old/body_review.md` | — | ❌ Удалён | Review кода body. |

### 3.3. Системы (концепции → docs_v2/02_systems/)

| Старый файл | Новый файл | Статус | Примечание |
|-------------|------------|--------|------------|
| `docs_old/body.md` | `02_systems/BODY_SYSTEM.md` | 🔄 | Kenshi-style body — концепция сохранена. |
| `docs_old/body_monsters.md` | `02_systems/BODY_SYSTEM.md` (раздел) | 🔄 | Тела монстров. |
| `docs_old/body_armor.md` | `06_player/EQUIPMENT_SYSTEM.md` (раздел) | 🔄 | Броня тела. |
| `docs_old/combat-system.md` | `02_systems/COMBAT_SYSTEM.md` | 🔄 | Боевая система. |
| `docs_old/qi_stone.md` | `02_systems/CHARGER_SYSTEM.md` (раздел) | 🔄 | Камни Ци. |
| `docs_old/charger.md` | `02_systems/CHARGER_SYSTEM.md` | 🔄 | Зарядники. |
| `docs_old/technique-system-v2.md` | `02_systems/TECHNIQUE_SYSTEM.md` | 🔄 | Техники V2. |
| `docs_old/technique-system-archive.md` | — | ❌ Удалён | Архив старой системы техник. |
| `docs_old/elements-system.md` | `02_systems/ELEMENTS_SYSTEM.md` | 🔄 | Стихии. |
| `docs_old/formation_unified.md` | `02_systems/FORMATION_SYSTEM.md` | 🔄 | Унифицированная система формаций. |
| `docs_old/formation_drain_system.md` | `02_systems/FORMATION_SYSTEM.md` (раздел) | 🔄 | Утечка Ци формаций. |
| `docs_old/formation_visualization.md` | — | ❌ Удалён | Визуализация через Phaser — engine-specific. |
| `docs_old/bonuses.md` | `02_systems/BUFF_MODIFIERS_SYSTEM.md` (раздел) | 🔄 | Бонусы. |
| `docs_old/BUFF_SYSTEM.md` | — | ❌ Удалён | Устаревшая версия баффов. |
| `docs_old/MODIFIERS_SYSTEM.md` | `02_systems/BUFF_MODIFIERS_SYSTEM.md` (раздел) | 🔄 | Модификаторы. |
| `docs_old/condition-system.md` | `02_systems/BUFF_MODIFIERS_SYSTEM.md` (раздел) | 🔄 | Состояния (bleed, stun, etc.). |
| `docs_old/stat-threshold-system.md` | `02_systems/STAT_THRESHOLD_SYSTEM.md` | 🔄 | Пороги развития. |
| `docs_old/stat-development-system.md` | `02_systems/STAT_THRESHOLD_SYSTEM.md` (раздел) | 🔄 | Развитие характеристик. |
| `docs_old/vitality-hp-system.md` | `02_systems/BODY_SYSTEM.md` (раздел) | 🔄 | Vitality → HP. |
| `docs_old/soul-system.md` | `02_systems/BODY_SYSTEM.md` (раздел) | 🔄 | Soul/душа. |
| `docs_old/physics-system.md` | — | ❌ Удалён | Phaser.Physics.Arcade — engine-specific. |
| `docs_old/event-bus-system.md` | `01_architecture/DI_AND_EVENTBUS.md` (концепция шины) | 🔄 | Event bus концепция сохранена. |
| `docs_old/relations-system.md` | `04_entities/NPC_AI_SYSTEM.md` (раздел) | 🔄 | Отношения NPC. |
| `docs_old/equip.md` | `06_player/EQUIPMENT_SYSTEM.md` | 🔄 | Экипировка v1. |
| `docs_old/equip-v2.md` | `06_player/EQUIPMENT_SYSTEM.md` | 🔄 | Экипировка v2. |
| `docs_old/weapon-armor-system.md` | `06_player/EQUIPMENT_SYSTEM.md` (раздел) | 🔄 | Оружие и броня. |
| `docs_old/materials.md` | `06_player/EQUIPMENT_SYSTEM.md` (раздел) | 🔄 | Материалы. |
| `docs_old/inventory-system.md` | `06_player/INVENTORY_SYSTEM.md` | 🔄 | Инвентарь. |
| `docs_old/generators.md` | `05_data/GENERATORS_SYSTEM.md` | 🔄 | Генераторы V4. |
| `docs_old/generator-specs.md` | `05_data/GENERATORS_SYSTEM.md` (раздел) | 🔄 | Спеки генераторов. |
| `docs_old/data-systems.md` | `05_data/DATA_MODELS.md` | 🔄 | Системы данных. |
| `docs_old/TIME_SYSTEM.md` | `03_world/TIME_SYSTEM.md` | 🔄 | Время. |
| `docs_old/ENVIRONMENT_SYSTEM_PLAN.md` | `03_world/WORLD_SYSTEM.md` (раздел) | 🔄 | Окружение. |
| `docs_old/random_npc.md` | `04_entities/NPC.md` (раздел) | 🔄 | Случайные NPC. |
| `docs_old/npc-session-integration.md` | — | ❌ Удалён | Session integration — server-specific. |
| `docs_old/NPC_COMBAT_INTERACTIONS.md` | `04_entities/NPC_AI_SYSTEM.md` (раздел) | 🔄 | NPC combat interactions. |
| `docs_old/NPC_AI_NEUROTHEORY.md` | `04_entities/NPC_AI_SYSTEM.md` (раздел) | 🔄 | Neurotheory — 3-tier nervous system. |
| `docs_old/NPC_AI_THEORY.md` | `04_entities/NPC_AI_SYSTEM.md` (раздел) | 🔄 | AI theory. |
| `docs_old/DAMAGE_FORMULAS_PROPOSAL.md` | `09_workflow/ALGORITHMS.md` (раздел) | 🔄 | Формулы урона. |
| `docs_old/FUNCTIONS.md` | `09_workflow/ALGORITHMS.md` (раздел) | 🔄 | Функции расчётов. |
| `docs_old/start_lore.md` | `08_content/START_LORE.md` | 🔄 | Стартовый лор. |
| `docs_old/implementation-plan-body-development.md` | — | ❌ Удалён | План реализации — устарел. |
| `docs_old/TRAINING_GROUND_ROADMAP.md` | — | ❌ Удалён | Training Ground — Phaser-specific. |
| `docs_old/TEST_WORLD_TARGETS.md` | — | ❌ Удалён | Тестовые цели — устарели. |
| `docs_old/CHEATS.md` | — | ❌ Удалён | Читы — реализационный документ. |
| `docs_old/PLAYER_SPRITES.md` | `07_ui/SPRITE_CATALOG.md` (раздел) | 🔄 | Спрайты игрока. |
| `docs_old/ui-terminology.md` | `07_ui/UI_DESIGN.md` | 🔄 | UI-терминология. |
| `docs_old/PROMPT-EXAMPLES.md` | — | ❌ Удалён | Промпты — не спецификация. |

---

## 4. `docs_temp/` (24 файла, исследования/чертежи)

### 4.1. Архитектурные исследования

| Старый файл | Новый файл | Статус | Примечание |
|-------------|------------|--------|------------|
| `docs_temp/ENGINE_CHOICE_ANALYSIS.md` | `00_overview/TECHNOLOGY_DECISIONS.md` (выводы) | 🔄 | Полный анализ → краткие выводы. Сам анализ-документ сохраняется как reference. |
| `docs_temp/COMPUTATIONAL_RESOURCES_CALCULATION.md` | `01_architecture/PERFORMANCE_STRATEGY.md` | ✅ | Полностью перенесён (с обновлением под engine-agnostic). |
| `docs_temp/UNITY_63_RESEARCH.md` | — | ❌ Удалён | Unity 6.3 API research — engine-specific. |
| `docs_temp/UNITY_VERSION_COMPARISON.md` | — | ❌ Удалён | Unity version comparison — engine-specific. |
| `docs_temp/MIGRATION_ANALYSIS.md` | — | ❌ Удалён | Phaser → Unity migration analysis — устарел. |
| `docs_temp/PROJECT_SETUP_PLAN.md` | — | ❌ Удалён | Unity project setup — engine-specific. |
| `docs_temp/LOST_SESSION_ANALYSIS.md` | — | ❌ Удалён | Анализ потерянной сессии — meta. |
| `docs_temp/ANALYSIS_REPORT.md` | — | ❌ Удалён | Анализ-отчёт — meta. |
| `docs_temp/CODE_REFERENCE.md` | — | ❌ Удалён | Справочник кода V2 — Unity-код. |
| `docs_temp/CODE_REVIEW_Local_Folder.md` | — | ❌ Удалён | Code review Local/ folder — meta. |

### 4.2. Workflow

| Старый файл | Новый файл | Статус | Примечание |
|-------------|------------|--------|------------|
| `docs_temp/WORKFLOW_GITHUB_UNITY.md` | `09_workflow/AI_DEVELOPMENT_WORKFLOW.md` (концепция) | 🔄 | Git workflow — концепция сохранена, Unity-специфика удалена. |
| `docs_temp/GIT_WORKFLOW_TWO_PC.md` | `09_workflow/AI_DEVELOPMENT_WORKFLOW.md` (раздел) | 🔄 | Two-PC workflow — концепция сохранена. |
| `docs_temp/LONG_TERM_MEMORY_SCHEME.md` | — | ❌ Удалён | AI long-term memory — не часть спецификации. |
| `docs_temp/!listing.md` | — | ❌ Удалён | Листинг docs_temp — meta. |

### 4.3. Системы-черновики

| Старый файл | Новый файл | Статус | Примечание |
|-------------|------------|--------|------------|
| `docs_temp/ACHIEVEMENT_SYSTEM.md` | — | ❌ Удалён | Черновик achievement-системы — не реализовано. |
| `docs_temp/STACKING_SYSTEM_DRAFT.md` | — | ❌ Удалён | Черновик stacking — не реализовано. |
| `docs_temp/LOOT_SYSTEM_DRAFT.md` | `02_systems/COMBAT_SYSTEM.md` (раздел loot) | 🔄 | Loot — концепция сохранена. |
| `docs_temp/tool_system_draft.md` | — | ❌ Удалён | Черновик tool-системы — не реализовано. |
| `docs_temp/OrbitalWeaponSystem.md` | — | ❌ Удалён | Орбитальное оружие — отброшено. |
| `docs_temp/QI_ABSORPTION_RADIUS.md` | `02_systems/QI_SYSTEM.md` (раздел) | 🔄 | Радиус поглощения Ци — концепция. |
| `docs_temp/CharacterSpriteMirroring.md` | `07_ui/SPRITE_CATALOG.md` (раздел) | 🔄 | Отражение спрайтов. |
| `docs_temp/INVENTORY_UI_DRAFT.md` | `07_ui/UI_DESIGN.md` (раздел) | 🔄 | UI инвентаря — концепция. |
| `docs_temp/INVENTORY_IMPLEMENTATION_PLAN.md` | — | ❌ Удалён | План реализации Unity — engine-specific. |
| `docs_temp/INVENTORY_FLAGS_AUDIT.md` | — | ❌ Удалён | Аудит флагов — meta. |
| `docs_temp/EQUIPPED_SPRITES_DRAFT.md` | `07_ui/SPRITE_CATALOG.md` (раздел) | 🔄 | Спрайты экипировки. |

---

## 5. Сводная статистика миграции

| Источник | Всего файлов | Перенесено (✅🔄📦🔜) | Удалено (❌) |
|----------|--------------|----------------------|-------------|
| Корневой | 1 | 0 | 1 |
| `docs/` | 60 | ~40 | ~20 |
| `docs_old/` | 69 | ~30 | ~39 |
| `docs_temp/` | 24 | ~10 | ~14 |
| **Итого** | **154** | **~80** | **~74** |

> **Вывод:** ~52% файлов перенесено (с engine-agnostic адаптацией), ~48% удалено как engine-specific или устаревшее.

---

## 6. Новые документы в docs_v2 (созданы с нуля)

| Новый файл | Статус | Описание |
|------------|--------|----------|
| `00_overview/PROJECT_CONCEPT.md` | ✅ Создан | Концепция игры, жанр, цели. Engine-agnostic. |
| `00_overview/GLOSSARY.md` | ✅ Создан | Расширенный глоссарий. |
| `00_overview/TECHNOLOGY_DECISIONS.md` | ✅ Создан (раньше) | Технологические решения. |
| `01_architecture/ARCHITECTURE.md` | ✅ Создан | Высокоуровневая архитектура. Engine-agnostic. |
| `01_architecture/MODULE_STRUCTURE.md` | ✅ Создан | 16 модулей, детально. |
| `01_architecture/DI_AND_EVENTBUS.md` | ✅ Создан | DI + шина событий, принципы. |
| `01_architecture/PERFORMANCE_STRATEGY.md` | ✅ Создан | Zero-GC, pooling, tick batching. |
| `01_architecture/FILE_TREE.md` | ✅ Создан | Структура файлов engine-agnostic. |
| `01_architecture/MIGRATION_MAP.md` | ✅ Создан | Этот документ. |
| `09_workflow/ALGORITHMS.md` | ✅ Создан | Формулы, расчёты. |
| `09_workflow/AI_DEVELOPMENT_WORKFLOW.md` | ✅ Создан | Workflow для AI-разработки. |

---

## 7. Статус завершения миграции

✅ **Все системные документы созданы** (агенты 2-a..2-e, всего 52 файла в docs_v2/).

| Категория | Файлы | Статус |
|-----------|-------|--------|
| **00_overview/** | PROJECT_CONCEPT, GLOSSARY, TECHNOLOGY_DECISIONS | ✅ Готово |
| **01_architecture/** | ARCHITECTURE, MODULE_STRUCTURE, DI_AND_EVENTBUS, PERFORMANCE_STRATEGY, FILE_TREE, MIGRATION_MAP | ✅ Готово |
| **02_systems/** | BODY_SYSTEM, COMBAT_SYSTEM, QI_SYSTEM, TECHNIQUE_SYSTEM, ELEMENTS_SYSTEM, BREAKTHROUGH_MODELS, FORMATION_SYSTEM, BUFF_MODIFIERS_SYSTEM, STAT_THRESHOLD_SYSTEM, PERK_SYSTEM, CHARGER_SYSTEM, TECHNIQUE_EFFECTS | ✅ Готово (12 файлов) |
| **03_world/** | WORLD_SYSTEM, WORLD_MAP_SYSTEM, TILE_SYSTEM, LOCATION_MAP_SYSTEM, TRANSITION_SYSTEM, TIME_SYSTEM | ✅ Готово (6 файлов) |
| **04_entities/** | ENTITY_TYPES, NPC, NPC_AI_SYSTEM, NPC_ASSEMBLY_PIPELINE, FACTION_SYSTEM | ✅ Готово (5 файлов) |
| **05_data/** | DATA_MODELS, CONFIGURATIONS, SAVE_SYSTEM, WORLD_SAVE_SYSTEM, GENERATORS_SYSTEM | ✅ Готово (5 файлов) |
| **06_player/** | INVENTORY_SYSTEM, EQUIPMENT_SYSTEM, JOURNAL_SYSTEM, MORTAL_DEVELOPMENT | ✅ Готово (4 файла) |
| **07_ui/** | UI_DESIGN, RENDER_LAYERS, SPRITE_CATALOG, HOTKEYS | ✅ Готово (4 файла) |
| **08_content/** | LORE_SYSTEM, START_LORE, NAME_GENERATOR | ✅ Готово (3 файла) |
| **09_workflow/** | AI_DEVELOPMENT_WORKFLOW, ALGORITHMS, TESTING_RULES | ✅ Готово (3 файла) |

### 7.1. Документы, которые ещё предстоит создать (при начале разработки)

Эти документы не являются частью спецификации, а создаются по мере реализации:

| Документ | Когда | Описание |
|----------|-------|----------|
| `09_workflow/PROJECT_SETUP.md` | На старте разработки | Инструкция по настройке Godot 4 проекта |
| `09_workflow/CODING_STANDARDS.md` | На старте | C# coding conventions, naming |
| `09_workflow/CI_CD.md` | После первого модуля | CI pipeline (dotnet build + test + headless check) |

---

*Документ обновлён после завершения агентов 2-a..2-e. Все 52 файла docs_v2 созданы.*
