# Чекпоинт: NPC_COMBAT_PREP реализация (локальный агент ZCode)

**Дата:** 2026-08-22
**Автор:** локальный ZCode (GLM-5.3), Windows, Godot 4.7.1 mono
**Источник плана:** docs/docs_v2/09_workflow/NPC_COMBAT_PREP.md

---

## FIX-1: Реакция на изменение скорости времени (коммит 11e495d)

**Проблема:** после интеграции тиковой системы (Q7) смена скорости PageUp/PageDown не давала реакции — контракт `TimeSpeedChangedEvent` существовал, но никто его не публиковал; лог смены скорости был закрыт `#if DEBUG_SPEED_LOG`.

**Решение:**
- `TimeService` (WorldService.cs): Speed теперь свойство с сеттером → публикует `TimeSpeedChangedEvent` через EventBus при каждом изменении (вкл. Pause/Resume).
- `WorldModule.Tick`: публикует `TimeChangedEvent` каждый тик + `DayChangedEvent`/`MonthChangedEvent`/`YearChangedEvent` при смене календарной даты (по MODULE_STRUCTURE §WorldContracts).
- `GameWorldController`: toast «⏩ Скорость: …» при PageUp/PageDown (видимая реакция), helper `SpeedLabel()`.

**Проверка:** dotnet build 0 ошибок; headless-запуск чистый.

## Phase 1: NPC Spawn + Render (коммит d7837e8)

**Новые файлы:**
- `Entry/Phases/HumanNPCSpawnPhase.cs` (PhaseOrder 6) — спавнит 4 людей (Merchant lvl1, Cultivator lvl3, Guard lvl2, Passerby lvl0) через полный пайплайн `NPCSpawnerService.SpawnNPC("human", role, level, pos, seed)`. Детерминированные seed (loc.Seed + 104729 + role), поиск walkable-тайлов с мин. дистанцией 5 от центра.
- `Adapter/Scene/NPCSpriteRenderer.cs` — цветные круги по роли (merchant=teal, cultivator=violet, guard=blue, elder=gold, monster=red), ZIndex=Objects(3), по образцу AnimalSpriteRenderer.

**Изменено:**
- `SceneAssemblyRegistrar`: регистрация HumanNPCSpawnPhase.
- `SceneBuilder`: `SetupNPCs()` → NPCSpriteRenderer в world root.
- `MainMenuController`: тестовый хук `GODOT_NEWGAME=1` — автостарт новой игры для headless-проверки полного флоу сборки (в семействе GODOT_MAP_SIZE / GODOT_GEN_DEBUG).

**Проверка (headless, `GODOT_NEWGAME=1 scenes/MainMenu.tscn`):**
```
[HumanNPCSpawn] Spawned Merchant #npc_bb2c1c6d11544fce at (2, 34)
[HumanNPCSpawn] Spawned Cultivator #npc_33dcc2c369c247b6 at (30, 35)
[HumanNPCSpawn] Spawned Guard #npc_4f9dd6aee6294a1c at (43, 48)
[HumanNPCSpawn] Spawned Passerby #npc_548eaec877764a3f at (13, 48)
[Phase 6] HumanNPCSpawn complete — 4/4 NPCs on 'test_polygon'
[GameSession] NewGame ready — state=Playing
```

**Заметки:**
- NPCData.Position — в тайлах (int), НЕ в милли-тайлах; NPCData.Role доступен напрямую.
- Прямой запуск GameWorld.tscn НЕ вызывает scene-assembly фазы (NewGame вызывается только из MainMenu) — тестировать полный флоу через GODOT_NEWGAME=1 + MainMenu.tscn.
- NPCVisualService (Modules) остаётся no-op стабом — рендер в Adapter-слое (правильно по архитектуре).

## Phase 2: Test Chat / Dialogue UI (коммит d89fed2)

**Новые файлы:**
- `Adapter/UI/DialogueWindow.cs` — нижняя панель диалога (пергамент): имя NPC, typewriter-текст (CurrentDisplayText), кнопки вариантов 1..4 (клик или цифры), E — далее, Esc — закрыть. Окно не блокирует ввод игры (MouseFilter.Ignore на корне, Stop на панели).

