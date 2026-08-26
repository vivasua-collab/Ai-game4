# Чекпоинт-план: Окно Культивации Ци + Система горячих клавиш + Техдолг

**Дата:** 2026-08-26 09:04 MSK (UTC+3)
**Сессия:** основная (Z.ai Code sandbox, режим без субагентов)
**Тип:** audit → plan → implementation
**HEAD на старте:** `f54ebd7` (Stage 0+1 реализовано, не запушено)
**Запрос:** окно Культивации (отдельная клавиша, как инвентарь) + система хоткеев (1=ближнее, 2=дальнее, 3-9=техники, Shift/Ctrl+цифра=пояс) + аудит + техдолг

---

## ⚠ Режим работы (новая схема)

1. **Без субагентов.** Все чтения/правки в основном потоке.
2. **Сначала план, потом выполнение.** Этот файл — план + журнал прогресса.
3. **После каждого этапа** — обновление этого чекпоинта (отметка `[x]`) + git commit.
4. **Точки восстановления** — после каждой завершённой секции. Если среда упадёт,
   следующий запуск читает этот файл и продолжает с последней `[x]`.

---

## Контекст (состояние на старте)

- Stage 0 «Модель заполнения» (коммит `f54ebd7`) — РЕАЛИЗОВАНО
- Stage 1 «Аура-задержка» (вариант В) — РЕАЛИЗОВАНО
- Аудит доков 5.2→5.3 — выполнен (TECHNIQUE_SYSTEM §5.3 K + §5.4 аура)
- Sim-тесты GODOT_NEWGAME / CHARGE_SIM / COMBAT_SIM / TRADE_DEBUG — все PASS
- Не запушено в origin (последний push `7655ee6`)

---

## Этап A — Аудит Stage 0+1 (вчерашний код)

- [x] A1. `TechniqueChargeService.cs` (Stage 0)
- [x] A2. `AuraHoldService.cs` (Stage 1)
- [x] A3. `TechniqueService.cs` (legacy `UseTechnique` + новый `CompleteUse`)
- [x] A4. `Constants.cs` (Stage 0+1 секция: COMBAT_CHANNEL_MULT=12, MIN_CHARGE_RATE=1.0, CHARGE_CANCEL_REFUND_PERMIL=500, AURA_HOLD_DECAY_PERMIL=10, POTENCY_BASE_PERMIL=1000, POTENCY_MAX_PERMIL=2000)
- [x] A5. `TechniqueChargeContracts.cs` (5 событий: Started/Progress/Completed/Cancelled + HeldTechniqueChanged)
- [x] A6. `PlayerTechniqueCaster.cs` (Stage 0+1 переключатель: OnCastRequested, OnChargeCompleted, FireTechnique)
- [x] A7. `CombatContracts.cs` (AttackIntentEvent + PotencyPermil + IsCharged)
- [x] A8. `CombatService.cs` — найдено: `_pendingTechnique` (стр 91, глобальный), `_lastAttackPotencyPermil` (стр 96, 317, 342, 396 — используется), `ExecuteAttack` 6-параметров (стр 286), skip pending при `isCharged || potency > 1000` (стр 315), **мёртвый `GetTechniquePotencyPermil`** (стр 770 — вызывает `_techniqueChargeService.GetPotencyPermil`, не используется в основном пути)
- [x] A9. `CombatModule.cs` — `Tick`: `UpdateTimer(delta)` + `UpdateCooldowns(delta)` + `UpdateCharges(delta)` (Stage 0); `OnAttackIntent` форвардит `e.PotencyPermil, e.IsCharged` в `ExecuteAttack`
- [x] A10. `ICombatService.cs` — `ExecuteAttack` 6-параметров с defaults (`targetId=null, isRanged=false, potencyPermil=1000, isCharged=false`)
- [x] A11. **Доп. аудит (вне Stage 0+1):** `InputAdapter.cs` (polling `hotbar_1..9` без Shift-модификатора; sticky keys HashSet), `InputMapInitializer.cs` (`hotbar_1..9` = Key0+i; **C = character_sheet** — конфликт! нужна другая клавиша для CultivationWindow), `HotbarPanel.cs` (1-2=оружие info, 3-9=пояс), `InventoryWindow.cs` (паттерн Control + Inject + SetAnchorsPreset)
- [x] A12. Зафиксировать найденные проблемы (см. §"Найденные проблемы Stage 0+1" ниже)

