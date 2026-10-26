using BatteryTray.Core;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace BatteryTray.Scanning;

/// <summary>
/// BLE 设备 GATT 电池服务实时读(0x180F → 0x2A19),Cached→Uncached 两段,
/// 参考 BTChargeTrayWatcher/同类实现。一次性读,V1 不订阅推送(轮询兜底)。
/// </summary>
internal static class GattBatteryReader
{
    public static async Task<byte?> ReadAsync(string aepId)
    {
        BluetoothLEDevice? device = null;
        GattDeviceService? service = null;
        try
        {
            device = await BluetoothLEDevice.FromIdAsync(aepId);
            if (device is null) return null;

            service = await FindServiceAsync(device, BluetoothCacheMode.Cached)
                   ?? await FindServiceAsync(device, BluetoothCacheMode.Uncached);
            if (service is null) return null;

            var characteristic = await FindCharacteristicAsync(service, BluetoothCacheMode.Cached)
                              ?? await FindCharacteristicAsync(service, BluetoothCacheMode.Uncached);
            if (characteristic is null) return null;

            var read = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
            if (read.Status != GattCommunicationStatus.Success
                || read.Value is not { Length: > 0 })
                return null;

            return GattRules.ParseBatteryLevel(DataReader.FromBuffer(read.Value).ReadByte());
        }
        catch (Exception ex)
        {
            Log.Warn("GATT 读取失败 " + aepId, ex);
            return null;
        }
        finally
        {
            service?.Dispose();
            device?.Dispose();
        }
    }

    private static async Task<GattDeviceService?> FindServiceAsync(BluetoothLEDevice device, BluetoothCacheMode mode)
    {
        try
        {
            var result = await device.GetGattServicesForUuidAsync(GattRules.BatteryServiceUuid, mode);
            return result.Status == GattCommunicationStatus.Success
                ? result.Services.FirstOrDefault()
                : null;
        }
        catch (Exception ex)
        {
            Log.Warn($"GATT 找服务失败 mode={mode}", ex);
            return null;
        }
    }

    private static async Task<GattCharacteristic?> FindCharacteristicAsync(GattDeviceService service, BluetoothCacheMode mode)
    {
        try
        {
            var result = await service.GetCharacteristicsForUuidAsync(GattRules.BatteryLevelUuid, mode);
            return result.Status == GattCommunicationStatus.Success
                ? result.Characteristics.FirstOrDefault()
                : null;
        }
        catch (Exception ex)
        {
            Log.Warn($"GATT 找特征失败 mode={mode}", ex);
            return null;
        }
    }
}
