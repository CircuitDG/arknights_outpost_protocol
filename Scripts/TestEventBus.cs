using Godot;
using OutpostProtocol.Core.EventBus;

/// <summary>
/// EventBus 冒烟测试：订阅并触发信号，验证 AutoLoad 正常加载。
/// </summary>
public partial class TestEventBus : Node
{
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

    private void OnGameStateChanged(GameState newState)
    {
        GD.Print($"[测试] 收到游戏状态变化: {newState}");
    }

    private void OnDayNightChanged(DayPhase phase, float progress)
    {
        GD.Print($"[测试] 收到昼夜变化: {phase} {progress:P0}");
    }
}
