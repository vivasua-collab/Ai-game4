# АУДИТ: Save (сериализация, файловый слой, автосейв, события)

**Дата:** 2026-09-11. **HEAD:** `d0b065d` (READ-ONLY, код не менялся)
**Scope:** Modules/Save/ — все 6 файлов полностью (~0.7k строк): SaveService,
SaveDataAggregator, SaveFileHandler-Modules, SaveJson, SaveModule, SaveConfig.
Стыки: Adapter/Persistence/SaveFileHandler (222, полн.), GameBoot (override
ISaveFileHandler + тик-луп), GameSession (LoadGame/SaveAndQuit), GameEntryPoint/
Container.ResolveAll, MainMenuController, SaveContracts, все Register<ISaveable>
по репо, TechniqueChargeService/AuraHoldService (B5), SaveLoadSimDebug v2,
InventoryService/NPCService RestoreState, docs_v2/05_data/SAVE_SYSTEM.md (полн.).

---

## Сводка

Сериализационное ядро R11 добротно: типизированный round-trip
(JsonElement→StateType), транзакционность Save/Load, честные Completed-
события, единые IncludeFields-опции, B5-отмена кастов/аур, QA
SaveLoadSimDebug v2. 8 блоков реально собираются: body, inventory,
formation, techniques, npc, technique_slots, charger, save_meta. Слабая
периферия: неатомарная запись файла (дока обещает tmp+rename); автосейв,
который по доке «отключён», а по коду пишет — каждые 60 тиков, включая
меню, плодя слоты autosave_NNNN без чистки; конфиг не инжектится; мёртвый
контракт SaveRequested/LoadRequestedEvent; версии не проверяются.
Находок: P2×2, P3×6, OK×2 (UI-сейвы отключены Q8 — большинство латентно).

---

## Находки

### SAV-1 [P2] Запись файла неатомарна — обрыв записи = потерянный слот

**Файл:** `Adapter/Persistence/SaveFileHandler.cs:63-76` (Write →
File.WriteAllText); `Modules/Save/SaveFileHandler.cs:43-59` — то же.
**Сценарий:** краш/питание в момент записи → усечённый main.json; старый
слот уже открыт на перезапись → прогресс потерян. Load честно падает
(JsonException → null → «file missing, unreadable or invalid JSON»,
Aggregator:99-103), но восстановления нет. SAVE_SYSTEM §9.1 обещает
«temp file → rename», §9.2 — rolling backups: не реализовано ни в одном
хендлере. Автосейв уже пишет (SAV-2) → риск актуален.
**Фикс:** main.json.tmp → File.Move(tmp, path, overwrite); опционально
бэкап предыдущего.

### SAV-2 [P2] Автосейв: активен вопреки доке/Q8; «минуты» — фикция; слоты плодятся; пишет и из меню

**Файл:** `SaveModule.cs:36, 59-69`; `SaveConfig.cs:22`; `GameBoot.cs:93-124`;
`WorldService.cs:32`; `SAVE_SYSTEM.md:9`.
1. `SaveModule._config = new SaveConfig()` (36) — **не инжектится** →
   AutoSaveIntervalMinutes (5) читается лишь как `<= 0` вкл/выкл (63). Имя
   «Minutes» лжёт: интервал жёстко `tickCount % 60` = 1 игровой час.
2. Слот `autosave_{tickCount/60:D4}` (65) — каждый час новый каталог: 10 ч
   игры = 1000 слотов; MaxSaveSlots=5 (SaveConfig:35) не enforced, чистки нет.
3. Гейта сессии нет: GameBoot тикает всегда после _Ready, TimeService
   default Speed=Normal → **и в главном меню** тики идут и SaveModule.Tick
   пишет autosave-слоты. [По коду; запуск не выполнялся — READ-ONLY]
4. Шапка SAVE_SYSTEM.md:6-10 «автосейва нет» — противоречит коду и решению Q8.
Побочно: каждый автосейв публикует SaveStartedEvent → B5 гасит активную
зарядку/ауру (TechniqueChargeService.cs:131, AuraHoldService.cs:55) —
незапрошенный сбой каста раз в игровой час.
**Фикс:** инжект конфига; интервал = минуты×тики; гейт Playing; один слот
или ротация; синхронизировать доку.

### SAV-3 [P3] SaveRequested/LoadRequestedEvent — 0 паблишеров (мёртвый контракт)

**Файл:** `SaveModule.cs:71-86`; `WorldModule.cs:92-95` (no-op «noted»);
SaveContracts.cs:36-64. rg `new SaveRequestedEvent|new LoadRequestedEvent`
— **0 совпадений**. Реальные вызовы: SaveModule.Tick (автосейв),
GameSession.SaveAndQuit (Direct, мёртв — SAV-4), SaveLoadSimDebug (QA);
лоад: GameSession.LoadGame (Direct) и QA. Подтверждение Core-аудита.
**Фикс:** удалить ветки или публиковать из реальных триггеров при
реактивации (F5).

### SAV-4 [P3] GameSession.SaveAndQuit — мёртвый API, слот-сирота

**Файл:** `GameSession.cs:208-225`; `MainMenuController.cs:222-240`.
0 вызывателей из Adapter/UI; пишет в слот `Data.Id` (GUID после NewGame) —
MainMenu ищет только «quicksave» (222), GetAllSaves в UI не показывает.
**Фикс:** удалить или шина + канонический слот.

### SAV-5 [P3] Порядок RestoreState не контрактован

