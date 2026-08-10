#nullable enable
using Godot;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Procedural scene builder. When added as a child of a <see cref="Node2D"/>
/// world root, it constructs the Camera2D, terrain and object TileMapLayers,
/// the player sprite (procedurally generated texture), and a subtle global
/// 2D light.
///
/// The world root is resolved via <see cref="GetParent"/>. This keeps the
/// builder decoupled from a specific parent node name.
/// </summary>
public partial class SceneBuilder : Node
{
    [Inject] private ITileService   TileService   { get; set; } = null!;
    [Inject] private IPlayerService PlayerService { get; set; } = null!;

    private Node2D _worldRoot = null!;
    private Camera2D _camera = null!;
    private TileMapLayer _terrainLayer = null!;
    private TileMapLayer _objectLayer = null!;
    private Sprite2D _playerSprite = null!;

    public override void _Ready()
    {
        // Wire DI from the global GameBoot container.
        var container = Scene.GameBoot.Container;
        if (container != null)
        {
            ContainerAdapter.InjectProperties(this, container);
        }

        // Resolve the world root: prefer an explicit Node2D parent.
        if (GetParent() is Node2D parent2D)
        {
            _worldRoot = parent2D;
        }
        else
        {
            // Fallback: create our own sub-tree.
            _worldRoot = new Node2D { Name = "WorldRoot" };
            AddChild(_worldRoot);
        }

        SetupCamera();
        SetupTileMap();
        SetupPlayer();
        SetupLight();
    }

    private void SetupCamera()
    {
        _camera = new Camera2D
        {
            Name = "MainCamera",
            Zoom = new Vector2(1f, 1f),
            PositionSmoothingEnabled = true,
            PositionSmoothingSpeed = 5.0f,
            ZIndex = (int)RenderLayer.UI,
        };
        _worldRoot.AddChild(_camera);
        _camera.MakeCurrent();
    }

    private void SetupTileMap()
    {
        // Godot 4.3 has TileMapLayer as a dedicated node (replacing TileMap with layers).
        _terrainLayer = new TileMapLayer
        {
            Name = "Terrain",
            ZIndex = (int)RenderLayer.Terrain,
        };
        _worldRoot.AddChild(_terrainLayer);

        _objectLayer = new TileMapLayer
        {
            Name = "Objects",
            ZIndex = (int)RenderLayer.Objects,
            YSortEnabled = true, // Y-sort for objects (see RENDER_LAYERS.md §3.4)
        };
        _worldRoot.AddChild(_objectLayer);

        RenderTiles();
    }

    /// <summary>
    /// Renders the test polygon as coloured quads (one Polygon2D per tile).
    /// In a future iteration this will be replaced with a proper TileSet +
    /// TileMapLayer cells, but for v1 we just need visible feedback.
    /// </summary>
    private void RenderTiles()
    {
        if (TileService == null) return;

        // Default test polygon bounds — matches Entry.LocationCatalog.TestPolygon
        // (50x50 tiles @ Constants.TILE_PIXELS).
        const int width = 50;
        const int height = 50;
        int tileSize = GameConstants.TILE_PIXELS;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var tile = TileService.GetTile(x, y);
                var color = TileColor(tile.Terrain);

                var quad = new Polygon2D
                {
                    Name = $"Tile_{x}_{y}",
                    Polygon = new Vector2[]
                    {
                        new(x * tileSize,        y * tileSize),
                        new((x + 1) * tileSize,  y * tileSize),
                        new((x + 1) * tileSize,  (y + 1) * tileSize),
                        new(x * tileSize,        (y + 1) * tileSize),
                    },
                    Color = color,
                    ZIndex = (int)RenderLayer.Terrain,
                };
                _terrainLayer.AddChild(quad);
            }
        }
    }

    private static Color TileColor(TerrainType terrain)
    {
        return terrain switch
        {
            TerrainType.Grass => new Color(0.30f, 0.50f, 0.25f),
            TerrainType.Dirt  => new Color(0.45f, 0.35f, 0.20f),
            TerrainType.Stone => new Color(0.50f, 0.50f, 0.50f),
            TerrainType.Water => new Color(0.20f, 0.30f, 0.60f),
            TerrainType.Sand  => new Color(0.85f, 0.80f, 0.55f),
            TerrainType.Snow  => new Color(0.92f, 0.95f, 0.98f),
            TerrainType.Ice   => new Color(0.70f, 0.85f, 0.95f),
            TerrainType.Lava  => new Color(0.85f, 0.25f, 0.10f),
            TerrainType.Void  => new Color(0.05f, 0.02f, 0.10f),
            _                 => new Color(0.30f, 0.50f, 0.25f),
        };
    }

    private void SetupPlayer()
    {
        _playerSprite = new Sprite2D
        {
            Name = "PlayerSprite",
            ZIndex = (int)RenderLayer.Player,
            Texture = CreatePlayerTexture(),
        };
        _worldRoot.AddChild(_playerSprite);

        if (PlayerService != null)
        {
            var pos = PlayerService.Position;
            _playerSprite.Position = new Vector2(pos.X * GameConstants.TILE_PIXELS, pos.Y * GameConstants.TILE_PIXELS);
        }
    }

    /// <summary>
    /// Creates a 48x48 RGBA8 texture procedurally: purple robe body + skin head + black hair.
    /// FilterMode=Point per SPRINT_CATALOG for pixel-perfect 2D look.
    /// </summary>
    private static ImageTexture CreatePlayerTexture()
    {
        var img = Image.CreateEmpty(48, 48, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));

        // Robe body.
        for (int y = 16; y < 40; y++)
            for (int x = 12; x < 36; x++)
                img.SetPixel(x, y, new Color(0.30f, 0.20f, 0.50f));
        // Head (skin).
        for (int y = 4; y < 16; y++)
            for (int x = 16; x < 32; x++)
                img.SetPixel(x, y, new Color(0.90f, 0.75f, 0.60f));
        // Hair.
        for (int y = 4; y < 10; y++)
            for (int x = 16; x < 32; x++)
                img.SetPixel(x, y, new Color(0.10f, 0.05f, 0.02f));

        var tex = ImageTexture.CreateFromImage(img);
        return tex;
    }

    private void SetupLight()
    {
        // 2D global light. Subtle — gives the world a small amount of depth.
        // Disabled for v1 to keep the rendering pipeline trivial; can be enabled
        // later by adding PointLight2D/DirectionalLight2D with a CanvasModulate parent.
    }

    public override void _PhysicsProcess(double delta)
    {
        // Sync player sprite position from PlayerService every physics frame.
        if (_playerSprite != null && PlayerService != null)
        {
            var pos = PlayerService.Position;
            _playerSprite.Position = new Vector2(pos.X * GameConstants.TILE_PIXELS, pos.Y * GameConstants.TILE_PIXELS);
        }

        // Camera follow player.
        if (_camera != null && _playerSprite != null)
        {
            _camera.Position = _playerSprite.Position;
        }
    }
}
