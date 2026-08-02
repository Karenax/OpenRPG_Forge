namespace Forge.Domain.Modules;

public sealed class ProjectBlackboard
{
    private readonly IReadOnlyDictionary<string, ProjectVariableDefinition> _definitions;
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private readonly bool _allowUndeclared;

    public ProjectBlackboard(
        IReadOnlyList<ProjectVariableDefinition> definitions,
        bool allowUndeclared = false)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        _definitions = definitions.ToDictionary(definition => definition.Key, StringComparer.Ordinal);
        _allowUndeclared = allowUndeclared;
        foreach (var definition in definitions)
        {
            _values.Add(definition.Key, definition.DefaultValue);
        }
    }

    public IReadOnlyList<ProjectVariableDefinition> Definitions => _definitions.Values
        .OrderBy(definition => definition.Key, StringComparer.Ordinal)
        .ToArray();

    public bool TryGetValue(string key, out string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _values.TryGetValue(key, out value!);
    }

    public bool TrySet(string key, string value, out string? error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        var normalizedKey = key.Trim();
        if (_definitions.TryGetValue(normalizedKey, out var definition))
        {
            try
            {
                _values[normalizedKey] = definition.Normalize(value);
                error = null;
                return true;
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        if (!_allowUndeclared)
        {
            error = $"Variable '{normalizedKey}' is not declared in the module schema.";
            return false;
        }

        _values[normalizedKey] = value;
        error = null;
        return true;
    }

    public IReadOnlyDictionary<string, string> Snapshot(ProjectVariableScope? scope = null)
    {
        if (scope is null)
        {
            return _values.OrderBy(item => item.Key, StringComparer.Ordinal)
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        }

        var keys = _definitions.Values.Where(definition => definition.Scope == scope).Select(definition => definition.Key)
            .ToHashSet(StringComparer.Ordinal);
        return _values.Where(item => keys.Contains(item.Key))
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
    }

    public IReadOnlyList<string> Load(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var diagnostics = new List<string>();
        foreach (var value in values)
        {
            if (!TrySet(value.Key, value.Value, out var error))
            {
                diagnostics.Add(error!);
            }
        }

        return diagnostics;
    }
}
