# Концепция: Защита слотов + Вложенные контейнеры + Режимы добычи

> **Дата:** 2026-08-19
> **Статус:** CONCEPT (теория, без кода)
> **Назначение:** Анализ трёх вопросов пользователя перед реализацией.

---

## 1. Защита слотов куклы от множественной экипировки

### Принцип: 1 слот = 1 предмет

**Текущее состояние:** `EquipmentService` уже обеспечивает это — `_equipment` это `Dictionary<EquipmentSlot, EquipmentData>`, один ключ = одно значение. При надевании нового предмета на занятый слот:
- Старый предмет возвращается в инвентарь (через `EquipmentChangedEvent.OldItemId`)
- Новый занимает слот

**Гарантии:**
- `TryEquip(slot, item)` атомарен: либо старый возвращается + новый надевается, либо операция отклоняется
- `GetEquipped(slot)` всегда возвращает 0 или 1 предмет
- Невозможно дублировать: предмет удаляется из инвентаря ДО надевания, откат при ошибке

### Исключение: расходники на поясе

**Концепция:** Слот `Belt` может содержать стопку расходников (как quick-access belt per `INVENTORY_SYSTEM.md §7`).

**Два подхода:**

| Подход | Описание | Сложность |
|--------|----------|-----------|
| **A: Belt как equipment** | `Belt` = 1 предмет (пояс как экипировка). Quick-access slots — отдельная панель 1-5, хранит ссылки на предметы в инвентаре. | 🟢 Low — текущая модель |
| **B: Belt как контейнер** | `Belt` = контейнер на N слотов (3-5), каждый слот = стопка расходников. Перетаскивание пилюль на пояс. | 🟡 Medium — нужен `BeltContainer` |

**Рекомендация:** Подход A для v1 (quick-access = ссылки, не копии). Подход B — v2, когда будет контейнерная система (см. §2).

### Проверки на стороне UI

**Doll panel должен:**
- `_CanDropData`: отклонять drop если предмет не EquipmentData (расходники не тащатся на слот)
- `_DropData`: валидация `eq.Slot == targetSlot` (или 1H weapon в любую руку)
- Слот `Belt` принимает только предметы с `Category == Consumable` (когда реализован подход B)

### Реализация защиты (уже есть)

| Проверка | Где | Статус |
|----------|-----|--------|
| 1 слот = 1 предмет | `EquipmentService._equipment` (Dictionary) | ✅ Уже |
| Старый предмет возвращается | `EquipmentService.TryEquip` (lines 107-114) | ✅ Уже |
| 2H оружие снимает off-hand | `EquipmentService.TryEquip` (lines 117-124) | ✅ Уже |
| UI отклоняет non-equipment | `CharacterDollPanel.HandleDropOnSlot` | ✅ Уже |
| Слот заблокирован при ампутации | `EquipmentService._blockedSlots` | ✅ Уже |

**Вывод:** Защита слотов уже реализована на уровне backend. UI дополняет валидацией типа предмета.

---

## 2. Вложенные контейнеры (Baldur's Gate 3 style)

### Что это

В BG3:
- Рюкзак содержит предметы
- Внутри рюкзака может быть мешок (bag)
- Внутри мешка — другие предметы, включая другой мешок
- Произвольная глубина вложенности
- Перетаскивание: предмет → мешок → мешок → ...

### Анализ сложности в Godot

| Компонент | Сложность | Описание |
|-----------|-----------|----------|
| **Data model** | 🟡 Medium | `ItemInstance` → `Container` с вложенными `ItemInstance[]`. Рекурсивная структура. |
| **Drag&drop UI** | 🔴 High | Godot `_GetDragData`/`_CanDropData`/`_DropData` работает на 1 уровень. Вложенный D&D требует ручной передачи drag data между уровнями. |
| **Рендеринг** | 🔴 High | Каждый контейнер = отдельное окно/панель. Вложенные окна = z-order проблемы. BG3 решает модальными окнами (открывает мешок поверх рюкзака). |
| **Поиск предмета** | 🟡 Medium | Рекурсивный поиск по дереву контейнеров. O(N) на каждый lookup, кэш решает. |
| **Save/Load** | 🟡 Medium | Рекурсивная сериализация дерева. JSON с $ref для циклических ссылок. |
| **Weight calculation** | 🟢 Low | Рекурсивная сумма. Уже есть `IInventoryService.GetCurrentWeight()`. |
| **Взаимодействие с куклой** | 🔴 High | Если предмет в мешке в рюкзаке — нельзя надеть напрямую. Нужен "путь" к предмету. |

