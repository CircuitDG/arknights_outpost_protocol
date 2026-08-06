using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Data;
using OutpostProtocol.Gameplay.Character.Enemy;
using OutpostProtocol.Gameplay.Character.Operator;
using OutpostProtocol.Gameplay.Entity;
using System.Collections.Generic;

namespace OutpostProtocol.Managers;

/// <summary>
/// 每日统计管理器（AutoLoad 单例）
/// 职责：记录当日击杀/经验/资源/塔/核心，供结算面板使用
/// </summary>
public partial class DailyStatsManager : Node
{
    private static DailyStatsManager _instance;
    public static DailyStatsManager Instance => _instance;

    private DailyStats _currentStats;
    private readonly Dictionary<int, int> _operatorLevelsAtStart = new();

    public DailyStats CurrentStats => _currentStats;

    public override void _Ready()
    {
        if (_instance != null)
        {
            GD.PushWarning("DailyStatsManager 已存在，销毁重复实例");
            QueueFree();
            return;
        }
        _instance = this;

        ResetStats();

        EventBus.Instance.EntityDied += OnEntityDied;
        EventBus.Instance.OperatorExpGained += OnOperatorExpGained;
        EventBus.Instance.OperatorLevelUp += OnOperatorLevelUp;
        EventBus.Instance.LootPickedUp += OnLootPickedUp;
        EventBus.Instance.ResourceGathered += OnResourceGathered;
        EventBus.Instance.TowerBuilt += OnTowerBuilt;
        EventBus.Instance.TowerUpgraded += OnTowerUpgraded;
        EventBus.Instance.CoreDamaged += OnCoreDamaged;
        EventBus.Instance.CoreRepaired += OnCoreRepaired;
        EventBus.Instance.WaveCompleted += OnWaveCompleted;
        EventBus.Instance.GameStateChanged += OnGameStateChanged;

        GD.Print("[DailyStatsManager] 初始化完成");
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.EntityDied -= OnEntityDied;
            EventBus.Instance.OperatorExpGained -= OnOperatorExpGained;
            EventBus.Instance.OperatorLevelUp -= OnOperatorLevelUp;
            EventBus.Instance.LootPickedUp -= OnLootPickedUp;
            EventBus.Instance.ResourceGathered -= OnResourceGathered;
            EventBus.Instance.TowerBuilt -= OnTowerBuilt;
            EventBus.Instance.TowerUpgraded -= OnTowerUpgraded;
            EventBus.Instance.CoreDamaged -= OnCoreDamaged;
            EventBus.Instance.CoreRepaired -= OnCoreRepaired;
            EventBus.Instance.WaveCompleted -= OnWaveCompleted;
            EventBus.Instance.GameStateChanged -= OnGameStateChanged;
        }
        _instance = null;
    }

    // ============================================================
    // 统计重置
    // ============================================================

    public void ResetStats()
    {
        _currentStats = new DailyStats
        {
            DayNumber = GameManager.Instance?.DayCount ?? 1,
        };

        _operatorLevelsAtStart.Clear();
        foreach (var op in GetOperators())
        {
            if (op?.Data != null)
            {
                _operatorLevelsAtStart[op.OperatorDataId] = op.CurrentLevel;
            }
        }

        GD.Print($"[DailyStatsManager] 统计已重置 — Day {_currentStats.DayNumber}");
    }

    // ============================================================
    // 事件处理
    // ============================================================

    private void OnEntityDied(Node2D entity, Node2D killer)
    {
        if (entity is Enemy enemy)
        {
            _currentStats.TotalKills++;

            if (killer is Operator op && op.Data != null)
            {
                _currentStats.OperatorKills.TryGetValue(op.OperatorDataId, out int kills);
                _currentStats.OperatorKills[op.OperatorDataId] = kills + 1;
            }
        }
    }

    private void OnOperatorExpGained(int operatorId, int amount)
    {
        _currentStats.TotalExpGained += amount;
        _currentStats.OperatorExpGained.TryGetValue(operatorId, out int exp);
        _currentStats.OperatorExpGained[operatorId] = exp + amount;
    }

    private void OnOperatorLevelUp(Node2D op, int newLevel)
    {
        // 经验已在 OnOperatorExpGained 统计
    }

    private void OnLootPickedUp(int itemId, int quantity)
    {
        _currentStats.ResourcesGained.TryGetValue(itemId, out int count);
        _currentStats.ResourcesGained[itemId] = count + quantity;
    }

    private void OnResourceGathered(int itemId, int quantity)
    {
        _currentStats.ResourcesGained.TryGetValue(itemId, out int count);
        _currentStats.ResourcesGained[itemId] = count + quantity;
    }

    private void OnTowerBuilt(int towerId)
    {
        _currentStats.TowersBuilt++;
    }

    private void OnTowerUpgraded(int towerId, int newLevel)
    {
        _currentStats.TowersUpgraded++;
    }

    private void OnCoreDamaged(int currentHealth, int damage)
    {
        _currentStats.CoreHealthLost += damage;
    }

    private void OnCoreRepaired(int amount)
    {
        _currentStats.CoreHealthRepaired += amount;
    }

    private void OnWaveCompleted(int waveNumber)
    {
        _currentStats.WavesCleared++;
    }

    private void OnGameStateChanged(GameState newState)
    {
        if (newState == GameState.Rest)
        {
            SnapshotOperators();
        }

        if (newState == GameState.Explore && GameManager.Instance != null &&
            _currentStats.DayNumber != GameManager.Instance.DayCount)
        {
            ResetStats();
        }
    }

    // ============================================================
    // 快照
    // ============================================================

    private void SnapshotOperators()
    {
        _currentStats.OperatorSnapshots.Clear();

        foreach (var op in GetOperators())
        {
            if (op?.Data == null) continue;

            _currentStats.OperatorSnapshots.Add(new OperatorSnapshot
            {
                OperatorId = op.OperatorDataId,
                Name = op.EntityName,
                Level = op.CurrentLevel,
                Exp = op.CurrentExp,
                Health = op.Health?.CurrentHealth ?? 0,
                MaxHealth = op.Health?.MaxHealth ?? 0,
                Morale = op.Morale,
                IsDown = op.State == OperatorState.Down,
            });
        }
    }

    // ============================================================
    // 辅助方法
    // ============================================================

    private List<Operator> GetOperators()
    {
        var result = new List<Operator>();
        foreach (var node in GetTree().GetNodesInGroup("operators"))
        {
            if (node is Operator op)
            {
                result.Add(op);
            }
        }
        return result;
    }

    // ============================================================
    // 公共 API
    // ============================================================

    public int GetOperatorLevelAtStart(int operatorId)
    {
        return _operatorLevelsAtStart.GetValueOrDefault(operatorId, 1);
    }

    public int GetOperatorKills(int operatorId)
    {
        return _currentStats.OperatorKills.GetValueOrDefault(operatorId, 0);
    }

    public int GetOperatorExpGained(int operatorId)
    {
        return _currentStats.OperatorExpGained.GetValueOrDefault(operatorId, 0);
    }
}
