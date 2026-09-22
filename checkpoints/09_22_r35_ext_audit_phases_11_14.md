# R35 — внешний аудит 09.22 12:00 (upload/audit_09_22_12_00): фазы 11–14

> План эпизода (создан ДО правок — правило пользователя с R20).
> Базлайн: R34 `3caea0c7b277e1493c883584e3cabc26aa3973a6` (main).
> Файл аудита: `upload/audit_09_22_12_00` (1774 строки) — верификация R34
> (принята, 3 P1 закрыты, P2-22 открыт — подтверждено) + фазы 11–14.

## §0. Что сказал верификатор про R34 (претензия к артефактам)

- Prefix/postfix runtime-логи были заявлены в чекпоинте, но НЕ закоммичены
  в git-tree R34 → runtime-часть подтверждена «на уровне harness + заявление».
- **Директива этого эпизода: логи prefix/postfix ОБЯЗАТЕЛЬНО в
  `checkpoints/logs/` и в коммите.**

## §1. Реестр фаз 11–14 (новые находки)

### Фаза 11 — Time / Calendar
| ID | Суть | Мой статический вердикт |
|----|------|--------------------------|
| P1-13 | GameBoot: cap 8 тиков/кадр + молчаливый сброс backlog → при Quick hitch ≥0.53с игровое время теряется | ПОДТВЕРЖДЁН (GameBoot.cs:162-177) |
| P1-14 | TimeService.ResetWorld() не сбрасывает _speed; WorldModule.Start ставит DefaultSpeed один раз → Quick/Paused протекает в NewGame/Load (новая игра может стартовать «замороженной») | ПОДТВЕРЖДЁН (WorldService.cs TimeService:145-150, WorldModule.cs:76+гвард) |
| P1-15 | WorldModule._lastDay/Month/Year инициализируются только в Start(), модуль НЕ IWorldResettable → после тёплого NewGame/Load ложные Day/Month/Year события (SurviveDays, старение NPC) | ПОДТВЕРЖДЁН (WorldModule.cs:30,85-89) |
| P2-26…P2-30, P3-11 | автосейв на процесс-тиках, две pause-authority, DeltaTime при паузе, 3 представления времени, валидация даты, устаревший конфиг | БЭКЛОГ (не этот эпизод — директива аудитора «сначала P1, P2 пачками после») |

### Фаза 12 — Inventory / Equipment
| ID | Суть | Мой статический вердикт |
|----|------|--------------------------|
| P1-16 | ItemId расходников `consumable_{L}_{seed%1000:D3}` — 1000 ID на уровень; Register() заменяет определение → стак меняет эффекты (мерчант-мутация qi_restore) | ПОДТВЕРЖДЁН (ItemGeneratorService.cs:86,148,225,287 + ItemDatabaseService.Register:86-94) |
| P1-17 | EquipmentValidator: RequiredCultivationLevel/StatRequirements — заглушки «всегда проходит» | ПОДТВЕРЖДЁН (EquipmentValidator.cs:78-90) |
| P1-18 | CharacterDollPanel разрешает OneHand→WeaponOff, валидатор требует item.Slot==targetSlot, генератор даёт Slot=WeaponMain → слот WeaponOff недостижим | ПОДТВЕРЖДЁН (EquipmentValidator.cs:43 vs CharacterDollPanel.cs:214-221) |
| P1-19 | StorageRingService: не ISaveable, не IWorldResettable; ActivateRingWithVolume сохраняет старое содержимое → warm-load утечка / cold-load потеря | ПОДТВЕРЖДЁН (StorageRingService.cs:297-318, InventoryModuleServices.cs:23) |
| P2-31/32 | category-index при замене использует НОВУЮ категорию (stale-запись); RestoreState merge без очистки | ПОДТВЕРЖДЁН (ItemDatabaseService.cs:90,277-279,254-299) — в скоупе (цепочка P1-16 по директиве) |
| P2-33…36, P2/P3 Backpack | overflow валют/сделок, restore-валидация инвентаря, 50/100 vs канон 30/30, архитектурный drift | БЭКЛОГ |

