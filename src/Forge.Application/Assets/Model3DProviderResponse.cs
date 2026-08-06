namespace Forge.Application.Assets;

public sealed record Model3DProviderResponse(
    string ProviderKey,
    byte[] ObjContent,
    int TriangleCount,
    string LicenseSpdx,
    string Provenance,
    decimal EstimatedCostUsd);
