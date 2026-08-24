using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.ModuleFormat.Network;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Authoring;
using Forge.Network.Identity;
using Forge.Network.Operations;
using Forge.Network.Sessions;
using Forge.Network.World;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private NetworkWorldAdministrationHistory? _worldAdministrationHistoryUx9C;
    private NetworkWorldAdministrationProfile? _persistedWorldAdministrationUx9C;
    private string? _worldAdministrationWorkspaceRootUx9C;
    private StudioUx9CWorldAdministrationDraft? _approvedWorldAdministrationUx9C;
    private string? _approvedWorldAdministrationFingerprintUx9C;
    private string? _worldAdministrationLoadMessageUx9C;
    private bool _worldAdministrationFileMissingUx9C;
    private StudioUx9CProbeSnapshot _worldAdministrationProbeUx9C = EmptyProbeUx9C();
    private readonly List<string> _worldAdministrationAuditUx9C = [];
    private bool _lastUx9CTemporaryReleased = true;

    internal string? WorldAdministrationLoadMessageUx9C => _worldAdministrationLoadMessageUx9C;
    internal bool WorldAdministrationFileMissingUx9C => _worldAdministrationFileMissingUx9C;
    internal bool LastUx9CTemporaryReleased => _lastUx9CTemporaryReleased;
    internal StudioUx9CProbeSnapshot WorldAdministrationProbeUx9C => _worldAdministrationProbeUx9C;
    internal IReadOnlyList<string> WorldAdministrationAuditUx9C => _worldAdministrationAuditUx9C.ToArray();
    internal NetworkWorldAdministrationProfile? CurrentWorldAdministrationUx9C =>
        EnsureWorldAdministrationUx9C() ? _worldAdministrationHistoryUx9C!.Current : null;
    internal bool WorldAdministrationHasUnsavedChangesUx9C =>
        EnsureWorldAdministrationUx9C() && _persistedWorldAdministrationUx9C is not null &&
        !string.Equals(ProfileFingerprintUx9C(_worldAdministrationHistoryUx9C!.Current),
            ProfileFingerprintUx9C(_persistedWorldAdministrationUx9C), StringComparison.Ordinal);

    internal StudioUx9CWorldAdministrationDraft? CreateWorldAdministrationDraftUx9C(
        bool explicitlySelected = false)
    {
        if (!explicitlySelected || !EnsureWorldAdministrationUx9C()) return null;
        var profile = _worldAdministrationHistoryUx9C!.Current;
        return new(profile.ServerRevision, ProfileFingerprintUx9C(profile),
            string.Join(", ", profile.Instances.TemplateIds), profile.Instances.MaximumInstances,
            profile.Instances.MaximumPlayersPerInstance, profile.Instances.GarbageCollectionMinutes,
            string.Join(", ", profile.DynamicAreas.AllowedOverrideKeys), profile.DynamicAreas.MaximumEntities,
            profile.DynamicAreas.MaximumResourceMegabytes, profile.Authentication.SessionLifetimeMinutes,
            profile.Authentication.Providers.Contains(AuthenticationProviderKind.OAuth),
            profile.Authentication.Providers.Contains(AuthenticationProviderKind.Anonymous),
            profile.Administration.RequestsPerMinute,
            string.Join(", ", profile.Administration.SafeCommandIds),
            profile.Moderation.PrivacyRetentionDays);
    }

    internal StudioUx9CWorldAdministrationPreview PreviewWorldAdministrationUx9C(
        StudioUx9CWorldAdministrationDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelWorldAdministrationApprovalUx9C();
        if (!EnsureWorldAdministrationUx9C())
            return FailedWorldAdministrationPreviewUx9C(
                _worldAdministrationLoadMessageUx9C ?? "L’administration réseau ne peut pas être chargée.");
        var current = _worldAdministrationHistoryUx9C!.Current;
        if (draft.BaseServerRevision != current.ServerRevision ||
            !string.Equals(draft.SourceProfileFingerprint, ProfileFingerprintUx9C(current),
                StringComparison.Ordinal))
            return FailedWorldAdministrationPreviewUx9C(
                "La configuration a changé ; rechargez le formulaire avant l’aperçu sec.");

        var normalized = NormalizeDraftUx9C(draft);
        if (ParseTokensUx9C(normalized.TemplateIds).Count == 0 ||
            ParseTokensUx9C(normalized.AllowedOverrideKeys).Count == 0 ||
            ParseTokensUx9C(normalized.SafeCommandIds).Count == 0)
            return FailedWorldAdministrationPreviewUx9C(
                "Renseignez au moins un template, une clé de surcharge et une commande sûre.");
        var candidate = BuildCandidateUx9C(current, normalized);
        var command = new NetworkWorldAdministrationCommand(Guid.NewGuid(), 1, "studio:ux9-c",
            current.ServerRevision, "studio.network.operator", NetworkRole.Administrator, false, candidate);
        var dryRun = _worldAdministrationHistoryUx9C.Execute(command);
        if (!dryRun.Succeeded || dryRun.Applied)
            return FailedWorldAdministrationPreviewUx9C(
                HumanWorldAdministrationDiagnosticsUx9C(dryRun.Diagnostics), dryRun.Diagnostics);

        WorldAdministrationProbeResultUx9C probe;
        try
        {
            probe = RunWorldAdministrationProbeUx9C(candidate);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                               UnauthorizedAccessException or InvalidDataException)
        {
            return FailedWorldAdministrationPreviewUx9C(
                $"La sonde locale a refusé la configuration : {exception.Message}");
        }

        _worldAdministrationProbeUx9C = probe.Snapshot;
        var changes = dryRun.SemanticDiff == "revision-only"
            ? Array.Empty<string>()
            : dryRun.SemanticDiff.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var runtimeReady = probe.Instances && probe.DynamicAreas && probe.Authentication &&
                           probe.Administration && probe.Moderation && probe.Snapshot.ResourcesReleased;
        var canApply = runtimeReady && changes.Length > 0;
        if (canApply)
        {
            _approvedWorldAdministrationUx9C = normalized with { };
            _approvedWorldAdministrationFingerprintUx9C =
                DraftFingerprintUx9C(_approvedWorldAdministrationUx9C);
        }
        return new(true, canApply, true, probe.Instances, probe.DynamicAreas, probe.Authentication,
            probe.Administration, probe.Moderation,
            changes.Length == 0
                ? "Aperçu sec validé · le profil est déjà identique."
                : "Aperçu sec et cinq sondes locales validés · aucune API distante ouverte.",
            changes.Select(HumanWorldAdministrationDiffUx9C).ToArray(), [], probe.Snapshot);
    }

    internal StudioUx9CActionResult ApplyApprovedWorldAdministrationUx9C(
        StudioUx9CWorldAdministrationDraft currentDraft)
    {
        var approved = _approvedWorldAdministrationUx9C;
        if (approved is null || _approvedWorldAdministrationFingerprintUx9C is null ||
            !EnsureWorldAdministrationUx9C())
        {
            CancelWorldAdministrationApprovalUx9C();
            return new(false, "Prévisualisez d’abord l’administration réseau.");
        }
        var current = _worldAdministrationHistoryUx9C!.Current;
        var normalizedCurrent = NormalizeDraftUx9C(currentDraft);
        if (!string.Equals(DraftFingerprintUx9C(normalizedCurrent), _approvedWorldAdministrationFingerprintUx9C,
                StringComparison.Ordinal) ||
            !string.Equals(DraftFingerprintUx9C(approved), _approvedWorldAdministrationFingerprintUx9C,
                StringComparison.Ordinal) || approved.BaseServerRevision != current.ServerRevision ||
            !string.Equals(approved.SourceProfileFingerprint, ProfileFingerprintUx9C(current),
                StringComparison.Ordinal))
        {
            CancelWorldAdministrationApprovalUx9C();
            return new(false, "La source ou le brouillon a changé ; relancez l’aperçu sec.");
        }
        var candidate = BuildCandidateUx9C(current, approved);
        var command = new NetworkWorldAdministrationCommand(Guid.NewGuid(), 1, "studio:ux9-c",
            current.ServerRevision, "studio.network.operator", NetworkRole.Administrator, false, candidate);
        var dryRun = _worldAdministrationHistoryUx9C.Execute(command);
        if (!dryRun.Succeeded || dryRun.Applied || dryRun.SemanticDiff == "revision-only")
        {
            CancelWorldAdministrationApprovalUx9C();
            return new(false, HumanWorldAdministrationDiagnosticsUx9C(dryRun.Diagnostics));
        }
        var applied = _worldAdministrationHistoryUx9C.Execute(command with { Apply = true });
        CancelWorldAdministrationApprovalUx9C();
        if (!applied.Succeeded || !applied.Applied)
            return new(false, HumanWorldAdministrationDiagnosticsUx9C(applied.Diagnostics));
        _worldAdministrationAuditUx9C.Add(applied.AuditEntry);
        return new(true, "Administration réseau appliquée par la commande canonique · enregistrez pour persister.");
    }

    internal StudioUx9CActionResult UndoWorldAdministrationUx9C()
    {
        CancelWorldAdministrationApprovalUx9C();
        if (!EnsureWorldAdministrationUx9C())
            return new(false, _worldAdministrationLoadMessageUx9C ?? "Aucune configuration active.");
        var result = _worldAdministrationHistoryUx9C!.Undo("studio.network.operator", NetworkRole.Administrator);
        if (result.Succeeded) _worldAdministrationAuditUx9C.Add(result.AuditEntry);
        return new(result.Succeeded && result.Applied,
            result.Succeeded ? "Modification réseau annulée." :
            HumanWorldAdministrationDiagnosticsUx9C(result.Diagnostics));
    }

    internal StudioUx9CActionResult RedoWorldAdministrationUx9C()
    {
        CancelWorldAdministrationApprovalUx9C();
        if (!EnsureWorldAdministrationUx9C())
            return new(false, _worldAdministrationLoadMessageUx9C ?? "Aucune configuration active.");
        var result = _worldAdministrationHistoryUx9C!.Redo("studio.network.operator", NetworkRole.Administrator);
        if (result.Succeeded) _worldAdministrationAuditUx9C.Add(result.AuditEntry);
        return new(result.Succeeded && result.Applied,
            result.Succeeded ? "Modification réseau rétablie." :
            HumanWorldAdministrationDiagnosticsUx9C(result.Diagnostics));
    }

    internal StudioUx9CActionResult SaveWorldAdministrationUx9C()
    {
        CancelWorldAdministrationApprovalUx9C();
        if (!EnsureWorldAdministrationUx9C() || Session is null)
            return new(false, _worldAdministrationLoadMessageUx9C ?? "Aucune configuration active.");
        var result = WorkspaceNetworkWorldAdministrationStore.Save(Session.RootDirectory,
            _worldAdministrationHistoryUx9C!.Current);
        if (!result.Succeeded)
            return new(false, result.Diagnostics.FirstOrDefault()?.Message ??
                              "L’administration réseau n’a pas été enregistrée.");
        _persistedWorldAdministrationUx9C = _worldAdministrationHistoryUx9C.Current;
        _worldAdministrationFileMissingUx9C = false;
        return new(true, "Administration réseau enregistrée atomiquement dans le workspace.");
    }

    internal StudioUx9CActionResult RunLocalWorldAdministrationUx9C(bool confirmed, NetworkRole role)
    {
        if (!confirmed)
        {
            _worldAdministrationAuditUx9C.Add("rejected:studio.network.world-administration:confirmation-required");
            return new(false, "Confirmez explicitement l’exécution de la sonde locale.");
        }
        if (role != NetworkRole.Administrator)
        {
            _worldAdministrationAuditUx9C.Add("rejected:studio.network.world-administration:permission-denied");
            return new(false, "La permission Administrateur est requise pour cette sonde.");
        }
        if (!EnsureWorldAdministrationUx9C())
            return new(false, _worldAdministrationLoadMessageUx9C ?? "Aucune configuration active.");
        try
        {
            var probe = RunWorldAdministrationProbeUx9C(_worldAdministrationHistoryUx9C!.Current);
            _worldAdministrationProbeUx9C = probe.Snapshot;
            _worldAdministrationAuditUx9C.AddRange(probe.Audit);
            var succeeded = probe.Instances && probe.DynamicAreas && probe.Authentication &&
                            probe.Administration && probe.Moderation && probe.Snapshot.ResourcesReleased;
            return new(succeeded, succeeded
                ? "Sonde locale terminée · services en mémoire libérés, aucune API distante ouverte."
                : "La sonde locale n’a pas validé les cinq autorités.");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                               UnauthorizedAccessException or InvalidDataException)
        {
            _worldAdministrationAuditUx9C.Add($"rejected:studio.network.world-administration:{exception.Message}");
            return new(false, $"La sonde locale a échoué : {exception.Message}");
        }
    }

    internal StudioUx9CApprovalSnapshot CaptureWorldAdministrationApprovalUx9C() =>
        new(_approvedWorldAdministrationUx9C, _approvedWorldAdministrationFingerprintUx9C);

    internal void RestoreWorldAdministrationApprovalUx9C(StudioUx9CApprovalSnapshot snapshot)
    {
        _approvedWorldAdministrationUx9C = snapshot.Draft;
        _approvedWorldAdministrationFingerprintUx9C = snapshot.DraftFingerprint;
    }

    internal void CancelWorldAdministrationApprovalUx9C()
    {
        _approvedWorldAdministrationUx9C = null;
        _approvedWorldAdministrationFingerprintUx9C = null;
    }

    internal void CloseUx9C()
    {
        CancelWorldAdministrationApprovalUx9C();
        _worldAdministrationHistoryUx9C = null;
        _persistedWorldAdministrationUx9C = null;
        _worldAdministrationWorkspaceRootUx9C = null;
        _worldAdministrationLoadMessageUx9C = null;
        _worldAdministrationFileMissingUx9C = false;
        _worldAdministrationProbeUx9C = EmptyProbeUx9C();
        _worldAdministrationAuditUx9C.Clear();
    }

    internal StudioUx9CSmokeResult RunUx9CContractSmoke()
    {
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceSession = Session;
        if (sourceProject is null || sourceHistory is null || sourceSession is null) return FailedSmokeUx9C();
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var sourceApproval = CaptureWorldAdministrationApprovalUx9C();
        var temporaryRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux9-c-{Guid.NewGuid():N}");
        StudioSessionController? editing = null;
        StudioSessionController? reopened = null;
        var released = false;
        var fiveRoutes = false;
        var explicitSelection = false;
        var invalidBlocked = false;
        var permissionBlocked = false;
        var confirmationRequired = false;
        var canonicalDryRun = false;
        var canonicalCommand = false;
        var staleSource = false;
        var staleApproval = false;
        var undoRedo = false;
        var persistence = false;
        var instances = false;
        var dynamicAreas = false;
        var authentication = false;
        var administration = false;
        var moderation = false;
        var audit = false;
        try
        {
            if (!ProjectSession.Create(temporaryRoot, sourceProject).Succeeded) return FailedSmokeUx9C();
            var baseline = NetworkWorldAdministrationProfile.CreateDefault("evidence:ux9-c");
            if (!WorkspaceNetworkWorldAdministrationStore.Save(temporaryRoot, baseline).Succeeded)
                return FailedSmokeUx9C();
            editing = new StudioSessionController();
            if (!editing.Open(temporaryRoot).Succeeded) return FailedSmokeUx9C();
            var draft = editing.CreateWorldAdministrationDraftUx9C(explicitlySelected: true);
            explicitSelection = draft is not null;
            if (draft is null) return FailedSmokeUx9C();
            var fixture = draft with
            {
                TemplateIds = "area:crypt-template, area:forest-template",
                MaximumInstances = 24,
                MaximumPlayersPerInstance = 12,
                GarbageCollectionMinutes = 10,
                AllowedOverrideKeys = "environment, encounters, entry-point, lighting",
                MaximumEntities = 1_500,
                MaximumResourceMegabytes = 768,
                SessionLifetimeMinutes = 180,
                OAuthEnabled = false,
                AnonymousEnabled = false,
                RequestsPerMinute = 90,
                SafeCommandIds = "operations.inspect, player.kick",
                ModerationRetentionDays = 120
            };

            invalidBlocked = !editing.PreviewWorldAdministrationUx9C(fixture with
            {
                MaximumPlayersPerInstance = 100
            }).Succeeded;
            staleSource = !editing.PreviewWorldAdministrationUx9C(fixture with
            {
                BaseServerRevision = fixture.BaseServerRevision + 9
            }).Succeeded;
            var preview = editing.PreviewWorldAdministrationUx9C(fixture);
            canonicalDryRun = preview.Succeeded && preview.CanApply && preview.CanonicalDryRun;
            instances = preview.InstancesRuntime;
            dynamicAreas = preview.DynamicAreasRuntime;
            authentication = preview.AuthenticationRuntime;
            administration = preview.AdministrationRuntime;
            moderation = preview.ModerationRuntime;
            staleApproval = !editing.ApplyApprovedWorldAdministrationUx9C(fixture with
            {
                RequestsPerMinute = fixture.RequestsPerMinute + 1
            }).Succeeded;
            preview = editing.PreviewWorldAdministrationUx9C(fixture);
            var applied = editing.ApplyApprovedWorldAdministrationUx9C(fixture);
            canonicalCommand = preview.CanApply && applied.Succeeded;
            var current = editing.CurrentWorldAdministrationUx9C;
            if (current is null) return FailedSmokeUx9C();
            fiveRoutes = current.Instances.MaximumInstances == 24 &&
                         current.Instances.MaximumPlayersPerInstance == 12 &&
                         current.DynamicAreas.AllowedOverrideKeys.Contains("lighting") &&
                         current.Authentication.SessionLifetimeMinutes == 180 &&
                         !current.Authentication.Providers.Contains(AuthenticationProviderKind.Anonymous) &&
                         current.Administration.RequestsPerMinute == 90 &&
                         current.Administration.SafeCommandIds.SequenceEqual(["operations.inspect", "player.kick"]) &&
                         current.Moderation.PrivacyRetentionDays == 120;

            var undone = editing.UndoWorldAdministrationUx9C();
            var afterUndo = editing.CurrentWorldAdministrationUx9C;
            var redone = editing.RedoWorldAdministrationUx9C();
            var afterRedo = editing.CurrentWorldAdministrationUx9C;
            undoRedo = undone.Succeeded && redone.Succeeded && afterUndo is not null && afterRedo is not null &&
                       afterUndo.ServerRevision == baseline.ServerRevision &&
                       afterRedo.ServerRevision == current.ServerRevision;
            var saved = editing.SaveWorldAdministrationUx9C();
            var loaded = WorkspaceNetworkWorldAdministrationStore.Load(temporaryRoot);
            var serializer = new NetworkWorldAdministrationStore();
            persistence = saved.Succeeded && loaded.Succeeded && !loaded.FileMissing &&
                          string.Equals(serializer.Serialize(afterRedo!), serializer.Serialize(loaded.Profile),
                              StringComparison.Ordinal);
            reopened = new StudioSessionController();
            persistence = persistence && reopened.Open(temporaryRoot).Succeeded &&
                          reopened.CreateWorldAdministrationDraftUx9C(explicitlySelected: true) is { } reopenedDraft &&
                          reopenedDraft.BaseServerRevision == afterRedo!.ServerRevision &&
                          string.Equals(reopenedDraft.SourceProfileFingerprint, ProfileFingerprintUx9C(afterRedo),
                              StringComparison.Ordinal);

            confirmationRequired = !editing.RunLocalWorldAdministrationUx9C(false,
                NetworkRole.Administrator).Succeeded;
            permissionBlocked = !editing.RunLocalWorldAdministrationUx9C(true, NetworkRole.Player).Succeeded;
            var tested = editing.RunLocalWorldAdministrationUx9C(true, NetworkRole.Administrator);
            var probe = editing.WorldAdministrationProbeUx9C;
            instances = instances && tested.Succeeded && probe.InstanceCode == "NET_ZONE_INSTANCE_CREATED" &&
                        probe.InstancePlayers == 1;
            dynamicAreas = dynamicAreas && probe.DynamicAreaCode == "NET_ZONE_INSTANCE_CREATED";
            authentication = authentication && probe.AuthenticationCode == "NET_AUTHENTICATED" &&
                             probe.NonEnumeratingFailure;
            administration = administration && probe.AdministrationCode == "NET_ADMIN_APPLIED" &&
                             probe.UnsafeCommandBlocked;
            moderation = moderation && probe.ModerationCode == "NET_MODERATION_APPLIED" &&
                         probe.ModerationAuditEntries == 1;
            audit = editing.WorldAdministrationAuditUx9C.Count >= 10 && probe.AuditEntries >= 8 &&
                    probe.ResourcesReleased;
        }
        finally
        {
            reopened?.Close();
            editing?.Close();
            released = DeleteUx9CTemporaryWorkspace(temporaryRoot);
            _lastUx9CTemporaryReleased = released;
            RestoreWorldAdministrationApprovalUx9C(sourceApproval);
        }
        var sourceUnchanged = ReferenceEquals(sourceProject, Project) && ReferenceEquals(sourceHistory, History) &&
                              History?.State.Revision == sourceRevision && History.Journal.SequenceEqual(sourceJournal) &&
                              IsDirty == sourceDirty && ActiveAreaId == sourceArea &&
                              SelectedEntityIds.SequenceEqual(sourceSelection) && SelectedEntityId == sourceSelected &&
                              CaptureWorldAdministrationApprovalUx9C() == sourceApproval;
        var succeeded = fiveRoutes && explicitSelection && invalidBlocked && permissionBlocked &&
                        confirmationRequired && canonicalDryRun && canonicalCommand && staleSource && staleApproval &&
                        undoRedo && persistence && instances && dynamicAreas && authentication && administration &&
                        moderation && audit && sourceUnchanged && released;
        return new(succeeded, fiveRoutes, explicitSelection, invalidBlocked, permissionBlocked,
            confirmationRequired, canonicalDryRun, canonicalCommand, staleSource, staleApproval, undoRedo,
            persistence, instances, dynamicAreas, authentication, administration, moderation, audit,
            sourceUnchanged, released);
    }

    private bool EnsureWorldAdministrationUx9C()
    {
        var root = Session?.RootDirectory;
        if (string.IsNullOrWhiteSpace(root))
        {
            _worldAdministrationLoadMessageUx9C = "Ouvrez d’abord un module.";
            return false;
        }
        var fullRoot = Path.GetFullPath(root);
        if (_worldAdministrationHistoryUx9C is not null &&
            string.Equals(_worldAdministrationWorkspaceRootUx9C, fullRoot, StringComparison.OrdinalIgnoreCase))
            return true;
        var loaded = WorkspaceNetworkWorldAdministrationStore.Load(fullRoot);
        if (!loaded.Succeeded)
        {
            _worldAdministrationHistoryUx9C = null;
            _persistedWorldAdministrationUx9C = null;
            _worldAdministrationWorkspaceRootUx9C = fullRoot;
            _worldAdministrationFileMissingUx9C = false;
            _worldAdministrationLoadMessageUx9C =
                $"Administration réseau invalide : {loaded.Diagnostics.FirstOrDefault()?.Message ?? "diagnostic inconnu"}";
            return false;
        }
        _worldAdministrationHistoryUx9C = new NetworkWorldAdministrationHistory(loaded.Profile);
        _persistedWorldAdministrationUx9C = loaded.Profile;
        _worldAdministrationWorkspaceRootUx9C = fullRoot;
        _worldAdministrationFileMissingUx9C = loaded.FileMissing;
        _worldAdministrationLoadMessageUx9C = loaded.FileMissing
            ? "Aucune administration réseau enregistrée · le profil initial est affiché sans écriture implicite."
            : null;
        return true;
    }

    private static NetworkWorldAdministrationProfile BuildCandidateUx9C(
        NetworkWorldAdministrationProfile current, StudioUx9CWorldAdministrationDraft draft)
    {
        var providers = new List<AuthenticationProviderKind>
        {
            AuthenticationProviderKind.Local,
            AuthenticationProviderKind.OpenIdConnect,
            AuthenticationProviderKind.Platform
        };
        if (draft.OAuthEnabled) providers.Add(AuthenticationProviderKind.OAuth);
        if (draft.AnonymousEnabled) providers.Add(AuthenticationProviderKind.Anonymous);
        return current with
        {
            ServerRevision = current.ServerRevision + 1,
            Provenance = "studio:ux9-c",
            RequiredAtRuntime = true,
            Instances = current.Instances with
            {
                TemplateIds = ParseTokensUx9C(draft.TemplateIds),
                MaximumInstances = draft.MaximumInstances,
                MaximumPlayersPerInstance = draft.MaximumPlayersPerInstance,
                PartyAllocation = true,
                PersistenceEnabled = true,
                RuntimeSceneInstantiation = true,
                GarbageCollectionMinutes = draft.GarbageCollectionMinutes
            },
            DynamicAreas = current.DynamicAreas with
            {
                TemplateInstantiation = true,
                CloneExistingArea = true,
                AllowedOverrideKeys = ParseTokensUx9C(draft.AllowedOverrideKeys),
                MaximumEntities = draft.MaximumEntities,
                MaximumResourceMegabytes = draft.MaximumResourceMegabytes,
                ServerAuthorityRequired = true
            },
            Authentication = current.Authentication with
            {
                Providers = providers,
                StableAccountId = true,
                PrivacyByDefault = true,
                SecretsHashed = true,
                NonEnumeratingFailures = true,
                SessionLifetimeMinutes = draft.SessionLifetimeMinutes
            },
            Administration = current.Administration with
            {
                ApiEnabled = true,
                CliEnabled = true,
                AuthenticationRequired = true,
                RequestsPerMinute = draft.RequestsPerMinute,
                AuditEnabled = true,
                HealthCommands = true,
                PlayerCommands = true,
                ConfigurationCommands = true,
                SafeCommandIds = ParseTokensUx9C(draft.SafeCommandIds)
            },
            Moderation = current.Moderation with
            {
                AccountAdapter = true,
                IpAdapter = true,
                DeviceAdapter = true,
                Mute = true,
                Timeout = true,
                Kick = true,
                Ban = true,
                Reports = true,
                Evidence = true,
                AppealNotes = true,
                PrivacyRetentionDays = draft.ModerationRetentionDays
            }
        };
    }

    private static WorldAdministrationProbeResultUx9C RunWorldAdministrationProbeUx9C(
        NetworkWorldAdministrationProfile profile)
    {
        var runtime = NetworkWorldAdministrationRuntimeAdapter.Build(profile);
        var audit = new List<string>();
        var template = new ZoneTemplate(Guid.NewGuid(), profile.Instances.TemplateIds[0],
            profile.Instances.MaximumInstances, profile.Instances.MaximumPlayersPerInstance, true);
        var templateDiagnostics = runtime.Instances.RegisterTemplate(template);
        var party = Guid.NewGuid();
        var created = runtime.Instances.Create(template.TemplateId, ZoneLifecycle.Session, party);
        var entered = created.Instance is null
            ? new WorldDecision(false, "NET_ZONE_INSTANCE_UNKNOWN", 0, "rejected")
            : runtime.Instances.Enter(created.Instance.InstanceId, Guid.NewGuid(), party, 1);
        audit.Add(created.Decision.AuditEntry);
        audit.Add(entered.AuditEntry);
        var instancePlayers = created.Instance is null ? 0 :
            runtime.Instances.Find(created.Instance.InstanceId)?.PlayerCharacterIds.Count ?? 0;
        var instancesReady = templateDiagnostics.Count == 0 && created.Decision.Accepted && entered.Accepted &&
                             instancePlayers == 1;

        var dynamic = runtime.DynamicAreas.Create(new(template.TemplateId, true,
            Math.Min(10, profile.DynamicAreas.MaximumEntities),
            Math.Min(32, profile.DynamicAreas.MaximumResourceMegabytes),
            new Dictionary<string, string>
            {
                [profile.DynamicAreas.AllowedOverrideKeys[0]] = "studio-probe"
            }, party));
        audit.Add(dynamic.AuditEntry);
        var dynamicReady = dynamic.Accepted && dynamic.Instance is not null;

        const string login = "ux9c.operator";
        const string password = "safe-password-2026";
        var account = runtime.Accounts.Register(login, password);
        var authenticated = runtime.Accounts.Authenticate(login, password);
        var missing = runtime.Accounts.Authenticate("missing.operator", password);
        var wrong = runtime.Accounts.Authenticate(login, "wrong-password-2026");
        var nonEnumerating = missing.Code == "NET_AUTH_INVALID_CREDENTIALS" && wrong.Code == missing.Code;
        var authenticationReady = authenticated.Authenticated && authenticated.AccountId == account.AccountId &&
                                  nonEnumerating;
        audit.Add(authenticated.AuditEntry);
        audit.Add(missing.AuditEntry);
        audit.Add(wrong.AuditEntry);

        var known = new[] { "operations.inspect", "player.kick", "player.ban", "world.save", "world.rollback" };
        var safeCommand = profile.Administration.SafeCommandIds.FirstOrDefault(known.Contains);
        AdministrationResult preview;
        AdministrationResult applied;
        if (safeCommand is null)
        {
            preview = new(false, "NET_ADMIN_COMMAND_UNKNOWN", 0, "no changes", "rejected");
            applied = preview;
        }
        else
        {
            var admin = new AuthorizationSubject(Guid.NewGuid(), NetworkRole.Administrator, new HashSet<Guid>());
            var command = new AdministrationCommand(Guid.NewGuid(), admin.AccountId, safeCommand, null, null, 0,
                new Dictionary<string, string>());
            preview = runtime.Administration.Execute(admin, command, false);
            applied = runtime.Administration.Execute(admin, command, true);
            var unsafeKind = known.FirstOrDefault(item => !profile.Administration.SafeCommandIds.Contains(item)) ??
                             "unlisted.command";
            var unsafeResult = runtime.Administration.Execute(admin,
                new(Guid.NewGuid(), admin.AccountId, unsafeKind, null, null, 1,
                    new Dictionary<string, string>()), false);
            var unsafeBlocked = unsafeResult.Code == "NET_ADMIN_COMMAND_NOT_ALLOWED";
            audit.Add(preview.AuditEntry);
            audit.Add(applied.AuditEntry);
            audit.Add(unsafeResult.AuditEntry);

            var moderator = new AuthorizationSubject(Guid.NewGuid(), NetworkRole.Moderator,
                new HashSet<Guid>());
            var moderated = runtime.Moderation.Apply(moderator, ModerationActionKind.Mute,
                account.AccountId, "studio:ux9-c-evidence", TimeSpan.FromMinutes(5));
            audit.Add(moderated.AuditEntry);
            var moderationCount = runtime.Moderation.AuditFor(account.AccountId).Count;
            var moderationReady = moderated.Applied &&
                                  runtime.Moderation.IsRestricted(account.AccountId, ModerationActionKind.Mute) &&
                                  moderationCount == 1;
            var administrationReady = preview.Code == "NET_ADMIN_PREVIEW" && applied.Applied && unsafeBlocked;
            var snapshot = new StudioUx9CProbeSnapshot(
                instancesReady && dynamicReady && authenticationReady && administrationReady && moderationReady
                    ? "Cinq sondes locales validées"
                    : "Sonde locale incomplète",
                created.Decision.Code, instancePlayers, dynamic.Code, authenticated.Code, nonEnumerating,
                applied.Code, unsafeBlocked, moderated.Code, moderationCount, audit.Count, true);
            return new(instancesReady, dynamicReady, authenticationReady, administrationReady, moderationReady,
                snapshot, audit);
        }

        var failedSnapshot = new StudioUx9CProbeSnapshot("Sonde locale incomplète", created.Decision.Code,
            instancePlayers, dynamic.Code, authenticated.Code, nonEnumerating, applied.Code, false,
            "NET_MODERATION_NOT_TESTED", 0, audit.Count, true);
        return new(instancesReady, dynamicReady, authenticationReady, false, false, failedSnapshot, audit);
    }

    private static StudioUx9CWorldAdministrationDraft NormalizeDraftUx9C(
        StudioUx9CWorldAdministrationDraft draft) => draft with
        {
            TemplateIds = NormalizeTokensUx9C(draft.TemplateIds),
            AllowedOverrideKeys = NormalizeTokensUx9C(draft.AllowedOverrideKeys),
            SafeCommandIds = NormalizeTokensUx9C(draft.SafeCommandIds)
        };

    private static string NormalizeTokensUx9C(string value) => string.Join(", ", ParseTokensUx9C(value));
    private static IReadOnlyList<string> ParseTokensUx9C(string value) => value
        .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal).ToArray();
    private static string ProfileFingerprintUx9C(NetworkWorldAdministrationProfile profile) =>
        Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(new NetworkWorldAdministrationStore().Serialize(profile))));
    private static string DraftFingerprintUx9C(StudioUx9CWorldAdministrationDraft draft) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(draft))));

    private static StudioUx9CWorldAdministrationPreview FailedWorldAdministrationPreviewUx9C(string message,
        IReadOnlyList<NetworkDiagnostic>? diagnostics = null) =>
        new(false, false, false, false, false, false, false, false, message, [],
            diagnostics?.Select(item => HumanWorldAdministrationDiagnosticUx9C(item.Code))
                .Distinct(StringComparer.Ordinal).ToArray() ?? [], EmptyProbeUx9C());

    private static string HumanWorldAdministrationDiagnosticsUx9C(
        IReadOnlyList<NetworkDiagnostic> diagnostics) => diagnostics.Count == 0
        ? "La commande réseau n’a produit aucun changement applicable."
        : string.Join(" · ", diagnostics.Select(item => HumanWorldAdministrationDiagnosticUx9C(item.Code))
            .Distinct(StringComparer.Ordinal));

    private static string HumanWorldAdministrationDiagnosticUx9C(string? code) => code switch
    {
        "NET_AREA_INSTANCE_POLICY_INVALID" => "Les instances exigent templates, plafonds de 128/64, allocation, persistance et collecte bornée.",
        "NET_DYNAMIC_AREA_POLICY_INVALID" => "Les zones dynamiques exigent template, clone, surcharges et budgets serveur bornés.",
        "NET_AUTHENTICATION_POLICY_INVALID" => "L’authentification exige Local, OpenID Connect, Platform, identité stable, secrets hachés et erreurs privées.",
        "NET_REMOTE_ADMIN_POLICY_INVALID" => "L’administration exige authentification, débit borné, audit et commandes sûres.",
        "NET_MODERATION_POLICY_INVALID" => "La modération exige adaptateurs, actions, preuves, appels et rétention bornée.",
        "NET_WORLD_ADMIN_PERMISSION_DENIED" => "La permission Administrateur est requise.",
        "NET_WORLD_ADMIN_REVISION_STALE" => "La révision serveur a changé ; rechargez le formulaire.",
        _ => string.IsNullOrWhiteSpace(code) ? "La configuration réseau est invalide." : code
    };

    private static string HumanWorldAdministrationDiffUx9C(string value) => value switch
    {
        "instances" => "Instances de zones",
        "dynamic-areas" => "Création dynamique de zone",
        "authentication" => "Authentification et comptes",
        "administration" => "Administration locale gouvernée",
        "moderation" => "Modération",
        _ => value
    };

    private static StudioUx9CProbeSnapshot EmptyProbeUx9C() =>
        new("Aucune sonde exécutée", "—", 0, "—", "—", false, "—", false, "—", 0, 0, true);
    private static StudioUx9CSmokeResult FailedSmokeUx9C() =>
        new(false, false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false);

    private static bool DeleteUx9CTemporaryWorkspace(string root)
    {
        try
        {
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            var parent = Path.GetDirectoryName(fullRoot)?.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            if (!string.Equals(parent, temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(fullRoot).StartsWith("openrpg-forge-studio-ux9-c-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(fullRoot)) Directory.Delete(fullRoot, recursive: true);
            return !Directory.Exists(fullRoot);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private sealed record WorldAdministrationProbeResultUx9C(bool Instances, bool DynamicAreas,
        bool Authentication, bool Administration, bool Moderation, StudioUx9CProbeSnapshot Snapshot,
        IReadOnlyList<string> Audit);
}
