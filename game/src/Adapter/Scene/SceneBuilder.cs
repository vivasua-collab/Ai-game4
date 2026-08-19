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

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
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

        GD.Print($"[BiomeTiles] Drew {drawn} textures, {missing} missing (fallback red)");
    }
}
