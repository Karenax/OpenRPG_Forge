using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Assets;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Items;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly SetWorldFeatureCatalogHandler _setPlaceableCatalog = new(new SystemClock());
    private StudioPlaceableDraft? _approvedPlaceable;

    internal StudioPlaceableCatalog CreatePlaceableCatalog()
    {
        var project = Project ?? throw new InvalidOperationException("Aucun module n’est ouvert.");
        var visualUsage = project.Areas
            .SelectMany(area => area.Entities)
            .Select(entity => entity.Overrides.Properties.FirstOrDefault(property =>
                string.Equals(property.Key, "visual.asset", StringComparison.Ordinal))?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var blueprints = project.Blueprints
            .Where(blueprint => blueprint.Kind == BlueprintKind.Placeable)
            .OrderBy(blueprint => blueprint.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(blueprint => new StudioPlaceableBlueprintOption(
                blueprint.Id,
                blueprint.DisplayName,
                blueprint.Slug,
                project.Areas.Sum(area => area.Entities.Count(entity => entity.BlueprintId == blueprint.Id)),
                project.Module.WorldFeatures.Placeables.Any(profile => profile.BlueprintId == blueprint.Id)))
            .ToArray();
        var visuals = AssetManifests
            .Where(manifest => manifest.Chunks.Any(chunk => chunk.Role == AssetChunkRole.Model))
            .Select(manifest => new StudioPlaceableVisualOption(
                manifest.AssetId.ToString(),
                HumanizeAssetManifest(manifest),
                manifest.LicenseSpdx,
                visualUsage.GetValueOrDefault(manifest.AssetId.ToString())))
            .OrderByDescending(option => option.UsageCount)
            .ThenBy(option => option.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        var parents = project.Module.WorldFeatures.Placeables
            .Where(profile => profile.Foundation is not null)
            .Select(profile => new StudioPlaceableParentOption(
                profile.Id,
                profile.BlueprintId,
                project.Blueprints.First(blueprint => blueprint.Id == profile.BlueprintId).DisplayName))
            .OrderBy(option => option.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        var factions = project.Module.Factions.Factions
            .OrderBy(option => option.Archived)
            .ThenBy(option => option.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(option => new StudioPlaceableFactionOption(
                option.Id,
                option.DisplayName,
                option.Slug,
                option.Archived))
            .ToArray();
        var dialogues = project.Dialogues
            .OrderBy(dialogue => dialogue.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(dialogue => new StudioPlaceableDialogueOption(
                dialogue.Id.Value,
                dialogue.DisplayName,
                dialogue.Slug,
                dialogue.EntryPoints
                    .OrderBy(entry => entry.Order)
                    .ThenBy(entry => entry.Key, StringComparer.Ordinal)
                    .Select(entry => entry.Key)
                    .ToArray()))
            .ToArray();
        var items = project.Blueprints
            .Where(item => item.Kind == BlueprintKind.Item)
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => new StudioPlaceableItemOption(item.Id, item.DisplayName, item.Slug))
            .ToArray();
        return new StudioPlaceableCatalog(blueprints, visuals, parents, factions, dialogues, items);
    }

    internal StudioPlaceableDraft? CreatePlaceableDraft(BlueprintId? requestedBlueprintId = null)
    {
        var project = Project;
        if (project is null)
        {
            return null;
        }
        var blueprint = project.Blueprints
            .Where(candidate => candidate.Kind == BlueprintKind.Placeable)
            .OrderBy(candidate => candidate.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .FirstOrDefault(candidate => requestedBlueprintId is not null && candidate.Id == requestedBlueprintId) ??
            project.Blueprints
                .Where(candidate => candidate.Kind == BlueprintKind.Placeable)
                .OrderBy(candidate => candidate.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .FirstOrDefault();
        if (blueprint is null)
        {
            return null;
        }

        var profile = project.Module.WorldFeatures.Placeables.FirstOrDefault(candidate =>
            candidate.BlueprintId == blueprint.Id);
        var foundation = profile?.Foundation;
        var text = foundation?.Texts.FirstOrDefault(candidate =>
                       string.Equals(candidate.Locale, "fr-FR", StringComparison.OrdinalIgnoreCase)) ??
                   foundation?.Texts.FirstOrDefault();
        var additionalLocalizedTexts = foundation?.Texts
            .Where(candidate => !string.Equals(candidate.Locale, "fr-FR", StringComparison.OrdinalIgnoreCase))
            .Select(candidate => new StudioPlaceableLocalizedTextDraft(
                candidate.Locale,
                candidate.Name,
                candidate.Prompt))
            .ToArray() ?? [];
        var mostUsedVisual = project.Areas
            .SelectMany(area => area.Entities)
            .Where(entity => entity.BlueprintId == blueprint.Id)
            .Select(entity => entity.Overrides.Properties.FirstOrDefault(property =>
                string.Equals(property.Key, "visual.asset", StringComparison.Ordinal))?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value!, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key)
            .FirstOrDefault();
        var catalog = CreatePlaceableCatalog();
        var visualAssetId = foundation?.Visual.AssetId ?? mostUsedVisual ?? catalog.Visuals.FirstOrDefault()?.AssetId ??
            string.Empty;
        var appearance = foundation?.Visual is { } authoredVisual
            ? new StudioPlaceableAppearanceDraft(
                string.Join(", ", authoredVisual.MeshIds),
                string.Join(", ", authoredVisual.MaterialIds),
                authoredVisual.IconAssetId,
                authoredVisual.PortraitAssetId,
                string.Join(", ", authoredVisual.VisualEffectIds))
            : new StudioPlaceableAppearanceDraft(
                string.IsNullOrWhiteSpace(visualAssetId) ? string.Empty : $"{visualAssetId}:primary",
                string.Empty,
                visualAssetId,
                visualAssetId,
                string.Empty);
        var collision = foundation?.Collision;
        var components = foundation?.Components;
        var transform = foundation?.Transform;
        var interaction = foundation?.Interaction;
        var prop = foundation?.StaticProp;
        var operations = profile?.Operations;
        var container = operations?.Container;
        var stateMachine = operations?.StateMachine;
        var damageable = operations?.Damageable;
        var protection = operations?.Protection;
        var lockProfile = operations?.Lock;
        var factionOwnership = profile?.FactionOwnership;
        var conversation = profile?.Conversation;
        var credentialPolicy = profile?.Credential;
        var securityEvents = profile?.SecurityEvents;
        var trapProfile = securityEvents?.Trap;
        var lootProfile = securityEvents?.Loot;
        var containerEventsProfile = securityEvents?.ContainerEvents;
        var lockEventsProfile = securityEvents?.LockEvents;
        var credentialItem = credentialPolicy is not null &&
                             credentialPolicy.Consumption !=
                             PlaceableWorldProfile.CredentialConsumption.VirtualPermission
            ? TryParseLockItemCredential(credentialPolicy.CredentialId)
            : null;
        if (credentialItem is not null && catalog.Items.All(item => item.Id != credentialItem))
        {
            credentialItem = null;
        }
        var lockKeyItem = lockProfile?.CredentialIds
            .Select(TryParseLockItemCredential)
            .FirstOrDefault(candidate => candidate is not null &&
                                         catalog.Items.Any(item => item.Id == candidate));
        var role = interaction is not null ? StudioPlaceableRole.Interactable : StudioPlaceableRole.StaticDecoration;
        var interactionVerbs = interaction?.Verbs.Select((verb, index) =>
        {
            var label = index == 0 && !string.IsNullOrWhiteSpace(text?.Prompt)
                ? text.Prompt
                : HumanizePlaceableVerb(verb.LabelKey);
            return new StudioPlaceableVerbDraft(
                verb.Id,
                label,
                verb.EventName,
                verb.Condition,
                label,
                verb.LabelKey);
        }).ToArray() ??
        [
            new(
                StablePlaceableId(blueprint.Id, "verb"),
                text?.Prompt ?? "Examiner",
                "placeable-used",
                "true",
                null,
                null),
        ];
        var onUsed = foundation?.OnUsed;

        var draft = new StudioPlaceableDraft(
            blueprint.Id,
            profile?.Id ?? StablePlaceableId(blueprint.Id, "profile"),
            foundation?.Id ?? StablePlaceableId(blueprint.Id, "foundation"),
            interaction?.Verbs.FirstOrDefault()?.Id ?? StablePlaceableId(blueprint.Id, "verb"),
            foundation?.Slug ?? blueprint.Slug,
            text?.Name ?? blueprint.DisplayName,
            text?.Prompt ?? "Examiner",
            additionalLocalizedTexts,
            string.Join(", ", foundation?.Tags ?? ["décor"]),
            foundation?.AuthorNotes ?? string.Empty,
            visualAssetId,
            appearance,
            collision?.Enabled ?? true,
            collision?.NavigationObstacle ?? true,
            collision?.Shape ?? "box",
            collision?.Size ?? Vector3Value.One,
            role,
            prop?.Batched ?? true,
            prop?.Instanced ?? true,
            prop?.Occluder ?? false,
            prop?.MergeEligible ?? false,
            prop?.CollisionEnabled ?? true,
            interaction?.MaximumDistance ?? 2.5,
            interaction?.MaximumAngleDegrees ?? 120,
            interaction?.RequiresFocus ?? true,
            interaction?.DurationSeconds ?? .25,
            interactionVerbs,
            interaction?.Authority ?? "server",
            string.Join(", ", interaction?.Conditions ?? ["true"]),
            onUsed?.Id ?? StablePlaceableId(blueprint.Id, "on-used"),
            onUsed is not null,
            onUsed?.BeforeScriptId ?? string.Empty,
            onUsed?.AfterScriptId ?? string.Empty,
            foundation?.OnUsedRequiredAtRuntime ?? false,
            components?.HasHealth ?? damageable is not null,
            components?.HasContainer ?? false,
            operations?.Id ?? StablePlaceableId(blueprint.Id, "operations"),
            stateMachine is not null,
            stateMachine is null
                ? CreatePlaceableStateMachineDraft(
                    blueprint.Id,
                    components?.HasContainer == true ? "closed" : "idle")
                : new StudioPlaceableStateMachineDraft(
                    stateMachine.Id,
                    stateMachine.InitialStateId,
                    stateMachine.States.Select(state => new StudioPlaceableVisualStateDraft(
                        state.Id,
                        state.StateId,
                        state.AnimationId,
                        state.VisualOverrideId,
                        state.Interactable)).ToArray(),
                    stateMachine.Transitions.Select(transition => new StudioPlaceableVisualTransitionDraft(
                        transition.Id,
                        transition.FromStateId,
                        transition.ToStateId,
                        transition.Trigger,
                        transition.DurationSeconds,
                        transition.Condition ?? string.Empty)).ToArray(),
                    true),
            new StudioPlaceableDamageableDraft(
                damageable?.Id ?? StablePlaceableId(blueprint.Id, "damageable"),
                damageable?.MaximumHealth ?? 100,
                damageable?.CurrentHealth ?? 100,
                damageable?.Armor ?? 5,
                damageable?.Resistances
                    .OrderBy(item => item.Key, StringComparer.Ordinal)
                    .Select(item => new StudioPlaceableDamageResistanceDraft(item.Key, item.Value))
                    .ToArray() ??
                [
                    new("physical", .1),
                    new("fire", .2),
                ],
                damageable?.VisualStageThresholds ?? [.75, .5, .25],
                damageable?.DestructionEffectId ?? $"vfx:{blueprint.Slug}-destruction",
                damageable?.DebrisDefinitionId ?? $"debris:{blueprint.Slug}",
                damageable?.Repairable ?? true,
                damageable?.RepairCost ?? 10,
                damageable is not null),
            protection is not null,
            new StudioPlaceableProtectionDraft(
                protection?.Id ?? StablePlaceableId(blueprint.Id, "plot-protection"),
                protection?.ProtectedActions ?? ["destroy", "move"],
                protection?.RequiredPermissions ?? ["placeable.plot.override"],
                protection?.Feedback ?? "Cet objet est protégé par le scénario.",
                protection?.AllowGameMasterOverride ?? true,
                protection?.AuditBinding ?? "PlaceableProtectionOverride",
                protection is not null),
            factionOwnership is not null,
            new StudioPlaceableFactionOwnershipDraft(
                factionOwnership?.Id ?? StablePlaceableId(blueprint.Id, "faction-ownership"),
                factionOwnership?.PrimaryFactionId ??
                catalog.Factions.FirstOrDefault(option => !option.Archived)?.Id ?? Guid.Empty,
                factionOwnership?.MembershipFactionIds ??
                (catalog.Factions.FirstOrDefault(option => !option.Archived) is { } defaultFaction
                    ? [defaultFaction.Id]
                    : []),
                factionOwnership?.InheritHostility ?? true,
                factionOwnership?.UnauthorizedActionsAreCrime ?? true,
                factionOwnership?.MembersOnlyAccess ?? false,
                factionOwnership?.BlockFriendlyTargeting ?? true,
                factionOwnership is not null),
            profile?.FactionOwnershipRequiredAtRuntime ?? false,
            conversation is not null,
            new StudioPlaceableConversationDraft(
                conversation?.Id ?? StablePlaceableId(blueprint.Id, "conversation"),
                conversation?.Bindings.Select(binding => new StudioPlaceableConversationBindingDraft(
                    binding.Id,
                    binding.DialogueId,
                    binding.Trigger,
                    binding.Selector,
                    binding.ItemBlueprintId,
                    binding.EntryPointKey,
                    binding.Priority,
                    binding.OncePerUser)).ToArray() ??
                (profile?.DialogueId is { } legacyDialogue &&
                 catalog.Dialogues.FirstOrDefault(option => option.Id == legacyDialogue) is { } legacyOption
                    ?
                    [
                        new StudioPlaceableConversationBindingDraft(
                            StablePlaceableId(blueprint.Id, "conversation-legacy-binding"),
                            legacyDialogue,
                            PlaceableConversationTrigger.Interaction,
                            "utiliser",
                            null,
                            legacyOption.EntryPointKeys.FirstOrDefault() ?? "default",
                            0,
                            false),
                    ]
                    : catalog.Dialogues.FirstOrDefault() is { } defaultDialogue
                        ?
                        [
                            new StudioPlaceableConversationBindingDraft(
                                StablePlaceableId(blueprint.Id, "conversation-binding"),
                                defaultDialogue.Id,
                                PlaceableConversationTrigger.Interaction,
                                "utiliser",
                                null,
                                defaultDialogue.EntryPointKeys.FirstOrDefault() ?? "default",
                                0,
                                false),
                        ]
                        : []),
                conversation is not null,
                conversation is null && profile?.DialogueId is not null),
            profile?.ConversationRequiredAtRuntime ?? false,
            credentialPolicy is not null,
            new StudioPlaceableCredentialDraft(
                credentialItem ?? (credentialPolicy is null ? catalog.Items.FirstOrDefault()?.Id : null),
                credentialItem is null
                    ? credentialPolicy?.CredentialId ??
                      (catalog.Items.Count == 0 ? $"key:{blueprint.Slug}" : string.Empty)
                    : string.Empty,
                credentialPolicy?.Consumption ?? PlaceableWorldProfile.CredentialConsumption.Consume,
                credentialPolicy?.Charges ?? 1,
                credentialPolicy?.VirtualPermission ?? string.Empty,
                credentialPolicy?.AuditUse ?? true,
                credentialPolicy is not null),
            profile?.CredentialRequiredAtRuntime ?? false,
            new StudioPlaceableLockDraft(
                lockProfile?.Id ?? StablePlaceableId(blueprint.Id, "lock"),
                lockProfile?.InitialState ?? PlaceableAuthoredLockState.Locked,
                lockKeyItem,
                lockProfile?.CredentialIds
                    .Where(value => lockKeyItem is null ||
                                    !string.Equals(value, LockItemCredential(lockKeyItem),
                                        StringComparison.OrdinalIgnoreCase))
                    .ToArray() ?? [],
                lockProfile?.SkillId ?? "mechanics",
                lockProfile?.Difficulty ?? 12,
                lockProfile?.CodeHash is not null,
                string.Empty,
                lockProfile?.CodeHash,
                lockProfile?.Hackable ?? true,
                lockProfile?.Relockable ?? true,
                lockProfile?.CanJam ?? false,
                lockProfile?.RequiredPermissions ?? ["lock.use"],
                lockProfile is not null),
            new StudioPlaceableContainerDraft(
                container?.Id ?? StablePlaceableId(blueprint.Id, "container"),
                container?.CapacitySlots ?? 24,
                container?.CapacityMass ?? 160,
                container?.OwnershipPolicy ?? "party",
                container?.AccessPermissions ??
                ["container.open", "container.deposit", "container.withdraw"],
                container?.Persistent ?? true,
                container?.LootPolicyId ?? $"loot:{blueprint.Slug}",
                container?.UiProfileId ?? "ui:placeable-chest",
                container?.Replicated ?? true,
                container?.InventoryId ?? $"inventory:{blueprint.Slug}",
                container is not null),
            profile?.OperationsRequiredAtRuntime ?? false,
            components?.HasLock ?? false,
            trapProfile is not null || components?.HasTrap == true,
            new StudioPlaceableTrapDraft(
                trapProfile?.Id ?? StablePlaceableId(blueprint.Id, "trap"),
                trapProfile?.TriggerId ?? "interaction:open",
                trapProfile?.PayloadId ?? "damage:physical",
                trapProfile?.DetectionDifficulty ?? 10,
                trapProfile?.DisarmDifficulty ?? 12,
                trapProfile?.ResetDelaySeconds ?? 30,
                trapProfile?.OwnerId ?? "module.owner",
                trapProfile?.VisualEffectId ?? "vfx:trap",
                trapProfile?.FriendlyPolicy ?? "module.owner",
                trapProfile?.Armed ?? true,
                trapProfile?.OneShot ?? false,
                trapProfile is not null),
            profile?.TrapRequiredAtRuntime ?? false,
            components?.HasPhysics ?? false,
            FormatPairs(components?.ScriptBindings),
            transform?.Position ?? Vector3Value.Zero,
            transform?.RotationDegrees ?? Vector3Value.Zero,
            transform?.Scale ?? Vector3Value.One,
            transform?.Pivot ?? Vector3Value.Zero,
            transform?.SnapIncrement ?? .25,
            transform?.ParentId,
            transform?.AlignToSurface ?? true,
            transform?.TransformAnimationId ?? string.Empty,
            FormatPairs(foundation?.Overrides),
            profile?.FoundationRequiredAtRuntime ?? false,
            profile is not null,
            foundation,
            operations,
            factionOwnership,
            conversation,
            credentialPolicy,
            securityEvents);
        var lootEntries = lootProfile?.LootTable.Entries.Select((entry, index) =>
            new StudioPlaceableLootEntryDraft(
                StablePlaceableId(blueprint.Id,
                    $"loot-entry:{entry.ItemDefinitionId:D}:{entry.Group}:{index}"),
                BlueprintId.From(entry.ItemDefinitionId),
                entry.Group,
                entry.Weight,
                entry.MinimumQuantity,
                entry.MaximumQuantity,
                entry.MinimumQuality,
                entry.Condition,
                entry.Unique)).ToArray() ??
            (catalog.Items.FirstOrDefault() is { } defaultItem
                ?
                [
                    new StudioPlaceableLootEntryDraft(
                        StablePlaceableId(blueprint.Id, "loot-entry:default"),
                        defaultItem.Id,
                        "principal",
                        1,
                        1,
                        1,
                        0,
                        string.Empty,
                        false),
                ]
                : []);
        return draft with
        {
            HasLoot = lootProfile is not null,
            Loot = new StudioPlaceableLootDraft(
                lootProfile?.Id ?? StablePlaceableId(blueprint.Id, "loot-spawner"),
                lootProfile?.LootTableId ?? StablePlaceableId(blueprint.Id, "loot-table"),
                lootProfile?.Timing ?? PlaceableAuthoredLootSpawnTiming.Open,
                lootProfile?.Seed ?? 42,
                lootProfile?.PersistGeneratedState ?? true,
                lootEntries,
                lootProfile?.SavedResults ?? [],
                lootProfile?.Generation ?? 0,
                false,
                lootProfile is not null),
            RequireLootRuntime = profile?.LootRequiredAtRuntime ?? false,
            HasContainerEvents = containerEventsProfile is not null,
            ContainerEvents = new StudioPlaceableContainerEventsDraft(
                containerEventsProfile?.Id ?? StablePlaceableId(blueprint.Id, "container-events"),
                containerEventsProfile?.ForgeScriptApiVersion ?? "1.0.0",
                containerEventsProfile?.OpenedBinding ?? "ContainerOpened",
                containerEventsProfile?.ClosedBinding ?? "ContainerClosed",
                containerEventsProfile?.IncludeActor ?? true,
                containerEventsProfile?.IncludeResult ?? true,
                containerEventsProfile?.IncludePreviousState ?? true,
                containerEventsProfile?.IncludeAuthority ?? true,
                containerEventsProfile is not null),
            RequireContainerEventsRuntime = profile?.ContainerEventsRequiredAtRuntime ?? false,
            HasLockEvents = lockEventsProfile is not null,
            LockEvents = new StudioPlaceableLockEventsDraft(
                lockEventsProfile?.Id ?? StablePlaceableId(blueprint.Id, "lock-events"),
                lockEventsProfile?.ForgeScriptApiVersion ?? "1.0.0",
                lockEventsProfile?.ChangingBinding ?? "LockStateChanging",
                lockEventsProfile?.ChangedBinding ?? "LockStateChanged",
                lockEventsProfile?.IncludeMethod ?? true,
                lockEventsProfile?.IncludeCredential ?? true,
                lockEventsProfile?.IncludeActor ?? true,
                lockEventsProfile?.ControlledVeto ?? true,
                lockEventsProfile is not null),
            RequireLockEventsRuntime = profile?.LockEventsRequiredAtRuntime ?? false,
        };
    }

    internal StudioPlaceablePreview PreviewPlaceable(StudioPlaceableDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        _approvedPlaceable = null;
        if (!TryBuildPlaceable(draft, dryRun: true, out var command, out var profile, out var message))
        {
            return FailedPlaceablePreview(draft, message);
        }

        var result = _setPlaceableCatalog.Handle(History!.State, command!);
        if (!result.Succeeded)
        {
            var diagnostic = result.Diagnostics.FirstOrDefault();
            var human = HumanizePlaceable(diagnostic?.Code);
            RecordPlaceableDiagnostic(diagnostic?.Code ?? "world.placeable.preview_failed", human);
            return FailedPlaceablePreview(draft, human);
        }

        var runtime = WorldFeatureRuntimeResolver.ResolvePlaceableFoundation(profile!);
        if (!runtime.Available || runtime.SnappedTransform is null)
        {
            var human = HumanizePlaceable(runtime.DiagnosticCode);
            RecordPlaceableDiagnostic(runtime.DiagnosticCode, human);
            return FailedPlaceablePreview(draft, human);
        }
        var instances = PlaceableInstances(draft.BlueprintId);
        var roleLabel = draft.Role == StudioPlaceableRole.Interactable ? "Objet utilisable" : "Décor statique";
        var visualLabel = CreatePlaceableCatalog().Visuals.FirstOrDefault(option =>
            string.Equals(option.AssetId, draft.VisualAssetId, StringComparison.Ordinal))?.DisplayName ??
            "Visuel lié au blueprint";
        var componentCount = new[]
        {
            draft.HasHealth, draft.HasContainer, draft.HasLock, draft.HasTrap, draft.HasPhysics,
            draft.HasFactionOwnership, draft.HasConversation, draft.HasCredentialPolicy, draft.HasLoot,
            draft.HasContainerEvents, draft.HasLockEvents,
        }.Count(value => value);
        var snapped = runtime.SnappedTransform.Position;
        var optimization = draft.Role == StudioPlaceableRole.StaticDecoration
            ? "\nOptimisation : " +
              $"{(draft.Batched ? "rendus regroupés" : "regroupement désactivé")} · " +
              $"{(draft.Instanced ? "modèle instancié" : "instanciation désactivée")} · " +
              $"{(draft.Occluder ? "occludeur actif" : "occlusion désactivée")} · " +
              $"{(draft.MergeEligible ? "fusion autorisée" : "fusion séparée")} · " +
              $"{(draft.StaticCollisionEnabled ? "collision par instance" : "sans collision par instance")}"
            : string.Empty;
        var interaction = draft.Role == StudioPlaceableRole.Interactable
            ? "\nInteraction : " +
              $"{draft.InteractionVerbs.Count} action(s) · portée {draft.InteractionDistance:0.##} m · " +
              $"angle {draft.InteractionAngle:0.##}° · " +
              $"{(draft.RequiresFocus ? "ciblage requis" : "sans ciblage requis")} · " +
              $"durée {draft.InteractionDuration:0.##} s · autorité {draft.InteractionAuthority.Trim()}\n" +
              $"Conditions : {string.Join(" · ", SplitValues(draft.InteractionConditionsText))}\n" +
              $"OnUsed : {(draft.OnUsedEnabled ? "publié" : "désactivé")} · " +
              $"avant {(string.IsNullOrWhiteSpace(draft.BeforeUseScript) ? "absent" : "configuré")} · " +
              $"après {(string.IsNullOrWhiteSpace(draft.AfterUseScript) ? "absent" : "configuré")} · " +
              $"{(draft.RequireOnUsedRuntime ? "requis au runtime" : "optionnel au runtime")}"
            : string.Empty;
        var container = draft.HasContainer
            ? "\nConteneur : " +
              $"{draft.Container.CapacitySlots} emplacement(s) · {draft.Container.CapacityMass:0.##} kg · " +
              $"{HumanizeContainerOwnership(draft.Container.OwnershipPolicy)}\n" +
              $"Accès : {HumanizeContainerAccess(draft.Container.AccessPermissions)} · " +
              $"{(draft.Container.Persistent ? "conservé dans les sauvegardes" : "réinitialisé")} · " +
              $"{(draft.Container.Replicated ? "répliqué" : "local")}\n" +
              $"Inventaire {draft.Container.InventoryId.Trim()} · butin {draft.Container.LootPolicyId.Trim()} · " +
              $"présentation {draft.Container.UiProfileId.Trim()} · " +
              $"{(draft.RequireOperationsRuntime ? "requis au runtime" : "optionnel au runtime")}"
            : string.Empty;
        var visualStates = draft.HasStateMachine
            ? "\nAnimation initiale : " + HumanizePlaceableVisualState(draft.StateMachine.InitialStateId) +
              $" · {draft.StateMachine.States.Count} état(s) · {draft.StateMachine.Transitions.Count} transition(s) · " +
              $"{(draft.RequireOperationsRuntime ? "requise au runtime" : "optionnelle au runtime")}"
            : string.Empty;
        var destructibility = draft.HasHealth
            ? "\nDestructibilité : " +
              $"{draft.Damageable.CurrentHealth:0.##}/{draft.Damageable.MaximumHealth:0.##} PV · " +
              $"armure {draft.Damageable.Armor:0.##} · {draft.Damageable.Resistances.Count} résistance(s) · " +
              $"{draft.Damageable.VisualStageThresholds.Count} étape(s) visuelle(s)\n" +
              $"Destruction : effet et débris configurés · " +
              $"{(draft.Damageable.Repairable ? $"réparable pour {draft.Damageable.RepairCost:0.##}" : "irréparable")} · " +
              $"{(draft.RequireOperationsRuntime ? "requise au runtime" : "optionnelle au runtime")}"
            : string.Empty;
        var plotProtection = draft.HasProtection
            ? "\nProtection plot : " +
              $"{draft.Protection.ProtectedActions.Count} action(s) protégée(s) · " +
              $"{draft.Protection.RequiredPermissions.Count} droit(s) requis\n" +
              $"Feedback joueur configuré · override MJ " +
              $"{(draft.Protection.AllowGameMasterOverride ? "autorisé et audité" : "interdit")} · " +
              $"{(draft.RequireOperationsRuntime ? "requise au runtime" : "optionnelle au runtime")}"
            : string.Empty;
        var factionOwnership = draft.HasFactionOwnership
            ? "\nFaction et propriété : " +
              $"{FactionDisplayName(draft.FactionOwnership.PrimaryFactionId)} principale · " +
              $"{draft.FactionOwnership.MembershipFactionIds.Count} appartenance(s)\n" +
              $"Hostilité {(draft.FactionOwnership.InheritHostility ? "héritée" : "ignorée")} · " +
              $"crime {(draft.FactionOwnership.UnauthorizedActionsAreCrime ? "signalé" : "désactivé")} · " +
              $"accès {(draft.FactionOwnership.MembersOnlyAccess ? "réservé aux membres" : "ouvert")} · " +
              $"ciblage allié {(draft.FactionOwnership.BlockFriendlyTargeting ? "bloqué" : "autorisé")} · " +
              $"{(draft.RequireFactionOwnershipRuntime ? "requise au runtime" : "optionnelle au runtime")}"
            : string.Empty;
        var conversation = draft.HasConversation
            ? "\nConversation : " +
              $"{draft.Conversation.Bindings.Count} liaison(s) · " +
              $"{draft.Conversation.Bindings.Select(binding => binding.DialogueId).Distinct().Count()} dialogue(s) · " +
              $"{draft.Conversation.Bindings.Select(binding => binding.Trigger).Distinct().Count()} déclencheur(s)\n" +
              $"Interaction, état, utilisateur, objet et événement disponibles · " +
              $"priorités ordonnées · " +
              $"{draft.Conversation.Bindings.Count(binding => binding.OncePerUser)} usage(s) unique(s) · " +
              $"{(draft.RequireConversationRuntime ? "requise au runtime" : "optionnelle au runtime")}"
            : string.Empty;
        var credentialSummary = draft.HasCredentialPolicy
            ? "\nJustificatif : " + HumanizeCredentialConsumption(draft.Credential.Consumption) + " · " +
              (draft.Credential.ItemBlueprintId is null ? "identité métier" : "objet clé du projet") + " · " +
              (draft.Credential.Consumption == PlaceableWorldProfile.CredentialConsumption.Charge
                  ? $"{draft.Credential.Charges} charge(s) · "
                  : string.Empty) +
              (draft.Credential.Consumption == PlaceableWorldProfile.CredentialConsumption.VirtualPermission
                  ? "permission virtuelle · "
                  : string.Empty) +
              $"audit {(draft.Credential.AuditUse ? "actif" : "désactivé")} · " +
              $"{(draft.RequireCredentialRuntime ? "requis au runtime" : "optionnel au runtime")}"
            : string.Empty;
        var trapSummary = draft.HasTrap
            ? "\nPiège : " + draft.Trap.TriggerId.Trim() + " → " + draft.Trap.PayloadId.Trim() + " · " +
              $"détection {draft.Trap.DetectionDifficulty:0.##} · " +
              $"désamorçage {draft.Trap.DisarmDifficulty:0.##} · " +
              $"réarmement {draft.Trap.ResetDelaySeconds:0.##} s\n" +
              $"Propriétaire {draft.Trap.OwnerId.Trim()} · alliés {draft.Trap.FriendlyPolicy.Trim()} · " +
              $"effet {draft.Trap.VisualEffectId.Trim()} · " +
              $"{(draft.Trap.OneShot ? "usage unique" : "réutilisable")} · " +
              $"{(draft.Trap.Armed ? "armé au départ" : "désarmé au départ")} · " +
              $"{(draft.RequireTrapRuntime ? "requis au runtime" : "optionnel au runtime")}"
            : string.Empty;
        var lootSummary = draft.HasLoot && draft.Loot is { } lootDraft
            ? "\nTrésor initial : " + lootDraft.Entries.Count + " objet(s) · " +
              HumanizeLootTiming(lootDraft.Timing) + " · graine " + lootDraft.Seed + " · " +
              (lootDraft.PersistGeneratedState ? "état généré conservé" : "nouveau tirage non conservé") +
              "\nÉtat existant : génération " + lootDraft.Generation + " · " +
              lootDraft.SavedResults.Count + " résultat(s) · " +
              (lootDraft.ResetGeneratedState ? "réinitialisation explicite demandée" : "préservé") + " · " +
              (draft.RequireLootRuntime ? "requis au runtime" : "optionnel au runtime")
            : string.Empty;
        var containerEventsSummary = draft.HasContainerEvents && draft.ContainerEvents is { } eventDraft
            ? "\nOuverture et fermeture : " + eventDraft.OpenedBinding.Trim() + " · " +
              eventDraft.ClosedBinding.Trim() + " · API " + eventDraft.ForgeScriptApiVersion.Trim() + "\n" +
              "Contexte : acteur · résultat · état précédent · autorité · " +
              (draft.RequireContainerEventsRuntime ? "requis au runtime" : "optionnel au runtime")
            : string.Empty;
        var lockEventsSummary = draft.HasLockEvents && draft.LockEvents is { } lockEventDraft
            ? "\nVerrouillage et déverrouillage : " + lockEventDraft.ChangingBinding.Trim() + " · " +
              lockEventDraft.ChangedBinding.Trim() + " · API " + lockEventDraft.ForgeScriptApiVersion.Trim() + "\n" +
              "Contexte : méthode · justificatif · acteur · veto contrôlé · " +
              (draft.RequireLockEventsRuntime ? "requis au runtime" : "optionnel au runtime")
            : string.Empty;
        var lockSummary = draft.HasLock
            ? "\nVerrouillage : " + HumanizeLockState(draft.Lock.InitialState) + " · " +
              $"{(draft.Lock.KeyItemId is null ? 0 : 1) + draft.Lock.OtherCredentialIds.Count} justificatif(s) · " +
              $"compétence {(string.IsNullOrWhiteSpace(draft.Lock.SkillId) ? "désactivée" : "configurée")} · " +
              $"code {(draft.Lock.CodeEnabled ? "configuré" : "désactivé")}\n" +
              $"Piratage {(draft.Lock.Hackable ? "autorisé" : "interdit")} · " +
              $"reverrouillage {(draft.Lock.Relockable ? "autorisé" : "interdit")} · " +
              $"brouillage {(draft.Lock.CanJam ? "autorisé" : "interdit")} · " +
              $"{draft.Lock.RequiredPermissions.Count} droit(s) · " +
              $"{(draft.RequireOperationsRuntime ? "requis au runtime" : "optionnel au runtime")}"
            : string.Empty;
        var identity =
            $"\nIdentité : {draft.Name.Trim()} · {draft.Slug.Trim()} · " +
            $"{draft.AdditionalLocalizedTexts.Count + 1} langue(s) · {SplitValues(draft.TagsText).Count} tag(s)\n" +
            $"Apparence : {SplitValues(draft.Appearance.MeshIdsText).Count} mesh(es) · " +
            $"{SplitValues(draft.Appearance.MaterialIdsText).Count} matériau(x) · " +
            $"icône et portrait liés · {SplitValues(draft.Appearance.VisualEffectIdsText).Count} VFX";
        var parent = CreatePlaceableCatalog().Parents.FirstOrDefault(option => option.ProfileId == draft.ParentId)
            ?.DisplayName ?? (draft.ParentId is null ? "racine de la scène" : "parent existant");
        var transformSummary = "\n" + FormattableString.Invariant(
            $"Transformation : position {draft.Position.X:0.##}, {draft.Position.Y:0.##}, {draft.Position.Z:0.##} · rotation {draft.RotationDegrees.X:0.##}°, {draft.RotationDegrees.Y:0.##}°, {draft.RotationDegrees.Z:0.##}° · échelle {draft.Scale.X:0.##}, {draft.Scale.Y:0.##}, {draft.Scale.Z:0.##}") +
            "\n" + FormattableString.Invariant(
                $"Pivot {draft.Pivot.X:0.##}, {draft.Pivot.Y:0.##}, {draft.Pivot.Z:0.##} · grille {draft.SnapIncrement:0.##} · parent {parent} · surface {(draft.AlignToSurface ? "alignée" : "libre")} · animation {(string.IsNullOrWhiteSpace(draft.TransformAnimationId) ? "aucune" : draft.TransformAnimationId.Trim())}");
        var summary =
            $"{roleLabel} · {visualLabel}\n" +
            $"Collision {(draft.CollisionEnabled ? "active" : "désactivée")} · " +
            $"{componentCount} composant(s) optionnel(s) · {instances.Count} instance(s) dans le module" +
            identity + optimization + interaction + container + visualStates + destructibility + plotProtection +
            factionOwnership + conversation + lockSummary + credentialSummary + trapSummary + lootSummary +
            containerEventsSummary + lockEventsSummary +
            transformSummary + "\n" +
            FormattableString.Invariant(
                $"Position prévisualisée : {snapped.X:0.##}, {snapped.Y:0.##}, {snapped.Z:0.##} · pas {draft.SnapIncrement:0.##}");
        _approvedPlaceable = draft;
        return new StudioPlaceablePreview(
            true,
            "Aperçu validé · aucun document n’a été modifié.",
            summary,
            true,
            draft.BlueprintId,
            instances,
            snapped,
            roleLabel,
            visualLabel);
    }

    internal StudioPlaceableResult ApplyApprovedPlaceable()
    {
        if (_approvedPlaceable is null)
        {
            return new StudioPlaceableResult(false, "Prévisualisez la fiche avant de l’appliquer.");
        }
        if (!TryBuildPlaceable(
                _approvedPlaceable,
                dryRun: false,
                out var command,
                out _,
                out var message))
        {
            _approvedPlaceable = null;
            return new StudioPlaceableResult(false, message);
        }

        var result = _setPlaceableCatalog.Handle(History!.State, command!);
        _approvedPlaceable = null;
        if (!result.Succeeded)
        {
            var diagnostic = result.Diagnostics.FirstOrDefault();
            var human = HumanizePlaceable(diagnostic?.Code);
            RecordPlaceableDiagnostic(diagnostic?.Code ?? "world.placeable.apply_failed", human);
            return new StudioPlaceableResult(false, human);
        }
        History.Record(result);
        InvalidateContentIndexes();
        return new StudioPlaceableResult(true, "Blueprint de décor appliqué · enregistrez pour écrire le module.");
    }

    internal void CancelPlaceablePreview() => _approvedPlaceable = null;

    internal string LastPlaceableDiagnosticCode => _studioOperationalDiagnostics
        .LastOrDefault(item => string.Equals(item.CorrelationId, "placeable:blueprint", StringComparison.Ordinal))
        ?.Code ?? string.Empty;

    internal StudioUx1ESmokeResult RunUx1EContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux1e-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var blueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1e10000-0000-4000-8000-000000000001")),
                "statue-des-brumes",
                "Statue des brumes",
                BlueprintKind.Placeable);
            var neighborBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1e10000-0000-4000-8000-000000000002")),
                "lanterne-temoin",
                "Lanterne témoin",
                BlueprintKind.Placeable);
            var first = new EntityInstance(
                EntityId.From(Guid.Parse("c1e10000-0000-4000-8000-000000000003")),
                blueprint.Id,
                new TransformValue(Vector3Value.Zero, QuaternionValue.Identity, Vector3Value.One));
            var second = new EntityInstance(
                EntityId.From(Guid.Parse("c1e10000-0000-4000-8000-000000000004")),
                blueprint.Id,
                new TransformValue(new(3, 0, 0), QuaternionValue.Identity, Vector3Value.One));
            var neighborEntity = new EntityInstance(
                EntityId.From(Guid.Parse("c1e10000-0000-4000-8000-000000000005")),
                neighborBlueprint.Id,
                new TransformValue(new(-3, 0, 0), QuaternionValue.Identity, Vector3Value.One));
            var area = new AreaDefinition(
                AreaId.From(Guid.Parse("c1e10000-0000-4000-8000-000000000006")),
                "atelier-placeables",
                "Atelier des placeables",
                [first, second, neighborEntity],
                AreaKind.Interior,
                new AreaDimensions(32, 32));
            var project = new ProjectDefinition(
                ProjectId.From(Guid.Parse("c1e10000-0000-4000-8000-000000000007")),
                "Studio UX1-E",
                [blueprint, neighborBlueprint],
                [area]);
            var neighborProfile = new PlaceableWorldProfile(
                Guid.Parse("c1e10000-0000-4000-8000-000000000008"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-e-neighbor",
                neighborBlueprint.Id,
                "neutral",
                null,
                null,
                new([], new(false, 1, 1, true, "placeable")),
                null);
            var seededCatalog = new WorldFeatureCatalog(
                project.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-e-catalog",
                placeables: [neighborProfile]);
            project = new ProjectDefinition(
                project.Id,
                project.Title,
                project.Module.WithWorldFeatures(seededCatalog),
                project.Blueprints,
                project.Areas,
                project.Dialogues,
                project.Quests);
            var created = ProjectSession.Create(tempRoot, project);
            if (!created.Succeeded || !editing.Open(tempRoot).Succeeded)
            {
                return FailedUx1ESmoke();
            }

            var originalProject = editing.Project!;
            var originalCatalog = originalProject.Module.WorldFeatures;
            var originalNeighbor = originalCatalog.Placeables.Single(item => item.BlueprintId == neighborBlueprint.Id);
            var draft = editing.CreatePlaceableDraft(blueprint.Id)! with
            {
                Name = "Statue des brumes",
                Prompt = "Examiner la statue",
                TagsText = "décor, pierre, brumes",
                AuthorNotes = "Point de repère de l’atelier.",
                VisualAssetId = "asset:statue-brumes",
                CollisionEnabled = true,
                NavigationObstacle = true,
                CollisionShape = "box",
                CollisionSize = new(1.5f, 3f, 1.5f),
                Role = StudioPlaceableRole.StaticDecoration,
                Batched = true,
                Instanced = true,
                Occluder = true,
                StaticCollisionEnabled = true,
                HasHealth = false,
                HasContainer = false,
                HasLock = false,
                HasTrap = false,
                HasPhysics = false,
                ScriptBindingsText = "onInspect=scripts/statue_inspect",
                Position = new(1.13f, 0, -.13f),
                RotationDegrees = new(0, 45, 0),
                Scale = new(1, 1.25f, 1),
                Pivot = new(0, 1.5f, 0),
                SnapIncrement = .25,
                AlignToSurface = true,
                ExplicitOverridesText = "material=weathered-stone; variant=misty",
                RequireRuntime = true,
            };
            var invalid = editing.PreviewPlaceable(draft with { CollisionSize = Vector3Value.Zero });
            var invalidBlocked = !invalid.Succeeded;
            var humanDiagnostics = invalidBlocked && IsHumanPlaceableMessage(invalid.Message);
            var preview = editing.PreviewPlaceable(draft);
            var previewValid = preview.Succeeded && preview.CanApply;
            var dryRun = previewValid && ReferenceEquals(originalProject, editing.Project);
            var instanceSelection = preview.InstanceIds.SequenceEqual([first.Id, second.Id]);

            var applied = editing.ApplyApprovedPlaceable();
            var appliedProject = editing.Project!;
            var appliedCatalog = appliedProject.Module.WorldFeatures;
            var profile = appliedCatalog.Placeables.Single(item => item.BlueprintId == blueprint.Id);
            var foundation = profile.Foundation;
            var composition = applied.Succeeded && foundation is not null &&
                              foundation.Texts.Single().Name == "Statue des brumes" &&
                              foundation.Tags.SequenceEqual(["décor", "pierre", "brumes"]) &&
                              foundation.StaticProp is { Batched: true, Instanced: true, Occluder: true } &&
                              foundation.Interaction is null && foundation.Collision.NavigationObstacle &&
                              foundation.Components.ScriptBindings["onInspect"] == "scripts/statue_inspect" &&
                              foundation.Overrides["variant"] == "misty" && profile.FoundationRequiredAtRuntime;
            var assetReference = foundation?.Visual.AssetId == "asset:statue-brumes" &&
                                 foundation.Visual.MeshIds.Count == 1;
            var neighborPreserved = ReferenceEquals(
                originalNeighbor,
                appliedCatalog.Placeables.Single(item => item.BlueprintId == neighborBlueprint.Id));
            var otherCatalogPreserved = appliedCatalog.Doors.SequenceEqual(originalCatalog.Doors) &&
                                        appliedCatalog.Triggers.SequenceEqual(originalCatalog.Triggers) &&
                                        appliedCatalog.Encounters.SequenceEqual(originalCatalog.Encounters) &&
                                        appliedCatalog.VehicleRoutes.SequenceEqual(originalCatalog.VehicleRoutes) &&
                                        appliedCatalog.Merchants.SequenceEqual(originalCatalog.Merchants) &&
                                        appliedCatalog.Companions.SequenceEqual(originalCatalog.Companions) &&
                                        ReferenceEquals(appliedCatalog.Scripts, originalCatalog.Scripts);
            var runtime = WorldFeatureRuntimeResolver.ResolvePlaceableFoundation(profile);
            var optional = WorldFeatureRuntimeResolver.ResolvePlaceableFoundation(profile with
            {
                Foundation = null,
                FoundationRequiredAtRuntime = false,
            });
            var required = WorldFeatureRuntimeResolver.ResolvePlaceableFoundation(profile with { Foundation = null });
            var runtimeSemantics = runtime is { Available: true, SnappedTransform.Position.X: 1.25f } &&
                                   runtime.SnappedTransform!.Position.Z == -.25f &&
                                   !optional.Available && string.IsNullOrEmpty(optional.DiagnosticCode) &&
                                   !required.Available &&
                                   required.DiagnosticCode == "WORLD_PLACEABLE_FOUNDATION_REQUIRED_MISSING";

            var undo = editing.Undo();
            var undone = ReferenceEquals(originalProject, editing.Project);
            var redo = editing.Redo();
            var redone = ReferenceEquals(appliedProject, editing.Project);
            var undoRedo = undo.Succeeded && undone && redo.Succeeded && redone;
            var save = editing.Save();
            editing.Close();
            var reopen = reopened.Open(tempRoot);
            var restored = reopened.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
                item.BlueprintId == blueprint.Id);
            var persistence = save.Succeeded && reopen.Succeeded && restored?.Foundation is not null &&
                              restored.Foundation.Visual.AssetId == "asset:statue-brumes" &&
                              restored.Foundation.Transform.Position == new Vector3Value(1.13f, 0, -.13f) &&
                              restored.Foundation.Overrides["material"] == "weathered-stone" &&
                              restored.Foundation.Components.ScriptBindings["onInspect"] == "scripts/statue_inspect";

            var succeeded = previewValid && dryRun && composition && assetReference && instanceSelection &&
                            neighborPreserved && otherCatalogPreserved && runtimeSemantics && undoRedo && persistence &&
                            invalidBlocked && humanDiagnostics;
            return new(
                succeeded,
                previewValid,
                dryRun,
                composition,
                assetReference,
                instanceSelection,
                neighborPreserved,
                otherCatalogPreserved,
                runtimeSemantics,
                undoRedo,
                persistence,
                invalidBlocked,
                humanDiagnostics);
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

    private bool TryBuildPlaceable(
        StudioPlaceableDraft draft,
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
        var blueprint = Project.Blueprints.FirstOrDefault(candidate => candidate.Id == draft.BlueprintId);
        if (blueprint is null || blueprint.Kind != BlueprintKind.Placeable)
        {
            message = "Le blueprint de décor choisi n’existe plus.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(draft.Name) || string.IsNullOrWhiteSpace(draft.Slug))
        {
            message = "Donnez un nom visible et un nom court au blueprint de décor.";
            return false;
        }
        if (draft.AdditionalLocalizedTexts.Any(item =>
                string.IsNullOrWhiteSpace(item.Locale) ||
                string.IsNullOrWhiteSpace(item.Name) ||
                string.Equals(item.Locale, "fr-FR", StringComparison.OrdinalIgnoreCase)) ||
            draft.AdditionalLocalizedTexts.Select(item => item.Locale.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != draft.AdditionalLocalizedTexts.Count)
        {
            message = "Chaque traduction supplémentaire exige une langue unique et un nom visible ; le français principal se règle au-dessus.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(draft.VisualAssetId))
        {
            message = "Choisissez un visuel pour ce blueprint de décor.";
            return false;
        }
        var meshIds = SplitValues(draft.Appearance.MeshIdsText);
        var materialIds = SplitValues(draft.Appearance.MaterialIdsText);
        var visualEffectIds = SplitValues(draft.Appearance.VisualEffectIdsText);
        var iconAssetId = draft.Appearance.IconAssetId.Trim();
        var portraitAssetId = draft.Appearance.PortraitAssetId.Trim();
        if (draft.OriginalFoundation is null)
        {
            meshIds = meshIds.Count == 0 ? [$"{draft.VisualAssetId.Trim()}:primary"] : meshIds;
            iconAssetId = string.IsNullOrWhiteSpace(iconAssetId) ? draft.VisualAssetId.Trim() : iconAssetId;
            portraitAssetId = string.IsNullOrWhiteSpace(portraitAssetId) ? draft.VisualAssetId.Trim() : portraitAssetId;
        }
        if (meshIds.Count == 0 || string.IsNullOrWhiteSpace(iconAssetId) ||
            string.IsNullOrWhiteSpace(portraitAssetId))
        {
            message = "L’apparence exige au moins un mesh, une icône et un portrait.";
            return false;
        }
        if (!TryParsePairs(draft.ScriptBindingsText, "liaison de script", out var bindings, out message) ||
            !TryParsePairs(draft.ExplicitOverridesText, "surcharge", out var overrides, out message))
        {
            return false;
        }
        if (draft.Role == StudioPlaceableRole.Interactable)
        {
            var verbs = draft.InteractionVerbs;
            if (verbs.Count == 0 ||
                verbs.Any(verb => verb.Id == Guid.Empty ||
                                  string.IsNullOrWhiteSpace(verb.Label) ||
                                  string.IsNullOrWhiteSpace(verb.EventName) ||
                                  string.IsNullOrWhiteSpace(verb.Condition)) ||
                verbs.Select(verb => verb.Id).Distinct().Count() != verbs.Count)
            {
                message = "Ajoutez au moins une action avec un nom, un événement et une condition.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(draft.InteractionAuthority) ||
                SplitValues(draft.InteractionConditionsText).Count == 0)
            {
                message = "Choisissez l’autorité et au moins une condition globale pour l’interaction.";
                return false;
            }
        }
        if (draft.HasContainer)
        {
            var container = draft.Container;
            if (container.Id == Guid.Empty || container.CapacitySlots < 1 ||
                !double.IsFinite(container.CapacityMass) || container.CapacityMass < 0)
            {
                message = "Le conteneur exige au moins un emplacement et une masse maximale positive ou nulle.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(container.OwnershipPolicy) ||
                container.AccessPermissions.Count == 0 ||
                container.AccessPermissions.Any(string.IsNullOrWhiteSpace) ||
                container.AccessPermissions.Distinct(StringComparer.Ordinal).Count() !=
                container.AccessPermissions.Count)
            {
                message = "Choisissez un propriétaire et au moins un droit d’accès unique pour le conteneur.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(container.InventoryId) ||
                string.IsNullOrWhiteSpace(container.LootPolicyId) ||
                string.IsNullOrWhiteSpace(container.UiProfileId))
            {
                message = "Renseignez l’inventaire, la politique de butin et la présentation du conteneur.";
                return false;
            }
        }
        PlaceableVisualStateMachineProfile? editedStateMachine = null;
        if (draft.HasStateMachine &&
            !TryBuildPlaceableStateMachine(draft.StateMachine, out editedStateMachine, out message))
        {
            return false;
        }
        var stateMachineChanged = draft.HasStateMachine &&
                                  !StateMachineValuesEqual(
                                      editedStateMachine,
                                      draft.OriginalOperations?.StateMachine);
        PlaceableDamageableProfile? editedDamageable = null;
        if (draft.HasHealth &&
            !TryBuildPlaceableDamageable(draft.Damageable, out editedDamageable, out message))
        {
            return false;
        }
        var damageableChanged = draft.HasHealth &&
                                !DamageableValuesEqual(
                                    editedDamageable,
                                    draft.OriginalOperations?.Damageable);
        PlaceableProtectionProfile? editedProtection = null;
        if (draft.HasProtection &&
            !TryBuildPlaceableProtection(draft.Protection, out editedProtection, out message))
        {
            return false;
        }
        var protectionChanged = draft.HasProtection &&
                                !ProtectionValuesEqual(
                                    editedProtection,
                                    draft.OriginalOperations?.Protection);
        PlaceableFactionOwnershipProfile? editedFactionOwnership = null;
        if (draft.HasFactionOwnership &&
            !TryBuildPlaceableFactionOwnership(draft.FactionOwnership, out editedFactionOwnership, out message))
        {
            return false;
        }
        var factionOwnershipChanged = draft.HasFactionOwnership &&
                                      !FactionOwnershipValuesEqual(
                                          editedFactionOwnership,
                                          draft.OriginalFactionOwnership);
        PlaceableConversationProfile? editedConversation = null;
        if (draft.HasConversation &&
            !TryBuildPlaceableConversation(draft.Conversation, out editedConversation, out message))
        {
            return false;
        }
        var conversationChanged = draft.HasConversation &&
                                  !ConversationValuesEqual(
                                      editedConversation,
                                      draft.OriginalConversation);
        PlaceableWorldProfile.CredentialPolicy? editedCredential = null;
        if (draft.HasCredentialPolicy &&
            !TryBuildPlaceableCredential(draft.Credential, out editedCredential, out message))
        {
            return false;
        }
        var credentialChanged = draft.HasCredentialPolicy &&
                                !CredentialValuesEqual(editedCredential, draft.OriginalCredential);
        PlaceableTrapProfile? editedTrap = null;
        if (draft.HasTrap && !TryBuildPlaceableTrap(draft.Trap, out editedTrap, out message))
        {
            return false;
        }
        var trapChanged = draft.HasTrap &&
                          !TrapValuesEqual(editedTrap, draft.OriginalSecurityEvents?.Trap);
        PlaceableLootSpawnerProfile? editedLoot = null;
        if (draft.HasLoot && (draft.Loot is null ||
            !TryBuildPlaceableLoot(draft.Loot, out editedLoot, out message)))
        {
            message = draft.Loot is null
                ? "Le trésor initial est actif mais sa configuration est absente."
                : message;
            return false;
        }
        if (!draft.HasLoot && draft.RequireLootRuntime)
        {
            message = "Le trésor initial est requis au runtime : activez-le ou retirez cette exigence.";
            return false;
        }
        var originalLoot = draft.OriginalSecurityEvents?.Loot;
        var lootConfigurationChanged = draft.HasLoot &&
                                       !LootConfigurationValuesEqual(editedLoot, originalLoot);
        if (lootConfigurationChanged && originalLoot is { } generatedLoot &&
            (generatedLoot.Generation > 0 || generatedLoot.SavedResults.Count > 0) &&
            draft.Loot?.ResetGeneratedState != true)
        {
            message = "Ce trésor possède déjà un état généré. Cochez « Réinitialiser l’état généré » pour modifier sa table, son moment ou sa graine.";
            return false;
        }
        PlaceableContainerStateEventsProfile? editedContainerEvents = null;
        if (draft.HasContainerEvents && (draft.ContainerEvents is null ||
            !TryBuildPlaceableContainerEvents(draft.ContainerEvents, out editedContainerEvents, out message)))
        {
            message = draft.ContainerEvents is null
                ? "Les événements d’ouverture et de fermeture sont actifs mais leur configuration est absente."
                : message;
            return false;
        }
        if (!draft.HasContainerEvents && draft.RequireContainerEventsRuntime)
        {
            message = "Les événements d’ouverture et de fermeture sont requis au runtime : activez-les ou retirez cette exigence.";
            return false;
        }
        var containerEventsChanged = draft.HasContainerEvents &&
                                     !ContainerEventValuesEqual(
                                         editedContainerEvents,
                                         draft.OriginalSecurityEvents?.ContainerEvents);
        PlaceableLockStateEventsProfile? editedLockEvents = null;
        if (draft.HasLockEvents && (draft.LockEvents is null ||
            !TryBuildPlaceableLockEvents(draft.LockEvents, out editedLockEvents, out message)))
        {
            message = draft.LockEvents is null
                ? "Les événements de verrouillage et de déverrouillage sont actifs mais leur configuration est absente."
                : message;
            return false;
        }
        if (!draft.HasLockEvents && draft.RequireLockEventsRuntime)
        {
            message = "Les événements de verrouillage et de déverrouillage sont requis au runtime : activez-les ou retirez cette exigence.";
            return false;
        }
        var lockEventsChanged = draft.HasLockEvents &&
                                !LockEventValuesEqual(
                                    editedLockEvents,
                                    draft.OriginalSecurityEvents?.LockEvents);
        PlaceableLockProfile? editedLock = null;
        if (draft.HasLock && !TryBuildPlaceableLock(draft.Lock, out editedLock, out message))
        {
            return false;
        }
        var lockChanged = draft.HasLock &&
                          !LockValuesEqual(editedLock, draft.OriginalOperations?.Lock);
        if (!ValidPlaceableTransformDraft(draft))
        {
            message = "La transformation exige des coordonnées finies, une échelle entre 0,01 et 100 et un pas de grille valide.";
            return false;
        }
        var currentCatalog = Project.Module.WorldFeatures;
        if (draft.ParentId == draft.ProfileId)
        {
            message = "Un placeable ne peut pas être son propre parent.";
            return false;
        }
        if (draft.ParentId is { } parentId && currentCatalog.Placeables.All(candidate =>
                candidate.Id != parentId || candidate.Foundation is null))
        {
            message = "Choisissez un parent existant avec une transformation canonique, ou laissez l’objet à la racine.";
            return false;
        }
        if (WouldCreatePlaceableParentCycle(currentCatalog.Placeables, draft.ProfileId, draft.ParentId))
        {
            message = "Ce parent créerait une boucle de transformation. Choisissez un autre placeable.";
            return false;
        }
        try
        {
            var original = draft.OriginalFoundation;
            var visual = new PlaceableWorldProfile.VisualDefinition(
                draft.VisualAssetId.Trim(),
                meshIds,
                materialIds,
                iconAssetId,
                portraitAssetId,
                visualEffectIds);
            var localized = MergeLocalizedTexts(
                draft.AdditionalLocalizedTexts,
                draft.Name.Trim(),
                draft.Prompt.Trim());
            var tags = SplitValues(draft.TagsText);
            var collision = new PlaceableWorldProfile.CollisionDefinition(
                draft.CollisionShape.Trim(),
                draft.CollisionSize,
                draft.CollisionEnabled,
                draft.NavigationObstacle,
                original?.Collision.PhysicsLayer ?? "world");
            PlaceableWorldProfile.InteractionPolicy? interaction = null;
            PlaceableWorldProfile.StaticPropPolicy? prop = null;
            if (draft.Role == StudioPlaceableRole.Interactable)
            {
                var verbs = draft.InteractionVerbs.Select(verb => new PlaceableWorldProfile.InteractionVerb(
                    verb.Id,
                    PlaceableVerbLabelKey(draft.Slug.Trim(), verb),
                    verb.EventName.Trim(),
                    verb.Condition.Trim())).ToArray();
                interaction = new PlaceableWorldProfile.InteractionPolicy(
                    verbs,
                    draft.InteractionDistance,
                    draft.InteractionAngle,
                    draft.RequiresFocus,
                    original?.Interaction?.PromptKey ?? $"{draft.Slug.Trim()}.use",
                    draft.InteractionDuration,
                    draft.InteractionAuthority.Trim(),
                    SplitValues(draft.InteractionConditionsText));
            }
            else
            {
                prop = new PlaceableWorldProfile.StaticPropPolicy(
                    draft.Batched,
                    draft.Instanced,
                    draft.Occluder,
                    draft.MergeEligible,
                    draft.StaticCollisionEnabled);
            }
            var onUsed = interaction is not null && draft.OnUsedEnabled
                ? new PlaceableWorldProfile.OnUsedPolicy(
                    draft.OnUsedId,
                    WorldFeatureCatalog.CurrentSchemaVersion,
                    original?.OnUsed?.Provenance ?? "studio:ux1-g-placeable-on-used",
                    original?.OnUsed?.ForgeScriptApiVersion ?? "1.0.0",
                    original?.OnUsed?.EventBinding ?? "InteractionExecuted",
                    NullIfWhiteSpace(draft.BeforeUseScript),
                    NullIfWhiteSpace(draft.AfterUseScript))
                : null;
            var editedContainer = draft.HasContainer
                ? new PlaceableContainerProfile(
                    draft.Container.Id,
                    draft.Container.CapacitySlots,
                    draft.Container.CapacityMass,
                    draft.Container.OwnershipPolicy.Trim(),
                    draft.Container.AccessPermissions.Select(value => value.Trim()).ToArray(),
                    draft.Container.Persistent,
                    draft.Container.LootPolicyId.Trim(),
                    draft.Container.UiProfileId.Trim(),
                    draft.Container.Replicated,
                    draft.Container.InventoryId.Trim())
                : null;
            var container = ContainerValuesEqual(
                editedContainer,
                draft.OriginalOperations?.Container)
                ? draft.OriginalOperations?.Container
                : editedContainer;
            var stateMachine = draft.HasStateMachine
                ? stateMachineChanged
                    ? editedStateMachine
                    : draft.OriginalOperations?.StateMachine
                : draft.OriginalOperations?.StateMachine;
            var damageable = draft.HasHealth
                ? damageableChanged
                    ? editedDamageable
                    : draft.OriginalOperations?.Damageable
                : draft.OriginalOperations?.Damageable;
            var protection = draft.HasProtection
                ? protectionChanged
                    ? editedProtection
                    : draft.OriginalOperations?.Protection
                : draft.OriginalOperations?.Protection;
            var lockProfile = draft.HasLock
                ? lockChanged
                    ? editedLock
                    : draft.OriginalOperations?.Lock
                : draft.OriginalOperations?.Lock;
            PlaceableOperationsProfile? operations;
            if (draft.OriginalOperations is null)
            {
                operations = container is null && stateMachine is null && damageable is null && protection is null &&
                             lockProfile is null
                    ? null
                    : new PlaceableOperationsProfile(
                        draft.OperationsId,
                        WorldFeatureCatalog.CurrentSchemaVersion,
                        container is not null
                            ? "studio:ux1-h-placeable-container"
                            : damageable is not null
                                ? "studio:ux1-l-placeable-destructibility"
                                : protection is not null
                                    ? "studio:ux1-m-placeable-plot-protection"
                                    : lockProfile is not null
                                        ? "studio:ux1-p-placeable-lock"
                                        : "studio:ux1-k-placeable-initial-animation",
                        container,
                        stateMachine ?? CreateContainerCompatibilityStateMachine(draft.BlueprintId),
                        damageable,
                        protection ?? CreateContainerCompatibilityProtection(draft.BlueprintId),
                        lockProfile);
            }
            else
            {
                operations = draft.OriginalOperations with
                {
                    SchemaVersion = WorldFeatureCatalog.CurrentSchemaVersion,
                    Container = container,
                    StateMachine = stateMachine,
                    Damageable = damageable,
                    Protection = protection,
                    Lock = lockProfile,
                };
            }
            var operationsRequiredAtRuntime = operations is not null && draft.RequireOperationsRuntime;
            var factionOwnership = draft.HasFactionOwnership
                ? factionOwnershipChanged
                    ? editedFactionOwnership
                    : draft.OriginalFactionOwnership
                : draft.OriginalFactionOwnership;
            var factionOwnershipRequiredAtRuntime = factionOwnership is not null &&
                                                    draft.RequireFactionOwnershipRuntime;
            var conversation = draft.HasConversation
                ? conversationChanged
                    ? editedConversation
                    : draft.OriginalConversation
                : draft.OriginalConversation;
            var conversationRequiredAtRuntime = conversation is not null && draft.RequireConversationRuntime;
            var credential = draft.HasCredentialPolicy
                ? credentialChanged
                    ? editedCredential
                    : draft.OriginalCredential
                : null;
            var trap = draft.HasTrap
                ? trapChanged
                    ? editedTrap
                    : draft.OriginalSecurityEvents?.Trap
                : null;
            var loot = draft.HasLoot
                ? lootConfigurationChanged || draft.Loot?.ResetGeneratedState == true
                    ? editedLoot
                    : draft.OriginalSecurityEvents?.Loot
                : null;
            var containerEvents = draft.HasContainerEvents
                ? containerEventsChanged
                    ? editedContainerEvents
                    : draft.OriginalSecurityEvents?.ContainerEvents
                : null;
            var lockEvents = draft.HasLockEvents
                ? lockEventsChanged
                    ? editedLockEvents
                    : draft.OriginalSecurityEvents?.LockEvents
                : null;
            PlaceableSecurityEventsProfile? securityEvents;
            if (draft.OriginalSecurityEvents is null)
            {
                securityEvents = trap is null && loot is null && containerEvents is null && lockEvents is null
                    ? null
                    : new PlaceableSecurityEventsProfile(
                        StablePlaceableId(draft.BlueprintId, "security-events"),
                        WorldFeatureCatalog.CurrentSchemaVersion,
                        trap is not null
                            ? "studio:ux1-r-placeable-trap"
                            : loot is not null
                                ? "studio:ux1-s-placeable-initial-treasure"
                                : containerEvents is not null
                                    ? "studio:ux1-t-placeable-container-events"
                                    : "studio:ux1-u-placeable-lock-events",
                        Trap: trap,
                        Loot: loot,
                        ContainerEvents: containerEvents,
                        LockEvents: lockEvents);
            }
            else if (ReferenceEquals(trap, draft.OriginalSecurityEvents.Trap) &&
                     ReferenceEquals(loot, draft.OriginalSecurityEvents.Loot) &&
                     ReferenceEquals(containerEvents, draft.OriginalSecurityEvents.ContainerEvents) &&
                     ReferenceEquals(lockEvents, draft.OriginalSecurityEvents.LockEvents))
            {
                securityEvents = draft.OriginalSecurityEvents;
            }
            else
            {
                securityEvents = draft.OriginalSecurityEvents with
                {
                    Trap = trap,
                    Loot = loot,
                    ContainerEvents = containerEvents,
                    LockEvents = lockEvents,
                };
                if (securityEvents is
                    {
                        Trap: null,
                        Loot: null,
                        ContainerEvents: null,
                        LockEvents: null,
                        ContentsChanged: null,
                    })
                {
                    securityEvents = null;
                }
            }
            var trapRequiredAtRuntime = draft.RequireTrapRuntime;
            var lootRequiredAtRuntime = draft.RequireLootRuntime;
            var containerEventsRequiredAtRuntime = draft.RequireContainerEventsRuntime;
            var lockEventsRequiredAtRuntime = draft.RequireLockEventsRuntime;
            var validation = MergeContainerInventoryReference(
                original?.Validation,
                draft.OriginalOperations?.Container?.InventoryId,
                container?.InventoryId);
            validation = MergeTransformAnimationReference(
                validation,
                NullIfWhiteSpace(draft.TransformAnimationId),
                meshIds.Count,
                bindings.Count);
            validation = MergeStateAnimationReferences(
                validation,
                stateMachineChanged ? stateMachine : null,
                meshIds.Count,
                bindings.Count);
            var foundation = new PlaceableWorldProfile.FoundationPolicy(
                draft.FoundationId,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "studio:ux1-e-placeable-blueprint",
                draft.Slug.Trim(),
                localized,
                tags,
                draft.AuthorNotes.Trim(),
                visual,
                collision,
                interaction,
                prop,
                new(
                    damageable is not null,
                    draft.HasContainer,
                    lockProfile is not null,
                    draft.HasTrap,
                    draft.HasPhysics,
                    bindings),
                new(
                    draft.Position,
                    draft.RotationDegrees,
                    draft.Scale,
                    draft.Pivot,
                    draft.SnapIncrement,
                    draft.ParentId,
                    draft.AlignToSurface,
                    NullIfWhiteSpace(draft.TransformAnimationId)),
                overrides,
                onUsed,
                validation,
                interaction is not null && draft.RequireOnUsedRuntime);

            var existing = currentCatalog.Placeables.FirstOrDefault(candidate => candidate.BlueprintId == draft.BlueprintId);
            profile = existing is null
                ? new PlaceableWorldProfile(
                    draft.ProfileId,
                    WorldFeatureCatalog.CurrentSchemaVersion,
                    "studio:ux1-e-placeable-blueprint",
                    draft.BlueprintId,
                    null,
                    null,
                    credential,
                    new([], new(false, 1, 1, true, "placeable")),
                    null,
                    foundation,
                    draft.RequireRuntime,
                    operations,
                    operationsRequiredAtRuntime,
                    SecurityEvents: securityEvents,
                    FactionOwnership: factionOwnership,
                    FactionOwnershipRequiredAtRuntime: factionOwnershipRequiredAtRuntime,
                    Conversation: conversation,
                    ConversationRequiredAtRuntime: conversationRequiredAtRuntime,
                    CredentialRequiredAtRuntime: draft.RequireCredentialRuntime,
                    TrapRequiredAtRuntime: trapRequiredAtRuntime,
                    LootRequiredAtRuntime: lootRequiredAtRuntime,
                    ContainerEventsRequiredAtRuntime: containerEventsRequiredAtRuntime,
                    LockEventsRequiredAtRuntime: lockEventsRequiredAtRuntime)
                : existing with
                {
                    Foundation = foundation,
                    FoundationRequiredAtRuntime = draft.RequireRuntime,
                    Operations = operations,
                    OperationsRequiredAtRuntime = operationsRequiredAtRuntime,
                    FactionOwnership = factionOwnership,
                    FactionOwnershipRequiredAtRuntime = factionOwnershipRequiredAtRuntime,
                    Conversation = conversation,
                    ConversationRequiredAtRuntime = conversationRequiredAtRuntime,
                    Credential = credential,
                    CredentialRequiredAtRuntime = draft.RequireCredentialRuntime,
                    SecurityEvents = securityEvents,
                    TrapRequiredAtRuntime = trapRequiredAtRuntime,
                    LootRequiredAtRuntime = lootRequiredAtRuntime,
                    ContainerEventsRequiredAtRuntime = containerEventsRequiredAtRuntime,
                    LockEventsRequiredAtRuntime = lockEventsRequiredAtRuntime,
                };
            var placeables = currentCatalog.Placeables
                .Where(candidate => candidate.BlueprintId != draft.BlueprintId)
                .Append(profile)
                .ToArray();
            var candidateCatalog = new WorldFeatureCatalog(
                currentCatalog.Id,
                currentCatalog.SchemaVersion,
                currentCatalog.Provenance,
                placeables,
                currentCatalog.Doors,
                currentCatalog.Triggers,
                currentCatalog.Encounters,
                currentCatalog.VehicleRoutes,
                currentCatalog.Merchants,
                currentCatalog.Companions,
                currentCatalog.Scripts);
            command = new SetWorldFeatureCatalogCommand(
                CommandId.From(Guid.NewGuid()),
                History.State.Revision,
                candidateCatalog,
                dryRun);
            return true;
        }
        catch (ArgumentException)
        {
            message = "La fiche du blueprint est incomplète. Vérifiez le visuel, la collision, le rôle et la transformation.";
            return false;
        }
    }

    private IReadOnlyList<EntityId> PlaceableInstances(BlueprintId blueprintId) =>
        Project?.Areas.SelectMany(area => area.Entities)
            .Where(entity => entity.BlueprintId == blueprintId)
            .Select(entity => entity.Id)
            .ToArray() ?? [];

    private static IReadOnlyList<PlaceableWorldProfile.LocalizedText> MergeLocalizedTexts(
        IReadOnlyList<StudioPlaceableLocalizedTextDraft> additional,
        string name,
        string prompt)
    {
        var values = additional
            .Select(item => new PlaceableWorldProfile.LocalizedText(
                item.Locale.Trim(),
                item.Name.Trim(),
                item.Prompt.Trim()))
            .Append(new("fr-FR", name, prompt))
            .OrderBy(item => item.Locale, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return values;
    }

    private static PlaceableWorldProfile.RuntimeValidationPolicy? MergeContainerInventoryReference(
        PlaceableWorldProfile.RuntimeValidationPolicy? validation,
        string? previousInventoryId,
        string? nextInventoryId)
    {
        if (validation is null)
        {
            return null;
        }
        var inventories = new List<string>();
        foreach (var inventoryId in validation.InventoryIds)
        {
            var value = string.Equals(inventoryId, previousInventoryId, StringComparison.Ordinal)
                ? nextInventoryId
                : inventoryId;
            if (!string.IsNullOrWhiteSpace(value) && !inventories.Contains(value, StringComparer.Ordinal))
            {
                inventories.Add(value);
            }
        }
        if (!string.IsNullOrWhiteSpace(nextInventoryId) && !inventories.Contains(nextInventoryId, StringComparer.Ordinal))
        {
            inventories.Add(nextInventoryId);
        }
        return inventories.SequenceEqual(validation.InventoryIds, StringComparer.Ordinal)
            ? validation
            : validation with { InventoryIds = inventories };
    }

    private static PlaceableWorldProfile.RuntimeValidationPolicy? MergeTransformAnimationReference(
        PlaceableWorldProfile.RuntimeValidationPolicy? validation,
        string? animationId,
        int meshCount,
        int scriptBindingCount)
    {
        if (string.IsNullOrWhiteSpace(animationId))
        {
            return validation;
        }
        if (validation is null)
        {
            return new(
                [animationId],
                [],
                [],
                [],
                [],
                Math.Max(1, meshCount),
                Math.Max(0, scriptBindingCount));
        }
        if (validation.AnimationIds.Contains(animationId, StringComparer.Ordinal))
        {
            return validation;
        }
        return validation with { AnimationIds = validation.AnimationIds.Append(animationId).ToArray() };
    }

    private static PlaceableWorldProfile.RuntimeValidationPolicy? MergeStateAnimationReferences(
        PlaceableWorldProfile.RuntimeValidationPolicy? validation,
        PlaceableVisualStateMachineProfile? stateMachine,
        int meshCount,
        int scriptBindingCount)
    {
        var animationIds = stateMachine?.States
            .Select(state => state.AnimationId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];
        if (animationIds.Length == 0)
        {
            return validation;
        }
        if (validation is null)
        {
            return new(
                animationIds,
                [],
                [],
                [],
                [],
                Math.Max(1, meshCount),
                Math.Max(0, scriptBindingCount));
        }
        var merged = validation.AnimationIds
            .Concat(animationIds.Where(animationId =>
                !validation.AnimationIds.Contains(animationId, StringComparer.Ordinal)))
            .ToArray();
        return merged.SequenceEqual(validation.AnimationIds, StringComparer.Ordinal)
            ? validation
            : validation with { AnimationIds = merged };
    }

    private static bool TryBuildPlaceableStateMachine(
        StudioPlaceableStateMachineDraft draft,
        out PlaceableVisualStateMachineProfile? stateMachine,
        out string message)
    {
        stateMachine = null;
        message = string.Empty;
        var stateIds = draft.States.Select(state => state.StateId.Trim()).ToArray();
        var required = new[] { "idle", "open", "closed", "broken", "active", "custom" };
        if (draft.Id == Guid.Empty || string.IsNullOrWhiteSpace(draft.InitialStateId) ||
            !stateIds.Contains(draft.InitialStateId.Trim(), StringComparer.Ordinal) ||
            required.Except(stateIds, StringComparer.Ordinal).Any() ||
            stateIds.Any(string.IsNullOrWhiteSpace) ||
            stateIds.Distinct(StringComparer.Ordinal).Count() != stateIds.Length)
        {
            message =
                "La machine visuelle exige un état initial valide et les états Au repos, Ouvert, Fermé, Brisé, Actif et Personnalisé.";
            return false;
        }
        if (draft.States.Any(state => state.Id == Guid.Empty ||
                                      string.IsNullOrWhiteSpace(state.AnimationId) ||
                                      string.IsNullOrWhiteSpace(state.VisualOverrideId) ||
                                      state.AnimationId.Trim().Length > 256 ||
                                      state.VisualOverrideId.Trim().Length > 256))
        {
            message = "Chaque état visuel exige une animation et une apparence valides.";
            return false;
        }
        if (draft.Transitions.Any(transition => transition.Id == Guid.Empty ||
                                               !stateIds.Contains(transition.FromStateId.Trim(), StringComparer.Ordinal) ||
                                               !stateIds.Contains(transition.ToStateId.Trim(), StringComparer.Ordinal) ||
                                               string.IsNullOrWhiteSpace(transition.Trigger) ||
                                               transition.Trigger.Trim().Length > 256 ||
                                               !double.IsFinite(transition.DurationSeconds) ||
                                               transition.DurationSeconds is < 0 or > 3_600 ||
                                               transition.Condition.Trim().Length > 512))
        {
            message =
                "Chaque transition exige un départ, une arrivée, un déclencheur et une durée comprise entre 0 et 3 600 secondes.";
            return false;
        }
        var identities = new[] { draft.Id }
            .Concat(draft.States.Select(state => state.Id))
            .Concat(draft.Transitions.Select(transition => transition.Id))
            .ToArray();
        if (identities.Distinct().Count() != identities.Length)
        {
            message = "Les états et transitions contiennent une identité interne en conflit.";
            return false;
        }

        stateMachine = new(
            draft.Id,
            draft.InitialStateId.Trim(),
            draft.States.Select(state => new PlaceableVisualStateProfile(
                state.Id,
                state.StateId.Trim(),
                state.AnimationId.Trim(),
                state.VisualOverrideId.Trim(),
                state.Interactable)).ToArray(),
            draft.Transitions.Select(transition => new PlaceableVisualTransitionProfile(
                transition.Id,
                transition.FromStateId.Trim(),
                transition.ToStateId.Trim(),
                transition.Trigger.Trim(),
                transition.DurationSeconds,
                NullIfWhiteSpace(transition.Condition))).ToArray());
        return true;
    }

    private static bool TryBuildPlaceableDamageable(
        StudioPlaceableDamageableDraft draft,
        out PlaceableDamageableProfile? damageable,
        out string message)
    {
        damageable = null;
        message = string.Empty;
        if (draft.Id == Guid.Empty || !double.IsFinite(draft.MaximumHealth) ||
            draft.MaximumHealth is <= 0 or > 1_000_000_000 || !double.IsFinite(draft.CurrentHealth) ||
            draft.CurrentHealth < 0 || draft.CurrentHealth > draft.MaximumHealth ||
            !double.IsFinite(draft.Armor) || draft.Armor is < 0 or > 1_000_000_000)
        {
            message = "La destructibilité exige une santé maximale positive, une santé actuelle comprise dans cette limite et une armure positive ou nulle.";
            return false;
        }
        var damageTypes = draft.Resistances.Select(item => item.DamageType.Trim()).ToArray();
        if (draft.Resistances.Count == 0 || damageTypes.Any(string.IsNullOrWhiteSpace) ||
            damageTypes.Any(value => value.Length > 128) ||
            damageTypes.Distinct(StringComparer.Ordinal).Count() != damageTypes.Length ||
            draft.Resistances.Any(item => !double.IsFinite(item.Ratio) || item.Ratio is < 0 or > 1))
        {
            message = "Ajoutez au moins une résistance avec un type unique et une réduction comprise entre 0 et 100 %.";
            return false;
        }
        if (draft.VisualStageThresholds.Count == 0 ||
            draft.VisualStageThresholds.Any(value => !double.IsFinite(value) || value is < 0 or > 1) ||
            draft.VisualStageThresholds.Distinct().Count() != draft.VisualStageThresholds.Count)
        {
            message = "Ajoutez au moins une étape visuelle unique, comprise entre 0 et 100 % de santé.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(draft.DestructionEffectId) ||
            draft.DestructionEffectId.Trim().Length > 256 ||
            string.IsNullOrWhiteSpace(draft.DebrisDefinitionId) ||
            draft.DebrisDefinitionId.Trim().Length > 256)
        {
            message = "Choisissez un effet de destruction et une définition de débris.";
            return false;
        }
        if (!double.IsFinite(draft.RepairCost) || draft.RepairCost is < 0 or > 1_000_000_000)
        {
            message = "Le coût de réparation doit être un nombre positif ou nul.";
            return false;
        }

        damageable = new(
            draft.Id,
            draft.MaximumHealth,
            draft.CurrentHealth,
            draft.Armor,
            draft.Resistances.ToDictionary(
                item => item.DamageType.Trim(),
                item => item.Ratio,
                StringComparer.Ordinal),
            draft.VisualStageThresholds.ToArray(),
            draft.DestructionEffectId.Trim(),
            draft.DebrisDefinitionId.Trim(),
            draft.Repairable,
            draft.RepairCost);
        return true;
    }

    private static bool DamageableValuesEqual(
        PlaceableDamageableProfile? left,
        PlaceableDamageableProfile? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }
        return left.Id == right.Id &&
               left.MaximumHealth.Equals(right.MaximumHealth) &&
               left.CurrentHealth.Equals(right.CurrentHealth) &&
               left.Armor.Equals(right.Armor) &&
               left.Resistances.OrderBy(item => item.Key, StringComparer.Ordinal)
                   .SequenceEqual(right.Resistances.OrderBy(item => item.Key, StringComparer.Ordinal)) &&
               left.VisualStageThresholds.SequenceEqual(right.VisualStageThresholds) &&
               left.DestructionEffectId == right.DestructionEffectId &&
               left.DebrisDefinitionId == right.DebrisDefinitionId &&
               left.Repairable == right.Repairable &&
               left.RepairCost.Equals(right.RepairCost);
    }

    private static bool TryBuildPlaceableProtection(
        StudioPlaceableProtectionDraft draft,
        out PlaceableProtectionProfile? protection,
        out string message)
    {
        protection = null;
        message = string.Empty;
        var actions = draft.ProtectedActions.Select(value => value.Trim()).ToArray();
        if (draft.Id == Guid.Empty || actions.Length == 0 ||
            actions.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 128) ||
            actions.Distinct(StringComparer.Ordinal).Count() != actions.Length)
        {
            message = "Choisissez au moins une action protégée unique pour cette politique plot.";
            return false;
        }
        var permissions = draft.RequiredPermissions.Select(value => value.Trim()).ToArray();
        if (permissions.Length == 0 ||
            permissions.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 128) ||
            permissions.Distinct(StringComparer.Ordinal).Count() != permissions.Length)
        {
            message = "Choisissez au moins un droit unique permettant de contourner la protection.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(draft.Feedback) || draft.Feedback.Trim().Length > 512)
        {
            message = "Rédigez le message affiché lorsqu’une action protégée est refusée.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(draft.AuditBinding) || draft.AuditBinding.Trim().Length > 256)
        {
            message = "La protection exige un journal d’audit gouverné pour les dérogations MJ.";
            return false;
        }

        protection = new(
            draft.Id,
            actions,
            permissions,
            draft.Feedback.Trim(),
            draft.AllowGameMasterOverride,
            draft.AuditBinding.Trim());
        return true;
    }

    private static bool ProtectionValuesEqual(
        PlaceableProtectionProfile? left,
        PlaceableProtectionProfile? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }
        return left.Id == right.Id &&
               left.ProtectedActions.SequenceEqual(right.ProtectedActions, StringComparer.Ordinal) &&
               left.RequiredPermissions.SequenceEqual(right.RequiredPermissions, StringComparer.Ordinal) &&
               left.Feedback == right.Feedback &&
               left.AllowGameMasterOverride == right.AllowGameMasterOverride &&
               left.AuditBinding == right.AuditBinding;
    }

    private bool TryBuildPlaceableFactionOwnership(
        StudioPlaceableFactionOwnershipDraft draft,
        out PlaceableFactionOwnershipProfile? ownership,
        out string message)
    {
        ownership = null;
        message = string.Empty;
        var available = Project?.Module.Factions.Factions
            .Where(item => !item.Archived)
            .Select(item => item.Id)
            .ToHashSet() ?? [];
        if (available.Count == 0)
        {
            message = "Ce module ne contient aucune faction active. Créez d’abord une faction dans le catalogue du projet.";
            return false;
        }
        if (draft.Id == Guid.Empty || draft.PrimaryFactionId == Guid.Empty ||
            !available.Contains(draft.PrimaryFactionId))
        {
            message = "Choisissez une faction principale active pour ce placeable.";
            return false;
        }
        var memberships = draft.MembershipFactionIds.ToArray();
        if (memberships.Length == 0 || memberships.Any(id => id == Guid.Empty || !available.Contains(id)) ||
            memberships.Distinct().Count() != memberships.Length || !memberships.Contains(draft.PrimaryFactionId))
        {
            message = "Choisissez des appartenances uniques et conservez la faction principale parmi les membres.";
            return false;
        }

        ownership = new(
            draft.Id,
            PlaceableFactionOwnershipProfile.CurrentSchemaVersion,
            "studio:ux1-n-placeable-faction",
            draft.PrimaryFactionId,
            memberships,
            draft.InheritHostility,
            draft.UnauthorizedActionsAreCrime,
            draft.MembersOnlyAccess,
            draft.BlockFriendlyTargeting);
        return true;
    }

    private static bool FactionOwnershipValuesEqual(
        PlaceableFactionOwnershipProfile? left,
        PlaceableFactionOwnershipProfile? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }
        return left.Id == right.Id &&
               left.PrimaryFactionId == right.PrimaryFactionId &&
               left.MembershipFactionIds.SequenceEqual(right.MembershipFactionIds) &&
               left.InheritHostility == right.InheritHostility &&
               left.UnauthorizedActionsAreCrime == right.UnauthorizedActionsAreCrime &&
               left.MembersOnlyAccess == right.MembersOnlyAccess &&
               left.BlockFriendlyTargeting == right.BlockFriendlyTargeting;
    }

    private bool TryBuildPlaceableConversation(
        StudioPlaceableConversationDraft draft,
        out PlaceableConversationProfile? conversation,
        out string message)
    {
        conversation = null;
        message = string.Empty;
        if (Project is null || draft.Id == Guid.Empty || draft.Bindings.Count == 0)
        {
            message = "Ajoutez au moins une liaison de conversation à ce placeable.";
            return false;
        }

        var bindings = draft.Bindings.Select(binding => new PlaceableConversationBinding(
            binding.Id,
            binding.DialogueId,
            binding.Trigger,
            binding.Trigger == PlaceableConversationTrigger.Item ? string.Empty : binding.Selector.Trim(),
            binding.Trigger == PlaceableConversationTrigger.Item ? binding.ItemBlueprintId : null,
            binding.EntryPointKey.Trim(),
            binding.Priority,
            binding.OncePerUser)).ToArray();
        conversation = new(
            draft.Id,
            PlaceableConversationProfile.CurrentSchemaVersion,
            "studio:ux1-o-placeable-conversation",
            bindings);
        var entryPoints = Project.Dialogues.ToDictionary(
            dialogue => dialogue.Id.Value,
            dialogue => (IReadOnlySet<string>)dialogue.EntryPoints
                .Select(entry => entry.Key)
                .ToHashSet(StringComparer.Ordinal));
        var items = Project.Blueprints
            .Where(blueprint => blueprint.Kind == BlueprintKind.Item)
            .Select(blueprint => blueprint.Id)
            .ToHashSet();
        var diagnostic = PlaceableConversationProfileValidator.Validate(
                conversation,
                entryPoints,
                items,
                $"module/worldFeatures/placeables/{draft.Id}/conversation")
            .FirstOrDefault();
        if (diagnostic is null)
        {
            return true;
        }

        message = diagnostic.Code switch
        {
            "world.placeable.conversation_dialogue_missing" =>
                "Choisissez un dialogue existant pour chaque liaison.",
            "world.placeable.conversation_entry_point_missing" =>
                "Choisissez un point d’entrée disponible dans le dialogue.",
            "world.placeable.conversation_item_missing" =>
                "Choisissez un objet existant pour le déclencheur Objet.",
            "world.placeable.conversation_priority_invalid" =>
                "La priorité d’une liaison doit rester entre -1000 et 1000.",
            "world.placeable.conversation_selector_invalid" =>
                "Renseignez un déclencheur lisible pour interaction, état, utilisateur ou événement.",
            "world.placeable.conversation_binding_uuid_invalid" =>
                "Les liaisons de conversation doivent être uniques.",
            _ => "La configuration de conversation est incomplète ou invalide.",
        };
        conversation = null;
        return false;
    }

    private static bool ConversationValuesEqual(
        PlaceableConversationProfile? left,
        PlaceableConversationProfile? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }
        return left.Id == right.Id &&
               left.Bindings.SequenceEqual(right.Bindings);
    }

    private static bool TryBuildPlaceableTrap(
        StudioPlaceableTrapDraft draft,
        out PlaceableTrapProfile? trap,
        out string message)
    {
        trap = null;
        message = string.Empty;
        if (draft.Id == Guid.Empty)
        {
            message = "Le piège exige une identité interne stable.";
            return false;
        }
        if (new[]
            {
                draft.TriggerId,
                draft.PayloadId,
                draft.OwnerId,
                draft.VisualEffectId,
                draft.FriendlyPolicy,
            }.Any(string.IsNullOrWhiteSpace))
        {
            message = "Renseignez le déclencheur, l’effet, le propriétaire, le VFX et la politique alliée du piège.";
            return false;
        }
        if (new[]
            {
                draft.TriggerId,
                draft.PayloadId,
                draft.OwnerId,
                draft.VisualEffectId,
                draft.FriendlyPolicy,
            }.Any(value => value.Trim().Length > 256))
        {
            message = "Une identité métier du piège dépasse la longueur autorisée.";
            return false;
        }
        if (!double.IsFinite(draft.DetectionDifficulty) || draft.DetectionDifficulty is < 0 or > 1000 ||
            !double.IsFinite(draft.DisarmDifficulty) || draft.DisarmDifficulty is < 0 or > 1000)
        {
            message = "Les difficultés de détection et de désamorçage doivent rester entre 0 et 1000.";
            return false;
        }
        if (!double.IsFinite(draft.ResetDelaySeconds) || draft.ResetDelaySeconds is < 0 or > 86400)
        {
            message = "Le délai de réarmement doit rester entre 0 seconde et 24 heures.";
            return false;
        }

        trap = new(
            draft.Id,
            draft.TriggerId.Trim(),
            draft.PayloadId.Trim(),
            draft.DetectionDifficulty,
            draft.DisarmDifficulty,
            draft.ResetDelaySeconds,
            draft.OwnerId.Trim(),
            draft.VisualEffectId.Trim(),
            draft.FriendlyPolicy.Trim(),
            draft.Armed,
            draft.OneShot);
        return true;
    }

    private static bool TrapValuesEqual(PlaceableTrapProfile? left, PlaceableTrapProfile? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return left.Id == right.Id &&
               left.TriggerId == right.TriggerId &&
               left.PayloadId == right.PayloadId &&
               left.DetectionDifficulty.Equals(right.DetectionDifficulty) &&
               left.DisarmDifficulty.Equals(right.DisarmDifficulty) &&
               left.ResetDelaySeconds.Equals(right.ResetDelaySeconds) &&
               left.OwnerId == right.OwnerId &&
               left.VisualEffectId == right.VisualEffectId &&
               left.FriendlyPolicy == right.FriendlyPolicy &&
               left.Armed == right.Armed &&
               left.OneShot == right.OneShot;
    }

    private bool TryBuildPlaceableLoot(
        StudioPlaceableLootDraft draft,
        out PlaceableLootSpawnerProfile? loot,
        out string message)
    {
        loot = null;
        message = string.Empty;
        if (Project is null)
        {
            message = "Aucun module n’est ouvert.";
            return false;
        }
        if (draft.Id == Guid.Empty || draft.LootTableId == Guid.Empty || draft.Id == draft.LootTableId)
        {
            message = "Le trésor et sa table exigent deux identités internes stables et distinctes.";
            return false;
        }
        if (draft.Generation < 0)
        {
            message = "La génération enregistrée du trésor ne peut pas être négative.";
            return false;
        }
        if (draft.Entries.Count == 0)
        {
            message = "Ajoutez au moins un objet à la table du trésor initial.";
            return false;
        }
        if (draft.Entries.Any(entry => entry.Id == Guid.Empty) ||
            draft.Entries.Select(entry => entry.Id).Distinct().Count() != draft.Entries.Count)
        {
            message = "Chaque ligne de trésor doit posséder une identité unique.";
            return false;
        }
        var itemIds = Project.Blueprints.Where(item => item.Kind == BlueprintKind.Item)
            .Select(item => item.Id).ToHashSet();
        if (draft.Entries.Any(entry => !itemIds.Contains(entry.ItemBlueprintId)))
        {
            message = "Un objet choisi pour le trésor initial n’existe plus dans le projet.";
            return false;
        }
        if (draft.Entries.Any(entry => string.IsNullOrWhiteSpace(entry.Group) || entry.Group.Trim().Length > 128))
        {
            message = "Chaque objet du trésor exige une catégorie lisible de 128 caractères maximum.";
            return false;
        }
        if (draft.Entries.Any(entry => !double.IsFinite(entry.Weight) || entry.Weight <= 0 ||
                                       entry.Weight > 1_000_000))
        {
            message = "Le poids de chaque objet du trésor doit être strictement positif.";
            return false;
        }
        if (draft.Entries.Any(entry => entry.MinimumQuantity < 1 ||
                                       entry.MaximumQuantity < entry.MinimumQuantity ||
                                       entry.MaximumQuantity > 1_000_000 ||
                                       entry.MinimumQuality is < 0 or > 1_000_000))
        {
            message = "Les quantités du trésor doivent commencer à 1, rester ordonnées, et la qualité doit être positive ou nulle.";
            return false;
        }

        var reset = draft.ResetGeneratedState;
        loot = new(
            draft.Id,
            draft.LootTableId,
            draft.Timing,
            draft.Seed,
            draft.PersistGeneratedState,
            reset ? [] : draft.SavedResults,
            reset ? 0 : draft.Generation,
            new ItemLootTablePolicy(
                draft.LootTableId,
                1,
                "studio:ux1-s-placeable-initial-treasure",
                draft.Entries.Select(entry => new ItemLootEntryPolicy(
                    entry.ItemBlueprintId.Value,
                    entry.Group.Trim(),
                    entry.Weight,
                    entry.MinimumQuantity,
                    entry.MaximumQuantity,
                    entry.MinimumQuality,
                    entry.Condition.Trim(),
                    entry.Unique)).ToArray()));
        return true;
    }

    private static bool LootConfigurationValuesEqual(
        PlaceableLootSpawnerProfile? left,
        PlaceableLootSpawnerProfile? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return left.Id == right.Id &&
               left.LootTableId == right.LootTableId &&
               left.Timing == right.Timing &&
               left.Seed == right.Seed &&
               left.PersistGeneratedState == right.PersistGeneratedState &&
               left.LootTable.Id == right.LootTable.Id &&
               left.LootTable.SchemaVersion == right.LootTable.SchemaVersion &&
               left.LootTable.Entries.SequenceEqual(right.LootTable.Entries);
    }

    private static bool TryBuildPlaceableContainerEvents(
        StudioPlaceableContainerEventsDraft draft,
        out PlaceableContainerStateEventsProfile? containerEvents,
        out string message)
    {
        containerEvents = null;
        message = string.Empty;
        if (draft.Id == Guid.Empty)
        {
            message = "Les événements d’ouverture et de fermeture exigent une identité interne stable.";
            return false;
        }
        if (new[] { draft.ForgeScriptApiVersion, draft.OpenedBinding, draft.ClosedBinding }
            .Any(string.IsNullOrWhiteSpace))
        {
            message = "Renseignez la version ForgeScript et les deux événements d’ouverture et de fermeture.";
            return false;
        }
        if (new[] { draft.ForgeScriptApiVersion, draft.OpenedBinding, draft.ClosedBinding }
            .Any(value => value.Trim().Length > 256))
        {
            message = "La version ForgeScript et les noms d’événements doivent rester sous 256 caractères.";
            return false;
        }
        if (!draft.IncludeActor || !draft.IncludeResult || !draft.IncludePreviousState ||
            !draft.IncludeAuthority)
        {
            message = "Le contexte des événements doit conserver l’acteur, le résultat, l’état précédent et l’autorité.";
            return false;
        }

        containerEvents = new(
            draft.Id,
            draft.ForgeScriptApiVersion.Trim(),
            draft.OpenedBinding.Trim(),
            draft.ClosedBinding.Trim(),
            draft.IncludeActor,
            draft.IncludeResult,
            draft.IncludePreviousState,
            draft.IncludeAuthority);
        return true;
    }

    private static bool ContainerEventValuesEqual(
        PlaceableContainerStateEventsProfile? left,
        PlaceableContainerStateEventsProfile? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return left.Id == right.Id &&
               left.ForgeScriptApiVersion == right.ForgeScriptApiVersion &&
               left.OpenedBinding == right.OpenedBinding &&
               left.ClosedBinding == right.ClosedBinding &&
               left.IncludeActor == right.IncludeActor &&
               left.IncludeResult == right.IncludeResult &&
               left.IncludePreviousState == right.IncludePreviousState &&
               left.IncludeAuthority == right.IncludeAuthority;
    }

    private static bool TryBuildPlaceableLockEvents(
        StudioPlaceableLockEventsDraft draft,
        out PlaceableLockStateEventsProfile? lockEvents,
        out string message)
    {
        lockEvents = null;
        message = string.Empty;
        if (draft.Id == Guid.Empty)
        {
            message = "Les événements de verrouillage et de déverrouillage exigent une identité interne stable.";
            return false;
        }
        if (new[] { draft.ForgeScriptApiVersion, draft.ChangingBinding, draft.ChangedBinding }
            .Any(string.IsNullOrWhiteSpace))
        {
            message = "Renseignez la version ForgeScript et les événements avant et après le changement de verrou.";
            return false;
        }
        if (new[] { draft.ForgeScriptApiVersion, draft.ChangingBinding, draft.ChangedBinding }
            .Any(value => value.Trim().Length > 256))
        {
            message = "La version ForgeScript et les noms d’événements doivent rester sous 256 caractères.";
            return false;
        }
        if (!draft.IncludeMethod || !draft.IncludeCredential || !draft.IncludeActor || !draft.ControlledVeto)
        {
            message = "Le contexte doit conserver la méthode, le justificatif, l’acteur et le veto contrôlé.";
            return false;
        }

        lockEvents = new(
            draft.Id,
            draft.ForgeScriptApiVersion.Trim(),
            draft.ChangingBinding.Trim(),
            draft.ChangedBinding.Trim(),
            draft.IncludeMethod,
            draft.IncludeCredential,
            draft.IncludeActor,
            draft.ControlledVeto);
        return true;
    }

    private static bool LockEventValuesEqual(
        PlaceableLockStateEventsProfile? left,
        PlaceableLockStateEventsProfile? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return left.Id == right.Id &&
               left.ForgeScriptApiVersion == right.ForgeScriptApiVersion &&
               left.ChangingBinding == right.ChangingBinding &&
               left.ChangedBinding == right.ChangedBinding &&
               left.IncludeMethod == right.IncludeMethod &&
               left.IncludeCredential == right.IncludeCredential &&
               left.IncludeActor == right.IncludeActor &&
               left.ControlledVeto == right.ControlledVeto;
    }

    private bool TryBuildPlaceableCredential(
        StudioPlaceableCredentialDraft draft,
        out PlaceableWorldProfile.CredentialPolicy? credential,
        out string message)
    {
        credential = null;
        message = string.Empty;
        if (Project is null)
        {
            message = "Ouvrez un module avant de configurer un justificatif.";
            return false;
        }

        if (draft.ItemBlueprintId is { } itemId && Project.Blueprints.All(blueprint =>
                blueprint.Id != itemId || blueprint.Kind != BlueprintKind.Item))
        {
            message = "Choisissez un objet clé encore présent dans le projet.";
            return false;
        }

        var virtualPermission = draft.VirtualPermission.Trim();
        if (draft.Consumption == PlaceableWorldProfile.CredentialConsumption.VirtualPermission &&
            string.IsNullOrWhiteSpace(virtualPermission))
        {
            message = "Renseignez la permission virtuelle qui autorise cet accès.";
            return false;
        }
        if (virtualPermission.Length > 128)
        {
            message = "Le nom de la permission virtuelle est trop long.";
            return false;
        }

        var credentialId = draft.ItemBlueprintId is { } selectedItem
            ? LockItemCredential(selectedItem)
            : draft.OtherCredentialId.Trim();
        if (draft.Consumption == PlaceableWorldProfile.CredentialConsumption.VirtualPermission &&
            string.IsNullOrWhiteSpace(credentialId))
        {
            credentialId = "permission:" + virtualPermission;
        }
        if (string.IsNullOrWhiteSpace(credentialId) || credentialId.Length > 256)
        {
            message = "Choisissez un objet clé ou nommez un justificatif métier.";
            return false;
        }
        if (credentialId.StartsWith("item:", StringComparison.OrdinalIgnoreCase) &&
            (TryParseLockItemCredential(credentialId) is not { } referencedItem ||
             Project.Blueprints.All(blueprint =>
                 blueprint.Id != referencedItem || blueprint.Kind != BlueprintKind.Item)))
        {
            message = "Le justificatif doit référencer un objet clé existant dans le projet.";
            return false;
        }

        if (draft.Charges < 0 ||
            draft.Consumption == PlaceableWorldProfile.CredentialConsumption.Charge && draft.Charges < 1)
        {
            message = "Une politique à charges exige au moins une charge disponible.";
            return false;
        }

        credential = new(
            credentialId,
            draft.Consumption,
            draft.Charges,
            draft.Consumption == PlaceableWorldProfile.CredentialConsumption.VirtualPermission
                ? virtualPermission
                : null,
            draft.AuditUse);
        return true;
    }

    private static bool CredentialValuesEqual(
        PlaceableWorldProfile.CredentialPolicy? left,
        PlaceableWorldProfile.CredentialPolicy? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return left.CredentialId == right.CredentialId &&
               left.Consumption == right.Consumption &&
               left.Charges == right.Charges &&
               left.VirtualPermission == right.VirtualPermission &&
               left.AuditUse == right.AuditUse;
    }

    private bool TryBuildPlaceableLock(
        StudioPlaceableLockDraft draft,
        out PlaceableLockProfile? lockProfile,
        out string message)
    {
        lockProfile = null;
        message = string.Empty;
        if (Project is null || draft.Id == Guid.Empty)
        {
            message = "Le verrou exige une identité interne stable.";
            return false;
        }

        if (!double.IsFinite(draft.Difficulty) || draft.Difficulty is < 0 or > 1000)
        {
            message = "La difficulté du verrou doit rester entre 0 et 1000.";
            return false;
        }

        if (draft.KeyItemId is { } keyItemId && Project.Blueprints.All(blueprint =>
                blueprint.Id != keyItemId || blueprint.Kind != BlueprintKind.Item))
        {
            message = "Choisissez une clé encore présente dans les objets du projet.";
            return false;
        }

        var credentials = (draft.KeyItemId is null
                ? Enumerable.Empty<string>()
                : [LockItemCredential(draft.KeyItemId)])
            .Concat(draft.OtherCredentialIds.Select(value => value.Trim()))
            .ToArray();
        if (credentials.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 256) ||
            credentials.Distinct(StringComparer.Ordinal).Count() != credentials.Length ||
            credentials.Where(value => value.StartsWith("item:", StringComparison.OrdinalIgnoreCase))
                .Any(value => TryParseLockItemCredential(value) is not { } itemId ||
                              Project.Blueprints.All(blueprint =>
                                  blueprint.Id != itemId || blueprint.Kind != BlueprintKind.Item)))
        {
            message = "Les clés et justificatifs doivent être uniques et référencer des objets existants.";
            return false;
        }

        var permissions = draft.RequiredPermissions.Select(value => value.Trim()).ToArray();
        if (permissions.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 128) ||
            permissions.Distinct(StringComparer.Ordinal).Count() != permissions.Length)
        {
            message = "Les droits de déverrouillage doivent être uniques et nommés.";
            return false;
        }

        var skillId = draft.SkillId.Trim();
        if (draft.Hackable && string.IsNullOrWhiteSpace(skillId))
        {
            message = "Le piratage exige une compétence ou une spécialité.";
            return false;
        }
        if (skillId.Length > 128)
        {
            message = "Le nom de la compétence est trop long.";
            return false;
        }

        string? codeHash = null;
        if (draft.CodeEnabled)
        {
            codeHash = !string.IsNullOrWhiteSpace(draft.AccessCode)
                ? HashLockCode(draft.AccessCode)
                : draft.ExistingCodeHash;
            if (string.IsNullOrWhiteSpace(codeHash))
            {
                message = "Saisissez un code d’accès avant de l’activer.";
                return false;
            }
        }

        var mechanismCount = credentials.Length + permissions.Length +
                             (string.IsNullOrWhiteSpace(skillId) ? 0 : 1) +
                             (codeHash is null ? 0 : 1) + (draft.Hackable ? 1 : 0);
        if (mechanismCount == 0)
        {
            message = "Ajoutez au moins une clé, une compétence, un code, un piratage ou un droit.";
            return false;
        }

        lockProfile = new(
            draft.Id,
            draft.InitialState,
            credentials,
            string.IsNullOrWhiteSpace(skillId) ? null : skillId,
            draft.Difficulty,
            codeHash,
            draft.Hackable,
            draft.Relockable,
            draft.CanJam,
            permissions);
        return true;
    }

    private static bool LockValuesEqual(PlaceableLockProfile? left, PlaceableLockProfile? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return left.Id == right.Id &&
               left.InitialState == right.InitialState &&
               left.CredentialIds.SequenceEqual(right.CredentialIds, StringComparer.Ordinal) &&
               left.SkillId == right.SkillId &&
               left.Difficulty.Equals(right.Difficulty) &&
               left.CodeHash == right.CodeHash &&
               left.Hackable == right.Hackable &&
               left.Relockable == right.Relockable &&
               left.CanJam == right.CanJam &&
               left.RequiredPermissions.SequenceEqual(right.RequiredPermissions, StringComparer.Ordinal);
    }

    private static string LockItemCredential(BlueprintId itemId) => $"item:{itemId.Value:D}";

    private static BlueprintId? TryParseLockItemCredential(string value) =>
        value.StartsWith("item:", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(value[5..], out var itemId)
            ? BlueprintId.From(itemId)
            : null;

    private static string HashLockCode(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        return "sha256:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))
            .ToLowerInvariant();
    }

    private static string HumanizeLockState(PlaceableAuthoredLockState state) => state switch
    {
        PlaceableAuthoredLockState.Unlocked => "Déverrouillé au chargement",
        PlaceableAuthoredLockState.Locked => "Verrouillé au chargement",
        PlaceableAuthoredLockState.Jammed => "Brouillé au chargement",
        PlaceableAuthoredLockState.Broken => "Brisé au chargement",
        _ => "État de verrou",
    };

    private static string HumanizeCredentialConsumption(
        PlaceableWorldProfile.CredentialConsumption consumption) => consumption switch
        {
            PlaceableWorldProfile.CredentialConsumption.Retain => "conserver après usage",
            PlaceableWorldProfile.CredentialConsumption.Consume => "consommer après usage",
            PlaceableWorldProfile.CredentialConsumption.Charge => "dépenser une charge",
            PlaceableWorldProfile.CredentialConsumption.Shared => "partager sans consommer",
            PlaceableWorldProfile.CredentialConsumption.VirtualPermission => "vérifier une permission virtuelle",
            _ => "politique d’accès",
        };

    private string FactionDisplayName(Guid factionId) =>
        Project?.Module.Factions.Factions.FirstOrDefault(item => item.Id == factionId)?.DisplayName ??
        "Faction indisponible";

    private static bool ValidPlaceableTransformDraft(StudioPlaceableDraft draft)
    {
        var bounded = new[]
        {
            draft.Position.X, draft.Position.Y, draft.Position.Z,
            draft.RotationDegrees.X, draft.RotationDegrees.Y, draft.RotationDegrees.Z,
            draft.Pivot.X, draft.Pivot.Y, draft.Pivot.Z,
        };
        var scales = new[] { draft.Scale.X, draft.Scale.Y, draft.Scale.Z };
        return bounded.All(value => float.IsFinite(value) && Math.Abs(value) <= 1_000_000) &&
               scales.All(value => float.IsFinite(value) && value is >= .01f and <= 100) &&
               double.IsFinite(draft.SnapIncrement) && draft.SnapIncrement is >= 0 and <= 10_000 &&
               (string.IsNullOrWhiteSpace(draft.TransformAnimationId) ||
                draft.TransformAnimationId.Trim().Length <= 256);
    }

    private static bool WouldCreatePlaceableParentCycle(
        IReadOnlyList<PlaceableWorldProfile> placeables,
        Guid profileId,
        Guid? parentId)
    {
        var parents = placeables.ToDictionary(
            candidate => candidate.Id,
            candidate => candidate.Id == profileId
                ? parentId
                : candidate.Foundation?.Transform.ParentId);
        var visited = new HashSet<Guid> { profileId };
        var current = parentId;
        while (current is { } candidateId)
        {
            if (!visited.Add(candidateId))
            {
                return true;
            }
            current = parents.GetValueOrDefault(candidateId);
        }
        return false;
    }

    private static PlaceableVisualStateMachineProfile CreateContainerCompatibilityStateMachine(
        BlueprintId blueprintId)
    {
        var states = new[] { "idle", "open", "closed", "broken", "active", "custom" }
            .Select(state => new PlaceableVisualStateProfile(
                StablePlaceableId(blueprintId, $"container-state-{state}"),
                state,
                $"animation:{state}",
                $"visual:{state}",
                state != "broken"))
            .ToArray();
        return new(
            StablePlaceableId(blueprintId, "container-state-machine"),
            "closed",
            states,
            []);
    }

    private static StudioPlaceableStateMachineDraft CreatePlaceableStateMachineDraft(
        BlueprintId blueprintId,
        string initialStateId)
    {
        var states = new[] { "idle", "open", "closed", "broken", "active", "custom" }
            .Select(state => new StudioPlaceableVisualStateDraft(
                StablePlaceableId(blueprintId, $"visual-state-{state}"),
                state,
                $"animation:{state}",
                $"visual:{state}",
                state != "broken"))
            .ToArray();
        return new(
            StablePlaceableId(blueprintId, "visual-state-machine"),
            initialStateId,
            states,
            [],
            false);
    }

    private static PlaceableProtectionProfile CreateContainerCompatibilityProtection(BlueprintId blueprintId) =>
        new(
            StablePlaceableId(blueprintId, "container-protection"),
            ["container.access"],
            ["placeable.operations.override"],
            "Accès autorisé par la politique du conteneur.",
            true,
            "PlaceableProtectionOverride");

    private static PlaceableWorldProfile CreatePlaceableTransformParent(
        BlueprintDefinition blueprint,
        Guid profileId,
        string provenance)
    {
        var assetId = $"asset:{blueprint.Slug}";
        var foundation = new PlaceableWorldProfile.FoundationPolicy(
            StablePlaceableId(blueprint.Id, "transform-parent-foundation"),
            WorldFeatureCatalog.CurrentSchemaVersion,
            provenance,
            blueprint.Slug,
            [new("fr-FR", blueprint.DisplayName, "Observer")],
            ["ancrage", "parent"],
            "Ancrage canonique de transformation.",
            new(assetId, [$"mesh:{blueprint.Slug}"], [], assetId, assetId, []),
            new("box", Vector3Value.One, true, true, "world"),
            null,
            new(true, true, false, false, true),
            new(false, false, false, false, false, new Dictionary<string, string>(StringComparer.Ordinal)),
            new(Vector3Value.Zero, Vector3Value.Zero, Vector3Value.One, Vector3Value.Zero, 0, null, true, null),
            new Dictionary<string, string>(StringComparer.Ordinal));
        return new(
            profileId,
            WorldFeatureCatalog.CurrentSchemaVersion,
            provenance,
            blueprint.Id,
            null,
            null,
            null,
            new([], new(false, 1, 1, true, "placeable-parent")),
            null,
            foundation,
            true);
    }

    private static IReadOnlyList<string> SplitValues(string text) => text
        .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    private static bool TryParsePairs(
        string text,
        string noun,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var token in text.Split([';', '\n', '\r'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = token.IndexOf('=');
            if (separator <= 0 || separator == token.Length - 1)
            {
                values = parsed;
                message = $"Chaque {noun} doit suivre la forme nom=valeur.";
                return false;
            }
            var key = token[..separator].Trim();
            var value = token[(separator + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value) || !parsed.TryAdd(key, value))
            {
                values = parsed;
                message = $"Chaque {noun} doit avoir un nom unique et une valeur.";
                return false;
            }
        }
        values = parsed;
        message = string.Empty;
        return true;
    }

    private static string FormatPairs(IReadOnlyDictionary<string, string>? values) => values is null
        ? string.Empty
        : string.Join("; ", values.OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => $"{item.Key}={item.Value}"));

    private static string? NullIfWhiteSpace(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string PlaceableVerbLabelKey(string slug, StudioPlaceableVerbDraft verb)
    {
        if (!string.IsNullOrWhiteSpace(verb.OriginalLabelKey) &&
            string.Equals(verb.Label.Trim(), verb.OriginalLabel?.Trim(), StringComparison.Ordinal))
        {
            return verb.OriginalLabelKey;
        }
        var decomposed = verb.Label.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        var separator = false;
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }
            if (char.IsLetterOrDigit(character))
            {
                if (separator && builder.Length > 0)
                {
                    builder.Append('-');
                }
                builder.Append(char.ToLowerInvariant(character));
                separator = false;
            }
            else
            {
                separator = true;
            }
        }
        var key = builder.Length == 0 ? "action" : builder.ToString();
        return $"{slug}.verb.{key}";
    }

    private static string HumanizePlaceableVerb(string labelKey)
    {
        var token = labelKey.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "action";
        var words = token.Replace('-', ' ').Replace('_', ' ');
        return CultureInfo.GetCultureInfo("fr-FR").TextInfo.ToTitleCase(words);
    }

    private static string HumanizeContainerOwnership(string value) => value.Trim().ToLowerInvariant() switch
    {
        "party" => "groupe",
        "owner" => "propriétaire de l’objet",
        "world" => "monde partagé",
        "gm" => "maître de jeu",
        _ => value.Trim(),
    };

    private static string HumanizeContainerAccess(IReadOnlyList<string> permissions)
    {
        var labels = permissions.Select(permission => permission.Trim() switch
        {
            "container.open" => "ouvrir",
            "container.deposit" => "déposer",
            "container.withdraw" => "retirer",
            var other => other,
        });
        return string.Join(" · ", labels);
    }

    private static string HumanizePlaceableVisualState(string stateId) => stateId.Trim() switch
    {
        "idle" => "Au repos",
        "open" => "Ouvert",
        "closed" => "Fermé",
        "broken" => "Brisé",
        "active" => "Actif",
        "custom" => "Personnalisé",
        var other => HumanizePlaceableVerb(other),
    };

    private static Guid StablePlaceableId(BlueprintId blueprintId, string purpose)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"openrpg-forge:studio:ux1-e:{blueprintId.Value:D}:{purpose}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16]);
    }

    private static string HumanizeAssetManifest(AssetPackageManifest manifest)
    {
        var variant = manifest.ModelMetadata?.Variants.FirstOrDefault(candidate =>
            !string.IsNullOrWhiteSpace(candidate.DisplayName))?.DisplayName;
        if (!string.IsNullOrWhiteSpace(variant))
        {
            return variant.Trim();
        }
        var modelPath = manifest.Chunks.First(chunk => chunk.Role == AssetChunkRole.Model).RelativePath;
        var folder = Path.GetFileName(Path.GetDirectoryName(modelPath)) ?? "modèle 3D";
        var prefixes = new[]
        {
            "kenney-fantasy-town-kit-", "kenney-castle-kit-", "kenney-nature-kit-",
            "kenney-mini-forest-", "kaykit-dungeon-remastered-1-0-",
            "kaykit-character-pack-adventures-1-0-", "polyhaven-",
        };
        foreach (var prefix in prefixes)
        {
            if (folder.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                folder = folder[prefix.Length..];
                break;
            }
        }
        var words = folder.Replace('-', ' ').Replace('_', ' ');
        return CultureInfo.GetCultureInfo("fr-FR").TextInfo.ToTitleCase(words);
    }

    private static string HumanizePlaceable(string? code) => code switch
    {
        "world.placeable.identity_invalid" => "Le nom court et le nom visible du blueprint sont requis.",
        "world.placeable.visual_invalid" => "Choisissez un visuel 3D complet pour ce blueprint.",
        "world.placeable.metadata_invalid" => "Les tags et surcharges doivent être uniques et non vides.",
        "world.placeable.collision_invalid" => "La collision active exige une taille positive et cohérente avec la navigation.",
        "world.placeable.role_invalid" => "Choisissez exactement un rôle : décor statique ou objet utilisable.",
        "world.placeable.static_invalid" => "Un décor doit être regroupé ou instancié ; la fusion exclut une collision par instance.",
        "world.placeable.interaction_invalid" => "L’interaction exige une portée, un angle, une durée et une action valides.",
        "world.placeable.on_used_invalid" => "L’événement d’utilisation exige une action utilisable et un contrat de script valide.",
        "world.placeable.on_used_required_missing" => "L’événement d’utilisation est requis au runtime mais il est désactivé.",
        "world.placeable.container_invalid" =>
            "Le conteneur exige des limites, un propriétaire, des accès et trois références complètes.",
        "world.placeable.container_required_missing" =>
            "Le composant conteneur est actif mais son profil canonique est absent.",
        "world.placeable.operations_required_missing" or "WORLD_PLACEABLE_OPERATIONS_REQUIRED_MISSING" =>
            "Le profil d’opérations est requis au runtime mais il est absent.",
        "world.placeable.operations_identity_conflict" =>
            "Les identités internes du conteneur entrent en conflit avec une autre capacité.",
        "world.placeable.lock_invalid" =>
            "Le verrou exige un état et au moins une clé, compétence, code, méthode de piratage ou permission valide.",
        "world.placeable.lock_required_missing" or "WORLD_PLACEABLE_LOCK_REQUIRED_MISSING" =>
            "Le verrou est requis au runtime mais son profil canonique est absent.",
        "world.placeable.credential_invalid" or "WORLD_PLACEABLE_CREDENTIAL_INVALID" =>
            "Le justificatif exige une identité et une politique de charges valides.",
        "world.placeable.credential_item_missing" =>
            "L’objet clé choisi pour le justificatif n’existe plus dans le projet.",
        "world.placeable.credential_virtual_permission_missing" =>
            "La politique virtuelle exige une permission nommée.",
        "world.placeable.credential_required_missing" or
            "WORLD_PLACEABLE_CREDENTIAL_REQUIRED_MISSING" =>
            "La politique de justificatif est requise au runtime mais elle est absente.",
        "world.placeable.trap_invalid" or "WORLD_PLACEABLE_TRAP_MISSING" =>
            "Le piège exige un déclencheur, un effet, deux difficultés, un délai, un propriétaire, un VFX et une politique alliée valides.",
        "world.placeable.trap_component_missing" =>
            "Le profil du piège exige que le composant Piège soit actif.",
        "world.placeable.trap_required_missing" or "WORLD_PLACEABLE_TRAP_REQUIRED_MISSING" =>
            "Le piège est requis au runtime mais son profil canonique est absent.",
        "world.placeable.loot_spawner_invalid" or "WORLD_PLACEABLE_LOOT_MISSING" =>
            "Le trésor initial exige une table, un moment, une graine, des objets et un état généré valides.",
        "world.placeable.loot_item_reference_missing" =>
            "Un objet choisi pour le trésor initial n’existe plus dans le projet.",
        "world.placeable.loot_required_missing" or "WORLD_PLACEABLE_LOOT_REQUIRED_MISSING" =>
            "Le trésor initial est requis au runtime mais son profil canonique est absent.",
        "world.placeable.container_events_invalid" or "WORLD_PLACEABLE_CONTAINER_EVENTS_MISSING" =>
            "Les événements d’ouverture et de fermeture exigent deux liaisons et leur contexte complet.",
        "world.placeable.container_events_required_missing" or
            "WORLD_PLACEABLE_CONTAINER_EVENTS_REQUIRED_MISSING" =>
            "Les événements d’ouverture et de fermeture sont requis au runtime mais leur profil canonique est absent.",
        "world.placeable.faction_ownership_identity_invalid" =>
            "Le rattachement de faction exige une identité interne stable.",
        "world.placeable.faction_memberships_invalid" =>
            "La faction principale doit appartenir à une liste de membres unique et non vide.",
        "world.placeable.faction_reference_missing" =>
            "Une faction choisie n’existe plus dans le catalogue du projet.",
        "world.placeable.faction_ownership_required_missing" or
            "WORLD_PLACEABLE_FACTION_OWNERSHIP_REQUIRED_MISSING" =>
            "Le rattachement de faction est requis au runtime mais il est absent.",
        "world.placeable.faction_ownership_identity_conflict" =>
            "L’identité interne du rattachement de faction entre en conflit avec une autre capacité.",
        "world.placeable.faction_ownership_uuid_duplicate" =>
            "Deux placeables partagent la même identité de rattachement de faction.",
        "world.placeable.conversation_identity_invalid" =>
            "La conversation exige une identité interne stable.",
        "world.placeable.conversation_bindings_empty" =>
            "Ajoutez au moins une liaison vers un dialogue.",
        "world.placeable.conversation_binding_uuid_invalid" or
            "world.placeable.conversation_binding_uuid_duplicate" =>
            "Les liaisons de conversation doivent être uniques.",
        "world.placeable.conversation_dialogue_missing" =>
            "Un dialogue choisi n’existe plus dans le projet.",
        "world.placeable.conversation_entry_point_missing" =>
            "Un point d’entrée choisi n’existe plus dans le dialogue.",
        "world.placeable.conversation_item_missing" =>
            "L’objet choisi pour déclencher la conversation n’existe plus.",
        "world.placeable.conversation_selector_invalid" =>
            "Renseignez un déclencheur lisible pour cette liaison de conversation.",
        "world.placeable.conversation_priority_invalid" =>
            "La priorité de conversation doit rester entre -1000 et 1000.",
        "world.placeable.conversation_required_missing" or
            "WORLD_PLACEABLE_CONVERSATION_REQUIRED_MISSING" =>
            "La conversation est requise au runtime mais son profil est absent.",
        "world.placeable.conversation_identity_conflict" or
            "world.placeable.conversation_uuid_duplicate" =>
            "L’identité interne de conversation entre en conflit avec une autre capacité.",
        "world.placeable.state_machine_invalid" =>
            "Les états visuels exigent un état initial, six apparences complètes et des transitions valides.",
        "world.placeable.state_machine_required_missing" =>
            "Le profil d’opérations exige une machine d’états visuels.",
        "world.placeable.inventory_reference_missing" =>
            "Le conteneur doit référencer un inventaire unique.",
        "world.placeable.transform_invalid" => "La position, l’échelle, le pivot ou le pas de grille sort des limites autorisées.",
        "world.placeable.parent_missing" => "Le parent choisi n’existe plus ou ne possède pas de transformation canonique.",
        "world.placeable.parent_cycle" => "Le parent choisi crée une boucle de transformation.",
        "world.placeable.animation_invalid" or "world.placeable.animation_reference_missing" =>
            "L’animation de transformation doit référencer une animation gouvernée du placeable.",
        "world.placeable.scripts_invalid" => "Chaque événement de script doit pointer vers une route non vide.",
        "world.placeable.foundation_required_missing" or "WORLD_PLACEABLE_FOUNDATION_REQUIRED_MISSING" =>
            "La fiche est requise au runtime mais elle est absente.",
        "world.reference.blueprint_missing" or "world.reference.blueprint_kind_mismatch" =>
            "Le blueprint de décor ciblé n’existe plus ou n’est pas du bon type.",
        "command.revision_conflict" => "Le module a changé depuis l’aperçu. Prévisualisez à nouveau.",
        _ => "La fiche du blueprint a été refusée. Vérifiez les champs signalés avant de réessayer.",
    };

    private void RecordPlaceableDiagnostic(string code, string message) => AddOperationalDiagnostic(
        code,
        message,
        target: null,
        documentPath: "placement",
        Forge.Application.Creator.CreatorDiagnosticSeverity.Error,
        "placeable:blueprint");

    private static bool IsHumanPlaceableMessage(string message) =>
        !message.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains(".json", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("world.placeable", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("module/", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("\\", StringComparison.OrdinalIgnoreCase);

    private static StudioPlaceablePreview FailedPlaceablePreview(StudioPlaceableDraft draft, string message) => new(
        false,
        message,
        "Aucun changement appliqué.",
        false,
        draft.BlueprintId,
        [],
        Vector3Value.Zero,
        string.Empty,
        string.Empty);

    private static StudioUx1ESmokeResult FailedUx1ESmoke() => new(
        false, false, false, false, false, false, false, false, false, false, false, false, false);

    private void CloseUx1E() => _approvedPlaceable = null;
}
