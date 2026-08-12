using Forge.Domain.Validation;

namespace Forge.Domain.Dialogues;

public static class DialogueValidator
{
    public const string MissingEntryNodeCode = "dialogue.reference.entry_node_missing";
    public const string MissingTargetNodeCode = "dialogue.reference.target_node_missing";
    public const string UnreachableNodeCode = "dialogue.graph.node_unreachable";
    public const string UnsupportedSchemaCode = "dialogue.schema.unsupported";
    public const string ProvenanceMissingCode = "dialogue.provenance.missing";
    public const string MetadataInvalidCode = "dialogue.metadata.invalid";
    public const string ParticipantInvalidCode = "dialogue.participant.invalid";
    public const string EntryPointInvalidCode = "dialogue.entry.invalid";
    public const string NodeRoleInvalidCode = "dialogue.node.role_invalid";
    public const string EdgeInvalidCode = "dialogue.edge.invalid";
    public const string AccidentalCycleCode = "dialogue.graph.accidental_cycle";
    public const string MissingTerminalNodeCode = "dialogue.graph.terminal_missing";
    public const string LocalizationInvalidCode = "dialogue.localization.invalid";
    public const string ConditionInvalidCode = "dialogue.condition.invalid";
    public const string ContradictoryConditionCode = "dialogue.condition.contradictory";
    public const string ActionInvalidCode = DialogueActionTransaction.InvalidActionCode;
    public const string DuplicateSemanticIdCode = "dialogue.identity.duplicate";

    public static IReadOnlyList<DomainDiagnostic> Validate(DialogueDefinition dialogue)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        var diagnostics = new List<DomainDiagnostic>();
        var nodes = dialogue.Nodes.ToDictionary(node => node.Id);
        var root = $"dialogues/{dialogue.Id}";
        if (dialogue.SchemaVersion != DialogueDefinition.CurrentSchemaVersion)
        {
            diagnostics.Add(new DomainDiagnostic(
                UnsupportedSchemaCode,
                $"Dialogue schema {dialogue.SchemaVersion} is unsupported; expected {DialogueDefinition.CurrentSchemaVersion}.",
                $"{root}/schemaVersion"));
        }

        if (string.IsNullOrWhiteSpace(dialogue.Provenance))
        {
            diagnostics.Add(new DomainDiagnostic(
                ProvenanceMissingCode,
                "Dialogue provenance is required.",
                $"{root}/provenance"));
        }

