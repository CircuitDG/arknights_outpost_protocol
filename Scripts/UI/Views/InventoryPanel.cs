using Godot;
using OutpostProtocol.Data;
using OutpostProtocol.Gameplay.Inventory;
using OutpostProtocol.Managers;
using System;

namespace OutpostProtocol.UI.Views;

/// <summary>
/// 背包面板（MC 风格）
/// 展示全部物品：图标、名称、数量、堆叠上限与背包容量
/// </summary>
public partial class InventoryPanel : Control
{
    public event Action Closed;

    private Label _capacityLabel;
    private GridContainer _grid;
    private Button _closeButton;
    private bool _built;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsPreset(LayoutPreset.FullRect);

        var dim = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.55f),
        };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        dim.MouseFilter = MouseFilterEnum.Stop;
        AddChild(dim);

        var panel = new Panel();
        panel.SetAnchorsPreset(LayoutPreset.Center);
        panel.OffsetLeft = -330;
        panel.OffsetTop = -260;
        panel.OffsetRight = 330;
        panel.OffsetBottom = 260;
        AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsPreset(LayoutPreset.FullRect);
        vbox.OffsetLeft = 20;
        vbox.OffsetTop = 18;
        vbox.OffsetRight = -20;
        vbox.OffsetBottom = -18;
        vbox.AddThemeConstantOverride("separation", 8);
        panel.AddChild(vbox);

        var titleRow = new HBoxContainer();
        vbox.AddChild(titleRow);

        var title = new Label
        {
            Text = "🎒 背包",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        title.AddThemeFontSizeOverride("font_size", 24);
        titleRow.AddChild(title);

        _closeButton = new Button { Text = "✕" };
        titleRow.AddChild(_closeButton);

        _capacityLabel = new Label
        {
            Text = "容量: 0/0",
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        _capacityLabel.AddThemeFontSizeOverride("font_size", 15);
        vbox.AddChild(_capacityLabel);

        vbox.AddChild(new HSeparator());

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        vbox.AddChild(scroll);

        _grid = new GridContainer { Columns = 4 };
        _grid.AddThemeConstantOverride("h_separation", 12);
        _grid.AddThemeConstantOverride("v_separation", 12);
        _grid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_grid);

        _closeButton.Pressed += OnClosePressed;
        _built = true;
    }

    public override void _ExitTree()
    {
        if (_closeButton != null)
        {
            _closeButton.Pressed -= OnClosePressed;
        }
    }

    public void SetOpen(bool open, Backpack backpack = null)
    {
        Visible = open;
        if (open && _built)
        {
            Refresh(backpack);
        }
    }

    public void Refresh(Backpack backpack)
    {
        if (!_built || backpack == null) return;

        foreach (var child in _grid.GetChildren())
        {
            child.QueueFree();
        }

        int total = backpack.GetTotalCount();
        _capacityLabel.Text = $"容量: {total}/{backpack.MaxCapacity}（剩余 {backpack.GetRemainingSpace()}）";

        foreach (var item in DataManager.Instance.Items.Values)
        {
            _grid.AddChild(CreateItemCell(item, backpack.GetCount(item.Id)));
        }
    }

    private Control CreateItemCell(ItemData item, int count)
    {
        var cell = new Panel();
        cell.CustomMinimumSize = new Vector2(138, 112);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsPreset(LayoutPreset.FullRect);
        vbox.OffsetLeft = 8;
        vbox.OffsetTop = 8;
        vbox.OffsetRight = -8;
        vbox.OffsetBottom = -8;
        vbox.AddThemeConstantOverride("separation", 4);
        vbox.Alignment = BoxContainer.AlignmentMode.Center;
        cell.AddChild(vbox);

        var icon = new TextureRect
        {
            CustomMinimumSize = new Vector2(56, 56),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        };
        var tex = GD.Load<Texture2D>(item.IconPath);
        if (tex != null) icon.Texture = tex;
        icon.Modulate = count > 0 ? Colors.White : new Color(1, 1, 1, 0.25f);
        vbox.AddChild(icon);

        var nameLabel = new Label
        {
            Text = item.Name,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 13);
        vbox.AddChild(nameLabel);

        var countLabel = new Label
        {
            Text = count > 0 ? $"×{count}（堆叠 {item.MaxStack}）" : "未持有",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        countLabel.AddThemeFontSizeOverride("font_size", 12);
        countLabel.AddThemeColorOverride("font_color", count > 0
            ? new Color(0.9f, 0.84f, 0.66f)
            : new Color(0.6f, 0.58f, 0.52f));
        vbox.AddChild(countLabel);

        return cell;
    }

    private void OnClosePressed()
    {
        Visible = false;
        Closed?.Invoke();
    }
}
