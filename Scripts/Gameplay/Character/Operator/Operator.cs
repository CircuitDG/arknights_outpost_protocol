using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Data;
using OutpostProtocol.Gameplay.Entity;
using OutpostProtocol.Gameplay.Entity.Components;
using OutpostProtocol.Managers;
using OutpostProtocol.UI.Controllers;
using System;
using System.Collections.Generic;

namespace OutpostProtocol.Gameplay.Character.Operator;

/// <summary>
/// 干员（博士指挥的 AI 单位）
/// 核心设计：组合模式 + 数据驱动 + 永不永久死亡
/// </summary>
public partial class Operator : BaseEntity
{
    // ============================================================
    // 导出变量
    // ============================================================

    [ExportGroup("干员配置")]
    [Export] public int OperatorDataId = 1001;

    [ExportGroup("指挥参数")]
    [Export] public float FollowDistance = 100.0f; // 跟随博士的距离
    [Export] public float FormationRadius = 30.0f; // 阵型半径

    // ============================================================
    // 运行时状态
    // ============================================================

    private OperatorData _data;
    private int _currentLevel = 1;
    private int _currentExp;
    private int _morale = 100; // 心情值
    private OperatorState _state = OperatorState.Idle;
    private bool _dataLoadPending = true;

    private Node2D _doctor; // 博士引用（用于跟随）
    private BaseEntity _commandTarget; // 指挥目标（敌人或位置）
    private Vector2 _commandPosition; // 指挥位置

    // ============================================================
    // 公共属性
    // ============================================================

    public OperatorData Data => _data;
    public int CurrentLevel => _currentLevel;
    public int CurrentExp => _currentExp;
    public int Morale => _morale;
    public OperatorState State { get => _state; set => _state = value; }
    public bool IsFollowingDoctor => _state == OperatorState.Following;

    // ============================================================
    // 生命周期
    // ============================================================

    public override void _Ready()
    {
        base._Ready();

        // 加入干员组，供 Doctor 指挥系统查询
        AddToGroup("operators");

        // 标记阵营
        Faction = FactionType.Player;

        // 设置自动攻击目标阵营
        if (Attack != null)
        {
            Attack.TargetFaction = FactionType.Enemy;
        }

        // 订阅事件
        EventBus.Instance.OperatorDown += OnOperatorDown;
        EventBus.Instance.OperatorRevived += OnOperatorRevived;

        // DataManager 是异步加载，先尝试立即加载；未就绪则由 _Process 重试
        TryLoadOperatorData();

        if (_data != null)
        {
            GD.Print($"[{EntityName}] 初始化完成 — Lv.{_currentLevel}, HP:{Health?.CurrentHealth}/{Health?.MaxHealth}");
        }
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        EventBus.Instance.OperatorDown -= OnOperatorDown;
        EventBus.Instance.OperatorRevived -= OnOperatorRevived;
    }

    public override void _Process(double delta)
    {
        if (IsDead) return;

        // 等待数据加载完成后补初始化
        if (_dataLoadPending)
        {
            TryLoadOperatorData();
        }

        // 状态逻辑由 OperatorAI 处理
    }

    // ============================================================
    // 数据加载
    // ============================================================

    private void TryLoadOperatorData()
    {
        if (!_dataLoadPending) return;
        if (DataManager.Instance == null || !DataManager.Instance.IsLoaded) return;

        _data = DataManager.Instance.GetOperator(OperatorDataId);
        _dataLoadPending = false;

        if (_data == null)
        {
            GD.PushError($"[Operator] 未找到干员数据 ID:{OperatorDataId}");
            EntityName = $"Unknown_{OperatorDataId}";
            return;
        }

        // 天赋：初始等级加成
        _currentLevel = Math.Min(1 + TalentTreeController.OperatorStartLevelBonus, _data.MaxLevel);

        EntityName = _data.Name;
        EntityId = _data.Id;

        UpdateSprite();
        ApplyStats();
        // 天赋：攻速加成（场景默认 1.0 为基础）
        if (Attack != null)
        {
            Attack.AttackInterval = 1.0f / (1f + TalentTreeController.OperatorAttackSpeedBonus);
        }
        GD.Print($"[{EntityName}] 初始化完成 — Lv.{_currentLevel}, HP:{Health?.CurrentHealth}/{Health?.MaxHealth}");
    }

    private void ApplyStats()
    {
        if (_data == null) return;

        // 计算当前等级属性（使用成长曲线）
        int levelIndex = Math.Min(_currentLevel - 1, _data.MaxLevel - 1);

        int hp = _data.BaseHp + (_data.HpGrowth.Count > levelIndex ? _data.HpGrowth[levelIndex] : 0);
        int attack = _data.BaseAttack + (_data.AttackGrowth.Count > levelIndex ? _data.AttackGrowth[levelIndex] : 0);

        if (Health != null)
        {
            Health.MaxHealth = hp;
            Health.FullHeal();
        }

        if (Attack != null)
        {
            Attack.AttackDamage = attack;
            if (_data.ClassType == "Sniper")
            {
                Attack.AttackRange += CollectionManager.SniperRangeBonusPx;
            }
        }

        GD.Print($"[{EntityName}] 应用属性 — HP:{hp}, ATK:{attack}");
    }

