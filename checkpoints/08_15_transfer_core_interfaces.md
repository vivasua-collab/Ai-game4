# Checkpoint: Transfer Core/Interfaces from Ai-game3 to Ai-game4

**Date:** 2026-08-15
**Task ID:** 7-b
**Agent:** transfer-core-interfaces
**Type:** migration

---

## Context

Ai-game3 (Unity iteration) has 37 battle-tested interface files in
`Core/Interfaces/`. Ai-game4 (Godot 4.7.1) had 24 stubs. This task
transfers the richer Ai-game3 versions to replace the stubs and adds
the missing 13+ interfaces, normalizing each file to be engine-agnostic.

## Work Log

### Source → Target
- **SOURCE:** `/home/z/my-project/Ai-game3-ref/UnityProject/Assets/Scripts/Core/Interfaces/`
- **TARGET:** `/home/z/my-project/Ai-game4/game/src/Core/Interfaces/`

### Files Transferred (37 total)
**Replaced (20) — existed in target as stubs:**
IBodyService.cs, IBuffService.cs, IChargerService.cs, ICombatService.cs,
IEquipmentService.cs, IFormationService.cs, IInteractionService.cs,
IInventoryService.cs, INPCService.cs, IPlayerInputService.cs,
IPlayerService.cs, IQiService.cs, IQuestService.cs, ISaveService.cs,
ISceneAssemblyPhase.cs, IStatService.cs, ITileService.cs, ITimeService.cs,
IUIService.cs, IWorldService.cs

**Added (17) — new to target:**
IBodyDataProvider.cs, ICraftingService.cs, ICurrencyService.cs,
IEquipmentDataProvider.cs, IEventService.cs, IInputLogService.cs,
IItemDatabaseService.cs, IItemGeneratorService.cs, IPerkService.cs,
IQiDataProvider.cs, IQuestRewardService.cs, IResourceService.cs,
IStaminaService.cs, IStatProvider.cs, IStorageRingService.cs,
IStorageService.cs, ITechniqueGeneratorService.cs

**Preserved (4) — Ai-game4 originals, NOT in transfer list:**
IModule.cs, IGeneratorService.cs, ISaveable.cs, IGameSession.cs

### Transformations applied per file
1. `#nullable enable` prepended at top.
2. Removed `using UnityEngine;`, `using MessagePipe;`, `using VContainer;`,
   `using Cysharp.Threading.Tasks;`, `using CultivationGame.Core.Messaging;`
   (Ai-game4 has no `CultivationGame.Core.Messaging` namespace — only
   `.Contracts` and `.Events`).
3. Namespace normalized: `CultivationGame.Core` → `CultivationGame.Core.Interfaces`
   (3 files in Ai-game3 already used `.Interfaces` — kept).
4. Injected `using CultivationGame.Core;` and `using CultivationGame.Core.Data;`
   — needed because Ai-game4 keeps some types (StatType, Element, Morphology,
   ControlType, ...) in `CultivationGame.Core` namespace while others
   (Position2D, NPCData, etc.) live in `CultivationGame.Core.Data`.
5. **ISceneAssemblyPhase.cs:** `UniTask ExecuteAsync()` → `Task ExecuteAsync()`
   + `using System.Threading.Tasks;` (engine-agnostic alternative to UniTask).
