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
    private readonly UpsertBlueprintHandler _upsertBlueprintUx3A = new(new SystemClock());
    private readonly CreateBlueprintFromEntityHandler _deriveBlueprintUx3A = new(new SystemClock());
    private StudioUx3ADraft? _approvedBlueprintDraftUx3A;
    private ProjectRevision? _approvedBlueprintRevisionUx3A;
    private string? _approvedBlueprintProjectFingerprintUx3A;
    private string? _approvedBlueprintDraftFingerprintUx3A;

    internal IReadOnlyList<StudioUx3ABlueprintChoice> BlueprintChoicesUx3A
    {
        get
        {
            var project = Project;
            if (project is null) return [];
            var references = BlueprintReferenceIndex.Build(project);
            return BlueprintPaletteCatalog.Build(project, WorkspaceProfile).Items
                .Select(item => new StudioUx3ABlueprintChoice(item.BlueprintId.Value,
                    $"{item.DisplayName} · {HumanBlueprintKindUx3A(item.Kind)} · {HumanOriginUx3A(item.Origin)}",
                    item.Kind, item.Origin, item.IsReadOnly, item.IsFavorite,
                    project.Blueprints.Single(value => value.Id == item.BlueprintId).Authoring.Version,
                    references.Find(item.BlueprintId).Count))
                .OrderBy(item => item.Origin)
                .ThenBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    internal IReadOnlyList<StudioUx3AEntityChoice> EntityChoicesUx3A
    {
        get
        {
            var project = Project;
            if (project is null) return [];
            var blueprints = project.Blueprints.ToDictionary(item => item.Id);
            return project.Areas.SelectMany(area => area.Entities.Select(entity => (area, entity)))
                .Where(item => blueprints.ContainsKey(item.entity.BlueprintId))
                .Select(item =>
                {
                    var blueprint = blueprints[item.entity.BlueprintId];
                    return new StudioUx3AEntityChoice(item.area.Id.Value, item.entity.Id.Value,
                        blueprint.Id.Value,
                        $"{blueprint.DisplayName} · {item.area.DisplayName} · {item.entity.Overrides.Properties.Count} surcharge(s)",
                        item.entity.Overrides.Properties.Count, item.entity.BlueprintVersionRequirement);
                })
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    internal StudioUx3ADraft? CreateBlueprintDraftUx3A(Guid blueprintId)
    {
        var project = Project;
        var history = History;
        var blueprint = project?.Blueprints.SingleOrDefault(item => item.Id.Value == blueprintId);
        if (project is null || history is null || blueprint is null) return null;
        return DraftFromBlueprintUx3A(history.State.Revision, ProjectFingerprintUx3A(project), blueprint,
            StudioUx3AOperation.EditBlueprint, blueprint.Id.Value, null, null, blueprint.Id.Value, false);
    }

    internal StudioUx3ADraft? CreateDerivedBlueprintDraftUx3A(Guid areaId, Guid entityId)
    {
        var project = Project;
        var history = History;
        var area = project?.Areas.SingleOrDefault(item => item.Id.Value == areaId);
        var entity = area?.Entities.SingleOrDefault(item => item.Id.Value == entityId);
        var source = entity is null ? null : project?.Blueprints.SingleOrDefault(item => item.Id == entity.BlueprintId);
        if (project is null || history is null || area is null || entity is null || source is null) return null;
        var slug = NextDerivedSlugUx3A(project, source.Slug);
        var id = StableGuidUx3A(project.Id.Value, $"derive:{area.Id.Value:D}:{entity.Id.Value:D}:{slug}");
        return DraftFromBlueprintUx3A(history.State.Revision, ProjectFingerprintUx3A(project), source,
            StudioUx3AOperation.DeriveFromInstance, id, area.Id.Value, entity.Id.Value, source.Id.Value, false) with
        {
            Slug = slug,
            DisplayName = $"{source.DisplayName} — variante projet",
            PackageNamespace = $"project.{slug.Replace('-', '.')}",
            ManifestPath = $"content/blueprints/{id:D}/{slug}.blueprint.json",
            PreviewPath = $"content/blueprints/{id:D}/{slug}.preview.png",
        };
    }

    internal StudioUx3APreview PreviewBlueprintUx3A(StudioUx3ADraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelBlueprintApprovalUx3A();
        var project = Project;
        var history = History;
        if (project is null || history is null)
            return FailedBlueprintUx3A("Aucun module n’est ouvert.", "blueprint.project_missing");
        if (history.State.Revision != draft.BaseRevision ||
            !string.Equals(ProjectFingerprintUx3A(project), draft.SourceProjectFingerprint,
                StringComparison.Ordinal))
            return FailedBlueprintUx3A("Le projet a changé ; rechargez le blueprint.",
                UpsertBlueprintHandler.RevisionConflictCode);
        if (!draft.Approved)
            return FailedBlueprintUx3A("Cochez l’approbation auteur avant le dry-run.",
                "blueprint.approval_required");

        var existing = draft.Operation == StudioUx3AOperation.EditBlueprint
            ? project.Blueprints.SingleOrDefault(item => item.Id.Value == draft.BlueprintId)
            : project.Blueprints.SingleOrDefault(item => item.Id.Value == draft.SourceBlueprintId);
        if (existing is null)
            return FailedBlueprintUx3A("Le blueprint source n’existe plus.", "blueprint.source_missing");
        if (draft.Operation == StudioUx3AOperation.EditBlueprint && existing.IsReadOnly())
            return new(false, false, false, true, true,
                "Les blueprints Standard installés sont protégés en lecture seule.", string.Empty, string.Empty,
                ["Inspecter la définition Standard sans modifier son package."],
                [new StudioUx3ADiagnostic(UpsertBlueprintHandler.ReadOnlyCode,
                    $"blueprints/{existing.Slug}", "Dupliquez une instance vers la palette Projet pour l’éditer.")]);

        BlueprintAuthoringMetadata authoring;
        try
        {
            authoring = BuildAuthoringUx3A(draft, existing.Authoring);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return FailedBlueprintUx3A(exception.Message, "blueprint.authoring_invalid");
        }

        var result = HandleBlueprintUx3A(history.State, draft, authoring, dryRun: true);
        var diagnostics = result.Diagnostics.Select(item => new StudioUx3ADiagnostic(item.Code,
                draft.Operation == StudioUx3AOperation.DeriveFromInstance ? "instances/derivation" : "blueprints/definition",
                HumanBlueprintDiagnosticUx3A(item.Code, item.Message)))
            .ToArray();
        var label = draft.DisplayName.Trim();
        var diff = draft.Operation == StudioUx3AOperation.DeriveFromInstance
            ? new[]
            {
                $"Créer « {label} » dans la palette Projet.",
                "Retargeter uniquement l’instance sélectionnée, sans perdre transform, tags ni surcharges.",
                $"Épingler cette instance sur la version {draft.Version.Trim()}.",
            }
            : new[]
            {
                $"Mettre à jour « {label} » dans la palette Projet.",
                $"Version {existing.Authoring.Version} → {draft.Version.Trim()} · namespace {draft.PackageNamespace.Trim()}.",
                $"Préserver {existing.Authoring.PrefabComposition?.Nodes.Count ?? 0} nœud(s) de composition et tous les profils gameplay voisins.",
            };
        if (!result.Succeeded)
            return new(false, false, false, false, true,
                diagnostics.FirstOrDefault()?.Message ?? "Le dry-run refuse le blueprint.", string.Empty,
                string.Empty, diff, diagnostics);

        var frozen = FreezeBlueprintDraftUx3A(draft);
        _approvedBlueprintDraftUx3A = frozen;
        _approvedBlueprintRevisionUx3A = history.State.Revision;
        _approvedBlueprintProjectFingerprintUx3A = ProjectFingerprintUx3A(project);
        _approvedBlueprintDraftFingerprintUx3A = BlueprintDraftFingerprintUx3A(frozen);
        return new(true, true, true, false, true,
            draft.Operation == StudioUx3AOperation.DeriveFromInstance
                ? "Dérivation prête · instance retargetée seulement après application."
                : "Blueprint Projet prêt · définition réutilisable et métadonnées gouvernées.",
            result.Patch?.Summary ?? "Prévisualiser la mutation blueprint.",
            result.Inverse?.Summary ?? "Restaurer la définition précédente.", diff, diagnostics);
    }

    internal StudioUx3AActionResult ApplyApprovedBlueprintUx3A(StudioUx3ADraft current)
    {
        var project = Project;
        var history = History;
        var approved = _approvedBlueprintDraftUx3A;
        if (project is null || history is null || approved is null || _approvedBlueprintRevisionUx3A is null ||
            _approvedBlueprintProjectFingerprintUx3A is null || _approvedBlueprintDraftFingerprintUx3A is null)
            return new(false, "Prévisualisez et approuvez d’abord le blueprint.");
        var stale = history.State.Revision != _approvedBlueprintRevisionUx3A ||
                    !string.Equals(ProjectFingerprintUx3A(project), _approvedBlueprintProjectFingerprintUx3A,
                        StringComparison.Ordinal) ||
                    !string.Equals(BlueprintDraftFingerprintUx3A(approved), _approvedBlueprintDraftFingerprintUx3A,
                        StringComparison.Ordinal) ||
                    !string.Equals(BlueprintDraftFingerprintUx3A(current), _approvedBlueprintDraftFingerprintUx3A,
                        StringComparison.Ordinal);
        if (stale)
        {
            CancelBlueprintApprovalUx3A();
            return new(false, "Le brouillon ou le projet a changé ; relancez l’aperçu.");
        }
        var existing = approved.Operation == StudioUx3AOperation.EditBlueprint
            ? project.Blueprints.SingleOrDefault(item => item.Id.Value == approved.BlueprintId)
            : project.Blueprints.SingleOrDefault(item => item.Id.Value == approved.SourceBlueprintId);
        if (existing is null)
        {
            CancelBlueprintApprovalUx3A();
            return new(false, "Le blueprint source n’existe plus.");
        }
        BlueprintAuthoringMetadata authoring;
        try { authoring = BuildAuthoringUx3A(approved, existing.Authoring); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            CancelBlueprintApprovalUx3A();
            return new(false, exception.Message);
        }
        var dryRun = HandleBlueprintUx3A(history.State, approved, authoring, dryRun: true);
        if (!dryRun.Succeeded)
        {
            CancelBlueprintApprovalUx3A();
            return new(false, dryRun.Diagnostics.FirstOrDefault()?.Message ?? "Le re-dry-run refuse le blueprint.");
        }
        var result = HandleBlueprintUx3A(history.State, approved, authoring, dryRun: false);
        CancelBlueprintApprovalUx3A();
        if (!result.Succeeded)
            return new(false, result.Diagnostics.FirstOrDefault()?.Message ?? "Application du blueprint refusée.");
        history.Record(result);
        return new(true, result.Patch?.Summary ?? "Blueprint appliqué.");
    }

    internal StudioUx3AActionResult UndoBlueprintUx3A()
    {
        CancelBlueprintApprovalUx3A();
        var result = Undo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx3AActionResult RedoBlueprintUx3A()
    {
        CancelBlueprintApprovalUx3A();
        var result = Redo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx3AActionResult SaveBlueprintUx3A()
    {
        CancelBlueprintApprovalUx3A();
        var result = Save();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx3AApprovalSnapshot CaptureBlueprintApprovalUx3A() => new(
        _approvedBlueprintDraftUx3A, _approvedBlueprintRevisionUx3A,
        _approvedBlueprintProjectFingerprintUx3A, _approvedBlueprintDraftFingerprintUx3A);

    internal void RestoreBlueprintApprovalUx3A(StudioUx3AApprovalSnapshot snapshot)
    {
        _approvedBlueprintDraftUx3A = snapshot.Draft;
        _approvedBlueprintRevisionUx3A = snapshot.Revision;
        _approvedBlueprintProjectFingerprintUx3A = snapshot.ProjectFingerprint;
        _approvedBlueprintDraftFingerprintUx3A = snapshot.DraftFingerprint;
    }

    internal void CancelBlueprintApprovalUx3A()
    {
        _approvedBlueprintDraftUx3A = null;
        _approvedBlueprintRevisionUx3A = null;
        _approvedBlueprintProjectFingerprintUx3A = null;
        _approvedBlueprintDraftFingerprintUx3A = null;
    }

    internal void CloseUx3A() => CancelBlueprintApprovalUx3A();

    internal string BlueprintDraftFingerprintUx3A(StudioUx3ADraft draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteBlueprintUx3A(writer, draft.BaseRevision.Value);
        WriteBlueprintUx3A(writer, draft.SourceProjectFingerprint);
        WriteBlueprintUx3A(writer, (int)draft.Operation);
        WriteBlueprintUx3A(writer, draft.BlueprintId);
        WriteBlueprintUx3A(writer, draft.SourceAreaId ?? Guid.Empty);
        WriteBlueprintUx3A(writer, draft.SourceEntityId ?? Guid.Empty);
        WriteBlueprintUx3A(writer, draft.SourceBlueprintId ?? Guid.Empty);
        WriteBlueprintUx3A(writer, draft.Slug);
        WriteBlueprintUx3A(writer, draft.DisplayName);
        WriteBlueprintUx3A(writer, (int)draft.Kind);
        WriteBlueprintUx3A(writer, draft.Version);
        WriteBlueprintUx3A(writer, draft.PackageNamespace);
        WriteBlueprintUx3A(writer, draft.ManifestPath);
        WriteBlueprintStringsUx3A(writer, draft.ExposedParameters);
        WriteBlueprintStringsUx3A(writer, draft.Dependencies);
        WriteBlueprintUx3A(writer, draft.PreviewPath ?? string.Empty);
        WriteBlueprintUx3A(writer, draft.PlatformByteBudget);
        writer.Write(draft.Approved);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    internal StudioUx3AEvidenceProject? CreateBlueprintEvidenceProjectUx3A() =>
        Project is { } project ? BuildBlueprintEvidenceProjectUx3A(project) : null;

    internal StudioUx3ASmokeResult RunUx3AContractSmoke()
    {
        var empty = EmptyBlueprintSmokeUx3A();
        var source = Project;
        if (source is null) return empty;
        var evidence = BuildBlueprintEvidenceProjectUx3A(source);
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux3-a-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        StudioUx3ASmokeResult Execute()
        {
            try
            {
                if (!ProjectSession.Create(root, evidence.Project).Succeeded || !editing.Open(root).Succeeded)
                    return empty;
                var choices = editing.BlueprintChoicesUx3A;
                var entities = editing.EntityChoicesUx3A;
                var explicitSelection = editing.CreateBlueprintDraftUx3A(Guid.Empty) is null && choices.Count >= 2;
                var standardPalette = choices.Any(item => item.Origin == BlueprintPaletteOrigin.InstalledPack);
                var projectPalette = choices.Any(item => item.Origin == BlueprintPaletteOrigin.ProjectContent);
                var standardDraft = editing.CreateBlueprintDraftUx3A(evidence.StandardBlueprintId);
                var standardPreview = standardDraft is null ? null : editing.PreviewBlueprintUx3A(standardDraft with
                {
                    Approved = true,
                    DisplayName = standardDraft.DisplayName + " modifié",
                });
                var standardReadOnly = standardPreview is { Succeeded: false, ReadOnlyProtected: true };

                var draft = editing.CreateBlueprintDraftUx3A(evidence.EditableBlueprintId);
                if (draft is null) return empty;
                var invalid = editing.PreviewBlueprintUx3A(draft with { Slug = "Slug Invalide", Approved = true });
                var invalidBlocked = !invalid.Succeeded;
                var configured = ChangedBlueprintDraftUx3A(draft);
                var beforeProject = editing.Project!;
                var beforeRevision = editing.History!.State.Revision;
                var preview = editing.PreviewBlueprintUx3A(configured);
                var dryRun = preview.Succeeded && ReferenceEquals(beforeProject, editing.Project) &&
                             editing.History.State.Revision == beforeRevision;

                var intervening = editing._upsertBlueprintUx3A.Handle(editing.History.State,
                    new UpsertBlueprintCommand(CommandId.From(Guid.NewGuid()), editing.History.State.Revision,
                        BlueprintId.From(evidence.EditableBlueprintId), draft.Slug, draft.DisplayName + " concurrent",
                        draft.Kind, false, BuildAuthoringUx3A(draft, evidence.Project.Blueprints
                            .Single(item => item.Id.Value == evidence.EditableBlueprintId).Authoring)));
                var staleSource = false;
                if (intervening.Succeeded)
                {
                    editing.History.Record(intervening);
                    staleSource = !editing.ApplyApprovedBlueprintUx3A(configured).Succeeded;
                    editing.Undo();
                }

                draft = editing.CreateBlueprintDraftUx3A(evidence.EditableBlueprintId);
                if (draft is null) return empty;
                configured = ChangedBlueprintDraftUx3A(draft);
                preview = editing.PreviewBlueprintUx3A(configured);
                var staleApproval = preview.Succeeded && !editing.ApplyApprovedBlueprintUx3A(configured with
                {
                    DisplayName = configured.DisplayName + " altéré",
                }).Succeeded;

                draft = editing.CreateBlueprintDraftUx3A(evidence.EditableBlueprintId);
                if (draft is null) return empty;
                configured = ChangedBlueprintDraftUx3A(draft);
                var sourceProject = editing.Project!;
                var sourceBlueprint = sourceProject.Blueprints.Single(item => item.Id.Value == evidence.EditableBlueprintId);
                var sourceOtherBlueprints = sourceProject.Blueprints
                    .Where(item => item.Id.Value != evidence.EditableBlueprintId).ToArray();
                var sourceOtherAreas = sourceProject.Areas.Where(item => item.Id.Value != evidence.AreaId).ToArray();
                var finalPreview = editing.PreviewBlueprintUx3A(configured);
                var editApplied = finalPreview.Succeeded
                    ? editing.ApplyApprovedBlueprintUx3A(configured)
                    : new StudioUx3AActionResult(false, "aperçu refusé");
                var editedProject = editing.Project!;
                var edited = editedProject.Blueprints.SingleOrDefault(item => item.Id.Value == evidence.EditableBlueprintId);
                var reusable = editApplied.Succeeded && edited is not null &&
                               edited.DisplayName == configured.DisplayName &&
                               edited.Authoring.Version == configured.Version &&
                               edited.Authoring.PackageNamespace == configured.PackageNamespace &&
                               edited.Authoring.ManifestPath == configured.ManifestPath &&
                               edited.Authoring.ExposedParameters.SequenceEqual(configured.ExposedParameters) &&
                               edited.Authoring.Dependencies.SequenceEqual(configured.Dependencies) &&
                               edited.Combat == sourceBlueprint.Combat &&
                               edited.CreatureFoundation == sourceBlueprint.CreatureFoundation &&
                               edited.ItemFoundation == sourceBlueprint.ItemFoundation;
                var editJson = ProjectJsonSerializer.Serialize(editedProject);
                var editUndo = editing.UndoBlueprintUx3A();
                var editUndone = editUndo.Succeeded && ProjectJsonSerializer.Serialize(editing.Project!) ==
                                 ProjectJsonSerializer.Serialize(sourceProject);
                var editRedo = editing.RedoBlueprintUx3A();
                var editRedone = editRedo.Succeeded && ProjectJsonSerializer.Serialize(editing.Project!) == editJson;

                var deriveDraft = editing.CreateDerivedBlueprintDraftUx3A(evidence.AreaId, evidence.EntityId);
                if (deriveDraft is null) return empty;
                deriveDraft = deriveDraft with
                {
                    Slug = "variante-instance-temoin",
                    DisplayName = "Variante d’instance témoin",
                    PackageNamespace = "project.variante_instance_temoin",
                    ManifestPath = $"content/blueprints/{deriveDraft.BlueprintId:D}/variante-instance-temoin.blueprint.json",
                    PreviewPath = $"content/blueprints/{deriveDraft.BlueprintId:D}/variante-instance-temoin.preview.png",
                    Approved = true,
                };
                var areaBefore = editing.Project!.Areas.Single(item => item.Id.Value == evidence.AreaId);
                var entityBefore = areaBefore.Entities.Single(item => item.Id.Value == evidence.EntityId);
                var derivePreview = editing.PreviewBlueprintUx3A(deriveDraft);
                var deriveApplied = derivePreview.Succeeded
                    ? editing.ApplyApprovedBlueprintUx3A(deriveDraft)
                    : new StudioUx3AActionResult(false, "dérivation refusée");
                var appliedProject = editing.Project!;
                var derived = appliedProject.Blueprints.SingleOrDefault(item => item.Id.Value == deriveDraft.BlueprintId);
                var entityAfter = appliedProject.Areas.Single(item => item.Id.Value == evidence.AreaId).Entities
                    .Single(item => item.Id.Value == evidence.EntityId);
                var derivation = deriveApplied.Succeeded && derived is not null &&
                                 entityAfter.BlueprintId == derived.Id;
                var overrides = derivation && entityAfter.Transform == entityBefore.Transform &&
                                entityAfter.Tags.SequenceEqual(entityBefore.Tags) &&
                                entityAfter.Overrides.Properties.SequenceEqual(entityBefore.Overrides.Properties);
                var versionPinned = derived is not null &&
                                    entityAfter.BlueprintVersionRequirement == $"={derived.Authoring.Version}";
                var snapshot = new BlueprintPaletteScriptService(appliedProject)
                    .Resolve(entityAfter.Id, blueprintRequired: true);
                var instanceResolved = snapshot.Diagnostics.Count == 0 && snapshot.Blueprint?.BlueprintId == derived?.Id &&
                                       snapshot.EffectiveProperties.Count >= entityAfter.Overrides.Properties.Count;
                var finalJson = ProjectJsonSerializer.Serialize(appliedProject);
                var deriveUndo = editing.UndoBlueprintUx3A();
                var deriveUndone = deriveUndo.Succeeded && ProjectJsonSerializer.Serialize(editing.Project!) == editJson;
                var deriveRedo = editing.RedoBlueprintUx3A();
                var deriveRedone = deriveRedo.Succeeded && ProjectJsonSerializer.Serialize(editing.Project!) == finalJson;
                var saved = editing.SaveBlueprintUx3A();
                editing.Close();
                var persistence = saved.Succeeded && reopened.Open(root).Succeeded &&
                                  ProjectJsonSerializer.Serialize(reopened.Project!) == finalJson;
                reopened.Close();

                var blueprintNeighbors = sourceOtherBlueprints.All(before => appliedProject.Blueprints
                    .Any(after => after.Id == before.Id && after == before)) && derived is not null;
                var targetArea = appliedProject.Areas.Single(item => item.Id.Value == evidence.AreaId);
                var areaNeighbors = sourceOtherAreas.SequenceEqual(appliedProject.Areas
                                        .Where(item => item.Id.Value != evidence.AreaId)) &&
                                    areaBefore.Entities.Where(item => item.Id.Value != evidence.EntityId)
                                        .SequenceEqual(targetArea.Entities.Where(item => item.Id.Value != evidence.EntityId));
                var projectNeighbors = appliedProject.Id == sourceProject.Id &&
                                       appliedProject.Title == sourceProject.Title &&
                                       appliedProject.Module == sourceProject.Module &&
                                       appliedProject.Dialogues.SequenceEqual(sourceProject.Dialogues) &&
                                       appliedProject.Quests.SequenceEqual(sourceProject.Quests);
                var canonical = editApplied.Succeeded && deriveApplied.Succeeded &&
                                deriveDraft.Operation == StudioUx3AOperation.DeriveFromInstance &&
                                appliedProject.Blueprints.Count == editedProject.Blueprints.Count + 1 &&
                                appliedProject.Blueprints.Count(item => item.Id.Value == deriveDraft.BlueprintId) == 1;
                var human = choices.All(item => !item.Label.Contains(item.Id.ToString("D"),
                    StringComparison.OrdinalIgnoreCase)) && entities.All(item =>
                    !item.Label.Contains(item.EntityId.ToString("D"), StringComparison.OrdinalIgnoreCase));
                var origin = evidence.UsesRealModule ^ evidence.UsesCanonicalFallback;
                var succeeded = explicitSelection && origin && standardPalette && projectPalette && standardReadOnly &&
                                reusable && instanceResolved && derivation && overrides && versionPinned &&
                                invalidBlocked && dryRun && staleSource && staleApproval && canonical &&
                                editUndone && editRedone && deriveUndone && deriveRedone && persistence &&
                                blueprintNeighbors && areaNeighbors && projectNeighbors && human;
                return new(succeeded, explicitSelection, evidence.UsesRealModule,
                    evidence.UsesCanonicalFallback, standardPalette, projectPalette, standardReadOnly, reusable,
                    instanceResolved, derivation, overrides, versionPinned, invalidBlocked, dryRun, staleSource,
                    staleApproval, canonical, editUndone && editRedone && deriveUndone && deriveRedone,
                    persistence, blueprintNeighbors, areaNeighbors, projectNeighbors, human, true, false);
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

    internal static bool DeleteTemporaryBlueprintUx3A(string root)
    {
        try
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            return !Directory.Exists(root);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal static StudioUx3ADraft ChangedBlueprintDraftUx3A(StudioUx3ADraft draft) => draft with
    {
        DisplayName = draft.DisplayName.EndsWith(" témoin", StringComparison.Ordinal)
            ? draft.DisplayName : draft.DisplayName + " témoin",
        Version = "1.1.0",
        ExposedParameters = ["apparence", "niveau"],
        Dependencies = ["content/shared/materials/forge-standard.material.json"],
        PlatformByteBudget = 2_097_152,
        Approved = true,
    };

    private CommandResult HandleBlueprintUx3A(ProjectState state, StudioUx3ADraft draft,
        BlueprintAuthoringMetadata authoring, bool dryRun) => draft.Operation switch
        {
            StudioUx3AOperation.EditBlueprint => _upsertBlueprintUx3A.Handle(state,
                new UpsertBlueprintCommand(CommandId.From(Guid.NewGuid()), state.Revision,
                    BlueprintId.From(draft.BlueprintId), draft.Slug.Trim(), draft.DisplayName.Trim(), draft.Kind,
                    dryRun, authoring)),
            StudioUx3AOperation.DeriveFromInstance when draft.SourceAreaId is { } areaId &&
                                                         draft.SourceEntityId is { } entityId =>
                _deriveBlueprintUx3A.Handle(state,
                    new CreateBlueprintFromEntityCommand(CommandId.From(Guid.NewGuid()), state.Revision,
                        AreaId.From(areaId), EntityId.From(entityId), BlueprintId.From(draft.BlueprintId),
                        draft.Slug.Trim(), draft.DisplayName.Trim(), dryRun, authoring)),
            _ => new CommandResult(false, state, state.Revision, null,
                [new CommandDiagnostic("blueprint.instance_missing", "Choisissez une instance existante.")],
                null, null, new AuditEntry(CommandId.From(Guid.NewGuid()), "ux3_a_invalid",
                    DateTimeOffset.UtcNow, false)),
        };

    private static BlueprintAuthoringMetadata BuildAuthoringUx3A(StudioUx3ADraft draft,
        BlueprintAuthoringMetadata source) => new(
        BlueprintAuthoringMetadata.CurrentSchemaVersion,
        draft.Version.Trim(), draft.PackageNamespace.Trim(), draft.ManifestPath.Trim(),
        draft.ExposedParameters, draft.Dependencies,
        string.IsNullOrWhiteSpace(draft.PreviewPath) ? null : draft.PreviewPath.Trim(),
        draft.PlatformByteBudget, source.Kit, source.Styles, source.Compatibility, source.Tags,
        source.CategoryPaths, source.PromotedDefaults, source.AuthorCollections,
        source.PrefabVariant, source.PrefabComposition, source.AssetBudget);

    private static StudioUx3ADraft DraftFromBlueprintUx3A(ProjectRevision revision, string fingerprint,
        BlueprintDefinition blueprint, StudioUx3AOperation operation, Guid blueprintId, Guid? areaId,
        Guid? entityId, Guid? sourceBlueprintId, bool approved) => new(
        revision, fingerprint, operation, blueprintId, areaId, entityId, sourceBlueprintId,
        blueprint.Slug, blueprint.DisplayName, blueprint.Kind, blueprint.Authoring.Version,
        blueprint.Authoring.PackageNamespace, blueprint.Authoring.ManifestPath,
        blueprint.Authoring.ExposedParameters.ToArray(), blueprint.Authoring.Dependencies.ToArray(),
        blueprint.Authoring.PreviewPath, blueprint.Authoring.PlatformByteBudget, approved);

    private static StudioUx3ADraft FreezeBlueprintDraftUx3A(StudioUx3ADraft draft) => draft with
    {
        ExposedParameters = draft.ExposedParameters.ToArray(),
        Dependencies = draft.Dependencies.ToArray(),
    };

    private static StudioUx3APreview FailedBlueprintUx3A(string message, string code) => new(
        false, false, false, code == UpsertBlueprintHandler.ReadOnlyCode, true,
        HumanBlueprintDiagnosticUx3A(code, message), string.Empty, string.Empty, [],
        [new StudioUx3ADiagnostic(code, "blueprints/preview", HumanBlueprintDiagnosticUx3A(code, message))]);

    private static string HumanBlueprintDiagnosticUx3A(string code, string fallback) => code switch
    {
        UpsertBlueprintHandler.RevisionConflictCode => "Le projet a changé ; rechargez la définition.",
        UpsertBlueprintHandler.InvalidSlugCode => "La clé doit être en minuscules avec des tirets.",
        UpsertBlueprintHandler.DuplicateSlugCode => "Cette clé est déjà utilisée par un autre blueprint.",
        UpsertBlueprintHandler.ReadOnlyCode => "Un blueprint Standard installé est protégé en lecture seule.",
        CreateBlueprintFromEntityHandler.MissingEntityCode => "L’instance sélectionnée n’existe plus.",
        CreateBlueprintFromEntityHandler.MissingAreaCode => "La zone de l’instance n’existe plus.",
        "blueprint.approval_required" => "Cochez l’approbation auteur avant le dry-run.",
        _ => fallback,
    };

    private static StudioUx3AEvidenceProject BuildBlueprintEvidenceProjectUx3A(ProjectDefinition source)
    {
        var blueprints = source.Blueprints.ToList();
        var areas = source.Areas.ToList();
        var fallback = false;
        var editable = blueprints.FirstOrDefault(item => !item.IsReadOnly());
        if (editable is null)
        {
            fallback = true;
            var id = BlueprintId.From(StableGuidUx3A(source.Id.Value, "ux3-a-project-blueprint"));
            editable = new BlueprintDefinition(id, "objet-temoin-ux3", "Objet témoin UX3",
                BlueprintKind.Placeable);
            blueprints.Add(editable);
        }
        if (areas.Count == 0)
            return new(source, editable.Id.Value, editable.Id.Value, Guid.Empty, Guid.Empty, false, true);
        var area = areas.FirstOrDefault(candidate => candidate.Entities.Any(item => item.BlueprintId == editable.Id))
                   ?? areas[0];
        var entity = area.Entities.FirstOrDefault(item => item.BlueprintId == editable.Id);
        if (entity is null)
        {
            fallback = true;
            entity = new EntityInstance(EntityId.From(StableGuidUx3A(source.Id.Value, "ux3-a-instance")),
                editable.Id, TransformValue.Identity, ["ux3-a", "preuve-isolee"],
                new EntityPropertyOverrides(EntityPropertyOverrides.CurrentSchemaVersion,
                    [new EntityPropertyOverride("appearance.variant", EntityPropertyValueKind.String, "temoin")]),
                $"={editable.Authoring.Version}");
            area = area.WithEntities([.. area.Entities, entity]);
            areas = areas.Select(item => item.Id == area.Id ? area : item).ToList();
        }
        var standard = blueprints.FirstOrDefault(item => item.IsReadOnly());
        if (standard is null)
        {
            fallback = true;
            var id = BlueprintId.From(StableGuidUx3A(source.Id.Value, "ux3-a-standard-blueprint"));
            var provenance = new BlueprintProvenance(BlueprintProvenance.CurrentSchemaVersion,
                "pack://studio-standard/1.0.0/objet-temoin", "OpenRPG Forge", "MIT",
                new string('a', 64), permissionEvidence: "spdx:MIT");
            standard = new BlueprintDefinition(id, "standard-objet-temoin", "Standard · objet témoin",
                editable.Kind, editable.Combat, BlueprintPaletteOrigin.InstalledPack, false, provenance,
                editable.CreaturePresentation, editable.CreatureProgression, editable.CreatureLoot,
                editable.CreatureRoleBehavior, editable.CreatureEvents, editable.ItemAdvanced,
                BlueprintAuthoringMetadata.CreateDefault(id, "standard-objet-temoin",
                    BlueprintPaletteOrigin.InstalledPack, provenance), editable.CreatureFoundation,
                editable.ItemFoundation);
            blueprints.Add(standard);
        }
        var project = fallback
            ? new ProjectDefinition(source.Id, source.Title, source.Module, blueprints, areas,
                source.Dialogues, source.Quests)
            : source;
        return new(project, editable.Id.Value, standard.Id.Value, area.Id.Value, entity.Id.Value,
            !fallback, fallback);
    }

    private static StudioUx3ASmokeResult EmptyBlueprintSmokeUx3A() => new(false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, true, false);

    private static string NextDerivedSlugUx3A(ProjectDefinition project, string sourceSlug)
    {
        var root = $"{sourceSlug}-variante";
        var slug = root;
        var index = 2;
        while (project.Blueprints.Any(item => item.Slug == slug)) slug = $"{root}-{index++}";
        return slug;
    }

    private static string ProjectFingerprintUx3A(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))))
            .ToLowerInvariant();

    private static Guid StableGuidUx3A(Guid root, string discriminator)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{root:D}:{discriminator}"));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes);
    }

    private static string HumanBlueprintKindUx3A(BlueprintKind kind) => kind switch
    {
        BlueprintKind.Creature => "créature",
        BlueprintKind.Placeable => "objet plaçable",
        BlueprintKind.Door => "porte",
        BlueprintKind.Item => "objet d’inventaire",
        BlueprintKind.Trigger => "déclencheur",
        BlueprintKind.PlayerSpawn => "joueur",
        _ => kind.ToString(),
    };

    private static string HumanOriginUx3A(BlueprintPaletteOrigin origin) => origin switch
    {
        BlueprintPaletteOrigin.InstalledPack => "Standard · lecture seule",
        BlueprintPaletteOrigin.ProjectContent => "Projet",
        BlueprintPaletteOrigin.Generated => "Généré",
        BlueprintPaletteOrigin.Temporary => "Temporaire",
        _ => origin.ToString(),
    };

    private static void WriteBlueprintUx3A(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static void WriteBlueprintUx3A(BinaryWriter writer, Guid value) => writer.Write(value.ToByteArray());
    private static void WriteBlueprintUx3A(BinaryWriter writer, int value) => writer.Write(value);
    private static void WriteBlueprintUx3A(BinaryWriter writer, long value) => writer.Write(value);
    private static void WriteBlueprintStringsUx3A(BinaryWriter writer, IEnumerable<string> values)
    {
        var items = values.ToArray();
        writer.Write(items.Length);
        foreach (var item in items) WriteBlueprintUx3A(writer, item);
    }
}
