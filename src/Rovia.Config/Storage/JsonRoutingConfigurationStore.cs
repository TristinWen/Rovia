using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rovia.Config.Storage;

/// <summary>Loads and atomically saves backend-independent routing configuration.</summary>
public sealed class JsonRoutingConfigurationStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly string _path = Path.GetFullPath(path);

    public RoutingConfiguration Read() => File.Exists(_path)
        ? JsonSerializer.Deserialize<RoutingConfiguration>(File.ReadAllText(_path), JsonOptions)
          ?? throw new InvalidOperationException("Routing configuration is empty.")
        : new();

    public void Write(RoutingConfiguration configuration)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        string temporaryPath = $"{_path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(configuration, JsonOptions));
        File.Move(temporaryPath, _path, true);
    }
}
