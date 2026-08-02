using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;

namespace Forge.Domain.Validation;

public static class ProjectDiagnosticCatalog
{
    public static IReadOnlyList<ActionableDiagnostic> Build(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var diagnostics = ProjectValidator.Validate(project)
            .Select(item => Map(project, item, "ProjectValidator"))
            .ToList();
        foreach (var area in project.Areas)
        {
            diagnostics.AddRange(AreaGeometryValidator.Validate(project, area)
                .Select(item => Map(project, item, "AreaGeometryValidator")));
            if (!project.Blueprints.Any(blueprint => blueprint.Kind == BlueprintKind.PlayerSpawn))
            {
                continue;
            }

            try
            {
                diagnostics.AddRange(AreaNavigationValidator.AnalyzeAll(project, area)
                    .SelectMany(analysis => analysis.Diagnostics)
                    .Distinct()
                    .Select(item => Map(project, item, "AreaNavigationValidator")));
            }
            catch (InvalidOperationException exception)
            {
                var dense = new DomainDiagnostic(
                    "area.navigation.bake_too_dense",
                    exception.Message,
                    $"areas/{area.Id}/navigation/cellSize");
                diagnostics.Add(Map(project, dense, "NavigationSurfaceBuilder"));
            }
        }

        return diagnostics
            .GroupBy(diagnostic => diagnostic.Id)
            .Select(group => group.First())
            .OrderByDescending(diagnostic => diagnostic.Severity)
            .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.DocumentPath, StringComparer.Ordinal)
            .ToArray();
    }

    private static ActionableDiagnostic Map(ProjectDefinition project, DomainDiagnostic diagnostic, string source)
    {
        var target = ResolveTarget(project, diagnostic.Path);
        var property = ResolveProperty(diagnostic.Path);
        var id = DiagnosticId.From(StableGuid(
            $"diagnostic:{diagnostic.Code}:{diagnostic.Path}:{target.SubjectUuid}:{property}:{source}"));
        var fixes = ResolveFixes(project, diagnostic, target.Area, target.Entity, id);
        return new ActionableDiagnostic(
            id,
            SeverityFor(diagnostic.Code),
            diagnostic.Code,
            diagnostic.Message,
            diagnostic.Path,
            target.SubjectUuid,
            property,
            source,
            fixes,
            "derived-project-diagnostic");
    }

    private static IReadOnlyList<DiagnosticQuickFixDefinition> ResolveFixes(
        ProjectDefinition project,
        DomainDiagnostic diagnostic,
        AreaDefinition? area,
        EntityInstance? entity,
        DiagnosticId diagnosticId)
    {
        if (diagnostic.Code == AreaGeometryValidator.OutOfBoundsCode && area is not null && entity is not null &&
            entity.Transform.Scale.X <= area.Dimensions.Width && entity.Transform.Scale.Z <= area.Dimensions.Depth)
        {
            return
            [
                Fix(
                    diagnosticId,
                    DiagnosticQuickFixKind.ClampEntityToArea,
                    area.Id,
                    entity.Id,
                    "Rentrer l’entité dans la zone",
                    "Déplace uniquement la position X/Z afin que le volume reste dans les limites."),
            ];
        }

        if (diagnostic.Code == AreaNavigationValidator.MissingSpawnCode && area is not null &&
            project.Blueprints.Any(blueprint => blueprint.Kind == BlueprintKind.PlayerSpawn))
        {
            return
            [
                Fix(
                    diagnosticId,
                    DiagnosticQuickFixKind.CreatePlayerSpawn,
                    area.Id,
                    null,
                    "Créer un point d’apparition",
                    "Ajoute au centre de la zone une instance du premier blueprint PlayerSpawn disponible."),
            ];
        }

        if (diagnostic.Code == "area.navigation.bake_too_dense" && area is not null && area.Navigation.CellSize < 4 &&
            (long)Math.Ceiling(area.Dimensions.Width / 4d) * (long)Math.Ceiling(area.Dimensions.Depth / 4d) <=
            NavigationSurfaceBuilder.MaximumCellCount)
        {
            return
            [
                Fix(
                    diagnosticId,
                    DiagnosticQuickFixKind.IncreaseNavigationCellSize,
                    area.Id,
                    null,
                    "Alléger la grille de navigation",
                    "Double la taille des cellules sans dépasser quatre mètres."),
            ];
        }

        return [];
    }

    private static DiagnosticQuickFixDefinition Fix(
        DiagnosticId diagnosticId,
        DiagnosticQuickFixKind kind,
        AreaId areaId,
        EntityId? entityId,
        string label,
        string description) => new(
        DiagnosticQuickFixId.From(StableGuid($"quick-fix:{diagnosticId}:{kind}:{areaId}:{entityId}")),
        label,
        description,
        kind,
        areaId,
        entityId,
        "derived-project-diagnostic");

    private static (AreaDefinition? Area, EntityInstance? Entity, Guid? SubjectUuid) ResolveTarget(
        ProjectDefinition project,
        string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && parts[0] == "areas" && Guid.TryParse(parts[1], out var areaGuid))
        {
            var area = project.Areas.FirstOrDefault(candidate => candidate.Id.Value == areaGuid);
            if (area is null)
            {
                return (null, null, areaGuid);
            }

            if (parts.Length >= 4 && parts[2] == "entities")
            {
                EntityInstance? entity = null;
                if (int.TryParse(parts[3], out var index) && index >= 0 && index < area.Entities.Count)
                {
                    entity = area.Entities[index];
                }
                else if (Guid.TryParse(parts[3], out var entityGuid))
                {
                    entity = area.Entities.FirstOrDefault(candidate => candidate.Id.Value == entityGuid);
                }

                if (entity is not null)
                {
                    return (area, entity, entity.Id.Value);
                }
            }

            return (area, null, area.Id.Value);
        }

        return (null, null, SubjectFromPath(parts) ?? project.Id.Value);
    }

    private static Guid? SubjectFromPath(IReadOnlyList<string> parts) =>
        parts.Skip(1).Select(part => Guid.TryParse(part, out var value) ? value : (Guid?)null)
            .FirstOrDefault(value => value is not null);

    private static string ResolveProperty(string path)
    {
        var last = path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return string.IsNullOrWhiteSpace(last) || Guid.TryParse(last, out _) || int.TryParse(last, out _)
            ? "document"
            : last;
    }

    private static DiagnosticSeverity SeverityFor(string code) => code switch
    {
        AreaGeometryValidator.CollisionOverlapCode or
        AreaGeometryValidator.SocketMismatchCode or
        AreaNavigationValidator.UnreachableEntityCode => DiagnosticSeverity.Warning,
        _ => DiagnosticSeverity.Error,
    };

    private static Guid StableGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:{value}"));
        Span<byte> guid = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guid);
        guid[7] = (byte)((guid[7] & 0x0f) | 0x50);
        guid[8] = (byte)((guid[8] & 0x3f) | 0x80);
        return new Guid(guid);
    }
}
