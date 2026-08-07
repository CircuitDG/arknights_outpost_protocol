using Godot;
using System.Collections.Generic;

namespace OutpostProtocol.UI.Views;

/// <summary>
/// 夜晚迷雾（PZ 风格）
/// 黑色遮罩按夜晚程度加深，角色/干员/防御塔周围通过光晕“照亮”路面
/// </summary>
public partial class NightFogLayer : CanvasLayer
{
    private ColorRect _fogRect;
    private Node2D _lights;
    private Texture2D _glow;
    private float _targetAlpha;
    private readonly List<Sprite2D> _lightSprites = new();

    public override void _Ready()
    {
        Layer = 1;

        _fogRect = GetNodeOrNull<ColorRect>("FogRect");
        _lights = GetNodeOrNull<Node2D>("FogLights");
        if (_fogRect == null)
        {
            _fogRect = new ColorRect
            {
                Color = new Color(0, 0, 0, 1),
                Modulate = new Color(1, 1, 1, 0),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _fogRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(_fogRect);
        }
        if (_lights == null)
        {
            _lights = new Node2D { Name = "FogLights" };
            AddChild(_lights);
        }

        _glow = GD.Load<Texture2D>("res://Assets/UI/light_glow.png");
    }

    public override void _Process(double delta)
    {
        float current = _fogRect.Modulate.A;
        float next = Mathf.MoveToward(current, _targetAlpha, 0.9f * (float)delta);
        _fogRect.Modulate = new Color(1, 1, 1, next);
    }

    /// <summary>更新迷雾强度与光源列表</summary>
    public void UpdateFog(float alpha, IReadOnlyList<(Vector2 Position, float Radius)> lights)
    {
        _targetAlpha = Mathf.Clamp(alpha, 0f, 0.85f);

        while (_lightSprites.Count > lights.Count)
        {
            var sprite = _lightSprites[^1];
            _lightSprites.RemoveAt(_lightSprites.Count - 1);
            sprite.QueueFree();
        }

        for (int i = 0; i < lights.Count; i++)
        {
            Sprite2D sprite;
            if (i >= _lightSprites.Count)
            {
                sprite = new Sprite2D
                {
                    Texture = _glow,
                    Material = new CanvasItemMaterial
                    {
                        BlendMode = CanvasItemMaterial.BlendModeEnum.Add,
                    },
                    SelfModulate = new Color(1, 1, 1, 0.85f),
                    Centered = true,
                };
                _lights.AddChild(sprite);
                _lightSprites.Add(sprite);
            }
            else
            {
                sprite = _lightSprites[i];
            }

            sprite.GlobalPosition = lights[i].Position;
            float scale = Mathf.Max(0.2f, lights[i].Radius / 64f);
            sprite.Scale = new Vector2(scale, scale);
        }
    }
}
