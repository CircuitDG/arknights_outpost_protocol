using System.Collections.Generic;

namespace OutpostProtocol.Data;

/// <summary>当日结算数据</summary>
public class SettlementData
{
    public int DayNumber { get; set; }
    public int TotalKills { get; set; }
    public int TotalExpGained { get; set; }
    public Dictionary<int, int> ResourcesGained { get; set; } = new(); // ItemId → Quantity
    public List<OperatorSettlementInfo> Operators { get; set; } = new();
    public int WaveNumber { get; set; }
    public bool IsGameOver { get; set; }
}

/// <summary>干员结算信息</summary>
public class OperatorSettlementInfo
{
    public int OperatorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int LevelBefore { get; set; }
    public int LevelAfter { get; set; }
    public int ExpGained { get; set; }
    public bool WasInjured { get; set; }
    public bool IsDown { get; set; }
    public int Kills { get; set; }
}
