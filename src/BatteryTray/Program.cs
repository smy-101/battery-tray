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

        ApplicationConfiguration.Initialize();
        using var app = new TrayApp();
        Application.Run();
        return 0;
    }

    private static int RunDump()
    {
        Console.WriteLine($"BatteryTray v{typeof(Program).Assembly.GetName().Version}");
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
