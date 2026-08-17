using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2EnvISelectionCode = "studio.audio_scatter.selection_invalid";
    private const string Ux2EnvICueCode = "studio.audio_scatter.cue_missing";
    private const string Ux2EnvIGeometryCode = "studio.audio_scatter.geometry_invalid";
    private const string Ux2EnvIIntervalCode = "studio.audio_scatter.interval_invalid";
    private const string Ux2EnvIVolumeCode = "studio.audio_scatter.volume_invalid";
    private const string Ux2EnvITagsCode = "studio.audio_scatter.tags_invalid";

    private readonly SetAreaExperienceProfileHandler _setAreaExperienceUx2EnvI = new(new SystemClock());
    private StudioUx2EnvIDraft? _approvedAudioScatterUx2EnvI;
    private ProjectRevision? _approvedAudioScatterRevisionUx2EnvI;
    private string? _approvedAudioScatterFingerprintUx2EnvI;

    internal IReadOnlyList<StudioUx2EnvIAreaChoice> AudioScatterChoicesUx2EnvI
    {
        get
        {
            var project = Project;
            return project is null ? [] : BuildAudioScatterChoicesUx2EnvI(project);
        }
    }

    private static IReadOnlyList<StudioUx2EnvIAreaChoice> BuildAudioScatterChoicesUx2EnvI(
        ProjectDefinition project)
    {
        var duplicateNames = project.Areas.GroupBy(area => area.DisplayName,
                StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(),
                StringComparer.CurrentCultureIgnoreCase);
        return project.Areas.Select(area =>
            {
                var experience = area.RuntimePolicies.Experience;
                var display = HumanTokenUx2EnvI(area.DisplayName);
                if (duplicateNames[area.DisplayName] > 1)
                    display = $"{display} · {HumanTokenUx2EnvI(area.Slug)}";
                var cueLabel = experience.AudioCues.Count == 1
                    ? "1 cue gouverné"
                    : $"{experience.AudioCues.Count} cues gouvernés";
                var scatterLabel = experience.ScatterEmitters.Count == 1
                    ? "1 dispersion"
                    : $"{experience.ScatterEmitters.Count} dispersions";
                return new StudioUx2EnvIAreaChoice(area.Id,
                    $"{display} · {cueLabel} · {scatterLabel}",
                    experience.AudioCues.Count,
                    experience.ScatterEmitters.Count);
            })
            .OrderBy(choice => choice.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    internal StudioUx2EnvIDraft? CreateAudioScatterDraftUx2EnvI(AreaId? requestedAreaId = null)
    {
        var project = Project;
        var history = History;
        if (project is null || history is null || requestedAreaId is null) return null;
        var matches = project.Areas.Where(area => area.Id == requestedAreaId).ToArray();
        return matches.Length == 1
            ? CreateAudioScatterDraftUx2EnvI(history.State.Revision, matches[0])
            : null;
    }

    internal IReadOnlyList<StudioUx2EnvICueChoice> AudioScatterCueChoicesUx2EnvI(AreaId areaId)
    {
        var area = Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        return area is null
            ? []
            : area.RuntimePolicies.Experience.AudioCues
                .Select(item => new StudioUx2EnvICueChoice(item.CueId, HumanCueUx2EnvI(item.CueId)))
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
    }

    private static StudioUx2EnvIDraft CreateAudioScatterDraftUx2EnvI(
        ProjectRevision revision,
        AreaDefinition area)
    {
        var scatter = area.RuntimePolicies.Experience.ScatterEmitters.Select(item =>
            new StudioUx2EnvIScatterDraft(
                item.Id,
                item.CueId,
                InvariantUx2EnvI(item.Center.X),
                InvariantUx2EnvI(item.Center.Y),
                InvariantUx2EnvI(item.Center.Z),
                InvariantUx2EnvI(item.Size.X),
                InvariantUx2EnvI(item.Size.Y),
                InvariantUx2EnvI(item.Size.Z),
                item.SurfaceOnly,
                item.DeterministicSeed.ToString(CultureInfo.InvariantCulture),
                InvariantUx2EnvI(item.MinimumIntervalSeconds),
                InvariantUx2EnvI(item.MaximumIntervalSeconds),
                InvariantUx2EnvI(item.Volume),
                item.RequireNavigable,
                string.Join(", ", item.BiomeTags),
                string.Join(", ", item.ExclusionTags),
                item.Provenance)).ToArray();
        return new StudioUx2EnvIDraft(
            area.Id,
            revision,
            AudioScatterSourceFingerprintUx2EnvI(area),
            area.DisplayName,
            area.Slug,
            scatter.FirstOrDefault()?.Id,
            scatter);
    }

    internal StudioUx2EnvIPreview PreviewAudioScatterUx2EnvI(StudioUx2EnvIDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelAudioScatterApprovalUx2EnvI();
        var history = History;
        if (history is null || Project is null)
            return FailedAudioScatterPreviewUx2EnvI("Aucun module n’est ouvert.");
        if (draft.BaseRevision != history.State.Revision)
            return FailedAudioScatterPreviewUx2EnvI(
                "Le projet a changé depuis l’ouverture. Rechargez la zone avant l’aperçu.");
        if (!TryBuildAudioScatterUx2EnvI(draft, true, out var command, out var sourceArea,
                out var candidate, out var diagnostics, out var message))
            return new(false, message, string.Empty, false, new([]),
                GroupAudioScatterDiagnosticsUx2EnvI(diagnostics), [], [], [], null,
                false, false, false, true, true, true);

        var dryRun = _setAreaExperienceUx2EnvI.Handle(history.State, command!);
        if (!dryRun.Succeeded)
        {
            var commandDiagnostics = dryRun.Diagnostics.Select(item => CommandDiagnosticUx2EnvI(item,
                $"areas/{HumanTokenUx2EnvI(sourceArea!.Slug)}/experience/scatter")).ToArray();
            return new(false,
                commandDiagnostics.FirstOrDefault()?.Message ?? "La commande canonique a refusé cet aperçu.",
                string.Empty,
                false,
                BuildAudioScatterDiffUx2EnvI(sourceArea!.RuntimePolicies.Experience, candidate!),
                GroupAudioScatterDiagnosticsUx2EnvI(commandDiagnostics), [], [], [], null,
                false, false, false, true, true, true);
        }

        var diff = BuildAudioScatterDiffUx2EnvI(sourceArea!.RuntimePolicies.Experience, candidate!);
        var selected = candidate!.ScatterEmitters.FirstOrDefault(item => item.Id == draft.SelectedEmitterId)
                       ?? candidate.ScatterEmitters.FirstOrDefault();
        IReadOnlyList<StudioUx2EnvIScatterSample> samples = selected is null
            ? []
            : BuildScatterSamplesUx2EnvI(candidate, selected);
        IReadOnlyList<StudioUx2EnvIFilterProbe> probes = selected is null
            ? []
            : BuildFilterProbesUx2EnvI(candidate, selected);
        var events = BuildTypedEventPlanUx2EnvI(candidate, selected?.CueId,
            out var scriptSnapshot, out var typedEvents);
        var deterministic = selected is null || SamplesAreDeterministicUx2EnvI(candidate, selected);

        if (diff.HasChanges)
        {
            _approvedAudioScatterUx2EnvI = draft;
            _approvedAudioScatterRevisionUx2EnvI = history.State.Revision;
            _approvedAudioScatterFingerprintUx2EnvI = AudioScatterDraftFingerprintUx2EnvI(draft);
        }

        var selectedLabel = selected is null
            ? "Aucune dispersion sélectionnée; le catalogue peut rester vide."
            : $"Dispersion « {HumanCueUx2EnvI(selected.CueId)} » · {samples.Count} positions déterministes " +
              $"calculées · {probes.Count} scénarios de filtre.";
        var summary = $"{selectedLabel} {events.Count} étapes typées simulent démarrage, fin, interruption, " +
                      "état musical et snapshot. Studio n’instancie aucun lecteur audio 3D, ne charge " +
                      "aucune ressource et ne déclenche aucun handler ForgeScript; le playback et les callbacks " +
                      "réels restent au runtime Godot. La seule mutation persistante proposée concerne les " +
                      "dispersions via la commande canonique d’expérience de zone.";
        return new(true,
            diff.HasChanges
                ? "Aperçu sec validé · les dispersions ne sont pas encore appliquées."
                : "Aperçu sec validé · aucune différence persistante à appliquer.",
            summary,
            diff.HasChanges,
            diff,
            GroupAudioScatterDiagnosticsUx2EnvI([]),
            samples,
            probes,
            events,
            scriptSnapshot,
            true,
            deterministic,
            typedEvents,
            true,
            true,
            true);
    }

    internal StudioUx2EnvIActionResult ApplyApprovedAudioScatterUx2EnvI()
    {
        var draft = _approvedAudioScatterUx2EnvI;
        var history = History;
        if (draft is null || history is null || _approvedAudioScatterRevisionUx2EnvI != history.State.Revision ||
            !string.Equals(_approvedAudioScatterFingerprintUx2EnvI,
                AudioScatterDraftFingerprintUx2EnvI(draft), StringComparison.Ordinal))
        {
            CancelAudioScatterApprovalUx2EnvI();
            return new(false, "Relancez l’aperçu sec avant d’appliquer les dispersions audio.");
        }
        if (!TryBuildAudioScatterUx2EnvI(draft, false, out var command, out _, out _, out _, out var message))
        {
            CancelAudioScatterApprovalUx2EnvI();
            return new(false, message);
        }
        var dryRun = _setAreaExperienceUx2EnvI.Handle(history.State, command! with { DryRun = true });
        if (!dryRun.Succeeded)
        {
            CancelAudioScatterApprovalUx2EnvI();
            return new(false, HumanizeAudioScatterDiagnosticUx2EnvI(dryRun.Diagnostics.FirstOrDefault()?.Code));
        }
        var applied = _setAreaExperienceUx2EnvI.Handle(history.State, command!);
        CancelAudioScatterApprovalUx2EnvI();
        if (!applied.Succeeded)
            return new(false, HumanizeAudioScatterDiagnosticUx2EnvI(applied.Diagnostics.FirstOrDefault()?.Code));
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true,
            "Dispersions appliquées par la commande canonique d’expérience · une mutation annulable.");
    }

    internal void CancelAudioScatterApprovalUx2EnvI()
    {
        _approvedAudioScatterUx2EnvI = null;
        _approvedAudioScatterRevisionUx2EnvI = null;
        _approvedAudioScatterFingerprintUx2EnvI = null;
    }

    internal StudioUx2EnvIApprovalSnapshot CaptureAudioScatterApprovalUx2EnvI() => new(
        _approvedAudioScatterUx2EnvI,
        _approvedAudioScatterRevisionUx2EnvI,
        _approvedAudioScatterFingerprintUx2EnvI);

    internal void RestoreAudioScatterApprovalUx2EnvI(StudioUx2EnvIApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedAudioScatterUx2EnvI = snapshot.Draft;
        _approvedAudioScatterRevisionUx2EnvI = snapshot.Revision;
        _approvedAudioScatterFingerprintUx2EnvI = snapshot.Fingerprint;
    }

    private bool TryBuildAudioScatterUx2EnvI(
        StudioUx2EnvIDraft draft,
        bool dryRun,
        out SetAreaExperienceProfileCommand? command,
        out AreaDefinition? sourceArea,
        out AreaExperienceProfile? candidate,
        out IReadOnlyList<StudioUx2EnvIDiagnostic> diagnostics,
        out string message)
    {
        command = null;
        sourceArea = null;
        candidate = null;
        var found = new List<StudioUx2EnvIDiagnostic>();
        diagnostics = found;
        message = "La fiche de dispersion audio est invalide.";
        var history = History;
        var project = Project;
        if (history is null || project is null)
        {
            message = "Aucun module n’est ouvert.";
            return false;
        }
        if (draft.BaseRevision != history.State.Revision)
        {
            message = "Le projet a changé depuis l’ouverture de la fiche.";
            return false;
        }
        var matches = project.Areas.Where(area => area.Id == draft.AreaId).ToArray();
        if (matches.Length != 1)
        {
            message = "La zone sélectionnée n’est plus disponible.";
            return false;
        }
        sourceArea = matches[0];
        if (!string.Equals(draft.SourceFingerprint,
                AudioScatterSourceFingerprintUx2EnvI(sourceArea), StringComparison.Ordinal))
        {
            message = "La zone a changé depuis l’ouverture; ses données voisines ne seront pas réinjectées.";
            return false;
        }
        if (draft.ScatterEmitters.Select(item => item.Id).Any(id => id == Guid.Empty) ||
            draft.ScatterEmitters.Select(item => item.Id).Distinct().Count() != draft.ScatterEmitters.Count)
            found.Add(new(Ux2EnvISelectionCode, "scatter/selection",
                "Chaque dispersion doit conserver une identité interne stable et distincte."));
        if (draft.SelectedEmitterId is not null &&
            draft.ScatterEmitters.All(item => item.Id != draft.SelectedEmitterId))
            found.Add(new(Ux2EnvISelectionCode, "scatter/selection",
                "La dispersion sélectionnée n’existe plus dans la collection."));

        var sourceExperience = sourceArea.RuntimePolicies.Experience;
        var cueIds = sourceExperience.AudioCues.Select(item => item.CueId)
            .ToHashSet(StringComparer.Ordinal);
        var values = new List<SpatialScatterEmitter>();
        foreach (var item in draft.ScatterEmitters)
        {
            var path = $"scatter/{HumanCueUx2EnvI(item.CueId)}";
            if (!cueIds.Contains((item.CueId ?? string.Empty).Trim()))
                found.Add(new(Ux2EnvICueCode, $"{path}/cue",
                    "Choisissez un cue déjà gouverné dans le profil d’expérience de la zone."));
            var numeric = new[]
            {
                item.CenterX, item.CenterY, item.CenterZ,
                item.SizeX, item.SizeY, item.SizeZ,
                item.MinimumIntervalSeconds, item.MaximumIntervalSeconds, item.Volume,
            };
            var parsed = numeric.Select(value => TryFloatUx2EnvI(value, out var number)
                    ? (Valid: true, Value: number)
                    : (Valid: false, Value: 0f))
                .ToArray();
            if (parsed.Take(6).Any(value => !value.Valid) ||
                parsed.Skip(3).Take(3).Any(value => value.Valid && value.Value <= 0))
                found.Add(new(Ux2EnvIGeometryCode, $"{path}/volume",
                    "Le centre et la taille exigent des nombres finis; chaque dimension doit être strictement positive."));
            if (!parsed[6].Valid || !parsed[7].Valid || parsed[6].Value < 0 ||
                parsed[7].Value < parsed[6].Value)
                found.Add(new(Ux2EnvIIntervalCode, $"{path}/interval",
                    "Les intervalles doivent être finis, positifs ou nuls, et le maximum doit suivre le minimum."));
            if (!parsed[8].Valid || parsed[8].Value is < 0 or > 4)
                found.Add(new(Ux2EnvIVolumeCode, $"{path}/volume",
                    "Le volume doit être un nombre fini compris entre 0 et 4."));
            if (!int.TryParse(item.DeterministicSeed, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var seed))
                found.Add(new(Ux2EnvIIntervalCode, $"{path}/seed",
                    "Le seed déterministe doit être un entier signé."));
            var biomes = ParseTagsUx2EnvI(item.BiomeTags, out var biomesValid);
            var exclusions = ParseTagsUx2EnvI(item.ExclusionTags, out var exclusionsValid);
            if (!biomesValid || !exclusionsValid)
                found.Add(new(Ux2EnvITagsCode, $"{path}/filters",
                    "Les filtres utilisent des tags séparés par des virgules, sans espace interne ni séparateur technique."));
            if (string.IsNullOrWhiteSpace(item.Provenance))
                found.Add(new(Ux2EnvISelectionCode, $"{path}/provenance",
                    "La provenance de la dispersion ne peut pas être vide."));
            if (found.Any(diagnostic => diagnostic.Path.StartsWith(path, StringComparison.Ordinal))) continue;
            values.Add(new SpatialScatterEmitter(
                item.Id,
                item.CueId.Trim(),
                new Vector3Value(parsed[0].Value, parsed[1].Value, parsed[2].Value),
                new Vector3Value(parsed[3].Value, parsed[4].Value, parsed[5].Value),
                item.SurfaceOnly,
                seed,
                parsed[6].Value,
                parsed[7].Value,
                parsed[8].Value,
                item.RequireNavigable,
                biomes,
                exclusions,
                item.Provenance.Trim()));
        }
        if (found.Count > 0)
        {
            message = found[0].Message;
            return false;
        }

        try
        {
            candidate = new AreaExperienceProfile(
                sourceExperience.Id,
                sourceExperience.SchemaVersion,
                sourceExperience.AudioCues,
                values,
                sourceExperience.Voices,
                sourceExperience.PatrolRoutes,
                sourceExperience.Loading,
                sourceExperience.Provenance);
            command = new SetAreaExperienceProfileCommand(
                CommandId.From(Guid.NewGuid()),
                history.State.Revision,
                sourceArea.Id,
                candidate,
                dryRun);
            message = "Dispersions prêtes pour la commande canonique.";
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            found.Add(new(Ux2EnvIGeometryCode, "scatter",
                $"Le profil d’expérience ne peut pas être reconstruit · {exception.Message}"));
            message = found[0].Message;
            return false;
        }
    }

    private static IReadOnlyList<StudioUx2EnvIScatterSample> BuildScatterSamplesUx2EnvI(
        AreaExperienceProfile profile,
        SpatialScatterEmitter emitter)
    {
        var activeBiomes = emitter.BiomeTags.ToHashSet(StringComparer.OrdinalIgnoreCase);
        IReadOnlySet<string> noExclusions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return Enumerable.Range(0, 5).Select(sequence =>
            {
                var emission = AreaExperienceRuntimeService.ResolveScatter(profile, emitter.Id, sequence,
                    activeBiomes, noExclusions, navigable: true);
                return emission is null
                    ? new StudioUx2EnvIScatterSample(HumanCueUx2EnvI(emitter.CueId), sequence,
                        "Aucune position", "—", "—", "Bloquée par les filtres courants")
                    : new StudioUx2EnvIScatterSample(HumanCueUx2EnvI(emitter.CueId), sequence,
                        $"X {emission.Position.X:0.##} · Y {emission.Position.Y:0.##} · Z {emission.Position.Z:0.##}",
                        $"{emission.DelaySeconds:0.##} s", emission.Volume.ToString("0.##", CultureInfo.CurrentCulture),
                        emitter.SurfaceOnly ? "Surface fixe en hauteur" : "Volume complet");
            })
            .ToArray();
    }

    private static IReadOnlyList<StudioUx2EnvIFilterProbe> BuildFilterProbesUx2EnvI(
        AreaExperienceProfile profile,
        SpatialScatterEmitter emitter)
    {
        var matchingBiomes = emitter.BiomeTags.ToHashSet(StringComparer.OrdinalIgnoreCase);
        IReadOnlySet<string> noTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        IReadOnlySet<string> exclusions = emitter.ExclusionTags.Count == 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "tag-non-configuré" }
            : new HashSet<string>([emitter.ExclusionTags[0]], StringComparer.OrdinalIgnoreCase);
        StudioUx2EnvIFilterProbe Probe(string scenario, IReadOnlySet<string> biomes,
            IReadOnlySet<string> excluded, bool navigable, string explanation)
        {
            var result = AreaExperienceRuntimeService.ResolveScatter(profile, emitter.Id, 0,
                biomes, excluded, navigable);
            return new(scenario, result is null ? "Bloqué" : "Autorisé", explanation);
        }
        return
        [
            Probe("Navigation disponible", matchingBiomes, noTags, true,
                emitter.RequireNavigable ? "La projection navigable est exigée et disponible." : "Aucune projection n’est exigée."),
            Probe("Navigation indisponible", matchingBiomes, noTags, false,
                emitter.RequireNavigable ? "La politique doit bloquer cette émission." : "La politique autorise une position hors navigation."),
            Probe("Biome correspondant", matchingBiomes, noTags, true,
                emitter.BiomeTags.Count == 0 ? "Aucun filtre de biome n’est configuré." : "Au moins un tag de biome correspond."),
            Probe("Biome absent", noTags, noTags, true,
                emitter.BiomeTags.Count == 0 ? "Sans filtre de biome, l’émission reste autorisée." : "Aucun tag de biome ne correspond."),
            Probe("Exclusion active", matchingBiomes, exclusions, true,
                emitter.ExclusionTags.Count == 0 ? "Aucune exclusion n’est configurée." : "Un tag d’exclusion configuré est actif."),
        ];
    }

    private static bool SamplesAreDeterministicUx2EnvI(
        AreaExperienceProfile profile,
        SpatialScatterEmitter emitter)
    {
        var biomes = emitter.BiomeTags.ToHashSet(StringComparer.OrdinalIgnoreCase);
        IReadOnlySet<string> exclusions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return Enumerable.Range(0, 8).All(sequence =>
            AreaExperienceRuntimeService.ResolveScatter(profile, emitter.Id, sequence, biomes, exclusions, true) ==
            AreaExperienceRuntimeService.ResolveScatter(profile, emitter.Id, sequence, biomes, exclusions, true));
    }

    private static IReadOnlyList<StudioUx2EnvIEventStep> BuildTypedEventPlanUx2EnvI(
        AreaExperienceProfile profile,
        string? preferredCue,
        out StudioUx2EnvIScriptSnapshot? snapshot,
        out bool typedEvents)
    {
        snapshot = null;
        typedEvents = false;
        var cue = profile.AudioCues.FirstOrDefault(item => item.CueId == preferredCue)
                  ?? profile.AudioCues.FirstOrDefault();
        if (cue is null) return [];
        var authoredCues = profile.AudioCues;
        var authoredScatter = profile.ScatterEmitters;
        var authoredVoices = profile.Voices;
        var authoredPatrols = profile.PatrolRoutes;
        var authoredLoading = profile.Loading;
        var authoredProvenance = profile.Provenance;
        var service = new AreaExperienceScriptService(profile);
        var resolved = service.Resolve(service.Handle);
        var copiedCueIds = resolved.Values["audioCueIds"] as string[] ?? [];
        var copiedValues = copiedCueIds.SequenceEqual(profile.AudioCues.Select(item => item.CueId),
            StringComparer.Ordinal);
        if (copiedCueIds.Length > 0)
        {
            var authored = profile.AudioCues[0].CueId;
            copiedCueIds[0] = "copie-modifiée";
            copiedValues &= profile.AudioCues[0].CueId == authored;
        }
        snapshot = new StudioUx2EnvIScriptSnapshot(
            service.Handle.SchemaVersion,
            profile.AudioCues.Count,
            profile.ScatterEmitters.Count,
            profile.Voices.Count,
            profile.PatrolRoutes.Count,
            copiedValues,
            false);
        var idCounter = 0;
        Guid NextId()
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"ux2-env-i-event:{++idCounter}"));
            return new Guid(hash.AsSpan(0, 16));
        }
        var now = new DateTimeOffset(2032, 6, 12, 10, 30, 0, TimeSpan.Zero);
        var completed = service.PlayCue(cue.CueId, now, NextId);
        service.CompleteCue(completed.PlaybackId, now.AddSeconds(1), NextId);
        var interrupted = service.PlayCue(cue.CueId, now.AddSeconds(2), NextId);
        service.InterruptCue(interrupted.PlaybackId, "transition de zone", now.AddSeconds(3), NextId);
        service.SetMusicState("combat", now.AddSeconds(4), NextId);
        service.PushSnapshot("focus-dialogue", now.AddSeconds(5), NextId);
        var steps = service.State.Events.Select(item => item switch
        {
            AudioCueStarted started => new StudioUx2EnvIEventStep("Lire le cue", "AudioCueStarted",
                HumanCueUx2EnvI(started.CueId), "Lecture active", "Événement de démarrage distinct"),
            AudioCueCompleted completedEvent => new StudioUx2EnvIEventStep("Terminer le cue", "AudioCueCompleted",
                HumanCueUx2EnvI(completedEvent.CueId), "Lecture terminée", "Fin normale distincte"),
            AudioCueInterrupted interruptedEvent => new StudioUx2EnvIEventStep("Interrompre le cue",
                "AudioCueInterrupted", HumanCueUx2EnvI(interruptedEvent.CueId), "Lecture interrompue",
                HumanTokenUx2EnvI(interruptedEvent.Reason)),
            MusicStateChanged music => new StudioUx2EnvIEventStep("Changer l’état musical",
                "MusicStateChanged", "Musique", HumanTokenUx2EnvI(music.Current),
                $"Depuis {HumanTokenUx2EnvI(music.Previous)}"),
            AudioSnapshotPushed pushed => new StudioUx2EnvIEventStep("Pousser un snapshot",
                "AudioSnapshotPushed", "Mixage", "Snapshot empilé", HumanTokenUx2EnvI(pushed.Snapshot)),
            _ => new StudioUx2EnvIEventStep("Événement inconnu", "Type non reconnu", "Audio", "Ignoré",
                "Aucune revendication runtime"),
        }).ToArray();
        typedEvents = steps.Select(item => item.EventType).SequenceEqual(
        [
            "AudioCueStarted", "AudioCueCompleted", "AudioCueStarted", "AudioCueInterrupted",
            "MusicStateChanged", "AudioSnapshotPushed",
        ]);
        snapshot = snapshot with
        {
            PersistentMutationExcluded = ReferenceEquals(authoredCues, profile.AudioCues) &&
                                         ReferenceEquals(authoredScatter, profile.ScatterEmitters) &&
                                         ReferenceEquals(authoredVoices, profile.Voices) &&
                                         ReferenceEquals(authoredPatrols, profile.PatrolRoutes) &&
                                         ReferenceEquals(authoredLoading, profile.Loading) &&
                                         string.Equals(authoredProvenance, profile.Provenance,
                                             StringComparison.Ordinal),
        };
        return steps;
    }

    private static StudioUx2EnvIDiff BuildAudioScatterDiffUx2EnvI(
        AreaExperienceProfile source,
        AreaExperienceProfile candidate)
    {
        var entries = new List<string>();
        if (source.ScatterEmitters.Count != candidate.ScatterEmitters.Count)
            entries.Add($"Collection : {source.ScatterEmitters.Count} → {candidate.ScatterEmitters.Count} dispersion(s).");
        foreach (var item in candidate.ScatterEmitters)
        {
            var previous = source.ScatterEmitters.FirstOrDefault(sourceItem => sourceItem.Id == item.Id);
            if (previous is null)
            {
                entries.Add($"Ajouter une dispersion pour « {HumanCueUx2EnvI(item.CueId)} »." );
                continue;
            }
            if (!SameScatterUx2EnvI(previous, item))
                entries.Add($"Mettre à jour « {HumanCueUx2EnvI(item.CueId)} » : volume, filtres, seed, " +
                            "position ou cadence selon les champs affichés.");
        }
        foreach (var item in source.ScatterEmitters.Where(sourceItem =>
                     candidate.ScatterEmitters.All(candidateItem => candidateItem.Id != sourceItem.Id)))
            entries.Add($"Retirer la dispersion « {HumanCueUx2EnvI(item.CueId)} »." );
        return new(entries);
    }

    private static bool SameScatterUx2EnvI(SpatialScatterEmitter left, SpatialScatterEmitter right) =>
        left.Id == right.Id &&
        string.Equals(left.CueId, right.CueId, StringComparison.Ordinal) &&
        left.Center == right.Center && left.Size == right.Size &&
        left.SurfaceOnly == right.SurfaceOnly &&
        left.DeterministicSeed == right.DeterministicSeed &&
        left.MinimumIntervalSeconds.Equals(right.MinimumIntervalSeconds) &&
        left.MaximumIntervalSeconds.Equals(right.MaximumIntervalSeconds) &&
        left.Volume.Equals(right.Volume) &&
        left.RequireNavigable == right.RequireNavigable &&
        left.BiomeTags.SequenceEqual(right.BiomeTags, StringComparer.Ordinal) &&
        left.ExclusionTags.SequenceEqual(right.ExclusionTags, StringComparer.Ordinal) &&
        string.Equals(left.Provenance, right.Provenance, StringComparison.Ordinal);

    private static IReadOnlyList<StudioUx2EnvIDiagnosticGroup> GroupAudioScatterDiagnosticsUx2EnvI(
        IReadOnlyList<StudioUx2EnvIDiagnostic> diagnostics)
    {
        var definitions = new[]
        {
            (Key: "selection", Title: "Sélection et cue"),
            (Key: "geometry", Title: "Volume, surface et cadence"),
            (Key: "filters", Title: "Navigation, biomes et exclusions"),
            (Key: "command", Title: "Commande canonique"),
        };
        return definitions.Select(definition =>
        {
            var items = diagnostics.Where(item =>
                AudioScatterDiagnosticGroupUx2EnvI(item) == definition.Key).ToArray();
            return new StudioUx2EnvIDiagnosticGroup(definition.Key, definition.Title,
                items.Length == 0 ? "Valide" : "À corriger", items);
        }).ToArray();
    }

    private static string AudioScatterDiagnosticGroupUx2EnvI(StudioUx2EnvIDiagnostic diagnostic)
    {
        if (diagnostic.Code is Ux2EnvISelectionCode or Ux2EnvICueCode) return "selection";
        if (diagnostic.Code is Ux2EnvIGeometryCode or Ux2EnvIIntervalCode or Ux2EnvIVolumeCode)
            return "geometry";
        if (diagnostic.Code == Ux2EnvITagsCode) return "filters";
        return "command";
    }

    private static StudioUx2EnvIDiagnostic CommandDiagnosticUx2EnvI(
        CommandDiagnostic diagnostic,
        string fallbackPath)
    {
        var message = diagnostic.Message ?? string.Empty;
        var separator = message.IndexOf(" · ", StringComparison.Ordinal);
        var path = separator > 0 ? message[..separator] : fallbackPath;
        return new(diagnostic.Code, path,
            separator > 0 ? message[(separator + 3)..] : HumanizeAudioScatterDiagnosticUx2EnvI(diagnostic.Code));
    }

    private static string HumanizeAudioScatterDiagnosticUx2EnvI(string? code) => code switch
    {
        null or "" => "Aucun diagnostic.",
        "command.revision_conflict" => "Le projet a changé; rechargez la zone et relancez l’aperçu.",
        "experience.area_missing" => "La zone sélectionnée n’existe plus.",
        _ => "La validation canonique a refusé ce profil d’expérience audio.",
    };

    private static StudioUx2EnvIPreview FailedAudioScatterPreviewUx2EnvI(string message) => new(
        false, message, string.Empty, false, new([]), GroupAudioScatterDiagnosticsUx2EnvI([]),
        [], [], [], null, false, false, false, true, true, true);

    private static string AudioScatterSourceFingerprintUx2EnvI(AreaDefinition area)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            writer.Write(AreaJsonSerializer.Serialize(area));
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static string AudioScatterDraftFingerprintUx2EnvI(StudioUx2EnvIDraft draft)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(draft.AreaId.ToString());
            writer.Write(draft.BaseRevision.Value);
            writer.Write(draft.SourceFingerprint ?? string.Empty);
            writer.Write(draft.AreaDisplayName ?? string.Empty);
            writer.Write(draft.AreaSlug ?? string.Empty);
            writer.Write(draft.SelectedEmitterId.HasValue);
            if (draft.SelectedEmitterId is { } selected) writer.Write(selected.ToByteArray());
            writer.Write(draft.ScatterEmitters.Count);
            foreach (var item in draft.ScatterEmitters)
            {
                writer.Write(item.Id.ToByteArray());
                writer.Write(item.CueId ?? string.Empty);
                writer.Write(item.CenterX ?? string.Empty);
                writer.Write(item.CenterY ?? string.Empty);
                writer.Write(item.CenterZ ?? string.Empty);
                writer.Write(item.SizeX ?? string.Empty);
                writer.Write(item.SizeY ?? string.Empty);
                writer.Write(item.SizeZ ?? string.Empty);
                writer.Write(item.SurfaceOnly);
                writer.Write(item.DeterministicSeed ?? string.Empty);
                writer.Write(item.MinimumIntervalSeconds ?? string.Empty);
                writer.Write(item.MaximumIntervalSeconds ?? string.Empty);
                writer.Write(item.Volume ?? string.Empty);
                writer.Write(item.RequireNavigable);
                writer.Write(item.BiomeTags ?? string.Empty);
                writer.Write(item.ExclusionTags ?? string.Empty);
                writer.Write(item.Provenance ?? string.Empty);
            }
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static bool TryFloatUx2EnvI(string? text, out float value)
    {
        value = 0;
        var normalized = (text ?? string.Empty).Trim();
        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
            double.IsFinite(parsed) && parsed is >= -float.MaxValue and <= float.MaxValue)
        {
            value = (float)parsed;
            return float.IsFinite(value);
        }
        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed) ||
            !double.IsFinite(parsed) || parsed is < -float.MaxValue or > float.MaxValue) return false;
        value = (float)parsed;
        return float.IsFinite(value);
    }

    private static IReadOnlyList<string> ParseTagsUx2EnvI(string? text, out bool valid)
    {
        var values = (text ?? string.Empty).Split(',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
        valid = values.All(value => value.Length <= 64 && !value.Any(char.IsWhiteSpace) &&
                                    !value.Contains('|') && !value.Contains(';'));
        return values;
    }

    private static string InvariantUx2EnvI(float value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    private static string HumanTokenUx2EnvI(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(text) || ContainsTechnicalIdentifierUx2EnvI(text)
            ? "Valeur conservée"
            : text;
    }

    private static string HumanCueUx2EnvI(string? cue)
    {
        var text = HumanTokenUx2EnvI(cue);
        return text.Replace('.', ' ').Replace('_', ' ').Replace('-', ' ');
    }

    private static bool ContainsTechnicalIdentifierUx2EnvI(string text) =>
        System.Text.RegularExpressions.Regex.IsMatch(text,
            "(?i)(?:[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}|[0-9a-f]{32})") ||
        text.Contains("areas/", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("forge://", StringComparison.OrdinalIgnoreCase);

    private static ProjectDefinition ReplaceAreaUx2EnvI(
        ProjectDefinition project,
        AreaId areaId,
        AreaDefinition replacement) => new(
        project.Id,
        project.Title,
        project.Module,
        project.Blueprints,
        project.Areas.Select(item => item.Id == areaId ? replacement : item).ToArray(),
        project.Dialogues,
        project.Quests);

    internal static ProjectDefinition CreateUx2EnvIContractProject(ProjectDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var real = source.Areas.FirstOrDefault(area =>
            area.RuntimePolicies.Experience.AudioCues.Count > 0 &&
            area.RuntimePolicies.Experience.ScatterEmitters.Count > 0);
        if (real is not null) return source;
        var area = source.Areas.First();
        var policies = area.RuntimePolicies;
        var experience = policies.Experience;
        var cue = new AudioCueDefinition(
            Guid.Parse("a0d10170-0000-4000-8000-000000000001"),
            "ambiance.foret",
            string.Empty,
            "Ambiance",
            .8f,
            false,
            string.Empty,
            string.Empty,
            "1.0",
            "studio:ux2-env-i:evidence");
        var surface = new SpatialScatterEmitter(
            Guid.Parse("a0d10170-0000-4000-8000-000000000002"),
            cue.CueId,
            new Vector3Value(4, 1.5f, -3),
            new Vector3Value(24, 6, 18),
            true,
            1701,
            4,
            11,
            .72f,
            true,
            ["forest"],
            ["combat"],
            "studio:ux2-env-i:evidence");
        var volume = new SpatialScatterEmitter(
            Guid.Parse("a0d10170-0000-4000-8000-000000000003"),
            cue.CueId,
            new Vector3Value(-8, 5, 6),
            new Vector3Value(12, 10, 16),
            false,
            1702,
            2,
            7,
            .5f,
            false,
            [],
            ["silence"],
            "studio:ux2-env-i:evidence");
        var candidateExperience = new AreaExperienceProfile(
            experience.Id,
            experience.SchemaVersion,
            [cue],
            [surface, volume],
            experience.Voices,
            experience.PatrolRoutes,
            experience.Loading,
            experience.Provenance);
        var candidatePolicies = new AreaRuntimePolicyProfile(
            policies.Id,
            policies.SchemaVersion,
            policies.Heartbeats,
            policies.Rest,
            policies.Provenance,
            policies.Cinematics,
            policies.Rules,
            candidateExperience);
        return ReplaceAreaUx2EnvI(source, area.Id, area.WithRuntimePolicies(candidatePolicies));
    }

    internal StudioUx2EnvISmokeResult RunUx2EnvIContractSmoke()
    {
        var sourceProject = Project;
        if (sourceProject is null || sourceProject.Areas.Count == 0) return FailedUx2EnvISmoke();
        var realModuleScatter = sourceProject.Areas.Any(area =>
            area.RuntimePolicies.Experience.AudioCues.Count > 0 &&
            area.RuntimePolicies.Experience.ScatterEmitters.Count > 0);
        var contractProject = CreateUx2EnvIContractProject(sourceProject);
        var target = contractProject.Areas.First(area =>
            area.RuntimePolicies.Experience.AudioCues.Count > 0 &&
            area.RuntimePolicies.Experience.ScatterEmitters.Count > 0);
        var tempRoot = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux2-env-i-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        var cleanup = false;
        try
        {
            var created = ProjectSession.Create(tempRoot, contractProject);
            if (!created.Succeeded || !editing.Open(tempRoot, target.Id.ToString()).Succeeded)
                return FailedUx2EnvISmoke();
            var history = editing.History!;
            var explicitSelection = editing.CreateAudioScatterDraftUx2EnvI() is null &&
                                    editing.CreateAudioScatterDraftUx2EnvI(target.Id) is not null;
            var baseDraft = editing.CreateAudioScatterDraftUx2EnvI(target.Id)!;
            var first = baseDraft.ScatterEmitters[0];

            var invalidCue = editing.PreviewAudioScatterUx2EnvI(baseDraft with
            {
                ScatterEmitters = [first with { CueId = "cue.absent" }, .. baseDraft.ScatterEmitters.Skip(1)],
            });
            var invalidGeometry = editing.PreviewAudioScatterUx2EnvI(baseDraft with
            {
                ScatterEmitters = [first with { SizeX = "0" }, .. baseDraft.ScatterEmitters.Skip(1)],
            });
            var invalidInterval = editing.PreviewAudioScatterUx2EnvI(baseDraft with
            {
                ScatterEmitters = [first with { MinimumIntervalSeconds = "9", MaximumIntervalSeconds = "2" },
                    .. baseDraft.ScatterEmitters.Skip(1)],
            });
            var nonFinite = editing.PreviewAudioScatterUx2EnvI(baseDraft with
            {
                ScatterEmitters = [first with { CenterY = "NaN", MinimumIntervalSeconds = "Infinity" },
                    .. baseDraft.ScatterEmitters.Skip(1)],
            });
            var invalidVolume = editing.PreviewAudioScatterUx2EnvI(baseDraft with
            {
                ScatterEmitters = [first with { Volume = "4.5" }, .. baseDraft.ScatterEmitters.Skip(1)],
            });

            var projectBeforeDryRun = editing.Project;
            var revisionBeforeDryRun = history.State.Revision;
            var journalBeforeDryRun = history.Journal.ToArray();
            var dirtyBeforeDryRun = editing.IsDirty;
            var canUndoBeforeDryRun = history.CanUndo;
            var canRedoBeforeDryRun = history.CanRedo;
            var dryDraft = baseDraft with
            {
                ScatterEmitters = [first with
                {
                    CenterX = "4.125",
                    CenterZ = "-2.75",
                    SizeX = "25.5",
                    SurfaceOnly = true,
                    DeterministicSeed = "1817",
                    MinimumIntervalSeconds = "3.25",
                    MaximumIntervalSeconds = "12.75",
                    Volume = "0.81",
                    RequireNavigable = true,
                    BiomeTags = "forest, ruins",
                    ExclusionTags = "combat, silence",
                }, .. baseDraft.ScatterEmitters.Skip(1)],
            };
            var dryPreview = editing.PreviewAudioScatterUx2EnvI(dryDraft);
            var dryStatePreserved = ReferenceEquals(projectBeforeDryRun, editing.Project) &&
                                    history.State.Revision == revisionBeforeDryRun &&
                                    history.Journal.SequenceEqual(journalBeforeDryRun) &&
                                    editing.IsDirty == dirtyBeforeDryRun &&
                                    history.CanUndo == canUndoBeforeDryRun && history.CanRedo == canRedoBeforeDryRun;
            var canonicalDryRun = dryPreview is { Succeeded: true, CanApply: true, CanonicalDryRun: true } &&
                                  dryStatePreserved;
            var deterministicScatter = dryPreview.DeterministicScatter && dryPreview.Samples.Count == 5 &&
                                       dryPreview.Samples.Select(item => item.Position).Distinct().Count() > 1;
            var surfaceAndVolume = dryDraft.ScatterEmitters.Any(item => item.SurfaceOnly) &&
                                   dryDraft.ScatterEmitters.Any(item => !item.SurfaceOnly) &&
                                   dryPreview.Samples.All(item => item.FilterState.Contains("Surface", StringComparison.Ordinal));
            var filters = dryPreview.FilterProbes.Count == 5 &&
                          dryPreview.FilterProbes.Any(item => item.Scenario == "Navigation indisponible" &&
                                                              item.Result == "Bloqué") &&
                          dryPreview.FilterProbes.Any(item => item.Scenario == "Biome absent" &&
                                                              item.Result == "Bloqué") &&
                          dryPreview.FilterProbes.Any(item => item.Scenario == "Exclusion active" &&
                                                              item.Result == "Bloqué");
            var typedEvents = dryPreview.TypedEvents && dryPreview.Events.Count == 6 &&
                              dryPreview.Events.Select(item => item.EventType).Distinct().Count() == 5;
            var scriptSnapshot = dryPreview.ScriptSnapshot is
            {
                CopiedValues: true,
                PersistentMutationExcluded: true,
                CueCount: > 0,
                ScatterCount: > 0,
            };

            var staleDraft = editing.CreateAudioScatterDraftUx2EnvI(target.Id)!;
            var staleMutation = ApplyUx2EnvIRevisionMutation(editing, target.Id, "draft");
            var stalePreview = editing.PreviewAudioScatterUx2EnvI(staleDraft);
            var stalePreviewBlocked = staleMutation && !stalePreview.Succeeded && editing.Undo().Succeeded;
            var staleApprovalDraft = editing.CreateAudioScatterDraftUx2EnvI(target.Id)!;
            var staleItem = staleApprovalDraft.ScatterEmitters[0];
            staleApprovalDraft = staleApprovalDraft with
            {
                ScatterEmitters = [staleItem with { Volume = "0.82" },
                    .. staleApprovalDraft.ScatterEmitters.Skip(1)],
            };
            var staleApprovalPreview = editing.PreviewAudioScatterUx2EnvI(staleApprovalDraft);
            var staleApprovalMutation = ApplyUx2EnvIRevisionMutation(editing, target.Id, "approval");
            var staleApprovalApply = editing.ApplyApprovedAudioScatterUx2EnvI();
            var staleApprovalBlocked = staleApprovalPreview.Succeeded && staleApprovalMutation &&
                                       !staleApprovalApply.Succeeded && editing.Undo().Succeeded;
            var staleRevisionBlocked = stalePreviewBlocked && staleApprovalBlocked;

            var fingerprintDraft = editing.CreateAudioScatterDraftUx2EnvI(target.Id)!;
            var fingerprintItem = fingerprintDraft.ScatterEmitters[0];
            fingerprintDraft = fingerprintDraft with
            {
                ScatterEmitters = [fingerprintItem with { Volume = "0.83" },
                    .. fingerprintDraft.ScatterEmitters.Skip(1)],
            };
            var fingerprintPreview = editing.PreviewAudioScatterUx2EnvI(fingerprintDraft);
            var approval = editing.CaptureAudioScatterApprovalUx2EnvI();
            var alteredApprovalDraft = approval.Draft!;
            var alteredItem = alteredApprovalDraft.ScatterEmitters[0];
            editing.RestoreAudioScatterApprovalUx2EnvI(approval with
            {
                Draft = alteredApprovalDraft with
                {
                    ScatterEmitters = [alteredItem with { Volume = "0.84" },
                        .. alteredApprovalDraft.ScatterEmitters.Skip(1)],
                },
            });
            var fingerprintApply = editing.ApplyApprovedAudioScatterUx2EnvI();
            var approvalFingerprintBlocked = fingerprintPreview.Succeeded && !fingerprintApply.Succeeded;

            var sourceState = editing.Project!;
            var sourceTarget = sourceState.Areas.Single(area => area.Id == target.Id);
            var sourcePolicies = sourceTarget.RuntimePolicies;
            var sourceExperience = sourcePolicies.Experience;
            var fresh = editing.CreateAudioScatterDraftUx2EnvI(target.Id)!;
            var freshItem = fresh.ScatterEmitters[0];
            fresh = fresh with
            {
                ScatterEmitters = [freshItem with
                {
                    CenterX = "7.125",
                    CenterY = "2.25",
                    CenterZ = "-4.5",
                    SizeX = "28.25",
                    SizeY = "7.5",
                    SizeZ = "19.75",
                    SurfaceOnly = false,
                    DeterministicSeed = "1919",
                    MinimumIntervalSeconds = "3.5",
                    MaximumIntervalSeconds = "13.25",
                    Volume = "0.91",
                    RequireNavigable = true,
                    BiomeTags = "forest, ruins",
                    ExclusionTags = "combat, silence",
                }, .. fresh.ScatterEmitters.Skip(1)],
            };
            var preview = editing.PreviewAudioScatterUx2EnvI(fresh);
            var apply = editing.ApplyApprovedAudioScatterUx2EnvI();
            var finalProject = editing.Project!;
            var finalTarget = finalProject.Areas.Single(area => area.Id == target.Id);
            var finalPolicies = finalTarget.RuntimePolicies;
            var finalExperience = finalPolicies.Experience;
            var finalItem = finalExperience.ScatterEmitters.Single(item => item.Id == freshItem.Id);
            var journalTail = editing.History!.Journal.LastOrDefault();
            var applied = preview.Succeeded && preview.CanApply && apply.Succeeded &&
                          journalTail?.CommandName == "set_area_experience_profile" &&
                          finalItem.Center == new Vector3Value(7.125f, 2.25f, -4.5f) &&
                          finalItem.Size == new Vector3Value(28.25f, 7.5f, 19.75f) &&
                          !finalItem.SurfaceOnly && finalItem.DeterministicSeed == 1919 &&
                          finalItem.MinimumIntervalSeconds == 3.5f && finalItem.MaximumIntervalSeconds == 13.25f &&
                          finalItem.Volume == .91f && finalItem.RequireNavigable &&
                          finalItem.BiomeTags.SequenceEqual(["forest", "ruins"]) &&
                          finalItem.ExclusionTags.SequenceEqual(["combat", "silence"]);

            var experienceTailsPreserved =
                sourceExperience.Id == finalExperience.Id &&
                sourceExperience.SchemaVersion == finalExperience.SchemaVersion &&
                sourceExperience.AudioCues.SequenceEqual(finalExperience.AudioCues) &&
                sourceExperience.Voices.SequenceEqual(finalExperience.Voices) &&
                sourceExperience.PatrolRoutes.SequenceEqual(finalExperience.PatrolRoutes) &&
                ReferenceEquals(sourceExperience.Loading, finalExperience.Loading) &&
                string.Equals(sourceExperience.Provenance, finalExperience.Provenance, StringComparison.Ordinal);
            var runtimePolicyTailsPreserved =
                sourcePolicies.Id == finalPolicies.Id &&
                sourcePolicies.SchemaVersion == finalPolicies.SchemaVersion &&
                sourcePolicies.Heartbeats.SequenceEqual(finalPolicies.Heartbeats) &&
                ReferenceEquals(sourcePolicies.Rest, finalPolicies.Rest) &&
                ReferenceEquals(sourcePolicies.Cinematics, finalPolicies.Cinematics) &&
                ReferenceEquals(sourcePolicies.Rules, finalPolicies.Rules) &&
                finalPolicies.Provenance == "experience-command";
            var expectedPolicies = new AreaRuntimePolicyProfile(
                sourcePolicies.Id,
                sourcePolicies.SchemaVersion,
                sourcePolicies.Heartbeats,
                sourcePolicies.Rest,
                "experience-command",
                sourcePolicies.Cinematics,
                sourcePolicies.Rules,
                finalExperience);
            var expectedArea = sourceTarget.WithRuntimePolicies(expectedPolicies);
            var areaNeighborsPreserved = string.Equals(
                AreaJsonSerializer.Serialize(expectedArea),
                AreaJsonSerializer.Serialize(finalTarget),
                StringComparison.Ordinal);
            var projectNeighborsPreserved = ReferenceEquals(sourceState.Module, finalProject.Module) &&
                                            sourceState.Blueprints.SequenceEqual(finalProject.Blueprints) &&
                                            sourceState.Dialogues.SequenceEqual(finalProject.Dialogues) &&
                                            sourceState.Quests.SequenceEqual(finalProject.Quests) &&
                                            sourceState.Areas.Where(area => area.Id != target.Id).All(sourceNeighbor =>
                                                ReferenceEquals(sourceNeighbor, finalProject.Areas.Single(area =>
                                                    area.Id == sourceNeighbor.Id)));

            var finalJson = ProjectJsonSerializer.Serialize(finalProject);
            var undo = editing.Undo();
            var undone = undo.Succeeded && ReferenceEquals(sourceState, editing.Project);
            var redo = editing.Redo();
            var redone = redo.Succeeded && ReferenceEquals(finalProject, editing.Project);
            var applyUndoRedo = applied && undone && redone;
            var save = editing.Save();
            var reopen = reopened.Open(tempRoot, target.Id.ToString());
            var persistence = save.Succeeded && reopen.Succeeded && reopened.Project is not null &&
                              string.Equals(finalJson, ProjectJsonSerializer.Serialize(reopened.Project!),
                                  StringComparison.Ordinal);
            var structuredDiagnostics = invalidCue.Groups.Count == 4 && invalidGeometry.Groups.Count == 4 &&
                                        invalidInterval.Groups.Count == 4 && nonFinite.Groups.Count == 4 &&
                                        invalidVolume.Groups.Count == 4 && preview.Groups.Count == 4;
            var humanText = !ContainsTechnicalIdentifierUx2EnvI(
                string.Join(' ', editing.AudioScatterChoicesUx2EnvI.Select(choice => choice.Label)) + " " +
                preview.Message + " " + preview.Summary + " " + string.Join(' ', preview.Diff.Entries) + " " +
                string.Join(' ', preview.Events.Select(item => item.Operation + " " + item.RuntimeState)));
            var boundedRuntime = preview is
            {
                PlayerInstantiationSkipped: true,
                ScriptDispatchSkipped: true,
                RuntimeClaimsBounded: true,
            } && preview.Summary.Contains("aucun lecteur audio 3D", StringComparison.Ordinal) &&
                 preview.Summary.Contains("aucun handler ForgeScript", StringComparison.Ordinal);

            editing.Close();
            reopened.Close();
            cleanup = DeleteUx2EnvITemporaryWorkspace(tempRoot);
            var succeeded = cleanup && explicitSelection && !invalidCue.Succeeded && !invalidGeometry.Succeeded &&
                            !invalidInterval.Succeeded && !nonFinite.Succeeded && !invalidVolume.Succeeded &&
                            canonicalDryRun && deterministicScatter && surfaceAndVolume && filters && typedEvents &&
                            scriptSnapshot && staleRevisionBlocked && approvalFingerprintBlocked && applyUndoRedo &&
                            persistence && experienceTailsPreserved && runtimePolicyTailsPreserved &&
                            areaNeighborsPreserved && projectNeighborsPreserved && structuredDiagnostics &&
                            humanText && boundedRuntime;
            return new StudioUx2EnvISmokeResult(
                succeeded,
                explicitSelection,
                realModuleScatter,
                !invalidCue.Succeeded,
                !invalidGeometry.Succeeded,
                !invalidInterval.Succeeded,
                !nonFinite.Succeeded,
                !invalidVolume.Succeeded,
                canonicalDryRun,
                deterministicScatter,
                surfaceAndVolume,
                filters,
                typedEvents,
                scriptSnapshot,
                staleRevisionBlocked,
                approvalFingerprintBlocked,
                applyUndoRedo,
                persistence,
                experienceTailsPreserved,
                runtimePolicyTailsPreserved,
                areaNeighborsPreserved,
                projectNeighborsPreserved,
                structuredDiagnostics,
                humanText,
                boundedRuntime,
                cleanup);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_ENV_I_CONTRACT_FAILED type={exception.GetType().Name} " +
                $"message={exception.Message}");
            return FailedUx2EnvISmoke();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            cleanup = DeleteUx2EnvITemporaryWorkspace(tempRoot);
            if (!cleanup) Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_ENV_I_CLEANUP_FAILED");
        }
    }

    private static bool ApplyUx2EnvIRevisionMutation(
        StudioSessionController controller,
        AreaId areaId,
        string marker)
    {
        var history = controller.History;
        var area = controller.Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (history is null || area is null) return false;
        var source = area.RuntimePolicies.Experience;
        var candidate = new AreaExperienceProfile(
            source.Id,
            source.SchemaVersion,
            source.AudioCues,
            source.ScatterEmitters,
            source.Voices,
            source.PatrolRoutes,
            source.Loading,
            $"{source.Provenance}:{marker}");
        var result = controller._setAreaExperienceUx2EnvI.Handle(history.State,
            new SetAreaExperienceProfileCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                area.Id, candidate, false));
        if (!result.Succeeded) return false;
        history.Record(result);
        return true;
    }

    internal static bool DeleteUx2EnvITemporaryWorkspace(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return true;
        try
        {
            var root = Path.GetFullPath(directory);
            var temp = Path.GetFullPath(Path.GetTempPath());
            var prefix = temp.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                         Path.DirectorySeparatorChar;
            var leaf = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));
            if (!root.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !leaf.StartsWith("openrpg-forge-studio-ux2-env-i-", StringComparison.Ordinal)) return false;
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            return !Directory.Exists(root);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static StudioUx2EnvISmokeResult FailedUx2EnvISmoke() => new(
        false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false);
}
