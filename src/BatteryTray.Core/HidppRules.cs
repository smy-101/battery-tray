namespace BatteryTray.Core;

/// <summary>
/// 罗技 HID++ 协议纯逻辑(请求构建/响应校验/电量解析),WSL 可测。
/// 依据:Solaar 实现、HID++ 公开规范(LEKENSTEYN PDF)、openlogi/hidpp。
/// 帧:短报告 7B / 长报告 20B;[reportId, devIdx, featureIdx, fn&lt;&lt;4|swId, params…]
/// </summary>
public static class HidppRules
{
    public const byte ReportShort = 0x10;
    public const byte ReportLong = 0x11;
    public const byte MaxDeviceSlots = 5; // 接收器配对槽 0x01–0x05
    public const ushort FeatureRoot = 0x0000;
    public const ushort FeatureSet = 0x0001;
    public const ushort FeatureBatteryUnified = 0x1004;
    public const ushort FeatureBatteryLegacy = 0x1000;

    /// <summary>fn 编号 + swId → byte3。</summary>
    public static byte FuncSw(byte function, byte swId) => (byte)((function << 4) | (swId & 0x0F));

    /// <summary>HID++ 2.0 feature 请求(默认短报告)。</summary>
    public static byte[] BuildFeatureRequest(byte deviceIndex, byte featureIndex,
        byte function, byte swId, byte p0 = 0, byte p1 = 0, byte p2 = 0, bool longReport = false)
    {
        var req = new byte[longReport ? 20 : 7];
        req[0] = longReport ? ReportLong : ReportShort;
        req[1] = deviceIndex;
        req[2] = featureIndex;
        req[3] = FuncSw(function, swId);
        req[4] = p0;
        req[5] = p1;
        req[6] = p2;
        return req;
    }

    /// <summary>HID++ 1.0 RAP 读寄存器(短报告)。</summary>
    public static byte[] BuildRegisterRead(byte deviceIndex, byte register, byte swId)
    {
        var req = new byte[7];
        req[0] = ReportShort;
        req[1] = deviceIndex;
        req[2] = FuncSw(0x08, swId); // 0x08 = RAP GetRegister
        req[3] = register;
        return req;
    }

    /// <summary>响应过短即坏。</summary>
    public static bool IsError(byte[] resp, out byte errorCode)
    {
        errorCode = 0;
        if (resp.Length < 5) return true;
        if ((resp[3] & 0xF0) == 0x80)
        {
            errorCode = resp[4];
            return true;
        }
        return false;
    }

    /// <summary>响应与请求对上(设备/feature 一致,swId 回显)。</summary>
    public static bool Matches(byte[] resp, byte deviceIndex, byte featureIndex, byte swId) =>
        resp.Length >= 4
        && resp[1] == deviceIndex
        && resp[2] == featureIndex
        && (resp[3] & 0x0F) == swId;

    /// <summary>root fn1(参数 0)响应 → [featureCount, major, minor]。</summary>
    public static (byte FeatureCount, byte Major, byte Minor)? ParseRoot(byte[] resp) =>
        resp.Length >= 7 ? (resp[4], resp[5], resp[6]) : null;

    /// <summary>FeatureSet fn1 响应 → 枚举到的 featureId 与其在设备上的 index。</summary>
    public static (ushort FeatureId, byte Index)? ParseFeatureEnum(byte[] resp) =>
        resp.Length >= 6
            ? ((ushort)(resp[5] << 8 | resp[4]), resp[6])
            : null;

    /// <summary>0x1004 unified battery fn1 → param0 = stateOfCharge(%)。</summary>
    public static byte? ParseUnifiedBattery(byte[] resp) =>
        resp.Length >= 5 && resp[4] <= 100 ? resp[4] : null;

    /// <summary>0x1000 legacy battery fn0 → param0 = level(%)。</summary>
    public static byte? ParseLegacyBattery(byte[] resp) =>
        resp.Length >= 5 && resp[4] <= 100 ? resp[4] : null;

    /// <summary>RAP 寄存器 0x07(电池)→ resp[4] = level(%)。</summary>
    public static byte? ParseRegisterBattery(byte[] resp) =>
        resp.Length >= 5 && resp[4] <= 100 ? resp[4] : null;
}
