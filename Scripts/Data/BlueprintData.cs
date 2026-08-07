using System.Text.Json.Serialization;

namespace OutpostProtocol.Data;

/// <summary>图纸配置数据（博士的战术笔记）</summary>
public class BlueprintData
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("iconPath")]
    public string IconPath { get; set; } = string.Empty;
}
