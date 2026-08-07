using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Data;
using OutpostProtocol.Managers;
using System;
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

    /// <summary>背包总容量（按物品总数量计）</summary>
    [Export] public int MaxCapacity = 200;

    private readonly Dictionary<int, int> _items = new();

    /// <summary>当前物品字典（只读）</summary>
    public IReadOnlyDictionary<int, int> Items => _items;

    /// <summary>查询物品数量</summary>
    public int GetCount(int itemId)
    {
        return _items.GetValueOrDefault(itemId);
    }

    /// <summary>添加物品（堆叠上限由 ItemData 决定，当前简化不设上限）</summary>
    public bool AddItem(int itemId, int amount)
    {
        if (amount <= 0) return false;
        if (!CanAddItem(itemId, amount)) return false;

        _items[itemId] = GetCount(itemId) + amount;
        EventBus.Instance?.EmitInventoryChanged();
        return true;
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

    // ============================================================
    // 空间检测
    // ============================================================

    /// <summary>当前物品总数量</summary>
    public int GetTotalCount()
    {
        int total = 0;
        foreach (var kvp in _items)
        {
            total += kvp.Value;
        }
        return total;
    }

    /// <summary>背包是否已满</summary>
    public bool IsFull()
    {
        return GetTotalCount() >= MaxCapacity;
    }

    /// <summary>剩余空间（物品总数量口径）</summary>
    public int GetRemainingSpace()
    {
        return Math.Max(0, MaxCapacity - GetTotalCount());
    }

    /// <summary>检查是否能添加指定数量的物品（堆叠上限 + 总容量）</summary>
    public bool CanAddItem(int itemId, int quantity)
    {
        if (quantity <= 0) return false;

        var itemData = DataManager.Instance?.GetItem(itemId);
        int current = GetCount(itemId);

        if (itemData != null && current + quantity > itemData.MaxStack) return false;
        return GetTotalCount() + quantity <= MaxCapacity;
    }
}
