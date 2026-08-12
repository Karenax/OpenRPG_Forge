using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Blueprints;

public sealed class ConfigureBlueprintPrefabHandler
{
    public const string MissingBlueprintCode = "blueprint.prefab.missing";
    public const string ReadOnlyCode = "blueprint.prefab.read_only";
    public const string RevisionConflictCode = "command.revision_conflict";
    private const string CommandName = "configure_blueprint_prefab";
    private readonly IClock _clock;

    public ConfigureBlueprintPrefabHandler(IClock clock) => _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CommandResult Handle(ProjectState state, ConfigureBlueprintPrefabCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (state.Revision != command.BaseRevision)
            return Failure(state, command, RevisionConflictCode, "Project revision changed before prefab update.");
        var existing = state.Project.Blueprints.FirstOrDefault(item => item.Id == command.BlueprintId);
        if (existing is null)
            return Failure(state, command, MissingBlueprintCode, "Blueprint does not exist.");
        if (existing.IsReadOnly())
            return Failure(state, command, ReadOnlyCode, "Installed pack blueprints are read-only.");

        BlueprintDefinition updated;
        try
        {
            updated = BuildUpdated(existing, command);
        }
        catch (ArgumentException exception)
        {
            return Failure(state, command, "blueprint.prefab.invalid", exception.Message);
        }
        var candidate = Replace(state.Project, updated);
        var diagnostics = BlueprintPrefabValidator.Validate(candidate, updated);
        if (diagnostics.Count > 0)
            return Failure(state, command, diagnostics[0].Code, $"{diagnostics[0].Path}: {diagnostics[0].Message}");
        var patch = new ProjectPatch("replace", $"blueprints/{existing.Id}/authoring/prefab",
            $"Configure variant={command.Variant is not null}, composition={command.Composition is not null}, " +
            $"budgets={command.AssetBudget?.Platforms.Count ?? 0}, collections={command.AuthorCollections.Count}.");
        var inverse = new ProjectPatch("replace", $"blueprints/{existing.Id}/authoring/prefab",
            "Restore previous prefab variant, composition, collections and asset budgets.");
        if (command.DryRun)
            return new(true, state, state.Revision, null, [], patch, inverse, Audit(command, false));
        var revision = state.Revision.Next();
        return new(true, new ProjectState(candidate, revision), state.Revision, revision, [], patch, inverse,
            Audit(command, true));
    }

