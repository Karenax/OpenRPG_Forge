#if TOOLS
using System.Text.Json;
using Forge.Application.Assistant;
using Forge.Application.Creator;
using Forge.Application.Projects;
using Forge.Application.Quality;
using Forge.Domain.Assistant;
using Forge.RpgKit.Application.Combat;
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
    private Action? _openAssistedQuests;
    private Action? _openAssistedForgeScripts;
    private Action? _openAssistedBosses;
    private Action? _openAssistedAbilities;
    private Action? _openAssistedBlenderCreation;
    private Action? _openAssistedModel3DGeneration;
    private Action? _openExternalWorkerSecurity;
    private Action? _openLocalization;
    private Action<Window>? _brandWindow;
    private AssistantWorkspaceProfile? _source;
    private AssistantWorkspaceProfile? _approved;
    private Guid _approvedPlanId;
    private AssistantProviderKind[] _providerKinds = Enum.GetValues<AssistantProviderKind>();
    private readonly AssistantPermissionLevel[] _agentPermissionLevels =
        [AssistantPermissionLevel.ApplyApprovedPlan, AssistantPermissionLevel.BoundedAutonomous];
    private readonly AgentProjectClassification[] _projectClassifications = Enum.GetValues<AgentProjectClassification>();

    private OptionButton? _providerKind;
    private LineEdit? _endpoint;
    private LineEdit? _model;
    private LineEdit? _credentialEnvironmentVariable;
    private CheckButton? _networkAllowed;
    private CheckButton? _shareProjectContent;
    private SpinBox? _maximumContextItems;
    private SpinBox? _maximumGraphDepth;
    private SpinBox? _maximumGraphNodes;
    private OptionButton? _maximumAgentPermission;
    private CheckButton? _allowBoundedAutonomy;
    private SpinBox? _maximumAgentCommands;
    private SpinBox? _maximumAgentDuration;
    private OptionButton? _projectClassification;
    private CheckButton? _cloudProjectContentOptIn;
    private SpinBox? _maximumProviderInputTokens;
    private SpinBox? _maximumProviderOutputTokens;
    private SpinBox? _maximumProviderCost;
    private ScrollContainer? _providerScroll;
    private CheckButton? _requireStructuredOutput;
    private CheckButton? _allowBoundedRepair;
    private CheckButton? _promptInjectionGuard;
    private CheckButton? _offlineCore;
    private SpinBox? _maximumResponseCharacters;
    private SpinBox? _maximumUntrustedCharacters;
    private CheckButton? _testAutomationEnabled;
    private CheckButton? _testAutomationRequired;
    private SpinBox? _maximumTestSteps;
    private SpinBox? _maximumDialoguePaths;
    private SpinBox? _maximumQuestPaths;
    private CheckButton? _combatSimulationEnabled;
    private SpinBox? _combatSimulationRuns;
    private SpinBox? _combatSimulationMaximumRounds;
    private CheckButton? _combatSimulationPathing;
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
    private string? _capturePath;
    private double _captureDelay;
    private bool _captureOpened;
    private bool _captureR022;
    private bool _captureR120;
    private readonly System.Net.Http.HttpClient _providerHttpClient = new(
        new System.Net.Http.SocketsHttpHandler { AllowAutoRedirect = false })
    {
        Timeout = Timeout.InfiniteTimeSpan,
    };

    public string PromptVersion => _getAssistant?.Invoke()?.Profile.Prompts.FirstOrDefault()?.Version ?? string.Empty;
    public string LastPromptHash { get; private set; } = string.Empty;
    public bool LastPreviewWasOffline { get; private set; }
    public decimal LastProviderCost { get; private set; }
    public int MaximumAgentCommands => (int)(_maximumAgentCommands?.Value ?? 0);
    public int MaximumResponseCharacters => (int)(_maximumResponseCharacters?.Value ?? 0);
    public bool LastStructuredResultSucceeded { get; private set; }
    public bool LastOfflineReady { get; private set; }
    public int LastInjectionSignalCount { get; private set; }

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
        Action openAssistedQuests,
        Action openAssistedForgeScripts,
        Action openAssistedBosses,
        Action openAssistedAbilities,
        Action openAssistedBlenderCreation,
        Action openAssistedModel3DGeneration,
        Action openExternalWorkerSecurity,
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
        _openAssistedQuests = openAssistedQuests ?? throw new ArgumentNullException(nameof(openAssistedQuests));
        _openAssistedForgeScripts = openAssistedForgeScripts ?? throw new ArgumentNullException(nameof(openAssistedForgeScripts));
        _openAssistedBosses = openAssistedBosses ?? throw new ArgumentNullException(nameof(openAssistedBosses));
        _openAssistedAbilities = openAssistedAbilities ?? throw new ArgumentNullException(nameof(openAssistedAbilities));
        _openAssistedBlenderCreation = openAssistedBlenderCreation ??
            throw new ArgumentNullException(nameof(openAssistedBlenderCreation));
        _openAssistedModel3DGeneration = openAssistedModel3DGeneration ??
            throw new ArgumentNullException(nameof(openAssistedModel3DGeneration));
        _openExternalWorkerSecurity = openExternalWorkerSecurity ??
            throw new ArgumentNullException(nameof(openExternalWorkerSecurity));
        _openLocalization = openLocalization ?? throw new ArgumentNullException(nameof(openLocalization));
        _brandWindow = brandWindow ?? throw new ArgumentNullException(nameof(brandWindow));
        Title = "Assistant de création · OpenRPG Forge";
        MinSize = new Vector2I(1380, 860);
        Size = new Vector2I(1600, 960);
        Transient = true;
        Exclusive = false;
        Unresizable = true;
        Visible = false;
        BuildUi();
        var capture = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--r020-agent-workflow-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r021-agent-governance-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r022-agent-safety-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r120-ai-test-lab-ui-capture=", StringComparison.Ordinal));
        if (capture is not null)
        {
            var separator = capture.IndexOf('=');
            var capturePath = separator >= 0 ? capture[(separator + 1)..].Trim() : string.Empty;
            if (capturePath.Length == 0)
            {
                GD.PrintErr("OPENRPG_FORGE_ASSISTANT_UI_CAPTURE_FAILED reason=path-empty");
                return;
            }
            _capturePath = capturePath;
            _captureR022 = capture.StartsWith("--r022-agent-safety-ui-capture=", StringComparison.Ordinal);
            _captureR120 = capture.StartsWith("--r120-ai-test-lab-ui-capture=", StringComparison.Ordinal);
            SetProcess(true);
        }
    }

    public override void _Process(double delta)
    {
        if (_capturePath is null) return;
        _captureDelay += delta;
        if (!_captureOpened && _captureDelay >= 1.5)
        {
            Open();
            if (_captureR120) RunTestAutomation();
            _captureOpened = true;
            return;
        }
        if (_captureOpened && _captureDelay >= 2.5 && _providerScroll is not null)
            _providerScroll.ScrollVertical = _captureR120 ? 430 : _captureR022 ? 980 : 520;
        if (!_captureOpened || _captureDelay < 4) return;
        var destination = Path.GetFullPath(_capturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(destination);
        var marker = _captureR120 ? "OPENRPG_FORGE_R120_AI_TEST_LAB_UI_CAPTURE" :
            _captureR022 ? "OPENRPG_FORGE_R022_AGENT_SAFETY_UI_CAPTURE" :
            "OPENRPG_FORGE_R021_AGENT_GOVERNANCE_UI_CAPTURE";
        GD.Print($"{marker}_{(error == Error.Ok ? "PASS" : "FAILED")} " +
                 $"path={destination} size={image.GetWidth()}x{image.GetHeight()}");
        _capturePath = null;
        SetProcess(false);
        GetTree().Quit(error == Error.Ok ? 0 : 1);
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
        _maximumContextItems!.Value = _source.AgentWorkflow.MaximumContextItems;
        _maximumGraphDepth!.Value = _source.AgentWorkflow.References.MaximumDepth;
        _maximumGraphNodes!.Value = _source.AgentWorkflow.References.MaximumNodes;
        var governance = _source.AgentWorkflow.EffectiveGovernance;
        _maximumAgentPermission!.Selected = Math.Max(0,
            Array.IndexOf(_agentPermissionLevels, _source.AgentWorkflow.MaximumPermission));
        _allowBoundedAutonomy!.ButtonPressed = governance.Authorization.AllowBoundedAutonomy;
        _maximumAgentCommands!.Value = governance.Authorization.MaximumCommands;
        _maximumAgentDuration!.Value = governance.Authorization.MaximumDurationSeconds;
        _projectClassification!.Selected = Array.IndexOf(_projectClassifications,
            governance.BudgetPrivacy.ProjectClassification);
        _cloudProjectContentOptIn!.ButtonPressed = governance.BudgetPrivacy.CloudProjectContentOptIn;
        var providerBudget = governance.BudgetPrivacy.Providers.FirstOrDefault(item =>
            string.Equals(item.ProviderKey, provider.Key, StringComparison.Ordinal)) ?? governance.BudgetPrivacy.Providers[0];
        _maximumProviderInputTokens!.Value = providerBudget.MaximumInputTokens;
        _maximumProviderOutputTokens!.Value = providerBudget.MaximumOutputTokens;
        _maximumProviderCost!.Value = (double)providerBudget.MaximumCostPerRequest;
        var safety = _source.AgentWorkflow.EffectiveSafety;
        _requireStructuredOutput!.ButtonPressed = safety.StructuredOutput.RequireJsonSchema &&
            safety.StructuredOutput.RequireToolCallsForMutation && safety.StructuredOutput.RejectAmbiguousMutation;
        _allowBoundedRepair!.ButtonPressed = safety.StructuredOutput.AllowBoundedSyntaxRepair;
        _promptInjectionGuard!.ButtonPressed = safety.PromptInjection.TreatProjectContentAsData &&
            safety.PromptInjection.EnforceToolAllowlist && safety.PromptInjection.ProhibitHiddenEscalation;
        _offlineCore!.ButtonPressed = safety.Offline.ManualAuthoringAvailable && safety.Offline.AiMetadataOptional &&
            safety.Offline.BuildRuntimeIndependent && safety.Offline.NetworkDisabledByDefault;
        _maximumResponseCharacters!.Value = safety.StructuredOutput.MaximumResponseCharacters;
        _maximumUntrustedCharacters!.Value = safety.PromptInjection.MaximumUntrustedCharacters;
        var testing = _source.TestAutomation;
        _testAutomationEnabled!.ButtonPressed = testing.Enabled;
        _testAutomationRequired!.ButtonPressed = testing.RequiredAtRuntime;
        _maximumTestSteps!.Value = testing.MaximumScenarioSteps;
        _maximumDialoguePaths!.Value = testing.MaximumDialoguePaths;
        _maximumQuestPaths!.Value = testing.MaximumQuestPaths;
        var combatSimulation = _source.CombatSimulation;
        _combatSimulationEnabled!.ButtonPressed = combatSimulation.Enabled;
        _combatSimulationRuns!.Value = combatSimulation.RunsPerMatchup;
        _combatSimulationMaximumRounds!.Value = combatSimulation.MaximumRounds;
        _combatSimulationPathing!.ButtonPressed = combatSimulation.IncludePathing;
        _promptVersion!.Text = prompt.Version;
        _systemTemplate!.Text = prompt.SystemTemplate;
        _userTemplate!.Text = prompt.UserTemplate;
        _responseSchema!.Text = PrettyJson(prompt.ResponseJsonSchema);
        _request!.Text = "Résume le contexte courant et propose un plan sans mutation.";
        _preview!.Text = "[color=#9f8dbd]Aucun plan prévisualisé.[/color]\n" +
                         "Décrivez une intention puis lancez le dry-run. Les erreurs de révision, cible, outil et permission apparaîtront ici.";
        _suppressSignals = false;
        RefreshContext();
        InvalidatePreview();
        SetStatus($"Profil chargé · révision {assistant.Revision} · provider {provider.Key}.", false);
    }

    public bool ConfigureForSmokeTest(string version)
    {
        Refresh();
        _promptVersion!.Text = version;
        _maximumAgentCommands!.Value = 12;
        _maximumAgentDuration!.Value = 45;
        _maximumResponseCharacters!.Value = 131072;
        _maximumUntrustedCharacters!.Value = 32768;
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

        var providerCard = AddScrollableCard(body, 350, out _providerScroll);
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
        providerCard.AddChild(Section("WORKFLOW AGENT P0"));
        _maximumContextItems = new SpinBox { MinValue = 1, MaxValue = 300, Step = 1, Rounded = true };
        providerCard.AddChild(Field("Objets de contexte max.", _maximumContextItems));
        _maximumGraphDepth = new SpinBox { MinValue = 1, MaxValue = 8, Step = 1, Rounded = true };
        providerCard.AddChild(Field("Profondeur d'impact max.", _maximumGraphDepth));
        _maximumGraphNodes = new SpinBox { MinValue = 10, MaxValue = 1000, Step = 10, Rounded = true };
        providerCard.AddChild(Field("Noeuds du graphe max.", _maximumGraphNodes));
        var workflowHelp = new Label
        {
            Text = "10 contextes structurés · graphe inverse typé · 5 outils métier · plan + dry-run sans écriture.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        workflowHelp.AddThemeColorOverride("font_color", new Color("d8c8ff"));
        providerCard.AddChild(workflowHelp);
        providerCard.AddChild(Section("GOUVERNANCE AGENT R021"));
        _maximumAgentPermission = new OptionButton();
        foreach (var permission in _agentPermissionLevels)
            _maximumAgentPermission.AddItem(permission == AssistantPermissionLevel.BoundedAutonomous
                ? "Autonomie bornée"
                : "Mutation avec confirmation");
        providerCard.AddChild(Field("Autorisation maximale", _maximumAgentPermission));
        _allowBoundedAutonomy = new CheckButton { Text = "Activer explicitement l'autonomie bornée" };
        providerCard.AddChild(_allowBoundedAutonomy);
        var executionLimits = new HBoxContainer();
        executionLimits.AddThemeConstantOverride("separation", 8);
        providerCard.AddChild(executionLimits);
        _maximumAgentCommands = new SpinBox { MinValue = 1, MaxValue = 100, Step = 1, Rounded = true };
        executionLimits.AddChild(Field("Commandes max.", _maximumAgentCommands));
        _maximumAgentDuration = new SpinBox { MinValue = 1, MaxValue = 3600, Step = 1, Rounded = true };
        executionLimits.AddChild(Field("Durée max. (s)", _maximumAgentDuration));
        _projectClassification = new OptionButton();
        foreach (var classification in _projectClassifications) _projectClassification.AddItem(classification.ToString());
        providerCard.AddChild(Field("Classification projet", _projectClassification));
        _cloudProjectContentOptIn = new CheckButton { Text = "Opt-in explicite : contenu vers cloud" };
        providerCard.AddChild(_cloudProjectContentOptIn);
        var providerLimits = new HBoxContainer();
        providerLimits.AddThemeConstantOverride("separation", 8);
        providerCard.AddChild(providerLimits);
        _maximumProviderInputTokens = new SpinBox { MinValue = 256, MaxValue = 1000000, Step = 256, Rounded = true };
        providerLimits.AddChild(Field("Tokens entrée", _maximumProviderInputTokens));
        _maximumProviderOutputTokens = new SpinBox { MinValue = 64, MaxValue = 1000000, Step = 64, Rounded = true };
        providerLimits.AddChild(Field("Tokens sortie", _maximumProviderOutputTokens));
        _maximumProviderCost = new SpinBox { MinValue = 0, MaxValue = 100, Step = .01, Rounded = false };
        providerLimits.AddChild(Field("Coût max.", _maximumProviderCost));
        var governanceHelp = new Label
        {
            Text = "Begin → preview → commit → rollback · diff structuré · audit expurgé · aucun secret journalisé.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        governanceHelp.AddThemeColorOverride("font_color", new Color("9ee6b3"));
        providerCard.AddChild(governanceHelp);
        providerCard.AddChild(Section("SORTIES SÛRES & HORS LIGNE R022"));
        _requireStructuredOutput = new CheckButton { Text = "JSON Schema + tool calls obligatoires" };
        providerCard.AddChild(_requireStructuredOutput);
        _allowBoundedRepair = new CheckButton { Text = "Réparer uniquement une fence JSON" };
        providerCard.AddChild(_allowBoundedRepair);
        _promptInjectionGuard = new CheckButton { Text = "Traiter le contenu projet comme données" };
        providerCard.AddChild(_promptInjectionGuard);
        _offlineCore = new CheckButton { Text = "Core manuel indépendant et hors ligne" };
        providerCard.AddChild(_offlineCore);
        var safetyLimits = new HBoxContainer();
        safetyLimits.AddThemeConstantOverride("separation", 8);
        providerCard.AddChild(safetyLimits);
        _maximumResponseCharacters = new SpinBox
        {
            MinValue = 1024,
            MaxValue = 1048576,
            Step = 1024,
            Rounded = true,
        };
        safetyLimits.AddChild(Field("Réponse max.", _maximumResponseCharacters));
        _maximumUntrustedCharacters = new SpinBox
        {
            MinValue = 1024,
            MaxValue = 1048576,
            Step = 1024,
            Rounded = true,
        };
        safetyLimits.AddChild(Field("Données max.", _maximumUntrustedCharacters));
        var safetyHelp = new Label
        {
            Text = "Données non fiables étiquetées par provenance · injection signalée · résultat ambigu refusé · réseau non requis.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        safetyHelp.AddThemeColorOverride("font_color", new Color("9ee6b3"));
        providerCard.AddChild(safetyHelp);
        providerCard.AddChild(Section("LABORATOIRE DE TEST P1"));
        _testAutomationEnabled = new CheckButton { Text = "Activer l'agent de test borné" };
        providerCard.AddChild(_testAutomationEnabled);
        _testAutomationRequired = new CheckButton { Text = "Exiger le profil au runtime" };
        providerCard.AddChild(_testAutomationRequired);
        _maximumTestSteps = new SpinBox { MinValue = 1, MaxValue = 512, Step = 1, Rounded = true };
        providerCard.AddChild(Field("Étapes maximum", _maximumTestSteps));
        _maximumDialoguePaths = new SpinBox { MinValue = 1, MaxValue = 4096, Step = 1, Rounded = true };
        providerCard.AddChild(Field("Chemins dialogue", _maximumDialoguePaths));
        _maximumQuestPaths = new SpinBox { MinValue = 1, MaxValue = 4096, Step = 1, Rounded = true };
        providerCard.AddChild(Field("Chemins quête", _maximumQuestPaths));
        var testHelp = new Label
        {
            Text = "Actions gameplay uniquement · couverture branches/états · cycles, récompenses, sauvegarde et cohérence avec citations.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        testHelp.AddThemeColorOverride("font_color", new Color("9ee6b3"));
        providerCard.AddChild(testHelp);
        providerCard.AddChild(Section("SIMULATION DE COMBAT P2"));
        _combatSimulationEnabled = new CheckButton { Text = "Comparer les archétypes de combat" };
        providerCard.AddChild(_combatSimulationEnabled);
        _combatSimulationRuns = new SpinBox { MinValue = 1, MaxValue = 256, Step = 1, Rounded = true };
        providerCard.AddChild(Field("Seeds par affrontement", _combatSimulationRuns));
        _combatSimulationMaximumRounds = new SpinBox { MinValue = 1, MaxValue = 5000, Step = 1, Rounded = true };
        providerCard.AddChild(Field("Tours maximum", _combatSimulationMaximumRounds));
        _combatSimulationPathing = new CheckButton { Text = "Inclure le temps de pathing" };
        providerCard.AddChild(_combatSimulationPathing);
        var combatHelp = new Label
        {
            Text = "Compare victoires, durée, ressources, pathing et déclencheurs de phases sur des seeds reproductibles.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        combatHelp.AddThemeColorOverride("font_color", new Color("9ee6b3"));
        providerCard.AddChild(combatHelp);

        var promptCard = AddCard(body, 430, true);
        promptCard.AddChild(Section("PROMPT VERSIONNÉ"));
        _promptVersion = AddLine(promptCard, "Version", "1.0.0");
        promptCard.AddChild(FieldLabel("MESSAGE SYSTÈME"));
        _systemTemplate = new TextEdit { CustomMinimumSize = new Vector2(0, 75), WrapMode = TextEdit.LineWrappingMode.Boundary };
        promptCard.AddChild(_systemTemplate);
        promptCard.AddChild(FieldLabel("DEMANDE AVEC VARIABLES {{...}}"));
        _userTemplate = new TextEdit { CustomMinimumSize = new Vector2(0, 80), WrapMode = TextEdit.LineWrappingMode.Boundary };
        promptCard.AddChild(_userTemplate);
        promptCard.AddChild(FieldLabel("SCHÉMA JSON DE RÉPONSE"));
        _responseSchema = new TextEdit { CustomMinimumSize = new Vector2(0, 75), WrapMode = TextEdit.LineWrappingMode.Boundary };
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
            CustomMinimumSize = new Vector2(0, 190),
        };
        _preview.AddThemeStyleboxOverride("normal", Card("0b0911", "3d3150", 1, 7));
        planCard.AddChild(_preview);

        var footer = new VBoxContainer();
        footer.AddThemeConstantOverride("separation", 8);
        root.AddChild(footer);
        _status = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        footer.AddChild(_status);
        var actions = new HFlowContainer();
        actions.AddThemeConstantOverride("h_separation", 8);
        actions.AddThemeConstantOverride("v_separation", 8);
        footer.AddChild(actions);
        var localization = new Button { Text = "Localisation" };
        localization.Pressed += () => _openLocalization?.Invoke();
        actions.AddChild(localization);
        var lore = new Button { Text = "Index de lore" };
        lore.Pressed += () => _openLore?.Invoke();
        actions.AddChild(lore);
        var areaPlans = new Button { Text = "Plans de zone" };
        areaPlans.Pressed += () => _openAreaPlans?.Invoke();
        actions.AddChild(areaPlans);
        var dialogues = new Button { Text = "Dialogue assisté" };
        dialogues.Pressed += () => _openAssistedDialogues?.Invoke();
        actions.AddChild(dialogues);
        var quests = new Button { Text = "Quête assistée" };
        quests.Pressed += () => _openAssistedQuests?.Invoke();
        actions.AddChild(quests);
        var scripts = new Button { Text = "ForgeScript assisté" };
        scripts.Pressed += () => _openAssistedForgeScripts?.Invoke();
        actions.AddChild(scripts);
        var bosses = new Button { Text = "Boss assisté" };
        bosses.Pressed += () => _openAssistedBosses?.Invoke();
        actions.AddChild(bosses);
        var abilities = new Button { Text = "Capacité assistée" };
        abilities.Pressed += () => _openAssistedAbilities?.Invoke();
        actions.AddChild(abilities);
        var blender = new Button { Text = "Création 3D" };
        blender.Pressed += () => _openAssistedBlenderCreation?.Invoke();
        actions.AddChild(blender);
        var model3D = new Button { Text = "Texte/Image vers 3D" };
        model3D.Pressed += () => _openAssistedModel3DGeneration?.Invoke();
        actions.AddChild(model3D);
        var workerSecurity = new Button { Text = "Securite workers" };
        workerSecurity.Pressed += () => _openExternalWorkerSecurity?.Invoke();
        actions.AddChild(workerSecurity);
        var testModule = new Button { Text = "Tester le module" };
        testModule.Pressed += RunTestAutomation;
        actions.AddChild(testModule);
        var simulateCombat = new Button { Text = "Simuler les combats" };
        simulateCombat.Pressed += RunCombatSimulation;
        actions.AddChild(simulateCombat);
        var undo = new Button { Text = "Annuler" };
        undo.Pressed += () => { if (_undo?.Invoke() == true) Refresh(); };
        actions.AddChild(undo);
        var redo = new Button { Text = "Rétablir" };
        redo.Pressed += () => { if (_redo?.Invoke() == true) Refresh(); };
        actions.AddChild(redo);
        _previewButton = new Button { Text = "Prévisualiser", CustomMinimumSize = new Vector2(140, 42) };
        _previewButton.Pressed += async () => await PreviewAsync();
        actions.AddChild(_previewButton);
        _applyButton = new Button { Text = "Appliquer le profil", Disabled = true, CustomMinimumSize = new Vector2(170, 42) };
        _applyButton.AddThemeColorOverride("font_color", Colors.White);
        _applyButton.AddThemeStyleboxOverride("normal", Card("d84f32", "ff8b63", 1, 8));
        _applyButton.Pressed += Apply;
        actions.AddChild(_applyButton);
        var close = new Button { Text = "Fermer" };
        close.Pressed += Hide;
        actions.AddChild(close);
        CloseRequested += Hide;

        _providerKind.ItemSelected += _ => InvalidatePreview();
        foreach (var line in new[] { _endpoint, _model, _credentialEnvironmentVariable, _promptVersion, _request })
            line.TextChanged += _ => InvalidatePreview();
        foreach (var text in new[] { _systemTemplate, _userTemplate, _responseSchema })
            text.TextChanged += () => InvalidatePreview();
        _networkAllowed.Toggled += _ => InvalidatePreview();
        _shareProjectContent.Toggled += _ => InvalidatePreview();
        _maximumContextItems.ValueChanged += _ => InvalidatePreview();
        _maximumGraphDepth.ValueChanged += _ => InvalidatePreview();
        _maximumGraphNodes.ValueChanged += _ => InvalidatePreview();
        _maximumAgentPermission.ItemSelected += _ => InvalidatePreview();
        _allowBoundedAutonomy.Toggled += _ => InvalidatePreview();
        _maximumAgentCommands.ValueChanged += _ => InvalidatePreview();
        _maximumAgentDuration.ValueChanged += _ => InvalidatePreview();
        _projectClassification.ItemSelected += _ => InvalidatePreview();
        _cloudProjectContentOptIn.Toggled += _ => InvalidatePreview();
        _maximumProviderInputTokens.ValueChanged += _ => InvalidatePreview();
        _maximumProviderOutputTokens.ValueChanged += _ => InvalidatePreview();
        _maximumProviderCost.ValueChanged += _ => InvalidatePreview();
        _requireStructuredOutput.Toggled += _ => InvalidatePreview();
        _allowBoundedRepair.Toggled += _ => InvalidatePreview();
        _promptInjectionGuard.Toggled += _ => InvalidatePreview();
        _offlineCore.Toggled += _ => InvalidatePreview();
        _maximumResponseCharacters.ValueChanged += _ => InvalidatePreview();
        _maximumUntrustedCharacters.ValueChanged += _ => InvalidatePreview();
        _testAutomationEnabled.Toggled += _ => InvalidatePreview();
        _testAutomationRequired.Toggled += _ => InvalidatePreview();
        _maximumTestSteps.ValueChanged += _ => InvalidatePreview();
        _maximumDialoguePaths.ValueChanged += _ => InvalidatePreview();
        _maximumQuestPaths.ValueChanged += _ => InvalidatePreview();
        _combatSimulationEnabled.Toggled += _ => InvalidatePreview();
        _combatSimulationRuns.ValueChanged += _ => InvalidatePreview();
        _combatSimulationMaximumRounds.ValueChanged += _ => InvalidatePreview();
        _combatSimulationPathing.Toggled += _ => InvalidatePreview();
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
            var variables = new Dictionary<string, string>
            {
                ["project.title"] = draft.ShareProjectContent ? project.Project.Title : "[contenu projet masqué]",
                ["selection.kind"] = draft.ShareProjectContent ? selection?.Kind.ToString() ?? "None" : "Hidden",
                ["request.text"] = _request!.Text.Trim(),
            };
            var guarded = AgentResponseSafetyService.RenderGuarded(prompt, variables,
                new Dictionary<string, string>
                {
                    ["project.title"] = "project.json:title",
                    ["selection.kind"] = selection is null ? "selection:none" : $"selection:{selection.Id:D}",
                }, draft.AgentWorkflow.EffectiveSafety);
            var fatalPromptDiagnostics = guarded.InjectionSignals.Where(item =>
                item.Code != AgentResponseSafetyService.UntrustedContentSignalCode).ToArray();
            if (fatalPromptDiagnostics.Length > 0)
            {
                SetStatus($"{fatalPromptDiagnostics[0].Code} · {fatalPromptDiagnostics[0].Message}", true);
                return false;
            }
            var rendered = guarded.Prompt;
            LastInjectionSignalCount = guarded.InjectionSignals.Count(item =>
                item.Code == AgentResponseSafetyService.UntrustedContentSignalCode);
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
            var providerRequest = new AssistantProviderRequest(
                planId,
                Guid.NewGuid(),
                rendered,
                AssistantPermissionLevel.Propose,
                draft.ActiveProvider.MaximumOutputTokens,
                draft.NetworkAllowed,
                draft.ShareProjectContent);
            var response = await registry.Resolve(draft.ActiveProvider).GenerateAsync(providerRequest);
            var structured = AgentResponseSafetyService.ValidateStructuredResult(
                prompt, response, providerRequest, draft.AgentWorkflow);
            LastStructuredResultSucceeded = structured.Succeeded;
            if (!structured.Succeeded)
            {
                SetStatus($"{structured.Diagnostics[0].Code} · {structured.Diagnostics[0].Message}", true);
                return false;
            }
            var offline = AgentResponseSafetyService.EvaluateOfflineReadiness(draft, false);
            LastOfflineReady = offline.Ready;
            if (!offline.Ready)
            {
                SetStatus("Le contrat hors ligne du profil est invalide.", true);
                return false;
            }
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
                $"Provenance : {response.Provenance}\n" +
                $"Contexte : {draft.AgentWorkflow.ContextResources.Count} surfaces structurées · " +
                $"{draft.AgentWorkflow.MaximumContextItems} objets max.\n" +
                $"Graphe : références typées + impact inverse · profondeur {draft.AgentWorkflow.References.MaximumDepth} · " +
                $"{draft.AgentWorkflow.References.MaximumNodes} noeuds max.\n" +
                $"Outils : {string.Join(", ", draft.AgentWorkflow.Tools.Select(item => item.Name))}\n" +
                "ChangePlan : but · commandes · hypothèses · objets · risques · tests\n" +
                "Dry-run : révision + batch + diff sémantique · aucune écriture\n" +
                $"Transaction : atomique · {_maximumAgentCommands!.Value:0} commandes · rollback UUID\n" +
                $"Autorisation : {draft.AgentWorkflow.MaximumPermission} · autonomie " +
                $"{(draft.AgentWorkflow.EffectiveGovernance.Authorization.AllowBoundedAutonomy ? "activée" : "désactivée")}\n" +
                $"Confidentialité : {draft.AgentWorkflow.EffectiveGovernance.BudgetPrivacy.ProjectClassification} · " +
                "redaction + aucun secret journalisé\nAudit : client · utilisateur · plan · commandes · hashes · tests · provenance\n\n" +
                $"Sortie : JSON Schema validé · réparation {(structured.Repaired ? "bornée" : "inutile")} · " +
                $"{structured.ToolCalls.Count} tool call(s)\n" +
                $"Injection : contenu projet=data + provenance · {LastInjectionSignalCount} signal(s)\n" +
                $"Hors ligne : core manuel={(offline.ManualAuthoringAvailable ? "prêt" : "bloqué")} · réseau requis=non\n\n" +
                structured.NormalizedJson;
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
        var sourceGovernance = source.AgentWorkflow.EffectiveGovernance;
        var providerPolicies = sourceGovernance.BudgetPrivacy.Providers.Select(item =>
            string.Equals(item.ProviderKey, providerKey, StringComparison.Ordinal)
                ? item with
                {
                    MaximumInputTokens = (int)_maximumProviderInputTokens!.Value,
                    MaximumOutputTokens = (int)_maximumProviderOutputTokens!.Value,
                    MaximumCostPerRequest = (decimal)_maximumProviderCost!.Value,
                    AllowProjectContent = !item.Cloud || _cloudProjectContentOptIn!.ButtonPressed,
                }
                : item).ToArray();
        var governance = sourceGovernance with
        {
            Provenance = "creator:r021",
            Transactions = sourceGovernance.Transactions with { MaximumCommands = (int)_maximumAgentCommands!.Value },
            Authorization = sourceGovernance.Authorization with
            {
                AllowBoundedAutonomy = _allowBoundedAutonomy!.ButtonPressed,
                MaximumCommands = (int)_maximumAgentCommands.Value,
                MaximumDurationSeconds = (int)_maximumAgentDuration!.Value,
            },
            BudgetPrivacy = sourceGovernance.BudgetPrivacy with
            {
                ProjectClassification = _projectClassifications[_projectClassification!.Selected],
                CloudProjectContentOptIn = _cloudProjectContentOptIn!.ButtonPressed,
                Providers = providerPolicies,
            },
        };
        var sourceSafety = source.AgentWorkflow.EffectiveSafety;
        var strictOutput = _requireStructuredOutput!.ButtonPressed;
        var guardedContent = _promptInjectionGuard!.ButtonPressed;
        var offlineCore = _offlineCore!.ButtonPressed;
        var safety = sourceSafety with
        {
            Provenance = "creator:r022",
            StructuredOutput = sourceSafety.StructuredOutput with
            {
                RequireJsonSchema = strictOutput,
                RequireToolCallsForMutation = strictOutput,
                AllowBoundedSyntaxRepair = _allowBoundedRepair!.ButtonPressed,
                RejectAmbiguousMutation = strictOutput,
                RejectUnknownProperties = strictOutput,
                MaximumResponseCharacters = (int)_maximumResponseCharacters!.Value,
            },
            PromptInjection = sourceSafety.PromptInjection with
            {
                TreatProjectContentAsData = guardedContent,
                RequireProvenanceLabels = guardedContent,
                EnforceToolAllowlist = guardedContent,
                ProhibitHiddenEscalation = guardedContent,
                RequireRiskConfirmation = guardedContent,
                MaximumUntrustedCharacters = (int)_maximumUntrustedCharacters!.Value,
            },
            Offline = sourceSafety.Offline with
            {
                ManualAuthoringAvailable = offlineCore,
                LocalProvidersOptional = offlineCore,
                AiMetadataOptional = offlineCore,
                BuildRuntimeIndependent = offlineCore,
                NetworkDisabledByDefault = offlineCore,
                DeterministicProviderAvailable = offlineCore,
            },
        };
        var workflow = source.AgentWorkflow with
        {
            MaximumPermission = _agentPermissionLevels[_maximumAgentPermission!.Selected],
            MaximumContextItems = (int)_maximumContextItems!.Value,
            References = source.AgentWorkflow.References with
            {
                MaximumDepth = (int)_maximumGraphDepth!.Value,
                MaximumNodes = (int)_maximumGraphNodes!.Value,
            },
            Governance = governance,
            Safety = safety,
        };
        var testing = new AssistantTestAutomationProfile(
            source.TestAutomation.Id, AssistantTestAutomationProfile.CurrentSchemaVersion, "creator:r120",
            _testAutomationEnabled!.ButtonPressed, _testAutomationRequired!.ButtonPressed,
            (int)_maximumTestSteps!.Value, (int)_maximumDialoguePaths!.Value, (int)_maximumQuestPaths!.Value,
            true, true, true, true, source.TestAutomation.AllowedGameplayActions);
        var project = _getProject?.Invoke()?.Project;
        var archetypeIds = project?.Blueprints.Where(item => item.Combat is not null)
            .Select(item => item.Id).OrderBy(item => item.Value).Take(32).ToArray() ?? [];
        var simulation = source.CombatSimulation with
        {
            Enabled = _combatSimulationEnabled!.ButtonPressed,
            RunsPerMatchup = (int)_combatSimulationRuns!.Value,
            MaximumRounds = (int)_combatSimulationMaximumRounds!.Value,
            IncludePathing = _combatSimulationPathing!.ButtonPressed,
            ArchetypeBlueprintIds = archetypeIds,
            Provenance = "creator:r143",
        };
        return new AssistantWorkspaceProfile(
            source.Id, 1, provider.Id, [provider], [prompt], AssistantPermissionLevel.Configure,
            _networkAllowed!.ButtonPressed, _shareProjectContent!.ButtonPressed, "creator:r021", workflow, testing,
            simulation);
    }

    private void RunCombatSimulation()
    {
        var project = _getProject?.Invoke()?.Project;
        if (project is null)
        {
            SetStatus("Aucun projet à simuler.", true);
            return;
        }
        var profile = BuildDraft().CombatSimulation;
        var archetypes = project.Blueprints.Where(item => item.Combat is not null)
            .Select(item => new CombatSimulationArchetype(item.Id, item.DisplayName, item.Combat!)).ToArray();
        var report = CombatBalanceSimulationService.Run(profile, archetypes);
        if (!report.Succeeded)
        {
            var first = report.Diagnostics.First();
            _preview!.Text = $"[b]SIMULATION COMBAT · BLOQUÉE[/b]\n{first.Code}\n{first.Message}\n{first.Path}";
            SetStatus($"{first.Code} · {first.Message}", true);
            return;
        }
        var rows = report.Matchups.Take(12).Select(item =>
            $"{item.FirstArchetypeId.ToString()[..8]} / {item.SecondArchetypeId.ToString()[..8]} · " +
            $"victoires {item.FirstWins}/{item.SecondWins}/{item.Draws} · durée {item.AverageDurationSeconds:0.0}s · " +
            $"ressources {item.AverageFirstResourceUsed:0.0}/{item.AverageSecondResourceUsed:0.0}");
        _preview!.Text = $"[b]SIMULATION COMBAT · PRÊTE[/b]\n" + string.Join('\n', rows) +
            $"\n\nEmpreinte : {report.Fingerprint}\nProvenance : {report.Provenance}";
        SetStatus($"Simulation terminée · {report.Matchups.Count} comparaison(s) déterministes.", false);
    }

    private void RunTestAutomation()
    {
        var project = _getProject?.Invoke();
        if (project is null)
        {
            SetStatus("Aucun projet à tester.", true);
            return;
        }
        try
        {
            var profile = BuildDraft().TestAutomation;
            var report = AssistantScenarioTestService.Run(project.Project, profile);
            var coverage = report.Coverage;
            var lines = report.Findings.Take(8).Select(item =>
                $"[color={(item.Severity == AssistantScenarioFindingSeverity.Blocking ? "#ff7777" : "#ffbd7a")}]{item.Code}[/color]\n{item.Message}\n{item.DocumentPath}");
            _preview!.Text = $"[b]AGENT DE TEST · {(report.Succeeded ? "PRÊT" : "BLOQUÉ")}[/b]\n" +
                $"Dialogues {coverage.DialogueNodesCovered}/{coverage.DialogueNodesTotal} nœuds · " +
                $"Quêtes {coverage.QuestStatesCovered}/{coverage.QuestStatesTotal} états\n" +
                $"Actions : {string.Join(", ", report.GameplayActions)}\n" +
                $"Empreinte : {report.Fingerprint[..16]}…\n\n" +
                (report.Findings.Count == 0 ? "[color=#9ee6b3]Aucun blocage narratif détecté.[/color]" : string.Join("\n\n", lines));
            SetStatus($"Test terminé · {report.Findings.Count} constat(s) · reproduction et citations disponibles.",
                !report.Succeeded);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            SetStatus($"Test refusé · {exception.Message}", true);
        }
    }

    private void RefreshContext()
    {
        var project = _getProject?.Invoke();
        var selection = _getSelection?.Invoke() ?? [];
        _context!.Text = project is null
            ? "Aucun contexte projet."
            : $"Projet · {project.Project.Title}\nRévision · {project.Revision.Value}\n" +
              $"Sélection · {(selection.Count == 0 ? "aucune" : string.Join(", ", selection.Select(item => item.DisplayName)))}\n" +
              "Contexte · 10 surfaces structurées, bornées et sans lecture brute\n" +
              "Graphe · références typées, impact inverse, ordre stable\n" +
              "Outils · commandes métier avec permission explicite\n" +
              "Transaction · atomique, rollback UUID et audit expurgé\n" +
              "Autorisation · confirmation ou autonomie bornée · outils/scope/chemin/temps\n" +
              "Confidentialité · classification, opt-in cloud, plafonds tokens/coût\n" +
              "Sortie · JSON Schema, tool calls et réparation bornée\n" +
              "Injection · contenu projet traité comme données étiquetées\n" +
              "Hors ligne · core manuel, build et runtime indépendants";
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

    private static string PrettyJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return json;
        }
    }

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

    private static VBoxContainer AddScrollableCard(Container parent, float width, out ScrollContainer scroll)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(width, 0),
            SizeFlagsHorizontal = Control.SizeFlags.Fill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", Card("171221", "47365f", 1, 10));
        parent.AddChild(panel);
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 13);
        panel.AddChild(margin);
        scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
        };
        margin.AddChild(scroll);
        var content = new VBoxContainer { CustomMinimumSize = new Vector2(width - 42, 0) };
        content.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(content);
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
