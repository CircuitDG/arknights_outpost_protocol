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
using System.Collections.Generic;

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
    private readonly Dictionary<Vector2I, bool> _savedResourceCollected = new();

    /// <summary>分块加载器（供测试/调试读取）</summary>
    public ChunkLoader ChunkLoader { get; private set; }

    /// <summary>生成的地图数据（供测试/调试读取）</summary>
    public MapData MapData { get; private set; }

    public int BuildingCount => MapData?.Buildings.Count ?? 0;
    public int ResourcePointCount => MapData?.ResourcePoints.Count ?? 0;

    public override void _Ready()
    {
        AddToGroup("game_world");

        _grid = GridManager.Instance;
        _builder = GetNode<TowerBuilder>("TowerBuilder");
        _spawner = GetNode<EnemySpawner>("EnemySpawner");

        // 程序化生成城市地图
        var config = new MapConfig { Width = 300, Height = 300, Seed = 12345 };
        MapData = new MapGenerator(config).Generate();

        _grid.GridSize = TileSize;
        var tileSet = CreateTileSet();
        _grid.BuildGridFromMap(MapData); // 逻辑网格直接来自地图数据
        SetupChunkLoader(tileSet); // 视觉图层按块加载

        PlacePlayerAndOutpost();

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

        GD.Print($"[GameWorld] 城市生成完成 — 300x300, 建筑:{BuildingCount}, 资源点:{ResourcePointCount}, 可行走:{_grid.GridDimensions}");

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
            Texture = GD.Load<Texture2D>("res://Assets/Art/tilemap.png"),
            TextureRegionSize = new Vector2I(TileSize, TileSize),
        };

        atlas.CreateTile(MapTiles.Grass);
        atlas.CreateTile(MapTiles.Wall);
        atlas.CreateTile(MapTiles.Street);
        atlas.CreateTile(MapTiles.Floor);
        tileSet.AddSource(atlas, 0);
        return tileSet;
    }

    /// <summary>应用建筑损坏状态并刷新网格/分块视觉</summary>
    public void ApplyBuildingStates(IEnumerable<BuildingStateRecord> states)
    {
        if (MapData == null || states == null) return;

        foreach (var record in states)
        {
            if (record.BuildingId >= 0 && record.BuildingId < MapData.Buildings.Count)
            {
                MapData.Buildings[record.BuildingId].State = (BuildingState)record.State;
            }
        }

        _grid.BuildGridFromMap(MapData);
        ChunkLoader?.RebuildAll();
    }

    private void SetupChunkLoader(TileSet tileSet)
    {
        var container = new Node2D { Name = "ChunkContainer" };
        var world = GetNode<Node2D>("World");
        world.AddChild(container);
        world.MoveChild(container, 0); // 插到最底层，保证角色显示在瓦片之上

        var loader = new ChunkLoader
        {
            ChunkSize = 16,
            LoadRadius = 2,
            AutoUnload = false,
            FollowTarget = GetNode<Node2D>("World/Doctor"),
        };
        loader.Setup(
            MapData,
            tileSet,
            container,
            GD.Load<PackedScene>("res://Scenes/World/GatherableResource.tscn"),
            cell => _savedResourceCollected.TryGetValue(cell, out bool collected) && collected
        );
        AddChild(loader);
        ChunkLoader = loader;

        // 相机限制在地图内 + 博士边界
        float mapSize = MapData.Width * TileSize;
        var doctor = GetNodeOrNull<Doctor>("World/Doctor");
        if (doctor != null)
        {
            doctor.MapBounds = new Vector2(mapSize, mapSize);
            var camera = doctor.GetNodeOrNull<Camera2D>("Camera2D");
            if (camera != null)
            {
                camera.LimitLeft = 0;
                camera.LimitTop = 0;
                camera.LimitRight = (int)mapSize;
                camera.LimitBottom = (int)mapSize;
            }
        }
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

    /// <summary>查询资源点是否在存档中标记为已搜索</summary>
    public bool IsResourceCollected(Vector2I cell)
    {
        return _savedResourceCollected.TryGetValue(cell, out bool collected) && collected;
    }

    // ============================================================
    // 存档恢复
    // ============================================================

    private void RestoreRun()
    {
        var sm = SaveManager.Instance;
        var run = sm?.CurrentRun;
        if (run == null) return;

        // 预载资源点状态（分块生成时按此判定）
        _savedResourceCollected.Clear();
        foreach (var state in run.ResourceStates)
        {
            if (state.Collected)
            {
                _savedResourceCollected[new Vector2I(state.GridX, state.GridY)] = true;
            }
        }
        GD.Print($"[GameWorld] 存档资源状态: {_savedResourceCollected.Count} 个");

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

        // 建筑损坏状态恢复（会触发网格重建 + 分块重载，资源节点随块按存档状态生成）
        ApplyBuildingStates(run.BuildingStates);

        sm.RestoreOnGameLoad = false;
        GD.Print($"[GameWorld] 对局恢复完成 — Day {run.DayCount}, 干员:{run.Operators.Count}, 塔:{run.Towers.Count}");
    }
}
