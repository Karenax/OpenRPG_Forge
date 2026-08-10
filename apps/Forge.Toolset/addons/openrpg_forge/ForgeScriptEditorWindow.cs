#if TOOLS
using Forge.ModuleFormat.Workspaces;
using Forge.Scripting;
using Godot;

namespace Forge.Toolset.Editor;

public partial class ForgeScriptEditorWindow : Window
{
    private static readonly ForgeScriptEventContract DamageEvent = new(
        Guid.Parse("12000000-0000-4000-8000-000000000010"), 1, "forge:events",
        "creature.damaged", "int", true);
    private Func<ProjectSession?>? _getSession;
    private Action<Window>? _brandWindow;
    private ItemList? _sources;
    private CodeEdit? _source;
    private ItemList? _diagnostics;
    private ItemList? _bindings;
    private ItemList? _capabilities;
    private Label? _identity;
    private Label? _status;
    private Label? _console;
    private Button? _apply;
    private Button? _undo;
    private Button? _redo;
    private OptionButton? _policyPreset;
    private Label? _policySummary;
    private Button? _policyApply;
    private Button? _policyUndo;
    private Button? _policyRedo;
    private ForgeScriptSourceHistory? _history;
    private ForgeScriptExecutionPolicyHistory _policyHistory = new(ForgeScriptExecutionPolicy.Default);
    private ForgeScriptPermissionsWindow? _permissionsWindow;
    private ForgeScriptDiagnostic[] _compileDiagnostics = [];
    private string? _capturePath;
    private double _captureDelay;
    private bool _captureOpened;

    internal int DiagnosticCount => _diagnostics?.ItemCount ?? 0;
    internal int BindingCount => _history?.Document.EventBindings.Count ?? 0;
    internal int CapabilityCount => _capabilities?.ItemCount ?? 0;
    internal bool LastCompileSucceeded { get; private set; }
    internal bool LastSandboxSucceeded { get; private set; }
    internal bool ModulePermissionsSmokeSucceeded { get; private set; }

    public void Initialize(Func<ProjectSession?> getSession, Action<Window> brandWindow)
    {
        _getSession = getSession ?? throw new ArgumentNullException(nameof(getSession));
        _brandWindow = brandWindow ?? throw new ArgumentNullException(nameof(brandWindow));
        Title = "ForgeScript · OpenRPG Forge";
        MinSize = new Vector2I(1440, 880);
        Size = new Vector2I(1440, 880);
        Transient = true;
        Exclusive = false;
        Unresizable = true;
        Visible = false;
        CloseRequested += Hide;
        BuildUi();
        _permissionsWindow = new ForgeScriptPermissionsWindow();
        _permissionsWindow.Initialize(getSession, brandWindow);
        AddChild(_permissionsWindow);
        var captureArgument = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--script-editor-ui-capture=", StringComparison.Ordinal));
        if (captureArgument is not null && captureArgument.Length > "--script-editor-ui-capture=".Length)
        {
            _capturePath = captureArgument["--script-editor-ui-capture=".Length..];
            SetProcess(true);
        }
    }

