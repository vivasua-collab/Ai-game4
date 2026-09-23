# R37-c — Зарядник Ци не принимает камни (баг-репорт 23.09)

> Раунд: R37-c · Дата: 2026-09-23 · Источник: прямой баг-репорт владельца
> (сессия после сбоя окружения на фразе «xauth отсутствует — запускаю Xvfb
> вручную», скриншот upload/pasted_image_1790136541567.png).

## 0. Контекст и репорт

Владелец: «не могу добавить камни Ци в него [зарядник], лекарство добавляется
без проблем». Зарядник получен читом «Зарядник Ци» (CheatPanel, секция
«Расходники:»), надет в слот пояса.

## 1. Диагноз (VLM-скрин + аудит кода)

Полный разбор причин (все — «обещание без реализации», layer-мир домена
Charger никогда не был связан с предметами):

1. **Окна зарядника H НЕ СУЩЕСТВОВАЛО**: ItemUseService тост для камня
   («Камень Ци нельзя поглотить напрямую — вставьте в зарядник (H)»,
   канон A6 от 21.09) обещал окно, которого нет: InputMap-действия
   `charger_window` нет, файла ChargerWindow.cs нет, в GWC окна нет.
2. **Моста предметы↔домен НЕ БЫЛО**: модуль Charger (ChargerService: слоты
   QiStone/буфер/тепло/проводимость — полный домен по CHARGER_SYSTEM.md) не
   знает о QiStoneData (предметы инвентаря, канон GENERATORS_SYSTEM §10:
   1024×см³): домен заряжался ТОЛЬКО из сейва (RestoreState).
   ChargerInitPhase — стаб («Stub for v1»).
3. **Зарядник маскировался под пояс** (видимая часть репорта):
   `BeltService.IsBeltEquipped` = «ЛЮБОЙ предмет слота Belt» → надетый
   зарядник включал BeltSlotRow «Пояс — слоты быстрого доступа» → drag камня
   в слот молча отвергался (`_CanDropData` требует ItemCategory.Consumable,
   камень = QiStone), а лекарство (Consumable) добавлялось «без проблем».
   Молчаливый отказ — ни тоста, ни причины.
4. Побочное: ChargerModule.Start() активировал домен БЕЗУСЛОВНО — камни
   качались в буфер даже без надетого зарядника.

## 2. Фиксы

- **ChargerItemBridge** (Modules/Charger, новый): единственная точка конверсии
  QiStoneData↔доменный QiStone (quality=Common — предметного качества нет по
  §10; size-маппинг Dust..Boulder→Tiny..Huge; element=Neutral; maxQi/currentQi
  = QiAmount/QiRemaining предмета — фактический объём, не доменный дефолт).
  Владелец режима домена: активен ТОЛЬКО при надетом заряднике
  (EquipmentChangedEvent; EquipmentService.RestoreState публикует события →
  синхронизация и после Load). Слот-адресная вставка (R10 P1-SlotId, Guid
  кучки). Отказы — только с тостом-причиной (B1-пруф).
- **ChargerWindow** (Adapter/UI, новый; клавиша H): модальное окно — 3 слота
  камней (drag&drop из инвентаря, ПКМ — извлечь), буфер Ци (значение+бар),
  тепло (уровень+состояние+цвет), режим Вкл/Выкл (§4.2), гейт-подсказка
  «Зарядник не надет» без зарядника. Обновление по событиям домена
  (ChargerStateChanged/BufferChanged/HeatChanged/EquipmentChanged).
- **InputMap/InputAdapter/PlayerInputService/IPlayerInputService/GWC**:
  action `charger_window` (Key.H), sticky-флаг, хоткей-хендлер с паузой
  (паттерн Q/J), Esc-ветка, модальные списки (AnyModalWindowOpen,
  OpenModalWindowNames «H·зарядник»), QA-аксессор ChargerWindowForQA.
- **ItemContextMenu**: кнопка «💎 Вставить в зарядник» для камней (гейт
  IsChargerEquipped; tooltip-подсказка «не надет»); InventoryWindow
  делегирует мосту (TryInsertToCharger).
- **BeltService.IsBeltEquipped**: пояс = предмет БРОНИ в слоте Belt
  (armor_belt, ItemType=="Armor"). Зарядник — НЕ пояс: слоты 3-9 (лекарства)
  работают только с настоящим поясом. Сим I2 подтверждает: armor_belt →
  IsBeltEquipped=true (функциональность пояса сохранена для пояса).
- **ChargerService**: GetSlotSnapshot (immutable-снимок для UI/моста);
  RestoreState — maxQi камня ИЗ СЕЙВА (stoneMaxQi), не доменный дефолт
  (иначе мост-камень 1024 урезался до BaseQi(Tiny)=100 при reload).
