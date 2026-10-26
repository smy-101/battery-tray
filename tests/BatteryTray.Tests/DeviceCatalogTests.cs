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
    public void 同名不同传输_合并为一行_电量可读者优先()
    {
        // 典型:蓝牙键鼠同时命中 AEP 枚举(蓝牙,无电量)与 HID 0x85(USB/2.4G,有电量)
        var catalog = new DeviceCatalog();
        var list = catalog.ReplaceAll(new[]
        {
            Entry("MX Keys", Transport.BluetoothClassic, battery: null),
            Entry("MX Keys", Transport.UsbHid, battery: 80),
        });
        var item = Assert.Single(list);
        Assert.Equal((byte?)80, item.BatteryPercent);
        Assert.Equal(Transport.UsbHid, item.Transport);
    }

    [Fact]
    public void 同名不同传输_双模耳机两行都有电量_保留先到者()
    {
        // 双模耳机同时命中经典/LE 两个 AEP,两条都有电量:确定性保留先到者
        var catalog = new DeviceCatalog();
        var list = catalog.ReplaceAll(new[]
        {
            Entry("WH-1000", Transport.BluetoothClassic, battery: 50),
            Entry("WH-1000", Transport.BluetoothLe, battery: 60),
        });
        var item = Assert.Single(list);
        Assert.Equal((byte?)50, item.BatteryPercent);
    }

    [Fact]
    public void 不同名不同传输_各自保留()
    {
        var catalog = new DeviceCatalog();
        var list = catalog.ReplaceAll(new[]
        {
            Entry("WH-1000", Transport.BluetoothClassic, battery: 50),
            Entry("Keychron K3", Transport.BluetoothLe, battery: 60),
        });
        Assert.Equal(2, list.Count);
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
