# АУДИТ: Formation (модуль Formation + генерация формаций + стыки Charger/Qi/Save/UI)

**Дата:** 2026-09-11, **HEAD:** `d0b065d`
**Скоуп (READ-ONLY):** Modules/Formation — все 8 файлов (Service 722 стр. полностью,
QiPool, Calculator, Config, Effects, Module, ModuleServices, FormationRegistry);
FormationGeneratorService; Core/Data: FormationData, FormationEnums, Constants
(1296-1409), LevelBoundaries:411; контракты Formation/QiContracts; стыки:
QiService/QiDataProvider (кэш-семантика), ChargerService, QiModule (Gathering ×2),
PlayerTechniqueCaster, DamageService (слой 3b), GameWorldController/
FormationVisualRenderer, GameSession/SceneOrchestrator (Save/Load),
TechniqueGrantPhase; docs_v2: FORMATION_SYSTEM.md (полностью), GENERATORS_SYSTEM §9.
**Заморожено (не репортится):** Qi=long; Permil; Element.Poison; player/player_0.

---

## Сводка

- Прочитано полностью 8 файлов + генератор + проводка (EventBus синхронный;
  QiChangedEvent публикует ТОЛЬКО QiService игрока → кэш FormationService
  игрок-scoped; NPC-Ци живёт в QiDataProvider без событий).
- **Находок: 12 (P2×4, P3×6, OK-BY-DESIGN×2).** Двойного списания Ци и
  переполнений long нет; атомарность «списание→пул» честная. Код НЕ менялся.

---

## Находки

### F-1 [P2] Связка Charger→Formation мертва: вклад зарядника всегда отвергается
`ChargerService.cs:330-331` публикует `FormationContributeQiRequestEvent("Charger",
transferred)` (после передачи Qi игроку через QiAddRequestEvent:323). В
`FormationService.ContributeQi` вклад отвергается ВСЕГДА: "Charger" не участник
и ≠ кассир → проверка уровня помощника (:287-290):
`_qiDataProvider.GetCultivationLevel("Charger")`=0 (сущности нет) <
`minHelperLevel≥1` → `return 0`. Даже при обходе: `GetCurrentQi("Charger")`=0
(:312-314), `TryConsumeQi("Charger")`=false (:333). FORMATION_SYSTEM §2.1
«Можно использовать зарядник» — не работает. Латентно: работающая ветка
зачисляла бы тот же Ци И игроку, И в пул (дюп Ци) — текущий запрет маскирует
дизайн-дыру.
**Фикс:** семантика контракта — ContributorId = фактический плательщик
(playerId) + ветка «внешний источник» (списание из буфера зарядника, без
TryConsumeQi игрока).

### F-2 [P2] Save/Load: позиция не сохраняется, события активации не пере-публикуются
`FormationService.cs:609-682`. CaptureState (:616-624) НЕ сохраняет
`_positionX/_positionY` (и `_autoFillAccumulator`); RestoreState ставит
состояние напрямую БЕЗ публикаций FormationActivated/StageChanged. После
LoadGame при активной формации: зона Amplification считается от (0,0) —
`PlayerTechniqueCaster.cs:288-298` (svc.PositionX/Y) → бонус потерян;
Gathering ×2 медитации не восстанавливается — `QiModule.cs:120-128` (только
по событию); формация невидима — `FormationVisualRenderer.cs:100-128`
(`_visible` только по событиям). Плюс: `(FormationStage)data.currentStage`
(:671) без валидации; `long.Parse` (:666) без try/catch — кривой сейв роняет
загрузку.
**Фикс:** +posX/posY в FormationSaveData; в RestoreState при Active —
публикация FormationActivatedEvent (+StageChanged), валидация enum/long.TryParse.

### F-3 [P2] Бонусы формации применяются глобально — без зоны и без ally/enemy
`DamageService.cs:172-176` (formationDmgPermil ко входящему урону любой пары),
`:283-285` (formationDefPermil любому защитнику). `FormationEffects.Get
FormationBonus` игнорирует EffectRadiusMeters и TargetTag. FORMATION_SYSTEM
§1/§4/§12 — «бонусы союзникам / дебаффы врагам В ЗОНЕ». Сейчас: «Меч Дао»
(+30% урона союзникам) усиливает и урон NPC по игроку; Barrier (+Defense
"ally") усиливает защиту врага. Зону проверяет только PlayerTechniqueCaster
:288-298 (Amplification игрока).
**Фикс:** в пайплайн урона передавать (attacker, defender, позиции) →
Chebyshev-радиус + TargetTag перед применением бонуса.

