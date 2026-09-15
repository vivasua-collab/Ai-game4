# R17 «Полнота сейва» — save/load всех world-scoped доменов

**Дата:** 2026-09-15
**Эпизод:** R17 (из очереди аудита 09_11, чекпоинт 09_11_full_audit_and_p1_fixes.md)
**Контекст:** сессия 2026-09-14 оборвалась на Фазе B5 (Formation/Charger/Animal) —
окружение умерло; работа фаз A–B4 выжила в рабочем дереве (21 файл), B5 и
инфраструктурная проводка (C) реализованы в этой сессии с нуля по плану.

## Состав эпизода (P1-семья «state не в сейве/не сбрасывается»)

| Дефект | Домен | Решение |
|--------|-------|---------|
| QI-2 [P1] | Qi-стейт игрока вне сейва | QiService ISaveable (блок `qi`) + IWorldResettable |
| QST-1 [P1] | Квесты сбрасываются (фарм наград) | QuestService ISaveable (`quests`) + ResetWorld→RegisterDefaultQuests |
| TRD-1 [P1] | Валюта не сохраняется (дюп камней) | CurrencyService ISaveable (`currency`) + ленивый ре-инициал |
| INV-4 [P1] | Кукла/пояс вне сейва (дюп экипировки) | EquipmentService (`equipment`, itemId→каталог), BeltService (`belt`) + IWorldResettable |
| G-2 [P1] | Фантомные ID предметов при cold-load | ItemDatabaseService ISaveable (`item_db`, типо-дискриминированный снимок) |
| G-3 [P3] | Счётчики ID генераторов | static Get/SetIdCounter (EquipmentGenerator, ItemGeneratorService), снапшот в `item_db` |
| NPC-1 [P2] | Ghost-animals переживают LoadGame | AnimalService ISaveable (`animals`, flat-массивы тел по паттерну NPCService) + ResetWorld=ClearAnimals |
| E-1 [P2] | Сброс world-scoped доменов размазан | IWorldResettable (новый Core-контракт) + WorldDomainResetPhase (фаза 0, замена NpcDomainResetPhase) + GameSession.LoadGame ResolveAll |
| E-3 [P2] | SkipOnLoad без сейва: позиция/мир/время | PlayerService (`player`), WorldService (`world`), TimeService (`world_time`); GameSession.LoadGame читает Data.WorldId/WorldTime из восстановленного состояния (хардкод TestPolygon/06:00 удалён) |
| E-4 [P2] | Тики/автосейв из главного меню | GameBoot._PhysicsProcess гейт `SessionState.Playing` |
| WT-5 [P2] | Время не сохраняется/не сбрасывается | TimeService ISaveable + ResetWorld (06:00 дня 1) |
| WT-3 [P2] | large_world не в реестре WorldService | Каталог локаций регистрирует GameEntryPoint (Entry-слой; перенос из WorldModule — дисциплина слоёв Core←Modules←Entry) |
| SAV-1 [P2] | Неатомарная запись | tmp→rename в обоих SaveFileHandler (Module + Adapter) |
| SAV-2 [P2] | Автосейв из меню, autosave_NNNN-плоды | SaveModule: инжект SaveConfig+IGameSession, гейт Playing, единый слот `autosave`, интервал 30 игровых минут из конфига |
| SAV-5 [P2] | Порядок RestoreState = порядок DI | Явный контракт RestoreOrder в SaveDataAggregator (19 блоков) |
| SAV-7 [P3] | Delete без try/catch | Оба хендлера — честный false + лог |
| CH-2 [P2] | Warm-load зарядника мержит поверх живого | ResetLiveState() в начале RestoreState (слоты→RemoveStone, буфер→ExtractQi, ChargerHeat.Reset(), mode→Off) + IWorldResettable |
| F-2 [P2] | Формация: позиция не в сейве, события не пере-публикуются | FormationSaveData +posX/posY/autoFillAccumulator; RestoreState пере-публикует StageChanged/Activated (визуал/медитация ×2/зона Amplification); long.TryParse + Enum.IsDefined |
| F-4 [P2] | Формация переживает пересборку мира | FormationService.ResetWorld (DeactivateFormation + сброс полей) |

## Архитектура

