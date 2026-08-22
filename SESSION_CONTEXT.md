# SESSION_CONTEXT — Передача контекста облачному агенту

**Дата создания:** 2026-08-22 10:35 UTC
**Обновлено:** 2026-08-22 22:35 UTC (обратная передача от локального ZCode после сессии прототипа)
**Назначение:** Полный контекст для продолжения разработки
**Инструкция:** Прочитай этот файл ПЕРВЫМ при старте новой сессии

---

## 0. ЧТО СДЕЛАЛОСЬ ЗА ЛОКАЛЬНУЮ СЕССИЮ (9 коммитов, 11e495d..1549919)

Главное: **играбельный прототип физической части** (ИИ + бой + лут + экипировка + слоты пояса). Духовная часть, глобальная карта, квесты — не тронуты (по условию задачи).

1. **Фикс скорости** (11e495d): TimeService публикует TimeSpeedChangedEvent (никто не публиковал!); Day/Month/Year контракты; тосты скорости.
2. **NPC_COMBAT_PREP P0** (d7837e8, d89fed2, f0ee6fb): спавн NPC + рендер + диалоги (E) + бой (Space). Тест-хук GODOT_NEWGAME=1.
3. **Playtest-фиксы** (219b3fc, 00c42bc): зависание после диалога (Resume через DialogueEndedEvent); скорость игрока ×2/×3.5; зум под инвентарём; ДУБЛИРОВАНИЕ экипировки (двойной возврат старого предмета: UI TryAddItem + событие OldItemId→InventoryModule; убран ручной возврат).
4. **Слоты пояса** (8fbb18b): BeltService (хотбар 3-9, гейт по поясу), HotbarPanel HUD, BeltSlotRow в инвентаре, использование heal/qi_restore.
5. **Генератор «Матрёшка»** (2532be9): EquipmentGenerationTables (7 оружий/6 бронь/14 материалов/грейды/зачарования) + EquipmentGenerator. Закрыты 4/5 проблем из NPC_COMBAT_PREP §8.
6. **Прототип** (1549919): диспозиционный ИИ (NPCDisposition), NPC-атаки (AttackIntentEvent цикл), экипировка NPC генератором, лут при смерти, HP-бар, респавн игрока, состав локации 2 Enemy + Guard + 2 Passerby + Merchant.

**Ключевые новые файлы:** Core/Data/EquipmentGenerationTables.cs, Core/Interfaces/IEquipmentGenerator.cs, Modules/Generator/EquipmentGenerator.cs, Modules/Inventory/BeltService.cs, Adapter/UI/{HotbarPanel,BeltSlotRow,DialogueWindow}.cs, Adapter/Scene/NPCSpriteRenderer.cs, Entry/Phases/HumanNPCSpawnPhase.cs.
**Ключевые изменённые:** NPCState (+Disposition), NPCSpawnerService (экипировка из генератора), NPCAIService (диспозиции, TargetId=argmax threat), NPCModule (атакующий цикл, лут), GameWorldController (HP-бар, диалог E, респавн, хотбар-клавиши), CharacterDollPanel (фикс дублирования), DialogueService (роль-диалоги).

---

## 1. БЫСТРЫЙ СТАРТ (прочитать первым)

