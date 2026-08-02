namespace Forge.Scripting;

public interface IForgeScriptRuntime
{
    string VirtualMachineVersion { get; }

    string ApiVersion { get; }

    ForgeScriptCompileResult Compile(string source);

    ForgeScriptExecutionResult Execute(ForgeScriptArtifact artifact, ForgeScriptQuotas? quotas = null);
}
