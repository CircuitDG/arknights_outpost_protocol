using Godot;
using OutpostProtocol.Core.MapGeneration;
using System.Collections.Generic;

namespace OutpostProtocol.Core.Grid;

/// <summary>
/// 分块加载器（无缝大地图）
/// 职责：围绕跟随目标加载/卸载 TileMap 块，逻辑网格由 GridManager 单独持有
/// </summary>
public partial class ChunkLoader : Node
{
    [ExportGroup("分块配置")]
    [Export] public int ChunkSize = 16; // 每块格子数
    [Export] public int LoadRadius = 2; // 加载半径（块数），实际 (2R+1)² 块
    [Export] public bool AutoUnload = false; // 是否自动卸载远离的块（默认保留）

    [ExportGroup("跟随目标")]
    [Export] public Node2D FollowTarget; // 如博士

    private MapData _mapData;
    private TileSet _tileSet;
    private Node2D _chunkContainer;
    private readonly Dictionary<Vector2I, Node2D> _loadedChunks = new();
    private Vector2I _lastCenterChunk = new(int.MinValue, int.MinValue);

    public int ActiveChunkCount => _loadedChunks.Count;
    public Vector2I CenterChunk => _lastCenterChunk;

    /// <summary>初始化（地图数据 + 瓦片集 + 容器）</summary>
    public void Setup(MapData mapData, TileSet tileSet, Node2D container)
    {
        _mapData = mapData;
        _tileSet = tileSet;
        _chunkContainer = container;
    }

    public override void _Process(double delta)
    {
        if (FollowTarget == null || _mapData == null || _tileSet == null || _chunkContainer == null) return;

        Vector2I centerChunk = WorldToChunk(FollowTarget.GlobalPosition);
        if (centerChunk == _lastCenterChunk) return;

        _lastCenterChunk = centerChunk;
        UpdateChunks(centerChunk);
    }

    // ============================================================
    // 分块管理
    // ============================================================

    private void UpdateChunks(Vector2I centerChunk)
    {
        var neededChunks = new HashSet<Vector2I>();
        for (int dx = -LoadRadius; dx <= LoadRadius; dx++)
        {
            for (int dy = -LoadRadius; dy <= LoadRadius; dy++)
            {
                neededChunks.Add(new Vector2I(centerChunk.X + dx, centerChunk.Y + dy));
            }
        }

        foreach (var chunkPos in neededChunks)
        {
            if (!_loadedChunks.ContainsKey(chunkPos))
            {
                LoadChunk(chunkPos);
            }
        }

        if (AutoUnload)
        {
            var toRemove = new List<Vector2I>();
            foreach (var kvp in _loadedChunks)
            {
                if (!neededChunks.Contains(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var key in toRemove)
            {
                UnloadChunk(key);
            }
        }
    }

    private void LoadChunk(Vector2I chunkPos)
    {
        var chunk = new Node2D
        {
            Name = $"Chunk_{chunkPos.X}_{chunkPos.Y}",
            Position = new Vector2(chunkPos.X * ChunkSize * 16, chunkPos.Y * ChunkSize * 16),
        };

        // 图层层级：地面在最下，障碍在其上，角色(z=0)永远显示在瓦片之上
        var ground = new TileMapLayer { Name = "GroundLayer", TileSet = _tileSet, ZIndex = -2 };
        var obstacle = new TileMapLayer { Name = "ObstacleLayer", TileSet = _tileSet, ZIndex = -1 };
        chunk.AddChild(ground);
        chunk.AddChild(obstacle);

        for (int lx = 0; lx < ChunkSize; lx++)
        {
            for (int ly = 0; ly < ChunkSize; ly++)
            {
                int wx = chunkPos.X * ChunkSize + lx;
                int wy = chunkPos.Y * ChunkSize + ly;
                if (!_mapData.IsValidCell(wx, wy)) continue;

                ground.SetCell(new Vector2I(lx, ly), 0, new Vector2I(0, 0));
                if (_mapData.IsWall(wx, wy))
                {
                    obstacle.SetCell(new Vector2I(lx, ly), 0, new Vector2I(1, 0));
                }
            }
        }

        _chunkContainer.AddChild(chunk);
        _loadedChunks[chunkPos] = chunk;
        GD.Print($"[ChunkLoader] 加载块: {chunkPos}");
    }

    private void UnloadChunk(Vector2I chunkPos)
    {
        if (_loadedChunks.TryGetValue(chunkPos, out var node))
        {
            node.QueueFree();
            _loadedChunks.Remove(chunkPos);
            GD.Print($"[ChunkLoader] 卸载块: {chunkPos}");
        }
    }

    // ============================================================
    // 工具
    // ============================================================

    private Vector2I WorldToChunk(Vector2 worldPos)
    {
        int chunkPixelSize = ChunkSize * 16;
        return new Vector2I(
            Mathf.FloorToInt(worldPos.X / chunkPixelSize),
            Mathf.FloorToInt(worldPos.Y / chunkPixelSize)
        );
    }
}
