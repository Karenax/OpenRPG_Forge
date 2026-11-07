"""Rehash the capture epoch and admit only cases outside unresolved material changes."""
from pathlib import Path
from datetime import datetime,timezone
import difflib,hashlib,json,re
W=Path('F:/OpenRPG_ImportWork');CAP=W/'pirate-lod-thumbnail-batch02-v361'
O=W/'pirate-lod-thumbnail-batch02-guard-v362'
PROJECT=W/'unity-library-source-thumbnail-pirate-bench-v2/project'
pins={}
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):
    pins[str(p)]=sha(p);return json.loads(Path(p).read_text('utf-8-sig'))
assert not O.exists()
run=read(CAP/'RUN-RESULT.v1.json');prep=read(CAP/'PREPARED.v1.json');batch=read(CAP/'NATIVE-RESULT.v1.json')
assert run['unity_exit_code']==0 and run['finished_utc']
snapshots={r['live_path']:r for r in read(CAP/'PRIVATE-EPOCH.v1.json')['private_input_snapshots']}
changes=[];blocked={'a441b81a4efaff04a85744086c47c402'}
historical_guard=read(W/'pirate-source-thumbnail-batch-0121-0128-guard-v202/RESULT.v1.json');blocked.update(row['guid'] for row in historical_guard['changes'])
historical_guard2=read(W/'pirate-source-thumbnail-batch-0129-0136-guard-v278/RESULT.v1.json');blocked.update(row['guid'] for row in historical_guard2['changes'])
historical_guard3=read(W/'pirate-source-thumbnail-batch-0145-0152-guard-v297/RESULT.v1.json');blocked.update(row['guid'] for row in historical_guard3['changes'])
historical_guard4=read(W/'pirate-source-thumbnail-batch-0153-0160-guard-v307/RESULT.v1.json');blocked.update(row['guid'] for row in historical_guard4['changes'])
historical_guard5=read(W/'pirate-source-thumbnail-batch-0161-0176-guard-v316/RESULT.v1.json');blocked.update(row['guid'] for row in historical_guard5['changes'])
historical_guard6=read(W/'pirate-source-thumbnail-batch-0177-0192-guard-v325/RESULT.v1.json');blocked.update(row['guid'] for row in historical_guard6['changes'])
for p,h in prep['input_pins'].items():
    actual=sha(p);pins[p]=actual
    if actual==h:continue
    path=Path(p);assert path.is_relative_to(PROJECT) and path.suffix=='.mat',p
    original=Path(snapshots[p]['snapshot']);assert sha(original)==h
    meta=path.with_suffix('.mat.meta');guid=re.search(r'^guid: ([a-f0-9]{32})$',meta.read_text('utf-8-sig'),re.M)[1]
    blocked.add(guid);pins[str(meta)]=sha(meta)
    changes.append(dict(path=p,guid=guid,before_sha256=h,after_sha256=actual,prior_snapshot=str(original),
        diff=''.join(difflib.unified_diff(original.read_text('utf-8-sig').splitlines(True),path.read_text('utf-8-sig').splitlines(True))),classified=False))
assert len(changes)==len(run['input_changes'])
for version in range(1,6):
    path=W/f'pirate-private-material-migrations-reopen-v{version}/RESULT.v1.json'
    if not path.exists():continue
    for row in read(path).get('material_changes',[]):
        if row.get('alpha_to_mask_keyword_moved_to_invalid'):
            meta=Path(row['path']).with_suffix('.mat.meta');pins[str(meta)]=sha(meta)
            blocked.add(re.search(r'^guid: ([a-f0-9]{32})$',meta.read_text('utf-8-sig'),re.M)[1])
admitted=[];refused=[]
assert len(batch['cases'])==len(prep['cases'])==24
for case,expected in zip(batch['cases'],prep['cases']):
    assert case['id']==expected['id']
    if not case['captured']:
        refused.append(dict(id=case['id'],stage='native_capture',reason=case['error']));continue
    native=read(case['receipt']);assert native['source_object_unchanged'] and native['preview_scene_isolated']
    assert native['id']==case['id'] and native['prefab_guid']==expected['prefab_guid']
    used={m.split(':')[0] for r in native['renderers'] for m in r['materials']}
    used|={t['material'].split(':')[0] for t in native['textures']}
    for image in native['images']:
        assert sha(image['path'])==image['sha256'];pins[image['path']]=image['sha256']
    affected=used&blocked
    if affected:refused.append(dict(id=case['id'],stage='source_material_parity',material_guids=sorted(affected),reason='Dedicated source migration qualification required'))
    else:admitted.append(case['id'])
pins[str(Path(__file__))]=sha(Path(__file__))
result=dict(status='twenty_four_lod_capture_epochs_rehashed_with_explicit_material_refusals',admitted_ids=admitted,
    cases_refused_source_parity=refused,changes=changes,input_pins=pins,
    original_sources_unchanged=True,private_import_changes_classified=False,source_version_parity_qualified=False,
    full_import=False,finished_utc=datetime.now(timezone.utc).isoformat())
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps(dict(admitted=len(admitted),refused=refused,changes=changes)))
