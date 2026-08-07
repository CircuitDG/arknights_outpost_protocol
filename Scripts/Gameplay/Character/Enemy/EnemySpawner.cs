using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Core.Grid;
using OutpostProtocol.Data;
using OutpostProtocol.Gameplay.Entity;
using OutpostProtocol.Managers;
using System;
using System.Collections.Generic;

namespace OutpostProtocol.Gameplay.Character.Enemy;

/// <summary>
/// 敌人波次生成器
/// 职责：根据配置生成敌人波次、管理波次进度、与 GameManager 联动
/// </summary>
public partial class EnemySpawner : Node
{
    // ============================================================
    // 导出变量
    // ============================================================

    [ExportGroup("生成配置")]
    [Export] public Node2D TargetPoint; // 敌人目标（前哨站核心）
    [Export] public Node2D SpawnContainer; // 敌人存放容器
    [Export] public PackedScene EnemyScene; // 敌人预制体

    [ExportGroup("波次配置")]
    [Export] public int StartWaveNumber = 1;
    [Export] public float WaveDelay = 2.0f; // 波次开始延迟（秒）

    [ExportGroup("调试")]
    [Export] public bool AutoStartWaves = true; // 是否自动开始波次
    [Export] public bool ShowDebugLogs = true;

    [ExportGroup("波次难度")]
    [Export] public bool UseWaveLevel = true; // 是否使用 GameManager.WaveLevel
    [Export] public int BaseWaveNumber = 1;

    // ============================================================
    // 运行时状态
    // ============================================================

    private int _currentWaveNumber = 1;
    private int _enemiesSpawned;
    private int _enemiesKilled;
    private int _totalEnemiesInWave;
    private bool _isWaveActive;
    private bool _isSpawning;
    private float _spawnTimer;
    private List<Enemy> _activeEnemies = new();
    private List<EnemySpawnConfig> _pendingSpawns = new();

    // ============================================================
    // 公共属性
    // ============================================================

    public int CurrentWaveNumber => _currentWaveNumber;
    public bool IsWaveActive => _isWaveActive;
    public int EnemiesKilled => _enemiesKilled;
    public int TotalEnemiesInWave => _totalEnemiesInWave;
    public float WaveProgress => _totalEnemiesInWave > 0 ? (float)_enemiesKilled / _totalEnemiesInWave : 0f;

    // ============================================================
    // 生命周期
    // ============================================================

    public override void _Ready()
    {
        if (EnemyScene == null)
        {
            GD.PushError("[EnemySpawner] 未设置 EnemyScene");
            return;
        }

        if (SpawnContainer == null)
        {
            SpawnContainer = new Node2D { Name = "EnemyContainer" };
            AddChild(SpawnContainer);
        }

        // 订阅事件
        EventBus.Instance.EntityDied += OnEntityDied;
        EventBus.Instance.GameStateChanged += OnGameStateChanged;

        // 初始化波次
        _currentWaveNumber = StartWaveNumber;

        GD.Print($"[EnemySpawner] 初始化完成 — 起始波次:{_currentWaveNumber}");
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.EntityDied -= OnEntityDied;
            EventBus.Instance.GameStateChanged -= OnGameStateChanged;
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // 波次生成逻辑
        if (_isSpawning && _isWaveActive)
        {
            if (_enemiesSpawned >= _totalEnemiesInWave)
            {
                _isSpawning = false;
                if (ShowDebugLogs)
                    GD.Print($"[EnemySpawner] 波次 {_currentWaveNumber} 生成完成，等待清理");
            }
            else
            {
                _spawnTimer += dt;
                var config = GetCurrentSpawnConfig();
                if (config != null && _spawnTimer >= config.SpawnInterval)
                {
                    SpawnNextEnemy();
                    _spawnTimer = 0;
                }
            }
        }

        // 检查波次是否完成（所有敌人生成且击杀）
        if (_isWaveActive && !_isSpawning && _activeEnemies.Count == 0)
        {
            CompleteWave();
        }
    }

