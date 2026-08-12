using Forge.RpgKit.Application.Scripting;
using Forge.RpgKit.Domain.Scripting;
using Forge.Scripting;
using Godot;

namespace Forge.Runtime;

public partial class ForgeScriptP1Panel : PanelContainer
{
    private readonly VBoxContainer _layout = new() { Name = "ForgeScriptP1Layout" };
    private readonly Label _title = new() { Name = "ForgeScriptP1Title" };
    private readonly ItemList _authoring = new()
    {
        Name = "AuthoringList",
        CustomMinimumSize = new(500, 150),
        FocusMode = FocusModeEnum.All,
    };
    private readonly ItemList _gameplay = new()
    {
        Name = "GameplayList",
        CustomMinimumSize = new(500, 180),
        FocusMode = FocusModeEnum.All,
    };
    private readonly HBoxContainer _actions = new() { Name = "ForgeScriptP1Actions" };
    private readonly Label _feedback = new()
    {
        Name = "ForgeScriptP1Feedback",
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
    };

    public ForgeScriptP1Panel()
    {
        Name = "ForgeScriptP1Panel";
        CustomMinimumSize = new(540, 500);
        AddChild(_layout);
        _layout.AddChild(_title);
        _layout.AddChild(new Label { Text = "Éditeur et Script Wizard" });
        _layout.AddChild(_authoring);
        _layout.AddChild(new Label { Text = "Actions différées, combat et effets" });
        _layout.AddChild(_gameplay);
        _layout.AddChild(_actions);
        _layout.AddChild(_feedback);
        AddAction("QueueActionButton", "Mettre en file", "action.queued");
        AddAction("AttackButton", "Attaquer", "combat.attack");
        AddAction("ApplyEffectButton", "Appliquer effet", "effect.applied");
        AddAction("DispelEffectButton", "Dissiper", "effect.dispelled");
    }

    public void Configure(
        ForgeScriptP1Runtime runtime,
        ForgeScriptEditorAnalysis analysis,
        ForgeScriptWizardResult wizard,
        ScriptCombatResult combat,
        EffectApplyResult effect,
        ForgeScriptExecutionResult execution)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(wizard);
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(effect);
        ArgumentNullException.ThrowIfNull(execution);
        _title.Text = $"ForgeScript P1 · API {runtime.Document.ApiVersion} · {runtime.Document.RelativePath}";
        _authoring.Clear();
        _authoring.AddItem($"Coloration · {analysis.SyntaxSpans.Count} token(s)");
        _authoring.AddItem($"Complétion · {analysis.Completions.Count} proposition(s)");
        _authoring.AddItem($"Symboles · {analysis.DocumentSymbols.Count} · format {analysis.FormattedSource.Length} caractères");
        _authoring.AddItem($"Wizard · {(wizard.Succeeded ? "AST typé et source relisible" : "diagnostic")}");
        _gameplay.Clear();
        _gameplay.AddItem($"File · {runtime.Tasks.Tasks.Count} tâche(s) · révision {runtime.Tasks.Revision}");
        _gameplay.AddItem($"Combat · {combat.AppliedDamage:0.##} dégâts · critique {combat.Critical}");
        _gameplay.AddItem($"Effets · {effect.Effects.Count} actif(s) · {effect.Events.FirstOrDefault()}");
        _gameplay.AddItem($"Sandbox · {execution.Status} · {execution.Events.Count} intention(s)");
        _feedback.Text = "Prêt · handles opaques, autorité serveur et quotas actifs";
        SetMeta("source_id", runtime.Document.Id.ToString("D"));
        SetMeta("task_count", runtime.Tasks.Tasks.Count);
        SetMeta("effect_count", effect.Effects.Count);
        SetMeta("screen_reader_summary",
            "Éditeur ForgeScript, Script Wizard, file d'actions, combat et effets accessibles");
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
