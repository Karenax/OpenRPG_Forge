using Forge.Domain.Entities;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Spatial;

namespace Forge.Application.Entities;

public sealed record RuntimeEntityStateComponent(string Name, string JsonValue);

public sealed record RuntimeEntityCopySource(
    EntityInstance Entity,
    string OwnerId,
    IReadOnlyList<EntityId> References,
    IReadOnlyList<RuntimeEntityStateComponent> State);

public sealed record RuntimeEntityCopyRequest(
    RuntimeEntityCopySource Source,
    EntityId DestinationId,
    Vector3Value PositionOffset,
    string RequestingOwnerId,
    bool HasServerAuthority,
    IReadOnlyDictionary<EntityId, EntityId> InternalReferenceMap);

public sealed record RuntimeEntityCopyResult(
    bool Succeeded,
    EntityInstance? Entity,
    string OwnerId,
    IReadOnlyList<EntityId> References,
    IReadOnlyList<RuntimeEntityStateComponent> State,
    string Code);

public sealed class RuntimeEntityCopyService(RuntimeObjectCopyPolicy policy)
{
    public const string SuccessCode = "runtime.copy.ok";
    public const string AuthorityRequiredCode = "runtime.copy.authority_required";
    public const string QuotaExceededCode = "runtime.copy.quota_exceeded";
    public const string DuplicateDestinationCode = "runtime.copy.destination_duplicate";

    private readonly RuntimeObjectCopyPolicy _policy = policy ?? throw new ArgumentNullException(nameof(policy));

    public IReadOnlyList<RuntimeEntityCopyResult> CopyTransaction(
        IReadOnlyList<RuntimeEntityCopyRequest> requests,
        IReadOnlySet<EntityId> occupiedIds)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(occupiedIds);
        if (requests.Count == 0) return [];
        if (requests.Count > _policy.MaxCopiesPerTransaction)
            return requests.Select(request => Failure(request, QuotaExceededCode)).ToArray();
        if (_policy.RequireServerAuthority && requests.Any(request => !request.HasServerAuthority))
            return requests.Select(request => Failure(request, AuthorityRequiredCode)).ToArray();
        var destinations = requests.Select(request => request.DestinationId).ToArray();
        if (destinations.Distinct().Count() != destinations.Length || destinations.Any(occupiedIds.Contains))
            return requests.Select(request => Failure(request, DuplicateDestinationCode)).ToArray();

        return requests.Select(Copy).ToArray();
    }

    private RuntimeEntityCopyResult Copy(RuntimeEntityCopyRequest request)
    {
        var source = request.Source.Entity;
        var position = new Vector3Value(
            source.Transform.Position.X + request.PositionOffset.X,
            source.Transform.Position.Y + request.PositionOffset.Y,
            source.Transform.Position.Z + request.PositionOffset.Z);
        var clone = new EntityInstance(request.DestinationId, source.BlueprintId,
            new TransformValue(position, source.Transform.Rotation, source.Transform.Scale), source.Tags, source.Overrides,
            source.BlueprintVersionRequirement);
        var references = request.Source.References.Select(reference =>
        {
            if (reference == source.Id) return request.DestinationId;
            if (request.InternalReferenceMap.TryGetValue(reference, out var mapped)) return mapped;
            return _policy.ReferenceMode == RuntimeReferenceCopyMode.DropExternal ? null : reference;
        }).Where(reference => reference is not null).Cast<EntityId>().Distinct().OrderBy(reference => reference.Value).ToArray();
        var allowed = _policy.StateComponents.ToHashSet(StringComparer.Ordinal);
        var state = request.Source.State.Where(component => allowed.Contains(component.Name))
            .OrderBy(component => component.Name, StringComparer.Ordinal).ToArray();
        var owner = _policy.CopyOwnership ? request.Source.OwnerId : request.RequestingOwnerId;
        return new RuntimeEntityCopyResult(true, clone, owner, references, state, SuccessCode);
    }

    private static RuntimeEntityCopyResult Failure(RuntimeEntityCopyRequest request, string code) =>
        new(false, null, request.RequestingOwnerId, [], [], code);
}
