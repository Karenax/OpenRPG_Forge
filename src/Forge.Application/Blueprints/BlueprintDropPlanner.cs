using Forge.Application.Spatial;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.Application.Blueprints;

public enum BlueprintDropTargetKind
{
    Viewport,
    Graph,
    Inventory,
    Property,
    Package,
}

public enum BlueprintDropOperationKind
{
    PlaceEntity,
    CreateGraphReference,
    AddInventoryItem,
    SetBlueprintReference,
    IncludePackageDependency,
}

public sealed record BlueprintDropRequest(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    BlueprintId BlueprintId,
    BlueprintDropTargetKind TargetKind,
    string TargetPath,
    AreaId? AreaId = null,
    Vector3Value? Position = null,
    float GridSize = 1);

public sealed record BlueprintDropSemanticChange(string Path, string Before, string After);

public sealed record BlueprintDropPlan(
    bool IsValid,
    string Code,
    string Message,
    string Path,
    Guid RequestId,
    BlueprintId BlueprintId,
    BlueprintDropTargetKind TargetKind,
    BlueprintDropOperationKind? Operation,
    IReadOnlyList<BlueprintDropSemanticChange> Changes,
    Vector3Value? CorrectedPosition = null)
{
    public const string ValidCode = "blueprint.drop.valid";
}

/// <summary>
/// Canonical, engine-agnostic validation for blueprint drag payloads. Target surfaces execute the
/// resulting operation through their existing command handler; this planner never mutates a project.
/// </summary>
public sealed class BlueprintDropPlanner
{
    public const int CurrentSchemaVersion = 1;
    public const string InvalidIdCode = "blueprint.drop.id_invalid";
    public const string UnsupportedSchemaCode = "blueprint.drop.schema_unsupported";
    public const string MissingProvenanceCode = "blueprint.drop.provenance_missing";
    public const string MissingBlueprintCode = "blueprint.drop.blueprint_missing";
    public const string InvalidTargetPathCode = "blueprint.drop.target_path_invalid";
    public const string ViewportContextMissingCode = "blueprint.drop.viewport_context_missing";
    public const string InventoryKindCode = "blueprint.drop.inventory_item_required";
    public const string PackageGovernanceCode = "blueprint.drop.package_governance_required";

    private readonly PlacementValidator _placementValidator;

    public BlueprintDropPlanner(PlacementValidator? placementValidator = null) =>
        _placementValidator = placementValidator ?? new PlacementValidator();

    public BlueprintDropPlan Plan(ProjectDefinition project, BlueprintDropRequest request)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(request);
        var root = $"blueprintDrops/{request.Id:D}";
        if (request.Id == Guid.Empty)
            return Invalid(request, InvalidIdCode, "Drop request UUID must be non-empty.", $"{root}/id");
        if (request.SchemaVersion != CurrentSchemaVersion)
            return Invalid(request, UnsupportedSchemaCode,
                $"Drop schema {request.SchemaVersion} is not supported.", $"{root}/schemaVersion");
        if (IsUnknown(request.Provenance))
            return Invalid(request, MissingProvenanceCode,
                "Drop modification provenance is required.", $"{root}/provenance");

        var blueprint = project.Blueprints.FirstOrDefault(candidate => candidate.Id == request.BlueprintId);
        if (blueprint is null)
            return Invalid(request, MissingBlueprintCode,
                $"Blueprint '{request.BlueprintId}' does not exist.", $"{root}/blueprintId");
        if (!IsSafeTargetPath(request.TargetPath))
            return Invalid(request, InvalidTargetPathCode,
                "Drop target must be a safe canonical document path.", $"{root}/targetPath");

