# Godot Procedural Geometry & Noise Generation Index

> **Task ID:** IDX-2 | **Agent:** index-procedural-docs
> **Engine target:** Godot 4.7.1 (stable) | **Project context:** 2D top-down game
> **Source pages:** official Godot docs (procedural_geometry/index, arraymesh, surfacetool,
> immediatemesh), FastNoiseLite & Noise class references, plus community tutorials
> (ziva.sh, abitawake.com, gameidea.org).
>
> **NOTE for 2D project:** The 3D procedural geometry toolchain (ArrayMesh / SurfaceTool /
> ImmediateMesh / MeshDataTool) is largely irrelevant for a 2D top-down tile game. The
> transferable parts are: (1) the FastNoiseLite noise API, (2) threshold/biome mapping
> patterns, and (3) cellular automata cave generation. Those are the focus below.

---

## 1. Procedural Geometry Tools — Overview (3D, for context)

Source: <https://docs.godotengine.org/en/stable/tutorials/3d/procedural_geometry/index.html>

> "All the procedural geometry generation methods described here run on the CPU. Godot
> doesn't support generating geometry on the GPU yet."

Four primary tools, ranked by typical use case:

| Tool            | Best for                                | Notes                                                                                |
| --------------- | --------------------------------------- | ------------------------------------------------------------------------------------ |
| `ArrayMesh`     | Static meshes, slightly faster          | Lower-level API; build via `add_surface_from_arrays()`                               |
| `SurfaceTool`   | Static meshes, easier API               | Has helpers `generate_normals()`, `index()`; OpenGL 1.x immediate-mode style        |
| `ImmediateMesh` | Per-frame dynamic geometry / debug vis  | Rebuilt every change; slightly faster than recreating ArrayMesh every frame          |
| `MeshDataTool`  | When you need edge/face array access    | Slowest, but only way to do mesh-topology algorithms                                 |

**Verdict for 2D top-down game:** Skip all four. For 2D terrain we use `TileMapLayer` +
`FastNoiseLite` + custom cellular-automata grid code. ArrayMesh/SurfaceTool are only
relevant if we render 2D terrain as a custom polygon mesh (rare; usually TileMap is better).

### Mesh structure recap (informational)
- A `Mesh` is composed of one or more **surfaces**.
- Each surface is an array of length `Mesh.ARRAY_MAX` containing sub-arrays of per-vertex
  data (`ARRAY_VERTEX`, `ARRAY_NORMAL`, `ARRAY_TEX_UV`, `ARRAY_INDEX`, etc.).
- Indexed vs non-indexed: indexed arrays are faster but force shared vertex data.
- `add_surface_from_arrays(primitive_type, arrays)` builds a surface on an ArrayMesh.

### ArrayMesh key API (3D only, included for completeness)
```gdscript
extends MeshInstance3D

func _ready():
    var surface_array = []
    surface_array.resize(Mesh.ARRAY_MAX)

    var verts   = PackedVector3Array()
    var uvs     = PackedVector2Array()
    var normals = PackedVector3Array()
    var indices = PackedInt32Array()

    # ... populate verts/uvs/normals/indices with geometry ...

    surface_array[Mesh.ARRAY_VERTEX] = verts
    surface_array[Mesh.ARRAY_TEX_UV] = uvs
    surface_array[Mesh.ARRAY_NORMAL] = normals
    surface_array[Mesh.ARRAY_INDEX] = indices

    mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, surface_array)
```

### SurfaceTool key API (3D only, included for completeness)
```gdscript
var st = SurfaceTool.new()
st.begin(Mesh.PRIMITIVE_TRIANGLES)

# Set attributes BEFORE add_vertex — they attach to the next vertex.
st.set_normal(Vector3(0, 0, 1))
st.set_uv(Vector2(0, 0))
st.add_vertex(Vector3(-1, -1, 0))

st.set_normal(Vector3(0, 0, 1))
st.set_uv(Vector2(0, 1))
st.add_vertex(Vector3(-1, 1, 0))

st.set_normal(Vector3(0, 0, 1))
st.set_uv(Vector2(1, 1))
st.add_vertex(Vector3(1, 1, 0))

# Helpers (optional):
# st.index()
# st.generate_normals()

var mesh = st.commit()      # or: st.commit(existing_arraymesh) to append
```

