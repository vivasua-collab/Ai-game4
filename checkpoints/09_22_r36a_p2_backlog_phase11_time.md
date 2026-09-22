# Чекпоинт: R36-a — P2-бэклог Фазы 11 (Time): P2-26…30 + P3-11

**Дата:** 2026-09-22 17:0x UTC
**Сессия:** R36-a (продолжение внешнего аудита 09.22 12:00, фазы 11–14)
**Тип:** fix (P2-пачка 1 из 4)

---

## Контекст

R35 закрыл все P1 фаз 11–14. Директива пользователя: P2-бэклог пачками
(P2-26…30 → P2-33…36 → P2-37…42 → P2-47/48/50) → интеграционная контрольная
фаза «Time→NPC→Cultivation→Techniques→Combat→Save→Time resume».
Этот эпизод — пачка 1: Фаза 11 (Time), 5 P2 + попутно P3-11.

## Что сделано

- **P2-26 (автосейв на process-tick):** ITimeService.TickCount (новый
  интерфейсный член — мировые тики, 0 = 06:00 день 1); SaveModule.Tick
  игнорирует process-tick, разностная схема по мировому тику (первый тик
  после старта/сброса НАЧИНАЕТ отсчёт — автосейв ровно через interval
  игровых минут; hitch/bulk не теряет каденцию); SaveModule :
  IWorldResettable (ResetWorld обнуляет отсчёт — WorldDomainReset 32→33
  домена). Первый автосейв нового мира — ровно на 30-м мировом тике.
- **P2-27 (две pause-authority):** GameSession.Pause()/Resume()
  синхронизируют TimeService (SessionState.Paused ⇔ IsPaused — единая
  истина); TimeService._speedBeforePause: Resume() восстанавливает скорость
  ДО паузы (Fast/Quick не уничтожаются в Normal); ResetWorld сбрасывает
  и её (канон NewGame/Load → Normal).
- **P2-28 (DeltaTime при паузе):** Pause() → DeltaTime=0f, Resume()/
  AdvanceTick() → 1f. Контракт: IsPaused ⇒ DeltaTime == 0.
- **P2-29 (три представления времени):** RestoreState канонизирует —
  дата первична; TickCount = TotalMinutes(даты) − 360 пересчитывается
  (сейвным tick/totalTime НЕ доверяем; рассинхрон — предупреждение +
  канонизация); TotalTime = TickCount. Инвариант живого мира:
  TotalMinutes == 360 + TickCount.
- **P2-30 (валидация даты):** 5-компонентный ctor WorldTime валидирует
  (month 1–12, day 1–30, hour 0–23, minute 0–59, year ≥ START_YEAR) →
  ArgumentOutOfRangeException; RestoreState с мусорной датой кидает →
  агрегатор ловит → Load=false → честный отказ загрузки (рекомендация
  аудитора: «отказ загрузки», не «мусорная дата»).
- **P3-11 (конфиг-дрейф):** WorldConfig — удалены мёртвые StartYear/
  StartMonth/StartDay/StartHour/BaseTickRate/AutoSaveIntervalTicks
  (потребителей нет — grep; канон: GameConstants + TimeService + SaveConfig).

## Решения

- RestoreState-throw вместо тихого фолбэка при невалидной дате —
  аудитор явно предпочёл отказ загрузки; агрегатор уже ловит (R17-контракт).
- Первый Tick = инициализация отсчёта, НЕ мгновенный автосейв — иначе
  Load мгновенно перезаписывал бы слот.
- ITimeService расширен (единственная реализация — TimeService, фейков
  не было; в симе — FakeTimeService с TickCount-членом, компилируемым
  в обоих состояниях).
- P3-11 закрыт в этом же эпизоде: поля мёртвые, grep пуст, риск нулевой.

## Найденные проблемы

- Мини-DI S1: Container резолвит IPublisher/ISubscriber ТОЛЬКО через
  registered EventBus (generic-спецкейс до поиска регистраций) — фейки
  pub/sub в мини-контейнере бесполезны; решено регистрацией реального
  EventBus.
- IGameSession/ISaveFileHandler-фейки: сигнатуры интерфейса отличаются
  от Ai-game3-наследия (LoadGame(SaveSlot), QuitWithoutSaving,
  OnStateChanged, GetAllSaves) — скопированы из живого FakeGameSession.

## Следующие шаги

- Пачка 2: P2-33…36 (Фаза 12 — Inventory/Trade: overflow валют/сделок,
  restore-валидация инвентаря, 50/100 vs канон 30/30).
- Пачка 3: P2-37…42 (Фаза 13 — Quest/NPC/награды).
- Пачка 4: P2-47/48/50 (Фаза 14 — Techniques/Player).
- Затем интеграционная контрольная фаза (маршрут аудитора).

## Пруфы (prefix FAIL / postfix PASS, логи в git)

- Prefix: checkpoints/logs/09_22_r36a_prefix_audit0922.log — 12 S-DEFECT
  (S2b OK и в старом коде — не гейтит), VERDICT FAIL.
- Postfix: checkpoints/logs/09_22_r36a_postfix_audit0922.log — 88 OK /
  0 DEFECT, VERDICT PASS.
- Build 0 errors; полная QA-регрессия 19/19 PASS.
- Доки: TIME_SYSTEM §4.3-примечание + §4.4 (P2-26…30+P3-11 контракты),
  TESTING_RULES §0.1 (сим №19 — секция S).

## Файлы

- game/src/Core/Interfaces/ITimeService.cs (TickCount + контракты)
- game/src/Core/Data/Structs.cs (WorldTime-валидация)
- game/src/Modules/World/WorldService.cs (TimeService: пауза/скорость/
  канонизация; WorldConfig — P3-11)
- game/src/Entry/GameSession.cs (Pause/Resume синхронизация)
- game/src/Modules/Save/SaveModule.cs (мировая каденция + IWorldResettable)
- game/src/Adapter/Scene/GameBoot.cs (комментарий process-tick)
- game/src/Adapter/Scene/Audit0922SimDebug.cs (секция S: S1a-d, S2a-b,
  S3, S4, S5, S6a-b, S7 + мини-DI)
- docs/docs_v2/03_world/TIME_SYSTEM.md, 09_workflow/TESTING_RULES.md
