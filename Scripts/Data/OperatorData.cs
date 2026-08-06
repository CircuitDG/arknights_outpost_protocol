using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OutpostProtocol.Data;

/// <summary>
/// 干员配置数据（映射 JSON）
/// </summary>
public class OperatorData
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("classType")]
    public string ClassType { get; set; } = string.Empty; // Vanguard, Guard, etc.

    [JsonPropertyName("baseHp")]
    public int BaseHp { get; set; }

    [JsonPropertyName("baseAttack")]
    public int BaseAttack { get; set; }

    [JsonPropertyName("baseDefense")]
    public int BaseDefense { get; set; }

    [JsonPropertyName("maxLevel")]
    public int MaxLevel { get; set; }

    [JsonPropertyName("lvUpExpCurve")]
    public List<int> LvUpExpCurve { get; set; } = new();

    [JsonPropertyName("hpGrowth")]
    public List<int> HpGrowth { get; set; } = new();

    [JsonPropertyName("attackGrowth")]
    public List<int> AttackGrowth { get; set; } = new();

    [JsonPropertyName("skillId")]
    public string SkillId { get; set; } = string.Empty;

    [JsonPropertyName("passive1Id")]
    public string Passive1Id { get; set; } = string.Empty;

    [JsonPropertyName("passive2Id")]
    public string Passive2Id { get; set; } = string.Empty;
}
