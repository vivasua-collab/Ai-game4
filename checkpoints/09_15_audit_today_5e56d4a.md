# Аудит сегодняшней работы (коммит 5e56d4a) — 2026-09-15

**Метод:** 3 последовательных READ-ONLY аудит-агента (glm-5.3, Explore), разные
мандаты: (1) R17 save-домен, (2) INP-1 + UI/Adapter, (3) Entry/Phases +
QA-раннер + процессная дисциплина. Evidence-based, file:line, сверка с
docs_v2. Аудит после пуша; код на момент аудита == origin/main 5e56d4a.

**Итог: 3 P1 · 7 P2 · 25 P3.** Подозрение пользователя «множественные
нарушения архитектурных правил и указаний» — ПОДТВЕРЖДЕНО частично:
архитектурная слойность в основном соблюдена (IWorldResettable в Core
законно, направление зависимостей верное, GameSession очищен от concrete),
но (а) фикс INP-1 закрыл не все пути того же класса дефекта, (б) docs_v2
рассинхронизирован в 4+ документах, (в) INP-1 закоммичен без QA своего
сценария — что и позволило уцелеть P1-багам ×-кнопок.

---

## P1 — обязательные фиксы (следующий коммит)

### A1. SAV-P1-1: LoadGame не перегенерирует тайловую сетку под локацию сейва
- E-3 выполнен наполовину: `WorldService.RestoreState` восстанавливает
  локацию, но `TileMapGenPhase` (SkipOnLoad=true по умолчанию,
  AbstractSceneAssemblyPhase:53) не генерирует сетку; тайлов нет ни в одном
  из 19 блоков; `TileModule.OnLocationChanged` — no-op; комментарий
  WorldService.cs:314-316 («TileMapGenPhase генерирует сетку правильного
  размера из сида локации») — ЛОЖЕН.
- Последствие: LoadGame сейва из large_world (500×500) → сетка 50×50,
  позиции вне сетки, миникарта/ресурсы чужой локации; warm-load — третья
  конфигурация. Дюп-вектор ресурсов (харвест→save→load).
- Фикс: `TileMapGenPhase.SkipOnLoad => false` + локация из
  `IWorldService.CurrentLocation` (восстановлена ДО RunAssembly).

### A2. INP1-1: «×»-кнопки Q/J/F1/T обходят точку резюма → тот же фриз
- QuestWindow.cs:112-114, EventLogWindow.cs:314-316, HotkeysWindow.cs:36+95,
  TechniqueBookWindow.cs:143+208: `closeBtn.Pressed += () => Visible = false`
  без Closed-события → Time.IsPaused висит → движение мертво до Esc.
  Сценарий «Q → клик × → мир заморожен» == исходный INP-1.
- Фикс: Closed-события у 4 окон (fire в Close/Toggle) + подписка GWC по
  образцу InventoryWindow (GWC:934/:940).

### A3. INP1-2: OnTradeOpened — инвертированное исключение → снапшот всегда
- GWC:2024-2031: `bool otherModalOpen = AnyModalWindowOpen() && _tradeWindow
  is not { IsOpen: true }` — TradeWindow подписан на TradeOpenedEvent РАНЬШЕ
  GWC (EventBus синхронный, порядок подписок) → IsOpen всегда true к моменту
  хендлера → otherModalOpen всегда false → снапшот снимается всегда.
- Последствие: торговля поверх другого паузящего окна → после закрытия
  стека мир заморожен.
- Фикс: предикат «модальное открыто КРОМЕ торговли» (проверка остальных
  8 окон), не «модальное И торговля закрыта».

## P2 — дисциплинарные

- **A4 (SAV-P2-1):** GameSession.LoadGame:162 игнорирует bool от
  `_save.Load()` → битый сейв = играбельная пустая сессия, автосейв затирает
  слот. Фикс: abort в MainMenu при false.
- **A5 (INP2-1):** PageUp/PageDown (GWC:1777-1790) резюмят под открытыми
  окнами без `AnyModalWindowOpen()`-гейта — противоречие инварианту и
  HOTKEYS.md:31. Фикс: гейт (PageUp как аварийный резюм — оставить, но
  гейтить).
