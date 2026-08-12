using Forge.Domain.Assets;

namespace Forge.EngineAdapter.Rendering;

public sealed record VfxProviderCapabilities(
    string Provider,
    bool Particles,
    bool Decals,
    bool Lights,
    bool AudioCues);

public sealed record AssetVfxRenderRequest(
    AssetVfxManifest Manifest,
    decimal ViewerDistance,
    int RequestedParticles,
    int RequestedDrawCalls,
    bool IncludeDecal,
    bool IncludeLight,
    bool IncludeAudioCue);

public sealed record AssetVfxRenderPlan(
    bool Valid,
    bool Culled,
    string DiagnosticCode,
    string Provider,
    int ParticleCount,
    int DrawCalls,
    bool Decal,
    bool Light,
    bool AudioCue);

public interface IAssetVfxRenderProvider
{
    VfxProviderCapabilities Capabilities { get; }
}

public static class AssetVfxRenderPlanBuilder
{
    public static AssetVfxRenderPlan Build(AssetVfxRenderRequest request, IAssetVfxRenderProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Manifest);
        if (provider is null)
        {
            return request.Manifest.Required
                ? Reject(request.Manifest.Provider, "asset.vfx.provider_required")
                : new(true, true, "asset.vfx.provider_optional_absent", request.Manifest.Provider, 0, 0,
                    false, false, false);
        }

        var capabilities = provider.Capabilities;
        if (!string.Equals(capabilities.Provider, request.Manifest.Provider, StringComparison.OrdinalIgnoreCase))
            return Reject(request.Manifest.Provider, "asset.vfx.provider_mismatch");
        if (request.ViewerDistance > request.Manifest.CullDistance)
            return new(true, true, "asset.vfx.culled", capabilities.Provider, 0, 0, false, false, false);
        if (request.RequestedParticles < 0 || request.RequestedDrawCalls < 0 ||
            request.RequestedParticles > request.Manifest.MaximumParticles ||
            request.RequestedDrawCalls > request.Manifest.MaximumDrawCalls)
            return Reject(capabilities.Provider, "asset.vfx.budget_exceeded");
        if ((request.RequestedParticles > 0 && !capabilities.Particles) ||
            (request.IncludeDecal && !capabilities.Decals) ||
            (request.IncludeLight && !capabilities.Lights) ||
            (request.IncludeAudioCue && !capabilities.AudioCues))
            return Reject(capabilities.Provider, "asset.vfx.capability_missing");

        return new(true, false, "asset.vfx.render_ready", capabilities.Provider,
            request.RequestedParticles, request.RequestedDrawCalls,
            request.IncludeDecal, request.IncludeLight, request.IncludeAudioCue);
    }

    private static AssetVfxRenderPlan Reject(string provider, string code) =>
        new(false, false, code, provider, 0, 0, false, false, false);
}
