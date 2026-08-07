using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OutpostProtocol.Core.Grid;

/// <summary>
/// A* 寻路算法（静态工具类）
/// 支持：4方向/8方向移动、路径平滑、异步计算
/// </summary>
public static class AStarPathfinder
{
    /// <summary>寻路配置</summary>
    public class PathConfig
    {
        public bool AllowDiagonal = true; // 是否允许对角线移动
        public bool SmoothPath = true; // 是否启用路径平滑
        public int MaxIterations = 10000; // 最大迭代次数（防止死循环）
    }

    /// <summary>同步查找路径</summary>
    public static List<Vector2> FindPath(
        Vector2 startWorld,
        Vector2 endWorld,
        GridManager grid,
        PathConfig config = null)
    {
        if (grid == null || !grid.IsBuilt)
            return new List<Vector2>();

        config ??= new PathConfig();

        // 转换到网格坐标
        Vector2I start = grid.WorldToGrid(startWorld);
        Vector2I end = grid.WorldToGrid(endWorld);

        // 起点/终点不可行走时吸附到最近可行走格，避免单位被软排斥推上墙后卡死
        start = FindNearestWalkable(grid, start);
        end = FindNearestWalkable(grid, end);
        if (start.X < 0 || end.X < 0)
            return new List<Vector2>();

        // 如果起点等于终点，直接返回
        if (start == end)
            return new List<Vector2> { grid.GridToWorld(end) };

        // A* 算法
        var openSet = new PriorityQueue<Vector2I, float>();
        var cameFrom = new Dictionary<Vector2I, Vector2I>();
        var gScore = new Dictionary<Vector2I, float>();
        var fScore = new Dictionary<Vector2I, float>();

        openSet.Enqueue(start, 0);
        gScore[start] = 0;
        fScore[start] = Heuristic(start, end);

        int iterations = 0;

        while (openSet.Count > 0 && iterations < config.MaxIterations)
        {
            iterations++;
            Vector2I current = openSet.Dequeue();

            if (current == end)
            {
                var path = ReconstructPath(cameFrom, current, start);
                return config.SmoothPath
                    ? SmoothPath(path, grid)
                    : path;
            }

            // 获取邻居
            var neighbors = config.AllowDiagonal
                ? grid.GetWalkableNeighbors8(current)
                : grid.GetWalkableNeighbors(current);

            foreach (var neighbor in neighbors)
            {
                // 计算移动代价（对角线代价为 1.414）
                int dx = Math.Abs(neighbor.X - current.X);
                int dy = Math.Abs(neighbor.Y - current.Y);
                float moveCost = (dx == 1 && dy == 1) ? 1.414f : 1.0f;

                float tentativeG = gScore[current] + moveCost;

                if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    fScore[neighbor] = tentativeG + Heuristic(neighbor, end);
                    openSet.Enqueue(neighbor, fScore[neighbor]);
                }
            }
        }

        // 无路径
        return new List<Vector2>();
    }

    /// <summary>异步查找路径（推荐用于大量单位）</summary>
    public static async Task<List<Vector2>> FindPathAsync(
        Vector2 startWorld,
        Vector2 endWorld,
        GridManager grid,
        PathConfig config = null)
    {
        return await Task.Run(() => FindPath(startWorld, endWorld, grid, config));
    }

    // ============================================================
    // 辅助方法
    // ============================================================

    /// <summary>Octile 距离启发式（允许对角线时更准确）</summary>
    private static float Heuristic(Vector2I a, Vector2I b)
    {
        int dx = Math.Abs(a.X - b.X);
        int dy = Math.Abs(a.Y - b.Y);
        return Math.Max(dx, dy) + 0.414f * Math.Min(dx, dy);
    }

    /// <summary>查找距离指定格最近的可行走格（BFS，半径 8 格）</summary>
    private static Vector2I FindNearestWalkable(GridManager grid, Vector2I cell, int maxRadius = 8)
    {
        if (grid.IsWalkable(cell)) return cell;

        var dims = grid.GridDimensions;
        var visited = new HashSet<Vector2I>();
        var queue = new Queue<Vector2I>();
        queue.Enqueue(cell);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current)) continue;

            int dist = Math.Max(Math.Abs(current.X - cell.X), Math.Abs(current.Y - cell.Y));
            if (dist > maxRadius) continue;

            foreach (var offset in new[]
            {
                new Vector2I(1, 0), new Vector2I(-1, 0),
                new Vector2I(0, 1), new Vector2I(0, -1),
            })
            {
                var next = new Vector2I(current.X + offset.X, current.Y + offset.Y);
                if (next.X < 0 || next.Y < 0 || next.X >= dims.X || next.Y >= dims.Y) continue;
                if (grid.IsWalkable(next))
                {
                    return next;
                }
                if (!visited.Contains(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return new Vector2I(-1, -1);
    }

    /// <summary>重建路径（从终点回溯到起点）</summary>
    private static List<Vector2> ReconstructPath(
        Dictionary<Vector2I, Vector2I> cameFrom,
        Vector2I current,
        Vector2I start)
    {
        var path = new List<Vector2>();
        var grid = GridManager.Instance;

        while (current != start)
        {
            path.Add(grid.GridToWorld(current));
            current = cameFrom[current];
        }
        path.Add(grid.GridToWorld(start));

        // 反转路径（从起点到终点）
        path.Reverse();
        return path;
    }

    /// <summary>路径平滑（视线剪枝 / String Pulling），移除冗余拐点</summary>
    private static List<Vector2> SmoothPath(List<Vector2> path, GridManager grid)
    {
        if (path.Count <= 2) return path;

        var smoothed = new List<Vector2> { path[0] };
        int currentIndex = 0;

        while (currentIndex < path.Count - 1)
        {
            int furthestVisible = currentIndex + 1;

            for (int i = currentIndex + 2; i < path.Count; i++)
            {
                if (grid.HasLineOfSight(path[currentIndex], path[i]))
                {
                    furthestVisible = i;
                }
                else
                {
                    break;
                }
            }

            smoothed.Add(path[furthestVisible]);
            currentIndex = furthestVisible;
        }

        return smoothed;
    }
}
