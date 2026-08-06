using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Data;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace OutpostProtocol.Managers;

/// <summary>
/// 数据管理器（AutoLoad 单例）
/// 职责：加载 JSON 配置表，缓存为 Dictionary&lt;int, T&gt;
/// </summary>
public partial class DataManager : Node
{
    // ============================================================
    // 单例
    // ============================================================

    private static DataManager _instance;

    /// <summary>全局单例实例（AutoLoad 就绪后可用）</summary>
    public static DataManager Instance => _instance;

    // ============================================================
    // 数据缓存
    // ============================================================

    private readonly Dictionary<int, OperatorData> _operatorDict = new();
    private readonly Dictionary<int, CollectionData> _collectionDict = new();
    private readonly Dictionary<int, TowerData> _towerDict = new();
    private readonly Dictionary<int, EnemyWaveData> _waveDict = new();
    private readonly Dictionary<int, ItemData> _itemDict = new();
    private readonly Dictionary<string, SkillData> _skillDict = new();

    // ============================================================
    // 公共访问属性
    // ============================================================

    public IReadOnlyDictionary<int, OperatorData> Operators => _operatorDict;
    public IReadOnlyDictionary<int, CollectionData> Collections => _collectionDict;
    public IReadOnlyDictionary<int, TowerData> Towers => _towerDict;
    public IReadOnlyDictionary<int, EnemyWaveData> Waves => _waveDict;
    public IReadOnlyDictionary<int, ItemData> Items => _itemDict;
    public IReadOnlyDictionary<string, SkillData> Skills => _skillDict;

    // ============================================================
    // 加载状态
    // ============================================================

    private bool _isLoaded;

    /// <summary>全部配置表是否加载完成</summary>
    public bool IsLoaded => _isLoaded;

    // ============================================================
    // 生命周期
    // ============================================================

    public override void _Ready()
    {
        if (_instance != null)
        {
            GD.PushWarning("DataManager 已存在，销毁重复实例");
            QueueFree();
            return;
        }

        _instance = this;

        // 异步加载数据，不阻塞主线程
        _ = LoadAllDataAsync();
    }

    public override void _ExitTree()
    {
        _instance = null;
    }

    // ============================================================
    // 数据加载
    // ============================================================

    /// <summary>异步加载所有 JSON 配置表</summary>
    private async Task LoadAllDataAsync()
    {
        GD.Print("[DataManager] 开始加载配置表...");

        var tasks = new List<Task>
        {
            LoadDataAsync<OperatorData>("res://Data/OperatorData.json", items =>
            {
                foreach (var item in items) _operatorDict[item.Id] = item;
            }),
            LoadDataAsync<CollectionData>("res://Data/CollectionData.json", items =>
            {
                foreach (var item in items) _collectionDict[item.Id] = item;
            }),
            LoadDataAsync<TowerData>("res://Data/TowerData.json", items =>
            {
                foreach (var item in items) _towerDict[item.Id] = item;
            }),
            LoadDataAsync<EnemyWaveData>("res://Data/EnemyWaveData.json", items =>
            {
                foreach (var item in items) _waveDict[item.Id] = item;
            }),
            LoadDataAsync<ItemData>("res://Data/ItemData.json", items =>
            {
                foreach (var item in items) _itemDict[item.Id] = item;
            }),
            LoadDataAsync<SkillData>("res://Data/SkillData.json", items =>
            {
                foreach (var item in items) _skillDict[item.Id] = item;
            }),
        };

        await Task.WhenAll(tasks);

        _isLoaded = true;
        GD.Print($"[DataManager] 加载完成 — 干员:{_operatorDict.Count}, 藏品:{_collectionDict.Count}, 塔:{_towerDict.Count}, 波次:{_waveDict.Count}, 物品:{_itemDict.Count}, 技能:{_skillDict.Count}");

        // 广播加载完成
        EventBus.Instance.EmitLogMessage("DataManager 加载完成", "INFO");
    }

    /// <summary>
    /// 加载指定类型的 JSON 数据文件。
    /// 使用 FileAccess 读取 res:// 资源，导出为 PCK 后依然可用；
    /// 文件 IO 放到后台线程，反序列化结果回到主线程写入缓存。
    /// </summary>
    private async Task LoadDataAsync<T>(string path, Action<List<T>> applyAction) where T : class
    {
        try
        {
            string json = await Task.Run(() =>
            {
                using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
                if (file == null)
                {
                    GD.PushWarning($"[DataManager] 文件不存在: {path}");
                    return null;
                }
                return file.GetAsText();
            });

            if (json == null) return;

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };

            var items = JsonSerializer.Deserialize<List<T>>(json, options);
            if (items == null)
            {
                GD.PushWarning($"[DataManager] 反序列化失败: {path}");
                return;
            }

            applyAction(items);
            GD.Print($"[DataManager] 加载成功: {path} → {items.Count} 条");
        }
        catch (Exception ex)
        {
            GD.PushError($"[DataManager] 加载失败: {path} | {ex.Message}");
        }
    }

    // ============================================================
    // 数据查询 API
    // ============================================================

    /// <summary>获取干员数据</summary>
    public OperatorData GetOperator(int id)
    {
        return _operatorDict.GetValueOrDefault(id);
    }

    /// <summary>获取藏品数据</summary>
    public CollectionData GetCollection(int id)
    {
        return _collectionDict.GetValueOrDefault(id);
    }

    /// <summary>获取防御塔数据</summary>
    public TowerData GetTower(int id)
    {
        return _towerDict.GetValueOrDefault(id);
    }

    /// <summary>获取波次数据</summary>
    public EnemyWaveData GetWave(int id)
    {
        return _waveDict.GetValueOrDefault(id);
    }

    /// <summary>获取物品数据</summary>
    public ItemData GetItem(int id)
    {
        return _itemDict.GetValueOrDefault(id);
    }

    /// <summary>获取技能数据</summary>
    public SkillData GetSkill(string id)
    {
        return _skillDict.GetValueOrDefault(id);
    }

    /// <summary>按波次编号获取波次数据</summary>
    public EnemyWaveData GetWaveByNumber(int waveNumber)
    {
        foreach (var wave in _waveDict.Values)
        {
            if (wave.WaveNumber == waveNumber)
                return wave;
        }
        return null;
    }

    /// <summary>获取所有干员 ID 列表</summary>
    public List<int> GetAllOperatorIds()
    {
        return new List<int>(_operatorDict.Keys);
    }

    /// <summary>获取所有藏品 ID 列表</summary>
    public List<int> GetAllCollectionIds()
    {
        return new List<int>(_collectionDict.Keys);
    }
}