### Варианты реализации

#### Вариант 1: Полная BG3 модель (рекомендация: отложить)
```
Backpack
├── Sword
├── Herb Pouch (container)
│   ├── Herb_1
│   └── Herb_2
└── Gem Bag (container)
    ├── Ruby
    └── Sapphire
```

- **LOC:** ~800-1200 (data model + UI + drag&drop + save)
- **Время:** 2-3 недели
- **Риски:** производительность при глубокой вложенности, UX сложность, edge cases (переместить мешок в себя)
- **Когда:** после v1 release, когда базовый инвентарь стабилен

#### Вариант 2: Storage Ring как "вложенный контейнер" (уже есть)
```
Inventory (line model)
└── Storage Ring (equipped on doll)
    ├── Item_1
    ├── Item_2
    └── Item_3
```

- **LOC:** ~0 (уже реализовано: `StorageRingService`, `IStorageService`)
- **Статус:** backend готов, UI не подключён
- **Ограничение:** только 1 уровень вложенности, кольцо экипировано на кукле

#### Вариант 3: Spirit Storage как "параллельный контейнер" (уже есть)
```
Inventory (line model)
Spirit Storage (parallel, unlimited slots, Qi cost per access)
```

- **LOC:** ~0 (уже реализовано: `StorageService` с `StorageType.Spirit`)
- **Статус:** backend готов, UI не подключён

### Рекомендация

**Реалистичность в Godot:** 🟡 Medium-hard. Технически возможно, но значительные усилия для UX.

**Решение:** НЕ реализовывать полную BG3 модель в v1. Вместо этого:
1. ✅ Использовать существующий **Storage Ring** (1 уровень вложенности) — backend готов
2. ✅ Использовать существующий **Spirit Storage** (параллельный контейнер) — backend готов
3. ⏳ Полная BG3 модель — отложить до v2, когда будет стабильный v1 release

**Причина:** BG3 — AAA игра с командой UX-дизайнеров. Для инди-проекта достаточно 2 контейнера (рюкзак + кольцо/дух). Если позже появится потребность — реализовать Вариант 1.

---

## 3. Окружение: режимы добычи и уничтожения

### Режим A: Постепенная добыча (Gradual Depletion)

**Описание:** Каждый Harvest даёт N% от ResourceMax. Объект исчезает, когда ResourceAmount ≤ 0.

```
Tree_Oak (ResourceMax=50)
├── Harvest 1: +5 wood (remaining=45)
├── Harvest 2: +5 wood (remaining=40)
├── ...
├── Harvest 10: +5 wood (remaining=0)
└── DEPLETED → ObjectRemoved, schedule respawn (7 days)
```

**Существующая инфраструктура:** ~85% готово
- ✅ `ResourceService.Harvest` (10% per harvest)
- ✅ `ResourceHarvestedEvent` → `InventoryModule.OnResourceHarvested` → `TryAddItem`
- ✅ `ResourceDepletedEvent` + `RegisterDepletedResource` (7-day respawn)
- ✅ `ObjectDefaults` table (11 ObjectTypes с ResourceMax, HarvestAmount, ItemId)

**Что нужно:** ~100 LOC
- Исправить `TileService.Generate` (использовать ObjectDefaults вместо хардкода)
- Исправить double-publish баг
- Добавить `IsHarvestPressed` в `IPlayerInputService`
- Wire F-key в `GameWorldController`
- `ObjectLayerRenderer` (процедурные спрайты)

