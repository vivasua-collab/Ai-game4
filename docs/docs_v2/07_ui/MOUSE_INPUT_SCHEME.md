# Схема обработки мыши: UI vs Game World

> **Дата:** 2026-08-20
> **Статус:** ACCEPTED
> **Проблема:** LMB над инвентарём двигает персонажа вместо перетаскивания предметов.

---

## 1. Проблема

**Симптом:** При открытом инвентаре нажатие ЛКМ на предмете для перетаскивания
одновременно вызывает перемещение персонажа к точке клика.

**Корневая причина:** `GameWorldController.HandleMouseClick()` использует
`Godot.Input.IsActionJustPressed("mouse_click")` — это **polling API**, который
проверяет сырое состояние InputMap action. Он **не уважает** цепочку потребления
ввода Godot (UI → unhandled).

```
_PhysicsProcess:
  HandleMouseClick():
    if Godot.Input.IsActionJustPressed("mouse_click")  ← POLLING, ignores UI
      _mouseTarget = mouseWorldPos  ← player moves!
```

При этом Godot drag&drop (`_GetDragData` на `InventoryItemRow`) тоже стартует.
Результат: **и перетаскивание, и перемещение** происходят одновременно.

---

## 2. Цепочка ввода Godot 4.7 (официальная)

Godot распространяет ввод в строго определённом порядке:

```
1. Node._input(InputEvent)          ← ВСЕ события (top-level)
2. Control._gui_input(InputEvent)   ← если мышь над Control с MouseFilter.Stop
3. (событие помечается как consumed)
4. Node._unhandled_input(InputEvent) ← если НЕ потреблено UI
5. InputMap action polling           ← Godot.Input.IsActionJustPressed()
                                      (ПОЛНОСТЬЮ игнорирует потребление!)
```

**Ключевое правило:**
- `_gui_input` + `MouseFilter.Stop` = UI потребляет событие
- `_unhandled_input` = получает только НЕпотреблённые события
- `Godot.Input.IsActionJustPressed()` = **обходит** всю цепочку (polling)

---

## 3. Решение

### Принцип: `_unhandled_input` для game world, `MouseFilter.Stop` для UI

**Game world (движение, атака):**
- Использовать `_unhandled_input(InputEvent)` вместо polling в `_PhysicsProcess`
- `_unhandled_input` автоматически НЕ вызывается, если UI потребил событие
- Не нужен `SetOverUI` hack для мыши

**UI (инвентарь, кукла, будущие minimap/quickbar):**
- `MouseFilter = Stop` на всех интерактивных Control
- `MouseFilter = Stop` на background overlay (чтобы клик по фону не шёл в world)
- `_gui_input` / `GuiInput` signal для обработки кликов
- `_GetDragData` / `_CanDropData` / `_DropData` для drag&drop (уже работает)

### Исключения
- **Mouse wheel (zoom):** оставить в `_input` — zoom не конфликтует с UI
- **Keyboard input:** оставить через `InputAdapter` + `SetOverUI` (keyboard не использует Godot propagation так же как mouse)

---

## 4. Матрица MouseFilter

| Элемент | MouseFilter | Причина |
|---------|-------------|---------|
| `InventoryWindow` (root) | `Stop` | Потребляет все клики в пределах окна |
| `InventoryWindow.bg` (background) | `Stop` | Клик по фону закрывает + НЕ идёт в world |
| `InventoryWindow._panel` | `Stop` | Клики на панели не идут в world |
| `InventoryItemRow` | `Stop` | Клик на предмет = drag start, не move |
| `CharacterDollPanel` | `Stop` | Клик на куклу = interact, не move |
| `DollSlotRow` | `Stop` | Клик на слот = unequip, не move |
| `DollSlotRow._itemLabel` | `Stop` | Клик на label внутри row |
| `GameWorldController` | N/A (Node2D) | Использует `_unhandled_input` |

### Правило для будущих UI
- **Интерактивный UI** → `MouseFilter = Stop` (всегда)
- **Декоративный UI** (label без кликов) → `MouseFilter = Ignore`
- **Overlay background** → `MouseFilter = Stop` (не `Pass`!)

---

## 5. Изменения в коде

### GameWorldController
- ❌ Убрать `HandleMouseClick()` из `_PhysicsProcess`
- ✅ Добавить `_unhandled_input(InputEvent)` override
- Логика: `if LMB pressed → set _mouseTarget`

### InventoryWindow
- `bg.MouseFilter`: `Pass` → `Stop` (клик по фону закрывает + не идёт в world)

### InputAdapter
- `SetOverUI` — оставить для keyboard input suppression (movement keys)
- `attack` action — можно убрать `_isOverUI` check (т.к. attack будет через `_unhandled_input`), но пока оставить для безопасности

---

## 6. Проверка после фикса

| Сценарий | Ожидание |
|----------|----------|
| LMB на мире (инвентарь закрыт) | Игрок идёт к точке ✓ |
| LMB на предмете в инвентаре | Начинается drag, игрок НЕ идёт ✓ |
| LMB на слоте куклы | Unequip, игрок НЕ идёт ✓ |
| LMB на фоне инвентаря | Закрытие инвентаря, игрок НЕ идёт ✓ |
| LMB на мире (инвентарь открыт, клик мимо окна) | Инвентарь закрывается, игрок НЕ идёт ✓ |
| Mouse wheel (zoom) | Zoom работает всегда (в `_input`) ✓ |
| WASD движение | Работает, `SetOverUI` подавляет когда инвентарь открыт ✓ |

---

## 7. Будущие расширения

Эта схема автоматически работает для:
- **Minimap** — `MouseFilter.Stop` на минимапе, клики не идут в world
- **Quick-access bar** — `MouseFilter.Stop` на слотах
- **Trade window** — `MouseFilter.Stop` на окне
- **Dialogue window** — `MouseFilter.Stop` на окне
- **Combat target selection** — через `_unhandled_input`, не конфликтует с UI

**Не нужно** добавлять `SetOverUI` вызовы для каждого нового UI элемента — Godot propagation обрабатывает это автоматически.
