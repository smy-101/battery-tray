using BatteryTray.Core;
using Xunit;

namespace BatteryTray.Tests;

public class TrayRulesTests
{
    private static DeviceEntry Dev(string name, byte? battery, bool connected = true,
        Transport t = Transport.BluetoothClassic) =>
        new() { Name = name, Transport = t, IsConnected = connected, BatteryPercent = battery };

    [Fact]
    public void 图标电量_取已连接可读设备的最低值()
    {
        var entries = new[] { Dev("A", 80), Dev("B", 35), Dev("C", 60) };
        Assert.Equal((byte?)35, TrayRules.MinBatteryPercent(entries));
    }

    [Fact]
    public void 图标电量_断连设备不参与()
    {
        var entries = new[] { Dev("A", 80), Dev("B", 10, connected: false) };
        Assert.Equal((byte?)80, TrayRules.MinBatteryPercent(entries));
    }

    [Fact]
    public void 图标电量_无可读设备_为空()
    {
        Assert.Null(TrayRules.MinBatteryPercent(new[] { Dev("A", null) }));
        Assert.Null(TrayRules.MinBatteryPercent(Array.Empty<DeviceEntry>()));
    }

    [Theory]
    [InlineData(null, BatteryBand.None)]
    [InlineData((byte)0, BatteryBand.Low)]
    [InlineData((byte)20, BatteryBand.Low)]
    [InlineData((byte)21, BatteryBand.Warn)]
    [InlineData((byte)40, BatteryBand.Warn)]
    [InlineData((byte)41, BatteryBand.Good)]
    [InlineData((byte)100, BatteryBand.Good)]
    public void 电量档位_与托盘颜色阈值一致(byte? percent, BatteryBand expected)
        => Assert.Equal(expected, TrayRules.BandOf(percent));

    [Fact]
    public void 提醒_跌破阈值_要弹()
    {
        var before = new[] { Dev("A", 25) };
        var after = new[] { Dev("A", 15) };
        var low = TrayRules.NewlyLow(before, after, 20);
        Assert.Single(low);
        Assert.Equal("A", low[0].Name);
    }

    [Fact]
    public void 提醒_持续低位_不重复弹()
    {
        var before = new[] { Dev("A", 15) };
        var after = new[] { Dev("A", 12) };
        Assert.Empty(TrayRules.NewlyLow(before, after, 20));
    }

    [Fact]
    public void 提醒_首读即低_要弹()
    {
        var after = new[] { Dev("A", 10) };
        Assert.Single(TrayRules.NewlyLow(Array.Empty<DeviceEntry>(), after, 20));
    }

    [Fact]
    public void 提醒_阈值之上_不弹()
    {
        var before = new[] { Dev("A", 30) };
        var after = new[] { Dev("A", 25) };
        Assert.Empty(TrayRules.NewlyLow(before, after, 20));
    }

    [Fact]
    public void 提醒_断连的低位设备_不弹()
    {
        var after = new[] { Dev("A", 10, connected: false) };
        Assert.Empty(TrayRules.NewlyLow(Array.Empty<DeviceEntry>(), after, 20));
    }

    [Fact]
    public void 提醒_回升后再跌_再弹()
    {
        var r1 = TrayRules.NewlyLow(new[] { Dev("A", 25) }, new[] { Dev("A", 15) }, 20);
        Assert.Single(r1);
        var r2 = TrayRules.NewlyLow(new[] { Dev("A", 15) }, new[] { Dev("A", 30) }, 20);
        Assert.Empty(r2);
        var r3 = TrayRules.NewlyLow(new[] { Dev("A", 30) }, new[] { Dev("A", 18) }, 20);
        Assert.Single(r3);
    }
}
