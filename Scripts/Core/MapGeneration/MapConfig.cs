namespace OutpostProtocol.Core.MapGeneration;

/// <summary>地图生成配置</summary>
public class MapConfig
{
    public int Width = 200;
    public int Height = 200;
    public long Seed = 12345;

    // 街道
    public int MainStreetSpacing = 20; // 街道间距（格）
    public int MainStreetWidth = 5;
    public int SideStreetWidth = 3;

    // 建筑
    public int MinBuildingSize = 4;
    public int MaxBuildingSize = 10;
    public float BlockFillRate = 0.6f;
    public int MaxBuildAttempts = 50;

    // 资源
    public float ResourceDensity = 0.3f;
    public int MaxResourcesPerBuilding = 8;

    // 装饰
    public float TreeDensity = 0.03f;
}
