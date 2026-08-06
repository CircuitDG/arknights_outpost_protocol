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

    public override void _Ready()
    {
        // 订阅信号
        EventBus.Instance.GameStateChanged += OnGameStateChanged;
        EventBus.Instance.DayNightChanged += OnDayNightChanged;

        // 触发信号
        EventBus.Instance.EmitGameStateChanged(GameState.Explore);
        EventBus.Instance.EmitDayNightChanged(DayPhase.Night, 0.5f);

        GD.Print("[测试] EventBus 冒烟测试执行完毕");
    }

    public override void _Process(double delta)
    {
        _frameCount++;
        if (GameManager.Instance == null) return;

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
}
