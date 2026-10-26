using System.Text.RegularExpressions;

namespace BatteryTray.Core;

/// <summary>
/// 从 Windows AEP 设备 id 中解析蓝牙地址(纯函数,WSL 可测)。
/// 经典蓝牙 AEP id 形如 "Bluetooth#BluetoothL#C0:E5:79:27:CE:76#-1";
/// 统一输出 12 位大写无冒号十六进制(注册表实例名用的格式)。
/// </summary>
public static partial class BluetoothAddressRules
{
    [GeneratedRegex(@"(?:^|[#_:])([0-9A-F]{2}(?::[0-9A-F]{2}){5})(?:$|[#_])", RegexOptions.IgnoreCase)]
    private static partial Regex ColonMacRegex();

    [GeneratedRegex(@"(?:^|[#_])([0-9A-F]{12})(?:$|[#_])", RegexOptions.IgnoreCase)]
    private static partial Regex BareMacRegex();

    public static bool TryParseFromAepId(string? aepId, out string hex12)
    {
        hex12 = string.Empty;
        if (string.IsNullOrEmpty(aepId)) return false;

        var m = ColonMacRegex().Match(aepId);
        if (m.Success)
        {
            hex12 = m.Groups[1].Value.Replace(":", "").ToUpperInvariant();
            return true;
        }

        m = BareMacRegex().Match(aepId);
        if (m.Success)
        {
            hex12 = m.Groups[1].Value.ToUpperInvariant();
            return true;
        }
        return false;
    }
}
