using BatteryTray.Core;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace BatteryTray.Scanning;

/// <summary>
/// 蓝牙设备枚举(经典 + LE):走 AssociationEndpoint 属性拿配对/连接/缓存电量。
/// 注意:枚举只能请求文档化属性名,乱请求会让 Windows 拒绝整个请求(参考实现踩过)。
/// </summary>
internal sealed class BluetoothScanner : IDisposable
{
    private const string PConnected = "System.Devices.Aep.IsConnected";
    private const string PPaired = "System.Devices.Aep.IsPaired";
    private const string PBattery = "System.Devices.BatteryLife";

    /// <summary>watcher 更新事件里可能出现的电量属性键(事件自带,不需要请求)。</summary>
    public static readonly string[] BatteryPropertyKeys =
    {
        "System.Devices.Aep.Bluetooth.Le.BatteryLevel",
        "System.Devices.Aep.BatteryLevel",
        "System.Devices.Aep.BatteryLifePercent",
        "System.Devices.BatteryLifePercent",
        "System.Devices.BatteryLevel",
        PBattery,
    };

    private static readonly string[] ConnectionProps = { PConnected, PPaired };
    private static readonly string[] BatteryProps = { PConnected, PPaired, PBattery };

    /// <summary>单台 BLE 设备 GATT 读取的兜底上限:防止蓝牙栈偶发挂起拖住整轮扫描。</summary>
    private const int GattTimeoutMs = 10_000;

    private readonly List<DeviceWatcher> _watchers = new();
    private bool _disposed;

    /// <summary>设备增删/属性变化。可能在任意线程触发,订阅方负责去抖。</summary>
    public event Action? Changed;

    /// <summary>诊断日志(--dump 挂接,正常运行为 null)。</summary>
    public static Action<string>? DebugLog { get; set; }

    private static (string Selector, Transport Transport)[] Selectors() => new[]
    {
        (BluetoothDevice.GetDeviceSelectorFromPairingState(true), Transport.BluetoothClassic),
        (BluetoothLEDevice.GetDeviceSelectorFromPairingState(true), Transport.BluetoothLe),
    };

    public async Task<List<DeviceEntry>> ScanAsync()
    {
        var entries = new List<DeviceEntry>();
        foreach (var (selector, transport) in Selectors())
        {
            DeviceInformationCollection devices;
            try
            {
                devices = await DeviceInformation.FindAllAsync(selector, BatteryProps, DeviceInformationKind.AssociationEndpoint);
            }
            catch (Exception ex)
            {
                Log.Warn("AEP 枚举降级(经典/LE: " + transport + ")", ex);
                // 属性集不被接受(老系统)→ 降级只请求连接属性
                try
                {
                    devices = await DeviceInformation.FindAllAsync(selector, ConnectionProps, DeviceInformationKind.AssociationEndpoint);
                }
                catch (Exception ex2)
                {
                    Log.Warn("AEP 枚举失败(跳过: " + transport + ")", ex2);
                    continue; // 单个 selector 失败不拖垮整体
                }
            }

            foreach (DeviceInformation d in devices)
            {
                bool connected = d.Properties.TryGetValue(PConnected, out var c) && c is true;
                entries.Add(new DeviceEntry
                {
                    Name = string.IsNullOrWhiteSpace(d.Name) ? "(未命名设备)" : d.Name,
                    Transport = transport,
                    IsConnected = connected,
                    BatteryPercent = ParseBattery(d.Properties, PBattery),
                    Source = "AepProperty",
                    DeviceId = d.Id,
                });
            }
        }

        await FillClassicHfpBatteryAsync(entries);
        await FillLeGattBatteryAsync(entries);
        entries.AddRange(await HidBatteryScanner.ScanAsync());
        entries.AddRange(await HidppBatteryScanner.ScanAsync());
        return entries;
    }

