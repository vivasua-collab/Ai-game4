# Аудит 2: Modules Layer

**Дата:** 2026-08-21 13:30 UTC
**Task ID:** AUDIT-2

---

## Сводка

- **Модулей проверено:** 16 (все)
- **Файлов проверено:** ~95 из ~110
- **Проблем найдено:** 48 (critical: 4, major: 14, minor: 30)
- **Cross-module нарушений:** 8
- **Stubs/TODOs:** 22 TODO + 4 stub фазы + 1 stub сервис

---

## CRITICAL проблемы

### C1: SetConfig() никогда не вызывается
- **Проблема:** 10 модулей (Body, Qi, Combat, Inventory, Buff, Charger, Formation, NPC, Interaction, Quest) определяют `SetConfig(XxxConfig)`, но никто не вызывает
- **Результат:** BodyService/QiService/CombatService/NPCService молча пропускают `Initialize()`
- **Игрок:** нет body parts, нет Qi pool, нет combat configuration
- **Решение:** Inject `XxxConfig` через `[Inject]` в каждом модуле

### C2: PlayerCombatAdapter не зарегистрирован в DI
- **Файл:** `Modules/Player/PlayerCombatAdapter.cs` (75 строк)
- **Проблема:** `PlayerModuleServices.Register()` не регистрирует его
- **Результат:** Player attack input никогда не доходит до CombatService
- **Решение:** Register в PlayerModuleServices + Start() из PlayerModule

### C3: PlayerService обходит Body module
- **Файл:** `Modules/Player/PlayerService.cs:27`
- **Проблема:** Свой `_data = new CharacterData()` с `_data.Health = 100f`
- **Не [Inject] IBodyService**, не подписан на DamageAppliedEvent
- **Результат:** Две параллельные HP системы: BodyService._parts vs PlayerService._data.Health
- **Игрок не может умереть** от combat damage
- **Решение:** Wire PlayerService → Body module

### C4: NPCVisualService — stub
- **Файл:** `Modules/NPC/NPCVisualService.cs`
- **Проблема:** Все 3 метода (Initialize, UpdateVisualPositions, Dispose) — no-ops
- **Результат:** Даже если NPC заспавнятся, они невидимы
- **Решение:** Реализовать Godot рендеринг

---

## MAJOR проблемы (14)

| # | Проблема | Файл | Решение |
|---|----------|------|---------|
| M1 | NPCSpawnPhase — stub | Entry/Phases/NPCSpawnPhase.cs | Реализовать спавн |
| M2 | 4 stub фазы (Charger, Formation, Quest, NPC Init) | Entry/Phases/ | Реализовать или удалить |
| M3 | CombatService 5 TODOs (equipment data = 0) | Combat/CombatService.cs:300,424,428,433,444 | Wire EquipmentDataProvider |
| M4 | EquipmentDataProvider.GetEquipped всегда null | Combat/EquipmentDataProvider.cs | Использовать IItemDatabaseService |
| M5 | NPC↔Body cycle (BodyPart type leak) | AUDIT-1 C1+C2 | Перенести BodyPart в Core |
| M6 | NPC↔Generator cycle (NPCConfig shared) | Modules/NPC + Modules/Generator | Перенести weight tables в Core |
| M7 | Random.Shared в combat (non-deterministic) | 10+ файлов | Injectable SeededRandom |
| M8 | CombatAIService single-tier, не 3-tier | NPC_AI_SYSTEM.md требует 3-tier | Реализовать 3-tier |
| M9 | BodyModule.OnBuffTicked DoT — stub | Body/BodyModule.cs | Реализовать DoT damage |
| M10 | NPCModuleServices duplicate registrations | NPC/NPCModuleServices.cs | Убрать дубликаты |
| M11 | 8 missing services (Sleep, Faction, Event, HUD, ...) | docs_v2 spec | Реализовать или удалить из docs |
| M12 | TimeService.DeltaTime hardcoded 1/60 | Time/TimeService.cs | Respect pause + speed |
| M13 | 22 TODOs across modules | various | Оценить и закрыть |
| M14 | Cross-module direct dependencies (8) | various | Через EventBus |

---

## Концептуальные расхождения (требуют решения)

| # | Код | Документация | Вопрос |
|---|-----|--------------|--------|
| 1 | SetConfig не вызывается | DI_AND_EVENTBUS.md §1.7 требует wiring | Inject config через [Inject] или вызывать SetConfig из module Start()? |
| 2 | PlayerService свой HP | BODY_SYSTEM.md требует dual HP (Red/Black) | PlayerService должен делегировать BodyService? |
| 3 | NPCAIService single-tier | NPC_AI_SYSTEM.md требует 3-tier (Spinal/Neural/Brain) | Реализовать 3-tier сейчас или отложить? |
| 4 | 8 missing services | MODULE_STRUCTURE.md spec | Реализовать все или удалить из docs? |

---

## План исправлений

### P0 — Критические (блокируют игру)
1. Fix SetConfig wiring (10 модулей) — inject config
2. Register PlayerCombatAdapter + Start()
3. Wire PlayerService → Body module (IsAlive через Body)

### P1 — Корректность
4. EquipmentDataProvider → IItemDatabaseService
5. Random.Shared → SeededRandom в combat
6. Реализовать NPCSpawnPhase + 3 stub фазы
7. Break NPC↔Generator cycle
8. DoT damage routing
9. Fix TimeService.DeltaTime

### P2 — Архитектура
10. Решить AUDIT-1 C1+C2 (BodyPart/NPCState в Core)
11. Убрать duplicate registrations
12. Реализовать или удалить 8 missing services

---

## Файлы

- **Аудит:** этот файл
- **Полный отчёт:** `/home/z/my-project/worklog.md` (Task AUDIT-2)
