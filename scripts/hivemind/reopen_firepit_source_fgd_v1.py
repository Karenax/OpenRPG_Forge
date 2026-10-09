"""Reopen the native GPU LUT and pin its SDK decoding and coordinate contract."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,struct,math
W=Path('F:/OpenRPG_ImportWork');CAP=W/'firepit-source-fgd-reference-v1';ROOT=W/'firepit-source-fgd-reopen-v1'
PKG=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache'
HDRP=PKG/'com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime/Material'
CORE=PKG/'com.unity.render-pipelines.core@a4c9a9793789/ShaderLibrary'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def sample(pixels,ndv,roughness):
    x=math.sqrt(max(0.0001,min(1,ndv)))*63;y=roughness*63
    xi=math.floor(x);yi=math.floor(y);tx=x-xi;ty=y-yi
    def at(a,b):return pixels[max(0,min(63,b))*64+max(0,min(63,a))]
    return [(at(xi,yi)[c]*(1-tx)+at(xi+1,yi)[c]*tx)*(1-ty)+(at(xi,yi+1)[c]*(1-tx)+at(xi+1,yi+1)[c]*tx)*ty for c in range(4)]
def main():
    assert not ROOT.exists()
    run=read(CAP/'RUN-RESULT.v1.json');prep=read(CAP/'PREPARED.v1.json');native=read(CAP/'NATIVE-RESULT.v1.json')
    assert run['status']=='native_hdrp_fgd_gpu_load_captured' and run['actual_exit_code']==0 and not run['private_changed']
    for pins in [prep['input_pins'],prep['private_before_pins'],run['fgd_files']]:
        for p,h in pins.items():assert sha(p)==h,p
    assert sha(run['compute_input']['path'])==run['compute_input']['sha256']
    old=read(W/'firepit-shadow-source-reference-v8/NATIVE-RESULT.v1.json')
    assert [r['raw_sha256'] for r in native['images']]==[r['raw_sha256'] for r in old['images']]
    raw=CAP/'source-fgd.rgba32f';meta=read(raw.with_suffix(raw.suffix+'.json'));data=raw.read_bytes()
    assert len(data)==meta['bytes']==64*64*16 and meta['gpu_load_readback'] and meta['native_texture_format']=='A2B10G10R10_UNormPack32'
    pixels=list(struct.iter_unpack('<4f',data));assert len(pixels)==4096
    assert all(math.isfinite(v) and 0<=v<=1 for p in pixels for v in p)
    quantization=max(abs(v-round(v*1023)/1023) for p in pixels for v in p[:3]);assert quantization<2e-7
    sdk=[HDRP/'PreIntegratedFGD/PreIntegratedFGD.hlsl',HDRP/'PreIntegratedFGD/preIntegratedFGD_GGXDisneyDiffuse.shader',HDRP/'Lit/Lit.hlsl',CORE/'Common.hlsl']
    decoding=sdk[0].read_text('utf-8');assert 'diffuseFGD = preFGD.z + 0.5;' in decoding and 'sqrt(NdotV), perceptualRoughness' in decoding
    rows=[]
    for ndv in [0.0001,0.01,0.1,0.5,1]:
        for roughness in [-1,0,0.25,0.5,0.75,1,2]:
            s=sample(pixels,ndv,roughness);rows.append(dict(ndotv=ndv,perceptual_roughness=roughness,sampled_rgba=s,diffuse_fgd=s[2]+0.5))
    assert all(0.5<=r['diffuse_fgd']<=1.5 for r in rows)
    pins=[Path(__file__).resolve(),CAP/'RUN-RESULT.v1.json',CAP/'PREPARED.v1.json',CAP/'NATIVE-RESULT.v1.json',CAP/'Unity.log',Path(run['compute_input']['path']),raw,raw.with_suffix(raw.suffix+'.json'),*sdk]
    result=dict(status='native_hdrp_fgd_gpu_readback_and_sdk_contract_reopened',finished_utc=datetime.now(timezone.utc).isoformat(),texels_verified=4096,rgba_channels_verified=16384,source_dependencies_verified=len(prep['dependency_paths']),original_reference_pair_unchanged=True,native_texture_format=meta['native_texture_format'],quantization_error_max=quantization,
        channel_ranges=[dict(minimum=min(p[c] for p in pixels),maximum=max(p[c] for p in pixels)) for c in range(4)],cpu_sampling_cases=rows,cpu_cases_count=len(rows),
        runtime_gpu_sampling_qualified=False,render_fidelity_qualified=False,full_import=False,editor_visual_verified=False,input_pins={str(p):sha(p) for p in pins},
        next_action='Bind shared preintegrated BRDF data with its exact clamp/half-texel convention, validate target GPU sampling, then apply source diffuse FGD, occlusion and exposure to the measured SH')
    ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','cpu_sampling_cases']}))
if __name__=='__main__':main()
