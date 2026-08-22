# Сводка сессий (обновляется при завершении каждой сессии)

Обновлено: 2026-08-22 22:30 UTC (локальный ZCode-агент, Windows)

## Проект
Cultivation World Simulator (Ai-game4), Godot 4.7.1 .NET, C#
Репозиторий: https://github.com/vivasua-collab/Ai-game4

---

## Последние сессии

### 2026-08-22 (вечер/ночь — локальный ZCode, 9 коммитов 11e495d..1549919)

#### Играбельный прототип физической части (главное)
- **Диспозиционный ИИ**: NPCDisposition (Hostile/Friendly/Neutral/Merchant) по роли; Hostile агро на игрока в 5 тайлах; Friendly (страж) вступается за игрока; Merchant стоит на месте; TargetId = argmax threat (фикс «атаки в никуда»)
- **NPC-атаки**: NPCModule.ProcessNpcAttacks — AttackIntentEvent(npc, target, "npc_strike") раз в 1.6 сек при дистанции ≤2 → полный damage pipeline
- **Экипировка NPC из «Матрёшки»**: оружие всем (Enemy +1 уровень), броня 60%; TotalDamage += сгенерированный урон
- **Лут**: смерть NPC → 1-2 предмета EquipmentGenerator → на землю (подбор E)
- **Игрок**: HP-бар в HUD (Σ RedHP, Q4), тост «💥 −X HP», смерть → респавн 3с (полное лечение + центр карты)
- **Состав локации**: 2 Enemy (бандиты, без диалога) + 1 Guard + 2 Passerby + 1 Merchant
- **Коммит:** 1549919

#### Генератор экипировки «Матрёшка»
- EquipmentGenerationTables (Core/Data): 7 оружий (§10.2), 6 броней (§11.2), 14 материалов (§5), GradeProfiles (§4.1), 5 зачарований (§8)
- IEquipmentGenerator + EquipmentGenerator (Modules/Generator): формулы §2, бонусы §7.3, зачарование §8.4, ItemId без коллизий; debug-dump в GODOT_GEN_DEBUG
- Закрыты 4/5 проблем NPC_COMBAT_PREP §8 (variety, slots, penetration, id-collision)
- **Коммит:** 2532be9

#### Слоты быстрого запуска (пояс)
- BeltService (7 слотов = хотбар 3-9), гейт по EquipmentSlot.Belt; HotbarPanel на HUD (1-2 оружие); BeltSlotRow в инвентаре (drag&drop расходников, ПКМ — вернуть); клавиши 3-9/click — использование (heal → BodyService, qi_restore → QiService)
- Контракты: BeltSlotsChangedEvent, ConsumableUsedEvent
- **Коммит:** 8fbb18b

#### P0 NPC_COMBAT_PREP (Phase 1/2/6)
- Phase 1: HumanNPCSpawnPhase + NPCSpriteRenderer (d7837e8); тест-хук GODOT_NEWGAME=1 (headless полный флоу)
- Phase 2: DialogueWindow + роль-диалоги + E-key (d89fed2)
- Phase 6: PlayerCombatAdapter full + выбор цели + Space (f0ee6fb)

#### Багфиксы по playtest
- Скорость: TimeSpeedChangedEvent публикуется (раньше никто), toast «⏩ Скорость», Day/Month/Year контракты (11e495d)
- Зависание после диалога: Resume через DialogueEndedEvent — единая точка (219b3fc)
- Скорость игрока: умеренный множитель Normal ×1 / Fast ×2 / Quick ×3.5 + камера (Q7 уточнён) (00c42bc)
- Зум в инвентаре: modalOpen-гвард на wheel/LMB/middle (00c42bc)
- ДУБЛИРОВАНИЕ экипировки: старый предмет возвращался дважды (UI TryAddItem + событие OldItemId → InventoryModule); убран ручной возврат из CharacterDollPanel (00c42bc)

### 2026-08-22 (утро — облачный агент)

#### Сборка тела + визуализация + животные
- **BodyStatusPanel** — схематическое отображение частей тела через _Draw()
  - Humanoid: голова + торс + сердце + 4 конечности + 2 кисти + 2 стопы
  - Quadruped: голова + торс + сердце + 4 ноги + хвост
  - Bird: голова + торс + сердце + 2 крыла + хвост + 2 ноги
  - Цвет по BodyPartState: Healthy=зелёный, Bruised=жёлтый, Wounded=оранжевый, Disabled=красный, Severed=серый
  - Live updates через BodyPartDamagedEvent/HealedEvent/SeveredEvent
