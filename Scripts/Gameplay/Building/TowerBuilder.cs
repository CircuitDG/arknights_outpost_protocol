using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Core.Grid;
using OutpostProtocol.Gameplay.Inventory;
using OutpostProtocol.Managers;
using OutpostProtocol.UI.Controllers;
using System;

namespace OutpostProtocol.Gameplay.Building;

/// <summary>
/// 塔建造系统
/// 职责：选择塔类型、预览位置、消耗资源建造
/// </summary>
public partial class TowerBuilder : Node
{
    // ============================================================
    // 导出变量
    // ============================================================

    [ExportGroup("建造配置")]
    [Export] public PackedScene[] TowerPrefabs;
    [Export] public Node2D TowerContainer;
    [Export] public Node2D PreviewNode;

    /// <summary>资源背包（建造消耗；未接线时允许免费建造）</summary>
    [Export] public Backpack Backpack;

    [ExportGroup("建造消耗")]
    [Export] public int[] BuildWoodCosts;
    [Export] public int[] BuildIronCosts;
    [Export] public int[] BuildOriginiumCosts;

    [ExportGroup("资源")]
    [Export] public int WoodAmount;
    [Export] public int IronAmount;
    [Export] public int OriginiumAmount;

    // ============================================================
    // 运行时状态
    // ============================================================

    private int _selectedTowerIndex = -1;
    private TowerBase _previewTower;
    private bool _isBuildingMode;
    private Vector2 _mouseWorldPos;

    // ============================================================
    // 公共属性
    // ============================================================

    public bool IsBuildingMode => _isBuildingMode;
    public int SelectedTowerIndex => _selectedTowerIndex;

    // ============================================================
    // 生命周期
    // ============================================================

