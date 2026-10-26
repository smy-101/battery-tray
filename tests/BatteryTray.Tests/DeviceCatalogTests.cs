using BatteryTray.Core;
using Xunit;

namespace BatteryTray.Tests;

public class DeviceCatalogTests
{
    private static DeviceEntry Entry(string name, Transport t = Transport.BluetoothClassic,
        bool connected = true, byte? battery = null) =>
        new() { Name = name, Transport = t, IsConnected = connected, BatteryPercent = battery };

    [Fact]
    public void 同名同传输_去重保留有电量者()
    {
        var catalog = new DeviceCatalog();
        var list = catalog.ReplaceAll(new[]
        {
            Entry("MX Keys", battery: null),
            Entry("MX Keys", battery: 80),
        });
        var item = Assert.Single(list);
        Assert.Equal((byte?)80, item.BatteryPercent);
    }

    [Fact]
    public void 同名不同传输_各自保留()
    {
        var catalog = new DeviceCatalog();
        var list = catalog.ReplaceAll(new[]
        {
            Entry("WH-1000", Transport.BluetoothClassic, battery: 50),
            Entry("WH-1000", Transport.BluetoothLe, battery: 60),
        });
        Assert.Equal(2, list.Count);
        Assert.Equal(2, list.Select(e => e.Transport).Distinct().Count());
    }

    [Fact]
    public void 排序_已连接在前_再按名称()
    {
        var catalog = new DeviceCatalog();
        var list = catalog.ReplaceAll(new[]
        {
            Entry("B-设备", connected: false),
            Entry("A-设备", connected: false),
            Entry("Z-设备", connected: true),
        });
        Assert.Equal(new[] { "Z-设备", "A-设备", "B-设备" }, list.Select(e => e.Name));
    }

    [Fact]
    public void 全部无电量_不丢设备()
    {
        var catalog = new DeviceCatalog();
        var list = catalog.ReplaceAll(new[] { Entry("鼠标", battery: null), Entry("键盘", battery: null) });
        Assert.Equal(2, list.Count);
        Assert.All(list, e => Assert.Null(e.BatteryPercent));
    }

    [Fact]
    public void 快照反映最近一次替换()
    {
        var catalog = new DeviceCatalog();
        catalog.ReplaceAll(new[] { Entry("旧设备") });
        catalog.ReplaceAll(new[] { Entry("新设备") });
        var name = Assert.Single(catalog.Snapshot).Name;
        Assert.Equal("新设备", name);
    }

    [Fact]
    public void 重复项_电量优先于连接状态()
    {
        var catalog = new DeviceCatalog();
        var list = catalog.ReplaceAll(new[]
        {
            Entry("X", connected: true, battery: null),
            Entry("X", connected: false, battery: 10),
        });
        var item = Assert.Single(list);
        Assert.Equal((byte?)10, item.BatteryPercent);
        // 保留的是带电量那条记录自身的连接标志
        Assert.False(item.IsConnected);
    }
}
