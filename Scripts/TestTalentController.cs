using Godot;
using OutpostProtocol.Data;
using OutpostProtocol.Gameplay.Character.Doctor;
using OutpostProtocol.Gameplay.Inventory;
using OutpostProtocol.Managers;
using OutpostProtocol.UI.Controllers;
using System.Collections.Generic;

/// <summary>
/// 天赋树自动化测试：
/// 升级天赋 → 点数消耗 → 背包/博士/采集效果生效 → 存档持久化 → 还原存档
/// </summary>
public partial class TestTalentController : Node
{
    private Doctor _doctor;
    private GatherableResource _resource;
    private TalentTreeController _talentTree;
    private Backpack _backpack;
    private SaveProfile _profile;
    private int _savedPoints;
    private Dictionary<string, int> _savedLevels = new();
    private int _frameCount;
    private bool _started;

    public override void _Ready()
    {
        _doctor = GetNode<Doctor>("../Doctor");
        _resource = GetNode<GatherableResource>("../Gatherable_1");
        _talentTree = GetNode<TalentTreeController>("../TalentTree");
        _backpack = _doctor.Backpack;
        _profile = SaveManager.Instance.Profile;

        _savedPoints = _profile.TotalTalentPoints;
        _savedLevels = new Dictionary<string, int>(_profile.TalentLevels);

        GD.Print("========== 天赋树测试 ==========");
    }

    public override void _Process(double delta)
    {
        _frameCount++;

        if (!_started && DataManager.Instance.IsLoaded && _talentTree.Visible == false)
        {
            _started = true;
            _frameCount = 0;
        }

        if (!_started) return;

        if (_frameCount == 3)
        {
            // 重置为确定性状态
            _profile.TotalTalentPoints = 10;
            _profile.TalentLevels.Clear();
            SaveManager.Instance.SaveProfile();
            GD.Print($"[TestTalent] 初始点数: {_profile.TotalTalentPoints}");
        }
        else if (_frameCount == 5)
        {
            bool ok = _talentTree.UpgradeTalent("backpack_capacity");
            GD.Print($"[TestTalent] 背包扩容: {ok}, 容量={_backpack.MaxCapacity}, 点数={_profile.TotalTalentPoints}");
        }
        else if (_frameCount == 7)
        {
            bool ok = _talentTree.UpgradeTalent("doctor_health");
            GD.Print($"[TestTalent] 生命强化: {ok}, 博士生命上限={_doctor.MaxHealthValue}, 点数={_profile.TotalTalentPoints}");
        }
        else if (_frameCount == 9)
        {
            bool ok = _talentTree.UpgradeTalent("gather_amount");
            GD.Print($"[TestTalent] 采集能手: {ok}, 采集加成={TalentTreeController.GatherAmountBonus}");
        }
        else if (_frameCount == 11)
        {
            bool gathered = _resource.Gather();
            GD.Print($"[TestTalent] 采集一次: {gathered}, 木材={_backpack.GetCount(Backpack.WOOD_ITEM_ID)}（应为 2）");
        }
        else if (_frameCount == 13)
        {
            // 点数不足测试
            _profile.TotalTalentPoints = 0;
            bool fail = _talentTree.UpgradeTalent("loot_drop");
            GD.Print($"[TestTalent] 点数不足升级: {fail}（应为 False）, 等级数={_profile.TalentLevels.Count}");
        }
        else if (_frameCount >= 16)
        {
            // 还原存档，避免污染其他测试
            _profile.TotalTalentPoints = _savedPoints;
            _profile.TalentLevels = new Dictionary<string, int>(_savedLevels);
            SaveManager.Instance.SaveProfile();
            GD.Print("[TestTalent] 测试完成，存档已还原");
            GetTree().Quit();
        }
    }
}
