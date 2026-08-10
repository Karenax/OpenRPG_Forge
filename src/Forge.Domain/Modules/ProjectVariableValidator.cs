using Forge.Domain.Validation;

namespace Forge.Domain.Modules;

public static class ProjectVariableValidator
{
    public const string IdentityCode = "project.variable.identity";
    public const string SchemaCode = "project.variable.schema_unsupported";
    public const string DuplicateIdCode = "project.variable.id_duplicate";
    public const string DuplicateKeyCode = "project.variable.key_duplicate";
    public const string RequiredSessionCode = "project.variable.required_session_only";
    public const string ServerReplicationCode = "project.variable.server_replication_scope";

    public static IReadOnlyList<DomainDiagnostic> Validate(IReadOnlyList<ProjectVariableDefinition> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        var diagnostics = new List<DomainDiagnostic>();
        foreach (var variable in variables)
        {
            var path = $"module/variables/{variable.Id}";
            if (variable.Id.Value == Guid.Empty)
                diagnostics.Add(new(IdentityCode, "Project variable requires a stable UUID.", $"{path}/id"));
            if (variable.SchemaVersion != ProjectVariableDefinition.CurrentSchemaVersion)
                diagnostics.Add(new(SchemaCode,
                    $"Project variable '{variable.Id}' uses unsupported schema {variable.SchemaVersion}.",
                    $"{path}/schemaVersion"));
            if (variable.RequiredAtRuntime && variable.Scope == ProjectVariableScope.Session)
                diagnostics.Add(new(RequiredSessionCode,
                    $"Required variable '{variable.Key}' must survive beyond a transient session.",
                    $"{path}/scope"));
            if (variable.Replication == ProjectVariableReplicationMode.ServerAuthority &&
                variable.Scope != ProjectVariableScope.Server)
                diagnostics.Add(new(ServerReplicationCode,
                    $"Server-authoritative variable '{variable.Key}' must use server scope.",
                    $"{path}/replication"));
        }

        foreach (var duplicate in variables.GroupBy(item => item.Id).Where(group => group.Count() > 1))
            diagnostics.Add(new(DuplicateIdCode, $"Project variable UUID '{duplicate.Key}' is duplicated.",
                $"module/variables/{duplicate.Key}/id"));
        foreach (var duplicate in variables.GroupBy(item => item.Key, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
            diagnostics.Add(new(DuplicateKeyCode, $"Project variable key '{duplicate.Key}' is duplicated.",
                $"module/variables/{duplicate.First().Id}/key"));
        return diagnostics;
    }
}
