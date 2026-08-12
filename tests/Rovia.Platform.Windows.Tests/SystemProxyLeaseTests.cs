using Rovia.Platform.Windows.Proxy;

namespace Rovia.Platform.Windows.Tests;

/// <summary>Validates exact system proxy activation and restoration semantics.</summary>
public sealed class SystemProxyLeaseTests
{
    [Fact]
    public void Dispose_RestoresPreviousSettings()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"rovia-proxy-{Guid.NewGuid():N}");
        string path      = Path.Combine(directory, "proxy.json");
        SystemProxySnapshot previous = new(true, "127.0.0.1:10808", "<local>", null);
        FakeSettings settings        = new(previous);
        try
        {
            using (SystemProxyLease.Activate(settings, path, "127.0.0.1:2080"))
                Assert.Equal("127.0.0.1:2080", settings.Current.Server);

            Assert.Equal(previous, settings.Current);
            Assert.False(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    private sealed class FakeSettings(SystemProxySnapshot current) : ISystemProxySettings
    {
        public SystemProxySnapshot Current { get; private set; } = current;
        public SystemProxySnapshot Read() => Current;
        public void Apply(SystemProxySnapshot snapshot) => Current = snapshot;
    }
}
