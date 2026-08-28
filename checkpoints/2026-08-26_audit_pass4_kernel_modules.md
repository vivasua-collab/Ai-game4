# АУДИТ-4: Все подсистемы, монтируемые в ядро (kernel-mounted modules)

**Дата:** 2026-08-26, старт 17:30 MSK
**Проход:** 4 (серия аудитов, основной поток ИИ)
**HEAD на старте:** `1fd576a` (synced)
**Scope:** каждая подсистема из GameLifetimeScope (17 модулей) и
SceneAssemblyRegistrar (фазы). Инварианты аудитов 1–3 считаются
удержанными. Полный аудит НЕохваченных модулей + сквозные проверки
уже охваченных.

## План прохода

- [x] A0. Сквозные инварианты по ВСЕМ модулям (grep-матрица):
  P0-DUAL-PLAYER-ID (PlayerIdResolver adoption), Subscribe/Dispose
  парность, Core→Modules нарушения, Godot-using в Core.
- [x] A1. Inventory (16 файлов, 3114 строк) — полный аудит.
- [x] A2. Buff (7 файлов, 839 строк) — полный аудит.
- [x] A3. Player (9 файлов, 1612 строк) — полный аудит.
- [x] A4. Quest (5 файлов, 710 строк) — полный аудит.
- [x] A5. Interaction (7 файлов, 1067 строк) — полный аудит.
- [x] A6. NPC — AI/Movement/Relationship (остаток от аудита-2).
- [x] A7. Charger (7 файлов, 1562 строк) — кроме точечных проверок аудита-3.
- [x] A8. Save (5 файлов, 406 строк) — полный аудит.
- [x] A9. UI module (5 файлов, 508 строк) — module-сторона.
- [x] A10. Generator module — wiring/registration/lifecycle
  (генераторы глубоко проверены в верификационной работе 08-26 №1).
- [x] A11. Фиксы критичных находок + build + headless-регресс.
- [x] A12. Коммит + push.

## Сводка (заполняется по ходу)

| Подсистема | Прочитано | Находок | Фиксов |
|------------|-----------|---------|--------|
| A0 сквозные | grep-матрица 17 модулей | 0 (чисто) | — |
| A1 Inventory | InventoryService, EquipmentService, InventoryModule, BeltService, EquipmentDataProvider | 3 (EQ-A1 MAJOR, EQ-A2, INV-A1) | 2 (EQ-A1, EQ-A2) |
| A2 Buff | BuffService, BuffModule, BuffTickProcessor | 2 (BUFF-A1 латент, BUFF-A3) | 0 (задокументированы) |
| A3 Player | | | |

### A1 Inventory — детали

- **EQ-A1 [MAJOR, ФИКС]:** EquipmentService.TryEquip — автоснятие WeaponOff
  при надевании двуручного оружия публиковало EquipmentChangedEvent через
  4-arg ctor (OldItemId=null) → InventoryModule.OnEquipmentChanged выходил
  по `IsNullOrEmpty(e.OldItemId)` → **предмет из левой руки уничтожался**
  (не возвращался в инвентарь). Фикс: 5-arg ctor с offHand.ItemId.
- **EQ-A2 [MINOR, ФИКС]:** OnBodyPartSevered — автоснятие при ампутации не
  вызывало SyncToProvider() → CombatService видел устаревшие статы до
  следующей смены экипировки. Фикс: SyncToProvider() после Remove.
- **INV-A1 [MINOR, задокументирован]:** TryAddItem — при одновременном
  сплите стака (MaxStack) и лимите объёма out-addedCount может завышать
  число (рекурсия отбрасывает уточнённый count). Влияет только на число
  дропа-на-землю в угловом случае.
- Чисто: InventoryModule (4 подписки парные, Dispose ✓), BeltService
  (гейт пояса по событию корректен, heal по самым раненым частям),
  EquipmentDataProvider (RemoveEntity чистит все 5 кэшей, копии словарей).

### A2 Buff — детали

- **BUFF-A1 [MINOR, латентно]:** TickBuffs итерирует _entityBuffs с
  публикацией событий внутри цикла. Когда DoT-урон переедет в
  CombatPipeline (сейчас BodyModule только логирует), цепочка
  «яд→смерть→RemoveAllBuffs» даст мутацию словаря во время итерации →
  InvalidOperationException. Рекомендация: снапшот ключей при
  подключении DoT-урона.
