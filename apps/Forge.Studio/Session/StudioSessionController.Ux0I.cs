using Forge.Application.Build;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Build;
using Forge.Domain.Modules;
using Forge.ModuleFormat.Build;
using Forge.Scripting;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    internal const string ProjectCacheLocation = "project";
    internal const string IsolatedCacheLocation = "isolated";
    internal const string PreservedCacheLocation = "preserved";

    private readonly SetWorkspaceBuildProfileHandler _setWorkspaceBuildProfile = new(new SystemClock());
    private WorkspaceBuildProfileCommandHistory? _buildProfileHistory;
    private WorkspaceBuildProfile? _persistedBuildProfile;
    private bool _buildProfileFileMissing;
    private readonly Stack<bool> _buildProfileUndoFileStates = [];
    private readonly Stack<bool> _buildProfileRedoFileStates = [];

    internal WorkspaceBuildProfile BuildProfile => _buildProfileHistory?.State.Profile ??
        throw new InvalidOperationException("No workspace build profile is active.");

    internal bool CanUndoScriptCacheProfile => _buildProfileHistory?.CanUndo == true;

    internal bool CanRedoScriptCacheProfile => _buildProfileHistory?.CanRedo == true;

    internal IReadOnlyList<ProjectVariableDefinition> CaptureProjectVariables() =>
        Project?.Module.Variables.ToArray() ?? [];

    internal StudioProjectPreview PreviewProjectVariables(IReadOnlyList<ProjectVariableDefinition> variables) =>
        MutateProjectVariables(variables, apply: false);

    internal StudioProjectPreview ApplyProjectVariables(IReadOnlyList<ProjectVariableDefinition> variables) =>
        MutateProjectVariables(variables, apply: true);

    internal StudioScriptCacheSettings CaptureScriptCacheSettings()
    {
        var profile = BuildProfile;
        var location = profile.CachePath switch
        {
            ".forge" => ProjectCacheLocation,
            ".forge/cache" => IsolatedCacheLocation,
            _ => PreservedCacheLocation,
        };
        return new StudioScriptCacheSettings(
            location,
            profile.IncrementalBuild,
            profile.RebuildAffectedOnly,
            profile.VerifyReproducibility,
            CountForgeScripts(),
            location == PreservedCacheLocation);
    }

    internal StudioActionResult PreviewScriptCacheSettings(string locationKey) =>
        MutateScriptCacheSettings(locationKey, apply: false);

    internal StudioActionResult ApplyScriptCacheSettings(string locationKey) =>
        MutateScriptCacheSettings(locationKey, apply: true);

    internal StudioActionResult UndoScriptCacheProfile()
    {
        if (_buildProfileHistory is null || !_buildProfileHistory.CanUndo ||
            !_buildProfileUndoFileStates.TryPop(out var targetMissing))
        {
            return new StudioActionResult(false, "Aucun réglage de cache à annuler.");
        }

        var currentMissing = _buildProfileFileMissing;
        if (!_buildProfileHistory.Undo())
        {
            _buildProfileUndoFileStates.Push(targetMissing);
            return new StudioActionResult(false, "Aucun réglage de cache à annuler.");
        }
        _buildProfileRedoFileStates.Push(currentMissing);
        if (!TryPersistBuildProfile(_buildProfileHistory.State.Profile, targetMissing, out var error))
        {
            _buildProfileRedoFileStates.Pop();
            _buildProfileHistory.Redo();
            _buildProfileUndoFileStates.Push(targetMissing);
            return new StudioActionResult(false, error);
        }

        _persistedBuildProfile = _buildProfileHistory.State.Profile;
        _buildProfileFileMissing = targetMissing;
        return new StudioActionResult(true, "Réglage précédent du cache restauré.");
    }

    internal StudioActionResult RedoScriptCacheProfile()
    {
        if (_buildProfileHistory is null || !_buildProfileHistory.CanRedo ||
            !_buildProfileRedoFileStates.TryPop(out var targetMissing))
        {
            return new StudioActionResult(false, "Aucun réglage de cache à rétablir.");
        }

        var currentMissing = _buildProfileFileMissing;
        if (!_buildProfileHistory.Redo())
        {
            _buildProfileRedoFileStates.Push(targetMissing);
            return new StudioActionResult(false, "Aucun réglage de cache à rétablir.");
        }
        _buildProfileUndoFileStates.Push(currentMissing);
        if (!TryPersistBuildProfile(_buildProfileHistory.State.Profile, targetMissing, out var error))
        {
            _buildProfileUndoFileStates.Pop();
            _buildProfileHistory.Undo();
            _buildProfileRedoFileStates.Push(targetMissing);
            return new StudioActionResult(false, error);
        }

        _persistedBuildProfile = _buildProfileHistory.State.Profile;
        _buildProfileFileMissing = targetMissing;
        return new StudioActionResult(true, "Réglage du cache rétabli.");
    }

    internal StudioScriptCacheReport WarmScriptCache()
    {
        if (Session is null || _buildProfileHistory is null)
        {
            return new StudioScriptCacheReport(false, "Aucun projet n’est ouvert.", 0, 0, 0, string.Empty,
                string.Empty);
        }

        try
        {
            var workspace = Path.GetFullPath(Session.RootDirectory);
            var scriptsRoot = Path.Combine(workspace, "scripts");
            var scripts = Directory.Exists(scriptsRoot)
                ? Directory.EnumerateFiles(scriptsRoot, "*.forge", SearchOption.AllDirectories)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                : [];
            var runtime = new NativeForgeScriptRuntime();
            if (scripts.Length == 0)
            {
                return new StudioScriptCacheReport(
                    true,
                    "Aucun script à compiler · le cache est prêt pour le premier script.",
                    0,
                    0,
                    0,
                    runtime.ApiVersion,
                    runtime.VirtualMachineVersion);
            }

            var cacheBase = Path.GetFullPath(Path.Combine(
                workspace,
                BuildProfile.CachePath.Replace('/', Path.DirectorySeparatorChar)));
            var relativeCache = Path.GetRelativePath(workspace, cacheBase);
            if (Path.IsPathRooted(relativeCache) || relativeCache.Split(Path.DirectorySeparatorChar)
                    .Any(segment => segment == ".."))
            {
                return new StudioScriptCacheReport(false, "L’emplacement du cache sort du projet.", scripts.Length,
                    0, 0, runtime.ApiVersion, runtime.VirtualMachineVersion);
            }

            var cache = new ForgeScriptArtifactCache(runtime, Path.Combine(cacheBase, "forgescript"));
            var firstPass = new List<ForgeScriptCacheResult>(scripts.Length);
            foreach (var script in scripts)
            {
                var result = cache.Compile(File.ReadAllText(script));
                firstPass.Add(result);
                if (!result.Compilation.Success)
                {
                    var diagnostic = result.Compilation.Diagnostics.FirstOrDefault()?.Message ??
                                     "Erreur de compilation non détaillée.";
                    return new StudioScriptCacheReport(
                        false,
                        $"Le script « {Path.GetFileNameWithoutExtension(script)} » est invalide : {diagnostic}",
                        scripts.Length,
                        firstPass.Count(item => !item.CacheHit),
                        firstPass.Count(item => item.CacheHit),
                        runtime.ApiVersion,
                        runtime.VirtualMachineVersion);
                }
            }

            var secondPass = scripts.Select(script => cache.Compile(File.ReadAllText(script))).ToArray();
            var secondPassValid = secondPass.All(result => result.Compilation.Success && result.CacheHit);
            var compiled = firstPass.Count(result => !result.CacheHit);
            var reused = secondPass.Count(result => result.CacheHit);
            return new StudioScriptCacheReport(
                secondPassValid,
                secondPassValid
                    ? $"Cache vérifié · {reused} script(s) réutilisé(s) sans recompilation."
                    : "Le cache n’a pas pu réutiliser tous les scripts.",
                scripts.Length,
                compiled,
                reused,
                runtime.ApiVersion,
                runtime.VirtualMachineVersion);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                               ArgumentException or InvalidOperationException or
                                               DllNotFoundException or BadImageFormatException or
                                               EntryPointNotFoundException or TypeInitializationException)
        {
            return new StudioScriptCacheReport(
                false,
                $"Le cache ForgeScript n’a pas pu être vérifié : {exception.Message}",
                0,
                0,
                0,
                string.Empty,
                string.Empty);
        }
    }

    internal StudioUx0ISmokeResult RunUx0IContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux0i-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            Directory.CreateDirectory(tempRoot);
            var draft = StudioNewProjectDraft.CreateDefault(tempRoot) with
            {
                DraftId = Guid.Parse("d0250000-0000-4000-8000-000000000400"),
            };
            var created = CreateNewProject(draft);
            if (!created.Succeeded || created.WorkspaceDirectory is null)
            {
                return FailedUx0ISmoke();
            }
            var scripts = Path.Combine(created.WorkspaceDirectory, "scripts");
            Directory.CreateDirectory(scripts);
            var probe = Path.Combine(scripts, "cache-probe.forge");
            File.WriteAllText(probe, "void main() { say(\"première compilation\"); }");
            if (!editing.Open(created.WorkspaceDirectory).Succeeded)
            {
                return FailedUx0ISmoke();
            }

            var original = editing.Project!;
            var originalDistribution = original.Module.Distribution;
            var originalContent = original.Module.Content;
            var originalPolicies = original.Module.RuntimePolicies;
            var variables = new[]
            {
                Variable("session.preview", ProjectVariableScope.Session, "aperçu"),
                Variable("world.chapter", ProjectVariableScope.Save, "1", ProjectVariableValueKind.Integer,
                    ProjectVariableReplicationMode.AllClients),
                Variable("player.reputation", ProjectVariableScope.Player, "0", ProjectVariableValueKind.Integer,
                    ProjectVariableReplicationMode.Owner),
                Variable("party.ready", ProjectVariableScope.Party, "false", ProjectVariableValueKind.Boolean,
                    ProjectVariableReplicationMode.Party),
                Variable("server.phase", ProjectVariableScope.Server, "lobby", replication:
                    ProjectVariableReplicationMode.ServerAuthority, required: true),
            };
            var variablePreview = editing.PreviewProjectVariables(variables);
            var dryRun = variablePreview.Succeeded && ReferenceEquals(original, editing.Project);
            var variableApply = editing.ApplyProjectVariables(variables);
            var variableScopes = variableApply.Succeeded &&
                                 Enum.GetValues<ProjectVariableScope>().All(scope =>
                                     editing.Project!.Module.Variables.Any(variable => variable.Scope == scope));
            var undoVariables = editing.Undo();
            var redoVariables = editing.Redo();
            var variableUndoRedo = undoVariables.Succeeded && redoVariables.Succeeded &&
                                   editing.Project!.Module.Variables.Count == variables.Length;

            var invalid = variables.Append(Variable(
                "session.required",
                ProjectVariableScope.Session,
                "must survive",
                required: true)).ToArray();
            var invalidPreview = editing.PreviewProjectVariables(invalid);
            var invalidVariableBlocked = !invalidPreview.Succeeded &&
                                         !invalidPreview.Message.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                                         !invalidPreview.Message.Contains("hash", StringComparison.OrdinalIgnoreCase);

            var configuration = editing.CaptureProjectConfiguration();
            var localized = configuration.Localizations.First(item => string.Equals(
                item.Locale,
                configuration.DefaultLocale,
                StringComparison.OrdinalIgnoreCase)) with
            {
                Description = "Synopsis complet de l’aventure.",
                Installation = "Installer puis lancer le module.",
                Rules = "Les choix ont des conséquences.",
                Controls = "Clavier, souris ou manette.",
                Credits = "Atelier Forge.",
                Licenses = "Contenus sous licences déclarées.",
                Warnings = "Violence fantastique modérée.",
                Compatibility = "OpenRPG Forge 1.x.",
            };
            var guideDraft = configuration with
            {
                Localizations = configuration.Localizations.Select(item =>
                    string.Equals(item.Locale, configuration.DefaultLocale, StringComparison.OrdinalIgnoreCase)
                        ? localized
                        : item).ToArray(),
            };
            var guidePreview = editing.PreviewProjectConfiguration(guideDraft);
            var guideApply = editing.ApplyProjectConfiguration(guideDraft);
            var undoGuide = editing.Undo();
            var redoGuide = editing.Redo();
            var playerGuide = guidePreview.Succeeded && guideApply.Succeeded && undoGuide.Succeeded &&
                              redoGuide.Succeeded && GuideIsComplete(
                                  editing.Project!.Module.Metadata.GetDefaultLocalization());

            var cachePreview = editing.PreviewScriptCacheSettings(IsolatedCacheLocation);
            var cacheApply = editing.ApplyScriptCacheSettings(IsolatedCacheLocation);
            var cacheUndo = editing.UndoScriptCacheProfile();
            var missingRestored = !File.Exists(Path.Combine(
                created.WorkspaceDirectory,
                WorkspaceBuildProfileStore.RelativePath));
            var cacheRedo = editing.RedoScriptCacheProfile();
            var firstCache = editing.WarmScriptCache();
            File.WriteAllText(probe, "void main() { say(\"source modifiée\"); }");
            var invalidatedCache = editing.WarmScriptCache();
            var scriptCache = cachePreview.Succeeded && cacheApply.Succeeded && cacheUndo.Succeeded &&
                              missingRestored && cacheRedo.Succeeded && firstCache.Succeeded &&
                              firstCache.ScriptCount == 1 && firstCache.Reused == 1;
            var cacheInvalidation = firstCache.Compiled == 1 && invalidatedCache.Succeeded &&
                                    invalidatedCache.Compiled == 1 && invalidatedCache.Reused == 1;

            var neighborDataPreserved = ReferenceEquals(originalDistribution, editing.Project!.Module.Distribution) &&
                                        ReferenceEquals(originalContent, editing.Project.Module.Content) &&
                                        ReferenceEquals(originalPolicies, editing.Project.Module.RuntimePolicies);
            var atomicUndoRedo = variableUndoRedo && undoGuide.Succeeded && redoGuide.Succeeded &&
                                 cacheUndo.Succeeded && cacheRedo.Succeeded;
            var saved = editing.Save();
            editing.Close();
            var opened = reopened.Open(created.WorkspaceDirectory);
            var restoredLocalization = reopened.Project?.Module.Metadata.GetDefaultLocalization();
            var loadedCache = opened.Succeeded ? reopened.CaptureScriptCacheSettings() : null;
            var restoredVariableCount = reopened.Project?.Module.Variables.Count == variables.Length;
            var restoredVariableIds = reopened.Project is not null &&
                                      reopened.Project.Module.Variables.Select(item => item.Id)
                                          .OrderBy(item => item.ToString(), StringComparer.Ordinal)
                                          .SequenceEqual(variables.Select(item => item.Id)
                                              .OrderBy(item => item.ToString(), StringComparer.Ordinal));
            var restoredGuide = restoredLocalization is not null && GuideIsComplete(restoredLocalization);
            var restoredCache = loadedCache?.LocationKey == IsolatedCacheLocation;
            var persistenceRoundTrip = saved.Succeeded && opened.Succeeded && restoredVariableCount &&
                                       restoredVariableIds && restoredGuide && restoredCache;
            var humanDiagnostics = invalidVariableBlocked &&
                                   !firstCache.Message.Contains(".forge", StringComparison.OrdinalIgnoreCase) &&
                                   !firstCache.Message.Contains("hash", StringComparison.OrdinalIgnoreCase);
            var succeeded = variableScopes && invalidVariableBlocked && playerGuide && scriptCache &&
                            cacheInvalidation && dryRun && atomicUndoRedo && persistenceRoundTrip &&
                            neighborDataPreserved && humanDiagnostics;
            return new StudioUx0ISmokeResult(
                succeeded,
                variableScopes,
                invalidVariableBlocked,
                playerGuide,
                scriptCache,
                cacheInvalidation,
                dryRun,
                atomicUndoRedo,
                persistenceRoundTrip,
                neighborDataPreserved,
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

    private StudioProjectPreview MutateProjectVariables(
        IReadOnlyList<ProjectVariableDefinition> variables,
        bool apply)
    {
        ArgumentNullException.ThrowIfNull(variables);
        if (History is null || Project is null)
        {
            return new StudioProjectPreview(false, "Aucun projet n’est ouvert.", string.Empty);
        }

        var command = new SetModuleConfigurationCommand(
            CommandId.From(Guid.NewGuid()),
            History.State.Revision,
            Project.Module.Metadata,
            variables,
            DryRun: !apply);
        var preview = _setModuleConfiguration.Handle(History.State, command with { DryRun = true });
        if (!preview.Succeeded)
        {
            return new StudioProjectPreview(
                false,
                HumanizeUx0I(preview.Diagnostics.FirstOrDefault()?.Code),
                string.Empty);
        }
        if (!apply)
        {
            return new StudioProjectPreview(
                true,
                "Aperçu validé · le projet n’a pas été modifié",
                $"{variables.Count} variable(s) typée(s) · cinq portées disponibles");
        }

        var applied = _setModuleConfiguration.Handle(History.State, command with { DryRun = false });
        if (!applied.Succeeded)
        {
            return new StudioProjectPreview(
                false,
                HumanizeUx0I(applied.Diagnostics.FirstOrDefault()?.Code),
                string.Empty);
        }
        History.Record(applied);
        return new StudioProjectPreview(
            true,
            "Variables appliquées · Ctrl+Z pour annuler · Ctrl+S pour enregistrer",
            $"{variables.Count} variable(s) · identités internes préservées",
            applied.State.Project);
    }

    private StudioActionResult MutateScriptCacheSettings(string locationKey, bool apply)
    {
        if (_buildProfileHistory is null)
        {
            return new StudioActionResult(false, "Aucun profil de build n’est ouvert.");
        }

        WorkspaceBuildProfile candidate;
        try
        {
            candidate = WithCacheLocation(_buildProfileHistory.State.Profile, locationKey);
        }
        catch (ArgumentException exception)
        {
            return new StudioActionResult(false, exception.Message);
        }
        var command = new SetWorkspaceBuildProfileCommand(
            CommandId.From(Guid.NewGuid()),
            _buildProfileHistory.State.Revision,
            candidate,
            DryRun: !apply);
        var preview = _setWorkspaceBuildProfile.Handle(
            _buildProfileHistory.State,
            command with { DryRun = true });
        if (!preview.Succeeded)
        {
            return new StudioActionResult(false, HumanizeUx0I(preview.Diagnostics.FirstOrDefault()?.Code));
        }
        if (!apply)
        {
            return new StudioActionResult(true, "Aperçu valide · aucun réglage ni fichier modifié.");
        }

        var previousFileMissing = _buildProfileFileMissing;
        var applied = _setWorkspaceBuildProfile.Handle(
            _buildProfileHistory.State,
            command with { DryRun = false });
        if (!applied.Succeeded)
        {
            return new StudioActionResult(false, HumanizeUx0I(applied.Diagnostics.FirstOrDefault()?.Code));
        }
        _buildProfileHistory.Record(applied);
        _buildProfileUndoFileStates.Push(previousFileMissing);
        _buildProfileRedoFileStates.Clear();
        if (!TryPersistBuildProfile(applied.State.Profile, fileMissing: false, out var error))
        {
            _buildProfileHistory.Undo();
            _buildProfileUndoFileStates.Pop();
            return new StudioActionResult(false, error);
        }

        _persistedBuildProfile = applied.State.Profile;
        _buildProfileFileMissing = false;
        return new StudioActionResult(true, "Réglage du cache validé et enregistré atomiquement.");
    }

    private void LoadUx0IProjectData(string workspaceRoot)
    {
        var loaded = WorkspaceBuildProfileStore.Load(workspaceRoot);
        _buildProfileHistory = new WorkspaceBuildProfileCommandHistory(loaded.Profile);
        _persistedBuildProfile = loaded.Profile;
        _buildProfileFileMissing = loaded.FileMissing;
        _buildProfileUndoFileStates.Clear();
        _buildProfileRedoFileStates.Clear();
        if (!loaded.Succeeded)
        {
            var warning = "Profil de cache ignoré : " + loaded.Diagnostics[0].Message;
            WorkspacePreferenceWarning = string.IsNullOrWhiteSpace(WorkspacePreferenceWarning)
                ? warning
                : WorkspacePreferenceWarning + " · " + warning;
        }
    }

    private void CloseUx0I()
    {
        _buildProfileHistory = null;
        _persistedBuildProfile = null;
        _buildProfileFileMissing = true;
        _buildProfileUndoFileStates.Clear();
        _buildProfileRedoFileStates.Clear();
    }

    private bool TryPersistBuildProfile(
        WorkspaceBuildProfile profile,
        bool fileMissing,
        out string error)
    {
        try
        {
            var workspace = Session?.RootDirectory ?? throw new InvalidOperationException("Aucun projet n’est ouvert.");
            var path = Path.Combine(Path.GetFullPath(workspace), WorkspaceBuildProfileStore.RelativePath);
            if (fileMissing)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                error = string.Empty;
                return true;
            }

            var result = WorkspaceBuildProfileStore.Save(workspace, profile);
            if (!result.Succeeded)
            {
                error = "Profil de cache non enregistré : " + result.Diagnostics[0].Message;
                return false;
            }
            error = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                               InvalidOperationException or ArgumentException)
        {
            error = "Profil de cache non enregistré : " + exception.Message;
            return false;
        }
    }

    private WorkspaceBuildProfile WithCacheLocation(WorkspaceBuildProfile source, string locationKey)
    {
        if (string.Equals(locationKey, PreservedCacheLocation, StringComparison.Ordinal))
        {
            return source;
        }
        var cachePath = locationKey switch
        {
            ProjectCacheLocation => ".forge",
            IsolatedCacheLocation => ".forge/cache",
            _ => throw new ArgumentException("Choisissez un emplacement de cache proposé par le Studio."),
        };
        var layout = source.Layout.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        layout["cache"] = cachePath;
        return new WorkspaceBuildProfile(
            source.Id,
            source.SchemaVersion,
            "studio:ux0-i-script-cache",
            source.RequireGitRepository,
            source.TextDocuments,
            source.ExplicitGeneratedFiles,
            source.ReproducibleAssets,
            layout,
            source.CliOperations,
            source.StableOrdering,
            source.NormalizedTimestamps,
            source.LockedDependencies,
            source.ContentHashes,
            source.VerifyReproducibility,
            source.IncrementalBuild,
            cachePath,
            source.RebuildAffectedOnly,
            source.Governance,
            source.Collaboration,
            source.Compatibility);
    }

    private int CountForgeScripts()
    {
        var scripts = Session is null ? string.Empty : Path.Combine(Session.RootDirectory, "scripts");
        return Directory.Exists(scripts)
            ? Directory.EnumerateFiles(scripts, "*.forge", SearchOption.AllDirectories).Count()
            : 0;
    }

    private static ProjectVariableDefinition Variable(
        string key,
        ProjectVariableScope scope,
        string value,
        ProjectVariableValueKind kind = ProjectVariableValueKind.String,
        ProjectVariableReplicationMode replication = ProjectVariableReplicationMode.None,
        bool required = false) => new(
        ProjectVariableId.From(StableUx0IGuid(key)),
        key,
        kind,
        scope,
        value,
        "Variable de validation UX0-I",
        "studio:ux0-i-smoke",
        ProjectVariableDefinition.CurrentSchemaVersion,
        replication,
        debugVisible: true,
        requiredAtRuntime: required);

    private static Guid StableUx0IGuid(string value)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("ux0-i:" + value));
        Span<byte> guid = stackalloc byte[16];
        bytes.AsSpan(0, guid.Length).CopyTo(guid);
        guid[6] = (byte)((guid[6] & 0x0f) | 0x40);
        guid[8] = (byte)((guid[8] & 0x3f) | 0x80);
        return new Guid(guid);
    }

    private static bool GuideIsComplete(ProjectLocalizedMetadata value) =>
        !string.IsNullOrWhiteSpace(value.Description) &&
        !string.IsNullOrWhiteSpace(value.Installation) &&
        !string.IsNullOrWhiteSpace(value.Rules) &&
        !string.IsNullOrWhiteSpace(value.Controls) &&
        !string.IsNullOrWhiteSpace(value.Credits) &&
        !string.IsNullOrWhiteSpace(value.Licenses) &&
        !string.IsNullOrWhiteSpace(value.Warnings) &&
        !string.IsNullOrWhiteSpace(value.Compatibility);

    private static string HumanizeUx0I(string? code) => code switch
    {
        ProjectVariableValidator.DuplicateIdCode or ProjectVariableValidator.DuplicateKeyCode =>
            "Deux variables portent le même nom. Donnez-leur des noms distincts.",
        ProjectVariableValidator.RequiredSessionCode =>
            "Une variable requise doit être conservée dans une sauvegarde, un joueur, un groupe ou le serveur.",
        ProjectVariableValidator.ServerReplicationCode =>
            "Une variable contrôlée par le serveur doit utiliser la portée Serveur.",
        WorkspaceBuildProfileValidator.LayoutInvalidCode =>
            "L’emplacement du cache doit rester dans le projet.",
        WorkspaceBuildProfileValidator.DeterminismInvalidCode or
            WorkspaceBuildProfileValidator.IncrementalInvalidCode =>
            "Le cache doit conserver le build incrémental et reproductible.",
        SetModuleConfigurationHandler.RevisionConflictCode or
            SetWorkspaceBuildProfileHandler.RevisionConflictCode =>
            "Le projet a changé depuis l’ouverture du formulaire. Rechargez ses valeurs.",
        _ => "Une valeur n’est pas valide. Vérifiez le nom, le type, la portée et la valeur par défaut.",
    };

    private static StudioUx0ISmokeResult FailedUx0ISmoke() => new(
        false, false, false, false, false, false, false, false, false, false, false);
}