    // ============================================================
    // 波次管理
    // ============================================================

    /// <summary>开始指定波次</summary>
    public void StartWave(int waveNumber)
    {
        if (_isWaveActive)
        {
            GD.PushWarning($"[EnemySpawner] 波次 {_currentWaveNumber} 正在进行中");
            return;
        }

        // 计算有效波次号（受难度影响）
        int effectiveWave = UseWaveLevel ? GetEffectiveWaveNumber() : waveNumber;

        // 加载波次配置
        var waveData = DataManager.Instance.GetWaveByNumber(effectiveWave);
        if (waveData == null)
        {
            GD.Print($"[EnemySpawner] 未找到波次 {effectiveWave} 配置，动态生成");
            GenerateDynamicWave(effectiveWave);
            return;
        }

        _currentWaveNumber = effectiveWave;
        _enemiesSpawned = 0;
        _enemiesKilled = 0;
        _spawnTimer = 0;

        // 按敌人类型展开生成队列（支持同波次多兵种）
        _pendingSpawns.Clear();
        foreach (var config in waveData.EnemyTypes)
        {
            for (int i = 0; i < config.Count; i++)
            {
                _pendingSpawns.Add(config);
            }
        }

        _totalEnemiesInWave = _pendingSpawns.Count;
        _isWaveActive = true;
        _isSpawning = true;

        GD.Print($"[EnemySpawner] 波次 {effectiveWave} 开始 — 总计 {_totalEnemiesInWave} 个敌人");

        // 广播波次开始
        EventBus.Instance.EmitWaveStarted(effectiveWave);
        EventBus.Instance.EmitLogMessage($"波次 {effectiveWave} 开始！{_totalEnemiesInWave} 个敌人来袭", "WARN");
    }

    /// <summary>获取受难度影响的波次编号</summary>
    private int GetEffectiveWaveNumber()
    {
        if (GameManager.Instance == null) return _currentWaveNumber;
        return Math.Max(1, GameManager.Instance.WaveLevel);
    }

    /// <summary>动态生成波次（配置不存在时按等级生成）</summary>
    private void GenerateDynamicWave(int waveLevel)
    {
        _currentWaveNumber = waveLevel;
        _enemiesSpawned = 0;
        _enemiesKilled = 0;
        _spawnTimer = 0;

        // 按等级计算敌人数量
        int enemyCount = 3 + waveLevel * 2; // 3, 5, 7, 9...
        int eliteCount = Math.Max(0, waveLevel / 3); // 每 3 级出 1 个精英

        _pendingSpawns.Clear();

        var normal = new EnemySpawnConfig
        {
            EnemyId = 1,
            Count = enemyCount,
            SpawnInterval = Math.Max(0.5f, 2.0f - waveLevel * 0.05f),
            SpawnPoint = "Edge",
            ExpReward = 5 + waveLevel,
            ResourceReward = 2 + waveLevel / 2,
        };
        for (int i = 0; i < normal.Count; i++) _pendingSpawns.Add(normal);

        if (eliteCount > 0)
        {
            var elite = new EnemySpawnConfig
            {
                EnemyId = 2,
                Count = eliteCount,
                SpawnInterval = 3.0f,
                SpawnPoint = "Edge",
                ExpReward = 20 + waveLevel * 2,
                ResourceReward = 5 + waveLevel,
            };
            for (int i = 0; i < elite.Count; i++) _pendingSpawns.Add(elite);
        }

        _totalEnemiesInWave = _pendingSpawns.Count;
        _isWaveActive = true;
        _isSpawning = true;

        GD.Print($"[EnemySpawner] 动态生成波次 {waveLevel}: {_totalEnemiesInWave} 个敌人 (精英 x{eliteCount})");
        EventBus.Instance.EmitWaveStarted(waveLevel);
        EventBus.Instance.EmitLogMessage($"波次 {waveLevel} 开始！{_totalEnemiesInWave} 个敌人来袭", "WARN");
    }

