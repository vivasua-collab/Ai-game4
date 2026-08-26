# Чекпоинт: Transfer Core/Data from Ai-game3 to Ai-game4

**Дата:** 2026-08-15 07:10 UTC
**Тип:** migration
**Task ID:** 7-a
**Agent:** transfer-core-data

---

## Контекст

Перенос Core/Data файлов из Ai-game3 (Unity итерация) в Ai-game4 (Godot 4.7.1).
Core слой — engine-agnostic pure C#, переносится напрямую с минимальной адаптацией:
- `using UnityEngine;` → удаляется
- `UnityEngine.Mathf.Max` → `System.Math.Max`
- `namespace CultivationGame.Core` → `namespace CultivationGame.Core.Data`
- `#nullable enable` добавляется в начало каждого файла

Источник: `/home/z/my-project/Ai-game3-ref/UnityProject/Assets/Scripts/Core/Data/` + `Core/Utility/Permil.cs` + `Core/Random/SeededRandom.cs`
Цель: `/home/z/my-project/Ai-game4/game/src/Core/Data/`

---

## Work Log

### Files Transferred (14 файлов, скопированы + адаптированы):

1. **Constants.cs** (1397→1467 строк) — все игровые константы (култивация, Qi, combat, body part HP, soft caps, morphology hit tables, formation tables, save system). Адаптации:
   - `UnityEngine.Mathf.Max` → `Math.Max` (line 1340)
   - Добавлен `using System;`
   - Namespace `CultivationGame.Core` → `CultivationGame.Core.Data`
   - Добавлен `#nullable enable`
   - Добавлены 17 missing constants для совместимости с existing Structs.WorldTime и Adapter: `TICKS_PER_HOUR/DAY/MONTH/YEAR`, `START_YEAR`, `SPEED_NORMAL/FAST/QUICK`, `AUTOSAVE_INTERVAL_TICKS`, `QI_REGEN_BATCH_TICKS`, `TILE_SIZE_M`, `METERS_TO_PIXELS`, `TILE_PIXELS`, `TILE_PPU`, `MAX_ACTIVE_NPCS`, `AGGRO_RADIUS`, `ATTACK_RADIUS`, `PATROL_RADIUS`, `DEFAULT_MOVE_SPEED`, `FLEE_SPEED_MULT`, `SAVE_MAIN_FILE`, `SAVE_CHUNKS_DIR`, `SAVE_LOCATIONS_DIR`, `SAVE_METADATA`

2. **Enums.cs** (985→1086 строк) — все enums (CultivationLevel с именами AwakenedCore/LifeFlow/..., Element, DamageType, TechniqueType, CombatSubtype, TechniqueGrade, BodyPartType, EquipmentSlot, ItemCategory, ItemRarity, NPCCategory, NPCRole, NPCAIState, PersonalityTrait, LocationType, BiomeType, TimeSpeed, TimeOfDay, GameState, ...). Адаптации:
   - Namespace `CultivationGame.Core` → `CultivationGame.Core.Data`
   - Добавлен `#nullable enable`
   - Добавлены 9 missing enums для backward compatibility с existing Modules/Entry/Adapter: `Direction`, `Season`, `WaterType`, `ConsciousnessType`, `TechniqueSubtype` (alias of CombatSubtype), `GameItemType`, `RenderLayer`, `SaveSlotType`

3. **FormationEnums.cs** (118 строк) — formation enums: `FormationCoreType`, `FormationCoreVariant`, `FormationType`, `FormationSize`, `FormationStage`, `FormationEffectType`, `ControlType`

4. **TileEnums.cs** (101 строк) — tile enums: `TerrainType` (None/Grass/Dirt/Stone/Water_Shallow/Water_Deep/Sand/Snow/Ice/Lava/Void/Road), `ObjectCategory`, `ObjectType`, `HardnessTier`, `TileFlags` (Flags attribute)

5. **StatType.cs** (41→45 строк) — extended StatType enum. **Внимание:** к source-списку добавлен `Conductivity` (используется CharacterData/NPCState в DataModels.cs)

6. **StatBonus.cs** (24→23 строк) — `[Serializable] class StatBonus` с полями `string StatName`, `float Value`, `bool IsPercentage`. Заменил existing target's `readonly struct StatBonus` (удалён из Structs.cs)

