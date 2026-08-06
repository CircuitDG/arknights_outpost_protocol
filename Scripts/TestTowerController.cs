using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Core.Grid;
using OutpostProtocol.Gameplay.Building;
using OutpostProtocol.Gameplay.Character.Enemy;
using OutpostProtocol.Managers;

/// <summary>
/// 防御塔自动化测试：
/// 1. 建造三种塔（弩炮/凝胶/爆裂）
/// 2. 升级 + 耐久/修复/损毁
/// 3. 生成波次，塔自动索敌攻击
/// </summary>
public partial class TestTowerController : Node
{
    private const int GridWidth = 20;
    private const int GridHeight = 20;
    private const int TileSize = 16;

    private TowerBuilder _builder;
    private EnemySpawner _spawner;
    private GridManager _grid;
    private TileMapLayer _groundLayer;
    private TileMapLayer _obstacleLayer;
    private Node2D _towerContainer;
    private int _frameCount;
    private bool _testsBuilt;

    public override void _Ready()
    {
        _builder = GetNode<TowerBuilder>("../TowerBuilder");
        _spawner = GetNode<EnemySpawner>("../EnemySpawner");
        _grid = GridManager.Instance;
        _groundLayer = GetNode<TileMapLayer>("../GroundLayer");
        _obstacleLayer = GetNode<TileMapLayer>("../ObstacleLayer");
        _towerContainer = GetNode<Node2D>("../TowerContainer");

        if (_builder == null || _spawner == null || _grid == null)
        {
            GD.PrintErr("[TestTower] 缺少必要节点");
            return;
        }

        // 构建测试地图
        _grid.GridSize = TileSize;
        SetupTileSet(_groundLayer);
        SetupTileSet(_obstacleLayer);
        PaintGround();
        PaintObstacles();
        _grid.ObstacleLayer = _obstacleLayer;
        _grid.BuildGrid();

        // 注入引用（NodePath 文本序列化对 C# 导出偶发失效，代码显式赋值保证可用）
        _spawner.TargetPoint = GetNode<Node2D>("../TargetPoint");
        _builder.TowerContainer = _towerContainer;
        _builder.TowerPrefabs = new[]
        {
            GD.Load<PackedScene>("res://Scenes/Buildings/Ballista.tscn"),
            GD.Load<PackedScene>("res://Scenes/Buildings/GelTower.tscn"),
            GD.Load<PackedScene>("res://Scenes/Buildings/ExplosionTower.tscn"),
        };

        EventBus.Instance.WaveCompleted += OnWaveCompleted;
        GD.Print("========== 防御塔测试 ==========");
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.WaveCompleted -= OnWaveCompleted;
        }
    }

    public override void _Process(double delta)
    {
        _frameCount++;

        if (!_testsBuilt && DataManager.Instance.IsLoaded)
        {
            _testsBuilt = true;
            BuildTowers();
        }

        if (!_testsBuilt) return;

        if (_frameCount == 8)
        {
            TestUpgradeAndDurability();
        }
        else if (_frameCount == 12)
        {
            StartWave();
        }
        else if (_frameCount == 120)
        {
            ShowStatus();
        }
        else if (_frameCount == 150)
        {
            TestDestroy();
        }
        else if (_frameCount >= 160)
        {
            ShowStatus();
            GD.Print("[TestTower] 测试完成");
            GetTree().Quit();
        }
    }

    private void BuildTowers()
    {
        // 弩炮台 (5,5)
        _builder.StartBuildMode(0);
        _builder.PlaceTowerAt(new Vector2(88, 88));
        _builder.ExitBuildMode();

        // 减速凝胶塔 (7,5)
        _builder.StartBuildMode(1);
        _builder.PlaceTowerAt(new Vector2(120, 88));
        _builder.ExitBuildMode();

        // 源石爆裂塔 (5,7)
        _builder.StartBuildMode(2);
        _builder.PlaceTowerAt(new Vector2(88, 120));
        _builder.ExitBuildMode();

        GD.Print("[TestTower] 已放置 3 座塔");
        ShowStatus();
    }

    private void TestUpgradeAndDurability()
    {
        var tower = GetTower(0);
        if (tower == null) return;

        tower.Upgrade(); // Lv.2
        GD.Print($"[TestTower] 升级后: Lv.{tower.Level}, 伤害:{tower.CurrentDamage}, 射程:{tower.CurrentRange:F0}, 攻速:{tower.CurrentSpeed:F2}");

        tower.TakeDurabilityDamage(30);
        tower.Repair(20);
        GD.Print($"[TestTower] 耐久测试: {tower.CurrentDurability}/{tower.MaxDurability}");
    }

    private void StartWave()
    {
        _spawner.StartWave(1);

        int guard = 0;
        while (_spawner.SpawnNextEnemy() && guard++ < 100)
        {
            var enemies = _spawner.GetActiveEnemies();
            if (enemies.Count == 0) continue;

            var enemy = enemies[^1];
            enemy.GlobalPosition = new Vector2(24, 24);
            // 降低血量，便于 headless 快速验证塔击杀
            if (enemy.Health != null)
            {
                enemy.Health.MaxHealth = 20;
                enemy.Health.CurrentHealth = 20;
            }
        }
        GD.Print("[TestTower] 波次 1 已生成 5 个敌人（HP=20）");
    }

    private void TestDestroy()
    {
        var tower = GetTower(1);
        if (tower == null) return;

        tower.TakeDurabilityDamage(999);
        GD.Print($"[TestTower] 损毁测试: IsBuilt={tower.IsBuilt}, IsDestroyed={tower.IsDestroyed}");
    }

    private TowerBase GetTower(int index)
    {
        int i = 0;
        foreach (var child in _towerContainer.GetChildren())
        {
            if (child is TowerBase tower)
            {
                if (i == index) return tower;
                i++;
            }
        }
        return null;
    }

    private void ShowStatus()
    {
        GD.Print("========== 塔状态 ==========");
        GD.Print($"塔数量: {_towerContainer.GetChildCount()}");
        foreach (var child in _towerContainer.GetChildren())
        {
            if (child is TowerBase tower)
            {
                GD.Print($"  {tower.Data?.Name} Lv.{tower.Level} 伤害:{tower.CurrentDamage} 射程:{tower.CurrentRange:F0} 攻速:{tower.CurrentSpeed:F2} 耐久:{tower.CurrentDurability}/{tower.MaxDurability}");
            }
        }
        GD.Print($"波次: {_spawner.CurrentWaveNumber}, 活跃: {_spawner.IsWaveActive}");
        GD.Print($"敌人存活: {_spawner.GetActiveEnemies().Count}, 已击杀: {_spawner.EnemiesKilled}/{_spawner.TotalEnemiesInWave}");
        GD.Print("=================================");
    }

    private void OnWaveCompleted(int waveNumber)
    {
        GD.Print($"[TestTower] 收到 WaveCompleted: {waveNumber}");
    }

    // ============================================================
    // 地图构建
    // ============================================================

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
                _groundLayer.SetCell(new Vector2I(x, y), 0, new Vector2I(0, 0));
            }
        }
    }

    private void PaintObstacles()
    {
        for (int x = 0; x < GridWidth; x++)
        {
            _obstacleLayer.SetCell(new Vector2I(x, 0), 0, new Vector2I(1, 0));
            _obstacleLayer.SetCell(new Vector2I(x, GridHeight - 1), 0, new Vector2I(1, 0));
        }
        for (int y = 0; y < GridHeight; y++)
        {
            _obstacleLayer.SetCell(new Vector2I(0, y), 0, new Vector2I(1, 0));
            _obstacleLayer.SetCell(new Vector2I(GridWidth - 1, y), 0, new Vector2I(1, 0));
        }

        for (int y = 5; y <= 14; y++)
        {
            _obstacleLayer.SetCell(new Vector2I(10, y), 0, new Vector2I(1, 0));
        }
    }
}
