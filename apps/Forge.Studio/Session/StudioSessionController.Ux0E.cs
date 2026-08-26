using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly NewModuleHandler _newModule = new(new SystemClock());
    private readonly SetModuleConfigurationHandler _setModuleConfiguration = new(new SystemClock());
    private readonly PatchModuleMetadataHandler _patchModuleMetadataUx0E = new(new SystemClock());

    internal StudioProjectPreview PreviewNewProject(StudioNewProjectDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!TryBuildNewModuleCommand(draft, dryRun: true, out var command, out var message))
        {
            return new StudioProjectPreview(false, message, string.Empty);
        }

        var result = _newModule.Handle(command!);
        if (!result.Succeeded || result.State is null)
        {
            return new StudioProjectPreview(false, Humanize(result.Diagnostics.FirstOrDefault()?.Code), string.Empty);
        }

        var project = result.State.Project;
        return new StudioProjectPreview(
            true,
            "Aperçu validé · aucun fichier créé",
            BuildNewProjectSummary(project),
            project);
    }

    internal StudioProjectCreationResult CreateNewProject(StudioNewProjectDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!TryBuildNewModuleCommand(draft, dryRun: false, out var command, out var message))
        {
            return new StudioProjectCreationResult(false, message);
        }

        var preview = _newModule.Handle(command! with { DryRun = true });
        if (!preview.Succeeded || preview.State is null)
        {
            return new StudioProjectCreationResult(false, Humanize(preview.Diagnostics.FirstOrDefault()?.Code));
        }
        var applied = _newModule.Handle(command!);
        if (!applied.Succeeded || applied.State is null)
        {
            return new StudioProjectCreationResult(false, Humanize(applied.Diagnostics.FirstOrDefault()?.Code));
        }

        string parentDirectory;
        string workspaceDirectory;
        try
        {
            parentDirectory = Path.GetFullPath(draft.ParentDirectory);
            workspaceDirectory = Path.Combine(parentDirectory, applied.State.Project.Module.Slug);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return new StudioProjectCreationResult(false, $"Le dossier choisi n’est pas valide : {exception.Message}");
        }

        var created = ProjectSession.Create(workspaceDirectory, applied.State.Project);
        if (!created.Succeeded || created.Session is null)
        {
            return new StudioProjectCreationResult(
                false,
                HumanizeWorkspace(created.Diagnostics.FirstOrDefault()?.Code));
        }

        var reopened = ProjectSession.Open(workspaceDirectory);
        if (!reopened.Succeeded || reopened.Session is null ||
            reopened.Session.Project.Id != applied.State.Project.Id ||
            reopened.Session.Project.Module.Metadata.Foundation?.Identity !=
            applied.State.Project.Module.Metadata.Foundation?.Identity)
        {
            return new StudioProjectCreationResult(false, "Le projet a été écrit mais sa vérification immédiate a échoué.");
        }

        return new StudioProjectCreationResult(
            true,
            "Projet créé et vérifié · ouverture dans le Studio",
            workspaceDirectory,
            reopened.Session.Project);
    }

    internal StudioProjectConfigurationDraft CaptureProjectConfiguration()
    {
        var project = Project ?? throw new InvalidOperationException("No project is open.");
        var metadata = project.Module.Metadata;
        var foundation = metadata.Foundation ?? CreateMigrationFoundation(project);
        var start = foundation.PlayerStarts.FirstOrDefault(item => item.PlayerSelectionRule == "default") ??
                    foundation.PlayerStarts.FirstOrDefault();
        return new StudioProjectConfigurationDraft(
            metadata.DefaultLocale,
            metadata.Localizations.Select(StudioLocalizedProjectDraft.From).ToArray(),
            metadata.Author,
            metadata.Version,
            metadata.License,
            metadata.ImagePath,
            metadata.Tags,
            metadata.Classification,
            foundation.Kits,
            foundation.TargetPlatforms,
            foundation.RulesProvider,
            foundation.RenderPreset,
            foundation.NetworkMode,
            start?.AreaId,
            start?.SpawnEntityId,
            start?.WaypointId,
            start?.OrientationDegrees ?? 0,
            start?.GameMode ?? "campaign",
            start?.EntryClass ?? "adventurer",
            start?.PlayerSelectionRule ?? "default");
    }

    internal StudioProjectPreview PreviewProjectConfiguration(StudioProjectConfigurationDraft draft) =>
        MutateProjectConfiguration(draft, apply: false);

    internal StudioProjectPreview ApplyProjectConfiguration(StudioProjectConfigurationDraft draft) =>
        MutateProjectConfiguration(draft, apply: true);

    internal StudioUx0ESmokeResult RunUx0EContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux0e-" + Guid.NewGuid().ToString("N"));
        var previewParent = Path.Combine(tempRoot, "preview-does-not-write");
        try
        {
            var rpgDraft = CreateSmokeDraft(previewParent, ProjectTemplateKind.Rpg, createArea: true);
            var preview = PreviewNewProject(rpgDraft);
            var repeated = PreviewNewProject(rpgDraft);
            var wizardPreview = preview.Succeeded && repeated.Succeeded &&
                                !Directory.Exists(previewParent) && preview.Project is not null;
            var stableIdentity = preview.Project?.Id == repeated.Project?.Id &&
                                 preview.Project?.Module.Metadata.Foundation?.Identity ==
                                 repeated.Project?.Module.Metadata.Foundation?.Identity;
            var localizedMetadata = preview.Project?.Module.Metadata.Localizations.Count == 2 &&
                                    preview.Project.Module.Metadata.GetDefaultLocalization().Summary ==
                                    "Preuve de création humaine";
            var start = preview.Project?.Module.Metadata.Foundation?.PlayerStarts.SingleOrDefault();
            var spawnBlueprint = start is null
                ? null
                : preview.Project?.Areas.SingleOrDefault(area => area.Id == start.AreaId)?.Entities
                    .Where(entity => entity.Id == start.SpawnEntityId)
                    .Select(entity => preview.Project.Blueprints.SingleOrDefault(blueprint => blueprint.Id == entity.BlueprintId))
                    .SingleOrDefault();
            var optionalWaypoint = start?.WaypointId is null;
            var playerStart = start?.OrientationDegrees == 90 && spawnBlueprint?.Kind == BlueprintKind.PlayerSpawn &&
                              optionalWaypoint;

            var invalid = PreviewNewProject(rpgDraft with { AreaDisplayName = string.Empty });
            var invalidRpgBlocked = !invalid.Succeeded;
            var humanDiagnostics = invalidRpgBlocked &&
                                   !invalid.Message.Contains("project.", StringComparison.OrdinalIgnoreCase) &&
                                   !invalid.Message.Contains("module/", StringComparison.OrdinalIgnoreCase) &&
                                   !Guid.TryParse(invalid.Message, out _);

            var coreDraft = CreateSmokeDraft(tempRoot, ProjectTemplateKind.CoreMenu, createArea: false) with
            {
                DraftId = Guid.Parse("d0250000-0000-4000-8000-000000000100"),
                Localizations =
                [
                    new StudioLocalizedProjectDraft(
                        "fr", "Studio Core UX0-E", "Interface autonome", "Projet sans zone.", "Atelier Forge",
                        string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, "OpenRPG Forge 1.x"),
                    new StudioLocalizedProjectDraft(
                        "en", "Studio Core UX0-E", "Standalone interface", "A project without an area.", "Forge Workshop",
                        string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, "OpenRPG Forge 1.x"),
                ],
                DefaultLocale = "fr",
            };
            var created = CreateNewProject(coreDraft);
            var reopened = new StudioSessionController();
            var openResult = created.WorkspaceDirectory is null
                ? new StudioOpenResult(false, [])
                : reopened.Open(created.WorkspaceDirectory);
            var coreWithoutArea = created.Succeeded && openResult.Succeeded &&
                                  reopened.Project?.Areas.Count == 0 && reopened.ActiveAreaId is null;
            var corePersistence = coreWithoutArea && reopened.Project?.Module.Metadata.Localizations.Count == 2 &&
                                  reopened.Project.Module.Metadata.Foundation?.Template == ProjectTemplateKind.CoreMenu;
            reopened.Close();

            var persistedRpg = CreateNewProject(rpgDraft with
            {
                ParentDirectory = Path.Combine(tempRoot, "rpg-persistence"),
            });
            var persistedController = new StudioSessionController();
            var persistedOpen = persistedRpg.WorkspaceDirectory is not null &&
                                persistedController.Open(persistedRpg.WorkspaceDirectory).Succeeded;
            var rpgPersistence = false;
            if (persistedOpen)
            {
                var persistedIdentity = persistedController.Project!.Module.Metadata.Foundation!.Identity;
                var persistedDraft = persistedController.CaptureProjectConfiguration() with
                {
                    Author = "Atelier Forge · persisté",
                    PlayerOrientationDegrees = 120,
                };
                var persistedApply = persistedController.ApplyProjectConfiguration(persistedDraft);
                var persistedSave = persistedController.Save();
                persistedController.Close();
                var verified = new StudioSessionController();
                var verifiedOpen = verified.Open(persistedRpg.WorkspaceDirectory!).Succeeded;
                rpgPersistence = persistedApply.Succeeded && persistedSave.Succeeded && verifiedOpen &&
                                 verified.Project?.Module.Metadata.Author == "Atelier Forge · persisté" &&
                                 verified.Project.Module.Metadata.Localizations.Count == 2 &&
                                 verified.Project.Module.Metadata.Foundation?.Identity == persistedIdentity &&
                                 verified.Project.Module.Metadata.Foundation?.PlayerStarts.Single().OrientationDegrees == 120;
                verified.Close();
            }
            else
            {
                persistedController.Close();
            }
            var persistenceRoundTrip = corePersistence && rpgPersistence;

            var editing = new StudioSessionController();
            var editingOpen = Session is not null && editing.Open(Session.RootDirectory, ActiveAreaId?.ToString()).Succeeded;
            var atomicUndoRedo = false;
            if (editingOpen)
            {
                var original = editing.Project;
                var originalIdentity = original?.Module.Metadata.Foundation?.Identity;
                var configuration = editing.CaptureProjectConfiguration();
                var candidate = configuration with
                {
                    Author = configuration.Author + " · UX0-E",
                    PlayerOrientationDegrees = (configuration.PlayerOrientationDegrees + 15) % 360,
                };
                var configurationPreview = editing.PreviewProjectConfiguration(candidate);
                var applied = editing.ApplyProjectConfiguration(candidate);
                var appliedIdentity = editing.Project?.Module.Metadata.Foundation?.Identity;
                var identityPreserved = originalIdentity is null
                    ? appliedIdentity?.ProjectId == original?.Id &&
                      string.Equals(appliedIdentity?.Slug, original?.Module.Slug, StringComparison.Ordinal)
                    : appliedIdentity == originalIdentity;
                var undo = editing.Undo();
                var undone = ReferenceEquals(editing.Project, original);
                var redo = editing.Redo();
                var redone = editing.Project?.Module.Metadata.Author.EndsWith("UX0-E", StringComparison.Ordinal) == true;
                var restored = editing.Undo();
                atomicUndoRedo = configurationPreview.Succeeded && applied.Succeeded && identityPreserved &&
                                 undo.Succeeded && undone && redo.Succeeded && redone && restored.Succeeded &&
                                 ReferenceEquals(editing.Project, original);
                editing.Close();
            }

            var succeeded = wizardPreview && invalidRpgBlocked && coreWithoutArea && localizedMetadata &&
                            stableIdentity && playerStart && atomicUndoRedo && persistenceRoundTrip && humanDiagnostics;
            return new StudioUx0ESmokeResult(
                succeeded,
                wizardPreview,
                invalidRpgBlocked,
                coreWithoutArea,
                localizedMetadata,
                stableIdentity,
                playerStart,
                optionalWaypoint,
                atomicUndoRedo,
                persistenceRoundTrip,
                humanDiagnostics);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private StudioProjectPreview MutateProjectConfiguration(StudioProjectConfigurationDraft draft, bool apply)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (History is null || Project is null)
        {
            return new StudioProjectPreview(false, "Aucun projet n’est ouvert.", string.Empty);
        }

        ProjectMetadata metadata;
        try
        {
            var current = Project.Module.Metadata;
            var foundation = BuildFoundation(Project, current.Foundation ?? CreateMigrationFoundation(Project), draft);
            metadata = new ProjectMetadata(
                current.Id,
                draft.DefaultLocale,
                draft.Localizations.Select(item => item.ToDomain()).ToArray(),
                draft.Author,
                draft.Version,
                draft.License,
                draft.ImagePath,
                draft.Tags,
                draft.Classification,
                "studio:ux0-e-project",
                current.SchemaVersion,
                foundation);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return new StudioProjectPreview(false, HumanizeException(exception), string.Empty);
        }

        var currentMetadata = Project.Module.Metadata;
        var metadataPatch = ModuleMetadataPatch.Between(currentMetadata, metadata);
        var foundationPatch = currentMetadata.Foundation == metadata.Foundation ? null : metadata.Foundation;
        if (metadataPatch.Fields.Count == 0 && (metadataPatch.Localizations?.Count ?? 0) == 0 &&
            foundationPatch is null)
        {
            return new StudioProjectPreview(true, "Aucune modification à appliquer",
                $"{metadata.Localizations.Count} langue(s) · métadonnées déjà à jour");
        }
        var command = new PatchModuleMetadataCommand(
            CommandId.From(Guid.NewGuid()),
            History.State.Revision,
            metadataPatch,
            foundationPatch,
            DryRun: !apply);
        var preview = _patchModuleMetadataUx0E.Handle(History.State, command with { DryRun = true });
        if (!preview.Succeeded)
        {
            return new StudioProjectPreview(false, Humanize(preview.Diagnostics.FirstOrDefault()?.Code), string.Empty);
        }
        if (!apply)
        {
            return new StudioProjectPreview(
                true,
                "Aperçu validé · le projet n’a pas été modifié",
                $"{metadata.Localizations.Count} langue(s) · {metadata.Foundation?.TargetPlatforms.Count ?? 0} plateforme(s)");
        }

        var applied = _patchModuleMetadataUx0E.Handle(History.State, command with { DryRun = false });
        if (!applied.Succeeded)
        {
            return new StudioProjectPreview(false, Humanize(applied.Diagnostics.FirstOrDefault()?.Code), string.Empty);
        }
        History.Record(applied);
        return new StudioProjectPreview(
            true,
            "Configuration appliquée · Ctrl+Z pour annuler · Ctrl+S pour enregistrer",
            $"{metadata.Localizations.Count} langue(s) · identité technique préservée",
            applied.State.Project);
    }

    private static ProjectFoundationProfile BuildFoundation(
        ProjectDefinition project,
        ProjectFoundationProfile foundation,
        StudioProjectConfigurationDraft draft)
    {
        var starts = foundation.PlayerStarts.ToList();
        var primaryIndex = starts.FindIndex(item => item.PlayerSelectionRule == "default");
        if (primaryIndex < 0 && starts.Count > 0)
        {
            primaryIndex = 0;
        }
        if (foundation.Template == ProjectTemplateKind.Rpg)
        {
            if (draft.PlayerStartAreaId is null || draft.PlayerStartEntityId is null)
            {
                throw new InvalidOperationException("Choisissez une zone et un point de départ pour le joueur.");
            }
            var updated = primaryIndex >= 0
                ? starts[primaryIndex] with
                {
                    AreaId = draft.PlayerStartAreaId,
                    SpawnEntityId = draft.PlayerStartEntityId,
                    WaypointId = draft.PlayerStartWaypointId,
                    OrientationDegrees = draft.PlayerOrientationDegrees,
                    GameMode = draft.GameMode,
                    EntryClass = draft.EntryClass,
                    PlayerSelectionRule = draft.PlayerSelectionRule,
                    Provenance = "studio:ux0-e-project",
                }
                : new PlayerStartProfile(
                    StableGuid(foundation.Id, "player-start:studio-default"),
                    PlayerStartProfile.CurrentSchemaVersion,
                    "studio:ux0-e-project",
                    "default",
                    draft.PlayerStartAreaId,
                    draft.PlayerStartEntityId,
                    draft.PlayerStartWaypointId,
                    draft.PlayerOrientationDegrees,
                    draft.GameMode,
                    draft.EntryClass,
                    draft.PlayerSelectionRule,
                    100,
                    true);
            if (primaryIndex >= 0)
            {
                starts[primaryIndex] = updated;
            }
            else
            {
                starts.Add(updated);
            }
        }

        return foundation with
        {
            Provenance = "studio:ux0-e-project",
            Kits = draft.Kits.ToArray(),
            TargetPlatforms = draft.TargetPlatforms.ToArray(),
            RulesProvider = draft.RulesProvider,
            RenderPreset = draft.RenderPreset,
            NetworkMode = draft.NetworkMode,
            PlayerStarts = starts.ToArray(),
        };
    }

    private static bool TryBuildNewModuleCommand(
        StudioNewProjectDraft draft,
        bool dryRun,
        out NewModuleCommand? command,
        out string message)
    {
        command = null;
        message = string.Empty;
        if (string.IsNullOrWhiteSpace(draft.ParentDirectory))
        {
            message = "Choisissez le dossier parent du nouveau projet.";
            return false;
        }
        if (draft.Localizations.Count == 0 ||
            !draft.Localizations.Any(item => string.Equals(item.Locale, draft.DefaultLocale, StringComparison.OrdinalIgnoreCase)))
        {
            message = "Ajoutez au moins une langue et choisissez la langue principale.";
            return false;
        }
        StudioLocalizedProjectDraft localization;
        NewModuleMetadataInput metadata;
        try
        {
            localization = draft.DefaultLocalization;
            metadata = new NewModuleMetadataInput(
                draft.DefaultLocale,
                draft.Localizations.Select(item => item.ToDomain()).ToArray(),
                draft.Author,
                draft.Version,
                draft.License,
                draft.ImagePath,
                draft.Tags,
                draft.Classification);
        }
        catch (ArgumentException exception)
        {
            message = HumanizeException(exception);
            return false;
        }

        command = new NewModuleCommand(
            CommandId.From(StableGuid(draft.DraftId, "command")),
            new ProjectRevision(0),
            ProjectId.From(StableGuid(draft.DraftId, "project")),
            ModuleId.From(StableGuid(draft.DraftId, "module")),
            AreaId.From(StableGuid(draft.DraftId, "area")),
            BlueprintId.From(StableGuid(draft.DraftId, "floor-blueprint")),
            EntityId.From(StableGuid(draft.DraftId, "floor-entity")),
            BlueprintId.From(StableGuid(draft.DraftId, "player-spawn-blueprint")),
            EntityId.From(StableGuid(draft.DraftId, "player-spawn-entity")),
            localization.Name,
            localization.Description,
            draft.AreaDisplayName,
            dryRun,
            draft.AreaKind,
            draft.Dimensions,
            draft.Template,
            draft.Kits,
            draft.TargetPlatforms,
            draft.RulesProvider,
            draft.RenderPreset,
            draft.NetworkMode,
            string.Empty,
            draft.GameMode,
            draft.EntryClass,
            draft.PlayerSelectionRule,
            draft.Template == ProjectTemplateKind.Rpg || draft.CreateInitialArea,
            metadata,
            draft.PlayerOrientationDegrees);
        return true;
    }

    private static StudioNewProjectDraft CreateSmokeDraft(
        string parentDirectory,
        ProjectTemplateKind template,
        bool createArea) =>
        StudioNewProjectDraft.CreateDefault(parentDirectory) with
        {
            DraftId = Guid.Parse("d0250000-0000-4000-8000-000000000010"),
            Template = template,
            CreateInitialArea = createArea,
            AreaDisplayName = createArea ? "Clairière de départ" : string.Empty,
            PlayerOrientationDegrees = 90,
            Localizations =
            [
                new StudioLocalizedProjectDraft(
                    "fr", "Preuve UX0-E", "Preuve de création humaine", "Projet construit par le Studio.",
                    "Atelier Forge", string.Empty, "Règles génériques", "Clavier ou manette", "CC0-1.0",
                    string.Empty, "OpenRPG Forge 1.x"),
                new StudioLocalizedProjectDraft(
                    "en", "UX0-E proof", "Human creation proof", "A project built by Studio.",
                    "Forge Workshop", string.Empty, "Generic rules", "Keyboard or gamepad", "CC0-1.0",
                    string.Empty, "OpenRPG Forge 1.x"),
            ],
            DefaultLocale = "fr",
            Author = "Atelier Forge",
            Version = "1.0.0",
            License = "CC0-1.0",
            Classification = "Tout public",
            Tags = ["preuve", "studio"],
            Kits = template == ProjectTemplateKind.Rpg ? ["forge-core", "forge-rpg-kit"] : ["forge-core"],
            RulesProvider = template == ProjectTemplateKind.Rpg ? "forge.generic" : "forge.core",
        };

    private static ProjectFoundationProfile CreateMigrationFoundation(ProjectDefinition project)
    {
        var area = project.Areas.FirstOrDefault();
        var spawn = area?.Entities.FirstOrDefault(entity =>
            project.Blueprints.FirstOrDefault(blueprint => blueprint.Id == entity.BlueprintId)?.Kind ==
            BlueprintKind.PlayerSpawn);
        var id = StableGuid(project.Id.Value, "studio-project-foundation");
        return area is not null && spawn is not null
            ? ProjectFoundationProfile.CreateRpgDefault(id, project.Id, project.Module.Slug, area.Id, spawn.Id)
            : ProjectFoundationProfile.CreateCoreDefault(
                id,
                project.Id,
                project.Module.Slug,
                ProjectTemplateKind.CoreSimulation);
    }

    private static string BuildNewProjectSummary(ProjectDefinition project)
    {
        var foundation = project.Module.Metadata.Foundation!;
        var template = foundation.Template switch
        {
            ProjectTemplateKind.Rpg => "Aventure RPG",
            ProjectTemplateKind.CoreMenu => "Interface ou menu",
            _ => "Simulation sans monde obligatoire",
        };
        var world = project.Areas.Count == 0
            ? "Aucune zone initiale"
            : $"Première zone : {project.Areas[0].DisplayName}";
        return $"{template} · {project.Module.Metadata.Localizations.Count} langue(s) · {world} · " +
               $"{foundation.TargetPlatforms.Count} plateforme(s)";
    }

    private static string Humanize(string? code) => code switch
    {
        NewModuleHandler.TitleRequiredCode => "Donnez un nom au projet.",
        NewModuleHandler.AreaNameRequiredCode => "Nommez la première zone.",
        ProjectFoundationValidator.InitialAreaCode => "Une aventure RPG doit commencer dans une première zone.",
        ProjectFoundationValidator.PlayerStartCode => "Le point de départ du joueur doit être complet.",
        ProjectFoundationValidator.PlayerStartReferenceCode => "Choisissez un point de départ présent dans la zone sélectionnée.",
        ProjectFoundationValidator.IdentityCode => "L’identité automatique du projet n’a pas pu être validée.",
        ProjectFoundationValidator.ChoicesCode => "Vérifiez le modèle, les kits, les plateformes, le rendu et le réseau.",
        SetModuleConfigurationHandler.RevisionConflictCode => "Le projet a changé depuis l’ouverture du formulaire. Rechargez ses valeurs.",
        null => "La demande n’a pas pu être validée.",
        _ => "Une valeur du formulaire n’est pas valide. Vérifiez les champs signalés.",
    };

    private static string HumanizeWorkspace(string? code) => code switch
    {
        ProjectSession.DirectoryNotEmptyCode => "Un projet portant ce nom existe déjà dans le dossier choisi.",
        ProjectSession.ProjectInvalidCode => "Le projet préparé n’a pas passé la validation finale.",
        ProjectSession.CreateFailedCode => "Le projet n’a pas pu être créé dans le dossier choisi.",
        _ => "Le projet n’a pas pu être enregistré.",
    };

    private static string HumanizeException(Exception exception)
    {
        var message = exception.Message;
        if (message.Contains("semantic", StringComparison.OrdinalIgnoreCase))
        {
            return "La version doit suivre le format majeur.mineur.correctif, par exemple 1.0.0.";
        }
        if (message.Contains("locale", StringComparison.OrdinalIgnoreCase))
        {
            return "Le code de langue doit ressembler à fr, en ou fr-FR.";
        }
        if (message.Contains("relative", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("path", StringComparison.OrdinalIgnoreCase))
        {
            return "L’image doit se trouver dans le projet et utiliser un chemin relatif sûr.";
        }
        return string.IsNullOrWhiteSpace(message) ? "Une valeur du formulaire n’est pas valide." : message;
    }

    private static Guid StableGuid(Guid value, string discriminator)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge-studio:{value:D}:{discriminator}"));
        Span<byte> guid = stackalloc byte[16];
        bytes.AsSpan(0, guid.Length).CopyTo(guid);
        guid[6] = (byte)((guid[6] & 0x0f) | 0x40);
        guid[8] = (byte)((guid[8] & 0x3f) | 0x80);
        return new Guid(guid);
    }
}
