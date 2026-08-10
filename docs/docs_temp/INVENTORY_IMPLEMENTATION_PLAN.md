# План внедрения инвентаря — Чекпоинты

**Создано:** 2026-04-18 18:20:58 UTC
**Версия:** 1.0
**Статус:** 📋 Активный план
**Решение:** ПОЛНАЯ ПЕРЕДЕЛКА кода инвентаря

---

## 🔑 Ключевая информация для восстановления контекста

### GitHub
- **Репозиторий:** `vivasua-collab/Ai-game3.git`
- **Ветвь:** `main`
- **Токен:** (см. контекст сессии, НЕ коммитить в файлы!)
- **Push:** `git add -A && git commit -m "описание" && git push`

### Правила работы
- См. `START_PROMPT.md` — АБСОЛЮТНЫЙ ЗАПРЕТ на запуск dev-серверов
- Комментарии ТОЛЬКО на русском
- Дата/время на КАЖДОЕ редактирование
- Чекпоинты в `checkpoints/` в формате `ММ_ДД_цель.md`
- Временная документация в `docs_temp/`
- Замороженные скрипты: FullSceneBuilder.cs, PhaseNNXxx.cs, IScenePhase — НЕ трогать

### Документация
- `docs_temp/INVENTORY_UI_DRAFT.md` — v2.0, актуальный черновик дизайна
- `docs/EQUIPMENT_SYSTEM.md` — Система экипировки (актуальна)
- `docs/UNITY_DOCS_LINKS.md` — Ссылки на документацию Unity 6.3
- `docs_asset_setup/SCENE_BUILDER_ARCHITECTURE.md` — Архитектура генератора сцены (ЗАМОРОЖЕНА)

---

## 📊 Аудит текущего кода (выполнен 2026-04-18)

### Текущие файлы (4 штуки, полная переделка)

| Файл | Строк | Вердикт |
|------|-------|---------|
| `Scripts/Inventory/InventoryController.cs` | 751 | ПЕРЕДЕЛАТЬ — фиксированная сетка 8×6, нет рюкзака |
| `Scripts/Inventory/EquipmentController.cs` | 752 | ПЕРЕДЕЛАТЬ — слоты не соответствуют v2.0 |
| `Scripts/Inventory/MaterialSystem.cs` | 549 | ОСТАВИТЬ — базовый функционал корректен |
| `Scripts/Inventory/CraftingController.cs` | 694 | ПЕРЕДЕЛАТЬ — зависит от InventoryController |

### Найденные ошибки в текущем коде

| # | Файл | Ошибка | Критичность |
|---|------|--------|-------------|
| AUD-01 | InventoryController.cs | `FreeSlots = TotalSlots - UsedSlots` — неверно. Предмет 2×2 занимает 4 ячейки, но считается как 1 слот | СРЕДНЯЯ |
| AUD-02 | EquipmentController.cs | `CanEquip` с `playerStats=null` пропускает проверки требований — если ServiceLocator не нашёл PlayerController | ВЫСОКАЯ |
| AUD-03 | EquipmentController.cs | `SwapSlots` меняет местами списки слоёв — физически бессмысленно (шлем на ноги) | СРЕДНЯЯ |
| AUD-04 | EquipmentController.cs | `EquipmentInstance.Slot` по умолчанию `EquipmentSlot.Backpack` при null data | НИЗКАЯ |
| AUD-05 | CraftingController.cs | `CraftCustom` всегда успешен — нет броска на успех/провал | СРЕДНЯЯ |
| AUD-06 | CraftingController.cs | `LoadSaveData` не восстанавливает `craftingExperience` | ВЫСОКАЯ |
| AUD-07 | CraftingController.cs | `LoadSaveData` не восстанавливает рецепты из `knownRecipeIds` | ВЫСОКАЯ |
| AUD-08 | InventoryController.cs | `Resize` — нет восстановления `nextSlotId` при загрузке, возможны коллизии ID | НИЗКАЯ |

### Несоответствия текущего EquipmentSlot и v2.0

**Текущий enum** (`Core/Enums.cs:321`):
```csharp
None, WeaponMain, WeaponOff, Armor, Clothing, Charger, RingLeft, RingRight, Accessory, Backpack
```

