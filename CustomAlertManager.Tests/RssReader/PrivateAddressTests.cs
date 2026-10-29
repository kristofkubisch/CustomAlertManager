using System.Net;
using CustomAlertManager.RssReader.Services;

namespace CustomAlertManager.Tests.RssReader;

public class PrivateAddressTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.1")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.0.1")]
    [InlineData("169.254.1.1")]
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("::1")]
    [InlineData("fd00::1")]
    [InlineData("fe80::1")]
    [InlineData("::ffff:10.0.0.1")]
    public void IsPrivate_PrivateOrLocal_IsTrue(string address)
    {
        Assert.True(PrivateAddressClassifier.IsPrivate(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("172.32.0.1")]
    [InlineData("100.128.0.1")]
    [InlineData("2001:4860:4860::8888")]
    public void IsPrivate_PublicAddress_IsFalse(string address)
    {
        Assert.False(PrivateAddressClassifier.IsPrivate(IPAddress.Parse(address)));
    }
}
