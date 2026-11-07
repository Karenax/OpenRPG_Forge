"""Stage the observed Clippy repair without changing sources under live checks."""
from pathlib import Path
import datetime, hashlib, json
R=Path(__file__).resolve().parents[2]
source=R/'crates/forge_project/src/zone_authoring/placements.rs'
candidate=R/'scripts/hivemind/candidates/ReviewedPhysicsReplacementRefactorV804.rs'
O=Path('F:/OpenRPG_ImportWork/reviewed-physics-replacement-refactor-v804')
assert not O.exists() and not candidate.exists()
raw=source.read_bytes(); text=raw.decode('utf-8').replace('\r\n','\n')
start=text.index('            PlacementEdit::ReplacePinnedAsset {')
end=text.index('            PlacementEdit::SetLocked {',start)
arm=text[start:end]
header=arm[:arm.index('            } => {')+len('            } => {')]
header=header.replace('mut ids,','ids,')
new=header+'''
                replace_pinned_asset(
                    placements,
                    ids,
                    (expected_asset_id, expected_asset_hash, expected_physics),
                    (asset_id, asset_hash, physics),
                )?;
            }
'''
text=text[:start]+new+text[end:]
text+='''
type ReplacementPin = (AssetId, ContentHash, Option<Box<PhysicsPackageReference>>);

fn replace_pinned_asset(
    placements: &mut [ManualPlacement],
    mut ids: Vec<EntityId>,
    expected: ReplacementPin,
    target: ReplacementPin,
) -> Result<(), String> {
    if ids.is_empty() || ids.len() > MAX_PLACEMENT_BATCH {
        return Err("Asset replacement requires 1 to 256 objects".into());
    }
    ids.sort_unstable();
    if ids.windows(2).any(|pair| pair[0] == pair[1]) {
        return Err("Asset replacement requires unique object IDs".into());
    }
    if expected == target {
        return Err("Choose a different asset or physics reference".into());
    }
    for id in ids {
        let placement = find(placements, id)?;
        if (placement.asset_id, placement.asset_hash, &placement.physics)
            != (expected.0, expected.1, &expected.2)
        {
            return Err("Placement reference changed; review replacement again".into());
        }
        placement.asset_id = target.0;
        placement.asset_hash = target.1;
        placement.physics.clone_from(&target.2);
    }
    Ok(())
}
'''
candidate.write_text(text,'utf-8'); assert source.read_bytes()==raw
O.mkdir(); (O/'placements.before.rs').write_bytes(raw)
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
failed=Path('F:/OpenRPG_ImportWork/placement-reviewed-physics-clippy-v802')
result=dict(status='clippy_function_extraction_prepared_not_applied',utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
    source=str(source),source_sha256=sha(source),candidate=str(candidate),candidate_sha256=sha(candidate),
    failure_result_sha256=sha(failed/'RESULT.v1.json'),failure_log_sha256=sha(failed/'cargo.log'),
    reason='prepare_placement exceeds Clippy 100-line limit after new replacement branch',
    lints_disabled=False,applied=False,full_import_increment=0)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8'); print(json.dumps(result))
