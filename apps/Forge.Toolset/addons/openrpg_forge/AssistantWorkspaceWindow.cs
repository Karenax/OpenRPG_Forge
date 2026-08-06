#if TOOLS
using Forge.Application.Assistant;
using Forge.Application.Creator;
using Forge.Application.Projects;
using Forge.Domain.Assistant;
using Godot;

namespace Forge.Toolset.Editor;

public partial class AssistantWorkspaceWindow : Window
{
    private Func<ProjectState?>? _getProject;
    private Func<AssistantWorkspaceProfileState?>? _getAssistant;
    private Func<IReadOnlyList<CreatorSelection>>? _getSelection;
    private Func<AssistantWorkspaceProfile, Guid, bool, bool>? _apply;
    private Func<bool>? _undo;
    private Func<bool>? _redo;
    private Action? _openLore;
    private Action? _openAreaPlans;
    private Action? _openAssistedDialogues;
    private Action? _openLocalization;
    private Action<Window>? _brandWindow;
    private AssistantWorkspaceProfile? _source;
    private AssistantWorkspaceProfile? _approved;
    private Guid _approvedPlanId;
    private AssistantProviderKind[] _providerKinds = Enum.GetValues<AssistantProviderKind>();

    private OptionButton? _providerKind;
    private LineEdit? _endpoint;
    private LineEdit? _model;
    private LineEdit? _credentialEnvironmentVariable;
    private CheckButton? _networkAllowed;
    private CheckButton? _shareProjectContent;
    private LineEdit? _promptVersion;
    private TextEdit? _systemTemplate;
    private TextEdit? _userTemplate;
    private TextEdit? _responseSchema;
    private LineEdit? _request;
    private Label? _context;
    private RichTextLabel? _preview;
    private Label? _status;
    private Button? _previewButton;
    private Button? _applyButton;
    private bool _suppressSignals;
    private readonly System.Net.Http.HttpClient _providerHttpClient = new(
        new System.Net.Http.SocketsHttpHandler { AllowAutoRedirect = false })
    {
        Timeout = Timeout.InfiniteTimeSpan,
    };

    public string PromptVersion => _getAssistant?.Invoke()?.Profile.Prompts.FirstOrDefault()?.Version ?? string.Empty;
    public string LastPromptHash { get; private set; } = string.Empty;
    public bool LastPreviewWasOffline { get; private set; }
    public decimal LastProviderCost { get; private set; }

    public void Initialize(
        Func<ProjectState?> getProject,
        Func<AssistantWorkspaceProfileState?> getAssistant,
        Func<IReadOnlyList<CreatorSelection>> getSelection,
        Func<AssistantWorkspaceProfile, Guid, bool, bool> apply,
        Func<bool> undo,
        Func<bool> redo,
        Action openLore,
        Action openAreaPlans,
        Action openAssistedDialogues,
        Action openLocalization,
        Action<Window> brandWindow)
    {
        _getProject = getProject ?? throw new ArgumentNullException(nameof(getProject));
        _getAssistant = getAssistant ?? throw new ArgumentNullException(nameof(getAssistant));
        _getSelection = getSelection ?? throw new ArgumentNullException(nameof(getSelection));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _undo = undo ?? throw new ArgumentNullException(nameof(undo));
        _redo = redo ?? throw new ArgumentNullException(nameof(redo));
        _openLore = openLore ?? throw new ArgumentNullException(nameof(openLore));
        _openAreaPlans = openAreaPlans ?? throw new ArgumentNullException(nameof(openAreaPlans));
        _openAssistedDialogues = openAssistedDialogues ?? throw new ArgumentNullException(nameof(openAssistedDialogues));
        _openLocalization = openLocalization ?? throw new ArgumentNullException(nameof(openLocalization));
        _brandWindow = brandWindow ?? throw new ArgumentNullException(nameof(brandWindow));
        Title = "Assistant de création · OpenRPG Forge";
        MinSize = new Vector2I(1260, 800);
        Size = MinSize;
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
        var project = _getProject?.Invoke();
        var assistant = _getAssistant?.Invoke();
        if (project is null || assistant is null)
        {
            SetStatus("Aucun projet Forge ouvert.", true);
            return;
        }
        _source = assistant.Profile;
        var provider = _source.ActiveProvider;
        var prompt = _source.Prompts[0];
        _suppressSignals = true;
        _providerKind!.Selected = Array.IndexOf(_providerKinds, provider.Kind);
        _endpoint!.Text = provider.Endpoint;
        _model!.Text = provider.Model;
        _credentialEnvironmentVariable!.Text = provider.CredentialEnvironmentVariable;
        _networkAllowed!.ButtonPressed = _source.NetworkAllowed;
        _shareProjectContent!.ButtonPressed = _source.ShareProjectContent;
        _promptVersion!.Text = prompt.Version;
        _systemTemplate!.Text = prompt.SystemTemplate;
        _userTemplate!.Text = prompt.UserTemplate;
        _responseSchema!.Text = prompt.ResponseJsonSchema;
        _request!.Text = "Résume le contexte courant et propose un plan sans mutation.";
        _suppressSignals = false;
        RefreshContext();
        InvalidatePreview();
        SetStatus($"Profil chargé · révision {assistant.Revision} · provider {provider.Key}.", false);
    }

