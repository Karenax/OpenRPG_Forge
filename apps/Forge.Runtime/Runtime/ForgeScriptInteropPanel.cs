using Forge.Scripting;
using Godot;

namespace Forge.Runtime;

public partial class ForgeScriptInteropPanel : PanelContainer
{
    private readonly VBoxContainer _layout = new() { Name = "ForgeScriptInteropLayout" };
    private readonly Label _title = new() { Name = "InteropTitle" };
    private readonly ItemList _tools = new()
    {
        Name = "InteropTools",
        CustomMinimumSize = new(580, 280),
        FocusMode = FocusModeEnum.All,
    };
    private readonly HBoxContainer _actions = new() { Name = "InteropActions" };
    private readonly Label _feedback = new()
    {
        Name = "InteropFeedback",
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
    };

    public ForgeScriptInteropPanel()
    {
        Name = "ForgeScriptInteropPanel";
        CustomMinimumSize = new(620, 460);
        AddChild(_layout);
        _layout.AddChild(_title);
        _layout.AddChild(new Label { Text = "API · Sources · Compilation · Tests · Bindings · Transactions" });
        _layout.AddChild(_tools);
        _layout.AddChild(_actions);
        _layout.AddChild(_feedback);
        AddAction("ApiButton", "API", "interop.api.read");
        AddAction("SourceButton", "Source", "interop.source.preview");
        AddAction("CompileButton", "Compiler", "interop.compile.completed");
        AddAction("TestsButton", "Tester", "interop.tests.completed");
        AddAction("BindingButton", "Binding", "interop.binding.preview");
        AddAction("UndoButton", "Annuler", "interop.plan.reverted");
        AddAction("RedoButton", "Rétablir", "interop.plan.redone");
        ShowEmpty();
    }

    public void ShowEmpty()
    {
        _title.Text = "ForgeScript · Interop éditeur/MCP";
        _tools.Clear();
        _tools.AddItem("Aucune transaction interop active");
        _feedback.Text = "Prêt · choisissez une opération transactionnelle";
        SetMeta("state", "empty");
    }

    public void ShowError(string diagnostic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnostic);
        _feedback.Text = $"Erreur localisée · {diagnostic}";
        SetMeta("state", "error");
    }

    public void Configure(
        ForgeScriptInteropProfile profile,
        ForgeScriptSourceDocument document,
        ForgeScriptExecutionResult execution,
        long revision)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(execution);
        _title.Text = $"Interop ForgeScript · API {profile.ApiVersion} · révision {revision}";
        _tools.Clear();
        _tools.AddItem($"get_api · API {profile.ApiVersion} · outils versionnés");
        _tools.AddItem($"create/update · {document.RelativePath} · {document.Id:D}");
        _tools.AddItem($"compile/diagnostics · {execution.Status} · {execution.Diagnostics.Count} diagnostic(s)");
        _tools.AddItem($"run_tests · {execution.Events.Count} événement(s) · quota {profile.TestInstructionLimit:N0}");
        _tools.AddItem($"assign_event · {profile.EventBinding} · preview obligatoire");
        _tools.AddItem($"transactions · preview/apply/reject · undo/redo · révision {revision}");
        _feedback.Text = "Console · aucune exécution hors sandbox · source canonique inchangée par les tests";
        SetMeta("state", "ready");
        SetMeta("profile_id", profile.Id.ToString("D"));
        SetMeta("source_id", document.Id.ToString("D"));
        SetMeta("revision", revision);
        SetMeta("screen_reader_summary",
            "Interop ForgeScript API, sources, compilation, tests, bindings et transactions accessible");
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
