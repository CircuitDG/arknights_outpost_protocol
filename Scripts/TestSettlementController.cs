using Godot;
using OutpostProtocol.Managers;
using OutpostProtocol.UI.Controllers;

/// <summary>
/// 休整期结算面板自动化测试：
/// 快速推进状态机到 Rest → 面板自动弹出 → 自动倒计时继续 → 进入新的一天
/// </summary>
public partial class TestSettlementController : Node
{
    private SettlementPanelController _panel;
    private int _frameCount;

    public override void _Ready()
    {
        _panel = GetNode<SettlementPanelController>("../SettlementPanel");
        GD.Print("========== 结算面板测试 ==========");
    }

    public override void _Process(double delta)
    {
        _frameCount++;

        // 快速推进：探索→建设→防守→休整
        if (_frameCount == 3 || _frameCount == 6 || _frameCount == 9)
        {
            GameManager.Instance.SkipCurrentPhase();
        }

        if (_frameCount == 60)
        {
            GD.Print($"[TestSettlement] 休整期: Day={GameManager.Instance.DayCount}, 面板可见={_panel.Visible}");
        }
        else if (_frameCount == 250)
        {
            GD.Print($"[TestSettlement] 自动继续后: Day={GameManager.Instance.DayCount}, State={GameManager.Instance.CurrentState}, 面板可见={_panel.Visible}");
        }
        else if (_frameCount >= 260)
        {
            GD.Print("[TestSettlement] 测试完成");
            GetTree().Quit();
        }
    }
}