    public override void _Process(double delta)
    {
        if (_capturePath is null) return;
        _captureDelay += delta;
        if (!_captureOpened && _captureDelay >= 1)
        {
            Open();
            Compile();
            _captureOpened = true;
            return;
        }
        if (!_captureOpened || _captureDelay < 2.5) return;
        var destination = Path.GetFullPath(_capturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(destination);
        GD.Print($"OPENRPG_FORGE_SCRIPT_EDITOR_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} " +
                 $"path={destination} size={image.GetWidth()}x{image.GetHeight()}");
        _capturePath = null;
        SetProcess(false);
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    public void Open()
    {
        LoadWorkspace();
        Exclusive = true;
        PopupCentered(new Vector2I(1440, 880));
        _brandWindow?.Invoke(this);
    }

    internal bool RunForSmokeTest()
    {
        LoadSample();
        Preview();
        Compile();
        RunSandbox();
        PreviewPolicy();
        ApplyPolicy();
        UndoPolicy();
        RedoPolicy();
        var handlers = ForgeScriptSourceValidator.ResolveHandlers(_history!.Document, "creature.combat",
            "creature.damaged", ForgeScriptEventAuthority.Server, condition => condition == "is_enraged");
        var denied = ForgeScriptCapabilityManifest.ValidateSource("void main() { file_open(\"x\"); }");
        ModulePermissionsSmokeSucceeded = _permissionsWindow is not null && _permissionsWindow.RunForSmokeTest();
        return LastCompileSucceeded && LastSandboxSucceeded && handlers.Count == 2 && BindingCount == 2 &&
               CapabilityCount == 6 && denied.Count == 1 && _history.Preview(Command()).Succeeded &&
               _policyHistory.Revision == 3 && ForgeScriptExecutionPolicyValidator.Validate(_policyHistory.Policy).Count == 0 &&
               ModulePermissionsSmokeSucceeded;
    }

    private void LoadWorkspace()
    {
        _sources!.Clear();
        var session = _getSession?.Invoke();
        if (session is null)
        {
            SetPolicy(ForgeScriptExecutionPolicy.Default);
            LoadSample();
            SetStatus("Aucun projet ouvert · exemple en lecture locale", warning: true);
            return;
        }
        LoadPolicy(session);
        var scripts = Path.Combine(session.RootDirectory, "scripts");
        var paths = Directory.Exists(scripts)
            ? Directory.GetFiles(scripts, "*.as", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray()
            : [];
        foreach (var path in paths) _sources.AddItem(Path.GetRelativePath(session.RootDirectory, path).Replace('\\', '/'));
        if (paths.Length == 0)
        {
            LoadSample();
            _sources.AddItem("＋ Nouvelle source · scripts/events.as");
            _sources.SetItemTooltip(0, "Créer la source canonique scripts/events.as via une transaction prévisualisée.");
            SetStatus("État vide · aucune source .as canonique ; l’exemple n’est pas encore enregistré", warning: true);
            return;
        }
        try
        {
            var relative = Path.GetRelativePath(session.RootDirectory, paths[0]).Replace('\\', '/');
            var document = new ForgeScriptSourceStore(session.RootDirectory).Load(relative);
            SetDocument(document);
            _sources.Select(0);
            SetStatus($"✓ Source chargée · {relative}", warning: false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or ArgumentException)
        {
            LoadSample();
            ShowDiagnostics([new("script.source.load_failed", "script/source", null, "error", exception.Message)]);
            SetStatus("Source invalide · consultez le diagnostic cliquable", warning: true);
        }
    }

    private void LoadSample()
    {
        var first = Binding("OnDamaged", 100, "is_enraged");
        var second = Binding("LogDamage", 20, null);
        SetDocument(new ForgeScriptSourceDocument(
            Guid.Parse("12000000-0000-4000-8000-000000000001"), 1, "forge:script-editor",
            "scripts/events.as",
            "void OnDamaged(const int &in e)\n{\n    int observed = e;\n    say(\"Cible touchee\");\n}\n\n" +
            "void LogDamage(const int &in e)\n{\n    int observed = e;\n    variable(\"combat.last_damage\", \"received\");\n}\n\n" +
            "void main()\n{\n    AreaRef area = current_area();\n    EntityRef hero = find_entity(area, \"hero\");\n" +
            "    Vec3 goal = vec3(6, 0, 4);\n    local_set_int(hero, \"quest.stage\", 2);\n" +
            "    MovementTaskRef task = move_to(hero, goal, \"run\");\n    cancel_movement(task);\n" +
            "    say(\"ForgeScript pret\");\n}\n",
            "1.0.0", ForgeScriptSourceDistributionPolicy.SourceAndBytecode, true, [], [first, second]));
    }

    private void SetDocument(ForgeScriptSourceDocument document)
    {
        _history = new ForgeScriptSourceHistory(document, new NativeForgeScriptRuntime(), [DamageEvent]);
        PopulateDocument();
    }

    private void LoadPolicy(ProjectSession session)
    {
        try
        {
            var root = Path.Combine(session.RootDirectory, ".forge");
            SetPolicy(File.Exists(Path.Combine(root, "forge-script-policy.json"))
                ? new ForgeScriptExecutionPolicyStore(root).Load()
                : ForgeScriptExecutionPolicy.Default);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or System.Text.Json.JsonException)
        {
            SetPolicy(ForgeScriptExecutionPolicy.Default);
            ShowDiagnostics([new("script.policy.load_failed", "script/policy", null, "error", exception.Message)]);
        }
    }

    private void SetPolicy(ForgeScriptExecutionPolicy policy)
    {
        _policyHistory = new(policy);
        if (_policyPreset is not null) _policyPreset.Selected = 1;
        PopulatePolicy();
    }

    private ForgeScriptExecutionPolicy SelectedPolicy() => (_policyPreset?.Selected ?? 1) switch
    {
        0 => _policyHistory.Policy with
        {
            HandlerInstructionLimit = 5_000,
            TickInstructionLimit = 25_000,
            ModuleInstructionLimit = 500_000,
            ContextMemoryLimitBytes = 4 * 1024 * 1024,
            ModuleMemoryLimitBytes = 64L * 1024 * 1024
        },
        2 => _policyHistory.Policy with
        {
            HandlerInstructionLimit = 25_000,
            TickInstructionLimit = 125_000,
            ModuleInstructionLimit = 2_500_000,
            ContextMemoryLimitBytes = 16 * 1024 * 1024,
            ModuleMemoryLimitBytes = 256L * 1024 * 1024
        },
        _ => _policyHistory.Policy with
        {
            HandlerInstructionLimit = 10_000,
            TickInstructionLimit = 50_000,
            ModuleInstructionLimit = 1_000_000,
            ContextMemoryLimitBytes = 8 * 1024 * 1024,
            ModuleMemoryLimitBytes = 128L * 1024 * 1024
        },
    };

    private ForgeScriptExecutionPolicyCommand PolicyCommand() => new(Guid.NewGuid(), _policyHistory.Revision,
        SelectedPolicy(), "script-editor-policy");

    private void PreviewPolicy()
    {
        var result = _policyHistory.Preview(PolicyCommand());
        _policyApply!.Disabled = !result.Succeeded;
        SetStatus(result.Succeeded ? $"✓ Politique prévisualisée · {result.SemanticDiff}" :
            "Politique de sandbox refusée · aucun changement", !result.Succeeded);
    }

    private void ApplyPolicy()
    {
        var result = _policyHistory.Apply(PolicyCommand());
        if (!result.Succeeded) return;
        PersistPolicy();
        PopulatePolicy();
        SetStatus($"✓ Politique de sandbox appliquée · révision {result.Revision}", false);
    }

    private void UndoPolicy()
    {
        var result = _policyHistory.Undo();
        if (!result.Succeeded) return;
        PersistPolicy();
        PopulatePolicy();
        SetStatus($"✓ Politique annulée · révision {result.Revision}", false);
    }

    private void RedoPolicy()
    {
        var result = _policyHistory.Redo();
        if (!result.Succeeded) return;
        PersistPolicy();
        PopulatePolicy();
        SetStatus($"✓ Politique rétablie · révision {result.Revision}", false);
    }

    private void PersistPolicy()
    {
        var session = _getSession?.Invoke();
        if (session is not null)
            new ForgeScriptExecutionPolicyStore(Path.Combine(session.RootDirectory, ".forge")).Save(_policyHistory.Policy);
    }

    private void PopulatePolicy()
    {
        if (_policySummary is null) return;
        var policy = _policyHistory.Policy;
        _policySummary.Text = $"{policy.HandlerInstructionLimit:N0} handler · {policy.TickInstructionLimit:N0} tick\n" +
                              $"{policy.ContextMemoryLimitBytes / 1_048_576} Mio contexte · seed {policy.DeterministicSeed}\n" +
                              $"tick {policy.SimulationTick} · {policy.Authority} · API {policy.ApiVersion}";
        _policyUndo!.Disabled = !_policyHistory.CanUndo;
        _policyRedo!.Disabled = !_policyHistory.CanRedo;
        _policyApply!.Disabled = true;
    }

    private void PopulateDocument()
    {
        var document = _history!.Document;
        _source!.Text = document.Source;
        _identity!.Text = $"UUID · {document.Id:D}\nSchéma · v{document.SchemaVersion}\n" +
                          $"Langage · {ForgeScriptSourceDocument.Language} {ForgeScriptSourceDocument.DialectVersion}\n" +
                          $"API · {document.ApiVersion}\nSource · {document.RelativePath}\n" +
                          $"Distribution · {document.Distribution}\nProvenance · {document.Provenance}";
        _capabilities!.Clear();
        foreach (var capability in ForgeScriptCapabilityManifest.Capabilities)
        {
            _capabilities.AddItem($"{capability.DisplayName} · {capability.ApiNames.Count} API");
            _capabilities.SetItemTooltip(_capabilities.ItemCount - 1,
                $"{string.Join(" · ", capability.ApiNames)}\nAutorité {capability.Authority}\nQuota {capability.Quota}");
        }
        _bindings!.Clear();
        foreach (var binding in document.EventBindings.OrderByDescending(item => item.Priority).ThenBy(item => item.Id))
        {
            _bindings.AddItem($"P{binding.Priority} · {binding.EventName} · " +
                              $"{binding.Handler}({binding.PayloadType}) · {binding.Authority}");
            _bindings.SetItemTooltip(_bindings.ItemCount - 1,
                $"UUID {binding.Id:D}\nComposant {binding.ComponentId}\n" +
                $"Entrée {ForgeScriptSourceValidator.EntryDeclaration(binding.Handler, binding.PayloadType)}\n" +
                $"Condition {binding.EnableCondition ?? "toujours"}\nProvenance {binding.Provenance}");
        }
        _undo!.Disabled = !_history.CanUndo;
        _redo!.Disabled = !_history.CanRedo;
        _apply!.Disabled = true;
        ShowDiagnostics([]);
    }

    private ForgeScriptEventBinding Binding(string handler, int priority, string? condition) => new(
        Guid.NewGuid(), 1, "forge:script-editor", "creature.combat", DamageEvent.EventName, handler,
        DamageEvent.PayloadType, priority, condition, ForgeScriptEventAuthority.Server, true);

    private ForgeScriptSourceCommand Command() => new(Guid.NewGuid(), _history!.Revision,
        _history.Document with { Source = _source!.Text }, "script-editor");

    private void Preview()
    {
        var result = _history!.Preview(Command());
        ShowDiagnostics(result.Diagnostics);
        _apply!.Disabled = !result.Succeeded;
        _console!.Text = result.Succeeded
            ? $"PRÉVISUALISATION\n{result.SemanticDiff}\nAucun fichier écrit."
            : "PRÉVISUALISATION REFUSÉE\nCorrigez les diagnostics avant l’enregistrement.";
        SetStatus(result.Succeeded ? "✓ Transaction source valide · prête à enregistrer" :
            "Transaction refusée · aucune mutation partielle", !result.Succeeded);
    }

    private void Apply()
    {
        var result = _history!.Apply(Command());
        if (!result.Succeeded)
        {
            ShowDiagnostics(result.Diagnostics);
            return;
        }
        var session = _getSession?.Invoke();
        if (session is null)
        {
            SetStatus("Aucun projet ouvert · enregistrement indisponible", true);
            return;
        }
        new ForgeScriptSourceStore(session.RootDirectory).Save(result.Document);
        PopulateDocument();
        SetStatus($"✓ Source enregistrée atomiquement · révision {result.Revision}", false);
    }

    private void Undo()
    {
        var result = _history!.Undo();
        if (result.Succeeded) PersistHistory(result, "Annulation appliquée");
    }

    private void Redo()
    {
        var result = _history!.Redo();
        if (result.Succeeded) PersistHistory(result, "Rétablissement appliqué");
    }

    private void PersistHistory(ForgeScriptSourceMutationResult result, string message)
    {
        var session = _getSession?.Invoke();
        if (session is not null) new ForgeScriptSourceStore(session.RootDirectory).Save(result.Document);
        PopulateDocument();
        SetStatus($"✓ {message} · révision {result.Revision}", false);
    }

    private void Compile()
    {
        var runtime = new NativeForgeScriptRuntime();
        var result = runtime.Compile(_source!.Text);
        _compileDiagnostics = result.Diagnostics.ToArray();
        LastCompileSucceeded = result.Success;
        ShowDiagnostics(result.Diagnostics.Select(item => new ForgeScriptSourceDiagnostic("script.compile",
            $"{_history!.Document.RelativePath}:{Math.Max(1, item.Line)}:{Math.Max(1, item.Column)}",
            _history.Document.Id, item.Severity, item.Message)).ToArray());
        _console!.Text = result.Success
            ? $"COMPILATION RÉUSSIE\nVM {runtime.VirtualMachineVersion} · API {runtime.ApiVersion} · " +
              $"bytecode {result.Artifact!.Bytecode.Length} octets\nCache : source + API + compilateur"
            : $"COMPILATION REFUSÉE\n{result.Diagnostics.Count} diagnostic(s) structuré(s)";
        SetStatus(result.Success ? "✓ Compilation terminée · bytecode jetable prêt" :
            "Compilation impossible · source canonique conservée", !result.Success);
    }

    private void RunSandbox()
    {
        var runtime = new NativeForgeScriptRuntime();
        var compiled = runtime.Compile(_source!.Text);
        if (!compiled.Success || compiled.Artifact is null)
        {
            LastSandboxSucceeded = false;
            Compile();
            return;
        }
        var policy = _policyHistory.Policy;
        var executed = runtime.Execute(compiled.Artifact, new ForgeScriptQuotas(policy.HandlerInstructionLimit,
            policy.TimeoutMilliseconds, policy.RecursionLimit, policy.SpawnLimit, policy.ContextMemoryLimitBytes,
            policy.DeterministicSeed, policy.SimulationTick, policy.SimulationTimeMilliseconds));
        LastSandboxSucceeded = executed.Status == ForgeScriptExecutionStatus.Completed;
        _console!.Text = $"TEST SANDBOX · {executed.Status}\n" +
                         $"Quotas : {policy.HandlerInstructionLimit:N0} instructions · " +
                         $"{policy.ContextMemoryLimitBytes / 1_048_576} Mio · seed {policy.DeterministicSeed} · tick {policy.SimulationTick}\n" +
                         $"Télémétrie : {executed.Telemetry?.Instructions ?? 0} instructions · " +
                         $"pic {executed.Telemetry?.PeakMemoryBytes ?? 0:N0} octets\n" +
                         string.Join("\n", executed.Events.Select(item => $"{item.Kind} · {item.Value}"));
        SetStatus(LastSandboxSucceeded ? "✓ Test sandbox terminé sans mutation du projet" :
            "Test interrompu proprement par le sandbox", !LastSandboxSucceeded);
    }

    private void ShowDiagnostics(IReadOnlyList<ForgeScriptSourceDiagnostic> diagnostics)
    {
        _diagnostics!.Clear();
        foreach (var diagnostic in diagnostics)
        {
            _diagnostics.AddItem($"{diagnostic.Severity.ToUpperInvariant()} · {diagnostic.Code} · " +
                                 $"{diagnostic.Path} — {diagnostic.Message}");
            _diagnostics.SetItemTooltip(_diagnostics.ItemCount - 1,
                $"UUID {diagnostic.SubjectId?.ToString("D") ?? "document"}\nChemin {diagnostic.Path}");
        }
        if (diagnostics.Count == 0)
        {
            _diagnostics.AddItem("✓ Aucun diagnostic · source prête");
            _diagnostics.SetItemSelectable(0, false);
        }
    }

    private void JumpToDiagnostic(long index)
    {
        if (index < 0 || index >= _compileDiagnostics.Length) return;
        _source!.SetCaretLine(Math.Max(0, _compileDiagnostics[index].Line - 1));
        _source.GrabFocus();
    }

    private void SetStatus(string text, bool warning)
    {
        _status!.Text = text;
        _status.AddThemeColorOverride("font_color", warning ? new Color("ffc66d") : new Color("83d6ae"));
    }

    private void BuildUi()
    {
        var background = new PanelContainer { AnchorRight = 1, AnchorBottom = 1 };
        background.AddThemeStyleboxOverride("panel", Card("0d0b17", "62458a"));
        AddChild(background);
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 20);
        background.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);
        var title = new Label { Text = "FORGESCRIPT · SOURCES, API SÛRE & SANDBOX" };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(title);
        var subtitle = new Label
        {
            Text = "Créer la logique du monde avec handles opaques, API versionnée et tests sous quotas avant enregistrement.",
        };
        subtitle.AddThemeColorOverride("font_color", new Color("c3b8d8"));
        root.AddChild(subtitle);

        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 10);
        root.AddChild(body);
        var left = Panel(body, 250);
        left.AddChild(Section("SOURCES CANONIQUES"));
        _sources = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 125),
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
        };
        left.AddChild(_sources);
        left.AddChild(Section("IDENTITÉ & GOUVERNANCE"));
        _identity = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _identity.AddThemeColorOverride("font_color", new Color("bdaed4"));
        left.AddChild(_identity);
        left.AddChild(Section("BUDGETS & DÉTERMINISME"));
        _policyPreset = new OptionButton();
        _policyPreset.AddItem("Strict · 5k / 4 Mio");
        _policyPreset.AddItem("Standard · 10k / 8 Mio");
        _policyPreset.AddItem("Large · 25k / 16 Mio");
        _policyPreset.Selected = 1;
        _policyPreset.ItemSelected += _ => _policyApply!.Disabled = true;
        left.AddChild(_policyPreset);
        _policySummary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _policySummary.AddThemeColorOverride("font_color", new Color("9dddc1"));
        left.AddChild(_policySummary);
        var policyPreview = Button(left, "Prévisualiser politique", PreviewPolicy);
        policyPreview.CustomMinimumSize = new Vector2(0, 30);
        _policyApply = Button(left, "Appliquer politique", ApplyPolicy, accent: true);
        _policyApply.CustomMinimumSize = new Vector2(0, 30);
        var policyHistory = new HBoxContainer();
        left.AddChild(policyHistory);
        _policyUndo = Button(policyHistory, "Annuler", UndoPolicy);
        _policyUndo.CustomMinimumSize = new Vector2(90, 28);
        _policyRedo = Button(policyHistory, "Rétablir", RedoPolicy);
        _policyRedo.CustomMinimumSize = new Vector2(90, 28);
        PopulatePolicy();

