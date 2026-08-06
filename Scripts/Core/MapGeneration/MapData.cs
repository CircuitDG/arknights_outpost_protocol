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

/// <summary>建筑数据</summary>
public class BuildingData
{
    public Rect2I Bounds;
    public BuildingType Type;
    public Vector2I Entrance;
    public List<Rect2I> Rooms = new();
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
}
