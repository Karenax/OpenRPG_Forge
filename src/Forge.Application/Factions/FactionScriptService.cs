using Forge.Domain.Factions;

namespace Forge.Application.Factions;

public sealed record FactionHandle(Guid Id, int SchemaVersion);

public sealed record FactionScriptSnapshot(
    FactionHandle Handle,
    string Provenance,
    bool ServerAuthoritative,
    int FactionCount,
    int InheritancePolicyCount,
    int WitnessPolicyCount,
    int MembershipCount,
    int DynamicFactionCount);

/// <summary>
/// Typed ForgeScript boundary. Catalog authoring remains owned by SetFactionCatalogHandler;
/// runtime diplomacy mutations are accepted only on the authoritative server.
/// </summary>
public sealed class FactionScriptService
{
    private FactionCatalog _catalog;

    public FactionScriptService(FactionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var diagnostics = FactionValidator.Validate(catalog);
        if (diagnostics.Count > 0)
            throw new ArgumentException(string.Join(';', diagnostics.Select(item => item.Code)), nameof(catalog));
        if (catalog.Advanced is null)
            throw new ArgumentException("FACTION_ADVANCED_PROFILE_MISSING", nameof(catalog));
        _catalog = catalog;
    }

    public FactionHandle Handle => new(_catalog.Advanced!.Id, _catalog.Advanced.SchemaVersion);

    public FactionScriptSnapshot Read(FactionHandle handle)
    {
        var profile = EnsureHandle(handle);
        return new(handle, profile.Provenance, profile.ServerAuthoritative, _catalog.Factions.Count,
            profile.Inheritance.Count, profile.WitnessPolicies.Count, profile.Memberships.Count,
            profile.DynamicFactions.Count);
    }

    public FactionEffectiveDefinition EffectiveDefinition(FactionHandle handle, Guid factionId)
    {
        EnsureHandle(handle);
        return FactionRuntimeResolver.EffectiveDefinition(_catalog, factionId);
    }

    public IReadOnlyList<FactionMembership> Memberships(
        FactionHandle handle, string actorId, DateTimeOffset at)
    {
        EnsureHandle(handle);
        return FactionRuntimeResolver.Memberships(_catalog, actorId, at);
    }

    public FactionWitnessResult PropagateWitnesses(
        FactionHandle handle, IEnumerable<FactionWitnessObservation> observations, DateTimeOffset at)
    {
        EnsureHandle(handle);
        return FactionRuntimeResolver.PropagateWitnesses(_catalog, observations, at);
    }

    public FactionDynamicResult CreateDynamic(
        FactionHandle handle, DynamicFactionDefinition definition, bool isServer)
    {
        var profile = EnsureHandle(handle);
        return Update(FactionRuntimeResolver.CreateDynamic(profile, definition, isServer));
    }

    public FactionDynamicResult TransitionDiplomacy(
        FactionHandle handle, Guid dynamicId, FactionDiplomacyState diplomacy, bool isServer)
    {
        var profile = EnsureHandle(handle);
        return Update(FactionRuntimeResolver.TransitionDiplomacy(profile, dynamicId, diplomacy, isServer));
    }

    public FactionDynamicResult AdvanceExpirations(
        FactionHandle handle, DateTimeOffset at, bool isServer)
    {
        var profile = EnsureHandle(handle);
        return Update(FactionRuntimeResolver.AdvanceExpirations(profile, at, isServer));
    }

    private FactionAdvancedProfile EnsureHandle(FactionHandle handle)
    {
        var profile = _catalog.Advanced!;
        if (handle.Id != profile.Id || handle.SchemaVersion != profile.SchemaVersion)
            throw new KeyNotFoundException("FACTION_HANDLE_INVALID");
        return profile;
    }

    private FactionDynamicResult Update(FactionDynamicResult result)
    {
        if (result.Diagnostics.Count == 0)
            _catalog = _catalog with { Advanced = result.Profile };
        return result;
    }
}
