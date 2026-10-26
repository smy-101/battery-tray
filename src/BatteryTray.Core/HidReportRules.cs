namespace BatteryTray.Core;

/// <summary>
/// HID 电池报告解析启发式(纯函数,WSL 可测)。
/// 通用电池页(0x85)没有统一报告布局;绝大多数键鼠/接收器是
/// [reportId, 百分比] 或 [reportId, 状态, 百分比] 的短报告。
/// 规则:跳过 reportId,在剩余字节里找 1–100 的候选,取最后一个
/// (常见布局:状态字节在前,百分比在后)。
/// ⚠️ 启发式:拿到真实设备 dump 后按实际布局收紧。
/// </summary>
public static class HidReportRules
{
    public static byte? GuessBatteryPercent(ReadOnlySpan<byte> report)
    {
        if (report.Length < 2) return null;
        byte? candidate = null;
        for (var i = 1; i < report.Length; i++)
            if (report[i] is >= 1 and <= 100)
                candidate = report[i];
        return candidate;
    }
}
