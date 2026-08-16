using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;

namespace Forge.Studio.Session;

internal enum StudioPlaceableRole
{
    StaticDecoration,
    Interactable,
}

internal sealed record StudioPlaceableVerbDraft(
    Guid Id,
    string Label,
    string EventName,
    string Condition,
    string? OriginalLabel,
    string? OriginalLabelKey);

internal sealed record StudioPlaceableContainerDraft(
    Guid Id,
    int CapacitySlots,
    double CapacityMass,
    string OwnershipPolicy,
    IReadOnlyList<string> AccessPermissions,
    bool Persistent,
    string LootPolicyId,
    string UiProfileId,
    bool Replicated,
    string InventoryId,
    bool ExistingProfile);

internal sealed record StudioPlaceableVisualStateDraft(
    Guid Id,
    string StateId,
    string AnimationId,
    string VisualOverrideId,
    bool Interactable);

internal sealed record StudioPlaceableVisualTransitionDraft(
    Guid Id,
    string FromStateId,
    string ToStateId,
    string Trigger,
    double DurationSeconds,
    string Condition);

internal sealed record StudioPlaceableStateMachineDraft(
    Guid Id,
    string InitialStateId,
    IReadOnlyList<StudioPlaceableVisualStateDraft> States,
    IReadOnlyList<StudioPlaceableVisualTransitionDraft> Transitions,
    bool ExistingProfile);

internal sealed record StudioPlaceableDamageResistanceDraft(
    string DamageType,
    double Ratio);

internal sealed record StudioPlaceableDamageableDraft(
    Guid Id,
    double MaximumHealth,
    double CurrentHealth,
    double Armor,
    IReadOnlyList<StudioPlaceableDamageResistanceDraft> Resistances,
    IReadOnlyList<double> VisualStageThresholds,
    string DestructionEffectId,
    string DebrisDefinitionId,
    bool Repairable,
    double RepairCost,
    bool ExistingProfile);

internal sealed record StudioPlaceableProtectionDraft(
    Guid Id,
    IReadOnlyList<string> ProtectedActions,
    IReadOnlyList<string> RequiredPermissions,
    string Feedback,
    bool AllowGameMasterOverride,
    string AuditBinding,
    bool ExistingProfile);

internal sealed record StudioPlaceableFactionOption(
    Guid Id,
    string DisplayName,
    string Slug,
    bool Archived);

internal sealed record StudioPlaceableFactionOwnershipDraft(
    Guid Id,
    Guid PrimaryFactionId,
    IReadOnlyList<Guid> MembershipFactionIds,
    bool InheritHostility,
    bool UnauthorizedActionsAreCrime,
    bool MembersOnlyAccess,
    bool BlockFriendlyTargeting,
    bool ExistingProfile);

internal sealed record StudioPlaceableDialogueOption(
    Guid Id,
    string DisplayName,
    string Slug,
    IReadOnlyList<string> EntryPointKeys);

internal sealed record StudioPlaceableItemOption(
    BlueprintId Id,
    string DisplayName,
    string Slug);

internal sealed record StudioPlaceableConversationBindingDraft(
    Guid Id,
    Guid DialogueId,
    PlaceableConversationTrigger Trigger,
    string Selector,
    BlueprintId? ItemBlueprintId,
    string EntryPointKey,
    int Priority,
    bool OncePerUser);

internal sealed record StudioPlaceableConversationDraft(
    Guid Id,
    IReadOnlyList<StudioPlaceableConversationBindingDraft> Bindings,
    bool ExistingProfile,
    bool ImportedLegacyDialogue);

internal enum StudioPlaceableCredentialPreviewMode
{
    PresentCredential,
    MissingCredential,
    VirtualPermission,
    ExhaustedCharges,
}

internal sealed record StudioPlaceableCredentialDraft(
    BlueprintId? ItemBlueprintId,
    string OtherCredentialId,
    PlaceableWorldProfile.CredentialConsumption Consumption,
    int Charges,
    string VirtualPermission,
    bool AuditUse,
    bool ExistingPolicy);

internal enum StudioPlaceableTrapPreviewMode
{
    DetectHidden,
    DetectSuccess,
    DisarmFailure,
    DisarmSuccess,
    TriggerFriendly,
    TriggerHostile,
    Reset,
}

internal sealed record StudioPlaceableTrapDraft(
    Guid Id,
    string TriggerId,
    string PayloadId,
    double DetectionDifficulty,
    double DisarmDifficulty,
    double ResetDelaySeconds,
    string OwnerId,
    string VisualEffectId,
    string FriendlyPolicy,
    bool Armed,
    bool OneShot,
    bool ExistingProfile);

