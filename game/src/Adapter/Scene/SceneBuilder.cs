#nullable enable
using Godot;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Procedural scene builder. Constructs Camera2D, terrain tile layer (via
/// <see cref="MultiMeshInstance2D"/> — one draw call for all tiles), object
/// layer, and the player sprite (procedural texture).
///
/// Godot 4.7 notes:
///  • TileMapLayer is the canonical tile node (TileMap with layers is deprecated since 4.5).
///  • For pure coloured tiles, MultiMeshInstance2D is dramatically faster than
///    per-tile Polygon2D (1 draw call vs N draw calls). We use it for the terrain.
///  • Player texture is generated via <see cref="Image"/> + <see cref="ImageTexture"/>
///    (DrawableTexture2D is for runtime drawing; our texture is static, so Image is enough).
/// </summary>
public partial class SceneBuilder : Node
{
    [Inject] private ITileService   TileService   { get; set; } = null!;
    [Inject] private IPlayerService PlayerService { get; set; } = null!;

    private Node2D _worldRoot = null!;
    private Camera2D _camera = null!;
    private MultiMeshInstance2D _terrainMesh = null!;
    private Sprite2D _playerSprite = null!;

    public override void _Ready()
    {
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
            _worldRoot = new Node2D { Name = "WorldRoot" };
            AddChild(_worldRoot);
        }

        SetupCamera();
        SetupTerrainMesh();
        SetupPlayer();
    }

    private void SetupCamera()
    {
        _camera = new Camera2D
        {
            Name = "MainCamera",
            Zoom = new Vector2(1f, 1f),
            PositionSmoothingEnabled = true,
            PositionSmoothingSpeed = 5.0f,
            // 4.7: PositionSmoothingEnabled now works correctly with ProcessCallback.
            ProcessCallback = Camera2D.Camera2DProcessCallback.Physics,
        };
        _worldRoot.AddChild(_camera);
        _camera.MakeCurrent();
    }

    /// <summary>
    /// Render the test polygon as a single MultiMesh of colored quads.
    /// One draw call for all 2500 tiles (vs 2500 Polygon2D nodes = 2500 draw calls).
    /// </summary>
    private void SetupTerrainMesh()
    {
        if (TileService == null) return;

        const int width = 50;
        const int height = 50;
        int tileSize = GameConstants.TILE_PIXELS;

        // Create a 1×1 quad mesh as the prototype for the MultiMesh.
        var quadMesh = new QuadMesh
        {
            Size = new Vector2(tileSize, tileSize),
        };

        var multimesh = new MultiMesh
        {
            Mesh = quadMesh,
            TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
            UseColors = true,
        };
        multimesh.InstanceCount = width * height;

        // Populate each tile instance: position + color.
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                var tile = TileService.GetTile(x, y);
                var color = TileColor(tile.Terrain);

                // QuadMesh is centered on origin; offset to tile center.
                var transform = new Transform2D(
                    0f,                          // rotation
                    new Vector2(tileSize, tileSize), // scale (already in mesh size, so identity)
                    0f,
                    new Vector2(x * tileSize + tileSize / 2f, y * tileSize + tileSize / 2f) // position
                );
                // Actually, for 2D, use Transform2D.Identity with translation:
                transform = Transform2D.Identity.Translated(
                    new Vector2(x * tileSize + tileSize / 2f, y * tileSize + tileSize / 2f));

                multimesh.SetInstanceTransform2D(idx, transform);
                multimesh.SetInstanceColor(idx, color);
            }
        }

        _terrainMesh = new MultiMeshInstance2D
        {
            Name = "TerrainMesh",
            Multimesh = multimesh,
            ZIndex = (int)RenderLayer.Terrain,
        };
        _worldRoot.AddChild(_terrainMesh);
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
            TerrainType.Road  => new Color(0.55f, 0.45f, 0.30f),
            TerrainType.Bush  => new Color(0.25f, 0.40f, 0.15f),
            TerrainType.TallGrass => new Color(0.35f, 0.55f, 0.30f),
            TerrainType.ShallowWater => new Color(0.30f, 0.45f, 0.65f),
            TerrainType.DeepWater => new Color(0.10f, 0.20f, 0.50f),
            TerrainType.Mountain => new Color(0.45f, 0.42f, 0.40f),
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
            // 4.7: TextureFilter is on the node, not the texture.
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
        _worldRoot.AddChild(_playerSprite);

        if (PlayerService != null)
        {
            var pos = PlayerService.Position;
            _playerSprite.Position = new Vector2(
                pos.X * GameConstants.TILE_PIXELS + GameConstants.TILE_PIXELS / 2f,
                pos.Y * GameConstants.TILE_PIXELS + GameConstants.TILE_PIXELS / 2f
            );
        }
    }

    /// <summary>
    /// Creates a 48×48 RGBA8 texture procedurally: purple robe body + skin head + black hair.
    /// FilterMode=Point (set on the Sprite2D via TextureFilter) per SPRINT_CATALOG
    /// for pixel-perfect 2D look.
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
        // Simple eyes (2 pixels).
        img.SetPixel(20, 12, new Color(0.05f, 0.05f, 0.05f));
        img.SetPixel(27, 12, new Color(0.05f, 0.05f, 0.05f));

        return ImageTexture.CreateFromImage(img);
    }

    public override void _PhysicsProcess(double delta)
    {
        // Sync player sprite position from PlayerService every physics frame.
        if (_playerSprite != null && PlayerService != null)
        {
            var pos = PlayerService.Position;
            _playerSprite.Position = new Vector2(
                pos.X * GameConstants.TILE_PIXELS + GameConstants.TILE_PIXELS / 2f,
                pos.Y * GameConstants.TILE_PIXELS + GameConstants.TILE_PIXELS / 2f
            );
        }

        // Camera follow player.
        if (_camera != null && _playerSprite != null)
        {
            _camera.Position = _playerSprite.Position;
        }
    }
}
