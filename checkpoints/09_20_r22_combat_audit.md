# Чекпоинт: R22-EP2-A — read-only аудит боевой системы (физика + Ци)

**Дата:** 2026-09-20 06:35 UTC
**Сессия:** R22-EP2-A (subagent, combat audit)
**Тип:** audit (read-only, НИКАКИХ правок кода)

---

## Контекст

Пользователь готовится к локальному плейтесту боевки после крупных правок R21:
9d974e2 (пассивный щит Ци + броня §5.2 + спрайты) и 518e32a (attack-speed
readiness-модель + CLASH-парирование + per-attacker pending-касты). Цель —
найти дефекты ДО плейтеста. Прочитаны: все 17 файлов Modules/Combat/,
Qi-проводка (QiService/QiBufferService/QiDataProvider/QiRegenCalculator),
PlayerTechniqueCaster/TechniqueSlotService, BodyService/BodyDamageCalculator
(боевые пути), проводка NPC/Animal-атак (NPCModule/NPCCombatAdapter/
AnimalService/NPCAIService), BodyModule (DoT-проводка), BuffService/
BuffTickProcessor (DoT-тики), контракты. Доки: COMBAT_SYSTEM (§1.4/§2/§7/§8.2),
QI_SYSTEM (§6/§8), TECHNIQUE_SYSTEM (§5.3), ALGORITHMS (§5.2), точечно
TECHNIQUE_EFFECTS.

## Сводка

**21 дефект: 3 P1 / 6 P2 / 12 P3.**

