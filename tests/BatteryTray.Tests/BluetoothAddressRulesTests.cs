using BatteryTray.Core;
using Xunit;

namespace BatteryTray.Tests;

public class BluetoothAddressRulesTests
{
    [Theory]
    [InlineData("Bluetooth#BluetoothL#C0:E5:79:27:CE:76#-1#HUAWEI", "C0E57927CE76")]
    [InlineData("Bluetooth#BluetoothL#00:11:22:33:44:55#-1", "001122334455")]
    [InlineData("Bluetooth#DEV_C0E57927CE76#7&1&0", "C0E57927CE76")]
    public void 带冒号地址_解析并归一化(string id, string expected)
    {
        Assert.True(BluetoothAddressRules.TryParseFromAepId(id, out var hex));
        Assert.Equal(expected, hex);
    }

    [Theory]
    [InlineData("BluetoothLE#BluetoothLEc0:e5:79:27:ce:76#-1")]   // LE id 冒号地址同样能解
    [InlineData("")]
    [InlineData(null)]
    [InlineData("USB\\VID_046D&PID_C52B\\5&2f3a")]                 // 无地址
    public void 无经典地址或空_id(string id)
    {
        // 这些要么解析失败,要么仅当含合法 MAC 时成功;关键是不抛异常
        BluetoothAddressRules.TryParseFromAepId(id, out _);
    }

    [Fact]
    public void 输出为12位大写()
    {
        Assert.True(BluetoothAddressRules.TryParseFromAepId("X#c0:e5:79:27:ce:76#Y", out var hex));
        Assert.Equal(12, hex.Length);
        Assert.Equal(hex, hex.ToUpperInvariant());
    }
}
