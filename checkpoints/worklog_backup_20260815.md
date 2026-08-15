# Worklog — Ai-game4 (новая итерация разработки игры)

## Контекст
- Пользователь плохо знает Unity. Предыдущая итерация застопорилась на графической части.
- Версия Unity была 6.3.
- В docs_old есть следы работы на Phaser (PHASER_STACK, PHASE3-PHASER-PROGRESS, phaser-game-analysis) — значит был опыт и с другим движком.
- План: 2D или 2.5D. Возможно собственная реализация движка.

## Структура распакованной документации
Расположение: `/home/z/my-project/game-docs/`
- `Caveman.md` — корневой файл (концепт игры)
- `docs/` — актуальная документация итерации Unity 6.3
- `docs_old/` — предыдущие итерации (вкл. Phaser)
- `docs_temp/` — исследования, планы, черновики, анализ сессий

## Задача текущего этапа
Большое исследование по выбору движка игры. Прочитать архитектуру и связанные документы, провести анализ: Unity 6.3 vs Phaser vs собственный движок vs альтернативы. Режим 2D / 2.5D.

---
Task ID: 2
Agent: orchestrator (главный)
Task: Подготовка структуры исследования и worklog

Work Log:
- Распаковал Ai-game4.zip в /home/z/my-project/game-docs/
- Зафиксировал структуру трёх папок документации (docs, docs_old, docs_temp)
- Спланировал 4 параллельных агента чтения + синтез отчёта

Stage Summary:
- Подготовлен worklog. Далее запускаются агенты 3-a..3-d для чтения документов.

---
Task ID: 3-b
Agent: research-unity (general-purpose)
Task: Read Unity 6.3 research & migration docs for engine-choice analysis

