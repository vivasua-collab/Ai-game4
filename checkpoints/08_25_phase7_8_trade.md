# Чекпоинт: Phase 7+8 + P0-фиксы + Phase 4-5 Торговля

**Дата:** 2026-08-25 15:00 UTC (сессия ~12:50–15:00 UTC, ≈2 ч 10 мин)
**Сессия:** Облачный агент (Z.ai Code sandbox), cold start с нуля
**Тип:** implementation + fix
**Объём:** 4 коммита (3 кодовых + 1 wrap-up), 33 файла, ~2 790 insertions / ~669 deletions, из них чистый код ≈2 330 строк в 29 файлах. Добавлен 17-й модуль (Trade), 11 новых файлов кода, исправлены 2 критических P0-бага.

> РАСШИРЕННАЯ РЕДАКЦИЯ (2026-08-25, вечер): первоначальная версия чекпоинта
> была слишком краткой относительно выполненного объёма работ. Ниже — полная
> запись сессии: хронология, разбор P0-багов до уровня строк кода, пофайловая
> детализация фаз, методология и результаты верификации, решения с обоснованием.
> Факты сверены с git-историей (679f19e, f02d61d, 8a5001b, 418ce7a) и worklog.

---

## Контекст

Сессия начата с чистой песочницы (передан только токен + URL репо).
HEAD на старте: f26a33d «Qi stages 6-8».

**Планировалось** (по NPC_COMBAT_PREP.md и SESSION_CONTEXT):
- P0-проверка прототипа боя (живой запуск, поиск критики)
- P1-фазы NPC_COMBAT_PREP

**Фактически выполнено** (план расширен по ходу, признано пользователем
как «много несогласованных задач» — см. Правило процесса ниже):
1. Холодный старт + инфраструктурные открытия (import текстур в облаке)
2. P0-БАГ №1: урон NPC→игрок никогда не применялся (найден, исправлен)
3. P0-БАГ №2: таймеры в 60× медленнее (найден, исправлен)
4. Phase 8: wiring боевых статов экипировки (5 TODO закрыто)
5. Phase 7: Combat Visuals (цифры урона + HP-бары NPC)
6. Phase 4-5: Торговля (17-й модуль, полный флоу лавки)
7. Wrap-up: чекпоинт + SESSION_CONTEXT/SESSION_SUMMARY + worklog

**Правило процесса (введено пользователем после сессии):** все будущие
работы — по схеме «план → подтверждение пользователя → правка кода
с чекпоинтом». Чекпоинты = ИСТОРИЯ разработки, они должны быть полными.
Этот чекпоинт расширен ретроактивно как первый шаг наведения порядка.

---

## Хронология сессии (UTC)

| Время | Событие |
|-------|---------|
| ~12:49 | Загрузка песочницы, клонирование репо (HEAD f26a33d) |
| ~12:50–13:05 | Холодный старт: .NET SDK 8.0.424, Godot 4.7.1 mono, NuGet.config, сборка |
| ~13:05–13:30 | Анализ планов; планирование Phase 8; изучение TODO в CombatService |
| 13:42 | **Коммит 679f19e** — Phase 8 + 2 P0-фикса + CombatSimDebug (9 файлов, +543/−44) |
| ~13:42–13:54 | Phase 7: DamageNumberRenderer, HP-бары, Xvfb+VLM верификация |
| 13:54 | **Коммит f02d61d** — Phase 7 Combat Visuals (7 файлов, +258/−2, 2 скриншота) |
| ~13:54–14:16 | Phase 4-5: делегирование субагенту Trade-системы, ревью, wiring |
| 14:16 | **Коммит 8a5001b** — Phase 4-5 Торговля (13 файлов, +1528/−13) |
| ~14:16–14:19 | Wrap-up: чекпоинт, SESSION_CONTEXT/SESSION_SUMMARY, worklog |
| 14:19 | **Коммит 418ce7a** — Session wrap-up (4 файла, +464/−610) |
| ~15:00 | Финальный regression-прогон, пуш на GitHub |

