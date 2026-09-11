# АУДИТ: Inventory

**Дата:** 2026-09-11. **HEAD:** `d0b065d`. **Режим:** READ-ONLY (код не изменялся).
**Scope:** Modules/Inventory целиком (15 файлов + Data/×2, ~3.4k строк) + проводка:
Core/Data (ItemData, EquipmentData, QiStoneData, Structs.InventorySlot, Enums), контракты
(Inventory/GroundItem/Belt/Crafting), TradeService, CorpseService, UI (InventoryWindow,
CharacterDollPanel, ItemContextMenu, LootWindow), Save-стык (SaveModule/Aggregator/
GameSession), Combat-стык (EquipmentDataProvider→DamageService), TileService (harvest),
EquipmentGenerator/WeaponVisualCatalog (R15), docs_v2 (06_player, 05_data).

---

## Сводка

- **Находок: 16 — 5×P1, 4×P2, 7×P3.** Ключевой паттерн: overflow-компенсация
  (3-arg TryAddItem + DropItemsNearPlayer) есть ТОЛЬКО в OnItemAddRequest/
  OnCraftCompleted — три других пути теряют предметы при volume-full (INV-2/3/17).
- **R10 SlotId (TOCTOU): работает во всех UI-путях** (drop/split/выброс/лут —
  Guid+expectedItemId, stale → отказ без деструктивного фолбэка). Equip адресован
  по ItemId — безопасно: ItemId генератора уникален per-process
  (EquipmentGenerator.cs:348-352, Interlocked counter).
- **R11:** инвентарь round-trip честный, но кукла/пояс/кольца/дух.хранилище/ground
  **не в сейве и не сбрасываются при LoadGame/NewGame** → дюп-окно (INV-4).
- **R15 WeaponClassId + fallback:** подтверждён (WeaponVisualCatalog:88-114).

---

## Находки

### INV-1 [P1] Кэш itemCount перезаписывается счётом ОДНОЙ кучки
**Файл:** `InventoryService.cs:171-172` — `_itemCountCache[item.ItemId] = newCount;`
(присваивание вместо `+= count`; соседняя ветка 146-148 корректно инкрементирует).
**Сценарий:** ≥2 кучки (сплит R10 или переполнение MaxStack): A=50 (полный), B=30,
кэш=80 → `TryAddItem(20)` → B=50, кэш=50, реально 100.
**Последствия:** неверные транзакции у всех читателей GetItemCount — крафт недоступен
(CraftingRecipe.cs:49), продажа урезана (TradeService.cs:316), перенос в пояс меньше
(BeltService.cs:106), «Нет камня» при наличии (InventoryWindow.cs:282). Дюпа нет.
**Фикс:** инкремент `cached + count` либо единый RecalcTotal (как TryRemoveFromSlot:349-359).

### INV-2 [P1] Unequip/замена экипировки при volume-full = тихая потеря
**Файл:** `InventoryModule.cs:137-141` (OnEquipmentChanged → `TryAddItem(oldItem, 1)`
2-arg, без overflow-дропа → InventoryService.cs:112-117 → false → предмет исчез).
**Сценарий:** инвентарь полон по объёму → снятие шлема (клик по кукле/замена/двуручник
EQ-A1) → OldItemId возвращается событием → TryAddItem false → потеря без лога.
Комментарий CharacterDollPanel.cs:281-282 («TryAddItem drops surplus on the ground») — ложь.
**Фикс:** 3-arg + DropItemsNearPlayer (паттерн OnItemAddRequest:96-102).

### INV-3 [P1] Возврат из пояса при почти полном инвентаре = потеря части стака
**Файл:** `BeltService.cs:128-143` — `TryAddItem(item, slot.Count)` возвращает true
при ЧАСТИЧНОМ добавлении (InventoryService.cs:118-123), а слот пояса очищается
ЦЕЛИКОМ (строки 140-141) → (slot.Count − canFit) предметов испаряются.
**Сценарий:** пояс 10 пилюль, объёма на 3 → 7 потеряны (снятие пояса = мульти-потеря).
**Фикс:** 3-arg: `added == slot.Count` → очистка; иначе `slot.Count -= added`.

