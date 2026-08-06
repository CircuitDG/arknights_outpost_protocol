using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Data;
using OutpostProtocol.Gameplay.Entity;
using OutpostProtocol.Gameplay.Inventory;
using OutpostProtocol.Managers;
using System;
using System.Collections.Generic;

namespace OutpostProtocol.Gameplay.Building;

/// <summary>
/// 防御塔基类
/// 职责：自动索敌攻击、升级、耐久度管理
/// </summary>
public partial class TowerBase : Node2D
{
    // ============================================================
    // 导出变量
    // ============================================================

    [ExportGroup("塔配置")]
    [Export] public int TowerDataId = 1;
    [Export] public int CurrentLevel = 1;

    [ExportGroup("视觉")]
    [Export] public Sprite2D TowerSprite;
    [Export] public Sprite2D RangeIndicator; // 射程指示器

    [ExportGroup("攻击")]
    [Export] public PackedScene ProjectileScene; // 弹道预制体

    /// <summary>资源背包（升级消耗；未接线时允许免费升级）</summary>
    [Export] public Backpack Backpack;

    // ============================================================
    // 运行时状态
    // ============================================================

    private TowerData _data;
    private int _currentDurability;
    private float _attackTimer;
    private readonly List<BaseEntity> _targetsInRange = new();
    private BaseEntity _currentTarget;
    private bool _isBuilt;

    // ============================================================
    // 组件引用
    // ============================================================

    private Area2D _detectionArea;
    private Timer _attackTimerNode;

    // ============================================================
    // 公共属性
    // ============================================================

    public int TowerId => TowerDataId;
    public int Level => CurrentLevel;
    public TowerData Data => _data;
    public int CurrentDurability => _currentDurability;
    public int MaxDurability => _data?.MaxDurability ?? 100;
    public bool IsBuilt => _isBuilt;
    public bool IsDestroyed => _currentDurability <= 0;

    // 当前属性（受等级影响）
    public int CurrentDamage => (_data?.BaseDamage ?? 0) + (int)GetLevelBonus("damage");
    public float CurrentRange => (_data?.AttackRange ?? 100f) + GetLevelBonus("range");
    public float CurrentSpeed => (_data?.AttackSpeed ?? 1f) * (1f + GetLevelBonus("speed") / 100f);
    public string CurrentSpecialEffect => GetCurrentSpecialEffect();

    // ============================================================
    // 生命周期
    // ============================================================

    public override void _Ready()
    {
        // 加载数据
        LoadTowerData();

        // 自动查找博士背包
        if (Backpack == null)
        {
            var doctor = GetTree().GetFirstNodeInGroup("doctor") as Node2D;
            Backpack = doctor?.GetNodeOrNull<Backpack>("Backpack");
        }

        // 获取组件
        _detectionArea = GetNodeOrNull<Area2D>("DetectionArea");
        _attackTimerNode = GetNodeOrNull<Timer>("AttackTimer");

        if (_detectionArea != null)
        {
            _detectionArea.BodyEntered += OnBodyEntered;
            _detectionArea.BodyExited += OnBodyExited;
        }

        if (_attackTimerNode != null)
        {
            _attackTimerNode.Timeout += OnAttackTimerTimeout;
        }

        // 初始化
        _currentDurability = MaxDurability;
        UpdateRangeIndicator();
        UpdateVisuals();

        _isBuilt = true;
        GD.Print($"[{_data?.Name ?? "Unknown"}] 建造完成 — Lv.{CurrentLevel}, 伤害:{CurrentDamage}, 射程:{CurrentRange}");
    }

    public override void _ExitTree()
    {
        if (_detectionArea != null)
        {
            _detectionArea.BodyEntered -= OnBodyEntered;
            _detectionArea.BodyExited -= OnBodyExited;
        }

        if (_attackTimerNode != null)
        {
            _attackTimerNode.Timeout -= OnAttackTimerTimeout;
        }
    }

    public override void _Process(double delta)
    {
        if (!_isBuilt || IsDestroyed) return;

        // 更新攻击计时器
        _attackTimer += (float)delta;

        // 如果没有目标，尝试索敌
        if (_currentTarget == null || !IsTargetValid(_currentTarget))
        {
            _currentTarget = FindTarget();
            if (_currentTarget != null)
            {
                _attackTimer = CurrentSpeed * 0.5f; // 首次攻击前摇
            }
        }

        // 如果有目标，检查是否仍在射程内
        if (_currentTarget != null && !IsInRange(_currentTarget))
        {
            _currentTarget = null;
        }

        // 攻击计时
        if (_currentTarget != null && _attackTimer >= CurrentSpeed)
        {
            ExecuteAttack();
            _attackTimer = 0;
        }
    }

    // ============================================================
    // 数据加载
    // ============================================================

    private void LoadTowerData()
    {
        _data = DataManager.Instance.GetTower(TowerDataId);
        if (_data == null)
        {
            GD.PushError($"[TowerBase] 未找到塔数据 ID:{TowerDataId}");
            Name = $"Unknown_{TowerDataId}";
        }
        else
        {
            Name = _data.Name;
        }
    }

