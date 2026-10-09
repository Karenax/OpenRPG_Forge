"""Reopen eight native diagnostics; display encoding is not final render parity."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,struct,math
from PIL import Image
W=Path('F:/OpenRPG_ImportWork');CAP=W/'firepit-source-surface-debug-v1';ROOT=W/'firepit-source-surface-debug-reopen-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def gamma(v):return 12.92*v if v<=0.0031308 else 1.055*v**(1/2.4)-0.055
def main():
    assert not ROOT.exists()
    run=read(CAP/'RUN-RESULT.v1.json');prep=read(CAP/'PREPARED.v1.json');native=read(CAP/'NATIVE-RESULT.v1.json')
    assert run['status']=='original_hdrp_surface_and_lighting_debug_captured' and run['actual_exit_code']==0
    assert run['source_inputs_unchanged'] and not run['private_changed']
    for files in [prep['input_pins'],prep['private_before_pins'],run['debug_files']]:
        for file,h in files.items():assert sha(file)==h,file
    old=read(W/'firepit-shadow-source-reference-v8/NATIVE-RESULT.v1.json')
    assert [row['raw_sha256'] for row in native['images']]==[row['raw_sha256'] for row in old['images']]
    ROOT.mkdir();rows=[]
    for role in run['debug_roles']:
        raw=CAP/f'DEBUG-{role}.rgba16f';meta=read(raw.with_suffix(raw.suffix+'.json'));data=raw.read_bytes()
        assert len(data)==512*512*8 and meta['native_hdrp_debug_render'] and meta['raw_sha256']==sha(raw)
        pixels=list(struct.iter_unpack('<4e',data));assert all(math.isfinite(v) for p in pixels for v in p)
        rgb=[v for p in pixels for v in p[:3]]
        crop=[v for y in range(220,350) for x in range(145,365) for v in pixels[(511-y)*512+x][:3]]
        preview=ROOT/f'debug-preview-{role}.png'
        display=bytes(round(255*max(0,min(1,gamma(v)))) for y in range(511,-1,-1) for p in pixels[y*512:(y+1)*512] for v in p[:3])
        Image.frombytes('RGB',(512,512),display).save(preview)
        rows.append(dict(role=role,raw_sha256=sha(raw),minimum=min(rgb),maximum=max(rgb),crop_mean=sum(crop)/len(crop),crop_maximum=max(crop),preview=str(preview),preview_sha256=sha(preview)))
    assert len(rows)==8 and len({r['raw_sha256'] for r in rows})>=4
    files=[Path(__file__).resolve(),CAP/'RUN-RESULT.v1.json',CAP/'PREPARED.v1.json',CAP/'NATIVE-RESULT.v1.json',CAP/'Unity.log']
    result=dict(status='eight_original_hdrp_surface_debug_images_reopened',finished_utc=datetime.now(timezone.utc).isoformat(),images_verified=8,raw_pixels_reopened=8*512*512,source_dependencies_verified=len(prep['dependency_paths']),original_shadow_pair_raw_bytes_identical=True,
        rows=rows,preview_transfer='CPU linear-to-sRGB, clamp, vertical row reversal for display only; SDK debug encoding still to interpret',
        full_import=False,editor_visual_verified=False,material_parity_qualified=False,render_fidelity_qualified=False,input_pins={str(p):sha(p) for p in files},
        next_action='Interpret pinned HDRP debug encodings; capture matching converted surface outputs and compare before changing lighting transport')
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()
