"""Seal the agent review of all 32 P14 views; no human or runtime verdict."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');O=W/'pirate-p14-combined-review-v330'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
direct=W/'pirate-direct-thumbnail-review-v326/CANDIDATES.v1.json'
migrated=W/'pirate-p14-migration-review-v329/CANDIDATES.v1.json'
d,m=read(direct),read(migrated)
assert len(d['rows'])==9 and len(m['rows'])==7 and not m['refusals']
rows=d['rows']+m['rows'];assert len({r['id'] for r in rows})==16
assert all(p['migration_exact'] and p['repeat_exact'] for r in m['rows'] for p in r['comparisons'])
pins=d['input_pins']|m['input_pins']
for p in [Path(__file__),direct,migrated,*sorted((migrated.parent).glob('review-group-*.png'))]:pins[str(p)]=sha(p)
for p,h in pins.items():assert sha(Path(p))==h,p
result=dict(status='sixteen_p14_source_thumbnails_agent_reviewed',cards_verified=16,images_verified=32,cards_refused=0,rows=rows,input_pins=pins,private_import_migration_receipt=m['private_import_migration_receipt'],agent_review=True,human_review=False,source_version_parity_qualified=False,original_version_pipeline_equivalence=False,conversion_qualified=False,full_import=False,finished_utc=datetime.now(timezone.utc).isoformat(),visual_observations='All 32 views inspected. Broken barrel, two baton variants, low and tall cages, two crate variants, wooden beam and pole: complete and textured. Cart A is a standalone wheel; carts B/C are damaged cart variants. Door has distinct front/back braces. Log, bundled logs and stump are distinct. No pink shader errors, uniform images or frame cut-off observed. No functional or animation qualification.')
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print('Sixteen cards and 32 views reviewed; seven migrations exactly compared and repeated')
