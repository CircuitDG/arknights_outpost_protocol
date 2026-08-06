using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OutpostProtocol.Data;

// ============================================================
// 全局档案（Profile）—— 永久保留
// ============================================================

/// <summary>全局档案：跨对局永久保留的成长数据</summary>
public class SaveProfile
{
    [JsonPropertyName("totalTalentPoints")]
    public int TotalTalentPoints { get; set; }

    [JsonPropertyName("talentLevels")]
    public Dictionary<string, int> TalentLevels { get; set; } = new();

    [JsonPropertyName("unlockedCollectionIds")]
    public List<int> UnlockedCollectionIds { get; set; } = new();

    [JsonPropertyName("trustData")]
    public Dictionary<int, int> TrustData { get; set; } = new(); // OperatorId → Trust

    [JsonPropertyName("collectionMilestone")]
    public int CollectionMilestone { get; set; }

    [JsonPropertyName("totalDaysSurvived")]
    public int TotalDaysSurvived { get; set; }

    [JsonPropertyName("unlockedBlueprints")]
    public List<int> UnlockedBlueprints { get; set; } = new(); // 图纸 ID
}

// ============================================================
// 对局存档（RunSave）—— 博士死亡时删除
// ============================================================

/// <summary>对局存档：本局全部可恢复状态</summary>
public class RunSave
{
    [JsonPropertyName("currentDate")]
    public string CurrentDate { get; set; } = string.Empty;

    [JsonPropertyName("currentPhase")]
    public int CurrentPhase { get; set; } // DayPhase 枚举值

    [JsonPropertyName("doctorPosX")]
    public float DoctorPosX { get; set; }

    [JsonPropertyName("doctorPosY")]
    public float DoctorPosY { get; set; }

    [JsonPropertyName("doctorHealth")]
    public float DoctorHealth { get; set; }

    [JsonPropertyName("doctorStamina")]
    public float DoctorStamina { get; set; }

    [JsonPropertyName("operators")]
    public List<OperatorRuntime> Operators { get; set; } = new();

    [JsonPropertyName("towers")]
    public List<TowerRuntime> Towers { get; set; } = new();

    [JsonPropertyName("inventoryItems")]
    public List<int> InventoryItems { get; set; } = new(); // 物品 ID

    [JsonPropertyName("ownedCollections")]
    public List<int> OwnedCollections { get; set; } = new();

    [JsonPropertyName("unlockedBlueprints")]
    public List<int> UnlockedBlueprints { get; set; } = new();

    [JsonPropertyName("activeOutpostId")]
    public int ActiveOutpostId { get; set; }

    [JsonPropertyName("dayCount")]
    public int DayCount { get; set; } = 1;

    [JsonPropertyName("waveLevel")]
    public int WaveLevel { get; set; } = 1;

    [JsonPropertyName("isGameOver")]
    public bool IsGameOver { get; set; }

    [JsonPropertyName("resourceStates")]
    public List<ResourceState> ResourceStates { get; set; } = new();
}

// ============================================================
// 干员运行时数据
// ============================================================

/// <summary>干员运行时快照</summary>
public class OperatorRuntime
{
    [JsonPropertyName("operatorId")]
    public int OperatorId { get; set; }

    [JsonPropertyName("currentLevel")]
    public int CurrentLevel { get; set; } = 1;

    [JsonPropertyName("currentExp")]
    public int CurrentExp { get; set; }

    [JsonPropertyName("currentHealth")]
    public int CurrentHealth { get; set; }

    [JsonPropertyName("maxHealth")]
    public int MaxHealth { get; set; }

    [JsonPropertyName("morale")]
    public int Morale { get; set; } = 100;

    [JsonPropertyName("posX")]
    public float PosX { get; set; }

    [JsonPropertyName("posY")]
    public float PosY { get; set; }

    [JsonPropertyName("isInjured")]
    public bool IsInjured { get; set; }

    [JsonPropertyName("injuryDaysLeft")]
    public int InjuryDaysLeft { get; set; }

    [JsonPropertyName("isFollowing")]
    public bool IsFollowing { get; set; } = true;

    [JsonPropertyName("trust")]
    public int Trust { get; set; }
}

// ============================================================
// 防御塔运行时数据
// ============================================================

/// <summary>防御塔运行时快照</summary>
public class TowerRuntime
{
    [JsonPropertyName("towerId")]
    public int TowerId { get; set; }

    [JsonPropertyName("currentLevel")]
    public int CurrentLevel { get; set; } = 1;

    [JsonPropertyName("posX")]
    public float PosX { get; set; }

    [JsonPropertyName("posY")]
    public float PosY { get; set; }

    [JsonPropertyName("currentDurability")]
    public int CurrentDurability { get; set; } = 100;
}

/// <summary>资源点状态（网格坐标 + 是否已搜索）</summary>
public class ResourceState
{
    [JsonPropertyName("gridX")]
    public int GridX { get; set; }

    [JsonPropertyName("gridY")]
    public int GridY { get; set; }

    [JsonPropertyName("collected")]
    public bool Collected { get; set; }
}

// ============================================================
// GameManager 状态快照
// ============================================================

/// <summary>GameManager 状态快照（供存档集成使用）</summary>
public class SaveState
{
    [JsonPropertyName("dayCount")]
    public int DayCount { get; set; } = 1;

    [JsonPropertyName("currentPhase")]
    public int CurrentPhase { get; set; } // DayPhase 枚举值

    [JsonPropertyName("currentState")]
    public int CurrentState { get; set; } // GameState 枚举值
}
