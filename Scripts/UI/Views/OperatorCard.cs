using Godot;
using OutpostProtocol.Gameplay.Character.Operator;
using System;

namespace OutpostProtocol.UI.Views;

/// <summary>
/// 干员卡牌（HUD 底部）
/// 展示头像、等级、血量、技能图标/冷却；点击选中、悬停显示详情
/// </summary>
public partial class OperatorCard : Control
{
    public Operator Operator { get; private set; }

    public event Action<OperatorCard> Selected;
    public event Action<OperatorCard> HoverStarted;
    public event Action<OperatorCard> HoverEnded;

    private Panel _cardPanel;
    private TextureRect _avatar;
    private Label _nameLabel;
    private ProgressBar _hpBar;
    private Label _hpLabel;
    private Label _statusLabel;
    private TextureRect _skillIcon;
    private TextureProgressBar _skillCooldown;
    private Label _skillReadyLabel;
    private bool _selected;

    private static readonly StyleBoxFlat NormalStyle = CreateStyle(new Color(0.16f, 0.13f, 0.09f, 0.96f), new Color(0.45f, 0.35f, 0.22f, 1f));
    private static readonly StyleBoxFlat SelectedStyle = CreateStyle(new Color(0.24f, 0.19f, 0.11f, 0.98f), new Color(0.95f, 0.78f, 0.35f, 1f));
    private static readonly StyleBoxFlat DownStyle = CreateStyle(new Color(0.22f, 0.12f, 0.1f, 0.96f), new Color(0.75f, 0.3f, 0.25f, 1f));

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(200, 64);
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
        GuiInput += OnGuiInput;
    }

    public void Setup(Operator op)
    {
        Operator = op;

        _cardPanel = new Panel();
        _cardPanel.SetAnchorsPreset(LayoutPreset.FullRect);
        _cardPanel.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_cardPanel);

        var mainRow = new HBoxContainer();
        mainRow.SetAnchorsPreset(LayoutPreset.FullRect);
        mainRow.OffsetLeft = 5;
        mainRow.OffsetTop = 4;
        mainRow.OffsetRight = -5;
        mainRow.OffsetBottom = -4;
        mainRow.AddThemeConstantOverride("separation", 6);
        mainRow.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(mainRow);

        _avatar = new TextureRect
        {
            CustomMinimumSize = new Vector2(44, 44),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        var avatarTex = GD.Load<Texture2D>(OutpostProtocol.Gameplay.Character.Operator.Operator.GetAvatarPath(op.OperatorDataId));
        if (avatarTex != null) _avatar.Texture = avatarTex;
        mainRow.AddChild(_avatar);

        var infoCol = new VBoxContainer();
        infoCol.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        infoCol.AddThemeConstantOverride("separation", 2);
        infoCol.MouseFilter = MouseFilterEnum.Ignore;
        mainRow.AddChild(infoCol);

        _nameLabel = new Label
        {
            Text = op.EntityName,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _nameLabel.AddThemeFontSizeOverride("font_size", 13);
        _nameLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.9f, 0.76f));
        infoCol.AddChild(_nameLabel);

        _hpBar = new ProgressBar
        {
            CustomMinimumSize = new Vector2(92, 8),
            MaxValue = 100,
            ShowPercentage = false,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        var hpStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.42f, 0.58f, 0.32f, 0.95f),
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomRight = 2,
            CornerRadiusBottomLeft = 2,
        };
        _hpBar.AddThemeStyleboxOverride("fill", hpStyle);
        infoCol.AddChild(_hpBar);

        _hpLabel = new Label
        {
            Text = "HP --/--",
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _hpLabel.AddThemeFontSizeOverride("font_size", 10);
        _hpLabel.AddThemeColorOverride("font_color", new Color(0.78f, 0.86f, 0.68f));
        infoCol.AddChild(_hpLabel);

        _statusLabel = new Label
        {
            Text = "",
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _statusLabel.AddThemeFontSizeOverride("font_size", 10);
        infoCol.AddChild(_statusLabel);

        // 技能角标：右上角 28x28
        var skillCorner = new Control
        {
            CustomMinimumSize = new Vector2(28, 28),
            Position = new Vector2(-32, 2),
            Size = new Vector2(28, 28),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(skillCorner);

        _skillIcon = new TextureRect
        {
            Position = new Vector2(0, 0),
            Size = new Vector2(28, 28),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        skillCorner.AddChild(_skillIcon);

        _skillCooldown = new TextureProgressBar
        {
            Position = new Vector2(0, 0),
            Size = new Vector2(28, 28),
            MaxValue = 100,
            Value = 0,
            FillMode = (int)TextureProgressBar.FillModeEnum.Clockwise,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        var cdTex = GD.Load<Texture2D>("res://Assets/UI/cooldown_fill.png");
        if (cdTex != null) _skillCooldown.TextureProgress = cdTex;
        skillCorner.AddChild(_skillCooldown);

        _skillReadyLabel = new Label
        {
            Position = new Vector2(0, 14),
            Size = new Vector2(28, 12),
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _skillReadyLabel.AddThemeFontSizeOverride("font_size", 10);
        skillCorner.AddChild(_skillReadyLabel);

        Refresh();
    }

    public void Refresh()
    {
        if (Operator == null || !IsInsideTree()) return;

        var op = Operator;
        int maxHp = op.Health?.MaxHealth ?? 0;
        int curHp = op.Health?.CurrentHealth ?? 0;

        _nameLabel.Text = $"{op.EntityName} Lv.{op.CurrentLevel}";

        _hpBar.MaxValue = Math.Max(1, maxHp);
        _hpBar.Value = curHp;
        _hpLabel.Text = $"HP {curHp}/{maxHp}";

        var skillComp = op.Skill;
        var skill = skillComp?.GetSkill(1);
        if (skill != null)
        {
            var iconTex = GD.Load<Texture2D>(skill.IconPath);
            if (iconTex != null) _skillIcon.Texture = iconTex;
            float progress = skillComp.GetCooldownProgress(1);
            _skillCooldown.Value = progress * 100;
            _skillCooldown.Visible = progress > 0.001f;
            bool ready = skillComp.IsSkillReady(1);
            _skillReadyLabel.Text = ready ? "✓" : $"{Mathf.CeilToInt(skill.Cooldown * progress)}";
            _skillReadyLabel.AddThemeColorOverride("font_color", ready
                ? new Color(0.55f, 0.9f, 0.5f)
                : new Color(0.95f, 0.7f, 0.35f));
        }
        else
        {
            _skillIcon.Texture = null;
            _skillCooldown.Visible = false;
            _skillReadyLabel.Text = "-";
        }

        if (op.IsDead || op.State == OperatorState.Down)
        {
            _statusLabel.Text = "💀 战斗不能";
            _statusLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.4f, 0.35f));
        }
        else if (op.State == OperatorState.Attacking || op.State == OperatorState.Chasing)
        {
            _statusLabel.Text = "⚔ 作战中";
            _statusLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.78f, 0.4f));
        }
        else
        {
            _statusLabel.Text = "◆ 待命";
            _statusLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.85f, 0.62f));
        }

        _cardPanel.AddThemeStyleboxOverride("panel", GetCardStyle());
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        if (_cardPanel != null)
        {
            _cardPanel.AddThemeStyleboxOverride("panel", GetCardStyle());
        }
    }

    private StyleBoxFlat GetCardStyle()
    {
        if (Operator != null && (Operator.IsDead || Operator.State == OperatorState.Down)) return DownStyle;
        return _selected ? SelectedStyle : NormalStyle;
    }

    private static StyleBoxFlat CreateStyle(Color bg, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = border,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusBottomLeft = 4,
        };
    }

    private void OnMouseEntered() => HoverStarted?.Invoke(this);

    private void OnMouseExited() => HoverEnded?.Invoke(this);

    private void OnGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            Selected?.Invoke(this);
        }
    }

    public override void _ExitTree()
    {
        MouseEntered -= OnMouseEntered;
        MouseExited -= OnMouseExited;
        GuiInput -= OnGuiInput;
    }
}
