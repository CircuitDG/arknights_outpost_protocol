using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Core.Grid;
using OutpostProtocol.Core.MapGeneration;
using OutpostProtocol.Data;
using OutpostProtocol.Gameplay.Building;
using OutpostProtocol.Gameplay.Character.Doctor;
using OutpostProtocol.Gameplay.Character.Enemy;
using OutpostProtocol.Gameplay.Character.Operator;
using OutpostProtocol.Gameplay.Inventory;
using OutpostProtocol.Managers;

/// <summary>
/// 主世界初始化控制器
/// 程序化生成 200×200 城市废墟 → 绘制图层 → 构建网格 → 注入引用 → 可选存档恢复
/// </summary>
public partial class GameWorldController : Node2D
{
    private const int TileSize = 16;

    private GridManager _grid;
    private TowerBuilder _builder;
    private EnemySpawner _spawner;
    private bool _restoreStarted;

    /// <summary>分块加载器（供测试/调试读取）</summary>
    public ChunkLoader ChunkLoader { get; private set; }

    /// <summary>生成的地图数据（供测试/调试读取）</summary>
    public MapData MapData { get; private set; }

    public int BuildingCount => MapData?.Buildings.Count ?? 0;
    public int ResourcePointCount => MapData?.ResourcePoints.Count ?? 0;

    public override void _Ready()
    {
        _grid = GridManager.Instance;
        _builder = GetNode<TowerBuilder>("TowerBuilder");
        _spawner = GetNode<EnemySpawner>("EnemySpawner");

        // 程序化生成城市地图
        var config = new MapConfig { Width = 200, Height = 200, Seed = 12345 };
        MapData = new MapGenerator(config).Generate();

        _grid.GridSize = TileSize;
        var tileSet = CreateTileSet();
        _grid.BuildGridFromMap(MapData); // 逻辑网格直接来自地图数据
        SetupChunkLoader(tileSet); // 视觉图层按块加载

        PlacePlayerAndOutpost();
        SpawnResourceNodes();

        // 移除场景预设的小测试资源点（城市地图改为程序化分布）
        GetNodeOrNull<Node2D>("World/Gatherable_1")?.QueueFree();
        GetNodeOrNull<Node2D>("World/Gatherable_2")?.QueueFree();

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

        GD.Print($"[GameWorld] 城市生成完成 — 200x200, 建筑:{BuildingCount}, 资源点:{ResourcePointCount}, 可行走:{_grid.GridDimensions}");

        // 继续游戏：待恢复（实际检测在 _Process，兼容场景就绪后才设置的标记）
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

        foreach (var node in GetTree().GetNodesInGroup("operators"))
        {
            if (node is Operator op && op.Data == null) return;
        }

        _restoreStarted = true;
        RestoreRun();
    }

    // ============================================================
    // 地图绘制
    // ============================================================

    private TileSet CreateTileSet()
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
        return tileSet;
    }

    private void SetupChunkLoader(TileSet tileSet)
    {
        var container = new Node2D { Name = "ChunkContainer" };
        GetNode<Node2D>("World").AddChild(container);

        var loader = new ChunkLoader
        {
            ChunkSize = 16,
            LoadRadius = 2,
            FollowTarget = GetNode<Node2D>("World/Doctor"),
        };
        loader.Setup(MapData, tileSet, container);
        AddChild(loader);
        ChunkLoader = loader;
    }

    // ============================================================
    // 玩家/前哨站/资源
    // ============================================================

    private void PlacePlayerAndOutpost()
    {
        var outpostWorld = _grid.GridToWorld(MapData.OutpostCell);
        GetNode<Node2D>("TargetPoint").GlobalPosition = outpostWorld;

        // 找离前哨站最近的街道格作为出生点
        Vector2I spawnCell = MapData.OutpostCell;
        int bestDist = int.MaxValue;
        foreach (var street in MapData.StreetCells)
        {
            int dist = Mathf.Abs(street.X - MapData.OutpostCell.X) + Mathf.Abs(street.Y - MapData.OutpostCell.Y);
            if (dist < bestDist)
            {
                bestDist = dist;
                spawnCell = street;
            }
        }

        Vector2 spawnWorld = _grid.GridToWorld(spawnCell);
        var doctor = GetNode<Node2D>("World/Doctor");
        doctor.GlobalPosition = spawnWorld;
        GetNode<Node2D>("World/Operator_1").GlobalPosition = spawnWorld + new Vector2(TileSize, 0);
        GetNode<Node2D>("World/Operator_2").GlobalPosition = spawnWorld + new Vector2(0, TileSize);
    }

    private void SpawnResourceNodes()
    {
        var container = GetNodeOrNull<Node2D>("World/ResourceContainer");
        if (container == null)
        {
            container = new Node2D { Name = "ResourceContainer" };
            GetNode<Node2D>("World").AddChild(container);
        }

        var prefab = GD.Load<PackedScene>("res://Scenes/World/GatherableResource.tscn");
        foreach (var point in MapData.ResourcePoints)
        {
            var node = prefab.Instantiate<GatherableResource>();
            node.ItemId = point.ItemId;
            node.AmountPerGather = point.Amount;
            node.MaxAmount = point.Amount;
            node.EnableRespawn = false; // 建筑内搜索点一次性
            node.MapCell = point.Position;
            container.AddChild(node);
            node.GlobalPosition = _grid.GridToWorld(point.Position);
        }
    }

    // ============================================================
    // 存档恢复
    // ============================================================

    private void RestoreRun()
    {
        var sm = SaveManager.Instance;
        var run = sm?.CurrentRun;
        if (run == null) return;

        var doctor = GetNodeOrNull<Doctor>("World/Doctor");
        if (doctor != null)
        {
            doctor.RestorePosition(new Vector2(run.DoctorPosX, run.DoctorPosY));
            doctor.SetHealth(run.DoctorHealth);
            doctor.SetStamina(run.DoctorStamina);
        }

        GameManager.Instance?.RestoreState(new SaveState
        {
            DayCount = run.DayCount,
            CurrentPhase = run.CurrentPhase,
            CurrentState = (int)GameState.Explore,
        });

        foreach (var node in GetTree().GetNodesInGroup("operators"))
        {
            if (node is not Operator op) continue;
            var rt = run.Operators.Find(o => o.OperatorId == op.OperatorDataId);
            if (rt != null)
            {
                op.RestoreFromRuntime(rt);
            }
        }

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

        // 资源点状态恢复（已搜索的保持隐藏）
        foreach (var state in run.ResourceStates)
        {
            if (!state.Collected) continue;
            foreach (var node in GetTree().GetNodesInGroup("gatherable_resources"))
            {
                if (node is GatherableResource resource &&
                    resource.MapCell.X == state.GridX &&
                    resource.MapCell.Y == state.GridY)
                {
                    resource.RestoreCollected();
                    break;
                }
            }
        }

        sm.RestoreOnGameLoad = false;
        GD.Print($"[GameWorld] 对局恢复完成 — Day {run.DayCount}, 干员:{run.Operators.Count}, 塔:{run.Towers.Count}");
    }
}
