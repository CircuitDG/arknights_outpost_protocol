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
            SettingsManager.Instance.RebindAction("move_right", 80); // P
            var events = InputMap.ActionGetEvents("move_right");
            var keyEvent = events.Count > 0 ? events[0] as InputEventKey : null;
            GD.Print($"[TestSettings] move_right 绑定数: {events.Count}（应为 1）, 键: {OS.GetKeycodeString(keyEvent?.PhysicalKeycode ?? Key.None)}（应为 P）");
        }
        else if (_frameCount == 11)
        {
            // 持久化回读
            SettingsManager.Instance.Save();
            SettingsManager.Instance.Load();
            int key = SettingsManager.Instance.GetActionKey("move_right");
            GD.Print($"[TestSettings] 回读键位: {OS.GetKeycodeString((Key)key)}（应为 P）, 音量: {SettingsManager.Instance.GetBusVolume("Master"):F0} dB");
        }
        else if (_frameCount == 14)
        {
            SettingsManager.Instance.ResetDefaults();
            GD.Print($"[TestSettings] 恢复默认: move_right={OS.GetKeycodeString((Key)SettingsManager.Instance.GetActionKey("move_right"))}, Master={SettingsManager.Instance.GetBusVolume("Master"):F0} dB");
        }
        else if (_frameCount >= 17)
        {
            GD.Print("[TestSettings] 测试完成");
            GetTree().Quit();
        }
    }
}
