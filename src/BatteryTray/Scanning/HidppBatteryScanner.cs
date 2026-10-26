using BatteryTray.Core;
using Windows.Devices.Enumeration;
using Windows.Devices.HumanInterfaceDevice;
using Windows.Storage;
using Windows.Storage.Streams;

namespace BatteryTray.Scanning;

/// <summary>
/// 罗技 HID++ 读取(尽力而为,无硬件验证):
/// 厂商页 0xFF00(短/长报告)与 0xFF43(Bolt/Lightspeed 新式)接口,
/// 配对槽 1–5 逐个探测;HID++ 2.0 走 feature-set 枚举找电池 feature,
/// 1.0 回退 RAP 寄存器 0x07。所有交互经 DebugLog 输出,便于拿真机校准。
/// </summary>
internal static class HidppBatteryScanner
{
    /// <summary>接口级缓存:featureId → index(枚举一次后续轮询直接用)。</summary>
    private static readonly Dictionary<string, Dictionary<ushort, byte>> FeatureIndexCache = new();

    private static readonly (ushort Page, ushort Usage)[] VendorInterfaces =
    {
        (0xFF00, 0x01), // Unifying 短报告
        (0xFF00, 0x02), // Unifying 长报告
        (0xFF43, 0x2102), // Bolt/Lightspeed
    };

