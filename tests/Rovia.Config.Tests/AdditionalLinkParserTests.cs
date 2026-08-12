using System.Text;
using Rovia.Config.Parsing;
using Rovia.Core.Models;

namespace Rovia.Config.Tests;

/// <summary>Validates Trojan, Shadowsocks, and VMess share-link parsing.</summary>
public sealed class AdditionalLinkParserTests
{
    [Fact]
    public void Trojan_MapsTlsAndWebSocket()
    {
        ProxyNode node = new TrojanLinkParser().Parse("trojan://secret@example.com:443?security=tls&sni=cdn.example.com&type=ws&path=%2Fws#Trojan");
        Assert.Equal(ProxyProtocol.Trojan, node.Protocol);
        Assert.Equal("secret", node.Credentials.Password);
        Assert.Equal("/ws", node.Transport?.Path);
    }

    [Fact]
    public void Shadowsocks_MapsSip002Credentials()
    {
        string credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("aes-256-gcm:secret")).TrimEnd('=');
        ProxyNode node = new ShadowsocksLinkParser().Parse($"ss://{credentials}@example.com:8388#SS");
        Assert.Equal("aes-256-gcm", node.Metadata["method"]);
        Assert.Equal("secret", node.Credentials.Password);
    }

    [Fact]
    public void Vmess_MapsBase64Json()
    {
        string json = "{\"v\":\"2\",\"ps\":\"VMess\",\"add\":\"example.com\",\"port\":\"443\",\"id\":\"11111111-1111-1111-1111-111111111111\",\"aid\":\"0\",\"net\":\"ws\",\"path\":\"/ws\",\"tls\":\"tls\"}";
        ProxyNode node = new VmessLinkParser().Parse("vmess://" + Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));
        Assert.Equal(ProxyProtocol.Vmess, node.Protocol);
        Assert.Equal("/ws", node.Transport?.Path);
    }
}
