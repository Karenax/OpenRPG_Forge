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
}

public static class WorldGenerationBackendCapabilityProbe
{
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
        new("addons/pcgodot/plugin.cfg", WorldGenerationProviderKeys.GfPcgodot, "unqualified"),
    ];

    public static IReadOnlyList<WorldGenerationBackendCapability> Probe(string godotProjectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(godotProjectRoot);
        var root = Path.GetFullPath(godotProjectRoot);
        var diagnostics = new List<string>();
        if (!File.Exists(Path.Combine(root, "project.godot")))
            diagnostics.Add("project.godot was not found; addon discovery is filesystem-only.");
        return
        [
            Probe(root, WorldGenerationBackend.Terrain3D, Terrain3DCandidates, diagnostics),
            Probe(root, WorldGenerationBackend.PcgGodot, GraphPcgCandidates, diagnostics),
            new(WorldGenerationBackend.GodotNative, true,
                "OpenRPG Godot bridge native fallback", "4.7.1", [])
            {
                ProviderKey = WorldGenerationProviderKeys.OpenRpgNative,
            },
        ];
    }

    private static WorldGenerationBackendCapability Probe(
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
            return new(backend, false, string.Empty, expected.VersionOrRevision,
                sharedDiagnostics.Concat([$"No {backend} addon marker was found."]).ToArray())
            {
                ProviderKey = expected.ProviderKey,
            };
        }

        var selected = available[0];
        var diagnostics = sharedDiagnostics.ToList();
        if (available.Length > 1)
        {
            diagnostics.Add(
                $"Multiple {backend} providers were detected; '{selected.ProviderKey}' was selected by marker precedence.");
        }

        return new(backend, true, selected.RelativePath, selected.VersionOrRevision, diagnostics)
        {
            ProviderKey = selected.ProviderKey,
        };
    }

    private sealed record BackendMarker(
        string RelativePath,
        string ProviderKey,
        string VersionOrRevision);
}
