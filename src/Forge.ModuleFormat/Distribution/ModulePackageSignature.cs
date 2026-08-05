namespace Forge.ModuleFormat.Distribution;

public sealed record ModulePackageSignature(
    string FormatVersion,
    string Algorithm,
    string PackageSha256,
    string KeyFingerprint,
    string PublicKey,
    string Signature);