**Изменено:**
- `DialogueService`: 3 новых дефолтных диалога (dialogue_guard / dialogue_cultivator / dialogue_passerby) + `TryStartNpcDialogue(npcId)` (поиск по карте NPC→dialogue без знания dialogueId).
- `HumanNPCSpawnPhase`: привязка диалога по роли при спавне (`MapNpcDialogue`).
- `GameWorldController`: E-key — приоритет диалогу (если открыт → Advance; если NPC в 2.5 тайлах → старт диалога + пауза тиков; иначе подбор предмета). Esc при открытом диалоге — закрыть + снять паузу.

**Проверка:** headless GODOT_NEWGAME=1 — 4/4 NPC, DialogueWindow Ready, state=Playing, 0 ошибок.

## Phase 6: Combat Activation (коммит см. ниже)

**Изменено:**
- `PlayerCombatAdapter` (полная реализация, был 74-строчный стаб): выбор цели — ближайший живой NPC в радиусе 2.5 тайлов (Chebyshev) от игрока; Space → `AttackIntentEvent(playerId, targetId, "basic_attack", false)`. CombatModule (мост Фаза 9D) сам стартует бой и запускает 11-слойный damage pipeline. Инжект INPCService — санкционированный прецедент NPCCombatAdapter.
- `PlayerModule.Register`: регистрация PlayerCombatAdapter (Singleton).
- `GameWorldController`: `CombatAdapter.Start()` в _Ready; `CombatAdapter.Tick(delta)` в _Process (до ResetFrameFlags, PLR-E06); toast «⚔ Атака!».

**Не входило (per plan):** 5 TODO экипировки в CombatService (penetration/dodge/parry/shield/crit) — заблокированы пустым IItemDatabaseService, запланированы в Phase 8 (Weapon Variety). Combat visuals — Phase 7.

**Проверка:** build 0 ошибок; headless GODOT_NEWGAME=1 — state=Playing, 0 ошибок. CombatModule._isConfigured=true (Start устанавливает) — AttackIntentEvent обрабатывается.

## Следующие шаги

- Phase 7: Combat Visuals (DamageNumber, HP-бары NPC).
- Phase 8: Weapon Variety + Ammo (+ 5 TODO экипировки в CombatService, генераторы penetration/dodge).
- Phase 3-5: Faction port, Trade foundation + UI.
- Live playtest: Space у NPC, диалоги, скорость PageUp/PageDown, тосты.

---

## Багфиксы по playtest-отчёту пользователя (2026-08-22, позднее)

### FIX-2: Зависание после диалога (коммит 219b3fc)
Диалог ставил паузу при открытии, но Resume был только в ветках E/Esc. Завершение кликом по выбору («Прощай») закрывало окно без снятия паузы → движение мертво (HandleFreeMovement: `if (Time.IsPaused) return`). Фикс: единая точка — подписка GameWorldController на DialogueEndedEvent (все пути завершения).

### FIX-3: Скорость игры не влияла на игрока
Q7 убрал Time.Speed из движения полностью — игрок «отставал» от ускорившегося мира. Решение: умеренный множитель вместо линейного (который давал экстремальные скорости): Normal ×1.0, Fast ×2.0, Quick ×3.5 + PositionSmoothingSpeed камеры масштабируется тем же множителем. Q7 уточнён по просьбе пользователя.

### FIX-4: Зум колесом при открытом инвентаре
ScrollContainer потребляет колесо только пока список прокручивается; на упоре событие проваливалось в _UnhandledInput и меняло зум. Фикс: modalOpen-гвард (инвентарь/лист персонажа/диалог) на LMB/Wheel/Middle в _UnhandledInput.

### FIX-5: Дублирование экипировки (критичный)
Старый предмет возвращался в инвентарь ДВАЖДЫ: вручную в CharacterDollPanel (TryAddItem) + событийно (EquipmentChangedEvent.OldItemId → InventoryModule.OnEquipmentChanged, INV-B05/P1-02). Спам двойным кликом плодил копии «Стального нагрудника». Фикс: убрать ручные TryAddItem из HandleDropOnSlot (замена), 2H-ветки и HandleUnequip — событийный путь канонический. Overflow-безопасность: TryAddItem дропает излишек на землю.

