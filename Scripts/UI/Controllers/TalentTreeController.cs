using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Data;
using OutpostProtocol.Gameplay.Character.Doctor;
using OutpostProtocol.Gameplay.Character.Operator;
using OutpostProtocol.Gameplay.Inventory;
using OutpostProtocol.Managers;
using OutpostProtocol.UI.Views;
using System.Collections.Generic;
using System.Linq;

namespace OutpostProtocol.UI.Controllers;

/// <summary>
/// 天赋树控制器
/// 负责：天赋列表展示、升级消耗、效果应用、存档持久化
/// </summary>
public partial class TalentTreeController : Control
{
    private VBoxContainer _branchContainer;
    private Label _availablePointsLabel;
    private Button _closeButton;
    private PackedScene _talentCardPrefab;

    private SaveProfile _profile;
    private readonly Dictionary<string, TalentCard> _talentCards = new();
    private bool _treeBuilt;
    private bool _dataReady;

    public override void _Ready()
    {
        _branchContainer = GetNodeOrNull<VBoxContainer>("Panel/MainContainer/ScrollContainer/BranchContainer");
        _availablePointsLabel = GetNodeOrNull<Label>("Panel/MainContainer/AvailablePointsLabel");
        _closeButton = GetNodeOrNull<Button>("Panel/MainContainer/TitleRow/CloseButton");

        _talentCardPrefab = GD.Load<PackedScene>("res://Scenes/UI/TalentCard.tscn");
        if (_talentCardPrefab == null)
        {
            GD.PushError("[TalentTree] 未找到 TalentCard.tscn");
        }

        Hide();

        _profile = SaveManager.Instance?.Profile;
        if (_profile == null)
        {
            GD.PushError("[TalentTree] 存档未加载");
            return;
        }

        _profile.TalentLevels ??= new Dictionary<string, int>();

        if (_closeButton != null)
        {
            _closeButton.Pressed += OnClosePressed;
        }

        if (DataManager.Instance != null && DataManager.Instance.IsLoaded)
        {
            BuildTalentTree();
            _treeBuilt = true;
            _dataReady = true;
        }
        ApplyAllTalents();
        GD.Print("[TalentTree] 初始化完成");
    }

    public override void _ExitTree()
    {
        if (_closeButton != null)
        {
            _closeButton.Pressed -= OnClosePressed;
        }
    }

    public override void _Process(double delta)
    {
        // DataManager 异步加载完成后补建天赋树
        if (!_dataReady && DataManager.Instance != null && DataManager.Instance.IsLoaded && _talentCardPrefab != null)
        {
            _dataReady = true;
            _treeBuilt = true;
            BuildTalentTree();
            ApplyAllTalents();
        }
    }

    // ============================================================
    // 构建天赋树
    // ============================================================

    private void BuildTalentTree()
    {
        if (_branchContainer == null || _talentCardPrefab == null) return;

        foreach (var child in _branchContainer.GetChildren())
        {
            child.QueueFree();
        }
        _talentCards.Clear();

        var branches = new Dictionary<string, List<TalentData>>();
        foreach (var talent in DataManager.Instance.Talents.Values)
        {
            if (!branches.TryGetValue(talent.Branch, out var list))
            {
                list = new List<TalentData>();
                branches[talent.Branch] = list;
            }
            list.Add(talent);
        }

        var branchOrder = new List<string> { "Survival", "Combat", "Base", "Explore" };
        var branchNames = new Dictionary<string, string>
        {
            { "Survival", "🛡️ 生存" },
            { "Combat", "⚔️ 战斗" },
            { "Base", "🏗️ 基建" },
            { "Explore", "🔍 探索" },
        };
        var branchColors = new Dictionary<string, Color>
        {
            { "Survival", new Color(0.3f, 0.8f, 0.3f) },
            { "Combat", new Color(0.9f, 0.3f, 0.3f) },
            { "Base", new Color(0.3f, 0.6f, 0.9f) },
            { "Explore", new Color(0.9f, 0.7f, 0.2f) },
        };

        foreach (var branch in branchOrder)
        {
            if (!branches.TryGetValue(branch, out var talentList)) continue;

            _branchContainer.AddChild(new Label
            {
                Text = branchNames.GetValueOrDefault(branch, branch),
                Modulate = branchColors.GetValueOrDefault(branch, Colors.White),
            });

            var cards = new List<TalentCard>();
            var cardById = new Dictionary<string, TalentCard>();

            foreach (var talent in talentList.OrderBy(t => t.Tier).ThenBy(t => t.Name))
            {
                var card = _talentCardPrefab.Instantiate<TalentCard>();
                if (card == null) continue;

                card.Setup(talent, _profile, this);
                cards.Add(card);
                cardById[talent.Id] = card;
                _talentCards[talent.Id] = card;
            }

            var edges = new List<(string ParentId, string ChildId)>();
            foreach (var talent in talentList)
            {
                foreach (var prereq in talent.Prerequisites)
                {
                    edges.Add((prereq, talent.Id));
                }
            }

            var graph = new TalentTreeGraph();
            graph.Setup(cards, edges);
            _branchContainer.AddChild(graph);

            _branchContainer.AddChild(new HSeparator());
        }

        UpdateAll();
    }

