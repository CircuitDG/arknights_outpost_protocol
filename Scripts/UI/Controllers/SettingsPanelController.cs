using Godot;
using OutpostProtocol.Managers;

namespace OutpostProtocol.UI.Controllers;

/// <summary>
/// 设置面板控制器
/// 音量滑块（Master/Music/SFX）+ 键位重绑 + 恢复默认
/// </summary>
public partial class SettingsPanelController : Control
{
    private readonly string[] _actions =
    {
        "move_up", "move_down", "move_left", "move_right", "sprint", "interact",
    };

    private readonly string[] _actionNames =
    {
        "向上", "向下", "向左", "向右", "冲刺", "交互",
    };

    private HSlider _masterSlider;
    private HSlider _musicSlider;
    private HSlider _sfxSlider;
    private Button _resetButton;
    private Button _closeButton;
    private Label _captureLabel;

    private readonly Label[] _keyLabels = new Label[6];
    private readonly Button[] _rebindButtons = new Button[6];
    private int _capturingIndex = -1;

    public override void _Ready()
    {
        _masterSlider = GetNodeOrNull<HSlider>("Panel/MainContainer/MasterSlider");
        _musicSlider = GetNodeOrNull<HSlider>("Panel/MainContainer/MusicSlider");
        _sfxSlider = GetNodeOrNull<HSlider>("Panel/MainContainer/SfxSlider");
        _resetButton = GetNodeOrNull<Button>("Panel/MainContainer/ResetButton");
        _closeButton = GetNodeOrNull<Button>("Panel/MainContainer/CloseButton");
        _captureLabel = GetNodeOrNull<Label>("Panel/MainContainer/CaptureLabel");

        for (int i = 0; i < _actions.Length; i++)
        {
            _keyLabels[i] = GetNodeOrNull<Label>($"Panel/MainContainer/KeyRows/KeyRow_{i + 1}/KeyLabel");
            _rebindButtons[i] = GetNodeOrNull<Button>($"Panel/MainContainer/KeyRows/KeyRow_{i + 1}/RebindButton");

            if (_rebindButtons[i] != null)
            {
                int index = i;
                _rebindButtons[i].Pressed += () => OnRebindPressed(index);
            }
        }

        if (_masterSlider != null) _masterSlider.ValueChanged += v => OnVolumeChanged("Master", v);
        if (_musicSlider != null) _musicSlider.ValueChanged += v => OnVolumeChanged("Music", v);
        if (_sfxSlider != null) _sfxSlider.ValueChanged += v => OnVolumeChanged("SFX", v);
        if (_resetButton != null) _resetButton.Pressed += OnResetPressed;
        if (_closeButton != null) _closeButton.Pressed += () => Hide();

        Refresh();
        Hide();
        GD.Print("[SettingsPanel] 初始化完成");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_capturingIndex < 0 || @event is not InputEventKey keyEvent || !keyEvent.Pressed) return;

        // ESC 取消重绑
        if (keyEvent.PhysicalKeycode == Key.Escape)
        {
            CancelCapture();
            return;
        }

        int code = keyEvent.PhysicalKeycode != Key.None
            ? (int)keyEvent.PhysicalKeycode
            : (int)keyEvent.Keycode;

        SettingsManager.Instance.RebindAction(_actions[_capturingIndex], code);
        GD.Print($"[SettingsPanel] 重绑 {_actions[_capturingIndex]} → {OS.GetKeycodeString((Key)code)}");
        CancelCapture();
        Refresh();
    }

    // ============================================================
    // 交互
    // ============================================================

    private void OnVolumeChanged(string bus, double value)
    {
        SettingsManager.Instance.SetBusVolume(bus, (float)value);
    }

    private void OnRebindPressed(int index)
    {
        _capturingIndex = index;
        if (_captureLabel != null) _captureLabel.Text = $"请按下新的按键（ESC 取消）: {_actionNames[index]}";
        if (_rebindButtons[index] != null) _rebindButtons[index].Text = "监听中...";
    }

    private void OnResetPressed()
    {
        SettingsManager.Instance.ResetDefaults();
        Refresh();
    }

    private void CancelCapture()
    {
        if (_capturingIndex >= 0 && _rebindButtons[_capturingIndex] != null)
        {
            _rebindButtons[_capturingIndex].Text = "更改";
        }
        _capturingIndex = -1;
        if (_captureLabel != null) _captureLabel.Text = "";
    }

    private void Refresh()
    {
        var sm = SettingsManager.Instance;
        if (sm == null) return;

        if (_masterSlider != null) _masterSlider.Value = sm.GetBusVolume("Master");
        if (_musicSlider != null) _musicSlider.Value = sm.GetBusVolume("Music");
        if (_sfxSlider != null) _sfxSlider.Value = sm.GetBusVolume("SFX");

        for (int i = 0; i < _actions.Length; i++)
        {
            if (_keyLabels[i] != null)
            {
                int code = sm.GetActionKey(_actions[i]);
                _keyLabels[i].Text = OS.GetKeycodeString((Key)code);
            }
            if (_rebindButtons[i] != null) _rebindButtons[i].Text = "更改";
        }

        CancelCapture();
    }
}