### Фаза 13 — Quest (новых P1 нет)
P2-37…P2-42, P3-12…14 — весь блок в бэклог (аудитор: приоритет фазы 14 выше).

### Фаза 14 — Player / Stats / Techniques
| ID | Суть | Мой статический вердикт |
|----|------|--------------------------|
| P1-20 | Развитие статов не работает: нет producer-ов virtual delta, нет вызова ConsolidateSleep, CanAdvance default threshold 100 vs канон max(1,floor(stat/10)), метода повышения нет; StartSleep(hours) часы не хранит | ПОДТВЕРЖДЁН (StatService.cs:144-168, PlayerModule/PlayerService: StartSleep без wiring, producers отсутствуют) |
| P2-43 | ConsolidateSleep не по канону: ×0.20 + Clear() вместо min(delta, hours×0.025) + остаток | ПОДТВЕРЖДЁН — чинится ВМЕСТЕ с P1-20 (это часть конвейера) |
| P2-44 | Revive() не оживляет тело (IsAlive из BodyService; Head/Heart RedHP≤0 остаётся) | ПОДТВЕРЖДЁН (PlayerService.cs:168-174) |
| P2-45 | Movement-техника SetPosition без clamp → логическая позиция вне мира | ПОДТВЕРЖДЁН (PlayerTechniqueCaster.cs:361-366) |
| P2-46 | Ложный success-cast: PublishSuccess безусловно после AttackIntentEvent (rejection синхронный) | ПОДТВЕРЖДЁН (PlayerTechniqueCaster.cs:329-332) |
| P2-47/48/50, P3-15/16 | слоты техник без проверки, DI concrete, RestoreState статов | БЭКЛОГ |
| P2-49 | AddVirtualDelta без инвариантов (отрицательные, любые типы, без капов) | ПОДТВЕРЖДЁН — в скоупе P1-20 (капы 10/10/15/10 — канон §4.2) |

## §2. Канон (STAT_THRESHOLD_SYSTEM.md — «формулы канонические и обязательны»)

- threshold = max(1.0, floor(stat/10)) (§3.1); модификаторы §3.3 — бэклог (нет техник ускорения/артефактов обучения).
- Виртуальные дельты: только первичные статы; капы STR/AGI/VIT 10.0, INT 15.0 (§4.2); MAX_STAT_VALUE=1000 (§8.1, Constants.cs:39).
- Продюсеры боя (§5.1): удар +0.001 STR, уклонение +0.001 AGI, блок +0.001 STR, получение урона +0.001 VIT, техника +0.001 INT.
- Медитация +0.01/мин INT (§5.2).
- Сон (§6): минимум 4ч; consolidated = min(delta, hours×0.025); остаток сохраняется; после закрепления delta ≥ threshold → стат +1, delta -= threshold.
- §6.3 таблица 4-7ч расходится с формулой §6.2 (4ч→0.067 vs 4×0.025=0.1) — формулы §6.2/§6.4 канон, таблица — артефакт черновика; зафиксировать в доке.

## §3. План фиксов

### Фаза 11
1. **P1-13**: извлечь catch-up в чистый `TickCatchUpClock` (Adapter/Scene):
   - prefix-состояние = дословно текущий алгоритм (cap 8 + сброс backlog);
   - postfix-контракт: бюджет/кадр = max(8, speed×2); долг НЕ сбрасывается молча;
     при долге > ceiling (5с × speed) — излишек bulk-продвигает мировые часы
     (`TimeService.BulkAdvanceTicks(n)` — календарь/тики/TotalTime) + видимое
     предупреждение + `TimeHitchedEvent` (WorldContracts) для UI; _currentTick
     синхронно +n (каденция автосейва не ломается).
   - Инвариант: смоделированные_тики + пропущенные_тики + остаток == накопленное реальное время.
