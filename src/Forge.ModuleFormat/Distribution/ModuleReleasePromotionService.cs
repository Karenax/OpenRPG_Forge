using Forge.Domain.Modules;

namespace Forge.ModuleFormat.Distribution;

public sealed record ModuleCompatibilityEvidence(
    string FromVersion,
    string ToVersion,
    ModuleTargetKind Target,
    ModuleDistributionPlatform Platform,
    bool CleanInstallPassed,
    bool UpgradePassed,
    bool RollbackPassed,
    string PackageSha256);

public sealed record ModuleReleasePromotionRequest(
    ModuleReleaseConfigurationId ReleaseId,
    ModuleReleaseChannel FromChannel,
    ModuleReleaseChannel ToChannel,
    IReadOnlyList<ModuleTargetKind> Targets,
    IReadOnlyList<ModuleDistributionPlatform> Platforms,
    bool SignatureTrusted,
    bool RemotePublisherConfigured,
    IReadOnlyList<ModuleCompatibilityEvidence> CompatibilityEvidence);

public sealed record ModuleReleasePromotionResult(
    bool Allowed,
    string DiagnosticCode,
    string Message,
    IReadOnlyList<string> MissingMatrixEntries,
    string AuditSummary);

public static class ModuleReleasePromotionService
{
    public static ModuleReleasePromotionResult Preview(ModuleReleasePromotionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsNextChannel(request.FromChannel, request.ToChannel))
        {
            return Reject("ORF-BUILD-024-CHANNEL", "Release promotion must advance exactly one channel.");
        }

        if (!request.RemotePublisherConfigured)
        {
            return Reject("ORF-BUILD-024-PUBLISHER", "A remote publisher adapter must be configured.");
        }

        if (request.ToChannel == ModuleReleaseChannel.Stable && !request.SignatureTrusted)
        {
            return Reject("ORF-BUILD-024-SIGNATURE", "Stable promotion requires a trusted signature.");
        }

        var missing = new List<string>();
        foreach (var target in request.Targets.Distinct().Order())
            foreach (var platform in request.Platforms.Distinct().Order())
            {
                var evidence = request.CompatibilityEvidence.SingleOrDefault(item =>
                    item.Target == target && item.Platform == platform);
                if (evidence is null || !evidence.CleanInstallPassed || !evidence.UpgradePassed ||
                    !evidence.RollbackPassed || evidence.PackageSha256.Length != 64)
                {
                    missing.Add($"{target}/{platform}");
                }
            }

        if (missing.Count > 0)
        {
            return new(false, "ORF-BUILD-024-COMPATIBILITY",
                "Compatibility evidence is incomplete.", missing,
                $"release={request.ReleaseId};from={request.FromChannel};to={request.ToChannel};missing={string.Join(',', missing)}");
        }

        return new(true, "ORF-BUILD-024-PROMOTION-READY", "Promotion prerequisites passed.", [],
            $"release={request.ReleaseId};from={request.FromChannel};to={request.ToChannel};matrix={request.Targets.Count}x{request.Platforms.Count};publisher=adapter");
    }

    private static bool IsNextChannel(ModuleReleaseChannel from, ModuleReleaseChannel to) =>
        (from, to) is (ModuleReleaseChannel.Development, ModuleReleaseChannel.Preview) or
            (ModuleReleaseChannel.Preview, ModuleReleaseChannel.Stable);

    private static ModuleReleasePromotionResult Reject(string code, string message) =>
        new(false, code, message, [], $"rejected={code}");
}
