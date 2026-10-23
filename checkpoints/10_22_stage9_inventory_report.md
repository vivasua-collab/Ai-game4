# Чекпоинт: Этап-9 аудита владельца — инвентарь, экипировка, крафт, переносы (ST9-01..03)

**Дата:** 2026-10-23 08:25 UTC
**Сессия:** coder-1023 (main Z.ai Code, режим lite)
**Тип:** audit → fix + regression
**Задание:** checkpoints/10_23_stage9_inventory_audit_request.md ([STAGE 9 AUDIT])
**Статус публикации:** до этой работы этапы 9/10 в репо отсутствовали
(10_23_stage9_10_publication_status.md) — этап 9 закрыт этой сессией, этап 10 — следующим коммитом.

---

## Коммиты

| Коммит | Тип | Содержимое |
|--------|-----|-----------|
| `12011e7` | test | Stage9InventoryTruthSimDebug (24 ассерта) + регистрация GODOT_STAGE9_DEBUG в GameWorldController и tools/qa_regression.sh (STAGE9, №27) + префикс-лог |
| `a83c36e` | fix | **ST9-01** — транзакционный расход ингредиентов CraftingService.TryCraft (откат при mid-loop отказе) |

## Окружение и команды

- Сборка: `dotnet build` — **0 errors** (.NET SDK 8.0.425/9.0.319, `DOTNET_ROOT=/home/z/.dotnet`).
- Сим: `GODOT_NEWGAME=1 GODOT_STAGE9_DEBUG=1 $GODOT --headless --path . scenes/MainMenu.tscn`
  ($GODOT = Godot 4.7.2 stable mono, `ed1daf0bf`).
- Зона воздействия: `bash tools/qa_regression.sh STORAGE TRASHDROP CONTEXT CHARGERQA STAGE6 STAGE7`.
- Логи: `checkpoints/10_22_stage9_inventory_prefix.log` (до фикса), 
  `checkpoints/10_22_stage9_inventory_report.log` (после; итоговый).
- Exit codes: сим завершается вердиктом в stdout (процесс снимается сторожем
  qa_regression после VERDICT — канон раннера; вердикт-строка выше).

## Верификация находок (все — по коду ДО фикса + runtime)

| ID | Вердикт | Доказательство |
|----|---------|----------------|
| ST9-01 | **ПОДТВЕРЖДЁН как латентный дефект → ИСПРАВЛЕН** | Код: TryCraft удаляет ингредиенты последовательно, отката нет (строки расхода). Достижимость при текущей модели: НЕТ — ItemRemovedEvent не имеет подписчиков, между HasIngredients и удалениями мутаторов нет (A4a PASS: craft succeeds). Воспроизведение потери: A4b hostile-подписчик (модель будущего реактивного сервиса) → **wood 10→7 при отказавшем крафте** (префикс-лог). Постфикс: wood=10 — точный откат |
| ST9-02 | **ОПРОВЕРГНУТ (в текущем объёме)** | B1–B6 PASS: замена в занятом слоте возвращает старый ровно 1× (событие OldItemId); двуручное при занятом WeaponOff: щит возвращён 1× (EQ-A1 5-arg ctor), WeaponOff пуст, отказ WeaponOff при 2h не съедает предмет (rollback контракта UI); гейты (RequiredCultivationLevel=9, Strength≥999, заблокированный слот) — отказ без исчезновения; ампутация: возврат ровно 1×, повторное событие идемпотентно, слот блокируется, провайдер синхронизирован (включая ампутацию — EQ-A2); capture/restore инвентарь+кукла — сумма 1, без дюпа; провайдер после ResetWorld пуст под обоими ключами |
| ST9-03 | **ОПРОВЕРГНУТ (в текущем объёме)** | C1–C7 PASS: пояс полный round-trip (10==10); частичный возврат при volume-full: +7 в инвентарь, остаток 3 в слоте (INV-3 жив); зарядник: полный камень — канонический возврат (FromDomainStone → qistone_dust_calm с сохранённым объёмом), частичный (60/100) — отказ извлечения (анти-дюп), пустой — рассыпается; stale Guid (кучка израсходована целиком) — обе операции безопасно отказали; pickup overflow: +4 инвентарь/+6 земля = 10; частичный стак: +3/+5 = 8; двуручное при volume-full: снятый щит → земля +1, не потерян |

**Прочность (DUR): GAP** — система объявлена (ItemData.HasDurability/MaxDurability,
DurabilityCondition, Constants-таблицы), но НЕ подключена к реальному игровому
пути: `EquipmentStatAggregator.GetDurabilityEfficiency` — заглушка «Pristine 1.0»
(«В будущих фазах: отслеживание текущей прочности через InstanceData»); сервисов
износа/ремонта нет; CurrentDurability не существует. PASS не имитировался —
строка DUR в сводке сима. **Решение за владельцем:** вводить износ (нужна
спецификация: скорость износа, эффекты Broken, ремонт) или зафиксировать
«прочность отложена» в доках.

## Результаты по сценариям (сводка сима, 24 PASS / 0 FAIL / 0 NOT TESTED / 1 GAP)

