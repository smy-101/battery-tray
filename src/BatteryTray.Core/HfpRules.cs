namespace BatteryTray.Core;

/// <summary>
/// HFP 耳机电量在注册表 BTHENUM 下的定位规则(纯函数,WSL 可测)。
/// 结构:HKLM\SYSTEM\CurrentControlSet\Enum\BTHENUM\{服务GUID子键}\{实例名}
/// 其中 HFP 服务的 GUID 是 {0000111e-…}(含 "111e"),实例名内含 12 位无冒号蓝牙地址。
/// </summary>
public static class HfpRules
{
    /// <summary>HFP(HF)服务子键形如 {0000111E-0000-1000-8000-00805F9B34FB}_VID&amp;…,判据:含 "111e"。</summary>
    public static bool IsHfpServiceKey(string subKeyName) =>
        subKeyName.Contains("111e", StringComparison.OrdinalIgnoreCase);

    /// <summary>实例名(如 7&amp;1bd0e2d8&amp;0&amp;0025764F8E3F_C00000000)内含目标地址即匹配。</summary>
    public static bool IsAddressMatch(string instanceName, string bluetoothAddressHex) =>
        !string.IsNullOrEmpty(bluetoothAddressHex)
        && instanceName.Contains(bluetoothAddressHex, StringComparison.OrdinalIgnoreCase);

    /// <summary>拼出给 CfgMgr32 用的完整实例 id。</summary>
    public static string InstanceId(string serviceSubKey, string instanceName) =>
        $"BTHENUM\\{serviceSubKey}\\{instanceName}";
}
