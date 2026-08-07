using Godot;
using OutpostProtocol.Core;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Core.Grid;
using OutpostProtocol.Gameplay.Character.Operator;
using OutpostProtocol.Gameplay.Entity;
using OutpostProtocol.Gameplay.Inventory;
using OutpostProtocol.Managers;
using System.Collections.Generic;

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

    [ExportGroup("指挥参数")]
    [Export] public float CommandRange = 500.0f; // 指挥范围
    [Export] public float AttackCommandRange = 400.0f; // 攻击指挥范围

    [ExportGroup("交互配置")]
    [Export] public float InteractionRange = 60.0f; // E 键交互范围

    [ExportGroup("世界边界")]
    [Export] public Vector2 MapBounds = new(3200, 3200); // 地图边界（世界坐标），由 GameWorld 设置

    // ============================================================
    // 运行时状态
    // ============================================================

    private float _currentHealth;
    private float _currentStamina;
    private Vector2 _velocity = Vector2.Zero;
    private bool _isSprinting;
    private bool _isDead;
    private bool _isBoxSelecting;
    private Vector2 _selectionStartScreen;
    private readonly List<OutpostProtocol.Gameplay.Character.Operator.Operator> _selectedOperators = new();
    private CanvasLayer _selectionLayer;
    private ColorRect _selectionBox;

    // ============================================================
    // 组件引用
    // ============================================================

    private Timer _staminaRegenTimer;
    private Sprite2D _sprite;
    private OutpostProtocol.Gameplay.Character.Operator.Operator _selectedOperator;

    /// <summary>背包组件</summary>
    public Backpack Backpack { get; private set; }

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
    public IReadOnlyList<OutpostProtocol.Gameplay.Character.Operator.Operator> SelectedOperators => _selectedOperators;

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
        Backpack = GetNodeOrNull<Backpack>("Backpack");

        // 框选辅助层（屏幕坐标）
        _selectionLayer = new CanvasLayer { Layer = 40 };
        AddChild(_selectionLayer);
        _selectionBox = new ColorRect
        {
            Color = new Color(0.3f, 0.7f, 1f, 0.18f),
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _selectionLayer.AddChild(_selectionBox);

        // 加入博士组，供采集点/塔/生成器查询
        AddToGroup("doctor");

        // 连接信号
        _staminaRegenTimer.Timeout += OnStaminaRegenTick;

        // 订阅 EventBus
        EventBus.Instance.DoctorDied += OnDoctorDied;
        EventBus.Instance.EntityDied += OnEntityDied;

        GD.Print($"[Doctor] 初始化完成 — HP:{_currentHealth}/{MaxHealth}, 体力:{_currentStamina}/{MaxStamina}");
        EmitHealthChanged();
        EmitStaminaChanged();
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.DoctorDied -= OnDoctorDied;
            EventBus.Instance.EntityDied -= OnEntityDied;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_isDead || InputLock.IsLocked) return;

        // 1. 处理输入
        HandleInput();

        // 2. 更新朝向
        UpdateFacingDirection();

        // 3. 移动 + 墙体碰撞（按网格可行走判定，与干员一致）
        ApplyGridMovement();

        // 4. 限制在地图边界内
        GlobalPosition = new Vector2(
            Mathf.Clamp(GlobalPosition.X, 0, MapBounds.X),
            Mathf.Clamp(GlobalPosition.Y, 0, MapBounds.Y)
        );

        // 4. 体力逻辑由 StaminaRegenTimer 驱动
    }

    public override void _Process(double delta)
    {
        // 框选时更新选择框（屏幕坐标）
        if (!_isBoxSelecting || _selectionBox == null) return;

        Vector2 current = GetViewport().GetMousePosition();
        Vector2 topLeft = new(
            Mathf.Min(_selectionStartScreen.X, current.X),
            Mathf.Min(_selectionStartScreen.Y, current.Y)
        );
        Vector2 size = new(
            Mathf.Abs(current.X - _selectionStartScreen.X),
            Mathf.Abs(current.Y - _selectionStartScreen.Y)
        );
        _selectionBox.Position = topLeft;
        _selectionBox.Size = size;
        _selectionBox.Visible = size.Length() > 1f;
    }

    public override void _Input(InputEvent @event)
    {
        if (_isDead || InputLock.IsLocked) return;

        // 建设期：点击由 TowerBuilder 处理（选塔/放置/取消），博士不消费鼠标事件
        if (GameManager.Instance?.CurrentState == GameState.Build &&
            @event is InputEventMouseButton)
        {
            return;
        }

        if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Right)
        {
            if (GetViewport().GuiGetHoveredControl() != null) return;
            HandleRightClick(mouseEvent);
            return;
        }

        if (@event is InputEventMouseButton leftDown && leftDown.Pressed && leftDown.ButtonIndex == MouseButton.Left)
        {
            if (GetViewport().GuiGetHoveredControl() != null) return;
            _isBoxSelecting = true;
            _selectionStartScreen = GetViewport().GetMousePosition();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is InputEventMouseButton leftUp && !leftUp.Pressed && leftUp.ButtonIndex == MouseButton.Left && _isBoxSelecting)
        {
            _isBoxSelecting = false;
            if (_selectionBox != null) _selectionBox.Visible = false;
            FinishSelection();
            GetViewport().SetInputAsHandled();
            return;
        }

        // 滚轮缩放地图（与物品栏滚轮切换不冲突：物品栏改用数字键）
        if (@event is InputEventMouseButton wheel && wheel.Pressed)
        {
            if (wheel.ButtonIndex == MouseButton.WheelUp)
            {
                ZoomCamera(1.12f);
            }
            else if (wheel.ButtonIndex == MouseButton.WheelDown)
            {
                ZoomCamera(0.9f);
            }
            return;
        }

        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            if (keyEvent.IsActionPressed("interact"))
            {
                TryInteract();
            }
            else if (keyEvent.IsActionPressed("select_next"))
            {
                CycleOperator();
            }
            else
            {
                switch (keyEvent.Keycode)
                {
                    case Key.F1:
                    TryCastSkill(1);
                    break;
                    case Key.F2:
                    TryCastSkill(2);
                    break;
                    case Key.F3:
                    TryCastSkill(3);
                    break;
                    case Key.F4:
                    TryCastSkill(4);
                    break;
                }
            }
        }
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

    /// <summary>
    /// 网格碰撞移动：按轴尝试移动，目标格不可行走则挡住（与干员寻路一致）
    /// </summary>
    private void ApplyGridMovement()
    {
        if (Velocity == Vector2.Zero) return;

        float dt = (float)GetPhysicsProcessDeltaTime();
        var grid = GridManager.Instance;
        if (grid == null || !grid.IsBuilt)
        {
            GlobalPosition += Velocity * dt;
            return;
        }

        // 脱困保护：当前位置不在可行走格上时（如建筑状态变化导致），允许自由移动直到回到路面
        if (!IsWalkableAt(grid, GlobalPosition))
        {
            GlobalPosition += Velocity * dt;
            return;
        }

        Vector2 pos = GlobalPosition;

        // X 轴
        Vector2 candidateX = pos + new Vector2(Velocity.X * dt, 0);
        if (IsWalkableAt(grid, candidateX)) pos = candidateX;

        // Y 轴
        Vector2 candidateY = pos + new Vector2(0, Velocity.Y * dt);
        if (IsWalkableAt(grid, candidateY)) pos = candidateY;

        GlobalPosition = pos;
    }

    private static bool IsWalkableAt(GridManager grid, Vector2 worldPos)
    {
        return grid.IsWalkable(grid.WorldToGrid(worldPos));
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

        EmitStaminaChanged();
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
        EmitHealthChanged();

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
        EmitHealthChanged();
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

    // ============================================================
    // 序列化支持
    // ============================================================

    /// <summary>设置生命值（用于恢复存档）</summary>
    public void SetHealth(float health)
    {
        _currentHealth = Mathf.Clamp(health, 0, MaxHealth);
        EmitHealthChanged();
    }

    /// <summary>设置体力值（用于恢复存档）</summary>
    public void SetStamina(float stamina)
    {
        _currentStamina = Mathf.Clamp(stamina, 0, MaxStamina);
        EmitStaminaChanged();
    }

    /// <summary>消耗体力，成功返回 true</summary>
    public bool SpendStamina(float amount)
    {
        if (_currentStamina < amount) return false;
        _currentStamina -= amount;
        EmitStaminaChanged();
        return true;
    }

    // ============================================================
    // 交互（E 键）
    // ============================================================

    /// <summary>寻找最近的掉落物并拾取（E 键）</summary>
    public void TryInteract()
    {
        LootItem nearest = null;
        float minDist = float.MaxValue;

        foreach (var node in GetTree().GetNodesInGroup("loot_items"))
        {
            if (node is LootItem loot && !loot.IsPickedUp)
            {
                float dist = GlobalPosition.DistanceTo(loot.GlobalPosition);
                if (dist < InteractionRange && dist < minDist)
                {
                    minDist = dist;
                    nearest = loot;
                }
            }
        }

        if (nearest != null)
        {
            nearest.ForcePickup(this);
            GD.Print("[Doctor] 按 E 拾取物品");
        }
    }

    /// <summary>从存档数据恢复位置</summary>
    public void RestorePosition(Vector2 position)
    {
        GlobalPosition = position;
    }

    // ============================================================
    // 战斗奖励
    // ============================================================

    /// <summary>处理击杀事件，分配经验</summary>
    public void HandleEnemyKilled(OutpostProtocol.Gameplay.Character.Enemy.Enemy enemy, BaseEntity killer)
    {
        if (enemy == null) return;

        // 检查击杀者是否是干员
        if (killer is OutpostProtocol.Gameplay.Character.Operator.Operator op)
        {
            // 干员获得经验
            op.AddExp(enemy.ExpReward);
            GD.Print($"[Doctor] {op.EntityName} 击杀敌人，获得 {enemy.ExpReward} 经验");
        }
    }

    private void OnEntityDied(Node2D entity, Node2D killer)
    {
        if (entity is OutpostProtocol.Gameplay.Character.Enemy.Enemy enemy && killer is BaseEntity killerEntity)
        {
            HandleEnemyKilled(enemy, killerEntity);
        }
    }

    private void EmitHealthChanged()
    {
        EventBus.Instance?.EmitDoctorHealthChanged(_currentHealth, MaxHealth);
    }

    private void EmitStaminaChanged()
    {
        EventBus.Instance?.EmitDoctorStaminaChanged(_currentStamina, MaxStamina);
    }

    // ============================================================
    // 技能输入
    // ============================================================

    /// <summary>设置当前选中的干员</summary>
    public void SelectOperator(OutpostProtocol.Gameplay.Character.Operator.Operator op)
    {
        _selectedOperators.Clear();
        if (op != null && !op.IsDead && op.State != OperatorState.Down)
        {
            _selectedOperators.Add(op);
        }
        _selectedOperator = op;
        EventBus.Instance.EmitSelectedOperatorChanged(op);
        GD.Print($"[Doctor] 选中干员: {op?.EntityName ?? "无"}（共 {_selectedOperators.Count} 个）");
    }

    /// <summary>批量选中干员（框选）</summary>
    public void SelectOperators(IEnumerable<OutpostProtocol.Gameplay.Character.Operator.Operator> operators)
    {
        _selectedOperators.Clear();
        foreach (var op in operators)
        {
            if (op == null || op.IsDead || op.State == OperatorState.Down) continue;
            if (!_selectedOperators.Contains(op)) _selectedOperators.Add(op);
        }

        _selectedOperator = _selectedOperators.Count > 0 ? _selectedOperators[^1] : null;
        EventBus.Instance.EmitSelectedOperatorChanged(_selectedOperator);
        GD.Print($"[Doctor] 框选干员: {_selectedOperators.Count} 个");
    }

    /// <summary>框选结束：小距离视为点击，否则按区域选择</summary>
    private void FinishSelection()
    {
        Vector2 startWorld = ScreenToWorld(_selectionStartScreen);
        Vector2 endWorld = ScreenToWorld(GetViewport().GetMousePosition());

        if (startWorld.DistanceTo(endWorld) < 10f)
        {
            SelectOperatorAtWorld(startWorld);
            return;
        }

        var rect = new Rect2(
            new Vector2(Mathf.Min(startWorld.X, endWorld.X), Mathf.Min(startWorld.Y, endWorld.Y)),
            new Vector2(Mathf.Abs(endWorld.X - startWorld.X), Mathf.Abs(endWorld.Y - startWorld.Y))
        );

        var selected = new List<OutpostProtocol.Gameplay.Character.Operator.Operator>();
        foreach (var node in GetTree().GetNodesInGroup("operators"))
        {
            if (node is OutpostProtocol.Gameplay.Character.Operator.Operator op &&
                !op.IsDead &&
                op.State != OperatorState.Down &&
                rect.HasPoint(op.GlobalPosition))
            {
                selected.Add(op);
            }
        }

        SelectOperators(selected);
    }

    /// <summary>点击世界坐标处的干员进行选择</summary>
    private void SelectOperatorAtWorld(Vector2 worldPos)
    {
        var space = GetWorld2D().DirectSpaceState;
        var query = new PhysicsPointQueryParameters2D
        {
            Position = worldPos,
            CollisionMask = 1u,
        };

        var results = space.IntersectPoint(query);
        foreach (var result in results)
        {
            var collider = result["collider"].As<GodotObject>();
            if (collider is OutpostProtocol.Gameplay.Character.Operator.Operator op)
            {
                SelectOperator(op);
                GD.Print($"[Doctor] 点击选择干员: {op.EntityName}");
                return;
            }
        }

        // 扩大命中判定：点击头像附近（48px）也能选中，避免小碰撞圆导致点不中
        OutpostProtocol.Gameplay.Character.Operator.Operator nearest = null;
        float bestDist = 48f;
        foreach (var node in GetTree().GetNodesInGroup("operators"))
        {
            if (node is not OutpostProtocol.Gameplay.Character.Operator.Operator op ||
                op.IsDead ||
                op.State == OperatorState.Down)
            {
                continue;
            }
            float dist = op.GlobalPosition.DistanceTo(worldPos);
            if (dist < bestDist)
            {
                bestDist = dist;
                nearest = op;
            }
        }
        if (nearest != null)
        {
            SelectOperator(nearest);
            GD.Print($"[Doctor] 头像附近选择干员: {nearest.EntityName}");
            return;
        }

        // 点击空地：有选中干员时直接指挥移动；否则清空选择
        if (_selectedOperators.Count > 0)
        {
            CommandMoveTo(worldPos);
            GD.Print($"[Doctor] 点击地面移动选中干员 → ({worldPos.X:F0}, {worldPos.Y:F0})");
        }
        else if (_selectedOperator != null)
        {
            SelectOperators(new List<OutpostProtocol.Gameplay.Character.Operator.Operator>());
        }
    }

    /// <summary>屏幕坐标 → 世界坐标</summary>
    private Vector2 ScreenToWorld(Vector2 screenPos)
    {
        var viewport = GetViewport();
        var camera = viewport.GetCamera2D();
        if (camera == null) return GlobalPosition;
        return camera.GlobalPosition + (screenPos - viewport.GetVisibleRect().Size * 0.5f) / camera.Zoom;
    }

    /// <summary>缩放主相机（跟随博士）</summary>
    private void ZoomCamera(float factor)
    {
        var camera = GetNodeOrNull<Camera2D>("Camera2D");
        if (camera == null) return;

        float newZoom = Mathf.Clamp(camera.Zoom.X * factor, 0.45f, 2.5f);
        camera.Zoom = new Vector2(newZoom, newZoom);
    }

    /// <summary>轮询切换选中干员（Tab）</summary>
    public void CycleOperator()
    {
        var operators = GetOperatorsInRange(1000f);
        if (operators.Count == 0) return;

        int currentIndex = operators.IndexOf(_selectedOperator);
        int nextIndex = (currentIndex + 1) % operators.Count;
        SelectOperator(operators[nextIndex]);
    }

    /// <summary>释放当前选中干员的指定槽位技能</summary>
    public bool TryCastSkill(int slot)
    {
        if (_selectedOperator == null || _selectedOperator.IsDead)
        {
            GD.Print("[Doctor] 未选中干员或干员已死亡");
            return false;
        }

        var skillComp = _selectedOperator.Skill;
        if (skillComp == null)
        {
            GD.Print("[Doctor] 该干员没有技能组件");
            return false;
        }

        var skill = skillComp.GetSkill(slot);
        if (skill == null)
        {
            GD.Print($"[Doctor] 槽位 F{slot} 未绑定技能");
            return false;
        }

        if (!skillComp.IsSkillReady(slot))
        {
            GD.Print($"[Doctor] 技能 F{slot} 不可用");
            return false;
        }

        // 消耗博士体力
        if (skill.StaminaCost > 0 && !SpendStamina(skill.StaminaCost))
        {
            GD.Print($"[Doctor] 体力不足，无法释放 {skill.Name}");
            return false;
        }

        bool success = skillComp.CastSkill(slot);
        if (success)
        {
            GD.Print($"[Doctor] 释放技能 F{slot} — {skill.Name}");
        }
        return success;
    }

    // ============================================================
    // 干员指挥
    // ============================================================

    /// <summary>获取指挥范围内的可用干员</summary>
    // 注意：类名 Operator 与命名空间 Operator 同名，且当前处于兄弟命名空间 Doctor 下，
    // 直接写 Operator 会被解析为命名空间，因此这里使用完全限定名。
    private List<OutpostProtocol.Gameplay.Character.Operator.Operator> GetOperatorsInRange(float range)
    {
        var result = new List<OutpostProtocol.Gameplay.Character.Operator.Operator>();

        foreach (var node in GetTree().GetNodesInGroup("operators"))
        {
            if (node is OutpostProtocol.Gameplay.Character.Operator.Operator op &&
                !op.IsDead &&
                op.State != OperatorState.Down &&
                GlobalPosition.DistanceTo(op.GlobalPosition) <= range)
            {
                result.Add(op);
            }
        }

        return result;
    }

    /// <summary>指挥范围内的干员移动到目标位置</summary>
    public void CommandMoveTo(Vector2 targetPos)
    {
        var operators = GetCommandTargets(CommandRange);
        foreach (var op in operators)
        {
            op.MoveToPosition(targetPos);
        }
    }

    /// <summary>指挥范围内的干员攻击目标</summary>
    public void CommandAttack(BaseEntity target)
    {
        var operators = GetCommandTargets(AttackCommandRange);
        foreach (var op in operators)
        {
            op.AttackTarget(target);
        }
    }

    /// <summary>
    /// 指令目标：优先使用已选中的干员；未选中时回退到范围内的所有干员
    /// </summary>
    private List<OutpostProtocol.Gameplay.Character.Operator.Operator> GetCommandTargets(float fallbackRange)
    {
        if (_selectedOperators.Count > 0)
        {
            return new List<OutpostProtocol.Gameplay.Character.Operator.Operator>(_selectedOperators);
        }
        return GetOperatorsInRange(fallbackRange);
    }

    private void HandleRightClick(InputEventMouseButton mouseEvent)
    {
        Vector2 worldPos = GetGlobalMousePosition();

        // 检测鼠标下是否有可攻击目标（敌人层 layer 2）
        var space = GetWorld2D().DirectSpaceState;
        var query = new PhysicsPointQueryParameters2D
        {
            Position = worldPos,
            CollisionMask = 2u,
        };

        var results = space.IntersectPoint(query);
        if (results.Count > 0)
        {
            var collider = results[0]["collider"].As<GodotObject>();
            if (collider is BaseEntity entity && entity.Faction == FactionType.Enemy)
            {
                CommandAttack(entity);
                return;
            }
        }

        // 点击地面 → 移动指令
        CommandMoveTo(worldPos);
    }
}
