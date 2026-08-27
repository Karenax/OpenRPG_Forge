using System.Security.Cryptography;
using System.Text.Json;
using Forge.Domain.WorldGeneration;

namespace Forge.EngineAdapter.WorldGeneration;

public sealed record WorldGenerationBackendCapability(
    WorldGenerationBackend Backend,
    bool Available,
    string Evidence,
    string VersionOrRevision,
    IReadOnlyList<string> Diagnostics)
{
    public string ProviderKey { get; init; } = string.Empty;
    public bool ReceiptVerified { get; init; }
    public string ReceiptPath { get; init; } = string.Empty;
}

public static class WorldGenerationBackendCapabilityProbe
{
    public const string InstallationReceiptRelativePath = ".openrpg/world-generation/provider-installation.json";

    private const string Terrain3DVersion = "v1.0.2-stable";
    private const string GodotFlowRevision = "284231f060099a8943eb56567c21732b7cd678d3";

    private static readonly BackendMarker[] Terrain3DCandidates =
    [
        new("addons/terrain_3d/plugin.cfg", WorldGenerationProviderKeys.Terrain3D, Terrain3DVersion),
        new("addons/terrain_3d/terrain.gdextension", WorldGenerationProviderKeys.Terrain3D, Terrain3DVersion),
    ];

    private static readonly BackendMarker[] GraphPcgCandidates =
    [
        new("addons/flow_nodes_editor/plugin.cfg", WorldGenerationProviderKeys.GodotFlow, GodotFlowRevision),
        new("addons/godot_flow/plugin.cfg", WorldGenerationProviderKeys.GodotFlow, GodotFlowRevision),
    ];

