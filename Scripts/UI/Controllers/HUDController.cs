using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Data;
using OutpostProtocol.Gameplay.Building;
using OutpostProtocol.Gameplay.Character.Enemy;
using OutpostProtocol.Gameplay.Character.Operator;
using OutpostProtocol.Gameplay.Inventory;
using OutpostProtocol.Managers;

namespace OutpostProtocol.UI.Controllers;

/// <summary>
/// 核心 HUD 控制器
/// 事件驱动更新：博士 HP/体力、天数/阶段/倒计时、资源、干员状态、波次信息、阶段引导
/// </summary>
public partial class HUDController : Node
{
    private Label _hpLabel;
    private ProgressBar _hpBar;
    private Label _staminaLabel;
    private ProgressBar _staminaBar;
    private Label _resourceLabel;
    private Label _dayLabel;
    private Label _phaseLabel;
    private ProgressBar _phaseBar;
    private Label _operatorLabel;
    private Label _skillLabel;
    private Label _waveLabel;
    private Label _phaseNotice;

    private Label _coreHealthLabel;
    private ProgressBar _coreHealthBar;
    private Label _coreStatusLabel;
    private OutpostCore _outpostCore;
    private Control _helpPanel;
    private Button _helpToggleButton;
    private Button _helpCloseButton;
    private Button _settingsToggleButton;
    private Control _settingsPanel;

    private readonly Control[] _skillSlots = new Control[4];
    private readonly TextureRect[] _skillIcons = new TextureRect[4];
    private readonly TextureProgressBar[] _skillCooldowns = new TextureProgressBar[4];
    private readonly Label[] _skillKeys = new Label[4];
    private readonly Label[] _skillStatuses = new Label[4];
    private Operator _selectedOperator;

    private TowerBuilder _towerBuilder;
    private EnemySpawner _spawner;
    private Backpack _backpack;

    public override void _Ready()
    {
        _hpLabel = GetNode<Label>("../Root/TopLeftVBox/HPLabel");
        _hpBar = GetNode<ProgressBar>("../Root/TopLeftVBox/HPBar");
        _staminaLabel = GetNode<Label>("../Root/TopLeftVBox/StaminaLabel");
        _staminaBar = GetNode<ProgressBar>("../Root/TopLeftVBox/StaminaBar");
        _resourceLabel = GetNode<Label>("../Root/TopLeftVBox/ResourceLabel");
        _dayLabel = GetNode<Label>("../Root/TopRightVBox/DayLabel");
        _phaseLabel = GetNode<Label>("../Root/TopRightVBox/PhaseLabel");
        _phaseBar = GetNode<ProgressBar>("../Root/TopRightVBox/PhaseBar");
        _operatorLabel = GetNode<Label>("../Root/BottomCenterHBox/OperatorLabel");
        _skillLabel = GetNode<Label>("../Root/BottomRightVBox/SkillLabel");
        _waveLabel = GetNode<Label>("../Root/BottomLeftVBox/WaveLabel");
        _phaseNotice = GetNode<Label>("../Root/PhaseNotice");
        _coreHealthLabel = GetNodeOrNull<Label>("../Root/TopLeftVBox/CoreHealthLabel");
        _coreHealthBar = GetNodeOrNull<ProgressBar>("../Root/TopLeftVBox/CoreHealthBar");
        _coreStatusLabel = GetNodeOrNull<Label>("../Root/TopLeftVBox/CoreStatusLabel");
        _helpPanel = GetNodeOrNull<Control>("../Root/HelpPanel");
        _helpToggleButton = GetNodeOrNull<Button>("../Root/HelpToggleButton");
        _helpCloseButton = GetNodeOrNull<Button>("../Root/HelpPanel/HelpVBox/HelpCloseButton");
        _settingsToggleButton = GetNodeOrNull<Button>("../Root/SettingsToggleButton");
        _settingsPanel = GetNodeOrNull<Control>("../../UICanvas/SettingsPanel");

        FindOutpostCore();

        if (_helpToggleButton != null) _helpToggleButton.Pressed += ToggleHelp;
        if (_helpCloseButton != null) _helpCloseButton.Pressed += ToggleHelp;
        if (_settingsToggleButton != null) _settingsToggleButton.Pressed += ToggleSettings;

        // 技能栏（F1-F4）
        for (int i = 1; i <= 4; i++)
        {
            var slot = GetNodeOrNull<Control>($"../Root/BottomRightVBox/SkillBar/SkillSlot_{i}");
            _skillSlots[i - 1] = slot;
            if (slot == null) continue;
            _skillIcons[i - 1] = slot.GetNodeOrNull<TextureRect>("SkillIcon");
            _skillCooldowns[i - 1] = slot.GetNodeOrNull<TextureProgressBar>("SkillCooldown");
            _skillKeys[i - 1] = slot.GetNodeOrNull<Label>("SkillKey");
            _skillStatuses[i - 1] = slot.GetNodeOrNull<Label>("SkillStatus");
        }

        _towerBuilder = GetNodeOrNull<TowerBuilder>("../../TowerBuilder");
        _spawner = GetNodeOrNull<EnemySpawner>("../../EnemySpawner");

        var doctor = GetTree().GetFirstNodeInGroup("doctor") as Node2D;
        _backpack = doctor?.GetNodeOrNull<Backpack>("Backpack");

        // 订阅事件（事件驱动，不轮询）
        var eb = EventBus.Instance;
        eb.DoctorHealthChanged += OnDoctorHealthChanged;
        eb.DoctorStaminaChanged += OnDoctorStaminaChanged;
        eb.InventoryChanged += OnInventoryChanged;
        eb.GameStateChanged += OnGameStateChanged;
        eb.DayNightChanged += OnDayNightChanged;
        eb.WaveStarted += OnWaveStarted;
        eb.WaveCompleted += OnWaveCompleted;
        eb.EntityDied += OnEntityDied;
        eb.OperatorLevelUp += OnOperatorChanged;
        eb.OperatorDown += OnOperatorChanged;
        eb.OperatorRevived += OnOperatorChanged;
        eb.SelectedOperatorChanged += OnSelectedOperatorChanged;
        eb.SkillCast += OnSkillCast;
        eb.SkillCooldownUpdated += OnSkillCooldownUpdated;

        RefreshAll();
        RefreshSkillBar();
        GD.Print("[HUD] 初始化完成");
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance == null) return;
        var eb = EventBus.Instance;
        eb.DoctorHealthChanged -= OnDoctorHealthChanged;
        eb.DoctorStaminaChanged -= OnDoctorStaminaChanged;
        eb.InventoryChanged -= OnInventoryChanged;
        eb.GameStateChanged -= OnGameStateChanged;
        eb.DayNightChanged -= OnDayNightChanged;
        eb.WaveStarted -= OnWaveStarted;
        eb.WaveCompleted -= OnWaveCompleted;
        eb.EntityDied -= OnEntityDied;
        eb.OperatorLevelUp -= OnOperatorChanged;
        eb.OperatorDown -= OnOperatorChanged;
        eb.OperatorRevived -= OnOperatorChanged;
        eb.SelectedOperatorChanged -= OnSelectedOperatorChanged;
        eb.SkillCast -= OnSkillCast;
        eb.SkillCooldownUpdated -= OnSkillCooldownUpdated;

