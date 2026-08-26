# Чекпоинт: Аудит 16 августа — фикс критических и важных проблем

**Дата:** 2026-08-16 09:50 UTC
**Тип:** fix
**Задача:** AUDIT-FIX — fix critical + important code issues from 08_15_code_audit.md

---

## Task ID: AUDIT-FIX
**Agent:** audit-fix
**Task:** Fix critical + important code issues

---

## Work Log

### Critical Issue #2: Sticky flags race condition — FIXED
**Problem:** `PlayerModule.Tick()` called `ResetFrameFlags()` at the end of Tick(),
which runs inside `GameBoot._PhysicsProcess` (autoload). The main scene's
`GameWorldController._PhysicsProcess` runs AFTER autoload, so by the time
`HandleStickyInput()` / `HandleMouseClick()` read the flags, they were already
cleared.

**Fix:**
- `src/Modules/Player/PlayerModule.cs` — removed `_playerInputService.ResetFrameFlags()` from `Tick()`. Left a comment explaining the change.
- `src/Adapter/Scene/GameWorldController.cs` — added `PlayerInput?.ResetFrameFlags()` at the end of `_PhysicsProcess`, AFTER `HandleStickyInput()` and `HandleMouseClick()` have read the flags.

### Critical Issue #3: Double Spawn in PlayerService — FIXED
**Problem:** `PlayerService.Spawn()` did not check `_spawned`, so both
`PlayerModule.Start()` and `PlayerSpawnPhase.ExecuteAsync()` could call Spawn —
causing the `_qiChangedToken` subscription to leak (QiChangedEvent fired twice).

**Fix:**
- `src/Modules/Player/PlayerService.cs` — added `if (_spawned) return;` guard at the start of `Spawn(Position2D)`. Documented as idempotent in the XML doc.

### Critical Issue #6: Wrong SaveFileHandler registration — FIXED
**Problem:** `SaveModuleServices.Register` registered `Modules.Save.SaveFileHandler`
(uses `AppContext.BaseDirectory`) as the save handler. The Adapter-layer
`Adapter.Persistence.SaveFileHandler` (uses `ProjectSettings.GlobalizePath`) existed
but was dead code. Saves were written next to the .dll instead of in Godot's user data dir.

**Fix (architectural):**
- Added `ISaveFileHandler` interface in `src/Core/Interfaces/ISaveFileHandler.cs` (engine-agnostic, flat-file API: Save/Load/HasSave/DeleteSave/GetAllSaves).
- `Modules.Save.SaveFileHandler` now implements `ISaveFileHandler` (no API change — methods already match). This is the Modules-layer default for headless tests.
- `Adapter.Persistence.SaveFileHandler` now implements `ISaveFileHandler` via explicit interface methods (Save/Load/HasSave/DeleteSave/GetAllSaves wrappers around the existing slot-directory layout, using `main.json` per slot).
- `SaveDataAggregator` now depends on `ISaveFileHandler` (interface) instead of the concrete `SaveFileHandler`.
- `SaveModuleServices.Register` now also registers `ISaveFileHandler → Modules.Save.SaveFileHandler` (default for tests).
- `GameLifetimeScope.Build()` now takes an optional `Action<IContainerBuilder>? configureAdapter` callback invoked AFTER all modules register, BEFORE Build().
- `GameBoot._Ready()` passes an Adapter-override callback that uses `RegisterInstance` to register a pre-built `Adapter.Persistence.SaveFileHandler` (calls ProjectSettings.GlobalizePath) as both `SaveFileHandler` and `ISaveFileHandler`, overriding the Modules-layer default.

  > NOTE on `RegisterInstance` vs `Register<T>`: The Adapter.Persistence.SaveFileHandler has two constructors — parameterless (uses `ProjectSettings.GlobalizePath("res://saves")`) and `(string saveRoot)` for tests. The DI container picks the greediest ctor (`string saveRoot`), then fails to resolve the string parameter. Using `RegisterInstance` with a pre-built instance bypasses this issue and ensures the parameterless ctor is called.

### Important Issue #15: Hardcoded world bounds `49` — FIXED
**Problem:** `Math.Clamp(newX, 0, 49)` — hardcoded 49 instead of MapWidth-1.
Future world-size changes would break.

