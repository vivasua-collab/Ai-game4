# АУДИТ: Qi (модуль Qi + проводки техник/камней/боя/сейва)

**Дата:** 2026-09-11, **HEAD:** `d0b065d`
**Скоуп (READ-ONLY):** Modules/Qi — все 8 файлов; стыки: Qi/Charger/
TechniqueCharge-контракты, GameConstants, PlayerIdResolver, EventBus,
TechniqueChargeService, PlayerTechniqueCaster, CombatService/DamageService
(Qi-ветки), NPCQiRegenService, FormationQiPool, InventoryWindow (RMB-камни),
Cultivation/Cheat/Hotbar/GameWorldController UI, Save-контур; docs_v2:
QI_SYSTEM, BREAKTHROUGH_MODELS, TECHNIQUE_SYSTEM.
**Заморожено (не репортится):** Qi=long; Permil; Element.Poison; player/player_0.

---

## Сводка

- Прочитано полностью: 8 Qi + ~15 контекстных; grep-трассировка всех
  QiConsumeRequestEvent/QiAddRequestEvent/QiBuffer*-проводок.
- **Находок: 10 (P1×2, P2×5, P3×3).** Код НЕ изменялся.

---

## Находки

### QI-1 [P1] Двойное списание Ци при защите «щитом» (25% → 50%)
`Modules/Combat/CombatService.cs:899-905` + `Modules/Qi/QiBufferService.cs:86-106`.
Игрок G→ExecuteDefense(Shield): `shieldQi = _cachedCurrentQi/4`; затем ОДНО
действие платится ДВАЖДЫ: (1) `QiConsumeRequestEvent(shieldQi,"Combat",
defenderId)` → QiService:321 `AreSameEntity("player_0","player")`=true →
TryConsumeQi (платёж №1); (2) `QiBufferActivateRequestEvent(shieldQi,Shield)`
→ QiBufferService.Activate:91 `TryConsumeQi(qiInvested)` (платёж №2). Итог:
50% текущего Ци за стойку вместо декларированных 25%. Barrier/Defense-техника
(PlayerTechniqueCaster.cs:80,235) платит однократно — двойной список только
в боевом контуре. **Фикс:** убрать явный QiConsumeRequestEvent в
ExecuteDefense (для NPC он и так мёртв — QiService игнорирует NPC-id,
провайдер в ветке не вызывается).

### QI-2 [P1] Qi-стейт игрока не сохраняется (нет ISaveable у QiService)
`Modules/Qi/QiService.cs:27`; `Modules/Save/SaveModule.cs:51`. Реестр SaveKey:
body/formation/charger/npc/techniques/inventory/save_meta/technique_slots —
блока «qi» НЕТ. Cold-process LoadGame → игрок = дефолт QiConfig (L1.0, full)
независимо от сейва: level/subLevel/quality/currentQi/heartSevered/bonus
сброшены. Warm-load маскирует дефект (уровень остаётся в памяти).
**Фикс:** QiService : ISaveable (SaveKey="qi"), регистрация в QiModuleServices.

### QI-3 [P2] Пассивная регенерация игрока vs канон (×256 000 на L9)
`QiRegenCalculator.cs:45-49`, `Constants.cs:171,193-205`. Дока (QI §5.1,
BRK §6.2): 10% от **coreCapacity**/сутки, КОНСТАНТА (L8→L9: 78 975/день).
Код: 10% от **maxQi** (=coreCapacity×density, L9 ×256) **×
RegenerationMultipliers[L]** (L9=1000) → ≈52.4 млрд/день, полное ядро L9 за
~14 игросекунд. NPC-реген (NPCQiRegenService.cs:69) — maxQi×0.1 без
множителя: третья модель. **Фикс:** выбрать канон и синхронизировать
QI_SYSTEM §5.1/BREAKTHROUGH §6.2/RegenerationMultipliers/NPCQiRegen.

