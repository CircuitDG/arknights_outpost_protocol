using Godot;

namespace OutpostProtocol.Gameplay.Building.TowerTypes;

/// <summary>
/// 源石爆裂塔
/// 范围法术伤害 + 地刺
/// </summary>
public partial class ExplosionTower : TowerBase
{
    public override void _Ready()
    {
        TowerDataId = 3;
        base._Ready();
    }
}