    /// <summary>开始下一波</summary>
    public void StartNextWave()
    {
        StartWave(_currentWaveNumber + 1);
    }

    /// <summary>完成当前波次</summary>
    private void CompleteWave()
    {
        if (!_isWaveActive) return;

        _isWaveActive = false;
        _isSpawning = false;

        GD.Print($"[EnemySpawner] 波次 {_currentWaveNumber} 完成！击杀 {_enemiesKilled}/{_totalEnemiesInWave}");

        // 广播波次完成
        EventBus.Instance.EmitWaveCompleted(_currentWaveNumber);
        EventBus.Instance.EmitLogMessage($"波次 {_currentWaveNumber} 完成！", "INFO");
    }

    // ============================================================
    // 敌人生成
    // ============================================================

    /// <summary>立即生成队列中的下一个敌人（波次进行中）</summary>
    public bool SpawnNextEnemy()
    {
        if (!_isWaveActive || _enemiesSpawned >= _totalEnemiesInWave) return false;

        var config = GetCurrentSpawnConfig();
        if (config == null) return false;

        if (!SpawnEnemy(config)) return false;
        _pendingSpawns.RemoveAt(0);
        _spawnTimer = 0;
        return true;
    }

    /// <summary>生成单个敌人</summary>
    private bool SpawnEnemy(EnemySpawnConfig config)
    {
        if (EnemyScene == null || TargetPoint == null)
        {
            GD.PushWarning("[EnemySpawner] EnemyScene 或 TargetPoint 未设置，无法生成");
            return false;
        }

        var enemy = EnemyScene.Instantiate<Enemy>();
        if (enemy == null)
        {
            GD.PushError("[EnemySpawner] 敌人实例化失败");
            return false;
        }

        // 设置奖励（配置未指定时使用敌人默认值）
        enemy.EnemyDataId = config.EnemyId;
        if (config.ExpReward > 0) enemy.ExpReward = config.ExpReward;
        if (config.ResourceReward > 0) enemy.ResourceReward = config.ResourceReward;

        // 加入容器后再设置坐标（未入树时 GlobalPosition 不生效）
        SpawnContainer.AddChild(enemy);
        enemy.GlobalPosition = GetSpawnPosition(config.SpawnPoint);

        // 设置目标位置（前哨站核心）
        enemy.SetTargetPosition(TargetPoint.GlobalPosition);

        _activeEnemies.Add(enemy);
        _enemiesSpawned++;

        if (ShowDebugLogs && _enemiesSpawned % 5 == 0)
        {
            GD.Print($"[EnemySpawner] 生成敌人 {_enemiesSpawned}/{_totalEnemiesInWave} 在 ({enemy.GlobalPosition.X:F0}, {enemy.GlobalPosition.Y:F0})");
        }

        return true;
    }