- **QiStone**: ctor-перегрузка (quality, size, element, maxQi, currentQi).
- **ChargerModule.Start**: безусловный Activate() → bridge.Initialize()
  (гейт по экипировке).

### 2.1. Контракт анти-дюпа (извлечение)

Инвентарь хранит стаки ItemId+Count — per-instance состояния НЕТ. Поэтому:
- полный камень (QiRemaining==QiAmount) — извлекается и возвращается в
  инвентарь честно;
- частично-израсходованный — извлечение ОТКАЗАНО с тостом-причиной (иначе
  «вставил 1024 → буфер взял 500 → вынул полный 1024» = дюп Ци);
- пустой — «рассыпался в пыль», в инвентарь НЕ возвращается (не дюпается).

## 3. Пруфы

- **Prefix** (checkpoints/logs/09_23_r37c_prefix_charger.log): статический
  слепок ДО правок — charger_window отсутствует во всей input-цепочке,
  ChargerWindow.cs отсутствует, моста QiStoneData→QiStone нет,
  IsBeltEquipped без гейта (73), ChargerInitPhase — стаб; VERDICT: FAIL.
  + скриншот пользователя (VLM): зарядник надет → ряд пояса виден.
- **Postfix** (checkpoints/logs/09_23_r37c_postfix_charger.log): ChargerSim
  (сим №20, GODOT_CHARGERQA_DEBUG=1) — A1/A2 инфраструктура, B1 честный
  отказ с тостом (предмет не потерян), C1-C4 генерация L3+/надевание/
  гейт пояса (IsBeltEquipped=false при заряднике — РАНЬШЕ true)/режим On,
  D1 окно H открыто+контент, E1-E3 вставка (MaxQi=1024 = канон §10),
  F1-F2 домен качает (камень 1024→997, ядро практика 1005→1049 —
  TransferToPractitioner вне боя сливает буфер ядру: BufferQi≈0 НЕ дефект),
  G1-G4 анти-дюп (частичный отказ/полный возврат/пустой рассыпался),
  H1 сейв round-trip MaxQi 1024→1024, I1/I2 снятие→Off, armor_belt→пояс.
  VERDICT: PASS.
- **Визуал**: screenshots/r37c_charger_window.png (Xvfb :99 + opengl3;
  xvfb-run НЕ годится — требует отсутствующий xauth; вручную
  `Xvfb :99` + `DISPLAY=:99` + `DOTNET_ROOT` — та самая ошибка, на которой
  упала прошлая сессия). VLM-вердикт: 3 слота, камень 💎 1024/1024,
  буфер 0/500, тепло 0% (Cool), кнопка режима, подсказка drag&drop,
  пергамент, контраст отличный, критических проблем нет.
- **Регрессия**: 19/19 PASS (полная) + CHARGERQA добавлен в qa_regression.sh
  (сим №20). Build 0 errors.
- Логи: 09_23_r37c_prefix_charger.log, 09_23_r37c_postfix_charger.log,
  09_23_r37c_postfix_charger_xvfb.log — В GIT.

## 4. Доки

- CHARGER_SYSTEM §12 — статус реализации (окно H, мост, контракт анти-дюпа,
  допущения: слот Belt vs доковый «charger», ChargeSpeedBonus — тултип).
- HOTKEYS §2.3 — клавиша H.
- INVENTORY_SYSTEM §7.2 — R37-c-примечание (пояс = Armor, камни — только
  в зарядник).
- TESTING_RULES §0.1 — GODOT_CHARGERQA_DEBUG (сим №20).

## 5. NEXT

1. **R37 (баг-репорт 23.09 №1-№2, хвост упавшей сессии)**: сползание надписей
   чит-панели при нажатии читов + сапоги из инвентаря (броня надевается) —
   QA-сим CheatEquipSimDebug уже написан (GODOT_CHEATEQUIP_DEBUG=1),
   закоммичен как prefix-инфраструктура; внести фиксы, снять вердикт PASS.
2. R37-бэклог из аудита 09.22 22:00: P1-13-hardening (MaxCatchupSeconds
   единицы), NEW-TIME-1 (TickCatchUpClock.Reset на границе мира), NEW-TIME-2
   (автосейв +1 тик: порядок WorldModule/SaveModule); P2-22; пачки
   P2-37…42/47/48/50 → интеграционная контрольная фаза
   «Time→NPC→Cultivation→Techniques→Combat→Save→Time resume».
3. Зарядник: будущие фазы — ChargeSpeedBonus в пайплайн техник (сейчас
   тултип), per-instance частичные камни в инвентаре (сейчас — контракт
   анти-дюпа), форм-факторы по §2.1 (bracelet/necklace/ring/backpack).