    /// <summary>按干员数据切换头像</summary>
    private void UpdateSprite()
    {
        var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        if (sprite == null) return;

        string path = GetAvatarPath(OperatorDataId);

        var tex = GD.Load<Texture2D>(path);
        if (tex != null)
        {
            sprite.Texture = tex;
            sprite.Scale = new Vector2(0.28f, 0.28f);
        }
    }

    /// <summary>按干员数据 ID 获取头像素材路径（供地图/卡牌/HUD 共用）</summary>
    public static string GetAvatarPath(int operatorDataId)
    {
        return operatorDataId switch
        {
            1002 => "res://Assets/Art/Characters/op_melantha.png",
            1003 => "res://Assets/Art/Characters/op_ansel.png",
            _ => "res://Assets/Art/Characters/op_fang.png",
        };
    }

    // ============================================================
    // 指挥接口
    // ============================================================

    /// <summary>跟随博士</summary>
    public void FollowDoctor(Node2D doctor)
    {
        _doctor = doctor;
        _state = OperatorState.Following;
        _commandTarget = null;

        GD.Print($"[{EntityName}] 开始跟随博士");
        UpdateFollowPosition();
    }

    /// <summary>移动到指定位置</summary>
    public void MoveToPosition(Vector2 position)
    {
        _state = OperatorState.Moving;
        _commandPosition = position;
        _commandTarget = null;

        MoveTo(position);
        GD.Print($"[{EntityName}] 移动到 ({position.X:F0}, {position.Y:F0})");
    }

    /// <summary>攻击指定目标</summary>
    public override void AttackTarget(BaseEntity target)
    {
        if (target == null || target.IsDead)
        {
            GD.Print($"[{EntityName}] 目标无效或已死亡");
            return;
        }

        _state = OperatorState.Attacking;
        _commandTarget = target;

        // 移动到攻击范围（AttackComponent 会在进入射程后自动开火）
        MoveTo(target.GlobalPosition);
        Attack?.Attack(target);
        GD.Print($"[{EntityName}] 攻击目标: {target.EntityName}");
    }

    /// <summary>停止当前行动</summary>
    public void StopCommand()
    {
        _state = OperatorState.Idle;
        _commandTarget = null;
        StopMoving();
        StopAttacking();
        GD.Print($"[{EntityName}] 停止行动");
    }

    /// <summary>更新跟随位置（由 OperatorAI 周期性调用）</summary>
    public void UpdateFollowPosition()
    {
        if (_doctor == null || _state != OperatorState.Following) return;

        // 计算跟随位置（博士周围分散）
        Vector2 targetPos = _doctor.GlobalPosition + new Vector2(FormationRadius, 0);
        MoveTo(targetPos);
    }

    // ============================================================
    // 升级系统
    // ============================================================

    /// <summary>增加经验值</summary>
    public void AddExp(int amount)
    {
        if (_data == null || _currentLevel >= _data.MaxLevel) return;

        // 天赋：经验加成
        int gained = (int)(amount * (1f + TalentTreeController.OperatorExpBonus));
        _currentExp += gained;
        EventBus.Instance.EmitOperatorExpGained(OperatorDataId, gained);

        // 检查升级
        while (_currentLevel < _data.MaxLevel)
        {
            int requiredExp = _data.LvUpExpCurve[_currentLevel];
            if (_currentExp >= requiredExp)
            {
                _currentExp -= requiredExp;
                _currentLevel++;
                ApplyStats();
                EventBus.Instance.EmitOperatorLevelUp(this, _currentLevel);
                GD.Print($"[{EntityName}] 升级！Lv.{_currentLevel}");
            }
            else
            {
                break;
            }
        }
    }

    // ============================================================
    // 心情系统
    // ============================================================

    /// <summary>调整心情</summary>
    public void AdjustMorale(int delta)
    {
        _morale = Math.Clamp(_morale + delta, 0, 100);

        if (_morale <= 20)
        {
            // TODO: 低心情惩罚（减少攻击力/攻速）
            GD.Print($"[{EntityName}] 心情低下 ({_morale})，性能下降");
        }
    }

    // ============================================================
    // 状态查询
    // ============================================================

    public int GetExpToNextLevel()
    {
        if (_data == null || _currentLevel >= _data.MaxLevel) return 0;
        return _data.LvUpExpCurve[_currentLevel] - _currentExp;
    }

    public float GetLevelProgress()
    {
        if (_data == null || _currentLevel >= _data.MaxLevel) return 1.0f;
        int required = _data.LvUpExpCurve[_currentLevel];
        return required > 0 ? (float)_currentExp / required : 1.0f;
    }