internal enum StudioPlaceableLootPreviewMode
{
    Initialize,
    Open,
    Reset,
}

internal enum StudioPlaceableContainerEventPreviewMode
{
    Open,
    Close,
}

internal enum StudioPlaceableLockEventPreviewMode
{
    AcceptedTransition,
    ControlledVeto,
}

internal sealed record StudioPlaceableContainerEventsDraft(
    Guid Id,
    string ForgeScriptApiVersion,
    string OpenedBinding,
    string ClosedBinding,
    bool IncludeActor,
    bool IncludeResult,
    bool IncludePreviousState,
    bool IncludeAuthority,
    bool ExistingProfile);

internal sealed record StudioPlaceableLockEventsDraft(
    Guid Id,
    string ForgeScriptApiVersion,
    string ChangingBinding,
    string ChangedBinding,
    bool IncludeMethod,
    bool IncludeCredential,
    bool IncludeActor,
    bool ControlledVeto,
    bool ExistingProfile);

internal sealed record StudioPlaceableLootEntryDraft(
    Guid Id,
    BlueprintId ItemBlueprintId,
    string Group,
    double Weight,
    int MinimumQuantity,
    int MaximumQuantity,
    int MinimumQuality,
    string Condition,
    bool Unique);

internal sealed record StudioPlaceableLootDraft(
    Guid Id,
    Guid LootTableId,
    PlaceableAuthoredLootSpawnTiming Timing,
    int Seed,
    bool PersistGeneratedState,
    IReadOnlyList<StudioPlaceableLootEntryDraft> Entries,
    IReadOnlyList<PlaceableSavedLootResultProfile> SavedResults,
    int Generation,
    bool ResetGeneratedState,
    bool ExistingProfile);

internal enum StudioPlaceableLockPreviewMode
{
    Credential,
    Skill,
    Code,
    Hacking,
    Permission,
    GameMaster,
    Relock,
    Jam,
}

internal sealed record StudioPlaceableLockDraft(
    Guid Id,
    PlaceableAuthoredLockState InitialState,
    BlueprintId? KeyItemId,
    IReadOnlyList<string> OtherCredentialIds,
    string SkillId,
    double Difficulty,
    bool CodeEnabled,
    string AccessCode,
    string? ExistingCodeHash,
    bool Hackable,
    bool Relockable,
    bool CanJam,
    IReadOnlyList<string> RequiredPermissions,
    bool ExistingProfile);

internal sealed record StudioPlaceableLocalizedTextDraft(
    string Locale,
    string Name,
    string Prompt);

internal sealed record StudioPlaceableAppearanceDraft(
    string MeshIdsText,
    string MaterialIdsText,
    string IconAssetId,
    string PortraitAssetId,
    string VisualEffectIdsText);

internal sealed record StudioPlaceableBlueprintOption(
    BlueprintId Id,
    string DisplayName,
    string Slug,
    int InstanceCount,
    bool HasProfile);

internal sealed record StudioPlaceableVisualOption(
    string AssetId,
    string DisplayName,
    string License,
    int UsageCount);

internal sealed record StudioPlaceableParentOption(
    Guid ProfileId,
    BlueprintId BlueprintId,
    string DisplayName);

internal sealed record StudioPlaceableCatalog(
    IReadOnlyList<StudioPlaceableBlueprintOption> Blueprints,
    IReadOnlyList<StudioPlaceableVisualOption> Visuals,
    IReadOnlyList<StudioPlaceableParentOption> Parents,
    IReadOnlyList<StudioPlaceableFactionOption> Factions,
    IReadOnlyList<StudioPlaceableDialogueOption> Dialogues,
    IReadOnlyList<StudioPlaceableItemOption> Items);

