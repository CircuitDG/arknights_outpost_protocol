using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OutpostProtocol.Data;

/// <summary>
/// 技能配置数据（映射 JSON）
/// </summary>
public class SkillData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("iconPath")]
    public string IconPath { get; set; } = string.Empty;

    [JsonPropertyName("cooldown")]
    public float Cooldown { get; set; } = 5.0f;

    [JsonPropertyName("staminaCost")]
    public float StaminaCost { get; set; }

    [JsonPropertyName("effectType")]
    public string EffectType { get; set; } = "Buff"; // Buff, Damage, Heal, Summon

    [JsonPropertyName("effectParams")]
    public Dictionary<string, float> EffectParams { get; set; } = new();

    [JsonPropertyName("targetType")]
    public string TargetType { get; set; } = "Self"; // Self, Target, Area

    [JsonPropertyName("targetRange")]
    public float TargetRange { get; set; }

    [JsonPropertyName("castTime")]
    public float CastTime { get; set; }

    [JsonPropertyName("animationTrigger")]
    public string AnimationTrigger { get; set; } = string.Empty;

    [JsonPropertyName("vfxPath")]
    public string VfxPath { get; set; } = string.Empty;

    [JsonPropertyName("sfxPath")]
    public string SfxPath { get; set; } = string.Empty;
}

/// <summary>
/// 干员技能绑定（用于 OperatorData 引用）
/// </summary>
public class OperatorSkillBinding
{
    [JsonPropertyName("slot")]
    public int Slot { get; set; } // 1-4 (F1-F4)

    [JsonPropertyName("skillId")]
    public string SkillId { get; set; } = string.Empty;
}
