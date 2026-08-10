using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;

namespace Forge.Domain.WorldFeatures;

public static class WorldFeatureRuntimeResolver
{
    public sealed record AccessResult(bool Allowed, bool Consume, int RemainingCharges, string Reason, string Audit);
    public sealed record PlaceableFoundationResolution(bool Available, PlaceableWorldProfile.FoundationPolicy? Foundation,
        PlaceableWorldProfile.TransformPolicy? SnappedTransform, string DiagnosticCode, string Overlay);
    public sealed record PlaceableInteractionExecuted(Guid EventId, int SchemaVersion, string Provenance,
        Guid PlaceableId, Guid VerbId, string ActorId, Vector3Value TargetPoint, string Result,
        DateTimeOffset OccurredAt, string EventBinding, string ForgeScriptApiVersion,
        string? BeforeScriptId, string? AfterScriptId);
    public sealed record EventResult(bool Dispatched, string? ScriptId, string Reason);
    public sealed record DoorDamageResult(double Health, double AppliedDamage, bool Breached, bool NavigationChanged, string Visual);
    public sealed record DoorInteractionResult(bool Accepted, string PreviousState, string CurrentState,
        bool CollisionEnabled, string Reason, DoorWorldProfile.PortalPolicy? Portal);
    public sealed record DoorNavigationResult(string StateId, bool ObstacleEnabled, bool LinkEnabled,
        double AnimationProgress, string Visualization, string EventName);
    public sealed record TriggerResult(bool Fired, bool Revealed, bool RestAllowed, string Reason, IReadOnlyList<string> Effects);
    public sealed record TriggerFoundationResolution(bool Available, TriggerWorldProfile.FoundationPolicy? Foundation,
        TriggerWorldProfile.ShapePolicy? SnappedShape, TriggerWorldProfile.TransformPolicy? SnappedTransform,
        string DiagnosticCode, string Overlay);
    public sealed record TriggerVolumeEntered(Guid EventId, int SchemaVersion, string Provenance, Guid TriggerId,
        string EntityId, Vector3Value Point, Vector3Value Direction, IReadOnlySet<string> Layers,
        IReadOnlyDictionary<string, string> State, DateTimeOffset EnteredAt, string EventBinding,
        string ForgeScriptApiVersion, string? BeforeScriptId, string? AfterScriptId);
    public sealed record TriggerVolumeExited(Guid EventId, int SchemaVersion, string Provenance, Guid TriggerId,
        string EntityId, TimeSpan DurationInside, string Reason, DateTimeOffset ExitedAt, string EventBinding,
        string ForgeScriptApiVersion, string? BeforeScriptId, string? AfterScriptId);
    public sealed record TriggerRuntimeState(IReadOnlyDictionary<string, DateTimeOffset> InsideSince,
        IReadOnlyDictionary<string, DateTimeOffset> LastFiredAt, IReadOnlySet<string> FiredEntities, bool FiredGlobally);
    public sealed record TriggerEnterResult(TriggerRuntimeState State, TriggerVolumeEntered? Event,
        IReadOnlyList<TriggerWorldProfile.ActionPolicy> Actions, bool Fired, string Reason);
    public sealed record TriggerExitResult(TriggerRuntimeState State, TriggerVolumeExited? Event);
    public sealed record TriggerValidationResolution(bool Available,
        TriggerWorldProfile.RuntimeValidationPolicy? Validation, string DiagnosticCode, string Summary);
    public sealed record EncounterResult(bool Spawn, bool Reinforce, bool Cleanup, int NextCycle, string Reason);
    public sealed record EncounterActivationResult(bool Activated, Guid? CandidateId, BlueprintId? BlueprintId,
        string Reason, string? ActivationScriptId);
    public sealed record VehicleSample(Vector3Value Position, double SpeedLimit, bool AtStop, double WaitSeconds);
    public sealed record MerchantQuote(bool Accepted, decimal Price, string CurrencyId, decimal TreasuryAfter, string Audit);
    public sealed record MerchantTransactionResolution(bool Available, MerchantWorldProfile.TransactionPolicy? Policy,
        string DiagnosticCode, string Audit);
    public sealed record CompanionDecision(bool DialogueAllowed, string Formation, bool UseAbility, bool HandleTrap,
        bool DirectControl, bool Regroup, string Reason);
    public sealed record ScriptResolution(string? ScriptId, IReadOnlyDictionary<string, string> Flags,
        IReadOnlyDictionary<string, string> AiSettings, bool CameraControlLocked, string AbortRecovery);

