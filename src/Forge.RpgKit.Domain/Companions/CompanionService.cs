using Forge.Domain.Spatial;

namespace Forge.RpgKit.Domain.Companions;

public enum RecruitDialogueContext { NotRecruited, RecruitedBySpeaker, RecruitedByOther, Unavailable }
public enum CompanionPersistence { Session, Area, Campaign, Permanent }
public sealed record RecruitPolicy(IReadOnlySet<string> Conditions, decimal Cost, string CurrencyId, double MinimumReputation, IReadOnlyList<string> ConsequenceIds, bool AllowDialogue, bool AllowScript);
public sealed record CompanionComponent(Guid Id, int SchemaVersion, string Provenance, Guid CreatureDefinitionId, RecruitPolicy RecruitPolicy, string OwnerId, CompanionPersistence Persistence, string InventoryId, TransformValue Position, IReadOnlySet<string> CommandIds, IReadOnlySet<string> DialogueContextIds, string EquipmentPolicyId, string DeathPolicyId);
public sealed record CompanionPartyState(string PartyId, int Capacity, IReadOnlyList<Guid> CompanionIds, IReadOnlyDictionary<string, decimal> Funds, IReadOnlySet<string> Conditions, double Reputation);
public sealed record CompanionWorldState(CompanionComponent Companion, CompanionPartyState Party, IReadOnlyDictionary<string, string> InventoryOwners, TransformValue DismissPosition);
public sealed record CompanionTransactionResult(bool Committed, CompanionWorldState State, IReadOnlyList<CompanionDiagnostic> Diagnostics, string SemanticDiff);
public sealed record CompanionDiagnostic(string Code, string Path, string Message);
public sealed record CompanionValidationContext(IReadOnlySet<Guid> CreatureDefinitionIds, IReadOnlySet<string> CommandIds, IReadOnlySet<string> InventoryIds, IReadOnlySet<string> EquipmentPolicyIds, IReadOnlySet<string> DeathPolicyIds, IReadOnlySet<string> DialogueContextIds, IReadOnlySet<string> CurrencyIds, int MaximumPartyCapacity);

public static class CompanionService
{
    public static RecruitDialogueContext DialogueContext(CompanionComponent companion, string speakerId, bool available) => !available ? RecruitDialogueContext.Unavailable : string.IsNullOrWhiteSpace(companion.OwnerId) ? RecruitDialogueContext.NotRecruited : companion.OwnerId == speakerId ? RecruitDialogueContext.RecruitedBySpeaker : RecruitDialogueContext.RecruitedByOther;

    public static CompanionTransactionResult Recruit(CompanionWorldState original, string ownerId)
    {
        var companion = original.Companion; var party = original.Party; var path = $"companions/{companion.Id}/recruit"; var errors = new List<CompanionDiagnostic>();
        if (!string.IsNullOrWhiteSpace(companion.OwnerId)) errors.Add(new("RPG_COMPANION_ALREADY_OWNED", path, "Companion already has an owner."));
        if (party.CompanionIds.Count >= party.Capacity) errors.Add(new("RPG_COMPANION_PARTY_FULL", path, "Party companion capacity is reached."));
        if (!companion.RecruitPolicy.Conditions.All(party.Conditions.Contains) || party.Reputation < companion.RecruitPolicy.MinimumReputation) errors.Add(new("RPG_COMPANION_RECRUIT_CONDITION_FAILED", path, "Recruit conditions or reputation are unmet."));
        if (!party.Funds.TryGetValue(companion.RecruitPolicy.CurrencyId, out var funds) || funds < companion.RecruitPolicy.Cost) errors.Add(new("RPG_COMPANION_RECRUIT_COST_UNPAID", path, "Recruit cost cannot be paid."));
        if (errors.Count != 0) return new(false, original, errors, "no-change");
        var updatedFunds = party.Funds.ToDictionary(); updatedFunds[companion.RecruitPolicy.CurrencyId] -= companion.RecruitPolicy.Cost;
        var owners = original.InventoryOwners.ToDictionary(); owners[companion.InventoryId] = ownerId;
        return new(true, original with { Companion = companion with { OwnerId = ownerId }, Party = party with { CompanionIds = party.CompanionIds.Append(companion.Id).ToArray(), Funds = updatedFunds }, InventoryOwners = owners }, [], $"recruit:{companion.Id}|owner:{ownerId}|cost:{companion.RecruitPolicy.Cost}");
    }

    public static CompanionTransactionResult Dismiss(CompanionWorldState original, string ownerId, TransformValue position)
    {
        var path = $"companions/{original.Companion.Id}/dismiss";
        if (original.Companion.OwnerId != ownerId) return new(false, original, [new("RPG_COMPANION_DISMISS_FORBIDDEN", path, "Only the current owner can dismiss this companion.")], "no-change");
        var owners = original.InventoryOwners.ToDictionary(); owners[original.Companion.InventoryId] = original.Companion.Id.ToString();
        return new(true, original with { Companion = original.Companion with { OwnerId = "", Position = position }, Party = original.Party with { CompanionIds = original.Party.CompanionIds.Where(id => id != original.Companion.Id).ToArray() }, InventoryOwners = owners, DismissPosition = position }, [], $"dismiss:{original.Companion.Id}|owner:{ownerId}");
    }

    public static IReadOnlyList<CompanionDiagnostic> Validate(CompanionComponent companion, CompanionValidationContext context)
    {
        var root = $"companions/{companion.Id}"; var result = new List<CompanionDiagnostic>();
        if (companion.Id == Guid.Empty || companion.SchemaVersion != 1 || string.IsNullOrWhiteSpace(companion.Provenance) || !context.CreatureDefinitionIds.Contains(companion.CreatureDefinitionId)) result.Add(new("RPG_COMPANION_IDENTITY_INVALID", root, "Identity and creature reference are required."));
        if (companion.CommandIds.Count == 0 || companion.CommandIds.Any(id => !context.CommandIds.Contains(id))) result.Add(new("RPG_COMPANION_COMMAND_INVALID", $"{root}/commands", "Companion commands must resolve."));
        if (!context.InventoryIds.Contains(companion.InventoryId) || !context.EquipmentPolicyIds.Contains(companion.EquipmentPolicyId)) result.Add(new("RPG_COMPANION_EQUIPMENT_INVALID", $"{root}/equipment", "Inventory and equipment policy must resolve."));
        if (!context.DeathPolicyIds.Contains(companion.DeathPolicyId)) result.Add(new("RPG_COMPANION_DEATH_POLICY_INVALID", $"{root}/deathPolicy", "Death policy must resolve."));
        if (companion.DialogueContextIds.Count < 2 || companion.DialogueContextIds.Any(id => !context.DialogueContextIds.Contains(id))) result.Add(new("RPG_COMPANION_DIALOGUE_INVALID", $"{root}/dialogueContexts", "Recruit and owner dialogue contexts must resolve."));
        if (!context.CurrencyIds.Contains(companion.RecruitPolicy.CurrencyId) || companion.RecruitPolicy.Cost < 0 || companion.RecruitPolicy.MinimumReputation < -1 || (!companion.RecruitPolicy.AllowDialogue && !companion.RecruitPolicy.AllowScript)) result.Add(new("RPG_COMPANION_RECRUIT_POLICY_INVALID", $"{root}/recruitPolicy", "Recruit path, currency, cost or reputation is invalid."));
        return result;
    }
}
