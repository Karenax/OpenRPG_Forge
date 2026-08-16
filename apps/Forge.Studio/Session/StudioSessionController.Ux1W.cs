using Forge.Application.Commands;
using Forge.Application.Entities;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux1WProvenance = "studio:ux1-w";
    private readonly ScaleEntitiesHandler _scaleEntitiesUx1W = new(new SystemClock());
    private readonly SetRuntimeExtensionsHandler _setRuntimeExtensionsUx1W = new(new SystemClock());
    private StudioZoneScaleDraft? _approvedZoneScaleUx1W;
    private ProjectRevision? _approvedZoneScaleRevisionUx1W;
    private StudioDynamicAreaDraft? _approvedDynamicAreaUx1W;
    private ProjectRevision? _approvedDynamicAreaRevisionUx1W;
    private StudioRuntimeCopyDraft? _approvedRuntimeCopyUx1W;
    private ProjectRevision? _approvedRuntimeCopyRevisionUx1W;

    internal IReadOnlyList<StudioZoneEntityChoice> ZoneEntityChoicesUx1W
    {
        get
        {
            var project = Project;
            var area = ActiveArea;
            if (project is null || area is null)
            {
                return [];
            }

            return area.Entities.Select((entity, index) =>
            {
                var blueprint = project.Blueprints.FirstOrDefault(item => item.Id == entity.BlueprintId);
                var label = blueprint?.DisplayName ?? "Objet sans modèle";
                return new StudioZoneEntityChoice(entity.Id, $"{label} · objet {index + 1}");
            }).ToArray();
        }
    }

    internal StudioZoneScaleDraft? CreateZoneScaleDraftUx1W()
    {
        var area = ActiveArea;
        if (area is null)
        {
            return null;
        }

        var selection = SelectedEntityIds.Where(id => area.Entities.Any(entity => entity.Id == id)).ToArray();
        if (selection.Length == 0 && area.Entities.Count > 0)
        {
            selection = [area.Entities[0].Id];
        }

        return new StudioZoneScaleDraft(area.Id, selection, Vector3Value.One, Multiply: false);
    }

    internal StudioZoneScalePreview PreviewZoneScaleUx1W(StudioZoneScaleDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        _approvedZoneScaleUx1W = null;
        _approvedZoneScaleRevisionUx1W = null;
        var history = History;
        if (history is null || Project is null)
        {
            return FailedScale("Aucun module n’est ouvert.");
        }

        var command = new ScaleEntitiesCommand(
            CommandId.From(Guid.NewGuid()),
            history.State.Revision,
            draft.AreaId,
            draft.EntityIds,
            draft.Scale,
            draft.Multiply,
            DryRun: true);
        var preview = _scaleEntitiesUx1W.Handle(history.State, command);
        if (!preview.Succeeded)
        {
            return FailedScale(HumanizeScaleUx1W(preview.Diagnostics.FirstOrDefault()?.Code));
        }

        var candidate = _scaleEntitiesUx1W.Handle(history.State, command with { DryRun = false });
        var area = candidate.State.Project.Areas.First(item => item.Id == draft.AreaId);
        var selected = area.Entities.Where(entity => draft.EntityIds.Contains(entity.Id)).ToArray();
        var firstScale = selected[0].Transform.Scale;
        var summary = selected.All(entity => entity.Transform.Scale == firstScale)
            ? $"{selected.Length} objet(s) · résultat X {firstScale.X:0.##}, Y {firstScale.Y:0.##}, Z {firstScale.Z:0.##}."
            : $"{selected.Length} objet(s) · proportions relatives conservées sur les trois axes.";
        _approvedZoneScaleUx1W = draft;
        _approvedZoneScaleRevisionUx1W = history.State.Revision;
        return new StudioZoneScalePreview(
            true,
            "Aperçu validé · géométrie contrôlée · aucun changement appliqué.",
            summary,
            true,
            selected.Length);
    }

    internal StudioUx1WActionResult ApplyApprovedZoneScaleUx1W()
    {
        var draft = _approvedZoneScaleUx1W;
        var history = History;
        if (draft is null || history is null || _approvedZoneScaleRevisionUx1W != history.State.Revision)
        {
            CancelZoneDynamicOperationUx1W();
            return new StudioUx1WActionResult(false, "Prévisualisez de nouveau l’échelle avant de l’appliquer.");
        }

        var command = new ScaleEntitiesCommand(
            CommandId.From(Guid.NewGuid()),
            history.State.Revision,
            draft.AreaId,
            draft.EntityIds,
            draft.Scale,
            draft.Multiply,
            DryRun: true);
        var preview = _scaleEntitiesUx1W.Handle(history.State, command);
        if (!preview.Succeeded)
        {
            CancelZoneDynamicOperationUx1W();
            return new StudioUx1WActionResult(false, HumanizeScaleUx1W(preview.Diagnostics.FirstOrDefault()?.Code));
        }

        var applied = _scaleEntitiesUx1W.Handle(history.State, command with { DryRun = false });
        if (!applied.Succeeded)
        {
            CancelZoneDynamicOperationUx1W();
            return new StudioUx1WActionResult(false, HumanizeScaleUx1W(applied.Diagnostics.FirstOrDefault()?.Code));
        }

        history.Record(applied);
        InvalidateContentIndexes();
        CancelZoneDynamicOperationUx1W();
        return new StudioUx1WActionResult(
            true,
            $"Échelle appliquée à {draft.EntityIds.Count} objet(s) · Ctrl+Z pour annuler · Ctrl+S pour enregistrer.");
    }

    internal StudioDynamicAreaDraft? CreateDynamicAreaDraftUx1W()
    {
        var project = Project;
        var area = ActiveArea;
        if (project is null || area is null)
        {
            return null;
        }

        var policy = project.Module.RuntimePolicies.Extensions.PostLaunch.CreatorExperience.DynamicAreas;
        return new StudioDynamicAreaDraft(
            area.Id,
            policy.Enabled,
            policy.MaximumInstances,
            policy.LifetimeSeconds,
            policy.ResolveDependencies,
            policy.Enabled ? Math.Min(2, policy.MaximumInstances) : 0);
    }

    internal StudioDynamicAreaPreview PreviewDynamicAreaUx1W(StudioDynamicAreaDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        _approvedDynamicAreaUx1W = null;
        _approvedDynamicAreaRevisionUx1W = null;
        var history = History;
        var project = Project;
        var activeArea = ActiveArea;
        if (history is null || project is null || activeArea is null)
        {
            return FailedDynamicArea("Aucune zone active n’est disponible.");
        }
        if (draft.PreviewAreaId != activeArea.Id)
        {
            return FailedDynamicArea("La zone active a changé · rouvrez l’outil pour recalculer l’aperçu.");
        }

        try
        {
            var profile = BuildDynamicAreaProfileUx1W(
                project.Module.RuntimePolicies.Extensions,
                draft);
            var creator = profile.PostLaunch.CreatorExperience;
            var decision = PostLaunchCapabilityResolver.ResolveCreatorTechnical(
                creator,
                [],
                requestedResourceResults: 0,
                requestedAreaInstances: draft.RequestedInstances);
            var dynamicRequestAccepted = decision.Accepted ||
                                         (!creator.ResourceCommands.Enabled && draft.RequestedInstances >= 0 &&
                                          draft.RequestedInstances <= creator.DynamicAreas.MaximumInstances &&
                                          (draft.RequestedInstances == 0 || creator.DynamicAreas.Enabled));
            if (!dynamicRequestAccepted)
            {
                return FailedDynamicArea(
                    draft.Enabled
                        ? "La simulation dépasse le nombre de zones dynamiques autorisé."
                        : "Activez les zones dynamiques ou demandez zéro instance pour la simulation.");
            }

            var command = new SetRuntimeExtensionsCommand(
                CommandId.From(Guid.NewGuid()),
                history.State.Revision,
                profile,
                DryRun: true);
            var dryRun = _setRuntimeExtensionsUx1W.Handle(history.State, command);
            if (!dryRun.Succeeded)
            {
                return FailedDynamicArea(HumanizeRuntimeProfileUx1W(dryRun.Diagnostics.FirstOrDefault()?.Code));
            }

            _approvedDynamicAreaUx1W = draft;
            _approvedDynamicAreaRevisionUx1W = history.State.Revision;
            var summary = draft.Enabled
                ? $"La zone active sert de gabarit de simulation · {draft.RequestedInstances} instance(s) demandée(s) sur " +
                  $"{draft.MaximumInstances} · durée {FormatDurationUx1W(draft.LifetimeSeconds)} · " +
                  $"dépendances {(draft.ResolveDependencies ? "résolues" : "laissées au serveur")}."
                : "Création dynamique désactivée · aucune instance ne sera demandée au runtime.";
            return new StudioDynamicAreaPreview(
                true,
                "Aperçu runtime validé · autorité serveur obligatoire · aucun module modifié.",
                summary,
                true,
                decision.DynamicAreaLimit);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return FailedDynamicArea("La politique de zones dynamiques contient une valeur hors limites.");
        }
    }

    internal StudioUx1WActionResult ApplyApprovedDynamicAreaUx1W()
    {
        var draft = _approvedDynamicAreaUx1W;
        var history = History;
        var project = Project;
        if (draft is null || history is null || project is null ||
            _approvedDynamicAreaRevisionUx1W != history.State.Revision)
        {
            CancelZoneDynamicOperationUx1W();
            return new StudioUx1WActionResult(false, "Prévisualisez de nouveau la politique de zones dynamiques.");
        }

        try
        {
            var profile = BuildDynamicAreaProfileUx1W(
                project.Module.RuntimePolicies.Extensions,
                draft);
            return ApplyRuntimeProfileUx1W(
                profile,
                "Politique de zones dynamiques enregistrée · Ctrl+Z pour annuler · Ctrl+S pour écrire le module.");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            CancelZoneDynamicOperationUx1W();
            return new StudioUx1WActionResult(false, "La politique de zones dynamiques n’est plus valide.");
        }
    }

    internal StudioRuntimeCopyDraft? CreateRuntimeCopyDraftUx1W()
    {
        var project = Project;
        var area = ActiveArea;
        if (project is null || area is null || area.Entities.Count == 0)
        {
            return null;
        }

        var sourceId = SelectedEntityId;
        if (sourceId is null || area.Entities.All(item => item.Id != sourceId))
        {
            sourceId = area.Entities[0].Id;
        }
        var selectedSourceId = sourceId ?? throw new InvalidOperationException("The active area has no copy source.");
        var policy = project.Module.RuntimePolicies.Extensions.ModernTooling.RuntimeCopy;
        return new StudioRuntimeCopyDraft(
            area.Id,
            selectedSourceId,
            policy.MaxCopiesPerTransaction,
            Math.Min(2, policy.MaxCopiesPerTransaction),
            policy.RequireServerAuthority,
            HasServerAuthority: true,
            policy.CopyOwnership,
            policy.ReferenceMode,
            policy.StateComponents,
            new Vector3Value(2, 0, 2));
    }

    internal StudioRuntimeCopyPreview PreviewRuntimeCopyUx1W(StudioRuntimeCopyDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        _approvedRuntimeCopyUx1W = null;
        _approvedRuntimeCopyRevisionUx1W = null;
        var history = History;
        var project = Project;
        if (history is null || project is null)
        {
            return FailedRuntimeCopy("Aucun module n’est ouvert.");
        }
        if (draft.RequestedCopies is < 1 || draft.RequestedCopies > draft.MaximumCopiesPerTransaction)
        {
            return FailedRuntimeCopy("Le nombre de copies demandé dépasse la limite de la transaction.");
        }

        var area = project.Areas.FirstOrDefault(item => item.Id == draft.AreaId);
        var source = area?.Entities.FirstOrDefault(item => item.Id == draft.SourceEntityId);
        if (area is null || source is null)
        {
            return FailedRuntimeCopy("L’objet source n’est plus disponible dans la zone active.");
        }

        try
        {
            var profile = BuildRuntimeCopyProfileUx1W(project.Module.RuntimePolicies.Extensions, draft);
            var command = new SetRuntimeExtensionsCommand(
                CommandId.From(Guid.NewGuid()),
                history.State.Revision,
                profile,
                DryRun: true);
            var dryRun = _setRuntimeExtensionsUx1W.Handle(history.State, command);
            if (!dryRun.Succeeded)
            {
                return FailedRuntimeCopy(HumanizeRuntimeProfileUx1W(dryRun.Diagnostics.FirstOrDefault()?.Code));
            }

            var destinations = Enumerable.Range(0, Math.Max(0, draft.RequestedCopies))
                .Select(_ => EntityId.From(Guid.NewGuid()))
                .ToArray();
            var state = new[]
            {
                new RuntimeEntityStateComponent("transform", "position-and-orientation"),
                new RuntimeEntityStateComponent("combat", "health-and-effects"),
                new RuntimeEntityStateComponent("inventory", "inventory-snapshot"),
                new RuntimeEntityStateComponent("blackboard", "script-state"),
            };
            var requests = destinations.Select((destination, index) => new RuntimeEntityCopyRequest(
                new RuntimeEntityCopySource(source, "propriétaire-source", [source.Id], state),
                destination,
                new Vector3Value(
                    draft.PositionOffset.X * (index + 1),
                    draft.PositionOffset.Y * (index + 1),
                    draft.PositionOffset.Z * (index + 1)),
                "éditeur-runtime",
                draft.HasServerAuthority,
                new Dictionary<EntityId, EntityId> { [source.Id] = destination })).ToArray();
            var results = new RuntimeEntityCopyService(profile.ModernTooling.RuntimeCopy)
                .CopyTransaction(requests, area.Entities.Select(item => item.Id).ToHashSet());
            var failed = results.FirstOrDefault(result => !result.Succeeded);
            if (failed is not null)
            {
                return FailedRuntimeCopy(HumanizeRuntimeCopyUx1W(failed.Code));
            }

            var ownershipCopied = results.All(result => result.OwnerId ==
                (draft.CopyOwnership ? "propriétaire-source" : "éditeur-runtime"));
            var referencesRemapped = results.Zip(destinations)
                .All(pair => pair.First.References.SequenceEqual([pair.Second]));
            var allowedState = draft.StateComponents.ToHashSet(StringComparer.Ordinal);
            var stateFiltered = results.All(result =>
                result.State.All(component => allowedState.Contains(component.Name)) &&
                result.State.Count == state.Count(component => allowedState.Contains(component.Name)));
            _approvedRuntimeCopyUx1W = draft;
            _approvedRuntimeCopyRevisionUx1W = history.State.Revision;
            var referenceText = draft.ReferenceMode switch
            {
                RuntimeReferenceCopyMode.DropExternal => "liens externes retirés",
                RuntimeReferenceCopyMode.PreserveExternal => "liens externes conservés",
                _ => "liens internes redirigés",
            };
            return new StudioRuntimeCopyPreview(
                true,
                "Simulation validée · nouvelles identités réservées · aucun objet créé dans l’éditeur.",
                $"{results.Count} copie(s) simulée(s) · {referenceText} · " +
                $"propriété {(draft.CopyOwnership ? "copiée" : "attribuée au demandeur")} · " +
                $"{results.FirstOrDefault()?.State.Count ?? 0} composant(s) d’état conservé(s).",
                true,
                destinations,
                ownershipCopied,
                referencesRemapped,
                stateFiltered);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            return FailedRuntimeCopy("La politique de copie runtime contient une valeur hors limites.");
        }
    }

    internal StudioUx1WActionResult ApplyApprovedRuntimeCopyUx1W()
    {
        var draft = _approvedRuntimeCopyUx1W;
        var history = History;
        var project = Project;
        if (draft is null || history is null || project is null ||
            _approvedRuntimeCopyRevisionUx1W != history.State.Revision)
        {
            CancelZoneDynamicOperationUx1W();
            return new StudioUx1WActionResult(false, "Prévisualisez de nouveau la politique de copie runtime.");
        }

        try
        {
            var profile = BuildRuntimeCopyProfileUx1W(
                project.Module.RuntimePolicies.Extensions,
                draft);
            return ApplyRuntimeProfileUx1W(
                profile,
                "Politique de copie runtime enregistrée · la simulation n’a créé aucun objet · Ctrl+Z pour annuler.");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            CancelZoneDynamicOperationUx1W();
            return new StudioUx1WActionResult(false, "La politique de copie runtime n’est plus valide.");
        }
    }

    internal void CancelZoneDynamicOperationUx1W()
    {
        _approvedZoneScaleUx1W = null;
        _approvedZoneScaleRevisionUx1W = null;
        _approvedDynamicAreaUx1W = null;
        _approvedDynamicAreaRevisionUx1W = null;
        _approvedRuntimeCopyUx1W = null;
        _approvedRuntimeCopyRevisionUx1W = null;
    }

    internal StudioUx1WSmokeResult RunUx1WContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux1w-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var blueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1c20000-0000-4000-8000-000000000001")),
                "statue-dynamique",
                "Statue dynamique",
                BlueprintKind.Placeable);
            var first = new EntityInstance(
                EntityId.From(Guid.Parse("c1c20000-0000-4000-8000-000000000002")),
                blueprint.Id,
                new TransformValue(new Vector3Value(2, 0, 3), QuaternionValue.Identity, Vector3Value.One),
                ["décor"]);
            var second = new EntityInstance(
                EntityId.From(Guid.Parse("c1c20000-0000-4000-8000-000000000003")),
                blueprint.Id,
                new TransformValue(new Vector3Value(6, 0, 5), QuaternionValue.Identity, Vector3Value.One),
                ["décor"]);
            var area = new AreaDefinition(
                AreaId.From(Guid.Parse("c1c20000-0000-4000-8000-000000000004")),
                "atelier-dynamique",
                "Atelier dynamique",
                [first, second],
                AreaKind.Interior,
                new AreaDimensions(48, 48));
            var project = new ProjectDefinition(
                ProjectId.From(Guid.Parse("c1c20000-0000-4000-8000-000000000005")),
                "Studio UX1-W",
                [blueprint],
                [area]);
            var created = ProjectSession.Create(tempRoot, project);
            var opened = created.Succeeded && editing.Open(tempRoot).Succeeded;
            if (!opened)
            {
                return FailedUx1WSmoke();
            }

            var originalProject = editing.Project!;
            var originalExtensions = originalProject.Module.RuntimePolicies.Extensions;
            editing.SelectEntities([first.Id, second.Id], second.Id);
            var scaleDraft = editing.CreateZoneScaleDraftUx1W()! with
            {
                Scale = new Vector3Value(1.5f, .75f, 2f),
                Multiply = false,
            };
            var scalePreview = editing.PreviewZoneScaleUx1W(scaleDraft);
            var scaleDryRun = scalePreview.Succeeded && scalePreview.CanApply &&
                              ReferenceEquals(originalProject, editing.Project);
            var scaleApplied = editing.ApplyApprovedZoneScaleUx1W();
            var scaledProject = editing.Project!;
            var scaledArea = scaledProject.Areas.Single(item => item.Id == area.Id);
            var scaleMultiEdit = scaleApplied.Succeeded && scaledArea.Entities.All(entity =>
                entity.Transform.Scale == scaleDraft.Scale);
            var invalidScale = editing.PreviewZoneScaleUx1W(scaleDraft with
            { Scale = new Vector3Value(.01f, 1, 1) });
            var scaleConstraints = !invalidScale.Succeeded;
            var scaleUndo = editing.Undo();
            var scaleUndone = ReferenceEquals(originalProject, editing.Project);
            var scaleRedo = editing.Redo();
            var scaleRedone = ReferenceEquals(scaledProject, editing.Project);

            var dynamicDraft = editing.CreateDynamicAreaDraftUx1W()! with
            {
                Enabled = true,
                MaximumInstances = 8,
                LifetimeSeconds = 120,
                ResolveDependencies = true,
                RequestedInstances = 2,
            };
            var dynamicPreview = editing.PreviewDynamicAreaUx1W(dynamicDraft);
            var dynamicDryRun = dynamicPreview.Succeeded && dynamicPreview.CanApply &&
                                ReferenceEquals(scaledProject, editing.Project);
            var dynamicApplied = editing.ApplyApprovedDynamicAreaUx1W();
            var dynamicProject = editing.Project!;
            var dynamicPolicy = dynamicProject.Module.RuntimePolicies.Extensions.PostLaunch.CreatorExperience.DynamicAreas;
            var dynamicAreaPreview = dynamicApplied.Succeeded && dynamicDryRun && dynamicPolicy is
            {
                Enabled: true,
                MaximumInstances: 8,
                LifetimeSeconds: 120,
                RequireServerAuthority: true,
                ResolveDependencies: true,
            };
            var invalidDynamic = editing.PreviewDynamicAreaUx1W(dynamicDraft with { RequestedInstances = 9 });
            var dynamicQuota = !invalidDynamic.Succeeded;
            var dynamicUndo = editing.Undo();
            var dynamicUndone = ReferenceEquals(scaledProject, editing.Project);
            var dynamicRedo = editing.Redo();
            var dynamicRedone = ReferenceEquals(dynamicProject, editing.Project);

            var copyDraft = editing.CreateRuntimeCopyDraftUx1W()! with
            {
                SourceEntityId = first.Id,
                MaximumCopiesPerTransaction = 4,
                RequestedCopies = 2,
                RequireServerAuthority = true,
                HasServerAuthority = true,
                CopyOwnership = true,
                ReferenceMode = RuntimeReferenceCopyMode.RemapInternal,
                StateComponents = ["transform", "inventory"],
                PositionOffset = new Vector3Value(3, 0, 2),
            };
            var copyPreview = editing.PreviewRuntimeCopyUx1W(copyDraft);
            var runtimeCopyPreview = copyPreview.Succeeded && copyPreview.CanApply;
            var destinationIds = copyPreview.DestinationIds;
            var freshIdentities = destinationIds is { Count: 2 } &&
                                  destinationIds.Distinct().Count() == 2 &&
                                  destinationIds.All(id => id != first.Id && id != second.Id);
            var ownershipReferencesState = copyPreview.OwnershipCopied && copyPreview.ReferencesRemapped &&
                                           copyPreview.StateFiltered;
            var copyApplied = editing.ApplyApprovedRuntimeCopyUx1W();
            var copiedProject = editing.Project!;
            var copyPolicy = copiedProject.Module.RuntimePolicies.Extensions.ModernTooling.RuntimeCopy;
            runtimeCopyPreview = runtimeCopyPreview && copyApplied.Succeeded &&
                                 copyPolicy.MaxCopiesPerTransaction == 4 &&
                                 copyPolicy.RequireServerAuthority && copyPolicy.CopyOwnership &&
                                 copyPolicy.ReferenceMode == RuntimeReferenceCopyMode.RemapInternal &&
                                 copyPolicy.StateComponents.SequenceEqual(["inventory", "transform"]);
            var unauthorized = editing.PreviewRuntimeCopyUx1W(copyDraft with { HasServerAuthority = false });
            var invalidBlocked = scaleConstraints && dynamicQuota && !unauthorized.Succeeded;
            var copyUndo = editing.Undo();
            var copyUndone = ReferenceEquals(dynamicProject, editing.Project);
            var copyRedo = editing.Redo();
            var copyRedone = ReferenceEquals(copiedProject, editing.Project);
            var undoRedo = scaleUndo.Succeeded && scaleUndone && scaleRedo.Succeeded && scaleRedone &&
                           dynamicUndo.Succeeded && dynamicUndone && dynamicRedo.Succeeded && dynamicRedone &&
                           copyUndo.Succeeded && copyUndone && copyRedo.Succeeded && copyRedone;

            var finalExtensions = copiedProject.Module.RuntimePolicies.Extensions;
            var neighborsPreserved = finalExtensions.SnapshotPolicy == originalExtensions.SnapshotPolicy &&
                                     finalExtensions.OperationalReadiness == originalExtensions.OperationalReadiness &&
                                     finalExtensions.RuleSchemas.SequenceEqual(originalExtensions.RuleSchemas) &&
                                     finalExtensions.UiDocuments.SequenceEqual(originalExtensions.UiDocuments) &&
                                     finalExtensions.JsonDocuments.SequenceEqual(originalExtensions.JsonDocuments) &&
                                     finalExtensions.PostLaunch.Gameplay == originalExtensions.PostLaunch.Gameplay &&
                                     finalExtensions.PostLaunch.LiveDirector == originalExtensions.PostLaunch.LiveDirector &&
                                     finalExtensions.PostLaunch.Network == originalExtensions.PostLaunch.Network &&
                                     finalExtensions.PostLaunch.Production == originalExtensions.PostLaunch.Production &&
                                     finalExtensions.ModernTooling.AreaEffectIndicator ==
                                     originalExtensions.ModernTooling.AreaEffectIndicator &&
                                     finalExtensions.ModernTooling.UiToolkit == originalExtensions.ModernTooling.UiToolkit &&
                                     finalExtensions.ModernTooling.Compiler == originalExtensions.ModernTooling.Compiler &&
                                     finalExtensions.ModernTooling.Documentation == originalExtensions.ModernTooling.Documentation &&
                                     finalExtensions.ModernTooling.QualityAssurance ==
                                     originalExtensions.ModernTooling.QualityAssurance;
            var canonicalCommands = copyApplied.Succeeded &&
                                    editing.History!.Journal.Select(item => item.CommandName)
                                        .SequenceEqual(["scale_entities", "set_runtime_extensions", "set_runtime_extensions"]);
            var save = editing.Save();
            editing.Close();
            var reopen = reopened.Open(tempRoot);
            var restoredArea = reopened.Project?.Areas.SingleOrDefault(item => item.Id == area.Id);
            var restoredExtensions = reopened.Project?.Module.RuntimePolicies.Extensions;
            var persistence = save.Succeeded && reopen.Succeeded && restoredArea is not null &&
                              restoredExtensions is not null &&
                              restoredArea.Entities.All(entity => entity.Transform.Scale == scaleDraft.Scale) &&
                              restoredExtensions.PostLaunch.CreatorExperience.DynamicAreas is
                              {
                                  Enabled: true,
                                  MaximumInstances: 8,
                                  LifetimeSeconds: 120,
                              } &&
                              restoredExtensions.ModernTooling.RuntimeCopy.MaxCopiesPerTransaction == 4 &&
                              restoredExtensions.ModernTooling.RuntimeCopy.CopyOwnership &&
                              restoredExtensions.ModernTooling.RuntimeCopy.ReferenceMode ==
                              RuntimeReferenceCopyMode.RemapInternal &&
                              restoredExtensions.ModernTooling.RuntimeCopy.StateComponents
                                  .SequenceEqual(["inventory", "transform"]);
            var humanDiagnostics = IsHumanUx1WMessage(invalidScale.Message) &&
                                   IsHumanUx1WMessage(invalidDynamic.Message) &&
                                   IsHumanUx1WMessage(unauthorized.Message);
            var succeeded = scaleMultiEdit && scaleConstraints && scaleDryRun && dynamicAreaPreview &&
                            dynamicQuota && runtimeCopyPreview && freshIdentities && ownershipReferencesState &&
                            canonicalCommands && undoRedo && persistence && neighborsPreserved && invalidBlocked &&
                            humanDiagnostics;
            return new StudioUx1WSmokeResult(
                succeeded,
                scaleMultiEdit,
                scaleConstraints,
                scaleDryRun,
                dynamicAreaPreview,
                dynamicQuota,
                runtimeCopyPreview,
                freshIdentities,
                ownershipReferencesState,
                canonicalCommands,
                undoRedo,
                persistence,
                neighborsPreserved,
                invalidBlocked,
                humanDiagnostics);
        }
        catch (Exception)
        {
            return FailedUx1WSmoke();
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

    private StudioUx1WActionResult ApplyRuntimeProfileUx1W(
        ModuleRuntimeExtensionProfile profile,
        string successMessage)
    {
        var history = History;
        if (history is null)
        {
            CancelZoneDynamicOperationUx1W();
            return new StudioUx1WActionResult(false, "Aucun module n’est ouvert.");
        }

        var command = new SetRuntimeExtensionsCommand(
            CommandId.From(Guid.NewGuid()),
            history.State.Revision,
            profile,
            DryRun: true);
        var preview = _setRuntimeExtensionsUx1W.Handle(history.State, command);
        if (!preview.Succeeded)
        {
            CancelZoneDynamicOperationUx1W();
            return new StudioUx1WActionResult(false, HumanizeRuntimeProfileUx1W(preview.Diagnostics.FirstOrDefault()?.Code));
        }

        var applied = _setRuntimeExtensionsUx1W.Handle(history.State, command with { DryRun = false });
        if (!applied.Succeeded)
        {
            CancelZoneDynamicOperationUx1W();
            return new StudioUx1WActionResult(false, HumanizeRuntimeProfileUx1W(applied.Diagnostics.FirstOrDefault()?.Code));
        }

        history.Record(applied);
        InvalidateContentIndexes();
        CancelZoneDynamicOperationUx1W();
        return new StudioUx1WActionResult(true, successMessage);
    }

    private static ModuleRuntimeExtensionProfile BuildDynamicAreaProfileUx1W(
        ModuleRuntimeExtensionProfile source,
        StudioDynamicAreaDraft draft)
    {
        var post = source.PostLaunch;
        var creator = post.CreatorExperience;
        var dynamicAreas = new CreatorExperienceProductionProfile.DynamicAreaSettings(
            creator.DynamicAreas.Id,
            draft.Enabled,
            draft.MaximumInstances,
            draft.LifetimeSeconds,
            RequireServerAuthority: true,
            ResolveDependencies: draft.ResolveDependencies);
        var updatedCreator = new CreatorExperienceProductionProfile(
            creator.Id,
            creator.SchemaVersion,
            creator.Water,
            creator.DeviceQueries,
            creator.ResourceCommands,
            dynamicAreas,
            creator.PartyControl,
            creator.ProgressionTracks,
            creator.RepositoryMetadata,
            creator.Observability,
            creator.Graphics,
            Ux1WProvenance + ":dynamic-area");
        var updatedPost = new PostLaunchCapabilityProfile(
            post.Id,
            post.SchemaVersion,
            post.Gameplay,
            post.LiveDirector,
            post.Network,
            updatedCreator,
            post.Production,
            Ux1WProvenance + ":dynamic-area");
        return new ModuleRuntimeExtensionProfile(
            source.Id,
            source.SchemaVersion,
            source.RuleSchemas,
            source.UiDocuments,
            source.JsonDocuments,
            source.SnapshotPolicy,
            Ux1WProvenance,
            source.ModernTooling,
            source.OperationalReadiness,
            updatedPost);
    }

    private static ModuleRuntimeExtensionProfile BuildRuntimeCopyProfileUx1W(
        ModuleRuntimeExtensionProfile source,
        StudioRuntimeCopyDraft draft)
    {
        var modern = source.ModernTooling;
        var copy = new RuntimeObjectCopyPolicy(
            modern.RuntimeCopy.Id,
            modern.RuntimeCopy.SchemaVersion,
            draft.MaximumCopiesPerTransaction,
            draft.RequireServerAuthority,
            draft.CopyOwnership,
            draft.ReferenceMode,
            draft.StateComponents,
            Ux1WProvenance + ":runtime-copy");
        var updatedModern = new ModernRuntimeToolingProfile(
            modern.Id,
            modern.SchemaVersion,
            copy,
            modern.AreaEffectIndicator,
            modern.UiToolkit,
            modern.Compiler,
            modern.Documentation,
            Ux1WProvenance + ":runtime-copy",
            modern.QualityAssurance);
        return new ModuleRuntimeExtensionProfile(
            source.Id,
            source.SchemaVersion,
            source.RuleSchemas,
            source.UiDocuments,
            source.JsonDocuments,
            source.SnapshotPolicy,
            Ux1WProvenance,
            updatedModern,
            source.OperationalReadiness,
            source.PostLaunch);
    }

    private static StudioZoneScalePreview FailedScale(string message) =>
        new(false, message, string.Empty, false);

    private static StudioDynamicAreaPreview FailedDynamicArea(string message) =>
        new(false, message, string.Empty, false);

    private static StudioRuntimeCopyPreview FailedRuntimeCopy(string message) =>
        new(false, message, string.Empty, false);

    private static StudioUx1WSmokeResult FailedUx1WSmoke() => new(
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false);

    private static string HumanizeScaleUx1W(string? code) => code switch
    {
        ScaleEntitiesHandler.EmptySelectionCode => "Sélectionnez au moins un objet dans la zone.",
        ScaleEntitiesHandler.DuplicateSelectionCode => "La sélection contient deux fois le même objet.",
        ScaleEntitiesHandler.MissingAreaCode => "La zone active n’est plus disponible.",
        ScaleEntitiesHandler.MissingEntityCode => "Un objet sélectionné n’existe plus dans la zone.",
        ScaleEntitiesHandler.InvalidScaleCode =>
            $"Chaque axe doit rester entre {ScaleEntitiesHandler.MinimumScale:0.##} et {ScaleEntitiesHandler.MaximumScale:0.##}.",
        ScaleEntitiesHandler.GeometryCode => "Cette échelle rendrait la géométrie de la zone invalide.",
        ScaleEntitiesHandler.RevisionConflictCode => "Le module a changé · relancez l’aperçu.",
        _ => "L’échelle demandée ne peut pas être appliquée.",
    };

    private static string HumanizeRuntimeCopyUx1W(string code) => code switch
    {
        RuntimeEntityCopyService.AuthorityRequiredCode =>
            "La simulation est refusée sans autorité serveur.",
        RuntimeEntityCopyService.QuotaExceededCode =>
            "Le nombre de copies demandé dépasse la limite de la transaction.",
        RuntimeEntityCopyService.DuplicateDestinationCode =>
            "Une identité de destination est déjà utilisée.",
        _ => "La copie runtime ne peut pas être simulée.",
    };

    private static string HumanizeRuntimeProfileUx1W(string? code) => code switch
    {
        "command.revision_conflict" => "Le module a changé · relancez l’aperçu.",
        _ => "La politique runtime ne respecte pas les contraintes du module.",
    };

    private static string FormatDurationUx1W(int seconds) => seconds >= 3_600 && seconds % 3_600 == 0
        ? $"{seconds / 3_600} h"
        : seconds >= 60 && seconds % 60 == 0
            ? $"{seconds / 60} min"
            : $"{seconds} s";

    private static bool IsHumanUx1WMessage(string message) =>
        !string.IsNullOrWhiteSpace(message) &&
        !message.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("runtime.copy.", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("scale.", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("POST_", StringComparison.OrdinalIgnoreCase) &&
        !Guid.TryParse(message, out _);

    private void CloseUx1W() => CancelZoneDynamicOperationUx1W();
}
