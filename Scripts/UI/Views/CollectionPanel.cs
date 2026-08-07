using Godot;
using OutpostProtocol.Managers;
using System.Collections.Generic;

namespace OutpostProtocol.UI.Views;

/// <summary>
/// 藏品图鉴面板
/// 显示已收集/未收集藏品、稀有度、描述与背景故事
/// </summary>
public partial class CollectionPanel : Control
{
    private Label _titleLabel;
    private GridContainer _grid;
    private GridContainer _blueprintGrid;
    private Label _blueprintTitle;
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
        panel.OffsetLeft = -360;
        panel.OffsetTop = -270;
        panel.OffsetRight = 360;
        panel.OffsetBottom = 270;
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

        _titleLabel = new Label
        {
            Text = "📖 藏品图鉴",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _titleLabel.AddThemeFontSizeOverride("font_size", 24);
        titleRow.AddChild(_titleLabel);

        _closeButton = new Button { Text = "✕ 关闭" };
        titleRow.AddChild(_closeButton);

        var hint = new Label
        {
            Text = "藏品获得后本局立即生效；图鉴永久保留（普通/稀有/超稀有）",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        hint.AddThemeFontSizeOverride("font_size", 13);
        vbox.AddChild(hint);

        vbox.AddChild(new HSeparator());

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        vbox.AddChild(scroll);

        var content = new VBoxContainer();
        content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        content.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(content);

        _grid = new GridContainer { Columns = 2 };
        _grid.AddThemeConstantOverride("h_separation", 12);
        _grid.AddThemeConstantOverride("v_separation", 12);
        _grid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        content.AddChild(_grid);

        content.AddChild(new HSeparator());

        _blueprintTitle = new Label
        {
            Text = "📜 博士的战术笔记",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _blueprintTitle.AddThemeFontSizeOverride("font_size", 18);
        content.AddChild(_blueprintTitle);

        _blueprintGrid = new GridContainer { Columns = 2 };
        _blueprintGrid.AddThemeConstantOverride("h_separation", 12);
        _blueprintGrid.AddThemeConstantOverride("v_separation", 12);
        _blueprintGrid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        content.AddChild(_blueprintGrid);

        _closeButton.Pressed += () => Visible = false;
        _built = true;
    }

    public void Refresh()
    {
        if (!_built) return;

        foreach (var child in _grid.GetChildren())
        {
            child.QueueFree();
        }
        foreach (var child in _blueprintGrid.GetChildren())
        {
            child.QueueFree();
        }

        var all = DataManager.Instance?.Collections;
        if (all == null) return;

        int owned = 0;
        var cards = new List<Control>();
        foreach (var item in all.Values)
        {
            bool has = CollectionManager.Has(item.Id);
            if (has) owned++;
            cards.Add(CreateCard(item, has));
        }
        foreach (var card in cards) _grid.AddChild(card);

        _titleLabel.Text = $"📖 藏品图鉴（{owned}/{all.Count}）";

        // 图纸（博士的战术笔记）
        var blueprints = DataManager.Instance?.Blueprints;
        if (blueprints == null) return;
        int ownedBp = 0;
        var bpCards = new List<Control>();
        foreach (var bp in blueprints.Values)
        {
            bool has = BlueprintManager.Has(bp.Id);
            if (has) ownedBp++;
            bpCards.Add(CreateBlueprintCard(bp, has));
        }
        foreach (var card in bpCards) _blueprintGrid.AddChild(card);
        _blueprintTitle.Text = $"📜 博士的战术笔记（{ownedBp}/{blueprints.Count}）";
    }

    private Control CreateCard(Data.CollectionData item, bool owned)
    {
        var panel = new Panel();
        panel.CustomMinimumSize = new Vector2(330, 118);

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.13f, 0.11f, 0.08f, 0.96f),
            BorderColor = owned ? CollectionManager.GetRarityColor(item.Rarity) : new Color(0.35f, 0.32f, 0.26f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusBottomLeft = 4,
        };
        panel.AddThemeStyleboxOverride("panel", style);

        var hbox = new HBoxContainer();
        hbox.SetAnchorsPreset(LayoutPreset.FullRect);
        hbox.OffsetLeft = 10;
        hbox.OffsetTop = 10;
        hbox.OffsetRight = -10;
        hbox.OffsetBottom = -10;
        hbox.AddThemeConstantOverride("separation", 10);
        panel.AddChild(hbox);

        var icon = new TextureRect
        {
            CustomMinimumSize = new Vector2(56, 56),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            Modulate = owned ? Colors.White : new Color(1, 1, 1, 0.25f),
        };
        var tex = GD.Load<Texture2D>(item.IconPath);
        if (tex != null) icon.Texture = tex;
        hbox.AddChild(icon);

        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        vbox.AddThemeConstantOverride("separation", 3);
        hbox.AddChild(vbox);

        var nameLabel = new Label
        {
            Text = owned ? $"{item.Name}  [{CollectionManager.GetRarityText(item.Rarity)}]" : $"？？？（{CollectionManager.GetRarityText(item.Rarity)}）",
            Modulate = owned ? CollectionManager.GetRarityColor(item.Rarity) : new Color(0.7f, 0.68f, 0.62f),
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 15);
        vbox.AddChild(nameLabel);

        var descLabel = new Label
        {
            Text = owned ? item.Description : "尚未获得",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        descLabel.AddThemeFontSizeOverride("font_size", 12);
        vbox.AddChild(descLabel);

        var loreLabel = new Label
        {
            Text = owned && !string.IsNullOrEmpty(item.LoreText) ? $"「{item.LoreText}」" : "",
            Modulate = new Color(0.72f, 0.68f, 0.58f),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        loreLabel.AddThemeFontSizeOverride("font_size", 11);
        vbox.AddChild(loreLabel);

        return panel;
    }

    private Control CreateBlueprintCard(Data.BlueprintData bp, bool owned)
    {
        var panel = new Panel();
        panel.CustomMinimumSize = new Vector2(330, 72);

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.13f, 0.11f, 0.08f, 0.96f),
            BorderColor = owned ? new Color(0.85f, 0.72f, 0.4f) : new Color(0.35f, 0.32f, 0.26f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusBottomLeft = 4,
        };
        panel.AddThemeStyleboxOverride("panel", style);

        var hbox = new HBoxContainer();
        hbox.SetAnchorsPreset(LayoutPreset.FullRect);
        hbox.OffsetLeft = 10;
        hbox.OffsetTop = 8;
        hbox.OffsetRight = -10;
        hbox.OffsetBottom = -8;
        hbox.AddThemeConstantOverride("separation", 10);
        panel.AddChild(hbox);

        var icon = new TextureRect
        {
            CustomMinimumSize = new Vector2(44, 44),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            Modulate = owned ? Colors.White : new Color(1, 1, 1, 0.25f),
        };
        var tex = GD.Load<Texture2D>(bp.IconPath);
        if (tex != null) icon.Texture = tex;
        hbox.AddChild(icon);

        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        vbox.AddThemeConstantOverride("separation", 3);
        hbox.AddChild(vbox);

        var nameLabel = new Label
        {
            Text = owned ? bp.Name : $"🔒 {bp.Name}（未获得）",
            Modulate = owned ? new Color(0.92f, 0.82f, 0.55f) : new Color(0.7f, 0.68f, 0.62f),
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 14);
        vbox.AddChild(nameLabel);

        var descLabel = new Label
        {
            Text = owned ? bp.Description : "在资源点搜索或击败精英敌人有概率获得",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        descLabel.AddThemeFontSizeOverride("font_size", 12);
        vbox.AddChild(descLabel);

        return panel;
    }
}
