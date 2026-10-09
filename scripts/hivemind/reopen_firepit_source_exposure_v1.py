"""Reopen measured HDRP exposure and its source/target scalar convention."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import math
import struct

W=Path('F:/OpenRPG_ImportWork')
CAP=W/'firepit-source-exposure-reference-v1'
ROOT=W/'firepit-source-exposure-reopen-v1'
PKG=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache'
HDRP=PKG/'com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime'


def read(path):
    return json.loads(path.read_text('utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream,'sha256').hexdigest()


def main():
    assert not ROOT.exists()
    run=read(CAP/'RUN-RESULT.v1.json');prep=read(CAP/'PREPARED.v1.json')
    native=read(CAP/'NATIVE-RESULT.v1.json')
    assert run['actual_exit_code']==0 and run['status']=='native_hdrp_actual_camera_exposure_gpu_captured'
    assert run['source_inputs_unchanged'] and not run['private_changed']
    for pins in [prep['input_pins'],prep['private_before_pins'],run['exposure_files']]:
        for path,digest in pins.items():assert sha(path)==digest,path
    compute=run['compute_input']
    assert sha(compute['path'])==compute['sha256']
    assert sha(compute['shader_config'])==compute['shader_config_sha256']
    original=read(W/'firepit-shadow-source-reference-v8/NATIVE-RESULT.v1.json')
    assert [row['raw_sha256'] for row in native['images']]==[row['raw_sha256'] for row in original['images']]
    rows=[];files=[]
    for view in range(2):
        raw=CAP/f'source-exposure-{view}.rgba32f';meta_path=raw.with_suffix('.rgba32f.json');meta=read(meta_path)
        assert len(raw.read_bytes())==meta['bytes']==16 and meta['gpu_load_readback']
        assert sha(raw)==meta['raw_sha256']
        assert meta['native_texture_format']=='R32G32_SFloat' and meta['source_history_channel']=='CustomUserHistory0'
        assert meta['pre_exposition_shader_option']==1
        rgba=struct.unpack('<4f',raw.read_bytes());assert all(math.isfinite(v) for v in rgba) and rgba[0]>0
        assert rgba[1]==10 and meta['probe_range_compression_factor']==1
        expected_f32=struct.unpack('<f',struct.pack('<f',2**(-rgba[1])/1.2))[0]
        assert rgba[0]==expected_f32
        rows.append(dict(view=view,gpu_rgba=list(rgba),ev100=rgba[1],
            probe_exposure_scale=1.0/max(meta['probe_range_compression_factor'],1e-6),
            effective_source_multiplier=rgba[0],target_cpu_formula_f32=expected_f32,
            source_and_target_cpu_scalar_equal=True))
        files.extend([raw,meta_path,CAP/f'SOURCE-STATE-{view}.json'])
    assert rows[0]['gpu_rgba']==rows[1]['gpu_rgba']
    sdk=[HDRP/'RenderPipeline/Camera/HDCamera.cs',HDRP/'RenderPipeline/HDRenderPipeline.PostProcess.cs',
        HDRP/'ShaderLibrary/ShaderVariables.hlsl',Path(compute['shader_config']),
        Path('C:/Users/karen/.cargo/registry/src/index.crates.io-1949cf8c6b5b557f/bevy_camera-0.19.1/src/camera.rs')]
    assert 'ops::exp2(-self.ev100) / 1.2' in sdk[-1].read_text('utf-8')
    assert 'LOAD_TEXTURE2D(_ExposureTexture, int2(0, 0)).x * _ProbeExposureScale' in sdk[2].read_text('utf-8')
    pins=[Path(__file__).resolve(),CAP/'RUN-RESULT.v1.json',CAP/'PREPARED.v1.json',CAP/'NATIVE-RESULT.v1.json',
        CAP/'Unity.log',Path(compute['path']),*sdk,*files]
    result=dict(status='actual_hdrp_exposure_gpu_readback_and_target_cpu_convention_verified',
        finished_utc=datetime.now(timezone.utc).isoformat(),source_exposure_texels_verified=2,
        source_dependencies_verified=len(prep['dependency_paths']),
        original_source_pair_unchanged=True,rows=rows,
        extra_exposure_correction_required_for_ev10=False,
        target_gpu_exposure_qualified=False,combined_diffuse_lighting_qualified=False,
        render_fidelity_qualified=False,editor_visual_verified=False,full_import=False,
        input_pins={str(p):sha(p) for p in pins},
        next_action='Apply measured diffuse irradiance with admitted source FGD and occlusion on both imported surface and receiver; use matching camera exposure once, then compare the original scene')
    ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))


if __name__=='__main__':
    main()
