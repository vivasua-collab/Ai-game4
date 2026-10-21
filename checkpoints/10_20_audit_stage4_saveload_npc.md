# Чекпоинт: Этап 4 аудита — неудачная загрузка/полнота формата/NPC (SPAWN+GROUPS)

Дата: 2026-10-20, сессия ~15:10–17:30 UTC.
Вход: HEAD 6ca7bc8 (Этап-3 закрыт, 23/23 QA), внешний аудит Этапа 4
(2 остаточных P1 Этапа-3 + BUG-013 P2 + GAP-014 P2) и порядок работы из
аудита: (1) разрушительный путь ошибки LoadGame + полнота формата;
(2) синхронизация NPC-спавнера; (3) решение по группам NPC; (4) QA
через полный GameSession, не только тесты агрегатора.

## 1. Валидация аудита Этапа 4 — ВСЕ 4 ПОДТВЕРЖДЕНЫ

| # | Находка | Вердикт | Корень |
|---|---------|---------|--------|
| P1-а | Неудачная загрузка уничтожает текущую сессию | ПОДТВЕРЖДЕНО | GameSession.LoadGame: WorldDomainResetCoordinator.ResetAll ДО _save.Load — сброс успевал уничтожить живой мир ДО первой проверки сейва. Этап-3 добавил переход из Playing (F5) — дефект стал играбельным: битый слот = потеря несохранённого прогресса |
| P1-б | Проверка формата пропускает отсутствующие блоки | ПОДТВЕРЖДЕНО | CriticalBlocksV1 = {world, world_time, player, body} — неполон; RestoreOne молча пропускал ЛЮБОЙ отсутствующий блок: сейв без inventory/qi/quests/npc/npc_relationships проходил, а после сброса эти домены оставались пустыми |
| BUG-013 | После загрузки NPC не восстанавливается учёт спавнера | ПОДТВЕРЖДЕНО | NPCService.RestoreState пишет в реестр напрямую (минуя SpawnNPC); NPCSpawnerService._spawnedIds — приватный трекер, ActiveNPCCount/лимит MaxActiveNPCs/гейт DespawnNPC читают ТОЛЬКО его; спавн-фазы SkipOnLoad=true → после Load трекер пуст |
| GAP-014 | Групповые состояния NPC не восстанавливаются | ПОДТВЕРЖДЕНО (решение: персистеть) | NPCGroupService : только INPCGroupService (не ISaveable); групповые фазы SkipOnLoad=true. Группы — мировое состояние (патрули/караваны/стаи/банды задают поведение мира) → блок npc_groups обязан существовать |

## 2. Исправления

### FIX-1 (P1-а) — транзакционная загрузка GameSession.LoadGame
- **Pre-flight ДО сброса**: `ISaveService.Validate(slot)` → агрегатор
  читает файл и гоняет ValidateFormat БЕЗ RestoreState. Отказ =
  `LoadGameOutcome.RejectedByPrecheck`, домены не тронуты ВООБЩЕ,
  сессия в прежнем состоянии (Playing), тост с причиной.
- **In-memory снимок при загрузке из игры**: `ISaveService.CaptureBackup()`
  (агрегатор `CaptureSnapshot()`: тот же CaptureState-конвейер, БЕЗ записи
  файла и БЕЗ SaveStartedEvent — снимок неразрушающий для transient-систем).
- **Откат**: сбой после сброса → повторный ResetAll (вычистить частично-
  восстановленное, RestoreState = merge) → `RestoreBackup()` →
  Data=prevData, `SetState(Playing)`, `RolledBack`, IPostLoadSync, тост.
  Fail-closed в MainMenu — только если снимка нет/не снялся/провалился.
- **`IGameSession.LastLoadOutcome`** (enum None/Success/RejectedByPrecheck/
  RolledBack/Failed) — пост-фактум маршрутизация UI.
- **GWC**: reject → БЕЗ пересборки сцены, причина в статусе меню сейвов
  (ShowLoadError); Success/RolledBack → пересборка GameWorld; Failed →
  главное меню. **MainMenuController**: причина из LastError в статусе.

