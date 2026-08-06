using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Core.Grid;
using OutpostProtocol.Gameplay.Building;
using OutpostProtocol.Gameplay.Character.Doctor;
using OutpostProtocol.Gameplay.Inventory;
using OutpostProtocol.Managers;

/// <summary>
/// 物品/背包/采集/建造消耗自动化测试：
/// 采集 → 背包 → 建造扣资源 → 升级扣资源 → 资源不足拒绝建造
/// </summary>
public partial class TestInventoryController : Node
{
    private Backpack _backpack;
    private GatherableResource _resource;
    private TowerBuilder _builder;
    private Node2D _towerContainer;
    private GridManager _grid;
    private TileMapLayer _groundLayer;
    private TileMapLayer _obstacleLayer;
    private int _frameCount;
    private int _inventoryEvents;

    public override void _Ready()
    {
        var doctor = GetNode<Doctor>("../Doctor");
        _backpack = doctor.Backpack;
        _resource = GetNode<GatherableResource>("../Gatherable_1");
        _builder = GetNode<TowerBuilder>("../TowerBuilder");
        _towerContainer = GetNode<Node2D>("../TowerContainer");
        _grid = GridManager.Instance;
        _groundLayer = GetNode<TileMapLayer>("../GroundLayer");
        _obstacleLayer = GetNode<TileMapLayer>("../ObstacleLayer");

        // 构建地图
        _grid.GridSize = 16;
        SetupTileSet(_groundLayer);
        SetupTileSet(_obstacleLayer);
        PaintGround();
        PaintObstacles();
        _grid.ObstacleLayer = _obstacleLayer;
        _grid.BuildGrid();

        // 注入建造引用
        _builder.TowerContainer = _towerContainer;
        _builder.TowerPrefabs = new[] { GD.Load<PackedScene>("res://Scenes/Buildings/Ballista.tscn") };
        _builder.BuildWoodCosts = new[] { 20 };
        _builder.BuildIronCosts = new[] { 5 };
        _builder.BuildOriginiumCosts = new[] { 0 };

        EventBus.Instance.InventoryChanged += OnInventoryChanged;
        GD.Print("========== 背包/采集测试 ==========");
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.InventoryChanged -= OnInventoryChanged;
        }
    }

    public override void _Process(double delta)
    {
        _frameCount++;

        if (_frameCount == 5)
        {
            // 采集 3 次（资源点 MaxAmount=3）
            int gathered = 0;
            while (_resource.Gather()) gathered++;
            GD.Print($"[TestInventory] 采集 {gathered} 次，木材: {_backpack.GetCount(Backpack.WOOD_ITEM_ID)}");
        }
        else if (_frameCount == 8)
        {
            // 补足建造资源（木材堆叠上限 99，已有 3 个，最多补 96）
            _backpack.AddItem(Backpack.WOOD_ITEM_ID, 96);
            _backpack.AddItem(Backpack.IRON_ITEM_ID, 50);
            GD.Print($"[TestInventory] 补足资源后 — 木材:{_backpack.GetCount(Backpack.WOOD_ITEM_ID)}, 铁:{_backpack.GetCount(Backpack.IRON_ITEM_ID)}");
        }
        else if (_frameCount == 10)
        {
            // 建造弩炮台（消耗 20 木 + 5 铁）
            _builder.StartBuildMode(0);
            bool placed = _builder.PlaceTowerAt(new Vector2(136, 168)); // 网格 (8,10)，可行走
            _builder.ExitBuildMode();
            GD.Print($"[TestInventory] 建造结果: {placed} — 木材:{_backpack.GetCount(Backpack.WOOD_ITEM_ID)}, 铁:{_backpack.GetCount(Backpack.IRON_ITEM_ID)}");
        }
        else if (_frameCount == 12)
        {
            // 升级 Lv.1→2（消耗 15 木 + 8 铁）
            var tower = GetTower(0);
            bool upgraded = tower != null && tower.Upgrade();
            GD.Print($"[TestInventory] 升级结果: {upgraded} — 木材:{_backpack.GetCount(Backpack.WOOD_ITEM_ID)}, 铁:{_backpack.GetCount(Backpack.IRON_ITEM_ID)}");
        }
        else if (_frameCount == 14)
        {
            // 资源不足：木材降到 1，建造应失败
            _backpack.RemoveItem(Backpack.WOOD_ITEM_ID, _backpack.GetCount(Backpack.WOOD_ITEM_ID) - 1);
            _builder.StartBuildMode(0);
            bool placed = _builder.PlaceTowerAt(new Vector2(200, 200));
            _builder.ExitBuildMode();
            GD.Print($"[TestInventory] 资源不足时建造: {placed}（应为 False）");
        }
        else if (_frameCount >= 18)
        {
            GD.Print($"[TestInventory] 测试完成 — InventoryChanged 事件触发 {_inventoryEvents} 次");
            GetTree().Quit();
        }
    }

    private TowerBase GetTower(int index)
    {
        int i = 0;
        foreach (var child in _towerContainer.GetChildren())
        {
            if (child is TowerBase tower)
            {
                if (i == index) return tower;
                i++;
            }
        }
        return null;
    }

    private void OnInventoryChanged()
    {
        _inventoryEvents++;
    }

    // ============================================================
    // 地图构建
    // ============================================================

    private void SetupTileSet(TileMapLayer layer)
    {
        var tileSet = new TileSet { TileSize = new Vector2I(16, 16) };
        var atlas = new TileSetAtlasSource
        {
            Texture = GD.Load<Texture2D>("res://icon.svg"),
            TextureRegionSize = new Vector2I(16, 16),
        };

        atlas.CreateTile(new Vector2I(0, 0));
        atlas.CreateTile(new Vector2I(1, 0));
        tileSet.AddSource(atlas, 0);
        layer.TileSet = tileSet;
    }

    private void PaintGround()
    {
        for (int x = 0; x < 20; x++)
        {
            for (int y = 0; y < 20; y++)
            {
                _groundLayer.SetCell(new Vector2I(x, y), 0, new Vector2I(0, 0));
            }
        }
    }

    private void PaintObstacles()
    {
        for (int x = 0; x < 20; x++)
        {
            _obstacleLayer.SetCell(new Vector2I(x, 0), 0, new Vector2I(1, 0));
            _obstacleLayer.SetCell(new Vector2I(x, 19), 0, new Vector2I(1, 0));
        }
        for (int y = 0; y < 20; y++)
        {
            _obstacleLayer.SetCell(new Vector2I(0, y), 0, new Vector2I(1, 0));
            _obstacleLayer.SetCell(new Vector2I(19, y), 0, new Vector2I(1, 0));
        }
        for (int y = 5; y <= 14; y++)
        {
            _obstacleLayer.SetCell(new Vector2I(10, y), 0, new Vector2I(1, 0));
        }
    }
}
