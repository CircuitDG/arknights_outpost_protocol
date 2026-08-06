using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Data;
using OutpostProtocol.Managers;
using OutpostProtocol.UI.Controllers;

namespace OutpostProtocol.Gameplay.Inventory;

/// <summary>
/// 可采集资源节点
/// 玩家靠近后按 E（interact）采集，物品直接进入博士背包
/// </summary>
public partial class GatherableResource : Node2D
{
    // ============================================================
    // 导出变量
    // ============================================================

    [ExportGroup("资源配置")]
    [Export] public int ItemId = 1;
    [Export] public int AmountPerGather = 1;
    [Export] public int MaxAmount = 5;

    [ExportGroup("重生配置")]
    [Export] public bool EnableRespawn = true;
    [Export] public float RespawnTime = 60.0f; // 重生时间（秒）
    [Export] public float RespawnOffset = 30.0f; // 重生位置随机偏移

    /// <summary>地图网格坐标（-1,-1 表示非地图生成节点），用于存档恢复</summary>
    [Export] public Vector2I MapCell = new(-1, -1);

    // ============================================================
    // 运行时状态
    // ============================================================

    private int _remaining;
    private Node2D _nearbyPlayer;
    private Area2D _detectionArea;
    private Sprite2D _sprite;
    private Vector2 _originalPosition;
    private float _respawnTimer;
    private bool _isCollected;
    private bool _isRespawning;
    private Tween _respawnTween;
    private ItemData _itemData;

    public int Remaining => _remaining;
    public bool IsCollected => _isCollected;
    public bool IsRespawning => _isRespawning;
    public float RespawnProgress => _isRespawning && RespawnTime > 0 ? _respawnTimer / RespawnTime : 0f;

    /// <summary>从存档恢复为"已搜索"状态</summary>
    public void RestoreCollected()
    {
        _isCollected = true;
        _isRespawning = false;
        _remaining = 0;
        Hide();
    }

    // ============================================================
    // 生命周期
    // ============================================================

    public override void _Ready()
    {
        _remaining = MaxAmount;
        _originalPosition = GlobalPosition;
        _detectionArea = GetNodeOrNull<Area2D>("DetectionArea");
        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        if (DataManager.Instance.IsLoaded)
        {
            _itemData = DataManager.Instance.GetItem(ItemId);
        }

        AddToGroup("gatherable_resources");

        if (_detectionArea != null)
        {
            _detectionArea.BodyEntered += OnBodyEntered;
            _detectionArea.BodyExited += OnBodyExited;
        }

        UpdateVisuals();
    }

