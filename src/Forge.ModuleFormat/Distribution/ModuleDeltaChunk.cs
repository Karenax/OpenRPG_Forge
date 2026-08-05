namespace Forge.ModuleFormat.Distribution;

public sealed record ModuleDeltaChunk(int Index, string Sha256, int Length, bool Included);
