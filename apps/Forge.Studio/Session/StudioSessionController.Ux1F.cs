using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    internal StudioUx1FSmokeResult RunUx1FContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux1f-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var blueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1f20000-0000-4000-8000-000000000001")),
                "colonnade-des-brumes",
                "Colonnade des brumes",
                BlueprintKind.Placeable);
            var parentBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1f20000-0000-4000-8000-000000000009")),
                "socle-des-brumes",
                "Socle des brumes",
                BlueprintKind.Placeable);
            var first = new EntityInstance(
                EntityId.From(Guid.Parse("c1f20000-0000-4000-8000-000000000002")),
                blueprint.Id,
                new TransformValue(Vector3Value.Zero, QuaternionValue.Identity, Vector3Value.One));
            var second = new EntityInstance(
                EntityId.From(Guid.Parse("c1f20000-0000-4000-8000-000000000003")),
                blueprint.Id,
                new TransformValue(new(4, 0, 0), QuaternionValue.Identity, Vector3Value.One));
            var area = new AreaDefinition(
                AreaId.From(Guid.Parse("c1f20000-0000-4000-8000-000000000004")),
                "atelier-decor-statique",
                "Atelier du décor statique",
                [first, second],
                AreaKind.Interior,
                new AreaDimensions(32, 32));
            var project = new ProjectDefinition(
                ProjectId.From(Guid.Parse("c1f20000-0000-4000-8000-000000000005")),
                "Studio UX1-F",
                [blueprint, parentBlueprint],
                [area]);
            var parentId = Guid.Parse("c1f20000-0000-4000-8000-000000000006");
            var foundation = new PlaceableWorldProfile.FoundationPolicy(
                Guid.Parse("c1f20000-0000-4000-8000-000000000007"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-f-foundation",
                blueprint.Slug,
                [
                    new("en-US", "Mist colonnade", "Observe"),
                    new("fr-FR", blueprint.DisplayName, "Observer"),
                ],
                ["architecture", "brumes"],
                "Décor architectural répétable.",
                new(
                    "asset:colonnade-brumes",
                    ["mesh:colonnade-a", "mesh:colonnade-b"],
                    ["material:stone-weathered"],
                    "asset:colonnade-icon",
                    "asset:colonnade-portrait",
                    ["vfx:mist-soft"]),
                new("box", new(2, 4, 1), true, true, "decor"),
                null,
                new(true, false, false, false, true),
                new(false, false, false, false, false,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["onVisible"] = "scripts/colonnade_visible",
                    }),
                new(new(.13f, 0, -.13f), Vector3Value.Zero, Vector3Value.One, new(0, 2, 0), .25,
                    parentId, true, "animation:colonnade-settle"),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["material"] = "weathered-stone",
                },
                Validation: new(
                    ["animation:colonnade-settle"],
                    [],
                    [],
                    [],
                    [],
                    4,
                    2));
            var originalProfile = new PlaceableWorldProfile(
                Guid.Parse("c1f20000-0000-4000-8000-000000000008"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-f-profile",
                blueprint.Id,
                "neutral",
                null,
                new("credential:architect", PlaceableWorldProfile.CredentialConsumption.Retain, 0, null, true),
                new([], new(false, 1, 1, true, "placeable")),
                new(PlaceableWorldProfile.BodyMode.Static, 10, ["rotation"], false, 2, true, "server"),
                foundation,
                true);
            var parentProfile = CreatePlaceableTransformParent(
                parentBlueprint,
                parentId,
                "test:ux1-f-parent");
            var seededCatalog = new WorldFeatureCatalog(
                project.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-f-catalog",
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
            if (!created.Succeeded || !editing.Open(tempRoot).Succeeded)
            {
                return FailedUx1FSmoke();
            }

            var originalProject = editing.Project!;
            var loadedOriginalProfile = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var loadedOriginalFoundation = loadedOriginalProfile.Foundation!;
            var draft = editing.CreatePlaceableDraft(blueprint.Id)! with
            {
                Role = StudioPlaceableRole.StaticDecoration,
                CollisionEnabled = false,
                NavigationObstacle = false,
                Batched = false,
                Instanced = true,
                Occluder = true,
                MergeEligible = true,
                StaticCollisionEnabled = false,
                AuthorNotes = "Colonnade fusionnable sans collision par instance.",
            };
            var mergeInvalid = editing.PreviewPlaceable(draft with { StaticCollisionEnabled = true });
            var noOptimizationInvalid = editing.PreviewPlaceable(draft with
            {
                Batched = false,
                Instanced = false,
                MergeEligible = false,
            });
            var mergeGuard = !mergeInvalid.Succeeded;
            var optimizationGuard = !noOptimizationInvalid.Succeeded;
            var humanDiagnostics = mergeGuard && optimizationGuard &&
                                   IsHumanPlaceableMessage(mergeInvalid.Message) &&
                                   IsHumanPlaceableMessage(noOptimizationInvalid.Message);

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
            var appliedFoundation = profile.Foundation;
            var staticRole = applied.Succeeded && appliedFoundation is
            {
                Interaction: null,
                StaticProp: not null,
            };
            var optimization = appliedFoundation?.StaticProp is
            {
                Batched: false,
                Instanced: true,
                Occluder: true,
                MergeEligible: true,
                CollisionEnabled: false,
            };
            var optionalCollision = appliedFoundation?.Collision is
            {
                Enabled: false,
                NavigationObstacle: false,
            };
            var commandParity = directBuilt && directProfile?.Foundation is { } directFoundation &&
                                appliedFoundation?.StaticProp == directFoundation.StaticProp &&
                                appliedFoundation?.Collision == directFoundation.Collision;
            var preservation = profile.Id == loadedOriginalProfile.Id && profile.FactionId == "neutral" &&
                               ReferenceEquals(profile.Credential, loadedOriginalProfile.Credential) &&
                               ReferenceEquals(profile.Physics, loadedOriginalProfile.Physics) &&
                               appliedFoundation?.Texts.Any(item => item.Locale == "en-US" &&
                                   item.Name == "Mist colonnade") == true &&
                               appliedFoundation.Visual.MeshIds.SequenceEqual(loadedOriginalFoundation.Visual.MeshIds) &&
                               appliedFoundation.Visual.MaterialIds.SequenceEqual(loadedOriginalFoundation.Visual.MaterialIds) &&
                               appliedFoundation.Collision.PhysicsLayer == "decor" &&
                               appliedFoundation.Components.ScriptBindings["onVisible"] == "scripts/colonnade_visible" &&
                               appliedFoundation.Transform.ParentId == parentId &&
                               appliedFoundation.Transform.TransformAnimationId == "animation:colonnade-settle" &&
                               appliedFoundation.Validation?.AnimationIds.Single() == "animation:colonnade-settle";
            var runtime = WorldFeatureRuntimeResolver.ResolvePlaceableFoundation(profile);
            var optional = WorldFeatureRuntimeResolver.ResolvePlaceableFoundation(profile with
            {
                Foundation = null,
                FoundationRequiredAtRuntime = false,
            });
            var required = WorldFeatureRuntimeResolver.ResolvePlaceableFoundation(profile with { Foundation = null });
            var runtimeSemantics = runtime is { Available: true, Foundation.StaticProp.MergeEligible: true } &&
                                   !optional.Available && string.IsNullOrEmpty(optional.DiagnosticCode) &&
                                   !required.Available &&
                                   required.DiagnosticCode == "WORLD_PLACEABLE_FOUNDATION_REQUIRED_MISSING";

            var undo = editing.Undo();
            var undone = ReferenceEquals(originalProject, editing.Project);
            var redo = editing.Redo();
            var redone = ReferenceEquals(appliedProject, editing.Project);
            var undoRedo = undo.Succeeded && undone && redo.Succeeded && redone;
            var save = editing.Save();
            editing.Close();
            var reopen = reopened.Open(tempRoot);
            var restored = reopened.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
                item.BlueprintId == blueprint.Id);
            var persistence = save.Succeeded && reopen.Succeeded && restored?.Foundation?.StaticProp is
            {
                Instanced: true,
                Occluder: true,
                MergeEligible: true,
                CollisionEnabled: false,
            } &&
                              restored.Foundation.Collision.Enabled == false &&
                              restored.Foundation.Transform.ParentId == parentId &&
                              restored.Foundation.Visual.MaterialIds.Single() == "material:stone-weathered";

            var succeeded = staticRole && optimization && optionalCollision && mergeGuard && optimizationGuard &&
                            dryRun && commandParity && instanceSelection && preservation && runtimeSemantics &&
                            undoRedo && persistence && humanDiagnostics;
            return new(
                succeeded,
                staticRole,
                optimization,
                optionalCollision,
                mergeGuard,
                optimizationGuard,
                dryRun,
                commandParity,
                instanceSelection,
                preservation,
                runtimeSemantics,
                undoRedo,
                persistence,
                humanDiagnostics);
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

    private static StudioUx1FSmokeResult FailedUx1FSmoke() => new(
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