    // ============================================================
    // 公共 API
    // ============================================================

    public void ShowTalentTree()
    {
        _profile = SaveManager.Instance?.Profile;
        if (_profile == null) return;
        _profile.TalentLevels ??= new Dictionary<string, int>();

        UpdateAll();
        Show();
    }

    public bool UpgradeTalent(string talentId)
    {
        if (_profile == null) return false;

        var talent = DataManager.Instance.GetTalent(talentId);
        if (talent == null) return false;

        int currentLevel = _profile.TalentLevels.GetValueOrDefault(talentId, 0);
        if (currentLevel >= talent.MaxLevel) return false;

        if (!ArePrerequisitesMet(talent))
        {
            GD.Print($"[TalentTree] 前置天赋未解锁，无法升级: {talent.Name}");
            EventBus.Instance.EmitLogMessage($"需要先解锁前置天赋", "WARN");
            return false;
        }

        int cost = talent.CostPerLevel;
        if (_profile.TotalTalentPoints < cost) return false;

        _profile.TotalTalentPoints -= cost;
        _profile.TalentLevels[talentId] = currentLevel + 1;
        SaveManager.Instance?.SaveProfile();

        ApplyTalentEffect(talentId, currentLevel + 1);
        UpdateAll();

        EventBus.Instance.EmitLogMessage($"天赋升级: {talent.Name} Lv.{currentLevel + 1}", "INFO");
        GD.Print($"[TalentTree] 升级天赋: {talent.Name} → Lv.{currentLevel + 1} (消耗 {cost} 点)");
        return true;
    }

