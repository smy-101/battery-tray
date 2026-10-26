using BatteryTray.Core;
using Xunit;

namespace BatteryTray.Tests;

public class HidReportRulesTests
{
    [Theory]
    [InlineData(new byte[] { 0x01, 85 }, 85)]                  // [id, 百分比]
    [InlineData(new byte[] { 0x01, 0x00, 90 }, 90)]            // [id, 状态, 百分比]
    [InlineData(new byte[] { 0x05, 0x02, 0x01, 40 }, 40)]      // 多个候选取最后
    public void 常见布局_解析(byte[] report, byte expected)
    {
        Assert.Equal(expected, HidReportRules.GuessBatteryPercent(report));
    }

    [Theory]
    [InlineData(new byte[] { 0x01 })]                          // 只有 reportId
    [InlineData(new byte[] { 0x01, 0x00 })]                    // 全零(未按任何状态)
    [InlineData(new byte[] { 0x01, 0xFF, 0x80 })]              // 候选都 >100
    public void 无合法候选_返回空(byte[] report)
    {
        Assert.Null(HidReportRules.GuessBatteryPercent(report));
    }

    [Fact]
    public void 百分比为零_视为无读数()
    {
        // 0 电量几乎总是"报告未填充"的哨兵值,不展示 0%(避免误报)
        Assert.Null(HidReportRules.GuessBatteryPercent(new byte[] { 0x01, 0 }));
    }
}