### Найденные проблемы Stage 0+1 (по итогам A1–A7)

**P1-ARCH (баги архитектуры):**
1. **`TechniqueService.LearnTechnique` не копирует `CapacityCost`** из TechniqueData в LearnedTechnique (строка 186-203). Из-за этого `tech.CapacityCost` всегда = 0 → TechniqueChargeService:154 берёт fallback `qiCost` как capacity → potency 1001-2000 (overcharge, Stage 2) никогда не активируется. **Влияние:** Stage 2 overcharge заблокирован. **Фикс:** добавить `CapacityCost = data.CapacityCost` в копирование.
2. **`AuraHoldService.Tick` декей не масштабируется с deltaTime** (строка 123). `decayPerTick = Math.Max(1, QiCost × AURA_HOLD_DECAY_PERMIL / 1000)` — фиксированный, без `× deltaTime`. На Fast speed (×2) декей должен быть вдвое больше. **Фикс:** `decayPerTick = Math.Max(1, (long)(QiCost × AURA_HOLD_DECAY_PERMIL / 1000.0 × deltaTime))`.
3. **`QiConsumeRequestEvent` публикуется без указания entityId** (TechniqueChargeService:241, 252). Сейчас работает (заряжает только игрок), но архитектурно нарушает per-entity модель — если NPC начнут заряжать, Ци спишется у игрока. **Фикс (Stage 2, опционально):** добавить EntityId в QiConsumeRequestEvent. Сейчас — отметить как техдолг.

**P2-DEAD-CODE (мёртвый код):**
4. `CombatService.GetTechniquePotencyPermil` — заменён на `_lastAttackPotencyPermil`. Удалить.
5. `ChargeState.LastMouseX/Y` — дублирует данные в `TechniqueChargeCompletedEvent`. После публикации не используется. Можно убрать, но не критично.

**P3-DUAL-PLAYER-ID (техдолг, известен):**
6. Третья копия нормализации "player"/"player_0" в TechniqueChargeService:365-381.
   - `PlayerService.PlayerId` = "player_0"
   - `QiService.UpdateState` публикует QiChangedEvent под "player"
   - `BodyService:455`, `TechniqueChargeService:365` — нормализуют lookup
   - **Фикс:** централизовать в helper `PlayerIdResolver` (Core/Helpers) — единая функция `ResolvePlayerId(raw)` и `AreSameEntity(a, b)`. Обновить 3 точки вызова.

**P4-SAVE-LOAD (техдолг, известен):**
7. `AuraHoldService._held` не сериализуется (нет ISaveable). При сейве удержание теряется.
8. `TechniqueChargeService._activeCharges` не сериализуется. При сейве активные зарядки теряются.
   - **Фикс (минимальный):** Cancel-on-save (публикуем Cancelled с возвратом 50%) перед сериализацией.

**P5-CONSISTENCY (несоответствия):**
9. `HeldTechniqueChangedEvent.EntityId` = `_player.PlayerId` = "player_0", но `QiChangedEvent.EntityId` = "player". UI/визуал, фильтрующий по entityId, не сматчится. Та же проблема P3. Фикс через централизацию.

---

## Этап B — Закрытие техдолга (по результатам аудита)