    public override void _Ready()
    {
        if (TowerContainer == null)
        {
            TowerContainer = new Node2D { Name = "TowerContainer" };
            AddChild(TowerContainer);
        }

        // 自动查找博士背包
        if (Backpack == null)
        {
            var doctor = GetTree().GetFirstNodeInGroup("doctor") as Node2D;
            Backpack = doctor?.GetNodeOrNull<Backpack>("Backpack");
        }

        EventBus.Instance.GameStateChanged += OnGameStateChanged;
        GD.Print("[TowerBuilder] 初始化完成");
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.GameStateChanged -= OnGameStateChanged;
        }
    }

    public override void _Process(double delta)
    {
        if (!_isBuildingMode || _previewTower == null) return;

        // 更新预览位置
        _mouseWorldPos = GetGlobalMousePosition();
        Vector2 snappedPos = SnapToGrid(_mouseWorldPos);

        _previewTower.GlobalPosition = snappedPos;

        // 检查是否可以建造
        bool canBuild = CanBuildAt(snappedPos);
        _previewTower.Modulate = canBuild ? Colors.Green : Colors.Red;
    }

    // ============================================================
    // 建造模式
    // ============================================================

    /// <summary>进入建造模式</summary>
    public void StartBuildMode(int towerIndex)
    {
        if (towerIndex < 0 || towerIndex >= TowerPrefabs.Length)
        {
            GD.PushWarning($"[TowerBuilder] 无效的塔索引: {towerIndex}");
            return;
        }

        _selectedTowerIndex = towerIndex;
        _isBuildingMode = true;

        // 创建预览
        if (PreviewNode != null)
        {
            _previewTower = PreviewNode as TowerBase;
            if (_previewTower == null)
            {
                _previewTower = TowerPrefabs[towerIndex].Instantiate<TowerBase>();
                _previewTower.Modulate = Colors.Green;
                _previewTower.Visible = true;
                AddChild(_previewTower);
            }
        }
        else
        {
            _previewTower = TowerPrefabs[towerIndex].Instantiate<TowerBase>();
            _previewTower.Modulate = Colors.Green;
            _previewTower.Visible = true;
            AddChild(_previewTower);
        }

        GD.Print($"[TowerBuilder] 进入建造模式: {_previewTower.Data?.Name ?? "Unknown"}");
        EventBus.Instance.EmitLogMessage("进入建造模式，点击放置塔", "INFO");
    }

    /// <summary>退出建造模式</summary>
    public void ExitBuildMode()
    {
        _isBuildingMode = false;
        _selectedTowerIndex = -1;

        if (_previewTower != null)
        {
            _previewTower.QueueFree();
            _previewTower = null;
        }

        GD.Print("[TowerBuilder] 退出建造模式");
    }

    /// <summary>放置塔</summary>
    public bool PlaceTower()
    {
        if (!_isBuildingMode || _previewTower == null) return false;

        Vector2 pos = _previewTower.GlobalPosition;
        if (!CanBuildAt(pos))
        {
            GD.Print("[TowerBuilder] 无法在此位置建造");
            return false;
        }

        if (!HasResources())
        {
            GD.Print("[TowerBuilder] 资源不足");
            return false;
        }

        ConsumeResources();

        // 实例化真正的塔（先入树再设坐标）
        var tower = TowerPrefabs[_selectedTowerIndex].Instantiate<TowerBase>();
        TowerContainer.AddChild(tower);
        tower.GlobalPosition = pos;

        GD.Print($"[TowerBuilder] 塔已放置: {tower.Data?.Name ?? "Unknown"} 在 ({pos.X:F0}, {pos.Y:F0})");
        EventBus.Instance.EmitTowerBuilt(tower.TowerDataId);
        return true;
    }

    /// <summary>在指定位置放置塔（程序化建造/测试用）</summary>
    public bool PlaceTowerAt(Vector2 pos)
    {
        if (!_isBuildingMode || _previewTower == null) return false;

        _previewTower.GlobalPosition = SnapToGrid(pos);
        return PlaceTower();
    }

    // ============================================================
    // 建造检测
    // ============================================================

    private bool CanBuildAt(Vector2 pos)
    {
        var grid = GridManager.Instance;
        if (grid == null || !grid.IsBuilt) return false;

        // 检查是否在可行走网格上
        var gridPos = grid.WorldToGrid(pos);
        if (!grid.IsWalkable(gridPos)) return false;

        // 检查是否与其他塔重叠
        if (TowerContainer != null)
        {
            foreach (var child in TowerContainer.GetChildren())
            {
                if (child is TowerBase existing)
                {
                    float dist = pos.DistanceTo(existing.GlobalPosition);
                    if (dist < 30.0f) return false;
                }
            }
        }

        return true;
    }

    private Vector2 SnapToGrid(Vector2 pos)
    {
        var grid = GridManager.Instance;
        if (grid == null) return pos;

        var gridPos = grid.WorldToGrid(pos);
        return grid.GridToWorld(gridPos);
    }

    // ============================================================
    // 资源管理
    // ============================================================

    private bool HasResources()
    {
        if (Backpack == null) return true;

        int wood = GetDiscountedCost(GetBuildCost(BuildWoodCosts));
        int iron = GetDiscountedCost(GetBuildCost(BuildIronCosts));
        int originium = GetDiscountedCost(GetBuildCost(BuildOriginiumCosts));

        return Backpack.GetCount(Backpack.WOOD_ITEM_ID) >= wood &&
               Backpack.GetCount(Backpack.IRON_ITEM_ID) >= iron &&
               Backpack.GetCount(Backpack.ORIGINIUM_ITEM_ID) >= originium;
    }

    private void ConsumeResources()
    {
        if (Backpack == null) return;

        int wood = GetDiscountedCost(GetBuildCost(BuildWoodCosts));
        int iron = GetDiscountedCost(GetBuildCost(BuildIronCosts));
        int originium = GetDiscountedCost(GetBuildCost(BuildOriginiumCosts));

        Backpack.TrySpend(wood, iron, originium);
        GD.Print($"[TowerBuilder] 消耗资源: 木材{wood}, 铁{iron}, 源石{originium}");
    }

    private int GetBuildCost(int[] costs)
    {
        if (costs == null || _selectedTowerIndex < 0 || _selectedTowerIndex >= costs.Length) return 0;
        return costs[_selectedTowerIndex];
    }

    private static int GetDiscountedCost(int raw)
    {
        float reduction = TalentTreeController.TowerBuildCostReduction;
        return reduction > 0 ? (int)Math.Ceiling(raw * (1f - reduction)) : raw;
    }

    // ============================================================
    // 输入处理
    // ============================================================

    public override void _Input(InputEvent @event)
    {
        if (!_isBuildingMode) return;

        if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Left)
            {
                PlaceTower();
            }
            else if (mouseEvent.ButtonIndex == MouseButton.Right)
            {
                ExitBuildMode();
            }
        }

        if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
        {
            ExitBuildMode();
        }
    }

    // ============================================================
    // 事件回调
    // ============================================================

    private void OnGameStateChanged(GameState newState)
    {
        // 进入战斗阶段时退出建造模式
        if (newState == GameState.Battle && _isBuildingMode)
        {
            ExitBuildMode();
            GD.Print("[TowerBuilder] 战斗开始，退出建造模式");
        }
    }

    // ============================================================
    // 辅助方法
    // ============================================================

    private Vector2 GetGlobalMousePosition()
    {
        var viewport = GetViewport();
        var camera = viewport.GetCamera2D();
        if (camera == null) return Vector2.Zero;

        Vector2 mousePos = viewport.GetMousePosition();
        return camera.GlobalPosition + (mousePos - viewport.GetVisibleRect().Size * 0.5f) / camera.Zoom;
    }
}
