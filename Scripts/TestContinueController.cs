using Godot;
using OutpostProtocol.Core.EventBus;
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

        // 预置一个已搜索的资源点状态
        var gw = GetNode<GameWorldController>("../Main");
        if (gw.MapData?.ResourcePoints.Count > 0)
        {
            var firstPoint = gw.MapData.ResourcePoints[0];
            run.ResourceStates.Add(new ResourceState
            {
                GridX = firstPoint.Position.X,
                GridY = firstPoint.Position.Y,
                Collected = true,
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
                var p = gw.MapData.ResourcePoints[0];
                bool hidden = true;
                foreach (var node in GetTree().GetNodesInGroup("gatherable_resources"))
                {
                    if (node is GatherableResource resource &&
                        resource.MapCell.X == p.Position.X &&
                        resource.MapCell.Y == p.Position.Y)
                    {
                        hidden = !resource.Visible;
                        break;
                    }
                }
                GD.Print($"[TestContinue] 已搜索资源点隐藏: {hidden}（应为 True）");
            }
        }
        else if (_frameCount >= 90)
        {
            GD.Print("[TestContinue] 测试完成");
            GetTree().Quit();
        }
    }
}
