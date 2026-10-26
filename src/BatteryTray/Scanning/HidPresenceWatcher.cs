using Windows.Devices.Enumeration;

namespace BatteryTray.Scanning;

/// <summary>
/// HID 设备接口插拔监视:2.4G/USB 接收器上下线即时触发刷新
/// (蓝牙设备走 BluetoothScanner 的 AEP watcher,HID 没有 AEP 事件)。
/// 只订阅 Added/Removed——插拔才是要即时响应的;接口属性 Updated 很频繁,
/// 交给轮询兜底,避免去抖被持续重置成高频扫描。
/// </summary>
internal sealed class HidPresenceWatcher : IDisposable
{
    /// <summary>HID 设备接口类 GUID:所有 HID 接口(含 0x85 电池页与厂商页)的插拔都会命中。</summary>
    private const string Selector =
        "System.DeviceInterface.ClassGuid:={4d1e55b2-f16f-11cf-88cb-001111000030}";

    private DeviceWatcher? _watcher;
    private bool _enumerated;
    private bool _disposed;

    /// <summary>设备插拔。可能在任意线程触发,订阅方负责去抖。</summary>
    public event Action? Changed;

    public void Start()
    {
        try
        {
            // 单参重载默认 DeviceInformationKind.DeviceInterface,正是接口插拔事件
            _watcher = DeviceInformation.CreateWatcher(Selector);
            // 启动时对既有设备有一轮 Added 风暴,那是初始状态不是变化:
            // 枚举完成前忽略,避免每次启动都多一次冗余扫描
            _watcher.EnumerationCompleted += (_, _) => _enumerated = true;
            _watcher.Added += OnChange;
            _watcher.Removed += OnChange;
            _watcher.Start();
        }
        catch (Exception ex)
        {
            Log.Warn("HID 插拔监视启动失败(回退轮询)", ex);
        }
    }

    private void OnChange(DeviceWatcher sender, object args)
    {
        if (_enumerated)
            Changed?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (_watcher is { Status: DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted })
                _watcher.Stop();
        }
        catch { /* 退出清理尽力而为 */ }
    }
}