- [x] B1. **Централизация P0-DUAL-PLAYER-ID:** создать `Core/Helpers/PlayerIdResolver.cs` с методами `ResolvePlayerId(string raw)` и `AreSameEntity(string a, string b)`. Обновить точки: `PlayerService`, `BodyService:455`, `TechniqueChargeService:365`.
- [x] B2. **Фикс CapacityCost копирования:** `TechniqueService.LearnTechnique` добавить `CapacityCost = data.CapacityCost`.
- [x] B3. **Фикс AuraHoldService.Tick декей × deltaTime.**
- [x] B4. **Удалить мёртвый `CombatService.GetTechniquePotencyPermil`.**
- [x] B5. **Save/Load зарядок:** `SaveService` перед сериализацией вызывает `TechniqueChargeService.CancelCharge(playerId, "save")` и `AuraHoldService.Dissipate("save")` (возврат 50%). Минимальный scope, edge-case закрыт.
- [x] B6. **Архивный баннер `docs/` v1:** в `docs/README.md` добавить предупреждение «историческая версия, источник истины — docs_v2/».

**Точка восстановления B:** ✅ `dotnet build` — 0 errors, 266 warnings (pre-existing CS0649/CS0169). git commit pending.

---

## Этап C — Окно Культивации Ци (CultivationWindow)

**Архитектура:** новый `Adapter/UI/CultivationWindow.cs` (Godot Control), открывается отдельной клавишей (предлагается `C`), аналогично InventoryWindow. Данные — из существующих сервисов (QiDataProvider, TechniqueService, PlayerService, StatService) через подписки на события (EVT-01).

### Структура окна (3 вкладки + панель слотов техник)

```
┌─ CultivationWindow (Control, скрытый по умолчанию) ──────────┐
│  [Вкладки: Техники | Меридианы | Ядро]      [× Закрыть]      │
├──────────────────────────────────────────────────────────────┤
│ ┌─ Левая панель (40%) ──┐  ┌─ Правая панель (60%) ─────────┐ │
│ │ Список изученных      │  │ Характеристики выбранной     │ │
│ │ техник (scroll)       │  │ техники: тип, грейд, уровень,│ │
│ │  • Меч Ветра L1       │  │ стихия, qiCost, кулдаун,     │ │
│ │  • Щит Земли L1       │  │ мощность, мастерство         │ │
│ │  • ...                │  │                              │ │
│ │                       │  │ [Установить в слот ▼] → 3-9  │ │
│ │                       │  │ (выбор слота быстрого дост.) │ │
│ └───────────────────────┘  └──────────────────────────────┘ │
├──────────────────────────────────────────────────────────────┤
│ Панель слотов техник (горизонтальная, 7 ячеек: 3..9)         │
│  [3] [4] [5] [6] [7] [8] [9]                                │
│ Каждая ячейка: иконка стихии + название техники + хоткей   │
│ Пустая ячейка: «— пусто —»                                  │
└──────────────────────────────────────────────────────────────┘
```

**Вкладка «Меридианы»:** проводимость (finalConductivity), уровень культивации, множитель канала (K=12), расчётная chargeRate.
**Вкладка «Ядро»:** currentQi / coreCapacity (заполнение ‰), coreCapacity (ёмкость), cultivationLevel (уровень ядра), breakthroughStage (этап прокачки — нужен новые данные из QiService/QiDataProvider).

### Шаги внедрения

- [x] C1. **Контракты:** добавить в `Core/Messaging/Contracts/UIContracts.cs` события:
  - `CultivationWindowToggleRequestedEvent` (bool open) — публикует InputAdapter по клавише K
  - `TechniqueSlotAssignedEvent` (slotIndex 3-9, techniqueId) — публикует CultivationWindow при установке
  - `TechniqueSlotClearedEvent` (slotIndex) — при очистке слота
