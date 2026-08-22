# Чекпоинт: NPC_COMBAT_PREP реализация (локальный агент ZCode)

**Дата:** 2026-08-22
**Автор:** локальный ZCode (GLM-5.3), Windows, Godot 4.7.1 mono
**Источник плана:** docs/docs_v2/09_workflow/NPC_COMBAT_PREP.md

---

## FIX-1: Реакция на изменение скорости времени (коммит 11e495d)

**Проблема:** после интеграции тиковой системы (Q7) смена скорости PageUp/PageDown не давала реакции — контракт `TimeSpeedChangedEvent` существовал, но никто его не публиковал; лог смены скорости был закрыт `#if DEBUG_SPEED_LOG`.

**Решение:**
- `TimeService` (WorldService.cs): Speed теперь свойство с сеттером → публикует `TimeSpeedChangedEvent` через EventBus при каждом изменении (вкл. Pause/Resume).
- `WorldModule.Tick`: публикует `TimeChangedEvent` каждый тик + `DayChangedEvent`/`MonthChangedEvent`/`YearChangedEvent` при смене календарной даты (по MODULE_STRUCTURE §WorldContracts).
- `GameWorldController`: toast «⏩ Скорость: …» при PageUp/PageDown (видимая реакция), helper `SpeedLabel()`.

**Проверка:** dotnet build 0 ошибок; headless-запуск чистый.

## Phase 1: NPC Spawn + Render (коммит d7837e8)

**Новые файлы:**
- `Entry/Phases/HumanNPCSpawnPhase.cs` (PhaseOrder 6) — спавнит 4 людей (Merchant lvl1, Cultivator lvl3, Guard lvl2, Passerby lvl0) через полный пайплайн `NPCSpawnerService.SpawnNPC("human", role, level, pos, seed)`. Детерминированные seed (loc.Seed + 104729 + role), поиск walkable-тайлов с мин. дистанцией 5 от центра.
- `Adapter/Scene/NPCSpriteRenderer.cs` — цветные круги по роли (merchant=teal, cultivator=violet, guard=blue, elder=gold, monster=red), ZIndex=Objects(3), по образцу AnimalSpriteRenderer.

**Изменено:**
- `SceneAssemblyRegistrar`: регистрация HumanNPCSpawnPhase.
- `SceneBuilder`: `SetupNPCs()` → NPCSpriteRenderer в world root.
- `MainMenuController`: тестовый хук `GODOT_NEWGAME=1` — автостарт новой игры для headless-проверки полного флоу сборки (в семействе GODOT_MAP_SIZE / GODOT_GEN_DEBUG).

**Проверка (headless, `GODOT_NEWGAME=1 scenes/MainMenu.tscn`):**
```
[HumanNPCSpawn] Spawned Merchant #npc_bb2c1c6d11544fce at (2, 34)
[HumanNPCSpawn] Spawned Cultivator #npc_33dcc2c369c247b6 at (30, 35)
[HumanNPCSpawn] Spawned Guard #npc_4f9dd6aee6294a1c at (43, 48)
[HumanNPCSpawn] Spawned Passerby #npc_548eaec877764a3f at (13, 48)
[Phase 6] HumanNPCSpawn complete — 4/4 NPCs on 'test_polygon'
[GameSession] NewGame ready — state=Playing
```

**Заметки:**
- NPCData.Position — в тайлах (int), НЕ в милли-тайлах; NPCData.Role доступен напрямую.
- Прямой запуск GameWorld.tscn НЕ вызывает scene-assembly фазы (NewGame вызывается только из MainMenu) — тестировать полный флоу через GODOT_NEWGAME=1 + MainMenu.tscn.
- NPCVisualService (Modules) остаётся no-op стабом — рендер в Adapter-слое (правильно по архитектуре).

## Следующие шаги

- Phase 2: DialogueWindow UI + E-key взаимодействие с NPC (backend DialogueService готов, 398 LOC).
- Phase 6: Combat Activation (PlayerCombatAdapter full + target selection + 5 TODO экипировки).
- Phase 3-5: Faction port, Trade.
