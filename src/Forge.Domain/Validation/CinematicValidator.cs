using Forge.Domain.Projects;
using Forge.Domain.Sequences;

namespace Forge.Domain.Validation;

public static class CinematicValidator
{
    public const string ModuleIdentityCode = "cinematic.module_identity";
    public const string AreaIdentityCode = "cinematic.area_identity";
    public const string CameraAnchorBoundsCode = "cinematic.camera_anchor_out_of_bounds";
    public const string MissingCameraAnchorCode = "cinematic.camera_anchor_missing";
    public const string MissingEntityCode = "cinematic.entity_missing";
    public const string MissingDialogueCode = "cinematic.dialogue_missing";
    public const string UnsafeResourcePathCode = "cinematic.resource_path_unsafe";
    public const string MissingTransformEntityCode = "cinematic.transform_override_entity_missing";
    public const string TransformHierarchyCode = "cinematic.transform_override_hierarchy_invalid";
    public const string MissingTweenEntityCode = "cinematic.tween_entity_missing";

    public static IReadOnlyList<DomainDiagnostic> Validate(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var diagnostics = new List<DomainDiagnostic>();
        if (project.Module.RuntimePolicies.Cinematics.Id != project.Module.Id.Value)
        {
            diagnostics.Add(new DomainDiagnostic(ModuleIdentityCode,
                "The module cinematic profile ID must match the module ID.",
                "module/runtimePolicies/cinematics/id"));
        }

        var anchorIds = new HashSet<Guid>();
        var entityIds = project.Areas.SelectMany(area => area.Entities).Select(entity => entity.Id.Value).ToHashSet();
        var dialogueIds = project.Dialogues.Select(dialogue => dialogue.Id.Value).ToHashSet();
        foreach (var area in project.Areas)
        {
            if (area.RuntimePolicies.Cinematics.Id != area.Id.Value)
            {
                diagnostics.Add(new DomainDiagnostic(AreaIdentityCode,
                    "The area cinematic profile ID must match the area ID.",
                    $"areas/{area.Id}/runtimePolicies/cinematics/id"));
            }

            foreach (var anchor in area.RuntimePolicies.Cinematics.CameraAnchors)
            {
                anchorIds.Add(anchor.Id);
                var position = anchor.Transform.Position;
                if (position.X < -area.Dimensions.Width / 2f || position.X > area.Dimensions.Width / 2f ||
                    position.Z < -area.Dimensions.Depth / 2f || position.Z > area.Dimensions.Depth / 2f)
                {
                    diagnostics.Add(new DomainDiagnostic(CameraAnchorBoundsCode,
                        $"Camera anchor '{anchor.Name}' must remain inside the area X/Z bounds.",
                        $"areas/{area.Id}/runtimePolicies/cinematics/cameraAnchors/{anchor.Id}"));
                }
            }
        }

        var cinematics = project.Module.RuntimePolicies.Cinematics;
        ValidateResource(cinematics.Startup.ResourcePath, "module/runtimePolicies/cinematics/startup/resourcePath",
            diagnostics);
        ValidateResource(cinematics.Startup.FallbackResourcePath,
            "module/runtimePolicies/cinematics/startup/fallbackResourcePath", diagnostics);
        foreach (var sequence in cinematics.Sequences)
        {
            foreach (var track in sequence.Tracks)
            {
                var path = $"module/runtimePolicies/cinematics/sequences/{sequence.Id}/tracks/{track.Id}";
                ValidateResource(track.ResourcePath, $"{path}/resourcePath", diagnostics);
                if (track.TargetKind == SequenceTargetKind.CameraAnchor &&
                    track.TargetId is not null && !anchorIds.Contains(track.TargetId.Value))
                {
                    diagnostics.Add(new DomainDiagnostic(MissingCameraAnchorCode,
                        $"Sequence track '{track.Name}' references a missing camera anchor.", $"{path}/targetId"));
                }
                else if (track.TargetKind == SequenceTargetKind.Entity &&
                         track.TargetId is not null && !entityIds.Contains(track.TargetId.Value))
                {
                    diagnostics.Add(new DomainDiagnostic(MissingEntityCode,
                        $"Sequence track '{track.Name}' references a missing entity.", $"{path}/targetId"));
                }
                else if (track.TargetKind == SequenceTargetKind.Dialogue &&
                         track.TargetId is not null && !dialogueIds.Contains(track.TargetId.Value))
                {
                    diagnostics.Add(new DomainDiagnostic(MissingDialogueCode,
                        $"Sequence track '{track.Name}' references a missing dialogue.", $"{path}/targetId"));
                }
            }
        }

        foreach (var component in cinematics.TransformOverrides)
        {
            if (!entityIds.Contains(component.EntityId.Value) ||
                component.ParentEntityId is not null && !entityIds.Contains(component.ParentEntityId.Value))
            {
                diagnostics.Add(new DomainDiagnostic(MissingTransformEntityCode,
                    "Transform overrides must reference existing entities.",
                    $"module/runtimePolicies/cinematics/transformOverrides/{component.Id}"));
            }
        }
        var parents = cinematics.TransformOverrides.Where(item => item.ParentEntityId is not null)
            .GroupBy(item => item.EntityId.Value).ToDictionary(group => group.Key,
                group => group.First().ParentEntityId!.Value);
        foreach (var entityId in parents.Keys)
        {
            var visited = new HashSet<Guid>();
            var current = entityId;
            while (parents.TryGetValue(current, out var parent))
            {
                if (!visited.Add(current) || parent == entityId)
                {
                    diagnostics.Add(new DomainDiagnostic(TransformHierarchyCode,
                        "Transform override hierarchy must not contain cycles.",
                        "module/runtimePolicies/cinematics/transformOverrides"));
                    break;
                }
                current = parent;
            }
        }
        foreach (var tween in cinematics.Tweens.Where(item => !entityIds.Contains(item.EntityId.Value)))
        {
            diagnostics.Add(new DomainDiagnostic(MissingTweenEntityCode,
                "Animation tweens must reference an existing entity.",
                $"module/runtimePolicies/cinematics/tweens/{tween.Id}"));
        }

        return diagnostics;
    }

    private static void ValidateResource(string resourcePath, string path, ICollection<DomainDiagnostic> diagnostics)
    {
        if (string.IsNullOrEmpty(resourcePath)) return;
        if (resourcePath.StartsWith('/') || resourcePath.Contains(':') ||
            resourcePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Contains("..", StringComparer.Ordinal))
        {
            diagnostics.Add(new DomainDiagnostic(UnsafeResourcePathCode,
                "Cinematic resources must use safe project-relative paths.", path));
        }
    }
}