- **CharacterSheetWindow** (hotkey C) — лист персонажа с телом, статами, культивацией
- **Простые животные** на тестовой карте:
  - Wolf (Quadruped/Medium, тёмно-серый, агрессивный)
  - Deer (Quadruped/Medium, коричневый, мирный)
  - Rabbit (Quadruped/Small, белый, быстрый)
  - 5 животных спавнятся при старте (детерминированный SeededRandom)
  - Body assembly через BodyFactory.CreateBody → IBodyDataProvider.SetBodyParts
  - Простое wandering (случайные цели, greedy step)
  - Процедурные спрайты (цветные круги)
- **Коммит:** 86152da

#### 9 аудиторских исправлений (по решениям пользователя)
- Q1: NPCState перенесён в Core/Data (architecture fix)
- Q2: BodyPart перенесён в Core/Data (NPC+player share body system)
- Q3: [Inject] config в 10 модулях (SetConfig never called bug fixed)
- Q4: PlayerService HP делегирует BodyService (единая HP система)
- Q5: Injectable SeededRandom в combat (детерминированный)
- Q6: Weight tables перенесены в Core/Data/GeneratorTables.cs (break NPC↔Generator cycle)
- Q7: Movement real-time без Time.Speed (фикс экстремальной скорости)
- Q8: Save/load отключён (сейвы невалидны во время разработки)
- Q9: Spirit + Ring storage разделены (per docs)
- Q13: EventBus re-entrancy queue (защита от StackOverflow)
- **Коммит:** a510aa3

#### 4 аудита с подробными описаниями
- AUDIT-1: Core layer (33 issues — 3 critical, 11 major, 19 minor)
- AUDIT-2: Modules layer (48 issues — 4 critical, 14 major, 30 minor)
- AUDIT-3: Entry + Adapter (47 issues — 5 critical, 14 major, 28 minor)
- AUDIT-4: Docs compliance (2 P0 formula mismatches fixed, 4 conceptual questions)
- 12 auto-fixable исправлений применено
- **Коммиты:** 5d51a9f, b3ccc28

### 2026-08-21

#### Ground item system
- При превышении объёма инвентаря предметы выпадают на землю
- Корзина (🗑) в инвентаре для выбрасывания
- E key — подбор ближайшего предмета
- Процедурные спрайты для предметов на земле (8 категорий)
- **Коммит:** a792a5b

#### Overweight system
- Перевес по весу разрешён (предметы всегда попадают в инвентарь)
- Штраф к скорости: 0.25×-1.0× в зависимости от overload ratio
- Toast уведомление при перевесе
- Weight label: красный при перевесе, золотой при >80%
- **Коммит:** 5c5ae08

#### 8 issues fixed
- Double-click equip, wheel zoom в инвентаре, pause на inventory open
- NEAREST texture filter (фикс сетки между тайлами)
- Все 9 биомов на большой карте (moisture → Steppe/Forest)
- Harvest добавляет ресурсы в инвентарь + refresh UI
- Объекты исчезают при исчерпании (Object=None, RefreshObjectLayer)
- Все объекты имеют ресурсы (Bush=fiber, Rock_Large=stone, OreVein=iron)
- **Коммит:** 43fadfa

### 2026-08-20

#### Mouse input scheme
- LMB над UI = drag items, LMB над world = move player
- _UnhandledInput вместо polling (уважает Godot input propagation)
- MouseFilter.Stop на всех UI элементах
- Zoom перенесён в _UnhandledInput (не срабатывает в инвентаре)
- **Коммит:** 7a0c0de

### 2026-08-19

#### NPC + Combat prep
- 3 параллельных аудита: NPC (5-a), Combat (5-b), Generators (5-c)
- Backend NPC 90% готов (stub: NPCSpawnPhase, NPCVisualService)
- Backend Combat 85% готов (stub: PlayerCombatAdapter, target selection)
- Generators работают (verified via GODOT_GEN_DEBUG=1)
- План: 9 phases, ~5110 LOC
- **Коммит:** 2f977e2

#### Performance + 500×500 LargeWorld
- Viewport culling (1736× reduction in draw calls)
- SmoothBiomes fix (Dictionary→int[16] array, 0 allocs)
- LargeWorld scene (500×500, all 9 biomes)
- MainMenu: 2 кнопки (50×50 + 500×500)
- **Коммит:** a0a4fc3