### INV-4 [P1] Save/Load домена Inventory: кукла/пояс/кольца/дух.хранилище/ground — мимо
**Файлы:** `InventoryModuleServices.cs:18-36` (ISaveable только InventoryService);
`GameSession.cs:143` (при LoadGame сбрасывается только NPC-домен); EquipmentService/
BeltService/SpiritStorage/StorageRing/GroundItemService — ни ISaveable, ни Reset.
**Сценарии (тёплый LoadGame/NewGame):** (1) **дюп:** сессия A — надет меч (не в сейве);
LoadGame сейва B (меч в инвентаре) → `_equipment` не очищен → меч A надет + меч B
в рюкзаке → снимаем → 2 меча; аналогично пояс/кольца/дух.хранилище; (2) **фантомный
ground-лут:** `_items`+`_nextDropId` переживают пересборку — E-подбор предметов
прошлого мира (GROUND_ITEM_SYSTEM §6 «исчезают при перезагрузке» — не так);
(3) R11-вопрос «пояс/кукла восстанавливаются?» — НЕТ (сейв = 8 блоков, без equipment).
**Doc:** SAVE_SYSTEM.md §4.4:215 обещает `"equipment"` — не реализовано.
**Фикс:** ISaveable для Equipment/Belt (+storage при желании) ИЛИ InventoryModule.ResetWorld()
в GameSession.LoadGame/NewGame до RestoreState; сброс ground-обязателен.

### INV-5 [P1, стык Inventory→Combat] SetEquipmentData: raw-статы + coverage=0 → броня игрока не работает
**Файл:** `EquipmentDataProvider.cs:304-326`; проявление `DamageService.cs:267-301`.
[UNVERIFIED-runtime — статический анализ проводки, боевой сим не запускался]
(1) `totalArmor += kvp.Value.Defense` (321) — без gradeMultiplier (0.5…2.0) и Coverage,
тогда как UI/EquipmentChangedEvent получают агрегаторные значения (расхождение до 2×);
(2) coverage не заполняется → DamageService:292-301 `armorCoversHit=false` →
**DefenseProcessor (слой 6-8) пропускается — Defense игрока не применяется вовсе**;
(3) `SetArmorCoverage` — 0 вызовов глобально → coverage-механика C5 мертва для всех
сущностей (похоже на регрессию P2-6.2 FIX: default 100→0 без компенсирующего вызова).
**Фикс:** пересчёт в SetEquipmentData через EquipmentStatAggregator (armor+coverage) +
явный SetArmorCoverage для NPC в NPCSpawnerService:179/NPCService:597 (совместно с Combat).

### INV-6 [P2] BeltService.Use не гейтит надетый пояс
`BeltService.cs:150-155` — контракт класса (строка 33: «Без пояса Assign/Use → false»)
нарушен: Use проверяет только слот. Застрявшие после INV-3 расходники используются
без пояса (хотбар 3-9, GameWorldController.cs:1762-1765). Фикс: `if (!IsBeltEquipped) return false;`.

### INV-7 [P2] SpiritStorage/StorageRing недоступны игроку (нет UI)
Вызовы TryStore/TryRetrieve — только QA-сим `StorageSimDebug.cs`; в Adapter/UI —
0 проводок. InventoryConfig.SpiritStorageCapacity/RingStorageCapacity (23-26) —
мёртвые настройки. Заявленные механики недостижимы; содержимое к тому же не в сейве (INV-4).

### INV-8 [P2] GroundItemService: нет TTL и нет сброса при пересборке мира
Нет деспавна вообще (у трупов TTL есть: NPCModule.cs:145); при NewGame/LoadGame не
сбрасывается (см. INV-4). Мелочи: offset через `Random(DateTime.UtcNow.Ticks)`
(InventoryModule.cs:124, InventoryWindow.cs:464) — док §2.2 обещает SeededRandom;
подбор напрямую из GameWorldController (док §3.2 — через InteractionService).

### INV-9 [P2] Doc-drift INVENTORY_SYSTEM.md
§9.1:386-390 описывает `slot_index` + легаси-фолбэк «выбросить все слоты» — это
УДАЛЁННОЕ поведение R10 (код: slot_id Guid, без — отказ, TrashDropZone:995-1005);
§9.1:377 TrySplitSlot(slotIndex) — Guid-перегрузка; §4.1 «заглушки не отображаются
и не принимают» — Amulet/RingLeft1/Hands/Back отображаются (CharacterDollPanel:55-61)
и принимают предметы (кольца-хранилища активируются через RingLeft1!);
§7.1/§12 «пояс 0-4, клавиши 1-4» — код 7 слотов, хотбар 3-9; §3.3/§16.4 maxVolume 30 —
код 100; §16.1 weightReduction в весе — GetCurrentWeight без редукции (0 вызовов
GetEffectiveWeight).

