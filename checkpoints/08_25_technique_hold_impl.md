# Чекпоинт: Stage 0+1 — Модель заполнения техник + Аура-задержка (вариант В)

**Дата:** 2026-08-25 (вечерняя сессия, ~20:00–22:00 MSK)
**Сессия:** Облачный агент (Z.ai Code sandbox), продолжение
**Тип:** implementation + verification
**Объём:** 6 новых/изменённых файлов кода + 1 новый sim-debug + 3 док-правки;
build 0 errors; GODOT_NEWGAME/CHARGE_SIM/COMBAT_SIM/TRADE_DEBUG — все PASS.

> План подтверждён пользователем (2026-08-25 19:58 MSK): «реализовать вариант
> привязки В (рекомендован) — все техники, аура держит одну» + «начинай все
> запланированные задачи». Решения по §10 анализа приняты по умолчанию
> (K=12, движение без замедления, декей 1%/тик, NPC-паритет = Stage 2,
> прерывание только станом).

---

## Контекст

Запрос пользователя (2026-08-25 17:20) потребовал:
1. Анализ «задержки срабатывания техник» (выполнен в
   `checkpoints/08_25_technique_hold_analysis.md`)
2. Реализацию варианта В (аура держит одну)
3. Аудит документации (GLM-5.2 → 5.3) — выполнен субагентом (Task ID 9,
   отчёт в worklog.md)

Аудит нашёл ключевые расхождения:
- `docs_v2/TECHNIQUE_SYSTEM.md §5.1 vs §5.3` — внутреннее противоречие
  (upfront-трата Ци против fill-модели); код следовал §5.1 (мгновенный расход)
- `TechniqueChargeService` существовал (legacy-перенос), но **спал** —
  зарегистрирован в DI, никто не вызывал StartCharge
- `CombatService._pendingTechnique` — глобальный на бой (баг NPC→NPC,
  известный по чекпоинту 08_25_phase7_8_trade проблема №1)
- Проводимость медитативного масштаба (coreCapacity/360) — 15-секундный каст
  на L1 (нужен K-множитель)

---

## Что сделано

### Stage 0 — Модель заполнения (charge-by-conductivity)

**Новые контракты** (`Core/Messaging/Contracts/TechniqueChargeContracts.cs`):
- `TechniqueChargeStartedEvent` (entityId, techId, qiCost, capacity, chargeRatePermil)
- `TechniqueChargeProgressEvent` (per-tick; charged, qiCost, potency)
- `TechniqueChargeCompletedEvent` (potency, chargedQi, mouse)
- `TechniqueChargeCancelledEvent` (refund, reason)

**`TechniqueChargeService` — полная переработка:**
- per-entity ChargeState (исправляет баг глобального PendingTechnique)
- chargeRate = finalConductivity × COMBAT_CHANNEL_MULT × (1 + mastery × 0.005)
- расход Ци тиками через `QiConsumeRequestEvent` (медитативная модель доки)
- окно перезарядки [qiCost..capacity] (potency 1000→2000 подготовлено для Stage 2)
- отмена с возвратом 50% ChargedQi
- **P0-DUAL-PLAYER-ID fix:** нормализация "player"/"player_0" в кэше QiChangedEvent
  (тот же паттерн, что BodyService:455 из P0-фикса 08_25) — QiService публикует
  под "player", а PlayerService.PlayerId = "player_0"; `TryGetQiCache` ищет оба

**`TechniqueService.CompleteUse` (новый):** вызывается ПОСЛЕ зарядки —
установка кулдауна + рост мастерства + TechniqueUsedEvent. НЕ списывает Ци
(уже слито тиками зарядки). Существующий `UseTechnique` сохранён для legacy.

**`Constants`:** `COMBAT_CHANNEL_MULT = 12`, `MIN_CHARGE_RATE = 1.0`,
`CHARGE_CANCEL_REFUND_PERMIL = 500`, `AURA_HOLD_DECAY_PERMIL = 10`,
`POTENCY_BASE_PERMIL = 1000`, `POTENCY_MAX_PERMIL = 2000`.

**`CombatService.ExecuteAttack` + `ICombatService`:** + `potencyPermil` + `isCharged`
параметры. Если `isCharged || potencyPermil > 1000` → пропуск pending-таймера
(зарядка была временем каста). `BuildAndExecuteDamageRequest` использует
`_lastAttackPotencyPermil` (вместо спящего `GetTechniquePotencyPermil`).

**`AttackIntentEvent`:** + `PotencyPermil` (default 1000) + `IsCharged` (default false).

