using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Data;
using OutpostProtocol.Gameplay.Character.Doctor;
using OutpostProtocol.Gameplay.Character.Operator;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace OutpostProtocol.Managers;

/// <summary>
/// 存档管理器（AutoLoad 单例）
/// 职责：双文件存档读写（Profile + RunSave）
/// 硬核删除：博士死亡仅删除 RunSave，Profile 保留
/// </summary>
public partial class SaveManager : Node
{
    // ============================================================
    // 单例
    // ============================================================

    private static SaveManager _instance;

    /// <summary>全局单例实例（AutoLoad 就绪后可用）</summary>
    public static SaveManager Instance => _instance;

    // ============================================================
    // 文件路径
    // ============================================================

    private const string PROFILE_PATH = "user://profile.save";
    private const string RUN_DIR = "user://runs/";

    // ============================================================
    // 运行时数据
    // ============================================================

    private SaveProfile _profile;
    private RunSave _currentRun;
    private string _currentRunPath;

    // ============================================================
    // 公共属性
    // ============================================================

    public SaveProfile Profile => _profile;
    public RunSave CurrentRun => _currentRun;
    public bool HasProfile => _profile != null;
    public bool HasRun => _currentRun != null;

    // ============================================================
    // 事件
    // ============================================================

    public event Action<SaveProfile> ProfileLoaded;
    public event Action<RunSave> RunLoaded;
    public event Action RunSaved;
    public event Action RunDeleted;

    // ============================================================
    // 生命周期
    // ============================================================

