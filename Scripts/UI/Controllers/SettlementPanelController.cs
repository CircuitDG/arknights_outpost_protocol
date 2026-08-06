using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Data;
using OutpostProtocol.Gameplay.Building;
using OutpostProtocol.Gameplay.Character.Enemy;
using OutpostProtocol.Gameplay.Character.Operator;
using OutpostProtocol.Gameplay.Inventory;
using OutpostProtocol.Managers;
using System.Collections.Generic;

namespace OutpostProtocol.UI.Controllers;

/// <summary>
/// 休整期结算面板控制器
/// 防守成功进入 Rest 阶段时弹出，展示当日数据，可点击/自动继续到下一天
/// </summary>
public partial class SettlementPanelController : Control
{
    // ============================================================
    // 导出变量
    // ============================================================

    [ExportGroup("配置")]
    [Export] public float AutoContinueDelay = 5.0f;

    // ============================================================
    // UI 节点引用
    // ============================================================

    private Label _dayLabel;
    private Label _killsLabel;
    private Label _expLabel;
    private VBoxContainer _resourceContainer;
    private VBoxContainer _operatorContainer;
    private Button _continueButton;
    private Label _autoContinueLabel;
    private Label _coreHealthLabel;
    private Label _coreStatusLabel;

    // ============================================================
    // 运行时状态
    // ============================================================

    private SettlementData _data;
    private float _autoTimer;
    private bool _isReady;
    private bool _autoContinued;
    private readonly Dictionary<int, int> _prevLevels = new();

    // ============================================================
    // 生命周期
    // ============================================================

