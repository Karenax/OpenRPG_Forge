using System.Globalization;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioCreaturePresentationOverlayUx4E : Control
{
    private StudioSessionController _session;
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private StudioCreaturePresentationEvidenceSourceSnapshotUx4E? _sourceSnapshot;
    private BlueprintId? _evidenceBlueprintId;
    private bool _usesRealPresentation;
    private bool _usesFallback;
    private bool _previewDryRun;
    private bool _resourcesReleased = true;
    private bool _sourceRestored = true;
    private bool _updating;
    private bool _applied;
    private string _lastFailure = "none";
    private IReadOnlyList<StudioCreaturePresentationChoiceUx4E> _choices = [];
    private StudioCreaturePresentationDraftUx4E? _draft;
    private StudioCreaturePresentationPreviewUx4E? _previewResult;
    private readonly List<CreaturePresentationProfile.PortraitVariant> _portraits = [];
    private readonly List<CreaturePresentationProfile.BodySlot> _bodySlots = [];
    private readonly List<CreaturePresentationProfile.MaterialColor> _colors = [];
    private readonly List<CreaturePresentationProfile.Attachment> _attachments = [];

    private readonly OptionButton _creature;
    private readonly Label _context;
    private readonly TabContainer _tabs;
    private readonly LineEdit _gender;
    private readonly LineEdit _bodyType;
    private readonly LineEdit _pronounSubject;
    private readonly LineEdit _pronounObject;
    private readonly LineEdit _pronounPossessive;
    private readonly LineEdit _pronounReflexive;
    private readonly CheckBox _modelFallback;
    private readonly LineEdit _rig;
    private readonly ItemList _portraitList;
    private readonly LineEdit _portraitExpression;
    private readonly LineEdit _portraitPath;
    private readonly LineEdit _portraitWidth;
    private readonly LineEdit _portraitHeight;
    private readonly LineEdit _portraitLicense;
    private readonly LineEdit _portraitHash;
    private readonly LineEdit _portraitAlt;
    private readonly TextureRect _portraitPreview;
    private readonly Label _portraitPreviewStatus;
    private readonly ItemList _bodyList;
    private readonly LineEdit _bodySlot;
    private readonly LineEdit _bodyAsset;
    private readonly LineEdit _bodyLayer;
    private readonly LineEdit _bodyHidden;
    private readonly LineEdit _bodyClipping;
    private readonly ItemList _colorList;
    private readonly LineEdit _colorParameter;
    private readonly LineEdit _colorHex;
    private readonly ItemList _attachmentList;
    private readonly LineEdit _attachmentSlot;
    private readonly LineEdit _attachmentKind;
    private readonly LineEdit _attachmentAsset;
    private readonly TextureRect _reviewPortrait;
    private readonly Label _reviewText;
    private readonly RichTextLabel _summary;
    private readonly VBoxContainer _diagnostics;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    internal event Action? CreatureChanged;
    internal event Action<string, bool>? StatusChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _choices.Count;
    internal bool HasExplicitSelection => _creature.Selected > 0 && _draft is not null;
    internal bool CanonicalPreviewReady => _previewResult?.CanApply == true;
    internal bool ShowsGenderAndPronouns => _summary.Text.Contains("Genre et pronoms", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsPortraits => _summary.Text.Contains("Portraits", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsAppearanceAndModel => _summary.Text.Contains("Apparence", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsBodyParts => _bodySlots.Count > 0 && _summary.Text.Contains("partie", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsColorsWingsTails => _colors.Count > 0 && _attachments.Count > 0 &&
                                          _summary.Text.Contains("attachement", StringComparison.OrdinalIgnoreCase);
    internal bool PortraitPreviewReady => _portraitPreview.Texture is not null || _reviewPortrait.Texture is not null;
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', VisibleText(this));
            return text.Contains("Genre et pronoms", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Portraits gouvernés", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Apparence et modèle", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Couleurs, ailes et queues", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu et validation", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsGuid(text);
        }
    }
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealPresentationEvidence => _usesRealPresentation;
    internal bool UsesCanonicalFallbackEvidence => _usesFallback;
    internal bool EvidencePreviewDryRun => _previewDryRun;
    internal bool EvidenceSourceUntouched => _sourceSnapshot is not null && SourceMatches(_sourceSnapshot);
    internal bool EvidenceResourcesReleased => _resourcesReleased;
    internal bool SourceSessionRestored => _sourceRestored;
    internal string LastEvidenceFailure => _lastFailure;

    internal StudioCreaturePresentationOverlayUx4E(StudioSessionController session)
    {
        _session = session;
        _sourceSession = session;
        Name = "StudioCreaturePresentationOverlayUx4E";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        var shade = new ColorRect { Color = new Color(0.012f, 0.018f, 0.03f, 0.94f) };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        var dialog = StudioStyle.Panel("CreaturePresentationDialogUx4E", StudioStyle.Chrome, 14, 2);
        dialog.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        dialog.OffsetLeft = 58;
        dialog.OffsetTop = 30;
        dialog.OffsetRight = -58;
        dialog.OffsetBottom = -30;
        AddChild(dialog);
        var root = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 7);
        dialog.AddChild(root);

        var header = new HBoxContainer();
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("PRÉSENTATION DE CRÉATURE · UX4-E", 17, StudioStyle.Accent, true));
        titles.AddChild(StudioStyle.Label(
            "Genre et pronoms, portraits gouvernés, modèle, parties du corps, couleurs, ailes et queues · aucun UUID affiché.",
            10, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        header.AddChild(close);
        root.AddChild(header);

        var contextRow = new HBoxContainer();
        contextRow.AddChild(StudioStyle.Label("Créature", 9, StudioStyle.Muted));
        _creature = new OptionButton { CustomMinimumSize = new Vector2(340, 30) };
        contextRow.AddChild(_creature);
        _context = StudioStyle.Label("Sélection explicite requise · aucune présentation créée automatiquement.",
            9, StudioStyle.Muted, wrap: true);
        _context.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        contextRow.AddChild(_context);
        root.AddChild(contextRow);

        var tabHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 360),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        root.AddChild(tabHost);
        _tabs = new TabContainer();
        _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        tabHost.AddChild(_tabs);

        var identity = Tab(_tabs, "1 · Genre et pronoms");
        identity.AddChild(StudioStyle.Label("Genre et pronoms", 11, StudioStyle.Accent));
        identity.AddChild(StudioStyle.Label(
            "Identifiants extensibles et quatre formes grammaticales ; aucune liste fermée n’impose une identité.",
            9, StudioStyle.Muted, wrap: true));
        _gender = Field(identity, "Genre canonique", "nonbinary");
        _bodyType = Field(identity, "Type de corps", "humanoid-medium");
        var pronouns = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _pronounSubject = GridField(pronouns, "Sujet", "iel");
        _pronounObject = GridField(pronouns, "Objet", "iel");
        _pronounPossessive = GridField(pronouns, "Possessif", "son");
        _pronounReflexive = GridField(pronouns, "Réfléchi", "soi-même");
        identity.AddChild(pronouns);

        var portraits = Tab(_tabs, "2 · Portraits gouvernés");
        portraits.AddChild(StudioStyle.Label("Portraits gouvernés", 11, StudioStyle.Accent));
        portraits.AddChild(StudioStyle.Label(
            "Chaque variante garde expression, chemin relatif, dimensions, licence, empreinte SHA-256 et texte alternatif.",
            9, StudioStyle.Muted, wrap: true));
        var portraitRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _portraitList = new ItemList { CustomMinimumSize = new Vector2(300, 245), SizeFlagsVertical = SizeFlags.ExpandFill };
        portraitRow.AddChild(_portraitList);
        var portraitForm = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _portraitExpression = Field(portraitForm, "Expression", "neutral");
        _portraitPath = Field(portraitForm, "Asset relatif", "assets/portraits/hero.png");
        var portraitDimensions = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _portraitWidth = GridField(portraitDimensions, "Largeur", "512");
        _portraitHeight = GridField(portraitDimensions, "Hauteur", "512");
        portraitForm.AddChild(portraitDimensions);
        _portraitLicense = Field(portraitForm, "Licence SPDX", "CC0-1.0");
        _portraitHash = Field(portraitForm, "SHA-256", "64 caractères hexadécimaux");
        _portraitAlt = Field(portraitForm, "Texte alternatif", "Portrait de la créature");
        portraitForm.AddChild(CollectionButtons("Nouveau portrait", SavePortrait, RemovePortrait, ClearPortraitEditor));
        portraitRow.AddChild(portraitForm);
        var portraitVisual = new VBoxContainer { CustomMinimumSize = new Vector2(180, 0) };
        _portraitPreview = new TextureRect
        {
            CustomMinimumSize = new Vector2(168, 168),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        portraitVisual.AddChild(_portraitPreview);
        _portraitPreviewStatus = StudioStyle.Label("Sélectionnez un portrait.", 9, StudioStyle.Muted, wrap: true);
        portraitVisual.AddChild(_portraitPreviewStatus);
        portraitRow.AddChild(portraitVisual);
        portraits.AddChild(portraitRow);

        var appearance = Tab(_tabs, "3 · Apparence et modèle");
        appearance.AddChild(StudioStyle.Label("Apparence et modèle", 11, StudioStyle.Accent));
        appearance.AddChild(StudioStyle.Label(
            "Le rig est l’autorité de compatibilité. Le repli 3D est une politique ; aucun modèle n’est instancié ici.",
            9, StudioStyle.Muted, wrap: true));
        _modelFallback = new CheckBox { Text = "Autoriser le repli vers le modèle 3D" };
        appearance.AddChild(_modelFallback);
        _rig = Field(appearance, "Rig canonique", "rig.humanoid");
        var bodyRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _bodyList = new ItemList { CustomMinimumSize = new Vector2(320, 220), SizeFlagsVertical = SizeFlags.ExpandFill };
        bodyRow.AddChild(_bodyList);
        var bodyForm = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _bodySlot = Field(bodyForm, "Partie / slot", "head");
        _bodyAsset = Field(bodyForm, "Asset", "asset.helmet");
        _bodyLayer = Field(bodyForm, "Couche 0..64", "4");
        _bodyHidden = Field(bodyForm, "Slots masqués", "hair");
        _bodyClipping = Field(bodyForm, "Règle de clipping", "hide-covered");
        bodyForm.AddChild(CollectionButtons("Nouvelle partie", SaveBodySlot, RemoveBodySlot, ClearBodyEditor));
        bodyRow.AddChild(bodyForm);
        appearance.AddChild(bodyRow);
        appearance.AddChild(StudioStyle.Label(
            "La voix et le soundset appartiennent à CRE-029 : ils restent présents mais ne sont jamais réécrits par cette fiche.",
            9, StudioStyle.Muted, wrap: true));

        var materials = Tab(_tabs, "4 · Couleurs, ailes et queues");
        materials.AddChild(StudioStyle.Label("Couleurs, ailes et queues", 11, StudioStyle.Accent));
        materials.AddChild(StudioStyle.Label(
            "Couleurs de matériau et attachements sont deux collections complètes, sélectionnables et indépendantes.",
            9, StudioStyle.Muted, wrap: true));
        var materialColumns = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var colorColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        colorColumn.AddChild(StudioStyle.Label("Couleurs", 10, StudioStyle.Text));
        _colorList = new ItemList { CustomMinimumSize = new Vector2(0, 125) };
        colorColumn.AddChild(_colorList);
        _colorParameter = Field(colorColumn, "Paramètre", "primary");
        _colorHex = Field(colorColumn, "Couleur", "#D68A3A");
        colorColumn.AddChild(CollectionButtons("Nouvelle couleur", SaveColor, RemoveColor, ClearColorEditor));
        materialColumns.AddChild(colorColumn);
        var attachmentColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        attachmentColumn.AddChild(StudioStyle.Label("Ailes, queues et attachements", 10, StudioStyle.Text));
        _attachmentList = new ItemList { CustomMinimumSize = new Vector2(0, 125) };
        attachmentColumn.AddChild(_attachmentList);
        _attachmentSlot = Field(attachmentColumn, "Slot", "back");
        _attachmentKind = Field(attachmentColumn, "Type", "wings");
        _attachmentAsset = Field(attachmentColumn, "Asset", "asset.wings");
        attachmentColumn.AddChild(CollectionButtons("Nouvel attachement", SaveAttachment, RemoveAttachment,
            ClearAttachmentEditor));
        materialColumns.AddChild(attachmentColumn);
        materials.AddChild(materialColumns);

        var review = Tab(_tabs, "5 · Aperçu et validation");
        review.AddChild(StudioStyle.Label("Aperçu et validation", 11, StudioStyle.Accent));
        review.AddChild(StudioStyle.Label(
            "Aperçu UI du portrait et projection du profil uniquement : aucun modèle, animation, moteur ou réseau n’est exécuté.",
            9, StudioStyle.Muted, wrap: true));
        var reviewPanel = StudioStyle.Panel("CreaturePresentationReviewUx4E", StudioStyle.Surface, 12, 1);
        reviewPanel.CustomMinimumSize = new Vector2(0, 190);
        review.AddChild(reviewPanel);
        var reviewRow = new HBoxContainer();
        reviewPanel.AddChild(reviewRow);
        _reviewPortrait = new TextureRect
        {
            CustomMinimumSize = new Vector2(180, 180),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        reviewRow.AddChild(_reviewPortrait);
        _reviewText = StudioStyle.Label("Sélectionnez une créature pour composer son aperçu.", 12,
            StudioStyle.Text, wrap: true);
        _reviewText.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        reviewRow.AddChild(_reviewText);
        _diagnostics = new VBoxContainer();
        review.AddChild(_diagnostics);

        _summary = new RichTextLabel
        {
            BbcodeEnabled = true,
            CustomMinimumSize = new Vector2(0, 132),
            FitContent = false,
            SelectionEnabled = true,
        };
        root.AddChild(_summary);
        root.AddChild(StudioStyle.Label(
            "Prévisualiser ne modifie rien. Appliquer écrit une commande annulable ; Enregistrer persiste ensuite le workspace.",
            9, StudioStyle.Muted, wrap: true));
        var footer = new HBoxContainer();
        _preview = StudioStyle.Button("Prévisualiser", primary: true, compact: true);
        _apply = StudioStyle.Button("Appliquer", compact: true);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
        {
            button.CustomMinimumSize = new Vector2(button == _preview ? 132 : 104, 34);
            footer.AddChild(button);
        }
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        root.AddChild(footer);

        ConnectEvents();
        RenderEmpty("Choisissez explicitement une créature. Une présentation absente ne sera créée qu’après aperçu et application.");
        SetEditingEnabled(false);
    }

    internal void Open()
    {
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence();
        _session = _sourceSession;
        OpenBoundSession();
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        _session = _sourceSession;
        OpenBoundSession();
    }

    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) && _evidenceSession is null && _evidenceRoot is null)
            return;
        _session.CancelCreaturePresentationUx4E();
        _previewResult = null;
        _draft = null;
        Visible = false;
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence();
    }

    private void OpenBoundSession()
    {
        _session.CancelCreaturePresentationUx4E();
        _previewResult = null;
        _draft = null;
        _applied = false;
        _choices = _session.CreaturePresentationChoicesUx4E;
        _updating = true;
        _creature.Clear();
        _creature.AddItem("Choisir explicitement une créature…");
        foreach (var choice in _choices) _creature.AddItem(choice.Label);
        _creature.Select(0);
        _updating = false;
        ClearControls();
        Visible = true;
        SetEditingEnabled(false);
        _context.Text = _choices.Count == 0
            ? "Aucune créature éditable dans ce module."
            : "Sélection explicite requise · aucune présentation créée automatiquement.";
        RenderEmpty(_choices.Count == 0
            ? "État vide réel : ajoutez d’abord un blueprint Créature dans le parcours de contenu."
            : "Choisissez une créature pour ouvrir sa présentation canonique.");
    }

    private void SelectCreature(long selected)
    {
        if (_updating) return;
        _session.CancelCreaturePresentationUx4E();
        _previewResult = null;
        _apply.Disabled = true;
        if (selected <= 0 || selected > _choices.Count)
        {
            _draft = null;
            ClearControls();
            SetEditingEnabled(false);
            RenderEmpty("Sélection explicite requise.");
            return;
        }
        var choice = _choices[(int)selected - 1];
        _draft = _session.CreateCreaturePresentationDraftUx4E(choice.BlueprintId);
        if (_draft is null)
        {
            ClearControls();
            SetEditingEnabled(false);
            RenderEmpty(choice.IsReadOnly
                ? "Ce blueprint installé est en lecture seule. Créez d’abord une variante de projet."
                : "La présentation ne peut pas être ouverte.");
            return;
        }
        Populate(_draft);
        _context.Text = $"{choice.Label} · {(choice.HasPresentation ? "présentation existante" : "initialisation proposée")} · identités protégées";
        SetEditingEnabled(true);
        RenderEmpty(choice.HasPresentation
            ? "Présentation chargée. Modifiez la fiche puis prévisualisez."
            : "Présentation absente : une valeur complète est proposée, sans écriture avant Appliquer.");
    }

    private void Populate(StudioCreaturePresentationDraftUx4E draft)
    {
        _updating = true;
        _gender.Text = draft.GenderId;
        _bodyType.Text = draft.BodyTypeId;
        _pronounSubject.Text = draft.PronounSubject;
        _pronounObject.Text = draft.PronounObject;
        _pronounPossessive.Text = draft.PronounPossessive;
        _pronounReflexive.Text = draft.PronounReflexive;
        _modelFallback.ButtonPressed = draft.UseModelFallback;
        _rig.Text = draft.RigId;
        _portraits.Clear(); _portraits.AddRange(draft.Portraits);
        _bodySlots.Clear(); _bodySlots.AddRange(draft.BodySlots);
        _colors.Clear(); _colors.AddRange(draft.MaterialColors);
        _attachments.Clear(); _attachments.AddRange(draft.Attachments);
        RebuildPortraitList(0);
        RebuildBodyList(0);
        RebuildColorList(0);
        RebuildAttachmentList(0);
        _updating = false;
        UpdateReview();
    }

    private StudioCreaturePresentationDraftUx4E? ReadDraft(bool commitEditors = true)
    {
        if (_draft is null || commitEditors && !CommitEditors()) return null;
        return _draft with
        {
            GenderId = _gender.Text,
            BodyTypeId = _bodyType.Text,
            PronounSubject = _pronounSubject.Text,
            PronounObject = _pronounObject.Text,
            PronounPossessive = _pronounPossessive.Text,
            PronounReflexive = _pronounReflexive.Text,
            UseModelFallback = _modelFallback.ButtonPressed,
            RigId = _rig.Text,
            Portraits = _portraits.ToArray(),
            BodySlots = _bodySlots.Select(item => item with { RequiredRigId = _rig.Text }).ToArray(),
            MaterialColors = _colors.ToArray(),
            Attachments = _attachments.Select(item => item with { RequiredRigId = _rig.Text }).ToArray(),
        };
    }

    private void Preview()
    {
        var draft = ReadDraft();
        if (draft is null)
        {
            RenderEmpty("Un éditeur de collection contient une valeur invalide.");
            return;
        }
        var project = _session.Project;
        var history = _session.History;
        var revision = history?.State.Revision;
        var journal = history?.Journal.ToArray() ?? [];
        var dirty = _session.IsDirty;
        _previewResult = _session.PreviewCreaturePresentationUx4E(draft);
        _previewDryRun = project is not null && history is not null && ReferenceEquals(project, _session.Project) &&
                         ReferenceEquals(history, _session.History) && history.State.Revision == revision &&
                         history.Journal.SequenceEqual(journal) && _session.IsDirty == dirty;
        RenderPreview(_previewResult, _previewDryRun);
    }

    private void Apply()
    {
        var draft = ReadDraft(commitEditors: false);
        if (draft is null) return;
        var result = _session.ApplyApprovedCreaturePresentationUx4E(draft);
        _applied = result.Succeeded;
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded)
        {
            _apply.Disabled = true;
            return;
        }
        CreatureChanged?.Invoke();
        RefreshAfterMutation(draft.BlueprintId);
    }

    private void Undo()
    {
        var selected = _draft?.BlueprintId;
        var result = _session.UndoCreaturePresentationUx4E();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            CreatureChanged?.Invoke();
            RefreshAfterMutation(selected);
        }
    }

    private void Redo()
    {
        var selected = _draft?.BlueprintId;
        var result = _session.RedoCreaturePresentationUx4E();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            CreatureChanged?.Invoke();
            RefreshAfterMutation(selected);
        }
    }

    private void Save()
    {
        var result = _session.Save();
        SetStatus(result.Message, !result.Succeeded);
    }

    private void RefreshAfterMutation(BlueprintId? selected)
    {
        _choices = _session.CreaturePresentationChoicesUx4E;
        _updating = true;
        _creature.Clear();
        _creature.AddItem("Choisir explicitement une créature…");
        foreach (var choice in _choices) _creature.AddItem(choice.Label);
        var index = selected is null ? -1 : _choices.ToList().FindIndex(item => item.BlueprintId == selected);
        _creature.Select(index >= 0 ? index + 1 : 0);
        _updating = false;
        SelectCreature(index >= 0 ? index + 1 : 0);
    }

    private void RenderPreview(StudioCreaturePresentationPreviewUx4E result, bool dryRun)
    {
        _summary.Text =
            $"[b]{(result.Succeeded ? "APERÇU CANONIQUE" : "CONFIGURATION REFUSÉE")}[/b]\n" +
            $"{result.Message}\n{result.PatchSummary}\nInverse : {result.InverseSummary}\n\n" +
            $"[color=#b8d9ff]Genre et pronoms[/color] · {result.IdentitySummary}\n" +
            $"[color=#b8d9ff]Portraits[/color] · {result.PortraitSummary}\n" +
            $"[color=#b8d9ff]Apparence[/color] · {result.AppearanceSummary}\n" +
            $"[color=#b8d9ff]Runtime[/color] · {result.RuntimeSummary}\n" +
            $"[color={(dryRun ? "#8ce0bd" : "#ff7777")}]Aperçu sans mutation : {dryRun.ToString().ToLowerInvariant()}[/color]";
        foreach (var child in _diagnostics.GetChildren()) child.QueueFree();
        if (result.Diagnostics.Count == 0)
            _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic bloquant.", 10, StudioStyle.Success, true));
        else
            foreach (var item in result.Diagnostics.Take(7))
                _diagnostics.AddChild(StudioStyle.Label($"{item.Code}\n{item.HumanMessage}", 9, StudioStyle.Danger, true));
        _apply.Disabled = !result.CanApply;
        UpdateReview();
    }

    private void RenderEmpty(string message)
    {
        _summary.Text = $"[color=#b8a8ce]{message}[/color]";
        foreach (var child in _diagnostics.GetChildren()) child.QueueFree();
        _diagnostics.AddChild(StudioStyle.Label("Aucune écriture sans aperçu approuvé.", 10, StudioStyle.Muted, true));
        _apply.Disabled = true;
        UpdateReview();
    }

    private void InvalidatePreview()
    {
        if (_updating) return;
        _session.CancelCreaturePresentationUx4E();
        _previewResult = null;
        _apply.Disabled = true;
        UpdateReview();
    }

    private void SetEditingEnabled(bool enabled)
    {
        _preview.Disabled = !enabled;
        _apply.Disabled = !enabled || !CanonicalPreviewReady;
        _undo.Disabled = !_session.CanUndo;
        _redo.Disabled = !_session.CanRedo;
        _save.Disabled = _session.Project is null;
        foreach (var control in new Control[] { _gender, _bodyType, _pronounSubject, _pronounObject,
                     _pronounPossessive, _pronounReflexive, _modelFallback, _rig, _portraitList,
                     _portraitExpression, _portraitPath, _portraitWidth, _portraitHeight, _portraitLicense,
                     _portraitHash, _portraitAlt, _bodyList, _bodySlot, _bodyAsset, _bodyLayer, _bodyHidden,
                     _bodyClipping, _colorList, _colorParameter, _colorHex, _attachmentList, _attachmentSlot,
                     _attachmentKind, _attachmentAsset }) control.MouseFilter = enabled
            ? MouseFilterEnum.Stop
            : MouseFilterEnum.Ignore;
    }

    private void ClearControls()
    {
        _updating = true;
        foreach (var field in new[] { _gender, _bodyType, _pronounSubject, _pronounObject, _pronounPossessive,
                     _pronounReflexive, _rig, _portraitExpression, _portraitPath, _portraitWidth, _portraitHeight,
                     _portraitLicense, _portraitHash, _portraitAlt, _bodySlot, _bodyAsset, _bodyLayer, _bodyHidden,
                     _bodyClipping, _colorParameter, _colorHex, _attachmentSlot, _attachmentKind,
                     _attachmentAsset }) field.Text = string.Empty;
        _modelFallback.ButtonPressed = false;
        _portraits.Clear(); _bodySlots.Clear(); _colors.Clear(); _attachments.Clear();
        _portraitList.Clear(); _bodyList.Clear(); _colorList.Clear(); _attachmentList.Clear();
        _portraitPreview.Texture = null;
        _reviewPortrait.Texture = null;
        _updating = false;
        UpdateReview();
    }

    private void ConnectEvents()
    {
        _creature.ItemSelected += SelectCreature;
        foreach (var field in new[] { _gender, _bodyType, _pronounSubject, _pronounObject, _pronounPossessive,
                     _pronounReflexive, _rig }) field.TextChanged += _ => InvalidatePreview();
        foreach (var field in new[] { _portraitExpression, _portraitPath, _portraitWidth, _portraitHeight,
                     _portraitLicense, _portraitHash, _portraitAlt, _bodySlot, _bodyAsset, _bodyLayer,
                     _bodyHidden, _bodyClipping, _colorParameter, _colorHex, _attachmentSlot,
                     _attachmentKind, _attachmentAsset }) field.TextChanged += _ => InvalidatePreview();
        _modelFallback.Toggled += _ => InvalidatePreview();
        _portraitList.ItemSelected += SelectPortrait;
        _bodyList.ItemSelected += SelectBodySlot;
        _colorList.ItemSelected += SelectColor;
        _attachmentList.ItemSelected += SelectAttachment;
        _preview.Pressed += Preview;
        _apply.Pressed += Apply;
        _undo.Pressed += Undo;
        _redo.Pressed += Redo;
        _save.Pressed += Save;
    }

    private bool CommitEditors()
    {
        if (!string.IsNullOrWhiteSpace(_portraitExpression.Text) && !SavePortrait(showStatus: false)) return false;
        if (!string.IsNullOrWhiteSpace(_bodySlot.Text) && !SaveBodySlot(showStatus: false)) return false;
        if (!string.IsNullOrWhiteSpace(_colorParameter.Text) && !SaveColor(showStatus: false)) return false;
        if (!string.IsNullOrWhiteSpace(_attachmentSlot.Text) && !SaveAttachment(showStatus: false)) return false;
        return true;
    }

    private bool SavePortrait(bool showStatus = true)
    {
        if (!int.TryParse(_portraitWidth.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) ||
            !int.TryParse(_portraitHeight.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var height))
        {
            if (showStatus) SetStatus("Largeur et hauteur du portrait doivent être des entiers.", true);
            return false;
        }
        var item = new CreaturePresentationProfile.PortraitVariant(_portraitExpression.Text, _portraitPath.Text,
            width, height, _portraitLicense.Text, _portraitHash.Text, _portraitAlt.Text);
        var selected = _portraitList.GetSelectedItems().FirstOrDefault(-1);
        if (selected >= 0 && selected < _portraits.Count) _portraits[selected] = item;
        else _portraits.Add(item);
        RebuildPortraitList(Math.Clamp(selected >= 0 ? selected : _portraits.Count - 1, 0, _portraits.Count - 1));
        InvalidatePreview();
        return true;
    }

    private void RemovePortrait()
    {
        var selected = _portraitList.GetSelectedItems().FirstOrDefault(-1);
        if (selected < 0 || selected >= _portraits.Count) return;
        _portraits.RemoveAt(selected);
        RebuildPortraitList(Math.Min(selected, _portraits.Count - 1));
        InvalidatePreview();
    }

    private void ClearPortraitEditor()
    {
        _portraitList.DeselectAll();
        foreach (var field in new[] { _portraitExpression, _portraitPath, _portraitWidth, _portraitHeight,
                     _portraitLicense, _portraitHash, _portraitAlt }) field.Text = string.Empty;
        _portraitPreview.Texture = null;
        _portraitPreviewStatus.Text = "Nouveau portrait · renseignez tous les champs.";
    }

    private void RebuildPortraitList(int selected)
    {
        _portraitList.Clear();
        foreach (var item in _portraits)
            _portraitList.AddItem($"{item.Expression} · {item.Width}×{item.Height} · {item.LicenseSpdx}");
        if (selected >= 0 && selected < _portraits.Count)
        {
            _portraitList.Select(selected);
            SelectPortrait(selected);
        }
        else ClearPortraitEditor();
    }

    private void SelectPortrait(long index)
    {
        if (index < 0 || index >= _portraits.Count) return;
        var item = _portraits[(int)index];
        _updating = true;
        _portraitExpression.Text = item.Expression;
        _portraitPath.Text = item.AssetPath;
        _portraitWidth.Text = item.Width.ToString(CultureInfo.InvariantCulture);
        _portraitHeight.Text = item.Height.ToString(CultureInfo.InvariantCulture);
        _portraitLicense.Text = item.LicenseSpdx;
        _portraitHash.Text = item.ContentSha256;
        _portraitAlt.Text = item.AltText;
        _updating = false;
        UpdatePortraitTexture(item.AssetPath, _portraitPreview, _portraitPreviewStatus);
        UpdateReview();
    }

    private bool SaveBodySlot(bool showStatus = true)
    {
        if (!int.TryParse(_bodyLayer.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var layer))
        {
            if (showStatus) SetStatus("La couche de partie du corps doit être un entier.", true);
            return false;
        }
        var item = new CreaturePresentationProfile.BodySlot(_bodySlot.Text, _bodyAsset.Text, _rig.Text, layer,
            SplitValues(_bodyHidden.Text), _bodyClipping.Text);
        var selected = _bodyList.GetSelectedItems().FirstOrDefault(-1);
        if (selected >= 0 && selected < _bodySlots.Count) _bodySlots[selected] = item;
        else _bodySlots.Add(item);
        RebuildBodyList(Math.Clamp(selected >= 0 ? selected : _bodySlots.Count - 1, 0, _bodySlots.Count - 1));
        InvalidatePreview();
        return true;
    }

    private void RemoveBodySlot()
    {
        var selected = _bodyList.GetSelectedItems().FirstOrDefault(-1);
        if (selected < 0 || selected >= _bodySlots.Count) return;
        _bodySlots.RemoveAt(selected);
        RebuildBodyList(Math.Min(selected, _bodySlots.Count - 1));
        InvalidatePreview();
    }

    private void ClearBodyEditor()
    {
        _bodyList.DeselectAll();
        foreach (var field in new[] { _bodySlot, _bodyAsset, _bodyLayer, _bodyHidden, _bodyClipping })
            field.Text = string.Empty;
    }

    private void RebuildBodyList(int selected)
    {
        _bodyList.Clear();
        foreach (var item in _bodySlots) _bodyList.AddItem($"{item.SlotId} · couche {item.Layer} · {item.AssetId}");
        if (selected >= 0 && selected < _bodySlots.Count)
        {
            _bodyList.Select(selected);
            SelectBodySlot(selected);
        }
        else ClearBodyEditor();
    }

    private void SelectBodySlot(long index)
    {
        if (index < 0 || index >= _bodySlots.Count) return;
        var item = _bodySlots[(int)index];
        _updating = true;
        _bodySlot.Text = item.SlotId;
        _bodyAsset.Text = item.AssetId;
        _bodyLayer.Text = item.Layer.ToString(CultureInfo.InvariantCulture);
        _bodyHidden.Text = string.Join(", ", item.HiddenSlots);
        _bodyClipping.Text = item.ClippingRule;
        _updating = false;
    }

    private bool SaveColor(bool showStatus = true)
    {
        _ = showStatus;
        var item = new CreaturePresentationProfile.MaterialColor(_colorParameter.Text, _colorHex.Text);
        var selected = _colorList.GetSelectedItems().FirstOrDefault(-1);
        if (selected >= 0 && selected < _colors.Count) _colors[selected] = item;
        else _colors.Add(item);
        RebuildColorList(Math.Clamp(selected >= 0 ? selected : _colors.Count - 1, 0, _colors.Count - 1));
        InvalidatePreview();
        return true;
    }

    private void RemoveColor()
    {
        var selected = _colorList.GetSelectedItems().FirstOrDefault(-1);
        if (selected < 0 || selected >= _colors.Count) return;
        _colors.RemoveAt(selected);
        RebuildColorList(Math.Min(selected, _colors.Count - 1));
        InvalidatePreview();
    }

    private void ClearColorEditor()
    {
        _colorList.DeselectAll();
        _colorParameter.Text = string.Empty;
        _colorHex.Text = string.Empty;
    }

    private void RebuildColorList(int selected)
    {
        _colorList.Clear();
        foreach (var item in _colors) _colorList.AddItem($"{item.ParameterId} · {item.HexColor}");
        if (selected >= 0 && selected < _colors.Count)
        {
            _colorList.Select(selected);
            SelectColor(selected);
        }
        else ClearColorEditor();
    }

    private void SelectColor(long index)
    {
        if (index < 0 || index >= _colors.Count) return;
        _updating = true;
        _colorParameter.Text = _colors[(int)index].ParameterId;
        _colorHex.Text = _colors[(int)index].HexColor;
        _updating = false;
    }

    private bool SaveAttachment(bool showStatus = true)
    {
        _ = showStatus;
        var item = new CreaturePresentationProfile.Attachment(_attachmentSlot.Text, _attachmentKind.Text,
            _attachmentAsset.Text, _rig.Text);
        var selected = _attachmentList.GetSelectedItems().FirstOrDefault(-1);
        if (selected >= 0 && selected < _attachments.Count) _attachments[selected] = item;
        else _attachments.Add(item);
        RebuildAttachmentList(Math.Clamp(selected >= 0 ? selected : _attachments.Count - 1, 0, _attachments.Count - 1));
        InvalidatePreview();
        return true;
    }

    private void RemoveAttachment()
    {
        var selected = _attachmentList.GetSelectedItems().FirstOrDefault(-1);
        if (selected < 0 || selected >= _attachments.Count) return;
        _attachments.RemoveAt(selected);
        RebuildAttachmentList(Math.Min(selected, _attachments.Count - 1));
        InvalidatePreview();
    }

    private void ClearAttachmentEditor()
    {
        _attachmentList.DeselectAll();
        foreach (var field in new[] { _attachmentSlot, _attachmentKind, _attachmentAsset }) field.Text = string.Empty;
    }

    private void RebuildAttachmentList(int selected)
    {
        _attachmentList.Clear();
        foreach (var item in _attachments) _attachmentList.AddItem($"{AttachmentKindLabel(item.Kind)} · {item.SlotId} · {item.AssetId}");
        if (selected >= 0 && selected < _attachments.Count)
        {
            _attachmentList.Select(selected);
            SelectAttachment(selected);
        }
        else ClearAttachmentEditor();
    }

    private void SelectAttachment(long index)
    {
        if (index < 0 || index >= _attachments.Count) return;
        var item = _attachments[(int)index];
        _updating = true;
        _attachmentSlot.Text = item.SlotId;
        _attachmentKind.Text = item.Kind;
        _attachmentAsset.Text = item.AssetId;
        _updating = false;
    }

    private void UpdatePortraitTexture(string path, TextureRect target, Label? status = null)
    {
        target.Texture = null;
        var absolute = _session.ResolveCreaturePortraitAbsolutePathUx4E(path);
        if (absolute is null)
        {
            if (status is not null) status.Text = "Fichier indisponible · métadonnées conservées, aucun faux aperçu.";
            return;
        }
        var image = Image.LoadFromFile(absolute);
        if (image is null || image.IsEmpty())
        {
            if (status is not null) status.Text = "Image illisible · métadonnées conservées.";
            return;
        }
        target.Texture = ImageTexture.CreateFromImage(image);
        if (status is not null) status.Text = $"Aperçu chargé · {image.GetWidth()}×{image.GetHeight()} px";
    }

    private void UpdateReview()
    {
        var portrait = _portraits.FirstOrDefault(item => item.Expression == "neutral") ?? _portraits.FirstOrDefault();
        if (portrait is not null) UpdatePortraitTexture(portrait.AssetPath, _reviewPortrait);
        else _reviewPortrait.Texture = null;
        _reviewText.Text = _draft is null
            ? "Aucune créature sélectionnée · aucun asset chargé."
            : $"{_choices.ElementAtOrDefault(Math.Max(0, _creature.Selected - 1))?.Label ?? "Créature sélectionnée"}\n" +
              $"{_gender.Text} · {_bodyType.Text} · {_rig.Text}\n" +
              $"{_portraits.Count} portrait(s) · {_bodySlots.Count} partie(s) · {_colors.Count} couleur(s) · " +
              $"{_attachments.Count} attachement(s)\n" +
              "Aperçu éditorial/UI uniquement · aucun modèle 3D instancié.";
    }

    internal bool PrepareEvidence(string mode)
    {
        _lastFailure = "none";
        _previewResult = null;
        if ((_evidenceSession is not null || _evidenceRoot is not null) && !ReleaseEvidence())
        {
            _lastFailure = "release_previous";
            return false;
        }
        if (!TryOpenEvidence())
        {
            _lastFailure = _lastFailure == "none" ? "open" : _lastFailure;
            return false;
        }
        var index = _choices.ToList().FindIndex(item => item.BlueprintId == _evidenceBlueprintId);
        if (index < 0)
        {
            _lastFailure = "fixture_creature_missing";
            Close();
            return false;
        }
        _creature.Select(index + 1);
        SelectCreature(index + 1);
        if (_draft is null)
        {
            _lastFailure = "draft";
            Close();
            return false;
        }
        _gender.Text = "nonbinary";
        _pronounSubject.Text = "iel";
        _pronounObject.Text = "iel";
        _pronounPossessive.Text = "son";
        _pronounReflexive.Text = "soi-même";
        if (_attachments.All(item => item.Kind != "tail"))
        {
            ClearAttachmentEditor();
            _attachmentSlot.Text = "tail-base";
            _attachmentKind.Text = "tail";
            _attachmentAsset.Text = "asset.tail";
            SaveAttachment();
        }
        if (_colors.All(item => item.ParameterId != "secondary"))
        {
            ClearColorEditor();
            _colorParameter.Text = "secondary";
            _colorHex.Text = "#7A5CFF";
            SaveColor();
        }
        var project = _session.Project!;
        var history = _session.History!;
        var revision = history.State.Revision;
        var journal = history.Journal.ToArray();
        var dirty = _session.IsDirty;
        Preview();
        _previewDryRun = ReferenceEquals(project, _session.Project) && ReferenceEquals(history, _session.History) &&
                         history.State.Revision == revision && history.Journal.SequenceEqual(journal) &&
                         _session.IsDirty == dirty && EvidenceSourceUntouched;
        _summary.Text = $"[b]{(_usesRealPresentation ? "CAPTURE RÉELLE PRÉSENTATION" : "FALLBACK CANONIQUE TEMPORAIRE")}[/b] · " +
                        $"copie isolée, source strictement inchangée.\n{_summary.Text}";
        _tabs.CurrentTab = mode.Contains("portrait", StringComparison.OrdinalIgnoreCase) ? 1 :
            mode.Contains("appearance", StringComparison.OrdinalIgnoreCase) || mode.Contains("body", StringComparison.OrdinalIgnoreCase) ? 2 :
            mode.Contains("color", StringComparison.OrdinalIgnoreCase) || mode.Contains("attachment", StringComparison.OrdinalIgnoreCase) ? 3 :
            mode.Contains("preview", StringComparison.OrdinalIgnoreCase) ? 4 : 0;
        var ready = _previewDryRun && CanonicalPreviewReady && HasExplicitSelection && ShowsHumanSurface &&
                    ShowsGenderAndPronouns && ShowsPortraits && ShowsAppearanceAndModel && ShowsBodyParts &&
                    ShowsColorsWingsTails && PortraitPreviewReady && UsesIsolatedEvidence && EvidenceSourceUntouched &&
                    (_usesRealPresentation ^ _usesFallback);
        if (!ready)
        {
            _lastFailure =
                $"ready:preview={CanonicalPreviewReady}:selection={HasExplicitSelection}:human={ShowsHumanSurface}:" +
                $"gender={ShowsGenderAndPronouns}:portraits={ShowsPortraits}:appearance={ShowsAppearanceAndModel}:" +
                $"body={ShowsBodyParts}:attachments={ShowsColorsWingsTails}:image={PortraitPreviewReady}:" +
                $"isolated={UsesIsolatedEvidence}:source={EvidenceSourceUntouched}:origin={_usesRealPresentation ^ _usesFallback}";
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4E_EVIDENCE_FAILED failure={_lastFailure}");
            Close();
            return false;
        }
        SetStatus("Preuve UX4-E isolée prête · aucune donnée source modifiée.", false);
        return true;
    }

    internal bool RunUiTransactionEvidence()
    {
        var snapshot = CaptureSource();
        if (snapshot is null) return false;
        StudioSessionController? reopened = null;
        try
        {
            if (!PrepareEvidence("ux4-e-creature-presentation") || _evidenceSession is null ||
                !ReferenceEquals(_session, _evidenceSession) || !CanonicalPreviewReady) return false;
            var transaction = _session;
            var original = transaction.Project;
            var history = transaction.History;
            if (original is null || history is null) return false;
            Apply();
            var applied = _applied && transaction.Project is { } appliedProject && !ReferenceEquals(original, appliedProject);
            if (applied) Undo();
            var undone = applied && ReferenceEquals(original, transaction.Project);
            if (undone) Redo();
            var redoneProject = transaction.Project;
            var redone = undone && redoneProject is not null && !ReferenceEquals(original, redoneProject);
            var saved = redone && transaction.Save().Succeeded;
            var persisted = false;
            if (saved && transaction.Session is { } workspace)
            {
                reopened = new StudioSessionController();
                persisted = reopened.Open(workspace.RootDirectory).Succeeded &&
                            ProjectJsonSerializer.Serialize(reopened.Project!) ==
                            ProjectJsonSerializer.Serialize(redoneProject!);
            }
            var sourceUntouched = SourceMatches(snapshot);
            reopened?.Close();
            reopened = null;
            Close();
            return applied && undone && redone && saved && persisted && sourceUntouched &&
                   EvidenceResourcesReleased && SourceSessionRestored;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4E_UI_TRANSACTION_FAILED type={exception.GetType().Name} message={exception.Message}");
            return false;
        }
        finally
        {
            reopened?.Close();
            if (_evidenceSession is not null || _evidenceRoot is not null) Close();
            _sourceSession.RestoreCreaturePresentationApprovalUx4E(snapshot.Approval);
        }
    }

    internal void CloseEvidence() => Close();

    private bool TryOpenEvidence()
    {
        var snapshot = CaptureSource();
        var evidence = _sourceSession.CreateUx4EEvidenceProject();
        if (snapshot is null || evidence is null) return false;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux4e-evidence-" + Guid.NewGuid().ToString("N"));
        var session = new StudioSessionController();
        try
        {
            if (!ProjectSession.Create(root, evidence.Project).Succeeded ||
                !StudioSessionController.WriteEvidencePortraitUx4E(root, evidence.PortraitAssetPath, evidence.PortraitBytes) ||
                !session.Open(root).Succeeded)
            {
                session.Close();
                _resourcesReleased = DeleteEvidenceRoot(root);
                return false;
            }
            _sourceSnapshot = snapshot;
            _evidenceSession = session;
            _evidenceRoot = root;
            _evidenceBlueprintId = evidence.BlueprintId;
            _session = session;
            _usesRealPresentation = evidence.UsesRealPresentation;
            _usesFallback = evidence.UsesCanonicalFallback;
            _previewDryRun = false;
            _resourcesReleased = false;
            _sourceRestored = false;
            OpenBoundSession();
            return SourceMatches(snapshot) && (_usesRealPresentation ^ _usesFallback);
        }
        catch (Exception exception)
        {
            _lastFailure = "open_exception:" + exception.GetType().Name;
            session.Close();
            _resourcesReleased = DeleteEvidenceRoot(root);
            _session = _sourceSession;
            _sourceRestored = true;
            return false;
        }
    }

    private StudioCreaturePresentationEvidenceSourceSnapshotUx4E? CaptureSource()
    {
        var project = _sourceSession.Project;
        var history = _sourceSession.History;
        return project is null || history is null ? null : new(project, history, history.State.Revision,
            history.Journal.ToArray(), _sourceSession.IsDirty, history.CanUndo, history.CanRedo,
            _sourceSession.ActiveAreaId, _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureCreaturePresentationApprovalUx4E());
    }

    private bool SourceMatches(StudioCreaturePresentationEvidenceSourceSnapshotUx4E snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) && ReferenceEquals(snapshot.History, history) &&
               history is not null && history.State.Revision == snapshot.Revision &&
               history.Journal.SequenceEqual(snapshot.Journal) && history.CanUndo == snapshot.CanUndo &&
               history.CanRedo == snapshot.CanRedo && _sourceSession.IsDirty == snapshot.IsDirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureCreaturePresentationApprovalUx4E() == snapshot.Approval;
    }

    private bool ReleaseEvidence()
    {
        var session = _evidenceSession;
        var root = _evidenceRoot;
        _evidenceSession = null;
        _evidenceRoot = null;
        _sourceSnapshot = null;
        _evidenceBlueprintId = null;
        _session = _sourceSession;
        _usesRealPresentation = false;
        _usesFallback = false;
        _previewDryRun = false;
        var released = true;
        try { session?.Close(); } catch { released = false; }
        var rootReleased = DeleteEvidenceRoot(root);
        if (!rootReleased) _evidenceRoot = root;
        _resourcesReleased = released && rootReleased;
        _sourceRestored = true;
        return _resourcesReleased;
    }

    private static bool DeleteEvidenceRoot(string? root)
    {
        if (string.IsNullOrWhiteSpace(root)) return true;
        try
        {
            var full = Path.GetFullPath(root);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) +
                       Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4e-evidence-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full) && !File.Exists(full);
        }
        catch
        {
            return false;
        }
    }

    private static HBoxContainer CollectionButtons(
        string newLabel,
        Func<bool> save,
        Action remove,
        Action clear)
    {
        var row = new HBoxContainer();
        var create = StudioStyle.Button(newLabel, compact: true);
        var update = StudioStyle.Button("Mettre à jour", compact: true);
        var delete = StudioStyle.Button("Retirer", compact: true);
        create.Pressed += clear;
        update.Pressed += () => save();
        delete.Pressed += remove;
        row.AddChild(create);
        row.AddChild(update);
        row.AddChild(delete);
        return row;
    }

    private static HBoxContainer CollectionButtons(
        string newLabel,
        Func<bool, bool> save,
        Action remove,
        Action clear) => CollectionButtons(newLabel, () => save(true), remove, clear);

    private static VBoxContainer Tab(TabContainer tabs, string title)
    {
        var scroll = new ScrollContainer
        {
            Name = title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
        };
        tabs.AddChild(scroll);
        var margin = StudioStyle.Margin(8, 8, 10, 10);
        margin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(margin);
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 6);
        margin.AddChild(content);
        return content;
    }

    private static LineEdit Field(VBoxContainer parent, string label, string placeholder)
    {
        parent.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        var field = new LineEdit { PlaceholderText = placeholder, ClearButtonEnabled = true };
        StudioStyle.ApplyLineEdit(field);
        parent.AddChild(field);
        return field;
    }

    private static LineEdit GridField(GridContainer parent, string label, string placeholder)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        var field = new LineEdit { PlaceholderText = placeholder, ClearButtonEnabled = true };
        StudioStyle.ApplyLineEdit(field);
        box.AddChild(field);
        parent.AddChild(box);
        return field;
    }

    private static IReadOnlyList<string> SplitValues(string value) => value
        .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.Ordinal).ToArray();

    private static string AttachmentKindLabel(string kind) => kind switch
    {
        "wings" => "Ailes",
        "tail" => "Queue",
        "horns" => "Cornes",
        _ => kind,
    };

    private void SetStatus(string message, bool error) => StatusChanged?.Invoke(message, error);

    private static IEnumerable<string> VisibleText(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            switch (child)
            {
                case Label label when label.Visible: yield return label.Text; break;
                case Button button when button.Visible: yield return button.Text; break;
                case RichTextLabel rich when rich.Visible: yield return rich.Text; break;
                case LineEdit line when line.Visible: yield return line.Text; break;
                case OptionButton option when option.Visible && option.Selected >= 0:
                    yield return option.GetItemText(option.Selected);
                    break;
                case TabContainer tabs when tabs.Visible:
                    for (var index = 0; index < tabs.GetTabCount(); index++) yield return tabs.GetTabTitle(index);
                    break;
            }
            foreach (var value in VisibleText(child)) yield return value;
        }
    }

    private static bool ContainsGuid(string value)
    {
        foreach (var token in value.Split([' ', '\r', '\n', '\t', '/', ':', '(', ')', '[', ']', ','],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (Guid.TryParse(token.Trim('.', ';'), out _)) return true;
        }
        return false;
    }
}
