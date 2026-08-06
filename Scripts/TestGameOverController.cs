using Godot;
using OutpostProtocol.Gameplay.Building;
using OutpostProtocol.Managers;
using OutpostProtocol.UI.Controllers;

/// <summary>
/// 失败结算面板测试：
/// 触发 GameOver → 面板自动弹出 → 原因/战绩显示
/// </summary>
public partial class TestGameOverController : Node
{
    private GameOverPanelController _panel;
    private int _frameCount;

    public override void _Ready()
    {
        _panel = GetNode<GameOverPanelController>("../GameOverPanel");
        GD.Print("========== 失败结算面板测试 ==========");
    }

    public override void _Process(double delta)
    {
        _frameCount++;

        if (_frameCount == 5)
        {
            GameManager.Instance.GameOverWithReason(GameOverReason.CoreDestroyed);
        }
        else if (_frameCount == 20)
        {
            GD.Print($"[TestGameOver] 面板可见={_panel.Visible}, 原因标签={(GetReasonText())}");
        }
        else if (_frameCount >= 25)
        {
            GD.Print("[TestGameOver] 测试完成");
            GetTree().Quit();
        }
    }

    private string GetReasonText()
    {
        var label = GetNodeOrNull<Label>("../GameOverPanel/Panel/MainContainer/ReasonLabel");
        return label?.Text ?? "null";
    }
}
