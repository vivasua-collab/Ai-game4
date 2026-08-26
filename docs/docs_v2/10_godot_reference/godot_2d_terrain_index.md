# Godot 4.x 2D Terrain — Documentation Index

> **Task ID:** IDX-1
> **Agent:** index-godot-docs
> **Purpose:** Index of official Godot 4.7 documentation pages relevant to 2D top-down terrain generation and rendering (pure 2D, NO Z coordinate).
> **Source:** Official Godot Engine docs (Russian 4.x branch + English stable), fetched via `z-ai function -n page_reader`.
> **Status:** READ-ONLY research artifact. No code was written.

---

## 0. Pages Indexed — Summary

| # | Page | URL | HTTP | Chars | Status |
|---|------|-----|------|-------|--------|
| 1 | Godot Docs — main landing (RU 4.x) | https://docs.godotengine.org/ru/4.x/ | 200 | 3 088 | Landing page only; confirms 4.7 RU branch |
| 2 | 2D tutorials index (RU 4.x) | https://docs.godotengine.org/ru/4.x/tutorials/2d/index.html | 200 | 1 424 | Toctree of all 2D sub-pages |
| 3 | Using TileSets (RU 4.x) | https://docs.godotengine.org/ru/4.x/tutorials/2d/using_tilesets.html | 200 | 25 324 | Full content |
| 4 | Using TileMaps / TileMapLayer (EN stable) | https://docs.godotengine.org/en/stable/tutorials/2d/using_tilemaps.html | 200 | 16 071 | Full content |
| 5 | 2D Particle Systems (RU 4.x) | https://docs.godotengine.org/ru/4.x/tutorials/2d/particle_systems_2d.html | 200 | 9 909 | Full content |
| 6 | Custom drawing in 2D (RU 4.x) | https://docs.godotengine.org/ru/4.x/tutorials/2d/custom_drawing_in_2d.html | 200 | 30 583 | Full content |
| 7 | 2D lights and shadows (RU 4.x) | https://docs.godotengine.org/ru/4.x/tutorials/2d/2d_lights_and_shadows.html | 200 | 17 995 | Full content |

### URL corrections applied during fetch

