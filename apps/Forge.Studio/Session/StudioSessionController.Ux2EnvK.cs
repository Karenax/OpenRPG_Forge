using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Spatial;
using Forge.EngineAdapter.Rendering;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2EnvKSelectionCode = "studio.water_graphics.selection_invalid";
    private const string Ux2EnvKWaterCode = "studio.water_graphics.water_quality_invalid";
    private const string Ux2EnvKAntiAliasingCode = "studio.water_graphics.anti_aliasing_invalid";
    private const string Ux2EnvKAnisotropyCode = "studio.water_graphics.anisotropy_invalid";
    private const string Ux2EnvKUpscalingCode = "studio.water_graphics.upscaling_invalid";
    private const string Ux2EnvKSourceCode = "studio.water_graphics.source_changed";
    private const string Ux2EnvKProvenance = "studio:ux2-env-k:water-graphics-quality";

    private readonly SetRuntimeExtensionsHandler _setRuntimeExtensionsUx2EnvK = new(new SystemClock());
    private StudioUx2EnvKDraft? _approvedWaterGraphicsUx2EnvK;
    private ProjectRevision? _approvedWaterGraphicsRevisionUx2EnvK;
    private string? _approvedWaterGraphicsFingerprintUx2EnvK;

    internal IReadOnlyList<StudioUx2EnvKProfileChoice> WaterGraphicsChoicesUx2EnvK
    {
        get
        {
            var project = Project;
            if (project is null) return [];
            var waterBodies = project.Areas.Sum(area => area.Environment.Water.Bodies.Count);
            var bodyLabel = waterBodies == 1 ? "1 masse d’eau" : $"{waterBodies} masses d’eau";
            return [new($"{HumanTokenUx2EnvK(project.Module.DisplayName)} · {bodyLabel}", waterBodies)];
        }
    }

    internal StudioUx2EnvKDraft? CreateWaterGraphicsDraftUx2EnvK(bool explicitlySelected = false)
    {
        var project = Project;
        var history = History;
        if (!explicitlySelected || project is null || history is null) return null;
        var creator = project.Module.RuntimePolicies.Extensions.PostLaunch.CreatorExperience;
        return new(
            history.State.Revision,
            WaterGraphicsSourceFingerprintUx2EnvK(project),
            creator.Water.Refraction,
            creator.Water.ScreenSpaceReflections,
            creator.Water.DepthColor,
            creator.Water.FallbackQuality,
            creator.Graphics.AntiAliasing,
            creator.Graphics.AnisotropicLevel,
            creator.Graphics.Upscaling,
            creator.Graphics.CapabilityFallback);
    }

    internal StudioUx2EnvKPreview PreviewWaterGraphicsUx2EnvK(StudioUx2EnvKDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelWaterGraphicsApprovalUx2EnvK();
        var history = History;
        var project = Project;
        if (history is null || project is null)
            return FailedWaterGraphicsPreviewUx2EnvK("Aucun module n’est ouvert.",
                new(Ux2EnvKSelectionCode, "module/quality", "Ouvrez un module avant de prévisualiser."));
        if (draft.BaseRevision != history.State.Revision)
            return FailedWaterGraphicsPreviewUx2EnvK(
                "Le projet a changé depuis l’ouverture. Rechargez le profil avant l’aperçu.",
                new(Ux2EnvKSourceCode, "module/runtimePolicies/extensions/postLaunch/creatorExperience",
                    "La révision du projet n’est plus celle du brouillon."));
        if (!string.Equals(draft.SourceFingerprint, WaterGraphicsSourceFingerprintUx2EnvK(project),
                StringComparison.Ordinal))
            return FailedWaterGraphicsPreviewUx2EnvK(
                "Le profil source a changé. Rechargez-le avant l’aperçu.",
                new(Ux2EnvKSourceCode, "module/runtimePolicies/extensions/postLaunch/creatorExperience",
                    "La source canonique ne correspond plus au brouillon ouvert."));

        if (!TryBuildWaterGraphicsProfileUx2EnvK(project, draft, out var candidate,
                out var diagnostic))
            return FailedWaterGraphicsPreviewUx2EnvK(
                diagnostic?.Message ?? "Le profil contient une valeur invalide.", diagnostic!);

        var command = new SetRuntimeExtensionsCommand(
            CommandId.From(Guid.NewGuid()),
            history.State.Revision,
            candidate!,
            DryRun: true);
        var dryRun = _setRuntimeExtensionsUx2EnvK.Handle(history.State, command);
        if (!dryRun.Succeeded)
        {
            var item = dryRun.Diagnostics.FirstOrDefault();
            return FailedWaterGraphicsPreviewUx2EnvK(
                HumanizeWaterGraphicsDiagnosticUx2EnvK(item?.Code),
                new(item?.Code ?? "command.validation_failed",
                    "module/runtimePolicies/extensions/postLaunch/creatorExperience",
                    HumanizeWaterGraphicsDiagnosticUx2EnvK(item?.Code)));
        }

        var sourceCreator = project.Module.RuntimePolicies.Extensions.PostLaunch.CreatorExperience;
        var candidateCreator = candidate!.PostLaunch.CreatorExperience;
        var diff = BuildWaterGraphicsDiffUx2EnvK(sourceCreator, candidateCreator);
        var waterPlans = BuildWaterPlansUx2EnvK(project, draft);
        var graphicsPlans = BuildGraphicsPlansUx2EnvK(candidateCreator);
        if (diff.HasChanges)
        {
            _approvedWaterGraphicsUx2EnvK = draft;
            _approvedWaterGraphicsRevisionUx2EnvK = history.State.Revision;
            _approvedWaterGraphicsFingerprintUx2EnvK = WaterGraphicsDraftFingerprintUx2EnvK(draft);
        }

        var summary =
            $"{waterPlans.Count} ligne(s) d’eau et {graphicsPlans.Count} ligne(s) de qualité sont calculées. " +
            "Le dry-run persiste uniquement la politique Creator par SetRuntimeExtensions. Les masses d’eau de zone " +
            "restent en lecture seule dans ce lot. Studio n’instancie aucun matériau, ne change aucun viewport et ne " +
            "sonde aucun GPU; l’adaptateur Godot et la plateforme décident réellement au runtime.";
        return new(
            true,
            diff.HasChanges
                ? "Aperçu sec validé · aucune mutation n’est encore appliquée."
                : "Aperçu sec validé · le profil est déjà identique.",
            summary,
            diff.HasChanges,
            diff,
            GroupWaterGraphicsDiagnosticsUx2EnvK([]),
            waterPlans,
            graphicsPlans,
            true,
            true,
            true,
            true,
            true);
    }

    internal StudioUx2EnvKActionResult ApplyApprovedWaterGraphicsUx2EnvK()
    {
        var draft = _approvedWaterGraphicsUx2EnvK;
        var history = History;
        var project = Project;
        if (draft is null || history is null || project is null ||
            _approvedWaterGraphicsRevisionUx2EnvK != history.State.Revision ||
            !string.Equals(_approvedWaterGraphicsFingerprintUx2EnvK,
                WaterGraphicsDraftFingerprintUx2EnvK(draft), StringComparison.Ordinal) ||
            draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceFingerprint, WaterGraphicsSourceFingerprintUx2EnvK(project),
                StringComparison.Ordinal))
        {
            CancelWaterGraphicsApprovalUx2EnvK();
            return new(false, "Relancez l’aperçu sec avant d’appliquer le profil.");
        }
        if (!TryBuildWaterGraphicsProfileUx2EnvK(project, draft, out var candidate, out var diagnostic))
        {
            CancelWaterGraphicsApprovalUx2EnvK();
            return new(false, diagnostic?.Message ?? "Le profil ne peut pas être reconstruit.");
        }

        var command = new SetRuntimeExtensionsCommand(
            CommandId.From(Guid.NewGuid()), history.State.Revision, candidate!, DryRun: true);
        var dryRun = _setRuntimeExtensionsUx2EnvK.Handle(history.State, command);
        if (!dryRun.Succeeded)
        {
            CancelWaterGraphicsApprovalUx2EnvK();
            return new(false, HumanizeWaterGraphicsDiagnosticUx2EnvK(dryRun.Diagnostics.FirstOrDefault()?.Code));
        }
        var applied = _setRuntimeExtensionsUx2EnvK.Handle(history.State, command with { DryRun = false });
        CancelWaterGraphicsApprovalUx2EnvK();
        if (!applied.Succeeded)
            return new(false, HumanizeWaterGraphicsDiagnosticUx2EnvK(applied.Diagnostics.FirstOrDefault()?.Code));
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true, "Politique eau et qualité appliquée par une commande canonique annulable.");
    }

    internal void CancelWaterGraphicsApprovalUx2EnvK()
    {
        _approvedWaterGraphicsUx2EnvK = null;
        _approvedWaterGraphicsRevisionUx2EnvK = null;
        _approvedWaterGraphicsFingerprintUx2EnvK = null;
    }

    internal StudioUx2EnvKApprovalSnapshot CaptureWaterGraphicsApprovalUx2EnvK() => new(
        _approvedWaterGraphicsUx2EnvK,
        _approvedWaterGraphicsRevisionUx2EnvK,
        _approvedWaterGraphicsFingerprintUx2EnvK);

    internal void RestoreWaterGraphicsApprovalUx2EnvK(StudioUx2EnvKApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedWaterGraphicsUx2EnvK = snapshot.Draft;
        _approvedWaterGraphicsRevisionUx2EnvK = snapshot.Revision;
        _approvedWaterGraphicsFingerprintUx2EnvK = snapshot.Fingerprint;
    }

    private static bool TryBuildWaterGraphicsProfileUx2EnvK(
        ProjectDefinition project,
        StudioUx2EnvKDraft draft,
        out ModuleRuntimeExtensionProfile? candidate,
        out StudioUx2EnvKDiagnostic? diagnostic)
    {
        candidate = null;
        diagnostic = null;
        if (draft.FallbackQuality is not ("low" or "medium" or "high"))
        {
            diagnostic = new(Ux2EnvKWaterCode,
                "module/runtimePolicies/extensions/postLaunch/creatorExperience/water/fallbackQuality",
                "Le repli d’eau doit être bas, moyen ou élevé.");
            return false;
        }
        if (draft.AntiAliasing is not ("disabled" or "fxaa" or "msaa2" or "msaa4" or "msaa8"))
        {
            diagnostic = new(Ux2EnvKAntiAliasingCode,
                "module/runtimePolicies/extensions/postLaunch/creatorExperience/graphics/antiAliasing",
                "Choisissez un mode d’anticrénelage pris en charge.");
            return false;
        }
        if (draft.AnisotropicLevel is not (1 or 2 or 4 or 8 or 16))
        {
            diagnostic = new(Ux2EnvKAnisotropyCode,
                "module/runtimePolicies/extensions/postLaunch/creatorExperience/graphics/anisotropicLevel",
                "Le filtrage anisotrope doit valoir 1, 2, 4, 8 ou 16.");
            return false;
        }
        if (draft.Upscaling is not ("disabled" or "fsr" or "native"))
        {
            diagnostic = new(Ux2EnvKUpscalingCode,
                "module/runtimePolicies/extensions/postLaunch/creatorExperience/graphics/upscaling",
                "Choisissez un mode de mise à l’échelle pris en charge.");
            return false;
        }

        try
        {
            var extensions = project.Module.RuntimePolicies.Extensions;
            var post = extensions.PostLaunch;
            var creator = post.CreatorExperience;
            var water = new CreatorExperienceProductionProfile.WaterQualitySettings(
                creator.Water.Id,
                draft.Refraction,
                draft.ScreenSpaceReflections,
                draft.DepthColor,
                draft.FallbackQuality);
            var graphics = new CreatorExperienceProductionProfile.GraphicsQualitySettings(
                creator.Graphics.Id,
                draft.AntiAliasing,
                draft.AnisotropicLevel,
                draft.Upscaling,
                draft.CapabilityFallback);
            var updatedCreator = new CreatorExperienceProductionProfile(
                creator.Id,
                creator.SchemaVersion,
                water,
                creator.DeviceQueries,
                creator.ResourceCommands,
                creator.DynamicAreas,
                creator.PartyControl,
                creator.ProgressionTracks,
                creator.RepositoryMetadata,
                creator.Observability,
                graphics,
                Ux2EnvKProvenance);
            var updatedPost = new PostLaunchCapabilityProfile(
                post.Id,
                post.SchemaVersion,
                post.Gameplay,
                post.LiveDirector,
                post.Network,
                updatedCreator,
                post.Production,
                Ux2EnvKProvenance);
            candidate = new ModuleRuntimeExtensionProfile(
                extensions.Id,
                extensions.SchemaVersion,
                extensions.RuleSchemas,
                extensions.UiDocuments,
                extensions.JsonDocuments,
                extensions.SnapshotPolicy,
                Ux2EnvKProvenance,
                extensions.ModernTooling,
                extensions.OperationalReadiness,
                updatedPost);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            diagnostic = new("command.validation_failed",
                "module/runtimePolicies/extensions/postLaunch/creatorExperience",
                $"Le contrat canonique refuse le profil : {exception.Message}");
            return false;
        }
    }

    private static StudioUx2EnvKDiff BuildWaterGraphicsDiffUx2EnvK(
        CreatorExperienceProductionProfile source,
        CreatorExperienceProductionProfile candidate)
    {
        var entries = new List<string>();
        AddBool(source.Water.Refraction, candidate.Water.Refraction, "Réfraction");
        AddBool(source.Water.ScreenSpaceReflections, candidate.Water.ScreenSpaceReflections,
            "Réflexions d’écran");
        AddBool(source.Water.DepthColor, candidate.Water.DepthColor, "Couleur selon la profondeur");
        AddText(HumanWaterQualityUx2EnvK(source.Water.FallbackQuality),
            HumanWaterQualityUx2EnvK(candidate.Water.FallbackQuality), "Repli eau");
        AddText(HumanAntiAliasingUx2EnvK(source.Graphics.AntiAliasing),
            HumanAntiAliasingUx2EnvK(candidate.Graphics.AntiAliasing), "Anticrénelage");
        AddText($"{source.Graphics.AnisotropicLevel}×", $"{candidate.Graphics.AnisotropicLevel}×",
            "Filtrage anisotrope");
        AddText(HumanUpscalingUx2EnvK(source.Graphics.Upscaling),
            HumanUpscalingUx2EnvK(candidate.Graphics.Upscaling), "Mise à l’échelle");
        AddBool(source.Graphics.CapabilityFallback, candidate.Graphics.CapabilityFallback,
            "Repli selon les capacités");
        return new(entries);

        void AddBool(bool before, bool after, string label)
        {
            if (before != after)
                entries.Add($"{label} · {(before ? "activé" : "désactivé")} → {(after ? "activé" : "désactivé")}");
        }

        void AddText(string before, string after, string label)
        {
            if (!string.Equals(before, after, StringComparison.Ordinal))
                entries.Add($"{label} · {before} → {after}");
        }
    }

    private static IReadOnlyList<StudioUx2EnvKWaterPlan> BuildWaterPlansUx2EnvK(
        ProjectDefinition project,
        StudioUx2EnvKDraft draft)
    {
        var plans = new List<StudioUx2EnvKWaterPlan>();
        foreach (var area in project.Areas.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            foreach (var body in area.Environment.Water.Bodies)
            {
                var plan = AreaProductionPlanBuilder.Water(body, body.Quality, submergedFraction: .5f);
                var foam = plan.ShaderParameters.TryGetValue("water_foam", out var foamValue) && foamValue > 0;
                var canonicalRefraction = plan.ShaderParameters.TryGetValue("water_refraction", out var refraction) &&
                                          refraction > 0;
                var surface =
                    $"réfraction {(draft.Refraction && canonicalRefraction ? "prévue" : "désactivée")}, " +
                    $"réflexions {(draft.ScreenSpaceReflections ? "demandées" : "désactivées")}, " +
                    $"profondeur {(draft.DepthColor ? "colorée" : "uniforme")}, écume {(foam ? "prévue" : "réduite")}";
                var material = string.IsNullOrWhiteSpace(body.MaterialResource)
                    ? "matériau procédural canonique"
                    : $"ressource {HumanResourceUx2EnvK(body.MaterialResource)}";
                var physics =
                    $"flottabilité {plan.BuoyancyAcceleration:0.##}, navigation {plan.NavigationCost:0.##}, " +
                    $"nage {(plan.SwimmingEnabled ? "autorisée" : "interdite")}";
                plans.Add(new(
                    HumanTokenUx2EnvK(area.DisplayName),
                    HumanTokenUx2EnvK(body.Name),
                    HumanWaterTierUx2EnvK(body.Quality),
                    material,
                    surface,
                    physics));
            }
        }
        if (plans.Count == 0)
        {
            plans.Add(new(
                "Projet ouvert",
                "Aucune masse d’eau de zone",
                $"repli {HumanWaterQualityUx2EnvK(draft.FallbackQuality)}",
                "aucun matériau à instancier",
                "la politique globale reste éditable sans inventer de surface",
                "aucune flottabilité ni navigation simulée"));
        }
        return plans;
    }

    private static IReadOnlyList<StudioUx2EnvKGraphicsPlan> BuildGraphicsPlansUx2EnvK(
        CreatorExperienceProductionProfile profile)
    {
        var fallback = profile.Graphics.CapabilityFallback
            ? "repli autorisé si l’adaptateur le requiert"
            : "aucun repli automatique demandé";
        var decision = PostLaunchCapabilityResolver.ResolveCreatorTechnical(
            profile, [], requestedResourceResults: 0, requestedAreaInstances: 0);
        var resolvedGraphics = string.Equals(decision.GraphicsMode,
            $"{profile.Graphics.AntiAliasing}+aniso{profile.Graphics.AnisotropicLevel}",
            StringComparison.Ordinal)
            ? "profil reconnu"
            : "profil divergent";
        const string runtime = "politique persistée · aucun changement runtime appliqué dans Studio";
        return
        [
            new("Anticrénelage", HumanAntiAliasingUx2EnvK(profile.Graphics.AntiAliasing), fallback, runtime),
            new("Filtrage anisotrope", $"{profile.Graphics.AnisotropicLevel}×", fallback, runtime),
            new("Mise à l’échelle", HumanUpscalingUx2EnvK(profile.Graphics.Upscaling), fallback, runtime),
            new("Résolveur canonique", $"{resolvedGraphics} · eau {HumanWaterQualityUx2EnvK(decision.WaterQuality)}",
                fallback,
                "décision de politique uniquement · aucune sonde GPU · Godot reste autorité au runtime"),
        ];
    }

    private static IReadOnlyList<StudioUx2EnvKDiagnosticGroup> GroupWaterGraphicsDiagnosticsUx2EnvK(
        IReadOnlyList<StudioUx2EnvKDiagnostic> diagnostics)
    {
        var definitions = new[]
        {
            ("selection", "Sélection du profil"),
            ("water", "Politique d’eau"),
            ("graphics", "Qualité graphique"),
            ("command", "Commande canonique"),
        };
        return definitions.Select(definition =>
        {
            var items = diagnostics.Where(item => DiagnosticGroupKeyUx2EnvK(item.Code) == definition.Item1).ToArray();
            return new StudioUx2EnvKDiagnosticGroup(
                definition.Item1,
                definition.Item2,
                items.Any(item => item.Blocking) ? "Bloqué" : "Valide",
                items);
        }).ToArray();
    }

    private static string DiagnosticGroupKeyUx2EnvK(string code) => code switch
    {
        Ux2EnvKSelectionCode => "selection",
        Ux2EnvKWaterCode => "water",
        Ux2EnvKAntiAliasingCode or Ux2EnvKAnisotropyCode or Ux2EnvKUpscalingCode => "graphics",
        _ => "command",
    };

    private static StudioUx2EnvKPreview FailedWaterGraphicsPreviewUx2EnvK(
        string message,
        StudioUx2EnvKDiagnostic diagnostic) => new(
        false,
        message,
        "Aucune politique n’a été modifiée.",
        false,
        new([]),
        GroupWaterGraphicsDiagnosticsUx2EnvK([diagnostic]),
        [],
        [],
        false,
        true,
        true,
        true,
        true);

    private static string WaterGraphicsSourceFingerprintUx2EnvK(ProjectDefinition project)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            WriteTextUx2EnvK(writer, "ux2-env-k-source-v1");
            WriteTextUx2EnvK(writer, ProjectJsonSerializer.Serialize(project));
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static string WaterGraphicsDraftFingerprintUx2EnvK(StudioUx2EnvKDraft draft)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            WriteTextUx2EnvK(writer, "ux2-env-k-draft-v1");
            writer.Write(draft.BaseRevision.Value);
            WriteTextUx2EnvK(writer, draft.SourceFingerprint);
            writer.Write(draft.Refraction);
            writer.Write(draft.ScreenSpaceReflections);
            writer.Write(draft.DepthColor);
            WriteTextUx2EnvK(writer, draft.FallbackQuality);
            WriteTextUx2EnvK(writer, draft.AntiAliasing);
            writer.Write(draft.AnisotropicLevel);
            WriteTextUx2EnvK(writer, draft.Upscaling);
            writer.Write(draft.CapabilityFallback);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteTextUx2EnvK(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string HumanizeWaterGraphicsDiagnosticUx2EnvK(string? code) => code switch
    {
        "command.revision_conflict" => "Le projet a changé; rechargez le profil.",
        Ux2EnvKWaterCode => "Le repli d’eau est invalide.",
        Ux2EnvKAntiAliasingCode => "Le mode d’anticrénelage est invalide.",
        Ux2EnvKAnisotropyCode => "Le niveau anisotrope est invalide.",
        Ux2EnvKUpscalingCode => "La mise à l’échelle est invalide.",
        null or "" => "La commande canonique a refusé le profil.",
        _ => $"La validation canonique a refusé le profil ({code}).",
    };

    internal static string HumanWaterQualityUx2EnvK(string value) => value switch
    {
        "low" => "bas",
        "medium" => "moyen",
        "high" => "élevé",
        _ => "inconnu",
    };

    internal static string HumanAntiAliasingUx2EnvK(string value) => value switch
    {
        "disabled" => "désactivé",
        "fxaa" => "FXAA",
        "msaa2" => "MSAA 2×",
        "msaa4" => "MSAA 4×",
        "msaa8" => "MSAA 8×",
        _ => "inconnu",
    };

    internal static string HumanUpscalingUx2EnvK(string value) => value switch
    {
        "disabled" => "désactivée",
        "fsr" => "FSR",
        "native" => "résolution native",
        _ => "inconnue",
    };

    private static string HumanWaterTierUx2EnvK(WaterQualityTier value) => value switch
    {
        WaterQualityTier.Low => "basse",
        WaterQualityTier.Medium => "moyenne",
        WaterQualityTier.High => "élevée",
        WaterQualityTier.Ultra => "ultra",
        _ => "inconnue",
    };

    private static string HumanResourceUx2EnvK(string value)
    {
        var normalized = value.Replace('\\', '/');
        var leaf = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return string.IsNullOrWhiteSpace(leaf) ? "gouvernée" : leaf;
    }

    private static string HumanTokenUx2EnvK(string value) => string.Join(' ', value.Trim()
        .Replace('_', ' ').Replace('-', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static bool ContainsTechnicalIdentifierUx2EnvK(string text) =>
        text.Split([' ', '\n', '\r', '\t', ',', ';', '(', ')', '[', ']', '{', '}'],
                StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim('«', '»', '.', ':'))
            .Any(token => Guid.TryParse(token, out _));

    internal StudioUx2EnvKSmokeResult RunUx2EnvKContractSmoke()
    {
        var sourceProject = Project;
        if (sourceProject is null) return FailedUx2EnvKSmoke();
        var realModuleProfile =
            sourceProject.Module.RuntimePolicies.Extensions.PostLaunch.CreatorExperience.Id != Guid.Empty;
        var realModuleWater = sourceProject.Areas.Any(area => area.Environment.Water.Bodies.Count > 0);
        var tempRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux2-env-k-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        var cleanup = false;
        try
        {
            var contractProject = CreateUx2EnvKContractProject(sourceProject);
            var created = ProjectSession.Create(tempRoot, contractProject);
            if (!created.Succeeded || !editing.Open(tempRoot).Succeeded) return FailedUx2EnvKSmoke();
            var history = editing.History!;
            var explicitSelection = editing.CreateWaterGraphicsDraftUx2EnvK() is null &&
                                    editing.CreateWaterGraphicsDraftUx2EnvK(true) is not null;
            var baseDraft = editing.CreateWaterGraphicsDraftUx2EnvK(true)!;
            var invalidWater = editing.PreviewWaterGraphicsUx2EnvK(baseDraft with { FallbackQuality = "ultra" });
            var invalidAa = editing.PreviewWaterGraphicsUx2EnvK(baseDraft with { AntiAliasing = "taa" });
            var invalidAniso = editing.PreviewWaterGraphicsUx2EnvK(baseDraft with { AnisotropicLevel = 3 });
            var invalidUpscaling = editing.PreviewWaterGraphicsUx2EnvK(baseDraft with { Upscaling = "dlss" });

            var projectBeforeDryRun = editing.Project;
            var revisionBeforeDryRun = history.State.Revision;
            var journalBeforeDryRun = history.Journal.ToArray();
            var dirtyBeforeDryRun = editing.IsDirty;
            var canUndoBeforeDryRun = history.CanUndo;
            var canRedoBeforeDryRun = history.CanRedo;
            var dryDraft = ChangedWaterGraphicsDraftUx2EnvK(baseDraft);
            var dryPreview = editing.PreviewWaterGraphicsUx2EnvK(dryDraft);
            var dryStatePreserved = ReferenceEquals(projectBeforeDryRun, editing.Project) &&
                                    history.State.Revision == revisionBeforeDryRun &&
                                    history.Journal.SequenceEqual(journalBeforeDryRun) &&
                                    editing.IsDirty == dirtyBeforeDryRun &&
                                    history.CanUndo == canUndoBeforeDryRun && history.CanRedo == canRedoBeforeDryRun;
            var canonicalDryRun = dryPreview is { Succeeded: true, CanApply: true, CanonicalDryRun: true } &&
                                  dryStatePreserved;
            var waterPlanBounded = dryPreview.WaterPlans.Count >= 1 && dryPreview.AreaWaterReadOnly &&
                                   dryPreview.WaterPlans.All(item =>
                                       item.WaterBody != "Aucune masse d’eau de zone") &&
                                   dryPreview.WaterPlans.Any(item =>
                                       item.Physics.Contains("flottabilité", StringComparison.Ordinal) &&
                                       item.Physics.Contains("navigation", StringComparison.Ordinal)) &&
                                   dryPreview.Summary.Contains("lecture seule", StringComparison.Ordinal);
            var graphicsPlanBounded = dryPreview.GraphicsPlans.Count == 4 &&
                                      dryPreview.GraphicsPlans.All(item =>
                                          item.RuntimeState.Contains("runtime", StringComparison.Ordinal));

            var staleDraft = editing.CreateWaterGraphicsDraftUx2EnvK(true)!;
            var staleMutation = ApplyUx2EnvKRevisionMutation(editing, "draft");
            var stalePreview = editing.PreviewWaterGraphicsUx2EnvK(staleDraft);
            var stalePreviewBlocked = staleMutation && !stalePreview.Succeeded && editing.Undo().Succeeded;
            var staleApprovalDraft = ChangedWaterGraphicsDraftUx2EnvK(
                editing.CreateWaterGraphicsDraftUx2EnvK(true)!);
            var staleApprovalPreview = editing.PreviewWaterGraphicsUx2EnvK(staleApprovalDraft);
            var staleApprovalMutation = ApplyUx2EnvKRevisionMutation(editing, "approval");
            var staleApprovalApply = editing.ApplyApprovedWaterGraphicsUx2EnvK();
            var staleApprovalBlocked = staleApprovalPreview.Succeeded && staleApprovalMutation &&
                                       !staleApprovalApply.Succeeded && editing.Undo().Succeeded;
            var staleRevisionBlocked = stalePreviewBlocked && staleApprovalBlocked;

            var fingerprintDraft = ChangedWaterGraphicsDraftUx2EnvK(
                editing.CreateWaterGraphicsDraftUx2EnvK(true)!);
            var fingerprintPreview = editing.PreviewWaterGraphicsUx2EnvK(fingerprintDraft);
            var approval = editing.CaptureWaterGraphicsApprovalUx2EnvK();
            editing.RestoreWaterGraphicsApprovalUx2EnvK(approval with
            {
                Draft = approval.Draft! with { AnisotropicLevel = approval.Draft.AnisotropicLevel == 16 ? 8 : 16 },
            });
            var fingerprintApply = editing.ApplyApprovedWaterGraphicsUx2EnvK();
            var approvalFingerprintBlocked = fingerprintPreview.Succeeded && !fingerprintApply.Succeeded;

            var sourceState = editing.Project!;
            var sourcePolicies = sourceState.Module.RuntimePolicies;
            var sourceExtensions = sourcePolicies.Extensions;
            var sourcePost = sourceExtensions.PostLaunch;
            var sourceCreator = sourcePost.CreatorExperience;
            var fresh = ChangedWaterGraphicsDraftUx2EnvK(editing.CreateWaterGraphicsDraftUx2EnvK(true)!);
            if (!TryBuildWaterGraphicsProfileUx2EnvK(sourceState, fresh, out var expectedExtensions, out _) ||
                expectedExtensions is null)
                return FailedUx2EnvKSmoke();
            var preview = editing.PreviewWaterGraphicsUx2EnvK(fresh);
            var apply = editing.ApplyApprovedWaterGraphicsUx2EnvK();
            var finalProject = editing.Project!;
            var finalPolicies = finalProject.Module.RuntimePolicies;
            var finalExtensions = finalPolicies.Extensions;
            var finalPost = finalExtensions.PostLaunch;
            var finalCreator = finalPost.CreatorExperience;
            var journalTail = editing.History!.Journal.LastOrDefault();
            var applied = preview.Succeeded && preview.CanApply && apply.Succeeded &&
                          journalTail?.CommandName == "set_runtime_extensions" &&
                          finalCreator.Water.Refraction == fresh.Refraction &&
                          finalCreator.Water.ScreenSpaceReflections == fresh.ScreenSpaceReflections &&
                          finalCreator.Water.DepthColor == fresh.DepthColor &&
                          finalCreator.Water.FallbackQuality == fresh.FallbackQuality &&
                          finalCreator.Graphics.AntiAliasing == fresh.AntiAliasing &&
                          finalCreator.Graphics.AnisotropicLevel == fresh.AnisotropicLevel &&
                          finalCreator.Graphics.Upscaling == fresh.Upscaling &&
                          finalCreator.Graphics.CapabilityFallback == fresh.CapabilityFallback;

            var creatorTails = SameCreatorTailsUx2EnvK(sourceCreator, finalCreator);
            var postTails = ReferenceEquals(sourcePost.Gameplay, finalPost.Gameplay) &&
                            ReferenceEquals(sourcePost.LiveDirector, finalPost.LiveDirector) &&
                            ReferenceEquals(sourcePost.Network, finalPost.Network) &&
                            ReferenceEquals(sourcePost.Production, finalPost.Production) &&
                            sourcePost.Id == finalPost.Id;
            var extensionTails = sourceExtensions.RuleSchemas.SequenceEqual(finalExtensions.RuleSchemas) &&
                                 sourceExtensions.UiDocuments.SequenceEqual(finalExtensions.UiDocuments) &&
                                 sourceExtensions.JsonDocuments.SequenceEqual(finalExtensions.JsonDocuments) &&
                                 ReferenceEquals(sourceExtensions.SnapshotPolicy, finalExtensions.SnapshotPolicy) &&
                                 ReferenceEquals(sourceExtensions.ModernTooling, finalExtensions.ModernTooling) &&
                                 ReferenceEquals(sourceExtensions.OperationalReadiness,
                                     finalExtensions.OperationalReadiness) &&
                                 sourceExtensions.Id == finalExtensions.Id;
            var runtimePolicyTails = sourcePolicies.Heartbeats.SequenceEqual(finalPolicies.Heartbeats) &&
                                     ReferenceEquals(sourcePolicies.Rest, finalPolicies.Rest) &&
                                     ReferenceEquals(sourcePolicies.SequenceAbort, finalPolicies.SequenceAbort) &&
                                     ReferenceEquals(sourcePolicies.Cinematics, finalPolicies.Cinematics) &&
                                     ReferenceEquals(sourcePolicies.Gameplay, finalPolicies.Gameplay) &&
                                     ReferenceEquals(sourcePolicies.Rules, finalPolicies.Rules) &&
                                     ReferenceEquals(sourcePolicies.GameplayEvents, finalPolicies.GameplayEvents) &&
                                     ReferenceEquals(sourcePolicies.World, finalPolicies.World) &&
                                     ReferenceEquals(sourcePolicies.LifecycleEvents, finalPolicies.LifecycleEvents);
            var expectedPolicies = new ModuleRuntimePolicyProfile(
                sourcePolicies.Id,
                sourcePolicies.SchemaVersion,
                sourcePolicies.Heartbeats,
                sourcePolicies.Rest,
                sourcePolicies.SequenceAbort,
                "runtime-extension-command",
                sourcePolicies.Cinematics,
                sourcePolicies.Gameplay,
                sourcePolicies.Rules,
                expectedExtensions,
                sourcePolicies.GameplayEvents,
                sourcePolicies.World,
                sourcePolicies.LifecycleEvents);
            var expectedProject = new ProjectDefinition(
                sourceState.Id,
                sourceState.Title,
                sourceState.Module.WithRuntimePolicies(expectedPolicies),
                sourceState.Blueprints,
                sourceState.Areas,
                sourceState.Dialogues,
                sourceState.Quests);
            var projectNeighbors = string.Equals(ProjectJsonSerializer.Serialize(expectedProject),
                ProjectJsonSerializer.Serialize(finalProject), StringComparison.Ordinal);

            var finalJson = ProjectJsonSerializer.Serialize(finalProject);
            var undo = editing.Undo();
            var undone = undo.Succeeded && ReferenceEquals(sourceState, editing.Project);
            var redo = editing.Redo();
            var redone = redo.Succeeded && ReferenceEquals(finalProject, editing.Project);
            var applyUndoRedo = applied && undone && redone;
            var save = editing.Save();
            var reopen = reopened.Open(tempRoot);
            var persistence = save.Succeeded && reopen.Succeeded && reopened.Project is not null &&
                              string.Equals(finalJson, ProjectJsonSerializer.Serialize(reopened.Project!),
                                  StringComparison.Ordinal);
            var structuredDiagnostics = invalidWater.Groups.Count == 4 && invalidAa.Groups.Count == 4 &&
                                        invalidAniso.Groups.Count == 4 && invalidUpscaling.Groups.Count == 4 &&
                                        preview.Groups.Count == 4;
            var humanText = !ContainsTechnicalIdentifierUx2EnvK(
                string.Join(' ', editing.WaterGraphicsChoicesUx2EnvK.Select(choice => choice.Label)) + " " +
                preview.Message + " " + preview.Summary + " " + string.Join(' ', preview.Diff.Entries) + " " +
                string.Join(' ', preview.WaterPlans.Select(item => item.Area + " " + item.WaterBody)));
            var boundedRuntime = preview is
            {
                AreaWaterReadOnly: true,
                RendererMutationSkipped: true,
                HardwareDetectionSkipped: true,
                RuntimeClaimsBounded: true,
            } && preview.Summary.Contains("aucun matériau", StringComparison.Ordinal) &&
                 preview.Summary.Contains("aucun GPU", StringComparison.Ordinal);

            editing.Close();
            reopened.Close();
            cleanup = DeleteUx2EnvKTemporaryWorkspace(tempRoot);
            var succeeded = cleanup && explicitSelection && !invalidWater.Succeeded && !invalidAa.Succeeded &&
                            !invalidAniso.Succeeded && !invalidUpscaling.Succeeded && canonicalDryRun &&
                            waterPlanBounded && graphicsPlanBounded && staleRevisionBlocked &&
                            approvalFingerprintBlocked && applyUndoRedo && persistence && creatorTails && postTails &&
                            extensionTails && runtimePolicyTails && projectNeighbors && structuredDiagnostics &&
                            humanText && boundedRuntime;
            return new(
                succeeded,
                explicitSelection,
                realModuleProfile,
                realModuleWater,
                !invalidWater.Succeeded,
                !invalidAa.Succeeded,
                !invalidAniso.Succeeded,
                !invalidUpscaling.Succeeded,
                canonicalDryRun,
                waterPlanBounded,
                graphicsPlanBounded,
                staleRevisionBlocked,
                approvalFingerprintBlocked,
                applyUndoRedo,
                persistence,
                creatorTails,
                postTails,
                extensionTails,
                runtimePolicyTails,
                projectNeighbors,
                structuredDiagnostics,
                humanText,
                boundedRuntime,
                cleanup);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_ENV_K_CONTRACT_FAILED " +
                              $"type={exception.GetType().Name} message={exception.Message}");
            return FailedUx2EnvKSmoke();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            cleanup = DeleteUx2EnvKTemporaryWorkspace(tempRoot);
            if (!cleanup) Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_ENV_K_CLEANUP_FAILED");
        }
    }

    private static StudioUx2EnvKDraft ChangedWaterGraphicsDraftUx2EnvK(StudioUx2EnvKDraft source) => source with
    {
        Refraction = !source.Refraction,
        ScreenSpaceReflections = !source.ScreenSpaceReflections,
        DepthColor = !source.DepthColor,
        FallbackQuality = source.FallbackQuality == "high" ? "medium" : "high",
        AntiAliasing = source.AntiAliasing == "msaa8" ? "msaa4" : "msaa8",
        AnisotropicLevel = source.AnisotropicLevel == 16 ? 8 : 16,
        Upscaling = source.Upscaling == "fsr" ? "native" : "fsr",
        CapabilityFallback = !source.CapabilityFallback,
    };

    internal static ProjectDefinition CreateUx2EnvKContractProject(ProjectDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Areas.Count == 0) return source;
        var area = source.Areas[0];
        var environment = area.Environment;
        var body = new WaterBodyProfile(
            StableUx2EnvK(area.Id.Value, "water-quality-evidence"),
            "Rivière de preuve",
            WaterBodyKind.River,
            new Vector3Value(2, .25f, -3),
            new Vector3Value(18, 3, 36),
            maximumDepth: 3,
            flowDirectionX: .8f,
            flowDirectionZ: .2f,
            flowSpeed: 1.4f,
            buoyancy: 2.25f,
            navigationCost: 3.5f,
            swimmingEnabled: true,
            quality: WaterQualityTier.High,
            materialResource: string.Empty,
            audioCue: "water.river");
        var water = new WaterProfile(WaterProfile.CurrentSchemaVersion, [body]);
        var candidateEnvironment = new AreaEnvironmentProfile(
            environment.SchemaVersion,
            environment.Classification,
            environment.Tags,
            environment.Lighting,
            environment.Fog,
            environment.Audio,
            environment.Rendering,
            environment.Sky,
            environment.Wind,
            environment.Weather,
            water,
            environment.Reverb,
            environment.Authoring,
            environment.RequireAuthoringRuntimeMetadata);
        var candidateArea = area.WithEnvironment(candidateEnvironment);
        return new ProjectDefinition(
            source.Id,
            source.Title,
            source.Module,
            source.Blueprints,
            source.Areas.Select(item => item.Id == area.Id ? candidateArea : item).ToArray(),
            source.Dialogues,
            source.Quests);
    }

    private static Guid StableUx2EnvK(Guid ownerId, string discriminator)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"openrpg-forge:studio:ux2-env-k:{ownerId:D}:{discriminator}"));
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes[..16]);
    }

    private static bool SameCreatorTailsUx2EnvK(
        CreatorExperienceProductionProfile source,
        CreatorExperienceProductionProfile candidate) =>
        source.Id == candidate.Id && source.SchemaVersion == candidate.SchemaVersion &&
        source.Water.Id == candidate.Water.Id && source.Graphics.Id == candidate.Graphics.Id &&
        source.DeviceQueries.Id == candidate.DeviceQueries.Id &&
        source.DeviceQueries.Enabled == candidate.DeviceQueries.Enabled &&
        source.DeviceQueries.RequireConsent == candidate.DeviceQueries.RequireConsent &&
        source.DeviceQueries.RedactStableIdentifiers == candidate.DeviceQueries.RedactStableIdentifiers &&
        source.DeviceQueries.AllowedQueries.SequenceEqual(candidate.DeviceQueries.AllowedQueries) &&
        source.ResourceCommands.Id == candidate.ResourceCommands.Id &&
        source.ResourceCommands.Enabled == candidate.ResourceCommands.Enabled &&
        source.ResourceCommands.MaximumResults == candidate.ResourceCommands.MaximumResults &&
        source.ResourceCommands.MaximumBytes == candidate.ResourceCommands.MaximumBytes &&
        source.ResourceCommands.AllowedNamespaces.SequenceEqual(candidate.ResourceCommands.AllowedNamespaces) &&
        source.DynamicAreas.Id == candidate.DynamicAreas.Id &&
        source.DynamicAreas.Enabled == candidate.DynamicAreas.Enabled &&
        source.DynamicAreas.MaximumInstances == candidate.DynamicAreas.MaximumInstances &&
        source.DynamicAreas.LifetimeSeconds == candidate.DynamicAreas.LifetimeSeconds &&
        source.DynamicAreas.RequireServerAuthority == candidate.DynamicAreas.RequireServerAuthority &&
        source.DynamicAreas.ResolveDependencies == candidate.DynamicAreas.ResolveDependencies &&
        source.PartyControl.Id == candidate.PartyControl.Id &&
        source.PartyControl.Enabled == candidate.PartyControl.Enabled &&
        source.PartyControl.MaximumControlledMembers == candidate.PartyControl.MaximumControlledMembers &&
        source.PartyControl.AllowDirectSwitch == candidate.PartyControl.AllowDirectSwitch &&
        source.PartyControl.Commands.SequenceEqual(candidate.PartyControl.Commands) &&
        source.ProgressionTracks.Id == candidate.ProgressionTracks.Id &&
        source.ProgressionTracks.MaximumTracks == candidate.ProgressionTracks.MaximumTracks &&
        source.ProgressionTracks.DataDriven == candidate.ProgressionTracks.DataDriven &&
        source.ProgressionTracks.SupportLargeSetUi == candidate.ProgressionTracks.SupportLargeSetUi &&
        source.RepositoryMetadata.Id == candidate.RepositoryMetadata.Id &&
        source.RepositoryMetadata.Channel == candidate.RepositoryMetadata.Channel &&
        source.RepositoryMetadata.Compatibility == candidate.RepositoryMetadata.Compatibility &&
        source.RepositoryMetadata.RequireSignature == candidate.RepositoryMetadata.RequireSignature &&
        source.RepositoryMetadata.RequireChangeNotes == candidate.RepositoryMetadata.RequireChangeNotes &&
        source.Observability.Id == candidate.Observability.Id &&
        source.Observability.Enabled == candidate.Observability.Enabled &&
        source.Observability.TracyCompatible == candidate.Observability.TracyCompatible &&
        source.Observability.OpenTelemetryCompatible == candidate.Observability.OpenTelemetryCompatible &&
        source.Observability.MaximumCaptureSeconds == candidate.Observability.MaximumCaptureSeconds &&
        source.Observability.IncludeScripts == candidate.Observability.IncludeScripts;

    private static bool ApplyUx2EnvKRevisionMutation(StudioSessionController controller, string marker)
    {
        var history = controller.History;
        var project = controller.Project;
        if (history is null || project is null) return false;
        var source = project.Module.RuntimePolicies.Extensions;
        var candidate = new ModuleRuntimeExtensionProfile(
            source.Id,
            source.SchemaVersion,
            source.RuleSchemas,
            source.UiDocuments,
            source.JsonDocuments,
            source.SnapshotPolicy,
            $"studio:ux2-env-k:revision:{marker}",
            source.ModernTooling,
            source.OperationalReadiness,
            source.PostLaunch);
        var result = controller._setRuntimeExtensionsUx2EnvK.Handle(history.State,
            new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()), history.State.Revision, candidate, false));
        if (!result.Succeeded) return false;
        history.Record(result);
        return true;
    }

    internal static bool DeleteUx2EnvKTemporaryWorkspace(string? directory)
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
                !leaf.StartsWith("openrpg-forge-studio-ux2-env-k-", StringComparison.Ordinal)) return false;
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            return !Directory.Exists(root);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static StudioUx2EnvKSmokeResult FailedUx2EnvKSmoke() => new(
        false, false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false, false);
}