        return request.TargetKind switch
        {
            BlueprintDropTargetKind.Viewport => PlanViewport(project, request, root),
            BlueprintDropTargetKind.Graph => Valid(request, BlueprintDropOperationKind.CreateGraphReference,
                $"Reference blueprint '{blueprint.Slug}' from graph.", request.TargetPath),
            BlueprintDropTargetKind.Inventory when blueprint.Kind != BlueprintKind.Item => Invalid(request,
                InventoryKindCode, "Only item blueprints can be dropped into an inventory.", request.TargetPath),
            BlueprintDropTargetKind.Inventory => Valid(request, BlueprintDropOperationKind.AddInventoryItem,
                $"Add item blueprint '{blueprint.Slug}' to inventory.", request.TargetPath),
            BlueprintDropTargetKind.Property => Valid(request, BlueprintDropOperationKind.SetBlueprintReference,
                $"Set property reference to blueprint '{blueprint.Slug}'.", request.TargetPath),
            BlueprintDropTargetKind.Package when !IsDistributionReady(blueprint.Provenance) => Invalid(request,
                PackageGovernanceCode,
                "Package drops require approved SPDX, source, author, permission evidence and SHA-256.",
                request.TargetPath),
            BlueprintDropTargetKind.Package => Valid(request, BlueprintDropOperationKind.IncludePackageDependency,
                $"Include governed blueprint '{blueprint.Slug}' in package dependencies.", request.TargetPath),
            _ => Invalid(request, InvalidTargetPathCode, "Unsupported blueprint drop target.", request.TargetPath),
        };
    }

    private BlueprintDropPlan PlanViewport(ProjectDefinition project, BlueprintDropRequest request, string root)
    {
        if (request.AreaId is null || request.Position is null)
            return Invalid(request, ViewportContextMissingCode,
                "Viewport drops require an area and a world position.", $"{root}/viewport");
        PlacementValidationResult placement;
        try
        {
            placement = _placementValidator.Validate(project, new PlacementValidationRequest(
                request.AreaId,
                request.BlueprintId,
                request.Position.Value,
                request.GridSize));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return Invalid(request, ViewportContextMissingCode, exception.Message, $"{root}/gridSize");
        }

        if (!placement.IsValid)
            return new BlueprintDropPlan(false, placement.Code, placement.Message, request.TargetPath,
                request.Id, request.BlueprintId, request.TargetKind, null, [], placement.CorrectedPosition);
        return Valid(request, BlueprintDropOperationKind.PlaceEntity, placement.Message, request.TargetPath,
            placement.CorrectedPosition);
    }

    private static BlueprintDropPlan Valid(
        BlueprintDropRequest request,
        BlueprintDropOperationKind operation,
        string message,
        string path,
        Vector3Value? correctedPosition = null) =>
        new(true, BlueprintDropPlan.ValidCode, message, path, request.Id, request.BlueprintId,
            request.TargetKind, operation,
            [new BlueprintDropSemanticChange(path, "<empty>", request.BlueprintId.ToString())], correctedPosition);

    private static BlueprintDropPlan Invalid(BlueprintDropRequest request, string code, string message, string path) =>
        new(false, code, message, path, request.Id, request.BlueprintId, request.TargetKind, null, []);

    private static bool IsSafeTargetPath(string? path)
    {
        var normalized = (path ?? string.Empty).Trim().Replace('\\', '/');
        return normalized.Length > 0 && !Path.IsPathRooted(normalized) &&
               normalized.Split('/').All(segment => segment is not "" and not "." and not "..");
    }

    private static bool IsDistributionReady(BlueprintProvenance provenance) =>
        DistributionComplianceValidator.IsApprovedLicense(provenance.LicenseSpdx) &&
        !IsUnknown(provenance.Source) && !IsUnknown(provenance.Author) &&
        !IsUnknown(provenance.PermissionEvidence) && provenance.ContentHash.Length == 64 &&
        provenance.ContentHash.All(Uri.IsHexDigit);

    private static bool IsUnknown(string? value) => string.IsNullOrWhiteSpace(value) ||
        value.Equals("unknown", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("unverified", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("not provided", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("migration-default", StringComparison.OrdinalIgnoreCase);
}
