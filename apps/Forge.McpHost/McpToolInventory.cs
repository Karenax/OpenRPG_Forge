using System.Reflection;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace Forge.McpHost;

public static class McpToolInventory
{
    public const string SdkVersion = "1.4.1";
    public const int SchemaVersion = 1;
    public const string ManifestFileName = "mcp-tools.json";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static IReadOnlyList<string> Discover(Assembly? assembly = null)
    {
        assembly ??= typeof(McpToolInventory).Assembly;
        return assembly
            .GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            .Select(method => (Method: method, Attribute: method.GetCustomAttribute<McpServerToolAttribute>()))
            .Where(candidate => candidate.Attribute is not null)
            .Select(candidate => candidate.Attribute!.Name ?? candidate.Method.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    public static string SerializeCurrentManifest() =>
        JsonSerializer.Serialize(
            new McpToolManifest(SchemaVersion, SdkVersion, Discover()),
            SerializerOptions);

    public static McpToolInventoryValidation Validate(string? manifestPath = null)
    {
        manifestPath ??= Path.Combine(AppContext.BaseDirectory, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return new(false, Discover(), [], [], [], $"Tool manifest is missing: {manifestPath}");
        }

        McpToolManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<McpToolManifest>(File.ReadAllText(manifestPath), SerializerOptions);
        }
        catch (JsonException exception)
        {
            return new(false, Discover(), [], [], [], $"Tool manifest is invalid JSON: {exception.Message}");
        }

        if (manifest is null || manifest.SchemaVersion != SchemaVersion ||
            !string.Equals(manifest.SdkVersion, SdkVersion, StringComparison.Ordinal))
        {
            return new(false, Discover(), manifest?.ToolNames ?? [], [], [],
                "Tool manifest schema or MCP SDK version does not match the host.");
        }

        var discovered = Discover();
        var declared = manifest.ToolNames.Order(StringComparer.Ordinal).ToArray();
        var duplicates = declared
            .GroupBy(name => name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var missing = declared.Except(discovered, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var unexpected = discovered.Except(declared, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var succeeded = duplicates.Length == 0 && missing.Length == 0 && unexpected.Length == 0 &&
            discovered.Count == declared.Length;
        return new(succeeded, discovered, declared, missing, unexpected,
            succeeded ? null : "The compiled MCP tool inventory differs from the checked manifest.", duplicates);
    }
}

public sealed record McpToolManifest(
    int SchemaVersion,
    string SdkVersion,
    IReadOnlyList<string> ToolNames);

public sealed record McpToolInventoryValidation(
    bool Succeeded,
    IReadOnlyList<string> Discovered,
    IReadOnlyList<string> Declared,
    IReadOnlyList<string> Missing,
    IReadOnlyList<string> Unexpected,
    string? Error,
    IReadOnlyList<string>? Duplicates = null);