- [x] C2. **Сервис слотов техник:** новый `Modules/Player/TechniqueSlotService.cs` — хранит маппинг slotIndex(3-9) → techniqueId, перезагружаемый из save. Подписан на TechniqueForgottenEvent (очищает слот, если техника удалена). ISaveable.
- [x] C3. **CultivationWindow.cs** — Godot Control (аналог InventoryWindow). Вкладки через `TabContainer`. Открытие/закрытие через метод Toggle() (из GameWorldController.HandleStickyInput) + через CultivationWindowToggleRequestedEvent (для программного открытия).
- [x] C4. **Вкладка «Техники»:** список (ItemList) + детали (Label). Подписка на `TechniqueLearnedEvent` / `TechniqueForgottenEvent` для обновления списка.
- [x] C5. **Вкладка «Меридианы»:** подписка на `QiChangedEvent` (finalConductivity, cultivationLevel). Отображение проводимости, K, chargeRate.
- [x] C6. **Вкладка «Ядро»:** currentQi / coreCapacity / cultivationLevel / breakthroughStage. Подписка на QiChangedEvent.
- [x] C7. **Панель слотов техник (нижняя панель):** 7 ячеек (3-9). Drag&drop техники из списка ИЛИ кнопка «Установить в слот N». Подписка на TechniqueSlotAssignedEvent/ClearedEvent. ЛКМ по занятому слоту = очистка.
- [x] C8. **Регистрация в DI:** `PlayerModuleServices` регистрирует TechniqueSlotService как `ISaveable` (forwarding в ContainerBuilder даёт тот же singleton для concrete типа); `GameWorldController` инстанцирует CultivationWindow в HUD.
- [ ] C9. **Save/Load слотов:** TechniqueSlotService реализует ISaveable, сериализует словарь slot→techId. ⚠️ **Известный gap:** SaveDataAggregator не вызывает ISaveable автоматически (нужен IEnumerable<ISaveable> wiring в SaveModule или GameBoot). Сейв заработает когда wiring будет починен (вне scope этой сессии).
- [x] C10. **UI подсказки:** тосты при установке/очистке слота, при открытии окна.

**Точка восстановления C:** ✅ `dotnet build` — 0 errors, 271 warnings (+5 от новых файлов, все CS0649/CS0414 [Inject] property-injection). git commit pending.

---

## Этап D — Система горячих клавиш

**Требования пользователя:**
- `1` — оружие ближнего боя (зарезервирована)
- `2` — оружие дальнего боя (зарезервирована)
- `3`–`9` — техники (из слотов CultivationWindow)
- Пояс: `Shift+цифра` ИЛИ `Ctrl+цифра` (выбираем **Shift+цифра** как каноничный, Ctrl как альтернатива для совместимости с существующими хоткеями)

### Текущее состояние (предварительная оценка, подтвердить в D1)
- `InputAdapter` обрабатывает: `1`-`9` (пояс, см. `BeltService`), `Z` (каст выбранной техники), `X` (цикл техник), `V` (медитация), `T` (панель техник), `I` (инвентарь), `E` (диалог), `Space` (атака)
- `HotbarPanel` — HUD с 9 слотами (текущий хотбар пояса 1-9)
- `BeltService` — 7 слотов пояса (belt-gated)

### Конфликт хоткеев
Текущие `1`-`9` → пояс. Новое требование: `1`-`2` → оружие, `3`-`9` → техники. **Пояс переезжает на Shift+цифра.**

### Шаги внедрения

- [x] D1. **Аудит InputAdapter + InputMapInitializer:** ✅ (см. A11). Найдено: `hotbar_1..9` без Shift; `C` = character_sheet (конфликт с CultivationWindow, выбрана клавиша **K**).
- [x] D2. **InputMapInitializer:** добавлено `cultivation_window` (Key.K). Существующие `hotbar_1..9` оставлены — теперь они обрабатываются с учётом Shift state в InputAdapter.
- [x] D3. **InputAdapter routing:** в `_PhysicsProcess` добавлена логика:
  - `bool shiftHeld = IsKeyPressed(Key.Shift)`
  - Цикл `hotbar_1..9` → если Shift → `hotbarSlot = i` (попадает в `InputFrameData.HotbarSlot` → BeltService как раньше); иначе `1` → sticky `weapon_melee`, `2` → sticky `weapon_ranged`, `3..9` → sticky `technique_slot_{i}`
  - `cultivation_window` → sticky `cultivation_window`