#### Environment + harvest
- Деревья (oak/pine/birch), камни, кусты, руда, травы
- Процедурные спрайты (placeholder)
- F key добыча (Mode A gradual depletion)
- Toast feedback
- **Коммит:** 57ce2e4

#### Character doll + inventory
- CharacterDollPanel (11 слотов экипировки)
- Drag&drop equip/unequip
- 17 тестовых предметов + 6 материалов
- **Коммит:** 1f4f167

### 2026-08-15 (начальная миграция)
- Перенос Core (85 файлов) + Modules (141 файл) из Ai-game3
- Адаптация MessagePipe→EventBus, VContainer→DI, UniTask→Task
- 0 errors, все 16 модулей стартуют

---

## Текущее состояние игры

### Что работает
- ✅ Main menu → New Game (50×50) / Large World (500×500) + GODOT_NEWGAME=1 headless-хук
- ✅ World generation (noise-based, 9 biomes, viewport-culled rendering)
- ✅ Environment objects + Harvest (F) + ground items (E)
- ✅ Inventory (B) + Character doll + двойной клик (БЕЗ дублирования — фикс)
- ✅ Character Sheet (C) — силуэт тела + HP + статы
- ✅ Body assembly + визуализация + животные (wolf/deer/rabbit)
- ✅ **Гуманоиды-NPC**: 6 на локации (2 Enemy + Guard + 2 Passerby + Merchant), спавн через полный пайплайн, рендер кругами по ролям
- ✅ **Диспозиционный ИИ**: агро/защита/блуждание/лавка; NPC-атаки через damage pipeline
- ✅ **Диалоги**: E возле NPC → DialogueWindow (typewriter, выборы 1-4/E/Esc), пауза на диалог с корректным resume
- ✅ **Бой**: Space — атака игрока по ближайшему NPC (2.5 тайла); HP-бар игрока; тосты урона; смерть игрока → респавн
- ✅ **Лут**: смерть NPC → 1-2 предмета на землю, подбор E
- ✅ **Генератор «Матрёшка»**: 7 оружий × 6 бронь × 14 материалов × 5 грейдов × зачарования (GODOT_GEN_DEBUG=1 — дамп)
- ✅ **Слоты пояса**: 7 слотов (хотбар 3-9), гейт по поясу, drag&drop из инвентаря, использование (heal/qi)
- ✅ Скорость: PageUp/PageDown с тостами + ускорение игрока (Fast ×2, Quick ×3.5)
- ✅ Overweight, EventBus re-entrancy, детерминированный combat RNG

### Что НЕ работает (отложено)
- ❌ Урон NPC→игрок по частям тела — проверить живьём (pipeline готов, headless не симулирует бой)
- ❌ Combat visuals (HP-бары над NPC, цифры урона — Phase 7)
- ❌ Trade (окно у торговца — Phase 4-5)
- ❌ Faction system (portable из Ai-game3-ref — Phase 3)
- ❌ Weapon variety в луте/экипировке игрока — интеграция StatBonuses в CombatService (5 TODO, Phase 8)
- ❌ Cultivator technique cap=0 (баг TechniqueGeneratorService)
- ❌ Духовная часть (культивация-геймплей), глобальная карта, квесты — не трогали по условию
- ❌ Save/load (отключён — Q8)
- ❌ TileMapLayer migration (ЗАПРЕТ 8 — Q11)

---

## Архитектура

### 3-layer (Core / Modules / Entry / Adapter)
- **Core** (engine-agnostic): Data, Interfaces, Messaging/Contracts, DI, Events
  - NPCState и BodyPart перенесены в Core/Data (Q1, Q2)
  - GeneratorTables в Core/Data (Q6)
- **Modules** (16): Body, Qi, Combat, Inventory, NPC, Player, World, Tile, Quest, Interaction, UI, Save, Generator, Buff, Charger, Formation
  - Все модули используют [Inject] для config (Q3)
  - PlayerService HP делегирует BodyService (Q4)
  - Combat использует ICombatRng (SeededRandom) (Q5)
- **Entry**: GameSession, GameEntryPoint, GameLifetimeScope, Phases (10 phases)
  - AnimalSpawnPhase (заменил NPCSpawnPhase)
- **Adapter** (Godot-specific): Scene (GameBoot, GameWorldController, renderers), UI (Inventory, CharacterSheet, BodyStatus), Input, DI, Persistence

