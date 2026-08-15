# 2.5D Demo — Visual Techniques Reference (IDX-3)

> **Source:** `godotengine/godot-demo-projects` → `mono/2.5d/` (also locally mirrored at
> `Ai-game4/2.5d-project-with-c#-demo/`).
> **Purpose:** Study how the official Godot 2.5D demo achieves **"наглядность"** (visual clarity
> and depth perception) without using a true 3D camera, and extract techniques applicable to our
> pure-2D top-down game (no Z coordinate yet).
> **Status:** READ-ONLY research. No code changes.

---

## 1. Demo Overview

The 2.5D demo mixes 2D and 3D nodes:

- **3D nodes** (`CharacterBody3D`, `StaticBody3D`, `BoxShape3D`) are used **only for math**
  (collision, position, movement).
- **2D nodes** (`Sprite2D`, `Camera2D`, `Node2D`) are used for **all rendering**.
- The camera is `Camera2D`, never `Camera3D`.

The "2.5D" trick is a custom **`Node25D`** (`Node2D` subclass) whose first child is a `Node3D`.
Each frame it converts the 3D child's position to a 2D screen position via a **`Basis25D`**
projection matrix and writes it into `GlobalPosition`.

Six view modes are supported (top-down, front-side, 45°, isometric, oblique-Y, oblique-Z),
each with its own `Basis25D` and its own pre-rendered sprite variant.

Renderer: `gl_compatibility`. Texture filter: `nearest` (pixel-perfect).

---

## 2. File Map (what was fetched)

| File | Purpose |
|---|---|
| `README.md` | High-level overview of approach. |
| `project.godot` | Renderer, input map, physics tick (120), clear color. |
| `addons/node25d-cs/Node25D.cs` | Base node: 3D position → 2D `GlobalPosition`. Holds `Transform25D`. Implements `IComparable<Node25D>` for Y-sort. |
| `addons/node25d-cs/Basis25D.cs` | 3-axis Vector2 matrix; 6 built-in view modes (`TopDown`, `FrontSide`, `FortyFive`, `Isometric`, `ObliqueY`, `ObliqueZ`). |
| `addons/node25d-cs/Transform25D.cs` | `FlatPosition = X·basis.x + Y·basis.y + Z·basis.z`. |
| `addons/node25d-cs/YSort25D.cs` | Plain `Node` (not `Node2D`); sorts `Node25D` siblings and assigns z-index with **gap of 2** to leave room for shadows. |
| `addons/node25d-cs/ShadowMath25D.cs` | `CharacterBody3D` that copies the target's 3D position and `MoveAndCollide(Vector3.Down * shadowLength)` to find the floor; if no collision, hides the shadow. |
| `assets/demo_scene.tscn` | Scene tree with Player25D + Shadow25D + ~24 Platforms + YSort25D. |
| `assets/player/player_25d.tscn` | Player node structure (Node2D + CharacterBody3D + Sprite2D + Camera2D). |
| `assets/shadow/shadow_25d.tscn` | Shadow node structure (Node2D + CharacterBody3D + flat BoxShape3D + Sprite2D). |
| `assets/platform/platform.tscn` | Platform node structure (Node2D + StaticBody3D + Sprite2D). |
| `assets/player/PlayerSprite.cs` | Animation + per-view-mode sprite-basis transforms (squash for 45°, skew for oblique). |
| `assets/shadow/shadow_sprite.gd` | Swaps shadow texture per view mode. |
| `assets/platform/platform_sprite.gd` | Swaps platform texture per view mode. |

---

## 3. Core Visual Techniques

### 3.1 Node structure pattern (the "two-body" pattern)

Every visible 2.5D object is a **`Node2D`** containing:

1. A **math-only 3D body** as the **first child** (CharacterBody3D for actors, StaticBody3D for
   scenery) — carries the real collision shape and physics position.
2. One or more **2D Sprite children** for rendering.

```
Player25D (Node2D, script=Node25D.cs, z_index=-3952)
├── PlayerMath25D (CharacterBody3D)         # first child = math body
│   └── CollisionShape3D (BoxShape3D 1×2×1) # real collider
├── PlayerSprite (Sprite2D, scale=(1, 0.75))# visual
│   └── PlayerCamera (Camera2D)             # camera follows sprite
Shadow25D (Node2D, z_index=-3958)           # SIBLING, not child
├── ShadowMath25D (CharacterBody3D)
│   └── CollisionShape3D (BoxShape3D 1×0.002×1)  # flat
└── ShadowSprite (Sprite2D, scale=(0.5, 0.5))
```

### 3.2 Position projection (Basis25D)

