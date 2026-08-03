#if TOOLS
using Forge.Application.Localization;
using Forge.Application.Projects;
using Forge.Domain.Localization;
using Godot;

namespace Forge.Toolset.Editor;

public partial class LocalizationWorkspaceWindow : Window
{
    private Func<ProjectState?>? _getState;
    private Func<LocalizationCatalog, bool, bool>? _apply;
    private Action<Window>? _brandWindow;
    private LocalizationCatalog? _draft;
    private IReadOnlyList<LocalizationMessage> _visibleMessages = [];
    private LocalizationMessage? _selectedMessage;
    private LineEdit? _search;
    private OptionButton? _locale;
    private ItemList? _messages;
    private Label? _metrics;
    private Label? _identity;
    private TextEdit? _translation;
    private Label? _preview;
    private Label? _status;
    private Button? _applyButton;

    public int MessageCount => _draft?.Messages.Count ?? 0;
    public int LocaleCount => _draft?.Locales.Count ?? 0;

    public void Initialize(
        Func<ProjectState?> getState,
        Func<LocalizationCatalog, bool, bool> apply,
        Action<Window> brandWindow)
    {
        _getState = getState ?? throw new ArgumentNullException(nameof(getState));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _brandWindow = brandWindow ?? throw new ArgumentNullException(nameof(brandWindow));
        Title = "Localisation · OpenRPG Forge";
        MinSize = new Vector2I(1180, 760);
        Size = new Vector2I(1180, 760);
        Transient = true;
        Exclusive = false;
        Unresizable = true;
        Visible = false;
        BuildUi();
    }

    public void Open()
    {
        Refresh();
        Exclusive = true;
        PopupCentered(Size);
        _brandWindow?.Invoke(this);
    }

    public void Refresh()
    {
        var state = _getState?.Invoke();
        if (state is null)
        {
            SetStatus("Aucun projet Forge ouvert.", true);
            return;
        }

        var extraction = LocalizationCatalogExtractor.Extract(state.Project);
        _draft = extraction.Catalog;
        _selectedMessage = null;
        PopulateLocales();
        RefreshMessages();
        SetStatus(
            extraction.Diagnostics.Count == 0
                ? "✓ Catalogue extrait du projet · aucune modification appliquée"
                : $"Extraction terminée avec {extraction.Diagnostics.Count} diagnostic(s).",
            extraction.Diagnostics.Count > 0);
    }

    public bool SelectForSmokeTest(string key, string locale)
    {
        if (_draft is null)
        {
            Refresh();
        }

        SelectLocale(locale);
        _search!.Text = key;
        RefreshMessages();
        var index = _visibleMessages.ToList().FindIndex(message => message.Key == key);
        if (index < 0)
        {
            return false;
        }

        _messages!.Select(index);
        SelectMessage(index);
        return true;
    }

    public bool SetTranslationForSmokeTest(string value)
    {
        if (_selectedMessage is null || _translation is null)
        {
            return false;
        }

        _translation.Text = value;
        return UpdateSelectedTranslation();
    }

    public bool PreviewForSmokeTest() => _draft is not null && _apply?.Invoke(_draft, true) == true;

