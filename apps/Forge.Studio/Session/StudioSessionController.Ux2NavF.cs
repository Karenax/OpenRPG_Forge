using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Navigation;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2NavFProvenance = "studio:ux2-nav-f:area-transition";
    private readonly SetNavigationTopologyHandler _setNavigationTopologyUx2NavF = new(new SystemClock());
    private StudioUx2NavFDraft? _approvedPortalUx2NavF;
    private ProjectRevision? _approvedPortalRevisionUx2NavF;
    private string? _approvedPortalFingerprintUx2NavF;

    internal IReadOnlyList<StudioUx2NavFAreaChoice> PortalAreaChoicesUx2NavF
    {
        get
        {
            var project = Project;
            if (project is null) return [];
            return project.Areas.Select(area => new StudioUx2NavFAreaChoice(area.Id,
                    HumanAreaLabelUx2NavF(project, area), area.Portals.Count > 0))
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase).ToArray();
        }
    }

    internal IReadOnlyList<StudioUx2NavFPortalChoice> PortalChoicesUx2NavF(AreaId? areaId)
    {
        var area = areaId is null ? null : Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (area is null) return [];
        return area.Portals.Select(portal => new StudioUx2NavFPortalChoice(portal.Id,
                $"{HumanUx2NavF(portal.DisplayName, "Passage")} · " +
                $"vers {HumanAreaLabelUx2NavF(Project!, Project!.Areas.Single(item => item.Id == portal.DestinationAreaId))}"))
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    internal IReadOnlyList<StudioUx2NavFEntityChoice> SourceEntityChoicesUx2NavF(AreaId? areaId)
    {
        var area = areaId is null ? null : Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        return area is null ? [] : BuildEntityChoicesUx2NavF(Project!, area);
    }

    internal IReadOnlyList<StudioUx2NavFDestinationChoice> DestinationChoicesUx2NavF(AreaId? sourceAreaId)
    {
        var project = Project;
        if (project is null) return [];
        return project.Areas.Where(area => area.Id != sourceAreaId)
            .SelectMany(area => BuildEntityChoicesUx2NavF(project, area)
                .Select(entity => new StudioUx2NavFDestinationChoice(area.Id, entity.EntityId,
                    $"{HumanAreaLabelUx2NavF(project, area)} · {entity.Label}")))
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    internal StudioUx2NavFDraft? CreatePortalDraftUx2NavF(AreaId? areaId, PortalId? portalId)
    {
        var project = Project;
        var history = History;
        var area = areaId is null ? null : project?.Areas.SingleOrDefault(item => item.Id == areaId);
        var portal = portalId is null ? null : area?.Portals.SingleOrDefault(item => item.Id == portalId);
        if (project is null || history is null || area is null || portal is null) return null;
        return DraftFromPortalUx2NavF(project, history.State.Revision, area, portal, false);
    }

    internal StudioUx2NavFDraft? CreateNewPortalDraftUx2NavF(AreaId? areaId)
    {
        var project = Project;
        var history = History;
        var area = areaId is null ? null : project?.Areas.SingleOrDefault(item => item.Id == areaId);
        var source = area?.Entities.FirstOrDefault();
        var destination = project?.Areas.FirstOrDefault(item => item.Id != areaId && item.Entities.Count > 0);
        var target = destination?.Entities.FirstOrDefault();
        if (project is null || history is null || area is null || source is null || destination is null || target is null)
            return null;
        return new(area.Id, history.State.Revision, PortalSourceFingerprintUx2NavF(project),
            PortalId.From(Guid.NewGuid()), null, true, false, "Nouveau passage", source.Id,
            destination.Id, target.Id, false, true, PortalOrientationPolicy.AnchorRotation, 0,
            PortalVelocityPolicy.Reset, string.Empty, string.Empty, false);
    }

    private static StudioUx2NavFDraft DraftFromPortalUx2NavF(ProjectDefinition project,
        ProjectRevision revision, AreaDefinition area, PortalLink portal, bool remove) => new(
        area.Id, revision, PortalSourceFingerprintUx2NavF(project), portal.Id, portal, false, remove,
        portal.DisplayName, portal.SourceEntityId, portal.DestinationAreaId, portal.DestinationEntityId,
        portal.Bidirectional, portal.PreloadDestination, portal.Orientation, portal.FixedYawDegrees,
        portal.Velocity, portal.BeforeTransitionScript, portal.AfterTransitionScript,
        portal.HooksRequiredAtRuntime);

    internal StudioUx2NavFPreview PreviewPortalUx2NavF(StudioUx2NavFDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelPortalApprovalUx2NavF();
        var history = History;
        if (history is null || Project is null)
            return FailedPortalPreviewUx2NavF("Aucun module n’est ouvert.");
        if (!TryBuildPortalMutationUx2NavF(draft, true, out var command, out var candidate,
                out var diagnostics, out var message))
            return FailedPortalPreviewUx2NavF(message, diagnostics);
        var dryRun = _setNavigationTopologyUx2NavF.Handle(history.State, command!);
        if (!dryRun.Succeeded)
        {
            var items = dryRun.Diagnostics.Select(item => new StudioUx2NavFDiagnostic(item.Code,
                "navigation/passages", HumanDiagnosticUx2NavF(item.Message))).ToArray();
            return FailedPortalPreviewUx2NavF(items.FirstOrDefault()?.Message ??
                                              "Le passage a été refusé.", items);
        }

        var diff = BuildPortalDiffUx2NavF(draft);
        var forward = false;
        var reverse = false;
        var summary = draft.Remove
            ? "Suppression préparée · aucun déplacement exécuté."
            : "Transition préparée · aucun déplacement exécuté.";
        if (!draft.Remove)
        {
            try
            {
                var resolved = PortalLinkRuntimeService.Resolve(candidate!, draft.SourceAreaId,
                    draft.PortalId, new Vector3Value(1, 0, 2), 35);
                forward = resolved.DestinationAreaId == draft.DestinationAreaId;
                reverse = !draft.Bidirectional || PortalLinkRuntimeService.Resolve(candidate!,
                    draft.DestinationAreaId, draft.PortalId, Vector3Value.Zero, 0).Reversed;
                summary = $"Aller résolu vers {HumanAreaLabelUx2NavF(candidate!, candidate!.Areas.Single(item => item.Id == resolved.DestinationAreaId))} · " +
                          $"orientation {HumanOrientationUx2NavF(draft.Orientation)} · vitesse {HumanVelocityUx2NavF(draft.Velocity)} · " +
                          (draft.Bidirectional ? "retour résolu" : "sens unique");
            }
            catch (Exception exception)
            {
                return FailedPortalPreviewUx2NavF(HumanDiagnosticUx2NavF(exception.Message),
                [new("area.portal.runtime_resolution_failed", "navigation/passages/runtime",
                    HumanDiagnosticUx2NavF(exception.Message))]);
            }
        }
        _approvedPortalUx2NavF = draft;
        _approvedPortalRevisionUx2NavF = history.State.Revision;
        _approvedPortalFingerprintUx2NavF = PortalDraftFingerprintUx2NavF(draft);
        return new(true, diff.Count > 0, "Aperçu sec validé · aucun acteur, document ou runtime n’a été modifié.",
            summary, diff, [], forward, reverse, true);
    }

    internal StudioUx2NavFActionResult ApplyApprovedPortalUx2NavF()
    {
        var draft = _approvedPortalUx2NavF;
        var history = History;
        if (draft is null || history is null || _approvedPortalRevisionUx2NavF != history.State.Revision ||
            !string.Equals(_approvedPortalFingerprintUx2NavF, PortalDraftFingerprintUx2NavF(draft),
                StringComparison.Ordinal))
        {
            CancelPortalApprovalUx2NavF();
            return new(false, "Prévisualisez de nouveau le passage avant de l’appliquer.");
        }
        if (!TryBuildPortalMutationUx2NavF(draft, false, out var command, out _, out _, out var message))
        {
            CancelPortalApprovalUx2NavF();
            return new(false, message);
        }
        var currentDryRun = _setNavigationTopologyUx2NavF.Handle(history.State, command! with { DryRun = true });
        if (!currentDryRun.Succeeded)
        {
            CancelPortalApprovalUx2NavF();
            return new(false, HumanDiagnosticUx2NavF(currentDryRun.Diagnostics.FirstOrDefault()?.Message));
        }
        var applied = _setNavigationTopologyUx2NavF.Handle(history.State, command!);
        CancelPortalApprovalUx2NavF();
        if (!applied.Succeeded)
            return new(false, HumanDiagnosticUx2NavF(applied.Diagnostics.FirstOrDefault()?.Message));
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true, draft.Remove ? "Passage supprimé · Ctrl+Z pour annuler" :
            "Passage appliqué · Ctrl+Z pour annuler, Ctrl+Y pour rétablir");
    }

    internal void CancelPortalApprovalUx2NavF()
    {
        _approvedPortalUx2NavF = null;
        _approvedPortalRevisionUx2NavF = null;
        _approvedPortalFingerprintUx2NavF = null;
    }

    internal void CloseUx2NavF() => CancelPortalApprovalUx2NavF();

    internal StudioUx2NavFApprovalSnapshot CapturePortalApprovalUx2NavF() => new(
        _approvedPortalUx2NavF, _approvedPortalRevisionUx2NavF, _approvedPortalFingerprintUx2NavF);

    internal void RestorePortalApprovalUx2NavF(StudioUx2NavFApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedPortalUx2NavF = snapshot.Draft;
        _approvedPortalRevisionUx2NavF = snapshot.Revision;
        _approvedPortalFingerprintUx2NavF = snapshot.Fingerprint;
    }

    private bool TryBuildPortalMutationUx2NavF(StudioUx2NavFDraft draft, bool dryRun,
        out SetNavigationTopologyCommand? command, out ProjectDefinition? candidate,
        out IReadOnlyList<StudioUx2NavFDiagnostic> diagnostics, out string message)
    {
        command = null;
        candidate = null;
        diagnostics = [];
        message = string.Empty;
        var project = Project;
        var history = History;
        if (project is null || history is null)
        {
            message = "Aucun module n’est ouvert.";
            return false;
        }
        if (draft.BaseRevision != history.State.Revision)
        {
            message = "Le projet a changé. Rechargez le passage.";
            return false;
        }
        if (!string.Equals(draft.SourceFingerprint, PortalSourceFingerprintUx2NavF(project),
                StringComparison.Ordinal))
        {
            message = "La topologie du projet a changé. Rechargez le passage.";
            return false;
        }
        var area = project.Areas.SingleOrDefault(item => item.Id == draft.SourceAreaId);
        var current = area?.Portals.SingleOrDefault(item => item.Id == draft.PortalId);
        if (area is null || draft.IsNew && current is not null || !draft.IsNew &&
            (current is null || draft.Original is null || current != draft.Original))
        {
            message = "Le passage sélectionné n’est plus dans l’état attendu.";
            return false;
        }
        var local = ValidatePortalDraftUx2NavF(project, area, draft);
        if (local.Count > 0)
        {
            diagnostics = local;
            message = local[0].Message;
            return false;
        }
        PortalLink? portal = null;
        if (!draft.Remove)
        {
            portal = new PortalLink(draft.PortalId, draft.DisplayName, draft.SourceEntityId,
                draft.DestinationAreaId, draft.DestinationEntityId, draft.Bidirectional,
                draft.PreloadDestination, PortalLink.CurrentSchemaVersion,
                current?.Provenance ?? Ux2NavFProvenance, draft.Orientation,
                (float)draft.FixedYawDegrees, draft.Velocity, draft.BeforeTransitionScript,
                draft.AfterTransitionScript, draft.HooksRequiredAtRuntime);
        }
        var mutation = draft.Remove
            ? NavigationPortalMutation.Remove(area.Id, draft.PortalId)
            : NavigationPortalMutation.Upsert(area.Id, portal!);
        command = new SetNavigationTopologyCommand(CommandId.From(Guid.NewGuid()),
            history.State.Revision, [], [mutation], null, dryRun);
        var portals = draft.Remove
            ? area.Portals.Where(item => item.Id != draft.PortalId).ToArray()
            : current is null
                ? area.Portals.Append(portal!).ToArray()
                : area.Portals.Select(item => item.Id == draft.PortalId ? portal! : item).ToArray();
        var replacement = area.WithPortals(portals);
        candidate = new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints,
            project.Areas.Select(item => item.Id == area.Id ? replacement : item).ToArray(),
            project.Dialogues, project.Quests);
        return true;
    }

    private static IReadOnlyList<StudioUx2NavFDiagnostic> ValidatePortalDraftUx2NavF(
        ProjectDefinition project, AreaDefinition area, StudioUx2NavFDraft draft)
    {
        var result = new List<StudioUx2NavFDiagnostic>();
        if (draft.Remove) return draft.IsNew
            ? [new("area.portal.remove_new", "navigation/passages/suppression",
                "Un nouveau passage non appliqué ne peut pas être supprimé.")]
            : result;
        if (string.IsNullOrWhiteSpace(draft.DisplayName) || draft.DisplayName.Trim().Length > 120)
            result.Add(new("area.portal.name_invalid", "navigation/passages/nom",
                "Le nom du passage est requis et limité à 120 caractères."));
        if (!area.Entities.Any(item => item.Id == draft.SourceEntityId))
            result.Add(new("area.portal.source_entity_missing", "navigation/passages/départ",
                "Choisissez un objet de départ présent dans la zone source."));
        var destination = project.Areas.SingleOrDefault(item => item.Id == draft.DestinationAreaId);
        if (destination is null || !destination.Entities.Any(item => item.Id == draft.DestinationEntityId))
            result.Add(new("area.portal.destination_missing", "navigation/passages/destination",
                "Choisissez une destination existante dans une autre zone."));
        if (!double.IsFinite(draft.FixedYawDegrees) || draft.FixedYawDegrees < -360 ||
            draft.FixedYawDegrees > 360)
            result.Add(new("area.portal.yaw_invalid", "navigation/passages/orientation",
                "L’orientation fixe doit rester entre -360° et 360°."));
        if (draft.HooksRequiredAtRuntime && string.IsNullOrWhiteSpace(draft.BeforeTransitionScript) &&
            string.IsNullOrWhiteSpace(draft.AfterTransitionScript))
            result.Add(new("area.portal.hook_invalid", "navigation/passages/scripts",
                "Au moins un script est requis lorsque les hooks sont obligatoires."));
        foreach (var path in new[] { draft.BeforeTransitionScript, draft.AfterTransitionScript })
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            var normalized = path.Replace('\\', '/');
            if (!normalized.StartsWith("scripts/", StringComparison.Ordinal) ||
                !normalized.EndsWith(".forge", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("../", StringComparison.Ordinal) || Path.IsPathRooted(path))
                result.Add(new("area.portal.hook_invalid", "navigation/passages/scripts",
                    "Les scripts doivent être des chemins relatifs confinés sous scripts/ et finir par .forge."));
        }
        return result;
    }

    private static IReadOnlyList<string> BuildPortalDiffUx2NavF(StudioUx2NavFDraft draft)
    {
        if (draft.Remove) return [$"Retirer « {HumanUx2NavF(draft.DisplayName, "Passage")} ». "];
        var source = draft.Original;
        var result = new List<string>();
        if (source is null) result.Add($"Créer « {HumanUx2NavF(draft.DisplayName, "Passage")} ».");
        else
        {
            if (!string.Equals(source.DisplayName, draft.DisplayName, StringComparison.Ordinal))
                result.Add($"Renommer « {HumanUx2NavF(source.DisplayName, "Passage")} » en « {HumanUx2NavF(draft.DisplayName, "Passage")} ».");
            if (source.SourceEntityId != draft.SourceEntityId || source.DestinationAreaId != draft.DestinationAreaId ||
                source.DestinationEntityId != draft.DestinationEntityId)
                result.Add("Changer le départ ou la destination du passage.");
            if (source.Bidirectional != draft.Bidirectional || source.PreloadDestination != draft.PreloadDestination)
                result.Add("Modifier le retour bidirectionnel ou le préchargement.");
            if (source.Orientation != draft.Orientation ||
                !source.FixedYawDegrees.Equals((float)draft.FixedYawDegrees) || source.Velocity != draft.Velocity)
                result.Add("Modifier l’orientation ou la conservation de vitesse.");
            if (!string.Equals(source.BeforeTransitionScript, draft.BeforeTransitionScript, StringComparison.Ordinal) ||
                !string.Equals(source.AfterTransitionScript, draft.AfterTransitionScript, StringComparison.Ordinal) ||
                source.HooksRequiredAtRuntime != draft.HooksRequiredAtRuntime)
                result.Add("Modifier les hooks de transition.");
        }
        return result;
    }

    private static StudioUx2NavFPreview FailedPortalPreviewUx2NavF(string message,
        IReadOnlyList<StudioUx2NavFDiagnostic>? diagnostics = null) => new(false, false, message,
        string.Empty, [], diagnostics ?? [], false, false, true);

    private static IReadOnlyList<StudioUx2NavFEntityChoice> BuildEntityChoicesUx2NavF(
        ProjectDefinition project, AreaDefinition area)
    {
        var blueprints = project.Blueprints.ToDictionary(item => item.Id);
        return area.Entities.Select((entity, index) =>
        {
            var blueprint = blueprints.GetValueOrDefault(entity.BlueprintId);
            var label = blueprint is null ? $"Objet {index + 1}" :
                $"{HumanUx2NavF(blueprint.DisplayName, "Objet")} · {HumanUx2NavF(blueprint.Slug, "objet")} · n° {index + 1}";
            return new StudioUx2NavFEntityChoice(entity.Id, label);
        }).OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    private static string HumanAreaLabelUx2NavF(ProjectDefinition project, AreaDefinition area)
    {
        var name = HumanUx2NavF(area.DisplayName, "Zone");
        return project.Areas.Count(item => string.Equals(HumanUx2NavF(item.DisplayName, "Zone"), name,
            StringComparison.CurrentCultureIgnoreCase)) > 1 ? $"{name} · {HumanUx2NavF(area.Slug, "zone")}" : name;
    }

    internal static string HumanOrientationUx2NavF(PortalOrientationPolicy value) => value switch
    {
        PortalOrientationPolicy.Preserve => "Conserver l’orientation",
        PortalOrientationPolicy.AnchorRotation => "Orientation de l’arrivée",
        PortalOrientationPolicy.FixedYaw => "Orientation fixe",
        _ => "Orientation inconnue",
    };

    internal static string HumanVelocityUx2NavF(PortalVelocityPolicy value) => value switch
    {
        PortalVelocityPolicy.Preserve => "Conserver la vitesse",
        PortalVelocityPolicy.Reset => "Arrêt à l’arrivée",
        PortalVelocityPolicy.RedirectForward => "Rediriger vers l’avant",
        _ => "Vitesse inconnue",
    };

    internal static string HumanDiagnosticUx2NavF(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "La commande a été refusée.";
        var separator = value.IndexOf(": ", StringComparison.Ordinal);
        var text = separator >= 0 ? value[(separator + 2)..] : value;
        return text.Replace("UUID", "identité", StringComparison.OrdinalIgnoreCase)
            .Replace("ID", "référence", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool ContainsTechnicalIdentifierUx2NavF(string text) =>
        System.Text.RegularExpressions.Regex.IsMatch(text,
            "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}") ||
        text.Contains("/areas/", StringComparison.OrdinalIgnoreCase) ||
        text.Contains(".json", StringComparison.OrdinalIgnoreCase);

    private static string HumanUx2NavF(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().Replace('-', ' ');

    private static string PortalSourceFingerprintUx2NavF(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));

    private static string PortalDraftFingerprintUx2NavF(StudioUx2NavFDraft draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(draft.SourceAreaId.Value.ToByteArray());
        writer.Write(draft.PortalId.Value.ToByteArray());
        writer.Write(draft.IsNew); writer.Write(draft.Remove); WriteUx2NavF(writer, draft.DisplayName);
        writer.Write(draft.SourceEntityId.Value.ToByteArray());
        writer.Write(draft.DestinationAreaId.Value.ToByteArray());
        writer.Write(draft.DestinationEntityId.Value.ToByteArray());
        writer.Write(draft.Bidirectional); writer.Write(draft.PreloadDestination);
        writer.Write((int)draft.Orientation); writer.Write(draft.FixedYawDegrees);
        writer.Write((int)draft.Velocity); WriteUx2NavF(writer, draft.BeforeTransitionScript);
        WriteUx2NavF(writer, draft.AfterTransitionScript); writer.Write(draft.HooksRequiredAtRuntime);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteUx2NavF(BinaryWriter writer, string? value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length); writer.Write(bytes);
    }

    internal static ProjectDefinition? CreateUx2NavFEvidenceProject(ProjectDefinition source)
    {
        if (source.Areas.Any(area => area.Portals.Count > 0)) return source;
        var owner = source.Areas.FirstOrDefault(area => area.Entities.Count > 0);
        var target = source.Areas.FirstOrDefault(area => area.Id != owner?.Id && area.Entities.Count > 0);
        if (owner is null || target is null) return null;
        var portal = new PortalLink(PortalId.From(Guid.Parse("2f000000-0000-4000-8000-000000000001")),
            "Passage témoin", owner.Entities[0].Id, target.Id, target.Entities[0].Id, true, true,
            provenance: Ux2NavFProvenance);
        var replacement = owner.WithPortals(owner.Portals.Append(portal).ToArray());
        return new ProjectDefinition(source.Id, source.Title, source.Module, source.Blueprints,
            source.Areas.Select(area => area.Id == owner.Id ? replacement : area).ToArray(),
            source.Dialogues, source.Quests);
    }

    internal StudioUx2NavFSmokeResult RunUx2NavFContractSmoke()
    {
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceRevision = sourceHistory?.State.Revision;
        var sourceJournal = sourceHistory?.Journal.ToArray() ?? [];
        var sourceDirty = IsDirty;
        var sourceUndo = sourceHistory?.CanUndo;
        var sourceRedo = sourceHistory?.CanRedo;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var sourceNav = CaptureNavigationApprovalUx2NavB();
        var sourceWaypoint = CaptureWaypointApprovalUx2NavC();
        var sourcePatrol = CapturePatrolApprovalUx2NavD();
        var sourcePortal = CapturePortalApprovalUx2NavF();
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-nav-f-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        var cleanup = false;
        try
        {
            var fixture = sourceProject is null ? null : CreateUx2NavFEvidenceProject(sourceProject);
            if (fixture is null || !ProjectSession.Create(tempRoot, fixture).Succeeded ||
                !editing.Open(tempRoot).Succeeded) return FailedUx2NavFSmoke();
            var areaChoice = editing.PortalAreaChoicesUx2NavF.First(item => item.HasPortals);
            var portalChoice = editing.PortalChoicesUx2NavF(areaChoice.AreaId).First();
            var original = editing.CreatePortalDraftUx2NavF(areaChoice.AreaId, portalChoice.PortalId);
            var explicitSelection = editing.CreatePortalDraftUx2NavF(null, null) is null &&
                                    editing.CreateNewPortalDraftUx2NavF(null) is null;
            if (original is null) return FailedUx2NavFSmoke();
            var labels = areaChoice.Label + " " + portalChoice.Label + " " +
                         string.Join(' ', editing.SourceEntityChoicesUx2NavF(areaChoice.AreaId).Select(item => item.Label)) +
                         " " + string.Join(' ', editing.DestinationChoicesUx2NavF(areaChoice.AreaId).Select(item => item.Label));
            var humanLabels = !ContainsTechnicalIdentifierUx2NavF(labels);

            var invalidSource = editing.PreviewPortalUx2NavF(original with
            { SourceEntityId = EntityId.From(Guid.NewGuid()) });
            var invalidDestination = editing.PreviewPortalUx2NavF(original with
            { DestinationEntityId = EntityId.From(Guid.NewGuid()) });
            var invalidReferences = !invalidSource.Succeeded && !invalidDestination.Succeeded;
            var invalidHook = editing.PreviewPortalUx2NavF(original with
            {
                HooksRequiredAtRuntime = true,
                BeforeTransitionScript = string.Empty,
                AfterTransitionScript = string.Empty
            });
            var unsafeHook = editing.PreviewPortalUx2NavF(original with
            { BeforeTransitionScript = "../escape.forge" });
            var hookValidation = !invalidHook.Succeeded && !unsafeHook.Succeeded;

            var configured = original with
            {
                DisplayName = "Passage des lucioles",
                Bidirectional = true,
                PreloadDestination = !original.PreloadDestination,
                Orientation = PortalOrientationPolicy.FixedYaw,
                FixedYawDegrees = 127.5,
                Velocity = PortalVelocityPolicy.RedirectForward,
            };
            var beforeProject = editing.Project;
            var beforeHistory = editing.History;
            var beforeRevision = beforeHistory!.State.Revision;
            var beforeJournal = beforeHistory.Journal.ToArray();
            var preview = editing.PreviewPortalUx2NavF(configured);
            var dryRun = preview.Succeeded && preview.CanApply && ReferenceEquals(beforeProject, editing.Project) &&
                         ReferenceEquals(beforeHistory, editing.History) && beforeHistory.State.Revision == beforeRevision &&
                         beforeHistory.Journal.SequenceEqual(beforeJournal);
            var runtime = preview.ForwardResolved && preview.ReverseResolved && preview.NoRuntimeExecuted;
            var staleSource = !editing.PreviewPortalUx2NavF(configured with
            { SourceFingerprint = "stale" }).Succeeded;

            var approvalPreview = editing.PreviewPortalUx2NavF(configured);
            var mutation = original with { DisplayName = original.DisplayName + " témoin" };
            var changed = editing.TryBuildPortalMutationUx2NavF(mutation, false, out var mutationCommand,
                out _, out _, out _) && mutationCommand is not null;
            var mutationResult = changed
                ? editing._setNavigationTopologyUx2NavF.Handle(editing.History!.State, mutationCommand!)
                : null;
            if (mutationResult?.Succeeded == true) editing.History!.Record(mutationResult);
            var staleApply = editing.ApplyApprovedPortalUx2NavF();
            var staleApproval = approvalPreview.Succeeded && mutationResult?.Succeeded == true &&
                                !staleApply.Succeeded && editing.Undo().Succeeded;

            original = editing.CreatePortalDraftUx2NavF(areaChoice.AreaId, portalChoice.PortalId)!;
            configured = original with
            {
                DisplayName = "Passage des lucioles",
                Bidirectional = true,
                PreloadDestination = !original.PreloadDestination,
                Orientation = PortalOrientationPolicy.FixedYaw,
                FixedYawDegrees = 127.5,
                Velocity = PortalVelocityPolicy.RedirectForward,
            };
            var canonicalSource = editing.Project!;
            var ownerBefore = canonicalSource.Areas.Single(item => item.Id == areaChoice.AreaId);
            var neighborBefore = canonicalSource.Areas.Single(item => item.Id == original.DestinationAreaId);
            var fresh = editing.PreviewPortalUx2NavF(configured);
            var applied = editing.ApplyApprovedPortalUx2NavF();
            var appliedProject = editing.Project!;
            var ownerAfter = appliedProject.Areas.Single(item => item.Id == areaChoice.AreaId);
            var canonical = fresh.Succeeded && applied.Succeeded &&
                            editing.History!.Journal.Last().CommandName == "set_navigation_topology";
            var areaNeighbors = ownerBefore.Entities.SequenceEqual(ownerAfter.Entities) &&
                                ReferenceEquals(ownerBefore.Navigation, ownerAfter.Navigation) &&
                                ReferenceEquals(ownerBefore.Environment, ownerAfter.Environment) &&
                                ReferenceEquals(ownerBefore.World, ownerAfter.World) &&
                                ReferenceEquals(ownerBefore.RuntimePolicies, ownerAfter.RuntimePolicies) &&
                                ownerBefore.Portals.Where(item => item.Id != original.PortalId)
                                    .SequenceEqual(ownerAfter.Portals.Where(item => item.Id != original.PortalId));
            var projectNeighbors = ReferenceEquals(canonicalSource.Module, appliedProject.Module) &&
                                   ReferenceEquals(neighborBefore, appliedProject.Areas.Single(item => item.Id == neighborBefore.Id)) &&
                                   canonicalSource.Blueprints.SequenceEqual(appliedProject.Blueprints) &&
                                   canonicalSource.Dialogues.SequenceEqual(appliedProject.Dialogues) &&
                                   canonicalSource.Quests.SequenceEqual(appliedProject.Quests);
            var undo = editing.Undo();
            var redo = editing.Redo();
            var undoRedo = undo.Succeeded && redo.Succeeded;
            var save = editing.Save();
            var reopen = reopened.Open(tempRoot, areaChoice.AreaId.ToString());
            var persisted = reopened.Project?.Areas.Single(item => item.Id == areaChoice.AreaId)
                .Portals.Single(item => item.Id == original.PortalId);
            var persistence = save.Succeeded && reopen.Succeeded && persisted?.DisplayName == configured.DisplayName;

            var current = editing.CreatePortalDraftUx2NavF(areaChoice.AreaId, portalChoice.PortalId)!;
            var removePreview = editing.PreviewPortalUx2NavF(current with { Remove = true });
            var removed = editing.ApplyApprovedPortalUx2NavF();
            var removalObserved = removed.Succeeded && editing.Project!.Areas.Single(item => item.Id == areaChoice.AreaId)
                .Portals.All(item => item.Id != current.PortalId);
            var removeUndo = editing.Undo();
            var newDraft = editing.CreateNewPortalDraftUx2NavF(areaChoice.AreaId);
            var newPreview = newDraft is null ? FailedPortalPreviewUx2NavF("Brouillon absent.") :
                editing.PreviewPortalUx2NavF(newDraft);
            var newApplied = editing.ApplyApprovedPortalUx2NavF();
            var createUpdateRemove = removePreview.Succeeded && removalObserved && removeUndo.Succeeded &&
                                     newDraft is not null && newPreview.Succeeded && newApplied.Succeeded;
            var humanText = !ContainsTechnicalIdentifierUx2NavF(fresh.Summary + " " +
                            string.Join(' ', fresh.Diff));
            var sourceUntouched = ReferenceEquals(sourceProject, Project) && ReferenceEquals(sourceHistory, History) &&
                                  History?.State.Revision == sourceRevision && (History?.Journal ?? []).SequenceEqual(sourceJournal) &&
                                  IsDirty == sourceDirty && History?.CanUndo == sourceUndo && History?.CanRedo == sourceRedo &&
                                  ActiveAreaId == sourceArea && SelectedEntityIds.SequenceEqual(sourceSelection) &&
                                  SelectedEntityId == sourceSelected && CaptureNavigationApprovalUx2NavB() == sourceNav &&
                                  CaptureWaypointApprovalUx2NavC() == sourceWaypoint && CapturePatrolApprovalUx2NavD() == sourcePatrol &&
                                  CapturePortalApprovalUx2NavF() == sourcePortal;
            editing.Close();
            reopened.Close();
            cleanup = DeleteUx2NavFTemporaryWorkspace(tempRoot);
            var succeeded = explicitSelection && humanLabels && createUpdateRemove && invalidReferences &&
                            hookValidation && runtime && dryRun && staleSource && staleApproval && canonical &&
                            undoRedo && persistence && areaNeighbors && projectNeighbors && sourceUntouched && humanText && cleanup;
            return new(succeeded, explicitSelection, humanLabels, createUpdateRemove, invalidReferences,
                hookValidation, runtime, dryRun, staleSource, staleApproval, canonical, undoRedo,
                persistence, areaNeighbors, projectNeighbors, sourceUntouched, humanText, cleanup);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_NAV_F_CONTRACT_FAILED type={exception.GetType().Name} message={exception.Message}");
            return FailedUx2NavFSmoke();
        }
        finally
        {
            editing.Close(); reopened.Close();
            if (!cleanup) cleanup = DeleteUx2NavFTemporaryWorkspace(tempRoot);
            if (!cleanup) Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_NAV_F_TEMP_WORKSPACE_FAILED");
        }
    }

    internal static bool DeleteUx2NavFTemporaryWorkspace(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return true;
        try
        {
            var full = Path.GetFullPath(directory);
            if (!Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux2-nav-f-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(full)) Directory.Delete(full, true);
            return !Directory.Exists(full);
        }
        catch { return false; }
    }

    private static StudioUx2NavFSmokeResult FailedUx2NavFSmoke() => new(false, false, false,
        false, false, false, false, false, false, false, false, false, false, false, false,
        false, false, false);
}
