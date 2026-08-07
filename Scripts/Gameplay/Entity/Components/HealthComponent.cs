using Godot;
using System;

namespace OutpostProtocol.Gameplay.Entity.Components;

/// <summary>
/// 血量组件
/// 职责：管理生命值、受伤、治疗、死亡触发
/// </summary>
public partial class HealthComponent : Node
{
    // ============================================================
    // 导出变量
    // ============================================================

    [ExportGroup("血量参数")]
    [Export] public int MaxHealth = 100;
    [Export] public int CurrentHealth { get; set; }

    [ExportGroup("死亡配置")]
    [Export] public bool IsInvincible; // 是否无敌（用于剧情）
    [Export] public bool AutoRevive; // 是否自动复活

    // ============================================================
    // 事件
    // ============================================================

    public event Action HealthChanged; // 血量变化
    public event Action HealthDepleted; // 血量归零（死亡）
    public event Action<int> DamageTaken; // 受到伤害（参数：伤害值）
    public event Action<int> Healed; // 受到治疗（参数：治疗值）

    // ============================================================
    // 生命周期
    // ============================================================

    public override void _Ready()
    {
        CurrentHealth = MaxHealth;
    }

    // ============================================================
    // 公共 API
    // ============================================================

    /// <summary>减少血量</summary>
    public void Reduce(int damage)
    {
        if (IsInvincible || CurrentHealth <= 0) return;

        CurrentHealth = Math.Max(0, CurrentHealth - damage);
        DamageTaken?.Invoke(damage);
        HealthChanged?.Invoke();

        if (CurrentHealth <= 0)
        {
            HealthDepleted?.Invoke();
        }
    }

    /// <summary>恢复血量</summary>
    public void Heal(int amount)
    {
        if (CurrentHealth <= 0) return;

        int oldHealth = CurrentHealth;
        CurrentHealth = Math.Min(MaxHealth, CurrentHealth + amount);
        int actualHeal = CurrentHealth - oldHealth;

        if (actualHeal > 0)
        {
            Healed?.Invoke(actualHeal);
            HealthChanged?.Invoke();
        }
    }

    /// <summary>完全恢复</summary>
    public void FullHeal()
    {
        if (CurrentHealth == MaxHealth) return;
        int healAmount = MaxHealth - CurrentHealth;
        CurrentHealth = MaxHealth;
        Healed?.Invoke(healAmount);
        HealthChanged?.Invoke();
    }

    /// <summary>重置血量（用于重生）</summary>
    public void ResetHealth()
    {
        CurrentHealth = MaxHealth;
        HealthChanged?.Invoke();
    }

    // ============================================================
    // 查询
    // ============================================================

    public bool IsFullHealth => CurrentHealth >= MaxHealth;
    public bool IsCritical => CurrentHealth <= MaxHealth * 0.2f;
    public float HealthPercent => (float)CurrentHealth / MaxHealth;
}
