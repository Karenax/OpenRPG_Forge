using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.LiveDirector.Audit;
using Forge.LiveDirector.Authoring;
using Forge.LiveDirector.Scenes;
using Forge.LiveDirector.State;
using Forge.LiveDirector.World;
using Forge.ModuleFormat.LiveDirector;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private LiveDirectorOperationsHistory? _liveOperationsHistoryUx9J;
    private LiveDirectorOperationsProfile? _persistedLiveOperationsUx9J;
    private string? _liveOperationsWorkspaceRootUx9J;
    private StudioUx9JOperationsDraft? _approvedLiveOperationsUx9J;
    private string? _approvedLiveOperationsFingerprintUx9J;
    private string? _liveOperationsLoadMessageUx9J;
    private bool _liveOperationsFileMissingUx9J;
    private StudioUx9JOperationsProbe _liveOperationsProbeUx9J = EmptyProbeUx9J();
    private readonly List<string> _liveOperationsAuditUx9J = [];

    internal string? LiveOperationsLoadMessageUx9J => _liveOperationsLoadMessageUx9J;
    internal bool LiveOperationsFileMissingUx9J => _liveOperationsFileMissingUx9J;
    internal StudioUx9JOperationsProbe LiveOperationsProbeUx9J => _liveOperationsProbeUx9J;
    internal IReadOnlyList<string> LiveOperationsAuditUx9J => _liveOperationsAuditUx9J.ToArray();
    internal LiveDirectorOperationsProfile? CurrentLiveOperationsUx9J =>
        EnsureLiveOperationsUx9J() ? _liveOperationsHistoryUx9J!.Current : null;
    internal bool LiveOperationsHasUnsavedChangesUx9J =>
        EnsureLiveOperationsUx9J() && _persistedLiveOperationsUx9J is not null &&
        ProfileFingerprintUx9J(_liveOperationsHistoryUx9J!.Current) !=
        ProfileFingerprintUx9J(_persistedLiveOperationsUx9J);

    internal StudioUx9JOperationsDraft? CreateLiveOperationsDraftUx9J(
        bool explicitlySelected = false)
    {
        if (!explicitlySelected || !EnsureLiveOperationsUx9J()) return null;
        var profile = _liveOperationsHistoryUx9J!.Current;
        return new(profile.ServerRevision, ProfileFingerprintUx9J(profile),
            profile.RequiredAtRuntime);
    }

    internal StudioUx9JOperationsPreview PreviewLiveOperationsUx9J(
        StudioUx9JOperationsDraft draft, StudioUx9JOperationsProbeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelLiveOperationsApprovalUx9J();
        if (!EnsureLiveOperationsUx9J())
            return FailedPreviewUx9J(_liveOperationsLoadMessageUx9J ??
                                     "Le profil d’opérations Live Director ne peut pas être chargé.");
        var current = _liveOperationsHistoryUx9J!.Current;
        if (draft.BaseServerRevision != current.ServerRevision ||
            draft.SourceProfileFingerprint != ProfileFingerprintUx9J(current))
            return FailedPreviewUx9J(
                "La configuration a changé ; rechargez le formulaire avant l’aperçu sec.");
        var candidate = BuildCandidateUx9J(current, draft);
        var command = new LiveDirectorOperationsCommand(Guid.NewGuid(), 1, "studio:ux9-j",
            current.ServerRevision, "studio.live.operations", NetworkRole.Administrator, false,
            candidate);
        var dryRun = _liveOperationsHistoryUx9J.Execute(command);
        if (!dryRun.Succeeded || dryRun.Applied)
            return FailedPreviewUx9J(HumanDiagnosticsUx9J(dryRun.Diagnostics),
                dryRun.Diagnostics);
        OperationsProbeResultUx9J probe;
        try
        {
            probe = RunLiveOperationsProbeUx9J(candidate, false,
                options ?? StudioUx9JOperationsProbeOptions.CreateDefault());
        }
        catch (Exception exception) when (exception is ArgumentException or
                                               InvalidOperationException or InvalidDataException)
        {
            return FailedPreviewUx9J(
                $"La sonde locale d’opérations a refusé la configuration : {exception.Message}");
        }
        _liveOperationsProbeUx9J = probe.Snapshot;
        var changes = dryRun.SemanticDiff == "revision-only" ? [] :
            dryRun.SemanticDiff.Split(',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var canApply = changes.Length > 0 && probe.Search && probe.Script && probe.Variable &&
                       probe.Scene && probe.Visibility;
        if (canApply)
        {
            _approvedLiveOperationsUx9J = draft with { };
            _approvedLiveOperationsFingerprintUx9J = DraftFingerprintUx9J(draft);
        }
        return new(true, canApply, true, probe.Search, probe.Script, probe.Variable,
            probe.Scene, probe.Visibility,
            canApply
                ? "Aperçu sec validé · aucun monde, script ou joueur réel n’a été modifié."
                : "Aperçu sec incomplet · vérifiez script, variable, scène et projection.",
            changes.Select(HumanDiffUx9J).ToArray(), [], probe.Snapshot);
    }

    internal StudioUx9JActionResult ApplyApprovedLiveOperationsUx9J(
        StudioUx9JOperationsDraft draft)
    {
        var approved = _approvedLiveOperationsUx9J;
        if (approved is null || _approvedLiveOperationsFingerprintUx9J is null ||
            !EnsureLiveOperationsUx9J())
        {
            CancelLiveOperationsApprovalUx9J();
            return new(false, "Prévisualisez d’abord le profil d’opérations.");
        }
        var current = _liveOperationsHistoryUx9J!.Current;
        if (DraftFingerprintUx9J(draft) != _approvedLiveOperationsFingerprintUx9J ||
            DraftFingerprintUx9J(approved) != _approvedLiveOperationsFingerprintUx9J ||
            approved.BaseServerRevision != current.ServerRevision ||
            approved.SourceProfileFingerprint != ProfileFingerprintUx9J(current))
        {
            CancelLiveOperationsApprovalUx9J();
            return new(false, "La source ou le brouillon a changé ; relancez l’aperçu sec.");
        }
        var command = new LiveDirectorOperationsCommand(Guid.NewGuid(), 1, "studio:ux9-j",
            current.ServerRevision, "studio.live.operations", NetworkRole.Administrator, false,
            BuildCandidateUx9J(current, approved));
        var dryRun = _liveOperationsHistoryUx9J.Execute(command);
        if (!dryRun.Succeeded || dryRun.Applied || dryRun.SemanticDiff == "revision-only")
        {
            CancelLiveOperationsApprovalUx9J();
            return new(false, HumanDiagnosticsUx9J(dryRun.Diagnostics));
        }
        var applied = _liveOperationsHistoryUx9J.Execute(command with { Apply = true });
        CancelLiveOperationsApprovalUx9J();
        if (!applied.Succeeded || !applied.Applied)
            return new(false, HumanDiagnosticsUx9J(applied.Diagnostics));
        _liveOperationsAuditUx9J.Add(applied.AuditEntry);
        return new(true,
            "Profil d’opérations appliqué par la commande canonique · enregistrez pour persister.");
    }

    internal StudioUx9JActionResult UndoLiveOperationsUx9J() => MoveHistoryUx9J(true);
    internal StudioUx9JActionResult RedoLiveOperationsUx9J() => MoveHistoryUx9J(false);

    private StudioUx9JActionResult MoveHistoryUx9J(bool undo)
    {
        CancelLiveOperationsApprovalUx9J();
        if (!EnsureLiveOperationsUx9J())
            return new(false, _liveOperationsLoadMessageUx9J ??
                              "Aucun profil d’opérations actif.");
        var result = undo
            ? _liveOperationsHistoryUx9J!.Undo("studio.live.operations",
                NetworkRole.Administrator)
            : _liveOperationsHistoryUx9J!.Redo("studio.live.operations",
                NetworkRole.Administrator);
        if (result.Succeeded) _liveOperationsAuditUx9J.Add(result.AuditEntry);
        return new(result.Succeeded && result.Applied,
            result.Succeeded ? undo ? "Modification d’opérations annulée." :
                "Modification d’opérations rétablie." :
                HumanDiagnosticsUx9J(result.Diagnostics));
    }

    internal StudioUx9JActionResult SaveLiveOperationsUx9J()
    {
        CancelLiveOperationsApprovalUx9J();
        if (!EnsureLiveOperationsUx9J() || Session is null)
            return new(false, _liveOperationsLoadMessageUx9J ??
                              "Aucun profil d’opérations actif.");
        var result = WorkspaceLiveDirectorOperationsStore.Save(Session.RootDirectory,
            _liveOperationsHistoryUx9J!.Current);
        if (!result.Succeeded)
            return new(false, result.Diagnostics.FirstOrDefault()?.Message ??
                              "Le profil d’opérations n'a pas été enregistré.");
        _persistedLiveOperationsUx9J = _liveOperationsHistoryUx9J.Current;
        _liveOperationsFileMissingUx9J = false;
        return new(true, "Profil d’opérations enregistré atomiquement dans le workspace.");
    }

    internal StudioUx9JActionResult RunLocalLiveOperationsUx9J(bool confirmed,
        NetworkRole role, StudioUx9JOperationsProbeOptions? options = null)
    {
        if (!confirmed)
        {
            _liveOperationsAuditUx9J.Add(
                "rejected:studio.live-operations:confirmation-required");
            return new(false, "Confirmez explicitement la sandbox locale d’opérations.");
        }
        if (role != NetworkRole.Administrator)
        {
            _liveOperationsAuditUx9J.Add(
                "rejected:studio.live-operations:permission-denied");
            return new(false, "La permission Administrateur est requise pour cette sandbox.");
        }
        if (!EnsureLiveOperationsUx9J())
            return new(false, _liveOperationsLoadMessageUx9J ??
                              "Aucun profil d’opérations actif.");
        try
        {
            var probe = RunLiveOperationsProbeUx9J(_liveOperationsHistoryUx9J!.Current, true,
                options ?? StudioUx9JOperationsProbeOptions.CreateDefault());
            _liveOperationsProbeUx9J = probe.Snapshot;
            _liveOperationsAuditUx9J.AddRange(probe.Audit);
            var ok = probe.Search && probe.Script && probe.Variable && probe.Scene &&
                     probe.Visibility && probe.Compensation &&
                     probe.Snapshot.ResourcesReleased;
            return new(ok, ok
                ? "Sandbox terminée · variables et scène restaurées, ressources libérées."
                : "La sandbox locale n'a pas validé les cinq opérations.");
        }
        catch (Exception exception) when (exception is ArgumentException or
                                               InvalidOperationException or InvalidDataException)
        {
            _liveOperationsAuditUx9J.Add(
                $"rejected:studio.live-operations:{exception.Message}");
            return new(false, $"La sandbox locale d’opérations a échoué : {exception.Message}");
        }
    }

    internal StudioUx9JApprovalSnapshot CaptureLiveOperationsApprovalUx9J() =>
        new(_approvedLiveOperationsUx9J, _approvedLiveOperationsFingerprintUx9J);

    internal void RestoreLiveOperationsApprovalUx9J(StudioUx9JApprovalSnapshot snapshot)
    {
        _approvedLiveOperationsUx9J = snapshot.Draft;
        _approvedLiveOperationsFingerprintUx9J = snapshot.DraftFingerprint;
    }

    internal void CancelLiveOperationsApprovalUx9J()
    {
        _approvedLiveOperationsUx9J = null;
        _approvedLiveOperationsFingerprintUx9J = null;
    }

    internal void CloseUx9J()
    {
        CancelLiveOperationsApprovalUx9J();
        _liveOperationsHistoryUx9J = null;
        _persistedLiveOperationsUx9J = null;
        _liveOperationsWorkspaceRootUx9J = null;
        _liveOperationsLoadMessageUx9J = null;
        _liveOperationsFileMissingUx9J = false;
        _liveOperationsProbeUx9J = EmptyProbeUx9J();
        _liveOperationsAuditUx9J.Clear();
    }

    internal StudioUx9JSmokeResult RunUx9JContractSmoke()
    {
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceSession = Session;
        if (sourceProject is null || sourceHistory is null || sourceSession is null)
            return FailedSmokeUx9J();
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var sourceApproval = CaptureLiveOperationsApprovalUx9J();
        var root = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux9-j-{Guid.NewGuid():N}");
        StudioSessionController? editing = null;
        StudioSessionController? reopened = null;
        var result = FailedSmokeUx9J();
        try
        {
            if (!ProjectSession.Create(root, sourceProject).Succeeded) return result;
            var baseline = LiveDirectorOperationsProfile.CreateDefault("evidence:ux9-j");
            if (!WorkspaceLiveDirectorOperationsStore.Save(root, baseline).Succeeded)
                return result;
            editing = new StudioSessionController();
            if (!editing.Open(root).Succeeded) return result;
            var draft = editing.CreateLiveOperationsDraftUx9J(explicitlySelected: true);
            if (draft is null) return result;
            var fixture = draft with { RequiredAtRuntime = true };
            var invalidOptions = StudioUx9JOperationsProbeOptions.CreateDefault() with
            {
                ScriptBudget = 0
            };
            var invalid = !editing.PreviewLiveOperationsUx9J(fixture, invalidOptions).Succeeded;
            var staleSource = !editing.PreviewLiveOperationsUx9J(fixture with
            { BaseServerRevision = fixture.BaseServerRevision + 1 }).Succeeded;
            var preview = editing.PreviewLiveOperationsUx9J(fixture);
            var staleApproval = !editing.ApplyApprovedLiveOperationsUx9J(fixture with
            { RequiredAtRuntime = false }).Succeeded;
            preview = editing.PreviewLiveOperationsUx9J(fixture);
            var applied = editing.ApplyApprovedLiveOperationsUx9J(fixture);
            var current = editing.CurrentLiveOperationsUx9J;
            if (current is null) return result;
            var fiveRoutes = current.IndexedSearch && current.NextMatch &&
                             current.SavedQueries && current.StableDeepLinks &&
                             current.VisualFocus && current.RegisteredCommandsOnly &&
                             current.SignedSnippets &&
                             current.RejectShellProcessFilesystemNetwork &&
                             current.ForgeScriptServerAdapter &&
                             current.VariableKinds.SequenceEqual(
                                 ["bool", "number", "enum", "string"]) &&
                             current.VariableHistory && current.PersistentStateAdapter &&
                             current.SceneFacets.SequenceEqual(
                                 ["spawns", "dialogue", "music", "weather", "triggers"]) &&
                             current.MandatoryRollback && current.SceneRuntimeAdapters &&
                             current.VisibilityLayers.SequenceEqual(
                                 ["hidden-objects", "secrets", "navigation", "encounters"]) &&
                             current.SelectedPlayerProjection && current.ViewportOverlayAdapter;
            var undo = editing.UndoLiveOperationsUx9J().Succeeded;
            var redo = editing.RedoLiveOperationsUx9J().Succeeded;
            var saved = editing.SaveLiveOperationsUx9J().Succeeded;
            var options = StudioUx9JOperationsProbeOptions.CreateDefault();
            var permission = !editing.RunLocalLiveOperationsUx9J(true, NetworkRole.Player,
                options).Succeeded;
            var confirmation = !editing.RunLocalLiveOperationsUx9J(false,
                NetworkRole.Administrator, options).Succeeded;
            var probed = editing.RunLocalLiveOperationsUx9J(true,
                NetworkRole.Administrator, options);
            var snapshot = editing.LiveOperationsProbeUx9J;
            reopened = new StudioSessionController();
            var persistence = saved && reopened.Open(root).Succeeded &&
                              reopened.CurrentLiveOperationsUx9J is { } loaded &&
                              new LiveDirectorOperationsStore().Serialize(current) ==
                              new LiveDirectorOperationsStore().Serialize(loaded);
            result = new(true, fiveRoutes, true, invalid, permission, confirmation,
                preview.Succeeded && preview.CanApply && preview.CanonicalDryRun,
                applied.Succeeded, staleSource, staleApproval, undo && redo, persistence,
                snapshot.SearchMatches == 2 && snapshot.SearchAdvanced &&
                snapshot.StableDeepLink && snapshot.VisualFocus,
                snapshot.ScriptCode == "LIVE_SCRIPT_APPLIED" && snapshot.ScriptOutput &&
                snapshot.ScriptAdapter.StartsWith("forgescript:server:",
                    StringComparison.Ordinal),
                snapshot.VariableCode == "LIVE_VARIABLE_APPLIED" &&
                snapshot.VariablePersisted && snapshot.VariableRolledBack,
                snapshot.SceneCode == "LIVE_SCENE_APPLIED" && snapshot.SceneFacets == 5 &&
                snapshot.SceneRolledBack,
                snapshot.VisibilityLayers == 4 && snapshot.PlayerProjection &&
                snapshot.ViewportOverlay,
                snapshot.Compensated, editing.LiveOperationsAuditUx9J.Count >= 7,
                false, false);
            result = result with
            {
                Succeeded = result.FiveRoutes && result.ExplicitSelection &&
                            result.InvalidBlocked && result.PermissionBlocked &&
                            result.ConfirmationRequired && result.CanonicalDryRun &&
                            result.CanonicalCommand && result.StaleSourceBlocked &&
                            result.StaleApprovalBlocked && result.UndoRedo &&
                            result.Persistence && result.SearchRuntime &&
                            result.ScriptRuntime && result.VariableRuntime &&
                            result.SceneRuntime && result.VisibilityRuntime &&
                            result.Compensation && result.Audit && probed.Succeeded
            };
        }
        finally
        {
            reopened?.Close();
            editing?.Close();
            var released = DeleteUx9JTemporaryWorkspace(root);
            var sourceUnchanged = ReferenceEquals(sourceProject, Project) &&
                                  ReferenceEquals(sourceHistory, History) &&
                                  ReferenceEquals(sourceSession, Session) &&
                                  History?.State.Revision == sourceRevision &&
                                  (History?.Journal ?? []).SequenceEqual(sourceJournal) &&
                                  IsDirty == sourceDirty && ActiveAreaId == sourceArea &&
                                  SelectedEntityIds.SequenceEqual(sourceSelection) &&
                                  SelectedEntityId == sourceSelected &&
                                  CaptureLiveOperationsApprovalUx9J() == sourceApproval;
            result = result with
            {
                SourceUnchanged = sourceUnchanged,
                TemporaryWorkspaceReleased = released,
                Succeeded = result.Succeeded && sourceUnchanged && released
            };
        }
        return result;
    }

    private bool EnsureLiveOperationsUx9J()
    {
        var root = Session?.RootDirectory;
        if (string.IsNullOrWhiteSpace(root))
        {
            _liveOperationsLoadMessageUx9J = "Ouvrez d’abord un module.";
            return false;
        }
        var full = Path.GetFullPath(root);
        if (_liveOperationsHistoryUx9J is not null &&
            string.Equals(_liveOperationsWorkspaceRootUx9J, full,
                StringComparison.OrdinalIgnoreCase)) return true;
        var loaded = WorkspaceLiveDirectorOperationsStore.Load(full);
        if (!loaded.Succeeded)
        {
            _liveOperationsHistoryUx9J = null;
            _persistedLiveOperationsUx9J = null;
            _liveOperationsWorkspaceRootUx9J = full;
            _liveOperationsFileMissingUx9J = false;
            _liveOperationsLoadMessageUx9J =
                $"Profil d’opérations invalide : {loaded.Diagnostics.FirstOrDefault()?.Message ?? "diagnostic inconnu"}";
            return false;
        }
        _liveOperationsHistoryUx9J = new(loaded.Profile);
        _persistedLiveOperationsUx9J = loaded.Profile;
        _liveOperationsWorkspaceRootUx9J = full;
        _liveOperationsFileMissingUx9J = loaded.FileMissing;
        _liveOperationsLoadMessageUx9J = loaded.FileMissing
            ? "Aucun profil d’opérations enregistré · le profil initial est affiché sans écriture implicite."
            : null;
        return true;
    }

    private static LiveDirectorOperationsProfile BuildCandidateUx9J(
        LiveDirectorOperationsProfile current, StudioUx9JOperationsDraft draft) => current with
        {
            ServerRevision = current.ServerRevision + 1,
            Provenance = "studio:ux9-j",
            RequiredAtRuntime = draft.RequiredAtRuntime,
            IndexedSearch = true,
            NextMatch = true,
            SavedQueries = true,
            StableDeepLinks = true,
            VisualFocus = true,
            RegisteredCommandsOnly = true,
            SignedSnippets = true,
            RejectShellProcessFilesystemNetwork = true,
            ForgeScriptServerAdapter = true,
            VariableKinds = ["bool", "number", "enum", "string"],
            VariableHistory = true,
            PersistentStateAdapter = true,
            SceneFacets = ["spawns", "dialogue", "music", "weather", "triggers"],
            MandatoryRollback = true,
            SceneRuntimeAdapters = true,
            VisibilityLayers = ["hidden-objects", "secrets", "navigation", "encounters"],
            SelectedPlayerProjection = true,
            ViewportOverlayAdapter = true
        };

    private static OperationsProbeResultUx9J RunLiveOperationsProbeUx9J(
        LiveDirectorOperationsProfile profile, bool applyRuntime,
        StudioUx9JOperationsProbeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.SearchText) ||
            options.ScriptKind is not ("registered-command" or "signed-snippet") ||
            string.IsNullOrWhiteSpace(options.ScriptSource) ||
            options.ScriptBudget is < 1 or > 10_000 ||
            string.IsNullOrWhiteSpace(options.VariableName) ||
            options.VariableKind is not ("bool" or "number" or "enum" or "string") ||
            string.IsNullOrWhiteSpace(options.VariableValue) ||
            options.VisibilityMode is not ("omniscient" or "selected-player"))
            throw new ArgumentException("LIVE_STUDIO_OPERATIONS_OPTIONS_INVALID",
                nameof(options));

        var runtime = LiveDirectorOperationsRuntimeAdapter.Create(profile,
            value => value.EndsWith(":valid", StringComparison.Ordinal),
            () => new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero));
        var zone = Guid.Parse("d1290000-0000-4000-8000-000000000200");
        var actorId = Guid.Parse("d1290000-0000-4000-8000-000000000201");
        var first = Guid.Parse("d1290000-0000-4000-8000-000000000202");
        var second = Guid.Parse("d1290000-0000-4000-8000-000000000203");
        var player = Guid.Parse("d1290000-0000-4000-8000-000000000204");
        var target = Guid.Parse("d1290000-0000-4000-8000-000000000205");
        var sceneId = Guid.Parse("d1290000-0000-4000-8000-000000000206");
        runtime.Chooser.Upsert(new(first, zone, "creature", "Garde nord", "garde", null,
            "active", [], "main", false, 1, 0, 1));
        runtime.Chooser.Upsert(new(second, zone, "creature", "Garde sud", "garde", null,
            "active", [], "main", false, 8, 0, 2));
        var actor = new AuthorizationSubject(actorId, NetworkRole.Administrator,
            new HashSet<Guid> { zone });
        var operationAudit = new List<string>();

        var queryId = Guid.Parse("d1290000-0000-4000-8000-000000000210");
        runtime.Search.Save(new(queryId, "Gardes",
            new LiveWorldChooser.Query(zone, "creature", options.SearchText, null, null,
                "main")));
        var firstPage = runtime.Search.NextSaved(actor, queryId);
        var secondPage = runtime.Search.NextSaved(actor, queryId);
        var searchAdvanced = firstPage.Current is not null && secondPage.Current is not null &&
                             firstPage.Current.ObjectId != secondPage.Current.ObjectId;
        var deepLink = firstPage.DeepLink?.StartsWith("openrpg://live/zone/",
            StringComparison.Ordinal) == true;
        var visualFocus = firstPage.VisualFocus?.StartsWith("viewport-focus:",
            StringComparison.Ordinal) == true;
        var searchReady = firstPage.Matches.Count == 2 && searchAdvanced && deepLink &&
                          visualFocus;
        operationAudit.Add($"read:live.search:matches={firstPage.Matches.Count}:next={searchAdvanced}");

        runtime.Scripts.Register(new("announce", arguments =>
            new Dictionary<string, string> { ["message"] = arguments["message"] }));
        var scriptRequest = new Forge.LiveDirector.Scripting.LiveScriptService.Request(
            Guid.Parse("d1290000-0000-4000-8000-000000000211"), options.ScriptKind,
            options.ScriptSource, options.ScriptSignature, options.ScriptBudget, 0,
            new Dictionary<string, string> { ["message"] = "Bienvenue dans la Crypte" });
        var scriptResult = runtime.Scripts.Run(actor, scriptRequest, false);
        operationAudit.Add(scriptResult.AuditEntry);
        if (applyRuntime && scriptResult.Code == "LIVE_SCRIPT_PREVIEW")
        {
            scriptResult = runtime.Scripts.Run(actor, scriptRequest, true);
            operationAudit.Add(scriptResult.AuditEntry);
        }
        var scriptReady = scriptResult.Code == (applyRuntime ? "LIVE_SCRIPT_APPLIED" :
            "LIVE_SCRIPT_PREVIEW") &&
                          scriptResult.ServerAdapter.StartsWith("forgescript:server:",
                              StringComparison.Ordinal) && scriptResult.Output.Count > 0;

        var definition = VariableDefinitionUx9J(options);
        runtime.Variables.Define(definition);
        var variableRequest = new LiveVariableService.Request(
            Guid.Parse("d1290000-0000-4000-8000-000000000212"), target,
            options.VariableName, options.VariableValue, "scène préparée", 0);
        var variableResult = runtime.Variables.Set(actor, variableRequest, false);
        operationAudit.Add(variableResult.Diff);
        var variablePersisted = !applyRuntime;
        var variableRolledBack = !applyRuntime;
        if (applyRuntime && variableResult.Code == "LIVE_VARIABLE_PREVIEW")
        {
            variableResult = runtime.Variables.Set(actor, variableRequest, true);
            operationAudit.Add(variableResult.Diff);
            var persisted = runtime.Variables.ExportState();
            var restored = new LiveVariableService(runtime.Authorization,
                new LiveAuditJournal(() => DateTimeOffset.UnixEpoch));
            restored.Define(definition);
            restored.RestoreState(persisted);
            variablePersisted = restored.Read(target, options.VariableName) ==
                                options.VariableValue;
            variableRolledBack = runtime.Variables.Rollback(actor, "fin de sandbox",
                                     Guid.Parse("d1290000-0000-4000-8000-000000000213")) ==
                                 "LIVE_VARIABLE_ROLLED_BACK" &&
                                 runtime.Variables.Read(target, options.VariableName) is null;
            operationAudit.Add($"compensated:live.variable={variableRolledBack}");
        }
        var variableReady = variableResult.Code == (applyRuntime ? "LIVE_VARIABLE_APPLIED" :
            "LIVE_VARIABLE_PREVIEW") && variablePersisted && variableRolledBack;

        runtime.Scenes.Save(new(sceneId, "Tempête sur la Crypte", ["garde", "spectre"],
            "warning", "storm", "rain", ["gate-enter"],
            ["despawn", "restore-dialogue", "restore-music", "restore-weather",
                "restore-trigger"]));
        var sceneResult = runtime.Scenes.Run(actor, sceneId,
            Guid.Parse("d1290000-0000-4000-8000-000000000214"), "temps fort", 0, false);
        operationAudit.Add(sceneResult.AuditEntry);
        var sceneCode = sceneResult.Code;
        var sceneRolledBack = !applyRuntime;
        if (applyRuntime && sceneResult.Code == "LIVE_SCENE_PREVIEW")
        {
            sceneResult = runtime.Scenes.Run(actor, sceneId,
                Guid.Parse("d1290000-0000-4000-8000-000000000214"), "temps fort", 0, true);
            sceneCode = sceneResult.Code;
            operationAudit.Add(sceneResult.AuditEntry);
            var rollback = runtime.Scenes.Rollback(actor,
                Guid.Parse("d1290000-0000-4000-8000-000000000215"), "fin de sandbox",
                sceneResult.Revision, true);
            operationAudit.Add(rollback.AuditEntry);
            sceneRolledBack = rollback.Code == "LIVE_SCENE_ROLLED_BACK" &&
                              rollback.Restored && runtime.Scenes.ActiveSceneCount == 0;
        }
        var sceneReady = sceneCode == (applyRuntime ? "LIVE_SCENE_APPLIED" :
            "LIVE_SCENE_PREVIEW") && sceneResult.AdaptedFacets == 5 && sceneRolledBack;

        var view = runtime.Visibility.Configure(actor, options.VisibilityMode,
            options.VisibilityMode == "selected-player" ? player : null,
            true, true, true, true);
        operationAudit.Add(view.AuditEntry);
        var visibilityReady = view.LayerCount == 4 &&
                              view.ViewportOverlay.StartsWith("viewport-overlay:",
                                  StringComparison.Ordinal) &&
                              (options.VisibilityMode != "selected-player" ||
                               view.PlayerProjection);

        var compensated = !applyRuntime || variableRolledBack && sceneRolledBack &&
                          runtime.Scenes.ActiveSceneCount == 0;
        var resourcesReleased = compensated;
        var snapshot = new StudioUx9JOperationsProbe(
            searchReady && scriptReady && variableReady && sceneReady && visibilityReady &&
            compensated ? "Cinq opérations locales validées" : "Sandbox d’opérations incomplète",
            firstPage.Matches.Count, searchAdvanced, deepLink, visualFocus, scriptResult.Code,
            scriptResult.ServerAdapter, scriptResult.Output.Count > 0, variableResult.Code,
            options.VariableKind, variablePersisted, variableRolledBack, sceneCode,
            sceneResult.AdaptedFacets, sceneRolledBack, view.Mode, view.LayerCount,
            view.PlayerProjection, view.ViewportOverlay.StartsWith("viewport-overlay:",
                StringComparison.Ordinal), compensated,
            operationAudit.Count + runtime.Audit.Entries.Count, resourcesReleased);
        return new(searchReady, scriptReady, variableReady, sceneReady, visibilityReady,
            compensated, snapshot, operationAudit);
    }

    private static LiveVariableService.Definition VariableDefinitionUx9J(
        StudioUx9JOperationsProbeOptions options) => new(options.VariableName,
        options.VariableKind, options.VariableKind == "number" ? 0 : null,
        options.VariableKind == "number" ? 100 : null,
        options.VariableKind == "enum"
            ? new HashSet<string>(StringComparer.Ordinal) { "calm", "tense", "crisis" }
            : new HashSet<string>(StringComparer.Ordinal));

    private static string ProfileFingerprintUx9J(LiveDirectorOperationsProfile profile) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            new LiveDirectorOperationsStore().Serialize(profile))));

    private static string DraftFingerprintUx9J(StudioUx9JOperationsDraft draft) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(draft))));

    private static StudioUx9JOperationsPreview FailedPreviewUx9J(string message,
        IReadOnlyList<NetworkDiagnostic>? diagnostics = null) => new(false, false, false,
        false, false, false, false, false, message, [], diagnostics?.Select(item =>
            LiveDirectorOperationsLocalization.French(item.Code)).Distinct().ToArray() ?? [],
        EmptyProbeUx9J());

    private static string HumanDiagnosticsUx9J(
        IReadOnlyList<NetworkDiagnostic> diagnostics) => diagnostics.Count == 0
        ? "La commande n'a produit aucun changement applicable."
        : string.Join(" · ", diagnostics.Select(item =>
            LiveDirectorOperationsLocalization.French(item.Code)).Distinct());

    private static string HumanDiffUx9J(string value) => value switch
    {
        "search" => "Recherche indexée, suivant et focus",
        "script" => "ForgeScript enregistré ou signé",
        "variables" => "Variables typées et état persistant",
        "scenes" => "Scènes multi-facettes et rollback",
        "visibility" => "Visibilité MJ et overlay viewport",
        "runtime" => "Chargement runtime requis",
        _ => value
    };

    private static StudioUx9JOperationsProbe EmptyProbeUx9J() => new(
        "Aucune sandbox exécutée", 0, false, false, false, "—", "—", false,
        "—", "—", false, false, "—", 0, false, "—", 0, false, false,
        false, 0, true);

    private static StudioUx9JSmokeResult FailedSmokeUx9J() => new(
        false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false);

    private static bool DeleteUx9JTemporaryWorkspace(string root)
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
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux9-j-",
                    StringComparison.Ordinal)) return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private sealed record OperationsProbeResultUx9J(
        bool Search, bool Script, bool Variable, bool Scene, bool Visibility,
        bool Compensation, StudioUx9JOperationsProbe Snapshot, IReadOnlyList<string> Audit);
}
