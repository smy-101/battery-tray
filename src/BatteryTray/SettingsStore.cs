using BatteryTray.Core;
using Microsoft.Win32;

namespace BatteryTray;

/// <summary>
/// 设置持久化:HKCU\Software\BatteryTray(值坏/缺一律回默认)。
/// 自启:HKCU\Software\Microsoft\Windows\CurrentVersion\Run。
/// </summary>
internal static class SettingsStore
{
    private const string Key = @"Software\BatteryTray";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "BatteryTray";

    public static AppSettings Load()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(Key);
            if (k is null) return new AppSettings();
            return new AppSettings
            {
                PollSeconds = SettingsRules.ClampPollSeconds(k.GetValue("PollSeconds") as int?),
                LowThreshold = SettingsRules.ClampThreshold(k.GetValue("LowThreshold") as int?),
                ToastEnabled = k.GetValue("ToastEnabled") as int? != 0,
                StartWithWindows = ReadAutoStart(),
            };
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings s)
    {
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(Key);
            k?.SetValue("PollSeconds", s.PollSeconds, RegistryValueKind.DWord);
            k?.SetValue("LowThreshold", s.LowThreshold, RegistryValueKind.DWord);
            k?.SetValue("ToastEnabled", s.ToastEnabled ? 1 : 0, RegistryValueKind.DWord);
        }
        catch
        {
            // 写不进注册表时保持内存设置可用,下次启动回默认
        }
        SetAutoStart(s.StartWithWindows);
    }

    public static bool ReadAutoStart()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey);
            return k?.GetValue(RunValue) is string path
                   && path.Contains("BatteryTray", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static void SetAutoStart(bool enable)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (k is null) return;
            if (enable)
                k.SetValue(RunValue, $"\"{Environment.ProcessPath}\"", RegistryValueKind.String);
            else
                k.DeleteValue(RunValue, throwOnMissingValue: false);
        }
        catch
        {
            // 权限/占用异常不致命:自启没写上,其余功能照常
        }
    }
}
