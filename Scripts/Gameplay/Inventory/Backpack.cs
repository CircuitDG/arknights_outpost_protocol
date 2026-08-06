using Godot;
using OutpostProtocol.Core.EventBus;
using System.Collections.Generic;

namespace OutpostProtocol.Gameplay.Inventory;

/// <summary>
/// 玩家背包组件（挂载在 Doctor 下）
/// 管理固定物品字典：添加/移除/查询/资源消费
/// </summary>
public partial class Backpack : Node
{
    public const int WOOD_ITEM_ID = 1;
    public const int IRON_ITEM_ID = 2;
    public const int ORIGINIUM_ITEM_ID = 5;

    private readonly Dictionary<int, int> _items = new();

    /// <summary>当前物品字典（只读）</summary>
    public IReadOnlyDictionary<int, int> Items => _items;

    /// <summary>查询物品数量</summary>
    public int GetCount(int itemId)
    {
        return _items.GetValueOrDefault(itemId);
    }

    /// <summary>添加物品（堆叠上限由 ItemData 决定，当前简化不设上限）</summary>
    public void AddItem(int itemId, int amount)
    {
        if (amount <= 0) return;

        _items[itemId] = GetCount(itemId) + amount;
        EventBus.Instance?.EmitInventoryChanged();
    }

    /// <summary>移除物品，成功返回 true</summary>
    public bool RemoveItem(int itemId, int amount)
    {
        if (amount <= 0) return true;
        if (GetCount(itemId) < amount) return false;

        int remaining = GetCount(itemId) - amount;
        if (remaining <= 0) _items.Remove(itemId);
        else _items[itemId] = remaining;

        EventBus.Instance?.EmitInventoryChanged();
        return true;
    }

    /// <summary>一次性消费三类建造资源（木材/铁皮/源石）</summary>
    public bool TrySpend(int wood, int iron, int originium)
    {
        if (GetCount(WOOD_ITEM_ID) < wood) return false;
        if (GetCount(IRON_ITEM_ID) < iron) return false;
        if (GetCount(ORIGINIUM_ITEM_ID) < originium) return false;

        RemoveItem(WOOD_ITEM_ID, wood);
        RemoveItem(IRON_ITEM_ID, iron);
        RemoveItem(ORIGINIUM_ITEM_ID, originium);
        return true;
    }

    /// <summary>清空背包（测试/新对局用）</summary>
    public void Clear()
    {
        _items.Clear();
        EventBus.Instance?.EmitInventoryChanged();
    }
}
