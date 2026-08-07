using Godot;
using System.Collections.Generic;

namespace OutpostProtocol.UI.Views;

/// <summary>全屏地图（M 键打开，拖拽移动、滚轮缩放，显示关键地点）</summary>
public partial class WorldMapView : Control
{
    private float _zoom = 1.1f;
    private Vector2 _panOffset;
    private bool _dragging;
    private Vector2 _lastMouse;
    private List<MapMarker> _markers = new();
    private float _refreshTimer;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
    }

    public void Open()
    {
        _panOffset = MapMarkerCollector.GetMapSize() * 0.5f;
        _zoom = 1.1f;
        _markers = MapMarkerCollector.Collect(GetTree());
        Visible = true;
        QueueRedraw();
    }

    public void Close()
    {
        Visible = false;
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;

        _refreshTimer -= (float)delta;
        if (_refreshTimer <= 0)
        {
            _refreshTimer = 0.4f;
            _markers = MapMarkerCollector.Collect(GetTree());
            QueueRedraw();
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (!Visible) return;

        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp)
            {
                ZoomAt(GetViewport().GetMousePosition(), 1.18f);
                AcceptEvent();
            }
            else if (mb.ButtonIndex == MouseButton.WheelDown)
            {
                ZoomAt(GetViewport().GetMousePosition(), 0.85f);
                AcceptEvent();
            }
            else if (mb.ButtonIndex == MouseButton.Left)
            {
                _dragging = true;
                _lastMouse = GetViewport().GetMousePosition();
                AcceptEvent();
            }
        }
        else if (@event is InputEventMouseButton mbUp && !mbUp.Pressed && mbUp.ButtonIndex == MouseButton.Left)
        {
            _dragging = false;
            AcceptEvent();
        }
        else if (@event is InputEventMouseMotion mm && _dragging)
        {
            Vector2 current = GetViewport().GetMousePosition();
            _panOffset -= (current - _lastMouse) / _zoom;
            _lastMouse = current;
            QueueRedraw();
            AcceptEvent();
        }
    }

    private void ZoomAt(Vector2 screenPos, float factor)
    {
        float newZoom = Mathf.Clamp(_zoom * factor, 0.35f, 4f);
        Vector2 center = Size * 0.5f;
        Vector2 worldAtMouse = (screenPos - center) / _zoom + _panOffset;
        _panOffset = worldAtMouse - (screenPos - center) / newZoom;
        _zoom = newZoom;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var size = Size;
        DrawRect(new Rect2(Vector2.Zero, size), new Color(0.04f, 0.04f, 0.05f, 0.97f));
        DrawRect(new Rect2(Vector2.Zero, size), new Color(0.6f, 0.52f, 0.38f, 1f), false, 2f);

        Vector2 center = size * 0.5f;
        Font font = ThemeDB.FallbackFont;

        foreach (var marker in _markers)
        {
            Vector2 pos = (marker.WorldPos - _panOffset) * _zoom + center;
            if (pos.X < -30 || pos.Y < -30 || pos.X > size.X + 30 || pos.Y > size.Y + 30) continue;

            float r = marker.Radius * Mathf.Max(1f, _zoom * 0.8f);
            DrawCircle(pos, r, marker.Color);
            DrawCircle(pos, Mathf.Max(1f, r * 0.4f), Colors.White);

            if (!string.IsNullOrEmpty(marker.Label) && font != null)
            {
                DrawString(font, pos + new Vector2(r + 4, -r), marker.Label, HorizontalAlignment.Left, -1, 13, marker.Color);
            }
        }

        if (font != null)
        {
            DrawString(font, new Vector2(18, 24), "拖拽移动 · 滚轮缩放 · M / Esc 关闭", HorizontalAlignment.Left, -1, 14, new Color(0.9f, 0.86f, 0.72f));
        }
    }
}
