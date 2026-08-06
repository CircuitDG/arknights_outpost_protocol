using Godot;
using OutpostProtocol.Core.Grid;
using System.Collections.Generic;

namespace OutpostProtocol.Gameplay.Entity.Components;

/// <summary>
/// 移动组件
/// 职责：路径跟随、平滑移动、软排斥
/// 挂载在 BaseEntity 或任意 Node2D 下
/// </summary>
public partial class MovementComponent : Node2D
{
    // ============================================================
    // 导出变量
    // ============================================================

    [ExportGroup("移动参数")]
    [Export] public float Speed = 200.0f;
    [Export] public float ArrivalDistance = 2.0f; // 到达判定距离

    [ExportGroup("软排斥")]
    [Export] public float SoftRadius = 16.0f;
    [Export] public float RepulsionStrength = 50.0f;

    [ExportGroup("调试")]
    [Export] public bool ShowPath = false; // 是否显示路径线

    // ============================================================
    // 运行时状态
    // ============================================================

    private List<Vector2> _currentPath = new();
    private int _pathIndex;
    private bool _isMoving;
    private Vector2 _targetPosition;
    private Node2D _owner;

    // ============================================================
    // 公共属性
    // ============================================================

    public bool IsMoving => _isMoving;
    public Vector2 TargetPosition => _targetPosition;
    public List<Vector2> CurrentPath => _currentPath;
    public float Progress
    {
        get
        {
            if (_currentPath.Count <= 1) return 0f;
            return (float)_pathIndex / (_currentPath.Count - 1);
        }
    }

    // ============================================================
    // 生命周期
    // ============================================================

    public override void _Ready()
    {
        _owner = GetParent<Node2D>();
        if (_owner == null)
        {
            GD.PushError("[MovementComponent] 必须挂载在 Node2D 下");
            return;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        // 如果不在移动状态，只处理软排斥
        if (!_isMoving)
        {
            ApplySoftRepulsion(dt);
            return;
        }

        // 路径跟随
        FollowPath(dt);

        // 软排斥（防止单位重叠）
        ApplySoftRepulsion(dt);
    }

    // ============================================================
    // 路径跟随
    // ============================================================

    /// <summary>移动到指定目标点（自动寻路）</summary>
    public void MoveTo(Vector2 target)
    {
        var grid = GridManager.Instance;
        if (grid == null || !grid.IsBuilt)
        {
            GD.PushWarning("[MovementComponent] GridManager 未就绪");
            return;
        }

        _targetPosition = target;

        // 异步寻路
        _ = FindPathAsync(target);
    }

    /// <summary>沿指定路径移动（预设路径）</summary>
    public void FollowPath(List<Vector2> path)
    {
        if (path == null || path.Count == 0)
        {
            Stop();
            return;
        }

        _currentPath = path;
        _pathIndex = 0;
        _isMoving = true;

        // 如果路径只有一个点（即当前位置），直接到达
        if (_currentPath.Count == 1)
        {
            _owner.GlobalPosition = _currentPath[0];
            _isMoving = false;
        }
    }

    /// <summary>停止移动</summary>
    public void Stop()
    {
        _isMoving = false;
        _currentPath.Clear();
        _pathIndex = 0;
    }

    /// <summary>异步寻路并跟随</summary>
    private async System.Threading.Tasks.Task FindPathAsync(Vector2 target)
    {
        var grid = GridManager.Instance;
        if (grid == null) return;

        var path = await AStarPathfinder.FindPathAsync(
            _owner.GlobalPosition,
            target,
            grid
        );

        // 切换到主线程应用路径
        Callable.From(() =>
        {
            if (path.Count > 0)
            {
                FollowPath(path);
            }
            else
            {
                Stop();
                GD.Print($"[MovementComponent] 无法到达目标: {target}");
            }
        }).CallDeferred();
    }

    /// <summary>逐点跟随路径</summary>
    private void FollowPath(float dt)
    {
        if (_currentPath.Count == 0 || _pathIndex >= _currentPath.Count)
        {
            _isMoving = false;
            return;
        }

            Vector2 target = _currentPath[_pathIndex];
            Vector2 direction = (target - _owner.GlobalPosition).Normalized();
            float distance = _owner.GlobalPosition.DistanceTo(target);

        // 如果到达当前路径点
        if (distance < ArrivalDistance)
        {
            _pathIndex++;

            if (_pathIndex >= _currentPath.Count)
            {
                // 路径走完
                _isMoving = false;
                    _owner.GlobalPosition = _currentPath[_currentPath.Count - 1];
                    return;
                }

                // 继续下一个点
                target = _currentPath[_pathIndex];
                direction = (target - _owner.GlobalPosition).Normalized();
            }

            // 移动
            _owner.GlobalPosition += direction * Speed * dt;
    }

    // ============================================================
    // 软排斥（防止单位完全重叠）
    // ============================================================

    private void ApplySoftRepulsion(float dt)
    {
        // 使用 Physics2D 检测周围单位
        var space = GetWorld2D().DirectSpaceState;
        var query = new PhysicsShapeQueryParameters2D();
        query.Shape = new CircleShape2D { Radius = SoftRadius };
        query.Transform = new Transform2D(0, GlobalPosition);
        query.CollisionMask = uint.MaxValue; // 检测所有碰撞层
        query.CollideWithBodies = true;
        query.CollideWithAreas = true;

        var results = space.IntersectShape(query);
        Vector2 repulsion = Vector2.Zero;

        foreach (var result in results)
        {
            var collider = result["collider"].As<Node2D>();
            if (collider == null || collider == _owner) continue;

            // 只对同一父级下的其他实体施加排斥
            if (collider.GetParent() != _owner.GetParent()) continue;

            Vector2 diff = GlobalPosition - collider.GlobalPosition;
            float dist = diff.Length();

            if (dist < SoftRadius && dist > 0.1f)
            {
                float strength = (SoftRadius - dist) / SoftRadius;
                repulsion += diff.Normalized() * strength * RepulsionStrength;
            }
        }

        if (repulsion != Vector2.Zero)
        {
            _owner.GlobalPosition += repulsion * dt;
        }
    }

    // ============================================================
    // 调试绘制
    // ============================================================

    public override void _Draw()
    {
        if (!ShowPath || _currentPath.Count == 0) return;

        // 绘制路径线
        var localPoints = new Vector2[_currentPath.Count];
        for (int i = 0; i < _currentPath.Count; i++)
        {
            localPoints[i] = _currentPath[i] - GlobalPosition;
        }
        DrawPolyline(localPoints, Colors.Yellow, 1.5f);

        // 绘制目标点
        DrawCircle(_currentPath[_currentPath.Count - 1] - GlobalPosition, 3, Colors.Red);

        // 绘制当前目标点
        if (_pathIndex < _currentPath.Count)
        {
            DrawCircle(_currentPath[_pathIndex] - GlobalPosition, 4, Colors.Green);
        }
    }

    public override void _Process(double delta)
    {
        if (ShowPath)
        {
            QueueRedraw();
        }
    }
}
