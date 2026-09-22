# Правила тестирования (Testing Rules)

> **Назначение:** Движко-независимая спецификация правил тестирования: структура тестов, паттерны, naming, типы тестов, mock-объекты, верификация баланса, headless-запуск. Без привязки к конкретному test-фреймворку.
>
> **Связанные документы:** `AI_DEVELOPMENT_WORKFLOW.md`, `ALGORITHMS.md`, `01_architecture/DI_AND_EVENTBUS.md`, `01_architecture/MODULE_STRUCTURE.md`.

---

## 0. Реальный QA-конвейер проекта (актуализировано 2026-09-10, верификация R13)

Классические `dotnet test`-наборы (§1-§10) в репозитории **не созданы** (плановая работа).
Фактический QA-конвейер — **headless env-харнесс Godot**: QA-сцены (`*SimDebug.cs` в
`Adapter/Scene/`) запускаются переменными окружения `GODOT_*`, логируют прогресс и
печатают вердикт `VERDICT: PASS/FAIL` (строка ищется в логе). Регрессия = полный
прогон всех хуков + `dotnet build` с 0 errors.

### 0.1. Реестр env-хуков (37)

| Хук | Сцена/файл | Что проверяет |
|---|---|---|
| `GODOT_NEWGAME=1` | MainMenuController | Полный флоу: меню → New Game → сборка сцены |
| `GODOT_COMBAT_SIM=1` | CombatSimDebug | Бой в обе стороны + weapon end-to-end + readiness-gate (R21-2: Accepted/Rejected «не готов»/re-ready; R23-1: 3e (a)-(e) через DebugSetReadinessPermil — charged при 600‰ = Accepted и НЕ расходует; R24-C: 3g multi — (a) тихий NPC-NPC без UI-сессии + месть Threats, (b) сессия пары с игроком + ретрай-удар даже при уже открытой сессии (R29: гонка NPC-AI — damage-гард обязан получить ≥1 явный удар), (b2) не-участник бьёт игрока в активном бое = CMB-2-регресс, retry-циклы против NPCModule-гонок; R25: 3h AoE — (a) стенд геометрии форм (круг/конус/полукруг/линия, int-математика), (b) конус игрока в толпу 3 NPC В ЧИСТОЙ ЗОНЕ В ГРАНИЦАХ КАРТЫ (R29-харденинг: мстители ранних фаз попадали в конус NPC-залпа и вытесняли crowd1 из MaxTargets — флейк «месть соседа»), месть замеряется НЕМЕДЛЕННО (R29: Threats затухают с Remove — пауза 1.2с стирала слабые записи), (c) AoE-залп NPC по игроку+соседу (критерий урона = прирост суммы DamageAppliedEvent ИЛИ HP-дельта — HP врёт при попадании в разрушенную часть, ретрай ×3, bestAcc против кулдаун-ретраев; месть соседа — немедленный замер); R27: 3i targeting — Tab-цикл; R28: 3j homing — стенд наведения + полёт с отложенным уроном; R29: 3k vfx — (a) AoE-залп → AoeFxRenderer.TotalVolleys +1 / TotalTargetFlashes ≥+2, (b) хоуминг-пуск → HomingProjectileRenderer.TotalLaunches +1, (c) Hold/Release ауры → TechniqueEffectRenderer.TotalHoldAuraChanges ровно +2; харденинг позиций: TeleportPlayer через DEBUG_TeleportPlayer (прямой SetPosition откатывается _PhysicsProcess-синком к _visualPosition), чистая зона клампится в границы карты, ожидание респавна при мёртвом игроке, паузы на прибытие камеры + r21-гарды: броня и пассивный щит Ци (VERDICT) |
| `GODOT_FORM_DEBUG=1` / `GODOT_FORM_SHOT=prefix` | FormationShotSimDebug | R30-П1/П2/П3/П7 (репорт 21.09): headless VERDICT — радиус зарядки (дрейн рядом → ПАУЗА на дистанции > ChargingRadiusTiles → резюм при возврате; телепорт ТОЛЬКО через GWC.DEBUG_TeleportPlayer — прямой SetPosition откатывается _PhysicsProcess-синком, урок R29), П3-описание FullDescription (тип действия + эффекты + радиус + правило зарядки), активация; SHOT (Xvfb+opengl3) — скриншоты _drawing/_filling/_active.png + VLM-проверка контраста (тёмная подложка П1) и отсутствие в логе ошибок «triangulation failed» (П7) |
| `GODOT_CHARGE_SIM=1` | ChargeSimDebug | Заряд техник (кулдауны/ёмкость) |
| `GODOT_TRADE_DEBUG=1` | TradeModule | Smoke покупки/продажи |
| `GODOT_TRADE_HOLD=1` | TradeModule | Не закрывать лавку (скриншоты) |
| `GODOT_TRADEUX_DEBUG=1` | TradeUXSimDebug | S6 UX лавки: пометки/тултипы/hover/Esc |
| `GODOT_GEN_DEBUG=1` | GeneratorModule | Дамп генераторов (легендарки/оверкап/верификация) |
| `GODOT_MAP_SIZE=500` | TileModule/AnimalService | Большая карта 500×500 |
| `GODOT_TOAST_DEBUG=1` | ToastSimDebug | S2 тост-стек: незатирание/×N/кап/TTL |
| `GODOT_LOWHP_DEBUG=1` | LowHpSimDebug | S2 виньетка HP<35%: alpha/пульс |
| `GODOT_KILLFEED_DEBUG=1` | KillFeedSimDebug | S3 kill-feed + атрибуция |
| `GODOT_HOTBAR_DEBUG=1` | HotbarSimDebug | S4 хотбар v2: кулдауны/Qi-гейт/пояс |
| `GODOT_DAMAGEDIR_DEBUG=1` | DamageDirSimDebug | S5 стрелка атакующего: 4/4 |
| `GODOT_DIALOGUE_DEBUG=1` | DialogueSimDebug | S6 диалог: геометрия/fit/индикатор/выбор |
| `GODOT_DIALOGUE_HOLD=1` | DialogueSimDebug | Держать диалог (скриншоты) |
| `GODOT_TRADEUX_HOLD=1` | TradeUXSimDebug | Держать лавку UX |
| `GODOT_FORMATION_TEST=1` | TechniqueGrantPhase | Формационный тест (этап формаций) |
| `GODOT_REASSEMBLY_DEBUG=1` | ReAssemblySimDebug | Ревью-1: повторная сборка в одном процессе — Reset фаз оркестратора + world-scoped реестры (16/16 Completed, техники не ×2). Аудит R13/R14: + ассерты NPC-домена — QA-смерть NPC (труп) и QA-группа в мире 1 НЕ переживают пересборку (R17: WorldDomainResetPhase), реестр NPC не ×2 (double-spawn), ghost-NPC отсутствует; R17: + звери/время (WorldDomainResetPhase вместо NpcDomainResetPhase); harness-гейт (QA-грязь обязана быть создана) |
| `GODOT_STORAGE_DEBUG=1` | StorageSimDebug | Ревью-этап-4: инвентарные транзакции — spirit retrieve/stacking (P0-1/P1-2), ring Qi (P1-3), craft overflow (P1-4), pickup unknown/граница (P1-5/P2-6) |
| `GODOT_DOT_DEBUG=1` | DotSimDebug | Ревью-этап-5: DoT (Poison/Burn/Bleed/Freeze) наносит реальный урон через DamageAppliedEvent (игрок + NPC, P1-4) |
| `GODOT_RESPAWN_DEBUG=1` | RespawnSimDebug | Ревью-этап-6: respawn ресурсов (истощение→7 дней→восстановление тайла), честный TryTravel, TimeChangedEvent.Delta == DeltaTime |
| `GODOT_QUEST_DEBUG=1` | QuestSimDebug | Ревью-этап-7: полный цикл квестов — accept (через реальный диалог старейшины) → событие → complete → reward; гейт RequiredCultivationLevel |
| `GODOT_TRASHDROP_DEBUG=1` | TrashDropSimDebug | Баг-репорт 09-08: инвентарный drag&drop в корзину — материалы draggable (раньше пустой Variant), корзина выбрасывает весь стек, кукла отклоняет не-экипировку, чужой source отвергается |
| `GODOT_CONTEXT_DEBUG=1` | ContextMenuSimDebug | Запрос 09-09: ПКМ-контекстное меню — окно свойств, «Разделить стак…» (слайдер −/+ с двумя числами), множественные кучки одного ItemId, слот-адресный выброс кучки в корзину, Esc-приоритет попапов (8/8); ревью-R10 SlotId: stale drop при мутации в полёте, stale split → отказ, split при дрейфе индексов (11/11); R19 (2026-09-19) «Потребляемые ресурсы» +4 шага: поглощение камня Ци сквозь кнопку (дрейн Ци — буфер полон), пилюля лечения из ПКМ-меню (урон→heal→счётчик −1), лекарство "Heal" с ЗАГЛАВНОЙ (репродукция бага кейса — нормализация), материал без кнопки «Использовать», читаемость подокна (высота панели >100/альфа ≥0.95/контраст ≥4.5) (15/15). Сим сам завершает процесс (GetTree().Quit) — без HOLD |
| `GODOT_MODALQA_DEBUG=1` | ModalSimDebug | Аудит-0915 A8 (P2-3): модальные окна — инвариант «пауза ⇔ стек окон ∨ Esc»: «×»/bg-click всех 6 окон (B/C/Q/J/F1/T) доходит до резюма тиков (Input.ActionPress эмуляция клавиш headless), идемпотентность двойного резюма (GWC-ветка + Closed), стек инвентарь+лавка не вешает паузу (A3) |
| `GODOT_NEWGAME_WORLD=<id>` | MainMenuController | L500: мир авто-старта GODOT_NEWGAME (по умолчанию test_polygon — детерминизм 16 симов; large_world — для L500-сима) |
| `GODOT_L500_DEBUG=1` | L500SimDebug | L500 (2026-09-15): мир 500×500 основной — сетка 500×500, генерации интегрированы (NPC ≥30 заспавнено / звери ≥15 / группы ≥4), пояс жизни (≥40% NPC в радиусе 100 от центра; эмерджентная смертность диких земель — живые ≥15 отдельно), MaxActiveNPCs ≤ 100. Запуск в связке: GODOT_NEWGAME_WORLD=large_world (раннер ставит сам) |
| `GODOT_SAVELOAD_DEBUG=1` | SaveLoadSimDebug | Ревью-R11 → R17: Save/Load round-trip — типизация (IncludeFields), честный success, РЕАЛЬНЫЕ мутации до/после (анти-тривиальность), R17: 19 блоков + R17-ключи 11/11, integrity-мутации 12/12 |
| `GODOT_LOOT_DEBUG=1` | LootSimDebug | R13 full-loot: генерация состава населения (12 NPC/6 ролей/детерминизм), труп-контейнер, окно обыска, SlotId-взятие, double-take отказ, full loot, TTL; R14: популяция в сессии НЕ восполняется (выбитая — остаётся выбитой) + ивент-спаун TrySpawnEventNpc(Caravan) работает |
| `GODOT_WEAPONVIS_DEBUG=1` | WeaponVisSimDebug | R15 «оружие в руках»: WeaponClassId у генерации (7/7), fallback-парсинг ItemId/unknown→sword, спрайты icon(32)/hand(48) + различимость классов/тиров/редкости, композит игрока (стартовый кинжал→копьё→анэкип→меч), иконки хотбара 1-2, overlay NPC (12 NPC), facing-зеркалирование |
| `GODOT_COMBATAI_DEBUG=1` | CombatAISimDebug | R16 боевой ИИ: месть NPC на атаку игрока (Attacking/Fleeing по личности), двусторонний урон, селектор защит NPC (щит→Block/силовик→Parry/прочие→Dodge — детерминированные кейсы), бегство HP<20% в бою (CombatDisengageEvent), leash AggroRadius×3, стойка игрока (DefenseIntentEvent→CurrentPlayerDefense), счётчики StrikeFX (swipes/strikes) |
| `GODOT_ANIMALQA_DEBUG=1` | AnimalCombatSimDebug | 2026-09-11 аудит боя с животными: таргетинг Space видит волка (NPC ∪ животные), урон игрок→волк через тело, месть (чейз+укус вплотную, HP игрока падает), смерть → труп «Волк» (камни, DEATH_AND_LOOT §5) + Victory/EndCombat, killfeed-имя, de-aggro (CombatEnded), кролик мирный; R18-1 (2026-09-19) +шаг 8: HP-бары врагов + глобальный тумблер ShowEnemyVitals (кролик/NPC direct-mutation повреждение → предикаты WouldDraw*, OFF → false + синтетический DamageAppliedEvent с фейковой целью → подавление цифр EnemyTextSuppressedCount, восстановление ON + откат повреждений; 8 тестов; телепорт игрока — GWC.DEBUG_TeleportPlayer) |
| `GODOT_STRIKEFX_HOLD=1` | CombatAISimDebug | R16 визуальный HOLD: живая драка у игрока для Xvfb-скриншотов (свипы/замахи/цифры непрерывно) — без сценария |
| `GODOT_SCREENSHOT=<путь>` | GameBoot | Скриншот в файл (VLM-верификация) |
| `GODOT_SCREENSHOT_DELAY=<сек>` | GameBoot | Задержка кадра для скриншота |
| `GODOT_CONTEXT_HOLD=1` | ContextMenuSimDebug | Держать ПКМ-меню (скриншоты) — без HOLD сим сам завершает процесс |
| `GODOT_SCREENSHOT_MENU=<путь>` | ContextMenuSimDebug | Скриншот окна ПКМ-свойств |
| `GODOT_VFX_SHOT=<префикс>` | CombatSimDebug 3k | R29: VFX-кадры боевых эффектов — Xvfb + `--rendering-driver opengl3` (НЕ headless): `_aoe.png` (конус/форма + маркеры целей), `_homing.png` (болт в полёте, 0.25с после пуска), `_hold.png` (аура удержания) + VLM-разбор. Паттерн GODOT_SCREENSHOT_MENU |
| `GODOT_CONTEXT_MENU_ITEM=<itemId>` | ContextMenuSimDebug | R19: предмет для скриншота ПКМ-меню (default material_stone; напр. con_pill_healing — снимок с кнопкой «Использовать») |
| `GODOT_SCREENSHOT_SPLIT=<путь>` | ContextMenuSimDebug | Скриншот слайдера разделения стака |
| `GODOT_AUDIT0922_DEBUG=1` | Audit0922SimDebug | Аудит 09.22 (внешний): инфраструктурные контракты — A) EventBus изоляция исключений + re-entrancy-дрен (P1-5: фан-аут не прерывается, стэл-очередь не протухает), B) санация имён слотов в Modules-layer SaveFileHandler (P1-8 path traversal), C) DI override prune: stale concrete-ключ + стражи мульти-форварда/last-wins/наследования ключа (P1-6), D) честный DeleteSave по слоям (P1-7), E) Trade/Corpse : IWorldResettable (P1-1/P1-4), F) ResolveAll — порядок = порядок регистрации, мёртвые регистрации не воскресают, ResolveAll<ISaveFileHandler> = ровно адаптер (P2-12, Фаза 4), G) startup fail-closed — AggregateException + заглушённый тик-луп + ретрай без «already initialised» (P2-13, Фаза 4), H) cycle detection по construction path с точным путём «A → B → A», без ложных срабатываний на цепочках (P2-14, Фаза 4), I) WorldDomainResetPhase fail-closed: исключение ResetWorld() домена = AggregateException наружу с именем виновника, все домены диагностируются за одну попытку, MarkAsCompleted недостижим (P1-9, Фазы 6/7/10), J) startup retry = resume from failure: успешно стартовавшие модули НЕ перезапускаются (SubscriberCount стабилен, StartCalls=1), провалившийся ретраится (P1-10, Фазы 7/10), K) DamageType.Pure не входит в Qi-буфер: Ци игрока/NPC не расходуется, поглощение 0; контроль Physical — Ци расходуется (P1-12, Фазы 9/10), M) Фаза 11 — time pipeline: честный catch-up TickCatchUpClock (инвариант учёта: моделирование+bulk+остаток = Σ(delta×speed); hitch 2с@Quick → 30/30 тиков), BulkAdvanceTicks календарь точен, ResetWorld сбрасывает Quick/Paused → Normal, фантомных Day/Month/Year после сброса мира нет (P1-13/P1-14/P1-15), N) Фаза 12 — item identity: ID уникальны при коллизионных сидах 5/1005 (counter-based), определения не подменяются, category-index без stale при смене категории, RestoreState заменяет каталог, ResetWorld чистит + ре-сеет канон (P1-16/P2-31/P2-32), O) Фаза 12 — equipment гейты: RequiredCultivationLevel=9 при уровне 1 → отказ (уровень 9 → успех), StatRequirements STR≥50 → отказ, одноручное → WeaponOff разрешено, двуручное → запрет (P1-17/P1-18), P) Фаза 12 — StorageRing persistence: ISaveable+IWorldResettable контракты, warm-load без утечки содержимого мира A, cold-load восстанавливает сейв, реактивация не чистит (P1-19), Q) Фаза 14 — stat progression: продюсеры §5.1/§5.2 (удар→STR, уклонение→AGI, урон→VIT, техника/медитация→INT), порог max(1,floor(stat/10)), ConsolidateSleep по §6.2/§6.4 (8ч → +0.2 + остаток; <4ч — нет; delta≥thr → +1), капы INT=15/отрицательные/вторичные отвергаются, sleep-pipeline StartSleep(8ч)→480 тиков→авто-вейк+закрепление (P1-20/P2-43/P2-49), R) Фаза 14 — Revive оживляет тело (vital Head/Heart: HealPart/Reattach), dash-цель клэмпится в границы мира (P2-44/P2-45), S) Фаза 11 P2-бэклог — автосейв по МИРОВЫМ тикам (мини-DI: фейк ITimeService/ISaveService/Playing-сессии + SaveModule; разностная схема 29→59=30 мин → сейв, ResetWorld обнуляет отсчёт — process-остаток 10029 не триггерит, первый автосейв нового мира ровно на 30-м тике, bulk-скачок не теряет каденцию), единая pause-authority (Session.Pause → TimeService.Paused на реальной сессии), Resume восстанавливает скорость до паузы (Fast), IsPaused ⇒ DeltaTime==0, Load канонизирует три представления (дата первична, TickCount=минуты с 06:00 дня 1, рассинхронный блок 777/42 → пересчёт), WorldTime/RestoreState отвергают month=13/day=0/hour=29/minute=-10/год до эпохи (ArgumentOutOfRangeException → битый сейв = отказ загрузки), WorldConfig без мёртвых Start*/AutoSaveIntervalTicks/BaseTickRate (рефлексия) (P2-26…30 + P3-11). Прогон до фиксов = runtime-подтверждение дефектов (чекпоинты 09_22_r32–r34 + фазы 11–14: 32 DEFECT + R36-a: 12 S-DEFECT, логи 09_22_r35_prefix/postfix + 09_22_r36a_prefix/postfix), после — регрессионный страж; сим №19 в qa_regression.sh |

