using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Domain.Companions;
using Forge.RpgKit.Domain.Creatures;

namespace Forge.RpgKit.Application.Companions;

public sealed record CompanionFoundationResolution(
    CompanionWorldProfile.FoundationProfile? Foundation,
    bool OptionalFallback,
    IReadOnlyList<CompanionDiagnostic> Diagnostics);

public sealed record CompanionFoundationTransactionPlan(
    Guid TransactionId,
    bool DryRun,
    CompanionTransactionResult Result);

public static class CompanionFoundationService
{
    public static CompanionFoundationResolution Resolve(CompanionWorldProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Foundation is not null) return new(profile.Foundation, false, []);
        return profile.FoundationRequiredAtRuntime
            ? new(null, false,
            [
                new("RPG_COMPANION_FOUNDATION_REQUIRED", $"companions/{profile.Id}/foundation",
                    "Required companion foundation profile is missing at runtime."),
            ])
            : new(null, true, []);
    }

    public static CompanionComponent CreateComponent(
        CompanionWorldProfile profile,
        string ownerId,
        TransformValue position)
    {
        var foundation = Require(profile);
        var recruitable = foundation.Recruitable;
        var validation = profile.Validation;
        return new(profile.Id, foundation.SchemaVersion, foundation.Provenance,
            profile.BlueprintId.Value,
            new(recruitable.Conditions.ToHashSet(StringComparer.Ordinal), recruitable.Cost,
                recruitable.CurrencyId, recruitable.MinimumReputation, recruitable.ConsequenceIds,
                recruitable.AllowDialogue, recruitable.AllowScript), ownerId,
            foundation.Membership.Persistence switch
            {
                CompanionWorldProfile.CompanionPersistence.Session => CompanionPersistence.Session,
                CompanionWorldProfile.CompanionPersistence.Area => CompanionPersistence.Area,
                CompanionWorldProfile.CompanionPersistence.Permanent => CompanionPersistence.Permanent,
                _ => CompanionPersistence.Campaign,
            },
            foundation.Membership.InventoryId, position,
            foundation.OwnerDialogue.CommandIds.ToHashSet(StringComparer.Ordinal),
            new HashSet<string>([foundation.PreRecruitment.ContextId, foundation.OwnerDialogue.ContextId],
                StringComparer.Ordinal),
            validation?.Equipment.EquipmentPolicyId ?? "companion.standard",
            validation?.Death.PolicyId ?? "companion.recoverable");
    }

    public static CompanionPartyState CreatePartyState(
        CompanionWorldProfile profile,
        string partyId,
        IReadOnlyList<Guid> companionIds,
        IReadOnlyDictionary<string, decimal> funds,
        IReadOnlySet<string> conditions,
        double reputation)
    {
        var party = Require(profile).Party;
        return new(partyId, party.CompanionSlots, companionIds, funds, conditions, reputation);
    }

    public static CompanionFoundationTransactionPlan Recruit(
        CompanionWorldProfile profile,
        CompanionWorldState original,
        string ownerId,
        bool dryRun)
    {
        var result = CompanionService.Recruit(original, ownerId);
        return new(StableTransactionId(profile.Id, "recruit", ownerId, original.Party.PartyId), dryRun, result);
    }

    public static CompanionFoundationTransactionPlan Dismiss(
        CompanionWorldProfile profile,
        CompanionWorldState original,
        string ownerId,
        TransformValue position,
        bool dryRun)
    {
        var result = CompanionService.Dismiss(original, ownerId, position);
        return new(StableTransactionId(profile.Id, "dismiss", ownerId, original.Party.PartyId), dryRun, result);
    }

    public static RecruitDialogueContext DialogueContext(
        CompanionWorldProfile profile,
        string ownerId,
        string speakerId,
        bool available) =>
        CompanionService.DialogueContext(CreateComponent(profile, ownerId, TransformValue.Identity), speakerId,
            available);

    public static IReadOnlyList<CompanionDiagnostic> ValidateParty(
        CompanionWorldProfile profile,
        PartyRoster roster)
    {
        var party = Require(profile).Party;
        var roles = party.RoleLimits.ToDictionary(
            item => Enum.Parse<EncounterRole>(item.Key, ignoreCase: true),
            item => item.Value);
        return CompanionControlService.ValidateParty(new PartyPolicy(party.CompanionSlots, party.SummonSlots,
            party.PetSlots, party.TemporaryAllySlots, party.ServerActorLimit, roles), roster);
    }

    private static CompanionWorldProfile.FoundationProfile Require(CompanionWorldProfile profile)
    {
        var resolved = Resolve(profile);
        if (resolved.Diagnostics.Count > 0)
            throw new InvalidOperationException(string.Join(';', resolved.Diagnostics.Select(item => item.Code)));
        return resolved.Foundation ?? throw new InvalidOperationException("RPG_COMPANION_FOUNDATION_DISABLED");
    }

    private static Guid StableTransactionId(Guid profileId, string operation, string ownerId, string partyId)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{profileId:D}:{operation}:{ownerId}:{partyId}"));
        Span<byte> guid = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guid);
        guid[7] = (byte)((guid[7] & 0x0f) | 0x40);
        guid[8] = (byte)((guid[8] & 0x3f) | 0x80);
        return new Guid(guid);
    }
}
