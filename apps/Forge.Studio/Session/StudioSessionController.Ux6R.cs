using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.RuntimeExtensions;
using Forge.Application.Time;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.RuntimePolicies;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Scripting;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux6RProvenance = "studio:ux6-r:runtime-data-ui";
    private readonly SetRuntimeExtensionsHandler _setRuntimeExtensionsUx6R = new(new SystemClock());
    private StudioUx6RDraft? _approvedRuntimeDataUx6R;
    private ProjectRevision? _approvedRuntimeDataRevisionUx6R;
    private string? _approvedRuntimeDataFingerprintUx6R;

    internal IReadOnlyList<StudioUx6RProfileChoice> RuntimeDataChoicesUx6R()
    {
        var project = Project;
        if (project is null) return [];
        var extensions = project.Module.RuntimePolicies.Extensions;
        return [new($"{HumanUx6R(project.Module.DisplayName)} · {extensions.RuleSchemas.Count} règle(s) · " +
                    $"{extensions.UiDocuments.Count} interface(s) · {extensions.JsonDocuments.Count} document(s) JSON")];
    }

    internal StudioUx6RDraft? CreateRuntimeDataDraftUx6R(bool explicitlySelected)
    {
        var project = Project;
        var history = History;
        if (!explicitlySelected || project is null || history is null) return null;
        var extensions = project.Module.RuntimePolicies.Extensions;
        return new StudioUx6RDraft(history.State.Revision, SourceFingerprintUx6R(project), extensions,
            extensions.RuleSchemas.Select(item => new StudioUx6RRuleDraft(item.Id, item.Key,
                item.Kind.ToString(), item.JsonSchema, item.DefaultsJson, item.Required, item.Provenance)).ToArray(),
            extensions.UiDocuments.Select(item => new StudioUx6RUiDocumentDraft(item.Id, item.Key, item.Title,
                item.ThemeKey, item.Required,
                item.Widgets.Select(widget => new StudioUx6RWidgetDraft(widget.Id, widget.Key,
                    widget.Kind.ToString(), widget.ParentKey ?? string.Empty, widget.Text,
                    widget.BindingKey ?? string.Empty, widget.Interactive, widget.FocusOrder,
                    widget.Permission)).ToArray(),
                item.EventBindings.Select(binding => new StudioUx6REventDraft(binding.Id,
                    binding.WidgetKey, binding.Event.ToString(), binding.Command, binding.InputContext,
                    binding.Permission)).ToArray(), item.Provenance)).ToArray(),
            extensions.JsonDocuments.Select(item => new StudioUx6RJsonDocumentDraft(item.Id, item.Key,
                item.JsonSchema, item.InitialValue, item.MaxBytes, item.MaxDepth, item.Mutable,
                item.Provenance)).ToArray(),
            new StudioUx6RSnapshotDraft(extensions.SnapshotPolicy.Id,
                extensions.SnapshotPolicy.Components.ToArray(), extensions.SnapshotPolicy.MaxReferences,
                extensions.SnapshotPolicy.MaxInventoryItems, extensions.SnapshotPolicy.MaxBlackboardBytes,
                extensions.SnapshotPolicy.RestoreReferences, extensions.SnapshotPolicy.Provenance));
    }

    internal StudioUx6RPreview PreviewRuntimeDataUx6R(StudioUx6RDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelRuntimeDataApprovalUx6R();
        var history = History;
        var project = Project;
        if (history is null || project is null)
            return FailedPreviewUx6R("Ouvrez un module et sélectionnez son profil runtime.",
                "studio.runtime_data.selection_required", "module/runtimePolicies/extensions");
        if (draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceFingerprint, SourceFingerprintUx6R(project), StringComparison.Ordinal))
            return FailedPreviewUx6R("Le projet a changé · rechargez les données runtime.",
                "studio.runtime_data.source_changed", "module/runtimePolicies/extensions");
        if (!TryBuildRuntimeDataCandidateUx6R(project, draft, out var candidate, out var diagnostic))
            return FailedPreviewUx6R(diagnostic!.Message, diagnostic.Code, diagnostic.Path);

        var command = new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            candidate!, DryRun: true);
        var dryRun = _setRuntimeExtensionsUx6R.Handle(history.State, command);
        if (!dryRun.Succeeded)
            return FailedPreviewUx6R(HumanCommandFailureUx6R(dryRun),
                dryRun.Diagnostics.FirstOrDefault()?.Code ?? "studio.runtime_data.command_refused",
                "module/runtimePolicies/extensions");

        var runtime = ExerciseRuntimeDataUx6R(candidate!);
        if (!runtime.Succeeded)
            return new StudioUx6RPreview(false, false, true, runtime.Message,
                "Remplacement atomique des règles, interfaces, documents JSON et quotas de sauvegarde.",
                [], runtime.Diagnostics, runtime.Lines,
                runtime.RuleResolved, runtime.UiEventPublished, runtime.JsonMutationSucceeded,
                runtime.SnapshotPolicyReady, true);

        var changed = !string.Equals(ProjectJsonSerializer.Serialize(project),
            ProjectJsonSerializer.Serialize(BuildProjectUx6R(project, candidate!)), StringComparison.Ordinal);
        var diff = RuntimeDataDiffUx6R(project.Module.RuntimePolicies.Extensions, candidate!);
        if (changed)
        {
            _approvedRuntimeDataUx6R = draft;
            _approvedRuntimeDataRevisionUx6R = history.State.Revision;
            _approvedRuntimeDataFingerprintUx6R = CandidateFingerprintUx6R(project, draft);
        }
        return new StudioUx6RPreview(true, changed, true,
            changed
                ? "Aperçu sec canonique validé · le projet et les données runtime restent inchangés."
                : "Aperçu sec validé · le profil runtime est déjà identique.",
            "Remplacement atomique des règles, interfaces, documents JSON et quotas de sauvegarde.",
            diff, [], runtime.Lines,
            runtime.RuleResolved, runtime.UiEventPublished, runtime.JsonMutationSucceeded,
            runtime.SnapshotPolicyReady, true);
    }

    internal StudioUx6RActionResult ApplyApprovedRuntimeDataUx6R()
    {
        var draft = _approvedRuntimeDataUx6R;
        var project = Project;
        var history = History;
        if (draft is null || project is null || history is null ||
            _approvedRuntimeDataRevisionUx6R != history.State.Revision ||
            draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceFingerprint, SourceFingerprintUx6R(project), StringComparison.Ordinal) ||
            !string.Equals(_approvedRuntimeDataFingerprintUx6R,
                CandidateFingerprintUx6R(project, draft), StringComparison.Ordinal) ||
            !TryBuildRuntimeDataCandidateUx6R(project, draft, out var candidate, out _))
        {
            CancelRuntimeDataApprovalUx6R();
            return new(false, "Le projet ou le brouillon a changé · relancez l’aperçu sec.");
        }

        var command = new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            candidate!, DryRun: true);
        var dryRun = _setRuntimeExtensionsUx6R.Handle(history.State, command);
        if (!dryRun.Succeeded)
        {
            CancelRuntimeDataApprovalUx6R();
            return new(false, HumanCommandFailureUx6R(dryRun));
        }
        var applied = _setRuntimeExtensionsUx6R.Handle(history.State, command with { DryRun = false });
        CancelRuntimeDataApprovalUx6R();
        if (!applied.Succeeded) return new(false, HumanCommandFailureUx6R(applied));
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true, "Données runtime appliquées · règles, UI, JSON et snapshots sont enregistrés.");
    }

    internal StudioUx6RActionResult UndoRuntimeDataUx6R()
    {
        CancelRuntimeDataApprovalUx6R();
        var result = Undo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx6RActionResult RedoRuntimeDataUx6R()
    {
        CancelRuntimeDataApprovalUx6R();
        var result = Redo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx6RActionResult SaveRuntimeDataUx6R()
    {
        var result = Save();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx6RApprovalSnapshot CaptureRuntimeDataApprovalUx6R() =>
        new(_approvedRuntimeDataUx6R, _approvedRuntimeDataRevisionUx6R,
            _approvedRuntimeDataFingerprintUx6R);

    internal void RestoreRuntimeDataApprovalUx6R(StudioUx6RApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedRuntimeDataUx6R = snapshot.Draft;
        _approvedRuntimeDataRevisionUx6R = snapshot.Revision;
        _approvedRuntimeDataFingerprintUx6R = snapshot.Fingerprint;
    }

    internal void CancelRuntimeDataApprovalUx6R()
    {
        _approvedRuntimeDataUx6R = null;
        _approvedRuntimeDataRevisionUx6R = null;
        _approvedRuntimeDataFingerprintUx6R = null;
    }

    internal static Guid StableIdUx6R(Guid owner, string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{owner:D}|ux6-r|{key.Trim().ToLowerInvariant()}"));
        Span<byte> value = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(value);
        value[6] = (byte)((value[6] & 0x0F) | 0x40);
        value[8] = (byte)((value[8] & 0x3F) | 0x80);
        return new Guid(value);
    }

    internal static ProjectDefinition CreateEvidenceProjectUx6R(ProjectDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var moduleId = source.Module.Id.Value;
        var extensions = source.Module.RuntimePolicies.Extensions;
        var rule = new DataDrivenRuleSchema(StableIdUx6R(moduleId, "rule:combat.stance"), 1,
            "combat.stance", RuleSchemaKind.Class,
            "{\"type\":\"object\",\"required\":[\"stance\"]}",
            "{\"stance\":\"guarded\",\"bonus\":2}", true, Ux6RProvenance + ":evidence");
        var panel = new ScriptableUiWidget(StableIdUx6R(moduleId, "widget:runtime.hud:root"),
            "root", ScriptableUiWidgetKind.Panel, null, string.Empty, null, false, 0, "player");
        var status = new ScriptableUiWidget(StableIdUx6R(moduleId, "widget:runtime.hud:status"),
            "status", ScriptableUiWidgetKind.Label, "root", "État de l’aventure", "state:/status",
            false, 1, "player");
        var journal = new ScriptableUiWidget(StableIdUx6R(moduleId, "widget:runtime.hud:journal"),
            "journal", ScriptableUiWidgetKind.Button, "root", "Ouvrir le journal", null,
            true, 2, "player");
        var binding = new ScriptableUiEventBinding(StableIdUx6R(moduleId, "event:runtime.hud:journal"),
            "journal", ScriptableUiEventKind.Pressed, "hud.toggle-journal", "1.0", "gameplay", "player");
        var ui = new ScriptableUiDocument(StableIdUx6R(moduleId, "ui:runtime.hud"), 1,
            "runtime.hud", "HUD d’aventure", "forge-dark", true, [panel, status, journal], [binding],
            Ux6RProvenance + ":evidence");
        var json = new RuntimeJsonDocumentDefinition(StableIdUx6R(moduleId, "json:runtime.session"), 1,
            "runtime.session", "{\"type\":\"object\",\"required\":[\"status\"]}",
            "{\"status\":\"ready\",\"count\":2}", 16_384, 16, true, "1.0",
            Ux6RProvenance + ":evidence");
        var snapshot = new EntityStateSnapshotPolicy(extensions.SnapshotPolicy.Id, 1,
            ["transform", "combat", "inventory", "blackboard"], 256, 512, 65_536, true,
            extensions.SnapshotPolicy.Provenance);
        var candidate = new ModuleRuntimeExtensionProfile(extensions.Id, extensions.SchemaVersion,
            [rule], [ui], [json], snapshot, Ux6RProvenance + ":evidence", extensions.ModernTooling,
            extensions.OperationalReadiness, extensions.PostLaunch);
        return BuildProjectUx6R(source, candidate);
    }

    internal StudioUx6RSmokeResult RunUx6RContractSmoke()
    {
        var real = Project;
        if (real is null) return FailedSmokeUx6R();
        var realExtensions = real.Module.RuntimePolicies.Extensions;
        var realReadable = realExtensions.Id == real.Module.Id.Value &&
                           realExtensions.SnapshotPolicy.Id != Guid.Empty;
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux6-r-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        var cleanup = false;
        try
        {
            var evidence = CreateEvidenceProjectUx6R(real);
            var created = ProjectSession.Create(root, evidence);
            if (!created.Succeeded || !editing.Open(root).Succeeded) return FailedSmokeUx6R();
            var explicitSelection = editing.CreateRuntimeDataDraftUx6R(false) is null &&
                                    editing.CreateRuntimeDataDraftUx6R(true) is not null;
            var baseline = editing.CreateRuntimeDataDraftUx6R(true)!;
            var invalidRule = baseline with
            {
                Rules = [baseline.Rules[0] with { JsonSchema = "[]" }],
            };
            var ruleValidation = !editing.PreviewRuntimeDataUx6R(invalidRule).Succeeded;
            var invalidUi = baseline with
            {
                UiDocuments = [baseline.UiDocuments[0] with
                {
                    Events = [baseline.UiDocuments[0].Events[0] with { WidgetKey = "missing" }],
                }],
            };
            var uiValidation = !editing.PreviewRuntimeDataUx6R(invalidUi).Succeeded;
            var invalidJson = baseline with
            {
                JsonDocuments = [baseline.JsonDocuments[0] with { MaxBytes = 16 }],
            };
            var jsonValidation = !editing.PreviewRuntimeDataUx6R(invalidJson).Succeeded;
            var invalidSnapshot = baseline with
            {
                Snapshot = baseline.Snapshot with { MaxReferences = -1 },
            };
            var snapshotValidation = !editing.PreviewRuntimeDataUx6R(invalidSnapshot).Succeeded;

            var history = editing.History!;
            var beforeProject = editing.Project;
            var beforeRevision = history.State.Revision;
            var beforeJournal = history.Journal.ToArray();
            var changed = ChangedDraftUx6R(baseline);
            var preview = editing.PreviewRuntimeDataUx6R(changed);
            var canonicalDryRun = preview is { Succeeded: true, CanApply: true, CanonicalDryRun: true } &&
                                  ReferenceEquals(beforeProject, editing.Project) &&
                                  history.State.Revision == beforeRevision &&
                                  history.Journal.SequenceEqual(beforeJournal) && !editing.IsDirty;

            var externalDraft = changed with
            {
                Snapshot = changed.Snapshot with { MaxInventoryItems = changed.Snapshot.MaxInventoryItems + 1 },
            };
            if (!TryBuildRuntimeDataCandidateUx6R(editing.Project!, externalDraft,
                    out var externalCandidate, out _) || externalCandidate is null)
                return FailedSmokeUx6R();
            var external = editing._setRuntimeExtensionsUx6R.Handle(history.State,
                new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                    externalCandidate, DryRun: false));
            if (!external.Succeeded) return FailedSmokeUx6R();
            history.Record(external);
            var staleSource = editing.ApplyApprovedRuntimeDataUx6R();
            var staleSourceBlocked = !staleSource.Succeeded && editing.Undo().Succeeded;

            var tamperDraft = ChangedDraftUx6R(editing.CreateRuntimeDataDraftUx6R(true)!);
            var tamperPreview = editing.PreviewRuntimeDataUx6R(tamperDraft);
            var approval = editing.CaptureRuntimeDataApprovalUx6R();
            editing.RestoreRuntimeDataApprovalUx6R(approval with
            {
                Draft = approval.Draft! with
                {
                    Snapshot = approval.Draft.Snapshot with
                    {
                        MaxBlackboardBytes = approval.Draft.Snapshot.MaxBlackboardBytes + 1,
                    },
                },
            });
            var staleApproval = editing.ApplyApprovedRuntimeDataUx6R();
            var staleApprovalBlocked = tamperPreview.Succeeded && !staleApproval.Succeeded;

            var sourceProject = editing.Project!;
            var sourceExtensions = sourceProject.Module.RuntimePolicies.Extensions;
            var fresh = ChangedDraftUx6R(editing.CreateRuntimeDataDraftUx6R(true)!);
            if (!TryBuildRuntimeDataCandidateUx6R(sourceProject, fresh,
                    out var expectedExtensions, out _) || expectedExtensions is null)
                return FailedSmokeUx6R();
            var freshPreview = editing.PreviewRuntimeDataUx6R(fresh);
            var applied = editing.ApplyApprovedRuntimeDataUx6R();
            var finalProject = editing.Project!;
            var finalExtensions = finalProject.Module.RuntimePolicies.Extensions;
            var canonicalCommand = freshPreview.Succeeded && applied.Succeeded &&
                                   editing.History!.Journal.Last().CommandName == "set_runtime_extensions";
            var neighborProfiles = ReferenceEquals(sourceExtensions.ModernTooling,
                                       finalExtensions.ModernTooling) &&
                                   ReferenceEquals(sourceExtensions.OperationalReadiness,
                                       finalExtensions.OperationalReadiness) &&
                                   ReferenceEquals(sourceExtensions.PostLaunch, finalExtensions.PostLaunch);
            var stableIdentities = sourceExtensions.RuleSchemas.All(before =>
                                       finalExtensions.RuleSchemas.Any(after => after.Id == before.Id &&
                                           after.Key == before.Key)) &&
                                   sourceExtensions.UiDocuments.All(before =>
                                       finalExtensions.UiDocuments.Any(after => after.Id == before.Id &&
                                           after.Key == before.Key)) &&
                                   sourceExtensions.JsonDocuments.All(before =>
                                       finalExtensions.JsonDocuments.Any(after => after.Id == before.Id &&
                                           after.Key == before.Key)) &&
                                   sourceExtensions.SnapshotPolicy.Id == finalExtensions.SnapshotPolicy.Id;
            var expectedProject = BuildProjectUx6R(sourceProject, expectedExtensions);
            var projectNeighbors = string.Equals(ProjectJsonSerializer.Serialize(expectedProject),
                ProjectJsonSerializer.Serialize(finalProject), StringComparison.Ordinal);
            var finalJson = ProjectJsonSerializer.Serialize(finalProject);
            var undo = editing.Undo();
            var undone = undo.Succeeded && ReferenceEquals(sourceProject, editing.Project);
            var redo = editing.Redo();
            var redone = redo.Succeeded && ReferenceEquals(finalProject, editing.Project);
            var persistence = editing.Save().Succeeded && reopened.Open(root).Succeeded &&
                              reopened.Project is not null && string.Equals(finalJson,
                                  ProjectJsonSerializer.Serialize(reopened.Project), StringComparison.Ordinal);
            var human = !ContainsUuidUx6R(string.Join(' ', editing.RuntimeDataChoicesUx6R()
                                .Select(item => item.Label)) + " " + string.Join(' ', freshPreview.Diff) + " " +
                            string.Join(' ', freshPreview.RuntimePreview));
            var bounded = freshPreview.RuntimeClaimsBounded && freshPreview.RuntimePreview.Any(item =>
                item.Contains("Aucun nœud Godot", StringComparison.Ordinal));

            editing.Close();
            reopened.Close();
            cleanup = DeleteTemporaryUx6R(root);
            var succeeded = explicitSelection && realReadable && ruleValidation && uiValidation &&
                            jsonValidation && snapshotValidation && canonicalDryRun &&
                            freshPreview.RuleResolved && freshPreview.UiEventPublished &&
                            freshPreview.JsonMutationSucceeded && freshPreview.SnapshotPolicyReady &&
                            staleSourceBlocked && staleApprovalBlocked && canonicalCommand && undone && redone &&
                            persistence && neighborProfiles && stableIdentities && projectNeighbors && human &&
                            bounded && cleanup;
            return new(succeeded, explicitSelection, realReadable, ruleValidation, uiValidation,
                jsonValidation, snapshotValidation, canonicalDryRun, freshPreview.RuleResolved,
                freshPreview.UiEventPublished, freshPreview.JsonMutationSucceeded,
                freshPreview.SnapshotPolicyReady, staleSourceBlocked, staleApprovalBlocked,
                canonicalCommand, undone && redone, persistence, neighborProfiles, stableIdentities,
                projectNeighbors, human, bounded, cleanup);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_R_CONTRACT_FAILED " +
                              $"type={exception.GetType().Name} message={exception.Message}");
            return FailedSmokeUx6R();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            cleanup = DeleteTemporaryUx6R(root);
            if (!cleanup) Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_R_CLEANUP_FAILED");
        }
    }

    internal static StudioUx6RDraft ChangedDraftUx6R(StudioUx6RDraft source)
    {
        var rules = source.Rules.Select((item, index) => index == 0
            ? item with { DefaultsJson = "{\"stance\":\"aggressive\",\"bonus\":3}" }
            : item).ToArray();
        var ui = source.UiDocuments.Select((item, index) => index == 0
            ? item with { Title = "HUD runtime validé" }
            : item).ToArray();
        var json = source.JsonDocuments.Select((item, index) => index == 0
            ? item with { InitialValue = "{\"status\":\"active\",\"count\":3}" }
            : item).ToArray();
        return source with
        {
            Rules = rules,
            UiDocuments = ui,
            JsonDocuments = json,
            Snapshot = source.Snapshot with
            {
                MaxReferences = source.Snapshot.MaxReferences == 4096
                    ? 4095
                    : source.Snapshot.MaxReferences + 1,
            },
        };
    }

    private static bool TryBuildRuntimeDataCandidateUx6R(ProjectDefinition project, StudioUx6RDraft draft,
        out ModuleRuntimeExtensionProfile? candidate, out StudioUx6RDiagnostic? diagnostic)
    {
        candidate = null;
        diagnostic = null;
        try
        {
            if (draft.Rules.Select(item => item.Key.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
                draft.Rules.Count)
                throw new InvalidOperationException("Les clés de règles doivent être uniques.");
            if (draft.UiDocuments.Select(item => item.Key.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
                draft.UiDocuments.Count)
                throw new InvalidOperationException("Les clés d’interfaces doivent être uniques.");
            if (draft.JsonDocuments.Select(item => item.Key.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
                draft.JsonDocuments.Count)
                throw new InvalidOperationException("Les clés JSON doivent être uniques.");

            var rules = draft.Rules.Select(item =>
            {
                if (!Enum.TryParse<RuleSchemaKind>(item.Kind, true, out var kind) || !Enum.IsDefined(kind))
                    throw new InvalidOperationException($"Type de règle inconnu : {item.Kind}.");
                return new DataDrivenRuleSchema(item.Id, 1, item.Key, kind, item.JsonSchema,
                    item.DefaultsJson, item.Required, item.Provenance);
            }).ToArray();
            var ui = draft.UiDocuments.Select(document =>
            {
                var widgets = document.Widgets.Select(widget =>
                {
                    if (!Enum.TryParse<ScriptableUiWidgetKind>(widget.Kind, true, out var kind) ||
                        !Enum.IsDefined(kind))
                        throw new InvalidOperationException($"Widget inconnu : {widget.Kind}.");
                    return new ScriptableUiWidget(widget.Id, widget.Key, kind,
                        string.IsNullOrWhiteSpace(widget.ParentKey) ? null : widget.ParentKey,
                        widget.Text, string.IsNullOrWhiteSpace(widget.BindingKey) ? null : widget.BindingKey,
                        widget.Interactive, widget.FocusOrder, widget.Permission);
                }).ToArray();
                var events = document.Events.Select(binding =>
                {
                    if (!Enum.TryParse<ScriptableUiEventKind>(binding.Event, true, out var kind) ||
                        !Enum.IsDefined(kind))
                        throw new InvalidOperationException($"Événement UI inconnu : {binding.Event}.");
                    return new ScriptableUiEventBinding(binding.Id, binding.WidgetKey, kind,
                        binding.Command, "1.0", binding.InputContext, binding.Permission);
                }).ToArray();
                return new ScriptableUiDocument(document.Id, 1, document.Key, document.Title,
                    document.ThemeKey, document.Required, widgets, events, document.Provenance);
            }).ToArray();
            var json = draft.JsonDocuments.Select(item => new RuntimeJsonDocumentDefinition(item.Id, 1,
                item.Key, item.JsonSchema, item.InitialValue, item.MaxBytes, item.MaxDepth, item.Mutable,
                "1.0", item.Provenance)).ToArray();
            var snapshot = new EntityStateSnapshotPolicy(draft.Snapshot.Id, 1, draft.Snapshot.Components,
                draft.Snapshot.MaxReferences, draft.Snapshot.MaxInventoryItems,
                draft.Snapshot.MaxBlackboardBytes, draft.Snapshot.RestoreReferences,
                draft.Snapshot.Provenance);
            var source = draft.SourceExtensions;
            if (source.Id != project.Module.Id.Value)
                throw new InvalidOperationException("Le profil runtime ne correspond pas au module ouvert.");
            candidate = new ModuleRuntimeExtensionProfile(source.Id, source.SchemaVersion, rules, ui, json,
                snapshot, Ux6RProvenance, source.ModernTooling, source.OperationalReadiness, source.PostLaunch);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           JsonException or OverflowException)
        {
            diagnostic = new StudioUx6RDiagnostic("studio.runtime_data.profile_invalid",
                "module/runtimePolicies/extensions", exception.Message);
            return false;
        }
    }

    private static RuntimeExerciseUx6R ExerciseRuntimeDataUx6R(ModuleRuntimeExtensionProfile profile)
    {
        var diagnostics = new List<StudioUx6RDiagnostic>();
        var lines = new List<string>();
        try
        {
            var ruleResolved = profile.RuleSchemas.All(rule =>
                RuntimeExtensionRuntimeService.GetRule(profile, rule.Key, rule.Required, out _) is not null);
            lines.Add(profile.RuleSchemas.Count == 0
                ? "Règles · état vide explicite."
                : $"Règles · {profile.RuleSchemas.Count} handle(s) typé(s) résolu(s) sans recompilation moteur.");

            var uiHandles = RuntimeExtensionRuntimeService.GetUiDocuments(profile);
            var required = RuntimeExtensionRuntimeService.ValidateRequired(profile);
            foreach (var item in required)
                diagnostics.Add(new StudioUx6RDiagnostic(item.Code, item.Path, item.Message));
            var uiEventPublished = required.Count == 0;
            var firstBinding = profile.UiDocuments.SelectMany(document => document.EventBindings.Select(binding =>
                (Document: document, Binding: binding))).FirstOrDefault();
            if (firstBinding.Document is not null)
            {
                var service = new ForgeScriptUiEventService();
                var handle = new ScriptUiHandle(firstBinding.Document.Id, firstBinding.Document.Key);
                var binding = firstBinding.Binding;
                service.Register(new ScriptUiBinding(binding.Id, handle, binding.WidgetKey,
                    binding.Event.ToString(), binding.Command, binding.ForgeScriptApiVersion,
                    binding.InputContext, binding.Permission));
                using var payload = JsonDocument.Parse("{\"value\":true}");
                var published = service.Publish(handle, binding.WidgetKey, binding.Event.ToString(),
                    payload.RootElement, binding.InputContext, binding.Permission);
                uiEventPublished &= published.BindingId == binding.Id && published.Sequence == 1;
            }
            lines.Add(profile.UiDocuments.Count == 0
                ? "Interface scriptable · état vide explicite."
                : $"Interface scriptable · {uiHandles.Count} document(s), événement typé publié en mémoire.");

            var jsonHandles = RuntimeExtensionRuntimeService.GetJsonDocuments(profile);
            var jsonMutation = true;
            foreach (var definition in profile.JsonDocuments)
            {
                var service = new ForgeScriptJsonService();
                var snapshot = service.Register(definition.Id, definition.Key, definition.JsonSchema,
                    definition.InitialValue, new SafeJsonQuota(definition.MaxBytes, definition.MaxDepth),
                    definition.Mutable);
                if (!definition.Mutable) continue;
                using var initial = JsonDocument.Parse(definition.InitialValue);
                if (initial.RootElement.ValueKind != JsonValueKind.Object ||
                    !initial.RootElement.EnumerateObject().Any()) continue;
                var property = initial.RootElement.EnumerateObject().First();
                var mutation = service.Set(snapshot.Handle, "/" + EscapePointerUx6R(property.Name),
                    property.Value.Clone(), snapshot.Revision, "module-author");
                jsonMutation &= mutation.Succeeded && mutation.Snapshot.Revision == snapshot.Revision + 1;
            }
            lines.Add(profile.JsonDocuments.Count == 0
                ? "JSON runtime · état vide explicite."
                : $"JSON runtime · {jsonHandles.Count} handle(s), quotas et révision exercés en mémoire.");

            var snapshotReady = profile.SnapshotPolicy.Id != Guid.Empty &&
                                profile.SnapshotPolicy.MaxReferences is >= 0 and <= 4096 &&
                                profile.SnapshotPolicy.MaxInventoryItems is >= 0 and <= 4096 &&
                                profile.SnapshotPolicy.MaxBlackboardBytes is >= 64 and <= 1_048_576;
            lines.Add($"État sérialisable · {profile.SnapshotPolicy.Components.Count} composant(s), " +
                      $"{profile.SnapshotPolicy.MaxReferences} référence(s), " +
                      $"{profile.SnapshotPolicy.MaxInventoryItems} objet(s) inventaire.");
            lines.Add("Aucun nœud Godot, acteur, sauvegarde produit ou bus runtime n’est muté par cet aperçu.");
            var succeeded = ruleResolved && uiEventPublished && jsonMutation && snapshotReady && diagnostics.Count == 0;
            return new RuntimeExerciseUx6R(succeeded, ruleResolved, uiEventPublished, jsonMutation,
                snapshotReady, lines, diagnostics,
                succeeded ? "Services runtime exercés sur des copies en mémoire." :
                    "La simulation runtime a refusé le profil.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or UnauthorizedAccessException or
                                           JsonException or ArgumentException)
        {
            diagnostics.Add(new StudioUx6RDiagnostic("studio.runtime_data.preview_failed",
                "module/runtimePolicies/extensions", exception.Message));
            return new RuntimeExerciseUx6R(false, false, false, false, false, lines, diagnostics,
                "La simulation runtime a signalé une configuration invalide.");
        }
    }

    private static IReadOnlyList<string> RuntimeDataDiffUx6R(ModuleRuntimeExtensionProfile source,
        ModuleRuntimeExtensionProfile target)
    {
        var changes = new List<string>();
        if (source.RuleSchemas.Count != target.RuleSchemas.Count ||
            !source.RuleSchemas.Select(item => item.Key).SequenceEqual(target.RuleSchemas.Select(item => item.Key)))
            changes.Add($"Règles data-driven · {source.RuleSchemas.Count} → {target.RuleSchemas.Count}.");
        else if (source.RuleSchemas.Where((item, index) => item != target.RuleSchemas[index]).Any())
            changes.Add($"Règles data-driven · {target.RuleSchemas.Count} définition(s) mise(s) à jour.");
        if (source.UiDocuments.Count != target.UiDocuments.Count ||
            source.UiDocuments.Where((item, index) => index >= target.UiDocuments.Count ||
                item != target.UiDocuments[index]).Any())
            changes.Add($"Interface scriptable · {source.UiDocuments.Count} → {target.UiDocuments.Count} document(s).");
        if (source.JsonDocuments.Count != target.JsonDocuments.Count ||
            source.JsonDocuments.Where((item, index) => index >= target.JsonDocuments.Count ||
                item != target.JsonDocuments[index]).Any())
            changes.Add($"JSON runtime · {source.JsonDocuments.Count} → {target.JsonDocuments.Count} document(s).");
        if (source.SnapshotPolicy != target.SnapshotPolicy)
            changes.Add("Politique d’état sérialisable · composants, quotas ou restauration modifiés.");
        return changes.Count == 0 ? ["Aucune modification authorée."] : changes;
    }

    private static StudioUx6RPreview FailedPreviewUx6R(string message, string code, string path) =>
        new(false, false, false, message, "Aucun patch.", [],
            [new StudioUx6RDiagnostic(code, path, message)], [], false, false, false, false, true);

    private static ProjectDefinition BuildProjectUx6R(ProjectDefinition source,
        ModuleRuntimeExtensionProfile candidate)
    {
        var current = source.Module.RuntimePolicies;
        var policies = new ModuleRuntimePolicyProfile(current.Id, current.SchemaVersion, current.Heartbeats,
            current.Rest, current.SequenceAbort, "runtime-extension-command", current.Cinematics,
            current.Gameplay, current.Rules, candidate, current.GameplayEvents, current.World,
            current.LifecycleEvents);
        return new ProjectDefinition(source.Id, source.Title, source.Module.WithRuntimePolicies(policies),
            source.Blueprints, source.Areas, source.Dialogues, source.Quests);
    }

    private static string CandidateFingerprintUx6R(ProjectDefinition project, StudioUx6RDraft draft)
    {
        if (!TryBuildRuntimeDataCandidateUx6R(project, draft, out var candidate, out _) || candidate is null)
            return string.Empty;
        return HashUx6R(ProjectJsonSerializer.Serialize(BuildProjectUx6R(project, candidate)));
    }

    private static string SourceFingerprintUx6R(ProjectDefinition project) =>
        HashUx6R(ProjectJsonSerializer.Serialize(project));

    private static string HashUx6R(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string HumanCommandFailureUx6R(CommandResult result) =>
        result.Diagnostics.FirstOrDefault()?.Message ?? "La commande runtime a été refusée.";

    private static string HumanUx6R(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "module";
        if (Guid.TryParse(value, out _)) return "identité protégée";
        return string.Join(' ', value.Trim().Replace('_', ' ').Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string EscapePointerUx6R(string value) =>
        value.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    private static bool ContainsUuidUx6R(string value) =>
        System.Text.RegularExpressions.Regex.IsMatch(value,
            @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b");

    private static bool DeleteTemporaryUx6R(string root)
    {
        try
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            return !Directory.Exists(root);
        }
        catch
        {
            return false;
        }
    }

    private static StudioUx6RSmokeResult FailedSmokeUx6R() =>
        new(false, false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, false, false, false);

    private void CloseUx6R() => CancelRuntimeDataApprovalUx6R();

    private sealed record RuntimeExerciseUx6R(
        bool Succeeded,
        bool RuleResolved,
        bool UiEventPublished,
        bool JsonMutationSucceeded,
        bool SnapshotPolicyReady,
        IReadOnlyList<string> Lines,
        IReadOnlyList<StudioUx6RDiagnostic> Diagnostics,
        string Message);
}