### INV-10 [P3] SpiritStorageService: мёртвый кэш + неконсистентный Remove
`SpiritStorageService.cs:131-135` — при опустошении кучки `_itemCountCache.Remove(itemId)`
без пересчёта (другие кучки того же item теряют счёт); кэш никем не читается —
мёртвый код. Удалить или пересчитывать всегда.

### INV-11 [P3] EquipmentDataProvider: комментарий-обещание «автопересчёта»
Строки 25-27 обещают пересчёт armor/damage «если кэш не задан» — код (101-116)
просто читает кэш. Вводит в заблуждение (усиливает INV-5).

### INV-12 [P3] Легаси DropItemOnGround («ВСЕ слоты предмета») жив
`InventoryWindow.cs:484-504` — публичный деструктивный метод, вызовов из UI нет
(только комментарии/QA). Кандидат на удаление.

### INV-13 [P3] RestoreState: SlotId не персистится, count не валидируется
`InventoryService.cs:561-587` — новые Guid после Load (fail-safe: stale-окна отклонят);
count без сверки с MaxStack (сейву доверяем).

### INV-14 [P3] NestingFlag-запреты доков не соблюдаются (латентно)
INVENTORY_SYSTEM §6.5: «кольцо→дух.хранилище / кольцо→кольцо запрещено». Код:
SpiritStorage.TryStore флаг НЕ проверяет; StorageRing.TryStore:107-108 запрещает
только None/Spirit — кольцо с дефолтным Any (ItemData.cs:87) пройдёт в кольцо.
Не эксплуатируется (UI нет — INV-7), всплывёт при появлении UI.

### INV-15 [P3] EquipmentValidator — заглушки требований
`EquipmentValidator.cs:79-90` — RequiredCultivationLevel/StatRequirements всегда
проходят (помечено «будущие фазы»; INVENTORY_SYSTEM §4.2-6 обещает проверки).
OK-BY-DESIGN с доказательством в коде.

### INV-16 [P3] TradeService.TryBuy игнорирует addedCount
`TradeService.cs:283-289` — камни списаны за полный buyCount; 2-arg TryAddItem при
частичном добавлении вернёт true без компенсации. HowManyCanFit↔TryAddItem
математически сходятся (Floor одного remainingVolume) — расхождение только при
float-дрейфе. Теоретическое; рекомендация 3-arg + сверка.

### INV-17 [P1] Harvest (F) при полном инвентаре = потеря ресурса
**Файлы:** `InventoryModule.cs:75-87`; источник `TileService.cs:60-101`.
**Сценарий:** TileService.TryHarvest ИСТОЩАЕТ тайл (строки 81-91; при 0 —
IsHarvestable=false, объект удалён, respawn 7 игровых дней) → ResourceHarvestedEvent →
OnResourceHarvested → `TryAddItem(itemData, e.Amount)` 2-arg БЕЗ overflow-дропа →
false → ресурс исчез (тайл истощён, предмета нет, на землю не упал). Заявка
пользователя «harvest → ItemAddRequest → overflow → DropItemsNearPlayer» — фактический
путь harvest идёт через ResourceHarvestedEvent и компенсации не имеет.
**Фикс:** 3-arg + DropItemsNearPlayer(e.ItemId, e.Amount − added).

---

## Проверено чисто

- **R10 SlotId:** DropSlotOnGround:416-453, TrySplitSlotForDialog:609-631,
  TrashDropZone:987-1006 (без slot_id — ОТКАЗ), ItemContextMenu/SplitStackDialog
  (действия по Guid; moveCount вне нового Count → отказ), CorpseService.TryTakeItem:119-151
  + LootWindow.HandleTake:342-349 (адресный лут), InventorySlot identity-preserving
  (Structs.cs:218-282, WithCount; Equals без SlotId).
- **Стаки/дюпы:** MaxStack во всех ветках TryAddItem; сплит «оба ≥1»; дюпов при
  backpack↔belt↔corpse↔trade↔ground не найдено — remove-после-проверки или rollback
  (TradeService.TryBuy:277-289 Spend→Add→откат; HandleDropOnSlot:230-261 remove→equip→
  rollback TryAddItem — объём освобождён тем же предметом).
