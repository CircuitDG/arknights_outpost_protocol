using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Data;
using OutpostProtocol.Gameplay.Character.Doctor;
using OutpostProtocol.Managers;

namespace OutpostProtocol.Gameplay.Inventory;

/// <summary>
/// 掉落物实体
/// 职责：显示在场景中，等待博士靠近按 E 拾取
/// </summary>
public partial class LootItem : Node2D
{
    // ============================================================
    // 导出变量
    // ============================================================

    [ExportGroup("掉落物配置")]
    [Export] public int ItemId = 1;
    [Export] public int Quantity = 1;
    [Export] public float PickupRange = 50.0f;
    [Export] public float FloatSpeed = 0.5f;
    [Export] public float FloatAmplitude = 5.0f;
    [Export] public float DespawnTime = 30.0f; // 30 秒后自动消失

    // ============================================================
    // 组件引用
    // ============================================================

    private Sprite2D _sprite;
    private Area2D _pickupArea;
    private Label _quantityLabel;

    // ============================================================
    // 运行时状态
    // ============================================================

    private float _lifeTime;
    private Vector2 _basePosition;
    private bool _isPickedUp;
    private ItemData _itemData;

    public bool IsPickedUp => _isPickedUp;
    public ItemData Data => _itemData;

    // ============================================================
    // 生命周期
    // ============================================================

    public override void _Ready()
    {
        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        _pickupArea = GetNodeOrNull<Area2D>("PickupArea");
        _quantityLabel = GetNodeOrNull<Label>("QuantityLabel");

        AddToGroup("loot_items");

        // DataManager 可能尚未加载完成，未加载时由 _Process 重试
        if (DataManager.Instance.IsLoaded)
        {
            _itemData = DataManager.Instance.GetItem(ItemId);
            if (_itemData == null)
            {
                GD.PushWarning($"[LootItem] 未找到物品 ID:{ItemId}");
            }
        }

        UpdateVisuals();

        if (_pickupArea != null)
        {
            _pickupArea.BodyEntered += OnBodyEntered;
            _pickupArea.BodyExited += OnBodyExited;
        }

        _basePosition = GlobalPosition;

        EventBus.Instance.EmitLogMessage($"掉落物生成: {_itemData?.Name ?? "物品"} x{Quantity}", "INFO");
        GetTree().CreateTimer(DespawnTime).Timeout += OnDespawnTimeout;

        GD.Print($"[LootItem] 生成: {_itemData?.Name ?? "物品"} x{Quantity} 位置 ({GlobalPosition.X:F0}, {GlobalPosition.Y:F0})");
    }

    public override void _ExitTree()
    {
        if (_pickupArea != null)
        {
            _pickupArea.BodyEntered -= OnBodyEntered;
            _pickupArea.BodyExited -= OnBodyExited;
        }
    }

    public override void _Process(double delta)
    {
        if (_isPickedUp) return;

        // DataManager 异步加载完成后补取物品数据
        if (_itemData == null && DataManager.Instance.IsLoaded)
        {
            _itemData = DataManager.Instance.GetItem(ItemId);
            UpdateVisuals();
        }

        float dt = (float)delta;
        _lifeTime += dt;
        float floatOffset = Mathf.Sin(_lifeTime * FloatSpeed) * FloatAmplitude;
        GlobalPosition = _basePosition + new Vector2(0, floatOffset);
        _sprite?.Rotate(dt * 0.5f);
    }

    // ============================================================
    // 视觉更新
    // ============================================================

    private void UpdateVisuals()
    {
        if (_itemData == null)
        {
            SetPlaceholderColor();
        }
        else if (_sprite != null && !string.IsNullOrEmpty(_itemData.IconPath) && ResourceLoader.Exists(_itemData.IconPath))
        {
            var texture = ResourceLoader.Load<Texture2D>(_itemData.IconPath);
            if (texture != null) _sprite.Texture = texture;
            else SetPlaceholderColor();
        }
        else
        {
            SetPlaceholderColor();
        }

        if (_quantityLabel != null)
        {
            _quantityLabel.Text = Quantity > 1 ? $"x{Quantity}" : string.Empty;
            _quantityLabel.Visible = Quantity > 1;
        }

    }

    private void SetPlaceholderColor()
    {
        if (_sprite == null) return;

        var color = _itemData?.Category switch
        {
            "Material" => Colors.SandyBrown,
            "Consumable" => Colors.Green,
            _ => Colors.White,
        };

        var image = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
        image.Fill(color);
        _sprite.Texture = ImageTexture.CreateFromImage(image);
    }

    // ============================================================
    // 拾取逻辑
    // ============================================================

    private void OnBodyEntered(Node body)
    {
        if (_isPickedUp) return;

        if (body is Doctor doctor)
        {
            Pickup(doctor);
        }
    }

    private void OnBodyExited(Node body)
    {
        // 自动拾取策略，无需处理离开
    }

    /// <summary>强制拾取（由 Doctor 按 E 触发）</summary>
    public void ForcePickup(Doctor doctor)
    {
        Pickup(doctor);
    }

    private void Pickup(Doctor doctor)
    {
        if (_isPickedUp || doctor == null || _itemData == null) return;

        var backpack = doctor.Backpack;
        if (backpack == null)
        {
            GD.PushWarning("[LootItem] 博士没有背包");
            return;
        }

        if (!backpack.AddItem(ItemId, Quantity))
        {
            GD.Print($"[LootItem] 背包已满，无法拾取 {_itemData.Name}");
            return;
        }

        _isPickedUp = true;
        GD.Print($"[LootItem] {doctor.Name} 拾取 {_itemData.Name} x{Quantity}");
        EventBus.Instance.EmitLogMessage($"拾取: {_itemData.Name} x{Quantity}", "INFO");
        EventBus.Instance.EmitLootPickedUp(ItemId, Quantity);

        PlayPickupEffect();
        GetTree().CreateTimer(0.3f).Timeout += () => QueueFree();
    }

    private void PlayPickupEffect()
    {
        var tween = CreateTween();
        tween.SetEase(Tween.EaseType.Out);
        tween.TweenProperty(this, "scale", new Vector2(1.5f, 1.5f), 0.15f);
        tween.TweenProperty(this, "scale", new Vector2(0f, 0f), 0.15f);
        tween.Parallel();
        tween.TweenProperty(this, "position", Position + new Vector2(0, -30), 0.3f);
    }

    private void OnDespawnTimeout()
    {
        if (_isPickedUp) return;

        GD.Print($"[LootItem] {_itemData?.Name ?? "Unknown"} 超时消失");
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate", Colors.Transparent, 0.5f);
        tween.TweenCallback(Callable.From(() => QueueFree()));
    }

    // ============================================================
    // 公共 API
    // ============================================================

    /// <summary>设置掉落物数据（供生成器调用）</summary>
    public void SetLoot(int itemId, int quantity)
    {
        ItemId = itemId;
        Quantity = quantity;
        _itemData = DataManager.Instance.GetItem(ItemId);
        UpdateVisuals();
        _basePosition = GlobalPosition;
    }
}