`FlatPosition = spatialPosition.X * basis.x + spatialPosition.Y * basis.y + spatialPosition.Z * basis.z`

Each `basis.*` is a `Vector2` — the on-screen 2D direction you move when the 3D position changes
by 1 unit on that axis. The 6 presets:

| Mode | basis.x | basis.y | basis.z | Effect |
|---|---|---|---|---|
| TopDown | (1, 0) | (0, 0) | (0, 1) | Y (height) invisible; pure top-down. |
| FrontSide | (1, 0) | (0, -1) | (0, 0) | Z invisible; side-scroller. |
| **FortyFive** | (1, 0) | (0, -0.707) | (0, 0.707) | Y goes up-screen, Z goes down-screen — default. |
| Isometric | (0.866, 0.5) | (0, -1) | (-0.866, 0.5) | True 30° iso. |
| ObliqueY | (1, 0) | (-0.707, -0.707) | (0, 1) | Y skews X. |
| ObliqueZ | (1, 0) | (0, -1) | (-0.707, 0.707) | Z skews X. |

Scale factor `SCALE = 32` pixels per 3D unit.

### 3.3 Y-sorting (depth illusion) — **YSort25D**

`YSort25D` is a **plain `Node`** (the comment in source explicitly notes "NOT Node2D, Node25D, or
Node2D") placed as a sibling alongside the Node25D objects.

Each `_Process`:

```csharp
public void Sort() {
    var children = GetParent().GetChildren();
    List<Node25D> node25dChildren = /* filter Node25D only */;
    node25dChildren.Sort();   // uses Node25D.CompareTo
    int zIndex = -4000;
    for (int i = 0; i < node25dChildren.Count; i++) {
        node25dChildren[i].ZIndex = zIndex;
        zIndex += 2;   // ← KEY: gap of 2 leaves room for shadow at z-1
    }
}
```

`Node25D.CompareTo`:

```csharp
real_t thisIndex  = spatialPosition.Y + 0.001f * (spatialPosition.X + spatialPosition.Z);
real_t otherIndex = other.spatialPosition.Y + 0.001f * (other.spatialPosition.X + other.spatialPosition.Z);
```

- **Primary key: Y** (3D height). Lower Y → farther back → smaller z-index → drawn first.
- **Tie-breaker: X + Z** (with tiny 0.001 weight) — prevents two coplanar objects from
  flickering (z-fighting).

Hard cap: 4000 sorted objects (because z-index ranges `-4000 … +4000` in Godot's
`z_index` range, and we step by 2).

### 3.4 Shadow rendering — **ShadowMath25D**

The shadow is **NOT** a child of the player. It is a **sibling** placed immediately before the
player in the scene tree. Its z-index sits **1 below** the player (gap of 2 → shadow uses
`-1` slot between adjacent objects).

Each frame:

```csharp
Position = targetMath.Position;                       // copy target's 3D pos
var k = MoveAndCollide(Vector3.Down * shadowLength);  // raycast downward
if (k == null) shadowRoot.Visible = false;            // no floor → hide
else { shadowRoot.Visible = true; GlobalTransform = Transform; }  // snap to floor
```

- Collision shape is **flat** (`BoxShape3D 1 × 0.002 × 1`) — a virtual pancake.
- Shadow sprite is a **separate pre-rendered PNG** per view mode
  (`forty_five.png`, `isometric.png`, `top_down.png`, `front_side.png`, `oblique_y.png`,
  `oblique_z.png`) — already shaped/colored to look correct from that camera angle.
- The shadow node's own `Node25D` script then projects the floor point back to 2D using the
  same `Basis25D`, so the shadow sprite stays glued to the floor on screen.

**Why sibling, not child?** If the shadow were a child of the player, the player's
z-index would force the shadow to render on top of the player. By being a sibling, the
shadow can have its own (lower) z-index assigned by `YSort25D`.

### 3.5 Sprite squash / skew per view mode (PlayerSprite.SetViewMode)

The sprite's own `Transform2D` is modified per view mode to make a single texture look correct
from multiple angles:

| Mode | t.X | t.Y | Visual effect |
|---|---|---|---|
| 45° | (1, 0) | (0, 0.75) | Vertical squash — fake "looking down at 45°". |
| Isometric | (1, 0) | (0, 1) | No squash. |
| Top-down | (1, 0) | (0, 0.5) | Heavy vertical squash. |
| Front-side | (1, 0) | (0, 1) | No squash. |
| Oblique Y | (0.75, 0.75) | (0.75, 0.75) | Diagonal skew. |
| Oblique Z | (1, 0.25) | (0, 1) | Slight horizontal skew. |

The PlayerSprite is also set to `scale = Vector2(1, 0.75)` in the scene file — a baked-in
baseline squash.

### 3.6 Project-level visual clarity ("наглядность") settings

From `project.godot`:

| Setting | Value | Why |
|---|---|---|
| `display/window/size/viewport_width` | 1600 | Large play field. |
| `display/window/size/viewport_height` | 900 | 16:9. |
| `display/window/stretch/mode` | `canvas_items` | UI scales with viewport. |
| `display/window/stretch/aspect` | `expand` | Fill screen, no letterbox. |
| `physics/common/physics_ticks_per_second` | 120 | Smoother movement, less jitter on shadow. |
| `rendering/textures/canvas_textures/default_texture_filter` | `0` (Nearest) | Crisp pixel art. |
| `rendering/renderer/rendering_method` | `gl_compatibility` | Max compatibility. |
| `environment/defaults/default_clear_color` | `Color(0.0836, 0.20636, 0.22, 1)` | **Dark teal** — sprites pop. |

### 3.7 Animation frame layout (PlayerSprite)

- `stand.png`: `Hframes = 1` — single frame.
- `run.png`: `Hframes = 6`, frame picked as `_direction * 6 + (int)_progress` where progress
  advances at `FRAMERATE = 15` fps, modulo 6.
- `jump.png`: `Hframes = 2`, frame picked as `_direction * 2 + (jumping ? 1 : 0)`.
- `_direction` is an int 0..4 representing 8 directions (N, NE, E, SE, S, SW, W, NW), with
  `FlipH = true` to mirror 4 of them — saves 4 texture rows.

### 3.8 Per-view-mode texture swap (platform/shadow sprites)

Both `shadow_sprite.gd` and `platform_sprite.gd` preload **six** PNG variants and switch on
input action. This means the shadow and platform *art itself* is pre-distorted to match the
view angle, so the engine doesn't have to do real-time skewing.

---

## 4. How "наглядность" (visual clarity) is achieved without full 3D

1. **Dark, low-saturation background** makes every sprite read instantly.
2. **Drop shadows** under every dynamic object ground the sprite in space — the eye instantly
   sees "this thing is floating" vs "this thing is on the floor".
3. **Y-sort with z-index gap** guarantees correct overlap; the 0.001 tie-breaker eliminates
   z-fighting flicker on coplanar objects.
4. **Sprite vertical squash** (`scale.y = 0.75`) suggests a tilted camera even when the camera
   is orthographic 2D.
5. **Per-view-mode pre-rendered textures** — artist bakes the perspective into the PNG so the
   runtime is cheap and the result is pixel-crisp.
6. **High physics tick (120 Hz)** keeps shadows locked to the floor during fast motion — no
   visible lag between sprite and shadow.
7. **Nearest-neighbor texture filter** keeps edges sharp at any zoom.
8. **Sibling-shadow pattern with z-index gap** ensures shadow is always *just below* its owner
   regardless of how many other objects crowd the same Y row.

---

## 5. What we can apply to our 2D top-down game (no Z coordinate)

We don't have `Node25D`'s 3D math. We **do** have Y (screen) and a hypothetical "height above
floor" value we can carry per entity. The following techniques transfer directly:

### 5.1 YSort with z-index gap of 2 (HIGH PRIORITY)

Replace Godot's built-in `YSort` with a small custom node that:

1. Collects all `Sprite2D` / `Node2D` children of the world root.
2. Sorts by `global_position.y` (with a tiny tie-breaker on `x` to avoid flicker).
3. Assigns `z_index = base + i * 2`.

This **guarantees a free slot** at `z_index - 1` for each entity's shadow, no matter how
densely entities are packed.

> Godot's built-in `YSort` (enabled via `Node2D.y_sort_enabled = true` on the parent) does
> NOT leave a gap — it just reorders children. That's fine for sprites-only, but breaks for
> sibling-shadow patterns. Use the custom sort if you want per-entity shadows.

### 5.2 Sibling-based shadow sprite (HIGH PRIORITY)

For each entity that needs a shadow, structure as:

```
WorldRoot
├── EntityShadow (Sprite2D, z_index = entity_z - 1, texture = shadow.png)
└── Entity (Node2D + Sprite2D, z_index = entity_z)
```

- The shadow's `global_position` follows the entity's `global_position` (set in `_process`).
- If the entity has a "height" value (jump, fly, hover), **offset shadow.y by 0** (it stays on
  the floor) and offset **entity sprite.y by -height** (sprite rises). This is the 2D analog
  of `MoveAndCollide(Vector3.Down)`.
- Optionally scale the shadow by `1.0 - height * k` so it shrinks as the entity rises — sells
  the height illusion.

### 5.3 Pre-rendered shadow texture

Use a single soft-edged ellipse PNG (e.g. 64×32, alpha gradient). Color it black with ~40%
alpha. Reuse for every entity. If you support multiple "view angles" later, add one PNG per
angle (as the demo does).

### 5.4 Sprite vertical squash for fake perspective

For characters in a top-down game, apply `scale = Vector2(1, 0.75)` (or 0.5 for stricter
top-down) to suggest the camera is tilted ~30–45° down. This is purely cosmetic but
dramatically improves "наглядность".

### 5.5 Dark, low-saturation clear color

In `project.godot`:

```
[rendering]
environment/defaults/default_clear_color=Color(0.08, 0.20, 0.22, 1)
```

Dark teal/navy works well. Test against your tile palette — the floor should be at least
30% darker than the lightest sprite.

### 5.6 Nearest-neighbor texture filter

```
[rendering]
textures/canvas_textures/default_texture_filter=0
```

Crisp pixel art. Pair with `window/stretch/mode = "canvas_items"` and `aspect = "expand"`.

### 5.7 High physics tick

```
[physics]
common/physics_ticks_per_second=120
```

Smooths out shadow-following during fast movement. Worth the CPU cost for visual clarity.

### 5.8 Animation frame indexing (direction × animation)

Mirror the demo's pattern: store `_direction` (0..N-1) and `_progress` (float frame counter).
Compute `frame = _direction * frames_per_anim + (int)_progress`. Use `FlipH` to halve the
texture rows for side-facing directions.

### 5.9 Tie-breaker to kill z-fighting

When two entities share the same Y, the 0.001 weight on X in the demo's `CompareTo` is the
difference between stable rendering and 60 Hz flicker. Apply the same trick:

```csharp
float key = position.Y + 0.001f * position.X;
```

### 5.10 Flat collision shape for shadow raycast (if we add jump)

If we ever add jump/fly mechanics, the demo's flat `BoxShape3D (1 × 0.002 × 1)` is the
template: a pancake collider used only to find the floor via `MoveAndCollide(down)`. In pure
2D this becomes a `RayCast2D` pointing down from the entity, querying the tilemap collision
layer.

---

## 6. What we should NOT copy

- **Node25D / Basis25D / Transform25D** — these only make sense if you have a real 3D position
  to project. We don't. Skip.
- **Per-view-mode texture swap** — we have only one view (top-down). One texture each.
- **`[Tool]` attribute on scripts** — needed for the demo's editor viewport plugin; we don't
  ship a custom editor.

---

## 7. Concrete recommendations for Ai-game4

| Priority | Technique | Effort | File to add |
|---|---|---|---|
| P0 | Dark clear color + nearest filter + 120 Hz physics | 5 min | `project.godot` |
| P0 | Custom `YSort2D` node with z-index gap of 2 | 1 h | `scripts/world/y_sort_2d.gd` |
| P0 | Sibling-shadow sprite per dynamic entity | 2 h | `scenes/entities/entity_shadow.tscn` |
| P1 | Sprite vertical squash `scale=(1, 0.75)` on character sprites | 5 min per sprite | entity scenes |
| P1 | Pre-rendered soft-ellipse shadow PNG | 30 min art | `assets/effects/shadow.png` |
| P2 | Raycast2D downward for jump-hover shadow scaling | 3 h | `scripts/entity/shadow_follower.gd` |
| P2 | Tie-breaker (0.001 × X) in Y-sort | 5 min | inside `y_sort_2d.gd` |
| P3 | Direction-based animation frame indexing | 1 h per entity | `scripts/entity/anim_controller.gd` |

---

## 8. Source citations

All findings are from the official `godotengine/godot-demo-projects` repository, path
`mono/2.5d/`, branch `master`. Key files:

- `addons/node25d-cs/Node25D.cs` (lines 1–135)
- `addons/node25d-cs/Basis25D.cs` (lines 1–200)
- `addons/node25d-cs/Transform25D.cs` (lines 1–110)
- `addons/node25d-cs/YSort25D.cs` (lines 1–55)
- `addons/node25d-cs/ShadowMath25D.cs` (lines 1–45)
- `assets/demo_scene.tscn` (Player25D z=-3952, Shadow25D z=-3958)
- `assets/player/player_25d.tscn` (PlayerSprite scale=(1, 0.75), vframes=5)
- `assets/shadow/shadow_25d.tscn` (BoxShape3D 1×0.002×1)
- `assets/player/PlayerSprite.cs` (SetViewMode, frame indexing, direction×progress)
- `project.godot` (clear color, filter, physics tick, renderer)

Local mirror at `Ai-game4/2.5d-project-with-c#-demo/` for offline re-reading.
