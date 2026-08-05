using Forge.Domain.Modules;
using System.Text.Json.Serialization;

namespace Forge.ModuleFormat.Packages;

[method: JsonConstructor]
public sealed record ModuleManifestDependency(
    ThirdPartyDependencyId Id,
    string Name,
    string Version,
    string LicenseSpdx,
    string Owner,
    string SourceReference,
    DependencyApprovalState ApprovalState,
    DependencyRiskLevel RiskLevel,
    IReadOnlyList<string> ApproverRoles,
    int RequiredApprovals,
    string SecurityAuditReference)
{
    public ModuleManifestDependency(ThirdPartyDependencyId id, string name, string version,
        string licenseSpdx, string owner, string sourceReference)
        : this(id, name, version, licenseSpdx, owner, sourceReference,
            DependencyApprovalState.Approved, DependencyRiskLevel.Low,
            [owner, "security-review"], 2, sourceReference)
    {
    }
}
