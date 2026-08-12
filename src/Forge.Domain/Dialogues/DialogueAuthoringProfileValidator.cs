using Forge.Domain.Validation;

namespace Forge.Domain.Dialogues;

public static class DialogueAuthoringProfileValidator
{
    public const string RequiredMissingCode = "dialogue.authoring.required_missing";
    public const string IdentityCode = "dialogue.authoring.identity_invalid";
    public const string ParticipantCode = "dialogue.authoring.participant_invalid";
    public const string NodeCode = "dialogue.authoring.node_invalid";
    public const string TokenCode = "dialogue.authoring.token_invalid";
    public const string EvaluationCode = "dialogue.authoring.evaluation_invalid";
    public const string WizardCode = "dialogue.authoring.wizard_invalid";
    public const string NotesCode = "dialogue.authoring.notes_invalid";

    public static IReadOnlyList<DomainDiagnostic> Validate(DialogueDefinition dialogue)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        var root = $"dialogues/{dialogue.Id}/authoring";
        if (dialogue.Authoring is null)
        {
            return dialogue.Metadata.TryGetValue("authoring.required", out var required) &&
                   string.Equals(required, "true", StringComparison.OrdinalIgnoreCase)
                ? [new DomainDiagnostic(RequiredMissingCode,
                    "Required dialogue authoring profile is missing.", root)]
                : [];
        }

        var profile = dialogue.Authoring;
        if (!profile.Enabled && !profile.RequiredAtRuntime) return [];
        var diagnostics = new List<DomainDiagnostic>();
        if (profile.Id == Guid.Empty || profile.SchemaVersion != DialogueAuthoringProfile.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(profile.Provenance))
            diagnostics.Add(new DomainDiagnostic(IdentityCode,
                "Dialogue authoring identity, schema and provenance are required.", root));

        var roles = profile.Participants.Select(item => item.Role).ToArray();
        if (profile.Participants.Count == 0 || roles.Distinct(StringComparer.Ordinal).Count() != roles.Length ||
            profile.Participants.Any(item => item.Id == Guid.Empty ||
                item.SchemaVersion != DialogueParticipantBinding.CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(item.Provenance) || string.IsNullOrWhiteSpace(item.Role) ||
                !Enum.IsDefined(item.Kind) || string.IsNullOrWhiteSpace(item.Value) ||
                (item.FallbackRole is not null && !roles.Contains(item.FallbackRole, StringComparer.Ordinal))))
            diagnostics.Add(new DomainDiagnostic(ParticipantCode,
                "Participant bindings require unique roles, supported kinds, values and resolved fallbacks.",
                $"{root}/participants"));

        var canonicalNodes = dialogue.Nodes.ToDictionary(item => item.Id);
        var authoringNodeIds = profile.Nodes.Select(item => item.NodeId).ToArray();
        if (profile.Nodes.Count != dialogue.Nodes.Count || authoringNodeIds.Distinct().Count() != authoringNodeIds.Length ||
            profile.Nodes.Any(item => item.Id == Guid.Empty ||
                item.SchemaVersion != DialogueNodeAuthoring.CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(item.Provenance) || !canonicalNodes.ContainsKey(item.NodeId) ||
                string.IsNullOrWhiteSpace(item.SpeakerRole) || !roles.Contains(item.SpeakerRole, StringComparer.Ordinal) ||
                !Enum.IsDefined(item.EvaluationMode)))
            diagnostics.Add(new DomainDiagnostic(NodeCode,
                "Every canonical node requires one valid authoring record and participant role.", $"{root}/nodes"));

