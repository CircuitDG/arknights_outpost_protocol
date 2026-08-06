using Godot;
using System;
using System.Collections.Generic;
using OutpostProtocol.Core.MapGeneration;

namespace OutpostProtocol.Core.Grid;

/// <summary>
/// 网格管理器（AutoLoad 单例）
/// 职责：扫描 TileMap 生成可行走网格，提供坐标转换和查询
/// </summary>
public partial class GridManager : Node2D
{
    // ============================================================
    // 单例
    // ============================================================

    private static GridManager _instance;

    /// <summary>全局单例实例（AutoLoad 就绪后可用）</summary>
    public static GridManager Instance => _instance;

    // ============================================================
    // 导出变量
    // ============================================================

    [ExportGroup("网格配置")]

    /// <summary>障碍物 TileMapLayer（场景就绪后由代码注入）</summary>
    [Export] public TileMapLayer ObstacleLayer;

    /// <summary>每格世界单位大小，必须与 TileMapLayer.TileSize 对齐（16px 瓦片 → 16.0）</summary>
    [Export] public float GridSize = 16.0f;

    /// <summary>
    /// 是否在 _Ready 自动构建。AutoLoad 无法在编辑器中引用场景节点，
    /// 因此默认关闭，由场景控制器注入 ObstacleLayer 后调用 BuildGrid()。
    /// </summary>
    [Export] public bool AutoBuildOnReady = false;

    // ============================================================
    // 网格数据
    // ============================================================

    private bool[,] _walkableGrid;
    private Vector2I _gridSize;
    private Vector2 _worldOrigin;
    private bool _isBuilt;

    // ============================================================
    // 公共属性
    // ============================================================

    public bool IsBuilt => _isBuilt;
    public Vector2I GridDimensions => _gridSize;
    public Vector2 WorldOrigin => _worldOrigin;

    // ============================================================
    // 生命周期
    // ============================================================

    public override void _Ready()
    {
        if (_instance != null)
        {
            GD.PushWarning("GridManager 已存在，销毁重复实例");
            QueueFree();
            return;
        }

        _instance = this;

        if (AutoBuildOnReady)
        {
            BuildGrid();
        }
    }

    public override void _ExitTree()
    {
        _instance = null;
    }

    // ============================================================
    // 网格构建
    // ============================================================

    /// <summary>扫描 TileMapLayer，构建可行走网格</summary>
    public void BuildGrid()
    {
        if (ObstacleLayer == null)
        {
            GD.PushError("[GridManager] 未设置 ObstacleLayer，无法构建网格");
            return;
        }

        var rect = ObstacleLayer.GetUsedRect();
        if (rect.Size.X <= 0 || rect.Size.Y <= 0)
        {
            GD.PushWarning("[GridManager] TileMap 为空，请先在地图上放置 Tile");
            _gridSize = Vector2I.Zero;
            _walkableGrid = new bool[0, 0];
            _isBuilt = false;
            return;
        }

        _gridSize = rect.Size;
        _worldOrigin = (Vector2)rect.Position * GridSize;

        _walkableGrid = new bool[_gridSize.X, _gridSize.Y];

        // 扫描每个格子
        int walkableCount = 0;
        for (int x = 0; x < _gridSize.X; x++)
        {
                for (int y = 0; y < _gridSize.Y; y++)
                {
                    Vector2I cellPos = rect.Position + new Vector2I(x, y);
                    bool hasTile = ObstacleLayer.GetCellSourceId(cellPos) != -1;
                    _walkableGrid[x, y] = !hasTile;
                    if (_walkableGrid[x, y]) walkableCount++;
                }
        }

        _isBuilt = true;
        GD.Print($"[GridManager] 网格构建完成 — 尺寸:{_gridSize.X}x{_gridSize.Y}, 可行走:{walkableCount} 格");

        // GridManager 所在命名空间 OutpostProtocol.Core 下存在 EventBus 命名空间，
        // 直接用类名会被解析为命名空间，这里使用完全限定名。
        OutpostProtocol.Core.EventBus.EventBus.Instance
            .EmitLogMessage($"GridManager 网格构建完成: {walkableCount} 格可行走", "INFO");
    }

    /// <summary>重新构建网格（在 TileMap 动态修改后调用）</summary>
    public void RebuildGrid()
    {
        GD.Print("[GridManager] 重新构建网格...");
        BuildGrid();
    }

    /// <summary>直接从地图数据构建网格（分块加载时逻辑网格与视觉图层解耦）</summary>
    public void BuildGridFromMap(MapData map)
    {
        if (map == null)
        {
            GD.PushError("[GridManager] 地图数据为空，无法构建网格");
            return;
        }

        _gridSize = new Vector2I(map.Width, map.Height);
        _worldOrigin = Vector2.Zero;
        _walkableGrid = new bool[map.Width, map.Height];

        int walkableCount = 0;
        for (int x = 0; x < map.Width; x++)
        {
            for (int y = 0; y < map.Height; y++)
            {
                bool walkable = !map.IsWall(x, y);
                _walkableGrid[x, y] = walkable;
                if (walkable) walkableCount++;
            }
        }

        _isBuilt = true;
        GD.Print($"[GridManager] 网格构建完成（地图数据）— 尺寸:{_gridSize.X}x{_gridSize.Y}, 可行走:{walkableCount} 格");
        OutpostProtocol.Core.EventBus.EventBus.Instance
            .EmitLogMessage($"GridManager 网格构建完成: {walkableCount} 格可行走", "INFO");
    }

