namespace Forge.ModuleFormat.Distribution;

public sealed record ModulePackageSignatureVerification(
    bool IsValid,
    bool IsTrusted,
    bool IsRevoked,
    string PackageSha256,
    string KeyFingerprint,
    string? DiagnosticCode,
    string? Message);
