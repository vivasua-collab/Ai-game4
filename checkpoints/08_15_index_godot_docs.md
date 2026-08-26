---
Task ID: IDX-1
Agent: index-godot-docs
Task: Index Godot 4.x documentation for 2D terrain techniques

Work Log:
- Verified z-ai CLI availability (v at /usr/local/bin/z-ai) and page_reader function signature.
- Created target dir `/home/z/my-project/Ai-game4/docs/docs_v2/10_godot_reference/` (did not exist).
- Fetched all 7 requested doc pages via `z-ai function -n page_reader`. Initial parallel batch hit a 429 rate-limit (5 of 7 failed); retried the 5 failures sequentially with 10-15s sleeps — all succeeded.
- Discovered 2 stale URLs in the task brief and corrected them (confirmed via the 2D tutorials toctree, page #2):
    * TileMap page: `using_tilemap.html` (singular) → 404. Correct: `using_tilemaps.html` (plural). The 4.7 page documents the new `TileMapLayer` node (legacy `TileMap` node is deprecated).
    * Particle systems page: `2d_particle_systems.html` → 404 (Страница не найдена). Correct: `particle_systems_2d.html`.
- Extracted text from each page's HTML payload (Python + BeautifulSoup), stripping nav/script/style/svg and collapsing blank lines.
- Read each extracted text file; grep'd for API names, class references, code patterns, and section headers.
- Wrote comprehensive 11-section index document covering: pages summary, URL corrections, per-page deep dives (nodes, properties, techniques, code patterns, sub-page links), cross-cutting synthesis table mapping terrain concerns → Godot APIs, top-priority API name list, open questions / next-reading list, and fetch provenance.

Pages fetched (all HTTP 200 after URL correction):
1. https://docs.godotengine.org/ru/4.x/                                          — landing page (3 088 chars; confirms 4.7 RU branch)
2. https://docs.godotengine.org/ru/4.x/tutorials/2d/index.html                   — 2D tutorials toctree (1 424 chars; gave us the corrected URLs)
3. https://docs.godotengine.org/ru/4.x/tutorials/2d/using_tilesets.html          — TileSets (25 324 chars)
4. https://docs.godotengine.org/en/stable/tutorials/2d/using_tilemaps.html       — Using TileMaps / TileMapLayer (16 071 chars)
5. https://docs.godotengine.org/ru/4.x/tutorials/2d/particle_systems_2d.html     — 2D particle systems (9 909 chars)
6. https://docs.godotengine.org/ru/4.x/tutorials/2d/custom_drawing_in_2d.html    — Custom drawing in 2D (30 583 chars)
7. https://docs.godotengine.org/ru/4.x/tutorials/2d/2d_lights_and_shadows.html   — 2D lights and shadows (17 995 chars)

Key techniques found:
- TileSet Terrains system (autotiling replacement for Godot 3.x autotiles): Connect / Path / tile-specific painting modes; Match Corners and Sides / Match Corners / Match Sides; 8-directional Peering Bits.
- TileSet multi-layer data: Physics Layers (collision), Navigation Layers, Occlusion Layers (shadows), Custom Data Layers (gameplay metadata), Terrain Sets — all per-tile.
- TileMapLayer (4.7 replacement for TileMap): Rendering Quadrant Size for perf, Y Sort Origin for top-down, X Draw Order Reversed, multiple layers for foreground/background.
- TileMap editor: Paint/Line/Rectangle/Bucket Fill/Picker/Eraser tools; Randomisation + Scattering for procedural grass/crumbs; Patterns (TileMapPattern stored in TileSet) for premade stamps.
- Navigation caveat: built-in TileMap nav has limitations; bake to NavigationRegion2D / NavigationServer2D instead. 2D nav meshes cannot be stacked.
- Custom drawing: `_draw()` virtual on CanvasItem-derived nodes; `queue_redraw()` to refresh; `@tool` for editor preview; full draw API (draw_texture/rect/line/multiline/polyline/polygon/colored_polygon/circle/arc/string/set_transform); PackedVector2Array for batch points. Recommended for "many simple objects" (e.g. a grid) to avoid node overhead.
- Particle systems: GPUParticles2D (recommended) + ParticleProcessMaterial; CPUParticles2D fallback; flipbook animation via CanvasItemMaterial; Local Coords=false for world-space ambient fog; Preprocess for already-populated fog on scene load. 4.3 caveat: no physics interpolation for 2D particles.
- 2D lights: CanvasModulate (ambient), PointLight2D (textures + GradientTexture2D Radial for procedural falloff), DirectionalLight2D (sun), LightOccluder2D (shadow casters, can be per-tile via TileSet Occlusion Layer). SDF Collision for custom finite-shadow shaders. CanvasTexture for normal+specular maps. Light/Item Cull Masks for selective lighting. Additive Sprite2D as perf fallback.
- Pixel-art lighting shader: floor-snapping LIGHT_VERTEX/SHADOW_VERTEX to pixel grid.
- Multi-layer terrain strategy: separate TileMapLayer nodes for ground/detail/walls/decals/occlusion; each independently toggleable and Y-sortable.
- MultiMeshInstance2D (referenced via perf docs) for thousands of instanced decorations (grass/rocks) — not detailed in fetched pages, flagged for next reading batch.

Stage Summary:
- Index file created: /home/z/my-project/Ai-game4/docs/docs_v2/10_godot_reference/godot_2d_terrain_index.md
- Key APIs identified:
    * Nodes: TileMapLayer, TileSet, TileSetAtlasSource, TileSetScenesCollectionSource, TileMapPattern, TileData, Polygon2D, Line2D, CanvasModulate, PointLight2D, DirectionalLight2D, LightOccluder2D, OccluderPolygon2D, GPUParticles2D, CPUParticles2D, ParticleProcessMaterial, NavigationRegion2D, MultiMeshInstance2D, Sprite2D, CanvasItemMaterial, CanvasTexture, Camera2D
    * Resources: TileSet (.tres), TileMapPattern, GradientTexture2D (Radial), CanvasTexture, CanvasItemMaterial, FastNoiseLite, OccluderPolygon2D
    * CanvasItem draw API: _draw(), queue_redraw(), draw_texture/rect/line/multiline/multiline_colors/polyline/polyline_colors/polygon/colored_polygon/circle/arc/string/set_transform/set_transform_matrix
    * TileMapLayer runtime: set_cell(), get_cell_tile_data(), set_pattern(), clear()
    * TileData runtime: get_custom_data(), get_navigation_polygon(), get_collision_polygon_shape(), get_occluder_polygon(), get_terrain_set/terrain/terrain_peering_bits()
    * Light2D constants: BLEND_MODE_ADD/SUBTRACT/MIX, SHADOW_FILTER_NONE/PCF5/PCF13
    * Pixel-art shader: shader_type canvas_item; LIGHT_VERTEX / SHADOW_VERTEX floor-snapping
- Techniques applicable to our terrain:
    * Grid-based tile terrain via TileMapLayer + TileSet (atlas source)
    * Auto-connecting biome edges via TileSet Terrains (Connect mode)
    * Procedural decoration scatter via editor Randomisation+Scattering and runtime randf()-gated set_cell
    * Premade structure stamps via TileMapPattern
    * Per-tile gameplay metadata via TileSet Custom Data Layers (walk_speed_multiplier, is_water, biome_id, footstep_sound)
    * Per-tile collision via TileSet Physics Layers
    * Per-tile shadow casting via TileSet Occlusion Layers (auto-generates LightOccluder2D per solid tile)
    * Pathfinding via baked NavigationRegion2D (NOT built-in TileMap nav)
    * Layered terrain via multiple TileMapLayer nodes (ground/detail/walls/decals/occlusion)
    * Top-down depth via CanvasItem.Y Sort Enabled + TileMapLayer.y_sort_origin + per-tile Y Sort Origin
    * Organic (non-tile) terrain shapes via Polygon2D node or custom _draw() with draw_polygon()
    * Procedural heightmap visualisation via custom _draw() iterating cells with draw_rect()
    * Contour/outline rendering via draw_polyline()/draw_multiline()
    * Thousands of instanced decorations via MultiMeshInstance2D (flagged for next reading)
    * Editor preview of procedural gen via @tool annotation
    * Day/night cycle via single DirectionalLight2D + CanvasModulate colour lerp
    * Local torches via PointLight2D + GradientTexture2D (Radial, no image asset)
    * Terrain bumpiness via CanvasTexture normal+specular maps (Laigter for normal-map generation)
    * Selective lighting via Light2D.range_item_cull_mask + CanvasItem.light_mask
    * Pixel-art lighting via custom canvas_item shader (LIGHT_VERTEX/SHADOW_VERTEX floor-snapping)
    * Ambient fog via GPUParticles2D (Local Coords=false, large Visibility Rect, Preprocess>0)
    * Animated terrain tiles (waterfall, lava) via Scene tiles containing GPUParticles2D or AnimationPlayer
    * Tiled background textures via Sprite2D.region_enabled + CanvasTexture.texture_repeat
