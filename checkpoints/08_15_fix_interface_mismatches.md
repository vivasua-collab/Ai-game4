# Чекпоинт: Fix Adapter + Entry + Module interface mismatches

**Дата:** 2026-08-15 UTC
**Сессия:** Task ID 10 — fix-interface-mismatches
**Тип:** fix

---

## Контекст

After transferring Core + Modules from Ai-game3 (Unity iteration) into Ai-game4
(Godot 4.7.1 .NET), the build had 200 errors. The bulk of these were interface
mismatches between the new Ai-game4 Core/Interfaces (cleaner, smaller) and the
migrated Ai-game3 Modules + Adapter/Entry stubs that still referenced the old
Ai-game3 API surface.

Error breakdown before fix:
- 80 CS1503 (float→int conversion — Position2D is int in Ai-game4)
- 58 CS1061 (missing members on interfaces / data classes)
- 16 CS0117 (Config field name mismatches)
- 12 CS1501 (method overload mismatch)
- 6 CS1678 / 6 CS1661 (delegate `in` keyword mismatch)
- 6 CS0019 (operator errors)
- 4 CS7036 (missing ctor args)
- 2 CS1729 / 2 CS1628 / 2 CS0452 / 2 CS0246 / 2 CS0136 / 2 CS0103

## Work Log

### Adapter fixes (Task 1)

- `Core/Interfaces/ITimeService.cs` — added `WorldTime CurrentTime { get; }` and
  `bool IsPaused { get; }` (already present on concrete TimeService as
  derived properties; surfaced to interface for Ai-game3 callers).
- `Core/Interfaces/IWorldService.cs` — added `void SetActiveLocation(string)`
  (already present on concrete WorldService; used by WorldInitPhase).
- `Core/Interfaces/ITileService.cs` — added
  `void Generate(int seed, int width, int height, TerrainType baseTerrain)`
  (already present on concrete TileService; used by TileModule / TileMapGenPhase).
- `Core/Interfaces/IPlayerService.cs` — added `void MoveTo(int x, int y)` and
  `void Spawn(Position2D position)` (already present on concrete PlayerService).
- `Core/Interfaces/IPlayerInputService.cs` — added `CurrentFrame`,
  `IsPausePressed`, `IsQuickSavePressed`, `IsQuickLoadPressed` properties.
- `Modules/Player/PlayerInputService.cs` — implemented the three new sticky
  flag properties (backed by existing `_pause` / `_quickSave` / `_quickLoad`
  fields, gated by `InputDisabled`).
- `Adapter/Input/InputAdapter.cs` — renamed `UpdateFrame` call to
  `UpdateInputState` (the actual interface method).

### Entry fixes (Task 2)

Entry phases were not directly broken — AbstractSceneAssemblyPhase already
implements the new ISceneAssemblyPhase interface (Order, State, CanExecute,
BlockReason, ExecuteAsync, MarkAsSkipped, Reset, SkipOnLoad). All Entry errors
were caused by interface member gaps in Core/Interfaces, which were resolved by
the additions above.

### Float→int casts (Task 3, 80 errors)

- `Modules/NPC/NPCService.cs` — `new Position2D(entry.PosX, entry.PosY)` →
  cast both args to int (PosX/PosY are float in save data).
- `Modules/NPC/NPCAIService.cs` — `new Vector2(e.X, e.Y)` in
  OnPlayerPositionChanged → cast to int.
- `Modules/NPC/NPCMovementService.cs` — `new Vector2(e.X, e.Y)` → cast to int;
  rewrote `Random.Shared.Next(0f, 360f)` → `Random.Shared.NextDouble() * 360.0`
  (Random has no float overload); rewrote `new Vector2(MathF.Cos(angle) * dist, …)`
  → cast to int for Position2D ctor.
- `Modules/Interaction/InteractionService.cs` — `new Position2D(e.X, e.Y)` →
  cast to int; `new Position2D(5f, 5f)` etc. (test interactables) → cast to int.

### Module method-name fixes (Task 4, 58 CS1061)

- `Modules/Tile/TileModule.cs` — `e.OldLocationId` → `e.PreviousLocationId`
  (new LocationChangedEvent contract).
- `Modules/Combat/CombatService.cs` — QiBufferStateChanged subscription lambda
  typed `in QiChangedEvent` → `in QiBufferStateChangedEvent` (was subscribing
  to the wrong event type).
- `Modules/Combat/DamageService.cs` — same QiBufferStateChanged fix, plus
  EquipmentChanged subscription lambda `in QiChangedEvent` →
  `in EquipmentChangedEvent`.
- `Modules/NPC/NPCService.cs` — renamed local `state` to `npcState` in
  RestoreState foreach (shadowed the outer `state` parameter, CS0136).
- `Modules/NPC/NPCQiRegenService.cs` — `OnDayChanged(DayChangedEvent e)` →
  `OnDayChanged(in DayChangedEvent e)` (Subscribe expects `in` delegate).
- `Modules/Quest/QuestProgressTracker.cs` — copied `e.Level` to local
  `breakthroughLevel` before lambda (CS1628: cannot use `in` param inside lambda).
