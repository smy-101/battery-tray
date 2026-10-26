using BatteryTray.Core;
using Xunit;

namespace BatteryTray.Tests;

public class HidppRulesTests
{
    [Fact]
    public void feature请求_帧格式正确()
    {
        var req = HidppRules.BuildFeatureRequest(0x02, 0x03, 0x01, 0x05, 0xAA, 0xBB, 0xCC);
        Assert.Equal(new byte[] { 0x10, 0x02, 0x03, 0x15, 0xAA, 0xBB, 0xCC }, req);
    }

    [Fact]
    public void 长报告_20字节_报告号正确()
    {
        var req = HidppRules.BuildFeatureRequest(0x01, 0x02, 0x00, 0x01, longReport: true);
        Assert.Equal(20, req.Length);
        Assert.Equal(0x11, req[0]);
        Assert.Equal(new byte[13], req[7..]); // 参数区补零
    }

    [Fact]
    public void RAP读寄存器_帧格式正确()
    {
        var req = HidppRules.BuildRegisterRead(0x01, 0x07, 0x03);
        Assert.Equal(new byte[] { 0x10, 0x01, 0x83, 0x07, 0, 0, 0 }, req);
    }

    [Fact]
    public void 错误响应_识别并取错误码()
    {
        var resp = new byte[] { 0x10, 0x01, 0x00, 0x8F, 0x02, 0, 0 }; // 0x8x = 错误,码 2
        Assert.True(HidppRules.IsError(resp, out var code));
        Assert.Equal(2, code);
    }

    [Theory]
    [InlineData(0x10)] // 正常回显
    [InlineData(0x90)] // 高位置 1 的回显
    public void 正常响应_不算错误(byte fnByte)
    {
        var resp = new byte[] { 0x10, 0x01, 0x02, fnByte, 0x50, 0, 0 };
        Assert.False(HidppRules.IsError(resp, out _));
    }

    [Fact]
    public void 响应匹配_设备feature_swId全对才匹配()
    {
        var resp = new byte[] { 0x10, 0x01, 0x02, 0x11, 0x50, 0, 0 };
        Assert.True(HidppRules.Matches(resp, 0x01, 0x02, 0x01));
        Assert.False(HidppRules.Matches(resp, 0x02, 0x02, 0x01));
        Assert.False(HidppRules.Matches(resp, 0x01, 0x03, 0x01));
        Assert.False(HidppRules.Matches(resp, 0x01, 0x02, 0x02));
    }

    [Fact]
    public void root响应_解析协议版本()
    {
        var resp = new byte[] { 0x10, 0x01, 0x00, 0x11, 0x1E, 0x04, 0x05 }; // 30 个 feature,协议 4.5
        var root = HidppRules.ParseRoot(resp);
        Assert.NotNull(root);
        Assert.Equal(30, root!.Value.FeatureCount);
        Assert.Equal(4, root.Value.Major);
        Assert.Equal(5, root.Value.Minor);
    }

    [Fact]
    public void feature枚举响应_解析featureId与索引()
    {
        var resp = new byte[] { 0x10, 0x01, 0x01, 0x11, 0x04, 0x10, 0x03, 0x00 }; // feature 0x1004 → idx 3
        var e = HidppRules.ParseFeatureEnum(resp);
        Assert.NotNull(e);
        Assert.Equal((ushort)0x1004, e!.Value.FeatureId);
        Assert.Equal(3, e.Value.Index);
    }

    [Theory]
    [MemberData(nameof(解析数据))]
    public void 电量解析_三种格式(byte[] resp, byte? expected, string label)
    {
        var actual = label switch
        {
            "unified" => HidppRules.ParseUnifiedBattery(resp),
            "legacy" => HidppRules.ParseLegacyBattery(resp),
            _ => HidppRules.ParseRegisterBattery(resp),
        };
        Assert.Equal(expected, actual);
    }

    public static TheoryData<byte[], byte?, string> 解析数据 = new()
    {
        { new byte[] { 0x10, 1, 2, 0x11, 55, 3, 0 }, (byte?)55, "unified" },
        { new byte[] { 0x10, 1, 2, 0x11, 101, 0, 0 }, null, "unified" },  // 越界
        { new byte[] { 0x10, 1, 2, 0x10, 80, 0, 0 }, (byte?)80, "legacy" },
        { new byte[] { 0x10, 1, 0x83, 0x07, 66, 0x00, 0x00 }, (byte?)66, "register" }, // RAP: [id, dev, fn, 寄存器回显, 电量, …]
    };
}
