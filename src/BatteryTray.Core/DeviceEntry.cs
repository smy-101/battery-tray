namespace BatteryTray.Core;

public enum Transport
{
    BluetoothClassic,
    BluetoothLe,
    UsbHid,
}

/// <summary>UI 展示单元:一台外设的名称、连接类型与电量(见 GLOSSARY.md)。</summary>
public sealed record DeviceEntry
{
    public required string Name { get; init; }
    public required Transport Transport { get; init; }
    public bool IsConnected { get; init; }

    /// <summary>电量百分比;null = 读取不到(来源不支持或设备断连)。</summary>
    public byte? BatteryPercent { get; init; }

    /// <summary>电量从哪条路径读到(AepProperty / HfpDevProp / Gatt / HidBattery),调试用。</summary>
    public string? Source { get; init; }

    /// <summary>原始设备接口/AEP id,调试与去重用。</summary>
    public string? DeviceId { get; init; }

    public string TransportLabel => Transport switch
    {
        Transport.BluetoothClassic => "蓝牙",
        Transport.BluetoothLe => "BLE",
        Transport.UsbHid => "USB/2.4G",
        _ => "?",
    };
}