### 0.2. Прогон регрессии — `tools/qa_regression.sh` (2026-09-15)

Раннер регрессии — `bash tools/qa_regression.sh [sim…]` из корня репо (без
аргументов — все 16 симов реестра; аргумент — имя сима `COMBAT_SIM` или пара
`NAME:VAR`). Семантика (важно — симы НЕ завершают процесс после VERDICT,
мир продолжает тикать):
- «сторож»: kill -9 через 3с ПОСЛЕ появления вердикта в логе;
- жёсткий потолок `QA_CEILING` сек (по умолчанию 240) без вердикта = FAIL;
- вердикт — ПОСЛЕДНЯЯ строка `VERDICT: (PASS|FAIL)` в выводе (не первая);
- trap INT/TERM/EXIT — прерывание не оставляет сироту-godot и хвосты в /tmp;
- L500: раннер сам ставит GODOT_NEWGAME_WORLD=large_world (остальные — test_polygon).

```bash
cd game && dotnet build                                # 0 errors обязательно
bash tools/qa_regression.sh                            # все 17 симов (~2-3 мин)
bash tools/qa_regression.sh COMBAT_SIM QUEST           # подмножество
QA_CEILING=120 bash tools/qa_regression.sh SAVELOAD    # свой потолок
# Итог: «N/N PASS» + список FAILED; exit 1 при любом FAIL.
```

