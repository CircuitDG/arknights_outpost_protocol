using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Core.Grid;
using OutpostProtocol.Gameplay.Character.Enemy;
using OutpostProtocol.Gameplay.Character.Operator;
using OutpostProtocol.Gameplay.Entity;
using OutpostProtocol.Managers;

/// <summary>
/// 敌袭波次自动化测试：
/// 1. 生成波次 1（5 个敌人）
/// 2. 敌人寻路向目标点
/// 3. 干员自动攻击敌人
/// 4. 击杀全部敌人 → 波次完成事件
/// </summary>
public partial class TestEnemyController : Node
{
    private const int GridWidth = 20;
    private const int GridHeight = 20;
    private const int TileSize = 16;
    private static readonly Vector2 SpawnPos = new(24, 24);

    private EnemySpawner _spawner;
    private GridManager _grid;
    private TileMapLayer _groundLayer;
    private TileMapLayer _obstacleLayer;
    private int _frameCount;
    private bool _waveStarted;

    public override void _Ready()
    {
        _spawner = GetNode<EnemySpawner>("../EnemySpawner");
        _grid = GridManager.Instance;
        _groundLayer = GetNode<TileMapLayer>("../GroundLayer");
        _obstacleLayer = GetNode<TileMapLayer>("../ObstacleLayer");

        if (_spawner == null || _grid == null)
        {
            GD.PrintErr("[TestEnemy] 缺少必要节点");
            return;
        }

        // NodePath 文本序列化对 C# Node 导出偶发不生效，这里显式注入目标点引用
        _spawner.TargetPoint = GetNode<Node2D>("../TargetPoint");

        // 构建测试地图
        _grid.GridSize = TileSize;
        SetupTileSet(_groundLayer);
        SetupTileSet(_obstacleLayer);
        PaintGround();
        PaintObstacles();
        _grid.ObstacleLayer = _obstacleLayer;
        _grid.BuildGrid();

        // 加速干员攻击，便于 headless 验证
        foreach (var node in GetTree().GetNodesInGroup("operators"))
        {
            if (node is Operator op && op.Attack != null)
            {
                op.Attack.AttackInterval = 0.2f;
            }
        }

        // 订阅事件
        EventBus.Instance.WaveStarted += OnWaveStarted;
        EventBus.Instance.WaveCompleted += OnWaveCompleted;
        EventBus.Instance.EntityDied += OnEntityDied;

        GD.Print("========== 敌袭波次测试 ==========");
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.WaveStarted -= OnWaveStarted;
            EventBus.Instance.WaveCompleted -= OnWaveCompleted;
            EventBus.Instance.EntityDied -= OnEntityDied;
        }
    }

    public override void _Process(double delta)
    {
        _frameCount++;

        if (_frameCount == 5)
        {
            // 等待数据加载后启动波次并立即生成全部敌人
            if (DataManager.Instance.IsLoaded)
            {
                GD.Print($"[TestEnemy] Spawner 引用检查 — TargetPoint: {_spawner.TargetPoint}, EnemyScene: {_spawner.EnemyScene != null}, SpawnContainer: {_spawner.SpawnContainer}");
                StartWaveAndSpawnAll();
            }
        }
        else if (_frameCount == 10)
        {
            ShowStatus();
        }
        else if (_frameCount == 100)
        {
            var enemies = _spawner.GetActiveEnemies();
            if (enemies.Count > 0)
            {
                var first = enemies[0];
                GD.Print($"[TestEnemy] 首个敌人状态: {first.GetStateString()}, HP:{first.Health?.CurrentHealth}/{first.Health?.MaxHealth}");
            }
            ShowStatus();
        }
        else if (_frameCount == 105)
        {
            KillAllEnemies();
        }
        else if (_frameCount == 115)
        {
            ShowStatus();
        }
        else if (_frameCount >= 120)
        {
            GD.Print("[TestEnemy] 测试完成");
            GetTree().Quit();
        }
    }

    private void StartWaveAndSpawnAll()
    {
        _spawner.StartWave(1);
        _waveStarted = true;

        int guard = 0;
        while (_spawner.SpawnNextEnemy() && guard++ < 100)
        {
            var enemies = _spawner.GetActiveEnemies();
            if (enemies.Count > 0)
            {
                // 统一放到测试出生点，便于验证自动索敌
                enemies[^1].GlobalPosition = SpawnPos;
            }
        }

        GD.Print($"[TestEnemy] 已生成 {_spawner.TotalEnemiesInWave} 个敌人");
    }

    private void KillAllEnemies()
    {
        foreach (var enemy in _spawner.GetActiveEnemies())
        {
            if (enemy != null && !enemy.IsDead)
            {
                enemy.TakeDamage(99999, null);
            }
        }
        GD.Print("[TestEnemy] 已击杀全部存活敌人");
    }

    private void ShowStatus()
    {
        GD.Print("========== 波次状态 ==========");
        GD.Print($"当前波次: {_spawner.CurrentWaveNumber}");
        GD.Print($"波次活跃: {_spawner.IsWaveActive}");
        GD.Print($"已击杀: {_spawner.EnemiesKilled}/{_spawner.TotalEnemiesInWave}");
        GD.Print($"进度: {_spawner.WaveProgress * 100:F0}%");
        GD.Print($"存活敌人: {_spawner.GetActiveEnemies().Count}");
        GD.Print("=================================");
    }

    private void OnWaveStarted(int waveNumber)
    {
        GD.Print($"[TestEnemy] 收到 WaveStarted: {waveNumber}");
    }

    private void OnWaveCompleted(int waveNumber)
    {
        GD.Print($"[TestEnemy] 收到 WaveCompleted: {waveNumber}");
    }

    private void OnEntityDied(Node2D entity, Node2D killer)
    {
        if (entity is Enemy enemy)
        {
            GD.Print($"[TestEnemy] 敌人死亡: {enemy.EntityName}, 剩余敌人: {_spawner.GetActiveEnemies().Count}");
        }
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
