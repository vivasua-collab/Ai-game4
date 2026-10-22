# Чекпоинт: Этап-8 аудита владельца — техники, Ци и боевые эффекты (ST8-01…ST8-06)

**Дата:** 2026-10-22 15:55 UTC
**Сессия:** Продолжение после обрыва (Stage 6-7 закрыты 8/8, QA 27/27); запрос аудитора «[STAGE 8 AUDIT] Techniques, Qi and effects» (GitHub Issue создать не удалось — 403 integration; запрос исполнен напрямую в этой сессии).
**Тип:** audit → fix + regression

---

## Контекст

Аудитор (владелец) представил первичный аудит Этапа-8: три подтверждённых по чтению кода
логических дефекта (ST8-01/02/03, высокий-высокий-средний приоритет) и три направления
проверки (ST8-04/05/06). Аудит охватывает функциональный код: `portal/` и `web-portal/`
исключены; портовые изменения в функциональные коммиты не включались. Playtest в Godot
на стороне аудитора не выполнялся (владелец — физический плейтест не раньше 25.10);
верификация — headless-симами в этой сессии, по фактическому состоянию сервисов,
событий и HP/Ци.

## Верификация находок по коду (все 6 подтверждены до фиксов)

| # | Причинная цепочка (до фикса) | Статус |
|---|---|---|
| ST8-01 | `AoEResolver.IsInsideShape`: Cone `dx==0&&dy==0 → return true` (все в радиусе), Line — то же, Semicircle — `Dot(v,0,0)=0 ≥ 0` пропускает всё; `IsAoeAimValid` не запрещал прицел в свою клетку | Подтверждён |
| ST8-02 | `PlayerTechniqueCaster.OnCastRequested:184-185`: `_aura.Release()` после `FireTechnique` безусловно, а у FireTechnique были отказы ДО `CompleteUse` («Цель исчезла», «Тело не ранено», «Формация уже создаётся») | Подтверждён |
| ST8-03 | `TechniqueService.CompleteUse:550` публиковал `TechniqueUsedEvent`; CombatService публиковал повторно (919 homing / 1082 AoE / 1430 generic) → `StatProgressProducer.OnTechniqueUsed` начислял INT-дельту ×2 за один Combat-каст | Подтверждён |
| ST8-04 | §5.4:274 документирует «Принудительное рассеивание: стюн/смерть/медитация», но вызовов Dissipate/CancelCharge по этим причинам не было (только save/world_reset) — мёртвый игрок продолжал зарядку | Подтверждён (код ≠ дизайн) |
| ST8-05 | `QiBufferService.Activate:91-99`: при неуспешном списании `QiInvested=0`, `IsActive=true`, публикует `QiBufferActivatedEvent`; `AbsorbDamage:139` при `CurrentQi < MIN_QI_FOR_BUFFER` — ноль поглощения; кастер публиковал успех безусловно | Подтверждён |
| ST8-06 | `AuraHoldService.Tick:143-151`: порог по post-decay `newCharged`, возврат 50% от pre-decay `h.ChargedQi` | Подтверждён |

## Что сделано (коммит фиксов)

**Коммит: `de5a8f4`** (fix(techniques-qi): Этап-8 аудита — ST8-01..06), 10 файлов, +1477/−59.

- **ST8-01** — направленная AoE без направления — ничто, а не круг:
  `AoEResolver`: Cone/Line `dx==0&&dy==0 → false`; Semicircle — проверка вырождения до Dot.
  `PlayerTechniqueCaster.IsAoeAimValid`: Cone/Semicircle/Line + прицел в свою клетку →
  отказ «Прицел в свою клетку — задайте направление» (Circle не тронут — эпицентр легален).
- **ST8-02** — удержание живёт при отказе выпуска:
  `FireTechnique → bool` (true ⇔ выпуск состоялся); held-путь: `Release` только при
  успехе; отказы (цель ушла, тело цело, формация занята, Qi-гейт Defense, синхронный
  AttackRejected) сохраняют удержание — Ци остаётся в ауре, декей §5.4 тает, выпуск
  повторим. Дополнительно честная причина старта «Уже заряжается другая техника»
  (прежде — неверное «Нет слота»/«Недостаточно Ци» при занятом StartCharge).
