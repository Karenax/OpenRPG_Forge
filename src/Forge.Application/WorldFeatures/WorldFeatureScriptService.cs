using Forge.Application.Projects;
using Forge.Domain.WorldFeatures;

namespace Forge.Application.WorldFeatures;

public sealed class WorldFeatureScriptService
{
    public sealed record Handle(
        int SchemaVersion,
        long Revision,
        string Kind,
        Guid Id,
        string TargetId,
        string Provenance,
        IReadOnlyDictionary<string, string> Properties);

    public IReadOnlyList<Handle> Query(ProjectState state, string? kind = null, string? idOrTarget = null,
        int limit = 100)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (limit is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(limit));
        var catalog = state.Project.Module.WorldFeatures;
        var values = new List<Handle>();
        values.AddRange(catalog.Placeables.Select(item => Create(state, "placeable", item.Id,
            item.BlueprintId.ToString(), item.Provenance, new Dictionary<string, string>
            {
                ["faction"] = item.FactionId ?? string.Empty,
                ["primaryFaction"] = item.FactionOwnership?.PrimaryFactionId.ToString("D") ?? string.Empty,
                ["factionMemberships"] = item.FactionOwnership is { } ownership
                    ? string.Join(',', ownership.MembershipFactionIds.OrderBy(id => id))
                    : string.Empty,
                ["inheritFactionHostility"] = item.FactionOwnership?.InheritHostility.ToString() ?? string.Empty,
                ["unauthorizedFactionActionsAreCrime"] =
                    item.FactionOwnership?.UnauthorizedActionsAreCrime.ToString() ?? string.Empty,
                ["factionMembersOnlyAccess"] = item.FactionOwnership?.MembersOnlyAccess.ToString() ?? string.Empty,
                ["blockFriendlyFactionTargeting"] =
                    item.FactionOwnership?.BlockFriendlyTargeting.ToString() ?? string.Empty,
                ["dialogue"] = item.DialogueId?.ToString("D") ?? string.Empty,
                ["conversationBindingCount"] = item.Conversation?.Bindings.Count.ToString() ?? string.Empty,
                ["conversationDialogueIds"] = item.Conversation is { } conversationDialogues
                    ? string.Join(',', conversationDialogues.Bindings
                        .Select(binding => binding.DialogueId)
                        .Distinct()
                        .OrderBy(id => id))
                    : string.Empty,
                ["conversationTriggers"] = item.Conversation is { } conversationTriggers
                    ? string.Join(',', conversationTriggers.Bindings
                        .Select(binding => binding.Trigger.ToString())
                        .Distinct()
                        .Order(StringComparer.Ordinal))
                    : string.Empty,
                ["conversationEntryPoints"] = item.Conversation is { } conversationEntries
                    ? string.Join(',', conversationEntries.Bindings
                        .Select(binding => binding.EntryPointKey)
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal))
                    : string.Empty,
                ["conversationRequiredAtRuntime"] = item.Conversation is null
                    ? string.Empty
                    : item.ConversationRequiredAtRuntime.ToString(),
                ["events"] = item.Reactive.Events.Count.ToString(),
                ["physics"] = item.Physics?.Mode.ToString() ?? "none",
                ["foundation"] = (item.Foundation is not null).ToString(),
                ["role"] = item.Foundation?.Interaction is not null ? "interactable" :
                    item.Foundation?.StaticProp is not null ? "static" : "legacy",
                ["asset"] = item.Foundation?.Visual.AssetId ?? string.Empty,
                ["slug"] = item.Foundation?.Slug ?? string.Empty,
                ["localizedNames"] = item.Foundation is { } identity
                    ? string.Join(';', identity.Texts.OrderBy(text => text.Locale, StringComparer.OrdinalIgnoreCase)
                        .Select(text => $"{text.Locale}:{text.Name}"))
                    : string.Empty,
                ["tags"] = item.Foundation is { } tagged
                    ? string.Join(',', tagged.Tags.Order(StringComparer.Ordinal))
                    : string.Empty,
                ["authorNotes"] = item.Foundation?.AuthorNotes ?? string.Empty,
                ["meshes"] = item.Foundation is { } meshes
                    ? string.Join(',', meshes.Visual.MeshIds)
                    : string.Empty,
                ["materials"] = item.Foundation is { } materials
                    ? string.Join(',', materials.Visual.MaterialIds)
                    : string.Empty,
                ["icon"] = item.Foundation?.Visual.IconAssetId ?? string.Empty,
                ["portrait"] = item.Foundation?.Visual.PortraitAssetId ?? string.Empty,
                ["visualEffects"] = item.Foundation is { } effects
                    ? string.Join(',', effects.Visual.VisualEffectIds)
                    : string.Empty,
                ["position"] = item.Foundation is { } positioned
                    ? FormattableString.Invariant(
                        $"{positioned.Transform.Position.X:0.###},{positioned.Transform.Position.Y:0.###},{positioned.Transform.Position.Z:0.###}")
                    : string.Empty,
                ["rotationDegrees"] = item.Foundation is { } rotated
                    ? FormattableString.Invariant(
                        $"{rotated.Transform.RotationDegrees.X:0.###},{rotated.Transform.RotationDegrees.Y:0.###},{rotated.Transform.RotationDegrees.Z:0.###}")
                    : string.Empty,
                ["scale"] = item.Foundation is { } scaled
                    ? FormattableString.Invariant(
                        $"{scaled.Transform.Scale.X:0.###},{scaled.Transform.Scale.Y:0.###},{scaled.Transform.Scale.Z:0.###}")
                    : string.Empty,
                ["pivot"] = item.Foundation is { } pivoted
                    ? FormattableString.Invariant(
                        $"{pivoted.Transform.Pivot.X:0.###},{pivoted.Transform.Pivot.Y:0.###},{pivoted.Transform.Pivot.Z:0.###}")
                    : string.Empty,
                ["snapIncrement"] = item.Foundation is { } snapped
                    ? FormattableString.Invariant($"{snapped.Transform.SnapIncrement:0.###}")
                    : string.Empty,
                ["parent"] = item.Foundation?.Transform.ParentId?.ToString("D") ?? string.Empty,
                ["alignToSurface"] = item.Foundation?.Transform.AlignToSurface.ToString() ?? string.Empty,
                ["transformAnimation"] = item.Foundation?.Transform.TransformAnimationId ?? string.Empty,
                ["onUsed"] = (item.Foundation?.OnUsed is not null).ToString(),
                ["forgeScriptApi"] = item.Foundation?.OnUsed?.ForgeScriptApiVersion ?? string.Empty,
                ["onUsedBinding"] = item.Foundation?.OnUsed?.EventBinding ?? string.Empty,
                ["validation"] = (item.Foundation?.Validation is not null).ToString(),
                ["requiredAtRuntime"] = item.FoundationRequiredAtRuntime.ToString(),
                ["credentialId"] = item.Credential?.CredentialId ?? string.Empty,
                ["credentialMode"] = item.Credential?.Consumption.ToString() ?? string.Empty,
                ["credentialCharges"] = item.Credential?.Charges.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                ["credentialVirtualPermission"] = item.Credential?.VirtualPermission ?? string.Empty,
                ["credentialAudit"] = item.Credential?.AuditUse.ToString() ?? string.Empty,
                ["credentialRequiredAtRuntime"] = item.CredentialRequiredAtRuntime.ToString(),
                ["operations"] = (item.Operations is not null).ToString(),
                ["container"] = item.Operations?.Container is { } container
                    ? $"slots:{container.CapacitySlots};mass:{container.CapacityMass:0.##};owner:{container.OwnershipPolicy};persistent:{container.Persistent};replicated:{container.Replicated}"
                    : string.Empty,
                ["containerAccess"] = item.Operations?.Container is { } accessContainer
                    ? string.Join(',', accessContainer.AccessPermissions.Order(StringComparer.Ordinal))
                    : string.Empty,
                ["inventory"] = item.Operations?.Container?.InventoryId ?? string.Empty,
                ["lootPolicy"] = item.Operations?.Container?.LootPolicyId ?? string.Empty,
                ["containerUi"] = item.Operations?.Container?.UiProfileId ?? string.Empty,
                ["initialVisualState"] = item.Operations?.StateMachine?.InitialStateId ?? string.Empty,
                ["visualStates"] = item.Operations?.StateMachine is { } stateMachine
                    ? string.Join(',', stateMachine.States.Select(state => state.StateId).Order(StringComparer.Ordinal))
                    : string.Empty,
                ["visualTransitions"] = item.Operations?.StateMachine?.Transitions.Count.ToString() ?? "0",
                ["visualStateAnimations"] = item.Operations?.StateMachine is { } animationMachine
                    ? string.Join(',', animationMachine.States
                        .OrderBy(state => state.StateId, StringComparer.Ordinal)
                        .Select(state => $"{state.StateId}={state.AnimationId}"))
                    : string.Empty,
                ["visualStateOverrides"] = item.Operations?.StateMachine is { } overrideMachine
                    ? string.Join(',', overrideMachine.States
                        .OrderBy(state => state.StateId, StringComparer.Ordinal)
                        .Select(state => $"{state.StateId}={state.VisualOverrideId}"))
                    : string.Empty,
                ["interactableVisualStates"] = item.Operations?.StateMachine is { } interactionMachine
                    ? string.Join(',', interactionMachine.States
                        .Where(state => state.Interactable)
                        .Select(state => state.StateId)
                        .Order(StringComparer.Ordinal))
                    : string.Empty,
                ["visualTransitionTriggers"] = item.Operations?.StateMachine is { } transitionMachine
                    ? string.Join(',', transitionMachine.Transitions
                        .OrderBy(transition => transition.FromStateId, StringComparer.Ordinal)
                        .ThenBy(transition => transition.ToStateId, StringComparer.Ordinal)
                        .ThenBy(transition => transition.Trigger, StringComparer.Ordinal)
                        .Select(transition =>
                            $"{transition.FromStateId}->{transition.ToStateId}:{transition.Trigger}@" +
                            transition.DurationSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                            (string.IsNullOrWhiteSpace(transition.Condition)
                                ? string.Empty
                                : $"[{transition.Condition}]")))
                    : string.Empty,
                ["damageable"] = item.Operations?.Damageable is { } damageable
                    ? $"health:{damageable.CurrentHealth:0.##}/{damageable.MaximumHealth:0.##};armor:{damageable.Armor:0.##};stages:{damageable.VisualStageThresholds.Count};repair:{damageable.Repairable}"
                    : string.Empty,
                ["maximumHealth"] = item.Operations?.Damageable?.MaximumHealth.ToString(
                    "0.###", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                ["currentHealth"] = item.Operations?.Damageable?.CurrentHealth.ToString(
                    "0.###", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                ["damageArmor"] = item.Operations?.Damageable?.Armor.ToString(
                    "0.###", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                ["damageResistances"] = item.Operations?.Damageable is { } resistanceProfile
                    ? string.Join(',', resistanceProfile.Resistances
                        .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                        .Select(entry => $"{entry.Key}=" + entry.Value.ToString(
                            "0.###", System.Globalization.CultureInfo.InvariantCulture)))
                    : string.Empty,
                ["visualDamageStages"] = item.Operations?.Damageable is { } stageProfile
                    ? string.Join(',', stageProfile.VisualStageThresholds.Select(value => value.ToString(
                        "0.###", System.Globalization.CultureInfo.InvariantCulture)))
                    : string.Empty,
                ["destructionEffect"] = item.Operations?.Damageable?.DestructionEffectId ?? string.Empty,
                ["debrisDefinition"] = item.Operations?.Damageable?.DebrisDefinitionId ?? string.Empty,
                ["repairable"] = item.Operations?.Damageable?.Repairable.ToString() ?? string.Empty,
                ["repairCost"] = item.Operations?.Damageable?.RepairCost.ToString(
                    "0.###", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                ["protection"] = item.Operations?.Protection is { } protection
                    ? string.Join(',', protection.ProtectedActions.Order(StringComparer.Ordinal))
                    : string.Empty,
                ["protectedActions"] = item.Operations?.Protection is { } protectedActionProfile
                    ? string.Join(',', protectedActionProfile.ProtectedActions.Order(StringComparer.Ordinal))
                    : string.Empty,
                ["protectionPermissions"] = item.Operations?.Protection is { } permissionProfile
                    ? string.Join(',', permissionProfile.RequiredPermissions.Order(StringComparer.Ordinal))
                    : string.Empty,
                ["protectionFeedback"] = item.Operations?.Protection?.Feedback ?? string.Empty,
                ["allowGameMasterOverride"] =
                    item.Operations?.Protection?.AllowGameMasterOverride.ToString() ?? string.Empty,
                ["protectionAudit"] = item.Operations?.Protection?.AuditBinding ?? string.Empty,
                ["lock"] = item.Operations?.Lock is { } lockProfile
                    ? $"{lockProfile.InitialState};credentials:{lockProfile.CredentialIds.Count};skill:{lockProfile.SkillId};code:{lockProfile.CodeHash is not null};hacking:{lockProfile.Hackable};relock:{lockProfile.Relockable};jam:{lockProfile.CanJam}"
                    : string.Empty,
                ["lockState"] = item.Operations?.Lock?.InitialState.ToString() ?? string.Empty,
                ["lockCredentialIds"] = item.Operations?.Lock is { } lockCredentialProfile
                    ? string.Join(',', lockCredentialProfile.CredentialIds.Order(StringComparer.Ordinal))
                    : string.Empty,
                ["lockSkill"] = item.Operations?.Lock?.SkillId ?? string.Empty,
                ["lockDifficulty"] = item.Operations?.Lock?.Difficulty.ToString(
                    "0.###", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                ["lockHasCode"] = (item.Operations?.Lock?.CodeHash is not null).ToString(),
                ["lockHackable"] = item.Operations?.Lock?.Hackable.ToString() ?? string.Empty,
                ["lockRelockable"] = item.Operations?.Lock?.Relockable.ToString() ?? string.Empty,
                ["lockCanJam"] = item.Operations?.Lock?.CanJam.ToString() ?? string.Empty,
                ["lockPermissions"] = item.Operations?.Lock is { } lockPermissionProfile
                    ? string.Join(',', lockPermissionProfile.RequiredPermissions.Order(StringComparer.Ordinal))
                    : string.Empty,
                ["operationsRequiredAtRuntime"] = item.OperationsRequiredAtRuntime.ToString(),
                ["securityEvents"] = (item.SecurityEvents is not null).ToString(),
                ["trap"] = item.SecurityEvents is { Trap: { } trap }
                    ? $"trigger:{trap.TriggerId};payload:{trap.PayloadId};detect:{trap.DetectionDifficulty:0.##};disarm:{trap.DisarmDifficulty:0.##};reset:{trap.ResetDelaySeconds:0.##};owner:{trap.OwnerId};vfx:{trap.VisualEffectId};friendly:{trap.FriendlyPolicy};armed:{trap.Armed};oneShot:{trap.OneShot}"
                    : string.Empty,
                ["trapTrigger"] = item.SecurityEvents?.Trap?.TriggerId ?? string.Empty,
                ["trapPayload"] = item.SecurityEvents?.Trap?.PayloadId ?? string.Empty,
                ["trapDetection"] = item.SecurityEvents?.Trap?.DetectionDifficulty.ToString(
                    "0.###", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                ["trapDisarm"] = item.SecurityEvents?.Trap?.DisarmDifficulty.ToString(
                    "0.###", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                ["trapResetSeconds"] = item.SecurityEvents?.Trap?.ResetDelaySeconds.ToString(
                    "0.###", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                ["trapOwner"] = item.SecurityEvents?.Trap?.OwnerId ?? string.Empty,
                ["trapVisualEffect"] = item.SecurityEvents?.Trap?.VisualEffectId ?? string.Empty,
                ["trapFriendlyPolicy"] = item.SecurityEvents?.Trap?.FriendlyPolicy ?? string.Empty,
                ["trapArmed"] = item.SecurityEvents?.Trap?.Armed.ToString() ?? string.Empty,
                ["trapOneShot"] = item.SecurityEvents?.Trap?.OneShot.ToString() ?? string.Empty,
                ["trapRequiredAtRuntime"] = item.TrapRequiredAtRuntime.ToString(),
                ["lootSpawner"] = item.SecurityEvents is { Loot: { } loot }
                    ? $"table:{loot.LootTableId:D};timing:{loot.Timing};seed:{loot.Seed};persisted:{loot.PersistGeneratedState};generation:{loot.Generation}"
                    : string.Empty,
                ["lootEntries"] = item.SecurityEvents?.Loot?.LootTable.Entries.Count.ToString() ?? "0",
                ["lootTableId"] = item.SecurityEvents?.Loot?.LootTableId.ToString("D") ?? string.Empty,
                ["lootTiming"] = item.SecurityEvents?.Loot?.Timing.ToString() ?? string.Empty,
                ["lootSeed"] = item.SecurityEvents?.Loot?.Seed.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                ["lootPersistGeneratedState"] =
                    item.SecurityEvents?.Loot?.PersistGeneratedState.ToString() ?? string.Empty,
                ["lootGeneration"] = item.SecurityEvents?.Loot?.Generation.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                ["lootSavedResults"] = item.SecurityEvents is { Loot: { } savedLoot }
                    ? string.Join(';', savedLoot.SavedResults.Select(result =>
                        $"{result.ItemDefinitionId:D}:{result.Group}:{result.Quantity}:{result.Quality}"))
                    : string.Empty,
                ["lootTableEntries"] = item.SecurityEvents is { Loot: { } tableLoot }
                    ? string.Join(';', tableLoot.LootTable.Entries.Select(entry =>
                        $"{entry.ItemDefinitionId:D}:{entry.Group}:{entry.Weight.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}:{entry.MinimumQuantity}:{entry.MaximumQuantity}:{entry.MinimumQuality}:{entry.Condition}:{entry.Unique}"))
                    : string.Empty,
                ["lootRequiredAtRuntime"] = item.LootRequiredAtRuntime.ToString(),
                ["containerOpened"] = item.SecurityEvents?.ContainerEvents?.OpenedBinding ?? string.Empty,
                ["containerClosed"] = item.SecurityEvents?.ContainerEvents?.ClosedBinding ?? string.Empty,
                ["containerEventApi"] = item.SecurityEvents?.ContainerEvents?.ForgeScriptApiVersion ?? string.Empty,
                ["containerEventPayload"] = item.SecurityEvents is { ContainerEvents: { } containerEvents } &&
                    containerEvents.IncludeActor && containerEvents.IncludeResult &&
                    containerEvents.IncludePreviousState && containerEvents.IncludeAuthority
                        ? "actor-result-previous-state-authority"
                        : string.Empty,
                ["containerEventsRequiredAtRuntime"] = item.ContainerEventsRequiredAtRuntime.ToString(),
                ["lockStateChanging"] = item.SecurityEvents?.LockEvents?.ChangingBinding ?? string.Empty,
                ["lockStateChanged"] = item.SecurityEvents?.LockEvents?.ChangedBinding ?? string.Empty,
                ["lockEventApi"] = item.SecurityEvents?.LockEvents?.ForgeScriptApiVersion ?? string.Empty,
                ["lockEventPayload"] = item.SecurityEvents is { LockEvents: { } lockEvents } &&
                    lockEvents.IncludeMethod && lockEvents.IncludeCredential && lockEvents.IncludeActor &&
                    lockEvents.ControlledVeto
                        ? "method-credential-actor-controlled-veto"
                        : string.Empty,
                ["lockEventsRequiredAtRuntime"] = item.LockEventsRequiredAtRuntime.ToString(),
                ["contentsChanged"] = item.SecurityEvents?.ContentsChanged?.EventBinding ?? string.Empty,
                ["contentsChangedApi"] = item.SecurityEvents?.ContentsChanged?.ForgeScriptApiVersion ?? string.Empty,
                ["contentsChangedPayload"] = item.SecurityEvents is { ContentsChanged: { } disturbed } &&
                    disturbed.IncludeTransactionalDelta && disturbed.IncludeCause && disturbed.IncludeRevision &&
                    disturbed.IncludeActor
                        ? "transactional-delta-cause-revision-actor"
                        : string.Empty,
                ["contentsChangedRequiredAtRuntime"] = item.ContentsChangedRequiredAtRuntime.ToString(),
                ["securityEventsRequiredAtRuntime"] = item.SecurityEventsRequiredAtRuntime.ToString(),
                ["compositionEvents"] = (item.CompositionEvents is not null).ToString(),
                ["damageApplied"] = EffectiveDamageEvents(item)?.DamagedBinding ?? string.Empty,
                ["entityDestroyed"] = EffectiveDamageEvents(item)?.DestroyedBinding ?? string.Empty,
                ["damageEventApi"] = EffectiveDamageEvents(item)?.ForgeScriptApiVersion ?? string.Empty,
                ["damageEventPayload"] = EffectiveDamageEvents(item) is { } damageEvents &&
                    damageEvents.IncludeSource && damageEvents.IncludeAmount && damageEvents.IncludeDamageType &&
                    damageEvents.IncludePreviousHealth && damageEvents.IncludeCurrentHealth &&
                    damageEvents.IncludeContext
                        ? "source-amount-damage-type-previous-current-context"
                        : string.Empty,
                ["damageEventsRequiredAtRuntime"] = item.DamageEventsRequiredAtRuntime.ToString(),
                ["attachmentSockets"] = item.CompositionEvents is { } composition
                    ? string.Join(';', composition.Sockets.Select(socket =>
                        $"{socket.SocketId}:{socket.Kind}:snap:{socket.SnapEnabled}:capacity:{socket.MaximumAttachments}"))
                    : string.Empty,
                ["attachments"] = item.CompositionEvents is { } attachments
                    ? string.Join(';', attachments.Attachments.Select(attachment =>
                        $"{attachment.SocketId}:{attachment.ChildBlueprintId}:{attachment.PrefabId}"))
                    : string.Empty,
                ["multiVerbs"] = item.CompositionEvents is { } multiVerb
                    ? string.Join(',', multiVerb.MultiVerb.Verbs.Select(verb => verb.VerbId))
                    : string.Empty,
                ["verbBindings"] = item.CompositionEvents is { } verbBindings
                    ? string.Join(',', verbBindings.MultiVerb.Verbs.Select(verb => verb.EventBinding))
                    : string.Empty,
                ["compositionEventsRequiredAtRuntime"] = item.CompositionEventsRequiredAtRuntime.ToString(),
            })));
        values.AddRange(catalog.Doors.Select(item => Create(state, "door", item.Id,
            item.BlueprintId.ToString(), item.Provenance, new Dictionary<string, string>
            {
                ["damageable"] = (item.Damage is not null).ToString(),
                ["assembly"] = item.Assembly?.Kind.ToString() ?? "single",
                ["events"] = item.Events.Count.ToString(),
                ["accessEvents"] = (item.AccessEvents is not null).ToString(),
                ["lockState"] = item.AccessEvents?.Lock.State.ToString() ?? string.Empty,
                ["credentials"] = item.AccessEvents?.Lock.CredentialIds.Count.ToString() ?? "0",
                ["consumeCredential"] = (item.AccessEvents?.Lock.ConsumeCredential == true).ToString(),
                ["rejectionLocales"] = item.AccessEvents?.Rejection.Texts.Count.ToString() ?? "0",
                ["trapPayload"] = item.AccessEvents?.Trap.PayloadId ?? string.Empty,
                ["portalRequested"] = item.AccessEvents?.PortalUse.RequestedBinding ?? string.Empty,
                ["portalCompleted"] = item.AccessEvents?.PortalUse.CompletedBinding ?? string.Empty,
                ["doorOpened"] = item.AccessEvents?.StateEvents.OpenedBinding ?? string.Empty,
                ["doorClosed"] = item.AccessEvents?.StateEvents.ClosedBinding ?? string.Empty,
                ["doorLocked"] = item.AccessEvents?.LockStateEvents?.LockedBinding ?? string.Empty,
                ["doorUnlocked"] = item.AccessEvents?.LockStateEvents?.UnlockedBinding ?? string.Empty,
                ["lockEventsRequiredAtRuntime"] =
                    (item.AccessEvents?.LockStateEventsRequiredAtRuntime == true).ToString(),
                ["accessEventsRequiredAtRuntime"] = item.AccessEventsRequiredAtRuntime.ToString(),
            })));
        values.AddRange(catalog.Triggers.Select(item => Create(state, "trigger", item.Id,
            item.BlueprintId.ToString(), item.Provenance, new Dictionary<string, string>
            {
                ["area"] = item.AreaId.ToString(),
                ["discovery"] = (item.Discovery is not null).ToString(),
                ["director"] = (item.Director?.AllowManualFire == true).ToString(),
                ["foundation"] = (item.Foundation is not null).ToString(),
                ["shape"] = item.Foundation?.Shape.Kind.ToString() ?? string.Empty,
                ["actions"] = item.Foundation?.Actions.Count.ToString() ?? "0",
                ["portal"] = (item.Foundation?.Portal is not null).ToString(),
                ["onEnter"] = item.Foundation?.OnEnter.EventBinding ?? string.Empty,
                ["onExit"] = item.Foundation?.OnExit.EventBinding ?? string.Empty,
                ["forgeScriptApi"] = item.Foundation?.OnEnter.ForgeScriptApiVersion ?? string.Empty,
                ["validation"] = (item.Foundation?.Validation is not null).ToString(),
                ["requiredAtRuntime"] = item.FoundationRequiredAtRuntime.ToString(),
                ["advanced"] = (item.Advanced is not null).ToString(),
                ["trap"] = item.Advanced is { Trap: var trap }
                    ? $"trigger:{trap.TriggerId};payload:{trap.PayloadId};detect:{trap.DetectionDifficulty:0.##};disarm:{trap.DisarmDifficulty:0.##};reset:{trap.ResetDelaySeconds:0.##};owner:{trap.OwnerId};vfx:{trap.VisualEffectId};friendly:{trap.FriendlyPolicy};gm:{trap.GameMasterVisualization}"
                    : string.Empty,
                ["trapBindings"] = item.Advanced is { Trap: var boundTrap }
                    ? string.Join(',', boundTrap.DetectedBinding, boundTrap.DisarmedBinding,
                        boundTrap.TriggeredBinding, boundTrap.ResetBinding)
                    : string.Empty,
                ["targetFilter"] = item.Advanced is { TargetFilter: var filter }
                    ? $"players:{filter.PlayersOnly};required:{string.Join(',', filter.RequiredTags)};excluded:{string.Join(',', filter.ExcludedTags)};factions:{filter.FactionIds.Count};components:{string.Join(',', filter.RequiredComponents)};owner:{filter.OwnerId};team:{filter.TeamId};predicate:{filter.SandboxPredicate}"
                    : string.Empty,
                ["gameplayVolumes"] = item.Advanced is { GameplayVolumes: var volumes }
                    ? string.Join(',', volumes.Select(volume => volume.Kind))
                    : string.Empty,
                ["gameplayVolumeBindings"] = item.Advanced is { GameplayVolumes: var boundVolumes }
                    ? string.Join(',', boundVolumes.SelectMany(volume =>
                        new[] { volume.EnterBinding, volume.ExitBinding }))
                    : string.Empty,
                ["occupancyDebug"] = item.Advanced is { OccupancyDebug: var debug }
                    ? $"enabled:{debug.Enabled};occupants:{debug.ShowOccupants};fires:{debug.ShowLastFires};cooldowns:{debug.ShowCooldowns};filters:{debug.ShowFilters};trace:{debug.ShowEventTrace};maximum:{debug.MaximumTraceEntries}"
                    : string.Empty,
                ["advancedRequiredAtRuntime"] = item.AdvancedRequiredAtRuntime.ToString(),
            })));
        values.AddRange(catalog.Encounters.Select(item => Create(state, "encounter", item.Id,
            item.AreaId.ToString(), item.Provenance, new Dictionary<string, string>
            {
                ["respawn"] = (item.Respawn is not null).ToString(),
                ["reinforcements"] = (item.Reinforcements is not null).ToString(),
                ["visibility"] = item.Visibility.Mode.ToString(),
                ["composition"] = (item.Composition is not null).ToString(),
                ["wizard"] = item.Composition?.Slug ?? string.Empty,
                ["difficulty"] = item.Composition?.Difficulty.Band.ToString() ?? string.Empty,
                ["partyProvider"] = item.Composition?.Selection.PartyAssessmentProviderId ?? string.Empty,
                ["groups"] = item.Composition?.Groups.Count.ToString() ?? "0",
                ["activation"] = item.Composition?.Activation.Mode.ToString() ?? string.Empty,
                ["compositionRequiredAtRuntime"] = item.CompositionRequiredAtRuntime.ToString(),
                ["scenario"] = (item.Scenario is not null).ToString(),
                ["spawnAnchors"] = item.Scenario?.SpawnAnchors.Count.ToString() ?? "0",
                ["distribution"] = item.Scenario?.Distribution.ToString() ?? string.Empty,
                ["activatorAuthority"] = item.Scenario?.Activator.RequiredAuthority.ToString() ?? string.Empty,
                ["encounterEntered"] = item.Scenario?.Lifecycle.Entered.Binding ?? string.Empty,
                ["encounterExited"] = item.Scenario?.Lifecycle.Exited.Binding ?? string.Empty,
                ["encounterExhausted"] = item.Scenario?.Lifecycle.Exhausted.Binding ?? string.Empty,
                ["phases"] = item.Scenario?.Phases.Count.ToString() ?? "0",
                ["waves"] = item.Scenario?.Phases.Sum(phase => phase.Waves.Count).ToString() ?? "0",
                ["scenarioRequiredAtRuntime"] = item.ScenarioRequiredAtRuntime.ToString(),
                ["statistics"] = (item.Statistics is not null).ToString(),
                ["statisticsRuns"] = item.Statistics?.Runs.ToString() ?? "0",
                ["statisticsSeed"] = item.Statistics?.BaseSeed.ToString() ?? "0",
                ["durationEstimator"] = item.Statistics?.DurationEstimatorId ?? string.Empty,
                ["rewardEstimator"] = item.Statistics?.RewardEstimatorId ?? string.Empty,
                ["statisticsPercentiles"] = item.Statistics is null
                    ? string.Empty
                    : string.Join(',', item.Statistics.Percentiles.Select(value =>
                        value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture))),
                ["statisticsRequiredAtRuntime"] = item.StatisticsRequiredAtRuntime.ToString(),
            })));
        values.AddRange(catalog.VehicleRoutes.Select(item => Create(state, "vehicle-route", item.Id,
            item.AreaId.ToString(), item.Provenance, new Dictionary<string, string>
            {
                ["points"] = item.Points.Count.ToString(),
                ["lanes"] = item.Lanes.Count.ToString(),
                ["speed"] = item.DefaultSpeedLimit.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
            })));
        values.AddRange(catalog.Merchants.Select(item => Create(state, "merchant", item.Id,
            item.BlueprintId.ToString(), item.Provenance, new Dictionary<string, string>
            {
                ["currencies"] = item.Currencies.Count.ToString(),
                ["services"] = item.Services.Count.ToString(),
                ["ledger"] = item.Ledger.Enabled.ToString(),
                ["transactions"] = (item.Transactions is not null).ToString(),
                ["stock"] = item.Transactions?.Stock.Count.ToString() ?? "0",
                ["requiredAtRuntime"] = item.TransactionsRequiredAtRuntime.ToString(),
                ["foundation"] = (item.Foundation is not null).ToString(),
                ["storeId"] = item.Foundation?.Id.ToString("D") ?? string.Empty,
                ["storeSlug"] = item.Foundation?.Slug ?? string.Empty,
                ["vendors"] = item.Foundation?.Vendors.Count.ToString() ?? "0",
                ["wizard"] = item.Foundation?.Wizard.CreateStoreDefinition == true &&
                             item.Foundation.Wizard.CreateVendorBinding &&
                             item.Foundation.Wizard.CreateDialogueChoice &&
                             item.Foundation.Wizard.CreateInteraction && item.Foundation.Wizard.CreateTests
                    ? "store-vendor-dialogue-interaction-tests"
                    : string.Empty,
                ["wizardFixture"] = item.Foundation?.Wizard.TestFixtureId ?? string.Empty,
                ["foundationStock"] = item.Foundation?.Stock.Count.ToString() ?? "0",
                ["supplyModes"] = item.Foundation is null
                    ? string.Empty
                    : string.Join(',', item.Foundation.Stock.Select(entry => entry.Supply).Distinct().Order()),
                ["buyCurve"] = item.Foundation?.Pricing.BuyCurve.Count.ToString() ?? "0",
                ["sellCurve"] = item.Foundation?.Pricing.SellCurve.Count.ToString() ?? "0",
                ["restrictionTypes"] = item.Foundation?.Restrictions.WhitelistTypes.Count.ToString() ?? "0",
                ["restrictionWhitelistTags"] = item.Foundation?.Restrictions.WhitelistTags.Count.ToString() ?? "0",
                ["restrictionBlacklistTags"] = item.Foundation?.Restrictions.BlacklistTags.Count.ToString() ?? "0",
                ["customRule"] = item.Foundation?.Restrictions.CustomRuleBinding ?? string.Empty,
                ["foundationRequiredAtRuntime"] = item.FoundationRequiredAtRuntime.ToString(),
                ["experience"] = (item.Experience is not null).ToString(),
                ["sessionApi"] = item.Experience?.Sessions.ForgeScriptApiVersion ?? string.Empty,
                ["sessionOpened"] = item.Experience?.Sessions.OpenEventBinding ?? string.Empty,
                ["sessionClosed"] = item.Experience?.Sessions.CloseEventBinding ?? string.Empty,
                ["sessionPayload"] = item.Experience is { Sessions: var sessions } &&
                                     sessions.IncludeCustomer && sessions.IncludeVendor &&
                                     sessions.IncludePriceContext && sessions.IncludeTransactionSummary
                    ? "customer-vendor-price-context-transaction-summary"
                    : string.Empty,
                ["storeUi"] = item.Experience is { Ui: var ui } && ui.KeyboardNavigation &&
                              ui.GamepadNavigation && ui.Compare && ui.Filters && ui.Cart && ui.Confirmation
                    ? "keyboard-gamepad-compare-filters-cart-confirmation"
                    : string.Empty,
                ["storeUiAccessibility"] = item.Experience is { Ui: var accessibleUi } &&
                                           accessibleUi.ScreenReaderLabels && accessibleUi.HighContrast
                    ? "screen-reader-high-contrast"
                    : string.Empty,
                ["storeUiColumns"] = item.Experience is null
                    ? string.Empty
                    : string.Join(',', item.Experience.Ui.Columns),
                ["experienceRequiredAtRuntime"] = item.ExperienceRequiredAtRuntime.ToString(),
            })));
        values.AddRange(catalog.Companions.Select(item => Create(state, "companion", item.Id,
            item.BlueprintId.ToString(), item.Provenance, new Dictionary<string, string>
            {
                ["formation"] = item.Formation.FormationId,
                ["level"] = item.Progression.CurrentLevel.ToString(),
                ["directControl"] = item.DirectControl.Enabled.ToString(),
                ["validation"] = (item.Validation is not null).ToString(),
                ["recruitmentPaths"] = item.Validation is null ? "0" :
                    ((item.Validation.Recruitment.AllowDialogue ? 1 : 0) +
                     (item.Validation.Recruitment.AllowScript ? 1 : 0)).ToString(),
                ["ownership"] = item.Validation?.Ownership.Kind ?? "legacy",
                ["commands"] = item.Validation?.CommandIds.Count.ToString() ?? "0",
                ["equipment"] = item.Validation?.Equipment.EquipmentPolicyId ?? string.Empty,
                ["death"] = item.Validation?.Death.Mode ?? string.Empty,
                ["dialogueContexts"] = item.Validation?.DialogueContexts.Count.ToString() ?? "0",
                ["crossZone"] = (item.Validation?.CrossZoneNavigation.Enabled == true).ToString(),
                ["requiredAtRuntime"] = item.ValidationRequiredAtRuntime.ToString(),
                ["foundation"] = (item.Foundation is not null).ToString(),
                ["recruitable"] = (item.Foundation?.Recruitable.Enabled == true).ToString(),
                ["preRecruitmentContext"] = item.Foundation?.PreRecruitment.ContextId ?? string.Empty,
                ["ownerContext"] = item.Foundation?.OwnerDialogue.ContextId ?? string.Empty,
                ["membership"] = item.Foundation?.Membership.Atomic == true
                    ? "capacity-ownership-inventory-position-persistence-atomic"
                    : string.Empty,
                ["persistence"] = item.Foundation?.Membership.Persistence.ToString() ?? string.Empty,
                ["companionSlots"] = item.Foundation?.Party.CompanionSlots.ToString() ?? "0",
                ["summonSlots"] = item.Foundation?.Party.SummonSlots.ToString() ?? "0",
                ["petSlots"] = item.Foundation?.Party.PetSlots.ToString() ?? "0",
                ["temporaryAllySlots"] = item.Foundation?.Party.TemporaryAllySlots.ToString() ?? "0",
                ["serverActorLimit"] = item.Foundation?.Party.ServerActorLimit.ToString() ?? "0",
                ["roleLimits"] = item.Foundation is null
                    ? string.Empty
                    : string.Join(',', item.Foundation.Party.RoleLimits.OrderBy(entry => entry.Key,
                        StringComparer.Ordinal).Select(entry => $"{entry.Key}:{entry.Value}")),
                ["foundationRequiredAtRuntime"] = item.FoundationRequiredAtRuntime.ToString(),
                ["control"] = (item.Control is not null).ToString(),
                ["controlOwnership"] = item.Control?.Ownership.Kind ?? string.Empty,
                ["controllers"] = item.Control is null
                    ? string.Empty
                    : string.Join(',', item.Control.Ownership.ControllerIds.Order(StringComparer.Ordinal)),
                ["allowedOwnershipTargets"] = item.Control is null
                    ? string.Empty
                    : string.Join(',', item.Control.Ownership.AllowedTargetKinds.Order(StringComparer.Ordinal)),
                ["ownershipRevision"] = item.Control?.Ownership.Revision.ToString() ?? "0",
                ["movementCommands"] = item.Control?.Movement.CommandKinds.Count.ToString() ?? "0",
                ["tacticalCommands"] = item.Control?.Tactical.CommandKinds.Count.ToString() ?? "0",
                ["stances"] = item.Control?.Tactical.Stances.Count.ToString() ?? "0",
                ["aiRole"] = item.Control?.Ai.Role ?? string.Empty,
                ["aiAuthorization"] = item.Control?.Ai.AuthorizationLevel ?? string.Empty,
                ["aiPlanId"] = item.Control?.Ai.PlanId.ToString("D") ?? string.Empty,
                ["equipmentPermission"] = item.Control?.Equipment.Permission ?? string.Empty,
                ["lockedItems"] = item.Control?.Equipment.LockedItemIds.Count.ToString() ?? "0",
                ["suggestedLoadout"] = item.Control?.Equipment.SuggestedLoadout.Count.ToString() ?? "0",
                ["equipmentOwner"] = item.Control?.Equipment.InventoryOwnerId ?? string.Empty,
                ["controlRequiredAtRuntime"] = item.ControlRequiredAtRuntime.ToString(),
                ["experience"] = (item.Experience is not null).ToString(),
                ["deathMode"] = item.Experience?.Death.Mode ?? string.Empty,
                ["downedDurationSeconds"] = item.Experience?.Death.DownedDurationSeconds.ToString() ?? "0",
                ["resurrectionResource"] = item.Experience?.Death.ResurrectionResourceId ?? string.Empty,
                ["replacementCreature"] = item.Experience?.Death.ReplacementCreatureId ?? string.Empty,
                ["memorial"] = item.Experience?.Death.MemorialId ?? string.Empty,
                ["dropInventory"] = (item.Experience?.Death.DropInventory == true).ToString(),
                ["cohesionThresholds"] = item.Experience is null
                    ? string.Empty
                    : $"{item.Experience.Cohesion.NavigateThreshold:0.##},{item.Experience.Cohesion.PortalThreshold:0.##},{item.Experience.Cohesion.TeleportThreshold:0.##}",
                ["cohesionAnchors"] = item.Experience is null
                    ? string.Empty
                    : string.Join(',', item.Experience.Cohesion.SafeAnchorTags.Order(StringComparer.Ordinal)),
                ["partyHud"] = item.Experience?.Hud.Portraits == true && item.Experience.Hud.Health &&
                               item.Experience.Hud.Statuses && item.Experience.Hud.QuickCommands
                    ? "portraits-health-resources-statuses-quick-commands-reorder"
                    : string.Empty,
                ["partyHudInputs"] = item.Experience?.Hud.Keyboard == true && item.Experience.Hud.Gamepad
                    ? "keyboard-gamepad"
                    : string.Empty,
                ["partyHudAccessibility"] = item.Experience?.Hud.ScreenReaderLabels == true
                    ? item.Experience.Hud.HighContrast ? "screen-reader-high-contrast" : "screen-reader"
                    : string.Empty,
                ["partyHudResources"] = item.Experience is null
                    ? string.Empty
                    : string.Join(',', item.Experience.Hud.VisibleResourceIds.Order(StringComparer.Ordinal)),
                ["experienceRequiredAtRuntime"] = item.ExperienceRequiredAtRuntime.ToString(),
            })));
        values.Add(Create(state, "script", catalog.Scripts.Id, state.Project.Module.Id.ToString(),
            catalog.Scripts.Provenance, new Dictionary<string, string>
            {
                ["shots"] = catalog.Scripts.Camera.Shots.Count.ToString(),
                ["tagRoutes"] = catalog.Scripts.TagRouting.Routes.Count.ToString(),
                ["flags"] = catalog.Scripts.ModuleFlags.Count.ToString(),
                ["aiSettings"] = catalog.Scripts.AiSettings.Count.ToString(),
            }));
        return values.Where(item => string.IsNullOrWhiteSpace(kind) || string.Equals(item.Kind, kind, StringComparison.OrdinalIgnoreCase))
            .Where(item => string.IsNullOrWhiteSpace(idOrTarget) ||
                item.Id.ToString().Contains(idOrTarget, StringComparison.OrdinalIgnoreCase) ||
                item.TargetId.Contains(idOrTarget, StringComparison.OrdinalIgnoreCase) ||
                item.Properties.Any(property => property.Key.Contains(idOrTarget, StringComparison.OrdinalIgnoreCase) ||
                    property.Value.Contains(idOrTarget, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(item => item.Kind, StringComparer.Ordinal).ThenBy(item => item.Id).Take(limit).ToArray();
    }

    private static Handle Create(ProjectState state, string kind, Guid id, string targetId, string provenance,
        IReadOnlyDictionary<string, string> properties) =>
        new(1, state.Revision.Value, kind, id, targetId, provenance, properties);

    private static PlaceableDamageEventsProfile? EffectiveDamageEvents(PlaceableWorldProfile item)
        => item.DamageEvents ?? item.CompositionEvents?.DamageEvents;
}
