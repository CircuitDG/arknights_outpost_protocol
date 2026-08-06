using Godot;
using System;
using System.Collections.Generic;

namespace OutpostProtocol.Core.MapGeneration;

/// <summary>
/// 程序化城市废墟地图生成器
/// 街道网格 → 街区 → 建筑（含内部房间/入口）→ 资源点 → 装饰 → 特殊地点
/// </summary>
public class MapGenerator
{
    private readonly MapConfig _config;
    private readonly Random _rng;

    public MapGenerator(MapConfig config)
    {
        _config = config;
        _rng = new Random((int)config.Seed);
    }

    public MapData Generate()
    {
        var map = new MapData
        {
            Width = _config.Width,
            Height = _config.Height,
            Seed = _config.Seed,
            Ground = new int[_config.Width, _config.Height],
            Building = new int[_config.Width, _config.Height],
        };

        GenerateStreets(map);
        var blocks = ComputeBlocks(map);
        GenerateBuildings(map, blocks);
        GenerateInteriors(map);
        GenerateResources(map);
        GenerateDecorations(map);
        MarkSpecialLocations(map);

        return map;
    }

    // ============================================================
    // 街道网格
    // ============================================================

    private void GenerateStreets(MapData map)
    {
        var xBands = GetStreetBands(map.Width);
        var yBands = GetStreetBands(map.Height);

        foreach (var band in xBands)
        {
            for (int x = band.Item1; x <= band.Item2; x++)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    map.Ground[x, y] = 1;
                }
            }
        }

        foreach (var band in yBands)
        {
            for (int y = band.Item1; y <= band.Item2; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    map.Ground[x, y] = 1;
                }
            }
        }

        for (int x = 0; x < map.Width; x++)
        {
            for (int y = 0; y < map.Height; y++)
            {
                if (map.Ground[x, y] == 1)
                {
                    map.StreetCells.Add(new Vector2I(x, y));
                }
            }
        }
    }

    /// <summary>生成街道条带（主/次宽度交替，间距固定）</summary>
    private List<(int, int)> GetStreetBands(int length)
    {
        var bands = new List<(int, int)>();
        int index = 0;
        int start = 0;
        while (start < length)
        {
            int width = index % 2 == 0 ? _config.MainStreetWidth : _config.SideStreetWidth;
            int end = Math.Min(length - 1, start + width - 1);
            bands.Add((start, end));
            index++;
            start += _config.MainStreetSpacing;
        }
        return bands;
    }

    /// <summary>街道条带之间的街区矩形</summary>
    private List<Rect2I> ComputeBlocks(MapData map)
    {
        var xBands = GetStreetBands(map.Width);
        var yBands = GetStreetBands(map.Height);
        var blocks = new List<Rect2I>();

        for (int xi = 0; xi < xBands.Count - 1; xi++)
        {
            int xStart = xBands[xi].Item2 + 1;
            int xEnd = xBands[xi + 1].Item1 - 1;
            if (xEnd < xStart) continue;

            for (int yi = 0; yi < yBands.Count - 1; yi++)
            {
                int yStart = yBands[yi].Item2 + 1;
                int yEnd = yBands[yi + 1].Item1 - 1;
                if (yEnd < yStart) continue;

                blocks.Add(new Rect2I(xStart, yStart, xEnd - xStart + 1, yEnd - yStart + 1));
            }
        }

        return blocks;
    }

    // ============================================================
    // 建筑
    // ============================================================

    private void GenerateBuildings(MapData map, List<Rect2I> blocks)
    {
        foreach (var block in blocks)
        {
            int margin = 2;
            var usable = new Rect2I(
                block.Position.X + margin,
                block.Position.Y + margin,
                Math.Max(0, block.Size.X - margin * 2),
                Math.Max(0, block.Size.Y - margin * 2)
            );
            if (usable.Size.X < _config.MinBuildingSize || usable.Size.Y < _config.MinBuildingSize) continue;

            int attempts = 0;
            int filledArea = 0;
            int blockArea = usable.Size.X * usable.Size.Y;

            while (attempts < _config.MaxBuildAttempts && filledArea < blockArea * _config.BlockFillRate)
            {
                attempts++;

                int width = _rng.Next(_config.MinBuildingSize, Math.Min(_config.MaxBuildingSize, usable.Size.X) + 1);
                int height = _rng.Next(_config.MinBuildingSize, Math.Min(_config.MaxBuildingSize, usable.Size.Y) + 1);
                int posX = _rng.Next(usable.Position.X, usable.Position.X + usable.Size.X - width + 1);
                int posY = _rng.Next(usable.Position.Y, usable.Position.Y + usable.Size.Y - height + 1);

                var rect = new Rect2I(posX, posY, width, height);
                if (OverlapsAnyBuilding(map, rect)) continue;

                var building = CarveBuilding(map, rect);
                map.Buildings.Add(building);
                filledArea += width * height;
            }
        }
    }

    private bool OverlapsAnyBuilding(MapData map, Rect2I rect)
    {
        // 允许 1 格间隙
        var expanded = new Rect2I(rect.Position.X - 1, rect.Position.Y - 1, rect.Size.X + 2, rect.Size.Y + 2);
        foreach (var other in map.Buildings)
        {
            if (expanded.Intersects(other.Bounds)) return true;
        }
        return false;
    }

    private BuildingData CarveBuilding(MapData map, Rect2I rect)
    {
        // 地板
        for (int x = rect.Position.X; x < rect.Position.X + rect.Size.X; x++)
        {
            for (int y = rect.Position.Y; y < rect.Position.Y + rect.Size.Y; y++)
            {
                map.Building[x, y] = 2;
            }
        }

        // 外墙
        for (int x = rect.Position.X; x < rect.Position.X + rect.Size.X; x++)
        {
            map.Building[x, rect.Position.Y] = 1;
            map.Building[x, rect.Position.Y + rect.Size.Y - 1] = 1;
        }
        for (int y = rect.Position.Y; y < rect.Position.Y + rect.Size.Y; y++)
        {
            map.Building[rect.Position.X, y] = 1;
            map.Building[rect.Position.X + rect.Size.X - 1, y] = 1;
        }

        var type = RollBuildingType();
        var building = new BuildingData
        {
            Bounds = rect,
            Type = type,
        };

        // 入口：选择距离街道最近的一侧，开一个缺口
        building.Entrance = CreateEntrance(map, rect);
        return building;
    }

    private BuildingType RollBuildingType()
    {
        float roll = (float)_rng.NextDouble();
        if (roll < 0.40f) return BuildingType.House;
        if (roll < 0.60f) return BuildingType.Shop;
        if (roll < 0.75f) return BuildingType.Warehouse;
        if (roll < 0.85f) return BuildingType.Office;
        return BuildingType.Ruin;
    }

    private Vector2I CreateEntrance(MapData map, Rect2I rect)
    {
        // 计算四边到最近街道的距离，选最近的边
        int left = MinDistToStreet(map, rect.Position.X - 1, rect.Position.Y, rect.Size.Y, false);
        int right = MinDistToStreet(map, rect.Position.X + rect.Size.X, rect.Position.Y, rect.Size.Y, false);
        int top = MinDistToStreet(map, rect.Position.X, rect.Position.Y - 1, rect.Size.X, true);
        int bottom = MinDistToStreet(map, rect.Position.X, rect.Position.Y + rect.Size.Y, rect.Size.X, true);

        Vector2I entrance;
        int minDist = Math.Min(Math.Min(left, right), Math.Min(top, bottom));

        if (minDist == left)
        {
            entrance = new Vector2I(rect.Position.X, rect.Position.Y + _rng.Next(1, rect.Size.Y - 1));
        }
        else if (minDist == right)
        {
            entrance = new Vector2I(rect.Position.X + rect.Size.X - 1, rect.Position.Y + _rng.Next(1, rect.Size.Y - 1));
        }
        else if (minDist == top)
        {
            entrance = new Vector2I(rect.Position.X + _rng.Next(1, rect.Size.X - 1), rect.Position.Y);
        }
        else
        {
            entrance = new Vector2I(rect.Position.X + _rng.Next(1, rect.Size.X - 1), rect.Position.Y + rect.Size.Y - 1);
        }

        map.Building[entrance.X, entrance.Y] = 0; // 开门
        return entrance;
    }

    private int MinDistToStreet(MapData map, int startX, int startY, int length, bool horizontal)
    {
        int min = int.MaxValue;
        for (int i = 0; i < length; i++)
        {
            int x = horizontal ? startX + i : startX;
            int y = horizontal ? startY : startY + i;
            if (!map.IsValidCell(x, y)) continue;

            int dist = 0;
            if (horizontal)
            {
                for (int d = 0; d < 30; d++)
                {
                    if (map.IsValidCell(x, y - d) && map.Ground[x, y - d] == 1) { dist = d; break; }
                    if (map.IsValidCell(x, y + d) && map.Ground[x, y + d] == 1) { dist = d; break; }
                }
            }
            else
            {
                for (int d = 0; d < 30; d++)
                {
                    if (map.IsValidCell(x - d, y) && map.Ground[x - d, y] == 1) { dist = d; break; }
                    if (map.IsValidCell(x + d, y) && map.Ground[x + d, y] == 1) { dist = d; break; }
                }
            }
            min = Math.Min(min, dist);
        }
        return min;
    }

    // ============================================================
    // 建筑内部（简化 BSP：内部隔墙 + 门）
    // ============================================================

    private void GenerateInteriors(MapData map)
    {
        foreach (var building in map.Buildings)
        {
            var rect = building.Bounds;
            building.Rooms.Add(rect);

            // 大建筑：垂直/水平隔墙，中间留门
            if (rect.Size.X > 6 && rect.Size.Y > 4)
            {
                int wallX = rect.Position.X + rect.Size.X / 2;
                if (wallX != building.Entrance.X)
                {
                    int doorY = rect.Position.Y + _rng.Next(1, rect.Size.Y - 1);
                    for (int y = rect.Position.Y; y < rect.Position.Y + rect.Size.Y; y++)
                    {
                        if (y != doorY)
                        {
                            map.Building[wallX, y] = 1;
                        }
                    }
                    building.Rooms.Add(new Rect2I(rect.Position.X, rect.Position.Y, wallX - rect.Position.X, rect.Size.Y));
                    building.Rooms.Add(new Rect2I(wallX + 1, rect.Position.Y, rect.Position.X + rect.Size.X - wallX - 1, rect.Size.Y));
                }
            }

            if (rect.Size.Y > 6 && rect.Size.X > 4)
            {
                int wallY = rect.Position.Y + rect.Size.Y / 2;
                if (wallY != building.Entrance.Y)
                {
                    int doorX = rect.Position.X + _rng.Next(1, rect.Size.X - 1);
                    for (int x = rect.Position.X; x < rect.Position.X + rect.Size.X; x++)
                    {
                        if (x != doorX)
                        {
                            map.Building[x, wallY] = 1;
                        }
                    }
                }
            }
        }
    }

    // ============================================================
    // 资源点
    // ============================================================

    private void GenerateResources(MapData map)
    {
        foreach (var building in map.Buildings)
        {
            int count = building.Type switch
            {
                BuildingType.House => _rng.Next(2, 4),
                BuildingType.Shop => _rng.Next(3, 5),
                BuildingType.Warehouse => _rng.Next(4, 6),
                BuildingType.Office => _rng.Next(3, 5),
                _ => _rng.Next(1, 3),
            };
            count = Math.Min(count, _config.MaxResourcesPerBuilding);

            for (int i = 0; i < count; i++)
            {
                var cell = FindRandomFloorCell(map, building.Bounds, building.Entrance);
                if (cell.X < 0) break;

                var point = new ResourcePointData
                {
                    Position = cell,
                    ItemId = RollResourceItem(building.Type),
                    Amount = _rng.Next(2, 6),
                };
                map.ResourcePoints.Add(point);
            }
        }
    }

    private Vector2I FindRandomFloorCell(MapData map, Rect2I rect, Vector2I entrance)
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            int x = _rng.Next(rect.Position.X, rect.Position.X + rect.Size.X);
            int y = _rng.Next(rect.Position.Y, rect.Position.Y + rect.Size.Y);
            if (map.Building[x, y] == 2 && new Vector2I(x, y) != entrance)
            {
                return new Vector2I(x, y);
            }
        }
        return new Vector2I(-1, -1);
    }

    private int RollResourceItem(BuildingType type)
    {
        float roll = (float)_rng.NextDouble();
        return type switch
        {
            BuildingType.House => roll < 0.5f ? 3 : roll < 0.8f ? 1 : 4, // 食物/木材/绷带
            BuildingType.Shop => roll < 0.6f ? 3 : 2, // 食物/铁皮
            BuildingType.Warehouse => roll < 0.6f ? 2 : 1, // 铁皮/木材
            BuildingType.Office => roll < 0.7f ? 2 : 5, // 铁皮/源石
            _ => roll < 0.7f ? 1 : 2, // 废墟：木材/铁皮
        };
    }

    // ============================================================
    // 装饰（树木作为障碍）
    // ============================================================

    private void GenerateDecorations(MapData map)
    {
        int target = (int)(map.Width * map.Height * _config.TreeDensity);
        int placed = 0;
        int attempts = 0;

        while (placed < target && attempts < target * 10)
        {
            attempts++;
            int x = _rng.Next(0, map.Width);
            int y = _rng.Next(0, map.Height);
            if (map.Building[x, y] != 0 || map.Ground[x, y] != 0) continue;
            if (NearAnyEntrance(map, new Vector2I(x, y))) continue;

            map.Building[x, y] = 1; // 树木阻挡
            placed++;
        }
    }

    private bool NearAnyEntrance(MapData map, Vector2I cell)
    {
        foreach (var building in map.Buildings)
        {
            if (Math.Abs(cell.X - building.Entrance.X) <= 1 && Math.Abs(cell.Y - building.Entrance.Y) <= 1)
            {
                return true;
            }
        }
        return false;
    }

    // ============================================================
    // 特殊地点
    // ============================================================

    private void MarkSpecialLocations(MapData map)
    {
        if (map.Buildings.Count == 0) return;

        // 前哨站：最接近地图中心的建筑
        Vector2 center = new(map.Width / 2f, map.Height / 2f);
        BuildingData nearest = null;
        float minDist = float.MaxValue;
        foreach (var building in map.Buildings)
        {
            var c = building.Bounds.GetCenter();
            float dist = Math.Abs(c.X - center.X) + Math.Abs(c.Y - center.Y);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = building;
            }
        }

        if (nearest != null)
        {
            var outpostCenter = nearest.Bounds.GetCenter();
            map.OutpostCell = new Vector2I((int)outpostCenter.X, (int)outpostCenter.Y);
        }

        // 求救信标：随机挑选 3 个非前哨站建筑
        var candidates = new List<BuildingData>(map.Buildings);
        candidates.Remove(nearest);
        for (int i = 0; i < 3 && candidates.Count > 0; i++)
        {
            int idx = _rng.Next(0, candidates.Count);
            var beaconCenter = candidates[idx].Bounds.GetCenter();
            map.BeaconCells.Add(new Vector2I((int)beaconCenter.X, (int)beaconCenter.Y));
            candidates.RemoveAt(idx);
        }
    }
}