The original task brief contained two stale URLs that return 404 on the current Godot docs site. The correct 4.7 URLs were discovered from the 2D tutorials index (page #2) and used instead:

- **TileMap** — `tutorials/2d/using_tilemap.html` (singular) → **404 Not Found**. Correct URL: `tutorials/2d/using_tilemaps.html` (plural). In 4.5+ the page documents the new `TileMapLayer` node (the legacy `TileMap` node is deprecated).
- **Particle systems** — `tutorials/2d/2d_particle_systems.html` → **404 Not Found** (Страница не найдена). Correct URL: `tutorials/2d/particle_systems_2d.html`.

Both corrections are confirmed by the toctree on page #2.

---

## 1. Page #1 — Godot Docs main landing (RU 4.x)

- **URL:** https://docs.godotengine.org/ru/4.x/
- **Title:** `Godot Docs – master branch`
- **HTTP:** 200 OK
- **Content:** Russian-translation landing page. Confirms the doc branch corresponds to **Godot Engine 4.7**. Notes that translations are only kept on the stable branch; Russian pages for other versions fall back to English.
- **Key classes/nodes:** None (landing page only).
- **Terrain techniques:** None directly.
- **Code patterns:** None.
- **Sub-pages worth reading:** links into "Step by step", "Manual", "Tutorials" — not terrain-specific.

---

## 2. Page #2 — 2D tutorials index (RU 4.x)

- **URL:** https://docs.godotengine.org/ru/4.x/tutorials/2d/index.html
- **Title:** `2D`
- **HTTP:** 200 OK
- **Content:** Toctree of the entire 2D tutorial section. Confirms Godot ships a dedicated 2D renderer + 2D physics engine + tilemaps, particles, animation systems.
- **Key classes/nodes mentioned:** `TileMap`, `TileMapLayer`, `TileSet`, `TileSetAtlasSource`, `TileSetScenesCollectionSource`, `TileSetSource`, `TileMapPattern`, `TileData`, `CPUParticles2D`, `GPUParticles2D`, `ParticleProcessMaterial`.

### 2D tutorial sub-pages (authoritative URL list)

These are the actual sibling pages of the 2D tutorial section (extracted from the page's toctree). The list below is the corrected, current URL set for 4.7:

| Sub-page | Filename (under `tutorials/2d/`) | Terrain relevance |
|----------|----------------------------------|-------------------|
| Introduction to 2D | `introduction_to_2d.html` | Coordinate system, units |
| Canvas layers | `2d_transforms.html` (linked via "Слои холста") | Layer ordering for terrain depth |
| 2D lights and shadows | `2d_lights_and_shadows.html` | **HIGH** — page #7 |
| 2D meshes | `2d_meshes.html` | Medium — `Polygon2D`/mesh conversion |
| 2D sprite animation | `2d_sprite_animation.html` | Low for terrain |
| 2D particle systems | `particle_systems_2d.html` | **CORRECT URL** — page #5 |
| Using ParticleProcessMaterial 2D | `particle_process_material_2d.html` | Medium — particle tuning |
| 2D antialiasing | `2d_antialiasing.html` | Medium — line/polygon AA |
| Custom drawing in 2D | `custom_drawing_in_2d.html` | **HIGH** — page #6 |
| 2D parallax | `2d_parallax.html` | **HIGH** — depth/background terrain |
| 2D movement | `2d_movement.html` | Low for terrain |
| Using TileSets | `using_tilesets.html` | **HIGH** — page #3 |
| Using TileMaps | `using_tilemaps.html` | **HIGH** — page #4 (covers `TileMapLayer`) |

### Additional referenced pages (outside `tutorials/2d/`)

- `../animation/2d_skeletons.html` — 2D skeletons (cut-out animation)
- `../navigation/navigation_using_navigationmeshes.html` — baking navigation meshes with `NavigationRegion2D` / `NavigationServer2D` (recommended over built-in TileMap navigation)
- `../physics/using_area_2d.html`, `../physics/using_character_body_2d.html`, `../physics/ray-casting.html` — 2D physics
- `../rendering/viewports.html`, `../rendering/renderers.html` — rendering pipeline
- `../shaders/your_first_shader/your_first_2d_shader.html` — first CanvasItem shader
- `../shaders/shader_reference/canvas_item_shader.html` — CanvasItem shader reference (incl. light built-ins)
- `../../engine_details/architecture/2d_coordinate_systems.html` — 2D coordinate systems deep dive
- `../performance/using_multimesh.html` — **MultiMesh** performance pattern (for 3D, but the 2D analogue is `MultiMeshInstance2D`)

### Class reference pages linked from the index

- `../../classes/class_tilemaplayer.html`
- `../../classes/class_tilemap.html` (legacy)
- `../../classes/class_tileset.html`
- `../../classes/class_tilesetatlassource.html`
- `../../classes/class_tilesetscenescollectionsource.html`
- `../../classes/class_tilesetsource.html`
- `../../classes/class_tilemappattern.html`
- `../../classes/class_tiledata.html`
- `../../classes/class_cpuparticles2d.html`
- `../../classes/class_gpuparticles2d.html`
- `../../classes/class_particleprocessmaterial.html`

---

## 3. Page #3 — Using TileSets (RU 4.x)

- **URL:** https://docs.godotengine.org/ru/4.x/tutorials/2d/using_tilesets.html
- **Title:** `TileSets (Тайловые наборы)`
- **HTTP:** 200 OK

### Key classes/nodes

- `TileMapLayer` — the 4.7 node that holds a tile grid (replaces the deprecated `TileMap` node).
- `TileSet` — `Resource` storing the tile collection (atlases + scene collections + terrain sets).
- `TileSetAtlasSource` — atlas-based tile source (most common).
- `TileSetScenesCollectionSource` — lets you place entire scene files as tiles (e.g., a `CPUParticles2D` scene, an `AudioStreamPlayer2D`, a shop).
- `TileSetSource` — abstract base.
- `TileMapPattern` — saved premade placement of tiles (stored in the `TileSet` so it is reusable across `TileMapLayer` nodes).
- `TileData` — per-tile data: collision/navigation/occlusion polygons, custom data, terrain bits.

### TileSet configuration properties (per atlas)

- `Tile Shape`: Square / Isometric / Half-offset square / Hexagonal.
- `Tile Layout`, `Tile Offset Axis` — for non-square shapes.
- `Rendering > UV Clipping` — clips tiles to their atlas region (prevents bleed).
- `Texture Region Size` — tile size in pixels (should match `TileMapLayer` tile size).
- `Margins`, `Separation` — atlas padding.
- `Use Texture Padding` — 1-px transparent edge to prevent texture bleed-through (recommended ON).

### TileSet layer systems (per-tile data)

A `TileSet` can define multiple **layer types** that every tile then carries:

1. **Physics Layers** — for collision shapes (one or more per tile). Press `F` in the TileSet editor for a default rectangle, then add/remove points for custom shapes.
2. **Navigation Layers** — for `NavigationRegion2D`/pathfinding polygons.
3. **Occlusion Layers** — `LightOccluder2D` polygons per tile (so terrain casts shadows from `PointLight2D` / `DirectionalLight2D`).
4. **Custom Data Layers** — arbitrary typed metadata per tile (e.g., `damage_per_second: int = 25`, `destructible: bool = false`). Useful for gameplay rules attached to terrain tiles.
5. **Terrain Sets** — autotiling (see below).

### Terrains system (autotiling — REPLACES Godot 3.x autotiles)

- A terrain set is created on the `TileSet` resource; each set contains one or more terrains.
- Each tile is tagged with `terrain_set_id` + `terrain_id` (both 0-indexed; `-1` = none).
- **Peering Bits** (8-directional) per tile control which neighbour tiles connect to which.
- Three matching modes per terrain set:
  - `Match Corners and Sides` (3×3, was Godot 3.x bitmask 3×3)
  - `Match Corners` (2×2, was 2×2 bitmask)
  - `Match Sides`
- Painting modes (in the TileMap editor, see page #4):
  - **Connect** — tiles auto-connect to surrounding tiles of the same terrain.
  - **Path** — tiles connect only to tiles painted in the *same stroke* (more artist control; lets adjacent roads stay disconnected).
  - **Tile-specific overrides** — paint exact tile IDs to resolve conflicts.

### Alternative tiles

Variants of a base tile that share the same atlas image but differ by:
- `Rendering > Flip H`, `Flip V`, `Transpose` (rotation matrix: 90° CW = `Flip H + Transpose`; 180° = `Flip H + Flip V`; 270° CW = `Flip V + Transpose`).
- `Rendering > Texture Origin` — visual offset.
- `Rendering > Modulate` — colour multiplier.
- `Rendering > Material` — per-tile `CanvasItemMaterial` or custom shader.
- `Z Index` — sort order within the layer.
- `Y Sort Origin` — vertical offset for Y-sort (top-down games).

> Tip (4.2+): you no longer need alternative tiles for flip/rotate — the TileMap editor toolbar can flip/rotate any tile at placement time.

### Tile proxies

Mapping table that says "tile ID X → tile ID Y". Auto-configured during atlas merging; can also be set manually. Useful when refactoring tilesets.

### Scene tiles (Tiles-as-Scenes)

- Place a saved `.tscn` scene as a tile (e.g., a `CPUParticles2D` for ambient fog, an `AudioStreamPlayer2D` for ambient sound, a shop the player can interact with).
- **Performance warning:** each placed scene tile instantiates a full scene. Prefer atlas tiles for plain sprites; reserve scene tiles for cases that genuinely need per-instance logic.

### Multi-tile property assignment

Two workflows:
1. **Multi-select** in TileSet Select mode (Shift-click / rectangle drag) → edit shared properties in the inspector.
2. **Paint properties** mode — pick a property (e.g., a collision polygon) and "paint" it onto tiles.

### Atlas merging

"Open Atlas Merging Tool" (three-dots menu) — combine several atlases into one. Optionally keep originals.

### Techniques applicable to our 2D terrain

- **TileShape = Square** + custom `Texture Region Size` for our tile size — the default for top-down.
- **Terrain Sets (Connect mode)** — automatic edge/corner tile selection for biome transitions (grass↔dirt, dirt↔stone, water↔land). This is the canonical Godot way to do "autotiling".
- **Multiple Physics Layers** — e.g., layer 0 = walkable collider, layer 1 = "blocks projectiles" collider.
- **Custom Data Layers** — attach `walk_speed_multiplier`, `is_water`, `biome_id`, `footstep_sound` to each tile. Read via `TileData.get_custom_data(name)` at runtime.
- **Occlusion Layers** — define occluder polygons per "solid" tile so terrain walls cast shadows from `DirectionalLight2D` (sun) and `PointLight2D` (torches).
- **Y Sort Origin** — for top-down terrain that should sort correctly with characters (e.g., tall grass tufts, cliffs).
- **Scene tiles** — for animated terrain features (waterfall, lava, animated grass sway via `GPUParticles2D`/`AnimationPlayer`).
- **TileMapPattern** — save premade structures (a house, a tree cluster, a rock formation) and stamp them procedurally.

### Code patterns (GDScript)

The page is editor-workflow-focused and does not show much runtime code, but the implied runtime API is:

```gdscript
# Reading per-tile data at runtime
var cell := Vector2i(x, y)
var tile_data: TileData = tile_map_layer.get_cell_tile_data(cell)
if tile_data:
    var speed_mul := tile_data.get_custom_data("walk_speed_multiplier")
    var is_water  := tile_data.get_custom_data("is_water")
    var nav_id    := tile_data.get_navigation_polygon(0)  # navigation layer 0

# Setting a tile procedurally
tile_map_layer.set_cell(cell, source_id, atlas_coords, alternative_tile)

# Erasing
tile_map_layer.set_cell(cell, -1)  # -1 source = empty

# Patterns (TileMapPattern resource)
var pattern: TileMapPattern = tile_set.get_pattern(pattern_id)
tile_map_layer.set_pattern(cell_origin, pattern)
```

(These API names are confirmed by the class-reference links in page #2; the tutorial itself is editor-focused.)

### Links to sub-pages worth reading

- `using_tilemaps.html` (page #4)
- `2d_lights_and_shadows.html` (page #7 — needed for occlusion-layer workflow)
- `../navigation/navigation_using_navigationmeshes.html` (for baking optimised navmesh from a TileMap)
- Class refs: `class_tileset.html`, `class_tilesetatlassource.html`, `class_tiledata.html`, `class_tilemappattern.html`, `class_tilemaplayer.html`

---

## 4. Page #4 — Using TileMaps (covers TileMapLayer, EN stable)

- **URL:** https://docs.godotengine.org/en/stable/tutorials/2d/using_tilemaps.html
- **Title:** `Using TileMaps`
- **HTTP:** 200 OK
- **Note:** The page title still says "TileMaps" but the body exclusively documents the new **`TileMapLayer`** node (the legacy `TileMap` node is deprecated since 4.3 and removed in 4.7-era guidance).

### Key classes/nodes

- `TileMapLayer` — the node.
- `TileSet` — resource (reuse across layers by saving as external `.tres`).
- `TileMapPattern` — saved premade placement, **stored in the TileSet** so reusable across all `TileMapLayer` nodes that share that TileSet.
- `NavigationRegion2D`, `NavigationServer2D` — recommended over built-in TileMap navigation for serious pathfinding.

### `TileMapLayer` properties

| Property group | Property | Purpose |
|----------------|----------|---------|
| General | `Enabled` | Visibility + runtime activity |
| General | `TileSet` | The tileset resource |
| Rendering | `Y Sort Origin` | Per-tile Y-sort offset (top-down games) |
| Rendering | `X Draw Order Reversed` | Reverses X-axis draw order (requires Y-sort on) |
| Rendering | `Rendering Quadrant Size` | Side length of the culling/draw quadrant (group of tiles drawn on one `CanvasItem`). Tune for perf. Ignored when Y-sorting. |
| Physics | `Collision Enabled` | Toggle collision |
| Physics | `Use Kinematic Bodies` | Instantiate collision shapes as kinematic bodies |
| Physics | `Collision Visibility Mode` | Debug visibility |
| Navigation | `Navigation Enabled` | Toggle nav regions |
| Navigation | `Navigation Visible` | Debug visibility |

### Multiple-layer strategy

- Use multiple `TileMapLayer` nodes for foreground/background separation, overlapping tiles, biome layers, decal layers.
- Reorder layers via the Scene dock drag-and-drop, or the layer-switch buttons top-right of the TileMap editor.
- Removing a layer removes all its tiles — be careful.

### Painting tools (TileMap editor toolbar, left→right)

1. **Selection** — click / rectangle; `Shift` = append, `Ctrl` = remove. `Del` erases selection. `Ctrl+C` / `Ctrl+V` copy-paste placed tiles.
2. **Paint** — left-click place; right-click erase. `Shift`-drag = line; `Ctrl+Shift`-drag = rectangle; `Ctrl`-click = picker.
3. **Line** — 1-tile-thick line; right-click = erase line.
4. **Rectangle** — axis-aligned fill; right-click = erase.
5. **Bucket Fill** — flood fill; `Contiguous` checkbox toggles 4-connected vs. global.
6. **Picker** — pick existing tile in 2D editor (`Ctrl`-click is the shortcut from Paint mode).
7. **Eraser** — composable with any other mode (right-click in any mode = erase).

### Randomisation & scattering (HIGH for terrain)

- **Randomisation toggle** — when painting, a random tile from the current multi-selection is placed. Works with Paint / Line / Rectangle / Bucket Fill.
- **Scattering** (`Scattering > 0`) — probability that *no* tile is placed when painting. Use for "occasional, non-repeating detail to large areas (such as adding grass or crumbs on a large top-down TileMap)" — verbatim use case from the docs.

### Patterns (saved premade placements)

- Create: `Select` mode → selection → `Ctrl+C` → click empty space in Patterns tab → `Ctrl+V`.
- Patterns are stored in the `TileSet` resource (not the `TileMapLayer`), so they are reusable across layers and scenes once the TileSet is saved to an external `.tres`.
- Patterns repeat when used with Line / Rectangle / Bucket Fill.

### Terrains tab (autotiling painting)

- Requires at least one terrain set + one terrain in the TileSet (see page #3).
- Three modes:
  - **Connect** — auto-connect to surrounding tiles on the same `TileMapLayer`.
  - **Path** — connect only tiles painted in the same stroke (mouse-down → mouse-up).
  - **Tile-specific overrides** — pick exact tiles to resolve conflicts.

### Navigation — important caveat

> "TileMap built-in navigation has many practical limitations that result in inferior pathfinding performance and pathfollowing quality. After designing the TileMap consider baking it to a more optimised navigation mesh (and disabling the TileMap NavigationLayer) using a `NavigationRegion2D` or the `NavigationServer2D`."

> "2D navigation meshes can not be 'layered' or stacked on top of each other like visuals or physic shapes. Attempting to stack navigation meshes on the same navigation map will result in merge and logical errors that break the pathfinding."

### Missing-tile handling

If a tile referenced by the TileMap is removed from the TileSet, a placeholder is shown in the editor (not at runtime), but the data persists. Re-adding a tile with the same ID restores visuals.

### Techniques applicable to our 2D terrain

- **Multiple TileMapLayer nodes** — e.g., `GroundLayer` (dirt/grass), `DetailLayer` (flowers, rocks via Scattering), `ObstacleLayer` (walls/trees with collision), `DecalLayer` (blood, scorch marks), `LightingOcclusionLayer` (occluder-only tiles). Each is independently toggleable and Y-sortable.
- **Scattering + Randomisation** — procedural-looking grass/cracks/pebbles without writing code. Perfect for top-down organic terrain.
- **Patterns** — premade "stamps" (a tree + surrounding grass + a rock) that procedural generation can place by calling `set_pattern(cell, pattern)`.
- **External `.tres` TileSet** — single source of truth shared by all chunks/layers; survives save/load.
- **`Rendering Quadrant Size`** — tune for performance with large maps (default 16; larger = fewer draw calls but more overdraw).
- **Navigation baking** — design terrain in TileMap, then bake to `NavigationRegion2D` for pathfinding; disable the TileMap's own NavigationLayer.

### Code patterns

The page is editor-focused, but the runtime API surface implied is:

```gdscript
# Procedural placement
@onready var ground: TileMapLayer = $Ground
@onready var detail: TileMapLayer = $Detail

func generate_chunk(origin: Vector2i, size: Vector2i) -> void:
    for y in size.y:
        for x in size.x:
            var cell := origin + Vector2i(x, y)
            var biome := sample_biome(cell)
            ground.set_cell(cell, biome.source_id, biome.atlas_coords)
            # Scattering — random detail on grass
            if biome.name == "grass" and randf() < 0.1:
                var pick := biome.detail_tiles.pick_random()
                detail.set_cell(cell, pick.source_id, pick.atlas_coords)

# Using a saved pattern (TileMapPattern resource)
@export var house_pattern: TileMapPattern
func place_house(at: Vector2i) -> void:
    $Structures.set_pattern(at, house_pattern)

# Reading tile data for gameplay
func tile_walk_speed(cell: Vector2i) -> float:
    var td := ground.get_cell_tile_data(cell)
    return td.get_custom_data("walk_speed_multiplier") if td else 0.0
```

### Links to sub-pages worth reading

- `using_tilesets.html` (page #3)
- `../navigation/navigation_using_navigationmeshes.html` — baking navmesh from a TileMap
- `../physics/using_area_2d.html`, `../physics/using_character_body_2d.html` — interaction with tile colliders

---

## 5. Page #5 — 2D Particle Systems (RU 4.x)

- **URL:** https://docs.godotengine.org/ru/4.x/tutorials/2d/particle_systems_2d.html
- **Title:** `2D Системы частиц`
- **HTTP:** 200 OK

### Key classes/nodes

- `GPUParticles2D` — GPU-accelerated particles (recommended for new content).
- `CPUParticles2D` — CPU fallback for low-end GPUs; has property parity with `GPUParticles2D` except trails.
- `ParticleProcessMaterial` — the `Material` resource that drives `GPUParticles2D` (assigned to `Process Material` slot).
- `CanvasItemMaterial` — used on the particle node itself to enable flipbook animation (`Particle Animation` + `H Frames` / `V Frames`).

### Key particle properties (terrain-relevant subset)

| Property | Effect | Terrain use |
|----------|--------|-------------|
| `Lifetime` | seconds each particle lives | long for fog, short for sparks |
| `One Shot` | emit once then stop | explosions, terrain events |
| `Preprocess` | simulate N seconds before first render | fog/ambience already present on scene load |
| `Speed Scale` | time multiplier | slow mist vs fast embers |
| `Explosiveness` | 0=even spacing, 1=all at once | burst effects |
| `Randomness` | per-param jitter formula: `initial = param + param * randomness` | natural variation |
| `Fixed FPS` | render rate (does NOT slow sim) | stylistic stepping |
| `Fract Delta` | fractional-delta calc, smoother motion | high-randomness systems |
| `Visibility Rect` | culling rect; `Particles > Generate Visibility Rect` auto-sets it | fog fields that cover large terrain areas |
| `Local Coords` | false=world space (particles stay when node moves); true=node space | moving emitters vs static ambience |
| `Draw Order` | `Index` (emission order) or `Lifetime` (remaining life) | visual layering |

### Flipbook textures

- A single texture containing multiple animation frames (spritesheet for particles).
- Set on the `GPUParticles2D` node via `CanvasItemMaterial > Particle Animation` + `H Frames` / `V Frames`.
- If the flipbook has a black background, set the `CanvasItemMaterial`'s `Blend Mode` to `Add` (or preprocess the texture to have transparency).

### Randomisation formula (from docs)

```
initial_value = param_value + param_value * randomness
```

### Techniques applicable to our 2D terrain

- **Ambient fog / mist over terrain** — `GPUParticles2D` with `Local Coords = false`, large `Visibility Rect`, low `Explosiveness`, high `Randomness`, `Preprocess > 0` so the fog is already present when the player enters.
- **Dust / leaves drifting across terrain tiles** — `GPUParticles2D` as a **scene tile** (see page #3) placed inside the TileSet, so each "ambience tile" emits localised particles.
- **Water splashes, lava bubbles, fire embers** on animated terrain tiles — flipbook textures via `CanvasItemMaterial`.
- **Performance note (4.3 caveat):** Godot 4.3 does not support physics interpolation for 2D particles; workaround = set `Node > Physics Interpolation > Mode = Off` on the particle node. (Verify current status for 4.7.1.)

### Code patterns

```gdscript
# Procedurally place an ambient-fog particle field over a terrain region
var fog := GPUParticles2D.new()
fog.amount = 200
fog.lifetime = 8.0
fog.preprocess = 4.0          # already populated on spawn
fog.local_coords = false      # world space
fog.explosiveness = 0.0
fog.randomness = 0.8
fog.visibility_rect = Rect2(region_pos, region_size)
var mat := ParticleProcessMaterial.new()
mat.direction = Vector3(10, 0, 0)   # drifting wind (z ignored for 2D)
mat.gravity = Vector3.ZERO
mat.initial_velocity_min = 5.0
mat.initial_velocity_max = 15.0
mat.scale_min = 0.5
mat.scale_max = 1.5
fog.process_material = mat
fog.texture = fog_sprite
add_child(fog)
```

### Links to sub-pages worth reading

- `particle_process_material_2d.html` — full `ParticleProcessMaterial` reference (the next page to read for serious particle work).
- `../../classes/class_gpuparticles2d.html`
- `../../classes/class_cpuparticles2d.html`
- `../../classes/class_particleprocessmaterial.html`

---

## 6. Page #6 — Custom drawing in 2D (RU 4.x)

- **URL:** https://docs.godotengine.org/ru/4.x/tutorials/2d/custom_drawing_in_2d.html
- **Title:** `Пользовательская отрисовка в 2D`
- **HTTP:** 200 OK
- **Length:** ~30 KB — the longest and most code-dense page indexed.

### Key classes/nodes

- `CanvasItem` — base class for all 2D drawable nodes; provides the draw API.
- `Node2D`, `Control` — concrete subclasses you typically extend.
- `Polygon2D` — a *node* alternative to the `draw_polygon()` function, for persistent deformable polygons.
- `Line2D` — node alternative to `draw_polyline()` (mentioned in passing).
- `Texture2D` — passed to `draw_texture`.
- `RenderingServer` — low-level batch drawing (mentioned; see performance docs).
- `CanvasItemMaterial` — material slot on any `CanvasItem`.

### The custom-draw contract

1. Extend a `CanvasItem`-derived class (`Node2D` or `Control`).
2. Override the virtual method `_draw()`.
3. Inside `_draw()`, call any number of `draw_*()` commands.
4. `_draw()` is called **once**, then the result is cached. To redraw, call `queue_redraw()` (typically from `_process(delta)` for animation).

```gdscript
extends Node2D
func _draw():
    pass # draw_* commands here

func _process(_delta: float):
    queue_redraw()   # re-run _draw() every frame
```

C# equivalent:

```csharp
using Godot;
public partial class MyNode2D : Node2D
{
    public override void _Draw() { /* Draw* calls here */ }
    public override void _Process(double delta) { QueueRedraw(); }
}
```

### Drawing API (full list extracted from the page)

| Method | Signature (GDScript) | Purpose |
|--------|----------------------|---------|
| `draw_texture` | `draw_texture(texture: Texture2D, pos: Vector2, modulate := Color.WHITE)` | Blit a texture |
| `draw_rect` | `draw_rect(rect: Rect2, color, filled := true, width := -1.0, antialiased := false)` | Rectangle outline or fill |
| `draw_line` | `draw_line(from: Vector2, to: Vector2, color, width := 1.0, antialiased := false)` | Single line |
| `draw_multiline` | `draw_multiline(points: PackedVector2Array, color, width := -1.0)` | Many disjoint line segments |
| `draw_multiline_colors` | `draw_multiline_colors(points, colors, width := -1.0)` | Same but per-segment colour |
| `draw_polyline` | `draw_polyline(points: PackedVector2Array, color, width := 1.0, antialiased := false)` | Connected open polyline |
| `draw_polyline_colors` | `draw_polyline_colors(points, colors, width := -1.0)` | Connected polyline, per-vertex colour |
| `draw_polygon` | `draw_polygon(points: PackedVector2Array, colors: PackedColorArray, uvs := PackedVector2Array(), texture := null)` | Filled polygon (can be textured) |
| `draw_colored_polygon` | `draw_colored_polygon(points, color, uvs, texture)` | Filled single-colour polygon |
| `draw_circle` | `draw_circle(pos: Vector2, radius: float, color)` | Filled circle |
| `draw_arc` | `draw_arc(center, radius, start_angle, end_angle, point_count, color, width := -1.0, antialiased := false)` | Arc outline (use many points for smooth circle) |
| `draw_string` | `draw_string(font, pos, text, alignment, width, font_size, modulate)` | Text |
| `draw_set_transform` | `draw_set_transform(offset: Vector2, rotation := 0.0, scale := Vector2.ONE)` | Push a local transform for subsequent draws |
| `draw_set_transform_matrix` | `draw_set_transform_matrix(xform: Transform2D)` | Push a matrix transform |

> The drawing API uses the `CanvasItem`'s local coordinate system (after its own transform). `draw_set_transform` layers an additional transform on top — useful for drawing repeated shapes at offsets without recomputing each point.

### Antialiasing

Most shape functions take an `antialiased: bool = false` parameter. For odd-width lines, offset endpoints by `0.5` to keep them pixel-centred.

### PackedVector2Array helper

The page includes a `float_array_to_Vector2Array()` helper that converts a flat `Array` of `[x, y, x, y, ...]` floats into a `PackedVector2Array` for `draw_polygon` / `draw_polyline`. (Useful when serialising terrain outlines as flat arrays.)

### Editor preview (`@tool`)

```gdscript
@tool
extends Node2D
# _draw() now also runs in the editor 2D view
```

For C#: `[Tool]` attribute. After adding/removing `@tool`, run `Scene > Reload Saved Scene` to refresh.

### Animation pattern

For a rotating custom shape:

```gdscript
extends Node2D
@export var rotation_speed: float = 1.0   # radians/sec

func _ready():
    rotation = 0

func _process(delta: float):
    rotation -= rotation_speed * delta
    queue_redraw()

func _draw():
    draw_polygon(points, colors)
```

### Mouse-interactive example (`draw_line` to mouse cursor)

```gdscript
extends Node2D
@export var point1: Vector2 = Vector2.ZERO
@export var width:  int    = 10
@export var color:  Color  = Color.GREEN
var _point2: Vector2

func _process(_delta):
    var mp := get_viewport().get_mouse_position()
    if mp != _point2:
        _point2 = mp
        queue_redraw()

func _draw():
    draw_line(point1, _point2, color, width)
```

### Arc-between-two-points example (terrain roads/contours)

```gdscript
extends Node2D
@export var point1: Vector2 = Vector2.ZERO
@export_range(1, 1000) var segments: int = 100
@export var width:  int   = 10
@export var color:  Color = Color.GREEN
@export var antialiasing: bool = false
var _point2: Vector2

func _process(_delta):
    _point2 = get_viewport().get_mouse_position()
    queue_redraw()

func _draw():
    var center := (point1 + _point2) * 0.5
    var radius := point1.distance_to(_point2) * 0.5
    draw_arc(center, radius, 0, TAU, segments, color, width, antialiasing)
```

### Techniques applicable to our 2D terrain

- **Procedural polygon terrain** — compute a `PackedVector2Array` of outline points (e.g., from a noise function) and call `draw_polygon(points, colors, uvs, texture)` to fill a textured biome blob. Update via `queue_redraw()` when the camera moves or the noise seed changes.
- **`Polygon2D` node for persistent deformable polygons** — when you need the polygon to live in the scene tree (so it can be selected, modulated, sorted, have a material). Use instead of `draw_polygon` for static or rarely-changed terrain patches.
- **`draw_multiline` / `draw_polyline` for contour lines** — draw elevation contours, grid overlays, debug visualisations of the noise field.
- **`draw_rect` for chunk / cell debug** — visualise chunk boundaries, quadtree subdivisions, or culling rectangles.
- **`draw_set_transform` for tiled rendering** — push a per-cell transform and reuse the same draw calls to render a grid of identical shapes (e.g., debug markers on each terrain cell).
- **`@tool` for editor preview** — let level designers see procedurally-generated terrain overlays directly in the 2D editor without running the game.
- **Avoiding node overhead** — the docs explicitly call out that custom drawing "many simple objects, such as a grid or field for a 2D game" avoids the per-node memory/CPU cost of thousands of `Sprite2D`s. **This is the recommended path for rendering a large procedural field of small decorations** that don't need to be individual nodes.
- **`RenderingServer` (mentioned, not detailed)** — for batched/instanced drawing of thousands of identical sprites (e.g., grass blades), look up `MultiMeshInstance2D` and `RenderingServer.canvas_item_add_multimesh` (cross-ref `../performance/using_multimesh.html` from page #2).

### Code patterns (synthesised for terrain)

```gdscript
@tool
extends Node2D
class_name TerrainOverlay

@export var noise: FastNoiseLite
@export var cell_size: float = 32.0
@export var grid_size: Vector2i = Vector2i(64, 64)
@export var height_color: Gradient

func _draw() -> void:
    for y in grid_size.y:
        for x in grid_size.x:
            var p := Vector2(x, y)
            var h := noise.get_noise_2d(x, y)                  # -1..1
            var col := height_color.sample(remap(h, -1, 1, 0, 1))
            draw_rect(Rect2(p * cell_size, Vector2(cell_size, cell_size)), col)
```

```gdscript
# Organic biome blob from a contour of points
extends Node2D
func _draw() -> void:
    var pts := PackedVector2Array()
    for i in 64:
        var a := TAU * i / 64.0
        var r := 80.0 + 20.0 * sin(a * 3.0)         # wobbly outline
        pts.append(Vector2(cos(a), sin(a)) * r)
    draw_polygon(pts, PackedColorArray([Color(0.4, 0.3, 0.2)]))
```

### Links to sub-pages worth reading

- `../ui/custom_gui_controls.html#drawing` — more on `_draw()` in `Control` context
- `../shaders/introduction_to_shaders.html` — replacing `_draw()` with a shader
- `../shaders/shader_reference/canvas_item_shader.html` — CanvasItem shader reference
- `../performance/using_servers.html` — low-level `RenderingServer` usage
- `../performance/using_multimesh.html` — `MultiMeshInstance2D` pattern for instanced grass/rocks
- `../../classes/class_canvasitem.html` — full `CanvasItem` draw API reference

---

## 7. Page #7 — 2D lights and shadows (RU 4.x)

- **URL:** https://docs.godotengine.org/ru/4.x/tutorials/2d/2d_lights_and_shadows.html
- **Title:** `2D свет и тени`
- **HTTP:** 200 OK

### Key classes/nodes

| Node | Role |
|------|------|
| `CanvasModulate` | Sets the base/ambient colour of the scene. Areas *not* reached by any light show this colour. Required — without it, lights just add brightness to an already-lit scene. |
| `PointLight2D` | Omnidirectional / positional light (torches, fire, projectiles). Texture defines its shape & size. |
| `DirectionalLight2D` | Parallel rays — sun / moon. |
| `LightOccluder2D` | Defines a polygon that casts shadows. Can be a standalone node **or** embedded per-tile in a `TileMapLayer` (via the TileSet's Occlusion Layers, see page #3). |
| `Sprite2D` | Lit receivers; also used as the light *texture* on `PointLight2D`. |
| `TileMapLayer` | Lit receiver; can also embed occluders per tile. |
| `CanvasTexture` | `Resource` that wraps a diffuse texture + normal map + specular map (so any 2D node can receive normal-mapped lighting). |
| `CanvasItemMaterial` | Per-node material; set `Blend Mode = Add` for cheap additive "lights". |

### `PointLight2D` properties

| Property | Effect |
|----------|--------|
| `Texture` | Light shape/size (alpha used in `Mix` mode; ignored for `Add`/`Subtract`). |
| `Offset` | Texture offset; does **not** move shadows. |
| `Texture Scale` | Light spread multiplier. Larger = slower (more pixels affected). |
| `Height` | Virtual height above normal-mapped surfaces. Increase for visible normal-map response. |
| `Blend Mode` | `Add` (default), `Subtract` (negative light), `Mix` (lerp with light texture). |

**Procedural light texture (no image asset needed):** assign a `GradientTexture2D` to `Texture`, set `Fill > Fill Mode = Radial`, gradient opaque-white → transparent-white, move start to centre. This gives a smooth radial falloff light.

### `DirectionalLight2D` properties

| Property | Effect |
|----------|--------|
| `Height` | 0 = parallel to surfaces, 1 = perpendicular. Only affects normal-map response; does NOT affect shadow length. |
| `Max Distance` | Pixel radius from camera centre beyond which shadows are culled (perf). Camera2D zoom is NOT considered. |

> **Limitation:** Directional shadows always appear infinitely long. For finite-length directional shadows, disable built-in shadows and use a custom shader that reads the SDF generated from `LightOccluder2D` nodes.

### Common `Light2D` properties (base class)

- `Enabled` — toggle visibility (does NOT hide children, unlike `visible = false`).
- `Editor Only` — visible in editor, auto-disabled at runtime.
- `Color` — light colour.
- `Energy` — intensity multiplier.
- `Blend Mode` — `Add` / `Subtract` / `Mix`.
- `Range > Z Min` / `Z Max` — only affects CanvasItems within this Z-index range.
- `Range > Layer Min` / `Layer Max` — only affects canvas layers in this range.
- `Range > Item Cull Mask` — bitwise mask vs. each CanvasItem's `Light Mask` property; lets you exclude specific receivers.

### `LightOccluder2D` properties

- `OccluderPolygon2D` resource — the actual polygon (must be set for any shadow).
- `SDF Collision` — if on, the occluder contributes to the real-time **Signed Distance Field** that custom shaders can read. Default ON (no perf cost if no shader uses it).
- `Occluder Light Mask` — bitwise mask paired with each light's `Shadow > Item Cull Mask`; controls which lights this occluder casts shadows for.

### Two ways to create occluders

1. **Auto-generate from a `Sprite2D`** — select the sprite → 2D editor menu → `Sprite2D > Create LightOccluder2D Sibling`. Tweak `Grow (pixels)` / `Shrink (pixels)` → `Update Preview` → `OK`.
2. **Draw manually** — add a `LightOccluder2D` node → click `+` in the 2D editor → confirm polygon creation → click to add points, right-click to delete, click-and-drag an existing segment to insert a point.

### Shadow properties (on `PointLight2D` / `DirectionalLight2D`)

| Property | Effect |
|----------|--------|
| `Shadow > Enabled` | Toggle shadows. |
| `Shadow > Color` | Shadow tint (default black). Alpha controls tint strength. |
| `Shadow > Filter` | `None` (fastest, blocky — pixel-art), `PCF5` (soft), `PCF13` (softest, expensive — use sparingly). |
| `Shadow > Filter Smooth` | Softness when `Filter` is PCF5/PCF13. Too high → banding artifacts (esp. PCF5). |
| `Shadow > Item Cull Mask` | Bitwise mask vs. each occluder's `Occluder Light Mask`. |

### Normal & specular maps (depth on flat terrain)

- Assign a `CanvasTexture` resource to the texture slot of any 2D node (e.g., `Sprite2D.texture`).
- `CanvasTexture` properties:
  - `Diffuse > Texture` — base colour.
  - `Normal Map > Texture` — per-pixel surface normal (generated from a height map; tool: **Laigter**, free OSS).
  - `Specular > Texture` — per-pixel specular intensity (greyscale, optionally tinted).
  - `Specular > Color` — specular colour multiplier.
  - `Specular > Shininess` — low = broad/diffuse highlight, high = tight/wet look.
  - `Texture > Filter` — override texture filtering.
  - `Texture > Repeat` — override texture repeat (enable for tiled background sprites).
- After enabling normal maps, lighting gets dimmer; raise the light's `Height` and slightly raise `Energy`.

### Pixel-art lighting shader (verbatim from docs)

For pixel-art terrain where you want lighting/shadows snapped to a pixel grid (Nearest texture filtering alone does NOT do this):

```glsl
shader_type canvas_item;
uniform float pixel_size = 4.0;
void fragment() {
    // Snap lighting and shadows to pixel grid.
    LIGHT_VERTEX.xy = floor(LIGHT_VERTEX.xy / pixel_size) * pixel_size;
    SHADOW_VERTEX   = floor(SHADOW_VERTEX   / pixel_size) * pixel_size;
    // Normal rendering.
    COLOR = texture(TEXTURE, UV);
}
```

This works by transforming the light/shadow vertex into grid space via `floor()`, then back to screen space — forcing the engine to sample lighting from discrete grid positions.

### Additive `Sprite2D` as a cheaper alternative

When 2D lighting is too expensive (many dynamic sources):

- Create a `Sprite2D`, assign a glow texture.
- In `CanvasItem > Material`, create a `New CanvasItemMaterial`, set `Blend Mode = Add`.
- Works with `AnimatedSprite2D` (or `Sprite2D` + `AnimationPlayer`) for animated "lights".

Trade-offs vs. real 2D lights:
- Cannot light fully dark areas correctly.
- Cannot cast shadows.
- Ignores normal/specular maps.

### Techniques applicable to our 2D terrain

- **`CanvasModulate` ambient colour** — set the base darkness (e.g., a deep blue `Color(0.15, 0.18, 0.25)` for night). All terrain not directly lit shows this colour. **One `CanvasModulate` per scene.**
- **`DirectionalLight2D` as global sun** — single instance, angle = time-of-day. Terrain occluders (from the TileSet Occlusion Layer, see page #3) cast shadows across the map.
- **`PointLight2D` for local lights** — torches, campfires, glowing crystals. Use `GradientTexture2D` (Radial fill) for procedural falloff.
- **TileSet Occlusion Layers (cross-ref page #3)** — define occluder polygons per "solid" tile (walls, cliffs, tree trunks). The TileMap then automatically generates `LightOccluder2D`s for every placed solid tile — **zero per-tile code**.
- **Standalone `LightOccluder2D` for procedural shapes** — for non-tile terrain (e.g., a `Polygon2D`-rendered blob), add a sibling `LightOccluder2D` with an `OccluderPolygon2D` matching the blob's outline.
- **`CanvasTexture` normal maps on terrain sprites** — generate a normal map from the terrain heightmap (Laigter or in-engine) to give flat terrain sprites a 3D bumpiness under the sun.
- **`Item Cull Mask` / `Light Mask` selective lighting** — e.g., a torch light that illuminates terrain (mask bit 0) but not the UI layer (mask bit 1). Or a "magic vision" light that reveals only hidden-terrain tiles.
- **`SDF Collision` + custom shader** — for finite-length directional shadows (sun shadows that don't stretch infinitely). Enable `SDF Collision` on occluders, write a canvas_item shader that reads the SDF.
- **Pixel-art lighting shader** — if our terrain is pixel-art, apply the `LIGHT_VERTEX`/`SHADOW_VERTEX` floor-snapping shader to keep lighting blocky and consistent with the art style.
- **Performance fallback** — replace cheap dynamic "lights" (muzzle flashes, glow particles) with additive-blend `Sprite2D`s; reserve real `PointLight2D`s for cast-shadow lights.
- **`Sprite2D` Region + `Texture > Repeat = Enabled`** — for tiled background textures (e.g., a repeating grass texture behind the tilemap).

### Code patterns

```gdscript
# Day/night cycle: rotate the sun + modulate ambient colour
@onready var sun:    DirectionalLight2D = $Sun
@onready var ambient: CanvasModulate     = $Ambient

func _process(delta: float) -> void:
    time_of_day = fmod(time_of_day + delta * 0.01, TAU)   # 0..TAU
    sun.rotation = time_of_day
    # Brighter at noon (time_of_day ≈ PI/2), dark at midnight (≈ 3*PI/2)
    var d := sin(time_of_day)                              # -1..1
    ambient.color = Color(0.05, 0.07, 0.12).lerp(Color(0.6, 0.55, 0.45), clampf(d, 0, 1))
    sun.energy = clampf(d, 0.0, 1.0) * 1.5
    sun.enabled = d > 0.0
```

```gdscript
# Procedural PointLight2D with no texture asset
var torch := PointLight2D.new()
torch.blend_mode = Light2D.BLEND_MODE_ADD
torch.color = Color(1.0, 0.75, 0.4)
torch.energy = 1.2
torch.shadow_enabled = true
torch.shadow_filter = Light2D.SHADOW_FILTER_PCF5

var grad := GradientTexture2D.new()
grad.fill = GradientTexture2D.FILL_RADIAL
grad.fill_from = Vector2(0.5, 0.5)
grad.fill_to = Vector2(0.5, 0.0)
var g := Gradient.new()
g.set_color(0, Color(1,1,1,1))
g.add_point(1.0, Color(1,1,1,0))
grad.gradient = g
torch.texture = grad
torch.texture_scale = 2.0
torch.height = 1.0   # raise above normal-mapped terrain
add_child(torch)
```

### Links to sub-pages worth reading

- `../shaders/introduction_to_shaders.html` — light-processor shaders
- `../shaders/shader_reference/canvas_item_shader.html#light-built-ins` — `LIGHT_VERTEX`, `SHADOW_VERTEX`, `LIGHT` built-ins
- `../../classes/class_pointlight2d.html`
- `../../classes/class_directionallight2d.html`
- `../../classes/class_lightoccluder2d.html`
- `../../classes/class_occluderpolygon2d.html`
- `../../classes/class_canvastexture.html`
- `../../classes/class_canvasmodulate.html`

---

## 8. Cross-Cutting Synthesis — Techniques for OUR 2D Top-Down Terrain

The table below maps terrain-generation concerns to the specific Godot APIs/techniques discovered across all 7 pages.

| Concern | Recommended Godot technique | Source page | Key API / node |
|---------|------------------------------|-------------|----------------|
| Grid-based tile terrain | `TileMapLayer` + `TileSet` (atlas source) | #3, #4 | `TileMapLayer.set_cell()`, `TileSetAtlasSource` |
| Auto-connecting biome edges | **Terrains** system (Connect mode) | #3, #4 | TileSet Terrain Sets + Peering Bits |
| Procedural decoration scatter | Painting Randomisation + Scattering in editor; runtime `set_cell` with `randf()` | #4 | `TileMapLayer` painting tools, `Randomness` |
| Premade structure stamps | `TileMapPattern` saved in TileSet | #3, #4 | `TileMapLayer.set_pattern(cell, pattern)` |
| Per-tile gameplay metadata | TileSet **Custom Data Layers** | #3 | `TileData.get_custom_data(name)` |
| Per-tile collision | TileSet **Physics Layers** | #3, #4 | `TileMapLayer.collision_enabled`, `Use Kinematic Bodies` |
| Per-tile shadow casting | TileSet **Occlusion Layers** | #3, #7 | `LightOccluder2D` (auto-per-tile) |
| Pathfinding on terrain | Bake `NavigationRegion2D` from the TileMap (do NOT rely on built-in TileMap nav) | #4 | `NavigationRegion2D`, `NavigationServer2D` |
| Layered terrain (ground/detail/walls/decals) | Multiple `TileMapLayer` nodes | #4 | Reorder in Scene dock |
| Top-down depth sorting | `CanvasItem > Y Sort Enabled` + `TileMapLayer.y_sort_origin` + per-tile `Y Sort Origin` | #3, #4 | `Y Sort Origin` |
| Organic (non-tile) terrain shapes | `Polygon2D` node OR custom `_draw()` with `draw_polygon()` | #6 | `draw_polygon`, `Polygon2D` |
| Procedural heightmap visualisation | Custom `_draw()` iterating cells, `draw_rect()` per cell | #6 | `_draw()`, `queue_redraw()`, `draw_rect()` |
| Contour / outline rendering | `draw_polyline()`, `draw_multiline()` | #6 | `draw_polyline(points, color, width, antialiased)` |
| Thousands of instanced decorations (grass) | `MultiMeshInstance2D` (cross-ref perf docs) | #2, #6 | `MultiMeshInstance2D`, `RenderingServer.canvas_item_add_multimesh` |
| Editor preview of procedural gen | `@tool` annotation + `_draw()` | #6 | `@tool` |
| Global sun lighting | `DirectionalLight2D` (single instance) | #7 | `DirectionalLight2D`, `Height`, `Max Distance` |
| Local torches / fires | `PointLight2D` + `GradientTexture2D` (Radial) | #7 | `PointLight2D`, `GradientTexture2D` |
| Ambient darkness | `CanvasModulate` (one per scene) | #7 | `CanvasModulate.color` |
| Terrain depth/bumpiness | `CanvasTexture` normal+specular maps on terrain sprites | #7 | `CanvasTexture`, `Normal Map > Texture` |
| Pixel-art lighting | Custom canvas_item shader snapping `LIGHT_VERTEX`/`SHADOW_VERTEX` | #7 | `shader_type canvas_item;`, `floor()` |
| Finite directional shadows | `LightOccluder2D.SDF Collision = true` + custom SDF-reading shader | #7 | `SDF Collision`, `OccluderPolygon2D` |
| Ambient fog / mist | `GPUParticles2D` (Local Coords=false, large Visibility Rect, Preprocess>0) | #5 | `GPUParticles2D`, `ParticleProcessMaterial` |
| Animated terrain tiles (waterfall, lava) | Scene tiles containing `GPUParticles2D` or `AnimationPlayer` | #3, #5 | `TileSetScenesCollectionSource` |
| Selective lighting (light affects only some layers) | `Light2D.range_item_cull_mask` + `CanvasItem.light_mask` | #7 | `Range > Item Cull Mask`, `Light Mask` |
| Cheap glow effects (perf fallback) | `Sprite2D` + `CanvasItemMaterial(Blend Mode=Add)` | #7 | `CanvasItemMaterial`, `BLEND_MODE_ADD` |
| Tiled background texture | `Sprite2D.region_enabled = true` + `Texture > Repeat = Enabled` | #7 | `Sprite2D`, `CanvasTexture.texture_repeat` |

### Top-priority APIs for our terrain system

These are the names we should be coding against:

**Nodes**
- `TileMapLayer` (NOT the deprecated `TileMap`)
- `TileSet`, `TileSetAtlasSource`, `TileSetScenesCollectionSource`
- `TileMapPattern`, `TileData`
- `Polygon2D`, `Line2D`
- `CanvasModulate`, `PointLight2D`, `DirectionalLight2D`, `LightOccluder2D`, `OccluderPolygon2D`
- `GPUParticles2D`, `CPUParticles2D`, `ParticleProcessMaterial`
- `NavigationRegion2D`
- `MultiMeshInstance2D` (for instanced grass — cross-ref perf docs)
- `Sprite2D`, `CanvasItemMaterial`, `CanvasTexture`
- `Camera2D`

**Resources**
- `TileSet` (external `.tres`)
- `TileMapPattern`
- `GradientTexture2D` (Radial fill for procedural lights)
- `CanvasTexture` (diffuse + normal + specular)
- `CanvasItemMaterial` (blend modes)
- `FastNoiseLite` (procedural noise — standard Godot noise class, not in these pages but implied for terrain generation)
- `OccluderPolygon2D`

**CanvasItem drawing API (`_draw()` virtual method)**
- `draw_texture(texture, pos, modulate)`
- `draw_rect(rect, color, filled, width, antialiased)`
- `draw_line(from, to, color, width, antialiased)`
- `draw_multiline(points, color, width)` / `draw_multiline_colors(points, colors, width)`
- `draw_polyline(points, color, width, antialiased)` / `draw_polyline_colors`
- `draw_polygon(points, colors, uvs, texture)` / `draw_colored_polygon`
- `draw_circle(pos, radius, color)`
- `draw_arc(center, radius, start_angle, end_angle, point_count, color, width, antialiased)`
- `draw_string(font, pos, text, ...)`
- `draw_set_transform(offset, rotation, scale)` / `draw_set_transform_matrix(xform)`
- `queue_redraw()` — trigger next `_draw()` call

**TileMapLayer runtime API (implied by class refs)**
- `set_cell(coords, source_id, atlas_coords, alternative_tile)`
- `get_cell_tile_data(coords) -> TileData`
- `set_pattern(origin, pattern)`
- `get_pattern(...)` / `TileSet.get_pattern(id)`
- `clear()`

**TileData runtime API**
- `get_custom_data(name)` / `set_custom_data(name, value)`
- `get_navigation_polygon(layer_id)`
- `get_collision_polygon_count()` / `get_collision_polygon_shape(layer, index)`
- `get_occluder_polygon(layer_id)` (returns `OccluderPolygon2D`)
- `get_terrain_set()` / `get_terrain()` / `get_terrain_peering_bits()`

**Light2D / shadow constants**
- `Light2D.BLEND_MODE_ADD` / `BLEND_MODE_SUBTRACT` / `BLEND_MODE_MIX`
- `Light2D.SHADOW_FILTER_NONE` / `SHADOW_FILTER_PCF5` / `SHADOW_FILTER_PCF13`

---

## 9. Open Questions / Next Reading

These are pages discovered during indexing that were NOT fetched but look directly relevant for terrain work. Recommended next fetch batch:

1. `tutorials/2d/2d_parallax.html` — background terrain depth (parallax layers).
2. `tutorials/2d/2d_meshes.html` — `Polygon2D` mesh deformation (organic terrain).
3. `tutorials/2d/2d_antialiasing.html` — AA options for line/polygon terrain outlines.
4. `tutorials/2d/particle_process_material_2d.html` — full particle material reference (fog/wind tuning).
5. `tutorials/navigation/navigation_using_navigationmeshes.html` — baking navmesh from a TileMap (the page #4 warning makes this mandatory).
6. `tutorials/performance/using_multimesh.html` — `MultiMeshInstance2D` for thousands of grass/rock instances.
7. `tutorials/shaders/shader_reference/canvas_item_shader.html` — full CanvasItem shader reference (`LIGHT_VERTEX`, `SHADOW_VERTEX`, `LIGHT` built-ins for custom terrain lighting).
8. `tutorials/shaders/your_first_shader/your_first_2d_shader.html` — beginner CanvasItem shader intro.
9. `engine_details/architecture/2d_coordinate_systems.html` — 2D coordinate system internals.
10. `classes/class_tilemaplayer.html` — full runtime API reference (the authoritative source for the `set_cell` / `get_cell_tile_data` / `set_pattern` signatures used above).

---

## 10. Fetch Provenance

All 7 pages were fetched via:

```
z-ai function -n page_reader -a '{"url": "..."}' -o <file>.json
```

Raw JSON responses and extracted plain-text versions are stored in `/tmp/godot_fetch/` (session-temporary):
- `p1_main.json` / `.txt`
- `p2_2d_index.json` / `.txt`
- `p3_tilesets.json` / `.txt`
- `p4_tilemap.json` / `.txt` (corrected URL)
- `p5_particles.json` / `.txt` (corrected URL)
- `p6_custom_draw.json` / `.txt`
- `p7_lights.json` / `.txt`

Extraction was performed with Python + BeautifulSoup (`html.parser`), stripping `script`/`style`/`nav`/`header`/`footer`/`svg` and collapsing blank lines. All pages returned HTTP 200 with non-empty content after URL correction.
