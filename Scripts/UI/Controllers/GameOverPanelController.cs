using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Gameplay.Building;
using OutpostProtocol.Managers;
using System.Text;

namespace OutpostProtocol.UI.Controllers;

/// <summary>
/// 失败结算面板
/// GameOver 时弹出：失败原因 + 当日战绩 + 返回主菜单/重新开始
/// </summary>
public partial class GameOverPanelController : Control
{
    private Label _titleLabel;
    private Label _reasonLabel;
    private Label _statsLabel;
    private Button _menuButton;
    private Button _restartButton;

    public override void _Ready()
    {
        _titleLabel = GetNodeOrNull<Label>("Panel/MainContainer/TitleLabel");
        _reasonLabel = GetNodeOrNull<Label>("Panel/MainContainer/ReasonLabel");
        _statsLabel = GetNodeOrNull<Label>("Panel/MainContainer/StatsLabel");
        _menuButton = GetNodeOrNull<Button>("Panel/MainContainer/ButtonRow/MenuButton");
        _restartButton = GetNodeOrNull<Button>("Panel/MainContainer/ButtonRow/RestartButton");

        if (_menuButton != null) _menuButton.Pressed += OnMenuPressed;
        if (_restartButton != null) _restartButton.Pressed += OnRestartPressed;

        EventBus.Instance.GameStateChanged += OnGameStateChanged;
        Hide();
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.GameStateChanged -= OnGameStateChanged;
        }
        if (_menuButton != null) _menuButton.Pressed -= OnMenuPressed;
        if (_restartButton != null) _restartButton.Pressed -= OnRestartPressed;
    }

    private void OnGameStateChanged(GameState newState)
    {
        if (newState == GameState.GameOver)
        {
            ShowPanel();
        }
        else
        {
            Hide();
        }
    }

    private void ShowPanel()
    {
        var gm = GameManager.Instance;
        string reason = gm?.GameOverReason switch
        {
            GameOverReason.CoreDestroyed => "前哨站核心被摧毁",
            GameOverReason.ResourceStarvation => "资源枯竭",
            _ => "博士倒下",
        };

        if (_titleLabel != null) _titleLabel.Text = "💀 游戏结束";
        if (_reasonLabel != null) _reasonLabel.Text = $"失败原因: {reason}";

        if (_statsLabel != null)
        {
            var stats = DailyStatsManager.Instance?.CurrentStats;
            var sb = new StringBuilder();
            sb.AppendLine($"存活天数: {gm?.DayCount ?? 1}");
            if (stats != null)
            {
                sb.AppendLine($"总击杀: {stats.TotalKills}");
                sb.AppendLine($"清波数: {stats.WavesCleared}");
                sb.AppendLine($"获得经验: {stats.TotalExpGained}");
                sb.AppendLine($"建造塔: {stats.TowersBuilt} 座, 升级: {stats.TowersUpgraded} 次");

                if (stats.ResourcesGained.Count > 0)
                {
                    sb.Append("资源: ");
                    bool first = true;
                    foreach (var kvp in stats.ResourcesGained)
                    {
                        if (!first) sb.Append(", ");
                        first = false;
                        sb.Append($"{DataManager.Instance.GetItem(kvp.Key)?.Name ?? $"物品{kvp.Key}"} x{kvp.Value}");
                    }
                    sb.AppendLine();
                }
            }
            _statsLabel.Text = sb.ToString();
        }

        Show();
        GD.Print("[GameOverPanel] 失败结算面板已显示");
    }

    private void OnMenuPressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
    }

    private void OnRestartPressed()
    {
        var sm = SaveManager.Instance;
        if (sm != null)
        {
            sm.DeleteCurrentRun();
            sm.RestoreOnGameLoad = false;
            sm.NewRun();
        }
        GetTree().ChangeSceneToFile("res://Scenes/Main.tscn");
    }
}
