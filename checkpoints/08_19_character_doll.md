# Чекпоинт: Реализация куклы персонажа + тестовые предметы + drag&drop

**Дата:** 2026-08-19 08:15 UTC
**Сессия:** web-d86b1055
**Тип:** implementation

---

## Контекст

Пользователь запросил: проверить реализацию инвентаря, реализовать "куклу" персонажа, перетаскивание экипировки мышкой с инвентаря на куклу, сгенерировать тестовые предметы для проверки работоспособности.

## Что сделано

### 1. Аудит текущей реализации (Task 2-a)
- **Backend Inventory** полностью реализован (15 файлов, ~3500 LOC): InventoryService, EquipmentService, EquipmentValidator, EquipmentStatAggregator, BackpackService, StorageRingService, CraftingService, MaterialService
- **Frontend**: только InventoryWindow (260 LOC, read-only display, список предметов)
- **Документация**: INVENTORY_SYSTEM.md §4 (Body Doll — 15 слотов: 7 видимых + 8 скрытых), EQUIPMENT_SYSTEM.md, INVENTORY_UI_DRAFT.md (668 строк, ASCII mockup), INVENTORY_IMPLEMENTATION_PLAN.md (6-stage plan)
- **Ai-game3-ref**: BodyDollPanel.cs (202 LOC) + EquipmentSlotUI.cs (213 LOC) — рабочая Unity реализация для порта
- **BodySlotMapping.cs**: статический словарь BodyPartType → EquipmentSlot[] (критично для блокировки слотов при ампутации)

### 2. Создан TestItemSeeder.cs (290 LOC)
17 тестовых предметов:
- **3 оружия**: железный меч-цзянь (1H), стальное копьё цян (2H), деревянный посох (1H)
- **7 брони**: шлем, нагрудник, роба, поножи, сапоги, пояс, перчатки
- **3 аксессуара**: нефритовый амулет, кольцо, плащ
- **4 расходника**: пилюля лечения, пилюля Ци, свиток телепорта, эликсир

Все предметы регистрируются в IItemDatabaseService и кладутся в инвентарь игрока.

### 3. Создан CharacterDollPanel.cs (470 LOC)
Панель куклы с 11 слотами:
- **7 видимых**: Head, Torso, Belt, Legs, Feet, WeaponMain, WeaponOff
- **4 скрытых**: Amulet, RingLeft1, Hands, Back
- Stats summary: Броня / Урон / Хват (TwoHand/OneHand/None)
- Каждый слот = DollSlotRow (HBoxContainer с rarity indicator + label + item name)
- Drag&drop через Godot Control API: `_GetDragData`, `_CanDropData`, `_DropData`
- LMB click на занятом слоте = quick unequip
- RMB click = info в лог

### 4. Переписан InventoryWindow.cs (340 LOC)
- Layout: слева список предметов (drag source, 560px), справа кукла (drop target, 260px)
- Размер 880×560 (вместо 600×500)
- Предметы draggable (только экипировка; расходники показывают "нельзя надеть")
- `RefreshExternally()` — обновление после drag&drop
- `TestItemSeeder.Seed()` при первом открытии
- Click на background = закрыть
- Каждый ItemRow показывает: rarity indicator + name + qty + weight

### 5. Drag&drop логика (HandleDropOnSlot)
- Проверка: item must be EquipmentData (расходники rejected)
- Slot match (1H weapon flexible в любую руку; 2H только в WeaponMain)
- Remove from inventory → Equip (с rollback при ошибке)
- 2H weapon: auto-unequip WeaponOff (возвращается в инвентарь)
- Old item из занятого слота возвращается в инвентарь
- Логирование всех операций для отладки

## Решения

