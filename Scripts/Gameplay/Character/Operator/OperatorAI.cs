using Godot;
using OutpostProtocol.Gameplay.Entity;

namespace OutpostProtocol.Gameplay.Character.Operator;

/// <summary>
/// 干员 AI 控制器
/// 职责：状态切换、目标选择、行为协调
/// </summary>
public partial class OperatorAI : Node
{
    // ============================================================
    // 导出变量
    // ============================================================

    [ExportGroup("AI 参数")]
    [Export] public float ChaseRange = 200.0f; // 追击范围
    [Export] public float AttackRangeOffset = 10.0f; // 攻击范围偏移（避免贴脸）
    [Export] public float IdleCheckInterval = 0.5f; // 空闲时检测间隔

    // ============================================================
    // 运行时状态
    // ============================================================

    private Operator _operator;
    private BaseEntity _currentTarget;
    private float _idleTimer;
    private float _followTimer;

    // ============================================================
    // 生命周期
    // ============================================================

    public override void _Ready()
    {
        _operator = GetParent<Operator>();
        if (_operator == null)
        {
            GD.PushError("[OperatorAI] 必须挂载在 Operator 下");
            return;
        }

        if (_operator.Attack != null)
        {
            _operator.Attack.TargetChanged += OnTargetChanged;
        }
    }

    public override void _ExitTree()
    {
        if (_operator != null && _operator.Attack != null)
        {
            _operator.Attack.TargetChanged -= OnTargetChanged;
        }
    }

    public override void _Process(double delta)
    {
        if (_operator == null || _operator.IsDead) return;

        float dt = (float)delta;

        // 根据状态执行行为
        switch (_operator.State)
        {
            case OperatorState.Idle:
                HandleIdle(dt);
                break;

            case OperatorState.Following:
                HandleFollowing(dt);
                break;

            case OperatorState.Moving:
                HandleMoving();
                break;

            case OperatorState.Attacking:
                HandleAttacking();
                break;

            case OperatorState.Down:
                // 不处理任何行为
                break;
        }
    }

    // ============================================================
    // 状态处理
    // ============================================================

    private void HandleIdle(float dt)
    {
        _idleTimer += dt;

        if (_idleTimer < IdleCheckInterval) return;
        _idleTimer = 0;

        // 尝试自动索敌（如果开启自动攻击）
        if (_operator.Attack != null && _operator.Attack.AutoAttack)
        {
            var target = _operator.Attack.FindNearestTarget();
            if (target != null)
            {
                _operator.AttackTarget(target);
            }
        }
    }

    private void HandleFollowing(float dt)
    {
        // 周期性刷新跟随位置，避免每帧重复异步寻路
        _followTimer += dt;
        if (_followTimer >= 0.3f)
        {
            _followTimer = 0;
            _operator.UpdateFollowPosition();
        }
    }

    private void HandleMoving()
    {
        // 到达目标位置后切换状态
        if (!_operator.IsMoving)
        {
            _operator.State = OperatorState.Idle;
            GD.Print($"[{_operator.EntityName}] 到达目标位置");
        }
    }

    private void HandleAttacking()
    {
        if (_operator.Attack == null) return;

        // 检查目标是否有效
        var target = _operator.Attack.CurrentTarget;
        if (target == null || target.IsDead)
        {
            _operator.StopCommand();
            return;
        }

        // 检查目标是否在追击范围内
        float dist = _operator.GlobalPosition.DistanceTo(target.GlobalPosition);
        if (dist > ChaseRange)
        {
            // 目标丢失，停止攻击
            _operator.StopCommand();
            GD.Print($"[{_operator.EntityName}] 目标丢失，停止追击");
            return;
        }

        // 超出攻击范围则靠近，进入射程后停下开火
        if (dist > _operator.Attack.AttackRange - AttackRangeOffset)
        {
            _operator.MoveTo(target.GlobalPosition);
        }
        else
        {
            _operator.StopMoving();
        }
    }

    // ============================================================
    // 事件回调
    // ============================================================

    private void OnTargetChanged(BaseEntity target)
    {
        if (target != null)
        {
            _currentTarget = target;
            _operator.State = OperatorState.Attacking;
        }
        else
        {
            _currentTarget = null;
            if (_operator.State == OperatorState.Attacking)
            {
                _operator.State = OperatorState.Idle;
            }
        }
    }
}