**Проверка:** build 0 ошибок; headless GODOT_NEWGAME=1 — state=Playing, 0 ошибок.

---

## Слоты быстрого запуска пояса (2026-08-22, вечер)

**Источники:** HOTKEYS.md §8 (хотбар 1-9), UI_DESIGN.md §6.1 View #3, запрос пользователя (слоты 3-9 гейтятся поясом).

**Новые файлы:**
- `Core/Messaging/Contracts/BeltContracts.cs` — BeltSlotsChangedEvent, ConsumableUsedEvent.
- `Modules/Inventory/BeltService.cs` — 7 слотов (хотбар 3-9), гейт IsBeltEquipped (EquipmentSlot.Belt), TryAssign (весь стек из инвентаря), Use (эффекты: heal → BodyService.HealPart по самым раненым частям; qi_restore → QiService.AddQi; прочие — заглушка до будущих фаз), TryTakeBack; при снятии пояса содержимое возвращается в инвентарь (overflow → на землю).
- `Adapter/UI/HotbarPanel.cs` — HUD-хотбар внизу по центру: 1-2 оружие (зеркало экипировки), 3-9 пояс (видны только при поясе), клик = использовать.
- `Adapter/UI/BeltSlotRow.cs` — ряд слотов пояса в InventoryWindow: drag&drop расходника (весь стек), ПКМ — вернуть; видимость по поясу.

**Изменено:**
- `InventoryModuleServices/InventoryModule`: регистрация + Initialize (подписка на EquipmentChangedEvent для гейта).
- `GameWorldController`: клавиши 1-9 (hotbar_i уже в InputMap) → слоты 3-9 = BeltService.Use + toast; HotbarPanel в HUD.

**Паттерн Godot:** встроенного хотбара нет; канонический подход (форум/туториалы) — Control drag&drop API (_GetDragData/_CanDropData/_DropData) + контейнеры + input actions — реализация следует ему.

**Проверка:** build 0 ошибок; headless GODOT_NEWGAME=1 — HotbarPanel Ready, Inventory Ready, state=Playing.

---

## Генератор экипировки «Матрёшка» (2026-08-22, поздний вечер)

**Источник:** EQUIPMENT_SYSTEM.md §2 (База × Материал × Грейд × Зачарование), §4-§5, §7-§8, §10-§11.

**Новые файлы:**
- `Core/Data/EquipmentGenerationTables.cs` — слой таблиц:
  - WeaponBaseClass (7 подтипов §10.2: dagger/sword/axe/spear/greatsword/bow/staff — база урона/пробития/дальности/веса, класс скорости, HandType);
  - ArmorBaseClass (6 подтипов §11.2: head/torso/arms/hands→нейминг arms, legs/feet/belt — слот, защита, покрытие min-max, вес, штраф уклонения);
  - MaterialDef (14 материалов §5.1/§5.3: тиры 1-5, бонусы урона/защиты, множители веса/ценности);
  - GradeProfiles (§4.1 множители эффективности/прочности; §7.3 кол-во/сила бонусов);
  - MaterialDurabilityByTier (§5.1: 35/65/115/275/500);
  - EnchantDefinition (§8.2-8.3: 5 зачарований, тиры, MinGrade, диапазон силы).
- `Core/Interfaces/IEquipmentGenerator.cs` — GenerateWeapon/GenerateArmor/GenerateRandom/TryApplyEnchant.
- `Modules/Generator/EquipmentGenerator.cs` — реализация:
  - Формулы §2: Эффективность = Base × GradeEff × (1+MaterialBonus); Прочность = MaterialDurability × GradeDurMult; Coverage/DamageReduction/dodge/spd-штрафы §11; объём clamp(weight,1,4) §11.5.
  - Тир материала = clamp((level+1)/2,1,5) §5.1; грейд — GeneratorTables.EquipmentGradeWeightsByLevel §4.2.
  - StatBonuses по грейду §7.3 (пул §7.1).
  - Зачарование §8.4: сила × GradeEfficiency, MinGrade-гейт, имя «…«Название»».
  - ItemId eq_wep/arm_{subtype}_L{n}_{seed}_{counter} — фикс коллизий modulo-1000.
  - Регистрирует предметы в IItemDatabaseService.

