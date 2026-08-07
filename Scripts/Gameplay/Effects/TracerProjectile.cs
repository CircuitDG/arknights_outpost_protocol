using Godot;

namespace OutpostProtocol.Gameplay.Effects;

/// <summary>
/// 简易攻击弹道（拖尾光点）
/// 从攻击者飞向目标后自动销毁
/// </summary>
public partial class TracerProjectile : Node2D
{
    private Color _color = new(1f, 0.85f, 0.45f);

    public void Fire(Vector2 from, Vector2 to, Color color, float duration = 0.18f)
    {
        GlobalPosition = from;
        _color = color;
        ZIndex = 80;
        QueueRedraw();

        var tween = CreateTween();
        tween.TweenProperty(this, "global_position", to, duration);
        tween.TweenCallback(Callable.From(QueueFree));
    }

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, 3.2f, _color);
        DrawCircle(Vector2.Zero, 1.4f, Colors.White);
    }
}
