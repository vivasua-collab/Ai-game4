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

### 0.1. Реестр env-хуков (41)

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
| `GODOT_SAVELOAD_DEBUG=1` | SaveLoadSimDebug | Ревью-R11 → R17: Save/Load round-trip — типизация (IncludeFields), честный success, РЕАЛЬНЫЕ мутации до/после (анти-тривиальность), R17: 19 блоков + R17-ключи 11/11, integrity-мутации 12/12; Этап-3 (adit_10_20) шаги 9-12: «{}»/без world_time/без player/версия из будущего → отказ ДО RestoreState (домены нетронуты), фантом-каталог (HasSave=main.json), честное удаление (нет→false, да→true, повтор→false), fail-closed сброс (AggregateException, домены после исключения); Этап-4: round-trip 22 блока (+npc_groups) |
| `GODOT_STAGE4_DEBUG=1` | Stage4SaveLoadSimDebug | Этап-4 аудита (20.10): ПОЛНЫЙ GameSession.LoadGame-путь (QA-требование аудита «неуспешная загрузка через полный GameSession, а не только тесты агрегатора») — (1) маркеры: камни+QA-группа(лидер/участник/Patrol/маршрут 3тц)+инвариант спавнер==реестр → Save; (2) мутации после Save; (3) pre-flight P1: три битых слота (v2 без quests / без npc_groups / версия+99) → RejectedByPrecheck, State=Playing, мир НЕ тронут; (4) транзакционный откат P1: порча npc-блока (bodyPartCount>массивов — проходит pre-flight, падает в RestoreState) → RolledBack, до-загрузочный мир восстановлен из снимка (НЕ из сейва), спавнер синхронизирован; (5) успех: Load(A) → Success, камни==сейв, QA-группа восстановлена (id/лидер/состав/маршрут); (5b) BUG-013: спавнер==реестр, все живые NPC в трекере; (6) DespawnNPC восстановленного → реестр/трекер/провайдеры чисты; (7) спавн после загрузки согласован. Сим №22 в qa_regression.sh |
| `GODOT_STAGE5_DEBUG=1` | Stage5CombatTruthSimDebug | Этап-5 аудита (22.10): тело = единый источник истины смерти + сплит по способностям части + унифицированный зачёт убийств. Регрессии аудитора: (1) BUG-016: сердце (MaxBlackHP=0) при прямом попадании 10 теряет ровно 10 красной (black=0), торс сохраняет сплит 70/30 (−7/−3); (2) BUG-015: удар 50 в голову живой цели НЕ убивает (red 50→15, ложной смерти нет: kill/death-событий 0), повторный 50 → СМЕРТЬ подтверждена телом, EnemyKilledEvent/NPCDeathEvent(killer=player_0)/труп — РОВНО по одному; (3) сердце: два удара по 50 (80 HP) убивают (порог ≥50 не срабатывает на живом теле); (4) консистентность DamageService: result.IsFatal == !IsEntityAlive после каждого удара (овекилл-каскад добивает через 3000-удар); (5) BUG-017: РЕАЛЬНАЯ DoT-цепочка — ApplyBuff(elemental_poison, sourceId=player) → тик → смерть волка: EnemyKilledEvent, NPCDeathEvent(killer=«player_0» — атрибуция, не «dot:…»), труп, квест «Охота на волков» +1 → добивание 2 удара → Completed 3/3 без двойного зачёта; (6) реальный бой через CombatService.ExecuteAttack: смерть NPC завершает бой (IsInCombat=false), зачёт ровно один; (7) игрок: голова-50 → жив, PlayerDeathEvent=0; сердце-10 → −10 полный; торс-10 → −7/−3. Сим №23 в qa_regression.sh |
| `GODOT_STAGE6_DEBUG=1` | Stage6TradeEconomySimDebug | Этап-6 аудита (22.10): инвентарь/торговля/экономика. Регрессии аудитора: (1) BUG-018 инварианты предметов: TryAddItem контролируемый отказ при MaxStack ≤ 0 (мусорных слотов нет, событий с count ≤ 0 нет — прежде бесконечная рекурсия подбора), регистрация в каталог валидирует определения (битое отклонено/валидное принято), RestoreState каталога отсекает битые записи; (2) BUG-019 честный кошелёк: CurrencyChangedEvent.Delta = ФАКТ (Add(10)@Max-3 → Delta=3, баланс не переполняется), TradeCompletedEvent.Price = зачисленное при потолке (TrySell@Max-3 → 3, руда изъята); (3) BUG-020 long-котировки: int-границы в long без отрицательных (21474836→1073741800, аудит-кейс 21474837→1073741850), непредставимые суммы → IsRepresentable=false (покупка И продажа), граница покупки 2147483640; (4) BUG-021 RestoreState колец: валидный слот ×10 восстановлен, битые кольца (Tier<0, MaxVolume≤0) и неизвестные каталогу предметы отклонены с журналом, переполненный 500-стак в кольцо не попадает (TryRetrieve 10 из сохранённых 10). Сим №24 в qa_regression.sh |
| `GODOT_STAGE7_DEBUG=1` | Stage7QuestTruthSimDebug | Этап-7 аудита (22.10): квесты/диалоги/NPC. Регрессии аудитора: (1) BUG-022 покупка ≠ сбор: РЕАЛЬНЫЙ TryBuy руды — предметы в инвентаре, источник merchant_buy, прогресс квеста не шевелится; РЕАЛЬНАЯ добыча (жила/ harvest-command) двигает на фактическое количество, ItemAddedEvent на добычу РОВНО ОДИН (дабл-адд command+факт вылечен); батарея источников — в ОТДЕЛЬНОМ квесте с таргетом 50 (урок конструкции: на реальном квесте с таргетом 5 батарея упиралась в потолок — world/pickup закрывали квест, loot/craft оставались без запаса): 6 не-сборов (merchant_buy/quest_reward/storage_transfer/belt_transfer/equip_return/starting_gear) не считаются, world/pickup/loot/craft — считаются (+4, квест жив 4/50); побочный инвариант: реальный квест закрывается РОВНО на таргете 5/5 без перелива; (2) BUG-023 снимок итерации: два активных квеста одной цели (KillEnemy wolf ×1) — ОДНО EnemyKilledEvent закрывает ОБА, оба автонаграждены (прежде индексный обход терял второй); (3) BUG-024 превалидация наград: неподдержанный тип (Experience) → выдача отклонена ЦЕЛИКОМ, AreRewardsGranted=false (повтор после починки контента возможен); починка (замена на Qi) → выдано ровно один раз, повтор false; (4) GAP-025 talk-гейт: NPCInteractedEvent(trade/attack/gift) НЕ двигает TalkToNPC, реальный E-путь «talk» двигает и завершает. Сим №25 в qa_regression.sh |
| `GODOT_STAGE8_DEBUG=1` | Stage8TechniqueTruthSimDebug | Этап-8 аудита (22.10): техники/Ци/эффекты. Регрессии аудитора A–F: (A) ST8-01 вырожденное направление AoE — Cone/Semicircle/Line с прицелом в клетку кастера поражают 0 целей из 3 в радиусе (прежде — «круг»), направленная геометрия точна (конус 45° — только front, полукруг — front+side, линия — только front), каст-уровень — отказ «Прицел в свою клетку» без AoeImpactEvent; (B) ST8-02 отказ выпуска удержанной техники (Combat «Цель исчезла», Healing «Тело не ранено») — удержание живёт, 0 событий, кулдаун/мастерство нетронуты, повторный выпуск применяет эффект, INT +0.001; (C) ST8-03 один TechniqueUsedEvent на применение (AoE-залп — 1, Healing — 1), синхронный AttackRejected (pending basic_attack) — RefundUse, 0 событий, INTΔ=0, удержание живёт, повторный выпуск; (D) ST8-04 прерывания §5.4 — смерть в зарядке → CancelCharge("death") refund>0, смерть/стюн/медитация при удержании → Dissipate refund=50%, стюн в зарядке → CancelCharge("stun"), world-reset снимает зарядку (world_reset) и ауру; (E) ST8-05 щит: пул<MIN_QI_FOR_BUFFER → отказ (не активен, 0 событий, 0 поглощения), резервный режим (пул≥MIN, invested=0) реально поглощает, Defense при низком пуле → «Недостаточно Ци для щита» с сохранением удержания, после регена — успех с инвестицией 72; (F) ST8-06 decay-возврат от post-decay остатка (60→45→22, boundary 51→50→25, 52→51 живёт). Сим №26 в qa_regression.sh; 30 ассертов, VERDICT PASS 30/30 (лог: checkpoints/10_22_stage8_technique.log) |
| `GODOT_STAGE9_DEBUG=1` | Stage9InventoryTruthSimDebug | Этап-9 аудита (23.10): инвентарь/экипировка/переносы. ST9-01 (крафт: списания до отказа — транзакционный откат CraftingService; воспроизведение hostile-подписчиком wood 10→7), ST9-02 (экипировка: swap/двуручное/гейты/ампутация/round-trip/провайдер — 24 ассерта), ST9-03 (пояс/зарядник/переполнение: суммы по ВСЕМ хранилищам инвариантны, stale SlotId безопасен), DUR — прочность не подключена к игровому пути (GAP). Сим №27; лог: checkpoints/10_22_stage9_inventory_report.log |
| `GODOT_STAGE10_DEBUG=1` | Stage10WorldLifecycleTruthSimDebug | Этап-10 аудита (23.10): жизненный цикл мира. ST10-01 (очередь истощённых ресурсов: изоляция миров — ResourceService IWorldResettable), ST10-02 (TTL трупов по игровому возрасту, wiring NPCModule.Tick), ST10-03 (гигиена групп при смерти: лидер → следующий, пустая → расформ), ST10-04 (BulkAdvanceTicks: 1 hitched, календарные переходы Month/Year независимы на первом естественном тике без фантомов — ST10-04b). 16 ассертов; лог: checkpoints/10_23_stage10_lifecycle.log |
| `GODOT_STAGE11_DEBUG=1` | Stage11BodyTruthSimDebug | Этап-11 аудита (23.10): тело/травмы/морфологии/восстановление. ST11-001 респавн = полное лечение RedHP (HealPartToFullRed; префикс — голова 35/50), ST11-002 reset-контракт тела (BodyService/SeveredDebuffSystem IWorldResettable; раны НЕ переживают New Game), ST11-003 морфология не блокирует допустимые слоты (альтернативная анатомия: гарпия-крылья, паук-хелицеры; змея — BD-23; матрица 10 морфологий × слоты = 0 расхождений), ST11-006 round-trip тела + повреждённые блоки, ST11-007 приживление разблокирует слот + LoadGame пересобирает блокировки/дебаффы по телу (IPostLoadSync); GAP: HealthRegen лечит только торс (HoT-распределение — за владельцем), Amorphous Core не фатален (§5.3.1). 27 PASS/0 FAIL/2 GAP/7 REF; лог: checkpoints/10_23_stage11_body_report.log |
| `GODOT_STAGE12_DEBUG=1` | Stage12ProgressionTruthSimDebug | Этап-12 аудита (23.10): развитие/баффы/Ци/культивация. ST12-001 насыщающее AddQi (префикс: CurrentQi = −9.22e18 от long.MaxValue), ST12-002 штраф сердца = производная флага (переживает смену уровня/прорыв; приживление снимает; идемпотентен), ST12-003 догоняющие тики DoT/HoT (5×1с == 1×5с, потолок 5 — анти-всплеск), ST12-004 неизвестный buffId = отказ (не AttackBoost +10%; батарея 15 реальных ID жива), ST12-005 по-записная валидация статов (NaN/±∞/отрицательные/сверхкапа отклоняются; дробные дельты round-trip), ST12-006 проводимость: только перки (спец §A.4, временные conductivity-ID отказаны); прогрессия: дельты/капы, сон §6 (4ч минимум, 0.025/ч, пороговый шаг), медитация INT, NPC↔NPC без дельт, требования прорыва = BREAKTHROUGH_MODELS §4.3-4.4; GAP ST12-007: требования прорыва (1100/5188) > MaxQi (1000) — успешный прорыв недостижим, решение за владельцем. 29 PASS/0 FAIL/1 GAP; лог: checkpoints/10_23_stage12_progression_report.log |
| `GODOT_STAGE13_DEBUG=1` | Stage13NpcAiTruthSimDebug | Этап-13 аудита (23.10): ИИ NPC/навигация/группы/животные. ST13-001 grid-based A* (NPC_AI_SYSTEM §6: 8-направлений, octile, детерминированный tie-break; прямая-до-препятствия; re-path; пути нет → жадный шаг, без телепорта/зависания; ST13-001a КОРЕНЬ: Vector2f-направления — неосевые направления замирали из-за (int)-транкации при объявлении, NPC двигались только по осям), ST13-002 приоритет бой>группа>индивидуальное (GROUP_SYSTEM §6; overlay не подавляет Attacking/Fleeing; возврат к задаче после боя), ST13-003 пересмотр цели в бою каждый тик (строго большая угроза → ретаргет с ЛЮБОЙ цели, включая игрока; равная → текущая сохраняется; детерминированный tie-break min-ordinal; дизенгейдж при смене), ST13-004 месть животного каноническому атаковавшему (игрок ИЛИ NPC; DoT-фантом → OriginalAttackerId; атакующий умер → de-aggro; смерть игрока снимает только месть к игроку), ST13-006 черты Loyal (−50% idle) / Treacherous (Attitude<Neutral → +40% patrol). 23 PASS/0 FAIL/2 GAP (ST13-005 потеря обнаруженной цели за препятствием — GAP; перки/лестница Attitude — GAP); лог: checkpoints/10_23_stage13_report.log |
| `GODOT_STAGE14_DEBUG=1` | Stage14InteractionFocusTruthSimDebug | Этап-14 аудита (23.10): ввод/фокус/модальные окна. ST14-001 фактический клавиатурный фокус (GuiGetFocusOwner: LineEdit/TextEdit/CodeEdit/SpinBox глушат ВСЕ игровые sticky/движение/хотбар — буквы печатаются в поле; прежде ~24 действия опрашивались безусловно), ST14-002 единый предикат модальности (WorldMouseBlockedByModal = AnyModalWindowOpen ∨ DeathScreen для _UnhandledInput; SetOverUI = AnyModalWindowOpen — окно зарядника H и меню сейвов прежде пропускались: зум уходил в мир поверх планировочных окон), ST14-003 Esc по фактическому стеку открытых окон (_modalOpenSeq: попапы инвентаря → верхнее окно → пауза; прежняя жёсткая цепочка закрывала нижнее; инвариант паузы INP-1 сохранён), ST14-005 E над планировочными окнами не запускает скрытые взаимодействия, ST14-006 потеря фокуса приложения: release всех действий + нулевой кадр (held WASD/бег/ПКМ-длительность прежде залипали; OS-путь — физический PT-U06), ST14-004-edge узел-призрак завершает диалог честным EndDialogue (диагностика; прежде старый текст висел). 6/6 PASS; лог: checkpoints/10_23_stage14_interaction_focus_report.log |
| `GODOT_STAGE15_DEBUG=1` (+ `GODOT_STAGE15_COLD=save\|load` — пара процессов) | Stage15GeneratorIntegrityTruthSimDebug | Этап-15 аудита (23.10): генераторы/валидация/дедуп/ID/реестры. ST15-001 NPC-экипировка на «Матрёшке» (WeaponClassId/MaterialId/Pen/Range/SPM; броня по слотам; офхенд 1H; зарядник — легаси по канону; легаси-методы оружия/брони живыми путями не вызываются), ST15-002 валидатор (unknown class оружия = Critical-отказ; NaN отвергается; аксессуар = документированный skip; enum-гарды до таблиц — Grade=99 диагностика, не крэш), ST15-003 Clean(FormationRegistry) реально удаляет (Remove-API; идемпотентность; уник выживает), ST15-004 fingerprints = все поведенческие поля + инвариантная культура + R-точность, ST15-005 ID: полный 32-бит хвост сида (seed:x8) + safe-register (коллизия Id с другим контентом не подменяет запись), ST15-006 Heavy достижим в Generate() (вес 1/11; гейт L6+), ST15-007 кламп уровня [1..10] + явный ArgumentException на неизвестный subtype, ST15-008 FormationRegistry : IWorldResettable (координатор сброса: 37 доменов) + подтверждение R17 каталога (fresh-restore, двойной ResetWorld, seed=0 контракты, cold-пара НОВЫХ процессов save→load: маркер переживает, новые ID уникальны). 27/27 PASS; лог: checkpoints/10_23_stage15_generator_integrity_report.log |
| `GODOT_STAGE16_DEBUG=1` | Stage16RenderAnimationSyncTruthSimDebug | Этап-16 аудита (23.10): рендеринг/анимация/синхронизация визуального состояния. ST16-001 one-shot-таймеры игрока не переживают death/respawn (melee/ranged/hit/пауза-вариант; death-hold по PlayerDeathEvent), ST16-002 живые состояния (meditate/walk/run/idle через FrameState + PNG-vs-fallback по диску + D2-ряд qa_grid_5x6 + GAP-реестр будущих фаз cast G3/guard G4/dash-sleep-breakthrough G5), ST16-003 канон звериного melee-ключа animal_{species}_attack (не melee!) + цепочка npc_{role}_melee→npc_base_melee, ST16-004 гарантированно живой зверь + доменный сигнал движения (AnimalEntity.Target != null) vs кадровая дельта (мерцание walk 1 кадр из 15); фактический walk-рейшо по AnimalAnimInfo.Moving — headless _Draw ВЫПОЛНЯЕТСЯ в Godot 4.7 (эмпирический пруф Этапа-16: G2-кролики/D2b-резолвы; walk-сигнал ≠ PNG-доставка — недоставленный walk-лист легитимно резолвится в процедурный static, контракт G1), ST16-005 ZIndex формации ≤ Objects + порядок сиблингов (контур под телами/игроком), ST16-006 Shield: one-shot каста ≠ persistent Qi-буфер (AbsorbDamage-репаблиш не стакает; деактивация щадит чужой one-shot), ST16-007 per-entity кэши NPC/Animal-рендереров чистятся после деспавна/смерти (циклы спавн→резолв→удаление), ST16-008 невалидная геометрия 30×30 → PrintErr+fallback, негативный кэш 1 попытка/ResetCache, реестр 13/6/5, ST16-009 синхрон тела/MainHand (один AttackIntentEvent → аниматор+замах; 0.42с оба; unknown-класс→sword). |
| `GODOT_STAGE17_DEBUG=1` (+ `GODOT_STAGE17_COLD=save\|load` — пара процессов) | Stage17InitConfigStaminaTruthSimDebug | Этап-17 аудита (24.10): инициализация/конфигурация/настройки/полная выносливость. ST17-001 реализация и DI-проводка IStaminaService (Core-интерфейс существовал с 2026-05-28 БЕЗ реализации; конкретный класс в Modules/Player + регистрация в PlayerModule + CoreValidationPhase-резолв), ST17-002 инварианты 1000‰ (новая игра = полный ресурс; Spend 1–1000 с отказом на некорректных аргументх и без частичного списания; реген-аккумулятор: короткие частые вызовы == один длинный; кламп к максимуму без переполнений), ST17-003 жизненный цикл (сон по PlayerConfig.SleepStaminaRecoveryPercentPerHour; Save/Load точный; cold-пара НОВЫХ процессов save→load — частичное значение из файла; ResetWorld → полный; death/respawn НЕ восстанавливает автоматически — задокументировано; легаси-сейв без stamina-блока грузится — OptionalBlocksV1), ST17-004 GameSettings (безопасный JSON-разбор вместо буквальных Contains: compact/отступы/порядок/неизвестные поля/повреждённый файл→дефолты+диагностика; Set* гарантирует загрузку ДО записи — пользовательские значения не затираются дефолтами; повреждённая загрузка ретраебельна), ST17-006 повторный старт идемпотентен + InputMap идемпотентен, ST17-010 статус остатков ST11-006res (BodyService size-enum валидация) и ST12-003res (BuffTickProcessor дробный остаток — неравные шаги эквивалентны равным). |
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
| `GODOT_CHARGERQA_DEBUG=1` | ChargerSimDebug | R37-c (баг-репорт 23.09 «Зарядник не принимает камни Ци»): полный путь зарядника — A) инфраструктура (InputMap-действие charger_window/H, окно ChargerWindow создано, камни сидированы), B) честный отказ вставки без зарядника (тост-причина, предмет не потерян), C) генерация L3+ → надевание в Belt → IsChargerEquipped, гейт пояса (зарядник в Belt НЕ включает BeltSlotRow — раньше включал: баг «лекарство добавляется, камни молча отвергаются»), режим домена синхронизирован экипировкой (On), D) окно H открывается, контент виден, E) вставка камня через мост (слот-адресно R10: Guid кучки; предмет покинул инвентарь; MaxQi камня = канон 1024×см³, НЕ доменный дефолт BaseQi×mult), F) домен качает (камень истощается, буфер/ядро практика растут — метрика камня обязательна: TransferToPractitioner вне боя сливает буфер ядру, BufferQi≈0 не дефект), G) анти-дюп: частичный камень НЕ извлекается (тост-причина: стаки ItemId+Count без per-экземпляра), полный извлекается и возвращается, пустой «рассыпается» (не дюпается), H) сейв round-trip: MaxQi камня из моста не урезается reload'ом (баг RestoreState: доменный дефолт вместо сейв-поля), I) снятие зарядника → домен Off; настоящий пояс (armor_belt) → IsBeltEquipped=true (слоты 3-9 для лекарств — только с поясом). Скриншот: `GODOT_CHARGER_SHOT=<префикс>` (Xvfb + opengl3, без xauth — Xvfb вручную: `Xvfb :99` + `DISPLAY=:99` + `DOTNET_ROOT`). Сим №20 в qa_regression.sh |
| `GODOT_ANIMQA_DEBUG=1` | SpriteAnimSimDebug | G0 «Подготовка спрайтов» (директива 25.09): контур sprite-swap ДО доставки PNG — A) fallback: TryGetSheet(player_idle/walk)=null при отсутствии файлов (визуал идентичен процедурному), B) загрузчик: QA-плейсхолдеры _qa/ (полоса 6 кадров 384×64 + D2-сетка 5×6 384×320) через LoadSheetDirect — FrameCount/Rows/FrameSize/FrameAt-математика (прогресс→кадр, ряд row*N+col), тихий null на несуществующий путь, C) аниматор игрока: старт fallback (IsPng=false, Frame=0) → оверлей QA-листа → IsPng=true + прокрутка кадров; one-shot melee 0.42с (истекает), death-hold до NotifyRespawn, возврат fallback после сброса, D) оружие (I-11): ключи кэша sword|1|Common стабильны, размеры 32/48, PngHand/PngIcon=0 (PNG не доставлены), E) NPC (I-5): ResolveNpcAnim(moving/idle) → процедурный fallback, PNG-счётчик рендерера 0, F) звери (I-7): ResolveAnimalAnim → fallback (wolf-сэмпл; отсутствие зверей — WARN), G) реестр: 13 player + 6 npc + 5 animal. Регламент доставки — docs_v2/07_ui/SPRITE_DELIVERY.md; сим №21 в qa_regression.sh |
| `GODOT_AUDIT0922_DEBUG=1` | Audit0922SimDebug | Аудит 09.22 (внешний): инфраструктурные контракты — A) EventBus изоляция исключений + re-entrancy-дрен (P1-5: фан-аут не прерывается, стэл-очередь не протухает), B) санация имён слотов в Modules-layer SaveFileHandler (P1-8 path traversal), C) DI override prune: stale concrete-ключ + стражи мульти-форварда/last-wins/наследования ключа (P1-6), D) честный DeleteSave по слоям (P1-7), E) Trade/Corpse : IWorldResettable (P1-1/P1-4), F) ResolveAll — порядок = порядок регистрации, мёртвые регистрации не воскресают, ResolveAll<ISaveFileHandler> = ровно адаптер (P2-12, Фаза 4), G) startup fail-closed — AggregateException + заглушённый тик-луп + ретрай без «already initialised» (P2-13, Фаза 4), H) cycle detection по construction path с точным путём «A → B → A», без ложных срабатываний на цепочках (P2-14, Фаза 4), I) WorldDomainResetPhase fail-closed: исключение ResetWorld() домена = AggregateException наружу с именем виновника, все домены диагностируются за одну попытку, MarkAsCompleted недостижим (P1-9, Фазы 6/7/10), J) startup retry = resume from failure: успешно стартовавшие модули НЕ перезапускаются (SubscriberCount стабилен, StartCalls=1), провалившийся ретраится (P1-10, Фазы 7/10), K) DamageType.Pure не входит в Qi-буфер: Ци игрока/NPC не расходуется, поглощение 0; контроль Physical — Ци расходуется (P1-12, Фазы 9/10), M) Фаза 11 — time pipeline: честный catch-up TickCatchUpClock (инвариант учёта: моделирование+bulk+остаток = Σ(delta×speed); hitch 2с@Quick → 30/30 тиков), BulkAdvanceTicks календарь точен, ResetWorld сбрасывает Quick/Paused → Normal, фантомных Day/Month/Year после сброса мира нет (P1-13/P1-14/P1-15), N) Фаза 12 — item identity: ID уникальны при коллизионных сидах 5/1005 (counter-based), определения не подменяются, category-index без stale при смене категории, RestoreState заменяет каталог, ResetWorld чистит + ре-сеет канон (P1-16/P2-31/P2-32), O) Фаза 12 — equipment гейты: RequiredCultivationLevel=9 при уровне 1 → отказ (уровень 9 → успех), StatRequirements STR≥50 → отказ, одноручное → WeaponOff разрешено, двуручное → запрет (P1-17/P1-18), P) Фаза 12 — StorageRing persistence: ISaveable+IWorldResettable контракты, warm-load без утечки содержимого мира A, cold-load восстанавливает сейв, реактивация не чистит (P1-19); Этап-6 (22.10, BUG-021): фикстура регистрирует зонду qa_p_probe в каталог — RestoreState колец валидирует слоты по каталогу и честно ОТКАЗЫВАЕТ неизвестные предметы (контракт «снятое ≠ стёртое» не менялся), Q) Фаза 14 — stat progression: продюсеры §5.1/§5.2 (удар→STR, уклонение→AGI, урон→VIT, техника/медитация→INT), порог max(1,floor(stat/10)), ConsolidateSleep по §6.2/§6.4 (8ч → +0.2 + остаток; <4ч — нет; delta≥thr → +1), капы INT=15/отрицательные/вторичные отвергаются, sleep-pipeline StartSleep(8ч)→480 тиков→авто-вейк+закрепление (P1-20/P2-43/P2-49), R) Фаза 14 — Revive оживляет тело (vital Head/Heart: HealPart/Reattach), dash-цель клэмпится в границы мира (P2-44/P2-45), S) Фаза 11 P2-бэклог — автосейв по МИРОВЫМ тикам (мини-DI: фейк ITimeService/ISaveService/Playing-сессии + SaveModule; разностная схема 29→59=30 мин → сейв, ResetWorld обнуляет отсчёт — process-остаток 10029 не триггерит, первый автосейв нового мира ровно на 30-м тике, bulk-скачок не теряет каденцию), единая pause-authority (Session.Pause → TimeService.Paused на реальной сессии), Resume восстанавливает скорость до паузы (Fast), IsPaused ⇒ DeltaTime==0, Load канонизирует три представления (дата первична, TickCount=минуты с 06:00 дня 1, рассинхронный блок 777/42 → пересчёт), WorldTime/RestoreState отвергают month=13/day=0/hour=29/minute=-10/год до эпохи (ArgumentOutOfRangeException → битый сейв = отказ загрузки), WorldConfig без мёртвых Start*/AutoSaveIntervalTicks/BaseTickRate (рефлексия) (P2-26…30 + P3-11), T) Фаза 12 P2-бэклог items/trade — валютная saturating-арифметика (Add у верхнего края int → int.MaxValue, SetBalance(-5) → 0), тотал сделки в long (покупка 6e9 > int.MaxValue → честный отказ ДО списания, SpendCalls=0; продажа 2.85e9 → насыщение int.MaxValue — не молча 0 и не минус), Inventory RestoreState валидирует слоты (count>0, ≤MaxStack, Enum.IsDefined category/rarity, ItemId в БД, _itemCountCache не отравлен), переноска — ВЛАДЕЛЬЧЕСКИЙ КАНОН 50/100 (user request 2026-08-22; P2-36 отклонён by-user: прямые указания владельца выше аудитора и документации; T4a/T4b — страж значений) (P2-33/34/35 + P2-36 by-user). Прогон до фиксов = runtime-подтверждение дефектов (чекпоинты 09_22_r32–r34 + фазы 11–14: 32 DEFECT + R36-a: 12 S-DEFECT + R36-b: 5 T-DEFECT, логи 09_22_r35_prefix/postfix + 09_22_r36a_prefix/postfix + 09_22_r36b_prefix/postfix), после — регрессионный страж; сим №19 в qa_regression.sh |

### 0.2. Прогон регрессии — `tools/qa_regression.sh` (2026-09-15)

Раннер регрессии — `bash tools/qa_regression.sh [sim…]` из корня репо (без
аргументов — все симы реестра; аргумент — имя сима `COMBAT_SIM` или пара
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