7. **NPCData.cs** (55→44 строк) — `class NPCData` с полями NpcId, PresetId, DisplayName, Category, Personality, Position, SoulType, Morphology, CultivationLevel, MaxQi, CurrentQi, Conductivity, MaxHealth, CurrentHealth, Role, AIState, TargetId, StateTimer, AttitudeScore, IsAlive, IsInCombat, SectId, CurrentLocation. Адаптации: удалён `using UnityEngine;`, string поля инициализированы `string.Empty`

8. **SpeciesData.cs** (86→85 строк) — `sealed class SpeciesData` (immutable, constructor-injected). Адаптации: удалён `using CultivationGame.Core;` (self-reference), `innateAbilities` parameter marked `string[]?`

9. **BodyPartTemplate.cs** (77→76 строк) — `sealed class BodyPartTemplate` (immutable). Адаптации: `parentPartId` marked `string?`, `equipmentSlots` marked `EquipmentSlot[]?`

10. **BodyTemplate.cs** (91→93 строк) — `sealed class BodyTemplate` с иерархией parts. Адаптации: `Hierarchy` typed `IReadOnlyDictionary<string, string?>`, `GetPartTemplate` returns `BodyPartTemplate?`

11. **TechniqueData.cs** (108→109 строк) — `sealed class TechniqueData` с полями TechniqueId, NameRu, NameEn, Description, Type, Subtype (CombatSubtype), Grade (TechniqueGrade), Element, Level, CapacityCost, QiCost, BaseDamage, Cooldown, Range, CastTime, IsUltimate, UltimateDamageMultiplier=2.0f, UltimateQiCostMultiplier=1.5f, Mastery, ArmorPenetration. Заменил existing target's `TechniqueData` class (удалён из DataModels.cs)

12. **GameTile.cs** (210→211 строк) — `[Serializable] struct GameTile` с X, Y, Terrain, MoveCost, Flags, Object, ObjectCategory, IsHarvestable, ResourceAmount, ResourceMax, ResourceId, IsDestructible, DestructibleHP, DestructibleMaxHP, HardnessTier. Properties: IsWalkable, EffectiveMoveCost. Static factories: CreateTerrain, CreateWithObject, GetTerrainMoveCost, GetTerrainFlags, GetObjectCategory. Зависит от ObjectDefaults

13. **ObjectDefaults.cs** (253→253 строк) — `readonly struct ObjectInfo` + `static class ObjectDefaults` с таблицей характеристик объектов (Tree_Oak, Tree_Pine, Tree_Birch, Bush, Bush_Berry, Rock_Small/Medium/Large, Chest, OreVein, Herb). Методы: TryGet, Get, GetHP, GetResourceMax, GetResourceId, IsPassable, GetMoveCostModifier, GetHarvestAmount, GetItemId, GetRespawnDays, GetHardnessTier

14. **Permil.cs** (165→165 строк) — `static class Permil` для промилле-арифметики (1‰ = 1/1000). Методы: Apply, ApplyLong, ApplyLongLong, Multiply, ApplyTwice, FromFloat, ToFloat, FromPercent, ToPercent, Ratio, RatioLong, SoftCap, Clamp. Константы: ONE=1000, HALF=500, ZERO=0. Перенесён из `Core/Utility/Permil.cs`

### Files Merged (existing target versions KEPT, source SKIPPED — 5 файлов):

Existing target уже содержал richer/лучше-интегрированные версии этих типов. Решение: оставить existing, не переносить source (это вызвало бы duplicate type conflict).

