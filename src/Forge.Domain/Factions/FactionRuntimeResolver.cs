namespace Forge.Domain.Factions;

public static class FactionRuntimeResolver
{
    public static FactionRuntimeResolution Resolve(FactionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.Advanced is null)
        {
            var diagnostics = catalog.RequireAdvancedRuntime
                ? new[] { new FactionDiagnostic(FactionValidator.AdvancedMissingCode, "module/factions/advanced",
                    "The module requires the advanced faction profile at runtime.") }
                : [];
            return new(null, diagnostics, !catalog.RequireAdvancedRuntime);
        }

        var validation = FactionValidator.Validate(catalog);
        return new(catalog.Advanced, validation, false);
    }

    public static FactionEffectiveDefinition EffectiveDefinition(FactionCatalog catalog, Guid factionId)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var faction = catalog.Factions.SingleOrDefault(item => item.Id == factionId)
            ?? throw new KeyNotFoundException("FACTION_HANDLE_INVALID");
        var policy = catalog.Advanced?.Inheritance.SingleOrDefault(item => item.FactionId == factionId);
        var tags = new HashSet<string>(StringComparer.Ordinal);
        var policies = new Dictionary<string, string>(StringComparer.Ordinal);
        var path = new List<Guid>();
        Collect(catalog, faction, policy, tags, policies, path, new HashSet<Guid>(), true, true);
        return new(factionId, tags, policies, path);
    }

    public static FactionRelationship? EffectiveRelationship(
        FactionCatalog catalog, Guid sourceFactionId, Guid targetFactionId)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var direct = catalog.Relationships.FirstOrDefault(item =>
            item.SourceId == sourceFactionId && item.TargetId == targetFactionId);
        if (direct is not null) return direct;

        var source = catalog.Factions.SingleOrDefault(item => item.Id == sourceFactionId);
        var policy = catalog.Advanced?.Inheritance.SingleOrDefault(item => item.FactionId == sourceFactionId);
        if (source is null || policy?.InheritRelationships != true) return null;
        var queue = new Queue<Guid>(source.ParentIds);
        var visited = new HashSet<Guid> { sourceFactionId };
        while (queue.TryDequeue(out var parentId))
        {
            if (!visited.Add(parentId)) continue;
            var inherited = catalog.Relationships.FirstOrDefault(item =>
                item.SourceId == parentId && item.TargetId == targetFactionId);
            if (inherited is not null) return inherited with { SourceId = sourceFactionId };
            var parent = catalog.Factions.SingleOrDefault(item => item.Id == parentId);
            if (parent is not null)
                foreach (var ancestor in parent.ParentIds) queue.Enqueue(ancestor);
        }
        return null;
    }

    public static IReadOnlyList<FactionMembership> Memberships(
        FactionCatalog catalog, string actorId, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        return (catalog.Advanced?.Memberships ?? [])
            .Where(item => string.Equals(item.ActorId, actorId, StringComparison.Ordinal) &&
                (item.ExpiresAt is null || item.ExpiresAt > at))
            .OrderByDescending(item => item.IsPrimary)
            .ThenByDescending(item => item.Loyalty)
            .ThenByDescending(item => item.Weight)
            .ThenBy(item => item.FactionId)
            .ToArray();
    }

    public static FactionWitnessResult PropagateWitnesses(
        FactionCatalog catalog,
        IEnumerable<FactionWitnessObservation> observations,
        DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(observations);
        var diagnostics = Resolve(catalog).Diagnostics.ToList();
        if (catalog.Advanced is null) return new([], diagnostics);
        var reports = new List<FactionWitnessReport>();
        foreach (var observation in observations.OrderBy(item => item.Id))
        {
            var policy = catalog.Advanced.WitnessPolicies.SingleOrDefault(item =>
                item.FactionId == observation.WitnessFactionId);
            if (policy is null) continue;
            var ageSeconds = (at - observation.ObservedAt).TotalSeconds;
            if (ageSeconds < 0 || ageSeconds > policy.MemorySeconds || !observation.CanCommunicate ||
                observation.DistanceMeters > policy.CommunicationRadiusMeters ||
                observation.Perception < policy.MinimumPerception ||
                observation.Evidence < policy.MinimumEvidence)
                continue;
            reports.Add(new(observation.Id, observation.IncidentId, observation.SubjectActorId,
                observation.WitnessActorId, observation.WitnessFactionId, policy.ReputationDelta,
                observation.Evidence, at));
        }
        return new(reports, diagnostics);
    }

    public static FactionDynamicResult CreateDynamic(
        FactionAdvancedProfile profile, DynamicFactionDefinition definition, bool isServer)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(definition);
        if (!isServer || !profile.ServerAuthoritative)
            return Denied(profile, definition.FactionId, "faction.dynamic.authority_denied");
        if (profile.DynamicFactions.Any(item => item.Id == definition.Id || item.FactionId == definition.FactionId))
            return new(profile, [], [new("faction.dynamic.duplicate", "module/factions/advanced/dynamicFactions",
                "Dynamic faction identity is already registered.")]);
        var updated = profile with { DynamicFactions = [.. profile.DynamicFactions, definition] };
        return new(updated,
            [new("faction.dynamic.created", definition.FactionId, null,
                $"Dynamic faction '{definition.FactionId}' created.", definition.Revision)], []);
    }

    public static FactionDynamicResult TransitionDiplomacy(
        FactionAdvancedProfile profile,
        Guid dynamicId,
        FactionDiplomacyState diplomacy,
        bool isServer)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(diplomacy);
        var dynamic = profile.DynamicFactions.SingleOrDefault(item => item.Id == dynamicId);
        if (dynamic is null)
            return new(profile, [], [new("faction.dynamic.missing",
                $"module/factions/advanced/dynamicFactions/{dynamicId}", "Dynamic faction does not exist.")]);
        if (!isServer || !profile.ServerAuthoritative)
            return Denied(profile, dynamic.FactionId, "faction.dynamic.authority_denied");
        var states = dynamic.Diplomacy.Where(item => item.TargetFactionId != diplomacy.TargetFactionId)
            .Append(diplomacy).OrderBy(item => item.TargetFactionId).ToArray();
        var nextRevision = checked(dynamic.Revision + 1);
        var changed = dynamic with { Diplomacy = states, Revision = nextRevision };
        var updated = profile with
        {
            DynamicFactions = profile.DynamicFactions
                .Select(item => item.Id == dynamicId ? changed : item).ToArray(),
        };
        return new(updated,
            [new("faction.dynamic.diplomacy_changed", dynamic.FactionId, diplomacy.TargetFactionId,
                $"Diplomacy changed to {diplomacy.Status}.", nextRevision)], []);
    }

    public static FactionDynamicResult AdvanceExpirations(
        FactionAdvancedProfile profile, DateTimeOffset at, bool isServer)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!isServer || !profile.ServerAuthoritative)
            return Denied(profile, Guid.Empty, "faction.dynamic.authority_denied");
        var events = new List<FactionRuntimeEvent>();
        var updated = profile.DynamicFactions.Select(item =>
        {
            var active = item.Active && (item.ExpiresAt is null || item.ExpiresAt > at);
            var diplomacy = item.Diplomacy.Where(state => state.ExpiresAt is null || state.ExpiresAt > at).ToArray();
            var changed = active != item.Active || diplomacy.Length != item.Diplomacy.Count;
            if (!changed) return item;
            var revision = checked(item.Revision + 1);
            events.Add(new(active ? "faction.dynamic.diplomacy_expired" : "faction.dynamic.expired",
                item.FactionId, null, active ? "Expired diplomacy was removed." : "Dynamic faction expired.",
                revision));
            return item with { Active = active, Diplomacy = diplomacy, Revision = revision };
        }).ToArray();
        return new(profile with { DynamicFactions = updated }, events, []);
    }

    private static void Collect(
        FactionCatalog catalog,
        FactionDefinition faction,
        FactionInheritancePolicy? policy,
        HashSet<string> tags,
        Dictionary<string, string> policies,
        List<Guid> path,
        HashSet<Guid> visited,
        bool includeTags,
        bool includePolicies)
    {
        if (!visited.Add(faction.Id)) return;
        path.Add(faction.Id);
        if (policy is not null &&
            ((includeTags && policy.InheritTags) || (includePolicies && policy.InheritPolicies)))
        {
            foreach (var parentId in faction.ParentIds)
            {
                var parent = catalog.Factions.SingleOrDefault(item => item.Id == parentId);
                if (parent is null) continue;
                var parentPolicy = catalog.Advanced?.Inheritance.SingleOrDefault(item => item.FactionId == parentId);
                Collect(catalog, parent, parentPolicy, tags, policies, path, visited,
                    includeTags && policy.InheritTags, includePolicies && policy.InheritPolicies);
            }
        }
        if (includeTags)
            foreach (var tag in faction.Tags.OrderBy(item => item, StringComparer.Ordinal)) tags.Add(tag);
        if (includePolicies)
            foreach (var item in (faction.Policies ?? new Dictionary<string, string>())
                .OrderBy(item => item.Key, StringComparer.Ordinal)) policies[item.Key] = item.Value;
    }

    private static FactionDynamicResult Denied(
        FactionAdvancedProfile profile, Guid factionId, string code) =>
        new(profile, [], [new(code, $"module/factions/advanced/dynamicFactions/{factionId}",
            "Only the authoritative server can mutate dynamic faction state.")]);
}
