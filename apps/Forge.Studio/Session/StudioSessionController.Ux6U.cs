using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Modules;
using Forge.Application.Projects;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux6UProvenance = "studio:ux6-u:blackboard";
    private StudioUx6UDraft? _approvedBlackboardUx6U;
    private ProjectRevision? _approvedBlackboardRevisionUx6U;
    private string? _approvedBlackboardFingerprintUx6U;

    internal IReadOnlyList<StudioUx6UVariableChoice> BlackboardChoicesUx6U()
    {
        var variables = Project?.Module.Variables ?? [];
        return variables.Select(variable => new StudioUx6UVariableChoice(variable.Id,
                $"{StudioUx0IVariableText.DisplayName(variable)} · {HumanKindUx6U(variable.Kind)} · " +
                HumanScopeUx6U(variable.Scope)))
            .ToArray();
    }

    internal StudioUx6UDraft? CreateBlackboardDraftUx6U(bool explicitlySelected, ProjectVariableId? variableId)
    {
        var project = Project;
        var history = History;
        if (!explicitlySelected || variableId is null || project is null || history is null) return null;
        var variable = project.Module.Variables.FirstOrDefault(item => item.Id == variableId);
        if (variable is null) return null;
        return new StudioUx6UDraft(history.State.Revision, SourceFingerprintUx6U(project), variable.Id,
            variable.DefaultValue, variable.Replication, variable.DebugVisible, variable.RequiredAtRuntime,
            variable.DefaultValue, DefaultAudienceUx6U(variable.Replication), variable.Scope);
    }

    internal ProjectVariableDefinition? FindBlackboardVariableUx6U(ProjectVariableId? variableId) =>
        variableId is null ? null : Project?.Module.Variables.FirstOrDefault(item => item.Id == variableId);

    internal StudioUx6UPreview PreviewBlackboardUx6U(StudioUx6UDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelBlackboardApprovalUx6U();
        var project = Project;
        var history = History;
        if (project is null || history is null)
            return FailedPreviewUx6U("Ouvrez un module et sélectionnez une variable déclarée.",
                "studio.blackboard.selection_required", "module/variables");
        if (draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceFingerprint, SourceFingerprintUx6U(project), StringComparison.Ordinal))
            return FailedPreviewUx6U("Le projet a changé · rechargez la variable avant de continuer.",
                "studio.blackboard.source_changed", "module/variables");
        if (!TryBuildVariablesUx6U(project, draft, out var variables, out var selected, out var diagnostic))
            return FailedPreviewUx6U(diagnostic!.Message, diagnostic.Code, diagnostic.Path);

        var command = new SetModuleConfigurationCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            project.Module.Metadata, variables!, DryRun: true);
        var dryRun = _setModuleConfiguration.Handle(history.State, command);
        if (!dryRun.Succeeded)
        {
            var failure = dryRun.Diagnostics.FirstOrDefault();
            return FailedPreviewUx6U(HumanCommandFailureUx6U(failure?.Code),
                failure?.Code ?? "studio.blackboard.command_refused",
                $"module/variables/{draft.VariableId}");
        }

        var runtime = ExerciseBlackboardUx6U(variables!, selected!, draft);
        if (!runtime.Succeeded)
            return new StudioUx6UPreview(false, false, true, runtime.Message, [], runtime.Diagnostics,
                runtime.Lines, runtime.TypedMutation, runtime.DebugSnapshot, runtime.ReplicationDelta,
                runtime.SaveScopeRoundTrip, runtime.StaleHandleRejected, true);

        var expected = BuildProjectUx6U(project, variables!);
        var changed = !string.Equals(ProjectJsonSerializer.Serialize(project),
            ProjectJsonSerializer.Serialize(expected), StringComparison.Ordinal);
        var source = project.Module.Variables.Single(item => item.Id == draft.VariableId);
        var diff = BuildDiffUx6U(source, selected!).ToArray();
        if (changed)
        {
            _approvedBlackboardUx6U = draft;
            _approvedBlackboardRevisionUx6U = history.State.Revision;
            _approvedBlackboardFingerprintUx6U = CandidateFingerprintUx6U(project, draft);
        }
        return new StudioUx6UPreview(true, changed, true,
            changed
                ? "Aperçu sec canonique validé · la définition et le blackboard source restent inchangés."
                : "Aperçu sec validé · la définition persistante est déjà identique.",
            diff, [], runtime.Lines, runtime.TypedMutation, runtime.DebugSnapshot,
            runtime.ReplicationDelta, runtime.SaveScopeRoundTrip, runtime.StaleHandleRejected, true);
    }

    internal StudioUx6UActionResult ApplyApprovedBlackboardUx6U()
    {
        var draft = _approvedBlackboardUx6U;
        var project = Project;
        var history = History;
        if (draft is null || project is null || history is null ||
            _approvedBlackboardRevisionUx6U != history.State.Revision ||
            draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceFingerprint, SourceFingerprintUx6U(project), StringComparison.Ordinal) ||
            !string.Equals(_approvedBlackboardFingerprintUx6U,
                CandidateFingerprintUx6U(project, draft), StringComparison.Ordinal) ||
            !TryBuildVariablesUx6U(project, draft, out var variables, out _, out _))
        {
            CancelBlackboardApprovalUx6U();
            return new(false, "Le projet ou le brouillon a changé · relancez l’aperçu sec canonique.");
        }

        var command = new SetModuleConfigurationCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            project.Module.Metadata, variables!, DryRun: true);
        var dryRun = _setModuleConfiguration.Handle(history.State, command);
        if (!dryRun.Succeeded)
        {
            CancelBlackboardApprovalUx6U();
            return new(false, HumanCommandFailureUx6U(dryRun.Diagnostics.FirstOrDefault()?.Code));
        }
        var applied = _setModuleConfiguration.Handle(history.State, command with { DryRun = false });
        CancelBlackboardApprovalUx6U();
        if (!applied.Succeeded)
            return new(false, HumanCommandFailureUx6U(applied.Diagnostics.FirstOrDefault()?.Code));
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true, "Définition appliquée · le blackboard runtime de démonstration n’est pas persisté.");
    }

    internal StudioUx6UActionResult UndoBlackboardUx6U()
    {
        CancelBlackboardApprovalUx6U();
        var result = Undo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx6UActionResult RedoBlackboardUx6U()
    {
        CancelBlackboardApprovalUx6U();
        var result = Redo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx6UActionResult SaveBlackboardUx6U()
    {
        var result = Save();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx6UApprovalSnapshot CaptureBlackboardApprovalUx6U() =>
        new(_approvedBlackboardUx6U, _approvedBlackboardRevisionUx6U, _approvedBlackboardFingerprintUx6U);

    internal void RestoreBlackboardApprovalUx6U(StudioUx6UApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedBlackboardUx6U = snapshot.Draft;
        _approvedBlackboardRevisionUx6U = snapshot.Revision;
        _approvedBlackboardFingerprintUx6U = snapshot.Fingerprint;
    }

    internal void CancelBlackboardApprovalUx6U()
    {
        _approvedBlackboardUx6U = null;
        _approvedBlackboardRevisionUx6U = null;
        _approvedBlackboardFingerprintUx6U = null;
    }

    internal void CloseUx6U() => CancelBlackboardApprovalUx6U();

    internal static StudioUx6UDraft ChangedBlackboardDraftUx6U(StudioUx6UDraft source) => source with
    {
        DefaultValue = source.DefaultValue == "24" ? "25" : "24",
        Replication = ProjectVariableReplicationMode.AllClients,
        DebugVisible = true,
        RequiredAtRuntime = true,
        RuntimeValue = "31",
        Audience = BlackboardReplicationAudience.Owner,
        SnapshotScope = ProjectVariableScope.Player,
    };

    internal static ProjectDefinition CreateEvidenceProjectUx6U(ProjectDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var variables = source.Module.Variables.ToList();
        var additions = new[]
        {
            EvidenceVariableUx6U(source, "studio.blackboard.health", ProjectVariableValueKind.Integer,
                ProjectVariableScope.Player, "12", ProjectVariableReplicationMode.Owner, true, true),
            EvidenceVariableUx6U(source, "studio.blackboard.quest-ready", ProjectVariableValueKind.Boolean,
                ProjectVariableScope.Party, "false", ProjectVariableReplicationMode.Party, true, false),
            EvidenceVariableUx6U(source, "studio.blackboard.world-hour", ProjectVariableValueKind.Number,
                ProjectVariableScope.Save, "12.5", ProjectVariableReplicationMode.AllClients, true, false),
            EvidenceVariableUx6U(source, "studio.blackboard.server-secret", ProjectVariableValueKind.String,
                ProjectVariableScope.Server, "secret", ProjectVariableReplicationMode.ServerAuthority, false, true),
            EvidenceVariableUx6U(source, "studio.blackboard.session-note", ProjectVariableValueKind.String,
                ProjectVariableScope.Session, "temporary", ProjectVariableReplicationMode.None, false, false),
        };
        foreach (var addition in additions)
            if (variables.All(item => !string.Equals(item.Key, addition.Key, StringComparison.Ordinal)))
                variables.Add(addition);
        return BuildProjectUx6U(source, variables);
    }

    internal StudioUx6USmokeResult RunUx6UContractSmoke()
    {
        var real = Project;
        if (real is null) return FailedSmokeUx6U();
        var realReadable = ProjectVariableValidator.Validate(real.Module.Variables).Count == 0;
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux6-u-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        var cleanup = false;
        try
        {
            var evidence = CreateEvidenceProjectUx6U(real);
            var created = ProjectSession.Create(root, evidence);
            if (!created.Succeeded || !editing.Open(root).Succeeded) return FailedSmokeUx6U();
            var health = editing.Project!.Module.Variables.Single(item =>
                item.Key == "studio.blackboard.health");
            var explicitSelection = editing.CreateBlackboardDraftUx6U(false, health.Id) is null &&
                                    editing.CreateBlackboardDraftUx6U(true, null) is null &&
                                    editing.CreateBlackboardDraftUx6U(true, health.Id) is not null;
            var baseline = editing.CreateBlackboardDraftUx6U(true, health.Id)!;
            var typedValidation = !editing.PreviewBlackboardUx6U(baseline with
            {
                RuntimeValue = "beaucoup",
            }).Succeeded;
            var schemaValidation = !editing.PreviewBlackboardUx6U(baseline with
            {
                Replication = ProjectVariableReplicationMode.ServerAuthority,
            }).Succeeded;

            var history = editing.History!;
            var beforeProject = editing.Project;
            var beforeRevision = history.State.Revision;
            var beforeJournal = history.Journal.ToArray();
            var changed = ChangedBlackboardDraftUx6U(baseline);
            var preview = editing.PreviewBlackboardUx6U(changed);
            var canonicalDryRun = preview is { Succeeded: true, CanApply: true, CanonicalDryRun: true } &&
                                  ReferenceEquals(beforeProject, editing.Project) &&
                                  history.State.Revision == beforeRevision &&
                                  history.Journal.SequenceEqual(beforeJournal) && !editing.IsDirty;

            var legacy = ProjectBlackboardScriptService.ForRuntime([]);
            var strict = ProjectBlackboardScriptService.ForRuntime(editing.Project.Module.Variables);
            var legacyBoundary = legacy.UsesLegacyCompatibility && !strict.UsesLegacyCompatibility &&
                                 legacy.TrySet("legacy.quest.stage", "2", out _) &&
                                 !strict.TrySet("legacy.quest.stage", "2", out var strictDiagnostic) &&
                                 strictDiagnostic?.Contains("not declared", StringComparison.Ordinal) == true;

            var external = editing._setModuleConfiguration.Handle(history.State,
                new SetModuleConfigurationCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                    editing.Project.Module.Metadata, editing.Project.Module.Variables, DryRun: false));
            if (!external.Succeeded) return FailedSmokeUx6U();
            history.Record(external);
            var staleSource = editing.ApplyApprovedBlackboardUx6U();
            var staleSourceBlocked = !staleSource.Succeeded && editing.Undo().Succeeded;

            var tamper = ChangedBlackboardDraftUx6U(editing.CreateBlackboardDraftUx6U(true, health.Id)!);
            var tamperPreview = editing.PreviewBlackboardUx6U(tamper);
            var approval = editing.CaptureBlackboardApprovalUx6U();
            editing.RestoreBlackboardApprovalUx6U(approval with
            {
                Draft = approval.Draft! with { RuntimeValue = approval.Draft.RuntimeValue + "0" },
            });
            var staleApproval = editing.ApplyApprovedBlackboardUx6U();
            var staleApprovalBlocked = tamperPreview.Succeeded && !staleApproval.Succeeded;

            var sourceProject = editing.Project!;
            var freshHealth = sourceProject.Module.Variables.Single(item => item.Key == health.Key);
            var fresh = ChangedBlackboardDraftUx6U(editing.CreateBlackboardDraftUx6U(true, freshHealth.Id)!);
            if (!TryBuildVariablesUx6U(sourceProject, fresh, out var expectedVariables, out _, out _) ||
                expectedVariables is null)
                return FailedSmokeUx6U();
            var freshPreview = editing.PreviewBlackboardUx6U(fresh);
            var applied = editing.ApplyApprovedBlackboardUx6U();
            var finalProject = editing.Project!;
            var canonicalCommand = freshPreview.Succeeded && applied.Succeeded &&
                                   editing.History!.Journal.Last().CommandName == "set_module_configuration";
            var sourceModule = sourceProject.Module;
            var finalModule = finalProject.Module;
            var moduleNeighbors = ReferenceEquals(sourceModule.Metadata, finalModule.Metadata) &&
                                  ReferenceEquals(sourceModule.Distribution, finalModule.Distribution) &&
                                  ReferenceEquals(sourceModule.Localization, finalModule.Localization) &&
                                  ReferenceEquals(sourceModule.Content, finalModule.Content) &&
                                  ReferenceEquals(sourceModule.ArtisticStyle, finalModule.ArtisticStyle) &&
                                  ReferenceEquals(sourceModule.RuntimePolicies, finalModule.RuntimePolicies) &&
                                  ReferenceEquals(sourceModule.Factions, finalModule.Factions) &&
                                  ReferenceEquals(sourceModule.WorldFeatures, finalModule.WorldFeatures);
            var expectedProject = BuildProjectUx6U(sourceProject, expectedVariables);
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
            var human = !ContainsUuidUx6U(string.Join(' ', editing.BlackboardChoicesUx6U()
                    .Select(item => item.Label)) + " " + string.Join(' ', freshPreview.Diff) + " " +
                string.Join(' ', freshPreview.RuntimePreview));
            var bounded = freshPreview.RuntimeClaimsBounded && freshPreview.RuntimePreview.Any(item =>
                item.Contains("Aucun serveur", StringComparison.Ordinal));

            editing.Close();
            reopened.Close();
            cleanup = DeleteTemporaryUx6U(root);
            var succeeded = explicitSelection && realReadable && typedValidation && schemaValidation &&
                            canonicalDryRun && freshPreview.TypedMutation && freshPreview.DebugSnapshot &&
                            freshPreview.ReplicationDelta && freshPreview.SaveScopeRoundTrip &&
                            freshPreview.StaleHandleRejected && legacyBoundary && staleSourceBlocked &&
                            staleApprovalBlocked && canonicalCommand && undone && redone && persistence &&
                            moduleNeighbors && projectNeighbors && human && bounded && cleanup;
            return new(succeeded, explicitSelection, realReadable, typedValidation, schemaValidation,
                canonicalDryRun, freshPreview.TypedMutation, freshPreview.DebugSnapshot,
                freshPreview.ReplicationDelta, freshPreview.SaveScopeRoundTrip,
                freshPreview.StaleHandleRejected, legacyBoundary, staleSourceBlocked,
                staleApprovalBlocked, canonicalCommand, undone && redone, persistence, moduleNeighbors,
                projectNeighbors, human, bounded, cleanup);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_U_CONTRACT_FAILED " +
                              $"type={exception.GetType().Name} message={exception.Message}");
            return FailedSmokeUx6U();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            cleanup = DeleteTemporaryUx6U(root);
            if (!cleanup) Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_U_CLEANUP_FAILED");
        }
    }

    private static bool TryBuildVariablesUx6U(ProjectDefinition project, StudioUx6UDraft draft,
        out IReadOnlyList<ProjectVariableDefinition>? variables, out ProjectVariableDefinition? selected,
        out StudioUx6UDiagnostic? diagnostic)
    {
        variables = null;
        selected = null;
        diagnostic = null;
        var source = project.Module.Variables.FirstOrDefault(item => item.Id == draft.VariableId);
        if (source is null)
        {
            diagnostic = new("studio.blackboard.variable_missing", "module/variables",
                "La variable sélectionnée n’existe plus dans le module.");
            return false;
        }
        try
        {
            selected = new ProjectVariableDefinition(source.Id, source.Key, source.Kind, source.Scope,
                draft.DefaultValue, source.Description, source.Provenance, source.SchemaVersion,
                draft.Replication, draft.DebugVisible, draft.RequiredAtRuntime);
        }
        catch (ArgumentException exception)
        {
            diagnostic = new("studio.blackboard.value_invalid", $"module/variables/{source.Id}/defaultValue",
                HumanValueFailureUx6U(source.Kind, exception.Message));
            return false;
        }
        var updated = selected;
        variables = project.Module.Variables.Select(item => item.Id == source.Id ? updated : item).ToArray();
        var validation = ProjectVariableValidator.Validate(variables).FirstOrDefault();
        if (validation is not null)
        {
            diagnostic = new(validation.Code, validation.Path, HumanCommandFailureUx6U(validation.Code));
            return false;
        }
        return true;
    }

    private static RuntimeExerciseUx6U ExerciseBlackboardUx6U(
        IReadOnlyList<ProjectVariableDefinition> variables,
        ProjectVariableDefinition selected,
        StudioUx6UDraft draft)
    {
        var service = ProjectBlackboardScriptService.ForRuntime(variables);
        var handle = service.Query().Single(item => item.Id == selected.Id);
        var mutation = service.Set(handle, draft.RuntimeValue);
        if (!mutation.Succeeded)
            return RuntimeExerciseUx6U.Failed(HumanValueFailureUx6U(selected.Kind,
                mutation.Diagnostic ?? "Valeur refusée."),
                new StudioUx6UDiagnostic("studio.blackboard.runtime_value_invalid",
                    $"module/variables/{selected.Id}/runtimeValue",
                    HumanValueFailureUx6U(selected.Kind, mutation.Diagnostic ?? "Valeur refusée.")));

        var debug = service.DebugSnapshot();
        var delta = service.ReplicationDelta(draft.Audience, [selected.Id]);
        var save = service.Snapshot(draft.SnapshotScope);
        var restored = ProjectBlackboardScriptService.ForRuntime(variables);
        var loadDiagnostics = restored.Load(save);
        var roundTrip = loadDiagnostics.Count == 0 && save.All(item =>
            restored.TryGetValue(item.Key, out var value) && value == item.Value);
        var staleRejected = false;
        try
        {
            service.Read(handle with { Key = handle.Key + ".ancienne" });
        }
        catch (KeyNotFoundException)
        {
            staleRejected = true;
        }
        var debugReady = debug.Variables.Count + debug.HiddenVariableCount == variables.Count;
        var lines = new[]
        {
            $"Handle typé · {StudioUx0IVariableText.DisplayName(selected)} · {HumanKindUx6U(selected.Kind)} · révision {mutation.After!.Revision}",
            $"Mutation en mémoire · {HumanValueUx6U(mutation.Before!.Value)} → {HumanValueUx6U(mutation.After.Value)} · normalisation validée",
            $"Vue debug · {debug.Variables.Count} visible(s) · {debug.HiddenVariableCount} protégée(s)",
            $"Delta {HumanAudienceUx6U(draft.Audience)} · {delta.Values.Count} valeur(s) · politique {HumanReplicationUx6U(selected.Replication)}",
            $"Sauvegarde {HumanScopeUx6U(draft.SnapshotScope)} · {save.Count} valeur(s) · rechargement {(roundTrip ? "validé" : "refusé")}",
            $"Compatibilité · schéma déclaré strict · handle périmé {(staleRejected ? "refusé" : "accepté")}",
            "Aucun serveur, client, GameRoot ou transport réseau n’est exécuté · simulation locale copiée seulement.",
        };
        return new(true, "Blackboard typé simulé sans muter le runtime actif.", [], lines,
            mutation.Succeeded, debugReady, delta.Revision == mutation.After.Revision, roundTrip,
            staleRejected);
    }

    private static IEnumerable<string> BuildDiffUx6U(ProjectVariableDefinition source,
        ProjectVariableDefinition candidate)
    {
        if (source.DefaultValue != candidate.DefaultValue)
            yield return $"Valeur par défaut · {HumanValueUx6U(source.DefaultValue)} → {HumanValueUx6U(candidate.DefaultValue)}";
        if (source.Replication != candidate.Replication)
            yield return $"Réplication · {HumanReplicationUx6U(source.Replication)} → {HumanReplicationUx6U(candidate.Replication)}";
        if (source.DebugVisible != candidate.DebugVisible)
            yield return $"Vue debug · {(candidate.DebugVisible ? "visible" : "protégée")}";
        if (source.RequiredAtRuntime != candidate.RequiredAtRuntime)
            yield return $"Runtime · {(candidate.RequiredAtRuntime ? "requise" : "optionnelle")}";
    }

    private static ProjectDefinition BuildProjectUx6U(ProjectDefinition source,
        IReadOnlyList<ProjectVariableDefinition> variables)
    {
        var module = source.Module;
        var updated = new ModuleDefinition(module.Id, module.Slug, module.DisplayName, module.Description,
            module.EntryAreaId, module.Metadata, variables, module.Distribution, module.Localization,
            module.Content, module.ArtisticStyle, module.RuntimePolicies, module.Factions, module.WorldFeatures);
        return new ProjectDefinition(source.Id, source.Title, updated, source.Blueprints, source.Areas,
            source.Dialogues, source.Quests);
    }

    private static ProjectVariableDefinition EvidenceVariableUx6U(ProjectDefinition source, string key,
        ProjectVariableValueKind kind, ProjectVariableScope scope, string value,
        ProjectVariableReplicationMode replication, bool debugVisible, bool required) => new(
        ProjectVariableId.From(StableIdUx6U(source.Module.Id.Value, key)), key, kind, scope, value,
        "Variable de preuve UX6-U", Ux6UProvenance, ProjectVariableDefinition.CurrentSchemaVersion,
        replication, debugVisible, required);

    private static Guid StableIdUx6U(Guid owner, string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{owner:D}|ux6-u|{key}"));
        Span<byte> value = stackalloc byte[16];
        bytes.AsSpan(0, value.Length).CopyTo(value);
        value[6] = (byte)((value[6] & 0x0f) | 0x40);
        value[8] = (byte)((value[8] & 0x3f) | 0x80);
        return new Guid(value);
    }

    private static StudioUx6UPreview FailedPreviewUx6U(string message, string code, string path) =>
        new(false, false, false, message, [], [new(code, path, message)], [],
            false, false, false, false, false, true);

    private static string SourceFingerprintUx6U(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));

    private static string CandidateFingerprintUx6U(ProjectDefinition project, StudioUx6UDraft draft)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            WriteTextUx6U(writer, "ux6-u-draft-v1");
            writer.Write(draft.BaseRevision.Value);
            WriteTextUx6U(writer, draft.SourceFingerprint);
            writer.Write(draft.VariableId.Value.ToByteArray());
            WriteTextUx6U(writer, draft.DefaultValue);
            writer.Write((int)draft.Replication);
            writer.Write(draft.DebugVisible);
            writer.Write(draft.RequiredAtRuntime);
            WriteTextUx6U(writer, draft.RuntimeValue);
            writer.Write((int)draft.Audience);
            writer.Write((int)draft.SnapshotScope);
            WriteTextUx6U(writer, ProjectJsonSerializer.Serialize(project));
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteTextUx6U(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static BlackboardReplicationAudience DefaultAudienceUx6U(ProjectVariableReplicationMode mode) =>
        mode switch
        {
            ProjectVariableReplicationMode.ServerAuthority => BlackboardReplicationAudience.Server,
            ProjectVariableReplicationMode.Party => BlackboardReplicationAudience.Party,
            ProjectVariableReplicationMode.AllClients => BlackboardReplicationAudience.Client,
            _ => BlackboardReplicationAudience.Owner,
        };

    private static string HumanKindUx6U(ProjectVariableValueKind value) => value switch
    {
        ProjectVariableValueKind.String => "Texte",
        ProjectVariableValueKind.Boolean => "Oui / non",
        ProjectVariableValueKind.Integer => "Entier",
        ProjectVariableValueKind.Number => "Nombre",
        _ => "Type inconnu",
    };

    private static string HumanScopeUx6U(ProjectVariableScope value) => value switch
    {
        ProjectVariableScope.Session => "Session",
        ProjectVariableScope.Save => "Sauvegarde",
        ProjectVariableScope.Player => "Joueur",
        ProjectVariableScope.Party => "Groupe",
        ProjectVariableScope.Server => "Serveur",
        _ => "Portée inconnue",
    };

    private static string HumanReplicationUx6U(ProjectVariableReplicationMode value) => value switch
    {
        ProjectVariableReplicationMode.None => "Aucune",
        ProjectVariableReplicationMode.Owner => "Propriétaire",
        ProjectVariableReplicationMode.Party => "Groupe",
        ProjectVariableReplicationMode.AllClients => "Tous les clients",
        ProjectVariableReplicationMode.ServerAuthority => "Autorité serveur",
        _ => "Politique inconnue",
    };

    private static string HumanAudienceUx6U(BlackboardReplicationAudience value) => value switch
    {
        BlackboardReplicationAudience.Owner => "propriétaire",
        BlackboardReplicationAudience.Party => "groupe",
        BlackboardReplicationAudience.Client => "client",
        BlackboardReplicationAudience.Server => "serveur",
        _ => "inconnue",
    };

    private static string HumanValueUx6U(string value) =>
        string.IsNullOrEmpty(value) ? "valeur vide" : value.Length > 32 ? value[..29] + "…" : value;

    private static string HumanValueFailureUx6U(ProjectVariableValueKind kind, string _) =>
        kind switch
        {
            ProjectVariableValueKind.Boolean => "Saisissez oui/non sous la forme true ou false.",
            ProjectVariableValueKind.Integer => "Saisissez un nombre entier sans unité.",
            ProjectVariableValueKind.Number => "Saisissez un nombre fini avec un point décimal si nécessaire.",
            _ => "La valeur dépasse la limite autorisée ou n’est pas valide.",
        };

    private static string HumanCommandFailureUx6U(string? code) => code switch
    {
        ProjectVariableValidator.RequiredSessionCode =>
            "Une variable requise doit survivre à la session : choisissez une autre portée dans l’éditeur Projet.",
        ProjectVariableValidator.ServerReplicationCode =>
            "L’autorité serveur exige une variable de portée Serveur.",
        SetModuleConfigurationHandler.RevisionConflictCode =>
            "Le projet a changé · rechargez la variable.",
        _ => "La commande canonique a refusé la définition de variable.",
    };

    private static bool ContainsUuidUx6U(string value) => System.Text.RegularExpressions.Regex.IsMatch(value,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b");

    internal static bool DeleteTemporaryUx6U(string root)
    {
        if (string.IsNullOrWhiteSpace(root)) return true;
        try
        {
            var full = Path.GetFullPath(root);
            var temporary = Path.GetFullPath(Path.GetTempPath());
            if (!full.StartsWith(temporary, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux6-u-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static StudioUx6USmokeResult FailedSmokeUx6U() => new(false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false);

    private sealed record RuntimeExerciseUx6U(
        bool Succeeded,
        string Message,
        IReadOnlyList<StudioUx6UDiagnostic> Diagnostics,
        IReadOnlyList<string> Lines,
        bool TypedMutation,
        bool DebugSnapshot,
        bool ReplicationDelta,
        bool SaveScopeRoundTrip,
        bool StaleHandleRejected)
    {
        internal static RuntimeExerciseUx6U Failed(string message, StudioUx6UDiagnostic diagnostic) =>
            new(false, message, [diagnostic], [], false, false, false, false, false);
    }
}
