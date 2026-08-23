using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioForgeScriptRuntimeToolsPageUx6L
{
    private static readonly string[] DebugAuthoritiesUx6N = ["local-author", "server-admin"];
    private static readonly string[] DebugStepsUx6N = ["into", "over", "out", "continue"];

    private LineEdit _reloadModuleUx6N = null!;
    private SpinBox _activeVersionUx6N = null!;
    private SpinBox _candidateVersionUx6N = null!;
    private LineEdit _activeHashUx6N = null!;
    private LineEdit _candidateHashUx6N = null!;
    private LineEdit _stateKeyUx6N = null!;
    private LineEdit _stateTypeUx6N = null!;
    private LineEdit _activeDependenciesUx6N = null!;
    private LineEdit _candidateDependenciesUx6N = null!;
    private CheckButton _handlerBoundaryUx6N = null!;
    private CheckButton _migrationUx6N = null!;

    private LineEdit _debugSessionUx6N = null!;
    private OptionButton _debugAuthorityUx6N = null!;
    private CheckButton _remoteDebugUx6N = null!;
    private LineEdit _debugFunctionUx6N = null!;
    private LineEdit _debugSourceUx6N = null!;
    private SpinBox _debugLineUx6N = null!;
    private LineEdit _localNameUx6N = null!;
    private SpinBox _localValueUx6N = null!;
    private LineEdit _watchExpressionUx6N = null!;
    private SpinBox _watchValueUx6N = null!;
    private LineEdit _entityAliasUx6N = null!;
    private LineEdit _entityReferenceUx6N = null!;
    private LineEdit _debugEventUx6N = null!;
    private OptionButton _debugStepUx6N = null!;

    private StudioUx6NSourcePlan? _livePlanUx6N;
    private StudioUx6NLiveResult? _liveRuntimeUx6N;

    internal bool LiveToolsReadyUx6N => _liveRuntimeUx6N is
    {
        Succeeded: true,
        ReloadApplied: true,
        DebugSession: true,
        AdapterReload: true,
        NativeExecution: true,
    };

    internal bool ShowsLiveHumanSurfaceUx6N
    {
        get
        {
            var text = string.Join(' ', DescendantText(this));
            return text.Contains("Hot reload", StringComparison.Ordinal) &&
                   text.Contains("Débogueur", StringComparison.Ordinal) &&
                   text.Contains("Frontière de handler", StringComparison.Ordinal) &&
                   text.Contains("Autorité du débogueur", StringComparison.Ordinal) &&
                   !ContainsUuid(text);
        }
    }

    internal void OpenLiveToolsUx6N(string mode)
    {
        Open();
        SelectLiveEvidenceTabUx6N(mode);
    }

    private void BuildLiveToolsUx6N(TabContainer tabs)
    {
        var reloadPage = TabPage(tabs, "Hot reload");
        reloadPage.AddChild(StudioStyle.Label(
            "Hot reload · version, migration d’état et dépendances",
            12, StudioStyle.Text));
        _reloadModuleUx6N = Text("forgotten-forest");
        _activeVersionUx6N = Number(1, 100_000, 1, 1);
        reloadPage.AddChild(Pair("Module logique", _reloadModuleUx6N,
            "Version active", _activeVersionUx6N));
        _candidateVersionUx6N = Number(1, 100_000, 1, 2);
        _stateKeyUx6N = Text("score");
        reloadPage.AddChild(Pair("Version candidate", _candidateVersionUx6N,
            "Composant d’état", _stateKeyUx6N));
        _stateTypeUx6N = Text("int");
        _activeHashUx6N = Text("sha256:active-reviewed");
        reloadPage.AddChild(Pair("Type d’état", _stateTypeUx6N,
            "Empreinte active", _activeHashUx6N));
        _candidateHashUx6N = Text("sha256:candidate-reviewed");
        _activeDependenciesUx6N = Text("dialogue");
        reloadPage.AddChild(Pair("Empreinte candidate", _candidateHashUx6N,
            "Dépendances actives · séparées par virgule", _activeDependenciesUx6N));
        _candidateDependenciesUx6N = Text("quest");
        reloadPage.AddChild(Field("Dépendances candidates · séparées par virgule",
            _candidateDependenciesUx6N));
        var reloadFlags = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _handlerBoundaryUx6N = new CheckButton
        {
            Text = "Frontière de handler atteinte",
            ButtonPressed = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _migrationUx6N = new CheckButton
        {
            Text = "Migration d’état compatible",
            ButtonPressed = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        reloadFlags.AddChild(_handlerBoundaryUx6N);
        reloadFlags.AddChild(_migrationUx6N);
        reloadPage.AddChild(reloadFlags);
        reloadPage.AddChild(StudioStyle.Label(
            "L’aperçu exerce report hors frontière, migration, invalidation des dépendances et rollback avec stratégie restart. Aucun module live n’est remplacé.",
            9, StudioStyle.Muted, wrap: true));

        var debugPage = TabPage(tabs, "Débogueur");
        debugPage.AddChild(StudioStyle.Label(
            "Débogueur · breakpoints, pile, watches et pas contrôlés",
            12, StudioStyle.Text));
        _debugSessionUx6N = Text("debug:studio-r099");
        _debugAuthorityUx6N = Options("Auteur local", "Administrateur serveur");
        _debugAuthorityUx6N.Select(1);
        debugPage.AddChild(Pair("Session de débogage", _debugSessionUx6N,
            "Autorité du débogueur", _debugAuthorityUx6N));
        _remoteDebugUx6N = new CheckButton
        {
            Text = "Session distante permissionnée",
            ButtonPressed = true,
        };
        debugPage.AddChild(_remoteDebugUx6N);
        _debugFunctionUx6N = Text("OnUse");
        _debugSourceUx6N = Text("scripts/runtime-tools.as");
        debugPage.AddChild(Pair("Fonction au sommet", _debugFunctionUx6N,
            "Source et breakpoint", _debugSourceUx6N));
        _debugLineUx6N = Number(1, 1_000_000, 1, 12);
        _localNameUx6N = Text("count");
        debugPage.AddChild(Pair("Ligne", _debugLineUx6N, "Locale", _localNameUx6N));
        _localValueUx6N = Number(int.MinValue, int.MaxValue, 1, 2);
        _watchExpressionUx6N = Text("hero.mana");
        debugPage.AddChild(Pair("Valeur locale", _localValueUx6N,
            "Expression observée", _watchExpressionUx6N));
        _watchValueUx6N = Number(int.MinValue, int.MaxValue, 1, 5);
        _entityAliasUx6N = Text("hero");
        debugPage.AddChild(Pair("Valeur observée", _watchValueUx6N,
            "Alias d’entité", _entityAliasUx6N));
        _entityReferenceUx6N = Text("entity:hero");
        _debugEventUx6N = Text("OnUse");
        debugPage.AddChild(Pair("Référence d’entité protégée", _entityReferenceUx6N,
            "Événement de trace", _debugEventUx6N));
        _debugStepUx6N = Options("Entrer", "Passer", "Sortir", "Continuer");
        _debugStepUx6N.Select(1);
        debugPage.AddChild(Field("Commande de pas", _debugStepUx6N));
        debugPage.AddChild(StudioStyle.Label(
            "La session copiée expose breakpoints, pile, locales, watches, événements et inspection d’entité. Aucun processus ni acteur distant n’est contrôlé.",
            9, StudioStyle.Muted, wrap: true));

        HookLiveControlsUx6N();
    }

    private StudioUx6NLiveDraft? ReadLiveDraftUx6N()
    {
        if (!HasExplicitSelection) return null;
        return new(
            _reloadModuleUx6N.Text,
            (int)_activeVersionUx6N.Value,
            (int)_candidateVersionUx6N.Value,
            _activeHashUx6N.Text,
            _candidateHashUx6N.Text,
            _stateKeyUx6N.Text,
            _stateTypeUx6N.Text,
            DependenciesUx6N(_activeDependenciesUx6N.Text),
            DependenciesUx6N(_candidateDependenciesUx6N.Text),
            _handlerBoundaryUx6N.ButtonPressed,
            _migrationUx6N.ButtonPressed,
            _debugSessionUx6N.Text,
            DebugAuthoritiesUx6N[Math.Clamp(_debugAuthorityUx6N.Selected, 0,
                DebugAuthoritiesUx6N.Length - 1)],
            _remoteDebugUx6N.ButtonPressed,
            _debugFunctionUx6N.Text,
            _debugSourceUx6N.Text,
            (int)_debugLineUx6N.Value,
            _localNameUx6N.Text,
            (int)_localValueUx6N.Value,
            _watchExpressionUx6N.Text,
            (int)_watchValueUx6N.Value,
            _entityAliasUx6N.Text,
            _entityReferenceUx6N.Text,
            _debugEventUx6N.Text,
            DebugStepsUx6N[Math.Clamp(_debugStepUx6N.Selected, 0, DebugStepsUx6N.Length - 1)]);
    }

    private void PopulateLiveToolsUx6N(StudioUx6NLiveDraft live)
    {
        _reloadModuleUx6N.Text = live.ModuleId;
        _activeVersionUx6N.Value = live.ActiveVersion;
        _candidateVersionUx6N.Value = live.CandidateVersion;
        _activeHashUx6N.Text = live.ActiveSourceHash;
        _candidateHashUx6N.Text = live.CandidateSourceHash;
        _stateKeyUx6N.Text = live.StateKey;
        _stateTypeUx6N.Text = live.StateType;
        _activeDependenciesUx6N.Text = string.Join(", ", live.ActiveDependencies);
        _candidateDependenciesUx6N.Text = string.Join(", ", live.CandidateDependencies);
        _handlerBoundaryUx6N.ButtonPressed = live.AtHandlerBoundary;
        _migrationUx6N.ButtonPressed = live.MigrationSucceeds;
        _debugSessionUx6N.Text = live.DebugSessionId;
        _debugAuthorityUx6N.Select(Array.IndexOf(DebugAuthoritiesUx6N, live.DebugAuthority));
        _remoteDebugUx6N.ButtonPressed = live.RemoteDebug;
        _debugFunctionUx6N.Text = live.Function;
        _debugSourceUx6N.Text = live.SourcePath;
        _debugLineUx6N.Value = live.SourceLine;
        _localNameUx6N.Text = live.LocalName;
        _localValueUx6N.Value = live.LocalValue;
        _watchExpressionUx6N.Text = live.WatchExpression;
        _watchValueUx6N.Value = live.WatchValue;
        _entityAliasUx6N.Text = live.EntityAlias;
        _entityReferenceUx6N.Text = live.EntityReference;
        _debugEventUx6N.Text = live.EventName;
        _debugStepUx6N.Select(Array.IndexOf(DebugStepsUx6N, live.StepMode));
    }

    private void HookLiveControlsUx6N()
    {
        foreach (var line in new[]
                 {
                     _reloadModuleUx6N, _activeHashUx6N, _candidateHashUx6N, _stateKeyUx6N,
                     _stateTypeUx6N, _activeDependenciesUx6N, _candidateDependenciesUx6N,
                     _debugSessionUx6N, _debugFunctionUx6N, _debugSourceUx6N, _localNameUx6N,
                     _watchExpressionUx6N, _entityAliasUx6N, _entityReferenceUx6N, _debugEventUx6N,
                 })
            line.TextChanged += _ => RuntimeChanged();
        foreach (var number in new[]
                 {
                     _activeVersionUx6N, _candidateVersionUx6N, _debugLineUx6N,
                     _localValueUx6N, _watchValueUx6N,
                 })
            number.ValueChanged += _ => RuntimeChanged();
        _handlerBoundaryUx6N.Toggled += _ => RuntimeChanged();
        _migrationUx6N.Toggled += _ => RuntimeChanged();
        _remoteDebugUx6N.Toggled += _ => RuntimeChanged();
        _debugAuthorityUx6N.ItemSelected += _ => RuntimeChanged();
        _debugStepUx6N.ItemSelected += _ => RuntimeChanged();
    }

    private void InvalidateLiveToolsUx6N()
    {
        _livePlanUx6N = null;
        _liveRuntimeUx6N = null;
    }

    private bool SelectLiveEvidenceTabUx6N(string mode)
    {
        if (!mode.StartsWith("ux6-n", StringComparison.OrdinalIgnoreCase)) return false;
        _tabs.CurrentTab = mode.Contains("debug", StringComparison.OrdinalIgnoreCase) ? 4 : 3;
        return true;
    }

    private string LiveReviewUx6N() => _liveRuntimeUx6N is null
        ? "Hot reload et débogueur · en attente."
        : $"Hot reload {Bool(_liveRuntimeUx6N.ReloadApplied && _liveRuntimeUx6N.MigrationRollback)} · " +
          $"Débogueur {Bool(_liveRuntimeUx6N.DebugSession && _liveRuntimeUx6N.GuardedStepping)} · " +
          $"Adaptateur {Bool(_liveRuntimeUx6N.AdapterReload && _liveRuntimeUx6N.AdapterDebugger)} · " +
          $"Natif {Bool(_liveRuntimeUx6N.NativeExecution)}";

    private static IReadOnlyList<string> DependenciesUx6N(string value) =>
        (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
