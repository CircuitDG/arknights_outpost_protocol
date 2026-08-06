using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Gameplay.Entity;
using OutpostProtocol.Gameplay.Entity.Components;

namespace OutpostProtocol.Gameplay.Character.Enemy;

/// <summary>
/// 敌人实体
/// 职责：寻路到目标点、攻击干员、死亡掉落
/// </summary>
public partial class Enemy : BaseEntity
{
    // ============================================================
    // 导出变量
    // ============================================================

    [ExportGroup("敌人配置")]
    [Export] public int EnemyDataId = 1;
    [Export] public float DetectionRange = 200.0f; // 索敌范围
    [Export] public float AttackRangeOffset = 5.0f; // 攻击范围偏移

    [ExportGroup("波次奖励")]
    [Export] public int ExpReward = 10;
    [Export] public int ResourceReward = 5; // 掉落物资

    // ============================================================
    // 运行时状态
    // ============================================================

    private Vector2 _targetPosition; // 目标点（前哨站核心）
    private BaseEntity _currentTarget; // 当前攻击目标
    private bool _hasTargetPosition;

    // ============================================================
    // 生命周期
    // ============================================================

    public override void _Ready()
    {
        base._Ready();

        // 标记阵营
        Faction = FactionType.Enemy;

        // 配置索敌：找玩家阵营（layer 1）
        if (Attack != null)
        {
            Attack.TargetFaction = FactionType.Player;
            Attack.TargetCollisionMask = 1u;
        }

        // 订阅事件
        EventBus.Instance.EntityDied += OnEntityDied;
        EventBus.Instance.GameStateChanged += OnGameStateChanged;

        GD.Print($"[{EntityName}] 敌人初始化完成 — HP:{Health?.CurrentHealth}/{Health?.MaxHealth}");
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (EventBus.Instance != null)
        {
            EventBus.Instance.EntityDied -= OnEntityDied;
            EventBus.Instance.GameStateChanged -= OnGameStateChanged;
        }
    }

    public override void _Process(double delta)
    {
        if (IsDead) return;

        // 自动索敌
        UpdateTarget();

        // 根据状态决定行为
        if (_currentTarget != null && !_currentTarget.IsDead)
        {
            HandleAttackBehavior();
        }
        else if (_hasTargetPosition)
        {
            HandleMoveBehavior();
        }
    }

    // ============================================================
    // 行为逻辑
    // ============================================================

    private void UpdateTarget()
    {
        if (Attack == null) return;

        // 在射程内找最近的干员
        var nearest = Attack.FindNearestTarget();
        if (nearest != null && nearest.Faction == FactionType.Player)
        {
            _currentTarget = nearest;
            return;
        }

        // 如果在检测范围内但不在射程内，继续追击
        if (_currentTarget != null && !_currentTarget.IsDead)
        {
            float dist = GlobalPosition.DistanceTo(_currentTarget.GlobalPosition);
            if (dist <= DetectionRange)
            {
                return;
            }
        }

        _currentTarget = null;
    }

    private void HandleAttackBehavior()
    {
        if (_currentTarget == null || _currentTarget.IsDead)
        {
            _currentTarget = null;
            return;
        }

        float dist = GlobalPosition.DistanceTo(_currentTarget.GlobalPosition);

        if (dist <= Attack.AttackRange - AttackRangeOffset)
        {
            // 在攻击范围内 → 攻击
            Attack.Attack(_currentTarget);
            Movement?.Stop();
        }
        else if (dist <= DetectionRange)
        {
            // 在追击范围内 → 移动靠近
            Movement?.MoveTo(_currentTarget.GlobalPosition);
        }
        else
        {
            // 超出追击范围 → 回到目标点
            _currentTarget = null;
            if (_hasTargetPosition)
            {
                Movement?.MoveTo(_targetPosition);
            }
        }
    }

    private void HandleMoveBehavior()
    {
        if (!_hasTargetPosition) return;

        // 如果接近目标点，停止移动
        float dist = GlobalPosition.DistanceTo(_targetPosition);
        if (dist < 10.0f)
        {
            Movement?.Stop();
            return;
        }

        // 如果已经停止移动，重新寻路
        if (Movement != null && !Movement.IsMoving)
        {
            Movement.MoveTo(_targetPosition);
        }
    }

    // ============================================================
    // 公共 API
    // ============================================================

    /// <summary>设置目标位置（前哨站核心）</summary>
    public void SetTargetPosition(Vector2 target)
    {
        _targetPosition = target;
        _hasTargetPosition = true;
        Movement?.MoveTo(target);
        GD.Print($"[{EntityName}] 设置目标位置: ({target.X:F0}, {target.Y:F0})");
    }

    /// <summary>获取当前是否已到达目标</summary>
    public bool HasReachedTarget()
    {
        if (!_hasTargetPosition) return false;
        return GlobalPosition.DistanceTo(_targetPosition) < 20.0f;
    }

    /// <summary>获取当前状态（用于调试）</summary>
    public string GetStateString()
    {
        if (IsDead) return "Dead";
        if (_currentTarget != null) return $"Attacking {_currentTarget.EntityName}";
        if (_hasTargetPosition) return $"Moving to target ({GlobalPosition.DistanceTo(_targetPosition):F0}px)";
        return "Idle";
    }

    // ============================================================
    // 死亡处理
    // ============================================================

    protected override void OnHealthDepleted()
    {
        base.OnHealthDepleted();

        // 广播敌人死亡（用于波次计数）
        EventBus.Instance.EmitLogMessage($"{EntityName} 被击杀", "INFO");
    }

    // ============================================================
    // 事件回调
    // ============================================================

    private void OnEntityDied(Node2D entity, Node2D killer)
    {
        if (entity == this)
        {
            GD.Print($"[{EntityName}] 被击杀，准备清理");
        }
    }

    private void OnGameStateChanged(GameState newState)
    {
        if (newState == GameState.GameOver || newState == GameState.Rest)
        {
            Movement?.Stop();
        }
    }

    // ============================================================
    // 重置（用于对象池复用）
    // ============================================================

    public void ResetEnemy()
    {
        _currentTarget = null;
        _hasTargetPosition = false;
        Health?.ResetHealth();
        Movement?.Stop();
        Show();
        _isDead = false;
    }
}
