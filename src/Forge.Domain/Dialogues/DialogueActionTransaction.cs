using Forge.Domain.Validation;

namespace Forge.Domain.Dialogues;

public sealed record DialogueSandboxState(IReadOnlyDictionary<string, string> Values)
{
    public static DialogueSandboxState Empty { get; } = new(new Dictionary<string, string>());
}

public sealed record DialogueActionTransactionResult(
    bool Succeeded,
    DialogueSandboxState State,
    IReadOnlyList<DialogueAction> AppliedActions,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class DialogueActionTransaction
{
    public const string InvalidActionCode = "dialogue.action.invalid";

    public static DialogueActionTransactionResult Apply(
        DialogueSandboxState state,
        IReadOnlyList<DialogueAction> actions,
        string path = "dialogue/actions")
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(actions);
        var diagnostics = actions.SelectMany((action, index) => Validate(action, $"{path}/{index}")).ToArray();
        var requiredFailure = diagnostics.Any(diagnostic =>
        {
            var indexText = diagnostic.Path.Split('/').Last();
            return int.TryParse(indexText, out var index) && index >= 0 && index < actions.Count && actions[index].Required;
        });
        if (requiredFailure)
            return new(false, state, [], diagnostics);

        var values = state.Values.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        var applied = new List<DialogueAction>();
        foreach (var action in actions.Where((_, index) => diagnostics.All(item => item.Path != $"{path}/{index}")))
        {
            values[StateKey(action)] = action.Value;
            applied.Add(action);
        }

        return new(true, new DialogueSandboxState(values), applied, diagnostics);
    }

    public static IReadOnlyList<DomainDiagnostic> Validate(DialogueAction action, string path)
    {
        ArgumentNullException.ThrowIfNull(action);
        var valid = action.Id != Guid.Empty && action.SchemaVersion == DialogueAction.CurrentSchemaVersion &&
                    !string.IsNullOrWhiteSpace(action.Provenance) && Enum.IsDefined(action.Kind) &&
                    !string.IsNullOrWhiteSpace(action.Key) && !string.IsNullOrWhiteSpace(action.Value);
        return valid
            ? []
            : [new DomainDiagnostic(InvalidActionCode,
                $"Dialogue action '{action.Id:D}' requires a supported type, schema, provenance, key and value.", path)];
    }

    public static string StateKey(DialogueAction action) => action.Kind switch
    {
        DialogueActionKind.SetVariable => $"variable:{action.Key}",
        DialogueActionKind.StartQuest or DialogueActionKind.AdvanceQuest or DialogueActionKind.CompleteQuest =>
            $"quest:{action.Key}",
        DialogueActionKind.GiveItem or DialogueActionKind.RemoveItem => $"item:{action.Key}",
        DialogueActionKind.UnlockEntity => $"entity:{action.Key}",
        DialogueActionKind.SetRelation => $"relation:{action.Key}",
        DialogueActionKind.PlayAnimation => $"animation:{action.Key}",
        DialogueActionKind.PlayAudio => $"audio:{action.Key}",
        DialogueActionKind.StartSequence => $"sequence:{action.Key}",
        DialogueActionKind.RunScript => $"script:{action.Key}",
        _ => $"unsupported:{action.Key}",
    };
}
