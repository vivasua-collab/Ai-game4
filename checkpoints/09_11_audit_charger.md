# АУДИТ: Charger (модуль Charger + проводки Qi/Save/Formation)

**Дата:** 2026-09-11, **HEAD:** `d0b065d`
**Скоуп (READ-ONLY):** Modules/Charger — все 7 файлов (Service/Buffer/Data/
Heat/Module/ModuleServices/Slot+QiStone); стыки: ChargerContracts,
GameConstants CHARGER_*, QiService (приём команд), FormationService
(ContributeQiRequest), Save-контур (SaveModule/Aggregator/GameSession/
GameEntryPoint/GameBoot/ChargerInitPhase/SaveLoadSimDebug), InventoryWindow
(QiStoneData); docs_v2: CHARGER_SYSTEM (+ QI §5.5, BRK §7.2).
**Заморожено (не репортится):** Qi=long; Permil; Element.Poison; player/player_0.

---

## Сводка

- Прочитано полностью: 7 Charger + контекст (синхронность EventBus, порядок
  Start/Restore, Core-константы).
- **Находок: 7 (P2×3, P3×4).** P1 нет — модуль не интегрирован в геймплей.
  Код НЕ изменялся.

---

## Находки

### CH-1 [P2] Charger не подключён к геймплейному контуру (известный TODO)
`Modules/Charger/ChargerService.cs` (публичный API); доказательство grep по
game/src: единственный потребитель IChargerService вне модуля —
`Adapter/Scene/SaveLoadSimDebug.cs:64` (toggle Mode). Следствия:
`EnterCombat/ExitCombat` — 0 вызовов → боевой кулдаун тепла (§5.3), автоактивация
в бою (:216-224) и блокировка передачи в бою (CH-16, :315) — мёртвые ветки;
`UseQiForTechnique/CanUseTechnique/GetAvailableQi` — 0 вызовов → техники
никогда не берут Ци из буфера (CHARGER §5.2, TECHNIQUE §9.3 не проводятся);
`InsertStone/RemoveStone` — 0 gameplay-вызовов → камни попадают в слоты только
из сейва. Две системы камней не связаны: Charger.QiStone (quality ×0.5-4.0,
sizes 100-50 000, Element) vs QiStoneData (dust-boulder, 1024×см³, calm/chaotic,
БЕЗ стихии) — вставить инвентарный камень в зарядник нельзя.
08_23_qi_impl_plan.md:112 явно откладывает интеграцию.
**Фикс:** ChargerInitPhase → слот экипировки «charger»; EnterCombat/ExitCombat
из CombatModule; UseQiForTechnique в TechniqueChargeService (ветка «ядра не
хватило»); перевод QiStoneData→QiStone.

### CH-2 [P2] Warm-load: RestoreState мержит поверх живого состояния
`ChargerService.cs:484-542`; `ChargerModule.cs:28-35`; `GameSession.cs:123-165`.
Configure() (сброс слотов/буфера) вызывается ТОЛЬКО в ChargerModule.Start —
один раз за процесс (guard GameEntryPoint._initialized). Cold-load корректен
(Start → позже RestoreState). Warm-load (SaveAndQuit→Quitting→LoadGame в том
же процессе): RestoreState без сброса → слоты заняты прошлой сессией →
`InsertStone` тихо false — **камни из сейва молча потеряны**;
`_buffer.AddQi(bufferQiVal)` (:493-497) накапливает поверх текущего — двойное
зачисление; тепло/режим перезаписываются. NPC-домен на этот случай сбрасывается
(R14 `_npcModule.ResetWorld()`), зарядник — нет.
**Фикс:** сброс в начале RestoreState (слоты→RemoveStone, буфер→Discharge,
тепло→ResetHeat, mode→Off) либо Reset()-метод рядом с ResetWorld.

### CH-3 [P2] DepleteStones: камни истощаются на post-loss величину
`ChargerService.cs:293-311,359-404`; `ChargerBuffer.cs:114-136`.
AccumulateFromStones: effectiveRate = min(totalRate,cond)×(1−loss) → буфер
получает raw×0.9×dt; DepleteStones распределяет ровно `added` (post-loss) →
10% Ци камня никогда не расходуется на накоплении: камень живёт ~11% дольше,
за полную жизнь отдаёт 100% Ци (CHARGER §5.6 «Потери 10% — штраф за
использование»; §4.3 трактует loss в effectiveRate). Потери на стадиях
буфер→практик и техники применяются честно — семантика непоследовательна.
**Фикс:** DepleteStones(added/(1−loss), totalRate) — камень платит сырую
величину.