### QI-4 [P2] L10: RegenerationMultipliers[9]=float.MaxValue ломает реген
`Constants.cs:204`, `QiRegenCalculator.cs:55-62`. perSecond×float.MaxValue ≈
1e45 → `(long)accumulator` за пределами long.MaxValue: x64 → long.MinValue
(отрицательный), кламп `> MAX_SAFE_CAPACITY` не срабатывает → CalculateRegen
возвращает отрицательное → `regenQi>0` отбрасывает: **на x64 реген на L10
мёртв**; ARM64 — saturate→кламп (платформозависимость). Аккумулятор навсегда
~1e45. **Фикс:** кламп actualRegen до MAX_SAFE_CAPACITY ДО каста; убрать
float.MaxValue из таблицы.

### QI-5 [P2] Штраф «сердце ампутировано» теряется при RecalculateStats
`QiService.cs:304-313` (×0.5) vs `:292-296` (перезапись из таблицы).
TryBreakthrough(:232)/SetCultivationLevel(:254) → −50% регена молча исчезает,
IsHeartSevered остаётся true. Побочно: QiConfig.RegenMultiplier (:122) всегда
затирается таблицей — поле конфига мертво. **Фикс:** effective =
base × (IsHeartSevered?0.5:1) в точке использования.

### QI-6 [P2] Gathering-бонус медитации ×2 без проверки зоны
`QiModule.cs:119-138`. FormationActivatedEvent несёт PositionX/Y/Radius, но
проверяется только Type==Gathering → ×2 ГЛОБАЛЬНО, на любом расстоянии
(FORMATION_SYSTEM §10.2 — «в зоне действия»). Побочно: пересчёт
SetMeditation(true) сбрасывает _meditationAccumulator (потеря дроби).
**Фикс:** Chebyshev-дистанция ≤ radius (прецедент —
PlayerTechniqueCaster.GetAmplificationBonusPermil).

### QI-7 [P2] QiBufferService.AbsorbDamage — мёртвый код; депозит щита возвращается целиком
`QiBufferService.cs:129-168` (0 вызовов; живой путь — inline-копия
`DamageService.cs:369-397`). Единственный инкремент
`_qiConsumedDuringActivation` (QI-A05) — в мёртвом методе; реальное
поглощение списывает Ци отдельным событием из DamageService → Deactivate
(:108-127) возвращает ВЕСЬ _qiInvested: семантика «возврат только
неизрасходованного» недостижима (щит = бесплатный возвратный депозит).
Формулы двух копий сейчас идентичны (сверено: RAW 900/800‰, ratio 3/5, shield
1/2, piercing 100/200‰) — дублирование = риск дрейфа. **Фикс:** удалить
мёртвый AbsorbDamage или перевести DamageService на IQiBufferService;
определить судьбу инвестиции.

### QI-8 [P2] Прорыв мгновенный — BREAKTHROUGH_MODELS §4.7 (8ч/80ч) не реализован
`QiService.cs:204-247`. Дока: малый ~480 игровых минут, большой ~4800; код —
мгновенная транзакция. RNG-провала в доках НЕТ — детерминированный успех
каноничен (OK). Побочно: BRK §5 L1→L2 = 5 188 vs код 5 186 (усечение
double→long, QiBreakthroughCalculator.cs:39-48,72). Двойного списания за
технику НЕТ (расход — тиками в TechniqueChargeService; CompleteUse/
TechniqueUsedEvent Ци не списывают — подтверждение C-2 аудита-3).
**Фикс:** фаза «ритуала прорыва» либо пометить §4.7 нереализованным.

### QI-9 [P3] AddQi — unchecked сложение (latent-переполнение)
`QiService.cs:158-168`: `Math.Min(max, _currentQi+amount)` — при amount
~long.MaxValue сумма (unchecked) → отрицательная → _currentQi<0. Текущие
источники далеки от порога — латентно. TryConsumeQi переполнения не имеет
(гвард `_currentQi>=amount`). **Фикс:** пороговое сравнение вместо сложения.

### QI-10 [P3] Мелочи
- `CalculateMeditationQiPerTick` (QiRegenCalculator.cs:75) — 0 вызовов,
  формула (1+level×0.1) не из док.
- `_dailyAccumulator` не сбрасывается в Initialize (staleness при ре-ините).
- QiFullEvent/QiChangedEvent публикуются даже при нулевом приросте (полное
  ядро) — event-шум (zero-GC, дёшево).
- TryBreakthrough не публикует QiDepletedEvent при обнулении Ци — боевые
  штрафы не срабатывают (вероятно by-design — пометить).
