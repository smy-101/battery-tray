using BatteryTray.Core;
using Xunit;

namespace BatteryTray.Tests;

public class SettingsRulesTests
{
    [Theory]
    [InlineData(null, 60)]
    [InlineData(60, 60)]
    [InlineData(5, 10)]      // 低于下限收敛到 10
    [InlineData(5_000, 600)] // 高于上限收敛到 600
    [InlineData(120, 120)]
    public void 轮询间隔_收敛(int? input, int expected)
    {
        Assert.Equal(expected, SettingsRules.ClampPollSeconds(input));
    }

    [Theory]
    [InlineData(null, 20)]
    [InlineData(20, 20)]
    [InlineData(1, 5)]    // 阈值下限 5(不然永远在报警)
    [InlineData(99, 50)]  // 阈值上限 50
    [InlineData(30, 30)]
    public void 低电量阈值_收敛(int? input, byte expected)
    {
        Assert.Equal(expected, SettingsRules.ClampThreshold(input));
    }

    [Fact]
    public void 默认值_合理()
    {
        var s = new AppSettings();
        Assert.Equal(60, s.PollSeconds);
        Assert.Equal((byte)20, s.LowThreshold);
        Assert.True(s.ToastEnabled);
        Assert.False(s.StartWithWindows);
    }
}
