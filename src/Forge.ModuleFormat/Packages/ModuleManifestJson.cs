using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Packages;

public static class ModuleManifestJson
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static string Serialize(ModuleManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return JsonSerializer.Serialize(manifest, Options);
    }

    public static ModuleManifest Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize<ModuleManifest>(json, Options)
            ?? throw new JsonException("The module manifest is empty.");
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new AreaIdJsonConverter());
        options.Converters.Add(new ModuleIdJsonConverter());
        options.Converters.Add(new ThirdPartyDependencyIdJsonConverter());
        return options;
    }
}