    public bool ConfigureForSmokeTest(string version)
    {
        Refresh();
        _promptVersion!.Text = version;
        _request!.Text = "Prépare un plan déterministe pour la sélection courante.";
        return true;
    }

    public bool PreviewForSmokeTest() => PreviewAsync().GetAwaiter().GetResult();

    public bool ApplyForSmokeTest()
    {
        Apply();
        return _approved is null;
    }

    private void BuildUi()
    {
        var background = new PanelContainer { AnchorRight = 1, AnchorBottom = 1 };
        background.AddThemeStyleboxOverride("panel", Card("100d1b", "62458a", 1, 0));
        AddChild(background);
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 20);
        background.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);
        var title = new Label { Text = "ASSISTANT DE CRÉATION", ThemeTypeVariation = "HeaderSmall" };
        title.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(title);
        root.AddChild(new Label
        {
            Text = "Contexte Creator, fournisseurs optionnels et prompts versionnés · aucun changement sans plan approuvé.",
        });

        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 12);
        root.AddChild(body);

        var providerCard = AddCard(body, 350);
        providerCard.AddChild(Section("FOURNISSEUR & CONFIDENTIALITÉ"));
        _providerKind = new OptionButton();
        foreach (var kind in _providerKinds) _providerKind.AddItem(FormatKind(kind));
        providerCard.AddChild(Field("Type", _providerKind));
        _endpoint = AddLine(providerCard, "Endpoint", "http://127.0.0.1:1234/v1");
        _model = AddLine(providerCard, "Modèle", "forge-deterministic-v1");
        _credentialEnvironmentVariable = AddLine(providerCard, "Variable du secret", "OPENRPG_ASSISTANT_API_KEY");
        var secretHelp = new Label
        {
            Text = "La valeur du secret n'est jamais persistée ; seul le nom de variable est autorisé.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        secretHelp.AddThemeColorOverride("font_color", new Color("9ee6b3"));
        providerCard.AddChild(secretHelp);
        _networkAllowed = new CheckButton { Text = "Autoriser le réseau pour ce profil" };
        providerCard.AddChild(_networkAllowed);
        _shareProjectContent = new CheckButton { Text = "Autoriser l'envoi du contexte projet" };
        providerCard.AddChild(_shareProjectContent);

        var promptCard = AddCard(body, 430, true);
        promptCard.AddChild(Section("PROMPT VERSIONNÉ"));
        _promptVersion = AddLine(promptCard, "Version", "1.0.0");
        promptCard.AddChild(FieldLabel("MESSAGE SYSTÈME"));
        _systemTemplate = new TextEdit { CustomMinimumSize = new Vector2(0, 110), WrapMode = TextEdit.LineWrappingMode.Boundary };
        promptCard.AddChild(_systemTemplate);
        promptCard.AddChild(FieldLabel("DEMANDE AVEC VARIABLES {{...}}"));
        _userTemplate = new TextEdit { CustomMinimumSize = new Vector2(0, 120), WrapMode = TextEdit.LineWrappingMode.Boundary };
        promptCard.AddChild(_userTemplate);
        promptCard.AddChild(FieldLabel("SCHÉMA JSON DE RÉPONSE"));
        _responseSchema = new TextEdit { CustomMinimumSize = new Vector2(0, 110), WrapMode = TextEdit.LineWrappingMode.Boundary };
        promptCard.AddChild(_responseSchema);

        var planCard = AddCard(body, 390, true);
        planCard.AddChild(Section("CONTEXTE & PLAN"));
        _context = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _context.AddThemeColorOverride("font_color", new Color("d8c8ff"));
        planCard.AddChild(_context);
        _request = AddLine(planCard, "Demande", "Décrire la modification souhaitée");
        planCard.AddChild(FieldLabel("PRÉVISUALISATION SANS MUTATION"));
        _preview = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 260),
        };
        _preview.AddThemeStyleboxOverride("normal", Card("0b0911", "3d3150", 1, 7));
        planCard.AddChild(_preview);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 8);
        root.AddChild(footer);
        _status = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        footer.AddChild(_status);
        var localization = new Button { Text = "Localisation" };
        localization.Pressed += () => _openLocalization?.Invoke();
        footer.AddChild(localization);
        var lore = new Button { Text = "Index de lore" };
        lore.Pressed += () => _openLore?.Invoke();
        footer.AddChild(lore);
        var areaPlans = new Button { Text = "Plans de zone" };
        areaPlans.Pressed += () => _openAreaPlans?.Invoke();
        footer.AddChild(areaPlans);
        var dialogues = new Button { Text = "Dialogue assisté" };
        dialogues.Pressed += () => _openAssistedDialogues?.Invoke();
        footer.AddChild(dialogues);
        var undo = new Button { Text = "Annuler" };
        undo.Pressed += () => { if (_undo?.Invoke() == true) Refresh(); };
        footer.AddChild(undo);
        var redo = new Button { Text = "Rétablir" };
        redo.Pressed += () => { if (_redo?.Invoke() == true) Refresh(); };
        footer.AddChild(redo);
        _previewButton = new Button { Text = "Prévisualiser", CustomMinimumSize = new Vector2(140, 42) };
        _previewButton.Pressed += async () => await PreviewAsync();
        footer.AddChild(_previewButton);
        _applyButton = new Button { Text = "Appliquer le profil", Disabled = true, CustomMinimumSize = new Vector2(170, 42) };
        _applyButton.AddThemeColorOverride("font_color", Colors.White);
        _applyButton.AddThemeStyleboxOverride("normal", Card("d84f32", "ff8b63", 1, 8));
        _applyButton.Pressed += Apply;
        footer.AddChild(_applyButton);
        var close = new Button { Text = "Fermer" };
        close.Pressed += Hide;
        footer.AddChild(close);
        CloseRequested += Hide;

        _providerKind.ItemSelected += _ => InvalidatePreview();
        foreach (var line in new[] { _endpoint, _model, _credentialEnvironmentVariable, _promptVersion, _request })
            line.TextChanged += _ => InvalidatePreview();
        foreach (var text in new[] { _systemTemplate, _userTemplate, _responseSchema })
            text.TextChanged += () => InvalidatePreview();
        _networkAllowed.Toggled += _ => InvalidatePreview();
        _shareProjectContent.Toggled += _ => InvalidatePreview();
    }

    private async Task<bool> PreviewAsync()
    {
        if (_previewButton is not null) _previewButton.Disabled = true;
        try
        {
            var draft = BuildDraft();
            var diagnostics = AssistantWorkspaceProfileValidator.Validate(draft);
            if (diagnostics.Count > 0)
            {
                SetStatus($"{diagnostics[0].Code} · {diagnostics[0].Message}", true);
                return false;
            }
            var prompt = draft.Prompts[0];
            var tests = AssistantPromptTestRunner.Run(prompt);
            if (!tests.Passed)
            {
                SetStatus($"Test de prompt refusé · {tests.Failures[0]}", true);
                return false;
            }
            var project = _getProject!.Invoke()!;
            var selection = _getSelection!.Invoke().FirstOrDefault();
            var rendered = AssistantPromptRenderer.Render(prompt, new Dictionary<string, string>
            {
                ["project.title"] = draft.ShareProjectContent ? project.Project.Title : "[contenu projet masqué]",
                ["selection.kind"] = draft.ShareProjectContent ? selection?.Kind.ToString() ?? "None" : "Hidden",
                ["request.text"] = _request!.Text.Trim(),
            });
            var planId = Guid.NewGuid();
            if (_apply?.Invoke(draft, planId, true) != true)
            {
                SetStatus("Le plan de configuration a été refusé par la commande de domaine.", true);
                return false;
            }
            LastPreviewWasOffline = draft.ActiveProvider.Kind == AssistantProviderKind.DeterministicOffline;
            var registry = LastPreviewWasOffline
                ? new AssistantProviderRegistry()
                : AssistantProviderRegistry.CreateWithLocalTransports(
                    _providerHttpClient,
                    requestTimeout: TimeSpan.FromSeconds(30));
            var response = await registry.Resolve(draft.ActiveProvider).GenerateAsync(new AssistantProviderRequest(
                planId,
                Guid.NewGuid(),
                rendered,
                AssistantPermissionLevel.Propose,
                draft.ActiveProvider.MaximumOutputTokens,
                draft.NetworkAllowed,
                draft.ShareProjectContent));
            _approved = draft;
            _approvedPlanId = planId;
            LastPromptHash = rendered.ContentSha256;
            LastProviderCost = response.EstimatedCost;
            _applyButton!.Disabled = false;
            _preview!.Text =
                $"[color=#83d6ae]PLAN {planId:D}[/color]\n" +
                $"Provider : {draft.ActiveProvider.DisplayName} · {draft.ActiveProvider.Kind}\n" +
                $"Prompt : {prompt.Key}@{prompt.Version}\nSHA-256 : {LastPromptHash}\n" +
                $"Permission : {draft.MaximumPermission} · coût estimé : {response.EstimatedCost:0.########}\n" +
                $"Tokens : {response.InputTokens} entrée / {response.OutputTokens} sortie\n" +
                $"Provenance : {response.Provenance}\n\n{response.Content}";
            SetStatus("✓ Plan prévisualisé · aucun fichier ni révision modifié.", false);
            return true;
        }
        catch (AssistantProviderTransportException exception)
        {
            SetStatus($"{exception.Code} · {exception.Message}", true);
            return false;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            SetStatus(exception.Message, true);
            return false;
        }
        finally
        {
            if (_previewButton is not null) _previewButton.Disabled = false;
        }
    }

    private void Apply()
    {
        if (_approved is null || _approvedPlanId == Guid.Empty) return;
        if (_apply?.Invoke(_approved, _approvedPlanId, false) != true)
        {
            SetStatus("Application refusée : plan obsolète, permission ou persistance invalide.", true);
            return;
        }
        _approved = null;
        _approvedPlanId = Guid.Empty;
        _applyButton!.Disabled = true;
        Refresh();
        SetStatus("✓ Profil assistant appliqué · annulation et rétablissement disponibles.", false);
    }

    private AssistantWorkspaceProfile BuildDraft()
    {
        var source = _source ?? throw new InvalidOperationException("Assistant profile is not loaded.");
        var previousProvider = source.ActiveProvider;
        var kind = _providerKinds[_providerKind!.Selected];
        var endpoint = kind == AssistantProviderKind.DeterministicOffline ? string.Empty : _endpoint!.Text;
        var credential = kind == AssistantProviderKind.DeterministicOffline ? string.Empty : _credentialEnvironmentVariable!.Text;
        var (providerKey, providerName) = kind switch
        {
            AssistantProviderKind.OpenAiCompatible => ("openai-compatible", "OpenAI compatible"),
            AssistantProviderKind.Ollama => ("ollama", "Ollama local"),
            _ => ("offline-ci", "Hors ligne déterministe"),
        };
        var provider = new AssistantProviderDefinition(
            previousProvider.Id, 1, providerKey, providerName, kind,
            endpoint, _model!.Text, credential, true,
            kind == AssistantProviderKind.DeterministicOffline || IsLoopback(endpoint),
            previousProvider.MaximumInputTokens, previousProvider.MaximumOutputTokens,
            previousProvider.InputCostPerMillionTokens, previousProvider.OutputCostPerMillionTokens,
            "creator:g12-a");
        var previousPrompt = source.Prompts[0];
        var version = _promptVersion!.Text.Trim();
        var changelog = previousPrompt.Changelog.Any(item => item.Version == version)
            ? previousPrompt.Changelog
            : previousPrompt.Changelog.Append(new AssistantPromptChange(version, "Mise à jour via l'Assistant Creator.")).ToArray();
        var prompt = new AssistantPromptDefinition(
            previousPrompt.Id, 1, previousPrompt.Key, version, previousPrompt.DisplayName,
            _systemTemplate!.Text, _userTemplate!.Text, _responseSchema!.Text,
            previousPrompt.RequiredCapabilities, previousPrompt.Tests, changelog, "creator:g12-a");
        return new AssistantWorkspaceProfile(
            source.Id, 1, provider.Id, [provider], [prompt], AssistantPermissionLevel.Configure,
            _networkAllowed!.ButtonPressed, _shareProjectContent!.ButtonPressed, "creator:g12-a");
    }

    private void RefreshContext()
    {
        var project = _getProject?.Invoke();
        var selection = _getSelection?.Invoke() ?? [];
        _context!.Text = project is null
            ? "Aucun contexte projet."
            : $"Projet · {project.Project.Title}\nRévision · {project.Revision.Value}\n" +
              $"Sélection · {(selection.Count == 0 ? "aucune" : string.Join(", ", selection.Select(item => item.DisplayName)))}\n" +
              $"Transaction · plan obligatoire, dry-run avant application";
    }

    private void InvalidatePreview()
    {
        if (_suppressSignals || _applyButton is null) return;
        _approved = null;
        _approvedPlanId = Guid.Empty;
        _applyButton.Disabled = true;
        LastPromptHash = string.Empty;
        LastPreviewWasOffline = false;
        LastProviderCost = 0;
    }

    private void SetStatus(string text, bool error)
    {
        if (_status is null) return;
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", new Color(error ? "ff8c8c" : "83d6ae"));
    }

    private static bool IsLoopback(string endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && uri.IsLoopback;

    private static string FormatKind(AssistantProviderKind kind) => kind switch
    {
        AssistantProviderKind.DeterministicOffline => "Hors ligne déterministe",
        AssistantProviderKind.OpenAiCompatible => "OpenAI compatible · LM Studio / vLLM / cloud",
        AssistantProviderKind.Ollama => "Ollama local",
        _ => kind.ToString(),
    };

    private static VBoxContainer AddCard(Container parent, float width, bool expand = false)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(width, 0),
            SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", Card("171221", "47365f", 1, 10));
        parent.AddChild(panel);
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 13);
        panel.AddChild(margin);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        margin.AddChild(content);
        return content;
    }

    private static Label Section(string text)
    {
        var label = new Label { Text = text };
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

    private static Control Field(string label, Control control)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 3);
        column.AddChild(FieldLabel(label.ToUpperInvariant()));
        column.AddChild(control);
        return column;
    }

    private static LineEdit AddLine(Container parent, string label, string placeholder)
    {
        var line = new LineEdit { PlaceholderText = placeholder };
        parent.AddChild(Field(label, line));
        return line;
    }

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
