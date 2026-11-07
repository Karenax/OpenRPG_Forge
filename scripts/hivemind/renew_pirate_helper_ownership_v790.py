"""Recheck every private helper against a known controlled source; no Unity launch."""
from pathlib import Path
import datetime, hashlib, json
R=Path(__file__).resolve().parents[2]; W=Path('F:/OpenRPG_ImportWork')
P=W/'unity-library-source-thumbnail-pirate-bench-v2/project'
O=W/'pirate-helper-ownership-recheck-v790'
prior=W/'pirate-lod-thumbnail-batch04-v491/HELPER-OWNERSHIP.v1.json'
assert not O.exists()
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
audit=json.loads(prior.read_text('utf-8-sig'))
rows=list(audit['helpers'])
extra={
 'PiratePalmContractV524.cs': W/'pirate-palm-native-contract-v524/PiratePalmContractV524.cs',
 'PiratePalmContractV527.cs': W/'pirate-palm-native-contract-v527/PiratePalmContractV527.cs',
 'PiratePalmContractV535.cs': W/'pirate-palm-native-contract-v535/PiratePalmContractV535.cs',
 'PirateWoodLodMigrationV591.cs': W/'pirate-wood-lod-migration-v591/PirateWoodLodMigrationV591.cs',
}
for version in [541,547,558]:
    name=f'PiratePalmWindReferenceV{version}.cs'
    extra[name]=R/'scripts/hivemind/candidates'/name
for name,source in extra.items():
    private=P/'Assets/Editor'/name
    assert sha(private)==sha(source),name
    rows.append(dict(private_path=str(private),controlled_source=str(source),
        source_kind='repository_candidate' if source.is_relative_to(R) else 'retained_owned_bench_source',sha256=sha(source)))
assert len({r['private_path'] for r in rows})==len(rows)
for row in rows:
    assert sha(row['private_path'])==sha(row['controlled_source'])==row['sha256'],row
actual={str(p) for p in (P/'Assets').rglob('*.cs')}
assert actual=={r['private_path'] for r in rows}
assert not any((P/'Assets').rglob('*.dll'))
O.mkdir()
result=dict(status='private_helpers_match_repository_or_retained_bench_sources',
    checked_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),helpers=rows,
    asset_dll_found=False,prior_receipt_sha256=sha(prior),helper_sha256=sha(__file__),
    prior_capture_attempt='v789 refused before preparing or launching Unity because seven owned helpers were absent from the old receipt',
    unity_launched=False,full_import_increment=0)
(O/'HELPER-OWNERSHIP.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps(dict(status=result['status'],checked_helpers=len(rows),added_owned_helpers=len(extra))))
