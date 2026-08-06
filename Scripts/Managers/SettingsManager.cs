using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace OutpostProtocol.Managers;

/// <summary>
/// 设置管理器（AutoLoad 单例）
/// 职责：音量/键位设置的持久化与应用
/// </summary>
public partial class SettingsManager : Node
{
    private static SettingsManager _instance;
    public static SettingsManager Instance => _instance;

    private const string SETTINGS_PATH = "user://settings.json";

    /// <summary>默认主键位（physical keycode）</summary>
    public static readonly Dictionary<string, int> DefaultKeys = new()
    {
        ["move_up"] = 87, // W
        ["move_down"] = 83, // S
        ["move_left"] = 65, // A
        ["move_right"] = 68, // D
        ["sprint"] = 4194325, // Shift
        ["interact"] = 69, // E
    };

    private static readonly string[] VolumeBuses = { "Master", "Music", "SFX" };
    private static readonly Dictionary<string, float> DefaultVolumes = new()
    {
        ["Master"] = 0f,
        ["Music"] = -6f,
        ["SFX"] = -6f,
    };

    private GameSettings _settings = new();

    /// <summary>设置数据结构（JSON 持久化）</summary>
    public class GameSettings
    {
        public Dictionary<string, float> Volumes { get; set; } = new();
        public Dictionary<string, int> KeyBindings { get; set; } = new();
    }

    public override void _Ready()
    {
        if (_instance != null)
        {
            GD.PushWarning("SettingsManager 已存在，销毁重复实例");
            QueueFree();
            return;
        }
        _instance = this;

        EnsureAudioBuses();
        Load();
        ApplyAll();

        GD.Print("[SettingsManager] 初始化完成");
    }

    public override void _ExitTree()
    {
        _instance = null;
    }

    // ============================================================
    // 音频总线
    // ============================================================

    private static void EnsureAudioBuses()
    {
        foreach (var bus in VolumeBuses)
        {
            if (AudioServer.GetBusIndex(bus) == -1)
            {
                AudioServer.AddBus();
                AudioServer.SetBusName(AudioServer.BusCount - 1, bus);
            }
        }
    }

    // ============================================================
    // 音量
    // ============================================================

    public float GetBusVolume(string bus)
    {
        return _settings.Volumes.GetValueOrDefault(bus, DefaultVolumes.GetValueOrDefault(bus, 0f));
    }

    public void SetBusVolume(string bus, float db)
    {
        _settings.Volumes[bus] = db;
        int index = AudioServer.GetBusIndex(bus);
        if (index != -1)
        {
            AudioServer.SetBusVolumeDb(index, db);
        }
        Save();
    }

    // ============================================================
    // 键位
    // ============================================================

    public int GetActionKey(string action)
    {
        return _settings.KeyBindings.GetValueOrDefault(action, DefaultKeys.GetValueOrDefault(action, 0));
    }

    public void RebindAction(string action, int physicalKeycode)
    {
        _settings.KeyBindings[action] = physicalKeycode;
        ApplyBinding(action, physicalKeycode);
        Save();
    }

    private static void ApplyBinding(string action, int physicalKeycode)
    {
        if (!InputMap.HasAction(action)) return;

        InputMap.ActionEraseEvents(action);
        var keyEvent = new InputEventKey
        {
            PhysicalKeycode = (Key)physicalKeycode,
            Keycode = (Key)physicalKeycode,
        };
        InputMap.ActionAddEvent(action, keyEvent);
    }

    // ============================================================
    // 默认值
    // ============================================================

    public void ResetDefaults()
    {
        _settings.Volumes.Clear();
        _settings.KeyBindings.Clear();
        ApplyAll();
        Save();
        GD.Print("[SettingsManager] 已恢复默认设置");
    }

    // ============================================================
    // 应用 / 持久化
    // ============================================================

    private void ApplyAll()
    {
        foreach (var bus in VolumeBuses)
        {
            int index = AudioServer.GetBusIndex(bus);
            if (index != -1)
            {
                AudioServer.SetBusVolumeDb(index, GetBusVolume(bus));
            }
        }

        foreach (var action in DefaultKeys.Keys)
        {
            ApplyBinding(action, GetActionKey(action));
        }
    }

    public void Save()
    {
        try
        {
            string path = ProjectSettings.GlobalizePath(SETTINGS_PATH);
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(path, JsonSerializer.Serialize(_settings, options));
        }
        catch (Exception ex)
        {
            GD.PushError($"[SettingsManager] 保存失败: {ex.Message}");
        }
    }

    public void Load()
    {
        try
        {
            string path = ProjectSettings.GlobalizePath(SETTINGS_PATH);
            if (!File.Exists(path))
            {
                return;
            }

            var loaded = JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(path));
            if (loaded != null)
            {
                _settings = loaded;
            }
        }
        catch (Exception ex)
        {
            GD.PushError($"[SettingsManager] 加载失败: {ex.Message}");
        }
    }
}
