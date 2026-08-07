using Forge.Domain.Spatial;

namespace Forge.Domain.WorldFeatures;

public static class WorldFeatureRuntimeResolver
{
    public sealed record AccessResult(bool Allowed, bool Consume, int RemainingCharges, string Reason, string Audit);
    public sealed record EventResult(bool Dispatched, string? ScriptId, string Reason);
    public sealed record DoorDamageResult(double Health, double AppliedDamage, bool Breached, bool NavigationChanged, string Visual);
    public sealed record TriggerResult(bool Fired, bool Revealed, bool RestAllowed, string Reason, IReadOnlyList<string> Effects);
    public sealed record EncounterResult(bool Spawn, bool Reinforce, bool Cleanup, int NextCycle, string Reason);
    public sealed record VehicleSample(Vector3Value Position, double SpeedLimit, bool AtStop, double WaitSeconds);
    public sealed record MerchantQuote(bool Accepted, decimal Price, string CurrencyId, decimal TreasuryAfter, string Audit);
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
