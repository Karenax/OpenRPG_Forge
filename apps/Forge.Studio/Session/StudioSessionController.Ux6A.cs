using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Gameplay;
using Forge.Application.Projects;
using Forge.Application.Rules;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Gameplay;
using Forge.Domain.Projects;
using Forge.Domain.Rules;
using Forge.Domain.RuntimePolicies;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux6ASelectionCode = "studio.rules.selection_required";
    private const string Ux6ASourceCode = "studio.rules.source_changed";
    private const string Ux6ADifficultyCode = "studio.rules.difficulty_invalid";
    private const string Ux6AMutatorCode = "studio.rules.mutator_invalid";
    private const string Ux6APluginCode = "studio.rules.plugin_invalid";
    private const string Ux6AWorldTimeCode = "studio.rules.world_time_invalid";
    private const string Ux6ARulesProvenance = "studio:ux6-a:rules";
    private const string Ux6AWorldProvenance = "studio:ux6-a:world-time";

    private readonly SetRulesProfilesHandler _setRulesUx6A = new(new SystemClock());
    private readonly SetGameplayControlProfileHandler _setGameplayUx6A = new(new SystemClock());
    private StudioUx6ADraft? _approvedRulesUx6A;
    private ProjectRevision? _approvedRulesRevisionUx6A;
    private string? _approvedRulesFingerprintUx6A;
    private StudioUx6ADraft? _approvedWorldUx6A;
    private ProjectRevision? _approvedWorldRevisionUx6A;
    private string? _approvedWorldFingerprintUx6A;

    internal IReadOnlyList<StudioUx6AProfileChoice> RulesWorldChoicesUx6A
    {
        get
        {
            var project = Project;
            if (project is null) return [];
            return project.Areas.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Select(area => new StudioUx6AProfileChoice(
                    $"{HumanUx6A(project.Module.DisplayName)} · contexte {HumanUx6A(area.DisplayName)}", area.Id))
                .ToArray();
        }
    }

    internal StudioUx6ADraft? CreateRulesWorldDraftUx6A(AreaId? contextAreaId, bool explicitlySelected = false)
    {
        var project = Project;
        var history = History;
        if (!explicitlySelected || project is null || history is null || contextAreaId is null) return null;
        var area = project.Areas.SingleOrDefault(item => item.Id == contextAreaId);
        if (area is null) return null;
        var rules = project.Module.RuntimePolicies.Rules;
        var gameplay = project.Module.RuntimePolicies.Gameplay;
        var world = gameplay.Simulation.WorldTime;
        var consumedScaleKeys = new HashSet<string>(StringComparer.Ordinal);
        var scales = new List<StudioUx6AAreaScaleDraft>();
        foreach (var candidateArea in project.Areas.OrderBy(item => item.DisplayName,
                     StringComparer.CurrentCultureIgnoreCase))
        {
            var key = new[] { candidateArea.Slug, candidateArea.Id.ToString() }
                .FirstOrDefault(world.LocalTimeScales.ContainsKey);
            var enabled = key is not null;
            if (key is not null) consumedScaleKeys.Add(key);
            scales.Add(new StudioUx6AAreaScaleDraft(
                key ?? candidateArea.Slug,
                candidateArea.DisplayName,
                key is not null ? world.LocalTimeScales[key] : 1,
                enabled,
                false));
        }
        var protectedIndex = 0;
        foreach (var item in world.LocalTimeScales.Where(item => !consumedScaleKeys.Contains(item.Key))
                     .OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            protectedIndex++;
            scales.Add(new StudioUx6AAreaScaleDraft(item.Key,
                $"Zone héritée protégée {protectedIndex}", item.Value, true, true));
        }

        return new StudioUx6ADraft(
            history.State.Revision,
            SourceFingerprintUx6A(project),
            area.Id,
            rules,
            rules.Difficulties.Select(item => new StudioUx6ADifficultyDraft(
                item.Id, item.SchemaVersion, item.Key, item.DisplayName, item.PlayerDamageMultiplier,
                item.IncomingDamageMultiplier, item.AiIntensityMultiplier, item.EconomyMultiplier,
                item.RestRecoveryMultiplier, item.SaveRestrictions, item.FriendlyFire,
                item.Accessibility.PauseAssist, item.Accessibility.TelegraphAssist,
                item.Accessibility.TimingWindowMultiplier, item.Accessibility.PerceptionCueMultiplier,
                item.Provenance)).ToArray(),
            rules.ActiveDifficultyKey,
            rules.Mutators.Mutators.Select(item => new StudioUx6AMutatorDraft(
                item.Id, item.Key, item.DisplayName, item.Enabled,
                item.ScalarOverrides.ToDictionary(entry => entry.Key, entry => entry.Value,
                    StringComparer.Ordinal),
                item.Tags.ToArray())).ToArray(),
            rules.Plugins.Enabled,
            rules.Plugins.RequiredAtRuntime,
            rules.Plugins.ActivePackageKey,
            rules.Plugins.Packages.Select(item => new StudioUx6APluginPackageDraft(
                item.Id, item.SchemaVersion, item.Key, item.Version, item.ContentPackageId,
                item.Attributes.ToArray(), item.Effects.ToArray(), item.Abilities.ToArray(),
                item.Inventories.ToArray(), item.ProgressionTracks.ToArray(), item.UiPanels.ToArray(),
                item.Provenance)).ToArray(),
            gameplay,
            world.Enabled,
            world.Required,
            world.Calendar.DaysPerMonth,
            world.Calendar.MonthsPerYear,
            world.Calendar.HoursPerDay,
            world.Calendar.MinutesPerHour,
            world.InitialClock.TotalMinutes,
            world.InitialClock.TimeScale,
            world.InitialClock.Paused,
            world.InitialClock.PauseDomain,
            world.InitialClock.Revision,
            scales,
            world.PauseDomains.ToArray(),
            world.Schedules.Select(item => new StudioUx6AScheduleDraft(
                item.Id, item.SchemaVersion, item.Provenance, item.Key, item.MinuteOfDay,
                item.DaysOfMonth.ToArray(), item.EventName, item.PauseDomain,
                item.ServerAuthoritative)).ToArray());
    }

    internal StudioUx6ABlockPreview PreviewRulesUx6A(StudioUx6ADraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelRulesApprovalUx6A();
        if (!TryGetCurrentUx6A(draft, out var history, out var project, out var area, out var sourceFailure))
            return sourceFailure!;
        if (!TryBuildRulesCandidateUx6A(draft, out var candidate, out var diagnostic))
            return FailedPreviewUx6A(diagnostic?.Message ?? "Le profil de règles est invalide.", diagnostic!);

        var command = new SetRulesProfilesCommand(CommandId.From(Guid.NewGuid()), history!.State.Revision,
            candidate!, area!.Id, area.RuntimePolicies.Rules, DryRun: true);
        var dryRun = _setRulesUx6A.Handle(history.State, command);
        if (!dryRun.Succeeded)
            return FailedCommandUx6A(dryRun, "module/runtimePolicies/rules");

        var rulesCandidate = candidate!;
        var diff = BuildRulesDiffUx6A(project!.Module.RuntimePolicies.Rules, rulesCandidate);
        var resolved = RulesRuntimeService.ResolveDifficulty(rulesCandidate);
        var activePackage = rulesCandidate.Plugins.Packages.SingleOrDefault(item =>
            item.Key == rulesCandidate.Plugins.ActivePackageKey);
        var present = RulesetPluginRuntimeService.Resolve(rulesCandidate.Plugins,
            activePackage is null ? [] : [activePackage.ContentPackageId]);
        var absent = RulesetPluginRuntimeService.Resolve(rulesCandidate.Plugins, []);
        var runtime = new[]
        {
            $"Difficulté effective · {HumanUx6A(resolved.DisplayName)} · dégâts joueur {resolved.PlayerDamageMultiplier:0.###}× · dégâts reçus {resolved.IncomingDamageMultiplier:0.###}×",
            $"IA {resolved.AiIntensityMultiplier:0.###}× · économie {resolved.EconomyMultiplier:0.###}× · repos {resolved.RestRecoveryMultiplier:0.###}× · progression {resolved.ProgressionMultiplier:0.###}×",
            resolved.EnabledMutators.Count == 0
                ? "Aucun mutateur activé."
                : $"Mutateurs actifs · {string.Join(", ", resolved.EnabledMutators.Select(HumanUx6A))}",
            present.Succeeded && present.ExtensionPresent
                ? $"Simulation avec contenu déclaré présent · package {HumanUx6A(present.Registration!.Key)} résolu."
                : "Simulation avec contenu déclaré présent · aucune extension active.",
            absent.Succeeded
                ? "Simulation sans contenu installé · extension optionnelle ignorée proprement."
                : $"Simulation sans contenu installé · refus attendu {absent.DiagnosticCode}.",
            "Aucun package natif n’est chargé et aucune règle de jeu n’est exécutée dans Studio.",
        };
        if (diff.Count > 0)
        {
            _approvedRulesUx6A = draft;
            _approvedRulesRevisionUx6A = history.State.Revision;
            _approvedRulesFingerprintUx6A = RulesCandidateFingerprintUx6A(project, draft);
        }
        return new(true, diff.Count > 0,
            diff.Count > 0
                ? "Aperçu sec des règles validé · aucune mutation appliquée."
                : "Aperçu sec validé · les règles sont déjà identiques.",
            "Remplacement canonique des difficultés, mutateurs et plugins; les règles de la zone de contexte restent inchangées.",
            diff, [], runtime, true, true);
    }

    internal StudioUx6ABlockPreview PreviewWorldTimeUx6A(StudioUx6ADraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelWorldApprovalUx6A();
        if (!TryGetCurrentUx6A(draft, out var history, out var project, out _, out var sourceFailure))
            return sourceFailure!;
        if (!TryBuildGameplayCandidateUx6A(draft, out var candidate, out var diagnostic))
            return FailedPreviewUx6A(diagnostic?.Message ?? "Le temps du monde est invalide.", diagnostic!);

        var command = new SetGameplayControlProfileCommand(CommandId.From(Guid.NewGuid()),
            history!.State.Revision, candidate!, DryRun: true);
        var dryRun = _setGameplayUx6A.Handle(history.State, command);
        if (!dryRun.Succeeded)
            return FailedCommandUx6A(dryRun, "module/runtimePolicies/gameplay/simulation/worldTime");

        var sourceWorld = project!.Module.RuntimePolicies.Gameplay.Simulation.WorldTime;
        var candidateWorld = candidate!.Simulation.WorldTime;
        var diff = BuildWorldDiffUx6A(sourceWorld, candidateWorld);
        var runtime = new List<string>();
        if (!candidateWorld.Enabled)
        {
            runtime.Add(candidateWorld.Required
                ? "Le temps du monde requis est désactivé; le validateur canonique doit refuser ce profil."
                : "Temps du monde désactivé · le runtime optionnel conserve l’état sans avancer l’horloge.");
        }
        else
        {
            var service = new GameplaySimulationScriptService(project.Module.Id, candidate.Simulation);
            var reference = service.WorldTime(service.Handle);
            var areaKey = candidateWorld.LocalTimeScales.Keys.FirstOrDefault();
            var minutes = candidateWorld.Schedules.Count == 0
                ? 5
                : Math.Max(1, candidateWorld.Schedules.Min(item => item.MinuteOfDay) -
                    candidateWorld.InitialClock.TotalMinutes + 1);
            var advanced = service.AdvanceWorld(reference, candidateWorld.InitialClock,
                (float)Math.Min(minutes, 10_000L), areaKey, new HashSet<string>(StringComparer.Ordinal),
                authorityGranted: true);
            runtime.Add($"Horloge déterministe · {candidateWorld.InitialClock.TotalMinutes} → {advanced.Clock.TotalMinutes} minute(s) · échelle {advanced.AppliedScale:0.###}×.");
            runtime.Add(advanced.DueSchedules.Count == 0
                ? "Aucun horaire n’arrive dans la fenêtre de prévisualisation."
                : $"{advanced.DueSchedules.Count} horaire(s) deviennent éligibles; aucun événement n’est distribué par Studio.");
        }
        runtime.Add("Aucun nœud Godot, réseau, sauvegarde live ni ForgeScript n’est exécuté par cet aperçu.");
        if (diff.Count > 0)
        {
            _approvedWorldUx6A = draft;
            _approvedWorldRevisionUx6A = history.State.Revision;
            _approvedWorldFingerprintUx6A = WorldCandidateFingerprintUx6A(project, draft);
        }
        return new(true, diff.Count > 0,
            diff.Count > 0
                ? "Aperçu sec du temps du monde validé · aucune mutation appliquée."
                : "Aperçu sec validé · le temps du monde est déjà identique.",
            "Remplacement canonique du profil gameplay; seul le temps du monde est modifié.",
            diff, [], runtime, true, true);
    }

    internal StudioUx6AActionResult ApplyApprovedRulesUx6A()
    {
        var draft = _approvedRulesUx6A;
        var history = History;
        var project = Project;
        if (draft is null || history is null || project is null ||
            _approvedRulesRevisionUx6A != history.State.Revision ||
            draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceFingerprint, SourceFingerprintUx6A(project), StringComparison.Ordinal) ||
            !string.Equals(_approvedRulesFingerprintUx6A, RulesCandidateFingerprintUx6A(project, draft),
                StringComparison.Ordinal) ||
            !TryBuildRulesCandidateUx6A(draft, out var candidate, out _))
        {
            CancelUx6AApprovals();
            return new(false, "Relancez l’aperçu sec des règles avant application.");
        }
        var area = project.Areas.Single(item => item.Id == draft.ContextAreaId);
        var command = new SetRulesProfilesCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            candidate!, area.Id, area.RuntimePolicies.Rules, DryRun: true);
        var dryRun = _setRulesUx6A.Handle(history.State, command);
        if (!dryRun.Succeeded)
        {
            CancelUx6AApprovals();
            return new(false, HumanCommandFailureUx6A(dryRun));
        }
        var applied = _setRulesUx6A.Handle(history.State, command with { DryRun = false });
        CancelUx6AApprovals();
        if (!applied.Succeeded) return new(false, HumanCommandFailureUx6A(applied));
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true, "Règles appliquées · le temps du monde doit être rechargé et prévisualisé séparément.");
    }

    internal StudioUx6AActionResult ApplyApprovedWorldTimeUx6A()
    {
        var draft = _approvedWorldUx6A;
        var history = History;
        var project = Project;
        if (draft is null || history is null || project is null ||
            _approvedWorldRevisionUx6A != history.State.Revision ||
            draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceFingerprint, SourceFingerprintUx6A(project), StringComparison.Ordinal) ||
            !string.Equals(_approvedWorldFingerprintUx6A, WorldCandidateFingerprintUx6A(project, draft),
                StringComparison.Ordinal) ||
            !TryBuildGameplayCandidateUx6A(draft, out var candidate, out _))
        {
            CancelUx6AApprovals();
            return new(false, "Relancez l’aperçu sec du temps du monde avant application.");
        }
        var command = new SetGameplayControlProfileCommand(CommandId.From(Guid.NewGuid()),
            history.State.Revision, candidate!, DryRun: true);
        var dryRun = _setGameplayUx6A.Handle(history.State, command);
        if (!dryRun.Succeeded)
        {
            CancelUx6AApprovals();
            return new(false, HumanCommandFailureUx6A(dryRun));
        }
        var applied = _setGameplayUx6A.Handle(history.State, command with { DryRun = false });
        CancelUx6AApprovals();
        if (!applied.Succeeded) return new(false, HumanCommandFailureUx6A(applied));
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true, "Temps du monde appliqué · les règles doivent être rechargées avant une autre transaction.");
    }

    internal void CancelUx6AApprovals()
    {
        CancelRulesApprovalUx6A();
        CancelWorldApprovalUx6A();
    }

    private void CancelRulesApprovalUx6A()
    {
        _approvedRulesUx6A = null;
        _approvedRulesRevisionUx6A = null;
        _approvedRulesFingerprintUx6A = null;
    }

    private void CancelWorldApprovalUx6A()
    {
        _approvedWorldUx6A = null;
        _approvedWorldRevisionUx6A = null;
        _approvedWorldFingerprintUx6A = null;
    }

    internal StudioUx6AApprovalSnapshot CaptureRulesWorldApprovalUx6A() => new(
        _approvedRulesUx6A, _approvedRulesRevisionUx6A, _approvedRulesFingerprintUx6A,
        _approvedWorldUx6A, _approvedWorldRevisionUx6A, _approvedWorldFingerprintUx6A);

    internal void RestoreRulesWorldApprovalUx6A(StudioUx6AApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedRulesUx6A = snapshot.RulesDraft;
        _approvedRulesRevisionUx6A = snapshot.RulesRevision;
        _approvedRulesFingerprintUx6A = snapshot.RulesFingerprint;
        _approvedWorldUx6A = snapshot.WorldDraft;
        _approvedWorldRevisionUx6A = snapshot.WorldRevision;
        _approvedWorldFingerprintUx6A = snapshot.WorldFingerprint;
    }

    private bool TryGetCurrentUx6A(StudioUx6ADraft draft, out ProjectCommandHistory? history,
        out ProjectDefinition? project, out AreaDefinition? area, out StudioUx6ABlockPreview? failure)
    {
        history = History;
        project = Project;
        area = project?.Areas.SingleOrDefault(item => item.Id == draft.ContextAreaId);
        failure = null;
        if (history is null || project is null || area is null)
        {
            failure = FailedPreviewUx6A("Ouvrez un module et choisissez un contexte de zone.",
                new(Ux6ASelectionCode, "module/runtimePolicies", "Sélection explicite requise."));
            return false;
        }
        if (draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceFingerprint, SourceFingerprintUx6A(project), StringComparison.Ordinal))
        {
            failure = FailedPreviewUx6A("Le projet a changé. Rechargez le profil avant l’aperçu.",
                new(Ux6ASourceCode, "module/runtimePolicies", "La révision ou la source ne correspond plus."));
            return false;
        }
        return true;
    }

    private static bool TryBuildRulesCandidateUx6A(StudioUx6ADraft draft, out ModuleRulesProfile? candidate,
        out StudioUx6ADiagnostic? diagnostic)
    {
        candidate = null;
        diagnostic = null;
        try
        {
            if (draft.Difficulties.Count == 0 ||
                draft.Difficulties.Select(item => item.Id).Distinct().Count() != draft.Difficulties.Count ||
                draft.Difficulties.Select(item => item.Key.Trim().ToLowerInvariant())
                    .Distinct(StringComparer.Ordinal).Count() != draft.Difficulties.Count)
                throw new ArgumentException("Les profils de difficulté exigent des identités et clés uniques.");
            if (!draft.Difficulties.Any(item =>
                    string.Equals(item.Key.Trim(), draft.ActiveDifficultyKey.Trim(),
                        StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("La difficulté active doit exister.");
            var difficulties = draft.Difficulties.Select(item =>
            {
                if (!Enum.IsDefined(item.SaveRestrictions) || !Enum.IsDefined(item.FriendlyFire))
                    throw new ArgumentException("Une politique de difficulté est inconnue.");
                return new DifficultyProfile(item.Id, item.SchemaVersion, item.Key, item.DisplayName,
                    item.PlayerDamageMultiplier, item.IncomingDamageMultiplier, item.AiIntensityMultiplier,
                    item.EconomyMultiplier, item.RestRecoveryMultiplier, item.SaveRestrictions,
                    item.FriendlyFire, new DifficultyAccessibilityOptions(item.PauseAssist,
                        item.TelegraphAssist, item.TimingWindowMultiplier, item.PerceptionCueMultiplier),
                    item.Provenance);
            }).ToArray();
            var mutators = new MutatorSet(draft.SourceRules.Mutators.Id,
                draft.SourceRules.Mutators.SchemaVersion,
                draft.Mutators.Select(item => new GameplayMutatorDefinition(item.Id, item.Key,
                    item.DisplayName, item.Enabled,
                    item.ScalarOverrides.ToDictionary(entry => entry.Key, entry => entry.Value,
                        StringComparer.Ordinal),
                    item.Tags.ToArray())).ToArray(),
                draft.SourceRules.Mutators.Provenance);
            var packages = draft.PluginPackages.Select(item => new RulesetPluginPackage(
                item.Id, item.SchemaVersion, item.Key, item.Version, item.ContentPackageId,
                item.Attributes, item.Effects, item.Abilities, item.Inventories,
                item.ProgressionTracks, item.UiPanels, item.Provenance)).ToArray();
            var plugins = new RulesetPluginCatalog(draft.SourceRules.Plugins.Id,
                draft.SourceRules.Plugins.SchemaVersion, draft.PluginsEnabled,
                draft.PluginsRequiredAtRuntime, draft.ActivePluginKey, packages,
                draft.SourceRules.Plugins.Provenance);
            candidate = new ModuleRulesProfile(draft.SourceRules.Id, draft.SourceRules.SchemaVersion,
                draft.SourceRules.Progression, difficulties, draft.ActiveDifficultyKey, mutators,
                Ux6ARulesProvenance, plugins);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or
                                              InvalidOperationException)
        {
            var code = exception.Message.Contains("mutator", StringComparison.OrdinalIgnoreCase)
                ? Ux6AMutatorCode
                : exception.Message.Contains("plugin", StringComparison.OrdinalIgnoreCase) ||
                  exception.Message.Contains("ruleset", StringComparison.OrdinalIgnoreCase)
                    ? Ux6APluginCode
                    : Ux6ADifficultyCode;
            diagnostic = new(code, "module/runtimePolicies/rules",
                $"Le contrat canonique refuse le profil : {exception.Message}");
            return false;
        }
    }

    private static bool TryBuildGameplayCandidateUx6A(StudioUx6ADraft draft,
        out ModuleGameplayControlProfile? candidate, out StudioUx6ADiagnostic? diagnostic)
    {
        candidate = null;
        diagnostic = null;
        try
        {
            var source = draft.SourceGameplay;
            var sourceSimulation = source.Simulation;
            var sourceWorld = sourceSimulation.WorldTime;
            var scales = draft.AreaScales.Where(item => item.Enabled)
                .ToDictionary(item => item.Key, item => item.Scale, StringComparer.Ordinal);
            var world = new WorldSimulationProfile(sourceWorld.Id, sourceWorld.SchemaVersion,
                sourceWorld.Provenance, draft.WorldTimeEnabled, draft.WorldTimeRequired,
                new WorldCalendar(draft.DaysPerMonth, draft.MonthsPerYear, draft.HoursPerDay,
                    draft.MinutesPerHour),
                new WorldClockState(draft.InitialTotalMinutes, draft.InitialTimeScale,
                    draft.InitiallyPaused, draft.InitialPauseDomain, draft.InitialClockRevision),
                scales, draft.PauseDomains.Select(item => item.Trim())
                    .Where(item => item.Length > 0).Distinct(StringComparer.Ordinal).ToArray(),
                draft.Schedules.Select(item => new WorldSchedule(item.Id, item.SchemaVersion,
                    item.Provenance, item.Key, item.MinuteOfDay, item.DaysOfMonth.ToArray(),
                    item.EventName, item.PauseDomain, item.ServerAuthoritative)).ToArray());
            var simulation = new GameplaySimulationProfile(sourceSimulation.Id,
                sourceSimulation.SchemaVersion, sourceSimulation.Provenance, sourceSimulation.Enabled,
                sourceSimulation.RequiredAtRuntime, sourceSimulation.Flight, sourceSimulation.Projectiles,
                sourceSimulation.Physics, sourceSimulation.Targeting, world);
            candidate = new ModuleGameplayControlProfile(source.Id, source.SchemaVersion,
                source.Swimming, source.Climbing, source.RadialMenu, source.TimeControl,
                source.CombatModes, Ux6AWorldProvenance, source.Foundation, simulation, source.Session);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or
                                              InvalidOperationException)
        {
            diagnostic = new(Ux6AWorldTimeCode,
                "module/runtimePolicies/gameplay/simulation/worldTime",
                $"Le contrat canonique refuse le temps du monde : {exception.Message}");
            return false;
        }
    }

    private static IReadOnlyList<string> BuildRulesDiffUx6A(ModuleRulesProfile source,
        ModuleRulesProfile candidate)
    {
        var entries = new List<string>();
        if (!string.Equals(source.ActiveDifficultyKey, candidate.ActiveDifficultyKey, StringComparison.Ordinal))
            entries.Add($"Difficulté active · {HumanUx6A(source.ActiveDifficulty.DisplayName)} → {HumanUx6A(candidate.ActiveDifficulty.DisplayName)}");
        if (!source.Difficulties.SequenceEqual(candidate.Difficulties))
            entries.Add($"Profils de difficulté · {source.Difficulties.Count} → {candidate.Difficulties.Count}; paramètres et identités conservés par profil.");
        if (!source.Mutators.Mutators.SequenceEqual(candidate.Mutators.Mutators))
            entries.Add($"Mutateurs · {candidate.Mutators.Mutators.Count(item => item.Enabled)} actif(s) sur {candidate.Mutators.Mutators.Count}.");
        if (source.Plugins.Enabled != candidate.Plugins.Enabled ||
            source.Plugins.RequiredAtRuntime != candidate.Plugins.RequiredAtRuntime ||
            !string.Equals(source.Plugins.ActivePackageKey, candidate.Plugins.ActivePackageKey,
                StringComparison.Ordinal) || !source.Plugins.Packages.SequenceEqual(candidate.Plugins.Packages))
            entries.Add($"Plugins de règles · {candidate.Plugins.Packages.Count} package(s) · actif {HumanUx6A(candidate.Plugins.ActivePackageKey)}.");
        return entries;
    }

    private static IReadOnlyList<string> BuildWorldDiffUx6A(WorldSimulationProfile source,
        WorldSimulationProfile candidate)
    {
        var entries = new List<string>();
        if (source.Enabled != candidate.Enabled || source.Required != candidate.Required)
            entries.Add($"Disponibilité · {(source.Enabled ? "active" : "inactive")} → {(candidate.Enabled ? "active" : "inactive")} · {(candidate.Required ? "requise" : "optionnelle")}.");
        if (source.Calendar != candidate.Calendar)
            entries.Add($"Calendrier · {candidate.Calendar.DaysPerMonth} jours × {candidate.Calendar.MonthsPerYear} mois · {candidate.Calendar.HoursPerDay} h × {candidate.Calendar.MinutesPerHour} min.");
        if (source.InitialClock != candidate.InitialClock)
            entries.Add($"Horloge initiale · minute {source.InitialClock.TotalMinutes} → {candidate.InitialClock.TotalMinutes} · échelle {candidate.InitialClock.TimeScale:0.###}×.");
        if (!source.LocalTimeScales.OrderBy(item => item.Key).SequenceEqual(
                candidate.LocalTimeScales.OrderBy(item => item.Key)))
            entries.Add($"Échelles locales · {candidate.LocalTimeScales.Count} zone(s) configurée(s).");
        if (!source.PauseDomains.SequenceEqual(candidate.PauseDomains))
            entries.Add($"Domaines de pause · {candidate.PauseDomains.Count} valeur(s).");
        if (!source.Schedules.SequenceEqual(candidate.Schedules))
            entries.Add($"Horaires · {candidate.Schedules.Count} événement(s) ordonné(s).");
        return entries;
    }

    private static StudioUx6ABlockPreview FailedPreviewUx6A(string message, StudioUx6ADiagnostic diagnostic) =>
        new(false, false, message, string.Empty, [], [diagnostic], [], false, true);

    private static StudioUx6ABlockPreview FailedCommandUx6A(CommandResult result, string path)
    {
        var diagnostics = result.Diagnostics.Select(item => new StudioUx6ADiagnostic(item.Code, path,
            HumanDiagnosticUx6A(item.Code, item.Message))).ToArray();
        return new(false, false, diagnostics.FirstOrDefault()?.Message ?? "La commande canonique a refusé le profil.",
            string.Empty, [], diagnostics, [], false, true);
    }

    private static string HumanCommandFailureUx6A(CommandResult result) =>
        HumanDiagnosticUx6A(result.Diagnostics.FirstOrDefault()?.Code,
            result.Diagnostics.FirstOrDefault()?.Message ?? "Commande refusée.");

    private static string HumanDiagnosticUx6A(string? code, string message) => code switch
    {
        "command.revision_conflict" => "Le projet a changé; rechargez le profil.",
        RulesetPluginValidator.RequiredDisabledCode => "Un plugin requis ne peut pas être désactivé.",
        RulesetPluginValidator.ActiveMissingCode => "Le package de règles actif n’existe pas.",
        RulesetPluginValidator.RegistryMissingCode => "Chaque package doit fournir les six registres de règles.",
        _ => string.IsNullOrWhiteSpace(message) ? "La validation canonique a refusé le profil." : message,
    };

    private static ProjectDefinition BuildRulesProjectUx6A(ProjectDefinition source,
        ModuleRulesProfile candidate, AreaId contextAreaId)
    {
        var area = source.Areas.Single(item => item.Id == contextAreaId);
        var previousModule = source.Module.RuntimePolicies;
        var modulePolicies = new ModuleRuntimePolicyProfile(previousModule.Id, previousModule.SchemaVersion,
            previousModule.Heartbeats, previousModule.Rest, previousModule.SequenceAbort, "rules-command",
            previousModule.Cinematics, previousModule.Gameplay, candidate, previousModule.Extensions,
            previousModule.GameplayEvents, previousModule.World, previousModule.LifecycleEvents);
        var previousArea = area.RuntimePolicies;
        var areaPolicies = new AreaRuntimePolicyProfile(previousArea.Id, previousArea.SchemaVersion,
            previousArea.Heartbeats, previousArea.Rest, "rules-command", previousArea.Cinematics,
            previousArea.Rules, previousArea.Experience);
        return new ProjectDefinition(source.Id, source.Title, source.Module.WithRuntimePolicies(modulePolicies),
            source.Blueprints, source.Areas.Select(item => item.Id == area.Id
                ? item.WithRuntimePolicies(areaPolicies) : item).ToArray(), source.Dialogues, source.Quests);
    }

    private static ProjectDefinition BuildGameplayProjectUx6A(ProjectDefinition source,
        ModuleGameplayControlProfile candidate)
    {
        var previous = source.Module.RuntimePolicies;
        var policies = new ModuleRuntimePolicyProfile(previous.Id, previous.SchemaVersion, previous.Heartbeats,
            previous.Rest, previous.SequenceAbort, "gameplay-command", previous.Cinematics, candidate,
            previous.Rules, previous.Extensions, previous.GameplayEvents, previous.World,
            previous.LifecycleEvents);
        return new ProjectDefinition(source.Id, source.Title, source.Module.WithRuntimePolicies(policies),
            source.Blueprints, source.Areas, source.Dialogues, source.Quests);
    }

    private static string RulesCandidateFingerprintUx6A(ProjectDefinition project, StudioUx6ADraft draft)
    {
        if (!TryBuildRulesCandidateUx6A(draft, out var candidate, out _) || candidate is null)
            return string.Empty;
        return HashUx6A(ProjectJsonSerializer.Serialize(BuildRulesProjectUx6A(project, candidate,
            draft.ContextAreaId)));
    }

    private static string WorldCandidateFingerprintUx6A(ProjectDefinition project, StudioUx6ADraft draft)
    {
        if (!TryBuildGameplayCandidateUx6A(draft, out var candidate, out _) || candidate is null)
            return string.Empty;
        return HashUx6A(ProjectJsonSerializer.Serialize(BuildGameplayProjectUx6A(project, candidate)));
    }

    private static string SourceFingerprintUx6A(ProjectDefinition project) =>
        HashUx6A(ProjectJsonSerializer.Serialize(project));

    private static string HashUx6A(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string HumanUx6A(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "aucun";
        if (Guid.TryParse(value, out _)) return "identifiant protégé";
        return string.Join(' ', value.Trim().Replace('_', ' ').Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    internal StudioUx6ASmokeResult RunUx6AContractSmoke()
    {
        var real = Project;
        if (real is null || real.Areas.Count == 0) return FailedUx6ASmoke();
        var realData = real.Module.RuntimePolicies.Rules.Difficulties.Count > 0 &&
                       real.Module.RuntimePolicies.Gameplay.Simulation.WorldTime.Id != Guid.Empty;
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux6-a-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        var cleanup = false;
        try
        {
            var created = ProjectSession.Create(root, real);
            if (!created.Succeeded || !editing.Open(root).Succeeded) return FailedUx6ASmoke();
            var areaId = editing.Project!.Areas[0].Id;
            var explicitSelection = editing.CreateRulesWorldDraftUx6A(areaId) is null &&
                                    editing.CreateRulesWorldDraftUx6A(areaId, true) is not null;
            var baseline = editing.CreateRulesWorldDraftUx6A(areaId, true)!;
            var invalidDifficulty = baseline with
            {
                Difficulties = baseline.Difficulties.Select((item, index) => index == 0
                    ? item with { PlayerDamageMultiplier = 0 } : item).ToArray(),
            };
            var invalidPlugin = baseline with { PluginsEnabled = false, PluginsRequiredAtRuntime = true };
            var invalidWorld = baseline with { DaysPerMonth = 0 };
            var difficultyValidation = !editing.PreviewRulesUx6A(invalidDifficulty).Succeeded;
            var pluginValidation = !editing.PreviewRulesUx6A(invalidPlugin).Succeeded;
            var worldValidation = !editing.PreviewWorldTimeUx6A(invalidWorld).Succeeded;

            var beforeDry = editing.Project;
            var history = editing.History!;
            var beforeRevision = history.State.Revision;
            var beforeJournal = history.Journal.ToArray();
            var rulesDraft = ChangedRulesDraftUx6A(baseline);
            var worldDraft = ChangedWorldDraftUx6A(baseline);
            var rulesPreview = editing.PreviewRulesUx6A(rulesDraft);
            var worldPreview = editing.PreviewWorldTimeUx6A(worldDraft);
            var dryState = ReferenceEquals(beforeDry, editing.Project) && history.State.Revision == beforeRevision &&
                           history.Journal.SequenceEqual(beforeJournal) && !editing.IsDirty;
            var rulesDryRun = rulesPreview is { Succeeded: true, CanApply: true, CanonicalDryRun: true } && dryState;
            var worldDryRun = worldPreview is { Succeeded: true, CanApply: true, CanonicalDryRun: true } && dryState;
            var runtimePreview = rulesPreview.RuntimePreview.Count >= 5 && worldPreview.RuntimePreview.Count >= 2;

            var staleWorldApply = editing.ApplyApprovedWorldTimeUx6A();
            var staleRulesApply = editing.ApplyApprovedRulesUx6A();
            var staleSourceBlocked = staleWorldApply.Succeeded && !staleRulesApply.Succeeded &&
                                     editing.Undo().Succeeded;

            var tamperDraft = ChangedRulesDraftUx6A(editing.CreateRulesWorldDraftUx6A(areaId, true)!);
            var tamperPreview = editing.PreviewRulesUx6A(tamperDraft);
            var approval = editing.CaptureRulesWorldApprovalUx6A();
            editing.RestoreRulesWorldApprovalUx6A(approval with
            {
                RulesDraft = approval.RulesDraft! with
                {
                    Difficulties = approval.RulesDraft.Difficulties.Select((item, index) => index == 0
                        ? item with { DisplayName = $"{item.DisplayName} modifié" }
                        : item).ToArray(),
                },
            });
            var staleApproval = editing.ApplyApprovedRulesUx6A();
            var staleApprovalBlocked = tamperPreview.Succeeded && !staleApproval.Succeeded;

            var sourceProject = editing.Project!;
            var sourceRules = sourceProject.Module.RuntimePolicies.Rules;
            var sourceGameplay = sourceProject.Module.RuntimePolicies.Gameplay;
            var freshRules = ChangedRulesDraftUx6A(editing.CreateRulesWorldDraftUx6A(areaId, true)!);
            if (!TryBuildRulesCandidateUx6A(freshRules, out var expectedRules, out _) || expectedRules is null)
                return FailedUx6ASmoke();
            var previewRules = editing.PreviewRulesUx6A(freshRules);
            var applyRules = editing.ApplyApprovedRulesUx6A();
            var afterRules = editing.Project!;
            var freshWorld = ChangedWorldDraftUx6A(editing.CreateRulesWorldDraftUx6A(areaId, true)!);
            if (!TryBuildGameplayCandidateUx6A(freshWorld, out var expectedGameplay, out _) || expectedGameplay is null)
                return FailedUx6ASmoke();
            var previewWorld = editing.PreviewWorldTimeUx6A(freshWorld);
            var applyWorld = editing.ApplyApprovedWorldTimeUx6A();
            var finalProject = editing.Project!;
            var finalRules = finalProject.Module.RuntimePolicies.Rules;
            var finalGameplay = finalProject.Module.RuntimePolicies.Gameplay;
            var journalNames = editing.History!.Journal.TakeLast(2).Select(item => item.CommandName).ToArray();
            var canonicalCommands = previewRules.Succeeded && applyRules.Succeeded && previewWorld.Succeeded &&
                                    applyWorld.Succeeded && journalNames.SequenceEqual(
                                        new[] { "set_rules_profiles", "set_gameplay_control_profile" });
            var rulesTails = ReferenceEquals(sourceRules.Progression, finalRules.Progression) &&
                             sourceRules.Id == finalRules.Id && sourceRules.SchemaVersion == finalRules.SchemaVersion;
            var gameplayTails = ReferenceEquals(sourceGameplay.Swimming, finalGameplay.Swimming) &&
                                ReferenceEquals(sourceGameplay.Climbing, finalGameplay.Climbing) &&
                                ReferenceEquals(sourceGameplay.RadialMenu, finalGameplay.RadialMenu) &&
                                ReferenceEquals(sourceGameplay.TimeControl, finalGameplay.TimeControl) &&
                                ReferenceEquals(sourceGameplay.CombatModes, finalGameplay.CombatModes) &&
                                ReferenceEquals(sourceGameplay.Foundation, finalGameplay.Foundation) &&
                                ReferenceEquals(sourceGameplay.Session, finalGameplay.Session) &&
                                ReferenceEquals(sourceGameplay.Simulation.Flight, finalGameplay.Simulation.Flight) &&
                                ReferenceEquals(sourceGameplay.Simulation.Projectiles,
                                    finalGameplay.Simulation.Projectiles) &&
                                ReferenceEquals(sourceGameplay.Simulation.Physics,
                                    finalGameplay.Simulation.Physics) &&
                                ReferenceEquals(sourceGameplay.Simulation.Targeting,
                                    finalGameplay.Simulation.Targeting);
            var expectedAfterRules = BuildRulesProjectUx6A(sourceProject, expectedRules, areaId);
            var expectedFinal = BuildGameplayProjectUx6A(expectedAfterRules, expectedGameplay);
            var projectNeighbors = string.Equals(ProjectJsonSerializer.Serialize(expectedFinal),
                ProjectJsonSerializer.Serialize(finalProject), StringComparison.Ordinal);

            var finalJson = ProjectJsonSerializer.Serialize(finalProject);
            var undoWorld = editing.Undo();
            var undoRules = editing.Undo();
            var undone = undoWorld.Succeeded && undoRules.Succeeded && ReferenceEquals(sourceProject, editing.Project);
            var redoRules = editing.Redo();
            var redoWorld = editing.Redo();
            var redone = redoRules.Succeeded && redoWorld.Succeeded && ReferenceEquals(finalProject, editing.Project);
            var undoRedo = undone && redone;
            var saved = editing.Save();
            var opened = reopened.Open(root);
            var persistence = saved.Succeeded && opened.Succeeded && reopened.Project is not null &&
                              string.Equals(finalJson, ProjectJsonSerializer.Serialize(reopened.Project),
                                  StringComparison.Ordinal);
            var humanText = !ContainsUuidUx6A(string.Join(' ',
                editing.RulesWorldChoicesUx6A.Select(item => item.Label)) + " " +
                string.Join(' ', rulesPreview.Diff) + " " + string.Join(' ', worldPreview.Diff) + " " +
                string.Join(' ', rulesPreview.RuntimePreview) + " " +
                string.Join(' ', worldPreview.RuntimePreview));
            var bounded = rulesPreview.RuntimeClaimsBounded && worldPreview.RuntimeClaimsBounded &&
                          rulesPreview.RuntimePreview.Any(item => item.Contains("Aucun package natif",
                              StringComparison.Ordinal)) &&
                          worldPreview.RuntimePreview.Any(item => item.Contains("Aucun nœud Godot",
                              StringComparison.Ordinal));

            editing.Close();
            reopened.Close();
            cleanup = DeleteUx6ATemporaryWorkspace(root);
            var succeeded = explicitSelection && realData && difficultyValidation && pluginValidation &&
                            worldValidation && rulesDryRun && worldDryRun && runtimePreview && staleSourceBlocked &&
                            staleApprovalBlocked && canonicalCommands && undoRedo && persistence && rulesTails &&
                            gameplayTails && projectNeighbors && humanText && bounded && cleanup;
            return new(succeeded, explicitSelection, realData, difficultyValidation, pluginValidation,
                worldValidation, rulesDryRun, worldDryRun, runtimePreview, staleSourceBlocked,
                staleApprovalBlocked, canonicalCommands, undoRedo, persistence, rulesTails, gameplayTails,
                projectNeighbors, humanText, bounded, cleanup);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_A_CONTRACT_FAILED " +
                              $"type={exception.GetType().Name} message={exception.Message}");
            return FailedUx6ASmoke();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            cleanup = DeleteUx6ATemporaryWorkspace(root);
            if (!cleanup) Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_A_CLEANUP_FAILED");
        }
    }

    private static StudioUx6ADraft ChangedRulesDraftUx6A(StudioUx6ADraft source)
    {
        var first = source.Difficulties[0];
        var changed = first with
        {
            DisplayName = first.DisplayName.EndsWith(" · Studio", StringComparison.Ordinal)
                ? first.DisplayName.Replace(" · Studio", string.Empty, StringComparison.Ordinal)
                : first.DisplayName + " · Studio",
            PlayerDamageMultiplier = first.PlayerDamageMultiplier >= 19.9
                ? first.PlayerDamageMultiplier - .1 : first.PlayerDamageMultiplier + .1,
        };
        var mutators = source.Mutators.Count == 0
            ? source.Mutators
            : source.Mutators.Select((item, index) => index == 0
                ? item with { Enabled = !item.Enabled } : item).ToArray();
        var packages = source.PluginPackages.Select((item, index) => index == 0
            ? item with
            {
                Version = item.Version.EndsWith("-studio", StringComparison.Ordinal)
                ? item.Version.Replace("-studio", string.Empty, StringComparison.Ordinal)
                : item.Version + "-studio"
            }
            : item).ToArray();
        return source with
        {
            Difficulties = source.Difficulties.Select((item, index) => index == 0 ? changed : item).ToArray(),
            Mutators = mutators,
            PluginPackages = packages,
        };
    }

    private static StudioUx6ADraft ChangedWorldDraftUx6A(StudioUx6ADraft source)
    {
        var scales = source.AreaScales.Select((item, index) => index == 0
            ? item with { Enabled = true, Scale = item.Scale >= 999 ? item.Scale - .5f : item.Scale + .5f }
            : item).ToArray();
        return source with
        {
            InitialTimeScale = source.InitialTimeScale >= 999 ? source.InitialTimeScale - .25f :
                source.InitialTimeScale + .25f,
            AreaScales = scales,
        };
    }

    private static bool ContainsUuidUx6A(string text) => System.Text.RegularExpressions.Regex.IsMatch(text,
        @"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\b");

    private static StudioUx6ASmokeResult FailedUx6ASmoke() => new(false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false, false, false,
        false, false);

    internal static bool DeleteUx6ATemporaryWorkspace(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return true;
        try
        {
            var full = Path.GetFullPath(directory);
            var temp = Path.GetFullPath(Path.GetTempPath());
            var leaf = Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !leaf.StartsWith("openrpg-forge-studio-ux6-a-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full);
        }
        catch
        {
            return false;
        }
    }

    private void CloseUx6A() => CancelUx6AApprovals();
}
