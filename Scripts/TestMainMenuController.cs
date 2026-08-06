using Godot;

/// <summary>
/// 主菜单启动流程测试：
/// 自动按下"新游戏" → 清档/开新对局 → 切换到游戏场景
/// </summary>
public partial class TestMainMenuController : Node
{
    private int _frameCount;

    public override void _Process(double delta)
    {
        _frameCount++;

        if (_frameCount == 5)
        {
            var button = GetNodeOrNull<Button>("../MainMenu/CenterContainer/MenuContainer/NewGameButton");
            if (button == null)
            {
                GD.PrintErr("[TestMainMenu] 未找到新游戏按钮");
                GetTree().Quit();
                return;
            }

            GD.Print("[TestMainMenu] 触发新游戏按钮");
            button.EmitSignal("pressed");
        }
    }
}
