using Godot;
using OutpostProtocol.Data;
using OutpostProtocol.Gameplay.Effects;
using OutpostProtocol.Gameplay.Entity;
using OutpostProtocol.Managers;

namespace OutpostProtocol.Gameplay.Building.TowerTypes;

/// <summary>
/// 铁丝网墙
/// 对经过的敌人造成伤害并施加 2 秒减速
/// </summary>
public partial class BarbedWire : TowerBase
{
    protected override void ExecuteAttack()
    {
        if (_currentTarget == null || _currentTarget.IsDead) return;

        int damage = CurrentDamage;
        _currentTarget.TakeDamage(damage, this);
        BuffManager.Instance?.AddBuff(_currentTarget, BuffType.MoveSpeed, -0.5f, 2f, sourceSkillId: "barbed_wire");

        AttackEffects.SpawnTracer(this, _currentTarget, new Color(0.82f, 0.82f, 0.88f));

        if (GD.Randf() < 0.1f)
        {
            GD.Print($"[{Name}] 铁丝网伤害 {_currentTarget.EntityName} -{damage}");
        }
    }
}
