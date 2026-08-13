using System.IO.Compression;
using Rovia.Runtime.Diagnostics;
using Rovia.Runtime.Runtime;

namespace Rovia.Runtime.Tests;

/// <summary>Validates bounded logs and credential-free diagnostic exports.</summary>
public sealed class DiagnosticExporterTests
{
    [Fact]
    public void Export_OmitsNodeIdentityAndConfiguration()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"rovia-diagnostics-{Guid.NewGuid():N}");
        string output    = Path.Combine(directory, "export.zip");
        try
        {
            new RuntimeStateStore(Path.Combine(directory, "runtime-state.json")).Write(new RuntimeState
            {
                NodeId = "private-id", NodeName = "private-name", LastMessage = "healthy"
            });
            new RuntimeLog(Path.Combine(directory, "logs")).Write("Information", "test", "safe message");

            DiagnosticExporter.Export(directory, output);

            using ZipArchive archive = ZipFile.OpenRead(output);
            Assert.DoesNotContain(archive.Entries, entry => entry.Name.Equals("nodes.json", StringComparison.OrdinalIgnoreCase));
            using StreamReader reader = new(archive.GetEntry("runtime-state.json")!.Open());
            string state = reader.ReadToEnd();
            Assert.DoesNotContain("private-id", state);
            Assert.DoesNotContain("private-name", state);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}
