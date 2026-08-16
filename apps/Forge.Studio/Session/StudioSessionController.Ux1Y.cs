using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux1YProvenance = "studio:ux1-y";
    private readonly SetWorldFeatureCatalogHandler _setTriggerCatalogUx1Y = new(new SystemClock());
    private StudioTriggerFoundationDraft? _approvedTriggerUx1Y;
    private ProjectRevision? _approvedTriggerRevisionUx1Y;

    internal IReadOnlyList<StudioTriggerChoice> TriggerChoicesUx1Y
    {
        get
        {
            var project = Project;
            var area = ActiveArea;
            if (project is null || area is null)
            {
                return [];
            }

            return project.Blueprints
                .Where(item => item.Kind == BlueprintKind.Trigger)
                .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Select(item =>
                {
                    var instances = area.Entities.Count(entity => entity.BlueprintId == item.Id);
                    var hasFoundation = project.Module.WorldFeatures.Triggers.Any(profile =>
                        profile.AreaId == area.Id && profile.BlueprintId == item.Id && profile.Foundation is not null);
                    var state = hasFoundation ? "volume configuré" : "à configurer";
                    return new StudioTriggerChoice(
                        item.Id,
                        $"{item.DisplayName} · {instances} instance(s) · {state}",
                        hasFoundation);
                })
                .ToArray();
        }
    }

    internal StudioTriggerFoundationDraft? CreateTriggerFoundationDraftUx1Y(
        BlueprintId? requestedBlueprintId = null)
    {
        var history = History;
        var project = Project;
        var area = ActiveArea;
        if (history is null || project is null || area is null || requestedBlueprintId is null)
        {
            return null;
        }

        var blueprint = project.Blueprints.FirstOrDefault(item =>
            item.Id == requestedBlueprintId && item.Kind == BlueprintKind.Trigger);
        if (blueprint is null)
        {
            return null;
        }

        var matches = project.Module.WorldFeatures.Triggers
            .Where(item => item.AreaId == area.Id && item.BlueprintId == blueprint.Id)
            .ToArray();
        if (matches.Length > 1)
        {
            return null;
        }

        var profile = matches.SingleOrDefault();
        var foundation = profile?.Foundation;
        var vertices = foundation?.Shape.Vertices.ToArray() ??
        [
            new Vector3Value(-2, 0, -2),
            new Vector3Value(2, 0, -2),
            new Vector3Value(2, 0, 2),
            new Vector3Value(-2, 0, 2),
        ];
        var holes = foundation?.Shape.Holes
            .Select(hole => (IReadOnlyList<Vector3Value>)hole.ToArray())
            .ToArray() ?? [];
        var profileId = profile?.Id ?? StableTriggerIdUx1Y(blueprint.Id, area.Id, "profile");
        var foundationId = foundation?.Id ?? StableTriggerIdUx1Y(blueprint.Id, area.Id, "foundation");
        var actions = foundation?.Actions.Select(item => new StudioTriggerActionDraft(
                item.Id,
                item.Kind,
                item.TargetId,
                item.Parameters))
            .ToArray() ??
        [
            new StudioTriggerActionDraft(
                StableTriggerIdUx1Y(blueprint.Id, area.Id, "action-primary"),
                "script",
                $"trigger:{blueprint.Slug}",
                new Dictionary<string, string>(StringComparer.Ordinal)),
        ];
        var onEnter = foundation?.OnEnter;
        var onExit = foundation?.OnExit;

        return new StudioTriggerFoundationDraft(
            blueprint.Id,
            area.Id,
            history.State.Revision,
            profileId,
            foundationId,
            foundation?.Slug ?? blueprint.Slug,
            foundation?.Shape.Kind ?? TriggerWorldProfile.ShapeKind.ExtrudedPolygon2D,
            vertices,
            holes,
            foundation?.Shape.Size ?? new Vector3Value(4, 3, 4),
            foundation?.Shape.Height ?? 3,
            foundation?.Shape.SnapIncrement ?? .25,
            foundation?.Shape.VisualizeInEditor ?? true,
            foundation?.Filter.EntityKinds.ToArray() ?? ["creature"],
            foundation?.Filter.RequiredTags.ToArray() ?? ["player"],
            foundation?.Filter.ExcludedTags.ToArray() ?? [],
            foundation?.Filter.Layers.ToArray() ?? ["characters"],
            foundation?.Conditions.ToArray() ?? ["area.ready"],
            foundation?.CooldownSeconds ?? 0,
            foundation?.Fire ?? TriggerWorldProfile.FirePolicy.OncePerEntity,
            actions,
            foundation?.Transform.Position ?? Vector3Value.Zero,
            foundation?.Transform.RotationDegrees ?? Vector3Value.Zero,
            foundation?.Transform.Scale ?? Vector3Value.One,
            new StudioTriggerEventDraft(
                onEnter?.Id ?? StableTriggerIdUx1Y(blueprint.Id, area.Id, "on-enter"),
                onEnter?.ForgeScriptApiVersion ?? "1.0.0",
                onEnter?.EventBinding ?? "VolumeEntered",
                onEnter?.BeforeScriptId,
                onEnter?.AfterScriptId),
            new StudioTriggerEventDraft(
                onExit?.Id ?? StableTriggerIdUx1Y(blueprint.Id, area.Id, "on-exit"),
                onExit?.ForgeScriptApiVersion ?? "1.0.0",
                onExit?.EventBinding ?? "VolumeExited",
                onExit?.BeforeScriptId,
                onExit?.AfterScriptId),
            profile?.FoundationRequiredAtRuntime ?? true,
            profile,
            foundation);
    }

    internal StudioTriggerFoundationPreview PreviewTriggerFoundationUx1Y(StudioTriggerFoundationDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelTriggerFoundationUx1Y();
        var history = History;
        if (history is null || Project is null)
        {
            return FailedTriggerPreviewUx1Y("Aucun module n’est ouvert.");
        }
        if (draft.BaseRevision != history.State.Revision)
        {
            return FailedTriggerPreviewUx1Y(
                "Le projet a changé depuis l’ouverture de cette fiche. Rechargez le déclencheur avant de prévisualiser.");
        }
        if (!TryBuildTriggerFoundationUx1Y(draft, dryRun: true, out var command, out var profile,
                out var message))
        {
            return FailedTriggerPreviewUx1Y(message);
        }

        var result = _setTriggerCatalogUx1Y.Handle(history.State, command!);
        if (!result.Succeeded)
        {
            return FailedTriggerPreviewUx1Y(HumanizeTriggerUx1Y(result.Diagnostics.FirstOrDefault()?.Code));
        }

        var runtime = WorldFeatureRuntimeResolver.ResolveTriggerFoundation(profile!);
        if (!runtime.Available || runtime.SnappedShape is null || runtime.SnappedTransform is null)
        {
            return FailedTriggerPreviewUx1Y(HumanizeTriggerUx1Y(runtime.DiagnosticCode));
        }

        var shape = runtime.SnappedShape;
        var transform = runtime.SnappedTransform;
        var shapeLabel = HumanizeShapeUx1Y(shape.Kind);
        var summary = FormattableString.Invariant(
            $"{shapeLabel} · {shape.Vertices.Count} sommet(s) · {shape.Holes.Count} contour(s) intérieur(s) · hauteur {shape.Height:0.##} m · grille {shape.SnapIncrement:0.##} m\nTransform : X {transform.Position.X:0.##}, Y {transform.Position.Y:0.##}, Z {transform.Position.Z:0.##} · rotation {transform.RotationDegrees.Y:0.##}° · échelle {transform.Scale.X:0.##}/{transform.Scale.Y:0.##}/{transform.Scale.Z:0.##}\nPolitique : {draft.EntityKinds.Count} type(s) · {draft.RequiredTags.Count} tag(s) requis · {draft.Conditions.Count} condition(s) · délai {draft.CooldownSeconds:0.##} s · {HumanizeFireUx1Y(draft.FirePolicy)}\nActions : {draft.Actions.Count} · entrée {draft.OnEnter.EventBinding} · sortie {draft.OnExit.EventBinding} · API {draft.OnEnter.ForgeScriptApiVersion}\nAperçu 2D lisible dans l’outil · portail, validation avancée et profil Advanced préservés sans être modifiés.");

        _approvedTriggerUx1Y = draft;
        _approvedTriggerRevisionUx1Y = history.State.Revision;
        return new StudioTriggerFoundationPreview(
            true,
            "Aperçu validé · aucun document n’a été modifié.",
            summary,
            true,
            shape.Vertices,
            shape.Holes);
    }

    internal StudioUx1YActionResult ApplyApprovedTriggerFoundationUx1Y()
    {
        var draft = _approvedTriggerUx1Y;
        var history = History;
        if (draft is null || history is null || _approvedTriggerRevisionUx1Y != history.State.Revision)
        {
            CancelTriggerFoundationUx1Y();
            return new StudioUx1YActionResult(false,
                "Prévisualisez de nouveau le déclencheur avant de l’appliquer.");
        }

        if (!TryBuildTriggerFoundationUx1Y(draft, dryRun: false, out var command, out _, out var message))
        {
            CancelTriggerFoundationUx1Y();
            return new StudioUx1YActionResult(false, message);
        }

        var result = _setTriggerCatalogUx1Y.Handle(history.State, command!);
        CancelTriggerFoundationUx1Y();
        if (!result.Succeeded)
        {
            return new StudioUx1YActionResult(
                false,
                HumanizeTriggerUx1Y(result.Diagnostics.FirstOrDefault()?.Code));
        }

        history.Record(result);
        InvalidateContentIndexes();
        return new StudioUx1YActionResult(
            true,
            "Déclencheur appliqué · Ctrl+Z pour annuler · Ctrl+S pour enregistrer.");
    }

    internal void CancelTriggerFoundationUx1Y()
    {
        _approvedTriggerUx1Y = null;
        _approvedTriggerRevisionUx1Y = null;
    }

    internal StudioUx1YSmokeResult RunUx1YContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux1y-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var triggerBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1d30000-0000-4000-8000-000000000001")),
                "seuil-des-brumes",
                "Seuil des brumes",
                BlueprintKind.Trigger);
            var neighborBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1d30000-0000-4000-8000-000000000002")),
                "alarme-du-jardin",
                "Alarme du jardin",
                BlueprintKind.Trigger);
            var area = new AreaDefinition(
                AreaId.From(Guid.Parse("c1d30000-0000-4000-8000-000000000003")),
                "jardin-des-brumes",
                "Jardin des brumes",
                [],
                AreaKind.Exterior,
                new AreaDimensions(64, 64));
            var shell = new ProjectDefinition(
                ProjectId.From(Guid.Parse("c1d30000-0000-4000-8000-000000000004")),
                "Studio UX1-Y",
                [triggerBlueprint, neighborBlueprint],
                [area]);
            var originalProfile = CreateUx1YSmokeTrigger(triggerBlueprint.Id, area.Id, neighbor: false);
            var neighborProfile = CreateUx1YSmokeTrigger(neighborBlueprint.Id, area.Id, neighbor: true);
            neighborProfile = neighborProfile with
            {
                Foundation = neighborProfile.Foundation! with
                {
                    Shape = new TriggerWorldProfile.ShapePolicy(
                        TriggerWorldProfile.ShapeKind.Box3D,
                        [],
                        [],
                        new Vector3Value(4, 3, 4),
                        3,
                        .25,
                        true),
                },
            };
            var catalog = new WorldFeatureCatalog(
                shell.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-y",
                triggers: [originalProfile, neighborProfile]);
            var project = new ProjectDefinition(
                shell.Id,
                shell.Title,
                shell.Module.WithWorldFeatures(catalog),
                shell.Blueprints,
                shell.Areas,
                shell.Dialogues,
                shell.Quests);
            var created = ProjectSession.Create(tempRoot, project);
            if (!created.Succeeded || !editing.Open(tempRoot).Succeeded)
            {
                return FailedUx1YSmoke();
            }

            var originalProject = editing.Project!;
            var originalCatalog = originalProject.Module.WorldFeatures;
            var loadedOriginalProfile = originalCatalog.Triggers.Single(item => item.Id == originalProfile.Id);
            var loadedNeighborProfile = originalCatalog.Triggers.Single(item => item.Id == neighborProfile.Id);
            var selectionExplicit = editing.CreateTriggerFoundationDraftUx1Y() is null &&
                                    editing.TriggerChoicesUx1Y.Count == 2;
            var nonPolygonDraft = editing.CreateTriggerFoundationDraftUx1Y(neighborBlueprint.Id)!;
            var nonPolygonPreview = editing.PreviewTriggerFoundationUx1Y(nonPolygonDraft);
            var nonPolygonBlocked = !nonPolygonPreview.Succeeded &&
                                    nonPolygonPreview.Message.Contains("polygones extrudés", StringComparison.Ordinal) &&
                                    ReferenceEquals(originalProject, editing.Project);

            var staleDraft = editing.CreateTriggerFoundationDraftUx1Y(triggerBlueprint.Id)!;
            var staleDraftMutationApplied = ApplyUx1YHiddenFoundationMutation(
                editing,
                originalProfile.Id);
            var hiddenMutationProject = editing.Project!;
            var hiddenMutationFoundation = hiddenMutationProject.Module.WorldFeatures.Triggers
                .Single(item => item.Id == originalProfile.Id)
                .Foundation!;
            var hiddenStateChanged = hiddenMutationFoundation.Portal!.DestinationId !=
                                     originalProfile.Foundation!.Portal!.DestinationId &&
                                     hiddenMutationFoundation.Validation!.KnownPortalDestinationIds
                                         .SequenceEqual([hiddenMutationFoundation.Portal.DestinationId]) &&
                                     hiddenMutationFoundation.Overrides.ContainsKey("hidden.revision") &&
                                     hiddenMutationFoundation.Transform.ParentId is not null;
            var staleDraftPreview = editing.PreviewTriggerFoundationUx1Y(staleDraft);
            var staleDraftBlocked = staleDraftMutationApplied && !staleDraftPreview.Succeeded &&
                                    staleDraftPreview.Message.Contains("depuis l’ouverture", StringComparison.Ordinal) &&
                                    hiddenStateChanged && ReferenceEquals(hiddenMutationProject, editing.Project);
            var staleDraftUndo = staleDraftMutationApplied && editing.Undo().Succeeded;
            var staleDraftRestored = staleDraftUndo && ReferenceEquals(originalProject, editing.Project);

            var draft = CustomizeUx1YSmokeDraft(
                editing.CreateTriggerFoundationDraftUx1Y(triggerBlueprint.Id)!);

            var selfIntersecting = draft with
            {
                Vertices =
                [
                    new(-2, 0, -2),
                    new(2, 0, 2),
                    new(-2, 0, 2),
                    new(2, 0, -2),
                ],
            };
            var invalidGeometry = editing.PreviewTriggerFoundationUx1Y(selfIntersecting);
            var invalidFilter = editing.PreviewTriggerFoundationUx1Y(draft with
            {
                ExcludedTags = draft.ExcludedTags.Append("player").ToArray(),
            });
            var invalidBinding = editing.PreviewTriggerFoundationUx1Y(draft with
            {
                OnEnter = draft.OnEnter with { EventBinding = "Entered" },
            });
            var preview = editing.PreviewTriggerFoundationUx1Y(draft);
            var dryRun = preview.Succeeded && preview.CanApply && ReferenceEquals(originalProject, editing.Project);
            var intermediateApplied = ApplyUx1YIntermediateMutation(
                editing,
                neighborProfile.Id,
                "trigger.neighbor.stale-approved");
            var staleApply = editing.ApplyApprovedTriggerFoundationUx1Y();
            var staleRevisionBlocked = intermediateApplied && !staleApply.Succeeded &&
                                       staleApply.Message.Contains("Prévisualisez de nouveau", StringComparison.Ordinal);
            var intermediateUndo = intermediateApplied && editing.Undo().Succeeded;
            var intermediateRestored = intermediateUndo && ReferenceEquals(originalProject, editing.Project);
            var freshDraft = CustomizeUx1YSmokeDraft(
                editing.CreateTriggerFoundationDraftUx1Y(triggerBlueprint.Id)!);
            var freshPreview = editing.PreviewTriggerFoundationUx1Y(freshDraft);
            var apply = editing.ApplyApprovedTriggerFoundationUx1Y();
            var appliedProject = editing.Project!;
            var applied = appliedProject.Module.WorldFeatures.Triggers.Single(item => item.Id == originalProfile.Id);
            var resolved = WorldFeatureRuntimeResolver.ResolveTriggerFoundation(applied);
            var geometry = apply.Succeeded && resolved.Available && resolved.SnappedShape is
            {
                Vertices.Count: 5,
                Holes.Count: 1,
                Height: 4.5,
                SnapIncrement: 0.5,
            } && resolved.SnappedTransform is
            {
                Position.X: 1.5f,
                Position.Z: -2.5f,
                RotationDegrees.Y: 35,
                Scale.X: 1.25f,
            } && freshPreview.Succeeded &&
                freshPreview.Summary.Contains("aperçu 2D", StringComparison.OrdinalIgnoreCase);

            var state = new WorldFeatureRuntimeResolver.TriggerRuntimeState(
                new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal),
                new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal),
                false);
            var entered = WorldFeatureRuntimeResolver.EnterTrigger(
                applied,
                state,
                "player:ilyne",
                "creature",
                new HashSet<string>(["player"], StringComparer.Ordinal),
                new HashSet<string>(["characters"], StringComparer.Ordinal),
                new Vector3Value(1, 0, 2),
                new Vector3Value(0, 0, 1),
                new Dictionary<string, string>(StringComparer.Ordinal) { ["area.ready"] = "true" },
                DateTimeOffset.UnixEpoch);
            var exited = WorldFeatureRuntimeResolver.ExitTrigger(
                applied,
                entered.State,
                "player:ilyne",
                DateTimeOffset.UnixEpoch.AddSeconds(9),
                "walked-out");
            var typedEvents = entered is { Fired: true, Event.EventBinding: "VolumeEntered" } &&
                              entered.Actions.Single().TargetId == "encounter:guardian" &&
                              exited.Event is { EventBinding: "VolumeExited", DurationInside.TotalSeconds: 9 };

            var portalPreserved = ReferenceEquals(
                applied.Foundation!.Portal,
                loadedOriginalProfile.Foundation!.Portal);
            var validationPreserved = ReferenceEquals(
                applied.Foundation!.Validation,
                loadedOriginalProfile.Foundation!.Validation);
            var advancedPreserved = ReferenceEquals(applied.Advanced, loadedOriginalProfile.Advanced) &&
                                    applied.AdvancedRequiredAtRuntime ==
                                    loadedOriginalProfile.AdvancedRequiredAtRuntime;
            var neighborPreserved = ReferenceEquals(
                                        appliedProject.Module.WorldFeatures.Triggers.Single(item =>
                                            item.Id == neighborProfile.Id),
                                        loadedNeighborProfile) &&
                                    appliedProject.Module.WorldFeatures.Placeables.SequenceEqual(originalCatalog.Placeables) &&
                                    appliedProject.Module.WorldFeatures.Doors.SequenceEqual(originalCatalog.Doors) &&
                                    appliedProject.Module.WorldFeatures.Encounters.SequenceEqual(originalCatalog.Encounters) &&
                                    appliedProject.Module.WorldFeatures.VehicleRoutes.SequenceEqual(originalCatalog.VehicleRoutes) &&
                                    appliedProject.Module.WorldFeatures.Merchants.SequenceEqual(originalCatalog.Merchants) &&
                                    appliedProject.Module.WorldFeatures.Companions.SequenceEqual(originalCatalog.Companions) &&
                                    ReferenceEquals(appliedProject.Module.WorldFeatures.Scripts, originalCatalog.Scripts);
            var journal = editing.History!.Journal;
            var canonicalCommand = staleDraftRestored && intermediateRestored && journal.Count == 3 &&
                                   journal.All(item => item.CommandName == "set_world_feature_catalog") &&
                                   !journal[0].IsApplied && !journal[1].IsApplied && journal[2].IsApplied;
            var undo = editing.Undo();
            var undone = ReferenceEquals(originalProject, editing.Project);
            var redo = editing.Redo();
            var redone = ReferenceEquals(appliedProject, editing.Project);
            var undoRedo = undo.Succeeded && undone && redo.Succeeded && redone;

            var save = editing.Save();
            editing.Close();
            var reopen = reopened.Open(tempRoot);
            var restored = reopened.Project?.Module.WorldFeatures.Triggers.SingleOrDefault(item =>
                item.Id == originalProfile.Id);
            var restoredPortal = restored?.Foundation?.Portal;
            var originalPortal = originalProfile.Foundation!.Portal!;
            var restoredValidation = restored?.Foundation?.Validation;
            var originalValidation = originalProfile.Foundation!.Validation!;
            var restoredAdvanced = restored?.Advanced;
            var persistence = save.Succeeded && reopen.Succeeded && restored?.Foundation is
            {
                Shape.Vertices.Count: 5,
                Shape.Holes.Count: 1,
                Shape.Height: 4.5,
                Shape.SnapIncrement: 0.5,
                Transform.Position.X: 1.26f,
                Transform.Position.Z: -2.26f,
                OnEnter.EventBinding: "VolumeEntered",
                OnExit.EventBinding: "VolumeExited",
            } && restoredPortal is not null &&
                restoredPortal.Destination == originalPortal.Destination &&
                restoredPortal.DestinationId == originalPortal.DestinationId &&
                restoredPortal.SpawnAnchorId == originalPortal.SpawnAnchorId &&
                restoredPortal.ExitOrientationDegrees == originalPortal.ExitOrientationDegrees &&
                restoredPortal.Preload == originalPortal.Preload &&
                restoredPortal.Permissions.SequenceEqual(originalPortal.Permissions) &&
                restoredValidation is not null &&
                restoredValidation.KnownPortalDestinationIds.SequenceEqual(
                    originalValidation.KnownPortalDestinationIds) &&
                restoredValidation.KnownTargetIds.SequenceEqual(originalValidation.KnownTargetIds) &&
                restoredValidation.KnownScriptIds.SequenceEqual(originalValidation.KnownScriptIds) &&
                restoredValidation.AccessibleLayers.SequenceEqual(originalValidation.AccessibleLayers) &&
                restoredAdvanced is not null && originalProfile.Advanced is not null &&
                restoredAdvanced.Id == originalProfile.Advanced.Id &&
                restoredAdvanced.Trap == originalProfile.Advanced.Trap &&
                restoredAdvanced.TargetFilter.Id == originalProfile.Advanced.TargetFilter.Id &&
                restoredAdvanced.GameplayVolumes.Select(item => item.Id).SequenceEqual(
                    originalProfile.Advanced.GameplayVolumes.Select(item => item.Id)) &&
                restoredAdvanced.OccupancyDebug == originalProfile.Advanced.OccupancyDebug;
            var humanDiagnostics = IsHumanUx1YMessage(invalidGeometry.Message) &&
                                   IsHumanUx1YMessage(invalidFilter.Message) &&
                                   IsHumanUx1YMessage(invalidBinding.Message);
            var succeeded = selectionExplicit && geometry && dryRun && !invalidGeometry.Succeeded &&
                            !invalidFilter.Succeeded && !invalidBinding.Succeeded && staleDraftBlocked &&
                            staleRevisionBlocked && nonPolygonBlocked && typedEvents &&
                            canonicalCommand && undoRedo && persistence && portalPreserved &&
                            validationPreserved && advancedPreserved && neighborPreserved && humanDiagnostics;
            return new StudioUx1YSmokeResult(
                succeeded,
                selectionExplicit,
                geometry,
                dryRun,
                !invalidGeometry.Succeeded,
                !invalidFilter.Succeeded,
                !invalidBinding.Succeeded,
                staleDraftBlocked,
                staleRevisionBlocked,
                nonPolygonBlocked,
                typedEvents,
                canonicalCommand,
                undoRedo,
                persistence,
                portalPreserved,
                validationPreserved,
                advancedPreserved,
                neighborPreserved,
                humanDiagnostics);
        }
        catch (Exception)
        {
            return FailedUx1YSmoke();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private bool TryBuildTriggerFoundationUx1Y(
        StudioTriggerFoundationDraft draft,
        bool dryRun,
        out SetWorldFeatureCatalogCommand? command,
        out TriggerWorldProfile? profile,
        out string message)
    {
        command = null;
        profile = null;
        message = string.Empty;
        var history = History;
        var project = Project;
        if (history is null || project is null)
        {
            message = "Aucun module n’est ouvert.";
            return false;
        }
        if (draft.BaseRevision != history.State.Revision)
        {
            message = "Le projet a changé depuis l’ouverture de cette fiche. Rechargez le déclencheur.";
            return false;
        }
        if (!project.Areas.Any(item => item.Id == draft.AreaId))
        {
            message = "La zone choisie n’existe plus.";
            return false;
        }
        var blueprint = project.Blueprints.FirstOrDefault(item =>
            item.Id == draft.BlueprintId && item.Kind == BlueprintKind.Trigger);
        if (blueprint is null)
        {
            message = "Le blueprint de déclencheur choisi n’existe plus.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(draft.Slug))
        {
            message = "Donnez un nom court au déclencheur.";
            return false;
        }
        if (!ValidateTriggerGeometryUx1Y(draft, out message) ||
            !ValidateTriggerPolicyUx1Y(draft, out message) ||
            !ValidateTriggerEventsUx1Y(draft, out message))
        {
            return false;
        }

        var originalFoundation = draft.OriginalFoundation;
        var shape = new TriggerWorldProfile.ShapePolicy(
            draft.ShapeKind,
            draft.Vertices.ToArray(),
            draft.Holes.Select(hole => (IReadOnlyList<Vector3Value>)hole.ToArray()).ToArray(),
            draft.Size,
            draft.Height,
            draft.SnapIncrement,
            draft.VisualizeInEditor);
        var filter = new TriggerWorldProfile.FilterPolicy(
            draft.EntityKinds.Select(item => item.Trim()).ToArray(),
            draft.RequiredTags.Select(item => item.Trim()).ToArray(),
            draft.ExcludedTags.Select(item => item.Trim()).ToArray(),
            draft.Layers.Select(item => item.Trim()).ToArray());
        var actions = draft.Actions.Select(item => new TriggerWorldProfile.ActionPolicy(
            item.Id,
            item.Kind.Trim(),
            item.TargetId.Trim(),
            item.Parameters)).ToArray();
        var transform = new TriggerWorldProfile.TransformPolicy(
            draft.Position,
            draft.RotationDegrees,
            draft.Scale,
            originalFoundation?.Transform.ParentId);
        var onEnter = BuildTriggerEventUx1Y(
            draft.OnEnter,
            originalFoundation?.OnEnter,
            "VolumeEntered");
        var onExit = BuildTriggerEventUx1Y(
            draft.OnExit,
            originalFoundation?.OnExit,
            "VolumeExited");
        var foundation = originalFoundation is null
            ? new TriggerWorldProfile.FoundationPolicy(
                draft.FoundationId,
                WorldFeatureCatalog.CurrentSchemaVersion,
                Ux1YProvenance,
                draft.Slug.Trim(),
                shape,
                filter,
                draft.Conditions.Select(item => item.Trim()).ToArray(),
                draft.CooldownSeconds,
                draft.FirePolicy,
                actions,
                null,
                transform,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["editor.overlay"] = "volume",
                },
                onEnter,
                onExit)
            : originalFoundation with
            {
                Slug = draft.Slug.Trim(),
                Shape = shape,
                Filter = filter,
                Conditions = draft.Conditions.Select(item => item.Trim()).ToArray(),
                CooldownSeconds = draft.CooldownSeconds,
                Fire = draft.FirePolicy,
                Actions = actions,
                Transform = transform,
                OnEnter = onEnter,
                OnExit = onExit,
            };

        var currentCatalog = project.Module.WorldFeatures;
        var currentProfile = currentCatalog.Triggers.FirstOrDefault(item => item.Id == draft.ProfileId);
        if (draft.OriginalProfile is not null && currentProfile is null)
        {
            message = "Le déclencheur a changé depuis son ouverture. Rechargez sa fiche.";
            return false;
        }
        var builtProfile = currentProfile is null
            ? new TriggerWorldProfile(
                Id: draft.ProfileId,
                SchemaVersion: WorldFeatureCatalog.CurrentSchemaVersion,
                Provenance: Ux1YProvenance,
                BlueprintId: draft.BlueprintId,
                AreaId: draft.AreaId,
                Discovery: null,
                Interaction: null,
                Scheduler: null,
                CustomEventNamespace: $"trigger.{blueprint.Slug}",
                Access: null,
                Rest: null,
                Clue: null,
                Narrative: null,
                Director: null,
                Foundation: foundation,
                FoundationRequiredAtRuntime: draft.RequireRuntime,
                Advanced: null,
                AdvancedRequiredAtRuntime: false)
            : currentProfile with
            {
                Foundation = foundation,
                FoundationRequiredAtRuntime = draft.RequireRuntime,
            };
        profile = builtProfile;
        var triggers = currentProfile is null
            ? currentCatalog.Triggers.Append(builtProfile).ToArray()
            : currentCatalog.Triggers.Select(item =>
                item.Id == currentProfile.Id ? builtProfile : item).ToArray();
        var candidateCatalog = new WorldFeatureCatalog(
            currentCatalog.Id,
            currentCatalog.SchemaVersion,
            currentCatalog.Provenance,
            currentCatalog.Placeables,
            currentCatalog.Doors,
            triggers,
            currentCatalog.Encounters,
            currentCatalog.VehicleRoutes,
            currentCatalog.Merchants,
            currentCatalog.Companions,
            currentCatalog.Scripts);
        command = new SetWorldFeatureCatalogCommand(
            CommandId.From(Guid.NewGuid()),
            history.State.Revision,
            candidateCatalog,
            dryRun);
        return true;
    }

    private static bool ValidateTriggerGeometryUx1Y(
        StudioTriggerFoundationDraft draft,
        out string message)
    {
        message = string.Empty;
        if (draft.ShapeKind != TriggerWorldProfile.ShapeKind.ExtrudedPolygon2D)
        {
            message =
                "Cette fiche UX1-Y édite uniquement les polygones extrudés. Le volume 3D existant reste préservé sans conversion.";
            return false;
        }
        var vectors = draft.Vertices
            .Concat(draft.Holes.SelectMany(item => item))
            .Append(draft.Size)
            .Append(draft.Position)
            .Append(draft.RotationDegrees)
            .Append(draft.Scale)
            .ToArray();
        if (vectors.Any(item => !float.IsFinite(item.X) || !float.IsFinite(item.Y) || !float.IsFinite(item.Z)) ||
            !double.IsFinite(draft.Height) || !double.IsFinite(draft.SnapIncrement) ||
            draft.SnapIncrement is < 0 or > 10_000 ||
            draft.Scale.X is <= .01f or > 100 || draft.Scale.Y is <= .01f or > 100 ||
            draft.Scale.Z is <= .01f or > 100)
        {
            message = "La géométrie et la transformation doivent rester finies et dans les limites du Studio.";
            return false;
        }

        var kindValid = draft.Vertices.Count >= 3 && draft.Height > 0 &&
                        draft.Holes.All(item => item.Count >= 3);
        if (!kindValid)
        {
            message = "Le volume exige assez de sommets, des contours fermés et des dimensions positives.";
            return false;
        }
        if (GeometryPolygonValidator.Validate(draft.Vertices, "trigger/contour").Count > 0 ||
            draft.Holes.Any(hole => GeometryPolygonValidator.Validate(hole, "trigger/trou").Count > 0))
        {
            message = "Un contour du volume se croise. Déplacez ses sommets avant de prévisualiser.";
            return false;
        }
        return true;
    }

    private static bool ValidateTriggerPolicyUx1Y(
        StudioTriggerFoundationDraft draft,
        out string message)
    {
        message = string.Empty;
        var lists = new IReadOnlyList<string>[]
        {
            draft.EntityKinds,
            draft.RequiredTags,
            draft.ExcludedTags,
            draft.Layers,
            draft.Conditions,
        };
        if (draft.EntityKinds.Count == 0 || lists.Any(values =>
                values.Any(string.IsNullOrWhiteSpace) ||
                values.Select(item => item.Trim()).Distinct(StringComparer.Ordinal).Count() != values.Count) ||
            !double.IsFinite(draft.CooldownSeconds) || draft.CooldownSeconds < 0)
        {
            message = "Les filtres, conditions et le délai doivent contenir des valeurs nommées uniques.";
            return false;
        }
        if (draft.RequiredTags.Intersect(draft.ExcludedTags, StringComparer.Ordinal).Any())
        {
            message = "Un même tag ne peut pas être à la fois requis et exclu.";
            return false;
        }
        if (draft.Actions.Count == 0 || draft.Actions.Any(item =>
                item.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.Kind) ||
                string.IsNullOrWhiteSpace(item.TargetId) ||
                item.Parameters.Any(parameter => string.IsNullOrWhiteSpace(parameter.Key))) ||
            draft.Actions.Select(item => item.Id).Distinct().Count() != draft.Actions.Count)
        {
            message = "Ajoutez au moins une action avec un type et une cible lisibles.";
            return false;
        }
        return true;
    }

    private static bool ValidateTriggerEventsUx1Y(
        StudioTriggerFoundationDraft draft,
        out string message)
    {
        message = string.Empty;
        if (!ValidTriggerEventUx1Y(draft.OnEnter, "VolumeEntered") ||
            !ValidTriggerEventUx1Y(draft.OnExit, "VolumeExited"))
        {
            message = "Les événements d’entrée et de sortie exigent leur liaison canonique et une version ForgeScript valide.";
            return false;
        }
        return true;
    }

    private static bool ValidTriggerEventUx1Y(StudioTriggerEventDraft value, string expectedBinding) =>
        value.Id != Guid.Empty &&
        Version.TryParse(value.ForgeScriptApiVersion, out _) &&
        string.Equals(value.EventBinding, expectedBinding, StringComparison.Ordinal) &&
        value.BeforeScriptId is not { Length: 0 } && value.AfterScriptId is not { Length: 0 };

    private static TriggerWorldProfile.EventPolicy BuildTriggerEventUx1Y(
        StudioTriggerEventDraft draft,
        TriggerWorldProfile.EventPolicy? original,
        string binding)
    {
        var before = NullIfWhiteSpaceUx1Y(draft.BeforeScriptId);
        var after = NullIfWhiteSpaceUx1Y(draft.AfterScriptId);
        return original is null
            ? new TriggerWorldProfile.EventPolicy(
                draft.Id,
                WorldFeatureCatalog.CurrentSchemaVersion,
                Ux1YProvenance,
                draft.ForgeScriptApiVersion.Trim(),
                binding,
                before,
                after)
            : original with
            {
                ForgeScriptApiVersion = draft.ForgeScriptApiVersion.Trim(),
                EventBinding = binding,
                BeforeScriptId = before,
                AfterScriptId = after,
            };
    }

    private static StudioTriggerFoundationDraft CustomizeUx1YSmokeDraft(
        StudioTriggerFoundationDraft draft) => draft with
    {
        Vertices =
        [
            new(-3.1f, 0, -2.1f),
            new(3.1f, 0, -2.1f),
            new(3.1f, 0, 2.1f),
            new(0, 0, 3.1f),
            new(-3.1f, 0, 2.1f),
        ],
        Holes =
        [
            [new Vector3Value(-.6f, 0, -.4f), new(.6f, 0, -.4f), new(0, 0, .7f)],
        ],
        Height = 4.5,
        SnapIncrement = .5,
        Position = new Vector3Value(1.26f, 0, -2.26f),
        RotationDegrees = new Vector3Value(0, 35, 0),
        Scale = new Vector3Value(1.25f, 1, .9f),
        CooldownSeconds = 2,
        OnEnter = draft.OnEnter with
        {
            BeforeScriptId = "scripts/enter-before",
            AfterScriptId = "scripts/enter-after",
        },
        OnExit = draft.OnExit with
        {
            AfterScriptId = "scripts/exit-after",
        },
    };

    private static bool ApplyUx1YIntermediateMutation(
        StudioSessionController editing,
        Guid neighborProfileId,
        string customEventNamespace) => ApplyUx1YProfileMutation(
        editing,
        neighborProfileId,
        profile => profile with { CustomEventNamespace = customEventNamespace });

    private static bool ApplyUx1YHiddenFoundationMutation(
        StudioSessionController editing,
        Guid profileId)
    {
        var replacementDestination = Guid.Parse("c1d30000-0000-4000-8200-000000000001");
        var replacementParent = Guid.Parse("c1d30000-0000-4000-8200-000000000002");
        return ApplyUx1YProfileMutation(
            editing,
            profileId,
            profile =>
            {
                var foundation = profile.Foundation!;
                return profile with
                {
                    Foundation = foundation with
                    {
                        Portal = foundation.Portal! with
                        {
                            DestinationId = replacementDestination,
                            SpawnAnchorId = "anchor:changed-after-draft",
                        },
                        Validation = foundation.Validation! with
                        {
                            KnownPortalDestinationIds = [replacementDestination],
                        },
                        Overrides = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["editor.overlay"] = "volume",
                            ["hidden.revision"] = "changed-after-draft",
                        },
                        Transform = foundation.Transform with { ParentId = replacementParent },
                    },
                };
            });
    }

    private static bool ApplyUx1YProfileMutation(
        StudioSessionController editing,
        Guid profileId,
        Func<TriggerWorldProfile, TriggerWorldProfile> mutate)
    {
        var history = editing.History;
        var project = editing.Project;
        if (history is null || project is null)
        {
            return false;
        }

        var currentCatalog = project.Module.WorldFeatures;
        var currentProfile = currentCatalog.Triggers.SingleOrDefault(item => item.Id == profileId);
        if (currentProfile is null)
        {
            return false;
        }

        var intermediateProfile = mutate(currentProfile);
        var intermediateCatalog = new WorldFeatureCatalog(
            currentCatalog.Id,
            currentCatalog.SchemaVersion,
            currentCatalog.Provenance,
            currentCatalog.Placeables,
            currentCatalog.Doors,
            currentCatalog.Triggers
                .Where(item => item.Id != intermediateProfile.Id)
                .Append(intermediateProfile)
                .ToArray(),
            currentCatalog.Encounters,
            currentCatalog.VehicleRoutes,
            currentCatalog.Merchants,
            currentCatalog.Companions,
            currentCatalog.Scripts);
        var intermediateCommand = new SetWorldFeatureCatalogCommand(
            CommandId.From(Guid.NewGuid()),
            history.State.Revision,
            intermediateCatalog,
            DryRun: false);
        var intermediateResult = editing._setTriggerCatalogUx1Y.Handle(history.State, intermediateCommand);
        if (!intermediateResult.Succeeded)
        {
            return false;
        }

        history.Record(intermediateResult);
        return true;
    }

    private static TriggerWorldProfile CreateUx1YSmokeTrigger(
        BlueprintId blueprintId,
        AreaId areaId,
        bool neighbor)
    {
        var prefix = neighbor ? "c1d30000-0000-4000-8100" : "c1d30000-0000-4000-8000";
        Guid Id(int suffix) => Guid.Parse($"{prefix}-{suffix:000000000000}");
        var portalDestination = Id(30);
        var foundation = new TriggerWorldProfile.FoundationPolicy(
            Id(10),
            1,
            "test:ux1-y",
            neighbor ? "alarme-jardin" : "seuil-brumes",
            new(
                TriggerWorldProfile.ShapeKind.ExtrudedPolygon2D,
                [new(-2, 0, -2), new(2, 0, -2), new(2, 0, 2), new(-2, 0, 2)],
                [[new Vector3Value(-.5f, 0, -.4f), new(.5f, 0, -.4f), new(0, 0, .6f)]],
                new(4, 3, 4),
                3,
                .25,
                true),
            new(["creature"], ["player"], ["undead"], ["characters"]),
            ["area.ready"],
            5,
            TriggerWorldProfile.FirePolicy.OncePerEntity,
            [new(Id(11), "start-encounter", "encounter:guardian", new Dictionary<string, string>())],
            new(TriggerWorldProfile.DestinationKind.Waypoint, portalDestination, "anchor:garden-exit",
                new(0, 180, 0), true, ["portal.travel"]),
            new(new(1.24f, 0, -2.24f), Vector3Value.Zero, Vector3Value.One, null),
            new Dictionary<string, string>(StringComparer.Ordinal) { ["editor.overlay"] = "volume" },
            new(Id(12), 1, "test:ux1-y", "1.0.0", "VolumeEntered", "scripts/enter-before",
                "scripts/enter-after"),
            new(Id(13), 1, "test:ux1-y", "1.0.0", "VolumeExited", null, "scripts/exit-after"),
            new([portalDestination], ["encounter:guardian"],
                ["scripts/enter-before", "scripts/enter-after", "scripts/exit-after"], ["characters"]),
            true);
        var advanced = CreateUx1YAdvancedProfile(prefix);
        return new TriggerWorldProfile(
            Id(100),
            1,
            "test:ux1-y",
            blueprintId,
            areaId,
            null,
            null,
            null,
            neighbor ? "trigger.neighbor" : "trigger.primary",
            null,
            null,
            null,
            null,
            null,
            foundation,
            true,
            advanced,
            true);
    }

    private static TriggerAdvancedProfile CreateUx1YAdvancedProfile(string prefix)
    {
        Guid Id(int suffix) => Guid.Parse($"{prefix}-{suffix:000000000000}");
        var volumes = Enum.GetValues<TriggerGameplayVolumeKind>()
            .Select((kind, index) => new TriggerGameplayVolumeProfile(
                Id(50 + index),
                kind,
                new Dictionary<string, double> { [kind.ToString().ToLowerInvariant()] = index + 1 },
                new Dictionary<string, string>(),
                $"{kind}VolumeEntered",
                $"{kind}VolumeExited",
                true))
            .ToArray();
        return new TriggerAdvancedProfile(
            Id(40),
            1,
            "test:ux1-y-advanced",
            "1.0.0",
            new(Id(41), "enter", "damage:mist", 8, 10, 20, "garden", "vfx:mist", "allies", true,
                false, true, "#55aaff", "TrapDetected", "TrapDisarmed", "TrapTriggered", "TrapReset"),
            new(Id(42), true, ["player"], ["undead"], [], ["health"], "party", "heroes",
                "quest==active"),
            volumes,
            new(Id(43), true, true, true, true, true, true, 64));
    }

    private static StudioTriggerFoundationPreview FailedTriggerPreviewUx1Y(string message) =>
        new(false, message, string.Empty, false);

    private static StudioUx1YSmokeResult FailedUx1YSmoke() =>
        new(
            Succeeded: false,
            SelectionExplicit: false,
            Geometry: false,
            DryRun: false,
            InvalidGeometryBlocked: false,
            InvalidFilterBlocked: false,
            InvalidBindingBlocked: false,
            StaleDraftBlocked: false,
            StaleRevisionBlocked: false,
            NonPolygonBlocked: false,
            TypedEvents: false,
            CanonicalCommand: false,
            UndoRedo: false,
            Persistence: false,
            PortalPreserved: false,
            ValidationPreserved: false,
            AdvancedPreserved: false,
            NeighborPreserved: false,
            HumanDiagnostics: false);

    private static string HumanizeTriggerUx1Y(string? code) => code switch
    {
        "command.revision_conflict" => "Le projet a changé. Prévisualisez de nouveau le déclencheur.",
        "world.trigger.geometry_invalid" or "world.trigger.geometry_self_intersection" =>
            "Le volume contient une géométrie invalide ou un contour qui se croise.",
        "world.trigger.policy_invalid" or "world.trigger.filter_contradiction" =>
            "Les filtres ou conditions du déclencheur se contredisent.",
        "world.trigger.actions_invalid" or "world.trigger.action_target_missing" =>
            "Une action du déclencheur n’a plus de cible valide.",
        "world.trigger.event_invalid" or "world.trigger.script_reference_missing" =>
            "Une liaison d’entrée ou de sortie n’est plus valide.",
        "world.trigger.inaccessible" => "Le déclencheur n’est relié à aucun calque accessible.",
        "world.trigger.validation_context_invalid" =>
            "Les références de validation du déclencheur sont incohérentes.",
        _ => "Le déclencheur ne peut pas être prévisualisé. Vérifiez ses champs nommés.",
    };

    private static string HumanizeShapeUx1Y(TriggerWorldProfile.ShapeKind value) => value switch
    {
        TriggerWorldProfile.ShapeKind.ExtrudedPolygon2D => "Polygone extrudé",
        TriggerWorldProfile.ShapeKind.Box3D => "Boîte 3D",
        TriggerWorldProfile.ShapeKind.Sphere3D => "Sphère 3D",
        TriggerWorldProfile.ShapeKind.Capsule3D => "Capsule 3D",
        TriggerWorldProfile.ShapeKind.ConvexHull3D => "Volume convexe 3D",
        _ => "Volume",
    };

    private static string HumanizeFireUx1Y(TriggerWorldProfile.FirePolicy value) => value switch
    {
        TriggerWorldProfile.FirePolicy.Once => "une seule fois",
        TriggerWorldProfile.FirePolicy.OncePerEntity => "une fois par entité",
        TriggerWorldProfile.FirePolicy.Repeat => "répétable",
        TriggerWorldProfile.FirePolicy.WhileInside => "tant que présent",
        _ => "politique définie",
    };

    private static Guid StableTriggerIdUx1Y(BlueprintId blueprintId, AreaId areaId, string segment)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"ux1-y:{blueprintId}:{areaId}:{segment}"));
        var bytes = hash[..16].ToArray();
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    private static string? NullIfWhiteSpaceUx1Y(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsHumanUx1YMessage(string message) =>
        !string.IsNullOrWhiteSpace(message) &&
        !message.Contains("world.", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("module/", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("json", StringComparison.OrdinalIgnoreCase) &&
        !Guid.TryParse(message, out _);

    private void CloseUx1Y() => CancelTriggerFoundationUx1Y();
}
