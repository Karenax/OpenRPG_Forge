using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Blueprints;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Validation;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly UpsertBlueprintHandler _upsertBlueprintUx3D = new(new SystemClock());
    private readonly ConfigureBlueprintPaletteHandler _configureBlueprintPaletteUx3D = new(new SystemClock());
    private readonly BlueprintDropPlanner _blueprintDropPlannerUx3D = new();

    private StudioUx3DDependencyDraft? _approvedDependencyDraftUx3D;
    private ProjectRevision? _approvedDependencyRevisionUx3D;
    private string? _approvedDependencyProjectFingerprintUx3D;
    private string? _approvedDependencyDraftFingerprintUx3D;
    private StudioUx3DProvenanceDraft? _approvedProvenanceDraftUx3D;
    private ProjectRevision? _approvedProvenanceRevisionUx3D;
    private string? _approvedProvenanceProjectFingerprintUx3D;
    private string? _approvedProvenanceDraftFingerprintUx3D;

    internal IReadOnlyList<StudioUx3DBlueprintChoice> BlueprintGovernanceChoicesUx3D
    {
        get
        {
            if (Project is null) return [];
            var used = Project.Areas.SelectMany(area => area.Entities).Select(item => item.BlueprintId)
                .Concat(Project.Dialogues.SelectMany(dialogue => dialogue.Nodes)
                    .Where(node => node.SpeakerBlueprintId is not null)
                    .Select(node => node.SpeakerBlueprintId!))
                .ToHashSet();
            return Project.Blueprints.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.Slug, StringComparer.Ordinal)
                .Select(item => new StudioUx3DBlueprintChoice(
                    item.Id.Value,
                    $"{item.DisplayName} · {HumanBlueprintKindUx3A(item.Kind)} · {HumanOriginUx3A(item.PaletteOrigin)}",
                    item.IsReadOnly(), used.Contains(item.Id), item.Authoring.Dependencies,
                    item.Authoring.PlatformByteBudget, item.Provenance.Source, item.Provenance.Author,
                    item.Provenance.LicenseSpdx, item.Provenance.ContentHash,
                    item.Provenance.PermissionEvidence, item.Provenance.IsAiGenerated,
                    item.Provenance.Modifications))
                .ToArray();
        }
    }

    internal StudioUx3DDependencyDraft? CreateDependencyDraftUx3D(Guid blueprintId)
    {
        if (Project is null || History is null || blueprintId == Guid.Empty) return null;
        var blueprint = Project.Blueprints.FirstOrDefault(item => item.Id.Value == blueprintId);
        return blueprint is null ? null : new StudioUx3DDependencyDraft(
            History.State.Revision, ProjectFingerprintUx3A(Project), blueprintId,
            blueprint.Authoring.Dependencies.ToArray(), blueprint.Authoring.PlatformByteBudget, false);
    }

    internal StudioUx3DProvenanceDraft? CreateProvenanceDraftUx3D(Guid blueprintId)
    {
        if (Project is null || History is null || blueprintId == Guid.Empty) return null;
        var blueprint = Project.Blueprints.FirstOrDefault(item => item.Id.Value == blueprintId);
        return blueprint is null ? null : new StudioUx3DProvenanceDraft(
            History.State.Revision, ProjectFingerprintUx3A(Project), blueprintId,
            blueprint.Provenance.Source, blueprint.Provenance.Author, blueprint.Provenance.LicenseSpdx,
            blueprint.Provenance.ContentHash, blueprint.Provenance.PermissionEvidence,
            blueprint.Provenance.IsAiGenerated, blueprint.Provenance.Modifications.ToArray(), false);
    }

    internal StudioUx3DDependencyPreview PreviewDependenciesUx3D(StudioUx3DDependencyDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelDependencyApprovalUx3D();
        if (Project is null || History is null || Session is null)
            return FailedDependenciesUx3D("Aucun module n’est ouvert.", "blueprint.dependencies.project_missing", "project");
        if (!draft.Approved)
            return FailedDependenciesUx3D("Approuvez les dépendances avant l’aperçu sec.",
                "blueprint.dependencies.approval_required", "blueprints/authoring/dependencies");
        if (draft.BaseRevision != History.State.Revision ||
            draft.SourceProjectFingerprint != ProjectFingerprintUx3A(Project))
            return FailedDependenciesUx3D("Le projet a changé ; rechargez le blueprint.",
                "blueprint.dependencies.project_stale", "project");
        var source = Project.Blueprints.FirstOrDefault(item => item.Id.Value == draft.BlueprintId);
        if (source is null)
            return FailedDependenciesUx3D("Le blueprint sélectionné n’existe plus.",
                "blueprint.dependencies.missing", "blueprints");
        if (source.IsReadOnly())
            return FailedDependenciesUx3D("Un blueprint Standard installé est protégé en lecture seule.",
                UpsertBlueprintHandler.ReadOnlyCode, $"blueprints/{source.Id}/authoring");

        BlueprintAuthoringMetadata authoring;
        try
        {
            authoring = CopyAuthoringUx3D(source.Authoring, draft.Dependencies, draft.PlatformByteBudget);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return FailedDependenciesUx3D(HumanDependencyExceptionUx3D(exception),
                "blueprint.dependencies.invalid", $"blueprints/{source.Id}/authoring/dependencies");
        }

        var frozen = draft with { Dependencies = authoring.Dependencies.ToArray() };
        var dryCommand = DependencyCommandUx3D(source, authoring, draft.BaseRevision, true);
        var before = Project;
        var beforeRevision = History.State.Revision;
        var dryRun = _upsertBlueprintUx3D.Handle(History.State, dryCommand);
        var dry = dryRun.Succeeded && ReferenceEquals(before, Project) && History.State.Revision == beforeRevision;
        if (!dryRun.Succeeded)
            return FailedDependenciesUx3D(dryRun.Diagnostics.FirstOrDefault()?.Message ?? "Commande refusée.",
                dryRun.Diagnostics.FirstOrDefault()?.Code ?? "blueprint.dependencies.command_rejected",
                $"blueprints/{source.Id}/authoring");

        var candidateResult = _upsertBlueprintUx3D.Handle(History.State,
            DependencyCommandUx3D(source, authoring, draft.BaseRevision, false));
        if (!candidateResult.Succeeded)
            return FailedDependenciesUx3D(candidateResult.Diagnostics.FirstOrDefault()?.Message ?? "Commande refusée.",
                candidateResult.Diagnostics.FirstOrDefault()?.Code ?? "blueprint.dependencies.command_rejected",
                $"blueprints/{source.Id}/authoring");

        var graph = ValidateDependencyCandidateUx3D(candidateResult.State.Project, draft.BlueprintId);
        if (!graph.Succeeded)
            return FailedDependenciesUx3D(graph.Message, graph.Diagnostics[0].Code, graph.Diagnostics[0].Path,
                graph.Diagnostics);

        var changedDependencies = !source.Authoring.Dependencies.SequenceEqual(authoring.Dependencies,
            StringComparer.Ordinal);
        var changedBudget = source.Authoring.PlatformByteBudget != authoring.PlatformByteBudget;
        if (!changedDependencies && !changedBudget)
            return new StudioUx3DDependencyPreview(true, false, dry, true, true,
                "Aucun changement de dépendance à appliquer.", dryRun.Patch?.Summary ?? string.Empty,
                dryRun.Inverse?.Summary ?? string.Empty, [], [], graph.NodeCount, graph.AutomaticPackages);

        _approvedDependencyDraftUx3D = frozen;
        _approvedDependencyRevisionUx3D = beforeRevision;
        _approvedDependencyProjectFingerprintUx3D = draft.SourceProjectFingerprint;
        _approvedDependencyDraftFingerprintUx3D = DependencyDraftFingerprintUx3D(frozen);
        var semantic = new List<string>();
        if (changedDependencies)
            semantic.Add($"Manifeste minimal · {source.Authoring.Dependencies.Count} → {authoring.Dependencies.Count} dépendance(s) déclarée(s).");
        if (changedBudget)
            semantic.Add($"Budget plateforme · {HumanBytesUx3D(source.Authoring.PlatformByteBudget)} → {HumanBytesUx3D(authoring.PlatformByteBudget)}.");
        semantic.Add($"Fermeture automatique · {graph.NodeCount} ressource(s) dans le graphe, {graph.AutomaticPackages.Count} package(s) externe(s).");
        return new StudioUx3DDependencyPreview(true, dry, dry, true, true,
            "Aperçu valide · le graphe a résolu chaque dépendance sans écrire le projet.",
            dryRun.Patch?.Summary ?? string.Empty, dryRun.Inverse?.Summary ?? string.Empty,
            semantic, [], graph.NodeCount, graph.AutomaticPackages);
    }

    internal StudioUx3DProvenancePreview PreviewProvenanceUx3D(StudioUx3DProvenanceDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelProvenanceApprovalUx3D();
        if (Project is null || History is null)
            return FailedProvenanceUx3D("Aucun module n’est ouvert.", "blueprint.provenance.project_missing", "project");
        if (!draft.Approved)
            return FailedProvenanceUx3D("Approuvez la provenance avant l’aperçu sec.",
                "blueprint.provenance.approval_required", "blueprints/provenance");
        if (draft.BaseRevision != History.State.Revision ||
            draft.SourceProjectFingerprint != ProjectFingerprintUx3A(Project))
            return FailedProvenanceUx3D("Le projet a changé ; rechargez le blueprint.",
                "blueprint.provenance.project_stale", "project");
        var source = Project.Blueprints.FirstOrDefault(item => item.Id.Value == draft.BlueprintId);
        if (source is null)
            return FailedProvenanceUx3D("Le blueprint sélectionné n’existe plus.",
                "blueprint.provenance.missing", "blueprints");
        if (source.IsReadOnly())
            return FailedProvenanceUx3D("La provenance d’un blueprint Standard installé est protégée.",
                ConfigureBlueprintPaletteHandler.ReadOnlyCode, $"blueprints/{source.Id}/provenance");

        BlueprintProvenance provenance;
        try
        {
            provenance = BuildProvenanceUx3D(draft);
        }
        catch (ArgumentException)
        {
            return FailedProvenanceUx3D("Source, auteur, licence, hash et preuve de permission sont obligatoires.",
                "blueprint.provenance.field_required", $"blueprints/{source.Id}/provenance");
        }
        var frozen = draft with { Modifications = provenance.Modifications.ToArray() };
        var dryCommand = ProvenanceCommandUx3D(source, provenance, draft.BaseRevision, true);
        var before = Project;
        var beforeRevision = History.State.Revision;
        var dryRun = _configureBlueprintPaletteUx3D.Handle(History.State, dryCommand);
        var dry = dryRun.Succeeded && ReferenceEquals(before, Project) && History.State.Revision == beforeRevision;
        if (!dryRun.Succeeded)
            return FailedProvenanceUx3D(dryRun.Diagnostics.FirstOrDefault()?.Message ?? "Commande refusée.",
                dryRun.Diagnostics.FirstOrDefault()?.Code ?? "blueprint.provenance.command_rejected",
                $"blueprints/{source.Id}/provenance");

        var candidate = _configureBlueprintPaletteUx3D.Handle(History.State,
            ProvenanceCommandUx3D(source, provenance, draft.BaseRevision, false));
        if (!candidate.Succeeded)
            return FailedProvenanceUx3D(candidate.Diagnostics.FirstOrDefault()?.Message ?? "Commande refusée.",
                candidate.Diagnostics.FirstOrDefault()?.Code ?? "blueprint.provenance.command_rejected",
                $"blueprints/{source.Id}/provenance");
        var packagePlan = _blueprintDropPlannerUx3D.Plan(candidate.State.Project, new BlueprintDropRequest(
            Guid.NewGuid(), BlueprintDropPlanner.CurrentSchemaVersion, "studio:ux3-d-provenance",
            source.Id, BlueprintDropTargetKind.Package, "module/distribution/blueprints"));
        if (!packagePlan.IsValid)
            return FailedProvenanceUx3D(HumanProvenanceMessageUx3D(packagePlan.Code, packagePlan.Message),
                packagePlan.Code, $"blueprints/{source.Id}/provenance");

        var selectedPath = $"blueprints/{candidate.State.Project.Blueprints.ToList().FindIndex(item => item.Id == source.Id)}/provenance";
        var compliance = DistributionComplianceValidator.Validate(candidate.State.Project, ModuleTargetKind.Shipping)
            .Where(item => item.Path.StartsWith(selectedPath, StringComparison.Ordinal))
            .Select(item => new StudioUx3DDiagnostic(item.Code, HumanPathUx3D(item.Path), item.Message))
            .ToArray();
        if (compliance.Length > 0)
            return FailedProvenanceUx3D("La distribution Shipping refuse encore la provenance choisie.",
                compliance[0].Code, compliance[0].Path, compliance);

        var changed = !SameProvenanceUx3D(source.Provenance, provenance);
        if (!changed)
            return new StudioUx3DProvenancePreview(true, false, dry, true, true,
                "Aucun changement de provenance à appliquer.", dryRun.Patch?.Summary ?? string.Empty,
                dryRun.Inverse?.Summary ?? string.Empty, [], []);

        _approvedProvenanceDraftUx3D = frozen;
        _approvedProvenanceRevisionUx3D = beforeRevision;
        _approvedProvenanceProjectFingerprintUx3D = draft.SourceProjectFingerprint;
        _approvedProvenanceDraftFingerprintUx3D = ProvenanceDraftFingerprintUx3D(frozen);
        return new StudioUx3DProvenancePreview(true, dry, dry, true, true,
            "Aperçu valide · licence, permission et SHA-256 autorisent l’inclusion en distribution.",
            dryRun.Patch?.Summary ?? string.Empty, dryRun.Inverse?.Summary ?? string.Empty,
            [
                $"Source · {HumanValueUx3D(source.Provenance.Source)} → {HumanValueUx3D(provenance.Source)}.",
                $"Licence · {source.Provenance.LicenseSpdx} → {provenance.LicenseSpdx}.",
                $"Permission · {HumanValueUx3D(source.Provenance.PermissionEvidence)} → {HumanValueUx3D(provenance.PermissionEvidence)}.",
                $"Origine IA · {(source.Provenance.IsAiGenerated ? "oui" : "non")} → {(provenance.IsAiGenerated ? "oui" : "non")}.",
                $"Modifications · {source.Provenance.Modifications.Count} → {provenance.Modifications.Count} entrée(s).",
            ], []);
    }

    internal StudioUx3DActionResult ApplyApprovedDependenciesUx3D(StudioUx3DDependencyDraft current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (Project is null || History is null || _approvedDependencyDraftUx3D is null ||
            _approvedDependencyRevisionUx3D != History.State.Revision ||
            _approvedDependencyProjectFingerprintUx3D != ProjectFingerprintUx3A(Project) ||
            _approvedDependencyDraftFingerprintUx3D != DependencyDraftFingerprintUx3D(FreezeDependencyDraftUx3D(current)))
            return new(false, "Aperçu des dépendances périmé · relancez l’aperçu sec.");
        var source = Project.Blueprints.FirstOrDefault(item => item.Id.Value == current.BlueprintId);
        if (source is null) return new(false, "Le blueprint sélectionné n’existe plus.");
        BlueprintAuthoringMetadata authoring;
        try
        {
            authoring = CopyAuthoringUx3D(source.Authoring, _approvedDependencyDraftUx3D.Dependencies,
                _approvedDependencyDraftUx3D.PlatformByteBudget);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return new(false, HumanDependencyExceptionUx3D(exception));
        }
        var result = _upsertBlueprintUx3D.Handle(History.State,
            DependencyCommandUx3D(source, authoring, History.State.Revision, false));
        if (!result.Succeeded)
            return new(false, result.Diagnostics.FirstOrDefault()?.Message ?? "Commande refusée.");
        var graph = ValidateDependencyCandidateUx3D(result.State.Project, current.BlueprintId);
        if (!graph.Succeeded) return new(false, graph.Message);
        History.Record(result);
        CancelUx3DApprovals();
        return new(true, "Dépendances appliquées par upsert_blueprint · Ctrl+Z restaure le document canonique.");
    }

    internal StudioUx3DActionResult ApplyApprovedProvenanceUx3D(StudioUx3DProvenanceDraft current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (Project is null || History is null || _approvedProvenanceDraftUx3D is null ||
            _approvedProvenanceRevisionUx3D != History.State.Revision ||
            _approvedProvenanceProjectFingerprintUx3D != ProjectFingerprintUx3A(Project) ||
            _approvedProvenanceDraftFingerprintUx3D != ProvenanceDraftFingerprintUx3D(FreezeProvenanceDraftUx3D(current)))
            return new(false, "Aperçu de provenance périmé · relancez l’aperçu sec.");
        var source = Project.Blueprints.FirstOrDefault(item => item.Id.Value == current.BlueprintId);
        if (source is null) return new(false, "Le blueprint sélectionné n’existe plus.");
        var provenance = BuildProvenanceUx3D(_approvedProvenanceDraftUx3D);
        var result = _configureBlueprintPaletteUx3D.Handle(History.State,
            ProvenanceCommandUx3D(source, provenance, History.State.Revision, false));
        if (!result.Succeeded)
            return new(false, result.Diagnostics.FirstOrDefault()?.Message ?? "Commande refusée.");
        var packagePlan = _blueprintDropPlannerUx3D.Plan(result.State.Project, new BlueprintDropRequest(
            Guid.NewGuid(), BlueprintDropPlanner.CurrentSchemaVersion, "studio:ux3-d-provenance",
            source.Id, BlueprintDropTargetKind.Package, "module/distribution/blueprints"));
        if (!packagePlan.IsValid) return new(false, HumanProvenanceMessageUx3D(packagePlan.Code, packagePlan.Message));
        History.Record(result);
        CancelUx3DApprovals();
        return new(true, "Provenance appliquée par configure_blueprint_palette · Ctrl+Z restaure l’état précédent.");
    }

    internal StudioUx3DActionResult UndoGovernanceUx3D()
    {
        var result = Undo();
        CancelUx3DApprovals();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx3DActionResult RedoGovernanceUx3D()
    {
        var result = Redo();
        CancelUx3DApprovals();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx3DActionResult SaveGovernanceUx3D()
    {
        var result = Save();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx3DApprovalSnapshot CaptureBlueprintGovernanceApprovalUx3D() => new(
        _approvedDependencyDraftUx3D, _approvedDependencyRevisionUx3D,
        _approvedDependencyProjectFingerprintUx3D, _approvedDependencyDraftFingerprintUx3D,
        _approvedProvenanceDraftUx3D, _approvedProvenanceRevisionUx3D,
        _approvedProvenanceProjectFingerprintUx3D, _approvedProvenanceDraftFingerprintUx3D);

    internal void RestoreBlueprintGovernanceApprovalUx3D(StudioUx3DApprovalSnapshot snapshot)
    {
        _approvedDependencyDraftUx3D = snapshot.DependencyDraft;
        _approvedDependencyRevisionUx3D = snapshot.DependencyRevision;
        _approvedDependencyProjectFingerprintUx3D = snapshot.DependencyProjectFingerprint;
        _approvedDependencyDraftFingerprintUx3D = snapshot.DependencyDraftFingerprint;
        _approvedProvenanceDraftUx3D = snapshot.ProvenanceDraft;
        _approvedProvenanceRevisionUx3D = snapshot.ProvenanceRevision;
        _approvedProvenanceProjectFingerprintUx3D = snapshot.ProvenanceProjectFingerprint;
        _approvedProvenanceDraftFingerprintUx3D = snapshot.ProvenanceDraftFingerprint;
    }

    internal void CloseUx3D() => CancelUx3DApprovals();

    internal StudioUx3DEvidenceProject? CreateBlueprintGovernanceEvidenceProjectUx3D()
    {
        var baseEvidence = CreateBlueprintEvidenceProjectUx3A();
        if (baseEvidence is null) return null;
        var supportId = BlueprintId.From(StableGuidUx3A(baseEvidence.Project.Id.Value, "ux3-d-support-blueprint"));
        const string supportManifest = "content/blueprints/support-governance.blueprint.json";
        const string assetPath = "content/ux3-d/shared-material.asset.json";
        var supportProvenance = new BlueprintProvenance(BlueprintProvenance.CurrentSchemaVersion,
            "project://evidence/ux3-d/support", "OpenRPG Forge evidence", "MIT", new string('b', 64),
            modifications: ["Isolat de preuve UX3-D"], permissionEvidence: "spdx:MIT");
        var supportAuthoring = new BlueprintAuthoringMetadata(BlueprintAuthoringMetadata.CurrentSchemaVersion,
            "1.0.0", "project.ux3_d_support", supportManifest, dependencies: [assetPath],
            platformByteBudget: 524_288, kit: "ux3-d-evidence", tags: ["governance", "support"]);
        var support = new BlueprintDefinition(supportId, "support-governance", "Support gouverné",
            BlueprintKind.Placeable, provenance: supportProvenance, authoring: supportAuthoring);
        var blueprints = baseEvidence.Project.Blueprints.Where(item => item.Id != supportId).Append(support).ToArray();
        var project = new ProjectDefinition(baseEvidence.Project.Id, baseEvidence.Project.Title,
            baseEvidence.Project.Module, blueprints, baseEvidence.Project.Areas,
            baseEvidence.Project.Dialogues, baseEvidence.Project.Quests);
        return new StudioUx3DEvidenceProject(project, baseEvidence.EditableBlueprintId, supportId.Value,
            supportManifest, assetPath, false, true);
    }

    internal static bool MaterializeGovernanceEvidenceFilesUx3D(string root, StudioUx3DEvidenceProject evidence)
    {
        try
        {
            var asset = Path.Combine(root, evidence.AssetDependencyPath.Replace('/', Path.DirectorySeparatorChar));
            var manifest = Path.Combine(root, evidence.SupportManifestPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(asset)!);
            Directory.CreateDirectory(Path.GetDirectoryName(manifest)!);
            File.WriteAllText(asset,
                "{\"schemaVersion\":1,\"provenance\":\"studio:ux3-d\",\"licenseSpdx\":\"MIT\"}",
                new UTF8Encoding(false));
            File.WriteAllText(manifest,
                "{\"schemaVersion\":1,\"provenance\":\"studio:ux3-d-support\"}",
                new UTF8Encoding(false));
            return File.Exists(asset) && File.Exists(manifest);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal StudioUx3DSmokeResult RunUx3DContractSmoke()
    {
        var empty = EmptyGovernanceSmokeUx3D();
        var evidence = CreateBlueprintGovernanceEvidenceProjectUx3D();
        if (evidence is null) return empty;
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux3-d-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        StudioUx3DSmokeResult Execute()
        {
            try
            {
                if (!ProjectSession.Create(root, evidence.Project).Succeeded ||
                    !MaterializeGovernanceEvidenceFilesUx3D(root, evidence) || !editing.Open(root).Succeeded)
                    return empty;
                var choices = editing.BlueprintGovernanceChoicesUx3D;
                var explicitSelection = editing.CreateDependencyDraftUx3D(Guid.Empty) is null && choices.Count >= 2;
                var human = choices.All(item => !item.Label.Contains(item.Id.ToString("D"),
                    StringComparison.OrdinalIgnoreCase));
                var dependency = editing.CreateDependencyDraftUx3D(evidence.EditableBlueprintId);
                if (dependency is null) return empty;
                var invalidDependency = editing.PreviewDependenciesUx3D(dependency with
                {
                    Dependencies = ["content/ux3-d/missing.asset.json"],
                    Approved = true,
                });
                var missingBlocked = !invalidDependency.Succeeded &&
                                     invalidDependency.Diagnostics.Any(item => item.Path.Contains("dépendances", StringComparison.OrdinalIgnoreCase));
                var configuredDependency = ChangedDependencyDraftUx3D(dependency, evidence);
                var sourceProject = editing.Project!;
                var sourceJson = ProjectJsonSerializer.Serialize(sourceProject);
                var sourceTarget = sourceProject.Blueprints.Single(item => item.Id.Value == evidence.EditableBlueprintId);
                var otherBlueprints = sourceProject.Blueprints.Where(item => item.Id.Value != evidence.EditableBlueprintId).ToArray();
                var beforeRevision = editing.History!.State.Revision;
                var dependencyPreview = editing.PreviewDependenciesUx3D(configuredDependency);
                var dryRun = dependencyPreview.Succeeded && ReferenceEquals(sourceProject, editing.Project) &&
                             editing.History.State.Revision == beforeRevision;
                var transitive = dependencyPreview.SemanticDiff.Any(item => item.Contains("Fermeture automatique", StringComparison.Ordinal));

                var staleApproval = dependencyPreview.Succeeded &&
                    !editing.ApplyApprovedDependenciesUx3D(configuredDependency with
                    {
                        PlatformByteBudget = configuredDependency.PlatformByteBudget + 1,
                    }).Succeeded;
                dependencyPreview = editing.PreviewDependenciesUx3D(configuredDependency);
                var concurrent = editing._configureBlueprintPaletteUx3D.Handle(editing.History.State,
                    new ConfigureBlueprintPaletteCommand(CommandId.From(Guid.NewGuid()), editing.History.State.Revision,
                        sourceTarget.Id, sourceTarget.PaletteOrigin, sourceTarget.IsFavorite,
                        sourceTarget.Provenance, false));
                var staleSource = false;
                if (concurrent.Succeeded)
                {
                    editing.History.Record(concurrent);
                    staleSource = !editing.ApplyApprovedDependenciesUx3D(configuredDependency).Succeeded;
                    editing.Undo();
                }

                dependency = editing.CreateDependencyDraftUx3D(evidence.EditableBlueprintId);
                if (dependency is null) return empty;
                configuredDependency = ChangedDependencyDraftUx3D(dependency, evidence);
                dependencyPreview = editing.PreviewDependenciesUx3D(configuredDependency);
                var provenanceBeforeDependency = editing.CreateProvenanceDraftUx3D(evidence.EditableBlueprintId);
                if (provenanceBeforeDependency is null) return empty;
                var validProvenanceBeforeDependency = ChangedProvenanceDraftUx3D(provenanceBeforeDependency);
                var provenancePreviewBeforeDependency = editing.PreviewProvenanceUx3D(validProvenanceBeforeDependency);
                var approvals = editing.CaptureBlueprintGovernanceApprovalUx3D();
                var separateApprovals = dependencyPreview.Succeeded && provenancePreviewBeforeDependency.Succeeded &&
                                        approvals.DependencyDraft is not null && approvals.ProvenanceDraft is not null;
                var dependencyApplied = editing.ApplyApprovedDependenciesUx3D(configuredDependency);
                var afterDependency = editing.Project!;
                var dependencyJson = ProjectJsonSerializer.Serialize(afterDependency);
                var targetAfterDependency = afterDependency.Blueprints.Single(item => item.Id.Value == evidence.EditableBlueprintId);
                var authoringTails = SameAuthoringTailsUx3D(sourceTarget.Authoring, targetAfterDependency.Authoring) &&
                                     targetAfterDependency.Authoring.Dependencies.Contains(evidence.SupportManifestPath,
                                         StringComparer.Ordinal);
                var graphAfter = WorkspaceBuildGraphBuilder.Build(root, afterDependency,
                    editing.Session!.ProjectFilePath);
                var graphResolved = graphAfter.Graph.Nodes.Any(item => item.Path == evidence.AssetDependencyPath);

                var provenance = editing.CreateProvenanceDraftUx3D(evidence.EditableBlueprintId);
                if (provenance is null) return empty;
                var invalidProvenance = editing.PreviewProvenanceUx3D(provenance with
                {
                    LicenseSpdx = "UNLICENSED",
                    ContentHash = "unverified",
                    Approved = true,
                });
                var invalidProvenanceBlocked = !invalidProvenance.Succeeded;
                var configuredProvenance = ChangedProvenanceDraftUx3D(provenance);
                var provenancePreview = editing.PreviewProvenanceUx3D(configuredProvenance);
                var distributionReady = provenancePreview.Succeeded && provenancePreview.DistributionReady;
                var provenanceApplied = editing.ApplyApprovedProvenanceUx3D(configuredProvenance);
                var finalProject = editing.Project!;
                var finalJson = ProjectJsonSerializer.Serialize(finalProject);
                var finalTarget = finalProject.Blueprints.Single(item => item.Id.Value == evidence.EditableBlueprintId);
                var blueprintTails = SameBlueprintTailsUx3D(sourceTarget, finalTarget);
                var blueprintNeighbors = otherBlueprints.SequenceEqual(finalProject.Blueprints
                    .Where(item => item.Id.Value != evidence.EditableBlueprintId));
                var projectNeighbors = finalProject.Id == sourceProject.Id && finalProject.Title == sourceProject.Title &&
                                       finalProject.Module == sourceProject.Module &&
                                       finalProject.Areas.SequenceEqual(sourceProject.Areas) &&
                                       finalProject.Dialogues.SequenceEqual(sourceProject.Dialogues) &&
                                       finalProject.Quests.SequenceEqual(sourceProject.Quests);
                var canonical = dependencyApplied.Succeeded && provenanceApplied.Succeeded &&
                                editing.History.Journal.Any(item => item.CommandName == "upsert_blueprint") &&
                                editing.History.Journal.Any(item => item.CommandName == "configure_blueprint_palette");
                var undoProvenance = editing.UndoGovernanceUx3D();
                var provenanceUndone = undoProvenance.Succeeded &&
                                       ProjectJsonSerializer.Serialize(editing.Project!) == dependencyJson;
                var undoDependencies = editing.UndoGovernanceUx3D();
                var dependenciesUndone = undoDependencies.Succeeded &&
                                         ProjectJsonSerializer.Serialize(editing.Project!) == sourceJson;
                var redoDependencies = editing.RedoGovernanceUx3D();
                var redoProvenance = editing.RedoGovernanceUx3D();
                var redone = redoDependencies.Succeeded && redoProvenance.Succeeded &&
                             ProjectJsonSerializer.Serialize(editing.Project!) == finalJson;
                var saved = editing.SaveGovernanceUx3D();
                editing.Close();
                var persistence = saved.Succeeded && reopened.Open(root).Succeeded &&
                                  ProjectJsonSerializer.Serialize(reopened.Project!) == finalJson;
                reopened.Close();
                var succeeded = explicitSelection && evidence.UsesCanonicalFallback && missingBlocked && transitive &&
                                graphResolved && invalidProvenanceBlocked && distributionReady && dryRun && staleSource &&
                                staleApproval && separateApprovals && canonical && provenanceUndone && dependenciesUndone &&
                                redone && persistence && authoringTails && blueprintTails && blueprintNeighbors &&
                                projectNeighbors && human;
                return new StudioUx3DSmokeResult(succeeded, explicitSelection, evidence.UsesRealModule,
                    evidence.UsesCanonicalFallback, missingBlocked, transitive, graphResolved,
                    invalidProvenanceBlocked, distributionReady, dryRun, staleSource, staleApproval,
                    separateApprovals, canonical, provenanceUndone && dependenciesUndone && redone,
                    persistence, authoringTails, blueprintTails, blueprintNeighbors, projectNeighbors,
                    human, true, false);
            }
            catch
            {
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

    internal static StudioUx3DDependencyDraft ChangedDependencyDraftUx3D(
        StudioUx3DDependencyDraft draft, StudioUx3DEvidenceProject evidence) => draft with
        {
            Dependencies = [evidence.SupportManifestPath],
            PlatformByteBudget = 2_097_152,
            Approved = true,
        };

    internal static StudioUx3DProvenanceDraft ChangedProvenanceDraftUx3D(StudioUx3DProvenanceDraft draft) => draft with
    {
        Source = "project://studio/ux3-d/governed-blueprint",
        Author = "Équipe du module",
        LicenseSpdx = "MIT",
        ContentHash = new string('d', 64),
        PermissionEvidence = "spdx:MIT",
        IsAiGenerated = false,
        Modifications = ["Dépendances vérifiées", "Provenance revue dans Forge Studio"],
        Approved = true,
    };

    internal string DependencyDraftFingerprintUx3D(StudioUx3DDependencyDraft draft)
    {
        var frozen = FreezeDependencyDraftUx3D(draft);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(frozen.BaseRevision.Value);
        WriteBlueprintUx3A(writer, frozen.SourceProjectFingerprint);
        writer.Write(frozen.BlueprintId.ToByteArray());
        WriteBlueprintStringsUx3A(writer, frozen.Dependencies);
        writer.Write(frozen.PlatformByteBudget);
        writer.Write(frozen.Approved);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    internal string ProvenanceDraftFingerprintUx3D(StudioUx3DProvenanceDraft draft)
    {
        var frozen = FreezeProvenanceDraftUx3D(draft);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(frozen.BaseRevision.Value);
        WriteBlueprintUx3A(writer, frozen.SourceProjectFingerprint);
        writer.Write(frozen.BlueprintId.ToByteArray());
        WriteBlueprintUx3A(writer, frozen.Source);
        WriteBlueprintUx3A(writer, frozen.Author);
        WriteBlueprintUx3A(writer, frozen.LicenseSpdx);
        WriteBlueprintUx3A(writer, frozen.ContentHash);
        WriteBlueprintUx3A(writer, frozen.PermissionEvidence);
        writer.Write(frozen.IsAiGenerated);
        WriteBlueprintStringsUx3A(writer, frozen.Modifications);
        writer.Write(frozen.Approved);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private DependencyGraphResultUx3D ValidateDependencyCandidateUx3D(ProjectDefinition candidate, Guid blueprintId)
    {
        if (Session is null)
            return DependencyGraphResultUx3D.Failed("Le workspace n’est pas ouvert.",
                "blueprint.dependencies.workspace_missing", "workspace");
        var blueprint = candidate.Blueprints.First(item => item.Id.Value == blueprintId);
        var manifestIndex = candidate.Blueprints.ToDictionary(item => item.Authoring.ManifestPath,
            item => item, StringComparer.Ordinal);
        foreach (var dependency in blueprint.Authoring.Dependencies)
        {
            if (dependency == blueprint.Authoring.ManifestPath)
                return DependencyGraphResultUx3D.Failed("Un blueprint ne peut pas dépendre de son propre manifeste.",
                    "blueprint.dependencies.self_reference", "dépendances / manifeste");
            if (dependency.StartsWith("pack://", StringComparison.Ordinal))
            {
                var segments = dependency[7..].Split('/', StringSplitOptions.RemoveEmptyEntries);
                var resolved = segments.Length >= 2 && (candidate.Module.Content.Packages.Packages.Any(item =>
                    item.PackageNamespace == segments[0] && item.Version == segments[1]) ||
                    candidate.Blueprints.Any(item => item.IsReadOnly() &&
                        item.Provenance.Source.StartsWith($"pack://{segments[0]}/{segments[1]}/", StringComparison.Ordinal)));
                if (!resolved)
                    return DependencyGraphResultUx3D.Failed(
                        $"Le package déclaré « {HumanValueUx3D(dependency)} » n’est pas installé.",
                        "blueprint.dependencies.package_missing", "dépendances / package");
                continue;
            }
            if (manifestIndex.ContainsKey(dependency)) continue;
            var absolute = Path.GetFullPath(Path.Combine(Session.RootDirectory,
                dependency.Replace('/', Path.DirectorySeparatorChar)));
            var root = Path.GetFullPath(Session.RootDirectory) + Path.DirectorySeparatorChar;
            if (!absolute.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(absolute))
                return DependencyGraphResultUx3D.Failed(
                    $"La dépendance locale « {HumanValueUx3D(dependency)} » est absente du workspace.",
                    "blueprint.dependencies.file_missing", "dépendances / fichier local");
        }
        try
        {
            var graph = WorkspaceBuildGraphBuilder.Build(Session.RootDirectory, candidate, Session.ProjectFilePath);
            return new DependencyGraphResultUx3D(true,
                $"Graphe résolu · {graph.Graph.Nodes.Count} ressource(s).", [], graph.Graph.Nodes.Count,
                graph.AutomaticDependencies);
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException)
        {
            return DependencyGraphResultUx3D.Failed(HumanDependencyExceptionUx3D(exception),
                "blueprint.dependencies.build_graph_invalid", "dépendances / graphe de build");
        }
    }

    private static BlueprintAuthoringMetadata CopyAuthoringUx3D(
        BlueprintAuthoringMetadata source, IReadOnlyList<string> dependencies, long platformByteBudget) => new(
        source.SchemaVersion, source.Version, source.PackageNamespace, source.ManifestPath,
        source.ExposedParameters, dependencies, source.PreviewPath, platformByteBudget, source.Kit,
        source.Styles, source.Compatibility, source.Tags, source.CategoryPaths, source.PromotedDefaults,
        source.AuthorCollections, source.PrefabVariant, source.PrefabComposition, source.AssetBudget);

    private static UpsertBlueprintCommand DependencyCommandUx3D(
        BlueprintDefinition source, BlueprintAuthoringMetadata authoring, ProjectRevision revision, bool dryRun) => new(
        CommandId.From(Guid.NewGuid()), revision, source.Id, source.Slug, source.DisplayName,
        source.Kind, dryRun, authoring);

    private static ConfigureBlueprintPaletteCommand ProvenanceCommandUx3D(
        BlueprintDefinition source, BlueprintProvenance provenance, ProjectRevision revision, bool dryRun) => new(
        CommandId.From(Guid.NewGuid()), revision, source.Id, source.PaletteOrigin, source.IsFavorite,
        provenance, dryRun);

    private static BlueprintProvenance BuildProvenanceUx3D(StudioUx3DProvenanceDraft draft) => new(
        BlueprintProvenance.CurrentSchemaVersion, draft.Source, draft.Author, draft.LicenseSpdx,
        draft.ContentHash, draft.IsAiGenerated, draft.Modifications, draft.PermissionEvidence);

    private static StudioUx3DDependencyDraft FreezeDependencyDraftUx3D(StudioUx3DDependencyDraft draft) => draft with
    {
        Dependencies = draft.Dependencies.Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim().Replace('\\', '/')).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray(),
    };

    private static StudioUx3DProvenanceDraft FreezeProvenanceDraftUx3D(StudioUx3DProvenanceDraft draft) => draft with
    {
        Source = draft.Source.Trim(),
        Author = draft.Author.Trim(),
        LicenseSpdx = draft.LicenseSpdx.Trim(),
        ContentHash = draft.ContentHash.Trim().ToLowerInvariant(),
        PermissionEvidence = draft.PermissionEvidence.Trim(),
        Modifications = draft.Modifications.Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
    };

    private void CancelUx3DApprovals()
    {
        CancelDependencyApprovalUx3D();
        CancelProvenanceApprovalUx3D();
    }

    private void CancelDependencyApprovalUx3D()
    {
        _approvedDependencyDraftUx3D = null;
        _approvedDependencyRevisionUx3D = null;
        _approvedDependencyProjectFingerprintUx3D = null;
        _approvedDependencyDraftFingerprintUx3D = null;
    }

    private void CancelProvenanceApprovalUx3D()
    {
        _approvedProvenanceDraftUx3D = null;
        _approvedProvenanceRevisionUx3D = null;
        _approvedProvenanceProjectFingerprintUx3D = null;
        _approvedProvenanceDraftFingerprintUx3D = null;
    }

    private static bool SameProvenanceUx3D(BlueprintProvenance left, BlueprintProvenance right) =>
        left.SchemaVersion == right.SchemaVersion && left.Source == right.Source && left.Author == right.Author &&
        left.LicenseSpdx == right.LicenseSpdx && left.ContentHash == right.ContentHash &&
        left.PermissionEvidence == right.PermissionEvidence && left.IsAiGenerated == right.IsAiGenerated &&
        left.Modifications.SequenceEqual(right.Modifications, StringComparer.Ordinal);

    private static bool SameAuthoringTailsUx3D(BlueprintAuthoringMetadata left, BlueprintAuthoringMetadata right) =>
        left.SchemaVersion == right.SchemaVersion && left.Version == right.Version &&
        left.PackageNamespace == right.PackageNamespace && left.ManifestPath == right.ManifestPath &&
        left.ExposedParameters.SequenceEqual(right.ExposedParameters, StringComparer.Ordinal) &&
        left.PreviewPath == right.PreviewPath && left.Kit == right.Kit &&
        left.Styles.SequenceEqual(right.Styles, StringComparer.Ordinal) &&
        left.Compatibility.SequenceEqual(right.Compatibility, StringComparer.Ordinal) &&
        left.Tags.SequenceEqual(right.Tags, StringComparer.Ordinal) &&
        left.CategoryPaths.SequenceEqual(right.CategoryPaths, StringComparer.Ordinal) &&
        left.PromotedDefaults.SequenceEqual(right.PromotedDefaults) &&
        left.AuthorCollections.SequenceEqual(right.AuthorCollections, StringComparer.Ordinal) &&
        Equals(left.PrefabVariant, right.PrefabVariant) && Equals(left.PrefabComposition, right.PrefabComposition) &&
        Equals(left.AssetBudget, right.AssetBudget);

    private static bool SameBlueprintTailsUx3D(BlueprintDefinition left, BlueprintDefinition right) =>
        left.Id == right.Id && left.Slug == right.Slug && left.DisplayName == right.DisplayName &&
        left.Kind == right.Kind && left.Combat == right.Combat && left.PaletteOrigin == right.PaletteOrigin &&
        left.IsFavorite == right.IsFavorite && left.CreaturePresentation == right.CreaturePresentation &&
        left.CreatureProgression == right.CreatureProgression && left.CreatureLoot == right.CreatureLoot &&
        left.CreatureRoleBehavior == right.CreatureRoleBehavior && left.CreatureEvents == right.CreatureEvents &&
        left.ItemAdvanced == right.ItemAdvanced && left.CreatureFoundation == right.CreatureFoundation &&
        left.ItemFoundation == right.ItemFoundation && SameAuthoringTailsUx3D(left.Authoring, right.Authoring);

    private static string HumanDependencyExceptionUx3D(Exception exception) => exception.Message
        .Replace("Blueprint", "Blueprint", StringComparison.Ordinal)
        .Replace("dependency", "dépendance", StringComparison.OrdinalIgnoreCase)
        .Replace("workspace file", "fichier du workspace", StringComparison.OrdinalIgnoreCase)
        .Replace("blueprint manifest", "manifeste de blueprint", StringComparison.OrdinalIgnoreCase);

    private static string HumanProvenanceMessageUx3D(string code, string fallback) => code switch
    {
        BlueprintDropPlanner.PackageGovernanceCode =>
            "La distribution exige une licence SPDX approuvée, une source, un auteur, une permission et un SHA-256 complet.",
        _ => fallback,
    };

    private static string HumanPathUx3D(string path) => path
        .Replace("blueprints/", "blueprints / ", StringComparison.Ordinal)
        .Replace("/provenance", " / provenance", StringComparison.Ordinal)
        .Replace("/licenseSpdx", " / licence SPDX", StringComparison.Ordinal)
        .Replace("/permissionEvidence", " / preuve de permission", StringComparison.Ordinal);

    private static string HumanValueUx3D(string value)
    {
        var sanitized = Regex.Replace(value,
            "(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
            "identifiant-protege", RegexOptions.CultureInvariant);
        return sanitized.Length <= 88 ? sanitized : sanitized[..85] + "…";
    }

    private static string HumanBytesUx3D(long value) => value >= 1_048_576
        ? $"{value / 1_048_576d:0.##} Mio"
        : $"{value / 1024d:0.##} Kio";

    private static StudioUx3DDependencyPreview FailedDependenciesUx3D(
        string message, string code, string path, IReadOnlyList<StudioUx3DDiagnostic>? diagnostics = null) => new(
        false, false, false, false, true, message, string.Empty, string.Empty, [],
        diagnostics ?? [new StudioUx3DDiagnostic(code, path, message)], 0, []);

    private static StudioUx3DProvenancePreview FailedProvenanceUx3D(
        string message, string code, string path, IReadOnlyList<StudioUx3DDiagnostic>? diagnostics = null) => new(
        false, false, false, false, true, message, string.Empty, string.Empty, [],
        diagnostics ?? [new StudioUx3DDiagnostic(code, path, message)]);

    private static StudioUx3DSmokeResult EmptyGovernanceSmokeUx3D() => new(false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, true, false);

    private sealed record DependencyGraphResultUx3D(
        bool Succeeded,
        string Message,
        IReadOnlyList<StudioUx3DDiagnostic> Diagnostics,
        int NodeCount,
        IReadOnlyList<string> AutomaticPackages)
    {
        internal static DependencyGraphResultUx3D Failed(string message, string code, string path) =>
            new(false, message, [new StudioUx3DDiagnostic(code, path, message)], 0, []);
    }
}
