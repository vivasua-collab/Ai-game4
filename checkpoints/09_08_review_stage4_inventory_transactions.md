# Этап 4 — Инвентарь, предметы на земле, крафт, хранилища

> Статус: ✅ **ВЫПОЛНЕН** (валидация: 6/6 подтверждены; фиксы + QA + регрессия PASS).

## Вердикты и фиксы

| # | Находка | Вердикт | Фикс |
|---|---|---|---|
| P0-1 | SpiritStorage.TryRetrieve удаляет предмет, не возвращая его | ✅ Подтверждено | Инжект IItemDatabaseService; резолв ДО мутации (unknown → false, ничего не удалено); `item = resolved` (реальный предмет). StorageRing.TryRetrieve — тот же паттерн |
| P1-2 | Нет stacking, кэш портится | ✅ Подтверждено | TryStore ВСЕГДА ищет неполный стек (не только при заполненности); кэш = сумма по всем слотам (RecalcCountCache) |
| P1-3 | StorageRing не списывает Ци | ✅ Подтверждено | Инжект IQiService; HasQiFor ДО мутации, ChargeQi ПОСЛЕ успешного изменения (транзакция); отказ при нехватке — содержимое не меняется |
| P1-4 | Крафт при полном инвентаре теряет результат | ✅ Подтверждено | OnCraftCompleted: TryAddItem(out addedCount) + overflow → DropItemsNearPlayer (как pickup); CraftingService: результат валидируется в ItemDatabase ДО расхода ингредиентов |
| P1-5 | Pickup unknown itemId теряет дроп | ✅ Подтверждено | Валидация в TryPickupNearest ДО удаления (инжект IItemDatabaseService) — предмет остаётся на земле с причиной в логе |
| P2-6 | Граничная дистанция `<` | ✅ Подтверждено | `distSq <= nearestDistSq` — предмет ровно на maxDistance подбирается |

## QA

- **НОВЫЙ хук `GODOT_STORAGE_DEBUG=1`** (StorageSimDebug, хук №21 в TESTING_RULES §0.1): spirit store×3 → 1 слот/стек 3; retrieve → реальный ItemId; Qi −40 (=4×10); ring store/retrieve списывают Ци (15/7); unknown из кольца не извлекается; craft overflow → ItemDroppedEvent + часть в инвентарь; pickup unknown остаётся на земле; граничная дистанция подбирается. VERDICT: PASS.
- Регрессия: COMBAT (ammo-путь), CHARGE, TRADEUX, GEN (0 искл.), TOAST, KILLFEED, REASSEMBLY, TRADE smoke — все PASS. Build 0 errors (308 warnings, +8 — nullable-инжекты).

## Находки ревью → план валидации и фикса

### P0-1. SpiritStorageService.TryRetrieve удаляет предмет, не возвращая его
**Клейм:** out ItemData инициализируется null!, слот уменьшается, Ци списывается, ItemRemovedEvent публикуется, но item никогда не присваивается → предмет исчезает из хранилища, вызывающий получает null.
**Валидация:** прочитать SpiritStorageService.TryRetrieve полностью; проверить контракт ISpiritStorageService.
**Фикс:**
- Инжектировать IItemDatabaseService в SpiritStorageService.
- До изменения слота разрешить itemId → ItemData; если записи нет — ничего не удалять, вернуть false.
- `item = resolved` (реальный предмет), затем удаление/уменьшение слота.
- QA: store → retrieve → ItemId совпадает, количество уменьшилось, потерь нет.

### P1-2. Нет нормального stacking; кэш количества портится
**Клейм:** TryStore при наличии свободного слота всегда создаёт новый InventorySlot с количеством 1; поиск существующего стека — только в ветке «хранилище заполнено»; кэш задаётся как newCount одного стека, не сумма.
**Валидация:** прочитать TryStore ветвление, _itemCountCache.
**Фикс:**
- Всегда сначала искать неполный stack того же ItemId.
- Новый слот — только при отсутствии подходящего стека.
- Кэш = суммарное количество по всем слотам (единая функция пересчёта RecalcCount).
- QA: store N одинаковых stackable → UsedSlots = 1, кэш = N.

### P1-3. StorageRingService считает стоимость Ци, но не списывает
**Клейм:** TryStore/TryRetrieve вычисляют qiCost, но IQiService нет, Ци не проверяется и не списывается.
**Валидация:** прочитать StorageRingService, IStorageRingService (контракт Qi cost).
**Фикс:**
- Инжектировать IQiService (или IQiDataProvider + запрос списания).
- Проверять баланс до изменения слота; списывать только при успехе; изменение слота — после успешного расхода.
- При нехватке Ци → false + причина (тост), содержимое кольца не меняется.

### P1-4. Крафт при полном инвентаре уничтожает материалы
**Клейм:** CraftingService.TryCraft удаляет ингредиенты → CraftCompletedEvent → true; InventoryModule.OnCraftCompleted зовёт TryAddItem, игнорируя результат; overflow НЕ выбрасывается на землю (в отличие от pickup).
**Валидация:** прочитать CraftingService.TryCraft, InventoryModule.OnCraftCompleted, сравнить с pickup-overflow путём.
**Фикс (минимальная защита, как в pickup):**
- В OnCraftCompleted: при частичном/полном отказе TryAddItem выбрасывать остаток через DropItemsNearPlayer (как pickup overflow).
- CraftCompletedEvent публиковать только после фактической выдачи (или расширить событие признаком ground-drop).

### P1-5. Pickup unknown itemId удаляет ground drop без восстановления
**Клейм:** GroundItemService.TryPickupNearest удаляет из _items + renderer, потом публикует ItemAddRequestEvent; InventoryModule при отсутствии itemId в ItemDatabase только логает → предмет исчезает.
**Валидация:** прочитать TryPickupNearest, OnItemAddRequest.
**Фикс:**
- Валидировать itemId через IItemDatabaseService ДО удаления ground item (в GroundItemService или InventoryModule возвращает результат).
- Двухфазный pickup: удалить drop только по фактически добавленному количеству; при невозможности — предмет остаётся на земле + причина в журнале.

### P2-6. Граничная дистанция pickup: строгое `<`
**Клейм:** `distSq < nearestDistSq` — предмет ровно на maxDistance не подбирается, хотя интерфейс определяет радиус как максимальную допустимую.
**Валидация:** прочитать цикл поиска ближайшего.
**Фикс:** `distSq <= nearestDistSq`.

## QA-план

- Новые хуки: `GODOT_SPIRITSTORAGE_DEBUG` (store/retrieve/stacking/кэш), `GODOT_RINGSTORAGE_DEBUG` (Ци списывается, отказ при нехватке), craft-overflow (полный инвентарь → материалы не теряются, результат на земле), pickup-unknown (дроп остаётся).
- Регрессия TRADE/GEN + полная.

## Коммит
`fix(review-4): inventory transactions — SpiritStorage, ring Qi, craft overflow, pickup restore` + push.
