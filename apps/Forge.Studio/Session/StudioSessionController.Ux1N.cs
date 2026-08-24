using Forge.Application.Commands;
using Forge.Application.Factions;
using Forge.Application.Time;
using Forge.Application.WorldFeatures;
using Forge.Domain.Factions;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Placeables;
using PlaceableFactionActor = Forge.RpgKit.Domain.Placeables.PlaceableFactionActor;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly SetFactionCatalogHandler _setUx1NFactionCatalog = new(new SystemClock());

    internal StudioUx1NSmokeResult RunUx1NContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux1n-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var blueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1aa0000-0000-4000-8000-000000000001")),
                "autel-des-veilleurs",
                "Autel des veilleurs",
                BlueprintKind.Placeable);
            var neighborBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1aa0000-0000-4000-8000-000000000002")),
                "borne-voisine",
                "Borne voisine",
                BlueprintKind.Placeable);
            var first = new EntityInstance(
                EntityId.From(Guid.Parse("c1aa0000-0000-4000-8000-000000000003")),
                blueprint.Id,
                new TransformValue(Vector3Value.Zero, QuaternionValue.Identity, Vector3Value.One));
            var second = new EntityInstance(
                EntityId.From(Guid.Parse("c1aa0000-0000-4000-8000-000000000004")),
                blueprint.Id,
                new TransformValue(new(3, 0, 2), QuaternionValue.Identity, Vector3Value.One));
            var area = new AreaDefinition(
                AreaId.From(Guid.Parse("c1aa0000-0000-4000-8000-000000000005")),
                "atelier-factions",
                "Atelier des factions",
                [first, second],
                AreaKind.Interior,
                new AreaDimensions(32, 32));
            var project = new ProjectDefinition(
                ProjectId.From(Guid.Parse("c1aa0000-0000-4000-8000-000000000006")),
                "Studio UX1-N",
                [blueprint, neighborBlueprint],
                [area]);

            var sentinels = new FactionDefinition(
                Guid.Parse("c1aa0000-0000-4000-8000-000000000010"),
                1,
                "test:ux1-n",
                "sentinelles",
                "Sentinelles",
                false,
                [],
                ["guardian"]);
            var allies = new FactionDefinition(
                Guid.Parse("c1aa0000-0000-4000-8000-000000000011"),
                1,
                "test:ux1-n",
                "allies",
                "Alliés du port",
                false,
                [],
                ["allied"]);
            var raiders = new FactionDefinition(
                Guid.Parse("c1aa0000-0000-4000-8000-000000000012"),
                1,
                "test:ux1-n",
                "pillards",
                "Pillards",
                false,
                [],
                ["hostile"]);
            var factions = new FactionCatalog(
                [sentinels, allies, raiders],
                [
                    new(allies.Id, sentinels.Id, 65, "pacte", "allied"),
                    new(raiders.Id, sentinels.Id, -80, "raids", "hostile"),
                ],
                [
                    new("hostile", -100, -1, 1),
                    new("neutral", 0, 49, 1),
                    new("allied", 50, 100, 1),
                ],
                []);
            var originalOwnership = new PlaceableFactionOwnershipProfile(
                Guid.Parse("c1aa0000-0000-4000-8000-000000000013"),
                PlaceableFactionOwnershipProfile.CurrentSchemaVersion,
                "test:ux1-n-ownership",
                sentinels.Id,
                [sentinels.Id],
                false,
                false,
                false,
                false);
            var originalProfile = CreatePlaceableTransformParent(
                blueprint,
                Guid.Parse("c1aa0000-0000-4000-8000-000000000014"),
                "test:ux1-n-profile") with
            {
                FactionId = "legacy:sentinelles",
                FactionOwnership = originalOwnership,
                FactionOwnershipRequiredAtRuntime = false,
            };
            var neighbor = CreatePlaceableTransformParent(
                neighborBlueprint,
                Guid.Parse("c1aa0000-0000-4000-8000-000000000015"),
                "test:ux1-n-neighbor");
            var worldFeatures = new WorldFeatureCatalog(
                project.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-n-catalog",
                placeables: [originalProfile, neighbor]);
            var module = project.Module.WithFactions(factions).WithWorldFeatures(worldFeatures);
            project = new ProjectDefinition(
                project.Id,
                project.Title,
                module,
                project.Blueprints,
                project.Areas,
                project.Dialogues,
                project.Quests);
            var created = ProjectSession.Create(tempRoot, project);
            if (!created.Succeeded)
            {
                Godot.GD.PrintErr(
                    "OPENRPG_FORGE_STUDIO_UX1N_PROJECT_CREATE_FAILED diagnostics=" +
                    string.Join(" | ", created.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
                return FailedUx1NSmoke();
            }
            var opened = editing.Open(tempRoot);
            if (!opened.Succeeded)
            {
                Godot.GD.PrintErr(
                    "OPENRPG_FORGE_STUDIO_UX1N_PROJECT_OPEN_FAILED diagnostics=" +
                    string.Join(" | ", opened.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
                return FailedUx1NSmoke();
            }

            var originalProject = editing.Project!;
            var loadedOriginal = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var loadedNeighbor = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == neighborBlueprint.Id);
            var sourceDraft = editing.CreatePlaceableDraft(blueprint.Id)!;
            var ownershipDraft = sourceDraft.FactionOwnership with
            {
                PrimaryFactionId = sentinels.Id,
                MembershipFactionIds = [sentinels.Id, allies.Id],
                InheritHostility = true,
                UnauthorizedActionsAreCrime = true,
                MembersOnlyAccess = true,
                BlockFriendlyTargeting = true,
            };
            var draft = sourceDraft with
            {
                HasFactionOwnership = true,
                FactionOwnership = ownershipDraft,
                RequireFactionOwnershipRuntime = true,
            };
            var unknown = Guid.Parse("c1aa0000-0000-4000-8000-000000000099");
            var invalidResults = new[]
            {
                editing.PreviewPlaceable(draft with
                {
                    FactionOwnership = ownershipDraft with { Id = Guid.Empty },
                }),
                editing.PreviewPlaceable(draft with
                {
                    FactionOwnership = ownershipDraft with { PrimaryFactionId = Guid.Empty },
                }),
                editing.PreviewPlaceable(draft with
                {
                    FactionOwnership = ownershipDraft with { MembershipFactionIds = [] },
                }),
                editing.PreviewPlaceable(draft with
                {
                    FactionOwnership = ownershipDraft with
                    {
                        MembershipFactionIds = [sentinels.Id, sentinels.Id],
                    },
                }),
                editing.PreviewPlaceable(draft with
                {
                    FactionOwnership = ownershipDraft with
                    {
                        MembershipFactionIds = [sentinels.Id, unknown],
                    },
                }),
                editing.PreviewPlaceable(draft with
                {
                    FactionOwnership = ownershipDraft with { MembershipFactionIds = [allies.Id] },
                }),
            };
            var invalidBlocked = invalidResults.All(result => !result.Succeeded);
            var humanDiagnostics = invalidBlocked && invalidResults.All(result =>
                IsHumanPlaceableMessage(result.Message));

            var directBuilt = editing.TryBuildPlaceable(
                draft,
                dryRun: true,
                out _,
                out var directProfile,
                out _);
            var preview = editing.PreviewPlaceable(draft);
            var dryRun = preview.Succeeded && preview.CanApply &&
                         preview.Summary.Contains("Faction et propriété", StringComparison.Ordinal) &&
                         ReferenceEquals(originalProject, editing.Project);
            var instanceSelection = preview.InstanceIds.SequenceEqual([first.Id, second.Id]);
            var decision = editing.PreviewPlaceableFactionDecision(draft, raiders.Id, "destroy");
            var applied = editing.ApplyApprovedPlaceable();
            var appliedProject = editing.Project!;
            var profile = appliedProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var ownership = profile.FactionOwnership;
            var primaryFaction = applied.Succeeded && ownership?.PrimaryFactionId == sentinels.Id;
            var memberships = ownership?.MembershipFactionIds.SequenceEqual([sentinels.Id, allies.Id]) == true;
            var commandParity = directBuilt &&
                                directProfile?.FactionOwnershipRequiredAtRuntime ==
                                profile.FactionOwnershipRequiredAtRuntime &&
                                FactionOwnershipValuesEqual(directProfile?.FactionOwnership, ownership);
            var preservation = profile.Id == loadedOriginal.Id &&
                               profile.Provenance == loadedOriginal.Provenance &&
                               profile.FactionId == "legacy:sentinelles" &&
                               ReferenceEquals(profile.Reactive, loadedOriginal.Reactive) &&
                               ReferenceEquals(profile.Operations, loadedOriginal.Operations) &&
                               profile.Foundation?.Id == loadedOriginal.Foundation?.Id &&
                               profile.Foundation?.Slug == loadedOriginal.Foundation?.Slug &&
                               ReferenceEquals(
                                   appliedProject.Module.WorldFeatures.Placeables.Single(item =>
                                       item.BlueprintId == neighborBlueprint.Id),
                                   loadedNeighbor) &&
                               ReferenceEquals(appliedProject.Module.Factions, originalProject.Module.Factions);

            var adapter = new PlaceableFactionRuntimeAdapter();
            var resolution = adapter.Resolve(profile, appliedProject.Module.Factions);
            var runtime = adapter.Build(resolution.Handle!, profile, appliedProject.Module.Factions);
            var memberDecision = adapter.Evaluate(
                resolution.Handle!,
                profile,
                appliedProject.Module.Factions,
                new PlaceableFactionActor(allies.Id, new HashSet<Guid> { allies.Id }),
                "damage");
            var enemyDecision = adapter.Evaluate(
                resolution.Handle!,
                profile,
                appliedProject.Module.Factions,
                new PlaceableFactionActor(raiders.Id, new HashSet<Guid> { raiders.Id }),
                "destroy");
            var hostilityAndCrime = enemyDecision.Hostile && enemyDecision.Crime &&
                                    enemyDecision.Code == "RPG_PLACEABLE_FACTION_ACCESS_DENIED";
            var accessAndTargeting = !enemyDecision.AccessAllowed && enemyDecision.TargetAllowed &&
                                     memberDecision.AccessAllowed && !memberDecision.TargetAllowed &&
                                     memberDecision.Code == "RPG_PLACEABLE_FACTION_FRIENDLY_TARGET_BLOCKED";
            var runtimeProjection = resolution.Available && runtime.PrimaryFactionId == sentinels.Id &&
                                    runtime.MembershipFactionIds.SetEquals(
                                        new HashSet<Guid> { sentinels.Id, allies.Id }) &&
                                    runtime.InheritHostility && runtime.UnauthorizedActionsAreCrime &&
                                    runtime.MembersOnlyAccess && runtime.BlockFriendlyTargeting;
            var decisionPreview = decision.Succeeded &&
                                  decision.Code == "RPG_PLACEABLE_FACTION_ACCESS_DENIED" &&
                                  decision.Summary.Contains("hostilité active", StringComparison.Ordinal) &&
                                  decision.Summary.Contains("crime signalé", StringComparison.Ordinal) &&
                                  decision.Summary.Contains("accès refusé", StringComparison.Ordinal) &&
                                  decision.Summary.Contains("ciblage autorisé", StringComparison.Ordinal);

            var scriptHandle = new WorldFeatureScriptService().Query(
                editing.History!.State,
                "placeable",
                profile.Id.ToString("D"),
                1).Single();
            var expectedMemberships = string.Join(',', new[] { sentinels.Id, allies.Id }.OrderBy(id => id));
            var scriptRead = scriptHandle.Properties["faction"] == "legacy:sentinelles" &&
                             scriptHandle.Properties["primaryFaction"] == sentinels.Id.ToString("D") &&
                             scriptHandle.Properties["factionMemberships"] == expectedMemberships &&
                             scriptHandle.Properties["inheritFactionHostility"] == "True" &&
                             scriptHandle.Properties["unauthorizedFactionActionsAreCrime"] == "True" &&
                             scriptHandle.Properties["factionMembersOnlyAccess"] == "True" &&
                             scriptHandle.Properties["blockFriendlyFactionTargeting"] == "True";
            var optional = adapter.Resolve(profile with
            {
                FactionOwnership = null,
                FactionOwnershipRequiredAtRuntime = false,
            }, appliedProject.Module.Factions);
            var required = adapter.Resolve(profile with
            {
                FactionOwnership = null,
                FactionOwnershipRequiredAtRuntime = true,
            }, appliedProject.Module.Factions);
            var optionalRequired = optional.OptionalFallback && optional.DiagnosticCode is null &&
                                   !required.Available &&
                                   required.DiagnosticCode == "WORLD_PLACEABLE_FACTION_OWNERSHIP_REQUIRED_MISSING";

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
            var restoredNeighbor = reopened.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
                item.BlueprintId == neighborBlueprint.Id);
            var persistence = save.Succeeded && reopen.Succeeded && restored is not null &&
                              restored.FactionOwnershipRequiredAtRuntime &&
                              FactionOwnershipValuesEqual(restored.FactionOwnership, ownership) &&
                              restored.FactionId == "legacy:sentinelles" &&
                              restoredNeighbor?.Id == neighbor.Id &&
                              reopened.Project?.Module.Factions.Factions.Count == 3;
            var succeeded = primaryFaction && memberships && hostilityAndCrime && accessAndTargeting &&
                            decisionPreview && dryRun && commandParity && instanceSelection && preservation &&
                            runtimeProjection && scriptRead && optionalRequired && undoRedo && persistence &&
                            invalidBlocked && humanDiagnostics;
            return new(
                succeeded,
                primaryFaction,
                memberships,
                hostilityAndCrime,
                accessAndTargeting,
                decisionPreview,
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
                $"OPENRPG_FORGE_STUDIO_UX1N_CONTRACT_EXCEPTION type={exception.GetType().Name} message={exception.Message}");
            return FailedUx1NSmoke();
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

    internal StudioPlaceableFactionDecisionPreview PreviewPlaceableFactionDecision(
        StudioPlaceableDraft draft,
        Guid? actorFactionId,
        string action)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!draft.HasFactionOwnership)
        {
            return new(false, "Décision indisponible · activez Faction et propriété.",
                "WORLD_PLACEABLE_FACTION_OWNERSHIP_REQUIRED_MISSING");
        }
        if (!TryBuildPlaceable(draft, dryRun: true, out _, out var profile, out var message) || profile is null ||
            Project is null)
        {
            return new(false, $"Décision refusée · {message}", "WORLD_PLACEABLE_FACTION_PREVIEW_INVALID");
        }

        var adapter = new PlaceableFactionRuntimeAdapter();
        var resolution = adapter.Resolve(profile, Project.Module.Factions);
        if (!resolution.Available || resolution.Handle is null)
        {
            return new(false,
                "Décision indisponible · le rattachement de faction n’est pas valide pour le runtime.",
                resolution.DiagnosticCode ?? "WORLD_PLACEABLE_FACTION_PREVIEW_UNAVAILABLE");
        }

        try
        {
            var actor = new PlaceableFactionActor(
                actorFactionId,
                actorFactionId is { } factionId ? new HashSet<Guid> { factionId } : new HashSet<Guid>());
            var decision = adapter.Evaluate(
                resolution.Handle,
                profile,
                Project.Module.Factions,
                actor,
                string.IsNullOrWhiteSpace(action) ? "use" : action.Trim());
            var summary =
                $"Décision · {(decision.IsMember ? "membre" : "non-membre")} · " +
                $"relation {decision.RelationshipScore:0.##} ({HumanizeFactionDisposition(decision.Disposition)}) · " +
                $"hostilité {(decision.Hostile ? "active" : "inactive")} · " +
                $"crime {(decision.Crime ? "signalé" : "non signalé")} · " +
                $"accès {(decision.AccessAllowed ? "autorisé" : "refusé")} · " +
                $"ciblage {(decision.TargetAllowed ? "autorisé" : "bloqué")} · aucun document modifié";
            return new(true, summary, decision.Code);
        }
        catch (InvalidOperationException exception)
        {
            return new(false,
                "Décision indisponible · le profil de faction a changé, prévisualisez à nouveau.",
                exception.Message);
        }
    }

    internal bool EnsureUx1NEvidenceFactions()
    {
        if (History is null || Project is null)
        {
            return false;
        }
        var current = Project.Module.Factions;
        if (current.Factions.Count(item => !item.Archived) >= 3)
        {
            return false;
        }

        var factions = current.Factions.ToList();
        var evidenceDefinitions = new[]
        {
            Faction("74000000-0000-4000-8000-000000000001", "alliance-cendres", "Alliance des Cendres", "allied"),
            Faction("74000000-0000-4000-8000-000000000002", "veilleurs-aube", "Veilleurs de l’Aube", "guardian"),
            Faction("74000000-0000-4000-8000-000000000003", "pillards-breche", "Pillards de la Brèche", "hostile"),
        };
        foreach (var definition in evidenceDefinitions)
        {
            if (factions.Count(item => !item.Archived) >= 3)
            {
                break;
            }
            if (factions.All(item => item.Id != definition.Id &&
                                     !string.Equals(item.Slug, definition.Slug, StringComparison.Ordinal)))
            {
                factions.Add(definition);
            }
        }
        if (factions.Count(item => !item.Archived) < 3)
        {
            return false;
        }

        var active = factions.Where(item => !item.Archived)
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Take(3)
            .ToArray();
        var relationships = current.Relationships.ToList();
        AddEvidenceRelationship(relationships, active[1].Id, active[0].Id, 65, "alliance", "allied");
        AddEvidenceRelationship(relationships, active[2].Id, active[0].Id, -75, "rivalité", "hostile");
        var bands = current.Bands.Count > 0
            ? current.Bands
            :
            [
                new DispositionBand("hostile", -100, -1, 1),
                new DispositionBand("neutral", 0, 49, 1),
                new DispositionBand("allied", 50, 100, 1),
            ];
        var catalog = current with
        {
            Factions = factions,
            Relationships = relationships,
            Bands = bands,
        };
        var command = new SetFactionCatalogCommand(
            CommandId.From(Guid.NewGuid()),
            History.State.Revision,
            catalog,
            false);
        var applied = _setUx1NFactionCatalog.Handle(History.State, command);
        if (!applied.Succeeded)
        {
            return false;
        }
        History.Record(applied);
        InvalidateContentIndexes();
        return true;
    }

    private static FactionDefinition Faction(
        string id,
        string slug,
        string displayName,
        string tag) =>
        new(Guid.Parse(id), 1, "studio:ux1-n-evidence", slug, displayName, false, [], [tag]);

    private static void AddEvidenceRelationship(
        List<FactionRelationship> relationships,
        Guid source,
        Guid target,
        double score,
        string explanation,
        string preset)
    {
        if (source == target || relationships.Any(item => item.SourceId == source && item.TargetId == target))
        {
            return;
        }
        relationships.Add(new(source, target, score, explanation, preset));
    }

    private static string HumanizeFactionDisposition(string disposition) => disposition switch
    {
        "member" => "membre",
        "allied" => "alliée",
        "hostile" => "hostile",
        "neutral" => "neutre",
        "unaffiliated" => "sans faction",
        "undefined" => "non classée",
        _ => disposition,
    };

    private static StudioUx1NSmokeResult FailedUx1NSmoke() => new(
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
