# Gate 3 campaign storage. Sources and proof chains stay hash-pinned;
# generated evidence can use the private F: root when E: is full.
function Assert-Gate3ProofPath {
    param(
        [Parameter(Mandatory)][string]$LiteralPath,
        [Parameter(Mandatory)][string]$Description
    )
    $full = [IO.Path]::GetFullPath($LiteralPath)
    $privateEvidence = 'F:\OpenRPG_Forge\.gate3-evidence\'
    if ($full.StartsWith('E:\', [StringComparison]::OrdinalIgnoreCase)) {
        Assert-DriveEPath -LiteralPath $full -Description $Description
    }
    elseif (-not $full.StartsWith($privateEvidence, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description must be on E: or beneath the private Gate 3 evidence root on F:"
    }
    Assert-NoExistingReparseAncestor -LiteralPath $full -Description $Description
}