### F-4 [P2] Состояние формации переживает пересборку мира (нет ResetWorld)
FormationService — DI-синглтон ISaveable; `GameSession.NewGame`
(`Entry/GameSession.cs:67-105`) не вызывает ни RestoreState, ни сброса.
NpcDomainResetPhase сбрасывает только NPC-домен; `WorldInitPhase.cs:35` —
только TechniqueRegistry. Мир A — активна формация → меню → NewGame →
формация действует в мире B (бонусы через pull DamageService; ×2 медитация
QiModule; пул тикает). Аналог R14-P2-1 (NPC), для формаций паттерн не перенесён.
**Фикс:** ResetWorld-хук (деактивация + сброс QiModule._gatheringEnvironmentMult)
при NewGame и ДО RestoreState при LoadGame.

### F-5 [P3] Cold-load: генерируемая формация из сейва молча теряется
`FormationService.cs:644-645`: FindFormationData → FormationRegistry пуст в
новом процессе (регистрация только в момент каста с НЕдетерминированным сидом
— `PlayerTechniqueCaster.cs:267-269` `_formationRng.NextInt64()`) → ранний
`return` без лога. Хардкод-ID (basic_barrier и др.) резолвятся (:581-591).
FormationRegistry не ISaveable.
**Фикс:** полный снимок FormationData в сейв (паттерн TechniqueSnapshotDto)
либо seed-from-world.

### F-6 [P3] Ручные формации: EffectRadiusMeters не задан по размеру
`Core/Data/FormationData.cs:86-135`: CreateDaoBlade/CreateShadowBindings
(Medium) не задают EffectRadiusMeters → default 50 м (=Small), по §4 Medium
= 200 м. Генерируемые — задают (FormationGeneratorService:147).

### F-7 [P3] Float-математика в drain пула (§14 «никаких float-накоплений»)
`FormationQiPool.cs:117` — `(long)(drainCycles * _drainAmount *
drainSpeedMultiplier)` умножает long через float; при произведении > 2^24 —
потеря точности. `FillRatio` (:41) — long→float. Практически редкий кейс.

### F-8 [P3] AutoFillTick теряет чанк при отказе ContributeQi
`FormationService.cs:466-478`: `_autoFillAccumulator -= chunk` ДО вызова
ContributeQi; при нехватке Ци у кассира вклад=0, чанк уже вычтен — темп
автонаполнения «сгорает». Не потеря Ци игрока.

### F-9 [P3] StartDrawing «грязнит» состояние при отказе
`:177-192`: `_currentFormation/_casterId/_positionX/Y` присваиваются ДО
проверок уровня/Ци; при `return false` (:187/:204/:209/:215) остаются
установленными при stage=None. Гейты стадий маскируют, но
`CurrentFormation/CasterId/PositionX/Y` возвращают мусор.

### F-10 [P3] Проверки создателя всегда по кэшу игрока (NPC-кастер недоступен)
`:187` (уровень), `:203` (Ци контура), `:302-307` (Ци вклада кассира), `:471`
(проводимость AutoFill) — читают кэш QiChangedEvent (игрок). Для casterId=NPC:
гейт по чужому Ци (ложный отказ при бедном игроке/богатом NPC). NPC-кастеров
сейчас нет (все вызовы StartDrawing — игрок: PlayerTechniqueCaster:270,
CheatPanel:400/564/589, TechniqueGrantPhase:108, SaveLoadSimDebug:156) —
заметка при появлении NPC-формаций (аналог C-4 аудита-3).