    /// <summary>
    /// AEP 属性没有电量的已连接 BLE 设备 → GATT 电池服务实时读。
    /// 各设备连接相互独立,并行读(串行会让慢设备延迟线性叠加)。
    /// </summary>
    private static async Task FillLeGattBatteryAsync(List<DeviceEntry> entries)
    {
        var pending = new List<Task<(int Index, byte? Level)>>();
        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.Transport != Transport.BluetoothLe || !e.IsConnected
                || e.BatteryPercent is not null || e.DeviceId is null)
                continue;
            pending.Add(ReadDeviceBatteryAsync(i, e.Name, e.DeviceId));
        }

        // 写回在 WhenAll 之后统一进行,不与并行读交叉
        foreach (var result in await Task.WhenAll(pending))
        {
            if (result.Level is not null)
                entries[result.Index] = entries[result.Index] with
                {
                    BatteryPercent = result.Level,
                    Source = "Gatt",
                };
        }

        async Task<(int Index, byte? Level)> ReadDeviceBatteryAsync(int index, string name, string deviceId)
        {
            var level = await WinRtTimeout.After(GattBatteryReader.ReadAsync(deviceId), GattTimeoutMs);
            DebugLog?.Invoke($"[GATT] {name} level={(level is byte b ? b : "<null>")}");
            return (index, level);
        }
    }

    /// <summary>
    /// AEP 属性没有电量的已连接经典蓝牙设备(典型:HFP 耳机),
    /// 经注册表 BTHENUM + CfgMgr32 私有属性补读。
    /// </summary>
    private static async Task FillClassicHfpBatteryAsync(List<DeviceEntry> entries)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.Transport != Transport.BluetoothClassic || !e.IsConnected
                || e.BatteryPercent is not null || e.DeviceId is null)
                continue;

            // 优先直接从 AEP id 解析地址(零开销);解析不到再开 BluetoothDevice 问
            var address = BluetoothAddressRules.TryParseFromAepId(e.DeviceId, out var parsed)
                ? parsed
                : await GetClassicAddressAsync(e.DeviceId);
            DebugLog?.Invoke($"[HFP] {e.Name} id={e.DeviceId} addr={address ?? "<null>"}");
            if (address is null) continue;

            var level = await Task.Run(() => HfpBatteryReader.Read(address));
            DebugLog?.Invoke($"[HFP] {e.Name} level={(level is byte b ? b : "<null>")}");
            if (level is not null)
                entries[i] = e with { BatteryPercent = level, Source = "HfpDevProp" };
        }
    }

    /// <summary>从 AEP id 打开 BluetoothDevice 拿地址;失败/超时返回 null 不影响其他设备。</summary>
    private static async Task<string?> GetClassicAddressAsync(string aepId)
    {
        try
        {
            var device = await BluetoothDevice.FromIdAsync(aepId);
            if (device is null) return null;
            try { return device.BluetoothAddress.ToString("X12"); }
            finally { device.Dispose(); }
        }
        catch (Exception ex)
        {
            Log.Warn("FromIdAsync 取地址失败 " + aepId, ex);
            return null;
        }
    }

    public void StartWatchers()
    {
        foreach (var (selector, _) in Selectors())
            foreach (var props in new[] { BatteryProps, ConnectionProps })
                if (TryStartWatcher(selector, props))
                    break; // 属性集能用就不再降级
    }

    private bool TryStartWatcher(string selector, string[] props)
    {
        DeviceWatcher? watcher = null;
        try
        {
            watcher = DeviceInformation.CreateWatcher(selector, props, DeviceInformationKind.AssociationEndpoint);
            watcher.Added += OnWatcherEvent;
            watcher.Updated += OnWatcherEvent;
            watcher.Removed += OnWatcherEvent;
            watcher.Start();
            _watchers.Add(watcher);
            return true;
        }
        catch
        {
            if (watcher != null)
            {
                watcher.Added -= OnWatcherEvent;
                watcher.Updated -= OnWatcherEvent;
                watcher.Removed -= OnWatcherEvent;
            }
            return false;
        }
    }

    private void OnWatcherEvent(DeviceWatcher sender, object args) => Changed?.Invoke();

    internal static byte? ParseBattery(IReadOnlyDictionary<string, object> properties, string key)
    {
        if (!properties.TryGetValue(key, out var value) || value is null)
            return null;
        try
        {
            // 属性值可能是 byte/ushort/uint/int;>100 视为脏数据
            var converted = Convert.ToUInt32(value, System.Globalization.CultureInfo.InvariantCulture);
            return converted <= 100 ? (byte)converted : null;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var w in _watchers)
        {
            try
            {
                if (w.Status == DeviceWatcherStatus.Started || w.Status == DeviceWatcherStatus.EnumerationCompleted)
                    w.Stop();
            }
            catch { /* 退出清理尽力而为 */ }
        }
        _watchers.Clear();
    }
}