    public static AccessResult ResolvePlaceableAccess(
        PlaceableWorldProfile profile,
        IReadOnlySet<string> credentials,
        IReadOnlySet<string> permissions,
        int availableCharges)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Credential is null)
            return new(true, false, availableCharges, "unrestricted", "access:unrestricted");
        var policy = profile.Credential;
        var allowed = policy.Consumption == PlaceableWorldProfile.CredentialConsumption.VirtualPermission
            ? policy.VirtualPermission is { } permission && permissions.Contains(permission)
            : credentials.Contains(policy.CredentialId) &&
              (policy.Consumption != PlaceableWorldProfile.CredentialConsumption.Charge || availableCharges > 0);
        if (!allowed) return new(false, false, availableCharges, "credential-missing", "access:denied");
        var consume = policy.Consumption is PlaceableWorldProfile.CredentialConsumption.Consume or
            PlaceableWorldProfile.CredentialConsumption.Charge;
        var remaining = policy.Consumption == PlaceableWorldProfile.CredentialConsumption.Charge
            ? Math.Max(0, availableCharges - 1)
            : availableCharges;
        return new(true, consume, remaining, "accepted", policy.AuditUse ? "access:accepted:audited" : "access:accepted");
    }

    public static PlaceableFoundationResolution ResolvePlaceableFoundation(PlaceableWorldProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Foundation is null)
            return profile.FoundationRequiredAtRuntime
                ? new(false, null, null, "WORLD_PLACEABLE_FOUNDATION_REQUIRED_MISSING", "placeable:required-missing")
                : new(false, null, null, string.Empty, "placeable:optional-fallback");
        var transform = profile.Foundation.Transform;
        if (transform.SnapIncrement > 0)
        {
            float Snap(float value) => (float)(Math.Round(value / transform.SnapIncrement,
                MidpointRounding.AwayFromZero) * transform.SnapIncrement);
            transform = transform with
            {
                Position = new(Snap(transform.Position.X), Snap(transform.Position.Y), Snap(transform.Position.Z)),
            };
        }
        var overlay = FormattableString.Invariant(
            $"placeable:{profile.Id:D}:pivot={transform.Pivot.X:0.###},{transform.Pivot.Y:0.###},{transform.Pivot.Z:0.###}:snap={transform.SnapIncrement:0.###}:surface={transform.AlignToSurface}");
        return new(true, profile.Foundation, transform, string.Empty, overlay);
    }

    public static PlaceableInteractionExecuted ExecutePlaceableUse(PlaceableWorldProfile profile, Guid eventId,
        Guid verbId, string actorId, Vector3Value targetPoint, string result, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var foundation = profile.Foundation ?? throw new InvalidOperationException(
            profile.FoundationRequiredAtRuntime ? "WORLD_PLACEABLE_FOUNDATION_REQUIRED_MISSING" :
            "WORLD_PLACEABLE_FOUNDATION_UNAVAILABLE");
        var policy = foundation.OnUsed ?? throw new InvalidOperationException(
            foundation.OnUsedRequiredAtRuntime ? "WORLD_PLACEABLE_ON_USED_REQUIRED_MISSING" :
            "WORLD_PLACEABLE_ON_USED_UNAVAILABLE");
        var interaction = foundation.Interaction ?? throw new InvalidOperationException("WORLD_PLACEABLE_NOT_INTERACTABLE");
        if (eventId == Guid.Empty || interaction.Verbs.All(item => item.Id != verbId))
            throw new InvalidOperationException("WORLD_PLACEABLE_VERB_UNAVAILABLE");
        if (string.IsNullOrWhiteSpace(actorId) || string.IsNullOrWhiteSpace(result))
            throw new InvalidOperationException("WORLD_PLACEABLE_INTERACTION_CONTEXT_INVALID");
        return new(eventId, 1, policy.Provenance, profile.Id, verbId, actorId, targetPoint, result, occurredAt,
            policy.EventBinding, policy.ForgeScriptApiVersion, policy.BeforeScriptId, policy.AfterScriptId);
    }

    public static EventResult ResolvePlaceableEvent(PlaceableWorldProfile profile, string eventName,
        int dispatchesThisMinute, double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var binding = profile.Reactive.Events.FirstOrDefault(item => string.Equals(item.Event, eventName, StringComparison.Ordinal));
        if (binding is null) return new(false, null, "unbound");
        if (string.Equals(eventName, "heartbeat", StringComparison.Ordinal))
        {
            var scheduler = profile.Reactive.Scheduler;
            if (!scheduler.Enabled || elapsedSeconds < scheduler.IntervalSeconds) return new(false, null, "cadence");
            if (dispatchesThisMinute >= scheduler.BudgetPerMinute) return new(false, null, "budget");
        }
        return new(true, binding.ScriptId, binding.Authoritative ? "server" : "local");
    }

    public static DoorDamageResult ApplyDoorDamage(DoorWorldProfile profile, double currentHealth, double rawDamage)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Damage is null || profile.Damage.PlotProtected)
            return new(currentHealth, 0, false, false, profile.Damage?.DamagedVisual ?? string.Empty);
        var applied = Math.Max(0, rawDamage - profile.Damage.Hardness);
        var health = Math.Clamp(currentHealth - applied, 0, profile.Damage.MaximumHealth);
        var breached = health <= 0;
        return new(health, applied, breached, breached && profile.Damage.UpdateNavigation,
            breached ? profile.Damage.BreachedVisual : profile.Damage.DamagedVisual);
    }

    public static DoorInteractionResult InteractWithDoor(
        DoorWorldProfile profile,
        string currentState,
        string actorSide,
        IReadOnlySet<string> permissions,
        int queuedRequests,
        IReadOnlySet<string> conditions)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Foundation is not { } foundation)
            return new(false, currentState, currentState, true, "foundation-missing", null);
        var current = foundation.States.SingleOrDefault(item => item.Id == currentState)
            ?? throw new InvalidOperationException("WORLD_DOOR_STATE_MISSING");
        var interaction = foundation.Interaction;
        if (queuedRequests >= interaction.QueueCapacity)
            return new(false, currentState, currentState, current.CollisionEnabled, "queue-full", null);
        if (!string.Equals(interaction.AllowedSide, "both", StringComparison.Ordinal) &&
            !string.Equals(interaction.AllowedSide, actorSide, StringComparison.Ordinal))
            return new(false, currentState, currentState, current.CollisionEnabled, "wrong-side", null);
        if (foundation.Portal is { } portal && portal.Permissions.Any(permission => !permissions.Contains(permission)))
            return new(false, currentState, currentState, current.CollisionEnabled, "permission-denied", null);
        var transition = foundation.Transitions.FirstOrDefault(item => item.FromStateId == currentState &&
            item.Trigger == "interact" && (string.IsNullOrWhiteSpace(item.Condition) || conditions.Contains(item.Condition)));
        if (transition is null)
            return new(false, currentState, currentState, current.CollisionEnabled,
                interaction.FailureFeedbackKey, null);
        var target = foundation.States.Single(item => item.Id == transition.ToStateId);
        return new(true, currentState, target.Id, target.CollisionEnabled, "accepted", foundation.Portal);
    }

    public static DoorNavigationResult SynchronizeDoorNavigation(
        DoorWorldProfile profile,
        string stateId,
        double animationProgress)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var foundation = profile.Foundation ?? throw new InvalidOperationException("WORLD_DOOR_FOUNDATION_MISSING");
        var navigation = foundation.Navigation ?? throw new InvalidOperationException("WORLD_DOOR_NAVIGATION_MISSING");
        var state = foundation.States.SingleOrDefault(item => item.Id == stateId)
            ?? throw new InvalidOperationException("WORLD_DOOR_STATE_MISSING");
        var progress = Math.Clamp(animationProgress, 0, 1);
        var traversable = navigation.TraversableStateIds.Contains(stateId, StringComparer.Ordinal);
        if (navigation.BlockingStateIds.Contains(stateId, StringComparer.Ordinal)) traversable = false;
        else if (state.Kind == DoorWorldProfile.StateKind.Opening)
            traversable = progress >= navigation.OpeningPassableAt;
        else if (state.Kind == DoorWorldProfile.StateKind.Closing)
            traversable = progress < navigation.ClosingBlockedAt;
        else if (!navigation.TraversableStateIds.Contains(stateId, StringComparer.Ordinal))
            traversable = !state.CollisionEnabled;
        var obstacle = !traversable;
        var visualization = navigation.VisualizeInEditor
            ? $"obstacle:{navigation.ObstacleId}:{(obstacle ? "blocked" : "clear")}|link:{navigation.LinkId}:{(traversable ? "enabled" : "disabled")}"
            : "hidden";
        return new(stateId, obstacle, traversable, progress, visualization, "DoorNavigationChanged");
    }

    public static EventResult ResolveDoorEvent(DoorWorldProfile profile, string eventName,
        int dispatchesThisMinute, double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var binding = profile.Events.FirstOrDefault(item => string.Equals(item.Event, eventName, StringComparison.Ordinal));
        if (binding is null) return new(false, null, "unbound");
        if (string.Equals(eventName, "heartbeat", StringComparison.Ordinal) &&
            (elapsedSeconds < 1 || dispatchesThisMinute >= 60))
            return new(false, null, dispatchesThisMinute >= 60 ? "budget" : "cadence");
        return new(true, binding.ScriptId, binding.Authoritative ? "server" : "local");
    }

    public static TriggerResult EvaluateTrigger(
        TriggerWorldProfile profile,
        int perception,
        IReadOnlySet<string> credentials,
        IReadOnlySet<string> permissions,
        bool dangerPresent,
        int occupants,
        int dispatchesThisMinute,
        bool manualDirectorFire)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var revealed = profile.Discovery is null || perception >= profile.Discovery.Difficulty;
        if (!revealed) return new(false, false, false, "hidden", []);
        if (profile.Access is { } access && !credentials.Contains(access.CredentialId))
            return new(false, true, false, "credential-missing", [access.DeniedFeedbackKey]);
        if (profile.Interaction is { } interaction && !permissions.Contains(interaction.Permission))
            return new(false, true, false, "permission-denied", [interaction.PromptKey]);
        if (manualDirectorFire && profile.Director is not { AllowManualFire: true })
            return new(false, true, false, "director-forbidden", []);
        if (profile.Scheduler is { } scheduler && scheduler.WhileOccupiedOnly && occupants == 0)
            return new(false, true, false, "unoccupied", []);
        if (profile.Scheduler is { } budget && dispatchesThisMinute >= budget.BudgetPerMinute)
            return new(false, true, false, "budget", []);
        var restAllowed = profile.Rest is { Safe: true } rest && !dangerPresent &&
            (!rest.WholePartyRequired || occupants > 0);
        var effects = new List<string>();
        if (profile.Clue is { } clue) effects.Add($"clue:{clue.TraceId}");
        if (profile.Narrative is { } narrative) effects.Add($"dialogue:{narrative.DialogueId:D}");
        if (restAllowed) effects.Add("rest:safe");
        if (manualDirectorFire) effects.Add("director:audit");
        return new(true, true, restAllowed, manualDirectorFire ? "director" : "activated", effects);
    }

    public static TriggerFoundationResolution ResolveTriggerFoundation(TriggerWorldProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Foundation is null)
            return profile.FoundationRequiredAtRuntime
                ? new(false, null, null, null, "WORLD_TRIGGER_FOUNDATION_REQUIRED_MISSING", "trigger:required-missing")
                : new(false, null, null, null, string.Empty, "trigger:optional-fallback");
        var foundation = profile.Foundation;
        var shape = foundation.Shape;
        var transform = foundation.Transform;
        if (shape.SnapIncrement > 0)
        {
            float Snap(float value) => (float)(Math.Round(value / shape.SnapIncrement,
                MidpointRounding.AwayFromZero) * shape.SnapIncrement);
            Vector3Value SnapValue(Vector3Value value) => new(Snap(value.X), Snap(value.Y), Snap(value.Z));
            shape = shape with
            {
                Vertices = shape.Vertices.Select(SnapValue).ToArray(),
                Holes = shape.Holes.Select(hole => (IReadOnlyList<Vector3Value>)hole.Select(SnapValue).ToArray()).ToArray(),
            };
            transform = transform with { Position = SnapValue(transform.Position) };
        }
        var overlay = FormattableString.Invariant($"trigger:{profile.Id:D}:shape={shape.Kind}:vertices={shape.Vertices.Count}:holes={shape.Holes.Count}:height={shape.Height:0.###}:snap={shape.SnapIncrement:0.###}");
        return new(true, foundation, shape, transform, string.Empty, overlay);
    }

    public static TriggerEnterResult EnterTrigger(TriggerWorldProfile profile, TriggerRuntimeState state,
        string entityId, string entityKind, IReadOnlySet<string> tags, IReadOnlySet<string> layers,
        Vector3Value point, Vector3Value direction, IReadOnlyDictionary<string, string> context,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var foundation = profile.Foundation ?? throw new InvalidOperationException(
            profile.FoundationRequiredAtRuntime ? "WORLD_TRIGGER_FOUNDATION_REQUIRED_MISSING" :
            "WORLD_TRIGGER_FOUNDATION_UNAVAILABLE");
        var filter = foundation.Filter;
        var matches = (filter.EntityKinds.Count == 0 || filter.EntityKinds.Contains(entityKind, StringComparer.Ordinal)) &&
            filter.RequiredTags.All(tags.Contains) && !filter.ExcludedTags.Any(tags.Contains) &&
            (filter.Layers.Count == 0 || filter.Layers.Any(layers.Contains));
        if (!matches) return new(state, null, [], false, "filtered");
        var inside = state.InsideSince.ToDictionary(); inside[entityId] = occurredAt;
        var updated = state with { InsideSince = inside };
        if (!foundation.Conditions.All(condition => context.GetValueOrDefault(condition) == "true"))
            return new(updated, null, [], false, "condition-failed");
        if (foundation.Fire == TriggerWorldProfile.FirePolicy.Once && state.FiredGlobally)
            return new(updated, null, [], false, "already-fired");
        if (foundation.Fire == TriggerWorldProfile.FirePolicy.OncePerEntity && state.FiredEntities.Contains(entityId))
            return new(updated, null, [], false, "entity-already-fired");
        if (state.LastFiredAt.TryGetValue(entityId, out var last) &&
            (occurredAt - last).TotalSeconds < foundation.CooldownSeconds)
            return new(updated, null, [], false, "cooldown");
        var lastFired = state.LastFiredAt.ToDictionary(); lastFired[entityId] = occurredAt;
        updated = updated with
        {
            LastFiredAt = lastFired,
            FiredEntities = state.FiredEntities.Append(entityId).ToHashSet(StringComparer.Ordinal),
            FiredGlobally = state.FiredGlobally || foundation.Fire == TriggerWorldProfile.FirePolicy.Once,
        };
        var policy = foundation.OnEnter;
        var entered = new TriggerVolumeEntered(Guid.NewGuid(), 1, policy.Provenance, profile.Id, entityId,
            point, direction, layers, context, occurredAt, policy.EventBinding, policy.ForgeScriptApiVersion,
            policy.BeforeScriptId, policy.AfterScriptId);
        return new(updated, entered, foundation.Actions, true, "fired");
    }

    public static TriggerExitResult ExitTrigger(TriggerWorldProfile profile, TriggerRuntimeState state,
        string entityId, DateTimeOffset occurredAt, string reason)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var foundation = profile.Foundation ?? throw new InvalidOperationException(
            profile.FoundationRequiredAtRuntime ? "WORLD_TRIGGER_FOUNDATION_REQUIRED_MISSING" :
            "WORLD_TRIGGER_FOUNDATION_UNAVAILABLE");
        if (!state.InsideSince.TryGetValue(entityId, out var enteredAt)) return new(state, null);
        var inside = state.InsideSince.ToDictionary(); inside.Remove(entityId);
        var updated = state with { InsideSince = inside };
        var policy = foundation.OnExit;
        return new(updated, new(Guid.NewGuid(), 1, policy.Provenance, profile.Id, entityId,
            occurredAt - enteredAt, reason, occurredAt, policy.EventBinding, policy.ForgeScriptApiVersion,
            policy.BeforeScriptId, policy.AfterScriptId));
    }

    public static TriggerValidationResolution ResolveTriggerValidation(TriggerWorldProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var foundation = profile.Foundation;
        if (foundation is null)
            return new(false, null,
                profile.FoundationRequiredAtRuntime ? "WORLD_TRIGGER_FOUNDATION_REQUIRED_MISSING" : string.Empty,
                "trigger-validation:foundation-unavailable");
        if (foundation.Validation is null)
            return new(false, null,
                foundation.ValidationRequiredAtRuntime ? "WORLD_TRIGGER_VALIDATION_REQUIRED_MISSING" : string.Empty,
                foundation.ValidationRequiredAtRuntime ? "trigger-validation:required-missing" :
                "trigger-validation:optional-fallback");
        var validation = foundation.Validation;
        var summary = FormattableString.Invariant(
            $"trigger-validation:targets={validation.KnownTargetIds.Count}:scripts={validation.KnownScriptIds.Count}:portals={validation.KnownPortalDestinationIds.Count}:layers={validation.AccessibleLayers.Count}");
        return new(true, validation, string.Empty, summary);
    }

    public static EncounterResult EvaluateEncounter(EncounterWorldProfile profile, int completedCycles,
        int alertLevel, bool visibleToCamera, bool abandoned, double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var cleanup = abandoned && elapsedSeconds >= profile.Cleanup.AbandonedAfterSeconds;
        if (cleanup) return new(false, false, true, completedCycles, "cleanup");
        var canRespawn = profile.Respawn is { } respawn && completedCycles < respawn.MaximumCycles &&
                         elapsedSeconds >= respawn.DelaySeconds;
        var visibilityAllows = profile.Visibility.Mode == EncounterWorldProfile.VisibilityMode.InViewAllowed ||
                               !visibleToCamera;
        var reinforce = profile.Reinforcements is { } reinforcement && alertLevel >= reinforcement.MinimumAlertLevel &&
                        visibilityAllows;
        return new(canRespawn && visibilityAllows, reinforce, false,
            canRespawn && visibilityAllows ? completedCycles + 1 : completedCycles,
            !visibilityAllows ? "visible" : reinforce ? "reinforcement" : canRespawn ? "respawn" : "waiting");
    }

    public static EncounterActivationResult ActivateEncounter(
        EncounterWorldProfile profile,
        IReadOnlySet<string> entityTags,
        string actorFaction,
        bool hasLineOfSight,
        double distance,
        double normalizedRoll,
        IReadOnlySet<string> activeConditions,
        IReadOnlyDictionary<Guid, int> currentAlive)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var foundation = profile.Foundation;
        if (foundation is null) return new(false, null, null, "foundation-missing", null);
        var volume = foundation.Volume;
        if (volume.RequiredEntityTags.Any(tag => !entityTags.Contains(tag)))
            return new(false, null, null, "filter-denied", volume.ActivationScriptId);
        if (volume.AllowedFactions.Count > 0 && !volume.AllowedFactions.Contains(actorFaction, StringComparer.Ordinal))
            return new(false, null, null, "faction-denied", volume.ActivationScriptId);
        if (volume.RequireLineOfSight && !hasLineOfSight)
            return new(false, null, null, "line-of-sight", volume.ActivationScriptId);
        if (volume.ThreatRadius > 0 && distance > volume.ThreatRadius)
            return new(false, null, null, "out-of-range", volume.ActivationScriptId);
        var candidates = foundation.Candidates.Where(item => item.Cost <= foundation.SpawnBudget &&
            (!currentAlive.TryGetValue(item.Id, out var alive) || alive < item.MaximumAlive) &&
            item.Conditions.All(activeConditions.Contains)).ToArray();
        if (candidates.Length == 0) return new(false, null, null, "pool-exhausted", volume.ActivationScriptId);
        var total = candidates.Sum(item => item.Weight);
        var cursor = Math.Clamp(normalizedRoll, 0, .999999999) * total;
        foreach (var candidate in candidates)
        {
            cursor -= candidate.Weight;
            if (cursor < 0)
                return new(true, candidate.Id, candidate.BlueprintId, "activated", volume.ActivationScriptId);
        }
        var fallback = candidates[^1];
        return new(true, fallback.Id, fallback.BlueprintId, "activated", volume.ActivationScriptId);
    }

    public static VehicleSample SampleVehicleRoute(VehicleRouteProfile route, double normalizedProgress)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (route.Points.Count < 2) throw new InvalidOperationException("WORLD_VEHICLE_ROUTE_POINTS_MISSING");
        var progress = Math.Clamp(normalizedProgress, 0, 1);
        var scaled = progress * (route.Points.Count - 1);
        var index = Math.Min(route.Points.Count - 2, (int)Math.Floor(scaled));
        var local = scaled - index;
        var from = route.Points[index].Position;
        var to = route.Points[index + 1].Position;
        var position = new Vector3Value((float)(from.X + ((to.X - from.X) * local)),
            (float)(from.Y + ((to.Y - from.Y) * local)), (float)(from.Z + ((to.Z - from.Z) * local)));
        var laneSpeed = route.Lanes.Min(item => item.SpeedLimit);
        var pointId = local < .5 ? route.Points[index].Id : route.Points[index + 1].Id;
        var stop = route.Stops.FirstOrDefault(item => item.RoutePointId == pointId);
        return new(position, Math.Min(route.DefaultSpeedLimit, laneSpeed), stop is not null, stop?.WaitSeconds ?? 0);
    }

    public static MerchantQuote QuoteMerchant(MerchantWorldProfile profile, string currencyId, decimal basePrice,
        bool stolen, string jurisdiction, double relationship, int negotiationMargin,
        double supply, double demand)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var currency = profile.Currencies.SingleOrDefault(item => item.Id == currencyId);
        var account = profile.Treasury.SingleOrDefault(item => item.CurrencyId == currencyId);
        if (currency is null || account is null) return new(false, 0, currencyId, account?.Balance ?? 0, "currency-missing");
        if (stolen && (!profile.StolenGoods.Accepted || profile.StolenGoods.Jurisdictions.Contains(jurisdiction)))
            return new(false, 0, currencyId, account.Balance, $"crime:{profile.StolenGoods.CrimeEventId}");
        if (relationship < profile.Purchase.MinimumRelationship)
            return new(false, 0, currencyId, account.Balance, "relationship");
        var marketFactor = profile.Market is null ? 1 : Math.Clamp(1 + ((demand * profile.Market.DemandWeight) -
            (supply * profile.Market.SupplyWeight)), .25, 4);
        var negotiation = Math.Clamp(negotiationMargin / 100d, -profile.Negotiation.MaximumMarkup,
            profile.Negotiation.MaximumDiscount);
        var risk = stolen ? 1 + profile.StolenGoods.RiskPremium : 1;
        var price = decimal.Round(basePrice * currency.ExchangeRate * (decimal)marketFactor * (decimal)(1 - negotiation) *
                                  (decimal)risk, 2, MidpointRounding.AwayFromZero);
        if (price > profile.Purchase.MaximumPerItem || price > profile.Purchase.MaximumPerTransaction ||
            profile.Purchase.RequireTreasuryFunds && price > account.Balance)
            return new(false, price, currencyId, account.Balance, "purchase-cap");
        return new(true, price, currencyId, account.Balance - price,
            profile.Ledger.Enabled ? $"ledger:{profile.Id:D}:{currencyId}:{price}" : "accepted");
    }

    public static MerchantTransactionResolution ResolveMerchantTransactions(MerchantWorldProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Transactions is not null)
            return new(true, profile.Transactions, string.Empty,
                $"merchant-transactions:{profile.Id:D}:{profile.Transactions.Id:D}");
        return profile.TransactionsRequiredAtRuntime
            ? new(false, null, "WORLD_MERCHANT_TRANSACTIONS_REQUIRED_MISSING", "merchant-transactions:required-missing")
            : new(false, null, string.Empty, "merchant-transactions:optional-fallback");
    }

    public static CompanionDecision EvaluateCompanion(CompanionWorldProfile profile, string actorRole,
        double healthRatio, bool trapPresent, int trapRisk, bool directControlRequested, bool separated)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var dialogueBehavior = actorRole switch
        {
            "owner" => profile.Dialogue.OwnerBehavior,
            "party" => profile.Dialogue.PartyBehavior,
            "gm" => profile.Dialogue.GameMasterBehavior,
            "hostile" => profile.Dialogue.HostileBehavior,
            _ => profile.Dialogue.OutsiderBehavior,
        };
        var dialogueAllowed = !string.Equals(dialogueBehavior, "deny", StringComparison.OrdinalIgnoreCase);
        var handleTrap = trapPresent && profile.Traps.Detect && trapRisk <= profile.Traps.MaximumRisk &&
                         (!profile.Traps.RequirePlayerCommand || actorRole == "owner");
        var useAbility = healthRatio <= profile.Abilities.HealthThreshold && profile.Abilities.Allowed.Count > 0;
        var control = directControlRequested && profile.DirectControl.Enabled &&
                      (actorRole == "owner" || actorRole == "gm") && profile.DirectControl.AllowPossession;
        var regroup = separated && profile.PartySplit.AllowRegroupRequest;
        return new(dialogueAllowed, $"{profile.Formation.FormationId}:{profile.Formation.Slot}", useAbility,
            handleTrap, control, regroup, regroup ? profile.PartySplit.InstanceSelection : "ready");
    }

    public static ScriptResolution ResolveScript(ScriptAutomationProfile profile, string blueprintTag,
        string eventName, IReadOnlyDictionary<string, string>? flagOverrides = null,
        IReadOnlyDictionary<string, string>? aiOverrides = null, bool cameraSequence = false)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var route = profile.TagRouting.Enabled
            ? profile.TagRouting.Routes.FirstOrDefault(item => item.Enabled &&
                string.Equals(item.BlueprintTag, blueprintTag, StringComparison.Ordinal) &&
                string.Equals(item.Event, eventName, StringComparison.Ordinal))
            : null;
        var flags = profile.ModuleFlags.ToDictionary(item => item.Id,
            item => flagOverrides?.GetValueOrDefault(item.Id) ?? item.DefaultValue, StringComparer.Ordinal);
        var settings = profile.AiSettings.ToDictionary(item => item.Id,
            item => NormalizeAiValue(item, aiOverrides?.GetValueOrDefault(item.Id)), StringComparer.Ordinal);
        return new(route?.ScriptId, flags, settings,
            cameraSequence && profile.Camera.Shots.Any(item => item.LockPlayerControl), profile.Camera.AbortRecovery);
    }

    private static string NormalizeAiValue(ScriptAutomationProfile.AiSetting setting, string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested)) return setting.DefaultValue;
        return setting.AllowedValues.Count == 0 || setting.AllowedValues.Contains(requested, StringComparer.Ordinal)
            ? requested
            : setting.DefaultValue;
    }
}
