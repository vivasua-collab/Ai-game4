# АУДИТ-1: Архитектура (Core / DI / Events / Entry / границы слоёв)

**Дата:** 2026-08-26, 14:15–15:00 MSK
**Проход:** 1 из 3 (минимум), основной поток ИИ
**HEAD на старте:** `f0d11a6`
**Scope:** Core (DI, Events, Messaging, Interfaces, Data), Entry (фазы, boot,
orchestrator, lifetime scope), Adapter-граница (структурно), межмодульные
зависимости (заявленные инварианты).

---

## Сводка

- **Файлов проверено:** ~30 (полностью: Container, EventBus, GameEntryPoint,
  GameLifetimeScope, SceneOrchestrator, SceneAssemblyRegistrar,
  все 16 фаз — порядки/логика, FinalizePhase; выборочно: интерфейсы,
  контракты, нарушения границ по grep).
- **Находок:** 6 (1 critical-баг, 1 арх-нарушение, 1 major-риск, 3 minor).
- **Исправлено в этом проходе:** 4 фикса (A-1 полностью, A-2 полностью,
  A-3 фикс, A-6 частично).

---

## Находки

### A-1 [CRITICAL-BUG] Порядок фаз: Finalize до PreGen/TechniqueGrant + дубли порядков + нестабильная сортировка

**Симптом (из headless-лога):**
```
[Phase 10] Finalize complete — Scene assembly complete   ← «сборка завершена»
[Phase 45] TechniqueGrant complete — granted 6 techniques ← …но техники ещё выдаются
[SceneOrchestrator] Assembly complete — 14 phases
```

**Состав проблемы:**
1. FinalizePhase (Order=10) выполнялся ДО PreGenTechniquePhase (44) и
   TechniqueGrantPhase (45) — комментарий в TechniqueGrantPhase планировал
   «NPC-фазы 50+», которые в реальности зарегистрированы на 5–7.
2. FinalizePhase публиковал ВТОРОЙ SceneReadyEvent(1, 0, 0) с фальшивым
   счётчиком фаз (1) до завершения реальных фаз. Подписчиков сейчас нет,
   но ловушка для будущих (двойной fired, разные данные).
3. Дубли порядков у зарегистрированных фаз: 6 (HumanNPCSpawn +
   FormationInit), 7 (GroupSpawn + ChargerInit); NPCSpawnPhase(5) не
   зарегистрирован (заменён AnimalSpawn).