---

## 2. FastNoiseLite — Core API for 2D Generation

Source: <https://docs.godotengine.org/en/stable/classes/class_fastnoiselite.html>
Parent class Noise: <https://docs.godotengine.org/en/stable/classes/class_noise.html>

### Class summary
> "This class generates noise using the FastNoiseLite library, which is a collection of
> several noise algorithms including Cellular, Perlin, Value, and more. Most generated
> noise values are in the range of [-1, 1], but not always. Some of the cellular noise
> algorithms return results above 1."

FastNoiseLite extends `Noise` and inherits the sampling methods.

### Inherited sampling methods (from `Noise`)
| Method | Signature | Returns |
| ------ | --------- | ------- |
| `get_noise_1d`  | `(x: float) const`                                | `float` in roughly `[-1, 1]` |
| `get_noise_2d`  | `(x: float, y: float) const`                      | `float` in roughly `[-1, 1]` |
| `get_noise_2dv` | `(v: Vector2) const`                              | `float` in roughly `[-1, 1]` |
| `get_noise_3d`  | `(x: float, y: float, z: float) const`            | `float` in roughly `[-1, 1]` |
| `get_noise_3dv` | `(v: Vector3) const`                              | `float` in roughly `[-1, 1]` |
| `get_image`     | `(w, h, invert=false, in_3d_space=false, normalize=true) const` | `Image` of 2D noise |
| `get_seamless_image` | `(w, h, invert=false, in_3d_space=false, skirt=0.1, normalize=true) const` | tiling `Image` |

> **`get_noise_2d` vs `get_image` caveat:** Community reports (Reddit, Godot forum) note
> that `get_image` defaults `normalize=true` and rescales values to `[0, 1]`, while raw
> `get_noise_2d` returns `[-1, 1]`. If you mix the two, normalize manually. For per-tile
> terrain, **use `get_noise_2d` per-cell** — `get_image` is for texture-based approaches.

### FastNoiseLite Properties (key for 2D terrain)

| Property                          | Type        | Default     | Purpose                                                        |
| --------------------------------- | ----------- | ----------- | -------------------------------------------------------------- |
| `seed`                            | `int`       | `0`         | Reproducibility — same seed = same map                         |
| `noise_type`                      | `NoiseType` | `1` (SimplexSmooth) | Underlying algorithm                                    |
| `frequency`                       | `float`     | `0.01`      | **Lower = bigger features.** Most important knob.              |
| `offset`                          | `Vector3`   | `(0,0,0)`   | Sample offset (use x,y for 2D shifts)                          |
| `fractal_type`                    | `FractalType` | `1` (FBM) | How octaves combine                                            |
| `fractal_octaves`                 | `int`       | `5`         | Detail levels; ~4 typical for terrain                          |
| `fractal_lacunarity`              | `float`     | `2.0`       | Frequency multiplier per octave                                |
| `fractal_gain`                    | `float`     | `0.5`       | Amplitude multiplier per octave                                |
| `fractal_weighted_strength`       | `float`     | `0.0`       | Bias toward stronger octaves (0..1)                            |
| `fractal_ping_pong_strength`      | `float`     | `2.0`       | For `FRACTAL_PING_PONG`                                        |
| `cellular_distance_function`      | enum        | `0` (Euclidean) | Cell metric                                                |
| `cellular_return_type`            | enum        | `1` (Distance) | What cellular noise returns                                 |
| `cellular_jitter`                 | `float`     | `1.0`       | Cell point displacement (0 = even grid)                        |
| `domain_warp_enabled`             | `bool`      | `false`     | Warp noise space for organic distortion                        |
| `domain_warp_type`                | enum        | `0` (Simplex) | Warp algorithm                                                |
| `domain_warp_amplitude`           | `float`     | `30.0`      | Max warp distance                                              |
| `domain_warp_frequency`           | `float`     | `0.05`      | Warp frequency                                                 |
| `domain_warp_fractal_type`        | enum        | `1` (Progressive) | Warp fractal                                            |
| `domain_warp_fractal_octaves`     | `int`       | `5`         | Warp fractal octaves                                           |
| `domain_warp_fractal_lacunarity`  | `float`     | `6.0`       | Warp lacunarity                                                |
| `domain_warp_fractal_gain`        | `float`     | `0.5`       | Warp gain                                                       |