**Требуется по v2.0:**
```csharp
None, Head, Torso, Belt, Legs, Feet, WeaponMain, WeaponOff,
// Скрытые:
Amulet, RingLeft1, RingLeft2, RingRight1, RingRight2, Charger, Hands, Back
```

**Проблема:** Текущий enum не имеет Head, Torso, Belt, Legs, Feet. Armor/Clothing — это не слоты тела, а типы одежды.

---

## 🗂️ План чекпоинтов

### Общая структура файлов (после переделки)

```
Scripts/Inventory/
├── InventoryController.cs     ← ПЕРЕПИСАТЬ (рюкзак, динамическая сетка)
├── EquipmentController.cs     ← ПЕРЕПИСАТЬ (7 видимых слотов, 1H/2H)
├── MaterialSystem.cs          ← ОСТАВИТЬ без изменений
├── CraftingController.cs      ← ПЕРЕПИСАТЬ (исправить баги, адаптировать)
├── BackpackData.cs            ← НОВЫЙ (ScriptableObject рюкзака)
├── SpiritStorageController.cs ← НОВЫЙ (позже, этап 4)
├── StorageRingController.cs   ← НОВЫЙ (позже, этап 5)
└── UI/                        ← НОВЫЕ (этап 3)
    ├── InventoryUI.cs
    ├── BodyDollPanel.cs
    ├── BackpackPanel.cs
    ├── InventorySlotUI.cs
    ├── DragDropHandler.cs
    └── TooltipPanel.cs
```

---

## ✅ Этап 0: Подготовка данных (базовые модели)

**Статус:** ⬜ Не начат
**Чекпоинт:** `checkpoints/04_18_inv_0_data.md`

### Задачи:

- [ ] **0.1** Обновить `EquipmentSlot` enum в `Core/Enums.cs`
  - Новые значения: None, Head, Torso, Belt, Legs, Feet, WeaponMain, WeaponOff
  - Скрытые: Amulet, RingLeft1, RingLeft2, RingRight1, RingRight2, Charger, Hands, Back
  - Удалить: Armor, Clothing, Accessory, Backpack (как слоты экипировки)
  - **ОСТОРОЖНО:** Этот enum используется другими системами! Проверить все ссылки

- [ ] **0.2** Добавить `NestingFlag` enum в `Core/Enums.cs`
  - Значения: None, Spirit, Ring, Any

- [ ] **0.3** Добавить поля в `ItemData` (`Data/ScriptableObjects/ItemData.cs`)
  - `float volume` — объём предмета (по умолчанию 1.0)
  - `NestingFlag allowNesting` — флаг вложения (по умолчанию Any)

- [ ] **0.4** Создать `BackpackData` ScriptableObject (`Data/ScriptableObjects/BackpackData.cs`)
  - Поля: id, nameRu, gridWidth, gridHeight, weightReduction, maxWeightBonus, beltSlots, ownWeight
  - Стартовый рюкзак: Тканевая сумка (3×4, 0% снижение, +0 вес)

- [ ] **0.5** Создать `WeaponHandType` enum в `Core/Enums.cs`
  - Значения: OneHand, TwoHand

- [ ] **0.6** Добавить поле `WeaponHandType handType` в `EquipmentData`
  - Для оружия: OneHand или TwoHand
  - Для прочего: OneHand (по умолчанию)

### Зависимости:
- `Core/Enums.cs` — затрагивает ВСЕ скрипты, использующие EquipmentSlot
- `Data/ScriptableObjects/ItemData.cs` — затрагивает генератор предметов

### Критерий готовности:
- Все enum обновлены
- BackpackData SO компилируется
- ItemData имеет volume и allowNesting
- Существующий код, зависящий от старого EquipmentSlot, АДАПТИРОВАН (или временно закомментирован с TODO)

---

## ✅ Этап 1: Базовая кукла (Doll)

**Статус:** ⬜ Не начат
**Чекпоинт:** `checkpoints/04_18_inv_1_doll.md`

### Задачи:

