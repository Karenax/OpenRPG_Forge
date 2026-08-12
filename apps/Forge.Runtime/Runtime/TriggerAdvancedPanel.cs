using Forge.RpgKit.Application.Triggers;
using Forge.RpgKit.Domain.Placeables;
using Forge.RpgKit.Domain.Triggers;
using Godot;

namespace Forge.Runtime;

public partial class TriggerAdvancedPanel : PanelContainer
{
    private readonly VBoxContainer _layout = new() { Name = "TriggerAdvancedLayout" };
    private readonly Label _title = new() { Name = "TriggerAdvancedTitle" };
    private readonly Label _trap = new() { Name = "TrapStatus" };
    private readonly Label _filter = new() { Name = "TargetFilter" };
    private readonly ItemList _volumes = new()
    {
        Name = "GameplayVolumeList",
        CustomMinimumSize = new(500, 210),
        FocusMode = FocusModeEnum.All,
    };
    private readonly ItemList _occupancy = new()
    {
        Name = "OccupancyList",
        CustomMinimumSize = new(500, 130),
        FocusMode = FocusModeEnum.All,
    };
    private readonly HBoxContainer _actions = new() { Name = "TriggerAdvancedActions" };
    private readonly Label _feedback = new()
    {
        Name = "TriggerAdvancedFeedback",
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
    };

    public TriggerAdvancedPanel()
    {
        Name = "TriggerAdvancedPanel";
        CustomMinimumSize = new(540, 540);
        AddChild(_layout);
        _layout.AddChild(_title);
        _layout.AddChild(_trap);
        _layout.AddChild(_filter);
        _layout.AddChild(new Label { Name = "GameplayVolumeHeading", Text = "Volumes gameplay" });
        _layout.AddChild(_volumes);
        _layout.AddChild(new Label { Name = "OccupancyHeading", Text = "Occupation et trace" });
        _layout.AddChild(_occupancy);
        _layout.AddChild(_actions);
        _layout.AddChild(_feedback);

        AddAction("DetectTrapButton", "Détecter", "TrapDetected");
        AddAction("DisarmTrapButton", "Désarmer", "TrapDisarmed");
        AddAction("ResetTrapButton", "Réarmer", "TrapReset");
        AddAction("ApplyVolumeButton", "Appliquer Damage", "DamageVolumeEntered");
    }

    public void Configure(TriggerAdvancedRuntime runtime, TrapResult detected, TrapResult disarmed,
        GameplayVolumeEffect effect, TriggerDebugOverlay overlay)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(detected);
        ArgumentNullException.ThrowIfNull(disarmed);
        ArgumentNullException.ThrowIfNull(effect);
        ArgumentNullException.ThrowIfNull(overlay);

        _title.Text = "Trigger avancé · piège, requête, volumes et debug";
        _trap.Text = $"{runtime.Trap.Trap.PayloadId} · détection {detected.Succeeded} · " +
                     $"désarmé {!disarmed.Trap.Armed} · overlay MJ {runtime.Trap.GameMasterVisualization}";
        _filter.Text = $"Joueurs {runtime.TargetFilter.PlayersOnly} · " +
                       $"tags {string.Join(',', runtime.TargetFilter.RequiredTags)} · " +
                       $"composants {string.Join(',', runtime.TargetFilter.RequiredComponents)}";
        _volumes.Clear();
        foreach (var volume in runtime.GameplayVolumes)
            _volumes.AddItem($"{volume.Kind} · {volume.EnterEvent} → {volume.ExitEvent} · " +
                             string.Join(", ", volume.NumericParameters.Select(item => $"{item.Key}={item.Value:0.##}")));
        _occupancy.Clear();
        _occupancy.AddItem($"Occupants · {string.Join(',', overlay.Occupants)}");
        _occupancy.AddItem($"Derniers tirs · {overlay.LastFires.Count} · cooldowns {overlay.CooldownsRemaining.Count}");
        _occupancy.AddItem($"Filtres · {overlay.FilterSummary}");
        foreach (var trace in overlay.EventTrace)
            _occupancy.AddItem($"{trace.EventName} · {trace.EntityId} · {trace.Result}");
        _feedback.Text = $"{effect.EventName} · {effect.EntityId} · " +
                         string.Join(", ", effect.Changes.Select(item => $"{item.Key}={item.Value}"));
        SetMeta("volume_count", runtime.GameplayVolumes.Count);
        SetMeta("occupant_count", overlay.Occupants.Count);
        SetMeta("trace_count", overlay.EventTrace.Count);
        SetMeta("screen_reader_summary",
            "Piège détectable et désarmable, filtre de cible, dix volumes gameplay, occupants et trace");
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