- **BUFF-A3 [MINOR]:** RemoveAllBuffs шлёт BuffRemovedEvent, но не
  StatModifierChangedEvent — подписчики статов не видят сброс к 0.
- Чисто: иммунитет-маппинг BF-A03, кап модификаторов, промилле-конверсия,
  BF-I04 анти-дрифт тиков, обратная итерация, cleanup пустых списков.

---

| Время MSK | Действие |
|-----------|----------|
| 17:30 | Старт Аудита-4 |

---

## Сводка (по ходу, append-only)

| Подсистема | Прочитано | Находок | Фиксов |
|------------|-----------|---------|--------|
| A0 сквозные | grep-матрица 17 модулей | 0 (чисто) | — |
| A1 Inventory | InvSvc, EquipSvc, InvModule, BeltSvc, EquipDataProvider (+skim прочих) | 3 | 2 |
| A2 Buff | BuffService, BuffModule, TickProcessor | 2 (латент) | 0 |

### A1 Inventory — детали

- **EQ-A1 [MAJOR, ФИКС]:** EquipmentService.TryEquip — автоснятие WeaponOff
  при надевании двуручного публиковало событие через 4-arg ctor
  (OldItemId=null) → InventoryModule.OnEquipmentChanged выходил по
  IsNullOrEmpty(e.OldItemId) → **предмет из левой руки уничтожался**
  (не возвращался в инвентарь). Фикс: 5-arg ctor с offHand.ItemId.
- **EQ-A2 [MINOR, ФИКС]:** OnBodyPartSevered — автоснятие при ампутации
  не вызывало SyncToProvider() → CombatService видел устаревшие статы.
  Фикс: SyncToProvider() после Remove.
- **INV-A1 [MINOR, док]:** TryAddItem — при одновременном сплите стака и
  лимите объёма out-addedCount может завышать (рекурсия отбрасывает
  уточнение). Влияет только на дроп-на-землю в угловом случае.
- Чисто: InventoryModule (подписки парные), BeltService (гейт пояса,
  heal по раненым частям), EquipmentDataProvider (RemoveEntity чистит
  все 5 кэшей).

### A2 Buff — детали

- **BUFF-A1 [MINOR, латентно]:** TickBuffs публикует события внутри
  итерации _entityBuffs. Когда DoT-урон переедет в CombatPipeline (сейчас
  BodyModule только логирует) — цепочка яд→смерть→RemoveAllBuffs даст
  мутацию словаря во время итерации → InvalidOperationException.
  Рекомендация: снапшот ключей при подключении DoT-урона.
- **BUFF-A3 [MINOR]:** RemoveAllBuffs шлёт BuffRemovedEvent, но не
  StatModifierChangedEvent — подписчики статов не видят сброс к 0.
- Чисто: BF-A03 иммунитет-маппинг, кап модификаторов, промилле,
  BF-I04 анти-дрифт, cleanup пустых списков.

## Журнал (append-only, продолжение)

| Время MSK | Действие |
|-----------|----------|
| 17:35 | A0: сквозная матрица чиста (DUAL-PLAYER-ID только в doc-комменте; Core без Modules/Godot-using; Subscribe/Dispose парность структурно) |
| 17:50 | A1 Inventory: EQ-A1 MAJOR (потеря off-hand при двуручнике) + EQ-A2 фиксы применены |
| 18:00 | A2 Buff: чисто; 2 минора задокументированы (TickBuffs реентерабельность латентна — DoT пока не наносит урон) |

### A3 Player — детали