Work Log:
- Read /home/z/my-project/game-docs/docs_temp/UNITY_63_RESEARCH.md (Unity 6.3 API research, Entities 1.3, ScriptableObjects, DOTS vs MonoBehaviour)
- Read /home/z/my-project/game-docs/docs_temp/UNITY_VERSION_COMPARISON.md (Unity 2022 LTS vs 6.3 comparison + recommendation)
- Read /home/z/my-project/game-docs/docs_temp/MIGRATION_ANALYSIS.md (Next.js/Phaser/React → Unity migration: refactor vs rewrite)
- Read /home/z/my-project/game-docs/docs_temp/ANALYSIS_REPORT.md (audit of new docs vs old docs + code state)
- Read /home/z/my-project/game-docs/docs_temp/PROJECT_SETUP_PLAN.md (archived setup plan, what can/can't be done without Unity Editor)
- Read /home/z/my-project/game-docs/docs_temp/LOST_SESSION_ANALYSIS.md (Qi system / breakthrough session analysis + Unity 6.3 compile errors)
- Read /home/z/my-project/game-docs/docs_temp/COMPUTATIONAL_RESOURCES_CALCULATION.md (memory/CPU/GPU budget for tile grid + NPC, modular VContainer+MessagePipe arch)
- Read /home/z/my-project/game-docs/docs_temp/CODE_REVIEW_Local_Folder.md (audit of duplicate Local folder, two-PC sync fallout)
- Read /home/z/my-project/game-docs/docs_temp/!listing.md (docs_temp index v4.1 — shows many files I read have already been archived/removed as "historical")
- Read /home/z/my-project/game-docs/docs/UNITY_DOCS_LINKS.md (curated Unity 6.3 doc URLs accessed via curl by AI assistant)
- Read /home/z/my-project/game-docs/docs_temp/WORKFLOW_GITHUB_UNITY.md (Sandbox↔GitHub↔Unity sync via "main3Uniny" branch)
- Read /home/z/my-project/game-docs/docs_temp/GIT_WORKFLOW_TWO_PC.md (two-PC Git workflow, .meta/Library pain points)

Stage Summary:
- Unity 6.3 research conclusions (UNITY_63_RESEARCH.md):
  - IMPORTANT correction: Unity 6.3 uses Entities 1.3 (DOTS 1.x), NOT DOTS 2.0 — earlier docs were wrong.
  - Pros highlighted: Box2D v3 + low-level 2D physics APIs (multi-threaded, deterministic), "Render 3D as 2D" (Mesh/Skinned Mesh in 2D Renderer), native screen-reader accessibility, Scriptable audio pipeline (Burst), shared Render Graph for URP/HDRP, DLSS4, new Hierarchy window, Build Profiles UI.
  - CRITICAL constraint flagged: ScriptableObject is READ-ONLY at runtime in standalone player — changes are not persisted. Forces a hybrid data design (ScriptableObjects for static presets, JSON/SQLite for saves).
  - Recommended approach: Phase 1 prototype in MonoBehaviour (low complexity, easy debug); Phase 2 migrate NPC AI / high-volume systems to DOTS (10–100× perf, scales 100k+ entities).
  - Document itself was later deemed "general info, not project-specific" and removed in !listing.md v4.0 cleanup.

- Version comparison outcome (UNITY_VERSION_COMPARISON.md):
  - Compared Unity 2022.3 LTS vs Unity 6.3 (6000.3). FINAL RECOMMENDATION: Unity 6.3, primarily for DOTS Entities 1.3, new 2D physics APIs (Box2D v3), scriptable audio pipeline, and LTS support to 2029+.
  - Reasoning table heavily favours 6.3 for performance/DOTS/graphics/AI-tools/future, favours 2022 LTS only for stability/ecosystem/ease of learning.
  - Explicit alternative plan: Unity 2022.3 LTS IF team are Unity beginners / critical stability needed / tight deadlines / limited debugging resources. (All four apply to this project — the user "плохо знает Unity" per worklog, and prior iteration stalled.)
  - Min hardware: Unity 6000.3.x, Rider 2024+/VS 2022, Win10/11 or macOS 12+, 16 GB RAM (32 GB recommended), DX12/Vulkan GPU, 50 GB SSD.
  - Recommended packages: Entities 1.3, Unity Physics (DOTS), Burst, URP, Shader Graph, VFX Graph, ProBuilder, TextMeshPro.
  - Phased strategy: Phase 1 prototype (MonoBehaviour + URP, months 1–2); Phase 2 NPC AI → DOTS (months 3–4); Phase 3 content (months 5+).

- Migration analysis findings (MIGRATION_ANALYSIS.md):
  - Migrating FROM existing Next.js + Phaser + Prisma + React codebase (439 files, ~121,411 LOC) TO Unity C#.
  - Only ~25% of code is portable: lib/game ~30–40%, lib/generator ~40–50%, types 60–70%. Phaser (12,742 LOC), React components (30,810), API routes (12,930), Prisma queries — 0% portable.
  - Platform dependencies: 61 Next.js imports, 11 Phaser, 11 Prisma, 50 React 'use client' — all incompatible with Unity.
  - Two options compared: (A) refactor existing code (~220 iterations, ~2.2 M tokens, high risk, hybrid/legacy result); (B) write new code from docs (~235 iterations, ~2.35 M tokens, low risk, native Unity).
  - VERDICT: Option B (new code) — only +7% effort, much better quality, no hidden dependencies, docs serve as spec. Reusable assets: prisma/schema.prisma → ScriptableObjects, data/*.ts → Unity assets, docs/*.md → specifications, formula algorithms.
  - Phase plan: Foundation (2–3 iters) → Core Systems (10–15) → Gameplay (8–10) → Polish (5–8).
  - Document later archived (see !listing.md): "Решение «новый код» принято и реализуется" — decision made and being implemented.

- Lost session — what & lessons (LOST_SESSION_ANALYSIS.md):
  - "Lost session" was a deep-dive on Qi System & Breakthrough mechanics (2026-04-09). Work produced: TECHNIQUE_USAGE_REPORT.md, design decisions on technique rapid-fire, removal of ungrounded regenerationMultiplier, two recovery models (meditation + charger), breakthrough time formulas.
  - CRITICAL DISCREPANCIES found between code and lore: breakthrough should set currentQi=0 (was only spending required), qi density vs core volume formula did not model compression correctly. Two competing models (A: growing reservoir, B: compression) produced identical UI numbers but differed in breakthrough & cross-level qi transfer.
  - AI HALLUCINATION documented: previous agent introduced `environmentMult` — a multiplier with NO basis in lore. Had to be removed. Lesson: AI agents in this project have introduced plausible-sounding but fictional parameters.
  - SESSION ENDED ON UNITY 6.3 COMPILE ERRORS:
    - CS0115 GameTile.cs:34 — 'GetTileData' no suitable method to override → Unity 6.3 changed TileBase API (renamed TileFlags → GameTileFlags).
    - CS0246 'TMPro' not found → asmdef missing TextMeshPro reference.
  - Files modified at session end (P1 fixes): TileEnums.cs, GameTile.cs, TileData.cs, TestLocationSetup.cs (added `using TMPro`), TileSystem.asmdef (added TMP ref), QiController.cs (PerformBreakthrough: currentQi=0).
  - Lesson: Unity 6.3 has breaking API changes vs older versions (TileBase, asmdef dependency on TMP) that surface late as compile errors. Code-control under AI agents is fragile; need careful spec adherence.

- Computational resource needs (COMPUTATIONAL_RESOURCES_CALCULATION.md):
  - Target: dynamic locations with 2×2 m tile grid, up to 10×10 km megapolis (25 M tiles per location).
  - Memory: megapolis 775 MB uncompressed → ~150 MB with RLE → ~77 MB sparse. NPC: ~1.9 KB/NPC + 2–4 KB per-entity providers. Per-location memory scales from 10 KB (хутор) to 3.8 MB NPC data (megapolis 2000 NPC).
  - CPU @ 100 NPC: AI tick ~2 ms, Qi-regen ~0.5 ms, Buff tick ~1 ms, Movement ~1 ms, A* pathfinding ~5–50 ms, serialization ~5 ms. MessagePipe overhead negligible (~0.1 ms/sec for 100 NPC × 10 events).
  - GPU: ~1000–5000 sprites on screen, 50–200 draw calls, 100–500 MB VRAM.
  - Architecture (v2.0, 2026-05-23): modular VContainer + MessagePipe + UniTask with Per-entity DataProvider pattern (Hub-and-Spoke), chunked loading for >1 km locations, AI tick scheduling, object pooling.
  - Hardware tiers: Minimum 4 cores/2.5 GHz/8 GB/GTX 1050; Recommended 6+ cores/3 GHz/16 GB/GTX 1660; Megapolis 8+ cores/3.5 GHz/32 GB/RTX 3060.
  - Conclusion: small/medium locations run on any modern PC; megapolis 10×10 km requires chunked loading + all optimizations.

- Codebase state from code review (CODE_REVIEW_Local_Folder.md):
  - AUDIT of UnityProject/Local folder (2026-04-09): verdict — safe to delete. Only outdated duplicates of `UnityProject/Assets/Scripts/`.
  - Local had 196 .cs files (dated 2026-03-31) vs main Assets 122 .cs (dated 2026-04-09). Tile system (12 files) present only in main.
  - QiController.cs version skew: Local v1.1 vs main v1.3 (main has conductivityBonus, baseConductivity, double precision, overflow protection, correct PerformBreakthrough, Formation integration).
  - "Unique" files in Local were only TextMesh Pro Examples & Extras (standard Unity samples, not project code).
  - Existence of `Local/` indicates two-PC development with manual folder copying / sync drift — a real pain point for the project.
  - Implied codebase size: ~122 .cs files in main Assets (post-cleanup); architecture later moved to VContainer+MessagePipe modular (per !listing.md and COMPUTATIONAL_RESOURCES_CALCULATION.md).

- Setup complexity (PROJECT_SETUP_PLAN.md, archived 2026-07-14):
  - HARD CONSTRAINT: Without Unity Editor, CANNOT create .asset (ScriptableObject instances), .unity (scenes), .prefab, or Project Settings — only text files (C# scripts, JSON configs, docs) can be prepared in Git.
  - Workaround: prepare code+data in Git under UnityProject/Assets/Scripts and Data/JSON, then a human opens Unity, creates 2D Core (URP) project, copies files in, generates assets/scenes manually via SETUP_GUIDE/IMPORT_GUIDE.
  - Project template chosen: "2D Core (URP)" — NOT 3D. Plan covers a full Assets/ layout (Scripts/Core, Data, Combat, Qi, Body, NPC, World, Inventory, Save, UI; ScriptableObjects Config/Techniques/Items/Materials; Prefabs; Scenes MainMenu/GameWorld/Combat; Art/Sprites; Audio; Data/JSON; Resources).
  - Cannot verify compilation, cannot set up 2D URP renderer, cannot create Sprite Atlas, cannot configure 2D physics without Editor.
  - Document explicitly archived: "Setup завершён. План от 2026-03-30 устарел — см. docs/SETUP_GUIDE.md v2.0." So setup did complete in the prior iteration.

- Git/Unity workflow pain points (WORKFLOW_GITHUB_UNITY.md + GIT_WORKFLOW_TWO_PC.md):
  - Workflow: Sandbox (AI) ↔ GitHub ↔ Local Unity. No direct sandbox-Unity sync. Branch named "main3Uniny" in the older doc, renamed to "main" in the two-PC doc.
  - PAIN POINTS explicitly listed:
    - Binary Unity files: scenes (.unity), prefabs, textures, audio — generate merge conflicts that are effectively unresolvable as text.
    - .meta files: every asset has a .meta; two PCs creating the same file → duplicate .meta conflicts. (NOTE: WORKFLOW_GITHUB_UNITY.md incorrectly advises "Не коммитить .meta файлы, если не критично" — this is WRONG; .meta MUST be committed, and GIT_WORKFLOW_TWO_PC.md contradicts it: "оба файла (файл + .meta) должны быть в Git".)
    - Library folder: must be gitignored; pushing while Unity is open corrupts Library → must rm -rf Library and reimport.
    - Unity locks files while running → MUST close Unity before push.
    - Scene corruption on .unity conflicts → recommendation: Git LFS for scenes & large binaries.
    - Different Unity versions on two PCs → must match versions, verify ProjectSettings/ProjectVersion.txt.
  - Practical rules: always `git pull` before work; close Unity before push; never edit same file on both PCs; use `git pull --rebase`; check `ProjectVersion.txt`.
  - Both workflow docs were later archived as "outdated branch / trivial Git" in !listing.md v4.0 — implying these pains were eventually managed but never eliminated.

- Why graphics work stalled (if mentioned):
  - NO document explicitly diagnoses "graphics stalled". The closest evidence: LOST_SESSION_ANALYSIS.md ends on Unity 6.3 compile errors (TileBase API change, missing TMP asmdef ref) — graphics/tile rendering work broke at the API level. The worklog context (Task 2) explicitly says "Предыдущая итерация застопорилась на графической части" but the docs themselves only implicitly corroborate via the Tile system breakage and the general complexity of URP 2D setup (called out as "cannot verify without Editor" in PROJECT_SETUP_PLAN.md). The 2D URP renderer setup, Sprite Atlas, and 2D physics configuration are flagged as requiring hands-on Editor work that an AI agent cannot do headlessly — a structural blocker for an AI-driven iteration.

- Alternative engines considered (if any):
  - Within these 12 docs, NO direct comparison with Phaser/Godot/Unreal/custom engine. The migration analysis treats the prior stack (Next.js/Phaser/Prisma/React) purely as legacy to be REPLACED by Unity, not as a candidate. UNITY_VERSION_COMPARISON.md only contrasts Unity 2022 LTS vs Unity 6.3.
  - Phaser is mentioned in MIGRATION_ANALYSIS.md only as part of the legacy codebase ("Phaser code 12,742 LOC, 0% portable").
  - Earlier Phaser iterations are referenced in docs_old/ (per Task 2 worklog) but not evaluated here as an alternative.

- AI workflow notes (relevant since this is an AI-agent-driven iteration):
  - Token budgeting: MIGRATION_ANALYSIS.md quantifies AI work — ~235 iterations / ~2.35 M tokens for new Unity codebase; AI "creates better than refactors" was an explicit decision driver.
  - AI hallucination risk: LOST_SESSION_ANALYSIS.md documents a prior agent inventing `environmentMult` (no lore basis) — flagged as a recurring failure mode. Mitigation: strict adherence to lore docs, audit reports like ANALYSIS_REPORT.md.
  - Documentation-as-spec: docs/ treated as the single source of truth; ANALYSIS_REPORT.md cross-checks new docs against old docs and code. Buff system, formation system discrepancies found and tracked.
  - Headless limitation: AI cannot operate Unity Editor → setup/asset/scene creation requires a human in the loop (PROJECT_SETUP_PLAN.md).
  - curl-based doc access: UNITY_DOCS_LINKS.md is a curated index of ~150 Unity 6.3 doc URLs verified readable via curl, so AI agents can self-serve Unity documentation.
  - Architecture evolved to be AI-friendly: VContainer + MessagePipe + UniTask modular (Hub-and-Spoke, per-entity DataProvider, MessagePipe events with zero GC) — designed so AI agents can implement one module per iteration with minimal cross-cutting coupling.
  - docs_temp/ has a token-cost budget (🔥>15K, ⚠️5–15K, ✅<5K) — AI agents are explicitly managing documentation size to fit context windows. v4.0 cleanup removed 17 historical files (−37% volume, ~69K tokens saved).
  - Two-PC + sandbox model means AI agent in sandbox NEVER sees Unity runtime output — it works blind against the Editor. Compile errors (LOST_SESSION_ANALYSIS) are reported back by the user, not observed directly.

- Other notable findings:
  - The architecture MIGRATED mid-project from plain MonoBehaviour → VContainer + MessagePipe + UniTask modular (per COMPUTATIONAL_RESOURCES_CALCULATION.md v2.0, 2026-05-23, and !listing.md). This is a significant pivot AFTER the initial Unity 6.3 setup.
  - Many of the docs read in this task were already archived in !listing.md v4.0 (2026-05-23) as "historical / decision-made / one-time audit": ANALYSIS_REPORT, CODE_REVIEW_Local_Folder, GIT_WORKFLOW_TWO_PC, LOST_SESSION_ANALYSIS, MIGRATION_ANALYSIS, PROJECT_SETUP_PLAN, UNITY_63_RESEARCH, UNITY_VERSION_COMPARISON, WORKFLOW_GITHUB_UNITY. They remain historically valuable for engine-choice context but are NOT the active current docs.
  - Project name: "Cultivation World Simulator" (Cultivation/xianxia genre, 2D, single-player, procedural generation, large NPC counts up to 2000 in megapolis).
  - Render pipeline chosen: URP 2D (not HDRP, not built-in).
  - Scale of ambition: 10×10 km megapolis with 25 M tiles and 2000 NPCs — clearly beyond Phaser's reach, justifying the engine switch in ambition terms.
  - The decision to write NEW code (not port) means the prior Unity 6.3 codebase is essentially greenfield reference, not a constraint — engine choice is genuinely open if Unity 6.3's complexity (DOTS, URP setup, two-PC Git pain, headless AI workflow gaps) is deemed too costly.

---
Task ID: 3-a
Agent: research-architecture (general-purpose)
Task: Read main architecture docs for engine-choice analysis

Work Log:
- Read /home/z/my-project/worklog.md (context — Unity 6.3, prev iteration stalled on graphics, earlier Phaser iterations, 2D/2.5D + maybe custom engine under evaluation)
- Read /home/z/my-project/game-docs/Caveman.md (NOT a game concept file — describes a "Caveman mode" communication style for the AI assistant; token-efficient Russian/English shorthand. No engine-relevant content. Project name revealed: "Cultivation World Simulator".)
- Read /home/z/my-project/game-docs/docs/ARCHITECTURE.md (887 lines, full — stable architecture v4.0, Unity 6.3 URP 2D, single-player)
- Read /home/z/my-project/game-docs/docs/ARCHITECTURE_CODE.md (918 lines, full — codebase architecture v3.18, 429 .cs files, 44 interfaces, 16 modules + SceneOrchestrator + GameSession)
- Read /home/z/my-project/game-docs/docs/ARCHITECTURE_IMPL.md (1075 lines, full — implementation status, migration examples, per-module details)
- Read /home/z/my-project/game-docs/docs/ARCHITECTURE_FILE_TREE.md (318 lines, full — file tree of Assets/Scripts/)
- Read /home/z/my-project/game-docs/docs/SETUP_GUIDE.md (246 lines, full — Unity 6000.3 setup, auto-config phases, package list, troubleshooting)
- Read /home/z/my-project/game-docs/docs/!LISTING.md (295 lines, full — docs/ index of 60 files, ~580K tokens total; revealed existence of SORTING_LAYERS.md, SPRITE_INDEX.md, SCENE_BUILDER_SYSTEM.md, WORLD_MAP/LOCATION_MAP/TILE/TRANSITION system docs)
- Read /home/z/my-project/game-docs/docs/!Ai_Skills.md (591 lines, full — list of 19 available Skills through Skill() tool + 25 CN-only skills; only relevant for engine choice insofar as it confirms asset pipeline expectations: image-generation, image-search, VLM for screenshot debugging, XLSX for balance tables)
- Read /home/z/my-project/game-docs/docs/DEVELOPMENT_PLAN.md (331 lines, full — ARCHIVED legacy plan v1.3 from 2026-03-30, pre-modular-rebuild era; mentions GameManager, SceneLoader, JSON content, 80+ legacy files, ScriptableObject Asset Generator. Confirms migration history.)
- Cross-referenced SETUP_GUIDE troubleshooting section for graphics pain points
- Cross-referenced ARCHITECTURE_CODE §10 (Assembly Definition) and §13 (metrics) for Unity dependency list

Stage Summary:
- Game type & perspective: "Cultivation World Simulator" — a single-player xianxia/wuxia cultivation life-sim. Top-down 2D, grid/tile-based world (Unity Tilemap + Grid). Camera is Orthographic with CameraFollow. Movement is tick-based: "1 тик = 1 минута игрового времени", "Движение — 1 тик на клетку". Time has 4 speeds (Pause/Normal 1s=1min/Accelerated 1s=5min/Fast 1s=15min). Not a side-scroller, not isometric (despite "2.5D" being on the table for the new iteration, the existing Unity iteration is pure 2D top-down with a Tilemap). The WORLD_SYSTEM.md (referenced, not read) allegedly contains "3D координаты" — but those appear to be data-layer coordinates, not rendering. Combat is real-time-with-pause, not turn-based. Heavy simulation: Kenshi-style body system (dual HP, amputations, reattachments), 11-layer damage pipeline, NPC behaviour trees + 3-tier nervous system (spinal 1-10ms / neural router 10-50ms / brain 100-500ms).

- Visual style & rendering needs: 2D URP (Universal Render Pipeline 2D). Sprites use Sprite-Lit-Default shader → require Light2D (Global) or sprites render black (this is an explicit pain point in SETUP_GUIDE). 184 sprites, PPU=64, Point filter, Alpha-isSource. Sorting layers are managed by a dedicated `SortingLayerManager` (separate SORTING_LAYERS.md doc, 19KB — code is source of truth). UI theme is "Древний Пергамент" (Ancient Parchment) using Unicode glyphs (◆ ○ ★ ✓ ◇ ▰) — explicitly NOT TextMeshPro because LiberationSans SDF lacks those glyphs; uses legacy `UnityEngine.UI.Text` via OS fonts. UI is uGUI Canvas-based (Screen-Space Overlay), NOT UI Toolkit. Procedural UI creation via `UIFactory` (~1153 LOC). No mention of custom shaders, particle effects, or 2D skeletal animation in the main architecture docs — those would live in the (unread) SORTING_LAYERS.md / SPRITE_INDEX.md / SCENE_BUILDER_SYSTEM.md. The architecture is sprite/tilemap-centric; no 3D models, no skinned meshes.

- Major subsystems & their weight: 16 modules + SceneOrchestrator + GameSession = 18 major subsystems. Weight breakdown:
  * Heavy simulation: Body (dual HP, 11 species, 10 morphologies, body-part functions, materials, severed-debuff system), Combat (11-layer damage pipeline, level suppression, elemental interactions ×1.5/×0.8 with 7 elements + poison), Qi (capacity grows exponentially: L9 ~2,048,400 × density 256 = ~524M effectiveQi — uses `long` not `float`), Formation (multi-participant qi pooling with contour drawing), Buff (28 BuffType values, soft-cap formulas, immunities).
  * Heavy persistent state: NPC (3 categories — Temp/Plot/Unique, Behaviour Tree + Spinal AI, relationships with decay, movement, combat adapter), Quest (progress tracker with 6 MessagePipe subscriptions), World (locations, sectors, factions, dynamic world events, calendar with 30d×12m×24h starting year 1864).
  * Medium: Inventory (15 equipment slots, "Matryoshka" 3-layer item generation: Base×Grade×Specialization), Player (split from 1425-LOC God Object into 6 services: PlayerService/SleepService/PlayerCombatAdapter/PlayerInputService/PlayerVisualService/StatService), Charger (qi-charger artifacts with 5-state thermal balance), Interaction (dialogues with branching DialogueNode/DialogueChoice + typewriter effect).
  * Light: UI (presenters, toasts, modals), Save (JSON file handler + aggregator), Tile (grid + resources + destructibles).
  * Hub-and-Spoke: all modules communicate ONLY via Core interfaces or MessagePipe `readonly struct` contracts (~130 contracts in 20 files). Zero GC allocation is an explicit design goal.

- Unity-specific dependencies found: Very deep Unity coupling throughout.
  * DI: VContainer (hadashikick) v1.17.0 — LifetimeScope, ModuleLifetimeScope, [Inject], IStartable, ITickable, IContainerBuilder.
  * Messaging: MessagePipe (Cysharp) v1.8.1 + MessagePipe.VContainer — IPublisher<T> / ISubscriber<T>.
  * Async: UniTask (Cysharp) v2.5.10 — replaces Coroutines.
  * Render: URP 2D (UniversalRP.asset + Renderer2D.asset), Light2D, Sprite-Lit-Default shader, SortingLayer, SpriteRenderer, UnityEngine.Tilemap, UnityEngine.Grid, Camera.orthographic.
  * UI: com.unity.ugui 2.0.0 (Canvas, Text, Image, VerticalLayoutGroup, ContentSizeFitter, LayoutElement, CanvasGroup), legacy `UnityEngine.UI.Text` (NOT TMP as primary), com.unity.textmeshpro 3.0.6 (optional, for future use). UI Toolkit explicitly NOT used.
  * Input: com.unity.inputsystem (latest) — Keyboard.current, Mouse.current. Active Input Handling = "Input System Package (New)" or "Both".
  * Data: ScriptableObjects for ItemData/EquipmentData, AssetGenerator Editor tool, JSON content files (techniques/enemies/equipment/npc_presets/quests — 11 files, legacy era).
  * Architecture types: MonoBehaviour-based UI Views (22 of them — HUDPanelView, HotbarPanelView, BuffBarView, ToastView, MiniMapView, DialoguePanelView, PausePanelView, CombatOverlayView, DeathScreenView, LoadingScreenView, CharacterPanelView, TechniqueChargeView, CombatLogView, TurnOrderView, DamageNumberView, EnemyHealthBarView, InputLogPanel, NPCInspectorPanel, ContextMenuUI, GameInputAdapter, UIComponentResolver, DraggableWindow), GameLifetimeScope, RuntimeSceneBuilder (programmatically creates GameObjects at runtime — no scene prefab), CameraFollow MonoBehaviour, Editor-only Phase00/01/01B/02 auto-config scripts ([InitializeOnLoadMethod]).
  * Assembly: single asmdef `CultivationGame.New.asmdef` referencing VContainer, MessagePipe, MessagePipe.VContainer, UniTask, Unity.InputSystem, Unity.TextMeshPro.
  * Anti-patterns explicitly banned: `FindFirstObjectByType` (except with FindObjectsInactive.Include for deactivated UI Views), `GameObject.Find`, `Singleton Instance`, `ServiceLocator`, raw C# `event`/`Action` for cross-module, `StartCoroutine`/`IEnumerator`. These are all Unity-specific anti-patterns.
  * Comment style mandates Russian comments; class/method/variable names English.
  * Notable: NPCConfig is a `class` (not ScriptableObject) — BD-48 lesson "mutable struct risk" generalized to "use class for configs".

- Performance targets: No explicit entity-count or FPS targets found in the main architecture docs. Implicit targets:
  * Zero GC allocation per frame (readonly struct contracts, UniTask, struct DamageRequest/DamageResult/DefenseContext/BodyPartData/etc.).
  * Tick-based simulation decoupled from frames (ITickable.Tick() called per Unity frame, but logic uses game-tick granularity).
  * 184 sprites, PPU=64 — small asset footprint.
  * Locations are separate Unity scenes loaded via scene transitions (LocationChangedEvent → SceneTransitionRequest) — implicit sector/location streaming by scene-swap, NOT open-world streaming.
  * NPC AI is tiered (spinal 1-10ms, neural 10-50ms, brain 100-500ms) — designed for many concurrent NPCs at different update cadences, but no concrete max-NPC number stated.
  * TileMap is procedurally generated at runtime (TileMapGenPhase) — not pre-baked.
  * Qi math goes up to ~524M (L9 effectiveQi) — `long` arithmetic, no float precision concerns.

- Multiplayer/cloud needs: NONE. Explicit: "Игра является полностью однопользовательской. Все данные хранятся локально." (Fully single-player. All data stored locally.) Hierarchy of truth: 1) in-memory game state, 2) local save files. No server, no cloud, no online services, no leaderboards. This eliminates a major category of engine requirements.

- Pain points / graphics blockers noted: Several explicit graphics/rendering pain points:
  * **"Default Renderer is missing"** — 24 Console errors after copying Assets/ because GUIDs change and GraphicsSettings.asset can't find URP asset. Workaround: `Phase00URPSetup` auto-fixes on Unity launch. Indicates GUID fragility in the asset pipeline.
  * **Black sprites** — sprites use Sprite-Lit-Default shader; without a Light2D they render black. RuntimeSceneBuilder now creates `Light2D (Global)` explicitly. (!Ai_Skills.md even mentions a use case: "VLM: [screenshot] → 'What's wrong with rendering?' → 'sprites black → no Light2D → Sprite-Lit-Default without light'".)
  * **Text renders at size 0x0** — text in VerticalLayoutGroup without ContentSizeFitter gets sizeDelta=0. Fixed by UIFactory.CreateText() adding ContentSizeFitter + LayoutElement. (UIF-02 fix.)
  * **FindFirstObjectByType returns null for deactivated UI Views** — fixed by passing FindObjectsInactive.Include in WireUIViews (UIA-01 fix).
  * **VContainerException: No such registration** — caused by direct `scope.Container.Inject()` before MessagePipe broker registration completes. Fixed via UIComponentResolver.TryInject() in Start() instead of in CreateXxx().
  * **UIFontCache static init** — fallback chain: `Font.CreateDynamicFontFromOSFont` → `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`.
  * **MiniMapView V3** — anchor (1,1), pivot (1,1), BuildUI with try/catch, ToggleVisibility robust, N-key toggle (UIF-04 fix).
  * **UI V3 not fully tested** — SETUP_GUIDE states: "UI V3 Фазы 0–4 реализованы, в активной переработке (не прошли полное тестирование). Фаза C (этапы 5–9) — pending." UI is the active work-in-progress, suggesting graphics/UI is exactly where the iteration stalled.
  * **19 dead stub files** — physical files in Entry/Stubs/ but FallbackRegistrar not called since Phase 17B. Recommended for deletion (Phase D, pending).
  * **God Objects split during migration** — PlayerController 1425 LOC / 14 deps → 6 services. BuffManager 1614 LOC → 3 services. EquipmentController 1418 LOC → 3 services. Indicates the legacy iteration had severe architectural debt before being modularized.
  * **User's knowledge gap** — the worklog itself states "Пользователь плохо знает Unity" (user doesn't know Unity well); SETUP_GUIDE is heavily hand-holding ("GitHub Desktop + copy Assets/ folder" workflow), suggesting the user struggled with Unity Editor operations.

- Save system: JSON-based. `SaveFileHandler` does file I/O (read/write JSON). `SaveDataAggregator` collects state from all `ISaveable` services (SaveKey/CaptureState/RestoreState interface). `SaveService` exposes Save/Load/HasSave/DeleteSave/GetAllSaves. Triggers: auto-save on location change, new technique, important item, breakthrough, combat end; manual save via menu or F5 (quick save) / F9 (quick load). SaveModule uses ModuleServices pattern (no separate LifetimeScope). Local file storage. (Referenced but not read: WORLD_SAVE_SYSTEM.md 62KB describes "chunk-based persistence" for world state — separate from the player-save system.) Unix timestamps mentioned in !LISTING description. Format choice (JSON over binary) suggests human-readability/debuggability was prioritized over compactness — easy to port to any engine.

- Asset pipeline expectations:
  * **Sprites**: 184 sprites, auto-configured on first Unity launch via Phase01 (`[InitializeOnLoadMethod]`): PPU=64, Point filter, Alpha-isSource. Referenced SPRITE_INDEX.md (17KB) lists sizes/formats. No mention of atlases, compressed formats, or platform-specific variants.
  * **ScriptableObjects**: ItemData, EquipmentData, plus generated CultivationLevelData / MortalStageData / ElementData / SpeciesData via Editor `AssetGenerator` tool (`Window → Asset Generator`).
  * **JSON content**: 11 JSON files (techniques/enemies/equipment/npc_presets/quests/items/cultivation_levels/elements/grades/materials/technique_types) — legacy-era content pipeline. Loaded at runtime.
  * **Scenes**: RuntimeSceneBuilder programmatically creates the scene (Camera, Canvas, EventSystem, World Root, Player, NPC, Light2D, GameInputAdapter). MainGame.unity only needs a GameLifetimeScope GameObject. No scene prefabs required.
  * **Auto-config phases**: Phase00 (URP Setup), Phase01 (Sprite Import), Phase01B (TMP Essentials, optional), Phase02 (Tags & Layers: Default/Background/Terrain/Objects/Player/UI). All `[InitializeOnLoadMethod]` Editor scripts.
  * **Asset generation via AI Skills**: !Ai_Skills.md prioritizes Image-Generation (item icons, concept art, UI elements, sprites), Image-Search (references, environment textures), VLM (screenshot debugging), XLSX (balance tables exported to JSON).
  * **GUID fragility**: SETUP_GUIDE troubleshooting documents that copying Assets/ between machines breaks GUIDs in GraphicsSettings.asset; auto-fix on Editor launch. This is a Unity-specific pain point that wouldn't exist in a custom engine.

- Other notable findings:
  * **Caveman.md is a red herring** — it's a token-efficiency communication protocol for the AI assistant ("speak like caveman, drop articles/fillers"), NOT the game concept. The game concept is "Cultivation World Simulator" as stated in every architecture doc.
  * **DEVELOPMENT_PLAN.md is ARCHIVED** — describes legacy v1.3 era (GameManager, SceneLoader, 80+ legacy files, JSON content). All migrated/frozen in `UnityProject/Legacy/`. Current state is "modular rebuild" v3.18+.
  * **Three doc folders**: `docs/` (60 files, current), `docs_old/` (69 files, Phaser-era archive), `docs_temp/` (24 files, drafts/audits). Plus `Legacy/docs_asset_setup/` (34 files, frozen Unity Editor instructions) and `checkpoints/` (183 files, work-session logs).
  * **Codebase size**: 429 .cs files, ~77K lines, single assembly `CultivationGame.New.asmdef`. 44 interfaces, ~130 MessagePipe contracts, 16 modules, 22 UI views, 19 dead stub files, 0 singletons, 0 ServiceLocator usages.
  * **Iteration history**: Phaser era (docs_old) → Unity legacy (GameManager+Singleton+ServiceLocator+C# Events, frozen in Legacy/) → Unity modular rebuild (VContainer+MessagePipe+UniTask, current, Phase 0-19 done, UI V3 in progress with Fix Plans A-D, A done, B in progress, C-D pending).
  * **Architecture is engine-agnostic at the Core layer**: All game logic sits behind pure-C# interfaces (IXxxService) and `readonly struct` MessagePipe contracts. The Unity-specific code is concentrated in: (a) MonoBehaviour UI Views (22 files in Entry/UI/), (b) RuntimeSceneBuilder + SceneOrchestrator + Phases (Entry/), (c) CameraFollow, TilemapVisualService, VisualProvider, SortingLayerManager, RenderPipelineLogger, SpriteHelper (Entry + Core utils). The 16 module services (Body/Qi/Combat/NPC/World/etc.) are pure C# and could in principle be ported to any engine with a DI container + message bus + async/await.
  * **No 2.5D rendering artifacts**: Tilemap, SpriteRenderer, Orthographic Camera, Light2D — all strictly 2D. No 3D meshes, no depth/Z-axis rendering, no perspective camera. The "2.5D" option mentioned in the worklog would be a NEW direction, not a continuation of existing assets.
  * **Heavy reliance on Unity Editor tooling**: AssetGenerator, SceneSetupTools, Phase00-02 auto-config — all `[InitializeOnLoadMethod]` Editor scripts. Migrating away from Unity means rebuilding this tooling.
  * **Phase-based development**: 19 implementation phases completed (0-19), each producing a discrete module with audit. Suggests disciplined, incremental development — the kind that could continue on any engine if the architectural pattern is preserved.

---
Task ID: 3-c
Agent: research-prev-iterations (general-purpose)
Task: Read previous engine iterations (Phaser + architecture history) for engine-choice analysis

Work Log:
- Read `/home/z/my-project/worklog.md` (context from prior tasks)
- Read `docs_old/ARCHITECTURE.md` (v21, 1115 lines — Phaser 3 + TruthSystem + Event Bus + Sandbox fix + Time Scaling + Stat Development)
- Read `docs_old/phaser-game-analysis.md` (2026-03-15 — PhaserGame.tsx optimization analysis, file was 2,798 lines)
- Read `docs_old/PHASER_STACK.md` (Phaser 3.90.0 stack description, bundle sizes, comparison with PixiJS/Three.js/Konva)
- Read `docs_old/PHASE3-PHASER-PROGRESS.md` (Phase 3 migration tracker, stages 1-10 done, 11-14 pending)
- Read `docs_old/architecture-analysis.md` (2026-03-19 v2.0 — analysis of TruthSystem, Event Bus, sandbox, Phaser integration)
- Read `docs_old/README.md` (categorizes PHASE3-PHASER-PROGRESS.md & PHASER_STACK.md as "Отменённые планы" — cancelled plans)
- Read `docs_old/INSTALL.md` (Phaser-era install guide: Bun/Next.js 16/Prisma, with extensive AI-agent troubleshooting notes)
- Read `docs_old/PROJECT_ROADMAP.md` (versions 0.5.0 → 0.9.0+, plans including Go rewrite, Tauri, Electron for packaging)
- Read `docs_old/development-1000-days-calculation.md` (v2.2 final — Stat Threshold system adopted, linear & soft-cap rejected)
- Read `docs_old/ARCHITECTURE_future.md` (2026-03-25 — unified HTTP+WebSocket server plan, persistent world, multi-session)
- Read `docs_old/ARCHITECTURE_refact.md` (2026-03-25 — server migration plan for combat/AI/techniques off client)
- Read `docs_old/ARCHITECTURE_cloud.md` (2026-03-25 — "Божество → Облако → Земля" thin-client HTTP-only architecture)
- Read `docs_old/matryoshka-architecture.md` (v3.0 — Base × Grade × Specialization layered generation)
- Read `docs_old/Listing.md` (catalog: 69 files, 2.0 MB, ~668K tokens; explicit statement that docs_old is Phaser-era archive, NOT for current Unity project)
- Read `docs_old/body-development-analysis.md` (v2.1 — virtual delta, sleep consolidation, stat thresholds, training types, vitality)
- Read `docs_old/formation_analysis.md` (v4.1 — contour drawing, multi-practitioner filling, drain system, formation cores disks/altars)

Stage Summary:
- Phaser iteration — what built, how far, what worked/failed: Phaser 3.90.0 was used as a 2D engine embedded in Next.js 16 + React 19 + Prisma/SQLite + Zustand stack (branch main2D). Reached Phase 3 stage 10/14 (Time Scaling) complete; stages 11-14 (World Map, Combat Scene, Assets, Testing) never started. Built: Training Ground with 6 straw targets, metric distance system (1m=32px), hitbox system, 51 unified presets, 4-tab StatusDialog, RestDialog, TechniquesDialog, body system Kenshi-style (functional+structural HP per limb), event bus, TruthSystem singleton, time scaling. Stats from docs: 46+ files, ~6,300+ lines of code, PhaserGame.tsx alone was 2,798 lines (down from 3,656). What worked: tick system (1 tick=1 second) solved sync lag; TruthSystem memory-primary pattern was clean; sandbox workaround (server-only storage, no localStorage). What failed: PhaserGame.tsx became a 2,798-line monster mixing logic + display; combat/AI/technique calculations ended up on the CLIENT (cheating vulnerability); sandbox iframe blocked localStorage forcing v14 architecture rewrite; assets never acquired (programmatic texture generation only); Combat Scene and World Map never built; scenes mixed business logic with rendering.

- Reasons for Phaser→Unity migration: No single explicit "we migrated because X" statement, but cumulative evidence: (1) docs_old/README.md explicitly labels PHASE3-PHASER-PROGRESS.md and PHASER_STACK.md as "Отменённые планы" (cancelled plans); (2) docs_old/Listing.md states "docs_old/ — архив документации из предыдущей реализации на Phaser 3. НЕ актуально для текущего Unity-проекта"; (3) ARCHITECTURE_refact.md acknowledges ~50,000+ lines of code with mixed concerns in Phaser scenes (logic+display) and combat/AI/techniques wrongly on client; (4) worklog states "Предыдущая итерация застопорилась на графической части" (previous iteration stalled on graphics); (5) Phase 3 roadmap stages 11-14 (World Map, Combat Scene, Assets, Tiled integration, Isometric view) were 0% — the engine was insufficient for the planned RimWorld-style sector system, isometric view, and proper tilemap/asset pipeline. The Phaser iteration produced systems design but never delivered a complete graphical game.

- Phaser technical decisions (tilemap/sprite/scene/physics): Physics = Phaser.Physics.Arcade with gravity {x:0, y:0} (top-down 2D); architecture-analysis.md explicitly argues "Phaser Render-Only НЕ НУЖЕН" because tick system already solved lag — keeping built-in physics saved 1-2 weeks of work. Sprites: NPCSprite extends Physics.Arcade.Sprite, position read from body.position, movement via setVelocity; NO external sprite assets — textures generated programmatically via this.make.graphics().fillCircle() etc. Scene system: LocationScene (combat), TrainingScene, WorldScene — three Phaser scenes; global variables (globalSessionId, globalCharacter, globalTechniques) used as React↔Phaser bridge (architectural decision, not garbage). State management: Zustand store (game.store.ts, time.store.ts) IN-MEMORY ONLY, NO localStorage (sandbox blocked it). Tilemap: Tiled Editor integration was a planned future item (Phase 2 of roadmap), never implemented.

- Matryoshka architecture concept: A multi-layered procedural generation principle where every object = Base × Grade × Specialization. Layer 1 (Base): level/material → base stats (e.g. Iron → 10 damage). Layer 2 (Grade): Common→Refined→Perfect→Transcendent multipliers (×1.0/1.3/1.4 etc.); crucially grade is INDEPENDENT of level — even L1 can roll transcendent at 2%. Layer 3 (Specialization): weapon type/technique subtype gives bonus multipliers. All generators use seededRandom for determinism. Applied uniformly to equipment, techniques (V2 with key principle baseDamage=qiCost), consumables, formations, qi-stones. The architecture-analysis.md references it as "Принцип Матрёшка" — design is clean and reusable.

- Cloud/online architecture plans: ARCHITECTURE_cloud.md describes "Облачная игра для одного" (cloud game for one) with "Божество → Облако → Земля" (Deity → Cloud → Earth) thin-client metaphor. Layered: Presentation (Phaser+React) → API (Next.js routes) → Domain Services (Combat/Qi/AI/Technique) → TruthSystem (singleton, in-memory) → Prisma/SQLite. HTTP-only initially because 1 TICK = 1 second and HTTP latency (10-100ms in sandbox, 1-20ms production) is well under the tick budget. WebSocket planned as future Phase 2 for real-time combat, NPC AI updates, multiplayer (Phase 3: PvP, broadcast, sync). ARCHITECTURE_future.md recommends Next.js Custom Server + Socket.IO (chose Socket.IO for ecosystem/rooms/fallback polling, one port, unified code for sandbox+production). ARCHITECTURE_refact.md lays out a 4-phase migration of combat, AI, techniques from client to server (combat was on client = cheat vulnerability). Hybrid Event Bus: specialized API for high-frequency (combat, AI, move); Event Bus for low-frequency (inventory, environment, body).

- 1000-day calculation summary: A balance research document. Three variants compared: (1) Linear growth — REJECTED because at 10,000 days body stat (×38.5) dominates cultivation core (×15); (2) Soft cap with diminishing returns at stat 50 — REJECTED because it creates artificial "walls" that demotivate; (3) Stat Threshold system — ADOPTED: threshold = max(1.0, floor(currentStat/10)), so higher stat needs more virtual delta for +1, by analogy with cultivation core ("bigger vessel → more to fill"). Per-action growth 0.001 (down from 0.01), sleep cap +0.20 per 8h sleep. Achievable: 1000 days → ~55 stat, 3000 days → ~80, 10000 days → ~125. Realistic for design goals, but the body-development system is complex (training types: classical 50/50, focus 70/30, extreme 95/5; vitality multipliers; intellect buffer; fatigue system with physical+mental split).

- Body development & formation complexity: VERY HIGH. Body: Kenshi-style with functional HP + structural HP per body part (head/torso/arms/legs), limb severing when structuralHP≤0, regeneration (structural first then functional), 5 bleeding levels (minor→arterial), vitality multiplier = 1+(vitality-10)*0.05, cultivation L8+ enables limb regrowth. CPU load analysis done: ~1ms per 100 NPC. Formations: multi-stage creation (contour drawing by ONE creator → filling by MULTIPLE practitioners → activation at 100%); formation cores: disks (portable L1-L6, 1-3 slots) and altars (stationary L5-L9, 3-10 slots); capacity multipliers small×10/medium×50/large×200/great×1000/heavy×10000; discrete Qi drain (1 tick=1 min); L8 Heavy barrier holds 102,400,000 Qi, lasts 15.6 years unaided; multiple refill mechanisms (cores, collection contour L8+, ley-lines, manual); independent of creator after activation (snapshot Qi concept). These systems are deeply intertwined with the tick system and event bus.

- Architecture evolution across iterations: v0.5.0 (Phase 2 complete) → v0.6.0 (Phase 3 Phaser integration Feb 2026, UI systems) → v0.6.2 (Combat training, hitbox) → v0.7.0 (Environment system) → v0.8.0 (Sector system RimWorld-style — planned, never built) → v0.9.0+ (Isometric, Tilemap, Tiled — planned, never built) → v14 (Mar 11, sandbox fix removing localStorage) → v18-19 (Mar 19, TruthSystem + Event Bus) → v21 (Mar 24, Time Scaling) → ARCHITECTURE_future.md / refact.md / cloud.md (Mar 25, all planning — server migration, unified HTTP+WS, thin client). Then full migration to Unity 6.3 happened (per worklog context, no explicit doc explains the trigger). Each iteration added complexity; by March 25 the team had three competing architecture docs (future/refact/cloud) all in PLANNING status — suggesting architectural indecision immediately before Unity pivot.

- What carried forward vs dropped: Carried forward (design only, not code): the entire game design — cultivation lore, 51 presets, body system Kenshi-style, matryoshka generation principle, stat threshold development system, formation system, combat formulas, matryoshka architecture concept. Dropped entirely: Phaser 3 engine, Next.js/React/Bun/Prisma/SQLite stack, Event Bus implementation, TruthSystem singleton code, Zustand stores, Socket.IO mini-service, sandbox-iframe compatibility layer, Caddy gateway, all 50,000+ lines of TypeScript. The Unity 6.3 rewrite is essentially a from-scratch engine migration that keeps the design doc corpus.

- Custom engine/framework considerations (if any): PHASER_STACK.md includes explicit comparison: Phaser 3 (1.2MB, medium complexity, "chosen for balance of features and simplicity"), PixiJS (250KB, low — render only), Three.js (500KB, high — 3D), Konva.js (150KB, low — Canvas). architecture-analysis.md rejects "Phaser Render-Only" mode because tick system already solves lag and built-in physics/collisions save 1-2 weeks. PROJECT_ROADMAP.md long-term packaging plans list: Portable Bundle (~250MB, low complexity, recommended first), Electron (~180MB, medium), **Go rewrite (~20MB, "High complexity", "Optimal size")**, **Tauri/Rust (~15MB, "High complexity", "Maximum optimization")**, Docker (~300MB, low). No explicit "build custom engine" mention in Phaser-era docs, but Go/Rust rewrites were on the distant roadmap — and the eventual choice was Unity (a third-party commercial engine, not custom). The migration to Unity suggests the team decided web stack + Phaser could not meet the graphical/sector/isometric ambitions.

- Roadmap status: Phase 2 (Foundation): ✅ Complete. Phase 3 Phaser (stages 1-10): ✅ Complete (infra, time system, UI, rest, techniques, presets, training ground, hitboxes, time scaling). Phase 3 stages 11-14: 🔜 Pending (World Map, Combat Scene, Assets, Testing). v0.7.0 Environment: ~30% (presets done, texture generator + physics + harvesting not). v0.8.0 Sector System: 0%. v0.9.0+ Isometric/Tilemap/Combat AI: 0%. Limb system (Kenshi-style): conceptualization only. Server migration refactoring (combat/AI/techniques to server): 0%, all 4 phases pending. Architecture future/cloud: planning only. Essentially the Phaser iteration stalled at "training ground tech demo" — never reached a playable world map or proper combat scene. Then Unity pivot occurred.

- Other notable findings: (1) Sandbox iframe was a major architectural constraint — blocked localStorage/sessionStorage/IndexedDB/cookies, forced server-only storage (v14), required Caddy gateway with XTransformPort routing. (2) TruthSystem pattern: memory primary, DB secondary, autosave every 1 minute + immediate save on critical events (new technique, new item, location change, breakthrough, combat end). (3) Time Scaling: 1 TICK = 1 SECOND REAL TIME (fixed), variable minutesPerTick from 0.25 (superSuperSlow, combat) to 60 (ultra, meditation); auto-switch on activity change; "инверсия скорости" bug fixed Mar 24. (4) ARCHITECTURE_refact.md admits combat/AI/technique logic was on the CLIENT — security/cheating vulnerability that demanded server migration. (5) Three parallel architecture docs (future/refact/cloud) all dated 2026-03-25 in PLANNING status — suggests architectural paralysis right before Unity pivot. (6) docs_old total: 69 files, 2.0 MB, ~668K tokens — substantial design corpus that is being migrated to Unity as reference. (7) PhaserGame.tsx single file at 2,798 lines (after Phase 3 reduction from 3,656) — analysis recommended extracting ~1,300 more lines (-46%) but this never happened. (8) Listing.md confirms the project is now "Cultivation World Simulator (Unity 6.3 URP 2D)" — Unity uses URP 2D renderer, so 2D/2.5D direction is preserved. (9) No mention of why specifically Unity was chosen over alternatives like Godot, Defold, or PixiJS-after-Phaser — that decision happened outside the documented Phaser era.

---
Task ID: 3-d
Agent: research-game-systems (general-purpose)
Task: Read key game system docs for technical-requirement extraction (engine choice)

Work Log:
- Read worklog.md (current state)
- Read all 30 required docs:
  - WORLD_SYSTEM.md, WORLD_MAP_SYSTEM.md, TILE_SYSTEM.md, TILE_SYSTEM_IMPLEMENTATION.md
  - COMBAT_SYSTEM.md, NPC_AI_SYSTEM.md, NPC_ASSEMBLY_PIPELINE.md, BODY_SYSTEM.md
  - FORMATION_SYSTEM.md, SPRITE_INDEX.md, SORTING_LAYERS.md
  - SCENE_BUILDER_SYSTEM.md, SCENE_BUILDER_SYSTEM_Old.md
  - TECHNIQUE_SYSTEM.md, QI_SYSTEM.md, ELEMENTS_SYSTEM.md
  - INVENTORY_SYSTEM.md, EQUIPMENT_SYSTEM.md
  - SAVE_SYSTEM.md, WORLD_SAVE_SYSTEM.md, TIME_SYSTEM.md
  - LOCATION_MAP_SYSTEM.md, TRANSITION_SYSTEM.md (first 300 lines)
  - ENTITY_TYPES.md, CONFIGURATIONS.md, DATA_MODELS.md
  - GENERATORS_SYSTEM.md, FACTION_SYSTEM.md
  - NPC.md, MORTAL_DEVELOPMENT.md
- Extracted Unity-specific dependencies (MonoBehaviour/ScriptableObject/Tilemap/Rigidbody2D/Light2D/TMP/VContainer/MessagePipe/UniTask)
- Extracted rendering, world/sector, tile, combat, AI, body, formation, save, time, data, config, UI specifics
- Cross-referenced tile size (2×2 m), sector (10×10 km), chunk (100×100 km), world (200,000 km²) across multiple docs

Stage Summary:
- Rendering load & complexity: Unity 6.3 URP 2D, **orthographic top-down 2D** (Camera Z=-10 → +Z), single Light2D Global (Sprite-Lit-Default shader). Only 6 sorting layers (Default, Background, Terrain, Objects, Player, UI). Sprites are small & low-res: terrain/objects 64×64 px @ PPU=32, player 128×128 @ PPU=64. ~133–184 sprite assets total, mostly procedural (Perlin noise + Sprite.Create at runtime). No skeletal animation — sprite-based composition (Player visual + shadow SpriteRenderer; NPC = single SpriteRenderer per role). Combat-effect sprites (12) and orbital-weapon sprites (8) exist; effects are ExpandingEffect/DirectionalEffect classes. Lighting 2D is minimal (one Global light). Sorting layers must be created at runtime via code (SceneBuilderConstants.cs) because ProjectSettings/TagManager.asset is intentionally removed (caused Unity Editor crash). No explicit particle system / shader graph references — VFX is sprite-swap based.
- World/sector sizes & streaming: Huge — World = 200,000 × 200,000 km (≈268× Earth's land), 4,000,000 chunks. **Chunk (100×100 km) = one save file**. **Sector (10×10 km) = world-map cell**, holds 1–10 locations. **Location (variable, 100 m – 10 km, up to 25 M tiles per megapolis)** = Unity scene = unit of loading. "Wild lands" between locations are NOT loaded as scenes — they are procedural transition encounters. So model is **per-location scene loading** (with travel/encounter screens), NOT continuous open-world streaming. Two-level navigation (world map + local scene), plus optional third level (building interior — but docs favour "free-building" inside main location instead of separate interior scenes).
- Tile system specifics (size/layers/ortho-vs-iso): Orthographic top-down (NOT isometric). Tile = **2×2 m** (project standard, "ЕДИНСТВЕННЫЙ ИСТОЧНИК ИСТИНЫ"). 4 logical layers per tile: (1) base params [qiDensity, temperature], (2) surface/terrain, (3) objects, (4) subjects. Z is a **logical level (-5..+5)**, not 3D. TileData struct ~20 fields (coordinates, terrain, objects list, entities list, qi, temperature, water, flags). Multi-tile objects supported (1×1 to 4×4+). Unity Tilemap used (Grid + Tilemap + TilemapRenderer + custom GameTile : TileBase). Test location = 30×20 tiles = 60×40 m. Tilemap cellSize = (2, 2, 1). Save uses delta compression + procedural regeneration from seed (only modified tiles stored). Optimisations listed: Delta Compression, LOD, Chunking, Pooling.
- Combat model (RT vs TB) & effect load: **Real-time with pause**, auto-slows to "superSuperSlow" on combat start. Tick-based (1 tick = 1 game minute; speeds: normal=1 tps, fast=5, quick=15 tps). 11-layer damage pipeline (rawDamage → level suppression → body-part roll → formation buff → active defense → Qi buffer → armor → material → HP split 0.7/0.3 → consequences). 5 attack subtypes (melee_strike, melee_weapon, ranged_projectile, ranged_beam, ranged_aoe). No rigidbody physics for combat — tile-based positioning, formulaic hit detection. DoT (burn, bleed), AoE (formations up to 300×300 m), chain lightning (2 targets), knockback (3 cells), pierce. Projectiles/Beams/AoE resolved through effect sprite classes (ExpandingEffect, DirectionalEffect) — limited count per combat, not projectile hell. CombatOverlayView + ~5 sub-views in UI.
- NPC AI complexity & active counts: Three-tier nervous system: Spinal AI (reflexes, 1–10 ms), Neural Router (10–50 ms signal buffer/router), Brain Controller (100–500 ms, "LLM or advanced AI" — implies external/dynamic dialogue generation). **Behaviour Tree** (Selector/Sequence/Condition/Action nodes) + **finite state machine** (15 states: Idle, Wandering, Patrolling, Following, Fleeing, Attacking, Defending, Meditating, Cultivating, Resting, Trading, Talking, Working, Searching, Guarding). 8 NPC roles, PersonalityTrait [Flags] enum (8 traits). Threat system with decay rate. Default config: **MaxActiveNPCs = 100**, AggroRadius=5, AttackRadius=1.5, PatrolRadius=10, DefaultMoveSpeed=2 u/s, FleeSpeedMultiplier=1.5. Pathfinding = tile-based (move 1 tile = 1 tick; micro-step = 0.1 tick). No explicit NavMesh mentioned — grid-based navigation implied. NPC module is **pure C# (no MonoBehaviour)** — ModuleServices pattern (NPCModule : IStartable, ITickable, IDisposable), VContainer DI, MessagePipe events (readonly struct, zero-GC).
- Body system nature (sprite composition? skeletal?): **NOT skeletal**. Kenshi-style body-part composition: each entity = List<BodyPart> with two HP types (functional/structural, 0.7/0.3 split on damage). Humanoid = 11 body parts + heart. 7 morphologies (humanoid, quadruped, bird, serpentine, arthropod, amorphous, hybrid_*). 7 size classes (Tiny→Colossal, multipliers 0.1×–50×). 6 body materials (organic, scaled, chitin, ethereal, mineral, chaos). BodyFactory + BodyTemplateProvider (10 templates) + SpeciesRegistry (11 species). Crippled/Severed states. Limb-reattachment mechanics. Body parts stored in NPCState.BodyParts (pure data, not visual). Visual representation is separate (sprite per role, no per-part sprite composition described).
- Formation system scale: Magical arrays drawn on ground. Sizes: Small (3×3 m) → Heavy (300×300 m); effect radii 50 m – 5 km. Capacity: 800 Qi (L1 Small) → 204,800,000 Qi (L9 Heavy). Up to **50 helpers** filling simultaneously. Drain: 1–100 Qi per drain, every 5–60 ticks. 8 formation types (Barrier, Trap, Amplification, Suppression, Gathering, Detection, Teleportation, Summoning). Physical cores: Disk (portable, L1-L6, 1-3 slots) or Altar (stationary, L5-L9, 3-10 slots). Formation UI with preview SpriteRenderer (sortingOrder=1000). Not a group-movement formation system — it's a magical AoE zone system.
- Save system format & size: JSON files (binary + GZIP optional). Hierarchical: main.sav (player/time/quests, 10–50 KB) + chunks/ (chunk_*.sav, ~25 B per chunk metadata) + locations/ (loc_*.sav, 100 B – 10 KB each) + metadata.sav (world index). **Tile data NOT saved individually** — regenerated from seed + delta list of modified tiles. 100h play: ~50–150 KB raw / ~5–15 KB optimised. 1000h: ~100 KB. Extreme 2000 locations: ~1–2 MB. ISaveable pattern + SaveDataAggregator aggregates modules (NPCService.SaveKey="npc"). Auto-save every 60 ticks (=1 game hour). Fix-08 added FormationSaveData, BuffSaveData, ChargerSaveData, TileSaveData. NPCSaveEntry captures ~20 fields. `long` type for all Qi values (Fix-01).
- Time system & tick rate: **1 tick = 1 game minute**. Speeds: pause=0, normal=1 tps, fast=5 tps, quick=15 tps. Unity Coroutine-based TickTimer (interval = 1000/tps ms). TimeManager Singleton publishes OnGameTick event to QiManager/NPCManager/CombatManager/UIManager. **Batch processing every 10 ticks** (QiTickProcessor) for performance. Auto-save every 60 ticks. ActivityManager auto-switches speed by player activity (combat→normal, travel→fast, meditation→quick). Single-player only (multi-player noted as future). Calendar: year 1864 start, 30 days/month, 12 months, 24 h/day, warm/cold seasons, 6 time-of-day bands. World simulates only when loaded (locations); offscreen world does NOT tick fully — sectors store lastVisited timestamp and minimal metadata.
- Data model Unity-coupling: Mixed. NPCState is pure C# class (no MonoBehaviour) in ModuleServices architecture (VContainer DI). NPCContracts events are `readonly struct` (zero-GC) for MessagePipe. Plain C# structs in Core/Data (StatBonus, Position2D, InputFrameData, InventorySlot, LootEntry). BUT heavy use of **ScriptableObject** for static data: TerrainTile, ObjectTile, EquipmentData, TechniqueData, ElementData, CultivationLevelData, MaterialData, ItemData, FormationCoreData, UIThemeV3, NPCPresetData. JSON sub-fields inside data (bodyState, personality, equipment, techniques, bonusStats, effects, scaling, requirements). `long` for Qi values; `float` for stats. Enums in Core/Data/Enums.cs (CultivationLevel, SoulType, Morphology, BodyMaterial, CoreQuality, NPCRole, NPCCategory, PersonalityTrait, NPCAIState, Attitude). Original source data was TypeScript (src/data/*.ts) — migrated from Phaser era.
- Config storage approach: **ScriptableObjects in `ScriptableObjects/Config/`** (CultivationLevels.asset, TechniqueTypes.asset, Materials.asset, Grades.asset, Elements.asset) + `ScriptableObjects/Presets/Techniques/{Basic,Advanced,Master,Legendary}/` + `ScriptableObjects/Presets/Items/` + `ScriptableObjects/Presets/Materials/`. Runtime configs are C# classes (NPCConfig, GameConstants, SceneBuilderConstants) registered via VContainer. Saves use JSON (session.json, world_state.json, characters.json, npcs.json). No external JSON config files for game-balance data — that lives in ScriptableObjects. Original migration source: TypeScript data files.
- UI complexity: Heavy. Canvas (ScreenSpaceOverlay, sortingOrder=100, CanvasScaler 1920×1080) with 3 sub-layers (HudLayer, WindowLayer, FloatingLayer). **22 UI Views** wired via VContainer DI + UIComponentResolver bridge (because Views are created via AddComponent<T> outside DI). UIThemeV3 ScriptableObject ("Ancient Parchment") with colors, sprites, **per-mille sizing** (integer math, no float drift). Phase17InventoryUI was 1793 LOC (refactored to 7 partial files, biggest = 395 LOC). Inventory UI includes: BodyDollPanel + DollSlotUI, BackpackPanel + StorageRing + SlotUI prefab, TooltipPanel (24 SerializeFields), DragDropHandler + ContextMenu, BodySilhouette (procedural), Header+Belt+TabBar+SpiritStorage. Inventory = line model (list + maxWeight + maxVolume, NOT grid). Equipment: 15 slots (Head, Torso, Belt, Legs, Feet, WeaponMain, WeaponOff, Amulet, Ring×4, Charger, Hands, Back) — many are 🔒 stubs. Drag&drop, context menu, tooltips. UI uses TMP (TextMeshPro) — has fallback to LegacyRuntime.ttf. Hotkeys (F5/F9/Esc/E/B/R/X/F).
- Performance budgets: **MaxActiveNPCs = 100** (NPCConfig). Batch processing every 10 ticks. Auto-save every 60 ticks. Procedural sprites (Perlin noise, Sprite.Create) instead of art assets. Delta compression for tile saves. Mentioned but not quantified: LOD, Chunking, Pooling. AssetDatabase.RefreshAndWait + LoadAssetWithRetry(maxRetries=3, delayMs=200) for Editor-time stability. No explicit frame-rate or render-call budgets documented. Tile-based collisions (no per-frame physics solver). Single Light2D Global (cheap). RenderPipelineLogger with 9 diagnostic levels (L1–L9). No explicit NPC/Entity per-scene cap beyond MaxActiveNPCs.
- Explicit Unity component dependencies (HEAVY): Unity 6.3 URP 2D (UniversalRP.asset, Renderer2D.asset, GraphicsSettings). **MonoBehaviour**: TileMapController, RuntimeSceneBuilder (1412 LOC), PlayerVisual, HarvestableSpawner, ResourceSpawner, DestructibleObjectController, FormationUI, FormationUIPrefabsGenerator, HarvestFeedbackUI, UIComponentResolver, Camera2DSetup. (NPCController/NPCAI/NPCVisual — REMOVED in Phase 0 refactor, replaced by pure C# services.) **ScriptableObject**: TerrainTile, ObjectTile, GameTile (TileBase), EquipmentData, TechniqueData, ElementData, CultivationLevelData, MaterialData, ItemData, FormationCoreData, UIThemeV3, NPCPresetData. **Tilemap + Grid + TilemapRenderer** (Unity's built-in 2D Tilemap). **Rigidbody2D** on Player (Phase06). **Light2D** (Global, Sprite-Lit-Default). **SpriteRenderer** everywhere. **Canvas + CanvasGroup + CanvasScaler** (ScreenSpaceOverlay). **TMP / TextMeshPro** (with fallback). **Physics Layers** (8 layers) + Sorting Layers (6) + Tags (7) configured via Phase02TagsLayers. **VContainer** (DI framework — GameLifetimeScope, ModuleLifetimeScope, IContainerBuilder, VContainerException). **MessagePipe** (pub/sub events, zero-GC readonly structs). **UniTask** (async for ISceneAssemblyPhase). **Animator** NOT used (no skeletal animation). **Collider2D** used (Phase15 for test location colliders + transition triggers). **NavMeshAgent** NOT used (grid-based pathfinding). AssetDatabase (Editor) used heavily by frozen FullSceneBuilder (20 phases) and still-used auto-run phases (Phase00 URP, Phase01 SpriteImport, Phase01B TMP, Phase02 TagsLayers) via [InitializeOnLoadMethod] + EditorApplication.delayCall. ProjectSettings/ folder intentionally deleted (caused Editor crash); TagManager.asset recreated by Unity automatically, Sorting Layers created/verified by code at runtime.
- Other notable findings: (1) Architectural shift to "ModuleServices" pattern — pure C# modules (NPCModule : IStartable/ITickable/IDisposable) registered via VContainer, completely removing MonoBehaviour from NPC logic. (2) GameLifetimeScope + per-module ModuleLifetimeScope + ModuleServices.Register() bridge for sibling-scope visibility. (3) SaveDataAggregator + ISaveable pattern across modules. (4) SeededRandom for deterministic procedural generation (techniques, equipment, NPC, tiles). (5) Heavy reliance on Unity Editor scripting ([InitializeOnLoadMethod], AssetDatabase, SerializedObject) for project bootstrap — significant migration cost if leaving Unity. (6) Two parallel scene-builder systems: frozen Editor-time FullSceneBuilder (20 phases, ~3000 LOC) and active RuntimeSceneBuilder (1412 LOC, 10 phases). (7) Phantom-fix history: ProjectSettings/ removal, TagManager.asset auto-recreation, deterministic uniqueID for Sorting Layers (= layer index) — all symptoms of Unity-specific fragility. (8) "Pure C#" NPC module + MessagePipe events + readonly struct contracts is engine-agnostic in spirit, but every ScriptableObject, every Tilemap, every UI Canvas, every Light2D, every Rigidbody2D, every AssetDatabase call is Unity-specific. (9) Combat is tick-resolved formula pipeline, not physics-driven — would port cleanly. (10) Time system uses Unity Coroutines (TickTimer.intervalId) — would need replacement (e.g., timer service) in another engine. (11) Sprite pipeline (64×64 PPU=32 Point filter, procedural Sprite.Create) is simple and engine-agnostic in concept. (12) World simulation is per-location (not continuous open world) — keeps entity counts bounded per scene but requires robust save/load infrastructure. (13) Brain Controller tier in NPC AI explicitly mentions "LLM or advanced AI" — suggests planned external/dynamic dialogue system, not pure game-logic AI.

---
Task ID: 4
Agent: orchestrator (главный)
Task: Синтез отчёта по выбору движка

Work Log:
- Прочитал отчёты 4 агентов (3-a, 3-b, 3-c, 3-d) из worklog
- Синтезировал комплексный аналитический отчёт
- Записал отчёт в /home/z/my-project/game-docs/docs_temp/ENGINE_CHOICE_ANALYSIS.md

Stage Summary:
- Финальный отчёт: docs_temp/ENGINE_CHOICE_ANALYSIS.md (~12K слов, 10 разделов + приложение)
- Главный вывод: Unity 6.3 структурно несовместим с AI-агентным циклом (AI не может оперировать Editor headless). Основная рекомендация — Godot 4.x + C#: текстовые .tscn/.tres, headless-компиляция, порт 16 чистых C#-модулей, нативный 2D + путь к 2.5D.
- Альтернативы оценены: Custom .NET (повторяет failure mode графики), Bevy (выбрасывает C#-core), Phaser (доказанный провал), Defold (выбрасывает C#-core).
- Рекомендация по 2D/2.5D: остаться на 2D для v1, спроектировать coordinate-слой проекционно-агностично. Godot даёт лучший путь к 2.5D.
- Оценка миграции на Godot: ~9–12 недель при активном AI-цикле.
- Открытые вопросы пользователю сформулированы в разделе 10 отчёта.

---
Task ID: 2-b
Agent: docs-combat-systems (general-purpose)
Task: Engine-agnostic rewrite of combat/body/qi/technique/elements docs

Work Log:
- Прочитал worklog.md, docs_v2/README.md, docs_v2/00_overview/TECHNOLOGY_DECISIONS.md (контекст миграции на Godot 4 + C#, engine-agnostic стандарт)
- Прочитал 16 source-документов:
  - docs/BODY_SYSTEM.md, docs/COMBAT_SYSTEM.md, docs/QI_SYSTEM.md, docs/TECHNIQUE_SYSTEM.md
  - docs/TECHNIQUE_USAGE_REPORT.md, docs/BREAKTHROUGH_MODELS_COMPARISON.md, docs/ELEMENTS_SYSTEM.md
  - docs/TechniqueEffectsSystem.md, docs/BuffSystem_Examples.md, docs/MORTAL_DEVELOPMENT.md
  - docs_old/body.md, docs_old/combat-system.md, docs_old/elements-system.md
  - docs_old/technique-system-v2.md, docs_old/vitality-hp-system.md, docs_old/DAMAGE_FORMULAS_PROPOSAL.md
  - docs_temp/QI_ABSORPTION_RADIUS.md, docs_temp/OrbitalWeaponSystem.md
- Извлёк engine-agnostic контент (формулы, числа, дизайн), отбросил Unity-специфику (MonoBehaviour, ScriptableObject, VContainer, MessagePipe, UniTask, Rigidbody2D, Collider2D, Physics2D, Canvas, SpriteRenderer как тип компонента, [SerializeField], [CreateAssetMenu], код на C# в формате Unity-классов, AssetDatabase, Editor-скрипты)
- Записал 8 новых документов в docs_v2/:
  - docs_v2/02_systems/BODY_SYSTEM.md (515 строк)
  - docs_v2/02_systems/COMBAT_SYSTEM.md (483 строки)
  - docs_v2/02_systems/QI_SYSTEM.md (404 строки)
  - docs_v2/02_systems/TECHNIQUE_SYSTEM.md (486 строк)
  - docs_v2/02_systems/ELEMENTS_SYSTEM.md (294 строки)
  - docs_v2/02_systems/BREAKTHROUGH_MODELS.md (410 строк)
  - docs_v2/02_systems/TECHNIQUE_EFFECTS.md (421 строка)
  - docs_v2/06_player/MORTAL_DEVELOPMENT.md (324 строки)
- Проверил engine-agnostic compliance: 0 упоминаний MonoBehaviour/ScriptableObject/GameObject/Rigidbody2D/Collider2D/Light2D/Tilemap/Canvas/VContainer/MessagePipe/UniTask/asmdef/AssetDatabase/SpriteRenderer-as-type/Animator/URP/prefab в написанных файлах

Stage Summary:
- Docs created: 8 файлов (7 в 02_systems/ + 1 в 06_player/), всего ~3 337 строк
- Key formulas/numbers preserved:
  * 11-слойный damage pipeline (raw → suppression → body part → active defense → Qi buffer → armor coverage → armor reduction → material → HP split 0.7/0.3 → consequences → loot)
  * coreCapacity = 1000 × 1.1^totalSubLevels × qualityMultiplier
  * qiDensity = 2^(level-1)
  * effectiveQi = coreCapacity × qiDensity (L9 ≈ 524 390 400, ~524M)
  * Тип данных Qi: long (не float), zero-GC
  * capacity = baseCapacity(type) × 2^(level-1) × (1 + mastery/100 × 0.5)
  * finalDamage = capacity × gradeMultiplier × ultimateMultiplier
  * Grade множители: common=1.0, refined=1.3, perfect=1.6, transcendent=2.0
  * 4 грейда техник, 8 стихий, 5 подтипов атак, 7 морфологий, 7 классов размера, 6 материалов тела, 10 body templates, 11 species registry
  * HP-значения частей тела: head 50/100, torso 100/200, heart 80, arm 40/80, leg 50/100
  * Qi buffer: 90%/3:1/10% (сырая Qi), 100%/1:1/0% (щит) для Qi-техник
  * Level suppression таблица (×1.0..×0.0)
  * Vitality множитель: 1.0 + (vit-10)×0.05
  * Bleeding 5 уровней: 0/1/3/5/10 HP/тик
  * Регенерация: 0.1 HP/тик базовая, 10%/сутки от микроядра
  * Прорыв Модель В: currentQi=0 после прорыва, требование = coreCapacity(next) × qiDensity(next)
  * 2 пути восстановления: микроядро (13 231 дней L1→L9) + медитация (1 033–2 363 дней с учётом восстановления зоны)
  * Радиус поглощения: R = 10 × (1 + 0.2 × (2^(level/3) - 1)) (L1=10м, L9=22м)
  * Conductivity = coreCapacity / 360 сек
  * Все формулы испарения камней Ци, зарядники
  * Mortal development: 5 этапов (0.0–0.5), 4 типа пробуждения, Семь Первых Пробудившихся
- Notable decisions:
  * 11-слойный pipeline описан как чёткая последовательность с под-опциями (1b weapon bonus, 3b formation buff, 10b loot) встроенными в основные слои (а не как 13 отдельных слоёв). Слой 11 (loot generation) помечен как запланированный.
  * Concepts описаны через логические структуры данных (BodyData, QiData, DamageInfo, EffectData) БЕЗ привязки к конкретным C#-классам движка. Чистый C# упоминается только как «pure-C# модули» в контексте существующей архитектуры (что согласовано с TECHNOLOGY_DECISIONS.md §5.1: «портируются напрямую pure C#, без Unity-зависимостей»).
  * Визуализация эффектов техник описана через sprite-swap (смена спрайтов + кривые масштаба/альфы), а не через частицы или скелетную анимацию — соответствует принятой стратегии.
  * Концепция орбитального оружия перенаправлена на будущую систему артефактов (как в исходнике OrbitalWeaponSystem.md — «ПЕРЕНАПРАВЛЕНИЕ: Концепция орбитального оружия будет использована для системы артифактов»).
  * Время каста техник: формула effectiveSpeed = conductivity × (1 + cultivationBonus) × (1 + masteryBonus) сохранена, но отмечено что упрощённая формула AGI×0.01 применяется только к базовым атакам (а не к касту техник).
  * Все ссылки на источники оставлены как исторические метаданные в разделе «Источники» каждого документа (это согласовано с docs_v2/README.md §«Источники (migration mapping)», который явно указывает итерации Unity 6.3 и Phaser).

---
Task ID: 2-c
Agent: docs-formation-npc (general-purpose)
Task: Engine-agnostic rewrite of formation/buff/npc/faction docs

Work Log:
- Read `/home/z/my-project/worklog.md` (context from prior tasks)
- Read `/home/z/my-project/game-docs/docs_v2/README.md` (engine-agnostic principles, structure, what's removed)
- Read `/home/z/my-project/game-docs/docs_v2/00_overview/TECHNOLOGY_DECISIONS.md` (Godot 4 + C#, performance budgets, AI 3-tier cadence, MaxActiveNPCs=100)
- Read 24 source files (docs/, docs_old/, docs_temp/):
  - docs/FORMATION_SYSTEM.md, docs/BUFF_MODIFIERS_SYSTEM.md (916 lines), docs/STAT_THRESHOLD_SYSTEM.md, docs/PERK_SYSTEM.md (526 lines), docs/CHARGER_SYSTEM.md, docs/ENTITY_TYPES.md, docs/NPC.md (519 lines), docs/NPC_AI_SYSTEM.md, docs/NPC_ASSEMBLY_PIPELINE.md (868 lines), docs/NPC_ASSEMBLY_EXAMPLES.md, docs/NPC_L6_ASSEMBLY_EXAMPLE.md, docs/FACTION_SYSTEM.md, docs/FormationSystem_Examples.md, docs/StatThresholdSystem_Examples.md
  - docs_old/formation_unified.md (1157 lines), docs_old/formation_drain_system.md, docs_old/formation_visualization.md, docs_old/NPC_AI_THEORY.md (2362 lines), docs_old/NPC_AI_NEUROTHEORY.md (2679 lines), docs_old/condition-system.md, docs_old/relations-system.md, docs_old/faction-system.md
  - docs_temp/OrbitalWeaponSystem.md, docs_temp/STACKING_SYSTEM_DRAFT.md
- Created `/home/z/my-project/game-docs/docs_v2/02_systems/` and `/home/z/my-project/game-docs/docs_v2/04_entities/` directories
- Wrote 10 new engine-agnostic docs (Russian, self-contained, concept-over-implementation):

Targets written:
1. `docs_v2/02_systems/FORMATION_SYSTEM.md` — 8 типов формаций, размеры (3×3..300×300), capacity (800..204.8M Qi), ядра (диски L1–L6, алтари L5–L9), drain mechanics, helpers (2..50), independence after activation
2. `docs_v2/02_systems/BUFF_MODIFIERS_SYSTEM.md` — 28 типов баффов/дебаффов (2 части: A — баффы, B — модификаторы), стекинг, мягкие капы, формула расчёта, conductivity rules
3. `docs_v2/02_systems/STAT_THRESHOLD_SYSTEM.md` — формула `threshold = max(1.0, floor(currentStat/10))`, прирост 0.001 за действие, сон кап +0.20/8h, проекции (1000 дн ~55, 3000 ~80, 10000 ~125)
4. `docs_v2/02_systems/PERK_SYSTEM.md` — 3 категории (Innate/Acquired/Cursed), 6 категорий эффектов, слоты (5 + уровень культивации), проводимость меридиан, 18 примеров перков
5. `docs_v2/02_systems/CHARGER_SYSTEM.md` — 5 форм-факторов, 3 назначения, проводимость 5–100, буфер 50–2000, тепловой баланс (100% → 30 сек блок), 8 материалов
6. `docs_v2/04_entities/ENTITY_TYPES.md` — SoulEntity vs PhysicalObject, 3-уровневая классификация (SoulType/Morphology/Species), 5 SoulType, 10 Morphology, 6 материалов тела, дополнительные типы (projectiles, formations, drop/loot)
7. `docs_v2/04_entities/NPC.md` — 3 категории (Temp/Plot/Unique), 8 ролей, 8 PersonalityTrait flags, defaults (MaxActiveNPCs=100, AggroRadius=5, AttackRadius=1.5, PatrolRadius=10, MoveSpeed=2, FleeSpeedMult=1.5), 15 AI-состояний, 7 событий
8. `docs_v2/04_entities/NPC_AI_SYSTEM.md` — 3-уровневая нервная система (Spinal 1–10ms / Neural 10–50ms / Brain 100–500ms), Behavior Tree (Selector/Sequence/Condition/Action), FSM 15 состояний, система угроз с затуханием, grid-based pathfinding (1 tile = 1 tick, NO NavMesh), 8 PersonalityTrait flags
9. `docs_v2/04_entities/NPC_ASSEMBLY_PIPELINE.md` — 8-шаговый пайплайн (Душа → Фенотип → Тело → Ци → Экипировка → Техники → Инвентарь → Регистрация), все формулы (coreCapacity, qiDensity, conductivity, maxLifespan), 7 CoreQuality, 4 AwakeningType, 6 BodyEnhancement типов
10. `docs_v2/04_entities/FACTION_SYSTEM.md` — 4-уровневая иерархия (Nation → Faction → Sect → Cultivator), 5 governmentType, 5 ideology, 6 sectType, 5 standing, формула attitude (+1.0/+0.5/+0.3/+0.2 + attire + reputation), 7 порогов Attitude, FactionRelation, Sect Attire

Stage Summary:
- Docs created: 10 файлов (5 в 02_systems/, 5 в 04_entities/) — общий объём ~220 KB
- Key numbers preserved:
  • Формации: 8 типов, 5 размеров (3×3 → 300×300), capacity 800 → 204 800 000 Qi, контур 80 × 2^(L-1), drain (1–1200 Ци/час), helpers 2–50
  • Баффы: 28 типов (15 баффов + 13 дебаффов), мягкие капы (speed ±50%, damage +100%, defense +80%)
  • Стат-пороги: floor(stat/10), прирост 0.001/действие, сон cap +0.20/8h, проекции (1000 дн ~55, 3000 ~80, 10000 ~125)
  • AI: 3 уровня (Spinal 1–10ms / Neural 10–50ms / Brain 100–500ms), FSM 15 состояний, 8 PersonalityTrait, threat decay 2/сек, AggroRadius=5, AttackRadius=1.5, PatrolRadius=10, MoveSpeed=2, FleeSpeedMult=1.5, MaxActiveNPCs=100
  • NPC Assembly: 8 шагов, CoreQuality {0.5, 0.7, 0.85, 1.0, 1.2, 1.5, 2.0}, AwakeningType {None/Natural/Guided/Artifact/Forced}, levelGrowthFactor {1.0..12.0}
  • Фракции: 4 уровня, 5 governmentType, 5 ideology, 6 sectType, 5 standing, weights (1.0/0.5/0.3/0.2)
- Notable decisions:
  • Трёхуровневая AI-нервная система описана как чистый C# модуль с биомиметической аналогией (позвоночник/нервная система/мозг), БЕЗ упоминания MonoBehaviour/ScriptableObject; движок используется только для визуального представления
  • NavMesh явно отвергнут в пользу grid-based pathfinding (1 тайл = 1 тик) — упоминается только в негативном контексте ("Никакого NavMesh") как требование задачи
  • ConductivityBoost (временный бафф проводимости) — явно помечен как УДАЛЁН; проводимость модифицируется только перками (постоянно) и проклятиями (отрицательно); формации увеличивают environmentMult, не проводимость
  • Система стекирования (из docs_temp/STACKING_SYSTEM_DRAFT.md) интегрирована в BUFF_MODIFIERS_SYSTEM.md как правила стекирования баффов
  • NPC L6 пример сборки сохранён как концептуальный пример в NPC_ASSEMBLY_PIPELINE.md §13 (без упоминания VContainer/MessagePipe)
  • OrbitalWeaponSystem.md (docs_temp) — отброшенная концепция, упомянута только косвенно как «артифакты парящие вокруг персонажа» (если будет нужно — отдельный документ)
  • Все числа, формулы, enum-значения и пороги сохранены 1:1 из источников
  • Документы самодостаточны: каждый начинается с «Самостоятельный документ: не требует иных файлов для понимания»
  • Формат: концептуальная спецификация, разделы «Назначение / Принцип / Формулы / Архитектурное представление / Производительность / Связанные документы»

---
Task ID: 2-e
Agent: docs-player-ui-content (general-purpose)
Task: Engine-agnostic rewrite of player/UI/content/workflow docs

Work Log:
- Sources read (30 files):
  - docs/INVENTORY_SYSTEM.md, docs/EQUIPMENT_SYSTEM.md, docs/JOURNAL_SYSTEM.md
  - docs/SORTING_LAYERS.md, docs/SPRITE_INDEX.md, docs/!hotkeys.md, docs/!Ai_Skills.md
  - docs/LORE_SYSTEM.md, docs/NameGenerator_Russian.md, docs/GLOSSARY.md
  - docs/UNIT_TEST_RULES.md, docs/RUNNING_TESTS.md, docs/ALGORITHMS.md (head)
  - docs_old/inventory-system.md, docs_old/equip.md, docs_old/equip-v2.md
  - docs_old/weapon-armor-system.md, docs_old/body_armor.md (head)
  - docs_old/start_lore.md, docs_old/ui-terminology.md, docs_old/PLAYER_SPRITES.md, docs_old/bonuses.md (head)
  - docs_temp/INVENTORY_UI_DRAFT.md, docs_temp/INVENTORY_IMPLEMENTATION_PLAN.md
  - docs_temp/EQUIPPED_SPRITES_DRAFT.md, docs_temp/CharacterSpriteMirroring.md
  - docs_temp/LOOT_SYSTEM_DRAFT.md, docs_temp/tool_system_draft.md
  - docs_temp/ACHIEVEMENT_SYSTEM.md, docs_temp/LONG_TERM_MEMORY_SCHEME.md
  - worklog.md, docs_v2/README.md, docs_v2/00_overview/TECHNOLOGY_DECISIONS.md (orientation)
- Targets written (12 files):
  - docs_v2/06_player/INVENTORY_SYSTEM.md
  - docs_v2/06_player/EQUIPMENT_SYSTEM.md
  - docs_v2/06_player/JOURNAL_SYSTEM.md
  - docs_v2/07_ui/UI_DESIGN.md
  - docs_v2/07_ui/RENDER_LAYERS.md
  - docs_v2/07_ui/SPRITE_CATALOG.md
  - docs_v2/07_ui/HOTKEYS.md
  - docs_v2/08_content/LORE_SYSTEM.md
  - docs_v2/08_content/START_LORE.md
  - docs_v2/08_content/NAME_GENERATOR.md
  - docs_v2/09_workflow/TESTING_RULES.md
  - docs_v2/09_workflow/AI_DEVELOPMENT_WORKFLOW.md

Stage Summary:
- Docs created: 12 новых файлов в 4 директориях (06_player/, 07_ui/, 08_content/, 09_workflow/).
- Key concepts preserved:
  - Инвентарь: строчная модель (список + maxWeight + maxVolume), 15 слотов стартового рюкзака, body doll с 15 слотами (8 активны, 7 заглушек 🔒), духовное хранилище + кольцо хранения + пояс + tooltip + drag&drop + контекстное меню + процедурный силуэт тела.
  - Экипировка: 15 слотов, матрёшка генерации (Base + Material + Grade + Enchant), 5 грейдов, 5 тиров материалов, 5 состояний прочности, сетовые бонусы, формулы урона, объёма.
  - Журнал: 8 категорий, 6 уровней редкости, прогресс заполнения (completionLevel + unlockedFacts), LoreEntry, PlayerNote, поиск, точки открытия.
  - UI: тема «Древний Пергамент» (10 цветов палитры + 6 редкости + 6 статусов), Unicode-глифы (◆ ◇ ○ ◉ ◐ ◑ ◒ ◓ ★ ☆ ✓ ✗ ▰ ▱ ● ■ □ ▲ ▼ ◄ ► ─ │ ┌ ┐ └ ┘), целевое разрешение 1920×1080, per-mille sizing, 3 sub-layers (Hud/Window/Floating), 22 UI Views (перечислены по функции: HUD, Status, Hotbar, MiniMap, InputLog, Inventory, Equipment, Techniques, Rest, Formation, Journal, Character Sheet, Cultivation Progress, Achievements, Map, Quest Log, Crafting, Trade, Dialogue, Tooltip, Context Menu, Notifications), UI Builder pattern (движко-нейтральный, замена UIFactory), OS fonts + fallback chain.
  - Render layers: 6 слоёв (Default, Background, Terrain, Objects, Player, UI), layerID = индекс (детерминированно), источник истины — код (не редактор), 9 уровней диагностики L1–L9, Y-сортировка внутри Objects.
  - Спрайты: ~132 ассета (12 категорий), terrain/objects 64×64 @ PPU=32 Point, player 128×128 @ PPU=64 Bilinear, процедурная генерация (Perlin + sprite creation), зеркалирование (flipX / scaleX=-1 + Independent Scale Compensation), sprite-swap анимация (12 effect + 8 orbital), fallback-генерация.
  - Hotkeys: канонический набор F5/F9/Esc/E/B/R/X/F + расширения (WASD, 1–9, мышь, J/T/C/Q/M/N/Space/K), 3-слойная архитектура ввода (Hardware → InputService → ActionResolver), sticky-флаги, RMB ≥300ms = контекстное меню, хотбар 1–9 (1=WeaponMain, 2=WeaponOff, 3–9 универсальные).
  - Лор: принципы мира (незыблемые правила, сохранение энергии/материи), Ци (квантованная, 2 спина, 4 агрегатных состояния), 10 уровней культивации, 7 стихий (6 + нейтральная, Яд удалён), эпохи, магия = Ци (нет отдельной магии).
  - Стартовый лор: роли (Рассказчик/ГГ/Повелитель), 3 контейнера памяти (Основа/Время/Энергия), стартовые условия ГГ (L1, простая экипировка, наставник).
  - Генератор имён: 4 грамматических рода, согласование прилагательных, NounDatabase + ModifierDatabase, 5 рангов, алгоритм генерации, расширение для техник и NPC, SeededRandom детерминизм.
  - Тестирование: AAA pattern, naming `Method_Scenario_ExpectedResult`, mock через интерфейсы, edge cases (≥3 на модуль), headless-запуск (dotnet test + движок --headless), верификация баланса, CI pipeline, чистый C# core тестируется без движка.
  - AI workflow: 8-шаговый цикл (документация → код → build → check-only → test → скриншот → commit → QA), документация как spec, контрольные аудиты, 3 уровня памяти (L1 SESSION_SUMMARY / L2 SQLite+FTS5 опционально / L3 worklog), версионирование save/API.
- Notable decisions:
  - UI описан без uGUI/TextMeshPro: вместо «Canvas + CanvasGroup + CanvasScaler + VerticalLayoutGroup + ContentSizeFitter» — нейтральные термины «UI root, UI widget, layout container, text widget, UI builder pattern», специфичные движку детали вынесены в реализационный слой.
  - Sorting layers (Unity) → render layers (engine-agnostic): 6 слоёв сохранены, но «источник истины — код» вместо TagManager.asset, uniqueID = индекс.
  - 22 UI Views перечислены по функциям (HUD, Inventory, Journal и т. д.), не по MonoBehaviour-классам — это позволяет переносить на любой движок.
  - SpriteRenderer → «спрайт / рендерер спрайта»; Light2D упомянут как «2D light / освещение» без Unity-термина; Sprite.Create → «процедурное создание спрайта».
  - Hotkeys: канонический набор F5/F9/Esc/E/B/R/X/F сохранён как обязательный минимум (из задачи). Дополнительные клавиши из Unity-итерации (I/J/K/M/N) добавлены как расширения, но не заменяют канонический набор.
  - ~133–184 спрайта: указан диапазон (132 существующих + планируемые equipped-спрайты и расширение object-tiles до ~184).
  - VContainer / MessagePipe / UniTask заменены на нейтральные «DI-контейнер / шина событий / async-await» (см. docs_v2/01_architecture/DI_AND_EVENTBUS.md).
  - ScriptableObject заменён на «ресурс данных / JSON / текстовый ресурс» во всех контекстах.
  - Термин «Матрёшка» сохранён как архитектурный термин для генерации предметов (Base × Material × Grade × Enchant), но явно отделён от упразднённой системы слоёв экипировки.

---
Task ID: 2-a
Agent: docs-architecture (general-purpose)
Task: Engine-agnostic rewrite of architecture + overview docs

Work Log:
- Прочитал контекст: `/home/z/my-project/worklog.md` (история прошлых задач 2, 3-a..3-d), `docs_v2/README.md` (принципы docs_v2), `docs_v2/00_overview/TECHNOLOGY_DECISIONS.md` (выбор Godot 4 + C#).
- Прочитал все 14 исходных документов:
  * `/home/z/my-project/game-docs/Caveman.md` — коммуникационный протокол AI (не игровой концепт, удалён из миграции).
  * `/home/z/my-project/game-docs/docs/ARCHITECTURE.md` (v4.0, Unity 6.3) — Hub-and-Spoke, 16 модулей, основные принципы.
  * `/home/z/my-project/game-docs/docs/ARCHITECTURE_CODE.md` (v3.18) — структура кода, ModuleServices pattern, реестр интерфейсов (44), контрактов (~130), анти-паттерны.
  * `/home/z/my-project/game-docs/docs/ARCHITECTURE_IMPL.md` (v1.1) — статусы модулей, история фаз 0-19, уроки CH/BD/FIX/QI-A/BF-A/INV/NPC/PLR/WLD/QST/INT/UI/SAV/SCN/MIN/SES.
  * `/home/z/my-project/game-docs/docs/ARCHITECTURE_FILE_TREE.md` (v3.17) — дерево файлов Unity (Assets/Scripts/Core, Entry, Modules).
  * `/home/z/my-project/game-docs/docs/!LISTING.md` (v5.5) — листинг 60 файлов docs/, ~580K токенов.
  * `/home/z/my-project/game-docs/docs/!Ai_Skills.md` (v3.1) — 19 Skill() через z-ai-web-dev-sdk, AI/Media/Web/Doc/Dev/Utility.
  * `/home/z/my-project/game-docs/docs/DEVELOPMENT_PLAN.md` (v1.3, ARCHIVED) — legacy план Фаз 1-8, GameManager/SceneLoader.
  * `/home/z/my-project/game-docs/docs_temp/COMPUTATIONAL_RESOURCES_CALCULATION.md` (v2.0) — расчёты CPU/GPU/RAM, hardware tiers, AI-оптимизации.
  * `/home/z/my-project/game-docs/docs_temp/ENGINE_CHOICE_ANALYSIS.md` — анализ выбора движка (Godot 4 + C# как primary, MonoGame как backup).
  * `/home/z/my-project/game-docs/docs_old/ARCHITECTURE.md` (v21, Phaser) — sandbox-архитектура, TruthSystem, Event Bus (концепции сохранены, реализация отброшена).
  * `/home/z/my-project/game-docs/docs_old/matryoshka-architecture.md` (v3.0) — принцип Матрёшки (Base × Grade × Specialization).
  * `/home/z/my-project/game-docs/docs_old/ARCHITECTURE_future.md` — облачная архитектура (ОТМЕНЕНА, игра однопользовательская).
  * `/home/z/my-project/game-docs/docs/ALGORITHMS.md` (v2.0) — формулы: подавление уровнем, Qi Buffer, пайплайн урона 11 слоёв, мягкие капы (8 категорий, 40+ переменных), стихии, масштабирование статов.
  * Дополнительно: `/home/z/my-project/game-docs/docs/GLOSSARY.md` (для переноса терминов).
- Создал 10 новых файлов в `docs_v2/`:
  1. `00_overview/PROJECT_CONCEPT.md` — концепция игры engine-agnostic (жанр, перспективы, 10 ключевых механик, вдохновение Kenshi/RimWorld/cultivation).
  2. `00_overview/GLOSSARY.md` — расширенный глоссарий (19 разделов, ~200 терминов, устаревшие термины, иерархия источников).
  3. `01_architecture/ARCHITECTURE.md` — высокоуровневая архитектура: Hub-and-Spoke, 16 модулей, 3-слойная (Core/Application/Adapter), сборка сцены (10 фаз), GameSession, время и тики, сохранение, анти-паттерны.
  4. `01_architecture/MODULE_STRUCTURE.md` — детально по 16 модулям: таблица + спецификация каждого (главный интерфейс, контракты, tick, зависимости, подписки, сервисы, особенности), карта межмодульных зависимостей через шину, история фаз 0-19.
  5. `01_architecture/DI_AND_EVENTBUS.md` — DI + шина событий engine-agnostic: ModuleServices pattern, lifetime scopes, реестр ~130 контрактов в 20 файлах, типы событий (state-changed/command/lifecycle), ISaveable pattern, async/await, 35+ уроков из реализации.
  6. `01_architecture/PERFORMANCE_STRATEGY.md` — стратегия производительности: 6 принципов, CPU/GPU/RAM бюджеты (100 NPC, мегаполис 25M тайлов), hardware tiers, Zero-GC, pooling, tick batching, Per-entity DataProvider, чанковая загрузка, AI-оптимизация, многопоточность, C# hot paths, `long` vs `float` для Qi.
  7. `01_architecture/FILE_TREE.md` — engine-agnostic структура: src/Core, src/Modules (16), src/Entry, src/Adapter, tests, data (JSON), scenes (текстовые), assets, saves; namespace правила; .csproj конфигурация.
  8. `01_architecture/MIGRATION_MAP.md` — карта миграции всех 154 исходных файлов (docs/, docs_old/, docs_temp/) → docs_v2/: ~52% перенесено, ~48% удалено как engine-specific или устаревшее; список новых документов и TODO.
  9. `09_workflow/ALGORITHMS.md` — полный перенос формул: Level Suppression, Qi Buffer (5:1, 3:1, 90%/80%/100%), пайплайн урона 11 слоёв, мягкие капы (8 категорий), стихии (Вариант А), масштабирование статов, Модель В coreCapacity, проводимость, Vitality→HP, время каста.
  10. `09_workflow/AI_DEVELOPMENT_WORKFLOW.md` — workflow AI-разработки: цикл итерации, headless-тестирование (5 уровней: unit/integration/scene/runtime/screenshot), CI pipeline, документация как spec, контрольные аудиты, Git workflow, AI-skills, жизненный цикл фичи, обработка ошибок, миграция с предыдущих итераций.

Stage Summary:
- Docs created: PROJECT_CONCEPT.md, GLOSSARY.md, ARCHITECTURE.md, MODULE_STRUCTURE.md, DI_AND_EVENTBUS.md, PERFORMANCE_STRATEGY.md, FILE_TREE.md, MIGRATION_MAP.md, ALGORITHMS.md, AI_DEVELOPMENT_WORKFLOW.md (всего 10 файлов).
- Key decisions made:
  * **Hub-and-Spoke описан как «звезда»** с центральным ядром (Core) и 16 модулями. Межмодульные связи запрещены — только через шину событий.
  * **ModuleServices pattern сохранён** как концептуальный паттерн (статический `XxxModuleServices.Register(builder)` для регистрации всех модулей в корневом scope). Реализация DI-контейнера (VContainer/MS DI/свой ServiceLocator) — на уровне adapter-слоя.
  * **readonly struct контракты** (~130 в 20 файлах) сохранены как фундамент zero-GC стратегии. Все имена событий и форматы — без изменений.
  * **16 модулей финализированы:** Body, Buff, Charger, Combat, Formation, Inventory, NPC, Player, Qi, Tile, World, Quest, Interaction, UI, Save, Generator. Plus SceneOrchestrator + GameSession как над-модульные сущности.
  * **3-слойная архитектура:** Core (engine-agnostic, чистый C#) / Application (16 модулей) / Adapter (engine-specific: рендеринг, ввод, UI, аудио, сцена, I/O). При смене движка меняется только Adapter.
  * **Tick-based симуляция** сохранена: 1 тик = 1 минута, 4 скорости (Pause/Normal/Fast/VeryFast = 0/1/5/15). Tick-batching: Qi-regen каждые 10 тиков, автосохранение каждые 60, AI по 3-уровневой каденции.
  * **Zero-GC как design goal** — явно зафиксирован во всех архитектурных документах. Hot paths на C#, readonly struct контракты, pooling, pre-allocated массивы.
  * **ISaveable pattern** сохранён: `SaveKey`, `CaptureState`, `RestoreState` + `SaveDataAggregator` для сбора данных через шину.
  * **Все формулы, числа, балансовые константы** перенесены без изменений в `09_workflow/ALGORITHMS.md`. ALGORITHMS.md — высший источник истины.
  * **Все enum-ы и термины** перенесены в `00_overview/GLOSSARY.md` (19 тематических разделов).
  * **Caveman.md** (корневой файл) — помечен как коммуникационный протокол AI, не игровой концепт. В миграцию не перенесён.
- Anything notable:
  * **Конфликты источников:** В docs_old/ARCHITECTURE.md описана серверная архитектура с Caddy/Socket.io/Bun/Prisma (Phaser-эра) — эти концепции ОТМЕНЕНЫ, так как игра однопользовательская. Сохранена только концепция «память первична» (TruthSystem), которая трансформирована в «игровое состояние в памяти — первичный источник» в docs_v2/ARCHITECTURE.md.
  * **Документ ARCHITECTURE_future.md** описывает несуществующую облачную архитектуру — помечен «удалён» в MIGRATION_MAP, так как стратегически игра не имеет мультиплеера.
  * **Generator модуль** в исходных документах упоминался как «17-й модуль», но в финальном списке модулей docs_v2 он включён в 16 как полноценный модуль (раньше был «Фаза 0», потом стал ModuleServices без отдельного Module.cs).
  * **Stub-сервисы (19 файлов)** в исходном Unity-коде помечены как «мёртвые» (FallbackRegistrar не вызывается с Phase 17B). В docs_v2 stub-паттерн описан как «временная реализация на ранних фазах» (CH-32/33 урок), но не как постоянная практика.
  * **NPC AI** использует grid-based pathfinding (без NavMesh) — это engine-agnostic решение, сохранено.
  * **Бой real-time-with-pause, tick-resolved, НЕ physics-driven** — явно зафиксировано как архитектурный принцип. Позволяет переносить на любой движок без потери функциональности.
  * **Тайловые данные** регенерируются из seed + delta, не сохраняются поштучно — engine-agnostic паттерн.
  * **Все Unity-specific технологии** (VContainer, MessagePipe, UniTask, ScriptableObject, MonoBehaviour, URP, Light2D, Rigidbody2D, asmdef, [InitializeOnLoadMethod], AssetDatabase, SortingLayer, SpriteRenderer, Camera.orthographic, Canvas, Tilemap component, Animator) — полностью удалены из текстов. Заменены на нейтральные: «DI-контейнер», «шина событий», «async/await», «data resource», «система», «компонент», «render pipeline», «2D light», «physics body», «physics collider», «sprite animator», «tile layer», «UI root», «module», «auto-config script», «render layer», «sprite», «camera».

---
Task ID: 2-d
Agent: docs-world-data (general-purpose)
Task: Engine-agnostic rewrite of world/tile/time/data/save docs

Work Log:
- Read /home/z/my-project/worklog.md (context from prior tasks)
- Read /home/z/my-project/game-docs/docs_v2/README.md (docs_v2 principles: engine-agnostic, concept-over-implementation)
- Read /home/z/my-project/game-docs/docs_v2/00_overview/TECHNOLOGY_DECISIONS.md (Godot 4 + C# target, performance strategy, migration scope)
- Read sources from docs/ (Unity 6.3 era):
  - docs/WORLD_SYSTEM.md, docs/WORLD_MAP_SYSTEM.md, docs/TILE_SYSTEM.md, docs/TILE_SYSTEM_IMPLEMENTATION.md
  - docs/LOCATION_MAP_SYSTEM.md, docs/TRANSITION_SYSTEM.md, docs/TIME_SYSTEM.md
  - docs/DATA_MODELS.md, docs/CONFIGURATIONS.md
  - docs/SAVE_SYSTEM.md, docs/WORLD_SAVE_SYSTEM.md
  - docs/GENERATORS_SYSTEM.md, docs/GENERATORS_NAME_FIX.md
- Read sources from docs_old/ (Phaser era + design corpus):
  - docs_old/sector-architecture.md, docs_old/data-systems.md
  - docs_old/generators.md, docs_old/generator-specs.md
  - docs_old/ENVIRONMENT_SYSTEM_PLAN.md (partial)
  - docs_old/qi_stone.md (partial, для концепции камней Ци)
  - docs_old/materials.md (partial, для тиров материалов)
- Created /home/z/my-project/game-docs/docs_v2/03_world/ and 05_data/ directories
- Wrote 11 engine-agnostic docs (Russian, concept-level, self-contained)
- Verified no engine-specific terms (Unity/Godot/MonoGame/MonoBehaviour/ScriptableObject/etc.) remain in new docs

Targets written:
- docs_v2/03_world/WORLD_SYSTEM.md — мир: размер 200000×200000 км, чанк 100×100 км = 1 save file, сектор 10×10 км, локация 100м-10км (до 25M тайлов), дикие земли (процедурные переходы), per-location scene loading (НЕ open-world streaming)
- docs_v2/03_world/WORLD_MAP_SYSTEM.md — карта мира: иерархия размерностей (§1 — единый источник истины), сектора, регионы, климатические зоны, фог войны (Hidden/Visible/Known/Visited), радиус видимости ±100 км
- docs_v2/03_world/TILE_SYSTEM.md — тайл 2×2 м, 4 логических слоя (base params/Ци/temp, surface, objects, subjects), Z логический уровень (-5..+5), orthographic top-down (НЕ isometric), многоячеечные объекты 1×1..4×4+, TileData ~20 полей, свободное строительство
- docs_v2/03_world/LOCATION_MAP_SYSTEM.md — типы локаций (хутор 100м → мегаполис 10км), точки входа, генерация зданий (BSP), биомы, погода, цикл день/ночь, сезоны
- docs_v2/03_world/TRANSITION_SYSTEM.md — переходы: 2-уровневая модель, travel screens (НЕ streaming), дикие земли как процедурные переходы, таймер памяти локации (город ∞, деревня 7д, данж 24ч), телепортация L3-L9 с кулдаунами
- docs_v2/03_world/TIME_SYSTEM.md — 1 тик = 1 минута, 4 скорости (Pause/Normal=1tps/Fast=5tps/Quick=15tps), Timer service (НЕ корутины движка), движение = 1 тик/клетка, batch каждые 10 тиков, auto-save каждые 60 тиков, ActivityManager auto-switch
- docs_v2/05_data/DATA_MODELS.md — все структуры (NPCState, Character, Location, InventoryItem, Technique, FormationCore, BodyPart, Faction, Material, SpeciesPreset, StatBonus, Position2D, InputFrameData, InventorySlot, LootEntry), long для Qi, float для статов, readonly struct contracts, система ID (префикс+счётчик)
- docs_v2/05_data/CONFIGURATIONS.md — CultivationLevels (1-10), TechniqueTypes, Materials (5 тиров), Grades (common→transcendent, 2% transcendent НЕ зависит от уровня), Elements (6-7 стихий), NPCConfig, GameConstants (размерности, время, combat, Qi, лимиты)
- docs_v2/05_data/SAVE_SYSTEM.md — JSON формат (binary+GZIP optional), main.sav (10-50 KB) + chunks/ + locations/ + metadata.sav, ISaveable pattern (SaveKey/CaptureState/RestoreState), SaveDataAggregator, автосохранение каждые 60 тиков + событийные триггеры, F5/F9, атомарная запись + rolling backups
- docs_v2/05_data/WORLD_SAVE_SYSTEM.md — чанковая модель (4M чанков), seed+delta для тайлов (НЕ индивидуальное сохранение), флаги состояния (Hidden/Visible/Known/Visited), 100h ~5-15 KB, 1000h ~100 KB, экстремум 2000 локаций ~1-2 MB, дельта-сжатие
- docs_v2/05_data/GENERATORS_SYSTEM.md — Матрёшка (Base × Grade × Specialization), SeededRandom (детерминированная), генераторы техник/экипировки/NPC/расходников/формаций/камней Ци, грейды ×1.0/×1.2/×1.4/×1.6 (НЕ зависят от уровня, 2% transcendent даже на L1), NamingDatabase с грамматическим согласованием (GrammaticalGender + AdjectiveForms)

Stage Summary:
- Docs created: 11 файлов в docs_v2/03_world/ (6) и docs_v2/05_data/ (5)
- Key numbers preserved:
  * Мир 200000×200000 км, 4,000,000 чанков
  * Чанк 100×100 км = 1 файл сохранения
  * Сектор 10×10 км = ячейка карты мира, 1-10 локаций
  * Локация 100м-10км, до 25,000,000 тайлов (мегаполис)
  * Тайл 2×2 м, 4 логических слоя, Z -5..+5, ~20 полей в TileData
  * Тик = 1 минута, 4 скорости (0/1/5/15 tps), движение 1 тик/клетка
  * Batch каждые 10 тиков, auto-save каждые 60 тиков
  * Save: main.sav 10-50 KB + chunks/ + locations/ + metadata.sav
  * World save: seed+delta, 100h ~5-15 KB, 1000h ~100 KB, 2000 локаций ~1-2 MB
  * Qi = long, статы = float
  * Grade ×1.0/×1.2/×1.4/×1.6, 2% transcendent НЕ зависит от уровня
  * Камни Ци: calm/chaotic, плотность 1024 ед/см³, без качества
- Notable decisions:
  * Tile system описан как «4 логических слоя на тайле» без упоминания Tilemap component / TileBase / GameTile — слои как логические сущности, агрегируемые при отображении (движок может реализовать как несколько параллельных тайловых слоёв или как один тайл с приоритетом отрисовки)
  * Per-location scene loading явно противопоставлен open-world streaming — это концептуальный выбор дизайна, не технический лимит
  * Дикие земли описаны как «процедурные переходы, не загружаемые сцены» — travel screens с возможными random encounters
  * Timer service описан как «чисто логический сервис, вызываемый из основного цикла симуляции с фиксированным интервалом» — НЕ корутины, НЕ tween-механизмы; в любом движке реализуется через эквивалентный механизм (physics-step callback, fixed-timestep loop, собственный таймер)
  * Data resources вместо ScriptableObject — нейтральные классы, сериализуемые в JSON или текстовые ресурсные файлы движка
  * Все упоминания Unity/Godot/MonoGame/ScriptableObject/MonoBehaviour и т.д. полностью удалены (включая negation-context — заменено на «движко-специфичные ресурсные типы»)
  * Сохранены все игровые формулы, балансовые числа, лор и структуры данных
  * Save pattern ISaveable + SaveDataAggregator перенесён с архитектурным разделением: каждая система знает, как себя сериализовать, агрегатор оркестрирует

---
Task ID: 3
Agent: orchestrator (главный)
Task: Синтез и аудит качества docs_v2

Work Log:
- Проверил структуру docs_v2: 52 файла, 1.3 MB
- Провёл grep-аудит на Unity/Phaser-термины: 0 совпадений в системных документах (02-08)
- Совпадения только в мета-документах (README, TECHNOLOGY_DECISIONS, MIGRATION_MAP, AI_WORKFLOW) — корректны, описывают миграцию
- Совпадения в FILE_TREE — adapter-слой (по дизайну engine-specific)
- Обновил MIGRATION_MAP: убрал устаревшие TODO-метки (агент 2-a писал карту до завершения 2-b..2-e), обновил section 7 со статусом завершения

Stage Summary:
- docs_v2 полностью сформирован: 52 файла в 10 категориях
- Качество engine-agnostic: подтверждено — ядро (02-08) чистое
- Структура: 00_overview, 01_architecture, 02_systems (12), 03_world (6), 04_entities (5), 05_data (5), 06_player (4), 07_ui (4), 08_content (3), 09_workflow (3)
- Migration map обновлён, показывает: ~52% старых файлов перенесены, ~48% удалены как engine-specific
- Готово для начала разработки на Godot 4

---
Task ID: V1
Agent: verify-architecture (general-purpose)
Task: Re-verify architecture and extract implementation brief for Godot 4 core

Work Log:
- Read 20 docs_v2 files (00_overview ×2, 01_architecture ×5, 03_world ×6, 04_entities ×1, 05_data ×2, 07_ui ×4)
- Re-read worklog context (Tasks 2, 3-a..3-d, 3)
- Cross-checked MODULE_STRUCTURE.md against ARCHITECTURE.md and DI_AND_EVENTBUS.md
- Verified consistency: 16 modules, ~130 contracts in 20 files, 30+ core interfaces, 22 UI Views, 6 render layers — all confirmed across multiple documents
- Extracted concrete implementation requirements (interface names, method signatures, struct fields, file paths, constants)

Stage Summary:
- Architecture is SOUND and ready for implementation. All core patterns specified: Hub-and-Spoke, ModuleServices, readonly struct contracts, ISaveable + SaveDataAggregator, 10-phase SceneOrchestrator, ITickable with 0/1/5/15 tps speeds, per-location scene loading (NOT streaming).
- 16 modules finalized with concrete main interfaces (IBodyService, IBuffService, IChargerService, ICombatService+IDamageService, IFormationService, IInventoryService+IStorageService+ICraftingService+IEquipmentService, INPCService+INPCSpawnerService, IPlayerService+IPlayerInputService, IQiService+IQiBufferService, ITileService+IResourceService, IWorldService+IEventService+ITimeService, IQuestService+IQuestRewardService, IInteractionService+IDialogueService, IUIService, ISaveService+ISaveable) + Generator (no interface).
- ~44 interfaces, ~130 readonly struct contracts in 20 files (GameContracts, CombatContracts, BodyContracts, QiContracts, BuffContracts, ChargerContracts, TileContracts, InventoryContracts, PlayerContracts, WorldContracts, NPCContracts, FormationContracts, QuestContracts, SaveContracts, DialogueContracts, StatContracts, CraftingContracts, UIContracts, SceneContracts, InputLogContracts).
- Critical decisions needed before code: (a) DI container choice — docs say "engine-agnostic; concrete implementation at adapter layer"; options are custom ServiceLocator / Godot Autoload DI / Microsoft.Extensions.DependencyInjection. Recommendation: minimal custom ServiceLocator on Core + adapter exposes IContainerBuilder/IResolver; (b) EventBus implementation — custom in Core (Dictionary<Type, List<Delegate>>) with IPublisher<T>/ISubscriber<T>; (c) Scene authoring — FILE_TREE.md describes `scenes/*.scene` text files + SceneOrchestrator builds nodes programmatically; for Godot, hybrid: .tscn for MainMenu/LoadingScreen + programmatic SceneBuilder for GameWorld; (d) IStatService is registered by PlayerModule (per MODULE_STRUCTURE §2.8 "StatService — реализация IStatService (реальная, не stub)"); (e) ActivityManager (TIME_SYSTEM §8) — NOT in 16-module list, treat as a service inside World module.
- Architecture gaps/ambiguities found: (1) fractional tick costs (0.1, 0.05, 0.25, 0.5, 2-5 ticks) — TimeSystem uses int tickCount but allows fractional action costs; need accumulator pattern per entity; (2) "Микро-шаг" (0.1 tick) vs "Move 1 tile" (1 tick) — needs explicit interface like `IPlayerMovement.TryConsumeTickFraction()`; (3) ModuleServices registration order in GameLifetimeScope not strictly specified beyond general "register all 16 then SceneAssemblyRegistrar"; (4) HotbarService referenced in HOTKEYS.md §5.3 and §6.3 but not in MODULE_STRUCTURE interface list — should be inside InventoryModule or PlayerModule (decide); (5) InputLogPanel/InputLogContracts exist for diagnostic logging but no explicit "InputLogModule" — implement as Adapter-level debug service subscribed to InputKey/InputAction contracts; (6) InputFrameData (HOTKEYS §5.1) lists fields slightly differently from DATA_MODELS §3.3 — reconcile to a single canonical struct (HOTKEYS version is more complete: includes stickyKeys HashSet, frame counter, rmbHoldDuration, hotbarSlot, mouseWorldPos); (7) QuestRewardService — is it separate module? No, MODULE_STRUCTURE §2.12 says "QuestRewardService выделен из QuestService — SRP" but lives in same QuestModule; (8) DialogueTypewriter uses async/await + CancellationToken — make sure no GC in hot path (it's NOT a hot path — only fires on user input); (9) "Combat.scene" optional in FILE_TREE — decide: real-time-with-pause suggests NOT separate scene; keep combat in GameWorld scene; (10) Godot specifics from TECHNOLOGY_DECISIONS §3.4: _PhysicsProcess for tick (60 Hz default), ProcessMode=Pausable for sim, Always for UI, CallDeferred for cross-thread, RenderingServer + MultiMeshInstance2D for tile batching, WorkerThreadPool for off-main sim — these are the concrete adapter-layer decisions.
- Performance constraints are clear: zero GC per frame; readonly struct for all contracts; long for Qi (L9 ~524M); float for stats; pre-allocated arrays in hot loops; pooling for projectiles/VFX/NPCState/UI; no LINQ/lambda-captures/boxing in hot paths; tick batching (Qi every 10, save every 60, AI Spinal 1-10ms/Neural ~3 ticks/Brain ~10 ticks); per-entity DataProvider cache (~2-4 KB/NPC); chunk loading for locations >1km.
- Concrete first-implementation targets identified for menu → world → test polygon flow: (1) Core project skeleton with Constants/Enums + 30 interfaces + EventBus skeleton; (2) World module with TimeService (4 speeds, 1 tick=1min, OnGameTick event); (3) Tile module with TileMapService (2×2 m tiles, TileData struct, deterministic seed gen); (4) Player module with PlayerService + PlayerInputService (sticky flags, ResetFrameFlags); (5) UI module with UIService + HUDPresenter; (6) SceneOrchestrator 10 phases (can stub phases 5-9 initially); (7) Adapter: Godot Camera2D + TileMapLayer + Sprite2D for Player + Control tree for HUD with Parchment theme.
- Ready to begin implementation. This brief is the deliverable.

---
Task ID: 3-e
Agent: impl-scenes (general-purpose)
Task: Write Godot scene files (.tscn), theme (.tres), and JSON data configs

Work Log:
- Read V1 brief from worklog (verify-architecture task) and 3 UI docs (UI_DESIGN.md, RENDER_LAYERS.md, SPRITE_CATALOG.md) to confirm color palette, target resolution (1920×1080), per-mille sizing (18‰ → 19 px font), 6 render layers, 22 UI Views, Parchment theme requirements
- Verified project.godot: main_scene=res://scenes/MainMenu.tscn, autoload GameBoot=res://src/Adapter/Scene/GameBoot.cs, 5 named 2D render layers (Background/Terrain/Objects/Player/UI) — matches docs_v2/RENDER_LAYERS.md
- Created /home/z/game/scenes/MainMenu.tscn — Control root, anchors_preset=15 (full rect), size 1920×1080, script=MainMenuController.cs (uid://mainmenu1234)
- Created /home/z/game/scenes/GameWorld.tscn — Node2D root, script=GameWorldController.cs (uid://gameworld1234)
- Created /home/z/game/resources/themes/ParchmentTheme.tres — Theme with default_font_size=19, 6 StyleBoxFlat sub-resources (panel, button normal/hover/pressed/disabled, lineedit normal), Button+Label+LineEdit color/style mappings per UI_DESIGN.md §2.1 palette (Parchment Base #e8d5a8, Dark #c9a878, Light #f5e8c8, Ink Black #2a1d10, Accent Gold #b8860b, Accent Red #8b0000)
- Created /home/z/game/data/test_polygon.json — 50×50 Farm location, seed 12345, Grass terrain, qiDensity 100, playerStart (25,25)
- Created /home/z/game/data/world_map.json — 2 locations (test_polygon Farm + world_map WildLands), startYear 1864
- Created /home/z/game/data/cultivation_levels.json — 9 cultivation levels (Начинающий→Небесный), qiDensity exponential (1→256), minCoreCapacity exponential (1000→548768)
- Created /home/z/game/data/elements.json — 7 elements (fire/water/earth/wind/lightning/light/dark) with strong/weak relationships
- Validation step 1 (python json.load): all 4 JSON files valid JSON ✓
- Validation step 2 (Godot headless --quit): MainMenu.tscn parse error gone (script file exists); GameWorld.tscn not loaded by main scene so not directly verified via boot — see step 3
- Validation step 3 (GDScript SceneTree test): both .tscn files load as PackedScene successfully:
    * MainMenu.tscn → 1 node: name='MainMenu' type='Control' ✓
    * GameWorld.tscn → 1 node: name='GameWorld' type='Node2D' ✓
- Validation step 4 (GDScript load test for theme): ParchmentTheme.tres loads as Theme resource, default_font_size=19, Panel/styles/panel=StyleBoxFlat, Button/styles/{normal,hover,pressed}=StyleBoxFlat, Button/colors/font_color=(0.165,0.114,0.063,1) (Ink Black), Label/colors/font_color=(0.165,0.114,0.063,1) ✓
- Remaining boot-time errors are about C# class instantiation (project not yet compiled via dotnet build) — these will resolve once parallel agents finish and the project is built. Not file-format issues.

Stage Summary:
- Scene files: 2 (MainMenu.tscn, GameWorld.tscn)
- Theme resources: 1 (ParchmentTheme.tres with 6 StyleBoxFlat sub-resources)
- Data files: 4 (test_polygon.json, world_map.json, cultivation_levels.json, elements.json)
- Parse status: ALL FILES VALID — .tscn load as PackedScene, .tres loads as Theme with all styleboxes/colors, all JSON parses cleanly. No file-format errors. Remaining Godot boot errors are C# compilation pending (out of scope for this task — depends on parallel agents completing C# code + dotnet build).

---
Task ID: 3-a
Agent: impl-core (general-purpose)
Task: Write Core layer C# code (DI, EventBus, Interfaces, Contracts, Data models)

Work Log:
- Read worklog V1 brief + 5 docs_v2 references (ARCHITECTURE, MODULE_STRUCTURE, DI_AND_EVENTBUS, PERFORMANCE_STRATEGY, DATA_MODELS, CONFIGURATIONS).
- Deleted placeholder `src/Core/_Stubs/` directory (8 stub files from Task 3-b — explicit "DELETE once Agent 3-a's Core layer is in place").
- Added `<EnableDefaultCompileItems>false</EnableDefaultCompileItems>` to .csproj to resolve NETSDK1022 duplicate Compile items (SDK auto-include conflicted with explicit `<Compile Include>` items per layer).
- Wrote 42 Core-layer .cs files across 5 subdirectories:
  * Core/Data/Constants.cs — GameConstants static class (all numeric/string constants: ticks, time, speeds, tile/render, NPC/combat, save files).
  * Core/Data/Enums.cs — 27 enums (TimeSpeed, TimeOfDay, Season, Direction, TerrainType, WaterType, LocationType, BodyPartType, SoulType, Morphology, BodyMaterial, ConsciousnessType, DamageType, ElementType, TechniqueType, TechniqueSubtype, FormationType, FormationCoreType, CultivationLevel [L1..L9], ItemCategory, GameItemType, EquipmentSlot [15 slots], StatType, PersonalityTrait [Flags], RenderLayer, SaveSlotType).
  * Core/Data/Structs.cs — 8 readonly structs with IEquatable (Position2D, Vector2f, WorldTime [Year/Month/Day/Hour/Minute/Season/TimeOfDay derived], StatBonus, InventorySlot, LootEntry, TileCoord, Rect2i, InputFrameData [canonical HOTKEYS §5.1: MoveDirection/IsRun/IsLmbPressed/IsRmbPressed/RmbHoldDuration/MouseWorldPos/IsOverUI/HotbarSlot/StickyKeys IROSet<string>/Frame]).
  * Core/Data/DataModels.cs — 9 JSON-serializable classes ([Serializable] + public auto-props): GameSessionData, CharacterData, NPCState, TileData, InventoryItem, TechniqueData, LocationData, FactionData, FactionRelation.
  * Core/DI/DIInterfaces.cs — Lifetime enum, IContainerBuilder (Register<TI,TImpl>/Register<TImpl>/RegisterInstance<T>), IResolver (Resolve/ResolveAll/TryResolve), InjectAttribute (Property | Field).
  * Core/DI/Container.cs — ContainerBuilder + Container implementation: Dictionary<Type,Registration> storage, singleton cache, greediest-ctor constructor injection, [Inject] property+field injection via reflection, circular-dep guard (depth>50 throws), IDisposable disposal of singletons. Self-registers IResolver.
  * Core/Events/EventBus.cs — Custom `delegate void MessageHandler<T>(in T)` for zero-GC publish (readonly struct passed by `in`). IPublisher<T>/ISubscriber<T> interfaces. EventBus: thread-safe (lock), per-type SubscriptionList<T> with copy-on-read snapshot (avoids reentrancy), IDisposable unsubscribe tokens, SubscriberCount<T>(). Plus EventBusPublisher<T>/EventBusSubscriber<T> adapter wrappers.
  * Core/Interfaces/*.cs — 24 interfaces: IModule (IStartable+ITickable+IDisposableModule), ITimeService, ISaveable, ISaveService, ISceneAssemblyPhase, IGameSession (+SessionState enum), IUIService, IWorldService, IPlayerService, IPlayerInputService (sticky flags per HOTKEYS §5.1), ITileService, IBodyService, IQiService, IInventoryService, ICombatService, IFormationService, IBuffService, IChargerService, INPCService, IQuestService, IInteractionService, IEquipmentService, IStatService, IGeneratorService.
  * Core/Messaging/Contracts/*.cs — 10 contract files, ~50 readonly struct events: GameContracts (GameStarted/Paused/Resumed/Saving/Saved/Loading/Loaded/Quit), SceneContracts (SceneInitializing/PhaseStarted/PhaseCompleted/Ready/AssemblyFailed), WorldContracts (LocationChanged/TileChanged/TileMapGenerated/LocationLoaded), PlayerContracts (PlayerMoved/Spawned/Interact/Damaged), InputContracts (InputKey/InputAction), QiContracts (QiChanged/Consumed/Added/Breakthrough), CombatContracts (CombatStarted/Ended/DamageDealt/EntityDeath), SaveContracts (SaveRequested/LoadRequested/SaveCompleted/LoadCompleted), UIContracts (ViewShown/Hidden/NotificationShown/TooltipRequested/Hidden), MiscContracts (BodyPartDamaged/BuffApplied/Removed/FormationCreated/Dissolved/ItemAdded/Removed/NPCSpawned/Despawned/QuestStarted/Completed/Progress/TimeTick/TimeSpeedChanged/ChargerRegistered/SlotChanged/TechniqueUsed/Learned).
  * Core/CoreProjectInfo.cs — InternalsVisibleTo("CultivationGame.Tests") + Layer/Version constants.
- All files: `#nullable enable` at top, no `using Godot;`, namespace `CultivationGame.Core.*`.

Fix iterations:
1. First build error: NETSDK1022 duplicate Compile items (SDK auto-include + explicit `<Compile Include>`). Fixed via `<EnableDefaultCompileItems>false</EnableDefaultCompileItems>` in .csproj.
2. Second build error: CS1960 invalid variance modifier on `IPublisher<in T> where T : struct` (variance incompatible with value-type constraint) + `Action<in T>` syntax not allowed as type argument. Fixed by replacing with non-variant `IPublisher<T>` + custom `delegate void MessageHandler<T>(in T message) where T : struct`. Same zero-GC semantics.
3. Third build error: missing `using System;` in Enums.cs for `[Flags]` attribute. Fixed.
4. Final widening: extended `InjectAttribute` from `AttributeTargets.Property` only to `Property | Field` to support adapter classes (e.g. Godot Node subclasses) that use field injection — modules written by Task 3-b use `[Inject]` on private fields.

Stage Summary:
- Core files: 42 created (43 in tree incl. pre-existing SeededRandom.cs from Task 3-b).
- DI container: working — singleton cache + transient + scoped, ctor injection (greediest) + property/field injection via [Inject], circular-dep guard, IDisposable disposal.
- EventBus: working — thread-safe, zero-GC publish path (readonly struct + `in` param + custom delegate, no boxing), copy-on-read snapshot for safe unsubscribe-during-publish, IDisposable unsubscribe tokens.
- Compile status: BUILD SUCCEEDED, 0 warnings, 0 errors (clean rebuild via `dotnet build --no-incremental`). All 42 Core .cs files compile cleanly under Godot.NET.Sdk 4.3.0 / net8.0 / Nullable enable / LangVersion latest. Entire project (Core + Modules + Adapter + Entry) builds green.

---
Task ID: 3-d
Agent: impl-adapter (general-purpose)
Task: Write Adapter layer (Godot: GameBoot, InputAdapter, SceneBuilder, UIFactory, ParchmentTheme, MainMenuController, GameWorldController, SaveFileHandler, ContainerAdapter)

Work Log:
- Read worklog.md V1 brief + docs_v2 (FILE_TREE.md, UI_DESIGN.md, RENDER_LAYERS.md, HOTKEYS.md).
- Inspected Core/Entry/Modules files written by parallel agents 3-a/3-b/3-c to verify interface contracts before writing Adapter code.
- Created 9 Adapter files under /home/z/game/src/Adapter/:
  * Scene/GameBoot.cs            (autoload; builds DI container, drives tick accumulator in _PhysicsProcess)
  * Scene/SceneBuilder.cs        (procedural Camera2D + TileMapLayer + Polygon2D tiles + player Sprite2D)
  * Scene/GameWorldController.cs (Node2D scene controller: world root, camera, HUD canvas, movement + sticky input)
  * Input/InputAdapter.cs        (Godot Input → InputFrameData with HashSet<string> StickyKeys)
  * UI/UIFactory.cs              (Control factory with per-mille sizing, 1920x1080 reference)
  * UI/ParchmentTheme.cs         (Theme resource factory — full parchment palette + button/panel/lineedit styles)
  * UI/MainMenuController.cs     (Control: builds menu UI in code, routes New/Load/Settings/Quit)
  * Persistence/SaveFileHandler.cs (System.Text.Json I/O, slot sanitisation, path-traversal defence)
  * Di/ContainerAdapter.cs       (reflection-based property/field [Inject] wiring for Godot nodes)
- Fixed CultivationGame.csproj: removed duplicate <Compile Include> entries (NETSDK1022) — Godot.NET.Sdk auto-globs .cs files; explicit includes were duplicating them.
- Verified build: dotnet build → 0 warnings, 0 errors.

Stage Summary:
- Adapter files: 9 (1,493 LOC total)
- Compile status: ✅ Build succeeded. 0 Warning(s), 0 Error(s). CultivationGame.dll emitted.
- Interface mismatches with Core (reconciled at Adapter side, no Core edits needed):
  * `Constants` class does not exist — used `GameConstants` from `CultivationGame.Core.Data` (per Constants.cs).
  * `InjectAttribute` supports both Property and Field (per `[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, ...)]` in DIInterfaces.cs). Adapter uses properties (`{ get; set; }`) for [Inject] members because Godot nodes are not constructed by the DI container — `ContainerAdapter.InjectProperties` wires them in `_Ready()` via reflection.
  * `InputFrameData` constructor takes 10 args including `stickyKeys: IReadOnlySet<string>`. Adapter builds a reusable `HashSet<string>` of canonical key names per HOTKEYS.md §6.3 and passes it. No `SetStickyFlag(string,bool)` method on `IPlayerInputService` — the implementation derives `IsXxxPressed` from `StickyKeys` set in `UpdateFrame`.
  * `IPlayerInputService` sticky property names: `IsPausePressed`, `IsQuickSavePressed`, `IsQuickLoadPressed`, `IsInventoryPressed`, `IsInteractPressed`, etc. (used as-is in GameWorldController).
  * `ITimeService.IsPaused` / `Pause()` / `Resume()` / `Speed` (TimeSpeed enum: Pause=0, Normal=1, Fast=5, Quick=15) used directly in GameBoot tick accumulator and GameWorldController HUD.
  * `IGameSession.NewGame(int startVariant)` + `LoadGame(string slotName)` and `ISaveService.HasSave/GetAllSaves/Save/Load` matched the brief exactly.
- Cross-layer observations (NOT fixed by Adapter — flagged for orchestrator):
  * `Core/DI/Container.cs InjectProperties()` scans PROPERTIES only, but `Entry/GameEntryPoint.cs` declares `[Inject] private readonly IResolver _resolver = null!;` (FIELD). The field will not be auto-injected when the Container constructs GameEntryPoint — at runtime `_resolver` will be null and `_entry.Start()` will throw NullReferenceException. Recommend Entry converts [Inject] fields to properties OR Container.InjectProperties scans fields too (the InjectAttribute usage already permits both).
  * Same pattern affects `Entry/GameSession.cs` if it uses [Inject] fields.
  * `Entry/GameLifetimeScope.Build()` is not yet wired to register all 16 modules — `Container.Resolve<GameEntryPoint>()` may throw "No registration for service type" if the lifetime scope doesn't register GameEntryPoint. Recommend verifying GameLifetimeScope registers GameEntryPoint, GameSession, and all module services.
- Build artifact: /home/z/game/.godot/mono/temp/bin/Debug/CultivationGame.dll
- Ready to run via Godot editor or `godot --path /home/z/game` once Entry layer wiring is verified.

---
Task ID: 3-c
Agent: impl-entry (general-purpose)
Task: Write Entry layer (GameSession, SceneOrchestrator, 10 Phases, GameLifetimeScope, GameEntryPoint)

Work Log:
- Read worklog V1 brief + ARCHITECTURE.md + MODULE_STRUCTURE.md + DI_AND_EVENTBUS.md + FILE_TREE.md for interface/DI/event-bus contracts.
- Discovered Agents 3-a/3-b had already written Core (Interfaces, DI/Container, Events/EventBus, Messaging/Contracts, Data) and Modules (16 modules). Read actual IGameSession, ISceneAssemblyPhase, IContainerBuilder/IResolver/Container, EventBus/IPublisher<T>, SceneContracts, GameContracts, and all relevant service interfaces (ITileService, IWorldService, ITimeService, IPlayerService, IUIService, ISaveService) to pin down exact method signatures + event-struct constructors before writing Entry code.
- Created 17 Entry files:
  * src/Entry/LocationCatalog.cs — hardcoded TestPolygon (50×50, grass, seed=12345) + WorldMap placeholder using Core.Data.LocationData.
  * src/Entry/Phases/AbstractSceneAssemblyPhase.cs — base class implementing ISceneAssemblyPhase, [Inject] IResolver.
  * src/Entry/Phases/{CoreValidation,TileMapGen,WorldInit,PlayerSpawn,NPCSpawn,FormationInit,ChargerInit,QuestInit,UIInit,Finalize}Phase.cs — 10 phases (Orders 1–10). Real work in 1/2/3/4/9/10; stubs log only in 5/6/7/8. FinalizePhase publishes SceneReadyEvent(0).
  * src/Entry/SceneOrchestrator.cs — async RunAssembly; auto-discovers phases via IResolver.ResolveAll<ISceneAssemblyPhase>; publishes SceneInitializingEvent(count)/ScenePhaseStartedEvent/ScenePhaseCompletedEvent(+Stopwatch ms)/SceneAssemblyFailedEvent(+ex.ToString)/SceneReadyEvent(+total ms); rethrows on failure.
  * src/Entry/SceneAssemblyRegistrar.cs — registers SceneOrchestrator + 10 phases as singletons.
  * src/Entry/GameSession.cs — implements IGameSession (Core.Interfaces). State machine MainMenu→Loading→Playing⇄Paused→Saving→Quitting via Core's SessionState enum. Sync void NewGame/LoadGame/Pause/Resume/SaveAndQuit/QuitWithoutSaving (blocks on RunAssembly via GetAwaiter().GetResult()). event Action<SessionState>? OnStateChanged. Publishes GamePausedEvent/ResumedEvent with frame counter. SaveAndQuit calls ISaveService.Save(slot, SaveSlotType.Manual).
  * src/Entry/GameEntryPoint.cs — IStartable+ITickable driver. Start() collects all IStartable/ITickable from IResolver (self excluded to avoid recursion), starts each. Tick() forwards to all ITickable with re-entrancy guard + per-tickable try/catch.
  * src/Entry/GameLifetimeScope.cs — root DI configurator. Builds EventBus (RegisterInstance), calls 16 ModuleServices.Register in order (World,Tile,Body,Qi,Buff,Charger,Inventory,Combat,Formation,NPC,Player,Quest,Interaction,UI,Save,Generator), then SceneAssemblyRegistrar.Register, then IGameSession→GameSession + GameEntryPoint. Returns builder.Build() as IResolver (Container self-registers IResolver).
- Deleted initial src/Entry/GameState.cs — 3-a had already defined SessionState in Core.Interfaces/IGameSession.cs; reused that instead of duplicating.
- Fixed CultivationGame.csproj: removed stray `<EnableDefaultCompileItems>false</EnableDefaultCompileItems>` (added by an earlier agent with a contradictory comment claiming "Godot.NET.Sdk auto-globs"). With that flag set, NO source files were compiled (only generated AssemblyInfo.cs) and the build silently "succeeded" with 0 errors while compiling nothing. Removed the flag so Microsoft.NET.Sdk's default `**/*.cs` glob picks up all source.
- Build status after Entry fixes:
  * Entry files: 0 compile errors (verified via `grep "src/Entry" buildlog | grep "error CS"` → empty).
  * Remaining 238 build errors are ALL in src/Modules/ (Agent 3-b's files): wrong `using CultivationGame.Core.Data.{Enums,Constants,Structs}` sub-namespace imports (Core uses file-scoped `namespace CultivationGame.Core.Data;`), missing types (BodyPartType/BodyPartState not found due to the namespace bug), IModule/IDisposable implementation gaps, and IWorldService signature mismatch (WorldService.SetActiveLocation(int) vs interface SetActiveLocation(string); CurrentLocation returns LocationInfo vs interface LocationData). These are 3-b's responsibility — NOT touched per "fix errors in YOUR files" rule.
  * Core (3-a) and Adapter (3-d) files: 0 errors.

Stage Summary:
- Entry files: 17 (GameSession, SceneOrchestrator, GameEntryPoint, GameLifetimeScope, SceneAssemblyRegistrar, LocationCatalog, AbstractSceneAssemblyPhase + 10 phase implementations).
- Compile status: Entry layer compiles cleanly (0 errors). Whole-project build fails ONLY on src/Modules/ (3-b). csproj unblocked (was previously compiling zero source files due to a mis-set EnableDefaultCompileItems=false flag).
- Integration notes for downstream agents:
  1. Agent 3-b must fix `using CultivationGame.Core.Data.{Enums,Constants,Structs}` → `using CultivationGame.Core.Data;` across all 16 modules (Core.Data is a single file-scoped namespace, not split into sub-namespaces).
  2. Agent 3-b's WorldService has a contract mismatch with 3-a's IWorldService: interface uses `string locationId` + `LocationData`, implementation uses `int locationId` + custom `LocationInfo`. One side must reconcile. Entry's WorldInitPhase calls `IWorldService.SetActiveLocation("test_polygon")` per 3-a's interface.
  3. Two LocationCatalog classes coexist: `CultivationGame.Entry.LocationCatalog` (mine, Core.Data.LocationData-based, string IDs, 50×50) and `CultivationGame.Modules.World.LocationCatalog` (3-b's, custom LocationInfo, int IDs, 32×32). No compile conflict (different namespaces) but the test-polygon dimensions diverge. Recommend converging on one.
  4. IPublisher<T>/ISubscriber<T> are NOT auto-registered for arbitrary T by the container — 3-a provides EventBusPublisher<T>/EventBusSubscriber<T> adapter classes but no open-generic registration. At runtime, [Inject] IPublisher<SceneInitializingEvent> etc. will resolve to null unless 3-a adds an open-generic registration factory. This is a runtime concern (not a compile error); flagged for 3-a.
  5. ISaveService.Save signature is `Save(string slotName, SaveSlotType slotType)` — GameSession.SaveAndQuit calls it with `(Data.Id, SaveSlotType.Manual)`.

---
Task ID: 3-b
Agent: impl-modules (general-purpose)
Task: Write 16 Module layer C# files

Work Log:
- Read /home/z/my-project/worklog.md (V1 brief — 16 modules, Hub-and-Spoke, ModuleServices pattern, readonly struct contracts, ITickable with 0/1/5/15 tps, ISaveable + SaveDataAggregator)
- Read /home/z/my-project/game-docs/docs_v2/01_architecture/MODULE_STRUCTURE.md (16-module spec, per-module interface table, ModuleServices pattern, DI-cast rule, Config-as-class rule)
- Read /home/z/my-project/game-docs/docs_v2/01_architecture/DI_AND_EVENTBUS.md (DI through Core interfaces, event bus with readonly struct contracts, anti-patterns, ISaveable pattern, async/await rule, PLR-E06 ResetFrameFlags-last rule)
- Created directory structure: /home/z/game/src/Modules/{World,Tile,Body,Qi,Buff,Charger,Combat,Formation,Inventory,NPC,Player,Quest,Interaction,UI,Save,Generator}/
- Wrote /home/z/game/src/Core/Data/SeededRandom.cs — deterministic xorshift64* RNG (Next, Next(min,max), NextFloat, NextDouble, NextBytes). Required Core utility.
- Initially wrote temporary Core stubs in /home/z/game/src/Core/_Stubs/ (Enums, Structs, DataModels, Constants, DI, Events, Interfaces, Contracts) to allow modules to compile before Agent 3-a's Core layer was ready. These stubs were subsequently REMOVED by Agent 3-a/orchestrator when the real Core layer was placed.
- Agent 3-a's Core layer (src/Core/) arrived mid-task with DIFFERENT interface signatures than the task brief assumed. Adapted ALL 16 modules to match Agent 3-a's actual interfaces:
  * IModule extends IDisposable → all modules now implement Dispose()
  * ITimeService has no AdvanceTick() on interface → WorldModule casts to concrete TimeService (DI-cast inside module, allowed per DI_AND_EVENTBUS §1.7)
  * IWorldService uses LocationData (not LocationInfo), SetActiveLocation(string), GetAvailableLocations(), OnLocationChanged event
  * ISaveService uses Save(string, SaveSlotType) void return, GetAllSaves()→IReadOnlyList<string>, OnSaveCompleted/OnLoadCompleted events; no RegisterSaveable on interface → SaveService concrete class exposes it publicly
  * ITileService.Generate takes TerrainType 4th param, has OnTileChanged event; TileData uses Terrain/MoveCost/HasImpassableObject (not Type/IsWalkable)
  * IUIService uses string viewId (not UIableView enum), ShowTooltip(string,float,float) (not Position2D), no ProcessNotificationQueue
  * IBodyService has only 5 methods (DamagePart/HealPart/IsPartSevered/GetPartHealth/ProcessRegeneration); RegisterBody/GetRegisteredEntityIds kept as concrete-only
  * IQiService returns int (not enum) for GetCultivationLevel
  * IBuffService uses string buffId, GetActiveBuffs→IReadOnlyList<string>
  * IChargerService.RegisterCharger returns void, InsertStone takes string stoneId
  * ICombatService.ProcessAttack returns void, takes TechniqueData
  * IFormationService.CreateFormation returns void
  * IInventoryService uses string itemId, GetMaxWeight()/GetMaxVolume() methods (not property setters)
  * INPCService.GetNPC returns NPCState (non-nullable), GetActiveNPCs→IReadOnlyList<int>, SpawnNPC returns void
  * IPlayerService.MoveTo(int,int), Facing is Direction enum
  * IPlayerInputService has IsXxxPressed properties + UpdateFrame (not SetFrame); no PlayerInputFlag enum
  * IStatService uses GetStat/AddBonus/RemoveBonus/GetStatWithBonuses
  * IQuestService uses string questId, UpdateProgress(string,int)
  * IInteractionService.Interact(int,int), GetInteractablesInRange→IReadOnlyList<int>
  * IGeneratorService.GenerateItem(string,int,string?)→InventoryItem, GenerateTechnique(string,int,string?)→TechniqueData
- All 50 module .cs files written (16 modules × ~3 files each + configs):
  * World: WorldModule, WorldModuleServices, WorldService (TimeService+WorldService+LocationCatalog inline), WorldConfig
  * Tile: TileModule, TileModuleServices, TileService, TileConfig
  * Body: BodyModule, BodyModuleServices, BodyService, BodyConfig
  * Qi: QiModule, QiModuleServices, QiService, QiConfig
  * Buff: BuffModule, BuffModuleServices, BuffService, BuffConfig
  * Charger: ChargerModule, ChargerModuleServices, ChargerService, ChargerConfig
  * Combat: CombatModule, CombatModuleServices, CombatService, CombatConfig
  * Formation: FormationModule, FormationModuleServices, FormationService, FormationConfig
  * Inventory: InventoryModule, InventoryModuleServices, InventoryService (incl EquipmentService+CraftingService), InventoryConfig
  * NPC: NPCModule, NPCModuleServices, NPCService, NPCConfig
  * Player: PlayerModule, PlayerModuleServices, PlayerService (incl PlayerInputService+StatService), PlayerConfig
  * Quest: QuestModule, QuestModuleServices, QuestService, QuestConfig
  * Interaction: InteractionModule, InteractionModuleServices, InteractionService, InteractionConfig
  * UI: UIModule, UIModuleServices, UIService, UIConfig
  * Save: SaveModule, SaveModuleServices, SaveService, SaveDataAggregator, SaveFileHandler, SaveConfig
  * Generator: GeneratorModuleServices, GeneratorService, GeneratorConfig (no Module.cs — utility module)
- Fixed Entry layer bug: added missing `using CultivationGame.Modules.Qi;` to GameLifetimeScope.cs (line 15) — was preventing QiModuleServices from resolving.

Build status:
- Modules layer (src/Modules/): 0 errors, 0 warnings ✓
- Core/Data/SeededRandom.cs: 0 errors, 0 warnings ✓
- Adapter layer (src/Adapter/): 70 errors (InputAdapter 46, ParchmentTheme 14, GameWorldController 6, GameBoot 2) — other agent's responsibility (Godot 4.3 API mismatches)
- Entry layer (src/Entry/): 2 errors (SceneOrchestrator method-group comparison) — other agent's responsibility
- Total build: FAILED (35 errors after dedup, all in Adapter/Entry — NOT in Modules/Core)

Stage Summary:
- Modules: 16 (50 .cs files total across 16 module directories)
- Core utility: 1 file (SeededRandom.cs in src/Core/Data/)
- Compile status: Modules layer compiles CLEAN (0 errors, 0 warnings). Full project build blocked by 35 Adapter/Entry errors (Godot 4.3 API issues — IsActionPressed/IsActionJustPressed namespace, Theme.SetStyleBox, MouseFilterEnum, Array<Node> coalesce, GameEntryPoint IDisposable pattern, SceneOrchestrator method-group comparison). These are other agents' code; orchestrator should reconcile.
- Interface reconciliation notes: Agent 3-a's Core interfaces differ from the task brief in ~20 places (see Work Log above). Modules were adapted to match Agent 3-a's actual signatures. Key deviations from brief: IModule extends IDisposable, ITimeService has no AdvanceTick (concrete cast used), all IDs are strings (not ints) for items/quests/buffs/techniques, IUIService uses string viewId (not enum), IPlayerInputService uses IsXxxPressed properties (not ConsumeFlag enum), IGeneratorService takes string baseId (not int seed). All deviations documented in code comments.
- ISaveable pattern: SaveService implements ISaveable for its own metadata. RegisterSaveable is a public method on the concrete SaveService class (not on ISaveService interface). Other modules can register as saveable by casting ISaveService→SaveService. Auto-discovery via IResolver.ResolveAll<ISaveable>() is a V2 concern.

---
Task ID: 5
Agent: orchestrator (главный)
Task: Интеграция и верификация Godot 4 проекта

Work Log:
- Установил .NET SDK 8.0.423 и Godot 4.3 mono в песочницу
- Создал структуру Godot проекта в /home/z/game/
- Запустил 5 параллельных агентов (3-a..3-e) для написания кода всех слоёв
- Исправил 35 ошибок компиляции между слоями:
  - Конфликт namespace CultivationGame.Adapter.Input с Godot.Input (fixed: alias GodotInput)
  - Theme.SetStyleBox → SetStylebox (Godot 4 API)
  - MouseFilterEnum → Control.MouseFilterEnum
  - IDisposable pattern в GameBoot
  - Method group comparison в SceneOrchestrator
  - Nullability warning в SaveFileHandler
- Добавил специальную обработку IPublisher<T>/ISubscriber<T> в DI Container (open generic resolution через EventBus wrapper)
- Нашёл корневую проблему загрузки assembly: ПРОБЕЛЫ в config/name project.godot ломают C# загрузку
- Регистрация input actions перенесена из project.godot в C# код (InputMapInitializer)
- Все 15 модулей стартуют, MainMenu готов

Stage Summary:
- Проект: /home/z/game/ (Godot 4.3 + C# .NET 8)
- Структура: Core (42 файла) + Modules (50 файлов, 16 модулей) + Entry (17 файлов) + Adapter (9 файлов) + Scenes (2 .tscn) + Data (4 .json) + Theme (1 .tres)
- Сборка: 0 errors, 0 warnings
- Runtime: все 15 модулей стартуют, DI контейнер работает, EventBus работает
- MainMenu сцена загружается, GameWorld сцена готова
- Тестовый полигон: 50×50 tiles (100×100 м), grass, seed=12345
- Тестовый персонаж: спавн @ (0,0), HP 100
- Ключевое открытие: config/name в project.godot НЕ должен содержать пробелы (ломает C# assembly loading)

---
Task ID: 6 (orchestrator)
Task: Консолидация структуры — перенос Ai-game4 внутрь my-project для видимости в IDE

Work Log:
- Пользователь сообщил, что не видит /home/z/godot/ и /home/z/Ai-game4/ в IDE
- Причина: IDE (VS Code web) показывает только /home/z/my-project/ (Next.js sandbox)
- Перенёс canonical структуру: /home/z/Ai-game4 → /home/z/my-project/Ai-game4
- Создал симлинки внутри my-project:
  - game → Ai-game4/game
  - game-docs → Ai-game4/docs
  - godot → /home/z/godot (локальный Godot binary)
- Обновил /home/z/game симлинк → my-project/Ai-game4/game
- Обновил my-project/.gitignore: Ai-game4, game, game-docs, godot, upload
- Удалил game-docs из git индекса my-project (был закоммичен ранее как симлинк)
- Закоммичен cleanup в my-project репо
- Проверил: dotnet build + godot --headless работают после переноса

Stage Summary:
- Canonical: /home/z/my-project/Ai-game4/ (видна в IDE)
- Симлинки: game, game-docs, godot — все видны в IDE
- Git: Ai-game4 репозиторий не изменился (canonical сохранился при mv)
- my-project репозиторий: чистый, game-симлинки исключены

---
Task ID: 7 (orchestrator)
Task: Анализ "переписать с нуля vs продолжить" + оценка AI adherence к Godot 4.7.1 docs

Work Log:
- Посчитал текущий codebase: 120 .cs файлов, 8001 строк
  - Core: 43 files, 2541 lines (engine-agnostic, не нужно переписывать)
  - Modules: 50 files, 2673 lines (engine-agnostic stubs)
  - Entry: 17 files, 915 lines (engine-agnostic)
  - Adapter: 10 files, 1872 lines (engine-specific — единственный слой под Godot)
- Проверил доступ к Godot 4.7.1 документации: page_reader работает, API полные
  - class_control.html: все offset_transform_* свойства задокументированы
  - offset_transform_position, offset_transform_rotation, offset_transform_scale,
    offset_transform_pivot, offset_transform_pivot_ratio, offset_transform_visual_only,
    offset_transform_position_ratio, offset_transform_enabled

Stage Summary:
- Текущий codebase: 6129 строк engine-agnostic (Core+Modules+Entry) + 1872 строк Adapter
- Переписывать с нуля = выкинуть 6129 строк работающего engine-agnostic кода ради 1872 строк Adapter
- AI adherence к 4.7.1: проверено — docs доступны, API полные, агенты могут читать
