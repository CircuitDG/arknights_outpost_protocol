using Godot;
using OutpostProtocol.Core.EventBus;
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
        if (_frameCount % 3 == 0)
        {
            GameManager.Instance.SkipCurrentPhase();
        }

        // 跑完两轮状态机后验证 GameOver，然后退出
        if (_frameCount >= 15)
        {
            GD.Print("[测试] GameManager 状态机测试完成，触发 GameOver");
            GameManager.Instance.GameOver();
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