        if (dialogue.Metadata.Count == 0 || dialogue.Metadata.Any(item =>
                string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Value)))
        {
            diagnostics.Add(new DomainDiagnostic(
                MetadataInvalidCode,
                "Dialogue metadata requires non-empty keys and values.",
                $"{root}/metadata"));
        }

        var participantIds = dialogue.Participants.Select(item => item.Id).ToArray();
        if (dialogue.Participants.Count == 0 || participantIds.Contains(Guid.Empty) ||
            participantIds.Distinct().Count() != participantIds.Length ||
            dialogue.Participants.Select(item => item.Role).Distinct(StringComparer.Ordinal).Count() !=
            dialogue.Participants.Count ||
            dialogue.Participants.Any(item =>
                item.SchemaVersion != DialogueParticipant.CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(item.Role) || string.IsNullOrWhiteSpace(item.Provenance)))
        {
            diagnostics.Add(new DomainDiagnostic(
                ParticipantInvalidCode,
                "Dialogue participants require unique identities and roles plus supported schema and provenance.",
                $"{root}/participants"));
        }

        var entryIds = dialogue.EntryPoints.Select(item => item.Id).ToArray();
        if (dialogue.EntryPoints.Count == 0 || entryIds.Contains(Guid.Empty) ||
            entryIds.Distinct().Count() != entryIds.Length ||
            dialogue.EntryPoints.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count() !=
            dialogue.EntryPoints.Count ||
            dialogue.EntryPoints.Select(item => item.Order).Distinct().Count() != dialogue.EntryPoints.Count ||
            dialogue.EntryPoints.Any(item =>
                item.SchemaVersion != DialogueEntryPoint.CurrentSchemaVersion || item.Order < 0 ||
                string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Provenance) ||
                (item.NodeId != dialogue.EntryNodeId && !nodes.ContainsKey(item.NodeId))) ||
            dialogue.EntryPoints.All(item => item.NodeId != dialogue.EntryNodeId))
        {
            diagnostics.Add(new DomainDiagnostic(
                EntryPointInvalidCode,
                "Dialogue entries require unique identities, keys and order plus a resolved canonical entry node.",
                $"{root}/entryPoints"));
        }

        if (!nodes.ContainsKey(dialogue.EntryNodeId))
        {
            diagnostics.Add(new DomainDiagnostic(
                MissingEntryNodeCode,
                $"Dialogue '{dialogue.Id}' references missing entry node '{dialogue.EntryNodeId}'.",
                $"dialogues/{dialogue.Id}/entryNodeId"));
            return diagnostics;
        }

        foreach (var node in dialogue.Nodes)
        {
            AddLocalizationDiagnostics(node.Text, $"{root}/nodes/{node.Id}/text", diagnostics);
            if (!Enum.IsDefined(node.Role) || node.ParticipantId == Guid.Empty ||
                !participantIds.Contains(node.ParticipantId) ||
                (node.Role == DialogueNodeRole.Subgraph && string.IsNullOrWhiteSpace(node.SubgraphKey)) ||
                (node.Role == DialogueNodeRole.End && node.Choices.Count > 0))
            {
                diagnostics.Add(new DomainDiagnostic(
                    NodeRoleInvalidCode,
                    $"Dialogue node '{node.Id}' has an invalid role, participant or subgraph contract.",
                    $"{root}/nodes/{node.Id}/role"));
            }

            foreach (var choice in node.Choices.Where(choice =>
                         choice.TargetNodeId is not null && !nodes.ContainsKey(choice.TargetNodeId)))
            {
                diagnostics.Add(new DomainDiagnostic(
                    MissingTargetNodeCode,
                    $"Choice '{choice.Id}' references missing target node '{choice.TargetNodeId}'.",
                    $"dialogues/{dialogue.Id}/nodes/{node.Id}/choices/{choice.Id}/targetNodeId"));
            }

            foreach (var choice in node.Choices.Where(choice =>
                         !Enum.IsDefined(choice.EdgeKind) || choice.Priority < 0 ||
                         !Enum.IsDefined(choice.ConditionCombination) ||
                         (choice.EdgeKind == DialogueEdgeKind.Reference && choice.TargetNodeId is null)))
            {
                diagnostics.Add(new DomainDiagnostic(
                    EdgeInvalidCode,
                    $"Choice '{choice.Id}' requires a valid edge kind, target and non-negative priority.",
                    $"{root}/nodes/{node.Id}/choices/{choice.Id}"));
            }

            for (var choiceIndex = 0; choiceIndex < node.Choices.Count; choiceIndex++)
            {
                var choice = node.Choices[choiceIndex];
                var choicePath = $"{root}/nodes/{node.Id}/choices/{choice.Id}";
                AddLocalizationDiagnostics(choice.Text, $"{choicePath}/text", diagnostics);
                for (var conditionIndex = 0; conditionIndex < choice.Conditions.Count; conditionIndex++)
                {
                    var condition = choice.Conditions[conditionIndex];
                    if (condition.Id == Guid.Empty ||
                        condition.SchemaVersion != DialogueCondition.CurrentSchemaVersion ||
                        string.IsNullOrWhiteSpace(condition.Provenance) ||
                        !Enum.IsDefined(condition.Kind) ||
                        string.IsNullOrWhiteSpace(condition.Key) ||
                        string.IsNullOrWhiteSpace(condition.ExpectedValue) ||
                        string.IsNullOrWhiteSpace(condition.EventBinding) ||
                        condition.ForgeScriptApiVersion != 1)
                    {
                        diagnostics.Add(new DomainDiagnostic(
                            ConditionInvalidCode,
                            $"Dialogue condition '{condition.Id:D}' requires a supported type, schema, binding, API, key and value.",
                            $"{choicePath}/conditions/{conditionIndex}"));
                    }
                }

                if (choice.ConditionCombination == DialogueConditionCombination.All &&
                    HasContradictoryConditions(choice.Conditions))
                {
                    diagnostics.Add(new DomainDiagnostic(
                        ContradictoryConditionCode,
                        $"Choice '{choice.Id}' contains conditions that cannot all be true.",
                        $"{choicePath}/conditions"));
                }

                for (var actionIndex = 0; actionIndex < choice.Actions.Count; actionIndex++)
                {
                    diagnostics.AddRange(DialogueActionTransaction.Validate(
                        choice.Actions[actionIndex], $"{choicePath}/actions/{actionIndex}"));
                }
            }

            for (var actionIndex = 0; actionIndex < node.OnEnterActions.Count; actionIndex++)
            {
                diagnostics.AddRange(DialogueActionTransaction.Validate(
                    node.OnEnterActions[actionIndex], $"{root}/nodes/{node.Id}/onEnterActions/{actionIndex}"));
            }
        }

        AddDuplicateIdentityDiagnostics(dialogue, root, diagnostics);
        diagnostics.AddRange(DialogueAuthoringProfileValidator.Validate(dialogue));
        diagnostics.AddRange(DialogueExperienceProfileValidator.Validate(dialogue));
        diagnostics.AddRange(DialogueProductionProfileValidator.Validate(dialogue));

        var expectedEdges = dialogue.Nodes.SelectMany(node => node.Choices
                .Select((choice, index) => (choice, index))
                .Where(item => item.choice.TargetNodeId is not null)
                .Select(item => new DialogueGraphEdge(item.choice.Id, node.Id, item.choice.TargetNodeId!,
                    item.choice.EdgeKind, item.index, item.choice.Priority)))
            .OrderBy(item => item.SourceNodeId.Value)
            .ThenBy(item => item.Order)
            .ToArray();
        if (!dialogue.Edges.SequenceEqual(expectedEdges))
        {
            diagnostics.Add(new DomainDiagnostic(
                EdgeInvalidCode,
                "Dialogue edges must exactly mirror ordered choices with resolved targets.",
                $"{root}/edges"));
        }

        AddAccidentalCycleDiagnostics(dialogue, nodes, diagnostics);

        var reachable = new HashSet<DialogueNodeId>();
        var pending = new Queue<DialogueNodeId>();
        pending.Enqueue(dialogue.EntryNodeId);
        while (pending.TryDequeue(out var nodeId))
        {
            if (!reachable.Add(nodeId)) continue;
            foreach (var target in nodes[nodeId].Choices
                         .Select(choice => choice.TargetNodeId)
                         .Where(target => target is not null && nodes.ContainsKey(target)))
            {
                pending.Enqueue(target!);
            }
        }

        foreach (var node in dialogue.Nodes.Where(node => !reachable.Contains(node.Id)))
        {
            diagnostics.Add(new DomainDiagnostic(
                UnreachableNodeCode,
                $"Dialogue node '{node.Id}' cannot be reached from the entry node.",
                $"dialogues/{dialogue.Id}/nodes/{node.Id}"));
        }

        if (!diagnostics.Any(item => item.Code is MissingEntryNodeCode or MissingTargetNodeCode) &&
            !dialogue.Nodes.Any(node => reachable.Contains(node.Id) &&
                (node.Role == DialogueNodeRole.End || node.Choices.Count == 0 ||
                 node.Choices.Any(choice => choice.TargetNodeId is null))))
        {
            diagnostics.Add(new DomainDiagnostic(
                MissingTerminalNodeCode,
                "Dialogue graph requires at least one reachable ending.",
                $"{root}/nodes"));
        }

        diagnostics.AddRange(DialoguePresentationValidator.Validate(dialogue));
        return diagnostics;
    }

    private static void AddLocalizationDiagnostics(
        LocalizedText text,
        string path,
        ICollection<DomainDiagnostic> diagnostics)
    {
        if (text.Id == Guid.Empty || text.SchemaVersion != LocalizedText.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(text.Provenance) || string.IsNullOrWhiteSpace(text.Key) ||
            string.IsNullOrWhiteSpace(text.Fallback) ||
            text.Variants.Any(item => item.Id == Guid.Empty ||
                item.SchemaVersion != LocalizedTextVariant.CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(item.Provenance) || string.IsNullOrWhiteSpace(item.Locale) ||
                string.IsNullOrWhiteSpace(item.Gender) || string.IsNullOrWhiteSpace(item.Text) ||
                !Enum.IsDefined(item.PluralCategory)) ||
            text.Variants.GroupBy(item => (item.Locale.ToLowerInvariant(), item.Gender, item.PluralCategory))
                .Any(group => group.Count() > 1))
        {
            diagnostics.Add(new DomainDiagnostic(
                LocalizationInvalidCode,
                $"Localized text '{text.Key}' requires a stable identity, supported schema, fallback and unique valid variants.",
                path));
        }
    }

    private static bool HasContradictoryConditions(IReadOnlyList<DialogueCondition> conditions)
    {
        foreach (var group in conditions.GroupBy(item => (item.Kind, item.Key),
                     EqualityComparer<(DialogueConditionKind Kind, string Key)>.Default))
        {
            var positive = group.Where(item => !item.Negated).Select(item => item.ExpectedValue)
                .Distinct(StringComparer.Ordinal).ToArray();
            if (positive.Length > 1) return true;
            if (group.Any(item => item.Negated) && group.Any(item => !item.Negated) &&
                group.Where(item => item.Negated).Select(item => item.ExpectedValue)
                    .Intersect(positive, StringComparer.Ordinal).Any()) return true;
        }

        return false;
    }

    private static void AddDuplicateIdentityDiagnostics(
        DialogueDefinition dialogue,
        string root,
        ICollection<DomainDiagnostic> diagnostics)
    {
        var identities = new List<(Guid Id, string Path)>();
        foreach (var node in dialogue.Nodes)
        {
            identities.Add((node.Text.Id, $"{root}/nodes/{node.Id}/text"));
            identities.AddRange(node.Text.Variants.Select(item =>
                (item.Id, $"{root}/nodes/{node.Id}/text/variants/{item.Id:D}")));
            identities.AddRange(node.OnEnterActions.Select(item =>
                (item.Id, $"{root}/nodes/{node.Id}/onEnterActions/{item.Id:D}")));
            foreach (var choice in node.Choices)
            {
                identities.Add((choice.Text.Id, $"{root}/nodes/{node.Id}/choices/{choice.Id}/text"));
                identities.AddRange(choice.Text.Variants.Select(item =>
                    (item.Id, $"{root}/nodes/{node.Id}/choices/{choice.Id}/text/variants/{item.Id:D}")));
                identities.AddRange(choice.Conditions.Select(item =>
                    (item.Id, $"{root}/nodes/{node.Id}/choices/{choice.Id}/conditions/{item.Id:D}")));
                identities.AddRange(choice.Actions.Select(item =>
                    (item.Id, $"{root}/nodes/{node.Id}/choices/{choice.Id}/actions/{item.Id:D}")));
            }
        }

        foreach (var duplicate in identities.Where(item => item.Id != Guid.Empty).GroupBy(item => item.Id)
                     .Where(group => group.Count() > 1))
        {
            diagnostics.Add(new DomainDiagnostic(
                DuplicateSemanticIdCode,
                $"Dialogue semantic UUID '{duplicate.Key:D}' is used more than once.",
                duplicate.First().Path));
        }
    }

    private static void AddAccidentalCycleDiagnostics(
        DialogueDefinition dialogue,
        IReadOnlyDictionary<DialogueNodeId, DialogueNode> nodes,
        ICollection<DomainDiagnostic> diagnostics)
    {
        var states = new Dictionary<DialogueNodeId, int>();
        var reported = new HashSet<DialogueChoiceId>();
        foreach (var node in dialogue.Nodes)
        {
            Visit(node.Id);
        }

        void Visit(DialogueNodeId nodeId)
        {
            if (states.GetValueOrDefault(nodeId) == 2) return;
            states[nodeId] = 1;
            foreach (var choice in nodes[nodeId].Choices.Where(item =>
                         item.EdgeKind == DialogueEdgeKind.Flow && item.TargetNodeId is not null &&
                         nodes.ContainsKey(item.TargetNodeId)))
            {
                var target = choice.TargetNodeId!;
                if (states.GetValueOrDefault(target) == 1 && reported.Add(choice.Id))
                {
                    diagnostics.Add(new DomainDiagnostic(
                        AccidentalCycleCode,
                        $"Flow edge '{choice.Id}' creates a cycle; mark an intentional reuse as Reference.",
                        $"dialogues/{dialogue.Id}/nodes/{nodeId}/choices/{choice.Id}/edgeKind"));
                    continue;
                }

                if (states.GetValueOrDefault(target) == 0) Visit(target);
            }

            states[nodeId] = 2;
        }
    }
}
