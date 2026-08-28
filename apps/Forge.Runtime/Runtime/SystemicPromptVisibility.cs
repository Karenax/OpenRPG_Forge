using Godot;

namespace Forge.Runtime;

/// <summary>
/// Keeps systemic affordance labels local to the player's current room instead of
/// rendering every prompt through the expanded dungeon.
/// </summary>
public partial class SystemicPromptVisibility : Node
{
    private const float VisibleDistance = 7.5f;
    private GameRoot? _root;
    private Node3D? _systemicRoot;

    public override async void _Ready()
    {
        SetProcess(false);
        _root = GetParent() as GameRoot;
        if (_root is null) return;

        for (var frame = 0; frame < 600 && !_root.IsSystemicRuntimeInitialized; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        if (!_root.IsSystemicGameplayEnabled) return;
        _systemicRoot = _root.GetNodeOrNull<Node3D>("SystemicEnvironmentGameplay");
        if (_systemicRoot is null) return;

        // Give the systemic director time to build its manifest-driven children.
        for (var frame = 0; frame < 120 && _systemicRoot.GetChildCount() == 0; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        SetProcess(true);
        UpdatePromptVisibility();
    }

    public override void _Process(double delta) => UpdatePromptVisibility();

    private void UpdatePromptVisibility()
    {
        if (_root?.SystemicPlayer is not { } player || _systemicRoot is null) return;

        foreach (var child in _systemicRoot.GetChildren())
        {
            if (child is not Node3D interaction || !interaction.Name.ToString().StartsWith("Systemic_")) continue;
            var label = interaction.GetNodeOrNull<Label3D>("Prompt");
            if (label is null) continue;
            label.Visible = interaction.GlobalPosition.DistanceTo(player.GlobalPosition) <= VisibleDistance;
        }
    }
}
