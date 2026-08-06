using Godot;
using OutpostProtocol.Managers;

namespace OutpostProtocol.UI.Controllers;

/// <summary>
/// 主菜单控制器
/// 新游戏（清档+开新对局）/ 继续游戏（读档）/ 设置占位 / 退出
/// </summary>
public partial class MainMenuController : Control
{
    private Button _newGameButton;
    private Button _continueButton;
    private Button _settingsButton;
    private Button _quitButton;
    private Label _versionLabel;

    private const string GameScenePath = "res://Scenes/Main.tscn";

    public override void _Ready()
    {
        _newGameButton = GetNodeOrNull<Button>("CenterContainer/MenuContainer/NewGameButton");
        _continueButton = GetNodeOrNull<Button>("CenterContainer/MenuContainer/ContinueButton");
        _settingsButton = GetNodeOrNull<Button>("CenterContainer/MenuContainer/SettingsButton");
        _quitButton = GetNodeOrNull<Button>("CenterContainer/MenuContainer/QuitButton");
        _versionLabel = GetNodeOrNull<Label>("CenterContainer/MenuContainer/VersionLabel");

        if (_newGameButton != null) _newGameButton.Pressed += OnNewGamePressed;
        if (_continueButton != null) _continueButton.Pressed += OnContinuePressed;
        if (_settingsButton != null) _settingsButton.Pressed += OnSettingsPressed;
        if (_quitButton != null) _quitButton.Pressed += OnQuitPressed;

        UpdateContinueButton();

        if (_versionLabel != null)
        {
            _versionLabel.Text = $"v0.1.0 - {OS.GetName()}";
        }

        GD.Print("[MainMenu] 主菜单加载完成");
    }

    public override void _ExitTree()
    {
        if (_newGameButton != null) _newGameButton.Pressed -= OnNewGamePressed;
        if (_continueButton != null) _continueButton.Pressed -= OnContinuePressed;
        if (_settingsButton != null) _settingsButton.Pressed -= OnSettingsPressed;
        if (_quitButton != null) _quitButton.Pressed -= OnQuitPressed;
    }

    // ============================================================
    // 按钮事件
    // ============================================================

    private void OnNewGamePressed()
    {
        GD.Print("[MainMenu] 开始新游戏");

        var saveManager = SaveManager.Instance;
        if (saveManager != null)
        {
            saveManager.DeleteCurrentRun();
            saveManager.NewRun();
        }

        GetTree().ChangeSceneToFile(GameScenePath);
    }

    private void OnContinuePressed()
    {
        GD.Print("[MainMenu] 继续游戏");

        var saveManager = SaveManager.Instance;
        if (saveManager != null && saveManager.LoadRun())
        {
            GetTree().ChangeSceneToFile(GameScenePath);
        }
        else
        {
            ShowMessage("没有找到存档", "请开始新游戏");
            UpdateContinueButton();
        }
    }

    private void OnSettingsPressed()
    {
        GD.Print("[MainMenu] 设置（占位）");
        ShowMessage("设置", "设置功能开发中...");
    }

    private void OnQuitPressed()
    {
        GD.Print("[MainMenu] 退出游戏");
        GetTree().Quit();
    }

    // ============================================================
    // UI 辅助
    // ============================================================

    private void UpdateContinueButton()
    {
        if (_continueButton == null) return;

        var saveManager = SaveManager.Instance;
        bool hasRun = saveManager != null && saveManager.GetRunFiles().Count > 0;

        _continueButton.Disabled = !hasRun;
        _continueButton.Text = hasRun ? "📂 继续游戏" : "📂 继续游戏 (无存档)";
        _continueButton.Modulate = hasRun ? Colors.White : Colors.Gray;
        GD.Print($"[MainMenu] 继续按钮可用: {hasRun}");
    }

    private void ShowMessage(string title, string message)
    {
        var dialog = new AcceptDialog
        {
            Title = title,
            DialogText = message,
            Size = new Vector2I(300, 150),
        };
        AddChild(dialog);
        dialog.PopupCentered();
    }
}
