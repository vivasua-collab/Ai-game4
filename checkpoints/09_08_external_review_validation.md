# Валидация внешнего ревью (ChatGPT) — этап 1 → исправления

**Дата:** 2026-09-08
**Вход:** результаты внешнего ревью кода (ChatGPT, статический анализ, без сборки)
**Метод:** каждая находка проверена по коду лично (валидация реальности), затем исправлена/отклонена.

---

## Вердикты по находкам

| # | Находка ревью | Вердикт | Обоснование |
|---|--------------|---------|-------------|
| P1-1 | PreGenTechniquePhase: грейды не передаются в генератор + реестр засоряется | ✅ **Подтверждён (критический)** | `GenerateSpecified(type, level, level, seed)` не принимал грейд — `DetermineGrade(rng)` выбирал его случайно (Cultivation всегда Common). Оба метода генератора регистрировали в TechniqueRegistry ДО FilterValid/дедупа → невалидные и дубли оставались в реестре. |
| P1-2 | quicksave: MainMenu проверяет QuickSave, а LoadGame читает Manual слот | ❌ **Ложное срабатывание (runtime), но API-несогласованность реальна** | SaveService.Load/HasSave маршрутизирует ТОЛЬКО по `slot.Name` → оба случая читают один файл `saves/quicksave.json`. Загрузка найденного сейва работает. НО: `SaveSlot.FileName` — мёртвое свойство (0 использований) с ложной семантикой «тип задаёт файл» — именно оно сбило ревьюера. |
| P1-3 | Контракт pipeline фаз не работает: CanExecute/State/SkipOnLoad игнорируются, skipped всегда 0 | ✅ **Подтверждён (контрактный)** | `RunAssembly` безусловно звал `ExecuteAsync()`; CanExecute не вызывался; State никогда не становился Completed; MarkAsSkipped не вызывался; `SceneReadyEvent(count, 0, ms)` — skipped всегда 0. Фазы — DI-синглтоны: без Reset() контракт CanExecute (true только для Pending) сломал бы повторную сборку. |
| P1-4 | Утечка подписок EventBus при уходе со сцены | ✅ **Подтверждён (реальный)** | `_Ready()` создаёт 11 подписок + 1 lazy (`_attackRejectedToken`); `_ExitTree()` диспозил только 2 торговых токена. EventBus процесс-глобальный (GameBoot-контейнер) → callbacks уничтоженного Godot-узла. |
| P2-1 | Коллизия PhaseOrder=5 (AnimalSpawn/StartingGear) — порядок решает обход Dictionary | ✅ **Подтверждён** | Оба = 5; ResolveAll обходит `_registrations.Values` словаря; «спасует stable sort» — миф (стабилизируется уже полученный порядок обхода словаря). |
| P2-2 | Мёртвый метод `ComparePhaseOrder` в SceneOrchestrator | ✅ **Подтверждён** | Сортировка через OrderBy напрямую; метод не вызывается. |
| P2-3 | `ISceneAssemblyLogger` — легаси-контракт (UnityEngine.Debug.Log, 0 реализаций) | ✅ **Подтверждён** | 0 реализаций, 0 потребителей, коммент «UnityEngine.Debug.Log» — наследие Unity-итерации. Правило пользователя: легаси не держим → удалён. |

Плюс из раздела «Что не включено»: ChargerInit/FormationInit/QuestInit — подтверждены как явные stubs (заглушки будущих реализаций — СОХРАНЕНЫ по решению пользователя от 2026-09-06).

---

## Внесённые исправления

### P1-1 — генерация техник (грейды + чистый реестр)
- `ITechniqueGeneratorService`/`TechniqueGeneratorService`: новый **`BuildSpecified(type, grade, level, cultivationLevel, seed)`** — ЯВНЫЙ грейд, БЕЗ авто-регистрации (для пайплайнов с валидацией). Рефакторинг: общее ядро `BuildCore`; `GenerateSpecified` выбирает грейд из отдельного детерминированного потока (`seed ^ 0x5A5A5A5A`) и регистрирует (семантика «generate = создать + зарегистрировать» для NPC/выдачи).
- `PreGenTechniquePhase`: использует `BuildSpecified` → FilterValid → дедуп → регистрация ТОЛЬКО валидных/уникальных. Добавлены метрики: `grades registered: [...]`, `gradeMismatches`, `registryInvalid` (integrity-check реестра против СОБСТВЕННОГО уровня техники — резонанс «уровень ученика» остаётся заботой LearnTechnique).
- `WorldInitPhase` (фаза 3): world-scoped `TechniqueRegistry.Clear()` — до спавна NPC (7) и PreGen (13); при повторной сборке мира реестр не тащит stale-техники прошлого мира.