### CH-4 [P3] Save/Load: перегрев/кулдаун не восстанавливаются; камни мимо событий; stoneMaxQi игнорируется
`ChargerService.cs:440-541`. (1) `isOverheated`/`cooldownTimer` сохраняются
(:471-472,478-479), но НЕ восстанавливаются (комментарий :511-513 признаёт):
сейв в перегреве (heat<1.0) → после загрузки сразу рабочий. (2) heatLevel=1.0
→ AddHeat повторно публикует ChargerOverheatedEvent + свежие 30с. (3) Камни
вставляются напрямую `_slots[i].InsertStone` (:539) в обход сервиса →
ChargerStateChangedEvent при загрузке не публикуется (UI слепо). (4) MaxQi
пересчитывается из quality/size (детерминированно — совпадает), но сохранённый
`stoneMaxQi` игнорируется: смена формулы в будущем → тихий сдвиг CurrentQi;
`diff<0` оставит камень ПОЛНЫМ (инфляция при даунгрейде) — клампать.

### CH-5 [P3] Мёртвый слой материалов/грейдов/назначений + мёртвые поля
`ChargerData.cs:127-258`; `ChargerHeat.cs:53-61`; `ChargerBuffer.cs:28`;
`ChargerSlot.cs:87,123`. ChargerConfigs.GetFormFactorConfig/
GetMaterialConductivity/GetMaterialDurability/GetMaterialQiRetention/
GetPurposeSpeedMultiplier/GetPurposeBufferMultiplier — 0 вызовов; enum'ы
ChargerMaterial/FormFactor/Purpose мертвы → таблицы CHARGER §2.1/§2.2/§7.1
(пров. 5-100, прочность, retention, множители назначения) не участвуют:
дефолт — хардкод «belt 500/10/3» (ChargerModuleServices.cs:30-50).
ChargerHeat.GetEfficiency (100%→50% при 30-90% тепла) — 0 вызовов.
ChargerBuffer._inputRate — записывается, не читается. ChargerSlot._qiRetention —
не применяется при извлечении.

### CH-6 [P3] Формулы vs docs — мелочи
(1) CHARGER §5.5 «−2%/сек вне боя» противоречит §5.3 «−1%/сек»; код следует
§5.3 (CHARGER_PASSIVE_COOLING_RATE=0.01, Constants.cs:1276) — док-конфликт.
(2) §4.3-формула включает practitioner.conductivity в min при накоплении;
код — min(totalRate, chargerCond) (практик ограничивает лишь передачу по
§4.1/§3.3) — код соответствует потоку §4.1, пометить в доке. (3) Шкала
камней Charger (100-50 000) не соответствует ни QI §5.4 (1K-100M), ни
GENERATORS §10 (1024×см³) — потребуется унификация при CH-1.

### CH-7 [P3] QiStone._stoneId = Guid.Substring(0,8) — 32 бита энтропии
`ChargerSlot.cs:44`. 8 hex = 32 бита: birthday-коллизия с ~10^4 камней.
StoneId нигде не ключ (адресация — int-индекс слота; Guid-адресности по R10
в модуле нет вообще) → риск сегодня нулевой; при будущем использовании как
ключа — полный Guid («N»).

---

## Проверено чисто

- **CH-17 (remainder):** остаток усечения < числа камней (доля теряет <1) →
  цикл `i<remainder && i<shares.Count` корректен; выравнивание shares/слотов
  (включая нули пустых) верное.
- **CH-18 (аккумулятор):** вычитается реально добавленное (`added`, не
  `toAdd`) — потери на почти-полном буфере нет.
- **CH-24:** UseQiForTechnique/CanUseTechnique гвардят qiCost ≤ 0.
- **UseQiForTechnique:** порядок ядро→буфер; `ceil(remaining/(1−loss))` —
  корректная инверсия потерь; QiLost согласован (§5.2 таблица решений).
