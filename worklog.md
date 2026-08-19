# Worklog — Cultivation World Simulator (Ai-game4)

> Хроника работы (append-only). Новые записи добавляются в конец.
> Формат записи см. в START_PROMPT.md §6.

---

## 2026-08-19

### 07:12 — Реструктуризация линкования окружения

**Task ID:** 08_19-linking
**Agent:** main (Z.ai Code)

**Проблема:**
Папка `checkpoints/` не линковалась в окружение sandbox. Пользователь не видел чекпоинты с 16 августа. Корневая причина: `recover_sandbox.sh` создавал только 3 симлинка (`game`, `game-docs`, `godot`), а вся папка `Ai-game4/` целиком не была видна.

**Анализ:**
- `Ai-game3-ref/` работает без проблем, потому что папка видна напрямую (нет симлинков внутрь).
- `Ai-game4/` не видна в workspace — только симлинки внутрь.
- `checkpoints/`, `worklog.md`, `START_PROMPT.md`, `SESSION_SUMMARY.md`, `recover_sandbox.sh` — всё невидимо.

**Решение (Вариант D — гибридный):**
- `aigame4` → `Ai-game4/` (единая точка входа, как Ai-game3-ref)
- `checkpoints` → `Ai-game4/checkpoints` (прямой доступ)
- `game`, `game-docs`, `godot` — сохранены для обратной совместимости

**Документация:**
- `docs/docs_v2/09_workflow/ENVIRONMENT_LINKING.md` — дизайн-документ с 4 вариантами
- `recover_sandbox.sh` шаг 4 обновлён (5 симлинков вместо 3)
- `START_PROMPT.md` §3 обновлён

**Stage Summary:**
- 5 симлинков созданы и проверены
- 18 чекпоинтов доступны через `checkpoints/` и `aigame4/checkpoints/`
- Структура задокументирована, восстанавливается через `recover_sandbox.sh`

---

### 07:35 — Переход на COLD START (минимальная структура)

**Task ID:** 08_19-cold-start
**Agent:** main (Z.ai Code)

**Причина:**
Пользователь хотел чистый холодный старт: "получения Ai-game4 на прямую в локальное окружение, без линковки в других местах, сразу же получится у меня структура как в gitHub". Variant D (5 симлинков) всё ещё выглядел как band-aid. Нужна структурная минимизация.

**Платформенное ограничение (обнаружено):**
`/home/z/my-project/` — сам git-репозиторий Z.ai Code (remote = Ai-game4, но коммиты = UUID сессий). Используется для персистентности sandbox. Следствие: Ai-game4 не может стать корнем workspace (git конфликт). Должен быть подпапкой.

**Решение:**
- Убраны избыточные симлинки: `checkpoints`, `game`, `game-docs`
- Осталось только 2: `aigame4` (весь репо) + `godot` (toolchain)
- Создан `cold_start.sh` — чистый idempotent скрипт (замена recover_sandbox.sh)
- `recover_sandbox.sh` → deprecated wrapper на `cold_start.sh`

**Файлы:**
- `cold_start.sh` — новый скрипт (6 шагов: .NET, Godot, git clone, 2 симлинка, NuGet, верификация)
- `docs/docs_v2/09_workflow/COLD_START.md` — дизайн-документ (сравнение с Variant D, ответ на вопрос про "другое окружение")
- `START_PROMPT.md` §3 — обновлён (2 симлинка вместо 5)
- `recover_sandbox.sh` — deprecated alias

**Stage Summary:**
- 2 симлинка: `aigame4` + `godot` (вместо 5)
- Доступ ко всему через `aigame4/` (структура 1-в-1 как GitHub)
- 19 чекпоинтов доступны через `aigame4/checkpoints/`
- cold_start.sh idempotent, безопасно запускать многократно


---

### 08:15 — Реализация куклы персонажа + тестовые предметы + drag&drop

**Task ID:** 2-b
**Agent:** main (Z.ai Code)