    public override void _Ready()
    {
        _dayLabel = GetNodeOrNull<Label>("Panel/MainContainer/DayLabel");
        _killsLabel = GetNodeOrNull<Label>("Panel/MainContainer/KillsLabel");
        _expLabel = GetNodeOrNull<Label>("Panel/MainContainer/ExpLabel");
        _resourceContainer = GetNodeOrNull<VBoxContainer>("Panel/MainContainer/ResourceContainer");
        _operatorContainer = GetNodeOrNull<VBoxContainer>("Panel/MainContainer/OperatorContainer");
        _continueButton = GetNodeOrNull<Button>("Panel/MainContainer/ButtonContainer/ContinueButton");
        _autoContinueLabel = GetNodeOrNull<Label>("Panel/MainContainer/ButtonContainer/AutoContinueLabel");
        _coreHealthLabel = GetNodeOrNull<Label>("Panel/MainContainer/CoreHealthLabel");
        _coreStatusLabel = GetNodeOrNull<Label>("Panel/MainContainer/CoreStatusLabel");

        Hide();

        if (_continueButton != null)
        {
            _continueButton.Pressed += OnContinuePressed;
        }

        EventBus.Instance.GameStateChanged += OnGameStateChanged;
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.GameStateChanged -= OnGameStateChanged;
        }
        if (_continueButton != null)
        {
            _continueButton.Pressed -= OnContinuePressed;
        }
    }

    public override void _Process(double delta)
    {
        if (!_isReady || _autoContinued) return;

        _autoTimer -= (float)delta;
        if (_autoTimer <= 0)
        {
            _autoTimer = 0;
            if (_autoContinueLabel != null)
            {
                _autoContinueLabel.Text = "自动继续...";
            }
            Continue();
        }
        else if (_autoContinueLabel != null)
        {
            _autoContinueLabel.Text = $"自动继续: {_autoTimer:F0}s";
        }
    }

    // ============================================================
    // 事件回调
    // ============================================================

    private void OnGameStateChanged(GameState newState)
    {
        if (newState == GameState.Rest)
        {
            ShowPanel();
        }
        else
        {
            Hide();
            _isReady = false;
        }
    }

    private void OnContinuePressed()
    {
        Continue();
    }

    // ============================================================
    // 数据收集与显示
    // ============================================================

    private void ShowPanel()
    {
        _data = CollectSettlementData();
        _isReady = true;
        _autoContinued = false;
        _autoTimer = AutoContinueDelay;

        UpdateUI();
        Show();

        GD.Print("[SettlementPanel] 结算面板已显示");
    }

    private SettlementData CollectSettlementData()
    {
        var stats = DailyStatsManager.Instance?.CurrentStats;
        if (stats == null)
        {
            GD.PushWarning("[SettlementPanel] DailyStatsManager 未就绪，使用空数据");
            return new SettlementData
            {
                DayNumber = GameManager.Instance?.DayCount ?? 1,
            };
        }

        var operators = new List<OperatorSettlementInfo>();
        foreach (var snapshot in stats.OperatorSnapshots)
        {
            operators.Add(new OperatorSettlementInfo
            {
                OperatorId = snapshot.OperatorId,
                Name = snapshot.Name,
                LevelBefore = DailyStatsManager.Instance.GetOperatorLevelAtStart(snapshot.OperatorId),
                LevelAfter = snapshot.Level,
                ExpGained = DailyStatsManager.Instance.GetOperatorExpGained(snapshot.OperatorId),
                Kills = DailyStatsManager.Instance.GetOperatorKills(snapshot.OperatorId),
                WasInjured = snapshot.MaxHealth > 0 && snapshot.Health < snapshot.MaxHealth * 0.5f,
                IsDown = snapshot.IsDown,
            });
        }

        return new SettlementData
        {
            DayNumber = stats.DayNumber,
            TotalKills = stats.TotalKills,
            TotalExpGained = stats.TotalExpGained,
            ResourcesGained = stats.ResourcesGained,
            Operators = operators,
            WaveNumber = stats.WavesCleared,
            IsGameOver = false,
        };
    }

    private void UpdateUI()
    {
        if (_data == null) return;

        if (_dayLabel != null) _dayLabel.Text = $"第 {_data.DayNumber} 天";
        if (_killsLabel != null) _killsLabel.Text = $"击杀: {_data.TotalKills}";
        if (_expLabel != null) _expLabel.Text = $"获得经验: {_data.TotalExpGained}";

        UpdateResourceList();
        UpdateOperatorList();
        UpdateCoreStatus();
    }

    private void UpdateCoreStatus()
    {
        var core = GetTree().GetFirstNodeInGroup("outpost_core") as OutpostCore;
        if (core == null) return;

        if (_coreStatusLabel != null)
        {
            _coreStatusLabel.Text = core.IsDestroyed ? "💀 已摧毁" : "🛡️ 运转中";
            _coreStatusLabel.Modulate = core.IsDestroyed ? Colors.Red : Colors.Green;
        }

        if (_coreHealthLabel != null)
        {
            _coreHealthLabel.Text = $"核心: {core.CurrentHealth}/{core.MaxHealth}";
        }
    }

    private void UpdateResourceList()
    {
        if (_resourceContainer == null) return;

        ClearContainer(_resourceContainer);

        if (_data.ResourcesGained == null || _data.ResourcesGained.Count == 0)
        {
            _resourceContainer.AddChild(new Label { Text = "无资源" });
            return;
        }

        foreach (var kvp in _data.ResourcesGained)
        {
            var item = DataManager.Instance.GetItem(kvp.Key);
            string name = item?.Name ?? $"未知({kvp.Key})";
            _resourceContainer.AddChild(new Label { Text = $"{name}: {kvp.Value}" });
        }
    }

    private void UpdateOperatorList()
    {
        if (_operatorContainer == null) return;

        ClearContainer(_operatorContainer);

        if (_data.Operators == null || _data.Operators.Count == 0)
        {
            _operatorContainer.AddChild(new Label { Text = "无干员" });
            return;
        }

        foreach (var info in _data.Operators)
        {
            string status = string.Empty;
            if (info.IsDown) status = " [战斗不能]";
            else if (info.WasInjured) status = " [受伤]";
            else if (info.LevelAfter > info.LevelBefore) status = $" ⬆ Lv.{info.LevelAfter}";

            _operatorContainer.AddChild(new Label
            {
                Text = $"{info.Name} Lv.{info.LevelBefore} → {info.LevelAfter}{status} 击杀:{info.Kills}",
            });
        }
    }

    private static void ClearContainer(Node container)
    {
        foreach (var child in container.GetChildren())
        {
            child.QueueFree();
        }
    }

    // ============================================================
    // 继续
    // ============================================================

    /// <summary>继续到下一天（测试/按钮共用）</summary>
    public void ForceContinue()
    {
        Continue();
    }

    private void Continue()
    {
        if (!_isReady || _autoContinued) return;
        _autoContinued = true;
        _isReady = false;

        // 记录干员等级，供下一天结算对比
        _prevLevels.Clear();
        foreach (var node in GetTree().GetNodesInGroup("operators"))
        {
            if (node is Operator op)
            {
                _prevLevels[op.OperatorDataId] = op.CurrentLevel;
            }
        }

        Hide();
        EventBus.Instance.EmitLogMessage("休整期结束，进入新的一天", "INFO");
        GameManager.Instance?.ContinueToNextDay();

        GD.Print("[SettlementPanel] 继续到新的一天");
    }
}
