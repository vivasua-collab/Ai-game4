# АУДИТ: Trade (лавка, транзакции, валюта)

**Дата:** 2026-09-11. **HEAD:** `d0b065d` (READ-ONLY, код не менялся)
**Scope:** Modules/Trade/ — все 5 файлов полностью (~0.8k строк): TradeService
(396), CurrencyService (78), TradeConfig, TradeModule (156),
TradeModuleServices. Стыки: MerchantStockEntry, TradeContracts, ITradeService/
ICurrencyService, TradeWindow (618, полн.), GWC (Trade-пауза/резюм 1991-2009,
Esc 1527-1530, модальность E 1684-1687), CorpseService (осколки из трупов),
ClassicLootSeeder (Value), ItemGeneratorService (ItemId), InventoryService.
HowManyCanFit, docs_v2/06_player/TRADE_SYSTEM.md.

---

## Сводка

Транзакционное ядро чисто: TryBuy (Spend→TryAddItem→откат Add(total)),
TrySell (TryRemoveItem→Add), идемпотентный OpenTrade, валидация торговца,
Permil-цены, FNV-1a-сид, модальность лавки с единой точкой резюма
(TradeClosedEvent). Главная находка — **P1-латент: валютный кошелёк не
сохраняется** (не ISaveable, SetBalance «для загрузки сейва» — 0 вызовов):
при Load инвентарь восстановится, а баланс = дефолтные 50 → дюп/потеря
камней; сейчас скрыто Q8 (F5/F9 отключены), но автосейв уже пишет файлы и
при реактивации сейвов дюп материализуется. Находок: P1×1 (латентно), P2×1,
P3×4, OK-BY-DESIGN×2.

---

## Находки

### TRD-1 [P1-латентно] Духовные камни НЕ сохраняются — дюп/потеря валюты при Load

**Файл:** `CurrencyService.cs:17-76` (класс без ISaveable; SetBalance:69 —
0 вызывов по game/src); `SaveModule.cs:96-114` (нет Register<ISaveable,
CurrencyService>); `TradeModuleServices.cs:19`.
**Сценарий:** игрок покупает на 30 камней (50→20), сейвится (inventory-блок
восстановится), лоадится → ленивая инициализация `_spiritStones =
StartStones` (40) → **предметы на месте И камни снова 50**. Обратный
случай (продажа→сейв→лоад) теряет заработок. SetBalance с комментарием «для
загрузки сейва» — API написан, в агрегатор подключить забыли (SAVE-A1 собирал
только зарегистрированные). Латентность: F5/F9 — тост (GWC:1732-1735),
MainMenu ищет только quicksave (MainMenuController.cs:222), который никто
не пишет; но автосейв пишет autosave_NNNN (см. SAV-2), QA-путь жив, реактивация
сейвов активирует дюп немедленно.
**Фикс:** `CurrencyService : ISaveable` (SaveKey "currency",
CaptureState/RestoreState→SetBalance) + Register<ISaveable, CurrencyService>
в TradeModuleServices — паттерном Body/Inventory; в связке решить судьбу
предметных камней (TRD-8).

### TRD-2 [P2] Сток торговца не персистентен — «ресток из воздуха» при Load

**Файл:** `TradeService.cs:25-28, 48, 114-122`.
**Сценарий:** `_stockByMerchant` — сессионный Dictionary; TradeService не
ISaveable → после Load лавка регенерируется тем же сидом FNV-1a(npcId):
состав тот же, но **выкупленное восстанавливается** (RestockTimestamp/
InitialCount не задействованы). Цикл купил-всё→сейв→лоад = бесконечная
закупка; проданное игроком в сток не попадает (исчезает из экономики).
Класс-комментарий честен («в рамках сессии»), но сейв-граница не оговорена
нигде, TRADE_SYSTEM.md молчит. **Фикс:** блок "merchant_stock" в сейв
({npcId, itemId, count}) или фиксация поведения в доке §3.

### TRD-3 [P3] FindEntry — первый матч: остатки дублей ItemId недоступны

**Файл:** `TradeService.cs:360-368, 179-184`; `ItemGeneratorService.cs:77, 139`.
**Сценарий:** материалы добавляются циклом StockMaterialCount раз с одним
ItemId; ItemId оружия/брони = `weapon_{level}_{seed%1000}` — коллизия двух
позиций даст одинаковые ItemId. FindEntry вернёт первый entry → после его
выкупа «Товара нет в наличии», хотя вторая UI-строка показывает остаток.
Сейчас не стреляет (StockMaterialCount=1 — TradeConfig:44; сиды разнесены
7919·k mod 1000 ≠ 0 при k≤2), но конфиг StockMaterialCount>1 /
StockWeaponMax>3 активирует. **Фикс:** суммировать available по всем entry.

### TRD-4 [P3] TradeFailedEvent из служебных веток — шум в UI

**Файл:** `TradeService.cs:234-238, 302-306`; `TradeWindow.cs:320-325`.
TryBuy/TrySell при `!IsTrading` публикуют TradeFailedEvent («Торговля не
открыта») → тост «⚠» из внутреннего инварианта, не ошибки игрока.
Переключение торговца (75) даёт всплеск Closed/Opened в одном кадре —
безвредно, лог замусоривается. **Фикс:** служебные отказы без Publish.

### TRD-5 [P3] GetMerchantStock — shallow-снапшот: mutable entries в руках UI

