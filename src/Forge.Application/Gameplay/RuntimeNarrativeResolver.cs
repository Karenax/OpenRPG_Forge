using Forge.Domain.Blueprints;
using Forge.Domain.Projects;

namespace Forge.Application.Gameplay;

public static class RuntimeNarrativeResolver
{
    public static RuntimeDialogueResolution? ResolveDialogue(
        ProjectDefinition project,
        BlueprintId participantBlueprintId,
        string entryPointKey = "default")
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(participantBlueprintId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryPointKey);

        var matches = project.Dialogues
            .Where(dialogue => dialogue.Participants.Any(participant =>
                participant.BlueprintId == participantBlueprintId))
            .SelectMany(dialogue => dialogue.EntryPoints
                .Where(entryPoint => string.Equals(entryPoint.Key, entryPointKey, StringComparison.Ordinal))
                .Select(entryPoint => new RuntimeDialogueResolution(dialogue, entryPoint.NodeId)))
            .ToArray();

        return matches.Length switch
        {
            0 => null,
            1 => matches[0],
            _ => throw new InvalidOperationException(
                $"Participant '{participantBlueprintId}' and entry point '{entryPointKey}' resolve to " +
                $"multiple dialogues: {string.Join(", ", matches.Select(match => match.Dialogue.Id))}."),
        };
    }
}
