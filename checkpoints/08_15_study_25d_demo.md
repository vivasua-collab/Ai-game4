---
Task ID: IDX-3
Agent: study-25d-demo
Task: Study 2.5D demo visual techniques for our 2D game

Work Log:
- Fetched README.md, project.godot from godotengine/godot-demo-projects/mono/2.5d
- Listed assets/ and addons/node25d-cs/ via GitHub API (token auth)
- Fetched source: Node25D.cs, Basis25D.cs, Transform25D.cs, YSort25D.cs, ShadowMath25D.cs
- Fetched scenes: demo_scene.tscn, player_25d.tscn, shadow_25d.tscn, platform.tscn
- Fetched scripts: PlayerSprite.cs, shadow_sprite.gd, platform_sprite.gd
- Cross-verified with local mirror at Ai-game4/2.5d-project-with-c#-demo/

Techniques found:
- Node25D = Node2D with Node3D first child; 3D position projected to 2D via Basis25D matrix
- 6 view modes (TopDown, FrontSide, FortyFive, Isometric, ObliqueY, ObliqueZ) — each is just
  a different 3-column Vector2 basis; SCALE = 32 px/unit
- YSort25D is a plain Node (not Node2D) sibling; sorts Node25D children by
  Y + 0.001*(X+Z) and assigns z_index = -4000 + i*2 (gap of 2 leaves room for shadows)
- Shadow = separate Node25D SIBLING (not child) placed before the player in tree;
  ShadowMath25D (CharacterBody3D) copies target 3D pos then MoveAndCollide(Vector3.Down*1000)
  to find floor; if no hit, hides shadow; flat BoxShape3D (1x0.002x1) collider
- Per-view-mode pre-rendered PNG textures (forty_five.png, isometric.png, etc.) for platform
  and shadow — artist bakes perspective into art
- PlayerSprite SetViewMode adjusts sprite Transform2D.X/Y per view (squash for top-down,
  skew for oblique); baseline scale=(1, 0.75) for fake tilt
- Animation: frame = _direction * frames_per_anim + (int)_progress; FlipH halves texture rows
- project.godot: dark clear color (teal 0.08/0.20/0.22), nearest texture filter,
  gl_compatibility renderer, 120 Hz physics tick, 1600x900 viewport, canvas_items stretch

Stage Summary:
- Index file created: /home/z/my-project/Ai-game4/docs/docs_v2/10_godot_reference/demo_25d_techniques.md
- Applicable techniques (no Z coordinate needed):
  1. Custom YSort2D node with z_index gap of 2 (sort by Y + 0.001*X tiebreaker)
  2. Sibling-based shadow sprite per entity (z_index = entity_z - 1)
  3. Pre-rendered soft-ellipse shadow PNG (one shared texture)
  4. Sprite vertical squash scale=(1, 0.75) for fake camera tilt
  5. Dark clear color + nearest texture filter + 120 Hz physics
  6. Direction-based animation frame indexing with FlipH mirroring
  7. RayCast2D downward for jump-hover shadow scaling (if jump added)
  8. 0.001*X tiebreaker to kill z-fighting on coplanar entities
- Shadow approach: 2D analog of demo's MoveAndCollide(down) — keep shadow.y on floor,
  offset entity sprite.y by -height; scale shadow by (1 - height*k) for height illusion.
  Shadow is a SIBLING of the entity (not a child) so it can have its own z_index slot
  one below the entity, regardless of how Y-sort reorders the entity.
- Skip: Node25D/Basis25D/Transform25D (require real 3D math); per-view-mode texture swap
  (we have only top-down); [Tool] attribute (we don't ship a custom editor).

Return concise summary.
