using Forge.RpgKit.Application.Placeables;
using Forge.RpgKit.Domain.Placeables;
using Godot;

namespace Forge.Runtime;

public partial class PlaceableSecurityEventsPanel : PanelContainer
{
    private readonly VBoxContainer _layout = new() { Name = "SecurityEventsLayout" };
    private readonly Label _title = new() { Name = "SecurityEventsTitle" };
    private readonly Label _trapState = new() { Name = "TrapState" };
    private readonly ItemList _lootResults = new()
    {
        Name = "LootResults",
        CustomMinimumSize = new(420, 100),
        FocusMode = FocusModeEnum.All,
    };
    private readonly ItemList _eventLog = new()
    {
        Name = "EventLog",
        CustomMinimumSize = new(420, 130),
        FocusMode = FocusModeEnum.All,
    };
    private readonly Label _feedback = new()
    {
        Name = "SecurityFeedback",
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
    };
    private readonly HBoxContainer _trapActions = new() { Name = "TrapActions" };
    private readonly HBoxContainer _containerActions = new() { Name = "ContainerActions" };

    public PlaceableSecurityEventsPanel()
    {
        Name = "PlaceableSecurityEventsPanel";
        CustomMinimumSize = new(480, 480);
        AddChild(_layout);
        _layout.AddChild(_title);
        _layout.AddChild(_trapState);
        _layout.AddChild(_trapActions);
        _layout.AddChild(new Label { Name = "LootHeading", Text = "TrÃ©sor gÃ©nÃ©rÃ©" });
        _layout.AddChild(_lootResults);
        _layout.AddChild(_containerActions);
        _layout.AddChild(new Label { Name = "EventHeading", Text = "Ã‰vÃ©nements ForgeScript" });
        _layout.AddChild(_eventLog);
        _layout.AddChild(_feedback);

        AddAction(_trapActions, "DetectButton", "DÃ©tecter", "TrapDetected");
        AddAction(_trapActions, "DisarmButton", "DÃ©samorcer", "TrapDisarmed");
        AddAction(_trapActions, "TriggerButton", "DÃ©clencher", "TrapTriggered");
        AddAction(_trapActions, "ResetButton", "RÃ©armer", "TrapReset");
        AddAction(_containerActions, "SpawnLootButton", "GÃ©nÃ©rer le trÃ©sor", "LootSpawned");
        AddAction(_containerActions, "OpenButton", "Ouvrir", "ContainerOpened");
        AddAction(_containerActions, "CloseButton", "Fermer", "ContainerClosed");
        AddAction(_containerActions, "UnlockButton", "DÃ©verrouiller", "LockStateChanged");
        AddAction(_containerActions, "DisturbButton", "Modifier le contenu", "ContainerContentsChanged");
    }

    public void Configure(
        PlaceableSecurityEventsRuntime runtime,
        LootSpawnResult loot,
        IReadOnlyList<string> events)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(loot);
        ArgumentNullException.ThrowIfNull(events);

        _title.Text = $"SÃ©curitÃ© et Ã©vÃ©nements Â· {runtime.Trap.TriggerId}";
        _trapState.Text = runtime.Trap.Armed
            ? $"PiÃ¨ge armÃ© Â· dÃ©tection {runtime.Trap.DetectionDifficulty:0.#} Â· " +
              $"dÃ©samorÃ§age {runtime.Trap.DisarmDifficulty:0.#}"
            : "PiÃ¨ge dÃ©sarmÃ©";
        _lootResults.Clear();
        if (loot.Results.Count == 0)
            _lootResults.AddItem("Aucun objet gÃ©nÃ©rÃ©");
        else
            foreach (var item in loot.Results)
                _lootResults.AddItem($"{item.ItemDefinitionId:D} Â· quantitÃ© {item.Quantity} Â· qualitÃ© {item.Quality}");

        _eventLog.Clear();
        foreach (var item in events)
            _eventLog.AddItem(item);
        _feedback.Text = $"Butin {loot.EventName} Â· gÃ©nÃ©ration {loot.Spawner.Generation} Â· " +
                         $"API {runtime.ContainerEvents.ForgeScriptApiVersion}";
        SetMeta("trap_owner", runtime.Trap.OwnerId);
        SetMeta("trap_vfx", runtime.Trap.VisualEffectId);
        SetMeta("friendly_policy", runtime.Trap.FriendlyPolicy);
        SetMeta("loot_persisted", runtime.LootSpawner.PersistGeneratedState);
        SetMeta("loot_reused", loot.ReusedSavedState);
        SetMeta("container_opened_binding", runtime.ContainerEvents.OpenedBinding);
        SetMeta("container_closed_binding", runtime.ContainerEvents.ClosedBinding);
        SetMeta("lock_changing_binding", runtime.LockEvents.ChangingBinding);
        SetMeta("lock_changed_binding", runtime.LockEvents.ChangedBinding);
        SetMeta("contents_changed_binding", runtime.ContentsChanged.EventBinding);
        SetMeta("screen_reader_summary",
            "PiÃ¨ge, trÃ©sor, ouverture, fermeture, verrou et modifications du contenu");
    }

    private void AddAction(HBoxContainer parent, string name, string text, string eventName)
    {
        var button = new Button
        {
            Name = name,
            Text = text,
            TooltipText = text,
            FocusMode = FocusModeEnum.All,
        };
        button.Pressed += () =>
        {
            _eventLog.AddItem(eventName);
            _eventLog.Select(_eventLog.ItemCount - 1);
            _feedback.Text = $"Commande exÃ©cutÃ©e Â· {eventName}";
        };
        parent.AddChild(button);
    }
}
