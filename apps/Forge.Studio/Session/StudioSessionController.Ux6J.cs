using System.Security.Cryptography;
using System.Text;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Scripting;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux6JPolicyFile = "forge-script-policy.json";
    private ForgeScriptExecutionPolicyHistory? _executionPolicyHistoryUx6J;
    private ForgeScriptExecutionPolicy? _persistedExecutionPolicyUx6J;
    private StudioUx6JPolicyDraft? _approvedExecutionPolicyUx6J;
    private long? _approvedExecutionPolicyRevisionUx6J;
    private string? _approvedExecutionPolicyFingerprintUx6J;

    internal StudioUx6JCatalog ScriptSafetyCatalogUx6J()
    {
        var project = Project;
        if (project is null) return new([], [], []);
        var areas = project.Areas
            .OrderBy(area => area.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(area => new StudioUx6JChoice(area.Id.Value, area.DisplayName,
                $"Zone {area.Slug} · {area.Entities.Count} objet(s)"))
            .ToArray();
        var blueprintNames = project.Blueprints.ToDictionary(blueprint => blueprint.Id,
            blueprint => blueprint.DisplayName);
        var entities = project.Areas
            .SelectMany(area => area.Entities.Select(entity => new StudioUx6JChoice(
                entity.Id.Value,
                blueprintNames.GetValueOrDefault(entity.BlueprintId) ?? "Objet sans nom",
                $"{area.DisplayName} · {entity.Tags.Count} tag(s)")))
            .OrderBy(entity => entity.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        var assets = project.Blueprints
            .OrderBy(blueprint => blueprint.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(blueprint => new StudioUx6JChoice(blueprint.Id.Value, blueprint.DisplayName,
                $"Ressource {blueprint.Kind} · {blueprint.Slug}"))
            .ToArray();
        return new(areas, entities, assets);
    }

    internal StudioUx6JPolicyDraft? CurrentExecutionPolicyUx6J()
    {
        if (!EnsureExecutionPolicyUx6J(out _)) return null;
        var history = _executionPolicyHistoryUx6J!;
        return ToDraftUx6J(history.Policy, history.Revision);
    }

    internal bool ExecutionPolicyHasUnsavedChangesUx6J =>
        _executionPolicyHistoryUx6J is not null &&
        (_persistedExecutionPolicyUx6J is null ||
         !string.Equals(PolicyFingerprintUx6J(_persistedExecutionPolicyUx6J),
             PolicyFingerprintUx6J(_executionPolicyHistoryUx6J.Policy), StringComparison.Ordinal));

    internal StudioUx6JPreview PreviewExecutionPolicyUx6J(StudioUx6JPolicyDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelUx6JApproval();
        if (!EnsureExecutionPolicyUx6J(out var failure)) return FailedPreviewUx6J(failure);
        var history = _executionPolicyHistoryUx6J!;
        var stale = ValidatePolicyDraftUx6J(draft, history);
        if (stale.Count > 0) return FailedPreviewUx6J("Aperçu refusé · la politique source a changé.", stale);

        var candidate = ToPolicyUx6J(draft);
        var command = new ForgeScriptExecutionPolicyCommand(Guid.NewGuid(), history.Revision, candidate,
            "UX6-J R014 policy preview");
        var preview = history.Preview(command);
        var diagnostics = preview.Diagnostics.Select(ToStudioDiagnosticUx6J).ToArray();
        if (!preview.Succeeded)
            return new(false, false, "Aperçu refusé · corrigez les budgets localisés.", preview.SemanticDiff,
                string.Empty, [], [], [], diagnostics, true);

        var negotiation = ForgeScriptApiCompatibility.Negotiate(candidate.ApiVersion,
            new HashSet<string>(["opaque-handles", "determinism"], StringComparer.Ordinal));
        if (!negotiation.Succeeded)
        {
            var apiDiagnostics = diagnostics.Append(new StudioUx6FDiagnostic(
                negotiation.DiagnosticCode, "script/policy/apiVersion", "error",
                negotiation.MissingCapabilities.Count == 0
                    ? $"La version {candidate.ApiVersion} n’est pas compatible."
                    : $"Capacités manquantes : {string.Join(", ", negotiation.MissingCapabilities)}."))
                .ToArray();
            return new(false, false, "Négociation d’API refusée.", preview.SemanticDiff,
                negotiation.Selected, [], [], [], apiDiagnostics, true);
        }

        var capabilities = ForgeScriptCapabilityManifest.Capabilities
            .Where(item => item.Id is "opaque-handles" or "determinism" or "sandbox")
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .Select(item => item.Id)
            .ToArray();
        var deprecations = negotiation.Deprecations
            .Select(item => $"{item.Symbol} → {item.Replacement} avant {item.Removal}")
            .ToArray();
        var migrations = ForgeScriptApiCompatibility.Migrations
            .Where(item => ForgeScriptApiCompatibility.IsSupported(item.To))
            .Select(item => $"{item.From} → {item.To} · {string.Join(" ", item.Steps)}")
            .ToArray();
        var changed = !string.Equals(PolicyFingerprintUx6J(history.Policy),
            PolicyFingerprintUx6J(candidate), StringComparison.Ordinal);
        if (changed)
        {
            _approvedExecutionPolicyUx6J = draft;
            _approvedExecutionPolicyRevisionUx6J = history.Revision;
            _approvedExecutionPolicyFingerprintUx6J = PolicyFingerprintUx6J(candidate);
        }
        return new(true, changed,
            changed
                ? "Aperçu sec valide · la politique peut être appliquée."
                : "Aperçu sec valide · aucune valeur n’a changé.",
            preview.SemanticDiff, negotiation.Selected, capabilities, deprecations, migrations,
            diagnostics, true);
    }

    internal StudioUx6JSimulation SimulateExecutionPolicyUx6J(
        StudioUx6JPolicyDraft draft,
        StudioUx6JHandleDraft handles,
        StudioUx6FDraft source)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(handles);
        ArgumentNullException.ThrowIfNull(source);
        if (!EnsureExecutionPolicyUx6J(out var failure)) return FailedSimulationUx6J(failure);
        var history = _executionPolicyHistoryUx6J!;
        var draftDiagnostics = ValidatePolicyDraftUx6J(draft, history);
        var catalog = ScriptSafetyCatalogUx6J();
        if (!catalog.Areas.Any(item => item.Id == handles.AreaId) ||
            !catalog.Entities.Any(item => item.Id == handles.EntityId) ||
            !catalog.Assets.Any(item => item.Id == handles.AssetId))
        {
            draftDiagnostics = draftDiagnostics.Append(new StudioUx6FDiagnostic(
                "script.handle.subject_missing", "script/handles/subjects", "error",
                "Sélectionnez une zone, un objet et une ressource encore présents dans le module."))
                .ToArray();
        }
        if (draftDiagnostics.Count > 0)
            return FailedSimulationUx6J("Simulation refusée · la politique ou les sujets ont changé.",
                draftDiagnostics);

        try
        {
            var candidate = ToPolicyUx6J(draft);
            var sourceCompilation = CompileScriptSourceUx6F(source);
            var registry = new ForgeScriptHandleRegistry();
            var entity = registry.Register(ForgeScriptHandleKind.Entity, handles.EntityId);
            var area = registry.Register(ForgeScriptHandleKind.Area, handles.AreaId);
            var asset = registry.Register(ForgeScriptHandleKind.Asset, handles.AssetId);
            var effect = registry.Register(ForgeScriptHandleKind.Effect,
                Guid.Parse("6e000000-0000-4000-8000-0000000000e1"));
            var movement = registry.Register(ForgeScriptHandleKind.MovementTask,
                Guid.Parse("6e000000-0000-4000-8000-0000000000e2"));
            var opaque = registry.TryResolve(entity, ForgeScriptHandleKind.Entity, out var entityId, out _) &&
                         entityId == handles.EntityId &&
                         registry.TryResolve(area, ForgeScriptHandleKind.Area, out var areaId, out _) &&
                         areaId == handles.AreaId &&
                         registry.TryResolve(asset, ForgeScriptHandleKind.Asset, out var assetId, out _) &&
                         assetId == handles.AssetId &&
                         registry.TryResolve(effect, ForgeScriptHandleKind.Effect, out _, out _) &&
                         registry.TryResolve(movement, ForgeScriptHandleKind.MovementTask, out _, out _);
            var wrongType = !registry.TryResolve(entity, ForgeScriptHandleKind.Area, out _, out var typeDiagnostic) &&
                            typeDiagnostic?.Code == "script.handle.type_invalid";
            _ = registry.Release(entity, ForgeScriptHandleKind.Entity);
            var stale = !registry.TryResolve(entity, ForgeScriptHandleKind.Entity, out _, out var staleDiagnostic) &&
                        staleDiagnostic?.Code == "script.handle.stale";
            var replacement = registry.Register(ForgeScriptHandleKind.Entity, handles.EntityId);
            var generation = replacement.Slot == entity.Slot && replacement.Generation > entity.Generation;

            var runtime = new NativeForgeScriptRuntime();
            var probe = CompileProbeUx6J(runtime);
            var quotas = new ForgeScriptQuotas(candidate.HandlerInstructionLimit,
                candidate.TimeoutMilliseconds, candidate.RecursionLimit, candidate.SpawnLimit,
                candidate.ContextMemoryLimitBytes, candidate.DeterministicSeed, candidate.SimulationTick,
                candidate.SimulationTimeMilliseconds);
            var first = runtime.Execute(probe, quotas);
            var second = runtime.Execute(probe, quotas);
            var changedSeed = runtime.Execute(probe, new ForgeScriptQuotas(candidate.HandlerInstructionLimit,
                candidate.TimeoutMilliseconds, candidate.RecursionLimit, candidate.SpawnLimit,
                candidate.ContextMemoryLimitBytes, candidate.DeterministicSeed + 1,
                candidate.SimulationTick, candidate.SimulationTimeMilliseconds));
            var deterministic = first.Status == ForgeScriptExecutionStatus.Completed &&
                                second.Status == ForgeScriptExecutionStatus.Completed &&
                                first.Events.SequenceEqual(second.Events) &&
                                !first.Events.SequenceEqual(changedSeed.Events);
            var clock = first.Events.Any(item => item.Kind == "say" &&
                                                  item.Value == candidate.SimulationTick.ToString()) &&
                        first.Events.Any(item => item.Kind == "say" &&
                                                  item.Value == candidate.SimulationTimeMilliseconds.ToString());

            var endless = CompileUx6J(runtime, "void main() { while (true) { int value = 1; } }");
            var instruction = runtime.Execute(endless, new ForgeScriptQuotas(300, 1000, 32, 8));
            var instructionBlocked = instruction.Status == ForgeScriptExecutionStatus.QuotaExceeded;
            var memory = runtime.Execute(CompileUx6J(runtime, "void main() { say(\"memory\"); }"),
                new ForgeScriptQuotas(memoryLimitBytes: 65_536));
            var memoryBlocked = memory.Status == ForgeScriptExecutionStatus.QuotaExceeded &&
                                memory.Diagnostics.Any(item => item.Message.Contains("memory_limit",
                                    StringComparison.Ordinal));
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(40));
            var cancelled = runtime.ExecuteAsync(endless,
                    new ForgeScriptQuotas(int.MaxValue, 5000, 32, 8), cancellation.Token)
                .GetAwaiter().GetResult();
            var cancellationBlocked = cancelled.Status == ForgeScriptExecutionStatus.Cancelled;
            var healthy = runtime.Execute(CompileUx6J(runtime,
                "void main() { say(\"healthy-after-quota\"); }"));
            var recovered = healthy.Status == ForgeScriptExecutionStatus.Completed &&
                            healthy.Events.Any(item => item.Value == "healthy-after-quota");

            var aggregatePolicy = candidate with
            {
                HandlerInstructionLimit = 1,
                TickInstructionLimit = 1,
                ModuleInstructionLimit = Math.Max(10, candidate.ModuleInstructionLimit),
            };
            var aggregateSession = new ForgeScriptExecutionSession(runtime, aggregatePolicy);
            _ = aggregateSession.ExecuteHandlerAsync(CompileUx6J(runtime,
                    "void main() { say(\"bounded\"); }"), "OnUse")
                .GetAwaiter().GetResult();
            var aggregate = aggregateSession.ExecuteHandlerAsync(CompileUx6J(runtime,
                    "void main() {}"), "OnUseAgain")
                .GetAwaiter().GetResult();
            var aggregateBlocked = aggregate.Status == ForgeScriptExecutionStatus.QuotaExceeded &&
                                   aggregateSession.Snapshot.HandlersRejected == 1;
            var negotiation = ForgeScriptApiCompatibility.Negotiate(candidate.ApiVersion,
                new HashSet<string>(["determinism", "opaque-handles"], StringComparer.Ordinal));
            var api = negotiation.Succeeded && negotiation.Selected == ForgeScriptApiCompatibility.Current &&
                      negotiation.Deprecations.Any(item => item.Symbol == "variable") &&
                      ForgeScriptApiCompatibility.Migrations.Any(item => item.From == "1.0" &&
                                                                          item.To == "1.0.0");
            var succeeded = opaque && wrongType && stale && generation && instructionBlocked &&
                            aggregateBlocked && memoryBlocked && cancellationBlocked && recovered &&
                            deterministic && clock && api && sourceCompilation.Succeeded;
            var events = first.Events.Select(item => $"{item.Kind} · {item.Value}")
                .Concat([
                    $"Handles · types protégés, génération {replacement.Generation}",
                    $"Instructions · {first.Telemetry?.Instructions ?? 0:N0}",
                    $"Mémoire pic · {first.Telemetry?.PeakMemoryBytes ?? 0:N0} octets",
                    "Abus · instructions, mémoire et annulation interrompus proprement",
                ])
                .ToArray();
            return new(succeeded,
                succeeded
                    ? "Sandbox R014 cohérente · handles, quotas, replay et version d’API sont prouvés."
                    : "La sandbox R014 n’a pas satisfait tous les invariants.",
                opaque, wrongType, stale, generation, instructionBlocked, aggregateBlocked,
                memoryBlocked, cancellationBlocked, recovered, deterministic, clock, api,
                sourceCompilation.Succeeded, first.Telemetry?.Instructions ?? 0,
                first.Telemetry?.PeakMemoryBytes ?? 0, events, sourceCompilation.Diagnostics);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or
                                           FormatException or OverflowException or DllNotFoundException or
                                           BadImageFormatException)
        {
            return FailedSimulationUx6J($"Simulation R014 refusée : {exception.Message}",
                [new("script.policy.simulation_failed", "script/policy/simulation", "error",
                    exception.Message)]);
        }
    }

    internal StudioUx6JActionResult ApplyApprovedExecutionPolicyUx6J(StudioUx6JPolicyDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!EnsureExecutionPolicyUx6J(out var failure)) return FailedActionUx6J(failure);
        var history = _executionPolicyHistoryUx6J!;
        var approved = _approvedExecutionPolicyUx6J;
        var candidate = ToPolicyUx6J(draft);
        if (approved is null || _approvedExecutionPolicyRevisionUx6J != history.Revision ||
            draft.BaseRevision != history.Revision ||
            !string.Equals(draft.SourceFingerprint, PolicyFingerprintUx6J(history.Policy),
                StringComparison.Ordinal) ||
            !string.Equals(_approvedExecutionPolicyFingerprintUx6J, PolicyFingerprintUx6J(candidate),
                StringComparison.Ordinal) ||
            !string.Equals(PolicyFingerprintUx6J(ToPolicyUx6J(approved)), PolicyFingerprintUx6J(candidate),
                StringComparison.Ordinal))
        {
            CancelUx6JApproval();
            return FailedActionUx6J("Application refusée · relancez l’aperçu sur la politique courante.");
        }

        var command = new ForgeScriptExecutionPolicyCommand(Guid.NewGuid(), history.Revision, candidate,
            "UX6-J R014 policy apply");
        var dryRun = history.Preview(command);
        if (!dryRun.Succeeded) return FailedActionUx6J("Application refusée · la commande n’est plus valide.");
        var previous = history.Policy;
        try
        {
            new ForgeScriptExecutionPolicyStore(Session!.RootDirectory).Save(candidate);
            var applied = history.Apply(command);
            if (!applied.Succeeded)
            {
                new ForgeScriptExecutionPolicyStore(Session.RootDirectory).Save(previous);
                return FailedActionUx6J("Application refusée · la révision a changé.");
            }
            _persistedExecutionPolicyUx6J = candidate;
            CancelUx6JApproval();
            return new(true, "Politique R014 appliquée et enregistrée atomiquement.", history.CanUndo,
                history.CanRedo);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException)
        {
            return FailedActionUx6J($"Enregistrement refusé : {exception.Message}");
        }
    }

    internal StudioUx6JActionResult UndoExecutionPolicyUx6J()
    {
        if (!EnsureExecutionPolicyUx6J(out var failure)) return FailedActionUx6J(failure);
        var history = _executionPolicyHistoryUx6J!;
        var result = history.Undo();
        if (!result.Succeeded) return FailedActionUx6J("Aucune politique à annuler.");
        try
        {
            new ForgeScriptExecutionPolicyStore(Session!.RootDirectory).Save(result.Policy);
            _persistedExecutionPolicyUx6J = result.Policy;
            CancelUx6JApproval();
            return new(true, "Politique R014 annulée.", history.CanUndo, history.CanRedo);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException)
        {
            _ = history.Redo();
            return FailedActionUx6J($"Annulation non enregistrée : {exception.Message}");
        }
    }

    internal StudioUx6JActionResult RedoExecutionPolicyUx6J()
    {
        if (!EnsureExecutionPolicyUx6J(out var failure)) return FailedActionUx6J(failure);
        var history = _executionPolicyHistoryUx6J!;
        var result = history.Redo();
        if (!result.Succeeded) return FailedActionUx6J("Aucune politique à rétablir.");
        try
        {
            new ForgeScriptExecutionPolicyStore(Session!.RootDirectory).Save(result.Policy);
            _persistedExecutionPolicyUx6J = result.Policy;
            CancelUx6JApproval();
            return new(true, "Politique R014 rétablie.", history.CanUndo, history.CanRedo);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException)
        {
            _ = history.Undo();
            return FailedActionUx6J($"Rétablissement non enregistré : {exception.Message}");
        }
    }

    internal StudioUx6JActionResult SaveExecutionPolicyUx6J()
    {
        if (!EnsureExecutionPolicyUx6J(out var failure)) return FailedActionUx6J(failure);
        try
        {
            var policy = _executionPolicyHistoryUx6J!.Policy;
            new ForgeScriptExecutionPolicyStore(Session!.RootDirectory).Save(policy);
            _persistedExecutionPolicyUx6J = policy;
            return new(true, "Politique R014 enregistrée.", _executionPolicyHistoryUx6J.CanUndo,
                _executionPolicyHistoryUx6J.CanRedo);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException)
        {
            return FailedActionUx6J($"Enregistrement refusé : {exception.Message}");
        }
    }

    internal StudioUx6JApprovalSnapshot CaptureExecutionPolicyApprovalUx6J() =>
        new(_approvedExecutionPolicyUx6J, _approvedExecutionPolicyRevisionUx6J,
            _approvedExecutionPolicyFingerprintUx6J);

    internal void RestoreExecutionPolicyApprovalUx6J(StudioUx6JApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedExecutionPolicyUx6J = snapshot.Draft;
        _approvedExecutionPolicyRevisionUx6J = snapshot.Revision;
        _approvedExecutionPolicyFingerprintUx6J = snapshot.CandidateFingerprint;
    }

    internal void CancelUx6JApproval()
    {
        _approvedExecutionPolicyUx6J = null;
        _approvedExecutionPolicyRevisionUx6J = null;
        _approvedExecutionPolicyFingerprintUx6J = null;
    }

    internal void CloseUx6J()
    {
        _executionPolicyHistoryUx6J = null;
        _persistedExecutionPolicyUx6J = null;
        CancelUx6JApproval();
    }

    internal StudioUx6JSmokeResult RunUx6JContractSmoke()
    {
        var sourceProject = Project;
        if (Session is null || sourceProject is null || History is null) return FailedSmokeUx6J();
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux6-j",
            Guid.NewGuid().ToString("N"));
        var released = false;
        StudioUx6JSmokeResult result;
        try
        {
            if (!ProjectSession.Create(root, sourceProject).Succeeded) return FailedSmokeUx6J();
            var sourceDocument = EvidenceDocumentUx6F();
            new ForgeScriptSourceStore(root).Save(sourceDocument);
            var editing = new StudioSessionController();
            if (!editing.Open(root).Succeeded ||
                !editing.TryOpenCanonicalSourceUx6F(sourceDocument.RelativePath, out var source, out _) ||
                source is null)
                return FailedSmokeUx6J();
            var catalog = editing.ScriptSafetyCatalogUx6J();
            var initial = editing.CurrentExecutionPolicyUx6J();
            if (!catalog.Complete || initial is null) return FailedSmokeUx6J();
            var handles = new StudioUx6JHandleDraft(catalog.Areas[0].Id, catalog.Entities[0].Id,
                catalog.Assets[0].Id);
            var candidate = initial with
            {
                HandlerInstructionLimit = 5_000,
                TickInstructionLimit = 25_000,
                ContextMemoryLimitBytes = 4 * 1024 * 1024,
                DeterministicSeed = 14_014,
                SimulationTick = 42,
                SimulationTimeMilliseconds = 7_000,
                Provenance = "studio:ux6-j:r014",
            };
            var invalidInstruction = !editing.PreviewExecutionPolicyUx6J(candidate with
            {
                HandlerInstructionLimit = candidate.TickInstructionLimit + 1,
            }).Succeeded;
            var invalidMemory = !editing.PreviewExecutionPolicyUx6J(candidate with
            {
                ContextMemoryLimitBytes = int.MaxValue,
            }).Succeeded;
            var invalidSeed = !editing.PreviewExecutionPolicyUx6J(candidate with
            {
                DeterministicSeed = 0,
            }).Succeeded;
            var invalidApi = !editing.PreviewExecutionPolicyUx6J(candidate with
            {
                ApiVersion = "2.0.0",
            }).Succeeded;
            var preview = editing.PreviewExecutionPolicyUx6J(candidate);
            var dryRun = preview.Succeeded && preview.CanApply &&
                         editing.CurrentExecutionPolicyUx6J()?.HandlerInstructionLimit ==
                         initial.HandlerInstructionLimit && !File.Exists(Path.Combine(root, Ux6JPolicyFile));
            var simulation = editing.SimulateExecutionPolicyUx6J(candidate, handles, source);
            var stale = !editing.ApplyApprovedExecutionPolicyUx6J(candidate with
            {
                DeterministicSeed = candidate.DeterministicSeed + 1,
            }).Succeeded;
            _ = editing.PreviewExecutionPolicyUx6J(candidate);
            var applied = editing.ApplyApprovedExecutionPolicyUx6J(candidate);
            var appliedDocument = new ForgeScriptExecutionPolicyStore(root).Load();
            var undone = editing.UndoExecutionPolicyUx6J();
            var undoDocument = new ForgeScriptExecutionPolicyStore(root).Load();
            var redone = editing.RedoExecutionPolicyUx6J();
            var redoDocument = new ForgeScriptExecutionPolicyStore(root).Load();
            var saved = editing.SaveExecutionPolicyUx6J();
            var reopened = new StudioSessionController();
            var persistence = reopened.Open(root).Succeeded &&
                              reopened.CurrentExecutionPolicyUx6J() is { } reopenedDraft &&
                              PolicyFingerprintUx6J(ToPolicyUx6J(reopenedDraft)) ==
                              PolicyFingerprintUx6J(ToPolicyUx6J(candidate));
            reopened.Close();
            var applyUndoRedo = applied.Succeeded && undone.Succeeded && redone.Succeeded && saved.Succeeded &&
                                PolicyFingerprintUx6J(appliedDocument) ==
                                PolicyFingerprintUx6J(ToPolicyUx6J(candidate)) &&
                                PolicyFingerprintUx6J(undoDocument) ==
                                PolicyFingerprintUx6J(ToPolicyUx6J(initial)) &&
                                PolicyFingerprintUx6J(redoDocument) ==
                                PolicyFingerprintUx6J(ToPolicyUx6J(candidate));
            var sourcePreserved = SourceDocumentContentsEqualUx6J(
                new ForgeScriptSourceStore(root).Load(sourceDocument.RelativePath), sourceDocument);
            var neighbors = editing.Project is not null &&
                            string.Equals(ProjectJsonSerializer.Serialize(editing.Project),
                                ProjectJsonSerializer.Serialize(sourceProject), StringComparison.Ordinal);
            var invalid = invalidInstruction && invalidMemory && invalidSeed && invalidApi;
            var instructionBudgets = simulation.InstructionQuotaBlocked && simulation.AggregateQuotaBlocked &&
                                     simulation.CancellationBlocked && simulation.RuntimeRecovered;
            var memoryBudgets = simulation.MemoryQuotaBlocked && simulation.PeakMemoryBytes > 0;
            var succeeded = preview.Succeeded && simulation.Succeeded && dryRun && invalid && stale &&
                            applyUndoRedo && persistence && sourcePreserved && neighbors;
            result = new(succeeded, true, catalog.Complete, initial.SchemaVersion ==
                ForgeScriptExecutionPolicy.CurrentSchemaVersion, simulation.OpaqueHandles &&
                simulation.WrongTypeBlocked && simulation.StaleHandleBlocked &&
                simulation.GenerationAdvanced, instructionBudgets, memoryBudgets,
                simulation.DeterministicReplay && simulation.SimulationClock,
                simulation.ApiNegotiation, dryRun, invalid, stale, applyUndoRedo, persistence,
                sourcePreserved, neighbors, false);
            editing.Close();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or ArgumentException or FormatException or
                                           DllNotFoundException or BadImageFormatException)
        {
            result = FailedSmokeUx6J();
        }
        finally
        {
            released = DeleteUx6FTemporaryWorkspace(root);
        }
        return result with { Succeeded = result.Succeeded && released, TemporaryWorkspaceReleased = released };
    }

    private bool EnsureExecutionPolicyUx6J(out string failure)
    {
        failure = string.Empty;
        if (_executionPolicyHistoryUx6J is not null) return true;
        if (Session is null)
        {
            failure = "Aucun module ouvert.";
            return false;
        }
        try
        {
            var path = Path.Combine(Session.RootDirectory, Ux6JPolicyFile);
            var policy = File.Exists(path)
                ? new ForgeScriptExecutionPolicyStore(Session.RootDirectory).Load()
                : ForgeScriptExecutionPolicy.Default;
            var diagnostics = ForgeScriptExecutionPolicyValidator.Validate(policy);
            if (diagnostics.Count > 0)
            {
                failure = $"Politique R014 invalide : {string.Join(", ", diagnostics.Select(item => item.Code))}.";
                return false;
            }
            _executionPolicyHistoryUx6J = new ForgeScriptExecutionPolicyHistory(policy);
            _persistedExecutionPolicyUx6J = File.Exists(path) ? policy : null;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or System.Text.Json.JsonException)
        {
            failure = $"Politique R014 illisible : {exception.Message}";
            return false;
        }
    }

    private static IReadOnlyList<StudioUx6FDiagnostic> ValidatePolicyDraftUx6J(
        StudioUx6JPolicyDraft draft,
        ForgeScriptExecutionPolicyHistory history)
    {
        var diagnostics = new List<StudioUx6FDiagnostic>();
        if (draft.BaseRevision != history.Revision)
            diagnostics.Add(new("script.policy.revision_conflict", "script/policy/revision", "error",
                "La révision de politique a changé."));
        if (!string.Equals(draft.SourceFingerprint, PolicyFingerprintUx6J(history.Policy),
                StringComparison.Ordinal))
            diagnostics.Add(new("script.policy.source_changed", "script/policy", "error",
                "La politique source a changé depuis son chargement."));
        return diagnostics;
    }

    private static StudioUx6JPolicyDraft ToDraftUx6J(ForgeScriptExecutionPolicy policy, long revision) =>
        new(policy.Id, policy.SchemaVersion, policy.Provenance, policy.ApiVersion,
            policy.HandlerInstructionLimit, policy.TickInstructionLimit, policy.ModuleInstructionLimit,
            policy.TimeoutMilliseconds, policy.RecursionLimit, policy.SpawnLimit,
            policy.ContextMemoryLimitBytes, policy.ModuleMemoryLimitBytes, policy.DeterministicSeed,
            policy.SimulationTick, policy.SimulationTimeMilliseconds, policy.Authority,
            policy.RequiredAtRuntime, revision, PolicyFingerprintUx6J(policy));

    private static ForgeScriptExecutionPolicy ToPolicyUx6J(StudioUx6JPolicyDraft draft) =>
        new(draft.Id, draft.SchemaVersion, draft.Provenance, draft.ApiVersion,
            draft.HandlerInstructionLimit, draft.TickInstructionLimit, draft.ModuleInstructionLimit,
            draft.TimeoutMilliseconds, draft.RecursionLimit, draft.SpawnLimit,
            draft.ContextMemoryLimitBytes, draft.ModuleMemoryLimitBytes, draft.DeterministicSeed,
            draft.SimulationTick, draft.SimulationTimeMilliseconds, draft.Authority,
            draft.RequiredAtRuntime);

    private static string PolicyFingerprintUx6J(ForgeScriptExecutionPolicy policy)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(policy.Id.ToByteArray());
        writer.Write(policy.SchemaVersion);
        WriteStringUx6J(writer, policy.Provenance);
        WriteStringUx6J(writer, policy.ApiVersion);
        writer.Write(policy.HandlerInstructionLimit);
        writer.Write(policy.TickInstructionLimit);
        writer.Write(policy.ModuleInstructionLimit);
        writer.Write(policy.TimeoutMilliseconds);
        writer.Write(policy.RecursionLimit);
        writer.Write(policy.SpawnLimit);
        writer.Write(policy.ContextMemoryLimitBytes);
        writer.Write(policy.ModuleMemoryLimitBytes);
        writer.Write(policy.DeterministicSeed);
        writer.Write(policy.SimulationTick);
        writer.Write(policy.SimulationTimeMilliseconds);
        writer.Write((int)policy.Authority);
        writer.Write(policy.RequiredAtRuntime);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteStringUx6J(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static bool SourceDocumentContentsEqualUx6J(
        ForgeScriptSourceDocument left,
        ForgeScriptSourceDocument right) =>
        left.Id == right.Id && left.SchemaVersion == right.SchemaVersion &&
        string.Equals(left.Provenance, right.Provenance, StringComparison.Ordinal) &&
        string.Equals(left.RelativePath, right.RelativePath, StringComparison.Ordinal) &&
        string.Equals(left.Source, right.Source, StringComparison.Ordinal) &&
        string.Equals(left.ApiVersion, right.ApiVersion, StringComparison.Ordinal) &&
        left.Distribution == right.Distribution && left.RequiredAtRuntime == right.RequiredAtRuntime &&
        left.Dependencies.SequenceEqual(right.Dependencies, StringComparer.Ordinal) &&
        left.EventBindings.SequenceEqual(right.EventBindings) &&
        (left.RequiredCapabilities ?? new HashSet<string>(StringComparer.Ordinal)).SetEquals(
            right.RequiredCapabilities ?? new HashSet<string>(StringComparer.Ordinal));

    private static ForgeScriptArtifact CompileProbeUx6J(NativeForgeScriptRuntime runtime) =>
        CompileUx6J(runtime, """
            void main() {
                AssetId asset = resolve_asset("ui/quest-marker");
                EffectHandle effect = create_effect("ash-burn");
                say(format_int(random_int(1, 1000000)));
                say(format_int(simulation_tick()));
                say(format_int(simulation_time_ms()));
                release_effect(effect);
            }
            """);

    private static ForgeScriptArtifact CompileUx6J(NativeForgeScriptRuntime runtime, string source)
    {
        var result = runtime.Compile(source);
        if (!result.Success || result.Artifact is not ForgeScriptArtifact artifact)
            throw new InvalidOperationException(string.Join("; ", result.Diagnostics.Select(item => item.Message)));
        return artifact;
    }

    private static StudioUx6FDiagnostic ToStudioDiagnosticUx6J(ForgeScriptExecutionPolicyDiagnostic diagnostic) =>
        new(diagnostic.Code, diagnostic.Path, diagnostic.Severity, diagnostic.Message);

    private static StudioUx6JPreview FailedPreviewUx6J(string message,
        IReadOnlyList<StudioUx6FDiagnostic>? diagnostics = null) =>
        new(false, false, message, "no-change", string.Empty, [], [], [], diagnostics ?? [], true);

    private static StudioUx6JSimulation FailedSimulationUx6J(string message,
        IReadOnlyList<StudioUx6FDiagnostic>? diagnostics = null) =>
        new(false, message, false, false, false, false, false, false, false, false, false, false,
            false, false, false, 0, 0, [], diagnostics ?? []);

    private StudioUx6JActionResult FailedActionUx6J(string message) =>
        new(false, message, _executionPolicyHistoryUx6J?.CanUndo == true,
            _executionPolicyHistoryUx6J?.CanRedo == true);

    private static StudioUx6JSmokeResult FailedSmokeUx6J() =>
        new(false, false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false);
}
