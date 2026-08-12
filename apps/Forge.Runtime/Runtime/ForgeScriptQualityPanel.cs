using Forge.Scripting;
using Godot;

namespace Forge.Runtime;

public partial class ForgeScriptQualityPanel : PanelContainer
{
    private readonly VBoxContainer _layout = new() { Name = "ForgeScriptQualityLayout" };
    private readonly Label _title = new() { Name = "QualityTitle" };
    private readonly ItemList _quality = new()
    {
        Name = "QualityList",
        CustomMinimumSize = new(580, 260),
        FocusMode = FocusModeEnum.All,
    };
    private readonly HBoxContainer _actions = new() { Name = "QualityActions" };
    private readonly Label _feedback = new()
    {
        Name = "QualityFeedback",
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
    };

    public ForgeScriptQualityPanel()
    {
        Name = "ForgeScriptQualityPanel";
        CustomMinimumSize = new(620, 450);
        AddChild(_layout);
        _layout.AddChild(_title);
        _layout.AddChild(new Label { Text = "Tests · Profilage · Documentation · Analyse · État" });
        _layout.AddChild(_quality);
        _layout.AddChild(_actions);
        _layout.AddChild(_feedback);
        AddAction("TestsButton", "Tester", "quality.tests.completed");
        AddAction("ProfileButton", "Profiler", "quality.profile.completed");
        AddAction("DocsButton", "Documenter", "quality.docs.generated");
        AddAction("AnalyzeButton", "Analyser", "quality.analysis.completed");
        AddAction("StateButton", "État", "quality.state.validated");
        ShowEmpty();
    }

    public void ShowEmpty()
    {
        _title.Text = "ForgeScript · Qualité runtime";
        _quality.Clear();
        _quality.AddItem("Aucun rapport qualité disponible");
        _feedback.Text = "Prêt · exécutez une vérification bornée";
        SetMeta("state", "empty");
    }

    public void ShowError(string diagnostic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnostic);
        _feedback.Text = $"Erreur bornée · {diagnostic}";
        SetMeta("state", "error");
    }

    public void Configure(
        ForgeScriptQualityProfile profile,
        IReadOnlyList<ScriptTestResult> tests,
        ProfileReport profiler,
        GeneratedApiDocument documentation,
        IReadOnlyList<StaticDiagnostic> diagnostics,
        IReadOnlyDictionary<string, object?> restoredState,
        ForgeScriptExecutionResult execution)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(tests);
        ArgumentNullException.ThrowIfNull(profiler);
        ArgumentNullException.ThrowIfNull(documentation);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(restoredState);
        ArgumentNullException.ThrowIfNull(execution);
        _title.Text = $"ForgeScript qualité · API {profile.ApiVersion} · schéma v{profile.SchemaVersion}";
        _quality.Clear();
        _quality.AddItem($"Tests · {tests.Count(item => item.Passed)}/{tests.Count} PASS · seed {profile.UnitTests.DeterministicSeed}");
        _quality.AddItem($"Profilage · {profiler.TotalInstructions:N0} instructions · {profiler.TotalAllocatedBytes / 1024:N0} Kio · {profiler.Alerts.Count} alerte(s)");
        _quality.AddItem($"Documentation · API {documentation.ApiVersion} · {documentation.McpResources.Count} ressource(s) MCP");
        _quality.AddItem($"Analyse · {diagnostics.Count} diagnostic(s) localisé(s) · {profile.StaticAnalysis.EnabledRules.Count} règle(s)");
        _quality.AddItem($"État · {restoredState.Count} composant(s) enregistré(s) · quota {profile.State.MaximumBytes / 1024} Kio");
        _feedback.Text = $"Console · {execution.Status} · {execution.Events.Count} événement(s) sandbox";
        SetMeta("state", "ready");
        SetMeta("quality_profile", profile.Id.ToString("D"));
        SetMeta("test_count", tests.Count);
        SetMeta("profile_alerts", profiler.Alerts.Count);
        SetMeta("screen_reader_summary",
            "Qualité ForgeScript tests, profilage, documentation, analyse statique et état accessible");
    }

    private void AddAction(string name, string text, string eventName)
    {
        var button = new Button
        {
            Name = name,
            Text = text,
            TooltipText = text,
            FocusMode = FocusModeEnum.All,
        };
        button.Pressed += () => _feedback.Text = $"Commande exécutée · {eventName}";
        _actions.AddChild(button);
    }
}
