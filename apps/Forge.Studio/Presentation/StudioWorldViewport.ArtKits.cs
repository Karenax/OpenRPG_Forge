using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.EngineAdapter.Assets;
using Forge.EngineAdapter.Rendering;
using Forge.Studio.Session;
using Godot;
using System.Text.Json;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorldViewport
{
    private const string FallbackPreviewNodeName = "FallbackPreview";
    private bool _artKitPassLoaded;
    private int _artKitModelInstanceCount;
    private int _hiddenArchitectureProxyCount;

    internal bool ArtKitPassLoaded => _artKitPassLoaded;
    internal int ArtKitModelInstanceCount => _artKitModelInstanceCount;
    internal int HiddenArchitectureProxyCount => _hiddenArchitectureProxyCount;

    private void PrepareAuthoredArtKit(StudioSessionController session, AreaDefinition area)
    {
        ClearArtKitState();
        var artKitId = ResolveArtKitId(area);
        if (artKitId is null || _areaRoot is null) return;

        var artRoot = ResolveArtRoot(session.Session!.RootDirectory, artKitId);
        var layoutPath = ResolveArtLayoutPath(session.Session.RootDirectory, artKitId);
        if (artRoot is null || layoutPath is null)
        {
            GD.PushWarning(
                $"OPENRPG_FORGE_STUDIO_ART_KIT_FALLBACK id={artKitId} reason=content_missing " +
                $"workspace={session.Session.RootDirectory}");
            return;
        }

        try
        {
            var library = new GodotExternalGltfModelLibrary(artRoot);
            var layout = GodotArtKitLayoutJson.Deserialize(File.ReadAllText(layoutPath));
            if (!string.Equals(layout.Id, artKitId, StringComparison.Ordinal))
                throw new InvalidDataException(
                    $"Art-kit layout '{layout.Id}' does not match requested kit '{artKitId}'.");
            var artPass = GodotArtKitBuilder.Build(layout, library.Instantiate);
            _areaRoot.AddChild(artPass.Root);
            _artKitPassLoaded = true;
            _artKitModelInstanceCount = artPass.ModelInstances;
            GD.Print(
                $"OPENRPG_FORGE_STUDIO_ART_KIT_PASS id={artKitId} models={artPass.ModelInstances} " +
                $"floors={artPass.FloorTiles} walls={artPass.WallSegments} props={artPass.Props} " +
                $"zones={artPass.Zones} source=external-glb");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or
                JsonException)
        {
            GD.PushWarning(
                $"OPENRPG_FORGE_STUDIO_ART_KIT_FALLBACK id={artKitId} reason={exception.GetType().Name} " +
                $"message={exception.Message}");
        }
    }

    private void ClearArtKitState()
    {
        _artKitPassLoaded = false;
        _artKitModelInstanceCount = 0;
        _hiddenArchitectureProxyCount = 0;
    }

    private void ConfigureArtKitArchitectureProxy(Node3D node, EntityInstance entity, BlueprintDefinition blueprint)
    {
        if (!_artKitPassLoaded ||
            blueprint.Kind != BlueprintKind.Placeable ||
            !entity.Tags.Contains("architecture", StringComparer.OrdinalIgnoreCase))
            return;
        var fallbackPreview = node.GetNodeOrNull<MeshInstance3D>(FallbackPreviewNodeName);
        if (fallbackPreview is null) return;

        // Keep the authored entity root and fallback mesh bounds for selection/manipulation, but do
        // not draw the coarse box proxy on top of the final shared art pass.
        fallbackPreview.Visible = false;
        _hiddenArchitectureProxyCount++;
    }

    private static string? ResolveArtKitId(AreaDefinition area) => area.Environment.Tags
        .FirstOrDefault(tag => tag.StartsWith("art-kit:", StringComparison.OrdinalIgnoreCase))?["art-kit:".Length..];

    private static string? ResolveArtRoot(string workspaceRoot, string artKitId)
    {
        if (!IsSafeArtKitId(artKitId)) return null;
        var canonicalWorkspace = Path.GetFullPath(workspaceRoot);
        var candidates = new List<string>
        {
            Path.Combine(canonicalWorkspace, "art", artKitId),
        };
        var contentRoot = Directory.GetParent(canonicalWorkspace);
        var runtimeRoot = contentRoot?.Parent;
        if (runtimeRoot is not null)
        {
            candidates.Add(Path.Combine(runtimeRoot.FullName, "art", artKitId));
        }

        foreach (var candidate in candidates.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))
            if (Directory.Exists(candidate))
                return candidate;
        return null;
    }

    private static string? ResolveArtLayoutPath(string workspaceRoot, string artKitId)
    {
        if (!IsSafeArtKitId(artKitId)) return null;
        var root = Path.GetFullPath(workspaceRoot);
        var path = Path.GetFullPath(Path.Combine(root, "art-kits", $"{artKitId}.layout.json"));
        return path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && File.Exists(path)
            ? path
            : null;
    }

    private static bool IsSafeArtKitId(string artKitId) =>
        !string.IsNullOrWhiteSpace(artKitId) &&
        string.Equals(Path.GetFileName(artKitId), artKitId, StringComparison.Ordinal) &&
        artKitId.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
}
