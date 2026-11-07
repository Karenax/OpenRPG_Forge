"""Decode captured PNGs while preserving every source-material refusal."""
from pathlib import Path
from collections import Counter
import datetime, hashlib, json
from PIL import Image
W=Path('F:/OpenRPG_ImportWork'); CAP=W/'pirate-lod-thumbnail-batch-next-v791'
GUARD=W/'pirate-lod-thumbnail-next-guard-v793/RESULT.v1.json'
O=W/'pirate-lod-refused-capture-file-audit-v809'
assert not O.exists()
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
pins={}
def read(p):
    pins[str(p)]=sha(p);return json.loads(Path(p).read_text('utf-8-sig'))
run=read(CAP/'RUN-RESULT.v1.json'); prep=read(CAP/'PREPARED.v1.json'); batch=read(CAP/'NATIVE-RESULT.v1.json'); guard=read(GUARD)
assert run['unity_exit_code']==0 and run['source_inputs_unchanged'] and not run['input_changes']
assert len(batch['cases'])==len(prep['cases'])==24 and not guard['admitted_ids']
refusals={r['id']:r for r in guard['cases_refused_source_parity']}
assert len(refusals)==24
rows=[]; materials=Counter()
for case,request in zip(batch['cases'],prep['cases']):
    assert case['id']==request['id'] and case['captured']
    native=read(Path(case['receipt']))
    assert native['id']==case['id'] and native['source_object_unchanged'] and native['preview_scene_isolated']
    assert native['requested_lod']==request['requested_lod'] and native['requested_lod_selection_verified']
    refusal=refusals[case['id']]; materials.update(refusal['material_guids'])
    assert len(native['images'])==2
    for image in native['images']:
        p=Path(image['path']);assert p.resolve().is_relative_to(CAP.resolve())
        digest=sha(p);assert digest==image['sha256'];pins[str(p)]=digest
        with Image.open(p) as png:
            png.verify()
        with Image.open(p) as png:
            png.load();assert png.format=='PNG' and png.size==(512,512)
            extrema=png.convert('RGB').getextrema()
        rows.append(dict(id=case['id'],index_candidate=request['index_candidate'],
            image_path=str(p),sha256=digest,png_decoded=True,dimensions_px=[512,512],
            rgb_extrema=extrema,uniform=all(low==high for low,high in extrema),
            clipped_rgb_pixels=image['clipped_rgb_pixels'],material_refusal=refusal))
pins[str(Path(__file__))]=sha(__file__)
changed=[p for p,h in pins.items() if sha(p)!=h];assert not changed,changed
result=dict(status='refused_native_capture_pngs_rehashed_and_decoded_without_admission',
    utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),captured_cases=24,
    decoded_images=len(rows),candidate_images=sum(r['index_candidate'] for r in rows),
    control_images=sum(not r['index_candidate'] for r in rows),
    uniform_images=sum(r['uniform'] for r in rows),clipped_images=sum(r['clipped_rgb_pixels']>0 for r in rows),
    material_refusal_case_counts=dict(materials),rows=rows,input_pins=pins,
    images_admitted=0,source_material_parity_qualified=False,agent_visual_review=False,
    images_installed_in_vault=False,full_import_increment=0)
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in ['rows','input_pins']}))