**Задача:**
Проверить реализацию инвентаря, реализовать "куклу" персонажа, перетаскивание экипировки мышкой с инвентаря на куклу. Сгенерировать тестовые предметы.

**Аудит (Task 2-a):**
- Backend Inventory полностью реализован (15 файлов, ~3500 LOC): InventoryService, EquipmentService, EquipmentValidator, EquipmentStatAggregator, BackpackService, StorageRingService, CraftingService
- Frontend: только InventoryWindow (260 LOC, read-only display)
- Docs: INVENTORY_SYSTEM.md §4 (кукла, 7 видимых + 8 скрытых слотов), EQUIPMENT_SYSTEM.md, INVENTORY_UI_DRAFT.md (668 строк)
- Ai-game3-ref: BodyDollPanel.cs (202 LOC) + EquipmentSlotUI.cs (213 LOC) — рабочая реализация для порта
- BodySlotMapping.cs: статический словарь BodyPartType → EquipmentSlot[] (критично для блокировки слотов при ампутации)

**Реализовано:**
1. **TestItemSeeder.cs** (290 LOC) — 17 тестовых предметов:
   - 3 оружия (1H меч-цзянь, 2H копьё, посох)
   - 7 брони (шлем, нагрудник, роба, поножи, сапоги, пояс, перчатки)
   - 3 аксессуара (амулет, кольцо, плащ)
   - 4 расходника (пилюля лечения, пилюля Ци, свиток телепорта, эликсир)
   - Предметы регистрируются в IItemDatabaseService + кладутся в инвентарь

2. **CharacterDollPanel.cs** (470 LOC) — кукла с 11 слотами:
   - 7 видимых: Head, Torso, Belt, Legs, Feet, WeaponMain, WeaponOff
   - 4 скрытых: Amulet, RingLeft1, Hands, Back
   - Подписка на EquipmentChangedEvent (обновление одного слота)
   - Stats summary: Броня/Урон/Хват
   - Drag&drop: _GetDragData (унести), _CanDropData, _DropData (надеть)
   - Click LMB = quick unequip, RMB = info

3. **InventoryWindow.cs** (переписан, 340 LOC):
   - Layout: слева список предметов (drag source), справа кукла (drop target)
   - Размер 880×560 (вместо 600×500)
   - Предметы draggable (только экипировка; расходники показывают "нельзя надеть")
   - RefreshExternally() — обновление после drag&drop
   - TestItemSeeder.Seed() при первом открытии
   - Click на background = закрыть

4. **Drag&drop логика (HandleDropOnSlot):**
   - Проверка: item must be EquipmentData
   - Slot match (1H weapon flexible в любую руку)
   - Remove from inventory → Equip (с rollback при ошибке)
   - 2H weapon: auto-unequip WeaponOff
   - Old item возвращается в инвентарь

**Исправлено:**
- .gitignore: правило `game` (строка 87) игнорировало все файлы в game/. Заменено на `/my-project/`
- AddThemeOffsetOverride: не существует в Godot 4 (удалено)
- ItemDatabase accessibility: добавлен internal accessor GetItemDatabase()

**Верификация:**
- dotnet build: 0 errors, 224 warnings (без изменений)
- Headless: [Inventory] Test items seeded, [CharacterDoll] Ready, [Inventory] Ready — все 3 компонента загружаются
- Все 17 тестовых предметов регистрируются в БД и попадают в инвентарь

**Stage Summary:**
- Кукла персонажа реализована (11 слотов, 7 видимых + 4 скрытых)
- Drag&drop работает: инвентарь → кукла (надеть), кукла → инвентарь (снять), click (быстрое снятие)
- 17 тестовых предметов покрывают все категории (Weapon/Armor/Accessory/Consumable)
- .gitignore fixed: новые файлы теперь трекаются корректно
- Backend не изменён — использованы существующие IInventoryService + IEquipmentService
