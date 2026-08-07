using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Core.Grid;
using OutpostProtocol.Data;
using OutpostProtocol.Core.MapGeneration;
using OutpostProtocol.Gameplay.Character.Doctor;
using OutpostProtocol.Gameplay.Character.Operator;
using OutpostProtocol.Gameplay.Inventory;
using OutpostProtocol.Managers;

/// <summary>
/// 继续游戏存档恢复测试：
/// 预置一份对局存档（博士/干员/塔/天数）→ 主世界加载后自动恢复
/// </summary>
public partial class TestContinueController : Node
{
    private int _frameCount;
    private Vector2I _savedResourcePos = new(-1, -1);

    public override void _Ready()
    {
        // 构造对局存档
        var sm = SaveManager.Instance;
        sm.NewRun();
        var run = sm.CurrentRun;

        run.DayCount = 3;
        run.CurrentPhase = (int)DayPhase.Morning;
        run.DoctorPosX = 200;
        run.DoctorPosY = 150;
        run.DoctorHealth = 70;
        run.DoctorStamina = 50;
        run.Operators.Add(new OperatorRuntime
        {
            OperatorId = 1001,
            CurrentLevel = 2,
            CurrentExp = 50,
            CurrentHealth = 80,
            MaxHealth = 120,
            Morale = 100,
            PosX = 180,
            PosY = 80,
            IsFollowing = false,
        });
        run.Towers.Add(new TowerRuntime
        {
            TowerId = 1,
            CurrentLevel = 2,
            PosX = 160,
            PosY = 160,
            CurrentDurability = 70,
        });

        // 预置一个"存档恢复点附近"的资源点为已搜索（该块初始必加载，能验证隐藏）
        var gw = GetNode<GameWorldController>("../Main");
        if (gw.MapData?.ResourcePoints.Count > 0)
        {
            // 博士恢复到世界 (200,150) → 网格格 (12,9)
            Vector2I restoreCell = new((int)(run.DoctorPosX / 16), (int)(run.DoctorPosY / 16));

            ResourcePointData target = gw.MapData.ResourcePoints[0];
            int bestRes = int.MaxValue;
            foreach (var point in gw.MapData.ResourcePoints)
            {
                int d = Mathf.Abs(point.Position.X - restoreCell.X) + Mathf.Abs(point.Position.Y - restoreCell.Y);
                if (d < bestRes)
                {
                    bestRes = d;
                    target = point;
                }
            }

            run.ResourceStates.Add(new ResourceState
            {
                GridX = target.Position.X,
                GridY = target.Position.Y,
                Collected = true,
            });
            _savedResourcePos = target.Position;
        }

        // 预置 1 号建筑为坍塌状态
        if (gw.MapData?.Buildings.Count > 0)
        {
            run.BuildingStates.Add(new BuildingStateRecord
            {
                BuildingId = 0,
                State = (int)BuildingState.Collapsed,
            });
        }

        sm.SaveRun();
        sm.RestoreOnGameLoad = true;
        GD.Print("[TestContinue] 存档已预置 — Day 3, 博士(200,150) HP70, 干员Lv2, 塔Lv2");
    }

    public override void _Process(double delta)
    {
        _frameCount++;

        if (_frameCount == 80)
        {
            var doctor = GetNode<Doctor>("../Main/World/Doctor");
            var op = GetNode<Operator>("../Main/World/Operator_1");
            var container = GetNode<Node2D>("../Main/TowerContainer");
            var gm = GameManager.Instance;

            GD.Print($"[TestContinue] 博士: 位置=({doctor.GlobalPosition.X:F0},{doctor.GlobalPosition.Y:F0}), HP={doctor.CurrentHealth}, 体力={doctor.CurrentStamina}");
            GD.Print($"[TestContinue] 干员: Lv={op.CurrentLevel}, HP={op.Health?.CurrentHealth}");
            GD.Print($"[TestContinue] 塔: 数量={container.GetChildCount()}, 等级={(container.GetChildCount() > 0 ? (int)container.GetChild(0).Get("CurrentLevel") : 0)}, 耐久={(container.GetChildCount() > 0 ? (int)container.GetChild(0).Get("CurrentDurability") : 0)}");
            GD.Print($"[TestContinue] 游戏状态: Day={gm.DayCount}, State={gm.CurrentState}, Phase={gm.CurrentPhase}");

            // 已搜索资源点应保持隐藏
            var gw = GetNode<GameWorldController>("../Main");
            if (gw.MapData?.ResourcePoints.Count > 0)
            {
                var p = _savedResourcePos;
                bool nodeHidden = false;
                int matchCount = 0;
                foreach (var node in GetTree().GetNodesInGroup("gatherable_resources"))
                {
                    if (node is GatherableResource resource &&
                        resource.MapCell.X == p.X &&
                        resource.MapCell.Y == p.Y)
                    {
                        matchCount++;
                        GD.Print($"[TestContinue] 节点状态: Visible={resource.Visible}, IsCollected={resource.IsCollected}, Remaining={resource.Remaining}, InTree={resource.IsInsideTree()}, InstanceId={resource.GetInstanceId()}");
                        nodeHidden = !resource.Visible;
                    }
                }
                GD.Print($"[TestContinue] 已搜索资源点({p}): 存档标记={gw.IsResourceCollected(p)}, 匹配节点={matchCount}, 节点隐藏={nodeHidden}, 块中心={gw.ChunkLoader?.CenterChunk}");
            }

            // 建筑状态：0 号建筑应坍塌，墙体变为可行走
            if (gw.MapData?.Buildings.Count > 0)
            {
                var building = gw.MapData.Buildings[0];
                bool collapsed = building.State == BuildingState.Collapsed;
                Vector2I wallCell = FindFirstWallCell(gw.MapData, building);
                bool wallWalkable = wallCell.X >= 0 && GridManager.Instance.IsWalkableWorld(GridManager.Instance.GridToWorld(wallCell));
                GD.Print($"[TestContinue] 建筑0坍塌: {collapsed}（应为 True）, 原墙体可行走: {wallWalkable}（应为 True）");
            }
        }
        else if (_frameCount >= 90)
        {
            GD.Print("[TestContinue] 测试完成");
            GetTree().Quit();
        }
    }

    private static Vector2I FindFirstWallCell(MapData map, BuildingData building)
    {
        for (int x = building.Bounds.Position.X; x < building.Bounds.Position.X + building.Bounds.Size.X; x++)
        {
            for (int y = building.Bounds.Position.Y; y < building.Bounds.Position.Y + building.Bounds.Size.Y; y++)
            {
                if (map.IsWall(x, y))
                {
                    return new Vector2I(x, y);
                }
            }
        }
        return new Vector2I(-1, -1);
    }
}
