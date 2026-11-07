"""Admit original source thumbnails with measured, archived texture limitations."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
from PIL import Image
W=Path('F:/OpenRPG_ImportWork');O=W/'mountain-original-limited-images-v945'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    original=W/'mountain-exact-capture-review-v897/RESULT.v1.json';visual=W/'mountain-capture-agent-review-v898/RESULT.v1.json'
    limits=W/'mountain-original-texture-limitations-v944/RESULT.v1.json';archive=W/'mountain-blank-source-archive-proof-v943/RESULT.v1.json';migration=W/'mountain-sdk-image-migration-v918/RESULT.v1.json'
    reviewed,holds,lim,arch=[read(p) for p in [original,visual,limits,archive]]
    expected={h['id'] for h in holds['visual_holds']};assert len(expected)==3
    pins={str(p):sha(p) for p in [original,visual,limits,archive,migration,Path(__file__).resolve()]}
    for proof in [reviewed,lim]:
        for p,d in proof['input_pins'].items():assert sha(p)==d,p;pins[p]=d
    assert sha(arch['archive'])==arch['archive_sha256']
    for r in arch['rows']:assert r['rgba_all_zero'] and r['source_sha256']==r['archived_sha256'] and sha(r['source'])==r['source_sha256'];pins[r['source']]=r['source_sha256']
    native_run=W/'mountain-generalorm-controls-run-v939/RUN-RESULT.v1.json';run=read(native_run);assert run['unity_exit_code']==0 and run['all_views_repeat_exact'] and not run['input_changes'] and not run['refused_cases']
    prep_path=W/'mountain-generalorm-controls-v938/PREPARED.v1.json';prep=read(prep_path);pins[str(native_run)]=sha(native_run);pins[str(prep_path)]=sha(prep_path)
    controls={prep['cases'][i]['source_card_id']:(prep['cases'][i+1]['output'],read(prep['cases'][i+1]['output'])) for i in range(0,18,6)}
    rows=[r for r in reviewed['rows'] if r['id'] in expected];assert len(rows)==3
    for r in rows:
        prior_native=read(r['native_receipt']);assert sha(r['native_receipt'])==r['native_receipt_sha256']
        receipt,native=controls[r['id']]
        assert prior_native['selected_mesh_identities']==native['selected_mesh_identities'] and prior_native['selected_mesh_vertex_counts']==native['selected_mesh_vertex_counts']
        assert native['source_object_unchanged'] and not native['diagnostic_white_base_map']
        r['previous_capture_raw_matches']=[a['raw_sha256']==b['raw_sha256'] for a,b in zip(r['images'],native['images'])]
        r['native_receipt']=receipt;r['native_receipt_sha256']=sha(receipt);r['images']=native['images'];pins[receipt]=sha(receipt)
        for image in r['images']:
            assert sha(image['path'])==image['sha256'] and sha(image['raw_path'])==image['raw_sha256']
            with Image.open(image['path']) as png:assert png.size==(512,512);png.load()
            assert image['clipped_rgb_pixels']==0
            pins[image['path']]=image['sha256'];pins[image['raw_path']]=image['raw_sha256']
    O.mkdir(exist_ok=False)
    result=dict(status='three_original_mountain_thumbnails_reviewed_with_archived_texture_limits',finished_utc=datetime.now(timezone.utc).isoformat(),cards_verified=3,images_verified=6,cards_refused=[],rows=rows,input_pins=pins,private_import_migration_receipt=str(migration),agent_review=True,human_review=False,conversion_qualified=False,full_import=False,source_visual_limits=dict(kind='original-source-texture-rgba-zero',textures=arch['rows'],archive_sha256=arch['archive_sha256'],evidence_sha256=sha(archive),scope='Original source images only; dark trunk/branches retained. Texture repair, old SDK parity and whole root cause unqualified.'),visual_observations='Both original views of each source were actually inspected: complete silhouette, foliage and black trunk/branches preserved. Prior hold becomes a documented original-source visual limitation after full RGBA and original archive verification. No white texture/material/vertex-color diagnostic images admitted. Source_support remains nonconverted/nonplaceable; renderer, runtime, old SDK parity and human review unqualified.')
    (O/'RESULT.v1.json').write_text(json.dumps(result,indent=2,ensure_ascii=False),'utf-8');print(json.dumps(dict(cards_verified=3,images_verified=6,source_visual_limitation_retained=True)))
if __name__=='__main__':main()