> Правило: **новая фича = новый хук** (паттерн S1-S6). Скриншот + VLM — для
> визуальных изменений (Xvfb + `--rendering-driver opengl3`).

---

## 1. Принципы

1. **AAA Pattern** — все тесты следуют паттерну Arrange-Act-Assert.
2. **Изоляция** — каждый тест независим; глобальное состояние запрещено.
3. **Очистка** — после каждого теста все ресурсы освобождаются (TearDown).
4. **Именование** — `Method_Scenario_ExpectedResult`.
5. **Edge cases обязательны** — минимум 3 граничных случая на модуль.
6. **Mock через интерфейсы** — моки реализуют интерфейсы, не наследуют конкретные классы.
7. **Pure C# тестируется без движка** — все игровые системы (16 модулей core) тестируются через `dotnet test` без движка.
8. **Движко-зависимый код тестируется отдельно** — UI, сцены, рендеринг — через тест-фреймворк движка или скриншот-тесты.

---

## 2. Структура тестов

### 2.1. Дерево каталогов

```
tests/
├── Core/                           # Тесты чистого C# core
│   ├── ValidationTests             # Базовая проверка сборки
│   └── GameConstantsTests          # Тесты констант
├── Modules/
│   ├── Body/
│   │   ├── BodyPartTests
│   │   ├── BodyDamageCalculatorTests
│   │   └── BodyServiceTests
│   ├── Buff/
│   │   ├── BuffCalculatorTests
│   │   └── BuffServiceTests
│   ├── Combat/
│   │   ├── DamageCalculatorTests
│   │   ├── DefenseProcessorTests
│   │   ├── LevelSuppressionTests
│   │   └── TechniqueCapacityTests
│   ├── Qi/
│   │   ├── QiBufferServiceTests
│   │   ├── QiRegenCalculatorTests
│   │   ├── QiBreakthroughCalculatorTests
│   │   └── QiServiceTests
│   ├── Tile/
│   │   ├── TileMapServiceTests
│   │   └── DestructibleServiceTests
│   ├── Charger/                    # TBD
│   ├── Formation/                  # TBD
│   ├── Inventory/                  # TBD
│   ├── NPC/                        # TBD
│   ├── Player/                     # TBD
│   ├── Quest/                      # TBD
│   ├── Save/                       # TBD
│   ├── Interaction/                # TBD
│   └── World/                      # TBD
├── Integration/                    # Интеграционные тесты
├── Balance/                        # Верификация баланса
└── TestUtilities/                  # Хелперы и моки
    ├── TestContainerBuilder        # DI-конфигуратор для тестов
    ├── EventBusTestHelper          # Хелпер для тестов событий
    ├── MockQiService               # Мок IQiService
    ├── MockBodyService             # Мок IBodyService
    ├── MockBuffService             # Мок IBuffService
    ├── MockTimeService             # Мок ITimeService
    └── MockInventoryService        # Мок IInventoryService
```

