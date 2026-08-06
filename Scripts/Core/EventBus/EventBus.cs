using Godot;

namespace OutpostProtocol.Core.EventBus;

/// <summary>
/// 全局事件总线（AutoLoad 单例）
/// 所有系统间的通信必须通过此处，禁止管理器之间直接函数调用。
/// </summary>
public partial class EventBus : Node
{
    private static EventBus _instance;

    /// <summary>全局单例实例（AutoLoad 就绪后可用）</summary>
    public static EventBus Instance => _instance;

    // ============================================================
    // 1. 游戏状态信号
    // ============================================================

    /// <summary>游戏状态机切换（探索/建设/防守/休整等）</summary>
    [Signal]
    public delegate void GameStateChangedEventHandler(GameState newState);

    /// <summary>昼夜阶段变化，progress 为该阶段内进度 [0,1]</summary>
    [Signal]
    public delegate void DayNightChangedEventHandler(DayPhase phase, float progress);

    // ============================================================
    // 2. 干员相关信号
    // ============================================================

    /// <summary>干员进入战斗不能（濒危）状态</summary>
    [Signal]
    public delegate void OperatorDownEventHandler(Node2D op);

    /// <summary>干员被急救/复活</summary>
    [Signal]
    public delegate void OperatorRevivedEventHandler(Node2D op);

    /// <summary>干员升级</summary>
    [Signal]
    public delegate void OperatorLevelUpEventHandler(Node2D op, int newLevel);

    // ============================================================
    // 3. 战斗相关信号
    // ============================================================

    /// <summary>敌方波次开始</summary>
    [Signal]
    public delegate void WaveStartedEventHandler(int waveNumber);

    /// <summary>敌方波次结束</summary>
    [Signal]
    public delegate void WaveCompletedEventHandler(int waveNumber);

    /// <summary>实体受到伤害</summary>
    [Signal]
    public delegate void EntityDamagedEventHandler(Node2D target, int damage);

    /// <summary>实体死亡</summary>
    [Signal]
    public delegate void EntityDiedEventHandler(Node2D entity, Node2D killer);

    // ============================================================
    // 4. 博士相关信号
    // ============================================================

    /// <summary>博士死亡（本局失败）</summary>
    [Signal]
    public delegate void DoctorDiedEventHandler();

    // ============================================================
    // 5. 藏品相关信号
    // ============================================================

    /// <summary>
    /// 获得藏品。参数为藏品配置 ID，接收方通过 DataManager 查询详情，
    /// 避免在信号参数中传递普通 C# 类（Godot 信号参数必须是 Variant 兼容类型）。
    /// </summary>
    [Signal]
    public delegate void CollectionAcquiredEventHandler(int collectionId);

    // ============================================================
    // 6. 日志/调试信号
    // ============================================================

    /// <summary>全局日志消息（level: INFO/WARN/ERROR）</summary>
    [Signal]
    public delegate void LogMessageEventHandler(string message, string level);

    // ============================================================
    // 7. 博士状态信号
    // ============================================================

    /// <summary>博士生命值变化</summary>
    [Signal]
    public delegate void DoctorHealthChangedEventHandler(float current, float max);

    /// <summary>博士体力值变化</summary>
    [Signal]
    public delegate void DoctorStaminaChangedEventHandler(float current, float max);

    // ============================================================
    // 8. 背包信号
    // ============================================================

    /// <summary>背包内容变化（HUD 资源栏刷新）</summary>
    [Signal]
    public delegate void InventoryChangedEventHandler();

    // ============================================================
    // 9. 技能信号
    // ============================================================

    /// <summary>技能释放（slot: F1-F4 对应的 1-4）</summary>
    [Signal]
    public delegate void SkillCastEventHandler(int slot, string skillId, Node2D caster);

    /// <summary>技能冷却进度更新（progress 0-1）</summary>
    [Signal]
    public delegate void SkillCooldownUpdatedEventHandler(int slot, float progress);

    /// <summary>当前选中干员变化</summary>
    [Signal]
    public delegate void SelectedOperatorChangedEventHandler(Node2D op);

    // ============================================================
    // 生命周期
    // ============================================================

    public override void _Ready()
    {
        if (_instance != null)
        {
            GD.PushWarning("EventBus 已存在，销毁重复实例");
            QueueFree();
            return;
        }

        _instance = this;
        GD.Print("[EventBus] 初始化完成");
    }

