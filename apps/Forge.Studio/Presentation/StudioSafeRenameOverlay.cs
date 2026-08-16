using Forge.Application.Creator;
using Forge.Application.Projects;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioSafeRenameOverlay : Control
{
    private readonly StudioSessionController _session;
    private readonly PanelContainer _dialog;
    private readonly Label _kind;
    private readonly Label _current;
    private readonly LineEdit _displayName;
    private readonly VBoxContainer _addressGroup;
    private readonly Label _addressLabel;
    private readonly LineEdit _address;
    private readonly Label _impact;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private StudioSafeRenameDraft? _draft;
    private bool _updating;
    private bool _addressEdited;
    private bool _contentUndoAvailable;
    private bool _contentRedoAvailable;

    internal event Action<string, bool>? StatusChanged;

    internal event Action? ContentChanged;

    internal bool IsOpen => Visible;

    internal bool ShowsHumanSurface =>
        Visible &&
        !_kind.Text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !_current.Text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !_impact.Text.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
        !_status.Text.Contains("UUID", StringComparison.OrdinalIgnoreCase);

    internal StudioSafeRenameOverlay(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Name = "StudioSafeRenameOverlay";
        Visible = false;
        ZIndex = 520;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var backdrop = new ColorRect
        {
            Color = new Color(0, 0, 0, .7f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        backdrop.GuiInput += BackdropInput;
        AddChild(backdrop);

        _dialog = StudioStyle.Panel("SafeRenameDialog", StudioStyle.Chrome, 12, 2);
        _dialog.ZIndex = 1;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(20, 18, 20, 18);
        _dialog.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);

        var heading = new HBoxContainer();
        heading.AddChild(StudioStyle.Label("RENOMMAGE SÛR", 12, StudioStyle.Accent));
        heading.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        heading.AddChild(StudioStyle.Label("Aperçu obligatoire · Échap pour fermer", 10, StudioStyle.Muted));
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);
        root.AddChild(heading);

        var intro = StudioStyle.Panel("SafeRenameIntro", StudioStyle.Surface, 8);
        var introMargin = StudioStyle.Margin(12, 10, 12, 10);
        intro.AddChild(introMargin);
        var introColumn = new VBoxContainer();
        introColumn.AddThemeConstantOverride("separation", 4);
        introMargin.AddChild(introColumn);
        _kind = StudioStyle.Label("Choisissez un contenu depuis la recherche globale", 16, StudioStyle.Text, wrap: true);
        introColumn.AddChild(_kind);
        _current = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted, wrap: true);
        introColumn.AddChild(_current);
        root.AddChild(intro);

        root.AddChild(StudioStyle.SectionLabel("NOM VISIBLE"));
        _displayName = new LineEdit
        {
            PlaceholderText = "Nom compris par l’équipe et les joueurs",
            ClearButtonEnabled = true,
        };
        StudioStyle.ApplyLineEdit(_displayName);
        _displayName.TextChanged += DisplayNameChanged;
        root.AddChild(_displayName);

        _addressGroup = new VBoxContainer();
        _addressGroup.AddThemeConstantOverride("separation", 5);
        _addressLabel = StudioStyle.SectionLabel("ADRESSE INTERNE");
        _addressGroup.AddChild(_addressLabel);
        _address = new LineEdit
        {
            PlaceholderText = "adresse-interne-sans-espace",
            ClearButtonEnabled = true,
        };
        StudioStyle.ApplyLineEdit(_address);
        _address.TextChanged += _ =>
        {
            if (!_updating)
            {
                _addressEdited = true;
                InvalidatePreview();
            }
        };
        _addressGroup.AddChild(_address);
        _addressGroup.AddChild(StudioStyle.Label(
            "Le Studio vérifie l’adresse, les doublons et toutes les références avant d’écrire.",
            10,
            StudioStyle.Muted,
            wrap: true));
        root.AddChild(_addressGroup);

        var impactPanel = StudioStyle.Panel("SafeRenameImpact", StudioStyle.SurfaceRaised, 8);
        var impactMargin = StudioStyle.Margin(12, 10, 12, 10);
        impactPanel.AddChild(impactMargin);
        var impactColumn = new VBoxContainer();
        impactColumn.AddThemeConstantOverride("separation", 5);
        impactMargin.AddChild(impactColumn);
        impactColumn.AddChild(StudioStyle.SectionLabel("IMPACT AVANT APPLICATION"));
        _impact = StudioStyle.Label(
            "Prévisualisez pour connaître les références conservées ou migrées.",
            11,
            StudioStyle.Text,
            wrap: true);
        impactColumn.AddChild(_impact);
        root.AddChild(impactPanel);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 7);
        _preview = StudioStyle.Button("1 · Prévisualiser", compact: true);
        _preview.Pressed += Preview;
        actions.AddChild(_preview);
        _apply = StudioStyle.Button("2 · Appliquer", primary: true, compact: true);
        _apply.Disabled = true;
        _apply.Pressed += Apply;
        actions.AddChild(_apply);
        actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _undo = StudioStyle.Button("Annuler ce renommage", compact: true);
        _undo.Disabled = true;
        _undo.Pressed += Undo;
        actions.AddChild(_undo);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _redo.Disabled = true;
        _redo.Pressed += Redo;
        actions.AddChild(_redo);
        root.AddChild(actions);

        _status = StudioStyle.Label(
            "Aucun changement ne sera appliqué sans aperçu validé.",
            10,
            StudioStyle.Muted,
            wrap: true);
        root.AddChild(_status);

        Resized += UpdateDialogBounds;
        UpdateDialogBounds();
    }

    internal bool Open(CreatorSearchDocument document)
    {
        var draft = _session.CreateSafeRenameDraft(document);
        if (draft is null)
        {
            return false;
        }

        _session.CancelSafeRename();
        _draft = draft;
        _updating = true;
        _addressEdited = false;
        _kind.Text = $"{draft.KindLabel} · {draft.CurrentDisplayName}";
        _current.Text = draft.IsWorkspaceResource
            ? $"Emplacement actuel : {draft.CurrentAddress}"
            : draft.ReferenceCount == 0
                ? "Aucune référence externe à migrer · identité interne conservée"
                : $"{draft.ReferenceCount} référence(s) resteront liées au même contenu";
        _displayName.Text = draft.CurrentDisplayName;
        _displayName.Editable = draft.HasEditableDisplayName;
        _displayName.TooltipText = draft.HasEditableDisplayName
            ? "Nom affiché dans le Studio et le jeu."
            : "Le nom visible d’une ressource vient de son fichier.";
        _addressGroup.Visible = draft.Document.Kind != CreatorContentKind.Project;
        _addressLabel.Text = draft.IsWorkspaceResource ? "NOUVEL EMPLACEMENT DANS LE PROJET" : "ADRESSE INTERNE";
        _address.PlaceholderText = draft.IsWorkspaceResource
            ? "scripts/nouveau-nom.forge ou assets/nouveau-nom.glb"
            : "adresse-interne-sans-espace";
        _address.Text = draft.CurrentAddress;
        _address.Editable = draft.HasEditableAddress;
        _address.TooltipText = draft.HasEditableAddress
            ? "Adresse validée et migrée avec toutes ses références."
            : "Cette adresse appartient à l’identité immuable du projet et reste conservée.";
        _impact.Text = "Prévisualisez pour connaître les références conservées ou migrées.";
        _status.Text = "Aucun changement ne sera appliqué sans aperçu validé.";
        _status.AddThemeColorOverride("font_color", StudioStyle.Muted);
        _apply.Disabled = true;
        _contentUndoAvailable = false;
        _contentRedoAvailable = false;
        _updating = false;
        Visible = true;
        UpdateHistoryButtons();
        if (IsInsideTree())
        {
            (_displayName.Editable ? _displayName : _address).GrabFocus();
        }
        return true;
    }

    internal void Close()
    {
        if (!Visible)
        {
            return;
        }
        _session.CancelSafeRename();
        _draft = null;
        Visible = false;
    }

    internal bool PrepareEvidence(CreatorSearchDocument document)
    {
        if (!Open(document) || _draft is null)
        {
            return false;
        }

        if (_draft.HasEditableDisplayName)
        {
            _updating = true;
            _displayName.Text = _draft.CurrentDisplayName + " — aperçu";
            if (_draft.HasEditableAddress)
            {
                _address.Text = SlugGenerator.FromDisplayName(_displayName.Text, "contenu");
            }
            _updating = false;
        }
        Preview();
        return _apply.Disabled == false && ShowsHumanSurface;
    }

    private void DisplayNameChanged(string value)
    {
        if (_updating)
        {
            return;
        }

        if (_draft is { HasEditableDisplayName: true, IsWorkspaceResource: false } &&
            _draft.HasEditableAddress && !_addressEdited && !string.IsNullOrWhiteSpace(value))
        {
            _updating = true;
            _address.Text = SlugGenerator.FromDisplayName(value, "contenu");
            _updating = false;
        }
        InvalidatePreview();
    }

    private void Preview()
    {
        if (_draft is null)
        {
            SetStatus("Choisissez d’abord un contenu depuis la recherche globale.", true);
            return;
        }

        var result = _session.PreviewSafeRename(_draft.Document, _displayName.Text, _address.Text);
        _impact.Text = string.IsNullOrWhiteSpace(result.Summary)
            ? "Aucun impact validé. Corrigez les champs puis relancez l’aperçu."
            : result.Summary;
        _apply.Disabled = !result.CanApply;
        SetStatus(result.Message, !result.Succeeded);
    }

    private void Apply()
    {
        var resource = _draft?.IsWorkspaceResource == true;
        var result = _session.ApplyApprovedSafeRename();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded)
        {
            _apply.Disabled = true;
            return;
        }

        _apply.Disabled = true;
        _preview.Disabled = true;
        if (!resource)
        {
            _contentUndoAvailable = true;
            _contentRedoAvailable = false;
        }
        UpdateHistoryButtons();
        ContentChanged?.Invoke();
    }

    private void Undo()
    {
        var result = _draft?.IsWorkspaceResource == true
            ? _session.UndoResourceRename()
            : _session.Undo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded && _draft?.IsWorkspaceResource != true)
        {
            _contentUndoAvailable = false;
            _contentRedoAvailable = true;
        }
        UpdateHistoryButtons();
        ContentChanged?.Invoke();
    }

    private void Redo()
    {
        var result = _draft?.IsWorkspaceResource == true
            ? _session.RedoResourceRename()
            : _session.Redo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded && _draft?.IsWorkspaceResource != true)
        {
            _contentUndoAvailable = true;
            _contentRedoAvailable = false;
        }
        UpdateHistoryButtons();
        ContentChanged?.Invoke();
    }

    private void UpdateHistoryButtons()
    {
        var resource = _draft?.IsWorkspaceResource == true;
        _undo.Disabled = resource ? !_session.CanUndoResourceRename : !_contentUndoAvailable;
        _redo.Disabled = resource ? !_session.CanRedoResourceRename : !_contentRedoAvailable;
    }

    private void InvalidatePreview()
    {
        _session.CancelSafeRename();
        _apply.Disabled = true;
        _impact.Text = "Valeurs modifiées · relancez l’aperçu avant d’appliquer.";
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Success);
        StatusChanged?.Invoke(message, error);
    }

    private void BackdropInput(InputEvent input)
    {
        if (input is InputEventMouseButton { Pressed: true })
        {
            Close();
        }
    }

    private void UpdateDialogBounds()
    {
        var width = Math.Clamp(Size.X - 56, 680, 860);
        var height = Math.Clamp(Size.Y - 160, 480, 560);
        _dialog.Position = new Vector2((Size.X - width) * .5f, Math.Max(48, (Size.Y - height) * .42f));
        _dialog.Size = new Vector2(width, height);
    }
}
