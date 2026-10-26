namespace BatteryTray.Core;

/// <summary>应用设置(持久化由应用层做,这里只管值和校验)。</summary>
public sealed record AppSettings
{
    public int PollSeconds { get; init; } = 60;
    public byte LowThreshold { get; init; } = 20;
    public bool ToastEnabled { get; init; } = true;
    public bool StartWithWindows { get; init; } = false;
}

/// <summary>设置取值规则(纯函数,WSL 可测):越界收敛、坏值回默认。</summary>
public static class SettingsRules
{
    public const int MinPollSeconds = 10;
    public const int MaxPollSeconds = 600;
    public const byte MinThreshold = 5;
    public const byte MaxThreshold = 50;

    public static int ClampPollSeconds(int? value) =>
        value is null ? 60 : Math.Clamp(value.Value, MinPollSeconds, MaxPollSeconds);

    public static byte ClampThreshold(int? value)
    {
        if (value is null) return 20;
        if (value < MinThreshold) return MinThreshold;
        if (value > MaxThreshold) return MaxThreshold;
        return (byte)value;
    }
}
