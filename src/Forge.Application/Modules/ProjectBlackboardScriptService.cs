using System.Collections.ObjectModel;
using Forge.Domain.Modules;

namespace Forge.Application.Modules;

public sealed record ProjectVariableHandle(ProjectVariableId Id, string Key, ProjectVariableValueKind Kind,
    ProjectVariableScope Scope, int SchemaVersion);

public sealed record ProjectVariableRead(ProjectVariableHandle Handle, string Value, long Revision);

public sealed record ProjectVariableMutation(bool Succeeded, ProjectVariableRead? Before,
    ProjectVariableRead? After, string? Diagnostic);

public sealed record BlackboardDebugEntry(ProjectVariableHandle Handle, string Value,
    ProjectVariableReplicationMode Replication, bool RequiredAtRuntime);

public sealed record BlackboardDebugSnapshot(long Revision, IReadOnlyList<BlackboardDebugEntry> Variables,
    int HiddenVariableCount);

public enum BlackboardReplicationAudience
{
    Owner,
    Party,
    Client,
    Server,
}

public sealed record BlackboardReplicationDelta(long Revision, BlackboardReplicationAudience Audience,
    IReadOnlyDictionary<string, string> Values);

/// <summary>
/// Safe ForgeScript/runtime façade over the canonical typed blackboard. Authoring remains governed by
/// SetModuleConfigurationHandler; runtime values can only change through this validating service.
/// </summary>
public sealed class ProjectBlackboardScriptService
{
    private readonly ProjectBlackboard _blackboard;
    private readonly IReadOnlyDictionary<ProjectVariableId, ProjectVariableDefinition> _byId;
    private long _revision;

    public ProjectBlackboardScriptService(IReadOnlyList<ProjectVariableDefinition> definitions,
        bool allowUndeclared = false)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        _blackboard = new ProjectBlackboard(definitions, allowUndeclared);
        _byId = definitions.ToDictionary(item => item.Id);
        UsesLegacyCompatibility = allowUndeclared;
    }

    public IReadOnlyList<ProjectVariableDefinition> Definitions => _blackboard.Definitions;

    /// <summary>
    /// True only for schema-less legacy modules. As soon as a module declares one typed variable,
    /// undeclared keys are rejected instead of silently bypassing its schema.
    /// </summary>
    public bool UsesLegacyCompatibility { get; }

    public static ProjectBlackboardScriptService ForRuntime(
        IReadOnlyList<ProjectVariableDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        return new ProjectBlackboardScriptService(definitions, allowUndeclared: definitions.Count == 0);
    }

    public bool TryGetValue(string key, out string value) => _blackboard.TryGetValue(key, out value!);

    public bool TrySet(string key, string value, out string? diagnostic)
    {
        var succeeded = _blackboard.TrySet(key, value, out diagnostic);
        if (succeeded) _revision++;
        return succeeded;
    }

    public IReadOnlyDictionary<string, string> Snapshot(ProjectVariableScope? scope = null) =>
        _blackboard.Snapshot(scope);

    public IReadOnlyList<string> Load(IReadOnlyDictionary<string, string> values)
    {
        var diagnostics = _blackboard.Load(values);
        if (values.Count > diagnostics.Count) _revision++;
        return diagnostics;
    }

    public IReadOnlyList<ProjectVariableHandle> Query(string? query = null, ProjectVariableScope? scope = null)
    {
        var normalized = query?.Trim() ?? string.Empty;
        return _blackboard.Definitions
            .Where(item => scope is null || item.Scope == scope)
            .Where(item => normalized.Length == 0 || item.Id.ToString().Contains(normalized,
                               StringComparison.OrdinalIgnoreCase) ||
                           item.Key.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                           item.Description.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .Select(Handle)
            .ToArray();
    }

    public ProjectVariableRead Read(ProjectVariableHandle handle)
    {
        var definition = Resolve(handle);
        if (!_blackboard.TryGetValue(definition.Key, out var value))
            throw new KeyNotFoundException($"Blackboard variable '{definition.Key}' is unavailable.");
        return new ProjectVariableRead(Handle(definition), value, _revision);
    }

    public ProjectVariableMutation Set(ProjectVariableHandle handle, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ProjectVariableDefinition definition;
        try { definition = Resolve(handle); }
        catch (KeyNotFoundException exception) { return new(false, null, null, exception.Message); }
        var before = Read(Handle(definition));
        if (!_blackboard.TrySet(definition.Key, value, out var diagnostic))
            return new(false, before, null, diagnostic);
        _revision++;
        return new(true, before, Read(Handle(definition)), null);
    }

    public BlackboardReplicationDelta ReplicationDelta(BlackboardReplicationAudience audience,
        IReadOnlyCollection<ProjectVariableId>? changed = null)
    {
        var filter = changed?.ToHashSet();
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var definition in _blackboard.Definitions)
        {
            if (filter is not null && !filter.Contains(definition.Id)) continue;
            if (!IsVisibleTo(definition.Replication, audience)) continue;
            if (_blackboard.TryGetValue(definition.Key, out var value)) values.Add(definition.Key, value);
        }
        return new(_revision, audience, new ReadOnlyDictionary<string, string>(values));
    }

    public BlackboardDebugSnapshot DebugSnapshot()
    {
        var visible = _blackboard.Definitions.Where(item => item.DebugVisible).Select(item =>
            new BlackboardDebugEntry(Handle(item), Read(Handle(item)).Value, item.Replication,
                item.RequiredAtRuntime)).ToArray();
        return new(_revision, visible, _blackboard.Definitions.Count - visible.Length);
    }

    private ProjectVariableDefinition Resolve(ProjectVariableHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (!_byId.TryGetValue(handle.Id, out var definition) || definition.Key != handle.Key ||
            definition.Kind != handle.Kind || definition.Scope != handle.Scope ||
            definition.SchemaVersion != handle.SchemaVersion)
            throw new KeyNotFoundException($"Unknown or stale project variable handle '{handle.Id}'.");
        return definition;
    }

    private static ProjectVariableHandle Handle(ProjectVariableDefinition item) =>
        new(item.Id, item.Key, item.Kind, item.Scope, item.SchemaVersion);

    private static bool IsVisibleTo(ProjectVariableReplicationMode mode, BlackboardReplicationAudience audience) =>
        mode switch
        {
            ProjectVariableReplicationMode.None => false,
            ProjectVariableReplicationMode.Owner => audience == BlackboardReplicationAudience.Owner,
            ProjectVariableReplicationMode.Party => audience is BlackboardReplicationAudience.Owner or
                BlackboardReplicationAudience.Party,
            ProjectVariableReplicationMode.AllClients => audience != BlackboardReplicationAudience.Server,
            ProjectVariableReplicationMode.ServerAuthority => audience == BlackboardReplicationAudience.Server,
            _ => false,
        };
}