### NoiseType enum
| Constant              | Value | Notes |
| --------------------- | ----- | ----- |
| `TYPE_VALUE`          | `5`   | Lattice of random values, interpolated. Fastest, lowest quality. |
| `TYPE_VALUE_CUBIC`    | `4`   | Slower value variant with more peak/valley variance. **Use this over `TYPE_VALUE` for heightmaps** to avoid artifacts. |
| `TYPE_PERLIN`         | `3`   | Lattice of random gradients; dot products interpolated. Classic. |
| `TYPE_CELLULAR`       | `2`   | Worley/Voronoi regions — same value per cell. Use for tile biome distribution. |
| `TYPE_SIMPLEX`        | `0`   | Gradients on a simplex lattice; no directional artifacts. Internally OpenSimplex2. |
| `TYPE_SIMPLEX_SMOOTH` | `1`   | (Default.) Higher quality Simplex, slower. Internally OpenSimplex2S. |

### FractalType enum
| Constant               | Value | Notes |
| ---------------------- | ----- | ----- |
| `FRACTAL_NONE`         | `0`   | No fractal layering. |
| `FRACTAL_FBM`          | `1`   | (Default.) Fractional Brownian Motion — standard for natural terrain. |
| `FRACTAL_RIDGED`       | `2`   | Produces ridge lines (mountain crests). |
| `FRACTAL_PING_PONG`    | `3`   | Modulates between ridges; varied terrain. |

### CellularDistanceFunction enum
| Constant                    | Value | Notes |
| --------------------------- | ----- | ----- |
| `DISTANCE_EUCLIDEAN`        | `0`   | Default |
| `DISTANCE_EUCLIDEAN_SQUARED`| `1`   | No sqrt |
| `DISTANCE_MANHATTAN`        | `2`   | Taxicab metric |
| `DISTANCE_HYBRID`           | `3`   | Euclidean+Manhattan blend → curved cell boundaries |

### CellularReturnType enum
| Constant                  | Value | Notes |
| ------------------------- | ----- | ----- |
| `RETURN_CELL_VALUE`       | `0`   | Same value for every point in a cell (Voronoi regions) |
| `RETURN_DISTANCE`         | `1`   | (Default.) Distance to nearest point |
| `RETURN_DISTANCE2`        | `2`   | Distance to 2nd-nearest point |
| `RETURN_DISTANCE2_ADD`    | `3`   | d1 + d2 |
| `RETURN_DISTANCE2_SUB`    | `4`   | d2 - d1 |
| `RETURN_DISTANCE2_MUL`    | `5`   | d1 * d2 |
| `RETURN_DISTANCE2_DIV`    | `6`   | d2 / d1 |

### DomainWarpType / DomainWarpFractalType enums
- `DOMAIN_WARP_SIMPLEX` (`0`), `DOMAIN_WARP_SIMPLEX_REDUCED` (`1`), `DOMAIN_WARP_BASIC_GRID` (`2`)
- `DOMAIN_WARP_FRACTAL_NONE` (`0`), `DOMAIN_WARP_FRACTAL_PROGRESSIVE` (`1`, default — "liquified"), `DOMAIN_WARP_FRACTAL_INDEPENDENT` (`2` — more chaotic)

