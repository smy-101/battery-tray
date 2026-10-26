using BatteryTray.Core;
using Xunit;

namespace BatteryTray.Tests;

public class GattRulesTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void 合法电量(byte raw)
    {
        Assert.Equal(raw, GattRules.ParseBatteryLevel(raw));
    }

    [Theory]
    [InlineData(101)]
    [InlineData(255)]
    public void 越界为脏数据(byte raw)
    {
        Assert.Null(GattRules.ParseBatteryLevel(raw));
    }

    [Fact]
    public void UUID_常量与规范一致()
    {
        Assert.Equal("0000180f-0000-1000-8000-00805f9b34fb", GattRules.BatteryServiceUuid.ToString());
        Assert.Equal("00002a19-0000-1000-8000-00805f9b34fb", GattRules.BatteryLevelUuid.ToString());
    }
}