> Структура повторяет структуру модулей (см. `01_architecture/MODULE_STRUCTURE.md`).

### 2.2. Покрытие по модулям (текущее состояние)

| Модуль | Статус | Тестов |
|--------|--------|--------|
| Core (Validation, Constants) | ✅ | 18 |
| Body | ✅ | 28 |
| Buff | ✅ | 13 |
| Combat | ✅ | 31 |
| Qi | ✅ | 31 |
| Tile | ✅ | 17 |
| Charger | 🔲 TBD | — |
| Formation | 🔲 TBD | — |
| Inventory | 🔲 TBD | — |
| NPC | 🔲 TBD | — |
| Player | 🔲 TBD | — |
| Quest | 🔲 TBD | — |
| Save | 🔲 TBD | — |
| Interaction | 🔲 TBD | — |
| World | 🔲 TBD | — |
| Integration | 🔲 TBD | — |
| Balance | 🔲 TBD | — |

---

## 3. Test Framework

### 3.1. Нейтральность к фреймворку

Документ не предписывает конкретный test-фреймворк. Возможные варианты:

| Фреймворк | Назначение | Примечание |
|-----------|------------|------------|
| xUnit | .NET unit testing | Рекомендуется для Godot C# |
| NUnit | .NET unit testing | Альтернатива |
| Godot test framework (GUT) | Godot-зависимые тесты | Для сцен и UI |
| Custom | Собственный | Не рекомендуется |

