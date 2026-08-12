using Forge.LiveDirector.Audit;
using Forge.LiveDirector.Authentication;
using Forge.LiveDirector.Authoring;
using Forge.LiveDirector.Chat;
using Forge.LiveDirector.Control;
using Forge.LiveDirector.Players;
using Forge.LiveDirector.World;
using Forge.Domain.Quality;
using Forge.Network.Authoring;
using Forge.Network.Identity;
using Forge.Network.Operations;

namespace Forge.LiveDirector.Scenarios;

public sealed record LiveDirectorAccessAcceptanceResult(bool Passed, string AuthenticationCode,
    bool SessionActive, string InspectionCode, int InspectionSections, int Players,
    int RefreshedLatencies, int LatencyMs, int AuditEntries, bool AuditVerified,
    int ChatMessages, string LastChannel, string ProfileDiff, string PermissionCode,
    IReadOnlyList<string> AuditTrail);

public static class LiveDirectorAccessAcceptanceScenario
{
    private static readonly Guid GmAccount = Guid.Parse("a1050000-0000-4000-8000-000000000101");
    private static readonly Guid SessionId = Guid.Parse("a1050000-0000-4000-8000-000000000102");
    private static readonly Guid ZoneId = Guid.Parse("a1050000-0000-4000-8000-000000000103");
    private static readonly Guid CharacterId = Guid.Parse("a1050000-0000-4000-8000-000000000104");
    private static readonly Guid TargetId = Guid.Parse("a1050000-0000-4000-8000-000000000105");

    public static LiveDirectorAccessAcceptanceResult Run()
    {
        var profile = LiveDirectorAccessProfile.CreateDefault("scenario:r105");
        var runtime = LiveDirectorAccessRuntimeAdapter.Build(profile);
        var now = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var accounts = new AccountService();
        var registered = accounts.Register("director-r105", "correct-horse-battery");
        var authentication = new DirectorAuthenticationService(accounts, () => now, () => SessionId,
            TimeSpan.FromMinutes(profile.Authentication.SessionLifetimeMinutes));
        authentication.GrantRole(registered.AccountId, NetworkRole.Administrator);
        var badFactor = authentication.Authenticate(new("director-r105", "correct-horse-battery", "bad"),
            value => value == "246810");
        var session = authentication.Authenticate(new("director-r105", "correct-horse-battery", "246810"),
            value => value == "246810");

        var authorization = new AuthorizationService();
        var gm = new AuthorizationSubject(GmAccount, NetworkRole.Administrator, new HashSet<Guid> { ZoneId });
        var chooser = new LiveWorldChooser(authorization);
        chooser.Upsert(new(CharacterId, ZoneId, "creature", "Elyra", "player", registered.AccountId,
            "active", [], "crypt", false, 4, 0, 9));
        chooser.Upsert(new(TargetId, ZoneId, "creature", "Gardien des braises", "ember", null,
            "active", [], "crypt", false, 8, 0, 8));

        var control = new LiveDirectorControlService(chooser, authorization, () => now);
        control.SetMetadata(TargetId, new(
            new Dictionary<string, string> { ["combat"] = "enabled" },
            new Dictionary<string, decimal> { ["health"] = 42 },
            new Dictionary<string, string> { ["quest.stage"] = "2" },
            ["guardian.forge"], null, ["spawned", "alerted"]));
        var inspection = control.Execute(gm, new(Guid.NewGuid(), "director.inspect", TargetId, 0,
            TimeSpan.Zero, new Dictionary<string, string>()), true);

        var players = new LivePlayerService(chooser, authorization);
        players.Upsert(new(registered.AccountId, CharacterId, "karen", "Elyra", ZoneId, 42, null,
            "player", "connected", ["none"]));
        var refreshed = players.RefreshLatencies(gm, _ => 37);
        var roster = players.List(gm);

        var audit = new LiveAuditJournal(() => now);
        audit.Append(GmAccount, "director.inspect", [TargetId], "unread", "read-only metadata",
            "session oversight", Guid.Parse("a1050000-0000-4000-8000-000000000106"));
        var chat = new LiveDirectorChatService(authorization, audit, () => now);
        chat.Send(gm, "gm", null, "Briefing de session", null,
            Guid.Parse("a1050000-0000-4000-8000-000000000107"));
        chat.Send(gm, "impersonation", null, "Approchez", TargetId,
            Guid.Parse("a1050000-0000-4000-8000-000000000108"), "dramatic scene");

        var history = new LiveDirectorAccessHistory(profile);
        var candidate = profile with
        {
            ServerRevision = 1,
            Players = profile.Players with { RefreshSeconds = 3 }
        };
        var preview = history.Execute(new(Guid.NewGuid(), 1, "scenario:r105", 0, "server:gm",
            NetworkRole.Administrator, NetworkAuthorityScope.Server, false, candidate));
        var applied = history.Execute(new(Guid.NewGuid(), 1, "scenario:r105", 0, "server:gm",
            NetworkRole.Administrator, NetworkAuthorityScope.Server, true, candidate));
        var denied = history.Execute(new(Guid.NewGuid(), 1, "scenario:r105", 1, "client:player",
            NetworkRole.Player, NetworkAuthorityScope.Server, true, candidate with { ServerRevision = 2 }));
        var auditTrail = new[] { badFactor.AuditEntry, session.AuditEntry, inspection.AuditEntry,
            preview.AuditEntry, applied.AuditEntry, denied.AuditEntry };

        var inspectionSections = inspection.Inspection is null ? 0 :
            3 + (inspection.Inspection.Scripts.Count > 0 ? 1 : 0) +
            (profile.Inspection.ShowOwnership ? 1 : 0) + (inspection.Inspection.History.Count > 0 ? 1 : 0);
        var passed = badFactor.Code == "LIVE_GM_SECOND_FACTOR_INVALID" && session.Authenticated &&
            authentication.IsActive(session) && inspection.Code == "LIVE_INSPECTION_READY" &&
            !inspection.Applied && inspectionSections == 6 && roster.Count == 1 && refreshed == 1 &&
            roster[0].LatencyMs == 37 && audit.Entries.Count == 3 && audit.Verify() &&
            chat.Messages.Count == 2 && chat.Messages[^1].Channel == "impersonation" &&
            preview.Succeeded && !preview.Applied && applied.Applied &&
            denied.Diagnostics.Any(item => item.Code == "LIVE_ACCESS_PERMISSION_DENIED") &&
            runtime.Scripts.Read(runtime.Scripts.Handle).ChatChannels == 7 &&
            auditTrail.All(item => !string.IsNullOrWhiteSpace(item));
        return new(passed, session.Code, authentication.IsActive(session), inspection.Code,
            inspectionSections, roster.Count, refreshed, roster[0].LatencyMs, audit.Entries.Count,
            audit.Verify(), chat.Messages.Count, chat.Messages[^1].Channel, applied.SemanticDiff,
            denied.Diagnostics.FirstOrDefault()?.Code ?? "none", auditTrail);
    }
}