2. **P1-14**: `TimeService.ResetWorld()` → `Speed = TimeSpeed.Normal`
   (канонический дефолт WorldConfig.DefaultSpeed==Normal); LoadGame-путь уже
   зовёт ResetWorld до RestoreState → скорость нормализуется на обеих дверях.
3. **P1-15**: `WorldModule : IWorldResettable` — ResetWorld ставит
   `_calendarDirty`; первый Tick после сброса синхронизирует маркеры ТИХО
   (без событий) — порядок сброса доменов не важен, фантомных Day/Year нет
   ни на NewGame, ни на Load.

### Фаза 12
4. **P1-16 + P2-31 + P2-32**: 
   - ItemGeneratorService: все 4 генератора → counter-based ID
     (`{prefix}_{L}_{seed:x8}_{counter:x6}` по паттерну EquipmentGenerator.NextId;
     счётчик ВСЕГДА инкрементится — уникальность независима от сида);
   - ItemDatabaseService.Register/RestoreState: удаление из category-index по
     СТАРОЙ категории (захват до перезаписи);
   - ItemDatabaseService.RestoreState: Clear() перед восстановлением;
   - `ItemDatabaseService : IWorldResettable`: ResetWorld = Clear + счётчики
     генераторов в 0 + ре-сид ClassicLootSeeder (канонический контент
     «как у свежего процесса»). LoadGame: сброс (чисто) → item_db RestoreState
     поверх. NewGame: сброс → фазы генерации.
5. **P1-17**: EquipmentValidator.ValidateEquip + опциональные
   `playerCultivationLevel`/`statService`; реальные проверки Required-уровня
   и StatRequirements (NPC экипируются мимо валидатора — ID-словарь
   NPCAssemblyService, гейт только игрока; IQiService/IStatService — Core-интерфейсы,
   DI-правило соблюдено).
6. **P1-18**: валидатор разрешает `Slot==WeaponMain && HandType==OneHand` →
   WeaponOff (и обратно в WeaponMain — симметрия слота). Двуручник в Off
   остаётся запрещён (правило 3/4 сохранены).
7. **P1-19**: `StorageRingService : ISaveable, IWorldResettable`:
   - блок `storage_rings` (полный снапшот _rings: tier/maxVolume/isActive/
     StoredItems; RestoreOrder после "equipment");
   - ResetWorld: _rings.Clear();
   - LoadGame: ResetWorld (чисто) → equipment-restore публикует
     EquipmentChangedEvent → активация пустого кольца → блок storage_rings
     восстанавливает содержимое. Warm-leak/loss закрыты;
   - InventoryModuleServices: +`Register<ISaveable, StorageRingService>`.

### Фаза 14
8. **P1-20 + P2-43 + P2-49**: конвейер развития статов:
   - StatService: канон-threshold (computed + override), ConsolidateSleep по
     §6.2/§6.4 (min(delta,hours×0.025) → стат; остаток; затем while
     delta≥threshold: +1/-threshold), AddVirtualDelta инварианты (не-отриц.,
     только первичные, капы 10/10/15/10), MAX_STAT_VALUE-клэмп в мутациях;
   - новый `StatProgressProducer` (Modules/Player): DamageAppliedEvent
     (удар/уклонение/блок/получение урона — только участие игрока) +
     TechniqueUsedEvent + MeditationStateChangedEvent (per-minute INT из
     PlayerModule.Tick);
   - сон: PlayerService хранит запланированные часы + счётчик минут сна;
     PlayerModule.Tick наращивает; авто-пробуждение по плану или WakeUp() →
     ConsolidateSleep(фактические часы) на IStatService (same-module inject).
9. **P2-44**: Revive() восстанавливает vital-части (HealPart Head/Heart на
   max) → IsAlive честен; событие ревайва остаётся.
10. **P2-45**: dash-расчёт в чистый helper + Math.Clamp по границам
    ITileService (как обычное движение PlayerModule).
