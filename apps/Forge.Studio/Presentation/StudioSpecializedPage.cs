using Forge.Domain.Dialogues;
using Forge.Studio.Core.Navigation;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioSpecializedPage : Control
{
    private readonly Label _mode;
    private readonly Label _leftTitle;
    private readonly ItemList _resources;
    private readonly Label _documentTitle;
    private readonly Label _documentMeta;
    private readonly RichTextLabel _documentBody;
    private IReadOnlyList<ResourceEntry> _entries = [];

    internal StudioSpecializedPage()
    {
        Name = "StudioSpecializedPage";
        var background = new ColorRect
        {
            Color = StudioStyle.Background,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var margin = StudioStyle.Margin(9, 9, 9, 9);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(margin);
        var column = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        column.AddThemeConstantOverride("separation", 7);
        margin.AddChild(column);

        var notice = StudioStyle.Panel("MigrationNotice", StudioStyle.AccentDark, 8);
        column.AddChild(notice);
        var noticeRow = new HBoxContainer();
        noticeRow.AddChild(StudioStyle.Label("FONDATION AUTONOME", 10, StudioStyle.Accent));
        noticeRow.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _mode = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted);
        noticeRow.AddChild(_mode);
        notice.AddChild(noticeRow);

        var content = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        content.AddThemeConstantOverride("separation", 7);
        column.AddChild(content);

        var resourcePanel = StudioStyle.Panel("ResourceList", StudioStyle.Chrome, 9);
        resourcePanel.CustomMinimumSize = new Vector2(210, 0);
        content.AddChild(resourcePanel);
        var resourceMargin = StudioStyle.Margin(8, 8, 8, 8);
        resourcePanel.AddChild(resourceMargin);
        var resourceColumn = new VBoxContainer();
        resourceColumn.AddThemeConstantOverride("separation", 6);
        resourceMargin.AddChild(resourceColumn);
        _leftTitle = StudioStyle.SectionLabel(string.Empty);
        resourceColumn.AddChild(_leftTitle);
        var search = new LineEdit { PlaceholderText = "Rechercher…", ClearButtonEnabled = true };
        StudioStyle.ApplyLineEdit(search);
        search.Editable = false;
        search.TooltipText = "Le filtre sera activé avec la migration fonctionnelle de cette page.";
        resourceColumn.AddChild(search);
        _resources = new ItemList
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
        };
        _resources.AddThemeStyleboxOverride("panel", StudioStyle.Box(StudioStyle.Surface, StudioStyle.Border, 7));
        _resources.AddThemeColorOverride("font_color", StudioStyle.Text);
        _resources.AddThemeColorOverride("font_selected_color", StudioStyle.Text);
        _resources.ItemSelected += ShowEntry;
        resourceColumn.AddChild(_resources);

        var documentPanel = StudioStyle.Panel("DocumentEditor", StudioStyle.Surface, 9);
        documentPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        documentPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        content.AddChild(documentPanel);
        var documentMargin = StudioStyle.Margin(14, 12, 14, 10);
        documentPanel.AddChild(documentMargin);
        var documentColumn = new VBoxContainer();
        documentColumn.AddThemeConstantOverride("separation", 5);
        documentMargin.AddChild(documentColumn);
        _documentTitle = StudioStyle.Label(string.Empty, 19);
        documentColumn.AddChild(_documentTitle);
        _documentMeta = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted, wrap: true);
        documentColumn.AddChild(_documentMeta);
        var separator = new HSeparator();
        separator.AddThemeColorOverride("separator", StudioStyle.Border);
        documentColumn.AddChild(separator);
        _documentBody = new RichTextLabel
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            FitContent = false,
            BbcodeEnabled = false,
            SelectionEnabled = true,
            ContextMenuEnabled = true,
        };
        _documentBody.AddThemeFontSizeOverride("normal_font_size", 12);
        _documentBody.AddThemeColorOverride("default_color", StudioStyle.Text);
        documentColumn.AddChild(_documentBody);
        var plannedAction = StudioStyle.Button("Édition transactionnelle en cours de migration", primary: true);
        plannedAction.Disabled = true;
        plannedAction.TooltipText = "Les données affichées sont authentiques; l'écriture reste désactivée jusqu'aux tests de non-régression.";
        documentColumn.AddChild(plannedAction);
    }

    internal void ShowPage(StudioPageDefinition page, StudioSessionController session)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(session);
        _mode.Text = page.Id == "dialogues"
            ? "Lecture réelle · lignes d'abord · graphe réservé à la vue avancée"
            : "Lecture réelle · édition désactivée jusqu'au branchement transactionnel";
        _leftTitle.Text = page.LeftPanelTitle;
        _entries = BuildEntries(page, session);
        _resources.Clear();
        foreach (var entry in _entries)
        {
            _resources.AddItem(entry.Title);
        }

        if (_entries.Count == 0)
        {
            _documentTitle.Text = "Aucune ressource";
            _documentMeta.Text = page.Title;
            _documentBody.Text = "Le module ne contient actuellement aucune ressource de ce type.";
            return;
        }

        _resources.Select(0);
        ShowEntry(0);
    }

    private void ShowEntry(long index)
    {
        if (index < 0 || index >= _entries.Count)
        {
            return;
        }

        var entry = _entries[(int)index];
        _documentTitle.Text = entry.Title;
        _documentMeta.Text = entry.Meta;
        _documentBody.Text = entry.Body;
    }

    private static IReadOnlyList<ResourceEntry> BuildEntries(
        StudioPageDefinition page,
        StudioSessionController session)
    {
        var project = session.Project!;
        if (page.Id == "dialogues")
        {
            var blueprints = project.Blueprints.ToDictionary(item => item.Id);
            return project.Dialogues.Select(dialogue => new ResourceEntry(
                dialogue.DisplayName,
                $"{dialogue.Nodes.Count} lignes · {dialogue.Edges.Count} liaisons · {dialogue.Slug}",
                dialogue.Nodes.Count == 0
                    ? "Ce dialogue ne contient aucune ligne."
                    : string.Join("\n\n", dialogue.Nodes.Take(12).Select((node, index) =>
                    {
                        var speaker = node.SpeakerBlueprintId is not null &&
                                      blueprints.TryGetValue(node.SpeakerBlueprintId, out var blueprint)
                            ? blueprint.DisplayName
                            : node.Role == DialogueNodeRole.Narration ? "Narration" : "Intervenant";
                        var choices = node.Choices.Count == 0
                            ? string.Empty
                            : $"\n   ↳ {node.Choices.Count} choix";
                        return $"{index + 1:00}  {speaker}\n{node.Text.Fallback}{choices}";
                    })))).ToArray();
        }

        if (page.Id == "quests")
        {
            return project.Quests.Select(quest => new ResourceEntry(
                quest.DisplayName,
                $"{quest.States.Count} états · {quest.Transitions.Count} transitions · priorité {quest.Priority}",
                $"{quest.Description.Fallback}\n\nÉtat initial\n{quest.InitialStateId}\n\n" +
                string.Join("\n", quest.States.Select((state, index) => $"{index + 1:00}  {state.DisplayName}"))))
                .ToArray();
        }

        if (page.Id == "scripts")
        {
            var root = session.Session!.RootDirectory;
            var files = Directory.EnumerateFiles(root, "*.forge", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return files.Select(path => new ResourceEntry(
                Path.GetFileName(path),
                Path.GetRelativePath(root, path),
                File.ReadAllText(path))).ToArray();
        }

        if (page.Id == "module")
        {
            var module = project.Module;
            return
            [
                new ResourceEntry(
                    module.DisplayName,
                    $"Module {module.Slug} · entrée {module.EntryAreaId}",
                    $"{module.Description}\n\n" +
                    $"Zones : {project.Areas.Count}\n" +
                    $"Blueprints : {project.Blueprints.Count}\n" +
                    $"Dialogues : {project.Dialogues.Count}\n" +
                    $"Quêtes : {project.Quests.Count}\n" +
                    $"Variables : {module.Variables.Count}")
            ];
        }

        if (page.Id == "localization")
        {
            var variants = project.Dialogues.SelectMany(dialogue => dialogue.Nodes)
                .Sum(node => node.Text.Variants.Count);
            return
            [
                new ResourceEntry(
                    "Textes du module",
                    $"{project.Dialogues.Sum(item => item.Nodes.Count)} lignes · {variants} variantes localisées",
                    "Vue de contrôle des textes réellement chargés. La table de traduction et l'ajout de langue " +
                    "seront branchés dans la phase Histoire, sans exposer les fichiers JSON à l'auteur.")
            ];
        }

        if (page.Id == "packages")
        {
            return session.AssetManifests.Select((manifest, index) => new ResourceEntry(
                $"Lot de ressources {index + 1}",
                $"{manifest.Chunks.Count} fragments gouvernés",
                string.Join("\n", manifest.Chunks.Select(chunk =>
                    $"{chunk.Role} · {Path.GetFileName(chunk.RelativePath)} · {chunk.StoredBytes:N0} octets"))))
                .ToArray();
        }

        if (page.Id == "validation")
        {
            var unresolved = project.Areas.Sum(area => area.Entities.Count(entity =>
                project.Blueprints.All(blueprint => blueprint.Id != entity.BlueprintId)));
            return
            [
                new ResourceEntry(
                    "Contrôle structurel immédiat",
                    unresolved == 0 ? "Aucune référence de blueprint manquante" : $"{unresolved} références manquantes",
                    $"Zones chargées : {project.Areas.Count}\n" +
                    $"Entités contrôlées : {project.Areas.Sum(area => area.Entities.Count)}\n" +
                    $"Blueprints chargés : {project.Blueprints.Count}\n\n" +
                    "Le gate complet reste disponible dans scripts/Verify.ps1 pendant la migration.")
            ];
        }

        return
        [
            new ResourceEntry(
                page.Title,
                $"Module {project.Module.DisplayName} · espace {page.Title}",
                $"Cette page est correctement routée dans la nouvelle application autonome.\n\n" +
                $"Données actuellement chargées\n" +
                $"• {project.Areas.Count} zones\n" +
                $"• {project.Areas.Sum(area => area.Entities.Count)} objets placés\n" +
                $"• {project.Blueprints.Count} blueprints\n" +
                $"• {project.Dialogues.Count} dialogues\n" +
                $"• {project.Quests.Count} quêtes\n\n" +
                "La fonction spécialisée sera activée phase par phase avec commandes, annulation, sauvegarde " +
                "atomique et tests de préservation des données.")
        ];
    }

    private sealed record ResourceEntry(string Title, string Meta, string Body);
}