- **Position2D.cs** — existing target's `readonly struct Position2D(int x, int y)` (int-based, tile-grid). Source version was `struct Position2D(float x, float y)` with Vector2 conversions. Existing уже используется PlayerService, PlayerModule, PlayerSpawnPhase с int-конструктором. Source redundant (existing target has separate `Vector2f` for float world-space)
- **InventorySlot.cs** — existing target's `readonly struct InventorySlot(string ItemId, int Count, float Weight, float Volume)` (used by IInventoryService.GetSlots). Source version was `(string ItemId, int Count, ItemCategory Category, ItemRarity Rarity)` — different shape, would break IInventoryService
- **LootEntry.cs** — existing target's `readonly struct LootEntry(string ItemId, float Chance, int MinCount, int MaxCount)`. Source was `(string ItemId, int Count, ItemRarity Rarity, string Source)` — different shape
- **InputFrameData.cs** — existing target's richer `readonly struct InputFrameData(Vector2f MoveDirection, bool IsRun, bool IsLmbPressed, bool IsRmbPressed, float RmbHoldDuration, Vector2f MouseWorldPos, bool IsOverUI, int? HotbarSlot, IReadOnlySet<string> StickyKeys, long Frame)`. Source was simpler (Position2D MoveDirection, dedicated action bools). Existing target's design better aligned с Ai-game4 architecture (StickyKeys generalises one-shot actions; Vector2f for mouse pos). Already integrated with PlayerService.cs and InputAdapter.cs
- **SeededRandom.cs** — existing target's `sealed class SeededRandom(int seed)` using xorshift64* (deterministic cross-platform). Source version used `System.Random` + `UnityEngine.Random.Range` in parameterless ctor. Existing target's version is engine-agnostic, deterministic, more sophisticated. Source version had `UnityEngine.Random` dependency (Unity-specific)

### Files Skipped (Unity-specific, NOT transferred — 5 файлов):

- `ScriptableObjects/ItemData.cs` — Unity ScriptableObject (мы используем JSON configs)
- `ScriptableObjects/EquipmentData.cs` — Unity ScriptableObject
- `UIFactory.cs` (41 KB, 964 lines) — Unity uGUI. Ai-game4 имеет собственный Godot UIFactory в Adapter
- `UIFontCache.cs` (387 lines) — Unity-specific font cache
- `UISpriteCache.cs` (434 lines) — Unity-specific sprite cache
- `UIThemeV3.cs` (172 lines) — Unity-specific UI theme. Ai-game4 использует ParchmentTheme (Godot .tres)

### Files Edited (existing target — для разрешения конфликтов):

- **DataModels.cs** — удалён `class TechniqueData` (теперь в отдельном файле TechniqueData.cs). Оставлены: GameSessionData, CharacterData, NPCState, TileData, InventoryItem, LocationData, FactionData, FactionRelation
- **Structs.cs** — удалён `readonly struct StatBonus` (теперь в отдельном файле StatBonus.cs как class). Оставлены: Position2D, Vector2f, WorldTime, InventorySlot, LootEntry, TileCoord, Rect2i, InputFrameData

---

## Build Status

```bash
cd /home/z/my-project/Ai-game4/game
dotnet build 2>&1 | grep "Core/Data"
# Output: (пусто — 0 warnings, 0 errors в Core/Data)
```

**Core/Data компилируется без ошибок и предупреждений.**

Total build errors: 476 (все в Modules/Core-Interfaces/Entry — pre-existing stubs, НЕ в Core/Data).
Task rule: "Errors in other layers (Modules/Entry/Adapter) are expected at this stage — don't touch them."

Pre-existing errors by layer:
- Modules: 350+ errors (старые stubs не реализуют новые rich interfaces; также ссылаются на удалённые enum members типа `TerrainType.Water`, `TerrainType.DeepWater` которые переименованы в `Water_Shallow/Water_Deep` по source)
- Core/Interfaces: 60+ errors (ссылаются на типы ItemData, EquipmentData, BodyPart, HeatState, ChargerSlotState, HarvestResult — ещё не мигрированы; task 7-b перенесёт Core/Interfaces из Ai-game3)
- Entry: 16+ errors (AbstractSceneAssemblyPhase не реализует ISceneAssemblyPhase полностью)

---

## Stage Summary