---

## 3. 2D Heightmap Pattern with FastNoiseLite

Minimal 5-line heightmap (ziva.sh pattern, confirmed working in Godot 4.x):

```gdscript
var noise := FastNoiseLite.new()
noise.seed = randi()
noise.frequency = 0.02            # smaller = bigger features
noise.fractal_octaves = 4         # more octaves = more detail

func height_at(x: int, y: int) -> float:
    return noise.get_noise_2d(x, y)   # returns -1.0..1.0
```

### Tuning guide
- **`frequency`** — most impactful. `0.02` with 4 octaves ≈ natural-looking terrain.
  Larger maps need lower frequency (e.g. `0.005`).
- **`fractal_octaves`** — 3–5 typical. Each octave roughly doubles CPU cost.
- **`fractal_lacunarity`** (default `2.0`) and **`fractal_gain`** (default `0.5`) —
  rarely need tuning; defaults give natural-looking self-similar detail.
- **`fractal_weighted_strength`** (`0`..`1`) — biases toward higher-frequency octaves;
  useful for sharper, more rugged terrain.
- **`fractal_type`**:
  - `FRACTAL_FBM` — default, good for general terrain
  - `FRACTAL_RIDGED` — mountain ranges, ridges
  - `FRACTAL_PING_PONG` — varied, less predictable

### Map noise → tile type (threshold mapping)
```gdscript
enum Tile { DEEP_WATER, SHALLOW_WATER, SAND, GRASS, FOREST, MOUNTAIN, SNOW }

func tile_for_height(h: float) -> int:
    # h is in [-1, 1]; remap to [0, 1]
    var t := (h + 1.0) * 0.5
    if t < 0.20: return Tile.DEEP_WATER
    if t < 0.35: return Tile.SHALLOW_WATER
    if t < 0.42: return Tile.SAND
    if t < 0.65: return Tile.GRASS
    if t < 0.80: return Tile.FOREST
    if t < 0.92: return Tile.MOUNTAIN
    return Tile.SNOW
```

### Loop-driven TileMapLayer fill
```gdscript
@tool
extends Node2D

@export var map_w := 128
@export var map_h := 128
@export var noise: FastNoiseLite

@onready var layer: TileMapLayer = $TileMapLayer

func generate() -> void:
    layer.clear()
    for y in range(map_h):
        for x in range(map_w):
            var h := noise.get_noise_2d(x, y)
            var tile_id := tile_for_height(h)
            layer.set_cell(Vector2i(x, y), 0, Vector2i(tile_id, 0))
```

> **Performance note (ziva.sh):** "Anything beyond 100x100 tiles locks your game for
> 200ms+. Use a worker thread or split across frames with `await`." For 128x128+ maps,
> generate inside a `Thread` or chunk it across `_process` calls.

---

## 4. Threshold-Based Biome Mapping (Whittaker Diagram)

Source: ziva.sh "Procedural Generation in Godot 4: 5 Patterns from Real Games"

> "For biome distribution, layer multiple noises with different seeds. Temperature noise +
> moisture noise gives you a 2D grid you can map to biomes (desert when high temperature,
> low moisture; forest when moderate temperature, high moisture; tundra when low
> temperature, anywhere). This is the classic Whittaker biome diagram, and it works just
> as well in 2026 as it did in Minecraft a decade ago."

