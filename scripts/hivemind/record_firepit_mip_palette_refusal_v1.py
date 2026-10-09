"""Retain the failed integer-palette assumption without relaxing its protocol."""
from pathlib import Path
from datetime import datetime,timezone
import json
import struct
import hashlib
from collections import Counter

W=Path('F:/OpenRPG_ImportWork');ROOT=W/'firepit-mip-palette-refusal-v1'

def sha(p):
    with p.open('rb') as f:
        return hashlib.file_digest(f,'sha256').hexdigest()

def main():
    assert not ROOT.exists()
    source=W/'firepit-source-mip-palette-v1';native=W/'firepit-mip-palette-native-v1';plan=W/'firepit-mip-palette-comparison-plan-v1/PROTOCOL.v1.json'
    protocol=json.loads(plan.read_text('utf-8-sig'))
    a=list(struct.iter_unpack('<4e',(source/'MIP-PROBE-0.rgba16f').read_bytes()))
    a=[p for y in range(511,-1,-1) for p in a[y*512:(y+1)*512]]
    b=list(struct.iter_unpack('<4e',(native/'gpu-0.rgba16f').read_bytes()))
    c=list(struct.iter_unpack('<4e',(source/'MIP-PROBE-1.rgba16f').read_bytes()))
    c=[p for y in range(511,-1,-1) for p in c[y*512:(y+1)*512]]
    assert len(a)==len(b)==len(c)==512*512
    invalid=[];histogram=Counter();model=0;equal_modes=0
    for i,(s,t,q) in enumerate(zip(a,b,c)):
        if t[3]!=.5:continue
        model+=1;equal_modes+=s[:3]==q[:3]
        level=int(round(s[0]*16))
        histogram[(s[0],s[1],s[2],int(round(t[0]*16)))]+=1
        if 0<=level<=11 and max(s[:3])-min(s[:3])<protocol['maximum_source_palette_rgb_spread'] and abs(s[0]*16-level)>=protocol['maximum_palette_id_rounding_distance']:
            invalid.append(i)
    assert invalid
    pins={}
    for folder in [source,native]:
        receipt=json.loads((folder/'RUN-RESULT.v1.json').read_text('utf-8-sig'))
        assert receipt['actual_exit_code']==0
        prep=json.loads((folder/'PREPARED.v1.json').read_text('utf-8-sig'))
        for path,expected in prep['input_pins'].items():
            assert sha(Path(path))==expected,path
            pins[path]=expected
    for p in [Path(__file__).resolve(),plan,source/'MIP-PROBE-0.rgba16f',source/'MIP-PROBE-1.rgba16f',source/'RUN-RESULT.v1.json',native/'gpu-0.rgba16f',native/'RUN-RESULT.v1.json']:
        pins[str(p)]=sha(p)
    result=dict(status='source_mip_palette_integer_id_protocol_refused',finished_utc=datetime.now(timezone.utc).isoformat(),model_pixels=model,source_gray_pixels_outside_declared_integer_rounding_distance=len(invalid),source_palette_and_hardware_lod_identical_rgb_model_pixels=equal_modes,most_common_values=[dict(source_rgb=list(k[:3]),native_mip=k[3],pixels=v) for k,v in histogram.most_common(12)],protocol_preserved=True,quality_override_hypothesis_not_yet_proven=True,source_gradient_values_not_read_back=True,full_import=False,render_fidelity_qualified=False,vault_assets_updated=0,complete_assets_delivered_increment=0,input_pins=pins,next_action='Measure live Unity quality anisotropy and compare unchanged source shaders with only private quality anisotropy disabled.')
    ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','most_common_values']}))

if __name__=='__main__':main()
