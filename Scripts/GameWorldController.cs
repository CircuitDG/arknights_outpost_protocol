using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Core.Grid;
using OutpostProtocol.Data;
using OutpostProtocol.Gameplay.Building;
using OutpostProtocol.Gameplay.Character.Doctor;
using OutpostProtocol.Gameplay.Character.Enemy;
using OutpostProtocol.Gameplay.Character.Operator;
using OutpostProtocol.Managers;

/// <summary>
/// 主世界初始化控制器
/// 运行时生成 20x20 测试地图、构建网格、注入场景引用（规避 C# Node 导出的 NodePath 序列化问题）
/// </summary>
public partial class GameWorldController : Node2D
{
    private const int GridWidth = 20;
    private const int GridHeight = 20;
    private const int TileSize = 16;

    private GridManager _grid;
    private TileMapLayer _ground;
    private TileMapLayer _obstacles;
    private TowerBuilder _builder;
    private EnemySpawner _spawner;
    private bool _restoreStarted;

    public override void _Ready()
    {
        _grid = GridManager.Instance;
        _ground = GetNode<TileMapLayer>("World/GroundLayer");
        _obstacles = GetNode<TileMapLayer>("World/ObstacleLayer");
        _builder = GetNode<TowerBuilder>("TowerBuilder");
        _spawner = GetNode<EnemySpawner>("EnemySpawner");

        // 构建网格
        _grid.GridSize = TileSize;
        SetupTileSet(_ground);
        SetupTileSet(_obstacles);
        PaintGround();
        PaintObstacles();
        _grid.ObstacleLayer = _obstacles;
        _grid.BuildGrid();

        // 注入场景引用
        _spawner.TargetPoint = GetNode<Node2D>("TargetPoint");
        _builder.TowerContainer = GetNode<Node2D>("TowerContainer");
        _builder.TowerPrefabs = new[]
        {
            GD.Load<PackedScene>("res://Scenes/Buildings/Ballista.tscn"),
            GD.Load<PackedScene>("res://Scenes/Buildings/GelTower.tscn"),
            GD.Load<PackedScene>("res://Scenes/Buildings/ExplosionTower.tscn"),
        };
        _builder.BuildWoodCosts = new[] { 20, 20, 30 };
        _builder.BuildIronCosts = new[] { 5, 5, 8 };
        _builder.BuildOriginiumCosts = new[] { 0, 0, 2 };

        GD.Print("[GameWorld] 世界初始化完成 — 地图 20x20, 资源点/波次/建造系统已接线");

        // 继续游戏：标记待恢复（实际检测在 _Process，兼容场景就绪后才设置的标记）
        if (SaveManager.Instance is { RestoreOnGameLoad: true, HasRun: true })
        {
            GD.Print("[GameWorld] 检测到对局存档，等待数据就绪后恢复");
        }
    }

    public override void _Process(double delta)
    {
        if (_restoreStarted) return;
        var sm = SaveManager.Instance;
        if (sm == null || !sm.RestoreOnGameLoad || !sm.HasRun) return;
        if (DataManager.Instance == null || !DataManager.Instance.IsLoaded) return;

        // 等待所有干员数据就绪（DataManager 异步）
        foreach (var node in GetTree().GetNodesInGroup("operators"))
        {
            if (node is Operator op && op.Data == null) return;
        }

        _restoreStarted = true;
        RestoreRun();
    }

    /// <summary>从 RunSave 恢复博士/干员/塔/游戏状态</summary>
    private void RestoreRun()
    {
        var sm = SaveManager.Instance;
        var run = sm?.CurrentRun;
        if (run == null) return;

        // 1. 博士
        var doctor = GetNodeOrNull<Doctor>("World/Doctor");
        if (doctor != null)
        {
            doctor.RestorePosition(new Vector2(run.DoctorPosX, run.DoctorPosY));
            doctor.SetHealth(run.DoctorHealth);
            doctor.SetStamina(run.DoctorStamina);
        }

        // 2. 游戏状态
        GameManager.Instance?.RestoreState(new SaveState
        {
            DayCount = run.DayCount,
            CurrentPhase = run.CurrentPhase,
            CurrentState = (int)GameState.Explore,
        });

        // 3. 干员
        foreach (var node in GetTree().GetNodesInGroup("operators"))
        {
            if (node is not Operator op) continue;
            var rt = run.Operators.Find(o => o.OperatorId == op.OperatorDataId);
            if (rt != null)
            {
                op.RestoreFromRuntime(rt);
            }
        }

        // 4. 防御塔
        foreach (var rt in run.Towers)
        {
            if (_builder?.TowerPrefabs == null || rt.TowerId < 1 || rt.TowerId > _builder.TowerPrefabs.Length)
            {
                continue;
            }

            var tower = _builder.TowerPrefabs[rt.TowerId - 1].Instantiate<TowerBase>();
            _builder.TowerContainer.AddChild(tower);
            tower.GlobalPosition = new Vector2(rt.PosX, rt.PosY);
            tower.RestoreFromRuntime(rt);
        }

        sm.RestoreOnGameLoad = false;
        GD.Print($"[GameWorld] 对局恢复完成 — Day {run.DayCount}, 干员:{run.Operators.Count}, 塔:{run.Towers.Count}");
    }

    private void SetupTileSet(TileMapLayer layer)
    {
        var tileSet = new TileSet { TileSize = new Vector2I(TileSize, TileSize) };
        var atlas = new TileSetAtlasSource
        {
            Texture = GD.Load<Texture2D>("res://icon.svg"),
            TextureRegionSize = new Vector2I(TileSize, TileSize),
        };

        atlas.CreateTile(new Vector2I(0, 0));
        atlas.CreateTile(new Vector2I(1, 0));
        tileSet.AddSource(atlas, 0);
        layer.TileSet = tileSet;
    }

    private void PaintGround()
    {
        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                _ground.SetCell(new Vector2I(x, y), 0, new Vector2I(0, 0));
            }
        }
    }

    private void PaintObstacles()
    {
        for (int x = 0; x < GridWidth; x++)
        {
            _obstacles.SetCell(new Vector2I(x, 0), 0, new Vector2I(1, 0));
            _obstacles.SetCell(new Vector2I(x, GridHeight - 1), 0, new Vector2I(1, 0));
        }
        for (int y = 0; y < GridHeight; y++)
        {
            _obstacles.SetCell(new Vector2I(0, y), 0, new Vector2I(1, 0));
            _obstacles.SetCell(new Vector2I(GridWidth - 1, y), 0, new Vector2I(1, 0));
        }

        for (int y = 5; y <= 14; y++)
        {
            _obstacles.SetCell(new Vector2I(10, y), 0, new Vector2I(1, 0));
        }
    }
}
