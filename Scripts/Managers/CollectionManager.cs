using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Data;
using System.Collections.Generic;
using System.Linq;

namespace OutpostProtocol.Managers;

/// <summary>
/// 藏品系统（集成战略式）
/// 局内获得即生效、永久记录图鉴；效果通过静态属性供各系统查询
/// </summary>
public static class CollectionManager
{
    public static HashSet<int> OwnedIds { get; private set; } = new();
    private static bool _loaded;

    private const int DaggerId = 1;          // 锈蚀的战术匕首：近卫/先锋攻击 +8%
    private const int ScopeId = 2;           // 陈旧的狙击镜：狙击攻击范围 +1 格
    private const int BiscuitId = 3;         // 压缩饼干：每日食物消耗 -20%
    private const int CrystalId = 101;       // 源石结晶碎片：术师技能伤害 +25%
    private const int RadioId = 102;         // 战术电台：博士指挥半径 +3 格
    private const int SwordManualId = 201;   // 真银斩剑谱：近卫 15% 概率真银斩 AOE

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        var profile = SaveManager.Instance?.Profile;
        if (profile?.UnlockedCollectionIds != null)
        {
            OwnedIds = profile.UnlockedCollectionIds.ToHashSet();
        }
    }

    public static bool Has(int collectionId)
    {
        EnsureLoaded();
        return OwnedIds.Contains(collectionId);
    }

    // ============================================================
    // 效果查询
    // ============================================================

    public static float GuardVanguardAttackBonus => Has(DaggerId) ? 0.08f : 0f;
    public static float SniperRangeBonusPx => Has(ScopeId) ? 16f : 0f;
    public static float FoodConsumptionReduction => Has(BiscuitId) ? 0.2f : 0f;
    public static float CasterSkillDamageBonus => Has(CrystalId) ? 0.25f : 0f;
    public static float DoctorCommandRangeBonusPx => Has(RadioId) ? 48f : 0f;
    public static float GuardSilverSlashChance => Has(SwordManualId) ? 0.15f : 0f;

    // ============================================================
    // 获取
    // ============================================================

    /// <summary>获得指定藏品（已拥有则返回 false）</summary>
    public static bool GrantCollection(int collectionId)
    {
        EnsureLoaded();
        var profile = SaveManager.Instance?.Profile;
        var data = DataManager.Instance?.GetCollection(collectionId);
        if (profile == null || data == null) return false;
        if (profile.UnlockedCollectionIds.Contains(collectionId)) return false;

        profile.UnlockedCollectionIds.Add(collectionId);
        OwnedIds.Add(collectionId);
        SaveManager.Instance.SaveProfile();

        EventBus.Instance.EmitCollectionAcquired(collectionId);
        EventBus.Instance.EmitLogMessage($"获得藏品: {data.Name}（{GetRarityText(data.Rarity)}）", "INFO");
        AudioManager.Instance?.Play("collection");
        GD.Print($"[Collection] 获得藏品: {data.Name} | {data.Description}");
        return true;
    }

    /// <summary>按稀有度权重随机获得一个未拥有的藏品，返回藏品 ID（无可用则 -1）</summary>
    public static int TryGrantRandom()
    {
        EnsureLoaded();
        var all = DataManager.Instance?.GetAllCollectionIds() ?? new List<int>();
        var available = all.Where(id => !OwnedIds.Contains(id)).ToList();
        if (available.Count == 0) return -1;

        var pool = new List<int>();
        foreach (int id in available)
        {
            var data = DataManager.Instance.GetCollection(id);
            if (data == null) continue;
            int weight = data.Rarity switch
            {
                "Common" => 60,
                "Rare" => 30,
                _ => 10,
            };
            for (int i = 0; i < weight; i++) pool.Add(id);
        }
        if (pool.Count == 0) return -1;

        int picked = pool[(int)(GD.Randi() % (uint)pool.Count)];
        GrantCollection(picked);
        return picked;
    }

    public static int OwnedCount => OwnedIds.Count;

    public static string GetRarityText(string rarity)
    {
        return rarity switch
        {
            "Common" => "普通",
            "Rare" => "稀有",
            "SuperRare" => "超稀有",
            _ => rarity,
        };
    }

    public static Color GetRarityColor(string rarity)
    {
        return rarity switch
        {
            "Common" => new Color(0.92f, 0.92f, 0.92f),
            "Rare" => new Color(0.35f, 0.65f, 1f),
            "SuperRare" => new Color(1f, 0.62f, 0.2f),
            _ => Colors.White,
        };
    }
}