```
A1  PASS  успешный крафт: wood 10→7, stone 6→4, arrow=5, events 1/0
A2  PASS  нехватка ингредиента: без списаний, fail-event 1
A3  PASS  неизвестный результат: гейт ItemDatabase, без списаний
A4a PASS  текущая модель: mid-loop отказ недостижим (нет мутаторов)
A4b PASS  hostile-подписчик: отказ + ПОЛНЫЙ откат (wood=10); до фикса wood=7
A5  PASS  volume-full при выдаче: стрелы inv 3 + ground 2 = 5
A6  PASS  2 крафта: events=2, arrow=10 — дублей нет
B1  PASS  swap: старый возвращён 1×, провайдер player+player_0 синхронен
B2  PASS  двуручное: щит возвращён 1×; WeaponOff при 2h — отказ с rollback
B3a PASS  гейт культивации L9: отказ, предмет в инвентаре
B3b PASS  гейт Strength≥999: отказ, предмет в инвентаре
B4  PASS  ампутация: возврат 1×, повтор идемпотентен, слот блокируется
B5  PASS  capture/restore: сумма 1==1, без дюпа/потери
B6  PASS  провайдер после ResetWorld пуст (статы прошлого мира не протекают)
C1  PASS  пояс: assign 10, takeback, сумма 10==10
C2  PASS  частичный возврат: inv +7, остаток 3 в поясе (INV-3)
C3a PASS  insert slot-addressed: inv 1, зарядник 1
C3b PASS  extract полный: канонический возврат dust_calm=1, зарядник пуст
C3c PASS  частичный 60/100: отказ извлечения (анти-дюп)
C3d PASS  пустой: рассыпался; 2 камня → 1 предмет (второй отдал Ци буферу)
C6  PASS  stale Guid: обе операции безопасно отказали
C4  PASS  pickup overflow: inv +4, ground +6, сумма 10
C5  PASS  частичный стак: inv +3, ground +5 = 8
C7  PASS  двуручное при volume-full: щит → земля +1
DUR GAP   прочность не подключена к игровому пути (см. выше)
Итого: PASS=24 FAIL=0 NOT TESTED=0 GAP=1
VERDICT: PASS — ST9-01..03 проверены (прочность = GAP)
```

## Регрессия зоны воздействия (после фикса ST9-01)

STORAGE / TRASHDROP / CONTEXT / CHARGERQA / STAGE6 / STAGE7 — **6/6 PASS**
(08:23 UTC). Крафт-пайплайн и источники поступлений (BUG-022) не задеты.

## Решения

- ST9-01 закрыт транзакционным откатом (минимальный доменный фикс, без
  изменения контрактов IInventoryService).
- A4a/A4b разделяют «латентность» и «воспроизводимость»: пути достаточно
  для будущих аудитов (регресс A4b охраняет откат навсегда).
- Прочность — GAP для владельца, не имитировалась.

## Остаточные риски и непроверенные сценарии

1. **Полный Save→Load→снятие** (B5 сервисный уровень: capture/restore обоих
   блоков) — полный путь SaveService/RestoreOrder покрыт существующими
   SAVELOAD/STAGE4-симами, но сценарий «экипировка→сейв→загрузка→снятие»
   в связке с инвентарём стоит добавить в Stage-10 lifecycle-регресс.
2. **Мульти-кучки одного ItemId** при частичном возврате из пояса: инвариант
   суммы проверен; раскладка по кучкам (GUID-адресность) — вне этапа.
3. **Канонический возврат камня** (FromDomainStone → ItemId размера):
   кастомные/сгенерированные камни с нестандартным ItemId возвращаются
   каноническим предметом с сохранённым объёмом Ци — поведение задокументировано
   симом C3b; если дизайн захочет «тот же предмет», нужен рефактор моста
   (вне этапа 9).
4. Физический плейтест пунктов инвентаря (перенос в поясе/заряднике через UI)
   — не выполнялся; сим работает через сервисные контракты, UI-слой
   (InventoryWindow/BeltSlotRow/ChargerWindow) покрыт существующими
   STORAGE/CONTEXT/CHARGERQA-симами.

## Следующие шаги

- Этап-10 (ST10-01..04): реализация следующим эпизодом — сим
  Stage10WorldLifecycleTruthSimDebug + артефакты
  checkpoints/10_23_stage10_lifecycle_report.md/.log.
- Этапы 11/12 — задания в git (10_23_stage{11,12}_*_audit_request.md);
  playtest-дополнения PT-B01..B06/PT-PR01..PR06 вносятся на соответствующих
  этапах в portal/data/playtest.json (статусы владельца не трогаются).

## Файлы

- Сим: `game/src/Adapter/Scene/Stage9InventoryTruthSimDebug.cs`
- Фикс: `game/src/Modules/Inventory/CraftingService.cs`
- Регистрация: `game/src/Adapter/Scene/GameWorldController.cs`, `tools/qa_regression.sh`
- Логи: `checkpoints/10_22_stage9_inventory_prefix.log`,
  `checkpoints/10_22_stage9_inventory_report.log`
- Отчёт: этот файл
