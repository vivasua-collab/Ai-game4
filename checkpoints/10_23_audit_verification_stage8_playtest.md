# Чекпоинт: Верификация аудитора — Этап-8 и плейтест-фиксы воспроизведены firsthand

**Дата:** 2026-10-23 06:45 UTC
**Сессия:** continuation-audit (main Z.ai Code, режим lite)
**Тип:** audit (сверка заявлений кодера с кодом и исполнением)

---

## Контекст

Задача из SESSION_SUMMARY («Аудитору: сверка вердиктов ST8-01..06 по
de5a8f4 … и следующий проход Этапа-9») плюс заявление-контроль из
протокола: «После получения отчётов … проверить, соответствуют ли
заявления кодера реальным изменениям кода и воспроизводимым логам».
Окружение восстановлено cold_start (.NET 8/9, Godot 4.7.2 mono
ed1daf0bf, build 0 errors). Верификация: чтение кода по HEAD
(75b8634) + повторное исполнение headless-симов в этой сессии.

## Результаты: Этап-8 (коммит de5a8f4, отчёт 10_22_stage8_technique_report.md)

Сверка «заявление → реальный код» — все 6 фиксов подтверждены:

| # | Заявление кодера | Факт в коде (HEAD) | Код | Сим |
|---|------------------|-------------------|-----|-----|
| ST8-01 | Вырожденное направление ≠ круг | `AoEResolver.cs:300,315,331` — `dx==0&&dy==0 → return false` (Cone/Line/Semicircle); `PlayerTechniqueCaster.IsAoeAimValid:761` + отказ «Прицел в свою клетку…» (:802); Circle не тронут | ✅ | ✅ |
| ST8-02 | Удержание живёт при отказе выпуска | `FireTechnique → bool` (:364); Release только при успехе (:205 комментарий «снимаем с ауры (fire уже применил эффект)») | ✅ | ✅ |
| ST8-03 | Один TechniqueUsedEvent на применение | `TechniqueService.CompleteUse(techniqueId, publishUsedEvent=true)` (:540); Combat-путь молчит; единственный издатель — CombatService (:921 homing, :1081 AoE с комментарием «Один TechniqueUsedEvent на ЗАЛП») | ✅ | ✅ |
| ST8-04 | Прерывания §5.4 wired | `AuraHoldService` (:40-42, подписки PlayerDeathEvent/MeditationStateChangedEvent/BuffAppliedEvent) и `TechniqueChargeService` (:59-61) — CancelCharge/Dissipate с возвратом 50% | ✅ | ✅ |
| ST8-05 | Честный щит | `QiBufferService.cs:95-107,156` — пул < MIN_QI_FOR_BUFFER → отказ; резервный режим реально поглощает | ✅ | ✅ |
| ST8-06 | Возврат от post-decay остатка | `AuraHoldService.Tick` — декей фиксируется ДО порога (:180-187), refund = 50% × newCharged (:193) | ✅ | ✅ |

**Сим STAGE8 повторён этой сессией firsthand: VERDICT: PASS — ST8-01..06
закрыты** (лог кодера 10_22_stage8_technique.log: PASS=30 FAIL=0 —
совпадает с воспроизведением; формулировки ассертов в логе соответствуют
сценариям A–F).

## Результаты: плейтест-фиксы PT-001..005 (коммит 75b8634)

| # | Заявление | Факт в коде (HEAD) | Сим |
|---|-----------|-------------------|-----|
| PT-001 | Агро-сброс на смерть игрока | `NPCAIService.OnPlayerDeath:740` (полный сброс через PublishDisengage-путь), `AnimalService.OnPlayerDeath:686` (месть зверей), `NPCModule.OnPlayerDeathForGate:318` (гейт «не бьём труп»); DoT-атрибуция по OriginalAttackerId (`NPCCombatAdapter:268`, `ElementalEffectService:134,224`, `NPCAIService:575`) | RESPAWN шаг 5 PASS firsthand |
| PT-002 | Квест-журнал layout | `QuestWindow.cs:159` PanelContainer (комментарий PT-002, карты были 736×0), `:217` HSeparator | UI-класс дефекта закрыт |
| PT-003 | X = только слоты 3-9 | `GameWorldController:1799+` — цикл по слотам 3-9, wrap по занятым, пусто → подсказка; CycleSelection удалён; HotbarPanel золотая рамка (:78) | HOTBAR PASS firsthand |
| PT-004 | Отказ цели называет радиус + Tab | `PlayerTechniqueCaster:256,272` — «Нет цели в радиусе {N} кл. — Tab выбирает цель, подойдите ближе» для одиночных подтипов | в RESPAWN/COMBAT_SIM зоне |
| PT-005 | Dual-path спрайты | `SpriteSheetCache.cs:229-237` — ResourceLoader.Exists → Load<Texture2D> (импортированный ресурс), raw Image.LoadFromFile — только фолбэк; предупреждение экспорта снято | ANIMQA PASS firsthand |