> Все примеры ниже используют **нейтральный синтаксис** (NUnit-подобный), но легко переносятся в xUnit.

### 3.2. Атрибуты

| Атрибут | Назначение |
|---------|------------|
| `[TestFixture]` | Класс с тестами |
| `[Test]` | Один тест (синхронный) |
| `[Test]` async | Один тест (асинхронный, через `async Task`) |
| `[SetUp]` | Выполняется перед каждым тестом |
| `[TearDown]` | Выполняется после каждого теста |
| `[OneTimeSetUp]` | Выполняется один раз перед всеми тестами в классе |
| `[OneTimeTearDown]` | Выполняется один раз после всех тестов в классе |
| `[Ignore("reason")]` | Пропустить тест |
| `[Category("name")]` | Категория для группировки |

---

## 4. AAA Pattern

Все тесты следуют паттерну **Arrange-Act-Assert**:

```csharp
[Test]
public void Test_Example()
{
    // === Arrange ===
    int expected = 10;
    int value = 5;

    // === Act ===
    int result = value * 2;

    // === Assert ===
    Assert.AreEqual(expected, result);
}
```

---

## 5. Именование тестов

### 5.1. Формат

```
[Метод]_[Сценарий]_[ОжидаемыйРезультат]
```

### 5.2. Примеры

