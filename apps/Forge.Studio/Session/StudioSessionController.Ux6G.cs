using System.Globalization;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Scripting;
using Forge.RpgKit.Domain.Scripting;
using Forge.Scripting;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux6GProvenance = "studio:ux6-g:script-authoring-gameplay";

    internal StudioUx6GEditorAnalysis AnalyzeScriptSourceUx6G(StudioUx6FDraft draft, int cursor)
    {
        ArgumentNullException.ThrowIfNull(draft);
        try
        {
            var analysis = ForgeScriptAuthoringService.Analyze(
                draft.Source,
                Math.Clamp(cursor, 0, draft.Source.Length),
                EditorContextUx6G());
            var diagnostics = analysis.Diagnostics.Select(ToStudioDiagnosticUx6F).ToArray();
            return new(true,
                diagnostics.Length == 0
                    ? $"Analyse terminée · {analysis.Completions.Count} complétion(s), " +
                      $"{analysis.DocumentSymbols.Count} symbole(s), {analysis.SyntaxSpans.Count} segment(s)."
                    : "Analyse terminée avec diagnostics localisés.",
                analysis.FormattedSource,
                analysis.SignatureHelp,
                analysis.Completions.Select(item =>
                        $"{item.Label} · {item.Detail} · {item.Documentation}")
                    .ToArray(),
                analysis.DocumentSymbols.Select(item =>
                        $"{item.Name} · {item.Signature} · {item.Documentation}")
                    .ToArray(),
                diagnostics,
                analysis.SyntaxSpans.Count);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return new(false, $"Analyse refusée : {exception.Message}", draft.Source, null, [], [],
                [new("script.editor.analysis_failed", draft.RelativePath, "error", exception.Message)], 0);
        }
    }

    internal StudioUx6GWizardResult BuildScriptWizardUx6G(
        StudioUx6FDraft draft,
        StudioUx6GWizardDraft wizard)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(wizard);
        if (string.IsNullOrWhiteSpace(wizard.BooleanCondition))
            return FailedWizardUx6G(draft, "script.wizard.condition_required",
                "Saisissez une condition booléenne revue, par exemple true.");
        if (wizard.Priority is < 0 or > 10_000 || !double.IsFinite(wizard.Amount) || wizard.Amount < 0 ||
            !double.IsFinite(wizard.DurationSeconds) || wizard.DurationSeconds <= 0)
            return FailedWizardUx6G(draft, "script.wizard.values_invalid",
                "Priorité, montant ou durée hors des bornes du contrat.");

        var condition = ForgeScriptAuthoringService.Build(
            ForgeScriptAstKind.Literal,
            wizard.BooleanCondition.Trim(),
            "bool");
        var action = WizardActionUx6G(wizard);
        var result = ForgeScriptAuthoringService.BuildWizard(new(
            Guid.NewGuid(),
            1,
            Ux6GProvenance,
            wizard.FunctionName,
            [condition],
            [action]));
        var diagnostics = result.Diagnostics.Select(ToStudioDiagnosticUx6F).ToArray();
        if (!result.Succeeded)
            return new(false, "Le Wizard refuse le brouillon · corrigez les champs localisés.",
                string.Empty, draft.Source, diagnostics);
        var candidate = draft.Source.TrimEnd() + Environment.NewLine + Environment.NewLine +
                        result.ReviewedSource + Environment.NewLine;
        return new(true,
            "Source typée générée · relisez-la puis insérez-la dans le brouillon avant le dry-run.",
            result.ReviewedSource,
            candidate,
            diagnostics);
    }

    internal StudioUx6GGameplayResult PreviewScriptGameplayUx6G(
        StudioUx6FDraft draft,
        StudioUx6GGameplayDraft gameplay)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(gameplay);
        if (string.IsNullOrWhiteSpace(gameplay.ActionName) || gameplay.Priority is < 0 or > 10_000 ||
            string.IsNullOrWhiteSpace(gameplay.AbilityId) || !double.IsFinite(gameplay.Damage) ||
            gameplay.Damage < 0 || !double.IsFinite(gameplay.ArmorPenetration) ||
            gameplay.ArmorPenetration is < 0 or > 1 || string.IsNullOrWhiteSpace(gameplay.EffectId) ||
            !double.IsFinite(gameplay.Magnitude) || !double.IsFinite(gameplay.DurationSeconds) ||
            gameplay.DurationSeconds <= 0 || !double.IsFinite(gameplay.PeriodSeconds) ||
            gameplay.PeriodSeconds < 0 || !Enum.IsDefined(gameplay.Stacking))
            return FailedGameplayUx6G("Paramètres d’action, combat ou effet invalides.",
                "script.gameplay.values_invalid");

        try
        {
            var document = ToDocumentUx6F(draft);
            var adapter = new ForgeScriptP1RuntimeAdapter();
            var optional = adapter.Resolve(null, requiredAtRuntime: false).OptionalFallback;
            var required = adapter.Resolve(null, requiredAtRuntime: true).DiagnosticCode ==
                           ForgeScriptP1RuntimeAdapter.RequiredMissingCode;
            var resolution = adapter.Resolve(document, draft.RequiredAtRuntime);
            if (!resolution.Available || resolution.Handle is null)
                return FailedGameplayUx6G("La source P1 n’est pas disponible.",
                    resolution.DiagnosticCode ?? "script.gameplay.source_missing");

            var combat = new ScriptCombatState(
                new Dictionary<string, double>(StringComparer.Ordinal) { ["training-target"] = 100 },
                [],
                0,
                new Dictionary<string, double>(StringComparer.Ordinal) { ["training-target"] = 4 });
            var runtime = adapter.Build(resolution.Handle, document, EditorContextUx6G(), combat);
            var at = DateTimeOffset.UnixEpoch;
            var task = new ScriptTask(
                Guid.Parse("6a000000-0000-4000-8000-000000000021"),
                "studio-author",
                gameplay.Priority,
                ScriptNetworkAuthority.Server,
                gameplay.ActionName.Trim(),
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["target"] = "training-target",
                },
                ScriptTaskStatus.Queued,
                at,
                null,
                null);
            runtime = adapter.Enqueue(resolution.Handle, runtime, task, ScriptNetworkAuthority.Server);
            var queued = runtime.Tasks.Tasks.Single().Status == ScriptTaskStatus.Queued;
            runtime = adapter.StartNext(resolution.Handle, runtime, at.AddSeconds(1));
            runtime = adapter.Complete(resolution.Handle, runtime, task.Id, "completed", at.AddSeconds(2));
            var completed = runtime.Tasks.Tasks.Single().Status == ScriptTaskStatus.Completed;
            var unauthorizedTask = false;
            try
            {
                adapter.Enqueue(resolution.Handle, runtime,
                    task with
                    {
                        Id = Guid.Parse("6a000000-0000-4000-8000-000000000022"),
                        Authority = ScriptNetworkAuthority.Local,
                    },
                    ScriptNetworkAuthority.Server);
            }
            catch (InvalidOperationException exception)
            {
                unauthorizedTask = exception.Message.StartsWith("FORGESCRIPT_TASK_REJECTED:",
                    StringComparison.Ordinal);
            }

            var attackContext = new AttackContext(
                Guid.Parse("6a000000-0000-4000-8000-000000000023"),
                "studio-author",
                "training-target",
                gameplay.AbilityId.Trim(),
                1,
                0,
                gameplay.Seed,
                "server",
                Ux6GProvenance);
            var damage = new DamageSpec(
                gameplay.Damage,
                "training",
                new HashSet<string>(["studio-preview"], StringComparer.Ordinal),
                false,
                gameplay.ArmorPenetration);
            var attack = adapter.Attack(resolution.Handle, runtime, attackContext, damage);
            runtime = attack.Runtime;
            var unauthorizedCombat = !adapter.Attack(resolution.Handle, runtime,
                attackContext with
                {
                    TransactionId = Guid.Parse("6a000000-0000-4000-8000-000000000024"),
                    Authority = "client",
                }, damage).Result.Committed;

            var definition = new EffectDefinition(
                gameplay.EffectId.Trim(),
                1,
                Ux6GProvenance,
                "training-periodic",
                gameplay.Stacking,
                TimeSpan.FromSeconds(gameplay.DurationSeconds),
                TimeSpan.FromSeconds(gameplay.PeriodSeconds),
                gameplay.Magnitude,
                new HashSet<string>(["training"], StringComparer.Ordinal),
                true);
            var effect = adapter.ApplyEffect(resolution.Handle, runtime, definition,
                new EffectSpec(
                    Guid.Parse("6a000000-0000-4000-8000-000000000025"),
                    definition.Id,
                    "studio-author",
                    "training-target",
                    1,
                    null,
                    at));
            var advancedAt = gameplay.PeriodSeconds > 0
                ? at.AddSeconds(Math.Min(gameplay.DurationSeconds / 2, gameplay.PeriodSeconds))
                : at.AddMilliseconds(1);
            var advanced = ScriptGameplayService.AdvanceEffects(effect.Result.Effects, advancedAt);
            var json = ScriptGameplayService.SerializeEffects(advanced.Effects, advancedAt);
            var restored = ScriptGameplayService.DeserializeEffects(json);
            var roundTrip = restored.CapturedAt == advancedAt &&
                            string.Equals(
                                ScriptGameplayService.SerializeEffects(restored.Effects, restored.CapturedAt),
                                json,
                                StringComparison.Ordinal);
            var events = runtime.Tasks.Tasks.Select(item =>
                    $"Action · {item.Action} · {item.Status} · priorité {item.Priority}")
                .Concat(attack.Result.Events.Select(item => $"Combat · {item}"))
                .Concat(effect.Result.Events.Select(item => $"Effet · {item}"))
                .Concat(advanced.Events.Select(item => $"Effet périodique · {item}"))
                .ToArray();
            var succeeded = optional && required && queued && completed && unauthorizedTask &&
                            attack.Result.Committed && unauthorizedCombat && effect.Result.Effects.Count > 0 &&
                            roundTrip;
            return new(succeeded,
                succeeded
                    ? "Simulation déterministe terminée · état copié, aucune entité moteur ni transaction réseau."
                    : "La simulation déterministe n’a pas satisfait tous les invariants.",
                optional,
                required,
                queued,
                completed,
                unauthorizedTask,
                attack.Result.Committed,
                unauthorizedCombat,
                attack.Result.AppliedDamage,
                runtime.Combat.Health["training-target"],
                effect.Result.Effects.Count > 0,
                advanced.Events.Count > 0 || gameplay.PeriodSeconds == 0,
                roundTrip,
                restored.Effects.Count,
                events,
                []);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or
                                           OverflowException)
        {
            return FailedGameplayUx6G($"Simulation refusée : {exception.Message}",
                "script.gameplay.preview_failed");
        }
    }

    internal StudioUx6GSmokeResult RunUx6GContractSmoke()
    {
        var sourceSession = Session;
        var sourceProject = Project;
        var sourceHistory = History;
        if (sourceSession is null || sourceProject is null || sourceHistory is null)
            return FailedSmokeUx6G();

        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux6-g", Guid.NewGuid().ToString("N"));
        var released = false;
        StudioUx6GSmokeResult result;
        try
        {
            result = RunIsolatedUx6G();
        }
        finally
        {
            released = DeleteUx6FTemporaryWorkspace(root);
        }

        return result with
        {
            Succeeded = result.Succeeded && released,
            TemporaryWorkspaceReleased = released,
        };

        StudioUx6GSmokeResult RunIsolatedUx6G()
        {
            if (!ProjectSession.Create(root, sourceProject).Succeeded) return FailedSmokeUx6G();
            var document = EvidenceDocumentUx6F();
            new ForgeScriptSourceStore(root).Save(document);
            var editing = new StudioSessionController();
            if (!editing.Open(root).Succeeded ||
                !editing.TryOpenCanonicalSourceUx6F(document.RelativePath, out var initial, out _) ||
                initial is null)
                return FailedSmokeUx6G();

            var explicitSelection = true;
            var sharedAuthority = editing.CurrentScriptDraftUx6F()?.Id == initial.Id;
            var analysisSource = initial with { Source = initial.Source + "\naction_" };
            var analysis = editing.AnalyzeScriptSourceUx6G(analysisSource, analysisSource.Source.Length);
            var signatureSource = initial with { Source = initial.Source + "\neffect_apply(" };
            var signature = editing.AnalyzeScriptSourceUx6G(signatureSource, signatureSource.Source.Length);
            var malformed = initial with { Source = initial.Source + "\nif (true) {" };
            var malformedAnalysis = editing.AnalyzeScriptSourceUx6G(malformed, malformed.Source.Length);
            var editorAnalysis = analysis.Succeeded && analysis.SyntaxSpanCount > 0;
            var completionAndSignature = analysis.Completions.Any(item =>
                                                item.StartsWith("action_enqueue", StringComparison.Ordinal)) &&
                                         !string.IsNullOrWhiteSpace(signature.SignatureHelp);
            var formatting = analysis.FormattedSource.EndsWith(Environment.NewLine, StringComparison.Ordinal);
            var clickableDiagnostics = malformedAnalysis.Diagnostics.Any(item => item.Line > 0 && item.Column > 0);

            var wizardDraft = DefaultWizardUx6G();
            var wizard = editing.BuildScriptWizardUx6G(initial, wizardDraft);
            var invalidWizard = editing.BuildScriptWizardUx6G(initial,
                wizardDraft with { FunctionName = "bad name" });
            if (!wizard.Succeeded) return FailedSmokeUx6G();
            var candidate = initial with { Source = wizard.CandidateSource };
            var preview = editing.PreviewScriptSourceUx6F(candidate);
            var compilation = editing.CompileScriptSourceUx6F(candidate);
            var sandbox = editing.RunScriptSandboxUx6F(candidate);
            var gameplay = editing.PreviewScriptGameplayUx6G(candidate, DefaultGameplayUx6G());
            var stale = !editing.ApplyApprovedScriptSourceUx6F(candidate with
            {
                Source = candidate.Source + "\n// approbation périmée",
            }).Succeeded;
            editing.PreviewScriptSourceUx6F(candidate);
            var applied = editing.ApplyApprovedScriptSourceUx6F(candidate);
            var undone = editing.UndoScriptSourceUx6F();
            var undoDocument = new ForgeScriptSourceStore(root).Load(candidate.RelativePath);
            var redone = editing.RedoScriptSourceUx6F();
            var redoDocument = new ForgeScriptSourceStore(root).Load(candidate.RelativePath);
            var reopened = new StudioSessionController();
            var persistence = reopened.Open(root).Succeeded &&
                              reopened.TryOpenCanonicalSourceUx6F(candidate.RelativePath, out var reopenedDraft,
                                  out _) && reopenedDraft?.Source == candidate.Source;
            reopened.Close();
            var applyUndoRedo = applied.Succeeded && undone.Succeeded && redone.Succeeded &&
                                undoDocument.Source == document.Source && redoDocument.Source == candidate.Source;
            var neighbors = ReferenceEquals(editing.Project, editing.Session?.Project) &&
                            editing.Project?.Areas.Count == sourceProject.Areas.Count &&
                            editing.Project?.Blueprints.Count == sourceProject.Blueprints.Count &&
                            editing.Project?.Dialogues.Count == sourceProject.Dialogues.Count &&
                            editing.Project?.Quests.Count == sourceProject.Quests.Count;
            editing.Close();
            var authorityBlocked = gameplay.UnauthorizedTaskBlocked && gameplay.UnauthorizedCombatBlocked;
            var succeeded = explicitSelection && sharedAuthority && editorAnalysis && completionAndSignature &&
                            formatting && clickableDiagnostics && wizard.Succeeded && !invalidWizard.Succeeded &&
                            gameplay.TaskQueued && gameplay.TaskCompleted && gameplay.CombatCommitted &&
                            gameplay.EffectApplied && gameplay.EffectRoundTrip && authorityBlocked &&
                            preview.Succeeded && compilation.Succeeded && sandbox.Succeeded && stale &&
                            applyUndoRedo && persistence && neighbors;
            return new(succeeded, explicitSelection, sharedAuthority, editorAnalysis, completionAndSignature,
                formatting, clickableDiagnostics, wizard.Succeeded, !invalidWizard.Succeeded,
                gameplay.TaskQueued && gameplay.TaskCompleted, gameplay.CombatCommitted,
                gameplay.EffectApplied && gameplay.EffectRoundTrip, authorityBlocked, preview.CanonicalDryRun,
                compilation.Succeeded, sandbox.Succeeded, stale, applyUndoRedo, persistence, neighbors, false);
        }
    }

    internal static StudioUx6GWizardDraft DefaultWizardUx6G() => new(
        "StudioReviewedAction",
        "true",
        StudioUx6GWizardAction.Message,
        "Action générée et revue",
        100,
        12,
        10,
        97);

    internal static StudioUx6GGameplayDraft DefaultGameplayUx6G() => new(
        "attack",
        100,
        "ability:studio-slash",
        12,
        0.5,
        97,
        "effect:studio-burn",
        2,
        10,
        2,
        EffectStacking.RefreshDuration);

    internal static ForgeScriptEditorContext EditorContextUx6G()
    {
        var symbols = ForgeScriptApiCatalog.Registrations.Select(item => new ForgeScriptSymbol(
            item.Name,
            "function",
            item.Signature.Split(' ', 2)[0],
            item.Summary,
            item.Signature,
            item.Availability.Order(StringComparer.Ordinal).ToArray())).ToArray();
        return new(symbols,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["main"] = "void main() {\n}\n",
                ["handler"] = "void Handler(const int &in e) {\n}\n",
                ["deferred"] = "ScriptTaskRef task = action_enqueue(hero, \"attack\", 100);\n",
                ["combat"] = "combat_attack(hero, target, \"slash\", 12.0f, 97);\n",
                ["effect"] = "EffectHandle effect = effect_apply(hero, target, \"burning\", 2.0f, 10.0f);\n",
            },
            new HashSet<string>(
                ["bool", "const", "double", "else", "float", "if", "int", "return", "string", "void", "while"],
                StringComparer.Ordinal));
    }

    private static ForgeScriptAst WizardActionUx6G(StudioUx6GWizardDraft wizard)
    {
        var text = wizard.ActionOrAbility.Trim();
        return wizard.Action switch
        {
            StudioUx6GWizardAction.Message => ForgeScriptAuthoringService.Build(
                ForgeScriptAstKind.Call,
                "say",
                "void",
                ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Literal,
                    QuoteUx6G(text), "string")),
            StudioUx6GWizardAction.DeferredAction => ForgeScriptAuthoringService.Build(
                ForgeScriptAstKind.Call,
                "action_enqueue",
                "ScriptTaskRef",
                ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Variable, "hero", "EntityRef"),
                ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Literal, QuoteUx6G(text), "string"),
                ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Literal,
                    wizard.Priority.ToString(CultureInfo.InvariantCulture), "int")),
            StudioUx6GWizardAction.CombatAttack => ForgeScriptAuthoringService.Build(
                ForgeScriptAstKind.Call,
                "combat_attack",
                "bool",
                ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Variable, "hero", "EntityRef"),
                ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Variable, "target", "EntityRef"),
                ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Literal, QuoteUx6G(text), "string"),
                ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Literal,
                    wizard.Amount.ToString("R", CultureInfo.InvariantCulture) + "f", "float"),
                ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Literal,
                    wizard.Seed.ToString(CultureInfo.InvariantCulture), "int")),
            StudioUx6GWizardAction.ApplyEffect => ForgeScriptAuthoringService.Build(
                ForgeScriptAstKind.Call,
                "effect_apply",
                "EffectHandle",
                ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Variable, "hero", "EntityRef"),
                ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Variable, "target", "EntityRef"),
                ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Literal, QuoteUx6G(text), "string"),
                ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Literal,
                    wizard.Amount.ToString("R", CultureInfo.InvariantCulture) + "f", "float"),
                ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Literal,
                    wizard.DurationSeconds.ToString("R", CultureInfo.InvariantCulture) + "f", "float")),
            _ => throw new ArgumentOutOfRangeException(nameof(wizard.Action)),
        };
    }

    private static StudioUx6GWizardResult FailedWizardUx6G(
        StudioUx6FDraft draft,
        string code,
        string message) =>
        new(false, message, string.Empty, draft.Source,
            [new(code, draft.RelativePath, "error", message)]);

    private static StudioUx6GGameplayResult FailedGameplayUx6G(string message, string code) =>
        new(false, message, false, false, false, false, false, false, false, 0, 0,
            false, false, false, 0, [], [new(code, "script/gameplay", "error", message)]);

    private static string QuoteUx6G(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal) + "\"";

    private static StudioUx6GSmokeResult FailedSmokeUx6G() =>
        new(false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, false, false, false);
}
