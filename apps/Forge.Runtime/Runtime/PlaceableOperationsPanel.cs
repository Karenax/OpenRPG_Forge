using Forge.RpgKit.Application.Placeables;
using Godot;

namespace Forge.Runtime;

public partial class PlaceableOperationsPanel : PanelContainer
{
    private readonly VBoxContainer _layout = new() { Name = "OperationsLayout" };
    private readonly Label _title = new() { Name = "OperationsTitle" };
    private readonly ItemList _inventory = new() { Name = "ContainerInventory", CustomMinimumSize = new(360, 110) };
    private readonly Label _state = new() { Name = "VisualState" };
    private readonly ProgressBar _health = new() { Name = "Health", ShowPercentage = true };
    private readonly Label _feedback = new() { Name = "Feedback", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly HBoxContainer _actions = new() { Name = "OperationsActions" };

    public PlaceableOperationsPanel()
    {
        Name = "PlaceableOperationsPanel";
        CustomMinimumSize = new(420, 360);
        AddChild(_layout);
        _layout.AddChild(_title);
        _layout.AddChild(_inventory);
        _layout.AddChild(_state);
        _layout.AddChild(_health);
        _layout.AddChild(_feedback);
        _layout.AddChild(_actions);
        foreach (var (name, label) in new[]
                 {
                     ("OpenButton", "Ouvrir"),
                     ("CloseButton", "Fermer"),
                     ("DamageButton", "Endommager"),
                     ("RepairButton", "Réparer"),
                     ("UnlockButton", "Déverrouiller"),
                 })
        {
            var button = new Button { Name = name, Text = label, FocusMode = FocusModeEnum.All };
            _actions.AddChild(button);
        }
    }

    public void Configure(PlaceableOperationsRuntime runtime)
    {
        var container = runtime.Profile.Container;
        var damageable = runtime.Profile.Damageable;
        _title.Text = $"{runtime.Definition.Texts.FirstOrDefault()?.Name ?? runtime.Definition.Slug} · " +
                      $"{container?.CapacitySlots ?? 0} slots";
        _inventory.Clear();
        _inventory.AddItem(container is null ? "Aucun conteneur" : "Inventaire canonique · prêt");
        _inventory.AddItem(container is null ? "" : $"Loot {container.LootPolicyId} · UI {container.UiProfileId}");
        _inventory.FocusMode = FocusModeEnum.All;
        _state.Text = $"État visuel · {runtime.Profile.StateMachine.InitialState} · " +
                      $"{runtime.Profile.StateMachine.States.Count} états";
        _health.MinValue = 0;
        _health.MaxValue = damageable?.MaximumHealth ?? 1;
        _health.Value = damageable?.CurrentHealth ?? 0;
        _feedback.Text = runtime.Protection is null
            ? "Protection absente"
            : $"Protection · feedback et override MJ audité · {runtime.ProtectionAuditBinding}";
        SetMeta("container_persistent", container?.Persistent ?? false);
        SetMeta("container_replicated", container?.Replicated ?? false);
        SetMeta("inventory_reference", runtime.Profile.InventoryReferences.FirstOrDefault() ?? string.Empty);
        SetMeta("lock_state", runtime.Lock?.State.ToString() ?? "none");
        SetMeta("screen_reader_summary", "Conteneur, état visuel, santé, protection et verrou");
    }
}