- [ ] **1.1** Переписать `EquipmentController.cs`
  - 7 видимых слотов: Head, Torso, Belt, Legs, Feet, WeaponMain, WeaponOff
  - Скрытые слоты — заглушки (не функциональны, не отображаются)
  - **БЕЗ системы слоёв** (armor+clothing) — один предмет на слот
  - Логика 1H/2H оружия:
    - TwoHand экипируется в WeaponMain, блокирует WeaponOff
    - Если WeaponOff занят — снять перед экипировкой двуручного
    - Одноручное — один слот
  - Equip → проверка требований (cultivation level, stats)
  - Unequip → возвращает EquipmentInstance
  - События: OnEquipped, OnUnequipped, OnStatsChanged
  - Расчёт статов: сумма бонусов от всей экипировки × gradeMultiplier
  - Save/Load

- [ ] **1.2** Обновить `EquipmentInstance`
  - Убрать `currentLayer` (нет слоёв)
  - Добавить `weaponHandType` (для UI проверки двуручности)

- [ ] **1.3** Обновить `EquipmentStats`
  - Сохранить: totalDamage, totalDefense, damageReduction, dodgeBonus
  - Сохранить: strength, agility, constitution, intelligence
  - Сохранить: maxQi, qiRegen
  - Сохранить: customBonuses
  - Убрать: EquipmentSlotsUI (это UI класс, не относится к бэкенду)

- [ ] **1.4** Интеграция с ServiceLocator
  - EquipmentController регистрируется при Awake
  - CanEquip использует ServiceLocator для проверки требований
  - Если QiController/PlayerController не найдены → разрешить экипировку (безопасный дефолт)

### Зависимости:
- Этап 0 (EquipmentSlot enum, WeaponHandType)
- QiController, PlayerController — через ServiceLocator

### Критерий готовности:
- EquipmentController компилируется и работает с новыми слотами
- Логика 1H/2H корректна
- Save/Load совместим с новым форматом
- Старые баги AUD-02, AUD-03, AUD-04 исправлены

---

## ✅ Этап 2: Рюкзак (Backpack)

**Статус:** ⬜ Не начат
**Чекпоинт:** `checkpoints/04_18_inv_2_backpack.md`

### Задачи:

- [ ] **2.1** Переписать `InventoryController.cs`
  - Размер сетки определяется рюкзаком (не фиксированный)
  - `SetBackpack(BackpackData backpack)` — сменить рюкзак
  - При смене рюкзака: если старый не пуст — предметы сохраняются в рюкзаке
  - Эффективный вес = totalWeight × (1 − backpack.weightReduction)
  - Стартовый рюкзак = Тканевая сумка (3×4)
  - Сохранить: AddItem, RemoveItem, MoveItem, SwapSlots
  - Сохранить: FindSlotById, HasItem, CountItem, HasFreeSpace
  - Сохранить: События (OnItemAdded, OnItemRemoved, OnItemStackChanged, OnWeightChanged, OnInventoryFull)
  - Исправить AUD-01: FreeSlots = количество свободных ячеек сетки (не TotalSlots - UsedSlots)
  - Save/Load: сохранять backpackId + содержимое

- [ ] **2.2** Переписать `CraftingController.cs`
  - Адаптировать к новому InventoryController
  - Исправить AUD-05: CraftCustom — добавить бросок успеха
  - Исправить AUD-06: LoadSaveData — восстанавливать craftingExperience
  - Исправить AUD-07: LoadSaveData — восстанавливать рецепты из knownRecipeIds
  - Исправить AUD-08: Сохранять nextSlotId или генерировать при загрузке

- [ ] **2.3** Создать систему рюкзаков
  - BackpackData SO (уже создан на этапе 0)
  - BackpackInstance — текущий рюкзак персонажа + его содержимое
  - При снятии рюкзака: содержимое остаётся внутри, рюкзак падает в мир (как предмет)
  - При надевании нового: если пуст — сменить; если нет — предложить убрать

### Зависимости:
- Этап 0 (BackpackData SO)
- Этап 1 (EquipmentController — рюкзак НЕ слот экипировки, отдельная система)

