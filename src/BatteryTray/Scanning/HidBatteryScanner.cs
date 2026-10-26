using BatteryTray.Core;
using Windows.Devices.Enumeration;
using Windows.Devices.HumanInterfaceDevice;
using Windows.Storage;
using Windows.Storage.Streams;

namespace BatteryTray.Scanning;

/// <summary>
/// 通用 HID 电池页(Usage Page 0x85)枚举与读取:
/// 覆盖经典蓝牙键鼠(Windows 把 BT-HID 电量也挂在 0x85 集合上)和
/// 直接暴露该页的 2.4G 接收器。接口存在即视为在线(HID 无轻量连接查询)。
/// </summary>
internal static class HidBatteryScanner
{
    private const string Selector = "System.DeviceInterface.Hid.UsagePage:=133"; // 0x85

    public static async Task<List<DeviceEntry>> ScanAsync()
    {
        var result = new List<DeviceEntry>();
        DeviceInformationCollection interfaces;
        try
        {
            interfaces = await DeviceInformation.FindAllAsync(Selector);
        }
        catch (Exception ex)
        {
            Log.Warn("HID 0x85 枚举失败", ex);
            return result;
        }

        foreach (DeviceInformation di in interfaces)
        {
            var name = di.Properties.TryGetValue("System.ItemNameDisplay", out var n) is true
                       && n is string s && !string.IsNullOrWhiteSpace(s)
                ? s : "(HID 电池设备)";
            byte? battery = null;
            try
            {
                var hid = await HidDevice.FromIdAsync(di.Id, FileAccessMode.Read);
                if (hid is null) continue; // 被系统占用(键鼠页被独占)等
                using (hid)
                {
                    var report = await hid.GetInputReportAsync();
                    if (report?.Data is { Length: > 0 } buffer)
                    {
                        var bytes = new byte[buffer.Length];
                        DataReader.FromBuffer(buffer).ReadBytes(bytes);
                        BluetoothScanner.DebugLog?.Invoke(
                            $"[HID] {name} report={Convert.ToHexString(bytes)}");
                        battery = HidReportRules.GuessBatteryPercent(bytes);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("HID 读取失败 " + di.Id, ex);
            }

            result.Add(new DeviceEntry
            {
                Name = name,
                Transport = Transport.UsbHid,
                IsConnected = true, // 接口存在即在线;无线端是否连着需要厂商协议(V2)
                BatteryPercent = battery,
                Source = battery is null ? null : "HidBattery",
                DeviceId = di.Id,
            });
        }
        return result;
    }
}
