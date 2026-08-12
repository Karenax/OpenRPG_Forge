using Forge.RpgKit.Application.Scripting;
using Forge.RpgKit.Domain.Scripting;
using Forge.Scripting;
using Godot;

namespace Forge.Runtime;

public partial class ForgeScriptWorldApiPanel : PanelContainer
{
    private readonly VBoxContainer _layout = new() { Name = "ForgeScriptWorldApiLayout" };
    private readonly Label _title = new() { Name = "WorldApiTitle" };
    private readonly ItemList _worldApis = new()
    {
        Name = "WorldApiList",
        CustomMinimumSize = new(540, 250),
        FocusMode = FocusModeEnum.All,
    };
    private readonly HBoxContainer _actions = new() { Name = "WorldApiActions" };
    private readonly Label _feedback = new()
    {
        Name = "WorldApiFeedback",
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
    };

    public ForgeScriptWorldApiPanel()
    {
        Name = "ForgeScriptWorldApiPanel";
        CustomMinimumSize = new(580, 430);
        AddChild(_layout);
        _layout.AddChild(_title);
        _layout.AddChild(new Label
        {
            Text = "Inventaire · Quêtes · Dialogues · Factions · Perception",
        });
        _layout.AddChild(_worldApis);
        _layout.AddChild(_actions);
        _layout.AddChild(_feedback);
        AddAction("TransferButton", "Transférer", "inventory.transfer");
        AddAction("QuestButton", "Transition", "quest.transition");
        AddAction("DialogueButton", "Dialoguer", "dialogue.complete");
        AddAction("FactionButton", "Réputation", "faction.reputation");
        AddAction("PerceptionButton", "Percevoir", "perception.query");
        ShowEmpty();
    }

    public void ShowEmpty()
    {
        _title.Text = "ForgeScript · API monde";
        _worldApis.Clear();
        _worldApis.AddItem("Aucune intention monde exécutée");
        _feedback.Text = "Prêt · choisissez une commande typée";
        SetMeta("state", "empty");
    }

    public void ShowError(string diagnostic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnostic);
        _feedback.Text = $"Erreur bornée · {diagnostic}";
        SetMeta("state", "error");
    }

    public void Configure(
        ForgeScriptWorldApiRuntime runtime,
        InventoryResult inventory,
        InventoryQueryResult inventoryQuery,
        QuestState quest,
        DialogueSession dialogue,
        FactionQueryResult faction,
        PerceptionResult perception,
        ForgeScriptExecutionResult execution)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(inventoryQuery);
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(dialogue);
        ArgumentNullException.ThrowIfNull(faction);
        ArgumentNullException.ThrowIfNull(perception);
        ArgumentNullException.ThrowIfNull(execution);

        _title.Text = $"ForgeScript monde · API {runtime.Document.ApiVersion} · {runtime.Document.RelativePath}";
        _worldApis.Clear();
        _worldApis.AddItem(
            $"Inventaire · {inventoryQuery.ItemId} ×{inventoryQuery.Quantity} · capacité {inventoryQuery.UsedCapacity}/{inventoryQuery.MaximumCapacity} · révision {inventory.State.Revision}");
        _worldApis.AddItem(
            $"Quête · {quest.QuestId} · {quest.Status} · objectif {quest.Objectives.Values.Sum()} · révision {quest.Revision}");
        _worldApis.AddItem(
            $"Dialogue · {dialogue.GraphId} · {dialogue.State} · {dialogue.Participants.Count} participant(s)");
        _worldApis.AddItem(
            $"Faction · {faction.FactionId} {faction.Relationship:+#;-#;0} · membre {faction.Member} · {faction.Scope}");
        _worldApis.AddItem(
            $"Perception · {(perception.Perceived ? "détecté" : "masqué")} · confiance {perception.Confidence:0.000} · {perception.Stimuli.Count} stimulus");
        _feedback.Text = $"Console · {execution.Status} · {execution.Events.Count} intention(s) typée(s)";
        SetMeta("state", "ready");
        SetMeta("source_id", runtime.Document.Id.ToString("D"));
        SetMeta("inventory_revision", inventory.State.Revision);
        SetMeta("quest_revision", quest.Revision);
        SetMeta("faction_revision", faction.Revision);
        SetMeta("perception_trace", perception.Trace);
        SetMeta("screen_reader_summary",
            "API monde ForgeScript : inventaire, quête, dialogue, faction et perception accessibles");
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