    public static IReadOnlyList<WorldGenerationBackendCapability> Probe(string godotProjectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(godotProjectRoot);
        var root = Path.GetFullPath(godotProjectRoot);
        var diagnostics = new List<string>();
        if (!File.Exists(Path.Combine(root, "project.godot")))
            diagnostics.Add("project.godot was not found; addon discovery is filesystem-only.");

        var receiptPath = ResolveProjectPath(root, InstallationReceiptRelativePath);
        if (File.Exists(receiptPath))
        {
            try
            {
                var installations = ReadReceipt(receiptPath);
                return
                [
                    ProbeReceipt(root, receiptPath, WorldGenerationBackend.Terrain3D, installations, diagnostics),
                    ProbeReceipt(root, receiptPath, WorldGenerationBackend.PcgGodot, installations, diagnostics),
                    NativeCapability(),
                ];
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
            {
                var receiptDiagnostics = diagnostics
                    .Concat([$"Provider installation receipt is invalid: {exception.Message}"])
                    .ToArray();
                return
                [
                    Unavailable(WorldGenerationBackend.Terrain3D, WorldGenerationProviderKeys.Terrain3D,
                        Terrain3DVersion, receiptDiagnostics),
                    Unavailable(WorldGenerationBackend.PcgGodot, WorldGenerationProviderKeys.GodotFlow,
                        GodotFlowRevision, receiptDiagnostics),
                    NativeCapability(),
                ];
            }
        }

        return
        [
            ProbeMarkers(root, WorldGenerationBackend.Terrain3D, Terrain3DCandidates, diagnostics),
            ProbeMarkers(root, WorldGenerationBackend.PcgGodot, GraphPcgCandidates, diagnostics),
            NativeCapability(),
        ];
    }

    private static WorldGenerationBackendCapability ProbeMarkers(
        string root,
        WorldGenerationBackend backend,
        IReadOnlyList<BackendMarker> candidates,
        IReadOnlyList<string> sharedDiagnostics)
    {
        var available = candidates
            .Where(candidate => File.Exists(Path.Combine(
                root,
                candidate.RelativePath.Replace('/', Path.DirectorySeparatorChar))))
            .ToArray();
        var expected = candidates[0];
        if (available.Length == 0)
        {
            return Unavailable(backend, expected.ProviderKey, expected.VersionOrRevision,
                sharedDiagnostics.Concat([$"No {backend} addon marker was found."]).ToArray());
        }

        var providerGroups = available
            .GroupBy(candidate => candidate.ProviderKey, StringComparer.Ordinal)
            .ToArray();
        var diagnostics = sharedDiagnostics.ToList();
        if (providerGroups.Length > 1)
        {
            diagnostics.Add(
                $"Multiple {backend} providers were detected; no concurrent owner may be selected.");
            return Unavailable(backend, string.Empty, expected.VersionOrRevision, diagnostics);
        }

        var selectedGroup = providerGroups[0].ToArray();
        var selected = selectedGroup[0];
        diagnostics.Add(
            $"No versioned provider installation receipt was found; {backend} identity is marker-only and unverified.");

        return new(backend, true,
            string.Join(';', selectedGroup.Select(candidate => candidate.RelativePath)),
            selected.VersionOrRevision,
            diagnostics)
        {
            ProviderKey = selected.ProviderKey,
        };
    }

    private static WorldGenerationBackendCapability ProbeReceipt(
        string root,
        string receiptPath,
        WorldGenerationBackend backend,
        IReadOnlyList<ProviderInstallation> installations,
        IReadOnlyList<string> sharedDiagnostics)
    {
        var backendKey = BackendKey(backend);
        var owners = installations
            .Where(installation => string.Equals(installation.Backend, backendKey, StringComparison.Ordinal))
            .ToArray();
        var fallbackProvider = backend == WorldGenerationBackend.Terrain3D
            ? WorldGenerationProviderKeys.Terrain3D
            : WorldGenerationProviderKeys.GodotFlow;
        var fallbackVersion = backend == WorldGenerationBackend.Terrain3D
            ? Terrain3DVersion
            : GodotFlowRevision;
        if (owners.Length == 0)
        {
            return Unavailable(backend, fallbackProvider, fallbackVersion,
                sharedDiagnostics.Concat([$"The provider installation receipt declares no owner for {backend}."]).ToArray());
        }

        if (owners.Length > 1)
        {
            return Unavailable(backend, string.Empty, fallbackVersion,
                sharedDiagnostics.Concat(
                    [$"The provider installation receipt declares {owners.Length} concurrent owners for {backend}."])
                    .ToArray());
        }

        var owner = owners[0];
        var diagnostics = sharedDiagnostics.ToList();
        if (!IsAllowedProvider(backend, owner.ProviderKey))
        {
            diagnostics.Add($"Provider '{owner.ProviderKey}' is not allowed to own {backend}.");
            return Unavailable(backend, owner.ProviderKey, owner.VersionOrRevision, diagnostics);
        }

        var expectedMarkers = MarkerPaths(owner.ProviderKey);
        var markerEvidence = owner.Evidence
            .Where(item => expectedMarkers.Contains(item.RelativePath, StringComparer.Ordinal))
            .ToArray();
        if (markerEvidence.Length == 0)
        {
            diagnostics.Add($"Provider '{owner.ProviderKey}' has no recognized addon marker in its receipt evidence.");
            return Unavailable(backend, owner.ProviderKey, owner.VersionOrRevision, diagnostics);
        }

        foreach (var evidence in owner.Evidence)
        {
            string evidencePath;
            try
            {
                evidencePath = ResolveProjectPath(root, evidence.RelativePath);
            }
            catch (InvalidDataException exception)
            {
                diagnostics.Add(exception.Message);
                return Unavailable(backend, owner.ProviderKey, owner.VersionOrRevision, diagnostics);
            }

            if (!File.Exists(evidencePath))
            {
                diagnostics.Add($"Receipt evidence '{evidence.RelativePath}' was not found.");
                return Unavailable(backend, owner.ProviderKey, owner.VersionOrRevision, diagnostics);
            }

            if (IsReparsePointBetween(root, evidencePath))
            {
                diagnostics.Add($"Receipt evidence '{evidence.RelativePath}' traverses a symbolic link or junction.");
                return Unavailable(backend, owner.ProviderKey, owner.VersionOrRevision, diagnostics);
            }

            var actualHash = ComputeSha256(evidencePath);
            if (!string.Equals(actualHash, evidence.Sha256, StringComparison.Ordinal))
            {
                diagnostics.Add(
                    $"Receipt evidence '{evidence.RelativePath}' SHA-256 mismatch: expected {evidence.Sha256}, received {actualHash}.");
                return Unavailable(backend, owner.ProviderKey, owner.VersionOrRevision, diagnostics);
            }
        }

        return new(backend, true,
            string.Join(';', markerEvidence.Select(item => item.RelativePath)),
            owner.VersionOrRevision,
            diagnostics)
        {
            ProviderKey = owner.ProviderKey,
            ReceiptVerified = true,
            ReceiptPath = Path.GetRelativePath(root, receiptPath).Replace('\\', '/'),
        };
    }

    private static IReadOnlyList<ProviderInstallation> ReadReceipt(string receiptPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(receiptPath));
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("schemaVersion", out var schemaVersion) ||
            schemaVersion.ValueKind != JsonValueKind.Number ||
            schemaVersion.GetInt32() != 1)
        {
            throw new InvalidDataException("schemaVersion must be 1.");
        }

        if (!root.TryGetProperty("generatedAtUtc", out var generatedAt) ||
            generatedAt.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParse(generatedAt.GetString(), out _))
        {
            throw new InvalidDataException("generatedAtUtc must be an ISO-8601 timestamp.");
        }

