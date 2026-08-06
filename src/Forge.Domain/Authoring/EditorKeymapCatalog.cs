namespace Forge.Domain.Authoring;

public static class EditorKeymapCatalog
{
    public const string SaveProject = "project.save";
    public const string Undo = "edit.undo";
    public const string Redo = "edit.redo";
    public const string CopySelection = "selection.copy";
    public const string PasteSelection = "selection.paste";
    public const string DuplicateSelection = "selection.duplicate";
    public const string DeleteSelection = "selection.delete";
    public const string FocusSelection = "view.focus_selection";
    public const string RunPlaytest = "playtest.run";
    public const string OpenSearch = "search.open";
    public const string OpenWorld = "workspace.world";
    public const string OpenBlueprints = "workspace.blueprints";
    public const string OpenDialogues = "workspace.dialogues";

    public static IReadOnlyList<EditorCommandDefinition> Commands { get; } =
    [
        new(SaveProject, "Enregistrer le projet", "Projet"),
        new(Undo, "Annuler", "Édition"),
        new(Redo, "Rétablir", "Édition"),
        new(CopySelection, "Copier la sélection", "Édition"),
        new(PasteSelection, "Coller", "Édition"),
        new(DuplicateSelection, "Dupliquer la sélection", "Édition"),
        new(DeleteSelection, "Supprimer la sélection", "Édition"),
        new(FocusSelection, "Cadrer la sélection", "Vue 3D"),
        new(RunPlaytest, "Jouer depuis le point de départ", "Playtest"),
        new(OpenSearch, "Ouvrir la recherche globale", "Navigation"),
        new(OpenWorld, "Ouvrir l’espace Monde", "Espaces"),
        new(OpenBlueprints, "Ouvrir l’espace Blueprints", "Espaces"),
        new(OpenDialogues, "Ouvrir l’espace Dialogues", "Espaces"),
    ];

    public static EditorCommandDefinition? Find(string commandId) =>
        Commands.FirstOrDefault(command => string.Equals(command.Id, commandId, StringComparison.Ordinal));
}