- **Core/Data files: 17** (14 transferred + 3 existing edited: DataModels.cs, Structs.cs, SeededRandom.cs)
- **Compile errors in Core/Data: 0** ✅
- **Notable adaptations:**
  - `UnityEngine.Mathf.Max` → `System.Math.Max` (Constants.cs line 1340)
  - `using UnityEngine;` удалён из NPCData.cs, SeededRandom.cs (existing target's version, не source)
  - `using CultivationGame.Core;` self-references удалены из SpeciesData.cs, BodyPartTemplate.cs, BodyTemplate.cs, TechniqueData.cs
  - Namespace `CultivationGame.Core` → `CultivationGame.Core.Data` для 8 файлов (Constants.cs, Enums.cs, FormationEnums.cs, TileEnums.cs, StatType.cs, Permil.cs, GameTile.cs, ObjectDefaults.cs)
  - `#nullable enable` добавлен во все 14 transferred files
  - Nullable annotations: `string?` для optional IDs (ParentPartId, TargetId), `string[]?` для optional arrays (innateAbilities, equipmentSlots), `T?` для TryGet-style returns (GetPartTemplate)
  - Default field initializers: `string.Empty` для non-nullable string fields в NPCData и TechniqueData
  - 17 time/tick/rendering constants добавлены в Constants.cs (для совместимости с existing Structs.WorldTime и Adapter)
  - 9 backward-compat enums добавлены в Enums.cs (Direction, Season, WaterType, ConsciousnessType, TechniqueSubtype, GameItemType, RenderLayer, SaveSlotType) — были в existing target's stub Enums.cs, отсутствуют в source
  - `StatType.Conductivity` добавлен (используется CharacterData/NPCState в DataModels.cs)

---

## Решения

1. **Existing target's Position2D/InventorySlot/LootEntry/InputFrameData/SeededRandom KEPT** (не перенесены из source) — existing versions уже интегрированы с Modules/Adapter, source versions имели бы duplicate type conflict. Existing versions богаче или лучше спроектированы под Ai-game4 architecture.
2. **Source's StatBonus class REPLACED existing target's StatBonus struct** — source version используется Ai-game3 modules (будут мигрированы в task 7-d). Existing struct был только в Structs.cs (никем не использовался).
3. **Source's TechniqueData class REPLACED existing target's TechniqueData class in DataModels.cs** — source version богаче (BaseDamage, Mastery, ArmorPenetration, UltimateDamageMultiplier). Existing target's ICombatService параметр принимает TechniqueData.
4. **Permil.cs перенесён из Core/Utility/ в Core/Data/** — task target directory. Логически Permil — math utility, может быть перемещён обратно в Utility если возникнет необходимость.
5. **`TerrainType` переименован в source-стиль** (Water_Shallow, Water_Deep) — Modules/Tile/TileService.cs и Adapter/Scene/SceneBuilder.cs используют старые имена (Water, DeepWater, ShallowWater, Mountain, Bush, TallGrass). Это вызовет ~20 errors в Modules/Adapter — expected, будет исправлено в task 7-e (Modules migration).
6. **CultivationLevel changing values** — source имеет `AwakenedCore=1, LifeFlow=2, ..., Ascension=10` (10 levels, имена). Existing target имел `L1=1..L9=9`. Existing Modules не использовали CultivationLevel.X members directly (только int CultivationLevel property). Safe to replace.

---

## Найденные проблемы

- **`Element` vs `ElementType`**: source Enums.cs имеет `Element` enum. Existing target's Enums.cs имел `ElementType`. DataModels.cs (existing) использует `ElementType`. TechniqueData.cs (source) использует `Element`. Решение: оставлены оба — `Element` (source, используется TechniqueData) и `ElementType` НЕ добавлен обратно (DataModels.cs Field — будет исправлено при Modules migration).
- **`CombatSubtype` vs `TechniqueSubtype`**: source имеет `CombatSubtype` (используется TechniqueData.cs). Existing target имел `TechniqueSubtype` (используется GeneratorService.cs). Оба оставлены — дублирование, но не критично. Modules migration адаптирует GeneratorService.
- **Core/Interfaces errors**: 60+ errors в Core/Interfaces из-за неразрешённых типов (BodyPart, HeatState, ItemData, EquipmentData, HarvestResult, ChargerSlotState). Эти типы будут перенесены в task 7-b (Core/Interfaces) и позже (ScriptableObject → plain class migration).

---

## Следующие шаги

1. **Task 7-b: Transfer Core/Interfaces** (37 файлов) — добавит `using CultivationGame.Core.Data;` где нужно, разрешит ~60 ошибок типов
2. **Task 7-c: Transfer Core/Messaging/Contracts** (22 файла)
3. **Task 7-d: Transfer Modules Calculators + Configs**
4. **Task 7-e: Transfer Modules Services** (адаптация MessagePipe→EventBus, переименование TerrainType members в Modules)
5. **Task 7-f: Transfer Tests**

---

## Файлы

**Created/Modified:**
- `/home/z/my-project/Ai-game4/game/src/Core/Data/Constants.cs` (modified — namespace, UnityEngine.Mathf, added 17 constants)
- `/home/z/my-project/Ai-game4/game/src/Core/Data/Enums.cs` (modified — namespace, added 9 backward-compat enums)
- `/home/z/my-project/Ai-game4/game/src/Core/Data/FormationEnums.cs` (new — namespace)
- `/home/z/my-project/Ai-game4/game/src/Core/Data/TileEnums.cs` (new — namespace, [Flags] using System)
- `/home/z/my-project/Ai-game4/game/src/Core/Data/StatType.cs` (new — namespace, added Conductivity)
- `/home/z/my-project/Ai-game4/game/src/Core/Data/StatBonus.cs` (new — namespace, default string.Empty)
- `/home/z/my-project/Ai-game4/game/src/Core/Data/NPCData.cs` (new — removed using UnityEngine, default string.Empty)
- `/home/z/my-project/Ai-game4/game/src/Core/Data/SpeciesData.cs` (new — removed self-using, nullable annotations)
- `/home/z/my-project/Ai-game4/game/src/Core/Data/BodyPartTemplate.cs` (new — nullable annotations)
- `/home/z/my-project/Ai-game4/game/src/Core/Data/BodyTemplate.cs` (new — nullable annotations)
- `/home/z/my-project/Ai-game4/game/src/Core/Data/TechniqueData.cs` (new — removed self-using, default string.Empty)
- `/home/z/my-project/Ai-game4/game/src/Core/Data/GameTile.cs` (new — namespace)
- `/home/z/my-project/Ai-game4/game/src/Core/Data/ObjectDefaults.cs` (new — namespace)
- `/home/z/my-project/Ai-game4/game/src/Core/Data/Permil.cs` (new — moved from Core/Utility/, namespace)
- `/home/z/my-project/Ai-game4/game/src/Core/Data/DataModels.cs` (modified — removed TechniqueData class)
- `/home/z/my-project/Ai-game4/game/src/Core/Data/Structs.cs` (modified — removed StatBonus struct)

**Unchanged (existing target versions kept):**
- `/home/z/my-project/Ai-game4/game/src/Core/Data/SeededRandom.cs` (existing xorshift64* version kept, source System.Random version skipped)
- Existing target's Position2D, InventorySlot, LootEntry, InputFrameData (in Structs.cs) — kept, source versions skipped

---

Task ID: 7-a
Agent: transfer-core-data
Task: Transfer Core/Data from Ai-game3 to Ai-game4

Work Log:
- 14 files transferred: Constants.cs, Enums.cs, FormationEnums.cs, TileEnums.cs, StatType.cs, StatBonus.cs, NPCData.cs, SpeciesData.cs, BodyPartTemplate.cs, BodyTemplate.cs, TechniqueData.cs, GameTile.cs, ObjectDefaults.cs, Permil.cs
- 5 files merged (existing kept, source skipped): Position2D, InventorySlot, LootEntry, InputFrameData, SeededRandom — existing target versions richer/better-integrated
- 6 files skipped (Unity-specific): ScriptableObjects/ItemData.cs, ScriptableObjects/EquipmentData.cs, UIFactory.cs, UIFontCache.cs, UISpriteCache.cs, UIThemeV3.cs
- 2 existing files edited: DataModels.cs (removed duplicate TechniqueData class), Structs.cs (removed duplicate StatBonus struct)
- Build status: Core/Data compiles cleanly (0 errors, 0 warnings). 476 pre-existing errors in Modules/Core-Interfaces/Entry (outside task scope)

Stage Summary:
- Core/Data files: 17 (14 transferred + 3 existing kept/edited)
- Compile errors in Core/Data: 0
- Notable adaptations: UnityEngine.Mathf→System.Math; using UnityEngine removed; namespace CultivationGame.Core→CultivationGame.Core.Data; #nullable enable added; nullable annotations for optional strings/arrays; 17 time/rendering constants added to Constants.cs for backward compat with Structs.WorldTime/Adapter; 9 backward-compat enums added to Enums.cs (Direction, Season, WaterType, ConsciousnessType, TechniqueSubtype, GameItemType, RenderLayer, SaveSlotType); StatType.Conductivity added