### FIX-2 (P1-б) — полный каталог обязательных блоков
- `CurrentFormatVersion` 1 → **2** (версия формата теперь из каталога,
  не хардкод в SaveService.CaptureState).
- **v2** (текущий писатель, 23 блока): ВСЕ обязательные — world,
  world_time, item_db, player, stats, body, qi, inventory, equipment,
  storage_rings, belt, techniques, technique_slots, formation, npc,
  npc_groups, npc_relationships, corpses, animals, quests, currency,
  charger, save_meta. Отсутствие любого = файл не этого конвейера → отказ.
- **v1** (легаси): 17 обязательных ядра + ЯВНОЕ правило совместимости
  `OptionalBlocksV1` = {formation (R15), item_db (R17), corpses (R13),
  storage_rings, charger (R37), npc_groups (Этап-4)} — «отсутствует =
  домен в дефолте после сброса», с диагностикой отсутствующих в лог.
  Аудит: «необязательные блоки должны быть явно определены правилами
  совместимости, а не молча пропускаться» — выполнено.

### FIX-3 (BUG-013) — IPostLoadSync + спавнер
- **`Core.Interfaces.IPostLoadSync.OnAfterLoad()`** — новый контракт
  (симметричен IWorldResettable): GameSession зовёт
  `ResolveAll<IPostLoadSync>()` после RestoreState И после отката.
  Решение против фазы: спавн-фазы SkipOnLoad=true, синхронизация —
  пост-загрузочный доменный hook, не сборочная фаза.
- `NPCSpawnerService.SyncWithRegistry()`: реестр NPCService — источник
  истины; трекер = все ID реестра (живые + мёртвые — инвариант штатного
  спавна-смерти-деспавна); убрать мёртвые ссылки, добавить отсутствующих.
- `DespawnNPC`: гейт «трекер ИЛИ реестр» — восстановленный NPC проходит
  полный путь очистки (провайдеры тела/Ци/экипировки, баффы, якоря,
  отношения, реестр, трекер, событие).
- `INPCSpawnerService.SyncWithRegistry()` — в интерфейсе (диагностика/QA).

### FIX-4 (GAP-014) — NPCGroupService : ISaveable
- Блок **`npc_groups`** (v2-обязателен): GroupId, TaskType, LeaderId,
  MemberIdsJoined («|» — конвенция NPCSaveEntry), Faction, Species,
  CurrentWaypointIndex, маршрут flat X/Y, GuardCenter (флаг+координаты),
  GuardRadius, EscortTargetId, IsActive, **NextGroupId** (анти-коллизии
  ID новых групп с восстановленными). CurrentGroupTarget — runtime,
  не сохраняется.
- **RestoreOrder**: npc_groups ПОСЛЕ npc И animals — состав группы
  наполняется и NPC (патруль стражи), и зверями (волчья стая/стадо —
  AnimalService); участники валидируются по ОБЕИМ реестрам
  (NPCService.GetNPCState + IAnimalService.TryGetAnimal). Найденный
  в регрессии косяк первой ревизии: валидация только по NPC роняла
  волчью стаю (участники animal_wolf_*) — исправлено + порядок.
- RestoreState: полная пересборка (не merge), отсутствующие участники
  отбрасываются с логом, лидер-промоут по семантике RemoveMember,
  пустая группа не восстанавливается.
- DI: `Register<ISaveable, NPCGroupService>` (форвард на тот же singleton,
  паттерн NPCService).

## 3. QA — новый сим STAGE4 (полный GameSession-путь)

**Stage4SaveLoadSimDebug** (`GODOT_STAGE4_DEBUG=1`, сим №22 в
qa_regression.sh, wired в GWC): аудит требовал «неуспешную загрузку
через полный GameSession, а не только тесты агрегатора» — сим зовёт
Session.LoadGame напрямую.

