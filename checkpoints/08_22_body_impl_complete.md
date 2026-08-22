# Чекпоинт: Сборка тела — визуализация + животные

**Дата:** 2026-08-22 10:30 UTC
**Task ID:** BODY-IMPL

---

## Что реализовано

### Phase A: BodyStatusPanel — схематическое отображение тела ✅

**Файл:** `game/src/Adapter/UI/BodyStatusPanel.cs` (400+ LOC)

- **Силуэт тела** через `_Draw()`:
  - **Humanoid**: голова (круг) + торс (прямоугольник) + сердце + 2 руки + 2 кисти + 2 ноги + 2 ступни
  - **Quadruped**: голова + торс + сердце + 4 ноги + хвост
  - **Bird**: голова + торс + сердце + 2 крыла + хвост + 2 ноги
  - **Amorphous**: ядро + сущность (2 круга)
- **Цвет по состоянию** (BodyPartState):
  - Healthy = зелёный
  - Bruised = жёлтый
  - Wounded = оранжевый
  - Disabled = красный
  - Severed = серый (полупрозрачный)
- **Список частей** с HP (RedHP/MaxRedHP) и glyph (◉ ◐ ◑ ◒ ○)
- **Live updates**: подписка на BodyPartDamagedEvent/HealedEvent/SeveredEvent
- Inject IBodyService для GetAllParts()

### Phase B: CharacterSheetWindow (hotkey C) ✅

**Файл:** `game/src/Adapter/UI/CharacterSheetWindow.cs` (230 LOC)

- **Hotkey C** — toggle окна (как B для инвентаря)
- **Layout**: слева BodyStatusPanel, справа статы + культивация
- **Pause** при открытии (как инвентарь)
- **Статы**: ID, позиция, состояние (жив/мёртв)
- **Культивация**: уровень, под-уровень, Ци, ёмкость ядра, проводимость
- Background click → закрыть
- IsCharacterSheetPressed добавлен в IPlayerInputService

### Phase C: Простые животные на тестовой карте ✅

**Файлы созданные:**
1. `game/src/Modules/NPC/AnimalEntity.cs` (66 LOC) — POCO для животного
2. `game/src/Modules/NPC/AnimalService.cs` (390 LOC) — управление животными
3. `game/src/Entry/Phases/AnimalSpawnPhase.cs` (43 LOC) — фаза спавна
4. `game/src/Adapter/Scene/AnimalSpriteRenderer.cs` (134 LOC) — рендеринг

**Файлы изменённые:**
- `NPCModuleServices.cs` — регистрация AnimalService
- `SceneAssemblyRegistrar.cs` — AnimalSpawnPhase (заменил NPCSpawnPhase stub)
- `SceneBuilder.cs` — SetupAnimals()
- `SpeciesRegistry.cs` — добавлены deer и rabbit

**Животные:**
| Species | Morphology | Size | Stats (STR/AGI/VIT/INT) | Цвет спрайта |
|---------|------------|------|--------------------------|--------------|
| Wolf | Quadruped | Medium | 8/14/10/4 | Тёмно-серый |
| Deer | Quadruped | Medium | 6/12/8/2 | Коричневый |
| Rabbit | Quadruped | Small | 3/14/4/1 | Белый |

**Спавн:** 3-5 животных при старте (детерминированный, SeededRandom)
- 50×50: 5 животных (3 волка, 2 оленя)
- Body assembly: BodyFactory.CreateBody(Quadruped, size, vitality) → IBodyDataProvider.SetBodyParts

**Поведение:** случайное блуждание
- Каждые 2-5 секунд (species-dependent) — выбор новой цели
- Движение к цели (1-2 tiles/tick, rabbit быстрее)
- Нет pathfinding (простой greedy step)
- Нет AI combat (просто бродят)

**Спрайты:** цветные круги (процедурные)
- Размер по SizeClass (Medium=12px, Small=8px)
- Тень + контур + глаз (направление взгляда)
- ZIndex = RenderLayer.Objects (под игроком)

---

## Верификация

- **Build:** 0 errors, 227 warnings (pre-existing)
- **Headless:** 
  - 5 животных заспавнены (3 волка, 2 оленя)
  - Body собран для каждого (Quadruped/Medium)
  - AnimalService.Tick работает (животные двигаются)
  - CharacterSheet + BodyStatusPanel загружаются

---

## Файлы

**Созданные (6):**
- `game/src/Adapter/UI/BodyStatusPanel.cs` — силуэт + HP + события
- `game/src/Adapter/UI/CharacterSheetWindow.cs` — окно (hotkey C)
- `game/src/Modules/NPC/AnimalEntity.cs` — POCO
- `game/src/Modules/NPC/AnimalService.cs` — управление + спавн + wandering
- `game/src/Entry/Phases/AnimalSpawnPhase.cs` — фаза спавна
- `game/src/Adapter/Scene/AnimalSpriteRenderer.cs` — процедурные спрайты

**Изменённые (5):**
- `game/src/Core/Interfaces/IPlayerInputService.cs` — +IsCharacterSheetPressed
- `game/src/Modules/Player/PlayerInputService.cs` — +IsCharacterSheetPressed impl
- `game/src/Adapter/Scene/GameWorldController.cs` — +CharacterSheetWindow, +C key handler, HUD update
- `game/src/Adapter/Scene/SceneBuilder.cs` — +SetupAnimals
- `game/src/Modules/Body/SpeciesRegistry.cs` — +deer, +rabbit
- `game/src/Modules/NPC/NPCModuleServices.cs` — +AnimalService registration
- `game/src/Entry/SceneAssemblyRegistrar.cs` — +AnimalSpawnPhase