        if (!root.TryGetProperty("providers", out var providers) || providers.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("providers must be an array.");

        var installations = new List<ProviderInstallation>();
        foreach (var provider in providers.EnumerateArray())
        {
            var backend = RequiredString(provider, "backend");
            if (backend is not ("terrain3d" or "pcgodot"))
                throw new InvalidDataException($"Unknown backend '{backend}'.");
            var providerKey = RequiredString(provider, "providerKey");
            var repository = RequiredString(provider, "repository");
            if (!Uri.TryCreate(repository, UriKind.Absolute, out var repositoryUri) ||
                repositoryUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidDataException($"Provider '{providerKey}' repository must use HTTPS.");
            }

            var versionOrRevision = RequiredString(provider, "versionOrRevision");
            if (backend == "pcgodot" && !IsLowerHex(versionOrRevision, 40))
                throw new InvalidDataException($"Provider '{providerKey}' revision must be a lowercase Git commit.");
            if (backend == "pcgodot")
            {
                var treeRevision = RequiredString(provider, "treeRevision");
                if (!IsLowerHex(treeRevision, 40))
                    throw new InvalidDataException($"Provider '{providerKey}' treeRevision must be a lowercase Git tree.");
                if (!provider.TryGetProperty("submodules", out var submodules) ||
                    submodules.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidDataException($"Provider '{providerKey}' submodules must be an array.");
                }
            }
            else
            {
                var sourceArchiveSha256 = RequiredString(provider, "sourceArchiveSha256");
                if (!IsLowerHex(sourceArchiveSha256, 64))
                    throw new InvalidDataException("Terrain3D sourceArchiveSha256 must be a lowercase SHA-256.");
            }

            if (!provider.TryGetProperty("evidence", out var evidenceItems) ||
                evidenceItems.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException($"Provider '{providerKey}' evidence must be an array.");
            }

            var evidence = evidenceItems.EnumerateArray()
                .Select(item => new ProviderEvidence(
                    RequiredString(item, "relativePath").Replace('\\', '/'),
                    RequiredString(item, "sha256")))
                .ToArray();
            if (evidence.Length == 0 || evidence.Any(item => !IsLowerHex(item.Sha256, 64)))
                throw new InvalidDataException($"Provider '{providerKey}' evidence must contain lowercase SHA-256 hashes.");

            installations.Add(new(backend, providerKey, repository, versionOrRevision, evidence));
        }

        return installations;
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new InvalidDataException($"{propertyName} must be a non-empty string.");
        }

        return property.GetString()!;
    }

    private static string ResolveProjectPath(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) ||
            relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
                .Any(segment => segment == ".."))
        {
            throw new InvalidDataException($"Provider evidence path '{relativePath}' is not a safe relative path.");
        }

        var resolved = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Provider evidence path '{relativePath}' escapes the Godot project.");
        return resolved;
    }

    private static bool IsReparsePointBetween(string root, string filePath)
    {
        var relative = Path.GetRelativePath(root, filePath);
        var current = root;
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if ((File.Exists(current) || Directory.Exists(current)) &&
                File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAllowedProvider(WorldGenerationBackend backend, string providerKey) =>
        backend switch
        {
            WorldGenerationBackend.Terrain3D => providerKey == WorldGenerationProviderKeys.Terrain3D,
            WorldGenerationBackend.PcgGodot => providerKey is WorldGenerationProviderKeys.GodotFlow or
                WorldGenerationProviderKeys.GfPcgodot,
            _ => false,
        };

    private static IReadOnlyList<string> MarkerPaths(string providerKey) => providerKey switch
    {
        WorldGenerationProviderKeys.Terrain3D => Terrain3DCandidates.Select(item => item.RelativePath).ToArray(),
        WorldGenerationProviderKeys.GodotFlow or WorldGenerationProviderKeys.GfPcgodot =>
            GraphPcgCandidates.Select(item => item.RelativePath).ToArray(),
        _ => [],
    };

    private static string BackendKey(WorldGenerationBackend backend) => backend switch
    {
        WorldGenerationBackend.Terrain3D => "terrain3d",
        WorldGenerationBackend.PcgGodot => "pcgodot",
        _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, null),
    };

    private static bool IsLowerHex(string value, int length) =>
        value.Length == length && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static WorldGenerationBackendCapability Unavailable(
        WorldGenerationBackend backend,
        string providerKey,
        string versionOrRevision,
        IReadOnlyList<string> diagnostics) =>
        new(backend, false, string.Empty, versionOrRevision, diagnostics)
        {
            ProviderKey = providerKey,
        };

    private static WorldGenerationBackendCapability NativeCapability() =>
        new(WorldGenerationBackend.GodotNative, true,
            "OpenRPG Godot bridge native fallback", "4.7.1", [])
        {
            ProviderKey = WorldGenerationProviderKeys.OpenRpgNative,
        };

    private sealed record BackendMarker(
        string RelativePath,
        string ProviderKey,
        string VersionOrRevision);

    private sealed record ProviderInstallation(
        string Backend,
        string ProviderKey,
        string Repository,
        string VersionOrRevision,
        IReadOnlyList<ProviderEvidence> Evidence);

    private sealed record ProviderEvidence(string RelativePath, string Sha256);
}
