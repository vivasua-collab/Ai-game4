# АУДИТ: Quest

**Дата:** 2026-09-11 (аудит-сессия 09-11, субагент playerquest-audit)
**HEAD на старте:** `d0b065d`
**Скоуп (READ-ONLY, код НЕ изменялся):** Modules/Quest — все 5 файлов
модуля + Data/3 (~1.1k строк, полностью) + стыки: QuestContracts,
SaveModule (реестр ISaveable, все Register<ISaveable> по репо),
DialogueService (dialogue-quest link), NPCService.OnNPCInteracted
(RoleId), GWC (E-путь 1676-1729, окно Q 1477-1490), QuestWindow,
InventoryService/SpiritStorageService (источники ItemAddedEvent),
CombatService (EnemyKilledEvent), QiService (QiAddRequestEvent),
QuestSimDebug/SaveLoadSimDebug (QA); docs_v2: SAVE_SYSTEM.md:222,
DIALOGUE_SYSTEM, JOURNAL_SYSTEM.

## Сводка

- Прочитано 8/8 файлов; rg-трассировка подписчиков всех 7 квест-событий
  (живой только QuestCompletedEvent → QuestRewardService), вызовов
  AbandonQuest (0 внешних) / FailQuest (только внутренний expire),
  ItemAddedEvent-публикаторов (6), StartQuest-вызывателей (диалог+UI+QA).
- **Находок: 8 (P1×1, P2×3, P3×4).** Главная: квесты НЕ сохраняются —
  save/load round-trip отсутствует. Живой цикл
  диалог→принятие→прогресс→награды в сессии чист.

## Находки

### QST-1 [P1] Квесты не сохраняются: QuestService не ISaveable, round-trip отсутствует

**Файл:** `Modules/Quest/QuestModuleServices.cs:66-75` (нет
Register<ISaveable, QuestService>); QuestService.cs:28 (класс без
ISaveable); SaveModule.cs:50-56 (реестр ISaveable из DI: body/formation/
charger/npc/techniques/inventory/save_meta/technique_slots — «quests» НЕТ).
**Сценарий:** взял «Охоту на волков» (2/3) → F5 → F9 → все квесты
NotStarted, прогресс 0, _rewardedQuestIds пуст. Квест можно перевзять →
повторные награды (фарм-цикл take→complete→save→load; усиливается QI-2 —
Qi-стейт тоже не сейвится, при восстановленном NPC-стейте волков).
**Доказательство:** rg ISaveable — 8 регистраций без Quest;
SAVE_SYSTEM.md:222 обещает блок `"quests"` в сейве.
**Фикс:** QuestService : ISaveable (SaveKey="quests"): статусы всех
квестов + Progress целей + StartDay + _rewardedQuestIds (без него после
починки — двойная награда для Completed); RestoreState после
ре-Initialize; регистрация в QuestModuleServices.

### QST-2 [P2] Technique/Experience/FactionRep-награды: квест помечается выданным при НЕвыданной награде

**Файл:** `QuestRewardService.cs:89-95` (MarkRewardsGranted после цикла
безусловно) vs `:129-133` (три типа — `break` без granted, событие не
публикуется).
**Сценарий:** квест с техникой-наградой (в дефолтных нет — латентно):
Qi/Item-части выданы, техник-часть молча потеряна, квест помечен выданным
→ повторной выдачи не будет никогда. В отличие от item-валидации
(:75-87 — честный отказ БЕЗ MarkRewardsGranted) — маскировка потери.
**Фикс:** реализовать выдачу (TechniqueLearn-контракт) либо fail-fast
как у item, либо тост «тип награды не поддерживается».

### QST-3 [P2] quest_talk_elder недостижим из диалога — нарративный seam

**Файл:** `DialogueService.cs:396` (QuestIdsToStart =
{quest_kill_wolves, quest_gather_iron}); QuestService.cs:407-438.
**Сценарий:** квест «поговори со старейшиной» НЕ выдаётся старейшиной —
только кнопкой «Принять» в окне Q (QuestWindow.cs:203-219). Механика
работает (E → NPCInteractedEvent(RoleId="Elder") → Complete → награда),
но контур «старейшина поручает поговорить со старейшиной» абсурден;
DIALOGUE_SYSTEM.md:81 обещает «выдать квест» выбором.
**Фикс:** добавить quest_talk_elder в QuestIdsToStart другого узла/роль
или переименовать под самоназначение.

### QST-4 [P2] UpdateObjectiveProgress публикуется без изменения прогресса

**Файл:** `QuestProgressTracker.cs:63-79` — матч без IsComplete-гейта;
:71 — событие вызывается даже когда AddProgress вернул false
(QuestObjective.cs:50-51 `if (IsComplete) return false`).
**Сценарий:** цель 5/5, квест ждёт вторую цель → каждый новый
ItemAdded(руда) публикует холостое QuestObjectiveUpdatedEvent(5/5);
сейчас подписчиков нет (QST-6), при подключении UI/журнала — спам.
**Фикс:** публиковать только при фактическом изменении прогресса.

### QST-5 [P3] FailQuest/AbandonQuest — недостижимые ветки

**Файл:** QuestService.cs:151-167 (AbandonQuest — 0 вызовов);
:182-192, :301-315 (FailQuest — только истечение TimeLimitDays; у всех
3 дефолтных квестов TimeLimitDays=0). QuestFailedEvent/QuestAbandonedEvent
не публикуются в игровых условиях; UI «бросить» нет; брошенный квест
нельзя перевзять (Status != NotStarted — вечный отказ).
**Фикс:** контент с TimeLimitDays>0 + кнопка «Бросить» (ветки корректны:
AbandonQuest сбрасывает Progress).

