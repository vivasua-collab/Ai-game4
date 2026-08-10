# Анализ выбора движка игры — Ai-game4 (новая итерация)

> **Статус:** Исследование / концепция. Не внедрено.
> **Дата:** Текущая итерация.
> **Автор:** AI-агент (синтез на основе документации docs/, docs_old/, docs_temp/).
> **Связанные документы:** ARCHITECTURE.md, UNITY_63_RESEARCH.md, UNITY_VERSION_COMPARISON.md, MIGRATION_ANALYSIS.md, LOST_SESSION_ANALYSIS.md, phaser-game-analysis.md, PHASER_STACK.md, COMPUTATIONAL_RESOURCES_CALCULATION.md, все системные документы docs/.

---

## 0. Краткий вывод (Executive Summary)

**Текущий стек Unity 6.3 — структурно несовместим с AI-агентным циклом разработки.** Главная причина не в самом Unity как рендере, а в том, что **AI-агент не может оперировать Unity Editor`ом headless-режиме**: нельзя создать `.asset`/`.unity`/`.prefab`, нельзя проверить компиляцию, нельзя увидеть runtime-вывод. Все графики, сцены, префабы и ScriptableObjects требуют «человека в цикле» с открытым редактором. Именно на этом застопорилась предыдущая итерация — не на движке как таковом, а на графической части, которая в Unity неотделима от Editor.

**Рекомендация (приоритезированный список):**

| # | Вариант | Когда выбирать |
|---|---------|----------------|
| **1** | **Godot 4.x + C#** | **Основная рекомендация.** Лучший баланс: текстовые сцены (`.tscn`/`.tres`), AI может авторить всё в коде, нативный 2D + 2.5D, порт 16 чистых C#-модулей «как есть», headless-проверка компиляции. |
| **2** | **Собственный движок на .NET (Silk.NET / Veldrid / MonoGame)** | Если нужен максимальный контроль и минимализм. Весь C#-код портируется, но graphics/tooling/asset-pipeline — большая работа (именно здесь упали и Phaser, и Unity). |
| **3** | **Продолжить на Unity 6.3** | Только если готов постоянно быть «человеком в цикле» для Editor-операций. Архитектурно проект уже глубокий (429 .cs, ~77K LOC), но AI-цикл останется ограниченным. |
| **4** | **Bevy (Rust)** | Технически элегантно, но выбрасывает весь C#-core. Имеет смысл только при полной перезаписи с нуля. |
| ❌ | **Возврат на Phaser/PixiJS + web-stack** | **Не рекомендуется.** Уже провалился однажды (нет World Map, Combat Scene, ассетов; combat на клиенте = дыра безопасности; 50K+ LOC смешанной логики). |

**По вопросу 2D vs 2.5D:** Текущий дизайн строго 2D top-down orthographic. Переход на 2.5D (изометрия/перспектива) — это **новое направление**, требующее переработки tile-системы, перемещений и сортировки. Рекомендация: **остаться на 2D для v1**, но спроектировать tile/coordinate-слой проекционно-агностично, чтобы 2.5D был возможен как будущий этап. Godot 4 нативно поддерживает и 2D, и 3D — переход к 2.5D будет самым гладким среди всех вариантов.

---

## 1. Контекст проекта (извлечён из документации)

### 1.1. Что это за игра
**«Cultivation World Simulator»** — однопользовательский xianxia/cultivation life-sim в духе Kenshi + RimWorld + cultivation-романов.

- **Жанр:** симулятор мира культивации (жизнь персонажа в мире боевых искусств и духовного развития).
- **Перспектива:** top-down 2D, orthographic camera, grid/tilemap-based.
- **Симуляция:** tick-based, 1 tick = 1 игровая минута; 4 скорости (Pause / 1с=1мин / 1с=5мин / 1с=15мин). Движение = 1 tick на клетку. Бой real-time-with-pause.
- **Сетевой режим:** **полностью однопользовательская**, все данные локально. (В docs_old были облачные планы — отменены.)
- **Масштаб амбиций:** мир 200 000×200 000 км; локации до 10×10 км (мегаполис = 25M тайлов); до 2000 NPC в мегаполисе (по умолчанию `MaxActiveNPCs=100`).

### 1.2. Тяжёлые подсистемы (требования к движку)
- **Body System** (Kenshi-style): dual HP per limb (functional/structural 0.7/0.3), ампутации, 5 уровней кровотечения, регенерация. 7 морфологий, 7 классов размера, 6 материалов тела.
- **Combat System:** 11-слойный damage pipeline, 5 подтипов атак (melee/ranged projectile/beam/aoe), DoT, AoE до 300×300 м, chain lightning, knockback, pierce. **Формульный tick-resolved pipeline, НЕ physics-driven** — портируется чисто.
- **NPC AI:** 3-уровневая «нервная система» (Spinal 1–10 мс / Neural 10–50 мс / Brain 100–500 мс), Behavior Tree + FSM (15 состояний), 8 ролей, PersonalityTrait [Flags] (8 черт). **Grid-based pathfinding, без NavMesh.**
- **Formation System:** магические массивы на земле (не групповое движение!), размеры 3×3 м → 300×300 м, capacity до 204.8M Qi, до 50 помощников.
- **Qi System:** `long` (не `float`), L9 ~524M effectiveQi. Tick-batch processing каждые 10 ticks.
- **Matryoshka generation:** `Base × Grade × Specialization`, seededRandom, равномерно применяется к equipment/techniques/consumables/formations/qi-stones.
- **UI:** тяжёлый — 22 MonoBehaviour Views, Inventory UI (1793 LOC → 7 партиалов), 15 слотов экипировки, Drag&Drop, tooltips, procedurally-built body silhouette. uGUI Canvas (НЕ UI Toolkit). Тема «Древний Пергамент» на Unicode-глифах (◆ ○ ★ ✓ ◇ ▰) — **специально НЕ TextMeshPro** (нет глифов в SDF).

### 1.3. Архитектурные принципы (критично для портирования)
- **Hub-and-Spoke:** 16 модулей общаются только через Core-интерфейсы или MessagePipe `readonly struct` контракты (~130 контрактов в 20 файлах).
- **Zero GC per frame** — явная design goal.
- **ModuleServices pattern:** чистые C#-модули (`NPCModule : IStartable/ITickable/IDisposable`) через VContainer DI. **NPC-модуль уже полностью pure-C# (без MonoBehaviour).**
- **DI:** VContainer v1.17.0. **Pub/sub:** MessagePipe v1.8.1 + MessagePipe.VContainer. **Async:** UniTask v2.5.10 (заменяет Coroutines).
- **Save system:** JSON (human-readable, debuggable, portable). `ISaveable` + `SaveDataAggregator`. Tile data регенерируется из seed + delta, не сохраняется поштучно.

**Ключевое наблюдение:** **16 модулей игровой логики (Body/Qi/Combat/NPC/World/Formation/Buff/Quest/...) — engine-agnostic по духу.** Unity-специфичный код концентрируется в: (a) 22 MonoBehaviour UI Views, (b) RuntimeSceneBuilder (1412 LOC) + замороженный FullSceneBuilder (~3000 LOC, 20 фаз), (c) CameraFollow/TilemapVisualService/SortingLayerManager/VisualProvider, (d) Editor auto-config phases (`[InitializeOnLoadMethod]`).

---

## 2. История итераций движка (что уже было)

### 2.1. Итерация 1: Phaser 3 + Next.js + React + Prisma (docs_old)
**Стек:** Phaser 3.90.0, встроенный в Next.js 16 + React 19 + Prisma/SQLite + Zustand. Web-based, sandbox iframe.

**Что построено:** Training Ground с 6 straw targets, метрическая система расстояний (1м=32px), hitbox system, 51 пресет, 4-таб StatusDialog, RestDialog, TechniquesDialog, Kenshi-style body system, Event Bus, TruthSystem singleton, Time Scaling. ~46+ файлов, ~6300+ LOC. PhaserGame.tsx = 2798 строк.

**Как далеко зашло:** Phase 3 миграции — стадии 1–10 из 14 завершены; стадии 11–14 (World Map, Combat Scene, Assets, Testing) **никогда не начаты**. v0.7 Environment ~30%, v0.8 Sector System 0%, v0.9 Isometric/Tilemap 0%.

**Что сработало:** tick system (1 tick=1s) решил sync lag; TruthSystem memory-primary pattern; seeded generation.

**Что провалилось:**
- PhaserGame.tsx = 2798-строчный монстр, смешивающий логику и отображение.
- Combat/AI/technique calculations оказались на **клиенте** = уязвимость к читерству.
- Sandbox iframe блокировал localStorage → вынужденная v14 architecture rewrite.
- **Ассеты так и не были созданы** — только процедурные текстуры.
- Combat Scene и World Map не построены.
- Три параллельных документа (ARCHITECTURE_future/refact/cloud) от 2026-03-25 в статусе PLANNING = **архитектурный паралич перед pivot на Unity.**

**Причина отхода:** Phaser был недостаточен для планируемого RimWorld-style секторного мира, изометрии и полноценного tilemap/asset pipeline.

### 2.2. Итерация 2: Unity 6.3 URP 2D + VContainer + MessagePipe + UniTask (docs)
**Стек:** Unity 6.3 (6000.3), URP 2D, VContainer, MessagePipe, UniTask, uGUI, TextMeshPro (с fallback на LegacyRuntime.ttf). Single asmdef `CultivationGame.New.asmdef`.

**Что построено:** 429 .cs файлов, ~77K LOC, 44 интерфейса, ~130 контрактов, 16 модулей, 22 UI views, 19 мёртвых стабов, 0 singletons, 0 ServiceLocator. Фазовая разработка 0–19 (каждая фаза аудировалась). Архитектурный pivot mid-project: plain MonoBehaviour → VContainer+MessagePipe+UniTask модульная.

**Где застопорилось (графика/UI):**
1. **«Default Renderer is missing»** — 24 Console errors после копирования Assets/ из-за GUID changes (auto-fixed Phase00).
2. **Чёрные спрайты** — Sprite-Lit-Default shader без Light2D рендерит чёрным; RuntimeSceneBuilder теперь явно создаёт Light2D Global.
3. **Текст 0×0** — VerticalLayoutGroup без ContentSizeFitter; починено через UIFactory.CreateText().
4. `FindFirstObjectByType` null для деактивированных Views.
5. VContainerException до завершения регистрации MessagePipe.
6. UIFontCache static init + fallback на LegacyRuntime.ttf.
7. **UI V3 Фазы 0–4 реализованы, но НЕ полностью протестированы**; Fix Plan A done, B in progress, C-D pending. **UI — именно то место, где итерация застопорилась.**
8. Unity 6.3 **breaking API changes**: `GameTile.cs:34` — `GetTileData` no suitable method to override (TileBase API изменился: TileFlags → GameTileFlags); `CS0246 'TMPro' not found` — asmdef missing TMP reference.
9. `ProjectSettings/TagManager.asset` удалён из-за Unity crash; sorting layers рекреируются кодом.

**Структурный блокер:** PROJECT_SETUP_PLAN.md явно фиксирует — **без Unity Editor нельзя создать `.asset`/`.unity`/`.prefab`/Project Settings**; AI может подготовить только текстовые файлы (C#, JSON, docs) в Git. Setup требует human-in-the-loop. AI-агент в sandbox **никогда не видит runtime-вывод Unity** — работает вслепую против Editor; compile errors сообщает пользователь.

### 2.3. Миграция Phaser → Unity (MIGRATION_ANALYSIS.md)
- Мигрировали с Next.js + Phaser + Prisma + React (439 файлов, ~121K LOC) на Unity C#.
- **Только ~25% кода портируемо:** lib/game ~30–40%, lib/generator ~40–50%, types 60–70%. Phaser/React/API/Prisma = 0%.
- Решение: **Option B — писать новый код из документации** (не рефакторить). +7% effort, намного выше качество.
- Это означает, что текущий Unity-кодbase — по сути greenfield reference, **не жёсткое ограничение**. Выбор движка genuinely открыт.

---

## 3. Технические требования, извлечённые из системной документации

### 3.1. Rendering
- **Спрайты:** ~133–184 ассета, маленькие/низкоразрешающие. Terrain/objects 64×64 px @ PPU=32, player 128×128 @ PPU=64. **Процедурная генерация** (Perlin noise + Sprite.Create) — внешних ассетов почти нет.
- **Sorting layers:** всего 6 (Default, Background, Terrain, Objects, Player, UI). Управляются кодом (`SortingLayerManager`, `SceneBuilderConstants.cs`).
- **Lighting:** минимальное — одна Light2D Global. Никаких custom шейдеров, частиц, скелетной анимации в основных docs.
- **Animation:** sprite-swap (ExpandingEffect/DirectionalEffect, 12 effect sprites + 8 orbital-weapon sprites). **Animator НЕ используется.**
- **Орфографическая top-down, НЕ изометрия.** Tile = 2×2 м. Z — логический уровень (−5..+5), не 3D.

### 3.2. World / Streaming
- **НЕ open-world streaming.** Per-location scene loading с travel screens между локациями.
- Локация = Unity scene = единица загрузки. Wild lands между локациями **не загружаются как сцены** — процедурные transition encounters.
- Это держит entity counts ограниченными (по умолчанию `MaxActiveNPCs=100`).

### 3.3. Physics
- **Tile-based positioning, formulaic hit detection.** Combat — tick-resolved formula pipeline, **не physics-driven**.
- `Rigidbody2D`/`Collider2D` использовались ограниченно (Player Phase06, transition triggers Phase15). NavMesh НЕ используется (grid pathfinding).
- → **Движок без встроенной физики тоже подходит** — нужна только AABB collision для UI/триггеров.

### 3.4. Performance budgets
- CPU @ 100 NPC: AI ~2 мс, Qi-regen ~0.5 мс, Buff ~1 мс, A* pathfinding ~5–50 мс.
- GPU: ~1000–5000 спрайтов, 50–200 draw calls, 100–500 MB VRAM.
- Память: мегаполис 775 MB uncompressed → ~150 MB RLE → ~77 MB sparse. NPC ~1.9 KB/NPC.
- Hardware tiers: Min 4c/8GB/GTX 1050; Rec 6+c/16GB/GTX 1660; Megapolis 8+c/32GB/RTX 3060.
- **Zero GC per frame** — явная цель (readonly struct контракты).

### 3.5. Data persistence
- JSON (опционально binary + GZIP). main.sav (10–50 KB) + chunks/ + locations/ + metadata.sav.
- 100h: ~5–15 KB compressed. 1000h: ~100 KB. Extreme 2000 locations: ~1–2 MB.
- Tile data регенерируется из seed + delta, не сохраняется поштучно.
- `long` для всех Qi-значений.
- → **Портативно на любой движок.**

### 3.6. UI complexity
- 22 UI Views, uGUI Canvas (ScreenSpaceOverlay, 1920×1080 CanvasScaler).
- 3 sub-слоя (Hud/Window/Floating). UIFactory (~1153 LOC) для процедурного UI.
- Inventory UI был 1793 LOC → разбит на 7 партиалов.
- 15 слотов экипировки (многие — stubs). Drag&Drop, tooltips, body silhouette (procedural).
- Hotkeys: F5/F9/Esc/E/B/R/X/F.
- → **Тяжёлая UI-нагрузка — критерий для движка.**

### 3.7. Editor tooling reliance
- `AssetGenerator` (Window → Asset Generator) для ScriptableObjects.
- `RuntimeSceneBuilder` (1412 LOC, 10 фаз) + замороженный `FullSceneBuilder` (~3000 LOC, 20 фаз).
- Auto-config phases (Phase00 URP / Phase01 sprites / Phase01B TMP / Phase02 tags-layers) через `[InitializeOnLoadMethod]`.
- `AssetDatabase.RefreshAndWait` + `LoadAssetWithRetry(3, 200ms)`.
- → **Вся эта Editor-инфраструктура — Unity-специфична и не переносится.**

---

## 4. Критерии оценки движка (взвешенные)

Для AI-агентного цикла разработки критерии приоритезированы так:

| Критерий | Вес | Почему |
|----------|-----|--------|
| **C1. AI может авторить 100% ассетов/сцен в тексте** | 🔴 критич. | Без этого AI-цикл невозможен — нужны правки человека. |
| **C2. Headless-компиляция и проверка** | 🔴 критич. | AI должен видеть compile errors без запуска GUI. |
| **C3. Git-friendly (текстовые файлы, нет binary сцен)** | 🔴 критич. | Two-PC + sandbox workflow; merge conflicts на binary = смерть. |
| **C4. Портативность существующего C#-core (16 модулей)** | 🟠 высокий | ~77K LOC уже написано; VContainer+MessagePipe+UniTask паттерн. |
| **C5. Качество 2D-рендеринга + asset pipeline** | 🟠 высокий | Именно здесь упали обе предыдущие итерации. |
| **C6. Путь к 2.5D (будущее)** | 🟡 средний | Пользователь упомянул 2.5D как опцию. |
| **C7. Знание пользователем** | 🟠 высокий | Плохо знает Unity — кривая обучения критична. |
| **C8. Производительность (100–2000 NPC, 25M tiles)** | 🟡 средний | Tick-based sim, не per-frame; требования умеренные. |
| **C9. Тяжёлая UI-поддержка** | 🟠 высокий | 22 Views, Inventory, Drag&Drop, tooltips. |
| **C10. Доступность экосистемы/туториалов** | 🟡 средний | AI обучен на корпусе; редкие движки = меньше знаний. |
| **C11. Лицензия/стоимость** | 🟢 низкий | Проект некоммерческий на данном этапе. |
| **C12. Размер/сложность тулчейна** | 🟡 средний | 50GB SSD + 32GB RAM для Unity = тяжело. |

---

## 5. Анализ кандидатов

### 5.1. Unity 6.3 (продолжить) ❌ не рекомендуется как primary

**Плюсы:**
- Уже ~77K LOC, 429 .cs, 16 модулей, 22 UI Views.
- URP 2D работает (после фиксов Light2D/sorting layers).
- Огромная экосистема, AI хорошо обучен на Unity-коде.
- DOTS Entities 1.3 + Box2D v3 для future scaling (до 100k+ entities).

**Минусы (фатальные для AI-цикла):**
- **C1 ❌** AI не может авторить `.asset`/`.unity`/`.prefab` — только текстовые C#/JSON. ScriptableObjects, сцены, префабы требуют Editor.
- **C2 ❌** Нет headless-проверки компиляции без запуска Editor. AI работает вслепую; compile errors сообщает пользователь.
- **C3 ❌** Binary `.unity`/`.prefab` + `.meta` files = merge hell. `WORKFLOW_GITHUB_UNITY.md` и `GIT_WORKFLOW_TWO_PC.md` фиксируют: Library corruption при push с открытым Unity; scene corruption на `.unity` конфликтах; duplicate `.meta` конфликты на двух ПК.
- **C7 ❌** Пользователь плохо знает Unity — каждый Editor-шаг требует обучения.
- **C12 ❌** 16GB min / 32GB recommended RAM, 50GB SSD.
- Unity 6.3 breaking API changes (TileBase, TMPro) — AI не видит их до compile time.
- UI V3 застряло на Phase 0–4 не протестированных — именно точка останова.

**Вердикт:** Продолжать имеет смысл ТОЛЬКО если пользователь готов быть полноценным «оператором Unity Editor» в цикле: создавать сцены/префабы/ассеты, копировать Assets/, репортить compile errors. Это медленно и фрустрирующе для AI-итеративного процесса.

---

### 5.2. Godot 4.x + C# ✅ ОСНОВНАЯ РЕКОМЕНДАЦИЯ

**Плюсы:**
- **C1 ✅✅** Сцены `.tscn` и ресурсы `.tres` — **текстовые файлы**. AI может авторить их напрямую. Не нужны GUI-операции для создания сцен/ассетов.
- **C2 ✅** `godot --headless --check-only --script` проверяет компиляцию GDScript; C#-версия компилируется `dotnet build`. AI видит ошибки без GUI.
- **C3 ✅** Всё текстовое → идеальный Git workflow. Two-PC без боли.
- **C4 ✅✅** C# support (через .NET 8). **16 чистых C#-модулей портируются почти напрямую.** VContainer → заменить на Godot DI или встроенный node-based; MessagePipe → заменить на Godot signals/event bus; UniTask → C# async/await (нативно в Godot C#).
- **C5 ✅** Нативный 2D-движок (CanvasItem, Y-sort, 2D lighting, 2D shadows, TileMapLayer). Asset pipeline простой — drag&drop спрайтов или кодовое `Sprite2D.Texture = load(...)`.
- **C6 ✅** Полноценный 3D-движок в комплекте → 2.5D (изометрия или 3D-сцена с orthographic camera) — нативно. Переход 2D→2.5D самый гладкий из всех вариантов.
- **C7 ✅** Godot известен короткой кривой обучения. UI = встроенный Control-узлы (аналог uGUI, но мощнее). Если пользователь знает основы C#, хватит.
- **C8 ✅** 100–2000 NPC на tick-based sim — Godot справится. Для экстремальных случаев есть GDExtension (C++/Rust) или серверный headless-run.
- **C9 ✅** Control nodes + Theme system = тяжёлая UI без боли. Inventory/Drag&Drop/tooltips — есть встроенные базисы.
- **C10 ✅** Godot 4 быстро растёт в AI-корпусе; plenty обучающих материалов.
- **C12 ✅** ~100MB download, работает на 4GB RAM. Editor очень лёгкий.

**Минусы:**
- ~~C#-support второго класса~~ — в Godot 4.3+ C# first-class, .NET 8.
- Меньше marketplace ассетов чем у Unity (но проект почти не использует внешние ассеты — процедурная генерация).
- TileMap в Godot 4.3+ переработан в `TileMapLayer` — нужно учесть при портировании tile system.
- VContainer/MessagePipe/UniTask не родные для Godot — нужна адаптация DI/eventbus (но паттерн сохраняется).

**Вердикт:** Лучший баланс. AI-цикл становится полноценным: агент пишет C#-код + `.tscn`/`.tres` текстовые файлы + JSON configs, всё коммитится в Git, компиляция проверяется `dotnet build` + `godot --headless --check-only`, runtime тестируется через `godot --headless --path . --script res://test.gd` или скриншот-тесты. Пользователь нужен только для финального визуального QA.