    public override void _ExitTree()
    {
        if (_detectionArea != null)
        {
            _detectionArea.BodyEntered -= OnBodyEntered;
            _detectionArea.BodyExited -= OnBodyExited;
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // DataManager 异步加载完成后补取物品图标
        if (_itemData == null && DataManager.Instance.IsLoaded)
        {
            _itemData = DataManager.Instance.GetItem(ItemId);
            UpdateVisuals();
        }

        // 重生计时
        if (_isRespawning)
        {
            _respawnTimer += dt;
            if (_respawnTimer >= RespawnTime)
            {
                Respawn();
            }
        }

        if (_isCollected || _nearbyPlayer == null || _remaining <= 0) return;

        if (Input.IsActionJustPressed("interact"))
        {
            Gather();
        }
    }

    // ============================================================
    // 采集
    // ============================================================

    /// <summary>采集一次（供交互输入与自动化测试调用）</summary>
    public bool Gather()
    {
        if (_isCollected || _isRespawning || _remaining <= 0) return false;

        // 优先用检测到的玩家，其次回退到场景中的博士（便于自动化测试）
        var player = _nearbyPlayer ?? GetTree().GetFirstNodeInGroup("doctor") as Node2D;
        var backpack = player?.GetNodeOrNull<Backpack>("Backpack");
        if (backpack == null)
        {
            GD.Print("[GatherableResource] 附近没有带背包的玩家");
            return false;
        }

        int gatherAmount = AmountPerGather + TalentTreeController.GatherAmountBonus;

        if (!backpack.AddItem(ItemId, gatherAmount))
        {
            GD.Print("[GatherableResource] 背包已满");
            return false;
        }

        _remaining = Mathf.Max(0, _remaining - gatherAmount);

        string itemName = DataManager.Instance.GetItem(ItemId)?.Name ?? $"物品{ItemId}";
        GD.Print($"[GatherableResource] 采集到 {gatherAmount} 个 {itemName}（剩余 {_remaining}）");
        EventBus.Instance.EmitLogMessage($"采集到 {gatherAmount} 个 {itemName}", "INFO");
        EventBus.Instance.EmitResourceGathered(ItemId, gatherAmount);

        UpdateVisuals();

        // 采空后进入采集/重生状态
        if (_remaining <= 0)
        {
            _isCollected = true;
            Hide();
            if (EnableRespawn)
            {
                StartRespawn();
            }
        }

        return true;
    }

    // ============================================================
    // 重生
    // ============================================================

    private void StartRespawn()
    {
        _isRespawning = true;
        _respawnTimer = 0f;
        GD.Print($"[GatherableResource] 开始重生计时: {RespawnTime}s");
    }

    private void Respawn()
    {
        _isRespawning = false;
        _respawnTimer = 0f;
        _isCollected = false;
        _remaining = MaxAmount;

        // 原地 + 随机偏移
        GlobalPosition = _originalPosition + new Vector2(
            (GD.Randf() - 0.5f) * RespawnOffset * 2,
            (GD.Randf() - 0.5f) * RespawnOffset * 2
        );

        Show();
        UpdateVisuals();
        PlayRespawnAnimation();

        string itemName = DataManager.Instance.GetItem(ItemId)?.Name ?? $"物品{ItemId}";
        GD.Print($"[GatherableResource] {itemName} 重生 at ({GlobalPosition.X:F0}, {GlobalPosition.Y:F0})");
        EventBus.Instance.EmitLogMessage($"资源重生: {itemName}", "INFO");
        EventBus.Instance.EmitResourceRespawned(GlobalPosition, ItemId);
    }

    /// <summary>强制重生（调试/测试用）</summary>
    public void ForceRespawn()
    {
        if (!_isCollected) return;
        _isRespawning = false;
        Respawn();
    }

    private void PlayRespawnAnimation()
    {
        if (_sprite == null) return;

        _sprite.Modulate = Colors.Transparent;
        _respawnTween?.Kill();
        _respawnTween = CreateTween();
        _respawnTween.SetEase(Tween.EaseType.Out);
        _respawnTween.TweenProperty(_sprite, "modulate", Colors.White, 0.5f);

        Scale = new Vector2(0.5f, 0.5f);
        _respawnTween.Parallel();
        _respawnTween.TweenProperty(this, "scale", Vector2.One, 0.5f);
    }

    // ============================================================
    // 事件回调
    // ============================================================

    private void OnBodyEntered(Node body)
    {
        if (body.IsInGroup("doctor"))
        {
            _nearbyPlayer = body as Node2D;
        }
    }

    private void OnBodyExited(Node body)
    {
        if (body == _nearbyPlayer)
        {
            _nearbyPlayer = null;
        }
    }

    private void UpdateVisuals()
    {
        if (_sprite == null) return;

        // 已采空/已恢复为已搜索 → 保持隐藏
        if (_remaining <= 0)
        {
            Visible = false;
            return;
        }

        // 优先使用配置的物品图标
        if (_itemData != null && !string.IsNullOrEmpty(_itemData.IconPath) && ResourceLoader.Exists(_itemData.IconPath))
        {
            var texture = ResourceLoader.Load<Texture2D>(_itemData.IconPath);
            if (texture != null)
            {
                _sprite.Texture = texture;
                _sprite.Scale = new Vector2(0.5f, 0.5f);
                return;
            }
        }

        Visible = true;
        float ratio = (float)_remaining / Mathf.Max(1, MaxAmount);
        _sprite.Modulate = new Color(0.4f + 0.6f * ratio, 0.8f, 0.4f, 0.7f + 0.3f * ratio);
    }
}