**Сложность:** 🟢 Low

### Режим B: Порог урона (Harvest-Damage Threshold)

**Описание:** Объект имеет HP. Каждый удар (F key или оружие) наносит damage. При HP ≤ 0 — объект уничтожен, ВСЕ ресурсы выдаются одним паком.

```
Rock_Medium (HP=100, ResourceMax=40 stone)
├── Hit 1: -34 HP (HP=66)
├── Hit 2: -34 HP (HP=32)
├── Hit 3: -34 HP (HP=-2) → DESTROYED
└── GRANT +40 stone (one pack)
```

**Существующая инфраструктура:** ~20% готово
- ✅ `GameTile.DestructibleHP` / `DestructibleMaxHP` / `HardnessTier` поля
- ✅ `ObjectDefaults` HP значения (Tree=100, Rock=100, Ore=150)
- ❌ Метод нанесения урона — не существует
- ❌ Tool data model (какой предмет = кирка/топор, какой тир)
- ❌ `ObjectDestroyedEvent` контракт
- ❌ Single-pack grant логика
- ❌ HardnessTier проверка против инструмента

**Что нужно:** ~300-400 LOC
- `DamageObject(x, y, float damage, HardnessTier toolTier)` метод
- `ObjectDestroyedEvent` контракт
- Tool data model (ToolType enum, ItemData.ToolTier)
- Wire F-key: если есть инструмент → DamageObject; иначе → Harvest (Mode A fallback)
- Визуальный фидбек (HP bar, трещины на камне)

**Сложность:** 🟡 Medium

### Сравнение

| Аспект | Mode A (Gradual) | Mode B (Threshold) |
|--------|------------------|---------------------|
| Существующая база | 85% | 20% |
| LOC до работающей версии | ~100 | ~350 |
| UX интуитивность | 🟢 Просто (F = собрать) | 🟡 Нужно понимать тир инструмента |
| Реализм | 🟡 Постепенная добыча | 🟢 Реалистично (рубить дерево) |
| Визуальный фидбек | 🟢 Просто (объект исчез) | 🟡 Нужно HP bar / стадии повреждения |
| Tool requirement | ❌ Нет (кулаки OK) | ✅ Да (HardnessTier check) |
| Респаун | ✅ 7 дней | ✅ 7 дней (тот же механизм) |
| Подходит для | Кусты, травы, ягоды | Деревья, камни, руда |

### Рекомендация: гибрид

**V1 (сейчас):** Mode A для всех объектов. F key = собрать. Backend 85% готов, ~100 LOC.

**V2 (позже):** Добавить Mode B для деревьев/камней/руды (объекты с `DestructibleHP > 0`):
- Если есть инструмент нужного тира → Mode B (рубить/добывать)
- Если инструмента нет → Mode A fallback (кулаки, штраф -90% к количеству)
- Кусты/травы → всегда Mode A (не требуют инструмента)

**Логика выбора режима (V2):**
```
On F key press:
  if tile.Object has DestructibleHP > 0 AND player has tool with HardnessTier >= tile.HardnessTier:
    → Mode B: DamageObject(tool damage)
  else if tile.Object has ResourceAmount > 0:
    → Mode A: Harvest (10% per press, or 1% if "кулаки" on hard object)
  else:
    → nothing to harvest
```

---

## 4. Итоговые рекомендации

| Вопрос | Решение | Когда |
|--------|---------|-------|
| Защита слотов | ✅ Уже реализована (Dictionary + UI валидация) | Готово |
| Belt расходники | Quick-access slots (ссылки, v1) → контейнер (v2) | v2 |
| Вложенные контейнеры BG3 | Отложить. Использовать Storage Ring + Spirit Storage | v2+ |
| Окружение: добыча | Mode A (gradual) для всех объектов | V1 (сейчас) |
| Окружение: уничтожение | Mode B (threshold) для деревьев/камней/руды | V2 |
| Респаун | 7 дней (уже реализован) | Готово |
