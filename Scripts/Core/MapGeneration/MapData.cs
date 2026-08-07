using Godot;
using System.Collections.Generic;

namespace OutpostProtocol.Core.MapGeneration;

/// <summary>建筑类型</summary>
public enum BuildingType
{
    House, // 住宅
    Shop, // 商店
    Warehouse, // 仓库
    Office, // 办公楼
    Ruin, // 废墟
}

/// <summary>建筑损坏状态</summary>
public enum BuildingState
{
    Intact, // 完好
    Damaged, // 受损（墙壁仍在）
    Collapsed, // 坍塌（墙壁移除，变为可行走废墟）
}

/// <summary>地图瓦片图集坐标</summary>
public static class MapTiles
{
    public static readonly Vector2I Grass = new(0, 0);
    public static readonly Vector2I Street = new(3, 0);
    public static readonly Vector2I Wall = new(1, 0);
    public static readonly Vector2I Floor = new(4, 0);
}

/// <summary>房间类型</summary>
public enum RoomType
{
    Hall, // 走廊/门厅
    LivingRoom, // 客厅
    Bedroom, // 卧室
    Kitchen, // 厨房
    Storage, // 储藏室
    Workshop, // 仓库/车间
}

/// <summary>房间数据</summary>
public class RoomData
{
    public Rect2I Rect;
    public RoomType Type;
}

/// <summary>建筑数据</summary>
public class BuildingData
{
    public int Id;
    public Rect2I Bounds;
    public BuildingType Type;
    public BuildingState State = BuildingState.Intact;
    public Vector2I Entrance;
    public List<RoomData> Rooms = new();
}

/// <summary>建筑内资源点</summary>
public class ResourcePointData
{
    public Vector2I Position;
    public int ItemId;
    public int Amount;
}

/// <summary>生成的地图数据（内存模型）</summary>
public class MapData
{
    public int Width;
    public int Height;
    public long Seed;

    /// <summary>地面层：0=草地/空地, 1=街道</summary>
    public int[,] Ground;

    /// <summary>建筑层：0=无, 1=墙壁/障碍, 2=室内地板</summary>
    public int[,] Building;

    public List<BuildingData> Buildings = new();
    public List<ResourcePointData> ResourcePoints = new();
    public List<Vector2I> StreetCells = new();
    public Vector2I OutpostCell;
    public List<Vector2I> BeaconCells = new();

    public bool IsValidCell(int x, int y)
    {
        return x >= 0 && x < Width && y >= 0 && y < Height;
    }

    public bool IsWall(int x, int y)
    {
        return IsValidCell(x, y) && Building[x, y] == 1;
    }

    public bool IsFloor(int x, int y)
    {
        return IsValidCell(x, y) && Building[x, y] == 2;
    }

    public bool IsStreet(int x, int y)
    {
        return IsValidCell(x, y) && Ground[x, y] == 1;
    }

    public bool IsWalkableCell(int x, int y)
    {
        return IsValidCell(x, y) && !IsWall(x, y);
    }

    /// <summary>该格是否阻挡移动（坍塌建筑的墙不再阻挡）</summary>
    public bool IsBlocked(int x, int y)
    {
        if (!IsWall(x, y)) return false;
        var building = GetBuildingAt(x, y);
        return building == null || building.State != BuildingState.Collapsed;
    }

    /// <summary>获取包含该格的建筑（建筑不重叠，返回唯一归属）</summary>
    public BuildingData GetBuildingAt(int x, int y)
    {
        var cell = new Vector2I(x, y);
        foreach (var building in Buildings)
        {
            if (building.Bounds.HasPoint(cell))
            {
                return building;
            }
        }
        return null;
    }
}
