using System.Runtime.InteropServices;
using BatteryTray.Core;
using BatteryTray.Scanning;

namespace BatteryTray;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--dump"))
        {
            AttachParentConsole();
            return RunDump();
        }
        if (args.Contains("--ui-preview"))
        {
            return RunUiPreview(args);
        }

        // 单实例:已有托盘实例时安静退出(dump 不受限)
        using var mutex = new Mutex(true, @"Local\BatteryTray-SingleInstance", out bool isFirst);
        if (!isFirst) return 0;

        ApplicationConfiguration.Initialize();
        using var app = new TrayApp();
        Application.Run();
        return 0;
    }

    private static int RunDump()
    {
        Console.WriteLine($"BatteryTray v{typeof(Program).Assembly.GetName().Version}");
        BluetoothScanner.DebugLog = m => Console.WriteLine(m);
        using var scanner = new BluetoothScanner();
        // WinRT 异步在线程池(MTA)上完成,避免 STA 阻塞
        var scan = Task.Run(() => scanner.ScanAsync());
        if (!scan.Wait(TimeSpan.FromSeconds(15)))
        {
            Console.WriteLine("扫描超时(15s)");
            return 2;
        }

        var list = new DeviceCatalog().ReplaceAll(scan.Result);
        Console.WriteLine();
        Console.WriteLine($"  {"设备",-30} {"类型",-10} {"连接",-6} {"电量",-5} 来源");
        Console.WriteLine($"  {new string('-', 70)}");
        foreach (var d in list)
        {
            string battery = d.BatteryPercent is byte b ? $"{b,3}%" : "  —";
            Console.WriteLine($"  {Trim(d.Name, 30),-30} {d.TransportLabel,-10} {(d.IsConnected ? "是" : "否"),-6} {battery,-5} {d.Source ?? ""}");
        }
        if (list.Count == 0) Console.WriteLine("  (未发现已配对蓝牙设备)");
        Console.WriteLine($"\n  共 {list.Count} 台(含已断连的已配对设备)");
        return 0;
    }

    /// <summary>
    /// UI 预览:示例设备渲染设备面板与设置对话框,DrawToBitmap 存 PNG。
    /// 用法:BatteryTray.exe --ui-preview &lt;输出目录&gt; [dark|light],主题缺省跟随系统。
    /// 与 --dump 一样不受单实例限制,供无硬件环境验证界面。
    /// </summary>
    private static int RunUiPreview(string[] args)
    {
        int i = Array.IndexOf(args, "--ui-preview");
        string outDir = args.Length > i + 1 && !args[i + 1].StartsWith("--") ? args[i + 1] : ".";
        if (args.Length > i + 2 && args[i + 2] is "dark" or "light")
            UiTheme.SetOverride(args[i + 2] == "dark");

        ApplicationConfiguration.Initialize();
        Directory.CreateDirectory(outDir);

        // 绘制期异常写到文件而不是弹窗阻塞(弹窗会让 -Wait 挂住)
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
        {
            try
            {
                File.WriteAllText(Path.Combine(outDir, "ui-error.txt"),
                    $"{(ex.ExceptionObject as Exception)?.ToString() ?? ex.ExceptionObject?.ToString() ?? "?"}");
            }
            catch { }
            Environment.Exit(1);
        };

        var entries = new DeviceEntry[]
        {
            new() { Name = "Keychron K3 Pro", Transport = Transport.BluetoothLe, IsConnected = true, BatteryPercent = 85, Source = "Gatt" },
            new() { Name = "HUAWEI FreeClip", Transport = Transport.BluetoothClassic, IsConnected = true, BatteryPercent = 100, Source = "Hfp" },
            new() { Name = "MX Master 3S", Transport = Transport.UsbHid, IsConnected = true, BatteryPercent = 15, Source = "Hidpp" },
            new() { Name = "Surface 蓝牙鼠标", Transport = Transport.BluetoothClassic, IsConnected = true, BatteryPercent = null },
            new() { Name = "AirPods Pro", Transport = Transport.BluetoothClassic, IsConnected = false, BatteryPercent = null },
        };

        using var panel = new DevicePanel { PreviewMode = true };
        panel.ShowNear(new Point(160, 160), entries);

        using var dialog = new SettingsDialog(new AppSettings());
        dialog.StartPosition = FormStartPosition.Manual;
        dialog.Location = new Point(panel.Location.X + panel.Width + 32, 160);
        dialog.Show();

        Application.DoEvents();
        Thread.Sleep(500);
        Application.DoEvents();

        SaveShot(panel, Path.Combine(outDir, "ui-panel.png"));
        SaveShot(dialog, Path.Combine(outDir, "ui-settings.png"));
        File.WriteAllText(Path.Combine(outDir, "ui-theme.txt"), UiTheme.IsSystemDark() ? "dark" : "light");

        dialog.Close();
        panel.Close();
        // 无消息循环时关闭窗口不等价于退出(句柄/线程残留),预览工具直接确定性退出
        Environment.Exit(0);
        return 0;
    }

    /// <summary>ListView 自绘在 DrawToBitmap(WM_PRINT)下不触发,改截屏拿真实像素(含 DWM 圆角/投影)。</summary>
    private static void SaveShot(Form form, string path)
    {
        var bounds = form.Bounds;
        using var bmp = new Bitmap(bounds.Width, bounds.Height);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bmp.Size);
        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static string Trim(string s, int width)
    {
        if (s.Length <= width) return s;
        return s[..(width - 1)] + "…";
    }

    /// <summary>WinExe 不继承控制台;挂到父进程控制台让 --dump 输出可见。</summary>
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);

    private static void AttachParentConsole()
    {
        const int ATTACH_PARENT_PROCESS = -1;
        AttachConsole(ATTACH_PARENT_PROCESS);
    }
}
