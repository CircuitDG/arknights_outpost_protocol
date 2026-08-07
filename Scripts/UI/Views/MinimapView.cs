using Godot;
using OutpostProtocol.Core.Grid;
using OutpostProtocol.Gameplay.Building;
using OutpostProtocol.Gameplay.Character.Enemy;
using System.Collections.Generic;

namespace OutpostProtocol.UI.Views;

/// <summary>地图标记</summary>
public readonly struct MapMarker
{
    public Vector2 WorldPos { get; }
    public Color Color { get; }
    public string Label { get; }
    public float Radius { get; }

    public MapMarker(Vector2 worldPos, Color color, string label = "", float radius = 3f)
    {
        WorldPos = worldPos;
        Color = color;
        Label = label;
        Radius = radius;
    }
}

/// <summary>收集地图关键地点（博士/干员/核心/塔/敌人/资源点）</summary>
public static class MapMarkerCollector
{
    public static Vector2 GetMapSize()
    {
        var grid = GridManager.Instance;
        if (grid != null && grid.IsBuilt)
        {
            var dims = grid.GridDimensions;
            return new Vector2(dims.X * grid.GridSize, dims.Y * grid.GridSize);
        }
        return new Vector2(3200, 3200);
    }

    public static List<MapMarker> Collect(SceneTree tree)
    {
        var markers = new List<MapMarker>();
        if (tree == null) return markers;

        foreach (var node in tree.GetNodesInGroup("doctor"))
        {
            if (node is Node2D d)
            {
                markers.Add(new MapMarker(d.GlobalPosition, Colors.White, "博士", 5f));
            }
        }

        foreach (var node in tree.GetNodesInGroup("outpost_core"))
        {
            if (node is Node2D core)
            {
                markers.Add(new MapMarker(core.GlobalPosition, Colors.Red, "前哨站", 6f));
            }
        }

        foreach (var node in tree.GetNodesInGroup("operators"))
        {
            if (node is Node2D op)
            {
                markers.Add(new MapMarker(op.GlobalPosition, new Color(0.35f, 0.6f, 1f), op.Name, 4f));
            }
        }

        foreach (var node in tree.GetNodesInGroup("towers"))
        {
            if (node is Node2D tower)
            {
                markers.Add(new MapMarker(tower.GlobalPosition, new Color(0.4f, 0.85f, 0.4f), "塔", 4f));
            }
        }

        foreach (var node in tree.GetNodesInGroup("enemies"))
        {
            if (node is Node2D enemy)
            {
                markers.Add(new MapMarker(enemy.GlobalPosition, new Color(0.95f, 0.25f, 0.25f), "", 2.5f));
            }
        }

        foreach (var node in tree.GetNodesInGroup("gatherable_resources"))
        {
            if (node is Node2D res)
            {
                markers.Add(new MapMarker(res.GlobalPosition, new Color(0.62f, 0.48f, 0.28f), "", 2.5f));
            }
        }

        return markers;
    }
}

/// <summary>左下/左中迷你小地图</summary>
public partial class MinimapView : Control
{
    private float _refreshTimer;
    private List<MapMarker> _markers = new();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        _refreshTimer -= (float)delta;
        if (_refreshTimer <= 0)
        {
            _refreshTimer = 0.25f;
            _markers = MapMarkerCollector.Collect(GetTree());
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        var size = Size;
        DrawRect(new Rect2(Vector2.Zero, size), new Color(0.06f, 0.06f, 0.08f, 0.88f));
        DrawRect(new Rect2(Vector2.Zero, size), new Color(0.55f, 0.48f, 0.35f, 0.9f), false, 1.5f);

        Vector2 mapSize = MapMarkerCollector.GetMapSize();
        float scale = Mathf.Min(size.X, size.Y) / Mathf.Max(mapSize.X, mapSize.Y);
        Vector2 offset = (size - mapSize * scale) * 0.5f;

        foreach (var marker in _markers)
        {
            Vector2 pos = marker.WorldPos * scale + offset;
            float r = Mathf.Max(1.5f, marker.Radius * 0.6f);
            DrawRect(new Rect2(pos - new Vector2(r, r), new Vector2(r * 2, r * 2)), marker.Color);
        }
    }
}