    private void BuildUi()
    {
        var background = new PanelContainer { AnchorRight = 1, AnchorBottom = 1 };
        background.AddThemeStyleboxOverride("panel", Card("100d1b", "62458a", 1, 0));
        AddChild(background);
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, 20);
        }

        background.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        var title = new Label { Text = "LOCALISATION & TRADUCTIONS" };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(title);
        var subtitle = new Label
        {
            Text = "Extraire les textes du module, traduire par langue et contrôler les replis avant application.",
        };
        subtitle.AddThemeColorOverride("font_color", new Color("c3b8d8"));
        root.AddChild(subtitle);

        _metrics = new Label();
        _metrics.AddThemeColorOverride("font_color", new Color("d8c8ff"));
        root.AddChild(_metrics);

        var toolbar = new HBoxContainer();
        toolbar.AddThemeConstantOverride("separation", 10);
        root.AddChild(toolbar);
        _search = new LineEdit { PlaceholderText = "Filtrer une clé, un texte, une provenance…", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _search.TextChanged += _ => RefreshMessages();
        toolbar.AddChild(_search);
        _locale = new OptionButton { CustomMinimumSize = new Vector2(170, 0) };
        _locale.ItemSelected += _ => RefreshSelection();
        toolbar.AddChild(_locale);
        var extract = new Button { Text = "Réextraire le projet" };
        extract.Pressed += Refresh;
        toolbar.AddChild(extract);

        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 14);
        root.AddChild(body);
        var listCard = AddCard(body, new Vector2(470, 0), true);
        listCard.AddChild(Section("MESSAGES CANONIQUES"));
        _messages = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill, SelectMode = ItemList.SelectModeEnum.Single };
        _messages.AddThemeStyleboxOverride("panel", Card("0c0a13", "3d3150", 1, 6));
        _messages.ItemSelected += index => SelectMessage((int)index);
        listCard.AddChild(_messages);

        var editor = AddCard(body, new Vector2(610, 0), true);
        editor.AddChild(Section("ÉDITEUR DE TRADUCTION"));
        _identity = DetailLabel();
        _identity.AddThemeColorOverride("font_color", new Color("f5dfc6"));
        editor.AddChild(_identity);
        editor.AddChild(FieldLabel("TRADUCTION DE LA LANGUE ACTIVE"));
        _translation = new TextEdit { CustomMinimumSize = new Vector2(0, 180), WrapMode = TextEdit.LineWrappingMode.Boundary };
        editor.AddChild(_translation);
        var saveTranslation = new Button { Text = "Mettre à jour la traduction" };
        saveTranslation.Pressed += () => UpdateSelectedTranslation();
        editor.AddChild(saveTranslation);
        editor.AddChild(FieldLabel("APERÇU RÉSOLU"));
        _preview = DetailLabel();
        _preview.CustomMinimumSize = new Vector2(0, 90);
        _preview.AddThemeColorOverride("font_color", new Color("83d6ae"));
        editor.AddChild(_preview);
        var pseudo = new Button { Text = "Afficher la pseudo-localisation" };
        pseudo.Pressed += ShowPseudoLocalization;
        editor.AddChild(pseudo);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 10);
        root.AddChild(footer);
        _status = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        footer.AddChild(_status);
        var previewCommand = new Button { Text = "Prévisualiser" };
        previewCommand.Pressed += PreviewCommand;
        footer.AddChild(previewCommand);
        _applyButton = new Button { Text = "Appliquer au projet" };
        _applyButton.AddThemeColorOverride("font_color", Colors.White);
        _applyButton.AddThemeStyleboxOverride("normal", Card("df5031", "ff8b61", 1, 7));
        _applyButton.Pressed += ApplyCommand;
        footer.AddChild(_applyButton);
        var close = new Button { Text = "Fermer" };
        close.Pressed += Hide;
        footer.AddChild(close);
        CloseRequested += Hide;
        ClearSelection();
    }

    private void PopulateLocales()
    {
        _locale!.Clear();
        foreach (var locale in _draft?.Locales ?? [])
        {
            _locale.AddItem(locale == _draft!.DefaultLocale ? $"{locale} · défaut" : locale);
            _locale.SetItemMetadata(_locale.ItemCount - 1, locale);
        }

        _locale.Selected = Math.Max(0, _draft?.Locales.ToList().FindIndex(item => item == _draft.DefaultLocale) ?? 0);
    }

    private void RefreshMessages()
    {
        if (_draft is null || _messages is null)
        {
            return;
        }

        var query = _search?.Text.Trim() ?? string.Empty;
        _visibleMessages = _draft.Messages.Where(message => string.IsNullOrEmpty(query) ||
            message.Key.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            message.Fallback.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            message.Provenance.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        _messages.Clear();
        var locale = SelectedLocale();
        foreach (var message in _visibleMessages)
        {
            var translated = message.Translations.ContainsKey(locale) ? "✓" : "○";
            _messages.AddItem($"{translated}  {message.Key}\n     {message.Fallback}");
        }

        var translatedCount = _draft.Messages.Count(message => message.Translations.ContainsKey(locale));
        _metrics!.Text = $"{_draft.Messages.Count} messages · {_draft.Locales.Count} langues · {translatedCount}/{_draft.Messages.Count} traduits en {locale}";
        if (_selectedMessage is null || !_visibleMessages.Any(item => item.Key == _selectedMessage.Key))
        {
            ClearSelection();
        }
    }

    private void SelectMessage(int index)
    {
        if (index < 0 || index >= _visibleMessages.Count)
        {
            ClearSelection();
            return;
        }

        _selectedMessage = _visibleMessages[index];
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        if (_selectedMessage is null || _draft is null)
        {
            ClearSelection();
            return;
        }

        var locale = SelectedLocale();
        _identity!.Text = $"{_selectedMessage.Key}\nSource : {_selectedMessage.Provenance}\nTexte de repli : {_selectedMessage.Fallback}";
        _translation!.Text = _selectedMessage.Translations.GetValueOrDefault(locale, string.Empty);
        _preview!.Text = _draft.Resolve(_selectedMessage.Key, locale);
    }

    private bool UpdateSelectedTranslation()
    {
        if (_draft is null || _selectedMessage is null || string.IsNullOrWhiteSpace(_translation?.Text))
        {
            SetStatus("La traduction ne peut pas être vide.", true);
            return false;
        }

        var locale = SelectedLocale();
        var translations = _selectedMessage.Translations.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        translations[locale] = _translation.Text.Trim();
        var updated = new LocalizationMessage(_selectedMessage.Key, _selectedMessage.Fallback, translations, _selectedMessage.Provenance);
        _draft = new LocalizationCatalog(_draft.Id, _draft.DefaultLocale, _draft.Locales,
            _draft.Messages.Select(message => message.Key == updated.Key ? updated : message).ToArray());
        _selectedMessage = updated;
        RefreshMessages();
        RefreshSelection();
        SetStatus($"Brouillon mis à jour · {updated.Key} · {locale}", false);
        return true;
    }

    private void ShowPseudoLocalization()
    {
        if (_selectedMessage is not null)
        {
            _preview!.Text = PseudoLocalization.Transform(_selectedMessage.Fallback);
        }
    }

    private void PreviewCommand()
    {
        if (_draft is not null && _apply?.Invoke(_draft, true) == true)
        {
            SetStatus("✓ Prévisualisation valide · aucune révision créée", false);
        }
        else
        {
            SetStatus("Prévisualisation refusée par la validation du projet.", true);
        }
    }

    private void ApplyCommand()
    {
        if (_draft is not null && _apply?.Invoke(_draft, false) == true)
        {
            SetStatus("✓ Catalogue appliqué · annulation/rétablissement disponibles", false);
        }
        else
        {
            SetStatus("Application refusée par la validation du projet.", true);
        }
    }

    private void ClearSelection()
    {
        _selectedMessage = null;
        if (_identity is null)
        {
            return;
        }

        _identity.Text = "Sélectionnez un message canonique.";
        _translation!.Text = string.Empty;
        _preview!.Text = "Le texte résolu et la pseudo-localisation apparaîtront ici.";
    }

    private string SelectedLocale()
    {
        if (_locale is null || _locale.ItemCount == 0)
        {
            return _draft?.DefaultLocale ?? "fr";
        }

        return _locale.GetItemMetadata(_locale.Selected).AsString();
    }

    private void SelectLocale(string locale)
    {
        if (_locale is null)
        {
            return;
        }

        for (var index = 0; index < _locale.ItemCount; index++)
        {
            if (string.Equals(_locale.GetItemMetadata(index).AsString(), locale, StringComparison.OrdinalIgnoreCase))
            {
                _locale.Selected = index;
                return;
            }
        }
    }

    private void SetStatus(string text, bool error)
    {
        if (_status is null)
        {
            return;
        }

        _status.Text = text;
        _status.AddThemeColorOverride("font_color", new Color(error ? "ff8c8c" : "83d6ae"));
    }

    private static VBoxContainer AddCard(Container parent, Vector2 minimum, bool expand)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = minimum,
            SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", Card("171221", "47365f", 1, 10));
        parent.AddChild(panel);
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, 14);
        }

        panel.AddChild(margin);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 9);
        margin.AddChild(content);
        return content;
    }

    private static Label Section(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", new Color("ffb276"));
        return label;
    }

    private static Label FieldLabel(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 11);
        label.AddThemeColorOverride("font_color", new Color("9f8dbd"));
        return label;
    }

    private static Label DetailLabel() => new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };

    private static StyleBoxFlat Card(string background, string border, int width, int radius) => new()
    {
        BgColor = new Color(background),
        BorderColor = new Color(border),
        BorderWidthLeft = width,
        BorderWidthTop = width,
        BorderWidthRight = width,
        BorderWidthBottom = width,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius,
    };
}
#endif
