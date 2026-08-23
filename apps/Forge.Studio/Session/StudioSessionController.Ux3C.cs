using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Blueprints;
using Forge.Application.Creator;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Authoring;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly BlueprintDropPlanner _dropPlannerUx3C = new();

    private StudioUx3CDiscoveryDraft? _approvedDiscoveryDraftUx3C;
    private string? _approvedDiscoveryProjectFingerprintUx3C;
    private string? _approvedDiscoveryWorkspaceFingerprintUx3C;
    private string? _approvedDiscoveryDraftFingerprintUx3C;
    private StudioUx3CDropDraft? _approvedDropDraftUx3C;
    private ProjectRevision? _approvedDropRevisionUx3C;
    private string? _approvedDropProjectFingerprintUx3C;
    private string? _approvedDropDraftFingerprintUx3C;

    internal IReadOnlyList<StudioUx3CBlueprintChoice> BuildBlueprintDiscoveryUx3C(
        BlueprintPaletteView view,
        string? query,
        EditorPaletteFilter? filter = null)
    {
        if (Project is null) return [];
        var project = Project;
        var root = Session?.RootDirectory;
        var usage = project.Areas.SelectMany(area => area.Entities)
            .GroupBy(entity => entity.BlueprintId)
            .ToDictionary(group => group.Key, group => group.Count());
        return BlueprintPaletteCatalog.Build(project, WorkspaceProfile)
            .Filter(view, query, filter ?? WorkspaceProfile.Shell.PaletteFilter)
            .Select(item =>
            {
                var blueprint = project.Blueprints.Single(candidate => candidate.Id == item.BlueprintId);
                var absolute = ResolvePreviewPathUx3C(root, blueprint.Authoring.PreviewPath);
                return new StudioUx3CBlueprintChoice(
                    item.BlueprintId.Value,
                    $"{item.DisplayName} · {HumanBlueprintKindUx3A(item.Kind)} · {HumanOriginUx3A(item.Origin)}",
                    item.Kind,
                    item.Origin,
                    item.IsFavorite,
                    item.IsRecent,
                    item.RecentRank,
                    item.IsReadOnly,
                    item.Kit,
                    item.Provenance.LicenseSpdx,
                    item.Styles,
                    item.Compatibility,
                    item.Tags,
                    blueprint.Authoring.PreviewPath,
                    absolute,
                    absolute is not null && File.Exists(absolute),
                    usage.GetValueOrDefault(item.BlueprintId));
            })
            .ToArray();
    }

    internal StudioUx3CDiscoveryDraft? CreateDiscoveryDraftUx3C(Guid blueprintId)
    {
        if (Project is null || blueprintId == Guid.Empty) return null;
        var choice = BuildBlueprintDiscoveryUx3C(BlueprintPaletteView.All, null,
            EditorPaletteFilter.ShowAll).FirstOrDefault(item => item.Id == blueprintId);
        return choice is null ? null : new StudioUx3CDiscoveryDraft(
            ProjectFingerprintUx3A(Project),
            WorkspaceFingerprintUx3C(WorkspaceProfile),
            blueprintId,
            BlueprintPaletteView.All,
            string.Empty,
            Enum.GetValues<BlueprintKind>(),
            [], [], [], [], [],
            choice.IsFavorite,
            Approved: false);
    }

    internal StudioUx3CDiscoveryPreview PreviewDiscoveryUx3C(StudioUx3CDiscoveryDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (Project is null || Project.Blueprints.All(item => item.Id.Value != draft.BlueprintId))
            return FailedDiscoveryUx3C("Le blueprint choisi n’existe plus.", "blueprint.missing", "blueprints");
        if (!draft.Approved)
            return FailedDiscoveryUx3C("Approuvez la mémorisation du favori et du récent avant l’aperçu sec.",
                "blueprint.discovery.approval_required", "editorWorkspace/palette");
        if (draft.SourceProjectFingerprint != ProjectFingerprintUx3A(Project))
            return FailedDiscoveryUx3C("Le projet a changé ; rechargez la bibliothèque.",
                "blueprint.discovery.project_stale", "project");
        if (draft.SourceWorkspaceFingerprint != WorkspaceFingerprintUx3C(WorkspaceProfile))
            return FailedDiscoveryUx3C("Les préférences Studio ont changé ; rechargez la sélection.",
                "blueprint.discovery.workspace_stale", "editorWorkspace");

        EditorPaletteFilter filter;
        try
        {
            filter = new EditorPaletteFilter(draft.Kinds, draft.Kits, draft.Styles, draft.Licenses,
                draft.Compatibility, draft.Tags);
        }
        catch (ArgumentException exception)
        {
            return FailedDiscoveryUx3C(exception.Message, "blueprint.discovery.filter_invalid",
                "editorWorkspace/paletteFilter");
        }

        var visible = BuildBlueprintDiscoveryUx3C(draft.View, draft.Query, filter);
        if (visible.All(item => item.Id != draft.BlueprintId))
            return FailedDiscoveryUx3C("Le blueprint sélectionné ne correspond plus à la recherche et aux filtres.",
                "blueprint.discovery.selection_filtered", "editorWorkspace/paletteFilter");

        var candidate = BuildDiscoveryCandidateUx3C(draft);
        var source = WorkspaceProfile;
        var preview = PreviewWorkspaceProfile(candidate);
        var dryRun = preview.Succeeded && ReferenceEquals(Project, Project) &&
                     WorkspaceFingerprintUx3C(WorkspaceProfile) == draft.SourceWorkspaceFingerprint;
        if (!preview.Succeeded)
            return FailedDiscoveryUx3C(preview.Message, "blueprint.discovery.command_rejected", "editorWorkspace");

        var frozen = FreezeDiscoveryDraftUx3C(draft);
        _approvedDiscoveryDraftUx3C = frozen;
        _approvedDiscoveryProjectFingerprintUx3C = draft.SourceProjectFingerprint;
        _approvedDiscoveryWorkspaceFingerprintUx3C = draft.SourceWorkspaceFingerprint;
        _approvedDiscoveryDraftFingerprintUx3C = DiscoveryDraftFingerprintUx3C(frozen);
        var selected = Project.Blueprints.Single(item => item.Id.Value == draft.BlueprintId);
        var favoriteBefore = source.PersonalFavoriteBlueprintIds.Contains(selected.Id);
        var semantic = new List<string>
        {
            $"Récent · « {selected.DisplayName} » remonte en tête de la liste personnelle.",
        };
        if (favoriteBefore != draft.Favorite)
            semantic.Add(draft.Favorite
                ? $"Favori · ajouter « {selected.DisplayName} »."
                : $"Favori · retirer « {selected.DisplayName} ».");
        return new StudioUx3CDiscoveryPreview(true, dryRun, dryRun, true,
            $"Aperçu valide · {semantic.Count} changement(s) personnel(s) · aucune donnée projet modifiée.",
            semantic, [], candidate);
    }

    internal StudioUx3CActionResult ApplyApprovedDiscoveryUx3C(StudioUx3CDiscoveryDraft current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (Project is null || _approvedDiscoveryDraftUx3C is null ||
            _approvedDiscoveryDraftFingerprintUx3C != DiscoveryDraftFingerprintUx3C(current) ||
            _approvedDiscoveryProjectFingerprintUx3C != ProjectFingerprintUx3A(Project) ||
            _approvedDiscoveryWorkspaceFingerprintUx3C != WorkspaceFingerprintUx3C(WorkspaceProfile))
            return new(false, "Aperçu personnel périmé · relancez l’aperçu sec.");
        var candidate = BuildDiscoveryCandidateUx3C(_approvedDiscoveryDraftUx3C);
        var applied = ApplyWorkspaceProfile(candidate);
        CancelDiscoveryApprovalUx3C();
        return new(applied.Succeeded, applied.Succeeded
            ? "Favori et récent appliqués en mémoire · enregistrez le profil Studio pour les conserver."
            : applied.Message);
    }

    internal StudioUx3CDropDraft? CreateDropDraftUx3C(Guid blueprintId)
    {
        if (Project is null || History is null || ActiveArea is null || blueprintId == Guid.Empty ||
            Project.Blueprints.All(item => item.Id.Value != blueprintId)) return null;
        var position = FindDropPositionUx3C(blueprintId);
        if (position is null) return null;
        return new StudioUx3CDropDraft(History.State.Revision, ProjectFingerprintUx3A(Project), blueprintId,
            ActiveArea.Id.Value, position.Value, BlueprintDropTargetKind.Viewport,
            $"areas/{ActiveArea.Id}/entities", Approved: false);
    }

    internal StudioUx3CDropPreview PreviewDropUx3C(StudioUx3CDropDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (Project is null || History is null)
            return FailedDropUx3C("Aucun projet n’est ouvert.", "blueprint.drop.project_missing", "project");
        if (!draft.Approved)
            return FailedDropUx3C("Approuvez le dépôt avant l’aperçu sec.",
                "blueprint.drop.approval_required", "blueprintDrops");
        if (draft.BaseRevision != History.State.Revision ||
            draft.SourceProjectFingerprint != ProjectFingerprintUx3A(Project))
            return FailedDropUx3C("Le projet a changé ; préparez de nouveau le dépôt.",
                "blueprint.drop.project_stale", "project");

        var before = Project;
        var beforeRevision = History.State.Revision;
        var request = new BlueprintDropRequest(Guid.NewGuid(), BlueprintDropPlanner.CurrentSchemaVersion,
            "studio:ux3-c-drag-drop", BlueprintId.From(draft.BlueprintId), draft.TargetKind, draft.TargetPath,
            AreaId.From(draft.AreaId), draft.Position, .25f);
        var plan = _dropPlannerUx3C.Plan(Project, request);
        if (!plan.IsValid || plan.CorrectedPosition is null || plan.Operation != BlueprintDropOperationKind.PlaceEntity)
            return FailedDropUx3C(HumanDropMessageUx3C(plan.Code, plan.Message), plan.Code, HumanPathUx3C(plan.Path));
        var placement = PreviewRepeatedPlacement(BuildPlacementRequestUx3C(draft, plan.CorrectedPosition.Value));
        var dryRun = placement.IsValid && ReferenceEquals(before, Project) && History.State.Revision == beforeRevision;
        if (!placement.IsValid)
            return FailedDropUx3C(placement.Message, "blueprint.drop.placement_invalid", "zone/placement");

        var frozen = draft with { Position = plan.CorrectedPosition.Value };
        _approvedDropDraftUx3C = frozen;
        _approvedDropRevisionUx3C = beforeRevision;
        _approvedDropProjectFingerprintUx3C = draft.SourceProjectFingerprint;
        _approvedDropDraftFingerprintUx3C = DropDraftFingerprintUx3C(frozen);
        var blueprint = Project.Blueprints.Single(item => item.Id.Value == draft.BlueprintId);
        return new StudioUx3CDropPreview(true, dryRun, dryRun, true,
            $"Dépôt valide · « {blueprint.DisplayName} » sera placé sur la grille de la zone active.",
            plan.Code, HumanPathUx3C(plan.Path), plan.CorrectedPosition,
            [$"Placement · {blueprint.DisplayName} · position corrigée X {plan.CorrectedPosition.Value.X:0.##}, Y {plan.CorrectedPosition.Value.Y:0.##}, Z {plan.CorrectedPosition.Value.Z:0.##}."], []);
    }

    internal StudioUx3CActionResult ApplyApprovedDropUx3C(StudioUx3CDropDraft current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (Project is null || History is null || _approvedDropDraftUx3C is null ||
            _approvedDropRevisionUx3C != History.State.Revision ||
            _approvedDropProjectFingerprintUx3C != ProjectFingerprintUx3A(Project) ||
            _approvedDropDraftFingerprintUx3C != DropDraftFingerprintUx3C(current))
            return new(false, "Aperçu de dépôt périmé · glissez de nouveau le blueprint.");
        var request = BuildPlacementRequestUx3C(_approvedDropDraftUx3C, _approvedDropDraftUx3C.Position);
        var applied = PlaceRepeatedEntities(request, persistPaletteUsage: false);
        CancelDropApprovalUx3C();
        CancelDiscoveryApprovalUx3C();
        return new(applied.Succeeded, applied.Succeeded
            ? "Blueprint placé par la commande canonique · Ctrl+Z annule toute la transaction."
            : applied.Message);
    }

    internal StudioUx3CActionResult UndoDiscoveryUx3C()
    {
        var result = UndoWorkspaceProfile(persist: false);
        CancelDiscoveryApprovalUx3C();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx3CActionResult RedoDiscoveryUx3C()
    {
        var result = RedoWorkspaceProfile(persist: false);
        CancelDiscoveryApprovalUx3C();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx3CActionResult SaveDiscoveryUx3C()
    {
        var result = SaveWorkspaceProfile();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx3CActionResult UndoDropUx3C()
    {
        var result = Undo();
        CancelDropApprovalUx3C();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx3CActionResult RedoDropUx3C()
    {
        var result = Redo();
        CancelDropApprovalUx3C();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx3CActionResult SaveDropUx3C()
    {
        var result = Save();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx3CApprovalSnapshot CaptureBlueprintDiscoveryApprovalUx3C() => new(
        _approvedDiscoveryDraftUx3C, _approvedDiscoveryProjectFingerprintUx3C,
        _approvedDiscoveryWorkspaceFingerprintUx3C, _approvedDiscoveryDraftFingerprintUx3C,
        _approvedDropDraftUx3C, _approvedDropRevisionUx3C,
        _approvedDropProjectFingerprintUx3C, _approvedDropDraftFingerprintUx3C);

    internal void RestoreBlueprintDiscoveryApprovalUx3C(StudioUx3CApprovalSnapshot snapshot)
    {
        _approvedDiscoveryDraftUx3C = snapshot.DiscoveryDraft;
        _approvedDiscoveryProjectFingerprintUx3C = snapshot.DiscoveryProjectFingerprint;
        _approvedDiscoveryWorkspaceFingerprintUx3C = snapshot.DiscoveryWorkspaceFingerprint;
        _approvedDiscoveryDraftFingerprintUx3C = snapshot.DiscoveryDraftFingerprint;
        _approvedDropDraftUx3C = snapshot.DropDraft;
        _approvedDropRevisionUx3C = snapshot.DropRevision;
        _approvedDropProjectFingerprintUx3C = snapshot.DropProjectFingerprint;
        _approvedDropDraftFingerprintUx3C = snapshot.DropDraftFingerprint;
    }

    internal void CancelDiscoveryApprovalUx3C()
    {
        _approvedDiscoveryDraftUx3C = null;
        _approvedDiscoveryProjectFingerprintUx3C = null;
        _approvedDiscoveryWorkspaceFingerprintUx3C = null;
        _approvedDiscoveryDraftFingerprintUx3C = null;
    }

    internal void CancelDropApprovalUx3C()
    {
        _approvedDropDraftUx3C = null;
        _approvedDropRevisionUx3C = null;
        _approvedDropProjectFingerprintUx3C = null;
        _approvedDropDraftFingerprintUx3C = null;
    }

    internal void CloseUx3C()
    {
        CancelDiscoveryApprovalUx3C();
        CancelDropApprovalUx3C();
    }

    internal string DiscoveryDraftFingerprintUx3C(StudioUx3CDiscoveryDraft draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteUx3C(writer, draft.SourceProjectFingerprint);
        WriteUx3C(writer, draft.SourceWorkspaceFingerprint);
        writer.Write(draft.BlueprintId.ToByteArray());
        writer.Write((int)draft.View);
        WriteUx3C(writer, draft.Query);
        WriteUx3C(writer, draft.Kinds.Select(item => item.ToString()));
        WriteUx3C(writer, draft.Kits);
        WriteUx3C(writer, draft.Styles);
        WriteUx3C(writer, draft.Licenses);
        WriteUx3C(writer, draft.Compatibility);
        WriteUx3C(writer, draft.Tags);
        writer.Write(draft.Favorite);
        writer.Write(draft.Approved);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    internal string DropDraftFingerprintUx3C(StudioUx3CDropDraft draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteUx3C(writer, draft.BaseRevision.ToString());
        WriteUx3C(writer, draft.SourceProjectFingerprint);
        writer.Write(draft.BlueprintId.ToByteArray());
        writer.Write(draft.AreaId.ToByteArray());
        writer.Write(draft.Position.X);
        writer.Write(draft.Position.Y);
        writer.Write(draft.Position.Z);
        writer.Write((int)draft.TargetKind);
        WriteUx3C(writer, draft.TargetPath);
        writer.Write(draft.Approved);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    internal StudioUx3CEvidenceProject? CreateBlueprintDiscoveryEvidenceProjectUx3C()
    {
        var evidence = CreateBlueprintEvidenceProjectUx3A();
        return evidence is null || evidence.AreaId == Guid.Empty ? null : new StudioUx3CEvidenceProject(
            evidence.Project, evidence.EditableBlueprintId, evidence.AreaId, evidence.EntityId,
            evidence.UsesRealModule, evidence.UsesCanonicalFallback);
    }

    internal StudioUx3CSmokeResult RunUx3CContractSmoke()
    {
        var empty = EmptyUx3CSmoke();
        var evidence = CreateBlueprintDiscoveryEvidenceProjectUx3C();
        if (evidence is null) return empty;
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux3-c-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        StudioUx3CSmokeResult Execute()
        {
            try
            {
                if (!ProjectSession.Create(root, evidence.Project).Succeeded ||
                    !WriteEvidenceThumbnailUx3C(root, evidence.Project, evidence.BlueprintId) ||
                    !editing.Open(root).Succeeded ||
                    !editing.SelectArea(AreaId.From(evidence.AreaId))) return empty;
                var all = editing.BuildBlueprintDiscoveryUx3C(BlueprintPaletteView.All, null,
                    EditorPaletteFilter.ShowAll);
                var choice = all.FirstOrDefault(item => item.Id == evidence.BlueprintId);
                if (choice is null) return empty;
                var explicitSelection = editing.CreateDiscoveryDraftUx3C(Guid.Empty) is null;
                var thumbnailMetadata = !string.IsNullOrWhiteSpace(choice.PreviewRelativePath);
                var thumbnailLoaded = choice.PreviewFileExists && choice.PreviewAbsolutePath is not null;
                var honestFallback = all.Where(item => !item.PreviewFileExists)
                    .All(item => !string.IsNullOrWhiteSpace(item.PreviewRelativePath) || item.PreviewAbsolutePath is null);
                var filter = new EditorPaletteFilter([choice.Kind], [choice.Kit], choice.Styles.Take(1).ToArray(),
                    [choice.License], choice.Compatibility.Take(1).ToArray(), choice.Tags.Take(1).ToArray());
                var searched = editing.BuildBlueprintDiscoveryUx3C(BlueprintPaletteView.All,
                    choice.Label.Split('·')[0].Trim(), filter);
                var advancedSearch = searched.Any(item => item.Id == choice.Id) &&
                                     editing.BuildBlueprintDiscoveryUx3C(BlueprintPaletteView.All,
                                         "résultat-introuvable-ux3-c", filter).Count == 0;

                var draft = editing.CreateDiscoveryDraftUx3C(choice.Id);
                if (draft is null) return empty;
                draft = draft with
                {
                    Query = choice.Label.Split('·')[0].Trim(),
                    Kinds = [choice.Kind],
                    Kits = [choice.Kit],
                    Styles = choice.Styles.Take(1).ToArray(),
                    Licenses = [choice.License],
                    Compatibility = choice.Compatibility.Take(1).ToArray(),
                    Tags = choice.Tags.Take(1).ToArray(),
                    Favorite = !choice.IsFavorite,
                    Approved = true,
                };
                var sourceProject = editing.Project!;
                var sourceWorkspace = editing.WorkspaceProfile;
                var sourceWorkspaceJson = EditorWorkspaceProfileJsonSerializer.Serialize(sourceWorkspace);
                var preferencePreview = editing.PreviewDiscoveryUx3C(draft);
                var dryRun = preferencePreview.Succeeded && ReferenceEquals(sourceProject, editing.Project) &&
                             EditorWorkspaceProfileJsonSerializer.Serialize(editing.WorkspaceProfile) == sourceWorkspaceJson;
                var staleApproval = preferencePreview.Succeeded &&
                    !editing.ApplyApprovedDiscoveryUx3C(draft with { Query = draft.Query + " altéré" }).Succeeded;

                draft = editing.CreateDiscoveryDraftUx3C(choice.Id)! with
                {
                    Query = choice.Label.Split('·')[0].Trim(),
                    Kinds = [choice.Kind],
                    Kits = [choice.Kit],
                    Styles = choice.Styles.Take(1).ToArray(),
                    Licenses = [choice.License],
                    Compatibility = choice.Compatibility.Take(1).ToArray(),
                    Tags = choice.Tags.Take(1).ToArray(),
                    Favorite = !choice.IsFavorite,
                    Approved = true,
                };
                preferencePreview = editing.PreviewDiscoveryUx3C(draft);
                var alternate = all.FirstOrDefault(item => item.Id != choice.Id) ?? choice;
                var concurrent = editing.ToggleBlueprintFavorite(BlueprintId.From(alternate.Id), persist: false);
                var staleSource = concurrent.Succeeded && !editing.ApplyApprovedDiscoveryUx3C(draft).Succeeded;
                if (concurrent.Succeeded) editing.UndoWorkspaceProfile(persist: false);

                draft = editing.CreateDiscoveryDraftUx3C(choice.Id)! with
                {
                    Query = choice.Label.Split('·')[0].Trim(),
                    Kinds = [choice.Kind],
                    Kits = [choice.Kit],
                    Styles = choice.Styles.Take(1).ToArray(),
                    Licenses = [choice.License],
                    Compatibility = choice.Compatibility.Take(1).ToArray(),
                    Tags = choice.Tags.Take(1).ToArray(),
                    Favorite = !choice.IsFavorite,
                    Approved = true,
                };
                preferencePreview = editing.PreviewDiscoveryUx3C(draft);
                var preferenceApplied = preferencePreview.Succeeded &&
                                        editing.ApplyApprovedDiscoveryUx3C(draft).Succeeded;
                var afterPreferenceJson = EditorWorkspaceProfileJsonSerializer.Serialize(editing.WorkspaceProfile);
                var favorites = preferenceApplied && editing.BuildBlueprintDiscoveryUx3C(
                    BlueprintPaletteView.Favorites, null, EditorPaletteFilter.ShowAll).Any(item => item.Id == choice.Id);
                var recents = preferenceApplied && editing.BuildBlueprintDiscoveryUx3C(
                    BlueprintPaletteView.Recent, null, EditorPaletteFilter.ShowAll).FirstOrDefault()?.Id == choice.Id;
                var preferenceUndo = editing.UndoDiscoveryUx3C().Succeeded &&
                                     EditorWorkspaceProfileJsonSerializer.Serialize(editing.WorkspaceProfile) == sourceWorkspaceJson;
                var preferenceRedo = editing.RedoDiscoveryUx3C().Succeeded &&
                                     EditorWorkspaceProfileJsonSerializer.Serialize(editing.WorkspaceProfile) == afterPreferenceJson;
                var profileSaved = editing.SaveDiscoveryUx3C().Succeeded;

                var drop = editing.CreateDropDraftUx3C(choice.Id);
                if (drop is null) return empty;
                var invalidDrop = !editing.PreviewDropUx3C(drop with
                {
                    TargetPath = "../project.json",
                    Approved = true,
                }).Succeeded;
                drop = drop with { Approved = true };
                var beforeDrop = editing.Project!;
                var beforeDropRevision = editing.History!.State.Revision;
                var dropPreview = editing.PreviewDropUx3C(drop);
                var dropDryRun = dropPreview.Succeeded && ReferenceEquals(beforeDrop, editing.Project) &&
                                 editing.History.State.Revision == beforeDropRevision;

                var discoveryForSeparate = editing.CreateDiscoveryDraftUx3C(alternate.Id)! with
                {
                    Favorite = !editing.WorkspaceProfile.PersonalFavoriteBlueprintIds
                        .Contains(BlueprintId.From(alternate.Id)),
                    Approved = true,
                };
                var separatePreview = editing.PreviewDiscoveryUx3C(discoveryForSeparate);
                var separateApprovals = separatePreview.Succeeded && editing.PreviewDropUx3C(drop).Succeeded &&
                    editing.CaptureBlueprintDiscoveryApprovalUx3C() is
                    { DiscoveryDraft: not null, DropDraft: not null };
                var staleDropApproval = dropPreview.Succeeded &&
                    !editing.ApplyApprovedDropUx3C(drop with
                    {
                        Position = new Vector3Value(drop.Position.X + .25f, drop.Position.Y, drop.Position.Z),
                    }).Succeeded;
                drop = editing.CreateDropDraftUx3C(choice.Id)! with { Approved = true };
                dropPreview = editing.PreviewDropUx3C(drop);
                var targetAreaBefore = editing.Project!.Areas.Single(item => item.Id.Value == evidence.AreaId);
                var dropApplied = dropPreview.Succeeded && editing.ApplyApprovedDropUx3C(drop).Succeeded;
                var appliedProject = editing.Project!;
                var targetAreaAfter = appliedProject.Areas.Single(item => item.Id.Value == evidence.AreaId);
                var placed = dropApplied && targetAreaAfter.Entities.Count == targetAreaBefore.Entities.Count + 1;
                var appliedJson = ProjectJsonSerializer.Serialize(appliedProject);
                var dropUndo = editing.UndoDropUx3C().Succeeded &&
                               ProjectJsonSerializer.Serialize(editing.Project!) == ProjectJsonSerializer.Serialize(sourceProject);
                var dropRedo = editing.RedoDropUx3C().Succeeded &&
                               ProjectJsonSerializer.Serialize(editing.Project!) == appliedJson;
                var projectSaved = editing.SaveDropUx3C().Succeeded;
                var finalWorkspace = editing.WorkspaceProfile;
                editing.Close();
                var reopenedOk = reopened.Open(root).Succeeded;
                var persistence = profileSaved && projectSaved && reopenedOk &&
                                  ProjectJsonSerializer.Serialize(reopened.Project!) == appliedJson &&
                                  reopened.WorkspaceProfile.PersonalFavoriteBlueprintIds.Contains(
                                      BlueprintId.From(choice.Id)) &&
                                  reopened.WorkspaceProfile.RecentBlueprintIds.FirstOrDefault() ==
                                  BlueprintId.From(choice.Id);

                var blueprintNeighbors = sourceProject.Blueprints.SequenceEqual(appliedProject.Blueprints);
                var sourceOtherAreas = sourceProject.Areas.Where(item => item.Id.Value != evidence.AreaId).ToArray();
                var appliedOtherAreas = appliedProject.Areas.Where(item => item.Id.Value != evidence.AreaId).ToArray();
                var otherAreasPreserved = sourceOtherAreas.Length == appliedOtherAreas.Length && sourceOtherAreas
                    .Zip(appliedOtherAreas).All(pair => pair.First.Id == pair.Second.Id &&
                        AreaJsonSerializer.Serialize(pair.First) == AreaJsonSerializer.Serialize(pair.Second));
                var existingEntitiesPreserved = targetAreaBefore.Entities.All(before =>
                    targetAreaAfter.Entities.Any(after => after.Id == before.Id &&
                        after.BlueprintId == before.BlueprintId && after.Transform == before.Transform &&
                        after.Tags.SequenceEqual(before.Tags) &&
                        after.Overrides.SchemaVersion == before.Overrides.SchemaVersion &&
                        after.Overrides.Properties.SequenceEqual(before.Overrides.Properties) &&
                        after.BlueprintVersionRequirement == before.BlueprintVersionRequirement));
                var targetAreaTailsPreserved = AreaJsonSerializer.Serialize(
                    targetAreaBefore.WithEntities(targetAreaAfter.Entities)) == AreaJsonSerializer.Serialize(targetAreaAfter);
                var areaNeighbors = otherAreasPreserved && existingEntitiesPreserved && targetAreaTailsPreserved;
                var projectNeighbors = sourceProject.Id == appliedProject.Id && sourceProject.Title == appliedProject.Title &&
                                       sourceProject.Module == appliedProject.Module &&
                                       sourceProject.Dialogues.SequenceEqual(appliedProject.Dialogues) &&
                                       sourceProject.Quests.SequenceEqual(appliedProject.Quests);
                var workspaceNeighbors = WorkspaceNeighborsUx3C(sourceWorkspace, finalWorkspace);
                var human = all.All(item => !item.Label.Contains(item.Id.ToString("D"),
                    StringComparison.OrdinalIgnoreCase));
                var origin = evidence.UsesRealModule ^ evidence.UsesCanonicalFallback;
                var succeeded = explicitSelection && origin && favorites && recents && advancedSearch &&
                                thumbnailMetadata && thumbnailLoaded && honestFallback && drop is not null &&
                                dropPreview.Succeeded && invalidDrop && dryRun && dropDryRun && staleSource &&
                                staleApproval && staleDropApproval && separateApprovals && placed && preferenceUndo &&
                                preferenceRedo && dropUndo && dropRedo && persistence && blueprintNeighbors &&
                                areaNeighbors && projectNeighbors && workspaceNeighbors && human;
                return new(succeeded, explicitSelection, evidence.UsesRealModule, evidence.UsesCanonicalFallback,
                    favorites, recents, advancedSearch, thumbnailMetadata, thumbnailLoaded, honestFallback,
                    true, dropPreview.Succeeded, invalidDrop, dryRun && dropDryRun, staleSource, staleApproval &&
                    staleDropApproval, separateApprovals, preferenceApplied && dropApplied,
                    preferenceUndo && preferenceRedo && dropUndo && dropRedo, persistence,
                    blueprintNeighbors, otherAreasPreserved, existingEntitiesPreserved, targetAreaTailsPreserved,
                    areaNeighbors, projectNeighbors, workspaceNeighbors, human, true, false);
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

    private EditorWorkspaceProfile BuildDiscoveryCandidateUx3C(StudioUx3CDiscoveryDraft draft)
    {
        var id = BlueprintId.From(draft.BlueprintId);
        var candidate = EditorWorkspaceProfilePlanner.RecordRecent(WorkspaceProfile, id);
        if (candidate.PersonalFavoriteBlueprintIds.Contains(id) != draft.Favorite)
            candidate = EditorWorkspaceProfilePlanner.TogglePersonalFavorite(candidate, id);
        return candidate;
    }

    private Vector3Value? FindDropPositionUx3C(Guid blueprintId)
    {
        var area = ActiveArea;
        if (area is null) return null;
        var halfWidth = area.Dimensions.Width / 2f;
        var halfDepth = area.Dimensions.Depth / 2f;
        for (var z = -halfDepth + 2; z <= halfDepth - 2; z += 2)
            for (var x = -halfWidth + 2; x <= halfWidth - 2; x += 2)
            {
                var position = new Vector3Value(x, 0, z);
                var draft = new StudioUx3CDropDraft(History!.State.Revision, ProjectFingerprintUx3A(Project!),
                    blueprintId, area.Id.Value, position, BlueprintDropTargetKind.Viewport,
                    $"areas/{area.Id}/entities", true);
                var request = new BlueprintDropRequest(Guid.NewGuid(), BlueprintDropPlanner.CurrentSchemaVersion,
                    "studio:ux3-c-position", BlueprintId.From(blueprintId), draft.TargetKind, draft.TargetPath,
                    area.Id, position, .25f);
                var plan = _dropPlannerUx3C.Plan(Project!, request);
                if (!plan.IsValid || plan.CorrectedPosition is null) continue;
                if (PreviewRepeatedPlacement(BuildPlacementRequestUx3C(draft, plan.CorrectedPosition.Value)).IsValid)
                    return plan.CorrectedPosition;
            }
        return null;
    }

    private StudioRepeatedPlacementRequest BuildPlacementRequestUx3C(
        StudioUx3CDropDraft draft,
        Vector3Value position)
    {
        var current = WorkspaceProfile.RepeatedPlacementProfile;
        var profile = new EditorRepeatedPlacementProfile(current.Id,
            EditorRepeatedPlacementProfile.CurrentSchemaVersion, "studio:ux3-c-drop", true, false, false,
            EditorPlacementDistribution.Grid, EditorPlacementAlignment.GridPlane, 1, 1, 32026, 0);
        var blueprint = Project!.Blueprints.Single(item => item.Id.Value == draft.BlueprintId);
        return new StudioRepeatedPlacementRequest(BlueprintId.From(draft.BlueprintId), position, profile, 1,
            ApplyCorrections: true, AvailableSockets: AvailablePlacementSockets,
            BlueprintEstimatedBytes: blueprint.Authoring.PlatformByteBudget);
    }

    private static StudioUx3CDiscoveryDraft FreezeDiscoveryDraftUx3C(StudioUx3CDiscoveryDraft draft) => draft with
    {
        Kinds = draft.Kinds.ToArray(),
        Kits = draft.Kits.ToArray(),
        Styles = draft.Styles.ToArray(),
        Licenses = draft.Licenses.ToArray(),
        Compatibility = draft.Compatibility.ToArray(),
        Tags = draft.Tags.ToArray(),
    };

    private static StudioUx3CDiscoveryPreview FailedDiscoveryUx3C(string message, string code, string path) =>
        new(false, false, false, true, message, [], [new(code, path, message)], null);

    private static StudioUx3CDropPreview FailedDropUx3C(string message, string code, string path) =>
        new(false, false, false, true, message, code, path, null, [], [new(code, path, message)]);

    private static string? ResolvePreviewPathUx3C(string? root, string? relative)
    {
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(relative) ||
            relative.StartsWith("pack://", StringComparison.Ordinal) || Path.IsPathRooted(relative)) return null;
        try
        {
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var candidate = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            return candidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) ? candidate : null;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string WorkspaceFingerprintUx3C(EditorWorkspaceProfile profile) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            EditorWorkspaceProfileJsonSerializer.Serialize(profile)))).ToLowerInvariant();

    private static bool WorkspaceNeighborsUx3C(EditorWorkspaceProfile source, EditorWorkspaceProfile candidate) =>
        EditorWorkspaceProfileJsonSerializer.Serialize(NormalizeWorkspaceUx3C(source)) ==
        EditorWorkspaceProfileJsonSerializer.Serialize(NormalizeWorkspaceUx3C(candidate));

    private static EditorWorkspaceProfile NormalizeWorkspaceUx3C(EditorWorkspaceProfile source) => new(
        source.Id, source.Name, source.Role, source.DisplayFilter, source.RepeatPlacement,
        source.OpenAreaTabs, source.ActiveAreaId, source.ComparisonAreaId, source.ContextualHelpEnabled,
        source.Provenance, EditorWorkspaceProfile.CurrentSchemaVersion, source.UiScale, source.TextScale,
        source.PrimaryFontFamily, source.FallbackFontFamilies, [], [], source.Shell,
        source.RepeatedPlacementProfile, source.MultiArea, source.Help, source.RolePolicy,
        source.NamedPaletteCollections);

    internal static bool WriteEvidenceThumbnailUx3C(string root, ProjectDefinition project, Guid blueprintId)
    {
        try
        {
            var preview = project.Blueprints.Single(item => item.Id.Value == blueprintId).Authoring.PreviewPath;
            var absolute = ResolvePreviewPathUx3C(root, preview);
            if (absolute is null) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllBytes(absolute, CreateEvidenceThumbnailPngUx3C());
            return File.Exists(absolute);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            return false;
        }
    }

    private static byte[] CreateEvidenceThumbnailPngUx3C()
    {
        using var png = new MemoryStream();
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);

        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header[..4], 2);
        BinaryPrimitives.WriteInt32BigEndian(header.Slice(4, 4), 2);
        header[8] = 8;
        header[9] = 6;
        WritePngChunkUx3C(png, "IHDR", header);

        byte[] pixels =
        [
            0, 50, 176, 214, 255, 91, 211, 232, 255,
            0, 91, 211, 232, 255, 50, 176, 214, 255,
        ];
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(pixels);
        WritePngChunkUx3C(png, "IDAT", compressed.ToArray());
        WritePngChunkUx3C(png, "IEND", []);
        return png.ToArray();
    }

    private static void WritePngChunkUx3C(Stream output, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);

        var crcInput = new byte[typeBytes.Length + data.Length];
        typeBytes.CopyTo(crcInput, 0);
        data.CopyTo(crcInput.AsSpan(typeBytes.Length));
        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, PngCrc32Ux3C(crcInput));
        output.Write(crc);
    }

    private static uint PngCrc32Ux3C(ReadOnlySpan<byte> data)
    {
        var crc = uint.MaxValue;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) == 0 ? crc >> 1 : 0xedb88320u ^ (crc >> 1);
        }
        return ~crc;
    }

    private static string HumanDropMessageUx3C(string code, string fallback) => code switch
    {
        BlueprintDropPlanner.InvalidTargetPathCode => "La cible de dépôt n’est pas un emplacement Studio sûr.",
        BlueprintDropPlanner.MissingBlueprintCode => "Le blueprint glissé n’existe plus.",
        BlueprintDropPlanner.ViewportContextMissingCode => "La zone active ne fournit pas de position de dépôt valide.",
        _ => fallback,
    };

    private static string HumanPathUx3C(string path) => path.Contains("blueprintDrops/", StringComparison.Ordinal)
        ? "dépôt de blueprint protégé"
        : path.Replace("entities", "objets", StringComparison.OrdinalIgnoreCase);

    private static void WriteUx3C(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static void WriteUx3C<T>(BinaryWriter writer, IEnumerable<T> values)
    {
        var items = values.Select(item => item?.ToString() ?? string.Empty).ToArray();
        writer.Write(items.Length);
        foreach (var item in items) WriteUx3C(writer, item);
    }

    private static StudioUx3CSmokeResult EmptyUx3CSmoke() => new(
        Succeeded: false,
        ExplicitSelection: false,
        UsesRealModuleEvidence: false,
        UsesCanonicalFallbackEvidence: false,
        Favorites: false,
        Recents: false,
        AdvancedSearch: false,
        ThumbnailMetadata: false,
        ThumbnailLoaded: false,
        HonestFallback: false,
        DragPayload: false,
        DropPlan: false,
        InvalidDropBlocked: false,
        DryRun: false,
        StaleSourceBlocked: false,
        StaleApprovalBlocked: false,
        SeparateApprovals: false,
        CanonicalCommands: false,
        UndoRedo: false,
        Persistence: false,
        BlueprintNeighborsPreserved: false,
        OtherAreasPreserved: false,
        ExistingEntitiesPreserved: false,
        TargetAreaTailsPreserved: false,
        AreaNeighborsPreserved: false,
        ProjectNeighborsPreserved: false,
        WorkspaceNeighborsPreserved: false,
        HumanText: false,
        RuntimeClaimsBounded: false,
        TemporaryWorkspaceReleased: true);
}