- **A6 (P2-1):** tools/qa_regression.sh не отражён в docs_v2 (rg → 0
  упоминаний); TESTING_RULES §0.2 предписывает устаревший `timeout 25`.
- **A7 (P2-2):** docs_v2 рассинхрон: ARCHITECTURE §6.1 (NpcDomainResetPhase
  удалён), §8.4 + TIME_SYSTEM ×4 («60 тиков» vs 30 мин), MODULE_STRUCTURE
  §2.7/§3.2, TESTING_RULES §0.1 («8 блоков» vs 19), SAVE_SYSTEM счётчики
  (20/15 vs 19/16) + внутреннее противоречие интервалов.
- **A8 (P2-3):** INP-1 закоммичен без QA своего сценария (ни один сим не
  открывает/закрывает окна bg-click/×); ручная матрица в чекпоинте
  «ожидаемая»; чекпоинт 09_16 описывает старую версию раннера (timeout -k 5
  150) при закоммиченной watchdog-версии.

## P3 — план (25 шт, выборка)

- Раннер: вердикт `head -1`+подстрока PASS → `tail -1`+точное «VERDICT:
  PASS» (Dialogue/TradeUX печатают 2 вердикт-строки); trap INT TERM;
  PID-recycle окно.
- WorldDomainResetPhase глотает исключения сброса (try/catch per-domain,
  фаза всегда Completed) → маркер ERRORS=N.
- GameSession.LoadGame сброс без per-domain изоляции (симметрия с фазой).
- TechniqueRegistry не чистится на тёплом LoadGame (только WorldInitPhase,
  SkipOnLoad=true); NPCSpawnComposition.Reset() мёртв; world_map 0×0 в
  реестре; EquipmentService.ResetWorld не пере-публикует
  EquipmentChangedEvent; QiService enum без IsDefined + _dailyAccumulator
  теряется; Entry→Adapter WeaponVisualCatalog.ResetCache из
  WorldDomainResetPhase; Adapter→Modules AnimalService конкретный тип в
  2 симах; GameEntryPoint Resolve<WorldService> конкретный (осознанно,
  комментарий есть); PageUp-инвариант; K вне Esc-каскада не задокументирован
  в HOTKEYS; 3 расходящихся ручных списка окон в GWC; E не гейтен
  модальными; DaysSinceStart неточен и мёртв; осиротевший .json.tmp.

## Проверенное и чистое (покрытие)

- R17 ядро: 19 ISaveable-блоков (типизированные SaveState + RestoreState +
  валидация), RestoreOrder зависимости верны, атомарность tmp→rename в
  обоих хендлерах, автосейв (двойной гейт Playing, слот autosave, интервал
  из конфига), хардкоды TestPolygon/06:00 удалены, гонок half-restored
  не найдено, QA-честность (интegrity-мутации реальные).
- Слойность: IWorldResettable — чистый контракт 0 зависимостей; 16
  реализаций; ResolveAll механика надёжна; фаза 0 порядок корректен;
  WT-3 направление исправлено верно (каталог в GameEntryPoint).
- INP-1 ядро: захват otherModalOpen ДО Toggle во всех 6 ветках; хелпер до
  Open на E-путях; идемпотентный резюм; Zero-GC чисто; polling не
  конфликтует; Closed в Toggle — единственная мутация видимости.
- Процесс: чекпоинты на эпизоды ✓; worklog append-only (+73/−0) ✓; QA
  R17 ✓ (нетривиальные ассерты); INP-1 QA — провал (A8).

## Рекомендуемый порядок

1. A1-A5 (кодовые фиксы) + modal-QA-хук (закрывает A8) → build + QA →
   коммит+пуш.
2. A6-A7 (docs sync одним док-коммитом) + чекпоинт 09_16 корректура.
3. P3-пачка раннера (tail -1, trap) в ближайшем touch qa_regression.sh.