```csharp
[Test]
public void LevelSuppression_SameLevel_ReturnsFullDamage() { }

[Test]
public void QiBuffer_InsufficientQi_PartialAbsorption() { }

[Test]
public void TechniqueCapacity_Ultimate_HasMultiplier() { }

[Test]
public void TakeDamage_ReducesRedHP_70Percent() { }
```

### 5.3. Категории имён

| Категория | Префикс | Пример |
|-----------|---------|--------|
| Конструктор | `Constructor_` | `Constructor_SetsPartType` |
| Свойство | `Property_` | `Property_CurrentQi_ReturnsValue` |
| Метод | `[MethodName]_` | `TakeDamage_ReducesRedHP_70Percent` |
| Edge case | `[MethodName]_Edge_` | `TakeDamage_Edge_ZeroDamage_NoChange` |
| Интеграционный | `Integration_` | `Integration_QiService_TechniqueService_CombatFlow` |
| Баланс | `Balance_` | `Balance_QiRegen_10PercentPerDay` |

---

## 6. Setup и Teardown

```csharp
private SomeService _service;

[SetUp]
public void Setup()
{
    // Создаём сервис перед каждым тестом
    var mockDeps = new MockDeps();
    _service = new SomeService(mockDeps);
}

[TearDown]
public void TearDown()
{
    // Освобождаем ресурсы после каждого теста
    _service?.Dispose();
    _service = null;
}
```

### 6.1. Правила

- **Никакого глобального состояния.** Если нужно состояние — оно в поле класса, инициализируется в SetUp.
- **Каждый тест получает свежий сервис.** Не переиспользуем между тестами.
- **TearDown обязателен** для тестов, создающих ресурсы (файлы, сети, БД).

---

## 7. Типы тестов

### 7.1. Unit тесты

Тестируют один метод или класс в изоляции:

```csharp
[Test]
public void LevelSuppression_CalculatesCorrectly()
{
    // Тестируем только LevelSuppression.CalculateSuppression
    float result = LevelSuppression.CalculateSuppression(1, 3, AttackType.Normal);
    Assert.AreEqual(0.0f, result);
}
```

### 7.2. Интеграционные тесты

Тестируют взаимодействие между системами:

```csharp
[Test]
public void Integration_QiService_TechniqueService_CombatFlow()
{
    // Создаём обе системы с моками
    var qiService = new QiService(new MockTimeService());
    var techService = new TechniqueService(qiService);

    qiService.SetCultivationLevel(3, 5);
    qiService.RestoreFull();

    // Проверяем, что техника может быть использована
    bool canUse = techService.CanUseTechnique(learnedTech);
    Assert.IsTrue(canUse);
}
```

### 7.3. Edge Cases

Обязательные граничные случаи:

| Тип | Пример |
|-----|--------|
| Нулевые значения | `QiBuffer_ZeroDamage_NoConsumption` |
| Отрицательные значения | Проверка защиты от invalid input |
| Overflow | `Test_EdgeCase_QiOverflow` |
| Минимальные значения | Level 1 vs Level 10 |
| Максимальные значения | Проверка капов |
| Граничные условия | Точно на пороге (HP = 1, Qi = 0) |

> **Минимум 3 граничных случая на модуль.**

---

## 8. Mock объекты

### 8.1. Принцип

Моки реализуют **интерфейсы**, не наследуют конкретные классы. Это обеспечивает:
- Изоляцию от реальных зависимостей.
- Контроль над возвращаемыми значениями.
- Возможность проверки вызовов.

### 8.2. Пример мока

```csharp
public class MockQiService : IQiService
{
    private long _currentQi;
    private long _maxQi;
    private int _cultivationLevel;

    public long CurrentQi => _currentQi;
    public long MaxQi => _maxQi;
    public int CultivationLevel => _cultivationLevel;

    public void SetCurrentQi(long value) => _currentQi = value;
    public void SetMaxQi(long value) => _maxQi = value;
    public void SetCultivationLevel(int level, int subLevel) => _cultivationLevel = level;

    public bool TryConsume(long amount)
    {
        if (_currentQi < amount) return false;
        _currentQi -= amount;
        return true;
    }

    public void RestoreFull() => _currentQi = _maxQi;
}
```

### 8.3. Использование мока

```csharp
[Test]
public void TryConsume_SufficientQi_ReturnsTrue()
{
    // Arrange
    var mockQi = new MockQiService();
    mockQi.SetCurrentQi(100);
    mockQi.SetMaxQi(100);

    // Act
    bool result = mockQi.TryConsume(30);

    // Assert
    Assert.IsTrue(result);
    Assert.AreEqual(70, mockQi.CurrentQi);
}
```

### 8.4. Существующие моки