### Проект
- **Название:** Cultivation World Simulator (Ai-game4)
- **Жанр:** Xianxia cultivation life-sim (Kenshi + RimWorld + cultivation novels)
- **Движок:** Godot 4.7.1 .NET (C#, .NET 9)
- **Рендер:** 2D top-down orthographic, NEAREST texture filter
- **Репозиторий:** https://github.com/vivasua-collab/Ai-game4
- **GitHub токен:** хранится в памяти сессии (не в файлах репозитория)

### Восстановление окружения (cloud sandbox)
```bash
# 1. Clone (token: see worklog or ask user)
cd /home/z/my-project
git clone https://github.com/vivasua-collab/Ai-game4.git
cd Ai-game4 && git remote set-url origin https://github.com/vivasua-collab/Ai-game4.git

# 2. Install .NET SDK 9.0
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 9.0 --install-dir /home/z/.dotnet

# 3. Install Godot 4.7.1
mkdir -p /home/z/godot && cd /home/z/godot
curl -sSL https://github.com/godotengine/godot/releases/download/4.7.1-stable/Godot_v4.7.1-stable_mono_linux_x86_64.zip -o /tmp/g.zip
unzip /tmp/g.zip && chmod +x Godot_v4.7.1-stable_mono_linux_x86_64/Godot_v4.7.1-stable_mono_linux.x86_64

# 4. Symlinks + build
ln -sf /home/z/my-project/Ai-game4 /home/z/my-project/aigame4
ln -sf /home/z/godot /home/z/my-project/godot
cat > /home/z/my-project/Ai-game4/game/NuGet.config << 'EOF'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
EOF
export DOTNET_ROOT=/home/z/.dotnet && export PATH="$DOTNET_ROOT:$PATH"
cd /home/z/my-project/Ai-game4/game && dotnet build

# 5. Verify (headless)
export DOTNET_ROOT=/home/z/.dotnet
/home/z/godot/Godot_v4.7.1-stable_mono_linux_x86_64/Godot_v4.7.1-stable_mono_linux.x86_64 --headless --path . scenes/GameWorld.tscn
```

### Для локального окружения (домашний ПК)
Если Godot и .NET уже установлены локально:
1. `git clone https://github.com/vivasua-collab/Ai-game4.git`
2. `cd Ai-game4/game && dotnet build`
3. Открыть `project.godot` в Godot Editor 4.7.1
4. Запустить сцену `scenes/MainMenu.tscn`

---

## 2. ТЕКУЩЕЕ СОСТОЯНИЕ (коммит 1549919)

### Что работает в игре (дельта за локальную сессию — жирным)
- ✅ **Main Menu** → New Game (50×50) / Large World (500×500)
- ✅ **World generation** — noise-based, 9 биомов (Ocean, Sea, Coast, Grassland, Steppe, Forest, Highlands, Mountains, Peak)
- ✅ **Tile rendering** — viewport-culled, NEAREST filter (нет сетки), 1736× reduction
- ✅ **Environment** — деревья (oak/pine/birch), камни (small/medium/large), кусты, руда, травы
- ✅ **Procedural sprites** — placeholder (нет PNG файлов)
- ✅ **Player movement** — WASD, pixel-based, real-time (не зависит от Time.Speed)
- ✅ **Camera** — zoom 1-8 (wheel), middle = reset, follow player
- ✅ **Inventory (B)** — line model, drag&drop, double-click equip, trash zone (🗑)
- ✅ **Character Doll** — 11 equipment slots, drag&drop equip/unequip
- ✅ **Character Sheet (C)** — body silhouette + HP + stats + cultivation
- ✅ **Body assembly** — BodyService + BodyFactory + 10 templates (Humanoid, Quadruped, Bird, etc.)
- ✅ **Body visualization** — schematic silhouette, 4 morphologies, color by state
- ✅ **Simple animals** — wolf, deer, rabbit (spawn + wander + body parts)
- ✅ **Harvest (F)** — Mode A gradual depletion, toast, object destruction on depletion
- ✅ **Ground items** — overflow drop, pickup (E key), procedural sprites (8 categories)
- ✅ **Overweight** — speed penalty (0.25×-1.0×), toast, color-coded weight label
- ✅ **Generators** — items + techniques (verified via GODOT_GEN_DEBUG=1)
- ✅ **EventBus** — re-entrancy queue (no StackOverflow)
- ✅ **Deterministic combat** — SeededRandom (seed=12345)
- ✅ **Гуманоиды-NPC**: 6 на локации (2 Enemy + Guard + 2 Passerby + Merchant), диспозиционный ИИ (Hostile агро 5 тайлов / Friendly защищает / Merchant лавка)
- ✅ **Бой активен**: Space — атака игрока (цель = ближайший NPC в 2.5 тайла); NPC бьют в ответ ("npc_strike" раз в 1.6с через AttackIntentEvent → полный damage pipeline)
- ✅ **Диалоги**: E возле NPC → DialogueWindow (typewriter, выборы 1-4, Esc), пауза тиков с resume через DialogueEndedEvent
- ✅ **HP-бар игрока** (Σ RedHP, Q4) + тосты «💥 −X HP» + смерть → респавн (3с, полное лечение, центр карты)
- ✅ **Лут**: смерть NPC → 1-2 предмета на землю (подбор E)
- ✅ **Слоты пояса**: HotbarPanel (1-2 оружие, 3-9 пояс при надетом Belt), drag&drop расходников в инвентаре, 3-9/click — использование (heal→BodyService по раненым частям, qi_restore→QiService)
- ✅ **Генератор «Матрёшка»**: EquipmentGenerator — 7 оружий/6 бронь/14 материалов/грейды/зачарования, NPC экипируются им, лут из него
- ✅ **Скорость игры**: PageUp/PageDown (тосты), игрок ускоряется (Fast ×2, Quick ×3.5, камера следом)

### НЕ проверено живьём (сделано headless-only)
- ⚠ Урон NPC→игрок: pipeline готов, но бой не симулировался — ПЕРВОЕ что проверить в редакторе
- ⚠ Страж-союзник вступается (Friendly → threat врагу)
- ⚠ Слоты пояса end-to-end (надеть → drag&drop → клавиша 3)

### Что НЕ работает (отложено, документация НЕ редактируется)
- ❌ NPC spawn (animals есть, но NPC нет)
- ❌ NPC AI (3-tier nervous system)
- ❌ Combat activation (PlayerCombatAdapter stub)
- ❌ Trade system
- ❌ Dialogue/chat UI
- ❌ Faction system
- ❌ Save/load (отключено пользователем — Q8)
- ❌ TileMapLayer migration (отложено — Q11)

---

## 3. АРХИТЕКТУРА

### 3-layer (Core / Modules / Entry / Adapter)

```
game/src/
├── Core/                      ← engine-agnostic (чистый C#)
│   ├── Data/                  ← Constants, Enums, Structs, DataModels
│   │   ├── BodyPart.cs        ← (перенесён из Modules/Body — Q2)
│   │   ├── NPCState.cs        ← (перенесён из Modules/NPC — Q1)
│   │   ├── GeneratorTables.cs ← weight tables (перенесены из NPCConfig — Q6)
│   │   ├── ObjectDefaults.cs  ← 11 ObjectType entries
│   │   ├── GameTile.cs        ← mutable struct (Q14 — отложено readonly)
│   │   ├── TechniqueData.cs   ← formulas per TECHNIQUE_SYSTEM.md
│   │   ├── ValueNoise.cs      ← fBm noise for terrain
│   │   └── SeededRandom.cs    ← deterministic RNG
│   ├── Interfaces/            ← 35+ service interfaces
│   │   ├── IBodyService.cs    ← body management
│   │   ├── IInventoryService.cs ← inventory + IsOverweight + OverweightRatio
│   │   ├── IGroundItemService.cs ← dropped items
│   │   ├── ISpiritStorageService.cs ← (Q9 — разделён из unified StorageService)
│   │   ├── IStorageRingService.cs ← ring storage (long qiCost — ЗАПРЕТ 2)
│   │   ├── ICombatRng.cs      ← (Q5 — deterministic combat RNG)
│   │   └── IPlayerInputService.cs ← +IsHarvestPressed, +IsCharacterSheetPressed
│   ├── Messaging/Contracts/   ← ~130 readonly struct events
│   │   ├── GroundItemContracts.cs ← ItemDroppedEvent, ItemPickedUpEvent
│   │   └── InventoryContracts.cs
│   ├── DI/                    ← ContainerBuilder, Container, InjectAttribute
│   └── Events/                ← EventBus (re-entrancy queue — Q13), IPublisher, ISubscriber
│
├── Modules/                   ← 16 game modules
│   ├── Body/                  ← BodyService (932 LOC), BodyFactory, BodyTemplateProvider (10 templates)
│   ├── Qi/                    ← QiService, meridians, cultivation
│   ├── Combat/                ← CombatService, DamageService (11-layer pipeline)
│   │   ├── CombatRng.cs       ← (Q5 — SeededRandom wrapper)
│   │   └── CombatLootService.cs
│   ├── Inventory/             ← InventoryService, EquipmentService, GroundItemService
│   │   ├── SpiritStorageService.cs ← (Q9 — отделён от Ring)
│   │   └── StorageRingService.cs
│   ├── NPC/                   ← NPCService, AnimalService, AnimalEntity
│   ├── Player/                ← PlayerService (HP делегирует BodyService — Q4)
│   ├── World/                 ← WorldService, locations
│   ├── Tile/                  ← TileService, ResourceService
│   ├── Generator/             ← ItemGeneratorService, TechniqueGeneratorService
│   └── (Buff, Charger, Formation, Quest, Interaction, UI, Save)
│
├── Entry/                     ← GameSession, GameEntryPoint, Phases
│   ├── GameLifetimeScope.cs   ← DI container build (16 modules, Charger at position 14)
│   ├── LocationCatalog.cs     ← TestPolygon (50×50), LargeWorld (500×500)
│   └── Phases/
│       ├── AnimalSpawnPhase.cs ← spawns 3-5 animals (replaced NPCSpawnPhase)
│       └── PlayerSpawnPhase.cs
│
└── Adapter/                   ← Godot-specific
    ├── Scene/
    │   ├── GameBoot.cs        ← autoload, DI container, tick driver
    │   ├── GameWorldController.cs ← movement, camera, input, harvest, pickup, HUD
    │   ├── SceneBuilder.cs    ← biome + transition + object + ground item + animal renderers
    │   ├── ObjectLayerRenderer.cs ← procedural object sprites, viewport culling
    │   ├── GroundItemRenderer.cs ← dropped item sprites (8 categories)
    │   └── AnimalSpriteRenderer.cs ← colored circles per species
    ├── UI/
    │   ├── InventoryWindow.cs ← B key, line model, drag&drop, trash zone, double-click equip
    │   ├── CharacterDollPanel.cs ← equipment slots
    │   ├── CharacterSheetWindow.cs ← C key, body + stats
    │   ├── BodyStatusPanel.cs ← schematic silhouette, 4 morphologies, live HP updates
    │   ├── MainMenuController.cs ← scene selection (50×50 + 500×500)
    │   ├── ParchmentTheme.cs
    │   └── TestItemSeeder.cs ← 23 items (#if DEBUG)
    ├── Input/
    │   ├── InputAdapter.cs    ← sticky keys
    │   └── InputMapInitializer.cs ← key bindings (WASD, E, B, C, F, etc.)
    └── Di/
        └── ContainerAdapter.cs ← property injection bridge
```

### Key patterns
- **Hub-and-Spoke**: модули общаются ТОЛЬКО через EventBus
- **DI**: custom ContainerBuilder + [Inject] attribute (property injection)
- **Zero-GC**: IPublisher<T>.Publish(in T) — in parameter, readonly struct events
- **Tick-based sim**: 1 tick = 1 game minute, speeds: Paused(0), Normal(1), Fast(5), Quick(15)
- **ЗАПРЕТ 2**: Qi = long (не float)
- **ЗАПРЕТ 3.9**: Integer math (Permil) для combat
- **ЗАПРЕТ 8**: TileMapLayer (отложено — Q11, используем custom _Draw)

---

## 4. ПОСЛЕДНИЕ ЗАПРОСЫ ПОЛЬЗОВАТЕЛЯ (контекст переписки)

### Запрос 1: Аудит кода (4 прохода)
> "Проведи аудит кода. У нас будет 3 последовательных аудита в основном потоке ИИ. С каждым следующим проходом аудита расширяй охват кода. После, 4-й проход соответствие кода документации. Правила, документация первична..."

**Выполнено:**
- 4 аудита: Core (33 issues), Modules (48), Entry+Adapter (47), Docs (4 conceptual)
- 12 auto-fixable исправлений (формулы, ЗАПРЕТы, ZIndex, input bugs)
- 14 концептуальных вопросов с подробными вариантами решений

### Запрос 2: Замечания по аудиту + ответы
> "замечания по аудиту, слишком короткие описания!!! я не понимаю варианты решения... Документацию без прямого указания РЕДАКТИРОВАТЬ запрещено!!!! Все с нее будет реализовано позже!!!!"

**Выполнено:**
- Переписаны все 4 аудита с подробными описаниями (что происходит, почему проблема, варианты A/B/C)
- Пользователь дал 14 ответов (Q1-Q14)
- 9 исправлений реализовано (Q1-Q9, Q13)

### Запрос 3: Сборка тела
> "До реализации NPC необходимо реализовать сборку тела. Начнем с гуманоидов и простых животных. Проработай план и начинай внедрение тел. Для начала сделаем основного персонажа. используй схематическое отображение частей тела. Выполни согласно документации. После реализуй простых животных. Добавь их на малую тестовую карту."

**Выполнено:**
- BodyStatusPanel (схематический силуэт, 4 морфологии, цвет по состоянию, live updates)
- CharacterSheetWindow (hotkey C, body + stats + cultivation)
- Простые животные (wolf, deer, rabbit) — spawn + wander + body assembly
- AnimalSpriteRenderer (процедурные спрайты)

### Запрос 4: Передача контекста (текущий)
> "Сейчас работа будет передаваться локальному zcode, необходимо составить для него файл передачи данных и выгрузить его на gitHub."

### Запросы локальной сессии ZCode (2026-08-22, вечер):
> 5. "Исправление, сломалась реакция на изменение скорости при интеграции тиковой системы… запусти аудит и ревю кода, после начинай реализацию фич согласно истории и документации. Полностью автоматически, периодические сохранения в GitHub и чекпоинты." → фикс скорости + P0-фазы NPC_COMBAT_PREP.
> 6. Playtest-баги: зависание после диалога; скорость игрока; зум в инвентаре; дублирование экипировки спамом двойного клика. → все исправлены.
> 7. "Начинаем реализацию слотов быстрого запуска. При одевании пояса должны появляться слоты быстрого доступа на главном экране и в инвентаре возможность поместить расходники в слоты пояса. Использование расходников." → BeltService + HotbarPanel + BeltSlotRow.
> 8. "Начни работу с генератором оборудования и экипировки. На основе документации реализуй скелет генератора." → EquipmentGenerator «Матрёшка».
> 9. "Выполни подключение. Финал — играбельный прототип физической части (духовную часть, глобальную карту, квесты не трогаем). Простой ИИ для гуманоидов: враги, друзья, нейтралы, торговцы. Малая локация." → 5 этапов, коммит 1549919.
> 10. Текущий: обновить контекст-файлы и выгрузить на GitHub.

---

## 5. КОНЦЕПТУАЛЬНЫЕ РЕШЕНИЯ (14 ответов пользователя)

| Q | Решение | Что сделано |
|---|---------|-------------|
| Q1 | A: NPCState → Core | Перенесён в Core/Data/NPCState.cs |
| Q2 | A: BodyPart → Core | Перенесён в Core/Data/BodyPart.cs (NPC+player одинаково) |
| Q3 | A: [Inject] config | 10 модулей обновлены, SetConfig убраны |
| Q4 | A: Делегировать Body | IsAlive через BodyService, BodyCriticalEvent → Die() |
| Q5 | A: SeededRandom | ICombatRng + CombatRng (seed=12345), 6 combat файлов |
| Q6 | A: Weight tables → Core | GeneratorTables.cs, NPC↔Generator cycle устранён |
| Q7 | B: Tick-based movement | Убран `*= Time.Speed` (подготовка для tick-based) |
| Q8 | A: Отключить save/load | F5/F9 закомментированы |
| Q9 | B: Разделить storage | ISpiritStorageService + SpiritStorageService созданы |
| Q10 | A: Оставить Poison | Не требует изменений |
| Q11 | A: Оставить _Draw | Не требует изменений (TileMapLayer отложен) |
| Q12 | A: Оставить ItemCategory | Не требует изменений (QiStone позже) |
| Q13 | A: Queue re-entrant | EventBus: _publishing + _pendingQueue |
| Q14 | A: Отложить readonly | GameTile остаётся mutable struct |

---

## 6. ЗАМОРОЖЕННЫЕ РЕШЕНИЯ (НЕ нарушать)

1. **Godot 4.7.1 .NET** — единственный движок
2. **Чистый 2D** (без 2.5D на v1)
3. **Qi = long** (не float) — ЗАПРЕТ 2
4. **Integer math** для combat (Permil) — ЗАПРЕТ 3.9
5. **config/name** без пробелов ("CultivationGame")
6. **Input actions** регистрируются программно
7. **Constructor injection** поддерживается DI Container
8. **Документация первична** — НЕ редактировать без прямого указания пользователя
9. **"Не реализовано"** — будет реализовано позже (начальный этап, отладка ядра)
10. **Custom _Draw** для рендеринга (TileMapLayer отложен — Q11)
11. **Element.Poison** остаётся (Q10)
12. **ItemCategory** без QiStone (Q12)
13. **GameTile** mutable struct (Q14 — отложено)
14. **Save/load** отключён (Q8 — сейвы невалидны во время разработки)

---

## 7. СЛЕДУЮЩИЕ ШАГИ

### P0 — live-проверка прототипа (первое, что делать в облаке)
1. Запустить в редакторе (scenes/MainMenu.tscn → Новая игра). Проверить:
   - Бандит (оранжевый круг) агрится в 5 тайлах, бьёт игрока (HP-бар падает, тосты «💥 −X»)
   - Space — ответный удар, убийство → лут на земле (E — подбор)
   - Страж (синий) рядом вступается за игрока
   - Смерть игрока → респавн в центре через 3с
   - Пояс: надеть (B, двойной клик) → хотбар 3-9 появился → drag&drop пилюли → клавиша 3 → HP восстановился
   - Диалоги E у торговца/прохожих; закрытие кликом выбора НЕ зависает
2. Если урон NPC→игрок не доходит: смотреть цепочку AttackIntentEvent(npc,"player_0") → CombatModule.OnAttackIntent → CombatService.ExecuteAttack → DamageAppliedEvent (target=player_0) → BodyService игрока.

### P1 — по плану NPC_COMBAT_PREP.md (фазы 1/2/6 ГОТОВЫ)
3. Phase 7: Combat Visuals — HP-бар над NPC, DamageNumber (~330 LOC)
4. Phase 4-5: Trade — торговец уже спавнится, диалог есть
5. Phase 3: Faction Port
6. Phase 8: Weapon Variety wiring — 5 TODO в CombatService (penetration/dodge/parry/shield/crit ← StatBonuses «Матрёшки» + EquipmentData); генератор уже выдаёт значения
7. Баг: Cultivator technique cap=0 (TechniqueGeneratorService)

### Проверенные фиксы (не ломать)
- Экипировка: возврат старого предмета ТОЛЬКО через событие OldItemId → InventoryModule (CharacterDollPanel больше не делает TryAddItem вручную — иначе дубль)
- Resume после диалога: только через DialogueEndedEvent-подписку GameWorldController
- Зум/движение мыши: modalOpen-гвард в _UnhandledInput
- Скорость игрока: gameSpeedMult = Fast→2.0 / Quick→3.5 (Q7 уточнён пользователем)

---

## 8. КЛЮЧЕВЫЕ ФАЙЛЫ ДЛЯ ЧТЕНИЯ

| Файл | Назначение |
|------|------------|
| `SESSION_CONTEXT.md` | **ЭТОТ ФАЙЛ** — читать первым |
| `START_PROMPT.md` | Правила работы, структура проекта, запреты |
| `SESSION_SUMMARY.md` | Сводка сессий, текущее состояние |
| `docs/docs_v2/09_workflow/NPC_COMBAT_PREP.md` | План внедрения NPC + Combat (9 phases) |
| `docs/docs_v2/09_workflow/COLD_START.md` | Структура окружения |
| `docs/docs_v2/09_workflow/ENVIRONMENT_CONCEPT.md` | Концепты (BG3 REJECTED, harvest modes) |
| `docs/docs_v2/07_ui/MOUSE_INPUT_SCHEME.md` | Схема обработки мыши |
| `checkpoints/08_22_body_impl_complete.md` | Последняя реализация (body + animals) |
| `checkpoints/08_22_impl_complete.md` | 9 аудиторских исправлений |
| `checkpoints/08_22_audit_fix_plan.md` | 14 концептуальных вопросов + ответы |
| `worklog.md` | Хроника работы (~2500+ строк, читать последние) |

---

## 9. КОМАНДЫ ПРОВЕРКИ

```bash
# Build
cd /home/z/my-project/aigame4/game
export DOTNET_ROOT=/home/z/.dotnet && export PATH="$DOTNET_ROOT:$PATH"
dotnet build CultivationGame.csproj

# Headless test (50×50 — default)
GODOT=/home/z/godot/Godot_v4.7.1-stable_mono_linux_x86_64/Godot_v4.7.1-stable_mono_linux.x86_64
timeout 15 "$GODOT" --headless --path . scenes/GameWorld.tscn

# Full flow headless (scene-assembly: NPC spawn, dialogues, state=Playing)
GODOT_NEWGAME=1 timeout 15 "$GODOT" --headless --path . scenes/MainMenu.tscn

# Large world test (500×500)
GODOT_MAP_SIZE=500 timeout 20 "$GODOT" --headless --path . scenes/GameWorld.tscn

# Generator debug (включая дамп «Матрёшки»: все подтипы + зачарование)
GODOT_GEN_DEBUG=1 timeout 15 "$GODOT" --headless --path . scenes/GameWorld.tscn

# Git
cd /home/z/my-project/aigame4
git log --oneline -10
git status
```

### Ожидаемый headless вывод (50×50):
```
[WorldModule] Started
[TileModule] Started — generated 50x50 grid in ~20 ms
[AnimalSpawn] Spawned wolf #animal_wolf_1 at (9, 7) (body: Quadruped/Medium)
[AnimalSpawn] Spawned deer #animal_deer_4 at (48, 22) (body: Quadruped/Medium)
[AnimalSpawn] test_polygon: spawned 5/5 animals
[GameBoot] Game initialized. Container built and entry point started.
[Inventory] Test items seeded
[CharacterSheet] Ready
[AnimalSpriteRenderer] Ready
```

---

## 10. ПРЕДУПРЕЖДЕНИЯ

1. **Next.js DEV сервер НЕ запускать** — это sandbox Z.ai Code, не игра
2. **worklog.md** большой (~2500+ строк) — читать последние записи
3. **Ai-game3-ref** — reference only (не коммитить в него)
4. **.gitignore** — правило `game` исправлено на `/my-project/` (commit 1f4f167)
5. **Biome sprites missing** (biome_ocean.png etc.) — нужен Godot Editor для импорта .ctex
6. **DOTNET_ROOT** должен быть установлен для Godot headless
7. **Cyclic symlink** — `/home/z/godot/godot` не должен существовать (был баг, удалён)
8. **Документация НЕ редактируется** без прямого указания пользователя
9. **Save/load отключён** — F5/F9 не работают (Q8 decision)
10. **TestItemSeeder** gated behind `#if DEBUG` — в release сборке тестовых предметов нет

---

## 11. ИНТЕГРАЦИОННЫЕ ТОЧКИ

### Hotkeys
| Key | Action |
|-----|--------|
| WASD | Movement (real-time; ускоряется на Fast ×2 / Quick ×3.5) |
| Shift | Run (1.8× speed) |
| LMB | Move to point (world) / drag items (inventory) |
| RMB | Info (inventory items) / вернуть из слота пояса |
| Wheel | Zoom (world; заблокирован при открытом инвентаре) / scroll (inventory) |
| Middle | Reset zoom to 3× |
| **Space** | **Атака ближайшего NPC в 2.5 тайла** |
| E | Диалог с NPC рядом (приоритет) / pick up ground item |
| B | Toggle Inventory (pause game) |
| C | Toggle Character Sheet (pause game) |
| F | Harvest resource at cursor |
| Esc | Pause / close inventory / character sheet / dialogue |
| PageUp/Down | Time speed (Normal/Fast/Quick, тосты) |
| **1-9** | **Хотбар: 1-2 оружие (инфо), 3-9 использование расходников пояса** |
| V | Meditate (action registered, effect TBD) |

### Env vars
| Var | Purpose |
|-----|---------|
| `GODOT_MAP_SIZE=500` | Override map size for perf testing |
| `GODOT_GEN_DEBUG=1` | Dump generated items + techniques (вкл. «Матрёшку»: все подтипы оружия/брони + зачарование) |
| `GODOT_SCREENSHOT=path` | Take screenshot on startup |
| `GODOT_NEWGAME=1` | Автостарт новой игры из MainMenu (headless полный флоу scene-assembly; GameWorld.tscn напрямую фазы НЕ запускает!) |

### DI registration order (GameLifetimeScope.cs)
World → Tile → Body → Qi → Buff → Inventory → Combat → Formation → NPC → Player → Quest → Interaction → UI → **Charger** → Save → Generator

---

*Конец файла SESSION_CONTEXT.md. Прочитай START_PROMPT.md следующим.*
