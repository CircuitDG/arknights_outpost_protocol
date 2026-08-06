using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Data;

namespace OutpostProtocol.Managers;

/// <summary>
/// 游戏状态管理器（AutoLoad 单例）
/// 负责：状态机切换、昼夜循环驱动、阶段计时
/// </summary>
public partial class GameManager : Node
{
    // ============================================================
    // 单例
    // ============================================================

    private static GameManager _instance;

    /// <summary>全局单例实例（AutoLoad 就绪后可用）</summary>
    public static GameManager Instance => _instance;

    // ============================================================
    // 导出变量（可在编辑器中调整，单位：秒）
    // ============================================================

    /// <summary>探索期：6 小时 → 360 秒</summary>
    [Export] public float ExploreDuration = 360.0f;

    /// <summary>建设期：3 小时 → 180 秒</summary>
    [Export] public float BuildDuration = 180.0f;

    /// <summary>防守期：4 小时 → 240 秒</summary>
    [Export] public float BattleDuration = 240.0f;

    /// <summary>休整期：1 小时 → 60 秒</summary>
    [Export] public float RestDuration = 60.0f;

    // ============================================================
    // 运行时状态
    // ============================================================

    private GameState _currentState = GameState.Loading;
    private DayPhase _currentPhase = DayPhase.Dawn;
    private float _phaseTimer;
    private float _totalElapsed;
    private int _dayCount = 1;

    /// <summary>当前阶段总时长（秒）</summary>
    private float CurrentPhaseDuration => _currentState switch
    {
        GameState.Explore => ExploreDuration,
        GameState.Build => BuildDuration,
        GameState.Battle => BattleDuration,
        GameState.Rest => RestDuration,
        _ => 60.0f,
    };

    // ============================================================
    // 公共属性
    // ============================================================

    public GameState CurrentState => _currentState;
    public DayPhase CurrentPhase => _currentPhase;
    public int DayCount => _dayCount;
    public float PhaseProgress => CurrentPhaseDuration > 0f ? _phaseTimer / CurrentPhaseDuration : 0f;

    // ============================================================
    // 生命周期
    // ============================================================

    public override void _Ready()
    {
        if (_instance != null)
        {
            GD.PushWarning("GameManager 已存在，销毁重复实例");
            QueueFree();
            return;
        }

        _instance = this;
        SubscribeEvents();
        StartGame();
    }

    public override void _ExitTree()
    {
        UnsubscribeEvents();
        _instance = null;
    }

    public override void _Process(double delta)
    {
        // GameOver 与 Loading 不驱动阶段计时
        if (_currentState is GameState.GameOver or GameState.Loading)
        {
            return;
        }

        float dt = (float)delta;
        _phaseTimer += dt;
        _totalElapsed += dt;

        // 更新昼夜进度
        EventBus.Instance.EmitDayNightChanged(_currentPhase, PhaseProgress);

        // 检查阶段是否结束
        if (_phaseTimer >= CurrentPhaseDuration)
        {
            AdvancePhase();
        }
    }

    // ============================================================
    // 事件订阅
    // ============================================================

    private void SubscribeEvents()
    {
        var eb = EventBus.Instance;
        if (eb == null)
        {
            GD.PushError("GameManager: EventBus 未初始化");
            return;
        }

        // 目前无外部事件需要监听，为扩展预留
        // eb.GameStateChanged += OnGameStateChanged;
    }

    private void UnsubscribeEvents()
    {
        var eb = EventBus.Instance;
        if (eb == null) return;
        // eb.GameStateChanged -= OnGameStateChanged;
    }

    // ============================================================
    // 核心逻辑
    // ============================================================

    /// <summary>启动游戏，进入探索阶段</summary>
    private void StartGame()
    {
        _dayCount = 1;
        _currentPhase = DayPhase.Dawn;
        SwitchState(GameState.Explore);
        GD.Print($"[GameManager] 游戏启动 — Day {_dayCount} 开始");
    }

