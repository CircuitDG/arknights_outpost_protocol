using Godot;
using OutpostProtocol.Core.Grid;
using OutpostProtocol.Gameplay.Entity.Components;

/// <summary>
/// Grid/A* 自动化测试：
/// 1. 运行时生成 TileSet 并绘制 20x20 地图（边框 + 中部墙壁为障碍）
/// 2. 构建网格并验证 A* 路径、障碍规避、不可达目标、路径平滑
/// 3. 通过 MovementComponent.MoveTo 验证异步寻路 + 路径跟随
/// </summary>
public partial class TestGridController : Node
{
    private const int GridWidth = 20;
    private const int GridHeight = 20;
    private const int TileSize = 16;

    private GridManager _grid;
    private MovementComponent _movement;
    private TileMapLayer _groundLayer;
    private TileMapLayer _obstacleLayer;
    private Node2D _testUnit;
    private Vector2 _startPos;
    private int _frameCount;

    public override void _Ready()
    {
        _grid = GridManager.Instance;
        _testUnit = GetNode<Node2D>("../TestUnit");
        _movement = GetNode<MovementComponent>("../TestUnit/MovementComponent");
        _groundLayer = GetNode<TileMapLayer>("../GroundLayer");
        _obstacleLayer = GetNode<TileMapLayer>("../ObstacleLayer");

        if (_grid == null || _movement == null)
        {
            GD.PrintErr("[TestGrid] GridManager 或 MovementComponent 未就绪");
            return;
        }

        // 网格尺寸必须与瓦片尺寸对齐
        _grid.GridSize = TileSize;

        // 构建测试地图
        SetupTileSet(_groundLayer);
        SetupTileSet(_obstacleLayer);
        PaintGround();
        PaintObstacles();

        // 注入障碍层并构建网格
        _grid.ObstacleLayer = _obstacleLayer;
        _grid.BuildGrid();

        _startPos = _testUnit.GlobalPosition;
        RunPathfindingTests();
    }

    public override void _Process(double delta)
    {
        if (_grid == null || _movement == null || _testUnit == null) return;

        _frameCount++;

        if (_frameCount == 5)
        {
            GD.Print($"[TestGrid] 开始 MoveTo 测试 — 起点 {_testUnit.GlobalPosition}");
            _movement.MoveTo(_grid.GridToWorld(18, 18));
        }
        else if (_frameCount == 45)
        {
            float moved = _testUnit.GlobalPosition.DistanceTo(_startPos);
            GD.Print($"[TestGrid] 移动状态: IsMoving={_movement.IsMoving}, 已移动距离={moved:F1}px, 当前位置={_testUnit.GlobalPosition}");
        }
        else if (_frameCount >= 50)
        {
            GD.Print("[TestGrid] 测试完成");
            GetTree().Quit();
        }
    }

    public override void _Input(InputEvent @event)
    {
        // 左键点击 → 移动到点击位置（编辑器内手动测试用）
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            var viewport = GetViewport();
            var camera = viewport.GetCamera2D();
            if (camera == null) return;

            Vector2 mousePos = mouseEvent.Position;
            Vector2 worldPos = camera.GetScreenCenterPosition() + (mousePos - viewport.GetVisibleRect().Size * 0.5f) / camera.Zoom;

            GD.Print($"[TestGrid] 点击移动至: {worldPos}");
            _movement?.MoveTo(worldPos);
        }
    }

    // ============================================================
    // 地图构建
    // ============================================================

    private void SetupTileSet(TileMapLayer layer)
    {
        var tileSet = new TileSet { TileSize = new Vector2I(TileSize, TileSize) };
        var atlas = new TileSetAtlasSource
        {
            Texture = GD.Load<Texture2D>("res://icon.svg"),
            TextureRegionSize = new Vector2I(TileSize, TileSize),
        };

        atlas.CreateTile(new Vector2I(0, 0)); // 地面
        atlas.CreateTile(new Vector2I(1, 0)); // 障碍
        tileSet.AddSource(atlas, 0);
        layer.TileSet = tileSet;
    }

    private void PaintGround()
    {
        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                _groundLayer.SetCell(new Vector2I(x, y), 0, new Vector2I(0, 0));
            }
        }
    }

    private void PaintObstacles()
    {
        // 边框墙
        for (int x = 0; x < GridWidth; x++)
        {
            _obstacleLayer.SetCell(new Vector2I(x, 0), 0, new Vector2I(1, 0));
            _obstacleLayer.SetCell(new Vector2I(x, GridHeight - 1), 0, new Vector2I(1, 0));
        }
        for (int y = 0; y < GridHeight; y++)
        {
            _obstacleLayer.SetCell(new Vector2I(0, y), 0, new Vector2I(1, 0));
            _obstacleLayer.SetCell(new Vector2I(GridWidth - 1, y), 0, new Vector2I(1, 0));
        }

        // 中部竖直墙（y=5..14 在 x=10），留出上下通道
        for (int y = 5; y <= 14; y++)
        {
            _obstacleLayer.SetCell(new Vector2I(10, y), 0, new Vector2I(1, 0));
        }
    }

    // ============================================================
    // 寻路验证
    // ============================================================

    private void RunPathfindingTests()
    {
        GD.Print($"[TestGrid] 网格构建成功 — 尺寸: {_grid.GridDimensions}, 原点: {_grid.WorldOrigin}");

        Vector2 startWorld = _grid.GridToWorld(1, 1);
        Vector2 endWorld = _grid.GridToWorld(18, 18);

        // 平滑路径
        var smoothedPath = AStarPathfinder.FindPath(startWorld, endWorld, _grid);
        GD.Print($"[TestGrid] A* 平滑路径: {smoothedPath.Count} 个点, 起点 {smoothedPath[0]}, 终点 {smoothedPath[^1]}");

        // 路径必须全部落在可行走格
        bool allWalkable = true;
        foreach (var point in smoothedPath)
        {
            if (!_grid.IsWalkableWorld(point))
            {
                allWalkable = false;
                break;
            }
        }
        GD.Print($"[TestGrid] 路径全部落在可行走格: {allWalkable}");

        // 未平滑路径对比
        var rawPath = AStarPathfinder.FindPath(startWorld, endWorld, _grid,
            new AStarPathfinder.PathConfig { SmoothPath = false });
        GD.Print($"[TestGrid] 原始路径 {rawPath.Count} 点 → 平滑后 {smoothedPath.Count} 点");

        // 不可达目标（墙内）→ 空路径
        Vector2 wallWorld = _grid.GridToWorld(10, 10);
        var emptyPath = AStarPathfinder.FindPath(startWorld, wallWorld, _grid);
        GD.Print($"[TestGrid] 墙内目标路径点数(应为0): {emptyPath.Count}");
    }
}
