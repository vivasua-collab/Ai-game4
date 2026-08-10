# Технологические решения — Technology Decisions

> **Статус:** Принято. Основа для разработки.
> **Связанные документы:** `docs_temp/ENGINE_CHOICE_ANALYSIS.md` (полный анализ), `01_architecture/PERFORMANCE_STRATEGY.md`.

---

## 1. Выбор движка: Godot 4.x + C# (.NET 8)

### 1.1. Решение
**Движок: Godot 4.3+ с C# support.** Чистый 2D (без 2.5D на v1).

### 1.2. Обоснование (кратко)
Полный анализ — в `docs_temp/ENGINE_CHOICE_ANALYSIS.md`. Главный аргумент: **AI-агентный цикл разработки**. Godot — единственный кандидат, где:
- Сцены (`.tscn`) и ресурсы (`.tres`) — **текстовые файлы**, AI авторит их напрямую.
- Компиляция C# проверяется headless через `dotnet build`.
- Встроенный 2D-движок (CanvasItem, Y-sort, 2D lighting, TileMapLayer) покрывает все нужды проекта.
- C# support first-class (.NET 8) — 16 модулей core портируются с минимальной адаптацией.

### 1.3. Альтернативы (отвергнутые)
- **Unity 6.3** — AI не может оперировать Editor headless (нельзя создавать `.asset`/`.unity`/`.prefab`, нельзя видеть runtime-вывод). Точка останова предыдущей итерации.
- **Phaser + web-stack** — доказанный провал (нет World Map/Combat/Assets, combat на клиенте = security hole, не тянет 2000 NPC + 25M tiles).
- **Bevy (Rust)** — выбрасывает весь C#-core (~77K LOC).
- **Defold (Lua)** — выбрасывает C#-core.

### 1.4. Backup: MonoGame + MonoGame.Extended
Если Godot заблокирует разработку, fallback — MonoGame + MonoGame.Extended + свой DI/EventBus на C#.

