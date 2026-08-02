using System.Runtime.InteropServices;
using System.Text;

namespace Forge.Scripting;

public sealed class NativeForgeScriptRuntime : IForgeScriptRuntime
{
    private const int BytecodeCapacity = 2 * 1024 * 1024;
    private const int TextCapacity = 128 * 1024;

    static NativeForgeScriptRuntime()
    {
        NativeLibrary.SetDllImportResolver(typeof(NativeForgeScriptRuntime).Assembly, ResolveNativeLibrary);
    }

    public string VirtualMachineVersion => ReadVersion(NativeMethods.GetVirtualMachineVersion());

    public string ApiVersion => ReadVersion(NativeMethods.GetApiVersion());

    public ForgeScriptCompileResult Compile(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        var bytecode = new byte[BytecodeCapacity];
        var diagnostics = new StringBuilder(TextCapacity);
        var size = NativeMethods.Compile(
            source,
            bytecode,
            bytecode.Length,
            diagnostics,
            diagnostics.Capacity);
        var parsedDiagnostics = ParseDiagnostics(diagnostics.ToString());
        if (size <= 0)
        {
            return new ForgeScriptCompileResult(false, null, parsedDiagnostics);
        }

        return new ForgeScriptCompileResult(
            true,
            new ForgeScriptArtifact(
                source,
                bytecode[..size],
                VirtualMachineVersion,
                ApiVersion),
            parsedDiagnostics);
    }

    public ForgeScriptExecutionResult Execute(ForgeScriptArtifact artifact, ForgeScriptQuotas? quotas = null)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        if (!string.Equals(artifact.VirtualMachineVersion, VirtualMachineVersion, StringComparison.Ordinal) ||
            !string.Equals(artifact.ApiVersion, ApiVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"ForgeScript artifact VM/API {artifact.VirtualMachineVersion}/{artifact.ApiVersion} " +
                $"does not match host {VirtualMachineVersion}/{ApiVersion}.");
        }

        quotas ??= new ForgeScriptQuotas();
        var events = new StringBuilder(TextCapacity);
        var diagnostics = new StringBuilder(TextCapacity);
        var code = NativeMethods.Execute(
            artifact.Bytecode,
            artifact.Bytecode.Length,
            quotas.InstructionLimit,
            quotas.TimeoutMilliseconds,
            quotas.RecursionLimit,
            quotas.SpawnCap,
            events,
            events.Capacity,
            diagnostics,
            diagnostics.Capacity);
        var status = code switch
        {
            0 => ForgeScriptExecutionStatus.Completed,
            1 => ForgeScriptExecutionStatus.QuotaExceeded,
            _ => ForgeScriptExecutionStatus.Failed,
        };
        return new ForgeScriptExecutionResult(
            status,
            ParseEvents(events.ToString()),
            ParseDiagnostics(diagnostics.ToString()));
    }

    private static string ReadVersion(IntPtr pointer) =>
        Marshal.PtrToStringUTF8(pointer)
        ?? throw new InvalidOperationException("The native ForgeScript host returned an empty version.");

    private static IntPtr ResolveNativeLibrary(
        string libraryName,
        System.Reflection.Assembly assembly,
        DllImportSearchPath? searchPath)
    {
        _ = searchPath;
        if (!string.Equals(libraryName, "forge_script_native", StringComparison.Ordinal))
        {
            return IntPtr.Zero;
        }

        var assemblyDirectory = Path.GetDirectoryName(assembly.Location) ?? AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(assemblyDirectory, "forge_script_native.dll"),
            Path.Combine(AppContext.BaseDirectory, "forge_script_native.dll"),
            Path.Combine(Environment.CurrentDirectory, "native", "forge_script_native.dll"),
        };
        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out var handle))
            {
                return handle;
            }
        }

        return IntPtr.Zero;
    }

    private static IReadOnlyList<ForgeScriptEvent> ParseEvents(string value) => value
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(line => line.Split('|', 2))
        .Select(parts => new ForgeScriptEvent(parts[0], parts.Length == 2 ? parts[1] : string.Empty))
        .ToArray();

    private static IReadOnlyList<ForgeScriptDiagnostic> ParseDiagnostics(string value) => value
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(line => line.Split(':', 5))
        .Select(parts => new ForgeScriptDiagnostic(
            parts.ElementAtOrDefault(0) ?? "native",
            int.TryParse(parts.ElementAtOrDefault(1), out var line) ? line : 0,
            int.TryParse(parts.ElementAtOrDefault(2), out var column) ? column : 0,
            parts.ElementAtOrDefault(3) ?? "error",
            parts.ElementAtOrDefault(4) ?? parts.ElementAtOrDefault(0) ?? "Unknown ForgeScript diagnostic."))
        .ToArray();

    private static class NativeMethods
    {
        private const string Library = "forge_script_native";

        [DllImport(Library, EntryPoint = "forge_script_vm_version")]
        internal static extern IntPtr GetVirtualMachineVersion();

        [DllImport(Library, EntryPoint = "forge_script_api_version")]
        internal static extern IntPtr GetApiVersion();

        [DllImport(Library, EntryPoint = "forge_script_compile", CharSet = CharSet.Ansi)]
        internal static extern int Compile(
            string source,
            [Out] byte[] bytecode,
            int bytecodeCapacity,
            [Out] StringBuilder diagnostics,
            int diagnosticsCapacity);

        [DllImport(Library, EntryPoint = "forge_script_execute")]
        internal static extern int Execute(
            byte[] bytecode,
            int bytecodeSize,
            int instructionQuota,
            int timeoutMilliseconds,
            int recursionLimit,
            int spawnCap,
            [Out] StringBuilder events,
            int eventsCapacity,
            [Out] StringBuilder diagnostics,
            int diagnosticsCapacity);
    }
}