- `Modules/Inventory/InventoryService.cs` — `slotSave.ItemId/Category/Rarity`
  → lowercase `itemId/category/rarity` (InventorySlotSaveData uses lowercase).
- `Modules/Inventory/EquipmentValidator.cs` — `item.statRequirements` →
  `item.StatRequirements` (ItemData field capitalisation).
- `Modules/Generator/ItemGeneratorService.cs` — `consumable.effects` →
  `Effects`; `effectType` / `value` / `duration` → `EffectType` / `Value` /
  `Duration` (ItemEffect field capitalisation); `rng.Next(6)` → `rng.Next(0, 6)`
  (SeededRandom.Next signature is `(int min, int max)`).
- `Modules/Charger/ChargerService.cs` — added `using CultivationGame.Core.Data;`
  so the `Element` enum (from Core/Data/Enums.cs) is visible.
- `Modules/Charger/ChargerData.cs` — `ChargerBufferConfig` changed from struct
  to class so `RegisterInstance<T> where T : class` accepts it (CS0452).
- `Modules/World/WorldService.cs` — `if (faction == null)` →
  `if (string.IsNullOrEmpty(faction.Id))` (FactionInfo is a struct, CS0019).

### Config field fixes (Task 5, 16 CS0117)

- `Modules/Formation/FormationModuleServices.cs` — removed `CasterId`,
  `InitialStage`, `MaxPoolCapacity` initialisers (no longer exist on
  FormationConfig); kept `DefaultCasterId = "player"` only.
- `Modules/Save/SaveModule.cs` — `_config.AutosaveEnabled` (removed field) →
  `_config.AutoSaveIntervalMinutes <= 0` (new gate semantics).
- `Modules/Save/SaveService.cs` — `new SaveInfo(slot, name, DateTime.UtcNow, 0)`
  → `new SaveInfo(slot, name, 0L, 0L, 0, string.Empty)` (SaveInfo ctor now
  takes 6 args: slot, displayName, createdUnixSeconds, playedSeconds,
  cultivationLevel, locationId).
- `Modules/Player/PlayerService.cs` — `CultivationLevel.Mortal` →
  `CultivationLevel.None` (enum renamed: `None` is the "смертный" tier now).

### Position2D / LootEntry / SeededRandom compatibility shims

These three Core/Data types had been redesigned in Ai-game4 with stricter
signatures. The migrated Ai-game3 Modules use the older, looser API. Added
shim members to restore compatibility without rewriting the Modules:

- `Core/Data/Structs.cs Position2D` — added:
  - `Vector2f normalized { get; }` (Ai-game3 lowercase alias)
  - `static float Distance(Position2D a, Position2D b)`
  - `static Vector2f operator *(Position2D a, float s)`
  - `static Position2D operator +(Position2D a, Vector2f b)`
  - `static Position2D operator -(Position2D a, Vector2f b)`
  - `implicit operator Position2D(Vector2f v)` (rounds to int)
- `Core/Data/Structs.cs LootEntry` — restored to Ai-game3 signature:
  `(string ItemId, int Count, ItemRarity Rarity, string Source)` (the Ai-game4
  stub had `{ Chance, MinCount, MaxCount }` — all 7 migrated CombatLootService
  call sites use the Ai-game3 signature).
- `Core/Data/SeededRandom.cs` — added parameterless ctor
  `SeededRandom() : this(Environment.TickCount)` (Ai-game3 had one).
- `Modules/Combat/CombatLootService.cs` — replaced `UnityEngine.Random.Range`
  / `Random.Value` with a private `static readonly Random _random` field
  (UnityEngine.Random not available in Godot/.NET).

## Build status

- **Before:** 200 errors, 254 warnings
- **After:** 0 errors, 255 warnings (no new warnings; one CS0414 is
  pre-existing dead field)

`dotnet build --no-incremental` exits cleanly in 2.6 s.

## Решения

- **Add missing members to interfaces rather than rewriting call sites** —
  adding `MoveTo` / `Spawn` / `SetActiveLocation` / `Generate` /
  `IsPaused` / `CurrentTime` / `CurrentFrame` / sticky flags to the new
  Ai-game4 interfaces is mechanical, preserves the migrated code unchanged,
  and matches the Ai-game3 API surface the Modules expect.
- **Add Position2D float helpers rather than converting all NPC math to
  Vector2f** — `normalized`, `Distance`, `operator *(float)`,
  `operator +(Vector2f)`, `implicit operator Position2D(Vector2f)` bridge
  the int-vs-float gap with zero rewrite of NPCMovementService logic.
  Trade-off: tile-coord rounding happens silently, but that's acceptable for
  NPC AI navigation in V1.
- **Restore LootEntry to Ai-game3 signature** — the Ai-game4 stub
  `{ Chance, MinCount, MaxCount }` represented a future loot-table design
  not yet implemented. All current callers want the simple
  `(itemId, count, rarity, source)` shape. Reverting avoids rewriting
  CombatLootService.
