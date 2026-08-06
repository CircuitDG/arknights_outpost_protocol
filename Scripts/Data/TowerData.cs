using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OutpostProtocol.Data;

/// <summary>
/// 防御塔配置数据（映射 JSON）
/// </summary>
public class TowerData
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("baseDamage")]
    public int BaseDamage { get; set; }

    [JsonPropertyName("attackRange")]
    public float AttackRange { get; set; }

    [JsonPropertyName("attackSpeed")]
    public float AttackSpeed { get; set; }

    [JsonPropertyName("towerType")]
    public string TowerType { get; set; } = "Ballista"; // Ballista, Gel, Explosion

    [JsonPropertyName("maxDurability")]
    public int MaxDurability { get; set; } = 100;

    [JsonPropertyName("upgradeLevels")]
    public List<TowerUpgradeLevel> UpgradeLevels { get; set; } = new();
}

/// <summary>
/// 防御塔升级等级配置
/// </summary>
public class TowerUpgradeLevel
{
    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("woodCost")]
    public int WoodCost { get; set; }

    [JsonPropertyName("ironCost")]
    public int IronCost { get; set; }

    [JsonPropertyName("originiumCost")]
    public int OriginiumCost { get; set; }

    [JsonPropertyName("damageBonus")]
    public int DamageBonus { get; set; }

    [JsonPropertyName("rangeBonus")]
    public float RangeBonus { get; set; }

    [JsonPropertyName("speedBonus")]
    public float SpeedBonus { get; set; }

    [JsonPropertyName("specialEffect")]
    public string SpecialEffect { get; set; } = string.Empty;

    [JsonPropertyName("specialEffectDescription")]
    public string SpecialEffectDescription { get; set; } = string.Empty;
}
