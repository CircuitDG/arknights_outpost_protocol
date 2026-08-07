using Godot;
using OutpostProtocol.Gameplay.Character.Operator;
using OutpostProtocol.Gameplay.Effects;
using OutpostProtocol.Gameplay.Entity;

namespace OutpostProtocol.Gameplay.Building.TowerTypes;

/// <summary>
/// 医疗无人机平台
/// 自动治疗射程内的友军（干员/博士），不攻击敌人
/// </summary>
public partial class MedicTower : TowerBase
{
    protected override bool AcceptsEntity(BaseEntity entity)
    {
        return entity != null && entity.Faction == FactionType.Player && !entity.IsDead;
    }

    protected override void ExecuteAttack()
    {
        if (_currentTarget == null || _currentTarget.IsDead || _currentTarget is not Operator op)
        {
            return;
        }
        if (op.Health == null) return;

        int heal = Mathf.Max(1, CurrentDamage);
        op.Heal(heal);

        AttackEffects.SpawnTracer(this, op, new Color(0.4f, 1f, 0.6f));
        AttackEffects.Pulse(this, 1f, 1.06f);
        OutpostProtocol.Managers.AudioManager.Instance?.Play("heal", -12f);

        if (GD.Randf() < 0.1f)
        {
            GD.Print($"[{Name}] 治疗 {op.EntityName} +{heal}");
        }
    }
}
