namespace BatteryTray.Core;

/// <summary>电量档位(GLOSSARY 托盘颜色规则):>40 绿,21–40 黄,≤20 红;None = 读取不到。</summary>
public enum BatteryBand { None, Good, Warn, Low }

/// <summary>托盘呈现规则(纯函数,WSL 可测):图标取色、电量档位与低电量提醒判定。</summary>
public static class TrayRules
{
    /// <summary>百分比 → 档位;null → None。图标与设备面板共用同一阈值。</summary>
    public static BatteryBand BandOf(byte? percent) => percent switch
    {
        null => BatteryBand.None,
        > 40 => BatteryBand.Good,
        > 20 => BatteryBand.Warn,
        _ => BatteryBand.Low,
    };

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

    /// <summary>
    /// 截断到 maxLength(含结尾的 …),不切断 UTF-16 代理对(emoji 设备名);
    /// NotifyIcon.Text 上限 63 字符,直接 Substring 可能把 emoji 切成孤立代理项产生非法字符串。
    /// </summary>
    public static string Ellipsize(string text, int maxLength)
    {
        if (maxLength <= 0) return string.Empty;
        if (text.Length <= maxLength) return text;
        var cut = maxLength - 1; // 留 … 的位置;保留区为 text[..cut],末字符是 text[cut-1]
        if (cut > 0 && char.IsHighSurrogate(text[cut - 1]))
            cut--; // 末字符是代理对前半:整对放弃
        return text[..cut] + "…";
    }
}