internal sealed record StudioPlaceableDraft(
    BlueprintId BlueprintId,
    Guid ProfileId,
    Guid FoundationId,
    Guid VerbId,
    string Slug,
    string Name,
    string Prompt,
    IReadOnlyList<StudioPlaceableLocalizedTextDraft> AdditionalLocalizedTexts,
    string TagsText,
    string AuthorNotes,
    string VisualAssetId,
    StudioPlaceableAppearanceDraft Appearance,
    bool CollisionEnabled,
    bool NavigationObstacle,
    string CollisionShape,
    Vector3Value CollisionSize,
    StudioPlaceableRole Role,
    bool Batched,
    bool Instanced,
    bool Occluder,
    bool MergeEligible,
    bool StaticCollisionEnabled,
    double InteractionDistance,
    double InteractionAngle,
    bool RequiresFocus,
    double InteractionDuration,
    IReadOnlyList<StudioPlaceableVerbDraft> InteractionVerbs,
    string InteractionAuthority,
    string InteractionConditionsText,
    Guid OnUsedId,
    bool OnUsedEnabled,
    string BeforeUseScript,
    string AfterUseScript,
    bool RequireOnUsedRuntime,
    bool HasHealth,
    bool HasContainer,
    Guid OperationsId,
    bool HasStateMachine,
    StudioPlaceableStateMachineDraft StateMachine,
    StudioPlaceableDamageableDraft Damageable,
    bool HasProtection,
    StudioPlaceableProtectionDraft Protection,
    bool HasFactionOwnership,
    StudioPlaceableFactionOwnershipDraft FactionOwnership,
    bool RequireFactionOwnershipRuntime,
    bool HasConversation,
    StudioPlaceableConversationDraft Conversation,
    bool RequireConversationRuntime,
    bool HasCredentialPolicy,
    StudioPlaceableCredentialDraft Credential,
    bool RequireCredentialRuntime,
    StudioPlaceableLockDraft Lock,
    StudioPlaceableContainerDraft Container,
    bool RequireOperationsRuntime,
    bool HasLock,
    bool HasTrap,
    StudioPlaceableTrapDraft Trap,
    bool RequireTrapRuntime,
    bool HasPhysics,
    string ScriptBindingsText,
    Vector3Value Position,
    Vector3Value RotationDegrees,
    Vector3Value Scale,
    Vector3Value Pivot,
    double SnapIncrement,
    Guid? ParentId,
    bool AlignToSurface,
    string TransformAnimationId,
    string ExplicitOverridesText,
    bool RequireRuntime,
    bool ExistingProfile,
    PlaceableWorldProfile.FoundationPolicy? OriginalFoundation,
    PlaceableOperationsProfile? OriginalOperations,
    PlaceableFactionOwnershipProfile? OriginalFactionOwnership,
    PlaceableConversationProfile? OriginalConversation,
    PlaceableWorldProfile.CredentialPolicy? OriginalCredential,
    PlaceableSecurityEventsProfile? OriginalSecurityEvents,
    bool HasLoot = false,
    StudioPlaceableLootDraft? Loot = null,
    bool RequireLootRuntime = false,
    bool HasContainerEvents = false,
    StudioPlaceableContainerEventsDraft? ContainerEvents = null,
    bool RequireContainerEventsRuntime = false,
    bool HasLockEvents = false,
    StudioPlaceableLockEventsDraft? LockEvents = null,
    bool RequireLockEventsRuntime = false);

internal sealed record StudioPlaceablePreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    BlueprintId BlueprintId,
    IReadOnlyList<EntityId> InstanceIds,
    Vector3Value SnappedPosition,
    string RoleLabel,
    string VisualLabel);

internal sealed record StudioPlaceableResult(bool Succeeded, string Message);

internal sealed record StudioPlaceableLootPreview(
    bool Succeeded,
    string Message,
    string DiagnosticCode,
    string EventName,
    bool ReusedSavedState,
    int Generation,
    IReadOnlyList<PlaceableSavedLootResultProfile> Results);

internal sealed record StudioPlaceableContainerEventPreview(
    bool Succeeded,
    string Summary,
    string Code,
    string Binding,
    string EventName,
    string ActorId,
    string Result,
    string PreviousState,
    string Authority);

internal sealed record StudioPlaceableLockEventPreview(
    bool Succeeded,
    string Summary,
    string Code,
    string ChangingBinding,
    string? ChangedBinding,
    string ChangingEventName,
    string? ChangedEventName,
    string Method,
    string? Credential,
    string ActorId,
    bool Vetoed,
    string? VetoReason,
    string FinalState);

internal sealed record StudioUx1ESmokeResult(
    bool Succeeded,
    bool Preview,
    bool DryRun,
    bool Composition,
    bool AssetReference,
    bool InstanceSelection,
    bool NeighborPreserved,
    bool OtherCatalogPreserved,
    bool RuntimeSemantics,
    bool UndoRedo,
    bool Persistence,
    bool InvalidBlocked,
    bool HumanDiagnostics);