    public override void _Ready()
    {
        if (_instance != null)
        {
            GD.PushWarning("SaveManager 已存在，销毁重复实例");
            QueueFree();
            return;
        }
        _instance = this;

        // 确保存档目录存在
        EnsureRunDirectory();

        // 加载全局档案
        LoadProfile();

        // 订阅事件
        EventBus.Instance.DoctorDied += OnDoctorDied;
        EventBus.Instance.GameStateChanged += OnGameStateChanged;

        GD.Print("[SaveManager] 初始化完成");
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.DoctorDied -= OnDoctorDied;
            EventBus.Instance.GameStateChanged -= OnGameStateChanged;
        }
        _instance = null;
    }

    // ============================================================
    // 目录管理
    // ============================================================

    private void EnsureRunDirectory()
    {
        if (!DirAccess.DirExistsAbsolute(RUN_DIR))
        {
            DirAccess.MakeDirRecursiveAbsolute(RUN_DIR);
            GD.Print($"[SaveManager] 创建存档目录: {RUN_DIR}");
        }
    }

    // ============================================================
    // Profile 存档（全局档案）
    // ============================================================

    /// <summary>加载全局档案</summary>
    public void LoadProfile()
    {
        string path = ProjectSettings.GlobalizePath(PROFILE_PATH);

        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                };
                _profile = JsonSerializer.Deserialize<SaveProfile>(json, options);

                if (_profile == null)
                {
                    GD.PushWarning("[SaveManager] Profile 反序列化失败，创建新档案");
                    _profile = new SaveProfile();
                }

                GD.Print($"[SaveManager] Profile 加载成功 — 藏品:{_profile.UnlockedCollectionIds.Count}, 信赖:{_profile.TrustData.Count}");
                ProfileLoaded?.Invoke(_profile);
            }
            catch (Exception ex)
            {
                GD.PushError($"[SaveManager] Profile 加载失败: {ex.Message}");
                _profile = new SaveProfile();
            }
        }
        else
        {
            GD.Print("[SaveManager] 未找到 Profile，创建新档案");
            _profile = new SaveProfile();
            SaveProfile();
        }
    }

    /// <summary>保存全局档案</summary>
    public void SaveProfile()
    {
        if (_profile == null) return;

        try
        {
            string path = ProjectSettings.GlobalizePath(PROFILE_PATH);
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
            };
            string json = JsonSerializer.Serialize(_profile, options);
            File.WriteAllText(path, json);

            GD.Print("[SaveManager] Profile 保存成功");
        }
        catch (Exception ex)
        {
            GD.PushError($"[SaveManager] Profile 保存失败: {ex.Message}");
        }
    }

    // ============================================================
    // Run 存档（对局存档）
    // ============================================================

    /// <summary>创建新的对局存档</summary>
    public void NewRun()
    {
        _currentRun = new RunSave
        {
            CurrentDate = DateTime.Now.ToString("yyyyMMdd_HHmmss"),
            CurrentPhase = (int)DayPhase.Morning,
            DayCount = 1,
            WaveLevel = 1,
        };

        SaveRun();
        GD.Print("[SaveManager] 新对局存档创建成功");
    }

    /// <summary>保存当前对局存档</summary>
    public void SaveRun()
    {
        if (_currentRun == null)
        {
            GD.PushWarning("[SaveManager] 没有对局存档可保存");
            return;
        }

        try
        {
            string filename = $"run_{_currentRun.CurrentDate}.save";
            string fullPath = RUN_DIR + filename;
            string physicalPath = ProjectSettings.GlobalizePath(fullPath);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
            };
            string json = JsonSerializer.Serialize(_currentRun, options);
            File.WriteAllText(physicalPath, json);

            _currentRunPath = fullPath;
            RunSaved?.Invoke();

            GD.Print($"[SaveManager] Run 保存成功: {filename}");
        }
        catch (Exception ex)
        {
            GD.PushError($"[SaveManager] Run 保存失败: {ex.Message}");
        }
    }

    /// <summary>加载对局存档（默认自动加载最新）</summary>
    public bool LoadRun(string path = null)
    {
        try
        {
            string fullPath;

            if (string.IsNullOrEmpty(path))
            {
                var files = GetRunFiles();
                if (files.Count == 0)
                {
                    GD.Print("[SaveManager] 没有找到对局存档");
                    return false;
                }

                // 按文件名倒序排列（最新的在前，yyyyMMdd_HHmmss 字典序即时间序）
                files.Sort((a, b) => string.Compare(b, a, StringComparison.Ordinal));
                fullPath = RUN_DIR + files[0];
            }
            else
            {
                fullPath = path;
            }

            string physicalPath = ProjectSettings.GlobalizePath(fullPath);
            if (!File.Exists(physicalPath))
            {
                GD.PushWarning($"[SaveManager] 存档文件不存在: {fullPath}");
                return false;
            }

            string json = File.ReadAllText(physicalPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };
            _currentRun = JsonSerializer.Deserialize<RunSave>(json, options);

            if (_currentRun == null)
            {
                GD.PushWarning("[SaveManager] Run 反序列化失败");
                return false;
            }

            _currentRunPath = fullPath;
            RunLoaded?.Invoke(_currentRun);

            GD.Print($"[SaveManager] Run 加载成功: {Path.GetFileName(fullPath)}");
            return true;
        }
        catch (Exception ex)
        {
            GD.PushError($"[SaveManager] Run 加载失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>删除当前对局存档（博士死亡时调用）</summary>
    public void DeleteCurrentRun()
    {
        if (string.IsNullOrEmpty(_currentRunPath))
        {
            GD.Print("[SaveManager] 没有对局存档可删除");
            return;
        }

        try
        {
            string physicalPath = ProjectSettings.GlobalizePath(_currentRunPath);
            if (File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
                GD.Print($"[SaveManager] Run 删除成功: {Path.GetFileName(_currentRunPath)}");
            }

            _currentRun = null;
            _currentRunPath = null;
            RunDeleted?.Invoke();
        }
        catch (Exception ex)
        {
            GD.PushError($"[SaveManager] Run 删除失败: {ex.Message}");
        }
    }

    /// <summary>获取所有对局存档文件列表</summary>
    public List<string> GetRunFiles()
    {
        var files = new List<string>();
        var dir = DirAccess.Open(RUN_DIR);

        if (dir == null) return files;

        dir.ListDirBegin();
        string fileName = dir.GetNext();

        while (!string.IsNullOrEmpty(fileName))
        {
            if (fileName.StartsWith("run_") && fileName.EndsWith(".save"))
            {
                files.Add(fileName);
            }
            fileName = dir.GetNext();
        }

        dir.ListDirEnd();
        return files;
    }

    // ============================================================
    // 从游戏状态更新 Run 存档
    // ============================================================

    /// <summary>从当前游戏状态更新 Run 存档数据</summary>
    public void UpdateRunFromGame()
    {
        if (_currentRun == null) return;

        // 1. 博士状态
        var doctor = GetDoctor();
        if (doctor != null)
        {
            _currentRun.DoctorPosX = doctor.GlobalPosition.X;
            _currentRun.DoctorPosY = doctor.GlobalPosition.Y;
            _currentRun.DoctorHealth = doctor.CurrentHealth;
            _currentRun.DoctorStamina = doctor.CurrentStamina;
        }

        // 2. 干员状态
        _currentRun.Operators.Clear();
        var operators = GetOperators();
        foreach (var op in operators)
        {
            if (op == null || op.Data == null) continue;

            var runtime = op.ExportRuntime();
            if (runtime == null) continue;

            // 从 Profile 读取信赖度
            if (_profile != null && _profile.TrustData.TryGetValue(op.OperatorDataId, out int trust))
            {
                runtime.Trust = trust;
            }

            _currentRun.Operators.Add(runtime);
        }

        // 3. 游戏状态
        var gameManager = GameManager.Instance;
        if (gameManager != null)
        {
            _currentRun.DayCount = gameManager.DayCount;
            _currentRun.CurrentPhase = (int)gameManager.CurrentPhase;
            // 波次等级从 EnemySpawner 读取（后续实现）
        }

        SaveRun();
    }

    // ============================================================
    // 从存档恢复游戏状态
    // ============================================================

    /// <summary>从 Run 存档恢复游戏状态</summary>
    public void RestoreGameFromRun()
    {
        if (_currentRun == null)
        {
            GD.PushWarning("[SaveManager] 没有对局存档可恢复");
            return;
        }

        // 1. 恢复博士
        var doctor = GetDoctor();
        if (doctor != null)
        {
            doctor.RestorePosition(new Vector2(_currentRun.DoctorPosX, _currentRun.DoctorPosY));
            doctor.SetHealth(_currentRun.DoctorHealth);
            doctor.SetStamina(_currentRun.DoctorStamina);
        }

        // 2. 恢复干员（由外部系统按存档重建场景后逐个 RestoreFromRuntime）
        // 3. 恢复游戏状态
        var gameManager = GameManager.Instance;
        if (gameManager != null)
        {
            gameManager.RestoreState(new SaveState
            {
                DayCount = _currentRun.DayCount,
                CurrentPhase = _currentRun.CurrentPhase,
                CurrentState = (int)GameState.Explore,
            });
        }

        GD.Print("[SaveManager] 游戏状态从存档恢复完成");
    }

    // ============================================================
    // 辅助方法
    // ============================================================

    /// <summary>获取博士节点</summary>
    private Doctor GetDoctor()
    {
        var world = GetTree().CurrentScene?.GetNodeOrNull<Node2D>("World");
        if (world == null) return null;

        return world.GetNodeOrNull<Doctor>("Doctor");
    }

    /// <summary>获取所有干员</summary>
    private List<Operator> GetOperators()
    {
        var result = new List<Operator>();

        foreach (var node in GetTree().GetNodesInGroup("operators"))
        {
            if (node is Operator op)
            {
                result.Add(op);
            }
        }

        return result;
    }

    // ============================================================
    // 事件回调
    // ============================================================

    private void OnDoctorDied()
    {
        // 博士死亡 → 更新存档为 GameOver 状态
        if (_currentRun != null)
        {
            _currentRun.IsGameOver = true;
            SaveRun();
        }

        // 硬核删除：删除 Run 存档，Profile 保留
        DeleteCurrentRun();

        GD.Print("[SaveManager] 博士死亡，Run 存档已删除（硬核模式）");
    }

    private void OnGameStateChanged(GameState newState)
    {
        // 在关键状态切换时自动存档
        if (newState == GameState.Rest || newState == GameState.Build)
        {
            UpdateRunFromGame();
        }
    }
}