---

### 5.3. Собственный движок на .NET (Silk.NET / Veldrid / MonoGame) ⚠️ возможен, но дорого

**Плюсы:**
- **C4 ✅✅✅** 100% C#-кода портируется без изменений. VContainer/MessagePipe/UniTask работают как есть.
- **C1 ✅** Всё — код. AI авторит 100%.
- **C2 ✅** `dotnet build` — полная headless-проверка.
- **C3 ✅** Всё текстовое.
- **C7 ✅** Если пользователь знает C#, не нужно учить новый Editor.
- Полный контроль, минимум зависимостей, крошечный бинарник.

**Минусы (фатальный риск):**
- **C5 ❌❌❌** **Graphics + asset pipeline + tooling = именно то, на чём упали Phaser И Unity.** Написать свой sprite renderer, batching, sorting layers, input, audio, UI framework, scene serialization, animation, TileMap — это **тысячи часов**. UI-фреймворк уровня Inventory/Drag&Drop/tooltip с нуля = неделенедельная работа.
- **C9 ❌** Нет встроенного UI-фреймворка. Придётся писать свой (или брать ImGui/Stmui — но это tooling-стиль, не game-UI).
- **C6 ❌** 2.5D → нужно писать 3D renderer. Огромная работа.
- **C10 ❌** AI обучен на библиотечных API, не на вашем собственном.
- High risk of repeating the same stall: graphics/tooling/UI.

