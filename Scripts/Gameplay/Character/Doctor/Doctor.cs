using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Managers;

namespace OutpostProtocol.Gameplay.Character.Doctor;

/// <summary>
/// 博士（玩家角色）
/// 核心设计：WASD 移动、体力系统、无法攻击、死亡即删档
/// </summary>
public partial class Doctor : CharacterBody2D
{
    // ============================================================
    // 导出变量（可在编辑器中调整）
    // ============================================================

    [ExportGroup("移动参数")]
    [Export] public float WalkSpeed = 150.0f;
    [Export] public float SprintSpeed = 300.0f;
    [Export] public float Acceleration = 800.0f;
    [Export] public float Friction = 600.0f;

    [ExportGroup("体力参数")]
    [Export] public float MaxStamina = 100.0f;
    [Export] public float StaminaDrainRate = 30.0f; // 每秒消耗
    [Export] public float StaminaRegenRate = 15.0f; // 每秒恢复
    [Export] public float MinStaminaToSprint = 10.0f; // 低于此值无法冲刺

    [ExportGroup("生命参数")]
    [Export] public float MaxHealth = 100.0f;

    // ============================================================
    // 运行时状态
    // ============================================================

    private float _currentHealth;
    private float _currentStamina;
    private Vector2 _velocity = Vector2.Zero;
    private bool _isSprinting;
    private bool _isDead;

    // ============================================================
    // 组件引用
    // ============================================================

    private Timer _staminaRegenTimer;
    private Sprite2D _sprite;

    // ============================================================
    // 公共属性
    // ============================================================

    public float CurrentHealth => _currentHealth;
    public float CurrentStamina => _currentStamina;
    public float MaxHealthValue => MaxHealth;
    public float MaxStaminaValue => MaxStamina;
    public bool IsSprinting => _isSprinting;
    public bool IsDead => _isDead;
    public Vector2 FacingDirection { get; private set; } = Vector2.Down;

    // ============================================================
    // 生命周期
    // ============================================================

    public override void _Ready()
    {
        // 初始化状态
        _currentHealth = MaxHealth;
        _currentStamina = MaxStamina;

        // 获取组件引用
        _staminaRegenTimer = GetNode<Timer>("StaminaRegenTimer");
        _sprite = GetNode<Sprite2D>("Sprite2D");

        // 连接信号
        _staminaRegenTimer.Timeout += OnStaminaRegenTick;

        // 订阅 EventBus
        EventBus.Instance.DoctorDied += OnDoctorDied;

        GD.Print($"[Doctor] 初始化完成 — HP:{_currentHealth}/{MaxHealth}, 体力:{_currentStamina}/{MaxStamina}");
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.DoctorDied -= OnDoctorDied;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_isDead) return;

        // 1. 处理输入
        HandleInput();

        // 2. 更新朝向
        UpdateFacingDirection();

        // 3. 移动并碰撞检测
        MoveAndSlide();

        // 4. 体力逻辑由 StaminaRegenTimer 驱动
    }

    // ============================================================
    // 输入处理
    // ============================================================

    private void HandleInput()
    {
        // 获取方向输入
        Vector2 inputDirection = Input.GetVector(
            "move_left", "move_right",
            "move_up", "move_down"
        );

        // 归一化，防止斜向速度过快
        if (inputDirection.Length() > 1.0f)
        {
            inputDirection = inputDirection.Normalized();
        }

        // 判断是否冲刺
        bool sprintPressed = Input.IsActionPressed("sprint");
        bool canSprint = _currentStamina > MinStaminaToSprint && sprintPressed && inputDirection != Vector2.Zero;

        // 更新冲刺状态
        _isSprinting = canSprint;

        // 计算目标速度
        float targetSpeed = _isSprinting ? SprintSpeed : WalkSpeed;
        Vector2 targetVelocity = inputDirection * targetSpeed;

        // 平滑加速/减速
        if (inputDirection != Vector2.Zero)
        {
            _velocity = _velocity.MoveToward(targetVelocity, Acceleration * (float)GetPhysicsProcessDeltaTime());
        }
        else
        {
            _velocity = _velocity.MoveToward(Vector2.Zero, Friction * (float)GetPhysicsProcessDeltaTime());
        }

        // 应用速度
        Velocity = _velocity;
    }

    // ============================================================
    // 体力系统（由 Timer 驱动）
    // ============================================================

    private void OnStaminaRegenTick()
    {
        if (_isDead) return;

        if (_isSprinting && Velocity.Length() > 0.1f)
        {
            // 冲刺中：消耗体力（Timer 每 0.1 秒触发）
            _currentStamina -= StaminaDrainRate * 0.1f;
            if (_currentStamina < 0) _currentStamina = 0;

            // 体力耗尽自动停止冲刺
            if (_currentStamina <= MinStaminaToSprint)
            {
                _isSprinting = false;
            }
        }
        else
        {
            // 非冲刺：恢复体力
            _currentStamina += StaminaRegenRate * 0.1f;
            if (_currentStamina > MaxStamina) _currentStamina = MaxStamina;
        }
    }

    // ============================================================
    // 朝向逻辑
    // ============================================================

    private void UpdateFacingDirection()
    {
        if (Velocity.Length() > 10f)
        {
            FacingDirection = Velocity.Normalized();
        }

        // 翻转精灵（如果朝左）
        if (_sprite != null)
        {
            _sprite.FlipH = FacingDirection.X < 0;
        }
    }

    // ============================================================
    // 战斗相关
    // ============================================================

    /// <summary>博士受伤</summary>
    public void TakeDamage(float damage)
    {
        if (_isDead) return;

        _currentHealth -= damage;
        if (_currentHealth < 0) _currentHealth = 0;

        GD.Print($"[Doctor] 受到 {damage} 点伤害，剩余 HP:{_currentHealth}");

        // 广播伤害事件
        EventBus.Instance.EmitEntityDamaged(this, (int)damage);

        if (_currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>博士死亡</summary>
    private void Die()
    {
        if (_isDead) return;
        _isDead = true;
        GD.Print("[Doctor] 博士死亡");

        // 通知 GameManager 触发游戏结束
        GameManager.Instance.GameOver();
    }

    // ============================================================
    // 事件回调
    // ============================================================

    private void OnDoctorDied()
    {
        // 游戏结束时的清理逻辑
        _isDead = true;
        Velocity = Vector2.Zero;
    }

    // ============================================================
    // 公共 API
    // ============================================================

    /// <summary>恢复博士生命值</summary>
    public void Heal(float amount)
    {
        if (_isDead) return;
        _currentHealth = Mathf.Min(_currentHealth + amount, MaxHealth);
        GD.Print($"[Doctor] 恢复 {amount} HP，当前 HP:{_currentHealth}");
    }

    /// <summary>恢复博士体力值</summary>
    public void RestoreStamina(float amount)
    {
        _currentStamina = Mathf.Min(_currentStamina + amount, MaxStamina);
    }

    /// <summary>获取移动速度百分比（用于 UI 显示）</summary>
    public float GetSpeedPercent()
    {
        if (Velocity.Length() < 0.1f) return 0f;
        float maxSpeed = _isSprinting ? SprintSpeed : WalkSpeed;
        return Velocity.Length() / maxSpeed;
    }
}