- RMB-камень: AddQi клампится при полном ядре — остаток камня молча теряется
  (InventoryWindow.cs:289-302); chaotic-RNG `new Random((int)Ticks)` — слабый
  сид. Транзакция атомарна, dupe/loss при прерывании невозможны.
- ENVIRONMENT_MULT_NORMAL=0.5 (Constants.cs:1503) — вне док; модель зон
  (ρ-запас/истощение, BRK §6.2 «Ци в зоне ИСЧЕЗАЕТ») не реализована.

---

## Проверено чисто

- **Long-арифметика умножений:** SafeMultiply/MAX_SAFE_CAPACITY (long.MaxValue/2),
  double-препромежутка; L9 ~5.24e8 — запас ×10^10. OK.
- **QiConsume/QiAddRequest из Charger/TechniqueChargeService:** второй аргумент
  = RequesterId, EntityId="" → QiService обрабатывает игрока
  (QiContracts.cs:118-126). Проводка живая (не P1).
- **Кэши Qi** (Combat/TCS/Charger): EventBus синхронный (re-entrant queue
  same-type only, EventBus.cs:63-95) → свежесть; стартовый QiChangedEvent —
  после подписок (ctors раньше GameEntryPoint.Start).
- **Техники:** зарядка тиками + 50% refund (‰), SaveStarted→CancelAllCharges
  (B5); cast-гейт _isCasting отклоняет с AttackRejectedEvent (C-5 аудита-3
  закрыт, CombatService.cs:505-510); pending — известный TODO.
- **NPCQiRegen:** DayChangedEvent — раз в сутки, НЕ каждый тик (O(n)/день).
- **QiDataProvider:** P0-4.1 preserve буфер-полей ✓; NPC TryConsumeQi без
  событий — by-design; RemoveEntity чистит.
- **DamageService==QiBufferService формулы** идентичны (см. QI-7).
- **BodyPartSevered-фильтр** `e.EntityId != _entityId` — алиас «player»==«player» ✓.

---

## Соответствие docs_v2

| Формула/константа | Дока | Код | Вердикт |
|---|---|---|---|
| coreCapacity = 1000×1.1^subL | QI §3.1 | CalculateFullCapacity | OK |
| effectiveQi = coreCap×2^(L-1) | QI §3.3 | RecalculateStats | OK |
| conductivity = coreCap/360 | QI §4.2 | QiService.cs:289 | OK |
| Реген 10% coreCap/сутки, const | QI §5.1, BRK §6.2 | 10% maxQi×Multipliers[L] | **P2 (QI-3)** |
| Медитация = cond×envMult | QI §5.2 | cond×0.5×gathering | OK (0.5 вне док) |
| Зона: запас истощается | BRK §6.2 | не реализовано | P3 |
| Малый/большой прорыв (Модель В) | BRK §4.3-4.4 | CalculateRequirement | OK |
| После прорыва Ци=0 | BRK §4.5 | TryBreakthrough:211 | OK |
| Время прорыва 480/4800 мин | BRK §4.7 | мгновенно | **P2 (QI-8)** |
| L1→L2 = 5 188 | BRK §5 | 5 186 (усечение) | P3 |
| chargeRate = cond×12×(1+m×0.005) | TCH §5.3 | ComputeChargeRate | OK |
| Отмена зарядки 50% | TCH §5.4 | REFUND_PERMIL=500 | OK |
| Старт: Ци ≥ qiCost | TCH §5.1 | лишь ≥10 (MIN_QI_FOR_BUFFER) | P3 (edge) |
| Буфер 90/80%, 3:1/5:1, 10/20%, щит 1:1/2:1 | QI §6 | Constants 316-363 + DamageService | OK |
| QiTickProcessor (батчи 10 тиков) | QI §8 | каждый тик | P3 |
| Камни 1K…100M | QI §5.4 | QiStoneData 1024×см³ (GENERATORS) | P3 (доки конфликтуют) |

**Приоритет фиксов:** QI-1 → QI-2 → QI-3/QI-4 → QI-5/QI-6 → остальное.

---

*Аудит Qi завершён (READ-ONLY). Сестринский отчёт: 09_11_audit_charger.md.*
