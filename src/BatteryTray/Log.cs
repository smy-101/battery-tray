using BatteryTray.Core;

namespace BatteryTray;

/// <summary>
/// 文件日志:%LOCALAPPDATA%\BatteryTray\log.txt,超 256KB 轮转为 .old。
/// 日志自身永不抛异常(连日志都写不进时保持沉默,不影响主功能)。
/// </summary>
internal static class Log
{
    private static readonly object Gate = new();
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BatteryTray");
    private static readonly string File = Path.Combine(Dir, "log.txt");

    public static void Warn(string operation, Exception ex) =>
        Write("WARN", $"{operation}: {ex.GetType().Name} (0x{ex.HResult:X8}) {ex.Message}");

    public static void Info(string message) => Write("INFO", message);

    private static void Write(string level, string message)
    {
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                if (System.IO.File.Exists(File))
                {
                    var info = new FileInfo(File);
                    if (LogRules.ShouldRotate(info.Length))
                        System.IO.File.Move(File, File + ".old", overwrite: true);
                }
                System.IO.File.AppendAllText(File, LogRules.Format(DateTimeOffset.Now, level, message) + "\n");
            }
            catch
            {
                // 日志失败不影响主功能
            }
        }
    }
}
