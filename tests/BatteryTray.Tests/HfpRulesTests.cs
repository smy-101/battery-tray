using BatteryTray.Core;
using Xunit;

namespace BatteryTray.Tests;

public class HfpRulesTests
{
    [Theory]
    [InlineData("{0000111E-0000-1000-8000-00805F9B34FB}_VID&00021_PID&4863")]
    [InlineData("{0000111e-0000-1000-8000-00805f9b34fb}")]
    public void HFP服务键_含111e_匹配(string key)
    {
        Assert.True(HfpRules.IsHfpServiceKey(key));
    }

    [Theory]
    [InlineData("{0000110B-0000-1000-8000-00805F9B34FB}_VID&0002_PID&4863")] // A2DP
    [InlineData("{0000110A-0000-1000-8000-00805F9B34FB}")]
    [InlineData("USB\\VID_046D&PID_C52B")]
    public void 非HFP服务键_不匹配(string key)
    {
        Assert.False(HfpRules.IsHfpServiceKey(key));
    }

    [Fact]
    public void 实例名_含目标地址_匹配_忽略大小写()
    {
        Assert.True(HfpRules.IsAddressMatch("7&1bd0e2d8&0&0025764F8E3F_C00000000", "0025764F8E3F"));
        Assert.True(HfpRules.IsAddressMatch("7&1bd0e2d8&0&0025764f8e3f_C00000000", "0025764F8E3F"));
    }

    [Fact]
    public void 实例名_地址不符_不匹配()
    {
        Assert.False(HfpRules.IsAddressMatch("7&1bd0e2d8&0&0025764FFFFFF_C00000000", "0025764F8E3F"));
    }

    [Fact]
    public void 空地址_不匹配()
    {
        Assert.False(HfpRules.IsAddressMatch("7&1&0&0025764F8E3F_C00000000", ""));
        Assert.False(HfpRules.IsAddressMatch("7&1&0&0025764F8E3F_C00000000", null!));
    }

    [Fact]
    public void 实例id_拼接格式()
    {
        Assert.Equal(
            @"BTHENUM\{0000111E-0000-1000-8000-00805F9B34FB}_VID&0002_PID&4863\7&1&0&0025764F8E3F_C00000000",
            HfpRules.InstanceId("{0000111E-0000-1000-8000-00805F9B34FB}_VID&0002_PID&4863", "7&1&0&0025764F8E3F_C00000000"));
    }
}
