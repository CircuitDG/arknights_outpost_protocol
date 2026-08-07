using Godot;

namespace OutpostProtocol.Gameplay.Character.Enemy;

/// <summary>
/// 敌人 AI 控制器（轻量级）
/// 职责：状态管理和行为调度（主逻辑在 Enemy._Process 中）
/// </summary>
public partial class EnemyAI : Node
{
    private Enemy _enemy;

    public override void _Ready()
    {
        _enemy = GetParent<Enemy>();
        if (_enemy == null)
        {
            GD.PushError("[EnemyAI] 必须挂载在 Enemy 下");
        }
    }

    public override void _Process(double delta)
    {
        if (_enemy == null || _enemy.IsDead) return;
        // 主逻辑由 Enemy._Process 处理，这里仅做扩展
    }
}