| Критерий | Godot 4 + C# (primary) | MonoGame + Extended (backup) |
|----------|------------------------|------------------------------|
| Язык | C# + GDScript (опционально) | 100% C# |
| Editor | Есть (текстовые .tscn) | Нет, всё в коде |
| UI-фреймворк | Control nodes + Theme | Свой или ImGui (не для game-UI) |
| Tilemap | Встроенный TileMapLayer | MonoGame.Extended.Tiled |
| Сцены | .tscn (текст) | Кодовая конфигурация |
| Binary размер | ~50–80 MB | ~5–15 MB |
| Кривая обучения | Средняя | Низкая (если знаешь C#) |
| Риск | Низкий | Средний (UI с нуля) |

**Условия переключения на backup:**
1. C#-поддержка Godot окажется недостаточной для performance-критичных путей.
2. Node system Godot станет блокером для AI-цикла.
3. Нужен минимальный binary и максимальный контроль.

**Архитектурный принцип для сохранения опции backup:** Весь game-logic core пишется как pure C# (без зависимостей от Godot API). Godot используется только для: рендеринга, ввода, UI, аудио, сцен. Это означает, что backup-переключение затронет только adapter-слой, а не core.

---

## 2. Решение по 2D vs 2.5D

**Решение: чистый 2D top-down orthographic для v1.**

### 2.1. Обоснование
- Текущий дизайн строго 2D: orthographic camera, tilemap, 6 sorting layers, sprite-based composition.
- Документация (60+ файлов в docs/) описывает 2D-системы. Переход на 2.5D = переработка tile/movement/sorting = месяцы.
- 2.5D не даёт существенной игровой ценности для cultivation life-sim.

### 2.2. Задел под будущее 2.5D
Coordinate/tile-слой проектируется **проекционно-агностично**:
- World-координаты хранятся как `(x, y, z)` где z — логический уровень (−5..+5), не 3D-координата.
- Renderer-слой изолирован от logic-слоя: при переходе на 2.5D меняется только renderer, не логика.
- Godot 4 имеет полноценный 3D + orthographic camera → путь к 2.5D (если когда-либо понадобится) самый гладкий.

---

## 3. Стратегия производительности (краткая, подробно — в `01_architecture/PERFORMANCE_STRATEGY.md`)

### 3.1. Принципы
1. **Zero GC per frame** — все hot-path аллокации исключены. Сообщения между системами — `readonly struct` через шину событий. Никаких LINQ/lambda-captures в hot loops.
2. **Tick-based simulation, decoupled от frame rate.** Sim работает с фиксированным tick rate, рендер — независимо.
3. **Object pooling** для всех часто-создаваемых объектов: спрайты эффектов, projectiles, NPC entities, UI elements.
4. **Batch processing** — Qi-regen каждые 10 ticks, auto-save каждые 60 ticks, AI по 3-уровневой каденции (Spinal 1–10ms / Neural 10–50ms / Brain 100–500ms).
5. **C# для hot paths, GDScript для scene glue.** Combat/AI/sim — C#. UI/scene-логика — GDScript (опционально).
6. **Многопоточность** — sim-расчёты (AI, pathfinding, Qi-regen) выносятся на worker threads; основной поток — рендер + ввод.

### 3.2. Performance budgets (из COMPUTATIONAL_RESOURCES_CALCULATION.md)
- **CPU @ 100 NPC:** AI ~2 ms, Qi-regen ~0.5 ms, Buff ~1 ms, A* pathfinding ~5–50 ms.
- **GPU:** ~1000–5000 спрайтов, 50–200 draw calls, 100–500 MB VRAM.
- **Память:** мегаполис 775 MB uncompressed → ~150 MB RLE → ~77 MB sparse. NPC ~1.9 KB/NPC + 2–4 KB per-entity providers.
- **Default MaxActiveNPCs:** 100 (мегаполис до 2000 с chunking).

### 3.3. Hardware tiers
| Уровень | CPU | RAM | GPU |
|---------|-----|-----|-----|
| Minimum | 4 cores | 8 GB | GTX 1050 |
| Recommended | 6+ cores | 16 GB | GTX 1660 |
| Megapolis (2000 NPC) | 8+ cores | 32 GB | RTX 3060 |

### 3.4. Godot-специфичные оптимизации
- Использовать `_PhysicsProcess` для fixed tick (60 Hz по умолчанию, configurable).
- `ProcessMode` = `Pausable` для игрового сима, `Always` для UI.
- `CallDeferred` для cross-thread модификаций узлов.
- `RenderingServer` напрямую для батчинга спрайтов (вместо множества Sprite2D nodes — один MultiMeshInstance2D для тайлов).
- `ThreadPool` или Godot `WorkerThreadPool` для off-main-thread sim.
- GDExtension (C++/Rust) — крайняя мера для узких мест (маловероятно для 100 NPC).

---

## 4. Языковой выбор: C# vs GDScript

| Слой | Язык | Обоснование |
|------|------|-------------|
| Game logic core (16 модулей) | **C#** | Производительность, типизация, порт existing code |
| Hot paths (combat, AI, sim) | **C#** | Zero-GC, multithreading |
| Scene glue / signals | GDScript или C# | На усмотрение; GDScript быстрее пишется |
| UI logic | C# | Единообразие с core |
| Shaders | Godot Shader Language | Нативно |
| Tooling / Editor scripts | GDScript или C# | Простой tooling — GDScript |

**Принцип:** C# как primary, GDScript опционально для scene-glue. Гибрид допустим, но не обязательный.

---

## 5. Сохранение существующего C#-core

### 5.1. Что портируется напрямую (pure C#, без Unity-зависимостей)
- ✅ Body System (data + logic)
- ✅ Qi System (`long` arithmetic, tick-batch)
- ✅ Combat System (11-layer formula pipeline)
- ✅ Formation System (data + drain logic)
- ✅ Buff/Modifier System (28 types)
- ✅ NPC AI (Behavior Tree + 3-tier nervous system)
- ✅ Matryoshka generators (seededRandom)
- ✅ Stat Threshold System
- ✅ Save System (JSON, ISaveable pattern)
- ✅ Time System (замена Unity Coroutines на Timer-сервис)
- ✅ Elements/Faction/Technique data models

### 5.2. Что переписывается (Unity-специфичное)
- ❌ 22 MonoBehaviour UI Views → Godot Control nodes + C# scripts
- ❌ RuntimeSceneBuilder (1412 LOC) → Godot scene authoring (текстом) + SceneOrchestrator
- ❌ ScriptableObjects → Godot Resources (`.tres`, текстовые) или JSON
- ❌ Unity Tilemap + GameTile : TileBase → Godot TileMapLayer + custom TileSet
- ❌ CameraFollow → Godot Camera2D
- ❌ Light2D → Godot PointLight2D / DirectionalLight2D
- ❌ uGUI Canvas → Godot Control tree
- ❌ UIFactory (~1153 LOC) → Godot Theme + Control instantiation
- ❌ VContainer → Godot Autoload-based DI или простой ServiceLocator
- ❌ MessagePipe → Godot signals или кастомный EventBus (readonly struct сохраняется)
- ❌ UniTask → C# Task/async (нативный в Godot C#)
- ❌ Editor auto-config phases → не нужны (Godot не требует URP setup)

### 5.3. Оценка миграции
- Портирование core (16 модулей): ~2–3 недели
- Переписывание UI (22 Views): ~3–4 недели
- SceneBuilder + Tile system: ~1–2 недели
- Asset pipeline + render layers: ~1 неделя
- Тестирование + полировка: ~2 недели
- **Итого: ~9–12 недель** при активном AI-цикле

---

## 6. AI-агентный цикл разработки

### 6.1. Цикл итерации
1. AI пишет C#-код + `.tscn`/`.tres` текстовые файлы + JSON configs.
2. `dotnet build` проверяет компиляцию C# (headless).
3. `godot --headless --check-only --script` проверяет сцены/ресурсы.
4. `godot --headless --path . -- res://tests/...` запускает unit/integration тесты.
5. Скриншот-тесты: `godot --headless` рендерит сцену, AI сравнивает с эталоном.
6. Git commit + push. Пользователь делает финальный визуальный QA.

### 6.2. Headless-тестирование
- Все игровые системы — pure C#, тестируются через `dotnet test` (xUnit/NUnit).
- Godot-зависимый код (UI, scene) — через Godot test framework (GUT или встроенный).
- CI: `dotnet build` + `dotnet test` + `godot --headless --check-only`.

### 6.3. Документация как spec
docs_v2/ — единственный source of truth. AI-агенты работают строго по документации; любые расхождения кода с документацией = баг. Контрольные аудиты (как ANALYSIS_REPORT.md в предыдущей итерации) — регулярно.
