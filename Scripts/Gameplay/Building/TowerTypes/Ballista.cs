using Godot;

namespace OutpostProtocol.Gameplay.Building.TowerTypes;

/// <summary>
/// 弩炮台
/// 基础物理攻击塔
/// </summary>
public partial class Ballista : TowerBase
{
    public override void _Ready()
    {
        TowerDataId = 1;
        base._Ready();
    }
}
