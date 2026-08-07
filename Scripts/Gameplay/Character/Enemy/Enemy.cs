using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Gameplay.Building;
using OutpostProtocol.Gameplay.Entity;
using OutpostProtocol.Gameplay.Entity.Components;
using OutpostProtocol.Gameplay.Inventory;
using OutpostProtocol.Managers;
using OutpostProtocol.UI.Controllers;

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
    [Export] public int ResourceItemId = 1; // 掉落物品 ID（默认木材）

    /// <summary>掉落物预制体（未设置时只走背包直发逻辑）</summary>
    [Export] public PackedScene LootScene;

    [ExportGroup("核心攻击")]
    [Export] public int CoreDamage = 10; // 对核心造成的伤害

    // ============================================================
    // 运行时状态
    // ============================================================

    private Vector2 _targetPosition; // 目标点（前哨站核心）
    private BaseEntity _currentTarget; // 当前攻击目标
    private bool _hasTargetPosition;
    private OutpostCore _targetCore;
    private bool _isAttackingCore;

    // ============================================================
    // 生命周期
    // ============================================================

    public override void _Ready()
    {
        base._Ready();

        // 查找目标点上的前哨站核心
        _targetCore = GetTree().GetFirstNodeInGroup("outpost_core") as OutpostCore;

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

        UpdateSprite();

        // 加入敌人组（小地图/统计使用）
        AddToGroup("enemies");

        // 按敌人类型应用基础属性（源石虫/士兵/机动盾兵/双持剑士/炮击组长/重装防御组长）
        ApplyEnemyStats();

        // 敌人难度随天数增长
        var gameManager = GameManager.Instance;
        if (gameManager != null && gameManager.DayCount > 1)
        {
            float multiplier = 1f + (gameManager.DayCount - 1) * 0.12f;
            if (Health != null) Health.MaxHealth = (int)(Health.MaxHealth * multiplier);
            if (Attack != null) Attack.AttackDamage = (int)(Attack.AttackDamage * multiplier);
            ExpReward += (gameManager.DayCount - 1) * 2;
            ResourceReward += gameManager.DayCount - 1;
            GD.Print($"[{EntityName}] 难度缩放 x{multiplier:F2}（Day {gameManager.DayCount}）");
        }

        GD.Print($"[{EntityName}] 敌人初始化完成 — HP:{Health?.CurrentHealth}/{Health?.MaxHealth}");
    }

    /// <summary>按 EnemyDataId 应用敌人基础属性</summary>
    private void ApplyEnemyStats()
    {
        (int Hp, int Atk, float Speed, float Range, int CoreDmg, int Exp, int Res) cfg = EnemyDataId switch
        {
            2 => (160, 22, 105f, 80f, 12, 16, 6),    // 士兵
            3 => (260, 18, 80f, 70f, 14, 22, 8),     // 机动盾兵
            4 => (150, 32, 135f, 75f, 14, 20, 7),    // 双持剑士
            5 => (140, 28, 90f, 160f, 18, 30, 9),    // 炮击组长（远程）
            6 => (420, 30, 70f, 75f, 22, 45, 12),    // 重装防御组长（精英）
            _ => (100, 15, 120f, 80f, 10, 10, 5),    // 源石虫
        };

        if (Health != null) Health.MaxHealth = cfg.Hp;
        if (Attack != null)
        {
            Attack.AttackDamage = cfg.Atk;
            Attack.AttackRange = cfg.Range;
        }
        if (Movement != null) Movement.Speed = cfg.Speed;
        CoreDamage = cfg.CoreDmg;
        ExpReward = cfg.Exp;
        ResourceReward = cfg.Res;
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

    /// <summary>按敌人数据切换头像</summary>
    private void UpdateSprite()
    {
        var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        if (sprite == null) return;

        string path = EnemyDataId switch
        {
            2 => "res://Assets/Art/Enemies/enemy_soldier.png",
            3 => "res://Assets/Art/Enemies/enemy_shield.png",
            4 => "res://Assets/Art/Enemies/enemy_dual.png",
            5 => "res://Assets/Art/Enemies/enemy_artillery.png",
            6 => "res://Assets/Art/Enemies/enemy_heavy.png",
            _ => "res://Assets/Art/Enemies/enemy_slug.png",
        };

        var tex = GD.Load<Texture2D>(path);
        if (tex != null)
        {
            sprite.Texture = tex;
            sprite.Scale = new Vector2(0.3f, 0.3f);
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

        float dist = GlobalPosition.DistanceTo(_targetPosition);

        // 接近核心 → 攻击核心而不是走到中心
        if (dist < 30.0f && _targetCore != null && !_targetCore.IsDestroyed)
        {
            AttackCore();
            return;
        }

        // 如果接近目标点，停止移动
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

    private void AttackCore()
    {
        if (_isAttackingCore) return;
        if (_targetCore == null || _targetCore.IsDestroyed) return;

        _isAttackingCore = true;
        Movement?.Stop();

        GD.Print($"[{EntityName}] 攻击核心！");
        _targetCore.TakeDamage(CoreDamage);

        // 攻击后延迟再攻击（避免刷屏，核心自身也有伤害冷却）
        GetTree().CreateTimer(1.0f).Timeout += () =>
        {
            if (IsDead) return; // 敌人已死亡，停止攻击循环
            _isAttackingCore = false;
            if (_targetCore != null && !_targetCore.IsDestroyed)
            {
                AttackCore();
            }
        };
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
        OutpostProtocol.Managers.AudioManager.Instance?.Play("enemy_die", -10f);

        // 生成场景掉落物
        SpawnLoot();

        // 广播敌人死亡（用于波次计数）
        EventBus.Instance.EmitLogMessage($"{EntityName} 被击杀", "INFO");
    }

    private void SpawnLoot()
    {
        if (LootScene == null)
        {
            GD.Print($"[{EntityName}] 未设置掉落物预制体");
            return;
        }

        var loot = LootScene.Instantiate<LootItem>();
        if (loot == null) return;

        loot.GlobalPosition = GlobalPosition;
        GetTree().CurrentScene.AddChild(loot);
        loot.GlobalPosition = GlobalPosition;
        int quantity = (int)((ResourceReward > 0 ? ResourceReward : 1) * (1f + TalentTreeController.LootDropRateBonus));
        loot.SetLoot(ResourceItemId > 0 ? ResourceItemId : 1, quantity);

        GD.Print($"[{EntityName}] 掉落 {loot.Data?.Name ?? "物品"} x{ResourceReward}");
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
