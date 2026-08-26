using Forge.Domain.WorldGeneration;

namespace Forge.EngineAdapter.WorldGeneration;

public sealed record WorldGenerationBackendCapability(
    WorldGenerationBackend Backend,
    bool Available,
    string Evidence,
    string VersionOrRevision,
    IReadOnlyList<string> Diagnostics);

public static class WorldGenerationBackendCapabilityProbe
{
    private static readonly string[] Terrain3DCandidates =
    [
        "addons/terrain_3d/plugin.cfg",
        "addons/terrain_3d/terrain.gdextension",
    ];

    private static readonly string[] PcgGodotCandidates =
    [
        "addons/flow_nodes_editor/plugin.cfg",
        "addons/pcgodot/plugin.cfg",
        "addons/godot_flow/plugin.cfg",
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
            Probe(root, WorldGenerationBackend.Terrain3D, Terrain3DCandidates, "v1.0.2-stable", diagnostics),
            Probe(root, WorldGenerationBackend.PcgGodot, PcgGodotCandidates,
                "284231f060099a8943eb56567c21732b7cd678d3", diagnostics),
            new(WorldGenerationBackend.GodotNative, true,
                "Forge.EngineAdapter native terrain, biome-scatter and navigation builders", "4.7.1", []),
        ];
    }

    private static WorldGenerationBackendCapability Probe(
        string root,
        WorldGenerationBackend backend,
        IEnumerable<string> candidates,
        string version,
        IReadOnlyList<string> sharedDiagnostics)
    {
        var evidence = candidates
            .Select(candidate => (Relative: candidate, Full: Path.Combine(root, candidate.Replace('/', Path.DirectorySeparatorChar))))
            .FirstOrDefault(candidate => File.Exists(candidate.Full));
        return evidence == default
            ? new(backend, false, string.Empty, version,
                sharedDiagnostics.Concat([$"No {backend} addon marker was found."]).ToArray())
            : new(backend, true, evidence.Relative, version, sharedDiagnostics.ToArray());
    }
}
