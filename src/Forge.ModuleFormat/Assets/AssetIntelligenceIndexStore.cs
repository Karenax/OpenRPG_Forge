using System.Text;
using System.Text.Json;
using Forge.Domain.Assets;
using Forge.Domain.Validation;

namespace Forge.ModuleFormat.Assets;

public static class AssetIntelligenceIndexStore
{
    public const string RelativePath = "metadata/asset-intelligence.v1.json";
    public const string InvalidCode = "asset.intelligence.index_invalid";
    public const string WriteFailedCode = "asset.intelligence.index_write_failed";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    public static AssetIntelligenceIndexLoadResult Load(string vaultRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vaultRoot);
        return LoadFile(Path.Combine(
            Path.GetFullPath(vaultRoot),
            RelativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    public static AssetIntelligenceIndexLoadResult LoadDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!Directory.Exists(directory)) return new([], []);
        var profiles = new Dictionary<string, AssetIntelligenceProfile>(StringComparer.OrdinalIgnoreCase);
        var diagnostics = new List<DomainDiagnostic>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json")
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            var loaded = LoadFile(path);
            diagnostics.AddRange(loaded.Diagnostics);
            foreach (var profile in loaded.Profiles)
            {
                if (!profiles.TryAdd(profile.ExternalId, profile))
                {
                    diagnostics.Add(new(
                        InvalidCode,
                        $"Asset intelligence profile '{profile.ExternalId}' is declared by multiple overlays.",
                        path));
                }
            }
        }

        return new(profiles.Values.OrderBy(item => item.ExternalId, StringComparer.Ordinal).ToArray(), diagnostics);
    }

    public static AssetIntelligenceIndexLoadResult LoadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path)) return new([], []);
        try
        {
            var document = JsonSerializer.Deserialize<AssetIntelligenceIndexDocument>(
                    File.ReadAllText(path),
                    JsonOptions)
                ?? throw new InvalidDataException("Asset intelligence index is empty.");
            if (document.SchemaVersion != AssetIntelligenceProfile.CurrentSchemaVersion || document.Profiles is null)
            {
                throw new InvalidDataException(
                    $"Asset intelligence index schema '{document.SchemaVersion}' is unsupported or incomplete.");
            }

            if (document.Profiles.Any(profile => profile is null))
            {
                throw new InvalidDataException("Asset intelligence profiles cannot contain null entries.");
            }

            var profiles = document.Profiles
                .OrderBy(item => item.ExternalId, StringComparer.Ordinal)
                .ToArray();
            if (profiles.Select(item => item.ExternalId).Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
                profiles.Length)
            {
                throw new InvalidDataException("Asset intelligence external IDs must be unique.");
            }

            foreach (var profile in profiles)
            {
                var diagnostic = AssetIntelligenceProfileValidator.Validate(profile, path).FirstOrDefault();
                if (diagnostic is not null) throw new InvalidDataException(diagnostic.Message);
            }

            return new(profiles, []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           JsonException or InvalidDataException or ArgumentException)
        {
            return new([], [new DomainDiagnostic(InvalidCode, exception.Message, path)]);
        }
    }

    public static AssetIntelligenceIndexSaveResult Save(
        string vaultRoot,
        IReadOnlyList<AssetIntelligenceProfile> profiles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vaultRoot);
        ArgumentNullException.ThrowIfNull(profiles);
        var path = Path.Combine(
            Path.GetFullPath(vaultRoot),
            RelativePath.Replace('/', Path.DirectorySeparatorChar));
        try
        {
            var ordered = profiles.OrderBy(item => item.ExternalId, StringComparer.Ordinal).ToArray();
            if (ordered.Select(item => item.ExternalId).Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
                ordered.Length)
            {
                throw new InvalidDataException("Asset intelligence external IDs must be unique.");
            }

            foreach (var profile in ordered)
            {
                var diagnostic = AssetIntelligenceProfileValidator.Validate(profile, RelativePath).FirstOrDefault();
                if (diagnostic is not null) throw new InvalidDataException(diagnostic.Message);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            WriteAtomically(path, JsonSerializer.Serialize(
                new AssetIntelligenceIndexDocument(AssetIntelligenceProfile.CurrentSchemaVersion, ordered),
                JsonOptions) + Environment.NewLine);
            return new(true, path, []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           JsonException or InvalidDataException or ArgumentException)
        {
            return new(false, null, [new DomainDiagnostic(WriteFailedCode, exception.Message, path)]);
        }
    }

    private static void WriteAtomically(string targetPath, string content)
    {
        var temporaryPath = Path.Combine(
            Path.GetDirectoryName(targetPath)!,
            $".{Path.GetFileName(targetPath)}.{Path.GetRandomFileName()}.tmp");
        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       16 * 1024,
                       FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 16 * 1024, leaveOpen: true))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private sealed record AssetIntelligenceIndexDocument(
        int SchemaVersion,
        IReadOnlyList<AssetIntelligenceProfile>? Profiles);
}

public sealed record AssetIntelligenceIndexLoadResult(
    IReadOnlyList<AssetIntelligenceProfile> Profiles,
    IReadOnlyList<DomainDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}

public sealed record AssetIntelligenceIndexSaveResult(
    bool Succeeded,
    string? Path,
    IReadOnlyList<DomainDiagnostic> Diagnostics);