    // ============================================================
    // 干员死亡（永不永久死亡）
    // ============================================================

    protected override void OnHealthDepleted()
    {
        // 干员不会真正死亡，进入"战斗不能"状态
        _state = OperatorState.Down;
        Movement?.Stop();
        Attack?.StopAttack();

        GD.Print($"[{EntityName}] 战斗不能！30秒内可急救或撤离");

        // 广播战斗不能事件
        EventBus.Instance.EmitOperatorDown(this);

        // 30 秒倒计时：未急救则自动撤离归队（恢复 20%，心情 -30）
        GetTree().CreateTimer(30f).Timeout += () =>
        {
            if (_state != OperatorState.Down || IsDead) return;
            EmergencyEvacuate();
        };
    }

    /// <summary>博士急救：恢复 20% 生命并脱离战斗不能</summary>
    public bool EmergencyReviveWithBandage()
    {
        if (_state != OperatorState.Down) return false;

        if (Health != null)
        {
            Health.CurrentHealth = Math.Max(1, Health.MaxHealth / 5);
        }
        _morale = Math.Max(0, _morale - 10);
        _state = OperatorState.Idle;
        GD.Print($"[{EntityName}] 博士急救成功，恢复 20% 生命");
        return true;
    }

    /// <summary>紧急撤离：自动归队（恢复 20%，心情 -30）</summary>
    public void EmergencyEvacuate()
    {
        if (_state != OperatorState.Down) return;

        if (Health != null)
        {
            Health.CurrentHealth = Math.Max(1, Health.MaxHealth / 5);
        }
        _morale = Math.Max(0, _morale - 30);
        _state = OperatorState.Idle;
        GD.Print($"[{EntityName}] 自动撤离归队，恢复 20% 生命，心情 -30");
    }

    private void OnOperatorDown(Node2D op)
    {
        // 其他干员倒下时的处理（暂空）
    }

    private void OnOperatorRevived(Node2D op)
    {
        if (op == this)
        {
            Revive();
        }
    }

    public override void Revive()
    {
        // 干员的 _isDead 始终为 false（不真正死亡），
        // 因此以 Down 状态为准，直接补满血量。
        if (_state != OperatorState.Down) return;

        Health?.FullHeal();
        _state = OperatorState.Idle;
        GD.Print($"[{EntityName}] 已复活，回归战斗");
    }

    // ============================================================
    // 调试
    // ============================================================

    public override string ToString()
    {
        return $"[{EntityName}] Lv.{_currentLevel} HP:{Health?.CurrentHealth}/{Health?.MaxHealth} 状态:{_state}";
    }

    // ============================================================
    // 序列化支持
    // ============================================================

    /// <summary>从运行时数据恢复干员状态</summary>
    public void RestoreFromRuntime(OperatorRuntime runtime)
    {
        if (runtime == null || _data == null) return;

        // 恢复等级和经验（先按等级重算属性，再套用存档血量，避免被 ApplyStats 覆盖）
        _currentLevel = runtime.CurrentLevel;
        _currentExp = runtime.CurrentExp;
        ApplyStats();

        if (Health != null)
        {
            Health.CurrentHealth = Math.Min(runtime.CurrentHealth, Health.MaxHealth);
        }

        // 恢复心情
        _morale = runtime.Morale;

        // 恢复位置
        GlobalPosition = new Vector2(runtime.PosX, runtime.PosY);

        // 恢复状态
        if (runtime.IsInjured)
        {
            _state = OperatorState.Down;
        }
        else if (runtime.IsFollowing)
        {
            _state = OperatorState.Following;
        }
        else
        {
            _state = OperatorState.Idle;
        }

        GD.Print($"[{EntityName}] 从存档恢复完成 — Lv.{_currentLevel}, HP:{Health?.CurrentHealth}/{Health?.MaxHealth}");
    }

    /// <summary>导出为运行时数据</summary>
    public OperatorRuntime ExportRuntime()
    {
        if (_data == null) return null;

        return new OperatorRuntime
        {
            OperatorId = _data.Id,
            CurrentLevel = _currentLevel,
            CurrentExp = _currentExp,
            CurrentHealth = Health?.CurrentHealth ?? 0,
            MaxHealth = Health?.MaxHealth ?? 0,
            Morale = _morale,
            PosX = GlobalPosition.X,
            PosY = GlobalPosition.Y,
            IsInjured = _state == OperatorState.Down,
            InjuryDaysLeft = 0, // 后续实现
            IsFollowing = _state == OperatorState.Following,
            Trust = 0, // 从 Profile 读取
        };
    }
}

/// <summary>干员状态枚举</summary>
public enum OperatorState
{
    Idle, // 待命
    Following, // 跟随博士
    Moving, // 移动到指定位置
    Attacking, // 攻击目标
    Chasing, // 追击中
    Down, // 战斗不能
    Resting, // 休整中
}
