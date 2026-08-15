---
Task ID: IDX-2
Agent: index-procedural-docs
Task: Index procedural geometry + noise generation docs

Work Log:
- Attempted z-ai page_reader CLI on all 5 Godot docs URLs — all timed out (godot.org Sphinx pages are large; page_reader exceeded context deadline).
- Fell back to `curl` + Python HTML strip to extract article body from each page (worked reliably).
- Fetched & indexed 5 official Godot docs pages:
  1. https://docs.godotengine.org/en/stable/tutorials/3d/procedural_geometry/index.html — overview of ArrayMesh / SurfaceTool / ImmediateMesh / MeshDataTool, when to use each
  2. https://docs.godotengine.org/en/stable/tutorials/3d/procedural_geometry/arraymesh.html — add_surface_from_arrays(), Mesh.ARRAY_VERTEX/UV/INDEX layout, full code sample
  3. https://docs.godotengine.org/en/stable/tutorials/3d/procedural_geometry/using_arraymesh.html — 404 (page lives at arraymesh.html, title is "Using the ArrayMesh")
  4. https://docs.godotengine.org/en/stable/tutorials/3d/procedural_geometry/surfacetool.html — begin/set_normal/set_uv/add_vertex/commit pattern
  5. https://docs.godotengine.org/en/stable/classes/class_fastnoiselite.html — all properties + NoiseType/FractalType/Cellular* enum tables
  6. https://docs.godotengine.org/en/stable/classes/class_noise.html — inherited get_noise_2d/get_noise_2dv/get_image/get_seamless_image methods
- Ran 6 z-ai web_search queries: FastNoiseLite terrain tutorials, cellular automata caves, ArrayMesh API, biome mapping, procedural patterns, FastNoiseLite 2D tilemap
- Fetched 2 community tutorials in full via curl: ziva.sh "5 Patterns" (Whittaker biomes, cellular automata 4-5 rule, BSP, WFC, drunkard's walk) and abitawake.com "Cellular Automata Caves" (complete pipeline: fill → random_floor → dig_caves → flood_fill get_caves → drunken_walk connect_caves)

Key techniques found:
- FastNoiseLite: 6 noise types (Value, ValueCubic, Perlin, Cellular, Simplex, SimplexSmooth), 4 fractal types (None, FBM, Ridged, PingPong), 4 cellular distance functions, 7 cellular return types
- get_noise_2d returns [-1, 1]; remap to [0, 1] via (h + 1.0) * 0.5
- Two-key tuning knobs: frequency (smaller = bigger features) + fractal_octaves (more = more detail); freq=0.02 + 4 octaves ≈ natural terrain
- Domain warping for organic coastlines (domain_warp_enabled + amplitude/frequency)
- Whittaker biome diagram: 3-noise stack (elevation + temperature + moisture) with different seeds & frequencies → biome lookup grid
- Cellular automata 4-5 rule: random fill 50%, then "wall if ≥4 of 8 neighbors are wall", 4-5 iterations
- Post-CA: flood-fill connected components, prune small caves (< min_cave_size), drunken-walk tunnel between largest regions
- Performance: >100x100 tiles locks main thread ~200ms — use Thread or chunked await
- Godot 4.3+: TileMapLayer replaces deprecated TileMap

Stage Summary:
- Index file created: /home/z/my-project/Ai-game4/docs/docs_v2/10_godot_reference/godot_procedural_index.md (9 sections, ~520 lines)
- Key APIs: FastNoiseLite (noise_type, frequency, fractal_*, cellular_*, domain_warp_*), Noise.get_noise_2d/2dv/get_image/get_seamless_image, ArrayMesh.add_surface_from_arrays + Mesh.ARRAY_* indices, SurfaceTool.begin/set_normal/set_uv/add_vertex/commit/generate_normals/index, TileMapLayer.set_cell/clear, Thread for off-main-thread generation
- Techniques for 2D terrain:
  1. FastNoiseLite heightmap → threshold tile mapping (deep_water/shallow_water/sand/grass/forest/mountain/snow)
  2. Two/three-noise Whittaker biome grid (elevation × temperature × moisture)
  3. Domain warping for organic coastlines
  4. Cellular automata caves (50% fill, 4-5 iterations, ≥4 wall-neighbor rule)
  5. Flood-fill connected-component detection + prune small caves
  6. Drunken-walk tunneling to connect disconnected cave regions
  7. Threaded / chunked generation to avoid main-thread lock on large maps
- Verdict for our 2D top-down game: ArrayMesh/SurfaceTool/ImmediateMesh/MeshDataTool are 3D-only and not applicable — focus is FastNoiseLite + TileMapLayer + custom CA grid code