**Файл:** `Container.cs:124-140` (ResolveAll обходит `_registrations.Values`
— insertion-порядок Dictionary, деталь реализации .NET); Aggregator:105-120.
Порядок фактически = порядку регистраций GameLifetimeScope:75-91 (+formation
через форвард); рефакторинг регистраций молча его изменит. Критичных
межблочных зависимостей нет (InventoryService.RestoreState без событий —
561-587; NPCService — merge; GameSession сбрасывает NPC-домен ДО RestoreState
— R14, GameSession.cs:143). **Фикс:** явный порядок или комментарий-контракт.

### SAV-6 [P3] Версии сейва пишутся, но не проверяются; миграций нет

**Файл:** `SaveService.cs:27-47` (Version=1, RestoreState no-op);
`SaveConfig.cs:49` (SaveVersion — 0 читателей); SAVE_SYSTEM §8 — концепция
SaveMigrator. Сейв чужой версии грузится молча (отсутствующий блок ≠ ошибка,
Aggregator:108-110). **Фикс:** гейт Version при Load.

### SAV-7 [P3] DeleteSave без try/catch; GetAllSaves — SaveInfo-пустышки

**Файл:** `Modules/Save/SaveFileHandler.cs:82-88` (File.Delete — IOException
уйдёт наружу); `Adapter/…/SaveFileHandler.cs:113-121`; `SaveService.cs:94-103`
(CreatedUnixSeconds=0 — метаданные не читаются). **Фикс:** try/catch → false;
читать save_meta.SavedAt.

### SAV-8 [P3] Мёртвые поля SaveConfig + SaveService._config

**Файл:** `SaveConfig.cs:29, 35, 42, 55` (SaveDirectory не используется
Adapter-хендлером — хардкод user://saves, Adapter/…:46; MaxSaveSlots/
EnableCompression/QuickSaveEnabled — 0 читателей); `SaveService.cs:24`
(`_config = new()` — не инжект, не читается). **Фикс:** санация при
реактивации сейвов.

### SAV-9 [OK] 8 фактических ISaveable-блоков (formation — ЕСТЬ)

**Доказательство:** Register<ISaveable,X>: Body, Inventory, Technique
(Combat), NPC, TechniqueSlot (Player), Charger, SaveService — 7;
**formation — восьмой через impl-форвард DI** (FormationService : ISaveable,
ResolveAll матчит по типу инстанса, Container.cs:126-140); SaveLoadSimDebug.
cs:123-126 требует `≥8 && Contains("formation")` — QA зелёный по R11.
Ключи: body, inventory, formation, techniques, npc, technique_slots,
charger, save_meta. Тезис контекста «формации не в сейве» устарел (верно
для стейджа до R11). Прочие пробелы (Qi, quests, баффы, отношения,
Equipment/Belt, ground items, время, трупы) — вне скоупа, подтверждены
другими аудитами.

### SAV-10 [OK] Битый/чужой сейв — честный fail, не краш

Adapter.Load: JsonException → catch → null (195-209); Aggregator: null →
LastErrors → false (99-103); битой блок → restore[X] → false с перечнем
(105-131); GameSession catch → MainMenu (169-173); чужой тип блока —
конверсия через JSON с null-гейтом (ConvertToStateType 144-165).

---

## Проверено чисто

- **Транзакционность Save:** ошибка CaptureState любого блока → файл НЕ
  пишется (Aggregator:86-91); частичный сейв невозможен.
- **Типизированный round-trip:** JsonElement.GetRawText → Deserialize
  (StateType) → null-гейт (144-153); прямые значения — IsInstanceOfType.
- **SaveJson.Options:** IncludeFields/CamelCase/CaseInsensitive/
  WhenWritingNull/WriteIndented — единый конвейер обоих хендлеров
  (SaveJson.cs:29-37).
- **B5:** SaveStartedEvent ДО CaptureState (SaveService.cs:59);
  TechniqueChargeService:131 / AuraHoldService:55 — отмена с возвратом 50%
  Ци для всех трёх путей вызова; Load без LoadStarted — корректно.
- **Честные Completed-события:** SaveCompletedEvent(ok, LastError)
  (SaveModule:76-78); LoadCompletedEvent(ok) (84-85); QA-зонд 107-111.
- **Adapter-override:** GameBoot RegisterInstance Godot-хендлера до
  Module-дефолта (46-51) — ловушка greediest-ctor избегнута; sanitизация
  путей закрывает traversal (Adapter:148-166).
- **GameSession.LoadGame-порядок:** NPCModule.ResetWorld → _save.Load →
  RunAssembly(LoadGame, SkipOnLoad) — согласован с R14.
- **Register-дедуп:** Aggregator.Contains-гейт (47-51); ResolveAll — по
  ссылке (Container:124-126); SaveModule.Dispose парный (88-93); повторный
  Start недостижим (GameEntryPoint идемпотентен).

---

## Соответствие docs_v2

- Шапка «автосейва нет» vs код — DRIFT (SAV-2); §4.4 таблица 17 SaveKey vs
  8 фактических (SAV-9; шапка «6 ISaveable» тоже устарела) — DRIFT
  (известные пробелы); §6.1 «60 тиков» — OK, но минуты игнорируются.
- §6.2 F5/F9 отключены Q8 — OK (задокументировано); §7.1/§9.1 tmp→rename и
  §9.2 rolling backups vs File.WriteAllText — DRIFT (SAV-1); §8
  версии+миграции — DRIFT (SAV-6); §2.1 main.sav → main.json
  {SaveKey→блок} — OK по сути.

*Аудит Save завершён. Приоритет: SAV-1+SAV-2 (до любой реактивации сейвов)
→ SAV-3/4 санация мёртвых контрактов → SAV-6. Плагин: TRD-1 (валюта не
сейвится, аудит Trade) лечить единым коммитом с QI-2/QST-1.*
