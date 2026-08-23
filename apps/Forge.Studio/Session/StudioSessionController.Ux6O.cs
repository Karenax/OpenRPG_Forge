using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Scripting;
using Forge.Scripting;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private ForgeScriptQualityHistory? _qualityHistoryUx6O;
    private ForgeScriptQualityProfile? _persistedQualityUx6O;
    private StudioUx6OQualityDraft? _approvedQualityUx6O;
    private long? _approvedQualityRevisionUx6O;
    private string? _approvedQualityFingerprintUx6O;
    private string? _approvedQualityScriptFingerprintUx6O;

    internal StudioUx6OQualityDraft? CurrentScriptQualityUx6O(StudioUx6FDraft source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!EnsureQualityUx6O(out _)) return null;
        return ToQualityDraftUx6O(_qualityHistoryUx6O!.Profile, _qualityHistoryUx6O.Revision, source);
    }

    internal bool ScriptQualityHasUnsavedChangesUx6O =>
        _qualityHistoryUx6O is not null &&
        (_persistedQualityUx6O is null ||
         !string.Equals(QualityFingerprintUx6O(_qualityHistoryUx6O.Profile),
             QualityFingerprintUx6O(_persistedQualityUx6O), StringComparison.Ordinal));

    internal StudioUx6OPreview PreviewScriptQualityUx6O(
        StudioUx6OQualityDraft draft,
        StudioUx6FDraft source)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(source);
        CancelUx6OApproval();
        if (!EnsureQualityUx6O(out var failure)) return FailedQualityPreviewUx6O(failure);
        var history = _qualityHistoryUx6O!;
        var diagnostics = ValidateQualityDraftUx6O(draft, source, history).ToArray();
        if (diagnostics.Length > 0)
            return new(false, false, true, "Aperçu refusé · corrigez les champs localisés.",
                "no-change", diagnostics);
        var candidate = ToQualityProfileUx6O(draft);
        var command = new ForgeScriptQualityCommand(Guid.NewGuid(), history.Revision, candidate,
            "UX6-O R100 quality preview");
        var result = history.Preview(command);
        var mapped = result.Diagnostics.Select(ToStudioQualityDiagnosticUx6O).ToArray();
        if (!result.Succeeded)
            return new(false, false, true, "Aperçu refusé · le profil R100 est invalide.",
                result.SemanticDiff, mapped);
        var changed = !string.Equals(QualityFingerprintUx6O(history.Profile),
            QualityFingerprintUx6O(candidate), StringComparison.Ordinal);
        if (changed)
        {
            _approvedQualityUx6O = draft;
            _approvedQualityRevisionUx6O = history.Revision;
            _approvedQualityFingerprintUx6O = QualityFingerprintUx6O(candidate);
            _approvedQualityScriptFingerprintUx6O = SourceFingerprintUx6O(source);
        }
        return new(true, changed, true,
            changed
                ? "Aperçu sec valide · le profil qualité peut être appliqué."
                : "Aperçu sec valide · aucune valeur n’a changé.",
            result.SemanticDiff, mapped);
    }

    internal StudioUx6OSimulation SimulateScriptQualityUx6O(
        StudioUx6OQualityDraft draft,
        StudioUx6FDraft source)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(source);
        if (!EnsureQualityUx6O(out var failure)) return FailedQualitySimulationUx6O(failure);
        var diagnostics = ValidateQualityDraftUx6O(draft, source, _qualityHistoryUx6O!).ToArray();
        if (diagnostics.Length > 0)
            return FailedQualitySimulationUx6O("Simulation refusée · le profil ou la source a changé.", diagnostics);

        try
        {
            var profile = ToQualityProfileUx6O(draft);
            var document = new ForgeScriptSourceStore(Session!.RootDirectory).Load(source.RelativePath);
            var adapter = new ForgeScriptQualityRuntimeAdapter();
            var resolution = adapter.Resolve(profile, document, profile.RequiredAtRuntime);
            if (!resolution.Available || resolution.Handle is null)
                return FailedQualitySimulationUx6O("L’adaptateur R100 n’a pas résolu la source sélectionnée.");
            var state = adapter.Build(resolution.Handle, profile, document);

            var fixture = new ScriptFixture("monde-test", profile.UnitTests.DeterministicSeed,
                new Dictionary<string, object?> { ["points"] = 4, ["ready"] = true },
                new Dictionary<string, object?> { ["clock"] = "horloge simulée", ["quest"] = "quête simulée" });
            var test = new ScriptTestCase("qualité-source", fixture,
                world => world.Events.Add("fixture préparée"),
                world => world.State["points"] = (int)world.State["points"]! + world.Random.Next(1, 2),
                [new("points", world => (int)world.State["points"]! == 5, "le score attendu vaut 5")]);
            var firstTests = adapter.RunTests(resolution.Handle, state, [test]);
            var secondTests = adapter.RunTests(resolution.Handle, state, [test]);
            var unitTests = firstTests.Count == 1 && firstTests[0].Passed && secondTests[0].Passed &&
                            firstTests[0].Seed == secondTests[0].Seed &&
                            firstTests[0].Events.SequenceEqual(secondTests[0].Events);
            var ci = ForgeScriptQualityService.ToCiReport(firstTests);
            var deterministicReport = ci.Contains("\"passed\":1", StringComparison.Ordinal) &&
                                      ci.Contains(profile.UnitTests.DeterministicSeed.ToString(),
                                          StringComparison.Ordinal);

            var profileReport = adapter.Profile(resolution.Handle, state,
            [
                new(source.RelativePath, "main", profile.Profiler.InstructionThreshold + 10,
                    profile.Profiler.AllocationThresholdBytes + 20,
                    TimeSpan.FromMilliseconds(profile.Profiler.CpuThresholdMilliseconds + 1),
                    ["event", "main"]),
                new(source.RelativePath, "helper", 4, 8, TimeSpan.FromMilliseconds(1), ["main", "helper"]),
            ]);
            var profiler = profileReport.Hotspots.Count == 2 && profileReport.Alerts.Count >= 3 &&
                           profileReport.CallGraph.Count == 2;

            var generated = adapter.GenerateDocumentation(resolution.Handle, state,
                ForgeScriptApiCatalog.Registrations);
            var documentation = generated.Markdown.Contains("Do not edit manually", StringComparison.Ordinal) &&
                                generated.McpResources.Count > 0;

            var analysisSource = new ScriptSource(source.RelativePath,
                source.Source + System.Environment.NewLine +
                "OnHeartbeat FindAll();\nUnsafeQuery();\nDeprecatedApi();\nServerOnly();\ntarget.Use();",
                "client", new HashSet<string>(StringComparer.Ordinal) { "target" });
            var staticDiagnostics = adapter.Analyze(resolution.Handle, state, analysisSource);
            var expectedLints = new HashSet<string>(StringComparer.Ordinal)
            {
                "SCR-LINT-EXPENSIVE-HEARTBEAT", "SCR-LINT-UNSAFE-QUERY", "SCR-LINT-NULL",
                "SCR-LINT-DEPRECATED", "SCR-LINT-AUTHORITY",
            };
            var staticAnalysis = expectedLints.All(code => staticDiagnostics.Any(item => item.Code == code));

            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            var ordinal = 0;
            foreach (var component in profile.State.RegisteredComponents.OrderBy(item => item.Key,
                         StringComparer.Ordinal))
            {
                var captured = component;
                state.StateService.RegisterPersistent(new PersistentComponent(captured.Key, captured.Value,
                    typeof(int), (_, json) => json.GetInt32())
                { Validate = value => value is int number && number >= 0 });
                values[captured.Key] = ++ordinal;
            }
            values["vm-pointer"] = "0xBAD";
            var stateJson = state.StateService.SerializeState(values);
            var restored = state.StateService.DeserializeState(stateJson);
            var stateRoundTrip = restored.Count == profile.State.RegisteredComponents.Count &&
                                 !stateJson.Contains("vm-pointer", StringComparison.Ordinal) &&
                                 profile.State.RegisteredComponents.Keys.All(restored.ContainsKey);

            var execution = adapter.Execute(resolution.Handle, state, new NativeForgeScriptRuntime(),
                new ForgeScriptQuotas(100_000, 1000, 128, 16));
            var nativeSandbox = execution.Status == ForgeScriptExecutionStatus.Completed;
            var adapterReady = resolution.Available && state.Profile.Id == profile.Id &&
                               state.Document.Id == source.Id;
            var succeeded = unitTests && deterministicReport && profiler && documentation &&
                            staticAnalysis && stateRoundTrip && adapterReady && nativeSandbox;
            var results = new[]
            {
                $"Tests · {(unitTests ? "réussis et déterministes" : "échec")}",
                $"Rapport CI · {(deterministicReport ? "généré" : "incomplet")}",
                $"Profilage · {profileReport.Hotspots.Count} handler(s), {profileReport.Alerts.Count} alerte(s)",
                $"Documentation · {generated.McpResources.Count} ressource(s) MCP",
                $"Analyse statique · {staticDiagnostics.Count} diagnostic(s) localisé(s)",
                $"État · {restored.Count} composant(s), pointeur VM exclu",
                $"Sandbox native · {execution.Status}",
            };
            return new(succeeded,
                succeeded
                    ? "Simulation R100 complète · cinq outils exercés sur une copie contrôlée."
                    : "La simulation R100 n’a pas satisfait tous les invariants.",
                unitTests, deterministicReport, profiler, documentation, staticAnalysis,
                stateRoundTrip, adapterReady, nativeSandbox, results,
                staticDiagnostics.Select(ToStudioStaticDiagnosticUx6O).ToArray());
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or
                                           IOException or UnauthorizedAccessException or JsonException or
                                           DllNotFoundException or BadImageFormatException)
        {
            return FailedQualitySimulationUx6O($"Simulation R100 refusée : {exception.Message}",
                [new("script.quality.simulation_failed", "script/quality/simulation", "error",
                    exception.Message)]);
        }
    }

    internal StudioUx6OActionResult ApplyApprovedScriptQualityUx6O(
        StudioUx6OQualityDraft draft,
        StudioUx6FDraft source)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(source);
        if (!EnsureQualityUx6O(out var failure)) return FailedQualityActionUx6O(failure);
        var history = _qualityHistoryUx6O!;
        var candidate = ToQualityProfileUx6O(draft);
        var candidateFingerprint = QualityFingerprintUx6O(candidate);
        var sourceFingerprint = SourceFingerprintUx6O(source);
        if (_approvedQualityUx6O is null || _approvedQualityRevisionUx6O != history.Revision ||
            !string.Equals(_approvedQualityFingerprintUx6O, candidateFingerprint, StringComparison.Ordinal) ||
            !string.Equals(_approvedQualityScriptFingerprintUx6O, sourceFingerprint, StringComparison.Ordinal) ||
            !string.Equals(QualityFingerprintUx6O(ToQualityProfileUx6O(_approvedQualityUx6O)),
                candidateFingerprint, StringComparison.Ordinal) ||
            ValidateQualityDraftUx6O(draft, source, history).Count > 0)
            return FailedQualityActionUx6O("Application refusée · relancez l’aperçu sec du brouillon courant.");

        var command = new ForgeScriptQualityCommand(Guid.NewGuid(), history.Revision, candidate,
            "UX6-O R100 quality apply");
        var preview = history.Preview(command);
        if (!preview.Succeeded)
            return FailedQualityActionUx6O(string.Join(" · ", preview.Diagnostics.Select(item => item.Code)));
        var path = QualityPathUx6O(Session!.RootDirectory);
        var existed = File.Exists(path);
        var bytes = existed ? File.ReadAllBytes(path) : null;
        try
        {
            new ForgeScriptQualityStore(Session.RootDirectory).Save(candidate);
            var applied = history.Apply(command);
            if (!applied.Succeeded)
            {
                RestoreQualityFileUx6O(path, existed, bytes);
                return FailedQualityActionUx6O("La commande R100 a été refusée après prévisualisation.");
            }
            _persistedQualityUx6O = candidate;
            CancelUx6OApproval();
            return new(true, "Profil qualité R100 appliqué atomiquement.", history.CanUndo, history.CanRedo);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException)
        {
            RestoreQualityFileUx6O(path, existed, bytes);
            return FailedQualityActionUx6O($"Écriture refusée : {exception.Message}");
        }
    }

    internal StudioUx6OActionResult UndoScriptQualityUx6O() => MutateQualityHistoryUx6O(undo: true);
    internal StudioUx6OActionResult RedoScriptQualityUx6O() => MutateQualityHistoryUx6O(undo: false);

    internal StudioUx6OActionResult SaveScriptQualityUx6O()
    {
        if (!EnsureQualityUx6O(out var failure)) return FailedQualityActionUx6O(failure);
        try
        {
            var profile = _qualityHistoryUx6O!.Profile;
            new ForgeScriptQualityStore(Session!.RootDirectory).Save(profile);
            _persistedQualityUx6O = profile;
            return new(true, "Profil qualité R100 enregistré.", _qualityHistoryUx6O.CanUndo,
                _qualityHistoryUx6O.CanRedo);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException)
        {
            return FailedQualityActionUx6O($"Enregistrement refusé : {exception.Message}");
        }
    }

    internal StudioUx6OApprovalSnapshot CaptureScriptQualityApprovalUx6O() => new(
        _approvedQualityUx6O, _approvedQualityRevisionUx6O, _approvedQualityFingerprintUx6O,
        _approvedQualityScriptFingerprintUx6O);

    internal void RestoreScriptQualityApprovalUx6O(StudioUx6OApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedQualityUx6O = snapshot.Draft;
        _approvedQualityRevisionUx6O = snapshot.Revision;
        _approvedQualityFingerprintUx6O = snapshot.CandidateFingerprint;
        _approvedQualityScriptFingerprintUx6O = snapshot.ScriptFingerprint;
    }

    internal void CancelUx6OApproval()
    {
        _approvedQualityUx6O = null;
        _approvedQualityRevisionUx6O = null;
        _approvedQualityFingerprintUx6O = null;
        _approvedQualityScriptFingerprintUx6O = null;
    }

    internal void CloseUx6O()
    {
        _qualityHistoryUx6O = null;
        _persistedQualityUx6O = null;
        CancelUx6OApproval();
    }

    internal StudioUx6OSmokeResult RunUx6OContractSmoke()
    {
        var sourceProject = Project;
        if (Session is null || sourceProject is null || History is null) return FailedQualitySmokeUx6O();
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux6-o", Guid.NewGuid().ToString("N"));
        var released = false;
        StudioUx6OSmokeResult result;
        try
        {
            if (!ProjectSession.Create(root, sourceProject).Succeeded) return FailedQualitySmokeUx6O();
            var document = EvidenceDocumentUx6F();
            new ForgeScriptSourceStore(root).Save(document);
            var editing = new StudioSessionController();
            if (!editing.Open(root).Succeeded ||
                !editing.TryOpenCanonicalSourceUx6F(document.RelativePath, out var source, out _) || source is null)
                return FailedQualitySmokeUx6O();
            var initial = editing.CurrentScriptQualityUx6O(source);
            if (initial is null) return FailedQualitySmokeUx6O();
            var candidate = initial with
            {
                Provenance = "studio:ux6-o:r100",
                MaximumCases = initial.MaximumCases + 1,
                InstructionThreshold = initial.InstructionThreshold + 100,
                DocumentationAvailability = new HashSet<string>(StringComparer.Ordinal)
                {
                    "server", "client", "rpg-kit",
                },
                TreatWarningsAsErrors = true,
                MaximumStateBytes = initial.MaximumStateBytes + 1024,
            };
            var invalidTests = !editing.PreviewScriptQualityUx6O(candidate with { DeterministicSeed = 0 }, source)
                .Succeeded;
            var invalidProfiler = !editing.PreviewScriptQualityUx6O(candidate with
            { InstructionThreshold = 0 }, source).Succeeded;
            var invalidDocs = !editing.PreviewScriptQualityUx6O(candidate with
            { DocumentationPath = "../quality.md" }, source).Succeeded;
            var invalidStatic = !editing.PreviewScriptQualityUx6O(candidate with
            { EnabledRules = new HashSet<string> { "unknown" } }, source).Succeeded;
            var invalidState = !editing.PreviewScriptQualityUx6O(candidate with
            { MaximumStateBytes = 1 }, source).Succeeded;
            var invalidProfiles = invalidTests && invalidProfiler && invalidDocs && invalidStatic && invalidState;
            var staleSource = !editing.PreviewScriptQualityUx6O(candidate with
            { ScriptSourceFingerprint = "stale" }, source).Succeeded;
            var preview = editing.PreviewScriptQualityUx6O(candidate, source);
            var simulation = editing.SimulateScriptQualityUx6O(candidate, source);
            var staleApproval = !editing.ApplyApprovedScriptQualityUx6O(candidate with
            { MaximumCases = candidate.MaximumCases + 1 }, source).Succeeded;
            preview = editing.PreviewScriptQualityUx6O(candidate, source);
            var apply = editing.ApplyApprovedScriptQualityUx6O(candidate, source);
            var undo = editing.UndoScriptQualityUx6O();
            var redo = editing.RedoScriptQualityUx6O();
            var save = editing.SaveScriptQualityUx6O();
            var persisted = new ForgeScriptQualityStore(root).Load();
            var reopened = new StudioSessionController();
            var reopenedOkay = reopened.Open(root).Succeeded &&
                               reopened.TryOpenCanonicalSourceUx6F(document.RelativePath, out var reopenedSource,
                                   out _) && reopenedSource is not null &&
                               reopened.CurrentScriptQualityUx6O(reopenedSource) is { } reopenedDraft &&
                               string.Equals(QualityFingerprintUx6O(ToQualityProfileUx6O(reopenedDraft)),
                                   QualityFingerprintUx6O(ToQualityProfileUx6O(candidate)), StringComparison.Ordinal);
            reopened.Close();
            var sourcePath = Path.Combine(root, document.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            var sourcePreserved = File.ReadAllText(sourcePath) == document.Source &&
                                  string.Equals(SourceFingerprintUx6O(
                                          new ForgeScriptSourceStore(root).Load(document.RelativePath)),
                                      SourceFingerprintUx6O(document), StringComparison.Ordinal);
            var projectNeighbors = editing.Project is not null &&
                                   ProjectJsonSerializer.Serialize(editing.Project) ==
                                   ProjectJsonSerializer.Serialize(sourceProject);
            var applyUndoRedo = apply.Succeeded && undo.Succeeded && redo.Succeeded;
            var persistence = save.Succeeded && reopenedOkay &&
                              string.Equals(QualityFingerprintUx6O(persisted),
                                  QualityFingerprintUx6O(ToQualityProfileUx6O(candidate)), StringComparison.Ordinal);
            var succeeded = invalidProfiles && staleSource && preview.Succeeded && preview.CanonicalDryRun &&
                            simulation.Succeeded && simulation.UnitTests && simulation.Profiler &&
                            simulation.Documentation && simulation.StaticAnalysis && simulation.StateRoundTrip &&
                            simulation.Adapter && simulation.NativeSandbox && staleApproval && applyUndoRedo &&
                            persistence && sourcePreserved && projectNeighbors;
            result = new(succeeded, true, true, simulation.UnitTests, simulation.Profiler,
                simulation.Documentation, simulation.StaticAnalysis, simulation.StateRoundTrip,
                simulation.Adapter, simulation.NativeSandbox, invalidProfiles, preview.CanonicalDryRun,
                staleSource, staleApproval, applyUndoRedo, persistence, sourcePreserved, projectNeighbors, false);
            editing.Close();
        }
        catch (Exception)
        {
            result = FailedQualitySmokeUx6O();
        }
        finally
        {
            released = DeleteUx6FTemporaryWorkspace(root);
        }
        return result with { Succeeded = result.Succeeded && released, TemporaryWorkspaceReleased = released };
    }

    private bool EnsureQualityUx6O(out string failure)
    {
        failure = string.Empty;
        if (_qualityHistoryUx6O is not null) return true;
        if (Session is null)
        {
            failure = "Ouvrez un module avant de configurer la qualité ForgeScript.";
            return false;
        }
        try
        {
            var path = QualityPathUx6O(Session.RootDirectory);
            var existed = File.Exists(path);
            var profile = existed ? new ForgeScriptQualityStore(Session.RootDirectory).Load() :
                ForgeScriptQualityProfile.Default;
            _qualityHistoryUx6O = new ForgeScriptQualityHistory(profile);
            _persistedQualityUx6O = existed ? profile : null;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or JsonException)
        {
            failure = $"Le profil qualité R100 est invalide : {exception.Message}";
            return false;
        }
    }

    private IReadOnlyList<StudioUx6FDiagnostic> ValidateQualityDraftUx6O(
        StudioUx6OQualityDraft draft,
        StudioUx6FDraft source,
        ForgeScriptQualityHistory history)
    {
        var diagnostics = new List<StudioUx6FDiagnostic>();
        if (draft.BaseRevision != history.Revision)
            diagnostics.Add(new("script.quality.revision_conflict", "script/quality/revision", "error",
                "Le profil qualité a changé depuis son chargement."));
        if (!string.Equals(draft.ProfileSourceFingerprint, QualityFingerprintUx6O(history.Profile),
                StringComparison.Ordinal))
            diagnostics.Add(new("script.quality.source_changed", "script/quality/source", "error",
                "Le profil qualité source a changé."));
        var sourceFingerprint = SourceFingerprintUx6O(source);
        if (draft.ScriptSourceId != source.Id ||
            !string.Equals(draft.ScriptRelativePath, source.RelativePath, StringComparison.Ordinal) ||
            !string.Equals(draft.ScriptSourceFingerprint, sourceFingerprint, StringComparison.Ordinal) ||
            !ScriptDiskMatchesUx6O(source))
            diagnostics.Add(new("script.quality.script_changed", "script/source", "error",
                "La source ForgeScript sélectionnée a changé ; rechargez-la."));
        try
        {
            diagnostics.AddRange(ForgeScriptQualityProfileValidator.Validate(ToQualityProfileUx6O(draft))
                .Select(ToStudioQualityDiagnosticUx6O));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            diagnostics.Add(new("script.quality.draft_invalid", "script/quality", "error", exception.Message));
        }
        return diagnostics;
    }

    private StudioUx6OActionResult MutateQualityHistoryUx6O(bool undo)
    {
        CancelUx6OApproval();
        if (!EnsureQualityUx6O(out var failure)) return FailedQualityActionUx6O(failure);
        var history = _qualityHistoryUx6O!;
        if (undo && !history.CanUndo || !undo && !history.CanRedo)
            return FailedQualityActionUx6O(undo ? "Aucune modification à annuler." : "Aucune modification à rétablir.");
        var before = history.Profile;
        var mutation = undo ? history.Undo() : history.Redo();
        if (!mutation.Succeeded) return FailedQualityActionUx6O("La mutation d’historique R100 a échoué.");
        try
        {
            new ForgeScriptQualityStore(Session!.RootDirectory).Save(mutation.Profile);
            _persistedQualityUx6O = mutation.Profile;
            return new(true, undo ? "Profil qualité annulé et enregistré." : "Profil qualité rétabli et enregistré.",
                history.CanUndo, history.CanRedo);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException)
        {
            if (undo) history.Redo(); else history.Undo();
            _persistedQualityUx6O = before;
            return FailedQualityActionUx6O($"Persistance de l’historique refusée : {exception.Message}");
        }
    }

    private bool ScriptDiskMatchesUx6O(StudioUx6FDraft source)
    {
        if (Session is null || source.NewSource) return false;
        try
        {
            var document = new ForgeScriptSourceStore(Session.RootDirectory).Load(source.RelativePath);
            return string.Equals(SourceFingerprintUx6O(source), SourceFingerprintUx6O(document),
                StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or JsonException or ArgumentException)
        {
            return false;
        }
    }

    private static StudioUx6OQualityDraft ToQualityDraftUx6O(
        ForgeScriptQualityProfile profile,
        long revision,
        StudioUx6FDraft source) => new(
        profile.Id, profile.SchemaVersion, profile.Provenance, profile.ApiVersion, profile.EventBinding,
        profile.RequiredAtRuntime, profile.UnitTests.Id, profile.UnitTests.DeterministicSeed,
        profile.UnitTests.MaximumCases, profile.Profiler.Id,
        profile.Profiler.InstructionThreshold, profile.Profiler.AllocationThresholdBytes,
        profile.Profiler.CpuThresholdMilliseconds, profile.Documentation.Id,
        profile.Documentation.OutputRelativePath,
        profile.Documentation.Availability.ToHashSet(StringComparer.Ordinal),
        profile.StaticAnalysis.Id,
        profile.StaticAnalysis.EnabledRules.ToHashSet(StringComparer.Ordinal),
        profile.StaticAnalysis.TreatWarningsAsErrors, profile.State.Id, profile.State.EnvelopeSchemaVersion,
        profile.State.MaximumBytes, profile.State.RegisteredComponents.ToDictionary(StringComparer.Ordinal),
        revision, QualityFingerprintUx6O(profile), source.Id, source.RelativePath, SourceFingerprintUx6O(source));

    private static ForgeScriptQualityProfile ToQualityProfileUx6O(StudioUx6OQualityDraft draft) => new(
        draft.Id, draft.SchemaVersion, draft.Provenance.Trim(), draft.ApiVersion.Trim(),
        draft.EventBinding.Trim(), draft.RequiredAtRuntime,
        new ForgeScriptUnitTestSettings(draft.UnitTestsId,
            draft.DeterministicSeed, draft.MaximumCases),
        new ForgeScriptProfilerSettings(draft.ProfilerId,
            draft.InstructionThreshold, draft.AllocationThresholdBytes, draft.CpuThresholdMilliseconds),
        new ForgeScriptDocumentationSettings(draft.DocumentationId,
            draft.DocumentationPath.Trim(), draft.DocumentationAvailability.Order(StringComparer.Ordinal).ToArray()),
        new ForgeScriptStaticAnalysisSettings(draft.StaticAnalysisId,
            draft.EnabledRules.Order(StringComparer.Ordinal).ToArray(), draft.TreatWarningsAsErrors),
        new ForgeScriptStateSettings(draft.StateId, draft.EnvelopeSchemaVersion,
            draft.MaximumStateBytes, draft.RegisteredComponents.OrderBy(item => item.Key, StringComparer.Ordinal)
                .ToDictionary(item => item.Key.Trim(), item => item.Value, StringComparer.Ordinal)));

    private static StudioUx6FDiagnostic ToStudioQualityDiagnosticUx6O(ForgeScriptQualityDiagnostic item) =>
        new(item.Code, item.Path, item.Severity, item.Message);

    private static StudioUx6FDiagnostic ToStudioStaticDiagnosticUx6O(StaticDiagnostic item) =>
        new(item.Code, item.Path, item.Severity, $"{item.Message} {item.Suggestion}", item.Line, 1);

    private static StudioUx6OPreview FailedQualityPreviewUx6O(string message) =>
        new(false, false, true, message, "no-change",
            [new("script.quality.unavailable", "script/quality", "error", message)]);

    private static StudioUx6OSimulation FailedQualitySimulationUx6O(
        string message,
        IReadOnlyList<StudioUx6FDiagnostic>? diagnostics = null) =>
        new(false, message, false, false, false, false, false, false, false, false, [],
            diagnostics ?? [new("script.quality.simulation_unavailable", "script/quality/simulation", "error", message)]);

    private StudioUx6OActionResult FailedQualityActionUx6O(string message) =>
        new(false, message, _qualityHistoryUx6O?.CanUndo == true, _qualityHistoryUx6O?.CanRedo == true);

    private static StudioUx6OSmokeResult FailedQualitySmokeUx6O() =>
        new(Succeeded: false, ExplicitSelection: false, CanonicalProfile: false, UnitTests: false,
            Profiler: false, Documentation: false, StaticAnalysis: false, StateSerialization: false,
            RuntimeAdapter: false, NativeSandbox: false, InvalidProfilesBlocked: false,
            CanonicalDryRun: false, StaleSourceBlocked: false, StaleApprovalBlocked: false,
            ApplyUndoRedo: false, Persistence: false, SourcePreserved: false,
            ProjectNeighborsPreserved: false, TemporaryWorkspaceReleased: false);

    private static string QualityPathUx6O(string root) =>
        Path.Combine(root, ForgeScriptQualityStore.RelativePath);

    private static void RestoreQualityFileUx6O(string path, bool existed, byte[]? bytes)
    {
        try
        {
            if (existed && bytes is not null) File.WriteAllBytes(path, bytes);
            else if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception)
        {
            // Best effort rollback; the caller still reports the failed mutation.
        }
    }

    private static string QualityFingerprintUx6O(ForgeScriptQualityProfile profile)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(profile.Id.ToByteArray());
        writer.Write(profile.SchemaVersion);
        WriteStringUx6O(writer, profile.Provenance);
        WriteStringUx6O(writer, profile.ApiVersion);
        WriteStringUx6O(writer, profile.EventBinding);
        writer.Write(profile.RequiredAtRuntime);
        writer.Write(profile.UnitTests.Id.ToByteArray());
        writer.Write(profile.UnitTests.DeterministicSeed);
        writer.Write(profile.UnitTests.MaximumCases);
        writer.Write(profile.Profiler.Id.ToByteArray());
        writer.Write(profile.Profiler.InstructionThreshold);
        writer.Write(profile.Profiler.AllocationThresholdBytes);
        writer.Write(profile.Profiler.CpuThresholdMilliseconds);
        writer.Write(profile.Documentation.Id.ToByteArray());
        WriteStringUx6O(writer, profile.Documentation.OutputRelativePath);
        WriteStringsUx6O(writer, profile.Documentation.Availability.Order(StringComparer.Ordinal));
        writer.Write(profile.StaticAnalysis.Id.ToByteArray());
        WriteStringsUx6O(writer, profile.StaticAnalysis.EnabledRules.Order(StringComparer.Ordinal));
        writer.Write(profile.StaticAnalysis.TreatWarningsAsErrors);
        writer.Write(profile.State.Id.ToByteArray());
        writer.Write(profile.State.EnvelopeSchemaVersion);
        writer.Write(profile.State.MaximumBytes);
        var components = profile.State.RegisteredComponents.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();
        writer.Write(components.Length);
        foreach (var component in components)
        {
            WriteStringUx6O(writer, component.Key);
            writer.Write(component.Value);
        }
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static string SourceFingerprintUx6O(StudioUx6FDraft source)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(source.Id.ToByteArray());
        writer.Write(source.SchemaVersion);
        WriteStringUx6O(writer, source.Provenance);
        WriteStringUx6O(writer, source.RelativePath);
        WriteStringUx6O(writer, source.Source);
        WriteStringUx6O(writer, source.ApiVersion);
        writer.Write((int)source.Distribution);
        writer.Write(source.RequiredAtRuntime);
        WriteStringsUx6O(writer, source.Dependencies);
        writer.Write(source.Bindings.Count);
        foreach (var binding in source.Bindings)
        {
            writer.Write(binding.Id.ToByteArray());
            WriteStringUx6O(writer, binding.ComponentId);
            WriteStringUx6O(writer, binding.EventName);
            WriteStringUx6O(writer, binding.Handler);
            WriteStringUx6O(writer, binding.PayloadType);
            writer.Write(binding.Priority);
            WriteStringUx6O(writer, binding.EnableCondition ?? string.Empty);
            writer.Write((int)binding.Authority);
            writer.Write(binding.Enabled);
        }
        WriteStringsUx6O(writer, source.RequiredCapabilities is null
            ? []
            : source.RequiredCapabilities.Order(StringComparer.Ordinal).ToArray());
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static string SourceFingerprintUx6O(ForgeScriptSourceDocument source)
    {
        var draft = new StudioUx6FDraft(source.Id, source.SchemaVersion, source.Provenance,
            source.RelativePath, source.Source, source.ApiVersion, source.Distribution, source.RequiredAtRuntime,
            source.Dependencies.ToArray(), source.EventBindings.Select(item => new StudioUx6FBindingDraft(
                item.Id, item.ComponentId, item.EventName, item.Handler, item.PayloadType, item.Priority,
                item.EnableCondition, item.Authority, item.Enabled)).ToArray(), source.RequiredCapabilities,
            0, string.Empty, false);
        return SourceFingerprintUx6O(draft);
    }

    private static void WriteStringUx6O(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static void WriteStringsUx6O(BinaryWriter writer, IEnumerable<string> values)
    {
        var materialized = values.ToArray();
        writer.Write(materialized.Length);
        foreach (var value in materialized) WriteStringUx6O(writer, value);
    }
}