- ЧИСТО. PlayerService: PlayerIdResolver (B1), идемпотентный Spawn
  (audit#3), IsAlive делегирован BodyService, парные токены.
  PlayerModule: PLR-E06 порядок сброса флагов (задокументирован),
  мёртвый код движения оставлен сознательно (Adapter владеет).
  TechniqueSlotService: валидация IsLearned, belt-паттерн, парная
  подписка TechniqueForgotten. PlayerTechniqueCaster/PlayerCombatAdapter
  — аудит-3 покрыл зарядовый путь; подписки парные (3+3).

### A4 Quest — детали

- ЧИСТО. QuestProgressTracker: 6 подписок парные. Синергия с аудитом-3:
  после виктим-центричного фикса EnemyKilledEvent публикуется только при
  победе ИГРОКА → зачёт KillEnemy-целей теперь корректен (раньше игрок
  получал кредит при собственной гибели).

### A5 Interaction — детали

- ЧИСТО. DialogueService: 4 подписки парные; sentinel "open_trade" —
  порядок EndDialogue→TradeRequestedEvent соблюдён (фикс 08-25).
  DialoguePresenter/Typewriter: подписки в списке, Dispose чистит все.

| Время MSK | Действие |
|-----------|----------|
| 18:15 | A3 Player: чисто (0 находок) |
| 18:25 | A4 Quest: чисто (0 находок, синергия с C-1 аудита-3) |
| 18:30 | A5 Interaction: чисто (0 находок) |

### A6 NPC (AI/Movement остаток) — детали

- **NPC-A2 [INFO, структурно]:** GetAllStates() возвращает ЖИВУЮ
  ValueCollection; Tick/AI и OnYearChanged публикуют события внутри
  итерации по ней. Сегодня безопасно, т.к. RemoveNPC() НЕ вызывается
  нигде (словарь не мутируется). При появлении деспавна-удаления —
  нужен снапшот. Threat-decay уже использует снапшот-буфер (NPC-A07).
- SourceId-консистентность: DamageAppliedEvent.AttackerId = "player_0"
  (PlayerCombatAdapter) = NPCAIService.PlayerId ✓. Hostile-агро и
  Friendly-защита согласованы с обоими алиасами.
- Чисто: NPCRelationshipService (3/2 paired), PerkService (0 подписок),
  NPCQiRegenService (4/2 paired), NPCMovementService (DeltaTimе,
  позиция игрока из события).

### A7 Charger — детали
- ЧИСТО. 1 подписка QiChangedEvent парная; Dispose корректный.

### A8 Save — детали
- **SAVE-A1 [MINOR, известный долг]:** RegisterSaveable() не вызывается
  НИГДЕ вне Save/ → SaveDataAggregator пуст при сохранении (только
  метаданные SaveService). Сохранение отключено (Q8), долг уже в
  worklog. При включении save/load: собрать ISaveable из DI-контейнера
  в SaveModule.Start().
- Чисто: try/catch вокруг Capture/Restore (падение одного сервиса не
  роняет сохранение), ISaveFileHandler через Adapter-override.

### A9 UI — детали
- ЧИСТО. Все подписки парные (8/9, 2/3, 4/5 с учётом полей).

### A10 Generator — детали
- ЧИСТО. Модуль без подписок на события (регистрация + debug-dump);
  глубокий аудит генераторов — сессия 08-26 №1 (верификация/дедуп).

---

## ИТОГ АУДИТА-4

| Категория | Кол-во |
|-----------|--------|
| Прочитано файлов (полностью + целевые) | ~35 |
| Находок всего | 8 |
| — MAJOR (исправлено) | 1 (EQ-A1: потеря off-hand оружия) |
| — MINOR (исправлено) | 1 (EQ-A2: SyncToProvider при ампутации) |
| — MINOR (задокументировано) | 3 (INV-A1, BUFF-A1 латент, BUFF-A3) |
| — INFO/долг | 3 (NPC-A2, SAVE-A1) |

**Верификация:** dotnet build 0 errors (271 warnings прежний уровень);
NEWGAME PASS (14 фаз, PreGen 100/100, 6 техник); COMBAT_SIM VERDICT
PASS; TRADE_DEBUG PASS (buy/sell Пилюля Ци).

**Покрытие серии аудитов 1–4:** ядро (Core DI/EventBus/Entry) + все 17
модуулей, монтируемых в GameLifetimeScope, + фазовый пайплайн — 100%
подсистем ядра.

| Время MSK | Действие |
|-----------|----------|
| 18:40 | A6 NPC: NPC-A2 INFO; SourceId консистентен |
| 18:50 | A7 Charger / A8 Save / A9 UI / A10 Generator: чисто (SAVE-A1 известный долг) |
| 19:00 | build 0 errors; NEWGAME+COMBAT_SIM+TRADE_DEBUG PASS |

---

# ФИКС-СЕССИЯ 2026-08-28: закрытие задокументированных находок

**Дата:** 2026-08-28, 09:27–11:40 MSK
**Запрос:** «проверь результаты аудита, найденные ошибки и выполни
исправления» (+ восстановление окружения после сброса песочницы).
**HEAD на старте:** 6204593 (GitHub; локальный диск — устаревший снапшот
284eecc, fetch+reset). **Фикс-коммит:** fd39377.

## Окружение (сброс 2026-08-28)

- Godot/dotnet//tmp стёрты; my-project/godot — битый симлинк (источник
  «os error 2» в предпросмотре); репо на диске отстало на 3 коммита.
