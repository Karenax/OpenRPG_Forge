using Forge.Application.Areas;
using Forge.Domain.RuntimePolicies;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioAreaLoadingOverlay : Control
{
    private readonly StudioSessionController _session;
    private readonly PanelContainer _dialog;
    private readonly Label _area;
    private readonly LineEdit _title;
    private readonly OptionButton _image;
    private readonly OptionButton _video;
    private readonly TextEdit _frenchTips;
    private readonly TextEdit _englishTips;
    private readonly TextEdit _accessibleFallback;
    private readonly CheckBox _showProgress;
    private readonly CheckBox _prefetch;
    private readonly SpinBox _minimumVisible;
    private readonly Label _streamingResources;
    private readonly OptionButton _previewMode;
    private readonly Label _previewMedia;
    private readonly TextureRect _previewImage;
    private readonly Label _previewEmpty;
    private readonly Label _previewTitle;
    private readonly Label _previewTip;
    private readonly Label _previewAccessible;
    private readonly ProgressBar _previewProgress;
    private readonly Label _summary;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Label _status;
    private StudioAreaLoadingCatalog? _catalog;
    private StudioAreaLoadingDraft? _draft;
    private StudioAreaLoadingPreview? _result;
    private bool _updating;
    private bool _applied;

    internal event Action<string, bool>? StatusChanged;

    internal event Action? AreaChanged;

    internal bool IsOpen => Visible;

    internal int PreviewModeCount => 2;

    internal bool ShowsHumanSurface
    {
        get
        {
            var optionLabels = Enumerable.Range(0, _image.ItemCount)
                .Select(index => _image.GetItemText(index))
                .Concat(Enumerable.Range(0, _video.ItemCount).Select(index => _video.GetItemText(index)));
            var text = string.Join(' ', optionLabels.Append(_area.Text).Append(_summary.Text).Append(_status.Text)
                .Append(_streamingResources.Text).Append(_previewMedia.Text));
            return PreviewModeCount == 2 &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains(".json", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("project.json", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("/", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("\\", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal StudioAreaLoadingOverlay(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Name = "StudioAreaLoadingOverlay";
        Visible = false;
        ZIndex = 440;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect
        {
            Color = new Color(0, 0, 0, .78f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        _dialog = StudioStyle.Panel("AreaLoadingDialog", StudioStyle.Chrome, 12, 2);
        AddChild(_dialog);
        var margin = StudioStyle.Margin(18, 14, 18, 14);
        _dialog.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 9);
        margin.AddChild(root);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        root.AddChild(header);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("ÉCRAN DE CHARGEMENT", 10, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Présenter la prochaine zone avec clarté", 20));
        heading.AddChild(StudioStyle.Label(
            "Aperçu normal et mouvement réduit · progression réelle · repli accessible",
            10,
            StudioStyle.Muted));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.TooltipText = "Fermer sans appliquer la présentation en attente · Échap";
        close.Pressed += Close;
        header.AddChild(close);

        _area = StudioStyle.Label(string.Empty, 12, StudioStyle.Success, wrap: true);
        root.AddChild(_area);

        var body = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        body.AddThemeConstantOverride("separation", 12);
        root.AddChild(body);

        var formScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(500, 0),
        };
        body.AddChild(formScroll);
        var form = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 8);
        formScroll.AddChild(form);
        form.AddChild(StudioStyle.SectionLabel("CONTENU VISIBLE"));
        _title = AddField(form, "Titre", Edit("Titre affiché pendant le chargement"));
        _image = AddField(form, "Image de repli", Option());
        _image.TooltipText = "Choisir une image déjà importée dans le module, sans saisir de chemin.";
        _video = AddField(form, "Vidéo", Option());
        _video.TooltipText = "La vidéo est utilisée en mode normal ; l’image prend le relais si les mouvements sont réduits.";
        _frenchTips = AddField(form, "Astuces françaises · une par ligne", TipsEdit());
        _englishTips = AddField(form, "Astuces anglaises · une par ligne", TipsEdit());
        _accessibleFallback = AddField(form, "Description accessible", TipsEdit(66));
        _accessibleFallback.TooltipText = "Texte annoncé lorsque le média est absent ou désactivé.";

        form.AddChild(StudioStyle.SectionLabel("PROGRESSION ET STREAMING"));
        _showProgress = new CheckBox { Text = "Afficher la progression réelle", ButtonPressed = true };
        _showProgress.TooltipText = "Relier la jauge aux ressources réellement terminées par le streaming.";
        form.AddChild(_showProgress);
        _prefetch = new CheckBox { Text = "Précharger les ressources déclarées", ButtonPressed = true };
        _prefetch.TooltipText = "Utiliser le profil de streaming canonique de cette zone pendant la présentation.";
        form.AddChild(_prefetch);
        _minimumVisible = AddField(form, "Durée visible minimale", DurationBox());
        _streamingResources = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted, wrap: true);
        form.AddChild(_streamingResources);

        var previewPanel = StudioStyle.Panel("LoadingPreview", new Color("0a0c0a"), 10, 1);
        previewPanel.CustomMinimumSize = new Vector2(420, 0);
        previewPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        body.AddChild(previewPanel);
        var previewMargin = StudioStyle.Margin(14, 12, 14, 12);
        previewPanel.AddChild(previewMargin);
        var previewColumn = new VBoxContainer();
        previewColumn.AddThemeConstantOverride("separation", 8);
        previewMargin.AddChild(previewColumn);
        var previewHeading = new HBoxContainer();
        previewHeading.AddChild(StudioStyle.SectionLabel("APERÇU JOUEUR"));
        previewHeading.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _previewMode = Option("Mode normal", "Mouvements réduits");
        _previewMode.CustomMinimumSize = new Vector2(190, StudioStyle.CompactControlHeight);
        _previewMode.TooltipText = "Comparer le média animé et son alternative sans mouvement.";
        _previewMode.ItemSelected += _ => RenderPreviewFrame();
        previewHeading.AddChild(_previewMode);
        previewColumn.AddChild(previewHeading);

        _previewMedia = StudioStyle.Label("REPLI ACCESSIBLE", 9, StudioStyle.Accent);
        previewColumn.AddChild(_previewMedia);
        var mediaFrame = StudioStyle.Panel("LoadingMediaFrame", new Color("111711"), 8, 1);
        mediaFrame.CustomMinimumSize = new Vector2(0, 220);
        previewColumn.AddChild(mediaFrame);
        var mediaStack = new Control { CustomMinimumSize = new Vector2(0, 220) };
        mediaFrame.AddChild(mediaStack);
        _previewImage = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _previewImage.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        mediaStack.AddChild(_previewImage);
        _previewEmpty = StudioStyle.Label(
            "Aucun média sélectionné\nLe texte accessible reste présenté.",
            12,
            StudioStyle.Muted,
            wrap: true);
        _previewEmpty.HorizontalAlignment = HorizontalAlignment.Center;
        _previewEmpty.VerticalAlignment = VerticalAlignment.Center;
        _previewEmpty.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        mediaStack.AddChild(_previewEmpty);

        _previewTitle = StudioStyle.Label("Chargement", 22, StudioStyle.Text, wrap: true);
        _previewTitle.HorizontalAlignment = HorizontalAlignment.Center;
        previewColumn.AddChild(_previewTitle);
        _previewTip = StudioStyle.Label("Prévisualisez pour afficher une astuce localisée.", 11, StudioStyle.Accent, wrap: true);
        _previewTip.HorizontalAlignment = HorizontalAlignment.Center;
        previewColumn.AddChild(_previewTip);
        _previewAccessible = StudioStyle.Label(
            "Le repli accessible apparaîtra ici.",
            9,
            StudioStyle.Muted,
            wrap: true);
        _previewAccessible.HorizontalAlignment = HorizontalAlignment.Center;
        previewColumn.AddChild(_previewAccessible);
        _previewProgress = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Value = .5,
            ShowPercentage = true,
        };
        _previewProgress.AddThemeStyleboxOverride("background", StudioStyle.Box(StudioStyle.Surface, StudioStyle.Border, 4));
        _previewProgress.AddThemeStyleboxOverride("fill", StudioStyle.Box(StudioStyle.Success, StudioStyle.Success, 4));
        _previewProgress.AddThemeColorOverride("font_color", StudioStyle.Text);
        previewColumn.AddChild(_previewProgress);

        _summary = StudioStyle.Label(
            "Modifiez la présentation puis demandez un aperçu avant de l’appliquer.",
            10,
            StudioStyle.Muted,
            wrap: true);
        previewColumn.AddChild(_summary);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 7);
        root.AddChild(footer);
        _preview = StudioStyle.Button("1 · Prévisualiser", compact: true);
        _preview.TooltipText = "Valider la commande en lecture seule et afficher le résultat joueur.";
        _preview.Pressed += Preview;
        footer.AddChild(_preview);
        _apply = StudioStyle.Button("2 · Appliquer", primary: true, compact: true);
        _apply.Disabled = true;
        _apply.TooltipText = "Appliquer la commande canonique en mémoire ; l’enregistrement reste séparé.";
        _apply.Pressed += Apply;
        footer.AddChild(_apply);
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _undo = StudioStyle.Button("Annuler l’écran", compact: true);
        _undo.Disabled = true;
        _undo.Pressed += Undo;
        footer.AddChild(_undo);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _redo.Disabled = true;
        _redo.Pressed += Redo;
        footer.AddChild(_redo);

        _status = StudioStyle.Label("Prêt · aucun changement appliqué", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        ConnectInvalidation();
        Resized += UpdateDialogBounds;
        UpdateDialogBounds();
    }

    internal void Open()
    {
        _draft = _session.CreateAreaLoadingDraft();
        _catalog = _session.CreateAreaLoadingCatalog();
        _result = null;
        _applied = false;
        LoadDraft();
        SetEditingEnabled(true);
        _preview.Disabled = false;
        _apply.Disabled = true;
        _undo.Disabled = true;
        _redo.Disabled = true;
        Visible = true;
        SetStatus("Outil ouvert · aperçu obligatoire avant application", false);
        _title.GrabFocus();
    }

    internal void Close()
    {
        if (!Visible)
        {
            return;
        }
        _session.CancelAreaLoading();
        _draft = null;
        _catalog = null;
        _result = null;
        Visible = false;
    }

    internal bool PrepareEvidence(string mode)
    {
        Open();
        _updating = true;
        _title.Text = "Passage vers la Forêt oubliée";
        _frenchTips.Text = "Écoutez les corbeaux : ils signalent les chemins sûrs.\nLes pierres moussues bordent les anciens sentiers.";
        _englishTips.Text = "Listen to the ravens before leaving the trail.";
        _accessibleFallback.Text = "Préparation de la forêt, des rencontres et du chemin praticable.";
        _showProgress.ButtonPressed = true;
        _prefetch.ButtonPressed = true;
        _minimumVisible.Value = 650;
        SelectFirstAvailable(_image);
        if (mode.EndsWith("fallback", StringComparison.OrdinalIgnoreCase))
        {
            _image.Selected = 0;
            _video.Selected = 0;
            _previewMode.Selected = 1;
        }
        else if (mode.EndsWith("reduced", StringComparison.OrdinalIgnoreCase))
        {
            _previewMode.Selected = 1;
        }
        else
        {
            _previewMode.Selected = 0;
        }
        _updating = false;
        Preview();
        return Visible && !_apply.Disabled && ShowsHumanSurface && _result?.Succeeded == true;
    }

    internal bool RunUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        if (originalProject is null || originalAreaId is null || _session.ActiveArea is null)
        {
            return false;
        }

        Open();
        _updating = true;
        _title.Text = "Preuve interface UX1-C";
        _frenchTips.Text = "Une astuce visible et localisée.";
        _accessibleFallback.Text = "Préparation accessible de la zone.";
        _showProgress.ButtonPressed = true;
        _prefetch.ButtonPressed = true;
        SelectFirstAvailable(_image);
        _updating = false;
        Preview();
        var previewed = !_apply.Disabled && _result?.StandardFrame is not null;
        Apply();
        var applied = !ReferenceEquals(originalProject, _session.Project) && !_undo.Disabled;
        Undo();
        var undone = ReferenceEquals(originalProject, _session.Project) && !_redo.Disabled;
        Redo();
        var redone = !ReferenceEquals(originalProject, _session.Project) && !_undo.Disabled;
        Undo();
        var restored = ReferenceEquals(originalProject, _session.Project) && !_session.IsDirty &&
                       _session.ActiveAreaId == originalAreaId;
        Close();
        return previewed && applied && undone && redone && restored && ShowsHumanSurface;
    }

    private void LoadDraft()
    {
        var draft = _draft!;
        var catalog = _catalog!;
        var area = _session.ActiveArea!;
        _updating = true;
        _area.Text = $"Zone active · {area.DisplayName} · présentation indépendante de ses objets et de son terrain";
        _title.Text = draft.Title;
        PopulateMediaOptions(_image, catalog.Images, draft.ImageResource);
        PopulateMediaOptions(_video, catalog.Videos, draft.VideoResource);
        _frenchTips.Text = string.Join('\n', draft.FrenchTips);
        _englishTips.Text = string.Join('\n', draft.EnglishTips);
        _accessibleFallback.Text = draft.AccessibleFallback;
        _showProgress.ButtonPressed = draft.ShowProgress;
        _prefetch.ButtonPressed = draft.PrefetchRequiredResources;
        _minimumVisible.Value = draft.MinimumVisibleMilliseconds;
        _streamingResources.Text = catalog.RequiredResourceCount == 0
            ? "État vide · aucune ressource de streaming déclarée pour cette zone ; la présentation reste instantanée."
            : $"Streaming réel · {catalog.RequiredResourceCount} ressource(s) : " +
              string.Join(", ", catalog.RequiredResourceLabels.Take(4)) +
              (catalog.RequiredResourceCount > 4 ? "…" : string.Empty);
        _streamingResources.AddThemeColorOverride(
            "font_color",
            catalog.RequiredResourceCount == 0 ? StudioStyle.Warning : StudioStyle.Success);
        _previewMode.Selected = 0;
        _updating = false;
        InvalidatePreview();
    }

    private StudioAreaLoadingDraft ReadDraft() => _draft! with
    {
        Title = _title.Text,
        ImageResource = SelectedResource(_image, _catalog!.Images),
        VideoResource = SelectedResource(_video, _catalog.Videos),
        FrenchTips = ReadTips(_frenchTips),
        EnglishTips = ReadTips(_englishTips),
        AccessibleFallback = _accessibleFallback.Text,
        ShowProgress = _showProgress.ButtonPressed,
        PrefetchRequiredResources = _prefetch.ButtonPressed,
        MinimumVisibleMilliseconds = (int)_minimumVisible.Value,
    };

    private void Preview()
    {
        _result = _session.PreviewAreaLoading(ReadDraft());
        _summary.Text = string.IsNullOrWhiteSpace(_result.Summary)
            ? "Aucun aperçu validé. Corrigez les champs signalés puis recommencez."
            : _result.Summary;
        _summary.AddThemeColorOverride(
            "font_color",
            _result.Succeeded ? StudioStyle.Text : StudioStyle.Danger);
        _apply.Disabled = !_result.CanApply;
        RenderPreviewFrame();
        SetStatus(_result.Message, !_result.Succeeded);
    }

    private void RenderPreviewFrame()
    {
        var frame = _previewMode.Selected == 1 ? _result?.ReducedMotionFrame : _result?.StandardFrame;
        if (frame is null)
        {
            _previewMedia.Text = "APERÇU À CALCULER";
            _previewImage.Texture = null;
            _previewImage.Visible = false;
            _previewEmpty.Visible = true;
            _previewEmpty.Text = "Prévisualisez pour voir le rendu joueur.";
            _previewTitle.Text = string.IsNullOrWhiteSpace(_title.Text) ? "Chargement" : _title.Text;
            _previewTip.Text = "Aucune astuce prévisualisée.";
            _previewAccessible.Text = "Le repli accessible apparaîtra ici.";
            _previewProgress.Visible = _showProgress.ButtonPressed;
            _previewProgress.Value = .5;
            return;
        }

        _previewTitle.Text = frame.Title;
        _previewTip.Text = frame.Tip;
        _previewAccessible.Text = frame.AccessibleText;
        _previewProgress.Visible = frame.ShowProgress;
        _previewProgress.Value = Math.Clamp(frame.Progress, 0, 1);
        _previewImage.Texture = null;
        _previewImage.Visible = false;
        _previewEmpty.Visible = true;
        switch (frame.MediaKind)
        {
            case LoadingMediaKind.Image:
                _previewMedia.Text = _previewMode.Selected == 1 ? "IMAGE · MOUVEMENTS RÉDUITS" : "IMAGE";
                if (LoadPreviewTexture(frame.MediaResource) is { } texture)
                {
                    _previewImage.Texture = texture;
                    _previewImage.Visible = true;
                    _previewEmpty.Visible = false;
                }
                else
                {
                    _previewEmpty.Text = "Image indisponible\nLe repli accessible sera utilisé.";
                }
                break;
            case LoadingMediaKind.Video:
                _previewMedia.Text = "VIDÉO · LECTURE AU RUNTIME";
                _previewEmpty.Text = "Vidéo sélectionnée\nL’image de repli remplace l’animation si nécessaire.";
                break;
            default:
                _previewMedia.Text = "REPLI ACCESSIBLE";
                _previewEmpty.Text = "Présentation sans média\nLisible avec ou sans animation.";
                break;
        }
    }

    private Texture2D? LoadPreviewTexture(string resource)
    {
        var path = _session.ResolveLoadingMediaAbsolutePath(resource);
        if (path is null)
        {
            return null;
        }
        var image = Image.LoadFromFile(path);
        return image.IsEmpty() ? null : ImageTexture.CreateFromImage(image);
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedAreaLoading();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded)
        {
            _apply.Disabled = true;
            return;
        }

        _applied = true;
        SetEditingEnabled(false);
        _preview.Disabled = true;
        _apply.Disabled = true;
        _undo.Disabled = false;
        _redo.Disabled = true;
        AreaChanged?.Invoke();
    }

    private void Undo()
    {
        var result = _session.Undo();
        SetStatus(
            result.Succeeded ? "Écran de chargement annulé · la présentation précédente est restaurée." : result.Message,
            !result.Succeeded);
        if (result.Succeeded)
        {
            _undo.Disabled = true;
            _redo.Disabled = false;
            AreaChanged?.Invoke();
        }
    }

    private void Redo()
    {
        var result = _session.Redo();
        SetStatus(
            result.Succeeded ? "Écran de chargement rétabli · enregistrez pour écrire le document." : result.Message,
            !result.Succeeded);
        if (result.Succeeded)
        {
            _undo.Disabled = false;
            _redo.Disabled = true;
            AreaChanged?.Invoke();
        }
    }

    private void ConnectInvalidation()
    {
        _title.TextChanged += _ => InvalidatePreview();
        _image.ItemSelected += _ => InvalidatePreview();
        _video.ItemSelected += _ => InvalidatePreview();
        _frenchTips.TextChanged += InvalidatePreview;
        _englishTips.TextChanged += InvalidatePreview;
        _accessibleFallback.TextChanged += InvalidatePreview;
        _showProgress.Toggled += _ => InvalidatePreview();
        _prefetch.Toggled += _ => InvalidatePreview();
        _minimumVisible.ValueChanged += _ => InvalidatePreview();
    }

    private void InvalidatePreview()
    {
        if (_updating || _applied)
        {
            return;
        }
        _session.CancelAreaLoading();
        _result = null;
        _apply.Disabled = true;
        _preview.Disabled = false;
        _summary.Text = "Aperçu à recalculer · aucun changement appliqué.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        RenderPreviewFrame();
    }

    private void SetEditingEnabled(bool enabled)
    {
        _title.Editable = enabled;
        _image.Disabled = !enabled;
        _video.Disabled = !enabled;
        _frenchTips.Editable = enabled;
        _englishTips.Editable = enabled;
        _accessibleFallback.Editable = enabled;
        _showProgress.Disabled = !enabled;
        _prefetch.Disabled = !enabled;
        _minimumVisible.Editable = enabled;
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Success);
        StatusChanged?.Invoke(message, error);
    }

    private void UpdateDialogBounds()
    {
        var scale = Size.X >= 3200 ? 1.75f : Size.X >= 2300 ? 1.25f : 1f;
        var width = Math.Clamp((Size.X - 56) / scale, 920, 1220);
        var height = Math.Clamp((Size.Y - 62) / scale, 650, 820);
        var renderedWidth = width * scale;
        var renderedHeight = height * scale;
        _dialog.Scale = Vector2.One * scale;
        _dialog.Position = new Vector2(
            (Size.X - renderedWidth) * .5f,
            Math.Max(20, (Size.Y - renderedHeight) * .45f));
        _dialog.Size = new Vector2(width, height);
    }

    private static T AddField<T>(VBoxContainer parent, string label, T control) where T : Control
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        parent.AddChild(control);
        return control;
    }

    private static LineEdit Edit(string placeholder)
    {
        var edit = new LineEdit
        {
            PlaceholderText = placeholder,
            ClearButtonEnabled = true,
        };
        StudioStyle.ApplyLineEdit(edit);
        return edit;
    }

    private static TextEdit TipsEdit(float height = 82)
    {
        var edit = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, height),
            WrapMode = TextEdit.LineWrappingMode.Boundary,
            PlaceholderText = "Une phrase claire par ligne",
        };
        edit.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.Surface, StudioStyle.Border, 5));
        edit.AddThemeStyleboxOverride("focus", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Accent, 5, 2));
        edit.AddThemeColorOverride("font_color", StudioStyle.Text);
        edit.AddThemeColorOverride("font_placeholder_color", StudioStyle.Muted);
        edit.AddThemeColorOverride("caret_color", StudioStyle.Accent);
        edit.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
        return edit;
    }

    private static OptionButton Option(params string[] labels)
    {
        var option = new OptionButton();
        foreach (var label in labels)
        {
            option.AddItem(label);
        }
        StudioStyle.ApplyOptionButton(option);
        return option;
    }

    private static SpinBox DurationBox()
    {
        var spin = new SpinBox
        {
            MinValue = 0,
            MaxValue = 30_000,
            Step = 50,
            Suffix = " ms",
        };
        StudioStyle.ApplySpinBox(spin);
        return spin;
    }

    private static void PopulateMediaOptions(
        OptionButton option,
        IReadOnlyList<StudioLoadingMediaOption> values,
        string selectedResource)
    {
        option.Clear();
        var selected = 0;
        for (var index = 0; index < values.Count; index++)
        {
            option.AddItem(values[index].DisplayName);
            if (!values[index].Available)
            {
                option.SetItemDisabled(index, true);
            }
            if (string.Equals(values[index].Resource, selectedResource, StringComparison.OrdinalIgnoreCase))
            {
                selected = index;
            }
        }
        option.Selected = selected;
    }

    private static string SelectedResource(
        OptionButton option,
        IReadOnlyList<StudioLoadingMediaOption> values)
    {
        var index = Math.Clamp(option.Selected, 0, Math.Max(0, values.Count - 1));
        return values.Count == 0 ? string.Empty : values[index].Resource;
    }

    private static IReadOnlyList<string> ReadTips(TextEdit edit) => edit.Text
        .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static void SelectFirstAvailable(OptionButton option)
    {
        for (var index = 1; index < option.ItemCount; index++)
        {
            if (!option.IsItemDisabled(index))
            {
                option.Selected = index;
                return;
            }
        }
        option.Selected = 0;
    }
}
