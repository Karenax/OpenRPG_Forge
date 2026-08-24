using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.ModuleFormat.Network;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Authoring;
using Forge.Network.Chat;
using Forge.Network.Content;
using Forge.Network.Events;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private NetworkOperationsHistory? _networkOperationsHistoryUx9B;
    private NetworkOperationsProfile? _persistedNetworkOperationsUx9B;
    private string? _networkOperationsWorkspaceRootUx9B;
    private StudioUx9BOperationsDraft? _approvedNetworkOperationsUx9B;
    private string? _approvedNetworkOperationsFingerprintUx9B;
    private string? _networkOperationsLoadMessageUx9B;
    private bool _networkOperationsFileMissingUx9B;
    private StudioUx9BProbeSnapshot _networkOperationsProbeUx9B = EmptyProbeUx9B();
    private readonly List<string> _networkOperationsAuditUx9B = [];
    private bool _lastUx9BTemporaryReleased = true;

    internal string? NetworkOperationsLoadMessageUx9B => _networkOperationsLoadMessageUx9B;
    internal bool NetworkOperationsFileMissingUx9B => _networkOperationsFileMissingUx9B;
    internal bool LastUx9BTemporaryReleased => _lastUx9BTemporaryReleased;
    internal StudioUx9BProbeSnapshot NetworkOperationsProbeUx9B => _networkOperationsProbeUx9B;
    internal IReadOnlyList<string> NetworkOperationsAuditUx9B => _networkOperationsAuditUx9B.ToArray();
    internal NetworkOperationsProfile? CurrentNetworkOperationsUx9B =>
        EnsureNetworkOperationsUx9B() ? _networkOperationsHistoryUx9B!.Current : null;
    internal bool NetworkOperationsHasUnsavedChangesUx9B =>
        EnsureNetworkOperationsUx9B() && _persistedNetworkOperationsUx9B is not null &&
        !string.Equals(ProfileFingerprintUx9B(_networkOperationsHistoryUx9B!.Current),
            ProfileFingerprintUx9B(_persistedNetworkOperationsUx9B), StringComparison.Ordinal);

    internal StudioUx9BOperationsDraft? CreateNetworkOperationsDraftUx9B(bool explicitlySelected = false,
        NetworkRole editedRole = NetworkRole.Builder)
    {
        if (!explicitlySelected || !EnsureNetworkOperationsUx9B()) return null;
        var profile = _networkOperationsHistoryUx9B!.Current;
        var grant = profile.Roles.Grants.Single(item => item.Role == editedRole);
        var filter = profile.Chat.LanguageFilter.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        var subscription = profile.Events.Subscriptions.OrderByDescending(item => item.Priority)
            .ThenBy(item => item.SubscriberId, StringComparer.Ordinal).First();
        return new(
            profile.ServerRevision,
            ProfileFingerprintUx9B(profile),
            profile.Content.ChunkBytes / 1024,
            checked((int)(profile.Content.MaximumCacheBytes / 1_048_576)),
            profile.Content.DeltaEnabled,
            string.Join(", ", profile.Content.AllowedLicenses),
            profile.VersionLock.ModuleId,
            profile.VersionLock.ModuleVersion,
            profile.VersionLock.ProtocolVersion,
            profile.VersionLock.ContentFingerprint,
            grant.Role,
            string.Join(", ", grant.Capabilities),
            string.Join(", ", grant.CustomCapabilities),
            grant.ZoneScoped,
            checked((int)profile.Chat.LocalRadius),
            profile.Chat.MaximumCharacters,
            string.Join(", ", profile.Chat.AllowedLanguages),
            filter.Key ?? "spoiler",
            filter.Value ?? "•••••••",
            profile.Chat.Channels.Contains(ChatChannelKind.Global),
            profile.Chat.Channels.Contains(ChatChannelKind.System),
            profile.Events.MaximumPendingEvents,
            subscription.EventKind,
            subscription.SubscriberId,
            subscription.Priority);
    }

    internal StudioUx9BOperationsPreview PreviewNetworkOperationsUx9B(StudioUx9BOperationsDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelNetworkOperationsApprovalUx9B();
        if (!EnsureNetworkOperationsUx9B())
            return FailedNetworkOperationsPreviewUx9B(
                _networkOperationsLoadMessageUx9B ?? "Les opérations réseau ne peuvent pas être chargées.");

        var current = _networkOperationsHistoryUx9B!.Current;
        if (draft.BaseServerRevision != current.ServerRevision ||
            !string.Equals(draft.SourceProfileFingerprint, ProfileFingerprintUx9B(current),
                StringComparison.Ordinal))
            return FailedNetworkOperationsPreviewUx9B(
                "La configuration a changé ; rechargez le formulaire avant l’aperçu sec.");

        var normalized = NormalizeDraftUx9B(draft);
        if (!TryParseCapabilitiesUx9B(normalized.Capabilities, out _) ||
            ParseTokensUx9B(normalized.AllowedLicenses).Count == 0 ||
            ParseTokensUx9B(normalized.AllowedLanguages).Count == 0 ||
            string.IsNullOrWhiteSpace(normalized.FilterToken) ||
            string.IsNullOrWhiteSpace(normalized.FilterReplacement))
            return FailedNetworkOperationsPreviewUx9B(
                "Renseignez des capacités connues, au moins une licence et une langue, puis un filtre non vide.");

        var candidate = BuildCandidateUx9B(current, normalized);
        var command = new NetworkOperationsCommand(Guid.NewGuid(), 1, "studio:ux9-b",
            current.ServerRevision, "studio.network.operator", NetworkRole.Administrator, false, candidate);
        var dryRun = _networkOperationsHistoryUx9B.Execute(command);
        if (!dryRun.Succeeded || dryRun.Applied)
            return FailedNetworkOperationsPreviewUx9B(
                HumanNetworkOperationsDiagnosticsUx9B(dryRun.Diagnostics), dryRun.Diagnostics);

        OperationsProbeUx9B probe;
        try
        {
            probe = RunNetworkOperationsProbeUx9B(candidate);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                               IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return FailedNetworkOperationsPreviewUx9B(
                $"La sonde locale a refusé la configuration : {exception.Message}");
        }

        _networkOperationsProbeUx9B = probe.Snapshot;
        var changes = dryRun.SemanticDiff == "revision-only"
            ? Array.Empty<string>()
            : dryRun.SemanticDiff.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var runtimeReady = probe.Content && probe.Version && probe.Roles && probe.Chat && probe.Events &&
                           probe.Snapshot.ResourcesReleased;
        var canApply = runtimeReady && changes.Length > 0;
        if (canApply)
        {
            _approvedNetworkOperationsUx9B = normalized with { };
            _approvedNetworkOperationsFingerprintUx9B = DraftFingerprintUx9B(_approvedNetworkOperationsUx9B);
        }

        return new(true, canApply, true, probe.Content, probe.Version, probe.Roles, probe.Chat, probe.Events,
            changes.Length == 0
                ? "Aperçu sec validé · le profil est déjà identique."
                : "Aperçu sec et cinq sondes locales validés · aucune connexion distante ouverte.",
            changes.Select(HumanOperationsDiffUx9B).ToArray(), [], probe.Snapshot);
    }

    internal StudioUx9BActionResult ApplyApprovedNetworkOperationsUx9B(StudioUx9BOperationsDraft currentDraft)
    {
        var approved = _approvedNetworkOperationsUx9B;
        if (approved is null || _approvedNetworkOperationsFingerprintUx9B is null ||
            !EnsureNetworkOperationsUx9B())
        {
            CancelNetworkOperationsApprovalUx9B();
            return new(false, "Prévisualisez d’abord les opérations réseau.");
        }

        var current = _networkOperationsHistoryUx9B!.Current;
        var normalizedCurrent = NormalizeDraftUx9B(currentDraft);
        if (!string.Equals(DraftFingerprintUx9B(normalizedCurrent), _approvedNetworkOperationsFingerprintUx9B,
                StringComparison.Ordinal) ||
            !string.Equals(DraftFingerprintUx9B(approved), _approvedNetworkOperationsFingerprintUx9B,
                StringComparison.Ordinal) ||
            approved.BaseServerRevision != current.ServerRevision ||
            !string.Equals(approved.SourceProfileFingerprint, ProfileFingerprintUx9B(current),
                StringComparison.Ordinal))
        {
            CancelNetworkOperationsApprovalUx9B();
            return new(false, "La source ou le brouillon a changé ; relancez l’aperçu sec.");
        }

        var candidate = BuildCandidateUx9B(current, approved);
        var command = new NetworkOperationsCommand(Guid.NewGuid(), 1, "studio:ux9-b",
            current.ServerRevision, "studio.network.operator", NetworkRole.Administrator, false, candidate);
        var dryRun = _networkOperationsHistoryUx9B.Execute(command);
        if (!dryRun.Succeeded || dryRun.Applied || dryRun.SemanticDiff == "revision-only")
        {
            CancelNetworkOperationsApprovalUx9B();
            return new(false, HumanNetworkOperationsDiagnosticsUx9B(dryRun.Diagnostics));
        }

        var applied = _networkOperationsHistoryUx9B.Execute(command with { Apply = true });
        CancelNetworkOperationsApprovalUx9B();
        if (!applied.Succeeded || !applied.Applied)
            return new(false, HumanNetworkOperationsDiagnosticsUx9B(applied.Diagnostics));
        _networkOperationsAuditUx9B.Add(applied.AuditEntry);
        return new(true, "Opérations réseau appliquées par la commande canonique · enregistrez pour persister.");
    }

    internal StudioUx9BActionResult UndoNetworkOperationsUx9B()
    {
        CancelNetworkOperationsApprovalUx9B();
        if (!EnsureNetworkOperationsUx9B())
            return new(false, _networkOperationsLoadMessageUx9B ?? "Aucune configuration active.");
        var result = _networkOperationsHistoryUx9B!.Undo("studio.network.operator", NetworkRole.Administrator);
        if (result.Succeeded) _networkOperationsAuditUx9B.Add(result.AuditEntry);
        return new(result.Succeeded && result.Applied,
            result.Succeeded ? "Modification réseau annulée." : HumanNetworkOperationsDiagnosticsUx9B(result.Diagnostics));
    }

    internal StudioUx9BActionResult RedoNetworkOperationsUx9B()
    {
        CancelNetworkOperationsApprovalUx9B();
        if (!EnsureNetworkOperationsUx9B())
            return new(false, _networkOperationsLoadMessageUx9B ?? "Aucune configuration active.");
        var result = _networkOperationsHistoryUx9B!.Redo("studio.network.operator", NetworkRole.Administrator);
        if (result.Succeeded) _networkOperationsAuditUx9B.Add(result.AuditEntry);
        return new(result.Succeeded && result.Applied,
            result.Succeeded ? "Modification réseau rétablie." : HumanNetworkOperationsDiagnosticsUx9B(result.Diagnostics));
    }

    internal StudioUx9BActionResult SaveNetworkOperationsUx9B()
    {
        CancelNetworkOperationsApprovalUx9B();
        if (!EnsureNetworkOperationsUx9B() || Session is null)
            return new(false, _networkOperationsLoadMessageUx9B ?? "Aucune configuration active.");
        var result = WorkspaceNetworkOperationsStore.Save(Session.RootDirectory,
            _networkOperationsHistoryUx9B!.Current);
        if (!result.Succeeded)
            return new(false, result.Diagnostics.FirstOrDefault()?.Message ??
                              "Les opérations réseau n’ont pas été enregistrées.");
        _persistedNetworkOperationsUx9B = _networkOperationsHistoryUx9B.Current;
        _networkOperationsFileMissingUx9B = false;
        return new(true, "Opérations réseau enregistrées atomiquement dans le workspace.");
    }

    internal StudioUx9BActionResult RunLocalNetworkOperationsUx9B(bool confirmed, NetworkRole role)
    {
        if (!confirmed)
        {
            _networkOperationsAuditUx9B.Add("rejected:studio.network.operations:confirmation-required");
            return new(false, "Confirmez explicitement l’exécution de la sonde locale.");
        }
        if (role != NetworkRole.Administrator)
        {
            _networkOperationsAuditUx9B.Add("rejected:studio.network.operations:permission-denied");
            return new(false, "La permission Administrateur est requise pour cette sonde.");
        }
        if (!EnsureNetworkOperationsUx9B())
            return new(false, _networkOperationsLoadMessageUx9B ?? "Aucune configuration active.");
        try
        {
            var probe = RunNetworkOperationsProbeUx9B(_networkOperationsHistoryUx9B!.Current);
            _networkOperationsProbeUx9B = probe.Snapshot;
            _networkOperationsAuditUx9B.AddRange(probe.Audit);
            var succeeded = probe.Content && probe.Version && probe.Roles && probe.Chat && probe.Events &&
                            probe.Snapshot.ResourcesReleased;
            return new(succeeded, succeeded
                ? "Sonde locale terminée · cache temporaire libéré, aucun transport distant ouvert."
                : "La sonde locale n’a pas validé les cinq autorités.");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                               IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _networkOperationsAuditUx9B.Add($"rejected:studio.network.operations:{exception.Message}");
            return new(false, $"La sonde locale a échoué : {exception.Message}");
        }
    }

    internal StudioUx9BApprovalSnapshot CaptureNetworkOperationsApprovalUx9B() =>
        new(_approvedNetworkOperationsUx9B, _approvedNetworkOperationsFingerprintUx9B);

    internal void RestoreNetworkOperationsApprovalUx9B(StudioUx9BApprovalSnapshot snapshot)
    {
        _approvedNetworkOperationsUx9B = snapshot.Draft;
        _approvedNetworkOperationsFingerprintUx9B = snapshot.DraftFingerprint;
    }

    internal void CancelNetworkOperationsApprovalUx9B()
    {
        _approvedNetworkOperationsUx9B = null;
        _approvedNetworkOperationsFingerprintUx9B = null;
    }

    internal void CloseUx9B()
    {
        CancelNetworkOperationsApprovalUx9B();
        _networkOperationsHistoryUx9B = null;
        _persistedNetworkOperationsUx9B = null;
        _networkOperationsWorkspaceRootUx9B = null;
        _networkOperationsLoadMessageUx9B = null;
        _networkOperationsFileMissingUx9B = false;
        _networkOperationsProbeUx9B = EmptyProbeUx9B();
        _networkOperationsAuditUx9B.Clear();
    }

    internal StudioUx9BSmokeResult RunUx9BContractSmoke()
    {
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceSession = Session;
        if (sourceProject is null || sourceHistory is null || sourceSession is null) return FailedSmokeUx9B();

        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var sourceApproval = CaptureNetworkOperationsApprovalUx9B();
        var temporaryRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux9-b-{Guid.NewGuid():N}");
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
        var content = false;
        var version = false;
        var roles = false;
        var chat = false;
        var eventsReady = false;
        var audit = false;
        try
        {
            if (!ProjectSession.Create(temporaryRoot, sourceProject).Succeeded) return FailedSmokeUx9B();
            var baseline = NetworkOperationsProfile.CreateDefault("evidence:ux9-b");
            if (!WorkspaceNetworkOperationsStore.Save(temporaryRoot, baseline).Succeeded)
                return FailedSmokeUx9B();

            editing = new StudioSessionController();
            if (!editing.Open(temporaryRoot).Succeeded) return FailedSmokeUx9B();
            var draft = editing.CreateNetworkOperationsDraftUx9B(explicitlySelected: true);
            explicitSelection = draft is not null;
            if (draft is null) return FailedSmokeUx9B();
            var fixture = draft with
            {
                ChunkKilobytes = 32,
                MaximumCacheMegabytes = 256,
                DeltaEnabled = false,
                AllowedLicenses = "CC0-1.0, MIT",
                ModuleVersion = "1.1.0",
                ContentFingerprint = new string('c', 64),
                Capabilities = "Play, Chat, DirectMessage, BuildContent, ManageWorld, ManageEvents, ViewOperations, Observe",
                CustomCapabilities = "content.publish, world.inspect",
                LocalRadius = 42,
                MaximumCharacters = 640,
                AllowedLanguages = "common, elvish, dwarven",
                FilterToken = "spoiler",
                FilterReplacement = "[masqué]",
                AllowGlobal = false,
                MaximumPendingEvents = 16,
                EventKind = "quest.completed",
                SubscriberId = "gameplay.journal",
                EventPriority = 250
            };

            invalidBlocked = !editing.PreviewNetworkOperationsUx9B(fixture with { ChunkKilobytes = 1 }).Succeeded;
            staleSource = !editing.PreviewNetworkOperationsUx9B(fixture with
            {
                BaseServerRevision = fixture.BaseServerRevision + 9
            }).Succeeded;
            var preview = editing.PreviewNetworkOperationsUx9B(fixture);
            canonicalDryRun = preview.Succeeded && preview.CanApply && preview.CanonicalDryRun;
            content = preview.ContentRuntime;
            version = preview.VersionRuntime;
            roles = preview.RolesRuntime;
            chat = preview.ChatRuntime;
            eventsReady = preview.EventsRuntime;
            staleApproval = !editing.ApplyApprovedNetworkOperationsUx9B(fixture with
            {
                MaximumCharacters = fixture.MaximumCharacters + 1
            }).Succeeded;
            preview = editing.PreviewNetworkOperationsUx9B(fixture);
            var applied = editing.ApplyApprovedNetworkOperationsUx9B(fixture);
            canonicalCommand = preview.CanApply && applied.Succeeded;
            var current = editing.CurrentNetworkOperationsUx9B;
            if (current is null) return FailedSmokeUx9B();
            var builder = current.Roles.Grants.Single(item => item.Role == NetworkRole.Builder);
            fiveRoutes = current.Content.ChunkBytes == 32 * 1024 && !current.Content.DeltaEnabled &&
                         current.VersionLock.ModuleVersion == "1.1.0" &&
                         current.VersionLock.ContentFingerprint == new string('c', 64) &&
                         builder.CustomCapabilities.Contains("world.inspect") && builder.ZoneScoped &&
                         current.Chat.LocalRadius == 42 && current.Chat.MaximumCharacters == 640 &&
                         !current.Chat.Channels.Contains(ChatChannelKind.Global) &&
                         current.Events.MaximumPendingEvents == 16 &&
                         current.Events.Subscriptions.Any(item => item.EventKind == "quest.completed" &&
                             item.SubscriberId == "gameplay.journal" && item.Priority == 250);

            var undone = editing.UndoNetworkOperationsUx9B();
            var afterUndo = editing.CurrentNetworkOperationsUx9B;
            var redone = editing.RedoNetworkOperationsUx9B();
            var afterRedo = editing.CurrentNetworkOperationsUx9B;
            undoRedo = undone.Succeeded && redone.Succeeded && afterUndo is not null && afterRedo is not null &&
                       afterUndo.ServerRevision == baseline.ServerRevision &&
                       afterRedo.ServerRevision == current.ServerRevision;
            var saved = editing.SaveNetworkOperationsUx9B();
            var loaded = WorkspaceNetworkOperationsStore.Load(temporaryRoot);
            var serializer = new NetworkOperationsStore();
            persistence = saved.Succeeded && loaded.Succeeded && !loaded.FileMissing &&
                          string.Equals(serializer.Serialize(afterRedo!), serializer.Serialize(loaded.Profile),
                              StringComparison.Ordinal);
            reopened = new StudioSessionController();
            persistence = persistence && reopened.Open(temporaryRoot).Succeeded &&
                          reopened.CreateNetworkOperationsDraftUx9B(explicitlySelected: true) is { } reopenedDraft &&
                          reopenedDraft.BaseServerRevision == afterRedo!.ServerRevision &&
                          string.Equals(reopenedDraft.SourceProfileFingerprint, ProfileFingerprintUx9B(afterRedo),
                              StringComparison.Ordinal);

            confirmationRequired = !editing.RunLocalNetworkOperationsUx9B(false,
                NetworkRole.Administrator).Succeeded;
            permissionBlocked = !editing.RunLocalNetworkOperationsUx9B(true, NetworkRole.Player).Succeeded;
            var tested = editing.RunLocalNetworkOperationsUx9B(true, NetworkRole.Administrator);
            var probe = editing.NetworkOperationsProbeUx9B;
            content = content && tested.Succeeded && probe.ContentCode == "NET_CONTENT_SYNC_APPLIED" &&
                      probe.CacheReused;
            version = version && probe.VersionCode == "NET_VERSION_LOCK_MISMATCH";
            roles = roles && probe.PermissionCode == "NET_AUTH_ALLOWED";
            chat = chat && probe.ChatCode == "NET_CHAT_ACCEPTED" && probe.ChatMessages >= 1;
            eventsReady = eventsReady && probe.EventHandlers >= 2 && probe.EventTraces >= 2 &&
                          probe.ReplayBlocked;
            audit = editing.NetworkOperationsAuditUx9B.Count >= 10 && probe.AuditEntries >= 8 &&
                    probe.ResourcesReleased;
        }
        finally
        {
            reopened?.Close();
            editing?.Close();
            released = DeleteUx9BTemporaryWorkspace(temporaryRoot, "openrpg-forge-studio-ux9-b-") &&
                       _lastUx9BTemporaryReleased;
            _lastUx9BTemporaryReleased = released;
            RestoreNetworkOperationsApprovalUx9B(sourceApproval);
        }

        var sourceUnchanged = ReferenceEquals(sourceProject, Project) && ReferenceEquals(sourceHistory, History) &&
                              History?.State.Revision == sourceRevision && History.Journal.SequenceEqual(sourceJournal) &&
                              IsDirty == sourceDirty && ActiveAreaId == sourceArea &&
                              SelectedEntityIds.SequenceEqual(sourceSelection) && SelectedEntityId == sourceSelected &&
                              CaptureNetworkOperationsApprovalUx9B() == sourceApproval;
        var succeeded = fiveRoutes && explicitSelection && invalidBlocked && permissionBlocked &&
                        confirmationRequired && canonicalDryRun && canonicalCommand && staleSource && staleApproval &&
                        undoRedo && persistence && content && version && roles && chat && eventsReady && audit &&
                        sourceUnchanged && released;
        return new(succeeded, fiveRoutes, explicitSelection, invalidBlocked, permissionBlocked,
            confirmationRequired, canonicalDryRun, canonicalCommand, staleSource, staleApproval, undoRedo,
            persistence, content, version, roles, chat, eventsReady, audit, sourceUnchanged, released);
    }

    private bool EnsureNetworkOperationsUx9B()
    {
        var root = Session?.RootDirectory;
        if (string.IsNullOrWhiteSpace(root))
        {
            _networkOperationsLoadMessageUx9B = "Ouvrez d’abord un module.";
            return false;
        }

        var fullRoot = Path.GetFullPath(root);
        if (_networkOperationsHistoryUx9B is not null &&
            string.Equals(_networkOperationsWorkspaceRootUx9B, fullRoot, StringComparison.OrdinalIgnoreCase))
            return true;
        var loaded = WorkspaceNetworkOperationsStore.Load(fullRoot);
        if (!loaded.Succeeded)
        {
            _networkOperationsHistoryUx9B = null;
            _persistedNetworkOperationsUx9B = null;
            _networkOperationsWorkspaceRootUx9B = fullRoot;
            _networkOperationsFileMissingUx9B = false;
            _networkOperationsLoadMessageUx9B =
                $"Opérations réseau invalides : {loaded.Diagnostics.FirstOrDefault()?.Message ?? "diagnostic inconnu"}";
            return false;
        }

        _networkOperationsHistoryUx9B = new NetworkOperationsHistory(loaded.Profile);
        _persistedNetworkOperationsUx9B = loaded.Profile;
        _networkOperationsWorkspaceRootUx9B = fullRoot;
        _networkOperationsFileMissingUx9B = loaded.FileMissing;
        _networkOperationsLoadMessageUx9B = loaded.FileMissing
            ? "Aucune opération réseau enregistrée · le profil initial est affiché sans écriture implicite."
            : null;
        return true;
    }

    private static NetworkOperationsProfile BuildCandidateUx9B(NetworkOperationsProfile current,
        StudioUx9BOperationsDraft draft)
    {
        TryParseCapabilitiesUx9B(draft.Capabilities, out var capabilities);
        var custom = ParseTokensUx9B(draft.CustomCapabilities);
        var licenses = ParseTokensUx9B(draft.AllowedLicenses);
        var languages = ParseTokensUx9B(draft.AllowedLanguages)
            .Select(item => item.ToLowerInvariant()).ToArray();
        var requiredChannels = new[]
        {
            ChatChannelKind.Local, ChatChannelKind.Party, ChatChannelKind.Tell,
            ChatChannelKind.Shout, ChatChannelKind.DungeonMaster
        };
        var channels = requiredChannels
            .Concat(draft.AllowGlobal ? [ChatChannelKind.Global] : [])
            .Concat(draft.AllowSystem ? [ChatChannelKind.System] : []).Distinct().ToArray();
        var grants = current.Roles.Grants.Select(item => item.Role == draft.EditedRole
            ? item with
            {
                Capabilities = capabilities,
                CustomCapabilities = custom,
                ZoneScoped = draft.ZoneScoped
            }
            : item).ToArray();
        var primary = current.Events.Subscriptions.OrderByDescending(item => item.Priority)
            .ThenBy(item => item.SubscriberId, StringComparer.Ordinal).First();
        var subscriptions = current.Events.Subscriptions.Select(item => item.Id == primary.Id
            ? item with { EventKind = draft.EventKind, SubscriberId = draft.SubscriberId, Priority = draft.EventPriority }
            : item).ToArray();
        return current with
        {
            ServerRevision = current.ServerRevision + 1,
            Provenance = "studio:ux9-b",
            RequiredAtRuntime = true,
            Content = current.Content with
            {
                Resumable = true,
                DeduplicateByHash = true,
                VerifyHashes = true,
                DeltaEnabled = draft.DeltaEnabled,
                ChunkBytes = checked(draft.ChunkKilobytes * 1024),
                MaximumCacheBytes = checked((long)draft.MaximumCacheMegabytes * 1_048_576),
                AllowedLicenses = licenses,
                RequireTrustedSigner = true
            },
            VersionLock = current.VersionLock with
            {
                ModuleId = draft.ModuleId,
                ModuleVersion = draft.ModuleVersion,
                ProtocolVersion = draft.ProtocolVersion,
                ContentFingerprint = draft.ContentFingerprint,
                StrictBeforeJoin = true
            },
            Roles = current.Roles with { Grants = grants },
            Chat = current.Chat with
            {
                Channels = channels,
                LocalRadius = draft.LocalRadius,
                MaximumCharacters = draft.MaximumCharacters,
                AllowedLanguages = languages,
                LanguageFilter = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [draft.FilterToken] = draft.FilterReplacement
                },
                StructuredLogging = true,
                AccessibilityText = true,
                BridgeAdapters = ["audit-log"]
            },
            Events = current.Events with
            {
                MaximumPendingEvents = draft.MaximumPendingEvents,
                TraceEnabled = true,
                ReplayProtection = true,
                Subscriptions = subscriptions
            }
        };
    }

    private OperationsProbeUx9B RunNetworkOperationsProbeUx9B(NetworkOperationsProfile profile)
    {
        var probeRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux9-b-probe-{Guid.NewGuid():N}");
        var released = false;
        var contentCode = "NET_CONTENT_SYNC_FAILED";
        var contentChunks = 0;
        var cacheReused = false;
        var versionCode = "NET_VERSION_LOCK_NOT_TESTED";
        var permissionCode = "NET_AUTH_NOT_TESTED";
        var chatCode = "NET_CHAT_NOT_TESTED";
        var chatMessages = 0;
        var eventHandlers = 0;
        var eventTraces = 0;
        var replayBlocked = false;
        var audit = new List<string>();
        var contentReady = false;
        var versionReady = false;
        var rolesReady = false;
        var chatReady = false;
        var eventsReady = false;
        try
        {
            Directory.CreateDirectory(probeRoot);
            var bridge = new RecordingBridgeUx9B("audit-log");
            var runtime = NetworkOperationsRuntimeAdapter.Build(profile, TimeProvider.System, [bridge]);

            var bytes = Encoding.UTF8.GetBytes(new string('x', runtime.ContentChunkBytes * 2 + 17));
            var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            var signer = profile.Content.TrustedSignerFingerprints[0];
            var license = profile.Content.AllowedLicenses[0];
            var resource = new ContentRepositoryResource("modules/local/content.bin", bytes.Length, hash,
                license, "studio:ux9-b-probe");
            var manifest = ContentRepositoryManifestService.Create(Guid.NewGuid(), profile.VersionLock.ModuleId,
                profile.VersionLock.ModuleVersion, signer, [resource]);
            var partial = Path.Combine(probeRoot, profile.Content.CacheDirectory, "partial", $"{hash}.partial");
            Directory.CreateDirectory(Path.GetDirectoryName(partial)!);
            File.WriteAllBytes(partial, bytes[..11]);
            var sync = new ContentRepositorySyncService(runtime.ContentPolicy, probeRoot,
                runtime.ContentChunkBytes, runtime.ContentDeltaEnabled);
            var plan = sync.Plan(manifest);
            var applied = sync.Synchronize(manifest, new MemoryTransportUx9B(resource.Path, bytes));
            var reused = sync.Synchronize(manifest, new MemoryTransportUx9B(resource.Path, bytes));
            contentChunks = plan.Items.Single().ChunkCount;
            cacheReused = reused.Valid && reused.Applied && reused.Items.Single().CacheHit;
            contentReady = applied.Valid && applied.Applied && contentChunks >= 2 && cacheReused;
            contentCode = contentReady ? "NET_CONTENT_SYNC_APPLIED" : "NET_CONTENT_SYNC_FAILED";
            audit.Add($"{contentCode}:chunks={contentChunks}:cache_reused={cacheReused}");

            var compatible = VersionLockNegotiator.Check(runtime.VersionLock, runtime.VersionLock.ModuleId,
                runtime.VersionLock.ModuleVersion, runtime.VersionLock.ProtocolVersion,
                runtime.VersionLock.ContentFingerprint);
            var mismatch = VersionLockNegotiator.Check(runtime.VersionLock, runtime.VersionLock.ModuleId,
                runtime.VersionLock.ModuleVersion + ".mismatch", runtime.VersionLock.ProtocolVersion,
                runtime.VersionLock.ContentFingerprint);
            versionReady = compatible.Compatible && !mismatch.Compatible &&
                           mismatch.Code == "NET_VERSION_LOCK_MISMATCH";
            versionCode = mismatch.Code;
            audit.Add(compatible.AuditEntry);
            audit.Add(mismatch.AuditEntry);

            var zone = Guid.NewGuid();
            rolesReady = true;
            AuthorizationDecision? representative = null;
            foreach (var grant in runtime.Roles.Grants)
            {
                var subject = new AuthorizationSubject(Guid.NewGuid(), grant.Role,
                    grant.ZoneScoped ? new HashSet<Guid> { zone } : new HashSet<Guid>(),
                    grant.CustomCapabilities.ToHashSet(StringComparer.Ordinal));
                foreach (var capability in grant.Capabilities)
                {
                    var decision = runtime.Authorization.Authorize(subject, capability,
                        grant.ZoneScoped ? zone : null);
                    rolesReady &= decision.Allowed;
                    representative ??= decision;
                }
                foreach (var customCapability in grant.CustomCapabilities.Where(item => item != "*"))
                {
                    var decision = runtime.Authorization.Authorize(subject, grant.Capabilities[0],
                        grant.ZoneScoped ? zone : null, customCapability);
                    rolesReady &= decision.Allowed;
                }
            }
            var restricted = runtime.Roles.Grants.FirstOrDefault(item =>
                Enum.GetValues<NetworkCapability>().Any(capability => !item.Capabilities.Contains(capability)));
            if (restricted is not null)
            {
                var deniedCapability = Enum.GetValues<NetworkCapability>()
                    .First(capability => !restricted.Capabilities.Contains(capability));
                var denied = runtime.Authorization.Authorize(new(Guid.NewGuid(), restricted.Role,
                    new HashSet<Guid> { zone }, restricted.CustomCapabilities.ToHashSet(StringComparer.Ordinal)),
                    deniedCapability, restricted.ZoneScoped ? zone : null);
                rolesReady &= !denied.Allowed;
                audit.Add(denied.AuditEntry);
            }
            permissionCode = representative?.Code ?? "NET_AUTH_NOT_TESTED";
            if (representative is not null) audit.Add(representative.AuditEntry);

            var chatGrant = runtime.Roles.Grants.First(item => item.Capabilities.Contains(NetworkCapability.Chat));
            var sender = new AuthorizationSubject(Guid.NewGuid(), chatGrant.Role,
                new HashSet<Guid> { zone }, chatGrant.CustomCapabilities.ToHashSet(StringComparer.Ordinal));
            var filter = profile.Chat.LanguageFilter.First();
            var sent = runtime.Chat.Send(sender, Guid.NewGuid(), ChatChannelKind.Local,
                $"message {filter.Key}", new(zone, null, profile.Chat.AllowedLanguages[0], 2, 2));
            chatCode = sent.Code;
            chatMessages = runtime.Chat.Since(0, _ => true).Count;
            var disabledChannelHonored = profile.Chat.Channels.Contains(ChatChannelKind.Global) ||
                runtime.Chat.Send(sender, null, ChatChannelKind.Global, null, "global").Code ==
                "NET_CHAT_CHANNEL_DENIED";
            chatReady = sent.Accepted && sent.Message is not null &&
                        sent.Message.Text.Contains(filter.Value, StringComparison.Ordinal) &&
                        sent.Message.AccessibleText.Length > sent.Message.Text.Length &&
                        sent.Message.BridgeReceipts?.Count == 1 && bridge.Messages.Count == 1 &&
                        disabledChannelHonored;
            audit.Add(sent.AuditEntry);

            var handled = new List<string>();
            foreach (var subscription in runtime.EventSubscriptions)
                runtime.Events.Subscribe(new ConfiguredHandlerUx9B(subscription, handled));
            ServerEvent? replay = null;
            foreach (var kind in runtime.EventSubscriptions.Select(item => item.EventKind).Distinct(StringComparer.Ordinal))
            {
                var published = runtime.Events.Publish(new(Guid.NewGuid(), kind, Guid.NewGuid(), sender.AccountId,
                    profile.ServerRevision, DateTimeOffset.UnixEpoch,
                    new Dictionary<string, string> { ["probe"] = "ux9-b" }));
                replay ??= published;
            }
            if (replay is not null)
            {
                try
                {
                    runtime.Events.Publish(replay);
                }
                catch (InvalidOperationException exception) when (exception.Message == "NET_EVENT_REPLAYED")
                {
                    replayBlocked = true;
                    audit.Add("rejected:NET_EVENT_REPLAYED");
                }
            }
            eventHandlers = handled.Count;
            eventTraces = runtime.Events.Traces.Count;
            eventsReady = eventHandlers == runtime.EventSubscriptions.Count &&
                          eventTraces == runtime.EventSubscriptions.Count && replayBlocked;
            audit.Add($"accepted:events:handlers={eventHandlers}:traces={eventTraces}");
        }
        finally
        {
            released = DeleteUx9BTemporaryWorkspace(probeRoot, "openrpg-forge-studio-ux9-b-probe-");
            _lastUx9BTemporaryReleased = released;
        }

        var snapshot = new StudioUx9BProbeSnapshot(
            contentReady && versionReady && rolesReady && chatReady && eventsReady && released
                ? "Cinq sondes locales validées"
                : "Sonde locale incomplète",
            contentCode, contentChunks, cacheReused, versionCode, permissionCode, chatCode, chatMessages,
            eventHandlers, eventTraces, replayBlocked, audit.Count, released);
        return new(contentReady, versionReady, rolesReady, chatReady, eventsReady, snapshot, audit);
    }

    private static StudioUx9BOperationsDraft NormalizeDraftUx9B(StudioUx9BOperationsDraft draft) => draft with
    {
        AllowedLicenses = NormalizeTokensUx9B(draft.AllowedLicenses),
        ModuleId = draft.ModuleId.Trim(),
        ModuleVersion = draft.ModuleVersion.Trim(),
        ProtocolVersion = draft.ProtocolVersion.Trim(),
        ContentFingerprint = draft.ContentFingerprint.Trim().ToLowerInvariant(),
        Capabilities = NormalizeTokensUx9B(draft.Capabilities),
        CustomCapabilities = NormalizeTokensUx9B(draft.CustomCapabilities),
        AllowedLanguages = NormalizeTokensUx9B(draft.AllowedLanguages).ToLowerInvariant(),
        FilterToken = draft.FilterToken.Trim(),
        FilterReplacement = draft.FilterReplacement.Trim(),
        EventKind = draft.EventKind.Trim(),
        SubscriberId = draft.SubscriberId.Trim()
    };

    private static string NormalizeTokensUx9B(string value) => string.Join(", ", ParseTokensUx9B(value));

    private static IReadOnlyList<string> ParseTokensUx9B(string value) => value
        .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal).ToArray();

    private static bool TryParseCapabilitiesUx9B(string value, out IReadOnlyList<NetworkCapability> capabilities)
    {
        var parsed = new List<NetworkCapability>();
        foreach (var token in ParseTokensUx9B(value))
        {
            if (!Enum.TryParse<NetworkCapability>(token, ignoreCase: true, out var capability))
            {
                capabilities = [];
                return false;
            }
            if (!parsed.Contains(capability)) parsed.Add(capability);
        }
        capabilities = parsed;
        return parsed.Count > 0;
    }

    private static string ProfileFingerprintUx9B(NetworkOperationsProfile profile) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(new NetworkOperationsStore().Serialize(profile))));

    private static string DraftFingerprintUx9B(StudioUx9BOperationsDraft draft) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(draft))));

    private static StudioUx9BOperationsPreview FailedNetworkOperationsPreviewUx9B(string message,
        IReadOnlyList<NetworkDiagnostic>? diagnostics = null) =>
        new(false, false, false, false, false, false, false, false, message, [],
            diagnostics?.Select(item => HumanNetworkOperationsDiagnosticUx9B(item.Code))
                .Distinct(StringComparer.Ordinal).ToArray() ?? [], EmptyProbeUx9B());

    private static string HumanNetworkOperationsDiagnosticsUx9B(IReadOnlyList<NetworkDiagnostic> diagnostics) =>
        diagnostics.Count == 0
            ? "La commande réseau n’a produit aucun changement applicable."
            : string.Join(" · ", diagnostics.Select(item => HumanNetworkOperationsDiagnosticUx9B(item.Code))
                .Distinct(StringComparer.Ordinal));

    private static string HumanNetworkOperationsDiagnosticUx9B(string? code) => code switch
    {
        "NET_CONTENT_POLICY_INVALID" => "La synchronisation exige 16 Kio à 4 Mio par bloc, un cache borné, des licences et un signataire fiable.",
        "NET_VERSION_LOCK_INVALID" => "Le verrou exige module, version, protocole et empreinte SHA-256 valides.",
        "NET_ROLE_GRANTS_INVALID" => "Chaque rôle doit conserver des capacités connues et uniques.",
        "NET_CHAT_POLICY_INVALID" => "Le chat exige canaux requis, rayon, langues, journal et accessibilité.",
        "NET_EVENT_POLICY_INVALID" => "Le bus exige une limite, une trace, une protection anti-rejeu et des abonnements uniques.",
        "NET_OPERATIONS_PERMISSION_DENIED" => "La permission Administrateur est requise.",
        "NET_OPERATIONS_REVISION_STALE" => "La révision serveur a changé ; rechargez le formulaire.",
        _ => string.IsNullOrWhiteSpace(code) ? "La configuration réseau est invalide." : code
    };

    private static string HumanOperationsDiffUx9B(string value) => value switch
    {
        "content-sync" => "Synchronisation de contenu",
        "version-lock" => "Verrou de version",
        "roles" => "Rôles et permissions",
        "chat" => "Chat structuré",
        "event-bus" => "Bus d’événements serveur",
        _ => value
    };

    private static StudioUx9BProbeSnapshot EmptyProbeUx9B() =>
        new("Aucune sonde exécutée", "—", 0, false, "—", "—", "—", 0, 0, 0, false, 0, true);

    private static StudioUx9BSmokeResult FailedSmokeUx9B() =>
        new(false, false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false);

    private static bool DeleteUx9BTemporaryWorkspace(string root, string requiredPrefix)
    {
        try
        {
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            var parent = Path.GetDirectoryName(fullRoot)?.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            if (!string.Equals(parent, temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(fullRoot).StartsWith(requiredPrefix, StringComparison.Ordinal))
                return false;
            if (Directory.Exists(fullRoot)) Directory.Delete(fullRoot, recursive: true);
            return !Directory.Exists(fullRoot);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private sealed record OperationsProbeUx9B(bool Content, bool Version, bool Roles, bool Chat, bool Events,
        StudioUx9BProbeSnapshot Snapshot, IReadOnlyList<string> Audit);

    private sealed class RecordingBridgeUx9B(string id) : IChatBridgeAdapter
    {
        public string Id { get; } = id;
        public List<ChatMessage> Messages { get; } = [];
        public string Forward(ChatMessage message) { Messages.Add(message); return "forwarded"; }
    }

    private sealed class MemoryTransportUx9B(string path, byte[] bytes) : IContentRepositoryTransport
    {
        public Stream OpenRead(string resourcePath, long offset)
        {
            if (!string.Equals(path, resourcePath, StringComparison.Ordinal))
                throw new InvalidOperationException("NET_CONTENT_PATH_MISMATCH");
            return new MemoryStream(bytes[checked((int)offset)..], writable: false);
        }
    }

    private sealed class ConfiguredHandlerUx9B(ServerEventSubscriptionPolicy subscription,
        ICollection<string> handled) : IServerEventHandler
    {
        public string EventKind => subscription.EventKind;
        public int Priority => subscription.Priority;
        public void Handle(ServerEvent serverEvent) => handled.Add(subscription.SubscriberId);
    }
}
