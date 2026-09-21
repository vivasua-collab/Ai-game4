# R24-C: мультибой «реестр входов» + R26: индикация заряда/ауры в хотбаре

**Дата:** 2026-09-21 (сессия R24, эпизод 1)
**Решения пользователя:** мультибой — вариант **C**; индикация заряда —
**идея 1 «слот-центричная»**; спуск = **Z / та же клавиша слота** (без
отдельной кнопки).
**План-источник:** checkpoints/plans/2026-09-20_r23_multicombat_aoe_design.md
(§1-C, §6.5); исследование DF/RimWorld/M&B — §6.1.

---

## 1. Мультибой C (CombatService + CombatModule) — CMB-2 закрыт

| Правка | Место | Суть |
|---|---|---|
| Гейт участника 1v1 | `ExecuteAttack` | **УДАЛЁН** (корень CMB-2: чужая стычка NPC-NPC блокировала атаки игрока) |
| UI-сессия | `ExecuteAttack` + `CombatModule.OnAttackIntent` | StartCombat только при `pairHasPlayer` (игрок в паре); NPC-NPC дерутся «молча» |
| Реестр дерущихся | `UpdateTimer` | Тик readiness/pendingCasts **ВСЕГДА** (не только в UI-бое); таймаут/_combatTimer — только UI-сессия |
| Инициатор | `GetReadinessPermil`/`ConsumeReadiness` | Незарегистрированный = готов (первый удар); запись заводится при расходе — иначе deadlock первого интента NPC-пары |
| Defender-резолв | `ResolveDefenderIdFor` | Участнику UI-боя — пара; не-участнику (толпа/NPC-NPC) — ЗАЯВЛЕННАЯ цель (мгновенный/charged/pending пути) |
| EndCombat | `EndCombat` | Чистит реестры ТОЛЬКО участников UI-пары (`RemoveFighter`); чужие тихие касты/готовность живут |
| Смерть | `BuildAndExecuteDamageRequest` | Не-участник UI-пары умирает «тихо» (труп по NPCDeathEvent, месть per-target); UI-бой — только если погиб участник |

Побочные эффекты (намеренные): NPC-NPC стычки теперь реально дерутся без
слота (RimWorld/DF «скрытая жизнь мира»); толпа бьёт игрока и игрок бьёт
толпу; месть NPC-NPC работает через DamageAppliedEvent (уже было).

## 2. R26: индикация заряда + удержания (HotbarPanel)

- Подписки: ChargeStarted/Progress/Completed/Cancelled +
  HeldTechniqueChanged (прежде — только QA-дебаггер; игрок не видел ничего).
- **Заряд:** изумрудная полоса снизу-вверх (ChargedQi/QiCost), «87%»,
  пульс рамки; overcharge — янтарная полоска сверху + «×N.N» (potency).
- **Удержание в ауре:** рамка цвета стихии (ElementStyle), метка «◉».
- Спуск = повторное нажатие Z/3-9/клик (механика PlayerTechniqueCaster
  08_25 без изменений — решение пользователя).

## 3. QA

- COMBAT_SIM **3g** (новый): (a) тихий NPC-NPC — Accepted, урон, UI-сессия
  НЕ открывается (immediate-замер в кадре — месть переоткрывает позже),
  месть = Threats[B]; (b) retry-открытие UI-сессии парой с игроком;
  (b2) **CMB-2-регресс: не-участник бьёт игрока в активном бое**
  (Accepted + урон).
- Гонки NPCModule (автоатаки мстящих NPC занимают каст-слоты и
  переоткрывают сессию в паузах) — лечится WaitForOwnCastClear +
  DebugSetReadiness + retry-циклы (soft-determinism).
- Прогоны: build 0 err; COMBAT_SIM PASS; **полная регрессия 18/18 PASS**
  (COMBATAI, ANIMALQA, CHARGE, HOTBAR, REASSEMBLY и пр. — гвайты
  мультибоя не сломали прежние сцены).

## 4. Доки

- COMBAT_SYSTEM §1.4.0 «МУЛЬТИБОЙ реестр входов (R24-C)» — вся модель.
- TECHNIQUE_SYSTEM §5.4 «Индикация в UI (R26)».
- TESTING_RULES: COMBAT_SIM + 3g.

## 5. Далее (роадмап R23 §6.7)

- R25: AoE-A «мгновенный залп» (AoEResolver int-геометрия, пер-таргет
  пайплайн, месть нейтралов бесплатна — §6.3, хук SpareAllies §6.4).
- R27: TargetingService (Tab-цикл, курсор-прицел, line-гейт melee).
- R28: хоуминг A (снаряды-сущности, turn-budget).
- Git: push восстановлен (токен обновлён 2026-09-21, коммиты
  2a50578..24df21a были НА GitHub — «ahead 2» прошлой сессии был
  артефактом устаревшего remote-tracking без fetch).
