# Чекпоинт: Fix LMB over UI — mouse input scheme

**Дата:** 2026-08-20 08:00 UTC
**Сессия:** web-d86b1055 (продолжение, модель не сменена)
**Тип:** fix + design

---

## Контекст

Из локального тестирования: при открытом инвентаре нажатие ЛКМ на предмете для перетаскивания одновременно вызывает перемещение персонажа к точке клика. Нужна корректная схема обработки мыши для UI vs game world, с учётом будущих элементов (minimap, quickbar, trade window, dialogue).

## Анализ

### Корневая причина
`GameWorldController.HandleMouseClick()` использовал `Godot.Input.IsActionJustPressed("mouse_click")` — polling API, который **обходит** цепочку потребления ввода Godot.

### Цепочка ввода Godot 4.7
```
1. Node._input(InputEvent)          ← ВСЕ события
2. Control._gui_input(InputEvent)   ← если мышь над Control с MouseFilter.Stop
3. (событие помечается как consumed)
4. Node._unhandled_input(InputEvent) ← если НЕ потреблено UI
5. Godot.Input.IsActionJustPressed() ← polling, ИГНОРИРУЕТ потребление!
```

Проблема: шаг 5 (polling) не уважает шаг 3 (consumed). Поэтому LMB на UI одновременно:
- Стартует drag в `InventoryItemRow._GetDragData` (через _gui_input)
- Двигает персонажа через `HandleMouseClick` (polling)

## Решение

### Принцип: `_unhandled_input` для game world, `MouseFilter.Stop` для UI

**Game world (движение):**
- `_UnhandledInput(InputEvent)` override — вызывается ТОЛЬКО если UI не потребил событие
- Не нужен `SetOverUI` hack для мыши

**UI (инвентарь, кукла, будущие minimap/quickbar):**
- `MouseFilter = Stop` на всех интерактивных Control
- `MouseFilter = Stop` на background overlay (клик по фону = закрыть + не идти в world)
- `_GetDragData` / `_CanDropData` / `_DropData` для drag&drop (уже работало)

### Матрица MouseFilter

| Элемент | MouseFilter | Причина |
|---------|-------------|---------|
| `InventoryWindow` (root) | `Stop` | Потребляет все клики в пределах окна |
| `InventoryWindow.bg` | `Stop` (был `Pass`) | Клик по фону закрывает + НЕ идёт в world |
| `InventoryWindow._panel` | `Stop` | Клики на панели не идут в world |
| `InventoryItemRow` | `Stop` | Клик на предмет = drag start |
| `CharacterDollPanel` | `Stop` | Клик на куклу = interact |
| `DollSlotRow` | `Stop` | Клик на слот = unequip |

## Изменения

### 1. GameWorldController.cs
- ❌ Удалён `HandleMouseClick()` (polling в `_PhysicsProcess`)
- ✅ Добавлен `_UnhandledInput(InputEvent)` override
- Логика: `if LMB pressed → set _mouseTarget` (только если UI не потребил)

### 2. InventoryWindow.cs
- `bg.MouseFilter`: `Pass` → `Stop`
- Клик по фону: закрывает инвентарь + НЕ идёт в world

### 3. cold_start.sh
- Добавлен `export DOTNET_ROOT` для Godot headless (hostfxr detection)

### 4. MOUSE_INPUT_SCHEME.md (новый документ)
`docs/docs_v2/07_ui/MOUSE_INPUT_SCHEME.md` — полная схема:
- Цепочка ввода Godot 4.7
- Матрица MouseFilter
- Правила для будущих UI
- Проверочные сценарии

## Проверочные сценарии

| Сценарий | Ожидание |
|----------|----------|
| LMB на мире (инвентарь закрыт) | Игрок идёт к точке ✓ |
| LMB на предмете в инвентаре | Drag, игрок НЕ идёт ✓ |
| LMB на слоте куклы | Unequip, игрок НЕ идёт ✓ |
| LMB на фоне инвентаря | Закрытие, игрок НЕ идёт ✓ |
| Mouse wheel (zoom) | Zoom работает всегда ✓ |
| WASD движение | Работает, `SetOverUI` подавляет ✓ |

## Дополнительный фикс: DOTNET_ROOT для Godot

При восстановлении окружения обнаружено: Godot .NET не находит `hostfxr` без `DOTNET_ROOT`. Добавлен `export DOTNET_ROOT=/home/z/.dotnet` в `cold_start.sh` шаг 6.

## Следующие шаги

- [ ] Визуальная проверка на ПК с Godot (LMB на предмете = drag, не move)
- [ ] Применить ту же схему к будущим UI: minimap, quickbar, trade, dialogue
- [ ] Рассмотреть перенос `attack` action на `_UnhandledInput` (сейчас использует `SetOverUI` workaround)

## Файлы

**Созданные:**
- `docs/docs_v2/07_ui/MOUSE_INPUT_SCHEME.md` — схема обработки мыши
- `checkpoints/08_20_mouse_input_fix.md` — этот чекпоинт

**Изменённые:**
- `game/src/Adapter/Scene/GameWorldController.cs` — `_UnhandledInput` вместо `HandleMouseClick`
- `game/src/Adapter/UI/InventoryWindow.cs` — `bg.MouseFilter = Stop`
- `cold_start.sh` — `export DOTNET_ROOT` для Godot headless
- `worklog.md` — запись 08:00

**Верификация:**
- `dotnet build`: 0 errors
- Headless: игра загружается, Inventory/Doll/ObjectLayer — OK
- DOTNET_ROOT fix: Godot headless теперь работает после cold start
