using Forge.Domain.Validation;

namespace Forge.Domain.Dialogues;

public static class DialogueExperienceProfileValidator
{
    public const string RequiredMissingCode = "dialogue.experience.required_missing";
    public const string IdentityCode = "dialogue.experience.identity_invalid";
    public const string LifecycleCode = "dialogue.experience.lifecycle_invalid";
    public const string SearchCode = "dialogue.experience.search_invalid";
    public const string ReviewCode = "dialogue.experience.review_invalid";
    public const string MemoryCode = "dialogue.experience.memory_invalid";
    public const string SkillCheckCode = "dialogue.experience.skill_check_invalid";

    public static IReadOnlyList<DomainDiagnostic> Validate(DialogueDefinition dialogue)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        var root = $"dialogues/{dialogue.Id}/experience";
        if (dialogue.Experience is null)
        {
            return dialogue.Metadata.TryGetValue("experience.required", out var required) &&
                   string.Equals(required, "true", StringComparison.OrdinalIgnoreCase)
                ? [new DomainDiagnostic(RequiredMissingCode,
                    "Required dialogue experience profile is missing.", root)]
                : [];
        }

        var profile = dialogue.Experience;
        if (!profile.Enabled && !profile.RequiredAtRuntime) return [];
        var diagnostics = new List<DomainDiagnostic>();
        if (profile.Id == Guid.Empty || profile.SchemaVersion != DialogueExperienceProfile.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(profile.Provenance))
            diagnostics.Add(new DomainDiagnostic(IdentityCode,
                "Dialogue experience identity, schema and provenance are required.", root));

        var lifecycle = profile.Lifecycle;
        if (lifecycle.Id == Guid.Empty || lifecycle.SchemaVersion != DialogueLifecyclePolicy.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(lifecycle.Provenance))
            diagnostics.Add(new DomainDiagnostic(LifecycleCode,
                "Dialogue lifecycle requires stable identity, schema and provenance.", $"{root}/lifecycle"));
        AddActionDiagnostics(lifecycle.CompletedActions, $"{root}/lifecycle/completedActions", diagnostics);
        AddActionDiagnostics(lifecycle.AbortedActions, $"{root}/lifecycle/abortedActions", diagnostics);

        var search = profile.Search;
        try
        {
            _ = DialogueExperienceRuntimeService.Compile(search);
        }
        catch (ArgumentException)
        {
            diagnostics.Add(new DomainDiagnostic(SearchCode,
                "Search requires a bounded valid pattern and safe regular expression.", $"{root}/search/pattern"));
        }
        if (search.Id == Guid.Empty || search.SchemaVersion != DialogueSearchProfile.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(search.Provenance) ||
            search.SpeakerRoles.Any(string.IsNullOrWhiteSpace) ||
            search.SpeakerRoles.Distinct(StringComparer.Ordinal).Count() != search.SpeakerRoles.Count ||
            search.ConditionKinds.Any(item => !Enum.IsDefined(item)) ||
            search.ActionKinds.Any(item => !Enum.IsDefined(item)))
            diagnostics.Add(new DomainDiagnostic(SearchCode,
                "Search identity, filters and provenance must be valid and deterministic.", $"{root}/search"));

        var review = profile.Review;
        if (review.Id == Guid.Empty || review.SchemaVersion != DialogueReviewProfile.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(review.Provenance) || review.Badges.Any(item => !Enum.IsDefined(item)) ||
            review.Badges.Distinct().Count() != review.Badges.Count ||
            review.SpeakerRoles.Any(string.IsNullOrWhiteSpace) ||
            review.SpeakerRoles.Distinct(StringComparer.Ordinal).Count() != review.SpeakerRoles.Count)
            diagnostics.Add(new DomainDiagnostic(ReviewCode,
                "Review badges, roles, schema and provenance must be valid.", $"{root}/review"));

        if (profile.MemorySlots.Select(item => item.Id).Distinct().Count() != profile.MemorySlots.Count ||
            profile.MemorySlots.Select(item => (item.Scope, item.ScopeId, item.Name)).Distinct().Count() !=
            profile.MemorySlots.Count || profile.MemorySlots.Any(item =>
                item.Id == Guid.Empty || item.SchemaVersion != DialogueMemorySlot.CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(item.Provenance) || !Enum.IsDefined(item.Scope) ||
                string.IsNullOrWhiteSpace(item.ScopeId) || string.IsNullOrWhiteSpace(item.Name) ||
                !Enum.IsDefined(item.Type) || !Enum.IsDefined(item.Duration) ||
                (item.DefaultValue is not null &&
                 !DialogueExperienceRuntimeService.CanConvert(item.Type, item.DefaultValue))))
            diagnostics.Add(new DomainDiagnostic(MemoryCode,
                "Memory slots require unique stable typed keys, explicit scope, duration and valid defaults.",
                $"{root}/memorySlots"));

        var nodeIds = dialogue.Nodes.Select(item => item.Id).ToHashSet();
        if (profile.SkillChecks.Select(item => item.Id).Distinct().Count() != profile.SkillChecks.Count ||
            profile.SkillChecks.Select(item => item.NodeId).Distinct().Count() != profile.SkillChecks.Count ||
            profile.SkillChecks.Any(item => item.Id == Guid.Empty ||
                item.SchemaVersion != DialogueSkillCheck.CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(item.Provenance) || !nodeIds.Contains(item.NodeId) ||
                string.IsNullOrWhiteSpace(item.SkillId) || double.IsNaN(item.Difficulty) ||
                double.IsInfinity(item.Difficulty) || item.Difficulty < 0 || !Enum.IsDefined(item.Visibility) ||
                !nodeIds.Contains(item.SuccessNodeId) || !nodeIds.Contains(item.FailureNodeId)))
            diagnostics.Add(new DomainDiagnostic(SkillCheckCode,
                "Skill checks require unique source nodes, resolved result nodes, supported visibility and finite difficulty.",
                $"{root}/skillChecks"));

        return diagnostics;
    }

    private static void AddActionDiagnostics(
        IReadOnlyList<DialogueAction> actions,
        string path,
        ICollection<DomainDiagnostic> diagnostics)
    {
        if (actions.Select(item => item.Id).Distinct().Count() != actions.Count)
            diagnostics.Add(new DomainDiagnostic(LifecycleCode,
                "Lifecycle action identities must be unique.", path));
        for (var index = 0; index < actions.Count; index++)
            foreach (var diagnostic in DialogueActionTransaction.Validate(actions[index], $"{path}/{index}"))
                diagnostics.Add(new DomainDiagnostic(LifecycleCode, diagnostic.Message, diagnostic.Path));
    }
}
