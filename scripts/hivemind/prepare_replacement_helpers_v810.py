"""Prepare all observed helper Clippy repairs while other source checks run."""
from pathlib import Path
import datetime, hashlib, json
R=Path(__file__).resolve().parents[2]; source=R/'crates/forge_project/src/zone_authoring/placements.rs'
candidate=R/'scripts/hivemind/candidates/ReviewedReplacementHelpersV810.rs'
O=Path('F:/OpenRPG_ImportWork/reviewed-replacement-helper-repair-v810')
assert not O.exists() and not candidate.exists()
raw=source.read_bytes();text=raw.decode('utf-8').replace('\r\n','\n')
start=text.index('            PlacementEdit::ReplaceAsset {')
end=text.index('            PlacementEdit::ReplacePinnedAsset {',start)
arm=text[start:end];header=arm[:arm.index('            } => {')+len('            } => {')].replace('mut ids,','ids,')
text=text[:start]+header+'''
                replace_asset(
                    placements,
                    ids,
                    (expected_asset_id, expected_asset_hash),
                    (asset_id, asset_hash),
                )?;
            }
'''+text[end:]
for old,new in [
 ('expected: ReplacementPin,','expected: &ReplacementPin,'),
 ('target: ReplacementPin,','target: &ReplacementPin,'),
 ('                    (expected_asset_id, expected_asset_hash, expected_physics),','                    &(expected_asset_id, expected_asset_hash, expected_physics),'),
 ('                    (asset_id, asset_hash, physics),','                    &(asset_id, asset_hash, physics),'),
]:
    assert text.count(old)==1,old
    text=text.replace(old,new)
text+='''
fn replace_asset(
    placements: &mut [ManualPlacement],
    mut ids: Vec<EntityId>,
    expected: (AssetId, ContentHash),
    target: (AssetId, ContentHash),
) -> Result<(), String> {
    if ids.is_empty() || ids.len() > MAX_PLACEMENT_BATCH {
        return Err("Asset replacement requires 1 to 256 objects".into());
    }
    ids.sort_unstable();
    if ids.windows(2).any(|pair| pair[0] == pair[1]) {
        return Err("Asset replacement requires unique object IDs".into());
    }
    if expected == target {
        return Err("Choose a different asset reference".into());
    }
    for id in ids {
        let placement = find(placements, id)?;
        if placement.physics.is_some() {
            return Err("Replacing this asset requires an explicitly reviewed physics package".into());
        }
        if (placement.asset_id, placement.asset_hash) != expected {
            return Err("Placement asset changed; review replacement again".into());
        }
        placement.asset_id = target.0;
        placement.asset_hash = target.1;
    }
    Ok(())
}
'''
candidate.write_text(text,'utf-8');assert source.read_bytes()==raw
O.mkdir();(O/'placements.before.rs').write_bytes(raw)
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
failed=Path('F:/OpenRPG_ImportWork/placement-reviewed-physics-clippy-v807')
result=dict(status='replacement_helpers_and_borrowed_pins_prepared_not_applied',
    utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),source=str(source),source_sha256=sha(source),
    candidate=str(candidate),candidate_sha256=sha(candidate),failure_result_sha256=sha(failed/'RESULT.v1.json'),
    failure_log_sha256=sha(failed/'cargo.log'),applied=False,lints_disabled=False,full_import_increment=0)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps(result))
