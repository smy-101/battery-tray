namespace BatteryTray.Core;

/// <summary>日志格式与轮转规则(纯函数,WSL 可测)。</summary>
public static class LogRules
{
    /// <summary>一行日志:ISO8601 时间 + 级别 + 消息。</summary>
    public static string Format(DateTimeOffset time, string level, string message) =>
        $"{time:O} {level} {message}";

    /// <summary>当前文件 ≥ 上限时轮转(重命名为 .old)。</summary>
    public static bool ShouldRotate(long currentBytes, long maxBytes = 256 * 1024) =>
        currentBytes >= maxBytes;
}
