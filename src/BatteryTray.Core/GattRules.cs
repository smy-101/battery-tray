namespace BatteryTray.Core;

/// <summary>GATT 电池服务常量与解析(纯函数,WSL 可测)。</summary>
public static class GattRules
{
    /// <summary>Battery Service 0x180F。</summary>
    public static readonly Guid BatteryServiceUuid = new("0000180f-0000-1000-8000-00805f9b34fb");

    /// <summary>Battery Level 特征 0x2A19。</summary>
    public static readonly Guid BatteryLevelUuid = new("00002a19-0000-1000-8000-00805f9b34fb");

    /// <summary>0–100 合法;越界视为脏数据。</summary>
    public static byte? ParseBatteryLevel(byte raw) => raw <= 100 ? raw : null;
}
