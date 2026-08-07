using Godot;
using OutpostProtocol.Managers;

/// <summary>
/// 设置系统自动化测试：
/// 音量设置生效 → 键位重绑生效 → 持久化回读 → 恢复默认
/// </summary>
public partial class TestSettingsController : Node
{
    private int _frameCount;

    public override void _Ready()
    {
        GD.Print("========== 设置系统测试 ==========");
    }

    public override void _Process(double delta)
    {
        _frameCount++;

        if (_frameCount == 5)
        {
            SettingsManager.Instance.SetBusVolume("Master", -12f);
            int index = AudioServer.GetBusIndex("Master");
            GD.Print($"[TestSettings] 主音量: {AudioServer.GetBusVolumeDb(index):F0} dB（应为 -12）");
        }
        else if (_frameCount == 8)
        {
            // 多键支持：追加 P 和 → 两个键位
            SettingsManager.Instance.RebindAction("move_right", 80); // P
            SettingsManager.Instance.RebindAction("move_right", 4194321); // →
            var events = InputMap.ActionGetEvents("move_right");
            GD.Print($"[TestSettings] move_right 绑定数: {events.Count}（默认2+追加P=3）, 键: {string.Join(" ", GetKeyNames(events))}");
        }
        else if (_frameCount == 11)
        {
            SettingsManager.Instance.Save();
            SettingsManager.Instance.Load();
            var keys = SettingsManager.Instance.GetActionKeys("move_right");
            GD.Print($"[TestSettings] 回读键位数: {keys.Count}（应为 3）, 音量: {SettingsManager.Instance.GetBusVolume("Master"):F0} dB");
        }
        else if (_frameCount == 14)
        {
            SettingsManager.Instance.ClearActionKeys("move_right");
            GD.Print($"[TestSettings] 清空后绑定数: {InputMap.ActionGetEvents("move_right").Count}（应为 0）");
        }
        else if (_frameCount == 16)
        {
            SettingsManager.Instance.ResetDefaults();
            GD.Print($"[TestSettings] 恢复默认绑定数: {InputMap.ActionGetEvents("move_right").Count}（应为 2）, Master={SettingsManager.Instance.GetBusVolume("Master"):F0} dB");
        }
        else if (_frameCount >= 19)
        {
            GD.Print("[TestSettings] 测试完成");
            GetTree().Quit();
        }
    }

    private static string GetKeyNames(Godot.Collections.Array<InputEvent> events)
    {
        var names = new System.Collections.Generic.List<string>();
        foreach (var e in events)
        {
            if (e is InputEventKey keyEvent)
            {
                names.Add(OS.GetKeycodeString(keyEvent.PhysicalKeycode));
            }
        }
        return string.Join(" ", names);
    }
}
