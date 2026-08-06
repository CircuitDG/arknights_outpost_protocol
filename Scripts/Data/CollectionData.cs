using System.Text.Json.Serialization;

namespace OutpostProtocol.Data;

/// <summary>
/// 藏品配置数据（映射 JSON）
/// </summary>
public class CollectionData
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("rarity")]
    public string Rarity { get; set; } = "Common"; // Common, Rare, SuperRare

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty; // Tactical, Survival, etc.

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("effect")]
    public string Effect { get; set; } = string.Empty;

    [JsonPropertyName("loreText")]
    public string LoreText { get; set; } = string.Empty;

    [JsonPropertyName("iconPath")]
    public string IconPath { get; set; } = string.Empty;
}