### Two-noise biome sampler
```gdscript
var elev_noise  := FastNoiseLite.new()
var temp_noise  := FastNoiseLite.new()
var moist_noise := FastNoiseLite.new()

func _init() -> void:
    elev_noise.seed  = randi(); elev_noise.frequency  = 0.01
    temp_noise.seed  = randi(); temp_noise.frequency  = 0.005  # large-scale climate
    moist_noise.seed = randi(); moist_noise.frequency = 0.008

enum Biome { OCEAN, BEACH, DESERT, SAVANNA, GRASSLAND, FOREST, RAINFOREST, TUNDRA, TAIGA, SNOW, MOUNTAIN }

func biome_at(x: int, y: int) -> int:
    var e := (elev_noise.get_noise_2d(x, y)  + 1.0) * 0.5   # 0..1
    var t := (temp_noise.get_noise_2d(x, y)  + 1.0) * 0.5   # 0..1
    var m := (moist_noise.get_noise_2d(x, y) + 1.0) * 0.5   # 0..1

    # Elevation gates first (water/mountain override biome)
    if e < 0.30: return Biome.OCEAN
    if e < 0.35: return Biome.BEACH
    if e > 0.85: return Biome.MOUNTAIN

    # Whittaker grid: temperature (rows) × moisture (cols)
    if t < 0.25:
        return Biome.SNOW if m < 0.5 else Biome.TUNDRA
    if t < 0.50:
        return Biome.TAIGA  if m > 0.50 else Biome.TUNDRA
    if t < 0.75:
        if m < 0.20: return Biome.GRASSLAND
        if m < 0.50: return Biome.SAVANNA
        if m < 0.80: return Biome.FOREST
        return Biome.RAINFOREST
    # Hot zone
    if m < 0.25: return Biome.DESERT
    if m < 0.50: return Biome.SAVANNA
    return Biome.RAINFOREST
```

### Pattern notes
- Use **different seeds** per noise — correlated seeds produce correlated maps.
- Use **different frequencies** so each noise has a different characteristic scale
  (e.g. temperature = broad climate bands, moisture = medium variation).
- Apply a **fall-off mask** (e.g. radial gradient or extra noise) near map edges to
  create island/continent shapes.
- **Domain warping** (`domain_warp_enabled = true`) on the elevation noise gives
  twisting, organic coastlines instead of blob shapes.

---

## 5. Cellular Automata — Cave Generation (2D)

Sources:
- ziva.sh "Procedural Generation in Godot 4: 5 Patterns"
- abitawake.com "Procedural Generation with Godot: Creating Caves with Cellular Automata"
- gameidea.org "Procedural Cave generation in Godot (2D)"

### Classic 4-5 step rule
> "Start with a random grid (50% wall, 50% floor), then apply the rule: 'a cell becomes
> a wall if it has 4+ wall neighbors, otherwise it becomes floor.' Run it 4-5 iterations."

```gdscript
# 0 = floor, 1 = wall
func step(grid: Array, w: int, h: int) -> Array:
    var new_grid := grid.duplicate(true)
    for y in range(1, h - 1):
        for x in range(1, w - 1):
            var wall_neighbors := 0
            for dy in [-1, 0, 1]:
                for dx in [-1, 0, 1]:
                    if dx == 0 and dy == 0:
                        continue
                    if grid[y + dy][x + dx] == 1:
                        wall_neighbors += 1
            new_grid[y][x] = 1 if wall_neighbors >= 4 else 0
    return new_grid

func generate_caves(w: int, h: int, fill: float = 0.5, iterations: int = 5) -> Array:
    var grid := []
    for y in range(h):
        var row := []
        for x in range(w):
            # Walls on borders; random fill inside
            if x == 0 or y == 0 or x == w - 1 or y == h - 1:
                row.append(1)
            else:
                row.append(1 if randf() < fill else 0)
        grid.append(row)
    for _i in range(iterations):
        grid = step(grid, w, h)
    return grid
```

### Tuning
- `fill` — 0.45–0.55 typical. Higher fill = more wall.
- `iterations` — 4–5 gives organic look; >7 tends to fully fill or empty regions.
- Wall threshold `>= 4` (out of 8 neighbors) is the classic "4-5 rule" — `>= 5` opens up
  larger caves, `>= 3` closes them.

### Critical post-step: Flood-fill for connected components
> "Cellular automata can produce disconnected regions. After generation, run a flood-fill
> to find the largest connected component, then either fill in or carve corridors to reach
> the others." — ziva.sh

