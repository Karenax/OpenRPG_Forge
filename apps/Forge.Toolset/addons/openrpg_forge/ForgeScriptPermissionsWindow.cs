#if TOOLS
using Forge.ModuleFormat.Workspaces;
using Forge.Scripting;
using Godot;

namespace Forge.Toolset.Editor;

public partial class ForgeScriptPermissionsWindow : Window
{
    private Func<ProjectSession?>? _getSession;
    private Action<Window>? _brandWindow;
    private readonly Dictionary<string, CheckButton> _capabilities = new(StringComparer.Ordinal);
    private Label? _identity;
    private Label? _status;
    private Label? _summary;
    private Label? _console;
    private ItemList? _grants;
    private ItemList? _bindings;
    private ItemList? _diagnostics;
    private CheckButton? _requireSignature;
    private CheckButton? _allowUnsignedDevelopment;
    private Button? _apply;
    private Button? _undo;
    private Button? _redo;
    private ForgeScriptModulePermissionHistory? _history;
    private string? _capturePath;
    private double _captureDelay;
    private bool _captureOpened;
    private bool _suppressPersistence;

    internal bool LastSandboxSucceeded { get; private set; }
    internal int VisibleCapabilityCount => _capabilities.Count;

    public void Initialize(Func<ProjectSession?> getSession, Action<Window> brandWindow)
    {
        _getSession = getSession ?? throw new ArgumentNullException(nameof(getSession));
        _brandWindow = brandWindow ?? throw new ArgumentNullException(nameof(brandWindow));
        Title = "Permissions ForgeScript · OpenRPG Forge";
        MinSize = new Vector2I(1280, 760);
        Size = new Vector2I(1280, 760);
        Transient = true;
        Exclusive = false;
        Unresizable = true;
        Visible = false;
        CloseRequested += Hide;
        BuildUi();
        var capture = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--script-permissions-ui-capture=", StringComparison.Ordinal));
        if (capture is not null && capture.Length > "--script-permissions-ui-capture=".Length)
        {
            _capturePath = capture["--script-permissions-ui-capture=".Length..];
            SetProcess(true);
        }
    }