## Регрессия: воспроизведена полностью, firsthand

Godot 4.7.2.stable.mono.official.ed1daf0bf; .NET 8.0.425/9.0.319;
build 0 errors; HEAD 75b8634 (рабочее дерево чистое).

`bash tools/qa_regression.sh` — все 27 симов раннера, батчами foreground
(платформа убивает фоновые): PASS 27/27. Отдельно FORM
(`GODOT_FORM_DEBUG=1`) — PASS. **Итого 28/28 — заявление кодера
подтверждено исполнением.** Детали: checkpoints/10_23_audit_verification.log.

Воспроизведённые вердикты: COMBAT_SIM, COMBATAI, RESPAWN (шаг 5 —
агро-сброс PT-001), ANIMALQA, LOOT, DOT, SAVELOAD, STAGE4, STAGE5,
STAGE6, STAGE7, **STAGE8**, KILLFEED, QUEST, STORAGE, HOTBAR, TRASHDROP,
CONTEXT, MODALQA, MODAL2, L500, WEAPONVIS, REASSEMBLY, CHARGE,
AUDIT0922, CHARGERQA, ANIMQA + FORM.

## Честные границы этой верификации

1. **Headless-симы + чтение кода, НЕ физический плейтест.** Пункты
   PT-T01..T07 (техники/Ци/ауры), PT-C01..C03 (повторные физраунды),
   PT-C04..C09/PT-Q/PT-S02+ остаются незакрытыми до раунда 2 владельца.
   Канон: «Не считать пункт физического плейтеста пройденным на
   основании симулятора» — соблюдён.
2. **Остаточные риски Этапа-8 из отчёта кодера остаются открытыми**:
   SpareAllies без party-системы; семантика «щит тратит пул, не
   инвестицию» (документирована кодом, возможный дизайн-ревайк);
   basic_attack больше не публикует TechniqueUsedEvent (будущему
   killfeed «применённых техник» нужен свой контракт); стюн только
   через BuffAppliedEvent; зарядка, начатая В медитации, не
   блокируется на уровне кастера (недостижимо через UI).
3. Формулировки причин отказов/тостов не проверялись визуально в
   рендере (только код-пути и сим-ассерты).

## Решения

- Заявления кодера по Этапу-8 и PT-001..005 СООТВЕТСТВУЮТ реальному коду
  и воспроизводимы — реестр bugs.json (STAGE8-001..006, PT-001..005
  verified) не противоречит фактам.
- Семантика «verified (реестр) ≠ passed (физраунд владельца)» сохранена
  (см. 10_23_playtest_registry_vs_checklist.md).

## Следующие шаги

1. Кодер: [STAGE 9 AUDIT] и [STAGE 10 AUDIT] — каноны в
   checkpoints/10_23_stage{9,10}_*_audit_request.md.
2. Владелец: раунд 2 физплейтеста в свободное время.
3. Аудитор: после отчётов 9/10 — сверка по той же схеме; затем общий
   анализ всех десяти этапов.

## Файлы

- Этот чекпоинт + `checkpoints/10_23_audit_verification.log` (транскрипт
  вердиктов воспроизведения)
- Код: game/src/Modules/{Combat,Player,Qi,NPC,Inventory}/…, game/src/Adapter/{Scene,UI}/…
- Отчёты кодера: checkpoints/10_22_stage8_technique_report.md, checkpoints/10_23_playtest_r1_report.md