```gdscript
# Returns: list of caves, each cave = Array of Vector2i
func find_caves(grid: Array, w: int, h: int, min_size: int = 80) -> Array:
    var visited := {}
    var caves := []
    for y in range(h):
        for x in range(w):
            var key := Vector2i(x, y)
            if grid[y][x] == 0 and not visited.has(key):
                var cave := _flood_fill(grid, w, h, x, y, visited)
                if cave.size() >= min_size:
                    caves.append(cave)
    return caves

func _flood_fill(grid: Array, w: int, h: int, sx: int, sy: int, visited: Dictionary) -> Array:
    var cave := []
    var stack := [Vector2i(sx, sy)]
    while stack.size() > 0:
        var cell: Vector2i = stack.pop_back()
        if visited.has(cell) or grid[cell.y][cell.x] == 1:
            continue
        visited[cell] = true
        cave.append(cell)
        for d in [Vector2i(0,-1), Vector2i(0,1), Vector2i(-1,0), Vector2i(1,0)]:
            var n := cell + d
            if n.x >= 0 and n.x < w and n.y >= 0 and n.y < h:
                if not visited.has(n) and grid[n.y][n.x] == 0:
                    stack.append(n)
    return cave
```

### Connect caves with drunken walk (abitawake pattern)
After flood-filling, pick a random tile in each cave and random-walk to the next cave's
nearest tile, carving floor as you go:

```gdscript
func connect_caves(caves: Array) -> void:
    var prev: Vector2i = Vector2i.ZERO
    for cave in caves:
        if prev != Vector2i.ZERO:
            var start: Vector2i = cave[randi() % cave.size()]
            _drunken_walk(prev, start)
        prev = cave[randi() % cave.size()]

func _drunken_walk(from: Vector2i, to: Vector2i, max_steps: int = 500) -> void:
    var p := from
    var steps := 0
    while steps < max_steps and p != to:
        steps += 1
        # Carve floor
        set_tile(p, Tile.GROUND)
        # Bias direction toward target
        var weights := {"n": 1.0, "s": 1.0, "e": 1.0, "w": 1.0}
        if p.x < to.x: weights["e"] += 1.0
        elif p.x > to.x: weights["w"] += 1.0
        if p.y < to.y: weights["s"] += 1.0
        elif p.y > to.y: weights["n"] += 1.0
        # Normalize and pick
        var total := weights.values().reduce(func(a, b): return a + b, 0.0)
        var r := randf() * total
        if r < weights["n"]:        p.y -= 1
        elif r < weights["n"] + weights["s"]: p.y += 1
        elif r < weights["n"] + weights["s"] + weights["e"]: p.x += 1
        else:                       p.x -= 1
```

### Full pipeline (abitawake-derived)
1. `clear()` — empty the TileMapLayer
2. `fill_walls()` — fill entire map with wall tiles
3. `random_floor()` — randomly carve floor (45–50% chance)
4. `dig_caves()` — run N cellular automata iterations
5. `find_caves()` — flood-fill connected floor regions
6. `connect_caves()` — drunken-walk tunnels between largest regions
7. (optional) prune small caves — anything below `min_cave_size` reverted to wall

---

## 6. Other Procedural Patterns (Briefly, for Reference)

From ziva.sh's "5 patterns that ship":

| Pattern            | Best for                              | Aesthetic              |
| ------------------ | ------------------------------------- | ---------------------- |
| FastNoiseLite      | Terrain heightmaps, biome distribution | Continuous, natural  |
| BSP (Binary Space Partition) | Grid-aligned dungeon rooms   | Rectangular, predictable |
| Cellular Automata  | Organic cave systems, lava/water flow | Hand-drawn, irregular  |
| Wave Function Collapse | Tile maps with strict adjacency rules | Tightly fitted, puzzle-like |
| Drunkard's Walk    | Random organic paths, erosion         | Twisting, unplanned    |

> "You almost never use all five in one game. You pick the one that matches your problem."