internal sealed record StudioUx1FSmokeResult(
    bool Succeeded,
    bool StaticRole,
    bool Optimization,
    bool OptionalCollision,
    bool MergeGuard,
    bool OptimizationGuard,
    bool DryRun,
    bool CommandParity,
    bool InstanceSelection,
    bool Preservation,
    bool RuntimeSemantics,
    bool UndoRedo,
    bool Persistence,
    bool HumanDiagnostics);

internal sealed record StudioUx1GSmokeResult(
    bool Succeeded,
    bool InteractableRole,
    bool Verbs,
    bool RangeFocus,
    bool AuthorityConditions,
    bool OnUsed,
    bool DryRun,
    bool CommandParity,
    bool InstanceSelection,
    bool Preservation,
    bool RuntimeEvent,
    bool OptionalRequired,
    bool UndoRedo,
    bool Persistence,
    bool InvalidBlocked,
    bool HumanDiagnostics);

internal sealed record StudioUx1HSmokeResult(
    bool Succeeded,
    bool ContainerComponent,
    bool Capacity,
    bool OwnershipAccess,
    bool PersistenceReplication,
    bool References,
    bool DryRun,
    bool CommandParity,
    bool InstanceSelection,
    bool Preservation,
    bool RuntimeProjection,
    bool ScriptRead,
    bool OptionalRequired,
    bool UndoRedo,
    bool Persistence,
    bool InvalidBlocked,
    bool HumanDiagnostics);

internal sealed record StudioUx1ISmokeResult(
    bool Succeeded,
    bool Identity,
    bool Localization,
    bool Appearance,
    bool Metadata,
    bool DryRun,
    bool CommandParity,
    bool InstanceSelection,
    bool Preservation,
    bool RuntimeProjection,
    bool ScriptRead,
    bool OptionalRequired,
    bool UndoRedo,
    bool Persistence,
    bool InvalidBlocked,
    bool HumanDiagnostics);

internal sealed record StudioUx1JSmokeResult(
    bool Succeeded,
    bool Coordinates,
    bool PivotSnap,
    bool Parentage,
    bool SurfaceAlignment,
    bool TransformAnimation,
    bool GeometryGuard,
    bool DryRun,
    bool CommandParity,
    bool InstanceSelection,
    bool Preservation,
    bool RuntimeProjection,
    bool ScriptRead,
    bool OptionalRequired,
    bool UndoRedo,
    bool Persistence,
    bool InvalidBlocked,
    bool HumanDiagnostics);

internal sealed record StudioUx1KSmokeResult(
    bool Succeeded,
    bool InitialState,
    bool SixStates,
    bool AnimationsAndOverrides,
    bool InteractableStates,
    bool Transitions,
    bool PreviewTransition,
    bool DryRun,
    bool CommandParity,
    bool InstanceSelection,
    bool Preservation,
    bool RuntimeProjection,
    bool ScriptRead,
    bool OptionalRequired,
    bool UndoRedo,
    bool Persistence,
    bool InvalidBlocked,
    bool HumanDiagnostics);

internal sealed record StudioUx1LSmokeResult(
    bool Succeeded,
    bool HealthArmor,
    bool Resistances,
    bool VisualStages,
    bool DestructionDebris,
    bool Repair,
    bool DamagePreview,
    bool DryRun,
    bool CommandParity,
    bool InstanceSelection,
    bool Preservation,
    bool RuntimeProjection,
    bool ScriptRead,
    bool OptionalRequired,
    bool UndoRedo,
    bool Persistence,
    bool InvalidBlocked,
    bool HumanDiagnostics);

internal sealed record StudioUx1MSmokeResult(
    bool Succeeded,
    bool ProtectedActions,
    bool Permissions,
    bool Feedback,
    bool GameMasterOverride,
    bool AuditTrail,
    bool PolicyPreview,
    bool DryRun,
    bool CommandParity,
    bool InstanceSelection,
    bool Preservation,
    bool RuntimeEnforcement,
    bool ScriptRead,
    bool OptionalRequired,
    bool UndoRedo,
    bool Persistence,
    bool InvalidBlocked,
    bool HumanDiagnostics);

internal sealed record StudioUx1NSmokeResult(
    bool Succeeded,
    bool PrimaryFaction,
    bool Memberships,
    bool HostilityAndCrime,
    bool AccessAndTargeting,
    bool DecisionPreview,
    bool DryRun,
    bool CommandParity,
    bool InstanceSelection,
    bool Preservation,
    bool RuntimeProjection,
    bool ScriptRead,
    bool OptionalRequired,
    bool UndoRedo,
    bool Persistence,
    bool InvalidBlocked,
    bool HumanDiagnostics);