### P1-2 — санитария слотов (гигиена контракта, бага не было)
- `IGameSession`/`GameSession`: **`LoadGame(SaveSlot slot)`** — UI передаёт тот же слот, что проверял в HasSave (тип QuickSave совпадает); `LoadGame(string)` сохранён как обёртка (Manual).
- `MainMenuController`: `LoadGame(new SaveSlot("quicksave", SaveSlotType.QuickSave))`.
- **Удалён мёртвый `SaveSlot.FileName`** (0 использований; ложная семантика маршрутизации по типу — реальная маршрутизация по Name).

### P1-3 — lifecycle оркестратора (контракт работает)
- `ISceneAssemblyPhase`: + `MarkAsRunning()`/`MarkAsCompleted()`/`MarkAsFailed(string)`; enum **`SceneAssemblyMode`** (NewGame/LoadGame). XML: lifecycle — ответственность оркестратора.
- `SceneOrchestrator.RunAssembly(ct, mode)`: **Reset() всех фаз перед прогоном** (DI-синглтоны!), SkipOnLoad-пропуск в Load-режиме (MarkAsSkipped + skipped++), гейт CanExecute, Running→Completed/Failed переходы, `SceneReadyEvent(completed, skipped, ms)` с реальными числами (было всегда 0). Удалён мёртвый `ComparePhaseOrder` (P2-2).
- `GameSession`: NewGame → `RunAssembly(NewGame)`; LoadGame → `RunAssembly(LoadGame)`.
- SkipOnLoad=false оверрайды (wiring-фазы): CoreValidation(1), FormationInit(9), ChargerInit(10), QuestInit(11), UIInit(12), Finalize(15).

### P1-4 — утечка подписок
- `GameWorldController._ExitTree()` → `DisposeSubscriptionTokens()`: полный диспоз ВСЕХ 12 токенов (11 из _Ready + lazy `_attackRejectedToken`) с обнулением.

### P2-1 — уникальная нумерация фаз
- AnimalSpawn 5→6, HumanNPCSpawn 6→7, GroupSpawn 7→8, FormationInit 8→9, ChargerInit 9→10, QuestInit 10→11, UIInit 11→12, PreGen 12→13, TechniqueGrant 13→14, Finalize 14→15. Порядки 1..15 уникальны; порядок словаря DI больше не участвует в семантике.

### QA-инфраструктура
- Новый хук **`GODOT_REASSEMBLY_DEBUG=1`** (ReAssemblySimDebug): повторная сборка в одном процессе — проверяет Reset фаз (15/15 Completed, 0 Skipped) и world-scoped реестр (не ×2). Хуков 19→20.

---

## Верификация

- `dotnet build` — **0 errors** (300 warnings, без прироста).
- Регрессия 12/12 PASS: COMBAT (melee+ranged+LOS/ammo), CHARGE, TOAST, LOWHP, KILLFEED, HOTBAR, DAMAGEDIR, DIALOGUE, TRADEUX, TRADE smoke (buy/sell True/True), GEN (0 C#-исключений), **REASSEMBLY PASS** (новый).
- PreGen-контроль: `grades registered: [Common=28, Refined=27/28, Perfect=28, Transcendent=10]`, **gradeMismatches=0**, **registryInvalid=0** (до фикса грейды были случайными, реестр содержал невалидные).
- Оркестратор: `Assembly complete — mode=NewGame: 15 completed, 0 skipped`.

## Наблюдения (не блокеры, для будущих этапов)

1. **NPC-спавн без очистки при повторной сборке**: HumanNPCSpawnPhase не чистит старых NPC (AnimalSpawnPhase чистит через `ClearAnimals()`). В игре путь «возврат в меню» пока не существует (ChangeSceneToFile(MainMenu) нет) — при его реализации нужен clear-паттерн как у животных. QA REASSEMBLY это видит (реестр 103→142, рост от повторного NPC-спавна, не от дублей реестра).
2. `SceneAssemblyCompletedWithErrorsEvent` — контракт «мягкого сбоя» (0 публикаторов/подписчиков) — заглушка будущей политики, оставлена по решению пользователя (заглушки ≠ легаси).
3. GenerateSpecified для выдачи игроку регистрирует каждую ПОПЫТКУ (в т.ч. неудачные для LearnTechnique) — рост реестра на ~9 записей за игру; невалидных среди них нет (проверено integrity-check), заметка на будущее при чистке реестра выдачи.

## Документация синхронизирована

- `ARCHITECTURE.md` §6: таблица фаз 1..15 + SkipOnLoad-колонка; §6.2 реальный интерфейс (Mark*/Reset/Mode); пометка об удалении ISceneAssemblyLogger; §6.4 LoadGame(SaveSlot).
- `PRE_GENERATION.md`: PhaseOrder 44→13, BuildSpecified в алгоритме, новые метрики логирования.
- `TESTING_RULES.md` §0.1: хук GODOT_REASSEMBLY_DEBUG (19→20).
