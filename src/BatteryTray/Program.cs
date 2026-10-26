using System.Runtime.InteropServices;

namespace BatteryTray;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--dump"))
        {
            AttachParentConsole();
            Console.WriteLine($"BatteryTray v{typeof(Program).Assembly.GetName().Version}");
            Console.WriteLine("设备读取尚未实现(骨架阶段)");
            return 0;
        }

        ApplicationConfiguration.Initialize();
        using var app = new TrayApp();
        Application.Run();
        return 0;
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
