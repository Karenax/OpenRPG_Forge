"""Verify every native sky face/mip and retain raw source half precision."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,math,struct
W=Path('F:/OpenRPG_ImportWork');CAP=W/'firepit-source-sky-reference-v1';ROOT=W/'firepit-source-sky-reopen-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not ROOT.exists()
    run=read(CAP/'RUN-RESULT.v1.json');prep=read(CAP/'PREPARED.v1.json');native=read(CAP/'NATIVE-RESULT.v1.json')
    assert run['actual_exit_code']==0 and run['source_inputs_unchanged'] and not run['private_changed']
    for pins in [prep['input_pins'],prep['private_before_pins'],run['sky_files']]:
        for p,h in pins.items():assert sha(p)==h,p
    compute=run['compute_input'];assert sha(compute['path'])==compute['sha256'] and sha(compute['sdk'])==compute['sdk_sha256']
    original=read(W/'firepit-shadow-source-reference-v8/NATIVE-RESULT.v1.json')
    assert [v['raw_sha256'] for v in native['images']]==[v['raw_sha256'] for v in original['images']]
    first=read(CAP/'source-sky-0-mip-0.rgba32f.json');levels=first['mipmap_count'];layers=first['layer_count'];width=first['base_width']
    assert width==256 and layers==6 and first['cubemap_count']==1 and levels==9
    rows=[];files=[];texels=0
    for mip in range(levels):
        raw=CAP/f'source-sky-0-mip-{mip}.rgba32f';meta=read(raw.with_suffix('.rgba32f.json'))
        pair=CAP/f'source-sky-1-mip-{mip}.rgba32f';paired=read(pair.with_suffix('.rgba32f.json'))
        assert sha(raw)==sha(pair)==meta['raw_sha256']==paired['raw_sha256']
        assert meta['width']==meta['height']==max(1,width>>mip)
        assert meta['layer_count']==paired['layer_count']==layers and meta['mip']==paired['mip']==mip
        assert meta['source_history_channel']==paired['source_history_channel']=='CustomUserHistory0'
        assert meta['native_texture_format']==paired['native_texture_format']=='R16G16B16A16_SFloat'
        assert meta['face_order']==['PositiveX','NegativeX','PositiveY','NegativeY','PositiveZ','NegativeZ']
        data=raw.read_bytes();count=meta['width']*meta['height']*layers
        assert len(data)==meta['bytes']==count*16 and meta['gpu_copy_then_load_readback']
        values=list(struct.iter_unpack('<4f',data));assert all(all(math.isfinite(v) for v in row) for row in values)
        # GPU Load converts stored half values exactly; reject any recoding loss.
        assert all(struct.unpack('<e',struct.pack('<e',v))[0]==v for row in values for v in row)
        rows.append(dict(mip=mip,width=meta['width'],texels_per_view=count,
            rgb_minimum=min(min(v[:3]) for v in values),rgb_maximum=max(max(v[:3]) for v in values),
            rgba32f_sha256=sha(raw),both_views_identical=True))
        texels+=count*2;files.extend([raw,raw.with_suffix('.rgba32f.json'),pair,pair.with_suffix('.rgba32f.json')])
    sdkroot=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache'
    sdk=[Path(compute['sdk']),sdkroot/'com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime/Sky/SkyRenderingContext.cs',
        sdkroot/'com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime/ShaderLibrary/ShaderVariablesFunctions.hlsl',
        sdkroot/'com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime/Lighting/LightEvaluation.hlsl',
        sdkroot/'com.unity.render-pipelines.core@a4c9a9793789/ShaderLibrary/ImageBasedLighting.hlsl']
    assert '6 * bsdfIdx + i' in sdk[0].read_text('utf-8')
    assert '#define UNITY_SPECCUBE_LOD_STEPS 6' in sdk[-1].read_text('utf-8')
    pins=[Path(__file__).resolve(),CAP/'RUN-RESULT.v1.json',CAP/'PREPARED.v1.json',CAP/'NATIVE-RESULT.v1.json',CAP/'Unity.log',Path(compute['path']),*sdk,*files]
    result=dict(status='native_prefiltered_sky_all_faces_mips_and_half_values_verified',finished_utc=datetime.now(timezone.utc).isoformat(),
        captured_views=2,cube_count=1,faces_per_cube=6,mip_levels=9,texels_verified=texels,
        source_shader_default_max_lod=6,source_dependencies_verified=len(prep['dependency_paths']),rows=rows,
        original_source_images_unchanged=True,half_precision_roundtrip_lossless=True,
        target_cube_sampling_qualified=False,source_sampling_witness_captured=False,render_fidelity_qualified=False,
        full_import=False,editor_visual_verified=False,input_pins={str(p):sha(p) for p in pins},
        next_action='Capture native source cube sampling directions and fractional LODs; admit a shared lossless cube on Forge, verify sampling before applying source specular FGD and AO.')
    ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()
