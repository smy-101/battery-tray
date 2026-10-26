namespace BatteryTray.Core;

/// <summary>托盘呈现规则(纯函数,WSL 可测):图标取色与低电量提醒判定。</summary>
public static class TrayRules
{
    /// <summary>
    /// 图标电量 = 全部"已连接且电量可读"设备中的最低值;
    /// 无可读设备返回 null(图标转空心灰)。
    /// </summary>
    public static byte? MinBatteryPercent(IEnumerable<DeviceEntry> entries) =>
        entries.Where(e => e.IsConnected && e.BatteryPercent is not null)
               .Select(e => (byte?)e.BatteryPercent)
               .Min();

    /// <summary>
    /// 需要弹提醒的设备:after 中已连接且电量 &lt; threshold,
    /// 且 before 中同设备无电量记录或电量 ≥ threshold(持续低位不重复弹)。
    /// </summary>
    public static IReadOnlyList<DeviceEntry> NewlyLow(
        IEnumerable<DeviceEntry> before, IEnumerable<DeviceEntry> after, byte threshold)
    {
        var prev = before
            .Where(e => e.BatteryPercent is not null)
            .GroupBy(e => (e.Name, e.Transport))
            .ToDictionary(g => g.Key, g => g.First().BatteryPercent!.Value);

        return after
            .Where(a => a.IsConnected
                        && a.BatteryPercent is not null
                        && a.BatteryPercent.Value < threshold
                        && (!prev.TryGetValue((a.Name, a.Transport), out var p) || p >= threshold))
            .ToList();
    }
}
