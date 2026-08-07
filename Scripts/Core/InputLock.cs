namespace OutpostProtocol.Core;

/// <summary>
/// 全局输入锁：设置等模态界面打开时，屏蔽游戏世界输入（移动/指令/建造）
/// </summary>
public static class InputLock
{
    public static bool IsLocked { get; private set; }

    public static void SetLocked(bool locked)
    {
        IsLocked = locked;
    }
}
