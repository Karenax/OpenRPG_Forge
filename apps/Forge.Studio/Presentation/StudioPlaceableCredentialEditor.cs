using Forge.Domain.Blueprints;
using Forge.Domain.WorldFeatures;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioPlaceableCredentialEditor : VBoxContainer
{
    private readonly Label _state;
    private readonly OptionButton _consumption;
    private readonly OptionButton _item;
    private readonly List<BlueprintId?> _itemIds = [];
    private readonly LineEdit _otherCredential;
    private readonly SpinBox _charges;
    private readonly LineEdit _virtualPermission;
    private readonly CheckBox _audit;
    private readonly CheckBox _runtimeRequired;
    private readonly OptionButton _previewMode;
    private readonly SpinBox _previewCharges;
    private readonly Button _preview;
    private readonly Label _previewResult;
    private bool _existingPolicy;
    private bool _loading;

    internal event Action? Changed;

    internal event Action<bool>? RuntimeRequirementChanged;

    internal event Action? PreviewRequested;

    internal bool RequiredAtRuntime => _runtimeRequired.ButtonPressed;

    internal StudioPlaceableCredentialPreviewMode PreviewMode =>
        _previewMode.Selected >= 0 &&
        _previewMode.Selected < Enum.GetValues<StudioPlaceableCredentialPreviewMode>().Length
            ? (StudioPlaceableCredentialPreviewMode)_previewMode.Selected
            : StudioPlaceableCredentialPreviewMode.PresentCredential;

    internal int PreviewAvailableCharges => (int)_previewCharges.Value;

    internal bool IsReady
    {
        get
        {
            var draft = Capture(new(
                null,
                string.Empty,
                PlaceableWorldProfile.CredentialConsumption.Retain,
                0,
                string.Empty,
                false,
                false));
            var identityReady = draft.Consumption == PlaceableWorldProfile.CredentialConsumption.VirtualPermission
                ? !string.IsNullOrWhiteSpace(draft.VirtualPermission)
                : draft.ItemBlueprintId is not null || !string.IsNullOrWhiteSpace(draft.OtherCredentialId);
            return identityReady &&
                   (draft.Consumption != PlaceableWorldProfile.CredentialConsumption.Charge || draft.Charges > 0);
        }
    }

    internal bool PreviewReady =>
        _previewResult.Text.Contains("Aperçu", StringComparison.OrdinalIgnoreCase) &&
        !_previewResult.Text.Contains("à recalculer", StringComparison.OrdinalIgnoreCase);

    internal string HumanText => string.Join(' ',
        _state.Text,
        "Justificatif Politique Conserver Consommer Charges Partager Permission virtuelle Objet du projet Audit Aperçu runtime",
        _previewResult.Text,
        string.Join(' ', Enumerable.Range(0, _item.ItemCount).Select(_item.GetItemText)),
        string.Join(' ', Enumerable.Range(0, _consumption.ItemCount).Select(_consumption.GetItemText)),
        string.Join(' ', Enumerable.Range(0, _previewMode.ItemCount).Select(_previewMode.GetItemText)));

    internal StudioPlaceableCredentialEditor()
    {
        Name = "StudioPlaceableCredentialEditor";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 7);

        _state = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted, wrap: true);
        _state.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        AddChild(_state);

        AddChild(StudioStyle.Label("POLITIQUE DU JUSTIFICATIF", 10, StudioStyle.Accent));
        _consumption = AddOption(this, "Après une utilisation");
        foreach (var mode in Enum.GetValues<PlaceableWorldProfile.CredentialConsumption>())
        {
            _consumption.AddItem(HumanConsumption(mode));
        }
        _item = AddOption(this, "Objet clé du projet");
        _otherCredential = AddLine(this, "Justificatif existant ou métier", "laissez-passer, membre-guilde");
        _charges = AddSpin(this, "Nombre de charges", 0, 999, 1);
        _virtualPermission = AddLine(this, "Permission virtuelle", "archive.enter");
        _audit = AddCheck(this, "Auditer chaque utilisation");

        AddChild(new HSeparator());
        AddChild(StudioStyle.Label("APERÇU SANS ÉCRITURE", 10, StudioStyle.Accent));
        _previewMode = AddOption(this, "Situation à simuler");
        foreach (var mode in Enum.GetValues<StudioPlaceableCredentialPreviewMode>())
        {
            _previewMode.AddItem(HumanPreviewMode(mode));
        }
        _previewCharges = AddSpin(this, "Charges disponibles dans l’inventaire", 0, 999, 1);
        _preview = StudioStyle.Button("Prévisualiser l’accès", compact: true);
        _preview.TooltipText = "Évaluer la politique canonique sans modifier l’inventaire ni le module";
        AddChild(_preview);
        _previewResult = StudioStyle.Label("Aucun accès prévisualisé.", 10, StudioStyle.Muted, wrap: true);
        _previewResult.AddThemeStyleboxOverride(
            "normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        AddChild(_previewResult);

        _runtimeRequired = AddCheck(this, "Exiger cette politique au runtime");
        _runtimeRequired.TooltipText =
            "Si activé, une politique absente produit un diagnostic au lieu d’un accès libre.";

        _consumption.ItemSelected += _ =>
        {
            UpdateModeFields();
            NotifyChanged();
        };
        _item.ItemSelected += _ =>
        {
            UpdateModeFields();
            NotifyChanged();
        };
        _otherCredential.TextChanged += _ => NotifyChanged();
        _charges.ValueChanged += _ => NotifyChanged();
        _virtualPermission.TextChanged += _ => NotifyChanged();
        _audit.Toggled += _ => NotifyChanged();
        _previewMode.ItemSelected += _ => PreviewInputChanged();
        _previewCharges.ValueChanged += _ => PreviewInputChanged();
        _preview.Pressed += () => PreviewRequested?.Invoke();
        _runtimeRequired.Toggled += value =>
        {
            if (!_loading)
            {
                RuntimeRequirementChanged?.Invoke(value);
                Changed?.Invoke();
            }
        };
    }

    internal void Load(
        StudioPlaceableCredentialDraft draft,
        IReadOnlyList<StudioPlaceableItemOption> items,
        bool enabled,
        bool requiredAtRuntime)
    {
        _loading = true;
        _existingPolicy = draft.ExistingPolicy;
        _consumption.Select((int)draft.Consumption);
        PopulateItems(items, draft.ItemBlueprintId);
        _otherCredential.Text = draft.OtherCredentialId;
        _charges.Value = draft.Charges;
        _virtualPermission.Text = draft.VirtualPermission;
        _audit.ButtonPressed = draft.AuditUse;
        _runtimeRequired.ButtonPressed = requiredAtRuntime;
        _previewMode.Select((int)(draft.Consumption ==
                                  PlaceableWorldProfile.CredentialConsumption.VirtualPermission
            ? StudioPlaceableCredentialPreviewMode.VirtualPermission
            : StudioPlaceableCredentialPreviewMode.PresentCredential));
        _previewCharges.Value = Math.Max(0, draft.Charges);
        _previewResult.Text = "Aucun accès prévisualisé.";
        _previewResult.AddThemeColorOverride("font_color", StudioStyle.Muted);
        SetEnabledState(enabled);
        UpdateModeFields();
        _loading = false;
    }

    internal StudioPlaceableCredentialDraft Capture(StudioPlaceableCredentialDraft source)
    {
        var itemIndex = _item.Selected;
        var consumption = _consumption.Selected >= 0
            ? (PlaceableWorldProfile.CredentialConsumption)_consumption.Selected
            : PlaceableWorldProfile.CredentialConsumption.Retain;
        return source with
        {
            ItemBlueprintId = consumption == PlaceableWorldProfile.CredentialConsumption.VirtualPermission
                ? null
                : itemIndex >= 0 && itemIndex < _itemIds.Count
                    ? _itemIds[itemIndex]
                    : null,
            OtherCredentialId = _otherCredential.Text,
            Consumption = consumption,
            Charges = (int)_charges.Value,
            VirtualPermission = _virtualPermission.Text,
            AuditUse = _audit.ButtonPressed,
        };
    }

    internal void SetEnabledState(bool enabled)
    {
        _state.Text = _existingPolicy
            ? "Politique canonique existante · l’identité métier est conservée jusqu’à l’application validée."
            : enabled
                ? "Nouvelle politique prête · l’aperçu ne retire ni objet ni charge."
                : "État vide · activez Clé consommée pour préparer la politique.";
        _state.AddThemeColorOverride("font_color", _existingPolicy || enabled
            ? StudioStyle.Success
            : StudioStyle.Warning);
    }

    internal void SetRequiredAtRuntime(bool value)
    {
        _loading = true;
        _runtimeRequired.ButtonPressed = value;
        _loading = false;
    }

    internal void FocusPolicy() => _consumption.GrabFocus();

    internal void FocusCharges() => _charges.GetLineEdit().GrabFocus();

    internal void FocusVirtualPermission() => _virtualPermission.GrabFocus();

    internal void FocusPreview() => _previewMode.GrabFocus();

    internal void SetEvidenceValues(string mode)
    {
        _loading = true;
        if (_itemIds.Count > 1)
        {
            _item.Select(1);
        }
        _otherCredential.Text = string.Empty;
        _audit.ButtonPressed = true;
        _runtimeRequired.ButtonPressed = true;
        _virtualPermission.Text = string.Empty;
        _charges.Value = 0;
        _previewCharges.Value = 2;
        if (mode.Contains("virtual", StringComparison.OrdinalIgnoreCase))
        {
            _consumption.Select((int)PlaceableWorldProfile.CredentialConsumption.VirtualPermission);
            _otherCredential.Text = "permission:archives";
            _virtualPermission.Text = "archives.enter";
            _previewMode.Select((int)StudioPlaceableCredentialPreviewMode.VirtualPermission);
        }
        else if (mode.Contains("charge", StringComparison.OrdinalIgnoreCase))
        {
            _consumption.Select((int)PlaceableWorldProfile.CredentialConsumption.Charge);
            _charges.Value = 3;
            _previewCharges.Value = 2;
            _previewMode.Select((int)StudioPlaceableCredentialPreviewMode.PresentCredential);
        }
        else if (mode.Contains("preview", StringComparison.OrdinalIgnoreCase))
        {
            _consumption.Select((int)PlaceableWorldProfile.CredentialConsumption.Consume);
            _previewMode.Select((int)StudioPlaceableCredentialPreviewMode.MissingCredential);
        }
        else
        {
            _consumption.Select((int)PlaceableWorldProfile.CredentialConsumption.Retain);
            _previewMode.Select((int)StudioPlaceableCredentialPreviewMode.PresentCredential);
        }
        UpdateModeFields();
        _loading = false;
        Changed?.Invoke();
        PreviewRequested?.Invoke();
    }

    internal void SetPreviewResult(StudioPlaceableCredentialPreview? result)
    {
        _previewResult.Text = result?.Summary ?? "Aucun accès prévisualisé.";
        _previewResult.AddThemeColorOverride(
            "font_color", result?.Succeeded == true && result.Allowed ? StudioStyle.Success : StudioStyle.Warning);
    }

    private void PopulateItems(IReadOnlyList<StudioPlaceableItemOption> items, BlueprintId? selected)
    {
        _itemIds.Clear();
        _item.Clear();
        _itemIds.Add(null);
        _item.AddItem("Aucun objet clé");
        var selectedIndex = 0;
        foreach (var item in items)
        {
            _itemIds.Add(item.Id);
            _item.AddItem(item.DisplayName);
            if (item.Id == selected)
            {
                selectedIndex = _item.ItemCount - 1;
            }
        }
        if (selected is not null && selectedIndex == 0)
        {
            _itemIds.Add(selected);
            _item.AddItem("Objet clé existant indisponible");
            _item.SetItemDisabled(_item.ItemCount - 1, true);
            selectedIndex = _item.ItemCount - 1;
        }
        _item.Select(selectedIndex);
    }

    private void UpdateModeFields()
    {
        var consumption = _consumption.Selected >= 0
            ? (PlaceableWorldProfile.CredentialConsumption)_consumption.Selected
            : PlaceableWorldProfile.CredentialConsumption.Retain;
        _item.Disabled = consumption == PlaceableWorldProfile.CredentialConsumption.VirtualPermission;
        var itemIndex = _item.Selected;
        var hasItem = itemIndex >= 0 && itemIndex < _itemIds.Count && _itemIds[itemIndex] is not null;
        _otherCredential.Editable = consumption ==
                                    PlaceableWorldProfile.CredentialConsumption.VirtualPermission || !hasItem;
        _charges.Editable = consumption == PlaceableWorldProfile.CredentialConsumption.Charge;
        _virtualPermission.Editable = consumption == PlaceableWorldProfile.CredentialConsumption.VirtualPermission;
        if (consumption == PlaceableWorldProfile.CredentialConsumption.Charge && _charges.Value < 1)
        {
            _charges.Value = 1;
        }
    }

    private void PreviewInputChanged()
    {
        if (!_loading)
        {
            _previewResult.Text = "Aperçu à recalculer · aucun inventaire n’a été modifié.";
            _previewResult.AddThemeColorOverride("font_color", StudioStyle.Muted);
        }
    }

    private void NotifyChanged()
    {
        if (!_loading)
        {
            PreviewInputChanged();
            Changed?.Invoke();
        }
    }

    private static string HumanConsumption(PlaceableWorldProfile.CredentialConsumption consumption) =>
        consumption switch
        {
            PlaceableWorldProfile.CredentialConsumption.Retain => "Conserver le justificatif",
            PlaceableWorldProfile.CredentialConsumption.Consume => "Consommer après usage",
            PlaceableWorldProfile.CredentialConsumption.Charge => "Dépenser une charge",
            PlaceableWorldProfile.CredentialConsumption.Shared => "Partager sans consommer",
            PlaceableWorldProfile.CredentialConsumption.VirtualPermission => "Vérifier une permission virtuelle",
            _ => "Politique du justificatif",
        };

    private static string HumanPreviewMode(StudioPlaceableCredentialPreviewMode mode) => mode switch
    {
        StudioPlaceableCredentialPreviewMode.PresentCredential => "Présenter le justificatif",
        StudioPlaceableCredentialPreviewMode.MissingCredential => "Ne pas présenter le justificatif",
        StudioPlaceableCredentialPreviewMode.VirtualPermission => "Présenter la permission virtuelle",
        StudioPlaceableCredentialPreviewMode.ExhaustedCharges => "Simuler des charges épuisées",
        _ => "Situation d’accès",
    };

    private static LineEdit AddLine(Container parent, string label, string placeholder)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var line = new LineEdit { PlaceholderText = placeholder, ClearButtonEnabled = false };
        StudioStyle.ApplyLineEdit(line);
        parent.AddChild(line);
        return line;
    }

    private static OptionButton AddOption(Container parent, string label)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var option = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(option);
        parent.AddChild(option);
        return option;
    }

    private static SpinBox AddSpin(Container parent, string label, double minimum, double maximum, double step)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var spin = new SpinBox
        {
            MinValue = minimum,
            MaxValue = maximum,
            Step = step,
            AllowGreater = false,
            AllowLesser = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        StudioStyle.ApplySpinBox(spin);
        parent.AddChild(spin);
        return spin;
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
