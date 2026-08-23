using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Assistant;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Scripting;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux6QProvenance = "studio:ux6-q:compiler-assistant";
    private const string Ux6QDefaultPath = "scripts/assisted-interaction.as";

    private readonly SetRuntimeExtensionsHandler _setRuntimeExtensionsUx6Q = new(new SystemClock());
    private readonly ApplyAssistedCanonicalForgeScriptHandler _applyAssistedCanonicalUx6Q = new();
    private readonly AssistedCanonicalForgeScriptCommandHistory _assistedHistoryUx6Q = new();

    private StudioUx6QCompilerDraft? _approvedCompilerUx6Q;
    private ProjectRevision? _approvedCompilerRevisionUx6Q;
    private string? _approvedCompilerFingerprintUx6Q;
    private AssistedForgeScriptProposal? _proposedAssistantUx6Q;
    private ForgeScriptSourceDocument? _proposedAssistantCandidateUx6Q;
    private string? _proposedAssistantDraftFingerprintUx6Q;
    private StudioUx6QAssistantApprovalState? _assistantApprovalUx6Q;

    internal IReadOnlyList<StudioUx6QProfileChoice> CompilerProfileChoicesUx6Q()
    {
        var project = Project;
        if (project is null) return [];
        var compiler = project.Module.RuntimePolicies.Extensions.ModernTooling.Compiler;
        return [new($"{HumanUx6Q(project.Module.DisplayName)} · compilation incrémentale " +
                    $"{(compiler.Incremental ? "active" : "inactive")} · {compiler.ConformanceSuites.Count} suite(s)")];
    }

    internal IReadOnlyList<StudioUx6QSourceChoice> AssistantSourceChoicesUx6Q()
    {
        var session = Session;
        if (session is null) return [];
        var scriptsRoot = Path.Combine(session.RootDirectory, "scripts");
        if (!Directory.Exists(scriptsRoot)) return [];
        return Directory.EnumerateFiles(scriptsRoot, "*.as", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var relative = Path.GetRelativePath(session.RootDirectory, path).Replace('\\', '/');
                var canonical = File.Exists(path + ".meta.json");
                return new StudioUx6QSourceChoice(
                    canonical
                        ? $"Source canonique · {HumanPathUx6Q(relative)}"
                        : $"Source incomplète protégée · {HumanPathUx6Q(relative)}",
                    relative,
                    canonical);
            }).ToArray();
    }

    internal StudioUx6QCompilerDraft? CreateCompilerDraftUx6Q(bool explicitlySelected)
    {
        var project = Project;
        var history = History;
        if (!explicitlySelected || project is null || history is null) return null;
        var compiler = project.Module.RuntimePolicies.Extensions.ModernTooling.Compiler;
        return new(history.State.Revision, ProjectFingerprintUx6Q(project), compiler.Incremental,
            compiler.StableDiagnostics, compiler.PerformanceTelemetry, compiler.MaxParallelism,
            compiler.MaxSourceBytes, compiler.ConformanceSuites.ToArray());
    }

    internal StudioUx6QCompilerPreview PreviewCompilerUx6Q(StudioUx6QCompilerDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelCompilerApprovalUx6Q();
        var history = History;
        var project = Project;
        if (history is null || project is null)
            return FailedCompilerPreviewUx6Q("Ouvrez un module et choisissez son profil compilateur.",
                "studio.compiler.selection_required", "module/runtimePolicies/extensions/modernTooling/compiler");
        if (draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceFingerprint, ProjectFingerprintUx6Q(project), StringComparison.Ordinal))
            return FailedCompilerPreviewUx6Q("Le projet a changé · rechargez le profil compilateur.",
                "studio.compiler.source_changed", "module/runtimePolicies/extensions/modernTooling/compiler");
        if (!TryBuildCompilerCandidateUx6Q(project, draft, out var candidate, out var diagnostic))
            return FailedCompilerPreviewUx6Q(diagnostic!.Message, diagnostic.Code, diagnostic.Path);

        var command = new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            candidate!, DryRun: true);
        var dryRun = _setRuntimeExtensionsUx6Q.Handle(history.State, command);
        if (!dryRun.Succeeded)
            return FailedCompilerPreviewUx6Q(HumanCommandFailureUx6Q(dryRun),
                dryRun.Diagnostics.FirstOrDefault()?.Code ?? "studio.compiler.command_refused",
                "module/runtimePolicies/extensions/modernTooling/compiler");

        var source = project.Module.RuntimePolicies.Extensions.ModernTooling.Compiler;
        var target = candidate!.ModernTooling.Compiler;
        var changes = CompilerChangesUx6Q(source, target);
        var compilation = RunCompilerCompilationUx6Q(draft);
        if (changes.Count > 0)
        {
            _approvedCompilerUx6Q = draft;
            _approvedCompilerRevisionUx6Q = history.State.Revision;
            _approvedCompilerFingerprintUx6Q = CompilerCandidateFingerprintUx6Q(project, draft);
        }
        return new(true, changes.Count > 0, true,
            changes.Count > 0
                ? "Aperçu sec canonique validé · le profil et les sources restent inchangés."
                : "Aperçu sec validé · le profil compilateur est déjà identique.",
            "Remplacement du compilateur moderne uniquement; copie runtime, indicateur, NUI, documentation et QA restent inchangés.",
            changes, compilation, []);
    }

    internal StudioUx6QCompilationEvidence RunCompilerCompilationUx6Q(StudioUx6QCompilerDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var session = Session;
        if (session is null)
            return FailedCompilationUx6Q("Aucun module ouvert.",
                new StudioUx6QDiagnostic("studio.compiler.workspace_required", "workspace", "error",
                    "Workspace requis."));
        if (!TryBuildCompilerOptionsUx6Q(draft, out var options, out var optionDiagnostic))
            return FailedCompilationUx6Q(optionDiagnostic!.Message, optionDiagnostic);

        var loaded = LoadCompilerUnitsUx6Q(session.RootDirectory);
        if (loaded.Diagnostics.Count > 0)
            return FailedCompilationUx6Q("Certaines sources canoniques sont invalides.",
                loaded.Diagnostics.ToArray());
        if (loaded.Units.Count == 0)
            return new(true, false, false, false, false, draft.StableDiagnostics,
                draft.PerformanceTelemetry, false, true, 0, 0, 0, 0, 0, [], [],
                "État vide · aucune source .as canonique à compiler. Créez-la dans la section assistée ou UX6-F.");

        var cacheRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux6-q-compiler",
            Guid.NewGuid().ToString("N"));
        var released = false;
        StudioUx6QCompilationEvidence result;
        try
        {
            var runtime = new NativeForgeScriptRuntime();
            var compiler = new ForgeScriptCompilerService(new ForgeScriptArtifactCache(runtime, cacheRoot), options!);
            var first = compiler.Compile(loaded.Units);
            var second = compiler.Compile(loaded.Units);
            var dependencyRoot = loaded.Units.SelectMany(unit => unit.Dependencies)
                .FirstOrDefault(path => loaded.Units.Any(unit =>
                    string.Equals(unit.Path, path, StringComparison.Ordinal)));
            var changedPath = dependencyRoot ?? loaded.Units[0].Path;
            var changedUnits = loaded.Units.Select(unit => string.Equals(unit.Path, changedPath,
                    StringComparison.Ordinal)
                ? unit with { Source = unit.Source + "\n// ux6-q-dependency-invalidation" }
                : unit).ToArray();
            var changed = compiler.Compile(changedUnits);
            var conformance = compiler.RunConformance(
            [
                new("syntax-valid", "void main() { say(\"compiler-ok\"); }", true, null),
                new("syntax-invalid", "void main( {", false, null),
            ]);
            var dependencyAware = dependencyRoot is null || changed.Compiled >= 1;
            var cacheReused = second.Reused == loaded.Units.Count;
            var invalidated = changed.Compiled >= 1 && (!string.IsNullOrWhiteSpace(dependencyRoot)
                ? changed.Compiled >= 2
                : true);
            var stable = !draft.StableDiagnostics || first.Entries.All(entry =>
                entry.Diagnostics.SequenceEqual(entry.Diagnostics.OrderBy(item => item.Line)
                    .ThenBy(item => item.Column).ThenBy(item => item.Source, StringComparer.Ordinal)
                    .ThenBy(item => item.Message, StringComparer.Ordinal)));
            var telemetry = !draft.PerformanceTelemetry || first.Entries.All(entry => entry.ElapsedMicroseconds >= 0);
            var conformancePassed = conformance.Count == 2 && conformance.All(item => item.Passed);
            var entries = first.Entries.Select(entry =>
                $"{HumanPathUx6Q(entry.Path)} · {(entry.Success ? "compilé" : "refusé")} · " +
                $"{entry.BytecodeBytes:N0} octets · {(entry.CacheHit ? "cache" : "nouveau")}").ToArray();
            var diagnostics = first.Entries.SelectMany(entry => entry.Diagnostics.Select(item =>
                new StudioUx6QDiagnostic(item.Source, entry.Path, item.Severity,
                    $"{item.Message} · ligne {item.Line}, colonne {item.Column}"))).ToArray();
            var succeeded = first.Success && second.Success && changed.Success && dependencyAware && cacheReused &&
                            invalidated && stable && telemetry && conformancePassed;
            result = new(succeeded, true, dependencyAware, cacheReused, invalidated, stable, telemetry,
                conformancePassed, false, loaded.Units.Count, first.Compiled, second.Reused,
                changed.Compiled, first.ElapsedMicroseconds, entries, diagnostics,
                succeeded
                    ? "Compilation incrémentale exercée en mémoire · cache, invalidation, diagnostics, télémétrie et conformité validés."
                    : "Le compilateur a signalé une preuve incomplète; aucune source n’a été modifiée.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or ArgumentException or
                                           DllNotFoundException or BadImageFormatException)
        {
            result = FailedCompilationUx6Q($"Compilation indisponible : {exception.Message}",
                new StudioUx6QDiagnostic("studio.compiler.unavailable", "scripts", "error",
                    exception.Message));
        }
        finally
        {
            released = DeleteTemporaryUx6Q(cacheRoot);
        }
        return result with
        {
            Succeeded = result.Succeeded && released,
            TemporaryCacheReleased = released,
        };
    }

    internal StudioUx6QActionResult ApplyApprovedCompilerUx6Q()
    {
        var draft = _approvedCompilerUx6Q;
        var history = History;
        var project = Project;
        if (draft is null || history is null || project is null ||
            _approvedCompilerRevisionUx6Q != history.State.Revision ||
            draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceFingerprint, ProjectFingerprintUx6Q(project), StringComparison.Ordinal) ||
            !string.Equals(_approvedCompilerFingerprintUx6Q,
                CompilerCandidateFingerprintUx6Q(project, draft), StringComparison.Ordinal) ||
            !TryBuildCompilerCandidateUx6Q(project, draft, out var candidate, out _))
        {
            CancelCompilerApprovalUx6Q();
            return ActionUx6Q(false, "Le profil ou le projet a changé · relancez l’aperçu compilateur.", projectHistory: true);
        }

        var command = new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            candidate!, DryRun: true);
        var dryRun = _setRuntimeExtensionsUx6Q.Handle(history.State, command);
        if (!dryRun.Succeeded)
        {
            CancelCompilerApprovalUx6Q();
            return ActionUx6Q(false, HumanCommandFailureUx6Q(dryRun), projectHistory: true);
        }
        var applied = _setRuntimeExtensionsUx6Q.Handle(history.State, command with { DryRun = false });
        CancelCompilerApprovalUx6Q();
        if (!applied.Succeeded)
            return ActionUx6Q(false, HumanCommandFailureUx6Q(applied), projectHistory: true);
        history.Record(applied);
        InvalidateContentIndexes();
        return ActionUx6Q(true,
            "Profil compilateur appliqué · les autres extensions restent inchangées.", projectHistory: true);
    }

    internal bool TryCreateAssistantDraftUx6Q(
        string relativePath,
        out StudioUx6QAssistantDraft? draft,
        out string message)
    {
        draft = null;
        var session = Session;
        var project = Project;
        var history = History;
        if (session is null || project is null || history is null)
        {
            message = "Ouvrez un module avant de préparer une source assistée.";
            return false;
        }

        string normalized;
        string sourcePath;
        try
        {
            normalized = AssistedForgeScriptCreationService.NormalizeRelativePath(relativePath);
            if (!normalized.EndsWith(".as", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("La création Studio exige une source canonique .as.");
            sourcePath = AssistedForgeScriptCreationService.ResolvePath(session.RootDirectory, normalized);
        }
        catch (ArgumentException exception)
        {
            message = $"Chemin refusé : {exception.Message}";
            return false;
        }

        var sourceExists = File.Exists(sourcePath);
        var metadataExists = File.Exists(sourcePath + ".meta.json");
        if (sourceExists != metadataExists)
        {
            message = "La paire source/métadonnées est incomplète et reste protégée.";
            return false;
        }

        ForgeScriptSourceDocument template;
        try
        {
            template = sourceExists
                ? new ForgeScriptSourceStore(session.RootDirectory).Load(normalized)
                : new ForgeScriptSourceDocument(Guid.NewGuid(), ForgeScriptSourceDocument.CurrentSchemaVersion,
                    Ux6QProvenance + ":draft", normalized, string.Empty, "1.0.0",
                    ForgeScriptSourceDistributionPolicy.SourceAndBytecode, false, [], []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or ArgumentException or
                                           System.Text.Json.JsonException)
        {
            message = $"Source canonique refusée : {exception.Message}";
            return false;
        }

        draft = new(history.State.Revision, ProjectFingerprintUx6Q(project),
            AssistantDiskFingerprintUx6Q(session.RootDirectory, normalized), !sourceExists, template,
            normalized, "OnUse", "Créer une interaction locale déterministe et lisible.",
            "Une étincelle répond.", "assistant.script.used", "true",
            [$"module:{project.Module.Slug}"], ["style:brief", "sandbox:no-external-io"],
            "@studio-author", 20260821, sourceExists ? template.Source : string.Empty);
        message = sourceExists
            ? $"Source canonique chargée pour réparation · {HumanPathUx6Q(normalized)}."
            : $"Nouvelle source préparée en mémoire · {HumanPathUx6Q(normalized)}.";
        CancelAssistantProposalUx6Q();
        return true;
    }

    internal StudioUx6QProposalView GenerateAssistantProposalUx6Q(StudioUx6QAssistantDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelAssistantProposalUx6Q();
        if (!AssistantDraftCurrentUx6Q(draft, out var currentFailure))
            return FailedProposalUx6Q(currentFailure);
        var session = Session!;
        try
        {
            var brief = new AssistedForgeScriptBrief(draft.Template.Id, draft.RelativePath,
                draft.EventBinding.Trim(), draft.Intent.Trim(), draft.Message.Trim(),
                draft.VariableKey.Trim(), draft.VariableValue.Trim(),
                draft.ContextCitations.Select(item => item.Trim()).Where(item => item.Length > 0).ToArray(),
                draft.StyleRules.Select(item => item.Trim()).Where(item => item.Length > 0).ToArray(),
                draft.AuthorId.Trim(), draft.Seed,
                string.IsNullOrWhiteSpace(draft.SourceOverride) ? null : draft.SourceOverride);
            var proposal = AssistedForgeScriptCreationService.Generate(brief, session.RootDirectory,
                new NativeForgeScriptRuntime(), "offline-studio", 0,
                "assistant:deterministic:studio:ux6-q", "offline-studio-ux6-q");
            var candidate = draft.Template with
            {
                Provenance = proposal.Provenance,
                RelativePath = proposal.RelativePath,
                Source = proposal.Source,
                ApiVersion = proposal.ForgeScriptApiVersion,
            };
            _proposedAssistantUx6Q = proposal;
            _proposedAssistantCandidateUx6Q = candidate;
            _proposedAssistantDraftFingerprintUx6Q = AssistantDraftFingerprintUx6Q(draft);
            return ProposalViewUx6Q(proposal);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           IOException or UnauthorizedAccessException or
                                           DllNotFoundException or BadImageFormatException)
        {
            CancelAssistantProposalUx6Q();
            return FailedProposalUx6Q(exception.Message);
        }
    }

    internal StudioUx6QAssistantPreview PreviewAssistantUx6Q(StudioUx6QAssistantDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        _assistantApprovalUx6Q = null;
        if (!AssistantDraftCurrentUx6Q(draft, out var currentFailure))
            return FailedAssistantPreviewUx6Q(currentFailure);
        if (_proposedAssistantUx6Q is null || _proposedAssistantCandidateUx6Q is null ||
            !string.Equals(_proposedAssistantDraftFingerprintUx6Q,
                AssistantDraftFingerprintUx6Q(draft), StringComparison.Ordinal))
            return FailedAssistantPreviewUx6Q("Le brouillon a changé · générez de nouveau la proposition hors ligne.");

        var approved = _proposedAssistantUx6Q.WithApproval(true, Ux6QProvenance + ":author-approved");
        var candidate = _proposedAssistantCandidateUx6Q with { Provenance = approved.Provenance };
        var transactionId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var command = new ApplyAssistedCanonicalForgeScriptCommand(Guid.NewGuid(), transactionId, planId,
            draft.BaseRevision.Value, approved, candidate, AssistantPermissionLevel.ApplyApprovedPlan, true);
        var preview = _applyAssistedCanonicalUx6Q.Handle(Session!.RootDirectory,
            History!.State.Revision.Value, command);
        if (!preview.Succeeded)
            return FailedAssistantPreviewUx6Q(HumanAssistantDiagnosticsUx6Q(preview.Diagnostics));

        _assistantApprovalUx6Q = new(draft, approved, candidate, draft.BaseRevision,
            AssistantDraftFingerprintUx6Q(draft), transactionId, planId);
        return new(true, true, true, true, true,
            "Plan canonique prévisualisé et approuvé · aucune source n’a été écrite.",
            preview.SemanticDiff, approved.Tests.Select(TestTextUx6Q).ToArray(), []);
    }

    internal StudioUx6QActionResult ApplyApprovedAssistantUx6Q(StudioUx6QAssistantDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var approval = _assistantApprovalUx6Q;
        if (approval is null || !AssistantDraftCurrentUx6Q(draft, out _) ||
            approval.Revision != History!.State.Revision || draft.BaseRevision != History.State.Revision ||
            !string.Equals(approval.DraftFingerprint, AssistantDraftFingerprintUx6Q(draft), StringComparison.Ordinal) ||
            !string.Equals(approval.DraftFingerprint, AssistantDraftFingerprintUx6Q(approval.Draft),
                StringComparison.Ordinal))
        {
            _assistantApprovalUx6Q = null;
            return ActionUx6Q(false,
                "La source, le projet ou le brouillon a changé · régénérez et prévisualisez le plan.",
                projectHistory: false);
        }

        var command = new ApplyAssistedCanonicalForgeScriptCommand(Guid.NewGuid(), approval.TransactionId,
            approval.ExecutionPlanId, approval.Revision.Value, approval.Proposal, approval.Candidate,
            AssistantPermissionLevel.ApplyApprovedPlan, false);
        var result = _applyAssistedCanonicalUx6Q.Handle(Session!.RootDirectory,
            History.State.Revision.Value, command);
        _assistantApprovalUx6Q = null;
        if (!result.Succeeded || !result.Applied)
            return ActionUx6Q(false, HumanAssistantDiagnosticsUx6Q(result.Diagnostics), projectHistory: false);
        _assistedHistoryUx6Q.Record(result);
        CancelAssistantProposalUx6Q(keepHistory: true);
        return ActionUx6Q(true,
            "Source assistée enregistrée atomiquement en .as + métadonnées canoniques.",
            projectHistory: false);
    }

    internal StudioUx6QActionResult UndoCompilerUx6Q()
    {
        CancelCompilerApprovalUx6Q();
        var result = Undo();
        return ActionUx6Q(result.Succeeded, result.Message, projectHistory: true);
    }

    internal StudioUx6QActionResult RedoCompilerUx6Q()
    {
        CancelCompilerApprovalUx6Q();
        var result = Redo();
        return ActionUx6Q(result.Succeeded, result.Message, projectHistory: true);
    }

    internal StudioUx6QActionResult SaveCompilerUx6Q()
    {
        var result = Save();
        return ActionUx6Q(result.Succeeded, result.Message, projectHistory: true);
    }

    internal StudioUx6QActionResult UndoAssistantUx6Q()
    {
        CancelAssistantProposalUx6Q(keepHistory: true);
        if (Session is null || !_assistedHistoryUx6Q.Undo(Session.RootDirectory))
            return ActionUx6Q(false, "Aucune source assistée à annuler.", projectHistory: false);
        return ActionUx6Q(true, "Écriture assistée annulée exactement.", projectHistory: false);
    }

    internal StudioUx6QActionResult RedoAssistantUx6Q()
    {
        CancelAssistantProposalUx6Q(keepHistory: true);
        if (Session is null || !_assistedHistoryUx6Q.Redo(Session.RootDirectory))
            return ActionUx6Q(false, "Aucune source assistée à rétablir.", projectHistory: false);
        return ActionUx6Q(true, "Écriture assistée rétablie exactement.", projectHistory: false);
    }

    internal StudioUx6QApprovalSnapshot CaptureCompilerAssistantApprovalUx6Q() => new(
        _approvedCompilerUx6Q, _approvedCompilerRevisionUx6Q, _approvedCompilerFingerprintUx6Q,
        _proposedAssistantUx6Q, _proposedAssistantCandidateUx6Q, _proposedAssistantDraftFingerprintUx6Q,
        _assistantApprovalUx6Q);

    internal void RestoreCompilerAssistantApprovalUx6Q(StudioUx6QApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedCompilerUx6Q = snapshot.CompilerDraft;
        _approvedCompilerRevisionUx6Q = snapshot.CompilerRevision;
        _approvedCompilerFingerprintUx6Q = snapshot.CompilerFingerprint;
        _proposedAssistantUx6Q = snapshot.Proposed;
        _proposedAssistantCandidateUx6Q = snapshot.ProposedCandidate;
        _proposedAssistantDraftFingerprintUx6Q = snapshot.ProposedDraftFingerprint;
        _assistantApprovalUx6Q = snapshot.AssistantApproval;
    }

    internal void CloseUx6Q()
    {
        CancelCompilerApprovalUx6Q();
        CancelAssistantProposalUx6Q();
        _assistedHistoryUx6Q.Reset();
    }

    internal StudioUx6QSmokeResult RunUx6QContractSmoke()
    {
        var sourceProject = Project;
        if (sourceProject is null) return FailedSmokeUx6Q();
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux6-q",
            Guid.NewGuid().ToString("N"));
        var released = false;
        StudioUx6QSmokeResult result;
        try
        {
            result = RunIsolated();
        }
        finally
        {
            released = DeleteTemporaryUx6Q(root);
        }
        return result with { Succeeded = result.Succeeded && released, TemporaryWorkspaceReleased = released };

        StudioUx6QSmokeResult RunIsolated()
        {
            if (!ProjectSession.Create(root, sourceProject).Succeeded) return FailedSmokeUx6Q();
            var shared = new ForgeScriptSourceDocument(Guid.NewGuid(), 1, "studio:ux6-q:evidence",
                "scripts/ux6-q-shared.as", "void main() { say(\"shared\"); }\n", "1.0.0",
                ForgeScriptSourceDistributionPolicy.SourceAndBytecode, false, [], []);
            var main = new ForgeScriptSourceDocument(Guid.NewGuid(), 1, "studio:ux6-q:evidence",
                "scripts/ux6-q-main.as", "void main() { say(\"main\"); }\n", "1.0.0",
                ForgeScriptSourceDistributionPolicy.SourceAndBytecode, false, [shared.RelativePath], []);
            var store = new ForgeScriptSourceStore(root);
            store.Save(shared);
            store.Save(main);

            var editing = new StudioSessionController();
            if (!editing.Open(root).Succeeded) return FailedSmokeUx6Q();
            var originalExtensions = editing.Project!.Module.RuntimePolicies.Extensions;
            var draft = editing.CreateCompilerDraftUx6Q(explicitlySelected: true);
            if (draft is null) return FailedSmokeUx6Q();
            var explicitSelection = true;
            var changedParallelism = draft.MaxParallelism < 32 ? draft.MaxParallelism + 1 : draft.MaxParallelism - 1;
            var changed = draft with
            {
                MaxParallelism = changedParallelism,
                ConformanceSuites = draft.ConformanceSuites.Append("ux6-q").Distinct(StringComparer.Ordinal).ToArray(),
            };
            var invalidCompilerBlocked = !editing.PreviewCompilerUx6Q(changed with { MaxParallelism = 0 }).Succeeded;
            var staleCompilerBlocked = !editing.PreviewCompilerUx6Q(changed with
            {
                SourceFingerprint = "stale",
            }).Succeeded;
            var compilerPreview = editing.PreviewCompilerUx6Q(changed);
            var compilation = compilerPreview.Compilation;
            var compilerApplied = editing.ApplyApprovedCompilerUx6Q();
            var compilerUndo = editing.UndoCompilerUx6Q();
            var compilerRedo = editing.RedoCompilerUx6Q();
            var compilerSaved = editing.SaveCompilerUx6Q();
            var reopened = ProjectSession.Open(root);
            var persistedCompiler = reopened.Session?.Project.Module.RuntimePolicies.Extensions.ModernTooling.Compiler;
            var compilerPersistence = compilerSaved.Succeeded && persistedCompiler is not null &&
                                      persistedCompiler.MaxParallelism == changedParallelism &&
                                      persistedCompiler.ConformanceSuites.Contains("ux6-q", StringComparer.Ordinal);
            var compilerApplyUndoRedo = compilerPreview.Succeeded && compilerPreview.CanApply &&
                                        compilerApplied.Succeeded && compilerUndo.Succeeded && compilerRedo.Succeeded;

            var invalidAssistantBlocked = !editing.TryCreateAssistantDraftUx6Q("../escape.as", out _, out _);
            if (!editing.TryCreateAssistantDraftUx6Q(Ux6QDefaultPath, out var assistantDraft, out _) ||
                assistantDraft is null)
                return FailedSmokeUx6Q();
            var proposal = editing.GenerateAssistantProposalUx6Q(assistantDraft);
            var proposalCompiled = proposal.Succeeded && proposal.CanPreview &&
                                   proposal.Diagnostics.All(item => item.Severity != "error");
            var sandboxTests = proposal.Tests.Count >= 3 && proposal.Tests.All(item => item.Contains("réussi", StringComparison.Ordinal));
            var offlineProvider = proposal.Provider == "offline-studio" && proposal.EstimatedCostUsd == 0;
            var unapproved = editing._proposedAssistantUx6Q!;
            var unapprovedCandidate = editing._proposedAssistantCandidateUx6Q!;
            var approvalProbe = new ApplyAssistedCanonicalForgeScriptHandler().Handle(root,
                editing.History!.State.Revision.Value,
                new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), editing.History.State.Revision.Value,
                    unapproved, unapprovedCandidate, AssistantPermissionLevel.ApplyApprovedPlan, true));
            var approvedProbe = unapproved.WithApproval(true, Ux6QProvenance + ":author-approved");
            var permissionProbe = new ApplyAssistedCanonicalForgeScriptHandler().Handle(root,
                editing.History.State.Revision.Value,
                new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), editing.History.State.Revision.Value,
                    approvedProbe, unapprovedCandidate with { Provenance = approvedProbe.Provenance },
                    AssistantPermissionLevel.Configure, true));
            var approvalRequired = approvalProbe.Diagnostics.Contains(
                ApplyAssistedCanonicalForgeScriptHandler.ApprovalRequiredCode);
            var permissionRequired = permissionProbe.Diagnostics.Contains(
                ApplyAssistedCanonicalForgeScriptHandler.PermissionDeniedCode);
            var assistantPreview = editing.PreviewAssistantUx6Q(assistantDraft);
            var staleDraftBlocked = !editing.ApplyApprovedAssistantUx6Q(assistantDraft with
            {
                Message = assistantDraft.Message + " modifiée",
            }).Succeeded;
            editing.PreviewAssistantUx6Q(assistantDraft);
            var sourcePath = AssistedForgeScriptCreationService.ResolvePath(root, assistantDraft.RelativePath);
            File.WriteAllText(sourcePath, "external", Encoding.UTF8);
            var staleDiskBlocked = !editing.ApplyApprovedAssistantUx6Q(assistantDraft).Succeeded;
            File.Delete(sourcePath);
            editing.PreviewAssistantUx6Q(assistantDraft);
            var assistantApplied = editing.ApplyApprovedAssistantUx6Q(assistantDraft);
            var appliedDocument = new ForgeScriptSourceStore(root).Load(assistantDraft.RelativePath);
            var assistantUndo = editing.UndoAssistantUx6Q();
            var removedPair = !File.Exists(sourcePath) && !File.Exists(sourcePath + ".meta.json");
            var assistantRedo = editing.RedoAssistantUx6Q();
            var redoneDocument = new ForgeScriptSourceStore(root).Load(assistantDraft.RelativePath);
            var sourceMetadataRoundTrip = removedPair && appliedDocument.Source == redoneDocument.Source &&
                                          appliedDocument.Id == redoneDocument.Id &&
                                          File.Exists(sourcePath + ".meta.json");
            var assistantApplyUndoRedo = assistantApplied.Succeeded && assistantUndo.Succeeded &&
                                         assistantRedo.Succeeded;
            var currentExtensions = editing.Project!.Module.RuntimePolicies.Extensions;
            var neighbors = ReferenceEquals(currentExtensions.ModernTooling.RuntimeCopy,
                                originalExtensions.ModernTooling.RuntimeCopy) &&
                            ReferenceEquals(currentExtensions.ModernTooling.AreaEffectIndicator,
                                originalExtensions.ModernTooling.AreaEffectIndicator) &&
                            ReferenceEquals(currentExtensions.ModernTooling.UiToolkit,
                                originalExtensions.ModernTooling.UiToolkit) &&
                            ReferenceEquals(currentExtensions.ModernTooling.Documentation,
                                originalExtensions.ModernTooling.Documentation) &&
                            ReferenceEquals(currentExtensions.ModernTooling.QualityAssurance,
                                originalExtensions.ModernTooling.QualityAssurance) &&
                            ReferenceEquals(currentExtensions.OperationalReadiness,
                                originalExtensions.OperationalReadiness) &&
                            ReferenceEquals(currentExtensions.PostLaunch, originalExtensions.PostLaunch) &&
                            currentExtensions.RuleSchemas.SequenceEqual(originalExtensions.RuleSchemas) &&
                            currentExtensions.UiDocuments.SequenceEqual(originalExtensions.UiDocuments) &&
                            currentExtensions.JsonDocuments.SequenceEqual(originalExtensions.JsonDocuments);
            editing.Close();

            var succeeded = explicitSelection && invalidCompilerBlocked && staleCompilerBlocked &&
                            compilerPreview.Succeeded && compilerPreview.CanonicalDryRun &&
                            compilation.Succeeded && compilation.DependencyAware && compilation.CacheReused &&
                            compilation.DependencyInvalidated && compilation.StableDiagnostics &&
                            compilation.Telemetry && compilation.Conformance && compilerApplyUndoRedo &&
                            compilerPersistence && invalidAssistantBlocked && proposalCompiled && sandboxTests &&
                            offlineProvider && approvalRequired && permissionRequired && staleDraftBlocked &&
                            staleDiskBlocked && assistantPreview.CanonicalDryRun && assistantApplyUndoRedo &&
                            sourceMetadataRoundTrip && neighbors;
            return new(succeeded, explicitSelection, compilerPreview.Succeeded, invalidCompilerBlocked,
                compilerPreview.CanonicalDryRun, compilation.DependencyAware, compilation.CacheReused,
                compilation.DependencyInvalidated, compilation.StableDiagnostics, compilation.Telemetry,
                compilation.Conformance, compilerApplyUndoRedo, compilerPersistence, invalidAssistantBlocked,
                proposalCompiled, sandboxTests, offlineProvider, approvalRequired, permissionRequired,
                staleDraftBlocked, staleDiskBlocked, assistantPreview.CanonicalDryRun, assistantApplyUndoRedo,
                sourceMetadataRoundTrip, neighbors, false);
        }
    }

    private static bool TryBuildCompilerCandidateUx6Q(
        ProjectDefinition project,
        StudioUx6QCompilerDraft draft,
        out ModuleRuntimeExtensionProfile? candidate,
        out StudioUx6QDiagnostic? diagnostic)
    {
        candidate = null;
        diagnostic = null;
        try
        {
            if (!TryBuildCompilerOptionsUx6Q(draft, out _, out var optionDiagnostic))
            {
                diagnostic = optionDiagnostic;
                return false;
            }
            var extensions = project.Module.RuntimePolicies.Extensions;
            var modern = extensions.ModernTooling;
            var source = modern.Compiler;
            var compiler = new ForgeScriptCompilerProfile(source.Id, source.SchemaVersion,
                draft.Incremental, draft.StableDiagnostics, draft.PerformanceTelemetry,
                draft.MaxParallelism, draft.MaxSourceBytes, draft.ConformanceSuites, Ux6QProvenance);
            var updatedModern = new ModernRuntimeToolingProfile(modern.Id, modern.SchemaVersion,
                modern.RuntimeCopy, modern.AreaEffectIndicator, modern.UiToolkit, compiler,
                modern.Documentation, Ux6QProvenance, modern.QualityAssurance);
            candidate = new ModuleRuntimeExtensionProfile(extensions.Id, extensions.SchemaVersion,
                extensions.RuleSchemas, extensions.UiDocuments, extensions.JsonDocuments,
                extensions.SnapshotPolicy, Ux6QProvenance, updatedModern,
                extensions.OperationalReadiness, extensions.PostLaunch);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or
                                           InvalidOperationException)
        {
            diagnostic = new("studio.compiler.profile_invalid",
                "module/runtimePolicies/extensions/modernTooling/compiler", "error",
                $"Profil compilateur refusé : {exception.Message}");
            return false;
        }
    }

    private static bool TryBuildCompilerOptionsUx6Q(
        StudioUx6QCompilerDraft draft,
        out ForgeScriptCompilerOptions? options,
        out StudioUx6QDiagnostic? diagnostic)
    {
        options = null;
        diagnostic = null;
        if (draft.MaxParallelism is < 1 or > 32 || draft.MaxSourceBytes is < 1024 or > 4_194_304 ||
            draft.ConformanceSuites.Count == 0 || draft.ConformanceSuites.Any(string.IsNullOrWhiteSpace))
        {
            diagnostic = new("studio.compiler.quotas_invalid",
                "module/runtimePolicies/extensions/modernTooling/compiler", "error",
                "Le parallélisme, le quota source et au moins une suite de conformité valide sont requis.");
            return false;
        }
        options = new(draft.Incremental, draft.StableDiagnostics, draft.PerformanceTelemetry,
            draft.MaxParallelism, draft.MaxSourceBytes,
            draft.ConformanceSuites.Select(item => item.Trim()).Distinct(StringComparer.Ordinal).ToArray());
        return true;
    }

    private static (IReadOnlyList<ForgeScriptCompilationUnit> Units,
        IReadOnlyList<StudioUx6QDiagnostic> Diagnostics) LoadCompilerUnitsUx6Q(string root)
    {
        var scriptsRoot = Path.Combine(root, "scripts");
        if (!Directory.Exists(scriptsRoot)) return ([], []);
        var units = new List<ForgeScriptCompilationUnit>();
        var diagnostics = new List<StudioUx6QDiagnostic>();
        var store = new ForgeScriptSourceStore(root);
        foreach (var path in Directory.EnumerateFiles(scriptsRoot, "*.as", SearchOption.AllDirectories)
                     .OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (!File.Exists(path + ".meta.json"))
            {
                diagnostics.Add(new("script.source.metadata_missing", relative, "error",
                    "La source .as existe sans métadonnées canoniques et reste protégée."));
                continue;
            }
            try
            {
                var document = store.Load(relative);
                units.Add(new(document.RelativePath, document.Source, document.Dependencies));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                               InvalidOperationException or ArgumentException or
                                               System.Text.Json.JsonException)
            {
                diagnostics.Add(new("script.source.load_failed", relative, "error", exception.Message));
            }
        }
        return (units, diagnostics);
    }

    private static IReadOnlyList<string> CompilerChangesUx6Q(
        ForgeScriptCompilerProfile source,
        ForgeScriptCompilerProfile target)
    {
        var changes = new List<string>();
        if (source.Incremental != target.Incremental)
            changes.Add($"Compilation incrémentale · {(target.Incremental ? "activée" : "désactivée")}.");
        if (source.StableDiagnostics != target.StableDiagnostics)
            changes.Add($"Diagnostics stables · {(target.StableDiagnostics ? "activés" : "désactivés")}.");
        if (source.PerformanceTelemetry != target.PerformanceTelemetry)
            changes.Add($"Télémétrie locale · {(target.PerformanceTelemetry ? "activée" : "désactivée")}.");
        if (source.MaxParallelism != target.MaxParallelism)
            changes.Add($"Parallélisme · {source.MaxParallelism} → {target.MaxParallelism} worker(s).");
        if (source.MaxSourceBytes != target.MaxSourceBytes)
            changes.Add($"Quota source · {source.MaxSourceBytes:N0} → {target.MaxSourceBytes:N0} octets.");
        if (!source.ConformanceSuites.SequenceEqual(target.ConformanceSuites))
            changes.Add($"Suites de conformité · {source.ConformanceSuites.Count} → {target.ConformanceSuites.Count}.");
        return changes;
    }

    private static string CompilerCandidateFingerprintUx6Q(ProjectDefinition project,
        StudioUx6QCompilerDraft draft)
    {
        if (!TryBuildCompilerCandidateUx6Q(project, draft, out var candidate, out _) || candidate is null)
            return string.Empty;
        var current = project.Module.RuntimePolicies;
        var policies = new Forge.Domain.RuntimePolicies.ModuleRuntimePolicyProfile(current.Id,
            current.SchemaVersion, current.Heartbeats, current.Rest, current.SequenceAbort,
            "runtime-extension-command", current.Cinematics, current.Gameplay, current.Rules,
            candidate, current.GameplayEvents, current.World, current.LifecycleEvents);
        var candidateProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithRuntimePolicies(policies), project.Blueprints, project.Areas,
            project.Dialogues, project.Quests);
        return ProjectFingerprintUx6Q(candidateProject);
    }

    private bool AssistantDraftCurrentUx6Q(StudioUx6QAssistantDraft draft, out string failure)
    {
        var history = History;
        var project = Project;
        var session = Session;
        if (history is null || project is null || session is null)
        {
            failure = "Aucun module ouvert.";
            return false;
        }
        if (draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.ProjectFingerprint, ProjectFingerprintUx6Q(project), StringComparison.Ordinal))
        {
            failure = "Le projet a changé · rechargez le brouillon assisté.";
            return false;
        }
        if (!string.Equals(draft.DiskFingerprint,
                AssistantDiskFingerprintUx6Q(session.RootDirectory, draft.RelativePath), StringComparison.Ordinal))
        {
            failure = "La source ou ses métadonnées ont changé sur disque.";
            return false;
        }
        failure = string.Empty;
        return true;
    }

    private static StudioUx6QProposalView ProposalViewUx6Q(AssistedForgeScriptProposal proposal) => new(
        proposal.Succeeded, proposal.CanApply,
        proposal.CanApply
            ? "Proposition locale compilée et testée · prévisualisez avant approbation."
            : "La proposition est refusée · corrigez le source ou le brief.",
        proposal.Source, proposal.SemanticDiff, proposal.ForgeScriptApiVersion,
        proposal.VirtualMachineVersion, proposal.ProviderKey, proposal.EstimatedCostUsd,
        proposal.Provenance, proposal.Tests.Select(TestTextUx6Q).ToArray(),
        proposal.Diagnostics.Select(item => new StudioUx6QDiagnostic(item.Source, "scripts/assistant",
            item.Severity, $"{item.Message} · ligne {item.Line}, colonne {item.Column}")).ToArray());

    private static string TestTextUx6Q(AssistedForgeScriptTestEvidence test) =>
        $"{HumanUx6Q(test.Id)} · {(test.Passed ? "réussi" : "refusé")} · {HumanUx6Q(test.Detail)}";

    private static StudioUx6QProposalView FailedProposalUx6Q(string message) => new(false, false,
        message, string.Empty, string.Empty, string.Empty, string.Empty, "offline-studio", 0,
        Ux6QProvenance, [], [new("assistant.script.proposal_failed", "scripts/assistant", "error", message)]);

    private static StudioUx6QAssistantPreview FailedAssistantPreviewUx6Q(string message) => new(false, false,
        false, false, false, message, "no-change", [],
        [new("assistant.script.preview_failed", "scripts/assistant", "error", message)]);

    private static StudioUx6QCompilerPreview FailedCompilerPreviewUx6Q(string message, string code, string path) =>
        new(false, false, false, message, "no-change", [],
            FailedCompilationUx6Q(message, new StudioUx6QDiagnostic(code, path, "error", message)),
            [new(code, path, "error", message)]);

    private static StudioUx6QCompilationEvidence FailedCompilationUx6Q(
        string message,
        params StudioUx6QDiagnostic[] diagnostics) =>
        new(false, false, false, false, false, false, false, false, true,
            0, 0, 0, 0, 0, [], diagnostics, message);

    private StudioUx6QActionResult ActionUx6Q(bool succeeded, string message, bool projectHistory) =>
        new(succeeded, message,
            projectHistory ? History?.CanUndo == true : _assistedHistoryUx6Q.CanUndo,
            projectHistory ? History?.CanRedo == true : _assistedHistoryUx6Q.CanRedo);

    private void CancelCompilerApprovalUx6Q()
    {
        _approvedCompilerUx6Q = null;
        _approvedCompilerRevisionUx6Q = null;
        _approvedCompilerFingerprintUx6Q = null;
    }

    private void CancelAssistantProposalUx6Q(bool keepHistory = false)
    {
        _proposedAssistantUx6Q = null;
        _proposedAssistantCandidateUx6Q = null;
        _proposedAssistantDraftFingerprintUx6Q = null;
        _assistantApprovalUx6Q = null;
        if (!keepHistory) _assistedHistoryUx6Q.Reset();
    }

    private static string AssistantDraftFingerprintUx6Q(StudioUx6QAssistantDraft draft)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(draft.BaseRevision.Value);
            writer.Write(draft.ProjectFingerprint);
            writer.Write(draft.DiskFingerprint);
            writer.Write(draft.NewSource);
            writer.Write(draft.Template.Id.ToByteArray());
            writer.Write(draft.RelativePath);
            writer.Write(draft.EventBinding);
            writer.Write(draft.Intent);
            writer.Write(draft.Message);
            writer.Write(draft.VariableKey);
            writer.Write(draft.VariableValue);
            writer.Write(draft.ContextCitations.Count);
            foreach (var item in draft.ContextCitations) writer.Write(item);
            writer.Write(draft.StyleRules.Count);
            foreach (var item in draft.StyleRules) writer.Write(item);
            writer.Write(draft.AuthorId);
            writer.Write(draft.Seed);
            writer.Write(draft.SourceOverride);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static string AssistantDiskFingerprintUx6Q(string root, string relativePath)
    {
        string sourcePath;
        try
        {
            sourcePath = AssistedForgeScriptCreationService.ResolvePath(root, relativePath);
        }
        catch (ArgumentException)
        {
            return "invalid";
        }
        var metadataPath = sourcePath + ".meta.json";
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            WriteFingerprintFileUx6Q(writer, sourcePath);
            WriteFingerprintFileUx6Q(writer, metadataPath);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteFingerprintFileUx6Q(BinaryWriter writer, string path)
    {
        var exists = File.Exists(path);
        writer.Write(exists);
        if (!exists) return;
        var bytes = File.ReadAllBytes(path);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string ProjectFingerprintUx6Q(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));

    private static string HumanAssistantDiagnosticsUx6Q(IReadOnlyList<string> diagnostics) =>
        diagnostics.Count == 0
            ? "Le plan assisté a été refusé."
            : string.Join(" · ", diagnostics.Select(HumanUx6Q));

    private static string HumanCommandFailureUx6Q(CommandResult result) =>
        result.Diagnostics.Count == 0
            ? "La commande canonique a refusé le profil."
            : string.Join(" · ", result.Diagnostics.Select(item => HumanUx6Q(item.Message)));

    private static string HumanPathUx6Q(string value)
    {
        var file = Path.GetFileNameWithoutExtension(value.Replace('\\', '/'));
        return HumanUx6Q(file);
    }

    private static string HumanUx6Q(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "aucun";
        if (Guid.TryParse(value, out _)) return "identité protégée";
        var normalized = value.Replace('_', ' ').Replace('-', ' ').Replace(':', ' ')
            .Replace('|', ' ').Trim();
        return string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool DeleteTemporaryUx6Q(string root)
    {
        try
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
            return !Directory.Exists(root);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static StudioUx6QSmokeResult FailedSmokeUx6Q() => new(false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false);
}
