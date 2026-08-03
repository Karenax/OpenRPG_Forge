using Forge.RpgKit.Domain.Doors;

namespace Forge.RpgKit.Application.Doors;

public static class DoorLinkAuthoringService
{
    public static DoorLinkMutation Link(IReadOnlyList<DoorLink> current, IReadOnlySet<Guid> knownDoors, Guid source, Guid target, bool oneWay, string returnNavigationLabel)
    {
        if (source == target || !knownDoors.Contains(source) || !knownDoors.Contains(target)) throw new InvalidOperationException("RPG_DOOR_LINK_TARGET_INVALID");
        if (current.Any(item => item.SourceDoorId == source && item.TargetDoorId == target)) throw new InvalidOperationException("RPG_DOOR_LINK_DUPLICATE");
        if (!oneWay && string.IsNullOrWhiteSpace(returnNavigationLabel)) throw new InvalidOperationException("RPG_DOOR_RETURN_NAVIGATION_REQUIRED");
        var added = oneWay ? new[] { new DoorLink(source, target, true, "") } : new[] { new DoorLink(source, target, false, returnNavigationLabel), new DoorLink(target, source, false, returnNavigationLabel) };
        var links = current.Concat(added).OrderBy(item => item.SourceDoorId).ThenBy(item => item.TargetDoorId).ToArray();
        return new(links, added, $"link:{source}:{target}:{(oneWay ? "one-way" : "pair")}", $"unlink:{string.Join(',', added.Select(item => $"{item.SourceDoorId}>{item.TargetDoorId}"))}");
    }

    public static IReadOnlyList<DoorDiagnostic> Validate(IReadOnlyList<DoorLink> links, IReadOnlySet<Guid> knownDoors)
    {
        var diagnostics = new List<DoorDiagnostic>();
        foreach (var link in links)
        {
            if (!knownDoors.Contains(link.SourceDoorId) || !knownDoors.Contains(link.TargetDoorId)) diagnostics.Add(new("RPG_DOOR_LINK_ORPHAN", $"doors/links/{link.SourceDoorId}/{link.TargetDoorId}", "Door link target is missing."));
            if (!link.OneWay && links.All(reverse => reverse.SourceDoorId != link.TargetDoorId || reverse.TargetDoorId != link.SourceDoorId)) diagnostics.Add(new("RPG_DOOR_LINK_REVERSE_MISSING", $"doors/links/{link.SourceDoorId}/{link.TargetDoorId}", "Bidirectional link has no return navigation."));
        }
        return diagnostics;
    }
}