---

## 1. Холодный старт (улучшен относительно прошлых сессий)

**Восстановление окружения с нуля:**
- .NET SDK **8.0.424** в ~/.dotnet (8.0.30 недоступен на feed — EOL;
  GodotSharp editor plugin ругается «required 8.0.30», но это НЕ фатально:
  import и runtime работают, сборка идёт через dotnet build)
- Godot 4.7.1 mono в ~/godot/…, симлинк `godot` для скриптов
- game/NuGet.config (локальный, gitignored) — офлайн-резолв пакетов
- dotnet build: 0 errors (238 warnings — все ранее существовавшие)
- Канонический путь: /home/z/my-project/aigame4 (симлинк Ai-game4)

**ОТКРЫТИЕ №1 (инфраструктурное, главное за сессию):**
`godot --headless --import --path <АБСОЛЮТНЫЙ_ПУТЬ>` генерирует
.godot/imported/*.ctex (150 файлов) — **биом-текстуры теперь рендерятся
в cloud sandbox**. Ранее считалось «нужен Godot Editor» (нерешаемо).
Побочный бонус: генерируется uid_cache.bin → main scene резолвится
при обычном запуске (раньше требовался явный путь сцены).
Проявилось в логе как «Drew 144 textures, 0 missing».

**Грабли:** `--import` с `--path .` из чужого cwd молча берёт не тот проект
(«no main scene defined»). Всегда абсолютный путь.

**ОТКРЫТИЕ №2:** скриншоты работающего рендера можно снимать в облаке:
Xvfb + opengl3 (не headless!) + GODOT_SCREENSHOT_DELAY + VLM-анализ кадра.
Это заменило «живую проверку в редакторе» для визуальных фич Phase 7/5.

---

## 2. P0-БАГ №1: урон NPC→игрок никогда не применялся

**Симптом:** в бою NPC→игрок тост «💥 −X HP» показывается, HP игрока
не падает, смерть игрока недостижима. Прототип боя «выглядел рабочим»
на logs, но был односторонним.

**Root cause (2 ID одного игрока):**
- BodyService.OnDamageApplied фильтровал события по `e.TargetId == _entityId`,
  где _entityId = **"player"** (ID под которым BodyService создаётся)
- NPC AI атакует игрока как **"player_0"** (NPCAIService.PlayerId — ID спавна)
- Прецедент нормализации уже был в коде: PlayerService:178 (там ту же пару
  ID сводили вручную), но в BodyService копию паттерна не внесли

**Фикс (BodyService.cs:455,474):**
```csharp
if (e.TargetId == _entityId || (IsPlayerEntityId(e.TargetId) && IsPlayerEntityId(_entityId)))
private static bool IsPlayerEntityId(string? id) => id == "player" || id == "player_0";
```

**Верификация:** GODOT_COMBAT_SIM — npc→player 21 dmg, HP 500→486
(ранее HP оставался 500). Двунаправленный бой заработал впервые.

**Урок:** паттерн DualPlayerId («player» vs «player_0») — сквозная мина;
нормализацию надо централизовать (занесено в SESSION_CONTEXT warnings
и в «Следующие шаги» как техдолг).

---

## 3. P0-БАГ №2: V1-плейсхолдер времени (все таймеры в 60× медленнее)

**Симптом:** каст 0.5 с разрешался ~30 реальных секунд; NPC почти не
двигались; медитация давала копейки; «урон не проходил» (на деле — просто
медленно). Симптомы маскировались под баг №1 и «несбалансированность».

**Root cause:** WorldService.DeltaTime = 1/60 на тик — плейсхолдер V1,
оставшийся с прототипа. Все сервисы умножали свои скорости на DeltaTime,
получая 1/60 реального масштаба.

**Фикс:**
- WorldService.DeltaTime = **1.0/тик** (TIME_SYSTEM.md §5-6: 1 тик =
  1 игроминута = 1 реальная секунда на Normal)
- QiRegenCalculator.SECONDS_PER_DAY 86400 → **1440** (игро-сутки =
  1440 тиков; пассивная регенерация теперь точно 10%/сутки по QI_SYSTEM.md §4)

**Верификация:** COMBAT_SIM PASS с реалистичными таймингами; регенерация
по доке (юнит-расчёт сходится).

**Урок:** единица времени — тик; «реальные секунды» в Core-коде запрещены
(см. ниже «Найденные проблемы» — CombatConsequencesService TICK_SECONDS=3.0
всё ещё отклонение).

---

## 4. Phase 8: wiring боевых статов экипировки (закрыто 5 TODO в CombatService)

**Проблема:** экипировка генерировалась (Матрёшка), надевалась, но на бой
не влияла вообще: уклонение/блок/парирование/крит/пробитие считались
от нулевых бонусов, базовая атака всегда была «кулак 10» независимо
от оружия.

**Что сделано (коммит 679f19e, 9 файлов, +543/−44):**

1. **IEquipmentDataProvider** (+47 строк): 5 новых агрегатов —
   GetDodgeBonusPermil / GetBlockBonusPermil / GetParryBonusPermil /
   GetCritBonusPermil / GetWeaponPenetration + SetEquipmentData
2. **EquipmentDataProvider** (+196/−? в Modules/Inventory): резолв
   itemID → EquipmentData через **IItemDatabaseService** (закрыт старый
   TODO из комментария класса), прямой кэш экипировки игрока
   (SetEquipmentData), EnumerateEquipment для агрегатов
3. **EquipmentService**: SyncToProvider после КАЖДОГО equip/unequip,
   синхронизация под обоими ID игрока («player» и «player_0» — см. баг №1)
4. **CombatService** — закрыты все 5 TODO:
   - armorDodgePenalty ← броня ЗАЩИТНИКА (тяжёлая броня мешает уклонению)
   - shieldBlock ← StatBonus blockChance щита; weaponParryBonus ← parryChance оружия
   - techniqueCritBonus ← critChance экипировки
   - penetration += weapon.Penetration (промилле)
   - базовая атака с оружием = урон ОРУЖИЯ (раньше кулак всегда 10 —
     оружие не имело смысла)
   - isPlayerAttacker вместо строковой проверки "player" (guard от двойного
     счёта игрока в статистике боя)
5. **CombatSimDebug.cs** (новый, 214 строк, GODOT_COMBAT_SIM=1):
   headless-верификация боевого конвейера в обе стороны + weapon
   end-to-end; VERDICT PASS/FAIL в лог

**Не баг, а дизайн:** «Cultivator cap=0» — Cultivation пассивна
(TECHNIQUE_SYSTEM.md §11.5: capacity=null, qiCost=0) — закрыт дизайном
ещё в Qi-этапах, не трогали.

**Верификация (числа из COMBAT_SIM):**
- экипированный NPC: pen=4, dmg=15 (голый NPC слабее)
- npc→player: 21 dmg, HP 500→486 (фикс бага №1 подтверждён на этих числах)
- игрок с посохом: 14 RedHP против 10 кулаком (Phase 8 wiring работает)

---

## 5. Phase 7: Combat Visuals (коммит f02d61d, 7 файлов, +258/−2)

**Проблема:** бой был «невидимым» — урон только в логах/тостах, состояние
NPC не читалось визуально.

**Что сделано:**

1. **DamageNumberRenderer.cs** (новый, 178 строк, Adapter/Scene):
   всплывающий боевой текст над целями
   - Hit → красное «−N» (урон игроку — ярче-красный), CriticalHit →
     золотое «КРИТ −N», Dodge/Parry/Block → серые слова
   - всплытие 26px + квадратичное затухание 0.9 с
   - пул структур + Node2D._Draw + DrawString(ThemeDB.FallbackFont) —
     без Label-аллокаций; максимум 48 одновременных (анти-спам)
   - ZIndex = RenderLayer.Objects+3
   - **АРХИТЕКТУРНЫЙ УРОК:** подписка на DamageAppliedEvent — строго
     в `_Ready` ПОСЛЕ DI-инъекции (_EnterTree срабатывает РАНЬШЕ инъекции
     полей → NRE). Занесено в SESSION_CONTEXT как правило.
2. **NPCSpriteRenderer.cs** (+43): HP-бар 48×5 над раненым NPC
   (полный HP → чистый спрайт); зелёный >50% / жёлтый >25% / красный ниже
   (семантика синхронизирована с BodyStatusPanel); IBodyDataProvider.
3. **SceneBuilder.SetupDamageNumbers()** — монтаж рендерера в _worldRoot.
4. **GameBoot.cs**: env GODOT_SCREENSHOT_DELAY (по умолчанию 2 с) —
   задержка перед скриншотом для кадров боя.
5. **CombatSimDebug.cs** (+16): телепорт враждебного NPC к игроку
   (в кадр камеры) вместо перемещения логической позиции игрока —
   камера следует за визуальной позицией контроллера.

**Визуальная верификация (Xvfb + opengl3 + VLM-анализ кадров):**
- зелёный HP-бар с обводкой над враждебным NPC: ПОДТВЕРЖДЕНО
- красное «−10» всплывает над NPC: ПОДТВЕРЖДЕНО
- биом-текстуры рендерятся («Drew 144 textures, 0 missing»)
- скриншоты: docs/screenshots/gameworld_v0.6_combat_visuals.png,
  gameworld_v0.6_world_hud.png

---

## 6. Phase 4-5: Торговля (коммит 8a5001b, 13 файлов, +1528/−13)

**Проблема:** NPC-торговец имел роль/диспозицию, но не имел функции:
диалог без магазина, валюты не существовало, лут-экономика замкнута
на себе.

**Архитектура (17-й модуль, зарегистрирован в GameLifetimeScope после
Interaction, до UI):**

Core (контракты):
- **TradeContracts.cs** (100): 5 readonly-struct событий —
  TradeRequested / TradeOpened / TradeClosed / TradeCompleted / TradeFailed
- **ITradeService.cs** (70): OpenTrade/CloseTrade/TryBuy/TrySell/GetStock
- **MerchantStockEntry.cs** (36): позиция ассортимента (itemID, цена,
  количество, категория)
- CurrencyChangedEvent уже существовал в PlayerContracts — переиспользован

Modules/Trade:
- **TradeConfig.cs** (58): наценки в промилле — MarkupPermil=1200 (×1.2),
  SellPermil=500 (×0.5), StartStones=50 (ЗАПРЕТ 3.9: int-математика)
- **CurrencyService.cs** (77): первая реализация ICurrencyService;
  старт 50 духовных камней; публикует CurrencyChangedEvent
- **TradeService.cs** (395): ассортимент торговца от сида **FNV-1a(npcId)**
  (string.GetHashCode рандомизирован per-process — не годится для сидов);
  состав: 1-2 оружия + 1-2 брони (EquipmentGenerator «Матрёшка» L1-3)
  + 3-4 расходника (ItemGeneratorService, нечётные → «Пилюля Ци») + 1
  материал; ленивая идемпотентная генерация; цены через Permil.Apply;
  TryBuy режет партию по остатку лавки и HowManyCanFit, при полном
  провале TryAddItem — полный возврат камней
- **TradeModule.cs** (155): подписка TradeRequestedEvent → OpenTrade;
  **GODOT_TRADE_DEBUG=1** — smoke-тест на первом тике (открыть → купить
  дешёвое → продать обратно → закрыть, всё в лог); **GODOT_TRADE_HOLD=1** —
  держать лавку открытой для скриншотов
- **TradeModuleServices.cs** (30): DI-регистрации

Adapter/UI:
- **TradeWindow.cs** (511): модальное окно «Лавка торговца» 900×600,
  ParchmentTheme; шапка с балансом «Духовные камни: N»; панели
  «Товары»/«Инвентарь»; строки с ценой, цвет имени по редкости;
  ЛКМ = купить/продать 1 (Shift+ЛКМ = 5); Esc — закрыть; подписки на
  5 событий, все токены диспозятся в _ExitTree; тосты через ToastShownEvent

Wiring:
- **DialogueService.cs**: выбор «Покажи товары» → sentinel-узел
  TargetNodeId=="open_trade" → **EndDialogue() СНАЧАЛА**, публикация
  TradeRequestedEvent ПОТОМ (обратный порядок ломал паузу:
  OnDialogueEnded резюмил бы тики поверх остановленной лавки).
  Отклонение от изначальной спецификации субагента, задокументировано
  в коде. Старый узел show_goods удалён.
- **GameWorldController.cs** (+74): _tradeWindow в SetupHUD; пауза/резюм
  тиков по TradeOpened/ClosedEvent (единая точка резюма — как
  DialogueEndedEvent); modalOpen-гард в _UnhandledInput; Esc-ветка первой
  в HandleStickyInput; E съедается при открытой лавке; SetOverUI(инвентарь
  ИЛИ лавка); диспоз токенов в _ExitTree

**Верификация:**
- dotnet build 0 ошибок (255 warnings — все ранее существовавшие,
  от новых файлов 0)
- GODOT_NEWGAME=1: 19 startables / 18 tickables, [TradeModule] Started,
  все фазы сборки, без исключений
- GODOT_TRADE_DEBUG=1: лавка npc_e273b1494b80439e (Николай): stock
  9 позиций (2 оруж + 2 брони + 4 расходника + 1 материал); TryBuy
  «Пилюля Ци ур.1» за 4 камня (50→46) True; TrySell обратно за 2
  (46→48) True; CloseTrade → тики возобновлены
- Совместный прогон COMBAT_SIM + TRADE_DEBUG: PASS (не сломали бой)
- Визуальная проверка окна (Xvfb+VLM): баланс 46, «Товары (9 поз. /
  22 шт.)» + «Инвентарь (22 поз.)», читаемость OK, дефектов нет
- Скриншот: docs/screenshots/trade_window_v0.6.png

---

## Решения (с обоснованием)

1. **DeltaTime=1.0/тик, а не «реальные секунды»** — время как ресурс:
   на Fast/Quick реальная длительность сжимается, количество событий
   на игро-сутки не меняется (дока TIME_SYSTEM). Альтернатива (rtc-секунды)
   сломала бы медитацию/регенерацию при смене скорости игры.
2. **EquipmentDataProvider с IItemDatabaseService** — Core-интерфейс,
   цикла модулей нет; порядок DI-регистраций не важен (резолв после
   билда контейнера). Прямая инъекция IDataBase в Combat запрещена
   архитектурой — провайдер-адаптер корректен.
3. **Подписки Godot-нод на EventBus — ТОЛЬКО в _Ready** (после
   ContainerAdapter) — _EnterTree раньше инъекции полей → NRE.
   Правило зафиксировано в SESSION_CONTEXT.
4. **Диалог торговца: sentinel-узел "open_trade"** вместо расширения
   модели DialogueNode экшенами — минимальное вторжение в готовую
   систему диалогов; sentinel виден только в данных конкретного NPC.
5. **Сид FNV-1a(npcId)** вместо string.GetHashCode — GetHashCode
   рандомизирован per-process (NET core), ассортимент «мигал» бы
   между запусками; FNV-1a стабилен между процессами и платформами.
6. **Порядок EndDialogue → TradeRequestedEvent** — резюм тиков диалога
   и пауза лавки должны идти строго в этом порядке, иначе лавка
   открывалась бы поверх идущих тиков.
7. **Permil-цены (1200/500)** — покупка/продажа через int-математику
   (ЗАПРЕТ 3.9), никаких float-денег; маржа торговца = источник
   «утечки» камней из экономики (слив 50% при продаже осознан).

---

## Найденные проблемы (техдолг, НЕ исправлены в сессии)

1. **Pending technique глобален на бой** (`_pendingTechnique` в
   CombatService — единственный на весь бой): при смене цели в полёте
   NPC может ударить себя (в логе COMBAT_SIM был кейс npc→npc: 21).
   Нужен per-attacker pending. *Важно: перекликается с запросом
   пользователя на «задержку срабатывания техник» — правильный момент
   рефакторить вместе (см. checkpoints/08_25_technique_hold_analysis.md).*
2. **CombatConsequencesService TICK_SECONDS=3.0** — ещё одна единица
   времени в коде (длительности баффов теперь «9 тиков» вместо «3
   секунд» — приемлемо, но консистентность хромает).
3. **BiomeTiles/рундуки логируют каждый кадр** — спам в лог, режет
   читаемость COMBAT_SIM.
4. **StatProvider возвращает статы игрока для ЛЮБОГО неизвестного ID**
   (fallback на дефолт) — маскирует ошибки резолва сущностей.
5. **DualPlayerId («player» / «player_0»)** — паттерн размазан по коду
   копипастой (PlayerService:178, BodyService:455 после фикса);
   нужна централизованная нормализация.

---

## Следующие шаги (приоритеты на следующую сессию)

1. **P0 (живьём в редакторе):** страж-союзник вступается; слоты пояса
   end-to-end; лут-дроп + подбор E — headless проверено, нужен ручной
   прогон в Godot Editor
2. **Запрос пользователя (2026-08-25 17:20):** анализ «задержки
   срабатывания техник» — ВЫПОЛНЕН отдельным документом
   `checkpoints/08_25_technique_hold_analysis.md`; ждёт подтверждения
   плана перед кодом (новое правило процесса)
3. **Phase 3:** Faction port (Ai-game3-ref → Modules/World, ~400 LOC)
4. **Phase 8 остаток:** isRanged → CombatSubtype (CombatService:300 TODO);
   ammo/bow логика (Phase 8 weapon variety часть 2)
5. **Refactor:** per-attacker pending technique; консистентность единиц
   времени (TICK_SECONDS против DeltaTime); централизация DualPlayerId
6. UI_DESIGN #22: Tooltip/ContextMenu для предметов — не начаты
7. **Документация:** обновление доков до «сверено с кодом» (5.2 → 5.3)
   — оценка в 08_25_technique_hold_analysis.md §9

---

## Статистика сессии

| Коммит | Описание | Файлов | +/− |
|--------|----------|--------|-----|
| 679f19e | Phase 8 + P0-фиксы + CombatSimDebug | 9 | +543/−44 |
| f02d61d | Phase 7 Combat Visuals | 7 | +258/−2 |
| 8a5001b | Phase 4-5 Торговля | 13 | +1528/−13 |
| 418ce7a | Wrap-up (чекпоинт + контексты) | 4 | +464/−610 |
| **Итого** | | **33** | **≈+2793/−669** |

Новые файлы кода (11): CombatSimDebug, DamageNumberRenderer, TradeWindow,
TradeContracts, ITradeService, MerchantStockEntry, TradeConfig,
CurrencyService, TradeService, TradeModule, TradeModuleServices.

Изменённые: IEquipmentDataProvider, EquipmentDataProvider, EquipmentService,
CombatService, BodyService, WorldService, QiRegenCalculator,
NPCSpriteRenderer, SceneBuilder, GameWorldController, GameBoot,
DialogueService, GameLifetimeScope.

Верификационные хуки сессии: GODOT_COMBAT_SIM / GODOT_TRADE_DEBUG /
GODOT_TRADE_HOLD / GODOT_SCREENSHOT_DELAY (+ существующие GODOT_NEWGAME).

---

## Коммиты

- 679f19e — Phase 8 + P0-фиксы + CombatSimDebug
- f02d61d — Phase 7 Combat Visuals
- 8a5001b — Phase 4-5 Торговля
- 418ce7a — Session wrap-up (первоначальная, краткая версия чекпоинта)
- *(расширенная редакция чекпоинта — отдельным коммитом 2026-08-25 вечер)*