- **ST8-03** — единый контракт события:
  `TechniqueService.CompleteUse(techniqueId, publishUsedEvent=true)`; Combat-каст
  игрока вызывает с `publishUsedEvent:false` — единственный издатель для Combat —
  CombatService в момент фактического пуска (после гейтов): синхронный отказ
  (AttackRejected → RefundUse) не оставляет ложного INT-прироста. Healing/Poison/Curse/
  Formation идут через CompleteUse — по одному событию. Дополнительно закрыт найденный
  симом `basic_attack`-пробел: Space-удар (не техника) больше не публикует
  TechniqueUsedEvent (прежде — INT +0.001 за каждый удар кулаком, §5.1 «использование
  ТЕХНИКИ → INT»).
- **ST8-04** — прерывания §5.4 wired:
  `AuraHoldService` + `TechniqueChargeService` подписаны на `PlayerDeathEvent`,
  `MeditationStateChangedEvent` (isActive) и `BuffAppliedEvent` (combat_stun на
  игроке): зарядка → `CancelCharge(reason)` с возвратом 50% ChargedQi; удержание →
  `Dissipate(reason)` с возвратом 50%. `CancelCharge` нормализует алиасы игрока.
  Поведение соответствовало задокументированному контракту §5.4 — код приведён к
  дизайну, документация не менялась.
- **ST8-05** — честный щит:
  `QiBufferService.Activate`: списание не удалось И пул < `MIN_QI_FOR_BUFFER`(10) →
  отказ активации (IsActive=false, без QiBufferActivatedEvent, только
  QiBufferStateChanged(false) для UI). Резервный режим (пул ≥ MIN, инвестиция не
  списалась) — активен с QiInvested=0 и РЕАЛЬНО поглощает из пула. Кастер:
  Defense-гейт до CompleteUse («Недостаточно Ци для щита»), резервный выпуск помечен
  в reason («Щит-резерв: без инвестиции Ци»); `TechniqueService.CachedCurrentQi`
  открыт для гейта (EVT-01-кэш).
- **ST8-06** — decay-возврат от фактического остатка:
  `AuraHoldService.Tick` фиксирует декей ДО проверки порога; возврат =
  50% × post-decay остаток на момент рассеивания.

## Регрессия (новая + существующие)

- **Новый сим:** `game/src/Adapter/Scene/Stage8TechniqueTruthSimDebug.cs`
  (`GODOT_STAGE8_DEBUG=1`, зарегистрирован в `GameWorldController` и `tools/qa_regression.sh`
  как STAGE8, №26 в реестре) — 30 ассертов по дословным сценариям A–F аудитора.
- **Команда прогона:**
  `GODOT_NEWGAME=1 GODOT_STAGE8_DEBUG=1 $GODOT --headless --path . scenes/MainMenu.tscn`
  ($GODOT = Godot 4.7.2 stable mono, `ed1daf0bf`; .NET SDK 8.0.425/9.0.319; build 0 errors)
  + контроль зоны воздействия: `bash tools/qa_regression.sh CHARGE DOT STAGE5`.
- **Результаты по сценариям** (полный лог: `checkpoints/10_22_stage8_technique.log`):