### BSP sketch (for dungeons)
```
1. Start with a single rectangle the size of your map.
2. Split it horizontally or vertically into two rectangles.
3. Recursively split each child until rectangles are small enough.
4. Place a room inside each leaf rectangle.
5. Connect adjacent rooms with corridors.
```

### Wave Function Collapse sketch
- Each cell starts as superposition of all possible tiles.
- Pick the cell with fewest options, collapse one randomly.
- Propagate constraints: neighbors lose options that violate adjacency rules.
- Repeat until done or no valid options remain (needs backtracking).
- Reference impl: Maxim Gumin's original. Godot 4 port: AlexeyBond's repo.

---

## 7. Adaptation Summary for Our 2D Top-Down Game

| Need                           | Use                                          | Notes                                                              |
| ------------------------------ | -------------------------------------------- | ------------------------------------------------------------------ |
| Overworld terrain tiles        | `FastNoiseLite.get_noise_2d` + thresholds    | Use `TYPE_SIMPLEX_SMOOTH` + `FRACTAL_FBM`, 4 octaves, freq 0.01–0.02 |
| Biomes                         | Two-noise Whittaker (temp + moisture)        | Different seeds + different frequencies                            |
| Coastlines                     | `domain_warp_enabled = true` on elevation    | Prevents blob-shaped continents                                    |
| Caves / dungeon interiors      | Cellular automata + flood-fill + drunken walk | 5 iterations, threshold ≥ 4, fill 0.5                             |
| Tile rendering                 | `TileMapLayer.set_cell(Vector2i, src_id, atlas)` | Godot 4.3+ uses `TileMapLayer` (deprecated `TileMap`)            |
| Performance (>100×100)         | `Thread` or chunked `await` per frame        | ziva.sh: 100×100 ≈ 200ms lock on main thread                       |
| Reproducibility                | Store `seed` in save file                    | Same seed → same map (verify `offset` is also saved if used)       |

### Minimal recommended FastNoiseLite config for 2D top-down terrain
```gdscript
var noise := FastNoiseLite.new()
noise.seed = world_seed
noise.noise_type = FastNoiseLite.TYPE_SIMPLEX_SMOOTH
noise.frequency = 0.012
noise.fractal_type = FastNoiseLite.FRACTAL_FBM
noise.fractal_octaves = 4
noise.fractal_lacunarity = 2.0
noise.fractal_gain = 0.5
# Optional for organic coastlines:
noise.domain_warp_enabled = true
noise.domain_warp_amplitude = 25.0
noise.domain_warp_frequency = 0.05
```

---

## 8. Source URLs (verified fetchable via `curl`)

The `z-ai page_reader` CLI times out on `docs.godotengine.org` (large Sphinx pages with
deep nav trees). All content below was fetched via `curl` + HTML strip as a fallback.
The `web_search` CLI works fine for these URLs.

### Official Godot docs (stable / 4.7)
- <https://docs.godotengine.org/en/stable/tutorials/3d/procedural_geometry/index.html>
- <https://docs.godotengine.org/en/stable/tutorials/3d/procedural_geometry/arraymesh.html>
- <https://docs.godotengine.org/en/stable/tutorials/3d/procedural_geometry/surfacetool.html>
- <https://docs.godotengine.org/en/stable/tutorials/3d/procedural_geometry/immediatemesh.html>
- <https://docs.godotengine.org/en/stable/classes/class_fastnoiselite.html>
- <https://docs.godotengine.org/en/stable/classes/class_noise.html>
- <https://docs.godotengine.org/en/stable/classes/class_arraymesh.html>
- <https://docs.godotengine.org/en/stable/classes/class_surfacetool.html>
- <https://docs.godotengine.org/en/stable/classes/class_immediatemesh.html>

> **Note:** `using_arraymesh.html` URL in the task spec returns HTTP 404 — the ArrayMesh
> tutorial lives at `arraymesh.html` (the page title is "Using the ArrayMesh").

