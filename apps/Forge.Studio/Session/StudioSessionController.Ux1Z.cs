using Forge.Application.Commands;
using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Factions;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string PhysicalAttackTargetedEvent = "physical-attack-targeted";
    private const string PhysicalAttackResolvedEvent = "physical-attack-resolved";
    private const string AbilityTargetedEvent = "ability-targeted";
    private const string AbilityImpactedEvent = "ability-impacted";
    private const string HeartbeatEvent = "heartbeat";

    private StudioPlaceableReactivePhysicsDraft? _approvedUx1Z;
    private Forge.Application.Projects.ProjectRevision? _approvedUx1ZRevision;

    internal StudioUx1ZCatalog CreateUx1ZCatalog()
    {
        var project = Project ?? throw new InvalidOperationException("Aucun module n’est ouvert.");
        var profileIds = project.Module.WorldFeatures.Placeables
            .Select(profile => profile.BlueprintId)
            .ToHashSet();
        var placeables = project.Blueprints
            .Where(blueprint => blueprint.Kind == BlueprintKind.Placeable)
            .Select(blueprint => new StudioUx1ZPlaceableOption(
                blueprint.Id,
                blueprint.DisplayName,
                profileIds.Contains(blueprint.Id)))
            .OrderBy(option => option.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        return new(placeables);
    }

    internal StudioPlaceableReactivePhysicsDraft? CreateUx1ZDraft(BlueprintId? requestedBlueprintId = null)
    {
        var project = Project;
        if (project is null || requestedBlueprintId is null)
        {
            return null;
        }
        var blueprint = project.Blueprints.FirstOrDefault(candidate =>
            candidate.Id == requestedBlueprintId && candidate.Kind == BlueprintKind.Placeable);
        if (blueprint is null)
        {
            return null;
        }
        var profile = project.Module.WorldFeatures.Placeables.FirstOrDefault(candidate =>
            candidate.BlueprintId == requestedBlueprintId);
        var existingProfile = profile is not null;
        if (profile is null)
        {
            var foundationDraft = CreatePlaceableDraft(requestedBlueprintId);
            if (foundationDraft is null ||
                !TryBuildPlaceable(foundationDraft, true, out _, out profile, out _))
            {
                return null;
            }
        }
        if (profile is null)
        {
            return null;
        }

        var events = profile.Reactive.Events;
        var physicalTargeted = FindEvent(events, PhysicalAttackTargetedEvent);
        var physicalResolved = FindEvent(events, PhysicalAttackResolvedEvent);
        var abilityTargeted = FindEvent(events, AbilityTargetedEvent);
        var abilityImpacted = FindEvent(events, AbilityImpactedEvent);
        var heartbeat = FindEvent(events, HeartbeatEvent);
        var custom = events
            .Where(candidate => candidate.Event.StartsWith("custom:", StringComparison.Ordinal))
            .OrderBy(candidate => candidate.Event, StringComparer.Ordinal)
            .FirstOrDefault();
        var scheduler = profile.Reactive.Scheduler;
        var physics = profile.Physics;

        return new(
            profile.BlueprintId,
            profile.Id,
            physicalTargeted is not null || physicalResolved is not null,
            ToDraft(physicalTargeted, PhysicalAttackTargetedEvent, "scripts/placeable_attack_targeted"),
            ToDraft(physicalResolved, PhysicalAttackResolvedEvent, "scripts/placeable_attack_resolved"),
            abilityTargeted is not null || abilityImpacted is not null,
            ToDraft(abilityTargeted, AbilityTargetedEvent, "scripts/placeable_ability_targeted"),
            ToDraft(abilityImpacted, AbilityImpactedEvent, "scripts/placeable_ability_impacted"),
            scheduler.Enabled || heartbeat is not null || custom is not null,
            ToDraft(heartbeat, HeartbeatEvent, "scripts/placeable_heartbeat"),
            scheduler.IntervalSeconds > 0 ? scheduler.IntervalSeconds : 5,
            scheduler.BudgetPerMinute > 0 ? scheduler.BudgetPerMinute : 12,
            scheduler.SuspendWhenUnobserved,
            string.IsNullOrWhiteSpace(scheduler.CustomNamespace) ? "placeable.studio" : scheduler.CustomNamespace,
            ToDraft(custom, custom?.Event ?? "custom:placeable:pulse", "scripts/placeable_custom"),
            custom?.Event,
            physics is not null,
            physics?.Mode ?? PlaceableWorldProfile.BodyMode.Rigid,
            physics?.Mass ?? 10,
            string.Join(", ", physics?.Constraints ?? ["lock-y"]),
            physics?.Replicated ?? true,
            physics?.SleepAfterSeconds ?? 4,
            physics?.ConvertToStaticWhenSleeping ?? true,
            physics?.Authority ?? "server",
            existingProfile,
            profile);
    }

    internal StudioUx1ZPathPreview PreviewUx1ZPhysicalAttack(StudioPlaceableReactivePhysicsDraft draft)
    {
        if (!draft.PhysicalAttackEnabled)
        {
            return FailedUx1ZPath("Activez la réaction aux attaques physiques avant de la prévisualiser.");
        }
        if (!TryPreviewUx1ZCandidate(draft, out var profile, out var message, out var code))
        {
            return FailedUx1ZPath(message, code);
        }

        var targeted = WorldFeatureRuntimeResolver.ResolvePlaceableEvent(profile!, PhysicalAttackTargetedEvent, 0, 0);
        var resolved = WorldFeatureRuntimeResolver.ResolvePlaceableEvent(profile!, PhysicalAttackResolvedEvent, 0, 0);
        if (!targeted.Dispatched || !resolved.Dispatched)
        {
            return FailedUx1ZPath("Les deux étapes de l’attaque doivent disposer d’un script valide.");
        }
        return new(true,
            $"Aperçu sans écriture · attaque ciblée puis résolue · scripts {targeted.ScriptId} et {resolved.ScriptId} · " +
            $"autorité {(draft.PhysicalAttackResolved.Authoritative ? "serveur" : "locale")}",
            string.Empty);
    }

    internal StudioUx1ZPathPreview PreviewUx1ZSpellCast(StudioPlaceableReactivePhysicsDraft draft)
    {
        if (!draft.SpellCastEnabled)
        {
            return FailedUx1ZPath("Activez la réaction aux capacités avant de la prévisualiser.");
        }
        if (!TryPreviewUx1ZCandidate(draft, out var profile, out var message, out var code))
        {
            return FailedUx1ZPath(message, code);
        }

        var targeted = WorldFeatureRuntimeResolver.ResolvePlaceableEvent(profile!, AbilityTargetedEvent, 0, 0);
        var impacted = WorldFeatureRuntimeResolver.ResolvePlaceableEvent(profile!, AbilityImpactedEvent, 0, 0);
        if (!targeted.Dispatched || !impacted.Dispatched)
        {
            return FailedUx1ZPath("Le ciblage et l’impact de capacité doivent disposer d’un script valide.");
        }
        return new(true,
            $"Aperçu sans écriture · capacité ciblée puis impact appliqué · scripts {targeted.ScriptId} et {impacted.ScriptId} · " +
            $"autorité {(draft.AbilityImpacted.Authoritative ? "serveur" : "locale")}",
            string.Empty);
    }

    internal StudioUx1ZPathPreview PreviewUx1ZHeartbeatAndCustom(StudioPlaceableReactivePhysicsDraft draft)
    {
        if (!draft.SchedulerEnabled)
        {
            return FailedUx1ZPath("Activez le rythme et l’événement personnalisé avant de les prévisualiser.");
        }
        if (!TryPreviewUx1ZCandidate(draft, out var profile, out var message, out var code))
        {
            return FailedUx1ZPath(message, code);
        }

        var heartbeat = WorldFeatureRuntimeResolver.ResolvePlaceableEvent(
            profile!, HeartbeatEvent, 0, draft.HeartbeatIntervalSeconds);
        var cadenceBlocked = !WorldFeatureRuntimeResolver.ResolvePlaceableEvent(
            profile!, HeartbeatEvent, 0, Math.Max(0, draft.HeartbeatIntervalSeconds - .01)).Dispatched;
        var budgetBlocked = !WorldFeatureRuntimeResolver.ResolvePlaceableEvent(
            profile!, HeartbeatEvent, draft.HeartbeatBudgetPerMinute, draft.HeartbeatIntervalSeconds).Dispatched;
        var custom = WorldFeatureRuntimeResolver.ResolvePlaceableEvent(profile!, draft.CustomEvent.EventName, 0, 0);
        if (!heartbeat.Dispatched || !custom.Dispatched || !cadenceBlocked || !budgetBlocked)
        {
            return FailedUx1ZPath("Le rythme, son budget et l’événement personnalisé ne forment pas un contrat borné.");
        }
        return new(true,
            $"Aperçu sans écriture · rythme toutes les {draft.HeartbeatIntervalSeconds:0.##} s · " +
            $"budget {draft.HeartbeatBudgetPerMinute}/min · " +
            $"{(draft.SuspendWhenUnobserved ? "suspension hors observation" : "rythme maintenu hors observation")} · " +
            $"événement personnalisé {draft.CustomEvent.EventName}",
            string.Empty);
    }

    internal StudioUx1ZPathPreview PreviewUx1ZPhysics(StudioPlaceableReactivePhysicsDraft draft)
    {
        if (!draft.PhysicsEnabled)
        {
            return FailedUx1ZPath("Activez la politique physique avant de la prévisualiser.");
        }
        if (!TryPreviewUx1ZCandidate(draft, out var profile, out var message, out var code))
        {
            return FailedUx1ZPath(message, code);
        }
        if (profile?.Physics is not { } physics)
        {
            return FailedUx1ZPath("La politique physique canonique est absente.");
        }

        return new(true,
            $"Aperçu de politique sans simulation · corps {HumanUx1ZBodyMode(physics.Mode)} · masse {physics.Mass:0.##} kg · " +
            $"{physics.Constraints.Count} contrainte(s) · " +
            $"{(physics.Replicated ? "réplication demandée" : "sans réplication")} · " +
            $"sommeil après {physics.SleepAfterSeconds:0.##} s · " +
            $"{(physics.ConvertToStaticWhenSleeping ? "conversion statique après sommeil" : "corps conservé après sommeil")} · " +
            $"autorité {HumanUx1ZAuthority(physics.Authority)} · politique uniquement, sans moteur ni réseau exécuté",
            string.Empty);
    }

    internal StudioUx1ZPreview PreviewUx1Z(StudioPlaceableReactivePhysicsDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelUx1ZPreview();
        if (!TryPreviewUx1ZCandidate(draft, out _, out var message, out var code))
        {
            return FailedUx1ZPreview(message, code);
        }

        var physical = draft.PhysicalAttackEnabled
            ? PreviewUx1ZPhysicalAttack(draft)
            : DisabledUx1ZPath("Réaction aux attaques désactivée");
        var spell = draft.SpellCastEnabled
            ? PreviewUx1ZSpellCast(draft)
            : DisabledUx1ZPath("Réaction aux capacités désactivée");
        var heartbeat = draft.SchedulerEnabled
            ? PreviewUx1ZHeartbeatAndCustom(draft)
            : DisabledUx1ZPath("Rythme et événement personnalisé désactivés");
        var physics = draft.PhysicsEnabled
            ? PreviewUx1ZPhysics(draft)
            : DisabledUx1ZPath("Politique physique désactivée");
        if (new[] { physical, spell, heartbeat, physics }.Any(path => !path.Succeeded))
        {
            var failed = new[] { physical, spell, heartbeat, physics }.First(path => !path.Succeeded);
            return FailedUx1ZPreview(failed.Summary, failed.DiagnosticCode);
        }

        _approvedUx1Z = draft;
        _approvedUx1ZRevision = History?.State.Revision;
        var activePaths = new[]
        {
            draft.PhysicalAttackEnabled,
            draft.SpellCastEnabled,
            draft.SchedulerEnabled,
            draft.PhysicsEnabled,
        }.Count(enabled => enabled);
        var summary = string.Join('\n', new[]
        {
            $"{activePaths} parcours actif(s) prévisualisé(s) sans modifier le module.",
            physical.Summary,
            spell.Summary,
            heartbeat.Summary,
            physics.Summary,
            "Application transactionnelle prête · les autres capacités du placeable restent intactes.",
        });
        return new(true, "Aperçu validé · aucun document n’a été modifié.", summary, true,
            physical, spell, heartbeat, physics);
    }

    internal StudioUx1ZActionResult ApplyApprovedUx1Z()
    {
        if (_approvedUx1Z is null)
        {
            return new(false, "Prévisualisez les événements et la politique physique avant de les appliquer.");
        }
        if (History is null || _approvedUx1ZRevision is null ||
            History.State.Revision != _approvedUx1ZRevision.Value)
        {
            CancelUx1ZPreview();
            return new(false,
                "Cet aperçu est périmé car le module a changé. Prévisualisez à nouveau avant d’appliquer.");
        }
        if (!TryBuildUx1Z(_approvedUx1Z, false, out var command, out _, out var message))
        {
            CancelUx1ZPreview();
            return new(false, message);
        }

        var result = _setPlaceableCatalog.Handle(History!.State, command!);
        CancelUx1ZPreview();
        if (!result.Succeeded)
        {
            var diagnostic = result.Diagnostics.FirstOrDefault();
            return new(false, HumanizeUx1ZDiagnostic(diagnostic?.Code));
        }
        History.Record(result);
        InvalidateContentIndexes();
        return new(true, "Événements et politique physique appliqués · enregistrez pour écrire le module.");
    }

    internal void CancelUx1ZPreview()
    {
        _approvedUx1Z = null;
        _approvedUx1ZRevision = null;
    }

    internal StudioUx1ZSmokeResult RunUx1ZContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux1z-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var blueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1e30000-0000-4000-8000-000000000001")),
                "reliquaire-reactif",
                "Reliquaire réactif",
                BlueprintKind.Placeable);
            var neighborBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1e30000-0000-4000-8000-000000000002")),
                "borne-preservee",
                "Borne préservée",
                BlueprintKind.Placeable);
            var unconfiguredBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1e30000-0000-4000-8000-000000000012")),
                "statue-a-configurer",
                "Statue à configurer",
                BlueprintKind.Placeable);
            var entity = new EntityInstance(
                EntityId.From(Guid.Parse("c1e30000-0000-4000-8000-000000000003")),
                blueprint.Id,
                new TransformValue(Vector3Value.Zero, QuaternionValue.Identity, Vector3Value.One));
            var unconfiguredEntity = new EntityInstance(
                EntityId.From(Guid.Parse("c1e30000-0000-4000-8000-000000000013")),
                unconfiguredBlueprint.Id,
                new TransformValue(new(4, 0, 0), QuaternionValue.Identity, Vector3Value.One),
                overrides: new EntityPropertyOverrides(
                    EntityPropertyOverrides.CurrentSchemaVersion,
                    [new("visual.asset", EntityPropertyValueKind.String, "asset:statue-a-configurer")]));
            var area = new AreaDefinition(
                AreaId.From(Guid.Parse("c1e30000-0000-4000-8000-000000000004")),
                "atelier-reactif",
                "Atelier réactif",
                [entity, unconfiguredEntity],
                AreaKind.Interior,
                new AreaDimensions(32, 32));
            var dialogue = Dialogue(
                "c1e30000-0000-4000-8000-000000000005",
                "c1e30000-0000-4000-8000-000000000006",
                "voix-reliquaire",
                "Voix du reliquaire",
                "Le reliquaire répond.",
                ["default"]);
            var project = new ProjectDefinition(
                ProjectId.From(Guid.Parse("c1e30000-0000-4000-8000-000000000007")),
                "Studio UX1-Z",
                [blueprint, neighborBlueprint, unconfiguredBlueprint],
                [area],
                [dialogue],
                []);

            var faction = new FactionDefinition(
                Guid.Parse("c1e30000-0000-4000-8000-000000000008"),
                1,
                "test:ux1-z",
                "veilleurs",
                "Veilleurs",
                false,
                [],
                ["guardian"]);
            var factions = new FactionCatalog(
                [faction],
                [],
                [new("hostile", -100, -1, 1), new("neutral", 0, 49, 1), new("allied", 50, 100, 1)],
                []);

            var original = CreatePlaceableTransformParent(
                blueprint,
                Guid.Parse("c1e30000-0000-4000-8000-000000000010"),
                "test:ux1-z-profile");
            var foundation = original.Foundation! with
            {
                Components = original.Foundation.Components with { HasHealth = true, HasPhysics = true },
                Validation = new([], [], [], [], ["physics:reliquaire"], 1, 0),
            };
            var operations = new PlaceableOperationsProfile(
                StablePlaceableId(blueprint.Id, "ux1-z-operations"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-z-operations",
                null,
                CreateContainerCompatibilityStateMachine(blueprint.Id),
                new(
                    StablePlaceableId(blueprint.Id, "ux1-z-damageable"),
                    100,
                    100,
                    5,
                    new Dictionary<string, double> { ["physical"] = .1 },
                    [.75, .5, .25],
                    "vfx:reliquaire-destruction",
                    "debris:reliquaire",
                    true,
                    10),
                CreateContainerCompatibilityProtection(blueprint.Id),
                null);
            var security = new PlaceableSecurityEventsProfile(
                StablePlaceableId(blueprint.Id, "ux1-z-security"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-z-security");
            var compositionDamage = new PlaceableDamageEventsProfile(
                StablePlaceableId(blueprint.Id, "ux1-z-damage-events"),
                "1.1.0",
                "ObjetEndommage",
                "ObjetDetruit",
                true,
                true,
                true,
                true,
                true,
                true);
            var composition = new PlaceableCompositionEventsProfile(
                StablePlaceableId(blueprint.Id, "ux1-z-composition"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-z-composition",
                compositionDamage,
                [new(
                    StablePlaceableId(blueprint.Id, "ux1-z-socket"),
                    "ancrage",
                    "prop",
                    new(Vector3Value.Zero, Vector3Value.Zero, Vector3Value.One, Vector3Value.Zero, .1, null,
                        false, null),
                    ["prop"],
                    true,
                    1)],
                [],
                new(
                    StablePlaceableId(blueprint.Id, "ux1-z-multi-verb"),
                    new[] { "inspect", "use", "open", "talk", "take", "push", "climb", "attune" }
                        .Select(verb => new PlaceableContextualVerbProfile(
                            StablePlaceableId(blueprint.Id, $"ux1-z-verb-{verb}"),
                            verb,
                            $"placeable.{verb}",
                            1,
                            [],
                            [],
                            $"{verb}Requested"))
                        .ToArray()));
            var ownership = new PlaceableFactionOwnershipProfile(
                StablePlaceableId(blueprint.Id, "ux1-z-faction"),
                PlaceableFactionOwnershipProfile.CurrentSchemaVersion,
                "test:ux1-z-faction",
                faction.Id,
                [faction.Id],
                true,
                true,
                false,
                true);
            var conversation = new PlaceableConversationProfile(
                StablePlaceableId(blueprint.Id, "ux1-z-conversation"),
                PlaceableConversationProfile.CurrentSchemaVersion,
                "test:ux1-z-conversation",
                [new(
                    StablePlaceableId(blueprint.Id, "ux1-z-conversation-binding"),
                    dialogue.Id.Value,
                    PlaceableConversationTrigger.Interaction,
                    "examiner",
                    null,
                    "default",
                    10,
                    false)]);
            original = original with
            {
                Foundation = foundation,
                Operations = operations,
                SecurityEvents = security,
                CompositionEvents = composition,
                FactionOwnership = ownership,
                Conversation = conversation,
                Reactive = new(
                    [new("neighbor-event", "scripts/neighbor", false, true)],
                    new(false, 5, 12, true, "placeable.original")),
            };
            var neighbor = CreatePlaceableTransformParent(
                neighborBlueprint,
                Guid.Parse("c1e30000-0000-4000-8000-000000000011"),
                "test:ux1-z-neighbor");
            var catalog = new WorldFeatureCatalog(
                project.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-z-catalog",
                placeables: [original, neighbor]);
            var module = project.Module.WithFactions(factions).WithWorldFeatures(catalog);
            project = new ProjectDefinition(project.Id, project.Title, module, project.Blueprints, project.Areas,
                project.Dialogues, project.Quests);

            var created = ProjectSession.Create(tempRoot, project);
            if (!created.Succeeded)
            {
                return FailedUx1ZSmoke("PROJECT_CREATE", created.Diagnostics.Select(item => item.Code));
            }
            var opened = editing.Open(tempRoot);
            if (!opened.Succeeded)
            {
                return FailedUx1ZSmoke("PROJECT_OPEN", opened.Diagnostics.Select(item => item.Code));
            }

            var originalProject = editing.Project!;
            var loadedOriginal = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var loadedNeighbor = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == neighborBlueprint.Id);
            var explicitSelectionRequired = editing.CreateUx1ZDraft() is null &&
                                            editing.CreateUx1ZDraft(BlueprintId.From(
                                                Guid.Parse("c1e30000-0000-4000-8000-000000000099"))) is null;
            var safeSource = editing.CreateUx1ZDraft(unconfiguredBlueprint.Id);
            var safePreview = safeSource is null ? null : editing.PreviewUx1Z(safeSource);
            var safeProfileCreation = safeSource is not null && safeSource.ExistingProfile == false &&
                                      safePreview?.Succeeded == true &&
                                      editing.TryBuildUx1Z(safeSource, true, out _, out var safeProfile, out _) &&
                                      safeProfile?.Foundation is not null &&
                                      ReferenceEquals(originalProject, editing.Project);
            editing.CancelUx1ZPreview();
            var source = editing.CreateUx1ZDraft(blueprint.Id)!;
            var draft = source with
            {
                PhysicalAttackEnabled = true,
                PhysicalAttackTargeted = source.PhysicalAttackTargeted with
                {
                    ScriptId = "scripts/reliquaire_attack_targeted",
                    Before = true,
                    Authoritative = true,
                },
                PhysicalAttackResolved = source.PhysicalAttackResolved with
                {
                    ScriptId = "scripts/reliquaire_attack_resolved",
                    Before = false,
                    Authoritative = true,
                },
                SpellCastEnabled = true,
                AbilityTargeted = source.AbilityTargeted with
                {
                    ScriptId = "scripts/reliquaire_ability_targeted",
                    Before = true,
                    Authoritative = true,
                },
                AbilityImpacted = source.AbilityImpacted with
                {
                    ScriptId = "scripts/reliquaire_ability_impacted",
                    Before = false,
                    Authoritative = true,
                },
                SchedulerEnabled = true,
                Heartbeat = source.Heartbeat with
                {
                    ScriptId = "scripts/reliquaire_heartbeat",
                    Authoritative = true,
                },
                HeartbeatIntervalSeconds = 5,
                HeartbeatBudgetPerMinute = 12,
                SuspendWhenUnobserved = true,
                CustomNamespace = "reliquaire.veilleurs",
                CustomEvent = source.CustomEvent with
                {
                    EventName = "custom:reliquaire:pulse",
                    ScriptId = "scripts/reliquaire_pulse",
                    Authoritative = true,
                },
                PhysicsEnabled = true,
                BodyMode = PlaceableWorldProfile.BodyMode.Rigid,
                Mass = 12,
                ConstraintsText = "lock-y, lock-rotation-x",
                Replicated = true,
                SleepAfterSeconds = 4,
                ConvertToStaticWhenSleeping = true,
                Authority = "server",
            };

            var physicalPreview = editing.PreviewUx1ZPhysicalAttack(draft);
            var spellPreview = editing.PreviewUx1ZSpellCast(draft);
            var heartbeatPreview = editing.PreviewUx1ZHeartbeatAndCustom(draft);
            var physicsPreview = editing.PreviewUx1ZPhysics(draft);
            var previewsWithoutWriting = new[]
                { physicalPreview, spellPreview, heartbeatPreview, physicsPreview }.All(item => item.Succeeded) &&
                ReferenceEquals(originalProject, editing.Project);

            var invalidCadence = editing.PreviewUx1Z(draft with { HeartbeatIntervalSeconds = 0 });
            var invalidBudget = editing.PreviewUx1Z(draft with { HeartbeatBudgetPerMinute = 0 });
            var invalidNamespace = editing.PreviewUx1Z(draft with { CustomNamespace = string.Empty });
            var invalidMass = editing.PreviewUx1Z(draft with { Mass = 0 });
            var invalidCadenceBlocked = !invalidCadence.Succeeded;
            var invalidBudgetBlocked = !invalidBudget.Succeeded;
            var invalidNamespaceBlocked = !invalidNamespace.Succeeded;
            var invalidMassBlocked = !invalidMass.Succeeded;
            var humanDiagnostics = new[] { invalidCadence, invalidBudget, invalidNamespace, invalidMass }
                .All(item => IsHumanUx1ZMessage(item.Message));

            var stalePreview = editing.PreviewUx1Z(draft);
            var staleSource = editing.Project!.Module.WorldFeatures;
            var staleCatalog = new WorldFeatureCatalog(
                staleSource.Id,
                staleSource.SchemaVersion,
                "test:ux1-z-neighbor-command",
                staleSource.Placeables,
                staleSource.Doors,
                staleSource.Triggers,
                staleSource.Encounters,
                staleSource.VehicleRoutes,
                staleSource.Merchants,
                staleSource.Companions,
                staleSource.Scripts);
            var staleCommand = new SetWorldFeatureCatalogCommand(
                CommandId.From(Guid.NewGuid()),
                editing.History!.State.Revision,
                staleCatalog,
                false);
            var staleMutation = editing._setPlaceableCatalog.Handle(editing.History.State, staleCommand);
            if (staleMutation.Succeeded)
            {
                editing.History.Record(staleMutation);
            }
            var staleApply = editing.ApplyApprovedUx1Z();
            var staleUndo = editing.Undo();
            var stalePreviewBlocked = stalePreview.Succeeded && staleMutation.Succeeded &&
                                      !staleApply.Succeeded &&
                                      staleApply.Message.Contains("périmé", StringComparison.OrdinalIgnoreCase) &&
                                      staleUndo.Succeeded && ReferenceEquals(originalProject, editing.Project);

            var directBuilt = editing.TryBuildUx1Z(draft, true, out _, out var directProfile, out _);
            var preview = editing.PreviewUx1Z(draft);
            var dryRun = previewsWithoutWriting && preview.Succeeded && preview.CanApply &&
                         ReferenceEquals(originalProject, editing.Project);
            var applied = editing.ApplyApprovedUx1Z();
            var appliedProject = editing.Project!;
            var appliedProfile = appliedProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var commandParity = directBuilt && directProfile is not null &&
                                Ux1ZReactiveValuesEqual(directProfile.Reactive, appliedProfile.Reactive) &&
                                Ux1ZPhysicsValuesEqual(directProfile.Physics, appliedProfile.Physics);
            var neighborCapabilitiesPreserved = applied.Succeeded &&
                ReferenceEquals(appliedProfile.Foundation, loadedOriginal.Foundation) &&
                ReferenceEquals(appliedProfile.Operations, loadedOriginal.Operations) &&
                ReferenceEquals(appliedProfile.SecurityEvents, loadedOriginal.SecurityEvents) &&
                ReferenceEquals(appliedProfile.CompositionEvents, loadedOriginal.CompositionEvents) &&
                ReferenceEquals(appliedProfile.FactionOwnership, loadedOriginal.FactionOwnership) &&
                ReferenceEquals(appliedProfile.Conversation, loadedOriginal.Conversation) &&
                appliedProfile.Reactive.Events.Any(item => item.Event == "neighbor-event");
            var neighborPlaceablePreserved = ReferenceEquals(
                appliedProject.Module.WorldFeatures.Placeables.Single(item =>
                    item.BlueprintId == neighborBlueprint.Id), loadedNeighbor);

            var undo = editing.Undo();
            var undone = ReferenceEquals(originalProject, editing.Project);
            var redo = editing.Redo();
            var redone = ReferenceEquals(appliedProject, editing.Project);
            var undoRedo = undo.Succeeded && undone && redo.Succeeded && redone;
            var saved = editing.Save();
            editing.Close();
            var reopen = reopened.Open(tempRoot);
            var persisted = reopened.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
                item.BlueprintId == blueprint.Id);
            var persistence = saved.Succeeded && reopen.Succeeded && persisted is
            {
                Physics:
                {
                    Mode: PlaceableWorldProfile.BodyMode.Rigid,
                    Mass: 12,
                    Replicated: true,
                    Authority: "server",
                },
                Foundation: not null,
                Operations: not null,
                SecurityEvents: not null,
                CompositionEvents: not null,
                FactionOwnership: not null,
                Conversation: not null,
            } && persisted.Reactive.Scheduler is
            {
                Enabled: true,
                IntervalSeconds: 5,
                BudgetPerMinute: 12,
                CustomNamespace: "reliquaire.veilleurs",
            } && new[]
            {
                PhysicalAttackTargetedEvent,
                PhysicalAttackResolvedEvent,
                AbilityTargetedEvent,
                AbilityImpactedEvent,
                HeartbeatEvent,
                "custom:reliquaire:pulse",
                "neighbor-event",
            }.All(name => persisted.Reactive.Events.Any(item => item.Event == name)) &&
                reopened.Project?.Module.WorldFeatures.Placeables.Any(item =>
                    item.BlueprintId == neighborBlueprint.Id) == true;

            var succeeded = physicalPreview.Succeeded && spellPreview.Succeeded && heartbeatPreview.Succeeded &&
                            physicsPreview.Succeeded && dryRun && commandParity && invalidCadenceBlocked &&
                            invalidBudgetBlocked && invalidNamespaceBlocked && invalidMassBlocked &&
                            stalePreviewBlocked && explicitSelectionRequired && safeProfileCreation &&
                            neighborCapabilitiesPreserved && neighborPlaceablePreserved && undoRedo && persistence &&
                            humanDiagnostics;
            return new(
                succeeded,
                physicalPreview.Succeeded,
                spellPreview.Succeeded,
                heartbeatPreview.Succeeded,
                physicsPreview.Succeeded,
                dryRun,
                commandParity,
                invalidCadenceBlocked,
                invalidBudgetBlocked,
                invalidNamespaceBlocked,
                invalidMassBlocked,
                stalePreviewBlocked,
                explicitSelectionRequired,
                safeProfileCreation,
                neighborCapabilitiesPreserved,
                neighborPlaceablePreserved,
                undoRedo,
                persistence,
                humanDiagnostics);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1Z_CONTRACT_EXCEPTION type={exception.GetType().Name} message={exception.Message}");
            return FailedUx1ZSmoke();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }
    }

    private bool TryPreviewUx1ZCandidate(
        StudioPlaceableReactivePhysicsDraft draft,
        out PlaceableWorldProfile? profile,
        out string message,
        out string diagnosticCode)
    {
        profile = null;
        diagnosticCode = string.Empty;
        if (!TryBuildUx1Z(draft, true, out var command, out profile, out message))
        {
            return false;
        }
        var result = _setPlaceableCatalog.Handle(History!.State, command!);
        if (result.Succeeded)
        {
            return true;
        }
        var diagnostic = result.Diagnostics.FirstOrDefault();
        diagnosticCode = diagnostic?.Code ?? "world.placeable.reactive_physics_preview_failed";
        message = HumanizeUx1ZDiagnostic(diagnosticCode);
        return false;
    }

    private bool TryBuildUx1Z(
        StudioPlaceableReactivePhysicsDraft draft,
        bool dryRun,
        out SetWorldFeatureCatalogCommand? command,
        out PlaceableWorldProfile? profile,
        out string message)
    {
        command = null;
        profile = null;
        message = string.Empty;
        if (History is null || Project is null)
        {
            message = "Aucun module n’est ouvert.";
            return false;
        }
        var catalog = Project.Module.WorldFeatures;
        var current = catalog.Placeables.FirstOrDefault(candidate =>
            candidate.BlueprintId == draft.BlueprintId && candidate.Id == draft.ProfileId);
        if (draft.ExistingProfile && current is null)
        {
            message = "Le placeable choisi n’existe plus dans le module.";
            return false;
        }
        if (draft.ExistingProfile && !ReferenceEquals(current, draft.OriginalProfile))
        {
            message = "Ce placeable a changé depuis l’ouverture de l’éditeur. Rechargez sa configuration avant de continuer.";
            return false;
        }
        if (!draft.ExistingProfile && current is not null)
        {
            message = "Une fiche a été créée pour ce placeable depuis l’ouverture. Rechargez sa configuration avant de continuer.";
            return false;
        }
        var sourceProfile = current ?? draft.OriginalProfile;
        if (!ValidUx1ZPair(draft.PhysicalAttackEnabled,
                draft.PhysicalAttackTargeted, draft.PhysicalAttackResolved, out message) ||
            !ValidUx1ZPair(draft.SpellCastEnabled,
                draft.AbilityTargeted, draft.AbilityImpacted, out message))
        {
            return false;
        }
        if (draft.SchedulerEnabled)
        {
            if (!ValidUx1ZEvent(draft.Heartbeat) || !ValidUx1ZEvent(draft.CustomEvent))
            {
                message = "Le rythme et l’événement personnalisé exigent chacun un script valide.";
                return false;
            }
            if (!double.IsFinite(draft.HeartbeatIntervalSeconds) || draft.HeartbeatIntervalSeconds <= 0)
            {
                message = "La cadence doit être un nombre strictement positif.";
                return false;
            }
            if (draft.HeartbeatBudgetPerMinute < 1)
            {
                message = "Le budget doit autoriser au moins un déclenchement par minute.";
                return false;
            }
            if (!ValidUx1ZNamespace(draft.CustomNamespace))
            {
                message = "Donnez un espace de noms court au planificateur, sans espace ni caractère spécial.";
                return false;
            }
            if (!ValidUx1ZCustomEvent(draft.CustomEvent.EventName))
            {
                message = "Le nom personnalisé doit suivre la forme custom:domaine:événement.";
                return false;
            }
        }

        var constraints = SplitValues(draft.ConstraintsText);
        if (draft.PhysicsEnabled)
        {
            if (!double.IsFinite(draft.Mass) || draft.Mass <= 0)
            {
                message = "La masse physique doit être un nombre strictement positif.";
                return false;
            }
            if (!double.IsFinite(draft.SleepAfterSeconds) || draft.SleepAfterSeconds < 0)
            {
                message = "Le délai de sommeil doit être positif ou nul.";
                return false;
            }
            if (constraints.Count == 0)
            {
                message = "Ajoutez au moins une contrainte physique explicite.";
                return false;
            }
            if (!ValidUx1ZAuthority(draft.Authority))
            {
                message = "Choisissez une autorité serveur ou locale.";
                return false;
            }
            if (draft.Replicated && !string.Equals(draft.Authority, "server", StringComparison.Ordinal))
            {
                message = "Une politique répliquée doit rester sous autorité serveur.";
                return false;
            }
        }

        var managedEvents = new HashSet<string>(StringComparer.Ordinal)
        {
            PhysicalAttackTargetedEvent,
            PhysicalAttackResolvedEvent,
            AbilityTargetedEvent,
            AbilityImpactedEvent,
            HeartbeatEvent,
        };
        if (!string.IsNullOrWhiteSpace(draft.OriginalCustomEventName))
        {
            managedEvents.Add(draft.OriginalCustomEventName);
        }
        var events = sourceProfile.Reactive.Events
            .Where(binding => !managedEvents.Contains(binding.Event))
            .ToList();
        if (draft.PhysicalAttackEnabled)
        {
            events.Add(ToBinding(draft.PhysicalAttackTargeted, PhysicalAttackTargetedEvent));
            events.Add(ToBinding(draft.PhysicalAttackResolved, PhysicalAttackResolvedEvent));
        }
        if (draft.SpellCastEnabled)
        {
            events.Add(ToBinding(draft.AbilityTargeted, AbilityTargetedEvent));
            events.Add(ToBinding(draft.AbilityImpacted, AbilityImpactedEvent));
        }
        if (draft.SchedulerEnabled)
        {
            events.Add(ToBinding(draft.Heartbeat, HeartbeatEvent));
            events.Add(ToBinding(draft.CustomEvent, draft.CustomEvent.EventName.Trim()));
        }
        if (events.Select(binding => binding.Event).Distinct(StringComparer.Ordinal).Count() != events.Count)
        {
            message = "Un événement du même nom existe déjà sur ce placeable.";
            return false;
        }

        var scheduler = new PlaceableWorldProfile.SchedulerPolicy(
            draft.SchedulerEnabled,
            draft.SchedulerEnabled ? draft.HeartbeatIntervalSeconds : 1,
            draft.SchedulerEnabled ? draft.HeartbeatBudgetPerMinute : 1,
            draft.SuspendWhenUnobserved,
            draft.SchedulerEnabled ? draft.CustomNamespace.Trim() : "placeable.disabled");
        var reactive = new PlaceableWorldProfile.ReactivePolicy(
            events.OrderBy(binding => binding.Event, StringComparer.Ordinal).ToArray(),
            scheduler);
        var physics = draft.PhysicsEnabled
            ? new PlaceableWorldProfile.DynamicPhysicsPolicy(
                draft.BodyMode,
                draft.Mass,
                constraints,
                draft.Replicated,
                draft.SleepAfterSeconds,
                draft.ConvertToStaticWhenSleeping,
                draft.Authority.Trim())
            : null;
        profile = sourceProfile with { Reactive = reactive, Physics = physics };
        var placeables = catalog.Placeables
            .Where(candidate => candidate.Id != sourceProfile.Id)
            .Append(profile)
            .ToArray();
        var candidateCatalog = new WorldFeatureCatalog(
            catalog.Id,
            catalog.SchemaVersion,
            catalog.Provenance,
            placeables,
            catalog.Doors,
            catalog.Triggers,
            catalog.Encounters,
            catalog.VehicleRoutes,
            catalog.Merchants,
            catalog.Companions,
            catalog.Scripts);
        command = new SetWorldFeatureCatalogCommand(
            CommandId.From(Guid.NewGuid()),
            History.State.Revision,
            candidateCatalog,
            dryRun);
        return true;
    }

    private static PlaceableWorldProfile.EventBinding? FindEvent(
        IReadOnlyList<PlaceableWorldProfile.EventBinding> events,
        string name) => events.FirstOrDefault(candidate => string.Equals(candidate.Event, name,
        StringComparison.Ordinal));

    private static StudioUx1ZEventDraft ToDraft(
        PlaceableWorldProfile.EventBinding? source,
        string eventName,
        string fallbackScript) => new(
        source?.Event ?? eventName,
        source?.ScriptId ?? fallbackScript,
        source?.Before ?? false,
        source?.Authoritative ?? true);

    private static PlaceableWorldProfile.EventBinding ToBinding(StudioUx1ZEventDraft source, string eventName) =>
        new(eventName, source.ScriptId.Trim(), source.Before, source.Authoritative);

    private static bool ValidUx1ZPair(
        bool enabled,
        StudioUx1ZEventDraft first,
        StudioUx1ZEventDraft second,
        out string message)
    {
        if (!enabled || ValidUx1ZEvent(first) && ValidUx1ZEvent(second))
        {
            message = string.Empty;
            return true;
        }
        message = "Chaque étape activée exige un script court et non vide.";
        return false;
    }

    private static bool ValidUx1ZEvent(StudioUx1ZEventDraft value) =>
        !string.IsNullOrWhiteSpace(value.ScriptId) && value.ScriptId.Trim().Length <= 256;

    private static bool ValidUx1ZNamespace(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 96 &&
        value.Trim().All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.');

    private static bool ValidUx1ZCustomEvent(string value)
    {
        var segments = value.Trim().Split(':', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 3 && string.Equals(segments[0], "custom", StringComparison.Ordinal) &&
               segments.Skip(1).All(segment => segment.Length <= 64 &&
                   segment.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.'));
    }

    private static bool ValidUx1ZAuthority(string value) =>
        string.Equals(value, "server", StringComparison.Ordinal) ||
        string.Equals(value, "local", StringComparison.Ordinal);

    private static bool Ux1ZReactiveValuesEqual(
        PlaceableWorldProfile.ReactivePolicy left,
        PlaceableWorldProfile.ReactivePolicy right) =>
        left.Events.SequenceEqual(right.Events) && left.Scheduler == right.Scheduler;

    private static bool Ux1ZPhysicsValuesEqual(
        PlaceableWorldProfile.DynamicPhysicsPolicy? left,
        PlaceableWorldProfile.DynamicPhysicsPolicy? right) =>
        left is null && right is null || left is not null && right is not null &&
        left.Mode == right.Mode && left.Mass == right.Mass &&
        left.Constraints.SequenceEqual(right.Constraints, StringComparer.Ordinal) &&
        left.Replicated == right.Replicated && left.SleepAfterSeconds == right.SleepAfterSeconds &&
        left.ConvertToStaticWhenSleeping == right.ConvertToStaticWhenSleeping &&
        string.Equals(left.Authority, right.Authority, StringComparison.Ordinal);

    private static StudioUx1ZPathPreview FailedUx1ZPath(string message, string code = "") =>
        new(false, message, code);

    private static StudioUx1ZPathPreview DisabledUx1ZPath(string message) => new(true, message, string.Empty);

    private static StudioUx1ZPreview FailedUx1ZPreview(string message, string code) => new(
        false,
        message,
        message,
        false,
        FailedUx1ZPath(message, code),
        FailedUx1ZPath(message, code),
        FailedUx1ZPath(message, code),
        FailedUx1ZPath(message, code));

    private static string HumanizeUx1ZDiagnostic(string? code) => code switch
    {
        "world.placeable.scheduler_invalid" =>
            "Le rythme exige une cadence, un budget et un espace de noms valides.",
        "world.placeable.physics_invalid" =>
            "La politique physique exige une masse, un délai de sommeil et une autorité valides.",
        "world.event.binding_invalid" =>
            "Chaque événement exige un nom et un script valides.",
        null or "" => "La prévisualisation n’a pas pu être validée.",
        _ => "La configuration contient une valeur invalide. Corrigez la section signalée puis réessayez.",
    };

    private static string HumanUx1ZBodyMode(PlaceableWorldProfile.BodyMode mode) => mode switch
    {
        PlaceableWorldProfile.BodyMode.Static => "statique",
        PlaceableWorldProfile.BodyMode.Rigid => "rigide",
        PlaceableWorldProfile.BodyMode.Kinematic => "cinématique",
        _ => "configuré",
    };

    private static string HumanUx1ZAuthority(string authority) =>
        string.Equals(authority, "server", StringComparison.Ordinal) ? "serveur" : "locale";

    private static bool IsHumanUx1ZMessage(string message) =>
        !string.IsNullOrWhiteSpace(message) &&
        !message.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains(".json", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains('\\');

    private static StudioUx1ZSmokeResult FailedUx1ZSmoke(
        string? stage = null,
        IEnumerable<string>? diagnostics = null)
    {
        if (stage is not null)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX1Z_{stage}_FAILED diagnostics=" +
                              string.Join(" | ", diagnostics ?? []));
        }
        return new(false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false);
    }
}
