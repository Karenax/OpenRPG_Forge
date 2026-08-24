using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.EngineAdapter.Assets;
using Forge.EngineAdapter.Rendering;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorldViewport
{
    private const string CryptAreaSlug = "crypt-of-ashes";
    private const string FallbackPreviewNodeName = "FallbackPreview";
    private bool _useCryptVisualFallbacks;
    private bool _cryptArtPassLoaded;
    private int _cryptArtModelInstanceCount;
    private int _cryptCharacterAssetInstanceCount;
    private int _hiddenCryptArchitectureProxyCount;

    internal bool CryptArtPassLoaded => _cryptArtPassLoaded;
    internal int CryptArtModelInstanceCount => _cryptArtModelInstanceCount;
    internal int CryptCharacterAssetInstanceCount => _cryptCharacterAssetInstanceCount;
    internal int HiddenCryptArchitectureProxyCount => _hiddenCryptArchitectureProxyCount;

    private void PrepareCryptVisuals(StudioSessionController session, AreaDefinition area)
    {
        ClearCryptVisualState();
        _useCryptVisualFallbacks = IsCryptArea(area);
        if (!_useCryptVisualFallbacks || _areaRoot is null) return;

        var artRoot = ResolveCryptArtRoot(session.Session!.RootDirectory);
        if (artRoot is null)
        {
            GD.PushWarning(
                "OPENRPG_FORGE_STUDIO_CRYPT_ART_FALLBACK reason=art_root_missing " +
                $"workspace={session.Session.RootDirectory}");
            return;
        }

        try
        {
            var library = new GodotExternalGltfModelLibrary(artRoot);
            var artPass = GodotCryptArtKitBuilder.Build(library.Instantiate);
            _areaRoot.AddChild(artPass.Root);
            _cryptArtPassLoaded = true;
            _cryptArtModelInstanceCount = artPass.ModelInstances;
            GD.Print(
                $"OPENRPG_FORGE_STUDIO_CRYPT_ART_PASS models={artPass.ModelInstances} " +
                $"floors={artPass.FloorTiles} walls={artPass.WallSegments} props={artPass.Props} " +
                $"zones={artPass.Zones} source=external-glb");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            GD.PushWarning(
                $"OPENRPG_FORGE_STUDIO_CRYPT_ART_FALLBACK reason={exception.GetType().Name} " +
                $"message={exception.Message}");
        }
    }

    private void ClearCryptVisualState()
    {
        _useCryptVisualFallbacks = false;
        _cryptArtPassLoaded = false;
        _cryptArtModelInstanceCount = 0;
        _cryptCharacterAssetInstanceCount = 0;
        _hiddenCryptArchitectureProxyCount = 0;
    }

    private CryptCharacterVisualDescriptor? ResolveCryptCharacterVisual(EntityInstance entity)
    {
        return _useCryptVisualFallbacks ? CryptCharacterVisualResolver.Resolve(entity.Tags) : null;
    }

    private void RecordCryptCharacterAssetInstance(bool resolvedFromCryptTags)
    {
        if (resolvedFromCryptTags) _cryptCharacterAssetInstanceCount++;
    }

    private void ConfigureCryptArchitectureProxy(Node3D node, EntityInstance entity, BlueprintDefinition blueprint)
    {
        if (!_cryptArtPassLoaded ||
            blueprint.Kind != BlueprintKind.Placeable ||
            !entity.Tags.Contains("architecture", StringComparer.OrdinalIgnoreCase))
            return;
        var fallbackPreview = node.GetNodeOrNull<MeshInstance3D>(FallbackPreviewNodeName);
        if (fallbackPreview is null) return;

        // Keep the authored entity root and fallback mesh bounds for selection/manipulation, but do
        // not draw the coarse box proxy on top of the final shared art pass.
        fallbackPreview.Visible = false;
        _hiddenCryptArchitectureProxyCount++;
    }

    private static bool IsCryptArea(AreaDefinition area)
    {
        return string.Equals(area.Slug, CryptAreaSlug, StringComparison.OrdinalIgnoreCase) ||
               area.Environment.Tags.Contains("crypt", StringComparer.OrdinalIgnoreCase);
    }

    private static string? ResolveCryptArtRoot(string workspaceRoot)
    {
        var canonicalWorkspace = Path.GetFullPath(workspaceRoot);
        var candidates = new List<string>
        {
            Path.Combine(canonicalWorkspace, "art", "crypt-kaykit"),
        };
        var contentRoot = Directory.GetParent(canonicalWorkspace);
        var runtimeRoot = contentRoot?.Parent;
        if (runtimeRoot is not null)
            candidates.Add(Path.Combine(runtimeRoot.FullName, "art", "crypt-kaykit"));

        foreach (var candidate in candidates.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))
            if (Directory.Exists(candidate))
                return candidate;
        return null;
    }
}