**Вердикт:** Технически возможен, потому что core уже engine-agnostic. Но **повторяет точно тот же failure mode, на котором остановились обе прошлые итерации** — графика/UI/tooling с нуля. Рекомендуется ТОЛЬКО если:
- Готов взять готовый UI-фреймворк (например, Stmui/ImGui для tooling-style UI — но это не подойдёт для «Древнего Пергамента»).
- ИЛИ принять minimal-fidelity UI (Canvas2D-like).
- ИЛИ использовать Veldrid + готовый 2D-фреймворк (например, `MonoGame.Extended`).

**Гибридный вариант:** Собственный движок на **MonoGame + MonoGame.Extended** (даёт 2D sprite batch, TileMap, input, базовый UI) + свой DI/EventBus. Это снижает риск graphics/tooling, но сохраняет control.

---

### 5.4. Bevy (Rust) ❌ не рекомендуется

**Плюсы:**
- ECS-native, идеально для 100k+ entities.
- Rust = safety, hot reload, современный тулчейн.
- Code-first, всё текстовое.
- Высокая производительность.

**Минусы (фатальные):**
- **C4 ❌❌❌** **Выбрасывает весь C#-core (~77K LOC).** Полная перезапись на Rust. Bevy ECS ≠ VContainer DI — паттерн другой.
- **C7 ❌** Пользователь должен учить Rust + Bevy. Высокая кривая.
- **C10 ⚠️** Bevy быстро эволюционирует, breaking changes между версиями. AI-знания могут устареть.
- **C9 ❌** UI в Bevy слабый (bevy_ui — базовый). Inventory/Drag&Drop придётся писать с нуля.
- Bevy 2D зрелый, но 2.5D/3D ещё стабилизируется.

