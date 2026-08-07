using Forge.Domain.RuntimeExtensions;

namespace Forge.Network.Production;

public sealed class ServerProductionService(NetworkProductionProfile profile)
{
    public sealed record ReloadRequest(Guid CommandId, string ShardKey, int PackageMegabytes,
        int ActivePlayers, long ExpectedRevision, string PackageFingerprint);

    public sealed record ReloadResult(bool Applied, string Code, long Revision, string SemanticDiff,
        bool RollbackAvailable, string AuditEntry);

    private readonly HashSet<Guid> _processed = [];
    private long _revision;

    public PostLaunchCapabilityResolver.ProtocolDecision Negotiate(string platform, int minimumVersion,
        int maximumVersion, IReadOnlyList<string> capabilities, bool signedExtension) =>
        PostLaunchCapabilityResolver.NegotiateProtocol(profile, platform, minimumVersion, maximumVersion,
            capabilities, signedExtension);

    public ReloadResult Reload(ReloadRequest request, bool apply)
    {
        if (request.CommandId == Guid.Empty) return Reject("NET_PRODUCTION_COMMAND_INVALID", request);
        if (request.ExpectedRevision != _revision) return Reject("NET_PRODUCTION_REVISION_STALE", request);
        if (_processed.Contains(request.CommandId)) return Reject("NET_PRODUCTION_COMMAND_REPLAYED", request);
        if (request.PackageFingerprint.Length != 64 || request.PackageFingerprint.Any(character => !Uri.IsHexDigit(character)))
            return Reject("NET_PRODUCTION_FINGERPRINT_INVALID", request);
        var decision = PostLaunchCapabilityResolver.PlanShardReload(profile, request.ShardKey,
            request.PackageMegabytes, request.ActivePlayers);
        if (!decision.Accepted) return Reject(decision.Diagnostic, request);
        var diff = $"hot-reload shard={decision.ShardKey};package={request.PackageFingerprint.ToLowerInvariant()};" +
                   $"size={request.PackageMegabytes}MB;atomic={decision.AtomicSwap};rollback={decision.Rollback};drain={decision.DrainTimeoutSeconds}s";
        if (!apply) return new(false, "NET_PRODUCTION_PREVIEW", _revision, diff, decision.Rollback, $"previewed:{diff}");
        _processed.Add(request.CommandId);
        _revision++;
        return new(true, "NET_PRODUCTION_APPLIED", _revision, diff, decision.Rollback,
            $"applied:{diff}:revision={_revision}");
    }

    private ReloadResult Reject(string code, ReloadRequest request) =>
        new(false, code, _revision, "no changes", false, $"rejected:{code}:command={request.CommandId}");
}
