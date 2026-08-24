using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.LiveDirector.Authoring;
using Forge.LiveDirector.Economy;
using Forge.LiveDirector.World;
using Forge.ModuleFormat.LiveDirector;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private LiveDirectorAuthorityHistory? _liveAuthorityHistoryUx9H;
    private LiveDirectorAuthorityProfile? _persistedLiveAuthorityUx9H;
    private string? _liveAuthorityWorkspaceRootUx9H;
    private StudioUx9HAuthorityDraft? _approvedLiveAuthorityUx9H;
    private string? _approvedLiveAuthorityFingerprintUx9H;
    private string? _liveAuthorityLoadMessageUx9H;
    private bool _liveAuthorityFileMissingUx9H;
    private StudioUx9HAuthorityProbe _liveAuthorityProbeUx9H = EmptyProbeUx9H();
    private readonly List<string> _liveAuthorityAuditUx9H = [];

    internal string? LiveAuthorityLoadMessageUx9H => _liveAuthorityLoadMessageUx9H;
    internal bool LiveAuthorityFileMissingUx9H => _liveAuthorityFileMissingUx9H;
    internal StudioUx9HAuthorityProbe LiveAuthorityProbeUx9H => _liveAuthorityProbeUx9H;
    internal IReadOnlyList<string> LiveAuthorityAuditUx9H => _liveAuthorityAuditUx9H.ToArray();
    internal LiveDirectorAuthorityProfile? CurrentLiveAuthorityUx9H =>
        EnsureLiveAuthorityUx9H() ? _liveAuthorityHistoryUx9H!.Current : null;
    internal bool LiveAuthorityHasUnsavedChangesUx9H =>
        EnsureLiveAuthorityUx9H() && _persistedLiveAuthorityUx9H is not null &&
        ProfileFingerprintUx9H(_liveAuthorityHistoryUx9H!.Current) !=
        ProfileFingerprintUx9H(_persistedLiveAuthorityUx9H);

    internal StudioUx9HAuthorityDraft? CreateLiveAuthorityDraftUx9H(
        bool explicitlySelected = false)
    {
        if (!explicitlySelected || !EnsureLiveAuthorityUx9H()) return null;
        var p = _liveAuthorityHistoryUx9H!.Current;
        return new(p.ServerRevision, ProfileFingerprintUx9H(p),
            p.MaximumProtectionMinutes, p.MaximumCurrencyAdjustment, p.MaximumWallet,
            p.MaximumExperienceAdjustment, p.RequiredAtRuntime);
    }

    internal StudioUx9HAuthorityPreview PreviewLiveAuthorityUx9H(
        StudioUx9HAuthorityDraft draft, StudioUx9HProbeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelLiveAuthorityApprovalUx9H();
        if (!EnsureLiveAuthorityUx9H())
            return FailedPreviewUx9H(_liveAuthorityLoadMessageUx9H ??
                                     "L'autorité Live Director ne peut pas être chargée.");
        var current = _liveAuthorityHistoryUx9H!.Current;
        if (draft.BaseServerRevision != current.ServerRevision ||
            draft.SourceProfileFingerprint != ProfileFingerprintUx9H(current))
            return FailedPreviewUx9H(
                "La configuration a changé ; rechargez le formulaire avant l’aperçu sec.");
        var candidate = BuildCandidateUx9H(current, draft);
        var command = new LiveDirectorAuthorityCommand(Guid.NewGuid(), 1, "studio:ux9-h",
            current.ServerRevision, "studio.live.authority", NetworkRole.Administrator, false,
            candidate);
        var dryRun = _liveAuthorityHistoryUx9H.Execute(command);
        if (!dryRun.Succeeded || dryRun.Applied)
            return FailedPreviewUx9H(HumanDiagnosticsUx9H(dryRun.Diagnostics),
                dryRun.Diagnostics);
        LiveAuthorityProbeResultUx9H probe;
        try
        {
            probe = RunLiveAuthorityProbeUx9H(candidate, false,
                options ?? StudioUx9HProbeOptions.CreateDefault());
        }
        catch (Exception exception) when (exception is ArgumentException or
                                               InvalidOperationException or InvalidDataException)
        {
            return FailedPreviewUx9H(
                $"La sonde locale d'autorité a refusé la configuration : {exception.Message}");
        }
        _liveAuthorityProbeUx9H = probe.Snapshot;
        var changes = dryRun.SemanticDiff == "revision-only" ? [] :
            dryRun.SemanticDiff.Split(',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var canApply = changes.Length > 0 && probe.Impersonation && probe.Protection &&
                       probe.Currency && probe.Experience && probe.Door;
        if (canApply)
        {
            _approvedLiveAuthorityUx9H = draft with { };
            _approvedLiveAuthorityFingerprintUx9H = DraftFingerprintUx9H(draft);
        }
        return new(true, canApply, true, probe.Impersonation, probe.Protection,
            probe.Currency, probe.Experience, probe.Door,
            changes.Length == 0
                ? "Aperçu sec validé · le profil est déjà identique."
                : "Aperçu sec validé · aucun joueur ni porte réelle n’a été modifié.",
            changes.Select(HumanDiffUx9H).ToArray(), [], probe.Snapshot);
    }

    internal StudioUx9HActionResult ApplyApprovedLiveAuthorityUx9H(
        StudioUx9HAuthorityDraft draft)
    {
        var approved = _approvedLiveAuthorityUx9H;
        if (approved is null || _approvedLiveAuthorityFingerprintUx9H is null ||
            !EnsureLiveAuthorityUx9H())
        {
            CancelLiveAuthorityApprovalUx9H();
            return new(false, "Prévisualisez d’abord le profil d'autorité.");
        }
        var current = _liveAuthorityHistoryUx9H!.Current;
        if (DraftFingerprintUx9H(draft) != _approvedLiveAuthorityFingerprintUx9H ||
            DraftFingerprintUx9H(approved) != _approvedLiveAuthorityFingerprintUx9H ||
            approved.BaseServerRevision != current.ServerRevision ||
            approved.SourceProfileFingerprint != ProfileFingerprintUx9H(current))
        {
            CancelLiveAuthorityApprovalUx9H();
            return new(false, "La source ou le brouillon a changé ; relancez l’aperçu sec.");
        }
        var command = new LiveDirectorAuthorityCommand(Guid.NewGuid(), 1, "studio:ux9-h",
            current.ServerRevision, "studio.live.authority", NetworkRole.Administrator, false,
            BuildCandidateUx9H(current, approved));
        var dryRun = _liveAuthorityHistoryUx9H.Execute(command);
        if (!dryRun.Succeeded || dryRun.Applied || dryRun.SemanticDiff == "revision-only")
        {
            CancelLiveAuthorityApprovalUx9H();
            return new(false, HumanDiagnosticsUx9H(dryRun.Diagnostics));
        }
        var applied = _liveAuthorityHistoryUx9H.Execute(command with { Apply = true });
        CancelLiveAuthorityApprovalUx9H();
        if (!applied.Succeeded || !applied.Applied)
            return new(false, HumanDiagnosticsUx9H(applied.Diagnostics));
        _liveAuthorityAuditUx9H.Add(applied.AuditEntry);
        return new(true,
            "Profil d'autorité appliqué par la commande canonique · enregistrez pour persister.");
    }

    internal StudioUx9HActionResult UndoLiveAuthorityUx9H() =>
        MoveHistoryUx9H(undo: true);
    internal StudioUx9HActionResult RedoLiveAuthorityUx9H() =>
        MoveHistoryUx9H(undo: false);

    private StudioUx9HActionResult MoveHistoryUx9H(bool undo)
    {
        CancelLiveAuthorityApprovalUx9H();
        if (!EnsureLiveAuthorityUx9H())
            return new(false, _liveAuthorityLoadMessageUx9H ?? "Aucun profil d'autorité actif.");
        var result = undo
            ? _liveAuthorityHistoryUx9H!.Undo("studio.live.authority", NetworkRole.Administrator)
            : _liveAuthorityHistoryUx9H!.Redo("studio.live.authority", NetworkRole.Administrator);
        if (result.Succeeded) _liveAuthorityAuditUx9H.Add(result.AuditEntry);
        return new(result.Succeeded && result.Applied,
            result.Succeeded ? undo ? "Modification d'autorité annulée." :
                "Modification d'autorité rétablie." : HumanDiagnosticsUx9H(result.Diagnostics));
    }

    internal StudioUx9HActionResult SaveLiveAuthorityUx9H()
    {
        CancelLiveAuthorityApprovalUx9H();
        if (!EnsureLiveAuthorityUx9H() || Session is null)
            return new(false, _liveAuthorityLoadMessageUx9H ?? "Aucun profil d'autorité actif.");
        var result = WorkspaceLiveDirectorAuthorityStore.Save(Session.RootDirectory,
            _liveAuthorityHistoryUx9H!.Current);
        if (!result.Succeeded)
            return new(false, result.Diagnostics.FirstOrDefault()?.Message ??
                              "Le profil d'autorité n'a pas été enregistré.");
        _persistedLiveAuthorityUx9H = _liveAuthorityHistoryUx9H.Current;
        _liveAuthorityFileMissingUx9H = false;
        return new(true, "Autorité Live Director enregistrée atomiquement dans le workspace.");
    }

    internal StudioUx9HActionResult RunLocalLiveAuthorityUx9H(bool confirmed, NetworkRole role,
        StudioUx9HProbeOptions? options = null)
    {
        if (!confirmed)
        {
            _liveAuthorityAuditUx9H.Add("rejected:studio.live-authority:confirmation-required");
            return new(false, "Confirmez explicitement la sonde locale d'autorité.");
        }
        if (role != NetworkRole.Administrator)
        {
            _liveAuthorityAuditUx9H.Add("rejected:studio.live-authority:permission-denied");
            return new(false, "La permission Administrateur est requise pour cette sonde.");
        }
        if (!EnsureLiveAuthorityUx9H())
            return new(false, _liveAuthorityLoadMessageUx9H ?? "Aucun profil d'autorité actif.");
        try
        {
            var probe = RunLiveAuthorityProbeUx9H(_liveAuthorityHistoryUx9H!.Current, true,
                options ?? StudioUx9HProbeOptions.CreateDefault());
            _liveAuthorityProbeUx9H = probe.Snapshot;
            _liveAuthorityAuditUx9H.AddRange(probe.Audit);
            var ok = probe.Impersonation && probe.Protection && probe.Currency &&
                     probe.Experience && probe.Door && probe.Compensation &&
                     probe.Snapshot.ResourcesReleased;
            return new(ok, ok
                ? "Sonde locale terminée · soldes, progression, contrôle et porte restaurés."
                : "La sonde locale n'a pas validé les cinq autorités.");
        }
        catch (Exception exception) when (exception is ArgumentException or
                                               InvalidOperationException or InvalidDataException)
        {
            _liveAuthorityAuditUx9H.Add($"rejected:studio.live-authority:{exception.Message}");
            return new(false, $"La sonde locale d'autorité a échoué : {exception.Message}");
        }
    }

    internal StudioUx9HApprovalSnapshot CaptureLiveAuthorityApprovalUx9H() =>
        new(_approvedLiveAuthorityUx9H, _approvedLiveAuthorityFingerprintUx9H);
    internal void RestoreLiveAuthorityApprovalUx9H(StudioUx9HApprovalSnapshot snapshot)
    {
        _approvedLiveAuthorityUx9H = snapshot.Draft;
        _approvedLiveAuthorityFingerprintUx9H = snapshot.DraftFingerprint;
    }
    internal void CancelLiveAuthorityApprovalUx9H()
    {
        _approvedLiveAuthorityUx9H = null;
        _approvedLiveAuthorityFingerprintUx9H = null;
    }
    internal void CloseUx9H()
    {
        CancelLiveAuthorityApprovalUx9H();
        _liveAuthorityHistoryUx9H = null;
        _persistedLiveAuthorityUx9H = null;
        _liveAuthorityWorkspaceRootUx9H = null;
        _liveAuthorityLoadMessageUx9H = null;
        _liveAuthorityFileMissingUx9H = false;
        _liveAuthorityProbeUx9H = EmptyProbeUx9H();
        _liveAuthorityAuditUx9H.Clear();
    }

    internal StudioUx9HSmokeResult RunUx9HContractSmoke()
    {
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceSession = Session;
        if (sourceProject is null || sourceHistory is null || sourceSession is null)
            return FailedSmokeUx9H();
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var sourceApproval = CaptureLiveAuthorityApprovalUx9H();
        var root = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux9-h-{Guid.NewGuid():N}");
        StudioSessionController? editing = null;
        StudioSessionController? reopened = null;
        var result = FailedSmokeUx9H();
        try
        {
            if (!ProjectSession.Create(root, sourceProject).Succeeded) return result;
            var baseline = LiveDirectorAuthorityProfile.CreateDefault("evidence:ux9-h");
            if (!WorkspaceLiveDirectorAuthorityStore.Save(root, baseline).Succeeded) return result;
            editing = new StudioSessionController();
            if (!editing.Open(root).Succeeded) return result;
            var draft = editing.CreateLiveAuthorityDraftUx9H(explicitlySelected: true);
            if (draft is null) return result;
            var fixture = draft with
            {
                MaximumProtectionMinutes = 120,
                MaximumCurrencyAdjustment = 50_000,
                MaximumWallet = 1_000_000,
                MaximumExperienceAdjustment = 250_000,
                RequiredAtRuntime = true
            };
            var invalid = !editing.PreviewLiveAuthorityUx9H(fixture with
            { MaximumWallet = 1 }).Succeeded;
            var staleSource = !editing.PreviewLiveAuthorityUx9H(fixture with
            { BaseServerRevision = fixture.BaseServerRevision + 1 }).Succeeded;
            var preview = editing.PreviewLiveAuthorityUx9H(fixture);
            var staleApproval = !editing.ApplyApprovedLiveAuthorityUx9H(fixture with
            { MaximumProtectionMinutes = 119 }).Succeeded;
            preview = editing.PreviewLiveAuthorityUx9H(fixture);
            var applied = editing.ApplyApprovedLiveAuthorityUx9H(fixture);
            var current = editing.CurrentLiveAuthorityUx9H;
            if (current is null) return result;
            var fiveRoutes = current.ImpersonationModes.Count == 4 &&
                             current.ImpersonationDisclosure &&
                             current.MaximumProtectionMinutes == 120 &&
                             current.ProtectionScopes.Count == 3 && current.ProtectionIndicator &&
                             current.MaximumCurrencyAdjustment == 50_000 &&
                             current.MaximumWallet == 1_000_000 && current.PersistentWalletAdapter &&
                             current.MaximumExperienceAdjustment == 250_000 &&
                             current.RulesetLevelProjection && current.PersistentProgressionAdapter &&
                             current.DoorStates.Count == 5 && current.PortalRelink &&
                             current.DoorRuntimeAdapter;
            var undo = editing.UndoLiveAuthorityUx9H().Succeeded;
            var redo = editing.RedoLiveAuthorityUx9H().Succeeded;
            var saved = editing.SaveLiveAuthorityUx9H().Succeeded;
            var permission = !editing.RunLocalLiveAuthorityUx9H(true, NetworkRole.Player).Succeeded;
            var confirmation = !editing.RunLocalLiveAuthorityUx9H(false,
                NetworkRole.Administrator).Succeeded;
            var probed = editing.RunLocalLiveAuthorityUx9H(true,
                NetworkRole.Administrator);
            var snapshot = editing.LiveAuthorityProbeUx9H;
            reopened = new StudioSessionController();
            var persistence = saved && reopened.Open(root).Succeeded &&
                              reopened.CurrentLiveAuthorityUx9H is { } loaded &&
                              new LiveDirectorAuthorityStore().Serialize(current) ==
                              new LiveDirectorAuthorityStore().Serialize(loaded);
            var compensation = snapshot.CurrencyCompensationCode ==
                               "LIVE_ECONOMY_COMPENSATED" &&
                               snapshot.ExperienceCompensationCode == "LIVE_RULE_COMPENSATED" &&
                               snapshot.DoorCompensationCode == "LIVE_RULE_COMPENSATED" &&
                               snapshot.ProtectionExpired;
            result = new(true, fiveRoutes, true, invalid, permission, confirmation,
                preview.Succeeded && preview.CanApply && preview.CanonicalDryRun,
                applied.Succeeded, staleSource, staleApproval, undo && redo, persistence,
                snapshot.ImpersonationCode == "LIVE_CONTROL_APPLIED",
                snapshot.ProtectionCode == "LIVE_CONTROL_APPLIED",
                snapshot.CurrencyCode == "LIVE_ECONOMY_APPLIED",
                snapshot.ExperienceCode == "LIVE_RULE_APPLIED",
                snapshot.DoorCode == "LIVE_RULE_APPLIED", compensation,
                editing.LiveAuthorityAuditUx9H.Count >= 8, false, false);
            result = result with
            {
                Succeeded = result.FiveRoutes && result.ExplicitSelection &&
                            result.InvalidBlocked && result.PermissionBlocked &&
                            result.ConfirmationRequired && result.CanonicalDryRun &&
                            result.CanonicalCommand && result.StaleSourceBlocked &&
                            result.StaleApprovalBlocked && result.UndoRedo &&
                            result.Persistence && result.ImpersonationRuntime &&
                            result.ProtectionRuntime && result.CurrencyRuntime &&
                            result.ExperienceRuntime && result.DoorRuntime &&
                            result.Compensation && result.Audit && probed.Succeeded
            };
        }
        finally
        {
            reopened?.Close();
            editing?.Close();
            var released = DeleteUx9HTemporaryWorkspace(root);
            var sourceUnchanged = ReferenceEquals(sourceProject, Project) &&
                                  ReferenceEquals(sourceHistory, History) &&
                                  ReferenceEquals(sourceSession, Session) &&
                                  History?.State.Revision == sourceRevision &&
                                  (History?.Journal ?? []).SequenceEqual(sourceJournal) &&
                                  IsDirty == sourceDirty && ActiveAreaId == sourceArea &&
                                  SelectedEntityIds.SequenceEqual(sourceSelection) &&
                                  SelectedEntityId == sourceSelected &&
                                  CaptureLiveAuthorityApprovalUx9H() == sourceApproval;
            result = result with
            {
                SourceUnchanged = sourceUnchanged,
                TemporaryWorkspaceReleased = released,
                Succeeded = result.Succeeded && sourceUnchanged && released
            };
        }
        return result;
    }

    private bool EnsureLiveAuthorityUx9H()
    {
        var root = Session?.RootDirectory;
        if (string.IsNullOrWhiteSpace(root))
        {
            _liveAuthorityLoadMessageUx9H = "Ouvrez d’abord un module.";
            return false;
        }
        var full = Path.GetFullPath(root);
        if (_liveAuthorityHistoryUx9H is not null &&
            string.Equals(_liveAuthorityWorkspaceRootUx9H, full,
                StringComparison.OrdinalIgnoreCase)) return true;
        var loaded = WorkspaceLiveDirectorAuthorityStore.Load(full);
        if (!loaded.Succeeded)
        {
            _liveAuthorityHistoryUx9H = null;
            _persistedLiveAuthorityUx9H = null;
            _liveAuthorityWorkspaceRootUx9H = full;
            _liveAuthorityFileMissingUx9H = false;
            _liveAuthorityLoadMessageUx9H =
                $"Autorité Live Director invalide : {loaded.Diagnostics.FirstOrDefault()?.Message ?? "diagnostic inconnu"}";
            return false;
        }
        _liveAuthorityHistoryUx9H = new(loaded.Profile);
        _persistedLiveAuthorityUx9H = loaded.Profile;
        _liveAuthorityWorkspaceRootUx9H = full;
        _liveAuthorityFileMissingUx9H = loaded.FileMissing;
        _liveAuthorityLoadMessageUx9H = loaded.FileMissing
            ? "Aucun profil d'autorité enregistré · le profil initial est affiché sans écriture implicite."
            : null;
        return true;
    }

    private static LiveDirectorAuthorityProfile BuildCandidateUx9H(
        LiveDirectorAuthorityProfile current, StudioUx9HAuthorityDraft draft) => current with
        {
            ServerRevision = current.ServerRevision + 1,
            Provenance = "studio:ux9-h",
            RequiredAtRuntime = draft.RequiredAtRuntime,
            ImpersonationModes = ["chat", "dialogue", "voice", "animation"],
            ImpersonationDisclosure = true,
            MaximumProtectionMinutes = draft.MaximumProtectionMinutes,
            ProtectionScopes = ["damage", "effects", "all"],
            ProtectionIndicator = true,
            MaximumCurrencyAdjustment = draft.MaximumCurrencyAdjustment,
            MaximumWallet = draft.MaximumWallet,
            PersistentWalletAdapter = true,
            MaximumExperienceAdjustment = draft.MaximumExperienceAdjustment,
            RulesetLevelProjection = true,
            PersistentProgressionAdapter = true,
            DoorStates = ["locked", "unlocked", "open", "repaired", "relinked"],
            PortalRelink = true,
            DoorRuntimeAdapter = true
        };

    private static LiveAuthorityProbeResultUx9H RunLiveAuthorityProbeUx9H(
        LiveDirectorAuthorityProfile profile, bool applyRuntime, StudioUx9HProbeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.ImpersonationMode is not ("chat" or "dialogue" or "voice" or "animation") ||
            options.Disclosure is not ("visible" or "hidden-by-policy") ||
            options.ProtectionMinutes < 1 ||
            options.ProtectionMinutes > profile.MaximumProtectionMinutes ||
            options.ProtectionScope is not ("damage" or "effects" or "all") ||
            string.IsNullOrWhiteSpace(options.ProtectionIndicator) ||
            options.CurrencyAmount == 0 ||
            Math.Abs((decimal)options.CurrencyAmount) > profile.MaximumCurrencyAdjustment ||
            string.IsNullOrWhiteSpace(options.CurrencyReason) ||
            options.ExperienceAmount == 0 ||
            Math.Abs((decimal)options.ExperienceAmount) > profile.MaximumExperienceAdjustment ||
            string.IsNullOrWhiteSpace(options.ExperienceReason) ||
            options.DoorState is not ("locked" or "unlocked" or "open" or "repaired" or
                "relinked") || options.DoorState == "relinked" &&
            string.IsNullOrWhiteSpace(options.DoorPortal))
            throw new ArgumentException("LIVE_STUDIO_AUTHORITY_OPTIONS_INVALID", nameof(options));

        var now = new DateTimeOffset(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        int Level(long xp) => (int)(xp / 1_000) + 1;
        var runtime = LiveDirectorAuthorityRuntimeAdapter.Create(profile, () => now, Level);
        var zone = Guid.Parse("d1270000-0000-4000-8000-000000000200");
        var actorId = Guid.Parse("d1270000-0000-4000-8000-000000000201");
        var targetId = Guid.Parse("d1270000-0000-4000-8000-000000000202");
        var doorId = Guid.Parse("d1270000-0000-4000-8000-000000000203");
        var walletId = Guid.Parse("d1270000-0000-4000-8000-000000000204");
        runtime.Chooser.Upsert(new(targetId, zone, "creature", "Elyra", "player", null,
            "active", []));
        runtime.Chooser.Upsert(new(doorId, zone, "door", "Porte du sanctuaire", "portal",
            null, "door:locked", []));
        var actor = new AuthorizationSubject(actorId, NetworkRole.Administrator,
            new HashSet<Guid> { zone });
        var audit = new List<string>();
        long controlRevision = 0;
        var renderer = options.ImpersonationMode switch
        {
            "chat" => "text",
            "dialogue" => "cinematic",
            "voice" => "audio",
            _ => "animation"
        };
        var impersonationRequest = new Forge.LiveDirector.Control.LiveDirectorControlService.Request(
            Guid.Parse("d1270000-0000-4000-8000-000000000210"), "director.impersonate",
            targetId, controlRevision, TimeSpan.Zero, new Dictionary<string, string>
            {
                ["mode"] = options.ImpersonationMode,
                ["disclosure"] = options.Disclosure,
                ["renderAdapter"] = renderer
            });
        var impersonation = runtime.Control.Execute(actor, impersonationRequest, false);
        audit.Add(impersonation.AuditEntry);
        var renderReady = impersonation.SemanticDiff.Contains($"renderAdapter={renderer}",
            StringComparison.Ordinal);
        var disclosureReady = impersonation.SemanticDiff.Contains(
            $"disclosure={options.Disclosure}", StringComparison.Ordinal);
        if (applyRuntime)
        {
            impersonation = runtime.Control.Execute(actor, impersonationRequest, true);
            controlRevision = impersonation.Revision;
            renderReady = impersonation.Target?.State.Contains($"renderer:{renderer}",
                StringComparison.Ordinal) == true;
            disclosureReady = impersonation.Target?.State.Contains(
                $"disclosure:{options.Disclosure}", StringComparison.Ordinal) == true;
            audit.Add(impersonation.AuditEntry);
            var restored = runtime.Control.RestoreTarget(actor, targetId,
                "director.impersonate", true);
            controlRevision = restored.Revision;
            audit.Add(restored.AuditEntry);
        }

        var protectionRequest = new Forge.LiveDirector.Control.LiveDirectorControlService.Request(
            Guid.Parse("d1270000-0000-4000-8000-000000000211"), "director.protect",
            targetId, controlRevision, TimeSpan.FromMinutes(options.ProtectionMinutes),
            new Dictionary<string, string>
            {
                ["scope"] = options.ProtectionScope,
                ["indicator"] = options.ProtectionIndicator,
                ["damageAdapter"] = "runtime"
            });
        var protection = runtime.Control.Execute(actor, protectionRequest, false);
        audit.Add(protection.AuditEntry);
        var damageAdapter = protection.SemanticDiff.Contains("damageAdapter=runtime",
            StringComparison.Ordinal);
        var protectionExpired = !applyRuntime;
        if (applyRuntime)
        {
            protection = runtime.Control.Execute(actor, protectionRequest, true);
            controlRevision = protection.Revision;
            damageAdapter = protection.Target?.State.Contains("damage-adapter:runtime",
                StringComparison.Ordinal) == true;
            audit.Add(protection.AuditEntry);
            now = now.AddMinutes(options.ProtectionMinutes + 1);
            var expiry = runtime.Control.ReleaseExpiredProtection(targetId);
            audit.Add(expiry);
            protectionExpired = expiry.StartsWith("LIVE_PROTECTION_RETURNED:",
                StringComparison.Ordinal) && runtime.Chooser.Find(targetId)?.State == "active";
        }

        var currencyCommand = Guid.Parse("d1270000-0000-4000-8000-000000000212");
        var currencyRequest = new LiveEconomyService.Request(currencyCommand, walletId,
            options.CurrencyAmount, options.CurrencyReason, 0);
        var currency = runtime.Economy.Adjust(actor, currencyRequest, false);
        audit.Add(currency.AuditEntry);
        var walletRoundTrip = true;
        var currencyCompensation = "LIVE_ECONOMY_COMPENSATION_NOT_REQUIRED";
        if (applyRuntime)
        {
            currency = runtime.Economy.Adjust(actor, currencyRequest, true);
            audit.Add(currency.AuditEntry);
            var snapshot = runtime.Economy.ExportWallets();
            var restoredWallet = new LiveEconomyService(runtime.Authorization,
                profile.MaximumCurrencyAdjustment, profile.MaximumWallet);
            restoredWallet.ImportWallets(snapshot);
            walletRoundTrip = restoredWallet.Balance(walletId) == currency.Balance;
            var compensated = runtime.Economy.Compensate(actor, currencyCommand,
                currency.Revision, true);
            currencyCompensation = compensated.Code;
            audit.Add(compensated.AuditEntry);
        }

        long progressionRevision = 0;
        var experienceCommand = Guid.Parse("d1270000-0000-4000-8000-000000000213");
        var experienceRequest = new LiveProgressionWorldService.Request(experienceCommand,
            "progression.adjust", targetId, progressionRevision,
            new Dictionary<string, string>
            {
                ["amount"] = options.ExperienceAmount.ToString(),
                ["reason"] = options.ExperienceReason
            });
        var experience = runtime.Progression.Execute(actor, experienceRequest, false);
        audit.Add(experience.AuditEntry);
        var projectedLevel = ExtractProjectedLevelUx9H(experience.SemanticDiff);
        var progressionRoundTrip = true;
        var experienceCompensation = "LIVE_RULE_COMPENSATION_NOT_REQUIRED";
        if (applyRuntime)
        {
            experience = runtime.Progression.Execute(actor, experienceRequest, true);
            progressionRevision = experience.Revision;
            audit.Add(experience.AuditEntry);
            var snapshot = runtime.Progression.ExportExperience();
            var restoredProgression = new LiveProgressionWorldService(runtime.Chooser,
                runtime.Authorization, Level, profile.MaximumExperienceAdjustment);
            restoredProgression.ImportExperience(snapshot);
            progressionRoundTrip = restoredProgression.Experience(targetId) ==
                                   runtime.Progression.Experience(targetId);
            var compensated = runtime.Progression.Compensate(actor, experienceCommand,
                progressionRevision, true);
            progressionRevision = compensated.Revision;
            experienceCompensation = compensated.Code;
            audit.Add(compensated.AuditEntry);
        }

        var doorCommand = Guid.Parse("d1270000-0000-4000-8000-000000000214");
        var doorRequest = new LiveProgressionWorldService.Request(doorCommand, "door.set-state",
            doorId, progressionRevision, new Dictionary<string, string>
            {
                ["state"] = options.DoorState,
                ["portal"] = options.DoorPortal
            });
        var door = runtime.Progression.Execute(actor, doorRequest, false);
        audit.Add(door.AuditEntry);
        var collisionUpdated = door.SemanticDiff.Contains("collision=",
            StringComparison.Ordinal);
        var portalUpdated = door.SemanticDiff.Contains(options.DoorPortal,
            StringComparison.Ordinal);
        var doorCompensation = "LIVE_RULE_COMPENSATION_NOT_REQUIRED";
        if (applyRuntime)
        {
            door = runtime.Progression.Execute(actor, doorRequest, true);
            progressionRevision = door.Revision;
            audit.Add(door.AuditEntry);
            collisionUpdated = runtime.Chooser.Find(doorId)?.State.Contains("collision:",
                StringComparison.Ordinal) == true;
            portalUpdated = runtime.Chooser.Find(doorId)?.State.Contains(options.DoorPortal,
                StringComparison.Ordinal) == true;
            var compensated = runtime.Progression.Compensate(actor, doorCommand,
                progressionRevision, true);
            doorCompensation = compensated.Code;
            audit.Add(compensated.AuditEntry);
        }

        var impersonationReady = impersonation.Code == (applyRuntime
            ? "LIVE_CONTROL_APPLIED" : "LIVE_CONTROL_PREVIEW") && renderReady &&
                                 disclosureReady;
        var protectionReady = protection.Code == (applyRuntime
            ? "LIVE_CONTROL_APPLIED" : "LIVE_CONTROL_PREVIEW") && damageAdapter &&
                              protectionExpired;
        var currencyReady = currency.Code == (applyRuntime
            ? "LIVE_ECONOMY_APPLIED" : "LIVE_ECONOMY_PREVIEW") && walletRoundTrip &&
                            (!applyRuntime || currencyCompensation == "LIVE_ECONOMY_COMPENSATED");
        var experienceReady = experience.Code == (applyRuntime
            ? "LIVE_RULE_APPLIED" : "LIVE_RULE_PREVIEW") && projectedLevel > 1 &&
                              progressionRoundTrip && (!applyRuntime ||
                                  experienceCompensation == "LIVE_RULE_COMPENSATED");
        var doorReady = door.Code == (applyRuntime ? "LIVE_RULE_APPLIED" :
            "LIVE_RULE_PREVIEW") && collisionUpdated && portalUpdated &&
                        (!applyRuntime || doorCompensation == "LIVE_RULE_COMPENSATED");
        var compensationReady = !applyRuntime || protectionExpired &&
                                currencyCompensation == "LIVE_ECONOMY_COMPENSATED" &&
                                experienceCompensation == "LIVE_RULE_COMPENSATED" &&
                                doorCompensation == "LIVE_RULE_COMPENSATED";
        var resourcesReleased = !applyRuntime || runtime.Chooser.Find(targetId)?.State == "active" &&
                                runtime.Economy.Balance(walletId) == 0 &&
                                runtime.Progression.Experience(targetId) == 0 &&
                                runtime.Chooser.Find(doorId)?.State == "door:locked";
        var snapshotResult = new StudioUx9HAuthorityProbe(
            impersonationReady && protectionReady && currencyReady && experienceReady &&
            doorReady && compensationReady && resourcesReleased
                ? "Cinq autorités locales validées" : "Sonde d'autorité incomplète",
            impersonation.Code, renderReady, disclosureReady, protection.Code,
            protectionExpired, damageAdapter, currency.Code, currency.Balance,
            walletRoundTrip, currencyCompensation, experience.Code, projectedLevel,
            progressionRoundTrip, experienceCompensation, door.Code, collisionUpdated,
            portalUpdated, doorCompensation, audit.Count, resourcesReleased);
        return new(impersonationReady, protectionReady, currencyReady, experienceReady,
            doorReady, compensationReady, snapshotResult, audit);
    }

    private static int ExtractProjectedLevelUx9H(string diff)
    {
        var marker = "level=";
        var index = diff.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0) return 0;
        var arrow = diff.IndexOf("->", index, StringComparison.Ordinal);
        if (arrow < 0) return 0;
        var end = diff.IndexOf(':', arrow + 2);
        var value = end < 0 ? diff[(arrow + 2)..] : diff[(arrow + 2)..end];
        return int.TryParse(value, out var level) ? level : 0;
    }

    private static string ProfileFingerprintUx9H(LiveDirectorAuthorityProfile profile) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            new LiveDirectorAuthorityStore().Serialize(profile))));
    private static string DraftFingerprintUx9H(StudioUx9HAuthorityDraft draft) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(draft))));

    private static StudioUx9HAuthorityPreview FailedPreviewUx9H(string message,
        IReadOnlyList<NetworkDiagnostic>? diagnostics = null) => new(false, false, false,
        false, false, false, false, false, message, [], diagnostics?.Select(item =>
            LiveDirectorAuthorityLocalization.French(item.Code)).Distinct().ToArray() ?? [],
        EmptyProbeUx9H());
    private static string HumanDiagnosticsUx9H(IReadOnlyList<NetworkDiagnostic> diagnostics) =>
        diagnostics.Count == 0 ? "La commande n'a produit aucun changement applicable." :
            string.Join(" · ", diagnostics.Select(item =>
                LiveDirectorAuthorityLocalization.French(item.Code)).Distinct());
    private static string HumanDiffUx9H(string value) => value switch
    {
        "impersonation" => "Modes d'impersonation et rendu",
        "protection" => "Protection visible et bornée",
        "currency" => "Plafonds du portefeuille",
        "experience" => "Progression et projection de niveau",
        "door" => "États de porte et portail",
        "runtime" => "Chargement runtime requis",
        _ => value
    };
    private static StudioUx9HAuthorityProbe EmptyProbeUx9H() => new(
        "Aucune sonde exécutée", "—", false, false, "—", false, false, "—", 0,
        false, "—", "—", 0, false, "—", "—", false, false, "—", 0, true);
    private static StudioUx9HSmokeResult FailedSmokeUx9H() => new(
        false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false);
    private static bool DeleteUx9HTemporaryWorkspace(string root)
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
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux9-h-",
                    StringComparison.Ordinal)) return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private sealed record LiveAuthorityProbeResultUx9H(
        bool Impersonation, bool Protection, bool Currency, bool Experience, bool Door,
        bool Compensation, StudioUx9HAuthorityProbe Snapshot, IReadOnlyList<string> Audit);
}