| Мок | Интерфейс | Назначение |
|-----|-----------|------------|
| `MockQiService` | `IQiService` | Управление Ци |
| `MockBodyService` | `IBodyService` | Тело и HP |
| `MockBuffService` | `IBuffService` | Баффы и модификаторы |
| `MockTimeService` | `ITimeService` | Тики и время |
| `MockInventoryService` | `IInventoryService` | Инвентарь |

---

## 9. Верификация баланса

### 9.1. Класс BalanceVerification

Специальный класс для проверки формул и таблиц баланса:

```csharp
public static class BalanceVerification
{
    public static void QuickVerify()
    {
        VerifyCoreCapacityFormula();
        VerifyLevelSuppressionTable();
        VerifyQiBufferEfficiency();
        VerifyMeditationTime();
    }

    public static void VerifyCoreCapacityFormula() { /* ... */ }
    public static void VerifyLevelSuppressionTable() { /* ... */ }
    public static void VerifyQiBufferEfficiency() { /* ... */ }
    public static void VerifyMeditationTime() { /* ... */ }
}
```

### 9.2. Проверяемые значения

| Категория | Метод | Что проверяет |
|-----------|-------|---------------|
| Плотность Ци | `VerifyCoreCapacityFormula()` | `qiDensity = 2^(level-1)` |
| Подавление уровнем | `VerifyLevelSuppressionTable()` | Таблица подавления |
| Qi Buffer | `VerifyQiBufferEfficiency()` | 90% поглощение, 10% пробитие |
| Время медитации | `VerifyMeditationTime()` | Формула времени медитации |
| Все сразу | `QuickVerify()` | Все вышеперечисленное |

> Формулы — в `09_workflow/ALGORITHMS.md`.

---

## 10. Обязательные модули для тестирования

| Модуль | Приоритет | Что тестировать |
|--------|-----------|-----------------|
| Combat/DamageCalculator | Высокий | Все формулы урона |
| Combat/LevelSuppression | Высокий | Таблица подавления |
| Combat/QiBuffer | Высокий | Поглощение, пробитие, частичное |
| Qi/QiService | Высокий | Накопление, расход, регенерация |
| Combat/TechniqueCapacity | Средний | Множители по типу техник |
| Formation/FormationQiPool | Средний | Пул Ци формации |
| Save/SaveManager | Средний | Сохранение/загрузка, версионирование |
| Body/BodyPart | Высокий | HP, статусы частей |
| Buff/BuffCalculator | Средний | Суммирование, капы |
| Tile/TileMapService | Средний | Размещение, удаление тайлов |

### Минимальные требования

- **Unit тесты:** Каждый публичный метод с логикой.
- **Edge cases:** Минимум 3 граничных случая на модуль.
- **Интеграция:** Каждая связь между системами.

---

## 11. Headless-запуск тестов

### 11.1. Командная строка

Для CI/CD или запуска без GUI:

```bash
# .NET (чистый C# core)
dotnet test --filter "Category=Unit" --logger "console;verbosity=detailed"

# С фильтром по неймспейсу
dotnet test --filter "FullyQualifiedName~CultivationGame.Tests.Modules.Body"

# С фильтром по классу
dotnet test --filter "FullyQualifiedName~BodyPartTests"

# С результатами в XML (NUnit/xUnit формат)
dotnet test --logger "nunit;LogFilePath=results.xml"
```

### 11.2. Движко-зависимые тесты (Godot example)

```bash
# Headless запуск Godot
godot --headless --path . --script res://tests/run_tests.gd

# Проверка компиляции C#
dotnet build
```

### 11.3. Формат результатов

Результаты сохраняются в XML (NUnit 3 Test Results XML или xUnit xml):

```xml
<?xml version="1.0" encoding="utf-8"?>
<test-run id="2" testcasecount="116" result="Passed" total="116" passed="116" failed="0">
  <test-suite type="Assembly" name="CultivationGame.Tests" result="Passed" total="116" passed="116">
    <test-suite type="TestFixture" name="BodyPartTests" result="Passed">
      <test-case name="Constructor_SetsPartType" result="Passed" duration="0.001" />
      <test-case name="TakeDamage_ReducesRedHP_70Percent" result="Passed" duration="0.002" />
    </test-suite>
  </test-suite>
</test-run>
```

### 11.4. Анализ результатов

| Метрика | Критерий успеха |
|---------|-----------------|
| Total | Общее количество тестов |
| Passed | Должно быть = Total |
| Failed | Должно быть = 0 |
| Inconclusive | Должно быть = 0 |
| Duration | Общее время (< 5 сек для unit-тестов) |

---

## 12. Отладка проваленных тестов

### 12.1. Алгоритм

1. **Прочитать сообщение ошибки** — Expected vs Actual.
2. **Запустить с отладчиком** — поставить breakpoint в тесте.
3. **Добавить diagnostic output** — `Console.WriteLine` промежуточных значений.
4. **Проверить типичные причины** (см. таблицу ниже).

### 12.2. Типичные причины провалов