    // ============================================================
    // 坐标转换
    // ============================================================

    /// <summary>世界坐标 → 网格坐标</summary>
    public Vector2I WorldToGrid(Vector2 worldPos)
    {
        return new Vector2I(
            Mathf.FloorToInt((worldPos.X - _worldOrigin.X) / GridSize),
            Mathf.FloorToInt((worldPos.Y - _worldOrigin.Y) / GridSize)
        );
    }

    /// <summary>网格坐标 → 世界坐标（格子中心）</summary>
    public Vector2 GridToWorld(Vector2I gridPos)
    {
        return new Vector2(
            _worldOrigin.X + gridPos.X * GridSize + GridSize * 0.5f,
            _worldOrigin.Y + gridPos.Y * GridSize + GridSize * 0.5f
        );
    }

    /// <summary>网格坐标 → 世界坐标（格子中心，int 重载）</summary>
    public Vector2 GridToWorld(int x, int y)
    {
        return GridToWorld(new Vector2I(x, y));
    }

    // ============================================================
    // 查询 API
    // ============================================================

    /// <summary>检查指定网格位置是否可行走</summary>
    public bool IsWalkable(Vector2I gridPos)
    {
        if (!_isBuilt) return false;
        if (gridPos.X < 0 || gridPos.X >= _gridSize.X ||
            gridPos.Y < 0 || gridPos.Y >= _gridSize.Y)
        {
            return false;
        }
        return _walkableGrid[gridPos.X, gridPos.Y];
    }

    /// <summary>检查指定世界坐标是否可行走</summary>
    public bool IsWalkableWorld(Vector2 worldPos)
    {
        var gridPos = WorldToGrid(worldPos);
        return IsWalkable(gridPos);
    }

    /// <summary>设置指定网格位置的可行走状态（用于动态障碍物）</summary>
    public void SetWalkable(Vector2I gridPos, bool walkable)
    {
        if (!_isBuilt) return;
        if (gridPos.X < 0 || gridPos.X >= _gridSize.X ||
            gridPos.Y < 0 || gridPos.Y >= _gridSize.Y)
        {
            return;
        }
        _walkableGrid[gridPos.X, gridPos.Y] = walkable;
    }

    /// <summary>获取指定世界坐标所在的网格位置，并检查是否可行走</summary>
    public bool TryGetWalkableGrid(Vector2 worldPos, out Vector2I gridPos)
    {
        gridPos = WorldToGrid(worldPos);
        return IsWalkable(gridPos);
    }

    /// <summary>检查两点之间是否有视线（Bresenham 直线检测）</summary>
    public bool HasLineOfSight(Vector2 from, Vector2 to)
    {
        if (!_isBuilt) return false;

        Vector2I fromGrid = WorldToGrid(from);
        Vector2I toGrid = WorldToGrid(to);

        // 如果起点或终点不可行走，视为无视线
        if (!IsWalkable(fromGrid) || !IsWalkable(toGrid))
            return false;

        var linePoints = BresenhamLine(fromGrid, toGrid);
        foreach (var point in linePoints)
        {
            if (!IsWalkable(point))
                return false;
        }
        return true;
    }

    // ============================================================
    // 工具方法
    // ============================================================

    /// <summary>Bresenham 直线算法，返回直线经过的所有网格位置</summary>
    public static List<Vector2I> BresenhamLine(Vector2I from, Vector2I to)
    {
        var points = new List<Vector2I>();

        int x0 = from.X, y0 = from.Y;
        int x1 = to.X, y1 = to.Y;

        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);

        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;

        int err = dx - dy;

        while (true)
        {
            points.Add(new Vector2I(x0, y0));

            if (x0 == x1 && y0 == y1) break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }

        return points;
    }

    /// <summary>获取指定位置周围的可行走邻居（4 方向）</summary>
    public List<Vector2I> GetWalkableNeighbors(Vector2I gridPos)
    {
        var neighbors = new List<Vector2I>();
        var directions = new Vector2I[]
        {
            new(0, -1), new(0, 1),
            new(-1, 0), new(1, 0),
        };

        foreach (var dir in directions)
        {
            Vector2I neighbor = gridPos + dir;
            if (IsWalkable(neighbor))
            {
                neighbors.Add(neighbor);
            }
        }

        return neighbors;
    }

    /// <summary>获取指定位置周围的可行走邻居（8 方向，含对角线）</summary>
    public List<Vector2I> GetWalkableNeighbors8(Vector2I gridPos)
    {
        var neighbors = new List<Vector2I>();
        var directions = new Vector2I[]
        {
            new(0, -1), new(0, 1),
            new(-1, 0), new(1, 0),
            new(-1, -1), new(-1, 1),
            new(1, -1), new(1, 1),
        };

        foreach (var dir in directions)
        {
            Vector2I neighbor = gridPos + dir;
            if (IsWalkable(neighbor))
            {
                neighbors.Add(neighbor);
            }
        }

        return neighbors;
    }
}