### IWorldResettable (новый контракт, Core/Interfaces)
World-scoped домен сбрасывается к «свежему процессу» (идемпотентно):
NewGame — WorldDomainResetPhase (фаза 0, ResolveAll), LoadGame —
GameSession.LoadGame ДО RestoreState (сейв = единственный источник истины).
Реализаторы (16): NPCModule, AnimalService, FormationService, ChargerService,
QiService, QiModule, PlayerService, StatService, QuestService, CurrencyService,
InventoryService, EquipmentService, BeltService, GroundItemService,
WorldService, TimeService.

### RestoreOrder (контракт загрузки)
```
world → world_time → item_db → player → stats → body → qi →
inventory → equipment → belt → techniques → technique_slots →
formation → npc → animals → quests → currency → charger → save_meta
```
Ключевое: item_db ДО equipment/belt/npc (резолв itemId); мир/время — первыми
(GameSession читает их после Load); отсутствующие ключи пропускаются.

### Реестр сейва: 8 → 19 блоков
Новые: world, world_time, item_db, player, stats, qi, equipment, belt,
animals, quests, currency.

## Ловушки, найденные при реализации (QA-детектед)

1. **FormationService.ResetWorld обнулял кэш Ци** (порядок сбросов DI:
   QiService.ResetWorld → publish 1000 → FormationService.ResetWorld → кэш 0
   → StartDrawing «недостаточно Ци»). Фикс: кэши Qi — зеркала живого
   процесс-scoped состояния игрока, в сбросе мира НЕ трогаются.
   Поймано SAVELOAD QA (integrity «формация»), воспроизведено диагностикой.
2. **AbandonQuest → Abandoned**, а не NotStarted — ассерт QA исправлен.
3. **Композиция зверей = 3–5 + стая GroupSpawnPhase** — ассерт REASSEMBLY
   сделан композиционно-относительным (не хардкод 3–5).
4. Слой-дисциплина: WT-3-фикс оборванной сессии тянул Entry.LocationCatalog
   из WorldModule (Modules→Entry — обратное направление) — перенесено в
   GameEntryPoint (законный Entry-слой), WorldModule оставлен fallback
   test_polygon.

## QA

- Build: 0 errors (395 warnings — базовый уровень).
- SAVELOAD v3: **PASS** — 19 ISaveable (порог ≥18, R17-блоки 11/11),
  round-trip 18/18 блоков, домены 7b+7c (Ци/баланс/квест/позиция/STR/
  каталог/звери), integrity-мутации 12/12 реальны.
- REASSEMBLY: **PASS** — 16/16 фаз, NPC/трупы/группы/реестр техник + R17:
  звери (QA-зверь не переживает сборку, поголовье == композиции), время
  (06:00 дня 1 после пересборки).
- Полная регрессия 15 симов: см. tools/qa_regression.sh (запуск из сессии).

## Файлы (33)

Core: Interfaces/IWorldResettable.cs (новый).
Entry: Phases/WorldDomainResetPhase.cs (новый), Phases/NpcDomainResetPhase.cs
(удалён), SceneAssemblyRegistrar.cs, GameSession.cs, GameEntryPoint.cs.
Modules: Qi (QiService, QiModule), Quest (QuestService), Trade (CurrencyService),
Inventory (InventoryService, EquipmentService, BeltService, GroundItemService),
Player (PlayerService, StatService), World (WorldService, WorldModule),
Formation (FormationService), Charger (ChargerService, ChargerHeat),
NPC (AnimalService, NPCModule), Generator (ItemDatabaseService,
ItemGeneratorService, EquipmentGenerator), Save (SaveConfig,
SaveDataAggregator, SaveFileHandler, SaveModule).
Adapter: Persistence/SaveFileHandler.cs, Scene/GameBoot.cs,
Scene/SaveLoadSimDebug.cs (v3), Scene/ReAssemblySimDebug.cs (R17-ассерты).
Docs: docs_v2/05_data/SAVE_SYSTEM.md (§4.4 реестр, §4.5 RestoreOrder,
§4.6 IWorldResettable, §6.1 автосейв, §9.1 атомарность).
Tools: tools/qa_regression.sh (новый раннер полной регрессии).

## Остаток (R18-очередь)

F-5 (снимок генерируемых формаций — FormationRegistry не в сейве), CH-4
(перегрев не восстанавливается), SAV-4 (SaveAndQuit мёртв), SAV-6 (Version
без миграций), G-1+NPC-6 (один генератор экипировки NPC), NPC-2 (NPC сквозь
стены), трупы вне сейва (осознанно V1), предметы на земле вне сейва (V1).
