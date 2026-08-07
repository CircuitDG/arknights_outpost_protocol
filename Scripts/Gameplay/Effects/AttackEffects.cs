using Godot;

namespace OutpostProtocol.Gameplay.Effects;

/// <summary>
/// 攻击表现辅助：弹道 + 受击/攻击脉冲
/// </summary>
public static class AttackEffects
{
    /// <summary>从攻击者向目标发射一条弹道光点</summary>
    public static void SpawnTracer(Node2D source, Node2D target, Color color)
    {
        if (source == null || target == null || source.GetTree() == null) return;

        var tracer = new TracerProjectile();
        source.GetTree().CurrentScene.AddChild(tracer);
        tracer.Fire(source.GlobalPosition, target.GlobalPosition, color);
    }

    /// <summary>攻击脉冲（缩放抖动）</summary>
    public static void Pulse(Node2D node, float baseScale, float peakScale = 1.12f)
    {
        if (node == null || !node.IsInsideTree()) return;

        var tween = node.CreateTween();
        tween.TweenProperty(node, "scale", new Vector2(peakScale, peakScale), 0.06f);
        tween.TweenProperty(node, "scale", new Vector2(baseScale, baseScale), 0.1f);
    }
}