6. **ISaveService.cs:** removed inline `ISaveable` interface block —
   Ai-game4 already has standalone `ISaveable.cs` with engine-agnostic
   signature (`object CaptureState()` / `void RestoreState(object state)`
   vs Ai-game3's `string CaptureState()`).

### Build Status
```
cd /home/z/my-project/Ai-game4/game && dotnet build
```
- **Total errors:** 476 (down from 736 before transfer script fixes)
- **Errors in Interfaces/:** 62 — ALL are CS0246 (missing types:
  `ItemData`, `EquipmentData`, `HarvestResult`, `BodyPart`, `HeatState`,
  `ChargerSlotState`). These will resolve when next agent transfers
  Core/Data and Modules/Body.
- **Errors in Modules/Entry/Adapter:** 414 — EXPECTED. Stub implementations
  don't match the new richer interfaces. Next agent will fix.
- **Errors caused by the interface files themselves (syntax/namespace/etc.):** 0 ✅

## Stage Summary

- **Core/Interfaces files:** 41 (37 transferred + 4 preserved Ai-game4 originals)
- **Compile errors in interface files:** 62 — all CS0246 missing-type
  (forward references to Core/Data + Modules/Body types not yet transferred)
- **Notable interface enrichments (vs old stubs):**
  - `ICombatService`: full damage pipeline — `DamageRequest`/`DamageResult`/
    `DefenseContext` readonly structs with 25+ fields (AttackerStats, DefenderStats,
    Penetration, TargetMorphology, AttackSubtype, IsPlayerTarget, etc.); added
    `IDamageService` interface; `ExecuteAttack` 4-arg overload.
  - `IQiService`: full cultivation system — `long CurrentQi/MaxQi/CoreCapacity`
    (zero-float rule), `CultivationLevel`/`SubLevel`/`CoreQuality`, breakthrough
    methods (`CanBreakthrough`/`TryBreakthrough`/`CalculateBreakthroughRequirement`),
    `Conductivity`/`ConductivityBonus`. Added `IQiBufferService` interface +
    `QiBufferResult` struct + `QiBufferMode` enum.
  - `IBodyService`: `Initialize`/`ProcessRegeneration`/`RecalculateHPFromVitality`/
    `ReattachPart`/`GetMorphology`/`GetSizeClass`. Added `BodyPartData` struct
    with `BodyPartFunction`, `BaseHitChance`.
  - `IPlayerService`: `PlayerId`/`IsAlive`/`IsSleeping`/`SleepState`/`Stance`/
    `StartSleep`/`WakeUp`/`CultivationLevel`/`GetCurrentQi`/`Tick`.
  - `IPlayerInputService`: 18 input flags (LMB/RMB/hotbar/meditate/etc.),
    `UpdateInputState(InputFrameData)` — replaces 8-param signature.
  - `INPCService`: `GetNearbyNPCIds`/`Attitude`/`GetAIState`/`SetAIState`/
    `UpdatePosition`/`GetNPCState`. Added `INPCSpawnerService` interface
    with full `SpawnNPC(speciesId, roleId, locationLevel, position, seed)`.
  - `IChargerService`: full charger state — `IsOperational`/`HeatLevel`/`HeatState`/
    `BufferQi`/`BufferCapacity`/`SlotCount`/`ActiveSlotsCount`, slot operations
    (`GetSlotState`/`TryCharge`/`TryDischarge`), combat mode, `Tick()`. Added
    `ChargerMode` enum.
  - `IFormationService`: full formation lifecycle — `StartDrawing`/`StartFilling`/
    `ContributeQi`/`ActivateFormation`/`DeactivateFormation`, `GetFormationBonus`/
    `GetFormationBonusPermil`, `QiPoolCurrent`/`QiPoolMax`/`ParticipantCount`.
    Added `FormationEffectData` struct.
  - `IEquipmentService`: `IsSlotBlocked`/`GetTotalArmor`/`GetTotalDamage`/
    `WeaponHandType`/`IsTwoHandEquipped`.
  - `IEquipmentDataProvider`: per-entity equipment data — `SetEquipment`/
    `SetTotalArmor`/`SetTotalDamage`/`GetArmorCoverage`/`InvalidateCache`.
  - `IQiDataProvider`: per-entity Qi state — `SetQiState`/`GetCultivationLevel`/
    `SetCultivationLevel`/QiBuffer methods/`TryConsumeQi`.
  - `IBodyDataProvider`: per-entity BodyParts — `SetBodyParts`/`HasEntity`/
    `RemoveEntity`/`GetCurrentHealth`/`GetMaxHealth`/`IsEntityAlive`.
  - `IBuffService`: `ApplyBuff`/`RemoveBuff`/`RemoveAllBuffs`/`HasBuff`,
    `GetStatModifier`/`GetStatModifierPermil`/`GetElementResistance`/
    `HasImmunity`/`GetActiveBuffs`/`TickBuffs`. Added `ActiveBuffData` struct.
  - `IInventoryService`: weight/volume model — `CanFitItem`/`HowManyCanFit`/
    `GetCurrentWeight`/`GetCurrentVolume`/`GetEffectiveMaxWeight`/`GetEffectiveMaxVolume`.
  - `ITimeService`: Ai-game3 returns `float DeltaTime`/`TotalTime` +
    `CurrentDay`/`Month`/`Year`/`Hour`/`TimeOfDay`. NOTE: Ai-game4's stub
    had richer `WorldTime CurrentTime`/`TickCount`/`event OnTick` — those are
    LOST in this replacement. (Next agent may need to merge back.)
  - `IWorldService`: `CurrentLocationId`/`CurrentSectorId`/`TryTravel`/
    `GetLocation`/`GetFaction`/`GetFactionRelation`/`GetDiscoveredSectors`/
    `IsSectorDiscovered`. Added `LocationInfo`/`FactionInfo` structs.
  - `IItemDatabaseService`/`IItemGeneratorService`/`ITechniqueGeneratorService`:
    full item/technique generation pipeline (for-level, loot, multi-gen).
  - `IStatProvider`: unified stat access for combat — `GetStat`/`GetElement`/
    `GetMaterial`/`GetMorphology` (player OR NPC).
  - `IStatService`: `GetStatBonus`/`ModifyStat`/`SetStat`/`GetStatDomain`/
    `GetVirtualDelta`/`AddVirtualDelta`/`ConsolidateSleep`/`GetThreshold`/`CanAdvance`.
  - `ISceneAssemblyPhase`: `PhaseName`/`Order`/`State`/`CanExecute`/`BlockReason`/
    `ExecuteAsync` (Task, was UniTask)/`MarkAsSkipped`/`Reset`/`SkipOnLoad`.
    Added `SceneAssemblyPhaseState` enum + `ISceneAssemblyLogger` interface.

## Decisions
- **Replace, don't merge** — task rule #4. Ai-game4 stubs fully replaced with
  Ai-game3 versions. (Known regression: ITimeService lost `WorldTime`/`TickCount`/
  `OnTick` event — Ai-game3 uses `float DeltaTime`/`TotalTime` + discrete
  properties. Next agent should reconcile, possibly by merging.)
- **Keep `using CultivationGame.Core;`** — required because Ai-game4 keeps
  enums (StatType, Element, Morphology, ControlType) in `CultivationGame.Core`
  namespace while data types (Position2D, NPCData) live in `CultivationGame.Core.Data`.
- **Remove inline ISaveable from ISaveService.cs** — Ai-game4 has standalone
  `ISaveable.cs` with engine-agnostic signature. Avoids duplicate type def.
- **UniTask → Task** — engine-agnostic. Ai-game4's existing
  `AbstractSceneAssemblyPhase.cs` already uses `Task` (not UniTask), so this
  aligns with existing Ai-game4 conventions.

## Known Issues / Forward References
The 62 interface-file compile errors are all CS0246 for types NOT YET in
Ai-game4. They will resolve when sibling tasks complete:
- `ItemData` (30 uses) — Core/Data transfer (sibling task)
- `EquipmentData` (18 uses) — Core/Data transfer
- `HarvestResult` (4 uses) — Core/Data transfer
- `GameTile` (already in Ai-game4 Core/Data/GameTile.cs at `CultivationGame.Core.Data`
  — but interfaces need `using CultivationGame.Core.Data;` which has been added)
- `BodyPart` (4 uses) — Modules/Body transfer (sibling task)
- `HeatState`, `ChargerSlotState` (4 uses) — Modules/Charger transfer

The 414 errors in Modules/Entry/Adapter are EXPECTED — stub implementations
don't match the new richer interfaces. Next agent will rewrite the modules.

## Files
- `/home/z/my-project/Ai-game4/game/src/Core/Interfaces/*.cs` — 37 transferred
  + 4 preserved = 41 total files.

## Next Steps
1. **Transfer Core/Data from Ai-game3** — adds `ItemData`, `EquipmentData`,
   `HarvestResult`, and many other types referenced by the new interfaces.
2. **Transfer Modules/Body** — adds `BodyPart` type referenced by `IBodyDataProvider`.
3. **Transfer Modules/Charger** — adds `HeatState`, `ChargerSlotState`.
4. **Update Modules/Entry/Adapter** — rewrite stubs to implement the new
   richer interfaces.
5. **Reconcile ITimeService** — decide whether to merge Ai-game4's
   `WorldTime`/`TickCount`/`OnTick` event back into the Ai-game3 version
   (which uses `float DeltaTime`/`TotalTime` + discrete properties).