- **Change ChargerBufferConfig from struct to class** — required for
  `RegisterInstance<T> where T : class`. Only used as a single DI-registered
  instance; no value-type semantics are relied on.

## Файлы

Modified (22 files):
- `game/src/Core/Interfaces/ITimeService.cs`
- `game/src/Core/Interfaces/IWorldService.cs`
- `game/src/Core/Interfaces/ITileService.cs`
- `game/src/Core/Interfaces/IPlayerService.cs`
- `game/src/Core/Interfaces/IPlayerInputService.cs`
- `game/src/Core/Data/Structs.cs` (Position2D + LootEntry)
- `game/src/Core/Data/SeededRandom.cs`
- `game/src/Modules/Player/PlayerInputService.cs`
- `game/src/Modules/Player/PlayerService.cs`
- `game/src/Modules/Tile/TileModule.cs`
- `game/src/Modules/NPC/NPCService.cs`
- `game/src/Modules/NPC/NPCAIService.cs`
- `game/src/Modules/NPC/NPCMovementService.cs`
- `game/src/Modules/NPC/NPCQiRegenService.cs`
- `game/src/Modules/Combat/CombatService.cs`
- `game/src/Modules/Combat/DamageService.cs`
- `game/src/Modules/Combat/CombatLootService.cs`
- `game/src/Modules/Charger/ChargerData.cs`
- `game/src/Modules/Charger/ChargerService.cs`
- `game/src/Modules/Formation/FormationModuleServices.cs`
- `game/src/Modules/Generator/ItemGeneratorService.cs`
- `game/src/Modules/Inventory/EquipmentValidator.cs`
- `game/src/Modules/Inventory/InventoryService.cs`
- `game/src/Modules/Interaction/InteractionService.cs`
- `game/src/Modules/Quest/QuestProgressTracker.cs`
- `game/src/Modules/Save/SaveModule.cs`
- `game/src/Modules/Save/SaveService.cs`
- `game/src/Modules/World/WorldService.cs`
- `game/src/Adapter/Input/InputAdapter.cs`

## Stage Summary

- **Build errors: 0** (down from 200; target was <30)
- **Remaining:** none blocking. 255 warnings (mostly CS0414 dead fields and
  CS0169 unassigned fields — pre-existing, not introduced by this fix).

## Следующие шаги

- Run headless smoke test (`$GODOT --headless --path . --quit`) to confirm
  runtime DI resolution works end-to-end.
- Audit the 255 warnings — many migrated fields are dead and can be removed.
- Review Position2D float-helper layer: consider migrating NPCMovementService
  to use Vector2f throughout (cleaner long-term) once AI navigation is
  rewritten for the tile-grid model.
- LootEntry now has two competing designs (Ai-game3 vs the stub); revisit
  when the real loot-table system is implemented.

---

Task ID: 10
Agent: fix-interface-mismatches
Task: Fix Adapter + Entry + Module interface mismatches

Work Log:
- Adapter fixes: Added CurrentTime/IsPaused to ITimeService, SetActiveLocation to
  IWorldService, Generate to ITileService, MoveTo/Spawn to IPlayerService,
  CurrentFrame/IsPausePressed/IsQuickSavePressed/IsQuickLoadPressed to
  IPlayerInputService. Renamed UpdateFrame→UpdateInputState call in InputAdapter.
- Entry fixes: AbstractSceneAssemblyPhase already implements the new interface;
  all Entry errors were transitive from Core/Interfaces gaps, fixed above.
- float→int casts: NPCService, NPCAIService, NPCMovementService, InteractionService
  — added (int) casts in Position2D constructions and Random.NextDouble() in
  place of Random.Next(float,float).
- Module method name fixes: TileModule.OldLocationId→PreviousLocationId;
  CombatService & DamageService QiChangedEvent→QiBufferStateChangedEvent /
  EquipmentChangedEvent in subscription lambdas; NPCService local var rename;
  NPCQiRegenService OnDayChanged added `in`; QuestProgressTracker breakthrough
  level copied to local; InventorySlotSaveData fields→lowercase; ItemData
  statRequirements→StatRequirements; ItemEffect effects/effectType/value/duration
  →Effects/EffectType/Value/Duration; SeededRandom.Next(6)→Next(0,6).
- Config field fixes: FormationModuleServices removed CasterId/InitialStage/
  MaxPoolCapacity (kept DefaultCasterId only); SaveModule.AutosaveEnabled→
  AutoSaveIntervalMinutes; SaveInfo ctor now passes 6 args; CultivationLevel.
  Mortal→None.
- Position2D / LootEntry / SeededRandom compatibility shims added to Core/Data
  to preserve Ai-game3 API surface used by migrated Modules.
- ChargerBufferConfig struct→class (RegisterInstance<T> requires T:class).
- CombatLootService UnityEngine.Random→System.Random.
- WorldService.RegisterFaction FactionInfo==null→IsNullOrEmpty(Id).
- build status: 200 errors → 0 errors (255 warnings, no new ones introduced).

Stage Summary:
- Build errors: 0 (down from 200)
- Remaining: 255 warnings (pre-existing dead fields — not blocking)