### Community tutorials (search-confirmed, content fetched)
- <https://ziva.sh/blogs/godot-procedural-generation> — 5 patterns that ship in real Godot games
- <https://abitawake.com/news/articles/procedural-generation-with-godot-creating-caves-with-cellular-automata> — Full CA cave tutorial with flood-fill + drunken walk
- <https://gameidea.org/2025/01/20/procedural-cave-generation-in-godot-2d/> — 2D cave gen with guaranteed path
- <https://www.redblobgames.com/maps/terrain-from-noise> — Classic noise → biome theory (Whittaker)
- <https://medium.com/@cece9200/godot-4-c-generating-terrain-with-simplex-noise-a3150a6e393f> — Simplex terrain in C#
- <https://thegodotbarn.com/contributions/snippet/84/blocky-3d-heightmap> — FastNoiseLite heightmap snippet

---

## 9. Key APIs Cheat Sheet

```gdscript
# --- FastNoiseLite construction ---
var n := FastNoiseLite.new()
n.seed              = randi()
n.noise_type        = FastNoiseLite.TYPE_SIMPLEX_SMOOTH  # default
n.frequency         = 0.01                                # default; smaller = bigger
n.fractal_type      = FastNoiseLite.FRACTAL_FBM           # default
n.fractal_octaves   = 5                                   # default
n.fractal_lacunarity = 2.0
n.fractal_gain      = 0.5
n.offset            = Vector3(0, 0, 0)                    # use .x, .y for 2D shift

# --- Sampling (inherited from Noise) ---
var h1: float = n.get_noise_2d(x, y)         # ~[-1, 1]
var h2: float = n.get_noise_2dv(Vector2(x, y))
var img: Image = n.get_image(128, 128)       # normalized to [0, 1] by default

# --- Cellular-specific (for Voronoi-region biome tiles) ---
n.noise_type = FastNoiseLite.TYPE_CELLULAR
n.cellular_distance_function = FastNoiseLite.DISTANCE_EUCLIDEAN
n.cellular_return_type       = FastNoiseLite.RETURN_CELL_VALUE
n.cellular_jitter            = 1.0   # 0 = regular grid

# --- Domain warp (organic coastlines) ---
n.domain_warp_enabled      = true
n.domain_warp_type         = FastNoiseLite.DOMAIN_WARP_SIMPLEX
n.domain_warp_amplitude    = 25.0
n.domain_warp_frequency    = 0.05
n.domain_warp_fractal_type = FastNoiseLite.DOMAIN_WARP_FRACTAL_PROGRESSIVE

# --- TileMapLayer (Godot 4.3+, replaces deprecated TileMap) ---
var layer: TileMapLayer = $TileMapLayer
layer.set_cell(Vector2i(x, y), source_id, atlas_coords)
layer.clear()
```

### ArrayMesh (3D only — for reference, not used in 2D tile game)
```gdscript
var arr_mesh := ArrayMesh.new()
var arrays := []
arrays.resize(Mesh.ARRAY_MAX)
arrays[Mesh.ARRAY_VERTEX] = PackedVector3Array(...)   # index 0
arrays[Mesh.ARRAY_NORMAL] = PackedVector3Array(...)   # index 1
arrays[Mesh.ARRAY_TEX_UV] = PackedVector2Array(...)   # index 4
arrays[Mesh.ARRAY_INDEX]  = PackedInt32Array(...)     # index 12
arr_mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
```

### SurfaceTool (3D only — for reference)
```gdscript
var st := SurfaceTool.new()
st.begin(Mesh.PRIMITIVE_TRIANGLES)
st.set_normal(Vector3(0, 0, 1))
st.set_uv(Vector2(0, 0))
st.add_vertex(Vector3(-1, -1, 0))
# ... more vertices ...
st.generate_normals()   # optional helper
st.index()              # optional helper
var mesh: ArrayMesh = st.commit()   # or st.commit(existing_arraymesh)
```