P1 ломают плейтест так или иначе почти в каждой сессии с боем: глобальный
1v1-лок боя (унаследован, R20-#8), потеря Ци+кулдауна при отклонении
заряженной техники против доков §8.2, резолв кастов после конца боя.
P2 — баланс/честность: асимметрия CLASH, двойная цена Defense-техник,
фантомные цели от DoT, невозможность умереть от яда.

---

## Дефекты P1

```
[CMB-1] P1 — Заряженные техники Ци гейтятся на readiness ОРУЖИЯ и тратят её,
        а при отклонении Ци+кулдаун+мастерство уже безвозвратно потеряны
Файл: game/src/Modules/Combat/CombatService.cs:562-568 (гейт readiness до
      charged-ветки), :609-616 (ConsumeReadiness в charged-пути);
      game/src/Modules/Player/PlayerTechniqueCaster.cs:190-195 (CompleteUse
      ДО AttackIntentEvent)
Что: ExecuteAttack проверяет readiness (562) и расходует её (613) для
     isCharged-атак техник; PlayerTechniqueCaster.FireTechnique вызывает
     TechniqueService.CompleteUse (кулдаун+мастерство) ДО публикации
     AttackIntentEvent — до гейтов CombatService.
Влияние: игрок зарядил технику (Ци дренировано тиками зарядки), жмёт
     выпуск — если readiness < порога, у игрока «уже кастует» (только что
     был Space-удар с 0.5с pending) или действует CMB-2 (чужой бой) —
     атака отвергнута: Ци ПОТЕРЯНО, кулдаун НАЧАЛСЯ, мастерство потрачено,
     эффекта нет. COMBAT_SYSTEM.md:404 прямо обещает «заряженные техники
     готовность не расходуют дополнительно».
Фикс: для isCharged пропускать readiness-гейт и ConsumeReadiness (доки
     §8.2); CompleteUse переносить после Accepted (или рефанд кулдауна при
     Rejected).
```

```
[CMB-2] P1 — Глобальный одиночный бой (1v1-синглтон) блокирует атаки игрока,
        пока идёт ЛЮБОЙ чужой бой на карте; таймаута нет
Файл: game/src/Modules/Combat/CombatService.cs:287 (StartCombat early-return
      при _isInCombat), :550-555 (гейт IsParticipant «бой 1v1»);
      game/src/Modules/Combat/CombatModuleServices.cs:66 (MaxCombatDuration=0);
      game/src/Modules/NPC/NPCModule.cs:203 (NPC-интенты);
      game/src/Modules/NPC/NPCAIService.cs:562-599 (RetaliateOrFlee → Attacking)
Что: CombatService хранит ОДИН бой на весь мир. Эмерджентная стычка
     NPC-vs-NPC (месть через RetaliateOrFlee) или волк-vs-NPC занимает
     слот: StartCombat игрока молча игнорируется, ExecuteAttack отвергает
     «не участник текущего боя». MaxCombatDuration=0 — конца боя нет до
     смерти/бегства одной из сторон (может быть минутами при per-part HP).
Влияние: игрок давит Space/Z — тост «не участник текущего боя (бой 1v1)»,
     драться нельзя, пока где-то в мире два NPC выясняют отношения. Это
     подтверждённая гипотеза R20-#8 — корень не устранён R21.
Фикс: приоритет боя с игроком (чужой бой вытесняется/переносится) либо
     переход на per-pair Dictionary боёв; как минимум — таймаут
     MaxCombatDuration для NPC-vs-NPC боёв.
```

```
[CMB-3] P1 — Касты резолвятся ПОСЛЕ конца боя: урон по мёртвому/вне боя,
        смерть игрока после EndCombat не публикует CombatEndedEvent
Файл: game/src/Modules/Combat/CombatService.cs:1084-1107 (batch завершённых
      кастов: ApplyTechniqueImmediately без проверки _isInCombat),
      :707-738 (BuildAndExecuteDamageRequest без гейта _isInCombat),
      :313-315 (EndCombat no-op при !_isInCombat)
Что: в UpdateTimer снапшот `completed` резолвит ВСЕ касты партии, даже если
     первый резолв убил защитника и вызвал EndCombat (который очистил
     _pendingCasts и _isInCombat=false). Снимок обходит очистку.
Влияние: обоюдный замах в один тик: каст игрока убивает волка → бой
     завершён Victory → каст волка ВСЁ РАВНО срабатывает и бьёт игрока вне
     боя; если этот удар убивает игрока — _currentStage=Defeat, но
     EndCombat уже не сработает: CombatEndedEvent НЕТ (NPCCombatAdapter
     не чистит стейты), лог боя фиксирует «Victory» при мёртвом игроке.
     Урон по мёртвому телу также возможен.
Фикс: в цикле `completed` проверять `!_isInCombat` → break; либо в
     BuildAndExecuteDamageRequest ранний return при !_isInCombat (кроме
     явного DoT-пути, который сюда не идёт).
```

## Дефекты P2

```
[CMB-4] P2 — CLASH асимметричен при одновременном замахе: парируется только
        удар ПЕРВОГО резолва, второй проходит полным
Файл: game/src/Modules/Combat/CombatService.cs:910-911 (clashParry на момент
      резолва), :1096-1105 (порядок партии = insertion order)
Что: оба участника в pending-касте → оба каста завершаются в одном тике;
     первый в `completed` (кто раньше вставил каст) видит второго «в замахе»
     → его удар парируется ×0.5; второй резолв видит словарь уже пустым →
     его удар проходит БЕЗ парирования. Прерывание каста уроном
     (OnDamageAppliedForCastInterrupt:249-257) для same-tick партии тоже
     обходится (снятие из словаря, но снапшот всё равно резолвит).
Влияние: «одновременная атака» (доки §1.4:44, §7.2) даёт не взаимное
     парирование, а монетку по порядку вставки: удар игрока (обычно первый)
     режется ×0.5, удар NPC проходит целиком.
Фикс: в партии сначала собрать все completed, вычислить mutualClash по
     исходному состоянию, потом резолвить; либо оба парируются, если оба
     были в замахе на начало тика.
```

```
[CMB-5] P2 — Окно «замаха» 0.5с у КАЖДОГО базового удара → CLASH срабатывает
        слишком часто; звери «парируют» без оружия (доки §7.2 требуют оружие)
Файл: game/src/Modules/Combat/CombatService.cs:619-643 (castTime default
      0.5f для npc_strike/укуса/кулаков → pending 0.5с), :898-911
      (clashParry без проверки экипировки защитника)
Что: любая атака без техники идёт через pending-каст ~0.5с (effectiveCastTime
     0.486с при проводимости NPC). Пока NPC «в замахе», каждый входящий удар
     игрока получает гарантированный Parry ×0.5 — окно замаха занимает
     ~25-50% каденции NPC. Волк в укусе «парирует» меч игрока зубами
     (проверки «есть оружие/щит» из §7.2 нет).
Влияние: значительная часть ударов игрока режется ×0.5 не по скиллу, а по
     таймингу тиков; «зубы парируют клинок» выглядит багом в плейтесте.
Фикс: clash-условие ужесточить (только оба начали замах в одном окне,
     напр. RemainingCastTime > 70% TotalCastTime); для парирования —
     требование WeaponMain/WeaponOff у защитника (beast → нет clash).
```

```
[CMB-6] P2 — Defense-техника платит Ци дважды: зарядка + отдельная инвестиция
        в буфер
Файл: game/src/Modules/Player/PlayerTechniqueCaster.cs:236-241 (invest =
      max(50, QiCost) → QiBufferActivateRequestEvent → QiBufferService.
      Activate → TryConsumeQi);
      game/src/Modules/Combat/TechniqueChargeService.cs:280-282 (зарядка уже
      дренировала QiCost)
Что: ChargedQi зарядки НЕ передаётся в буфер; активация берёт из ядра НОВУЮ
     сумму invest.
Влияние: Defense-техника с QiCost=50 фактически стоит ~100 Ци; экономика
     защитных техник вдвое дороже любой другой школы.
Фикс: передавать ChargedQi в QiBufferActivateRequestEvent (инвестировать
     накопленное, а не новое), либо явный учёт в доках.
```

```
[CMB-7] P2 — DoT-источник "dot:{buffId}" становится TargetId NPC (фантом):
        NPC без агро преследует и атакует игрока, leash не срабатывает
Файл: game/src/Modules/NPC/NPCAIService.cs:527 (RetaliateOrFlee(state,
      e.SourceId) без валидации), :597 (state.TargetId = sourceId);
      game/src/Modules/NPC/NPCModule.cs:166-182 (цель «не NPC» трактуется
      как игрок)
Что: BodyModule публикует DoT-тики как DamageAppliedEvent с SourceId
     "dot:combat_bleed" и т.п. NPC-7-фильтр фантомов есть ТОЛЬКО в
     GetTopThreatId (:208-231); прямой путь OnDamageApplied→RetaliateOrFlee
     не фильтрует. Кровоточащий NPC получает TargetId="dot:..." →
     ProcessNpcAttacks резолвит «не-NPC» как игрока (позиция игрока) →
     погоня/атака без агро; leash-гейт требует IsPlayer(TargetId) → false →
     погоня вечная.
Влияние: раненый игроком NPC с кровотечением гоняется за игроком через
     полкарты (ChaseSpeed 4.8 т/с), «отбиться» уходом нельзя.
Фикс: в RetaliateOrFlee валидировать sourceId (игрок/живой NPC), как в
     GetTopThreatId; dot-источники игнорировать.
```

```
[CMB-8] P2 — DoT (яд/кровотечение/горение) всегда бьёт в Torso → отравить
        насмерть НЕВОЗМОЖНО (смерть = только Head|Heart)
Файл: game/src/Modules/Body/BodyModule.cs:99-121 (DoT → DamageAppliedEvent
      с HitPart=Torso);
      game/src/Modules/Body/BodyDamageCalculator.cs:78-99 (IsFatalPart =
      Head|Heart)
Что: правило пользователя (worklog R20): «NPC умирают только в бою/от
     проклятий/ядов». DoT-тикиходят только в торс; торс не смертелен;
     CombatService.IsFatalHit тоже смотрит hitPart. Смерть от яда
     недостижима никаким путём.
Влияние: отравленный/кровоточащий NPC живёт вечно с 0 HP торса (ползающий
     «труп»); проклятые механики будущих фаз унаследуют проблему.
Фикс: в DoT-пути BodyModule добавить проверку полного дренажа HP
     (GetCurrentHealth ≤ 0 → NPCDeathEvent) или распределение DoT по
     частям; согласовать с правилом смерти.
```

```
[CMB-9] P2 — DoT-тик прерывает pending-каст игрока (interrupt без возврата
        readiness)
Файл: game/src/Modules/Combat/CombatService.cs:249-257
      (OnDamageAppliedForCastInterrupt триггерится ЛЮБЫМ DamageAppliedEvent,
      включая "dot:..."), :651 (readiness уже потрачена на замах)
Что: кровотечение тикает раз в 3с → DamageAppliedEvent → pending-каст
     игрока снимается без возврата readiness. Прерывание работает и ВНЕ
     боя (гейт _isInCombat отсутствует).
Влияние: игрок с кровотечением регулярно теряет замах (готовность
     потрачена, удара нет) — «удары не срабатывают» в плейтесте.
Фикс: игнорировать SourceId вида "dot:" в прерывателе (только реальные
     удары прерывают каст); опционально возврат readiness при interrupt.
```

## Дефекты P3

```
[CMB-10] P3 — TechniqueUsedEvent публикуется ДВАЖДЫ на технику игрока, и
         вообще не имеет подписчиков
Файл: game/src/Modules/Combat/TechniqueService.cs:538 (CompleteUse) +
      game/src/Modules/Combat/CombatService.cs:953 (BuildAndExecute
      DamageRequest)
Что: одна и та же атака техники: publish в CompleteUse + publish в
     CombatService. ISubscriber<TechniqueUsedEvent> в кодовой базе нет.
Влияние: безвредно сейчас; при появлении подписчика (мастерство/UI/квесты)
     задвоение сразу сломает учёт.
Фикс: оставить одну точку публикации (CombatService) или удалить мёртвое
     событие.
```

```
[CMB-11] P3 — CombatConfig.PlayerDamageMultiplier / EnemyDamageMultiplier
         нигде не читаются
Файл: game/src/Modules/Combat/CombatConfig.cs:31-34;
      game/src/Modules/Combat/CombatModuleServices.cs:71-72
Что: поля-ручки баланса не подключены к DamageCalculator.
Влияние: ложное ощущение настраиваемого баланса.
Фикс: подключить в Слой 1 (промилле) или удалить.
```

```
[CMB-12] P3 — QiBufferService.AbsorbDamage — мёртвый путь; инвестированное
         Ци всегда возвращается ПОЛНОСТЬЮ; формулы буфера дублированы
Файл: game/src/Modules/Qi/QiBufferService.cs:129-168 (AbsorbDamage — 0
      вызывателей), :108-127 (Deactivate возвращает _qiInvested −
      _qiConsumedDuringActivation, а инлайн-путь DamageService
      _qiConsumedDuringActivation не трогает);
      game/src/Modules/Combat/DamageService.cs:404-431 (живой инлайн-расчёт)
Что: поглощение живёт в DamageService-копии формул; QI-A05-трекинг расхода
     из инвестиции не инкрементируется → Deactivate возвращает весь депозит.
Влияние: два источника правды для формул Ци-буфера (риск дрейфа при
     правке коэффициентов); семантика «щит тратит инвестицию» не работает.
Фикс: удалить AbsorbDamage, инкремент потребления перенести в
     DamageService (или в сам QiBufferService по событию).
```

```
[CMB-13] P3 — Подписки QiChangedEvent в CombatService/DamageService без
         фильтра EntityId (глобальный кэш игрока)
Файл: game/src/Modules/Combat/DamageService.cs:131-133;
      game/src/Modules/Combat/CombatService.cs:225-229
Что: кэш _cachedCurrentQi/_cachedBufferIsActive перезаписывается любым
     QiChangedEvent. Сегодня публикует только QiService (игрок) — латентно.
Влияние: при появлении NPC-кастеров/мультиплеера кэш игрока будет затёрт.
Фикс: фильтр PlayerIdResolver.AreSameEntity(e.EntityId, "player").
```

```
[CMB-14] P3 — Сравнение attackerId == _instigatorId сырыми строками (алиасы
         игрока) в резолве защитника
Файл: game/src/Modules/Combat/CombatService.cs:648 (castTargetId),
      :738 (defenderId)
Что: вместо PlayerIdResolver.AreSameEntity. При смешении "player"/"player_0"
     атакующий-игрок получил бы defender = сам инстагатор (self-hit).
     Сегодня все caller'ы шлют канонический "player_0" — латентно.
Фикс: AreSameEntity в обоих местах (паттерн уже есть в GetReadinessPermil).
```

```
[CMB-15] P3 — «Чистый урон игнорирует броню» — комментарий лжёт: Pure
         проходит DefenseProcessor с бронёй
Файл: game/src/Modules/Combat/DamageService.cs:280-285 (сбрасывается только
      Ци-буфер), далее :288-348 броня применяется
Влияние: при введении Pure-источников баланс поедет; читатель кода введён
     в заблуждение.
Фикс: либо байпас слоёв 6-8 для Pure, либо поправить комментарий.
```

```
[CMB-16] P3 — Устаревшие float-константы Ци-буфера дублируют промилле-версии
Файл: game/src/Core/Data/Constants.cs:293-363 (RAW_QI_ABSORPTION 0.9f рядом
      с RAW_QI_ABSORPTION_PERMIL 900 и т.д.)
Влияние: риск взять не ту константу (ЗАПРЕТ 3.9-гигиена).
Фикс: удалить float-версии (проверив callers).
```

```
[CMB-17] P3 — StatProviderAdapter: неизвестная сущность получает СТАТЫ
         ИГРОКА (fallback IStatService)
Файл: game/src/Modules/Combat/StatProviderAdapter.cs:87-89
Что: NPC не найден, животное не найдено → берётся стат игрока (глобальный
     IStatService). Атакующий-фантом ("dot:...") считался бы игроком.
Влияние: латентно (DoT не идёт через CombatService), но урон фантомных
     атакующих масштабировался бы статами игрока.
Фикс: return 0 для неразрешённых сущностей + лог.
```

```
[CMB-18] P3 — ExecuteDefense: активация Qi-щита без гейта «защитник — игрок»
Файл: game/src/Modules/Combat/CombatService.cs:1013-1027
Что: QiBufferActivateRequestEvent активирует ГЛОБАЛЬНЫЙ буфер игрока при
     DefenseSubtype.Shield от ЛЮБОГО defenderId. Сегодня DefenseIntentEvent
     публикует только игрок — латентно.
Фикс: гейт IsPlayer(defenderId) перед publish.
```

```
[CMB-19] P3 — StartCombat не валидирует instigator==target (self-combat);
         ExecuteAttack не проверяет цель==атакующий напрямую
Файл: game/src/Modules/Combat/CombatService.cs:285-311
Что: StartCombat(x, x) создаст «бой с собой» (defender-резолв даст self-hit).
     Текущие caller'ы (PlayerCombatAdapter/NPCModule/AnimalService) цель
     резолвят валидно — латентно.
Фикс: ранний return + лог при AreSameEntity(instigator, target).
```

```
[CMB-20] P3 — Комментарий CombatService «Обычные удары НЕ блокируются стойкой
         выбора» противоречит коду (и докам)
Файл: game/src/Modules/Combat/CombatService.cs:898-905 (комментарий R21-2) vs
      DamageCalculator.cs:177-209 (стойки Dodge/Block/Parry реально
      роллятся для каждой атаки)
Что: DetermineAttackResult применяет шансы стойки всегда, когда стойка
     выбрана (R16). Комментарий/док §1.4:44 («обычные удары стойкой не
     блокируются») описывают НЕ то, что делает код; COMBAT_SYSTEM §7
     описывает как раз работающее поведение — доки противоречат сами себе.
Фикс: согласовать текст §1.4:44/комментарий с фактической семантикой.
```

```
[CMB-21] P3 — QI_SYSTEM §8: «регенерация батчами каждые 10 тиков
         (QiTickProcessor)» — код регенерит каждый тик
Файл: game/src/Modules/Qi/QiModule.cs:81 (_qiService.Regenerate каждый Tick)
      vs docs/docs_v2/02_systems/QI_SYSTEM.md:392
Влияние: расхождение производительности/поведения с доками.
Фикс: батчинг или правка §8.
```

Мелкие замечания без отдельных карточек: AnimalService.OnDamageApplied —
`_animals.Find` (O(n) на каждый удар, AnimalService.cs:552);
GetReadinessPermil/TryGetCastByAlias — O(n) скан словаря на каждый вызов
(CombatService.cs:415-437, 260-272); CombatService.IsCasting (:146,
combat-wide) остался от пер-атакерной эпохи — потребители только QA-сцены
(CombatSimDebug:629, CombatAISimDebug:303); heal через
TechniqueService.UseTechnique (Qi списывается) — единственный вызыватель
QA-пути; BuffService.GetActiveBuffs().ToList() в ApplyPurify — GC на каждый
Light-удар (ElementalEffectService.cs:197).

---

## Регрессионные риски R21 (чеклист задачи A)

1. **Readiness-модель (518e32a)** — минус невозможен (Math.Max(0),
   CombatService.cs:488), переполнение исключено капом=порогу (:459-461);
   мёртвым готовность не капает (бой завершается смертью → словарь чистится
   :383); частичная готовность не атакует (гейт :562-568); инициатива не
   даёт залпа (кап=порог, «банкования» нет). РЕГРЕССИИ: CMB-1 (гейт для
   charged-техник против доков), CMB-3 (резолв после конца боя).
2. **CLASH (518e32a)** — против не-атаки не срабатывает (вычисляется только
   в пайплайне удара); «оба в замахе» — асимметрия CMB-4; зверь/зубы
   парируют — да, без требования оружия (CMB-5); замах действительно не
   отменяется (:1096-1105 резолвит каст защитника; interrupt только от
   урона, CMB-9 — DoT-интerrupt излишне агрессивен).
3. **Per-attacker pending (518e32a)** — утечек словаря нет (EndCombat чистит
   :392-399, смерть → EndCombat); LOS-гейт IsEntityCasting(атакующего)
   корректен (CombatModule.cs:191); регрессия — CMB-3 (batch-снапшот обходит
   очистку) и CMB-4 (interrupt не работает в same-tick партии).
4. **Пассивный щит Ци (9d974e2)** — при Ци=0 поглощения нет (гейт
   MIN_QI_FOR_BUFFER, DamageService.cs:410-411) — буфер «гаснет» честно;
   двойного поглощения щит+броня нет (буфер ДО брони, пробитие 20% идёт в
   броню — соответствует §5); already-dead поглощает только через CMB-3;
   NPC авто-активация (QiDataProvider.SetQiState:99-103) + гейт по
   currentQi — согласовано с QI_SYSTEM §6. Замечание: пассивный щит ДРЕЙНит
   Ци 5:1 за физ-поглощение — игрок L1 (500 Ци) за ~12 поглощённых ударов
   по 100 остаётся без Ци — следить в плейтесте (не дефект, баланс).
5. **Броня (9d974e2)** — порядок percent→flat соответствует ALGORITHMS §5.2;
   overkill-отрицательный итог невозможен (min 1, DefenseProcessor.cs:76);
   DR-агрегат аддитивен в DamageReductionPermil, суммарный кап 800‰ —
   соответствует докам R21-примечанию; penetration снижает и процентную и
   плоскую часть (по §5.2 — ок).

## Физический pipeline (B) — итог

Цепочка STR/AGI/Weapon → WeaponDamageCalculator → DamageCalculator (крит/
парирование) → DefenseProcessor → BodyDamageCalculator → CombatConsequences
целостна: NaN нет (весь урон int/long+промилле), деления на ноль закрыты
(requiredQi>0-гарды, maxHP>0-гарды), int-overflow закрыт long-промежуточными.
Смерть при уроне 0 невозможна (ApplyDamage гейт ≤0; IsFatalHit ≥ 50).
Проблемные точки: CMB-3 (атака мёртвого), CMB-14/19 (self-hit латентно),
CMB-11 (мультипликаторы мертвы), CMB-15 (Pure).

## Культиваторный pipeline (C) — итог

PlayerTechniqueCaster → TechniqueService → TechniqueChargeService →
QiService списание тиками — модель заполнения соответствует
TECHNIQUE_SYSTEM §5.3 (включая отмену с 50% рефандом и SaveStarted-отмену).
Каст при 0 Ци невозможен (StartCharge ≥ MIN_QI_FOR_BUFFER + rate ≥
MIN_CHARGE_RATE). Регенерация в бою продолжается (Qi 10%/сутки, тело
0.1HP/с — пренебрежимо; медитация корректно прерывается боем).
Проблемные точки: CMB-1 (потеря Ци+кулдауна при отклонении), CMB-6
(двойная цена Defense), CMB-8/9 (DoT), CMB-12/13.

## Контракты и события (D) — итог

DamageAppliedEvent — ровно один на удар (DamageService) + отдельные DoT-тики
(BodyModule, by design, SourceId="dot:…"). EnemyKilledEvent — один, только
при убийстве игроком; атрибуции атакующего в нём нет (killfeed живёт на
NPCDeathEvent с sourceId — работает). Порядок: урон → тело → смерть NPC →
труп (CorpseService) → потом EnemyKilled/EndCombat — корректно.
EntityDiedEvent не существует (player-смерть — PlayerDeathEvent через
BodyCriticalEvent — работает даже вне боя). Дыры: CMB-3 (нет
CombatEndedEvent при смерти после конца боя), CMB-10 (двойной
TechniqueUsedEvent, мёртвый).

## Расхождения код ↔ доки (E)

1. COMBAT_SYSTEM.md:404-405 «заряженные техники готовность НЕ расходуют» ↔
   CombatService.cs:562+613 (и гейт, и расход). [CMB-1]
2. COMBAT_SYSTEM.md:44 (§1.4) «обычные удары стойкой НЕ блокируются» ↔
   DamageCalculator.cs:177-209 (стойки роллятся всегда) и COMBAT_SYSTEM §7
   (описывает работающие стойки) — доки противоречат сами себе. [CMB-20]
3. COMBAT_SYSTEM.md:349 (§7.2) «парирование: требуется оружие или щит» ↔
   clashParry без проверки экипировки (звери парируют). [CMB-5]
4. COMBAT_SYSTEM.md:184-187 (слой 8: материал — отдельный мультипликативный
   слой) ↔ DefenseProcessor.cs:52-65 (материал аддитивен в общий редукшн,
   общий кап 800‰).
5. DamageService.cs:280 комментарий «чистый урон игнорирует броню» ↔ броня
   применяется. [CMB-15]
6. QI_SYSTEM.md:392 (§8: регенерация батчами каждые 10 тиков) ↔
   QiModule.cs:81 (каждый тик). [CMB-21]
7. Совпадает (проверено): ALGORITHMS §5.2 (armor/(armor+100) → flat
   effectiveArmor×0.5 → min 1) ↔ DefenseProcessor; QI_SYSTEM §6 пассивный
   RawQi ↔ DamageService/QiDataProvider; TECHNIQUE_SYSTEM §5.3 fill-модель ↔
   TechniqueChargeService/PlayerTechniqueCaster.

## Рекомендованный порядок фиксов

1. **CMB-2** — глобальный 1v1-лок (дизайн-решение: приоритет игрока /
   per-pair бои; минимум — таймаут NPC-боёв). Главный блокер плейтеста.
2. **CMB-1** — убрать readiness-гейт/расход для isCharged + не терять
   кулдаун/Ци при отклонении (вернуть докам §8.2 честность).
3. **CMB-3** — гейт _isInCombat в резолве партии кастов (микро-фикс, 2-3
   строки).
4. **CMB-4 + CMB-5** — честный mutual-clash + требование оружия для
   парирования (сузить окно замаха).
5. **CMB-7** — фильтр фантомных "dot:"-целей в RetaliateOrFlee (NPC-7
   доводка).
6. **CMB-8 + CMB-9** — смертоносность DoT + игнор dot-источников в
   прерывателе кастов.
7. **CMB-6** — передача ChargedQi в инвестицию щита.
8. P3-пакет одним эпизодом (CMB-10..21) + синхронизация доков (E:1-6).

## Файлы

- Прочитано (полностью/точечно): 17 × Modules/Combat, 4 × Modules/Qi,
  Player/PlayerTechniqueCaster, Player/TechniqueSlotService,
  Body/BodyService, Body/BodyDamageCalculator, Body/BodyModule,
  NPC/NPCModule, NPC/NPCCombatAdapter, NPC/NPCAIService (фрагменты),
  NPC/AnimalService (фрагменты), Buff/BuffService+BuffsTickProcessor
  (фрагменты), Core/Helpers/PlayerIdResolver, контракты CombatContracts/
  QiContracts, Constants (точечно).
- Создан: checkpoints/09_20_r22_combat_audit.md (этот файл).
- Код НЕ правился, build НЕ запускался, коммитов нет (read-only аудит).
