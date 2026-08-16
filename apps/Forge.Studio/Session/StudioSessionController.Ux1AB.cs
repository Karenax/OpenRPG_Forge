using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Application.WorldFeatures;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux1ABProvenance = "studio:ux1-ab:door-damage-loading";
    private const string DamageAppliedEventUx1AB = "damage-applied";
    private const string EntityDestroyedEventUx1AB = "entity-destroyed";
    private const string PhysicalAttackTargetedEventUx1AB = "physical-attack-targeted";
    private const string PhysicalAttackResolvedEventUx1AB = "physical-attack-resolved";

    private static readonly string[] ManagedDoorEventsUx1AB =
    [
        DamageAppliedEventUx1AB,
        EntityDestroyedEventUx1AB,
        PhysicalAttackTargetedEventUx1AB,
        PhysicalAttackResolvedEventUx1AB,
    ];

    private readonly SetWorldFeatureCatalogHandler _setDoorCatalogUx1AB = new(new SystemClock());
    private StudioDoorDamageLoadingDraft? _approvedDoorDamageLoadingDraftUx1AB;
    private ProjectRevision? _approvedDoorDamageLoadingRevisionUx1AB;

    internal IReadOnlyList<StudioDoorDamageLoadingChoice> DoorDamageLoadingChoicesUx1AB
    {
        get
        {
            var project = Project;
            if (project is null)
            {
                return [];
            }

            var configuredBlueprints = project.Module.WorldFeatures.Doors
                .Where(profile => profile.Damage is not null || profile.Loading is not null ||
                                  profile.Events.Any(binding => IsManagedDoorEventUx1AB(binding.Event)))
                .Select(profile => profile.BlueprintId)
                .ToHashSet();
            return project.Blueprints
                .Where(blueprint => blueprint.Kind == BlueprintKind.Door)
                .OrderBy(blueprint => blueprint.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Select(blueprint => new StudioDoorDamageLoadingChoice(
                    blueprint.Id,
                    blueprint.DisplayName,
                    configuredBlueprints.Contains(blueprint.Id)))
                .ToArray();
        }
    }

    internal StudioDoorDamageLoadingDraft? CreateDoorDamageLoadingDraftUx1AB(BlueprintId blueprintId)
    {
        var project = Project;
        if (project is null || project.Blueprints.All(blueprint =>
                blueprint.Id != blueprintId || blueprint.Kind != BlueprintKind.Door))
        {
            return null;
        }

        var matches = project.Module.WorldFeatures.Doors
            .Where(profile => profile.BlueprintId == blueprintId)
            .ToArray();
        if (matches.Length > 1)
        {
            return null;
        }

        var profile = matches.SingleOrDefault();
        var damage = profile?.Damage;
        var loading = profile?.Loading;
        var damaged = FindDoorEventUx1AB(profile, DamageAppliedEventUx1AB);
        var destroyed = FindDoorEventUx1AB(profile, EntityDestroyedEventUx1AB);
        var attackTargeted = FindDoorEventUx1AB(profile, PhysicalAttackTargetedEventUx1AB);
        var attackResolved = FindDoorEventUx1AB(profile, PhysicalAttackResolvedEventUx1AB);
        return new StudioDoorDamageLoadingDraft(
            blueprintId,
            damage is not null,
            damage?.MaximumHealth ?? 40,
            damage?.Hardness ?? 4,
            damage?.PlotProtected ?? false,
            damage?.DamagedVisual ?? "door.damaged",
            damage?.BreachedVisual ?? "door.breached",
            damage?.UpdateNavigation ?? true,
            damage?.DebrisAssetId ?? "assets/debris/door.glb",
            damage?.RepairServiceId ?? "service.repair-door",
            loading is not null,
            loading?.ImageAssetId ?? "assets/loading/door.png",
            loading?.TitleKey ?? "loading.door.title",
            loading?.TipKey ?? "loading.door.tip",
            loading?.UseDestinationFallback ?? true,
            loading?.MinimumDisplaySeconds ?? .5,
            damaged is not null,
            damaged?.ScriptId ?? "scripts/door_damaged",
            destroyed is not null,
            destroyed?.ScriptId ?? "scripts/door_destroyed",
            attackTargeted is not null,
            attackTargeted?.ScriptId ?? "scripts/door_attack_targeted",
            attackResolved is not null,
            attackResolved?.ScriptId ?? "scripts/door_attack_resolved");
    }

    internal StudioDoorDamageLoadingPreview PreviewDoorDamageLoadingUx1AB(
        StudioDoorDamageLoadingDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelDoorDamageLoadingPreviewUx1AB();
        var history = History;
        if (!TryBuildDoorDamageLoadingUx1AB(draft, dryRun: true, out var command, out var profile,
                out var message) || command is null || profile is null || history is null)
        {
            return FailedDoorDamageLoadingPreviewUx1AB(message);
        }

        var originalProject = history.State.Project;
        var originalRevision = history.State.Revision;
        var preview = _setDoorCatalogUx1AB.Handle(history.State, command);
        if (!preview.Succeeded)
        {
            return FailedDoorDamageLoadingPreviewUx1AB(
                HumanizeDoorDamageLoadingDiagnosticUx1AB(preview.Diagnostics.FirstOrDefault()?.Code));
        }

        var dryRunUntouched = ReferenceEquals(originalProject, history.State.Project) &&
                              originalRevision == history.State.Revision &&
                              preview.NewRevision is null;
        if (!dryRunUntouched)
        {
            return FailedDoorDamageLoadingPreviewUx1AB(
                "L’aperçu n’a pas pu garantir l’absence d’écriture.");
        }

        var damageAccepted = PreviewDoorDamageUx1AB(profile, draft);
        var eventsAccepted = PreviewDoorEventsUx1AB(profile, draft);
        if (!damageAccepted || !eventsAccepted)
        {
            return FailedDoorDamageLoadingPreviewUx1AB(
                "La simulation de dégâts ou d’événements ne correspond pas aux réglages choisis.");
        }

        var damageSummary = draft.DamageEnabled
            ? $"Solidité · {draft.MaximumHealth:0.##} points · dureté {draft.Hardness:0.##} · " +
              $"{(draft.PlotProtected ? "protégée par l’histoire" : "brisable")} · " +
              $"{(draft.UpdateNavigation ? "passage recalculé après brèche" : "passage inchangé")}"
            : "Solidité · désactivée";
        var loadingSummary = draft.LoadingEnabled
            ? $"Chargement · {(string.IsNullOrWhiteSpace(draft.LoadingImageAssetId) ? "visuel de destination" : "visuel spécifique")} · " +
              $"minimum {draft.MinimumDisplaySeconds:0.##} s · " +
              $"{(draft.UseDestinationFallback ? "repli destination autorisé" : "visuel requis")}"
            : "Chargement · présentation spécifique désactivée";
        var eventLabels = EnabledDoorEventLabelsUx1AB(draft).ToArray();
        var eventSummary = eventLabels.Length == 0
            ? "Événements serveur · aucun binding géré"
            : $"Événements serveur · {string.Join(" · ", eventLabels)}";
        _approvedDoorDamageLoadingDraftUx1AB = draft;
        _approvedDoorDamageLoadingRevisionUx1AB = originalRevision;
        return new StudioDoorDamageLoadingPreview(
            true,
            "Aperçu validé · aucune porte ni aucun fichier n’a été modifié.",
            $"{damageSummary}\n{loadingSummary}\n{eventSummary}",
            true,
            damageAccepted,
            eventsAccepted);
    }

    internal StudioDoorDamageLoadingActionResult ApplyApprovedDoorDamageLoadingUx1AB()
    {
        var draft = _approvedDoorDamageLoadingDraftUx1AB;
        var approvedRevision = _approvedDoorDamageLoadingRevisionUx1AB;
        var history = History;
        if (draft is null || approvedRevision is null || history is null)
        {
            CancelDoorDamageLoadingPreviewUx1AB();
            return new(false, "Relancez l’aperçu avant d’appliquer.");
        }
        if (history.State.Revision != approvedRevision.Value)
        {
            CancelDoorDamageLoadingPreviewUx1AB();
            return new(false, "Le module a changé depuis l’aperçu · relancez-le avant d’appliquer.", true);
        }
        if (!TryBuildDoorDamageLoadingUx1AB(draft, dryRun: true, out var command, out _, out var message) ||
            command is null)
        {
            CancelDoorDamageLoadingPreviewUx1AB();
            return new(false, message);
        }

        var preview = _setDoorCatalogUx1AB.Handle(history.State, command);
        if (!preview.Succeeded)
        {
            CancelDoorDamageLoadingPreviewUx1AB();
            return new(false,
                HumanizeDoorDamageLoadingDiagnosticUx1AB(preview.Diagnostics.FirstOrDefault()?.Code));
        }

        var applied = _setDoorCatalogUx1AB.Handle(
            history.State,
            command with
            {
                CommandId = CommandId.From(Guid.NewGuid()),
                DryRun = false,
            });
        if (!applied.Succeeded)
        {
            CancelDoorDamageLoadingPreviewUx1AB();
            return new(false,
                HumanizeDoorDamageLoadingDiagnosticUx1AB(applied.Diagnostics.FirstOrDefault()?.Code));
        }

        history.Record(applied);
        InvalidateContentIndexes();
        CancelDoorDamageLoadingPreviewUx1AB();
        return new(true, "Dégâts, chargement et événements de porte appliqués · enregistrez le module.");
    }

    internal void CancelDoorDamageLoadingPreviewUx1AB()
    {
        _approvedDoorDamageLoadingDraftUx1AB = null;
        _approvedDoorDamageLoadingRevisionUx1AB = null;
    }

    private void CloseUx1AB() => CancelDoorDamageLoadingPreviewUx1AB();

    internal StudioUx1ABSmokeResult RunUx1ABContractSmoke()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "openrpg-forge-studio-ux1ab-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var authored = OriginalModuleAuthoringService.CreateVeilleeDesEclats().State.Project;
            var targetBlueprint = authored.Blueprints.Single(blueprint => blueprint.Kind == BlueprintKind.Door);
            var neighborBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1ea0000-0000-4000-8000-000000000002")),
                "porte-voisine-ab",
                "Porte voisine AB",
                BlueprintKind.Door);
            var freshBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1ea0000-0000-4000-8000-000000000003")),
                "porte-neuve-ab",
                "Porte neuve AB",
                BlueprintKind.Door);
            var targetProfile = CreateCompleteDoorProfileUx1X(
                targetBlueprint.Id,
                Guid.Parse("c1ea0000-0000-4000-8000-000000000010"),
                authored.Dialogues.Single().Id.Value) with
            {
                Events =
                [
                    new(DamageAppliedEventUx1AB, "scripts/door_damage_old", false, true),
                    new("ability-impacted", "scripts/door_ability_neighbor", false, true),
                    new(EntityDestroyedEventUx1AB, "scripts/door_death_old", false, true),
                    new("heartbeat", "scripts/door_heartbeat_neighbor", false, true),
                    new(PhysicalAttackResolvedEventUx1AB, "scripts/door_attack_old", false, true),
                    new("custom:ash:seal", "scripts/door_custom_neighbor", true, false),
                ],
            };
            var neighborProfile = CreateNeighborDoorProfileUx1X(
                neighborBlueprint.Id,
                Guid.Parse("c1ea0000-0000-4000-8000-000000000011"));
            var catalog = new WorldFeatureCatalog(
                authored.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-ab-catalog",
                authored.Module.WorldFeatures.Placeables,
                [targetProfile, neighborProfile],
                authored.Module.WorldFeatures.Triggers,
                authored.Module.WorldFeatures.Encounters,
                authored.Module.WorldFeatures.VehicleRoutes,
                authored.Module.WorldFeatures.Merchants,
                authored.Module.WorldFeatures.Companions,
                authored.Module.WorldFeatures.Scripts);
            var project = new ProjectDefinition(
                authored.Id,
                authored.Title,
                authored.Module.WithWorldFeatures(catalog),
                authored.Blueprints.Append(neighborBlueprint).Append(freshBlueprint).ToArray(),
                authored.Areas,
                authored.Dialogues,
                authored.Quests);
            var created = ProjectSession.Create(tempRoot, project);
            if (!created.Succeeded)
            {
                return FailedUx1ABSmoke();
            }
            var opened = editing.Open(tempRoot);
            var currentProject = editing.Project;
            var history = editing.History;
            if (!opened.Succeeded || currentProject is null || history is null)
            {
                return FailedUx1ABSmoke();
            }

            var loadedOriginal = currentProject.Module.WorldFeatures.Doors.Single(profile =>
                profile.BlueprintId == targetBlueprint.Id);
            var loadedNeighbor = currentProject.Module.WorldFeatures.Doors.Single(profile =>
                profile.BlueprintId == neighborBlueprint.Id);
            var loadedOrder = currentProject.Module.WorldFeatures.Doors.Select(profile => profile.Id).ToArray();
            var neighborEventNames = new[] { "ability-impacted", "heartbeat", "custom:ash:seal" };
            var loadedNeighborEvents = loadedOriginal.Events
                .Where(binding => neighborEventNames.Contains(binding.Event, StringComparer.Ordinal))
                .ToArray();
            var choices = editing.DoorDamageLoadingChoicesUx1AB;
            var source = editing.CreateDoorDamageLoadingDraftUx1AB(targetBlueprint.Id);
            var fresh = editing.CreateDoorDamageLoadingDraftUx1AB(freshBlueprint.Id);
            if (source is null || fresh is null)
            {
                return FailedUx1ABSmoke();
            }

            var explicitSelection = choices.Count == 3 &&
                                    choices.Single(choice => choice.BlueprintId == targetBlueprint.Id).Configured &&
                                    !choices.Single(choice => choice.BlueprintId == freshBlueprint.Id).Configured &&
                                    source.BlueprintId == targetBlueprint.Id;
            var draft = source with
            {
                DamageEnabled = true,
                MaximumHealth = 120,
                Hardness = 6,
                PlotProtected = false,
                DamagedVisual = "door.ab.damaged",
                BreachedVisual = "door.ab.breached",
                UpdateNavigation = true,
                DebrisAssetId = "assets/debris/door-ab.glb",
                RepairServiceId = "service.repair-door-ab",
                LoadingEnabled = true,
                LoadingImageAssetId = "assets/loading/door-ab.png",
                LoadingTitleKey = "loading.door-ab.title",
                LoadingTipKey = "loading.door-ab.tip",
                UseDestinationFallback = false,
                MinimumDisplaySeconds = .75,
                DamageEventEnabled = true,
                DamageScriptId = "scripts/door_ab_damaged",
                DestructionEventEnabled = true,
                DestructionScriptId = "scripts/door_ab_destroyed",
                AttackTargetedEventEnabled = true,
                AttackTargetedScriptId = "scripts/door_ab_attack_targeted",
                AttackResolvedEventEnabled = true,
                AttackResolvedScriptId = "scripts/door_ab_attack_resolved",
            };

            var beforePreview = editing.Project;
            var beforeRevision = history.State.Revision;
            var preview = editing.PreviewDoorDamageLoadingUx1AB(draft);
            var dryRun = preview.Succeeded && preview.CanApply && preview.DamageSimulationAccepted &&
                         preview.EventSimulationAccepted && ReferenceEquals(beforePreview, editing.Project) &&
                         history.State.Revision == beforeRevision && !editing.IsDirty;
            var fallbackPreview = editing.PreviewDoorDamageLoadingUx1AB(draft with
            {
                LoadingImageAssetId = string.Empty,
                UseDestinationFallback = true,
            });
            var plotPreview = editing.PreviewDoorDamageLoadingUx1AB(draft with { PlotProtected = true });

            var commandBuilt = editing.TryBuildDoorDamageLoadingUx1AB(
                draft,
                dryRun: true,
                out var dryRunCommand,
                out var builtProfile,
                out _);
            var commandPreview = dryRunCommand is null
                ? null
                : editing._setDoorCatalogUx1AB.Handle(history.State, dryRunCommand);
            var freshDraft = fresh with
            {
                DamageEnabled = true,
                MaximumHealth = 35,
                Hardness = 2,
                DamageEventEnabled = true,
                DamageScriptId = "scripts/door_fresh_damaged",
            };
            var freshBuilt = editing.TryBuildDoorDamageLoadingUx1AB(
                freshDraft,
                dryRun: true,
                out var freshCommand,
                out var freshProfile,
                out _);
            var canonicalCommand = commandBuilt && commandPreview?.Succeeded == true && builtProfile is not null &&
                                   dryRunCommand is { DryRun: true } doorCommand &&
                                   doorCommand.Catalog.Doors.Select(profile => profile.Id)
                                       .SequenceEqual(loadedOrder) &&
                                   freshBuilt && freshCommand is { } newDoorCommand &&
                                   freshProfile is { } newDoorProfile &&
                                   newDoorCommand.Catalog.Doors.Select(profile => profile.Id)
                                       .SequenceEqual(loadedOrder.Append(newDoorProfile.Id)) &&
                                   newDoorProfile.Id == StableDoorIdUx1AB(freshBlueprint.Id) &&
                                   ReferenceEquals(beforePreview, editing.Project) &&
                                   history.State.Revision == beforeRevision;

            var builtDamage = builtProfile?.Damage;
            var damagePolicy = plotPreview.Succeeded && builtDamage is
            {
                MaximumHealth: 120,
                Hardness: 6,
                PlotProtected: false,
                DamagedVisual: "door.ab.damaged",
                BreachedVisual: "door.ab.breached",
                UpdateNavigation: true,
                DebrisAssetId: "assets/debris/door-ab.glb",
                RepairServiceId: "service.repair-door-ab",
            };
            var loadingPresentation = fallbackPreview.Succeeded && builtProfile?.Loading is
            {
                ImageAssetId: "assets/loading/door-ab.png",
                TitleKey: "loading.door-ab.title",
                TipKey: "loading.door-ab.tip",
                UseDestinationFallback: false,
                MinimumDisplaySeconds: .75,
            };
            var authoritativeEvents = builtProfile is { } eventProfile &&
                                      ManagedDoorEventsUx1AB.All(eventName =>
                                      {
                                          var binding = eventProfile.Events.Single(item => item.Event == eventName);
                                          var resolved = WorldFeatureRuntimeResolver.ResolveDoorEvent(
                                              eventProfile,
                                              eventName,
                                              0,
                                              0);
                                          return binding.Authoritative && resolved is
                                          {
                                              Dispatched: true,
                                              Reason: "server",
                                          } && resolved.ScriptId == binding.ScriptId;
                                      }) &&
                                      eventProfile.Events.Single(binding =>
                                          binding.Event == PhysicalAttackTargetedEventUx1AB).Before &&
                                      !eventProfile.Events.Single(binding =>
                                          binding.Event == PhysicalAttackResolvedEventUx1AB).Before;
            var expectedEventOrder = new[]
            {
                DamageAppliedEventUx1AB,
                "ability-impacted",
                EntityDestroyedEventUx1AB,
                "heartbeat",
                PhysicalAttackResolvedEventUx1AB,
                "custom:ash:seal",
                PhysicalAttackTargetedEventUx1AB,
            };
            var neighborEventMerge = builtProfile is { } mergedProfile &&
                                     mergedProfile.Events.Select(binding => binding.Event)
                                         .SequenceEqual(expectedEventOrder) &&
                                     loadedNeighborEvents.All(original =>
                                         ReferenceEquals(
                                             original,
                                             mergedProfile.Events.Single(binding => binding.Event == original.Event)));

            var staleBase = editing.Project;
            var stalePreview = editing.PreviewDoorDamageLoadingUx1AB(draft);
            var interveningBuilt = editing.TryBuildDoorDamageLoadingUx1AB(
                draft with { MaximumHealth = 121 },
                dryRun: false,
                out var interveningCommand,
                out _,
                out _);
            var intervening = interveningCommand is null
                ? null
                : editing._setDoorCatalogUx1AB.Handle(history.State, interveningCommand);
            if (intervening?.Succeeded == true)
            {
                history.Record(intervening);
            }
            var staleApply = editing.ApplyApprovedDoorDamageLoadingUx1AB();
            var staleUndo = editing.Undo();
            var staleRevisionBlocked = stalePreview.Succeeded && interveningBuilt &&
                                       intervening?.Succeeded == true && !staleApply.Succeeded &&
                                       staleApply.StaleRevision && staleUndo.Succeeded &&
                                       ReferenceEquals(staleBase, editing.Project) && !editing.IsDirty;

            var invalidPreviews = new[]
            {
                editing.PreviewDoorDamageLoadingUx1AB(draft with { MaximumHealth = 0 }),
                editing.PreviewDoorDamageLoadingUx1AB(draft with { Hardness = -1 }),
                editing.PreviewDoorDamageLoadingUx1AB(draft with { BreachedVisual = string.Empty }),
                editing.PreviewDoorDamageLoadingUx1AB(draft with { DebrisAssetId = string.Empty }),
                editing.PreviewDoorDamageLoadingUx1AB(draft with
                {
                    LoadingImageAssetId = string.Empty,
                    UseDestinationFallback = false,
                }),
                editing.PreviewDoorDamageLoadingUx1AB(draft with { LoadingTitleKey = string.Empty }),
                editing.PreviewDoorDamageLoadingUx1AB(draft with { MinimumDisplaySeconds = -1 }),
                editing.PreviewDoorDamageLoadingUx1AB(draft with { DamageScriptId = string.Empty }),
                editing.PreviewDoorDamageLoadingUx1AB(draft with { AttackResolvedScriptId = string.Empty }),
            };
            var invalidBlocked = invalidPreviews.All(result => !result.Succeeded && !result.CanApply) &&
                                 ReferenceEquals(staleBase, editing.Project) && !editing.IsDirty;
            var humanDiagnostics = invalidPreviews.All(result => IsHumanDoorDamageLoadingMessageUx1AB(result.Message));

            var approved = editing.PreviewDoorDamageLoadingUx1AB(draft);
            var apply = editing.ApplyApprovedDoorDamageLoadingUx1AB();
            var appliedProject = editing.Project;
            var appliedProfile = appliedProject?.Module.WorldFeatures.Doors.Single(profile =>
                profile.BlueprintId == targetBlueprint.Id);
            var preservedNeighbor = appliedProject?.Module.WorldFeatures.Doors.Single(profile =>
                profile.BlueprintId == neighborBlueprint.Id);
            var preservation = approved.Succeeded && apply.Succeeded &&
                               appliedProject is { } preservedProject &&
                               appliedProfile is { } preservedProfile &&
                               preservedProfile.Id == loadedOriginal.Id &&
                               preservedProfile.SchemaVersion == loadedOriginal.SchemaVersion &&
                               preservedProfile.Provenance == loadedOriginal.Provenance &&
                               preservedProfile.DialogueId == loadedOriginal.DialogueId &&
                               preservedProfile.FactionId == loadedOriginal.FactionId &&
                               ReferenceEquals(preservedProfile.Assembly, loadedOriginal.Assembly) &&
                               ReferenceEquals(preservedProfile.Foundation, loadedOriginal.Foundation) &&
                               ReferenceEquals(preservedProfile.AccessEvents, loadedOriginal.AccessEvents) &&
                               preservedProfile.AccessEventsRequiredAtRuntime ==
                               loadedOriginal.AccessEventsRequiredAtRuntime &&
                               ReferenceEquals(preservedNeighbor, loadedNeighbor) &&
                               preservedProject.Module.WorldFeatures.Doors.Select(profile => profile.Id)
                                   .SequenceEqual(loadedOrder) &&
                               loadedNeighborEvents.All(original =>
                                   ReferenceEquals(
                                       original,
                                       preservedProfile.Events.Single(binding => binding.Event == original.Event)));

            var undo = editing.Undo();
            var undone = undo.Succeeded && ReferenceEquals(staleBase, editing.Project) && !editing.IsDirty;
            var redo = editing.Redo();
            var redone = redo.Succeeded && editing.Project?.Module.WorldFeatures.Doors.Single(profile =>
                profile.BlueprintId == targetBlueprint.Id).Damage?.MaximumHealth == 120;
            var undoRedo = apply.Succeeded && undone && redone;

            var saved = editing.Save();
            editing.Close();
            var reopen = reopened.Open(tempRoot);
            var persisted = reopened.Project?.Module.WorldFeatures.Doors.Single(profile =>
                profile.BlueprintId == targetBlueprint.Id);
            var persistence = saved.Succeeded && reopen.Succeeded && persisted is
                              {
                                  Damage:
                                  {
                                      MaximumHealth: 120,
                                      Hardness: 6,
                                      PlotProtected: false,
                                      BreachedVisual: "door.ab.breached",
                                      UpdateNavigation: true,
                                      DebrisAssetId: "assets/debris/door-ab.glb",
                                      RepairServiceId: "service.repair-door-ab",
                                  },
                                  Loading:
                                  {
                                      ImageAssetId: "assets/loading/door-ab.png",
                                      TitleKey: "loading.door-ab.title",
                                      TipKey: "loading.door-ab.tip",
                                      UseDestinationFallback: false,
                                      MinimumDisplaySeconds: .75,
                                  },
                              } persistedProfile && persistedProfile.Events.Select(binding => binding.Event)
                                  .SequenceEqual(expectedEventOrder) &&
                              persistedProfile.Events.Single(binding =>
                                  binding.Event == DamageAppliedEventUx1AB).ScriptId ==
                              "scripts/door_ab_damaged" &&
                              persistedProfile.Events.Single(binding =>
                                  binding.Event == PhysicalAttackTargetedEventUx1AB).Authoritative &&
                              persistedProfile.Events.Single(binding => binding.Event == "ability-impacted") is
                              {
                                  ScriptId: "scripts/door_ability_neighbor",
                                  Before: false,
                                  Authoritative: true,
                              } && persistedProfile.Events.Single(binding => binding.Event == "custom:ash:seal") is
                              {
                                  ScriptId: "scripts/door_custom_neighbor",
                                  Before: true,
                                  Authoritative: false,
                              } &&
                              persistedProfile.DialogueId == loadedOriginal.DialogueId &&
                              persistedProfile.FactionId == loadedOriginal.FactionId &&
                              persistedProfile.Assembly?.Kind == loadedOriginal.Assembly?.Kind &&
                              persistedProfile.Foundation?.InitialStateId == loadedOriginal.Foundation?.InitialStateId &&
                              persistedProfile.AccessEvents?.Id == loadedOriginal.AccessEvents?.Id;

            var succeeded = explicitSelection && damagePolicy && loadingPresentation &&
                            authoritativeEvents && neighborEventMerge && dryRun && staleRevisionBlocked &&
                            canonicalCommand && preservation && undoRedo && persistence && invalidBlocked &&
                            humanDiagnostics;
            return new StudioUx1ABSmokeResult(
                succeeded,
                explicitSelection,
                damagePolicy,
                loadingPresentation,
                authoritativeEvents,
                neighborEventMerge,
                dryRun,
                staleRevisionBlocked,
                canonicalCommand,
                preservation,
                undoRedo,
                persistence,
                invalidBlocked,
                humanDiagnostics);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX1AB_CONTRACT_FAILED " + exception);
            return FailedUx1ABSmoke();
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

    private bool TryBuildDoorDamageLoadingUx1AB(
        StudioDoorDamageLoadingDraft draft,
        bool dryRun,
        out SetWorldFeatureCatalogCommand? command,
        out DoorWorldProfile? profile,
        out string message)
    {
        command = null;
        profile = null;
        message = string.Empty;
        var history = History;
        var project = Project;
        if (history is null || project is null)
        {
            message = "Ouvrez un module avant de configurer une porte.";
            return false;
        }
        if (project.Blueprints.All(blueprint =>
                blueprint.Id != draft.BlueprintId || blueprint.Kind != BlueprintKind.Door))
        {
            message = "La porte sélectionnée n’est plus disponible dans le module.";
            return false;
        }
        if (!ValidateDoorDamageLoadingDraftUx1AB(draft, out message))
        {
            return false;
        }

        var catalog = project.Module.WorldFeatures;
        var matches = catalog.Doors.Where(candidate => candidate.BlueprintId == draft.BlueprintId).ToArray();
        if (matches.Length > 1)
        {
            message = "Plusieurs fiches ciblent cette porte · corrigez le module avant l’édition.";
            return false;
        }

        try
        {
            var existing = matches.SingleOrDefault();
            var damage = draft.DamageEnabled
                ? new DoorWorldProfile.DamagePolicy(
                    draft.MaximumHealth,
                    draft.Hardness,
                    draft.PlotProtected,
                    draft.DamagedVisual.Trim(),
                    draft.BreachedVisual.Trim(),
                    draft.UpdateNavigation,
                    draft.DebrisAssetId.Trim(),
                    draft.RepairServiceId.Trim())
                : null;
            var loading = draft.LoadingEnabled
                ? new DoorWorldProfile.LoadingPresentation(
                    draft.LoadingImageAssetId.Trim(),
                    draft.LoadingTitleKey.Trim(),
                    draft.LoadingTipKey.Trim(),
                    draft.UseDestinationFallback,
                    draft.MinimumDisplaySeconds)
                : null;
            var events = MergeDoorEventsUx1AB(existing?.Events ?? [], draft);
            var builtProfile = existing is null
                ? new DoorWorldProfile(
                    StableDoorIdUx1AB(draft.BlueprintId),
                    WorldFeatureCatalog.CurrentSchemaVersion,
                    Ux1ABProvenance,
                    draft.BlueprintId,
                    damage,
                    loading,
                    events,
                    null,
                    null,
                    null)
                : existing with
                {
                    Damage = damage,
                    Loading = loading,
                    Events = events,
                };
            profile = builtProfile;
            var doors = existing is null
                ? catalog.Doors.Append(builtProfile).ToArray()
                : catalog.Doors.Select(candidate =>
                    candidate.Id == existing.Id ? builtProfile : candidate).ToArray();
            var candidateCatalog = new WorldFeatureCatalog(
                catalog.Id,
                catalog.SchemaVersion,
                catalog.Provenance,
                catalog.Placeables,
                doors,
                catalog.Triggers,
                catalog.Encounters,
                catalog.VehicleRoutes,
                catalog.Merchants,
                catalog.Companions,
                catalog.Scripts);
            command = new SetWorldFeatureCatalogCommand(
                CommandId.From(Guid.NewGuid()),
                history.State.Revision,
                candidateCatalog,
                dryRun);
            return true;
        }
        catch (ArgumentException)
        {
            message = "La fiche de dégâts ou de chargement est incomplète · vérifiez les champs signalés.";
            return false;
        }
    }

    private static bool ValidateDoorDamageLoadingDraftUx1AB(
        StudioDoorDamageLoadingDraft draft,
        out string message)
    {
        message = string.Empty;
        if (draft.DamageEnabled)
        {
            if (!double.IsFinite(draft.MaximumHealth) || draft.MaximumHealth <= 0)
            {
                message = "Les points de structure doivent être supérieurs à zéro.";
                return false;
            }
            if (!double.IsFinite(draft.Hardness) || draft.Hardness < 0)
            {
                message = "La dureté ne peut pas être négative.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(draft.DamagedVisual) ||
                string.IsNullOrWhiteSpace(draft.BreachedVisual))
            {
                message = "Choisissez les apparences endommagée et brisée de la porte.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(draft.DebrisAssetId))
            {
                message = "Choisissez l’asset de débris produit par la brèche.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(draft.RepairServiceId))
            {
                message = "Choisissez le service utilisé pour réparer la porte.";
                return false;
            }
        }

        if (draft.LoadingEnabled)
        {
            if (!draft.UseDestinationFallback && string.IsNullOrWhiteSpace(draft.LoadingImageAssetId))
            {
                message = "Choisissez une image de chargement ou autorisez le visuel de destination.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(draft.LoadingTitleKey) ||
                string.IsNullOrWhiteSpace(draft.LoadingTipKey))
            {
                message = "Renseignez le titre et le conseil localisés de l’écran de chargement.";
                return false;
            }
            if (!double.IsFinite(draft.MinimumDisplaySeconds) || draft.MinimumDisplaySeconds < 0)
            {
                message = "La durée minimale de chargement ne peut pas être négative.";
                return false;
            }
        }

        foreach (var (enabled, script, label) in new[]
                 {
                     (draft.DamageEventEnabled, draft.DamageScriptId, "OnDamaged"),
                     (draft.DestructionEventEnabled, draft.DestructionScriptId, "OnDeath"),
                     (draft.AttackTargetedEventEnabled, draft.AttackTargetedScriptId, "attaque ciblée"),
                     (draft.AttackResolvedEventEnabled, draft.AttackResolvedScriptId, "attaque résolue"),
                 })
        {
            if (enabled && string.IsNullOrWhiteSpace(script))
            {
                message = $"Choisissez le script de l’événement {label}.";
                return false;
            }
        }
        return true;
    }

    private static IReadOnlyList<DoorWorldProfile.EventBinding> MergeDoorEventsUx1AB(
        IReadOnlyList<DoorWorldProfile.EventBinding> existing,
        StudioDoorDamageLoadingDraft draft)
    {
        var desired = new Dictionary<string, DoorWorldProfile.EventBinding>(StringComparer.Ordinal);
        if (draft.DamageEventEnabled)
            desired[DamageAppliedEventUx1AB] = new(
                DamageAppliedEventUx1AB, draft.DamageScriptId.Trim(), false, true);
        if (draft.DestructionEventEnabled)
            desired[EntityDestroyedEventUx1AB] = new(
                EntityDestroyedEventUx1AB, draft.DestructionScriptId.Trim(), false, true);
        if (draft.AttackTargetedEventEnabled)
            desired[PhysicalAttackTargetedEventUx1AB] = new(
                PhysicalAttackTargetedEventUx1AB, draft.AttackTargetedScriptId.Trim(), true, true);
        if (draft.AttackResolvedEventEnabled)
            desired[PhysicalAttackResolvedEventUx1AB] = new(
                PhysicalAttackResolvedEventUx1AB, draft.AttackResolvedScriptId.Trim(), false, true);

        var merged = new List<DoorWorldProfile.EventBinding>();
        var inserted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var binding in existing)
        {
            if (!IsManagedDoorEventUx1AB(binding.Event))
            {
                merged.Add(binding);
                continue;
            }
            if (desired.TryGetValue(binding.Event, out var replacement) && inserted.Add(binding.Event))
            {
                merged.Add(replacement);
            }
        }
        foreach (var eventName in ManagedDoorEventsUx1AB)
        {
            if (desired.TryGetValue(eventName, out var binding) && inserted.Add(eventName))
            {
                merged.Add(binding);
            }
        }
        return merged.ToArray();
    }

    private static bool PreviewDoorDamageUx1AB(
        DoorWorldProfile profile,
        StudioDoorDamageLoadingDraft draft)
    {
        if (!draft.DamageEnabled)
        {
            return profile.Damage is null;
        }
        if (profile.Damage is null)
        {
            return false;
        }
        var result = WorldFeatureRuntimeResolver.ApplyDoorDamage(
            profile,
            profile.Damage.MaximumHealth,
            profile.Damage.MaximumHealth + profile.Damage.Hardness);
        return profile.Damage.PlotProtected
            ? !result.Breached && result.AppliedDamage == 0
            : result.Breached && result.Health == 0 &&
              result.NavigationChanged == profile.Damage.UpdateNavigation &&
              result.Visual == profile.Damage.BreachedVisual;
    }

    private static bool PreviewDoorEventsUx1AB(
        DoorWorldProfile profile,
        StudioDoorDamageLoadingDraft draft)
    {
        var expected = new[]
        {
            (draft.DamageEventEnabled, DamageAppliedEventUx1AB, draft.DamageScriptId),
            (draft.DestructionEventEnabled, EntityDestroyedEventUx1AB, draft.DestructionScriptId),
            (draft.AttackTargetedEventEnabled, PhysicalAttackTargetedEventUx1AB,
                draft.AttackTargetedScriptId),
            (draft.AttackResolvedEventEnabled, PhysicalAttackResolvedEventUx1AB,
                draft.AttackResolvedScriptId),
        };
        return expected.Where(item => item.Item1).All(item =>
        {
            var resolved = WorldFeatureRuntimeResolver.ResolveDoorEvent(profile, item.Item2, 0, 0);
            return resolved.Dispatched && resolved.ScriptId == item.Item3.Trim() && resolved.Reason == "server";
        });
    }

    private static IEnumerable<string> EnabledDoorEventLabelsUx1AB(StudioDoorDamageLoadingDraft draft)
    {
        if (draft.DamageEventEnabled) yield return "OnDamaged";
        if (draft.DestructionEventEnabled) yield return "OnDeath";
        if (draft.AttackTargetedEventEnabled) yield return "attaque ciblée";
        if (draft.AttackResolvedEventEnabled) yield return "attaque résolue";
    }

    private static DoorWorldProfile.EventBinding? FindDoorEventUx1AB(
        DoorWorldProfile? profile,
        string eventName) => profile?.Events.FirstOrDefault(binding =>
        string.Equals(binding.Event, eventName, StringComparison.Ordinal));

    private static bool IsManagedDoorEventUx1AB(string eventName) =>
        ManagedDoorEventsUx1AB.Contains(eventName, StringComparer.Ordinal);

    private static Guid StableDoorIdUx1AB(BlueprintId blueprintId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{blueprintId.Value:D}:ux1-ab:profile"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16]);
    }

    private static StudioDoorDamageLoadingPreview FailedDoorDamageLoadingPreviewUx1AB(string message) =>
        new(false, message, string.Empty, false);

    private static StudioUx1ABSmokeResult FailedUx1ABSmoke() => new(
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

    private static string HumanizeDoorDamageLoadingDiagnosticUx1AB(string? code) => code switch
    {
        "command.revision_conflict" => "Le module a changé · relancez l’aperçu.",
        "world.door.damage_invalid" =>
            "La solidité exige des points de structure, une dureté et une apparence de brèche valides.",
        "world.door.loading_invalid" =>
            "Choisissez une image de chargement ou autorisez le visuel de destination.",
        "world.event.binding_invalid" => "Chaque événement activé exige un script nommé.",
        "world.reference.blueprint_missing" => "La porte sélectionnée n’est plus disponible.",
        _ => "Les réglages de dégâts, chargement ou événements ne respectent pas le module.",
    };

    private static bool IsHumanDoorDamageLoadingMessageUx1AB(string message) =>
        !string.IsNullOrWhiteSpace(message) &&
        !message.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("world.door", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("module/worldFeatures", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains(".json", StringComparison.OrdinalIgnoreCase) &&
        !Guid.TryParse(message, out _);
}