| Причина | Симптом | Решение |
|---------|---------|---------|
| Формула изменена | Expected ≠ Actual | Обновить тест под новую формулу |
| Integer truncation | `(int)(1 * 0.7) = 0` | Использовать `Math.Max(1, ...)` |
| Мок не настроен | NullReferenceException | Проверить мок в SetUp |
| Namespace mismatch | CS0246 | Проверить `using` |
| Reference missing | CS0246 | Добавить сборку в references |
| Race condition | Тест проходит по отдельности, но падает в группе | Изоляция: проверить глобальное состояние |

### 12.3. Пример с диагностикой

```csharp
[Test]
public void TakeDamage_ReducesRedHP_70Percent()
{
    // Arrange
    var part = new BodyPart(BodyPartType.Torso, 100);
    Console.WriteLine($"[TEST] Before: RedHP={part.CurrentRedHP}");

    // Act
    bool result = part.TakeDamage(7, 3);
    Console.WriteLine($"[TEST] After: RedHP={part.CurrentRedHP}, result={result}");

    // Assert
    Assert.AreEqual(93, part.CurrentRedHP);
}
```

---

## 13. Архитектурные принципы тестов

### 13.1. Новый подход (vs Legacy)

| Принцип | Реализация |
|---------|------------|
| Создание объектов | `new Service(mockDeps)` — чистый C# |
| DI | `TestContainerBuilder.Build()` или ручной конструктор |
| События | `EventBusTestHelper.CreatePair<T>()` |
| Async | `async Task` (нативный C#) |
| Mock | Реализация интерфейсов, не наследование |
| Структура | По модулям: `Tests/Modules/Combat/`, `Tests/Core/` |

### 13.2. Изоляция тестов

```csharp
// ❌ ПЛОХО — глобальное состояние
private static int counter = 0;

// ✅ ХОРОШО — изолированное состояние
private int counter;

[SetUp]
public void Setup()
{
    counter = 0;
}
```

### 13.3. Очистка ресурсов

```csharp
[TearDown]
public void TearDown()
{
    // Отписка от событий
    if (_service != null)
    {
        _service.OnQiChanged -= OnQiChangedHandler;
    }

    // Освобождение ресурсов
    _service?.Dispose();
    _service = null;
}
```

### 13.4. Использование try-finally

```csharp
[Test]
public void Test_WithCleanup()
{
    var resource = AllocateResource();

    try
    {
        // Тест
        Assert.IsNotNull(resource);
    }
    finally
    {
        // Гарантированная очистка
        ReleaseResource(resource);
    }
}
```

---

## 14. CI/CD интеграция

### 14.1. Минимальный pipeline

```bash
# 1. Сборка
dotnet build

# 2. Тесты
dotnet test --logger "nunit;LogFilePath=test-results.xml"

# 3. Проверка движка (если есть движко-зависимые тесты)
godot --headless --path . --script res://tests/run_tests.gd

# 4. Анализ покрытия (опционально)
dotnet test --collect:"XPlat Code Coverage"
```

### 14.2. Критерии успеха CI

- ✅ `dotnet build` без ошибок и предупреждений.
- ✅ Все тесты прошли (Failed = 0).
- ✅ Покрытие ≥ 80% для core-модулей (опционально, но желательно).

---

## 15. Покрытие тестами

### 15.1. Метрики

| Метрика | Цель |
|---------|------|
| Line coverage | ≥ 80% для core |
| Branch coverage | ≥ 70% для core |
| Method coverage | 100% для публичных методов с логикой |

### 15.2. Что НЕ нужно покрывать тестами

- Автосвойства (get-only, set-only без логики).
- Trivial методы (одна строка return).
- Движко-специфичный код (UI, сцены) — покрывается скриншот-тестами, не unit-тестами.

---

## 16. Текущий прогресс

| Этап | Статус | Файлов | Тестов |
|------|--------|--------|--------|
| T1.1 Инфраструктура | ✅ | 2 | 1 |
| T1.2 TestUtilities (моки) | ✅ | 7 | 0 |
| T2.1 GameConstants + LevelSuppression | ✅ | 2 | 18 |
| T2.2 BodyPart + BodyDamageCalculator | ✅ | 2 | 22 |
| T2.3 QiBuffer + QiRegen + QiBreakthrough | ✅ | 3 | 18 |
| T2.4 DamageCalculator + DefenseProcessor | ✅ | 2 | 20 |
| T3.1 QiService + TechniqueCapacity | ✅ | 2 | 18 |
| T3.2 BuffCalculator + BuffService | ✅ | 2 | 13 |
| T3.3 TileMapService + DestructibleService | ✅ | 2 | 17 |
| T3.4 BodyService | ✅ | 1 | 6 |
| T4.1 P2 вторичные модули | 🔲 | 7 | ~35 (план) |
| T4.2 P3 UI/Save | 🔲 | 4 | ~15 (план) |
| T4.3 Integration + Balance | 🔲 | 5 | ~27 (план) |
| **ИТОГО** | | | **~116 / 210** |

---

## 17. Связанные документы

- `AI_DEVELOPMENT_WORKFLOW.md` — workflow тестирования в AI-цикле.
- `ALGORITHMS.md` — формулы для верификации баланса.
- `01_architecture/DI_AND_EVENTBUS.md` — DI, моки, шина событий.
- `01_architecture/MODULE_STRUCTURE.md` — структура модулей (зеркалится в тестах).