**`CombatModule.Tick`:** + `_techniqueChargeService.UpdateCharges(delta)`.
**`CombatModule.OnAttackIntent`:** прокинуло `e.PotencyPermil, e.IsCharged`.

### Stage 1 — Аура-задержка (вариант В)

**Новый `Modules/Player/AuraHoldService.cs`:** одиночный слот HeldTechnique:
- `Hold(techId, potency, chargedQi, qiCost, element)` → HeldTechniqueChangedEvent
- `Release()` → снимает (для fire); `Dissipate(reason)` → рассеивает с возвратом 50%
- `Tick(deltaTime)`: декей 1% QiCost/тик; при `ChargedQi < QiCost/2` → авто-рассеивание
- зарегистрирован в `PlayerModuleServices`; тикается из `PlayerModule.Tick`

**`PlayerTechniqueCaster` — переписан OnCastRequested + добавлен OnChargeCompleted:**
- OnCastRequested: если аура удерживает → Release + FireTechnique (второе нажатие);
  иначе → StartCharge (первое нажатие) после валидации (Cultivation, цели для Combat)
- OnChargeCompleted: аура свободна → Hold (park); аура занята → FireTechnique немедленно
- FireTechnique: CompleteUse + switch по типам (Combat → AttackIntentEvent с isCharged=true
  и potency; Healing → лечение × potency; Defense → щит × potency; Movement → dash;
  Sensory/Support/Curse → тост+визуал; Formation → StartDrawing)

### UI / Верификация

**`Adapter/Scene/ChargeSimDebug.cs` (новый, GODOT_CHARGE_SIM=1):** headless
сценарий: StartCharge → COMPLETED → HELD → PRESS 2 → RELEASE INTENT → damage.
Инстанцируется из `GameWorldController._Ready`.

**`TechniquesPanel._Process`:** показывает «⚡X%» для заряжаемой техники и
«⏸В ауре (Z — выпуск)» для удерживаемой. Подсказка обновлена.

### Документация (5.2 → 5.3, эволюция с кодом)

- `docs_v2/TECHNIQUE_SYSTEM.md`: статус-баннер (Stage 0+1 реализовано);
  §5.3 — примечание о медитативном масштабе + K-множитель с балансовой таблицей;
  **новая §5.4 «Аура-задержка (вариант В)»** — полный поток, декей, potency, NPC-паритет
- `docs_v2/QI_SYSTEM.md §4.2`: примечание о боевом прогоне меридиан (K=12) +
  напоминание, что ConductivityBoost удалён (перки — единственный способ ускорить)

### Локальные тесты (все PASS)

| Тест | Команда | Результат |
|------|---------|----------|
| build | `dotnet build` | 0 errors |
| newgame | `GODOT_NEWGAME=1` | 19 startables / 18 tickables; 6 техник выдано (L1); без исключений |
| **charge_sim** | `GODOT_CHARGE_SIM=1` | **PASS — fill model + aura hold + release all wired** (charge 64 qi ~2 тика, potency 1000, hold→release→110 dmg) |
| combat_sim | `GODOT_COMBAT_SIM=1` | PASS — обе стороны боя получают урон (без регрессии) |
| trade_debug | `GODOT_TRADE_DEBUG=1` | PASS — buy/sell + tick pause (без регрессии) |

**Числа CHARGE_SIM:** L1 Combat-техника qiCost=64, chargeRate=538‰ (34.4 qi/тик =
cond 2.78 × K 12 × masteryBonus ~1.03) → fill ~2 тика → COMPLETED potency=1000 →
HELD → PRESS 2 → AttackIntent isCharged=True → 110 dmg по NPC немедленно
(пропуск pending-таймера подтверждён).

---

## Решения

1. **K = 12** (CombatChannelMult) — по умолчанию из анализа §10 (пользователь не
   указал явно). L1 базовая техника ~2 тика, лёгкие реакции мгновенны.
2. **IsCharged bool** в AttackIntentEvent (в дополнение к PotencyPermil) — на Stage 0
   potency всегда 1000 (нет overcharge), поэтому нужен явный сигнал «пропустить
   pending». Potency останется для Stage 2 (overcharge 1001–2000).
3. **P0-DUAL-PLAYER-ID** нормализация в TechniqueChargeService — третья копия
   паттерна (после PlayerService:178 и BodyService:455). Техдолг: централизовать.
4. **NPC-паритет = Stage 2** — NPC используют "npc_strike" (без данных техники),
   зарядка не применяется; pending-таймер сохранён для NPC. Осознанная временная
   асимметрия, задокументирована в §5.4.