**Вердикт:** Технически элегантно, но экономика не сходится: выбрасывать 77K LOC ради перезаписи на Rust — это +месяцы работы без добавленной ценности. Имеет смысл только при стратегическом решении «начать заново на Rust», что выходит за рамки текущей задачи.

---

### 5.5. Возврат на Phaser 3 / PixiJS + web-stack ❌ не рекомендуется

**Минусы (уже доказанные):**
- Уже провалился однажды: Phase 3 stages 11–14 (World Map, Combat Scene, Assets, Testing) = 0%.
- Combat/AI/technique на клиенте = security hole.
- 50K+ LOC смешанной логики (PhaserGame.tsx = 2798 строк).
- Sandbox iframe блокирует localStorage.
- Web-stack не тянет 2000 NPC + 25M tiles мегаполис (даже с chunking).

**Вердикт:** Возврат = повторение известной ошибки. Исключить.

---

### 5.6. Defold ❌ не рекомендуется

**Плюсы:** Lua, лёгкий, code-first, text-based scenes.

**Минусы:**
- **C4 ❌** Lua ≠ C#. Весь core переписывается.
- **C6 ❌** 2.5D слабый.
- **C9 ⚠️** UI средний.
- Экосистема меньше Godot.

**Вердикт:** Если бы писали с нуля на Lua —可以考虑. Но с существующим C#-core — не оправдано.

