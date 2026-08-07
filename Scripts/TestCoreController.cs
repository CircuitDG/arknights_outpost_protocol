using Godot;
using OutpostProtocol.Gameplay.Building;
using OutpostProtocol.Gameplay.Character.Enemy;
using OutpostProtocol.Managers;

/// <summary>
/// 前哨站核心自动化测试：
/// 敌人到达目标点 → 攻击核心扣血 → 核心归零 → GameOver(CoreDestroyed)
/// </summary>
public partial class TestCoreController : Node
{
    private OutpostCore _core;
    private Enemy _enemy;
    private int _frameCount;

    public override void _Ready()
    {
        _core = GetNode<OutpostCore>("../TargetPoint/OutpostCore");
        _enemy = GetNode<Enemy>("../Enemy");
        GD.Print("========== 前哨站核心测试 ==========");
    }

    public override void _Process(double delta)
    {
        _frameCount++;

        if (_frameCount == 5)
        {
            // 敌人贴近核心（距离 16 < 30），下达目标指令；同时手动打掉 40 血测试休整修复
            if (_enemy != null && _core != null)
            {
                _enemy.SetTargetPosition(_core.GlobalPosition);
                _core.TakeDamage(40);
            }
            else
            {
                GD.PrintErr("[TestCore] 节点引用为空");
            }
        }
        else if (_frameCount == 8 || _frameCount == 11 || _frameCount == 14)
        {
            // 快速推进到休整期（Explore→Build→Battle→Rest），验证自动修复
            GameManager.Instance.SkipCurrentPhase();
        }
        else if (_frameCount == 20)
        {
            GD.Print($"[TestCore] 休整修复后核心 HP: {_core?.CurrentHealth}/{_core?.MaxHealth}（应为 100）, Day={GameManager.Instance.DayCount}");
        }
        else if (_frameCount == 25)
        {
            // 清除敌人，避免其后续攻击干扰摧毁测试
            _enemy?.TakeDamage(99999, null);
        }
        else if (_frameCount == 200)
        {
            if (_core != null)
            {
                _core.TakeDamage(999);
                GD.Print($"[TestCore] 核心摧毁: IsDestroyed={_core.IsDestroyed}, GameOverReason={GameManager.Instance.GameOverReason}, State={GameManager.Instance.CurrentState}");
            }
        }
        else if (_frameCount >= 210)
        {
            GD.Print("[TestCore] 测试完成");
            GetTree().Quit();
        }
    }
}
