using System.Collections.Generic;

namespace OutpostProtocol.Data;

/// <summary>每日统计数据</summary>
public class DailyStats
{
    public int DayNumber { get; set; }

    public int TotalKills { get; set; }
    public Dictionary<int, int> OperatorKills { get; set; } = new(); // OperatorId → Kills

    public int TotalExpGained { get; set; }
    public Dictionary<int, int> OperatorExpGained { get; set; } = new(); // OperatorId → Exp

    public Dictionary<int, int> ResourcesGained { get; set; } = new(); // ItemId → Quantity

    public int TowersBuilt { get; set; }
    public int TowersUpgraded { get; set; }

    public int CoreHealthLost { get; set; }
    public int CoreHealthRepaired { get; set; }

    public int WavesCleared { get; set; }

    public List<OperatorSnapshot> OperatorSnapshots { get; set; } = new();
}

/// <summary>干员状态快照（结算时采集）</summary>
public class OperatorSnapshot
{
    public int OperatorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public int Exp { get; set; }
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public int Morale { get; set; }
    public bool IsDown { get; set; }
}
