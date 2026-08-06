using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OutpostProtocol.Data;

/// <summary>
/// 敌人波次配置数据（映射 JSON）
/// </summary>
public class EnemyWaveData
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("waveNumber")]
    public int WaveNumber { get; set; }

    [JsonPropertyName("enemyTypes")]
    public List<EnemySpawnConfig> EnemyTypes { get; set; } = new();
}

/// <summary>
/// 单个敌人类型的生成配置
/// </summary>
public class EnemySpawnConfig
{
    [JsonPropertyName("enemyId")]
    public int EnemyId { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("spawnInterval")]
    public float SpawnInterval { get; set; } = 1.0f;

    [JsonPropertyName("spawnPoint")]
    public string SpawnPoint { get; set; } = "Edge"; // Edge, Random, etc.
}
