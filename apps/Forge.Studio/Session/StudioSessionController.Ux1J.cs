using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Placeables;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    internal StudioUx1JSmokeResult RunUx1JContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux1j-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var blueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1a60000-0000-4000-8000-000000000001")),
                "balise-des-vents",
                "Balise des vents",
                BlueprintKind.Placeable);
            var parentBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1a60000-0000-4000-8000-000000000002")),
                "socle-des-vents",
                "Socle des vents",
                BlueprintKind.Placeable);
            var first = new EntityInstance(
                EntityId.From(Guid.Parse("c1a60000-0000-4000-8000-000000000003")),
                blueprint.Id,
                new TransformValue(Vector3Value.Zero, QuaternionValue.Identity, Vector3Value.One));
            var second = new EntityInstance(
                EntityId.From(Guid.Parse("c1a60000-0000-4000-8000-000000000004")),
                blueprint.Id,
                new TransformValue(new(4, 0, 2), QuaternionValue.Identity, Vector3Value.One));
            var area = new AreaDefinition(
                AreaId.From(Guid.Parse("c1a60000-0000-4000-8000-000000000005")),
                "atelier-transformation",
                "Atelier de transformation précise",
                [first, second],
                AreaKind.Interior,
                new AreaDimensions(32, 32));
            var project = new ProjectDefinition(
                ProjectId.From(Guid.Parse("c1a60000-0000-4000-8000-000000000006")),
                "Studio UX1-J",
                [blueprint, parentBlueprint],
                [area]);

            var profileId = Guid.Parse("c1a60000-0000-4000-8000-000000000007");
            var parentId = Guid.Parse("c1a60000-0000-4000-8000-000000000008");
            var foundation = new PlaceableWorldProfile.FoundationPolicy(
                Guid.Parse("c1a60000-0000-4000-8000-000000000009"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-j-foundation",
                blueprint.Slug,
                [new("fr-FR", blueprint.DisplayName, "Observer la balise")],
                ["balise", "vents"],
                "Transformation initiale à préserver puis modifier.",
                new(
                    "asset:balise-vents",
                    ["mesh:balise", "mesh:girouette"],
                    ["material:cuivre"],
                    "asset:balise-icon",
                    "asset:balise-portrait",
                    ["vfx:vent-doux"]),
                new("box", new(1, 3, 1), true, true, "world"),
                null,
                new(true, true, true, false, true),
                new(false, false, false, false, false,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["onVisible"] = "scripts/balise_visible",
                    }),
                new(new(.2f, 0, -.2f), new(0, 15, 0), Vector3Value.One, new(0, 1.5f, 0), .1,
                    null, false, "animation:balise-idle"),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["variant"] = "cuivre",
                },
                Validation: new(["animation:balise-idle"], [], [], [], [], 4, 2));
            var operations = new PlaceableOperationsProfile(
                Guid.Parse("c1a60000-0000-4000-8000-00000000000a"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-j-operations",
                null,
                CreateContainerCompatibilityStateMachine(blueprint.Id),
                null,
                CreateContainerCompatibilityProtection(blueprint.Id),
                null);
            var originalProfile = new PlaceableWorldProfile(
                profileId,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-j-profile",
                blueprint.Id,
                "neutral",
                null,
                null,
                new([], new(false, 1, 1, true, "transform")),
                null,
                foundation,
                true,
                operations,
                true);
            var parentProfile = CreatePlaceableTransformParent(
                parentBlueprint,
                parentId,
                "test:ux1-j-parent");
            var seededCatalog = new WorldFeatureCatalog(
                project.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-j-catalog",
                placeables: [originalProfile, parentProfile]);
            project = new ProjectDefinition(
                project.Id,
                project.Title,
                project.Module.WithWorldFeatures(seededCatalog),
                project.Blueprints,
                project.Areas,
                project.Dialogues,
                project.Quests);
            var created = ProjectSession.Create(tempRoot, project);
            if (!created.Succeeded)
            {
                Godot.GD.PrintErr(
                    "OPENRPG_FORGE_STUDIO_UX1J_PROJECT_CREATE_FAILED diagnostics=" +
                    string.Join(" | ", created.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
                return FailedUx1JSmoke();
            }
            var opened = editing.Open(tempRoot);
            if (!opened.Succeeded)
            {
                Godot.GD.PrintErr(
                    "OPENRPG_FORGE_STUDIO_UX1J_PROJECT_OPEN_FAILED diagnostics=" +
                    string.Join(" | ", opened.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
                return FailedUx1JSmoke();
            }

            var originalProject = editing.Project!;
            var loadedOriginal = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var loadedParent = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == parentBlueprint.Id);
            var loadedFoundation = loadedOriginal.Foundation!;
            var draft = editing.CreatePlaceableDraft(blueprint.Id)! with
            {
                Position = new(1.13f, .62f, -.13f),
                RotationDegrees = new(12, 37, -5),
                Scale = new(1.1f, 1.25f, .9f),
                Pivot = new(.1f, 1.2f, -.1f),
                SnapIncrement = .25,
                ParentId = parentId,
                AlignToSurface = true,
                TransformAnimationId = "animation:balise-deploy",
                AuthorNotes = "Transformation précise liée au socle des vents.",
            };

            var invalidScale = editing.PreviewPlaceable(draft with { Scale = new(0, 1, 1) });
            var invalidBounds = editing.PreviewPlaceable(draft with
            {
                Position = new(1_000_001, 0, 0),
            });
            var invalidParent = editing.PreviewPlaceable(draft with { ParentId = Guid.NewGuid() });
            var invalidCycle = editing.PreviewPlaceable(draft with { ParentId = profileId });
            var invalidBlocked = !invalidScale.Succeeded && !invalidBounds.Succeeded &&
                                 !invalidParent.Succeeded && !invalidCycle.Succeeded;
            var humanDiagnostics = invalidBlocked &&
                                   IsHumanPlaceableMessage(invalidScale.Message) &&
                                   IsHumanPlaceableMessage(invalidBounds.Message) &&
                                   IsHumanPlaceableMessage(invalidParent.Message) &&
                                   IsHumanPlaceableMessage(invalidCycle.Message);
            var geometryGuard = !invalidScale.Succeeded && !invalidBounds.Succeeded;

            var directBuilt = editing.TryBuildPlaceable(
                draft,
                dryRun: true,
                out _,
                out var directProfile,
                out _);
            var preview = editing.PreviewPlaceable(draft);
            var dryRun = preview.Succeeded && preview.CanApply && ReferenceEquals(originalProject, editing.Project);
            var instanceSelection = preview.InstanceIds.SequenceEqual([first.Id, second.Id]);
            var applied = editing.ApplyApprovedPlaceable();
            var appliedProject = editing.Project!;
            var profile = appliedProject.Module.WorldFeatures.Placeables.Single(item => item.BlueprintId == blueprint.Id);
            var edited = profile.Foundation;
            var transform = edited?.Transform;
            var coordinates = applied.Succeeded && transform is
            {
                Position: { X: 1.13f, Y: .62f, Z: -.13f },
                RotationDegrees: { X: 12, Y: 37, Z: -5 },
                Scale: { X: 1.1f, Y: 1.25f, Z: .9f },
            };
            var pivotSnap = transform is
            {
                Pivot: { X: .1f, Y: 1.2f, Z: -.1f },
                SnapIncrement: .25,
            };
            var parentage = transform?.ParentId == parentId;
            var surfaceAlignment = transform?.AlignToSurface == true;
            var transformAnimation = transform?.TransformAnimationId == "animation:balise-deploy" &&
                                     edited?.Validation?.AnimationIds.Contains(
                                         "animation:balise-idle", StringComparer.Ordinal) == true &&
                                     edited.Validation.AnimationIds.Contains(
                                         "animation:balise-deploy", StringComparer.Ordinal);
            var commandParity = directBuilt && directProfile?.Foundation is { } directFoundation &&
                                PreciseTransformValuesEqual(directFoundation, edited);
            var preservation = profile.Id == loadedOriginal.Id && profile.Provenance == loadedOriginal.Provenance &&
                               profile.FactionId == loadedOriginal.FactionId &&
                               ReferenceEquals(profile.Reactive, loadedOriginal.Reactive) &&
                               profile.Operations?.Id == loadedOriginal.Operations?.Id &&
                               edited?.Id == loadedFoundation.Id && edited.Slug == loadedFoundation.Slug &&
                               edited.Texts.SequenceEqual(loadedFoundation.Texts) &&
                               edited.Tags.SequenceEqual(loadedFoundation.Tags) &&
                               SamePlaceableVisual(edited.Visual, loadedFoundation.Visual) &&
                               edited.Collision == loadedFoundation.Collision &&
                               edited.StaticProp == loadedFoundation.StaticProp &&
                               SamePlaceableComponents(edited.Components, loadedFoundation.Components) &&
                               edited.Overrides.OrderBy(item => item.Key)
                                   .SequenceEqual(loadedFoundation.Overrides.OrderBy(item => item.Key)) &&
                               ReferenceEquals(
                                   appliedProject.Module.WorldFeatures.Placeables.Single(item =>
                                       item.BlueprintId == parentBlueprint.Id),
                                   loadedParent);

            var adapter = new PlaceableOperationsRuntimeAdapter();
            var runtimeResolution = adapter.Resolve(profile);
            var runtime = adapter.Build(runtimeResolution.Handle!, profile);
            var resolved = WorldFeatureRuntimeResolver.ResolvePlaceableFoundation(profile);
            var runtimeProjection = runtimeResolution.Available &&
                                    runtime.Definition.Transform.Position ==
                                    new Forge.RpgKit.Domain.Placeables.Vector3Value(
                                        (double)1.13f, (double).62f, (double)-.13f) &&
                                    runtime.Definition.Transform.RotationDegrees ==
                                    new Forge.RpgKit.Domain.Placeables.Vector3Value(12, 37, -5) &&
                                    runtime.Definition.Transform.Scale ==
                                    new Forge.RpgKit.Domain.Placeables.Vector3Value(
                                        (double)1.1f, (double)1.25f, (double).9f) &&
                                    runtime.Definition.Transform.Pivot ==
                                    new Forge.RpgKit.Domain.Placeables.Vector3Value(
                                        (double).1f, (double)1.2f, (double)-.1f) &&
                                    runtime.Definition.Transform.SnapIncrement == .25 &&
                                    runtime.Definition.Transform.ParentId == parentId &&
                                    runtime.Definition.Transform.AlignToSurface &&
                                    runtime.Definition.Transform.TransformAnimationId == "animation:balise-deploy" &&
                                    resolved.SnappedTransform?.Position == new Vector3Value(1.25f, .5f, -.25f) &&
                                    resolved.Overlay.Contains($"parent={parentId:D}", StringComparison.Ordinal) &&
                                    resolved.Overlay.Contains("animation=animation:balise-deploy", StringComparison.Ordinal);
            var scriptHandle = new WorldFeatureScriptService().Query(editing.History!.State,
                "placeable", profile.Id.ToString("D"), 1).Single();
            var scriptRead = scriptHandle.Properties["position"] == "1.13,0.62,-0.13" &&
                             scriptHandle.Properties["rotationDegrees"] == "12,37,-5" &&
                             scriptHandle.Properties["scale"] == "1.1,1.25,0.9" &&
                             scriptHandle.Properties["pivot"] == "0.1,1.2,-0.1" &&
                             scriptHandle.Properties["snapIncrement"] == "0.25" &&
                             scriptHandle.Properties["parent"] == parentId.ToString("D") &&
                             scriptHandle.Properties["alignToSurface"] == "True" &&
                             scriptHandle.Properties["transformAnimation"] == "animation:balise-deploy";
            var optional = WorldFeatureRuntimeResolver.ResolvePlaceableFoundation(profile with
            {
                Foundation = null,
                FoundationRequiredAtRuntime = false,
            });
            var required = WorldFeatureRuntimeResolver.ResolvePlaceableFoundation(profile with { Foundation = null });
            var optionalRequired = optional is
            { Available: false, DiagnosticCode: "", Overlay: "placeable:optional-fallback" } &&
                                   required is
                                   {
                                       Available: false,
                                       DiagnosticCode: "WORLD_PLACEABLE_FOUNDATION_REQUIRED_MISSING",
                                   };

            var undo = editing.Undo();
            var undone = ReferenceEquals(originalProject, editing.Project);
            var redo = editing.Redo();
            var redone = ReferenceEquals(appliedProject, editing.Project);
            var undoRedo = undo.Succeeded && undone && redo.Succeeded && redone;
            var save = editing.Save();
            editing.Close();
            var reopen = reopened.Open(tempRoot);
            var restoredProfile = reopened.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
                item.BlueprintId == blueprint.Id);
            var restoredParent = reopened.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
                item.BlueprintId == parentBlueprint.Id);
            var persistence = save.Succeeded && reopen.Succeeded &&
                              PreciseTransformValuesEqual(restoredProfile?.Foundation, edited) &&
                              restoredParent?.Id == parentId;
            var succeeded = coordinates && pivotSnap && parentage && surfaceAlignment && transformAnimation &&
                            geometryGuard && dryRun && commandParity && instanceSelection && preservation &&
                            runtimeProjection && scriptRead && optionalRequired && undoRedo && persistence &&
                            invalidBlocked && humanDiagnostics;
            return new(
                succeeded,
                coordinates,
                pivotSnap,
                parentage,
                surfaceAlignment,
                transformAnimation,
                geometryGuard,
                dryRun,
                commandParity,
                instanceSelection,
                preservation,
                runtimeProjection,
                scriptRead,
                optionalRequired,
                undoRedo,
                persistence,
                invalidBlocked,
                humanDiagnostics);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1J_CONTRACT_EXCEPTION type={exception.GetType().Name} message={exception.Message}");
            return FailedUx1JSmoke();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static bool PreciseTransformValuesEqual(
        PlaceableWorldProfile.FoundationPolicy? left,
        PlaceableWorldProfile.FoundationPolicy? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }
        return left.Transform == right.Transform &&
               left.Validation?.AnimationIds.SequenceEqual(right.Validation?.AnimationIds ?? [],
                   StringComparer.Ordinal) == true;
    }

    private static bool SamePlaceableVisual(
        PlaceableWorldProfile.VisualDefinition left,
        PlaceableWorldProfile.VisualDefinition right) =>
        left.AssetId == right.AssetId &&
        left.MeshIds.SequenceEqual(right.MeshIds, StringComparer.Ordinal) &&
        left.MaterialIds.SequenceEqual(right.MaterialIds, StringComparer.Ordinal) &&
        left.IconAssetId == right.IconAssetId &&
        left.PortraitAssetId == right.PortraitAssetId &&
        left.VisualEffectIds.SequenceEqual(right.VisualEffectIds, StringComparer.Ordinal);

    private static bool SamePlaceableComponents(
        PlaceableWorldProfile.ComponentPolicy left,
        PlaceableWorldProfile.ComponentPolicy right) =>
        left.HasHealth == right.HasHealth &&
        left.HasContainer == right.HasContainer &&
        left.HasLock == right.HasLock &&
        left.HasTrap == right.HasTrap &&
        left.HasPhysics == right.HasPhysics &&
        left.ScriptBindings.OrderBy(item => item.Key, StringComparer.Ordinal)
            .SequenceEqual(right.ScriptBindings.OrderBy(item => item.Key, StringComparer.Ordinal));

    private static StudioUx1JSmokeResult FailedUx1JSmoke() => new(
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false);
}
