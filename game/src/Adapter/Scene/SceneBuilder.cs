#nullable enable
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Procedural scene builder. Renders stratum 0 (biome) using sprite textures
/// from resources/tiles/64/, and stratum 1 (surface transitions) via
/// SurfaceTransitionRenderer.
/// </summary>
public partial class SceneBuilder : Node
{
    [Inject] private ITileService TileService { get; set; } = null!;

    private Node2D _worldRoot = null!;

    // Cached biome textures (loaded once from resources/tiles/64/).
    private static Dictionary<BiomeType, Texture2D>? _biomeTextures;

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
        {
            ContainerAdapter.InjectProperties(this, container);
        }

        if (GetParent() is Node2D parent2D)
        {
            _worldRoot = parent2D;
        }
        else
        {
            _worldRoot = new Node2D { Name = "WorldRoot" };
            AddChild(_worldRoot);
        }

        // Stratum 0: biome sprite textures.
        SetupTerrainSprites();
        // Stratum 1: surface transition sprites.
        SetupSurfaceTransitions();
        // Stratum 3+: environment objects (trees, rocks, bushes, ore).
        SetupObjectLayer();
        // Ground items (dropped from inventory overflow + player throw).
        SetupGroundItems();
        // Phase C: wandering animals (wolf/deer/rabbit) — drawn as coloured circles.
        SetupAnimals();
        // NPC_COMBAT_PREP Phase 1: human NPCs — coloured circles per role.
        SetupNPCs();
        // Qi Stage 6: formation visual renderer (contour + runes + progress arc).
        SetupFormationVisuals();
        // NPC_COMBAT_PREP Phase 7: floating damage numbers (DamageAppliedEvent).
        SetupDamageNumbers();
    }

    /// <summary>
    /// Load biome textures from resources/tiles/64/biome_{name}.png.
    /// Cached statically — loaded once per session.
    /// </summary>
    private static Dictionary<BiomeType, Texture2D> LoadBiomeTextures()
    {
        if (_biomeTextures != null) return _biomeTextures;

        _biomeTextures = new Dictionary<BiomeType, Texture2D>();
        var basePath = "res://resources/tiles/64/biome_";

        foreach (BiomeType biome in System.Enum.GetValues(typeof(BiomeType)))
        {
            // Skip legacy aliases (they have same values as real biomes).
            string name = biome.ToString().ToLowerInvariant();
            if (name == "plains" || name == "desert" || name == "swamp" ||
                name == "tundra" || name == "jungle" || name == "volcanic" || name == "spiritual")
                continue;

            string path = $"{basePath}{name}.png";
            var tex = GD.Load<Texture2D>(path);
            if (tex != null)
            {
                _biomeTextures[biome] = tex;
                GD.Print($"[SceneBuilder] Loaded biome texture: {path}");
            }
            else
            {
                GD.PrintErr($"[SceneBuilder] Missing biome texture: {path}");
            }
        }

        return _biomeTextures;
    }

    /// <summary>
    /// Stratum 0: render biome sprites via a single Node2D with _Draw().
    /// Each tile draws its biome texture at the correct position.
    /// </summary>
    private void SetupTerrainSprites()
    {
        if (TileService == null) return;

        var textures = LoadBiomeTextures();
        var renderer = new BiomeTileRenderer();
        renderer.Initialize(TileService, GameConstants.TILE_PIXELS, textures);
        _worldRoot.AddChild(renderer);

        // Ambient lighting.
        var modulate = new CanvasModulate
        {
            Name = "AmbientLight",
            Color = new Color(1.05f, 1.0f, 0.95f, 1.0f),
        };
        _worldRoot.AddChild(modulate);
    }

    /// <summary>
    /// Stratum 1: surface transition sprites.
    /// </summary>
    private void SetupSurfaceTransitions()
    {
        if (TileService == null) return;
        var renderer = new SurfaceTransitionRenderer();
        _worldRoot.AddChild(renderer);
        renderer.Initialize(TileService, GameConstants.TILE_PIXELS);
    }

    /// <summary>
    /// Stratum 3+: environment objects (trees, rocks, bushes, ore).
    /// Procedural placeholder textures (no PNG files needed yet).
    /// </summary>
    private ObjectLayerRenderer? _objectRenderer;

    private void SetupObjectLayer()
    {
        if (TileService == null) return;
        _objectRenderer = new ObjectLayerRenderer();
        _worldRoot.AddChild(_objectRenderer);
        _objectRenderer.Initialize(TileService, GameConstants.TILE_PIXELS);
    }

    /// <summary>Refresh object layer after tile changes (harvest/depletion).</summary>
    public void RefreshObjectLayer()
    {
        _objectRenderer?.Refresh();
    }

    /// <summary>Setup ground item renderer (dropped items on world).</summary>
    private GroundItemRenderer? _groundItemRenderer;

    private void SetupGroundItems()
    {
        _groundItemRenderer = new GroundItemRenderer();
        _worldRoot.AddChild(_groundItemRenderer);
    }

    /// <summary>
    /// Phase C: wandering animals (wolf/deer/rabbit). Renderer subscribes
    /// to <see cref="Modules.NPC.AnimalService.GetAllAnimals"/> each frame
    /// and draws coloured circles per species. Spawn happens in
    /// <see cref="Entry.Phases.AnimalSpawnPhase"/> (scene-assembly phase 5).
    /// </summary>
    private AnimalSpriteRenderer? _animalRenderer;

    private void SetupAnimals()
    {
        _animalRenderer = new AnimalSpriteRenderer();
        _worldRoot.AddChild(_animalRenderer);
    }

    /// <summary>
    /// NPC_COMBAT_PREP Phase 1: human NPCs. Renderer queries NPCService each
    /// frame and draws coloured circles per role. Spawn happens in
    /// <see cref="Entry.Phases.HumanNPCSpawnPhase"/> (scene-assembly phase 6).
    /// </summary>
    private NPCSpriteRenderer? _npcRenderer;

    private void SetupNPCs()
    {
        _npcRenderer = new NPCSpriteRenderer();
        _worldRoot.AddChild(_npcRenderer);
    }

    /// <summary>Qi Stage 6: formation visual renderer — contour, runes, progress arc.</summary>
    private FormationVisualRenderer? _formationRenderer;

    private void SetupFormationVisuals()
    {
        _formationRenderer = new FormationVisualRenderer();
        _worldRoot.AddChild(_formationRenderer);
    }

    /// <summary>NPC_COMBAT_PREP Phase 7: floating damage numbers over targets.</summary>
    private DamageNumberRenderer? _damageNumberRenderer;

    private void SetupDamageNumbers()
    {
        _damageNumberRenderer = new DamageNumberRenderer();
        _worldRoot.AddChild(_damageNumberRenderer);
    }

    /// <summary>
    /// Queue redraw on all renderers (needed for viewport culling to update
    /// when camera moves). Called from GameWorldController._PhysicsProcess.
    /// </summary>
    public void QueueRedrawAll()
    {
        // Each renderer is a child Node2D; QueueRedraw propagates.
        // BiomeTileRenderer and SurfaceTransitionRenderer are added to _worldRoot,
        // not to SceneBuilder directly, so we call QueueRedraw on each.
        foreach (var child in _worldRoot.GetChildren())
        {
            if (child is CanvasItem ci)
            {
                ci.QueueRedraw();
            }
        }
    }
}