        var center = Panel(body, 690, expand: true);
        var editorHeader = new HBoxContainer();
        editorHeader.AddChild(Section("ÉDITEUR C-LIKE STATIQUEMENT TYPÉ"));
        center.AddChild(editorHeader);
        _source = new CodeEdit
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 450),
            DrawTabs = true,
            GuttersDrawLineNumbers = true,
        };
        _source.TextChanged += () => _apply!.Disabled = true;
        center.AddChild(_source);
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 6);
        center.AddChild(actions);
        Button(actions, "Prévisualiser", Preview);
        Button(actions, "Compiler", Compile);
        Button(actions, "Tester sandbox", RunSandbox, accent: true);
        Button(actions, "Permissions module", () => _permissionsWindow?.Open());
        _apply = Button(actions, "Enregistrer", Apply, accent: true);
        _apply.Disabled = true;
        _undo = Button(actions, "Annuler", Undo);
        _redo = Button(actions, "Rétablir", Redo);
        _console = new Label
        {
            Text = "CONSOLE\nCompilez ou testez la source ; aucun fichier n’est écrit par ces actions.",
            CustomMinimumSize = new Vector2(0, 105),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _console.AddThemeColorOverride("font_color", new Color("9dddc1"));
        center.AddChild(_console);

        var right = Panel(body, 390);
        right.AddChild(Section("API P0 ALLOWLISTÉE"));
        var capabilityHelp = new Label
        {
            Text = "Aucun fichier · réseau · processus · moteur · .NET\nHandles générationnels · RNG et horloge déterministes",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        capabilityHelp.AddThemeColorOverride("font_color", new Color("83d6ae"));
        right.AddChild(capabilityHelp);
        _capabilities = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 155),
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
        };
        right.AddChild(_capabilities);
        right.AddChild(Section("ÉVÉNEMENTS PAR OBJET"));
        var eventHelp = new Label
        {
            Text = "Multi-handlers · priorité · condition · autorité explicite\nEntrée : void Handler(const Payload &in e)",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        eventHelp.AddThemeColorOverride("font_color", new Color("bdaed4"));
        right.AddChild(eventHelp);
        _bindings = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 115),
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
        };
        right.AddChild(_bindings);
        right.AddChild(Section("DIAGNOSTICS CLIQUABLES"));
        _diagnostics = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _diagnostics.ItemSelected += JumpToDiagnostic;
        right.AddChild(_diagnostics);
        _status = new Label { Text = "Initialisation…", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        root.AddChild(_status);
    }

    private static VBoxContainer Panel(Container parent, float width, bool expand = false)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(width, 0),
            SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkBegin,
        };
        panel.AddThemeStyleboxOverride("panel", Card("151020", "47365f"));
        parent.AddChild(panel);
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 12);
        panel.AddChild(margin);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        margin.AddChild(content);
        return content;
    }

    private static Label Section(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", new Color("ffb276"));
        return label;
    }

    private static Button Button(Container parent, string text, Action action, bool accent = false)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(105, 34) };
        if (accent) button.AddThemeStyleboxOverride("normal", Card("d84f32", "ff9a73"));
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    private static StyleBoxFlat Card(string background, string border) => new()
    {
        BgColor = new Color(background),
        BorderColor = new Color(border),
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 8,
        CornerRadiusTopRight = 8,
        CornerRadiusBottomLeft = 8,
        CornerRadiusBottomRight = 8,
    };
}
#endif
