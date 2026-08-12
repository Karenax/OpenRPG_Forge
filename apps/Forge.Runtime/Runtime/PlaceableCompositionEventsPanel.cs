using Forge.RpgKit.Application.Placeables;
using Forge.RpgKit.Domain.Placeables;
using Godot;

namespace Forge.Runtime;

public partial class PlaceableCompositionEventsPanel : PanelContainer
{
    private readonly VBoxContainer _layout = new() { Name = "CompositionEventsLayout" };
    private readonly Label _title = new() { Name = "CompositionEventsTitle" };
    private readonly Label _damage = new() { Name = "DamageBindings" };
    private readonly ItemList _sockets = new()
    {
        Name = "SocketList",
        CustomMinimumSize = new(460, 90),
        FocusMode = FocusModeEnum.All,
    };
    private readonly ItemList _verbs = new()
    {
        Name = "VerbList",
        CustomMinimumSize = new(460, 150),
        FocusMode = FocusModeEnum.All,
    };
    private readonly Label _feedback = new()
    {
        Name = "CompositionFeedback",
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
    };
    private readonly HBoxContainer _actions = new() { Name = "CompositionActions" };

    public PlaceableCompositionEventsPanel()
    {
        Name = "PlaceableCompositionEventsPanel";
        CustomMinimumSize = new(500, 450);
        AddChild(_layout);
        _layout.AddChild(_title);
        _layout.AddChild(_damage);
        _layout.AddChild(new Label { Name = "SocketHeading", Text = "Sockets et assemblage" });
        _layout.AddChild(_sockets);
        _layout.AddChild(new Label { Name = "VerbHeading", Text = "Interactions disponibles" });
        _layout.AddChild(_verbs);
        _layout.AddChild(_actions);
        _layout.AddChild(_feedback);

        AddAction("DamageButton", "Appliquer 20 dégâts", "DamageApplied");
        AddAction("DestroyButton", "Détruire", "EntityDestroyed");
        AddAction("AttachButton", "Assembler", "AttachmentApplied");
        AddAction("UseVerbButton", "Exécuter le verbe", "InteractionRequested");
    }

    public void Configure(
        PlaceableCompositionEventsRuntime runtime,
        PlaceableBoundEvent<EntityEventPayload> damage,
        PlaceableBoundEvent<EntityEventPayload> destroyed,
        AttachmentResult attachment,
        IReadOnlyList<ContextualVerb> verbs)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(damage);
        ArgumentNullException.ThrowIfNull(destroyed);
        ArgumentNullException.ThrowIfNull(attachment);
        ArgumentNullException.ThrowIfNull(verbs);

        _title.Text = "Composition du placeable · dégâts, sockets et verbes";
        _damage.Text = $"{damage.Binding} → {destroyed.Binding} · " +
                       $"{damage.Payload.PreviousHealth:0.#} / {damage.Payload.CurrentHealth:0.#} PV";
        _sockets.Clear();
        foreach (var socket in runtime.Sockets)
            _sockets.AddItem($"{socket.Id} · {socket.Kind} · snap {socket.SnapEnabled} · " +
                             $"capacité {socket.MaximumAttachments}");
        foreach (var item in runtime.Attachments)
            _sockets.AddItem($"↳ {item.PrefabId ?? item.ChildDefinitionId.ToString("D")} · {item.SocketId}");

        _verbs.Clear();
        foreach (var verb in verbs)
            _verbs.AddItem($"{verb.Label} · {verb.EventName} · priorité {verb.BasePriority}");
        _feedback.Text = $"Assemblage prêt · {attachment.Patch} · inverse {attachment.InversePatch}";
        SetMeta("damage_binding", damage.Binding);
        SetMeta("destroyed_binding", destroyed.Binding);
        SetMeta("attachment_patch", attachment.Patch);
        SetMeta("attachment_inverse_patch", attachment.InversePatch);
        SetMeta("verb_count", verbs.Count);
        SetMeta("screen_reader_summary",
            "Dégâts et destruction, sockets et assemblage, interactions contextuelles ordonnées");
    }

    private void AddAction(string name, string text, string eventName)
    {
        var button = new Button
        {
            Name = name,
            Text = text,
            TooltipText = text,
            FocusMode = FocusModeEnum.All,
        };
        button.Pressed += () => _feedback.Text = $"Commande exécutée · {eventName}";
        _actions.AddChild(button);
    }
}
