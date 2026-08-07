using Forge.Domain.RuntimeExtensions;
using Forge.LiveDirector.World;
using Forge.Network.Operations;

namespace Forge.LiveDirector.Production;

public sealed class LiveProductionService(
    LiveWorldChooser chooser,
    AuthorizationService authorization,
    LiveDirectorProductionProfile profile,
    Func<DateTimeOffset> utcNow,
    Func<Guid>? idSource = null)
{
    public sealed record InterventionRequest(Guid CommandId, string Kind, IReadOnlyList<Guid> TargetIds,
        long ExpectedRevision, string Reason, int LevelDelta = 0, float Scale = 1, bool Confirmed = false);

    public sealed record InterventionResult(bool Applied, string Code, long Revision,
        IReadOnlyList<Guid> TargetIds, string SemanticDiff, string AuditEntry);

    public sealed record RecordingResult(bool Started, string Code, Guid RecordingId,
        IReadOnlyList<Guid> Members, string ExportFormat, bool Redacted, string AuditEntry);

    private readonly HashSet<Guid> _processed = [];
    private long _revision;

    public InterventionResult Execute(AuthorizationSubject actor, InterventionRequest request, bool apply)
    {
        if (request.CommandId == Guid.Empty) return Reject("LIVE_PRODUCTION_COMMAND_INVALID", request);
        if (request.ExpectedRevision != _revision) return Reject("LIVE_PRODUCTION_REVISION_STALE", request);
        if (_processed.Contains(request.CommandId)) return Reject("LIVE_PRODUCTION_COMMAND_REPLAYED", request);
        var targets = request.TargetIds.Distinct().Select(chooser.Find).ToArray();
        if (targets.Length == 0 || targets.Any(item => item is null)) return Reject("LIVE_PRODUCTION_TARGET_UNKNOWN", request);
        if (targets.Any(item => !authorization.Authorize(actor, NetworkCapability.ManageWorld, item!.ZoneId).Allowed))
            return Reject("LIVE_PRODUCTION_PERMISSION_DENIED", request);
        if (string.IsNullOrWhiteSpace(request.Reason)) return Reject("LIVE_PRODUCTION_REASON_REQUIRED", request);
        if (request.Kind is "adjust-level" or "appearance" && !request.Confirmed)
            return Reject("LIVE_PRODUCTION_CONFIRMATION_REQUIRED", request);
        var decision = PostLaunchCapabilityResolver.PlanDirectorIntervention(profile, request.Kind,
            targets.Length, request.LevelDelta, request.Scale);
        if (!decision.Accepted) return Reject(decision.Diagnostic, request);
        var audit = $"{(apply ? "applied" : "previewed")}:{decision.SemanticDiff};actor={actor.AccountId};reason={request.Reason.Trim()};at={utcNow():O}";
        if (!apply)
            return new(false, "LIVE_PRODUCTION_PREVIEW", _revision, request.TargetIds, decision.SemanticDiff, audit);
        foreach (var target in targets.Cast<LiveWorldChooser.Entry>())
        {
            var state = request.Kind switch
            {
                "force-rest" => "rest:completed:director",
                "adjust-level" => $"level-adjusted:{request.LevelDelta};snapshot={decision.SnapshotRequired}",
                "appearance" => $"appearance-scale:{request.Scale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}",
                _ => target.State,
            };
            chooser.Upsert(target with { State = state });
        }
        _processed.Add(request.CommandId);
        _revision++;
        return new(true, "LIVE_PRODUCTION_APPLIED", _revision, request.TargetIds, decision.SemanticDiff, audit);
    }

    public RecordingResult StartRecording(AuthorizationSubject actor, int groupHotkey,
        IReadOnlyDictionary<string, IReadOnlyList<Guid>> queryResults)
    {
        if (!authorization.Authorize(actor, NetworkCapability.ViewOperations).Allowed)
            return new(false, "LIVE_RECORDING_PERMISSION_DENIED", Guid.Empty, [],
                profile.SessionRecording.ExportFormat, true, $"rejected:actor={actor.AccountId}");
        var decision = PostLaunchCapabilityResolver.ResolveRecording(profile, groupHotkey, queryResults);
        if (!decision.Accepted)
            return new(false, decision.Diagnostic, Guid.Empty, decision.Members, decision.ExportFormat,
                decision.RedactPrivateData, $"rejected:{decision.Diagnostic}:actor={actor.AccountId}");
        var recordingId = (idSource ?? Guid.NewGuid)();
        if (recordingId == Guid.Empty)
            return new(false, "LIVE_RECORDING_ID_INVALID", Guid.Empty, decision.Members, decision.ExportFormat,
                decision.RedactPrivateData, $"rejected:recording-id-empty:actor={actor.AccountId}");
        return new(true, "LIVE_RECORDING_STARTED", recordingId, decision.Members, decision.ExportFormat,
            decision.RedactPrivateData,
            $"started:recording={recordingId}:members={decision.Members.Count}:redacted={decision.RedactPrivateData}:actor={actor.AccountId}:at={utcNow():O}");
    }

    private InterventionResult Reject(string code, InterventionRequest request) =>
        new(false, code, _revision, request.TargetIds, "no changes", $"rejected:{code}:command={request.CommandId}");
}