    public override void _ExitTree()
    {
        _instance = null;
    }

    // ============================================================
    // 便捷触发方法
    // ============================================================

    /// <summary>触发游戏状态切换</summary>
    public void EmitGameStateChanged(GameState newState)
    {
        // Godot 的 EmitSignal 只接受 Variant 兼容类型，枚举需转为底层 int
        EmitSignal(SignalName.GameStateChanged, (int)newState);
    }

    /// <summary>触发昼夜阶段变化</summary>
    public void EmitDayNightChanged(DayPhase phase, float progress)
    {
        EmitSignal(SignalName.DayNightChanged, (int)phase, progress);
    }

    /// <summary>触发干员濒危</summary>
    public void EmitOperatorDown(Node2D op)
    {
        EmitSignal(SignalName.OperatorDown, op);
    }

    /// <summary>触发干员复活</summary>
    public void EmitOperatorRevived(Node2D op)
    {
        EmitSignal(SignalName.OperatorRevived, op);
    }

    /// <summary>触发干员升级</summary>
    public void EmitOperatorLevelUp(Node2D op, int newLevel)
    {
        EmitSignal(SignalName.OperatorLevelUp, op, newLevel);
    }

    /// <summary>触发波次开始</summary>
    public void EmitWaveStarted(int waveNumber)
    {
        EmitSignal(SignalName.WaveStarted, waveNumber);
    }

    /// <summary>触发波次结束</summary>
    public void EmitWaveCompleted(int waveNumber)
    {
        EmitSignal(SignalName.WaveCompleted, waveNumber);
    }

    /// <summary>触发实体受伤</summary>
    public void EmitEntityDamaged(Node2D target, int damage)
    {
        EmitSignal(SignalName.EntityDamaged, target, damage);
    }

    /// <summary>触发实体死亡</summary>
    public void EmitEntityDied(Node2D entity, Node2D killer)
    {
        EmitSignal(SignalName.EntityDied, entity, killer);
    }

    /// <summary>触发博士死亡</summary>
    public void EmitDoctorDied()
    {
        EmitSignal(SignalName.DoctorDied);
    }

    /// <summary>触发获得藏品（按配置 ID）</summary>
    public void EmitCollectionAcquired(int collectionId)
    {
        EmitSignal(SignalName.CollectionAcquired, collectionId);
    }

    /// <summary>触发日志消息</summary>
    public void EmitLogMessage(string message, string level = "INFO")
    {
        EmitSignal(SignalName.LogMessage, message, level);
    }

    /// <summary>触发博士生命值变化</summary>
    public void EmitDoctorHealthChanged(float current, float max)
    {
        EmitSignal(SignalName.DoctorHealthChanged, current, max);
    }

    /// <summary>触发博士体力值变化</summary>
    public void EmitDoctorStaminaChanged(float current, float max)
    {
        EmitSignal(SignalName.DoctorStaminaChanged, current, max);
    }

    /// <summary>触发背包内容变化</summary>
    public void EmitInventoryChanged()
    {
        EmitSignal(SignalName.InventoryChanged);
    }

    /// <summary>触发技能释放</summary>
    public void EmitSkillCast(int slot, string skillId, Node2D caster)
    {
        EmitSignal(SignalName.SkillCast, slot, skillId, caster);
    }

    /// <summary>触发技能冷却更新</summary>
    public void EmitSkillCooldownUpdated(int slot, float progress)
    {
        EmitSignal(SignalName.SkillCooldownUpdated, slot, progress);
    }

    /// <summary>触发选中干员变化</summary>
    public void EmitSelectedOperatorChanged(Node2D op)
    {
        EmitSignal(SignalName.SelectedOperatorChanged, op);
    }
}

/// <summary>游戏主状态机阶段</summary>
public enum GameState
{
    Loading,
    Explore, // 探索期 (06:00-17:00)
    Build, // 建设期 (17:00-21:00)
    Battle, // 防守期 (21:00-05:00)
    Rest, // 休整期 (05:00-06:00)
    GameOver, // 博士死亡 / 核心被毁
}

/// <summary>昼夜循环阶段</summary>
public enum DayPhase
{
    Dawn,
    Morning,
    Afternoon,
    Dusk,
    Night,
}
