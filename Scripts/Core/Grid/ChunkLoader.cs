using Godot;
using System.Collections.Generic;

namespace OutpostProtocol.Core.Grid;

/// <summary>
/// 分块加载器（无缝大地图优化）
/// 职责：仅加载玩家周围的 TileMap 块，优化性能
/// </summary>
public partial class ChunkLoader : Node
{
    // ============================================================
    // 导出变量
    // ============================================================

    [ExportGroup("分块配置")]
    [Export] public TileMapLayer SourceTileMap; // 原始 TileMap
    [Export] public Node2D ChunkContainer; // 存放已加载块的容器
    [Export] public int ChunkSize = 16; // 每块大小（格子数）
    [Export] public int LoadRadius = 3; // 加载半径（块数）

    [ExportGroup("跟随目标")]
    [Export] public Node2D FollowTarget; // 跟随的目标（如博士）

    // ============================================================
    // 运行时状态
    // ============================================================

    private Dictionary<Vector2I, Node2D> _loadedChunks = new();
    private Vector2I _lastCenterChunk = new(int.MinValue, int.MinValue);

    // ============================================================
    // 生命周期
    // ============================================================

    public override void _Ready()
    {
        if (SourceTileMap == null)
        {
            GD.PushError("[ChunkLoader] 未设置 SourceTileMap");
            return;
        }

        if (ChunkContainer == null)
        {
            ChunkContainer = new Node2D { Name = "ChunkContainer" };
            AddChild(ChunkContainer);
        }
    }

    public override void _Process(double delta)
    {
        if (FollowTarget == null) return;

        Vector2 targetPos = FollowTarget.GlobalPosition;
        Vector2I centerChunk = WorldToChunk(targetPos);

        // 如果中心块未变化，跳过
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

        // 计算需要的块
        for (int dx = -LoadRadius; dx <= LoadRadius; dx++)
        {
            for (int dy = -LoadRadius; dy <= LoadRadius; dy++)
            {
                neededChunks.Add(new Vector2I(centerChunk.X + dx, centerChunk.Y + dy));
            }
        }

        // 加载新块
        foreach (var chunkPos in neededChunks)
        {
            if (!_loadedChunks.ContainsKey(chunkPos))
            {
                LoadChunk(chunkPos);
            }
        }

        // 卸载旧块
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

    private void LoadChunk(Vector2I chunkPos)
    {
        var chunkNode = new Node2D
        {
            Name = $"Chunk_{chunkPos.X}_{chunkPos.Y}",
            Position = new Vector2(
                chunkPos.X * ChunkSize * 16,
                chunkPos.Y * ChunkSize * 16
            ),
        };

        // TODO: 使用 SourceTileMap.GetCell() 逐格复制真实块数据

        ChunkContainer.AddChild(chunkNode);
        _loadedChunks[chunkPos] = chunkNode;
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
    // 工具方法
    // ============================================================

    private Vector2I WorldToChunk(Vector2 worldPos)
    {
        int tileSize = 16; // 假设每格 16 像素
        int chunkPixelSize = ChunkSize * tileSize;

        return new Vector2I(
            Mathf.FloorToInt(worldPos.X / chunkPixelSize),
            Mathf.FloorToInt(worldPos.Y / chunkPixelSize)
        );
    }
}
