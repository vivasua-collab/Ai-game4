#nullable enable
using Godot;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;
// Note: IPlayerService no longer needed — player sprite handled by GameWorldController.

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

    private Node2D _worldRoot = null!;
    private MultiMeshInstance2D _terrainMesh = null!;

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

        // NOTE: Camera and Player sprite are created by GameWorldController (parent).
        // SceneBuilder only creates terrain + transition tiles (rendering only).
        SetupTerrainMesh();
        SetupTransitionTiles();
    }

    /// <summary>
    /// Add transition tile overlays for smooth terrain edges.
    /// Uses _draw() on Node2D (Godot 4.7 canonical for custom 2D drawing).
    /// </summary>
    private void SetupTransitionTiles()
    {
        if (TileService == null) return;
        var renderer = new TransitionTileRenderer();
        _worldRoot.AddChild(renderer);
        renderer.Initialize(TileService, GameConstants.TILE_PIXELS);
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
                var color = TileColor(tile.Terrain, x, y);

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

        // Ambient lighting via CanvasModulate — gives the world a warm tone.
        // This acts as a global color multiplier (like a 2D light without Light2D).
        var modulate = new CanvasModulate
        {
            Name = "AmbientLight",
            Color = new Color(1.05f, 1.0f, 0.95f, 1.0f),  // warm daylight
        };
        _worldRoot.AddChild(modulate);
    }

    private static Color TileColor(TerrainType terrain, int x, int y)
    {
        // Use shared palette from TerrainColors (same as TransitionTileRenderer).
        return TerrainColors.Get(terrain);
    }
}
