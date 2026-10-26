namespace BatteryTray.Core;

/// <summary>
/// 设备快照仓:把每次原始枚举(可能含重复)合并成稳定排序的展示列表。
/// 线程安全:扫描在后台,读取在 UI 线程。
/// </summary>
public sealed class DeviceCatalog
{
    private readonly object _gate = new();
    private List<DeviceEntry> _snapshot = new();

    public IReadOnlyList<DeviceEntry> Snapshot
    {
        get { lock (_gate) return _snapshot.ToArray(); }
    }

    /// <summary>
    /// 合并规则:同名同传输类型视为同一台设备——电量可读者优先,其次已连接者,否则保留先到者。
    /// 排序:已连接在前,再按名称(ordinal)。
    /// </summary>
    public IReadOnlyList<DeviceEntry> ReplaceAll(IEnumerable<DeviceEntry> raw)
    {
        var merged = new Dictionary<(string Name, Transport Transport), DeviceEntry>();
        foreach (var entry in raw)
        {
            var key = (entry.Name, entry.Transport);
            if (!merged.TryGetValue(key, out var existing))
            {
                merged[key] = entry;
                continue;
            }
            merged[key] = Prefer(existing, entry);
        }

        var list = merged.Values
            .OrderByDescending(e => e.IsConnected)
            .ThenBy(e => e.Name, StringComparer.Ordinal)
            .ToList();
        lock (_gate) _snapshot = list;
        return list;
    }

    private static DeviceEntry Prefer(DeviceEntry a, DeviceEntry b)
    {
        if ((a.BatteryPercent is null) != (b.BatteryPercent is null))
            return a.BatteryPercent is not null ? a : b;
        if (a.IsConnected != b.IsConnected)
            return a.IsConnected ? a : b;
        return a;
    }
}
