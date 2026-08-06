using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Managers;

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

    // ============================================================
    // 运行时状态
    // ============================================================

    private int _remaining;
    private Node2D _nearbyPlayer;
    private Area2D _detectionArea;
    private Sprite2D _sprite;

    public int Remaining => _remaining;

    // ============================================================
    // 生命周期
    // ============================================================

    public override void _Ready()
    {
        _remaining = MaxAmount;
        _detectionArea = GetNodeOrNull<Area2D>("DetectionArea");
        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");

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
        if (_nearbyPlayer == null || _remaining <= 0) return;

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
        if (_remaining <= 0) return false;

        // 优先用检测到的玩家，其次回退到场景中的博士（便于自动化测试）
        var player = _nearbyPlayer ?? GetTree().GetFirstNodeInGroup("doctor") as Node2D;
        var backpack = player?.GetNodeOrNull<Backpack>("Backpack");
        if (backpack == null)
        {
            GD.Print("[GatherableResource] 附近没有带背包的玩家");
            return false;
        }

        backpack.AddItem(ItemId, AmountPerGather);
        _remaining = Mathf.Max(0, _remaining - AmountPerGather);

        string itemName = DataManager.Instance.GetItem(ItemId)?.Name ?? $"物品{ItemId}";
        GD.Print($"[GatherableResource] 采集到 {AmountPerGather} 个 {itemName}（剩余 {_remaining}）");
        EventBus.Instance.EmitLogMessage($"采集到 {AmountPerGather} 个 {itemName}", "INFO");

        UpdateVisuals();
        return true;
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

        if (_remaining <= 0)
        {
            Visible = false;
            return;
        }

        Visible = true;
        float ratio = (float)_remaining / Mathf.Max(1, MaxAmount);
        _sprite.Modulate = new Color(0.4f + 0.6f * ratio, 0.8f, 0.4f, 0.7f + 0.3f * ratio);
    }
}