11. **P2-46**: после публикации интента — PublishSuccess только если выпуск
    не отклонён (OnAttackRejected уже синхронно гасит _lastFiredTechniqueId).

## §4. Сим №19 — секции M/N/O/P/Q/R (prefix=FAIL → postfix=PASS)

- **M (Фаза 11)**: M1 hitch 1с на Quick → все 15 тиков смоделированы (не 8+сброс);
  M2 гигантский hitch → bulk-jump+предупреждение, учёт тиков сходится;
  M3 BulkAdvanceTicks календарь корректен; M4 ResetWorld сбрасывает Speed
  (Quick→Normal, Paused→Normal); M5/M6 маркеры календаря после ResetWorld+Tick —
  ноль фантомных Day/Month/Year (12-30 → сброс → тишина).
- **N (Фаза 12)**: N1 коллизия сидов (5 vs 1005) → ID различны; N2 определение
  стака не подменяется (эффекты стабильны); N3 category-index без stale;
  N4 RestoreState очищает; N5 IWorldResettable + ре-сид канона.
- **O**: O1 L9-экип на уровне 1 → отказ; O2 на уровне 9 → успех; O3 StatRequirements;
  O4 OneHand→WeaponOff разрешён; O5 TwoHand→Off по-прежнему запрет.
- **P**: P1/P2 ISaveable+IWorldResettable контракты; P3 warm-load: reset →
  restore → содержимое из сейва B (не мира A); P4 cold-load: restore в пустой
  сервис; P5 активация не чистит содержимое (деактивация ≠ очистка — контракт).
- **Q**: Q1 продюсер удара (STR), Q2 уклонения (AGI), Q3 получения урона (VIT),
  Q4 медитации (INT/мин); Q5 порог канона (stat 10 → 1.0, 25 → 2.0);
  Q6 ConsolidateSleep канон (0.5 дельта, 8ч → +0.2 стат, 0.3 остаток);
  Q7 сон <4ч — без закрепления; Q8 шаг повышения (delta≥threshold → +1);
  Q9 капы INT=15; Q10 полный sleep-pipeline (StartSleep(8) + 480 тиков →
  авто-вейк + закрепление).
- **R (P2-tier)**: R1 Revive на мёртвом теле → IsAlive; R2 dash-клэмп в границы.
  P2-46 — статический фикс (runtime-ассерт признан нецелесообразным: требует
  полного каст-флоу; отмечен в §6).

## §5. Порядок работ

1. Сим-секции + API-заготовки (TickCatchUpClock в prefix-поведении,
   сигнатура валидатора с опциональными параметрами, helper dash без клэмпа
   — дословный перенос) → **prefix-прогон** (ожидание: DEFECT по каждому P1)
   → лог в checkpoints/logs/09_22_r35_prefix_audit0922.log (В GIT).
2. Фиксы §3.
3. **Постфикс-прогон** → лог (В GIT).
4. Полная QA-регрессия 19/19 (чанками в foreground), build 0 err.
5. Доки: TIME_SYSTEM (контракты catch-up/speed-reset/markers), 
   STAT_THRESHOLD_SYSTEM §6.3-примечание + §10 статус, EQUIPMENT_SYSTEM
   (гейты валидатора), INVENTORY_SYSTEM (storage_rings блок), 
   TESTING_RULES §0.1 (секции M-R).
6. Чекпоинт (ответ аудитору: верификация каждого пункта + развилки),
   worklog, SESSION_SUMMARY, коммит+пуш.

## §6. Осознанные развилки

- Bulk-jump времени симулирует ТОЛЬКО часы (не NPC/реген) — контракт:
  «долг > 5с×speed → календарь честен, пертиковые эффекты пропущены,
  видимое предупреждение». Альтернатива (неограниченный догон) — spiral-of-death.
- Скорость из сейва НЕ восстанавливается: канон «LoadGame → Normal»
  (Data.IsPaused=false уже это обещает; сейв скорость не хранит).
