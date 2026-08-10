namespace Forge.Scripting.Tests;

public sealed class ForgeScriptModulePermissionPolicyTests
{
    private readonly NativeForgeScriptRuntime _runtime = new();
    private static readonly Guid ModuleId = Guid.Parse("15000000-0000-4000-8000-000000000001");

    [Fact]
    public void Canonical_policy_is_deny_default_server_enforced_and_validated()
    {
        var policy = ForgeScriptModulePermissionPolicy.CreateDefault(ModuleId);
        Assert.Empty(ForgeScriptModulePermissionValidator.Validate(policy));

        var weakened = policy with { DenyByDefault = false, ServerEnforced = false, RequireSignedDistribution = false };
        var diagnostics = ForgeScriptModulePermissionValidator.Validate(weakened);

        Assert.Contains(diagnostics, item => item.Code == "script.permission.deny_default_required");
        Assert.Contains(diagnostics, item => item.Code == "script.permission.server_enforcement_required");
        Assert.Contains(diagnostics, item => item.Code == "script.permission.signature_required");
        Assert.All(diagnostics, item => Assert.Equal("error", item.Severity));
    }

    [Fact]
    public void Permission_commands_preview_apply_undo_redo_and_roundtrip_exactly()
    {
        var initial = ForgeScriptModulePermissionPolicy.CreateDefault(ModuleId);
        var history = new ForgeScriptModulePermissionHistory(initial);
        var changed = initial with
        {
            AllowedCapabilities = new HashSet<string>(["world", "utilities"], StringComparer.Ordinal),
            KitGrants = [initial.KitGrants[0] with
            {
                Capabilities = new HashSet<string>(["world", "utilities"], StringComparer.Ordinal),
            }],
            EventBindings = [initial.EventBindings[0] with
            {
                Capabilities = new HashSet<string>(["world", "utilities"], StringComparer.Ordinal),
            }],
        };
        var command = new ForgeScriptModulePermissionCommand(Guid.NewGuid(), 0, changed, "R015 permission preview");

        Assert.True(history.Preview(command).Succeeded);
        Assert.Equal(initial, history.Policy);
        Assert.True(history.Apply(command).Succeeded);
        Assert.Equal(changed, history.Policy);
        Assert.Equal(initial, history.Undo().Policy);
        Assert.Equal(changed, history.Redo().Policy);

        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-r015", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ForgeScriptModulePermissionStore(root);
            store.Save(changed);
            AssertPolicyEquivalent(changed, store.Load());
            Assert.Equal(Path.Combine(root, "scripts", "forge-script-permissions.json"), store.Path);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Authorization_intersects_module_kit_binding_authority_and_signature()
    {
        var policy = ForgeScriptModulePermissionPolicy.CreateDefault(ModuleId);
        var nominal = Request(Set("world"), signerTrusted: true);
        var accepted = ForgeScriptModuleAuthorizationService.Authorize(policy, nominal);
        var undeclared = ForgeScriptModuleAuthorizationService.Authorize(policy, Request(Set("movement"), true));
        var unsigned = ForgeScriptModuleAuthorizationService.Authorize(policy, Request(Set("world"), false));
        var local = ForgeScriptModuleAuthorizationService.Authorize(policy,
            Request(Set("world"), true) with { Authority = ForgeScriptEventAuthority.Local });

        Assert.True(accepted.Succeeded);
        Assert.True(accepted.ShouldExecute);
        Assert.Contains("world", accepted.GrantedCapabilities);
        Assert.Equal("script.permission.capability_denied", Assert.Single(undeclared.Diagnostics).Code);
        Assert.Equal("script.permission.signature_untrusted", Assert.Single(unsigned.Diagnostics).Code);
        Assert.Equal("script.permission.authority_denied", Assert.Single(local.Diagnostics).Code);
    }

    [Fact]
    public void Missing_optional_policy_is_ignored_but_required_policy_is_diagnostic()
    {
        var optional = ForgeScriptModuleAuthorizationService.Authorize(null,
            Request(Set("world"), true) with { RequiredAtRuntime = false });
        var required = ForgeScriptModuleAuthorizationService.Authorize(null,
            Request(Set("world"), true) with { RequiredAtRuntime = true });

        Assert.True(optional.Succeeded);
        Assert.False(optional.ShouldExecute);
        Assert.False(required.Succeeded);
        Assert.Equal("script.permission.required_missing", Assert.Single(required.Diagnostics).Code);
    }

    [Fact]
    public void Unsigned_development_can_be_explicitly_allowed()
    {
        var policy = ForgeScriptModulePermissionPolicy.CreateDefault(ModuleId);
        var decision = ForgeScriptModuleAuthorizationService.Authorize(policy,
            Request(Set("world"), false) with { IsDevelopment = true });

        Assert.True(decision.Succeeded);
        Assert.True(decision.ShouldExecute);
    }

    [Fact]
    public void Authorized_execution_obeys_quota_and_recovers()
    {
        var policy = ForgeScriptModulePermissionPolicy.CreateDefault(ModuleId);
        var service = new ForgeScriptModuleExecutionService(_runtime);
        var nominal = service.Execute(policy, Request(Set("world"), true), "void main() { say(\"r015\"); }");
        var abusive = service.Execute(policy, Request(Set("world"), true),
            "void main() { while (true) { int value = 1; } }", new ForgeScriptQuotas(instructionLimit: 10));
        var recovered = service.Execute(policy, Request(Set("world"), true),
            "void main() { say(\"recovered-r015\"); }");

        Assert.Equal(ForgeScriptExecutionStatus.Completed, nominal.Execution?.Status);
        Assert.Equal(ForgeScriptExecutionStatus.QuotaExceeded, abusive.Execution?.Status);
        Assert.Equal(ForgeScriptExecutionStatus.Completed, recovered.Execution?.Status);
        Assert.Equal("recovered-r015", Assert.Single(recovered.Execution!.Events).Value);
    }

    private static ForgeScriptModuleAuthorizationRequest Request(
        IReadOnlySet<string> capabilities,
        bool signerTrusted) => new(ModuleId, "core", "module.start", "main", capabilities,
        ForgeScriptEventAuthority.Server, signerTrusted, false, true);

    private static IReadOnlySet<string> Set(params string[] values) =>
        values.ToHashSet(StringComparer.Ordinal);

    private static void AssertPolicyEquivalent(
        ForgeScriptModulePermissionPolicy expected,
        ForgeScriptModulePermissionPolicy actual)
    {
        Assert.Equal(expected with
        {
            AllowedCapabilities = actual.AllowedCapabilities,
            KitGrants = actual.KitGrants,
            EventBindings = actual.EventBindings
        }, actual);
        Assert.Equal(expected.AllowedCapabilities.Order(), actual.AllowedCapabilities.Order());
        Assert.Equal(expected.KitGrants.Select(item => (item.Id, item.SchemaVersion, item.Provenance, item.KitId)),
            actual.KitGrants.Select(item => (item.Id, item.SchemaVersion, item.Provenance, item.KitId)));
        Assert.Equal(expected.KitGrants.SelectMany(item => item.Capabilities).Order(),
            actual.KitGrants.SelectMany(item => item.Capabilities).Order());
        Assert.Equal(expected.EventBindings.Select(item => (item.Id, item.SchemaVersion, item.Provenance,
                item.EventName, item.Handler, item.KitId, item.Authority, item.Enabled)),
            actual.EventBindings.Select(item => (item.Id, item.SchemaVersion, item.Provenance,
                item.EventName, item.Handler, item.KitId, item.Authority, item.Enabled)));
        Assert.Equal(expected.EventBindings.SelectMany(item => item.Capabilities).Order(),
            actual.EventBindings.SelectMany(item => item.Capabilities).Order());
    }
}
