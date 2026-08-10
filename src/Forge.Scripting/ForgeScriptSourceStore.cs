using System.Text;
using System.Text.Json;

namespace Forge.Scripting;

public sealed class ForgeScriptSourceStore
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true,
    };
    private readonly string _workspaceRoot;

    public ForgeScriptSourceStore(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        _workspaceRoot = Path.GetFullPath(workspaceRoot);
    }

    public void Save(ForgeScriptSourceDocument document)
    {
        var inferredContracts = document.EventBindings.GroupBy(binding => binding.EventName, StringComparer.Ordinal)
            .Select(group => new ForgeScriptEventContract(group.First().Id, 1, group.First().Provenance,
                group.Key, group.First().PayloadType, document.RequiredAtRuntime)).ToArray();
        var diagnostics = ForgeScriptSourceValidator.Validate(document, inferredContracts);
        var blocking = diagnostics.FirstOrDefault(item => item.Severity == "error");
        if (blocking is not null) throw new InvalidOperationException($"{blocking.Code}:{blocking.Path}");
        var sourcePath = Resolve(document.RelativePath);
        var metadataPath = sourcePath + ".meta.json";
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        WriteAtomically(sourcePath, Utf8NoBom.GetBytes(document.Source));
        var metadata = new Metadata(document.Id, document.SchemaVersion, document.Provenance,
            document.RelativePath.Replace('\\', '/'), document.ApiVersion, document.Distribution,
            document.RequiredAtRuntime, document.Dependencies, document.EventBindings, document.RequiredCapabilities);
        WriteAtomically(metadataPath, Utf8NoBom.GetBytes(JsonSerializer.Serialize(metadata, JsonOptions) + "\n"));
    }

    public ForgeScriptSourceDocument Load(string relativePath)
    {
        var sourcePath = Resolve(relativePath);
        var metadata = JsonSerializer.Deserialize<Metadata>(File.ReadAllText(sourcePath + ".meta.json", Utf8NoBom), JsonOptions)
            ?? throw new InvalidOperationException("script.source.metadata_invalid");
        if (!string.Equals(metadata.RelativePath.Replace('\\', '/'), relativePath.Replace('\\', '/'), StringComparison.Ordinal))
            throw new InvalidOperationException("script.source.metadata_path_mismatch");
        return new(metadata.Id, metadata.SchemaVersion, metadata.Provenance, metadata.RelativePath,
            File.ReadAllText(sourcePath, Utf8NoBom), metadata.ApiVersion, metadata.Distribution,
            metadata.RequiredAtRuntime, metadata.Dependencies, metadata.EventBindings, metadata.RequiredCapabilities);
    }

    private string Resolve(string relativePath)
    {
        if (!ForgeScriptSourceValidator.IsSafeSourcePath(relativePath))
            throw new ArgumentException("script.source.path_invalid", nameof(relativePath));
        var destination = Path.GetFullPath(Path.Combine(_workspaceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!destination.StartsWith(_workspaceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("script.source.path_outside_workspace", nameof(relativePath));
        return destination;
    }

    private static void WriteAtomically(string destination, byte[] content)
    {
        var temporary = $"{destination}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(temporary, content);
            File.Move(temporary, destination, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private sealed record Metadata(Guid Id, int SchemaVersion, string Provenance, string RelativePath,
        string ApiVersion, ForgeScriptSourceDistributionPolicy Distribution, bool RequiredAtRuntime,
        IReadOnlyList<string> Dependencies, IReadOnlyList<ForgeScriptEventBinding> EventBindings,
        IReadOnlySet<string>? RequiredCapabilities);
}