    /// <summary>检查天赋前置是否全部满足（至少 1 级）</summary>
    public bool ArePrerequisitesMet(TalentData talent)
    {
        if (talent == null || talent.Prerequisites == null || talent.Prerequisites.Count == 0) return true;
        if (_profile?.TalentLevels == null) return false;

        foreach (var prereqId in talent.Prerequisites)
        {
            if (_profile.TalentLevels.GetValueOrDefault(prereqId, 0) <= 0)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>应用所有已点天赋（启动时调用）</summary>
    public void ApplyAllTalents()
    {
        if (_profile?.TalentLevels == null) return;

        foreach (var kvp in _profile.TalentLevels)
        {
            if (kvp.Value > 0)
            {
                ApplyTalentEffect(kvp.Key, kvp.Value);
            }
        }
        GD.Print($"[TalentTree] 应用所有天赋: {_profile.TalentLevels.Count} 个");
    }

    // ============================================================
    // UI 更新
    // ============================================================

    private void UpdateAll()
    {
        if (_availablePointsLabel != null)
        {
            _availablePointsLabel.Text = $"可用天赋点: {_profile?.TotalTalentPoints ?? 0}";
        }

        foreach (var card in _talentCards.Values)
        {
            card?.UpdateUI();
        }
    }

    // ============================================================
    // 效果应用
    // ============================================================

    private void ApplyTalentEffect(string talentId, int level)
    {
        var talent = DataManager.Instance.GetTalent(talentId);
        if (talent == null) return;

        float value = talent.EffectValues.Count >= level ? talent.EffectValues[level - 1] : 0;

        switch (talent.EffectType)
        {
            case "BackpackCapacity":
                ApplyBackpackCapacity(value);
                break;
            case "DoctorHealth":
                ApplyDoctorHealth(value);
                break;
            case "DoctorStaminaRegen":
                ApplyDoctorStaminaRegen(value);
                break;
            case "DoctorSpeed":
                ApplyDoctorSpeed(value);
                break;
            case "OperatorAttackSpeed":
                _operatorAttackSpeedBonus = value;
                GD.Print($"[TalentTree] 干员攻速加成: +{value * 100}%");
                break;
            case "OperatorExpGain":
                _operatorExpBonus = value;
                GD.Print($"[TalentTree] 干员经验加成: +{value * 100}%");
                break;
            case "OperatorStartLevel":
                _operatorStartLevelBonus = (int)value;
                GD.Print($"[TalentTree] 干员初始等级: +{value}");
                break;
            case "TowerBuildCost":
                _towerBuildCostReduction = value;
                GD.Print($"[TalentTree] 建造消耗减少: {value * 100}%");
                break;
            case "TowerUpgradeCost":
                _towerUpgradeCostReduction = value;
                GD.Print($"[TalentTree] 升级消耗减少: {value * 100}%");
                break;
            case "CoreRepairEfficiency":
                _coreRepairBonus = value;
                GD.Print($"[TalentTree] 核心修复效率: +{value * 100}%");
                break;
            case "GatherAmount":
                _gatherAmountBonus = (int)value;
                GD.Print($"[TalentTree] 采集量: +{value}");
                break;
            case "LootDropRate":
                _lootDropRateBonus = value;
                GD.Print($"[TalentTree] 掉落率: +{value * 100}%");
                break;
            default:
                GD.PushWarning($"[TalentTree] 未知效果类型: {talent.EffectType}");
                break;
        }
    }

    private void ApplyBackpackCapacity(float value)
    {
        var backpack = GetBackpack();
        if (backpack != null)
        {
            backpack.MaxCapacity = 200 + (int)value;
            GD.Print($"[TalentTree] 背包容量更新: {backpack.MaxCapacity}");
        }
    }

    private void ApplyDoctorHealth(float value)
    {
        var doctor = GetDoctor();
        if (doctor != null)
        {
            doctor.MaxHealth = 100 + value;
            doctor.Heal(0); // 触发 HUD 刷新
            GD.Print($"[TalentTree] 博士生命上限更新: {doctor.MaxHealthValue}");
        }
    }

    private void ApplyDoctorStaminaRegen(float value)
    {
        var doctor = GetDoctor();
        if (doctor != null)
        {
            doctor.StaminaRegenRate = 15 + value * 15;
            GD.Print($"[TalentTree] 博士体力恢复更新: {doctor.StaminaRegenRate}");
        }
    }

    private void ApplyDoctorSpeed(float value)
    {
        var doctor = GetDoctor();
        if (doctor != null)
        {
            doctor.WalkSpeed = 150 * (1 + value);
            GD.Print($"[TalentTree] 博士移速更新: {doctor.WalkSpeed}");
        }
    }

    // ============================================================
    // 静态加成（供其他系统查询）
    // ============================================================

    private static float _operatorAttackSpeedBonus;
    private static float _operatorExpBonus;
    private static int _operatorStartLevelBonus;
    private static float _towerBuildCostReduction;
    private static float _towerUpgradeCostReduction;
    private static float _coreRepairBonus;
    private static int _gatherAmountBonus;
    private static float _lootDropRateBonus;

    public static float OperatorAttackSpeedBonus => _operatorAttackSpeedBonus;
    public static float OperatorExpBonus => _operatorExpBonus;
    public static int OperatorStartLevelBonus => _operatorStartLevelBonus;
    public static float TowerBuildCostReduction => _towerBuildCostReduction;
    public static float TowerUpgradeCostReduction => _towerUpgradeCostReduction;
    public static float CoreRepairBonus => _coreRepairBonus;
    public static int GatherAmountBonus => _gatherAmountBonus;
    public static float LootDropRateBonus => _lootDropRateBonus;

    // ============================================================
    // 辅助方法
    // ============================================================

    private Backpack GetBackpack()
    {
        return GetDoctor()?.GetNodeOrNull<Backpack>("Backpack");
    }

    private Doctor GetDoctor()
    {
        return GetTree().GetFirstNodeInGroup("doctor") as Doctor;
    }

    private void OnClosePressed()
    {
        Hide();
    }
}
