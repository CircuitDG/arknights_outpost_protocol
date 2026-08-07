using Godot;
using System.Collections.Generic;

namespace OutpostProtocol.Managers;

/// <summary>
/// 音频管理器（AutoLoad 单例）
/// 预加载常用音效，通过名称播放
/// </summary>
public partial class AudioManager : Node
{
    private static AudioManager _instance;
    public static AudioManager Instance => _instance;

    private readonly Dictionary<string, AudioStreamPlayer> _players = new();

    private static readonly string[] SoundNames =
    {
        "ui_click", "pickup", "build", "shoot", "heal",
        "enemy_die", "wave_start", "skill", "collection", "hurt",
    };

    public override void _Ready()
    {
        if (_instance != null)
        {
            GD.PushWarning("AudioManager 已存在，销毁重复实例");
            QueueFree();
            return;
        }
        _instance = this;

        foreach (var name in SoundNames)
        {
            var stream = GD.Load<AudioStream>($"res://Assets/Audio/{name}.wav");
            if (stream == null) continue;
            var player = new AudioStreamPlayer
            {
                Stream = stream,
                VolumeDb = -8f,
                Name = name,
            };
            AddChild(player);
            _players[name] = player;
        }

        GD.Print("[AudioManager] 初始化完成");
    }

    public override void _ExitTree()
    {
        _instance = null;
    }

    public void Play(string name, float volumeDb = -8f)
    {
        if (_players.TryGetValue(name, out var player))
        {
            player.VolumeDb = volumeDb;
            player.Play();
        }
    }
}