5. **Save/Load** — не реализован (минимальный scope). Активные зарядки теряются
   при сейве (можно добавить Cancel-on-save в следующей итерации).
6. **Движение при зарядке** — без ограничений (по умолчанию из анализа).
7. **Прерывание** — только стан/смерть/медитация (Dissipate с возвратом 50%).
   Урон по зарядке НЕ прерывает (иначе неиграбельно).

---

## Файлы

**Новые (3):**
- `game/src/Core/Messaging/Contracts/TechniqueChargeContracts.cs` — 5 событий
- `game/src/Modules/Player/AuraHoldService.cs` — слот ауры + декей
- `game/src/Adapter/Scene/ChargeSimDebug.cs` — headless верификация

**Изменённые (8):**
- `game/src/Core/Data/Constants.cs` — +6 констант (Stage 0+1)
- `game/src/Core/Messaging/Contracts/CombatContracts.cs` — AttackIntentEvent + PotencyPermil, IsCharged
- `game/src/Core/Interfaces/ICombatService.cs` — ExecuteAttack + potency, isCharged
- `game/src/Modules/Combat/TechniqueChargeService.cs` — переработка под per-entity + проводимость
- `game/src/Modules/Combat/TechniqueService.cs` — + CompleteUse (post-charge)
- `game/src/Modules/Combat/CombatService.cs` — potency/isCharged + skip pending + _lastAttackPotencyPermil
- `game/src/Modules/Combat/CombatModule.cs` — Tick UpdateCharges + forward e.PotencyPermil/IsCharged
- `game/src/Modules/Player/PlayerTechniqueCaster.cs` — переписан под charge + aura + release
- `game/src/Modules/Player/PlayerModule.cs` — + AuraHoldService inj + Tick decay
- `game/src/Adapter/UI/TechniquesPanel.cs` — + charge progress / held marker
- `game/src/Adapter/Scene/GameWorldController.cs` — + ChargeSimDebug инстанцирование

**Доки (2):**
- `docs/docs_v2/02_systems/TECHNIQUE_SYSTEM.md` — статус + §5.3 K + §5.4 аура
- `docs/docs_v2/02_systems/QI_SYSTEM.md` — §4.2 боевой прогон меридиан

---

## Найденные проблемы / Техдолг

1. **P0-DUAL-PLAYER-ID** — третья копия нормализации (PlayerService, BodyService,
   TechniqueChargeService). Нужна централизация (в IGameSession или helper).
2. **NPC-паритет** — Stage 2: NPC должны получать данные техники (через
   NPCAssembly) и идти через тот же charge path. Сейчас "npc_strike" — заглушка.
3. **Save/Load зарядок** — не реализовано. На сейв активные зарядки теряются
   (минимально: Cancel-on-save с возвратом 50%).
4. **Перезарядка (overcharge)** — Stage 2: окно [qiCost..capacity] → potency
   1001-2000; дестабилизация §7 (сейчас только подготовлено в коде, не активна).
5. **`CombatService.GetTechniquePotencyPermil`** — мёртвый код (заменён на
   `_lastAttackPotencyPermil`); удалить в следующей чистке.
6. **`docs/docs/` (v1, 52 файла)** — не помечены архивным баннером (аудит P2);
   источник истины — `docs_v2/`, но v1 не имеет предупреждения.
7. **ALGORITHMS §15 / TECHNIQUE_USAGE_REPORT.md** — дублируют устаревшую формулу
   15.15с; при будущей чистке обновить (аудит P1).

---

## Следующие шаги

1. **Save/Load зарядок** — Cancel-on-save (минимальный объём, закрывает edge-case)
2. **Stage 2 (опционально):** перезарядка + дестабилизация; NPC-паритет; реакция
   NPC AI на ауру игрока; HoldPolicy-исключения для Ultimate-техник
3. **Phase 3:** Faction port (Ai-game3-ref → Modules/World)
4. **Refactor:** централизация DualPlayerId; чистка мёртвого GetTechniquePotencyPermil
5. **Доки:** архивный баннер для docs/ v1; обновить ALGORITHMS §15 +
   TECHNIQUE_USAGE_REPORT.md (аудит P1)
6. P0 живьём в редакторе: страж-союзник; слоты пояса end-to-end; лут-дроп + E

---

## Коммиты

- `7655ee6` — Checkpoint expand + analysis (вечерняя сессия, запрос 17:20)
- *(этот чекпоинт — отдельный коммит после реализации Stage 0+1)*
