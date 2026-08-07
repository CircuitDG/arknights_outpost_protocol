using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Data;
using OutpostProtocol.Gameplay.Character.Doctor;
using OutpostProtocol.Gameplay.Character.Operator;
using OutpostProtocol.Managers;

/// <summary>
/// 存档系统自动化测试：
/// Profile 保存/读取持久化 → Run 新建/更新/回读 → 删除 → 硬核删档（博士死亡）
/// </summary>
public partial class TestSaveController : Node
{
    private SaveManager _save;
    private Doctor _doctor;
    private Operator _op;
    private int _frameCount;
    private int _testFrames;
    private bool _started;

    public override void _Ready()
    {
        _save = SaveManager.Instance;
        _doctor = GetNodeOrNull<Doctor>("../World/Doctor");
        _op = GetNodeOrNull<Operator>("../World/Operator_1");

        if (_save == null)
        {
            GD.PrintErr("[TestSave] SaveManager 未初始化");
            return;
        }

        GD.Print("========== SaveManager 测试 ==========");
        GD.Print("自动化流程：Profile 持久化 → NewRun → Update → Load → Delete → 硬核删档");
        GD.Print("======================================");
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            switch (keyEvent.Keycode)
            {
                case Key.P: _save.SaveProfile(); break;
                case Key.L: _save.LoadProfile(); ShowProfileInfo(); break;
                case Key.N: _save.NewRun(); ShowRunInfo(); break;
                case Key.S: _save.SaveRun(); break;
                case Key.R:
                    if (_save.LoadRun()) ShowRunInfo();
                    else GD.Print("[TestSave] 没有找到 Run 存档");
                    break;
                case Key.D: _save.DeleteCurrentRun(); break;
                case Key.U: _save.UpdateRunFromGame(); ShowRunInfo(); break;
                case Key.I: ShowInfo(); break;
            }
        }
    }

    public override void _Process(double delta)
    {
        _frameCount++;

        // 等 DataManager 和干员数据就绪后开始自动化
        if (!_started && DataManager.Instance.IsLoaded && _op != null && _op.Data != null)
        {
            _started = true;
            _testFrames = 0;
            ShowProfileInfo();
        }

        if (!_started) return;
        _testFrames++;

        switch (_testFrames)
        {
            case 8:
                TestProfilePersistence();
                break;

            case 12:
                _save.NewRun();
                ShowRunInfo();
                break;

            case 15:
                _save.UpdateRunFromGame();
                GD.Print("[TestSave] 已从游戏状态更新 Run");
                ShowRunInfo();
                break;

            case 18:
                TestLoadRun();
                break;

            case 21:
                TestDeleteRun();
                break;

            case 24:
                // 重新创建 Run，用于硬核删除测试
                _save.NewRun();
                _save.UpdateRunFromGame();
                GD.Print("[TestSave] 已重新创建 Run（硬核删除测试准备）");
                break;

            case 27:
                TestHardcoreDelete();
                break;

            case 32:
                GD.Print("[TestSave] 测试完成");
                GetTree().Quit();
                break;
        }
    }

    // ============================================================
    // 测试步骤
    // ============================================================

    private void TestProfilePersistence()
    {
        _save.Profile.TotalTalentPoints = 7;
        _save.Profile.TrustData[1001] = 55;
        _save.SaveProfile();
        _save.LoadProfile();

        GD.Print($"[TestSave] Profile 持久化验证 — 天赋点:{_save.Profile.TotalTalentPoints}, 信赖数据:{_save.Profile.TrustData.Count} 个干员");
    }

    private void TestLoadRun()
    {
        if (_save.LoadRun())
        {
            var run = _save.CurrentRun;
            GD.Print($"[TestSave] Run 回读验证 — 日期:{run.CurrentDate}, 天数:{run.DayCount}, 阶段:{(DayPhase)run.CurrentPhase}, 干员:{run.Operators.Count} 个, 博士位置:({run.DoctorPosX:F0},{run.DoctorPosY:F0}), HP:{run.DoctorHealth:F0}");
        }
        else
        {
            GD.Print("[TestSave] LoadRun 失败");
        }
    }

    private void TestDeleteRun()
    {
        _save.DeleteCurrentRun();
        GD.Print($"[TestSave] 删除后 Run 文件数: {_save.GetRunFiles().Count}");
        ShowProfileInfo();
    }

    private void TestHardcoreDelete()
    {
        GameManager.Instance.GameOver();
        GD.Print($"[TestSave] 硬核删除后 — Run 文件数:{_save.GetRunFiles().Count}, HasRun:{_save.HasRun}, HasProfile:{_save.HasProfile}");
    }

    // ============================================================
    // 信息显示
    // ============================================================

    private void ShowProfileInfo()
    {
        var profile = _save.Profile;
        if (profile == null)
        {
            GD.Print("[TestSave] Profile 为空");
            return;
        }

        GD.Print("========== Profile 信息 ==========");
        GD.Print($"天赋点: {profile.TotalTalentPoints}");
        GD.Print($"藏品图鉴: {profile.UnlockedCollectionIds.Count} 个");
        GD.Print($"信赖数据: {profile.TrustData.Count} 个干员");
        GD.Print($"总存活天数: {profile.TotalDaysSurvived}");
        GD.Print("=====================================");
    }

    private void ShowRunInfo()
    {
        var run = _save.CurrentRun;
        if (run == null)
        {
            GD.Print("[TestSave] Run 为空");
            return;
        }

        GD.Print("========== Run 信息 ==========");
        GD.Print($"日期: {run.CurrentDate}");
        GD.Print($"天数: {run.DayCount}");
        GD.Print($"阶段: {(DayPhase)run.CurrentPhase}");
        GD.Print($"干员: {run.Operators.Count} 个");
        GD.Print($"塔: {run.Towers.Count} 个");
        GD.Print($"藏品: {run.OwnedCollections.Count} 个");
        GD.Print($"博士位置: ({run.DoctorPosX:F0}, {run.DoctorPosY:F0})");
        GD.Print($"博士血量: {run.DoctorHealth:F0}");
        GD.Print($"是否结束: {run.IsGameOver}");
        GD.Print("=====================================");
    }

    private void ShowInfo()
    {
        ShowProfileInfo();
        ShowRunInfo();
    }
}
