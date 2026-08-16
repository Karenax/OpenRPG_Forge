using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioPlaceableContainerEditor : VBoxContainer
{
    private static readonly string[] StandardPermissions =
        ["container.open", "container.deposit", "container.withdraw"];

    private readonly Label _state;
    private readonly SpinBox _slots;
    private readonly SpinBox _mass;
    private readonly OptionButton _ownership;
    private readonly CheckBox _open;
    private readonly CheckBox _deposit;
    private readonly CheckBox _withdraw;
    private readonly CheckBox _persistent;
    private readonly CheckBox _replicated;
    private readonly LineEdit _inventory;
    private readonly LineEdit _loot;
    private readonly LineEdit _ui;
    private readonly CheckBox _runtimeRequired;
    private readonly List<string> _ownershipValues = ["party", "owner", "world", "gm"];
    private IReadOnlyList<string> _additionalPermissions = [];
    private bool _loading;

    internal event Action? Changed;

    internal event Action<bool>? RuntimeRequirementChanged;

    internal bool IsReady => Visible && _slots.Value >= 1 && !string.IsNullOrWhiteSpace(_inventory.Text) &&
                             !string.IsNullOrWhiteSpace(_loot.Text) && !string.IsNullOrWhiteSpace(_ui.Text);

    internal string HumanText => string.Join(' ',
        _state.Text,
        "Capacité Masse Propriétaire Ouvrir Déposer Retirer Persistance Réplication Inventaire Butin Présentation Runtime");

    internal StudioPlaceableContainerEditor()
    {
        Name = "StudioPlaceableContainerEditor";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 7);

        _state = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted, wrap: true);
        _state.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        AddChild(_state);

        var capacity = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        capacity.AddThemeConstantOverride("h_separation", 8);
        AddChild(capacity);
        _slots = AddSpinCell(capacity, "Emplacements", 1, 10_000, 1);
        _mass = AddSpinCell(capacity, "Masse maximale (kg)", 0, 1_000_000, .5);

        _ownership = new OptionButton { TooltipText = "Qui possède l’inventaire de ce décor ?" };
        StudioStyle.ApplyOptionButton(_ownership);
        AddChild(StudioStyle.Label("Propriétaire", 10, StudioStyle.Muted));
        foreach (var label in new[] { "Groupe", "Propriétaire de l’objet", "Monde partagé", "Maître de jeu" })
        {
            _ownership.AddItem(label);
        }
        AddChild(_ownership);

        AddChild(StudioStyle.Label("Droits accordés", 10, StudioStyle.Muted));
        var access = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        access.AddThemeConstantOverride("h_separation", 10);
        AddChild(access);
        _open = AddCheck(access, "Ouvrir");
        _deposit = AddCheck(access, "Déposer");
        _withdraw = AddCheck(access, "Retirer");

        var lifecycle = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        lifecycle.AddThemeConstantOverride("h_separation", 10);
        AddChild(lifecycle);
        _persistent = AddCheck(lifecycle, "Conserver dans les sauvegardes");
        _replicated = AddCheck(lifecycle, "Partager en multijoueur");

        AddChild(new HSeparator());
        AddChild(StudioStyle.Label("RÉFÉRENCES DU CONTENU", 10, StudioStyle.Accent));
        _inventory = AddLine(this, "Inventaire", "inventory:coffre-principal");
        _loot = AddLine(this, "Politique de butin", "loot:coffre-principal");
        _ui = AddLine(this, "Présentation", "ui:placeable-chest");
        _runtimeRequired = AddCheck(this, "Exiger ce profil au runtime");
        _runtimeRequired.TooltipText =
            "Si activé, un profil d’opérations absent produit un diagnostic au lieu d’un repli optionnel.";

        foreach (var spin in new[] { _slots, _mass })
        {
            spin.ValueChanged += _ => NotifyChanged();
        }
        _ownership.ItemSelected += _ => NotifyChanged();
        foreach (var check in new[] { _open, _deposit, _withdraw, _persistent, _replicated, _runtimeRequired })
        {
            check.Toggled += _ => NotifyChanged();
        }
        foreach (var line in new[] { _inventory, _loot, _ui })
        {
            line.TextChanged += _ => NotifyChanged();
        }
        _runtimeRequired.Toggled += value =>
        {
            if (!_loading)
            {
                RuntimeRequirementChanged?.Invoke(value);
            }
        };
    }

    internal void Load(StudioPlaceableContainerDraft draft, bool requiredAtRuntime)
    {
        _loading = true;
        _state.Text = draft.ExistingProfile
            ? "Profil canonique existant · les capacités voisines et les permissions supplémentaires seront conservées."
            : "État vide · le Studio prépare un conteneur sûr, sans écrire le module avant votre validation.";
        _state.AddThemeColorOverride("font_color", draft.ExistingProfile ? StudioStyle.Success : StudioStyle.Warning);
        _slots.Value = draft.CapacitySlots;
        _mass.Value = draft.CapacityMass;
        SelectOwnership(draft.OwnershipPolicy);
        _open.ButtonPressed = draft.AccessPermissions.Contains(StandardPermissions[0], StringComparer.Ordinal);
        _deposit.ButtonPressed = draft.AccessPermissions.Contains(StandardPermissions[1], StringComparer.Ordinal);
        _withdraw.ButtonPressed = draft.AccessPermissions.Contains(StandardPermissions[2], StringComparer.Ordinal);
        _additionalPermissions = draft.AccessPermissions
            .Where(value => !StandardPermissions.Contains(value, StringComparer.Ordinal))
            .ToArray();
        _persistent.ButtonPressed = draft.Persistent;
        _replicated.ButtonPressed = draft.Replicated;
        _inventory.Text = draft.InventoryId;
        _loot.Text = draft.LootPolicyId;
        _ui.Text = draft.UiProfileId;
        _runtimeRequired.ButtonPressed = requiredAtRuntime;
        _loading = false;
    }

    internal StudioPlaceableContainerDraft Capture(StudioPlaceableContainerDraft source)
    {
        var permissions = new List<string>();
        if (_open.ButtonPressed) permissions.Add(StandardPermissions[0]);
        if (_deposit.ButtonPressed) permissions.Add(StandardPermissions[1]);
        if (_withdraw.ButtonPressed) permissions.Add(StandardPermissions[2]);
        permissions.AddRange(_additionalPermissions);
        return source with
        {
            CapacitySlots = (int)_slots.Value,
            CapacityMass = _mass.Value,
            OwnershipPolicy = OwnershipValue(),
            AccessPermissions = permissions.Distinct(StringComparer.Ordinal).ToArray(),
            Persistent = _persistent.ButtonPressed,
            LootPolicyId = _loot.Text,
            UiProfileId = _ui.Text,
            Replicated = _replicated.ButtonPressed,
            InventoryId = _inventory.Text,
        };
    }

    internal bool RequiredAtRuntime => _runtimeRequired.ButtonPressed;

    internal void SetRequiredAtRuntime(bool value)
    {
        _loading = true;
        _runtimeRequired.ButtonPressed = value;
        _loading = false;
    }

    internal void FocusCapacity() => _slots.GetLineEdit().GrabFocus();

    internal void FocusAccess() => _ownership.GrabFocus();

    internal void FocusReferences() => _inventory.GrabFocus();

    internal void SetEvidenceValues(
        int slots,
        double mass,
        string ownership,
        bool persistent,
        bool replicated,
        string inventory,
        string loot,
        string ui,
        bool requiredAtRuntime)
    {
        _slots.Value = slots;
        _mass.Value = mass;
        SelectOwnership(ownership);
        _open.ButtonPressed = true;
        _deposit.ButtonPressed = true;
        _withdraw.ButtonPressed = true;
        _persistent.ButtonPressed = persistent;
        _replicated.ButtonPressed = replicated;
        _inventory.Text = inventory;
        _loot.Text = loot;
        _ui.Text = ui;
        _runtimeRequired.ButtonPressed = requiredAtRuntime;
    }

    private void SelectOwnership(string value)
    {
        var normalized = value.Trim();
        var index = _ownershipValues.FindIndex(candidate => string.Equals(candidate, normalized, StringComparison.Ordinal));
        if (index < 0)
        {
            _ownershipValues.Add(normalized);
            _ownership.AddItem($"Règle existante · {normalized}");
            index = _ownershipValues.Count - 1;
        }
        _ownership.Select(index);
    }

    private string OwnershipValue()
    {
        var index = _ownership.Selected;
        return index >= 0 && index < _ownershipValues.Count ? _ownershipValues[index] : "party";
    }

    private void NotifyChanged()
    {
        if (!_loading)
        {
            Changed?.Invoke();
        }
    }

    private static SpinBox AddSpinCell(GridContainer parent, string label, double minimum, double maximum, double step)
    {
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 3);
        parent.AddChild(column);
        column.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var spin = new SpinBox
        {
            MinValue = minimum,
            MaxValue = maximum,
            Step = step,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        StudioStyle.ApplySpinBox(spin);
        column.AddChild(spin);
        return spin;
    }

    private static LineEdit AddLine(VBoxContainer parent, string label, string placeholder)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var line = new LineEdit { PlaceholderText = placeholder, ClearButtonEnabled = false };
        StudioStyle.ApplyLineEdit(line);
        parent.AddChild(line);
        return line;
    }

    private static CheckBox AddCheck(Container parent, string text)
    {
        var check = new CheckBox { Text = text, FocusMode = FocusModeEnum.All };
        check.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
        check.AddThemeColorOverride("font_color", StudioStyle.Text);
        parent.AddChild(check);
        return check;
    }
}
