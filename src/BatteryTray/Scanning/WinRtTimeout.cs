namespace BatteryTray.Scanning;

/// <summary>
/// WinRT 异步操作的超时包装:超时返回 null,不取消底层操作
/// (与 HID++ 事务同一策略;被放弃的操作自然完成后由其自身 finally 释放资源)。
/// </summary>
internal static class WinRtTimeout
{
    public static async Task<T?> After<T>(Task<T> task, int ms) where T : class
    {
        var done = await Task.WhenAny(task, Task.Delay(ms));
        if (done != task) return null;
        try { return await task; }
        catch { return null; }
    }

    /// <summary>可空值类型版本(Task&lt;byte?&gt; 等):超时/异常返回 null。</summary>
    public static async Task<T?> After<T>(Task<T?> task, int ms) where T : struct
    {
        var done = await Task.WhenAny(task, Task.Delay(ms));
        if (done != task) return null;
        try { return await task; }
        catch { return null; }
    }
}