- **Тепло:** AddHeatFromQi = qiUsed×0.05/100 — нормализация CH-01 корректна,
  док-примеры совпадают (50→2.5%, 250→12.5%); тепло только от QiFromBuffer
  (не ядра) — §5.4 ход 1 = 0% ✓; перегрев блокирует накопление И передачу
  (IsOperational-гейт Tick:285) — §5.1 «камни не высвобождают» ✓; рассеивание
  идёт и при Off (Tick:282 до mode-чека) ✓; кулдаун 30с → сброс в 0 ✓.
- **Проводка к Qi:** QiConsume/QiAddRequestEvent(…, "Charger") — второй
  аргумент RequesterId, EntityId="" → QiService корректно списывает/зачисляет
  игроку (QiService.cs:316-330); кэш _cachedCurrentQi/_cachedConductivity
  свежий (EventBus синхронный; стартовый QiChangedEvent после подписки в ctor).
- **FormationContributeQiRequestEvent("Charger", transferred):**
  FormationService.cs:542-551 гвардит стадию (Filling/Active/Depleted) —
  вне формации игнорируется тихо, дублей нет.
- **Save/Load cold-path:** Start(Configure→Activate) → _save.Load →
  RestoreState — порядок корректен; ChargerInitPhase — wiring-фаза
  (SkipOnLoad=false), модуль не рестартует.
- **ChargerBuffer.AddQi/ExtractQi:** клампы по ёмкости/остатку, long-математика
  без переполнений (значения ≤ 2000); TransferToPractitioner =
  min(outputRate, practitionerCond)×(1−loss) — §3.3/§5.5 ✓.
- **ChargerSlot:** автомат состояний (Empty/Active/Depleted/Sealed/Inactive)
  консистентен; замена истощённого камня ✓; CheckDepletedStones (CH-02)
  проверяет IsEmpty, не HasStone ✓.

---

## Соответствие docs_v2 (CHARGER_SYSTEM.md)

| Формула/константа | Дока | Код | Вердикт |
|---|---|---|---|
| Накопление: min(totalRate, cond)×(1−loss) | §4.3 | AccumulateFromStones:119 | OK (§4.3 лишний practitioner в min — CH-6) |
| Передача: min(charger.cond, pract.cond) | §3.3, §5.5 | TransferToPractitioner:147 | OK |
| Потери 10% (режим on) | §4.2 | CHARGER_EFFICIENCY_LOSS=0.1 | OK (но см. CH-3) |
| Камни расходуются при использовании | §9.2 | DepleteStones списывает post-loss | **P2 (CH-3)** |
| Техники: ядро→буфер, ceil(rem/0.9) | §5.2 | UseQiForTechnique:169-209 | OK (ветка мертва — CH-1) |
| heatGain = qiUsed×0.05 | §5.3 | qiUsed×0.05/100 | OK |
| Рассеивание 1%/сек; бой 0.5%/сек | §5.3 | 0.01/0.005 | OK (§5.5 «−2%» — док-конфликт) |
| Перегрев 100%→30с, камни стоп | §5.3 | threshold 1.0, cooldown 30, IsOperational | OK |
| В бою нет передачи практику | §5.5 | `!_heat.IsInCombat` (CH-16) | OK (ветка мертва — CH-1) |
| Форм-факторы/материалы/грейды | §2, §7 | таблицы мертвы, дефолт хардкод | P3 (CH-5) |
| Эффективность от тепла 100→50% | — | GetEfficiency мёртв | P3 |
| Зарядник ISaveable | §10 | ChargerSaveData, round-trip PASS | OK (CH-2/CH-4 оговорки) |
| Генератор зарядников | §10 | отсутствует (hardcoded singleton) | P3 (v1, часть CH-1) |
| Пороги Warm/Hot/Critical 30/60/90 | — | GetHeatState | OK (не специфицировано) |

**Приоритет фиксов:** CH-2 (warm-load сброс) → CH-1 (интеграция) → CH-3 →
CH-4/CH-5 (при интеграции).

---

*Аудит Charger завершён (READ-ONLY). Сестринский отчёт: 09_11_audit_qi.md
(в т.ч. QI-1 двойное списание щита, QI-2 отсутствие Qi-блока в сейве).*