| Сценарий | Ассерты | Результат |
|---|---|---|
| A (ST8-01) | A1-A7: Cone/Semicircle/Line degenerate → 0 целей (3 цели в радиусе); направленные — только front (полукруг front+side); каст в свою клетку — отказ, 0 залпов | PASS 7/7 |
| B (ST8-02) | B1a-c Combat «Цель исчезла» → удержание живо, 0 событий, кулдаун/мастерство нетронуты, повторный выпуск применяет урон, INT +0.001; B2a-b Healing «Тело не ранено» → удержание живо, повторный выпуск лечит, INT +0.001 | PASS 5/5 |
| C (ST8-03) | C1: AoE-залп → ровно 1 событие; C2: TargetIds=[только front]; C3: синхронный AttackRejected (pending basic_attack) → refund, 0 событий, INTΔ=0, удержание живо; C4: повторный выпуск после отказа | PASS 4/4 |
| D (ST8-04) | D1 смерть в зарядке → CancelCharge("death") refund>0; D2 смерть при удержании → Dissipate refund=50%; D3 combat_stun в зарядке → CancelCharge("stun"); D4 стюн при удержании; D5 медитация при удержании; D6a/D6b world-reset → зарядка снята (world_reset) + аура рассеяна | PASS 8/8 |
| E (ST8-05) | E1 пул<MIN → отказ (не активен, 0 событий, 0 поглощения); E2 резерв (пул≥MIN, invest=0) реально поглощает 5/5; E3 Defense при низком пуле → «Недостаточно Ци для щита», удержание живо, кулдаун 0; E4 после регена → успех, инвестировано 72 | PASS 4/4 |
| F (ST8-06) | F1: 60→45, возврат 22 (не 30); F2 boundary 51→50 == порог, возврат 25; F3: 52→51 живёт, следующий тик рассеивает | PASS 3/3 |

- **Итог сима:** `PASS=30 FAIL=0 NOT TESTED=0` → **VERDICT: PASS — ST8-01..06 закрыты**.
- **Побочные регрессы:** `CHARGE → PASS`, `DOT → PASS`, `STAGE5 → PASS` (3/3) —
  модель заполнения/удержания/выпуска, DoT-пайплайн и истина смерти не задеты.

## Файлы

- Фиксы: `game/src/Modules/Combat/AoEResolver.cs`, `CombatService.cs`,
  `TechniqueChargeService.cs`, `TechniqueService.cs`,
  `game/src/Modules/Player/AuraHoldService.cs`, `PlayerTechniqueCaster.cs`,
  `game/src/Modules/Qi/QiBufferService.cs`
- Тест: `game/src/Adapter/Scene/Stage8TechniqueTruthSimDebug.cs`;
  регистрация: `GameWorldController.cs`, `tools/qa_regression.sh` (STAGE8)
- Отчёт: `checkpoints/10_22_stage8_technique_report.md` (этот файл);
  лог: `checkpoints/10_22_stage8_technique.log` (исключение в `.gitignore`
  `!checkpoints/10_22_*.log` — файл реально доступен в git)

## Оставшиеся риски (техники/Ци/эффекты/прерывания)

1. **AoE SpareAllies-хук** (§6.4) остался без party-системы — при появлении сопартийцев
   потребуется сценарий щадящих залпов (за пределами Stage 8).
2. **Пасcивные интеракции Qi-буфера**: AbsorbDamage использует ТЕКУЩИЙ пул, а не
   инвестицию (инвестиция — залог, возвращаемый при Deactivate). Если дизайн захочет
   «щит тратит только вложенное», потребуется отдельная ревизия — сейчас поведение
   документировано кодом E-сценариев сима.
3. **`basic_attack`-событие** перестало публиковаться — единственный функциональный
   потребитель `TechniqueUsedEvent` (StatProgressProducer) учтён; если будущий UI
   захочет killfeed «применённых техник», ему нужен свой контракт (не через INT-дельту).
4. **Стюн по баффу `combat_stun`** — рассеивание срабатывает на ПРИМЕНЕНИЕ баффа;
   если появятся другие источники оглушения (не через BuffService), их нужно будет
   подключить к тому же контракту (BuffAppliedEvent или отдельный StunnedEvent).
5. **Медитация при зарядке**: старт медитации срывает зарядку (возврат 50%), но НАЧАЛО
   зарядки во время уже активной медитации не заблокировано на уровне кастера
   (нет события-триггера) — зарядка, начатая в медитации, живёт до первого
   MeditationStateChangedEvent. Практически недостижимо через UI (M-гейт ввода),
   отмечено для полноты.

## Следующие шаги

- Аудитору: сверить вердикты ST8-01…06 с реализацией по `de5a8f4` и логу; следующий
  проход — этап 9.
- Владельцу: физический плейтест не раньше 25.10 по вкладке «Плейтест» портала
  (PT-XX по номерам); техникам/Ци/аурам добавлены пункты физической проверки.