    /// <summary>推进到下一个阶段</summary>
    private void AdvancePhase()
    {
        _phaseTimer = 0.0f;

        switch (_currentState)
        {
            case GameState.Explore:
                SwitchState(GameState.Build);
                break;

            case GameState.Build:
                SwitchState(GameState.Battle);
                break;

            case GameState.Battle:
                // 战斗结束 → 进入休整期 → 新的一天
                _dayCount++;
                SwitchState(GameState.Rest);
                break;

            case GameState.Rest:
                // 休整结束 → 新的一天探索
                SwitchState(GameState.Explore);
                GD.Print($"[GameManager] Day {_dayCount} 开始");
                break;

            default:
                GD.PushWarning($"[GameManager] 未处理的阶段切换: {_currentState}");
                break;
        }
    }

    /// <summary>切换游戏状态并广播</summary>
    private void SwitchState(GameState newState)
    {
        var oldState = _currentState;
        _currentState = newState;

        // 更新对应的 DayPhase（方便 UI 显示）
        UpdateDayPhaseForState(newState);

        // 广播状态变化
        EventBus.Instance.EmitGameStateChanged(newState);

        GD.Print($"[GameManager] 状态切换: {oldState} → {newState} (Phase: {_currentPhase})");
    }

    /// <summary>根据游戏状态更新昼夜阶段显示</summary>
    private void UpdateDayPhaseForState(GameState state)
    {
        _currentPhase = state switch
        {
            GameState.Explore => DayPhase.Morning,
            GameState.Build => DayPhase.Dusk,
            GameState.Battle => DayPhase.Night,
            GameState.Rest => DayPhase.Dawn,
            _ => _currentPhase,
        };
    }

    // ============================================================
    // 公共 API
    // ============================================================

    /// <summary>获取当前阶段剩余时间（秒）</summary>
    public float GetRemainingTime()
    {
        return CurrentPhaseDuration - _phaseTimer;
    }

    /// <summary>获取当前阶段进度百分比（0~1）</summary>
    public float GetPhaseProgress()
    {
        return PhaseProgress;
    }

    /// <summary>博士死亡 → 游戏结束</summary>
    public void GameOver()
    {
        SwitchState(GameState.GameOver);
        GD.Print("[GameManager] 游戏结束");
        EventBus.Instance.EmitDoctorDied();

        // TODO: 触发硬核删除逻辑（由 SaveManager 处理）
    }

    // ============================================================
    // 扩展：外部触发（供其他系统调用）
    // ============================================================

    /// <summary>强制进入战斗阶段（用于夜间提前触发特殊事件）</summary>
    public void ForceBattle()
    {
        if (_currentState == GameState.Build)
        {
            // 提前结束建设，进入战斗
            _phaseTimer = BuildDuration;
        }
    }

    /// <summary>跳过当前阶段（开发用）</summary>
    public void SkipCurrentPhase()
    {
        _phaseTimer = CurrentPhaseDuration;
        GD.Print("[GameManager] 跳过阶段");
    }

    // ============================================================
    // 存档集成
    // ============================================================

    /// <summary>获取当前可存档的游戏状态</summary>
    public SaveState GetSaveState()
    {
        return new SaveState
        {
            DayCount = _dayCount,
            CurrentPhase = (int)_currentPhase,
            CurrentState = (int)_currentState,
        };
    }

    /// <summary>从存档恢复游戏状态</summary>
    public void RestoreState(SaveState state)
    {
        if (state == null) return;

        _dayCount = state.DayCount;
        _currentPhase = (DayPhase)state.CurrentPhase;
        _currentState = (GameState)state.CurrentState;
        _phaseTimer = 0;
        _totalElapsed = 0;

        GD.Print($"[GameManager] 从存档恢复 — Day {_dayCount}, Phase {_currentPhase}, State {_currentState}");
    }
}
