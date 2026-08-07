using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Gameplay.Character.Doctor;
using OutpostProtocol.Managers;

/// <summary>
/// 冒烟测试：
/// 1. EventBus 订阅/触发信号
/// 2. GameManager 状态机完整循环（借助 SkipCurrentPhase 快速推进）
/// </summary>
public partial class TestEventBus : Node
{
    private int _frameCount;
    private bool _dataTestPending = true;
    private bool _stateMachineTestActive = true;
    private Doctor _doctor;
    private Vector2 _startPos;
    private float _startStamina;

    public override void _Ready()
    {
        // 订阅信号
        EventBus.Instance.GameStateChanged += OnGameStateChanged;
        EventBus.Instance.DayNightChanged += OnDayNightChanged;
        EventBus.Instance.LogMessage += OnLogMessage;

        // 触发信号
        EventBus.Instance.EmitGameStateChanged(GameState.Explore);
        EventBus.Instance.EmitDayNightChanged(DayPhase.Night, 0.5f);

        GD.Print("[测试] EventBus 冒烟测试执行完毕");
    }

    public override void _Process(double delta)
    {
        _frameCount++;
        if (GameManager.Instance == null) return;

        // 等待 DataManager 异步加载完成后做数据测试
        if (_dataTestPending && _frameCount >= 5)
        {
            if (DataManager.Instance.IsLoaded)
            {
                _dataTestPending = false;
                TestDataManager();
            }
            else if (_frameCount >= 14)
            {
                _dataTestPending = false;
                GD.Print("[测试] DataManager 尚未加载完成，跳过数据测试");
            }
        }

        // 每 3 帧跳过当前阶段，快速走完：探索 → 建设 → 防守 → 休整 → 次日探索
        if (_stateMachineTestActive && _frameCount % 3 == 0)
        {
            GameManager.Instance.SkipCurrentPhase();
        }

        if (_frameCount == 15)
        {
            _stateMachineTestActive = false;
            GD.Print("[测试] GameManager 状态机循环测试完成（GameOver 交由 Doctor 死亡验证）");
        }
        else if (_frameCount == 18)
        {
            TestDoctorInit();
        }
        else if (_frameCount == 20)
        {
            StartDoctorMovementTest();
        }
        else if (_frameCount == 40)
        {
            FinishDoctorMovementTest();
        }
        else if (_frameCount == 42)
        {
            TestDoctorDeath();
        }
        else if (_frameCount >= 44)
        {
            GetTree().Quit();
        }
    }

    private void OnGameStateChanged(GameState newState)
    {
        GD.Print($"[测试] 收到游戏状态变化: {newState}");
    }

    private void OnDayNightChanged(DayPhase phase, float progress)
    {
        GD.Print($"[测试] 收到昼夜变化: {phase} {progress:P0}");
    }

    private void OnLogMessage(string message, string level)
    {
        GD.Print($"[日志] {level}: {message}");
    }

    private void TestDoctorInit()
    {
        _doctor = GetNodeOrNull<Doctor>("/root/Main/World/Doctor");
        if (_doctor == null)
        {
            GD.Print("[测试] 未找到 Doctor 节点");
            return;
        }

        GD.Print("========== Doctor 测试 ==========");
        GD.Print($"HP: {_doctor.CurrentHealth}/{_doctor.MaxHealthValue}");
        GD.Print($"体力: {_doctor.CurrentStamina}/{_doctor.MaxStaminaValue}");
        GD.Print($"输入绑定: 左={InputMap.ActionGetEvents("move_left").Count}, 右={InputMap.ActionGetEvents("move_right").Count}, 上={InputMap.ActionGetEvents("move_up").Count}, 下={InputMap.ActionGetEvents("move_down").Count}, 冲刺={InputMap.ActionGetEvents("sprint").Count}");
    }

    private void StartDoctorMovementTest()
    {
        if (_doctor == null) return;

        _startPos = _doctor.GlobalPosition;
        _startStamina = _doctor.CurrentStamina;
        Input.ActionPress("move_right");
        Input.ActionPress("sprint");
        GD.Print("[测试] 开始冲刺移动测试 (move_right + sprint)");
    }

    private void FinishDoctorMovementTest()
    {
        if (_doctor == null) return;

        Input.ActionRelease("move_right");
        Input.ActionRelease("sprint");

        float movedX = _doctor.GlobalPosition.X - _startPos.X;
        float staminaUsed = _startStamina - _doctor.CurrentStamina;
        GD.Print($"[测试] 冲刺移动结果: 位移X={movedX:F1}px, 冲刺中={_doctor.IsSprinting}, 体力消耗={staminaUsed:F1}");
    }

    private void TestDoctorDeath()
    {
        if (_doctor == null) return;

        _doctor.TakeDamage(20f);
        GD.Print($"[测试] 受伤后 HP: {_doctor.CurrentHealth}");
        _doctor.TakeDamage(999f);
        GD.Print($"[测试] 博士死亡状态: {_doctor.IsDead}");
    }

    private void TestDataManager()
    {
        GD.Print("========== DataManager 测试 ==========");

        var fang = DataManager.Instance.GetOperator(1001);
        if (fang != null)
        {
            GD.Print($"干员: {fang.Name} ({fang.ClassType}), HP:{fang.BaseHp}, ATK:{fang.BaseAttack}");
        }

        var collection = DataManager.Instance.GetCollection(101);
        if (collection != null)
        {
            GD.Print($"藏品: {collection.Name} [{collection.Rarity}] - {collection.Description}");
        }

        var tower = DataManager.Instance.GetTower(1);
        if (tower != null)
        {
            GD.Print($"塔: {tower.Name}, 伤害:{tower.BaseDamage}, 射程:{tower.AttackRange}");
        }

        var wave = DataManager.Instance.GetWaveByNumber(1);
        if (wave != null)
        {
            GD.Print($"波次 {wave.WaveNumber}: {wave.EnemyTypes.Count} 种敌人");
        }

        GD.Print("========================================");
    }
}
