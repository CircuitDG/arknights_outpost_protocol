using Godot;
using OutpostProtocol.Core.Grid;
using OutpostProtocol.Core.MapGeneration;
using OutpostProtocol.Gameplay.Character.Doctor;
using OutpostProtocol.Managers;

/// <summary>
/// 程序化城市地图测试：
/// 尺寸/建筑/资源点统计 → 网格构建 → 出生点可行走 → A* 到达前哨站 → 资源节点生成
/// </summary>
public partial class TestCityMapController : Node
{
    private int _frameCount;

    public override void _Ready()
    {
        GD.Print("========== 程序化城市地图测试 ==========");
    }

    public override void _Process(double delta)
    {
        _frameCount++;

        if (_frameCount == 80)
        {
            var gw = GetNode<GameWorldController>("../Main");
            var grid = GridManager.Instance;
            var doctor = GetNode<Doctor>("../Main/World/Doctor");

            GD.Print($"[TestCityMap] 地图: {gw.MapData.Width}x{gw.MapData.Height}, 建筑:{gw.BuildingCount}, 资源点:{gw.ResourcePointCount}, 街道格:{gw.MapData.StreetCells.Count}");
            GD.Print($"[TestCityMap] 网格: {grid.GridDimensions}, 可行走格: {CountWalkable(grid)}");
            GD.Print($"[TestCityMap] 分块: 已加载={gw.ChunkLoader?.ActiveChunkCount}, 中心={gw.ChunkLoader?.CenterChunk}");

            Vector2 start = doctor.GlobalPosition;
            Vector2 end = grid.GridToWorld(gw.MapData.OutpostCell);
            bool startOk = grid.IsWalkableWorld(start);
            bool endOk = grid.IsWalkableWorld(end);
            var path = AStarPathfinder.FindPath(start, end, grid);

            GD.Print($"[TestCityMap] 出生点可行走:{startOk}, 前哨站可行走:{endOk}, A*路径点数:{path.Count}");

            int resourceNodes = GetTree().GetNodesInGroup("gatherable_resources").Count;
            GD.Print($"[TestCityMap] 已加载资源节点: {resourceNodes}（按块实例化，全图 {gw.ResourcePointCount}）");
        }
        else if (_frameCount == 82)
        {
            // 传送到远处街道，验证分块卸载/加载
            var gw = GetNode<GameWorldController>("../Main");
            Vector2I farCell = Vector2I.Zero;
            foreach (var street in gw.MapData.StreetCells)
            {
                if (street.X > 250 && street.Y > 250)
                {
                    farCell = street;
                    break;
                }
            }
            var doctor = GetNode<Doctor>("../Main/World/Doctor");
            doctor.GlobalPosition = GridManager.Instance.GridToWorld(farCell);
            GD.Print($"[TestCityMap] 传送到街道格 {farCell}");
        }
        else if (_frameCount == 100)
        {
            var gw = GetNode<GameWorldController>("../Main");
            GD.Print($"[TestCityMap] 传送后: 中心={gw.ChunkLoader?.CenterChunk}, 已加载={gw.ChunkLoader?.ActiveChunkCount}, 资源节点={GetTree().GetNodesInGroup("gatherable_resources").Count}");
        }
        else if (_frameCount >= 110)
        {
            GD.Print("[TestCityMap] 测试完成");
            GetTree().Quit();
        }
    }

    private static int CountWalkable(GridManager grid)
    {
        int count = 0;
        var dims = grid.GridDimensions;
        for (int x = 0; x < dims.X; x++)
        {
            for (int y = 0; y < dims.Y; y++)
            {
                if (grid.IsWalkable(new Vector2I(x, y))) count++;
            }
        }
        return count;
    }
}
