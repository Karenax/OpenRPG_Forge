using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Areas;

namespace Forge.Domain.Authoring;

public sealed record EditorWorkspaceProfile
{
    public const int CurrentSchemaVersion = 1;

    public EditorWorkspaceProfileId Id { get; }

    public int SchemaVersion { get; }

    public string Name { get; }

    public EditorRole Role { get; }

    public EditorDisplayFilter DisplayFilter { get; }

    public bool RepeatPlacement { get; }

    public IReadOnlyList<AreaId> OpenAreaTabs { get; }

    public AreaId? ActiveAreaId { get; }

    public AreaId? ComparisonAreaId { get; }

    public bool ContextualHelpEnabled { get; }

    public string Provenance { get; }

    public EditorWorkspaceProfile(
        EditorWorkspaceProfileId id,
        string name,
        EditorRole role,
        EditorDisplayFilter displayFilter,
        bool repeatPlacement,
        IReadOnlyList<AreaId> openAreaTabs,
        AreaId? activeAreaId,
        AreaId? comparisonAreaId,
        bool contextualHelpEnabled,
        string provenance,
        int schemaVersion = CurrentSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(displayFilter);
        ArgumentNullException.ThrowIfNull(openAreaTabs);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (schemaVersion != CurrentSchemaVersion) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        var tabs = openAreaTabs.Distinct().ToArray();
        if (activeAreaId is not null && !tabs.Contains(activeAreaId))
        {
            throw new ArgumentException("The active area must be present in the open tabs.", nameof(activeAreaId));
        }

        if (comparisonAreaId is not null && (!tabs.Contains(comparisonAreaId) || comparisonAreaId == activeAreaId))
        {
            throw new ArgumentException("The comparison area must be a different open tab.", nameof(comparisonAreaId));
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Name = name.Trim();
        Role = role;
        DisplayFilter = displayFilter;
        RepeatPlacement = repeatPlacement;
        OpenAreaTabs = tabs;
        ActiveAreaId = activeAreaId;
        ComparisonAreaId = comparisonAreaId;
        ContextualHelpEnabled = contextualHelpEnabled;
        Provenance = provenance.Trim();
    }

    public static EditorWorkspaceProfile CreateDefault(EditorRole role, AreaId? areaId = null)
    {
        var name = role switch
        {
            EditorRole.GameMaster => "Maître de jeu",
            _ => role.ToString(),
        };
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:editor-profile:{role}"));
        Span<byte> id = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(id);
        id[7] = (byte)((id[7] & 0x0f) | 0x50);
        id[8] = (byte)((id[8] & 0x3f) | 0x80);
        return new EditorWorkspaceProfile(
            EditorWorkspaceProfileId.From(new Guid(id)),
            name,
            role,
            EditorDisplayFilter.ShowAll,
            repeatPlacement: role is EditorRole.Builder or EditorRole.Artist,
            areaId is null ? [] : [areaId],
            areaId,
            null,
            contextualHelpEnabled: role is EditorRole.Beginner or EditorRole.Builder,
            "openrpg-forge-default");
    }
}