- **GroundItemService.TryPickupNearest:59-104:** валидация itemId ДО RemoveAt
  (P1-5 fix); двойной pickup невозможен (синхронный); overflow подбора → предмет
  перепрыгивает к игроку, не теряется.
- **CraftingService.TryCraft:84-134:** атомарность при согласованном кэше (между
  HasIngredients и расходом мутаций нет); результат валидируется до списания (P1-4);
  overflow результата → на землю (InventoryModule:150-160).
- **StorageRingService:** Qi-транзакции «баланс до мутации, списание после» (P1-3:
  125-129/211-216); long-арифметика; резолв ItemData до удаления (P0-1: 199-203);
  реактивация сохраняет содержимое.
- **EquipmentService:** off-hand при двуручнике — 5-arg событие (EQ-A1:131-134);
  ампутация → BlockedSlots + автоснятие + SyncToProvider (EQ-A2:227-250); события
  парные с OldItemId; двойного применения статов нет (агрегатор on-the-fly).
- **EquipmentStatAggregator:** (base+flat)×(1+pct), flat/pct-разделение с "_pct" —
  чисто; Permil-конвертации провайдера чисты.
- **QiStone RMB:** TryUseQiStone:276-337 — списание 1 шт → AddQi(long) → chaotic
  10% / −10% MaxHP; стакинг по размеру корректен.
- **BackpackService:** парная подписка, кэш Back-слота из события.
- **CorpseService:** честный full-loot (снапшот экипировки+инвентаря+камней), дедуп
  смертей, фильтр фантомных ID, TTL 1440 с, ResetWorld (R14).
- **R15:** EquipmentData.WeaponClassId (31-38) + генератор (EquipmentGenerator.cs:93)
  + fallback WeaponVisualCatalog.WeaponClassOf:88-114 (eq_wep_ → generic sword).
- **R11 инвентарь:** InventorySaveData (поля, IncludeFields), RestoreState пересобирает
  кэш суммы корректно.
- **TradeService.TrySell:** remove→камни; при fail снятия — отказ без списания.

---

## Соответствие docs_v2

| Док | Положение | Код | Вердикт |
|-----|-----------|-----|---------|
| INVENTORY_SYSTEM §9.1 | slot_index + легаси-фолбэк «все слоты» | slot_id (Guid), без — отказ | ❌ drift (INV-9) |
| INVENTORY_SYSTEM §4.1 | 15 слотов, заглушки не отображаются | 11 в UI (7+4), скрытые 4 принимают | ❌ drift |
| INVENTORY_SYSTEM §7.1/§12 | пояс 0-4, клавиши 1-4 | 7 слотов, хотбар 3-9 | ❌ drift (HOTKEYS §8 — канон) |
| INVENTORY_SYSTEM §3.3/§16.4 | maxVolume 30 | 100 | ❌ drift |
| INVENTORY_SYSTEM §16.1 | weightReduction в весе | GetCurrentWeight без редукции | ❌ drift |
| SAVE_SYSTEM §4.4 | "equipment" в сейве | не реализовано | ❌ обещание без кода (INV-4) |
| GROUND_ITEM_SYSTEM §6 | вне сейва осознанно | так, НО «исчезают при перезагрузке» — не сбрасываются | ⚠️ частично |
| GROUND_ITEM_SYSTEM §2.2/§3.2 | SeededRandom offset; InteractionService | Random(Ticks); прямой вызов | ❌/⚠️ P3 |
| INVENTORY_SYSTEM §9 (шапка) | ПКМ-меню/сплит/слот-выброс | реализовано, совпадает | ✅ |
| EQUIPMENT_SYSTEM / R15 | WeaponClassId, 7 классов, fallback | генератор+каталог+fallback | ✅ |

---

## Приоритет фиксов (рекомендация)

1. **INV-2 + INV-17 + INV-3** — один паттерн: 3-arg TryAddItem + DropItemsNearPlayer
   в трёх точках (потери предметов игроком).
2. **INV-4** — ISaveable для Equipment/Belt + InventoryModule.ResetWorld() при
   LoadGame/NewGame (закрывает дюп-окно).
3. **INV-1** — одна строка кэша (инвалидация craft/sell/use/belt).
4. **INV-5** — консистентность SetEquipmentData с агрегатором + coverage
   (совместно с владельцем Combat-контура).
5. INV-6…INV-9 — по ходу; синхронизировать INVENTORY_SYSTEM §4.1/§7/§9.1/§12 с кодом R10/R15.
