# Документация проекта — docs_v2 (engine-agnostic)

> **Назначение:** Данный набор документации описывает игру «Cultivation World Simulator» на уровне архитектуры, концепций и наполнения **без привязки к конкретному движку**. На основе этой документации ведётся разработка.
>
> **Движок реализации:** Godot 4.x + C# (.NET 8). Документация спроектирована так, чтобы при смене движка (на MonoGame или иной) не требовать переработки — меняется только реализационный слой.

## Принципы построения docs_v2

1. **Engine-agnostic.** Никаких упоминаний MonoBehaviour, ScriptableObject, GameObject, prefab, URP, Light2D, Rigidbody2D, Canvas, VContainer, MessagePipe, UniTask, asmdef, AssetDatabase и т.д. Используются нейтральные термины: «система», «компонент», «ресурс данных», «узел сцены», «DI-контейнер», «шина событий», «async/await», «модуль».
2. **Концепция важнее реализации.** Каждый документ отвечает на вопрос «что делает система и по каким правилам», а не «как это закодировано».
3. **Сохранены все игровые формулы, балансовые числа, лор, структуры данных.** Это design spec, не технический design doc.
4. **Производительность — first-class concern.** Везде, где это релевантно, указаны performance budgets, tick rates, entity counts, memory estimates.

## Структура

```
docs_v2/
├── README.md                          ← этот файл
├── 00_overview/                       ← обзор проекта
│   ├── PROJECT_CONCEPT.md             — концепция игры, жанр, цели
│   ├── GLOSSARY.md                    — глоссарий терминов
│   └── TECHNOLOGY_DECISIONS.md        — выбор Godot 4, стратегия производительности, backup
├── 01_architecture/                   ← архитектура
│   ├── ARCHITECTURE.md                — высокоуровневая архитектура
│   ├── MODULE_STRUCTURE.md            — 16 модулей, Hub-and-Spoke
│   ├── DI_AND_EVENTBUS.md             — паттерны DI + шина событий (engine-agnostic)
│   ├── PERFORMANCE_STRATEGY.md        — zero-GC, pooling, tick batching, многопоточность
│   └── FILE_TREE.md                   — предлагаемая структура файлов
├── 02_systems/                        ← игровые системы (механики)
│   ├── BODY_SYSTEM.md
│   ├── COMBAT_SYSTEM.md
│   ├── QI_SYSTEM.md
│   ├── TECHNIQUE_SYSTEM.md
│   ├── ELEMENTS_SYSTEM.md
│   ├── BREAKTHROUGH_MODELS.md
│   ├── FORMATION_SYSTEM.md
│   ├── BUFF_MODIFIERS_SYSTEM.md
│   ├── STAT_THRESHOLD_SYSTEM.md
│   ├── PERK_SYSTEM.md
│   ├── CHARGER_SYSTEM.md
│   └── TECHNIQUE_EFFECTS.md
├── 03_world/                          ← мир, карта, тайлы, время
│   ├── WORLD_SYSTEM.md
│   ├── WORLD_MAP_SYSTEM.md
│   ├── TILE_SYSTEM.md
│   ├── LOCATION_MAP_SYSTEM.md
│   ├── TRANSITION_SYSTEM.md
│   └── TIME_SYSTEM.md
├── 04_entities/                       ← сущности мира
│   ├── ENTITY_TYPES.md
│   ├── NPC.md
│   ├── NPC_AI_SYSTEM.md
│   ├── NPC_ASSEMBLY_PIPELINE.md
│   └── FACTION_SYSTEM.md
├── 05_data/                           ← данные, конфиги, сохранения
│   ├── DATA_MODELS.md
│   ├── CONFIGURATIONS.md
│   ├── SAVE_SYSTEM.md
│   ├── WORLD_SAVE_SYSTEM.md
│   └── GENERATORS_SYSTEM.md
├── 06_player/                         ← системы игрока
│   ├── INVENTORY_SYSTEM.md
│   ├── EQUIPMENT_SYSTEM.md
│   ├── JOURNAL_SYSTEM.md
│   └── MORTAL_DEVELOPMENT.md
├── 07_ui/                             ← UI концепции (engine-agnostic)
│   ├── UI_DESIGN.md
│   ├── RENDER_LAYERS.md
│   ├── SPRITE_CATALOG.md
│   └── HOTKEYS.md
├── 08_content/                        ← лор, имена, контент
│   ├── LORE_SYSTEM.md
│   ├── START_LORE.md
│   └── NAME_GENERATOR.md
└── 09_workflow/                       ← процесс разработки
    ├── AI_DEVELOPMENT_WORKFLOW.md
    ├── TESTING_RULES.md
    └── ALGORITHMS.md
```

## Источники (migration mapping)

Документация собрана из:
- `docs/` — актуальная документация итерации Unity 6.3 (60+ файлов)
- `docs_old/` — документация итераций Phaser (70+ файлов, дизайн-корпус)
- `docs_temp/` — исследования, планы, черновики (25+ файлов)
- `ENGINE_CHOICE_ANALYSIS.md` — анализ выбора движка (текущая итерация)

Подробный mapping старых документов → новых находится в `01_architecture/MIGRATION_MAP.md`.

## Что удалено из docs_v2

Следующие документы были Unity/Phaser-специфичными и НЕ переносятся:
- `UNITY_DOCS_LINKS.md` — ссылки на Unity-документацию
- `SETUP_GUIDE.md` — инструкция по установке Unity (заменена на `09_workflow/PROJECT_SETUP.md` для Godot)
- `ARCHITECTURE_CODE.md` — Unity-специфичная структура кода (asmdef, MonoBehaviour)
- `SCENE_BUILDER_SYSTEM.md` / `SCENE_BUILDER_SYSTEM_Old.md` — Unity RuntimeSceneBuilder (концепция перенесена в `01_architecture/ARCHITECTURE.md` как «сборка сцены»)
- `SORTING_LAYERS.md` — Unity SortingLayer (концепция перенесена в `07_ui/RENDER_LAYERS.md`)
- `SPRITE_INDEX.md` — индекс Unity-спрайтов (перенесён в `07_ui/SPRITE_CATALOG.md` как каталог ассетов)
- `WORKFLOW_GITHUB_UNITY.md`, `GIT_WORKFLOW_TWO_PC.md` — Unity-специфичный git workflow
- `UNITY_63_RESEARCH.md`, `UNITY_VERSION_COMPARISON.md`, `MIGRATION_ANALYSIS.md` — исследования миграции (выводы в `00_overview/TECHNOLOGY_DECISIONS.md`)

## Что добавлено нового

- `00_overview/TECHNOLOGY_DECISIONS.md` — обоснование выбора Godot 4, стратегия производительности, backup-план
- `01_architecture/PERFORMANCE_STRATEGY.md` — выделенная стратегия производительности (zero-GC, pooling, многопоточность)
- `01_architecture/MIGRATION_MAP.md` — таблица соответствия старых и новых документов
- `09_workflow/AI_DEVELOPMENT_WORKFLOW.md` — workflow для AI-агентной разработки (headless-тестирование, цикл итераций)