4. `List<T>.Sort` — НЕСТАБИЛЬНЫЙ: при равных Order порядок выполнения
   фаз не определён (сегодня спасают no-op stub'ы на дублях).

**Фикс (реализован):**
- Перенумерация с сохранением фактической последовательности:
  1 CoreValidation, 2 TileMapGen, 3 WorldInit, 4 PlayerSpawn, 5 AnimalSpawn,
  6 HumanNPCSpawn, 7 GroupSpawn, 8 FormationInit(было 6), 9 ChargerInit(7),
  10 QuestInit(8), 11 UIInit(9), 12 PreGenTechnique(44), 13 TechniqueGrant(45),
  14 Finalize(10) — теперь ПОСЛЕДНЯЯ.
- FinalizePhase: дубль-публикация SceneReadyEvent УДАЛЕНА (авторитетный
  публикует SceneOrchestrator после всех фаз с реальным счётчиком);
  неиспользуемые using/inject вычищены.
- SceneOrchestrator: стабильная сортировка `OrderBy(p => p.Order)` в обоих
  местах (RegisterPhase, EnsurePhasesLoaded); `_phases` больше не readonly.
- Верифицировано headless: последовательность 1→14 корректна,
  Finalize последний, valid=100/100.

### A-2 [ARCH-VIOLATION] Core→Modules рецидив: FormationData в Core-интерфейсах

**Файлы:** `Core/Interfaces/IVerificationService.cs` (реальная ссылка на
Modules.Formation.Data.FormationData), `Core/Data/LevelBoundaries.cs`
(мёртвый using).

Прецедент: аудит 08-21 C1/C2 (NPCState, BodyPart) — те переносили в Core.
Прошлая сессия (2026-08-27 Phase B/C) нечаянно воссоздала паттерн.

**Фикс (реализован):** FormationData.cs (155 строк, чистый DTO — зависимости
только System/Core/Core.Data) перенесён `git mv` в `Core/Data/` с namespace
`CultivationGame.Core.Data`. FormationRegistry остался в модуле (сервисная
инфраструктура). Обновлены using в 10 файлах; мёртвый using в LevelBoundaries
удалён; `Data.FormationData` → `FormationData` в FormationService.CurrentFormation.
**Проверка:** `grep "using CultivationGame.Modules" Core/` — пусто. Слой Core
снова не знает о модулях.

### A-3 [MAJOR-RISK] SceneAssemblyRegistrar: комментарий «registration order is purely cosmetic»

Комментарий утверждал, что порядок регистрации фаз не важен (сортирует
оркестратор). При дублях порядков + нестабильном Sort это было НЕВЕРНО.
После фикса A-1 (уникальные порядки + стабильный OrderBy) утверждение стало
истинным. Комментарий обновлён в телах фаз (каждая фаза документирует свой
порядок и историю перенумерации).

### A-4 [MINOR] EventBus: ThreadStatic re-entrancy state — общий для всех инстансов

`_publishing`/`_pendingQueue` — static [ThreadStatic] поля: при нескольких
инстансах EventBus состояние re-entrancy разделяется между ними. В игре один
EventBus (зарегистрирован в GameLifetimeScope) — не баг, но заметка для
будущего (если появятся скоупы/под-шины). Не чиним (нет сценария).

### A-5 [MINOR] DI Container: жадный конструктор без диагностики циклов

Construct выбирает жаднейший конструктор; Resolve(depth) имеет guard глубины
(защита от циклов — бросает при превышении), но сообщение об ошибке при
неудовлетворённом параметре не показывает цепочку разрешений. Приемлемо для
текущего масштаба. Не чиним в этом проходе.

### A-6 [MINOR→OK] Уникальность: NPCSpawnPhase-стаб не зарегистрирован

NPCSpawnPhase.cs (Order=5, «No NPCs in test polygon») существует, но НЕ
зарегистрирован в SceneAssemblyRegistrar — заменён AnimalSpawnPhase. Мёртвый
код. Частичный фикс: оставлен как есть (закомментирован в регистраторе ранее),
рекомендация — удалить файл в следующем подходе (модуль мира).

---

## Что проверено и чисто

- **DI Container (292 строки):** потокобезопасность (lock), singleton-кэш с
  форвард-регистрациями (interface + concrete-type ключи → один инстанс),
  дедуп Dispose по ReferenceEqualityComparer, IResolver special-case.
- **EventBus (239 строк):** zero-GC publish (in-параметры, no boxing),
  re-entrancy queue (Q13), потокобезопасная подписка.
- **GameEntryPoint:** изоляция ошибок startable/tickable (try/catch на каждый),
  re-entrancy guard Tick, порядок «модули → Entry → self».
- **GameLifetimeScope:** 17 модулей в каноническом порядке §1.2, adapter-
  override hook (configureAdapter до Build), документация circular IResolver.
- **Граница Adapter:** Core не ссылается на Godot (`grep "using Godot" Core/`
  пусто — проверено).
- **Core→Modules:** после фикса A-2 — пусто.

---

## Журнал фиксов

| Файл | Изменение |
|------|-----------|
| Entry/Phases/*.cs (7 файлов) | Перенумерация Order (уникальные 1–14) |
| Entry/Phases/FinalizePhase.cs | Order 10→14, дубль SceneReadyEvent удалён, using вычищены |
| Entry/SceneOrchestrator.cs | Стабильный OrderBy вместо List.Sort (2 места), _phases не readonly |
| Modules/Formation/Data/FormationData.cs → Core/Data/FormationData.cs | git mv + namespace Core.Data |
| 10 файлов (using) | Обновлены на Core.Data / удалены мёртвые |

**Верификация:** dotnet build 0 errors; GODOT_NEWGAME=1 — фазы 1→14 в
правильном порядке, Finalize последний, PreGen valid=100/100,
TechniqueGrant granted=6.

---

*Аудит-1 завершён. Следующий проход (аудит-2): архитектура + модуль мира
(World/Tile/NPC-спавн) — файл `2026-08-26_audit_pass2_worldgen.md`.*
