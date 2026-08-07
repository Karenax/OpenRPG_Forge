using Forge.Domain.RuntimeExtensions;

namespace Forge.Mcp.Projects;

public sealed record PostLaunchCapabilityResource(int SchemaVersion, long Revision, string Section,
    string Summary, PostLaunchCapabilityProfile Profile);

public sealed record PostLaunchPreviewRequest(int SchemaVersion, string Kind,
    IReadOnlyDictionary<string, string> Arguments);

public sealed record PostLaunchPreviewResult(bool Succeeded, string Code, string SemanticDiff,
    IReadOnlyList<AgentDiagnostic> Diagnostics);
