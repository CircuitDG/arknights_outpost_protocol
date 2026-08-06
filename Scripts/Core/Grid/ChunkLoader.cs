using Godot;
using OutpostProtocol.Core.MapGeneration;
using OutpostProtocol.Gameplay.Inventory;
using System;
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
    private PackedScene _resourceScene;
    private Func<Vector2I, bool> _isResourceCollected;
    private readonly Dictionary<Vector2I, Node2D> _loadedChunks = new();
    private Vector2I _lastCenterChunk = new(int.MinValue, int.MinValue);

    public int ActiveChunkCount => _loadedChunks.Count;
    public Vector2I CenterChunk => _lastCenterChunk;

    /// <summary>初始化（地图数据 + 瓦片集 + 容器）</summary>
    public void Setup(MapData mapData, TileSet tileSet, Node2D container,
        PackedScene resourceScene = null, Func<Vector2I, bool> isResourceCollected = null)
    {
        _mapData = mapData;
        _tileSet = tileSet;
        _chunkContainer = container;
        _resourceScene = resourceScene;
        _isResourceCollected = isResourceCollected;
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

        // 先挂进场景树（子节点 _Ready 立即执行），再生成资源，
        // 保证 RestoreCollected 在 _Ready 之后生效，不会被 _Ready 重置。
        _chunkContainer.AddChild(chunk);
        _loadedChunks[chunkPos] = chunk;

        for (int lx = 0; lx < ChunkSize; lx++)
        {
            for (int ly = 0; ly < ChunkSize; ly++)
            {
                int wx = chunkPos.X * ChunkSize + lx;
                int wy = chunkPos.Y * ChunkSize + ly;
                if (!_mapData.IsValidCell(wx, wy)) continue;

                Vector2I groundTile = _mapData.Ground[wx, wy] == 1
                    ? MapTiles.Street
                    : _mapData.Building[wx, wy] == 2
                        ? MapTiles.Floor
                        : MapTiles.Grass;
                ground.SetCell(new Vector2I(lx, ly), 0, groundTile);
                if (_mapData.IsBlocked(wx, wy))
                {
                    obstacle.SetCell(new Vector2I(lx, ly), 0, MapTiles.Wall);
                }
            }
        }

        // 该块内的资源点随块实例化
        if (_resourceScene != null)
        {
            foreach (var point in _mapData.ResourcePoints)
            {
                if (point.Position.X / ChunkSize != chunkPos.X ||
                    point.Position.Y / ChunkSize != chunkPos.Y)
                {
                    continue;
                }

                var node = _resourceScene.Instantiate<GatherableResource>();
                if (node == null) continue;

                node.ItemId = point.ItemId;
                node.AmountPerGather = point.Amount;
                node.MaxAmount = point.Amount;
                node.EnableRespawn = false; // 建筑内搜索点一次性
                node.MapCell = point.Position;

                chunk.AddChild(node);
                node.GlobalPosition = new Vector2(
                    (point.Position.X + 0.5f) * 16f,
                    (point.Position.Y + 0.5f) * 16f
                );

                if (_isResourceCollected?.Invoke(point.Position) == true)
                {
                    node.RestoreCollected();
                }
            }
        }
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

    /// <summary>重建所有已加载块（建筑状态变化后刷新视觉）</summary>
    public void RebuildAll()
    {
        foreach (var node in _loadedChunks.Values)
        {
            node.QueueFree();
        }
        _loadedChunks.Clear();
        _lastCenterChunk = new Vector2I(int.MinValue, int.MinValue); // 下一帧按当前中心重新加载
    }
}