**Fix:**
- `src/Core/Data/Constants.cs` — added `DEFAULT_MAP_WIDTH = 50` and `DEFAULT_MAP_HEIGHT = 50` constants.
- `src/Modules/Player/PlayerModule.cs`:
  - Injected `ITileService _tileService`.
  - Added `MaxX` / `MaxY` properties that read `TileService.MapWidth/MapHeight - 1`, falling back to `DEFAULT_MAP_WIDTH/HEIGHT - 1` when the grid is not yet generated.
  - Replaced all `Math.Clamp(..., 0, 49)` with `Math.Clamp(..., 0, MaxX/MaxY)` in `HandleKeyboardMovement`, `HandleMouseMovement`, `SetMouseDestination`.
- `src/Adapter/Scene/GameWorldController.cs`:
  - `SetupWorld`: resolved `_worldWidth` / `_worldHeight` from `Tiles.MapWidth/MapHeight` (falling back to defaults), used in camera limits.
  - `HandleFreeMovement`: replaced `50 * GameConstants.TILE_PIXELS` with `mapW * GameConstants.TILE_PIXELS`, replaced `Mathf.Clamp(..., 0, 49)` with `Mathf.Clamp(..., 0, mapW - 1)`.
  - `HandleMouseClick`: same — replaced `Mathf.Clamp(..., 0, 49)` with `Mathf.Clamp(..., 0, mapW - 1)`.
- `src/Adapter/Scene/SceneBuilder.cs`:
  - `SetupTerrainMesh`: replaced `const int width = 50; const int height = 50;` with `TileService.MapWidth/MapHeight` lookups (falling back to defaults).

### Important Issue #16: Invalid UID in MainMenu.tscn — FIXED
**Problem:** `uid://q7qb2mav6ygx` in `scenes/MainMenu.tscn` was invalid — Godot logged a warning and fell back to the text path.

**Fix:**
- `scenes/MainMenu.tscn` — removed the `uid="uid://q7qb2mav6ygx"` attribute from the `[ext_resource]` line. Godot will regenerate a valid UID on next editor open.

### Duplicate using warnings (CS0105) cleanup — FIXED
**Problem:** 32 files in `src/Modules/` had duplicate `using CultivationGame.Core.Messaging.Contracts;` or `using CultivationGame.Core.Data;` directives — generating 32 CS0105 warnings.

**Fix:**
- Ran a Python script that walks every `.cs` file under `src/Modules/`, finds duplicate `using ...;` lines, and removes the second occurrence. 32 files cleaned up:
  - Charger (3): ChargerBuffer, ChargerHeat, ChargerService
  - Combat (6): CombatConsequencesService, CombatService, DamageService, ElementalEffectService, TechniqueChargeService, TechniqueService
  - Formation (1): FormationService
  - Generator (2): ItemGeneratorService, TechniqueGeneratorService
  - Interaction (3): DialoguePresenter, DialogueService, InteractionService
  - Inventory (6): BackpackService, CraftingService, EquipmentService, InventoryService, StorageRingService, StorageService
  - NPC (10): NPCAIService, NPCAssemblyService, NPCCombatAdapter, NPCMovementService, NPCNameGenerator, NPCQiRegenService, NPCRelationshipService, NPCService, NPCSpawnerService, NPCSpeciesSelector, SoulGenerator

### Audit of NEW issues (informational — NOT fixed in this pass)

