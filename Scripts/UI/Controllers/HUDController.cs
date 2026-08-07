using Godot;
using OutpostProtocol.Core;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Gameplay.Building;
using OutpostProtocol.Gameplay.Character.Doctor;
using OutpostProtocol.Gameplay.Character.Enemy;
using OutpostProtocol.Gameplay.Character.Operator;
using OutpostProtocol.Gameplay.Inventory;
using OutpostProtocol.Managers;
using OutpostProtocol.UI.Views;
using System.Collections.Generic;

namespace OutpostProtocol.UI.Controllers;

/// <summary>
/// 核心 HUD 控制器
/// 左上生存状态、顶部中间天数/阶段/波次、右侧干员卡牌（竖排）、底部 MC 物品栏、背包面板、悬停详情
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
    private Label _phaseTimeLabel;
    private ProgressBar _phaseBar;
    private Label _waveLabel;
    private Label _phaseNotice;
    private Control _phaseNoticePanel;

    private Label _coreHealthLabel;
    private ProgressBar _coreHealthBar;
    private Label _coreStatusLabel;
    private OutpostCore _outpostCore;
    private Button _settingsToggleButton;
    private Control _settingsPanel;
    private Control _helpPanel;
    private Button _helpCloseButton;

    // 建设期防御塔选择
    private Control _buildPanel;
    private readonly Button[] _towerButtons = new Button[3];
    private readonly System.Action[] _towerButtonHandlers = new System.Action[3];
    private int _selectedTowerIndex = -1;

    // 地图
    private MinimapView _minimap;
    private WorldMapView _worldMap;
    private NightFogLayer _fogLayer;

    // 干员卡牌（右侧竖排）
    private VBoxContainer _operatorCardContainer;
    private readonly List<OperatorCard> _operatorCards = new();
    private readonly Dictionary<Operator, OperatorCard> _cardByOperator = new();
    private Operator _selectedOperator;

    // 物品栏（MC 风格）
    private HBoxContainer _hotbarContainer;
    private readonly List<Panel> _hotbarPanels = new();
    private readonly List<TextureRect> _hotbarIcons = new();
    private readonly List<Label> _hotbarCounts = new();
    private readonly List<Label> _hotbarKeys = new();
    private static readonly int[] HotbarItemIds = { 1, 2, 3, 4, 5, 0, 0, 0, 0 };
    private int _selectedHotbarSlot;

    // 背包
    private Button _backpackButton;
    private InventoryPanel _inventoryPanel;

    // 悬停详情
    private PanelContainer _tooltipPanel;
    private Label _tooltipTitle;
    private Label _tooltipHp;
    private Label _tooltipAtk;
    private Label _tooltipDef;
    private Label _tooltipMorale;
    private Label _tooltipState;
    private Label _tooltipSkill;
    private Label _tooltipDesc;
    private Label _tooltipReady;
    private OperatorCard _hoveredCard;
    private OperatorCard _pendingCard;
    private float _hoverTimer;
    private const float HoverDelay = 0.45f;

    private TowerBuilder _towerBuilder;
    private EnemySpawner _spawner;
    private Backpack _backpack;
    private Doctor _doctor;

    private float _uiRefreshTimer;
    private static readonly StyleBoxFlat HotbarStyle = CreateHotbarStyle(new Color(0.12f, 0.1f, 0.08f, 0.88f), new Color(0.38f, 0.3f, 0.19f, 1f));
    private static readonly StyleBoxFlat HotbarSelectedStyle = CreateHotbarStyle(new Color(0.22f, 0.18f, 0.12f, 0.95f), new Color(0.95f, 0.78f, 0.35f, 1f));

    public override void _Ready()
    {
        _hpLabel = GetNode<Label>("../Root/TopLeftVBox/HPLabel");
        _hpBar = GetNode<ProgressBar>("../Root/TopLeftVBox/HPBar");
        _staminaLabel = GetNode<Label>("../Root/TopLeftVBox/StaminaLabel");
        _staminaBar = GetNode<ProgressBar>("../Root/TopLeftVBox/StaminaBar");
        _resourceLabel = GetNode<Label>("../Root/TopLeftVBox/ResourceLabel");
        _dayLabel = GetNode<Label>("../Root/CenterTopHBox/DayLabel");
        _phaseLabel = GetNode<Label>("../Root/CenterTopHBox/PhaseLabel");
        _phaseTimeLabel = GetNode<Label>("../Root/CenterTopHBox/PhaseTimeLabel");
        _phaseBar = GetNode<ProgressBar>("../Root/CenterTopHBox/PhaseBar");
        _waveLabel = GetNode<Label>("../Root/CenterTopHBox/WaveLabel");
        _phaseNoticePanel = GetNodeOrNull<Control>("../Root/PhaseNoticePanel");
        _phaseNotice = GetNodeOrNull<Label>("../Root/PhaseNoticePanel/PhaseNotice");
        _coreHealthLabel = GetNodeOrNull<Label>("../Root/TopLeftVBox/CoreHealthLabel");
        _coreHealthBar = GetNodeOrNull<ProgressBar>("../Root/TopLeftVBox/CoreHealthBar");
        _coreStatusLabel = GetNodeOrNull<Label>("../Root/TopLeftVBox/CoreStatusLabel");
        _operatorCardContainer = GetNode<VBoxContainer>("../Root/OperatorCardsScroll/OperatorCardContainer");
        _hotbarContainer = GetNode<HBoxContainer>("../Root/HotbarContainer");
        _backpackButton = GetNode<Button>("../Root/HotbarContainer/BackpackButton");
        _inventoryPanel = GetNodeOrNull<InventoryPanel>("../Root/InventoryPanel");
        _tooltipPanel = GetNodeOrNull<PanelContainer>("../Root/TooltipPanel");
        _tooltipTitle = GetNodeOrNull<Label>("../Root/TooltipPanel/TooltipVBox/TooltipTitle");
        _tooltipHp = GetNodeOrNull<Label>("../Root/TooltipPanel/TooltipVBox/TooltipGrid/TooltipHPLabel");
        _tooltipAtk = GetNodeOrNull<Label>("../Root/TooltipPanel/TooltipVBox/TooltipGrid/TooltipATKLabel");
        _tooltipDef = GetNodeOrNull<Label>("../Root/TooltipPanel/TooltipVBox/TooltipGrid/TooltipDEFLabel");
        _tooltipMorale = GetNodeOrNull<Label>("../Root/TooltipPanel/TooltipVBox/TooltipGrid/TooltipMoraleLabel");
        _tooltipState = GetNodeOrNull<Label>("../Root/TooltipPanel/TooltipVBox/TooltipStateLabel");
        _tooltipSkill = GetNodeOrNull<Label>("../Root/TooltipPanel/TooltipVBox/TooltipSkillLabel");
        _tooltipDesc = GetNodeOrNull<Label>("../Root/TooltipPanel/TooltipVBox/TooltipDescLabel");
        _tooltipReady = GetNodeOrNull<Label>("../Root/TooltipPanel/TooltipVBox/TooltipReadyLabel");
        _settingsToggleButton = GetNodeOrNull<Button>("../Root/SettingsToggleButton");
        _settingsPanel = GetNodeOrNull<Control>("../../UICanvas/SettingsPanel");
        _helpPanel = GetNodeOrNull<Control>("../Root/HelpPanel");
        _helpCloseButton = GetNodeOrNull<Button>("../Root/HelpPanel/HelpVBox/HelpCloseButton");
        _buildPanel = GetNodeOrNull<Control>("../Root/BuildPanel");
        for (int i = 0; i < 3; i++)
        {
            _towerButtons[i] = GetNodeOrNull<Button>($"../Root/BuildPanel/BuildVBox/TowerButton_{i + 1}");
        }
        _minimap = GetNodeOrNull<MinimapView>("../Root/MinimapView");
        _worldMap = GetNodeOrNull<WorldMapView>("../Root/WorldMapView");
        _fogLayer = GetNodeOrNull<NightFogLayer>("../FogLayer");

        FindOutpostCore();

        if (_backpackButton != null) _backpackButton.Pressed += ToggleInventory;
        if (_inventoryPanel != null) _inventoryPanel.Closed += OnInventoryClosed;
        if (_settingsToggleButton != null) _settingsToggleButton.Pressed += ToggleSettings;
        if (_helpCloseButton != null) _helpCloseButton.Pressed += ToggleHelp;
        for (int i = 0; i < _towerButtons.Length; i++)
        {
            if (_towerButtons[i] == null) continue;
            int index = i;
            _towerButtonHandlers[i] = () => OnTowerButtonPressed(index);
            _towerButtons[i].Pressed += _towerButtonHandlers[i];
        }

        BuildHotbar();

        _towerBuilder = GetNodeOrNull<TowerBuilder>("../../TowerBuilder");
        _spawner = GetNodeOrNull<EnemySpawner>("../../EnemySpawner");

        var doctor = GetTree().GetFirstNodeInGroup("doctor") as Doctor;
        _doctor = doctor;
        _backpack = doctor?.GetNodeOrNull<Backpack>("Backpack");

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

        if (_backpackButton != null) _backpackButton.Pressed -= ToggleInventory;
        if (_inventoryPanel != null) _inventoryPanel.Closed -= OnInventoryClosed;
        if (_settingsToggleButton != null) _settingsToggleButton.Pressed -= ToggleSettings;
        if (_helpCloseButton != null) _helpCloseButton.Pressed -= ToggleHelp;
        for (int i = 0; i < _towerButtons.Length; i++)
        {
            if (_towerButtons[i] == null) continue;
            if (_towerButtonHandlers[i] != null)
            {
                _towerButtons[i].Pressed -= _towerButtonHandlers[i];
            }
        }

        HideTooltip();
        InputLock.SetLocked(false);
    }

    public override void _Process(double delta)
    {
        _uiRefreshTimer += (float)delta;

        // 悬停延迟：停留一小段时间后才显示详情框
        if (_pendingCard != null && (_tooltipPanel == null || !_tooltipPanel.Visible))
        {
            _hoverTimer -= (float)delta;
            if (_hoverTimer <= 0)
            {
                ShowTooltip(_pendingCard);
            }
        }

        if (_uiRefreshTimer >= 0.15f)
        {
            _uiRefreshTimer = 0;
            InputLock.SetLocked(_settingsPanel != null && _settingsPanel.Visible);
            RefreshOperatorCards();
            RefreshHotbar();
            UpdateNightFog();
            if (_tooltipPanel != null && _tooltipPanel.Visible && _hoveredCard != null)
            {
                UpdateTooltip(_hoveredCard);
            }
        }
    }

    // ============================================================
    // 干员卡牌
    // ============================================================

    private void RefreshOperatorCards()
    {
        if (_operatorCardContainer == null) return;

        var ops = GetTree().GetNodesInGroup("operators");
        var seen = new HashSet<Operator>();

        foreach (var node in ops)
        {
            if (node is not Operator op) continue;
            seen.Add(op);

            if (!_cardByOperator.TryGetValue(op, out var card))
            {
                card = new OperatorCard();
                card.Setup(op);
                card.Selected += OnCardSelected;
                card.HoverStarted += OnCardHoverStarted;
                card.HoverEnded += OnCardHoverEnded;
                _operatorCardContainer.AddChild(card);
                _operatorCards.Add(card);
                _cardByOperator[op] = card;
            }
        }

        // 清理已消失的干员卡牌
        for (int i = _operatorCards.Count - 1; i >= 0; i--)
        {
            var card = _operatorCards[i];
            if (card.Operator == null || !seen.Contains(card.Operator))
            {
                if (_hoveredCard == card) HideTooltip();
                if (_pendingCard == card) _pendingCard = null;
                card.Selected -= OnCardSelected;
                card.HoverStarted -= OnCardHoverStarted;
                card.HoverEnded -= OnCardHoverEnded;
                card.QueueFree();
                _cardByOperator.Remove(card.Operator);
                _operatorCards.RemoveAt(i);
            }
        }

        foreach (var card in _operatorCards)
        {
            card.Refresh();
            card.SetSelected(IsOperatorSelected(card.Operator));
        }
    }

    private bool IsOperatorSelected(Operator op)
    {
        if (_doctor == null || op == null) return false;
        foreach (var selected in _doctor.SelectedOperators)
        {
            if (selected == op) return true;
        }
        return false;
    }

    private void OnCardSelected(OperatorCard card)
    {
        if (card?.Operator == null || _doctor == null) return;
        _doctor.SelectOperator(card.Operator);
    }

    private void OnCardHoverStarted(OperatorCard card)
    {
        _pendingCard = card;
        _hoverTimer = HoverDelay;
    }

    private void OnCardHoverEnded(OperatorCard card)
    {
        if (_pendingCard == card || _hoveredCard == card)
        {
            HideTooltip();
        }
    }

    private void ShowTooltip(OperatorCard card)
    {
        if (_tooltipPanel == null || card?.Operator == null) return;

        _hoveredCard = card;
        _pendingCard = null;
        UpdateTooltip(card);

        // 显示在卡牌左侧，避免与右侧列表、底部物品栏重叠
        Vector2 cardPos = card.GlobalPosition;
        var vpSize = GetViewport().GetVisibleRect().Size;
        _tooltipPanel.ResetSize();
        float x = Mathf.Clamp(cardPos.X - _tooltipPanel.Size.X - 14, 8, Mathf.Max(8, vpSize.X - _tooltipPanel.Size.X - 8));
        float y = Mathf.Clamp(cardPos.Y, 8, Mathf.Max(8, vpSize.Y - _tooltipPanel.Size.Y - 8));
        _tooltipPanel.Position = new Vector2(x, y);

        _tooltipPanel.Visible = true;
        _tooltipPanel.Modulate = new Color(1, 1, 1, 0);
        var tween = CreateTween();
        tween.TweenProperty(_tooltipPanel, "modulate:a", 1.0f, 0.12f);
    }

    private void UpdateTooltip(OperatorCard card)
    {
        if (_tooltipPanel == null || card?.Operator == null) return;

        var op = card.Operator;
        int maxHp = op.Health?.MaxHealth ?? 0;
        int curHp = op.Health?.CurrentHealth ?? 0;

        if (_tooltipTitle != null)
        {
            _tooltipTitle.Text = $"[{op.EntityName}] Lv.{op.CurrentLevel} · {op.Data?.ClassType ?? "未知职业"}";
        }
        if (_tooltipHp != null) _tooltipHp.Text = $"生命 {curHp}/{maxHp}";
        if (_tooltipAtk != null) _tooltipAtk.Text = $"攻击 {op.Attack?.AttackDamage ?? 0}";
        if (_tooltipDef != null) _tooltipDef.Text = $"防御 {op.Data?.BaseDefense ?? 0}";
        if (_tooltipMorale != null) _tooltipMorale.Text = $"心情 {op.Morale}/100";
        if (_tooltipState != null) _tooltipState.Text = $"状态: {GetOperatorStateText(op)}";

        var skill = op.Skill?.GetSkill(1);
        if (skill != null)
        {
            if (_tooltipSkill != null)
            {
                _tooltipSkill.Text = $"技能: {skill.Name}   冷却 {skill.Cooldown:F1}s   体力 {skill.StaminaCost:F0}";
            }
            if (_tooltipDesc != null) _tooltipDesc.Text = skill.Description;
            if (_tooltipReady != null)
            {
                if (op.Skill.IsSkillReady(1))
                {
                    _tooltipReady.Text = "状态: ✅ 就绪（F1 释放）";
                    _tooltipReady.AddThemeColorOverride("font_color", new Color(0.55f, 0.9f, 0.5f));
                }
                else
                {
                    float progress = op.Skill.GetCooldownProgress(1);
                    _tooltipReady.Text = $"状态: ⏳ 冷却中 {skill.Cooldown * progress:F1}s";
                    _tooltipReady.AddThemeColorOverride("font_color", new Color(0.95f, 0.7f, 0.35f));
                }
            }
        }
        else
        {
            if (_tooltipSkill != null) _tooltipSkill.Text = "技能: 未绑定";
            if (_tooltipDesc != null) _tooltipDesc.Text = "";
            if (_tooltipReady != null)
            {
                _tooltipReady.Text = "";
            }
        }
    }

    private void HideTooltip()
    {
        _hoveredCard = null;
        _pendingCard = null;
        _hoverTimer = 0;
        if (_tooltipPanel != null)
        {
            _tooltipPanel.Visible = false;
            _tooltipPanel.Modulate = Colors.White;
        }
    }

    private static string GetOperatorStateText(Operator op)
    {
        if (op.IsDead || op.State == OperatorState.Down) return "💀 战斗不能";
        return op.State switch
        {
            OperatorState.Attacking or OperatorState.Chasing => "⚔ 作战中",
            OperatorState.Following => "▸ 跟随中",
            OperatorState.Moving => "▸ 移动中",
            OperatorState.Resting => "◈ 休整中",
            _ => "◆ 待命",
        };
    }

    // ============================================================
    // MC 式物品栏
    // ============================================================

    private void BuildHotbar()
    {
        if (_hotbarContainer == null) return;

        for (int i = 0; i < HotbarItemIds.Length; i++)
        {
            var slot = new Control
            {
                CustomMinimumSize = new Vector2(36, 36),
            };
            _hotbarContainer.AddChild(slot);

            var panel = new Panel();
            panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            panel.AddThemeStyleboxOverride("panel", HotbarStyle);
            panel.MouseFilter = Control.MouseFilterEnum.Ignore;
            slot.AddChild(panel);

            var icon = new TextureRect
            {
                Position = new Vector2(3, 3),
                Size = new Vector2(30, 30),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            slot.AddChild(icon);

            var count = new Label
            {
                Position = new Vector2(16, 20),
                Size = new Vector2(18, 14),
                Text = "",
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            count.AddThemeFontSizeOverride("font_size", 11);
            count.AddThemeColorOverride("font_color", Colors.White);
            count.AddThemeColorOverride("font_shadow_color", Colors.Black);
            count.AddThemeConstantOverride("shadow_offset_x", 1);
            count.AddThemeConstantOverride("shadow_offset_y", 1);
            slot.AddChild(count);

            var key = new Label
            {
                Position = new Vector2(2, 0),
                Size = new Vector2(18, 12),
                Text = i < 9 ? $"{i + 1}" : "",
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            key.AddThemeFontSizeOverride("font_size", 9);
            key.AddThemeColorOverride("font_color", new Color(0.75f, 0.68f, 0.52f));
            slot.AddChild(key);

            _hotbarPanels.Add(panel);
            _hotbarIcons.Add(icon);
            _hotbarCounts.Add(count);
            _hotbarKeys.Add(key);
        }
    }

    private void RefreshHotbar()
    {
        if (_backpack == null || _hotbarPanels.Count == 0) return;

        for (int i = 0; i < _hotbarPanels.Count; i++)
        {
            int itemId = HotbarItemIds[i];
            _hotbarPanels[i].AddThemeStyleboxOverride("panel", i == _selectedHotbarSlot ? HotbarSelectedStyle : HotbarStyle);

            if (itemId <= 0)
            {
                _hotbarIcons[i].Texture = null;
                _hotbarCounts[i].Text = "";
                continue;
            }

            int count = _backpack.GetCount(itemId);
            var item = DataManager.Instance?.GetItem(itemId);
            _hotbarIcons[i].Texture = item != null ? GD.Load<Texture2D>(item.IconPath) : null;
            _hotbarIcons[i].Modulate = count > 0 ? Colors.White : new Color(1, 1, 1, 0.3f);
            _hotbarCounts[i].Text = count > 0 ? count.ToString() : "";
        }
    }

    private void SelectHotbarSlot(int index)
    {
        if (index < 0 || index >= _hotbarPanels.Count) return;
        _selectedHotbarSlot = index;
        RefreshHotbar();
    }

    private void CycleHotbar(int direction)
    {
        if (_hotbarPanels.Count == 0) return;
        SelectHotbarSlot((_selectedHotbarSlot + direction + _hotbarPanels.Count) % _hotbarPanels.Count);
    }

    // ============================================================
    // 背包
    // ============================================================

    private void ToggleInventory()
    {
        if (_inventoryPanel == null) return;
        _inventoryPanel.SetOpen(!_inventoryPanel.Visible, _backpack);
    }

    private void OnInventoryClosed()
    {
        // 面板内关闭按钮已隐藏自身
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
            _coreStatusLabel.Text = _outpostCore.IsDestroyed ? "💀 已摧毁" : "◆ 运转中";
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

    private void ToggleSettings()
    {
        if (_settingsPanel != null)
        {
            _settingsPanel.Visible = !_settingsPanel.Visible;
        }
    }

    private void ToggleHelp()
    {
        if (_helpPanel != null)
        {
            _helpPanel.Visible = !_helpPanel.Visible;
        }
    }

    private void ToggleWorldMap()
    {
        if (_worldMap == null) return;
        if (_worldMap.Visible)
        {
            _worldMap.Close();
        }
        else
        {
            _worldMap.Open();
        }
    }

    private void OnTowerButtonPressed(int index)
    {
        if (_towerBuilder == null) return;
        _selectedTowerIndex = index;
        RefreshTowerButtons();
        _towerBuilder.StartBuildMode(index);
    }

    private void RefreshTowerButtons()
    {
        for (int i = 0; i < _towerButtons.Length; i++)
        {
            if (_towerButtons[i] == null) continue;
            _towerButtons[i].Modulate = i == _selectedTowerIndex
                ? new Color(1f, 0.85f, 0.4f)
                : Colors.White;
        }
    }

    private void UpdateBuildPanel()
    {
        bool isBuild = GameManager.Instance?.CurrentState == GameState.Build;
        if (_buildPanel != null) _buildPanel.Visible = isBuild;
        if (!isBuild)
        {
            _selectedTowerIndex = -1;
            RefreshTowerButtons();
        }
    }

    private void UpdateNightFog()
    {
        if (_fogLayer == null) return;

        var gm = GameManager.Instance;
        float alpha = 0f;
        if (gm != null)
        {
            alpha = gm.CurrentState switch
            {
                GameState.Battle => 0.72f,
                GameState.Build => 0.38f,
                GameState.Rest => 0.22f,
                _ => 0f,
            };
        }

        var lights = new List<(Vector2 Position, float Radius)>();
        if (alpha > 0f)
        {
            foreach (var node in GetTree().GetNodesInGroup("doctor"))
            {
                if (node is Node2D d) lights.Add((d.GlobalPosition, 140f));
            }
            foreach (var node in GetTree().GetNodesInGroup("operators"))
            {
                if (node is Node2D op) lights.Add((op.GlobalPosition, 100f));
            }
            foreach (var node in GetTree().GetNodesInGroup("towers"))
            {
                if (node is TowerBase tower) lights.Add((tower.GlobalPosition, tower.CurrentRange));
            }
            foreach (var node in GetTree().GetNodesInGroup("outpost_core"))
            {
                if (node is Node2D core) lights.Add((core.GlobalPosition, 160f));
            }
        }

        _fogLayer.UpdateFog(alpha, lights);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvt && keyEvt.Pressed)
        {
            // 背包打开时优先处理关闭
            if (_inventoryPanel != null && _inventoryPanel.Visible)
            {
                if (keyEvt.IsActionPressed("backpack") || keyEvt.Keycode == Key.Escape)
                {
                    _inventoryPanel.SetOpen(false);
                    GetViewport().SetInputAsHandled();
                }
                return;
            }

            // 设置面板打开时 Esc 关闭
            if (_settingsPanel != null && _settingsPanel.Visible)
            {
                if (keyEvt.Keycode == Key.Escape || keyEvt.IsActionPressed("talent"))
                {
                    _settingsPanel.Visible = false;
                    GetViewport().SetInputAsHandled();
                }
                return;
            }

            // 全屏地图打开时 M / Esc 关闭
            if (_worldMap != null && _worldMap.Visible)
            {
                if (keyEvt.Keycode == Key.M || keyEvt.Keycode == Key.Escape)
                {
                    _worldMap.Close();
                    GetViewport().SetInputAsHandled();
                }
                return;
            }

            if (keyEvt.IsActionPressed("talent"))
            {
                GetNodeOrNull<TalentTreeController>("../../UICanvas/TalentTree")?.ShowTalentTree();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (keyEvt.Keycode == Key.H)
            {
                ToggleHelp();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (keyEvt.Keycode == Key.M)
            {
                ToggleWorldMap();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (keyEvt.IsActionPressed("backpack"))
            {
                ToggleInventory();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (keyEvt.Keycode == Key.O)
            {
                ToggleSettings();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (keyEvt.Keycode == Key.Escape)
            {
                // 建设模式中先退出建造；否则打开/关闭设置
                if (_towerBuilder != null && _towerBuilder.IsBuildingMode)
                {
                    _towerBuilder.ExitBuildMode();
                }
                else if (_helpPanel != null && _helpPanel.Visible)
                {
                    _helpPanel.Visible = false;
                }
                else
                {
                    ToggleSettings();
                }
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        // 非建设期：数字键选择物品栏
        if (@event is InputEventKey hotKey && hotKey.Pressed)
        {
            int index = hotKey.Keycode switch
            {
                Key.Key1 => 0,
                Key.Key2 => 1,
                Key.Key3 => 2,
                Key.Key4 => 3,
                Key.Key5 => 4,
                Key.Key6 => 5,
                Key.Key7 => 6,
                Key.Key8 => 7,
                Key.Key9 => 8,
                _ => -1,
            };
            if (index >= 0)
            {
                SelectHotbarSlot(index);
                GetViewport().SetInputAsHandled();
            }
        }

    }

    // ============================================================
    // 事件回调
    // ============================================================

    private void OnDoctorHealthChanged(float current, float max)
    {
        _hpBar.MaxValue = max;
        _hpBar.Value = current;
        _hpLabel.Text = $"生命 {current:F0}/{max:F0}";
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
        RefreshHotbar();
        if (_inventoryPanel != null && _inventoryPanel.Visible)
        {
            _inventoryPanel.Refresh(_backpack);
        }
    }

    private void OnGameStateChanged(GameState newState)
    {
        _phaseLabel.Text = GetPhaseText(newState);
        ShowPhaseNotice(newState);
        UpdateBuildPanel();

        if (newState != GameState.Build && _towerBuilder != null && _towerBuilder.IsBuildingMode)
        {
            _towerBuilder.ExitBuildMode();
        }
    }

    private void OnDayNightChanged(DayPhase phase, float progress)
    {
        _phaseBar.MaxValue = 100;
        _phaseBar.Value = Mathf.Clamp(progress * 100f, 0, 100);
        if (_phaseTimeLabel != null)
        {
            _phaseTimeLabel.Text = FormatPhaseTime(phase, progress);
        }
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
        RefreshOperatorCards();
    }

    private void OnOperatorChanged(Node2D op, int newLevel)
    {
        RefreshOperatorCards();
    }

    private void OnSelectedOperatorChanged(Node2D op)
    {
        _selectedOperator = op as Operator;
        RefreshOperatorCards();
    }

    private void OnSkillCast(int slot, string skillId, Node2D caster)
    {
        RefreshOperatorCards();
    }

    private void OnSkillCooldownUpdated(int slot, float progress)
    {
        RefreshOperatorCards();
    }

    // ============================================================
    // 刷新
    // ============================================================

    private void RefreshAll()
    {
        if (_doctor != null)
        {
            OnDoctorHealthChanged(_doctor.CurrentHealth, _doctor.MaxHealthValue);
            OnDoctorStaminaChanged(_doctor.CurrentStamina, _doctor.MaxStaminaValue);
        }

        var gm = GameManager.Instance;
        _dayLabel.Text = gm != null ? $"Day {gm.DayCount}" : "Day 1";
        _phaseLabel.Text = GetPhaseText(gm?.CurrentState ?? GameState.Explore);
        if (gm != null)
        {
            _phaseBar.MaxValue = 100;
            _phaseBar.Value = Mathf.Clamp(gm.PhaseProgress * 100f, 0, 100);
            if (_phaseTimeLabel != null)
            {
                _phaseTimeLabel.Text = FormatPhaseTime(gm.CurrentPhase, gm.PhaseProgress);
            }
        }

        RefreshResources();
        RefreshWave();
        RefreshOperatorCards();
        RefreshHotbar();
    }

    private void RefreshResources()
    {
        if (_backpack == null) return;

        int wood = _backpack.GetCount(Backpack.WOOD_ITEM_ID);
        int iron = _backpack.GetCount(Backpack.IRON_ITEM_ID);
        int originium = _backpack.GetCount(Backpack.ORIGINIUM_ITEM_ID);
        _resourceLabel.Text = $"木材 ×{wood}  铁皮 ×{iron}  源石 ×{originium}";
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

    private void ShowPhaseNotice(GameState state)
    {
        string text = state switch
        {
            GameState.Explore => "⛏ 探索期 — 外出采集资源（靠近资源点按 E）",
            GameState.Build => "🏗 建设期 — 按 1/2/3 选择防御塔建造",
            GameState.Battle => "⚔ 防守期 — 守住前哨站！",
            GameState.Rest => "🛌 休整期 — 修复与备战",
            GameState.GameOver => "💀 博士倒下，对局结束",
            _ => string.Empty,
        };

        _phaseNotice.Text = text;
        _phaseNotice.Visible = !string.IsNullOrEmpty(text);
        if (_phaseNoticePanel != null)
        {
            _phaseNoticePanel.Visible = !string.IsNullOrEmpty(text);
        }

        GetTree().CreateTimer(3.0).Timeout += () =>
        {
            if (_phaseNotice != null) _phaseNotice.Visible = false;
            if (_phaseNoticePanel != null) _phaseNoticePanel.Visible = false;
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

    /// <summary>把阶段进度格式化为时间（当前时间 / 阶段结束时间）</summary>
    private static string FormatPhaseTime(DayPhase phase, float progress)
    {
        (float start, float end) = phase switch
        {
            DayPhase.Dawn => (5f, 7f),
            DayPhase.Morning => (7f, 12f),
            DayPhase.Afternoon => (12f, 17f),
            DayPhase.Dusk => (17f, 21f),
            DayPhase.Night => (21f, 29f), // 21:00 → 次日 05:00
            _ => (0f, 24f),
        };

        float t = start + (end - start) * Mathf.Clamp(progress, 0f, 1f);
        int hour = ((int)t) % 24;
        int minute = (int)((t - (int)t) * 60f);
        int endHour = ((int)end) % 24;
        return $"{hour:00}:{minute:00} / {endHour:00}:00";
    }

    private static StyleBoxFlat CreateHotbarStyle(Color bg, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = border,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusBottomLeft = 3,
        };
    }

}
