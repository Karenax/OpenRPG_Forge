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
        var contentsChangedProfile = securityEvents?.ContentsChanged;
        var canonicalDamageEvents = profile is null ? null : PlaceableDamageEventsResolver.Resolve(profile);
        var damageEventsProfile = canonicalDamageEvents?.Profile ?? profile?.DamageEvents ??
                                  profile?.CompositionEvents?.DamageEvents;
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
            OnUsedForgeScriptApiVersion = onUsed?.ForgeScriptApiVersion ?? "1.0.0",
            OnUsedEventBinding = onUsed?.EventBinding ?? "InteractionExecuted",
            HasContentsChanged = contentsChangedProfile is not null,
            ContentsChanged = new StudioPlaceableContentsChangedDraft(
                contentsChangedProfile?.Id ?? StablePlaceableId(blueprint.Id, "contents-changed"),
                contentsChangedProfile?.ForgeScriptApiVersion ?? "1.0.0",
                contentsChangedProfile?.EventBinding ?? "ContainerContentsChanged",
                contentsChangedProfile?.IncludeTransactionalDelta ?? true,
                contentsChangedProfile?.IncludeCause ?? true,
                contentsChangedProfile?.IncludeRevision ?? true,
                contentsChangedProfile?.IncludeActor ?? true,
                contentsChangedProfile is not null),
            RequireContentsChangedRuntime = profile?.ContentsChangedRequiredAtRuntime ?? false,
            HasDamageEvents = damageEventsProfile is not null,
            DamageEvents = new StudioPlaceableDamageEventsDraft(
                damageEventsProfile?.Id ?? StablePlaceableId(blueprint.Id, "damage-events"),
                damageEventsProfile?.ForgeScriptApiVersion ?? "1.0.0",
                damageEventsProfile?.DamagedBinding ?? "DamageApplied",
                damageEventsProfile?.DestroyedBinding ?? "EntityDestroyed",
                damageEventsProfile?.IncludeSource ?? true,
                damageEventsProfile?.IncludeAmount ?? true,
                damageEventsProfile?.IncludeDamageType ?? true,
                damageEventsProfile?.IncludePreviousHealth ?? true,
                damageEventsProfile?.IncludeCurrentHealth ?? true,
                damageEventsProfile?.IncludeContext ?? true,
                damageEventsProfile is not null),
            RequireDamageEventsRuntime = profile?.DamageEventsRequiredAtRuntime ?? false,
            OriginalDamageEvents = profile?.DamageEvents,
            OriginalCompositionEvents = profile?.CompositionEvents,
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
            draft.HasContentsChanged, draft.HasDamageEvents,
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
               $"{draft.OnUsedEventBinding.Trim()} · API {draft.OnUsedForgeScriptApiVersion.Trim()} · " +
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
        var contentsChangedSummary = draft.HasContentsChanged && draft.ContentsChanged is { } disturbedDraft
            ? "\nContenu modifié : " + disturbedDraft.EventBinding.Trim() + " · API " +
              disturbedDraft.ForgeScriptApiVersion.Trim() + "\n" +
              "Contexte : delta transactionnel · cause · révisions · acteur · " +
              (draft.RequireContentsChangedRuntime ? "requis au runtime" : "optionnel au runtime")
            : string.Empty;
        var damageEventsSummary = draft.HasDamageEvents && draft.DamageEvents is { } damageEventDraft
            ? "\nDégâts et mort : " + damageEventDraft.DamagedBinding.Trim() + " · " +
              damageEventDraft.DestroyedBinding.Trim() + " · API " + damageEventDraft.ForgeScriptApiVersion.Trim() +
              "\nContexte : source · quantité · type · santé avant/après · contexte · " +
              (draft.RequireDamageEventsRuntime ? "requis au runtime" : "optionnel au runtime")
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
            containerEventsSummary + lockEventsSummary + contentsChangedSummary + damageEventsSummary +
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

}
