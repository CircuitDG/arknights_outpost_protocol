using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Managers;
using System;

namespace OutpostProtocol.Gameplay.Building;

/// <summary>
/// 前哨站核心
/// 职责：管理核心血量、受损/摧毁事件、修复
/// </summary>
public partial class OutpostCore : Node2D
{
    // ============================================================
    // 导出变量
    // ============================================================

    [ExportGroup("核心配置")]
    [Export] public int MaxHealth = 100;
    [Export] public int CurrentHealth { get; set; }

    [ExportGroup("受损配置")]
    [Export] public int EnemyDamage = 10; // 每个敌人造成的伤害
    [Export] public float DamageCooldown = 1.0f; // 伤害冷却

    [ExportGroup("视觉")]
    [Export] public Color HealthyColor = Colors.Green;
    [Export] public Color DamagedColor = Colors.Yellow;
    [Export] public Color CriticalColor = Colors.Red;

    // ============================================================
    // 组件引用
    // ============================================================

    private ProgressBar _healthBar;
    private Sprite2D _coreSprite;

    // ============================================================
    // 运行时状态
    // ============================================================

    private float _damageCooldownTimer;
    private bool _isDestroyed;

    // ============================================================
    // 事件
    // ============================================================

    public event Action<int> OnDamaged; // 参数：当前血量
    public event Action OnDestroyed;

    // ============================================================
    // 公共属性
    // ============================================================

    public bool IsDestroyed => _isDestroyed;
    public float HealthPercent => MaxHealth > 0 ? (float)CurrentHealth / MaxHealth : 0f;

    // ============================================================
    // 生命周期
    // ============================================================

    public override void _Ready()
    {
        _healthBar = GetNodeOrNull<ProgressBar>("HealthBar");
        _coreSprite = GetNodeOrNull<Sprite2D>("CoreSprite");

        CurrentHealth = MaxHealth;
        UpdateHealthBar();
        UpdateSpriteColor();

        AddToGroup("outpost_core");
        GD.Print($"[OutpostCore] 初始化完成 — HP:{CurrentHealth}/{MaxHealth}");
    }

    public override void _Process(double delta)
    {
        if (_damageCooldownTimer > 0)
        {
            _damageCooldownTimer -= (float)delta;
        }
    }

    // ============================================================
    // 核心 API
    // ============================================================

    /// <summary>受到伤害（由敌人到达时调用）</summary>
    public void TakeDamage(int damage)
    {
        if (_isDestroyed) return;
        if (_damageCooldownTimer > 0) return;

        CurrentHealth = Math.Max(0, CurrentHealth - damage);
        _damageCooldownTimer = DamageCooldown;

        GD.Print($"[OutpostCore] 受到 {damage} 点伤害，剩余 HP:{CurrentHealth}/{MaxHealth}");
        UpdateHealthBar();
        UpdateSpriteColor();

        EventBus.Instance.EmitLogMessage($"核心受损! HP: {CurrentHealth}/{MaxHealth}", "WARN");
        EventBus.Instance.EmitCoreDamaged(CurrentHealth, damage);
        OnDamaged?.Invoke(CurrentHealth);

        if (CurrentHealth <= 0)
        {
            DestroyCore();
        }
    }

    /// <summary>修复核心，返回实际修复量</summary>
    public int Repair(int amount)
    {
        if (_isDestroyed)
        {
            GD.Print("[OutpostCore] 核心已摧毁，无法修复");
            return 0;
        }

        int oldHealth = CurrentHealth;
        CurrentHealth = Math.Min(MaxHealth, CurrentHealth + amount);
        int actualRepair = CurrentHealth - oldHealth;

        if (actualRepair > 0)
        {
            UpdateHealthBar();
            UpdateSpriteColor();
            GD.Print($"[OutpostCore] 修复 +{actualRepair} HP，当前 HP:{CurrentHealth}/{MaxHealth}");
            EventBus.Instance.EmitLogMessage($"核心修复 +{actualRepair} HP", "INFO");
            EventBus.Instance.EmitCoreRepaired(actualRepair);
        }

        return actualRepair;
    }

    /// <summary>完全修复（新的一天开始时调用）</summary>
    public void FullRepair()
    {
        if (_isDestroyed) return;

        int repairAmount = MaxHealth - CurrentHealth;
        if (repairAmount > 0)
        {
            CurrentHealth = MaxHealth;
            UpdateHealthBar();
            UpdateSpriteColor();
            EventBus.Instance.EmitCoreRepaired(repairAmount);
            GD.Print($"[OutpostCore] 完全修复 — HP:{CurrentHealth}/{MaxHealth}");
        }
    }

    // ============================================================
    // 摧毁逻辑
    // ============================================================

    private void DestroyCore()
    {
        if (_isDestroyed) return;

        _isDestroyed = true;
        CurrentHealth = 0;
        UpdateHealthBar();

        GD.Print("[OutpostCore] 核心被摧毁！");
        OnDestroyed?.Invoke();
        EventBus.Instance.EmitLogMessage("前哨站核心被摧毁！游戏结束", "ERROR");

        GameManager.Instance?.GameOverWithReason(GameOverReason.CoreDestroyed);
    }

    // ============================================================
    // 视觉更新
    // ============================================================

    private void UpdateHealthBar()
    {
        if (_healthBar == null) return;

        _healthBar.MaxValue = MaxHealth;
        _healthBar.Value = CurrentHealth;
        _healthBar.Modulate = GetHealthColor();
    }

    private void UpdateSpriteColor()
    {
        if (_coreSprite == null) return;
        _coreSprite.Modulate = GetHealthColor();
    }

    private Color GetHealthColor()
    {
        float percent = HealthPercent;
        if (percent > 0.5f) return HealthyColor;
        if (percent > 0.25f) return DamagedColor;
        return CriticalColor;
    }
}

/// <summary>游戏结束原因</summary>
public enum GameOverReason
{
    DoctorDied,
    CoreDestroyed,
    ResourceStarvation,
}
