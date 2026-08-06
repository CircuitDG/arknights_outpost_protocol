using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Core.Grid;
using OutpostProtocol.Gameplay.Character.Doctor;
using OutpostProtocol.Gameplay.Character.Operator;
using OutpostProtocol.Gameplay.Entity;
using OutpostProtocol.Managers;

/// <summary>
/// 干员系统自动化测试：
/// 1. 数据驱动初始化（芬/玫兰莎）
/// 2. 跟随博士
/// 3. 移动指令 + Doctor 攻击指令
/// 4. 攻击敌人造成伤害
/// 5. 战斗不能（Down）+ 复活
/// 6. 升级系统
/// </summary>
public partial class TestOperatorController : Node
{
    private const int GridWidth = 20;
    private const int GridHeight = 20;
    private const int TileSize = 16;

    private GridManager _grid;
    private Doctor _doctor;
    private Operator _op1;
    private Operator _op2;
    private BaseEntity _enemy;
    private TileMapLayer _groundLayer;
    private TileMapLayer _obstacleLayer;
    private int _frameCount;
    private bool _testsStarted;

    public override void _Ready()
    {
        _grid = GridManager.Instance;
        _doctor = GetNode<Doctor>("../Doctor");
        _op1 = GetNode<Operator>("../Operator_1");
        _op2 = GetNode<Operator>("../Operator_2");
        _enemy = GetNode<BaseEntity>("../Enemy");
        _groundLayer = GetNode<TileMapLayer>("../GroundLayer");
        _obstacleLayer = GetNode<TileMapLayer>("../ObstacleLayer");

        if (_grid == null || _op1 == null || _op2 == null || _enemy == null)
        {
            GD.PrintErr("[TestOperator] 缺少必要节点");
            return;
        }

        // 构建测试地图
        _grid.GridSize = TileSize;
        SetupTileSet(_groundLayer);
        SetupTileSet(_obstacleLayer);
        PaintGround();
        PaintObstacles();
        _grid.ObstacleLayer = _obstacleLayer;
        _grid.BuildGrid();

        // 订阅干员事件，验证信号链路
        EventBus.Instance.OperatorDown += OnOperatorDown;
        EventBus.Instance.OperatorRevived += OnOperatorRevived;

        GD.Print("[TestOperator] 测试初始化完成");
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.OperatorDown -= OnOperatorDown;
            EventBus.Instance.OperatorRevived -= OnOperatorRevived;
        }
    }

    public override void _Process(double delta)
    {
        _frameCount++;

        // 等 DataManager 异步加载 + 干员数据初始化
        if (!_testsStarted && DataManager.Instance.IsLoaded && _op1.Data != null && _op2.Data != null)
        {
            _testsStarted = true;
            StartTests();
        }
        else if (!_testsStarted && _frameCount >= 60)
        {
            GD.Print("[TestOperator] 数据加载超时，测试中止");
            GetTree().Quit();
            return;
        }

        if (!_testsStarted) return;

        if (_frameCount == 10)
        {
            // 移动指令：玫兰莎前往 (15,15)
            _op2.MoveToPosition(_grid.GridToWorld(15, 15));
            GD.Print($"[TestOperator] 移动指令后状态: {_op2.State}");
        }
        else if (_frameCount == 20)
        {
            // Doctor 攻击指令：范围内所有干员攻击敌人
            _doctor.CommandAttack(_enemy);
            GD.Print($"[TestOperator] 攻击指令后干员1状态: {_op1.State}, 目标: {_op1.Attack?.CurrentTarget?.EntityName}");
        }
        else if (_frameCount == 70)
        {
            GD.Print($"[TestOperator] 敌人剩余 HP: {_enemy.Health?.CurrentHealth}/{_enemy.Health?.MaxHealth}");
            GD.Print($"[TestOperator] 干员1: {_op1}");
            TestDownReviveLevel();
        }
        else if (_frameCount >= 80)
        {
            GD.Print("[TestOperator] 测试完成");
            GetTree().Quit();
        }
    }

    private void StartTests()
    {
        GD.Print("========== 干员初始化 ==========");
        GD.Print($"干员1: {_op1}");
        GD.Print($"干员2: {_op2}");
        GD.Print($"敌人: HP={_enemy.Health?.CurrentHealth}/{_enemy.Health?.MaxHealth}");
        GD.Print("=================================");

        // 加速攻击间隔，便于 headless 快速验证
        _op1.Attack.AttackInterval = 0.2f;
        _op2.Attack.AttackInterval = 0.2f;

        // 跟随博士
        _op1.FollowDoctor(_doctor);
        GD.Print($"[TestOperator] 跟随指令后干员1状态: {_op1.State}");
    }

    private void TestDownReviveLevel()
    {
        GD.Print("========== 战斗不能/复活/升级 ==========");

        // 致命伤害 → Down（不死亡）
        _op1.TakeDamage(999, _enemy);
        GD.Print($"[TestOperator] 致命伤害后: 状态={_op1.State}, IsDead={_op1.IsDead}");

        // 复活 → 满血 + Idle
        _op1.Revive();
        GD.Print($"[TestOperator] 复活后: 状态={_op1.State}, HP={_op1.Health?.CurrentHealth}/{_op1.Health?.MaxHealth}");

        // 升级：芬 Lv1 → Lv2（需求 100 经验）
        int levelBefore = _op1.CurrentLevel;
        _op1.AddExp(100);
        GD.Print($"[TestOperator] 升级: Lv.{levelBefore} → Lv.{_op1.CurrentLevel}, ATK={_op1.Attack?.AttackDamage}");

        GD.Print("========================================");
    }

    private void OnOperatorDown(Node2D op)
    {
        GD.Print($"[测试] 收到 OperatorDown 信号: {op.Name}");
    }

    private void OnOperatorRevived(Node2D op)
    {
        GD.Print($"[测试] 收到 OperatorRevived 信号: {op.Name}");
    }

    // ============================================================
    // 地图构建（与 TestGrid 相同）
    // ============================================================

    private void SetupTileSet(TileMapLayer layer)
    {
        var tileSet = new TileSet { TileSize = new Vector2I(TileSize, TileSize) };
        var atlas = new TileSetAtlasSource
        {
            Texture = GD.Load<Texture2D>("res://icon.svg"),
            TextureRegionSize = new Vector2I(TileSize, TileSize),
        };

        atlas.CreateTile(new Vector2I(0, 0));
        atlas.CreateTile(new Vector2I(1, 0));
        tileSet.AddSource(atlas, 0);
        layer.TileSet = tileSet;
    }

    private void PaintGround()
    {
        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                _groundLayer.SetCell(new Vector2I(x, y), 0, new Vector2I(0, 0));
            }
        }
    }

    private void PaintObstacles()
    {
        for (int x = 0; x < GridWidth; x++)
        {
            _obstacleLayer.SetCell(new Vector2I(x, 0), 0, new Vector2I(1, 0));
            _obstacleLayer.SetCell(new Vector2I(x, GridHeight - 1), 0, new Vector2I(1, 0));
        }
        for (int y = 0; y < GridHeight; y++)
        {
            _obstacleLayer.SetCell(new Vector2I(0, y), 0, new Vector2I(1, 0));
            _obstacleLayer.SetCell(new Vector2I(GridWidth - 1, y), 0, new Vector2I(1, 0));
        }

        for (int y = 5; y <= 14; y++)
        {
            _obstacleLayer.SetCell(new Vector2I(10, y), 0, new Vector2I(1, 0));
        }
    }
}