/// <summary>
/// Renders stratum 0 (biome) using sprite textures via _Draw().
/// One draw call per tile via DrawTexture — Godot batches internally.
/// </summary>
public partial class BiomeTileRenderer : Node2D
{
    private ITileService? _tileService;
    private int _tileSize;
    private Dictionary<BiomeType, Texture2D> _textures = new();

    public void Initialize(ITileService tileService, int tileSize, Dictionary<BiomeType, Texture2D> textures)
    {
        _tileService = tileService;
        _tileSize = tileSize;
        _textures = textures;
        ZIndex = (int)RenderLayer.Terrain;  // stratum 0
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_tileService == null) return;

        int w = _tileService.MapWidth;
        int h = _tileService.MapHeight;
        int drawn = 0;
        int missing = 0;

        // Viewport culling: only draw tiles visible on screen.
        // At 500×500 = 250k tiles, only ~57 are visible at default zoom.
        GetVisibleTileRange(out int xMin, out int yMin, out int xMax, out int yMax, w, h);

        for (int x = xMin; x <= xMax; x++)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                var tile = _tileService.GetTile(x, y);
                if (_textures.TryGetValue(tile.Biome, out var tex))
                {
                    DrawTexture(tex, new Vector2(x * _tileSize, y * _tileSize));
                    drawn++;
                }
                else
                {
                    // Fallback: draw colored rect if texture missing.
                    DrawRect(new Rect2(x * _tileSize, y * _tileSize, _tileSize, _tileSize),
                             new Color(0.5f, 0.2f, 0.2f), true);
                    missing++;
                }
            }
        }

        if (drawn + missing > 0)
            GD.Print($"[BiomeTiles] Drew {drawn} textures, {missing} missing (culled to {xMax-xMin+1}×{yMax-yMin+1})");
    }

    /// <summary>
    /// Compute visible tile range from viewport + camera transform.
    /// Returns clamped bounds [xMin..xMax, yMin..yMax] within [0..w, 0..h].
    /// </summary>
    private void GetVisibleTileRange(out int xMin, out int yMin, out int xMax, out int yMax, int w, int h)
    {
        // GetVisibleRect() returns viewport rect in screen space.
        // Transform canvas X-form converts to this node's local space.
        var canvasXform = GetGlobalTransformWithCanvas();
        var vpRectScreen = GetViewportRect();
        // Inverse-transform viewport corners into tile space.
        var topLeft = canvasXform.AffineInverse() * vpRectScreen.Position;
        var botRight = canvasXform.AffineInverse() * (vpRectScreen.Position + vpRectScreen.Size);

        xMin = Mathf.Clamp((int)(topLeft.X / _tileSize), 0, w - 1);
        yMin = Mathf.Clamp((int)(topLeft.Y / _tileSize), 0, h - 1);
        xMax = Mathf.Clamp((int)(botRight.X / _tileSize) + 1, 0, w - 1);
        yMax = Mathf.Clamp((int)(botRight.Y / _tileSize) + 1, 0, h - 1);
    }
}