    // ============================================================
    // 等级与属性
    // ============================================================

    private float GetLevelBonus(string stat)
    {
        if (_data == null || _data.UpgradeLevels == null) return 0;

        var levelData = _data.UpgradeLevels.Find(l => l.Level == CurrentLevel);
        if (levelData == null) return 0;

        return stat switch
        {
            "damage" => levelData.DamageBonus,
            "range" => levelData.RangeBonus,
            "speed" => levelData.SpeedBonus,
            _ => 0,
        };
    }

    private string GetCurrentSpecialEffect()
    {
        if (_data == null || _data.UpgradeLevels == null) return string.Empty;

        var levelData = _data.UpgradeLevels.Find(l => l.Level == CurrentLevel);
        return levelData?.SpecialEffect ?? string.Empty;
    }

    private string GetCurrentSpecialEffectDescription()
    {
        if (_data == null || _data.UpgradeLevels == null) return string.Empty;

        var levelData = _data.UpgradeLevels.Find(l => l.Level == CurrentLevel);
        return levelData?.SpecialEffectDescription ?? string.Empty;
    }

    /// <summary>获取升级所需资源</summary>
    public TowerUpgradeLevel GetUpgradeInfo()
    {
        if (_data == null || _data.UpgradeLevels == null) return null;

        int nextLevel = CurrentLevel + 1;
        return _data.UpgradeLevels.Find(l => l.Level == nextLevel);
    }

    /// <summary>是否可以升级（资源检查由外部系统管理）</summary>
    public bool CanUpgrade()
    {
        var info = GetUpgradeInfo();
        if (info == null) return false;
        if (IsDestroyed) return false;
        return true;
    }

    /// <summary>执行升级</summary>
    public bool Upgrade()
    {
        var info = GetUpgradeInfo();
        if (info == null)
        {
            GD.Print($"[{Name}] 已达最高等级");
            return false;
        }

        if (IsDestroyed)
        {
            GD.Print($"[{Name}] 已损坏，无法升级");
            return false;
        }

        // 消耗资源（未接线 Backpack 时允许升级）
        if (Backpack != null && !Backpack.TrySpend(info.WoodCost, info.IronCost, info.OriginiumCost))
        {
            GD.Print($"[{Name}] 资源不足，无法升级到 Lv.{CurrentLevel + 1}");
            return false;
        }

        CurrentLevel++;
        UpdateVisuals();
        UpdateRangeIndicator();

        GD.Print($"[{Name}] 升级成功！Lv.{CurrentLevel}");
        EventBus.Instance.EmitLogMessage($"{Name} 升级到 Lv.{CurrentLevel}", "INFO");
        return true;
    }

    // ============================================================
    // 索敌与攻击
    // ============================================================

    private void OnBodyEntered(Node body)
    {
        if (body is BaseEntity entity && entity.Faction == FactionType.Enemy && !entity.IsDead)
        {
            if (!_targetsInRange.Contains(entity))
            {
                _targetsInRange.Add(entity);
            }
        }
    }

    private void OnBodyExited(Node body)
    {
        if (body is BaseEntity entity)
        {
            _targetsInRange.Remove(entity);
            if (_currentTarget == entity)
            {
                _currentTarget = null;
            }
        }
    }

    private BaseEntity FindTarget()
    {
        // 优先选择最近的敌人
        float minDist = float.MaxValue;
        BaseEntity nearest = null;

        foreach (var entity in _targetsInRange)
        {
            if (entity == null || entity.IsDead) continue;
            if (entity.Faction != FactionType.Enemy) continue;

            float dist = GlobalPosition.DistanceTo(entity.GlobalPosition);
            if (dist < minDist && dist <= CurrentRange)
            {
                minDist = dist;
                nearest = entity;
            }
        }

        return nearest;
    }

    private bool IsTargetValid(BaseEntity target)
    {
        if (target == null) return false;
        if (target.IsDead) return false;
        if (target.Faction != FactionType.Enemy) return false;
        return IsInRange(target);
    }

    private bool IsInRange(BaseEntity target)
    {
        if (target == null) return false;
        return GlobalPosition.DistanceTo(target.GlobalPosition) <= CurrentRange;
    }

    private void ExecuteAttack()
    {
        if (_currentTarget == null || !IsTargetValid(_currentTarget)) return;

        // 计算伤害
        int damage = CurrentDamage;

        // 应用特殊效果
        ApplySpecialEffect(_currentTarget);

        // 执行攻击（来源是塔，Node2D）
        _currentTarget.TakeDamage(damage, this);

        // 创建弹道效果
        SpawnProjectile(_currentTarget.GlobalPosition);

        // 广播攻击事件
        EventBus.Instance.EmitEntityDamaged(_currentTarget, damage);

        if (GD.Randf() < 0.1f) // 减少日志刷屏
        {
            GD.Print($"[{Name}] 攻击 {_currentTarget.EntityName}，伤害 {damage}");
        }

        // 检查目标是否死亡
        if (_currentTarget.IsDead)
        {
            _currentTarget = null;
        }
    }

