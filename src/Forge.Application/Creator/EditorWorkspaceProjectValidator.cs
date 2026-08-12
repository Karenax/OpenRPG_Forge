using Forge.Domain.Authoring;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Creator;

public static class EditorWorkspaceProjectValidator
{
    public const string AreaReferenceCode = "editor.workspace.area_reference_missing";
    public const string LayerReferenceCode = "editor.workspace.layer_reference_missing";
    public const string SplitReferenceCode = "editor.workspace.split_reference_invalid";
    public const string HelpTopicCode = "editor.workspace.help_topic_missing";

    public static IReadOnlyList<DomainDiagnostic> Validate(EditorWorkspaceProfile profile, ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(project);
        var diagnostics = new List<DomainDiagnostic>();
        var areaIds = project.Areas.Select(area => area.Id).ToHashSet();
        foreach (var areaId in profile.OpenAreaTabs.Where(areaId => !areaIds.Contains(areaId)))
            diagnostics.Add(new(AreaReferenceCode, $"Editor tab references missing area '{areaId}'.",
                "editorWorkspace/openAreaTabs"));
        var layerIds = project.Areas.SelectMany(area => area.Authoring.Layers).Select(layer => layer.Id).ToHashSet();
        foreach (var layerId in profile.DisplayFilter.VisibleLayerIds.Where(id => !layerIds.Contains(id)))
            diagnostics.Add(new(LayerReferenceCode, $"Display filter references missing layer '{layerId}'.",
                "editorWorkspace/displayFilter/visibleLayerIds"));
        if (profile.MultiArea.SplitViewEnabled &&
            (profile.ActiveAreaId is null || profile.ComparisonAreaId is null ||
             profile.ActiveAreaId == profile.ComparisonAreaId))
            diagnostics.Add(new(SplitReferenceCode, "Split view requires two different open areas.",
                "editorWorkspace/multiArea"));
        if (EditorContextualHelpCatalog.Topics.All(topic => topic.Key != profile.Help.ActiveTopicKey))
            diagnostics.Add(new(HelpTopicCode, $"Help topic '{profile.Help.ActiveTopicKey}' does not exist.",
                "editorWorkspace/help/activeTopicKey"));
        return diagnostics;
    }
}
