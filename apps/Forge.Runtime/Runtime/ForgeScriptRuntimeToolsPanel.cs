using Forge.RpgKit.Application.Scripting;
using Forge.Scripting;
using Godot;

namespace Forge.Runtime;

public partial class ForgeScriptRuntimeToolsPanel : PanelContainer
{
    private readonly VBoxContainer _layout = new() { Name = "ForgeScriptRuntimeToolsLayout" };
    private readonly Label _title = new() { Name = "RuntimeToolsTitle" };
    private readonly ItemList _tools = new()
    {
        Name = "RuntimeToolsList",
        CustomMinimumSize = new(580, 260),
        FocusMode = FocusModeEnum.All,
    };
    private readonly HBoxContainer _actions = new() { Name = "RuntimeToolActions" };
    private readonly Label _feedback = new()
    {
        Name = "RuntimeToolsFeedback",
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
    };

    public ForgeScriptRuntimeToolsPanel()
    {
        Name = "ForgeScriptRuntimeToolsPanel";
        CustomMinimumSize = new(620, 450);
        AddChild(_layout);
        _layout.AddChild(_title);
        _layout.AddChild(new Label
        {
            Text = "Audio · Temps · Événements · Hot reload · Débogueur",
        });
        _layout.AddChild(_tools);
        _layout.AddChild(_actions);
        _layout.AddChild(_feedback);
        AddAction("AudioButton", "Jouer", "audio.started");
        AddAction("TimerButton", "Planifier", "time.timer.scheduled");
        AddAction("EventButton", "Publier", "event.published");
        AddAction("ReloadButton", "Recharger", "hot-reload.requested");
        AddAction("DebugButton", "Pas à pas", "debug.step");
        ShowEmpty();
    }

    public void ShowEmpty()
    {
        _title.Text = "ForgeScript · Outils runtime";
        _tools.Clear();
        _tools.AddItem("Aucune session d'outil active");
        _feedback.Text = "Prêt · choisissez une commande bornée";
        SetMeta("state", "empty");
    }

    public void ShowError(string diagnostic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnostic);
        _feedback.Text = $"Erreur bornée · {diagnostic}";
        SetMeta("state", "error");
    }

    public void Configure(
        ForgeScriptRuntimeToolState runtime,
        IReadOnlyList<EventDelivery> deliveries,
        HotReloadResult reload,
        ForgeScriptExecutionResult execution)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(deliveries);
        ArgumentNullException.ThrowIfNull(reload);
        ArgumentNullException.ThrowIfNull(execution);
        if (runtime.Debugger is null) throw new InvalidOperationException("FORGESCRIPT_DEBUG_SESSION_MISSING");

        _title.Text = $"ForgeScript outils · API {runtime.Document.ApiVersion} · {runtime.Document.RelativePath}";
        _tools.Clear();
        _tools.AddItem(
            $"Audio · {runtime.Audio.Playing.Keys.SingleOrDefault() ?? "silence"} · snapshot {runtime.Audio.Snapshot} · {runtime.Audio.ListenerPolicy}");
        _tools.AddItem(
            $"Temps · {runtime.Clock.SimulationTime.TotalSeconds:0} s · {runtime.Timers.Count} timer(s) · révision {runtime.Clock.Revision}");
        _tools.AddItem(
            $"Événement · {deliveries.FirstOrDefault()?.Event.Name ?? "aucun"} · {deliveries.Count} livraison(s) tracée(s)");
        _tools.AddItem(
            $"Hot reload · v{reload.Active.Version} · {reload.Strategy} · {reload.Invalidated.Count} invalidation(s)");
        _tools.AddItem(
            $"Débogueur · {runtime.Debugger.State} · distant {runtime.Debugger.Remote} · {runtime.Debugger.Breakpoints.Count} breakpoint(s)");
        _feedback.Text = $"Console · {execution.Status} · {execution.Events.Count} intention(s) typée(s)";
        SetMeta("state", "ready");
        SetMeta("source_id", runtime.Document.Id.ToString("D"));
        SetMeta("audio_revision", runtime.Audio.Revision);
        SetMeta("clock_revision", runtime.Clock.Revision);
        SetMeta("debug_revision", runtime.Debugger.Revision);
        SetMeta("screen_reader_summary",
            "Outils ForgeScript audio, temps, événements, hot reload et débogueur accessibles");
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
