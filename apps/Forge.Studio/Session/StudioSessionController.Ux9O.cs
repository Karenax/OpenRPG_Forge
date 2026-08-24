using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Domain.Quality;
using Forge.ModuleFormat.Network;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Authoring;
using Forge.Network.Configuration;
using Forge.Network.Observability;
using Forge.Network.Operations;
using Forge.Network.Security;
using Forge.Network.Sessions;
using Forge.Network.World;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux9OActor = "studio.network.closure";
    private NetworkAssuranceHistory? _networkAssuranceHistoryUx9O;
    private NetworkAssuranceProfile? _persistedNetworkAssuranceUx9O;
    private string? _networkAssuranceWorkspaceRootUx9O;
    private string? _networkAssuranceLoadMessageUx9O;
    private bool _networkAssuranceFileMissingUx9O;
    private StudioUx9ONetworkClosureDraft? _approvedNetworkClosureUx9O;
    private string? _approvedNetworkClosureFingerprintUx9O;
    private StudioUx9ONetworkClosureProbe _networkClosureProbeUx9O = EmptyProbeUx9O();
    private readonly List<string> _networkClosureAuditUx9O = [];
    private AppliedGroupsUx9O _appliedGroupsUx9O;
    private AppliedGroupsUx9O _undoneGroupsUx9O;

    internal NetworkAssuranceProfile? CurrentNetworkAssuranceUx9O =>
        EnsureNetworkAssuranceUx9O() ? _networkAssuranceHistoryUx9O!.Current : null;
    internal StudioUx9ONetworkClosureProbe NetworkClosureProbeUx9O => _networkClosureProbeUx9O;
    internal IReadOnlyList<string> NetworkClosureAuditUx9O => _networkClosureAuditUx9O.ToArray();

    internal StudioUx9ONetworkClosureDraft? CreateNetworkClosureDraftUx9O(
        bool explicitlySelected = false)
    {
        if (!explicitlySelected || !EnsureNetworkFoundationUx9A() ||
            !EnsureNetworkExperienceUx8M() || !EnsureNetworkAssuranceUx9O()) return null;
        var foundation = _networkFoundationHistoryUx9A!.Current;
        var player = _networkExperienceHistoryUx8M!.Current;
        var assurance = _networkAssuranceHistoryUx9O!.Current;
        var sink = assurance.Logs.RemoteSinks.First();
        return new(foundation.ServerRevision, FingerprintUx9O(foundation),
            player.ServerRevision, FingerprintUx9O(player), assurance.ServerRevision,
            FingerprintUx9O(assurance), foundation.Server.Name, foundation.Server.BindAddress,
            foundation.Server.Port, foundation.Server.Capacity, foundation.RequiredAtRuntime,
            foundation.Dedicated.ServiceMode, foundation.Dedicated.HealthCheckPath,
            player.Combat.PvpEnabled, player.Combat.FriendlyFire,
            player.Combat.GlobalPolicy, player.Combat.AreaOverrides,
            player.Combat.PartyOverrides, player.Combat.ConsentRequired,
            player.Combat.DuelsEnabled, player.Combat.CrimeEnabled,
            player.Combat.SafeZonesEnabled, player.Combat.ServerOverride,
            assurance.Logs.MinimumSeverity, string.Join(", ", assurance.Logs.Categories),
            assurance.Logs.PlayerPrivacy, assurance.Logs.OutputFormat,
            assurance.Logs.RetentionDays, assurance.Logs.MaximumEntries,
            assurance.Logs.RotationBytes, assurance.Logs.RotationFiles, sink.Name,
            sink.Kind, sink.Endpoint, sink.Enabled, sink.Required,
            assurance.AntiCheat.MaximumUnitsPerSecond,
            assurance.AntiCheat.MaximumIntentsPerSecond,
            assurance.AntiCheat.AnomalyThreshold, assurance.AntiCheat.RequireSignedContent,
            string.Join(", ", assurance.AntiCheat.TrustedSignerFingerprints),
            assurance.AntiCheat.AuthoritativeRandom, assurance.AntiCheat.TrustClientSaves,
            assurance.AntiCheat.RandomSeed);
    }

    internal StudioUx9ONetworkClosurePreview PreviewNetworkClosureUx9O(
        StudioUx9ONetworkClosureDraft draft, NetworkRole role = NetworkRole.Administrator)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelNetworkClosureApprovalUx9O();
        if (!TryBuildCandidatesUx9O(draft, out var normalized, out var foundation,
                out var player, out var assurance, out var error))
            return FailedPreviewUx9O(error);

        var currentFoundation = _networkFoundationHistoryUx9A!.Current;
        var currentPlayer = _networkExperienceHistoryUx8M!.Current;
        var currentAssurance = _networkAssuranceHistoryUx9O!.Current;
        var foundationResult = _networkFoundationHistoryUx9A.Execute(new(Guid.NewGuid(), 1,
            "studio:ux9-o", currentFoundation.ServerRevision, Ux9OActor, role, false,
            foundation!));
        var playerResult = _networkExperienceHistoryUx8M.Execute(new(Guid.NewGuid(), 1,
            "studio:ux9-o", currentPlayer.ServerRevision, Ux9OActor, role, false, player!));
        var assuranceResult = _networkAssuranceHistoryUx9O.Execute(new(Guid.NewGuid(), 1,
            "studio:ux9-o", currentAssurance.ServerRevision, Ux9OActor, role, false,
            assurance!));
        var diagnostics = foundationResult.Diagnostics.Concat(playerResult.Diagnostics)
            .Concat(assuranceResult.Diagnostics).ToArray();
        if (!foundationResult.Succeeded || !playerResult.Succeeded ||
            !assuranceResult.Succeeded || foundationResult.Applied || playerResult.Applied ||
            assuranceResult.Applied)
            return FailedPreviewUx9O(HumanDiagnosticsUx9O(diagnostics), diagnostics);

        StudioUx9ONetworkClosureProbe probe;
        try
        {
            probe = RunNetworkClosureProbeUx9O(foundation!, player!, assurance!);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                               InvalidDataException or UriFormatException)
        {
            return FailedPreviewUx9O($"La sandbox locale a refusé le profil : {exception.Message}");
        }

        var groups = ChangedGroupsUx9O(foundationResult.SemanticDiff,
            playerResult.SemanticDiff, assuranceResult.SemanticDiff);
        var changes = HumanChangesUx9O(foundationResult.SemanticDiff,
            playerResult.SemanticDiff, assuranceResult.SemanticDiff);
        var runtimeReady = ProbeReadyUx9O(probe);
        var canApply = groups != AppliedGroupsUx9O.None && runtimeReady;
        if (canApply)
        {
            _approvedNetworkClosureUx9O = normalized;
            _approvedNetworkClosureFingerprintUx9O = DraftFingerprintUx9O(normalized!);
        }
        _networkClosureProbeUx9O = probe;
        return new(true, canApply, true, runtimeReady,
            groups == AppliedGroupsUx9O.None
                ? "Aperçu sec validé · les trois profils sont déjà identiques."
                : "Aperçu sec validé sur trois commandes canoniques · aucun port ni service externe ouvert.",
            changes, [], probe);
    }

    internal StudioUx9OActionResult ApplyApprovedNetworkClosureUx9O(
        StudioUx9ONetworkClosureDraft draft)
    {
        var approved = _approvedNetworkClosureUx9O;
        var fingerprint = _approvedNetworkClosureFingerprintUx9O;
        if (approved is null || fingerprint is null ||
            !string.Equals(DraftFingerprintUx9O(NormalizeDraftUx9O(draft)), fingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(DraftFingerprintUx9O(approved), fingerprint,
                StringComparison.Ordinal) ||
            !TryBuildCandidatesUx9O(approved, out _, out var foundation,
                out var player, out var assurance, out _))
        {
            CancelNetworkClosureApprovalUx9O();
            return new(false, "La source ou le brouillon a changé ; relancez l’aperçu sec.");
        }

        var currentFoundation = _networkFoundationHistoryUx9A!.Current;
        var currentPlayer = _networkExperienceHistoryUx8M!.Current;
        var currentAssurance = _networkAssuranceHistoryUx9O!.Current;
        var foundationCommand = new NetworkFoundationCommand(Guid.NewGuid(), 1,
            "studio:ux9-o", currentFoundation.ServerRevision, Ux9OActor,
            NetworkRole.Administrator, false, foundation!);
        var playerCommand = new NetworkPlayerExperienceCommand(Guid.NewGuid(), 1,
            "studio:ux9-o", currentPlayer.ServerRevision, Ux9OActor,
            NetworkRole.Administrator, false, player!);
        var assuranceCommand = new NetworkAssuranceCommand(Guid.NewGuid(), 1,
            "studio:ux9-o", currentAssurance.ServerRevision, Ux9OActor,
            NetworkRole.Administrator, false, assurance!);
        var foundationDry = _networkFoundationHistoryUx9A.Execute(foundationCommand);
        var playerDry = _networkExperienceHistoryUx8M.Execute(playerCommand);
        var assuranceDry = _networkAssuranceHistoryUx9O.Execute(assuranceCommand);
        if (!foundationDry.Succeeded || !playerDry.Succeeded || !assuranceDry.Succeeded)
        {
            CancelNetworkClosureApprovalUx9O();
            return new(false, HumanDiagnosticsUx9O(foundationDry.Diagnostics
                .Concat(playerDry.Diagnostics).Concat(assuranceDry.Diagnostics)));
        }

        var groups = ChangedGroupsUx9O(foundationDry.SemanticDiff, playerDry.SemanticDiff,
            assuranceDry.SemanticDiff);
        if (groups == AppliedGroupsUx9O.None)
        {
            CancelNetworkClosureApprovalUx9O();
            return new(false, "Les profils sont déjà identiques.");
        }

        var appliedFoundation = !groups.HasFlag(AppliedGroupsUx9O.Foundation) ||
            _networkFoundationHistoryUx9A.Execute(foundationCommand with { Apply = true }) is
            { Succeeded: true, Applied: true };
        var appliedPlayer = appliedFoundation &&
            (!groups.HasFlag(AppliedGroupsUx9O.Player) ||
             _networkExperienceHistoryUx8M.Execute(playerCommand with { Apply = true }) is
             { Succeeded: true, Applied: true });
        var appliedAssurance = appliedPlayer &&
            (!groups.HasFlag(AppliedGroupsUx9O.Assurance) ||
             _networkAssuranceHistoryUx9O.Execute(assuranceCommand with { Apply = true }) is
             { Succeeded: true, Applied: true });
        if (!appliedAssurance)
        {
            if (groups.HasFlag(AppliedGroupsUx9O.Player) && appliedPlayer)
                _networkExperienceHistoryUx8M.Undo(Ux9OActor, NetworkRole.Administrator);
            if (groups.HasFlag(AppliedGroupsUx9O.Foundation) && appliedFoundation)
                _networkFoundationHistoryUx9A.Undo(Ux9OActor, NetworkRole.Administrator);
            CancelNetworkClosureApprovalUx9O();
            return new(false, "L’application coordonnée a été compensée ; aucun profil partiel n’est conservé.");
        }

        _appliedGroupsUx9O = groups;
        _undoneGroupsUx9O = AppliedGroupsUx9O.None;
        _networkClosureAuditUx9O.Add($"applied:studio.ux9-o:groups={groups}");
        CancelNetworkClosureApprovalUx9O();
        CancelNetworkFoundationApprovalUx9A();
        CancelNetworkExperienceApprovalUx8M();
        return new(true, "Politiques réseau appliquées par leurs trois commandes canoniques.");
    }

    internal StudioUx9OActionResult UndoNetworkClosureUx9O()
    {
        CancelNetworkClosureApprovalUx9O();
        if (_appliedGroupsUx9O == AppliedGroupsUx9O.None)
            return new(false, "Aucune modification UX9-O ne peut être annulée.");
        var groups = _appliedGroupsUx9O;
        var assurance = !groups.HasFlag(AppliedGroupsUx9O.Assurance) ||
            _networkAssuranceHistoryUx9O?.Undo(Ux9OActor, NetworkRole.Administrator) is
            { Succeeded: true, Applied: true };
        var player = assurance && (!groups.HasFlag(AppliedGroupsUx9O.Player) ||
            _networkExperienceHistoryUx8M?.Undo(Ux9OActor, NetworkRole.Administrator) is
            { Succeeded: true, Applied: true });
        var foundation = player && (!groups.HasFlag(AppliedGroupsUx9O.Foundation) ||
            _networkFoundationHistoryUx9A?.Undo(Ux9OActor, NetworkRole.Administrator) is
            { Succeeded: true, Applied: true });
        if (!foundation) return new(false, "L’annulation coordonnée a échoué.");
        _undoneGroupsUx9O = groups;
        _appliedGroupsUx9O = AppliedGroupsUx9O.None;
        _networkClosureAuditUx9O.Add($"compensated:studio.ux9-o:undo:groups={groups}");
        return new(true, "Modification réseau coordonnée annulée.");
    }

    internal StudioUx9OActionResult RedoNetworkClosureUx9O()
    {
        CancelNetworkClosureApprovalUx9O();
        if (_undoneGroupsUx9O == AppliedGroupsUx9O.None)
            return new(false, "Aucune modification UX9-O ne peut être rétablie.");
        var groups = _undoneGroupsUx9O;
        var foundation = !groups.HasFlag(AppliedGroupsUx9O.Foundation) ||
            _networkFoundationHistoryUx9A?.Redo(Ux9OActor, NetworkRole.Administrator) is
            { Succeeded: true, Applied: true };
        var player = foundation && (!groups.HasFlag(AppliedGroupsUx9O.Player) ||
            _networkExperienceHistoryUx8M?.Redo(Ux9OActor, NetworkRole.Administrator) is
            { Succeeded: true, Applied: true });
        var assurance = player && (!groups.HasFlag(AppliedGroupsUx9O.Assurance) ||
            _networkAssuranceHistoryUx9O?.Redo(Ux9OActor, NetworkRole.Administrator) is
            { Succeeded: true, Applied: true });
        if (!assurance) return new(false, "Le rétablissement coordonné a échoué.");
        _appliedGroupsUx9O = groups;
        _undoneGroupsUx9O = AppliedGroupsUx9O.None;
        _networkClosureAuditUx9O.Add($"compensated:studio.ux9-o:redo:groups={groups}");
        return new(true, "Modification réseau coordonnée rétablie.");
    }

    internal StudioUx9OActionResult SaveNetworkClosureUx9O()
    {
        CancelNetworkClosureApprovalUx9O();
        if (Session is null || !EnsureNetworkFoundationUx9A() ||
            !EnsureNetworkExperienceUx8M() || !EnsureNetworkAssuranceUx9O())
            return new(false, "Les trois profils réseau ne sont pas disponibles.");
        try
        {
            _ = new NetworkFoundationStore().Serialize(_networkFoundationHistoryUx9A!.Current);
            _ = new NetworkPlayerExperienceStore().Serialize(_networkExperienceHistoryUx8M!.Current);
            _ = new NetworkAssuranceStore().Serialize(_networkAssuranceHistoryUx9O!.Current);
        }
        catch (InvalidDataException exception)
        {
            return new(false, $"La sérialisation canonique a refusé la sauvegarde : {exception.Message}");
        }
        var foundation = WorkspaceNetworkFoundationStore.Save(Session.RootDirectory,
            _networkFoundationHistoryUx9A.Current);
        if (!foundation.Succeeded)
            return new(false, "La sauvegarde atomique du socle réseau a échoué.");
        var player = WorkspaceNetworkPlayerExperienceStore.Save(Session.RootDirectory,
            _networkExperienceHistoryUx8M.Current);
        if (!player.Succeeded)
            return new(false, "La sauvegarde atomique du profil joueur a échoué.");
        var assurance = WorkspaceNetworkAssuranceStore.Save(Session.RootDirectory,
            _networkAssuranceHistoryUx9O.Current);
        if (!assurance.Succeeded)
            return new(false, "La sauvegarde atomique d’un des profils réseau a échoué.");
        _persistedNetworkFoundationUx9A = _networkFoundationHistoryUx9A.Current;
        _networkFoundationFileMissingUx9A = false;
        _persistedNetworkExperienceUx8M = _networkExperienceHistoryUx8M.Current;
        _networkExperienceFileMissingUx8M = false;
        _persistedNetworkAssuranceUx9O = _networkAssuranceHistoryUx9O.Current;
        _networkAssuranceFileMissingUx9O = false;
        return new(true, "Trois profils réseau enregistrés atomiquement dans le workspace.");
    }

    internal StudioUx9OActionResult RunLocalNetworkClosureUx9O(bool confirmed,
        NetworkRole role)
    {
        if (!confirmed) return new(false, "Confirmez explicitement la sandbox locale.");
        if (role != NetworkRole.Administrator)
            return new(false, "La permission Administrateur est requise.");
        if (!EnsureNetworkFoundationUx9A() || !EnsureNetworkExperienceUx8M() ||
            !EnsureNetworkAssuranceUx9O())
            return new(false, "Les profils réseau sont indisponibles.");
        try
        {
            _networkClosureProbeUx9O = RunNetworkClosureProbeUx9O(
                _networkFoundationHistoryUx9A!.Current,
                _networkExperienceHistoryUx8M!.Current,
                _networkAssuranceHistoryUx9O!.Current);
            _networkClosureAuditUx9O.Add(
                $"sandbox:studio.ux9-o:ready={ProbeReadyUx9O(_networkClosureProbeUx9O)}");
            return new(ProbeReadyUx9O(_networkClosureProbeUx9O),
                _networkClosureProbeUx9O.Status);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                               InvalidDataException)
        {
            _networkClosureProbeUx9O = EmptyProbeUx9O() with
            { Status = $"Sandbox refusée : {exception.Message}" };
            return new(false, _networkClosureProbeUx9O.Status);
        }
    }

    internal StudioUx9OApprovalSnapshot CaptureNetworkClosureApprovalUx9O() =>
        new(_approvedNetworkClosureUx9O, _approvedNetworkClosureFingerprintUx9O);

    internal void RestoreNetworkClosureApprovalUx9O(StudioUx9OApprovalSnapshot snapshot)
    {
        _approvedNetworkClosureUx9O = snapshot.Draft;
        _approvedNetworkClosureFingerprintUx9O = snapshot.DraftFingerprint;
    }

    internal void CancelNetworkClosureApprovalUx9O()
    {
        _approvedNetworkClosureUx9O = null;
        _approvedNetworkClosureFingerprintUx9O = null;
    }

    internal void CloseUx9O()
    {
        CancelNetworkClosureApprovalUx9O();
        _networkAssuranceHistoryUx9O = null;
        _persistedNetworkAssuranceUx9O = null;
        _networkAssuranceWorkspaceRootUx9O = null;
        _networkAssuranceLoadMessageUx9O = null;
        _networkAssuranceFileMissingUx9O = false;
        _networkClosureProbeUx9O = EmptyProbeUx9O();
        _networkClosureAuditUx9O.Clear();
        _appliedGroupsUx9O = AppliedGroupsUx9O.None;
        _undoneGroupsUx9O = AppliedGroupsUx9O.None;
    }

    internal StudioUx9OSmokeResult RunUx9OContractSmoke()
    {
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceSession = Session;
        if (sourceProject is null || sourceHistory is null || sourceSession is null)
            return FailedSmokeUx9O();
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceCanUndo = sourceHistory.CanUndo;
        var sourceCanRedo = sourceHistory.CanRedo;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var foundationApproval = CaptureNetworkFoundationApprovalUx9A();
        var playerApproval = CaptureNetworkExperienceApprovalUx8M();
        var closureApproval = CaptureNetworkClosureApprovalUx9O();
        var root = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux9-o-{Guid.NewGuid():N}");
        StudioSessionController? editing = null;
        StudioSessionController? reopened = null;
        var result = FailedSmokeUx9O();
        try
        {
            if (!ProjectSession.Create(root, sourceProject).Succeeded) return result;
            var baselineFoundation = NetworkFoundationProfile.CreateDefault("evidence:ux9-o");
            var baselinePlayer = NetworkPlayerExperienceProfile.CreateDefault("evidence:ux9-o");
            var baselineAssurance = NetworkAssuranceProfile.CreateDefault("evidence:ux9-o");
            if (!WorkspaceNetworkFoundationStore.Save(root, baselineFoundation).Succeeded ||
                !WorkspaceNetworkPlayerExperienceStore.Save(root, baselinePlayer).Succeeded ||
                !WorkspaceNetworkAssuranceStore.Save(root, baselineAssurance).Succeeded)
                return result;
            editing = new StudioSessionController();
            if (!editing.Open(root).Succeeded) return result;
            var explicitSelection = editing.CreateNetworkClosureDraftUx9O() is null &&
                                    editing.CreateNetworkClosureDraftUx9O(true) is not null;
            var draft = editing.CreateNetworkClosureDraftUx9O(true)!;
            var originalFoundation = editing.CurrentNetworkFoundationUx9A!;
            var originalPlayer = editing.CurrentNetworkExperienceUx8M!;
            var originalAssurance = editing.CurrentNetworkAssuranceUx9O!;
            var originalNeighbors = NeighborFingerprintUx9O(originalFoundation,
                originalPlayer, originalAssurance);
            var fingerprint = new string('b', 64);
            var fixture = draft with
            {
                ServerName = "Forgotten Forest · dédié local",
                ServerPort = draft.ServerPort == 5122 ? 5123 : 5122,
                ServerCapacity = 24,
                DedicatedRequired = true,
                ServiceMode = DedicatedServiceMode.WindowsService,
                HealthCheckPath = "/healthz",
                PvpEnabled = true,
                FriendlyFire = FriendlyFirePolicy.Disabled,
                MinimumSeverity = StructuredLogSeverity.Warning,
                LogCategoriesText = "network, security",
                PlayerPrivacy = PlayerPrivacyMode.Hash,
                LogOutputFormat = StructuredLogOutputFormat.JsonAndText,
                LogRetentionDays = 14,
                LogMaximumEntries = 4096,
                LogRotationBytes = 1024,
                LogRotationFiles = 3,
                RemoteSinkName = "audit",
                RemoteSinkKind = RemoteLogSinkKind.OpenTelemetry,
                RemoteSinkEndpoint = "http://127.0.0.1:4318",
                RemoteSinkEnabled = true,
                RemoteSinkRequired = true,
                MaximumUnitsPerSecond = 18,
                MaximumIntentsPerSecond = 12,
                AnomalyThreshold = 20,
                TrustedSignerFingerprintsText = fingerprint,
                RandomSeed = 2042
            };
            var invalid = !editing.PreviewNetworkClosureUx9O(fixture with
            { HealthCheckPath = "health" }).Succeeded &&
                          !editing.PreviewNetworkClosureUx9O(fixture with
                          { ConsentRequired = false }).Succeeded &&
                          !editing.PreviewNetworkClosureUx9O(fixture with
                          { LogCategoriesText = "" }).Succeeded &&
                          !editing.PreviewNetworkClosureUx9O(fixture with
                          { TrustedSignerFingerprintsText = "invalid" }).Succeeded;
            var staleSource = !editing.PreviewNetworkClosureUx9O(fixture with
            { FoundationRevision = fixture.FoundationRevision + 1 }).Succeeded;
            var permission = !editing.PreviewNetworkClosureUx9O(fixture,
                NetworkRole.Player).Succeeded;
            var beforeFoundation = FingerprintUx9O(editing.CurrentNetworkFoundationUx9A!);
            var beforePlayer = FingerprintUx9O(editing.CurrentNetworkExperienceUx8M!);
            var beforeAssurance = FingerprintUx9O(editing.CurrentNetworkAssuranceUx9O!);
            var preview = editing.PreviewNetworkClosureUx9O(fixture);
            var dryRun = preview.Succeeded && preview.CanApply && preview.CanonicalDryRun &&
                         beforeFoundation == FingerprintUx9O(editing.CurrentNetworkFoundationUx9A!) &&
                         beforePlayer == FingerprintUx9O(editing.CurrentNetworkExperienceUx8M!) &&
                         beforeAssurance == FingerprintUx9O(editing.CurrentNetworkAssuranceUx9O!);
            var staleApproval = !editing.ApplyApprovedNetworkClosureUx9O(fixture with
            { ServerCapacity = fixture.ServerCapacity + 1 }).Succeeded;
            preview = editing.PreviewNetworkClosureUx9O(fixture);
            var applied = editing.ApplyApprovedNetworkClosureUx9O(fixture);
            var foundation = editing.CurrentNetworkFoundationUx9A!;
            var player = editing.CurrentNetworkExperienceUx8M!;
            var assurance = editing.CurrentNetworkAssuranceUx9O!;
            var fourRoutes = foundation.Dedicated.Headless &&
                             !foundation.Dedicated.EditorDependencyAllowed &&
                             foundation.Dedicated.ServiceMode == DedicatedServiceMode.WindowsService &&
                             foundation.Dedicated.HealthCheckPath == "/healthz" &&
                             player.Combat.PvpEnabled &&
                             player.Combat.FriendlyFire == FriendlyFirePolicy.Disabled &&
                             assurance.Logs.MinimumSeverity == StructuredLogSeverity.Warning &&
                             assurance.Logs.RotationFiles == 3 &&
                             assurance.AntiCheat.MaximumUnitsPerSecond == 18 &&
                             assurance.AntiCheat.TrustedSignerFingerprints.Single() == fingerprint;
            var commands = applied.Succeeded &&
                           editing.NetworkClosureAuditUx9O.Any(item =>
                               item.StartsWith("applied:studio.ux9-o", StringComparison.Ordinal));
            var neighbors = string.Equals(originalNeighbors,
                NeighborFingerprintUx9O(foundation, player, assurance),
                StringComparison.Ordinal);
            var undo = editing.UndoNetworkClosureUx9O().Succeeded;
            var redo = editing.RedoNetworkClosureUx9O().Succeeded;
            var saved = editing.SaveNetworkClosureUx9O().Succeeded;
            var expectedFoundation = new NetworkFoundationStore().Serialize(
                editing.CurrentNetworkFoundationUx9A!);
            var expectedPlayer = new NetworkPlayerExperienceStore().Serialize(
                editing.CurrentNetworkExperienceUx8M!);
            var expectedAssurance = new NetworkAssuranceStore().Serialize(
                editing.CurrentNetworkAssuranceUx9O!);
            reopened = new StudioSessionController();
            var persistence = saved && reopened.Open(root).Succeeded &&
                              expectedFoundation == new NetworkFoundationStore().Serialize(
                                  reopened.CurrentNetworkFoundationUx9A!) &&
                              expectedPlayer == new NetworkPlayerExperienceStore().Serialize(
                                  reopened.CurrentNetworkExperienceUx8M!) &&
                              expectedAssurance == new NetworkAssuranceStore().Serialize(
                                  reopened.CurrentNetworkAssuranceUx9O!);
            var confirmation = !editing.RunLocalNetworkClosureUx9O(false,
                NetworkRole.Administrator).Succeeded;
            permission = permission && !editing.RunLocalNetworkClosureUx9O(true,
                NetworkRole.Player).Succeeded;
            var probed = editing.RunLocalNetworkClosureUx9O(true,
                NetworkRole.Administrator).Succeeded;
            var probe = editing.NetworkClosureProbeUx9O;
            result = new(true, fourRoutes, explicitSelection, invalid, permission,
                confirmation, dryRun, commands, staleSource, staleApproval, undo && redo,
                persistence, probed && probe.Dedicated, probe.Combat &&
                probe.FriendlyFireRespected, probe.StructuredLogs && probe.Privacy,
                probe.AntiCheat && probe.ForgedIntentBlocked && probe.ClientSaveBlocked,
                neighbors, true, true);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX9_O_CONTRACT_FAILED " +
                              $"type={exception.GetType().Name} message={exception.Message}");
            result = FailedSmokeUx9O();
        }
        finally
        {
            reopened?.Close();
            editing?.Close();
            var released = DeleteTemporaryWorkspaceUx9O(root);
            var sourceUnchanged = SourceMatchesUx9O(sourceProject, sourceHistory,
                sourceRevision, sourceJournal, sourceDirty, sourceCanUndo, sourceCanRedo,
                sourceArea, sourceSelection, sourceSelected, foundationApproval,
                playerApproval, closureApproval);
            result = result with
            {
                SourceUnchanged = sourceUnchanged,
                TemporaryWorkspaceReleased = released
            };
        }
        var succeeded = result.FourRoutes && result.ExplicitSelection &&
                        result.InvalidBlocked && result.PermissionBlocked &&
                        result.ConfirmationRequired && result.CanonicalDryRun &&
                        result.CanonicalCommands && result.StaleSourceBlocked &&
                        result.StaleApprovalBlocked && result.UndoRedo && result.Persistence &&
                        result.Dedicated && result.Combat && result.StructuredLogs &&
                        result.AntiCheat && result.NeighborsPreserved &&
                        result.SourceUnchanged && result.TemporaryWorkspaceReleased;
        return result with { Succeeded = succeeded };
    }

    private bool EnsureNetworkAssuranceUx9O()
    {
        var root = Session?.RootDirectory;
        if (string.IsNullOrWhiteSpace(root))
        {
            _networkAssuranceLoadMessageUx9O = "Ouvrez d’abord un module.";
            return false;
        }
        var full = Path.GetFullPath(root);
        if (_networkAssuranceHistoryUx9O is not null &&
            string.Equals(_networkAssuranceWorkspaceRootUx9O, full,
                StringComparison.OrdinalIgnoreCase)) return true;
        var loaded = WorkspaceNetworkAssuranceStore.Load(full);
        _networkAssuranceWorkspaceRootUx9O = full;
        if (!loaded.Succeeded)
        {
            _networkAssuranceHistoryUx9O = null;
            _persistedNetworkAssuranceUx9O = null;
            _networkAssuranceFileMissingUx9O = false;
            _networkAssuranceLoadMessageUx9O =
                $"Profil assurance invalide : {loaded.Diagnostics.FirstOrDefault()?.Message}";
            return false;
        }
        _networkAssuranceHistoryUx9O = new(loaded.Profile);
        _persistedNetworkAssuranceUx9O = loaded.Profile;
        _networkAssuranceFileMissingUx9O = loaded.FileMissing;
        _networkAssuranceLoadMessageUx9O = loaded.FileMissing
            ? "Aucun profil assurance enregistré ; valeurs initiales sans écriture."
            : null;
        return true;
    }

    private bool TryBuildCandidatesUx9O(StudioUx9ONetworkClosureDraft draft,
        out StudioUx9ONetworkClosureDraft? normalized,
        out NetworkFoundationProfile? foundation,
        out NetworkPlayerExperienceProfile? player,
        out NetworkAssuranceProfile? assurance, out string error)
    {
        normalized = null;
        foundation = null;
        player = null;
        assurance = null;
        error = "Les profils réseau sont indisponibles.";
        if (!EnsureNetworkFoundationUx9A() || !EnsureNetworkExperienceUx8M() ||
            !EnsureNetworkAssuranceUx9O()) return false;
        var currentFoundation = _networkFoundationHistoryUx9A!.Current;
        var currentPlayer = _networkExperienceHistoryUx8M!.Current;
        var currentAssurance = _networkAssuranceHistoryUx9O!.Current;
        normalized = NormalizeDraftUx9O(draft);
        if (normalized.FoundationRevision != currentFoundation.ServerRevision ||
            normalized.PlayerRevision != currentPlayer.ServerRevision ||
            normalized.AssuranceRevision != currentAssurance.ServerRevision ||
            !string.Equals(normalized.FoundationFingerprint,
                FingerprintUx9O(currentFoundation), StringComparison.Ordinal) ||
            !string.Equals(normalized.PlayerFingerprint, FingerprintUx9O(currentPlayer),
                StringComparison.Ordinal) ||
            !string.Equals(normalized.AssuranceFingerprint,
                FingerprintUx9O(currentAssurance), StringComparison.Ordinal))
        {
            error = "Une source réseau a changé ; rechargez les trois profils.";
            return false;
        }
        var sinks = currentAssurance.Logs.RemoteSinks.ToArray();
        sinks[0] = sinks[0] with
        {
            Name = normalized.RemoteSinkName,
            Kind = normalized.RemoteSinkKind,
            Endpoint = normalized.RemoteSinkEndpoint,
            Enabled = normalized.RemoteSinkEnabled,
            Required = normalized.RemoteSinkRequired
        };
        foundation = currentFoundation with
        {
            ServerRevision = currentFoundation.ServerRevision + 1,
            RequiredAtRuntime = normalized.DedicatedRequired,
            Server = currentFoundation.Server with
            {
                Name = normalized.ServerName,
                BindAddress = normalized.ServerHost,
                Port = normalized.ServerPort,
                Capacity = normalized.ServerCapacity
            },
            Dedicated = currentFoundation.Dedicated with
            {
                Headless = true,
                EditorDependencyAllowed = false,
                ServiceMode = normalized.ServiceMode,
                HealthCheckPath = normalized.HealthCheckPath
            }
        };
        player = currentPlayer with
        {
            ServerRevision = currentPlayer.ServerRevision + 1,
            Combat = currentPlayer.Combat with
            {
                PvpEnabled = normalized.PvpEnabled,
                FriendlyFire = normalized.FriendlyFire,
                GlobalPolicy = normalized.GlobalCombatPolicy,
                AreaOverrides = normalized.AreaOverrides,
                PartyOverrides = normalized.PartyOverrides,
                ConsentRequired = normalized.ConsentRequired,
                DuelsEnabled = normalized.DuelsEnabled,
                CrimeEnabled = normalized.CrimeEnabled,
                SafeZonesEnabled = normalized.SafeZonesEnabled,
                ServerOverride = normalized.ServerOverride
            }
        };
        assurance = currentAssurance with
        {
            ServerRevision = currentAssurance.ServerRevision + 1,
            Logs = currentAssurance.Logs with
            {
                MinimumSeverity = normalized.MinimumSeverity,
                Categories = TokensUx9O(normalized.LogCategoriesText, lower: true),
                PlayerPrivacy = normalized.PlayerPrivacy,
                OutputFormat = normalized.LogOutputFormat,
                RetentionDays = normalized.LogRetentionDays,
                MaximumEntries = normalized.LogMaximumEntries,
                RotationBytes = normalized.LogRotationBytes,
                RotationFiles = normalized.LogRotationFiles,
                RemoteSinks = sinks
            },
            AntiCheat = currentAssurance.AntiCheat with
            {
                MaximumUnitsPerSecond = normalized.MaximumUnitsPerSecond,
                MaximumIntentsPerSecond = normalized.MaximumIntentsPerSecond,
                AnomalyThreshold = normalized.AnomalyThreshold,
                RequireSignedContent = normalized.RequireSignedContent,
                TrustedSignerFingerprints = TokensUx9O(
                    normalized.TrustedSignerFingerprintsText, lower: true),
                AuthoritativeRandom = normalized.AuthoritativeRandom,
                TrustClientSaves = normalized.TrustClientSaves,
                RandomSeed = normalized.RandomSeed
            }
        };
        error = string.Empty;
        return true;
    }

    private static StudioUx9ONetworkClosureProbe RunNetworkClosureProbeUx9O(
        NetworkFoundationProfile foundation, NetworkPlayerExperienceProfile player,
        NetworkAssuranceProfile assurance)
    {
        var configurationStore = new NetworkConfigurationStore();
        var configuration = configurationStore.Deserialize(
            configurationStore.Serialize(foundation.Server));
        var runtime = NetworkFoundationRuntimeAdapter.Build(foundation);
        var connection = Guid.Parse("a9000000-0000-4000-8000-000000000901");
        var connected = runtime.Session.Connect(connection,
            new(Guid.Parse("a9000000-0000-4000-8000-000000000902"),
                "Elyra", SessionRole.Administrator), configuration.ModuleFingerprint,
            configuration.ProtocolVersion);
        var disconnected = connected.Accepted && runtime.Session.Disconnect(connection).Accepted;
        var dedicated = foundation.Dedicated.Headless &&
                        !foundation.Dedicated.EditorDependencyAllowed &&
                        foundation.Dedicated.HealthCheckPath.StartsWith("/", StringComparison.Ordinal) &&
                        configuration == foundation.Server && disconnected &&
                        runtime.Session.Snapshot.Members.Count == 0;

        var parties = new PartyService();
        var leader = Guid.Parse("a9000000-0000-4000-8000-000000000903");
        var ally = Guid.Parse("a9000000-0000-4000-8000-000000000904");
        var party = parties.Create(leader);
        var joined = parties.Join(party.PartyId, leader, ally, party.Revision);
        var combatAuthority = new CombatAuthority(parties,
            new(player.Combat.PvpEnabled, player.Combat.FriendlyFire));
        var allyDecision = combatAuthority.AuthorizeDamage(leader, ally, true, 7);
        var creatureDecision = combatAuthority.AuthorizeDamage(leader,
            Guid.Parse("a9000000-0000-4000-8000-000000000905"), false, 7);
        var friendlyRespected = player.Combat.PvpEnabled
            ? player.Combat.FriendlyFire == FriendlyFirePolicy.Enabled
                ? allyDecision.Accepted : !allyDecision.Accepted
            : !allyDecision.Accepted && allyDecision.Code == "NET_PVP_DISABLED";
        var combat = joined.Accepted && creatureDecision.Accepted && friendlyRespected &&
                     player.Combat.GlobalPolicy && player.Combat.AreaOverrides &&
                     player.Combat.PartyOverrides && player.Combat.ConsentRequired &&
                     player.Combat.ServerOverride;

        var sinkPolicy = assurance.Logs.RemoteSinks.First();
        var sink = new MemoryRemoteLogSink(sinkPolicy.Name);
        var assuranceRuntime = NetworkAssuranceRuntimeAdapter.Build(assurance,
            TimeProvider.System, [sink]);
        var category = assurance.Logs.Categories.First();
        var log = assuranceRuntime.Logs.Write(assurance.Logs.MinimumSeverity,
            "network.studio.probe", Guid.Parse("a9000000-0000-4000-8000-000000000906"),
            new(category, assurance.Logs.Id, 9,
                Guid.Parse("a9000000-0000-4000-8000-000000000907"), "player-elyra"),
            new Dictionary<string, object?>
            {
                ["accessToken"] = "never-log",
                ["playerAccount"] = "elyra"
            });
        var outputs = string.Join(' ', new[] { log.Json, log.Text }.Where(value => value is not null));
        var privacy = !outputs.Contains("never-log", StringComparison.Ordinal) &&
                      !outputs.Contains("player-elyra", StringComparison.Ordinal) &&
                      !outputs.Contains("elyra", StringComparison.Ordinal);
        var delivered = sinkPolicy.Enabled ? log.DeliveredSinks.Contains(sinkPolicy.Name,
            StringComparer.Ordinal) : log.DeliveredSinks.Count == 0;
        var logs = privacy && delivered &&
                   (assurance.Logs.OutputFormat != StructuredLogOutputFormat.Json || log.Json is not null) &&
                   (assurance.Logs.OutputFormat != StructuredLogOutputFormat.Text || log.Text is not null);

        var signer = assurance.AntiCheat.TrustedSignerFingerprints.First();
        var anti = assuranceRuntime.AntiCheat;
        var antiConnection = Guid.Parse("a9000000-0000-4000-8000-000000000908");
        var join = anti.ValidateJoin(antiConnection,
            new(new string('c', 64), signer, true));
        var forged = anti.ValidateIntent(new(Guid.NewGuid(), antiConnection,
                Guid.Parse("a9000000-0000-4000-8000-000000000909"), 1, 0,
                new(0, 0, 0), new(1000, 0, 0), TimeSpan.FromSeconds(1)),
            0, DateTimeOffset.UnixEpoch.AddSeconds(1));
        var clientSave = anti.ValidateClientSave(antiConnection, "client-save"u8);
        var random = anti.NextAuthoritativeRandom(1, 1000);
        var repeated = new AntiCheatService(assurance.AntiCheat)
            .NextAuthoritativeRandom(1, 1000);
        var antiReady = join.Allowed && !forged.Allowed && !clientSave.Allowed &&
                        random == repeated;
        return new(dedicated && combat && logs && antiReady
                ? "Quatre politiques réseau vérifiées et ressources libérées"
                : "Sandbox réseau incomplète",
            dedicated
                ? $"HEADLESS_READY mode={foundation.Dedicated.ServiceMode} health={foundation.Dedicated.HealthCheckPath}"
                : "HEADLESS_INVALID", dedicated,
            allyDecision.Code, combat, friendlyRespected,
            logs ? "NET_LOG_ROUTED" : "NET_LOG_PRIVACY_FAILED", logs, privacy,
            log.DeliveredSinks.Count, join.Code, antiReady, !forged.Allowed,
            !clientSave.Allowed, runtime.Session.Snapshot.Members.Count == 0);
    }

    private static StudioUx9ONetworkClosureDraft NormalizeDraftUx9O(
        StudioUx9ONetworkClosureDraft draft) => draft with
        {
            ServerName = draft.ServerName.Trim(),
            ServerHost = draft.ServerHost.Trim(),
            HealthCheckPath = draft.HealthCheckPath.Trim(),
            LogCategoriesText = string.Join(", ", TokensUx9O(draft.LogCategoriesText,
            lower: true)),
            RemoteSinkName = draft.RemoteSinkName.Trim().ToLowerInvariant(),
            RemoteSinkEndpoint = draft.RemoteSinkEndpoint.Trim(),
            TrustedSignerFingerprintsText = string.Join(", ", TokensUx9O(
            draft.TrustedSignerFingerprintsText, lower: true))
        };

    private static string[] TokensUx9O(string text, bool lower) => text
        .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries |
                           StringSplitOptions.TrimEntries)
        .Select(value => lower ? value.ToLowerInvariant() : value)
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    private static string FingerprintUx9O(NetworkFoundationProfile value) =>
        HashUx9O(new NetworkFoundationStore().Serialize(value));
    private static string FingerprintUx9O(NetworkPlayerExperienceProfile value) =>
        HashUx9O(new NetworkPlayerExperienceStore().Serialize(value));
    private static string FingerprintUx9O(NetworkAssuranceProfile value) =>
        HashUx9O(new NetworkAssuranceStore().Serialize(value));
    private static string HashUx9O(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string DraftFingerprintUx9O(StudioUx9ONetworkClosureDraft draft) =>
        HashUx9O(string.Join('|', draft.FoundationRevision, draft.FoundationFingerprint,
            draft.PlayerRevision, draft.PlayerFingerprint, draft.AssuranceRevision,
            draft.AssuranceFingerprint, draft.ServerName, draft.ServerHost, draft.ServerPort,
            draft.ServerCapacity, draft.DedicatedRequired, draft.ServiceMode,
            draft.HealthCheckPath, draft.PvpEnabled, draft.FriendlyFire,
            draft.GlobalCombatPolicy, draft.AreaOverrides, draft.PartyOverrides,
            draft.ConsentRequired, draft.DuelsEnabled, draft.CrimeEnabled,
            draft.SafeZonesEnabled, draft.ServerOverride, draft.MinimumSeverity,
            draft.LogCategoriesText, draft.PlayerPrivacy, draft.LogOutputFormat,
            draft.LogRetentionDays, draft.LogMaximumEntries, draft.LogRotationBytes,
            draft.LogRotationFiles, draft.RemoteSinkName, draft.RemoteSinkKind,
            draft.RemoteSinkEndpoint, draft.RemoteSinkEnabled, draft.RemoteSinkRequired,
            draft.MaximumUnitsPerSecond.ToString("R", CultureInfo.InvariantCulture),
            draft.MaximumIntentsPerSecond, draft.AnomalyThreshold,
            draft.RequireSignedContent, draft.TrustedSignerFingerprintsText,
            draft.AuthoritativeRandom, draft.TrustClientSaves, draft.RandomSeed));

    private static string NeighborFingerprintUx9O(NetworkFoundationProfile foundation,
        NetworkPlayerExperienceProfile player, NetworkAssuranceProfile assurance) =>
        HashUx9O(JsonSerializer.Serialize(new
        {
            foundation.Authority,
            foundation.Party,
            foundation.Replication,
            foundation.Interest,
            player.Discovery,
            player.Lobby,
            player.CharacterBuilder,
            player.CharacterAuthority,
            assurance.DeterministicTests
        }));

    private static AppliedGroupsUx9O ChangedGroupsUx9O(string foundation,
        string player, string assurance)
    {
        var groups = AppliedGroupsUx9O.None;
        if (foundation != "revision-only") groups |= AppliedGroupsUx9O.Foundation;
        if (player != "revision-only") groups |= AppliedGroupsUx9O.Player;
        if (assurance != "revision-only") groups |= AppliedGroupsUx9O.Assurance;
        return groups;
    }

    private static string[] HumanChangesUx9O(string foundation, string player,
        string assurance)
    {
        var changes = new List<string>();
        if (foundation != "revision-only") changes.Add("Serveur dédié et santé");
        if (player != "revision-only") changes.Add("PvP et tir ami");
        if (assurance.Contains("structured-logs", StringComparison.Ordinal))
            changes.Add("Journaux structurés");
        if (assurance.Contains("anti-cheat", StringComparison.Ordinal))
            changes.Add("Anti-triche autoritaire");
        return changes.ToArray();
    }

    private static bool ProbeReadyUx9O(StudioUx9ONetworkClosureProbe probe) =>
        probe.Dedicated && probe.Combat && probe.FriendlyFireRespected &&
        probe.StructuredLogs && probe.Privacy && probe.AntiCheat &&
        probe.ForgedIntentBlocked && probe.ClientSaveBlocked && probe.ResourcesReleased;

    private static StudioUx9ONetworkClosurePreview FailedPreviewUx9O(string message,
        IEnumerable<NetworkDiagnostic>? diagnostics = null) => new(false, false, false,
        false, message, [], diagnostics?.Select(item => item.Message)
            .Distinct(StringComparer.Ordinal).ToArray() ?? [message], EmptyProbeUx9O());

    private static string HumanDiagnosticsUx9O(IEnumerable<NetworkDiagnostic> diagnostics)
    {
        var values = diagnostics.Select(item => item.Message).Distinct(StringComparer.Ordinal)
            .ToArray();
        return values.Length == 0 ? "Les commandes réseau ont été refusées." :
            string.Join(" · ", values);
    }

    private static StudioUx9ONetworkClosureProbe EmptyProbeUx9O() => new(
        "Aucune sandbox exécutée", "—", false, "—", false, false, "—", false,
        false, 0, "—", false, false, false, true);

    private static StudioUx9OSmokeResult FailedSmokeUx9O() => new(false, false,
        false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false);

    private bool SourceMatchesUx9O(Forge.Domain.Projects.ProjectDefinition project,
        Forge.Application.Commands.ProjectCommandHistory history,
        Forge.Application.Projects.ProjectRevision revision,
        IReadOnlyList<Forge.Application.Commands.CommandHistoryJournalEntry> journal,
        bool dirty, bool canUndo, bool canRedo, Forge.Domain.Areas.AreaId? area,
        IReadOnlyList<Forge.Domain.Entities.EntityId> selection,
        Forge.Domain.Entities.EntityId? selected,
        StudioUx9AApprovalSnapshot foundationApproval,
        StudioUx8MApprovalSnapshot playerApproval,
        StudioUx9OApprovalSnapshot closureApproval) =>
        ReferenceEquals(Project, project) && ReferenceEquals(History, history) &&
        history.State.Revision == revision && history.Journal.SequenceEqual(journal) &&
        IsDirty == dirty && history.CanUndo == canUndo && history.CanRedo == canRedo &&
        ActiveAreaId == area && SelectedEntityIds.SequenceEqual(selection) &&
        SelectedEntityId == selected &&
        CaptureNetworkFoundationApprovalUx9A() == foundationApproval &&
        CaptureNetworkExperienceApprovalUx8M() == playerApproval &&
        CaptureNetworkClosureApprovalUx9O() == closureApproval;

    private static bool DeleteTemporaryWorkspaceUx9O(string root)
    {
        try
        {
            var full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(Path.GetDirectoryName(full)?.TrimEnd(
                        Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), temp,
                    StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux9-o-",
                    StringComparison.Ordinal)) return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    [Flags]
    private enum AppliedGroupsUx9O
    {
        None = 0,
        Foundation = 1,
        Player = 2,
        Assurance = 4
    }
}