Шаги: (1) маркеры [камни +5, QA-группа: лидер+участник, Patrol,
маршрут 3 точки; инвариант спавнер==реестр] → Save A; (2) мутации
[камни→0, DisbandGroup]; (3) pre-flight [v2 без quests → «обязательный
блок», без npc_groups → «обязательный блок», версия+99 → «новее»] —
все RejectedByPrecheck, State=Playing, мир нетронут (мутации живы);
(4) откат [npc-блок испорчен: bodyPartCount > длины массивов — проходит
pre-flight, IndexOutOfRange в RestoreState] → RolledBack, ДО-ЗАГРУЗОЧНЫЙ
мир восстановлен (камни 0, группы нет — НЕ из сейва!), спавнер синхронен;
(5) успех Load A → камни==сейв, QA-группа восстановлена (id/лидер/состав/
маршрут/TaskType/GetGroupByMember); (5b) спавнер==реестр, все живые NPC
в трекере; (6) DespawnNPC восстановленного → реестр/трекер/провайдеры
(EquipmentDataProvider.GetTotalArmor==0) чисты, счётчик −1; (7) спавн
после загрузки согласован.

Фейки AUDIT0922SimDebug (FakeGameSession/FakePlayingSession/FakeSaveService)
дополнены новыми членами интерфейсов.

## 4. Регрессия

- `dotnet build` — 0 Error(s) (524 warnings — базовый уровень, в
  изменённых файлах 0 новых).
- **qa_regression.sh 22/22 PASS** (21 прежний + новый STAGE4) + FORM +
  RESPAWN = **24/24** — паритет с «23/23» волны-1 с учётом нового сима.
- Пруфы: `qa_artifacts/stage4_sim_20261020.log` (полный лог STAGE4,
  VERDICT: PASS), `qa_artifacts/qa_regression_stage4_20261020.log`
  (итог 22/22).
- Найденная и закрытая в ходе QA регрессия первой ревизии: npc_groups
  round-trip FAIL в SAVELOAD (волчья стая — участники-звери отброшены
  валидацией по NPC-реестру) → состав групп валидируется по обоим
  реестрам + RestoreOrder npc_groups после animals → 22/22.

## 5. Доки и портал

- **SAVE_SYSTEM.md**: §4.5 RestoreOrder (npc_groups после npc+animals,
  зависимость описана), §4.7 IPostLoadSync (новый), §7.2 — пролог Load
  Этапа-4 (pre-flight/снимок/откат/outcome), §8.0 — каталог версий
  v1/v2 с явными правилами совместимости.
- **TESTING_RULES.md**: строка SAVELOAD дополнена шагами 9-12 Этапа-3
  (пропущенными при Этапе-3) + блоком Этапа-4; новая строка STAGE4.
- **portal/data/bugs.json v1.6**: STAGE4-001 (P1 транзакционная
  загрузка), STAGE4-002 (P1 каталог v2), STAGE4-003 (P2 BUG-013),
  STAGE4-004 (P2 GAP-014) — все verified (QA); портал (порт 3000)
  отдаёт реестр v1.6 (31 запись) — проверено через /api/portal.

## 6. Файлы

Новые: `Core/Interfaces/IPostLoadSync.cs`,
`Adapter/Scene/Stage4SaveLoadSimDebug.cs`,
`qa_artifacts/stage4_sim_20261020.log`,
`qa_artifacts/qa_regression_stage4_20261020.log`.
Изменённые: SaveDataAggregator (v2-каталог, Validate/CaptureSnapshot/
RestoreSnapshot, RestoreOrder), SaveService+ISaveService (Validate/
CaptureBackup/RestoreBackup/DiscardBackup, Version из каталога),
GameSession (транзакция + LastLoadOutcome + IPostLoadSync),
IGameSession (LoadGameOutcome), NPCSpawnerService (IPostLoadSync,
SyncWithRegistry, гейт деспавна), NPCGroupService (ISaveable),
NPCModuleServices, INPCService-файл (SyncWithRegistry в интерфейсе),
GWC + MainMenuController (маршрутизация), Audit0922SimDebug (фейки),
qa_regression.sh, SAVE_SYSTEM.md, TESTING_RULES.md, bugs.json.

NEXT: (1) плейтест владельца — внутриигровая загрузка F5 на живом мире
(выбрать битый слот → остаться в игре с тостом); (2) аудит-ответ: Этап 4
закрыт 4/4, Этап-3 остатки сняты; (3) волна 2 портала (GAP-002/003/004)
— по директиве владельца.
