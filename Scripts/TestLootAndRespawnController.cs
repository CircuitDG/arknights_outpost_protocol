using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Gameplay.Character.Doctor;
using OutpostProtocol.Gameplay.Inventory;

/// <summary>
/// 掉落物拾取 + 采集点重生自动化测试：
/// E 键交互拾取 → 背包增加 → 掉落物消失
/// 采集采空 → 隐藏 → 重生计时 → 自动重生
/// </summary>
public partial class TestLootAndRespawnController : Node
{
    private Doctor _doctor;
    private LootItem _loot;
    private GatherableResource _resource;
    private int _frameCount;
    private int _lootPickupEvents;

    public override void _Ready()
    {
        _doctor = GetNode<Doctor>("../Doctor");
        _loot = GetNode<LootItem>("../Loot_1");
        _resource = GetNode<GatherableResource>("../Gatherable_1");

        EventBus.Instance.LootPickedUp += OnLootPickedUp;
        GD.Print("========== 掉落物 + 采集点重生测试 ==========");
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.LootPickedUp -= OnLootPickedUp;
        }
    }

    public override void _Process(double delta)
    {
        _frameCount++;

        if (_frameCount == 5)
        {
            // E 键交互：拾取附近掉落物
            _doctor.TryInteract();
            GD.Print($"[TestLoot] 拾取后: IsPickedUp={_loot?.IsPickedUp}, 木材={_doctor.Backpack?.GetCount(Backpack.WOOD_ITEM_ID)}");
        }
        else if (_frameCount == 8)
        {
            // 采集 3 次采空（MaxAmount=3）
            int gathered = 0;
            while (_resource.Gather()) gathered++;
            GD.Print($"[TestLoot] 采集 {gathered} 次后: IsCollected={_resource.IsCollected}, IsRespawning={_resource.IsRespawning}, 木材={_doctor.Backpack?.GetCount(Backpack.WOOD_ITEM_ID)}");
        }
        else if (_frameCount == 160)
        {
            GD.Print($"[TestLoot] 重生后: IsCollected={_resource.IsCollected}, Remaining={_resource.Remaining}, IsRespawning={_resource.IsRespawning}");
        }
        else if (_frameCount >= 170)
        {
            GD.Print($"[TestLoot] 测试完成 — LootPickedUp 事件触发 {_lootPickupEvents} 次");
            GetTree().Quit();
        }
    }

    private void OnLootPickedUp(int itemId, int quantity)
    {
        _lootPickupEvents++;
        GD.Print($"[TestLoot] 收到 LootPickedUp: 物品{itemId} x{quantity}");
    }
}