- **Порт из Ai-game3 вместо написания с нуля** — BodyDollPanel.cs и EquipmentSlotUI.cs уже были рабочими в Unity. Адаптированы под Godot Control API (VerticalLayoutGroup → VBoxContainer, IPointerClickHandler → _GuiInput).
- **Список слотов вместо силуэта** — для v1 выбран list layout (как в InventoryWindow) вместо ASCII силуэта тела. Силуэт запланирован на Phase H (per INVENTORY_IMPLEMENTATION_PLAN.md stage 3).
- **11 слотов вместо 15** — убраны RingLeft2, RingRight1, RingRight2, Charger (заглушки для будущих фаз). Добавлены 4 скрытых для теста: Amulet, RingLeft1, Hands, Back.
- **TestItemSeeder как статический класс** — не сервис, вызывается из InventoryWindow._Ready. Idempotent (static bool _itemsSeeded). Отключается удалением вызова Seed().
- **Drag&drop через Godot Control API** — `_GetDragData` / `_CanDropData` / `_DropData` (Godot 4 нативный drag&drop, без кастомной реализации).
- **Backend не изменён** — использованы существующие IInventoryService + IEquipmentService + IItemDatabaseService. Все операции (TryAddItem, TryRemoveItem, TryEquip, TryUnequip) уже работали.

## Найденные проблемы

- **.gitignore правило `game` (строка 87)** — игнорировало ВСЕ файлы в `game/` (совпадение по имени). Новые CharacterDollPanel.cs и TestItemSeeder.cs не трекались. **Исправлено**: заменено на `/my-project/`.
- **`AddThemeOffsetOverride` не существует в Godot 4** — Label не имеет этого метода. **Исправлено**: удалено (shadow опущен для simplicity).
- **`ItemDatabase` accessibility** — приватное поле недоступно из DollSlotRow. **Исправлено**: добавлен `internal IItemDatabaseService GetItemDatabase()`.
- **EquipmentDataProvider.GetEquipped() возвращает null** (TODO line 28) — не влияет на player doll (используется IEquipmentService напрямую). Для NPC нужно доделать.
- **EquipmentValidator stubs** (cult-level & stat requirements) — позволяет надеть любую экипировку. Для v1 acceptable.
- **`Back` slot ambiguity** — Ai-game3 включает Back в visible (8 slots), v2 doc — в hidden. Выбран v2 (7 visible, Back hidden).

## Следующие шаги

- [ ] Визуальная проверка на ПК с Godot (у пользователя нет доступа сегодня)
- [ ] Phase B: Tooltip (порт TooltipPanel.cs из Ai-game3) — hover card с полными характеристиками
- [ ] Phase E: Backpack panel с stacking UI (per STACKING_SYSTEM_DRAFT.md)
- [ ] Phase F: Spirit storage + storage ring catalogs
- [ ] Phase G: Belt quick-access slots
- [ ] Phase H: Body silhouette (procedural 64×64 humanoid, clickable parts, color by BodyPartState)
- [ ] Wire EquipmentValidator к IQiService + IStatService (проверка cult-level & stat requirements)
- [ ] Использовать IItemGeneratorService вместо TestItemSeeder для runtime генерации

## Файлы

**Созданные:**
- `/home/z/my-project/aigame4/game/src/Adapter/UI/TestItemSeeder.cs` (290 LOC) — 17 тестовых предметов
- `/home/z/my-project/aigame4/game/src/Adapter/UI/CharacterDollPanel.cs` (470 LOC) — кукла + DollSlotRow + drag&drop
- `/home/z/my-project/aigame4/checkpoints/08_19_character_doll.md` — этот чекпоинт

**Изменённые:**
- `/home/z/my-project/aigame4/game/src/Adapter/UI/InventoryWindow.cs` — переписан (340 LOC, layout с куклой, drag source)
- `/home/z/my-project/aigame4/.gitignore` — исправлено правило `game` → `/my-project/`
- `/home/z/my-project/aigame4/worklog.md` — добавлена запись 08:15

**Верификация:**
- `dotnet build`: 0 errors, 224 warnings
- Headless: `[Inventory] Test items seeded`, `[CharacterDoll] Ready`, `[Inventory] Ready` — все компоненты загружаются