        if (_helpToggleButton != null) _helpToggleButton.Pressed -= ToggleHelp;
        if (_helpCloseButton != null) _helpCloseButton.Pressed -= ToggleHelp;
        if (_settingsToggleButton != null) _settingsToggleButton.Pressed -= ToggleSettings;

        UpdateSkillBar(null);
    }

    // ============================================================
    // 核心血量显示
    // ============================================================

    private void FindOutpostCore()
    {
        _outpostCore = GetTree().GetFirstNodeInGroup("outpost_core") as OutpostCore;
        if (_outpostCore != null)
        {
            _outpostCore.OnDamaged += UpdateCoreHealth;
            _outpostCore.OnDestroyed += OnCoreDestroyed;
            UpdateCoreHealth(_outpostCore.CurrentHealth);
        }
    }

    private void UpdateCoreHealth(int currentHealth)
    {
        if (_outpostCore == null) return;

        if (_coreHealthBar != null)
        {
            _coreHealthBar.MaxValue = _outpostCore.MaxHealth;
            _coreHealthBar.Value = currentHealth;
            _coreHealthBar.Modulate = _outpostCore.HealthPercent > 0.5f ? Colors.Green
                : _outpostCore.HealthPercent > 0.25f ? Colors.Yellow : Colors.Red;
        }

        if (_coreHealthLabel != null)
        {
            _coreHealthLabel.Text = $"核心: {currentHealth}/{_outpostCore.MaxHealth}";
        }

        if (_coreStatusLabel != null)
        {
            _coreStatusLabel.Text = _outpostCore.IsDestroyed ? "💀 已摧毁" : "🛡️ 运转中";
            _coreStatusLabel.Modulate = _outpostCore.IsDestroyed ? Colors.Red : Colors.Green;
        }
    }

    private void OnCoreDestroyed()
    {
        if (_coreStatusLabel != null)
        {
            _coreStatusLabel.Text = "💀 已摧毁";
            _coreStatusLabel.Modulate = Colors.Red;
        }
    }

    /// <summary>切换操作指引面板</summary>
    private void ToggleHelp()
    {
        if (_helpPanel != null)
        {
            _helpPanel.Visible = !_helpPanel.Visible;
        }
    }

    /// <summary>切换游戏内设置面板</summary>
    private void ToggleSettings()
    {
        if (_settingsPanel != null)
        {
            _settingsPanel.Visible = !_settingsPanel.Visible;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // T 键打开天赋树
        if (@event is InputEventKey keyEvt && keyEvt.Pressed && keyEvt.Keycode == Key.T)
        {
            GetNodeOrNull<TalentTreeController>("../../UICanvas/TalentTree")?.ShowTalentTree();
            return;
        }

        if (@event is InputEventKey helpKey && helpKey.Pressed && helpKey.Keycode == Key.H)
        {
            ToggleHelp();
            return;
        }

        if (@event is InputEventKey settingsKey && settingsKey.Pressed && settingsKey.Keycode == Key.O)
        {
            ToggleSettings();
            return;
        }

        // 建设期快捷键：1/2/3 选择塔，ESC 取消
        if (GameManager.Instance?.CurrentState != GameState.Build || _towerBuilder == null) return;

        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed) return;

        switch (keyEvent.Keycode)
        {
            case Key.Key1:
                _towerBuilder.StartBuildMode(0);
                break;
            case Key.Key2:
                _towerBuilder.StartBuildMode(1);
                break;
            case Key.Key3:
                _towerBuilder.StartBuildMode(2);
                break;
            case Key.Escape:
                _towerBuilder.ExitBuildMode();
                break;
        }
    }

    // ============================================================
    // 事件回调
    // ============================================================

    private void OnDoctorHealthChanged(float current, float max)
    {
        _hpBar.MaxValue = max;
        _hpBar.Value = current;
        _hpLabel.Text = $"HP {current:F0}/{max:F0}";
    }

    private void OnDoctorStaminaChanged(float current, float max)
    {
        _staminaBar.MaxValue = max;
        _staminaBar.Value = current;
        _staminaLabel.Text = $"体力 {current:F0}/{max:F0}";
    }

    private void OnInventoryChanged()
    {
        RefreshResources();
    }

    private void OnGameStateChanged(GameState newState)
    {
        _phaseLabel.Text = GetPhaseText(newState);
        ShowPhaseNotice(newState);

        // 离开建设期自动退出建造模式
        if (newState != GameState.Build && _towerBuilder != null && _towerBuilder.IsBuildingMode)
        {
            _towerBuilder.ExitBuildMode();
        }
    }

    private void OnDayNightChanged(DayPhase phase, float progress)
    {
        _phaseBar.MaxValue = 100;
        _phaseBar.Value = Mathf.Clamp(progress * 100f, 0, 100);
    }

    private void OnWaveStarted(int waveNumber)
    {
        _waveLabel.Text = $"波次 {waveNumber} 开始！";
    }

    private void OnWaveCompleted(int waveNumber)
    {
        _waveLabel.Text = $"波次 {waveNumber} 完成";
        RefreshWave();
    }

    private void OnEntityDied(Node2D entity, Node2D killer)
    {
        if (entity is Enemy)
        {
            RefreshWave();
        }
    }

    private void OnOperatorChanged(Node2D op)
    {
        RefreshOperators();
    }

    private void OnOperatorChanged(Node2D op, int newLevel)
    {
        RefreshOperators();
    }

    private void OnSelectedOperatorChanged(Node2D op)
    {
        UpdateSkillBar(op as Operator);
    }

    private void OnSkillCast(int slot, string skillId, Node2D caster)
    {
        RefreshSkillBar();
    }

    private void OnSkillCooldownUpdated(int slot, float progress)
    {
        RefreshSkillBar();
    }

    // ============================================================
    // 刷新
    // ============================================================

    private void RefreshAll()
    {
        var doctor = GetTree().GetFirstNodeInGroup("doctor") as Node2D;
        if (doctor != null)
        {
            OnDoctorHealthChanged(doctor.Get("CurrentHealth").As<float>(), doctor.Get("MaxHealthValue").As<float>());
            OnDoctorStaminaChanged(doctor.Get("CurrentStamina").As<float>(), doctor.Get("MaxStaminaValue").As<float>());
        }

        var gm = GameManager.Instance;
        _dayLabel.Text = gm != null ? $"Day {gm.DayCount}" : "Day 1";
        _phaseLabel.Text = GetPhaseText(gm?.CurrentState ?? GameState.Explore);

        RefreshResources();
        RefreshOperators();
        RefreshWave();
    }

    private void RefreshResources()
    {
        if (_backpack == null) return;

        int wood = _backpack.GetCount(Backpack.WOOD_ITEM_ID);
        int iron = _backpack.GetCount(Backpack.IRON_ITEM_ID);
        int originium = _backpack.GetCount(Backpack.ORIGINIUM_ITEM_ID);
        _resourceLabel.Text = $"木材:{wood}  铁皮:{iron}  源石:{originium}";
    }

    private void RefreshOperators()
    {
        var text = new System.Text.StringBuilder("干员: ");
        bool first = true;
        foreach (var node in GetTree().GetNodesInGroup("operators"))
        {
            if (node is Operator op)
            {
                if (!first) text.Append(" | ");
                first = false;
                text.Append($"{op.EntityName} Lv.{op.CurrentLevel} HP:{op.Health?.CurrentHealth}/{op.Health?.MaxHealth}");
            }
        }
        _operatorLabel.Text = text.ToString();
    }

    private void RefreshWave()
    {
        if (_spawner == null)
        {
            _waveLabel.Text = "波次: --";
            return;
        }
        _waveLabel.Text = $"波次 {_spawner.CurrentWaveNumber} | 活跃:{_spawner.IsWaveActive} | 剩余:{_spawner.GetRemainingEnemies()}";
    }

    /// <summary>绑定/解绑选中干员的技能事件并刷新技能栏</summary>
    private void UpdateSkillBar(Operator op)
    {
        if (_selectedOperator?.Skill != null)
        {
            _selectedOperator.Skill.SkillCasted -= OnLocalSkillCasted;
            _selectedOperator.Skill.CooldownUpdated -= OnLocalCooldownUpdated;
            _selectedOperator.Skill.CastStateChanged -= OnLocalCastStateChanged;
        }

        _selectedOperator = op;

        if (_selectedOperator?.Skill != null)
        {
            _selectedOperator.Skill.SkillCasted += OnLocalSkillCasted;
            _selectedOperator.Skill.CooldownUpdated += OnLocalCooldownUpdated;
            _selectedOperator.Skill.CastStateChanged += OnLocalCastStateChanged;
        }

        RefreshSkillBar();
    }

    private void OnLocalSkillCasted(int slot, SkillData skill)
    {
        RefreshSkillBar();
    }

    private void OnLocalCooldownUpdated(int slot, float progress)
    {
        RefreshSkillBar();
    }

    private void OnLocalCastStateChanged(int slot, bool casting)
    {
        RefreshSkillBar();
    }

    private void RefreshSkillBar()
    {
        for (int i = 0; i < 4; i++)
        {
            var slot = _skillSlots[i];
            if (slot == null) continue;

            var skillComp = _selectedOperator?.Skill;
            var skill = skillComp?.GetSkill(i + 1);
            if (skill == null)
            {
                slot.Visible = false;
                continue;
            }

            slot.Visible = true;

            if (_skillKeys[i] != null)
            {
                _skillKeys[i].Text = $"F{i + 1}";
            }

            float progress = skillComp.GetCooldownProgress(i + 1);
            if (_skillCooldowns[i] != null)
            {
                _skillCooldowns[i].Value = progress * 100;
                _skillCooldowns[i].Visible = progress > 0.001f;
            }

            bool ready = skillComp.IsSkillReady(i + 1);
            if (_skillStatuses[i] != null)
            {
                _skillStatuses[i].Text = ready ? "✓" : "⏳";
                _skillStatuses[i].Modulate = ready ? Colors.Green : Colors.Orange;
            }
        }
    }

    private void ShowPhaseNotice(GameState state)
    {
        string text = state switch
        {
            GameState.Explore => "☀️ 探索期 — 外出采集资源（靠近资源点按 E）",
            GameState.Build => "🌆 建设期 — 按 1/2/3 选择防御塔建造",
            GameState.Battle => "🌙 防守期 — 守住前哨站！",
            GameState.Rest => "🌅 休整期 — 修复与备战",
            GameState.GameOver => "💀 博士倒下，对局结束",
            _ => string.Empty,
        };

        _phaseNotice.Text = text;
        _phaseNotice.Visible = !string.IsNullOrEmpty(text);

        GetTree().CreateTimer(3.0).Timeout += () =>
        {
            _phaseNotice.Visible = false;
        };
    }

    private static string GetPhaseText(GameState state)
    {
        return state switch
        {
            GameState.Explore => "探索期",
            GameState.Build => "建设期",
            GameState.Battle => "防守期",
            GameState.Rest => "休整期",
            GameState.GameOver => "游戏结束",
            _ => "加载中",
        };
    }
}