- [x] D4. **IPlayerInputService + PlayerInputService:** добавлены свойства `IsCultivationWindowPressed`, `IsWeaponMeleePressed`, `IsWeaponRangedPressed`, `TechniqueSlotIndex` (int 3-9, 0 = не нажато). `ResetFrameFlags` сбрасывает все новые флаги.
- [x] D5. **GameWorldController.HandleStickyInput:** добавлены ветки:
  - `IsCultivationWindowPressed` → `_cultivationWindow.Toggle()` (НЕ паузит игру — окно справочное)
  - `IsWeaponMeleePressed` → тост «Слот 1: оружие ближнего боя (зарезервировано)» (weapon switching system pending)
  - `IsWeaponRangedPressed` → тост «Слот 2: оружие дальнего боя (зарезервировано)»
  - `TechniqueSlotIndex is int N (3-9)` → `TechniqueSlots.GetTechniqueAtSlot(N)` → если есть техника → `TechniqueCastPub.Publish(TechniqueCastRequestedEvent(techId, mouseX, mouseY))`; иначе тост «Слот N: пусто»
- [x] D6. **Проверка архитектуры:** Hub-and-Spoke соблюдён — Adapter (InputAdapter) → PlayerInputService (Module) → GameWorldController (Adapter scene) → EventBus → PlayerTechniqueCaster. Нет прямых вызовов сервисов из InputAdapter.

**Точка восстановления D:** ✅ `dotnet build` — 0 errors, 271 warnings (без изменений от Stage C). git commit pending.

---

## Этап E — Верификация

- [ ] E1. `dotnet build` — 0 errors, 0 warnings (Stage 0+1 + B + C + D)
- [ ] E2. `GODOT_NEWGAME=1` — headless newgame: 19 startables / 18 tickables, техники выданы, без исключений
- [ ] E3. `GODOT_CHARGE_SIM=1` — зарядка → аура → выпуск работает (без регрессии)
- [ ] E4. `GODOT_COMBAT_SIM=1` — бой работает (без регрессии)
- [ ] E5. `GODOT_TRADE_DEBUG=1` — торговля работает (без регрессии)
- [ ] E6. Xvfb + opengl3 + скриншот — визуальная верификация CultivationWindow (3 вкладки + панель слотов)
- [ ] E7. Agent Browser — открыть окно (C), переключить вкладки, установить технику в слот 3, нажать 3 → каст техники

---

## Этап F — Документация и пуш

- [ ] F1. Обновить `docs_v2/02_systems/TECHNIQUE_SYSTEM.md` — новая §13 «Слоты быстрого доступа техник» (3-9)
- [ ] F2. Обновить `docs_v2/02_systems/QI_SYSTEM.md` — ссылка на CultivationWindow
- [ ] F3. Создать `docs_v2/03_ui/CULTIVATION_WINDOW.md` — UI-спецификация (вкладки, слоты, хоткеи)
- [ ] F4. Создать `docs_v2/03_ui/HOTKEY_SYSTEM.md` — таблица хоткеев (1-9, Shift+1-9, Z X C V T I E Space)
- [ ] F5. Обновить `SESSION_SUMMARY.md` и `worklog.md`
- [ ] F6. Git commit + push в origin/main (с токеном)

---

## Журнал прогресса (append-only, обновлять после каждого этапа)

| Время MSK | Этап | Статус | Commit |
|-----------|------|--------|--------|
| 09:04 | A1-A7 | ✅ завершён (чтение + фиксация проблем) | — |
| — | A8-A10 | pending | — |
| — | B1-B6 | pending | — |
| — | C1-C10 | pending | — |
| — | D1-D6 | pending | — |
| — | E1-E7 | pending | — |
| — | F1-F6 | pending | — |

---

## Точка восстановления (для следующего запуска)

Если среда упала — следующий запуск:
1. Прочитать этот файл (чекпоинт-план)
2. Найти последнюю `[x]` в журнале прогресса
3. Продолжить со следующего `[ ]` этапа
4. Перед продолжением — `git status` + `git log --oneline -5` чтобы сверить HEAD с ожидаемым

**Ожидаемый HEAD на старте:** `f54ebd7` (если не было коммитов в этой сессии)
**Финальный ожидаемый HEAD:** новый коммит после F6

---

*План составлен в основном потоке без субагентов. Все правки — через Read/Write/Edit. После каждого этапа — обновление этого файла и git commit.*