    private static BlueprintDefinition BuildUpdated(BlueprintDefinition blueprint,
        ConfigureBlueprintPrefabCommand command)
    {
        var old = blueprint.Authoring;
        var authoring = new BlueprintAuthoringMetadata(old.SchemaVersion, old.Version, old.PackageNamespace,
            old.ManifestPath, old.ExposedParameters, old.Dependencies, old.PreviewPath, old.PlatformByteBudget,
            old.Kit, old.Styles, old.Compatibility, old.Tags, old.CategoryPaths, old.PromotedDefaults,
            command.AuthorCollections, command.Variant, command.Composition, command.AssetBudget);
        var fingerprint = BuildFingerprint(blueprint.Provenance.ContentHash, command);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint))).ToLowerInvariant();
        var provenance = new BlueprintProvenance(blueprint.Provenance.SchemaVersion, blueprint.Provenance.Source,
            blueprint.Provenance.Author, blueprint.Provenance.LicenseSpdx, hash, blueprint.Provenance.IsAiGenerated,
            [.. blueprint.Provenance.Modifications, "Configured prefab variant, composition and asset budgets"],
            blueprint.Provenance.PermissionEvidence);
        return blueprint.WithPaletteUpdate(authoring, provenance);
    }

    private static string BuildFingerprint(string previousHash, ConfigureBlueprintPrefabCommand command)
    {
        var builder = new StringBuilder(previousHash);
        foreach (var collection in command.AuthorCollections.Order(StringComparer.Ordinal))
            builder.Append("|collection:").Append(collection);
        if (command.Variant is { } variant)
        {
            builder.Append("|variant:").Append(variant.Id.ToString("D")).Append(':')
                .Append(variant.SchemaVersion).Append(':').Append(variant.Provenance).Append(':')
                .Append(variant.BaseBlueprintId).Append(':').Append(variant.RequiredAtRuntime);
            foreach (var item in variant.Overrides.OrderBy(value => value.Kind).ThenBy(value => value.Key,
                         StringComparer.Ordinal))
                builder.Append("|override:").Append(item.Kind).Append(':').Append(item.Key).Append(':')
                    .Append(item.Value);
        }
        if (command.Composition is { } composition)
        {
            builder.Append("|composition:").Append(composition.Id.ToString("D")).Append(':')
                .Append(composition.SchemaVersion).Append(':').Append(composition.Provenance).Append(':')
                .Append(composition.RequiredAtRuntime);
            foreach (var node in composition.Nodes.OrderBy(value => value.Id))
                builder.Append("|node:").Append(node.Id.ToString("D")).Append(':').Append(node.BlueprintId)
                    .Append(':').Append(node.ParentNodeId?.ToString("D") ?? "root").Append(':')
                    .Append(node.Socket ?? string.Empty).Append(':').Append(Format(node.Transform.Position.X))
                    .Append(',').Append(Format(node.Transform.Position.Y)).Append(',')
                    .Append(Format(node.Transform.Position.Z));
            AppendSorted(builder, "rule", composition.AssemblyRules);
            AppendSorted(builder, "parameter", composition.Parameters);
            AppendSorted(builder, "script", composition.AssemblyScripts);
        }
        if (command.AssetBudget is { } budget)
        {
            var metrics = budget.Metrics;
            builder.Append("|budget:").Append(budget.Id.ToString("D")).Append(':').Append(budget.SchemaVersion)
                .Append(':').Append(budget.Provenance).Append(':').Append(budget.RequiredAtRuntime)
                .Append(':').Append(metrics.TriangleCount).Append(':').Append(metrics.MaterialCount)
                .Append(':').Append(metrics.TextureCount).Append(':').Append(metrics.MemoryBytes)
                .Append(':').Append(metrics.AnimationCount).Append(':').Append(metrics.LodLevelCount)
                .Append(':').Append(metrics.CollisionShapeCount);
            foreach (var platform in budget.Platforms.OrderBy(value => value.Platform))
                builder.Append("|platform:").Append(platform.Platform).Append(':')
                    .Append(platform.MaximumTriangles).Append(':').Append(platform.MaximumMaterials).Append(':')
                    .Append(platform.MaximumTextures).Append(':').Append(platform.MaximumMemoryBytes).Append(':')
                    .Append(platform.MaximumAnimations).Append(':').Append(platform.MaximumLodLevels).Append(':')
                    .Append(platform.MaximumCollisionShapes).Append(':').Append(platform.GateBuild);
        }
        return builder.ToString();
    }

    private static void AppendSorted(StringBuilder builder, string kind, IReadOnlyList<string> values)
    {
        foreach (var value in values.Order(StringComparer.Ordinal)) builder.Append('|').Append(kind).Append(':').Append(value);
    }

    private static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static ProjectDefinition Replace(ProjectDefinition project, BlueprintDefinition updated) => new(
        project.Id, project.Title, project.Module,
        project.Blueprints.Select(item => item.Id == updated.Id ? updated : item).ToArray(), project.Areas,
        project.Dialogues, project.Quests);

    private CommandResult Failure(ProjectState state, ConfigureBlueprintPrefabCommand command, string code,
        string message) => new(false, state, state.Revision, null, [new CommandDiagnostic(code, message)], null, null,
        Audit(command, false));

    private AuditEntry Audit(ConfigureBlueprintPrefabCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);
}
