using Forge.Application.Commands;
using Forge.Application.Projects;
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
    private static readonly string[] Ux1AAStandardVerbs =
        ["inspect", "use", "open", "talk", "take", "push", "climb"];

    private StudioPlaceableAssemblyValidationDraft? _approvedUx1AA;
    private ProjectRevision? _approvedUx1AARevision;

    internal StudioUx1AACatalog CreateUx1AACatalog()
    {
        var project = Project ?? throw new InvalidOperationException("Aucun module n’est ouvert.");
        var placeables = project.Blueprints
            .Where(blueprint => blueprint.Kind == BlueprintKind.Placeable)
            .Select(blueprint =>
            {
                var profile = project.Module.WorldFeatures.Placeables.FirstOrDefault(candidate =>
                    candidate.BlueprintId == blueprint.Id);
                var availability = DescribeUx1AAAvailability(profile);
                return new StudioUx1AAPlaceableOption(
                    blueprint.Id,
                    blueprint.DisplayName,
                    availability.Ready,
                    availability.Message);
            })
            .OrderBy(option => option.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        var children = project.Blueprints
            .Where(blueprint => blueprint.Kind == BlueprintKind.Placeable)
            .Select(blueprint => new StudioUx1AAChildOption(blueprint.Id, blueprint.DisplayName))
            .OrderBy(option => option.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        return new(placeables, children);
    }

    internal StudioPlaceableAssemblyValidationDraft? CreateUx1AADraft(
        BlueprintId? requestedBlueprintId = null,
        Guid? requestedSocketId = null,
        Guid? requestedAttachmentId = null)
    {
        var project = Project;
        if (project is null || requestedBlueprintId is null)
        {
            return null;
        }
        var profile = project.Module.WorldFeatures.Placeables.FirstOrDefault(candidate =>
            candidate.BlueprintId == requestedBlueprintId);
        if (profile is null || profile.Foundation is null || !DescribeUx1AAAvailability(profile).Ready)
        {
            return null;
        }

        var composition = profile.CompositionEvents;
        var socket = requestedSocketId is null
            ? composition?.Sockets.FirstOrDefault()
            : composition?.Sockets.FirstOrDefault(candidate => candidate.Id == requestedSocketId);
        if (requestedSocketId is not null && socket is null)
        {
            return null;
        }
        var socketDraft = socket is null
            ? new StudioUx1AASocketDraft(
                StablePlaceableId(profile.BlueprintId, "ux1-aa-socket"),
                false,
                "assembly",
                "assembly",
                "prop",
                DefaultUx1AATransform(),
                "assembly",
                true,
                1)
            : new StudioUx1AASocketDraft(
                socket.Id,
                true,
                socket.SocketId,
                socket.SocketId,
                socket.Kind,
                ToUx1AATransform(socket.LocalTransform),
                string.Join(", ", socket.AllowedAttachmentTags),
                socket.SnapEnabled,
                socket.MaximumAttachments);
        var attachment = requestedAttachmentId is null
            ? composition?.Attachments.FirstOrDefault(candidate => candidate.SocketId == socketDraft.SocketId)
            : composition?.Attachments.FirstOrDefault(candidate =>
                candidate.Id == requestedAttachmentId && candidate.SocketId == socketDraft.SocketId);
        if (requestedAttachmentId is not null && attachment is null)
        {
            return null;
        }
        var attachmentDraft = attachment is null
            ? new StudioUx1AAAttachmentDraft(
                StablePlaceableId(profile.BlueprintId, $"ux1-aa-attachment-{socketDraft.SocketId}"),
                false,
                socketDraft.SocketId,
                null,
                socketDraft.AllowedTagsText,
                DefaultUx1AATransform(),
                string.Empty)
            : new StudioUx1AAAttachmentDraft(
                attachment.Id,
                true,
                attachment.SocketId,
                attachment.ChildBlueprintId,
                string.Join(", ", attachment.Tags),
                ToUx1AATransform(attachment.Transform),
                attachment.PrefabId ?? string.Empty);
        var validation = profile.Foundation.Validation;
        var minimumDrawCalls = Math.Max(1, profile.Foundation.Visual.MeshIds.Count);
        var minimumScripts = profile.Foundation.Components.ScriptBindings.Count;
        var validationDraft = new StudioUx1AAValidationDraft(
            string.Join(", ", validation?.AnimationIds ?? []),
            string.Join(", ", validation?.LockIds ?? []),
            string.Join(", ", validation?.TrapIds ?? []),
            string.Join(", ", validation?.InventoryIds ?? []),
            string.Join(", ", validation?.PhysicsProfileIds ?? []),
            validation?.DrawCallBudget ?? minimumDrawCalls,
            validation?.ScriptBudget ?? minimumScripts,
            minimumDrawCalls,
            minimumScripts);
        var verbs = composition?.MultiVerb.Verbs.Select(ToUx1AAVerb).ToArray() ??
                    DefaultUx1AAVerbs(profile.BlueprintId);
        var context = string.Join(", ", verbs
            .SelectMany(verb => SplitUx1AAValues(verb.RequiredContextText))
            .Distinct(StringComparer.Ordinal));
        var socketChoices = composition?.Sockets
            .Select(candidate => new StudioUx1AAEntryOption(candidate.Id,
                $"{candidate.SocketId} · {candidate.Kind}"))
            .OrderBy(option => option.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray() ?? [];
        var attachmentChoices = composition?.Attachments
            .Where(candidate => candidate.SocketId == socketDraft.SocketId)
            .Select(candidate => new StudioUx1AAEntryOption(candidate.Id,
                string.IsNullOrWhiteSpace(candidate.PrefabId)
                    ? project.Blueprints.FirstOrDefault(blueprint =>
                        blueprint.Id == candidate.ChildBlueprintId)?.DisplayName ?? "Blueprint enfant"
                    : candidate.PrefabId))
            .OrderBy(option => option.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray() ?? [];
        return new(
            profile.BlueprintId,
            profile.Id,
            socketChoices,
            attachmentChoices,
            socketDraft,
            attachmentDraft,
            verbs,
            context,
            validationDraft,
            profile);
    }

    internal string DescribeUx1AAAvailability(BlueprintId blueprintId)
    {
        var profile = Project?.Module.WorldFeatures.Placeables.FirstOrDefault(candidate =>
            candidate.BlueprintId == blueprintId);
        return DescribeUx1AAAvailability(profile).Message;
    }

    internal StudioUx1AAPathPreview PreviewUx1AAAssembly(StudioPlaceableAssemblyValidationDraft draft)
    {
        if (!TryPreviewUx1AACandidate(draft, out var profile, out var message, out var code))
        {
            return FailedUx1AAPath(message, code);
        }
        var adapter = new PlaceableCompositionEventsRuntimeAdapter();
        var resolution = adapter.Resolve(profile!);
        if (!resolution.Available || resolution.Handle is null)
        {
            return FailedUx1AAPath("Le profil d’assemblage canonique reste indisponible.",
                resolution.DiagnosticCode ?? string.Empty);
        }
        var result = adapter.Attach(resolution.Handle, profile!, draft.Attachment.Id);
        return new(true,
            $"Aperçu sans écriture · socket {draft.Socket.SocketId} · assemblage compatible et borné · " +
            $"snap {(draft.Socket.SnapEnabled ? "actif" : "désactivé")} · capacité {draft.Socket.MaximumAttachments} · " +
            $"patch {HumanUx1AAPatch(result.Patch)} · inverse {HumanUx1AAPatch(result.InversePatch)}",
            string.Empty);
    }

    internal StudioUx1AAPathPreview PreviewUx1AAMultiVerb(StudioPlaceableAssemblyValidationDraft draft)
    {
        if (!TryPreviewUx1AACandidate(draft, out var profile, out var message, out var code))
        {
            return FailedUx1AAPath(message, code);
        }
        if (!TryParseUx1AAValues(draft.PreviewContextText, "contexte", false, out var context, out message))
        {
            return FailedUx1AAPath(message);
        }
        var adapter = new PlaceableCompositionEventsRuntimeAdapter();
        var resolution = adapter.Resolve(profile!);
        if (!resolution.Available || resolution.Handle is null)
        {
            return FailedUx1AAPath("Le profil multi-verbes canonique reste indisponible.",
                resolution.DiagnosticCode ?? string.Empty);
        }
        var resolved = adapter.ResolveVerbs(resolution.Handle, profile!, context.ToHashSet(StringComparer.Ordinal));
        var labels = resolved.Select(verb => HumanizeUx1AAVerbLabel(verb.Label)).ToArray();
        return new(true,
            $"Aperçu sans écriture · {resolved.Count} action(s) disponible(s) dans ce contexte · " +
            (labels.Length == 0 ? "aucune action éligible" : string.Join(" → ", labels)),
            string.Empty);
    }

    internal StudioUx1AAPathPreview PreviewUx1AAValidation(StudioPlaceableAssemblyValidationDraft draft)
    {
        if (!TryPreviewUx1AACandidate(draft, out var profile, out var message, out var code))
        {
            return FailedUx1AAPath(message, code);
        }
        var validation = profile!.Foundation!.Validation!;
        return new(true,
            $"Validation localisée sans écriture · collision et navigation cohérentes · " +
            $"animations {validation.AnimationIds.Count} · locks {validation.LockIds.Count} · " +
            $"pièges {validation.TrapIds.Count} · inventaires {validation.InventoryIds.Count} · " +
            $"physique {validation.PhysicsProfileIds.Count} · budgets {validation.DrawCallBudget} appels / " +
            $"{validation.ScriptBudget} scripts",
            string.Empty);
    }

    internal StudioUx1AAPreview PreviewUx1AA(StudioPlaceableAssemblyValidationDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelUx1AAPreview();
        if (!TryPreviewUx1AACandidate(draft, out _, out var message, out var code))
        {
            return FailedUx1AAPreview(message, code);
        }
        var assembly = PreviewUx1AAAssembly(draft);
        var verbs = PreviewUx1AAMultiVerb(draft);
        var validation = PreviewUx1AAValidation(draft);
        var failed = new[] { assembly, verbs, validation }.FirstOrDefault(path => !path.Succeeded);
        if (failed is not null)
        {
            return FailedUx1AAPreview(failed.Summary, failed.DiagnosticCode);
        }
        _approvedUx1AA = draft;
        _approvedUx1AARevision = History?.State.Revision;
        var summary = string.Join('\n', new[]
        {
            "Trois parcours prévisualisés sans modifier le module.",
            assembly.Summary,
            verbs.Summary,
            validation.Summary,
            "Application transactionnelle prête · les autres capacités et placeables restent intacts.",
        });
        return new(true, "Aperçu validé · aucun document n’a été modifié.", summary, true,
            assembly, verbs, validation);
    }

    internal StudioUx1AAActionResult ApplyApprovedUx1AA()
    {
        if (_approvedUx1AA is null)
        {
            return new(false, "Prévisualisez les sockets, actions et contrôles avant de les appliquer.");
        }
        if (History is null || _approvedUx1AARevision is null ||
            History.State.Revision != _approvedUx1AARevision.Value)
        {
            CancelUx1AAPreview();
            return new(false,
                "Cet aperçu est périmé car le module a changé. Prévisualisez à nouveau avant d’appliquer.");
        }
        if (!TryBuildUx1AA(_approvedUx1AA, false, out var command, out _, out var message, out _))
        {
            CancelUx1AAPreview();
            return new(false, message);
        }
        var result = _setPlaceableCatalog.Handle(History.State, command!);
        CancelUx1AAPreview();
        if (!result.Succeeded)
        {
            var diagnostic = result.Diagnostics.FirstOrDefault();
            return new(false, HumanizeUx1AADiagnostic(diagnostic?.Code));
        }
        History.Record(result);
        InvalidateContentIndexes();
        return new(true, "Sockets, actions et validation appliqués · enregistrez pour écrire le module.");
    }

    internal void CancelUx1AAPreview()
    {
        _approvedUx1AA = null;
        _approvedUx1AARevision = null;
    }

    internal void CloseUx1AA() => CancelUx1AAPreview();

    internal StudioUx1AAEvidenceWorkspace? CreateUx1AAEvidenceWorkspace()
    {
        var rootPath = StudioUx1AAEvidenceWorkspace.CreateRootPath();
        var evidenceSession = new StudioSessionController();
        var workspace = new StudioUx1AAEvidenceWorkspace(evidenceSession, rootPath);
        try
        {
            var blueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1aa1000-0000-4000-8000-000000000001")),
                "reliquaire-preuve-ux1-aa",
                "Reliquaire d’assemblage",
                BlueprintKind.Placeable);
            var childBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1aa1000-0000-4000-8000-000000000002")),
                "levier-preuve-ux1-aa",
                "Levier enfant",
                BlueprintKind.Placeable);
            var entity = new EntityInstance(
                EntityId.From(Guid.Parse("c1aa1000-0000-4000-8000-000000000003")),
                blueprint.Id,
                TransformValue.Identity);
            var area = new AreaDefinition(
                AreaId.From(Guid.Parse("c1aa1000-0000-4000-8000-000000000004")),
                "atelier-preuve-ux1-aa",
                "Atelier de preuve UX1-AA",
                [entity],
                AreaKind.Interior,
                new AreaDimensions(24, 24));
            var project = new ProjectDefinition(
                ProjectId.From(Guid.Parse("c1aa1000-0000-4000-8000-000000000005")),
                "Preuve Studio UX1-AA",
                [blueprint, childBlueprint],
                [area]);
            var profile = CreatePlaceableTransformParent(
                blueprint,
                Guid.Parse("c1aa1000-0000-4000-8000-000000000010"),
                "evidence:ux1-aa-profile");
            var sourceFoundation = profile.Foundation!;
            var foundation = sourceFoundation with
            {
                Components = sourceFoundation.Components with { HasHealth = true },
            };
            var damageable = new PlaceableDamageableProfile(
                StablePlaceableId(blueprint.Id, "ux1-aa-evidence-damageable"),
                100,
                100,
                5,
                new Dictionary<string, double> { ["physical"] = .1 },
                [.75, .5, .25],
                "vfx:ux1-aa-evidence-destruction",
                "debris:ux1-aa-evidence",
                true,
                10);
            var operations = new PlaceableOperationsProfile(
                StablePlaceableId(blueprint.Id, "ux1-aa-evidence-operations"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                "evidence:ux1-aa-operations",
                null,
                CreateContainerCompatibilityStateMachine(blueprint.Id),
                damageable,
                CreateContainerCompatibilityProtection(blueprint.Id),
                null);
            var damageEvents = new PlaceableDamageEventsProfile(
                StablePlaceableId(blueprint.Id, "ux1-aa-evidence-damage-events"),
                "1.1.0",
                "DamageApplied",
                "EntityDestroyed",
                true,
                true,
                true,
                true,
                true,
                true);
            profile = profile with
            {
                Foundation = foundation,
                Operations = operations,
                DamageEvents = damageEvents,
            };
            var childProfile = CreatePlaceableTransformParent(
                childBlueprint,
                Guid.Parse("c1aa1000-0000-4000-8000-000000000011"),
                "evidence:ux1-aa-child");
            var catalog = new WorldFeatureCatalog(
                project.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "evidence:ux1-aa-catalog",
                placeables: [profile, childProfile]);
            var module = project.Module.WithWorldFeatures(catalog);
            project = new ProjectDefinition(project.Id, project.Title, module, project.Blueprints, project.Areas);

            var created = ProjectSession.Create(rootPath, project);
            if (!created.Succeeded)
            {
                Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX1AA_EVIDENCE_CREATE_FAILED diagnostics=" +
                                  string.Join(" | ", created.Diagnostics.Select(item => item.Code)));
                workspace.Dispose();
                return null;
            }
            var opened = evidenceSession.Open(rootPath);
            if (!opened.Succeeded)
            {
                Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX1AA_EVIDENCE_OPEN_FAILED diagnostics=" +
                                  string.Join(" | ", opened.Diagnostics.Select(item => item.Code)));
                workspace.Dispose();
                return null;
            }
            return workspace;
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AA_EVIDENCE_EXCEPTION type={exception.GetType().Name} message={exception.Message}");
            workspace.Dispose();
            return null;
        }
    }

    internal StudioUx1AASmokeResult RunUx1AAContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux1aa-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var blueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1aa0000-0000-4000-8000-000000000001")),
                "reliquaire-assemblable",
                "Reliquaire assemblable",
                BlueprintKind.Placeable);
            var childBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1aa0000-0000-4000-8000-000000000002")),
                "levier-enfant",
                "Levier enfant",
                BlueprintKind.Placeable);
            var blockedBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1aa0000-0000-4000-8000-000000000003")),
                "socle-incomplet",
                "Socle incomplet",
                BlueprintKind.Placeable);
            var missingBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1aa0000-0000-4000-8000-000000000004")),
                "ornement-sans-profil",
                "Ornement sans profil",
                BlueprintKind.Placeable);
            var entity = new EntityInstance(
                EntityId.From(Guid.Parse("c1aa0000-0000-4000-8000-000000000005")),
                blueprint.Id,
                new TransformValue(Vector3Value.Zero, QuaternionValue.Identity, Vector3Value.One));
            var area = new AreaDefinition(
                AreaId.From(Guid.Parse("c1aa0000-0000-4000-8000-000000000006")),
                "atelier-assemblage",
                "Atelier d’assemblage",
                [entity],
                AreaKind.Interior,
                new AreaDimensions(32, 32));
            var project = new ProjectDefinition(
                ProjectId.From(Guid.Parse("c1aa0000-0000-4000-8000-000000000007")),
                "Studio UX1-AA",
                [blueprint, childBlueprint, blockedBlueprint, missingBlueprint],
                [area]);

            var original = CreatePlaceableTransformParent(
                blueprint,
                Guid.Parse("c1aa0000-0000-4000-8000-000000000010"),
                "test:ux1-aa-profile");
            var scripts = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["inspect"] = "scripts/reliquaire_inspect",
                ["use"] = "scripts/reliquaire_use",
            };
            var originalFoundation = original.Foundation!;
            var foundation = originalFoundation with
            {
                Components = originalFoundation.Components with
                {
                    HasHealth = true,
                    HasPhysics = true,
                    ScriptBindings = scripts,
                },
                Transform = originalFoundation.Transform with
                {
                    TransformAnimationId = "animation:reliquaire",
                },
                Validation = new(
                    ["animation:reliquaire"],
                    ["lock:ancien"],
                    ["trap:ancien"],
                    ["inventory:ancien"],
                    ["physics:ancien"],
                    3,
                    2),
            };
            var damageable = new PlaceableDamageableProfile(
                StablePlaceableId(blueprint.Id, "ux1-aa-damageable"),
                100,
                100,
                5,
                new Dictionary<string, double> { ["physical"] = .1 },
                [.75, .5, .25],
                "vfx:reliquaire-destruction",
                "debris:reliquaire",
                true,
                10);
            var operations = new PlaceableOperationsProfile(
                StablePlaceableId(blueprint.Id, "ux1-aa-operations"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-aa-operations",
                null,
                CreateContainerCompatibilityStateMachine(blueprint.Id),
                damageable,
                CreateContainerCompatibilityProtection(blueprint.Id),
                null);
            var damageEvents = new PlaceableDamageEventsProfile(
                StablePlaceableId(blueprint.Id, "ux1-aa-damage-events"),
                "1.1.0",
                "DamageApplied",
                "EntityDestroyed",
                true,
                true,
                true,
                true,
                true,
                true);
            original = original with
            {
                Foundation = foundation,
                Operations = operations,
                DamageEvents = damageEvents,
                Physics = new(
                    PlaceableWorldProfile.BodyMode.Rigid,
                    20,
                    ["lock-y"],
                    true,
                    5,
                    true,
                    "server"),
            };
            var child = CreatePlaceableTransformParent(
                childBlueprint,
                Guid.Parse("c1aa0000-0000-4000-8000-000000000011"),
                "test:ux1-aa-child");
            var blocked = new PlaceableWorldProfile(
                Guid.Parse("c1aa0000-0000-4000-8000-000000000012"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-aa-blocked",
                blockedBlueprint.Id,
                null,
                null,
                null,
                new([], new(false, 3, 4, true, "placeable.blocked")),
                null);
            var catalog = new WorldFeatureCatalog(
                project.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-aa-catalog",
                placeables: [original, child, blocked]);
            var module = project.Module.WithWorldFeatures(catalog);
            project = new ProjectDefinition(project.Id, project.Title, module, project.Blueprints, project.Areas);

            var created = ProjectSession.Create(tempRoot, project);
            if (!created.Succeeded)
            {
                return FailedUx1AASmoke("PROJECT_CREATE", created.Diagnostics.Select(item => item.Code));
            }
            var opened = editing.Open(tempRoot);
            if (!opened.Succeeded)
            {
                return FailedUx1AASmoke("PROJECT_OPEN", opened.Diagnostics.Select(item => item.Code));
            }
            var originalProject = editing.Project!;
            var originalPlaceableOrder = originalProject.Module.WorldFeatures.Placeables
                .Select(item => item.Id)
                .ToArray();
            var loadedOriginal = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var originalNeighborPlaceables = originalProject.Module.WorldFeatures.Placeables
                .Where(item => item.BlueprintId != blueprint.Id)
                .ToArray();
            var loadedFoundation = loadedOriginal.Foundation!;
            var options = editing.CreateUx1AACatalog();
            var collisionBlocked = loadedOriginal with
            {
                Foundation = loadedFoundation with
                {
                    Collision = loadedFoundation.Collision with { Enabled = false },
                },
            };
            var operationsBlocked = loadedOriginal with { Operations = null };
            var damageBlocked = loadedOriginal with { DamageEvents = null, CompositionEvents = null };
            var divergentDamage = loadedOriginal with
            {
                CompositionEvents = new(
                    StablePlaceableId(blueprint.Id, "ux1-aa-divergent-composition"),
                    WorldFeatureCatalog.CurrentSchemaVersion,
                    "test:ux1-aa-divergent",
                    loadedOriginal.DamageEvents! with { DamagedBinding = "OtherDamageApplied" },
                    [],
                    [],
                    new(StablePlaceableId(blueprint.Id, "ux1-aa-divergent-verbs"), [])),
            };
            var prerequisiteDiagnostics = new[]
            {
                DescribeUx1AAAvailability(collisionBlocked),
                DescribeUx1AAAvailability(operationsBlocked),
                DescribeUx1AAAvailability(damageBlocked),
                DescribeUx1AAAvailability(divergentDamage),
            };
            var explicitSelection = editing.CreateUx1AADraft() is null &&
                                    editing.CreateUx1AADraft(BlueprintId.From(
                                        Guid.Parse("c1aa0000-0000-4000-8000-000000000099"))) is null &&
                                    editing.CreateUx1AADraft(
                                        blueprint.Id,
                                        Guid.Parse("c1aa0000-0000-4000-8000-000000000098")) is null;
            var prerequisitesRefused = editing.CreateUx1AADraft(blockedBlueprint.Id) is null &&
                                       editing.CreateUx1AADraft(missingBlueprint.Id) is null &&
                                       options.Placeables.Single(option => option.BlueprintId == blockedBlueprint.Id)
                                           is { Ready: false } &&
                                       options.Placeables.Single(option => option.BlueprintId == missingBlueprint.Id)
                                           is { Ready: false } &&
                                       IsHumanUx1AAMessage(editing.DescribeUx1AAAvailability(blockedBlueprint.Id)) &&
                                       IsHumanUx1AAMessage(editing.DescribeUx1AAAvailability(missingBlueprint.Id)) &&
                                       prerequisiteDiagnostics.All(item => !item.Ready &&
                                           IsHumanUx1AAMessage(item.Message));

            var source = editing.CreateUx1AADraft(blueprint.Id)!;
            var socketTransform = source.Socket.Transform with
            {
                Position = new(1, 2, 3),
                RotationDegrees = new(0, 90, 0),
                Scale = new(1, 1, 1),
                Pivot = new(.1f, .2f, .3f),
                SnapIncrement = .25,
                ParentId = entity.Id.Value,
                AlignToSurface = true,
            };
            var attachmentTransform = source.Attachment.Transform with
            {
                Position = new(.5f, 0, 0),
                RotationDegrees = new(0, 45, 0),
                Scale = new(.75f, .75f, .75f),
                Pivot = new(0, .1f, 0),
                SnapIncrement = .125,
                ParentId = entity.Id.Value,
                AlignToSurface = false,
            };
            var draft = source with
            {
                Socket = source.Socket with
                {
                    SocketId = "reliquaire",
                    Kind = "lever",
                    Transform = socketTransform,
                    AllowedTagsText = "lever, rune",
                    SnapEnabled = true,
                    MaximumAttachments = 2,
                },
                Attachment = source.Attachment with
                {
                    SocketId = "reliquaire",
                    ChildBlueprintId = childBlueprint.Id,
                    TagsText = "rune",
                    Transform = attachmentTransform,
                    PrefabId = "prefab:reliquaire-levier",
                },
                PreviewContextText = "portable, custom-context, unlocked, interlocutor, climbable",
                Validation = source.Validation with
                {
                    AnimationIdsText = "animation:reliquaire, animation:activation",
                    LockIdsText = "lock:reliquaire",
                    TrapIdsText = "trap:reliquaire",
                    InventoryIdsText = "inventory:reliquaire",
                    PhysicsProfileIdsText = "physics:reliquaire",
                    DrawCallBudget = 4,
                    ScriptBudget = 3,
                },
            };
            var assemblyPreview = editing.PreviewUx1AAAssembly(draft);
            var verbPreview = editing.PreviewUx1AAMultiVerb(draft);
            var validationPreview = editing.PreviewUx1AAValidation(draft);
            var sectionDryRun = new[] { assemblyPreview, verbPreview, validationPreview }
                                    .All(preview => preview.Succeeded) &&
                                ReferenceEquals(originalProject, editing.Project);

            var invalidSocket = editing.PreviewUx1AA(draft with
            {
                Socket = draft.Socket with { MaximumAttachments = 0 },
            });
            var invalidAttachment = editing.PreviewUx1AA(draft with
            {
                Attachment = draft.Attachment with { TagsText = "incompatible" },
            });
            var duplicateVerb = draft.Verbs.ToArray();
            duplicateVerb[^1] = duplicateVerb[^1] with { VerbId = "inspect" };
            var invalidVerb = editing.PreviewUx1AA(draft with { Verbs = duplicateVerb });
            var invalidAnimation = editing.PreviewUx1AA(draft with
            {
                Validation = draft.Validation with { AnimationIdsText = "animation:activation" },
            });
            var invalidPhysics = editing.PreviewUx1AA(draft with
            {
                Validation = draft.Validation with { PhysicsProfileIdsText = string.Empty },
            });
            var invalidBudget = editing.PreviewUx1AA(draft with
            {
                Validation = draft.Validation with { DrawCallBudget = 0, ScriptBudget = 0 },
            });
            var invalidSocketBlocked = !invalidSocket.Succeeded;
            var invalidAttachmentBlocked = !invalidAttachment.Succeeded;
            var invalidVerbBlocked = !invalidVerb.Succeeded;
            var invalidLockBlocked = !TryBuildUx1AAValidation(
                loadedFoundation with
                {
                    Components = loadedFoundation.Components with { HasLock = true },
                },
                draft.Validation with { LockIdsText = string.Empty },
                out _,
                out var invalidLockMessage,
                out _);
            var invalidTrapBlocked = !TryBuildUx1AAValidation(
                loadedFoundation with
                {
                    Components = loadedFoundation.Components with { HasTrap = true },
                },
                draft.Validation with { TrapIdsText = string.Empty },
                out _,
                out var invalidTrapMessage,
                out _);
            var invalidInventoryBlocked = !TryBuildUx1AAValidation(
                loadedFoundation with
                {
                    Components = loadedFoundation.Components with { HasContainer = true },
                },
                draft.Validation with { InventoryIdsText = string.Empty },
                out _,
                out var invalidInventoryMessage,
                out _);
            var invalidValidationResults = new[]
            {
                invalidAnimation,
                invalidPhysics,
                invalidBudget,
            };
            var invalidValidationBlocked = invalidValidationResults.All(result => !result.Succeeded) &&
                                           invalidLockBlocked && invalidTrapBlocked && invalidInventoryBlocked;
            var humanDiagnostics = new[] { invalidSocket, invalidAttachment, invalidVerb }
                .Concat(invalidValidationResults)
                .All(result => IsHumanUx1AAMessage(result.Message)) &&
                                   new[] { invalidLockMessage, invalidTrapMessage, invalidInventoryMessage }
                                       .All(IsHumanUx1AAMessage);

            var stalePreview = editing.PreviewUx1AA(draft);
            var staleCatalog = originalProject.Module.WorldFeatures;
            var staleReplacement = new WorldFeatureCatalog(
                staleCatalog.Id,
                staleCatalog.SchemaVersion,
                "test:ux1-aa-neighbor-revision",
                staleCatalog.Placeables,
                staleCatalog.Doors,
                staleCatalog.Triggers,
                staleCatalog.Encounters,
                staleCatalog.VehicleRoutes,
                staleCatalog.Merchants,
                staleCatalog.Companions,
                staleCatalog.Scripts);
            var staleCommand = new SetWorldFeatureCatalogCommand(
                CommandId.From(Guid.NewGuid()),
                editing.History!.State.Revision,
                staleReplacement,
                false);
            var staleMutation = editing._setPlaceableCatalog.Handle(editing.History.State, staleCommand);
            if (staleMutation.Succeeded)
            {
                editing.History.Record(staleMutation);
            }
            var staleApply = editing.ApplyApprovedUx1AA();
            var staleUndo = editing.Undo();
            var stalePreviewBlocked = stalePreview.Succeeded && staleMutation.Succeeded &&
                                      !staleApply.Succeeded &&
                                      staleApply.Message.Contains("périmé", StringComparison.OrdinalIgnoreCase) &&
                                      staleUndo.Succeeded && ReferenceEquals(originalProject, editing.Project);

            var directBuilt = editing.TryBuildUx1AA(
                draft,
                true,
                out _,
                out var directProfile,
                out _,
                out _);
            var preview = editing.PreviewUx1AA(draft);
            var dryRun = sectionDryRun && preview.Succeeded && preview.CanApply &&
                         ReferenceEquals(originalProject, editing.Project);
            var applied = editing.ApplyApprovedUx1AA();
            var appliedProject = editing.Project!;
            var appliedProfile = appliedProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var commandParity = directBuilt && directProfile is not null &&
                                Ux1AAValuesEqual(directProfile, appliedProfile);
            explicitSelection = explicitSelection && editing.CreateUx1AADraft(
                blueprint.Id,
                draft.Socket.Id,
                Guid.Parse("c1aa0000-0000-4000-8000-000000000097")) is null;
            var damageAuthorityPreserved = applied.Succeeded &&
                                           ReferenceEquals(appliedProfile.DamageEvents, loadedOriginal.DamageEvents) &&
                                           ReferenceEquals(appliedProfile.CompositionEvents!.DamageEvents,
                                               loadedOriginal.DamageEvents) &&
                                           PlaceableDamageEventsResolver.Resolve(appliedProfile).Authority ==
                                           PlaceableDamageEventsAuthority.DualCanonical;
            var appliedFoundation = appliedProfile.Foundation!;
            var neighborCapabilitiesPreserved =
                ReferenceEquals(appliedProfile.Operations, loadedOriginal.Operations) &&
                ReferenceEquals(appliedProfile.Reactive, loadedOriginal.Reactive) &&
                ReferenceEquals(appliedProfile.Physics, loadedOriginal.Physics) &&
                ReferenceEquals(appliedFoundation.Visual, loadedFoundation.Visual) &&
                ReferenceEquals(appliedFoundation.Collision, loadedFoundation.Collision) &&
                ReferenceEquals(appliedFoundation.Components, loadedFoundation.Components) &&
                ReferenceEquals(appliedFoundation.Transform, loadedFoundation.Transform);
            var appliedNeighborPlaceables = appliedProject.Module.WorldFeatures.Placeables
                .Where(item => item.BlueprintId != blueprint.Id)
                .ToArray();
            var neighborPlaceablePreserved = appliedNeighborPlaceables.Length == originalNeighborPlaceables.Length &&
                                             appliedNeighborPlaceables.Zip(originalNeighborPlaceables)
                                                 .All(pair => ReferenceEquals(pair.First, pair.Second)) &&
                                             appliedProject.Module.WorldFeatures.Placeables
                                                 .Select(item => item.Id)
                                                 .SequenceEqual(originalPlaceableOrder);
            var undo = editing.Undo();
            var undone = undo.Succeeded && ReferenceEquals(originalProject, editing.Project);
            var redo = editing.Redo();
            var redone = redo.Succeeded && ReferenceEquals(appliedProject, editing.Project);
            var undoRedo = undone && redone;
            var saved = editing.Save();
            editing.Close();
            var reopen = reopened.Open(tempRoot);
            var persisted = reopened.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
                item.BlueprintId == blueprint.Id);
            var persistedSocket = persisted?.CompositionEvents?.Sockets.SingleOrDefault(item =>
                item.SocketId == "reliquaire");
            var persistedAttachment = persisted?.CompositionEvents?.Attachments.SingleOrDefault(item =>
                item.Id == draft.Attachment.Id);
            var persistedValidation = persisted?.Foundation?.Validation;
            var persistence = saved.Succeeded && reopen.Succeeded && persisted is not null &&
                              persistedSocket is
                              {
                                  Kind: "lever",
                                  SnapEnabled: true,
                                  MaximumAttachments: 2,
                              } && persistedSocket.AllowedAttachmentTags.SequenceEqual(
                                  ["lever", "rune"], StringComparer.Ordinal) &&
                              persistedSocket.LocalTransform == ToUx1AATransform(socketTransform) &&
                              persistedAttachment is
                              {
                                  ChildBlueprintId: var persistedChild,
                                  PrefabId: "prefab:reliquaire-levier",
                              } && persistedChild == childBlueprint.Id &&
                              persistedAttachment.Tags.SequenceEqual(["rune"], StringComparer.Ordinal) &&
                              persistedAttachment.Transform == ToUx1AATransform(attachmentTransform) &&
                              persistedValidation is
                              {
                                  DrawCallBudget: 4,
                                  ScriptBudget: 3,
                              } && persistedValidation.AnimationIds.SequenceEqual(
                                  ["animation:reliquaire", "animation:activation"], StringComparer.Ordinal) &&
                              persistedValidation.LockIds.SequenceEqual(["lock:reliquaire"], StringComparer.Ordinal) &&
                              persistedValidation.TrapIds.SequenceEqual(["trap:reliquaire"], StringComparer.Ordinal) &&
                              persistedValidation.InventoryIds.SequenceEqual(
                                  ["inventory:reliquaire"], StringComparer.Ordinal) &&
                              persistedValidation.PhysicsProfileIds.SequenceEqual(
                                  ["physics:reliquaire"], StringComparer.Ordinal) &&
                              Ux1AAVerbsEqual(persisted.CompositionEvents!.MultiVerb.Verbs, draft.Verbs) &&
                              persisted.CompositionEvents.DamageEvents == persisted.DamageEvents &&
                              reopened.Project?.Module.WorldFeatures.Placeables.Any(item =>
                                  item.BlueprintId == childBlueprint.Id) == true;

            var succeeded = explicitSelection && prerequisitesRefused && assemblyPreview.Succeeded &&
                            verbPreview.Succeeded && validationPreview.Succeeded && dryRun && commandParity &&
                            invalidSocketBlocked && invalidAttachmentBlocked && invalidVerbBlocked &&
                            invalidValidationBlocked && stalePreviewBlocked && damageAuthorityPreserved &&
                            neighborCapabilitiesPreserved && neighborPlaceablePreserved && undoRedo &&
                            persistence && humanDiagnostics;
            return new(
                succeeded,
                explicitSelection,
                prerequisitesRefused,
                assemblyPreview.Succeeded,
                verbPreview.Succeeded,
                validationPreview.Succeeded,
                dryRun,
                commandParity,
                invalidSocketBlocked,
                invalidAttachmentBlocked,
                invalidVerbBlocked,
                invalidValidationBlocked,
                stalePreviewBlocked,
                damageAuthorityPreserved,
                neighborCapabilitiesPreserved,
                neighborPlaceablePreserved,
                undoRedo,
                persistence,
                humanDiagnostics);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AA_CONTRACT_EXCEPTION type={exception.GetType().Name} message={exception.Message}");
            return FailedUx1AASmoke();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }
    }

    private bool TryPreviewUx1AACandidate(
        StudioPlaceableAssemblyValidationDraft draft,
        out PlaceableWorldProfile? profile,
        out string message,
        out string diagnosticCode)
    {
        diagnosticCode = string.Empty;
        if (!TryBuildUx1AA(draft, true, out var command, out profile, out message, out diagnosticCode))
        {
            return false;
        }
        var result = _setPlaceableCatalog.Handle(History!.State, command!);
        if (result.Succeeded)
        {
            return true;
        }
        diagnosticCode = result.Diagnostics.FirstOrDefault()?.Code ?? "world.placeable.ux1_aa_preview_failed";
        message = HumanizeUx1AADiagnostic(diagnosticCode);
        return false;
    }

    private bool TryBuildUx1AA(
        StudioPlaceableAssemblyValidationDraft draft,
        bool dryRun,
        out SetWorldFeatureCatalogCommand? command,
        out PlaceableWorldProfile? profile,
        out string message,
        out string diagnosticCode)
    {
        command = null;
        profile = null;
        message = string.Empty;
        diagnosticCode = string.Empty;
        var project = Project;
        var history = History;
        if (project is null || history is null)
        {
            message = "Aucun module n’est ouvert.";
            return false;
        }
        var catalog = project.Module.WorldFeatures;
        var current = catalog.Placeables.FirstOrDefault(candidate =>
            candidate.Id == draft.ProfileId && candidate.BlueprintId == draft.BlueprintId);
        if (current is null)
        {
            message = "Le profil choisi n’existe plus dans le module.";
            return false;
        }
        if (!ReferenceEquals(current, draft.OriginalProfile))
        {
            message = "Ce placeable a changé depuis l’ouverture. Rechargez sa configuration avant de continuer.";
            return false;
        }
        var availability = DescribeUx1AAAvailability(current);
        var sourceFoundation = current.Foundation;
        if (!availability.Ready || sourceFoundation is null || current.Operations?.Damageable is null)
        {
            message = availability.Message;
            diagnosticCode = availability.Code;
            return false;
        }
        var damage = PlaceableDamageEventsResolver.Resolve(current);
        var damageProfile = damage.Profile;
        if (!damage.Available || damageProfile is null || damage.DiagnosticCode is not null)
        {
            message = "L’autorité des événements de dégâts doit être canonique avant de configurer l’assemblage.";
            diagnosticCode = damage.DiagnosticCode ?? "world.placeable.damage_events_required_missing";
            return false;
        }
        if (draft.Socket.Existing && !string.Equals(
                draft.Socket.SocketId.Trim(), draft.Socket.OriginalSocketId, StringComparison.Ordinal))
        {
            message = "L’identifiant d’un socket existant reste stable. Créez un autre socket pour changer ce nom.";
            diagnosticCode = "world.placeable.attachment_sockets_invalid";
            return false;
        }
        if (string.IsNullOrWhiteSpace(draft.Socket.SocketId) || string.IsNullOrWhiteSpace(draft.Socket.Kind) ||
            draft.Socket.MaximumAttachments < 1 || !ValidUx1AATransform(draft.Socket.Transform))
        {
            message = "Le socket exige un nom, un type, une capacité positive et une transformation bornée.";
            diagnosticCode = "world.placeable.attachment_sockets_invalid";
            return false;
        }
        if (!TryParseUx1AAValues(draft.Socket.AllowedTagsText, "tag de socket", true,
                out var allowedTags, out message))
        {
            diagnosticCode = "world.placeable.attachment_sockets_invalid";
            return false;
        }
        if (draft.Attachment.ChildBlueprintId is not { } childBlueprintId ||
            project.Blueprints.All(candidate => candidate.Id != childBlueprintId ||
                                                candidate.Kind != BlueprintKind.Placeable))
        {
            message = "Choisissez un blueprint Placeable enfant existant pour l’assemblage.";
            diagnosticCode = "world.placeable.attachment_invalid";
            return false;
        }
        if (!TryParseUx1AAValues(draft.Attachment.TagsText, "tag d’assemblage", true,
                out var attachmentTags, out message) || !allowedTags.Intersect(attachmentTags,
                StringComparer.Ordinal).Any() || !ValidUx1AATransform(draft.Attachment.Transform))
        {
            message = string.IsNullOrWhiteSpace(message)
                ? "L’assemblage exige au moins un tag accepté par le socket et une transformation bornée."
                : message;
            diagnosticCode = "world.placeable.attachment_invalid";
            return false;
        }
        if (!TryBuildUx1AAVerbs(draft.Verbs, out var verbs, out message))
        {
            diagnosticCode = "world.placeable.multi_verb_invalid";
            return false;
        }
        if (!TryBuildUx1AAValidation(sourceFoundation, draft.Validation, out var validation, out message,
                out diagnosticCode))
        {
            return false;
        }

        var socket = new PlaceableAttachmentSocketProfile(
            draft.Socket.Id,
            draft.Socket.SocketId.Trim(),
            draft.Socket.Kind.Trim(),
            ToUx1AATransform(draft.Socket.Transform),
            allowedTags,
            draft.Socket.SnapEnabled,
            draft.Socket.MaximumAttachments);
        var attachment = new PlaceableAuthoredAttachmentProfile(
            draft.Attachment.Id,
            socket.SocketId,
            childBlueprintId,
            attachmentTags,
            ToUx1AATransform(draft.Attachment.Transform),
            string.IsNullOrWhiteSpace(draft.Attachment.PrefabId) ? null : draft.Attachment.PrefabId.Trim());
        var originalComposition = current.CompositionEvents;
        var sourceSockets = originalComposition?.Sockets ?? [];
        var sockets = sourceSockets.Any(candidate => candidate.Id == draft.Socket.Id)
            ? sourceSockets.Select(candidate => candidate.Id == draft.Socket.Id ? socket : candidate).ToArray()
            : sourceSockets.Append(socket).ToArray();
        var sourceAttachments = originalComposition?.Attachments ?? [];
        var attachments = sourceAttachments.Any(candidate => candidate.Id == draft.Attachment.Id)
            ? sourceAttachments.Select(candidate => candidate.Id == draft.Attachment.Id ? attachment : candidate)
                .ToArray()
            : sourceAttachments.Append(attachment).ToArray();
        if (sockets.Select(candidate => candidate.SocketId).Distinct(StringComparer.Ordinal).Count() != sockets.Length)
        {
            message = "Chaque socket doit porter un nom unique dans ce placeable.";
            diagnosticCode = "world.placeable.attachment_sockets_invalid";
            return false;
        }
        if (attachments.Count(candidate => candidate.SocketId == socket.SocketId) > socket.MaximumAttachments)
        {
            message = "La capacité du socket est inférieure au nombre d’assemblages qu’il contient.";
            diagnosticCode = "world.placeable.attachment_socket_capacity_exceeded";
            return false;
        }
        var multiVerb = new PlaceableMultiVerbProfile(
            originalComposition?.MultiVerb.Id ?? StablePlaceableId(current.BlueprintId, "ux1-aa-multi-verb"),
            verbs);
        var composition = new PlaceableCompositionEventsProfile(
            originalComposition?.Id ?? StablePlaceableId(current.BlueprintId, "ux1-aa-composition"),
            originalComposition?.SchemaVersion ?? WorldFeatureCatalog.CurrentSchemaVersion,
            originalComposition?.Provenance ?? "studio:ux1-aa-placeable-assembly-validation",
            damageProfile,
            sockets,
            attachments,
            multiVerb);
        var foundation = sourceFoundation with { Validation = validation };
        var updatedProfile = current with { Foundation = foundation, CompositionEvents = composition };
        profile = updatedProfile;
        var placeables = catalog.Placeables
            .Select(candidate => candidate.Id == current.Id ? updatedProfile : candidate)
            .ToArray();
        var candidateCatalog = new WorldFeatureCatalog(
            catalog.Id,
            catalog.SchemaVersion,
            catalog.Provenance,
            placeables,
            catalog.Doors,
            catalog.Triggers,
            catalog.Encounters,
            catalog.VehicleRoutes,
            catalog.Merchants,
            catalog.Companions,
            catalog.Scripts);
        command = new SetWorldFeatureCatalogCommand(
            CommandId.From(Guid.NewGuid()),
            history.State.Revision,
            candidateCatalog,
            dryRun);
        return true;
    }

    private static bool Ux1AAValuesEqual(PlaceableWorldProfile left, PlaceableWorldProfile right)
    {
        if (left.CompositionEvents is not { } leftComposition ||
            right.CompositionEvents is not { } rightComposition ||
            left.Foundation?.Validation is not { } leftValidation ||
            right.Foundation?.Validation is not { } rightValidation)
        {
            return false;
        }
        return leftComposition.Id == rightComposition.Id &&
               leftComposition.SchemaVersion == rightComposition.SchemaVersion &&
               string.Equals(leftComposition.Provenance, rightComposition.Provenance, StringComparison.Ordinal) &&
               leftComposition.DamageEvents == rightComposition.DamageEvents &&
               Ux1AASocketsEqual(leftComposition.Sockets, rightComposition.Sockets) &&
               Ux1AAAttachmentsEqual(leftComposition.Attachments, rightComposition.Attachments) &&
               Ux1AAVerbsEqual(leftComposition.MultiVerb.Verbs, rightComposition.MultiVerb.Verbs) &&
               Ux1AAValidationEqual(leftValidation, rightValidation);
    }

    private static bool Ux1AASocketsEqual(
        IReadOnlyList<PlaceableAttachmentSocketProfile> left,
        IReadOnlyList<PlaceableAttachmentSocketProfile> right) =>
        left.Count == right.Count && left.Zip(right).All(pair =>
            pair.First.Id == pair.Second.Id &&
            string.Equals(pair.First.SocketId, pair.Second.SocketId, StringComparison.Ordinal) &&
            string.Equals(pair.First.Kind, pair.Second.Kind, StringComparison.Ordinal) &&
            pair.First.LocalTransform == pair.Second.LocalTransform &&
            pair.First.AllowedAttachmentTags.SequenceEqual(
                pair.Second.AllowedAttachmentTags, StringComparer.Ordinal) &&
            pair.First.SnapEnabled == pair.Second.SnapEnabled &&
            pair.First.MaximumAttachments == pair.Second.MaximumAttachments);

    private static bool Ux1AAAttachmentsEqual(
        IReadOnlyList<PlaceableAuthoredAttachmentProfile> left,
        IReadOnlyList<PlaceableAuthoredAttachmentProfile> right) =>
        left.Count == right.Count && left.Zip(right).All(pair =>
            pair.First.Id == pair.Second.Id &&
            string.Equals(pair.First.SocketId, pair.Second.SocketId, StringComparison.Ordinal) &&
            pair.First.ChildBlueprintId == pair.Second.ChildBlueprintId &&
            pair.First.Tags.SequenceEqual(pair.Second.Tags, StringComparer.Ordinal) &&
            pair.First.Transform == pair.Second.Transform &&
            string.Equals(pair.First.PrefabId, pair.Second.PrefabId, StringComparison.Ordinal));

    private static bool Ux1AAVerbsEqual(
        IReadOnlyList<PlaceableContextualVerbProfile> left,
        IReadOnlyList<PlaceableContextualVerbProfile> right) =>
        left.Count == right.Count && left.Zip(right).All(pair =>
            pair.First.Id == pair.Second.Id &&
            string.Equals(pair.First.VerbId, pair.Second.VerbId, StringComparison.Ordinal) &&
            string.Equals(pair.First.LabelKey, pair.Second.LabelKey, StringComparison.Ordinal) &&
            pair.First.BasePriority == pair.Second.BasePriority &&
            pair.First.RequiredContext.SequenceEqual(pair.Second.RequiredContext, StringComparer.Ordinal) &&
            pair.First.ForbiddenContext.SequenceEqual(pair.Second.ForbiddenContext, StringComparer.Ordinal) &&
            string.Equals(pair.First.EventBinding, pair.Second.EventBinding, StringComparison.Ordinal));

    private static bool Ux1AAVerbsEqual(
        IReadOnlyList<PlaceableContextualVerbProfile> persisted,
        IReadOnlyList<StudioUx1AAVerbDraft> expected) =>
        persisted.Count == expected.Count && persisted.Zip(expected).All(pair =>
            pair.First.Id == pair.Second.Id &&
            string.Equals(pair.First.VerbId, pair.Second.VerbId.Trim(), StringComparison.Ordinal) &&
            string.Equals(pair.First.LabelKey, pair.Second.LabelKey.Trim(), StringComparison.Ordinal) &&
            pair.First.BasePriority == pair.Second.BasePriority &&
            pair.First.RequiredContext.SequenceEqual(
                SplitUx1AAValues(pair.Second.RequiredContextText), StringComparer.Ordinal) &&
            pair.First.ForbiddenContext.SequenceEqual(
                SplitUx1AAValues(pair.Second.ForbiddenContextText), StringComparer.Ordinal) &&
            string.Equals(pair.First.EventBinding, pair.Second.EventBinding.Trim(), StringComparison.Ordinal));

    private static bool Ux1AAValidationEqual(
        PlaceableWorldProfile.RuntimeValidationPolicy left,
        PlaceableWorldProfile.RuntimeValidationPolicy right) =>
        left.AnimationIds.SequenceEqual(right.AnimationIds, StringComparer.Ordinal) &&
        left.LockIds.SequenceEqual(right.LockIds, StringComparer.Ordinal) &&
        left.TrapIds.SequenceEqual(right.TrapIds, StringComparer.Ordinal) &&
        left.InventoryIds.SequenceEqual(right.InventoryIds, StringComparer.Ordinal) &&
        left.PhysicsProfileIds.SequenceEqual(right.PhysicsProfileIds, StringComparer.Ordinal) &&
        left.DrawCallBudget == right.DrawCallBudget && left.ScriptBudget == right.ScriptBudget;

    private static StudioUx1AASmokeResult FailedUx1AASmoke(
        string? stage = null,
        IEnumerable<string>? diagnostics = null)
    {
        if (stage is not null)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX1AA_{stage}_FAILED diagnostics=" +
                              string.Join(" | ", diagnostics ?? []));
        }
        return new(
            Succeeded: false,
            ExplicitSelection: false,
            PrerequisitesRefused: false,
            AssemblyPreview: false,
            MultiVerbPreview: false,
            ValidationPreview: false,
            DryRun: false,
            CommandParity: false,
            InvalidSocketBlocked: false,
            InvalidAttachmentBlocked: false,
            InvalidVerbBlocked: false,
            InvalidValidationBlocked: false,
            StalePreviewBlocked: false,
            DamageAuthorityPreserved: false,
            NeighborCapabilitiesPreserved: false,
            NeighborPlaceablePreserved: false,
            UndoRedo: false,
            Persistence: false,
            HumanDiagnostics: false);
    }

    private static Ux1AAAvailability DescribeUx1AAAvailability(PlaceableWorldProfile? profile)
    {
        if (profile is null)
        {
            return new(false,
                "Aucun profil canonique n’existe pour ce blueprint. UX1-AA ne synthétise pas ses prérequis.",
                "world.placeable.profile_missing");
        }
        if (profile.Foundation is null)
        {
            return new(false,
                "La fondation canonique manque. Configurez d’abord l’identité, l’apparence et la collision du placeable.",
                "world.placeable.composition_events_foundation_missing");
        }
        if (!profile.Foundation.Collision.Enabled)
        {
            return new(false,
                "La collision canonique doit être active avant de créer des sockets.",
                "world.placeable.composition_events_collision_missing");
        }
        var collision = profile.Foundation.Collision;
        if (string.IsNullOrWhiteSpace(collision.Shape) || collision.Size.X <= 0 || collision.Size.Y <= 0 ||
            collision.Size.Z <= 0 || string.IsNullOrWhiteSpace(collision.PhysicsLayer))
        {
            return new(false,
                "La collision canonique exige une forme, des dimensions positives et une couche physique avant l’assemblage.",
                "world.placeable.collision_invalid");
        }
        if (profile.Operations?.Damageable is null)
        {
            return new(false,
                "Le profil de destructibilité canonique manque. Configurez-le avant l’assemblage.",
                "world.placeable.damage_events_damageable_missing");
        }
        var damage = PlaceableDamageEventsResolver.Resolve(profile);
        if (damage.DiagnosticCode is not null)
        {
            return new(false,
                "Les autorités de dégâts directe et composée divergent. Réconciliez-les avant l’assemblage.",
                damage.DiagnosticCode);
        }
        if (!damage.Available || damage.Profile is null)
        {
            return new(false,
                "Les événements de dégâts canoniques manquent. UX1-AA ne les crée pas implicitement.",
                "world.placeable.damage_events_required_missing");
        }
        return new(true, "Prérequis canoniques disponibles.", string.Empty);
    }

    private static StudioUx1AATransformDraft DefaultUx1AATransform() => new(
        Vector3Value.Zero,
        Vector3Value.Zero,
        Vector3Value.One,
        Vector3Value.Zero,
        .25,
        null,
        false,
        string.Empty);

    private static StudioUx1AATransformDraft ToUx1AATransform(
        PlaceableWorldProfile.TransformPolicy transform) => new(
        transform.Position,
        transform.RotationDegrees,
        transform.Scale,
        transform.Pivot,
        transform.SnapIncrement,
        transform.ParentId,
        transform.AlignToSurface,
        transform.TransformAnimationId ?? string.Empty);

    private static PlaceableWorldProfile.TransformPolicy ToUx1AATransform(
        StudioUx1AATransformDraft transform) => new(
        transform.Position,
        transform.RotationDegrees,
        transform.Scale,
        transform.Pivot,
        transform.SnapIncrement,
        transform.ParentId,
        transform.AlignToSurface,
        string.IsNullOrWhiteSpace(transform.TransformAnimationId)
            ? null
            : transform.TransformAnimationId.Trim());

    private static bool ValidUx1AATransform(StudioUx1AATransformDraft transform)
    {
        var values = new[]
        {
            transform.Position.X, transform.Position.Y, transform.Position.Z,
            transform.RotationDegrees.X, transform.RotationDegrees.Y, transform.RotationDegrees.Z,
            transform.Scale.X, transform.Scale.Y, transform.Scale.Z,
            transform.Pivot.X, transform.Pivot.Y, transform.Pivot.Z,
        };
        return values.All(float.IsFinite) &&
               new[]
               {
                   transform.Position.X, transform.Position.Y, transform.Position.Z,
                   transform.Pivot.X, transform.Pivot.Y, transform.Pivot.Z,
               }.All(value => Math.Abs(value) <= 1_000_000) &&
               transform.Scale.X is > .01f and <= 100 &&
               transform.Scale.Y is > .01f and <= 100 &&
               transform.Scale.Z is > .01f and <= 100 &&
               double.IsFinite(transform.SnapIncrement) && transform.SnapIncrement is >= 0 and <= 10000 &&
               transform.TransformAnimationId.Trim().Length <= 256;
    }

    private static IReadOnlyList<StudioUx1AAVerbDraft> DefaultUx1AAVerbs(BlueprintId blueprintId) =>
    [
        DefaultUx1AAVerb(blueprintId, "push", "placeable.verb.push", 90, "", "anchored", "PushRequested"),
        DefaultUx1AAVerb(blueprintId, "take", "placeable.verb.take", 80, "portable", "", "TakeRequested"),
        DefaultUx1AAVerb(blueprintId, "custom-action", "placeable.verb.custom-action", 70,
            "custom-context", "", "CustomActionRequested"),
        DefaultUx1AAVerb(blueprintId, "inspect", "placeable.verb.inspect", 60, "", "", "InspectRequested"),
        DefaultUx1AAVerb(blueprintId, "use", "placeable.verb.use", 50, "", "", "UseRequested"),
        DefaultUx1AAVerb(blueprintId, "open", "placeable.verb.open", 40, "unlocked", "", "OpenRequested"),
        DefaultUx1AAVerb(blueprintId, "talk", "placeable.verb.talk", 30,
            "interlocutor", "", "TalkRequested"),
        DefaultUx1AAVerb(blueprintId, "climb", "placeable.verb.climb", 20,
            "climbable", "", "ClimbRequested"),
    ];

    private static StudioUx1AAVerbDraft DefaultUx1AAVerb(
        BlueprintId blueprintId,
        string verbId,
        string labelKey,
        int priority,
        string required,
        string forbidden,
        string eventBinding) => new(
        StablePlaceableId(blueprintId, $"ux1-aa-verb-{verbId}"),
        verbId,
        labelKey,
        priority,
        required,
        forbidden,
        eventBinding);

    private static StudioUx1AAVerbDraft ToUx1AAVerb(PlaceableContextualVerbProfile verb) => new(
        verb.Id,
        verb.VerbId,
        verb.LabelKey,
        verb.BasePriority,
        string.Join(", ", verb.RequiredContext),
        string.Join(", ", verb.ForbiddenContext),
        verb.EventBinding);

    private static bool TryBuildUx1AAVerbs(
        IReadOnlyList<StudioUx1AAVerbDraft> drafts,
        out IReadOnlyList<PlaceableContextualVerbProfile> verbs,
        out string message)
    {
        var built = new List<PlaceableContextualVerbProfile>();
        foreach (var draft in drafts)
        {
            if (draft.Id == Guid.Empty || string.IsNullOrWhiteSpace(draft.VerbId) ||
                string.IsNullOrWhiteSpace(draft.LabelKey) || string.IsNullOrWhiteSpace(draft.EventBinding))
            {
                verbs = [];
                message = "Chaque action exige un nom, un libellé et un événement de gameplay.";
                return false;
            }
            if (!TryParseUx1AAValues(draft.RequiredContextText, "contexte requis", false,
                    out var required, out message) ||
                !TryParseUx1AAValues(draft.ForbiddenContextText, "contexte interdit", false,
                    out var forbidden, out message))
            {
                verbs = [];
                return false;
            }
            if (required.Intersect(forbidden, StringComparer.Ordinal).Any())
            {
                verbs = [];
                message = "Une action ne peut pas exiger et interdire le même contexte.";
                return false;
            }
            built.Add(new(
                draft.Id,
                draft.VerbId.Trim(),
                draft.LabelKey.Trim(),
                draft.BasePriority,
                required,
                forbidden,
                draft.EventBinding.Trim()));
        }
        var verbIds = built.Select(verb => verb.VerbId).ToArray();
        var standardReady = Ux1AAStandardVerbs.All(required =>
            verbIds.Contains(required, StringComparer.OrdinalIgnoreCase));
        var customReady = verbIds.Any(candidate =>
            !Ux1AAStandardVerbs.Contains(candidate, StringComparer.OrdinalIgnoreCase));
        if (built.Count < 8 || !standardReady || !customReady ||
            verbIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != verbIds.Length ||
            built.Select(verb => verb.Id).Distinct().Count() != built.Count)
        {
            verbs = [];
            message = "Conservez Inspect, Use, Open, Talk, Take, Push, Climb et au moins une action personnalisée unique.";
            return false;
        }
        verbs = built;
        message = string.Empty;
        return true;
    }

    private static bool TryBuildUx1AAValidation(
        PlaceableWorldProfile.FoundationPolicy foundation,
        StudioUx1AAValidationDraft draft,
        out PlaceableWorldProfile.RuntimeValidationPolicy validation,
        out string message,
        out string diagnosticCode)
    {
        validation = new([], [], [], [], [], 0, 0);
        diagnosticCode = string.Empty;
        if (!TryParseUx1AAValues(draft.AnimationIdsText, "animation", false, out var animations, out message) ||
            !TryParseUx1AAValues(draft.LockIdsText, "référence de verrou", false, out var locks, out message) ||
            !TryParseUx1AAValues(draft.TrapIdsText, "référence de piège", false, out var traps, out message) ||
            !TryParseUx1AAValues(draft.InventoryIdsText, "référence d’inventaire", false,
                out var inventories, out message) ||
            !TryParseUx1AAValues(draft.PhysicsProfileIdsText, "profil physique", false,
                out var physics, out message))
        {
            diagnosticCode = "world.placeable.validation_reference_invalid";
            return false;
        }
        if (foundation.Transform.TransformAnimationId is { } animation &&
            !animations.Contains(animation, StringComparer.Ordinal))
        {
            message = "Ajoutez l’animation de transformation active à la liste des animations validées.";
            diagnosticCode = "world.placeable.animation_invalid";
            return false;
        }
        if (foundation.Components.HasLock && locks.Count == 0)
        {
            message = "Le composant verrou exige au moins une référence de verrou.";
            diagnosticCode = "world.placeable.lock_reference_missing";
            return false;
        }
        if (foundation.Components.HasTrap && traps.Count == 0)
        {
            message = "Le composant piège exige au moins une référence de piège.";
            diagnosticCode = "world.placeable.trap_reference_missing";
            return false;
        }
        if (foundation.Components.HasContainer && inventories.Count == 0)
        {
            message = "Le composant conteneur exige au moins une référence d’inventaire.";
            diagnosticCode = "world.placeable.inventory_reference_missing";
            return false;
        }
        if (foundation.Components.HasPhysics && physics.Count == 0)
        {
            message = "Le composant physique exige au moins un profil physique.";
            diagnosticCode = "world.placeable.physics_profile_missing";
            return false;
        }
        if (draft.DrawCallBudget < Math.Max(1, foundation.Visual.MeshIds.Count) ||
            draft.ScriptBudget < foundation.Components.ScriptBindings.Count)
        {
            message = "Augmentez les budgets pour couvrir les meshes et scripts canoniques du placeable.";
            diagnosticCode = "world.placeable.budget_exceeded";
            return false;
        }
        validation = new(
            animations,
            locks,
            traps,
            inventories,
            physics,
            draft.DrawCallBudget,
            draft.ScriptBudget);
        message = string.Empty;
        return true;
    }

    private static bool TryParseUx1AAValues(
        string text,
        string noun,
        bool required,
        out IReadOnlyList<string> values,
        out string message)
    {
        var parsed = text.Split([',', ';', '\n', '\r'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
        if (required && parsed.Length == 0)
        {
            values = [];
            message = $"Ajoutez au moins un {noun}.";
            return false;
        }
        if (parsed.Any(string.IsNullOrWhiteSpace) ||
            parsed.Distinct(StringComparer.Ordinal).Count() != parsed.Length)
        {
            values = [];
            message = $"Chaque {noun} doit être non vide et unique.";
            return false;
        }
        values = parsed;
        message = string.Empty;
        return true;
    }

    private static IReadOnlyList<string> SplitUx1AAValues(string text) => text
        .Split([',', ';', '\n', '\r'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    private static string HumanUx1AAPatch(string patch) => patch.StartsWith("attach:", StringComparison.Ordinal)
        ? "ajouter sur le socket"
        : patch.StartsWith("detach:", StringComparison.Ordinal)
            ? "retirer du socket"
            : "opération réversible";

    private static string HumanizeUx1AAVerbLabel(string label)
    {
        var token = label.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? label;
        return token.Replace('-', ' ').Replace('_', ' ');
    }

    private static string HumanizeUx1AADiagnostic(string? code) => code switch
    {
        "world.placeable.composition_events_foundation_missing" =>
            "La fondation canonique manque. Configurez d’abord l’identité, l’apparence et la collision.",
        "world.placeable.composition_events_collision_missing" =>
            "Activez une collision canonique avant de configurer les sockets.",
        "world.placeable.collision_invalid" =>
            "Corrigez la forme, les dimensions et la couche physique de la collision avant de configurer les sockets.",
        "world.placeable.damage_events_damageable_missing" =>
            "Configurez la destructibilité canonique avant l’assemblage.",
        "world.placeable.damage_events_required_missing" =>
            "Configurez les événements de dégâts canoniques avant l’assemblage.",
        "WORLD_PLACEABLE_DAMAGE_EVENTS_AUTHORITY_DIVERGED" or
            "world.placeable.damage_events_authority_diverged" =>
            "Les autorités de dégâts directe et composée divergent. Réconciliez-les avant de continuer.",
        "world.placeable.attachment_sockets_invalid" =>
            "Le socket exige une identité, un type, des tags, une capacité et une transformation valides.",
        "world.placeable.attachment_invalid" =>
            "L’assemblage exige un socket compatible, un blueprint enfant résolu, des tags et une transformation valide.",
        "world.placeable.attachment_socket_capacity_exceeded" =>
            "Augmentez la capacité du socket ou retirez un assemblage.",
        "world.placeable.multi_verb_invalid" =>
            "Les sept actions standard et une action personnalisée doivent rester uniques et contextualisées.",
        "world.placeable.animation_invalid" =>
            "L’animation de transformation doit être déclarée dans la validation.",
        "world.placeable.animation_reference_missing" =>
            "Ajoutez l’animation de transformation active aux animations validées.",
        "world.placeable.lock_reference_missing" =>
            "Le composant verrou exige une référence de verrou.",
        "world.placeable.trap_reference_missing" =>
            "Le composant piège exige une référence de piège.",
        "world.placeable.inventory_reference_missing" =>
            "Le composant conteneur exige une référence d’inventaire.",
        "world.placeable.physics_profile_missing" =>
            "Le composant physique exige un profil physique.",
        "world.placeable.budget_exceeded" =>
            "Les budgets doivent couvrir les meshes et scripts canoniques.",
        "world.placeable.validation_reference_invalid" =>
            "Chaque référence de validation doit être non vide et unique.",
        "world.placeable.scripts_invalid" =>
            "Corrigez les événements et routes de script canoniques avant de valider ce placeable.",
        null or "" => "La prévisualisation n’a pas pu être validée.",
        _ => "La configuration contient une erreur localisée. Corrigez la section signalée puis réessayez.",
    };

    private static bool IsHumanUx1AAMessage(string message) =>
        !string.IsNullOrWhiteSpace(message) &&
        !message.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains(".json", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains('\\');

    private static StudioUx1AAPathPreview FailedUx1AAPath(string message, string code = "") =>
        new(false, message, code);

    private static StudioUx1AAPreview FailedUx1AAPreview(string message, string code) => new(
        false,
        message,
        message,
        false,
        FailedUx1AAPath(message, code),
        FailedUx1AAPath(message, code),
        FailedUx1AAPath(message, code));

    private sealed record Ux1AAAvailability(bool Ready, string Message, string Code);
}