#### SurfaceTransitionRenderer.cs — OK ✓
- Compiles cleanly.
- `_Draw()` iterates all `w × h` tiles and checks 8 neighbors per tile. For 50×50 = 2500 tiles, that's up to 20000 transition checks (with caching of `ImageTexture` sprites via `TransitionSpriteGenerator`).
- Direction mapping is correct (N=top half, S=bottom, E=right, W=left, NW/NE/SW/SE = quarter-circle at the corresponding corner).
- Priority logic: sprite is drawn on the LOWER-priority tile, with the higher-priority neighbor's biome color. Correct.
- Diagonal-only-when-both-orthogonals-are-same-biome rule prevents multi-biome corner artifacts. Correct.
- Known issue (carried over from audit #11): `_Draw()` only runs once after `Initialize()`. If `TileService.SetTile` changes a tile, the renderer does not redraw. Not fixed in this pass.

#### TransitionSpriteGenerator.cs — OK ✓
- 8-direction sprite generation:
  - Straight (N/S/E/W): FillHalf / FillHalfVertical — fills half the tile with the biome color.
  - Diagonal (NW/NE/SW/SE): FillQuarterCircle at the appropriate corner (cx, cy) with radius `size * 0.5`.
  - Anti-aliasing on the diagonal edge (1-pixel transition based on distance to radius).
- Static cache (`Dictionary<(BiomeType, Direction), ImageTexture>`) — 80 sprites max (10 pairs × 8 directions, but only the 9 base biomes are reachable, so max ~72 sprites).
- No issues found.

#### BiomeType.cs — OK ✓
- Defined in `src/Core/Data/BiomeType.cs`. Included in build (compiles cleanly, headless run loads it).
- 9 base biomes (Ocean, Sea, Coast, Grassland, Steppe, Forest, Highlands, Mountains, Peak) + 7 legacy aliases (Plains=Grassland, Desert=Steppe, Swamp=Forest, Tundra=Highlands, Jungle=Forest, Volcanic=Mountains, Spiritual=Peak).
- **Note:** `TileService.MapToBiome(elevation)` only generates 7 of the 9 biomes — `Steppe` and `Forest` are never produced by the elevation thresholds. The biome colors and transition sprites for these biomes are defined but unused. Not a bug per se — the test polygon just doesn't have those biomes — but worth noting if future maps expect them.

#### GameWorldController.cs free movement code — DOUBLE-MOVEMENT BUG FOUND (not fixed)
**Issue:** Both `PlayerModule.HandleKeyboardMovement` (tick-based, called from `GameBoot._PhysicsProcess` via `GameEntryPoint.Tick`) AND `GameWorldController.HandleFreeMovement` (frame-based, called from `GameWorldController._PhysicsProcess`) read WASD input and move the player via `PlayerService.SetPosition` / `Player.MoveTo`.

**Conflict scenario at TimeSpeed.Normal (1 tick/sec):**
1. WASD pressed → GameWorldController.HandleFreeMovement moves `_visualPosition` smoothly each frame, calls `Player.MoveTo(tileX, tileY)` when crossing tile boundary.
2. Once per second, PlayerModule.HandleKeyboardMovement reads `_playerInputService.MoveDirection`, calls `_playerService.SetPosition(newX, newY)` — moves player by 2 tiles in one tick.
3. Next frame, GameWorldController.HandleFreeMovement reads `Player.Position` (the new tile from step 2), but `_visualPosition` is still at the old tile. The "if currentTile != tileX" check fires and calls `Player.MoveTo(oldTileX, oldTileY)` — **moving the player BACK**.

**Same conflict for mouse-click movement:**
- `GameWorldController.HandleMouseClick` sets BOTH `_mouseTarget` (for pixel movement in HandleFreeMovement) AND `playerModule.SetMouseDestination(tileX, tileY)` (for tick-based tile movement in HandleMouseMovement).

**Recommended fix (not applied):**
Disable `PlayerModule.HandleKeyboardMovement` and `HandleMouseMovement` (make `Tick()` a no-op for movement) now that `GameWorldController.HandleFreeMovement` is the canonical movement source. Update the comment in `GameWorldController.cs` (lines 65-68) which still says "Movement is handled by PlayerModule.Tick()" — that comment is stale.

#### SceneBuilder biome colors rendering — OK ✓
- `SetupTerrainMesh` creates a `MultiMeshInstance2D` with one quad per tile (50×50 = 2500 instances in a single draw call).
- Each tile gets its biome color via `BiomeColors.Get(tile.Biome)` (muted palette: 9 biomes + default fallback).
- `CanvasModulate` applies a warm daylight tint (1.05, 1.0, 0.95).
- No issues found.

### Build status

```
dotnet build: 0 errors, 224 warnings (down from 256 — saved 32 CS0105 warnings)
Headless run: 17 startables, 16 tickables, all started without errors
SaveFileHandler: Adapter.Persistence.SaveFileHandler registered as ISaveFileHandler
```

---

## Stage Summary

- **Errors fixed:** 6 critical/important issues from 08_15 audit:
  - #2 Sticky flags race condition (PlayerModule + GameWorldController)
  - #3 Double Spawn (PlayerService idempotent guard)
  - #6 Wrong SaveFileHandler (ISaveFileHandler interface + Adapter override)
  - #15 Hardcoded world bounds 49 (PlayerModule + GameWorldController + SceneBuilder + Constants)
  - #16 Invalid UID in MainMenu.tscn
  - CS0105 duplicate usings cleanup (32 files)

- **Warnings reduced:** 256 → 224 (32 fewer)
  - Removed: 32 × CS0105 (duplicate using directives)
  - Remaining: 186 × CS8618 ([Inject] non-nullable fields — DI pattern), 144 × CS8625 (null! in non-nullable), 50 × CS8603 (nullable return), 26 × CS8600 (null conversion), 18 × CS0414 (unused [Inject] fields), 10 × CS8604 / 10 × CS8601 (null in nullable arg), 2 × CS8629 (nullable value type), 2 × CS0649 ([Inject] field never assigned). All remaining are nullable-reference-type warnings from the [Inject] reflection-based DI pattern — would require either `= null!` initializers or `#pragma warning disable` to suppress.

- **Remaining issues (not fixed in this pass):**
  - **NEW: Double-movement bug** — PlayerModule.HandleKeyboardMovement and GameWorldController.HandleFreeMovement both move the player. Same for mouse-click (PlayerModule.SetMouseDestination + GameWorldController._mouseTarget). Recommended fix: disable PlayerModule movement, let GameWorldController.HandleFreeMovement be the sole source.
  - **#7 Adapter contains game logic** — HandleStickyInput calls Time.Pause()/Resume(), changes Time.Speed. Should be in a Module.
  - **#9 Adapter directly resolves PlayerModule** — `container.Resolve<PlayerModule>()` + `SetMouseDestination()` from GameWorldController.HandleMouseClick. Should go via EventBus.
  - **#10 SetOverUI never called** — InputAdapter.SetOverUI exists but no UI panel calls it.
  - **#11 (carried over to SurfaceTransitionRenderer)** — renderer doesn't redraw on TileChangedEvent.
  - **#12 Double tile generation** — TileModule.Start + TileMapGenPhase both call Generate.
  - **#13 Dead alias checks** — `IsSticky("j")`, `IsSticky("e")` etc. in PlayerInputService (InputAdapter never adds the single-letter aliases).
  - **#14 WorldConfig.StartHour ignored** — TimeService.CurrentTime hardcoded to 06:00.
  - **#18 IsPaused only via Speed==Paused** — no independent pause flag.
  - **#20 TimeService doesn't implement ITickable** — WorldModule.Tick casts to concrete.
  - Cosmetic: debug logs in production, two classes in WorldService.cs, _debugFrameCount in production.

- **Files changed:** 41 modified + 1 new
  - New: `src/Core/Interfaces/ISaveFileHandler.cs`
  - Adapter: `Persistence/SaveFileHandler.cs`, `Scene/GameBoot.cs`, `Scene/GameWorldController.cs`, `Scene/SceneBuilder.cs`
  - Core: `Data/Constants.cs`
  - Entry: `GameLifetimeScope.cs`
  - Modules: `Player/PlayerModule.cs`, `Player/PlayerService.cs`, `Save/SaveDataAggregator.cs`, `Save/SaveFileHandler.cs`, `Save/SaveModule.cs`
  - 32 Modules files: duplicate `using` cleanup (Charger/Combat/Formation/Generator/Interaction/Inventory/NPC subdirectories)
  - Scene: `scenes/MainMenu.tscn` (uid removed)

---

## Next actions (recommended order)

1. **Fix the double-movement bug** — disable `PlayerModule.HandleKeyboardMovement` / `HandleMouseMovement`. Update the stale comment in `GameWorldController.cs:65-68`.
2. **Move game logic out of Adapter** — create `TimeControlModule` and route `HandleStickyInput` (pause/speed/save) through it via EventBus.
3. **Replace direct PlayerModule access with EventBus** — publish `MouseClickEvent(tileX, tileY)` from Adapter; PlayerModule subscribes.
4. **Wire up SetOverUI** — when UI panels are added, connect their `mouse_entered/exited` signals to `InputAdapter.SetOverUI`.
5. **Subscribe SurfaceTransitionRenderer to TileChangedEvent** — call `QueueRedraw()` (with throttle) when tiles change.
6. **Remove duplicate tile generation** — either remove `TileModule.Start` Generate call, or skip it in `TileMapGenPhase`.
7. **Clean up dead alias checks** in PlayerInputService — remove `IsSticky("j")`, `IsSticky("e")`, etc.
8. **Apply WorldConfig.StartHour** in `TimeService` initialization.
9. **Suppress remaining warnings** — `= null!` for [Inject] fields, `#pragma warning disable CS0414` for unused.
