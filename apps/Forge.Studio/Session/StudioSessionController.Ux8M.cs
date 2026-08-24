using System.Security.Cryptography;
using System.Text;
using Forge.ModuleFormat.Network;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private NetworkPlayerExperienceHistory? _networkExperienceHistoryUx8M;
    private NetworkPlayerExperienceProfile? _persistedNetworkExperienceUx8M;
    private string? _networkExperienceWorkspaceRootUx8M;
    private StudioUx8MNetworkDraft? _approvedNetworkExperienceUx8M;
    private string? _approvedNetworkExperienceFingerprintUx8M;
    private string? _networkExperienceLoadMessageUx8M;
    private bool _networkExperienceFileMissingUx8M;
    private bool _lastUx8MTemporaryReleased = true;

    internal string? NetworkExperienceLoadMessageUx8M => _networkExperienceLoadMessageUx8M;
    internal bool NetworkExperienceFileMissingUx8M => _networkExperienceFileMissingUx8M;
    internal bool LastUx8MTemporaryReleased => _lastUx8MTemporaryReleased;
    internal NetworkPlayerExperienceProfile? CurrentNetworkExperienceUx8M =>
        EnsureNetworkExperienceUx8M() ? _networkExperienceHistoryUx8M!.Current : null;
    internal bool NetworkExperienceHasUnsavedChangesUx8M =>
        EnsureNetworkExperienceUx8M() && _persistedNetworkExperienceUx8M is not null &&
        !string.Equals(ProfileFingerprintUx8M(_networkExperienceHistoryUx8M!.Current),
            ProfileFingerprintUx8M(_persistedNetworkExperienceUx8M), StringComparison.Ordinal);

    internal StudioUx8MNetworkDraft? CreateNetworkExperienceDraftUx8M(bool explicitlySelected = false)
    {
        if (!explicitlySelected || !EnsureNetworkExperienceUx8M()) return null;
        var profile = _networkExperienceHistoryUx8M!.Current;
        return new(
            profile.ServerRevision,
            ProfileFingerprintUx8M(profile),
            profile.Discovery.DirectAddress,
            profile.Discovery.LanDiscovery,
            profile.Discovery.CuratedDirectory,
            profile.Discovery.Favorites,
            profile.Discovery.RecentServers,
            profile.Discovery.InviteLinks,
            profile.Lobby.RulesetId,
            profile.Lobby.ContentFingerprint,
            profile.Lobby.MaximumSlots,
            profile.Lobby.MaximumTeams,
            profile.Lobby.CharacterSelection,
            profile.Lobby.PermissionsVisible,
            profile.Lobby.ReadyState,
            profile.CharacterBuilder.RulesetId,
            profile.CharacterBuilder.TemplateIds.ToArray(),
            profile.CharacterBuilder.PreviewEnabled,
            profile.CharacterBuilder.ImportAllowed,
            profile.CharacterBuilder.ServerApprovalRequired,
            profile.CharacterBuilder.MinimumAttribute,
            profile.CharacterBuilder.MaximumAttribute,
            profile.CharacterAuthority.AllowedModes.ToArray(),
            profile.CharacterAuthority.SignedUploadsRequired,
            profile.CharacterAuthority.EncryptedAtRest,
            profile.CharacterAuthority.MigrationEnabled,
            profile.CharacterAuthority.AccountBindingRequired);
    }

    internal StudioUx8MNetworkPreview PreviewNetworkExperienceUx8M(StudioUx8MNetworkDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelNetworkExperienceApprovalUx8M();
        if (!EnsureNetworkExperienceUx8M())
            return FailedNetworkPreviewUx8M(_networkExperienceLoadMessageUx8M ?? "Le profil réseau ne peut pas être chargé.");

        var current = _networkExperienceHistoryUx8M!.Current;
        if (draft.BaseServerRevision != current.ServerRevision ||
            !string.Equals(draft.SourceProfileFingerprint, ProfileFingerprintUx8M(current), StringComparison.Ordinal))
            return FailedNetworkPreviewUx8M("Le profil réseau a changé ; rechargez le formulaire avant l’aperçu sec.");

        var normalized = NormalizeDraftUx8M(draft);
        var candidate = BuildCandidateUx8M(current, normalized);
        var command = new NetworkPlayerExperienceCommand(
            Guid.NewGuid(), 1, "studio:ux8-m", current.ServerRevision,
            "studio.network.author", NetworkRole.Administrator, false, candidate);
        var dryRun = _networkExperienceHistoryUx8M.Execute(command);
        if (!dryRun.Succeeded || dryRun.Applied)
            return FailedNetworkPreviewUx8M(HumanNetworkDiagnosticsUx8M(dryRun.Diagnostics), dryRun.Diagnostics);

        var runtime = NetworkPlayerExperienceRuntimeAdapter.Resolve(candidate, required: true);
        var runtimeResolved = runtime.Available && runtime.Profile is not null && runtime.Diagnostics.Count == 0;
        var scriptService = new NetworkPlayerExperienceScriptService(candidate);
        var snapshot = scriptService.Read(scriptService.Handle);
        var scriptReadable = snapshot.Id == candidate.Id && snapshot.Revision == candidate.ServerRevision &&
                             snapshot.ConnectionRoutes > 0 && snapshot.LobbySlots == candidate.Lobby.MaximumSlots &&
                             snapshot.CharacterTemplates == candidate.CharacterBuilder.TemplateIds.Count &&
                             snapshot.AuthorityModes == candidate.CharacterAuthority.AllowedModes.Count;
        var changes = dryRun.SemanticDiff == "revision-only"
            ? Array.Empty<string>()
            : dryRun.SemanticDiff.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var canApply = runtimeResolved && scriptReadable && changes.Length > 0;
        if (canApply)
        {
            _approvedNetworkExperienceUx8M = FreezeDraftUx8M(normalized);
            _approvedNetworkExperienceFingerprintUx8M = DraftFingerprintUx8M(_approvedNetworkExperienceUx8M);
        }

        return new(
            true,
            canApply,
            true,
            runtimeResolved,
            scriptReadable,
            changes.Length == 0
                ? "Aperçu sec validé · le profil est déjà identique, aucune écriture nécessaire."
                : "Aperçu sec validé · configuration uniquement, aucun serveur ni transport distant lancé.",
            changes.Select(HumanDiffUx8M).ToArray(),
            runtime.Diagnostics.Select(item => HumanNetworkDiagnosticUx8M(item.Code)).Distinct(StringComparer.Ordinal).ToArray());
    }

    internal StudioUx8MActionResult ApplyApprovedNetworkExperienceUx8M(StudioUx8MNetworkDraft currentDraft)
    {
        var approved = _approvedNetworkExperienceUx8M;
        if (approved is null || _approvedNetworkExperienceFingerprintUx8M is null ||
            !EnsureNetworkExperienceUx8M())
        {
            CancelNetworkExperienceApprovalUx8M();
            return new(false, "Prévisualisez d’abord la configuration réseau.");
        }

        var current = _networkExperienceHistoryUx8M!.Current;
        var normalizedCurrent = NormalizeDraftUx8M(currentDraft);
        if (!string.Equals(DraftFingerprintUx8M(normalizedCurrent), _approvedNetworkExperienceFingerprintUx8M,
                StringComparison.Ordinal) ||
            !string.Equals(DraftFingerprintUx8M(approved), _approvedNetworkExperienceFingerprintUx8M,
                StringComparison.Ordinal) ||
            approved.BaseServerRevision != current.ServerRevision ||
            !string.Equals(approved.SourceProfileFingerprint, ProfileFingerprintUx8M(current), StringComparison.Ordinal))
        {
            CancelNetworkExperienceApprovalUx8M();
            return new(false, "La source ou le brouillon a changé ; relancez l’aperçu sec.");
        }

        var candidate = BuildCandidateUx8M(current, approved);
        var command = new NetworkPlayerExperienceCommand(
            Guid.NewGuid(), 1, "studio:ux8-m", current.ServerRevision,
            "studio.network.author", NetworkRole.Administrator, false, candidate);
        var dryRun = _networkExperienceHistoryUx8M.Execute(command);
        if (!dryRun.Succeeded || dryRun.Applied || dryRun.SemanticDiff == "revision-only")
        {
            CancelNetworkExperienceApprovalUx8M();
            return new(false, HumanNetworkDiagnosticsUx8M(dryRun.Diagnostics));
        }

        var applied = _networkExperienceHistoryUx8M.Execute(command with { Apply = true });
        CancelNetworkExperienceApprovalUx8M();
        return applied.Succeeded && applied.Applied
            ? new(true, "Profil joueur réseau appliqué par la commande canonique · enregistrez pour le persister.")
            : new(false, HumanNetworkDiagnosticsUx8M(applied.Diagnostics));
    }

    internal StudioUx8MActionResult UndoNetworkExperienceUx8M()
    {
        CancelNetworkExperienceApprovalUx8M();
        if (!EnsureNetworkExperienceUx8M())
            return new(false, _networkExperienceLoadMessageUx8M ?? "Aucun profil réseau actif.");
        var result = _networkExperienceHistoryUx8M!.Undo("studio.network.author", NetworkRole.Administrator);
        return new(result.Succeeded && result.Applied,
            result.Succeeded ? "Modification réseau annulée." : HumanNetworkDiagnosticsUx8M(result.Diagnostics));
    }

    internal StudioUx8MActionResult RedoNetworkExperienceUx8M()
    {
        CancelNetworkExperienceApprovalUx8M();
        if (!EnsureNetworkExperienceUx8M())
            return new(false, _networkExperienceLoadMessageUx8M ?? "Aucun profil réseau actif.");
        var result = _networkExperienceHistoryUx8M!.Redo("studio.network.author", NetworkRole.Administrator);
        return new(result.Succeeded && result.Applied,
            result.Succeeded ? "Modification réseau rétablie." : HumanNetworkDiagnosticsUx8M(result.Diagnostics));
    }

    internal StudioUx8MActionResult SaveNetworkExperienceUx8M()
    {
        CancelNetworkExperienceApprovalUx8M();
        if (!EnsureNetworkExperienceUx8M() || Session is null)
            return new(false, _networkExperienceLoadMessageUx8M ?? "Aucun profil réseau actif.");
        var result = WorkspaceNetworkPlayerExperienceStore.Save(
            Session.RootDirectory, _networkExperienceHistoryUx8M!.Current);
        if (!result.Succeeded)
            return new(false, result.Diagnostics.FirstOrDefault()?.Message ?? "Le profil réseau n’a pas été enregistré.");
        _persistedNetworkExperienceUx8M = _networkExperienceHistoryUx8M.Current;
        _networkExperienceFileMissingUx8M = false;
        return new(true, "Profil joueur réseau enregistré atomiquement dans le workspace.");
    }

    internal StudioUx8MApprovalSnapshot CaptureNetworkExperienceApprovalUx8M() =>
        new(_approvedNetworkExperienceUx8M, _approvedNetworkExperienceFingerprintUx8M);

    internal void RestoreNetworkExperienceApprovalUx8M(StudioUx8MApprovalSnapshot snapshot)
    {
        _approvedNetworkExperienceUx8M = snapshot.Draft;
        _approvedNetworkExperienceFingerprintUx8M = snapshot.DraftFingerprint;
    }

    internal void CancelNetworkExperienceApprovalUx8M()
    {
        _approvedNetworkExperienceUx8M = null;
        _approvedNetworkExperienceFingerprintUx8M = null;
    }

    internal void CloseUx8M()
    {
        CancelNetworkExperienceApprovalUx8M();
        _networkExperienceHistoryUx8M = null;
        _persistedNetworkExperienceUx8M = null;
        _networkExperienceWorkspaceRootUx8M = null;
        _networkExperienceLoadMessageUx8M = null;
        _networkExperienceFileMissingUx8M = false;
    }

    internal StudioUx8MSmokeResult RunUx8MContractSmoke()
    {
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceSession = Session;
        if (sourceProject is null || sourceHistory is null || sourceSession is null)
            return FailedSmokeUx8M();

        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var sourceApproval = CaptureNetworkExperienceApprovalUx8M();
        var temporaryRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux8-m-{Guid.NewGuid():N}");
        StudioSessionController? editing = null;
        StudioSessionController? reopened = null;
        var released = false;
        var fourRoutes = false;
        var explicitSelection = false;
        var invalidBlocked = false;
        var canonicalDryRun = false;
        var canonicalCommand = false;
        var runtimeAndScript = false;
        var staleSource = false;
        var staleApproval = false;
        var undoRedo = false;
        var persistence = false;
        var combatPreserved = false;
        try
        {
            var created = ProjectSession.Create(temporaryRoot, sourceProject);
            if (!created.Succeeded) return FailedSmokeUx8M();
            var baseline = NetworkPlayerExperienceProfile.CreateDefault("evidence:ux8-m");
            if (!WorkspaceNetworkPlayerExperienceStore.Save(temporaryRoot, baseline).Succeeded)
                return FailedSmokeUx8M();

            editing = new StudioSessionController();
            if (!editing.Open(temporaryRoot).Succeeded) return FailedSmokeUx8M();
            var draft = editing.CreateNetworkExperienceDraftUx8M(explicitlySelected: true);
            explicitSelection = draft is not null;
            if (draft is null) return FailedSmokeUx8M();
            var fixture = draft with
            {
                DirectAddress = false,
                LanDiscovery = true,
                CuratedDirectory = true,
                MaximumSlots = 12,
                MaximumTeams = 3,
                TemplateIds = ["template:adventurer", "template:ranger"],
                ImportAllowed = false,
                MinimumAttribute = 2,
                MaximumAttribute = 24,
                AllowedModes =
                [
                    CharacterAuthorityMode.SignedUpload,
                    CharacterAuthorityMode.ServerManaged,
                    CharacterAuthorityMode.AccountBound
                ]
            };

            invalidBlocked = !editing.PreviewNetworkExperienceUx8M(fixture with
            {
                DirectAddress = false,
                LanDiscovery = false,
                CuratedDirectory = false
            }).Succeeded;
            staleSource = !editing.PreviewNetworkExperienceUx8M(fixture with
            {
                BaseServerRevision = fixture.BaseServerRevision + 9
            }).Succeeded;
            var preview = editing.PreviewNetworkExperienceUx8M(fixture);
            canonicalDryRun = preview.Succeeded && preview.CanApply && preview.CanonicalDryRun;
            runtimeAndScript = preview.RuntimeResolved && preview.ScriptProjectionReadable;
            staleApproval = !editing.ApplyApprovedNetworkExperienceUx8M(fixture with
            {
                MaximumSlots = fixture.MaximumSlots + 1
            }).Succeeded;
            preview = editing.PreviewNetworkExperienceUx8M(fixture);
            var applied = editing.ApplyApprovedNetworkExperienceUx8M(fixture);
            canonicalCommand = preview.CanApply && applied.Succeeded;
            var current = editing.CurrentNetworkExperienceUx8M;
            if (current is null) return FailedSmokeUx8M();
            fourRoutes = !current.Discovery.DirectAddress && current.Discovery.LanDiscovery &&
                         current.Discovery.CuratedDirectory && current.Discovery.Favorites &&
                         current.Discovery.RecentServers && current.Discovery.InviteLinks &&
                         current.Lobby.MaximumSlots == 12 && current.Lobby.MaximumTeams == 3 &&
                         current.Lobby.CharacterSelection && current.Lobby.PermissionsVisible && current.Lobby.ReadyState &&
                         current.CharacterBuilder.TemplateIds.SequenceEqual(fixture.TemplateIds, StringComparer.Ordinal) &&
                         current.CharacterBuilder.PreviewEnabled && !current.CharacterBuilder.ImportAllowed &&
                         current.CharacterBuilder.ServerApprovalRequired &&
                         current.CharacterAuthority.AllowedModes.SequenceEqual(fixture.AllowedModes) &&
                         current.CharacterAuthority.SignedUploadsRequired && current.CharacterAuthority.EncryptedAtRest &&
                         current.CharacterAuthority.MigrationEnabled && current.CharacterAuthority.AccountBindingRequired;
            combatPreserved = current.Combat == baseline.Combat && current.Id == baseline.Id &&
                              current.RequiredAtRuntime == baseline.RequiredAtRuntime;

            var undone = editing.UndoNetworkExperienceUx8M();
            var afterUndo = editing.CurrentNetworkExperienceUx8M;
            var redone = editing.RedoNetworkExperienceUx8M();
            var afterRedo = editing.CurrentNetworkExperienceUx8M;
            undoRedo = undone.Succeeded && redone.Succeeded && afterUndo is not null && afterRedo is not null &&
                       afterUndo.ServerRevision == baseline.ServerRevision &&
                       afterRedo.ServerRevision == current.ServerRevision;
            var saved = editing.SaveNetworkExperienceUx8M();
            var loaded = WorkspaceNetworkPlayerExperienceStore.Load(temporaryRoot);
            var serializer = new NetworkPlayerExperienceStore();
            persistence = saved.Succeeded && loaded.Succeeded && !loaded.FileMissing &&
                          string.Equals(serializer.Serialize(afterRedo!), serializer.Serialize(loaded.Profile),
                              StringComparison.Ordinal);
            reopened = new StudioSessionController();
            persistence = persistence && reopened.Open(temporaryRoot).Succeeded &&
                          reopened.CreateNetworkExperienceDraftUx8M(explicitlySelected: true) is { } reopenedDraft &&
                          reopenedDraft.BaseServerRevision == afterRedo!.ServerRevision &&
                          string.Equals(reopenedDraft.SourceProfileFingerprint,
                              ProfileFingerprintUx8M(afterRedo), StringComparison.Ordinal);
        }
        finally
        {
            reopened?.Close();
            editing?.Close();
            released = DeleteUx8MTemporaryWorkspace(temporaryRoot);
            _lastUx8MTemporaryReleased = released;
            RestoreNetworkExperienceApprovalUx8M(sourceApproval);
        }

        var sourceUnchanged = ReferenceEquals(sourceProject, Project) && ReferenceEquals(sourceHistory, History) &&
                              History?.State.Revision == sourceRevision && History.Journal.SequenceEqual(sourceJournal) &&
                              IsDirty == sourceDirty && ActiveAreaId == sourceArea &&
                              SelectedEntityIds.SequenceEqual(sourceSelection) && SelectedEntityId == sourceSelected &&
                              CaptureNetworkExperienceApprovalUx8M() == sourceApproval;
        var succeeded = fourRoutes && explicitSelection && invalidBlocked && canonicalDryRun && canonicalCommand &&
                        runtimeAndScript && staleSource && staleApproval && undoRedo && persistence && combatPreserved &&
                        sourceUnchanged && released;
        return new(succeeded, fourRoutes, explicitSelection, invalidBlocked, canonicalDryRun, canonicalCommand,
            runtimeAndScript, staleSource, staleApproval, undoRedo, persistence, combatPreserved, sourceUnchanged, released);
    }

    private bool EnsureNetworkExperienceUx8M()
    {
        var root = Session?.RootDirectory;
        if (string.IsNullOrWhiteSpace(root))
        {
            _networkExperienceLoadMessageUx8M = "Ouvrez d’abord un module.";
            return false;
        }

        var fullRoot = Path.GetFullPath(root);
        if (_networkExperienceHistoryUx8M is not null &&
            string.Equals(_networkExperienceWorkspaceRootUx8M, fullRoot, StringComparison.OrdinalIgnoreCase))
            return true;

        var loaded = WorkspaceNetworkPlayerExperienceStore.Load(fullRoot);
        if (!loaded.Succeeded)
        {
            _networkExperienceHistoryUx8M = null;
            _persistedNetworkExperienceUx8M = null;
            _networkExperienceWorkspaceRootUx8M = fullRoot;
            _networkExperienceFileMissingUx8M = false;
            _networkExperienceLoadMessageUx8M =
                $"Profil réseau invalide : {loaded.Diagnostics.FirstOrDefault()?.Message ?? "diagnostic inconnu"}";
            return false;
        }

        _networkExperienceHistoryUx8M = new NetworkPlayerExperienceHistory(loaded.Profile);
        _persistedNetworkExperienceUx8M = loaded.Profile;
        _networkExperienceWorkspaceRootUx8M = fullRoot;
        _networkExperienceFileMissingUx8M = loaded.FileMissing;
        _networkExperienceLoadMessageUx8M = loaded.FileMissing
            ? "Aucun profil réseau enregistré · le formulaire montre le profil initial sans écrire de fichier."
            : null;
        return true;
    }

    private static NetworkPlayerExperienceProfile BuildCandidateUx8M(
        NetworkPlayerExperienceProfile current, StudioUx8MNetworkDraft draft) => current with
        {
            ServerRevision = current.ServerRevision + 1,
            Discovery = current.Discovery with
            {
                DirectAddress = draft.DirectAddress,
                LanDiscovery = draft.LanDiscovery,
                CuratedDirectory = draft.CuratedDirectory,
                Favorites = draft.Favorites,
                RecentServers = draft.RecentServers,
                InviteLinks = draft.InviteLinks
            },
            Lobby = current.Lobby with
            {
                RulesetId = draft.LobbyRulesetId,
                ContentFingerprint = draft.ContentFingerprint,
                MaximumSlots = draft.MaximumSlots,
                MaximumTeams = draft.MaximumTeams,
                CharacterSelection = draft.CharacterSelection,
                PermissionsVisible = draft.PermissionsVisible,
                ReadyState = draft.ReadyState
            },
            CharacterBuilder = current.CharacterBuilder with
            {
                RulesetId = draft.BuilderRulesetId,
                TemplateIds = draft.TemplateIds.ToArray(),
                PreviewEnabled = draft.PreviewEnabled,
                ImportAllowed = draft.ImportAllowed,
                ServerApprovalRequired = draft.ServerApprovalRequired,
                MinimumAttribute = draft.MinimumAttribute,
                MaximumAttribute = draft.MaximumAttribute
            },
            CharacterAuthority = current.CharacterAuthority with
            {
                AllowedModes = draft.AllowedModes.ToArray(),
                SignedUploadsRequired = draft.SignedUploadsRequired,
                EncryptedAtRest = draft.EncryptedAtRest,
                MigrationEnabled = draft.MigrationEnabled,
                AccountBindingRequired = draft.AccountBindingRequired
            }
        };

    private static StudioUx8MNetworkDraft NormalizeDraftUx8M(StudioUx8MNetworkDraft draft) => draft with
    {
        LobbyRulesetId = draft.LobbyRulesetId.Trim(),
        ContentFingerprint = draft.ContentFingerprint.Trim(),
        BuilderRulesetId = draft.BuilderRulesetId.Trim(),
        TemplateIds = draft.TemplateIds.Select(item => item.Trim()).ToArray(),
        AllowedModes = draft.AllowedModes.ToArray()
    };

    private static StudioUx8MNetworkDraft FreezeDraftUx8M(StudioUx8MNetworkDraft draft) => draft with
    {
        TemplateIds = draft.TemplateIds.ToArray(),
        AllowedModes = draft.AllowedModes.ToArray()
    };

    private static string ProfileFingerprintUx8M(NetworkPlayerExperienceProfile profile) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(new NetworkPlayerExperienceStore().Serialize(profile))));

    private static string DraftFingerprintUx8M(StudioUx8MNetworkDraft draft)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, new UTF8Encoding(false), leaveOpen: true))
        {
            writer.Write(draft.BaseServerRevision);
            writer.Write(draft.SourceProfileFingerprint);
            writer.Write(draft.DirectAddress);
            writer.Write(draft.LanDiscovery);
            writer.Write(draft.CuratedDirectory);
            writer.Write(draft.Favorites);
            writer.Write(draft.RecentServers);
            writer.Write(draft.InviteLinks);
            writer.Write(draft.LobbyRulesetId);
            writer.Write(draft.ContentFingerprint);
            writer.Write(draft.MaximumSlots);
            writer.Write(draft.MaximumTeams);
            writer.Write(draft.CharacterSelection);
            writer.Write(draft.PermissionsVisible);
            writer.Write(draft.ReadyState);
            writer.Write(draft.BuilderRulesetId);
            writer.Write(draft.TemplateIds.Count);
            foreach (var item in draft.TemplateIds) writer.Write(item);
            writer.Write(draft.PreviewEnabled);
            writer.Write(draft.ImportAllowed);
            writer.Write(draft.ServerApprovalRequired);
            writer.Write(draft.MinimumAttribute);
            writer.Write(draft.MaximumAttribute);
            writer.Write(draft.AllowedModes.Count);
            foreach (var mode in draft.AllowedModes) writer.Write((int)mode);
            writer.Write(draft.SignedUploadsRequired);
            writer.Write(draft.EncryptedAtRest);
            writer.Write(draft.MigrationEnabled);
            writer.Write(draft.AccountBindingRequired);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static StudioUx8MNetworkPreview FailedNetworkPreviewUx8M(
        string message, IReadOnlyList<NetworkDiagnostic>? diagnostics = null) =>
        new(false, false, false, false, false, message, [],
            diagnostics?.Select(item => HumanNetworkDiagnosticUx8M(item.Code)).Distinct(StringComparer.Ordinal).ToArray() ?? []);

    private static string HumanNetworkDiagnosticsUx8M(IReadOnlyList<NetworkDiagnostic> diagnostics) =>
        diagnostics.Count == 0
            ? "La commande réseau n’a produit aucun changement applicable."
            : string.Join(" · ", diagnostics.Select(item => HumanNetworkDiagnosticUx8M(item.Code)).Distinct(StringComparer.Ordinal));

    private static string HumanNetworkDiagnosticUx8M(string? code) => code switch
    {
        "NET_DISCOVERY_ROUTE_REQUIRED" => "Activez au moins une voie : adresse directe, réseau local ou annuaire curaté.",
        "NET_DISCOVERY_HISTORY_REQUIRED" => "Favoris, serveurs récents et liens d’invitation doivent rester disponibles.",
        "NET_LOBBY_POLICY_INVALID" => "Renseignez règles, version de contenu, slots, équipes, sélection, permissions et état prêt.",
        "NET_CHARACTER_BUILDER_INVALID" => "Le constructeur exige un ruleset, des modèles uniques, un aperçu, une approbation serveur et des bornes valides.",
        "NET_CHARACTER_AUTHORITY_INVALID" => "Conservez les modes serveur et compte ainsi que signature, chiffrement, migration et liaison de compte.",
        "NET_PLAYER_REVISION_STALE" => "La révision serveur a changé ; rechargez le formulaire.",
        "NET_PLAYER_PERMISSION_DENIED" => "Une identité administrateur est requise.",
        "NET_PLAYER_PROFILE_INCOMPLETE" => "Le document réseau est incomplet.",
        _ => string.IsNullOrWhiteSpace(code) ? "La configuration réseau est invalide." : code
    };

    private static string HumanDiffUx8M(string value) => value switch
    {
        "discovery" => "Découverte et connexion",
        "lobby" => "Lobby et sélection de personnage",
        "character-builder" => "Création de personnage",
        "character-authority" => "Personnages locaux ou serveur",
        _ => value
    };

    private static StudioUx8MSmokeResult FailedSmokeUx8M() =>
        new(false, false, false, false, false, false, false, false, false, false, false, false, false, false);

    private static bool DeleteUx8MTemporaryWorkspace(string root)
    {
        try
        {
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var parent = Path.GetDirectoryName(fullRoot)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(parent, temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(fullRoot).StartsWith("openrpg-forge-studio-ux8-m-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(fullRoot)) Directory.Delete(fullRoot, recursive: true);
            return !Directory.Exists(fullRoot);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
