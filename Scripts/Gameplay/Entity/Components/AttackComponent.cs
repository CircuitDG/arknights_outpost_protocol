using Godot;
using OutpostProtocol.Gameplay.Effects;
using System;
using System.Collections.Generic;

namespace OutpostProtocol.Gameplay.Entity.Components;

/// <summary>
/// 攻击组件
/// 职责：索敌、攻击间隔、伤害计算
/// </summary>
public partial class AttackComponent : Node
{
    // ============================================================
    // 导出变量
    // ============================================================

    [ExportGroup("攻击参数")]
    [Export] public int AttackDamage = 20;
    [Export] public float AttackRange = 100.0f;
    [Export] public float AttackInterval = 1.0f; // 攻击间隔（秒）
    [Export] public float AttackAngle = 360.0f; // 攻击角度（度，360=全方向）

    [ExportGroup("目标筛选")]
    [Export] public FactionType TargetFaction = FactionType.Enemy;
    [Export] public bool AutoAttack = true; // 是否自动攻击

    /// <summary>索敌物理查询使用的碰撞掩码（干员层=1，敌人层=2）</summary>
    [Export] public uint TargetCollisionMask = 2u;

    // ============================================================
    // 运行时状态
    // ============================================================

    private BaseEntity _owner;
    private BaseEntity _currentTarget;
    private float _attackTimer;
    private bool _isAttacking;
    private List<BaseEntity> _targetsInRange = new();

    // ============================================================
    // 事件
    // ============================================================

    public event Action<BaseEntity> TargetChanged;
    public event Action<BaseEntity, int> AttackExecuted; // 目标, 伤害

    // ============================================================
    // 公共属性
    // ============================================================

    public BaseEntity CurrentTarget => _currentTarget;
    public bool IsAttacking => _isAttacking;
    public float AttackCooldownProgress => AttackInterval > 0 ? _attackTimer / AttackInterval : 0f;

    // ============================================================
    // 生命周期
    // ============================================================

    public override void _Ready()
    {
        _owner = GetParent<BaseEntity>();
        if (_owner == null)
        {
            GD.PushError("[AttackComponent] 必须挂载在 BaseEntity 下");
            return;
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        if (!_isAttacking || _owner.IsDead)
        {
            _attackTimer = 0;
            return;
        }

        // 检查目标是否有效
        if (!IsTargetValid(_currentTarget))
        {
            _currentTarget = null;
            _isAttacking = false;
            TargetChanged?.Invoke(null);
            return;
        }

        // 目标超出射程：保持攻击指令，等待 AI 靠近（不重置冷却）
        if (!IsInRange(_currentTarget))
        {
            return;
        }

        // 攻击冷却
        _attackTimer += dt;
        if (_attackTimer >= AttackInterval)
        {
            ExecuteAttack(_currentTarget);
            _attackTimer = 0;
        }
    }

    // ============================================================
    // 攻击逻辑
    // ============================================================

    /// <summary>
    /// 攻击指定目标。
    /// 允许目标暂时超出射程（命令攻击时 AI 会负责靠近），
    /// 进入射程后自动开始攻击。
    /// </summary>
    public void Attack(BaseEntity target)
    {
        if (_owner.IsDead) return;

        if (!IsTargetValid(target))
        {
            GD.Print("[AttackComponent] 目标无效");
            return;
        }

        // 幂等：已经是当前目标且正在攻击时，不重置前摇计时
        if (_isAttacking && _currentTarget == target) return;

        _currentTarget = target;
        _isAttacking = true;
        _attackTimer = AttackInterval * 0.5f; // 首次攻击有短暂前摇
        TargetChanged?.Invoke(target);

        GD.Print($"[AttackComponent] 开始攻击: {target.EntityName}");
    }

    /// <summary>停止攻击</summary>
    public void StopAttack()
    {
        _isAttacking = false;
        _currentTarget = null;
        _attackTimer = 0;
        TargetChanged?.Invoke(null);
    }

    /// <summary>自动索敌（在射程内找最近的合法目标）</summary>
    public BaseEntity FindNearestTarget()
    {
        var space = _owner.GetWorld2D().DirectSpaceState;
        var query = new PhysicsShapeQueryParameters2D();
        query.Shape = new CircleShape2D { Radius = AttackRange };
        query.Transform = new Transform2D(0, _owner.GlobalPosition);
        query.CollisionMask = TargetCollisionMask;

        var results = space.IntersectShape(query);

        float minDist = float.MaxValue;
        BaseEntity nearest = null;

        foreach (var result in results)
        {
            // collider 可能是非 BaseEntity 的物理体（如博士），必须安全转换
            var collider = result["collider"].As<GodotObject>();
            if (collider is not BaseEntity entity || entity == _owner) continue;
            if (entity.Faction != TargetFaction) continue;
            if (entity.IsDead) continue;

            float dist = _owner.GlobalPosition.DistanceTo(entity.GlobalPosition);
            if (dist < minDist && dist <= AttackRange)
            {
                minDist = dist;
                nearest = entity;
            }
        }

        return nearest;
    }

    /// <summary>执行攻击</summary>
    private void ExecuteAttack(BaseEntity target)
    {
        if (target == null || target.IsDead) return;

        // 计算伤害（可扩展为携带者属性加成）
        int damage = AttackDamage;

        target.TakeDamage(damage, _owner);

        // 攻击表现：弹道 + 脉冲（干员/敌人通用）
        if (_owner is Node2D ownerNode && target is Node2D targetNode)
        {
            AttackEffects.SpawnTracer(ownerNode, targetNode, new Color(1f, 0.88f, 0.5f));
            AttackEffects.Pulse(ownerNode, 1f, 1.08f);
        }
        AttackExecuted?.Invoke(target, damage);

        GD.Print($"[AttackComponent] {_owner.EntityName} 攻击 {target.EntityName}，伤害 {damage}");
    }

    // ============================================================
    // 辅助方法
    // ============================================================

    private bool IsTargetValid(BaseEntity target)
    {
        if (target == null) return false;
        if (target == _owner) return false;
        if (target.IsDead) return false;
        if (target.Faction != TargetFaction) return false;
        return true;
    }

    private bool IsInRange(BaseEntity target)
    {
        if (target == null) return false;
        float dist = _owner.GlobalPosition.DistanceTo(target.GlobalPosition);
        return dist <= AttackRange;
    }

    /// <summary>获取射程内的所有合法目标（占位，后续由外部系统注入）</summary>
    public List<BaseEntity> GetTargetsInRange()
    {
        _targetsInRange.Clear();
        return _targetsInRange;
    }
}