---

## 6. Анализ 2D vs 2.5D

### 6.1. Текущий дизайн — строго 2D
- Orthographic top-down, Tilemap, Camera Z=-10 → +Z.
- Tile = 2×2 м, Z — логический уровень (−5..+5), не 3D-координата.
- 6 sorting layers, никаких 3D-элементов.
- Sprite-based composition (НЕ скелетная анимация).

### 6.2. Что значит «2.5D»
Два интерпретации:
1. **Изометрия** (как RimWorld isometric mod, Stardew Valley с модом): tile = ромб, спрайты с высотой, Y-sort. Требует переработки tile system, movement, rendering сортировки.
2. **3D-сцена с orthographic camera + биллборд-спрайты** (как Enter the Gungeon, Don't Starve): полноценный 3D renderer, спрайты как биллборды. Гибкий, но сложнее.

### 6.3. Стоимость перехода 2D → 2.5D по движкам

| Движок | 2D → изометрия | 2D → 3D-orthographic |
|--------|----------------|----------------------|
| Unity 6.3 | Tilemap изометрия есть, но требует Editor-настройки | URP 3D + orthographic — нужен новый pipeline |
| **Godot 4** | **TileMapLayer + Y-sort, всё текстом, AI-дружелюбно** | **Полноценный 3D, orthographic camera = одна настройка** |
| Custom .NET | Переписать tile renderer | Написать 3D renderer с нуля |
| Bevy | Переписать tile renderer | Написать 3D renderer |
| Phaser | Изометрия = боль (плагины) | Нет 3D |

### 6.4. Рекомендация по 2D/2.5D
- **v1: остаться на 2D top-down.** Текущий дизайн зрелый, документация огромна, переработка на 2.5D = месяцы.
- **Спроектировать coordinate/tile-слой проекционно-агностично:** хранить world-координаты (x, y, z) отдельно от screen-проекции. Тогда переход 2D→2.5D = замена renderer-слоя, не переписывание логики.
- **Godot 4 даёт лучший путь к 2.5D** — нативный 3D + orthographic camera + биллборд-спрайты. Если 2.5D — стратегическая цель, Godot минимизирует будущую работу.

---

## 7. Сравнительная таблица финальная

| Критерий | Unity 6.3 | **Godot 4 + C#** | Custom .NET | Bevy (Rust) | Phaser |
|----------|-----------|------------------|-------------|-------------|--------|
| C1 AI авторит ассеты/сцены в тексте | ❌ | ✅✅ | ✅ | ✅ | ✅ |
| C2 Headless компиляция | ❌ | ✅ | ✅ | ✅ | ✅ |
| C3 Git-friendly | ❌ | ✅✅ | ✅✅ | ✅✅ | ✅ |
| C4 Порт C#-core | ✅✅ | ✅ (адаптация DI) | ✅✅✅ | ❌ | ❌ |
| C5 2D rendering + asset pipeline | ✅ | ✅✅ | ❌❌ | ⚠️ | ⚠️ |
| C6 Путь к 2.5D | ⚠️ | ✅✅ | ❌ | ⚠️ | ❌ |
| C7 Знание пользователем | ❌ | ✅ | ⚠️ | ❌ | ⚠️ |
| C8 Производительность | ✅✅ | ✅ | ✅✅ | ✅✅✅ | ❌ |
| C9 Тяжёлая UI | ✅ | ✅✅ | ❌ | ❌ | ⚠️ |
| C10 Экосистема/AI-знания | ✅✅ | ✅ | ⚠️ | ⚠️ | ✅ |
| C12 Лёгкость тулчейна | ❌ | ✅✅ | ✅ | ✅ | ✅ |
| **Итог** | 3/12 | **10/12** | 5/12 | 5/12 | 4/12 |

---

## 8. Финальная рекомендация

### 8.1. Primary: Godot 4.x + C# (.NET 8)

**Обоснование:** Это единственный вариант, который решает **главный структурный блокер** — AI-агентность. Текстовые `.tscn`/`.tres` + `dotnet build` + `godot --headless --check-only` делают весь цикл разработки автоматизированным. При этом:
- 16 чистых C#-модулей портируются с минимальной адаптацией (VContainer→Godot DI/встроенный, MessagePipe→signals/event bus, UniTask→нативный async).
- JSON save system работает как есть.
- Tick-based sim + formula combat — чистый C#, движок-независимый.
- 2D нативный + путь к 2.5D лучший в классе.
- Пользователь с плохим знанием Unity освоит Godot быстрее (проще концептуально).

### 8.2. Что портируется напрямую (engine-agnostic ядро)
- ✅ Body System (pure C# data + logic)
- ✅ Qi System (`long` arithmetic, tick-batch)
- ✅ Combat System (11-layer formula pipeline)
- ✅ Formation System (data + drain logic)
- ✅ Buff/Modifier System (28 types)
- ✅ NPC AI (Behavior Tree + 3-tier nervous system, pure C#)
- ✅ Matryoshka generators (seededRandom)
- ✅ Stat Threshold System
- ✅ Save System (JSON, ISaveable pattern)
- ✅ Time System (нужна замена Unity Coroutines на Timer-сервис)
- ✅ Elements/Faction/Technique data models (заменить ScriptableObjects на JSON или Godot Resource `.tres`)

### 8.3. Что переписывается (Unity-специфичное)
- ❌ 22 MonoBehaviour UI Views → Godot Control nodes + C# scripts
- ❌ RuntimeSceneBuilder (1412 LOC) → Godot scene authoring (текстом!) + SceneOrchestrator
- ❌ FullSceneBuilder (~3000 LOC, frozen) → удалить, не нужен
- ❌ ScriptableObjects → Godot Resources (`.tres`, текстовые) или JSON
- ❌ Unity Tilemap + GameTile : TileBase → Godot TileMapLayer + custom TileSet
- ❌ CameraFollow → Godot Camera2D
- ❌ Light2D → Godot PointLight2D / DirectionalLight2D
- ❌ uGUI Canvas → Godot Control tree
- ❌ UIFactory (~1153 LOC) → Godot Theme + Control instantiation
- ❌ Editor auto-config phases → не нужны (Godot не требует URP setup)
- ❌ VContainer → Godot node-based DI или простой ServiceLocator-аналог
- ❌ MessagePipe → Godot signals или кастомный EventBus (readonly struct сохраняется)
- ❌ UniTask → C# Task/async (нативный в Godot C#)

### 8.4. Оценка миграции (грубая)
- Портирование core (16 модулей): ~2–3 недели (адаптация DI/EventBus/async).
- Переписывание UI (22 Views): ~3–4 недели (Godot Control + Theme).
- Переписывание SceneBuilder + Tile system: ~1–2 недели.
- Asset pipeline + sorting: ~1 неделя.
- Тестирование + полировка: ~2 недели.
- **Итого: ~9–12 недель** при активном AI-цикле (меньше, чем миграция Phaser→Unity, потому что core сохраняется).

### 8.5. Альтернатива: продолжить Unity 6.3 (fallback)
Если стратегическое решение — «не менять движок», то нужно принять ограничения:
- Пользователь =全职 «Unity Editor operator» в цикле.
- AI пишет только C#/JSON/docs; все `.asset`/`.unity`/`.prefab` — руками.
- Workflow: AI → git push → пользователь pull → open Unity → fix compile errors → create missing assets → commit → AI continues.
- Это медленно (итерации 1–2 дня вместо часов), но возможно.

### 8.6. Не рекомендуется
- Возврат на Phaser/web-stack (доказанный провал).
- Bevy/Rust (выбрасывает 77K LOC).
- Defold/Lua (выбрасывает C#-core).
- Custom engine на голом Silk.NET/Veldrid (повторяет failure mode графики/UI с нуля). **Исключение:** MonoGame + MonoGame.Extended как гибрид — если хочется control и C#-нативность, но не писать graphics с нуля.

---

## 9. План следующих шагов (после выбора Godot)

1. **Установить Godot 4.3+ с .NET support** на машину пользователя.
2. **Создать пустой Godot C# проект** в Git (текстовые `.tscn`/`.tres`/`.cs`/`.csproj`).
3. **Перенести 16 модулей core** как чистый C# (без Unity-зависимостей):
   - Заменить `UnityEngine` usings на абстракции.
   - VContainer → минимальный DI или Godot Autoload.
   - MessagePipe → кастомный `EventBus` с `readonly struct` контракты.
   - UniTask → `System.Threading.Tasks` + C# async.
4. **Создать Godot-сцену проекта** (`.tscn`) текстом — AI может это делать.
5. **Переписать Tile system** на Godot `TileMapLayer` + custom `TileSet`.
6. **Переписать UI** на Godot `Control` + Theme («Древний Пергамент» через Theme resources).
7. **Настроить headless-тестирование:** `godot --headless --check-only` в CI/цикле AI.
8. **Скриншот-тесты** для визуального QA (AI рендерит сцену headless, сравнивает с эталоном).

---

## 10. Риски и открытие вопросы для пользователя

1. **Готовы ли сменить движок на Godot 4?** Это решение меняет тулчейн, но сохраняет core и дизайн-документацию.
2. **Готовы ли принять 2D как v1** с заделом на 2.5D, или 2.5D — обязательное требование с самого начала?
3. **Знание C# vs GDScript:** Godot поддерживает оба. C# сохраняет порт core; GDScript быстрее для UI/scene-логики. Гибрид (C# для core, GDScript для scene glue) — распространённый паттерн.
4. **MonoGame-гибрид как backup:** если Godot по какой-то причине не подойдёт, MonoGame + MonoGame.Extended — второй C#-нативный вариант с меньшим graphics-риском, чем голый Veldrid.
5. **Сохранение документации:** все docs/ остаются валидными как spec — они описывают системы, а не Unity-реализацию. Нужна ревизия на предмет Unity-специфичных упоминаний (ScriptableObject, MonoBehaviour, URP) — заменить на engine-agnostic термины.

---

## Приложение A. Источники

- `docs/ARCHITECTURE.md`, `ARCHITECTURE_CODE.md`, `ARCHITECTURE_IMPL.md`, `ARCHITECTURE_FILE_TREE.md`
- `docs/SETUP_GUIDE.md`, `DEVELOPMENT_PLAN.md`, `!LISTING.md`, `!Ai_Skills.md`, `UNITY_DOCS_LINKS.md`
- `docs/WORLD_SYSTEM.md`, `WORLD_MAP_SYSTEM.md`, `TILE_SYSTEM.md`, `TILE_SYSTEM_IMPLEMENTATION.md`
- `docs/COMBAT_SYSTEM.md`, `NPC_AI_SYSTEM.md`, `NPC_ASSEMBLY_PIPELINE.md`, `BODY_SYSTEM.md`, `FORMATION_SYSTEM.md`
- `docs/SPRITE_INDEX.md`, `SORTING_LAYERS.md`, `SCENE_BUILDER_SYSTEM.md`, `SCENE_BUILDER_SYSTEM_Old.md`
- `docs/TECHNIQUE_SYSTEM.md`, `QI_SYSTEM.md`, `ELEMENTS_SYSTEM.md`, `INVENTORY_SYSTEM.md`, `EQUIPMENT_SYSTEM.md`
- `docs/SAVE_SYSTEM.md`, `WORLD_SAVE_SYSTEM.md`, `TIME_SYSTEM.md`, `LOCATION_MAP_SYSTEM.md`, `TRANSITION_SYSTEM.md`
- `docs/ENTITY_TYPES.md`, `CONFIGURATIONS.md`, `DATA_MODELS.md`, `GENERATORS_SYSTEM.md`, `FACTION_SYSTEM.md`, `NPC.md`, `MORTAL_DEVELOPMENT.md`
- `docs_temp/UNITY_63_RESEARCH.md`, `UNITY_VERSION_COMPARISON.md`, `MIGRATION_ANALYSIS.md`, `ANALYSIS_REPORT.md`
- `docs_temp/PROJECT_SETUP_PLAN.md`, `LOST_SESSION_ANALYSIS.md`, `COMPUTATIONAL_RESOURCES_CALCULATION.md`
- `docs_temp/CODE_REVIEW_Local_Folder.md`, `WORKFLOW_GITHUB_UNITY.md`, `GIT_WORKFLOW_TWO_PC.md`, `!listing.md`
- `docs_old/ARCHITECTURE.md`, `phaser-game-analysis.md`, `PHASER_STACK.md`, `PHASE3-PHASER-PROGRESS.md`
- `docs_old/architecture-analysis.md`, `README.md`, `INSTALL.md`, `PROJECT_ROADMAP.md`
- `docs_old/development-1000-days-calculation.md`, `ARCHITECTURE_future.md`, `ARCHITECTURE_refact.md`, `ARCHITECTURE_cloud.md`
- `docs_old/matryoshka-architecture.md`, `body-development-analysis.md`, `formation_analysis.md`

Полные выдержки агентов — в `/home/z/my-project/worklog.md` (Task IDs 3-a, 3-b, 3-c, 3-d).
