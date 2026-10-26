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
    /// 合并规则:
    /// 1) 同名同传输 → 同一设备的多个来源(AEP/GATT/HFP);
    /// 2) 同名跨传输 → 同一台物理设备的两条枚举路径
    ///    (双模耳机同时命中经典/LE 两个 AEP;蓝牙键鼠同时命中 AEP 与 HID 0x85),
    ///    罗技接收器设备名带“· 槽N”后缀不受影响。
    /// 电量可读者优先,其次已连接者,否则保留先到者。
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

        var byName = new Dictionary<string, DeviceEntry>();
        foreach (var entry in merged.Values)
        {
            if (!byName.TryGetValue(entry.Name, out var existing))
            {
                byName[entry.Name] = entry;
                continue;
            }
            byName[entry.Name] = Prefer(existing, entry);
        }

        var list = byName.Values
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
