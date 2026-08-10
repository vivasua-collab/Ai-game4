# Ai-game4 — Cultivation World Simulator

> **Xianxia cultivation life-sim** в духе Kenshi + RimWorld + cultivation-романов.
> Разработка ведётся на **Godot 4 + C# (.NET 8)** с AI-агентным циклом.

## Структура репозитория

```
Ai-game4/
├── game/                    # Godot 4 проект (C# .NET 8)
│   ├── src/                 # исходный код (4 слоя)
│   │   ├── Core/            # engine-agnostic: DI, EventBus, интерфейсы, контракты
│   │   ├── Modules/         # 16 игровых модулей (stub реализации)
│   │   ├── Entry/           # GameSession, SceneOrchestrator, 10 фаз сборки
│   │   └── Adapter/         # Godot-specific: Input, Rendering, UI, Scene
│   ├── scenes/              # .tscn файлы (MainMenu, GameWorld)
│   ├── resources/           # темы, ресурсы
│   ├── data/                # JSON конфиги (локации, уровни культивации, стихии)
│   ├── saves/               # runtime saves (пусто, .gitkeep)
│   ├── project.godot        # Godot project file
│   └── CultivationGame.csproj
│
├── docs/                    # документация проекта
│   ├── docs_v2/             # ★ актуальная engine-agnostic документация
│   │   ├── 00_overview/     # концепция, глоссарий, тех. решения
│   │   ├── 01_architecture/ # архитектура, модули, DI, производительность
│   │   ├── 02_systems/      # боёвка, тело, Ци, техники, формации, баффы
│   │   ├── 03_world/        # мир, карта, тайлы, время, переходы
│   │   ├── 04_entities/     # NPC, ИИ, фракции, типы сущностей
│   │   ├── 05_data/         # модели данных, конфиги, сохранения, генераторы
│   │   ├── 06_player/       # инвентарь, экипировка, журнал
│   │   ├── 07_ui/           # UI дизайн, render layers, спрайты, хоткеи
│   │   ├── 08_content/      # лор, имена, стартовый лор
│   │   └── 09_workflow/     # AI workflow, алгоритмы, тестирование
│   ├── docs/                # документация итерации Unity 6.3 (архив)
│   ├── docs_old/            # документация итерации Phaser (архив)
│   └── docs_temp/           # исследования, планы, черновики
│
├── .gitignore               # исключает локальное окружение
└── .gitattributes           # корректные line endings
```

## Что НЕ попадает в GitHub

Эти элементы локальны для машины разработчика и **никогда не коммитятся**:

- `godot/` — Godot engine binary (дистрибутив движка)
- `.dotnet/` — .NET SDK
- `.nuget/` — NuGet cache
- `my-project/` — Next.js песочница (не относится к игре)
- `.godot/` — Godot editor cache
- `bin/`, `obj/` — build artifacts
- `*.import` — Godot import cache
- `game/saves/*` — runtime saves (кроме `.gitkeep`)
- IDE файлы (`.vs/`, `.vscode/`, `.idea/`)
- OS файлы (`.DS_Store`, `Thumbs.db`)
- Секреты (`.env`, токены, ключи)

## Быстрый старт

### Требования
- **Godot 4.3+** (рекомендуется 4.7.1 stable .NET build) — https://godotengine.org/download
- **.NET SDK 8.0+** — https://dotnet.microsoft.com/download
- Git

### Запуск
```bash
# 1. Клонировать репозиторий
git clone https://github.com/vivasua-collab/Ai-game4.git
cd Ai-game4/game

# 2. Открыть проект в Godot Editor
#    File → Open → выбрать game/project.godot
#    Godot автоматически восстановит .godot/ и .import кэш

# 3. Или запустить headless для проверки:
dotnet build
godot --headless --path . --quit    # проверка загрузки
```

### Headless проверка (без Godot Editor)
```bash
cd game
dotnet build
godot --headless --path . --quit
# Должно вывести: [GameBoot] Game initialized. Container built...
```

## Архитектура (кратко)

Проект построен по **3-слойной архитектуре** с изоляцией движка:

| Слой | Зависимости | Назначение |
|------|-------------|------------|
| **Core** | чистый C# (нет Godot) | DI контейнер, EventBus, интерфейсы, контракты, data models |
| **Modules** | Core | 16 модулей: World, Tile, Body, Qi, Combat, NPC, Player, etc. |
| **Entry** | Core + Modules | GameSession, SceneOrchestrator, 10 фаз сборки сцены |
| **Adapter** | Core + Modules + Godot | Godot-specific: Input, Rendering, UI, Scene, Persistence |

**Принцип:** 100% игровой логики — engine-agnostic. Смена движка затрагивает только Adapter слой.

## Документация

**Основной источник истины:** `docs/docs_v2/` — engine-agnostic спецификация.

Начните с:
- `docs/docs_v2/00_overview/PROJECT_CONCEPT.md` — концепция игры
- `docs/docs_v2/00_overview/TECHNOLOGY_DECISIONS.md` — выбор Godot 4, стратегия
- `docs/docs_v2/01_architecture/ARCHITECTURE.md` — архитектура
- `docs/docs_v2/README.md` — индекс документации

## Лицензия

Приватный проект. Все права защищены.