internal sealed record StudioPlaceableFactionDecisionPreview(
    bool Succeeded,
    string Summary,
    string Code);

internal sealed record StudioUx1OSmokeResult(
    bool Succeeded,
    bool FiveTriggers,
    bool DialogueAndEntryPoints,
    bool PriorityAndOnce,
    bool ConversationPreview,
    bool DryRun,
    bool CommandParity,
    bool InstanceSelection,
    bool Preservation,
    bool RuntimeProjection,
    bool ScriptRead,
    bool OptionalRequired,
    bool UndoRedo,
    bool Persistence,
    bool InvalidBlocked,
    bool HumanDiagnostics);

internal sealed record StudioPlaceableConversationPreview(
    bool Succeeded,
    string Summary,
    string Code,
    Guid? BindingId,
    Guid? DialogueId);

internal sealed record StudioUx1PSmokeResult(
    bool Succeeded,
    bool StatesAndMechanisms,
    bool UnlockMethods,
    bool RelockAndJam,
    bool LockPreview,
    bool DryRun,
    bool CommandParity,
    bool InstanceSelection,
    bool Preservation,
    bool RuntimeProjection,
    bool ScriptRead,
    bool OptionalRequired,
    bool UndoRedo,
    bool Persistence,
    bool InvalidBlocked,
    bool HumanDiagnostics);

internal sealed record StudioPlaceableLockPreview(
    bool Succeeded,
    string Summary,
    string Code,
    string Method,
    PlaceableAuthoredLockState State);

internal sealed record StudioUx1QSmokeResult(
    bool Succeeded,
    bool PolicyModes,
    bool ConsumptionDecisions,
    bool VirtualPermission,
    bool CredentialPreview,
    bool DryRun,
    bool CommandParity,
    bool InstanceSelection,
    bool Preservation,
    bool RuntimeProjection,
    bool ScriptRead,
    bool OptionalRequired,
    bool UndoRedo,
    bool Persistence,
    bool InvalidBlocked,
    bool HumanDiagnostics);

internal sealed record StudioPlaceableCredentialPreview(
    bool Succeeded,
    string Summary,
    string Code,
    bool Allowed,
    bool Consume,
    int RemainingCharges,
    string Audit);

internal sealed record StudioUx1RSmokeResult(
    bool Succeeded,
    bool TriggerAndPayload,
    bool DetectAndDisarm,
    bool ResetAndOneShot,
    bool OwnershipVfxFriendly,
    bool TrapPreview,
    bool DryRun,
    bool CommandParity,
    bool InstanceSelection,
    bool Preservation,
    bool RuntimeProjection,
    bool ScriptRead,
    bool OptionalRequired,
    bool UndoRedo,
    bool Persistence,
    bool InvalidBlocked,
    bool HumanDiagnostics);

internal sealed record StudioUx1SSmokeResult(
    bool Succeeded,
    bool TableEntries,
    bool TimingAndSeed,
    bool GeneratedStatePreserved,
    bool ExplicitResetGuard,
    bool LootPreview,
    bool DryRun,
    bool CommandParity,
    bool InstanceSelection,
    bool Preservation,
    bool RuntimeProjection,
    bool ScriptRead,
    bool OptionalRequired,
    bool StableEntrySelection,
    bool UndoRedo,
    bool Persistence,
    bool InvalidBlocked,
    bool HumanDiagnostics);

internal sealed record StudioUx1TSmokeResult(
    bool Succeeded,
    bool BindingsAndApi,
    bool CanonicalPayload,
    bool OpenClosePreview,
    bool DryRun,
    bool CommandParity,
    bool InstanceSelection,
    bool Preservation,
    bool RuntimeProjection,
    bool ScriptRead,
    bool OptionalRequired,
    bool UndoRedo,
    bool Persistence,
    bool InvalidBlocked,
    bool HumanDiagnostics);

internal sealed record StudioUx1USmokeResult(
    bool Succeeded,
    bool BindingsAndApi,
    bool CanonicalPayload,
    bool BeforeAfterPreview,
    bool ControlledVeto,
    bool DryRun,
    bool CommandParity,
    bool InstanceSelection,
    bool Preservation,
    bool RuntimeProjection,
    bool ScriptRead,
    bool OptionalRequired,
    bool UndoRedo,
    bool Persistence,
    bool InvalidBlocked,
    bool HumanDiagnostics);

internal sealed record StudioPlaceableTrapPreview(
    bool Succeeded,
    string Summary,
    string Code,
    bool OperationSucceeded,
    bool Armed,
    string EventName,
    string Detail);