**Изменено:** GeneratorModuleServices (регистрация IEquipmentGenerator), GeneratorModule (инжект + секция debug dump: все 13 подтипов + зачарование).

**Решено из NPC_COMBAT_PREP §8:** weapon variety ✅, armor slots variety ✅, penetration≠0 ✅, dodge≠0 ✅, ItemId collision ✅. Осталось: Cultivator technique cap=0 (отдельный баг TechniqueGenerator), интеграция в CombatService 5 TODO (Phase 8 wire).

**Проверка (GODOT_GEN_DEBUG=1 headless):** 7 видов оружия (разные dmg/pen/range/hand), 6 видов брони (разные слоты/покрытие/штрафы), зачарование «Удача» наложено. 0 ошибок.

**Скелет без (будущие фазы):** сетовые бонусы §9, даруемые техники §12, износ/ремонт §6 (только MaxDurability), ammo для лука (Phase 8), интеграция в лут/NPC (замена вызовов ItemGeneratorService на EquipmentGenerator по мере фич).

---

## Играбельный прототип физической части (2026-08-22, ночь)

**Цель:** подключение генератора + простой ИИ гуманоидов + враги/союзники/нейтралы/торговцы на малой локации. Духовная часть/глобальная карта/квесты — вне скоупа.

### Этап 1 — Диспозиционный ИИ
- `NPCDisposition` (Enums): Hostile / Friendly / Neutral / Merchant; `NPCState.Disposition`; назначение при спавне (Enemy+Monster→Hostile, Guard→Friendly, Merchant→Merchant, прочие→Neutral).
- `NPCAIService.ProcessDisposition`: Hostile — игрок в AggroRadius (5 тайлов) → мгновенная угроза (агро); Friendly — враг в бою с игроком в 2×AggroRadius → угроза врагу (союзник вступается). При Attacking назначается TargetId = argmax threat (раньше цель не ставилась — NPC «атаковал» в никуда).
- `NPCModule.ProcessNpcAttacks`: атакующий цикл — NPC в Attacking с целью в 2 тайлах публикует AttackIntentEvent(npc, target, "npc_strike") с кулдауном 1.6 сек → полный damage pipeline CombatService (статы NPC через StatProviderAdapter, статы игрока для любых не-NPC id).
- Merchant стоит на месте (Idle), не блуждает.

### Этап 2 — Экипировка NPC из «Матрёшки»
- `NPCSpawnerService.EquipFromGenerator`: оружие всем (Enemy — уровнем выше), броня 60% (torso/head); `EquipmentIds[slot] = itemId` (предмет зарегистрирован генератором в ItemDatabase); TotalDamage = BaseDamage + сгенерированный урон оружия.

### Этап 3 — Лут
- `NPCModule.OnNPCDeathForLoot` (NPCDeathEvent): 1-2 предмета из EquipmentGenerator → GroundItemService.DropItem у места смерти; подбор E.

### Этап 4 — Игрок
- HP-бар в HUD (Σ RedHP частей, Q4), цвет зелёный→красный.
- Тост «💥 −X HP» при DamageAppliedEvent по игроку.
- Смерть: PlayerDeathEvent → тост → 3 сек → полное лечение частей + Revive + телепорт в центр («✦ Вы возродились»).

### Этап 5 — Состав малой локации
- HumanNPCSpawnPhase: Enemy×2 (бандиты, без диалога), Guard×1 (союзник), Passerby×2, Merchant×1 (торговец). Seed сдвинут на номер спавна (2 Passerby → разные позиции).

**Проверка:** build 0 ошибок; headless GODOT_NEWGAME=1: 6/6 NPC (2 Enemy, 1 Guard, 2 Passerby, 1 Merchant), state=Playing, 0 ошибок.

**Живой тест-сценарий:** подойти к бандиту (красный не подойдёт — цвет по роли: Enemy=оранжевый) в 5 тайлах → он атакует (движется, бьёт раз в 1.6с, тосты урона, HP-бар падает); Space — ответный удар; убить → лут на земле (E — подбор); страж рядом вступается; смерть игрока → респавн в центре.