    public static async Task<List<DeviceEntry>> ScanAsync()
    {
        var result = new List<DeviceEntry>();
        foreach (var (page, usage) in VendorInterfaces)
        {
            DeviceInformationCollection interfaces;
            try
            {
                interfaces = await DeviceInformation.FindAllAsync(
                    HidDevice.GetDeviceSelector(page, usage));
            }
            catch (Exception ex)
            {
                Log.Warn($"HID++ 枚举失败 page={page:X4}", ex);
                continue;
            }

            foreach (DeviceInformation di in interfaces)
            {
                var name = di.Properties.TryGetValue("System.ItemNameDisplay", out var n) is true
                           && n is string s && !string.IsNullOrWhiteSpace(s)
                    ? s : "罗技接收器";
                try
                {
                    var hid = await HidDevice.FromIdAsync(di.Id, FileAccessMode.ReadWrite);
                    if (hid is null) continue;
                    using (hid)
                    {
                        for (byte slot = 1; slot <= HidppRules.MaxDeviceSlots; slot++)
                        {
                            var battery = await ReadSlotBatteryAsync(hid, di.Id, name, slot);
                            if (battery is not null)
                                result.Add(new DeviceEntry
                                {
                                    Name = $"{name} · 槽{slot}",
                                    Transport = Transport.UsbHid,
                                    IsConnected = true,
                                    BatteryPercent = battery,
                                    Source = "Hidpp",
                                    DeviceId = di.Id,
                                });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("HID++ 打开失败 " + di.Id, ex);
                }
            }
        }
        return result;
    }

    private static async Task<byte?> ReadSlotBatteryAsync(HidDevice hid, string interfaceId, string name, byte slot)
    {
        var sw = NextSwId();
        // 1) HID++ 2.0 探测:root fn1(参数 0)
        var root = await ExchangeAsync(hid,
            HidppRules.BuildFeatureRequest(slot, 0x00, 0x01, sw), sw);
        if (root is not null && HidppRules.Matches(root, slot, 0x00, sw) && !HidppRules.IsError(root, out _))
        {
            var proto = HidppRules.ParseRoot(root);
            BluetoothScanner.DebugLog?.Invoke($"[HID++] {name} 槽{slot} 协议 {proto?.Major}.{proto?.Minor}");

            // 2) 找电池 feature(统一版优先)
            var idx = await FindBatteryFeatureIndexAsync(hid, interfaceId, slot, HidppRules.FeatureBatteryUnified, sw)
                   ?? await FindBatteryFeatureIndexAsync(hid, interfaceId, slot, HidppRules.FeatureBatteryLegacy, sw);
            if (idx is null) return null;

            // 3) 读电量:0x1004 用 fn1(GetStatus),0x1000 用 fn0
            var isUnified = true;
            sw = NextSwId();
            var resp = await ExchangeAsync(hid,
                HidppRules.BuildFeatureRequest(slot, idx.Value, 0x01, sw), sw);
            if (resp is null || !HidppRules.Matches(resp, slot, idx.Value, sw)
                || HidppRules.IsError(resp, out _) || HidppRules.ParseUnifiedBattery(resp) is null)
            {
                isUnified = false;
                sw = NextSwId();
                resp = await ExchangeAsync(hid,
                    HidppRules.BuildFeatureRequest(slot, idx.Value, 0x00, sw), sw);
            }

            if (resp is null || !HidppRules.Matches(resp, slot, idx.Value, sw) || HidppRules.IsError(resp, out _))
                return null;
            var level = isUnified ? HidppRules.ParseUnifiedBattery(resp) : HidppRules.ParseLegacyBattery(resp);
            BluetoothScanner.DebugLog?.Invoke($"[HID++] {name} 槽{slot} 电量={level}");
            return level;
        }

        // 4) HID++ 1.0 回退:RAP 寄存器 0x07(电池)
        sw = NextSwId();
        var rap = await ExchangeAsync(hid, HidppRules.BuildRegisterRead(slot, 0x07, sw), sw);
        if (rap is not null && !HidppRules.IsError(rap, out _))
        {
            var level = HidppRules.ParseRegisterBattery(rap);
            BluetoothScanner.DebugLog?.Invoke($"[HID++ RAP] {name} 槽{slot} resp={Convert.ToHexString(rap)} 电量={level}");
            return level;
        }
        return null;
    }

    /// <summary>FeatureSet 枚举找 featureIndex(接口级缓存)。</summary>
    private static async Task<byte?> FindBatteryFeatureIndexAsync(
        HidDevice hid, string interfaceId, byte slot, ushort wantedFeatureId, byte sw)
    {
        var cache = FeatureIndexCache.GetValueOrDefault(interfaceId);
        if (cache is not null && cache.TryGetValue(wantedFeatureId, out var cached))
            return cached;

        for (byte featureNum = 1; featureNum < 32; featureNum++)
        {
            sw = NextSwId();
            var resp = await ExchangeAsync(hid,
                HidppRules.BuildFeatureRequest(slot, 0x01, 0x01, sw,
                    (byte)(featureNum & 0xFF), (byte)(featureNum >> 8)), sw);
            if (resp is null || !HidppRules.Matches(resp, slot, 0x01, sw)
                || HidppRules.IsError(resp, out _))
                break; // 越界错误 = 枚举结束
            var e = HidppRules.ParseFeatureEnum(resp);
            if (e is not { } entry) continue;
            if (entry.FeatureId == wantedFeatureId)
            {
                FeatureIndexCache.TryAdd(interfaceId, new Dictionary<ushort, byte>());
                FeatureIndexCache[interfaceId][wantedFeatureId] = entry.Index;
                return entry.Index;
            }
        }
        return null;
    }

    /// <summary>一次 HID++ 事务:发 feature 请求 → 读同报告号响应 → 校验帧。</summary>
    private static async Task<byte[]?> ExchangeAsync(HidDevice hid, byte[] request, byte swId)
    {
        try
        {
            var send = hid.CreateFeatureReport(request[0]);
            var writer = new DataWriter();
            writer.WriteBytes(request[1..]);
            send.Data = writer.DetachBuffer();
            var sent = await WithTimeoutValue(hid.SendFeatureReportAsync(send).AsTask());
            if (sent == 0) return null;

            // 异步事件可能插队:最多读 3 次找匹配帧
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var resp = await WithTimeout(hid.GetFeatureReportAsync(request[0]).AsTask());
                if (resp is null) return null;
                var reader = DataReader.FromBuffer(resp.Data);
                var bytes = new byte[resp.Data.Length];
                reader.ReadBytes(bytes);
                if (bytes.Length >= 4 && bytes[0] == request[0] && bytes[1] == request[1])
                    return bytes; // 帧级匹配;featureIdx/swId 由调用方校验
            }
            return null;
        }
        catch (Exception ex)
        {
            Log.Warn("HID++ 事务失败 " + Convert.ToHexString(request), ex);
            return null;
        }
    }

    private static byte _swId;

    private static byte NextSwId()
    {
        _swId = (byte)(_swId % 15 + 1); // 1–15 循环,避开 0(0=设备主动事件)
        return _swId;
    }

    private static async Task<T?> WithTimeout<T>(Task<T> task, int ms = 1500) where T : class
    {
        var done = await Task.WhenAny(task, Task.Delay(ms));
        if (done != task) return null;
        try { return await task; }
        catch { return null; }
    }

    private static async Task<T> WithTimeoutValue<T>(Task<T> task, int ms = 1500)
    {
        var done = await Task.WhenAny(task, Task.Delay(ms));
        if (done != task) return default!;
        try { return await task; }
        catch { return default!; }
    }
}