    /// <summary>获取生成位置（优先地图边缘可行走格，回退随机可行走格/屏幕边缘）</summary>
    private Vector2 GetSpawnPosition(string spawnPoint)
    {
        var grid = GridManager.Instance;
        if (grid != null && grid.IsBuilt)
        {
            var dims = grid.GridDimensions;
            var candidates = new List<Vector2I>();

            for (int x = 0; x < dims.X; x++)
            {
                if (grid.IsWalkable(new Vector2I(x, 0))) candidates.Add(new Vector2I(x, 0));
                if (grid.IsWalkable(new Vector2I(x, dims.Y - 1))) candidates.Add(new Vector2I(x, dims.Y - 1));
            }
            for (int y = 0; y < dims.Y; y++)
            {
                if (grid.IsWalkable(new Vector2I(0, y))) candidates.Add(new Vector2I(0, y));
                if (grid.IsWalkable(new Vector2I(dims.X - 1, y))) candidates.Add(new Vector2I(dims.X - 1, y));
            }

            if (candidates.Count > 0)
            {
                return grid.GridToWorld(candidates[(int)(GD.Randi() % (uint)candidates.Count)]);
            }

            // 边缘全被堵住时：随机可行走格
            for (int attempt = 0; attempt < 50; attempt++)
            {
                var cell = new Vector2I(
                    (int)(GD.Randi() % (uint)dims.X),
                    (int)(GD.Randi() % (uint)dims.Y)
                );
                if (grid.IsWalkable(cell))
                {
                    return grid.GridToWorld(cell);
                }
            }
        }

        // 回退：屏幕边缘外
        var viewport = GetViewport();
        if (viewport != null)
        {
            var rect = viewport.GetVisibleRect();
            float edge = GD.Randf();
            if (edge < 0.25f) return new Vector2(rect.Position.X - 100, rect.Position.Y + GD.Randf() * rect.Size.Y);
            if (edge < 0.5f) return new Vector2(rect.Position.X + rect.Size.X + 100, rect.Position.Y + GD.Randf() * rect.Size.Y);
            if (edge < 0.75f) return new Vector2(rect.Position.X + GD.Randf() * rect.Size.X, rect.Position.Y - 100);
            return new Vector2(rect.Position.X + GD.Randf() * rect.Size.X, rect.Position.Y + rect.Size.Y + 100);
        }

        return new Vector2(960, 540);
    }

    /// <summary>获取当前正在生成的敌人配置</summary>
    private EnemySpawnConfig GetCurrentSpawnConfig()
    {
        return _pendingSpawns.Count > 0 ? _pendingSpawns[0] : null;
    }

    // ============================================================
    // 事件回调
    // ============================================================

    private void OnEntityDied(Node2D entity, Node2D killer)
    {
        if (entity is Enemy enemy && _activeEnemies.Contains(enemy))
        {
            _activeEnemies.Remove(enemy);
            _enemiesKilled++;

            if (ShowDebugLogs)
            {
                GD.Print($"[EnemySpawner] 敌人被击杀 ({_enemiesKilled}/{_totalEnemiesInWave})");
            }
        }
    }

    private void OnGameStateChanged(GameState newState)
    {
        // 进入战斗阶段时开始波次
        if (newState == GameState.Battle && AutoStartWaves)
        {
            if (!_isWaveActive)
            {
                GetTree().CreateTimer(WaveDelay).Timeout += () =>
                {
                    if (GameManager.Instance.CurrentState == GameState.Battle)
                    {
                        StartWave(_currentWaveNumber);
                    }
                };
            }
        }

        // 游戏结束停止生成
        if (newState == GameState.GameOver)
        {
            _isSpawning = false;
            _isWaveActive = false;
        }
    }

    // ============================================================
    // 公共 API
    // ============================================================

    /// <summary>获取当前波次的剩余敌人数量</summary>
    public int GetRemainingEnemies()
    {
        return _totalEnemiesInWave - _enemiesKilled;
    }

    /// <summary>获取存活敌人列表</summary>
    public List<Enemy> GetActiveEnemies()
    {
        return new List<Enemy>(_activeEnemies);
    }

    /// <summary>清除所有敌人（用于测试或游戏结束）</summary>
    public void ClearAllEnemies()
    {
        foreach (var enemy in _activeEnemies)
        {
            if (enemy != null && !enemy.IsDead)
            {
                enemy.QueueFree();
            }
        }
        _activeEnemies.Clear();
        _isWaveActive = false;
        _isSpawning = false;
        GD.Print("[EnemySpawner] 所有敌人已清除");
    }

    /// <summary>跳过当前波次（调试用）</summary>
    public void SkipWave()
    {
        if (_isWaveActive)
        {
            CompleteWave();
            GD.Print("[EnemySpawner] 跳过当前波次");
        }
    }
}