### QST-6 [P3] Четыре квест-события — 0 подписчиков (мёртвые контракты)

**Файл:** QuestContracts.cs:16-57 — QuestStarted/ObjectiveUpdated/Failed/
Abandoned: единственный подписчик квест-контрактов — QuestRewardService
(QuestCompletedEvent, :39). JOURNAL_SYSTEM.md:17 («Прогресс квестов» в
журнале) и QuestContracts.cs:8 («C02: QuestType for UI») обещают
потребителей; QuestWindow обновляется только Rebuild при открытии.
**Фикс:** подписать журнал/QuestWindow-live либо удалить проводы.

### QST-7 [P3] «Сбор» = «получение любым способом»: покупка двигает GatherItem

**Файл:** `QuestProgressTracker.cs:85-86` (OnItemAdded безусловно +Count);
источники: InventoryService.AddItem (:149,173,187,211 — любое
происхождение, вкл. покупку/квест-награду); SpiritStorageService.StoreItem
(:83,99) — сам мёртв (0 вызовов), двойного прогресса через кольцо НЕТ.
**Сценарий:** «собери 5 руды» выполняется покупкой у торговца; предметная
квест-награда засчитается в прогресс ДРУГОГО gather-квеста на тот же
ItemId (в дефолтном контенте пересечений нет — латентно). ItemRemovedEvent
не отслеживается («принеси»-квесты потребуют инвентарь-снимок).
**Фикс:** контрактом решить семантику gather (origin-фильтр) или
проверка наличия на CompleteQuest.

### QST-8 [P3] QuestWindow «Принять» — тихий отказ

**Файл:** `QuestWindow.cs:209-217` — StartQuest false → молчание;
диалоговый путь честно тостит причину (QuestService.cs:84-100,
StartQuestRejectionReason).
**Сценарий:** MaxActiveQuests/гейт культивации → кнопка visual no-op.
**Фикс:** reuse StartQuestRejectionReason → ToastShownEvent.

## Проверено чисто

- **Живой цикл:** E → роль-диалог (GWC → NPCService.OnNPCInteracted:221-226
  с RoleId) → «Конечно, помогу» → QuestStartRequestedEvent ×2
  (DialogueService.cs:188-198) → StartQuest с гейтами (существование/
  NotStarted/MaxActive/предквест/RequiredCultivationLevel :126-148; кэш
  уровня из QiChangedEvent с PlayerIdResolver-фильтром :102-106) →
  тосты с причинами отказа.
- **Semantic targets (09-08):** NormalizeKillTargetId «animal_wolf_5»→
  «wolf» (:108-116; EnemyKilledEvent публикует только CombatService при
  убийстве игроком — виктим-центрично, повторного счёта нет); руда —
  canonical material_iron_ore; старейшина — RoleId «Elder»; двухвызовный
  матч NpcId+RoleId (:91-100) — объектив матчится максимум одним
  измерением, двойного прогресса нет.
- **Награды:** двойной выдачи в сессии нет — AreRewardsGranted-гейт (:68),
  MarkRewardsGranted после цикла (:95), CompleteQuest гейтится
  Status==Active → повторный QuestCompletedEvent невозможен; item-награды
  валидируются через IItemDatabaseService ДО публикации (:75-87);
  QiAddRequestEvent — единственная точка (EntityId-пусто → игрок).
- **Прогресс-математика:** AddProgress — гейт отрицательных amount (B04),
  кламп к Target, justCompleted (QuestObjective.cs:48-55); CompleteQuest
  только при AllObjectivesComplete && justCompleted — двойного нет;
  пустые цели → false (safe). UpdateDay/FailQuest только при реальном
  истечении (IsExpired: currentDay > StartDay + TimeLimitDays).
- **Сейвы смежные:** technique_slots round-trip чист (SaveLoadSimDebug);
  квесты — QST-1. GetQuestSummaries — read-only DTO, корректен для
  QuestWindow-рендера.

## OK-BY-DESIGN (с доказательством)

- Единый QuestData на квест (шаблон = состояние) — V1, не мультиплеер;
  AbandonQuest сбрасывает Reset() — консистентно.
- _playerCultivationLevel=1 до первого QiChangedEvent — консервативный
  дефолт (гейты строже).
- quest_reach_forest НЕ регистрируется (travel не реализован — честное
  «не обещать», QuestService.cs:326-335).
- QuestModule.Tick пуст — event-driven, корректно (P2-4 чистил конфиг).

## Соответствие docs_v2

| Дока | Статус |
|------|--------|
| SAVE_SYSTEM.md:222 (блок «quests») | ✖ не реализовано (QST-1) |
| DIALOGUE_SYSTEM: выбор → выдать квест | ✔ кроме talk_elder (QST-3) |
| JOURNAL_SYSTEM: «Прогресс квестов» в журнале | ✖ события без подписчиков (QST-6) |
| QuestContracts «C02: QuestType for UI» | ✖ потребитель отсутствует (QST-6) |

*Аудит Quest завершён. Код не изменялся (READ-ONLY). Приоритет: QST-1
(единым сейв-эпизодом с QI-2) → QST-2 → QST-3/4 → P3.*
