"""Seal SDK scope for 47 cards and visually admit 44, keeping three holds."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
from PIL import Image
W=Path('F:/OpenRPG_ImportWork');M=W/'mountain-sdk-image-migration-v918';O=W/'mountain-reviewed-images-v919'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def write(p,v):
    with p.open('x',encoding='utf-8') as f:json.dump(v,f,indent=2,ensure_ascii=False,allow_nan=False)
def main():
    sources=[W/'mountain-exact-capture-review-v897/RESULT.v1.json',W/'mountain-capture-agent-review-v898/RESULT.v1.json',W/'mountain-sdk-partial-review-v911/RESULT.v1.json',W/'mountain-waterfall-typed-review-v916/RESULT.v1.json']
    decoded,visual,partial,waterfall=[read(p) for p in sources]
    assert len(decoded['rows'])==47 and partial['cards_verified']==46 and waterfall['all_sdk_reconstructed_views_raw_exact']
    pins={str(p):sha(p) for p in sources}
    for proof in [decoded,visual,partial,waterfall]:
        for p,d in proof['input_pins'].items():assert sha(p)==d,p;pins[p]=d
    held={r['id']:r for r in visual['visual_holds']};assert len(held)==3
    verified={r['id']:read(r['native_receipts'][1]) for r in partial['rows']}
    prep_path=W/'mountain-waterfall-typed-controls-v914/PREPARED.v1.json';prep=read(prep_path)
    verified[waterfall['source_id']]=read(prep['cases'][1]['output'])
    assert set(verified)=={r['id'] for r in decoded['rows']}
    rows=[]
    for row in decoded['rows']:
        native=read(row['native_receipt'])
        assert sha(row['native_receipt'])==row['native_receipt_sha256']
        assert native['selected_mesh_identities']==verified[row['id']]['selected_mesh_identities']
        for image in row['images']:
            assert sha(image['path'])==image['sha256'] and sha(image['raw_path'])==image['raw_sha256']
            assert image['raw_sha256']==verified[row['id']]['images'][image['view']]['raw_sha256']
            with Image.open(image['path']) as png:assert png.size==(512,512);png.load()
            assert image['clipped_rgb_pixels']==0
            pins[image['path']]=image['sha256'];pins[image['raw_path']]=image['raw_sha256']
        if row['id'] not in held:rows.append(row)
    assert len(rows)==44
    M.mkdir(exist_ok=False)
    migration=dict(status='installed_hdrp_sdk_author_reconstruction_raw_equivalence_verified_47_cards',cards_verified=47,views_verified=94,input_pins={str(p):sha(p) for p in sources[2:]},original_sdk_pipeline_equivalence=False,full_import=False)
    write(M/'RESULT.v1.json',migration);pins[str(M/'RESULT.v1.json')]=sha(M/'RESULT.v1.json')
    O.mkdir(exist_ok=False)
    review=dict(status='44_mountain_source_cards_visually_reviewed_three_held',finished_utc=datetime.now(timezone.utc).isoformat(),cards_verified=44,images_verified=88,cards_refused=list(held.values()),rows=rows,input_pins=pins,private_import_migration_receipt=str(M/'RESULT.v1.json'),agent_review=True,human_review=False,conversion_qualified=False,full_import=False,visual_observations='All eight contact sheets actually inspected. Statues, birds, grass, flowers, heather, leaf trees, MountainTree01 LOD variants, plants and waterfall are framed and recognizable without pink shader failures or clipped PNG pixels. Three MountainTree02/03 nearly black trunks remain held. SDK author controls and RAW repeats are exact for all 47 cards under installed Unity/HDRP; no old SDK, Forge rendering, animation/VFX behavior, placement, runtime or GUI qualification.')
    write(O/'RESULT.v1.json',review);print(json.dumps(dict(cards_verified=44,images_verified=88,cards_held=3)))
if __name__=='__main__':main()
