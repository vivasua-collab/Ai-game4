# Чекпоинт: Ground item system — overflow drop, trash zone, pickup

**Дата:** 2026-08-21 13:00 UTC
**Сессия:** web-d86b1055
**Тип:** feature

---

## Контекст

Пользователь запросил:
- При превышении ОБЪЁМА ресурсы должны выпадать на землю
- Корзина в инвентаре для выбрасывания предметов
- Подбор выпавших предметов с земли
- Спрайты для предметов на земле

## Реализация

### 1. Контракты (`GroundItemContracts.cs`)
- `ItemDroppedEvent` — предмет выпал (dropId, itemId, count, worldX, worldY)
- `ItemPickedUpEvent` — предмет подобран (dropId, itemId, count)

### 2. IGroundItemService + GroundItemService
- `DropItem(itemId, count, x, y)` → создаёт ground item, публикует ItemDroppedEvent
- `TryPickupNearest(x, y, maxDistance)` → находит ближайший, публикует ItemPickedUpEvent + ItemAddRequestEvent
- `GetAllGroundItems()` — для рендерера
- Хранит `List<GroundItem>`, уникальные dropId (инкрементальный)

### 3. GroundItemRenderer (270 LOC)
- Подписывается на ItemDroppedEvent / ItemPickedUpEvent через EventBus
- Создаёт/удаляет Sprite2D для каждого ground item
- **Процедурные текстуры 16×16** по категориям:
  - Weapon: меч (вертикальная линия + гарда)
  - Armor: щит (прямоугольник)
  - Accessory: кольцо (окружность)
  - Consumable: зелье (бутылка)
  - Material: куб
  - Technique: свиток
  - Quest: звезда
  - Misc: круг
- ZIndex = RenderLayer.Objects + 1 (выше объектов окружения)
- Scale 0.5 (16×16 → 8×8 на земле)

### 4. InventoryService.TryAddItem — новая сигнатура
```csharp
bool TryAddItem(ItemData item, int count, out int addedCount)
```
- Возвращает сколько реально добавлено (partial add при полном объёме)
- Caller вычисляет overflow = requested - addedCount

### 5. Overflow handling (InventoryModule.OnItemAddRequest)
- TryAddItem with out addedCount
- If overflow > 0 → `DropItemsNearPlayer(itemId, overflow)`
- DropItemsNearPlayer: tile→pixel conversion, random offset ±15px, GroundItemService.DropItem

### 6. TrashDropZone (корзина в инвентаре)
- Panel с 🗑 иконкой + "Выбросить" label
- MouseFilter.Stop, _CanDropData принимает source="inventory"
- _DropData → `InventoryWindow.DropItemOnGround(itemId)`
- DropItemOnGround: GetItemCount → TryRemoveItem → DropItem near player

### 7. GameWorldController.HandlePickup (E key)
- `TryPickupNearest(playerPixelPos, 1.5 tiles)`
- Toast: "Подобран предмет" / "Рядом нет предметов (подойди ближе)"
- RefreshExternally после подбора

### 8. DI registration
- `IGroundItemService` registered в `InventoryModuleServices`
- Injected в: GameWorldController (pickup), InventoryWindow (trash drop), InventoryModule (overflow drop), GroundItemRenderer (events)

## Полный цикл

```
Harvest (F key)
  → TryHarvest → ResourceService.Harvest → ItemAddRequestEvent
  → InventoryModule.OnItemAddRequest
  → TryAddItem(item, count, out addedCount)
  → if overflow > 0: DropItemsNearPlayer(itemId, overflow)
  → GroundItemService.DropItem → ItemDroppedEvent
  → GroundItemRenderer creates Sprite2D

Player walks near ground item
  → E key → HandlePickup
  → GroundItemService.TryPickupNearest
  → ItemPickedUpEvent (renderer removes sprite)
  → ItemAddRequestEvent (inventory adds, may overflow again)

Player drags item to 🗑 basket
  → TrashDropZone._DropData
  → InventoryWindow.DropItemOnGround(itemId)
  → TryRemoveItem → GroundItemService.DropItem
  → ItemDroppedEvent → sprite created
```

## Верификация

- `dotnet build`: 0 errors
- Headless: `[GroundItemRenderer] Ready`, `[Inventory] Test items seeded`
- Все компоненты загружаются корректно

## Файлы

**Созданные:**
- `game/src/Core/Messaging/Contracts/GroundItemContracts.cs` — ItemDroppedEvent + ItemPickedUpEvent
- `game/src/Core/Interfaces/IGroundItemService.cs` — interface + GroundItem struct
- `game/src/Modules/Inventory/GroundItemService.cs` — implementation
- `game/src/Adapter/Scene/GroundItemRenderer.cs` — procedural sprites + event subscription
- `checkpoints/08_21_ground_items.md` — этот чекпоинт

**Изменённые:**
- `game/src/Modules/Inventory/InventoryModuleServices.cs` — +IGroundItemService registration
- `game/src/Modules/Inventory/InventoryModule.cs` — +IPlayerService, +IGroundItemService inject, overflow drop
- `game/src/Modules/Inventory/InventoryService.cs` — TryAddItem with out addedCount
- `game/src/Adapter/Scene/SceneBuilder.cs` — +SetupGroundItems
- `game/src/Adapter/Scene/GameWorldController.cs` — +IGroundItemService inject, +HandlePickup (E key)
- `game/src/Adapter/UI/InventoryWindow.cs` — +IGroundItemService/IPlayerService inject, +DropItemOnGround, +TrashDropZone class
- `worklog.md` — запись 13:00