- P2-26 не чиним в этом эпизоде, но bulk-jump синхронит _currentTick с
  мировым временем — каденция автосейва при hitch не рвётся.
- Идентичность предметов: counter-based (как EquipmentGenerator) —
  стак «одинаковых» лекарств с разных источников не мержится (и сейчас
  не мержится: сиды разные); дубль-определения исключены.
- Тренировки §5.2 (кроме медитации) — нет UI/механики в игре → продюсеры
  не вешаем (зафиксировано как NEXT при появлении тренировок).

---

## §7. РЕЗУЛЬТАТЫ (заполнено после исполнения)

### 7.1. Runtime-пруфы (претензия верификатора к R34 — логи теперь В GIT)

- **Prefix** (контракт написан, дефекты ещё в коде):
  `checkpoints/logs/09_22_r35_prefix_audit0922.log` —
  **VERDICT: FAIL, 32 DEFECT** по всем 8 P1 (P1-13…P1-20) + P2-31/32/43/44/45/49.
- **Postfix** (после фиксов):
  `checkpoints/logs/09_22_r35_postfix_audit0922.log` —
  **VERDICT: PASS, 76 OK, 0 DEFECT.**
- Сим №19 AUDIT0922 расширен секциями M (time), N (items), O (equipment),
  P (storage rings), Q (stats), R (revive/dash) — 35 новых проверок.

### 7.2. Реестр закрытия

| ID | Фикс | Пруф |
|----|------|------|
| P1-13 | TickCatchUpClock (budget max(8,speed×2), долг не сбрасывается, излишек >5с×speed → BulkAdvanceTicks скачком + TimeHitchedEvent + предупреждение; инвариант учёта) | M1 (2с@Quick → 30/30), M2 (60с@Quick → учёт 900/900, bulk 795), M3 (календарь +1500 точен) |
| P1-14 | TimeService.ResetWorld() → Speed=Normal (обе двери NewGame/Load; сейв скорость не хранит — канон LoadGame→Normal) | M4a (Quick→Normal), M4b (Paused→Normal) |
| P1-15 | WorldModule : IWorldResettable, dirty-флаг → тихая ре-синхронизация маркеров в первом Tick | M5 (0 фантомных событий после сброса; контроль — легитимный переход суток даёт 1 DayChanged) |
| P1-16 | ItemGeneratorService: все 4 генератора → NextId (counter ВСЕГДА инкрементится: `{prefix}_{L}_{seed:x8}_{counter:x6}` — паттерн EquipmentGenerator) | N1/N1b (сид 5 vs 1005 → разные ID), N2 (определения не подменяются) |
| P1-17 | EquipmentValidator: честные гейты (playerCultivationLevel из IQiService, StatRequirements через IStatService; EquipmentService передаёт данные; NPC — мимо валидатора) | O1 (L9 при уровне 1 → отказ), O2 (уровень 9 → успех), O3 (STR≥50 → отказ) |
| P1-18 | Гибкий слот: OneHand×weapon_main ↔ weapon_off (двуручное — по-прежнему запрет) | O4 (разрешено), O5 (двуручное → запрет), O6 (Main работает) |
| P1-19 | StorageRingService : ISaveable+IWorldResettable: блок "storage_rings" (RestoreOrder после equipment), ResetWorld очищает, restore заменяет | P1/P2 (контракты), P3 (warm: нет утечки A), P4 (cold: восстановление), P5 (реактивация не чистит) |
| P1-20 | Конвейер статов: StatService (порог §3.1, дельты с инвариантами, ConsolidateSleep §6.2/§6.4) + StatProgressProducer (§5.1/§5.2) + сон (StartSleep→минуты→авто-вейк→закрепление) | Q1–Q4b (продюсеры), Q6 (порог), Q7 (<4ч), Q8 (min(delta,hours×0.025)+остаток), Q9 (шаг +1), Q10 (полный pipeline 480 тиков) |
| P2-31 | Register/RestoreState: удаление из category-index по СТАРОЙ категории | N3 |
| P2-32 | RestoreState очищает каталог; ItemDatabaseService : IWorldResettable (clear + счётчики 0 + ре-сид ClassicLoot — ResetForNewWorld) | N4 (Count==1 после restore), N5a/N5b (контракт + ре-сид канона) |
| P2-43 | ConsolidateSleep по канону | Q7/Q8 |
| P2-44 | Revive(): HealPart vital-частей / ReattachPart при ампутации | R1 (IsAlive после Revive на мёртвом теле) |
| P2-45 | ComputeDashTarget + Math.Clamp по границам | R2 (клэмп по 4 сторонам) |
| P2-46 | PublishSuccess только если интент не отклонён синхронно (OnAttackRejected гасит _lastFiredTechniqueId) | статический фикс (runtime-ассерт признан нецелесообразным: требует полного каст-флоу — см. §6) |
| P2-49 | AddVirtualDelta: не-отрицательные, только первичные, капы 10/10/15/10 | Q5/Q5b/Q5c |

