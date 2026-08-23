using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Mcp;
using Forge.Mcp.Scripts;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Scripting;
using Forge.Scripting;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private ForgeScriptInteropHistory? _interopHistoryUx6P;
    private ForgeScriptInteropProfile? _persistedInteropUx6P;
    private StudioUx6PInteropDraft? _approvedInteropUx6P;
    private long? _approvedInteropRevisionUx6P;
    private string? _approvedInteropFingerprintUx6P;
    private string? _approvedInteropScriptFingerprintUx6P;

    internal StudioUx6PInteropDraft? CurrentScriptInteropUx6P(StudioUx6FDraft source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!EnsureInteropUx6P(out _)) return null;
        return ToInteropDraftUx6P(_interopHistoryUx6P!.Profile, _interopHistoryUx6P.Revision, source);
    }

    internal bool ScriptInteropHasUnsavedChangesUx6P =>
        _interopHistoryUx6P is not null &&
        (_persistedInteropUx6P is null ||
         !string.Equals(InteropFingerprintUx6P(_interopHistoryUx6P.Profile),
             InteropFingerprintUx6P(_persistedInteropUx6P), StringComparison.Ordinal));

    internal StudioUx6PPreview PreviewScriptInteropUx6P(
        StudioUx6PInteropDraft draft,
        StudioUx6FDraft source)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(source);
        CancelUx6PApproval();
        if (!EnsureInteropUx6P(out var failure)) return FailedInteropPreviewUx6P(failure);
        var history = _interopHistoryUx6P!;
        var diagnostics = ValidateInteropDraftUx6P(draft, source, history).ToList();
        if (diagnostics.Count > 0)
            return new(false, false, true, false, false,
                "Aperçu refusé · corrigez les champs localisés.", "no-change", diagnostics);

        try
        {
            var candidate = ToInteropProfileUx6P(draft);
            var command = new ForgeScriptInteropCommand(Guid.NewGuid(), history.Revision, candidate,
                "UX6-P R101 interop preview");
            var profilePreview = history.Preview(command);
            diagnostics.AddRange(profilePreview.Diagnostics.Select(ToStudioInteropDiagnosticUx6P));
            if (!profilePreview.Succeeded)
                return new(false, false, true, false, false,
                    "Aperçu refusé · le profil R101 est invalide.", profilePreview.SemanticDiff, diagnostics);

            var service = new ForgeScriptInteropService(new NativeForgeScriptRuntime(), candidate);
            var sourcePlan = service.PlanCreateOrUpdateSource(SourceTransactionUx6P(draft, source, service.Revision));
            diagnostics.AddRange(sourcePlan.Diagnostics.Select(ToStudioInteropDiagnosticUx6P));
            if (!sourcePlan.Succeeded || sourcePlan.PlanId is null)
                return new(false, false, true, false, false,
                    "Aperçu refusé · le plan source MCP est invalide.", sourcePlan.SemanticDiff, diagnostics);
            var sourceApply = service.ApplySourcePlan(sourcePlan.PlanId);
            diagnostics.AddRange(sourceApply.Diagnostics.Select(ToStudioInteropDiagnosticUx6P));
            var bindingPlan = sourceApply.Succeeded
                ? service.PlanEventBinding(BindingRequestUx6P(draft, service.Revision))
                : null;
            if (bindingPlan is not null)
                diagnostics.AddRange(bindingPlan.Diagnostics.Select(ToStudioInteropDiagnosticUx6P));
            var sourceReady = sourceApply.Succeeded;
            var bindingReady = bindingPlan is { Succeeded: true, PlanId: not null };
            if (!sourceReady || !bindingReady)
                return new(false, false, true, sourceReady, bindingReady,
                    "Aperçu refusé · la source ou la liaison typée ne respecte pas R101.",
                    bindingPlan?.SemanticDiff ?? sourcePlan.SemanticDiff, diagnostics);

            var changed = !string.Equals(InteropFingerprintUx6P(history.Profile),
                InteropFingerprintUx6P(candidate), StringComparison.Ordinal);
            if (changed)
            {
                _approvedInteropUx6P = draft;
                _approvedInteropRevisionUx6P = history.Revision;
                _approvedInteropFingerprintUx6P = InteropFingerprintUx6P(candidate);
                _approvedInteropScriptFingerprintUx6P = SourceFingerprintUx6P(source);
            }
            var semanticDiff = string.Join(" | ",
                new[] { profilePreview.SemanticDiff, sourcePlan.SemanticDiff, bindingPlan!.SemanticDiff }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
            return new(true, changed, true, true, true,
                changed
                    ? "Aperçu sec valide · profil, source et liaison R101 sont cohérents."
                    : "Aperçu sec valide · aucune valeur de profil n’a changé.",
                semanticDiff, diagnostics);
        }
        catch (Exception exception) when (InteropExceptionUx6P(exception))
        {
            return FailedInteropPreviewUx6P($"Aperçu R101 refusé : {exception.Message}");
        }
    }

    internal StudioUx6PSimulation SimulateScriptInteropUx6P(
        StudioUx6PInteropDraft draft,
        StudioUx6FDraft source)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(source);
        if (!EnsureInteropUx6P(out var failure)) return FailedInteropSimulationUx6P(failure);
        var validation = ValidateInteropDraftUx6P(draft, source, _interopHistoryUx6P!).ToArray();
        if (validation.Length > 0)
            return FailedInteropSimulationUx6P(
                "Simulation refusée · le profil, la liaison ou la source a changé.", validation);

        try
        {
            var profile = ToInteropProfileUx6P(draft);
            var service = new ForgeScriptInteropService(new NativeForgeScriptRuntime(), profile);
            var api = service.GetApi().ApiVersion == profile.ApiVersion;

            var sourcePlan = service.PlanCreateOrUpdateSource(SourceTransactionUx6P(draft, source, 0));
            var sourceApply = sourcePlan.PlanId is null
                ? null
                : service.ApplySourcePlan(sourcePlan.PlanId);
            var sourceUndo = sourcePlan.PlanId is null
                ? null
                : service.RevertSourcePlan(sourcePlan.PlanId);
            var sourceRedo = sourcePlan.PlanId is null
                ? null
                : service.RedoSourcePlan(sourcePlan.PlanId);
            var sourceTransactions = sourcePlan.Succeeded && sourceApply?.Applied == true &&
                                     sourceUndo?.Reverted == true && sourceRedo is { Applied: true, Reverted: false } &&
                                     service.GetSources().Count == 1;
            var compilation = service.Compile(source.RelativePath);
            var compileReady = compilation.Succeeded && service.GetDiagnostics(source.RelativePath).Count == 0;
            var tests = service.RunTests(source.RelativePath);
            var sandboxTests = tests.Succeeded && tests.Events.Any(item => item.Kind == "say");

            var bindingPreview = service.PreviewBinding(draft.EntityId, draft.EventName, source.RelativePath);
            var bindingPlan = service.PlanEventBinding(BindingRequestUx6P(draft, service.Revision));
            var bindingApply = bindingPlan.PlanId is null
                ? null
                : service.ApplyEventBindingPlan(bindingPlan.PlanId);
            var bindingUndo = bindingPlan.PlanId is null
                ? null
                : service.RevertEventBindingPlan(bindingPlan.PlanId);
            var bindingRedo = bindingPlan.PlanId is null
                ? null
                : service.RedoEventBindingPlan(bindingPlan.PlanId);
            var bindingTransactions = bindingPreview.Resolved && bindingPlan.Succeeded &&
                                      bindingApply?.Applied == true && bindingUndo?.Reverted == true &&
                                      bindingRedo is { Applied: true, Reverted: false } &&
                                      service.GetBindings().Count == 1;

            var snapshot = service.ExportSnapshot();
            var restored = new ForgeScriptInteropService(new NativeForgeScriptRuntime(), profile);
            restored.RestoreSnapshot(snapshot);
            var snapshotRoundTrip = restored.Revision == service.Revision &&
                                    restored.GetSources().SequenceEqual(service.GetSources()) &&
                                    restored.GetBindings().SequenceEqual(service.GetBindings()) &&
                                    string.Equals(snapshot, restored.ExportSnapshot(), StringComparison.Ordinal);

            var document = new ForgeScriptSourceStore(Session!.RootDirectory).Load(source.RelativePath) with
            {
                EventBindings = [],
            };
            var adapter = new ForgeScriptInteropRuntimeAdapter();
            var resolution = adapter.Resolve(profile, document, profile.RequiredAtRuntime);
            var optional = adapter.Resolve(null, null, false);
            var required = adapter.Resolve(null, document, true);
            var runtimeAdapter = resolution.Available && resolution.Handle is not null &&
                                 optional.OptionalFallback &&
                                 required.DiagnosticCode == ForgeScriptInteropRuntimeAdapter.RequiredMissingCode;
            var native = runtimeAdapter
                ? adapter.Execute(resolution.Handle!, adapter.Build(resolution.Handle!, profile, document),
                    new NativeForgeScriptRuntime())
                : null;
            var nativeSandbox = native?.Status == ForgeScriptExecutionStatus.Completed;

            var abuse = new ForgeScriptInteropService(new NativeForgeScriptRuntime(), profile);
            var abuseCreate = abuse.CreateOrUpdateSource(new(Guid.NewGuid(), 0, "scripts/abuse.as",
                "void main() { while (true) { int value = 1; } }", "studio:ux6-p:abuse"));
            var abuseResult = abuseCreate.Succeeded
                ? abuse.RunTests("scripts/abuse.as", new ForgeScriptQuotas(10, 1000, 32, 8))
                : null;
            var healthyCreate = abuse.CreateOrUpdateSource(new(Guid.NewGuid(), abuse.Revision,
                "scripts/recovered.as", "void main() { say(\"recovered\"); }", "studio:ux6-p:recovery"));
            var healthy = healthyCreate.Succeeded ? abuse.RunTests("scripts/recovered.as") : null;
            var quotaRecovery = abuseResult?.Status == "quotaexceeded" && healthy?.Succeeded == true &&
                                healthy.Events.Any(item => item.Kind == "say" && item.Value == "recovered");

            var diagnostics = sourcePlan.Diagnostics.Concat(sourceApply?.Diagnostics ?? [])
                .Concat(bindingPlan.Diagnostics).Concat(bindingApply?.Diagnostics ?? [])
                .Select(ToStudioInteropDiagnosticUx6P).ToArray();
            var succeeded = api && sourceTransactions && compileReady && sandboxTests && bindingTransactions &&
                            snapshotRoundTrip && runtimeAdapter && nativeSandbox && quotaRecovery;
            var results = new[]
            {
                $"API versionnée · {service.GetApi().ApiVersion}",
                $"Source MCP · plan, apply, undo, redo · révision {service.Revision}",
                $"Compilation · {(compileReady ? "réussie" : "échec")} · tests {tests.Status}",
                $"Liaison · {draft.TargetLabel} · {draft.EventName} → {draft.Handler}",
                $"Snapshot · {service.GetSources().Count} source(s), {service.GetBindings().Count} liaison(s)",
                $"Adaptateur runtime · {(runtimeAdapter ? "résolu" : "indisponible")}",
                $"Sandbox native · {native?.Status}",
                $"Quota et récupération · {(quotaRecovery ? "interruption puis reprise" : "échec")}",
            };
            return new(succeeded,
                succeeded
                    ? "Interop R101 exercée · éditeur, MCP et adaptateur partagent la même source copiée."
                    : "L’interop R101 n’a pas satisfait tous les invariants.",
                api, sourceTransactions, compileReady, sandboxTests, bindingTransactions,
                snapshotRoundTrip, runtimeAdapter, nativeSandbox, quotaRecovery, results, diagnostics);
        }
        catch (Exception exception) when (InteropExceptionUx6P(exception))
        {
            return FailedInteropSimulationUx6P($"Simulation R101 refusée : {exception.Message}");
        }
    }

    internal StudioUx6PActionResult ApplyApprovedScriptInteropUx6P(
        StudioUx6PInteropDraft draft,
        StudioUx6FDraft source)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(source);
        if (!EnsureInteropUx6P(out var failure)) return FailedInteropActionUx6P(failure);
        var history = _interopHistoryUx6P!;
        var candidate = ToInteropProfileUx6P(draft);
        var candidateFingerprint = InteropFingerprintUx6P(candidate);
        var sourceFingerprint = SourceFingerprintUx6P(source);
        if (_approvedInteropUx6P is null || _approvedInteropRevisionUx6P != history.Revision ||
            !string.Equals(_approvedInteropFingerprintUx6P, candidateFingerprint, StringComparison.Ordinal) ||
            !string.Equals(_approvedInteropScriptFingerprintUx6P, sourceFingerprint, StringComparison.Ordinal) ||
            !string.Equals(InteropFingerprintUx6P(ToInteropProfileUx6P(_approvedInteropUx6P)),
                candidateFingerprint, StringComparison.Ordinal) ||
            ValidateInteropDraftUx6P(draft, source, history).Count > 0)
            return FailedInteropActionUx6P(
                "Application refusée · relancez l’aperçu sec du brouillon courant.");

        var command = new ForgeScriptInteropCommand(Guid.NewGuid(), history.Revision, candidate,
            "UX6-P R101 interop apply");
        var preview = history.Preview(command);
        if (!preview.Succeeded)
            return FailedInteropActionUx6P(string.Join(" · ", preview.Diagnostics.Select(item => item.Code)));
        var path = InteropPathUx6P(Session!.RootDirectory);
        var existed = File.Exists(path);
        var bytes = existed ? File.ReadAllBytes(path) : null;
        try
        {
            new ForgeScriptInteropProfileStore(Session.RootDirectory).Save(candidate);
            var applied = history.Apply(command);
            if (!applied.Succeeded)
            {
                RestoreInteropFileUx6P(path, existed, bytes);
                return FailedInteropActionUx6P("La commande R101 a été refusée après prévisualisation.");
            }
            _persistedInteropUx6P = candidate;
            CancelUx6PApproval();
            return new(true, "Profil d’interop R101 appliqué atomiquement.", history.CanUndo, history.CanRedo);
        }
        catch (Exception exception) when (InteropExceptionUx6P(exception))
        {
            RestoreInteropFileUx6P(path, existed, bytes);
            return FailedInteropActionUx6P($"Écriture refusée : {exception.Message}");
        }
    }

    internal StudioUx6PActionResult UndoScriptInteropUx6P() => MutateInteropHistoryUx6P(undo: true);
    internal StudioUx6PActionResult RedoScriptInteropUx6P() => MutateInteropHistoryUx6P(undo: false);

    internal StudioUx6PActionResult SaveScriptInteropUx6P()
    {
        if (!EnsureInteropUx6P(out var failure)) return FailedInteropActionUx6P(failure);
        try
        {
            var profile = _interopHistoryUx6P!.Profile;
            new ForgeScriptInteropProfileStore(Session!.RootDirectory).Save(profile);
            _persistedInteropUx6P = profile;
            return new(true, "Profil d’interop R101 enregistré.", _interopHistoryUx6P.CanUndo,
                _interopHistoryUx6P.CanRedo);
        }
        catch (Exception exception) when (InteropExceptionUx6P(exception))
        {
            return FailedInteropActionUx6P($"Enregistrement refusé : {exception.Message}");
        }
    }

    internal StudioUx6PApprovalSnapshot CaptureScriptInteropApprovalUx6P() => new(
        _approvedInteropUx6P, _approvedInteropRevisionUx6P, _approvedInteropFingerprintUx6P,
        _approvedInteropScriptFingerprintUx6P);

    internal void RestoreScriptInteropApprovalUx6P(StudioUx6PApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedInteropUx6P = snapshot.Draft;
        _approvedInteropRevisionUx6P = snapshot.Revision;
        _approvedInteropFingerprintUx6P = snapshot.CandidateFingerprint;
        _approvedInteropScriptFingerprintUx6P = snapshot.ScriptFingerprint;
    }

    internal void CancelUx6PApproval()
    {
        _approvedInteropUx6P = null;
        _approvedInteropRevisionUx6P = null;
        _approvedInteropFingerprintUx6P = null;
        _approvedInteropScriptFingerprintUx6P = null;
    }

    internal void CloseUx6P()
    {
        _interopHistoryUx6P = null;
        _persistedInteropUx6P = null;
        CancelUx6PApproval();
    }

    internal StudioUx6PSmokeResult RunUx6PContractSmoke()
    {
        var sourceProject = Project;
        if (Session is null || sourceProject is null || History is null) return FailedInteropSmokeUx6P();
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux6-p", Guid.NewGuid().ToString("N"));
        var released = false;
        StudioUx6PSmokeResult result;
        try
        {
            if (!ProjectSession.Create(root, sourceProject).Succeeded) return FailedInteropSmokeUx6P();
            var document = EvidenceDocumentUx6F();
            new ForgeScriptSourceStore(root).Save(document);
            var editing = new StudioSessionController();
            if (!editing.Open(root).Succeeded ||
                !editing.TryOpenCanonicalSourceUx6F(document.RelativePath, out var source, out _) || source is null)
                return FailedInteropSmokeUx6P();
            var initial = editing.CurrentScriptInteropUx6P(source);
            if (initial is null) return FailedInteropSmokeUx6P();
            var candidate = initial with
            {
                Provenance = "studio:ux6-p:r101",
                MaximumSources = initial.MaximumSources + 1,
                MaximumSourceBytes = initial.MaximumSourceBytes + 1024,
                Priority = 90,
            };
            var invalidProfiles =
                !editing.PreviewScriptInteropUx6P(candidate with { EventBinding = "" }, source).Succeeded &&
                !editing.PreviewScriptInteropUx6P(candidate with { MaximumSources = 0 }, source).Succeeded &&
                !editing.PreviewScriptInteropUx6P(candidate with { TestInstructionLimit = 0 }, source).Succeeded;
            var invalidSource = !editing.PreviewScriptInteropUx6P(candidate with
            {
                ScriptRelativePath = "../unsafe.as",
            }, source).Succeeded;
            var invalidBinding = !editing.PreviewScriptInteropUx6P(candidate with
            {
                Handler = "MissingHandler",
            }, source).Succeeded;
            var staleSource = !editing.PreviewScriptInteropUx6P(candidate with
            {
                ScriptSourceFingerprint = "stale",
            }, source).Succeeded;
            var preview = editing.PreviewScriptInteropUx6P(candidate, source);
            var simulation = editing.SimulateScriptInteropUx6P(candidate, source);
            var staleApproval = !editing.ApplyApprovedScriptInteropUx6P(candidate with
            {
                MaximumSources = candidate.MaximumSources + 1,
            }, source).Succeeded;
            preview = editing.PreviewScriptInteropUx6P(candidate, source);
            var apply = editing.ApplyApprovedScriptInteropUx6P(candidate, source);
            var undo = editing.UndoScriptInteropUx6P();
            var redo = editing.RedoScriptInteropUx6P();
            var save = editing.SaveScriptInteropUx6P();
            var persisted = new ForgeScriptInteropProfileStore(root).Load();
            var reopened = new StudioSessionController();
            var reopenedOkay = reopened.Open(root).Succeeded &&
                               reopened.TryOpenCanonicalSourceUx6F(document.RelativePath, out var reopenedSource,
                                   out _) && reopenedSource is not null &&
                               reopened.CurrentScriptInteropUx6P(reopenedSource) is { } reopenedDraft &&
                               string.Equals(InteropFingerprintUx6P(ToInteropProfileUx6P(reopenedDraft)),
                                   InteropFingerprintUx6P(ToInteropProfileUx6P(candidate)),
                                   StringComparison.Ordinal);
            reopened.Close();
            var sourcePath = Path.Combine(root, document.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            var sourcePreserved = File.ReadAllText(sourcePath) == document.Source &&
                                  string.Equals(SourceFingerprintUx6P(
                                          new ForgeScriptSourceStore(root).Load(document.RelativePath)),
                                      SourceFingerprintUx6P(document), StringComparison.Ordinal);
            var projectNeighbors = editing.Project is not null &&
                                   ProjectJsonSerializer.Serialize(editing.Project) ==
                                   ProjectJsonSerializer.Serialize(sourceProject);
            var applyUndoRedo = apply.Succeeded && undo.Succeeded && redo.Succeeded;
            var persistence = save.Succeeded && reopenedOkay &&
                              string.Equals(InteropFingerprintUx6P(persisted),
                                  InteropFingerprintUx6P(ToInteropProfileUx6P(candidate)),
                                  StringComparison.Ordinal);
            var succeeded = invalidProfiles && invalidSource && invalidBinding && staleSource &&
                            preview.Succeeded && preview.CanonicalDryRun && preview.SourcePlan &&
                            preview.BindingPlan && simulation.Succeeded && simulation.Api &&
                            simulation.SourceTransactions && simulation.Compilation && simulation.SandboxTests &&
                            simulation.BindingTransactions && simulation.SnapshotRoundTrip &&
                            simulation.RuntimeAdapter && simulation.NativeSandbox && simulation.QuotaRecovery &&
                            staleApproval && applyUndoRedo && persistence && sourcePreserved && projectNeighbors;
            result = new(succeeded, true, true, simulation.Api, simulation.SourceTransactions,
                simulation.Compilation, simulation.SandboxTests, simulation.BindingTransactions,
                simulation.SnapshotRoundTrip, simulation.RuntimeAdapter, simulation.NativeSandbox,
                simulation.QuotaRecovery, invalidProfiles, invalidSource, invalidBinding,
                preview.CanonicalDryRun, staleSource, staleApproval, applyUndoRedo, persistence,
                sourcePreserved, projectNeighbors, false);
            editing.Close();
        }
        catch (Exception)
        {
            result = FailedInteropSmokeUx6P();
        }
        finally
        {
            released = DeleteUx6FTemporaryWorkspace(root);
        }
        return result with { Succeeded = result.Succeeded && released, TemporaryWorkspaceReleased = released };
    }

    private bool EnsureInteropUx6P(out string failure)
    {
        failure = string.Empty;
        if (_interopHistoryUx6P is not null) return true;
        if (Session is null)
        {
            failure = "Ouvrez un module avant de configurer l’interop ForgeScript.";
            return false;
        }
        try
        {
            var path = InteropPathUx6P(Session.RootDirectory);
            var existed = File.Exists(path);
            var profile = existed ? new ForgeScriptInteropProfileStore(Session.RootDirectory).Load() :
                ForgeScriptInteropProfile.Default;
            _interopHistoryUx6P = new ForgeScriptInteropHistory(profile);
            _persistedInteropUx6P = existed ? profile : null;
            return true;
        }
        catch (Exception exception) when (InteropExceptionUx6P(exception))
        {
            failure = $"Le profil d’interop R101 est invalide : {exception.Message}";
            return false;
        }
    }

    private IReadOnlyList<StudioUx6FDiagnostic> ValidateInteropDraftUx6P(
        StudioUx6PInteropDraft draft,
        StudioUx6FDraft source,
        ForgeScriptInteropHistory history)
    {
        var diagnostics = new List<StudioUx6FDiagnostic>();
        if (draft.BaseRevision != history.Revision)
            diagnostics.Add(new("script.interop.revision_conflict", "script/interop/revision", "error",
                "Le profil d’interop a changé depuis son chargement."));
        if (!string.Equals(draft.ProfileSourceFingerprint, InteropFingerprintUx6P(history.Profile),
                StringComparison.Ordinal))
            diagnostics.Add(new("script.interop.source_changed", "script/interop/source", "error",
                "Le profil d’interop source a changé."));
        if (draft.ScriptSourceId != source.Id ||
            !string.Equals(draft.ScriptRelativePath, source.RelativePath, StringComparison.Ordinal) ||
            !string.Equals(draft.ScriptSourceFingerprint, SourceFingerprintUx6P(source), StringComparison.Ordinal) ||
            !ScriptDiskMatchesUx6P(source))
            diagnostics.Add(new("script.interop.script_changed", "script/source", "error",
                "La source ForgeScript sélectionnée a changé ; rechargez-la."));
        if (string.IsNullOrWhiteSpace(draft.TargetLabel) || string.IsNullOrWhiteSpace(draft.EntityId) ||
            string.IsNullOrWhiteSpace(draft.ComponentId) || string.IsNullOrWhiteSpace(draft.EventName) ||
            string.IsNullOrWhiteSpace(draft.Handler) || string.IsNullOrWhiteSpace(draft.PayloadType) ||
            draft.Priority is < -1000 or > 1000 || !Enum.IsDefined(draft.Authority) ||
            Guid.TryParse(draft.EntityId, out _))
            diagnostics.Add(new("script.interop.binding_invalid", "script/eventBindings", "error",
                "La cible, le composant, l’événement, le handler, le payload et la priorité sont requis."));
        try
        {
            diagnostics.AddRange(ForgeScriptInteropProfileValidator.Validate(ToInteropProfileUx6P(draft))
                .Select(ToStudioInteropDiagnosticUx6P));
        }
        catch (Exception exception) when (InteropExceptionUx6P(exception))
        {
            diagnostics.Add(new("script.interop.draft_invalid", "script/interop", "error", exception.Message));
        }
        return diagnostics;
    }

    private StudioUx6PActionResult MutateInteropHistoryUx6P(bool undo)
    {
        CancelUx6PApproval();
        if (!EnsureInteropUx6P(out var failure)) return FailedInteropActionUx6P(failure);
        var history = _interopHistoryUx6P!;
        if (undo && !history.CanUndo || !undo && !history.CanRedo)
            return FailedInteropActionUx6P(undo ? "Aucune modification à annuler." :
                "Aucune modification à rétablir.");
        var before = history.Profile;
        var mutation = undo ? history.Undo() : history.Redo();
        if (!mutation.Succeeded) return FailedInteropActionUx6P("La mutation d’historique R101 a échoué.");
        try
        {
            new ForgeScriptInteropProfileStore(Session!.RootDirectory).Save(mutation.Profile);
            _persistedInteropUx6P = mutation.Profile;
            return new(true,
                undo ? "Profil d’interop annulé et enregistré." : "Profil d’interop rétabli et enregistré.",
                history.CanUndo, history.CanRedo);
        }
        catch (Exception exception) when (InteropExceptionUx6P(exception))
        {
            if (undo) history.Redo(); else history.Undo();
            _persistedInteropUx6P = before;
            return FailedInteropActionUx6P($"Persistance de l’historique refusée : {exception.Message}");
        }
    }

    private bool ScriptDiskMatchesUx6P(StudioUx6FDraft source)
    {
        if (Session is null || source.NewSource) return false;
        try
        {
            var document = new ForgeScriptSourceStore(Session.RootDirectory).Load(source.RelativePath);
            return string.Equals(SourceFingerprintUx6P(source), SourceFingerprintUx6P(document),
                StringComparison.Ordinal);
        }
        catch (Exception exception) when (InteropExceptionUx6P(exception))
        {
            return false;
        }
    }

    private static StudioUx6PInteropDraft ToInteropDraftUx6P(
        ForgeScriptInteropProfile profile,
        long revision,
        StudioUx6FDraft source) => new(
        profile.Id, profile.SchemaVersion, profile.Provenance, profile.ApiVersion, profile.EventBinding,
        profile.RequiredAtRuntime, profile.MaximumSources, profile.MaximumSourceBytes,
        profile.TestInstructionLimit, profile.TestTimeoutMilliseconds, profile.TestRecursionLimit,
        profile.TestSpawnLimit, "Gardien de la crypte", "warden", "creature.combat", "creature.damaged",
        "OnDamagedHigh", "int", 90, "is_enraged", ForgeScriptEventAuthority.Server,
        revision, InteropFingerprintUx6P(profile), source.Id, source.RelativePath, SourceFingerprintUx6P(source));

    private static ForgeScriptInteropProfile ToInteropProfileUx6P(StudioUx6PInteropDraft draft) => new(
        draft.Id, draft.SchemaVersion, draft.Provenance.Trim(), draft.ApiVersion.Trim(),
        draft.EventBinding.Trim(), draft.RequiredAtRuntime, draft.MaximumSources, draft.MaximumSourceBytes,
        draft.TestInstructionLimit, draft.TestTimeoutMilliseconds, draft.TestRecursionLimit,
        draft.TestSpawnLimit);

    private static ScriptSourceTransaction SourceTransactionUx6P(
        StudioUx6PInteropDraft draft,
        StudioUx6FDraft source,
        long revision) => new(Guid.NewGuid(), revision, source.RelativePath, source.Source,
        string.IsNullOrWhiteSpace(draft.Provenance) ? "studio:ux6-p:r101" : draft.Provenance.Trim(),
        source.Id, source.RequiredAtRuntime);

    private static ScriptEventBindingPlanRequest BindingRequestUx6P(
        StudioUx6PInteropDraft draft,
        long revision) => new(Guid.NewGuid(), revision, draft.EntityId.Trim(), draft.ComponentId.Trim(),
        draft.EventName.Trim(), draft.ScriptRelativePath, draft.Handler.Trim(), draft.PayloadType.Trim(),
        draft.Priority, string.IsNullOrWhiteSpace(draft.EnableCondition) ? null : draft.EnableCondition.Trim(),
        draft.Authority);

    private static StudioUx6FDiagnostic ToStudioInteropDiagnosticUx6P(ForgeScriptInteropDiagnostic item) =>
        new(item.Code, item.Path, item.Severity, item.Message);

    private static StudioUx6FDiagnostic ToStudioInteropDiagnosticUx6P(AgentDiagnostic item) =>
        new(item.Code, item.Path ?? "script/interop", item.Severity ?? "error", item.Message);

    private static StudioUx6PPreview FailedInteropPreviewUx6P(string message) =>
        new(false, false, true, false, false, message, "no-change",
            [new("script.interop.unavailable", "script/interop", "error", message)]);

    private static StudioUx6PSimulation FailedInteropSimulationUx6P(
        string message,
        IReadOnlyList<StudioUx6FDiagnostic>? diagnostics = null) =>
        new(false, message, false, false, false, false, false, false, false, false, false, [],
            diagnostics ?? [new("script.interop.simulation_unavailable", "script/interop/simulation", "error",
                message)]);

    private StudioUx6PActionResult FailedInteropActionUx6P(string message) =>
        new(false, message, _interopHistoryUx6P?.CanUndo == true, _interopHistoryUx6P?.CanRedo == true);

    private static StudioUx6PSmokeResult FailedInteropSmokeUx6P() =>
        new(Succeeded: false, ExplicitSelection: false, CanonicalProfile: false, Api: false,
            SourceTransactions: false, Compilation: false, SandboxTests: false,
            BindingTransactions: false, SnapshotRoundTrip: false, RuntimeAdapter: false,
            NativeSandbox: false, QuotaRecovery: false, InvalidProfilesBlocked: false,
            InvalidSourceBlocked: false, InvalidBindingBlocked: false, CanonicalDryRun: false,
            StaleSourceBlocked: false, StaleApprovalBlocked: false, ApplyUndoRedo: false,
            Persistence: false, SourcePreserved: false, ProjectNeighborsPreserved: false,
            TemporaryWorkspaceReleased: false);

    private static string InteropPathUx6P(string root) =>
        Path.Combine(root, ForgeScriptInteropProfileStore.RelativePath);

    private static void RestoreInteropFileUx6P(string path, bool existed, byte[]? bytes)
    {
        try
        {
            if (existed && bytes is not null) File.WriteAllBytes(path, bytes);
            else if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception)
        {
            // Best effort rollback; the failed action remains visible to the author.
        }
    }

    private static string InteropFingerprintUx6P(ForgeScriptInteropProfile profile)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(profile.Id.ToByteArray());
        writer.Write(profile.SchemaVersion);
        WriteStringUx6P(writer, profile.Provenance);
        WriteStringUx6P(writer, profile.ApiVersion);
        WriteStringUx6P(writer, profile.EventBinding);
        writer.Write(profile.RequiredAtRuntime);
        writer.Write(profile.MaximumSources);
        writer.Write(profile.MaximumSourceBytes);
        writer.Write(profile.TestInstructionLimit);
        writer.Write(profile.TestTimeoutMilliseconds);
        writer.Write(profile.TestRecursionLimit);
        writer.Write(profile.TestSpawnLimit);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static string SourceFingerprintUx6P(StudioUx6FDraft source)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(source.Id.ToByteArray());
        writer.Write(source.SchemaVersion);
        WriteStringUx6P(writer, source.Provenance);
        WriteStringUx6P(writer, source.RelativePath);
        WriteStringUx6P(writer, source.Source);
        WriteStringUx6P(writer, source.ApiVersion);
        writer.Write((int)source.Distribution);
        writer.Write(source.RequiredAtRuntime);
        WriteStringsUx6P(writer, source.Dependencies);
        writer.Write(source.Bindings.Count);
        foreach (var binding in source.Bindings)
        {
            writer.Write(binding.Id.ToByteArray());
            WriteStringUx6P(writer, binding.ComponentId);
            WriteStringUx6P(writer, binding.EventName);
            WriteStringUx6P(writer, binding.Handler);
            WriteStringUx6P(writer, binding.PayloadType);
            writer.Write(binding.Priority);
            WriteStringUx6P(writer, binding.EnableCondition ?? string.Empty);
            writer.Write((int)binding.Authority);
            writer.Write(binding.Enabled);
        }
        WriteStringsUx6P(writer, source.RequiredCapabilities is null
            ? []
            : source.RequiredCapabilities.Order(StringComparer.Ordinal));
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static string SourceFingerprintUx6P(ForgeScriptSourceDocument source)
    {
        var draft = new StudioUx6FDraft(source.Id, source.SchemaVersion, source.Provenance,
            source.RelativePath, source.Source, source.ApiVersion, source.Distribution, source.RequiredAtRuntime,
            source.Dependencies.ToArray(), source.EventBindings.Select(item => new StudioUx6FBindingDraft(
                item.Id, item.ComponentId, item.EventName, item.Handler, item.PayloadType, item.Priority,
                item.EnableCondition, item.Authority, item.Enabled)).ToArray(), source.RequiredCapabilities,
            0, string.Empty, false);
        return SourceFingerprintUx6P(draft);
    }

    private static void WriteStringUx6P(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static void WriteStringsUx6P(BinaryWriter writer, IEnumerable<string> values)
    {
        var materialized = values.ToArray();
        writer.Write(materialized.Length);
        foreach (var value in materialized) WriteStringUx6P(writer, value);
    }

    private static bool InteropExceptionUx6P(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or InvalidOperationException or
            ArgumentException or JsonException or DllNotFoundException or BadImageFormatException;
}
