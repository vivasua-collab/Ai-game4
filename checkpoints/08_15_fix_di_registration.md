# Чекпоинт: DI concrete type registration fixed

**Дата:** 2026-08-15
**Тип:** migration | fix | di
**Task ID:** 11
**Agent:** fix-di-registration

---

## Контекст

Ai-game4 build succeeds (0 errors), but runtime DI failed: `NPCAIService` constructor requires
concrete `NPCService`, but `NPCModuleServices` only registered `INPCService` via
`builder.Register<INPCService, NPCService>(Lifetime.Singleton)`. The Container couldn't find a
registration for `NPCService` (concrete type) and threw.

The Ai-game3 services use **constructor injection** with concrete-type parameters (VContainer
pattern), but our DI container only keyed registrations by interface.

## Approach chosen: Option A — modify `Container.cs`

Rather than touching all 16 `*ModuleServices.cs` files, the fix is in
`game/src/Core/DI/Container.cs`. `Register<TInterface, TImplementation>` now also registers
`TImplementation` as a forwarded key pointing to the **same** `Registration` object, so a single
singleton is shared between interface and concrete-type resolutions.

## Changes to `Container.cs`

### 1. `Register<TInterface, TImplementation>` — forwarding registration

Both `typeof(TInterface)` and `typeof(TImplementation)` now map to the same `Registration`
instance (when they differ). This means a later `Resolve<NPCService>()` finds the same
registration record that `Resolve<INPCService>()` would use.

```csharp
var reg = new Registration(typeof(TInterface), typeof(TImplementation), lifetime, null);
_registrations[typeof(TInterface)] = reg;
if (typeof(TInterface) != typeof(TImplementation))
{
    _registrations[typeof(TImplementation)] = reg;  // forwarded key
}
_orderedRegistrations.Add(reg);
```

### 2. `Resolve(Type, ...)` — shared singleton cache

The original cache only keyed by `serviceType`. With forwarding, `Resolve<INPCService>` and
`Resolve<NPCService>` would have produced **two** separate singletons. The lookup and store now
use both `serviceType` and `reg.ImplementationType`:

- **Lookup**: check `_singletons[serviceType]` first, then fall back to
  `_singletons[reg.ImplementationType]`.
- **Store**: write to both keys when they differ.

This guarantees that whichever key is resolved first constructs the instance, and the second
resolution returns the cached one.

### 3. `ResolveAll<T>()` — reference dedup

Because the same `Registration` object is now a value for two dictionary keys,
`_registrations.Values` would yield it twice → `ResolveAll<IEventSubscriber>()` would subscribe
the same handler twice. Added `HashSet<Registration>(ReferenceEqualityComparer.Instance)` to
skip duplicates.

### 4. `Dispose()` — reference dedup

Same reason: `_singletons.Values` can now contain the same instance under multiple keys.
Dedupe by reference before calling `Dispose()` to avoid double-disposal.

## ModuleServices changes

None. All 16 `*ModuleServices.cs` files left untouched — the Container-side fix covers them
all.

## Verification

### Build

```
255 Warning(s)
0 Error(s)
Time Elapsed 00:00:02.29
```

### Headless run

```
[WorldService] Active location: Test Polygon (id=test_polygon)
[WorldModule] Started — time 1864-01-01 06:00, speed Normal
[TileService] Generated 50x50 grid, seed=12345, baseTerrain=Grass
[TileModule] Started — generated 50x50 grid
[PlayerService] Player spawned @ (0, 0), hp 100
[PlayerModule] Started — stat svc wired=True
[QuestModule] Started
[UIModule] Started — HUD shown
[SaveModule] Started
[ItemDatabase] Initialized (no Resources catalogue — populate via Register())
[GeneratorModule] База данных предметов инициализирована
[GeneratorModule] Модуль запущен. Зарегистрировано предметов: 0
[GameEntryPoint] Started. 17 startables, 16 tickables, session=GameSession
[GameBoot] Game initialized. Container built and entry point started.
[MainMenu] Ready
[GameBoot] Game shutdown.
```

- `grep -iE "(error|exception|traceback|cannot resolve|missing|fail|throw)"` → **no output**
- All 16 modules started cleanly
- GameBoot reached `Game initialized`
- MainMenu reached `Ready`
- Clean shutdown (no exceptions, no DI errors)

## Stage Summary

- DI errors fixed: 1 (concrete-type registration via forwarding)
- Headless run reaches GameBoot Ready: **yes**
- Remaining issues:
  - 255 build warnings (unused fields — cosmetic, not blocking; pre-existing from transfer)
  - `GeneratorModule` reports 0 items registered (Resources catalogue not populated — feature gap, not a bug)
  - `ItemDatabase` notes "no Resources catalogue" (same as above)

## Next actions

1. Git commit + push (DI fix is ready)
2. Optionally clean up the 255 unused-field warnings
3. Populate the Items Resources catalogue so GeneratorModule registers items
4. Continue with screenshots / next checkpoint
