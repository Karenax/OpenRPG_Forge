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
        if (draft.OnUsedEnabled && (draft.Role != StudioPlaceableRole.Interactable ||
            draft.OnUsedId == Guid.Empty || string.IsNullOrWhiteSpace(draft.OnUsedForgeScriptApiVersion) ||
            string.IsNullOrWhiteSpace(draft.OnUsedEventBinding) ||
            draft.OnUsedForgeScriptApiVersion.Trim().Length > 256 || draft.OnUsedEventBinding.Trim().Length > 256))
        {
            message = "L’événement d’utilisation exige un objet utilisable, une identité stable, une version ForgeScript et un nom d’événement.";
            return false;
        }
        if (!draft.OnUsedEnabled && draft.RequireOnUsedRuntime)
        {
            message = "L’événement d’utilisation est requis au runtime : activez-le ou retirez cette exigence.";
            return false;
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
        PlaceableContentsChangedEventProfile? editedContentsChanged = null;
        if (draft.HasContentsChanged && (draft.ContentsChanged is null ||
            !TryBuildPlaceableContentsChanged(draft.ContentsChanged, out editedContentsChanged, out message)))
        {
            message = draft.ContentsChanged is null
                ? "L’événement de contenu modifié est actif mais sa configuration est absente."
                : message;
            return false;
        }
        if (!draft.HasContentsChanged && draft.RequireContentsChangedRuntime)
        {
            message = "L’événement de contenu modifié est requis au runtime : activez-le ou retirez cette exigence.";
            return false;
        }
        var contentsChanged = draft.HasContentsChanged
            ? ContentsChangedValuesEqual(editedContentsChanged, draft.OriginalSecurityEvents?.ContentsChanged)
                ? draft.OriginalSecurityEvents?.ContentsChanged
                : editedContentsChanged
            : null;
        PlaceableDamageEventsProfile? editedDamageEvents = null;
        if (draft.HasDamageEvents && (draft.DamageEvents is null ||
            !TryBuildPlaceableDamageEvents(draft.DamageEvents, out editedDamageEvents, out message)))
        {
            message = draft.DamageEvents is null
                ? "Les événements de dégâts et de mort sont actifs mais leur configuration est absente."
                : message;
            return false;
        }
        if (draft.HasDamageEvents && !draft.HasHealth)
        {
            message = "Les événements de dégâts et de mort exigent d’activer la santé du placeable.";
            return false;
        }
        if (!draft.HasDamageEvents && draft.RequireDamageEventsRuntime)
        {
            message = "Les événements de dégâts et de mort sont requis au runtime : activez-les ou retirez cette exigence.";
            return false;
        }
        if (!draft.HasDamageEvents && draft.OriginalCompositionEvents is not null)
        {
            message = "Les événements de dégâts et de mort appartiennent au profil de composition existant et ne peuvent pas être retirés isolément.";
            return false;
        }
        var originalEffectiveDamageEvents = draft.OriginalDamageEvents ?? draft.OriginalCompositionEvents?.DamageEvents;
        var damageEvents = draft.HasDamageEvents
            ? DamageEventValuesEqual(editedDamageEvents, originalEffectiveDamageEvents)
                ? originalEffectiveDamageEvents
                : editedDamageEvents
            : null;
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
                    draft.OnUsedForgeScriptApiVersion.Trim(),
                    draft.OnUsedEventBinding.Trim(),
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
                securityEvents = trap is null && loot is null && containerEvents is null && lockEvents is null &&
                                 contentsChanged is null
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
                                    : lockEvents is not null
                                        ? "studio:ux1-u-placeable-lock-events"
                                        : "studio:ux1-v-placeable-events",
                        Trap: trap,
                        Loot: loot,
                        ContainerEvents: containerEvents,
                        LockEvents: lockEvents,
                        ContentsChanged: contentsChanged);
            }
            else if (ReferenceEquals(trap, draft.OriginalSecurityEvents.Trap) &&
                     ReferenceEquals(loot, draft.OriginalSecurityEvents.Loot) &&
                      ReferenceEquals(containerEvents, draft.OriginalSecurityEvents.ContainerEvents) &&
                      ReferenceEquals(lockEvents, draft.OriginalSecurityEvents.LockEvents) &&
                      ReferenceEquals(contentsChanged, draft.OriginalSecurityEvents.ContentsChanged))
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
                    ContentsChanged = contentsChanged,
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
            var contentsChangedRequiredAtRuntime = draft.RequireContentsChangedRuntime;
            var damageEventsRequiredAtRuntime = draft.RequireDamageEventsRuntime;
            var directDamageEvents = draft.OriginalDamageEvents is not null || draft.OriginalCompositionEvents is null
                ? damageEvents
                : null;
            var compositionEvents = draft.OriginalCompositionEvents;
            if (compositionEvents is not null && damageEvents is not null &&
                compositionEvents.DamageEvents != damageEvents)
            {
                compositionEvents = compositionEvents with { DamageEvents = damageEvents };
            }
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
                    CompositionEvents: compositionEvents,
                    FactionOwnership: factionOwnership,
                    FactionOwnershipRequiredAtRuntime: factionOwnershipRequiredAtRuntime,
                    Conversation: conversation,
                    ConversationRequiredAtRuntime: conversationRequiredAtRuntime,
                    CredentialRequiredAtRuntime: draft.RequireCredentialRuntime,
                    TrapRequiredAtRuntime: trapRequiredAtRuntime,
                    LootRequiredAtRuntime: lootRequiredAtRuntime,
                    ContainerEventsRequiredAtRuntime: containerEventsRequiredAtRuntime,
                    LockEventsRequiredAtRuntime: lockEventsRequiredAtRuntime,
                    ContentsChangedRequiredAtRuntime: contentsChangedRequiredAtRuntime,
                    DamageEvents: directDamageEvents,
                    DamageEventsRequiredAtRuntime: damageEventsRequiredAtRuntime)
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
                    CompositionEvents = compositionEvents,
                    TrapRequiredAtRuntime = trapRequiredAtRuntime,
                    LootRequiredAtRuntime = lootRequiredAtRuntime,
                    ContainerEventsRequiredAtRuntime = containerEventsRequiredAtRuntime,
                    LockEventsRequiredAtRuntime = lockEventsRequiredAtRuntime,
                    ContentsChangedRequiredAtRuntime = contentsChangedRequiredAtRuntime,
                    DamageEvents = directDamageEvents,
                    DamageEventsRequiredAtRuntime = damageEventsRequiredAtRuntime,
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


}