- cold_start.sh — 3 фикса: (1) python-извлечение убирает битый симлинк
  (иначе os.makedirs → FileExistsError); (2) pull/clone без валидного
  GITHUB_TOKEN использует публичный URL (placeholder-токен валился даже
  на публичном репо); (3) ln -sf → ln -sfn + чистка самоссылающихся
  симлинк-петель в репо (Ai-game4/Ai-game4 ломал rg/glob/предпросмотр).
- Прогон cold_start с нуля — PASS (SDK 8.0.424+9.0.317, Godot 4.7.1,
  build 0 errors, headless старт модулей).
- Сверка фиксов прошлой сессии: EQ-A1 (EquipmentService.cs:131-134,
  5-arg ctor с offHand.ItemId) и EQ-A2 (:242-244, SyncToProvider) —
  на месте, корректны.

## Фиксы (7)

| ID | Файл | Суть |
|----|------|------|
| BUFF-A1 | BuffService.TickBuffs | Двойной снапшот (сущности + список баффов), отложенное удаление истёкших ПОСЛЕ итерации; бонус: пересчёт StatModifierChanged ПОСЛЕ удаления (раньше newMod включал истёкший бафф) |
| BUFF-A3 | BuffService.RemoveAllBuffs | StatModifierChangedEvent по всем затронутым статам после Clear() — подписчики видят сброс к 0 |
| NPC-A2 | NPCService.GetAllStates | Снапшот List<NPCState> вместо живой ValueCollection — 5 мест итерации с публикацией событий безопасны при будущем деспавне |
| INV-A1 | InventoryService.TryAddItem | Обе рекурсии → 3-arg перегрузка с аккумуляцией addedCount (2-arg отбрасывала уточнение → завышение при сплите стака + лимите объёма) |
| SAVE-A1 | SaveModule.Start / SaveModuleServices | Сбор ISaveable через IResolver.ResolveAll (контейнер дедуплицирует форварды, кэш по impl-типу = те же синглтоны) + Register<ISaveable, SaveService> (save_meta). Лог: «6 ISaveable registered» |
| C-5 (аудит-3) | CombatContracts / CombatService | AttackRejectedEvent (паттерн EquipmentBlockedEvent) публикуется вместо тихого return при _isCasting |
| C-6 (аудит-3) | CombatConfig / CombatModuleServices | PlayerEntityId удалён (мёртвое поле после миграции на PlayerIdResolver) |

## Верификация

- build: 0 errors / 271 warnings (базовый уровень; 3 добавленных
  nullable-предупреждения отполированы аннотациями).
- NEWGAME PASS: 14 фаз, PreGen 100/100, 6 техник, state=Playing,
  [SaveModule] Started — 6 ISaveable service(s) registered.
- COMBAT_SIM VERDICT PASS: NPC→игрок 21 (Torso), игрок→NPC 10/14,
  qi-щит npc→npc 21.
- TRADE_DEBUG PASS: лавка 8 позиций, buy/sell True.
- GEN_DEBUG PASS: Epic 16.8% / Legendary 4.0%, оверкап 2/16 (12.5%),
  верификация 40/40, dmg 115 (оверкап) vs 104 (без) — эталоны Phase H.

## Итог по находкам аудита-4

| Находка | Статус |
|---------|--------|
| EQ-A1 MAJOR | ✅ фиксен 08-26, сверен 08-28 |
| EQ-A2 MINOR | ✅ фиксен 08-26, сверен 08-28 |
| INV-A1 MINOR | ✅ фиксен 08-28 |
| BUFF-A1 MINOR | ✅ фиксен 08-28 |
| BUFF-A3 MINOR | ✅ фиксен 08-28 |
| NPC-A2 INFO | ✅ фиксен 08-28 (снапшот) |
| SAVE-A1 MINOR/долг | ✅ фиксен 08-28 (ResolveAll) |

Все 7 находок аудита-4 закрыты; отложенных пунктов серии аудитов 1–4
(вкл. C-5/C-6 аудита-3) не осталось.

| Время MSK | Действие |
|-----------|----------|
| 09:27 | Диагностика сброса песочницы; дата 2026-08-28 |
| 09:33 | cold_start hardened (3 фикса); прогон с нуля PASS |
| 10:05 | Чтение Buff/Inventory/NPC/Save/Combat; сверка EQ-A1/A2 |
| 10:45 | 7 фиксов применены; build 0 errors / 271 warnings |
| 11:20 | 4 регресса PASS; коммит fd39377 + push |
