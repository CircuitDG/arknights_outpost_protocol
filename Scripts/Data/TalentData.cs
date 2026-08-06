using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OutpostProtocol.Data;

/// <summary>天赋分支枚举</summary>
public enum TalentBranch
{
    Survival, // 生存
    Combat, // 战斗
    Base, // 基建
    Explore, // 探索
}

/// <summary>天赋效果类型</summary>
public enum TalentEffectType
{
    BackpackCapacity,
    DoctorHealth,
    DoctorStaminaRegen,
    OperatorAttackSpeed,
    OperatorExpGain,
    OperatorStartLevel,
    TowerBuildCost,
    TowerUpgradeCost,
    CoreRepairEfficiency,
    GatherAmount,
    LootDropRate,
    DoctorSpeed,
}

/// <summary>天赋配置数据</summary>
public class TalentData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("branch")]
    public string Branch { get; set; } = "Survival";

    [JsonPropertyName("iconPath")]
    public string IconPath { get; set; } = string.Empty;

    [JsonPropertyName("maxLevel")]
    public int MaxLevel { get; set; } = 3;

    [JsonPropertyName("costPerLevel")]
    public int CostPerLevel { get; set; } = 1;

    [JsonPropertyName("effectType")]
    public string EffectType { get; set; } = string.Empty;

    [JsonPropertyName("effectValues")]
    public List<float> EffectValues { get; set; } = new();

    [JsonPropertyName("descriptions")]
    public List<string> Descriptions { get; set; } = new();
}
