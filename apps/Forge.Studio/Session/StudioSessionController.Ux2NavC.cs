using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Navigation;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Core.Navigation;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2NavCProvenance = "studio:ux2-nav-c:waypoint-markers";
    private const string Ux2NavCEvidenceProvenance = "studio:ux2-nav-c:evidence";

    private readonly SetNavigationTopologyHandler _setNavigationTopologyUx2NavC = new(new SystemClock());
    private StudioUx2NavCDraft? _approvedWaypointUx2NavC;
    private ProjectRevision? _approvedWaypointRevisionUx2NavC;
    private string? _approvedWaypointFingerprintUx2NavC;

    internal IReadOnlyList<StudioUx2NavCAreaChoice> WaypointAreaChoicesUx2NavC
    {
        get
        {
            var project = Project;
            if (project is null)
            {
                return [];
            }
            var duplicateNames = project.Areas.GroupBy(item => HumanUx2NavC(item.DisplayName, "Zone"),
                    StringComparer.CurrentCultureIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(),
                    StringComparer.CurrentCultureIgnoreCase);
            return project.Areas.Select(item =>
                {
                    var name = HumanUx2NavC(item.DisplayName, "Zone");
                    var label = duplicateNames[name] > 1
                        ? $"{name} · {HumanUx2NavC(item.Slug, "zone")}" : name;
                    var evidence = item.Navigation.Waypoints.Waypoints.Any(waypoint =>
                        string.Equals(waypoint.Provenance, Ux2NavCEvidenceProvenance,
                            StringComparison.Ordinal));
                    return new StudioUx2NavCAreaChoice(item.Id, label, evidence);
                })
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    internal IReadOnlyList<StudioUx2NavCWaypointChoice> WaypointChoicesUx2NavC(AreaId? areaId)
    {
        var area = areaId is null ? null : Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (area is null)
        {
            return [];
        }
        return area.Navigation.Waypoints.Waypoints.Select(item =>
            {
                var name = HumanUx2NavC(item.Name, "Repère sans nom");
                var detail = item.Marker is { } marker
                    ? HumanUx2NavC(marker.Text, "note cartographique")
                    : "sans note cartographique";
                return new StudioUx2NavCWaypointChoice(item.Id, $"{name} · {detail}", item.Marker is not null);
            })
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.WaypointId)
            .ToArray();
    }

    internal StudioUx2NavCDraft? CreateWaypointDraftUx2NavC(AreaId? areaId, Guid? waypointId)
    {
        var history = History;
        var area = areaId is null ? null : Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (history is null || area is null || waypointId is null)
        {
            return null;
        }
        var waypoint = area.Navigation.Waypoints.Waypoints.SingleOrDefault(item => item.Id == waypointId.Value);
        return waypoint is null ? null : DraftFromWaypointUx2NavC(area, waypoint, history.State.Revision);
    }

    internal StudioUx2NavCDraft? CreateNewWaypointDraftUx2NavC(AreaId? areaId)
    {
        var history = History;
        var area = areaId is null ? null : Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (history is null || area is null)
        {
            return null;
        }
        var existing = area.Navigation.Waypoints.Waypoints.Select(item => item.Name).ToArray();
        var name = UniqueNameUx2NavC(existing, "Nouveau repère");
        return new StudioUx2NavCDraft(area.Id, Guid.NewGuid(), true, history.State.Revision,
            WaypointSourceFingerprintUx2NavC(area), HumanUx2NavC(area.DisplayName, "Zone"),
            area.Dimensions.Width, area.Dimensions.Depth, null, name, Vector3Value.Zero,
            "flag", "#F2B84B", "Repères", true, 28, true, name, "pin", 0,
            "exploration", 2, MarkerVisibilityPolicy.Always, MarkerVisibilityScope.Player,
            false, false, string.Empty, string.Empty, true);
    }

    private static StudioUx2NavCDraft DraftFromWaypointUx2NavC(AreaDefinition area,
        WaypointProfile waypoint, ProjectRevision revision)
    {
        var marker = waypoint.Marker;
        return new StudioUx2NavCDraft(area.Id, waypoint.Id, false, revision,
            WaypointSourceFingerprintUx2NavC(area), HumanUx2NavC(area.DisplayName, "Zone"),
            area.Dimensions.Width, area.Dimensions.Depth, waypoint, waypoint.Name, waypoint.Position,
            waypoint.Appearance.Icon, waypoint.Appearance.Color, waypoint.Appearance.Layer,
            waypoint.Appearance.Visible, waypoint.Appearance.ScreenSize, marker is not null,
            marker?.Text ?? waypoint.Name, marker?.Icon ?? "pin", marker?.Floor ?? 0,
            marker?.Category ?? "exploration", marker?.Precision ?? 2,
            marker?.VisibilityPolicy ?? MarkerVisibilityPolicy.Always,
            marker?.VisibilityScope ?? MarkerVisibilityScope.Player, marker?.Tracked ?? false,
            marker?.Revealed ?? false, marker?.RevealEvent ?? string.Empty,
            marker?.RevealAnimation ?? string.Empty, marker?.Persistent ?? true);
    }

    internal StudioUx2NavCPreview PreviewWaypointUx2NavC(StudioUx2NavCDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelWaypointUx2NavC();
        var history = History;
        if (history is null || Project is null)
        {
            return FailedWaypointPreviewUx2NavC("Aucun module n’est ouvert.");
        }
        if (!TryBuildWaypointMutationUx2NavC(draft, true, out var command, out var sourceArea,
                out var candidateArea, out var diagnostics, out var message))
        {
            return FailedWaypointPreviewUx2NavC(message, diagnostics);
        }
        var dryRun = _setNavigationTopologyUx2NavC.Handle(history.State, command!);
        if (!dryRun.Succeeded)
        {
            var commandDiagnostics = dryRun.Diagnostics.Select(item => new StudioUx2NavCDiagnostic(
                item.Code, HumanPathUx2NavC($"areas/{draft.AreaId}/navigation/waypoints"),
                HumanDiagnosticUx2NavC(item.Message))).ToArray();
            return FailedWaypointPreviewUx2NavC(commandDiagnostics.FirstOrDefault()?.Message ??
                                                "L’aperçu du repère a été refusé.", commandDiagnostics);
        }

        var builtArea = candidateArea!;
        var candidateWaypoint = builtArea.Navigation.Waypoints.Waypoints.Single(item =>
            item.Id == draft.WaypointId);
        var diff = BuildWaypointDiffUx2NavC(sourceArea!, candidateWaypoint, draft);
        var overlay = BuildWaypointOverlayUx2NavC(builtArea, draft.WaypointId);
        var appearanceMarkers = BuildWaypointAppearanceMarkersUx2NavC(builtArea, draft.WaypointId);
        var beforeReveal = MarkerVisibleUx2NavC(builtArea.Navigation.Waypoints, candidateWaypoint,
            WaypointRuntimeState.Empty);
        var reveal = candidateWaypoint.Marker is { } marker
            ? WaypointRuntimeService.Reveal(builtArea.Navigation.Waypoints, WaypointRuntimeState.Empty,
                marker.VisibilityScope, marker.RevealEvent)
            : new WaypointRevealResolution(WaypointRuntimeState.Empty, [], []);
        var afterReveal = MarkerVisibleUx2NavC(builtArea.Navigation.Waypoints, candidateWaypoint,
            reveal.State);
        if (diff.Count > 0)
        {
            _approvedWaypointUx2NavC = draft;
            _approvedWaypointRevisionUx2NavC = history.State.Revision;
            _approvedWaypointFingerprintUx2NavC = WaypointDraftFingerprintUx2NavC(draft);
        }
        var markerSummary = candidateWaypoint.Marker is null
            ? "sans note cartographique"
            : $"note « {HumanUx2NavC(candidateWaypoint.Marker.Text, "Repère")} » · " +
              $"{HumanVisibilityUx2NavC(candidateWaypoint.Marker.VisibilityPolicy)} · " +
              $"portée {HumanScopeUx2NavC(candidateWaypoint.Marker.VisibilityScope)}";
        return new StudioUx2NavCPreview(true,
            diff.Count == 0
                ? "Aperçu validé · aucune différence sémantique à appliquer."
                : "Aperçu sec validé · aucun document, événement runtime ou nœud moteur n’a été modifié.",
            $"{HumanUx2NavC(candidateWaypoint.Name, "Repère")} · {candidateWaypoint.Appearance.ScreenSize:0.#} px · " +
            $"couche {HumanUx2NavC(candidateWaypoint.Appearance.Layer, "Repères")} · {markerSummary}",
            diff.Count > 0, diff, [], overlay, appearanceMarkers, beforeReveal, afterReveal);
    }

    internal StudioUx2NavCActionResult ApplyApprovedWaypointUx2NavC()
    {
        var draft = _approvedWaypointUx2NavC;
        var history = History;
        if (draft is null || history is null || _approvedWaypointRevisionUx2NavC != history.State.Revision ||
            !string.Equals(_approvedWaypointFingerprintUx2NavC, WaypointDraftFingerprintUx2NavC(draft),
                StringComparison.Ordinal))
        {
            CancelWaypointUx2NavC();
            return new(false, "Prévisualisez de nouveau le repère avant de l’appliquer.");
        }
        if (!TryBuildWaypointMutationUx2NavC(draft, false, out var command, out _, out _, out _,
                out var message))
        {
            CancelWaypointUx2NavC();
            return new(false, message);
        }
        var currentPreview = _setNavigationTopologyUx2NavC.Handle(history.State, command! with { DryRun = true });
        if (!currentPreview.Succeeded)
        {
            CancelWaypointUx2NavC();
            return new(false, HumanDiagnosticUx2NavC(currentPreview.Diagnostics.FirstOrDefault()?.Message));
        }
        var applied = _setNavigationTopologyUx2NavC.Handle(history.State, command!);
        CancelWaypointUx2NavC();
        if (!applied.Succeeded)
        {
            return new(false, HumanDiagnosticUx2NavC(applied.Diagnostics.FirstOrDefault()?.Message));
        }
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true, "Repère appliqué · Ctrl+Z pour annuler, Ctrl+Y pour rétablir");
    }

    internal void CancelWaypointUx2NavC()
    {
        _approvedWaypointUx2NavC = null;
        _approvedWaypointRevisionUx2NavC = null;
        _approvedWaypointFingerprintUx2NavC = null;
    }

    internal void CloseUx2NavC() => CancelWaypointUx2NavC();

    internal StudioUx2NavCApprovalSnapshot CaptureWaypointApprovalUx2NavC() => new(
        _approvedWaypointUx2NavC, _approvedWaypointRevisionUx2NavC, _approvedWaypointFingerprintUx2NavC);

    internal void RestoreWaypointApprovalUx2NavC(StudioUx2NavCApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedWaypointUx2NavC = snapshot.Draft;
        _approvedWaypointRevisionUx2NavC = snapshot.Revision;
        _approvedWaypointFingerprintUx2NavC = snapshot.Fingerprint;
    }

    private bool TryBuildWaypointMutationUx2NavC(StudioUx2NavCDraft draft, bool dryRun,
        out SetNavigationTopologyCommand? command, out AreaDefinition? sourceArea,
        out AreaDefinition? candidateArea, out IReadOnlyList<StudioUx2NavCDiagnostic> diagnostics,
        out string message)
    {
        command = null;
        sourceArea = null;
        candidateArea = null;
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
            message = "Le projet a changé depuis l’ouverture de cette fiche. Rechargez le repère.";
            return false;
        }
        sourceArea = project.Areas.SingleOrDefault(item => item.Id == draft.AreaId);
        if (sourceArea is null)
        {
            message = "La zone choisie n’existe plus.";
            return false;
        }
        if (!string.Equals(draft.SourceFingerprint, WaypointSourceFingerprintUx2NavC(sourceArea),
                StringComparison.Ordinal))
        {
            message = "Les repères de cette zone ont changé. Rechargez la fiche avant de prévisualiser.";
            return false;
        }
        var inputDiagnostics = ValidateWaypointDraftUx2NavC(draft, sourceArea);
        if (inputDiagnostics.Count > 0)
        {
            diagnostics = inputDiagnostics;
            message = inputDiagnostics[0].Message;
            return false;
        }

        try
        {
            var sourceProfile = sourceArea.Navigation.Waypoints;
            var original = sourceProfile.Waypoints.SingleOrDefault(item => item.Id == draft.WaypointId);
            if (draft.IsNew && original is not null || !draft.IsNew && original is null)
            {
                message = draft.IsNew
                    ? "Un repère portant cette identité existe déjà. Recommencez l’ajout."
                    : "Le repère choisi n’existe plus. Rechargez la liste.";
                return false;
            }
            var appearance = new WaypointEditorAppearance(draft.AppearanceIcon.Trim(),
                draft.AppearanceColor.Trim(), draft.AppearanceLayer.Trim(), draft.AppearanceVisible,
                draft.AppearanceScreenSize);
            var marker = draft.HasMarker
                ? new MapMarker(draft.MarkerText.Trim(), draft.MarkerIcon.Trim(), draft.MarkerFloor,
                    draft.MarkerCategory.Trim(), draft.MarkerPrecision, draft.MarkerVisibility,
                    draft.MarkerScope, draft.MarkerTracked, draft.MarkerRevealed,
                    draft.MarkerRevealEvent.Trim(), draft.MarkerRevealAnimation.Trim(),
                    draft.MarkerPersistent)
                : null;
            var candidateWaypoint = original is null
                ? new WaypointProfile(draft.WaypointId, 1, Ux2NavCProvenance, draft.Name.Trim(),
                    draft.Position, appearance, marker)
                : new WaypointProfile(original.Id, original.SchemaVersion, original.Provenance,
                    draft.Name.Trim(), draft.Position, appearance, marker, original.AnchorInstanceId,
                    original.RequiredAtRuntime, original.ForgeScriptApiVersion, original.Arrival);
            var waypoints = sourceProfile.Waypoints.ToList();
            if (original is null)
            {
                waypoints.Add(candidateWaypoint);
            }
            else
            {
                waypoints[waypoints.FindIndex(item => item.Id == original.Id)] = candidateWaypoint;
            }
            var candidateProfile = new WaypointAuthoringProfile(sourceProfile.Id, sourceProfile.SchemaVersion,
                sourceProfile.Provenance, sourceProfile.AreaId, sourceProfile.RequiredAtRuntime, waypoints,
                sourceProfile.PatrolRoutes, sourceProfile.Locators, sourceProfile.Collections,
                sourceProfile.RouteVisualization);
            var navigation = sourceArea.Navigation.WithWaypoints(candidateProfile);
            candidateArea = sourceArea.WithNavigation(navigation);
            command = new SetNavigationTopologyCommand(CommandId.From(Guid.NewGuid()),
                draft.BaseRevision, [new(draft.AreaId, navigation)], [], null, dryRun);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           OverflowException)
        {
            message = HumanDiagnosticUx2NavC(exception.Message);
            diagnostics = [new("studio.waypoint.build_invalid", "navigation/repere",
                HumanDiagnosticUx2NavC(exception.Message))];
            return false;
        }
    }

    private static IReadOnlyList<StudioUx2NavCDiagnostic> ValidateWaypointDraftUx2NavC(
        StudioUx2NavCDraft draft, AreaDefinition area)
    {
        var result = new List<StudioUx2NavCDiagnostic>();
        var root = "navigation/repere";
        if (draft.WaypointId == Guid.Empty || string.IsNullOrWhiteSpace(draft.Name))
        {
            result.Add(new("studio.waypoint.identity_invalid", root,
                "Le repère requiert un nom humain et une identité stable."));
        }
        if (area.Navigation.Waypoints.Waypoints.Any(item => item.Id != draft.WaypointId &&
                string.Equals(item.Name, draft.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            result.Add(new("studio.waypoint.name_duplicate", $"{root}/nom",
                "Un autre repère de cette zone porte déjà ce nom."));
        }
        if (!InsideAreaUx2NavC(area, draft.Position))
        {
            result.Add(new("studio.waypoint.position_out_of_bounds", $"{root}/position",
                "La position doit rester dans les limites de la zone."));
        }
        if (string.IsNullOrWhiteSpace(draft.AppearanceIcon) ||
            string.IsNullOrWhiteSpace(draft.AppearanceLayer) || !ValidColorUx2NavC(draft.AppearanceColor) ||
            !double.IsFinite(draft.AppearanceScreenSize) || draft.AppearanceScreenSize is < 8 or > 96)
        {
            result.Add(new("waypoint.appearance.invalid", $"{root}/apparence",
                "L’apparence requiert une icône, une couche, une couleur #RRGGBB ou #RRGGBBAA et une taille de 8 à 96 px."));
        }
        if (draft.HasMarker && (string.IsNullOrWhiteSpace(draft.MarkerText) ||
                string.IsNullOrWhiteSpace(draft.MarkerIcon) || string.IsNullOrWhiteSpace(draft.MarkerCategory) ||
                draft.MarkerFloor is < -64 or > 64 || !double.IsFinite(draft.MarkerPrecision) ||
                draft.MarkerPrecision is < 0 or > 100))
        {
            result.Add(new("waypoint.marker.invalid", $"{root}/note-carte",
                "La note requiert texte, icône, catégorie, étage entre -64 et 64 et précision entre 0 et 100 m."));
        }
        if (draft.HasMarker && draft.MarkerVisibility == MarkerVisibilityPolicy.HiddenUntilRevealed &&
            (string.IsNullOrWhiteSpace(draft.MarkerRevealEvent) ||
             string.IsNullOrWhiteSpace(draft.MarkerRevealAnimation)))
        {
            result.Add(new("waypoint.marker.reveal_invalid", $"{root}/revelation",
                "Une note masquée requiert un événement et une animation de révélation nommés."));
        }
        return result;
    }

    private static StudioNavigationOverlaySnapshot BuildWaypointOverlayUx2NavC(AreaDefinition area,
        Guid selectedWaypointId) => new(area.Navigation.Waypoints.Waypoints.Select(item =>
            new StudioNavigationOverlayMarker($"waypoint:{item.Id:N}", HumanUx2NavC(item.Name, "Repère"),
                StudioNavigationOverlaySemantic.Visibility,
                new StudioNavigationPoint(item.Position.X, item.Position.Y, item.Position.Z),
                item.Id == selectedWaypointId,
                false))
            .ToArray(), [], []);

    private static IReadOnlyList<StudioUx2NavCAppearanceMarker> BuildWaypointAppearanceMarkersUx2NavC(
        AreaDefinition area, Guid selectedWaypointId) => area.Navigation.Waypoints.Waypoints.Select(item =>
            new StudioUx2NavCAppearanceMarker(HumanUx2NavC(item.Name, "Repère"),
                new StudioNavigationPoint(item.Position.X, item.Position.Y, item.Position.Z),
                item.Appearance.Icon, item.Appearance.Color, item.Appearance.Layer,
                item.Appearance.Visible, item.Appearance.ScreenSize, item.Id == selectedWaypointId))
            .ToArray();

    private static IReadOnlyList<string> BuildWaypointDiffUx2NavC(AreaDefinition sourceArea,
        WaypointProfile candidate, StudioUx2NavCDraft draft)
    {
        var original = sourceArea.Navigation.Waypoints.Waypoints.SingleOrDefault(item =>
            item.Id == candidate.Id);
        var result = new List<string>();
        if (original is null)
        {
            result.Add($"Ajouter le repère « {HumanUx2NavC(candidate.Name, "Nouveau repère")} ».");
        }
        else
        {
            if (!string.Equals(original.Name, candidate.Name, StringComparison.Ordinal) ||
                original.Position != candidate.Position)
            {
                result.Add("Mettre à jour le nom ou la position du repère.");
            }
            if (original.Appearance != candidate.Appearance)
            {
                result.Add($"Apparence : {HumanUx2NavC(candidate.Appearance.Icon, "icône")} · " +
                           $"{candidate.Appearance.ScreenSize:0.#} px · couche " +
                           $"{HumanUx2NavC(candidate.Appearance.Layer, "Repères")}.");
            }
            if (original.Marker != candidate.Marker)
            {
                result.Add(candidate.Marker is null
                    ? "Retirer la note cartographique du repère."
                    : $"Note cartographique : « {HumanUx2NavC(candidate.Marker.Text, "Repère")} » · " +
                      $"{HumanVisibilityUx2NavC(candidate.Marker.VisibilityPolicy)}.");
            }
        }
        if (draft.IsNew)
        {
            result.Add($"Apparence : {HumanUx2NavC(candidate.Appearance.Icon, "icône")} · " +
                       $"{candidate.Appearance.ScreenSize:0.#} px · couche " +
                       $"{HumanUx2NavC(candidate.Appearance.Layer, "Repères")}.");
            if (candidate.Marker is not null)
            {
                result.Add($"Note cartographique : « {HumanUx2NavC(candidate.Marker.Text, "Repère")} » · " +
                           $"{HumanVisibilityUx2NavC(candidate.Marker.VisibilityPolicy)}.");
            }
        }
        return result;
    }

    private static StudioUx2NavCPreview FailedWaypointPreviewUx2NavC(string message,
        IReadOnlyList<StudioUx2NavCDiagnostic>? diagnostics = null) => new(false, message,
        "Aucun aperçu disponible.", false, [], diagnostics ?? [], StudioNavigationOverlaySnapshot.Empty,
        [], false, false);

    private static bool MarkerVisibleUx2NavC(WaypointAuthoringProfile profile, WaypointProfile waypoint,
        WaypointRuntimeState state) => waypoint.Marker is { } marker &&
            WaypointRuntimeService.VisibleMarkers(profile, state, marker.VisibilityScope)
                .Any(item => item.WaypointId == waypoint.Id);

    private static bool InsideAreaUx2NavC(AreaDefinition area, Vector3Value position) =>
        float.IsFinite(position.X) && float.IsFinite(position.Y) && float.IsFinite(position.Z) &&
        Math.Abs(position.X) <= area.Dimensions.Width / 2f &&
        Math.Abs(position.Z) <= area.Dimensions.Depth / 2f && Math.Abs(position.Y) <= 4096;

    private static bool ValidColorUx2NavC(string value) => value is { Length: 7 or 9 } && value[0] == '#' &&
        value.AsSpan(1).ToArray().All(char.IsAsciiHexDigit);

    private static string WaypointSourceFingerprintUx2NavC(AreaDefinition area)
    {
        var json = AreaJsonSerializer.Serialize(area);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    private static string WaypointDraftFingerprintUx2NavC(StudioUx2NavCDraft draft)
    {
        var values = new[]
        {
            draft.AreaId.ToString(), draft.WaypointId.ToString("D"), draft.IsNew.ToString(),
            draft.BaseRevision.ToString(), draft.SourceFingerprint, draft.Name,
            F(draft.Position.X), F(draft.Position.Y), F(draft.Position.Z), draft.AppearanceIcon,
            draft.AppearanceColor, draft.AppearanceLayer, draft.AppearanceVisible.ToString(),
            F(draft.AppearanceScreenSize), draft.HasMarker.ToString(), draft.MarkerText,
            draft.MarkerIcon, draft.MarkerFloor.ToString(CultureInfo.InvariantCulture),
            draft.MarkerCategory, F(draft.MarkerPrecision), draft.MarkerVisibility.ToString(),
            draft.MarkerScope.ToString(), draft.MarkerTracked.ToString(), draft.MarkerRevealed.ToString(),
            draft.MarkerRevealEvent, draft.MarkerRevealAnimation, draft.MarkerPersistent.ToString(),
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\u001f', values))));

        static string F(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static string HumanDiagnosticUx2NavC(string? message)
    {
        var text = string.IsNullOrWhiteSpace(message) ? "La commande a été refusée." : message.Trim();
        return System.Text.RegularExpressions.Regex.Replace(text,
            @"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b",
            "référence interne", System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                                  System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }

    private static string HumanPathUx2NavC(string path) => System.Text.RegularExpressions.Regex.Replace(path,
        @"[0-9a-f]{8}-[0-9a-f-]{27,}", "zone-selectionnee",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase |
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static string HumanUx2NavC(string? value, string fallback)
    {
        var text = value?.Trim();
        return string.IsNullOrWhiteSpace(text) || Guid.TryParse(text, out _) ? fallback : text;
    }

    private static string UniqueNameUx2NavC(IEnumerable<string> existing, string proposed)
    {
        var values = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = proposed;
        var suffix = 2;
        while (values.Contains(result))
        {
            result = $"{proposed} {suffix++}";
        }
        return result;
    }

    private static string HumanVisibilityUx2NavC(MarkerVisibilityPolicy policy) => policy switch
    {
        MarkerVisibilityPolicy.Always => "toujours visible",
        MarkerVisibilityPolicy.HiddenUntilRevealed => "masquée jusqu’à révélation",
        MarkerVisibilityPolicy.WhileTracked => "visible pendant le suivi",
        _ => "politique héritée",
    };

    private static string HumanScopeUx2NavC(MarkerVisibilityScope scope) => scope switch
    {
        MarkerVisibilityScope.Player => "joueur",
        MarkerVisibilityScope.Party => "groupe",
        MarkerVisibilityScope.World => "monde",
        _ => "héritée",
    };

    internal ProjectDefinition? CreateUx2NavCEvidenceProject()
    {
        var source = Project;
        return source is null ? null : CreateUx2NavCEvidenceProject(source.Id.Value);
    }

    private static ProjectDefinition CreateUx2NavCEvidenceProject(Guid seed)
    {
        var project = CreateUx2NavBEvidenceProject(seed == Guid.Empty
            ? Guid.Parse("2c000000-0000-4000-8000-000000000050") : seed);
        var area = project.Areas.Single(item => item.Slug == "clairiere-navigation");
        var profile = area.Navigation.Waypoints;
        var first = new WaypointProfile(Guid.Parse("2c000000-0000-4000-8000-000000000001"), 1,
            Ux2NavCEvidenceProvenance, "Autel des braises", new Vector3Value(-2, 0, 3),
            new WaypointEditorAppearance("flame", "#F3A847", "Quêtes", true, 30),
            new MapMarker("Autel des braises", "ember", -1, "quête", 2,
                MarkerVisibilityPolicy.HiddenUntilRevealed, MarkerVisibilityScope.Party, true, false,
                "quest.ashes.discovered", "pulse", true),
            Guid.Parse("2b000000-0000-4000-8000-000000000020"), true, "1.0",
            new ArrivalBehavior(ArrivalFacingMode.Direction, 90, null, "look", .5, null,
                "waypoint.arrived"));
        var neighbor = new WaypointProfile(Guid.Parse("2c000000-0000-4000-8000-000000000002"), 1,
            "fixture:neighbor", "Sentier discret", new Vector3Value(5, 0, -4),
            new WaypointEditorAppearance("arrow", "#72C7FF", "Exploration", false, 22),
            new MapMarker("Sentier discret", "path", 0, "exploration", 4,
                MarkerVisibilityPolicy.Always, MarkerVisibilityScope.Player, false, true,
                string.Empty, string.Empty, false), null, false, "1.0", null);
        var points = new[]
        {
            new PatrolPoint(Guid.Parse("2c000000-0000-4000-8000-000000000011"), new(-5, 0, -5),
                1, 0, 2, "observe", null),
            new PatrolPoint(Guid.Parse("2c000000-0000-4000-8000-000000000012"), new(5, 0, 5),
                2, 180, 2, null, null),
        };
        var route = new PatrolRoute(Guid.Parse("2c000000-0000-4000-8000-000000000010"), 1,
            "fixture:neighbor", PatrolTraversalPolicy.PingPong, points);
        var locator = new GenericLocator(Guid.Parse("2c000000-0000-4000-8000-000000000020"), 1,
            "fixture:neighbor", "Éclat sonore", new Vector3Value(1, 0, 1), LocatorPurpose.Audio,
            ["ambiance"], null, "audio.embers");
        var collection = new AnchorCollection(Guid.Parse("2c000000-0000-4000-8000-000000000021"), 1,
            "fixture:neighbor", "ambiance", [locator.Id], "Ambiances", LocatorPurpose.Audio,
            ["ambiance"], LocatorSelectionMode.Stable);
        var visualization = new RouteVisualizationProfile(
            Guid.Parse("2c000000-0000-4000-8000-000000000030"), 1, "fixture:neighbor", true,
            true, false, true, true, true, true, 1.25);
        var waypoints = new WaypointAuthoringProfile(profile.Id, profile.SchemaVersion, profile.Provenance,
            area.Id, profile.RequiredAtRuntime, [first, neighbor], [route], [locator], [collection],
            visualization);
        var replacement = area.WithNavigation(area.Navigation.WithWaypoints(waypoints));
        var areas = project.Areas.Select(item => item.Id == area.Id ? replacement : item).ToArray();
        return new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints, areas,
            project.Dialogues, project.Quests);
    }

    internal StudioUx2NavCSmokeResult RunUx2NavCContractSmoke()
    {
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceRevision = sourceHistory?.State.Revision;
        var sourceJournal = sourceHistory?.Journal.ToArray() ?? [];
        var sourceDirty = IsDirty;
        var sourceUndo = sourceHistory?.CanUndo;
        var sourceRedo = sourceHistory?.CanRedo;
        var sourceApproval = CaptureWaypointApprovalUx2NavC();
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-nav-c-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var fixture = CreateUx2NavCEvidenceProject(Guid.Parse("2c000000-0000-4000-8000-000000000060"));
            var created = ProjectSession.Create(tempRoot, fixture);
            if (!created.Succeeded || !editing.Open(tempRoot).Succeeded)
            {
                return FailedUx2NavCSmoke();
            }
            var areaChoice = editing.WaypointAreaChoicesUx2NavC.Single(item => item.IsEvidence);
            var waypointChoice = editing.WaypointChoicesUx2NavC(areaChoice.AreaId)
                .Single(item => item.Label.StartsWith("Autel des braises", StringComparison.Ordinal));
            var explicitSelection = editing.CreateWaypointDraftUx2NavC(null, waypointChoice.WaypointId) is null &&
                                    editing.CreateWaypointDraftUx2NavC(areaChoice.AreaId, null) is null;
            var original = editing.CreateWaypointDraftUx2NavC(areaChoice.AreaId, waypointChoice.WaypointId);
            var newDraft = editing.CreateNewWaypointDraftUx2NavC(areaChoice.AreaId);
            var createSupported = newDraft is { IsNew: true, Original: null } &&
                                  editing.PreviewWaypointUx2NavC(newDraft).Succeeded;
            if (original is null)
            {
                return FailedUx2NavCSmoke();
            }
            var humanLabels = editing.WaypointAreaChoicesUx2NavC.All(item =>
                                  !ContainsTechnicalIdentifierUx2NavC(item.Label)) &&
                              editing.WaypointChoicesUx2NavC(areaChoice.AreaId).All(item =>
                                  !ContainsTechnicalIdentifierUx2NavC(item.Label));
            var invalidAppearance = editing.PreviewWaypointUx2NavC(original with
            {
                AppearanceScreenSize = 140,
            });
            var invalidMarker = editing.PreviewWaypointUx2NavC(original with
            {
                MarkerVisibility = MarkerVisibilityPolicy.HiddenUntilRevealed,
                MarkerRevealEvent = string.Empty,
                MarkerRevealAnimation = string.Empty,
            });
            var appearanceValidation = !invalidAppearance.Succeeded && invalidAppearance.Diagnostics.Any(item =>
                item.Code == "waypoint.appearance.invalid");
            var markerValidation = !invalidMarker.Succeeded && invalidMarker.Diagnostics.Any(item =>
                item.Code == "waypoint.marker.reveal_invalid");
            var configured = original with
            {
                AppearanceIcon = "beacon",
                AppearanceColor = "#E66A3C",
                AppearanceLayer = "Quêtes principales",
                AppearanceVisible = true,
                AppearanceScreenSize = 36,
                MarkerText = "Autel révélé",
                MarkerIcon = "ember",
                MarkerFloor = 1,
                MarkerCategory = "quête principale",
                MarkerPrecision = 1.5,
                MarkerVisibility = MarkerVisibilityPolicy.HiddenUntilRevealed,
                MarkerScope = MarkerVisibilityScope.Party,
                MarkerTracked = true,
                MarkerRevealed = false,
                MarkerRevealEvent = "quest.ashes.revealed",
                MarkerRevealAnimation = "pulse",
                MarkerPersistent = true,
            };
            var historyBefore = editing.History!;
            var projectBefore = editing.Project;
            var revisionBefore = historyBefore.State.Revision;
            var journalBefore = historyBefore.Journal.ToArray();
            var preview = editing.PreviewWaypointUx2NavC(configured);
            var dryRun = preview.Succeeded && preview.CanApply && ReferenceEquals(projectBefore, editing.Project) &&
                         ReferenceEquals(historyBefore, editing.History) &&
                         historyBefore.State.Revision == revisionBefore &&
                         historyBefore.Journal.SequenceEqual(journalBefore);
            var revealPolicy = !preview.MarkerVisibleBeforeReveal && preview.MarkerVisibleAfterReveal;

            var staleSource = editing.PreviewWaypointUx2NavC(configured with
            {
                SourceFingerprint = "stale",
            });
            var staleSourceBlocked = !staleSource.Succeeded;

            var staleDraft = editing.CreateWaypointDraftUx2NavC(areaChoice.AreaId, waypointChoice.WaypointId)!;
            var otherArea = editing.Project!.Areas.Single(item => item.Id != areaChoice.AreaId);
            var neighborChanged = ApplyUx2NavCNeighborMutation(editing, otherArea);
            var stalePreview = editing.PreviewWaypointUx2NavC(staleDraft);
            var staleRevisionBlocked = neighborChanged && !stalePreview.Succeeded && editing.Undo().Succeeded;

            configured = editing.CreateWaypointDraftUx2NavC(areaChoice.AreaId, waypointChoice.WaypointId)! with
            {
                AppearanceScreenSize = 36,
                MarkerText = "Autel révélé",
                MarkerRevealEvent = "quest.ashes.revealed",
            };
            var approvalPreview = editing.PreviewWaypointUx2NavC(configured);
            otherArea = editing.Project!.Areas.Single(item => item.Id != areaChoice.AreaId);
            neighborChanged = ApplyUx2NavCNeighborMutation(editing, otherArea);
            var staleApply = editing.ApplyApprovedWaypointUx2NavC();
            var staleApprovalBlocked = approvalPreview.Succeeded && neighborChanged && !staleApply.Succeeded &&
                                       editing.Undo().Succeeded;

            var canonicalSource = editing.Project!;
            var sourceArea = canonicalSource.Areas.Single(item => item.Id == areaChoice.AreaId);
            var sourceNeighbor = canonicalSource.Areas.Single(item => item.Id != areaChoice.AreaId);
            var sourceWaypoint = sourceArea.Navigation.Waypoints.Waypoints.Single(item =>
                item.Id == waypointChoice.WaypointId);
            configured = editing.CreateWaypointDraftUx2NavC(areaChoice.AreaId, waypointChoice.WaypointId)! with
            {
                AppearanceIcon = "beacon",
                AppearanceColor = "#E66A3C",
                AppearanceLayer = "Quêtes principales",
                AppearanceScreenSize = 36,
                MarkerText = "Autel révélé",
                MarkerFloor = 1,
                MarkerCategory = "quête principale",
                MarkerPrecision = 1.5,
                MarkerVisibility = MarkerVisibilityPolicy.HiddenUntilRevealed,
                MarkerScope = MarkerVisibilityScope.Party,
                MarkerTracked = true,
                MarkerRevealed = false,
                MarkerRevealEvent = "quest.ashes.revealed",
                MarkerRevealAnimation = "pulse",
                MarkerPersistent = true,
            };
            var freshPreview = editing.PreviewWaypointUx2NavC(configured);
            var appearanceMarker = freshPreview.AppearanceMarkers.SingleOrDefault(item => item.Selected);
            appearanceValidation &= appearanceMarker is
            {
                Icon: "beacon",
                Color: "#E66A3C",
                Layer: "Quêtes principales",
                Visible: true,
                ScreenSize: 36,
            };
            var applied = editing.ApplyApprovedWaypointUx2NavC();
            var appliedProject = editing.Project!;
            var appliedArea = appliedProject.Areas.Single(item => item.Id == areaChoice.AreaId);
            var appliedWaypoint = appliedArea.Navigation.Waypoints.Waypoints.Single(item =>
                item.Id == waypointChoice.WaypointId);
            markerValidation &= appliedWaypoint.Marker is
            {
                Text: "Autel révélé",
                Icon: "ember",
                Floor: 1,
                Category: "quête principale",
                Precision: 1.5,
                Tracked: true,
                Revealed: false,
            };
            var wrongScope = WaypointRuntimeService.Reveal(appliedArea.Navigation.Waypoints,
                WaypointRuntimeState.Empty, MarkerVisibilityScope.Player, "quest.ashes.revealed");
            var wrongEvent = WaypointRuntimeService.Reveal(appliedArea.Navigation.Waypoints,
                WaypointRuntimeState.Empty, MarkerVisibilityScope.Party, "quest.other");
            var correctReveal = WaypointRuntimeService.Reveal(appliedArea.Navigation.Waypoints,
                WaypointRuntimeState.Empty, MarkerVisibilityScope.Party, "quest.ashes.revealed");
            revealPolicy &= wrongScope.RevealedWaypointIds.Count == 0 &&
                            wrongEvent.RevealedWaypointIds.Count == 0 &&
                            correctReveal.RevealedWaypointIds.SequenceEqual([appliedWaypoint.Id]) &&
                            appliedWaypoint.Marker is
                            {
                                Persistent: true,
                                RevealAnimation: "pulse",
                                VisibilityScope: MarkerVisibilityScope.Party,
                            } &&
                            MarkerVisibleUx2NavC(appliedArea.Navigation.Waypoints, appliedWaypoint,
                                correctReveal.State);
            var canonicalCommand = freshPreview.Succeeded && applied.Succeeded &&
                                   editing.History!.Journal.LastOrDefault()?.CommandName ==
                                   "set_navigation_topology";
            var hiddenFieldsPreserved = appliedWaypoint.Id == sourceWaypoint.Id &&
                                        appliedWaypoint.SchemaVersion == sourceWaypoint.SchemaVersion &&
                                        appliedWaypoint.Provenance == sourceWaypoint.Provenance &&
                                        appliedWaypoint.AnchorInstanceId == sourceWaypoint.AnchorInstanceId &&
                                        appliedWaypoint.RequiredAtRuntime == sourceWaypoint.RequiredAtRuntime &&
                                        appliedWaypoint.ForgeScriptApiVersion == sourceWaypoint.ForgeScriptApiVersion &&
                                        appliedWaypoint.Arrival == sourceWaypoint.Arrival;
            var waypointNeighborsPreserved = sourceArea.Navigation.Waypoints.Waypoints
                    .Where(item => item.Id != sourceWaypoint.Id)
                    .SequenceEqual(appliedArea.Navigation.Waypoints.Waypoints
                        .Where(item => item.Id != sourceWaypoint.Id)) &&
                sourceArea.Navigation.Waypoints.Waypoints.Select(item => item.Id).SequenceEqual(
                    appliedArea.Navigation.Waypoints.Waypoints.Select(item => item.Id)) &&
                sourceArea.Navigation.Waypoints.Id == appliedArea.Navigation.Waypoints.Id &&
                sourceArea.Navigation.Waypoints.SchemaVersion ==
                    appliedArea.Navigation.Waypoints.SchemaVersion &&
                sourceArea.Navigation.Waypoints.Provenance == appliedArea.Navigation.Waypoints.Provenance &&
                sourceArea.Navigation.Waypoints.AreaId == appliedArea.Navigation.Waypoints.AreaId &&
                sourceArea.Navigation.Waypoints.RequiredAtRuntime ==
                    appliedArea.Navigation.Waypoints.RequiredAtRuntime &&
                sourceArea.Navigation.Waypoints.PatrolRoutes.SequenceEqual(
                    appliedArea.Navigation.Waypoints.PatrolRoutes) &&
                sourceArea.Navigation.Waypoints.Locators.SequenceEqual(
                    appliedArea.Navigation.Waypoints.Locators) &&
                sourceArea.Navigation.Waypoints.Collections.SequenceEqual(
                    appliedArea.Navigation.Waypoints.Collections) &&
                sourceArea.Navigation.Waypoints.RouteVisualization ==
                    appliedArea.Navigation.Waypoints.RouteVisualization;
            var navigationNeighborsPreserved = sourceArea.Navigation.AgentProfiles.SequenceEqual(
                                                   appliedArea.Navigation.AgentProfiles) &&
                                               sourceArea.Navigation.OffMeshLinks.SequenceEqual(
                                                   appliedArea.Navigation.OffMeshLinks) &&
                                               ReferenceEquals(sourceArea.Navigation.Anchors,
                                                   appliedArea.Navigation.Anchors) &&
                                               sourceArea.Portals.SequenceEqual(appliedArea.Portals);
            var appliedNeighbor = appliedProject.Areas.Single(item => item.Id == sourceNeighbor.Id);
            var projectNeighborsPreserved = ReferenceEquals(canonicalSource.Module, appliedProject.Module) &&
                                            ReferenceEquals(sourceNeighbor, appliedNeighbor) &&
                                            ReferenceEquals(sourceArea.Environment, appliedArea.Environment) &&
                                            ReferenceEquals(sourceArea.Authoring, appliedArea.Authoring) &&
                                            ReferenceEquals(sourceArea.World, appliedArea.World) &&
                                            ReferenceEquals(sourceArea.RuntimePolicies,
                                                appliedArea.RuntimePolicies) &&
                                            ReferenceEquals(sourceArea.Foundation, appliedArea.Foundation) &&
                                            ReferenceEquals(sourceArea.Playtest, appliedArea.Playtest) &&
                                            sourceArea.Entities.SequenceEqual(appliedArea.Entities) &&
                                            canonicalSource.Blueprints.SequenceEqual(appliedProject.Blueprints) &&
                                            canonicalSource.Dialogues.SequenceEqual(appliedProject.Dialogues) &&
                                            canonicalSource.Quests.SequenceEqual(appliedProject.Quests);
            var appliedJson = AreaJsonSerializer.Serialize(appliedArea);
            var undo = editing.Undo();
            var undone = ReferenceEquals(canonicalSource, editing.Project);
            var redo = editing.Redo();
            var redone = ReferenceEquals(appliedProject, editing.Project);
            var undoRedo = undo.Succeeded && undone && redo.Succeeded && redone;
            var save = editing.Save();
            var reopen = reopened.Open(tempRoot, areaChoice.AreaId.ToString());
            var restoredArea = reopened.Project?.Areas.SingleOrDefault(item => item.Id == areaChoice.AreaId);
            var persistence = save.Succeeded && reopen.Succeeded && restoredArea is not null &&
                              string.Equals(appliedJson, AreaJsonSerializer.Serialize(restoredArea),
                                  StringComparison.Ordinal);
            var humanText = !ContainsTechnicalIdentifierUx2NavC(areaChoice.Label + " " +
                            waypointChoice.Label + " " + freshPreview.Summary + " " +
                            string.Join(' ', freshPreview.Diff));
            var sourceUntouched = ReferenceEquals(sourceProject, Project) &&
                                  ReferenceEquals(sourceHistory, History) &&
                                  History?.State.Revision == sourceRevision &&
                                  (History?.Journal ?? []).SequenceEqual(sourceJournal) &&
                                  IsDirty == sourceDirty && History?.CanUndo == sourceUndo &&
                                  History?.CanRedo == sourceRedo &&
                                  CaptureWaypointApprovalUx2NavC() == sourceApproval;
            var succeeded = explicitSelection && humanLabels && createSupported && appearanceValidation &&
                            markerValidation && revealPolicy && dryRun && staleRevisionBlocked &&
                            staleSourceBlocked && staleApprovalBlocked && canonicalCommand && undoRedo &&
                            persistence && hiddenFieldsPreserved && waypointNeighborsPreserved &&
                            navigationNeighborsPreserved && projectNeighborsPreserved && sourceUntouched && humanText;
            return new(succeeded, explicitSelection, humanLabels, createSupported, appearanceValidation,
                markerValidation, revealPolicy, dryRun, staleRevisionBlocked, staleSourceBlocked,
                staleApprovalBlocked, canonicalCommand, undoRedo, persistence, hiddenFieldsPreserved,
                waypointNeighborsPreserved, navigationNeighborsPreserved, projectNeighborsPreserved,
                sourceUntouched, humanText);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_NAV_C_CONTRACT_FAILED type={exception.GetType().Name} message={exception.Message}");
            return FailedUx2NavCSmoke();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            DeleteUx2NavCTemporaryWorkspace(tempRoot);
        }
    }

    private static bool ApplyUx2NavCNeighborMutation(StudioSessionController controller, AreaDefinition area)
    {
        var history = controller.History;
        if (history is null)
        {
            return false;
        }
        var navigation = area.Navigation;
        var candidate = new AreaNavigationDefinition(navigation.Id,
            navigation.CellSize == 1f ? .9f : 1f, navigation.AgentProfiles, navigation.OffMeshLinks,
            "studio:ux2-nav-c:neighbor", navigation.SchemaVersion, navigation.Anchors,
            navigation.Waypoints);
        var result = controller._setNavigationTopologyUx2NavC.Handle(history.State,
            new SetNavigationTopologyCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                [new(area.Id, candidate)], [], null, false));
        if (!result.Succeeded)
        {
            return false;
        }
        history.Record(result);
        return true;
    }

    internal static bool DeleteUx2NavCTemporaryWorkspace(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return true;
        }
        try
        {
            var root = Path.GetFullPath(directory);
            var temp = Path.GetFullPath(Path.GetTempPath());
            var prefix = temp.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                         Path.DirectorySeparatorChar;
            var leaf = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));
            if (!root.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !leaf.StartsWith("openrpg-forge-studio-ux2-nav-c-", StringComparison.Ordinal))
            {
                return false;
            }
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
            return !Directory.Exists(root);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool ContainsTechnicalIdentifierUx2NavC(string text) =>
        text.Contains("UUID", StringComparison.OrdinalIgnoreCase) ||
        System.Text.RegularExpressions.Regex.IsMatch(text,
            @"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static StudioUx2NavCSmokeResult FailedUx2NavCSmoke() => new(false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false, false, false,
        false, false, false);
}