        foreach (var item in profile.Nodes.Where(item => canonicalNodes.ContainsKey(item.NodeId)))
        {
            var node = canonicalNodes[item.NodeId];
            var path = $"{root}/nodes/{item.NodeId}";
            if (item.Tokens.Select(token => token.Name).Distinct(StringComparer.Ordinal).Count() != item.Tokens.Count ||
                item.Tokens.Any(token => token.Id == Guid.Empty ||
                    token.SchemaVersion != DialogueTextToken.CurrentSchemaVersion ||
                    string.IsNullOrWhiteSpace(token.Provenance) || string.IsNullOrWhiteSpace(token.Name) ||
                    !Enum.IsDefined(token.Type) ||
                    !node.Text.Fallback.Contains("{{" + token.Name + "}}", StringComparison.Ordinal) ||
                    (token.DefaultValue is not null && !CanConvert(token.Type, token.DefaultValue))))
                diagnostics.Add(new DomainDiagnostic(TokenCode,
                    "Tokens require unique names, typed defaults and a matching template marker.", $"{path}/tokens"));

            var choices = node.Choices.Select(choice => choice.Id).ToHashSet();
            if (item.ChoiceEvaluations.Select(value => value.ChoiceId).Distinct().Count() !=
                item.ChoiceEvaluations.Count || item.ChoiceEvaluations.Any(value => value.Id == Guid.Empty ||
                    value.SchemaVersion != DialogueChoiceEvaluation.CurrentSchemaVersion ||
                    string.IsNullOrWhiteSpace(value.Provenance) || !choices.Contains(value.ChoiceId) ||
                    double.IsNaN(value.Weight) || double.IsInfinity(value.Weight) || value.Weight < 0) ||
                (item.EvaluationMode == DialogueChoiceEvaluationMode.Weighted &&
                 item.ChoiceEvaluations.Sum(value => value.Weight) <= 0))
                diagnostics.Add(new DomainDiagnostic(EvaluationCode,
                    "Evaluation entries require resolved choices, finite weights and positive weighted totals.",
                    $"{path}/evaluation"));

            var conditionIds = node.Choices.SelectMany(choice => choice.Conditions).Select(value => value.Id);
            var actionIds = node.OnEnterActions.Concat(node.Choices.SelectMany(choice => choice.Actions))
                .Select(value => value.Id);
            var targets = conditionIds.Concat(actionIds).ToHashSet();
            if (item.WizardEntries.Select(value => value.TargetId).Distinct().Count() != item.WizardEntries.Count ||
                item.WizardEntries.Any(value => value.Id == Guid.Empty ||
                    value.SchemaVersion != DialogueWizardEntry.CurrentSchemaVersion ||
                    string.IsNullOrWhiteSpace(value.Provenance) || !Enum.IsDefined(value.Kind) ||
                    !targets.Contains(value.TargetId) ||
                    !value.SourceDescription.StartsWith("builder:", StringComparison.Ordinal)))
                diagnostics.Add(new DomainDiagnostic(WizardCode,
                    "Wizard entries require a unique typed target and a visible builder source.",
                    $"{path}/wizard"));

            var notes = item.Notes;
            if (notes.Id == Guid.Empty || notes.SchemaVersion != DialogueNodeEditorialNotes.CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(notes.Provenance) || string.IsNullOrWhiteSpace(notes.Intent) ||
                string.IsNullOrWhiteSpace(notes.Emotion) || string.IsNullOrWhiteSpace(notes.Source) ||
                !Enum.IsDefined(notes.Status) || string.IsNullOrWhiteSpace(notes.AuthorMention) ||
                notes.Todos.Any(string.IsNullOrWhiteSpace))
                diagnostics.Add(new DomainDiagnostic(NotesCode,
                    "Node notes require intent, emotion, source, editorial status, author and valid TODOs.",
                    $"{path}/notes"));
        }

        return diagnostics;
    }

    private static bool CanConvert(DialogueTextTokenType type, string value) => type switch
    {
        DialogueTextTokenType.Integer => long.TryParse(value, out _),
        DialogueTextTokenType.Decimal => double.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out _),
        DialogueTextTokenType.Boolean => bool.TryParse(value, out _),
        _ => true,
    };
}
