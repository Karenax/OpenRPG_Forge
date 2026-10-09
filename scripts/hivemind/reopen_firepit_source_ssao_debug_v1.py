"""Reopen the SSAO diagnostic; this is not target transport qualification."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import math
import struct

W=Path('F:/OpenRPG_ImportWork')
CAP=W/'firepit-source-ssao-debug-v1'
ROOT=W/'firepit-source-ssao-debug-reopen-v1'
def read(p): return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f: return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not ROOT.exists()
    run=read(CAP/'RUN-RESULT.v1.json');prep=read(CAP/'PREPARED.v1.json')
    assert run['actual_exit_code']==0 and run['source_inputs_unchanged'] and not run['private_changed']
    for pins in [prep['input_pins'],prep['private_before_pins'],run['debug_files']]:
        for path,digest in pins.items(): assert sha(path)==digest,path
    raw=CAP/'DEBUG-ScreenSpaceAmbientOcclusion.rgba16f'
    meta=read(raw.with_suffix('.rgba16f.json'))
    assert meta['width']==meta['height']==512 and sha(raw)==meta['raw_sha256']
    data=raw.read_bytes();assert len(data)==512*512*8
    rows=list(struct.iter_unpack('<4e',data))
    assert all(all(math.isfinite(v) for v in r) and r[0]==r[1]==r[2] and 0<=r[0]<=1 for r in rows)
    sdk=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime/Debug/DebugFullScreen.shader'
    assert 'return 1.0f - SAMPLE_TEXTURE2D_X(_DebugFullScreenTexture, s_point_clamp_sampler, input.texcoord).xxxx;' in sdk.read_text('utf-8')
    pins=[Path(__file__).resolve(),sdk,raw,raw.with_suffix('.rgba16f.json'),CAP/'RUN-RESULT.v1.json',CAP/'PREPARED.v1.json',CAP/'Unity.log']
    result=dict(status='actual_ssao_fullscreen_diagnostic_pixels_reopened',finished_utc=datetime.now(timezone.utc).isoformat(),
        pixels_verified=len(rows),finite_gray_pixels=len(rows),minimum_diagnostic_visibility=min(r[0] for r in rows),
        maximum_diagnostic_visibility=max(r[0] for r in rows),pixels_below_0999=sum(r[0]<.999 for r in rows),
        source_dependencies_verified=len(prep['dependency_paths']),sdk_debug_encoding='1 - sampled SSAO red channel',
        target_ssao_transport_qualified=False,render_fidelity_qualified=False,editor_visual_verified=False,full_import=False,
        input_pins={str(p):sha(p) for p in pins},
        next_action='Apply measured diffuse lighting with explicit Deferred material AO semantics; qualify screen AO sampling independently before full render comparison.')
    ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__': main()