    private void OnAttackTimerTimeout()
    {
        // 使用 _Process 驱动的攻击循环，此 Timer 作为扩展预留
    }

    // ============================================================
    // 特殊效果
    // ============================================================

    private void ApplySpecialEffect(BaseEntity target)
    {
        string effect = CurrentSpecialEffect;
        if (string.IsNullOrEmpty(effect)) return;

        switch (effect)
        {
            case "piercing_shot":
                if (GD.Randf() < 0.2f)
                {
                    GD.Print($"[{Name}] 触发穿透射击！");
                }
                break;

            case "piercing_shot_enhanced":
                if (GD.Randf() < 0.4f)
                {
                    GD.Print($"[{Name}] 触发强化穿透射击！");
                }
                break;

            case "slow_50":
            case "slow_55":
            case "slow_damage":
            case "slow_damage_enhanced":
                // TODO: 减速/持续伤害由状态效果系统处理
                break;

            case "aoe_150":
            case "aoe_180":
            case "aoe_spike":
            case "aoe_spike_enhanced":
                // TODO: 范围伤害/地刺由弹道爆炸效果处理
                break;
        }
    }

    // ============================================================
    // 弹道效果
    // ============================================================

    private void SpawnProjectile(Vector2 targetPos)
    {
        if (ProjectileScene == null) return;

        var projectile = ProjectileScene.Instantiate<Node2D>();
        if (projectile == null) return;

        projectile.GlobalPosition = GlobalPosition;
        GetTree().CurrentScene.AddChild(projectile);

        // 简单弹道：移向目标
        var tween = projectile.CreateTween();
        tween.TweenProperty(projectile, "global_position", targetPos, 0.2f);
        tween.TweenCallback(Callable.From(() => projectile.QueueFree()));
    }

    // ============================================================
    // 耐久度与损毁
    // ============================================================

    /// <summary>受到伤害（敌人攻击塔）</summary>
    public void TakeDurabilityDamage(int damage)
    {
        if (IsDestroyed) return;

        _currentDurability = Math.Max(0, _currentDurability - damage);

        if (_currentDurability <= 0)
        {
            DestroyTower();
        }

        GD.Print($"[{Name}] 耐久度 {_currentDurability}/{MaxDurability}");
    }

    /// <summary>修复塔</summary>
    public void Repair(int amount)
    {
        if (_currentDurability >= MaxDurability) return;

        _currentDurability = Math.Min(MaxDurability, _currentDurability + amount);
        GD.Print($"[{Name}] 修复 +{amount}，耐久 {_currentDurability}/{MaxDurability}");
    }

    private void DestroyTower()
    {
        _isBuilt = false;
        GD.Print($"[{Name}] 已损毁！");

        EventBus.Instance.EmitLogMessage($"{Name} 已损毁", "WARN");

        if (TowerSprite != null)
        {
            TowerSprite.Modulate = Colors.Gray;
        }
    }

    // ============================================================
    // 视觉更新
    // ============================================================

    private void UpdateRangeIndicator()
    {
        if (RangeIndicator == null && _detectionArea == null) return;

        float range = CurrentRange;

        if (RangeIndicator != null)
        {
            RangeIndicator.Scale = new Vector2(range / 50, range / 50);
        }

        // 同步索敌区域半径
        if (_detectionArea != null)
        {
            var shape = _detectionArea.GetNodeOrNull<CollisionShape2D>("CollisionShape2D")?.Shape as CircleShape2D;
            if (shape != null)
            {
                shape.Radius = range;
            }
        }
    }

    private void UpdateVisuals()
    {
        if (TowerSprite == null) return;

        // 根据等级改变大小和亮度
        float scale = 0.8f + (CurrentLevel - 1) * 0.07f;
        TowerSprite.Scale = new Vector2(scale, scale);

        float brightness = 0.7f + (CurrentLevel - 1) * 0.1f;
        TowerSprite.Modulate = new Color(brightness, brightness, brightness);
    }

    // ============================================================
    // 存档支持
    // ============================================================

    /// <summary>导出运行时数据</summary>
    public TowerRuntime ExportRuntime()
    {
        return new TowerRuntime
        {
            TowerId = TowerDataId,
            CurrentLevel = CurrentLevel,
            PosX = GlobalPosition.X,
            PosY = GlobalPosition.Y,
            CurrentDurability = _currentDurability,
        };
    }

    /// <summary>从运行时数据恢复</summary>
    public void RestoreFromRuntime(TowerRuntime runtime)
    {
        if (runtime == null) return;

        TowerDataId = runtime.TowerId;
        CurrentLevel = runtime.CurrentLevel;
        GlobalPosition = new Vector2(runtime.PosX, runtime.PosY);
        _currentDurability = runtime.CurrentDurability;

        // 重新加载数据和更新视觉
        LoadTowerData();
        UpdateVisuals();
        UpdateRangeIndicator();

        if (_currentDurability <= 0)
        {
            DestroyTower();
        }
        else
        {
            _isBuilt = true;
        }
    }
}
