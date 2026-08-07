using Godot;

namespace OutpostProtocol.Gameplay.Building.TowerTypes;

/// <summary>
/// 减速凝胶塔
/// 减速 + 源石伤害
/// </summary>
public partial class GelTower : TowerBase
{
    public override void _Ready()
    {
        TowerDataId = 2;
        base._Ready();
    }
}
