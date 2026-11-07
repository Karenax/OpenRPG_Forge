"""Recheck all 24 controlled helpers, including the completed generic wood probe."""
from pathlib import Path
import datetime, hashlib, json
W=Path('F:/OpenRPG_ImportWork')
P=W/'unity-library-source-thumbnail-pirate-bench-v2/project'
O=W/'pirate-helper-ownership-recheck-v839'
prior=W/'pirate-helper-ownership-recheck-v790/HELPER-OWNERSHIP.v1.json'
assert not O.exists()
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
audit=json.loads(prior.read_text('utf-8-sig'))
assert audit['status']=='private_helpers_match_repository_or_retained_bench_sources'
rows=list(audit['helpers'])
name='PirateWoodLodMigrationV819.cs'
source=W/'pirate-wood-lod-migration-v819'/name
private=P/'Assets/Editor'/name
assert sha(source)==sha(private)
rows.append(dict(private_path=str(private),controlled_source=str(source),source_kind='retained_owned_bench_source',sha256=sha(source)))
assert len(rows)==len({r['private_path'] for r in rows})==24
for row in rows:
    assert sha(row['private_path'])==sha(row['controlled_source'])==row['sha256']
assert {str(p) for p in (P/'Assets').rglob('*.cs')}=={r['private_path'] for r in rows}
assert not any((P/'Assets').rglob('*.dll'))
O.mkdir()
result=dict(status='private_helpers_match_repository_or_retained_bench_sources',checked_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),helpers=rows,asset_dll_found=False,prior_receipt_sha256=sha(prior),helper_sha256=sha(__file__),unity_launched=False,full_import_increment=0)
(O/'HELPER-OWNERSHIP.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps(dict(status=result['status'],checked_helpers=len(rows),generic_wood_helper_reusable_without_new_private_script=True)))
