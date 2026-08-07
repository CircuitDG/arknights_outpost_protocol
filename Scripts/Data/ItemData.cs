using System.Text.Json.Serialization;

namespace OutpostProtocol.Data;

/// <summary>
/// 物品配置数据（映射 JSON）
/// </summary>
public class ItemData
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = "Material"; // Material, Consumable, Equipment

    [JsonPropertyName("maxStack")]
    public int MaxStack { get; set; } = 99;

    [JsonPropertyName("iconPath")]
    public string IconPath { get; set; } = string.Empty;
}
