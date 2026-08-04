using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Network.Sessions;

namespace Forge.Network.Configuration;

public sealed class NetworkConfigurationStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public string Serialize(DedicatedServerConfiguration configuration)
    {
        var diagnostics = NetworkValidation.Validate(configuration);
        if (diagnostics.Count != 0) throw new InvalidDataException(diagnostics[0].Code);
        return JsonSerializer.Serialize(configuration, Options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    public DedicatedServerConfiguration Deserialize(string json)
    {
        var configuration = JsonSerializer.Deserialize<DedicatedServerConfiguration>(json, Options)
            ?? throw new InvalidDataException("NET_CONFIG_EMPTY");
        var diagnostics = NetworkValidation.Validate(configuration);
        if (diagnostics.Count != 0) throw new InvalidDataException(diagnostics[0].Code);
        return configuration;
    }
}
