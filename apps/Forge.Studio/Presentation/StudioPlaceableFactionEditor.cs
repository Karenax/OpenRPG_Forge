using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioPlaceableFactionEditor : VBoxContainer
{
    private readonly Label _state;
    private readonly OptionButton _primary;
    private readonly List<Guid> _primaryIds = [];
    private readonly VBoxContainer _memberships;
    private readonly List<(Guid Id, CheckBox Check)> _membershipChecks = [];
    private readonly CheckBox _inheritHostility;
    private readonly CheckBox _crime;
    private readonly CheckBox _membersOnlyAccess;
    private readonly CheckBox _blockFriendlyTargeting;
    private readonly OptionButton _previewActor;
    private readonly List<Guid?> _previewActorIds = [];
    private readonly OptionButton _previewAction;
    private readonly List<string> _previewActions = ["use", "open", "move", "damage", "destroy"];
    private readonly Button _preview;
    private readonly Label _previewResult;
    private readonly CheckBox _runtimeRequired;
    private bool _loading;
    private bool _existingProfile;

    internal event Action? Changed;

    internal event Action? DecisionPreviewRequested;

    internal event Action<bool>? RuntimeRequirementChanged;

    internal bool RequiredAtRuntime => _runtimeRequired.ButtonPressed;

    internal Guid? PreviewActorFactionId =>
        _previewActor.Selected >= 0 && _previewActor.Selected < _previewActorIds.Count
            ? _previewActorIds[_previewActor.Selected]
            : null;

    internal string PreviewAction =>
        _previewAction.Selected >= 0 && _previewAction.Selected < _previewActions.Count
            ? _previewActions[_previewAction.Selected]
            : "use";

    internal bool HasFactionOptions => _primaryIds.Count > 0;

    internal bool IsReady
    {
        get
        {
            var primary = SelectedPrimaryFactionId();
            var memberships = SelectedMemberships();
            return Visible && primary != Guid.Empty && memberships.Count > 0 &&
                   memberships.Contains(primary) && memberships.Distinct().Count() == memberships.Count;
        }
    }

    internal bool DecisionPreviewReady =>
        _previewResult.Text.Contains("Décision", StringComparison.OrdinalIgnoreCase) &&
        (_previewResult.Text.Contains("accès", StringComparison.OrdinalIgnoreCase) ||
         _previewResult.Text.Contains("ciblage", StringComparison.OrdinalIgnoreCase));

    internal string HumanText => string.Join(' ',
        Enumerable.Range(0, _primary.ItemCount).Select(_primary.GetItemText)
            .Concat(Enumerable.Range(0, _previewActor.ItemCount).Select(_previewActor.GetItemText))
            .Append(_state.Text)
            .Append("Faction principale appartenances hostilité crime accès ciblage allié aperçu sans écriture runtime")
            .Append(_previewResult.Text));

    internal StudioPlaceableFactionEditor()
    {
        Name = "StudioPlaceableFactionEditor";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 7);

        _state = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted, wrap: true);
        _state.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        AddChild(_state);

        AddChild(StudioStyle.Label("RATTACHEMENT CANONIQUE", 10, StudioStyle.Accent));
        AddChild(StudioStyle.Label(
            "La faction principale porte les relations. Les appartenances partagées accordent les droits de membre sans afficher d’identifiant technique.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _primary = AddOption(this, "Faction principale");
        _memberships = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _memberships.AddThemeConstantOverride("separation", 3);
        AddChild(StudioStyle.Label("Appartenances du placeable", 10, StudioStyle.Muted));
        AddChild(_memberships);

        AddChild(new HSeparator());
        AddChild(StudioStyle.Label("EFFETS DE PROPRIÉTÉ", 10, StudioStyle.Accent));
        _inheritHostility = AddCheck(this, "Hériter de l’hostilité entre factions");
        _crime = AddCheck(this, "Signaler les actions non autorisées comme crime");
        _membersOnlyAccess = AddCheck(this, "Réserver l’accès aux factions membres");
        _blockFriendlyTargeting = AddCheck(this, "Bloquer le ciblage offensif des alliés");

        AddChild(new HSeparator());
        AddChild(StudioStyle.Label("APERÇU SANS ÉCRITURE", 10, StudioStyle.Accent));
        _previewActor = AddOption(this, "Faction de l’acteur");
        _previewAction = AddOption(this, "Action tentée");
        foreach (var action in _previewActions)
        {
            _previewAction.AddItem(HumanizeAction(action));
        }
        _preview = StudioStyle.Button("Prévisualiser la décision de faction", compact: true);
        _preview.TooltipText =
            "Projeter appartenance, relation, hostilité, crime, accès et ciblage sans modifier le module";
        AddChild(_preview);
        _previewResult = StudioStyle.Label(
            "Aucune décision prévisualisée.", 10, StudioStyle.Muted, wrap: true);
        _previewResult.AddThemeStyleboxOverride(
            "normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        AddChild(_previewResult);

        _runtimeRequired = AddCheck(this, "Exiger le rattachement de faction au runtime");
        _runtimeRequired.TooltipText =
            "Si activé, un profil absent produit un diagnostic au lieu d’un repli optionnel.";

        _primary.ItemSelected += _ =>
        {
            EnsurePrimaryMembership();
            PolicyChanged();
        };
        foreach (var check in new[]
                 {
                     _inheritHostility, _crime, _membersOnlyAccess, _blockFriendlyTargeting,
                 })
        {
            check.Toggled += _ => PolicyChanged();
        }
        _previewActor.ItemSelected += _ => ClearDecisionPreview();
        _previewAction.ItemSelected += _ => ClearDecisionPreview();
        _preview.Pressed += () => DecisionPreviewRequested?.Invoke();
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
        StudioPlaceableFactionOwnershipDraft draft,
        IReadOnlyList<StudioPlaceableFactionOption> factions,
        bool enabled,
        bool requiredAtRuntime)
    {
        _loading = true;
        _existingProfile = draft.ExistingProfile;
        PopulateFactionChoices(factions, draft.PrimaryFactionId, draft.MembershipFactionIds);
        _inheritHostility.ButtonPressed = draft.InheritHostility;
        _crime.ButtonPressed = draft.UnauthorizedActionsAreCrime;
        _membersOnlyAccess.ButtonPressed = draft.MembersOnlyAccess;
        _blockFriendlyTargeting.ButtonPressed = draft.BlockFriendlyTargeting;
        _previewAction.Select(0);
        _runtimeRequired.ButtonPressed = requiredAtRuntime;
        _previewResult.Text = "Aucune décision prévisualisée.";
        _previewResult.AddThemeColorOverride("font_color", StudioStyle.Muted);
        UpdateEnabledState(enabled);
        _loading = false;
    }

    internal StudioPlaceableFactionOwnershipDraft Capture(StudioPlaceableFactionOwnershipDraft source) =>
        source with
        {
            PrimaryFactionId = SelectedPrimaryFactionId(),
            MembershipFactionIds = SelectedMemberships(),
            InheritHostility = _inheritHostility.ButtonPressed,
            UnauthorizedActionsAreCrime = _crime.ButtonPressed,
            MembersOnlyAccess = _membersOnlyAccess.ButtonPressed,
            BlockFriendlyTargeting = _blockFriendlyTargeting.ButtonPressed,
        };

    internal void SetEnabledState(bool enabled) => UpdateEnabledState(enabled);

    internal void SetRequiredAtRuntime(bool value)
    {
        _loading = true;
        _runtimeRequired.ButtonPressed = value;
        _loading = false;
    }

    internal void FocusPrimaryFaction() => _primary.GrabFocus();

    internal void FocusPolicies() => _inheritHostility.GrabFocus();

    internal void FocusDecision() => _previewActor.GrabFocus();

    internal void SetEvidenceValues(bool hostileActor)
    {
        _loading = true;
        if (_primaryIds.Count > 0)
        {
            _primary.Select(0);
            for (var index = 0; index < _membershipChecks.Count; index++)
            {
                _membershipChecks[index].Check.ButtonPressed = index < Math.Min(2, _membershipChecks.Count);
            }
            EnsurePrimaryMembership();
        }
        _inheritHostility.ButtonPressed = true;
        _crime.ButtonPressed = true;
        _membersOnlyAccess.ButtonPressed = true;
        _blockFriendlyTargeting.ButtonPressed = true;
        _previewActor.Select(hostileActor && _previewActor.ItemCount > 3
            ? 3
            : Math.Min(1, _previewActor.ItemCount - 1));
        _previewAction.Select(hostileActor ? _previewActions.IndexOf("destroy") : _previewActions.IndexOf("use"));
        _runtimeRequired.ButtonPressed = true;
        _loading = false;
        ClearDecisionPreview();
        Changed?.Invoke();
    }

    internal void SetDecisionPreview(StudioPlaceableFactionDecisionPreview preview)
    {
        _previewResult.Text = preview.Summary;
        _previewResult.AddThemeColorOverride(
            "font_color", preview.Succeeded ? StudioStyle.Success : StudioStyle.Danger);
    }

    private void PopulateFactionChoices(
        IReadOnlyList<StudioPlaceableFactionOption> factions,
        Guid selectedPrimary,
        IReadOnlyList<Guid> selectedMemberships)
    {
        _primaryIds.Clear();
        _primary.Clear();
        _previewActorIds.Clear();
        _previewActor.Clear();
        _previewActorIds.Add(null);
        _previewActor.AddItem("Sans faction");
        foreach (var child in _memberships.GetChildren())
        {
            _memberships.RemoveChild(child);
            child.QueueFree();
        }
        _membershipChecks.Clear();

        var primaryIndex = 0;
        foreach (var faction in factions)
        {
            _primaryIds.Add(faction.Id);
            _primary.AddItem(faction.Archived ? $"{faction.DisplayName} · archivée" : faction.DisplayName);
            _primary.SetItemDisabled(_primary.ItemCount - 1, faction.Archived && faction.Id != selectedPrimary);
            if (faction.Id == selectedPrimary)
            {
                primaryIndex = _primary.ItemCount - 1;
            }

            var check = AddCheck(_memberships,
                faction.Archived ? $"{faction.DisplayName} · archivée" : faction.DisplayName);
            check.ButtonPressed = selectedMemberships.Contains(faction.Id);
            check.Disabled = faction.Archived && !check.ButtonPressed;
            check.Toggled += _ => PolicyChanged();
            _membershipChecks.Add((faction.Id, check));

            _previewActorIds.Add(faction.Id);
            _previewActor.AddItem(faction.DisplayName);
        }
        if (_primary.ItemCount > 0)
        {
            _primary.Select(primaryIndex);
        }
        _previewActor.Select(_previewActor.ItemCount > 1 ? 1 : 0);
    }

    private Guid SelectedPrimaryFactionId() =>
        _primary.Selected >= 0 && _primary.Selected < _primaryIds.Count
            ? _primaryIds[_primary.Selected]
            : Guid.Empty;

    private IReadOnlyList<Guid> SelectedMemberships() => _membershipChecks
        .Where(item => item.Check.ButtonPressed)
        .Select(item => item.Id)
        .ToArray();

    private void EnsurePrimaryMembership()
    {
        var primary = SelectedPrimaryFactionId();
        var membership = _membershipChecks.FirstOrDefault(item => item.Id == primary);
        if (membership.Check is not null)
        {
            membership.Check.ButtonPressed = true;
        }
    }

    private void PolicyChanged()
    {
        if (_loading)
        {
            return;
        }
        ClearDecisionPreview();
        Changed?.Invoke();
    }

    private void ClearDecisionPreview()
    {
        if (_loading)
        {
            return;
        }
        _previewResult.Text = "Décision à recalculer · le module n’a pas été modifié.";
        _previewResult.AddThemeColorOverride("font_color", StudioStyle.Muted);
    }

    private void UpdateEnabledState(bool enabled)
    {
        _state.Text = _existingProfile
            ? "Rattachement canonique existant · les appartenances et politiques masquées sont conservées."
            : !enabled
                ? "État vide · activez Faction et propriété pour préparer un rattachement sûr."
                : _primaryIds.Count == 0
                    ? "Aucune faction active · créez d’abord une faction dans le projet."
                    : "Nouveau rattachement prêt · il reste dans le brouillon jusqu’à l’application validée.";
        _state.AddThemeColorOverride(
            "font_color", _existingProfile || enabled && _primaryIds.Count > 0
                ? StudioStyle.Success
                : StudioStyle.Warning);
    }

    private static string HumanizeAction(string action) => action switch
    {
        "use" => "Utiliser l’objet",
        "open" => "Ouvrir l’objet",
        "move" => "Déplacer l’objet",
        "damage" => "Endommager l’objet",
        "destroy" => "Détruire l’objet",
        _ => "Action",
    };

    private static OptionButton AddOption(Container parent, string label)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var option = new OptionButton();
        StudioStyle.ApplyOptionButton(option);
        parent.AddChild(option);
        return option;
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