### Key patterns
- Hub-and-Spoke: модули общаются через EventBus
- DI: custom ContainerBuilder + [Inject] attribute
- Zero-GC: IPublisher<T>.Publish(in T) — in parameter
- Tick-based sim: 1 tick = 1 game minute, speeds: Paused(0), Normal(1), Fast(5), Quick(15)

---

## Замороженные решения (НЕ нарушать)

- Godot 4.7.1 .NET — единственный движок
- Чистый 2D (без 2.5D на v1)
- Qi = long (не float) — ЗАПРЕТ 2
- Integer math для combat (Permil) — ЗАПРЕТ 3.9
- config/name без пробелов ("CultivationGame")
- Input actions регистрируются программно
- Constructor injection поддерживается DI Container
- **Документация первична** — НЕ редактировать без прямого указания пользователя
- "Не реализовано" — будет реализовано позже (начальный этап, отладка ядра)
- Custom _Draw для рендеринга (TileMapLayer отложен — Q11)
- Element.Poison остаётся (Q10)
- ItemCategory без QiStone (Q12)
- GameTile mutable struct (Q14 — отложено)
- Save/load отключён (Q8 — сейвы невалидны во время разработки)

---

## Концептуальные решения пользователя (14 ответов)

| Q | Решение | Описание |
|---|---------|----------|
| Q1 | A | NPCState в Core/Data |
| Q2 | A | BodyPart в Core/Data (NPC+player одинаково) |
| Q3 | A | [Inject] config в модулях |
| Q4 | A | PlayerService HP делегирует BodyService |
| Q5 | A | Injectable SeededRandom (детерминированный combat) |
| Q6 | A | Weight tables в Core (break NPC↔Generator cycle) |
| Q7 | B | Tick-based movement (убран Time.Speed multiplier) |
| Q8 | A | Save/load отключён |
| Q9 | B | Spirit + Ring storage разделены |
| Q10 | A | Element.Poison оставить |
| Q11 | A | _Draw оставить (TileMapLayer отложен) |
| Q12 | A | ItemCategory без QiStone |
| Q13 | A | EventBus re-entrancy queue |
| Q14 | A | GameTile readonly — отложить |

---

## Следующие шаги

### P0 — проверить живьём (сделано локально, не проверено в бою)
1. Урон NPC→игрок через pipeline (бандит бьёт игрока: HP-бар, тосты, смерть/респавн)
2. Дружественный страж вступается за игрока (Friendly → threat врагу)
3. Лут-дроп при смерти NPC + подбор
4. Слоты пояса end-to-end (надеть пояс → drag&drop пилюли → клавиша 3)

### P1 — по плану NPC_COMBAT_PREP.md
5. Phase 7: Combat Visuals (HP-бары над NPC, DamageNumber)
6. Phase 4-5: Trade (торговец уже на локации с диалогом)
7. Phase 3: Faction Port
8. Phase 8: Weapon Variety wiring (5 TODO CombatService ← StatBonuses «Матрёшки», генератор готов)

---

## Команды проверки

```bash
# Build
cd /home/z/my-project/aigame4/game && dotnet build

# Headless test (50×50)
/home/z/godot/Godot_v4.7.1-stable_mono_linux_x86_64/Godot_v4.7.1-stable_mono_linux.x86_64 --headless --path . scenes/GameWorld.tscn

# Large world test (500×500)
GODOT_MAP_SIZE=500 /home/z/godot/Godot_v4.7.1-stable_mono_linux_x86_64/Godot_v4.7.1-stable_mono_linux.x86_64 --headless --path . scenes/GameWorld.tscn

# Generator debug
GODOT_GEN_DEBUG=1 /home/z/godot/Godot_v4.7.1-stable_mono_linux_x86_64/Godot_v4.7.1-stable_mono_linux.x86_64 --headless --path . scenes/GameWorld.tscn

# Git
cd /home/z/my-project/aigame4 && git log --oneline -5
```

---

## Предупреждения

- Next.js DEV сервер НЕ запускать (это sandbox, не игра)
- worklog.md большой (~2500+ строк) — читать последние записи
- Ai-game3-ref — reference only (не коммитить в него)
- .gitignore: правило `game` исправлено на `/my-project/` (commit 1f4f167)
- Biome sprites missing (biome_ocean.png etc.) — нужен Godot Editor для импорта .ctex
- DOTNET_ROOT должен быть установлен для Godot headless
