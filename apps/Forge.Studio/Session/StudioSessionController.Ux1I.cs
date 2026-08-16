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
    internal StudioUx1ISmokeResult RunUx1IContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux1i-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var blueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1a50000-0000-4000-8000-000000000001")),
                "stele-du-veilleur",
                "Stèle du veilleur",
                BlueprintKind.Placeable);
            var first = new EntityInstance(
                EntityId.From(Guid.Parse("c1a50000-0000-4000-8000-000000000002")),
                blueprint.Id,
                new TransformValue(Vector3Value.Zero, QuaternionValue.Identity, Vector3Value.One));
            var second = new EntityInstance(
                EntityId.From(Guid.Parse("c1a50000-0000-4000-8000-000000000003")),
                blueprint.Id,
                new TransformValue(new(3, 0, 2), QuaternionValue.Identity, Vector3Value.One));
            var area = new AreaDefinition(
                AreaId.From(Guid.Parse("c1a50000-0000-4000-8000-000000000004")),
                "atelier-identite",
                "Atelier identité et apparence",
                [first, second],
                AreaKind.Interior,
                new AreaDimensions(32, 32));
            var project = new ProjectDefinition(
                ProjectId.From(Guid.Parse("c1a50000-0000-4000-8000-000000000005")),
                "Studio UX1-I",
                [blueprint],
                [area]);
            var foundation = new PlaceableWorldProfile.FoundationPolicy(
                Guid.Parse("c1a50000-0000-4000-8000-000000000006"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-i-foundation",
                blueprint.Slug,
                [
                    new("en-US", "Watcher stele", "Examine the stele"),
                    new("es-ES", "Estela del vigía", "Examinar la estela"),
                    new("fr-FR", blueprint.DisplayName, "Examiner la stèle"),
                ],
                ["décor", "veilleur"],
                "Repère ancien à préserver.",
                new(
                    "asset:stele-veilleur-v1",
                    ["mesh:stele-v1", "mesh:socle-v1"],
                    ["material:pierre-v1"],
                    "asset:stele-v1-icon",
                    "asset:stele-v1-portrait",
                    ["vfx:runes-v1"]),
                new("box", new(1.2f, 2.4f, .6f), true, true, "world"),
                null,
                new(true, true, true, false, true),
                new(false, false, false, false, false,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["onInspect"] = "scripts/stele_inspect",
                    }),
                new(new(.25f, 0, -.25f), Vector3Value.Zero, Vector3Value.One, new(0, 1.2f, 0), .25,
                    null, true, "animation:stele-idle"),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["lore"] = "watcher",
                },
                Validation: new(["animation:stele-idle"], [], [], [], [], 3, 2));
            var operations = new PlaceableOperationsProfile(
                Guid.Parse("c1a50000-0000-4000-8000-000000000007"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-i-operations",
                null,
                CreateContainerCompatibilityStateMachine(blueprint.Id),
                null,
                CreateContainerCompatibilityProtection(blueprint.Id),
                null);
            var originalProfile = new PlaceableWorldProfile(
                Guid.Parse("c1a50000-0000-4000-8000-000000000008"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-i-profile",
                blueprint.Id,
                "keepers",
                null,
                null,
                new([], new(false, 1, 1, true, "identity")),
                null,
                foundation,
                true,
                operations,
                true);
            var seededCatalog = new WorldFeatureCatalog(
                project.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-i-catalog",
                placeables: [originalProfile]);
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
                return FailedUx1ISmoke();
            }

            var originalProject = editing.Project!;
            var loadedOriginal = originalProject.Module.WorldFeatures.Placeables.Single();
            var loadedFoundation = loadedOriginal.Foundation!;
            var draft = editing.CreatePlaceableDraft(blueprint.Id)! with
            {
                Slug = "stele-du-veilleur-restauree",
                Name = "Stèle du veilleur restaurée",
                Prompt = "Lire les runes restaurées",
                AdditionalLocalizedTexts =
                [
                    new("en-US", "Restored watcher stele", "Read the restored runes"),
                    new("de-DE", "Restaurierte Wächterstele", "Restaurierte Runen lesen"),
                ],
                TagsText = "décor, runes, veilleur, restauré",
                AuthorNotes = "Repère localisé avec matériaux et VFX gouvernés.",
                VisualAssetId = "asset:stele-veilleur-v2",
                Appearance = new(
                    "mesh:stele-v2, mesh:socle-v2",
                    "material:pierre-v2, material:runes-v2",
                    "asset:stele-v2-icon",
                    "asset:stele-v2-portrait",
                    "vfx:runes-v2, vfx:poussiere-v2"),
                RequireRuntime = true,
            };

            var invalidLocales = editing.PreviewPlaceable(draft with
            {
                AdditionalLocalizedTexts =
                [
                    new("en-US", "First", "First prompt"),
                    new("en-US", "Duplicate", "Duplicate prompt"),
                ],
            });
            var invalidMeshes = editing.PreviewPlaceable(draft with
            {
                Appearance = draft.Appearance with { MeshIdsText = string.Empty },
            });
            var invalidPortrait = editing.PreviewPlaceable(draft with
            {
                Appearance = draft.Appearance with { PortraitAssetId = string.Empty },
            });
            var invalidBlocked = !invalidLocales.Succeeded && !invalidMeshes.Succeeded && !invalidPortrait.Succeeded;
            var humanDiagnostics = invalidBlocked &&
                                   IsHumanPlaceableMessage(invalidLocales.Message) &&
                                   IsHumanPlaceableMessage(invalidMeshes.Message) &&
                                   IsHumanPlaceableMessage(invalidPortrait.Message);

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
            var identity = applied.Succeeded && edited is
            {
                Slug: "stele-du-veilleur-restauree",
                Id: var foundationId,
                SchemaVersion: WorldFeatureCatalog.CurrentSchemaVersion,
                Provenance: "studio:ux1-e-placeable-blueprint",
            } && foundationId == loadedFoundation.Id;
            var localization = edited?.Texts.Count == 3 &&
                               edited.Texts.Any(item => item.Locale == "fr-FR" &&
                                   item.Name == "Stèle du veilleur restaurée" &&
                                   item.Prompt == "Lire les runes restaurées") &&
                               edited.Texts.Any(item => item.Locale == "en-US" &&
                                   item.Name == "Restored watcher stele") &&
                               edited.Texts.Any(item => item.Locale == "de-DE" &&
                                   item.Name == "Restaurierte Wächterstele") &&
                               edited.Texts.All(item => item.Locale != "es-ES");
            var appearance = edited?.Visual is
            {
                AssetId: "asset:stele-veilleur-v2",
                IconAssetId: "asset:stele-v2-icon",
                PortraitAssetId: "asset:stele-v2-portrait",
            } && edited.Visual.MeshIds.SequenceEqual(["mesh:stele-v2", "mesh:socle-v2"]) &&
                         edited.Visual.MaterialIds.SequenceEqual(["material:pierre-v2", "material:runes-v2"]) &&
                         edited.Visual.VisualEffectIds.SequenceEqual(["vfx:runes-v2", "vfx:poussiere-v2"]);
            var metadata = edited?.Tags.SequenceEqual(["décor", "runes", "veilleur", "restauré"]) == true &&
                           edited.AuthorNotes == "Repère localisé avec matériaux et VFX gouvernés.";
            var commandParity = directBuilt && directProfile?.Foundation is { } directFoundation &&
                                IdentityAppearanceValuesEqual(directFoundation, edited);
            var preservation = profile.Id == loadedOriginal.Id && profile.Provenance == loadedOriginal.Provenance &&
                               profile.FactionId == "keepers" &&
                               ReferenceEquals(profile.Reactive, loadedOriginal.Reactive) &&
                               profile.Operations?.Id == loadedOriginal.Operations?.Id &&
                               profile.Operations?.Provenance == loadedOriginal.Operations?.Provenance &&
                               ReferenceEquals(profile.Operations?.StateMachine, loadedOriginal.Operations?.StateMachine) &&
                               ReferenceEquals(profile.Operations?.Protection, loadedOriginal.Operations?.Protection) &&
                               edited?.Collision == loadedFoundation.Collision &&
                               edited.StaticProp == loadedFoundation.StaticProp &&
                               edited.Components.ScriptBindings.OrderBy(item => item.Key)
                                   .SequenceEqual(loadedFoundation.Components.ScriptBindings.OrderBy(item => item.Key)) &&
                               edited.Transform == loadedFoundation.Transform &&
                               edited.Overrides.OrderBy(item => item.Key)
                                   .SequenceEqual(loadedFoundation.Overrides.OrderBy(item => item.Key)) &&
                               ReferenceEquals(edited.Validation, loadedFoundation.Validation);

            var adapter = new PlaceableOperationsRuntimeAdapter();
            var runtimeResolution = adapter.Resolve(profile);
            var runtime = adapter.Build(runtimeResolution.Handle!, profile);
            var runtimeProjection = runtimeResolution.Available &&
                                    runtime.Definition.Slug == "stele-du-veilleur-restauree" &&
                                    runtime.Definition.Texts.Count == 3 &&
                                    runtime.Definition.Tags.SetEquals(["décor", "runes", "veilleur", "restauré"]) &&
                                    runtime.Definition.AuthorNotes ==
                                    "Repère localisé avec matériaux et VFX gouvernés." &&
                                    runtime.Definition.Visual.Meshes.SequenceEqual(["mesh:stele-v2", "mesh:socle-v2"]) &&
                                    runtime.Definition.Visual.Materials.SequenceEqual(
                                        ["material:pierre-v2", "material:runes-v2"]) &&
                                    runtime.Definition.Visual.IconAssetId == "asset:stele-v2-icon" &&
                                    runtime.Definition.Visual.PortraitAssetId == "asset:stele-v2-portrait" &&
                                    runtime.Definition.Visual.VisualEffects.SequenceEqual(
                                        ["vfx:runes-v2", "vfx:poussiere-v2"]);
            var scriptHandle = new WorldFeatureScriptService().Query(editing.History!.State,
                "placeable", profile.Id.ToString("D"), 1).Single();
            var scriptRead = scriptHandle.Properties["slug"] == "stele-du-veilleur-restauree" &&
                             scriptHandle.Properties["localizedNames"].Contains(
                                 "fr-FR:Stèle du veilleur restaurée", StringComparison.Ordinal) &&
                             scriptHandle.Properties["asset"] == "asset:stele-veilleur-v2" &&
                             scriptHandle.Properties["meshes"] == "mesh:stele-v2,mesh:socle-v2" &&
                             scriptHandle.Properties["materials"] == "material:pierre-v2,material:runes-v2" &&
                             scriptHandle.Properties["icon"] == "asset:stele-v2-icon" &&
                             scriptHandle.Properties["portrait"] == "asset:stele-v2-portrait" &&
                             scriptHandle.Properties["visualEffects"] == "vfx:runes-v2,vfx:poussiere-v2";
            var optional = WorldFeatureRuntimeResolver.ResolvePlaceableFoundation(profile with
            {
                Foundation = null,
                FoundationRequiredAtRuntime = false,
            });
            var required = WorldFeatureRuntimeResolver.ResolvePlaceableFoundation(profile with { Foundation = null });
            var optionalRequired = optional is { Available: false, DiagnosticCode: "", Overlay: "placeable:optional-fallback" } &&
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
            var restored = reopened.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
                item.BlueprintId == blueprint.Id)?.Foundation;
            var persistence = save.Succeeded && reopen.Succeeded && restored is not null &&
                              IdentityAppearanceValuesEqual(restored, edited);
            var succeeded = identity && localization && appearance && metadata && dryRun && commandParity &&
                            instanceSelection && preservation && runtimeProjection && scriptRead && optionalRequired &&
                            undoRedo && persistence && invalidBlocked && humanDiagnostics;
            return new(
                succeeded,
                identity,
                localization,
                appearance,
                metadata,
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
            Console.Error.WriteLine(
                $"OPENRPG_FORGE_STUDIO_UX1I_CONTRACT_EXCEPTION type={exception.GetType().Name} message={exception.Message}");
            return FailedUx1ISmoke();
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

    private static bool IdentityAppearanceValuesEqual(
        PlaceableWorldProfile.FoundationPolicy? left,
        PlaceableWorldProfile.FoundationPolicy? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }
        return left.Id == right.Id && left.SchemaVersion == right.SchemaVersion &&
               left.Provenance == right.Provenance && left.Slug == right.Slug &&
               left.Texts.SequenceEqual(right.Texts) && left.Tags.SequenceEqual(right.Tags) &&
               left.AuthorNotes == right.AuthorNotes && left.Visual.AssetId == right.Visual.AssetId &&
               left.Visual.IconAssetId == right.Visual.IconAssetId &&
               left.Visual.PortraitAssetId == right.Visual.PortraitAssetId &&
               left.Visual.MeshIds.SequenceEqual(right.Visual.MeshIds) &&
               left.Visual.MaterialIds.SequenceEqual(right.Visual.MaterialIds) &&
               left.Visual.VisualEffectIds.SequenceEqual(right.Visual.VisualEffectIds);
    }

    private static StudioUx1ISmokeResult FailedUx1ISmoke() => new(
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