**Файл:** `TradeService.cs:106-112`; `TradeWindow.cs:368-392`. Копируется
List, элементы — те же MerchantStockEntry (public-поля). Контракт «UI не
мутирует» держится на дисциплине. **Фикс:** глубокая копия/readonly-обёртка.

### TRD-6 [P3/Doc] TRADE_SYSTEM §8.4 «вес/объём покупки не реализовано»

**Файл:** `docs_v2/06_player/TRADE_SYSTEM.md:95`; `TradeService.cs:259-265`;
`InventoryService.cs:436-448`. Объём РЕАЛИЗОВАН (HowManyCanFit режет buyCount;
комм. «Weight NOT checked — overflow allowed»); не реализован только вес.
§5 «вкладки» — фактически две колонки (TradeWindow 193-259). **Фикс:**
§8.4 → «вес не реализован, объём есть», §5 → «колонки».

### TRD-7 [OK-BY-DESIGN] Камни — int (не long), цены — Permil

**Доказательство:** `CurrencyService.cs:3` «Баланс — int (ЗАПРЕТ 3.9)»;
`TradeConfig.cs:8`; CurrencyChangedEvent — int (PlayerContracts.cs:85).
Замороженное решение проекта. Переполнение int теоретично (>2.1e9) — вне
реализма V1.

### TRD-8 [OK-BY-DESIGN] Кошелёк ≠ предметные камни: два представления

**Файл:** `CurrencyService.cs:22` (кошелёк int); `ClassicLootSeeder.cs:65-95`
(spirit_stone_shard Value=12 / fragment Value=30 — ПРЕДМЕТЫ инвентаря,
ItemType "currency"); `CorpseService.cs:348-356` (трупы зверей дают
осколки-предметы через ItemAddRequestEvent). Обмен единственный: продажа
осколка = Permil(12, 500) = 6 камней в кошелёк. Согласовано с TRADE_SYSTEM
§8.1 («курс/конвертация не специфицированы»). Не дюп; учесть при фиксе
TRD-1 (предметные камни уже в inventory-блоке).

### TRD-9 [P3] Валидация «только Merchant»: диспозиция как запасной вход

**Файл:** `TradeService.cs:78-85`. Disposition-ветка (`Role != Merchant &&
Disposition != Merchant`) допускает торговлю с НЕ-торговцем с
Merchant-диспозицией — в текущем спавне таких нет ([UNVERIFIED] на будущие
пресеты). Отметить при появлении таких NPC.

---

## Проверено чисто

- **TryBuy (своя сторона):** строгая проверка `_currency.SpiritStones <
  total` ДО Spend (271-275); откат `_currency.Add(total)` при провале
  TryAddItem (283-289); canFit-предрезка до списания (259-265);
  entry.Count -= buyCount только после успеха (291). Подтверждение
  Inventory-аудита.
- **TrySell:** снимает min(count, have); Add(total) после изъятия — обратный
  откат не нужен (Add не падает); TradeCompletedEvent с честным sellCount.
- **Цены:** GetBuyPrice = max(1, Permil(Value, 1200)); GetSellPrice =
  max(0, Permil(Value, 500)) — согласовано с TRADE §4; неизвестный itemId
  гейтится TryGetItem (241-245, 309-313).
- **Идемпотентность OpenTrade:** тот же npcId — no-op без повторного
  TradeOpenedEvent (72); смена торговца — корректное Close→Open.
- **Модальность лавки:** TradeOpenedEvent → Time.Pause (GWC 1991-1997);
  TradeClosedEvent → единая точка резюма (2004-2009, паттерн LootWindow
  подтверждён); Esc → Close() → CloseTrade() → событие (1527-1530,
  TradeWindow 301-307); E при лавке съедается (1684-1687); клик по
  подложке не закрывает (TradeWindow 143).
- **Сид:** FNV-1a 32-bit, ненулевой гейт (380-393) — одинаковый npcId →
  одинаковая лавка; дочерние сиды разнесены.
- **EnsureMaterialItem:** регистрация material_iron_ore только при
  отсутствии (204-227) — не перетирает БД.
- **TradeWindow:** подписки парные с _ExitTree-dispose (117-125);
  RefreshAll по Completed/Failed/CurrencyChanged — обновление без
  переоткрытия; QA-хуки изолированы в TradeModule.Tick. Мост
  TradeRequestedEvent→OpenTrade — единственный паблишер
  DialogueService.cs:184; подписка парная (45-46, 80-82).

---

## Соответствие docs_v2

| Пункт TRADE_SYSTEM.md | Код | Статус |
|---|---|---|
| §1 int/ЗАПРЕТ 3.9, старт 50 | CurrencyService/TradeConfig | OK |
| §2 контракт-таблица, порядок EndDialogue→TradeRequested | TradeContracts, DialogueService:176-184 | OK |
| §3 ассортимент 1-2/1-2/3-4/1, FNV-1a | TradeConfig, GenerateStock | OK |
| §4 Permil 1200/500 | GetBuyPrice/GetSellPrice | OK |
| §5 «вкладки» | две колонки | DRIFT minor (TRD-6) |
| §8.4 «вес/объём не реализовано» | объём есть, веса нет | DRIFT (TRD-6) |
| §8.1 курс предметных камней | подтверждено (TRD-8) | OK (открытый) |
| — (не отражено) | валюта не в сейве (TRD-1), сток не в сейве (TRD-2) | GAP в доке |

*Аудит Trade завершён. Приоритет: TRD-1 (единым сейв-эпизодом с QI-2/QST-1)
→ TRD-2 → P3. Всё с file:line в checkpoints/09_11_audit_trade.md.*
