using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Data;
using System.Collections.Generic;
using System.Linq;

namespace OutpostProtocol.Managers;

/// <summary>
/// 图纸系统（博士的战术笔记）
/// 图纸永久解锁对应防御塔建造；通过探索/精英掉落获得
/// </summary>
public static class BlueprintManager
{
    public static HashSet<int> OwnedIds { get; private set; } = new();
    private static bool _loaded;

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        var profile = SaveManager.Instance?.Profile;
        if (profile?.UnlockedBlueprints != null)
        {
            OwnedIds = profile.UnlockedBlueprints.ToHashSet();
        }
    }

    public static bool Has(int blueprintId)
    {
        EnsureLoaded();
        return blueprintId <= 0 || OwnedIds.Contains(blueprintId);
    }

    /// <summary>获得图纸（永久解锁）</summary>
    public static bool Grant(int blueprintId)
    {
        EnsureLoaded();
        var profile = SaveManager.Instance?.Profile;
        var data = DataManager.Instance?.GetBlueprint(blueprintId);
        if (profile == null || data == null) return false;
        if (profile.UnlockedBlueprints.Contains(blueprintId)) return false;

        profile.UnlockedBlueprints.Add(blueprintId);
        OwnedIds.Add(blueprintId);
        SaveManager.Instance.SaveProfile();

        EventBus.Instance.EmitLogMessage($"获得图纸: {data.Name} — {data.Description}", "INFO");
        GD.Print($"[Blueprint] 获得图纸: {data.Name}");
        return true;
    }

    /// <summary>随机获得一张未拥有的图纸，返回 ID（无则 -1）</summary>
    public static int GrantRandom()
    {
        EnsureLoaded();
        var available = (DataManager.Instance?.GetAllBlueprintIds() ?? new List<int>())
            .Where(id => !OwnedIds.Contains(id))
            .ToList();
        if (available.Count == 0) return -1;

        int picked = available[(int)(GD.Randi() % (uint)available.Count)];
        Grant(picked);
        return picked;
    }

    /// <summary>搜索资源点时的小概率图纸掉落</summary>
    public static void TryGrantFromSearch(float chance = 0.08f)
    {
        if (GD.Randf() < chance)
        {
            GrantRandom();
        }
    }

    /// <summary>精英敌人掉落图纸</summary>
    public static void TryGrantFromElite(float chance = 0.25f)
    {
        if (GD.Randf() < chance)
        {
            GrantRandom();
        }
    }
}
