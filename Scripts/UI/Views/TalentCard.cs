using Godot;
using OutpostProtocol.Data;
using OutpostProtocol.Managers;
using OutpostProtocol.UI.Controllers;
using System.Collections.Generic;

namespace OutpostProtocol.UI.Views;

/// <summary>
/// 天赋卡片（单个天赋的 UI）
/// </summary>
public partial class TalentCard : Control
{
    private Label _nameLabel;
    private Label _descriptionLabel;
    private Label _levelLabel;
    private Label _costLabel;
    private Button _upgradeButton;
    private ProgressBar _progressBar;

    private TalentData _talent;
    private SaveProfile _profile;
    private TalentTreeController _controller;

    public override void _Ready()
    {
        _nameLabel = GetNodeOrNull<Label>("CardPanel/MainRow/InfoColumn/NameLabel");
        _descriptionLabel = GetNodeOrNull<Label>("CardPanel/MainRow/InfoColumn/DescriptionLabel");
        _progressBar = GetNodeOrNull<ProgressBar>("CardPanel/MainRow/InfoColumn/ProgressBar");
        _levelLabel = GetNodeOrNull<Label>("CardPanel/MainRow/ActionColumn/LevelLabel");
        _costLabel = GetNodeOrNull<Label>("CardPanel/MainRow/ActionColumn/CostLabel");
        _upgradeButton = GetNodeOrNull<Button>("CardPanel/MainRow/ActionColumn/UpgradeButton");

        if (_upgradeButton != null)
        {
            _upgradeButton.Pressed += OnUpgradePressed;
        }

        UpdateUI();
    }

    public override void _ExitTree()
    {
        if (_upgradeButton != null)
        {
            _upgradeButton.Pressed -= OnUpgradePressed;
        }
    }

    public void Setup(TalentData talent, SaveProfile profile, TalentTreeController controller)
    {
        _talent = talent;
        _profile = profile;
        _controller = controller;
        UpdateUI();
    }

    public void UpdateUI()
    {
        if (_talent == null || _profile == null) return;

        int currentLevel = _profile.TalentLevels.GetValueOrDefault(_talent.Id, 0);
        bool isMaxLevel = currentLevel >= _talent.MaxLevel;
        int cost = _talent.CostPerLevel;
        bool canAfford = _profile.TotalTalentPoints >= cost;

        if (_nameLabel != null) _nameLabel.Text = _talent.Name;

        if (_descriptionLabel != null)
        {
            _descriptionLabel.Text = currentLevel > 0 && _talent.Descriptions.Count >= currentLevel
                ? _talent.Descriptions[currentLevel - 1]
                : _talent.Description;
        }

        if (_levelLabel != null) _levelLabel.Text = $"{currentLevel}/{_talent.MaxLevel}";

        if (_costLabel != null)
        {
            _costLabel.Text = isMaxLevel ? "已满级" : $"消耗: {cost} 点";
        }

        if (_progressBar != null)
        {
            _progressBar.MaxValue = _talent.MaxLevel;
            _progressBar.Value = currentLevel;
        }

        if (_upgradeButton != null)
        {
            _upgradeButton.Disabled = isMaxLevel || !canAfford;
            _upgradeButton.Text = isMaxLevel ? "已满级" : "升级";
            _upgradeButton.Modulate = canAfford && !isMaxLevel ? Colors.White : Colors.Gray;
        }
    }

    private void OnUpgradePressed()
    {
        _controller?.UpgradeTalent(_talent?.Id);
    }
}