### F-11 [OK-BY-DESIGN] Внесённое Ци не возвращается при роспуске
`:412` — `_qiPool.Reset()`: вклады уничтожаются при DeactivateFormation (вкл.
авто по CombatEnded:534 и истощению:509). FORMATION_SYSTEM §2.1/§2.3 refund
не обещает («контур исчезает навсегда»). Двойного списания нет: контур
(StartDrawing:200-216) и наполнение (ContributeQi:325-338) — независимые
стоимости (§2.1 ЭТАП 1 vs ЭТАП 2); списание игрока подтверждается дифом кэша
(QiService публикует QiChangedEvent ТОЛЬКО при успехе — QiService.cs:140-156),
NPC — прямым TryConsumeQi; пул пополняется после подтверждения (:335-342).

### F-12 [OK-BY-DESIGN] Смерть участника не обрабатывается
Участник остаётся в `_participants`; §2.3 — «формация самостоятельна после
активации, создатель может покинуть место», подпитка опциональна. Заметка на
будущее (роспись вкладов при роспуске).

---

## Проверено чисто

- **Формулы = канон дословно:** contourQi=80×2^(L−1) (Calculator:25 =
  Constants:1303 = §6); capacity=contourQi×{10,50,200,1000,10000}
  (Constants:1309-1316 = §7); drain-интервалы {60,60,40,40,20,20,10,10,5,5}
  по уровню и {1,3,10,30,100} по размеру (Constants:1363-1387 = §8.2-8.3);
  maxHelpers {2,5,10,20,50} (Constants:1350 = §9.2, лимит до списания
  :293-296, +1 кассир); minHelperLevel=max(1,L−2) (:288 = §9.3); Heavy L6+
  (Calculator:102-107, генератор:121/130, VerificationService:178-184 = §4).
- **Пул long-арифметика:** AddQi кламп Min(max) (QiPool:85), drain кламп
  Max(0) (:119), long строкой в сейве; остаток тика аккумулируется дискретно
  (:112-115).
- **Depleted — стабильная стадия с перезарядкой** (FMT-A03: вклад из
  Filling/Active/Depleted:260-264; Depleted→Filling→Active:355-364;
  одноразовые исчезают:507-510 — §2.1).
- **CombatEnded → автодеактивация:** подписка парная (Initialize:150/
  Dispose:688), гейт конфига (Config:23=true).
- **Gathering ×2 медитации** ✓ (QiModule:120-128 — §10.2: environmentMult,
  НЕ finalConductivity).
- **Промил-конверсия бонуса** ✓ (GetFormationBonusPermil:445-449;
  DamageService — integer math, ЗАПРЕТ 3.9).
- **Стекинг:** аддитивная агрегация по статам внутри формации
  (FormationEffects:50-59); одна формация одновременно (v1-скоуп).
- **FormationGeneratorService:** SeededRandom-детерминизм (Generate→
  GenerateSpecified ре-сид — воспроизводимо); радиусы 50/200/600/1000/5000
  ✓ §4; Heavy→Medium даунгрейд; эффекты масштабируются уровнем с клампом.
- **Стартовый кэш Qi:** FormationModule.Start → QiAddRequestEvent(0) →
  QiService.AddQi(0) публикует QiChangedEvent (QiService.cs:158-168) — race
  закрыт.

---

## Соответствие docs_v2

| Пункт доки | Код | Статус |
|---|---|---|
| §2.1 этапы 1-3, charger в наполнении | Service:163-367 | ✓ / charger ✗ (F-1) |
| §2.2 вариант Б (ядра) | не реализован (IsReusable=false) | отложено (G-13 Generator) |
| §4 радиусы/множители | Constants + генератор | ✓ (ручные ✗ F-6) |
| §6/§7/§8 формулы | Calculator/Constants | ✓ точно |
| §9 наполнение Σ участников | только автонаполнение создателем | упрощение (TODO FMT-D03) |
| §10.2 Gathering→environmentMult ×2 | QiModule | ✓ |
| §11 стадии (+Imbuing/Mounting) | enum без них | doc-drift P3 (вариант А) |
| §12 примеры (Меч Дао +30%, 4 м) | dao_blade 0.3, радиус 50 default | частично (F-6) |
| §13 ISaveable | FormationService | ✓ (F-2: пробелы) |
| §14 long без float | QiPool:117/41 | ✗ мягко (F-7) |

---

*Аудит READ-ONLY. Код не менялся. Порядок фиксов: F-1 → F-2 → F-4 → F-3.*
