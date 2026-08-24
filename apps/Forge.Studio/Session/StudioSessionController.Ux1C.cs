using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Domain.RuntimePolicies;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private static readonly HashSet<string> LoadingImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".svg",
    };

    private static readonly HashSet<string> LoadingVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ogv", ".webm",
    };

    private readonly SetAreaExperienceProfileHandler _setAreaExperienceProfile = new(new SystemClock());
    private StudioAreaLoadingDraft? _approvedAreaLoading;

    internal StudioAreaLoadingDraft CreateAreaLoadingDraft()
    {
        var area = ActiveArea ?? throw new InvalidOperationException("Aucune zone n’est active.");
        var loading = area.RuntimePolicies.Experience.Loading;
        return new StudioAreaLoadingDraft(
            area.Id,
            loading.Title,
            loading.ImageResource,
            loading.VideoResource,
            TipsForLocale(loading.LocalizedTips, "fr"),
            TipsForLocale(loading.LocalizedTips, "en"),
            loading.LocalizedTips
                .Where(pair => !string.Equals(pair.Key, "fr", StringComparison.OrdinalIgnoreCase) &&
                               !string.Equals(pair.Key, "en", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.OrdinalIgnoreCase),
            loading.ShowProgress,
            loading.AccessibleFallback,
            loading.PrefetchRequiredResources,
            loading.MinimumVisibleMilliseconds);
    }

    internal StudioAreaLoadingCatalog CreateAreaLoadingCatalog()
    {
        var area = ActiveArea ?? throw new InvalidOperationException("Aucune zone n’est active.");
        var loading = area.RuntimePolicies.Experience.Loading;
        var files = DiscoverLoadingMedia();
        var images = BuildMediaOptions(files, LoadingMediaKind.Image, loading.ImageResource);
        var videos = BuildMediaOptions(files, LoadingMediaKind.Video, loading.VideoResource);
        var resources = area.World.Loading.RequiredResources
            .Select(HumanizeResourceName)
            .ToArray();
        return new StudioAreaLoadingCatalog(images, videos, resources);
    }

    internal StudioAreaLoadingPreview PreviewAreaLoading(StudioAreaLoadingDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        _approvedAreaLoading = null;
        if (!TryBuildAreaLoading(draft, dryRun: true, out var command, out var profile, out var message))
        {
            RecordAreaLoadingDiagnostic("experience.loading.invalid", message);
            return new StudioAreaLoadingPreview(false, message, string.Empty, false);
        }

        var dryRun = _setAreaExperienceProfile.Handle(History!.State, command!);
        if (!dryRun.Succeeded)
        {
            var diagnostic = dryRun.Diagnostics.FirstOrDefault();
            var human = HumanizeAreaLoading(diagnostic?.Code);
            RecordAreaLoadingDiagnostic(diagnostic?.Code ?? "experience.loading.refused", human);
            return new StudioAreaLoadingPreview(false, human, string.Empty, false);
        }

        var loading = profile!.Loading;
        var imageAvailable = ResolveLoadingMediaAbsolutePath(loading.ImageResource) is not null;
        var videoAvailable = ResolveLoadingMediaAbsolutePath(loading.VideoResource) is not null;
        var standard = AreaExperienceRuntimeService.ResolveLoading(
            loading,
            "fr",
            completed: 1,
            total: 2,
            tipIndex: 0,
            reduceMotion: false,
            imageAvailable,
            videoAvailable);
        var reduced = AreaExperienceRuntimeService.ResolveLoading(
            loading,
            "fr",
            completed: 1,
            total: 2,
            tipIndex: 0,
            reduceMotion: true,
            imageAvailable,
            videoAvailable);
        var requiredResourceCount = ActiveArea!.World.Loading.RequiredResources.Count;
        _approvedAreaLoading = draft;
        return new StudioAreaLoadingPreview(
            true,
            "Aperçu validé · aucun document ni média modifié.",
            BuildAreaLoadingSummary(loading, standard, reduced, requiredResourceCount),
            true,
            standard,
            reduced,
            requiredResourceCount);
    }

    internal StudioAreaLoadingResult ApplyApprovedAreaLoading()
    {
        if (_approvedAreaLoading is null || History is null)
        {
            return new StudioAreaLoadingResult(false, "Prévisualisez d’abord l’écran de chargement.");
        }

        var draft = _approvedAreaLoading;
        if (!TryBuildAreaLoading(draft, dryRun: false, out var command, out _, out var message))
        {
            _approvedAreaLoading = null;
            return new StudioAreaLoadingResult(false, message);
        }

        var dryRun = _setAreaExperienceProfile.Handle(History.State, command! with { DryRun = true });
        if (!dryRun.Succeeded)
        {
            _approvedAreaLoading = null;
            return new StudioAreaLoadingResult(
                false,
                HumanizeAreaLoading(dryRun.Diagnostics.FirstOrDefault()?.Code));
        }

        var applied = _setAreaExperienceProfile.Handle(History.State, command!);
        if (!applied.Succeeded)
        {
            _approvedAreaLoading = null;
            return new StudioAreaLoadingResult(
                false,
                HumanizeAreaLoading(applied.Diagnostics.FirstOrDefault()?.Code));
        }

        History.Record(applied);
        InvalidateContentIndexes();
        _approvedAreaLoading = null;
        return new StudioAreaLoadingResult(
            true,
            $"Écran « {draft.Title.Trim()} » appliqué à la zone · Ctrl+Z pour annuler");
    }

    internal void CancelAreaLoading() => _approvedAreaLoading = null;

    internal string? ResolveLoadingMediaAbsolutePath(string resource)
    {
        if (string.IsNullOrWhiteSpace(resource) || Session is null)
        {
            return null;
        }

        return TryResolveWorkspaceMedia(resource, LoadingMediaKind.Image, out var imagePath) ||
               TryResolveWorkspaceMedia(resource, LoadingMediaKind.Video, out imagePath)
            ? imagePath
            : null;
    }

    internal StudioUx1CSmokeResult RunUx1CContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux1c-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var sourceId = AreaId.From(Guid.Parse("c1c10000-0000-4000-8000-000000000001"));
            var neighborId = AreaId.From(Guid.Parse("c1c10000-0000-4000-8000-000000000002"));
            var source = new AreaDefinition(
                sourceId,
                "clairiere-chargement",
                "Clairière du chargement",
                [],
                AreaKind.Exterior,
                new AreaDimensions(48, 48));
            var originalPolicies = source.RuntimePolicies;
            var originalExperience = originalPolicies.Experience;
            var cue = new AudioCueDefinition(
                Guid.Parse("c1c10000-0000-4000-8000-000000000003"),
                "ambient.leaves",
                string.Empty,
                "Ambience",
                .7f,
                true,
                string.Empty,
                string.Empty,
                "1.0",
                "test:ux1-c");
            var seedLoading = originalExperience.Loading with
            {
                LocalizedTips = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["fr"] = ["Le sentier suit la rivière."],
                    ["en"] = ["The trail follows the river."],
                    ["de"] = ["Der Pfad folgt dem Fluss."],
                },
                Provenance = "test:ux1-c",
            };
            var seedExperience = new AreaExperienceProfile(
                originalExperience.Id,
                originalExperience.SchemaVersion,
                [cue],
                originalExperience.ScatterEmitters,
                originalExperience.Voices,
                originalExperience.PatrolRoutes,
                seedLoading,
                "test:ux1-c");
            var policies = new AreaRuntimePolicyProfile(
                originalPolicies.Id,
                originalPolicies.SchemaVersion,
                originalPolicies.Heartbeats,
                originalPolicies.Rest,
                "test:ux1-c",
                originalPolicies.Cinematics,
                originalPolicies.Rules,
                seedExperience);
            var originalWorld = source.World;
            var worldLoading = new AreaLoadingProfile(
                originalWorld.Loading.Id,
                originalWorld.Loading.SchemaVersion,
                true,
                true,
                true,
                64,
                2,
                1,
                2_000,
                ["chunks/area.bin", "chunks/shared.bin"],
                "test:ux1-c");
            var world = new AreaWorldProfile(
                originalWorld.Id,
                originalWorld.SchemaVersion,
                originalWorld.Terrain,
                originalWorld.Biomes,
                originalWorld.Features,
                originalWorld.VolumetricNavigation,
                originalWorld.FlightRoutes,
                worldLoading,
                "test:ux1-c");
            source = source.WithRuntimePolicies(policies).WithWorld(world);
            var neighbor = new AreaDefinition(
                neighborId,
                "lisiere",
                "Lisière",
                [],
                AreaKind.Exterior,
                new AreaDimensions(32, 32));
            var project = new ProjectDefinition(
                ProjectId.From(Guid.Parse("c1c10000-0000-4000-8000-000000000004")),
                "Studio UX1-C",
                [],
                [source, neighbor]);
            var created = ProjectSession.Create(tempRoot, project);
            if (!created.Succeeded)
            {
                return FailedUx1CSmoke();
            }

            WriteSmokeResource(tempRoot, "assets/loading/forest.png", TinyPngBytes());
            WriteSmokeResource(tempRoot, "assets/loading/forest.ogv", [0x4f, 0x67, 0x67, 0x53]);
            WriteSmokeResource(tempRoot, "chunks/area.bin", [1, 2, 3]);
            WriteSmokeResource(tempRoot, "chunks/shared.bin", [4, 5, 6, 7]);
            if (!editing.Open(tempRoot).Succeeded)
            {
                return FailedUx1CSmoke();
            }

            var originalProject = editing.Project!;
            var originalSource = originalProject.Areas.Single(item => item.Id == sourceId);
            var originalNeighbor = originalProject.Areas.Single(item => item.Id == neighborId);
            var draft = editing.CreateAreaLoadingDraft() with
            {
                Title = "Sous les frondaisons",
                ImageResource = "assets/loading/forest.png",
                VideoResource = "assets/loading/forest.ogv",
                FrenchTips = ["Observez les pierres moussues.", "Écoutez le vent avant d’avancer."],
                EnglishTips = ["Watch the moss-covered stones."],
                ShowProgress = true,
                AccessibleFallback = "Préparation de la clairière et de ses rencontres.",
                PrefetchRequiredResources = true,
                MinimumVisibleMilliseconds = 650,
            };
            var invalidMedia = editing.PreviewAreaLoading(draft with
            {
                ImageResource = "../outside.png",
            });
            var invalidFallback = editing.PreviewAreaLoading(draft with
            {
                AccessibleFallback = " ",
            });
            var invalidBlocked = !invalidMedia.Succeeded && !invalidFallback.Succeeded;
            var humanDiagnostics = invalidBlocked &&
                                   IsHumanAreaLoadingMessage(invalidMedia.Message) &&
                                   IsHumanAreaLoadingMessage(invalidFallback.Message);

            var preview = editing.PreviewAreaLoading(draft);
            var previewValid = preview.Succeeded && preview.CanApply && preview.StandardFrame is not null &&
                               preview.ReducedMotionFrame is not null && preview.RequiredResourceCount == 2;
            var dryRun = previewValid && ReferenceEquals(originalProject, editing.Project);
            var imageAndVideo = preview.StandardFrame?.MediaKind == LoadingMediaKind.Video &&
                                preview.StandardFrame.MediaResource == draft.VideoResource;
            var reducedMotion = preview.ReducedMotionFrame?.MediaKind == LoadingMediaKind.Image &&
                                preview.ReducedMotionFrame.MediaResource == draft.ImageResource;
            var fallbackFrame = AreaExperienceRuntimeService.ResolveLoading(
                new AreaLoadingPresentation(
                    seedLoading.Id,
                    draft.Title,
                    string.Empty,
                    draft.VideoResource,
                    new Dictionary<string, IReadOnlyList<string>> { ["fr"] = draft.FrenchTips },
                    draft.ShowProgress,
                    draft.AccessibleFallback,
                    draft.PrefetchRequiredResources,
                    draft.MinimumVisibleMilliseconds,
                    "test:ux1-c"),
                "fr",
                0,
                0,
                0,
                reduceMotion: true,
                imageAvailable: false,
                videoAvailable: true);
            var accessibleFallback = fallbackFrame.MediaKind == LoadingMediaKind.AccessibleFallback &&
                                     fallbackFrame.AccessibleText == draft.AccessibleFallback &&
                                     fallbackFrame.Progress == 1;

            var applied = editing.ApplyApprovedAreaLoading();
            var appliedProject = editing.Project!;
            var appliedSource = appliedProject.Areas.Single(item => item.Id == sourceId);
            var appliedExperience = appliedSource.RuntimePolicies.Experience;
            var otherExperiencePreserved = applied.Succeeded && appliedExperience.AudioCues.Count == 1 &&
                                           appliedExperience.AudioCues[0] == cue &&
                                           appliedExperience.ScatterEmitters.SequenceEqual(seedExperience.ScatterEmitters) &&
                                           appliedExperience.Voices.SequenceEqual(seedExperience.Voices) &&
                                           appliedExperience.PatrolRoutes.SequenceEqual(seedExperience.PatrolRoutes) &&
                                           appliedExperience.Loading.LocalizedTips["de"].Single() ==
                                           seedLoading.LocalizedTips["de"].Single();
            var worldProfilePreserved = ReferenceEquals(appliedSource.World, originalSource.World) &&
                                        appliedSource.World.Loading.RequiredResources.SequenceEqual(
                                            originalSource.World.Loading.RequiredResources);
            var neighborPreserved = ReferenceEquals(
                originalNeighbor,
                appliedProject.Areas.Single(item => item.Id == neighborId));
            var minimumDuration = appliedExperience.Loading.MinimumVisibleMilliseconds == 650;

            var progress = new List<AreaStreamingProgress>();
            var coordinator = new AreaStreamingCoordinator(new SmokeFileChunkLoader(tempRoot));
            var streaming = coordinator.LoadAsync(
                    appliedSource.World.Loading,
                    new InlineProgress<AreaStreamingProgress>(progress.Add))
                .GetAwaiter()
                .GetResult();
            var realStreaming = appliedExperience.Loading.PrefetchRequiredResources &&
                                streaming.Resources.Count == 2 &&
                                streaming.LoadedResources == 2 &&
                                progress.Count == 2 &&
                                progress[^1].Fraction == 1;

            var undo = editing.Undo();
            var undone = ReferenceEquals(originalProject, editing.Project);
            var redo = editing.Redo();
            var redone = ReferenceEquals(appliedProject, editing.Project);
            var undoRedo = undo.Succeeded && undone && redo.Succeeded && redone;

            var save = editing.Save();
            editing.Close();
            var reopen = reopened.Open(tempRoot);
            var restoredSource = reopened.Project?.Areas.SingleOrDefault(item => item.Id == sourceId);
            var restoredLoading = restoredSource?.RuntimePolicies.Experience.Loading;
            var persistence = save.Succeeded && reopen.Succeeded && restoredLoading is not null &&
                              restoredLoading.Title == draft.Title &&
                              restoredLoading.ImageResource == draft.ImageResource &&
                              restoredLoading.VideoResource == draft.VideoResource &&
                              restoredLoading.LocalizedTips["fr"].SequenceEqual(draft.FrenchTips) &&
                              restoredLoading.LocalizedTips["en"].SequenceEqual(draft.EnglishTips) &&
                              restoredLoading.LocalizedTips["de"].Single() ==
                              seedLoading.LocalizedTips["de"].Single() &&
                              restoredLoading.ShowProgress &&
                              restoredLoading.PrefetchRequiredResources &&
                              restoredLoading.MinimumVisibleMilliseconds == draft.MinimumVisibleMilliseconds &&
                              restoredSource!.World.Loading.RequiredResources.SequenceEqual(
                                  worldLoading.RequiredResources);

            var succeeded = previewValid && dryRun && imageAndVideo && reducedMotion && accessibleFallback &&
                            realStreaming && minimumDuration && otherExperiencePreserved && worldProfilePreserved &&
                            neighborPreserved && undoRedo && persistence && invalidBlocked && humanDiagnostics;
            return new StudioUx1CSmokeResult(
                succeeded,
                previewValid,
                dryRun,
                imageAndVideo,
                reducedMotion,
                accessibleFallback,
                realStreaming,
                minimumDuration,
                otherExperiencePreserved,
                worldProfilePreserved,
                neighborPreserved,
                undoRedo,
                persistence,
                invalidBlocked,
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

    private bool TryBuildAreaLoading(
        StudioAreaLoadingDraft draft,
        bool dryRun,
        out SetAreaExperienceProfileCommand? command,
        out AreaExperienceProfile? profile,
        out string message)
    {
        command = null;
        profile = null;
        message = string.Empty;
        if (History is null || Project is null || Session is null)
        {
            message = "Aucun module n’est ouvert.";
            return false;
        }

        var area = Project.Areas.FirstOrDefault(item => item.Id == draft.AreaId);
        if (area is null)
        {
            message = "La zone à configurer n’existe plus.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(draft.Title))
        {
            message = "Donnez un titre visible à l’écran de chargement.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(draft.AccessibleFallback))
        {
            message = "Décrivez le chargement pour les lecteurs d’écran et les médias indisponibles.";
            return false;
        }
        if (!string.IsNullOrWhiteSpace(draft.ImageResource) &&
            !TryResolveWorkspaceMedia(draft.ImageResource, LoadingMediaKind.Image, out _))
        {
            message = "L’image choisie n’est plus disponible dans les médias importés du module.";
            return false;
        }
        if (!string.IsNullOrWhiteSpace(draft.VideoResource) &&
            !TryResolveWorkspaceMedia(draft.VideoResource, LoadingMediaKind.Video, out _))
        {
            message = "La vidéo choisie n’est plus disponible dans les médias importés du module.";
            return false;
        }

        var localizedTips = MergeLocalizedTips(draft);
        if (localizedTips.Count == 0)
        {
            message = "Ajoutez au moins une astuce de chargement en français ou en anglais.";
            return false;
        }

        try
        {
            var source = area.RuntimePolicies.Experience;
            var loading = new AreaLoadingPresentation(
                source.Loading.Id,
                draft.Title.Trim(),
                draft.ImageResource,
                draft.VideoResource,
                localizedTips,
                draft.ShowProgress,
                draft.AccessibleFallback.Trim(),
                draft.PrefetchRequiredResources,
                draft.MinimumVisibleMilliseconds,
                "studio:ux1-c-loading-screen");
            profile = new AreaExperienceProfile(
                source.Id,
                source.SchemaVersion,
                source.AudioCues,
                source.ScatterEmitters,
                source.Voices,
                source.PatrolRoutes,
                loading,
                "studio:ux1-c-loading-screen");
            command = new SetAreaExperienceProfileCommand(
                CommandId.From(Guid.NewGuid()),
                History.State.Revision,
                area.Id,
                profile,
                dryRun);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            message = "La durée doit rester comprise entre 0 et 30 secondes, avec des textes non vides.";
            return false;
        }
        catch (ArgumentException)
        {
            message = "La présentation est incomplète. Vérifiez le titre, les astuces et le texte accessible.";
            return false;
        }
    }

    private IReadOnlyList<(string Relative, LoadingMediaKind Kind)> DiscoverLoadingMedia()
    {
        if (Session is null || !Directory.Exists(Session.RootDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(Session.RootDirectory, "*", SearchOption.AllDirectories)
            .Select(path => new
            {
                Relative = Path.GetRelativePath(Session.RootDirectory, path).Replace('\\', '/'),
                Extension = Path.GetExtension(path),
            })
            .Where(item => !item.Relative.StartsWith(".forge/", StringComparison.OrdinalIgnoreCase))
            .Select(item => LoadingImageExtensions.Contains(item.Extension)
                ? (item.Relative, LoadingMediaKind.Image)
                : LoadingVideoExtensions.Contains(item.Extension)
                    ? (item.Relative, LoadingMediaKind.Video)
                    : (item.Relative, LoadingMediaKind.AccessibleFallback))
            .Where(item => item.Item2 != LoadingMediaKind.AccessibleFallback)
            .OrderBy(item => Path.GetFileName(item.Relative).Equals("model.preview.png", StringComparison.OrdinalIgnoreCase)
                ? 0
                : 1)
            .ThenBy(item => item.Relative, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<StudioLoadingMediaOption> BuildMediaOptions(
        IReadOnlyList<(string Relative, LoadingMediaKind Kind)> files,
        LoadingMediaKind kind,
        string currentResource)
    {
        var options = new List<StudioLoadingMediaOption>
        {
            new("Aucun média", string.Empty, LoadingMediaKind.AccessibleFallback, true),
        };
        options.AddRange(files
            .Where(item => item.Kind == kind)
            .Select(item => new StudioLoadingMediaOption(
                HumanizeMediaName(item.Relative),
                item.Relative,
                kind,
                true)));
        if (!string.IsNullOrWhiteSpace(currentResource) &&
            options.All(item => !string.Equals(item.Resource, currentResource, StringComparison.OrdinalIgnoreCase)))
        {
            options.Insert(1, new StudioLoadingMediaOption(
                "Média actuel introuvable",
                currentResource,
                kind,
                false));
        }
        return options;
    }

    private bool TryResolveWorkspaceMedia(string resource, LoadingMediaKind kind, out string? absolutePath)
    {
        absolutePath = null;
        if (Session is null || string.IsNullOrWhiteSpace(resource) || Path.IsPathFullyQualified(resource) ||
            resource.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var allowed = kind == LoadingMediaKind.Image ? LoadingImageExtensions : LoadingVideoExtensions;
        if (!allowed.Contains(Path.GetExtension(resource)))
        {
            return false;
        }

        var root = Path.GetFullPath(Session.RootDirectory);
        var candidate = Path.GetFullPath(Path.Combine(root, resource.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                         Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(candidate))
        {
            return false;
        }

        absolutePath = candidate;
        return true;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> MergeLocalizedTips(
        StudioAreaLoadingDraft draft)
    {
        var values = draft.PreservedLocalizedTips
            .Where(pair => pair.Value.Any(item => !string.IsNullOrWhiteSpace(item)))
            .ToDictionary(
                pair => pair.Key.Trim().ToLowerInvariant(),
                pair => (IReadOnlyList<string>)NormalizeTips(pair.Value),
                StringComparer.OrdinalIgnoreCase);
        var french = NormalizeTips(draft.FrenchTips);
        var english = NormalizeTips(draft.EnglishTips);
        if (french.Length > 0)
        {
            values["fr"] = french;
        }
        else
        {
            values.Remove("fr");
        }
        if (english.Length > 0)
        {
            values["en"] = english;
        }
        else
        {
            values.Remove("en");
        }
        return values;
    }

    private static string[] NormalizeTips(IEnumerable<string> tips) => tips
        .Select(item => item.Trim())
        .Where(item => item.Length > 0)
        .ToArray();

    private static IReadOnlyList<string> TipsForLocale(
        IReadOnlyDictionary<string, IReadOnlyList<string>> tips,
        string locale) => tips.FirstOrDefault(pair => string.Equals(
            pair.Key,
            locale,
            StringComparison.OrdinalIgnoreCase)).Value ?? [];

    private static string BuildAreaLoadingSummary(
        AreaLoadingPresentation loading,
        LoadingPresentationFrame standard,
        LoadingPresentationFrame reduced,
        int requiredResourceCount)
    {
        var media = standard.MediaKind switch
        {
            LoadingMediaKind.Video => reduced.MediaKind == LoadingMediaKind.Image
                ? "vidéo, avec image lorsque les mouvements sont réduits"
                : "vidéo, avec repli textuel lorsque les mouvements sont réduits",
            LoadingMediaKind.Image => "image fixe",
            _ => "présentation textuelle accessible",
        };
        var progress = loading.ShowProgress ? "progression visible" : "progression masquée";
        var prefetch = loading.PrefetchRequiredResources
            ? requiredResourceCount == 0
                ? "préchargement activé, aucune ressource déclarée"
                : $"{requiredResourceCount} ressource(s) préchargée(s) par le streaming réel"
            : "préchargement désactivé pour cette présentation";
        var tipCount = loading.LocalizedTips.Sum(pair => pair.Value.Count);
        return $"{loading.Title} · {media}\n" +
               $"{tipCount} astuce(s) · {progress} · affichage minimal {loading.MinimumVisibleMilliseconds} ms\n" +
               $"{prefetch} · repli accessible toujours disponible.";
    }

    private static string HumanizeMediaName(string relative)
    {
        var file = Path.GetFileNameWithoutExtension(relative);
        var parent = Path.GetFileName(Path.GetDirectoryName(relative));
        if (file.Equals("model.preview", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(parent))
        {
            return HumanizeToken(parent);
        }
        var fileLabel = HumanizeToken(file);
        var parentLabel = HumanizeToken(parent ?? string.Empty);
        return string.IsNullOrWhiteSpace(parentLabel) ||
               string.Equals(fileLabel, parentLabel, StringComparison.OrdinalIgnoreCase)
            ? fileLabel
            : $"{fileLabel} · {parentLabel}";
    }

    private static string HumanizeResourceName(string resource)
    {
        var name = Path.GetFileNameWithoutExtension(resource);
        return string.IsNullOrWhiteSpace(name) ? "Ressource de zone" : HumanizeToken(name);
    }

    private static string HumanizeToken(string value)
    {
        var words = value.Replace('_', ' ').Replace('-', ' ').Trim();
        if (words.Length == 0)
        {
            return string.Empty;
        }
        return char.ToUpperInvariant(words[0]) + words[1..];
    }

    private void RecordAreaLoadingDiagnostic(string code, string message) => AddOperationalDiagnostic(
        code,
        message,
        target: null,
        documentPath: "zones",
        Forge.Application.Creator.CreatorDiagnosticSeverity.Error,
        "area:loading-screen");

    private static string HumanizeAreaLoading(string? code) => code switch
    {
        "command.revision_conflict" => "Le module a changé depuis l’aperçu. Prévisualisez à nouveau.",
        "experience.area_missing" => "La zone à configurer n’existe plus.",
        _ => "L’écran de chargement a été refusé. Vérifiez les médias, les astuces et le texte accessible.",
    };

    private static bool IsHumanAreaLoadingMessage(string message) =>
        !message.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains(".json", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("../", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("experience.", StringComparison.OrdinalIgnoreCase);

    private static void WriteSmokeResource(string root, string relative, byte[] content)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    private static byte[] TinyPngBytes() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    private static StudioUx1CSmokeResult FailedUx1CSmoke() =>
        new(false, false, false, false, false, false, false, false, false, false, false, false, false, false,
            false);

    private void CloseUx1C() => _approvedAreaLoading = null;

    private sealed class SmokeFileChunkLoader(string root) : IAreaChunkLoader
    {
        public Task<AreaChunkPayload> LoadAsync(string resourcePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = Path.GetFullPath(Path.Combine(root, resourcePath.Replace('/', Path.DirectorySeparatorChar)));
            var bytes = File.ReadAllBytes(path);
            return Task.FromResult(new AreaChunkPayload(resourcePath, bytes, []));
        }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
