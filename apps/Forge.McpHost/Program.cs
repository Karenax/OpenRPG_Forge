using Forge.Mcp.Bridge;
using Forge.Mcp.Projects;
using Forge.Mcp.LiveDirector;
using Forge.Mcp.Network;
using Forge.Mcp.Scripts;
using Forge.Mcp.Serialization;
using Forge.Mcp.Testing;
using Forge.McpHost;
using Forge.Scripting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

const string McpSdkVersion = "1.4.1";

var arguments = ArgumentMap.Parse(args);
if (!arguments.TryGetValue("workspace", out var workspaceDirectory))
{
    Console.Error.WriteLine("Missing --workspace <Forge project directory>.");
    return 2;
}

var opened = ForgeProjectAgentService.Open(workspaceDirectory);
if (opened.Service is null)
{
    foreach (var diagnostic in opened.Diagnostics)
    {
        Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
    }

    return 3;
}

var project = opened.Service;
if (arguments.ContainsKey("self-test"))
{
    return await RunSelfTestAsync(project);
}

if (arguments.ContainsKey("scenario-test"))
{
    return await RunScenarioTestAsync(arguments, workspaceDirectory);
}

var toolSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    PropertyNameCaseInsensitive = true,
    TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
};
toolSerializerOptions.Converters.Add(new JsonStringEnumConverter());
toolSerializerOptions.Converters.Add(new McpVector3ValueJsonConverter());
toolSerializerOptions.Converters.Add(new McpQuaternionValueJsonConverter());

var builder = Host.CreateApplicationBuilder();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Services.AddSingleton(project);
builder.Services.AddSingleton<IForgeScriptRuntime, NativeForgeScriptRuntime>();
builder.Services.AddSingleton<ForgeScriptAgentService>();
builder.Services.AddSingleton<ForgeScriptInteropService>();
builder.Services.AddSingleton<NetworkFoundationAgentService>();
builder.Services.AddSingleton<NetworkOperationsAgentService>();
builder.Services.AddSingleton<NetworkAssuranceAgentService>();
builder.Services.AddSingleton<LiveDirectorAccessAgentService>();
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly(serializerOptions: toolSerializerOptions);
await builder.Build().RunAsync();
return 0;

static async Task<int> RunSelfTestAsync(ForgeProjectAgentService project)
{
    var scripts = new ForgeScriptAgentService(new NativeForgeScriptRuntime());
    var sandbox = scripts.TestInSandbox("void main() { say(\"mcp-ready\"); }");
    var discoveryDirectory = Path.Combine(Path.GetTempPath(), "openrpg-forge-mcp", Guid.NewGuid().ToString("N"));
    await using var bridge = new ForgeEditorBridgeServer(project, discoveryDirectory, TimeSpan.FromMinutes(5));
    await bridge.StartAsync();
    var client = ForgeEditorBridgeClient.FromDiscoveryFile(bridge.Discovery.DiscoveryPath);
    var first = await client.QueryAsync("project.summary");
    var second = await client.QueryAsync("project.revision");
    var diagnostics = project.GetDiagnostics();
    var assets = project.GetAssets(new Forge.Mcp.Assets.AgentAssetCatalogQuery(MaximumResults: 1));
    if (!sandbox.Succeeded || !first.Succeeded || !second.Succeeded || diagnostics.Count > 0 ||
        assets.CatalogCount < 674 || assets.Diagnostics.Count > 0)
    {
        Console.Error.WriteLine("OPENRPG_FORGE_MCP_HOST_SMOKE_FAILED");
        return 4;
    }

    Console.WriteLine(
        $"OPENRPG_FORGE_MCP_HOST_SMOKE_PASS sdk={McpSdkVersion} transport=stdio " +
        $"bridge={bridge.Discovery.Transport} revision={project.GetCurrentRevision()} diagnostics={diagnostics.Count} " +
        $"assets={assets.CatalogCount}");
    return 0;
}

static async Task<int> RunScenarioTestAsync(ArgumentMap arguments, string workspaceDirectory)
{
    if (!arguments.TryGetValue("engine", out var engine) ||
        !arguments.TryGetValue("runtime", out var runtime) ||
        !arguments.TryGetValue("module", out var module) ||
        !arguments.TryGetValue("artifact", out var artifact))
    {
        Console.Error.WriteLine("Scenario test requires --engine, --runtime, --module and --artifact.");
        return 5;
    }

    engine = Path.GetFullPath(engine);
    runtime = Path.GetFullPath(runtime);
    module = Path.GetFullPath(module);
    artifact = Path.GetFullPath(artifact);
    var engineLog = Path.ChangeExtension(artifact, ".engine.log");
    var request = new ScenarioTestRequest(
        engine,
        [
            "--headless",
            "--path",
            runtime,
            "--log-file",
            engineLog,
            "--",
            "--module-package",
            module,
            "--smoke-test",
        ],
        workspaceDirectory,
        [
            "OPENRPG_FORGE_RUNTIME_MODULE_LOADED slug=crypt-of-ashes",
            "OPENRPG_FORGE_RUNTIME_SPAWN_SELECTED id=20000000-0000-4000-8000-000000000010",
            "OPENRPG_FORGE_RUNTIME_PROJECT_LOADED entities=40 collisions=30",
            "OPENRPG_FORGE_RUNTIME_PERF startup_ms=",
            "OPENRPG_FORGE_FORGESCRIPT_SMOKE_PASS",
            "OPENRPG_FORGE_LOOT_GRANTED item=warden-ember quantity=1",
            "OPENRPG_FORGE_RUNTIME_SMOKE_PASS",
        ],
        TimeSpan.FromSeconds(45),
        artifact);
    var result = await new ScenarioTestAgent().RunAsync(request);
    if (!result.Succeeded)
    {
        foreach (var diagnostic in result.Diagnostics)
        {
            Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
        }

        return 6;
    }

    Console.WriteLine(
        $"OPENRPG_FORGE_MCP_SCENARIO_PASS exit={result.ExitCode} " +
        $"markers={result.FoundMarkers.Count} artifact={result.ArtifactLogPath}");
    return 0;
}

internal sealed class ArgumentMap : Dictionary<string, string>
{
    private ArgumentMap()
        : base(StringComparer.Ordinal)
    {
    }

    public static ArgumentMap Parse(IReadOnlyList<string> arguments)
    {
        var result = new ArgumentMap();
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = argument[2..];
            var value = index + 1 < arguments.Count && !arguments[index + 1].StartsWith("--", StringComparison.Ordinal)
                ? arguments[++index]
                : string.Empty;
            result[key] = value;
        }

        return result;
    }
}
