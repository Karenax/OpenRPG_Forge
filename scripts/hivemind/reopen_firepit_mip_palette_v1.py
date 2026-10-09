"""Compare actual implicit mip palette IDs; retain silhouette exclusions explicitly."""
from pathlib import Path
from datetime import datetime, timezone
import json
import hashlib
import math
import struct
from collections import Counter

W=Path('F:/OpenRPG_ImportWork')
SOURCE=W/'firepit-source-mip-palette-v1'
NATIVE=W/'firepit-mip-palette-native-v1'
ROOT=W/'firepit-mip-palette-reopen-v1'
PLAN=W/'firepit-mip-palette-comparison-plan-v1'

def read(p):
    return json.loads(p.read_text('utf-8-sig'))

def sha(p):
    with p.open('rb') as f:
        return hashlib.file_digest(f,'sha256').hexdigest()

def source_pixels(path):
    raw=list(struct.iter_unpack('<4e',path.read_bytes()))
    assert len(raw)==512*512
    return [p for y in range(511,-1,-1) for p in raw[y*512:(y+1)*512]]

def main():
    assert not ROOT.exists()
    plan=read(PLAN/'PROTOCOL.v1.json')
    assert plan['gpu_pixels_absent_at_definition'] and plan['maximum_mip_mismatch_fraction']==0
    native=read(NATIVE/'RUN-RESULT.v1.json');prep=read(NATIVE/'PREPARED.v1.json')
    source=read(SOURCE/'RUN-RESULT.v1.json');sp=read(SOURCE/'PREPARED.v1.json');derivation=read(SOURCE/'DERIVATION.v1.json')
    assert native['actual_exit_code']==0 and source['actual_exit_code']==0
    assert source['source_inputs_unchanged'] and not source['private_changed'] and len(sp['dependency_paths'])==24
    pins={}
    for collection in [prep['input_pins'],sp['input_pins']]:
        for p,expected in collection.items():
            assert sha(Path(p))==expected,p
            pins[p]=expected
    for path,key in [(derivation['original_shader'],'original_sha256'),(derivation['private_shader'],'private_sha256')]:
        assert sha(Path(path))==derivation[key]
        pins[path]=derivation[key]
    for view in range(2):
        for rel in [f'images/{view}.png',f'linear-{view}.rgba16f']:
            a=SOURCE/rel;b=W/'firepit-shadow-source-reference-v8'/rel
            assert sha(a)==sha(b)
            pins[str(a)]=sha(a);pins[str(b)]=sha(b)
    for path,expected in source['probe_files'].items():
        assert sha(Path(path))==expected
        pins[path]=expected
    palette=source_pixels(SOURCE/'MIP-PROBE-0.rgba16f')
    lod=source_pixels(SOURCE/'MIP-PROBE-1.rgba16f')
    rows=[]
    for view in range(2):
        raw=NATIVE/f'gpu-{view}.rgba16f';meta=NATIVE/f'gpu-{view}.metadata.json'
        assert read(meta)['format']=='Rgba16Float' and raw.stat().st_size==512*512*8
        current=read(NATIVE/f'CAMERA-STATE-{view}.json')
        original=read(W/f'firepit-surface-instance-fix-v1/CAMERA-STATE-{view}.json')
        assert current==original
        histogram=Counter();target_counts=Counter();source_counts=Counter();delta_counts=Counter()
        mismatch=0;outside=0;model=0;compared=0;lod_delta_sum=0.0
        for i,p in enumerate(struct.iter_unpack('<4e',raw.read_bytes())):
            assert all(math.isfinite(v) for v in p)
            if p[3]!=.5:
                continue
            model+=1
            target=int(round(p[0]*16));s=palette[i];source_id=int(round(s[0]*16))
            assert 0<=target<=11 and max(p[:3])-min(p[:3])<1e-6
            assert abs(p[0]*16-target)<plan['maximum_palette_id_rounding_distance']
            if source_id<0 or source_id>11 or max(s[:3])-min(s[:3])>=plan['maximum_source_palette_rgb_spread']:
                outside+=1
                continue
            assert abs(s[0]*16-source_id)<plan['maximum_palette_id_rounding_distance']
            assert all(math.isfinite(v) for v in lod[i])
            compared+=1;histogram[f'{source_id}->{target}']+=1
            source_counts[source_id]+=1;target_counts[target]+=1;delta_counts[target-source_id]+=1
            mismatch+=target!=source_id
            lod_delta_sum+=abs(target-max(0,min(11,lod[i][0]*16)))
        assert model>0 and compared>0
        rows.append(dict(view=view,model_pixels=model,source_pixels_not_admitted_by_palette_protocol=outside,compared_palette_pixels=compared,mip_mismatches=mismatch,mismatch_fraction=mismatch/compared,pairs=dict(histogram),source_mip_histogram=dict(source_counts),target_mip_histogram=dict(target_counts),target_minus_source_mip_histogram=dict(delta_counts),target_vs_source_continuous_hardware_lod_mean_absolute_difference=lod_delta_sum/compared))
        for p in [raw,meta,NATIVE/f'CAMERA-STATE-{view}.json',W/f'firepit-surface-instance-fix-v1/CAMERA-STATE-{view}.json']:
            pins[str(p)]=sha(p)
    for p in [Path(__file__).resolve(),PLAN/'PROTOCOL.v1.json',SOURCE/'RUN-RESULT.v1.json',SOURCE/'PREPARED.v1.json',SOURCE/'DERIVATION.v1.json',NATIVE/'RUN-RESULT.v1.json',NATIVE/'PREPARED.v1.json',NATIVE/'GPU-PAIR.v1.json']:
        pins[str(p)]=sha(p)
    result=dict(status='implicit_mip_palette_selection_equal_at_all_compared_model_pixels' if all(r['mip_mismatches']==0 for r in rows) else 'implicit_mip_palette_selection_discrepancy_measured',finished_utc=datetime.now(timezone.utc).isoformat(),rows=rows,source_dependencies_verified=24,source_reference_images_byte_identical=True,sampler_cloned_from_actual_native_texture=True,private_palette_only=True,original_shader_and_textures_preserved=True,source_geometry_unchanged=True,input_pins=pins,source_pins_verified=len(pins),texture_gradient_values_not_read_back=True,original_texture_sampling_fidelity_qualified=False,full_import=False,editor_visual_verified=False,vault_assets_updated=0,complete_assets_delivered_increment=0)
    ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))

if __name__=='__main__':
    import sys
    if sys.argv[-1]=='plan':
        assert not PLAN.exists() and not any(NATIVE.glob('gpu-*.rgba16f'))
        PLAN.mkdir()
        protocol=dict(created_utc=datetime.now(timezone.utc).isoformat(),gpu_pixels_absent_at_definition=True,maximum_mip_mismatch_fraction=0,maximum_palette_id_rounding_distance=.25,maximum_source_palette_rgb_spread=.00001,palette='12 levels, level m is round(m*255/16) in all RGB channels, RGBA8 linear',source_exclusion='native model pixel whose source RGB is not a gray palette level 0..11; excluded count retained',camera_and_geometry_must_equal_previous_capture=True,full_import=False)
        (PLAN/'PROTOCOL.v1.json').write_text(json.dumps(protocol,indent=2),'utf-8')
        print(json.dumps(protocol))
    else:
        main()
