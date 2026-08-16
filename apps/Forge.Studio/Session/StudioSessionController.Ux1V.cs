using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Placeables;
using Forge.RpgKit.Domain.Placeables;
using DomainVector3 = Forge.Domain.Spatial.Vector3Value;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    internal StudioPlaceableOnUsedEventPreview PreviewPlaceableOnUsedEvent(StudioPlaceableDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!draft.OnUsedEnabled)
            return FailedOnUsedPreview("OnUsed est désactivé · activez-le avant l’aperçu.",
                "WORLD_PLACEABLE_ON_USED_DISABLED");
        if (!TryBuildPlaceable(draft, dryRun: true, out _, out var profile, out var message) || profile is null)
            return FailedOnUsedPreview(message, LastPlaceableDiagnosticCode);
        var foundation = profile.Foundation;
        var verb = foundation?.Interaction?.Verbs.FirstOrDefault();
        if (foundation?.OnUsed is null || verb is null)
            return FailedOnUsedPreview(
                "OnUsed exige un objet utilisable avec au moins une action.",
                "WORLD_PLACEABLE_ON_USED_UNAVAILABLE");

        try
        {
            var dispatched = WorldFeatureRuntimeResolver.ExecutePlaceableUse(
                profile,
                Guid.Parse("c1d00000-0000-4000-8000-000000000090"),
                verb.Id,
                "joueuse-locale",
                new(1.5f, 0, 2.25f),
                "success",
                DateTimeOffset.UnixEpoch);
            return new(
                true,
                $"Aperçu OnUsed · {dispatched.EventBinding} · actrice locale · cible 1,5 / 0 / 2,25 · " +
                "résultat réussi · scripts avant/après gouvernés · aucun objet utilisé.",
                "RPG_PLACEABLE_INTERACTION_EXECUTED",
                dispatched.EventBinding,
                dispatched.ForgeScriptApiVersion,
                dispatched.ActorId,
                dispatched.Result,
                dispatched.BeforeScriptId,
                dispatched.AfterScriptId);
        }
        catch (InvalidOperationException exception)
        {
            return FailedOnUsedPreview(HumanizePlaceable(exception.Message), exception.Message);
        }
    }

    internal StudioPlaceableContentsChangedPreview PreviewPlaceableContentsChanged(StudioPlaceableDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!draft.HasContentsChanged)
            return FailedContentsChangedPreview(
                "Le contenu modifié est désactivé · activez-le avant l’aperçu.",
                "WORLD_PLACEABLE_CONTENTS_CHANGED_DISABLED");
        if (!TryBuildPlaceable(draft, dryRun: true, out _, out var profile, out var message) || profile is null)
            return FailedContentsChangedPreview(message, LastPlaceableDiagnosticCode);

        var adapter = new PlaceableSecurityEventsRuntimeAdapter();
        var resolution = adapter.ResolveContentsChanged(profile);
        if (!resolution.Available || resolution.Handle is null)
            return FailedContentsChangedPreview(HumanizePlaceable(resolution.DiagnosticCode),
                resolution.DiagnosticCode ?? "WORLD_PLACEABLE_CONTENTS_CHANGED_UNAVAILABLE");
        var dispatched = adapter.ContentsChanged(
            resolution.Handle,
            profile,
            7,
            [new(Guid.Parse("c1d00000-0000-4000-8000-000000000091"), -1, "take",
                "inventory:placeable", "inventory:joueuse")],
            "player-loot",
            "joueuse-locale");
        return new(
            true,
            $"Aperçu OnDisturbed · {dispatched.Binding} · delta -1 · cause player-loot · " +
            $"révision {dispatched.Payload.PreviousRevision} → {dispatched.Payload.CurrentRevision} · " +
            "actrice locale · aucun inventaire modifié.",
            "RPG_PLACEABLE_CONTENTS_CHANGED",
            dispatched.Binding,
            nameof(ContainerContentsChanged),
            dispatched.Payload.PreviousRevision,
            dispatched.Payload.CurrentRevision,
            dispatched.Payload.Delta.Single().QuantityDelta,
            dispatched.Payload.Cause,
            dispatched.Payload.ActorId);
    }

    internal StudioPlaceableDamageEventPreview PreviewPlaceableDamageEvent(
        StudioPlaceableDraft draft,
        StudioPlaceableDamageEventPreviewMode mode)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!draft.HasDamageEvents)
            return FailedDamageEventPreview(
                "Les événements de dégâts et de mort sont désactivés · activez-les avant l’aperçu.",
                "WORLD_PLACEABLE_DAMAGE_EVENTS_DISABLED");
        if (!TryBuildPlaceable(draft, dryRun: true, out _, out var profile, out var message) || profile is null)
            return FailedDamageEventPreview(message, LastPlaceableDiagnosticCode);

        var adapter = new PlaceableCompositionEventsRuntimeAdapter();
        var resolution = adapter.ResolveDamageEvents(profile);
        if (!resolution.Available || resolution.Handle is null)
            return FailedDamageEventPreview(HumanizePlaceable(resolution.DiagnosticCode),
                resolution.DiagnosticCode ?? "WORLD_PLACEABLE_DAMAGE_EVENTS_UNAVAILABLE");
        var destroyed = mode == StudioPlaceableDamageEventPreviewMode.Destroyed;
        var dispatched = adapter.Damage(
            resolution.Handle,
            profile,
            "joueuse-locale",
            destroyed ? 30 : 20,
            "force",
            destroyed ? 30 : 50,
            destroyed ? 0 : 30,
            new Dictionary<string, string> { ["weapon"] = "marteau" });
        return new(
            true,
            $"Aperçu {(destroyed ? "OnDeath" : "OnDamaged")} · {dispatched.Binding} · source actrice locale · " +
            $"{dispatched.Payload.Amount:0.##} force · santé {dispatched.Payload.PreviousHealth:0.##} → " +
            $"{dispatched.Payload.CurrentHealth:0.##} · contexte marteau · aucune santé modifiée.",
            $"RPG_PLACEABLE_{dispatched.Payload.Name.ToUpperInvariant()}",
            dispatched.Binding,
            dispatched.Payload.Name,
            dispatched.Payload.SourceId,
            dispatched.Payload.Amount,
            dispatched.Payload.DamageType,
            dispatched.Payload.PreviousHealth,
            dispatched.Payload.CurrentHealth);
    }

    internal StudioUx1VSmokeResult RunUx1VContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux1v-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var blueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1d00000-0000-4000-8000-000000000001")),
                "reliquaire-des-veilleurs",
                "Reliquaire des veilleurs",
                BlueprintKind.Placeable);
            var neighborBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1d00000-0000-4000-8000-000000000002")),
                "borne-voisine",
                "Borne voisine",
                BlueprintKind.Placeable);
            var first = new EntityInstance(
                EntityId.From(Guid.Parse("c1d00000-0000-4000-8000-000000000003")),
                blueprint.Id,
                new TransformValue(DomainVector3.Zero, QuaternionValue.Identity, DomainVector3.One));
            var second = new EntityInstance(
                EntityId.From(Guid.Parse("c1d00000-0000-4000-8000-000000000004")),
                blueprint.Id,
                new TransformValue(new(4, 0, 2), QuaternionValue.Identity, DomainVector3.One));
            var area = new AreaDefinition(
                AreaId.From(Guid.Parse("c1d00000-0000-4000-8000-000000000005")),
                "atelier-evenements-placeable",
                "Atelier des événements de placeable",
                [first, second],
                AreaKind.Interior,
                new AreaDimensions(32, 32));
            var project = new ProjectDefinition(
                ProjectId.From(Guid.Parse("c1d00000-0000-4000-8000-000000000006")),
                "Studio UX1-V",
                [blueprint, neighborBlueprint],
                [area]);
            var originalProfile = CreatePlaceableTransformParent(
                blueprint,
                Guid.Parse("c1d00000-0000-4000-8000-000000000010"),
                "test:ux1-v-profile");
            var neighbor = CreatePlaceableTransformParent(
                neighborBlueprint,
                Guid.Parse("c1d00000-0000-4000-8000-000000000011"),
                "test:ux1-v-neighbor");
            var catalog = new WorldFeatureCatalog(
                project.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-v-catalog",
                placeables: [originalProfile, neighbor]);
            project = new ProjectDefinition(project.Id, project.Title,
                project.Module.WithWorldFeatures(catalog), project.Blueprints, project.Areas);
            var created = ProjectSession.Create(tempRoot, project);
            if (!created.Succeeded)
                return FailedUx1VSmoke("PROJECT_CREATE", created.Diagnostics.Select(item => item.Code));
            var opened = editing.Open(tempRoot);
            if (!opened.Succeeded)
                return FailedUx1VSmoke("PROJECT_OPEN", opened.Diagnostics.Select(item => item.Code));

            var originalProject = editing.Project!;
            var loadedOriginal = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var loadedNeighbor = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == neighborBlueprint.Id);
            var source = editing.CreatePlaceableDraft(blueprint.Id)!;
            var contents = source.ContentsChanged! with
            {
                ForgeScriptApiVersion = "1.1.0",
                EventBinding = "ContenuObjetModifie",
            };
            var damage = source.DamageEvents! with
            {
                ForgeScriptApiVersion = "1.1.0",
                DamagedBinding = "ObjetEndommage",
                DestroyedBinding = "ObjetDetruit",
            };
            var draft = source with
            {
                Role = StudioPlaceableRole.Interactable,
                OnUsedEnabled = true,
                OnUsedForgeScriptApiVersion = "1.1.0",
                OnUsedEventBinding = "ObjetUtilise",
                BeforeUseScript = "scripts/placeable_before_use",
                AfterUseScript = "scripts/placeable_after_use",
                RequireOnUsedRuntime = true,
                HasHealth = true,
                HasContentsChanged = true,
                ContentsChanged = contents,
                RequireContentsChangedRuntime = true,
                HasDamageEvents = true,
                DamageEvents = damage,
                RequireDamageEventsRuntime = true,
            };
            var invalidResults = new[]
            {
                editing.PreviewPlaceable(draft with { OnUsedForgeScriptApiVersion = string.Empty }),
                editing.PreviewPlaceable(draft with { OnUsedEventBinding = string.Empty }),
                editing.PreviewPlaceable(draft with
                    { ContentsChanged = contents with { IncludeTransactionalDelta = false } }),
                editing.PreviewPlaceable(draft with
                    { ContentsChanged = contents with { EventBinding = string.Empty } }),
                editing.PreviewPlaceable(draft with
                    { DamageEvents = damage with { DestroyedBinding = string.Empty } }),
                editing.PreviewPlaceable(draft with
                    { DamageEvents = damage with { IncludeContext = false } }),
                editing.PreviewPlaceable(draft with { HasHealth = false }),
                editing.PreviewPlaceable(draft with
                    { HasContentsChanged = false, RequireContentsChangedRuntime = true }),
                editing.PreviewPlaceable(draft with
                    { HasDamageEvents = false, RequireDamageEventsRuntime = true }),
            };
            var invalidBlocked = invalidResults.All(result => !result.Succeeded);
            var humanDiagnostics = invalidBlocked && invalidResults.All(result =>
                IsHumanPlaceableMessage(result.Message));

            var directBuilt = editing.TryBuildPlaceable(draft, dryRun: true, out _, out var directProfile, out _);
            var preview = editing.PreviewPlaceable(draft);
            var usedPreview = editing.PreviewPlaceableOnUsedEvent(draft);
            var disturbedPreview = editing.PreviewPlaceableContentsChanged(draft);
            var damagedPreview = editing.PreviewPlaceableDamageEvent(
                draft, StudioPlaceableDamageEventPreviewMode.Damaged);
            var destroyedPreview = editing.PreviewPlaceableDamageEvent(
                draft, StudioPlaceableDamageEventPreviewMode.Destroyed);
            var dryRun = preview.Succeeded && preview.CanApply &&
                         preview.Summary.Contains("Contenu modifié", StringComparison.Ordinal) &&
                         preview.Summary.Contains("Dégâts et mort", StringComparison.Ordinal) &&
                         ReferenceEquals(originalProject, editing.Project);
            var instanceSelection = preview.InstanceIds.SequenceEqual([first.Id, second.Id]);
            var applied = editing.ApplyApprovedPlaceable();
            var appliedProject = editing.Project!;
            var profile = appliedProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var onUsed = profile.Foundation?.OnUsed;
            var disturbed = profile.SecurityEvents?.ContentsChanged;
            var damageEvents = profile.DamageEvents ?? profile.CompositionEvents?.DamageEvents;
            var onUsedBindingAndApi = onUsed is
            {
                ForgeScriptApiVersion: "1.1.0",
                EventBinding: "ObjetUtilise",
            };
            var onUsedPayloadAndScripts = usedPreview.Succeeded && usedPreview.Binding == "ObjetUtilise" &&
                                          usedPreview.ApiVersion == "1.1.0" &&
                                          usedPreview.ActorId == "joueuse-locale" &&
                                          usedPreview.Result == "success" &&
                                          usedPreview.BeforeScriptId == "scripts/placeable_before_use" &&
                                          usedPreview.AfterScriptId == "scripts/placeable_after_use";
            var contentsChangedBindingAndPayload = disturbed is
                {
                    ForgeScriptApiVersion: "1.1.0",
                    EventBinding: "ContenuObjetModifie",
                    IncludeTransactionalDelta: true,
                    IncludeCause: true,
                    IncludeRevision: true,
                    IncludeActor: true,
                } && disturbedPreview.Succeeded && disturbedPreview.Binding == "ContenuObjetModifie" &&
                    disturbedPreview.PreviousRevision == 7 && disturbedPreview.CurrentRevision == 8 &&
                    disturbedPreview.QuantityDelta == -1 && disturbedPreview.Cause == "player-loot" &&
                    disturbedPreview.ActorId == "joueuse-locale";
            var damageAndDeathBindings = damageEvents is
                {
                    ForgeScriptApiVersion: "1.1.0",
                    DamagedBinding: "ObjetEndommage",
                    DestroyedBinding: "ObjetDetruit",
                } && damagedPreview.Binding == "ObjetEndommage" &&
                    destroyedPreview.Binding == "ObjetDetruit" && damagedPreview.EventName == "DamageApplied" &&
                    destroyedPreview.EventName == "EntityDestroyed";
            var damageAndDeathPayload = damageEvents is
                {
                    IncludeSource: true,
                    IncludeAmount: true,
                    IncludeDamageType: true,
                    IncludePreviousHealth: true,
                    IncludeCurrentHealth: true,
                    IncludeContext: true,
                } && damagedPreview.Succeeded && destroyedPreview.Succeeded &&
                    damagedPreview.SourceId == "joueuse-locale" && damagedPreview.Amount == 20 &&
                    damagedPreview.DamageType == "force" && damagedPreview.PreviousHealth == 50 &&
                    damagedPreview.CurrentHealth == 30 && destroyedPreview.CurrentHealth == 0;
            var commandParity = directBuilt && directProfile is not null &&
                                directProfile.Foundation?.OnUsed == onUsed &&
                                ContentsChangedValuesEqual(
                                    directProfile.SecurityEvents?.ContentsChanged, disturbed) &&
                                DamageEventValuesEqual(
                                    directProfile.DamageEvents ?? directProfile.CompositionEvents?.DamageEvents,
                                    damageEvents) && directProfile.ContentsChangedRequiredAtRuntime &&
                                directProfile.DamageEventsRequiredAtRuntime;
            var preservation = profile.Id == loadedOriginal.Id &&
                               profile.Provenance == loadedOriginal.Provenance &&
                               profile.Foundation?.Id == loadedOriginal.Foundation?.Id &&
                               ReferenceEquals(profile.Reactive, loadedOriginal.Reactive) &&
                               ReferenceEquals(
                                   appliedProject.Module.WorldFeatures.Placeables.Single(item =>
                                       item.BlueprintId == neighborBlueprint.Id), loadedNeighbor) &&
                               appliedProject.Blueprints.SequenceEqual(originalProject.Blueprints);

            var securityAdapter = new PlaceableSecurityEventsRuntimeAdapter();
            var disturbedResolution = securityAdapter.ResolveContentsChanged(profile);
            var disturbedRuntime = securityAdapter.ContentsChanged(
                disturbedResolution.Handle!, profile, 2,
                [new(Guid.Parse("c1d00000-0000-4000-8000-000000000092"), 1, "deposit",
                    "inventory:joueuse", "inventory:placeable")], "player-deposit", "joueuse-locale");
            var compositionAdapter = new PlaceableCompositionEventsRuntimeAdapter();
            var damageResolution = compositionAdapter.ResolveDamageEvents(profile);
            var damagedRuntime = compositionAdapter.Damage(
                damageResolution.Handle!, profile, "joueuse-locale", 10, "fire", 40, 30);
            var destroyedRuntime = compositionAdapter.Damage(
                damageResolution.Handle!, profile, "joueuse-locale", 30, "fire", 30, 0);
            var runtimeProjection = applied.Succeeded && disturbedResolution.Available &&
                                    damageResolution.Available && profile.ContentsChangedRequiredAtRuntime &&
                                    profile.DamageEventsRequiredAtRuntime &&
                                    disturbedRuntime.Binding == "ContenuObjetModifie" &&
                                    disturbedRuntime.Payload.CurrentRevision == 3 &&
                                    damagedRuntime.Binding == "ObjetEndommage" &&
                                    damagedRuntime.Payload.Name == "DamageApplied" &&
                                    destroyedRuntime.Binding == "ObjetDetruit" &&
                                    destroyedRuntime.Payload.Name == "EntityDestroyed";

            var scriptHandle = new WorldFeatureScriptService().Query(
                editing.History!.State, "placeable", profile.Id.ToString("D"), 1).Single();
            var scriptRead = scriptHandle.Properties["onUsedBinding"] == "ObjetUtilise" &&
                             scriptHandle.Properties["forgeScriptApi"] == "1.1.0" &&
                             scriptHandle.Properties["contentsChanged"] == "ContenuObjetModifie" &&
                             scriptHandle.Properties["contentsChangedApi"] == "1.1.0" &&
                             scriptHandle.Properties["contentsChangedPayload"] ==
                             "transactional-delta-cause-revision-actor" &&
                             scriptHandle.Properties["damageApplied"] == "ObjetEndommage" &&
                             scriptHandle.Properties["entityDestroyed"] == "ObjetDetruit" &&
                             scriptHandle.Properties["damageEventPayload"] ==
                             "source-amount-damage-type-previous-current-context";
            var noContents = profile with
            {
                SecurityEvents = profile.SecurityEvents! with { ContentsChanged = null },
                ContentsChangedRequiredAtRuntime = false,
            };
            var noDamage = profile with { DamageEvents = null, DamageEventsRequiredAtRuntime = false };
            var contentsOptional = securityAdapter.ResolveContentsChanged(noContents);
            var contentsRequired = securityAdapter.ResolveContentsChanged(noContents with
                { ContentsChangedRequiredAtRuntime = true });
            var damageOptional = compositionAdapter.ResolveDamageEvents(noDamage);
            var damageRequired = compositionAdapter.ResolveDamageEvents(noDamage with
                { DamageEventsRequiredAtRuntime = true });
            var optionalRequired = contentsOptional.OptionalFallback &&
                                   contentsRequired.DiagnosticCode ==
                                   "WORLD_PLACEABLE_CONTENTS_CHANGED_REQUIRED_MISSING" &&
                                   damageOptional.OptionalFallback &&
                                   damageRequired.DiagnosticCode ==
                                   "WORLD_PLACEABLE_DAMAGE_EVENTS_REQUIRED_MISSING";

            var undo = editing.Undo();
            var undone = ReferenceEquals(originalProject, editing.Project);
            var redo = editing.Redo();
            var redone = ReferenceEquals(appliedProject, editing.Project);
            var undoRedo = undo.Succeeded && undone && redo.Succeeded && redone;
            var save = editing.Save();
            editing.Close();
            var reopen = reopened.Open(tempRoot);
            var persisted = reopened.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
                item.BlueprintId == blueprint.Id);
            var persistedDamage = persisted?.DamageEvents ?? persisted?.CompositionEvents?.DamageEvents;
            var persistence = save.Succeeded && reopen.Succeeded && persisted is not null &&
                              persisted.Foundation?.OnUsed?.EventBinding == "ObjetUtilise" &&
                              persisted.SecurityEvents?.ContentsChanged?.EventBinding == "ContenuObjetModifie" &&
                              persistedDamage?.DamagedBinding == "ObjetEndommage" &&
                              persistedDamage?.DestroyedBinding == "ObjetDetruit" &&
                              persisted.ContentsChangedRequiredAtRuntime && persisted.DamageEventsRequiredAtRuntime &&
                              reopened.Project?.Module.WorldFeatures.Placeables.Any(item =>
                                  item.BlueprintId == neighborBlueprint.Id) == true;
            var succeeded = onUsedBindingAndApi && onUsedPayloadAndScripts &&
                            contentsChangedBindingAndPayload && damageAndDeathBindings && damageAndDeathPayload &&
                            dryRun && commandParity && instanceSelection && preservation && runtimeProjection &&
                            scriptRead && optionalRequired && undoRedo && persistence && invalidBlocked &&
                            humanDiagnostics;
            return new(
                succeeded,
                onUsedBindingAndApi,
                onUsedPayloadAndScripts,
                contentsChangedBindingAndPayload,
                damageAndDeathBindings,
                damageAndDeathPayload,
                dryRun,
                commandParity,
                instanceSelection,
                preservation,
                runtimeProjection,
                scriptRead,
                optionalRequired,
                undoRedo,
                persistence,
                invalidBlocked,
                humanDiagnostics);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1V_CONTRACT_EXCEPTION type={exception.GetType().Name} message={exception.Message}");
            return FailedUx1VSmoke();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static StudioPlaceableOnUsedEventPreview FailedOnUsedPreview(string message, string code) =>
        new(false, message, code, string.Empty, string.Empty, string.Empty, string.Empty, null, null);

    private static StudioPlaceableContentsChangedPreview FailedContentsChangedPreview(string message, string code) =>
        new(false, message, code, string.Empty, string.Empty, 0, 0, 0, string.Empty, string.Empty);

    private static StudioPlaceableDamageEventPreview FailedDamageEventPreview(string message, string code) =>
        new(false, message, code, string.Empty, string.Empty, string.Empty, 0, string.Empty, 0, 0);

    private static StudioUx1VSmokeResult FailedUx1VSmoke(
        string? stage = null,
        IEnumerable<string>? diagnostics = null)
    {
        if (stage is not null)
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX1V_{stage}_FAILED diagnostics=" +
                              string.Join(" | ", diagnostics ?? []));
        return new(false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false);
    }
}