    public override void _Process(double delta)
    {
        if (_capturePath is null) return;
        _captureDelay += delta;
        if (!_captureOpened && _captureDelay >= 1)
        {
            if (GetParent() is Window parent && !parent.Visible)
                parent.PopupCentered(new Vector2I(1440, 880));
            Open();
            Preview();
            RunSandbox();
            _captureOpened = true;
            return;
        }
        if (!_captureOpened || _captureDelay < 2.5) return;
        var destination = Path.GetFullPath(_capturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(destination);
        GD.Print($"OPENRPG_FORGE_SCRIPT_PERMISSIONS_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} " +
                 $"path={destination} size={image.GetWidth()}x{image.GetHeight()}");
        _capturePath = null;
        SetProcess(false);
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    public void Open()
    {
        LoadWorkspace();
        Exclusive = true;
        PopupCentered(new Vector2I(1280, 760));
        _brandWindow?.Invoke(this);
    }

    internal bool RunForSmokeTest()
    {
        _suppressPersistence = true;
        try
        {
            LoadPolicy(ForgeScriptModulePermissionPolicy.CreateDefault(
                Guid.Parse("15000000-0000-4000-8000-000000000015")));
            Preview();
            Apply();
            Undo();
            Redo();
            RunSandbox();
            var policy = _history!.Policy;
            var denied = ForgeScriptModuleAuthorizationService.Authorize(policy,
                Request(policy, new HashSet<string>(["movement"], StringComparer.Ordinal), true));
            var optional = ForgeScriptModuleAuthorizationService.Authorize(null,
                Request(policy, new HashSet<string>(["world"], StringComparer.Ordinal), true) with
                { RequiredAtRuntime = false });
            return _history.Revision == 3 && LastSandboxSucceeded && VisibleCapabilityCount == 6 &&
                   denied.Diagnostics.SingleOrDefault()?.Code == "script.permission.capability_denied" &&
                   optional.Succeeded && !optional.ShouldExecute;
        }
        finally
        {
            _suppressPersistence = false;
        }
    }

    private void LoadWorkspace()
    {
        var session = _getSession?.Invoke();
        if (session is null)
        {
            LoadPolicy(ForgeScriptModulePermissionPolicy.CreateDefault(
                Guid.Parse("15000000-0000-4000-8000-000000000015")));
            SetStatus("État vide · exemple local, ouvrez un module pour enregistrer", true);
            return;
        }
        var store = new ForgeScriptModulePermissionStore(session.RootDirectory);
        try
        {
            LoadPolicy(File.Exists(store.Path)
                ? store.Load()
                : ForgeScriptModulePermissionPolicy.CreateDefault(session.Project.Module.Id.Value));
            SetStatus(File.Exists(store.Path)
                ? "✓ Manifeste canonique chargé"
                : "État vide · manifeste par défaut prêt à prévisualiser", !File.Exists(store.Path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException)
        {
            LoadPolicy(ForgeScriptModulePermissionPolicy.CreateDefault(session.Project.Module.Id.Value));
            _diagnostics!.Clear();
            _diagnostics.AddItem($"ERROR · script.permission.document_invalid · {exception.Message}");
            SetStatus("Manifeste illisible · aucune mutation effectuée", true);
        }
    }

    private void LoadPolicy(ForgeScriptModulePermissionPolicy policy)
    {
        _history = new ForgeScriptModulePermissionHistory(policy);
        _requireSignature!.ButtonPressed = policy.RequireSignedDistribution;
        _allowUnsignedDevelopment!.ButtonPressed = policy.AllowUnsignedDevelopment;
        foreach (var pair in _capabilities)
            pair.Value.ButtonPressed = policy.AllowedCapabilities.Contains(pair.Key);
        Populate();
    }

    private ForgeScriptModulePermissionPolicy PolicyFromControls()
    {
        var current = _history!.Policy;
        var allowed = _capabilities.Where(pair => pair.Value.ButtonPressed).Select(pair => pair.Key)
            .ToHashSet(StringComparer.Ordinal);
        return current with
        {
            DenyByDefault = true,
            ServerEnforced = true,
            RequireSignedDistribution = _requireSignature!.ButtonPressed,
            AllowUnsignedDevelopment = _allowUnsignedDevelopment!.ButtonPressed,
            AllowedCapabilities = allowed,
            KitGrants = current.KitGrants.Select(item => item with
            { Capabilities = item.Capabilities.Intersect(allowed).ToHashSet(StringComparer.Ordinal) }).ToArray(),
            EventBindings = current.EventBindings.Select(item => item with
            { Capabilities = item.Capabilities.Intersect(allowed).ToHashSet(StringComparer.Ordinal) }).ToArray(),
        };
    }

    private ForgeScriptModulePermissionCommand Command() => new(Guid.NewGuid(), _history!.Revision,
        PolicyFromControls(), "script-permissions-editor");

    private void Preview()
    {
        var result = _history!.Preview(Command());
        ShowDiagnostics(result.Diagnostics);
        _apply!.Disabled = !result.Succeeded;
        _console!.Text = result.Succeeded
            ? $"PRÉVISUALISATION\n{result.SemanticDiff}\nAucun fichier écrit."
            : "PRÉVISUALISATION REFUSÉE\nCorrigez les diagnostics listés.";
        SetStatus(result.Succeeded ? "✓ Transaction valide · prête à appliquer" :
            "Configuration refusée · aucune mutation partielle", !result.Succeeded);
    }

    private void Apply()
    {
        var result = _history!.Apply(Command());
        if (!result.Succeeded)
        {
            ShowDiagnostics(result.Diagnostics);
            return;
        }
        Persist();
        Populate();
        SetStatus($"✓ Permissions appliquées · révision {result.Revision}", false);
    }

    private void Undo()
    {
        var result = _history!.Undo();
        if (!result.Succeeded) return;
        Persist();
        Populate();
        SetStatus($"✓ Permissions annulées · révision {result.Revision}", false);
    }

    private void Redo()
    {
        var result = _history!.Redo();
        if (!result.Succeeded) return;
        Persist();
        Populate();
        SetStatus($"✓ Permissions rétablies · révision {result.Revision}", false);
    }

    private void Persist()
    {
        if (_suppressPersistence) return;
        var session = _getSession?.Invoke();
        if (session is not null)
            new ForgeScriptModulePermissionStore(session.RootDirectory).Save(_history!.Policy);
    }

    private void RunSandbox()
    {
        var policy = PolicyFromControls();
        var runtime = new NativeForgeScriptRuntime();
        var service = new ForgeScriptModuleExecutionService(runtime);
        var result = service.Execute(policy,
            Request(policy, new HashSet<string>(["world"], StringComparer.Ordinal), true),
            "void main() { say(\"permissions-r015\"); }");
        LastSandboxSucceeded = result.Authorization.Succeeded &&
                               result.Execution?.Status == ForgeScriptExecutionStatus.Completed;
        _console!.Text = LastSandboxSucceeded
            ? "TEST AUTORITÉ + SANDBOX · RÉUSSI\nSignature fiable · serveur · module/kit/événement concordants\n" +
              $"Capacités accordées : {string.Join(", ", result.Authorization.GrantedCapabilities.Order())}"
            : "TEST AUTORITÉ + SANDBOX · REFUSÉ\n" +
              string.Join("\n", result.Authorization.Diagnostics.Select(item => $"{item.Code} · {item.Message}"));
        SetStatus(LastSandboxSucceeded ? "✓ Autorisation et exécution sandbox validées" :
            "Exécution refusée proprement par la politique", !LastSandboxSucceeded);
    }

    private static ForgeScriptModuleAuthorizationRequest Request(
        ForgeScriptModulePermissionPolicy policy,
        IReadOnlySet<string> requested,
        bool signerTrusted) => new(policy.ModuleId, "core", "module.start", "main", requested,
        ForgeScriptEventAuthority.Server, signerTrusted, false, policy.RequiredAtRuntime);

    private void Populate()
    {
        var policy = _history!.Policy;
        _identity!.Text = $"UUID · {policy.Id:D}\nModule · {policy.ModuleId:D}\nSchéma · v{policy.SchemaVersion}\n" +
                          $"API · {policy.ApiVersion}\nProvenance · {policy.Provenance}\n" +
                          $"Chemin · {ForgeScriptModulePermissionPolicy.RelativePath}";
        _summary!.Text = "REFUS PAR DÉFAUT · AUTORITÉ SERVEUR\n" +
                         $"{policy.AllowedCapabilities.Count} capacité(s) · {policy.KitGrants.Count} kit(s) · " +
                         $"{policy.EventBindings.Count} liaison(s)\nDistribution signée · " +
                         (policy.RequireSignedDistribution ? "requise" : "désactivée");
        _grants!.Clear();
        foreach (var grant in policy.KitGrants)
            _grants.AddItem($"{grant.KitId} · {string.Join(", ", grant.Capabilities.Order())}");
        _bindings!.Clear();
        foreach (var binding in policy.EventBindings)
            _bindings.AddItem($"{binding.EventName} → {binding.Handler} · {binding.KitId} · {binding.Authority}");
        _undo!.Disabled = !_history.CanUndo;
        _redo!.Disabled = !_history.CanRedo;
        _apply!.Disabled = true;
        ShowDiagnostics(ForgeScriptModulePermissionValidator.Validate(policy));
    }

    private void ShowDiagnostics(IReadOnlyList<ForgeScriptModulePermissionDiagnostic> diagnostics)
    {
        _diagnostics!.Clear();
        foreach (var diagnostic in diagnostics)
        {
            _diagnostics.AddItem($"{diagnostic.Severity.ToUpperInvariant()} · {diagnostic.Code} · {diagnostic.Path}");
            _diagnostics.SetItemTooltip(_diagnostics.ItemCount - 1,
                $"{diagnostic.Message}\nUUID {diagnostic.SubjectId?.ToString("D") ?? "document"}");
        }
        if (diagnostics.Count == 0)
        {
            _diagnostics.AddItem("✓ Aucun diagnostic · manifeste prêt");
            _diagnostics.SetItemSelectable(0, false);
        }
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
        var title = new Label { Text = "FORGESCRIPT · PERMISSIONS PAR MODULE" };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(title);
        var subtitle = new Label
        {
            Text = "Accorder explicitement les capacités d’un kit et de ses événements avant toute exécution serveur.",
        };
        subtitle.AddThemeColorOverride("font_color", new Color("c3b8d8"));
        root.AddChild(subtitle);

        var cards = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        cards.AddThemeConstantOverride("separation", 10);
        root.AddChild(cards);
        var left = Panel(cards, 310);
        left.AddChild(Section("IDENTITÉ CANONIQUE"));
        _identity = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _identity.AddThemeColorOverride("font_color", new Color("bdaed4"));
        left.AddChild(_identity);
        left.AddChild(Section("GARDE-FOUS"));
        var immutable = new Label
        {
            Text = "✓ Refus des capacités non déclarées\n✓ Décision imposée par le serveur",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        immutable.AddThemeColorOverride("font_color", new Color("83d6ae"));
        left.AddChild(immutable);
        _requireSignature = new CheckButton { Text = "Signature fiable en production", ButtonPressed = true };
        left.AddChild(_requireSignature);
        _allowUnsignedDevelopment = new CheckButton
        {
            Text = "Autoriser un module local en développement",
            ButtonPressed = true,
        };
        left.AddChild(_allowUnsignedDevelopment);
        left.AddChild(Section("RÉSUMÉ D’EXÉCUTION"));
        _summary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _summary.AddThemeColorOverride("font_color", new Color("9dddc1"));
        left.AddChild(_summary);

        var center = Panel(cards, 430, true);
        center.AddChild(Section("CAPACITÉS EXPLICITES"));
        foreach (var capability in ForgeScriptCapabilityManifest.Capabilities)
        {
            var toggle = new CheckButton
            {
                Text = $"{capability.DisplayName} · {capability.Id}",
                TooltipText = $"Autorité {capability.Authority} · quota {capability.Quota}",
            };
            center.AddChild(toggle);
            _capabilities.Add(capability.Id, toggle);
        }
        center.AddChild(Section("KIT"));
        _grants = new ItemList { CustomMinimumSize = new Vector2(0, 76) };
        center.AddChild(_grants);
        center.AddChild(Section("ÉVÉNEMENT AUTORISÉ"));
        _bindings = new ItemList { CustomMinimumSize = new Vector2(0, 76) };
        center.AddChild(_bindings);

        var right = Panel(cards, 370);
        right.AddChild(Section("DIAGNOSTICS STABLES"));
        _diagnostics = new ItemList { CustomMinimumSize = new Vector2(0, 180) };
        right.AddChild(_diagnostics);
        right.AddChild(Section("CONSOLE D’AUTORISATION"));
        _console = new Label
        {
            Text = "Prévisualisez la transaction ou testez le serveur sandbox.",
            CustomMinimumSize = new Vector2(0, 160),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _console.AddThemeColorOverride("font_color", new Color("9dddc1"));
        right.AddChild(_console);
        Button(right, "Tester autorité + sandbox", RunSandbox, true);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        root.AddChild(actions);
        Button(actions, "Prévisualiser", Preview);
        _apply = Button(actions, "Appliquer", Apply, true);
        _apply.Disabled = true;
        _undo = Button(actions, "Annuler", Undo);
        _redo = Button(actions, "Rétablir", Redo);
        _status = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        actions.AddChild(_status);
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
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(130, 34) };
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
