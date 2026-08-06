namespace Forge.Domain.Authoring;

public sealed record EditorCommandBinding
{
    public string CommandId { get; }

    public string DisplayName { get; }

    public string Category { get; }

    public EditorKeyGesture Gesture { get; }

    public EditorCommandBinding(string commandId, string displayName, string category, EditorKeyGesture gesture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentNullException.ThrowIfNull(gesture);
        CommandId = commandId.Trim();
        DisplayName = displayName.Trim();
        Category = category.Trim();
        Gesture = gesture;
    }
}
