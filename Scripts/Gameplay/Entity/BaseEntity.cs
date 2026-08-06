using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Gameplay.Entity.Components;
using System;

namespace OutpostProtocol.Gameplay.Entity;

/// <summary>
/// 实体基类
/// 所有可交互单位（干员、敌人、炮塔）的根节点。
/// 继承 CharacterBody2D 以便 CollisionShape2D 参与物理检测（索敌/点击判定）。
/// </summary>
public partial class BaseEntity : CharacterBody2D
{
    // ============================================================
    // 基础属性
    // ============================================================

    [Export] public int EntityId { get; set; }
    [Export] public string EntityName { get; set; } = "Entity";
    [Export] public FactionType Faction { get; set; } = FactionType.Neutral;

    // ============================================================
    // 组件引用（由子类初始化）
    // ============================================================

    public HealthComponent Health { get; protected set; }
    public MovementComponent Movement { get; protected set; }
    public AttackComponent Attack { get; protected set; }
    public SkillComponent Skill { get; protected set; }

    // ============================================================
    // 状态
    // ============================================================

    protected bool _isDead;
    public bool IsDead => _isDead;
    public event Action<BaseEntity> OnDeath;

    // ============================================================
    // 生命周期
    // ============================================================

    public override void _Ready()
    {
        // 组件可能因实体类型不同而缺失，使用 GetNodeOrNull 保证健壮性
        Health = GetNodeOrNull<HealthComponent>("HealthComponent");
        Movement = GetNodeOrNull<MovementComponent>("MovementComponent");
        Attack = GetNodeOrNull<AttackComponent>("AttackComponent");
        Skill = GetNodeOrNull<SkillComponent>("SkillComponent");

        if (Health == null)
            GD.PushWarning($"[{EntityName}] 缺少 HealthComponent");

        if (Movement == null)
            GD.PushWarning($"[{EntityName}] 缺少 MovementComponent");

        if (Attack == null)
            GD.PushWarning($"[{EntityName}] 缺少 AttackComponent");

        if (Health != null)
        {
            Health.HealthDepleted += OnHealthDepleted;
        }
    }

    public override void _ExitTree()
    {
        if (Health != null)
        {
            Health.HealthDepleted -= OnHealthDepleted;
        }
    }

    // ============================================================
    // 战斗方法
    // ============================================================

    /// <summary>受到伤害</summary>
    public virtual void TakeDamage(int damage, BaseEntity source)
    {
        if (_isDead) return;
        Health?.Reduce(damage);

        EventBus.Instance.EmitEntityDamaged(this, damage);

        GD.Print($"[{EntityName}] 受到 {damage} 点伤害，剩余 HP:{Health?.CurrentHealth}/{Health?.MaxHealth}");
    }

    /// <summary>治疗</summary>
    public virtual void Heal(int amount)
    {
        if (_isDead) return;
        Health?.Heal(amount);
    }

    /// <summary>死亡处理（干员会重写为"战斗不能"）</summary>
    protected virtual void OnHealthDepleted()
    {
        if (_isDead) return;
        _isDead = true;

        GD.Print($"[{EntityName}] 死亡");

        // 停止移动和攻击
        Movement?.Stop();
        Attack?.StopAttack();

        // 广播
        EventBus.Instance.EmitEntityDied(this, null);
        OnDeath?.Invoke(this);
    }

    /// <summary>复活（用于干员紧急撤离后回归）</summary>
    public virtual void Revive()
    {
        if (!_isDead) return;
        _isDead = false;
        Health?.FullHeal();
        GD.Print($"[{EntityName}] 复活");
    }

    // ============================================================
    // 寻路移动
    // ============================================================

    /// <summary>移动到目标点</summary>
    public void MoveTo(Vector2 target)
    {
        if (_isDead) return;
        Movement?.MoveTo(target);
    }

    /// <summary>停止移动</summary>
    public void StopMoving()
    {
        Movement?.Stop();
    }

    public bool IsMoving => Movement?.IsMoving ?? false;

    // ============================================================
    // 攻击控制
    // ============================================================

    /// <summary>攻击目标</summary>
    public virtual void AttackTarget(BaseEntity target)
    {
        if (_isDead) return;
        Attack?.Attack(target);
    }

    /// <summary>停止攻击</summary>
    public void StopAttacking()
    {
        Attack?.StopAttack();
    }
}

/// <summary>阵营枚举</summary>
public enum FactionType
{
    Neutral,
    Player, // 博士阵营（干员）
    Enemy, // 敌人阵营（整合运动）
    Hostile, // 中立敌对（源石虫等）
}