### Критерий готовности:
- InventoryController работает с динамической сеткой
- Смена рюкзака корректна
- WeightReduction применяется
- CraftingController адаптирован, баги исправлены
- Save/Load работает

---

## ✅ Этап 3: Пользовательский интерфейс

**Статус:** ⬜ Не начат
**Чекпоинт:** `checkpoints/04_18_inv_3_ui.md`

### Задачи:

- [ ] **3.1** Создать `InventoryUI.cs` — главный контроллер UI
  - Canvas (Screen Space - Overlay)
  - Открытие/закрытие по клавише I
  - GameState.Inventory при открытии
  - Cursor.lockState = None при открытии

- [ ] **3.2** Создать `BodyDollPanel.cs` — кукла тела
  - 7 видимых слотов с иконками
  - Подсветка занятых/пустых
  - Отображение экипированных предметов

- [ ] **3.3** Создать `BackpackPanel.cs` — сетка рюкзака
  - Динамическая сетка (3×4 / 4×5 / ...)
  - Предметы с иконками и счётчиками
  - Рамки по редкости
  - Вес и вместимость внизу

- [ ] **3.4** Создать `InventorySlotUI.cs` — визуальный слот
  - Иконка предмета
  - Счётчик стака
  - Рамка по редкости
  - Подсветка при hover/drag

- [ ] **3.5** Создать `DragDropHandler.cs` — система перетаскивания
  - Drag & Drop между слотами рюкзака
  - Drag на куклу (экипировка)
  - Swap при помещении на занятый слот
  - Подсветка валидных/невалидных позиций
  - 2H оружие: блокировка off-слота

- [ ] **3.6** Создать `TooltipPanel.cs` — карточка предмета
  - Полная информация о предмете
  - Статы, грейд, прочность
  - Объём и вложение

### Зависимости:
- Этап 1 (EquipmentController)
- Этап 2 (InventoryController с рюкзаком)

### Критерий готовности:
- Инвентарь открывается/закрывается по I
- Кукла показывает экипировку
- Рюкзак показывает предметы в сетке
- Drag & Drop работает
- Tooltip при наведении

---

## ⬜ Этап 4: Духовное хранилище (ПОСЛЕ этапа 3)

**Статус:** ⬜ Отложен
- SpiritStorageController (новый)
- SpiritStorageView (каталогизатор)
- Фильтры, поиск, группировка
- Стоимость (Ци + время)

## ⬜ Этап 5: Кольца хранения (ПОСЛЕ этапа 4)

**Статус:** ⬜ Отложен
- StorageRingController (новый)
- StorageRingView
- Объём предметов
- Флаг allowNesting
- Слоты колец на кукле (скрытые → открываются)

## ⬜ Этап 6: Пояс + доработка (ПОСЛЕ этапа 5)

**Статус:** ⬜ Отложен
- BeltPanel (динамические слоты)
- Контекстное меню (правый клик)
- Разделение стека
- Выброс предметов
- Анимации

---

## 📋 Шаблон чекпоинта

Каждый чекпоинт создаётся в `checkpoints/ММ_ДД_цель.md`:

```markdown
# Чекпоинт: [Название]
**Дата:** YYYY-MM-DD HH:MM:SS UTC
**Статус:** in_progress | complete | blocked

## Выполнено
- [ ] Задача 1
- [ ] Задача 2

## Изменённые файлы
- `path/to/file.cs` — описание изменений

## Проблемы
- Описание (если есть)

## Следующий шаг
- Что делать дальше
```

---

## ⚠️ Риски и предупреждения

1. **EquipmentSlot enum** — используется во многих местах. При изменении — глобальный поиск и замена.
2. **ItemData** — изменение затронет генератор предметов (Phase09GenerateAssets). Нужно обновить JSON-шаблоны.
3. **Save/Load** — новый формат несовместим со старым. Версионирование сохранений!
4. **UI Toolkit vs uGUI** — Unity 6.3 поддерживает оба. Для инвентаря используем uGUI (Canvas) — проще для drag & drop.
5. **Контекст может быть потерян** — после каждого этапа ОБЯЗАТЕЛЬНО пушить на GitHub и обновлять этот файл.

---

*Создано: 2026-04-18 18:20:58 UTC*
