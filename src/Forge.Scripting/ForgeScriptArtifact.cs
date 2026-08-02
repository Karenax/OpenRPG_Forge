namespace Forge.Scripting;

public sealed record ForgeScriptArtifact(
    string Source,
    byte[] Bytecode,
    string VirtualMachineVersion,
    string ApiVersion);