### 7.3. Регрессия

- build: **0 errors** (CultivationGame.csproj, .NET 8, Godot 4.7.2 mono).
- Полная QA-регрессия чанками (foreground): **19/19 PASS**
  (COMBAT_SIM, COMBATAI, ANIMALQA, LOOT, DOT, SAVELOAD, KILLFEED, QUEST,
  STORAGE, HOTBAR, TRASHDROP, CONTEXT, MODALQA, MODAL2, L500, WEAPONVIS,
  REASSEMBLY, CHARGE, AUDIT0922).
- Побочные правки под честный гейт P1-17: CombatSimDebug 3b/3c и
  WeaponVisSimDebug генерируют экип по ФАКТИЧЕСКОМУ уровню игрока (L1) —
  прежде хардкод L2/L3 проходил мимо заглушки.
- WorldDomainReset: 29 → 32 домена (WorldModule/ItemDatabaseService/
  StorageRingService) — реестр сброса растёт декларативно.

### 7.4. Осталось в бэклоге (не этот эпизод)

- Фаза 11: P2-26 (автосейв на process-tick; bulk-jump теперь синхронит
  _currentTick — каденция при hitch не рвётся, но абсолютная привязка к
  мировым тикам не сделана), P2-27 (две pause-authority + Resume→Normal),
  P2-28 (DeltaTime при паузе), P2-29 (3 представления времени), P2-30
  (валидация даты), P3-11 (устаревший WorldConfig).
- Фаза 12: P2-33/34 (overflow валют/сделок), P2-35 (restore-валидация
  инвентаря), P2-36 (50/100 vs канон 30/30 — нужен баланс-ревью),
  Backpack-архитектура drift.
- Фаза 13 (новых P1 нет): P2-37…P2-42, P3-12…14.
- Фаза 14: P2-47 (слоты техник без проверки), P2-48 (DI concrete в кастере),
  P2-50 (RestoreState статов/Body-уведомление), P3-15/16.
- Сквозные: P2-22 (EventBus drain guard — подтверждён L-диагностикой),
  P2-23/18/24/25 и др.

### 7.5. Контрольные вопросы аудитора (Фаза 11/12 преамбулы)

- «прогнать сценарии NewGame → Pause → NewGame, NewGame → Fast → Load,
  day/year transition, hitch at Quick и Load → first tick calendar events» —
  покрыто секциями M (ResetWorld-семантика маркеров/скорости = эквивалент
  тёплой границы миров; hitch-сценарии; календарные переходы) в headless-
  среде; интеграционная фаза «Time→NPC→Cultivation→…→Save→Time resumes»
  (маршрут аудитора после фаз 11–14) — рекомендую отдельным эпизодом.
- «runtime-пруфы ItemId collision и StorageRing Save→Load» — секции N/P.
