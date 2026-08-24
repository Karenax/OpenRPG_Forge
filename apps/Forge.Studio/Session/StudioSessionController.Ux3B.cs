using System.Security.Cryptography;
using System.Text;
using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly UpdateBlueprintFromEntityHandler _updateBlueprintUx3B = new(new SystemClock());
    private readonly ConfigureBlueprintPrefabHandler _configurePrefabUx3B = new(new SystemClock());

    private StudioUx3BUpdateDraft? _approvedUpdateDraftUx3B;
    private ProjectRevision? _approvedUpdateRevisionUx3B;
    private string? _approvedUpdateProjectFingerprintUx3B;
    private string? _approvedUpdateDraftFingerprintUx3B;
    private StudioUx3BPrefabDraft? _approvedPrefabDraftUx3B;
    private ProjectRevision? _approvedPrefabRevisionUx3B;
    private string? _approvedPrefabProjectFingerprintUx3B;
    private string? _approvedPrefabDraftFingerprintUx3B;

    internal IReadOnlyList<StudioUx3BBlueprintChoice> BlueprintChoicesUx3B
    {
        get
        {
            var project = Project;
            if (project is null) return [];
            var index = BlueprintLibraryIndex.Build(project).Entries.ToDictionary(item => item.BlueprintId);
            return project.Blueprints.Select(blueprint =>
                {
                    var entry = index[blueprint.Id];
                    return new StudioUx3BBlueprintChoice(blueprint.Id.Value,
                        $"{blueprint.DisplayName} · {HumanBlueprintKindUx3A(blueprint.Kind)} · " +
                        $"{HumanOriginUx3A(blueprint.PaletteOrigin)} · {entry.InstanceCount} instance(s)",
                        blueprint.Kind, blueprint.PaletteOrigin, blueprint.Authoring.Version,
                        entry.InstanceCount, blueprint.IsReadOnly(), blueprint.Authoring.PrefabVariant is not null,
                        blueprint.Authoring.PrefabComposition is not null);
                })
                .OrderBy(item => item.Origin)
                .ThenBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    internal IReadOnlyList<StudioUx3BInstanceChoice> InstanceChoicesUx3B(Guid blueprintId)
    {
        var project = Project;
        if (project is null) return [];
        var id = BlueprintId.From(blueprintId);
        var blueprint = project.Blueprints.SingleOrDefault(item => item.Id == id);
        if (blueprint is null) return [];
        var index = 0;
        return project.Areas.SelectMany(area => area.Entities.Where(entity => entity.BlueprintId == id)
                .Select(entity => new StudioUx3BInstanceChoice(area.Id.Value, entity.Id.Value, blueprintId,
                    $"{blueprint.DisplayName} · {area.DisplayName} · instance {++index} · " +
                    $"{entity.Overrides.Properties.Count} surcharge(s) · contrainte {entity.BlueprintVersionRequirement}",
                    entity.BlueprintVersionRequirement, entity.Overrides.Properties.ToArray())))
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    internal StudioUx3BUpdateDraft? CreateUpdateDraftUx3B(Guid blueprintId, Guid entityId)
    {
        var project = Project;
        var history = History;
        var blueprint = project?.Blueprints.SingleOrDefault(item => item.Id.Value == blueprintId);
        var located = project?.Areas.SelectMany(area => area.Entities.Select(entity => (area, entity)))
            .SingleOrDefault(item => item.entity.Id.Value == entityId);
        if (project is null || history is null || blueprint is null || located?.entity is null ||
            located.Value.entity.BlueprintId != blueprint.Id)
            return null;
        return new StudioUx3BUpdateDraft(history.State.Revision, ProjectFingerprintUx3A(project), blueprintId,
            located.Value.area.Id.Value, entityId, [], BlueprintVersionBump.Minor, [],
            blueprint.Authoring.CategoryPaths.ToArray(), false);
    }

    internal StudioUx3BPrefabDraft? CreatePrefabDraftUx3B(Guid blueprintId)
    {
        var project = Project;
        var history = History;
        var blueprint = project?.Blueprints.SingleOrDefault(item => item.Id.Value == blueprintId);
        if (project is null || history is null || blueprint is null) return null;
        var variant = blueprint.Authoring.PrefabVariant;
        var composition = blueprint.Authoring.PrefabComposition;
        var baseBlueprint = variant?.BaseBlueprintId.Value ?? project.Blueprints
            .FirstOrDefault(item => item.Id != blueprint.Id && item.Kind == blueprint.Kind)?.Id.Value;
        return new StudioUx3BPrefabDraft(history.State.Revision, ProjectFingerprintUx3A(project), blueprintId,
            variant is not null,
            variant?.Id ?? StableGuidUx3A(blueprintId, "ux3-b-variant"),
            variant?.Provenance ?? "studio:ux3-b",
            baseBlueprint,
            variant?.Overrides.Select(item => new StudioUx3BVariantOverrideDraft(item.Kind, item.Key, item.Value))
                .ToArray() ?? [],
            variant?.RequiredAtRuntime ?? false,
            composition is not null,
            composition?.Id ?? StableGuidUx3A(blueprintId, "ux3-b-composition"),
            composition?.Provenance ?? "studio:ux3-b",
            composition?.Nodes.Select(item => new StudioUx3BCompositionNodeDraft(item.Id,
                item.BlueprintId.Value, item.ParentNodeId, item.Socket, item.Transform)).ToArray() ?? [],
            composition?.AssemblyRules.ToArray() ?? [], composition?.Parameters.ToArray() ?? [],
            composition?.AssemblyScripts.ToArray() ?? [], composition?.RequiredAtRuntime ?? false,
            blueprint.Authoring.AuthorCollections.ToArray(), false);
    }

    internal StudioUx3BUpdatePreview PreviewUpdateUx3B(StudioUx3BUpdateDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelUpdateApprovalUx3B();
        var project = Project;
        var history = History;
        if (project is null || history is null)
            return FailedUpdateUx3B("Aucun module n’est ouvert.", "blueprint.project_missing");
        if (history.State.Revision != draft.BaseRevision ||
            !string.Equals(ProjectFingerprintUx3A(project), draft.SourceProjectFingerprint,
                StringComparison.Ordinal))
            return FailedUpdateUx3B("Le projet a changé ; rechargez le plan de propagation.",
                UpdateBlueprintFromEntityHandler.RevisionConflictCode);
        if (!draft.Approved)
            return FailedUpdateUx3B("Approuvez séparément la propagation avant le dry-run.",
                "blueprint.update.approval_required");
        var blueprint = project.Blueprints.SingleOrDefault(item => item.Id.Value == draft.BlueprintId);
        if (blueprint is null)
            return FailedUpdateUx3B("Le blueprint n’existe plus.", BlueprintUpdatePlanner.MissingBlueprintCode);
        if (blueprint.IsReadOnly())
            return FailedUpdateUx3B("Un blueprint Standard est protégé en lecture seule.",
                BlueprintUpdatePlanner.ReadOnlyCode);

        BlueprintUpdatePlan plan;
        try
        {
            plan = BlueprintUpdatePlanner.Build(project, blueprint.Id, EntityId.From(draft.SourceEntityId),
                draft.PromotedOverrideKeys, draft.VersionBump,
                draft.ExcludedEntityIds.Select(EntityId.From).ToArray());
        }
        catch (BlueprintUpdatePlanException exception)
        {
            return FailedUpdateUx3B(HumanUpdateDiagnosticUx3B(exception.Code, exception.Message), exception.Code,
                exception.Path);
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            return FailedUpdateUx3B(exception.Message, "blueprint.update.invalid");
        }

        var result = _updateBlueprintUx3B.Handle(history.State, BuildUpdateCommandUx3B(draft, dryRun: true));
        if (!result.Succeeded)
            return FailedUpdateUx3B(result.Diagnostics.FirstOrDefault()?.Message ?? "Le dry-run refuse la mise à jour.",
                result.Diagnostics.FirstOrDefault()?.Code ?? "blueprint.update.invalid");

        var frozen = FreezeUpdateDraftUx3B(draft);
        _approvedUpdateDraftUx3B = frozen;
        _approvedUpdateRevisionUx3B = history.State.Revision;
        _approvedUpdateProjectFingerprintUx3B = ProjectFingerprintUx3A(project);
        _approvedUpdateDraftFingerprintUx3B = UpdateDraftFingerprintUx3B(frozen);
        var labels = InstanceChoicesUx3B(draft.BlueprintId).ToDictionary(item => item.EntityId, item => item.Label);
        var impacts = plan.Instances.Select(item => new StudioUx3BMigrationImpact(item.EntityId.Value,
            labels.GetValueOrDefault(item.EntityId.Value, "Instance protégée"), item.VersionRequirement,
            item.Action, item.Action == "excluded")).ToArray();
        var diff = new List<string>
        {
            $"Version {plan.PreviousVersion} → {plan.TargetVersion}.",
            $"Promouvoir {draft.PromotedOverrideKeys.Count} surcharge(s) sélectionnée(s) depuis l’instance source.",
            $"Analyser {plan.Instances.Count} instance(s), dont {impacts.Count(item => item.Excluded)} exclue(s).",
            $"Publier {draft.CategoryPaths.Count} chemin(s) de catégorie hiérarchique.",
        };
        return new(true, true, true, true,
            "Propagation prête · aucune instance n’est réécrite avant application.",
            result.Patch?.Summary ?? "Prévisualiser la propagation du blueprint.",
            result.Inverse?.Summary ?? "Restaurer version et contraintes d’instances.",
            plan.PreviousVersion, plan.TargetVersion, plan.PromotedDefaults, impacts, diff, []);
    }

    internal StudioUx3BPrefabPreview PreviewPrefabUx3B(StudioUx3BPrefabDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelPrefabApprovalUx3B();
        var project = Project;
        var history = History;
        if (project is null || history is null)
            return FailedPrefabUx3B("Aucun module n’est ouvert.", "blueprint.project_missing");
        if (history.State.Revision != draft.BaseRevision ||
            !string.Equals(ProjectFingerprintUx3A(project), draft.SourceProjectFingerprint,
                StringComparison.Ordinal))
            return FailedPrefabUx3B("Le projet a changé ; rechargez variantes et compositions.",
                ConfigureBlueprintPrefabHandler.RevisionConflictCode);
        if (!draft.Approved)
            return FailedPrefabUx3B("Approuvez séparément variantes et compositions avant le dry-run.",
                "blueprint.prefab.approval_required");
        var blueprint = project.Blueprints.SingleOrDefault(item => item.Id.Value == draft.BlueprintId);
        if (blueprint is null)
            return FailedPrefabUx3B("Le blueprint n’existe plus.", ConfigureBlueprintPrefabHandler.MissingBlueprintCode);
        if (blueprint.IsReadOnly())
            return FailedPrefabUx3B("Un blueprint Standard est protégé en lecture seule.",
                ConfigureBlueprintPrefabHandler.ReadOnlyCode);

        ConfigureBlueprintPrefabCommand command;
        try
        {
            command = BuildPrefabCommandUx3B(history.State.Revision, draft, blueprint.Authoring.AssetBudget,
                dryRun: true);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return FailedPrefabUx3B(exception.Message, "blueprint.prefab.invalid");
        }
        var dryRun = _configurePrefabUx3B.Handle(history.State, command);
        if (!dryRun.Succeeded)
        {
            var diagnostic = dryRun.Diagnostics.FirstOrDefault();
            return FailedPrefabUx3B(HumanPrefabDiagnosticUx3B(diagnostic?.Code ?? "blueprint.prefab.invalid",
                diagnostic?.Message ?? "Le dry-run refuse la configuration."),
                diagnostic?.Code ?? "blueprint.prefab.invalid");
        }

        var candidateResult = _configurePrefabUx3B.Handle(history.State,
            command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false });
        if (!candidateResult.Succeeded)
            return FailedPrefabUx3B("La projection canonique ne peut pas être construite.",
                "blueprint.prefab.invalid");
        var runtime = new BlueprintPrefabRuntimeService(candidateResult.State.Project);
        var variant = runtime.ResolveVariant(blueprint.Id, draft.VariantRequiredAtRuntime);
        var composition = runtime.ResolveComposition(blueprint.Id, draft.CompositionRequiredAtRuntime);
        var diagnostics = variant.Diagnostics.Concat(composition.Diagnostics)
            .Select(item => new StudioUx3BDiagnostic(item.Split(':', 2)[0], "blueprints/prefab", item))
            .ToArray();
        if (diagnostics.Length > 0)
            return new(false, false, true, true, diagnostics[0].Message,
                dryRun.Patch?.Summary ?? string.Empty, dryRun.Inverse?.Summary ?? string.Empty,
                variant.EffectiveOverrides, composition.Nodes.Count, [], diagnostics);

        var frozen = FreezePrefabDraftUx3B(draft);
        _approvedPrefabDraftUx3B = frozen;
        _approvedPrefabRevisionUx3B = history.State.Revision;
        _approvedPrefabProjectFingerprintUx3B = ProjectFingerprintUx3A(project);
        _approvedPrefabDraftFingerprintUx3B = PrefabDraftFingerprintUx3B(frozen);
        var diff = new[]
        {
            draft.VariantEnabled
                ? $"Variante : {variant.EffectiveOverrides.Count} surcharge(s) effectives depuis un blueprint de même famille."
                : "Variante : absente explicitement.",
            draft.CompositionEnabled
                ? $"Composition : {composition.Nodes.Count} nœud(s), {composition.Rules.Count} règle(s) et {composition.Scripts.Count} script(s)."
                : "Composition : absente explicitement.",
            $"Collections auteur : {draft.AuthorCollections.Count} chemin(s) ordonné(s).",
            "Le budget d’asset existant est conservé sans être édité par UX3-B.",
        };
        return new(true, true, true, true,
            "Variantes et composition prêtes · résolution effectuée sur une copie immuable.",
            dryRun.Patch?.Summary ?? "Prévisualiser variantes et composition.",
            dryRun.Inverse?.Summary ?? "Restaurer variantes et composition précédentes.",
            variant.EffectiveOverrides, composition.Nodes.Count, diff, []);
    }

    internal StudioUx3BActionResult ApplyApprovedUpdateUx3B(StudioUx3BUpdateDraft current)
    {
        var project = Project;
        var history = History;
        var approved = _approvedUpdateDraftUx3B;
        if (project is null || history is null || approved is null || _approvedUpdateRevisionUx3B is null ||
            _approvedUpdateProjectFingerprintUx3B is null || _approvedUpdateDraftFingerprintUx3B is null)
            return new(false, "Prévisualisez et approuvez d’abord la propagation.");
        var stale = history.State.Revision != _approvedUpdateRevisionUx3B ||
                    !string.Equals(ProjectFingerprintUx3A(project), _approvedUpdateProjectFingerprintUx3B,
                        StringComparison.Ordinal) ||
                    !string.Equals(UpdateDraftFingerprintUx3B(approved), _approvedUpdateDraftFingerprintUx3B,
                        StringComparison.Ordinal) ||
                    !string.Equals(UpdateDraftFingerprintUx3B(current), _approvedUpdateDraftFingerprintUx3B,
                        StringComparison.Ordinal);
        if (stale)
        {
            CancelUpdateApprovalUx3B();
            return new(false, "Le projet ou le plan de propagation a changé ; relancez l’aperçu.");
        }
        var dryRun = _updateBlueprintUx3B.Handle(history.State, BuildUpdateCommandUx3B(approved, dryRun: true));
        if (!dryRun.Succeeded)
        {
            CancelUpdateApprovalUx3B();
            return new(false, dryRun.Diagnostics.FirstOrDefault()?.Message ?? "Le re-dry-run refuse la propagation.");
        }
        var result = _updateBlueprintUx3B.Handle(history.State, BuildUpdateCommandUx3B(approved, dryRun: false));
        CancelUpdateApprovalUx3B();
        if (!result.Succeeded)
            return new(false, result.Diagnostics.FirstOrDefault()?.Message ?? "La propagation est refusée.");
        history.Record(result);
        CancelPrefabApprovalUx3B();
        return new(true, result.Patch?.Summary ?? "Blueprint et contraintes d’instances mis à jour.");
    }

    internal StudioUx3BActionResult ApplyApprovedPrefabUx3B(StudioUx3BPrefabDraft current)
    {
        var project = Project;
        var history = History;
        var approved = _approvedPrefabDraftUx3B;
        if (project is null || history is null || approved is null || _approvedPrefabRevisionUx3B is null ||
            _approvedPrefabProjectFingerprintUx3B is null || _approvedPrefabDraftFingerprintUx3B is null)
            return new(false, "Prévisualisez et approuvez d’abord variantes et composition.");
        var stale = history.State.Revision != _approvedPrefabRevisionUx3B ||
                    !string.Equals(ProjectFingerprintUx3A(project), _approvedPrefabProjectFingerprintUx3B,
                        StringComparison.Ordinal) ||
                    !string.Equals(PrefabDraftFingerprintUx3B(approved), _approvedPrefabDraftFingerprintUx3B,
                        StringComparison.Ordinal) ||
                    !string.Equals(PrefabDraftFingerprintUx3B(current), _approvedPrefabDraftFingerprintUx3B,
                        StringComparison.Ordinal);
        if (stale)
        {
            CancelPrefabApprovalUx3B();
            return new(false, "Le projet ou la configuration prefab a changé ; relancez l’aperçu.");
        }
        var blueprint = project.Blueprints.SingleOrDefault(item => item.Id.Value == approved.BlueprintId);
        if (blueprint is null)
        {
            CancelPrefabApprovalUx3B();
            return new(false, "Le blueprint n’existe plus.");
        }
        var dryRunCommand = BuildPrefabCommandUx3B(history.State.Revision, approved,
            blueprint.Authoring.AssetBudget, dryRun: true);
        var dryRun = _configurePrefabUx3B.Handle(history.State, dryRunCommand);
        if (!dryRun.Succeeded)
        {
            CancelPrefabApprovalUx3B();
            return new(false, dryRun.Diagnostics.FirstOrDefault()?.Message ?? "Le re-dry-run refuse le prefab.");
        }
        var result = _configurePrefabUx3B.Handle(history.State,
            dryRunCommand with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false });
        CancelPrefabApprovalUx3B();
        if (!result.Succeeded)
            return new(false, result.Diagnostics.FirstOrDefault()?.Message ?? "La configuration prefab est refusée.");
        history.Record(result);
        CancelUpdateApprovalUx3B();
        return new(true, result.Patch?.Summary ?? "Variantes et composition appliquées.");
    }

    internal StudioUx3BActionResult UndoBlueprintLibraryUx3B()
    {
        CancelBlueprintLibraryApprovalsUx3B();
        var result = Undo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx3BActionResult RedoBlueprintLibraryUx3B()
    {
        CancelBlueprintLibraryApprovalsUx3B();
        var result = Redo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx3BActionResult SaveBlueprintLibraryUx3B()
    {
        CancelBlueprintLibraryApprovalsUx3B();
        var result = Save();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx3BApprovalSnapshot CaptureBlueprintLibraryApprovalUx3B() => new(
        _approvedUpdateDraftUx3B, _approvedUpdateRevisionUx3B, _approvedUpdateProjectFingerprintUx3B,
        _approvedUpdateDraftFingerprintUx3B, _approvedPrefabDraftUx3B, _approvedPrefabRevisionUx3B,
        _approvedPrefabProjectFingerprintUx3B, _approvedPrefabDraftFingerprintUx3B);

    internal void RestoreBlueprintLibraryApprovalUx3B(StudioUx3BApprovalSnapshot snapshot)
    {
        _approvedUpdateDraftUx3B = snapshot.UpdateDraft;
        _approvedUpdateRevisionUx3B = snapshot.UpdateRevision;
        _approvedUpdateProjectFingerprintUx3B = snapshot.UpdateProjectFingerprint;
        _approvedUpdateDraftFingerprintUx3B = snapshot.UpdateDraftFingerprint;
        _approvedPrefabDraftUx3B = snapshot.PrefabDraft;
        _approvedPrefabRevisionUx3B = snapshot.PrefabRevision;
        _approvedPrefabProjectFingerprintUx3B = snapshot.PrefabProjectFingerprint;
        _approvedPrefabDraftFingerprintUx3B = snapshot.PrefabDraftFingerprint;
    }

    internal void CancelBlueprintLibraryApprovalsUx3B()
    {
        CancelUpdateApprovalUx3B();
        CancelPrefabApprovalUx3B();
    }

    internal void CancelUpdateBlueprintApprovalUx3B() => CancelUpdateApprovalUx3B();

    internal void CancelPrefabBlueprintApprovalUx3B() => CancelPrefabApprovalUx3B();

    internal void CloseUx3B() => CancelBlueprintLibraryApprovalsUx3B();

    internal string UpdateDraftFingerprintUx3B(StudioUx3BUpdateDraft draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteBlueprintUx3A(writer, draft.BaseRevision.Value);
        WriteBlueprintUx3A(writer, draft.SourceProjectFingerprint);
        WriteBlueprintUx3A(writer, draft.BlueprintId);
        WriteBlueprintUx3A(writer, draft.SourceAreaId);
        WriteBlueprintUx3A(writer, draft.SourceEntityId);
        WriteBlueprintStringsUx3A(writer, draft.PromotedOverrideKeys);
        WriteBlueprintUx3A(writer, (int)draft.VersionBump);
        writer.Write(draft.ExcludedEntityIds.Count);
        foreach (var id in draft.ExcludedEntityIds) WriteBlueprintUx3A(writer, id);
        WriteBlueprintStringsUx3A(writer, draft.CategoryPaths);
        writer.Write(draft.Approved);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    internal string PrefabDraftFingerprintUx3B(StudioUx3BPrefabDraft draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteBlueprintUx3A(writer, draft.BaseRevision.Value);
        WriteBlueprintUx3A(writer, draft.SourceProjectFingerprint);
        WriteBlueprintUx3A(writer, draft.BlueprintId);
        writer.Write(draft.VariantEnabled);
        WriteBlueprintUx3A(writer, draft.VariantId);
        WriteBlueprintUx3A(writer, draft.VariantProvenance);
        WriteBlueprintUx3A(writer, draft.BaseBlueprintId ?? Guid.Empty);
        writer.Write(draft.VariantOverrides.Count);
        foreach (var item in draft.VariantOverrides)
        {
            WriteBlueprintUx3A(writer, (int)item.Kind);
            WriteBlueprintUx3A(writer, item.Key);
            WriteBlueprintUx3A(writer, item.Value);
        }
        writer.Write(draft.VariantRequiredAtRuntime);
        writer.Write(draft.CompositionEnabled);
        WriteBlueprintUx3A(writer, draft.CompositionId);
        WriteBlueprintUx3A(writer, draft.CompositionProvenance);
        writer.Write(draft.CompositionNodes.Count);
        foreach (var node in draft.CompositionNodes)
        {
            WriteBlueprintUx3A(writer, node.Id);
            WriteBlueprintUx3A(writer, node.BlueprintId);
            WriteBlueprintUx3A(writer, node.ParentNodeId ?? Guid.Empty);
            WriteBlueprintUx3A(writer, node.Socket ?? string.Empty);
            WriteTransformUx3B(writer, node.Transform);
        }
        WriteBlueprintStringsUx3A(writer, draft.AssemblyRules);
        WriteBlueprintStringsUx3A(writer, draft.Parameters);
        WriteBlueprintStringsUx3A(writer, draft.AssemblyScripts);
        writer.Write(draft.CompositionRequiredAtRuntime);
        WriteBlueprintStringsUx3A(writer, draft.AuthorCollections);
        writer.Write(draft.Approved);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    internal StudioUx3BEvidenceProject? CreateBlueprintLibraryEvidenceProjectUx3B() =>
        Project is { } project ? BuildBlueprintLibraryEvidenceProjectUx3B(project) : null;

    internal StudioUx3BSmokeResult RunUx3BContractSmoke()
    {
        var empty = EmptyBlueprintLibrarySmokeUx3B();
        var source = Project;
        if (source is null) return empty;
        var evidence = BuildBlueprintLibraryEvidenceProjectUx3B(source);
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux3-b-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        StudioUx3BSmokeResult Fail(string stage)
        {
            Console.Error.WriteLine($"OPENRPG_FORGE_STUDIO_UX3_B_CONTRACT_STAGE_FAILED stage={stage}");
            return empty;
        }
        StudioUx3BSmokeResult Execute()
        {
            try
            {
                var created = ProjectSession.Create(root, evidence.Project);
                if (!created.Succeeded)
                {
                    Console.Error.WriteLine("OPENRPG_FORGE_STUDIO_UX3_B_CONTRACT_CREATE_FAILED " +
                                            string.Join('|', created.Diagnostics.Select(item =>
                                                $"{item.Code}:{item.Message}")));
                    return Fail("create");
                }
                var opened = editing.Open(root);
                if (!opened.Succeeded)
                {
                    Console.Error.WriteLine("OPENRPG_FORGE_STUDIO_UX3_B_CONTRACT_OPEN_FAILED " +
                                            string.Join('|', opened.Diagnostics.Select(item =>
                                                $"{item.Code}:{item.Message}")));
                    return Fail("open");
                }
                var choices = editing.BlueprintChoicesUx3B;
                var instances = editing.InstanceChoicesUx3B(evidence.BlueprintId);
                var explicitSelection = editing.CreateUpdateDraftUx3B(Guid.Empty, Guid.Empty) is null &&
                                        editing.CreatePrefabDraftUx3B(Guid.Empty) is null &&
                                        instances.Count >= 3;
                var draft = editing.CreateUpdateDraftUx3B(evidence.BlueprintId, evidence.SourceEntityId);
                if (draft is null) return Fail("initial_update_draft");
                var sourceEntity = instances.Single(item => item.EntityId == evidence.SourceEntityId);
                var promotedKey = sourceEntity.Overrides.First().Key;
                var configured = draft with
                {
                    PromotedOverrideKeys = [promotedKey],
                    VersionBump = BlueprintVersionBump.Minor,
                    ExcludedEntityIds = [evidence.ExcludedEntityId],
                    CategoryPaths = ["Monde/Créatures/Gardiens", "Bibliothèque/UX3"],
                    Approved = true,
                };
                var invalid = editing.PreviewUpdateUx3B(configured with
                {
                    PromotedOverrideKeys = ["override.absent"],
                });
                var invalidUpdate = !invalid.Succeeded;
                var beforeDryProject = editing.Project!;
                var beforeDryRevision = editing.History!.State.Revision;
                var preview = editing.PreviewUpdateUx3B(configured);
                var dryRun = preview.Succeeded && ReferenceEquals(beforeDryProject, editing.Project) &&
                             editing.History.State.Revision == beforeDryRevision;

                var concurrentProject = editing.Project!;
                var concurrentBlueprint = concurrentProject.Blueprints
                    .Single(item => item.Id.Value == evidence.BlueprintId);
                var intervening = editing._upsertBlueprintUx3A.Handle(editing.History.State,
                    new UpsertBlueprintCommand(CommandId.From(Guid.NewGuid()), editing.History.State.Revision,
                        BlueprintId.From(evidence.BlueprintId), concurrentBlueprint.Slug,
                        "Modification concurrente UX3-B", concurrentBlueprint.Kind, false));
                var staleSource = false;
                if (intervening.Succeeded)
                {
                    editing.History.Record(intervening);
                    staleSource = !editing.ApplyApprovedUpdateUx3B(configured).Succeeded;
                    editing.Undo();
                }

                draft = editing.CreateUpdateDraftUx3B(evidence.BlueprintId, evidence.SourceEntityId);
                if (draft is null) return Fail("stale_update_draft");
                configured = draft with
                {
                    PromotedOverrideKeys = [promotedKey],
                    VersionBump = BlueprintVersionBump.Minor,
                    ExcludedEntityIds = [evidence.ExcludedEntityId],
                    CategoryPaths = ["Monde/Créatures/Gardiens", "Bibliothèque/UX3"],
                    Approved = true,
                };
                preview = editing.PreviewUpdateUx3B(configured);
                var staleApproval = preview.Succeeded && !editing.ApplyApprovedUpdateUx3B(configured with
                {
                    CategoryPaths = [.. configured.CategoryPaths, "Altération"],
                }).Succeeded;

                draft = editing.CreateUpdateDraftUx3B(evidence.BlueprintId, evidence.SourceEntityId);
                if (draft is null) return Fail("apply_update_draft");
                configured = draft with
                {
                    PromotedOverrideKeys = [promotedKey],
                    VersionBump = BlueprintVersionBump.Minor,
                    ExcludedEntityIds = [evidence.ExcludedEntityId],
                    CategoryPaths = ["Monde/Créatures/Gardiens", "Bibliothèque/UX3"],
                    Approved = true,
                };
                var sourceProject = editing.Project!;
                var sourceBlueprint = sourceProject.Blueprints.Single(item => item.Id.Value == evidence.BlueprintId);
                var sourceNeighbors = sourceProject.Blueprints.Where(item => item.Id.Value != evidence.BlueprintId)
                    .ToArray();
                var sourceAreas = sourceProject.Areas.ToArray();
                preview = editing.PreviewUpdateUx3B(configured);
                var updateApplied = preview.Succeeded
                    ? editing.ApplyApprovedUpdateUx3B(configured)
                    : new StudioUx3BActionResult(false, "aperçu refusé");
                var updateProject = editing.Project!;
                var updated = updateProject.Blueprints.Single(item => item.Id.Value == evidence.BlueprintId);
                var migrated = updateProject.Areas.SelectMany(item => item.Entities)
                    .Single(item => item.Id.Value == evidence.MigratedEntityId);
                var excluded = updateProject.Areas.SelectMany(item => item.Entities)
                    .Single(item => item.Id.Value == evidence.ExcludedEntityId);
                var sourceAfter = updateProject.Areas.SelectMany(item => item.Entities)
                    .Single(item => item.Id.Value == evidence.SourceEntityId);
                var updatePlan = updateApplied.Succeeded && preview.Instances.Count >= 3;
                var selective = updated.Authoring.PromotedDefaults.Any(item => item.Key == promotedKey) &&
                                updated.Authoring.PromotedDefaults.Count == 1;
                var semver = updated.Authoring.Version == preview.TargetVersion &&
                             preview.TargetVersion != preview.PreviousVersion;
                var migration = migrated.BlueprintVersionRequirement == $"={preview.TargetVersion}";
                var exclusion = excluded.BlueprintVersionRequirement == $">={preview.PreviousVersion}";
                var overrides = sourceAfter.Overrides.Properties.SequenceEqual(
                                    sourceAreas.SelectMany(item => item.Entities)
                                        .Single(item => item.Id.Value == evidence.SourceEntityId).Overrides.Properties) &&
                                migrated.Overrides.Properties.SequenceEqual(sourceAreas.SelectMany(item => item.Entities)
                                    .Single(item => item.Id.Value == evidence.MigratedEntityId).Overrides.Properties) &&
                                excluded.Overrides.Properties.SequenceEqual(sourceAreas.SelectMany(item => item.Entities)
                                    .Single(item => item.Id.Value == evidence.ExcludedEntityId).Overrides.Properties);
                var categories = updated.Authoring.CategoryPaths.Contains("Monde/Créatures/Gardiens") &&
                                 BlueprintLibraryIndex.Build(updateProject).CategoryPaths.Contains("Monde/Créatures");
                var updateJson = ProjectJsonSerializer.Serialize(updateProject);
                var updateUndo = editing.UndoBlueprintLibraryUx3B();
                var updateUndone = updateUndo.Succeeded &&
                                   ProjectJsonSerializer.Serialize(editing.Project!) ==
                                   ProjectJsonSerializer.Serialize(sourceProject);
                var updateRedo = editing.RedoBlueprintLibraryUx3B();
                var updateRedone = updateRedo.Succeeded &&
                                   ProjectJsonSerializer.Serialize(editing.Project!) == updateJson;

                var prefabDraft = editing.CreatePrefabDraftUx3B(evidence.BlueprintId);
                if (prefabDraft is null) return Fail("initial_prefab_draft");
                var nodeId = StableGuidUx3A(evidence.BlueprintId, "ux3-b-root-node");
                var configuredPrefab = prefabDraft with
                {
                    VariantEnabled = true,
                    BaseBlueprintId = evidence.BaseBlueprintId,
                    VariantOverrides =
                    [new StudioUx3BVariantOverrideDraft(PrefabVariantOverrideKind.Material,
                        "body", "asset:ember")],
                    VariantRequiredAtRuntime = true,
                    CompositionEnabled = true,
                    CompositionNodes =
                    [new StudioUx3BCompositionNodeDraft(nodeId, evidence.BaseBlueprintId, null, "floor",
                        new TransformValue(new Vector3Value(2, 0, 3), QuaternionValue.Identity,
                            Vector3Value.One))],
                    AssemblyRules = ["socket-compatible"],
                    Parameters = ["intensity"],
                    AssemblyScripts = [],
                    CompositionRequiredAtRuntime = true,
                    AuthorCollections = ["Bosses", "Crypt/Encounter"],
                    Approved = true,
                };
                var invalidPrefab = editing.PreviewPrefabUx3B(configuredPrefab with
                {
                    BaseBlueprintId = evidence.BlueprintId,
                });
                var invalidBlocked = invalidUpdate && !invalidPrefab.Succeeded;

                var updateApprovalDraft = editing.CreateUpdateDraftUx3B(evidence.BlueprintId,
                    evidence.SourceEntityId)! with
                { Approved = true };
                var updateApproval = editing.PreviewUpdateUx3B(updateApprovalDraft);
                var prefabApproval = editing.PreviewPrefabUx3B(configuredPrefab);
                var approvals = editing.CaptureBlueprintLibraryApprovalUx3B();
                var separateApprovals = updateApproval.Succeeded && prefabApproval.Succeeded &&
                                        approvals.UpdateDraft is not null && approvals.PrefabDraft is not null;
                editing.CancelBlueprintLibraryApprovalsUx3B();

                prefabDraft = editing.CreatePrefabDraftUx3B(evidence.BlueprintId);
                if (prefabDraft is null) return Fail("apply_prefab_draft");
                configuredPrefab = configuredPrefab with
                {
                    BaseRevision = prefabDraft.BaseRevision,
                    SourceProjectFingerprint = prefabDraft.SourceProjectFingerprint,
                    Approved = true,
                };
                prefabApproval = editing.PreviewPrefabUx3B(configuredPrefab);
                var prefabApplied = prefabApproval.Succeeded
                    ? editing.ApplyApprovedPrefabUx3B(configuredPrefab)
                    : new StudioUx3BActionResult(false, "aperçu prefab refusé");
                var finalProject = editing.Project!;
                var configuredBlueprint = finalProject.Blueprints.Single(item => item.Id.Value == evidence.BlueprintId);
                var variantConfigured = prefabApplied.Succeeded &&
                                        configuredBlueprint.Authoring.PrefabVariant?.BaseBlueprintId.Value ==
                                        evidence.BaseBlueprintId &&
                                        configuredBlueprint.Authoring.PrefabVariant.Overrides.Count == 1;
                var compositionConfigured = configuredBlueprint.Authoring.PrefabComposition?.Nodes.Count == 1 &&
                                            configuredBlueprint.Authoring.AuthorCollections
                                                .SequenceEqual(["Bosses", "Crypt/Encounter"]);
                var runtime = new BlueprintPrefabRuntimeService(finalProject);
                var runtimeProjection = runtime.ResolveVariant(BlueprintId.From(evidence.BlueprintId), true)
                                            .EffectiveOverrides.Count == 1 &&
                                        runtime.ResolveComposition(BlueprintId.From(evidence.BlueprintId), true)
                                            .Nodes.Count == 1;
                var finalJson = ProjectJsonSerializer.Serialize(finalProject);
                var prefabUndo = editing.UndoBlueprintLibraryUx3B();
                var prefabUndone = prefabUndo.Succeeded &&
                                   ProjectJsonSerializer.Serialize(editing.Project!) == updateJson;
                var prefabRedo = editing.RedoBlueprintLibraryUx3B();
                var prefabRedone = prefabRedo.Succeeded &&
                                   ProjectJsonSerializer.Serialize(editing.Project!) == finalJson;
                var saved = editing.SaveBlueprintLibraryUx3B();
                editing.Close();
                var persistence = saved.Succeeded && reopened.Open(root).Succeeded &&
                                  ProjectJsonSerializer.Serialize(reopened.Project!) == finalJson;
                reopened.Close();

                var blueprintNeighbors = sourceNeighbors.All(before => finalProject.Blueprints
                    .Any(after => after.Id == before.Id && after == before));
                var areaNeighbors = sourceAreas.Length == finalProject.Areas.Count && sourceAreas
                    .Zip(finalProject.Areas).All(pair => pair.First.Id == pair.Second.Id &&
                        pair.First.Entities.Count == pair.Second.Entities.Count && pair.First.Entities
                            .Zip(pair.Second.Entities).All(entityPair =>
                                entityPair.First.Id == entityPair.Second.Id &&
                                entityPair.First.BlueprintId == entityPair.Second.BlueprintId &&
                                entityPair.First.Transform == entityPair.Second.Transform &&
                                entityPair.First.Tags.SequenceEqual(entityPair.Second.Tags) &&
                                entityPair.First.Overrides.Properties.SequenceEqual(
                                    entityPair.Second.Overrides.Properties)));
                var projectNeighbors = finalProject.Id == sourceProject.Id &&
                                       finalProject.Title == sourceProject.Title &&
                                       finalProject.Module == sourceProject.Module &&
                                       finalProject.Dialogues.SequenceEqual(sourceProject.Dialogues) &&
                                       finalProject.Quests.SequenceEqual(sourceProject.Quests) &&
                                       configuredBlueprint.Combat == sourceBlueprint.Combat &&
                                       configuredBlueprint.CreatureFoundation == sourceBlueprint.CreatureFoundation &&
                                       configuredBlueprint.ItemFoundation == sourceBlueprint.ItemFoundation &&
                                       configuredBlueprint.Authoring.AssetBudget == sourceBlueprint.Authoring.AssetBudget;
                var human = choices.All(item => !item.Label.Contains(item.Id.ToString("D"),
                                    StringComparison.OrdinalIgnoreCase)) && instances.All(item =>
                                    !item.Label.Contains(item.EntityId.ToString("D"),
                                        StringComparison.OrdinalIgnoreCase));
                var origin = evidence.UsesRealModule ^ evidence.UsesCanonicalFallback;
                var canonical = updateApplied.Succeeded && prefabApplied.Succeeded;
                var undoRedo = updateUndone && updateRedone && prefabUndone && prefabRedone;
                var succeeded = explicitSelection && origin && updatePlan && selective && semver && migration &&
                                exclusion && overrides && categories && variantConfigured && compositionConfigured &&
                                runtimeProjection && invalidBlocked && dryRun && staleSource && staleApproval &&
                                separateApprovals && canonical && undoRedo && persistence && blueprintNeighbors &&
                                areaNeighbors && projectNeighbors && human;
                return new StudioUx3BSmokeResult(succeeded, explicitSelection, evidence.UsesRealModule,
                    evidence.UsesCanonicalFallback, updatePlan, selective, semver, migration, exclusion, overrides,
                    categories, variantConfigured, compositionConfigured, runtimeProjection, invalidBlocked, dryRun,
                    staleSource, staleApproval, separateApprovals, canonical, undoRedo, persistence,
                    blueprintNeighbors, areaNeighbors, projectNeighbors, human, true, false);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"OPENRPG_FORGE_STUDIO_UX3_B_CONTRACT_EXCEPTION " +
                                        $"type={exception.GetType().Name} message={exception.Message}");
                return empty;
            }
            finally
            {
                editing.Close();
                reopened.Close();
            }
        }

        var result = Execute();
        var released = DeleteTemporaryBlueprintUx3A(root);
        return result with { TemporaryWorkspaceReleased = released, Succeeded = result.Succeeded && released };
    }

    private void CancelUpdateApprovalUx3B()
    {
        _approvedUpdateDraftUx3B = null;
        _approvedUpdateRevisionUx3B = null;
        _approvedUpdateProjectFingerprintUx3B = null;
        _approvedUpdateDraftFingerprintUx3B = null;
    }

    private void CancelPrefabApprovalUx3B()
    {
        _approvedPrefabDraftUx3B = null;
        _approvedPrefabRevisionUx3B = null;
        _approvedPrefabProjectFingerprintUx3B = null;
        _approvedPrefabDraftFingerprintUx3B = null;
    }

    private static UpdateBlueprintFromEntityCommand BuildUpdateCommandUx3B(StudioUx3BUpdateDraft draft,
        bool dryRun) => new(CommandId.From(Guid.NewGuid()), draft.BaseRevision,
        BlueprintId.From(draft.BlueprintId), EntityId.From(draft.SourceEntityId),
        draft.PromotedOverrideKeys, draft.VersionBump,
        draft.ExcludedEntityIds.Select(EntityId.From).ToArray(), dryRun, draft.CategoryPaths);

    private static ConfigureBlueprintPrefabCommand BuildPrefabCommandUx3B(ProjectRevision revision,
        StudioUx3BPrefabDraft draft, BlueprintAssetBudgetProfile? assetBudget, bool dryRun)
    {
        PrefabVariantDefinition? variant = null;
        if (draft.VariantEnabled)
        {
            if (draft.BaseBlueprintId is not { } baseId)
                throw new ArgumentException("Choisissez le blueprint de base de la variante.");
            variant = new PrefabVariantDefinition(draft.VariantId,
                PrefabVariantDefinition.CurrentSchemaVersion, draft.VariantProvenance,
                BlueprintId.From(baseId), draft.VariantOverrides.Select(item =>
                    new PrefabVariantOverride(item.Kind, item.Key.Trim(), item.Value.Trim())).ToArray(),
                draft.VariantRequiredAtRuntime);
        }
        PrefabCompositionDefinition? composition = null;
        if (draft.CompositionEnabled)
        {
            composition = new PrefabCompositionDefinition(draft.CompositionId,
                PrefabCompositionDefinition.CurrentSchemaVersion, draft.CompositionProvenance,
                draft.CompositionNodes.Select(item => new PrefabCompositionNode(item.Id,
                    BlueprintId.From(item.BlueprintId), item.ParentNodeId,
                    string.IsNullOrWhiteSpace(item.Socket) ? null : item.Socket.Trim(), item.Transform)).ToArray(),
                draft.AssemblyRules, draft.Parameters, draft.AssemblyScripts,
                draft.CompositionRequiredAtRuntime);
        }
        return new ConfigureBlueprintPrefabCommand(CommandId.From(Guid.NewGuid()), revision,
            BlueprintId.From(draft.BlueprintId), variant, composition, assetBudget,
            draft.AuthorCollections, dryRun);
    }

    private static StudioUx3BUpdateDraft FreezeUpdateDraftUx3B(StudioUx3BUpdateDraft draft) => draft with
    {
        PromotedOverrideKeys = draft.PromotedOverrideKeys.ToArray(),
        ExcludedEntityIds = draft.ExcludedEntityIds.ToArray(),
        CategoryPaths = draft.CategoryPaths.ToArray(),
    };

    private static StudioUx3BPrefabDraft FreezePrefabDraftUx3B(StudioUx3BPrefabDraft draft) => draft with
    {
        VariantOverrides = draft.VariantOverrides.ToArray(),
        CompositionNodes = draft.CompositionNodes.ToArray(),
        AssemblyRules = draft.AssemblyRules.ToArray(),
        Parameters = draft.Parameters.ToArray(),
        AssemblyScripts = draft.AssemblyScripts.ToArray(),
        AuthorCollections = draft.AuthorCollections.ToArray(),
    };

    private static StudioUx3BUpdatePreview FailedUpdateUx3B(string message, string code,
        string path = "blueprints/update") => new(false, false, false, true,
        HumanUpdateDiagnosticUx3B(code, message), string.Empty, string.Empty, string.Empty, string.Empty,
        [], [], [], [new StudioUx3BDiagnostic(code, path, HumanUpdateDiagnosticUx3B(code, message))]);

    private static StudioUx3BPrefabPreview FailedPrefabUx3B(string message, string code,
        string path = "blueprints/prefab") => new(false, false, false, true,
        HumanPrefabDiagnosticUx3B(code, message), string.Empty, string.Empty, [], 0, [],
        [new StudioUx3BDiagnostic(code, path, HumanPrefabDiagnosticUx3B(code, message))]);

    private static string HumanUpdateDiagnosticUx3B(string code, string fallback) => code switch
    {
        UpdateBlueprintFromEntityHandler.RevisionConflictCode =>
            "Le projet a changé ; rechargez la propagation.",
        BlueprintUpdatePlanner.MissingBlueprintCode => "Le blueprint sélectionné n’existe plus.",
        BlueprintUpdatePlanner.ReadOnlyCode => "Un blueprint Standard installé est protégé en lecture seule.",
        BlueprintUpdatePlanner.MissingSourceCode => "L’instance source n’existe plus.",
        BlueprintUpdatePlanner.SourceMismatchCode => "L’instance source ne dépend pas de ce blueprint.",
        BlueprintUpdatePlanner.MissingOverrideCode => "Une surcharge sélectionnée n’existe pas sur l’instance source.",
        "blueprint.update.approval_required" => "Approuvez séparément la propagation avant le dry-run.",
        _ => fallback,
    };

    private static string HumanPrefabDiagnosticUx3B(string code, string fallback) => code switch
    {
        ConfigureBlueprintPrefabHandler.RevisionConflictCode =>
            "Le projet a changé ; rechargez variantes et composition.",
        ConfigureBlueprintPrefabHandler.MissingBlueprintCode => "Le blueprint sélectionné n’existe plus.",
        ConfigureBlueprintPrefabHandler.ReadOnlyCode =>
            "Un blueprint Standard installé est protégé en lecture seule.",
        "blueprint.variant.reference_missing" =>
            "La variante doit référencer un autre blueprint de la même famille.",
        "blueprint.variant.override_invalid" =>
            "Chaque surcharge de variante exige un type, une clé unique et une valeur.",
        "blueprint.composition.hierarchy_invalid" =>
            "La composition exige un seul nœud racine et une hiérarchie sans cycle.",
        "blueprint.composition.reference_missing" =>
            "Chaque nœud doit référencer un blueprint existant et un parent existant.",
        "blueprint.prefab.approval_required" =>
            "Approuvez séparément variantes et composition avant le dry-run.",
        _ => fallback,
    };

    private static StudioUx3BEvidenceProject BuildBlueprintLibraryEvidenceProjectUx3B(ProjectDefinition source)
    {
        var blueprints = source.Blueprints.ToList();
        var areas = source.Areas.ToList();
        var fallback = false;
        var target = blueprints.FirstOrDefault(item => !item.IsReadOnly());
        if (target is null)
        {
            fallback = true;
            var id = BlueprintId.From(StableGuidUx3A(source.Id.Value, "ux3-b-target"));
            target = new BlueprintDefinition(id, "gardien-ux3-b", "Gardien témoin UX3-B",
                BlueprintKind.Placeable);
            blueprints.Add(target);
        }
        var baseBlueprint = blueprints.FirstOrDefault(item => item.Id != target.Id && item.Kind == target.Kind);
        if (baseBlueprint is null)
        {
            fallback = true;
            var id = BlueprintId.From(StableGuidUx3A(source.Id.Value, "ux3-b-base"));
            baseBlueprint = new BlueprintDefinition(id, "socle-ux3-b", "Socle de variante UX3-B",
                target.Kind, target.Combat, BlueprintPaletteOrigin.ProjectContent, false,
                target.Provenance, target.CreaturePresentation, target.CreatureProgression,
                target.CreatureLoot, target.CreatureRoleBehavior, target.CreatureEvents, target.ItemAdvanced,
                authoring: null, target.CreatureFoundation, target.ItemFoundation);
            blueprints.Add(baseBlueprint);
        }
        if (areas.Count == 0)
            return new(source, target.Id.Value, baseBlueprint.Id.Value, Guid.Empty, Guid.Empty, Guid.Empty,
                Guid.Empty, false, true);
        var area = areas.FirstOrDefault(candidate => candidate.Entities.Any(item => item.BlueprintId == target.Id))
                   ?? areas[0];
        var sourceEntity = area.Entities.FirstOrDefault(item => item.BlueprintId == target.Id);
        var sourceOverrides = new EntityPropertyOverrides(EntityPropertyOverrides.CurrentSchemaVersion,
        [
            new EntityPropertyOverride("visual.scale", EntityPropertyValueKind.Number, "2"),
            new EntityPropertyOverride("appearance.variant", EntityPropertyValueKind.String, "ember"),
        ]);
        if (sourceEntity is null)
        {
            fallback = true;
            sourceEntity = new EntityInstance(EntityId.From(StableGuidUx3A(source.Id.Value, "ux3-b-source")),
                target.Id, TransformValue.Identity, ["ux3-b", "source"], sourceOverrides,
                $"={target.Authoring.Version}");
        }
        else if (sourceEntity.Overrides.Properties.Count < 2)
        {
            fallback = true;
            sourceEntity = new EntityInstance(sourceEntity.Id, sourceEntity.BlueprintId, sourceEntity.Transform,
                sourceEntity.Tags, sourceOverrides, $"={target.Authoring.Version}");
        }
        EntityInstance Instance(string discriminator, string tag) => new(
            EntityId.From(StableGuidUx3A(source.Id.Value, discriminator)), target.Id,
            new TransformValue(new Vector3Value(discriminator.EndsWith("migrated", StringComparison.Ordinal) ? 2 : 4,
                0, 0), QuaternionValue.Identity, Vector3Value.One), ["ux3-b", tag],
            new EntityPropertyOverrides(EntityPropertyOverrides.CurrentSchemaVersion,
                [new EntityPropertyOverride("instance.note", EntityPropertyValueKind.String, tag)]),
            $"={target.Authoring.Version}");
        var migrated = area.Entities.FirstOrDefault(item => item.Id.Value ==
            StableGuidUx3A(source.Id.Value, "ux3-b-migrated")) ?? Instance("ux3-b-migrated", "migrer");
        var excluded = area.Entities.FirstOrDefault(item => item.Id.Value ==
            StableGuidUx3A(source.Id.Value, "ux3-b-excluded")) ?? Instance("ux3-b-excluded", "exclure");
        var retained = area.Entities.Where(item => item.Id != sourceEntity.Id && item.Id != migrated.Id &&
                                                item.Id != excluded.Id).ToArray();
        area = area.WithEntities([.. retained, sourceEntity, migrated, excluded]);
        areas = areas.Select(item => item.Id == area.Id ? area : item).ToList();
        fallback = true;
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, blueprints, areas,
            source.Dialogues, source.Quests);
        return new(project, target.Id.Value, baseBlueprint.Id.Value, area.Id.Value, sourceEntity.Id.Value,
            migrated.Id.Value, excluded.Id.Value, !fallback, fallback);
    }

    private static StudioUx3BSmokeResult EmptyBlueprintLibrarySmokeUx3B() => new(
        Succeeded: false, ExplicitSelection: false, UsesRealModuleEvidence: false,
        UsesCanonicalFallbackEvidence: false, UpdatePlan: false, SelectivePromotion: false,
        SemanticVersionBump: false, MigrationImpact: false, ExclusionPreserved: false,
        OverridesPreserved: false, CategoriesHierarchical: false, VariantConfigured: false,
        CompositionConfigured: false, RuntimeProjection: false, InvalidConfigurationBlocked: false,
        DryRun: false, StaleSourceBlocked: false, StaleApprovalBlocked: false,
        SeparateApprovals: false, CanonicalCommands: false, UndoRedo: false, Persistence: false,
        BlueprintNeighborsPreserved: false, AreaNeighborsPreserved: false,
        ProjectNeighborsPreserved: false, HumanText: false, RuntimeClaimsBounded: true,
        TemporaryWorkspaceReleased: false);

    private static void WriteTransformUx3B(BinaryWriter writer, TransformValue transform)
    {
        writer.Write(transform.Position.X); writer.Write(transform.Position.Y); writer.Write(transform.Position.Z);
        writer.Write(transform.Rotation.X); writer.Write(transform.Rotation.Y); writer.Write(transform.Rotation.Z);
        writer.Write(transform.Rotation.W);
        writer.Write(transform.Scale.X); writer.Write(transform.Scale.Y); writer.Write(transform.Scale.Z);
    }
}
