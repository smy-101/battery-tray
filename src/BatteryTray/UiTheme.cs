using System.Drawing;
using System.Runtime.InteropServices;
using BatteryTray.Core;

namespace BatteryTray;

/// <summary>
/// 深浅色主题(跟随系统,可强制覆盖)+ DWM 窗口效果。
/// 主题来自 HKCU AppsUseLightTheme;读取失败一律回退浅色。
/// </summary>
internal static class UiTheme
{
    public sealed record Palette(
        Color Back,
        Color Text,
        Color Muted,
        Color Border,
        Color Track,
        Color Hover,
        Color ButtonBack,
        Color ButtonBorder,
        Color ButtonText);

    public static readonly Palette Light = new(
        Back: Color.White,
        Text: Color.FromArgb(32, 32, 32),
        Muted: Color.FromArgb(140, 140, 140),
        Border: Color.FromArgb(228, 228, 228),
        Track: Color.FromArgb(233, 233, 233),
        Hover: Color.FromArgb(240, 240, 240),
        ButtonBack: Color.FromArgb(243, 243, 243),
        ButtonBorder: Color.FromArgb(200, 200, 200),
        ButtonText: Color.FromArgb(32, 32, 32));

    public static readonly Palette Dark = new(
        Back: Color.FromArgb(32, 32, 32),
        Text: Color.FromArgb(235, 235, 235),
        Muted: Color.FromArgb(148, 148, 148),
        Border: Color.FromArgb(56, 56, 56),
        Track: Color.FromArgb(64, 64, 64),
        Hover: Color.FromArgb(43, 43, 43),
        ButtonBack: Color.FromArgb(51, 51, 51),
        ButtonBorder: Color.FromArgb(80, 80, 80),
        ButtonText: Color.FromArgb(235, 235, 235));

    private static bool? _forced;

    /// <summary>预览/测试用:强制主题;null = 跟随系统。</summary>
    public static void SetOverride(bool? dark) => _forced = dark;

    public static bool IsSystemDark()
    {
        if (_forced is { } forced) return forced;
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch
        {
            return false;
        }
    }

    public static Palette Current() => IsSystemDark() ? Dark : Light;

    /// <summary>档位 → 品牌色,图标/电量条/状态点共用。</summary>
    public static Color BandColor(BatteryBand band) => band switch
    {
        BatteryBand.Good => Color.FromArgb(46, 204, 113),
        BatteryBand.Warn => Color.FromArgb(241, 196, 15),
        BatteryBand.Low => Color.FromArgb(231, 76, 60),
        _ => Color.FromArgb(148, 148, 148),
    };

    // ---- DWM 窗口效果(Win10 2004 / Win11;不支持时静默跳过) ----

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    /// <summary>标题栏/边框用系统深色。</summary>
    public static void TryDarkTitleBar(Control control) =>
        ApplyWhenHandleCreated(control, hwnd =>
        {
            int on = 1;
            _ = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
        });

    /// <summary>Win11 圆角;Win10 上调用失败保持直角。</summary>
    public static void TryRoundCorners(Control control) =>
        ApplyWhenHandleCreated(control, hwnd =>
        {
            int round = DWMWCP_ROUND;
            _ = DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
        });

    private static void ApplyWhenHandleCreated(Control control, Action<nint> apply)
    {
        void Run(Control c)
        {
            try { apply(c.Handle); }
            catch { /* dwmapi 不可用时忽略 */ }
        }

        if (control.IsHandleCreated)
        {
            Run(control);
            return;
        }

        control.HandleCreated += (sender, _) =>
        {
            if (sender is Control c) Run(c);
        };
    }
}
