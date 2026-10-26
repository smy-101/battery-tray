using BatteryTray.Core;
using Xunit;

namespace BatteryTray.Tests;

public class LogRulesTests
{
    [Fact]
    public void 格式_含ISO时间_级别_消息()
    {
        var t = new DateTimeOffset(2026, 10, 26, 14, 30, 5, TimeSpan.FromHours(8));
        var line = LogRules.Format(t, "WARN", "boom");
        Assert.StartsWith("2026-10-26T14:30:05.0000000+08:00 WARN boom", line);
    }

    [Theory]
    [InlineData(256 * 1024, true)]   // 恰好到上限 → 轮转
    [InlineData(256 * 1024 + 1, true)]
    [InlineData(256 * 1024 - 1, false)]
    [InlineData(0, false)]
    public void 轮转判定(long size, bool expected)
    {
        Assert.Equal(expected, LogRules.ShouldRotate(size));
    }
}
